using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup de confirmação exclusivo da compra de pacotes de stickers.
/// Vive no objeto de cena "PopUpConfirmPack", filho do PopUpsController.
/// </summary>
public class PopUpConfirmPackController : MonoBehaviour
{
    public static PopUpConfirmPackController Instance { get; private set; }

    [Header("Information")]
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text descriptionTMP;

    [Header("Buttons")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text priceTMP;

    [Header("Transição")]
    [SerializeField] private UITransitionController transitionController;

    private Action onConfirm;
    private bool visible;
    private bool initialized;

    public bool IsVisible => visible;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// O objeto começa inativo, então o Awake só roda no primeiro SetActive(true).
    /// Este método garante que os botões e a instância estejam prontos antes disso.
    /// </summary>
    public void EnsureInitialized()
    {
        if (initialized)
            return;
        initialized = true;
        Instance = this;

        if (transitionController == null)
            transitionController = FindFirstObjectByType<UITransitionController>();

        if (priceTMP == null && confirmButton != null)
            priceTMP = confirmButton.GetComponentInChildren<TMP_Text>(true);

        WireButton(cancelButton, Cancel);
        WireButton(confirmButton, Confirm);

        // Clique no overlay (fora do painel) também cancela.
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

    // =========================================
    // FLOW
    // =========================================

    /// <summary>
    /// Mostra o popup com nome do pacote, descrição, ícone e preço no ConfirmBtt.
    /// </summary>
    public void Show(
        string title,
        string message,
        string price,
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
            itemImage.enabled = icon != null;
        }
        if (priceTMP != null)
            priceTMP.text = price ?? string.Empty;

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
        MainHUDUGUIController.Instance?.PlayOpen();
    }

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
        MainHUDUGUIController.Instance?.PlayClose();
    }
}
