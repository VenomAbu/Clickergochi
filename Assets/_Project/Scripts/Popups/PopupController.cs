
using UnityEngine;

public class PopupController : MonoBehaviour
{
    [Header("Popup Panels")]
    [SerializeField] private GameObject popupMovel;
    [SerializeField] private GameObject popupSkinMovel;
    [SerializeField] private GameObject popupSticker;

    [Header("Individual Controllers")]
    [SerializeField] private PopupMovelController movelController;
    [SerializeField] private PopupSkinMovelController skinMovelController;
    [SerializeField] private PopupStickerController stickerController;

    private FurnitureData currentFurniture;

    private void Awake()
    {
        CloseAllPopups();
    }

    // =========================================
    // OPEN FURNITURE POPUP
    // =========================================

    public void OpenFurniturePopup(FurnitureData furnitureData)
    {
        if (furnitureData == null)
            return;

        currentFurniture = furnitureData;

        CloseAllPopups();

        if (movelController != null)
            movelController.SetFurnitureData(currentFurniture);

        if (popupMovel != null)
            popupMovel.SetActive(true);
    }

    // =========================================
    // OPEN SKIN POPUP
    // =========================================

    public void OpenSkinPopup(FurnitureData furnitureData)
    {
        if (furnitureData == null)
            return;

        currentFurniture = furnitureData;

        CloseAllPopups();

        if (skinMovelController != null)
            skinMovelController.SetFurnitureData(currentFurniture);

        if (popupSkinMovel != null)
            popupSkinMovel.SetActive(true);
    }

    // =========================================
    // BACK TO FURNITURE POPUP
    // =========================================

    public void BackToFurniturePopup()
    {
        if (currentFurniture == null)
            return;

        CloseAllPopups();

        if (movelController != null)
            movelController.SetFurnitureData(currentFurniture);

        if (popupMovel != null)
            popupMovel.SetActive(true);
    }

    // =========================================
    // OPEN STICKER POPUP
    // =========================================

    public void OpenStickerPopup(StickerData stickerData)
    {
        if (stickerData == null)
            return;

        CloseAllPopups();

        if (stickerController != null)
            stickerController.SetStickerData(stickerData);

        if (popupSticker != null)
            popupSticker.SetActive(true);
    }

    // =========================================
    // CLOSE ALL POPUPS
    // =========================================

    public void CloseAllPopups()
    {
        if (popupMovel != null)
            popupMovel.SetActive(false);

        if (popupSkinMovel != null)
            popupSkinMovel.SetActive(false);

        if (popupSticker != null)
            popupSticker.SetActive(false);
    }
}
