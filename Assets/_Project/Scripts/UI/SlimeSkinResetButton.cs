using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Botao da loja de skins do slime. Pede confirmacao no PopUpConfirm2 antes
/// de apagar as skins compradas e voltar para a skin azul padrao.
/// </summary>
[RequireComponent(typeof(Button))]
public class SlimeSkinResetButton : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Button resetButton;
    [Tooltip("Popup de confirmacao. Se vazio, procura PopUpConfirm2 na cena.")]
    [SerializeField] private PopUpConfirmController confirmPopup;

    [Header("Textos do popup")]
    [SerializeField] private string title = "Zerar skins?";
    [SerializeField, TextArea(2, 4)] private string message =
        "Todas as skins de slime compradas serão removidas e o slime voltará ao azul padrão. Deseja continuar?";
    [SerializeField] private string confirmText = "Zerar";

    [Header("Apos resetar (opcional)")]
    [SerializeField] private UnityEvent onReset = new UnityEvent();

    private bool bound;

    private void Awake()
    {
        Bind();
    }

    /// <summary>
    /// O SlimeSkinUI comeca inativo, entao o Awake so roda quando ele abre.
    /// O controller da loja chama isto no Build para o botao funcionar antes
    /// da primeira abertura.
    /// </summary>
    public void Bind()
    {
        if (bound)
            return;
        bound = true;

        if (resetButton == null)
            resetButton = GetComponent<Button>();
        resetButton.onClick.AddListener(AskConfirm);
    }

    private void OnDestroy()
    {
        if (bound && resetButton != null)
            resetButton.onClick.RemoveListener(AskConfirm);
    }

    public void AskConfirm()
    {
        PopUpConfirmController popup = confirmPopup;
        if (popup == null)
            popup = PopUpConfirmController.FindPopUpConfirm2();
        if (popup == null)
            popup = PopUpConfirmController.Instance;

        if (popup == null)
        {
            Debug.LogWarning("[SlimeSkinShop] PopUpConfirm2 não encontrado; reset cancelado.");
            return;
        }

        popup.Show(title, message, confirmText, () =>
        {
            SlimeSkinShopUGUIController.ResetOwned();
            onReset.Invoke();
        });
    }
}
