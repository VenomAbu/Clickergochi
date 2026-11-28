using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loja de comida do MainScene (UGUI), adaptada da versao UI Toolkit do
/// TesteDanillo, SEM raridade: cada comida vende um card so, usando a
/// variante do meio (tiers[1]) de preco/efeito. Monta os cards em runtime
/// a partir do FoodCardPrefab que ja existe na cena (nenhuma edicao de
/// cena), mostra o efeito/fome no Food Description TMP e compra direto
/// no clique, sem popup de confirmacao.
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

    private void OnEnable()
    {
        // Atualiza na hora em que o painel abre, antes do primeiro Update.
        RefreshButtons();
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
        Transform container = FindCardContainer();
        if (container == null)
        {
            Debug.LogWarning("[FoodShop] Container de cards (FoodCardPainel/StickerCardPainel) nao encontrado.");
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

    /// <summary>
    /// Localiza o grid de cards. A cena atual usa FoodScrollPainel/FoodCardPainel,
    /// mas versoes antigas usavam StickerScrollPainel/StickerCardPainel.
    /// </summary>
    private Transform FindCardContainer()
    {
        Transform container = transform.Find("StickerScrollPainel/StickerCardPainel");
        if (container != null)
            return container;

        container = transform.Find("FoodScrollPainel/FoodCardPainel");
        if (container != null)
            return container;

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<GridLayoutGroup>() == null)
                continue;
            for (int i = 0; i < t.childCount; i++)
            {
                if (t.GetChild(i).name.StartsWith("FoodCardPrefab"))
                    return t;
            }
        }

        return null;
    }

    private void Bind(Transform card, FoodItemData food, FoodTier tier, int index)
    {
        int hungerPercent = Mathf.RoundToInt(tier.hungerRestore * 100f);

        SetText(card, "CardTittle", food.foodName);
        SetText(card, "HungerInfo", $"{hungerPercent}%");
        SetText(card, "Text (TMP)", tier.price.ToString());

        // Antes o efeito so aparecia no popup de confirmacao. Agora o card
        // mostra os dados da comida direto no Food Description TMP.
        SetText(card, "Food Description TMP",
            $"{tier.Describe()}\nRecupera {hungerPercent}% da Fome");

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
    // COMPRA DIRETA (sem popup de confirmacao)
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
            RefreshButtons();
            return;
        }

        // Sem saldo o botao tambem ja fica desativado. A checagem aqui so
        // protege contra um clique vindo antes do RefreshButtons.
        SaveData save = SaveManager.Instance?.GetSaveData();
        float goo = save != null
            ? save.goo
            : SlimeManager.Instance != null ? SlimeManager.Instance.Goo : 0f;
        if (goo < card.Tier.price)
        {
            RefreshButtons();
            return;
        }

        if (bonus.TryEat(card.Food, card.Tier))
            GameAudioManager.Instance?.PlayPurchase();
        else
            GameAudioManager.Instance?.PlayClose();

        RefreshButtons();
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
