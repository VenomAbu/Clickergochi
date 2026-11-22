using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fecha o balao de medicao quando o proprio dialogo e clicado.
/// Fica no dialogo (filho do botao de medicao) para que o clique seja
/// consumido aqui e nao reabra o balao pelo botao pai.
/// </summary>
public class MeasurementBubbleDismisser : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.pointerCurrentRaycast.gameObject != gameObject)
            return;

        RoomController.HideActiveBubbles();
    }
}
