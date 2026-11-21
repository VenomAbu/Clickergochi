using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Transicao de opacidade entre salas do HorizontalNavegationController.
/// Usa o sprite de degrade (transparente a esquerda, branco a direita) nas
/// bordas dos quartos e anima o alpha de ambos os painéis conforme o
/// progresso do swipe/seta: o quarto que sai desaparece e o que entra
/// aparece, com o gradiente suavizando a costura no pico da transicao.
/// </summary>
public class HorizontalEdgeFadeController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private RectTransform[] navigationPanels;

    [Header("Fade Sprite")]
    [SerializeField] private Sprite edgeFadeSprite;

    [Header("Settings")]
    [SerializeField] private float edgeFadeWidth = 120f;

    [SerializeField, Range(0f, 1f)]
    private float edgeFadeAlpha = 0.65f;

    [Tooltip("Alpha minimo dos quartos no pico da transicao (0 = totalmente transparente).")]
    [SerializeField, Range(0f, 1f)]
    private float minPanelAlpha = 0.15f;

    private Image[] fadeImages;
    private CanvasGroup[] panelGroups;

    private int currentPanelIndex = -1;
    private int nextPanelIndex = -1;
    private int direction;
    private bool fading;

    private void Awake()
    {
        InitializeFades();
    }

    // =========================================
    // INITIALIZATION
    // =========================================

    private void InitializeFades()
    {
        if (navigationPanels == null)
            return;

        fadeImages = new Image[navigationPanels.Length];
        panelGroups = new CanvasGroup[navigationPanels.Length];

        for (int i = 0; i < navigationPanels.Length; i++)
        {
            if (navigationPanels[i] == null)
                continue;

            // CanvasGroup controla a opacidade de todo o quarto.
            CanvasGroup group =
                navigationPanels[i].GetComponent<CanvasGroup>();
            if (group == null)
                group = navigationPanels[i].gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            panelGroups[i] = group;

            GameObject fadeObject = new GameObject(
                "EdgeFade",
                typeof(RectTransform),
                typeof(Image)
            );

            fadeObject.transform.SetParent(
                navigationPanels[i],
                false
            );

            RectTransform fadeRect =
                fadeObject.GetComponent<RectTransform>();

            Image fadeImage =
                fadeObject.GetComponent<Image>();

            fadeRect.anchorMin = new Vector2(0f, 0f);
            fadeRect.anchorMax = new Vector2(0f, 1f);
            fadeRect.pivot = new Vector2(0f, 0.5f);
            fadeRect.anchoredPosition = Vector2.zero;
            fadeRect.sizeDelta = new Vector2(edgeFadeWidth, 0f);

            fadeImage.sprite = edgeFadeSprite;
            fadeImage.type = Image.Type.Simple;
            fadeImage.raycastTarget = false;
            fadeImage.color = new Color(1f, 1f, 1f, 0f);
            fadeImage.enabled = false;

            fadeImages[i] = fadeImage;
        }
    }

    // =========================================
    // SHOW FADE
    // =========================================

    public void ShowFades(
        int currentPanelIndex,
        int nextPanelIndex,
        int direction)
    {
        HideAll();

        this.currentPanelIndex = currentPanelIndex;
        this.nextPanelIndex = nextPanelIndex;
        this.direction = direction;
        fading = true;

        if (fadeImages == null)
            return;

        // Arrastando para a esquerda:
        // painel atual = borda direita (degrade invertido)
        // proximo painel = borda esquerda (degrade original)
        if (direction == 1)
        {
            PlaceFade(currentPanelIndex, true);
            PlaceFade(nextPanelIndex, false);
        }
        else
        {
            // Arrastando para a direita: invertido.
            PlaceFade(currentPanelIndex, false);
            PlaceFade(nextPanelIndex, true);
        }

        SetProgress(0f);
    }

    private void PlaceFade(int panelIndex, bool rightSide)
    {
        if (panelIndex < 0 ||
            panelIndex >= fadeImages.Length ||
            fadeImages[panelIndex] == null)
            return;

        Image fadeImage = fadeImages[panelIndex];
        RectTransform fadeRect = fadeImage.rectTransform;

        if (rightSide)
        {
            fadeRect.anchorMin = new Vector2(1f, 0f);
            fadeRect.anchorMax = new Vector2(1f, 1f);
            fadeRect.pivot = new Vector2(1f, 0.5f);

            // Inverte o degrade: branco encostado na borda do quarto.
            fadeRect.localScale = new Vector3(-1f, 1f, 1f);
        }
        else
        {
            fadeRect.anchorMin = new Vector2(0f, 0f);
            fadeRect.anchorMax = new Vector2(0f, 1f);
            fadeRect.pivot = new Vector2(0f, 0.5f);
            fadeRect.localScale = Vector3.one;
        }

        fadeRect.anchoredPosition = Vector2.zero;
        fadeRect.sizeDelta = new Vector2(edgeFadeWidth, 0f);
        fadeImage.enabled = edgeFadeSprite != null;
        fadeImage.transform.SetAsLastSibling();
    }

    // =========================================
    // PROGRESS
    // =========================================

    /// <summary>
    /// Chamado pelo navigation a cada frame do arrasto/transicao.
    /// progress 0 = quarto atual visivel / 1 = proximo quarto visivel.
    /// </summary>
    public void SetProgress(float progress)
    {
        if (!fading || fadeImages == null)
            return;

        float t = Mathf.Clamp01(progress);

        SetPanelAlpha(currentPanelIndex, Mathf.Lerp(1f, minPanelAlpha, t));
        SetPanelAlpha(nextPanelIndex, Mathf.Lerp(minPanelAlpha, 1f, t));

        // O gradiente aparece com forca no meio da transicao e some
        // nas pontas, onde cada quarto ja esta completamente opaco.
        float fadeAlpha = edgeFadeAlpha * Mathf.Sin(t * Mathf.PI);
        SetFadeAlpha(currentPanelIndex, fadeAlpha);
        SetFadeAlpha(nextPanelIndex, fadeAlpha);
    }

    private void SetPanelAlpha(int index, float alpha)
    {
        if (index < 0 || panelGroups == null ||
            index >= panelGroups.Length || panelGroups[index] == null)
            return;

        panelGroups[index].alpha = alpha;
    }

    private void SetFadeAlpha(int index, float alpha)
    {
        if (index < 0 || fadeImages == null ||
            index >= fadeImages.Length || fadeImages[index] == null)
            return;

        Image image = fadeImages[index];
        image.color = new Color(1f, 1f, 1f, alpha);
        image.enabled = edgeFadeSprite != null && alpha > 0.001f;
    }

    // =========================================
    // HIDE FADE
    // =========================================

    public void HideAll()
    {
        if (panelGroups != null)
        {
            foreach (CanvasGroup group in panelGroups)
            {
                if (group != null)
                    group.alpha = 1f;
            }
        }

        if (fadeImages != null)
        {
            foreach (Image fadeImage in fadeImages)
            {
                if (fadeImage != null)
                    fadeImage.enabled = false;
            }
        }

        currentPanelIndex = -1;
        nextPanelIndex = -1;
        direction = 0;
        fading = false;
    }
}
