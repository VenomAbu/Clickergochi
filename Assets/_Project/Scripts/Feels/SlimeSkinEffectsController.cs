using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fundo do SlimeSkinUI: estrelas aparecendo aleatoriamente, com o mesmo
/// ciclo das espirais. Cada estrela nasce pequena e transparente, cresce ate
/// o tamanho normal e, ao sumir, encolhe e desaparece de novo.
///
/// Vive no proprio painel, entao so atualiza enquanto a tela estiver aberta.
/// </summary>
public class SlimeSkinEffectsController : MonoBehaviour
{
#if UNITY_EDITOR
    private const string StarIconPath = "Assets/_Project/UI/StarIcon.png";
    private const string StarIcon2Path = "Assets/_Project/UI/StarIcon2.png";
#endif

    [Header("Sprites")]
    [SerializeField] private Sprite[] starSprites;

    [Header("Quantidade e tamanho")]
    [SerializeField, Range(1, 40)] private int starCount = 14;
    [SerializeField] private Vector2 sizeRange = new Vector2(80f, 150f);

    [Header("Giro e opacidade")]
    [SerializeField] private Vector2 spinSpeedRange = new Vector2(60f, 180f);
    [SerializeField] private Vector2 alphaRange = new Vector2(0.45f, 1f);
    [SerializeField] private Color starColor = Color.white;

    [Header("Tempo de vida")]
    [Tooltip("Quanto tempo cada estrela fica na tela.")]
    [SerializeField] private Vector2 lifeRange = new Vector2(2.5f, 5.5f);
    [Tooltip("Intervalo aleatorio entre um spawn e outro.")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.12f, 0.55f);
    [SerializeField, Range(0f, 0.5f)] private float initialScale = 0.18f;
    [SerializeField, Range(0.05f, 0.4f)] private float appearFraction = 0.22f;
    [SerializeField, Range(0.05f, 0.5f)] private float disappearFraction = 0.28f;

    private sealed class Star
    {
        public RectTransform Rect;
        public Image Image;
        public float Spin;
        public float Age;
        public float Life;
        public float TargetSize;
        public float TargetAlpha;
        public bool Alive;
    }

    private readonly List<Star> stars = new();
    private RectTransform starsRoot;
    private bool running;
    private float spawnTimer;
    private int spawnIndex;

    // =========================================
    // CICLO DO UNITY
    // =========================================

    private void Reset()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
    }

    private void OnValidate()
    {
        starCount = Mathf.Clamp(starCount, 1, 40);
        sizeRange.x = Mathf.Max(8f, sizeRange.x);
        sizeRange.y = Mathf.Max(sizeRange.x, sizeRange.y);
        spinSpeedRange.x = Mathf.Max(1f, spinSpeedRange.x);
        spinSpeedRange.y = Mathf.Max(spinSpeedRange.x, spinSpeedRange.y);
        alphaRange.x = Mathf.Clamp01(alphaRange.x);
        alphaRange.y = Mathf.Clamp01(Mathf.Max(alphaRange.x, alphaRange.y));
        lifeRange.x = Mathf.Max(0.5f, lifeRange.x);
        lifeRange.y = Mathf.Max(lifeRange.x, lifeRange.y);
        spawnIntervalRange.x = Mathf.Max(0f, spawnIntervalRange.x);
        spawnIntervalRange.y = Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y);
    }

    private void Awake()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
        EnsureBuilt();
    }

    private void OnEnable()
    {
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
        if (!running || starsRoot == null)
            return;

        float dt = Time.deltaTime;

        spawnTimer -= dt;
        if (spawnTimer <= 0f && AliveCount() < starCount)
        {
            Spawn();
            spawnTimer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        foreach (Star star in stars)
        {
            if (!star.Alive || star.Rect == null)
                continue;

            star.Age += dt;
            ApplyVisual(star);

            star.Rect.localRotation *= Quaternion.Euler(0f, 0f, star.Spin * dt);

            if (star.Age >= star.Life)
            {
                star.Alive = false;
                star.Rect.gameObject.SetActive(false);
            }
        }
    }

    // =========================================
    // API DA NAVEGACAO
    // =========================================

    /// <summary>
    /// Chamado pelo VerticalNavegationController ao abrir qualquer painel.
    /// Reinicia o padrao apenas quando o painel e o SlimeSkinUI.
    /// </summary>
    public static void NotifyPanelOpened(GameObject panel)
    {
        if (panel == null)
            return;

        SlimeSkinEffectsController effects =
            panel.GetComponent<SlimeSkinEffectsController>();
        if (effects == null)
            effects = panel.GetComponentInChildren<SlimeSkinEffectsController>(true);
        if (effects == null)
            return;

        effects.EnsureBuilt();
        effects.Restart();
    }

    /// <summary>Sorteia estrelas em posicoes, tamanhos e fases diferentes.</summary>
    public void Restart()
    {
        EnsureBuilt();
        if (starsRoot == null)
            return;

        foreach (Star star in stars)
        {
            star.Alive = false;
            if (star.Rect != null)
                star.Rect.gameObject.SetActive(false);
        }

        spawnTimer = 0f;

        // Ja comeca com algumas em fases diferentes, para nao abrir vazio.
        int initial = Mathf.Min(4, starCount);
        for (int i = 0; i < initial; i++)
        {
            Star star = Spawn();
            if (star == null)
                break;
            star.Age = Random.Range(0f, star.Life * 0.6f);
            ApplyVisual(star);
        }
    }

    // =========================================
    // SPAWN / ANIMACAO
    // =========================================

    private int AliveCount()
    {
        int count = 0;
        foreach (Star star in stars)
        {
            if (star.Alive)
                count++;
        }
        return count;
    }

    private Star Spawn()
    {
        Star star = stars.Find(s => !s.Alive && s.Rect != null);
        if (star == null)
        {
            if (stars.Count >= starCount)
                return null;
            star = TakeStar();
        }

        float halfW = starsRoot.rect.width * 0.5f;
        float halfH = starsRoot.rect.height * 0.5f;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        float limitX = Mathf.Max(0f, halfW - size * 0.5f);
        float limitY = Mathf.Max(0f, halfH - size * 0.5f);

        float magnitude = Random.Range(spinSpeedRange.x, spinSpeedRange.y);

        star.Age = 0f;
        star.Life = Random.Range(lifeRange.x, lifeRange.y);
        star.TargetSize = size;
        star.TargetAlpha = Random.Range(alphaRange.x, alphaRange.y);
        star.Spin = Random.value < 0.5f ? -magnitude : magnitude;
        star.Alive = true;

        star.Rect.gameObject.SetActive(true);
        star.Rect.anchoredPosition = new Vector2(
            Random.Range(-limitX, limitX),
            Random.Range(-limitY, limitY));
        star.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        star.Rect.sizeDelta = Vector2.one * size * initialScale;

        if (star.Image != null)
        {
            // Padrao original: StarIcon, StarIcon, StarIcon2, StarIcon2...
            star.Image.sprite = SpriteForSpawn();
            star.Image.color = new Color(
                starColor.r, starColor.g, starColor.b, 0f);
        }

        return star;
    }

    private Sprite SpriteForSpawn()
    {
        if (starSprites == null || starSprites.Length == 0)
            return null;
        Sprite sprite = starSprites[(spawnIndex / 2) % starSprites.Length];
        spawnIndex++;
        return sprite;
    }

    /// <summary>Nasce pequena, fica no tamanho normal e encolhe ao sumir.</summary>
    private void ApplyVisual(Star star)
    {
        float u = Mathf.Clamp01(star.Age / Mathf.Max(0.01f, star.Life));
        float appear = Mathf.Clamp01(appearFraction);
        float disappear = Mathf.Clamp01(disappearFraction);
        float scale;
        float alphaFactor;

        if (u < appear)
        {
            float k = Smooth(u / Mathf.Max(0.001f, appear));
            scale = Mathf.Lerp(initialScale, 1f, k);
            alphaFactor = k;
        }
        else if (u > 1f - disappear)
        {
            float k = Smooth((u - (1f - disappear)) / Mathf.Max(0.001f, disappear));
            scale = Mathf.Lerp(1f, initialScale, k);
            alphaFactor = 1f - k;
        }
        else
        {
            scale = 1f;
            alphaFactor = 1f;
        }

        star.Rect.sizeDelta = Vector2.one * star.TargetSize * scale;

        if (star.Image != null)
        {
            star.Image.color = new Color(
                starColor.r,
                starColor.g,
                starColor.b,
                star.TargetAlpha * alphaFactor);
        }
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private Star TakeStar()
    {
        GameObject go = new GameObject(
            "Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(starsRoot, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        Star star = new Star { Rect = rect, Image = image };
        stars.Add(star);
        return star;
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
            root.transform.SetSiblingIndex(0);
        }

        starsRoot.anchorMin = Vector2.zero;
        starsRoot.anchorMax = Vector2.one;
        starsRoot.offsetMin = Vector2.zero;
        starsRoot.offsetMax = Vector2.zero;
    }

    // =========================================
    // SETUP (EDITOR)
    // =========================================

    public void SetupAssets()
    {
#if UNITY_EDITOR
        if (starSprites == null || starSprites.Length == 0)
        {
            starSprites = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(StarIconPath),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(StarIcon2Path)
            };
        }
#endif
    }
}
