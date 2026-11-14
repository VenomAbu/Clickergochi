using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia os efeitos dos alimentos conforme o TP3:
/// - Comer exige Fome abaixo de 100% e custa gosma.
/// - Efeitos temporarios (Pizza/Omelete/Macarrao) acumulam entre si e
///   expiram pelo relogio de parede, valendo mesmo com o jogo fechado.
/// - Efeitos instantaneos (Cookie/Pudim/Bolo/Fruta) creditam na hora.
/// - Descontos (Sanduiche/Milk-shake) NAO acumulam: fica o maior e e
///   consumido no proximo aprimoramento bem-sucedido.
/// </summary>
public class FoodBonusManager : MonoBehaviour
{
    private static FoodBonusManager _instance;

    /// <summary>
    /// Instancia unica. Se a cena nao tiver o componente, cria um
    /// GameObject em runtime (mesmo padrao do FurnitureManager).
    /// </summary>
    public static FoodBonusManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<FoodBonusManager>();
            if (_instance == null)
            {
                var go = new GameObject("FoodBonusManager");
                _instance = go.AddComponent<FoodBonusManager>();
            }
            return _instance;
        }
    }

    [Tooltip("Intervalo de limpeza dos buffs expirados.")]
    [SerializeField, Min(0.2f)] private float pruneIntervalSeconds = 1f;

    private float pruneTimer;

    /// <summary>Disparado quando buffs expiram ou um alimento e consumido.</summary>
    public event Action StateChanged;

    /// <summary>Pode comer? Regra do TP3: sem comer com a Fome em 100%.</summary>
    public bool CanEat
    {
        get
        {
            SlimeManager slime = SlimeManager.Instance;
            return slime != null && slime.Hunger < 1f;
        }
    }

    /// <summary>Bonus fracionario de Gosma AFK ativo (0.15 = +15%).</summary>
    public float AfkBonusFraction => SumActive(FoodEffectKind.AfkGooBonus);

    /// <summary>Bonus fracionario de chance de critico ativo (0.02 = +2%).</summary>
    public float CritChanceBonus => SumActive(FoodEffectKind.CritChanceBonus);

    /// <summary>Bonus fracionario de chance de evento aleatorio ativo.</summary>
    public float EventChanceBonus => SumActive(FoodEffectKind.EventChanceBonus);

    /// <summary>Stickers extras ja acumulados para o proximo pacote.</summary>
    public int PendingStickerPackBonus
    {
        get
        {
            SaveData data = Save();
            return data != null ? data.pendingStickerPackBonus : 0;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        pruneTimer -= Time.unscaledDeltaTime;
        if (pruneTimer > 0f)
            return;
        pruneTimer = pruneIntervalSeconds;

        if (PruneExpired())
        {
            SaveManager.Instance?.RequestSave();
            StateChanged?.Invoke();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        // Buffs expiram pelo relogio de parede; ao voltar, limpa antes da
        // UI mostrar efeitos que ja terminaram.
        if (!paused)
            return;
        if (PruneExpired())
        {
            SaveManager.Instance?.RequestSave();
            StateChanged?.Invoke();
        }
    }

    // ---- Consumo ----

    /// <summary>
    /// Come uma variante de alimento: paga o preco, recupera a Fome e
    /// aplica o efeito. Retorna false se nao houver saldo ou Fome cheia.
    /// </summary>
    public bool TryEat(FoodItemData food, FoodTier tier)
    {
        if (food == null || tier == null)
            return false;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null || slime.Hunger >= 1f)
            return false;

        if (!slime.SpendGoo(tier.price))
            return false;

        slime.AddHunger(tier.hungerRestore);
        ApplyEffect(tier);

        SaveManager.Instance?.RequestSave();
        StateChanged?.Invoke();
        Debug.Log($"[Food] {food.foodName} ({tier.rarity}): {tier.Describe()}");
        return true;
    }

    private void ApplyEffect(FoodTier tier)
    {
        SaveData data = Save();
        SlimeManager slime = SlimeManager.Instance;
        if (data == null || slime == null)
            return;

        switch (tier.effect)
        {
            case FoodEffectKind.AfkGooBonus:
            case FoodEffectKind.CritChanceBonus:
            case FoodEffectKind.EventChanceBonus:
                AddTimedBuff(data, tier);
                break;

            case FoodEffectKind.InstantClickGoo:
                slime.AddGooPassive(slime.StandardClickGoo() * tier.effectValue);
                break;

            case FoodEffectKind.InstantAfkMinutes:
                slime.AddGooPassive(
                    FurnitureManager.BedGooRate(FurnitureManager.Instance.FurnitureLevel(FurnitureKind.Bed))
                    * tier.effectValue * 60f);
                break;

            case FoodEffectKind.FurnitureDiscount:
                // Descontos nao acumulam: fica o maior.
                data.nextFurnitureDiscount = Mathf.Max(data.nextFurnitureDiscount, tier.effectValue);
                break;

            case FoodEffectKind.ComodoDiscount:
                data.nextComodoDiscount = Mathf.Max(data.nextComodoDiscount, tier.effectValue);
                break;

            case FoodEffectKind.StickerPackBonus:
                data.pendingStickerPackBonus += Mathf.RoundToInt(tier.effectValue);
                break;

            case FoodEffectKind.ComodoCostGoo:
                slime.AddGooPassive(
                    FurnitureManager.Instance.ComodoUpgradeCostBase() * tier.effectValue);
                break;

            case FoodEffectKind.HungerFill:
                slime.AddHunger(tier.effectValue);
                break;
        }
    }

    private static void AddTimedBuff(SaveData data, FoodTier tier)
    {
        // Rato Chef (sticker): estende o tempo de todos os efeitos de
        // comida como fator multiplicativo por cima.
        float seconds = Mathf.Max(0f, tier.effectDurationMinutes) * 60f
            * (1f + StickerManager.FoodDurationFraction);
        string endUtc = DateTime.UtcNow
            .AddSeconds(seconds)
            .ToString("O");
        data.foodBuffs.Add(new ActiveFoodBuff
        {
            kind = (int)tier.effect,
            amount = tier.effectValue,
            endUtc = endUtc
        });
    }

    // ---- Descontos (consumidos pelo FurnitureManager na compra) ----

    /// <summary>Desconto pendente do proximo aprimoramento de movel (0 = nenhum).</summary>
    public float PeekFurnitureDiscount
    {
        get
        {
            SaveData data = Save();
            return data != null ? data.nextFurnitureDiscount : 0f;
        }
    }

    /// <summary>Consome o desconto de movel (chamar so apos compra bem-sucedida).</summary>
    public void TakeFurnitureDiscount()
    {
        SaveData data = Save();
        if (data != null)
            data.nextFurnitureDiscount = 0f;
    }

    /// <summary>Desconto pendente do proximo nivel do cômodo (0 = nenhum).</summary>
    public float PeekComodoDiscount
    {
        get
        {
            SaveData data = Save();
            return data != null ? data.nextComodoDiscount : 0f;
        }
    }

    /// <summary>Consome o desconto do cômodo (chamar so apos compra bem-sucedida).</summary>
    public void TakeComodoDiscount()
    {
        SaveData data = Save();
        if (data != null)
            data.nextComodoDiscount = 0f;
    }

    // ---- Internos ----

    private float SumActive(FoodEffectKind kind)
    {
        SaveData data = Save();
        if (data == null || data.foodBuffs == null)
            return 0f;

        float total = 0f;
        int target = (int)kind;
        for (int i = 0; i < data.foodBuffs.Count; i++)
        {
            ActiveFoodBuff buff = data.foodBuffs[i];
            if (buff != null && buff.kind == target)
                total += buff.amount;
        }
        return total;
    }

    private bool PruneExpired()
    {
        SaveData data = Save();
        if (data == null || data.foodBuffs == null || data.foodBuffs.Count == 0)
            return false;

        DateTime nowUtc = DateTime.UtcNow;
        bool removedAny = false;
        for (int i = data.foodBuffs.Count - 1; i >= 0; i--)
        {
            ActiveFoodBuff buff = data.foodBuffs[i];
            if (buff == null
                || string.IsNullOrEmpty(buff.endUtc)
                || !DateTime.TryParse(buff.endUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime endUtc)
                || nowUtc >= endUtc)
            {
                data.foodBuffs.RemoveAt(i);
                removedAny = true;
            }
        }
        return removedAny;
    }

    private SaveData Save()
    {
        SaveManager manager = SaveManager.Instance;
        return manager != null ? manager.GetSaveData() : null;
    }
}
