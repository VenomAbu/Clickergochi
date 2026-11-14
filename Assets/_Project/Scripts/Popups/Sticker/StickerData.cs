
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSticker",
    menuName = "Game/Items/Sticker"
)]
public class StickerData : ScriptableObject
{
    [Header("Sticker Information")]
    public string stickerName;
    public Sprite stickerImage;

    [TextArea(3, 6)]
    public string description;

    [Header("Album (gacha pendente)")]
    [Tooltip("Posicao do sticker no album (ordenado com os demais).")]
    public int order;

    [Tooltip("Raridade base do sticker (o gacha ainda nao existe).")]
    public StickerRarity rarity;

    [Tooltip("Marque para o sticker aparecer bloqueado no album ate o gacha chegar.")]
    public bool blocked;

    [Header("Efeito (fracao; o valor ativo sai do rarity atual)")]
    [Tooltip("Qual sistema do jogo este sticker melhora. None = sem efeito (ainda).")]
    public StickerEffectKind effectKind = StickerEffectKind.None;

    [Tooltip("Valor no tier Comum (0.15 = +15%).")]
    public float valueComum;
    [Tooltip("Valor no tier Epico (0.15 = +15%).")]
    public float valueEpico;
    [Tooltip("Valor no tier Lendario (0.15 = +15%).")]
    public float valueLendario;

    /// <summary>Valor do efeito no tier atual (rarity do sticker).</summary>
    public float EffectValue
    {
        get
        {
            switch (rarity)
            {
                case StickerRarity.Epico:
                    return valueEpico;
                case StickerRarity.Lendario:
                    return valueLendario;
                default:
                    return valueComum;
            }
        }
    }

    /// <summary>Raridade em PT-BR para o popup do sticker.</summary>
    public string GetRarityLabel()
    {
        switch (rarity)
        {
            case StickerRarity.Epico:
                return "\u00c9pico";
            case StickerRarity.Lendario:
                return "Lend\u00e1rio";
            default:
                return "Comum";
        }
    }
}

/// <summary>Raridade base do sticker (doc de stickers).</summary>
public enum StickerRarity
{
    Comum,
    Epico,
    Lendario
}

/// <summary>Sistema do jogo afetado pelo efeito do sticker.</summary>
public enum StickerEffectKind
{
    None,
    AfkGoo,             // Capivara: +gosma AFK da Cama
    ClickGoo,           // Controle Retro: +gosma por clique
    ClickBase,          // Walkman: +no clique-base do Comodo
    FurnitureDiscount,  // Martelo: -custo de upgrade de moveis
    ComodoDiscount,     // Caixa com Gatinho: -custo de avanco do Comodo
    CritChance,         // Dino: +chance critica da Comoda
    FunDuration,        // Bichinho Virtual: +tempo do Bonus de Diversao
    FoodDuration,       // Rato Chef: +tempo do efeito de comida
    EuforiaAfk,         // Bolo de Morango: +AFK durante a Euforia
    EventChance         // Trevo da Sorte: pendente (sem sistema de eventos)
}
