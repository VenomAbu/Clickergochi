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
        private const float WaitForAdSeconds = 20f;

        private int _requestId;
        private Action<RewardedAdResult> _onComplete;
        private AdsManager _manager;
        private bool _subscribed;
        private bool _rewarded;

        public string DisplayName => "LevelPlay (AdsManager)";

        /// <summary>So depende do AdsManager existir; a prontidao e checada em Show.</summary>
        public bool IsReady => AdsManager.Instance != null;

        public void Show(string placementId, Action<RewardedAdResult> onComplete)
        {
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

            Cleanup();
            int id = ++_requestId;
            _onComplete = onComplete;
            _rewarded = false;

            AdsManager manager = AdsManager.Instance;
            if (manager == null)
            {
                Finish(id, RewardedAdResult.Unavailable);
                return;
            }

            // Primeira execucao pode acontecer antes do Init/load terminar.
            // Espera o anuncio ficar pronto em vez de cancelar a recompensa.
            if (!manager.Inicializado || !manager.RecompensadoPronto)
            {
                RewardedAdRuntime.Ensure().RunUntil(
                    () => AdsManager.Instance == manager &&
                          manager.Inicializado &&
                          manager.RecompensadoPronto,
                    WaitForAdSeconds,
                    ready =>
                    {
                        if (id != _requestId)
                            return;
                        if (!ready || AdsManager.Instance != manager)
                        {
                            Finish(id, RewardedAdResult.Unavailable);
                            return;
                        }

                        ShowNow(id, manager, placementId);
                    });
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
            bool started = manager.MostrarRecompensado(
                amount => { _rewarded = amount > 0; },
                string.IsNullOrEmpty(placementId) ? null : placementId);

            if (!started)
                Finish(id, RewardedAdResult.Unavailable);
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
        }
    }
}
