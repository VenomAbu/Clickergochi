using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loja de skins dos moveis no MainScene (UGUI). Cada FurnitureContainer
/// da cena vira a linha de um movel (3 slots = Padrão + 2 variantes);
/// a compra pede confirmacao (GDD/TP3) e sai gosma, e depois de comprada
/// o slot vira botao de equipar — o carrossel do popup usa a mesma trava
/// de posse. Montada em runtime a partir dos cards que a cena ja traz,
/// sem edicao de cena.
/// </summary>
public class SkinShopUGUIController : MonoBehaviour
{
    /// <summary>Um slot da loja: skin, icone, botao e rotulo de estado.</summary>
    private sealed class SkinSlot
    {
        public FurnitureKind Kind;
        public int Index;
        public FurnitureSkinData Skin;
        public Image Icon;
        public Button Button;
        public TMP_Text Label;
    }

    private readonly List<SkinSlot> slots = new();

    /// <summary>Cor dos slots de skin ainda nao comprada (travada).</summary>
    private static readonly Color LockedColor =
        new Color(0.42f, 0.42f, 0.42f, 1f);

    /// <summary>
    /// Liga a loja de skins do scene em runtime. Idempotente: pode ser
    /// chamada a cada Start sem duplicar cards nem listeners.
    /// </summary>
    public static void Install()
    {
        SkinShopUGUIController existing =
            FindFirstObjectByType<SkinShopUGUIController>(
                FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.Build();
            return;
        }

        Transform panel = FindPanel("FurnitureSkinUI");
        if (panel == null)
        {
            Debug.LogWarning(
                "[SkinShop] Painel FurnitureSkinUI nao encontrado no scene.");
            return;
        }

        panel.gameObject.AddComponent<SkinShopUGUIController>().Build();
    }

    private void Update()
    {
        // O componente so roda com o painel aberto: reavalia saldo,
        // compra e equip enquanto o jogador esta na loja.
        RefreshButtons();
    }

    // =========================================
    // BUILD (linhas e slots em runtime)
    // =========================================

    private void Build()
    {
        Transform container =
            transform.Find("StickerScrollPainel/StickerCardPainel");
        if (container == null)
        {
            Debug.LogWarning(
                "[SkinShop] Container de cards (StickerCardPainel) nao encontrado.");
            return;
        }

        // Titulo do painel em PT (a cena traz "Furniture Skin").
        Transform title = transform.Find("StickerAlbumTittle");
        TMP_Text titleTMP = title != null ? title.GetComponent<TMP_Text>() : null;
        if (titleTMP != null)
            titleTMP.text = "Skins dos M\u00f3veis";

        PopupController hub = PopupController.Instance;
        if (hub == null)
        {
            Debug.LogWarning("[SkinShop] PopupController indisponivel.");
            return;
        }

        // Uma linha por movel, na ordem dos filhos do grid da cena.
        var rows = new List<Transform>();
        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child.name.StartsWith("FurnitureContainer"))
                rows.Add(child);
        }

        if (rows.Count == 0)
        {
            Debug.LogWarning(
                "[SkinShop] Nenhum FurnitureContainer no container.");
            return;
        }

        var kinds = new[]
        {
            FurnitureKind.Bed,
            FurnitureKind.Wardrobe,
            FurnitureKind.Dresser
        };

        slots.Clear();
        for (int r = 0; r < rows.Count && r < kinds.Length; r++)
            BindRow(rows[r], kinds[r], hub);

        RefreshButtons();
    }

    private void BindRow(Transform row, FurnitureKind kind, PopupController hub)
    {
        // Titulo da linha = nome do movel.
        Transform titleTransform = FindDirect(row, "TittleContainer");
        TMP_Text titleTMP = titleTransform != null
            ? titleTransform.GetComponent<TMP_Text>()
            : null;
        if (titleTMP != null)
            titleTMP.text = FurnitureShopUI.FurnitureName(kind);

        List<FurnitureSkinData> skins = hub.ResolveSkins(kind);

        // Slots na ordem dos filhos (FurnitureCard, (1), (2)).
        var cards = new List<Transform>();
        for (int i = 0; i < row.childCount; i++)
        {
            Transform child = row.GetChild(i);
            if (child.name.StartsWith("FurnitureCard"))
                cards.Add(child);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            // Slot sobrando (lista menor) some; lista maior avisa o artista.
            if (i >= skins.Count || skins[i] == null)
            {
                cards[i].gameObject.SetActive(false);
                continue;
            }

            FurnitureSkinData skin = skins[i];

            Transform iconTransform = FindDirect(cards[i], "Image");
            Image icon = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : null;
            if (icon != null)
            {
                // O clique precisa chegar ao botao do slot.
                icon.raycastTarget = false;
                icon.sprite = skin.skinImage;
            }

            Transform buttonTransform = FindDirect(cards[i], "Button");
            Button button = buttonTransform != null
                ? buttonTransform.GetComponent<Button>()
                : null;
            TMP_Text label = buttonTransform != null
                ? buttonTransform.GetComponentInChildren<TMP_Text>(true)
                : null;

            if (button != null)
            {
                int captured = i;
                FurnitureKind capturedKind = kind;
                // RemoveAllListeners mantem o Bind re-executavel sem duplicar.
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnSlot(capturedKind, captured));
            }

            slots.Add(new SkinSlot
            {
                Kind = kind,
                Index = i,
                Skin = skin,
                Icon = icon,
                Button = button,
                Label = label
            });
        }

        if (skins.Count > cards.Count)
        {
            Debug.LogWarning(
                $"[SkinShop] {FurnitureShopUI.FurnitureName(kind)} tem " +
                $"{skins.Count} skins e so {cards.Count} slots na cena.");
        }
    }

    // =========================================
    // COMPRA / EQUIPE (confirmada, GDD/TP3)
    // =========================================

    private void OnSlot(FurnitureKind kind, int index)
    {
        PopupController hub = PopupController.Instance;
        if (hub == null)
            return;

        List<FurnitureSkinData> skins = hub.ResolveSkins(kind);
        if (index < 0 || index >= skins.Count || skins[index] == null)
            return;

        FurnitureSkinData skin = skins[index];

        // Ja comprou: equipa direto (a trava de posse fica no EquipSkin).
        if (PopupController.IsSkinOwned(skin))
        {
            hub.EquipSkin(kind, index);
            RefreshButtons();
            return;
        }

        void DoBuy()
        {
            if (!PopupController.BuySkin(skin))
            {
                // Sem gosma ou ja dona: retranca.
                MainHUDUGUIController.Instance?.PlayClose();
                RefreshButtons();
                return;
            }

            // Comprada: equipa na hora (o EquipSkin toca o som de compra).
            hub.EquipSkin(kind, index);
            RefreshButtons();
        }

        // Toda compra pede confirmacao (mesmo fluxo da comida e moveis).
        ConfirmPopupUGUI confirm = ConfirmPopupUGUI.Instance;
        if (confirm != null)
        {
            confirm.Show(
                skin.skinName,
                $"Skin de {FurnitureShopUI.FurnitureName(kind)}\n" +
                $"Por {skin.skinPrice} gosma?",
                "Comprar",
                DoBuy);
        }
        else
        {
            DoBuy();
        }
    }

    /// <summary>
    /// Rotulo e estado por slot: bloqueado mostra o preco (so habilita com
    /// gosma); dono vira "Equipar"; equipada fica "Equipada" travada.
    /// </summary>
    private void RefreshButtons()
    {
        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null ? manager.GetSaveData() : null;
        float goo = save != null
            ? save.goo
            : SlimeManager.Instance != null ? SlimeManager.Instance.Goo : 0f;

        foreach (SkinSlot slot in slots)
        {
            if (slot.Skin == null)
                continue;

            bool owned = PopupController.IsSkinOwned(slot.Skin);
            bool equipped = owned && save != null &&
                save.furnitureSkinIndex != null &&
                (int)slot.Kind < save.furnitureSkinIndex.Length &&
                save.furnitureSkinIndex[(int)slot.Kind] == slot.Index;

            if (slot.Label != null)
            {
                slot.Label.text = owned
                    ? equipped ? "Equipada" : "Equipar"
                    : slot.Skin.skinPrice.ToString();
            }

            if (slot.Button != null)
            {
                slot.Button.interactable = owned
                    ? !equipped
                    : goo >= slot.Skin.skinPrice;
            }

            if (slot.Icon != null)
                slot.Icon.color = owned ? Color.white : LockedColor;
        }
    }

    // =========================================
    // HELPERS
    // =========================================

    /// <summary>Filho direto com o nome exato (nao desce a arvore).</summary>
    private static Transform FindDirect(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child;
        }
        return null;
    }

    /// <summary>Acha a panel inativa por nome em qualquer lugar da cena.</summary>
    private static Transform FindPanel(string name)
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
