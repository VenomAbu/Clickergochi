using System;
using System.Collections;
using Clickergochi.Ads;
using UnityEngine;

/// <summary>
/// Fluxo do AD_Btt da MainScene:
/// 1) o AdConfirmationButton abre o PopUpConfirmTV com a recompensa do dia;
/// 2) ao confirmar, guarda a sala horizontal atual (Room/Room (1)/Room (2));
/// 3) toca o anuncio recompensado via RewardedAdBridge;
/// 4) so com Watched credita a recompensa do dia:
///    1º Golden Pack, 2º Pacote Comum, demais 1000 Slimes (sem limite);
/// 5) ao sair do sticker/popup, volta para a sala guardada.
///
/// O contador diario vive no SaveData (dia local do jogador) e so avanca
/// depois que o SDK confirma que o anuncio foi assistido ate o fim.
/// </summary>
[DisallowMultipleComponent]
public sealed class AdRewardGoldenPackFlow : MonoBehaviour
{
    private enum DailyReward
    {
        GoldenPack,
        ComumPack,
        Slimes
    }

    private const int SlimeRewardAmount = 1000;

    [Header("Referencias")]
    [SerializeField] private AdConfirmationButton adButton;
    [SerializeField] private HorizontalNavegationController horizontalNavigation;
    [SerializeField] private VerticalNavegationController verticalNavigation;
    [SerializeField] private PackShopUGUIController packShop;
    [Tooltip("PopUpConfirm2 usado para mostrar o recebimento dos 1000 Slimes.")]
    [SerializeField] private PopUpConfirmController rewardPopup;

    [Header("Recompensa")]
    [Tooltip("Rotulo do pedido. Em build o LevelPlay usa o rewarded padrao do ad unit; este valor nao e enviado ao SDK.")]
    [SerializeField] private string placementId = "rewarded_golden_pack";
    [Tooltip("Somente Watched libera a recompensa; Skipped/Failed nao dao nada.")]
    [SerializeField] private bool requireWatched = true;

    private GameObject roomToReturn;
    private bool waitingAd;
    private bool adResultHandled;
    private DailyReward? pendingReward;
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
        {
            adButton.AddConfirmedListener(OnAdConfirmed);
            adButton.BeforeConfirmation += RefreshConfirmationText;
        }
        else
        {
            Debug.LogWarning("[Ads] AdRewardGoldenPackFlow sem AdConfirmationButton.");
        }
    }

    private void OnDestroy()
    {
        if (adButton != null)
        {
            adButton.RemoveConfirmedListener(OnAdConfirmed);
            adButton.BeforeConfirmation -= RefreshConfirmationText;
        }

        if (openPackRoutine != null)
        {
            StopCoroutine(openPackRoutine);
            openPackRoutine = null;
        }
    }

    /// <summary>
    /// Atualiza o texto do PopUpConfirmTV no exato clique da TV, para o jogador
    /// sempre ver o que vai receber naquele anuncio.
    /// </summary>
    private void RefreshConfirmationText()
    {
        if (adButton == null)
            return;

        DailyReward reward = CurrentDailyReward();
        pendingReward = reward;
        adButton.SetConfirmationTexts("Assistir anúncio?", MessageFor(reward), "Assistir");
    }

    private static string MessageFor(DailyReward reward)
    {
        switch (reward)
        {
            case DailyReward.GoldenPack:
                return "Assista ao anúncio para abrir um Pacote Golden grátis!";
            case DailyReward.ComumPack:
                return "Assista ao anúncio para abrir um Pacote Comum grátis!";
            default:
                return $"Assista ao anúncio para receber {SlimeRewardAmount} Slimes!";
        }
    }

    private void OnAdConfirmed()
    {
        if (waitingAd)
            return;

        DailyReward reward = pendingReward ?? CurrentDailyReward();
        pendingReward = reward;
        adResultHandled = false;

        // Captura a sala no exato momento em que o jogador pediu o anuncio.
        roomToReturn = horizontalNavigation != null
            ? horizontalNavigation.GetCurrentPanel()
            : null;

        waitingAd = true;
        GameAudioManager.Instance?.SetMusicSuppressed(true);
        Debug.Log($"[Ads] Recompensa pedida na sala " +
                  $"{(roomToReturn != null ? roomToReturn.name : "desconhecida")} " +
                  $"({reward}).");

        RewardedAdBridge.ShowRewarded(placementId, result => OnAdFinished(result, reward));
    }

    private void OnAdFinished(RewardedAdResult result, DailyReward reward)
    {
        if (adResultHandled)
            return;
        adResultHandled = true;
        waitingAd = false;
        // O pedido foi respondido; nao reuse o rotulo do dia num proximo clique
        // que tenha vindo sem passar pelo BeforeConfirmation.
        pendingReward = null;

        if (requireWatched && result != RewardedAdResult.Watched)
        {
            GameAudioManager.Instance?.SetMusicSuppressed(false);
            Debug.Log($"[Ads] Anuncio terminou sem recompensa ({result}).");
            return;
        }

        CommitWatched(reward);

        switch (reward)
        {
            case DailyReward.GoldenPack:
                OpenPackAfterAd(true);
                break;
            case DailyReward.ComumPack:
                OpenPackAfterAd(false);
                break;
            default:
                GrantSlimes();
                break;
        }
    }

    // =========================================
    // CONTADOR DIARIO
    // =========================================

    private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");

    private static DailyReward CurrentDailyReward()
    {
        SaveData data = SaveManager.Instance?.GetSaveData();
        if (data == null)
            return DailyReward.GoldenPack;

        if (!string.Equals(data.adRewardDay, Today(), StringComparison.Ordinal))
            return DailyReward.GoldenPack;
        if (data.adRewardWatchedCount <= 0)
            return DailyReward.GoldenPack;
        if (data.adRewardWatchedCount == 1)
            return DailyReward.ComumPack;
        return DailyReward.Slimes;
    }

    private static void CommitWatched(DailyReward reward)
    {
        SaveManager manager = SaveManager.Instance;
        SaveData data = manager?.GetSaveData();
        if (data == null)
            return;

        string today = Today();
        if (!string.Equals(data.adRewardDay, today, StringComparison.Ordinal))
        {
            data.adRewardDay = today;
            data.adRewardWatchedCount = 0;
        }

        data.adRewardWatchedCount++;
        // Grava na hora: fechar o app logo apos o anuncio nao pode permitir
        // assistir de novo e ganhar o pacote do dia seguinte.
        try
        {
            manager.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Ads] Falha ao gravar contador diario: {e.Message}");
        }
        Debug.Log($"[Ads] Recompensa diaria '{reward}' creditada " +
                  $"({data.adRewardWatchedCount} no dia {today}).");
    }

    // =========================================
    // ENTREGA DAS RECOMPENSAS
    // =========================================

    private void GrantSlimes()
    {
        GameAudioManager.Instance?.SetMusicSuppressed(false);

        SlimeManager slime = SlimeManager.Instance;
        if (slime != null)
        {
            slime.AddGoo(SlimeRewardAmount);
        }
        else
        {
            SaveManager manager = SaveManager.Instance;
            SaveData data = manager?.GetSaveData();
            if (data == null)
            {
                Debug.LogWarning("[Ads] SaveData indisponivel; 1000 Slimes nao creditados.");
                return;
            }
            data.goo += SlimeRewardAmount;
            manager.RequestSave();
        }

        Debug.Log($"[Ads] +{SlimeRewardAmount} Slimes creditados pelo anuncio.");
        ShowSlimeRewardPopup();
    }

    private void ShowSlimeRewardPopup()
    {
        PopUpConfirmController popup = rewardPopup;
        if (popup == null)
            popup = PopUpConfirmController.FindPopUpConfirm2();
        if (popup == null)
            popup = PopUpConfirmController.Instance;

        if (popup == null)
        {
            Debug.LogWarning("[Ads] PopUpConfirm2 nao encontrado; recompensa creditada sem aviso.");
            return;
        }

        popup.Show(
            "Recompensa recebida!",
            $"Você ganhou {SlimeRewardAmount} Slimes pelo anúncio.",
            "OK",
            null);
    }

    private void OpenPackAfterAd(bool golden)
    {
        if (verticalNavigation == null || packShop == null)
        {
            GameAudioManager.Instance?.SetMusicSuppressed(false);
            Debug.LogWarning("[Ads] Navegacao ou PackUI nao encontrado; recompensa cancelada.");
            return;
        }

        openPackRoutine = StartCoroutine(OpenPackAfterAdRoutine(golden));
    }

    private IEnumerator OpenPackAfterAdRoutine(bool golden)
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
        if (golden)
            packShop.ShowGoldenPackFromAd(ReturnToRoom);
        else
            packShop.ShowComumPackFromAd(ReturnToRoom);

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
