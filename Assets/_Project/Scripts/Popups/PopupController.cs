
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central dos popups da MainScene (UGUI). Adaptado dos popups dummy do
/// colega para os dados reais do FurnitureManager:
/// - OpenFurniturePopup(FurnitureKind) abre com nivel/preco reais;
/// - OpenFurniturePopup(FurnitureData) continua compativel com o wiring
///   antigo da cena (o SO vira apenas origem de kind/sprites);
/// - no Start() ligamos todos os botoes de movel dos 3 comodos (Cama =
///   bed, Guarda-roupas = closet, Comoda = desk) ao popup;
/// - monta o ConfirmPopupUGUI de compra (GDD/TP3: toda compra confirma).
/// </summary>
public class PopupController : MonoBehaviour
{
    public static PopupController Instance { get; private set; }

    [Header("Popup Panels")]
    [SerializeField] private GameObject popupMovel;
    [SerializeField] private GameObject popupSkinMovel;
    [SerializeField] private GameObject popupSticker;

    [Header("Individual Controllers")]
    [SerializeField] private PopupMovelController movelController;
    [SerializeField] private PopupSkinMovelController skinMovelController;
    [SerializeField] private PopupStickerController stickerController;

    private FurnitureData currentFurniture;
    private FurnitureKind currentKind = FurnitureKind.Bed;

    /// <summary>Sprites dos moveis descobertos nos comodos (para o popup).</summary>
    private readonly Dictionary<FurnitureKind, Sprite> kindSprites = new();

    /// <summary>Imagens dos moveis por kind (todas as instancias nos comodos).</summary>
    private readonly Dictionary<FurnitureKind, List<Image>> kindImages = new();

    /// <summary>Sprite equipado por kind (preview do popup e comodo).</summary>
    private readonly Dictionary<FurnitureKind, Sprite> equippedSprites = new();

    /// <summary>Skins placeholder geradas em runtime ate a arte chegar.</summary>
    private readonly Dictionary<FurnitureKind, List<FurnitureSkinData>> placeholderSkins = new();

    /// <summary>FurnitureData por kind (Resources/Furniture, carregado uma vez).</summary>
    private Dictionary<FurnitureKind, FurnitureData> furnitureByKind;

    /// <summary>Popup aberto agora no modo movel.</summary>
    public FurnitureKind CurrentKind => currentKind;

    /// <summary>Sprite do movel no comodo: a skin equipada, ou o original.</summary>
    public Sprite SpriteFor(FurnitureKind kind)
        => equippedSprites.TryGetValue(kind, out Sprite sprite) ? sprite
        : kindSprites.TryGetValue(kind, out sprite) ? sprite
        : null;

    private void Awake()
    {
        Instance = this;
        CloseAllInternal(false);
        BuildConfirmPopup();
    }

    private void Start()
    {
        WireFurnitureButtons();
        FoodShopUGUIController.Install();
        SkinShopUGUIController.Install();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================
    // CONFIRMACAO DE COMPRA (GDD / TP3)
    // =========================================

    private void BuildConfirmPopup()
    {
        if (popupMovel == null)
            return;

        // Reaproveita a fonte, o fundo e os botoes da propria cena para o
        // popup ficar com a mesma identidade visual (montado em runtime,
        // sem exigir edicao da cena).
        TMP_Text sampleText = popupMovel.GetComponentInChildren<TMP_Text>(true);
        Transform bgTransform = popupMovel.transform.Find("PopUpBG");
        Transform upgradeTransform = popupMovel.transform.Find("UpgrateBtt");
        Transform skinTransform = popupMovel.transform.Find("SkinBtt");

        ConfirmPopupUGUI.EnsureBuilt(
            transform,
            sampleText != null ? sampleText.font : null,
            bgTransform != null ? bgTransform.GetComponent<Image>() : null,
            upgradeTransform != null ? upgradeTransform.GetComponent<Image>() : null,
            skinTransform != null ? skinTransform.GetComponent<Image>() : null);
    }

    // =========================================
    // BOTOES DE MOVEIS DOS COMODOS
    // =========================================

    /// <summary>
    /// Liga todos os botoes dos moveis posicionados nos comodos (3 por
    /// comodo) ao popup com o kind real, mapeando pelo nome do sprite.
    /// Assim nenhum botao precisa de wiring manual na cena.
    /// </summary>
    private void WireFurnitureButtons()
    {
        foreach (RoomController room in FindObjectsByType<RoomController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform bg = room.transform.Find("BG");
            if (bg == null)
                continue;

            for (int i = 0; i < bg.childCount; i++)
            {
                Transform child = bg.GetChild(i);
                Button button = child.GetComponent<Button>();
                Image image = child.GetComponent<Image>();
                if (button == null || image == null || image.sprite == null)
                    continue;

                if (!TryMapSpriteToKind(image.sprite.name, out FurnitureKind kind))
                {
                    Debug.LogWarning(
                        $"[PopupController] Sprite '{image.sprite.name}' do comodo " +
                        "nao mapeado para um movel.");
                    continue;
                }

                // Garante que o clique chega ao botao (imagem invisivel ao raycast
                // nao dispararia o popup).
                image.raycastTarget = true;

                if (!kindSprites.ContainsKey(kind))
                    kindSprites.Add(kind, image.sprite);

                if (!kindImages.TryGetValue(kind, out List<Image> images))
                {
                    images = new List<Image>();
                    kindImages[kind] = images;
                }
                if (!images.Contains(image))
                    images.Add(image);

                FurnitureKind captured = kind;
                button.onClick.AddListener(() => OpenFurniturePopup(captured));
            }
        }

        // Skin gravada no save aplicada aos moveis dos comodos (0 = o
        // sprite original em saves antigos).
        ApplySavedSkins();
    }

    /// <summary>bed/cama, closet/wardrobe/guarda, desk/dresser/comoda.</summary>
    private static bool TryMapSpriteToKind(string spriteName, out FurnitureKind kind)
    {
        kind = FurnitureKind.Bed;
        string n = spriteName == null ? string.Empty : spriteName.ToLowerInvariant();

        if (n.Contains("bed") || n.Contains("cama"))
        {
            kind = FurnitureKind.Bed;
            return true;
        }
        if (n.Contains("closet") || n.Contains("wardrobe") || n.Contains("guarda"))
        {
            kind = FurnitureKind.Wardrobe;
            return true;
        }
        if (n.Contains("desk") || n.Contains("dresser") || n.Contains("comoda"))
        {
            kind = FurnitureKind.Dresser;
            return true;
        }
        return false;
    }

    // =========================================
    // OPEN FURNITURE POPUP
    // =========================================

    /// <summary>Compatibilidade com o wiring antigo (argumento FurnitureData).</summary>
    public void OpenFurniturePopup(FurnitureData furnitureData)
    {
        if (furnitureData == null)
            return;

        currentFurniture = furnitureData;
        OpenFurniturePopup(furnitureData.kind);
    }

    /// <summary>Abre o popup de movel com os dados reais do FurnitureManager.</summary>
    public void OpenFurniturePopup(FurnitureKind kind)
    {
        if (popupMovel == null)
            return;

        // Clique duplo (wiring persistente da cena + listener em runtime):
        // nao reabre nem toca o som de novo, so atualiza os dados.
        if (popupMovel.activeSelf && currentKind == kind &&
            (popupSkinMovel == null || !popupSkinMovel.activeSelf))
        {
            movelController?.Refresh();
            return;
        }

        currentKind = kind;
        if (currentFurniture != null && currentFurniture.kind != kind)
            currentFurniture = null;

        CloseAllInternal(false);

        if (movelController != null)
            movelController.SetFurniture(kind);

        popupMovel.SetActive(true);
        PlayOpen();
    }

    // =========================================
    // OPEN SKIN POPUP
    // =========================================

    /// <summary>Compatibilidade com o wiring antigo (argumento FurnitureData).</summary>
    public void OpenSkinPopup(FurnitureData furnitureData)
    {
        currentFurniture = furnitureData;
        OpenSkinPopup();
    }

    /// <summary>Abre o carrossel de skins do ultimo movel selecionado.</summary>
    public void OpenSkinPopup()
    {
        if (popupSkinMovel == null || popupSkinMovel.activeSelf)
            return;

        CloseAllInternal(false);

        if (currentFurniture == null || currentFurniture.kind != currentKind)
            currentFurniture = FurnitureFor(currentKind);

        if (skinMovelController != null)
        {
            List<FurnitureSkinData> skins = ResolveSkins(currentKind);
            skinMovelController.SetSkins(
                currentKind, skins, GetSkinIndex(currentKind, skins.Count));
        }

        popupSkinMovel.SetActive(true);
        PlayOpen();
    }

    // =========================================
    // BACK TO FURNITURE POPUP
    // =========================================

    public void BackToFurniturePopup()
    {
        OpenFurniturePopup(currentKind);
    }

    // =========================================
    // SKINS DOS MOVEIS
    // =========================================

    /// <summary>
    /// Lista de skins do movel: os assets em Resources/Furniture quando
    /// existirem; senao placeholders em runtime (padrao + variantes
    /// tingidas) para o fluxo funcionar antes da arte chegar.
    /// </summary>
    public List<FurnitureSkinData> ResolveSkins(FurnitureKind kind)
    {
        FurnitureData furniture = FurnitureFor(kind);
        if (furniture != null &&
            furniture.skins != null && furniture.skins.Count > 0)
            return furniture.skins;

        return PlaceholdersFor(kind);
    }

    private FurnitureData FurnitureFor(FurnitureKind kind)
    {
        if (furnitureByKind == null)
        {
            furnitureByKind = new Dictionary<FurnitureKind, FurnitureData>();
            foreach (FurnitureData furniture in
                Resources.LoadAll<FurnitureData>("Furniture"))
            {
                if (!furnitureByKind.ContainsKey(furniture.kind))
                    furnitureByKind.Add(furniture.kind, furniture);
            }
        }

        furnitureByKind.TryGetValue(kind, out FurnitureData found);
        return found;
    }

    /// <summary>
    /// Placeholders: padrao + duas variantes tingidas do sprite atual do
    /// movel. Somem sozinhas quando o asset ganhar skins reais.
    /// </summary>
    private List<FurnitureSkinData> PlaceholdersFor(FurnitureKind kind)
    {
        if (placeholderSkins.TryGetValue(kind,
            out List<FurnitureSkinData> cached))
            return cached;

        kindSprites.TryGetValue(kind, out Sprite original);

        var skins = new List<FurnitureSkinData>
        {
            MakeSkin("Padrão", original)
        };

        if (original != null)
        {
            skins.Add(MakeSkin("Verde",
                TintSprite(original, new Color(0.30f, 0.95f, 0.35f), 0.65f)));
            skins.Add(MakeSkin("Azul",
                TintSprite(original, new Color(0.35f, 0.55f, 1f), 0.65f)));
        }

        placeholderSkins[kind] = skins;
        return skins;
    }

    private static FurnitureSkinData MakeSkin(string skinName, Sprite sprite)
    {
        var skin = ScriptableObject.CreateInstance<FurnitureSkinData>();
        skin.skinName = skinName;
        skin.skinImage = sprite;
        return skin;
    }

    /// <summary>Copia o sprite e mistura as cores na cor dada (placeholder).</summary>
    private static Sprite TintSprite(Sprite source, Color tint, float amount)
    {
        Texture2D sourceTexture = source.texture;
        int width = sourceTexture.width;
        int height = sourceTexture.height;

        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(sourceTexture, temporary);
        RenderTexture.active = temporary;

        var readable = new Texture2D(
            width, height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        Color[] pixels = readable.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            pixels[i] = new Color(
                Mathf.Lerp(pixel.r, tint.r, amount),
                Mathf.Lerp(pixel.g, tint.g, amount),
                Mathf.Lerp(pixel.b, tint.b, amount),
                pixel.a);
        }
        readable.SetPixels(pixels);
        readable.Apply();

        return Sprite.Create(
            readable,
            new Rect(0, 0, width, height),
            source.pivot,
            source.pixelsPerUnit);
    }

    /// <summary>Index equipado no save, limitado a lista atual (0 = padrao).</summary>
    private int GetSkinIndex(FurnitureKind kind, int skinCount)
    {
        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        if (save == null || save.furnitureSkinIndex == null ||
            (int)kind >= save.furnitureSkinIndex.Length)
            return 0;

        int index = save.furnitureSkinIndex[(int)kind];
        return index >= 0 && index < skinCount ? index : 0;
    }

    // =========================================
    // COMPRA / POSSE DAS SKINS (GDD: toda compra confirma)
    // =========================================

    /// <summary>
    /// Skin comprada? Padrao (preco0 ou sem id) e sempre livre; as demais
    /// precisam do id gravado em SaveData.ownedSkins.
    /// </summary>
    public static bool IsSkinOwned(FurnitureSkinData skin)
    {
        if (skin == null)
            return false;
        if (skin.skinPrice <= 0 || string.IsNullOrEmpty(skin.skinId))
            return true;

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        return save != null && save.ownedSkins != null &&
               save.ownedSkins.Contains(skin.skinId);
    }

    /// <summary>
    /// Compra a skin com gosma (SlimeManager.SpendGoo valida o saldo e
    /// grava). Retorna false se ja for dono ou nao tiver gosma.
    /// </summary>
    public static bool BuySkin(FurnitureSkinData skin)
    {
        if (skin == null || skin.skinPrice <= 0 ||
            string.IsNullOrEmpty(skin.skinId) || IsSkinOwned(skin))
            return false;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null || !slime.SpendGoo(skin.skinPrice))
            return false;

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        if (save == null)
            return false;

        if (save.ownedSkins == null)
            save.ownedSkins = new List<string>();
        save.ownedSkins.Add(skin.skinId);
        manager.RequestSave();
        return true;
    }

    /// <summary>Equipa a skin escolhida no carrossel (grava e aplica).</summary>
    public void EquipSkin(FurnitureKind kind, int index)
    {
        List<FurnitureSkinData> skins = ResolveSkins(kind);
        if (skins == null || index < 0 || index >= skins.Count)
            return;

        // Trava: so equipa quem comprou (feedback de bloqueio no popup e
        // na loja — o click em skin comprada cai aqui).
        if (!IsSkinOwned(skins[index]))
        {
            MainHUDUGUIController.Instance?.PlayClose();
            return;
        }

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        if (save != null)
        {
            if (save.furnitureSkinIndex == null)
                save.furnitureSkinIndex = new int[0];

            int slot = (int)kind;
            if (save.furnitureSkinIndex.Length <= slot)
            {
                var grown = new int[slot + 1];
                for (int i = 0; i < save.furnitureSkinIndex.Length; i++)
                    grown[i] = save.furnitureSkinIndex[i];
                save.furnitureSkinIndex = grown;
            }

            save.furnitureSkinIndex[slot] = index;
            manager.RequestSave();
        }

        ApplySkin(kind);
        MainHUDUGUIController.Instance?.PlayBuy();
    }

    private void ApplySavedSkins()
    {
        foreach (FurnitureKind kind in kindImages.Keys)
            ApplySkin(kind);
    }

    private void ApplySkin(FurnitureKind kind)
    {
        if (!kindImages.TryGetValue(kind, out List<Image> images) ||
            images.Count == 0)
            return;

        List<FurnitureSkinData> skins = ResolveSkins(kind);
        int index = GetSkinIndex(kind, skins.Count);

        // Save antigo pode apontar para skin que ainda nao foi comprada
        // (teste anterior): o comodo volta para o padrao ate comprar.
        if (index > 0 && !IsSkinOwned(skins[index]))
            index = 0;

        Sprite sprite = null;
        if (index >= 0 && index < skins.Count && skins[index] != null)
            sprite = skins[index].skinImage;
        if (sprite == null &&
            kindSprites.TryGetValue(kind, out Sprite original))
            sprite = original;

        if (sprite == null)
            return;

        equippedSprites[kind] = sprite;
        foreach (Image image in images)
        {
            if (image != null)
                image.sprite = sprite;
        }
    }

    // =========================================
    // OPEN STICKER POPUP
    // =========================================

    public void OpenStickerPopup(StickerData stickerData)
    {
        if (stickerData == null)
            return;

        CloseAllInternal(false);

        if (stickerController != null)
            stickerController.SetStickerData(stickerData);

        if (popupSticker != null)
            popupSticker.SetActive(true);

        PlayOpen();
    }

    // =========================================
    // CLOSE ALL POPUPS
    // =========================================

    public void CloseAllPopups()
    {
        CloseAllInternal(true);
    }

    private void CloseAllInternal(bool playSound)
    {
        bool anyOpen =
            (popupMovel != null && popupMovel.activeSelf) ||
            (popupSkinMovel != null && popupSkinMovel.activeSelf) ||
            (popupSticker != null && popupSticker.activeSelf);

        // Seguranca: nunca deixa o popup de confirmacao aberto "solto" por
        // cima do jogo apos fechar os demais popups.
        ConfirmPopupUGUI.Instance?.Hide();

        if (popupMovel != null)
            popupMovel.SetActive(false);

        if (popupSkinMovel != null)
            popupSkinMovel.SetActive(false);

        if (popupSticker != null)
            popupSticker.SetActive(false);

        if (playSound && anyOpen)
            PlayClose();
    }

    private static void PlayOpen()
    {
        MainHUDUGUIController.Instance?.PlayOpen();
    }

    private static void PlayClose()
    {
        MainHUDUGUIController.Instance?.PlayClose();
    }
}
