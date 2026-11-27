using TMPro;
using UnityEngine;

/// <summary>
/// Bolinha vermelha no AlbumBtt da HUD com a quantidade de stickers
/// conquistados e ainda nao vistos no album.
/// </summary>
public class StickerUnseenBadgeController : MonoBehaviour
{
    [SerializeField] private GameObject badgeRoot;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private HorizontalNavegationController horizontalNavigation;
    [SerializeField] private VerticalNavegationController verticalNavigation;

    [Header("Pulse")]
    [SerializeField, Range(0f, 0.25f)] private float pulseAmount = 0.06f;
    [SerializeField, Min(0f)] private float pulseSpeed = 3.2f;

    private int unseenCount = -1;

    private void OnEnable()
    {
        StickerUnseenTracker.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        StickerUnseenTracker.Changed -= Refresh;
    }

    private void Start()
    {
        if (countText != null)
            countText.color = Color.white;
    }

    private void Update()
    {
        // A sala/camada pode mudar sem mudar a quantidade de stickers.
        RefreshVisibility();
        AnimatePulse();
    }

    /// <summary>Pulsacao leve do badge (bolinha + numero juntos).</summary>
    private void AnimatePulse()
    {
        if (badgeRoot == null)
            return;

        float scale = 1f;
        if (badgeRoot.activeSelf && pulseAmount > 0f)
            scale += Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;

        badgeRoot.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void Refresh()
    {
        unseenCount = StickerUnseenTracker.UnseenCount();

        if (countText != null)
        {
            countText.text = unseenCount.ToString();
            countText.color = Color.white;
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (badgeRoot == null)
            return;

        bool visible = unseenCount > 0 && IsInHorizontalRoom();
        if (badgeRoot.activeSelf != visible)
            badgeRoot.SetActive(visible);
    }

    private bool IsInHorizontalRoom()
    {
        // O badge so vale com a HUD dos compodos visivel.
        if (hudRoot != null && !hudRoot.activeInHierarchy)
            return false;

        // Enquanto Album/Shop/Food estiver aberto, a HUD nao conta.
        if (verticalNavigation != null && verticalNavigation.IsAnyPanelOpen)
            return false;

        if (horizontalNavigation == null)
            return true;

        GameObject room = horizontalNavigation.GetCurrentPanel();
        return room != null && room.activeInHierarchy;
    }
}
