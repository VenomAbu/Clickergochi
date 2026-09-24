using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Sistema central do slime. Os valores de gosma e cuidados vivem no
/// SaveManager; este componente emite StateChanged após cada alteração para
/// que a HUD e os objetos de Barsatas remainham sincronizados.
/// </summary>
public class SlimeManager : MonoBehaviour
{
    [Header("Produção ativa")]
    [SerializeField] private float baseGooPerClick = 1f;

    [Header("Decaimento dos cuidados (por hora)")]
    [Tooltip("Quantidade de pontos (0 a 1) que a Diversão perde a cada hora real.")]
    [SerializeField, Min(0f)] private float funDecayPerHour = 0.10f;
    [Tooltip("Quantidade de pontos (0 a 1) que a Fome perde a cada hora real. Deve ser menor que a diversão.")]
    [SerializeField, Min(0f)] private float hungerDecayPerHour = 0.04f;
    [Tooltip("Quantidade de pontos (0 a 1) que a Higiene perde a cada hora real. Deve ser o menor valor.")]
    [SerializeField, Min(0f)] private float hygieneDecayPerHour = 0.02f;
    [Tooltip("Intervalo de atualização do decaimento durante a partida.")]
    [SerializeField, Min(0.1f)] private float decayUpdateIntervalSeconds = 1f;
    [Tooltip("Intervalo para solicitar uma gravação dos valores que mudaram apenas por decaimento.")]
    [SerializeField, Min(1f)] private float decaySaveIntervalSeconds = 30f;

    [Header("Diversão")]
    [SerializeField] private int requiredFunClicks = 200;
    [SerializeField] private int funBonusClickCount = 20;
    [SerializeField] private float funBonusClickMultiplier = 2f;
    [SerializeField] private float funBonusDurationSeconds = 30f;

    [Header("Euforia (carga 100->0% em 4h; bonus x1.00-x2.00 por tier)")]
    [SerializeField] private float euphoriaDurationHours = 4f;

    private readonly Dictionary<string, float> gooMultipliers = new();
    private const string FunBonusId = "fun_bonus";
    private const string EuphoriaId = "euphoria";

    private int funBonusClicksLeft;
    private float funBonusTimer;
    private SaveData fallback = new SaveData();
    private SaveManager subscribedSaveManager;
    private DateTime lastDecayReferenceUtc;
    private bool decayReferenceReady;
    private float decayCheckTimer;
    private float decaySaveTimer;

    /// <summary>Taxa de decaimento da Diversão por hora real.</summary>
    public float FunDecayPerHour
    {
        get => funDecayPerHour;
        set => funDecayPerHour = Mathf.Max(0f, value);
    }

    /// <summary>Taxa de decaimento da Fome por hora real.</summary>
    public float HungerDecayPerHour
    {
        get => hungerDecayPerHour;
        set => hungerDecayPerHour = Mathf.Max(0f, value);
    }

    /// <summary>Taxa de decaimento da Higiene por hora real.</summary>
    public float HygieneDecayPerHour
    {
        get => hygieneDecayPerHour;
        set => hygieneDecayPerHour = Mathf.Max(0f, value);
    }

    /// <summary>Disparado quando goo, fome, diversão, higiene ou humor mudam.</summary>
    public event Action StateChanged;

    public float Goo => Save().goo;
    public float Hunger => Save().hunger;
    public float Fun => Save().fun;
    public float Hygiene => Save().hygiene;
    public bool FunBonusActive => funBonusClicksLeft > 0 && funBonusTimer > 0f;
    public int FunBonusClicksLeft => funBonusClicksLeft;

    private SaveData Save()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager != null)
        {
            SaveData data = manager.GetSaveData();
            if (data != null)
                return data;
        }
        return fallback;
    }

    private void Persist()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager != null && manager.GetSaveData() != null)
            manager.RequestSave();
    }

    private void Commit()
    {
        Persist();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private void OnEnable()
    {
        SubscribeToSaveManager();
        InitializeCareDecay();
        RefreshEuphoriaMultiplier();
        NotifyStateChanged();
    }

    private void OnDisable()
    {
        // Atualiza a referência de tempo antes de trocar de cena ou sair do Play Mode.
        ApplyTimeDecay(DateTime.UtcNow, true);
        UnsubscribeFromSaveManager();
    }

    private void SubscribeToSaveManager()
    {
        SaveManager manager = SaveManager.Instance;
        if (subscribedSaveManager == manager)
            return;

        UnsubscribeFromSaveManager();
        subscribedSaveManager = manager;
        if (subscribedSaveManager != null)
        {
            subscribedSaveManager.BeforeSave += HandleBeforeSave;
            subscribedSaveManager.SaveDataChanged += HandleSaveDataChanged;
        }
    }

    private void UnsubscribeFromSaveManager()
    {
        if (subscribedSaveManager == null)
            return;
        subscribedSaveManager.BeforeSave -= HandleBeforeSave;
        subscribedSaveManager.SaveDataChanged -= HandleSaveDataChanged;
        subscribedSaveManager = null;
    }

    private void HandleBeforeSave()
    {
        // O SaveManager grava o arquivo de forma sincrona; aplica o tempo
        // decorrido antes disso para que o proximo carregamento nao conte a
        // mesma janela novamente.
        ApplyTimeDecay(DateTime.UtcNow, false);
    }

    private void HandleSaveDataChanged(SaveData data)
    {
        RefreshEuphoriaMultiplier();
        NotifyStateChanged();
    }

    private void Update()
    {
        decayCheckTimer -= Time.unscaledDeltaTime;
        if (decayCheckTimer <= 0f)
        {
            decayCheckTimer = Mathf.Max(0.1f, decayUpdateIntervalSeconds);
            ApplyTimeDecay(DateTime.UtcNow, true);
        }

        if (funBonusClicksLeft > 0)
        {
            funBonusTimer -= Time.deltaTime;
            if (funBonusTimer <= 0f)
                ClearFunBonus();
        }
        RefreshEuphoriaMultiplier();
    }

    private void InitializeCareDecay()
    {
        SaveData data = Save();
        DateTime nowUtc = DateTime.UtcNow;
        bool hasBaseline = TryGetDecayBaselineUtc(data, out DateTime baselineUtc);
        bool changed = false;

        if (hasBaseline)
        {
            double elapsedHours = (nowUtc - baselineUtc).TotalHours;
            if (elapsedHours > 0d)
                changed = ApplyDecay(data, elapsedHours);
        }

        // A referencia e sempre UTC para que uma mudanca de fuso horario ou
        // horario de verão nao altere a duracao calculada.
        data.lastCareDecayUtc = FormatUtc(nowUtc);
        lastDecayReferenceUtc = nowUtc;
        decayReferenceReady = true;
        decayCheckTimer = Mathf.Max(0.1f, decayUpdateIntervalSeconds);
        decaySaveTimer = 0f;

        // Um save antigo pode nao ter o novo campo. Persistimos a migracao
        // junto com a aplicacao do tempo offline.
        if (changed || !hasBaseline)
        {
            NotifyStateChanged();
            Persist();
        }
    }

    private void ApplyPendingCareDecay()
    {
        if (decayReferenceReady)
            ApplyTimeDecay(DateTime.UtcNow, false);
    }

    private void ApplyTimeDecay(DateTime nowUtc, bool requestSave)
    {
        SaveData data = Save();
        if (!decayReferenceReady)
        {
            lastDecayReferenceUtc = nowUtc;
            decayReferenceReady = true;
            data.lastCareDecayUtc = FormatUtc(nowUtc);
            return;
        }

        double elapsedSeconds = (nowUtc - lastDecayReferenceUtc).TotalSeconds;
        if (elapsedSeconds <= 0d)
            return;

        lastDecayReferenceUtc = nowUtc;
        bool changed = ApplyDecay(data, elapsedSeconds / 3600d);
        data.lastCareDecayUtc = FormatUtc(nowUtc);

        if (!changed)
        {
            decaySaveTimer = 0f;
            return;
        }

        decaySaveTimer += (float)elapsedSeconds;
        NotifyStateChanged();

        // Durante uma partida longa, nao e necessario escrever no disco a
        // cada segundo. O SaveManager ainda garante esse flush no pause/quit.
        if (requestSave && decaySaveTimer >= Mathf.Max(1f, decaySaveIntervalSeconds))
        {
            decaySaveTimer = 0f;
            Persist();
        }
    }

    private bool ApplyDecay(SaveData data, double elapsedHours)
    {
        if (data == null || elapsedHours <= 0d)
            return false;

        float oldFun = data.fun;
        float oldHunger = data.hunger;
        float oldHygiene = data.hygiene;

        float funRate = Mathf.Max(0f, funDecayPerHour);
        float hungerRate = Mathf.Max(0f, hungerDecayPerHour);
        float hygieneRate = Mathf.Max(0f, hygieneDecayPerHour);

        data.fun = Mathf.Clamp01(data.fun - (float)(elapsedHours * funRate));
        data.hunger = Mathf.Clamp01(data.hunger - (float)(elapsedHours * hungerRate));
        data.hygiene = Mathf.Clamp01(data.hygiene - (float)(elapsedHours * hygieneRate));

        return !Mathf.Approximately(oldFun, data.fun)
            || !Mathf.Approximately(oldHunger, data.hunger)
            || !Mathf.Approximately(oldHygiene, data.hygiene);
    }

    private static string FormatUtc(DateTime value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static bool TryGetDecayBaselineUtc(SaveData data, out DateTime baselineUtc)
    {
        baselineUtc = default;
        if (data == null)
            return false;

        string value = data.lastCareDecayUtc;
        if (string.IsNullOrWhiteSpace(value))
            value = data.lastSaveUtc;
        if (string.IsNullOrWhiteSpace(value))
            value = data.lastSaveTime;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out DateTime parsed))
            return false;

        if (parsed.Kind == DateTimeKind.Unspecified)
            parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Local);

        baselineUtc = parsed.ToUniversalTime();
        return true;
    }

    /// <summary>Clique na slime. Retorna a gosma ganha com multiplicadores.</summary>
    public float RegisterClick()
    {
        ApplyPendingCareDecay();
        SaveData data = Save();
        float gain = baseGooPerClick * CurrentMultiplier();
        data.goo += gain;

        if (data.fun < 1f)
        {
            data.fun = Mathf.Min(1f, data.fun + 1f / Mathf.Max(1, requiredFunClicks));
            if (data.fun >= 1f && !FunBonusActive)
                ActivateFunBonus();
        }

        if (FunBonusActive && funBonusClicksLeft > 0)
        {
            funBonusClicksLeft--;
            if (funBonusClicksLeft <= 0)
                ClearFunBonus();
        }

        CheckEuphoriaTrigger();
        Commit();
        return gain;
    }

    public void AddHunger(float amount)
    {
        ApplyPendingCareDecay();
        SaveData data = Save();
        data.hunger = Mathf.Clamp01(data.hunger + amount);
        CheckEuphoriaTrigger();
        Commit();
    }

    public void AddHygiene(float amount)
    {
        ApplyPendingCareDecay();
        SaveData data = Save();
        data.hygiene = Mathf.Clamp01(data.hygiene + amount);
        CheckEuphoriaTrigger();
        Commit();
    }

    public void AddFun(float amount)
    {
        ApplyPendingCareDecay();
        SaveData data = Save();
        data.fun = Mathf.Clamp01(data.fun + amount);
        if (data.fun >= 1f && !FunBonusActive)
            ActivateFunBonus();
        CheckEuphoriaTrigger();
        Commit();
    }

    /// <summary>Define os cuidados e solicita a persistência com debounce.</summary>
    public void SetCare(float hunger, float fun, float hygiene)
    {
        ApplyPendingCareDecay();
        SaveData data = Save();
        data.hunger = Mathf.Clamp01(hunger);
        data.fun = Mathf.Clamp01(fun);
        if (data.fun >= 1f && !FunBonusActive)
            ActivateFunBonus();
        data.hygiene = Mathf.Clamp01(hygiene);
        CheckEuphoriaTrigger();
        Commit();
    }

    public void ClearEuphoria()
    {
        Save().euphoriaEndUtc = "";
        UnregisterMultiplier(EuphoriaId);
        Commit();
    }

    public void AddGoo(float amount)
    {
        SaveData data = Save();
        data.goo = Mathf.Max(0f, data.goo + amount);
        Commit();
    }

    /// <summary>Tenta gastar gosma. Retorna false se não houver saldo.</summary>
    public bool SpendGoo(float amount)
    {
        SaveData data = Save();
        if (data.goo < amount)
            return false;
        data.goo -= amount;
        Commit();
        return true;
    }

    public void RegisterMultiplier(string id, float multiplier)
    {
        gooMultipliers[id] = multiplier;
    }

    public void UnregisterMultiplier(string id)
    {
        gooMultipliers.Remove(id);
    }

    public float CurrentMultiplier()
    {
        float result = 1f;
        foreach (float multiplier in gooMultipliers.Values)
            result *= multiplier;
        return result;
    }

    public bool IsEuphoric
    {
        get
        {
            string end = Save().euphoriaEndUtc;
            if (string.IsNullOrEmpty(end))
                return false;
            if (!DateTime.TryParse(end, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime endTime))
                return false;
            return DateTime.UtcNow < endTime;
        }
    }

    public TimeSpan EuphoriaRemaining()
    {
        string end = Save().euphoriaEndUtc;
        if (string.IsNullOrEmpty(end))
            return TimeSpan.Zero;
        if (!DateTime.TryParse(end, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime endTime))
            return TimeSpan.Zero;
        TimeSpan remaining = endTime - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public float EuphoriaCharge()
    {
        if (!IsEuphoric)
            return 0f;
        double total = TimeSpan.FromHours(Mathf.Max(0.01f, euphoriaDurationHours)).TotalSeconds;
        return Mathf.Clamp01((float)(EuphoriaRemaining().TotalSeconds / total));
    }

    public int GetEuphoriaMoodTier()
    {
        float charge = EuphoriaCharge();
        if (charge >= 0.75f) return 4;
        if (charge >= 0.50f) return 3;
        if (charge >= 0.25f) return 2;
        return charge > 0f ? 1 : 0;
    }

    public static readonly string[] MoodTexts = { ":(", ":|", ":0", ":P", ":)" };
    public static readonly Color[] MoodColors =
    {
        new Color(0.12f, 0.23f, 0.55f),
        new Color(0.25f, 0.25f, 0.28f),
        new Color(0.51f, 0.51f, 0.55f),
        new Color(0.51f, 0.71f, 0.55f),
        new Color(0.31f, 0.78f, 0.47f),
    };

    public void RefreshEuphoriaMultiplier()
    {
        if (!IsEuphoric)
        {
            UnregisterMultiplier(EuphoriaId);
            return;
        }
        RegisterMultiplier(EuphoriaId, 1f + 0.25f * GetEuphoriaMoodTier());
    }

    private void CheckEuphoriaTrigger()
    {
        SaveData data = Save();
        if (IsEuphoric)
            return;
        if (data.hunger < 1f || data.fun < 1f || data.hygiene < 1f)
            return;
        data.euphoriaEndUtc = DateTime.UtcNow.AddHours(euphoriaDurationHours).ToString("o");
        RefreshEuphoriaMultiplier();
    }

    private void ActivateFunBonus()
    {
        funBonusClicksLeft = funBonusClickCount;
        funBonusTimer = funBonusDurationSeconds;
        RegisterMultiplier(FunBonusId, funBonusClickMultiplier);
    }

    private void ClearFunBonus()
    {
        funBonusClicksLeft = 0;
        funBonusTimer = 0f;
        UnregisterMultiplier(FunBonusId);
    }
}
