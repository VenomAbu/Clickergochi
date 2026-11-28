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
    private readonly List<Transform> rows = new();
    private readonly List<RoomTab> roomTabs = new();
    private SkinRoom currentRoom = SkinRoom.Bedroom;
    private bool built;

    private enum SkinRoom
    {
        Bedroom,
        Bathroom,
        Kitchen
    }

    private sealed class RoomTab
    {
        public SkinRoom Room;
        public Button Button;
        public RectTransform ButtonTransform;
        public UIFeelGeral Feel;
        public GameObject SelectPoint;
        public int[] RowIndices;
    }

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

    private void OnEnable()
    {
        // Cada vez que a loja abre, comeca pelo quarto (default pedido).
        if (built)
        {
            currentRoom = SkinRoom.Bedroom;
            SelectRoom(currentRoom);
        }
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
        rows.Clear();
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

        // Ordem pedida para os itens de cada comodo:
        // Quarto: Guarda-roupa, Cama, Escrivaninha
        // Banheiro: Vaso, Pia, Banheira
        // Cozinha: Micro-ondas, Geladeira, Mesa
        var kinds = new[]
        {
            FurnitureKind.Wardrobe,
            FurnitureKind.Bed,
            FurnitureKind.Dresser,
            FurnitureKind.Vaso,
            FurnitureKind.Pia,
            FurnitureKind.Banheira,
            FurnitureKind.Microondas,
            FurnitureKind.Geladeira,
            FurnitureKind.Mesa
        };

        slots.Clear();
        for (int r = 0; r < rows.Count && r < kinds.Length; r++)
            BindRow(rows[r], kinds[r], hub);

        BuildRoomTabs();
        built = true;
        SelectRoom(SkinRoom.Bedroom);
        RefreshButtons();
    }

    // =========================================
    // ROOM SELECTOR (Quarto / Banheiro / Cozinha)
    // =========================================

    private void BuildRoomTabs()
    {
        roomTabs.Clear();

        Transform selector = transform.Find("RoomSelector");
        Transform points = transform.Find("SelectPoins");
        if (selector == null || points == null)
        {
            Debug.LogWarning(
                "[SkinShop] RoomSelector/SelectPoins nao encontrados.", this);
            return;
        }

        AddRoomTab(selector, points, SkinRoom.Bedroom,
            "BttRoonSelect (Bedroon)", "SelectPoin (Bedroon)", 0, 1, 2);
        AddRoomTab(selector, points, SkinRoom.Bathroom,
            "BttRoonSelect (Bathroon)", "SelectPoin (Bathroon)", 3, 4, 5);
        AddRoomTab(selector, points, SkinRoom.Kitchen,
            "BttRoonSelect (Kitchen)", "SelectPoin (Kitchen)", 6, 7, 8);
    }

    private void AddRoomTab(
        Transform selector,
        Transform points,
        SkinRoom room,
        string buttonName,
        string pointName,
        params int[] rowIndices)
    {
        Transform buttonTransform = FindDirect(selector, buttonName);
        Button button = buttonTransform != null
            ? buttonTransform.GetComponent<Button>()
            : null;
        GameObject selectPoint = FindDirect(points, pointName)?.gameObject;

        if (button == null || selectPoint == null)
        {
            Debug.LogWarning(
                $"[SkinShop] Botao/ponto do comodo '{room}' nao encontrado.", this);
            return;
        }

        UIFeelGeral feel = button.GetComponent<UIFeelGeral>();
        if (feel == null)
            feel = button.gameObject.AddComponent<UIFeelGeral>();

        SkinRoom captured = room;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            GameAudioManager.Instance?.PlayClick();
            // Seleciona primeiro para a animacao usar a escala final do botao.
            SelectRoom(captured);
            feel.PlayStretchAndSquash();
        });

        roomTabs.Add(new RoomTab
        {
            Room = room,
            Button = button,
            ButtonTransform = buttonTransform as RectTransform,
            Feel = feel,
            SelectPoint = selectPoint,
            RowIndices = rowIndices
        });
    }

    private void SelectRoom(SkinRoom room)
    {
        currentRoom = room;

        // Sem abas montadas, preserva o comportamento antigo (tudo visivel).
        if (roomTabs.Count == 0)
        {
            foreach (Transform row in rows)
                row.gameObject.SetActive(true);
            return;
        }

        foreach (Transform row in rows)
            row.gameObject.SetActive(false);

        foreach (RoomTab tab in roomTabs)
        {
            bool selected = tab.Room == room;
            Vector3 targetScale = selected ? Vector3.one * 1.15f : Vector3.one;
            if (tab.Feel != null)
            {
                tab.Feel.SetBaseScale(targetScale);
            }
            else if (tab.ButtonTransform != null)
            {
                tab.ButtonTransform.localScale = targetScale;
            }
            if (tab.SelectPoint != null)
                tab.SelectPoint.SetActive(selected);

            if (!selected || tab.RowIndices == null)
                continue;

            foreach (int index in tab.RowIndices)
            {
                if (index >= 0 && index < rows.Count)
                    rows[index].gameObject.SetActive(true);
            }
        }

        // O jogador sempre cai no topo da lista do comodo escolhido.
        ScrollRect scroll = transform.Find("StickerScrollPainel")
            ?.GetComponent<ScrollRect>();
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;

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
        {
            // A cena chamava o Dresser de "Comoda"; nesta loja o nome pedido
            // e "Escrivaninha". As outras linhas seguem o nome do GDD.
            titleTMP.text = kind == FurnitureKind.Dresser
                ? "Escrivaninha"
                : FurnitureShopUI.FurnitureName(kind);
        }

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
                // Mesmo ajuste dos demais moveis: a sprite nunca distorce.
                // A primeira linha da cena vinha com preserveAspect desligado.
                icon.preserveAspect = true;
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
                GameAudioManager.Instance?.PlayClose();
                RefreshButtons();
                return;
            }

            // Comprada: som de confirmacao de compra; EquipSkin fica silencioso.
            GameAudioManager.Instance?.PlayPurchase();
            hub.EquipSkin(kind, index, false);
            RefreshButtons();
        }

        // Toda compra pede confirmacao (mesmo fluxo da comida e moveis).
        PopupController popup = PopupController.Instance;
        if (popup != null)
        {
            popup.ShowConfirm(
                skin.skinName,
                $"Skin de {FurnitureShopUI.FurnitureName(kind)}\n" +
                $"Por {skin.skinPrice} gosma?",
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
