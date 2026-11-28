using System;
using UnityEngine;

namespace Clickergochi.Ads
{
    /// <summary>
    /// Liga o AdsManager (Unity LevelPlay 9.x) ao RewardedAdBridge usado pelo
    /// fluxo do AD_Btt. O Golden Pack so e liberado quando o SDK confirma o
    /// reward configurado no painel ("Recompensa", amount 1).
    ///
    /// O adaptador nao chama o callback no evento de reward: o LevelPlay costuma emitir
    /// reward e, em seguida, close. O resultado final e devolvido no close para
    /// que o AdsManager ja tenha despausado o jogo antes de abrir o pacote.
    /// </summary>
    public sealed class LevelPlayRewardedProvider : IRewardedAdProvider
    {
        private const float WatchTimeoutSeconds = 60f;

        private int _requestId;
        private Action<RewardedAdResult> _onComplete;
        private AdsManager _manager;
        private bool _subscribed;
        private bool _rewarded;

        public string DisplayName => "LevelPlay (AdsManager)";

        /// <summary>
        /// So esta pronto quando o SDK inicializou E ha um anuncio carregado.
        /// Assim o RewardedAdBridge consegue abrir a tela de espera antes do Show.
        /// </summary>
        public bool IsReady => AdsManager.Instance != null &&
                               AdsManager.Instance.Inicializado &&
                               AdsManager.Instance.RecompensadoPronto;

        public void Show(string placementId, Action<RewardedAdResult> onComplete)
        {
            Cleanup();

#if UNITY_EDITOR
            // O LevelPlay nao exibe anuncio no Editor e nao dispara close.
            // Mostra uma propaganda de teste que so recompensa ao fim do tempo;
            // em build (Android/iOS) segue o caminho real do AdsManager abaixo.
            if (Application.isEditor)
            {
                Debug.Log("[Ads] Editor: propaganda de teste; " +
                          "recompensa somente quando assistida ate o fim.");
                EditorRewardedAdOverlay.Show(placementId, onComplete);
                return;
            }
#endif

            // Sem anuncio carregado, o Bridge mostra a tela de espera com
            // contador. Quando ficar pronto, ele chama este Show de novo.
            if (!IsReady)
            {
                Debug.Log("[Ads] Rewarded ainda nao carregado; abrindo tela de espera.");
                RewardedAdWaitOverlay.Show(placementId, this, onComplete);
                return;
            }

            int id = ++_requestId;
            _onComplete = onComplete;
            _rewarded = false;

            AdsManager manager = AdsManager.Instance;
            if (manager == null)
            {
                Finish(id, RewardedAdResult.Unavailable);
                return;
            }

            ShowNow(id, manager, placementId);
        }

        private void ShowNow(int id, AdsManager manager, string placementId)
        {
            if (id != _requestId || AdsManager.Instance != manager)
                return;

            _manager = manager;
            manager.RecompensadoFechado += OnAdClosed;
            manager.RecompensadoFalhouExibicao += OnAdDisplayFailed;
            _subscribed = true;
            _rewarded = false;

            // OnAdRewarded do LevelPlay so dispara para o item configurado.
            // O placement do fluxo nao e enviado: ate confirmar no painel, o
            // ad unit padrao e o unico caminho seguro em build.
            Debug.Log($"[Ads] Exibindo rewarded padrao do ad unit " +
                      $"(placement '{placementId}' ignorado em build).");
            bool started = manager.MostrarRecompensado(
                amount => { if (amount > 0) _rewarded = true; });

            if (!started)
            {
                Finish(id, RewardedAdResult.Unavailable);
                return;
            }

            // Se o SDK aceitar ShowAd mas nao emitir close/display failed, o
            // fluxo nao pode ficar preso para sempre. Nunca recompensa nesse caso.
            RewardedAdRuntime.Ensure().Schedule(WatchTimeoutSeconds, () =>
            {
                if (id != _requestId)
                    return;

                Debug.LogWarning("[Ads] Timeout apos ShowAd; sem confirmacao de fechamento.");
                if (manager != null)
                    manager.CancelarRecompensado();
                Finish(id, RewardedAdResult.Failed);
            });
        }

        private void OnAdClosed()
        {
            Finish(_requestId,
                _rewarded ? RewardedAdResult.Watched : RewardedAdResult.Skipped);
        }

        private void OnAdDisplayFailed()
        {
            Finish(_requestId, RewardedAdResult.Failed);
        }

        private void Finish(int id, RewardedAdResult result)
        {
            if (id != _requestId)
                return;

            Action<RewardedAdResult> callback = _onComplete;
            Cleanup();
            callback?.Invoke(result);
        }

        private void Cleanup()
        {
            if (_subscribed && _manager != null)
            {
                _manager.RecompensadoFechado -= OnAdClosed;
                _manager.RecompensadoFalhouExibicao -= OnAdDisplayFailed;
            }

            _subscribed = false;
            _manager = null;
            _onComplete = null;
            _rewarded = false;

            // Invalida timeouts e callbacks das requisicoes anteriores.
            _requestId++;
        }
    }
}
