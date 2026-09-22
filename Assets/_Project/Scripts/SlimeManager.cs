using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema central do slime (segue o padrao *Manager do projeto).
/// Fonte unica de: producao de gosma, barras de cuidado (fome/diversao/
/// higiene), bonus de diversao, pipeline multiplicativo e euforia.
/// Nao altera ClickerManager/LevelUpManager (cena MainGame intacta).
/// </summary>
public class SlimeManager : MonoBehaviour
{
    [Header("Producao ativa")]
    [SerializeField] private float baseGooPerClick = 1f;

    [Header("Diversao")]
    [SerializeField] private int requiredFunClicks = 200;
    [SerializeField] private int funBonusClickCount = 20;
    [SerializeField] private float funBonusClickMultiplier = 2f;
    [SerializeField] private float funBonusDurationSeconds = 30f;

    [Header("Euforia (carga 100->0% em 4h; bonus x1.00-x2.00 por tier)")]
    [SerializeField] private float euphoriaDurationHours = 4f;

    // Modificadores ativos (id -> multiplicador). Composicao multiplicativa (§20).
    private readonly Dictionary<string, float> gooMultipliers = new();
    private const string FunBonusId = "fun_bonus";
    private const string EuphoriaId = "euphoria";

    // Bonus de diversao: runtime-only por enquanto (persistencia offline = pendencia).
    private int funBonusClicksLeft;
    private float funBonusTimer;

    public float Goo => Save().goo;
    public float Hunger => Save().hunger;
    public float Fun => Save().fun;
    public float Hygiene => Save().hygiene;
    public bool FunBonusActive => funBonusClicksLeft > 0 && funBonusTimer > 0f;
    public int FunBonusClicksLeft => funBonusClicksLeft;

    private SaveData fallback = new SaveData();

    private SaveData Save()
    {
        if (SaveManager.Instance != null)
            return SaveManager.Instance.GetSaveData();
        return fallback;
    }

    private void Persist()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

    private void Update()
    {
        if (funBonusClicksLeft > 0)
        {
            funBonusTimer -= Time.deltaTime;
            if (funBonusTimer <= 0f)
                ClearFunBonus();
        }
        RefreshEuphoriaMultiplier();
    }

    /// <summary>Clique na slime. Retorna a gosma ganha (ja com multiplicadores).</summary>
    public float RegisterClick()
    {
        var data = Save();
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
        Persist();
        return gain;
    }

    public void AddHunger(float amount)
    {
        var data = Save();
        data.hunger = Mathf.Clamp01(data.hunger + amount);
        CheckEuphoriaTrigger();
        Persist();
    }

    public void AddHygiene(float amount)
    {
        var data = Save();
        data.hygiene = Mathf.Clamp01(data.hygiene + amount);
        CheckEuphoriaTrigger();
        Persist();
    }

    public void AddFun(float amount)
    {
        var data = Save();
        data.fun = Mathf.Clamp01(data.fun + amount);
        if (data.fun >= 1f && !FunBonusActive)
            ActivateFunBonus();
        CheckEuphoriaTrigger();
        Persist();
    }

    /// <summary>Define os cuidados (debug/teste). Valores sempre 0-1.</summary>
    public void SetCare(float h, float f, float hy)
    {
        var data = Save();
        data.hunger = Mathf.Clamp01(h);
        data.fun = Mathf.Clamp01(f);
        if (data.fun >= 1f && !FunBonusActive)
            ActivateFunBonus();
        data.hygiene = Mathf.Clamp01(hy);
        CheckEuphoriaTrigger();
        Persist();
    }

    public void ClearEuphoria()
    {
        Save().euphoriaEndUtc = "";
        UnregisterMultiplier(EuphoriaId);
        Persist();
    }

    public void AddGoo(float amount)
    {
        var data = Save();
        data.goo = Mathf.Max(0f, data.goo + amount);
        Persist();
    }

    /// <summary>Tenta gastar gosma. Retorna false se nao ha saldo.</summary>
    public bool SpendGoo(float amount)
    {
        var data = Save();
        if (data.goo < amount)
            return false;
        data.goo -= amount;
        Persist();
        return true;
    }

    public void RegisterMultiplier(string id, float mult)
    {
        gooMultipliers[id] = mult;
    }

    public void UnregisterMultiplier(string id)
    {
        gooMultipliers.Remove(id);
    }

    public float CurrentMultiplier()
    {
        float result = 1f;
        foreach (var mult in gooMultipliers.Values)
            result *= mult;
        return result;
    }

    // ---- Euforia (timestamp UTC: sobrevive ao jogo fechado) ----
    public bool IsEuphoric
    {
        get
        {
            var end = Save().euphoriaEndUtc;
            if (string.IsNullOrEmpty(end))
                return false;
            if (!DateTime.TryParse(end, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endTime))
                return false;
            if (DateTime.UtcNow >= endTime)
                return false;
            return true;
        }
    }

    public TimeSpan EuphoriaRemaining()
    {
        var end = Save().euphoriaEndUtc;
        if (string.IsNullOrEmpty(end))
            return TimeSpan.Zero;
        if (!DateTime.TryParse(end, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endTime))
            return TimeSpan.Zero;
        var remaining = endTime - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>Carga atual da euforia 0-1 (100% ao ativar, esvazia no prazo).</summary>
    public float EuphoriaCharge()
    {
        if (!IsEuphoric)
            return 0f;
        double total = TimeSpan.FromHours(Mathf.Max(0.01f, euphoriaDurationHours)).TotalSeconds;
        return Mathf.Clamp01((float)(EuphoriaRemaining().TotalSeconds / total));
    }

    /// <summary>Tier 0-4 por faixa da carga: 0 / 0-25 / 25-50 / 50-75 / 75-100%. Bonus: x1.00-x2.00.</summary>
    public int GetEuphoriaMoodTier()
    {
        float charge = EuphoriaCharge();
        if (charge >= 0.75f) return 4;
        if (charge >= 0.50f) return 3;
        if (charge >= 0.25f) return 2;
        if (charge > 0f) return 1;
        return 0;
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

    /// <summary>Recalcula o bonus da euforia pela carga (x1.00-x2.00).</summary>
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
        var data = Save();
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

    private void OnEnable()
    {
        // Re-registra a euforia se o save indica estado ativo (ex.: abriu o jogo depois).
        RefreshEuphoriaMultiplier();
    }
}
