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
    [SerializeField] private float openDuration = 0.35f;
    [SerializeField] private float closeDuration = 0.25f;

    [Header("Bounce Settings")]
    [SerializeField] private float startScale = 0.75f;
    [SerializeField] private float closeScale = 0.78f;

    [Header("Vignette")]
    [SerializeField] private string vignetteProperty = "_VignetteStrength";
    [SerializeField, Range(0f, 1f)] private float vignetteMaxOpacity = 1f;
    [SerializeField] private float vignetteFadeDuration = 0.25f;

    private readonly Dictionary<Image, Material> vignetteMaterials = new();

    private Coroutine transitionCoroutine;
    private bool isTransitioning;


    // Abre um painel vertical, fechando qualquer outro painel vertical ativo.
    public void OpenVerticalPanel(GameObject targetPanel, GameObject[] verticalPanels)
    {
        if (targetPanel == null || isTransitioning)
            return;

        transitionCoroutine = StartCoroutine(
            OpenVerticalPanelRoutine(targetPanel, verticalPanels)
        );
    }

    // Ativa o painel horizontal atual e fecha os painéis verticais.
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

        // Agora fecha os outros painéis verticais.
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
                "UITransitionController: HorizontalNavigation não configurado."
            );
        }

        // Fecha os painéis verticais com a animação de saída.
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

    // ANIMAÇÃO DE ENTRADA
    private IEnumerator EnterPanel(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogWarning(
                $"UITransitionController: {panel.name} não possui RectTransform."
            );

            yield break;
        }

        CanvasGroup canvasGroup = GetCanvasGroup(panel);

        // Guarda os valores originais do painel.
        Vector2 targetPosition = rect.anchoredPosition;
        Vector3 targetScale = rect.localScale;

        // Prepara o estado inicial da animação.
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

    // ANIMAÇÃO DE SAÍDA
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

            // Fade durante a saída.
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

    // Ease In Back: efeito contrário, antecipando a retração.
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

            // Cria uma instância exclusiva para cada Image.
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
