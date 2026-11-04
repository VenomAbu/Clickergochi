
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class HorizontalNavegationController : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Navigation")]
    [SerializeField] private RectTransform panelsViewport;
    [SerializeField] private RectTransform[] navigationPanels;
    [SerializeField] private RectTransform navigationArrows;

    [SerializeField] private int initialPanelIndex = 0;
    [SerializeField] private float transitionDuration = 0.35f;

    [Header("Swipe Settings")]
    [SerializeField, Range(0.1f, 0.8f)]
    private float dragCommitThreshold = 0.25f;

    private int currentPanelIndex;

    private Vector2[] originalPanelPositions;

    private bool isTransitioning;
    private bool isDragging;

    private float dragStartX;
    private float currentDragX;

    private int dragDirection;
    private int dragNextIndex;

    private Coroutine transitionCoroutine;

    private float PanelWidth =>
        panelsViewport != null
            ? panelsViewport.rect.width
            : Screen.width;

    private void Awake()
    {
        InitializeNavigation();
    }

    private void InitializeNavigation()
    {
        if (panelsViewport == null ||
            navigationPanels == null ||
            navigationPanels.Length == 0)
        {
            Debug.LogError(
                "HorizontalNavegationController: Configure os painéis e o Viewport."
            );
            return;
        }

        currentPanelIndex = Mathf.Clamp(
            initialPanelIndex,
            0,
            navigationPanels.Length - 1
        );

        originalPanelPositions =
            new Vector2[navigationPanels.Length];

        for (int i = 0; i < navigationPanels.Length; i++)
        {
            if (navigationPanels[i] == null)
                continue;

            originalPanelPositions[i] =
                navigationPanels[i].anchoredPosition;

            navigationPanels[i].gameObject.SetActive(
                i == currentPanelIndex
            );
        }

        // Mantém as setas na frente do Viewport.
        if (navigationArrows != null)
            navigationArrows.SetAsLastSibling();
    }

    // =========================================
    // ARROW NAVIGATION
    // =========================================

    public void LeftArrow()
    {
        Navigate(-1);
    }

    public void RightArrow()
    {
        Navigate(1);
    }

    private void Navigate(int direction)
    {
        if (isTransitioning || isDragging ||
            navigationPanels == null ||
            navigationPanels.Length <= 1)
            return;

        int nextIndex = currentPanelIndex + direction;

        // Navegação circular.
        if (nextIndex >= navigationPanels.Length)
            nextIndex = 0;

        if (nextIndex < 0)
            nextIndex = navigationPanels.Length - 1;

        if (navigationPanels[currentPanelIndex] == null ||
            navigationPanels[nextIndex] == null)
            return;

        transitionCoroutine = StartCoroutine(
            AnimatePanelTransition(nextIndex, direction)
        );
    }

    private IEnumerator AnimatePanelTransition(
        int nextIndex,
        int direction
    )
    {
        isTransitioning = true;

        RectTransform currentPanel =
            navigationPanels[currentPanelIndex];

        RectTransform nextPanel =
            navigationPanels[nextIndex];

        Vector2 currentStart =
            originalPanelPositions[currentPanelIndex];

        Vector2 nextStart =
            originalPanelPositions[nextIndex];

        Vector2 currentTarget =
            currentStart + Vector2.left * direction * PanelWidth;

        Vector2 nextInitial =
            nextStart + Vector2.right * direction * PanelWidth;

        nextPanel.anchoredPosition = nextInitial;
        nextPanel.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = transitionDuration > 0f
                ? Mathf.Clamp01(elapsed / transitionDuration)
                : 1f;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            currentPanel.anchoredPosition = Vector2.Lerp(
                currentStart,
                currentTarget,
                smoothT
            );

            nextPanel.anchoredPosition = Vector2.Lerp(
                nextInitial,
                nextStart,
                smoothT
            );

            yield return null;
        }

        currentPanel.anchoredPosition = currentStart;
        currentPanel.gameObject.SetActive(false);

        nextPanel.anchoredPosition = nextStart;

        currentPanelIndex = nextIndex;

        isTransitioning = false;
        transitionCoroutine = null;
    }

    // =========================================
    // SWIPE INPUT
    // =========================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isTransitioning ||
            navigationPanels == null ||
            navigationPanels.Length <= 1)
            return;

        if (!GetLocalPointerPosition(
            eventData,
            out Vector2 localPosition))
            return;

        dragStartX = localPosition.x;
        currentDragX = 0f;
        dragDirection = 0;

        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || isTransitioning)
            return;

        if (!GetLocalPointerPosition(
            eventData,
            out Vector2 localPosition))
            return;

        currentDragX = localPosition.x - dragStartX;

        currentDragX = Mathf.Clamp(
            currentDragX,
            -PanelWidth,
            PanelWidth
        );

        if (Mathf.Abs(currentDragX) < 1f)
            return;

        // Esquerda = próximo.
        // Direita = anterior.
        int newDirection = currentDragX < 0f ? 1 : -1;

        if (newDirection != dragDirection)
            PrepareDrag(newDirection);

        UpdateDragPositions();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (dragDirection == 0)
            return;

        float requiredDistance =
            PanelWidth * dragCommitThreshold;

        bool shouldCommit =
            Mathf.Abs(currentDragX) >= requiredDistance;

        transitionCoroutine = StartCoroutine(
            FinishDrag(shouldCommit)
        );
    }

    // =========================================
    // DRAG PREPARATION
    // =========================================

    private void PrepareDrag(int direction)
    {
        // Esconde o painel preparado no sentido anterior.
        if (dragDirection != 0 &&
            navigationPanels[dragNextIndex] != null)
        {
            RectTransform previousNext =
                navigationPanels[dragNextIndex];

            previousNext.anchoredPosition =
                originalPanelPositions[dragNextIndex];

            previousNext.gameObject.SetActive(false);
        }

        dragDirection = direction;

        dragNextIndex = currentPanelIndex + direction;

        // Navegação circular.
        if (dragNextIndex >= navigationPanels.Length)
            dragNextIndex = 0;

        if (dragNextIndex < 0)
            dragNextIndex = navigationPanels.Length - 1;

        RectTransform nextPanel =
            navigationPanels[dragNextIndex];

        Vector2 nextInitialPosition =
            originalPanelPositions[dragNextIndex] +
            Vector2.right * direction * PanelWidth;

        nextPanel.anchoredPosition = nextInitialPosition;
        nextPanel.gameObject.SetActive(true);
    }

    // =========================================
    // DRAG POSITION UPDATE
    // =========================================

    private void UpdateDragPositions()
    {
        RectTransform currentPanel =
            navigationPanels[currentPanelIndex];

        RectTransform nextPanel =
            navigationPanels[dragNextIndex];

        Vector2 currentStart =
            originalPanelPositions[currentPanelIndex];

        Vector2 nextStart =
            originalPanelPositions[dragNextIndex];

        currentPanel.anchoredPosition =
            currentStart + Vector2.right * currentDragX;

        nextPanel.anchoredPosition =
            nextStart + Vector2.right *
            (dragDirection * PanelWidth + currentDragX);
    }

    // =========================================
    // DRAG COMPLETION
    // =========================================

    private IEnumerator FinishDrag(bool shouldCommit)
    {
        isTransitioning = true;

        RectTransform currentPanel =
            navigationPanels[currentPanelIndex];

        RectTransform nextPanel =
            navigationPanels[dragNextIndex];

        Vector2 currentPosition =
            currentPanel.anchoredPosition;

        Vector2 nextPosition =
            nextPanel.anchoredPosition;

        Vector2 currentTarget;
        Vector2 nextTarget;

        if (shouldCommit)
        {
            currentTarget =
                originalPanelPositions[currentPanelIndex] +
                Vector2.left * dragDirection * PanelWidth;

            nextTarget =
                originalPanelPositions[dragNextIndex];
        }
        else
        {
            currentTarget =
                originalPanelPositions[currentPanelIndex];

            nextTarget =
                originalPanelPositions[dragNextIndex] +
                Vector2.right * dragDirection * PanelWidth;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = transitionDuration > 0f
                ? Mathf.Clamp01(elapsed / transitionDuration)
                : 1f;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            currentPanel.anchoredPosition = Vector2.Lerp(
                currentPosition,
                currentTarget,
                smoothT
            );

            nextPanel.anchoredPosition = Vector2.Lerp(
                nextPosition,
                nextTarget,
                smoothT
            );

            yield return null;
        }

        currentPanel.anchoredPosition =
            originalPanelPositions[currentPanelIndex];

        nextPanel.anchoredPosition =
            originalPanelPositions[dragNextIndex];

        if (shouldCommit)
        {
            currentPanel.gameObject.SetActive(false);
            currentPanelIndex = dragNextIndex;
        }
        else
        {
            nextPanel.gameObject.SetActive(false);
        }

        dragDirection = 0;
        currentDragX = 0f;

        isTransitioning = false;
        transitionCoroutine = null;
    }

    // =========================================
    // POINTER POSITION
    // =========================================

    private bool GetLocalPointerPosition(
        PointerEventData eventData,
        out Vector2 localPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelsViewport,
            eventData.position,
            eventData.pressEventCamera,
            out localPosition
        );
    }

    public GameObject GetCurrentPanel()
    {
        if (navigationPanels == null ||
            currentPanelIndex < 0 ||
            currentPanelIndex >= navigationPanels.Length)
            return null;

        if (navigationPanels[currentPanelIndex] == null)
            return null;

        return navigationPanels[currentPanelIndex].gameObject;
    }
}
