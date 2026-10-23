using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Conteudo da categoria "Mveis" na tela de detalhe da loja. Monta o cartao
/// do Comodo (evolucao com aviso de reset) e um cartao por movel, com nivel,
/// efeito atual/proximo, custo do GDD e estados bloqueado/maximo/sem gosma.
/// As compras passam pelo ConfirmPopupUI antes de executar.
/// </summary>
public class FurnitureShopUI
{
    private readonly VisualElement host;
    private readonly Label placeholder;
    private readonly FurnitureManager furniture;
    private readonly SlimeManager slime;
    private readonly ConfirmPopupUI confirm;
    private readonly Action playBuy;

    private bool built;

    private Label roomTitle;
    private Label roomLine;
    private Label roomUnlockLine;
    private Label roomCost;
    private Button roomButton;

    private class Card
    {
        public FurnitureKind kind;
        public string name;
        public VisualElement root;
        public Label level;
        public Label effect;
        public Label price;
        public Button buy;
    }

    private readonly List<Card> cards = new();

    public FurnitureShopUI(
        VisualElement host,
        Label placeholder,
        FurnitureManager furniture,
        SlimeManager slime,
        ConfirmPopupUI confirm,
        Action playBuy)
    {
        this.host = host;
        this.placeholder = placeholder;
        this.furniture = furniture;
        this.slime = slime;
        this.confirm = confirm;
        this.playBuy = playBuy;
    }

    /// <summary>Mostra (constroi) ou esconde a categoria Mveis da loja.</summary>
    public void Show(bool visible)
    {
        if (host == null)
            return;

        if (!visible)
        {
            built = false;
            host.Clear();
            if (placeholder != null)
            {
                placeholder.RemoveFromClassList("hidden");
                host.Add(placeholder);
            }
            return;
        }

        host.Clear();
        if (placeholder != null)
            placeholder.AddToClassList("hidden");
        BuildRoomCard();
        BuildFurnitureCards();
        built = true;
        UpdateStates();
    }

    /// <summary>Atualiza textos e estados sem reconstruir a arvore.</summary>
    public void UpdateStates()
    {
        if (!built || host == null || host.panel == null)
            return;
        UpdateRoomCard();
        foreach (Card card in cards)
            UpdateCard(card);
    }

    private float GooNow => slime != null ? slime.Goo : 0f;

    // ---- Cartao do Comodo ----

    private void BuildRoomCard()
    {
        var card = new VisualElement();
        card.AddToClassList("room-card");

        roomTitle = new Label();
        roomTitle.AddToClassList("room-title");
        roomLine = new Label();
        roomLine.AddToClassList("room-line");
        roomUnlockLine = new Label();
        roomUnlockLine.AddToClassList("room-line");
        roomCost = new Label();
        roomCost.AddToClassList("food-price");

        roomButton = new Button(OnRoomUpgradeClicked) { text = "Evoluir cômodo" };
        roomButton.AddToClassList("room-btn");

        card.Add(roomTitle);
        card.Add(roomLine);
        card.Add(roomUnlockLine);
        card.Add(roomCost);
        card.Add(roomButton);
        host.Add(card);
    }

    private void UpdateRoomCard()
    {
        if (furniture == null || roomTitle == null)
            return;

        int comodo = furniture.ComodoLevel;
        roomTitle.text = $"Cômodo — Nv {comodo}";
        roomLine.text = $"Clique-base: {FormatGoo(BaseClickValue())} gosma por clique";

        // Proximo desbloqueio ainda nao alcancado.
        string nextUnlock = null;
        int nextLevel = -1;
        foreach (FurnitureKind kind in System.Enum.GetValues(typeof(FurnitureKind)))
        {
            int unlock = furniture.NextUnlockComodoLevel(kind);
            if (unlock > 0 && (nextLevel < 0 || unlock < nextLevel))
            {
                nextLevel = unlock;
                nextUnlock = FurnitureName(kind);
            }
        }
        roomUnlockLine.text = nextUnlock != null
            ? $"Próximo desbloqueio: {nextUnlock} (Cômodo Lv.{nextLevel})"
            : "Todos os móveis já foram desbloqueados";

        float cost = furniture.ComodoUpgradeCost();
        if (cost < 0f)
        {
            roomCost.text = "Nível máximo!";
            roomButton.text = "Máximo";
            roomButton.SetEnabled(false);
        }
        else
        {
            roomCost.text = $"Custo do próximo nível: {FormatGoo(cost)} gosma";
            bool affordable = GooNow >= cost;
            roomButton.text = affordable ? "Evoluir cômodo" : $"Faltam {FormatGoo(cost - GooNow)}";
            roomButton.SetEnabled(affordable);
        }
    }

    private void OnRoomUpgradeClicked()
    {
        if (furniture == null || confirm == null)
            return;

        float cost = furniture.ComodoUpgradeCost();
        if (cost < 0f)
            return;

        int next = furniture.ComodoLevel + 1;
        string unlockNote = "";
        foreach (FurnitureKind kind in System.Enum.GetValues(typeof(FurnitureKind)))
        {
            if (furniture.UnlockLevel(kind) == next)
                unlockNote = $"\nDesbloqueia: {FurnitureName(kind)}.";
        }

        confirm.Show(
            $"Evoluir o Cômodo para o Nv {next}?",
            $"Custo: {FormatGoo(cost)} gosma.{unlockNote}\n\n⚠️ Os níveis dos móveis serão zerados.",
            "Evoluir",
            () =>
            {
                if (furniture.TryUpgradeComodo() && playBuy != null)
                    playBuy();
                UpdateStates();
            });
    }

    // ---- Cartoes dos moveis ----

    private void BuildFurnitureCards()
    {
        foreach (FurnitureKind kind in System.Enum.GetValues(typeof(FurnitureKind)))
        {
            var card = new VisualElement();
            card.AddToClassList("food-card");

            var img = new Label(FurnitureEmoji(kind));
            img.AddToClassList("food-img");

            var info = new VisualElement();
            info.AddToClassList("food-info");

            var level = new Label();
            level.AddToClassList("food-name");
            var effect = new Label();
            effect.AddToClassList("card-effect");
            var price = new Label();
            price.AddToClassList("food-price");

            info.Add(level);
            info.Add(effect);
            info.Add(price);

            var data = new Card
            {
                kind = kind,
                name = FurnitureName(kind),
                root = card,
                level = level,
                effect = effect,
                price = price,
            };
            int captured = (int)kind;
            data.buy = new Button(() => OnFurnitureBuyClicked((FurnitureKind)captured)) { text = "Evoluir" };
            data.buy.AddToClassList("buy-btn");
            cards.Add(data);

            card.Add(img);
            card.Add(info);
            card.Add(data.buy);
            host.Add(card);
        }
    }

    private void UpdateCard(Card card)
    {
        if (furniture == null)
            return;

        int level = furniture.FurnitureLevel(card.kind);
        int max = furniture.FurnitureMaxLevel(card.kind);
        int unlock = furniture.UnlockLevel(card.kind);

        if (max <= 0)
        {
            card.level.text = $"{card.name} — bloqueado";
            card.effect.text = $"Desbloqueia no Cômodo Lv.{unlock}";
            card.price.text = "";
            card.buy.text = "Bloqueado";
            card.buy.SetEnabled(false);
            card.root.EnableInClassList("card-locked", true);
            return;
        }

        card.root.EnableInClassList("card-locked", false);
        card.level.text = $"{card.name} — Nv {level} / {max}";
        card.effect.text = BuildEffectText(card.kind, level);

        float cost = furniture.FurnitureUpgradeCost(card.kind);
        if (cost < 0f)
        {
            card.price.text = "";
            card.buy.text = "Máximo";
            card.buy.SetEnabled(false);
            return;
        }

        card.price.text = $"Custo: {FormatGoo(cost)} gosma";
        if (GooNow >= cost)
        {
            card.buy.text = "Evoluir";
            card.buy.SetEnabled(true);
        }
        else
        {
            card.buy.text = "Sem gosma";
            card.buy.SetEnabled(false);
        }
    }

    private void OnFurnitureBuyClicked(FurnitureKind kind)
    {
        if (furniture == null || confirm == null)
            return;

        float cost = furniture.FurnitureUpgradeCost(kind);
        if (cost < 0f)
            return;

        string name = FurnitureName(kind);
        int next = furniture.FurnitureLevel(kind) + 1;
        confirm.Show(
            $"Evoluir {name}?",
            $"Passar para o Nv {next} por {FormatGoo(cost)} gosma?",
            "Evoluir",
            () =>
            {
                if (furniture.TryUpgradeFurniture(kind) && playBuy != null)
                    playBuy();
                UpdateStates();
            });
    }

    // ---- Helpers publicos (compartilhados com o popup da tela principal) ----

    public static string FurnitureName(FurnitureKind kind)
    {
        switch (kind)
        {
            case FurnitureKind.Bed: return "Cama";
            case FurnitureKind.Wardrobe: return "Guarda-roupas";
            default: return "Cômoda";
        }
    }

    public static string FurnitureEmoji(FurnitureKind kind)
    {
        switch (kind)
        {
            case FurnitureKind.Bed: return "🛏";
            case FurnitureKind.Wardrobe: return "👕";
            default: return "🪞";
        }
    }

    /// <summary>Texto do efeito atual (e proximo nivel, quando existir).</summary>
    public static string BuildEffectText(FurnitureKind kind, int level)
    {
        string current = EffectAt(kind, level);
        if (level <= 0)
            return $"Próximo nível: {EffectAt(kind, 1)}";
        return $"{current}\nPróximo: {EffectAt(kind, level + 1)}";
    }

    private static string EffectAt(FurnitureKind kind, int level)
    {
        switch (kind)
        {
            case FurnitureKind.Bed:
                return $"{FormatGoo(FurnitureManager.BedGooRate(level))} gosma/s";
            case FurnitureKind.Wardrobe:
                return $"+{FormatPercent(FurnitureManager.WardrobeBonusAt(level))} por clique";
            default:
                float chance = FurnitureManager.DresserCritChanceAt(level) * 100f;
                return $"{chance.ToString("0.##")}% de chance crítica";
        }
    }

    public static string FormatGoo(double value)
    {
        if (double.IsNaN(value) || value < 0d)
            return "—";
        if (value >= 9e18d)
            return value.ToString("0.###E+0");
        return ((long)value).ToString("N0");
    }

    public static string FormatPercent(double fraction)
    {
        double pct = fraction * 100d;
        if (double.IsNaN(pct) || pct < 0d)
            return "0%";
        if (pct >= 1e6d)
            return $"{pct.ToString("0.###E+0")}%";
        if (pct >= 100d)
            return $"{pct.ToString("N0")}%";
        return $"{pct.ToString("N1")}%";
    }

    private float BaseClickValue()
    {
        float baseClick = slime != null ? slime.BaseGooPerClick : 1f;
        return baseClick * (furniture != null ? furniture.ComodoLevel : 1f);
    }

    /// <summary>Mapeia o nome usado nos botoes da tela principal para o tipo.</summary>
    public static FurnitureKind KindFromName(string furnitureName)
    {
        switch (furnitureName)
        {
            case "Cama": return FurnitureKind.Bed;
            case "Guarda-roupas": return FurnitureKind.Wardrobe;
            default: return FurnitureKind.Dresser;
        }
    }
}
