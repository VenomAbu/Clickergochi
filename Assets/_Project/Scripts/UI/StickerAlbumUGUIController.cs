using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Album de stickers do MainScene (UGUI). Liga os StickerData de
/// Resources/Stickers aos StickerCardPrefb que a cena ja traz dentro de
/// StickerCardPainel (painel AlbumUI), na ordem do grid. Todos comecam
/// desbloqueados; o campo blocked do inspector trava um card (o gacha de
/// stickers ainda nao existe). Clique abre o popup do sticker.
/// </summary>
public class StickerAlbumUGUIController : MonoBehaviour
{
    /// <summary>Stickers do album, na ordem exibida (para o clique).</summary>
    private StickerData[] entries;

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

    private void Build()
    {
        Transform container =
            transform.Find("StickerScrollPainel/StickerCardPainel");
        if (container == null)
        {
            Debug.LogWarning(
                "[StickerAlbum] Container de cards (StickerCardPainel) nao encontrado.");
            return;
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
        SetText(card, "Sticker Name", data.stickerName);
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

            icon.color = data.blocked ? LockedColor : Color.white;
        }

        Button button = card.GetComponent<Button>();
        if (button != null)
        {
            int captured = index;
            // RemoveAllListeners mantem o Bind re-executavel sem duplicar.
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCard(captured));

            // Trava do inspector: card visivel, mas nao clicavel.
            button.interactable = !data.blocked;
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

        // Bloqueado no inspector: nao abre (gacha pendente). O botao ja
        // fica nao clicavel no Bind; este ramo e so uma seguranca.
        if (data.blocked)
        {
            MainHUDUGUIController.Instance?.PlayClose();
            return;
        }

        PopupController.Instance?.OpenStickerPopup(data);
    }

    // =========================================
    // HELPERS
    // =========================================

    private static void SetText(Transform root, string childName, string value)
    {
        Transform found = FindDirect(root, childName);
        TMP_Text label = found != null ? found.GetComponent<TMP_Text>() : null;
        if (label != null)
            label.text = value;
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
