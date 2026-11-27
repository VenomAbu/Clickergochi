using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fundo do SlimeSkinUI: copias do BlueEspiral aparecendo aleatoriamente,
/// girando e com vida curta. Cada copia nasce pequena e transparente, cresce
/// ate o tamanho normal e, ao sumir, encolhe e desaparece de novo.
///
/// Vive no proprio painel, entao so atualiza enquanto a tela estiver aberta.
/// </summary>
public class SlimeSkinSpiralBackground : MonoBehaviour
{
#if UNITY_EDITOR
    private const string SpiralPath = "Assets/_Project/UI/SlimeShop/BlueEspiral.png";
#endif

    [Header("Sprite")]
    [SerializeField] private Sprite spiralSprite;

    [Header("Quantidade e tamanho")]
    [SerializeField, Range(1, 40)] private int spiralCount = 12;
    [SerializeField] private Vector2 sizeRange = new Vector2(90f, 240f);

    [Header("Giro e opacidade")]
    [SerializeField] private Vector2 spinSpeedRange = new Vector2(20f, 85f);
    [SerializeField] private Vector2 alphaRange = new Vector2(0.25f, 0.65f);

    [Header("Tempo de vida")]
    [Tooltip("Quanto tempo cada espiral fica na tela.")]
    [SerializeField] private Vector2 lifeRange = new Vector2(2.5f, 5.5f);
    [Tooltip("Intervalo aleatorio entre um spawn e outro.")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.12f, 0.55f);
    [SerializeField, Range(0f, 0.5f)] private float initialScale = 0.18f;
    [SerializeField, Range(0.05f, 0.4f)] private float appearFraction = 0.22f;
    [SerializeField, Range(0.05f, 0.5f)] private float disappearFraction = 0.28f;

    private sealed class Spiral
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

    private readonly List<Spiral> spirals = new();
    private RectTransform spiralsRoot;
    private bool running;
    private float spawnTimer;

    private void Reset()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
    }

    private void OnValidate()
    {
        spiralCount = Mathf.Clamp(spiralCount, 1, 40);
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
        if (!running || spiralsRoot == null)
            return;

        float dt = Time.deltaTime;

        // Novas espirais surgem em intervalos aleatorios, ate o limite de simultaneas.
        spawnTimer -= dt;
        if (spawnTimer <= 0f && AliveCount() < spiralCount)
        {
            Spawn();
            spawnTimer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        foreach (Spiral spiral in spirals)
        {
            if (!spiral.Alive || spiral.Rect == null)
                continue;

            spiral.Age += dt;
            ApplyVisual(spiral);

            spiral.Rect.localRotation *= Quaternion.Euler(0f, 0f, spiral.Spin * dt);

            if (spiral.Age >= spiral.Life)
            {
                spiral.Alive = false;
                spiral.Rect.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Reinicia o padrao quando a tela abre.</summary>
    public void Restart()
    {
        EnsureBuilt();
        if (spiralsRoot == null)
            return;

        foreach (Spiral spiral in spirals)
        {
            spiral.Alive = false;
            if (spiral.Rect != null)
                spiral.Rect.gameObject.SetActive(false);
        }

        spawnTimer = 0f;

        // Ja comeca com algumas em fases diferentes, para nao abrir vazio.
        int initial = Mathf.Min(4, spiralCount);
        for (int i = 0; i < initial; i++)
        {
            Spiral spiral = Spawn();
            if (spiral == null)
                break;
            spiral.Age = Random.Range(0f, spiral.Life * 0.6f);
            ApplyVisual(spiral);
        }
    }

    private int AliveCount()
    {
        int count = 0;
        foreach (Spiral spiral in spirals)
        {
            if (spiral.Alive)
                count++;
        }
        return count;
    }

    private Spiral Spawn()
    {
        Spiral spiral = spirals.Find(s => !s.Alive && s.Rect != null);
        if (spiral == null)
        {
            if (spirals.Count >= spiralCount)
                return null;
            spiral = TakeSpiral();
        }

        float halfW = spiralsRoot.rect.width * 0.5f;
        float halfH = spiralsRoot.rect.height * 0.5f;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        float limitX = Mathf.Max(0f, halfW - size * 0.5f);
        float limitY = Mathf.Max(0f, halfH - size * 0.5f);

        float magnitude = Random.Range(spinSpeedRange.x, spinSpeedRange.y);

        spiral.Age = 0f;
        spiral.Life = Random.Range(lifeRange.x, lifeRange.y);
        spiral.TargetSize = size;
        spiral.TargetAlpha = Random.Range(alphaRange.x, alphaRange.y);
        spiral.Spin = Random.value < 0.5f ? -magnitude : magnitude;
        spiral.Alive = true;

        spiral.Rect.gameObject.SetActive(true);
        spiral.Rect.anchoredPosition = new Vector2(
            Random.Range(-limitX, limitX),
            Random.Range(-limitY, limitY));
        spiral.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        spiral.Rect.sizeDelta = Vector2.one * size * initialScale;

        if (spiral.Image != null)
        {
            spiral.Image.sprite = spiralSprite;
            spiral.Image.color = new Color(1f, 1f, 1f, 0f);
        }

        return spiral;
    }

    /// <summary>Nasce pequeno, fica no tamanho normal e encolhe ao sumir.</summary>
    private void ApplyVisual(Spiral spiral)
    {
        float u = Mathf.Clamp01(spiral.Age / Mathf.Max(0.01f, spiral.Life));
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

        spiral.Rect.sizeDelta = Vector2.one * spiral.TargetSize * scale;

        if (spiral.Image != null)
            spiral.Image.color = new Color(1f, 1f, 1f, spiral.TargetAlpha * alphaFactor);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private Spiral TakeSpiral()
    {
        GameObject go = new GameObject(
            "BlueEspiral", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(spiralsRoot, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();
        image.sprite = spiralSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        Spiral spiral = new Spiral { Rect = rect, Image = image };
        spirals.Add(spiral);
        return spiral;
    }

    private void EnsureBuilt()
    {
        if (spiralsRoot == null)
        {
            Transform found = transform.Find("Spirals");
            if (found != null)
                spiralsRoot = found as RectTransform;
        }

        if (spiralsRoot == null)
        {
            GameObject root = new GameObject("Spirals", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            spiralsRoot = (RectTransform)root.transform;
            // Atras da scroll, do titulo e dos botoes (inclusive das estrelas).
            root.transform.SetSiblingIndex(0);
        }

        spiralsRoot.anchorMin = Vector2.zero;
        spiralsRoot.anchorMax = Vector2.one;
        spiralsRoot.offsetMin = Vector2.zero;
        spiralsRoot.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    public void SetupAssets()
    {
        if (spiralSprite != null)
            return;

        foreach (UnityEngine.Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(SpiralPath))
        {
            if (asset is Sprite sprite && (spiralSprite == null || sprite.name == "BlueEspiral_0"))
                spiralSprite = sprite;
        }
    }
#endif
}
