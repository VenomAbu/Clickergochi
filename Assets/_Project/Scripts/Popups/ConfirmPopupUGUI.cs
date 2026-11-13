using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup UGUI de confirmacao de compra (GDD/TP3: toda compra pede
/// confirmacao). Montado em runtime sob o PopUpsController reaproveitando a
/// fonte, o fundo e os botoes da propria cena — nenhuma edicao de cena e
/// necessaria. Estilo pronto para ser trocado por um prefab desenhado pelo
/// time depois; a API (Show/Hide/Instance) nao muda.
/// </summary>
public class ConfirmPopupUGUI : MonoBehaviour
{
    public static ConfirmPopupUGUI Instance { get; private set; }

    private TMP_Text titleLabel;
    private TMP_Text messageLabel;
    private TMP_Text confirmLabel;
    private Action onConfirm;
    private bool visible;

    /// <summary>True quando o popup esta na tela.</summary>
    public bool IsVisible => visible;

    /// <summary>
    /// Cria (uma unica vez) o popup de confirmacao dentro de parent,
    /// copiando o visual das referencias da cena.
    /// </summary>
    public static void EnsureBuilt(
        Transform parent,
        TMP_FontAsset font,
        Image panelSource,
        Image confirmButtonSource,
        Image cancelButtonSource)
    {
        if (parent == null || Instance != null)
            return;

        var root = new GameObject("ConfirmPopUp", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        // Obrigatorio: um RectTransform novo nasce 0x0 no centro. Sem isso os
        // filhos em % de ancora colapsam todos no mesmo ponto (os textos ficam
        // empilhados no meio da tela e o overlay nao bloqueia clique algum).
        SetAnchors((RectTransform)root.transform, 0f, 0f, 1f, 1f);

        var popup = root.AddComponent<ConfirmPopupUGUI>();
        popup.Build(font, panelSource, confirmButtonSource, cancelButtonSource);

        // Por cima de todos os popups, iniciando escondido.
        root.transform.SetAsLastSibling();
        root.SetActive(false);
        Instance = popup;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================
    // BUILD (runtime, % de ancora = adapta a tela)
    // =========================================

    private void Build(
        TMP_FontAsset font,
        Image panelSource,
        Image confirmButtonSource,
        Image cancelButtonSource)
    {
        // Overlay escuro que tambem bloqueia o clique fora do painel
        // (clicar fora = cancelar).
        var overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.65f);
        overlay.raycastTarget = true;

        var overlayButton = gameObject.AddComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.targetGraphic = overlay;
        overlayButton.onClick.AddListener(Cancel);

        // Painel central.
        RectTransform panel = CreateRect("Panel", transform);
        SetAnchors(panel, 0.06f, 0.27f, 0.94f, 0.73f);

        var panelImage = panel.gameObject.AddComponent<Image>();
        if (panelSource != null && panelSource.sprite != null)
        {
            panelImage.sprite = panelSource.sprite;
            panelImage.type = panelSource.type;
            panelImage.color = panelSource.color;
        }
        else
        {
            panelImage.color = new Color(0.13f, 0.12f, 0.16f, 0.98f);
        }

        // Titulo.
        titleLabel = CreateText("ConfirmTitle", panel, font, 44f,
            TextAlignmentOptions.Center);
        SetAnchors(titleLabel.rectTransform, 0.05f, 0.74f, 0.95f, 0.95f);

        // Mensagem.
        messageLabel = CreateText("ConfirmMessage", panel, font, 30f,
            TextAlignmentOptions.Center);
        SetAnchors(messageLabel.rectTransform, 0.07f, 0.36f, 0.93f, 0.72f);

        // Botoes: Cancelar (esquerda) e Confirmar (direita).
        CreateButton("ConfirmNoButton", panel, font,
            new Vector2(0.08f, 0.09f), new Vector2(0.47f, 0.31f),
            cancelButtonSource, "Cancelar", Cancel, out _);

        CreateButton("ConfirmYesButton", panel, font,
            new Vector2(0.53f, 0.09f), new Vector2(0.92f, 0.31f),
            confirmButtonSource, "Confirmar", Confirm, out confirmLabel);
    }

    private void CreateButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Image source,
        string label,
        Action onClick,
        out TMP_Text labelText)
    {
        RectTransform rect = CreateRect(name, parent);
        SetAnchors(rect, anchorMin, anchorMax);

        var image = rect.gameObject.AddComponent<Image>();
        if (source != null && source.sprite != null)
        {
            image.sprite = source.sprite;
            image.type = source.type;
            image.color = source.color;
        }
        else
        {
            image.color = name.Contains("No")
                ? new Color(0.78f, 0.28f, 0.24f)
                : new Color(0.25f, 0.65f, 0.35f);
        }

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(() => onClick?.Invoke());

        labelText = CreateText("Label", rect, font, 30f, TextAlignmentOptions.Center);
        SetAnchors(labelText.rectTransform, 0f, 0f, 1f, 1f);
        labelText.text = label;
        labelText.color = Color.white;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = new Color(0.4f, 0.19f, 0.19f);
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(
        RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetAnchors(
        RectTransform rect,
        float minX, float minY, float maxX, float maxY)
    {
        SetAnchors(rect, new Vector2(minX, minY), new Vector2(maxX, maxY));
    }

    // =========================================
    // FLOW
    // =========================================

    /// <summary>Mostra titulo/mensagem e executa a acao ao confirmar.</summary>
    public void Show(string title, string message, string confirmText, Action confirm)
    {
        // Sem popup montado: executa direto (mesmo fallback da versao UI Toolkit).
        if (titleLabel == null)
        {
            confirm?.Invoke();
            return;
        }

        onConfirm = confirm;
        titleLabel.text = title;
        messageLabel.text = message;
        if (confirmLabel != null)
            confirmLabel.text = string.IsNullOrEmpty(confirmText)
                ? "Confirmar"
                : confirmText;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        visible = true;
        MainHUDUGUIController.Instance?.PlayOpen();
    }

    /// <summary>Esconde sem executar nada.</summary>
    public void Hide()
    {
        if (!visible)
            return;

        visible = false;
        onConfirm = null;
        gameObject.SetActive(false);
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
