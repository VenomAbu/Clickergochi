
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupStickerController : MonoBehaviour
{
    [Header("Sticker Information")]
    [SerializeField] private TMP_Text nameTMP;
    [SerializeField] private Image stickerImage;
    [SerializeField] private TMP_Text descriptionTMP;

    [Header("Raridade")]
    [SerializeField] private Image rarityBackground;
    [SerializeField] private TMP_Text rarityTMP;

    [Header("Popup Controller")]
    [SerializeField] private PopupController popupController;

    public void SetStickerData(StickerData stickerData)
    {
        if (stickerData == null)
            return;

        nameTMP.text = stickerData.stickerName;

        stickerImage.sprite = stickerData.stickerImage;
        stickerImage.enabled = stickerData.stickerImage != null;

        // A raridade sai da descricao e vai para o selo proprio no popup.
        descriptionTMP.text = stickerData.description;

        if (rarityBackground != null)
            rarityBackground.color = stickerData.GetRarityColor();

        if (rarityTMP != null)
            rarityTMP.text = stickerData.GetRarityLabel();
        // A cor do TMP e mantida: so o fundo muda com a raridade.
    }

    public void ClosePopup()
    {
        // O ClosePopUpBtt silencia o som generico; este popup tem som proprio.
        GameAudioManager.Instance?.PlayPopupClose();
        if (popupController != null)
            popupController.CloseAllPopups();
    }
}
