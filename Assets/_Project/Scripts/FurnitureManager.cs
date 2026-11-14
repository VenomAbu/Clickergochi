using System;
using UnityEngine;

public enum FurnitureKind
{
    Bed,
    Wardrobe,
    Dresser
}

/// <summary>
/// Progressao de Comodo e Moveis conforme o GDD:
/// - O Comodo e permanente e evolui custando Gosma (1000 * 2^(alvo-2)).
/// - A Cama desbloqueia no Comodo Lv.2, o Guarda-roupas no Lv.5 e a Comoda no Lv.10.
/// - A cada nivel do Comodo todos os moveis resetam para 0; em troca o nivel
///   maximo de cada movel desbloqueado sobe +5 (teto 100).
/// - Custo do movel: Teto(base * 1.40^(nivel-1)); bases 5 / 42 / 1185.
/// - Efeitos: Cama gera Gosma/s, Guarda-roupas da bonus % por clique e a
///   Comoda da chance de critico (critico = clique-base * 5).
/// - A Cama produz com o jogo aberto e liquida o tempo com o app fechado
///   ao voltar, via relogio AFK em UTC (teto/eficiencia no Inspector).
/// </summary>
public class FurnitureManager : MonoBehaviour
{
    private static FurnitureManager _instance;

    /// <summary>
    /// Instancia unica. Se a cena nao tiver o componente, cria um GameObject
    /// em runtime para que nenhum sistema dependa de uma referencia manual.
    /// </summary>
    public static FurnitureManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<FurnitureManager>();
            if (_instance == null)
            {
                var go = new GameObject("FurnitureManager");
                _instance = go.AddComponent<FurnitureManager>();
            }
            return _instance;
        }
    }

    [Header("Comodo")]
    [Tooltip("Nivel inicial do comodo em saves novos. Antigos iniciam em 1.")]
    [SerializeField, Min(1)] private int startComodoLevel = 1;
    [Tooltip("Nivel maximo do comodo (GDD usa tabelas ate o Lv.100).")]
    [SerializeField, Min(1)] private int maxComodoLevel = 100;
    [Tooltip("Custo do avanco para o Comodo Lv.2. Custo = base * 2^(alvo-2).")]
    [SerializeField, Min(1f)] private float comodoBaseCost = 1000f;

    [Header("Moveis")]
    [Tooltip("Nivel maximo de cada movel (GDD: teto 100).")]
    [SerializeField, Min(1)] private int furnitureLevelCap = 100;
    [Tooltip("Quanto o nivel maximo de cada movel sobe por nivel do comodo.")]
    [SerializeField, Min(1)] private int maxLevelGainPerComodo = 5;

    [Header("Efeitos")]
    [Tooltip("Multiplicador do clique critico sobre o clique-base (GDD nao define; definido 5x).")]
    [SerializeField, Min(1f)] private float critMultiplier = 5f;

    [Header("Producao da Cama (com o jogo aberto)")]
    [Tooltip("Intervalo de credito da producao da Cama.")]
    [SerializeField, Min(0.1f)] private float bedTickSeconds = 1f;
    [Tooltip("Pedir gravacao do save a cada N segundos de producao da Cama.")]
    [SerializeField, Min(1f)] private float passiveSaveIntervalSeconds = 10f;

    [Header("Producao Offline (app fechado / segundo plano)")]
    [Tooltip("Horas maximas de producao creditadas por retorno. 0 = sem teto.")]
    [SerializeField, Min(0f)] private float maxOfflineHours = 8f;
    [Tooltip("Fracao da producao offline creditada (1 = 100%, fiel ao GDD).")]
    [SerializeField, Range(0f, 1f)] private float offlineEfficiency = 1f;

    private SaveManager subscribedSaveManager;
    private float bedAccumulator;
    private float bedSecondsSinceSave;

    /// <summary>Disparado quando nivel do comodo/niveis dos moveis mudam.</summary>
    public event Action StateChanged;

    private static readonly int[] UnlockLevels = { 2, 5, 10 };
    private static readonly float[] BaseCosts = { 5f, 42f, 1185f };

    public int ComodoLevel => Mathf.Max(1, Save()?.comodoLevel ?? startComodoLevel);
    public int MaxComodoLevel => maxComodoLevel;
    public float CritMultiplier => critMultiplier;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        SubscribeToSystems();
        // Garante o default do comodo mesmo antes do primeiro acesso.
        SaveData data = Save();
        if (data != null && data.comodoLevel < 1)
        {
            data.comodoLevel = Mathf.Max(1, startComodoLevel);
            SaveManager.Instance?.RequestSave();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromSystems();
    }

    private void Start()
    {
        // Primeira carga concluida: liquida o tempo com o jogo fechado.
        ApplyOfflineProduction();
    }

    private void OnApplicationPause(bool paused)
    {
        // Ciclo de vida mobile (Android/iOS): o SO pode suspender ou matar o
        // app sem outro aviso. Ida: fixa o relogio AFK e grava o par
        // (gosma + timestamp) na hora. Volta: credita o tempo ausente.
        if (paused)
        {
            FlushPendingProduction();
            SaveManager.Instance?.Save();
        }
        else
        {
            ApplyOfflineProduction();
        }
    }

    private void SubscribeToSystems()
    {
        SaveManager saveManager = SaveManager.Instance;
        if (subscribedSaveManager != saveManager)
        {
            UnsubscribeFromSystems();
            subscribedSaveManager = saveManager;
            if (subscribedSaveManager != null)
                subscribedSaveManager.SaveDataChanged += HandleSaveDataChanged;
        }
    }

    private void UnsubscribeFromSystems()
    {
        if (subscribedSaveManager != null)
            subscribedSaveManager.SaveDataChanged -= HandleSaveDataChanged;
        subscribedSaveManager = null;
    }

    private void HandleSaveDataChanged(SaveData data)
    {
        StateChanged?.Invoke();
    }

    private SaveData Save()
    {
        SaveManager manager = SaveManager.Instance;
        return manager != null ? manager.GetSaveData() : null;
    }

    // ---- Comodo ----

    /// <summary>Custo para avancar do nivel atual (sem descontos de comida). -1 se maximo.</summary>
    public float ComodoUpgradeCostBase()
    {
        int next = ComodoLevel + 1;
        if (next > maxComodoLevel)
            return -1f;
        return comodoBaseCost * Mathf.Pow(2f, next - 2);
    }

    /// <summary>
    /// Custo para avancar, ja com o desconto pendente do Milk-shake
    /// (descontos nao acumulam; apenas um vale por aprimoramento). -1 se maximo.
    /// </summary>
    public float ComodoUpgradeCost()
    {
        float baseCost = ComodoUpgradeCostBase();
        if (baseCost < 0f)
            return -1f;
        FoodBonusManager food = FoodBonusManager.Instance;
        float cost = food != null
            ? baseCost * (1f - food.PeekComodoDiscount)
            : baseCost;
        // Desconto permanente do sticker Caixa de Papelao com Gatinho,
        // aplicado por cima do desconto de comida.
        return cost * (1f - StickerManager.ComodoDiscountFraction);
    }

    /// <summary>Nivel do comodo que desbloqueia o movel (2, 5 ou 10).</summary>
    public int UnlockLevel(FurnitureKind kind) => UnlockLevels[(int)kind];

    public bool IsUnlocked(FurnitureKind kind) => ComodoLevel >= UnlockLevel(kind);

    /// <summary>Proximo nivel do comodo ainda sem desbloqueio, ou -1 se ja liberado.</summary>
    public int NextUnlockComodoLevel(FurnitureKind kind)
    {
        int unlock = UnlockLevel(kind);
        return ComodoLevel < unlock ? unlock : -1;
    }

    /// <summary>
    /// Evolui o comodo. Zera os niveis dos moveis (reset do GDD) e desbloqueia
    /// os moveis do novo nivel. Retorna false se nao houver saldo/maximo.
    /// </summary>
    public bool TryUpgradeComodo()
    {
        float cost = ComodoUpgradeCost();
        if (cost < 0f)
            return false;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null || !slime.SpendGoo(cost))
            return false;

        SaveData data = Save();
        if (data == null)
            return false;

        data.comodoLevel = Mathf.Min(maxComodoLevel, data.comodoLevel + 1);
        // Reset de moveis: todos os niveis voltam a zero a cada avanco do comodo.
        data.bedLevel = 0;
        data.wardrobeLevel = 0;
        data.dresserLevel = 0;

        // Desconto do Milk-shake e consumido nesta compra.
        FoodBonusManager.Instance.TakeComodoDiscount();

        SaveManager.Instance?.RequestSave();
        StateChanged?.Invoke();
        return true;
    }

    // ---- Moveis ----

    public int FurnitureLevel(FurnitureKind kind)
    {
        SaveData data = Save();
        if (data == null)
            return 0;
        switch (kind)
        {
            case FurnitureKind.Bed: return data.bedLevel;
            case FurnitureKind.Wardrobe: return data.wardrobeLevel;
            default: return data.dresserLevel;
        }
    }

    /// <summary>Nivel maximo do movel no comodo atual. 0 se ainda bloqueado.</summary>
    public int FurnitureMaxLevel(FurnitureKind kind)
    {
        if (!IsUnlocked(kind))
            return 0;
        return Mathf.Min(furnitureLevelCap, (ComodoLevel - UnlockLevel(kind) + 1) * maxLevelGainPerComodo);
    }

    /// <summary>Custo do proximo nivel. -1 se bloqueado ou no maximo.</summary>
    public float FurnitureUpgradeCost(FurnitureKind kind)
    {
        int level = FurnitureLevel(kind);
        int max = FurnitureMaxLevel(kind);
        if (max <= 0 || level >= max)
            return -1f;
        float baseCost = Mathf.Ceil(BaseCosts[kind == FurnitureKind.Bed ? 0 : kind == FurnitureKind.Wardrobe ? 1 : 2]
            * Mathf.Pow(1.4f, level));
        // Desconto do Sanduiche (nao acumula; apenas um por aprimoramento) e
        // desconto permanente do Martelo (sticker), aplicado por cima.
        FoodBonusManager food = FoodBonusManager.Instance;
        float discount = food != null ? food.PeekFurnitureDiscount : 0f;
        float cost = baseCost * (1f - discount)
            * (1f - StickerManager.FurnitureDiscountFraction);
        return Mathf.Max(1f, Mathf.Ceil(cost));
    }

    public bool TryUpgradeFurniture(FurnitureKind kind)
    {
        float cost = FurnitureUpgradeCost(kind);
        if (cost < 0f)
            return false;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null || !slime.SpendGoo(cost))
            return false;

        SaveData data = Save();
        if (data == null)
            return false;

        switch (kind)
        {
            case FurnitureKind.Bed: data.bedLevel++; break;
            case FurnitureKind.Wardrobe: data.wardrobeLevel++; break;
            default: data.dresserLevel++; break;
        }

        // Desconto do Sanduiche e consumido nesta compra.
        FoodBonusManager.Instance.TakeFurnitureDiscount();

        SaveManager.Instance?.RequestSave();
        StateChanged?.Invoke();
        return true;
    }

    // ---- Efeitos ----

    /// <summary>Gosma por segundo produzida pela Cama no nivel atual.</summary>
    public static float BedGooRate(int level) => level <= 0 ? 0f : 5f * Mathf.Pow(2f, level - 1);

    /// <summary>
    /// Gosma por segundo produzida pela Cama (inclui bônus AFK de comida e
    /// de stickers: Capivara Tranquila sempre, Bolo de Morango durante a
    /// Euforia). Cada bonus entra como fator multiplicativo por cima.
    /// </summary>
    public float BedGooPerSecond
    {
        get
        {
            float rate = BedGooRate(FurnitureLevel(FurnitureKind.Bed))
                * (1f + FoodBonusManager.Instance.AfkBonusFraction)
                * (1f + StickerManager.AfkGooBonusFraction);

            SlimeManager slime = SlimeManager.Instance;
            if (slime != null && slime.IsEuphoric)
                rate *= 1f + StickerManager.EuforiaAfkBonusFraction;

            return rate;
        }
    }

    /// <summary>Bonus fracionario do Guarda-roupas (0.20 = +20%). GDD: 100*(1.2^n - 1)%.</summary>
    public static float WardrobeBonusAt(int level) => level <= 0 ? 0f : Mathf.Pow(1.2f, level) - 1f;

    public float WardrobeBonusFraction => WardrobeBonusAt(FurnitureLevel(FurnitureKind.Wardrobe));

    /// <summary>Chance de critico da Comoda (0.35% por nivel; 35% no Lv.100).</summary>
    public static float DresserCritChanceAt(int level) => Mathf.Clamp01(level * 0.0035f);

    public float DresserCritChance => DresserCritChanceAt(FurnitureLevel(FurnitureKind.Dresser));

    // ---- Producao da Cama ----

    private void Update()
    {
        bedAccumulator += Time.unscaledDeltaTime;
        if (bedAccumulator < bedTickSeconds)
            return;

        float creditedSeconds = bedAccumulator;
        bedAccumulator = 0f;

        SaveData data = Save();
        if (data == null)
            return;

        // Vazio do relogio: o loop nao rodou (app suspenso/fechado sem
        // callback, editor pausado). Credita como offline e ignora o delta
        // do frame para nao contar o mesmo tempo duas vezes.
        if (TryGetAfkBaselineUtc(data, out DateTime baselineUtc))
        {
            float gapSeconds = (float)(DateTime.UtcNow - baselineUtc).TotalSeconds;
            if (gapSeconds >= OfflineGapThresholdSeconds)
            {
                CreditOffline(data, gapSeconds);
                return;
            }
        }

        float rate = BedGooPerSecond;
        if (rate > 0f)
        {
            // Credita sem gravar a cada tick; a gravacao e pedida em intervalo
            // maior para nao gerar escritas constantes no disco.
            SlimeManager slime = SlimeManager.Instance;
            if (slime != null)
            {
                slime.AddGooPassive(rate * creditedSeconds);
                bedSecondsSinceSave += creditedSeconds;
                if (bedSecondsSinceSave >= passiveSaveIntervalSeconds)
                {
                    bedSecondsSinceSave = 0f;
                    SaveManager.Instance?.RequestSave();
                }
            }
        }

        // Relogio AFK em dia a cada credito (mesmo com a Cama zerada): o
        // proximo lancamento/corrido credita apenas o tempo realmente ausente.
        data.lastAfkUtc = FormatUtc(DateTime.UtcNow);
    }

    // ---- Producao offline (app fechado / segundo plano) ----

    /// <summary>
    /// Vao minimo para tratar um vazio de relogio como ausencia do loop.
    /// Entre credits o intervalo normal e ~bedTickSeconds; qualquer vazio
    /// 3x maior (min. 5s) indica suspensao/fechamento do app.
    /// </summary>
    private float OfflineGapThresholdSeconds => Mathf.Max(bedTickSeconds * 3f, 5f);

    /// <summary>
    /// Liquida a producao da Cama do periodo em que o jogo nao estava
    /// rodando (app fechado, segundo plano ou editor parado). Usa o relogio
    /// AFK persistido em UTC — confiavel no Android/iOS, onde o Update()
    /// para ao suspender e o SO pode matar o processo sem avisar.
    /// Chamado no Start e ao voltar do segundo plano; idempotente.
    /// </summary>
    public void ApplyOfflineProduction()
    {
        SaveData data = Save();
        if (data == null)
            return;

        if (!TryGetAfkBaselineUtc(data, out DateTime baselineUtc))
        {
            // Sem relogio (primeiro uso do save): fixa a referencia sem
            // conceder producao retroativa automatica.
            data.lastAfkUtc = FormatUtc(DateTime.UtcNow);
            return;
        }

        CreditOffline(data, (float)(DateTime.UtcNow - baselineUtc).TotalSeconds);
    }

    /// <summary>
    /// Credita o periodo ausente com teto de horas e eficiencia offline, e
    /// realinha o relogio AFK para o instante atual. Cama zerada nao credita
    /// (saves antigos / comodo novo), apenas atualiza o relogio.
    /// </summary>
    private void CreditOffline(SaveData data, float gapSeconds)
    {
        float elapsed = Mathf.Max(0f, gapSeconds);
        if (maxOfflineHours > 0f)
            elapsed = Mathf.Min(elapsed, maxOfflineHours * 3600f);

        float amount = BedGooPerSecond * elapsed * offlineEfficiency;

        // Relogio primeiro: mesmo sem credito, o proximo exame comeca do agora.
        data.lastAfkUtc = FormatUtc(DateTime.UtcNow);
        bedAccumulator = 0f;

        if (amount <= 0f)
            return;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null)
            return;

        slime.AddGooPassive(amount);
        SaveManager.Instance?.RequestSave();
        Debug.Log($"[FurnitureManager] Gosma offline: +{amount:0.##} em {elapsed / 60f:0.#} min (Cama {BedGooPerSecond:0.##} gosma/s).");
    }

    /// <summary>
    /// Credita o acumulado pendente e fixa o relogio AFK no instante atual.
    /// Chamado ao ir para segundo plano para que o par (gosma + timestamp)
    /// gravado seja exato caso o SO mate o app em seguida.
    /// </summary>
    private void FlushPendingProduction()
    {
        SaveData data = Save();
        if (data == null)
            return;

        float pending = bedAccumulator;
        bedAccumulator = 0f;

        float rate = BedGooPerSecond;
        if (rate > 0f && pending > 0f)
        {
            SlimeManager slime = SlimeManager.Instance;
            if (slime != null)
                slime.AddGooPassive(rate * pending);
        }

        data.lastAfkUtc = FormatUtc(DateTime.UtcNow);
    }

    private static string FormatUtc(DateTime value) => value.ToString("O");

    /// <summary>
    /// Baseline do relogio AFK (UTC). Prefere lastAfkUtc; saves antigos
    /// caem para lastSaveUtc, cujo par gravado tambem casa com a gosma.
    /// </summary>
    private static bool TryGetAfkBaselineUtc(SaveData data, out DateTime baselineUtc)
    {
        if (TryParseUtc(data.lastAfkUtc, out baselineUtc))
            return true;
        return TryParseUtc(data.lastSaveUtc, out baselineUtc);
    }

    private static bool TryParseUtc(string value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrEmpty(value))
            return false;
        if (!DateTime.TryParse(
                value,
                null,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces |
                System.Globalization.DateTimeStyles.RoundtripKind,
                out parsed))
            return false;
        if (parsed.Kind == DateTimeKind.Unspecified)
            parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        return true;
    }
}
