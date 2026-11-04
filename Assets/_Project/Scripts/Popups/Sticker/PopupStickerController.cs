
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupStickerController : MonoBehaviour
{
    [Header("Sticker Information")]
    [SerializeField] private TMP_Text nameTMP;
    [SerializeField] private Image stickerImage;
    [SerializeField] private TMP_Text descriptionTMP;

    [Header("Popup Controller")]
    [SerializeField] private PopupController popupController;

    public void SetStickerData(StickerData stickerData)
    {
        if (stickerData == null)
            return;

        nameTMP.text = stickerData.stickerName;

        stickerImage.sprite = stickerData.stickerImage;
        stickerImage.enabled = stickerData.stickerImage != null;

        descriptionTMP.text = stickerData.description;
    }

    public void ClosePopup()
    {
        if (popupController != null)
            popupController.CloseAllPopups();
    }
}
