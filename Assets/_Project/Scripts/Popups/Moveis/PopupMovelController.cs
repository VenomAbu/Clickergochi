
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup de movel da MainScene (UGUI). Adaptado do dummy do colega para os
/// dados reais do FurnitureManager: mostra nivel/maximo, efeito atual e
/// proximo nivel, custo em gosma e estados bloqueado/maximo/sem gosma.
/// O upgrade pede confirmacao (ConfirmPopupUGUI) antes de comprar.
/// </summary>
public class PopupMovelController : MonoBehaviour
{
    [Header("Information")]
    [SerializeField] private TMP_Text nameTMP;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text levelTMP;
    [SerializeField] private TMP_Text descriptionTMP;
    [SerializeField] private TMP_Text priceTMP;

    [Header("Popup Controller")]
    [SerializeField] private PopupController popupController;

    private FurnitureData currentFurniture;
    private FurnitureKind currentKind = FurnitureKind.Bed;

    private Button upgradeButton;
    private Color priceDefaultColor;
    private bool priceColorCached;

    /// <summary>Movel que este popup esta exibindo.</summary>
    public FurnitureKind CurrentKind => currentKind;

    private void Awake()
    {
        if (priceTMP != null)
        {
            upgradeButton = priceTMP.GetComponentInParent<Button>();
            priceDefaultColor = priceTMP.color;
            priceColorCached = true;
        }
    }

    // =========================================
    // DATA BINDING
    // =========================================

    /// <summary>Vincula um movel real (dados vem do FurnitureManager).</summary>
    public void SetFurniture(FurnitureKind kind)
    {
        currentKind = kind;
        currentFurniture = null;
        Refresh();
    }

    /// <summary>Compatibilidade com o caminho antigo baseado em FurnitureData.</summary>
    public void SetFurnitureData(FurnitureData furnitureData)
    {
        currentFurniture = furnitureData;
        if (furnitureData != null)
            currentKind = furnitureData.kind;
        Refresh();
    }

    /// <summary>Atualiza nome, sprite, nivel, efeito e preco com dados reais.</summary>
    public void Refresh()
    {
        FurnitureManager furniture = FurnitureManager.Instance;
        if (nameTMP == null || furniture == null)
            return;

        int level = furniture.FurnitureLevel(currentKind);
        int max = furniture.FurnitureMaxLevel(currentKind);
        int unlock = furniture.UnlockLevel(currentKind);
        SlimeManager slime = SlimeManager.Instance;

        nameTMP.text = FurnitureShopUI.FurnitureName(currentKind);
        RefreshImage();

        // Bloqueado: comodo ainda nao liberou o movel.
        if (max <= 0)
        {
            // Nivel atual mesmo bloqueado (0). Evita "—" que a fonte Matcha
            // nao possui (glifo faltante vira lixo visual).
            levelTMP.text = $"lvl. {level}";
            descriptionTMP.text = $"Desbloqueia no Cômodo Lv.{unlock}.";
            if (priceTMP != null)
                priceTMP.text = string.Empty;
            SetUpgradeInteractable(false);
            return;
        }

        // So o nivel atual, no formato original do layout ("lvl. N").
        levelTMP.text = $"lvl. {level}";
        descriptionTMP.text = FurnitureShopUI.BuildEffectText(currentKind, level);

        float cost = furniture.FurnitureUpgradeCost(currentKind);

        // Nivel maximo no comodo atual (evolua o comodo para subir mais).
        if (cost < 0f)
        {
            descriptionTMP.text += "\nEvolua o Cômodo para subir mais de nível.";
            if (priceTMP != null)
                priceTMP.text = "Máximo";
            SetUpgradeInteractable(false);
            return;
        }

        if (priceTMP != null)
        {
            priceTMP.text = FurnitureShopUI.FormatGoo(cost);
            if (priceColorCached)
                priceTMP.color = priceDefaultColor;
        }

        bool affordable = slime == null || slime.Goo >= cost;
        SetUpgradeInteractable(affordable);
    }

    private void RefreshImage()
    {
        if (itemImage == null)
            return;

        Sprite sprite = popupController != null
            ? popupController.SpriteFor(currentKind)
            : null;

        if (sprite == null && currentFurniture != null)
            sprite = currentFurniture.itemImage;

        if (sprite == null)
        {
            itemImage.enabled = false;
            return;
        }

        itemImage.sprite = sprite;
        itemImage.enabled = true;
    }

    private void SetUpgradeInteractable(bool interactable)
    {
        if (upgradeButton == null && priceTMP != null)
            upgradeButton = priceTMP.GetComponentInParent<Button>();

        if (upgradeButton != null)
            upgradeButton.interactable = interactable;
    }

    // =========================================
    // BUTTONS
    // =========================================

    public void ClosePopup()
    {
        if (popupController != null)
            popupController.CloseAllPopups();
    }

    public void OpenSkinPopup()
    {
        if (popupController == null)
            return;

        popupController.OpenSkinPopup();
    }

    /// <summary>Pede confirmacao e entao executa a compra do proximo nivel.</summary>
    public void UpgradeButton()
    {
        FurnitureManager furniture = FurnitureManager.Instance;
        if (furniture == null)
            return;

        float cost = furniture.FurnitureUpgradeCost(currentKind);
        if (cost < 0f)
        {
            Refresh();
            return;
        }

        string name = FurnitureShopUI.FurnitureName(currentKind);
        int next = furniture.FurnitureLevel(currentKind) + 1;
        string message =
            $"Passar para o Nv {next} por {FurnitureShopUI.FormatGoo(cost)} gosma?";

        ConfirmPopupUGUI confirm = ConfirmPopupUGUI.Instance;
        if (confirm != null)
            confirm.Show($"Evoluir {name}?", message, "Evoluir", ExecuteUpgrade);
        else
            ExecuteUpgrade();
    }

    private void ExecuteUpgrade()
    {
        if (FurnitureManager.Instance.TryUpgradeFurniture(currentKind))
        {
            MainHUDUGUIController.Instance?.PlayBuy();
            Refresh();
        }
        else if (priceTMP != null)
        {
            // Saldo mudou entre a confirmacao e a compra.
            priceTMP.text = "Sem gosma!";
            if (priceColorCached)
                priceTMP.color = new Color(0.85f, 0.2f, 0.15f);
            SetUpgradeInteractable(false);
        }
    }
}
