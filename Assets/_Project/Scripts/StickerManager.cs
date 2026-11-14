using UnityEngine;

/// <summary>
/// Aplica os efeitos dos stickers do album (doc "Stickers"): todo sticker
/// desbloqueado (blocked = false) vale ao mesmo tempo, e os bonus sao
/// aplicados SOBRE os bônus de moveis/comodo/comida — como fatores
/// multiplicativos, nao somados aos deles.
/// Catalogo puro: carrega os StickerData de Resources/Stickers uma vez (ja
/// ordenados por order) e soma o valor do efeito por tipo; os sistemas
/// consultam as propriedades no ponto de uso. Os campos dos assets sao
/// lidos ao vivo, entao marcar blocked no Inspector reflete na hora.
/// Pendente: Trevo da Sorte (Chance de Eventos) — o sistema de eventos
/// aleatorios ainda nao existe no jogo.
/// </summary>
public static class StickerManager
{
    private static StickerData[] catalog;

    /// <summary>Stickers do album, ordenados por order (fonte unica).</summary>
    public static StickerData[] Catalog
    {
        get
        {
            EnsureLoaded();
            return catalog;
        }
    }

    /// <summary>Descarrega o catalogo (proxima leitura recarrega do Resources).</summary>
    public static void Refresh() => catalog = null;

    // ---- Somas por tipo de efeito (0 = nenhum ativo) ----

    /// <summary>Bonus de Gosma AFK da Cama (Capivara Tranquila).</summary>
    public static float AfkGooBonusFraction => Sum(StickerEffectKind.AfkGoo);

    /// <summary>Bonus de Gosma por Clique (Controle Retro).</summary>
    public static float ClickGooBonusFraction => Sum(StickerEffectKind.ClickGoo);

    /// <summary>Bonus no Clique-Base do Comodo (Walkman).</summary>
    public static float ClickBaseBonusFraction => Sum(StickerEffectKind.ClickBase);

    /// <summary>Desconto em upgrades de moveis (Martelo).</summary>
    public static float FurnitureDiscountFraction => Sum(StickerEffectKind.FurnitureDiscount);

    /// <summary>Desconto no avanco de nivel do Comodo (Caixa com Gatinho).</summary>
    public static float ComodoDiscountFraction => Sum(StickerEffectKind.ComodoDiscount);

    /// <summary>Soma em chance critica da Comoda (Dino).</summary>
    public static float CritChanceBonusFraction => Sum(StickerEffectKind.CritChance);

    /// <summary>Estende o tempo do Bonus de Diversao (Bichinho Virtual).</summary>
    public static float FunBonusDurationFraction => Sum(StickerEffectKind.FunDuration);

    /// <summary>Estende o tempo do efeito de comida (Rato Chef).</summary>
    public static float FoodDurationFraction => Sum(StickerEffectKind.FoodDuration);

    /// <summary>Bonus de Gosma AFK durante a Euforia (Bolo de Morango).</summary>
    public static float EuforiaAfkBonusFraction => Sum(StickerEffectKind.EuforiaAfk);

    private static float Sum(StickerEffectKind kind)
    {
        if (kind == StickerEffectKind.None)
            return 0f;

        EnsureLoaded();
        if (catalog == null)
            return 0f;

        float total = 0f;
        for (int i = 0; i < catalog.Length; i++)
        {
            StickerData sticker = catalog[i];
            if (sticker == null || sticker.blocked || sticker.effectKind != kind)
                continue;
            total += sticker.EffectValue;
        }
        return total;
    }

    private static void EnsureLoaded()
    {
        if (catalog != null)
            return;

        catalog = Resources.LoadAll<StickerData>("Stickers");
        System.Array.Sort(catalog, (a, b) => a.order.CompareTo(b.order));
    }
}
