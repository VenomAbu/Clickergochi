using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Responsividade do Canvas da MainScene.
///
/// O jogo foi desenhado em 9:16 (1080x1920). Com Scale With Screen Size:
/// - tela proporcional a 9:16 ou mais larga (width/height >= 9/16) => Match 1
///   (escala pela altura);
/// - tela mais alta que 9:16 (width/height &lt; 9/16) => Match 0
///   (escala pela largura).
///
/// A regra e aplicada no inicio e sempre que a resolucao muda.
/// </summary>
[DisallowMultipleComponent]
public sealed class CanvasScalerAspectMatcher : MonoBehaviour
{
    [Tooltip("CanvasScaler ajustado. Se vazio, usa o deste GameObject.")]
    [SerializeField] private CanvasScaler canvasScaler;

    private int _lastWidth = -1;
    private int _lastHeight = -1;

    private void Awake()
    {
        if (canvasScaler == null)
            canvasScaler = GetComponent<CanvasScaler>();

        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            Apply();
    }

    /// <summary>Aplica a regra de Match para a resolucao atual.</summary>
    public void Apply()
    {
        if (canvasScaler == null)
            return;
        if (canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
            canvasScaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
            return;

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        canvasScaler.matchWidthOrHeight =
            ShouldMatchHeight(_lastWidth, _lastHeight,
                canvasScaler.referenceResolution)
                ? 1f
                : 0f;
    }

    /// <summary>
    /// True = Match 1 (escala pela altura); false = Match 0 (pela largura).
    /// Comparacao por produto cruzado evita erro de ponto flutuante em 9:16.
    /// </summary>
    public static bool ShouldMatchHeight(int width, int height, Vector2 reference)
    {
        if (width <= 0 || height <= 0 || reference.y <= 0f)
            return true;

        // width/height >= reference.x/reference.y
        double widthSide = (double)width * reference.y;
        double heightSide = (double)height * reference.x;
        return widthSide >= heightSide;
    }
}
