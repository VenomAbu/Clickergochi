using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Botao da TV na HUD: abre o PopUpConfirmTV antes de seguir com qualquer
/// acao de anuncio. A recompensa/provedor de ads pode ser
/// ligado no evento OnAdConfirmed pelo Inspector, sem acoplar este botao a
/// nenhum SDK especifico.
/// </summary>
public class AdConfirmationButton : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private UnityEngine.UI.Button adButton;
    [Tooltip("Popup de confirmacao da TV. Se vazio, procura PopUpConfirmTV na cena.")]
    [SerializeField] private PopUpConfirmController confirmPopup;

    [Header("Textos do popup")]
    [SerializeField] private string title = "Assistir anúncio?";
    [SerializeField, TextArea(2, 4)] private string message =
        "Você quer assistir a este anúncio para receber a recompensa?";
    [SerializeField] private string confirmText = "Assistir";

    [Header("Ao confirmar")]
    [SerializeField] private UnityEvent onAdConfirmed = new UnityEvent();

    /// <summary>Evento de confirmacao (usado pelo fluxo de recompensa e pelo Inspector).</summary>
    public UnityEvent OnAdConfirmed => onAdConfirmed;

    /// <summary>Inscreve um codigo no confirmar sem depender de configuracao no Inspector.</summary>
    public void AddConfirmedListener(UnityAction listener)
    {
        if (listener != null)
            onAdConfirmed.AddListener(listener);
    }

    public void RemoveConfirmedListener(UnityAction listener)
    {
        if (listener != null)
            onAdConfirmed.RemoveListener(listener);
    }

    private void Awake()
    {
        if (adButton == null)
            adButton = GetComponent<UnityEngine.UI.Button>();
        if (adButton != null)
            adButton.onClick.AddListener(ShowConfirmation);
    }

    /// <summary>Mostra a confirmacao. Pode ser chamado direto por um Button.onClick.</summary>
    public void ShowConfirmation()
    {
        PopUpConfirmController popup = confirmPopup;
        if (popup == null)
            popup = FindPopUpConfirmTv();
        if (popup == null)
            popup = PopUpConfirmController.Instance;

        if (popup == null)
        {
            Debug.LogWarning("[Ads] PopUpConfirmTV não encontrado na cena; anúncio cancelado.");
            return;
        }

        popup.Show(title, message, confirmText, () => onAdConfirmed.Invoke());
    }

    private static PopUpConfirmController FindPopUpConfirmTv()
    {
        foreach (PopUpConfirmController popup in
                 FindObjectsByType<PopUpConfirmController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (popup.name == "PopUpConfirmTV")
                return popup;
        }
        return null;
    }
}
