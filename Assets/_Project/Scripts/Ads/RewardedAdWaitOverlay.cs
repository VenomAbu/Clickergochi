using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Clickergochi.Ads
{
    /// <summary>
    /// Tela mostrada quando o jogador pediu um anuncio recompensado mas o
    /// provedor ainda nao tem nenhum anuncio carregado.
    /// <para>- Conta 10 segundos em tempo real (funciona com o jogo pausado).</para>
    /// <para>- Se o anuncio ficar pronto antes disso, a tela fecha e o anuncio toca.</para>
    /// <para>- Se chegar a zero sem anuncio, devolve Watched para liberar a recompensa.</para>
    /// <para>- O botao Voltar devolve Skipped, sem recompensa.</para>
    ///
    /// Nao depende de nada na cena: cria o proprio Canvas em runtime, igual ao
    /// overlay de teste do Editor.
    /// </summary>
    public sealed class RewardedAdWaitOverlay : MonoBehaviour
    {
        /// <summary>Duracao maxima da espera antes de liberar a recompensa.</summary>
        public static float WaitSeconds = RewardedAdBridge.WaitForReadySeconds;

        private static RewardedAdWaitOverlay _instance;

        private IRewardedAdProvider _provider;
        private string _placementId;
        private Action<RewardedAdResult> _onComplete;
        private float _startedAt;
        private float _elapsed;
        private bool _finished;

        private float _previousTimeScale = 1f;
        private bool _restored;

        private TextMeshProUGUI _countdownText;
        private Image _progressFill;

        public static bool IsShowing => _instance != null;

        public static void Show(string placementId, IRewardedAdProvider provider,
            Action<RewardedAdResult> onComplete)
        {
            if (_instance != null)
            {
                Debug.LogWarning("[Ads] Ja existe uma tela de espera aberta.");
                onComplete?.Invoke(RewardedAdResult.Unavailable);
                return;
            }

            GameObject go = new GameObject("[RewardedAdWait]");
            RewardedAdWaitOverlay overlay = go.AddComponent<RewardedAdWaitOverlay>();
            _instance = overlay;
            overlay.Begin(placementId, provider, onComplete);
        }

        private void Begin(string placementId, IRewardedAdProvider provider,
            Action<RewardedAdResult> onComplete)
        {
            _placementId = placementId;
            _provider = provider;
            _onComplete = onComplete;
            _startedAt = Time.realtimeSinceStartup;
            _elapsed = 0f;
            _previousTimeScale = Time.timeScale;

            BuildUi();
            Time.timeScale = 0f;
            StartCoroutine(WaitRoutine());
        }

        private bool ProviderReady()
            => _provider != null && _provider.IsReady;

        private IEnumerator WaitRoutine()
        {
            float duration = Mathf.Max(0.1f, WaitSeconds);

            while (_elapsed < duration)
            {
                // Checa antes de contar: se o anuncio chegou no mesmo instante
                // em que o contador zera, prefere tocar o anuncio.
                if (ProviderReady())
                {
                    StartAd();
                    yield break;
                }

                // Usa relogio real em vez de acumular delta: um unico frame
                // longo (tool/debug/foco do Editor) nao deve distorcer a conta.
                _elapsed = Time.realtimeSinceStartup - _startedAt;
                RefreshUi(duration);
                yield return null;
            }

            if (ProviderReady())
                StartAd();
            else
                Finish(RewardedAdResult.Watched);
        }

        private void StartAd()
        {
            if (_finished)
                return;
            _finished = true;

            Debug.Log("[Ads] Anuncio ficou pronto durante a espera; iniciando.");
            Action<RewardedAdResult> callback = _onComplete;
            _onComplete = null;

            CloseOverlay();
            RewardedAdBridge.ShowRewarded(_placementId, callback);
        }

        private void Finish(RewardedAdResult result)
        {
            if (_finished)
                return;
            _finished = true;

            Action<RewardedAdResult> callback = _onComplete;
            _onComplete = null;

            if (result == RewardedAdResult.Watched)
                Debug.Log($"[Ads] Anuncio nao carregou em {WaitSeconds:0}s; " +
                          "recompensa liberada.");
            else
                Debug.Log($"[Ads] Espera encerrada ({result}); sem recompensa.");

            CloseOverlay();
            callback?.Invoke(result);
        }

        private void CloseOverlay()
        {
            RestoreTime();
            if (_instance == this)
                _instance = null;
            Destroy(gameObject);
        }

        private void RestoreTime()
        {
            if (_restored)
                return;
            _restored = true;
            Time.timeScale = _previousTimeScale;
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

        private void RefreshUi(float duration)
        {
            float remaining = Mathf.Max(0f, duration - _elapsed);
            if (_countdownText != null)
                _countdownText.text = $"Tempo restante: {Mathf.CeilToInt(remaining)}s";
            if (_progressFill != null)
                _progressFill.fillAmount = Mathf.Clamp01(_elapsed / duration);
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("WaitCanvas",
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

            RectTransform panel = CreateRect("WaitPanel", canvasGo.transform,
                Vector2.zero, Vector2.one);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.045f, 0.055f, 0.11f, 1f);
            panelImage.raycastTarget = true;

            RectTransform header = CreateRect("Header", panel,
                new Vector2(0f, 0.82f), new Vector2(1f, 1f));
            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.color = new Color(0.36f, 0.55f, 0.95f, 1f);
            headerImage.raycastTarget = false;

            CreateText("Title", header, Vector2.zero, Vector2.one,
                "PREPARANDO ANÚNCIO", 58f, TextAlignmentOptions.Center, Color.white);

            CreateText("Message", panel,
                new Vector2(0.10f, 0.45f), new Vector2(0.90f, 0.78f),
                "Aguarde enquanto o anúncio é carregado.\n\n" +
                "Se ele ficar pronto, começará automaticamente.\n" +
                "Se não aparecer, você recebe a recompensa.",
                40f, TextAlignmentOptions.Center, Color.white);

            _countdownText = CreateText("Countdown", panel,
                new Vector2(0.12f, 0.31f), new Vector2(0.88f, 0.39f),
                $"Tempo restante: {Mathf.CeilToInt(Mathf.Max(0.1f, WaitSeconds))}s",
                48f, TextAlignmentOptions.Center,
                new Color(1f, 0.86f, 0.35f, 1f));

            RectTransform progressBg = CreateRect("ProgressBg", panel,
                new Vector2(0.18f, 0.265f), new Vector2(0.82f, 0.295f));
            Image progressBgImage = progressBg.gameObject.AddComponent<Image>();
            progressBgImage.color = new Color(0f, 0f, 0f, 0.55f);
            progressBgImage.raycastTarget = false;

            RectTransform fill = CreateRect("Fill", progressBg, Vector2.zero, Vector2.one);
            _progressFill = fill.gameObject.AddComponent<Image>();
            _progressFill.color = new Color(0.35f, 0.75f, 0.95f, 1f);
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = 0;
            _progressFill.fillAmount = 0f;
            _progressFill.raycastTarget = false;

            Button back = CreateButton("BackBtt", panel,
                new Vector2(0.26f, 0.10f), new Vector2(0.74f, 0.21f),
                "Voltar", new Color(0.38f, 0.40f, 0.50f, 1f), out _);
            back.onClick.AddListener(() =>
            {
                Debug.Log("[Ads] Jogador voltou durante a espera do anuncio.");
                Finish(RewardedAdResult.Skipped);
            });
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
                text, 46f, TextAlignmentOptions.Center, Color.white);
            return button;
        }
    }
}
