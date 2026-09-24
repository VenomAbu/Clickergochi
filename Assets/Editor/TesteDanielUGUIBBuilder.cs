#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TesteDanielUGUIBBuilder
{
    private const string SourcePath = "Assets/_Project/Scene/TesteDaniel.unity";
    private const string TargetPath = "Assets/_Project/Scene/TesteDaniel_UGUI.unity";
    private const string CanvasName = "TesteDanielCanvas";

    private static Sprite uiSprite;
    private static Sprite backgroundSprite;
    private static Sprite slimeSprite;
    private static Sprite roachSprite;
    private static TMP_FontAsset font;

    private static readonly Color ScreenBlue = new Color(0.227f, 0.275f, 0.431f, 1f);
    private static readonly Color DarkOverlay = new Color(0.08f, 0.06f, 0.16f, 0.62f);
    private static readonly Color White = Color.white;
    private static readonly Color Navy = new Color(0.235f, 0.196f, 0.353f, 1f);
    private static readonly Color LightPurple = new Color(0.941f, 0.922f, 1f, 1f);
    private static readonly Color BorderPurple = new Color(0.784f, 0.706f, 0.941f, 1f);
    private static readonly Color Green = new Color(0.588f, 0.863f, 0.471f, 1f);
    private static readonly Color HungerColor = new Color(1f, 0.667f, 0.471f, 1f);
    private static readonly Color FunColor = new Color(0.549f, 0.745f, 1f, 1f);
    private static readonly Color HygieneColor = new Color(0.549f, 0.902f, 0.863f, 1f);

    [MenuItem("Tools/Clickergochi/Create TesteDaniel uGUI Copy")]
    public static void CreateAndBuild()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
            {
                Debug.LogError($"Não foi possível copiar {SourcePath} para {TargetPath}.");
                return;
            }
            AssetDatabase.SaveAssets();
        }

        Scene scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
        Build(scene);
    }

    [MenuItem("Tools/Clickergochi/Rebuild TesteDaniel uGUI Copy")]
    public static void Rebuild()
    {
        Build(SceneManager.GetActiveScene());
    }

    private static void Build(Scene scene)
    {
        if (scene.path != TargetPath)
            Debug.LogWarning($"Abra {TargetPath} antes de reconstruir a UI.");

        LoadAssets();

        GameObject legacyUi = FindRoot(scene, "MainUI") ?? FindRoot(scene, "Legacy_UIToolkit_Disabled");
        if (legacyUi != null)
            UnityEngine.Object.DestroyImmediate(legacyUi);

        GameObject legacyRoaches = FindRoot(scene, "CockroachManager") ?? FindRoot(scene, "Legacy_CockroachManager_Disabled");
        if (legacyRoaches != null)
            UnityEngine.Object.DestroyImmediate(legacyRoaches);

        SlimeManager slimeManager = UnityEngine.Object.FindFirstObjectByType<SlimeManager>();
        if (slimeManager == null)
        {
            GameObject petRoot = FindRoot(scene, "PetSystems");
            if (petRoot == null)
                petRoot = new GameObject("PetSystems");
            slimeManager = petRoot.GetComponent<SlimeManager>();
            if (slimeManager == null)
                slimeManager = petRoot.AddComponent<SlimeManager>();
        }

        EnsureEventSystem();
        GameObject oldCanvas = FindRoot(scene, CanvasName);
        if (oldCanvas != null)
            UnityEngine.Object.DestroyImmediate(oldCanvas);

        var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AudioSource), typeof(TesteDanielUGUIController));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        AudioSource audio = canvasGo.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        TesteDanielUGUIController hud = canvasGo.GetComponent<TesteDanielUGUIController>();
        hud.slimeManager = slimeManager;
        hud.audioSource = audio;
        hud.slimeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/squish-cut.mp3");

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        GameObject background = CreateImage("Background", canvasRect, White, backgroundSprite, false, false);
        Stretch(background.GetComponent<RectTransform>());

        GameObject main = CreatePanel("MainScreen", canvasRect, new Color(0f, 0f, 0f, 0f), false);
        Stretch(main.GetComponent<RectTransform>());
        GameObject shop = CreatePanel("ShopScreen", canvasRect, ScreenBlue, false);
        Stretch(shop.GetComponent<RectTransform>());
        GameObject detail = CreatePanel("ShopDetailScreen", canvasRect, ScreenBlue, false);
        Stretch(detail.GetComponent<RectTransform>());
        GameObject stickers = CreatePanel("StickersScreen", canvasRect, ScreenBlue, false);
        Stretch(stickers.GetComponent<RectTransform>());
        GameObject food = CreatePanel("FoodScreen", canvasRect, ScreenBlue, false);
        Stretch(food.GetComponent<RectTransform>());
        GameObject furniturePopup = CreatePanel("FurniturePopup", canvasRect, DarkOverlay, false);
        Stretch(furniturePopup.GetComponent<RectTransform>());
        GameObject skinPopup = CreatePanel("SkinPopup", canvasRect, DarkOverlay, false);
        Stretch(skinPopup.GetComponent<RectTransform>());
        GameObject stickerPopup = CreatePanel("StickerPopup", canvasRect, DarkOverlay, false);
        Stretch(stickerPopup.GetComponent<RectTransform>());

        var refs = new HudRefs();
        BuildMain(main.GetComponent<RectTransform>(), refs, slimeSprite, roachSprite);
        BuildShop(shop.GetComponent<RectTransform>(), refs);
        BuildShopDetail(detail.GetComponent<RectTransform>(), refs);
        BuildStickers(stickers.GetComponent<RectTransform>(), refs);
        BuildFood(food.GetComponent<RectTransform>(), refs);
        BuildFurniturePopup(furniturePopup.GetComponent<RectTransform>(), refs);
        BuildSkinPopup(skinPopup.GetComponent<RectTransform>(), refs);
        BuildStickerPopup(stickerPopup.GetComponent<RectTransform>(), refs);
        AssignController(hud, refs, main, shop, detail, stickers, food, furniturePopup, skinPopup, stickerPopup);

        SetActive(shop, false);
        SetActive(detail, false);
        SetActive(stickers, false);
        SetActive(food, false);
        SetActive(furniturePopup, false);
        SetActive(skinPopup, false);
        SetActive(stickerPopup, false);
        SetActive(main, true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = canvasGo;
        Debug.Log("Cena TesteDaniel_UGUI criada com todos os elementos uGUI serializados na Scene.");
    }

    private sealed class HudRefs
    {
        public Button slimeButton;
        public Button muteButton;
        public Button shopButton;
        public Button stickersButton;
        public Button foodButton;
        public Image slimeImage;
        public Image levelFill;
        public Image hungerFill;
        public Image funFill;
        public Image hygieneFill;
        public TextMeshProUGUI levelText;
        public List<TextMeshProUGUI> slimeCountTexts = new List<TextMeshProUGUI>();
        public TextMeshProUGUI hungerText;
        public TextMeshProUGUI funText;
        public TextMeshProUGUI hygieneText;
        public TextMeshProUGUI moodText;
        public TextMeshProUGUI muteText;
        public Button[] furnitureButtons;
        public Button[] roachButtons;
        public RectTransform critterLayer;
        public Button[] shopOptionButtons;
        public Button[] stickerButtons;
        public Button[] foodBuyButtons;
        public TextMeshProUGUI[] foodNameTexts;
        public TextMeshProUGUI[] foodPriceTexts;
        public Button[] skinCards;
        public Button shopBackButton;
        public Button detailBackButton;
        public Button stickersBackButton;
        public Button foodBackButton;
        public Button closeFurnitureButton;
        public Button closeStickerButton;
        public Button skinButton;
        public Button skinBackButton;
        public Button skinLeftButton;
        public Button skinRightButton;
        public Button upgradeButton;
        public TextMeshProUGUI furnitureTitleText;
        public TextMeshProUGUI skinTitleText;
        public TextMeshProUGUI skinSelectionText;
        public TextMeshProUGUI detailTitleText;
        public TextMeshProUGUI stickerTitleText;
        public TextMeshProUGUI stickerDescriptionText;
        public TextMeshProUGUI stickerThumbText;
    }

    private static void BuildMain(RectTransform parent, HudRefs refs, Sprite slimeAsset, Sprite roachAsset)
    {
        GameObject critterLayer = CreatePanel("CritterLayer", parent, new Color(0f, 0f, 0f, 0f), false);
        Stretch(critterLayer.GetComponent<RectTransform>());
        refs.critterLayer = critterLayer.GetComponent<RectTransform>();
        refs.roachButtons = new Button[3];
        Vector2[] roachPositions = { new Vector2(-180f, 260f), new Vector2(120f, 100f), new Vector2(210f, -160f) };
        for (int i = 0; i < 3; i++)
        {
            Button roach = CreateButton("Roach" + i, critterLayer.transform, Color.clear, new Vector2(68f, 80f), roachPositions[i]);
            GameObject image = CreateImage("RoachImage", roach.transform, White, roachAsset, false, true);
            Stretch(image.GetComponent<RectTransform>());
            refs.roachButtons[i] = roach;
        }

        GameObject topArea = CreatePanel("TopArea", parent, new Color(0f, 0f, 0f, 0f), false);
        SetTop(topArea.GetComponent<RectTransform>(), 300f, 14f, 16f, 16f);
        GameObject levelBar = CreateImage("LevelBar", topArea.transform, new Color(0.235f, 0.196f, 0.353f, 0.25f), uiSprite, false, false);
        SetTopCenter(levelBar.GetComponent<RectTransform>(), new Vector2(688f, 40f), new Vector2(0f, -14f));
        GameObject levelFill = CreateImage("LevelFill", levelBar.transform, Green, uiSprite, false, false);
        SetTopLeftSize(levelFill.GetComponent<RectTransform>(), new Vector2(275f, 34f), new Vector2(3f, -3f));
        levelFill.GetComponent<Image>().type = Image.Type.Filled;
        levelFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;
        levelFill.GetComponent<Image>().fillAmount = 0.4f;
        refs.levelFill = levelFill.GetComponent<Image>();

        GameObject controls = CreatePanel("TopControls", topArea.transform, new Color(0f, 0f, 0f, 0f), false);
        SetTop(controls.GetComponent<RectTransform>(), 225f, 78f, 0f, 0f);
        Button mute = CreateButton("MuteButton", controls.transform, White, new Vector2(80f, 80f), Vector2.zero);
        SetTopLeftSize(mute.GetComponent<RectTransform>(), new Vector2(80f, 80f), new Vector2(0f, 0f));
        refs.muteButton = mute;
        refs.muteText = AddText("MuteText", mute.transform, "ON", 24, Navy, TextAlignmentOptions.Center);
        Stretch(refs.muteText.rectTransform);

        GameObject levelBlock = CreatePanel("LevelBlock", controls.transform, new Color(0f, 0f, 0f, 0f), false);
        SetTopCenter(levelBlock.GetComponent<RectTransform>(), new Vector2(270f, 120f), new Vector2(0f, 0f));
        refs.levelText = AddText("LevelText", levelBlock.transform, "Nv 1 • 40%", 22, White, TextAlignmentOptions.Center);
        SetTopCenter(refs.levelText.GetComponent<RectTransform>(), new Vector2(260f, 32f), new Vector2(0f, 0f));
        GameObject slimeCounter = CreatePanel("SlimeCounter", levelBlock.transform, White, false);
        SetTopCenter(slimeCounter.GetComponent<RectTransform>(), new Vector2(190f, 60f), new Vector2(0f, -48f));
        GameObject dot = CreateImage("SlimeDot", slimeCounter.transform, Green, uiSprite, false, false);
        SetCenter(dot.GetComponent<RectTransform>(), new Vector2(-58f, 0f), new Vector2(42f, 42f));
        TextMeshProUGUI count = AddText("SlimeCountLabel", slimeCounter.transform, "x 0", 26, Navy, TextAlignmentOptions.Center);
        SetCenter(count.GetComponent<RectTransform>(), new Vector2(28f, 0f), new Vector2(95f, 45f));
        refs.slimeCountTexts.Add(count);

        GameObject statsColumn = CreatePanel("StatsColumn", controls.transform, new Color(0f, 0f, 0f, 0f), false);
        SetTopRightSize(statsColumn.GetComponent<RectTransform>(), new Vector2(100f, 230f), new Vector2(0f, 0f));
        refs.hungerFill = BuildStat(statsColumn.transform, "StatHunger", "F", HungerColor, 0f, refs);
        refs.funFill = BuildStat(statsColumn.transform, "StatFun", "D", FunColor, -76f, refs);
        refs.hygieneFill = BuildStat(statsColumn.transform, "StatHygiene", "H", HygieneColor, -152f, refs);

        GameObject middle = CreatePanel("MiddleArea", parent, new Color(0f, 0f, 0f, 0f), false);
        SetMiddle(middle.GetComponent<RectTransform>(), 300f, 170f, 16f, 16f);
        GameObject furnitureRow = CreatePanel("FurnitureRow", middle.transform, new Color(0f, 0f, 0f, 0f), false);
        SetTopCenter(furnitureRow.GetComponent<RectTransform>(), new Vector2(600f, 130f), new Vector2(0f, -5f));
        refs.furnitureButtons = new Button[3];
        string[] furnitureLabels = { "Cama", "Sofá", "Planta" };
        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton("Furniture" + i, furnitureRow.transform, White, new Vector2(120f, 120f), new Vector2((i - 1) * 144f, 0f));
            AddText("Label", button.transform, furnitureLabels[i], 22, Navy, TextAlignmentOptions.Center);
            refs.furnitureButtons[i] = button;
        }

        Button slime = CreateButton("SlimeButton", middle.transform, Green, new Vector2(210f, 210f), new Vector2(0f, -70f));
        refs.slimeButton = slime;
        GameObject slimeImage = CreateImage("SlimeImage", slime.transform, White, slimeAsset, false, true);
        SetCenter(slimeImage.GetComponent<RectTransform>(), Vector2.zero, new Vector2(170f, 170f));
        refs.slimeImage = slimeImage.GetComponent<Image>();
        refs.moodText = AddText("MoodText", slime.transform, ":)", 28, White, TextAlignmentOptions.Bottom);
        SetBottomCenter(refs.moodText.GetComponent<RectTransform>(), new Vector2(0f, 16f), new Vector2(180f, 35f));

        GameObject bottomNav = CreatePanel("BottomNav", parent, White, false);
        SetBottom(bottomNav.GetComponent<RectTransform>(), 140f, 16f, 16f, 16f);
        refs.shopButton = CreateButton("ShopButton", bottomNav.transform, LightPurple, new Vector2(104f, 104f), new Vector2(-120f, 0f));
        AddText("Label", refs.shopButton.transform, "Loja", 21, Navy, TextAlignmentOptions.Center);
        refs.stickersButton = CreateButton("StickersButton", bottomNav.transform, LightPurple, new Vector2(104f, 104f), Vector2.zero);
        AddText("Label", refs.stickersButton.transform, "Stickers", 19, Navy, TextAlignmentOptions.Center);
        refs.foodButton = CreateButton("FoodButton", bottomNav.transform, LightPurple, new Vector2(104f, 104f), new Vector2(120f, 0f));
        AddText("Label", refs.foodButton.transform, "Comidas", 19, Navy, TextAlignmentOptions.Center);
    }

    private static Image BuildStat(Transform parent, string name, string icon, Color color, float y, HudRefs refs)
    {
        GameObject ring = CreateImage(name, parent, White, uiSprite, false, false);
        SetTopCenter(ring.GetComponent<RectTransform>(), new Vector2(84f, 84f), new Vector2(0f, y));
        GameObject fill = CreateImage(name + "Fill", ring.transform, color, uiSprite, false, false);
        SetCenter(fill.GetComponent<RectTransform>(), Vector2.zero, new Vector2(60f, 60f));
        Image fillImage = fill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillAmount = 0.5f;
        TextMeshProUGUI glyph = AddText("StatIcon", ring.transform, icon, 24, Navy, TextAlignmentOptions.Center);
        Stretch(glyph.GetComponent<RectTransform>());
        TextMeshProUGUI pct = AddText(name + "Pct", parent, "50%", 16, Navy, TextAlignmentOptions.Center);
        SetTopCenter(pct.GetComponent<RectTransform>(), new Vector2(100f, 24f), new Vector2(0f, y + 43f));
        if (name == "StatHunger") refs.hungerText = pct;
        else if (name == "StatFun") refs.funText = pct;
        else refs.hygieneText = pct;
        return fillImage;
    }

    private static void BuildShop(RectTransform parent, HudRefs refs)
    {
        AddText("ShopTitle", parent, "Loja", 42, White, TextAlignmentOptions.Center).GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 570f);
        AddSlimeCounter(parent, "ShopSlimeCounter", new Vector2(0f, 500f), refs);
        GameObject options = CreatePanel("ShopOptions", parent, new Color(0f, 0f, 0f, 0f), false);
        SetMiddle(options.GetComponent<RectTransform>(), 120f, 120f, 18f, 18f);
        refs.shopOptionButtons = new Button[3];
        string[] labels = { "Pacote de Stickers", "Skin dos Slimes", "Skin dos Móveis" };
        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton("Option" + i, options.transform, White, new Vector2(360f, 100f), new Vector2(0f, 210f - i * 120f));
            AddText("Label", button.transform, labels[i], 24, Navy, TextAlignmentOptions.Center);
            refs.shopOptionButtons[i] = button;
        }
        refs.shopBackButton = CreateButton("ShopBack", parent, White, new Vector2(260f, 72f), new Vector2(0f, -570f));
        AddText("Label", refs.shopBackButton.transform, "← Voltar", 25, Navy, TextAlignmentOptions.Center);
    }

    private static void BuildShopDetail(RectTransform parent, HudRefs refs)
    {
        refs.detailTitleText = AddText("DetailTitle", parent, "Detalhe", 42, White, TextAlignmentOptions.Center);
        refs.detailTitleText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 570f);
        AddSlimeCounter(parent, "DetailSlimeCounter", new Vector2(0f, 500f), refs);
        AddText("DetailPlaceholder", parent, "Conteúdo em breve...", 24, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center).GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        refs.detailBackButton = CreateButton("DetailBack", parent, White, new Vector2(260f, 72f), new Vector2(0f, -570f));
        AddText("Label", refs.detailBackButton.transform, "← Voltar", 25, Navy, TextAlignmentOptions.Center);
    }

    private static void BuildStickers(RectTransform parent, HudRefs refs)
    {
        AddText("StickersTitle", parent, "Stickers!!", 42, White, TextAlignmentOptions.Center).GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 570f);
        AddSlimeCounter(parent, "StickersSlimeCounter", new Vector2(0f, 500f), refs);
        GameObject grid = CreatePanel("StickersGrid", parent, new Color(0f, 0f, 0f, 0f), false);
        SetMiddle(grid.GetComponent<RectTransform>(), 135f, 120f, 12f, 12f);
        refs.stickerButtons = new Button[12];
        string[] labels = { "Estrela", "Arco-íris", "Foguinho", "Trevo", "Balão", "Musiquinha", "Patinha", "Diamante", "Rosquinha", "Foguete", "Estrelinha", "Redemoinho" };
        for (int i = 0; i < 12; i++)
        {
            int col = i % 3;
            int row = i / 3;
            Button button = CreateButton("Sticker" + i, grid.transform, White, new Vector2(165f, 110f), new Vector2((col - 1) * 180f, 260f - row * 120f));
            AddText("Label", button.transform, labels[i], 17, Navy, TextAlignmentOptions.Center);
            refs.stickerButtons[i] = button;
        }
        refs.stickersBackButton = CreateButton("StickersBack", parent, White, new Vector2(260f, 72f), new Vector2(0f, -570f));
        AddText("Label", refs.stickersBackButton.transform, "← Voltar", 25, Navy, TextAlignmentOptions.Center);
    }

    private static void BuildFood(RectTransform parent, HudRefs refs)
    {
        AddText("FoodTitle", parent, "Alimento", 42, White, TextAlignmentOptions.Center).GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 570f);
        AddSlimeCounter(parent, "FoodSlimeCounter", new Vector2(0f, 500f), refs);
        FoodItemData[] foods = Resources.LoadAll<FoodItemData>("Food").OrderBy(f => f.order).ToArray();
        GameObject list = CreatePanel("FoodList", parent, new Color(0f, 0f, 0f, 0f), false);
        SetMiddle(list.GetComponent<RectTransform>(), 135f, 120f, 18f, 18f);
        refs.foodBuyButtons = new Button[foods.Length];
        refs.foodNameTexts = new TextMeshProUGUI[foods.Length];
        refs.foodPriceTexts = new TextMeshProUGUI[foods.Length];
        for (int i = 0; i < foods.Length; i++)
        {
            FoodItemData item = foods[i];
            GameObject card = CreatePanel("Food_" + i, list.transform, White, false);
            SetTopCenter(card.GetComponent<RectTransform>(), new Vector2(560f, 96f), new Vector2(0f, -i * 110f));
            GameObject icon = CreateImage("FoodImage", card.transform, LightPurple, uiSprite, false, false);
            SetCenter(icon.GetComponent<RectTransform>(), new Vector2(-225f, 0f), new Vector2(72f, 72f));
            string initial = string.IsNullOrEmpty(item.foodName) ? "F" : item.foodName.Substring(0, 1).ToUpperInvariant();
            AddText("Initial", icon.transform, initial, 30, Navy, TextAlignmentOptions.Center).GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            refs.foodNameTexts[i] = AddText("FoodName", card.transform, item.foodName, 22, Navy, TextAlignmentOptions.Left);
            SetCenter(refs.foodNameTexts[i].GetComponent<RectTransform>(), new Vector2(-55f, 18f), new Vector2(280f, 30f));
            refs.foodPriceTexts[i] = AddText("FoodPrice", card.transform, item.price + " slimes", 19, Green, TextAlignmentOptions.Left);
            SetCenter(refs.foodPriceTexts[i].GetComponent<RectTransform>(), new Vector2(-55f, -18f), new Vector2(280f, 26f));
            refs.foodBuyButtons[i] = CreateButton("Buy", card.transform, Green, new Vector2(140f, 60f), new Vector2(205f, 0f));
            AddText("Label", refs.foodBuyButtons[i].transform, "Comprar", 20, White, TextAlignmentOptions.Center);
        }
        refs.foodBackButton = CreateButton("FoodBack", parent, White, new Vector2(260f, 72f), new Vector2(0f, -570f));
        AddText("Label", refs.foodBackButton.transform, "← Voltar", 25, Navy, TextAlignmentOptions.Center);
    }

    private static void BuildFurniturePopup(RectTransform parent, HudRefs refs)
    {
        GameObject card = CreatePanel("PopupCard", parent, White, false);
        SetCenter(card.GetComponent<RectTransform>(), Vector2.zero, new Vector2(480f, 440f));
        refs.furnitureTitleText = AddText("FurnitureTitle", card.transform, "Cama", 34, Navy, TextAlignmentOptions.Center);
        refs.furnitureTitleText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 165f);
        refs.closeFurnitureButton = CreateButton("CloseButton", card.transform, White, new Vector2(64f, 64f), new Vector2(208f, 188f));
        AddText("Label", refs.closeFurnitureButton.transform, "×", 26, new Color(0.86f, 0.31f, 0.31f), TextAlignmentOptions.Center);
        GameObject actions = CreatePanel("PopupActionsRow", card.transform, new Color(0f, 0f, 0f, 0f), false);
        SetBottom(actions.GetComponent<RectTransform>(), 80f, 18f, 0f, 0f);
        refs.upgradeButton = CreateButton("UpgradeButton", actions.transform, Green, new Vector2(200f, 68f), new Vector2(-105f, 0f));
        AddText("Label", refs.upgradeButton.transform, "Upgrade", 21, White, TextAlignmentOptions.Center);
        refs.skinButton = CreateButton("SkinButton", actions.transform, LightPurple, new Vector2(200f, 68f), new Vector2(105f, 0f));
        AddText("Label", refs.skinButton.transform, "Trocar Skin", 20, Navy, TextAlignmentOptions.Center);
    }

    private static void BuildSkinPopup(RectTransform parent, HudRefs refs)
    {
        GameObject card = CreatePanel("PopupCard", parent, White, false);
        SetCenter(card.GetComponent<RectTransform>(), Vector2.zero, new Vector2(480f, 440f));
        refs.skinTitleText = AddText("SkinTitle", card.transform, "Cama", 34, Navy, TextAlignmentOptions.Center);
        refs.skinTitleText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 165f);
        refs.skinLeftButton = CreateButton("SkinLeft", card.transform, LightPurple, new Vector2(54f, 54f), new Vector2(-190f, 20f));
        AddText("Label", refs.skinLeftButton.transform, "◀", 23, Navy, TextAlignmentOptions.Center);
        refs.skinRightButton = CreateButton("SkinRight", card.transform, LightPurple, new Vector2(54f, 54f), new Vector2(190f, 20f));
        AddText("Label", refs.skinRightButton.transform, "▶", 23, Navy, TextAlignmentOptions.Center);
        refs.skinCards = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton("Skin" + i, card.transform, i == 1 ? White : LightPurple, new Vector2(92f, 130f), new Vector2((i - 1) * 100f, 20f));
            AddText("Label", button.transform, "Skin " + (i + 1), 16, Navy, TextAlignmentOptions.Center);
            refs.skinCards[i] = button;
        }
        refs.skinSelectionText = AddText("SkinSelection", card.transform, "Skin 2", 22, Green, TextAlignmentOptions.Center);
        refs.skinSelectionText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -105f);
        refs.skinBackButton = CreateButton("SkinBackButton", card.transform, LightPurple, new Vector2(320f, 64f), new Vector2(0f, -170f));
        AddText("Label", refs.skinBackButton.transform, "← Voltar", 23, Navy, TextAlignmentOptions.Center);
    }

    private static void BuildStickerPopup(RectTransform parent, HudRefs refs)
    {
        GameObject card = CreatePanel("PopupCard", parent, White, false);
        SetCenter(card.GetComponent<RectTransform>(), Vector2.zero, new Vector2(480f, 380f));
        refs.closeStickerButton = CreateButton("StickerCloseButton", card.transform, White, new Vector2(64f, 64f), new Vector2(208f, 158f));
        AddText("Label", refs.closeStickerButton.transform, "×", 26, new Color(0.86f, 0.31f, 0.31f), TextAlignmentOptions.Center);
        GameObject thumb = CreateImage("StickerThumb", card.transform, LightPurple, uiSprite, false, false);
        SetTopLeftSize(thumb.GetComponent<RectTransform>(), new Vector2(88f, 88f), new Vector2(28f, -105f));
        refs.stickerThumbText = AddText("StickerThumbLabel", thumb.transform, "★", 42, Navy, TextAlignmentOptions.Center);
        Stretch(refs.stickerThumbText.rectTransform);
        refs.stickerTitleText = AddText("StickerName", card.transform, "Estrela", 28, Navy, TextAlignmentOptions.Left);
        SetTopLeftSize(refs.stickerTitleText.GetComponent<RectTransform>(), new Vector2(260f, 40f), new Vector2(132f, -130f));
        refs.stickerDescriptionText = AddText("StickerDescription", card.transform, "Descrição do sticker...", 21, new Color(0.39f, 0.35f, 0.52f), TextAlignmentOptions.TopLeft);
        SetTopLeftSize(refs.stickerDescriptionText.GetComponent<RectTransform>(), new Vector2(410f, 120f), new Vector2(28f, -220f));
    }

    private static void AddSlimeCounter(RectTransform parent, string name, Vector2 position, HudRefs refs)
    {
        GameObject counter = CreatePanel(name, parent, White, false);
        SetTopCenter(counter.GetComponent<RectTransform>(), new Vector2(190f, 60f), position);
        GameObject dot = CreateImage("SlimeDot", counter.transform, Green, uiSprite, false, false);
        SetCenter(dot.GetComponent<RectTransform>(), new Vector2(-58f, 0f), new Vector2(42f, 42f));
        TextMeshProUGUI count = AddText("SlimeCountLabel", counter.transform, "x 0", 25, Navy, TextAlignmentOptions.Center);
        SetCenter(count.GetComponent<RectTransform>(), new Vector2(28f, 0f), new Vector2(95f, 45f));
        refs.slimeCountTexts.Add(count);
    }

    private static void AssignController(TesteDanielUGUIController hud, HudRefs refs, GameObject main, GameObject shop, GameObject detail, GameObject stickers, GameObject food, GameObject furniture, GameObject skin, GameObject sticker)
    {
        hud.mainScreen = main;
        hud.shopScreen = shop;
        hud.shopDetailScreen = detail;
        hud.stickersScreen = stickers;
        hud.foodScreen = food;
        hud.furniturePopup = furniture;
        hud.skinPopup = skin;
        hud.stickerPopup = sticker;
        hud.slimeButton = refs.slimeButton;
        hud.muteButton = refs.muteButton;
        hud.shopButton = refs.shopButton;
        hud.stickersButton = refs.stickersButton;
        hud.foodButton = refs.foodButton;
        hud.slimeImage = refs.slimeImage;
        hud.levelFill = refs.levelFill;
        hud.hungerFill = refs.hungerFill;
        hud.funFill = refs.funFill;
        hud.hygieneFill = refs.hygieneFill;
        hud.levelText = refs.levelText;
        hud.slimeCountTexts = refs.slimeCountTexts.ToArray();
        hud.hungerText = refs.hungerText;
        hud.funText = refs.funText;
        hud.hygieneText = refs.hygieneText;
        hud.moodText = refs.moodText;
        hud.muteText = refs.muteText;
        hud.furnitureButtons = refs.furnitureButtons;
        hud.roachButtons = refs.roachButtons;
        hud.critterLayer = refs.critterLayer;
        hud.shopOptionButtons = refs.shopOptionButtons;
        hud.stickerButtons = refs.stickerButtons;
        hud.foodBuyButtons = refs.foodBuyButtons;
        hud.foodNameTexts = refs.foodNameTexts;
        hud.foodPriceTexts = refs.foodPriceTexts;
        hud.skinCards = refs.skinCards;
        hud.shopBackButton = refs.shopBackButton;
        hud.detailBackButton = refs.detailBackButton;
        hud.stickersBackButton = refs.stickersBackButton;
        hud.foodBackButton = refs.foodBackButton;
        hud.closeFurnitureButton = refs.closeFurnitureButton;
        hud.closeStickerButton = refs.closeStickerButton;
        hud.skinButton = refs.skinButton;
        hud.skinBackButton = refs.skinBackButton;
        hud.skinLeftButton = refs.skinLeftButton;
        hud.skinRightButton = refs.skinRightButton;
        hud.upgradeButton = refs.upgradeButton;
        hud.furnitureTitleText = refs.furnitureTitleText;
        hud.skinTitleText = refs.skinTitleText;
        hud.skinSelectionText = refs.skinSelectionText;
        hud.detailTitleText = refs.detailTitleText;
        hud.stickerTitleText = refs.stickerTitleText;
        hud.stickerDescriptionText = refs.stickerDescriptionText;
        hud.stickerThumbText = refs.stickerThumbText;
        hud.foodItems = Resources.LoadAll<FoodItemData>("Food").OrderBy(f => f.order).ToArray();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        InputSystemUIInputModule module = go.GetComponent<InputSystemUIInputModule>();
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (actions != null)
            module.actionsAsset = actions;
    }

    private static void LoadAssets()
    {
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Background.png");
        slimeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/2D/Slime Icon.png");
        roachSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/2D/Barata.png");
        font = TMP_Settings.defaultFontAsset;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = uiSprite;
        image.color = color;
        image.raycastTarget = raycast;
        return go;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Sprite sprite, bool raycast, bool preserveAspect)
    {
        GameObject go = CreatePanel(name, parent, color, raycast);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : uiSprite;
        image.preserveAspect = preserveAspect;
        return go;
    }

    private static Button CreateButton(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        GameObject go = CreatePanel(name, parent, color, true);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        return button;
    }

    private static TextMeshProUGUI AddText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetTop(RectTransform rt, float height, float top, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -height - top);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void SetMiddle(RectTransform rt, float top, float bottom, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void SetBottom(RectTransform rt, float height, float margin, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(left, margin);
        rt.offsetMax = new Vector2(-right, margin + height);
    }

    private static void SetTopCenter(RectTransform rt, Vector2 size, Vector2 position)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void SetTopLeftSize(RectTransform rt, Vector2 size, Vector2 position)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void SetTopRightSize(RectTransform rt, Vector2 size, Vector2 position)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void SetCenter(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void SetBottomCenter(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
#endif
