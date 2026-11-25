using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fecha o popup quando o clique acontece fora do painel, ou seja, diretamente
/// na imagem de fundo (overlay) que cobre a tela. Cliques nos filhos do painel
/// nao fecham o popup.
/// </summary>
public class PopupOutsideClickCloser : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Controller que fecha o popup. Se vazio, usa o PopupController da cena.")]
    [SerializeField] private PopupController popupController;

    [Tooltip("Se true, usa CloseAllPopups (fecha qualquer popup aberto).")]
    [SerializeField] private bool closeAllPopups = true;

    [Tooltip("Se true, volta ao popup do movel (PopUpMovel) em vez de fechar todos.")]
    [SerializeField] private bool backToFurniturePopup;

    [Tooltip("Painel visivel do popup. Cliques dentro dele nunca fecham/voltam.")]
    [SerializeField] private RectTransform popupPanel;

    public void OnPointerClick(PointerEventData eventData)
    {
        // So reage quando o clique cai neste overlay, nao em um filho do painel.
        if (eventData == null || eventData.pointerCurrentRaycast.gameObject != gameObject)
            return;

        // Segunda barreira: mesmo que algum filho do painel nao tenha raycast,
        // o clique cai na imagem raiz do overlay. A posicao decide se e fora.
        if (popupPanel == null)
            popupPanel = transform.Find("PopUpBG") as RectTransform;

        if (popupPanel != null && RectTransformUtility.RectangleContainsScreenPoint(
                popupPanel, eventData.position, eventData.pressEventCamera))
            return;

        PopupController controller = popupController != null
            ? popupController
            : FindFirstObjectByType<PopupController>();
        if (controller == null)
            return;

        if (backToFurniturePopup)
        {
            // Mesmo caminho do BackBtt do PopUpSkinMovel.
            GameAudioManager.Instance?.PlayClick();
            controller.BackToFurniturePopup();
            return;
        }

        if (closeAllPopups)
            controller.CloseAllPopups();
        else
            controller.CloseStickerPopup();
    }
}
