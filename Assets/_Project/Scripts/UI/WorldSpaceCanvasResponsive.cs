
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class WorldSpaceCanvasResponsive : MonoBehaviour
{
    private RectTransform rectTransform;
    private float originalHeight;
    private float lastAspect;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Preserva a altura configurada originalmente no Canvas.
        originalHeight = rectTransform.sizeDelta.y;

        UpdateCanvasSize();
    }

    private void Update()
    {
        float currentAspect = (float)Screen.width / Screen.height;

        // Só atualiza quando a proporção da tela mudar.
        if (!Mathf.Approximately(currentAspect, lastAspect))
        {
            UpdateCanvasSize();
        }
    }

    private void UpdateCanvasSize()
    {
        if (Screen.height <= 0 || Screen.width <= 0)
            return;

        float aspect = (float)Screen.width / Screen.height;

        rectTransform.sizeDelta = new Vector2(
            originalHeight * aspect,
            originalHeight
        );

        lastAspect = aspect;
    }
}
