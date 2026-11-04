
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private int upgradeClickCount;

    public void SetFurnitureData(FurnitureData furnitureData)
    {
        currentFurniture = furnitureData;

        if (currentFurniture == null)
            return;

        nameTMP.text = currentFurniture.itemName;
        itemImage.sprite = currentFurniture.itemImage;
        itemImage.enabled = currentFurniture.itemImage != null;

        levelTMP.text = currentFurniture.level.ToString();
        descriptionTMP.text = currentFurniture.description;
        priceTMP.text = currentFurniture.price.ToString();
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
        if (popupController == null || currentFurniture == null)
            return;

        popupController.OpenSkinPopup(currentFurniture);
    }

    public void UpgradeButton()
    {
        upgradeClickCount++;

        Debug.Log(
            $"Melhoria pressionada {upgradeClickCount} vezes. " +
            $"Móvel: {currentFurniture?.itemName}"
        );
    }
}
