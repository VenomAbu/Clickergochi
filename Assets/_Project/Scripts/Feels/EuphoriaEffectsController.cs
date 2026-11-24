using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efeito visual da Euforia dentro de Canvas/Effects/Euforia.
/// Fica num objeto SEMPRE ativo (Effects) para detectar o inicio/fim da
/// euforia mesmo enquanto o painel estiver desligado.
///
/// - Anima os 4 frames do spritesheet dourado no fundo do painel.
/// - Gira continuamente o "TwinImage" (sprite sunPattern).
/// - Faz chover partículas StarIcon com tamanho, cor, velocidade e rotacao
///   iniciais aleatorios, limitadas a area do proprio painel.
/// </summary>
public class EuphoriaEffectsController : MonoBehaviour
{
    public static EuphoriaEffectsController Instance { get; private set; }

#if UNITY_EDITOR
    private const string GoldSheetPath =
        "Assets/_Project/UI/spritesheet_dourado_4_frames_1024.png";
    private const string SunPatternPath =
        "Assets/_Project/UI/sunPattern.png";
    private const string StarIconPath =
        "Assets/_Project/UI/StarIcon.png";
#endif

    [Header("Referências")]
    [SerializeField] private SlimeManager slimeManager;
    [SerializeField] private RectTransform euforiaPanel;
    [SerializeField] private Image goldImage;
    [SerializeField] private RectTransform twinImage;

    [Header("Sprites")]
    [SerializeField] private Sprite[] goldFrames;
    [SerializeField] private Sprite sunPatternSprite;
    [SerializeField] private Sprite starSprite;

    [Header("Animação Dourada")]
    [SerializeField, Min(0.1f)] private float goldFps = 8f;

    [Header("Twin Image (sunPattern)")]
    [SerializeField] private float twinSize = 820f;
    [SerializeField, Range(0f, 360f)] private float twinRotationSpeed = 24f;

    [Header("Estrelas")]
    [SerializeField, Range(1, 64)] private int starCount = 18;
    [SerializeField] private float starMinSize = 28f;
    [SerializeField] private float starMaxSize = 88f;
    [SerializeField] private float starMinFallSpeed = 140f;
    [SerializeField] private float starMaxFallSpeed = 340f;
    [SerializeField] private float starMinSpin = -240f;
    [SerializeField] private float starMaxSpin = 240f;
    [Tooltip("Amarelo mais quente (começo do espectro).")]
    [SerializeField] private Color starHotColor = new Color(1f, 0.78f, 0.12f, 1f);
    [Tooltip("Amarelo mais frio (fim do espectro).")]
    [SerializeField] private Color starColdColor = new Color(1f, 1f, 0.82f, 1f);

    [Header("Duracao do efeito")]
    [Tooltip("Segundos com o efeito cheio antes de comecar a sumir.")]
    [SerializeField, Min(0.1f)] private float fullEffectDuration = 5f;
    [Tooltip("Segundos de fade ate o visual desaparecer. O estado da euforia continua ativo.")]
    [SerializeField, Min(0.1f)] private float fadeOutDuration = 2f;

    private sealed class StarParticle
    {
        public RectTransform Rect;
        public Image Image;
        public float Speed;
        public float Spin;
        public float Size;
    }

    private readonly List<StarParticle> stars = new();

    private bool wasActive;
    private bool wasRealEuphoric;
    private bool temporaryVisual;
    private int goldFrame;
    private float goldTimer;
    private float effectTimer;
    private CanvasGroup panelGroup;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Reset()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
    }

    private void OnValidate()
    {
        starMinSize = Mathf.Max(1f, starMinSize);
        starMaxSize = Mathf.Max(starMinSize, starMaxSize);
        starMinFallSpeed = Mathf.Max(0f, starMinFallSpeed);
        starMaxFallSpeed = Mathf.Max(starMinFallSpeed, starMaxFallSpeed);
        twinSize = Mathf.Max(16f, twinSize);
        goldFps = Mathf.Max(0.1f, goldFps);
        fullEffectDuration = Mathf.Max(0.1f, fullEffectDuration);
        fadeOutDuration = Mathf.Max(0.1f, fadeOutDuration);
    }

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
#if UNITY_EDITOR
        SetupAssets();
#endif
        EnsureBuilt();

        // Nao liga no Awake: o SlimeDebugPanel.Start() ainda vai forcar o
        // estado limpo. O primeiro Update ja le o estado final apos os Starts.
        ApplyState(false, true);
    }

    private void Update()
    {
        bool realEuphoria = slimeManager != null && slimeManager.IsEuphoric;
        bool active = temporaryVisual || realEuphoria;

        // A euforia real que comeca enquanto o bonus do presente ja mostra o
        // visual dourado tambem deve reapresentar os 5s cheios + fade.
        if (realEuphoria && !wasRealEuphoric)
        {
            wasRealEuphoric = true;
            wasActive = true;
            ApplyState(true, true);
            return;
        }
        wasRealEuphoric = realEuphoria;

        if (active != wasActive)
        {
            ApplyState(active, false);
            return;
        }
        if (!active || euforiaPanel == null)
            return;

        Animate(Time.deltaTime);
        UpdateVisualFade(Time.unscaledDeltaTime);
    }

    private bool IsEuphoric()
        => temporaryVisual || (slimeManager != null && slimeManager.IsEuphoric);

    /// <summary>
    /// Liga/desliga o visual dourado sem mexer na euforia real do SlimeManager.
    /// Usado pelo bonus de clique do presente entregue com a mesma cara da
    /// euforia; se a euforia real estiver ativa, ela continua no lugar.
    /// </summary>
    public void SetTemporaryEuphoriaVisual(bool active)
    {
        temporaryVisual = active;
        // Ativar reinicia a animacao mesmo se o painel ja estiver ligado
        // (ex.: euforia real ja ativa e o bonus do presente dispara).
        ApplyState(IsEuphoric(), active);
    }

    // =========================================
    // SETUP
    // =========================================

    private void ResolveReferences()
    {
        if (slimeManager == null)
            slimeManager = SlimeManager.Instance;
        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();

        if (euforiaPanel == null)
        {
            // O painel fica desativado, entao GameObject.Find nao o encontra.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Euforia")
                {
                    euforiaPanel = t as RectTransform;
                    break;
                }
            }
        }

        if (euforiaPanel == null)
        {
            foreach (Transform t in FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Euforia" &&
                    t.parent != null && t.parent.name == "Effects")
                {
                    euforiaPanel = t as RectTransform;
                    break;
                }
            }
        }

        if (goldImage == null && euforiaPanel != null)
            goldImage = euforiaPanel.GetComponent<Image>();

        if (twinImage == null && euforiaPanel != null)
        {
            Transform t = euforiaPanel.Find("TwinImage");
            twinImage = t as RectTransform;
        }
    }

    /// <summary>
    /// Preenche os sprites a partir do disco no editor. Em runtime os arrays
    /// ja vem serializados na cena; se algo estiver faltando, usa o que ha.
    /// </summary>
    public void SetupAssets()
    {
#if UNITY_EDITOR
        if (goldFrames == null || goldFrames.Length == 0)
        {
            goldFrames = UnityEditor.AssetDatabase
                .LoadAllAssetsAtPath(GoldSheetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ToArray();
        }
        if (starSprite == null)
            starSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(StarIconPath);
        if (sunPatternSprite == null)
            sunPatternSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SunPatternPath);
#endif

        if (goldFrames == null || goldFrames.Length == 0)
            goldFrames = goldImage != null && goldImage.sprite != null
                ? new[] { goldImage.sprite }
                : Array.Empty<Sprite>();
    }

    private void EnsureBuilt()
    {
        if (euforiaPanel == null || goldImage == null)
            return;

        if (panelGroup == null)
            panelGroup = euforiaPanel.GetComponent<CanvasGroup>();
        if (panelGroup == null)
            panelGroup = euforiaPanel.gameObject.AddComponent<CanvasGroup>();
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        goldImage.raycastTarget = false;
        if (goldImage.sprite == null && goldFrames.Length > 0)
            goldImage.sprite = goldFrames[0];

        EnsureTwinImage();

        Transform starRoot = euforiaPanel.Find("Stars");
        if (starRoot == null)
        {
            GameObject root = new GameObject(
                "Stars", typeof(RectTransform));
            starRoot = root.transform;
            starRoot.SetParent(euforiaPanel, false);

            RectTransform rootRect = (RectTransform)starRoot;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        if (stars.Count != starCount || starRoot.childCount != starCount)
        {
            // A lista nao e serializada: no Play ela comeca vazia mesmo com
            // estrelas ja salvas na cena. Remove TODAS as filhas antes de
            // recriar, senao as particulas duplicam a cada entrada.
            for (int i = starRoot.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(
                        starRoot.GetChild(i).gameObject);
                    continue;
                }
#endif
                Destroy(starRoot.GetChild(i).gameObject);
            }
            stars.Clear();

            for (int i = 0; i < starCount; i++)
            {
                GameObject go = new GameObject(
                    "Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(starRoot, false);

                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                Image image = go.GetComponent<Image>();
                image.sprite = starSprite;
                image.raycastTarget = false;

                stars.Add(new StarParticle { Rect = rect, Image = image });
            }
        }
    }

    private void EnsureTwinImage()
    {
        if (twinImage == null)
        {
            Transform found = euforiaPanel.Find("TwinImage");
            if (found != null)
                twinImage = found as RectTransform;
        }

        if (twinImage == null)
        {
            GameObject go = new GameObject(
                "TwinImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(euforiaPanel, false);
            twinImage = (RectTransform)go.transform;
        }

        twinImage.anchorMin = new Vector2(0.5f, 0.5f);
        twinImage.anchorMax = new Vector2(0.5f, 0.5f);
        twinImage.pivot = new Vector2(0.5f, 0.5f);
        twinImage.anchoredPosition = Vector2.zero;
        twinImage.sizeDelta = new Vector2(twinSize, twinSize);

        Image image = twinImage.GetComponent<Image>();
        if (image == null)
            image = twinImage.gameObject.AddComponent<Image>();
        if (image.sprite == null)
            image.sprite = sunPatternSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        twinImage.SetAsLastSibling();
    }

    // =========================================
    // ESTADO / ANIMACAO
    // =========================================

    private void ApplyState(bool active, bool force)
    {
        if (!force && active == wasActive)
            return;

        wasActive = active;
        if (euforiaPanel == null)
            return;

        euforiaPanel.gameObject.SetActive(active);
        if (!active)
            return;

        EnsureBuilt();

        // Cada entrada de euforia reapresenta o efeito cheio por alguns
        // segundos; o fade so comeca depois disso.
        effectTimer = 0f;
        if (panelGroup != null)
            panelGroup.alpha = 1f;

        goldTimer = 0f;
        goldFrame = 0;
        if (goldFrames.Length > 0)
            goldImage.sprite = goldFrames[0];

        foreach (StarParticle star in stars)
            ResetStar(star, true);
    }

    private void Animate(float dt)
    {
        // --- Frames dourados ---
        if (goldFrames.Length > 1 && goldImage != null)
        {
            float frameDuration = 1f / Mathf.Max(0.1f, goldFps);
            goldTimer += dt;
            while (goldTimer >= frameDuration)
            {
                goldTimer -= frameDuration;
                goldFrame = (goldFrame + 1) % goldFrames.Length;
                goldImage.sprite = goldFrames[goldFrame];
            }
        }

        // --- sunPattern girando ---
        if (twinImage != null)
            twinImage.localRotation *= Quaternion.Euler(
                0f, 0f, -twinRotationSpeed * dt);

        // --- Estrelas caindo e girando ---
        float halfW = euforiaPanel.rect.width * 0.5f;
        float halfH = euforiaPanel.rect.height * 0.5f;

        foreach (StarParticle star in stars)
        {
            Vector2 pos = star.Rect.anchoredPosition;
            pos.y -= star.Speed * dt;
            star.Rect.anchoredPosition = pos;
            star.Rect.localRotation *= Quaternion.Euler(0f, 0f, star.Spin * dt);

            if (pos.y < -halfH - star.Size)
                ResetStar(star, false, halfW, halfH);
        }
    }

    /// <summary>
    /// Mantem o efeito cheio por fullEffectDuration e depois baixa o alpha do
    /// painel ate zero em fadeOutDuration. Nao encerra a euforia: so o visual.
    /// </summary>
    private void UpdateVisualFade(float dt)
    {
        if (panelGroup == null)
            return;

        effectTimer += dt;
        if (effectTimer <= fullEffectDuration)
        {
            panelGroup.alpha = 1f;
            return;
        }

        float k = Mathf.Clamp01(
            (effectTimer - fullEffectDuration) / fadeOutDuration);
        panelGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, k);
    }

    private void ResetStar(StarParticle star, bool initial,
        float halfW = -1f, float halfH = -1f)
    {
        if (star == null || star.Rect == null)
            return;

        if (halfW < 0f) halfW = euforiaPanel != null
            ? euforiaPanel.rect.width * 0.5f : 540f;
        if (halfH < 0f) halfH = euforiaPanel != null
            ? euforiaPanel.rect.height * 0.5f : 960f;

        star.Size = UnityEngine.Random.Range(starMinSize, starMaxSize);
        star.Speed = UnityEngine.Random.Range(starMinFallSpeed, starMaxFallSpeed);
        star.Spin = UnityEngine.Random.Range(starMinSpin, starMaxSpin);

        float margin = star.Size * 0.5f;
        float x = UnityEngine.Random.Range(-halfW + margin, halfW - margin);
        float y = initial
            ? UnityEngine.Random.Range(-halfH, halfH)
            : halfH + star.Size;

        star.Rect.anchoredPosition = new Vector2(x, y);
        star.Rect.sizeDelta = new Vector2(star.Size, star.Size);
        star.Rect.localRotation = Quaternion.Euler(
            0f, 0f, UnityEngine.Random.Range(0f, 360f));

        if (star.Image != null)
            star.Image.color = Color.Lerp(
                starHotColor, starColdColor, UnityEngine.Random.value);
    }
}
