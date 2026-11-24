using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Botao do Album que pede confirmacao no PopUpConfirmTV antes de zerar todos
/// os stickers conquistados. Nao mexe em nenhum outro dado do save.
/// </summary>
[RequireComponent(typeof(Button))]
public class StickerResetButton : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Button resetButton;
    [Tooltip("Popup de confirmacao da TV. Se vazio, procura PopUpConfirmTV na cena.")]
    [SerializeField] private PopUpConfirmController confirmPopup;

    [Header("Textos do popup")]
    [SerializeField] private string title = "Zerar álbum?";
    [SerializeField, TextArea(2, 4)] private string message =
        "Todos os stickers conquistados serão removidos do álbum. Deseja continuar?";
    [SerializeField] private string confirmText = "Zerar";

    [Header("Apos resetar (opcional)")]
    [SerializeField] private UnityEvent onReset = new UnityEvent();

    private bool bound;

    private void Awake()
    {
        Bind();
    }

    /// <summary>
    /// O Album comeca inativo, entao o Awake so roda quando ele abre. O
    /// controller do album chama isto no Build para o botao ja funcionar.
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
            popup = FindPopUpConfirmTv();
        if (popup == null)
            popup = PopUpConfirmController.Instance;

        if (popup == null)
        {
            Debug.LogWarning("[StickerAlbum] PopUpConfirmTV não encontrado; reset cancelado.");
            return;
        }

        popup.Show(title, message, confirmText, () =>
        {
            StickerCollection.ResetOwned();
            onReset.Invoke();
        });
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
