
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

    /// <summary>Popup aberto agora no modo movel.</summary>
    public FurnitureKind CurrentKind => currentKind;

    /// <summary>Sprite do movel no comodo, quando descoberto.</summary>
    public Sprite SpriteFor(FurnitureKind kind)
        => kindSprites.TryGetValue(kind, out Sprite sprite) ? sprite : null;

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

                FurnitureKind captured = kind;
                button.onClick.AddListener(() => OpenFurniturePopup(captured));
            }
        }
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

        if (skinMovelController != null)
            skinMovelController.SetFurnitureData(currentFurniture);

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
