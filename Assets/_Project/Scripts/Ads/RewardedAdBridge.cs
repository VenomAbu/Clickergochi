using System;
using System.Collections;
using UnityEngine;

namespace Clickergochi.Ads
{
    /// <summary>Resultado final de um anuncio recompensado.</summary>
    public enum RewardedAdResult
    {
        /// <summary>Anuncio assistido ate o fim: a recompensa pode ser dada.</summary>
        Watched,
        /// <summary>Jogador fechou o anuncio antes do fim: sem recompensa.</summary>
        Skipped,
        /// <summary>Falha de carregamento/rede: sem recompensa.</summary>
        Failed,
        /// <summary>Nenhum provedor disponível: sem recompensa.</summary>
        Unavailable
    }

    /// <summary>
    /// Interface minima que qualquer SDK de anuncios precisa implementar.
    /// O fluxo do jogo so conhece esta interface; trocar de SDK nao mexe na HUD,
    /// na navegacao nem no PackUI.
    /// </summary>
    public interface IRewardedAdProvider
    {
        string DisplayName { get; }
        bool IsReady { get; }

        /// <summary>
        /// Chama onComplete exatamente uma vez, com Watched somente quando o
        /// jogador assistiu o anuncio ate o final.
        /// </summary>
        void Show(string placementId, Action<RewardedAdResult> onComplete);
    }

    /// <summary>
    /// Ponte unica entre a UI e o provedor de anuncios. Por padrao usa um
    /// anuncio simulado no Editor, para o fluxo poder ser testado sem SDK.
    /// </summary>
    public static class RewardedAdBridge
    {
        public static event Action<IRewardedAdProvider> ProviderChanged;

        public static IRewardedAdProvider Provider { get; private set; }
            = SimulatedRewardedAdProvider.Instance;

        public static bool IsSimulated => Provider is SimulatedRewardedAdProvider;

        /// <summary>Registra o provedor real (chamado uma vez no Start do jogo).</summary>
        public static void RegisterProvider(IRewardedAdProvider provider)
        {
            Provider = provider ?? SimulatedRewardedAdProvider.Instance;
            ProviderChanged?.Invoke(Provider);
        }

        /// <summary>Volta a usar o anuncio simulado (util para testar).</summary>
        public static void UseSimulatedProvider()
            => RegisterProvider(SimulatedRewardedAdProvider.Instance);

        /// <summary>Pede um anuncio recompensado e devolve o resultado final.</summary>
        public static void ShowRewarded(string placementId, Action<RewardedAdResult> onComplete)
        {
            IRewardedAdProvider provider = Provider ?? SimulatedRewardedAdProvider.Instance;

            if (!provider.IsReady)
            {
                Debug.LogWarning($"[Ads] Provedor '{provider.DisplayName}' nao esta pronto.");
                onComplete?.Invoke(RewardedAdResult.Unavailable);
                return;
            }

            try
            {
                provider.Show(placementId, onComplete);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onComplete?.Invoke(RewardedAdResult.Failed);
            }
        }
    }

    /// <summary>
    /// Provedor de teste: mostra apenas um log e espera alguns segundos.
    /// Em builds so funciona se allowInBuilds estiver marcado.
    /// </summary>
    public sealed class SimulatedRewardedAdProvider : IRewardedAdProvider
    {
        public static readonly SimulatedRewardedAdProvider Instance = new();

        [Tooltip("Segundos de duracao do anuncio simulado.")]
        public float durationSeconds = 2.5f;

        [Tooltip("Permite o simulado em builds (normalmente deixe desligado).")]
        public bool allowInBuilds;

        [Tooltip("Altere para Skipped/Failed para testar os caminhos sem recompensa.")]
        public RewardedAdResult result = RewardedAdResult.Watched;

        public string DisplayName => "Simulado (Editor)";
        public bool IsReady => Application.isEditor || allowInBuilds;

        public void Show(string placementId, Action<RewardedAdResult> onComplete)
        {
            Debug.Log($"[Ads] Anuncio simulado '{placementId}' ({durationSeconds:0.0}s).");
            RewardedAdRuntime.Ensure().RunSimulation(durationSeconds, onComplete);
        }
    }

    /// <summary>Host das coroutines dos provedores que nao sao MonoBehaviours.</summary>
    public sealed class RewardedAdRuntime : MonoBehaviour
    {
        public static RewardedAdRuntime Instance { get; private set; }

        public static RewardedAdRuntime Ensure()
        {
            if (Instance != null)
                return Instance;

            GameObject go = new GameObject("[RewardedAds]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RewardedAdRuntime>();
            return Instance;
        }

        public void RunSimulation(float seconds, Action<RewardedAdResult> onComplete)
        {
            StartCoroutine(SimulationRoutine(
                Mathf.Max(0.05f, seconds), onComplete));
        }

        /// <summary>
        /// Espera ate condition virar true ou o timeout terminar. Usa tempo real
        /// para funcionar mesmo com o jogo pausado durante um anuncio.
        /// </summary>
        public void RunUntil(Func<bool> condition, float timeoutSeconds,
            Action<bool> onComplete)
        {
            StartCoroutine(UntilRoutine(condition,
                Mathf.Max(0f, timeoutSeconds), onComplete));
        }

        private IEnumerator UntilRoutine(Func<bool> condition,
            float timeoutSeconds, Action<bool> onComplete)
        {
            const float step = 0.25f;
            float elapsed = 0f;

            while (condition != null && !condition() && elapsed < timeoutSeconds)
            {
                elapsed += step;
                yield return new WaitForSecondsRealtime(step);
            }

            onComplete?.Invoke(condition != null && condition());
        }

        private IEnumerator SimulationRoutine(
            float seconds, Action<RewardedAdResult> onComplete)
        {
            yield return new WaitForSecondsRealtime(seconds);
            RewardedAdResult final = SimulatedRewardedAdProvider.Instance.result;
            Debug.Log($"[Ads] Anuncio simulado finalizado: {final}.");
            onComplete?.Invoke(final);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
