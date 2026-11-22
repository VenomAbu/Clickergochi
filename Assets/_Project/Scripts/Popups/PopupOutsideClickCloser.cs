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

    public void OnPointerClick(PointerEventData eventData)
    {
        // So reage quando o clique cai neste overlay, nao em um filho do painel.
        if (eventData == null || eventData.pointerCurrentRaycast.gameObject != gameObject)
            return;

        PopupController controller = popupController != null
            ? popupController
            : FindFirstObjectByType<PopupController>();
        if (controller == null)
            return;

        if (closeAllPopups)
            controller.CloseAllPopups();
        else
            controller.CloseStickerPopup();
    }
}
