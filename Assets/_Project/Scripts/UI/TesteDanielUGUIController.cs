using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador da versão uGUI da cena TesteDaniel.
/// Todos os elementos visuais são GameObjects serializados na Scene;
/// este componente apenas conecta esses objetos ao SlimeManager e ao SaveManager.
/// </summary>
public class TesteDanielUGUIController : MonoBehaviour
{
    [Header("Sistemas")]
    public SlimeManager slimeManager;
    public AudioSource audioSource;
    public AudioClip slimeClip;
    [Range(0f, 1f)] public float audioVolume = 0.5f;

    [Header("Telas")]
    public GameObject mainScreen;
    public GameObject shopScreen;
    public GameObject shopDetailScreen;
    public GameObject stickersScreen;
    public GameObject foodScreen;
    public GameObject furniturePopup;
    public GameObject skinPopup;
    public GameObject stickerPopup;

    [Header("HUD principal")]
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
    public TextMeshProUGUI[] slimeCountTexts;
    public TextMeshProUGUI hungerText;
    public TextMeshProUGUI funText;
    public TextMeshProUGUI hygieneText;
    public TextMeshProUGUI moodText;
    public TextMeshProUGUI muteText;
    public int level = 1;
    [Range(0f, 1f)] public float levelProgress = 0.4f;

    [Header("Móveis e skins")]
    public Button[] furnitureButtons;
    public Button closeFurnitureButton;
    public Button upgradeButton;
    public Button skinButton;
    public Button skinBackButton;
    public Button skinLeftButton;
    public Button skinRightButton;
    public Button[] skinCards;
    public TextMeshProUGUI furnitureTitleText;
    public TextMeshProUGUI skinTitleText;
    public TextMeshProUGUI skinSelectionText;

    [Header("Loja")]
    public Button[] shopOptionButtons;
    public Button shopBackButton;
    public Button detailBackButton;
    public TextMeshProUGUI detailTitleText;

    [Header("Stickers")]
    public Button[] stickerButtons;
    public Button stickersBackButton;
    public Button closeStickerButton;
    public TextMeshProUGUI stickerTitleText;
    public TextMeshProUGUI stickerDescriptionText;
    public TextMeshProUGUI stickerThumbText;

    [Header("Comida")]
    public FoodItemData[] foodItems;
    public Button[] foodBuyButtons;
    public TextMeshProUGUI[] foodNameTexts;
    public TextMeshProUGUI[] foodPriceTexts;
    public Button foodBackButton;

    [Header("Baratas / higiene")]
    public RectTransform critterLayer;
    public Button[] roachButtons;
    public int roachHitsRequired = 5;
    public float hygienePerKill = 0.25f;
    public float roachMoveSpeedMin = 45f;
    public float roachMoveSpeedMax = 95f;

    private readonly string[] furnitureNames = { "Cama", "Sofá", "Planta" };
    private readonly string[] skinNames = { "Skin 1", "Skin 2", "Skin 3", "Skin 4", "Skin 5" };
    private readonly string[] stickerNames =
    {
        "Estrela", "Arco-íris", "Foguinho", "Trevo", "Balão", "Musiquinha",
        "Patinha", "Diamante", "Rosquinha", "Foguete", "Estrelinha", "Redemoinho"
    };
    private readonly string[] stickerDescriptions =
    {
        "Brilha no escuro do quarto do slime.",
        "Traz cor para os dias nublados.",
        "Aquece o slime no inverno.",
        "Dizem que dá sorte nas colheitas.",
        "Flutua pela casa sem parar.",
        "Toca uma melodia suave.",
        "Marca de um amigo peludo.",
        "Raro e muito brilhante.",
        "Quase dá pra comer de tão fofa.",
        "Pronto para decolar.",
        "Pequena mas poderosa.",
        "Gira sem parar de alegria."
    };

    private int[] roachHits = Array.Empty<int>();
    private Vector2[] roachDirections = Array.Empty<Vector2>();
    private float[] roachSpeeds = Array.Empty<float>();
    private int currentFurniture;
    private int currentSticker;
    private int skinIndex = 1;
    private bool muted;
    private bool listenersBound;
    private float nextRefresh;

    private void Awake()
    {
        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        int count = roachButtons?.Length ?? 0;
        roachHits = new int[count];
        roachDirections = new Vector2[count];
        roachSpeeds = new float[count];
        for (int i = 0; i < count; i++)
        {
            roachDirections[i] = RandomDirection();
            roachSpeeds[i] = UnityEngine.Random.Range(roachMoveSpeedMin, roachMoveSpeedMax);
        }
    }

    private void OnEnable()
    {
        BindListeners();
        ShowMain();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindListeners();
    }

    private void Update()
    {
        UpdateRoaches();
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.25f;
        Refresh();
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        AddListener(slimeButton, OnSlimeClicked);
        AddListener(muteButton, ToggleMute);
        AddListener(shopButton, () => ShowScreen(shopScreen));
        AddListener(stickersButton, () => ShowScreen(stickersScreen));
        AddListener(foodButton, () => ShowScreen(foodScreen));
        AddListener(shopBackButton, ShowMain);
        AddListener(detailBackButton, () => ShowScreen(shopScreen));
        AddListener(stickersBackButton, ShowMain);
        AddListener(foodBackButton, ShowMain);
        AddListener(closeFurnitureButton, ClosePopups);
        AddListener(closeStickerButton, ClosePopups);
        AddListener(skinButton, OpenSkinPopup);
        AddListener(skinBackButton, ClosePopups);
        AddListener(upgradeButton, OnUpgrade);
        AddListener(skinLeftButton, () => MoveSkin(-1));
        AddListener(skinRightButton, () => MoveSkin(1));

        for (int i = 0; i < furnitureButtons?.Length; i++)
        {
            int index = i;
            AddListener(furnitureButtons[index], () => OpenFurniture(index));
        }
        for (int i = 0; i < shopOptionButtons?.Length; i++)
        {
            int index = i;
            AddListener(shopOptionButtons[index], () => OpenShopDetail(index));
        }
        for (int i = 0; i < stickerButtons?.Length; i++)
        {
            int index = i;
            AddListener(stickerButtons[index], () => OpenSticker(index));
        }
        for (int i = 0; i < skinCards?.Length; i++)
        {
            int index = i;
            AddListener(skinCards[index], () => SelectSkin(index));
        }
        for (int i = 0; i < foodBuyButtons?.Length; i++)
        {
            int index = i;
            AddListener(foodBuyButtons[index], () => BuyFood(index));
        }
        for (int i = 0; i < roachButtons?.Length; i++)
        {
            int index = i;
            AddListener(roachButtons[index], () => HitRoach(index));
        }

        listenersBound = true;
        RefreshSkins();
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
            return;

        ClearButtonListeners(slimeButton);
        ClearButtonListeners(muteButton);
        ClearButtonListeners(shopButton);
        ClearButtonListeners(stickersButton);
        ClearButtonListeners(foodButton);
        ClearButtonListeners(shopBackButton);
        ClearButtonListeners(detailBackButton);
        ClearButtonListeners(stickersBackButton);
        ClearButtonListeners(foodBackButton);
        ClearButtonListeners(closeFurnitureButton);
        ClearButtonListeners(closeStickerButton);
        ClearButtonListeners(skinButton);
        ClearButtonListeners(skinBackButton);
        ClearButtonListeners(upgradeButton);
        ClearButtonListeners(skinLeftButton);
        ClearButtonListeners(skinRightButton);
        for (int i = 0; i < furnitureButtons?.Length; i++) ClearButtonListeners(furnitureButtons[i]);
        for (int i = 0; i < shopOptionButtons?.Length; i++) ClearButtonListeners(shopOptionButtons[i]);
        for (int i = 0; i < stickerButtons?.Length; i++) ClearButtonListeners(stickerButtons[i]);
        for (int i = 0; i < skinCards?.Length; i++) ClearButtonListeners(skinCards[i]);
        for (int i = 0; i < foodBuyButtons?.Length; i++) ClearButtonListeners(foodBuyButtons[i]);
        for (int i = 0; i < roachButtons?.Length; i++) ClearButtonListeners(roachButtons[i]);
        listenersBound = false;
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        button?.onClick.AddListener(action);
    }

    private static void ClearButtonListeners(Button button)
    {
        if (button != null)
            button.onClick.RemoveAllListeners();
    }

    private void ShowMain()
    {
        ShowScreen(mainScreen);
    }

    private void ShowScreen(GameObject screen)
    {
        SetVisible(mainScreen, screen == mainScreen);
        SetVisible(shopScreen, screen == shopScreen);
        SetVisible(shopDetailScreen, screen == shopDetailScreen);
        SetVisible(stickersScreen, screen == stickersScreen);
        SetVisible(foodScreen, screen == foodScreen);
        ClosePopups();
    }

    private static void SetVisible(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
    }

    private void ClosePopups()
    {
        SetVisible(furniturePopup, false);
        SetVisible(skinPopup, false);
        SetVisible(stickerPopup, false);
    }

    private void OpenFurniture(int index)
    {
        currentFurniture = Mathf.Clamp(index, 0, furnitureNames.Length - 1);
        if (furnitureTitleText != null)
            furnitureTitleText.text = furnitureNames[currentFurniture];
        SetVisible(furniturePopup, true);
    }

    private void OpenSkinPopup()
    {
        if (skinTitleText != null)
            skinTitleText.text = furnitureNames[currentFurniture];
        SetVisible(furniturePopup, false);
        SetVisible(skinPopup, true);
        RefreshSkins();
    }

    private void OpenShopDetail(int index)
    {
        string[] names = { "Pacote de Stickers", "Skin dos Slimes", "Skin dos Móveis" };
        if (detailTitleText != null)
            detailTitleText.text = names[Mathf.Clamp(index, 0, names.Length - 1)];
        ShowScreen(shopDetailScreen);
    }

    private void OpenSticker(int index)
    {
        currentSticker = Mathf.Clamp(index, 0, stickerNames.Length - 1);
        if (stickerTitleText != null)
            stickerTitleText.text = stickerNames[currentSticker];
        if (stickerDescriptionText != null)
            stickerDescriptionText.text = stickerDescriptions[currentSticker];
        if (stickerThumbText != null)
            stickerThumbText.text = "★";
        SetVisible(stickerPopup, true);
    }

    private void OnUpgrade()
    {
        Debug.Log($"[TesteDanielUGUI] Upgrade de {furnitureNames[currentFurniture]} ainda em modo demo.");
    }

    private void OnSlimeClicked()
    {
        slimeManager?.RegisterClick();
        Play(slimeClip);
        Refresh();
    }

    private void BuyFood(int index)
    {
        if (slimeManager == null || index < 0 || index >= foodItems?.Length)
            return;
        FoodItemData item = foodItems[index];
        if (item == null || !slimeManager.SpendGoo(item.price))
            return;
        slimeManager.AddHunger(item.hungerRestore);
        Play(slimeClip);
        Refresh();
    }

    private void HitRoach(int index)
    {
        if (slimeManager == null || index < 0 || index >= roachHits.Length)
            return;
        roachHits[index]++;
        if (roachHits[index] < Mathf.Max(1, roachHitsRequired))
        {
            Play(slimeClip);
            return;
        }
        roachHits[index] = 0;
        slimeManager.AddHygiene(hygienePerKill);
        Play(slimeClip);
        Refresh();
    }

    private void ToggleMute()
    {
        muted = !muted;
        AudioListener.pause = muted;
        if (muteText != null)
            muteText.text = muted ? "OFF" : "ON";
    }

    private void MoveSkin(int direction)
    {
        skinIndex = (skinIndex + direction + skinNames.Length) % skinNames.Length;
        RefreshSkins();
    }

    private void SelectSkin(int visibleSlot)
    {
        MoveSkin(visibleSlot - 1);
    }

    private void RefreshSkins()
    {
        if (skinSelectionText != null)
            skinSelectionText.text = skinNames[skinIndex];
        for (int slot = 0; slot < (skinCards?.Length ?? 0); slot++)
        {
            if (skinCards[slot] == null)
                continue;
            int index = (skinIndex + slot - 1 + skinNames.Length) % skinNames.Length;
            TextMeshProUGUI label = skinCards[slot].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = skinNames[index];
        }
    }

    private void UpdateRoaches()
    {
        if (critterLayer == null || roachButtons == null || roachButtons.Length == 0)
            return;

        Vector2 area = critterLayer.rect.size;
        if (area.x <= 1f || area.y <= 1f)
            area = new Vector2(720f, 1280f);

        for (int i = 0; i < roachButtons.Length; i++)
        {
            Button button = roachButtons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;
            RectTransform rt = button.GetComponent<RectTransform>();
            if (rt == null)
                continue;

            Vector2 half = rt.rect.size * 0.5f;
            Vector2 position = rt.anchoredPosition + roachDirections[i] * roachSpeeds[i] * Time.deltaTime;
            float minX = -area.x * 0.5f + half.x;
            float maxX = area.x * 0.5f - half.x;
            float minY = -area.y * 0.5f + half.y;
            float maxY = area.y * 0.5f - half.y;
            bool bounce = false;
            if (position.x < minX || position.x > maxX)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
                roachDirections[i].x = -roachDirections[i].x;
                bounce = true;
            }
            if (position.y < minY || position.y > maxY)
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
                roachDirections[i].y = -roachDirections[i].y;
                bounce = true;
            }
            if (bounce)
                roachDirections[i] = RandomDirection();
            rt.anchoredPosition = position;
        }
    }

    private void Refresh()
    {
        if (slimeManager == null)
            return;

        float goo = slimeManager.Goo;
        float hunger = slimeManager.Hunger;
        float fun = slimeManager.Fun;
        float hygiene = slimeManager.Hygiene;
        int count = Mathf.FloorToInt(goo);

        if (slimeCountTexts != null)
            foreach (TextMeshProUGUI text in slimeCountTexts)
                if (text != null) text.text = $"x {count}";
        if (levelText != null)
            levelText.text = $"Nv {level} • {Mathf.RoundToInt(levelProgress * 100f)}%";
        if (hungerText != null) hungerText.text = $"{Mathf.RoundToInt(hunger * 100f)}%";
        if (funText != null) funText.text = $"{Mathf.RoundToInt(fun * 100f)}%";
        if (hygieneText != null) hygieneText.text = $"{Mathf.RoundToInt(hygiene * 100f)}%";
        SetFill(levelFill, levelProgress);
        SetFill(hungerFill, hunger);
        SetFill(funFill, fun);
        SetFill(hygieneFill, hygiene);

        int mood = slimeManager.GetEuphoriaMoodTier();
        if (moodText != null) moodText.text = SlimeManager.MoodTexts[mood];
        if (slimeButton != null && slimeButton.image != null)
            slimeButton.image.color = SlimeManager.MoodColors[mood];

        int desired = hygiene >= 0.999f
            ? 0
            : Mathf.Clamp(Mathf.CeilToInt((1f - hygiene) * (roachButtons?.Length ?? 0)), 1, roachButtons?.Length ?? 0);
        for (int i = 0; i < (roachButtons?.Length ?? 0); i++)
            if (roachButtons[i] != null)
                SetVisible(roachButtons[i].gameObject, i < desired);
    }

    private static void SetFill(Image image, float value)
    {
        if (image != null)
            image.fillAmount = Mathf.Clamp01(value);
    }

    private static Vector2 RandomDirection()
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        return direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource == null || clip == null || muted)
            return;
        audioSource.volume = audioVolume;
        audioSource.PlayOneShot(clip);
    }
}
