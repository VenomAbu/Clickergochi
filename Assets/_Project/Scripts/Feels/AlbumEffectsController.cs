using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efeitos visuais do AlbumUI, ativos somente enquanto a tela estiver aberta.
///
/// - Gira o "SunPattern" em loop.
/// - Faz chover partículas StarIcon na cor #EAE395 com 50% de opacidade,
///   girando, com tamanhos e rotações iniciais diferentes.
///
/// O componente fica no próprio AlbumUI: OnEnable liga o efeito e OnDisable
/// desliga. O VerticalNavegationController avisa na abertura para reiniciar
/// o loop (posições, tamanhos e rotações) a cada visita à tela.
/// </summary>
public class AlbumEffectsController : MonoBehaviour
{
#if UNITY_EDITOR
    private const string StarIconPath = "Assets/_Project/UI/StarIcon.png";
#endif

    [Header("Referências")]
    [Tooltip("SunPattern dentro de AlbumBG. Se vazio, procurado pelo nome.")]
    [SerializeField] private RectTransform sunPattern;
    [SerializeField] private Sprite starSprite;

    [Header("SunPattern")]
    [SerializeField, Range(0f, 360f)] private float sunRotationSpeed = 18f;

    [Header("Estrelas")]
    [SerializeField, Range(1, 64)] private int starCount = 18;
    [SerializeField] private float starMinSize = 28f;
    [SerializeField] private float starMaxSize = 88f;
    [SerializeField] private float starMinFallSpeed = 140f;
    [SerializeField] private float starMaxFallSpeed = 340f;
    [Tooltip("Velocidade angular mínima para garantir que a estrela gire.")]
    [SerializeField] private float starMinSpin = 60f;
    [SerializeField] private float starMaxSpin = 240f;
    [Tooltip("#EAE395 com metade da opacidade.")]
    [SerializeField] private Color starColor =
        new Color(234f / 255f, 227f / 255f, 149f / 255f, 0.5f);

    private sealed class StarParticle
    {
        public RectTransform Rect;
        public Image Image;
        public float Speed;
        public float Spin;
        public float Size;
        public int Index;
    }

    private readonly List<StarParticle> stars = new();
    private RectTransform starsRoot;
    private bool running;

    private void Reset()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
        ResolveReferences();
    }

    private void OnValidate()
    {
        starMinSize = Mathf.Max(1f, starMinSize);
        starMaxSize = Mathf.Max(starMinSize, starMaxSize);
        starMinFallSpeed = Mathf.Max(0f, starMinFallSpeed);
        starMaxFallSpeed = Mathf.Max(starMinFallSpeed, starMaxFallSpeed);
        starMinSpin = Mathf.Max(0f, starMinSpin);
        starMaxSpin = Mathf.Max(starMinSpin, starMaxSpin);
        starColor.a = Mathf.Clamp01(starColor.a);
    }

    private void Awake()
    {
        ResolveReferences();
#if UNITY_EDITOR
        SetupAssets();
#endif
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureBuilt();
        Restart();
        running = true;
    }

    private void OnDisable()
    {
        running = false;
    }

    private void Update()
    {
        if (!running || sunPattern == null || starsRoot == null)
            return;

        float dt = Time.deltaTime;

        if (sunPattern != null)
            sunPattern.localRotation *= Quaternion.Euler(0f, 0f, -sunRotationSpeed * dt);

        float halfH = starsRoot.rect.height * 0.5f;

        foreach (StarParticle star in stars)
        {
            Vector2 pos = star.Rect.anchoredPosition;
            pos.y -= star.Speed * dt;
            star.Rect.anchoredPosition = pos;
            star.Rect.localRotation *= Quaternion.Euler(0f, 0f, star.Spin * dt);

            if (pos.y < -halfH - star.Size)
                ResetStar(star, false);
        }
    }

    // =========================================
    // API DA NAVEGACAO
    // =========================================

    /// <summary>
    /// Chamado pelo VerticalNavegationController ao abrir qualquer painel.
    /// Reinicia o loop apenas quando o painel e o AlbumUI.
    /// </summary>
    public static void NotifyPanelOpened(GameObject panel)
    {
        if (panel == null)
            return;

        AlbumEffectsController effects = panel.GetComponent<AlbumEffectsController>();
        if (effects == null)
            effects = panel.GetComponentInChildren<AlbumEffectsController>(true);
        if (effects == null)
            return;

        effects.ResolveReferences();
        effects.EnsureBuilt();
        effects.Restart();
    }

    /// <summary>Reinicia posições, tamanhos e rotações das estrelas.</summary>
    public void Restart()
    {
        if (sunPattern != null)
            sunPattern.localRotation = Quaternion.identity;

        float halfW = starsRoot != null ? starsRoot.rect.width * 0.5f : 768f;
        float halfH = starsRoot != null ? starsRoot.rect.height * 0.5f : 1024f;

        for (int i = 0; i < stars.Count; i++)
            ResetStar(stars[i], true, halfW, halfH);
    }

    // =========================================
    // SETUP
    // =========================================

    private void ResolveReferences()
    {
        if (sunPattern == null)
        {
            foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == "SunPattern")
                {
                    sunPattern = rect;
                    break;
                }
            }
        }
    }

    public void SetupAssets()
    {
#if UNITY_EDITOR
        if (starSprite == null)
            starSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(StarIconPath);
#endif
    }

    private void EnsureBuilt()
    {
        if (starsRoot == null)
        {
            Transform found = transform.Find("Stars");
            if (found != null)
                starsRoot = found as RectTransform;
        }

        if (starsRoot == null)
        {
            GameObject root = new GameObject("Stars", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            starsRoot = (RectTransform)root.transform;
            // Fica atrás do título, da scroll e do botão de voltar.
            root.transform.SetSiblingIndex(1);
        }

        starsRoot.anchorMin = Vector2.zero;
        starsRoot.anchorMax = Vector2.one;
        starsRoot.offsetMin = Vector2.zero;
        starsRoot.offsetMax = Vector2.zero;

        if (stars.Count == starCount && starsRoot.childCount == starCount)
            return;

        for (int i = starsRoot.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(starsRoot.GetChild(i).gameObject);
                continue;
            }
#endif
            Destroy(starsRoot.GetChild(i).gameObject);
        }
        stars.Clear();

        for (int i = 0; i < starCount; i++)
        {
            GameObject go = new GameObject(
                "Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(starsRoot, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = go.GetComponent<Image>();
            image.sprite = starSprite;
            image.color = starColor;
            image.raycastTarget = false;

            stars.Add(new StarParticle
            {
                Rect = rect,
                Image = image,
                Index = i
            });
        }
    }

    private void ResetStar(StarParticle star, bool initial,
        float halfW = -1f, float halfH = -1f)
    {
        if (star == null || star.Rect == null)
            return;

        if (halfW < 0f) halfW = starsRoot != null ? starsRoot.rect.width * 0.5f : 768f;
        if (halfH < 0f) halfH = starsRoot != null ? starsRoot.rect.height * 0.5f : 1024f;

        // Distribui os tamanhos pelo intervalo para garantir variedade.
        float sizeT = (star.Index + Random.value) / Mathf.Max(1, starCount);
        star.Size = Mathf.Lerp(starMinSize, starMaxSize, sizeT);
        star.Speed = Random.Range(starMinFallSpeed, starMaxFallSpeed);

        // Sempre gira, ora para um lado, ora para o outro.
        float spinMagnitude = Random.Range(starMinSpin, starMaxSpin);
        star.Spin = Random.value < 0.5f ? -spinMagnitude : spinMagnitude;

        float margin = star.Size * 0.5f;
        float x = Random.Range(-halfW + margin, halfW - margin);
        float y = initial ? Random.Range(-halfH, halfH) : halfH + star.Size;

        star.Rect.anchoredPosition = new Vector2(x, y);
        star.Rect.sizeDelta = new Vector2(star.Size, star.Size);
        // Cada estrela nasce com uma rotação diferente.
        star.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        if (star.Image != null)
            star.Image.color = starColor;
    }
}
