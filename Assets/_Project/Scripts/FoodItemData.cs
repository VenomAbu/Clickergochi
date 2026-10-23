using System;
using UnityEngine;

/// <summary>Raridade de uma variante de alimento.</summary>
public enum FoodRarity
{
    Caseira,
    Artesanal,
    Gourmet
}

/// <summary>
/// Efeito de um alimento. O efeito e aplicado sobre os valores ja
/// existentes dos moveis/cômodo (multiplicando, nao somando) e os efeitos
/// de comidas diferentes acumulam entre si. Excecao: descontos nao
/// acumulam — apenas um desconto vale por aprimoramento.
/// </summary>
public enum FoodEffectKind
{
    None,
    AfkGooBonus,        // Pizza: +X% de gosma AFK durante duration.
    InstantClickGoo,    // Cookie: gosma equivalente a X cliques.
    InstantAfkMinutes,  // Pudim: gosma equivalente a X minutos de producao.
    FurnitureDiscount,  // Sanduiche: -X% no proximo aprimoramento de movel.
    ComodoDiscount,     // Milk-shake: -X% no proximo nivel do cômodo.
    CritChanceBonus,    // Omelete: +X de chance de critico durante duration.
    StickerPackBonus,   // Panquecas: +X stickers no proximo pacote.
    EventChanceBonus,   // Macarrao: +X de chance de evento aleatorio durante duration.
    ComodoCostGoo,      // Bolo: gosma equivalente a X% do custo do proximo cômodo.
    HungerFill          // Fruta: +X de Fome.
}

/// <summary>
/// Uma variante vendavel de um alimento (uma por raridade). Ajuste tudo
/// pelo Inspector sem tocar em codigo.
/// </summary>
[Serializable]
public class FoodTier
{
    public FoodRarity rarity = FoodRarity.Caseira;
    [Min(0)] public int price = 10;
    [Tooltip("Fracao da barra de Fome recuperada ao comer (0.5 = 50%).")]
    [Range(0f, 1f)] public float hungerRestore = 0.2f;

    [Header("Efeito")]
    public FoodEffectKind effect = FoodEffectKind.None;
    [Tooltip("Significado por efeito: fracao (0.15 = +15%), cliques, minutos ou % do custo.")]
    [Min(0f)] public float effectValue;
    [Tooltip("Duracao em minutos dos buffs temporarios (Pizza/Omelete/Macarrao).")]
    [Min(0f)] public float effectDurationMinutes = 15f;

    /// <summary>Descricao legivel do efeito para o card da loja.</summary>
    public string Describe()
    {
        float v = effectValue;
        switch (effect)
        {
            case FoodEffectKind.AfkGooBonus:
                return $"+{v * 100f:0.#}% Gosma AFK por {effectDurationMinutes:0.#} min";
            case FoodEffectKind.InstantClickGoo:
                return $"Gosma igual a {v:0} cliques";
            case FoodEffectKind.InstantAfkMinutes:
                return $"Gosma igual a {v:0.#} min de producao";
            case FoodEffectKind.FurnitureDiscount:
                return $"-{v * 100f:0.#}% no proximo aprimoramento de movel";
            case FoodEffectKind.ComodoDiscount:
                return $"-{v * 100f:0.#}% no proximo nivel do cômodo";
            case FoodEffectKind.CritChanceBonus:
                return $"+{v * 100f:0.#}% de Crítico por {effectDurationMinutes:0.#} min";
            case FoodEffectKind.StickerPackBonus:
                return $"+{v:0} stickers no proximo pacote";
            case FoodEffectKind.EventChanceBonus:
                return $"+{v * 100f:0.#}% de eventos aleatorios por {effectDurationMinutes:0.#} min";
            case FoodEffectKind.ComodoCostGoo:
                return $"Gosma igual a {v * 100f:0.#}% do custo do cômodo";
            case FoodEffectKind.HungerFill:
                return $"+{v * 100f:0.#}% de Fome";
            default:
                return "Sem efeito";
        }
    }
}

/// <summary>
/// Dados de uma comida: nome, icone e as variantes por raridade vendidas
/// na loja (preco, fome restaurada e efeito de cada uma).
/// </summary>
[CreateAssetMenu(fileName = "Food", menuName = "Clickergochi/Food Item")]
public class FoodItemData : ScriptableObject
{
    [Tooltip("Ordem na loja (menor aparece primeiro).")]
    public int order;
    public string foodName = "Comida";
    public string iconEmoji = "🍎";
    [Tooltip("Uma entrada por raridade vendida desta comida.")]
    public FoodTier[] tiers = Array.Empty<FoodTier>();
}
