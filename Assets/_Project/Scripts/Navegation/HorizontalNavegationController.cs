
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField, Range(0.1f, 0.8f)] private float dragCommitThreshold = 0.25f;
    [SerializeField] private HorizontalEdgeFadeController edgeFadeController;

    [Header("Viewport Clipping")]
    [Tooltip("Recorta o BG quadrado de cada sala na largura real do Canvas/dispositivo. " +
             "Sem isso o BG de 2048px invade a tela de 1536px e a proxima sala aparece antes do swipe.")]
    [SerializeField] private bool clipRoomBackgrounds = true;
    [SerializeField] private RectMask2D viewportMask;

    private int currentPanelIndex;

    private Vector2[] originalPanelPositions;

    private bool isTransitioning;
    private bool isDragging;

    private float dragStartX;
    private float currentDragX;

    private int dragDirection;
    private int dragNextIndex;

    private Coroutine transitionCoroutine;

    private float lastViewportWidth = -1f;

    /// <summary>
    /// Largura visivel em unidades do Canvas. O Canvas acompanha o
    /// dispositivo, entao este valor ja e a largura real da tela.
    /// </summary>
    private float PanelWidth =>
        panelsViewport != null
            ? panelsViewport.rect.width
            : Screen.width;

    private void Awake()
    {
        InitializeNavigation();
    }

    private void Update()
    {
        // Rotacao ou mudanca de resolucao: recalcula o recorte.
        if (!clipRoomBackgrounds || panelsViewport == null)
            return;

        if (Mathf.Abs(PanelWidth - lastViewportWidth) > 0.5f)
            ApplyViewportClipping();
    }

    private void InitializeNavigation()
    {
        if (panelsViewport == null ||
            navigationPanels == null ||
            navigationPanels.Length == 0)
        {
            Debug.LogError(
                "HorizontalNavegationController: Configure os pain�is e o Viewport."
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

        // Mant�m as setas na frente do Viewport.
        if (navigationArrows != null)
            navigationArrows.SetAsLastSibling();

        ApplyViewportClipping();
    }

    // =========================================
    // VIEWPORT CLIPPING
    // =========================================

    /// <summary>
    /// Usa a largura real do Canvas para recortar o BG quadrado de cada
    /// sala. O viewport ganha um RectMask2D e cada sala outro, limitando o
    /// BG (2048px) a area visivel (ex.: 1536px). Assim, ao deslocar a
    /// proxima sala por PanelWidth, ela comeca totalmente fora da tela.
    /// </summary>
    private void ApplyViewportClipping()
    {
        if (!clipRoomBackgrounds || panelsViewport == null ||
            navigationPanels == null)
            return;

        float width = PanelWidth;
        if (width <= 0f)
            return;

        lastViewportWidth = width;

        if (viewportMask == null)
            viewportMask = panelsViewport.GetComponent<RectMask2D>();
        if (viewportMask == null)
            viewportMask = panelsViewport.gameObject.AddComponent<RectMask2D>();
        viewportMask.padding = Vector4.zero;
        viewportMask.enabled = true;

        foreach (RectTransform panel in navigationPanels)
        {
            if (panel == null)
                continue;

            RectMask2D mask = panel.GetComponent<RectMask2D>();
            if (mask == null)
                mask = panel.gameObject.AddComponent<RectMask2D>();

            // Se o painel for maior que a tela, remove o excedente
            // simetricamente. Se for menor, nao faz nada.
            float overflow = Mathf.Max(0f, panel.rect.width - width) * 0.5f;
            mask.padding = new Vector4(overflow, 0f, overflow, 0f);
            mask.enabled = true;
        }
    }

    /// <summary>Largura atual do viewport/Canvas (util para debug e testes).</summary>
    public float GetViewportWidth() => PanelWidth;

    /// <summary>Forca o recorte apos alterar o Canvas em tempo de execucao.</summary>
    public void RefreshViewportClipping() => ApplyViewportClipping();

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

        // Navega��o circular.
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

        if (edgeFadeController != null)
            edgeFadeController.ShowFades(currentPanelIndex, nextIndex, direction);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = transitionDuration > 0f
                ? Mathf.Clamp01(elapsed / transitionDuration)
                : 1f;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (edgeFadeController != null)
                edgeFadeController.SetProgress(t);

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

        if (edgeFadeController != null)
            edgeFadeController.HideAll();

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
        {
            if (edgeFadeController != null)
                edgeFadeController.SetProgress(0f);
            return;
        }

        // Esquerda = pr�ximo.
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

        // Navega��o circular.
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

        if (edgeFadeController != null)
        {
            edgeFadeController.ShowFades(
                currentPanelIndex,
                dragNextIndex,
                direction
            );
        }
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

        if (edgeFadeController != null)
        {
            float progress = PanelWidth > 0f
                ? Mathf.Clamp01(Mathf.Abs(currentDragX) / PanelWidth)
                : 0f;
            edgeFadeController.SetProgress(progress);
        }
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
        float startProgress = PanelWidth > 0f
            ? Mathf.Clamp01(Mathf.Abs(currentDragX) / PanelWidth)
            : 0f;
        float targetProgress = shouldCommit ? 1f : 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = transitionDuration > 0f
                ? Mathf.Clamp01(elapsed / transitionDuration)
                : 1f;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (edgeFadeController != null)
            {
                edgeFadeController.SetProgress(
                    Mathf.Lerp(startProgress, targetProgress, t)
                );
            }

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

        if (edgeFadeController != null)
        {
            edgeFadeController.HideAll();
        }

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
