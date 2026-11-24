using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loja de skins do slime no MainScene (UGUI). Os modelos 3D ficam
/// empilhados em Slimes3D (so o equipado fica ativo) e os cards do
/// SlimeCardPainel (SlimeSkinCardPrefab) viram compra confirmada +
/// equipar, no mesmo fluxo da SkinShopUGUIController. Catalogo em
/// Resources/SlimeSkins (SlimeSkinData, ordenado por order); montada em
/// runtime, sem edicao de cena.
/// </summary>
public class SlimeSkinShopUGUIController : MonoBehaviour
{
    /// <summary>Um slot da loja: skin, icone, botao e rotulo de estado.</summary>
    private sealed class SkinSlot
    {
        public SlimeSkinData Skin;
        public Image Icon;
        public Button Button;
        public TMP_Text Label;
    }

    private readonly List<SkinSlot> slots = new();

    /// <summary>Cor dos cards de skin ainda nao comprada (travada).</summary>
    private static readonly Color LockedColor =
        new Color(0.42f, 0.42f, 0.42f, 1f);

    private static SlimeSkinData[] catalogCache;

    /// <summary>
    /// Liga a loja de skins do slime em runtime e aplica a skin salva.
    /// Idempotente: pode ser chamada a cada Start sem duplicar cards.
    /// </summary>
    public static void Install()
    {
        SlimeSkinShopUGUIController existing =
            FindFirstObjectByType<SlimeSkinShopUGUIController>(
                FindObjectsInactive.Include);
        if (existing == null)
        {
            Transform panel = FindAnywhere("SlimeSkinUI");
            if (panel == null)
            {
                Debug.LogWarning(
                    "[SlimeSkinShop] Painel SlimeSkinUI nao encontrado no scene.");
                return;
            }
            existing = panel.gameObject.AddComponent<SlimeSkinShopUGUIController>();
        }

        existing.Build();
        ApplyEquipped();
    }

    private void Update()
    {
        // So roda com o painel aberto: reavalia saldo, compra e equip.
        RefreshButtons();
    }

    // =========================================
    // BUILD (cards da cena)
    // =========================================

    private void Build()
    {
        Transform container = transform.Find("SlimeScrollPainel/SlimeCardPainel");
        if (container == null)
        {
            Debug.LogWarning(
                "[SlimeSkinShop] Container de cards (SlimeCardPainel) nao encontrado.");
            return;
        }

        Transform title = transform.Find("StickerAlbumTittle");
        TMP_Text titleTMP = title != null ? title.GetComponent<TMP_Text>() : null;
        if (titleTMP != null)
            titleTMP.text = "Skins dos Slimes";

        SlimeSkinData[] catalog = LoadCatalog();
        if (catalog.Length == 0)
        {
            Debug.LogWarning(
                "[SlimeSkinShop] Nenhum SlimeSkinData em Resources/SlimeSkins.");
            return;
        }

        slots.Clear();
        for (int i = 0; i < container.childCount; i++)
        {
            Transform card = container.GetChild(i);

            // Slot sobrando (lista menor) some; lista maior avisa.
            if (i >= catalog.Length || catalog[i] == null)
            {
                card.gameObject.SetActive(false);
                continue;
            }

            BindCard(card, catalog[i]);
        }

        if (container.childCount < catalog.Length)
        {
            Debug.LogWarning(
                $"[SlimeSkinShop] {catalog.Length} skins e so " +
                $"{container.childCount} slots na cena.");
        }

        RefreshButtons();
    }

    private void BindCard(Transform card, SlimeSkinData skin)
    {
        Image icon = FindDeep<Image>(card, "SkinImage");
        if (icon != null)
        {
            // O clique precisa chegar ao botao do card.
            icon.raycastTarget = false;
            if (skin.skinImage != null)
                icon.sprite = skin.skinImage;
        }

        TMP_Text cardTitle = FindDeep<TMP_Text>(card, "CardTittle");
        if (cardTitle != null)
            cardTitle.text = skin.skinName;

        Button button = FindDeep<Button>(card, "BuyBtt");
        TMP_Text label = button != null
            ? button.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (label == null)
            label = FindDeep<TMP_Text>(card, "Text (TMP)");

        if (button != null)
        {
            SlimeSkinData captured = skin;
            // RemoveAllListeners mantem o Bind re-executavel sem duplicar.
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSlot(captured));
        }

        slots.Add(new SkinSlot
        {
            Skin = skin,
            Icon = icon,
            Button = button,
            Label = label
        });
    }

    // =========================================
    // COMPRA / EQUIPE (confirmada, GDD/TP3)
    // =========================================

    private void OnSlot(SlimeSkinData skin)
    {
        if (skin == null)
            return;

        // Ja comprou (ou e o padrao): equipa direto.
        if (IsOwned(skin))
        {
            Equip(skin);
            RefreshButtons();
            return;
        }

        void DoBuy()
        {
            if (!Buy(skin))
            {
                // Sem gosma ou ja dona: retranca.
                GameAudioManager.Instance?.PlayClose();
                RefreshButtons();
                return;
            }

            // Comprada: equipa na hora (o Equip toca o som de compra).
            Equip(skin);
            RefreshButtons();
        }

        // Toda compra pede confirmacao (mesmo fluxo da comida e moveis).
        PopupController popup = PopupController.Instance;
        if (popup != null)
        {
            popup.ShowConfirm(
                skin.skinName,
                $"Skin de Slime\nPor {skin.skinPrice} gosma?",
                "Comprar",
                DoBuy,
                skin.skinImage);
        }
        else
        {
            DoBuy();
        }
    }

    /// <summary>
    /// Rotulo e estado por card: bloqueado mostra o preco (so habilita com
    /// gosma); dono vira "Equipar"; equipada fica "Equipada" travada.
    /// </summary>
    private void RefreshButtons()
    {
        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        float goo = save != null
            ? save.goo
            : SlimeManager.Instance != null ? SlimeManager.Instance.Goo : 0f;

        SlimeSkinData[] catalog = LoadCatalog();
        SlimeSkinData equipped = ResolveEquipped(catalog, save);

        foreach (SkinSlot slot in slots)
        {
            if (slot.Skin == null)
                continue;

            bool owned = IsOwned(slot.Skin);
            bool isEquipped = owned && slot.Skin == equipped;

            if (slot.Label != null)
            {
                slot.Label.text = owned
                    ? isEquipped ? "Equipada" : "Equipar"
                    : slot.Skin.skinPrice.ToString();
            }

            if (slot.Button != null)
            {
                slot.Button.interactable = owned
                    ? !isEquipped
                    : goo >= slot.Skin.skinPrice;
            }

            if (slot.Icon != null)
                slot.Icon.color = owned ? Color.white : LockedColor;
        }
    }

    // =========================================
    // POSSE / SAVE / APLICACAO
    // =========================================

    /// <summary>
    /// Padrao (preco 0 ou sem id) e sempre livre; as demais precisam do
    /// id gravado em SaveData.ownedSlimeSkins.
    /// </summary>
    public static bool IsOwned(SlimeSkinData skin)
    {
        if (skin == null)
            return false;
        if (skin.skinPrice <= 0 || string.IsNullOrEmpty(skin.skinId))
            return true;

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        return save != null && save.ownedSlimeSkins != null &&
               save.ownedSlimeSkins.Contains(skin.skinId);
    }

    /// <summary>
    /// Compra a skin com gosma (SlimeManager.SpendGoo valida o saldo e
    /// grava). Retorna false se ja for dono ou nao tiver gosma.
    /// </summary>
    private static bool Buy(SlimeSkinData skin)
    {
        if (skin == null || skin.skinPrice <= 0 ||
            string.IsNullOrEmpty(skin.skinId) || IsOwned(skin))
            return false;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null || !slime.SpendGoo(skin.skinPrice))
            return false;

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        if (save == null)
            return false;

        if (save.ownedSlimeSkins == null)
            save.ownedSlimeSkins = new List<string>();
        save.ownedSlimeSkins.Add(skin.skinId);
        manager.RequestSave();
        return true;
    }

    /// <summary>Grava a skin escolhida e ativa o modelo correspondente.</summary>
    private static void Equip(SlimeSkinData skin)
    {
        if (skin == null || !IsOwned(skin))
        {
            GameAudioManager.Instance?.PlayClose();
            return;
        }

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        if (save != null)
        {
            save.slimeSkinId = skin.skinId != null ? skin.skinId : "";
            manager.RequestSave();
        }

        ApplyEquipped();
        GameAudioManager.Instance?.PlayBuy();
    }

    /// <summary>
    /// Skin ativa: a gravada no save (se comprada); save antigo ou id
    /// invalido volta para o padrao. Ativa so o GameObject dela em
    /// Slimes3D e desativa os demais catalogados.
    /// </summary>
    public static void ApplyEquipped()
    {
        SlimeSkinData[] catalog = LoadCatalog();
        if (catalog.Length == 0)
            return;

        Transform root = FindAnywhere("Slimes3D");
        if (root == null)
        {
            Debug.LogWarning("[SlimeSkinShop] Slimes3D nao encontrado no scene.");
            return;
        }

        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        SlimeSkinData target = ResolveEquipped(catalog, save);

        // Alvo sem GameObject na cena (asset renomeado): fica no padrao.
        if (FindDirect(root, target.sceneObjectName) == null)
        {
            target = ResolveEquipped(catalog, null);
            if (FindDirect(root, target.sceneObjectName) == null)
                return;
        }

        for (int i = 0; i < catalog.Length; i++)
        {
            SlimeSkinData skin = catalog[i];
            if (skin == null || string.IsNullOrEmpty(skin.sceneObjectName))
                continue;

            GameObject go = FindDirect(root, skin.sceneObjectName);
            if (go != null)
                go.SetActive(skin == target);
        }
    }

    /// <summary>Resolve a skin equipada: id gravado (comprado) ou padrao.</summary>
    private static SlimeSkinData ResolveEquipped(
        SlimeSkinData[] catalog, SaveData save)
    {
        string equippedId = save != null && save.slimeSkinId != null
            ? save.slimeSkinId
            : "";

        if (!string.IsNullOrEmpty(equippedId))
        {
            for (int i = 0; i < catalog.Length; i++)
            {
                SlimeSkinData skin = catalog[i];
                // Save apontando para skin nao comprada (teste antigo):
                // o slime volta para o padrao ate comprar.
                if (skin != null && skin.skinId == equippedId && IsOwned(skin))
                    return skin;
            }
        }

        for (int i = 0; i < catalog.Length; i++)
        {
            SlimeSkinData skin = catalog[i];
            if (skin != null &&
                (skin.skinPrice <= 0 || string.IsNullOrEmpty(skin.skinId)))
                return skin;
        }

        return catalog[0];
    }

    // =========================================
    // HELPERS
    // =========================================

    /// <summary>Catalogo em Resources/SlimeSkins, ordenado por order.</summary>
    private static SlimeSkinData[] LoadCatalog()
    {
        if (catalogCache == null)
        {
            catalogCache = Resources.LoadAll<SlimeSkinData>("SlimeSkins");
            System.Array.Sort(catalogCache,
                (a, b) => a.order.CompareTo(b.order));
        }
        return catalogCache;
    }

    /// <summary>Filho direto com o nome exato (nao desce a arvore).</summary>
    private static GameObject FindDirect(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child.gameObject;
        }
        return null;
    }

    /// <summary>Componente T no objeto (ou descendente) com o nome exato.</summary>
    private static T FindDeep<T>(Transform root, string name)
        where T : Component
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root.GetComponent<T>();

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child.GetComponent<T>();

            T found = FindDeep<T>(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>Acha objeto inativo por nome em qualquer lugar da cena.</summary>
    private static Transform FindAnywhere(string name)
    {
        foreach (Transform t in FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }
}
