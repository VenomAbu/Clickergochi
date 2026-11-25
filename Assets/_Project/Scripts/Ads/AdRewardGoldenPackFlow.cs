using System;
using System.Collections;
using Clickergochi.Ads;
using UnityEngine;

/// <summary>
/// Fluxo do AD_Btt da MainScene:
/// 1) o AdConfirmationButton ja abre o PopUpConfirmTV;
/// 2) ao confirmar, guarda a sala horizontal atual (Room/Room (1)/Room (2));
/// 3) toca o anuncio recompensado via RewardedAdBridge;
/// 4) se assistiu ate o fim, abre o PackUI e mostra um Golden Pack esperando
///    o toque do jogador (a abertura so começa quando ele tocar na tela);
/// 5) ao sair do sticker, volta para a sala guardada em vez de ficar no PackUI.
///
/// Sem SDK instalado o bridge usa o anuncio simulado no Editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class AdRewardGoldenPackFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private AdConfirmationButton adButton;
    [SerializeField] private HorizontalNavegationController horizontalNavigation;
    [SerializeField] private VerticalNavegationController verticalNavigation;
    [SerializeField] private PackShopUGUIController packShop;

    [Header("Recompensa")]
    [Tooltip("Placement id enviado ao provedor de anuncios.")]
    [SerializeField] private string placementId = "rewarded_golden_pack";
    [Tooltip("Somente Watched libera o pacote; Skipped/Failed nao dão recompensa.")]
    [SerializeField] private bool requireWatched = true;

    private GameObject roomToReturn;
    private bool waitingAd;
    private Coroutine openPackRoutine;

    private void Awake()
    {
        if (adButton == null)
            adButton = GetComponent<AdConfirmationButton>();
        if (horizontalNavigation == null)
            horizontalNavigation = FindFirstObjectByType<HorizontalNavegationController>(FindObjectsInactive.Include);
        if (verticalNavigation == null)
            verticalNavigation = FindFirstObjectByType<VerticalNavegationController>(FindObjectsInactive.Include);
        if (packShop == null)
            packShop = FindFirstObjectByType<PackShopUGUIController>(FindObjectsInactive.Include);

        if (adButton != null)
            adButton.AddConfirmedListener(OnAdConfirmed);
        else
            Debug.LogWarning("[Ads] AdRewardGoldenPackFlow sem AdConfirmationButton.");
    }

    private void OnDestroy()
    {
        if (adButton != null)
            adButton.RemoveConfirmedListener(OnAdConfirmed);

        if (openPackRoutine != null)
        {
            StopCoroutine(openPackRoutine);
            openPackRoutine = null;
        }
    }

    private void OnAdConfirmed()
    {
        if (waitingAd)
            return;

        // Captura a sala no exato momento em que o jogador pediu o anuncio.
        roomToReturn = horizontalNavigation != null
            ? horizontalNavigation.GetCurrentPanel()
            : null;

        waitingAd = true;
        GameAudioManager.Instance?.SetMusicSuppressed(true);
        Debug.Log($"[Ads] Recompensa pedida na sala " +
                  $"{(roomToReturn != null ? roomToReturn.name : "desconhecida")}.");

        RewardedAdBridge.ShowRewarded(placementId, OnAdFinished);
    }

    private void OnAdFinished(RewardedAdResult result)
    {
        waitingAd = false;

        if (requireWatched && result != RewardedAdResult.Watched)
        {
            GameAudioManager.Instance?.SetMusicSuppressed(false);
            Debug.Log($"[Ads] Anuncio terminou sem recompensa ({result}).");
            return;
        }

        if (verticalNavigation == null || packShop == null)
        {
            GameAudioManager.Instance?.SetMusicSuppressed(false);
            Debug.LogWarning("[Ads] Navegacao ou PackUI nao encontrado; recompensa cancelada.");
            return;
        }

        openPackRoutine = StartCoroutine(OpenGoldenPackAfterAd());
    }

    private IEnumerator OpenGoldenPackAfterAd()
    {
        // O painel do PackUI pode comecar inativo. Abre pela navegacao vertical
        // (que fecha qualquer outro painel) e espera o OnEnable resetar a view.
        const float timeout = 1.5f;
        float elapsed = 0f;

        while (!packShop.gameObject.activeInHierarchy && elapsed < timeout)
        {
            verticalNavigation.OpenPackPanel();
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!packShop.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[Ads] PackUI nao abriu apos o anuncio.");
            ReturnToRoom();
            openPackRoutine = null;
            yield break;
        }

        yield return null;

        // Mostra o pacote pulsando; o EventTrigger de clique na tela e quem
        // inicia a abertura, igual ao fluxo normal de compra.
        packShop.ShowGoldenPackFromAd(ReturnToRoom);
        openPackRoutine = null;
    }

    private void ReturnToRoom()
    {
        if (roomToReturn == null)
        {
            verticalNavigation?.BackBtt();
            return;
        }

        verticalNavigation?.ReturnToRoom(roomToReturn);
        roomToReturn = null;
    }
}
