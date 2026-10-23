using System;
using UnityEngine.UIElements;

/// <summary>
/// Popup generico de confirmacao de compra. Montado a partir do overlay
/// "ConfirmPopup" do UXML; cada compra exibe titulo, mensagem e botoes
/// Confirmar/Cancelar, executando a acao apenas no Confirmar.
/// </summary>
public class ConfirmPopupUI
{
    private readonly VisualElement root;
    private readonly Label titleLabel;
    private readonly Label messageLabel;
    private readonly Button yesButton;
    private readonly Button noButton;
    private Action onConfirm;

    /// <summary>Disparado quando o jogador confirma. Usado para sons/feels.</summary>
    public event Action Confirmed;

    /// <summary>Disparado quando o jogador cancela (ou toca fora, se aplicavel).</summary>
    public event Action Canceled;

    public bool IsVisible => root != null && !root.ClassListContains("hidden");

    public ConfirmPopupUI(VisualElement popupRoot)
    {
        root = popupRoot;
        if (root == null)
            return;

        titleLabel = root.Q<Label>("ConfirmTitle");
        messageLabel = root.Q<Label>("ConfirmMessage");
        yesButton = root.Q<Button>("ConfirmYesButton");
        noButton = root.Q<Button>("ConfirmNoButton");

        if (yesButton != null)
            yesButton.clicked += HandleConfirm;
        if (noButton != null)
            noButton.clicked += Hide;
    }

    public void Show(string title, string message, string confirmText, Action confirm)
    {
        if (root == null)
        {
            confirm?.Invoke();
            return;
        }

        onConfirm = confirm;
        if (titleLabel != null)
            titleLabel.text = title;
        if (messageLabel != null)
            messageLabel.text = message;
        if (yesButton != null)
            yesButton.text = string.IsNullOrEmpty(confirmText) ? "✔ Confirmar" : confirmText;
        root.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        if (root == null)
            return;
        bool wasVisible = !root.ClassListContains("hidden");
        root.AddToClassList("hidden");
        onConfirm = null;
        if (wasVisible)
            Canceled?.Invoke();
    }

    private void HandleConfirm()
    {
        Action confirm = onConfirm;
        root?.AddToClassList("hidden");
        onConfirm = null;
        Confirmed?.Invoke();
        confirm?.Invoke();
    }
}
