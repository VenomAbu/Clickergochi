using UnityEngine.UI;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class UITransitionController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private HorizontalNavegationController horizontalNavigation;

    [Header("Vertical Transition")]
    [SerializeField] private bool useSlideTransition = true;
    [SerializeField] private float slideDistance = 250f;
    [SerializeField] private float openDuration = 0.22f;
    [SerializeField] private float closeDuration = 0.16f;

    [Header("Bounce Settings")]
    [SerializeField] private float startScale = 0.75f;
    [SerializeField] private float closeScale = 0.78f;

    [Header("Vignette")]
    [SerializeField] private string vignetteProperty = "_VignetteStrength";
    [SerializeField, Range(0f, 1f)] private float vignetteMaxOpacity = 1f;
    [SerializeField] private float vignetteFadeDuration = 0.15f;

    private readonly Dictionary<Image, Material> vignetteMaterials = new();

    private Coroutine transitionCoroutine;
    private bool isTransitioning;
    private readonly Dictionary<GameObject, Coroutine> popupCoroutines = new();


    // Abre um painel vertical, fechando qualquer outro painel vertical ativo.
    public void OpenVerticalPanel(GameObject targetPanel, GameObject[] verticalPanels)
    {
        if (targetPanel == null || isTransitioning)
            return;

        transitionCoroutine = StartCoroutine(
            OpenVerticalPanelRoutine(targetPanel, verticalPanels)
        );
    }

    // Ativa o painel horizontal atual e fecha os pain�is verticais.
    public void ReturnToHorizontal(GameObject[] verticalPanels)
    {
        if (isTransitioning)
            return;

        transitionCoroutine = StartCoroutine(
            ReturnToHorizontalRoutine(verticalPanels)
        );
    }

    private IEnumerator OpenVerticalPanelRoutine(
        GameObject targetPanel,
        GameObject[] verticalPanels)
    {
        isTransitioning = true;

        targetPanel.SetActive(true);
        targetPanel.transform.SetAsLastSibling();

        // Inicia sem vinheta.
        SetVignette(targetPanel, 0f);

        // Faz a vinheta aparecer.
        yield return AnimateVignette(
            targetPanel,
            vignetteMaxOpacity,
            vignetteFadeDuration
        );

        // Executa Bounce In e Slide.
        yield return EnterPanel(targetPanel);

        // Remove a vinheta assim que o painel terminar de entrar.
        yield return AnimateVignette(
            targetPanel,
            0f,
            vignetteFadeDuration
        );

        // Agora fecha os outros pain�is verticais.
        if (verticalPanels != null)
        {
            foreach (GameObject panel in verticalPanels)
            {
                if (panel == null ||
                    panel == targetPanel ||
                    !panel.activeSelf)
                    continue;

                yield return ExitPanel(panel);
            }
        }

        isTransitioning = false;
        transitionCoroutine = null;
    }

    private IEnumerator ReturnToHorizontalRoutine(GameObject[] verticalPanels)
    {
        isTransitioning = true;

        // Ativa o painel horizontal antes de fechar o vertical.
        if (horizontalNavigation != null)
        {
            GameObject horizontalPanel = horizontalNavigation.GetCurrentPanel();

            if (horizontalPanel != null)
                horizontalPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "UITransitionController: HorizontalNavigation n�o configurado."
            );
        }

        // Fecha os pain�is verticais com a anima��o de sa�da.
        if (verticalPanels != null)
        {
            foreach (GameObject panel in verticalPanels)
            {
                if (panel == null || !panel.activeSelf)
                    continue;

                yield return ExitPanel(panel);
            }
        }

        isTransitioning = false;
        transitionCoroutine = null;
    }

    // ANIMA��O DE ENTRADA
    // =========================================
    // POPUPS (mesma animacao da navegacao vertical)
    // =========================================

    private const string PopupContentName = "TransitionContent";

    /// <summary>
    /// Abre o popup: o objeto raiz (fundo preto da tela inteira) e ligado
    /// na hora, sem animacao. So o wrapper com os filhos e animado.
    /// </summary>
    public void OpenPopup(GameObject panel)
    {
        if (panel == null)
            return;

        StopPopupCoroutine(panel);
        popupCoroutines[panel] = StartCoroutine(OpenPopupRoutine(panel));
    }

    /// <summary>
    /// Fecha o popup: anima os filhos e so desliga o fundo preto no final,
    /// tambem sem qualquer animacao no objeto raiz.
    /// </summary>
    public void ClosePopup(GameObject panel)
    {
        if (panel == null)
            return;

        StopPopupCoroutine(panel);
        popupCoroutines[panel] = StartCoroutine(ClosePopupRoutine(panel));
    }

    private void StopPopupCoroutine(GameObject panel)
    {
        if (popupCoroutines.TryGetValue(panel, out Coroutine running) &&
            running != null)
        {
            StopCoroutine(running);
        }
        popupCoroutines.Remove(panel);
    }

    /// <summary>
    /// Cria (uma vez) um filho que agrupa todo o conteudo visual do popup.
    /// O fundo preto continua na raiz e nunca e transformado pela animacao.
    /// </summary>
    private RectTransform EnsurePopupContent(GameObject overlay)
    {
        Transform existing = overlay.transform.Find(PopupContentName);
        if (existing != null)
            return existing as RectTransform;

        GameObject content = new GameObject(PopupContentName, typeof(RectTransform));
        RectTransform rect = (RectTransform)content.transform;
        rect.SetParent(overlay.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Copia a ordem original antes de mexer na hierarquia: ao mover um
        // filho ele sai do overlay e os indices seguintes mudam. Depois
        // reparentiza na mesma ordem, senao a pilha de renderizacao inverte
        // (o ultimo filho, que deve ficar a frente, acabaria atras).
        var visualChildren = new List<Transform>();
        for (int i = 0; i < overlay.transform.childCount; i++)
        {
            Transform child = overlay.transform.GetChild(i);
            if (child != content.transform)
                visualChildren.Add(child);
        }

        foreach (Transform child in visualChildren)
            child.SetParent(content.transform, true);

        content.transform.SetAsLastSibling();

        return rect;
    }

    private IEnumerator OpenPopupRoutine(GameObject overlay)
    {
        RectTransform content = EnsurePopupContent(overlay);

        // Fundo preto: aparece imediatamente, sem slide/scale/fade.
        if (!overlay.activeSelf)
        {
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
        }

        yield return AnimatePopupContent(content, true);
        popupCoroutines.Remove(overlay);
    }

    private IEnumerator ClosePopupRoutine(GameObject overlay)
    {
        RectTransform content = EnsurePopupContent(overlay);

        if (overlay.activeSelf)
            yield return AnimatePopupContent(content, false);

        // Fundo preto: some imediatamente, sem slide/scale/fade.
        overlay.SetActive(false);
        popupCoroutines.Remove(overlay);
    }

    private IEnumerator AnimatePopupContent(RectTransform content, bool entering)
    {
        if (content == null)
            yield break;

        CanvasGroup canvasGroup = GetCanvasGroup(content.gameObject);
        float duration = entering ? openDuration : closeDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            if (entering)
            {
                float bounce = EaseOutBack(t);
                content.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, bounce);
                if (useSlideTransition)
                {
                    float slide = EaseOutCubic(t);
                    content.anchoredPosition = Vector2.Lerp(
                        Vector2.down * slideDistance, Vector2.zero, slide);
                }
                canvasGroup.alpha = 1f;
            }
            else
            {
                float bounce = EaseInBack(t);
                content.localScale = Vector3.one * Mathf.LerpUnclamped(1f, closeScale, bounce);
                if (useSlideTransition)
                {
                    float slide = EaseInCubic(t);
                    content.anchoredPosition = Vector2.Lerp(
                        Vector2.zero, Vector2.down * slideDistance, slide);
                }
                canvasGroup.alpha = 1f - t;
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            yield return null;
        }

        // Estado final do conteudo; o overlay raiz nunca e alterado.
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator EnterPanel(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogWarning(
                $"UITransitionController: {panel.name} n�o possui RectTransform."
            );

            yield break;
        }

        CanvasGroup canvasGroup = GetCanvasGroup(panel);

        // Guarda os valores originais do painel.
        Vector2 targetPosition = rect.anchoredPosition;
        Vector3 targetScale = rect.localScale;

        // Prepara o estado inicial da anima��o.
        rect.localScale = targetScale * startScale;

        if (useSlideTransition)
        {
            rect.anchoredPosition =
                targetPosition + Vector2.down * slideDistance;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = openDuration > 0f
                ? Mathf.Clamp01(elapsed / openDuration)
                : 1f;

            // Bounce In.
            float bounceT = EaseOutBack(t);

            rect.localScale = Vector3.LerpUnclamped(
                targetScale * startScale,
                targetScale,
                bounceT
            );

            // Slide de baixo para cima.
            if (useSlideTransition)
            {
                float slideT = EaseOutCubic(t);

                rect.anchoredPosition = Vector2.Lerp(
                    targetPosition + Vector2.down * slideDistance,
                    targetPosition,
                    slideT
                );
            }

            yield return null;
        }

        // Estado final.
        rect.localScale = targetScale;
        rect.anchoredPosition = targetPosition;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    // ANIMA��O DE SA�DA
    private IEnumerator ExitPanel(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();

        if (rect == null)
        {
            panel.SetActive(false);
            yield break;
        }

        CanvasGroup canvasGroup = GetCanvasGroup(panel);

        Vector2 originalPosition = rect.anchoredPosition;
        Vector3 originalScale = rect.localScale;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < closeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = closeDuration > 0f
                ? Mathf.Clamp01(elapsed / closeDuration)
                : 1f;

            // Bounce In invertido.
            float bounceT = EaseInBack(t);

            rect.localScale = Vector3.LerpUnclamped(
                originalScale,
                originalScale * closeScale,
                bounceT
            );

            // Slide de cima para baixo.
            if (useSlideTransition)
            {
                float slideT = EaseInCubic(t);

                rect.anchoredPosition = Vector2.Lerp(
                    originalPosition,
                    originalPosition + Vector2.down * slideDistance,
                    slideT
                );
            }

            // Fade durante a sa�da.
            canvasGroup.alpha = 1f - t;

            yield return null;
        }

        // Restaura as propriedades antes de desativar.
        rect.anchoredPosition = originalPosition;
        rect.localScale = originalScale;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        panel.SetActive(false);
    }

    private CanvasGroup GetCanvasGroup(GameObject panel)
    {
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    // Ease Out Back: ultrapassa um pouco o tamanho final e retorna.
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f)
            + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // Ease In Back: efeito contr�rio, antecipando a retra��o.
    private float EaseInBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return c3 * t * t * t - c1 * t * t;
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private List<Material> GetVignetteMaterials(GameObject panel)
    {
        List<Material> materials = new();

        if (panel == null)
            return materials;

        Image[] images = panel.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            Material material;

            // Cria uma inst�ncia exclusiva para cada Image.
            if (!vignetteMaterials.TryGetValue(image, out material))
            {
                Material originalMaterial = image.material;

                if (originalMaterial == null ||
                    !originalMaterial.HasProperty(vignetteProperty))
                    continue;

                material = new Material(originalMaterial);

                image.material = material;

                vignetteMaterials.Add(image, material);
            }

            if (material != null &&
                !materials.Contains(material))
            {
                materials.Add(material);
            }
        }

        return materials;
    }

    private void SetVignette(GameObject panel, float value)
    {
        List<Material> materials = GetVignetteMaterials(panel);

        foreach (Material material in materials)
        {
            material.SetFloat(vignetteProperty, value);
        }
    }

    private IEnumerator AnimateVignette(
        GameObject panel,
        float targetOpacity,
        float duration)
    {
        List<Material> materials = GetVignetteMaterials(panel);

        if (materials.Count == 0)
            yield break;

        float startOpacity = materials[0].GetFloat(vignetteProperty);

        if (duration <= 0f)
        {
            SetVignette(panel, targetOpacity);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            float opacity = Mathf.Lerp(
                startOpacity,
                targetOpacity,
                t
            );

            foreach (Material material in materials)
            {
                material.SetFloat(vignetteProperty, opacity);
            }

            yield return null;
        }

        SetVignette(panel, targetOpacity);
    }
}
