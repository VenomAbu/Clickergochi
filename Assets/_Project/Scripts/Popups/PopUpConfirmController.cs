using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup de confirmacao de compra da MainScene (GDD/TP3: toda compra confirma).
/// Vive no objeto de cena "PopUpConfirm", filho do PopUpsController, seguindo
/// o mesmo padrao dos demais popups (script proprio + referencias no Inspector).
/// Substitui o antigo ConfirmPopUp criado em runtime; a API Show/Hide/Instance
/// continua compativel com todos os call sites.
/// </summary>
public class PopUpConfirmController : MonoBehaviour
{
    public static PopUpConfirmController Instance { get; private set; }

    /// <summary>Busca um popup de confirmacao pelo nome do objeto na cena.</summary>
    public static PopUpConfirmController FindByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        foreach (PopUpConfirmController popup in
                 FindObjectsByType<PopUpConfirmController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (popup.name == objectName)
                return popup;
        }
        return null;
    }

    /// <summary>Popup generico PopUpConfirm2 da MainScene.</summary>
    public static PopUpConfirmController FindPopUpConfirm2()
        => FindByName("PopUpConfirm2");

    [Header("Information")]
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text descriptionTMP;

    [Header("Buttons")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    [Header("Popup Controller")]
    [SerializeField] private PopupController popupController;

    [Header("Transição")]
    [SerializeField] private UITransitionController transitionController;

    private Action onConfirm;
    private bool visible;
    private bool initialized;

    /// <summary>True quando o popup esta na tela.</summary>
    public bool IsVisible => visible;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// O objeto comeca inativo na cena, entao o Awake so roda no primeiro
    /// SetActive(true). Este metodo garante que os botoes e a instancia
    /// estejam prontos antes disso, sem depender de ordem de execucao.
    /// </summary>
    public void EnsureInitialized()
    {
        if (initialized)
            return;
        initialized = true;
        Instance = this;

        if (transitionController == null)
            transitionController = FindFirstObjectByType<UITransitionController>();

        // Mantem os listeners persistentes do cenario (ex.: UIFeelGeral) e
        // adiciona os nossos. Os listeners do antigo PopupMovelController
        // sao removidos no editor quando o componente e trocado.
        WireButton(cancelButton, Cancel);
        WireButton(confirmButton, Confirm);

        // Clique no overlay (fora do painel) tambem cancela.
        Button overlay = GetComponent<Button>();
        if (overlay != null)
            WireButton(overlay, Cancel);
    }

    private static void WireButton(Button button, Action action)
    {
        if (button == null)
            return;
        button.onClick.AddListener(() => action?.Invoke());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Permite ao PopupController associar este popup a ele.</summary>
    public void Bind(PopupController controller)
    {
        EnsureInitialized();
        popupController = controller;
    }

    // =========================================
    // FLOW
    // =========================================

    /// <summary>
    /// Mostra titulo, descricao, sprite e texto do botao; executa a acao ao confirmar.
    /// </summary>
    public void Show(
        string title,
        string message,
        string confirmText,
        Action confirm,
        Sprite icon = null)
    {
        EnsureInitialized();
        onConfirm = confirm;

        if (titleTMP != null)
            titleTMP.text = title ?? string.Empty;
        if (descriptionTMP != null)
            descriptionTMP.text = message ?? string.Empty;
        if (itemImage != null)
        {
            itemImage.sprite = icon;
            // Sem sprite, um Image UGUI desenharia um retangulo em branco.
            itemImage.enabled = icon != null;
        }

        TMP_Text confirmLabel = confirmButton != null
            ? confirmButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (confirmLabel != null)
            confirmLabel.text = string.IsNullOrEmpty(confirmText) ? "Confirmar" : confirmText;

        if (transitionController != null)
        {
            transitionController.OpenPopup(gameObject);
        }
        else
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
        visible = true;
        GameAudioManager.Instance?.PlayOpen();
    }

    /// <summary>Esconde sem executar nada (sem som, igual ao comportamento antigo).</summary>
    public void Hide()
    {
        Hide(true);
    }

    public void Hide(bool animate)
    {
        EnsureInitialized();
        visible = false;
        onConfirm = null;

        if (animate && transitionController != null)
        {
            transitionController.ClosePopup(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Confirm()
    {
        if (!visible)
            return;

        Action confirm = onConfirm;
        Hide();
        confirm?.Invoke();
    }

    private void Cancel()
    {
        if (!visible)
            return;

        Hide();
        GameAudioManager.Instance?.PlayClose();
    }
}
