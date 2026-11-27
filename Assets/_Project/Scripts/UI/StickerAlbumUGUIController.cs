using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Album de stickers do MainScene (UGUI). Liga os StickerData de
/// Resources/Stickers aos StickerCardPrefb que a cena ja traz dentro de
/// StickerCardPainel (painel AlbumUI), na ordem do grid. O card fica liberado
/// quando o sticker esta no save (StickerCollection), incluindo os ganhos ao
/// abrir pacotes. Clique abre o popup do sticker.
/// </summary>
public class StickerAlbumUGUIController : MonoBehaviour
{
    [Header("Album")]
    [Tooltip("Sprite RedCircle usado nas bolinhas de sticker novo.")]
    [SerializeField] private Sprite unseenDotSprite;

    /// <summary>Stickers do album, na ordem exibida (para o clique).</summary>
    private StickerData[] entries;

    /// <summary>
    /// O Album fica inativo na cena, entao OnEnable nao dispara no Install.
    /// A inscricao e feita no Build para acompanhar ganhos mesmo fechado.
    /// </summary>
    private bool subscribedToCollection;

    /// <summary>Acompanha tambem quando um sticker e marcado como visto.</summary>
    private bool subscribedToUnseen;

    /// <summary>Cor dos cards bloqueados no album (mesma das skins travadas).</summary>
    private static readonly Color LockedColor =
        new Color(0.42f, 0.42f, 0.42f, 1f);

    /// <summary>
    /// Liga o album de stickers do scene em runtime. Idempotente: pode ser
    /// chamada a cada Start sem duplicar cards nem listeners.
    /// </summary>
    public static void Install()
    {
        StickerAlbumUGUIController existing =
            FindFirstObjectByType<StickerAlbumUGUIController>(
                FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.Build();
            return;
        }

        Transform panel = FindPanel("AlbumUI");
        if (panel == null)
        {
            Debug.LogWarning(
                "[StickerAlbum] Painel AlbumUI nao encontrado no scene.");
            return;
        }

        panel.gameObject.AddComponent<StickerAlbumUGUIController>().Build();
    }

    // =========================================
    // BUILD (cards em runtime)
    // =========================================

    private void OnDestroy()
    {
        if (subscribedToCollection)
            StickerCollection.Changed -= HandleCollectionChanged;
        if (subscribedToUnseen)
            StickerUnseenTracker.Changed -= HandleUnseenChanged;
    }

    private void HandleCollectionChanged()
    {
        // Um pacote pode ser aberto enquanto o Album esta desativado; ao
        // ganhar o sticker, os cards ja ficam atualizados quando ele abre.
        if (entries != null)
            Build();
    }

    private void HandleUnseenChanged()
    {
        // Ao abrir o popup de um card, a bolinha some na hora.
        if (entries != null)
            Build();
    }

    private void Build()
    {
        if (!subscribedToCollection)
        {
            StickerCollection.Changed += HandleCollectionChanged;
            subscribedToCollection = true;
        }
        if (!subscribedToUnseen)
        {
            StickerUnseenTracker.Changed += HandleUnseenChanged;
            subscribedToUnseen = true;
        }

        Transform container =
            transform.Find("StickerScrollPainel/StickerCardPainel");
        if (container == null)
        {
            Debug.LogWarning(
                "[StickerAlbum] Container de cards (StickerCardPainel) nao encontrado.");
            return;
        }

        // O botao fica dentro deste painel inativo; liga aqui para que o
        // clique confirme mesmo antes da primeira abertura do Album.
        Transform resetRoot = transform.Find("ResetStickersButton");
        if (resetRoot != null)
        {
            StickerResetButton resetButton =
                resetRoot.GetComponent<StickerResetButton>();
            if (resetButton != null)
                resetButton.Bind();
        }

        // Titulo em PT (a cena traz "STICKER ALBUM").
        Transform title = transform.Find("StickerAlbumTittle");
        TMP_Text titleTMP = title != null ? title.GetComponent<TMP_Text>() : null;
        if (titleTMP != null)
            titleTMP.text = "\u00c1lbum de Stickers";

        // Catalogo unico com o StickerManager (mesmos assets que os
        // efeitos usam); Refresh cobre assets novos no Inspector.
        StickerManager.Refresh();
        entries = StickerManager.Catalog;
        if (entries == null || entries.Length == 0)
        {
            Debug.LogWarning(
                "[StickerAlbum] Nenhum StickerData em Resources/Stickers.");
            return;
        }

        // Reaproveita os cards que a cena ja traz (15 slots): ativa os
        // primeiros (10 stickers) e esconde os sobrando. O GridLayoutGroup
        // do container reposiciona tudo sozinho.
        var cardTransforms = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child.name.StartsWith("StickerCardPrefb"))
                cardTransforms.Add(child);
        }

        if (cardTransforms.Count == 0)
        {
            Debug.LogWarning(
                "[StickerAlbum] StickerCardPrefb nao encontrado no container.");
            return;
        }

        // Se algum dia faltar card, clona o primeiro (mesma receita da loja).
        Transform template = cardTransforms[0];
        while (cardTransforms.Count < entries.Length)
        {
            Transform clone = Instantiate(template, container);
            clone.name = $"StickerCardPrefb ({cardTransforms.Count})";
            cardTransforms.Add(clone);
        }

        for (int i = 0; i < entries.Length; i++)
        {
            cardTransforms[i].gameObject.SetActive(true);
            Bind(cardTransforms[i], entries[i], i);
        }
        for (int i = entries.Length; i < cardTransforms.Count; i++)
            cardTransforms[i].gameObject.SetActive(false);
    }

    private void Bind(Transform card, StickerData data, int index)
    {
        // O nome mantem a cor do prefab; quem indica a raridade e o fundo do titulo.
        SetText(card, "Sticker Name", data.stickerName);

        Transform titleBackgroundTransform = FindDirect(card, "backgroundTittle");
        Image titleBackground = titleBackgroundTransform != null
            ? titleBackgroundTransform.GetComponent<Image>()
            : null;
        if (titleBackground != null)
        {
            Color rarityColor = data.GetCardRarityColor();
            rarityColor.a = titleBackground.color.a; // preserva a transparencia do prefab
            titleBackground.color = rarityColor;
        }

        SetText(card, "NumTMP", (index + 1).ToString());

        Transform iconTransform = FindDirect(card, "Image");
        Image icon = iconTransform != null
            ? iconTransform.GetComponent<Image>()
            : null;
        if (icon != null)
        {
            // O clique precisa chegar ao botao do card.
            icon.raycastTarget = false;

            // Sem arte no asset, mantem o placeholder (sombra) do card.
            if (data.stickerImage != null)
                icon.sprite = data.stickerImage;

            icon.color = StickerCollection.IsOwned(data) ? Color.white : LockedColor;
        }

        // Bolinha vermelha: so aparece em sticker conquistado cujo card
        // ainda nao foi aberto. Some ao abrir o popup (StickerUnseenTracker).
        Image unseenDot = EnsureUnseenDot(card);
        unseenDot.gameObject.SetActive(StickerUnseenTracker.IsUnseen(data));

        Button button = card.GetComponent<Button>();
        if (button != null)
        {
            int captured = index;
            // RemoveAllListeners mantem o Bind re-executavel sem duplicar.
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCard(captured));

            // Card visivel, mas nao clicavel ate o sticker ser conquistado.
            button.interactable = StickerCollection.IsOwned(data);
        }
    }

    // =========================================
    // CLICK (abre o popup do sticker)
    // =========================================

    private void OnCard(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
            return;

        StickerData data = entries[index];
        if (data == null)
            return;

        // Ainda nao conquistado: nao abre. O botao ja fica nao clicavel no
        // Bind; este ramo e so uma seguranca.
        if (!StickerCollection.IsOwned(data))
        {
            GameAudioManager.Instance?.PlayClose();
            return;
        }

        PopupController.Instance?.OpenStickerPopup(data);
    }

    // =========================================
    // HELPERS
    // =========================================

    private static TMP_Text SetText(Transform root, string childName, string value)
    {
        Transform found = FindDirect(root, childName);
        TMP_Text label = found != null ? found.GetComponent<TMP_Text>() : null;
        if (label != null)
            label.text = value;
        return label;
    }

    /// <summary>
    /// Reaproveita o marcador RedCirclePosition do prefab do card. Ele ja esta
    /// no canto superior direito dentro do card, na posicao correta.
    /// </summary>
    private Image EnsureUnseenDot(Transform card)
    {
        Transform dotTransform = FindDirect(card, "RedCirclePosition");
        if (dotTransform == null)
        {
            // Fallback para cards antigos sem o marcador.
            GameObject dot = new GameObject("RedCirclePosition");
            dot.transform.SetParent(card, false);

            RectTransform rect = dot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(35.998f, 35.998f);
            rect.anchoredPosition = new Vector2(102f, 89f);
            rect.localScale = new Vector3(1.0354f, 1.0354f, 1f);

            dot.AddComponent<CanvasRenderer>();
            dotTransform = dot.transform;
        }

        Image image = dotTransform.GetComponent<Image>();
        if (image == null)
            image = dotTransform.gameObject.AddComponent<Image>();

        image.sprite = unseenDotSprite;
        image.color = unseenDotSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

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
