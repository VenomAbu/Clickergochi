using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controles do fundo animado (RawImage com o shader de scroll infinito).
/// Deixa velocidade, zoom e alinhamento ajustaveis direto no Inspector do
/// painel, sem precisar abrir o material. As mudancas aparecem na hora,
/// mesmo fora do Play Mode.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ScrollingBackgroundController : MonoBehaviour
{
    [Header("Velocidade (UV por segundo)")]
    [Tooltip("X positivo = direita. Y negativo = baixo para cima; Y positivo = cima para baixo.")]
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.012f, 0.009f);

    [Header("Enquadramento")]
    [Tooltip("1 = tamanho original. Menor que 1 = padrao maior/mais perto. Maior que 1 = padrao menor/mais longe.")]
    [SerializeField, Range(0.25f, 4f)] private float tileScale = 1f;

    [Tooltip("Deslocamento manual da UV para alinhar o padrao no olho.")]
    [SerializeField] private Vector2 uvOffset = Vector2.zero;

    private RawImage _raw;

    private void OnEnable()
    {
        _raw = null;
        Apply(false);
    }

    private void OnValidate()
    {
        _raw = null;
        Apply(true);
    }

    private void Update()
    {
        if (Application.isPlaying)
            Apply(false);
    }

    private void Apply(bool markDirty)
    {
        if (_raw == null)
            _raw = GetComponent<RawImage>();
        if (_raw == null)
            return;

        // Graphic.material ja devolve o material do asset (nao clona), entao
        // o ajuste aparece na hora no Editor e vale tambem em Play Mode.
        Material mat = _raw.material;
        if (mat == null)
            return;

        if (mat.HasProperty("_ScrollSpeed"))
            mat.SetVector("_ScrollSpeed", new Vector4(scrollSpeed.x, scrollSpeed.y, 0f, 0f));
        if (mat.HasProperty("_TileScale"))
            mat.SetFloat("_TileScale", tileScale);
        if (mat.HasProperty("_UVOffset"))
            mat.SetVector("_UVOffset", new Vector4(uvOffset.x, uvOffset.y, 0f, 0f));

#if UNITY_EDITOR
        if (markDirty && !Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(mat);
#endif
    }
}
