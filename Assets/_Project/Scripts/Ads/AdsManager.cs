using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;
using Clickergochi.Ads;

/// <summary>
/// Gerenciador central de anúncios (Unity LevelPlay 9.x).
/// Coloque em um GameObject vazio chamado "AdsManager" na PRIMEIRA cena do jogo.
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("Android (troque pelos valores do SEU painel)")]
    [SerializeField] string androidAppKey = "1a2b3c4d5";
    [SerializeField] string androidBannerId = "ab12cd34ef56gh78";
    [SerializeField] string androidInterstitialId = "ij90kl12mn34op56";
    [SerializeField] string androidRewardedId = "qr78st90uv12wx34";

    [Header("iOS (preencher se criar o app iOS no painel)")]
    [SerializeField] string iosAppKey = "";
    [SerializeField] string iosBannerId = "";
    [SerializeField] string iosInterstitialId = "";
    [SerializeField] string iosRewardedId = "";

    [Header("Testes")]
    [SerializeField] bool abrirTestSuite = false;
    [SerializeField] bool validarIntegracao = true;

    [Header("Banner")]
    [SerializeField] bool mostrarBannerAoCarregar = false;

    public bool Inicializado { get; private set; }
    public bool RecompensadoPronto => recompensado != null && recompensado.IsAdReady();
    public bool IntersticialPronto => intersticial != null && intersticial.IsAdReady();

    /// <summary>Avisa que o SDK confirmou a exibição do rewarded (diagnóstico).</summary>
    public event Action RecompensadoExibido;

    /// <summary>
    /// Eventos usados pelo adaptador do RewardedAdBridge. O close e o display
    /// failed são despachados DEPOIS de despausar o jogo.
    /// </summary>
    public event Action RecompensadoFechado;
    public event Action RecompensadoFalhouExibicao;

    LevelPlayBannerAd banner;
    LevelPlayInterstitialAd intersticial;
    LevelPlayRewardedAd recompensado;
    Action aoFecharIntersticial;
    Action<int> aoRecompensar;
    bool recompensadoEmExibicao;

    string AppKey => Plataforma(androidAppKey, iosAppKey);
    string BannerId => Plataforma(androidBannerId, iosBannerId);
    string InterstitialId => Plataforma(androidInterstitialId, iosInterstitialId);
    string RewardedId => Plataforma(androidRewardedId, iosRewardedId);

    static string Plataforma(string android, string ios)
    {
#if UNITY_IOS
        return ios;
#else
        return android;
#endif
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // O AD_Btt continua usando RewardedAdBridge; este é o provedor real em build.
        RewardedAdBridge.RegisterProvider(new LevelPlayRewardedProvider());
    }

    void Start()
    {
#if UNITY_EDITOR
        Debug.Log("[Ads] Anúncios reais não aparecem no Editor. Teste em um celular Android.");
#endif

        // Privacidade: sempre ANTES do Init (GDPR, LGPD, COPPA).
        // LevelPlayPrivacySettings.SetGDPRConsent(true);
        if (abrirTestSuite)
            LevelPlay.SetMetaData("is_test_suite", "enable");

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        Debug.Log($"[Ads] Inicializando LevelPlay appKey='{AppKey}' rewardedId='{RewardedId}'...");
        LevelPlay.Init(AppKey);
    }

    void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[Ads] LevelPlay inicializado com sucesso");
        Inicializado = true;

        if (validarIntegracao)
            LevelPlay.ValidateIntegration();
        if (abrirTestSuite)
            LevelPlay.LaunchTestSuite();

        CriarBanner();
        CriarIntersticial();
        CriarRecompensado();
    }

    void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogWarning($"[Ads] Falha ao inicializar LevelPlay: {error}. Nova tentativa em 30 s");
        StartCoroutine(Depois(30f, () => LevelPlay.Init(AppKey)));
    }

    // ---------- Banner

    void CriarBanner()
    {
        var config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(LevelPlayAdSize.BANNER)
            // O SlimeConter fica no topo do HUD; o banner nativo vai colado nessa area.
            .SetPosition(LevelPlayBannerPosition.TopCenter)
            .SetDisplayOnLoad(mostrarBannerAoCarregar)
            .SetRespectSafeArea(true)
            .Build();

        banner = new LevelPlayBannerAd(BannerId, config);
        banner.OnAdLoaded += info => Debug.Log("[Ads] Banner carregado");
        banner.OnAdLoadFailed += err => Debug.LogWarning($"[Ads] Banner falhou ao carregar: {err}");
        banner.LoadAd();
    }

    public void MostrarBanner() => banner?.ShowAd();
    public void EsconderBanner() => banner?.HideAd();

    // ---------- Intersticial

    void CriarIntersticial()
    {
        intersticial = new LevelPlayInterstitialAd(InterstitialId);
        intersticial.OnAdLoaded += info => Debug.Log("[Ads] Intersticial pronto");
        intersticial.OnAdLoadFailed += err =>
        {
            Debug.LogWarning($"[Ads] Intersticial falhou ao carregar: {err}. Nova tentativa em 15 s");
            StartCoroutine(Depois(15f, CarregarIntersticial));
        };
        intersticial.OnAdDisplayFailed += (info, err) =>
        {
            Debug.LogWarning($"[Ads] Intersticial falhou ao exibir: {err}");
            FecharIntersticial();
        };
        intersticial.OnAdClosed += info => FecharIntersticial();
        CarregarIntersticial();
    }

    void CarregarIntersticial() => intersticial?.LoadAd();

    public void MostrarIntersticial(Action depois = null)
    {
        if (IntersticialPronto)
        {
            aoFecharIntersticial = depois;
            PausarJogo(true);
            intersticial.ShowAd();
        }
        else
        {
            depois?.Invoke();
        }
    }

    void FecharIntersticial()
    {
        PausarJogo(false);
        var acao = aoFecharIntersticial;
        aoFecharIntersticial = null;
        acao?.Invoke();
        CarregarIntersticial();
    }

    // ---------- Recompensado

    void CriarRecompensado()
    {
        recompensado = new LevelPlayRewardedAd(RewardedId);
        recompensado.OnAdLoaded += OnRecompensadoLoaded;
        recompensado.OnAdLoadFailed += OnRecompensadoLoadFailed;
        recompensado.OnAdDisplayed += OnRecompensadoDisplayed;
        recompensado.OnAdDisplayFailed += OnRecompensadoDisplayFailed;
        recompensado.OnAdRewarded += OnRecompensadoRewarded;
        recompensado.OnAdClosed += OnRecompensadoClosed;
        CarregarRecompensado();
    }

    void OnRecompensadoLoaded(LevelPlayAdInfo info)
    {
        Debug.Log("[Ads] Recompensado pronto");
    }

    void CarregarRecompensado() => recompensado?.LoadAd();

    void OnRecompensadoLoadFailed(LevelPlayAdError err)
    {
        Debug.LogWarning($"[Ads] Recompensado falhou ao carregar: {err}. Nova tentativa em 15 s");
        StartCoroutine(Depois(15f, CarregarRecompensado));
    }

    void OnRecompensadoDisplayed(LevelPlayAdInfo info)
    {
        Debug.Log("[Ads] Recompensado exibido");
        RecompensadoExibido?.Invoke();
    }

    void OnRecompensadoDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError err)
    {
        Debug.LogWarning($"[Ads] Recompensado falhou ao exibir: {err}");
        FecharRecompensado();
        RecompensadoFalhouExibicao?.Invoke();
    }

    void OnRecompensadoRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        Debug.Log($"[Ads] Prêmio recebido: {reward.Amount} {reward.Name}");
        var acao = aoRecompensar;
        aoRecompensar = null;
        acao?.Invoke(reward.Amount);
    }

    void OnRecompensadoClosed(LevelPlayAdInfo info)
    {
        Debug.Log("[Ads] Recompensado fechado");
        FecharRecompensado();
        RecompensadoFechado?.Invoke();
    }

    /// <summary>
    /// Retorna true somente quando o anúncio foi efetivamente enviado ao SDK.
    /// O adaptador usa o retorno para não deixar o fluxo preso sem callback.
    ///
    /// Usa SEMPRE o rewarded padrão do ad unit: o placement customizado do fluxo
    /// é apenas informativo (Editor/testes) e não é enviado ao LevelPlay até o
    /// placement ser confirmado no painel.
    /// </summary>
    public bool MostrarRecompensado(Action<int> premio)
    {
        if (!RecompensadoPronto)
        {
            Debug.LogWarning("[Ads] MostrarRecompensado: anúncio não está pronto.");
            return false;
        }

        aoRecompensar = premio;
        recompensadoEmExibicao = true;
        PausarJogo(true);

        try
        {
            recompensado.ShowAd();
            Debug.Log("[Ads] MostrarRecompensado: ShowAd enviado sem placement " +
                      $"(ad unit '{RewardedId}').");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            aoRecompensar = null;
            recompensadoEmExibicao = false;
            PausarJogo(false);
            return false;
        }
    }

    /// <summary>
    /// Usado pelo adaptador se o SDK não emitir close/display failed a tempo.
    /// Nunca concede recompensa.
    /// </summary>
    public void CancelarRecompensado()
    {
        Debug.LogWarning("[Ads] Recompensado cancelado por timeout; sem recompensa.");
        aoRecompensar = null;
        FecharRecompensado();
    }

    void FecharRecompensado()
    {
        if (!recompensadoEmExibicao)
            return;

        recompensadoEmExibicao = false;
        aoRecompensar = null;
        PausarJogo(false);
        CarregarRecompensado();
    }

    // ---------- Utilitários

    static void PausarJogo(bool pausar)
    {
        Time.timeScale = pausar ? 0f : 1f;
        AudioListener.pause = pausar;
    }

    static IEnumerator Depois(float segundos, Action acao)
    {
        yield return new WaitForSecondsRealtime(segundos);
        acao?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        StopAllCoroutines();
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;

        if (recompensado != null)
        {
            recompensado.OnAdLoaded -= OnRecompensadoLoaded;
            recompensado.OnAdLoadFailed -= OnRecompensadoLoadFailed;
            recompensado.OnAdDisplayed -= OnRecompensadoDisplayed;
            recompensado.OnAdDisplayFailed -= OnRecompensadoDisplayFailed;
            recompensado.OnAdRewarded -= OnRecompensadoRewarded;
            recompensado.OnAdClosed -= OnRecompensadoClosed;
            recompensado.DestroyAd();
            recompensado = null;
        }

        banner?.DestroyAd();
        intersticial?.DestroyAd();
        PausarJogo(false);
    }
}
