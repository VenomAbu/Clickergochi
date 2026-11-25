using System;
using Clickergochi.Ads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anuncio recompensado de teste, usado SOMENTE no Editor.
///
/// O LevelPlay nao exibe anuncio no Editor (a API informa "not available on
/// this platform" e nao dispara OnAdClosed). Este overlay mostra uma tela de
/// propaganda com progresso e so devolve Watched quando o tempo termina.
/// Sair antes (Pular) devolve Skipped, sem recompensa.
///
/// Em build Android/iOS este arquivo nao e usado: o AdsManager/LevelPlay real
/// cuida do anuncio.
/// </summary>
public sealed class EditorRewardedAdOverlay : MonoBehaviour
{
    /// <summary>Duracao do anuncio de teste em segundos.</summary>
    public static float DurationSeconds = 10f;

    private static EditorRewardedAdOverlay _instance;

    private Action<RewardedAdResult> _onComplete;
    private float _previousTimeScale = 1f;
    private bool _previousAudioPause;
    private bool _restored;
    private bool _finished;

    private TextMeshProUGUI _countdownText;
    private TextMeshProUGUI _continueLabel;
    private Image _progressFill;
    private Button _continueButton;
    private Button _skipButton;

    public static void Show(string placementId, Action<RewardedAdResult> onComplete)
    {
        if (_instance != null)
        {
            Debug.LogWarning("[Ads] Ja existe um anuncio de teste na tela.");
            onComplete?.Invoke(RewardedAdResult.Unavailable);
            return;
        }

        GameObject go = new GameObject("[EditorRewardedAd]");
        EditorRewardedAdOverlay ad = go.AddComponent<EditorRewardedAdOverlay>();
        _instance = ad;
        ad.Begin(placementId, onComplete);
    }

    private void Begin(string placementId, Action<RewardedAdResult> onComplete)
    {
        _onComplete = onComplete;
        _previousTimeScale = Time.timeScale;
        _previousAudioPause = AudioListener.pause;

        BuildUi(placementId);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        StartCoroutine(RunAd());
    }

    private void BuildUi(string placementId)
    {
        GameObject canvasGo = new GameObject("AdCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight =
            CanvasScalerAspectMatcher.ShouldMatchHeight(
                Screen.width, Screen.height, scaler.referenceResolution)
                ? 1f
                : 0f;

        RectTransform panel = CreateRect("AdPanel", canvasGo.transform,
            Vector2.zero, Vector2.one);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.065f, 0.12f, 1f);
        panelImage.raycastTarget = true;

        RectTransform header = CreateRect("Header", panel,
            new Vector2(0f, 0.88f), new Vector2(1f, 1f));
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color(0.95f, 0.28f, 0.38f, 1f);
        headerImage.raycastTarget = false;

        CreateText("Title", header, Vector2.zero, Vector2.one,
            "PROPAGANDA", 62f, TextAlignmentOptions.Center, Color.white);

        RectTransform card = CreateRect("AdCard", panel,
            new Vector2(0.13f, 0.34f), new Vector2(0.87f, 0.80f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(1f, 0.86f, 0.35f, 1f);
        cardImage.raycastTarget = false;

        CreateText("AdText", card, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f),
            "SLIME CLICKER!!\n\nAssista até o fim para ganhar\no Golden Pack de stickers",
            52f, TextAlignmentOptions.Center, new Color(0.16f, 0.10f, 0.05f, 1f));

        _countdownText = CreateText("Countdown", panel,
            new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.31f),
            $"Tempo restante: {Mathf.CeilToInt(DurationSeconds)}s",
            42f, TextAlignmentOptions.Center, Color.white);

        RectTransform progressBg = CreateRect("ProgressBg", panel,
            new Vector2(0.18f, 0.195f), new Vector2(0.82f, 0.225f));
        Image progressBgImage = progressBg.gameObject.AddComponent<Image>();
        progressBgImage.color = new Color(0f, 0f, 0f, 0.5f);
        progressBgImage.raycastTarget = false;

        RectTransform progressFillRect = CreateRect("Fill", progressBg,
            Vector2.zero, Vector2.one);
        _progressFill = progressFillRect.gameObject.AddComponent<Image>();
        _progressFill.color = new Color(0.35f, 0.95f, 0.55f, 1f);
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillOrigin = 0;
        _progressFill.fillAmount = 0f;
        _progressFill.raycastTarget = false;

        _continueButton = CreateButton("ContinueBtt", panel,
            new Vector2(0.24f, 0.07f), new Vector2(0.76f, 0.16f),
            "Continuar", new Color(0.25f, 0.75f, 0.42f, 1f),
            out _continueLabel);
        _continueButton.interactable = false;
        _continueButton.onClick.AddListener(() => Finish(RewardedAdResult.Watched));

        _skipButton = CreateButton("SkipBtt", panel,
            new Vector2(0.79f, 0.895f), new Vector2(0.965f, 0.965f),
            "Pular", new Color(0.35f, 0.35f, 0.42f, 0.95f),
            out _);
        _skipButton.onClick.AddListener(() => Finish(RewardedAdResult.Skipped));

        CreateText("Footer", panel,
            new Vector2(0.1f, 0.015f), new Vector2(0.9f, 0.055f),
            $"Anúncio de teste no Editor · placement: {placementId}",
            26f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.65f));
    }

    private System.Collections.IEnumerator RunAd()
    {
        float duration = Mathf.Max(0.1f, DurationSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (_progressFill != null)
                _progressFill.fillAmount = progress;
            if (_countdownText != null)
                _countdownText.text =
                    $"Tempo restante: {Mathf.CeilToInt(duration - elapsed)}s";

            yield return null;
        }

        _finished = true;
        if (_countdownText != null)
            _countdownText.text = "Anúncio concluído!";
        if (_progressFill != null)
            _progressFill.fillAmount = 1f;
        if (_skipButton != null)
            _skipButton.gameObject.SetActive(false);
        if (_continueButton != null)
            _continueButton.interactable = true;
        if (_continueLabel != null)
            _continueLabel.text = "Continuar";

        // Mostra "concluído" por um instante e libera so então.
        yield return new WaitForSecondsRealtime(0.6f);

        if (_finished)
            Finish(RewardedAdResult.Watched);
    }

    private void Finish(RewardedAdResult result)
    {
        if (_onComplete == null)
            return;

        Action<RewardedAdResult> callback = _onComplete;
        _onComplete = null;
        _finished = false;

        RestoreTime();
        gameObject.SetActive(false);
        Destroy(gameObject);
        callback?.Invoke(result);
    }

    private void RestoreTime()
    {
        if (_restored)
            return;
        _restored = true;
        Time.timeScale = _previousTimeScale;
        AudioListener.pause = _previousAudioPause;
    }

    private void OnDestroy()
    {
        RestoreTime();

        if (_instance == this)
            _instance = null;

        if (_onComplete != null)
        {
            Action<RewardedAdResult> callback = _onComplete;
            _onComplete = null;
            callback?.Invoke(RewardedAdResult.Unavailable);
        }
    }

    private static RectTransform CreateRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, string text, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, string text, Color color,
        out TextMeshProUGUI label)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateText("Label", rect, Vector2.zero, Vector2.one,
            text, 42f, TextAlignmentOptions.Center, Color.white);
        return button;
    }
}
