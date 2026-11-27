using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Contorno proprio dos slimes 3D, compativel com URP.
///
/// Cria dois renderers “gemeos” por peca:
///   - Mask: marca no stencil todas as partes do slime (corpo + acessorios);
///   - Outline: inverted hull desenhado so onde o stencil NAO foi marcado.
/// Resultado: contorno apenas na silhueta conjunta voltada a camera, sem
/// contornos internos de oculos/bigode sobre o rosto.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class Slime3DOutline : MonoBehaviour
{
    private const string OutlineShaderName = "Clickergochi/Slime Outline";
    private const string MaskShaderName = "Clickergochi/Slime Outline Mask";

    [Header("Outline")]
    [SerializeField, Range(0f, 100f)] private float outlineWidth = 50f;
    [SerializeField] private Color outlineColor = Color.white;

    [Header("Shaders (referencia de build)")]
    [Tooltip("Referencias diretas: sem elas o Shader.Find pode nao encontrar os shaders no build Android.")]
    [SerializeField] private Shader outlineShaderAsset;
    [SerializeField] private Shader maskShaderAsset;

    [Header("Options")]
    [SerializeField, Tooltip("Inclui renderers de filhos desativados.")]
    private bool includeInactive = true;

    private readonly List<Renderer> generatedRenderers = new List<Renderer>();
    private Material outlineMaterial;
    private Material maskMaterial;

    /// <summary>Espessura do contorno (0 desliga o outline).</summary>
    public float OutlineWidth
    {
        get => outlineWidth;
        set
        {
            outlineWidth = value;
            ApplyMaterial();
        }
    }

    /// <summary>Cor do contorno.</summary>
    public Color OutlineColor
    {
        get => outlineColor;
        set
        {
            outlineColor = value;
            ApplyMaterial();
        }
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnValidate()
    {
        ApplyMaterial();
    }

    private void Update()
    {
        // Slimes podem trocar de skin/ativar pecas em runtime.
        if (Application.isPlaying && generatedRenderers.Count == 0)
            Rebuild();
    }

    /// <summary>Recria os renderers de mascara e contorno.</summary>
    public void Rebuild()
    {
        Cleanup();

        if (outlineWidth <= 0f)
            return;

        // No Editor o Shader.Find sempre acha tudo. Em build, um shader sem
        // referencia explicita pode ser removido pelo stripper: por isso as
        // referencias serializadas vem primeiro e o Find so e fallback.
        Shader outlineShader = outlineShaderAsset != null
            ? outlineShaderAsset
            : Shader.Find(OutlineShaderName);
        Shader maskShader = maskShaderAsset != null
            ? maskShaderAsset
            : Shader.Find(MaskShaderName);

        Debug.Log(
            $"[Slime3DOutline] shaders: outline=" +
            $"{(outlineShader != null ? outlineShader.name : "null")}, " +
            $"mask={(maskShader != null ? maskShader.name : "null")}.", this);

        if (outlineShader == null || maskShader == null)
        {
            Debug.LogWarning(
                "[Slime3DOutline] Shaders de outline nao encontrados. " +
                "Verifique as referencias no Inspector e " +
                "Assets/_Project/Shaders/SlimeOutline.shader e " +
                "Assets/_Project/Shaders/SlimeOutlineMask.shader.",
                this
            );
            return;
        }

        outlineMaterial = new Material(outlineShader)
        {
            name = "Slime Outline (Instance)",
            hideFlags = HideFlags.DontSave
        };
        maskMaterial = new Material(maskShader)
        {
            name = "Slime Outline Mask (Instance)",
            hideFlags = HideFlags.DontSave
        };
        ApplyMaterial();

        Renderer[] sources = GetComponentsInChildren<Renderer>(includeInactive);
        foreach (Renderer source in sources)
        {
            if (source == null || source is ParticleSystemRenderer)
                continue;

            SkinnedMeshRenderer skinned = source as SkinnedMeshRenderer;
            MeshFilter meshFilter = source.GetComponent<MeshFilter>();

            if (skinned == null && (meshFilter == null || meshFilter.sharedMesh == null))
                continue;

            CreateDuplicate(source, maskMaterial, "Outline Mask - ");
            CreateDuplicate(source, outlineMaterial, "Outline - ");
        }
    }

    private Renderer CreateDuplicate(Renderer source, Material material, string namePrefix)
    {
        GameObject duplicate = new GameObject(namePrefix + source.gameObject.name);
        duplicate.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        duplicate.transform.SetParent(source.transform, false);
        duplicate.transform.localPosition = Vector3.zero;
        duplicate.transform.localRotation = Quaternion.identity;
        duplicate.transform.localScale = Vector3.one;

        Renderer duplicateRenderer;

        SkinnedMeshRenderer skinned = source as SkinnedMeshRenderer;
        if (skinned != null)
        {
            SkinnedMeshRenderer duplicateSkinned =
                duplicate.AddComponent<SkinnedMeshRenderer>();
            duplicateSkinned.sharedMesh = skinned.sharedMesh;
            duplicateSkinned.bones = skinned.bones;
            duplicateSkinned.rootBone = skinned.rootBone;
            duplicateSkinned.quality = skinned.quality;
            duplicateSkinned.updateWhenOffscreen = true;
            duplicateRenderer = duplicateSkinned;
        }
        else
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            MeshFilter duplicateFilter = duplicate.AddComponent<MeshFilter>();
            duplicateFilter.sharedMesh = sourceFilter.sharedMesh;
            duplicateRenderer = duplicate.AddComponent<MeshRenderer>();
        }

        duplicateRenderer.sharedMaterial = material;
        duplicateRenderer.shadowCastingMode = ShadowCastingMode.Off;
        duplicateRenderer.receiveShadows = false;
        duplicateRenderer.lightProbeUsage = LightProbeUsage.Off;
        duplicateRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        generatedRenderers.Add(duplicateRenderer);
        return duplicateRenderer;
    }

    private void ApplyMaterial()
    {
        if (outlineMaterial == null)
            return;

        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
    }

    private void Cleanup()
    {
        for (int i = generatedRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = generatedRenderers[i];
            if (renderer == null)
                continue;

            GameObject duplicate = renderer.gameObject;
            if (Application.isPlaying)
                Destroy(duplicate);
            else
                DestroyImmediate(duplicate);
        }
        generatedRenderers.Clear();

        DestroyMaterial(ref outlineMaterial);
        DestroyMaterial(ref maskMaterial);
    }

    private void DestroyMaterial(ref Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);

        material = null;
    }
}
