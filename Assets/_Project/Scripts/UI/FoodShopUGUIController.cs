using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loja de comida do MainScene (UGUI), adaptada da versao UI Toolkit do
/// TesteDanillo, SEM raridade: cada comida vende um card so, usando a
/// variante do meio (tiers[1]) de preco/efeito. Monta os cards em runtime
/// a partir do FoodCardPrefab que ja existe na cena (nenhuma edicao de
/// cena) e toda compra pede confirmacao no PopUpConfirm (GDD/TP3).
/// </summary>
public class FoodShopUGUIController : MonoBehaviour
{
    /// <summary>Um card da loja: comida, variante vendida e botao.</summary>
    private sealed class FoodCard
    {
        public FoodItemData Food;
        public FoodTier Tier;
        public Button Buy;
    }

    private readonly List<FoodCard> cards = new();

    /// <summary>
    /// Liga a loja de comida do scene em runtime. Idempotente: pode ser
    /// chamada a cada Start sem duplicar cards nem listeners.
    /// </summary>
    public static void Install()
    {
        FoodShopUGUIController existing = FindFirstObjectByType<FoodShopUGUIController>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.Build();
            return;
        }

        VerticalNavegationController nav =
            FindFirstObjectByType<VerticalNavegationController>();
        Transform panel = nav != null ? FindByName(nav.transform, "FoodUI") : null;
        if (panel == null)
        {
            Debug.LogWarning("[FoodShop] Painel FoodUI nao encontrado no scene.");
            return;
        }

        panel.gameObject.AddComponent<FoodShopUGUIController>().Build();
    }

    private void Update()
    {
        // O componente so roda com o painel aberto: reavalia Fome e saldo
        // enquanto o jogador esta na loja (sem depender de eventos).
        RefreshButtons();
    }

    // =========================================
    // BUILD (cards em runtime)
    // =========================================

    private void Build()
    {
        Transform container = transform.Find("StickerScrollPainel/StickerCardPainel");
        if (container == null)
        {
            Debug.LogWarning("[FoodShop] Container de cards (StickerCardPainel) nao encontrado.");
            return;
        }

        // Uma entrada por comida (sem raridade): a variante do meio, ou a
        // unica existente quando a comida so tem uma.
        var entries = new List<(FoodItemData food, FoodTier tier)>();
        FoodItemData[] all = Resources.LoadAll<FoodItemData>("Food");
        System.Array.Sort(all, (a, b) => a.order.CompareTo(b.order));
        foreach (FoodItemData food in all)
        {
            if (food.tiers == null || food.tiers.Length == 0)
                continue;
            FoodTier tier = food.tiers.Length > 1 ? food.tiers[1] : food.tiers[0];
            entries.Add((food, tier));
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning("[FoodShop] Nenhuma comida em Resources/Food.");
            return;
        }

        // Reaproveita os cards que a cena ja traz (7 slots) e clona o
        // primeiro quando faltam (10 comidas). O GridLayoutGroup do
        // container reposiciona tudo sozinho.
        var cardTransforms = new List<Transform>();
        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child.name.StartsWith("FoodCardPrefab"))
                cardTransforms.Add(child);
        }

        if (cardTransforms.Count == 0)
        {
            Debug.LogWarning("[FoodShop] FoodCardPrefab nao encontrado no container.");
            return;
        }

        Transform template = cardTransforms[0];
        while (cardTransforms.Count < entries.Count)
        {
            Transform clone = Instantiate(template, container);
            clone.name = $"FoodCardPrefab ({cardTransforms.Count})";
            cardTransforms.Add(clone);
        }
        while (cardTransforms.Count > entries.Count)
        {
            Transform extra = cardTransforms[cardTransforms.Count - 1];
            cardTransforms.RemoveAt(cardTransforms.Count - 1);
            Destroy(extra.gameObject);
        }

        cards.Clear();
        for (int i = 0; i < entries.Count; i++)
            Bind(cardTransforms[i], entries[i].food, entries[i].tier, i);

        RefreshButtons();
    }

    private void Bind(Transform card, FoodItemData food, FoodTier tier, int index)
    {
        SetText(card, "CardTittle", food.foodName);
        SetText(card, "HungerInfo",
            $"{Mathf.RoundToInt(tier.hungerRestore * 100f)}%");
        SetText(card, "Text (TMP)", tier.price.ToString());

        // O prefab ja traz um FoodIcon (Image). Se o ScriptableObject tiver
        // sprite definido, troca o sprite Padrao pelo da comida desta card.
        Transform iconTransform = FindByName(card, "FoodIcon");
        Image foodIcon = iconTransform != null
            ? iconTransform.GetComponent<Image>()
            : null;
        if (foodIcon != null && food.icon != null)
            foodIcon.sprite = food.icon;

        Transform buyTransform = FindByName(card, "BuyBtt");
        Button buy = buyTransform != null
            ? buyTransform.GetComponent<Button>()
            : null;
        if (buy != null)
        {
            // RemoveAllListeners mantem o Bind re-executavel sem duplicar.
            buy.onClick.RemoveAllListeners();
            buy.onClick.AddListener(() => OnBuy(index));
        }

        cards.Add(new FoodCard { Food = food, Tier = tier, Buy = buy });
    }

    // =========================================
    // COMPRA (confirmada, GDD/TP3)
    // =========================================

    private void OnBuy(int index)
    {
        if (index < 0 || index >= cards.Count)
            return;

        FoodCard card = cards[index];

        FoodBonusManager bonus = FoodBonusManager.Instance;
        if (bonus == null || !bonus.CanEat)
        {
            // Fome cheia: o botao ja fica desativado, so retranca.
            GameAudioManager.Instance?.PlayClose();
            return;
        }

        void DoBuy()
        {
            if (FoodBonusManager.Instance.TryEat(card.Food, card.Tier))
                GameAudioManager.Instance?.PlayBuy();
            else
                GameAudioManager.Instance?.PlayClose();
            RefreshButtons();
        }

        // Toda compra pede confirmacao (mesmo fluxo dos moveis).
        PopupController popup = PopupController.Instance;
        if (popup != null)
        {
            int hungerPercent = Mathf.RoundToInt(card.Tier.hungerRestore * 100f);
            popup.ShowConfirm(
                card.Food.foodName,
                $"{card.Tier.Describe()}\nRecupera {hungerPercent}% da Fome\n" +
                $"Por {card.Tier.price} gosma?",
                "Comprar",
                DoBuy,
                card.Food.icon);
        }
        else
        {
            DoBuy();
        }
    }

    /// <summary>
    /// Habilita o comprar so quando da para comer (TP3: Fome abaixo de
    /// 100%) e para pagar (GDD: so com gosma suficiente).
    /// </summary>
    private void RefreshButtons()
    {
        FoodBonusManager bonus = FoodBonusManager.Instance;
        bool canEat = bonus != null && bonus.CanEat;

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        float goo = save != null
            ? save.goo
            : SlimeManager.Instance != null ? SlimeManager.Instance.Goo : 0f;

        foreach (FoodCard card in cards)
        {
            if (card.Buy != null)
                card.Buy.interactable = canEat && goo >= card.Tier.price;
        }
    }

    // =========================================
    // HELPERS
    // =========================================

    private static void SetText(Transform root, string childName, string value)
    {
        Transform found = FindByName(root, childName);
        TMP_Text label = found != null ? found.GetComponent<TMP_Text>() : null;
        if (label != null)
            label.text = value;
    }

    private static Transform FindByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }
}
