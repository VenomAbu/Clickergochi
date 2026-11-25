using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;
using Clickergochi.Ads;
/// Gerenciador central de an�ncios (Unity LevelPlay 9.x).
/// Coloque em um GameObject vazio chamado "AdsManager" na PRIMEIRA cena do jogo.
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
    [SerializeField] bool abrirTestSuite = false; // desligue antes de publicar!
    [SerializeField] bool validarIntegracao = true;
    [Header("Banner")]
    [SerializeField] bool mostrarBannerAoCarregar = false;
    public bool Inicializado { get; private set; }
    public bool RecompensadoPronto => recompensado != null && recompensado.IsAdReady();
    public bool IntersticialPronto => intersticial != null && intersticial.IsAdReady();

    // Eventos usados pelo adaptador do RewardedAdBridge. O close e o display
    // failed sao despachados DEPOIS de despausar o jogo.
    public event Action RecompensadoFechado;
    public event Action RecompensadoFalhouExibicao;
    LevelPlayBannerAd banner;
    LevelPlayInterstitialAd intersticial;
    LevelPlayRewardedAd recompensado;
    Action aoFecharIntersticial;
    Action<int> aoRecompensar;
    string AppKey => Plataforma(androidAppKey, iosAppKey);
    string BannerId => Plataforma(androidBannerId, iosBannerId);
    string InterstitialId => Plataforma(androidInterstitialId,
    iosInterstitialId);
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
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // O AD_Btt continua usando RewardedAdBridge; este e o provedor real.
        RewardedAdBridge.RegisterProvider(new LevelPlayRewardedProvider());
    }
    void Start()
    {
#if UNITY_EDITOR

        Debug.Log("[Ads] An�ncios n�o aparecem no Editor. Teste em um celular Android.");
    #endif

        // Privacidade: sempre ANTES do Init (GDPR, LGPD, COPPA)
        // LevelPlayPrivacySettings.SetGDPRConsent(true);
if (abrirTestSuite)
            LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        Debug.Log("[Ads] Inicializando LevelPlay...");
        LevelPlay.Init(AppKey);
    }
    void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[Ads] LevelPlay inicializado com sucesso");
        Inicializado = true;
        if (validarIntegracao) LevelPlay.ValidateIntegration();
        if (abrirTestSuite) LevelPlay.LaunchTestSuite();
        CriarBanner();
        CriarIntersticial();
        CriarRecompensado();
    }
    void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogWarning($"[Ads] Falha ao inicializar: {error}. Nova tentativa em 30 s");
        StartCoroutine(Depois(30f, () => LevelPlay.Init(AppKey)));
    }
    // ---------- Banner
    void CriarBanner()
    {
        var config = new LevelPlayBannerAd.Config.Builder()
        .SetSize(LevelPlayAdSize.BANNER)
        .SetPosition(LevelPlayBannerPosition.BottomCenter)
        .SetDisplayOnLoad(mostrarBannerAoCarregar)
        .SetRespectSafeArea(true)
        .Build();
        banner = new LevelPlayBannerAd(BannerId, config);
        banner.OnAdLoaded += info => Debug.Log("[Ads] Banner carregado");
        banner.OnAdLoadFailed += err => Debug.LogWarning($"[Ads] Banner falhou: { err} ");
    
banner.LoadAd();
    }
    public void MostrarBanner() => banner?.ShowAd();
    public void EsconderBanner() => banner?.HideAd();
    // ---------- Intersticial
    void CriarIntersticial()
    {
        intersticial = new LevelPlayInterstitialAd(InterstitialId);
        intersticial.OnAdLoaded += info => Debug.Log("[Ads] Intersticial pronto");
        intersticial.OnAdLoadFailed += err => StartCoroutine(Depois(15f, CarregarIntersticial));
        intersticial.OnAdDisplayFailed += (info, err) => FecharIntersticial();
        intersticial.OnAdClosed += info => FecharIntersticial();
        CarregarIntersticial();
    }
    void CarregarIntersticial() => intersticial.LoadAd();
    public void MostrarIntersticial(Action depois = null)
    {
        if (IntersticialPronto)
        {
            aoFecharIntersticial = depois;
            PausarJogo(true);
            intersticial.ShowAd();
        }
        else depois?.Invoke();
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
        recompensado.OnAdLoaded += info => Debug.Log("[Ads] Recompensado pronto");
        recompensado.OnAdLoadFailed += err => StartCoroutine(Depois(15f, CarregarRecompensado));
        recompensado.OnAdDisplayFailed += (info, err) =>
        {
            FecharRecompensado();
            RecompensadoFalhouExibicao?.Invoke();
        };
        recompensado.OnAdClosed += info =>
        {
            FecharRecompensado();
            RecompensadoFechado?.Invoke();
        };
        recompensado.OnAdRewarded += (info, reward) =>{Debug.Log($"[Ads] Pr�mio recebido: {reward.Amount} { reward.Name}");
        
        var acao = aoRecompensar;
            aoRecompensar = null;
            acao?.Invoke(reward.Amount);
        };
        CarregarRecompensado();
    }
    void CarregarRecompensado() => recompensado.LoadAd();
    /// <summary>
    /// Retorna true somente quando o anuncio foi efetivamente exibido.
    /// O adaptador usa o retorno para nao deixar o fluxo preso sem callback.
    /// </summary>
    public bool MostrarRecompensado(Action<int> premio, string placement = null)
    {
        if (!RecompensadoPronto) return false;
        if (!string.IsNullOrEmpty(placement) &&
            LevelPlayRewardedAd.IsPlacementCapped(placement)) return false;

        aoRecompensar = premio;
        PausarJogo(true);
        try
        {
            if (string.IsNullOrEmpty(placement)) recompensado.ShowAd();
            else recompensado.ShowAd(placement);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            aoRecompensar = null;
            PausarJogo(false);
            return false;
        }
    }
    void FecharRecompensado()
    {
        PausarJogo(false);
        CarregarRecompensado();
    }
    // ---------- Utilit�rios
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
        if (Instance != this) return;
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;
        banner?.DestroyAd();
        intersticial?.DestroyAd();
    }
}