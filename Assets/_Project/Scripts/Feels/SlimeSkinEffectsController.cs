using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chuva de estrelas do SlimeSkinUI, ativa somente enquanto a tela estiver aberta.
///
/// Cinco linhas paralelas, todas na direção da hipotenusa entre a parede
/// esquerda e a de baixo do canvas:
///   - 2 nascendo na parede esquerda;
///   - 2 nascendo no topo;
///   - 1 nascendo no canto superior esquerdo.
///
/// Cada linha é preenchida por completo com estrelas igualmente espaçadas,
/// no padrão StarIcon, StarIcon, StarIcon2, StarIcon2 (repetindo).
/// Ao abrir a tela, a posição inicial (fase) de cada linha é aleatorizada e
/// todo o restante da linha é preenchido mantendo o mesmo espaçamento.
/// </summary>
public class SlimeSkinEffectsController : MonoBehaviour
{
#if UNITY_EDITOR
    private const string StarIconPath = "Assets/_Project/UI/StarIcon.png";
    private const string StarIcon2Path = "Assets/_Project/UI/StarIcon2.png";
#endif

    [Header("Sprites")]
    [SerializeField] private Sprite[] starSprites;

    [Header("Linhas")]
    [Tooltip("Distância igual entre estrelas da mesma linha.")]
    [SerializeField] private float starSpacing = 160f;
    [SerializeField] private float starSize = 56f;
    [Tooltip("Mesma velocidade para todas, para preservar o espaçamento.")]
    [SerializeField] private float fallSpeed = 80f;

    [Header("Giro")]
    [SerializeField] private float minSpinSpeed = 60f;
    [SerializeField] private float maxSpinSpeed = 180f;
    [SerializeField] private Color starColor = Color.white;

    private sealed class StarParticle
    {
        public RectTransform Rect;
        public Image Image;
        public int Slot;
        public float Spin;
        public StarLane Lane;
    }

    private sealed class StarLane
    {
        public Vector2 Origin;
        public float Length;
        public float Phase;
        public readonly List<StarParticle> Stars = new();
    }

    private static readonly Vector2 Direction = new Vector2(1f, -1f).normalized;

    private readonly List<StarParticle> stars = new();
    private readonly List<StarLane> lanes = new();
    private RectTransform starsRoot;
    private bool running;

    private void Reset()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
    }

    private void OnValidate()
    {
        starSpacing = Mathf.Max(starSize + 4f, starSpacing);
        starSize = Mathf.Max(1f, starSize);
        fallSpeed = Mathf.Max(1f, fallSpeed);
        minSpinSpeed = Mathf.Max(1f, minSpinSpeed);
        maxSpinSpeed = Mathf.Max(minSpinSpeed, maxSpinSpeed);
    }

    private void Awake()
    {
#if UNITY_EDITOR
        SetupAssets();
#endif
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
        if (!running || starsRoot == null || lanes.Count == 0)
            return;

        float dt = Time.deltaTime;

        foreach (StarLane lane in lanes)
        {
            // A linha inteira avança junta: a fase é a mesma para todas as
            // estrelas, então o espaçamento entre slots nunca muda.
            lane.Phase += fallSpeed * dt;

            int minSlot = int.MaxValue;
            foreach (StarParticle star in lane.Stars)
                minSlot = Mathf.Min(minSlot, star.Slot);

            foreach (StarParticle star in lane.Stars)
            {
                float distance = lane.Phase + star.Slot * starSpacing;
                if (distance > lane.Length + starSize)
                {
                    // Volta para trás da primeira estrela da linha, mantendo
                    // o padrão 2+2 e o espaçamento igual.
                    star.Slot = minSlot - 1;
                    distance = lane.Phase + star.Slot * starSpacing;
                    if (star.Image != null)
                        star.Image.sprite = SpriteFor(star.Slot);
                }

                Vector2 pos = lane.Origin + Direction * distance;
                star.Rect.anchoredPosition = pos;
                star.Rect.localRotation *= Quaternion.Euler(0f, 0f, star.Spin * dt);
            }
        }
    }

    // =========================================
    // API DA NAVEGACAO
    // =========================================

    /// <summary>
    /// Chamado pelo VerticalNavegationController ao abrir qualquer painel.
    /// Reinicia o padrão apenas quando o painel é o SlimeSkinUI.
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

    /// <summary>
    /// Sorteia a fase das 5 linhas e preenche cada uma delas inteira,
    /// mantendo o espaçamento igual e o padrão 2+2 de sprites.
    /// </summary>
    public void Restart()
    {
        EnsureBuilt();
        if (starsRoot == null)
            return;

        float halfW = starsRoot.rect.width * 0.5f;
        float halfH = starsRoot.rect.height * 0.5f;
        float size = Mathf.Max(1f, starSize);

        Vector2[] origins =
        {
            new Vector2(-halfW,  halfH * 0.5f), // parede esquerda (alta)
            new Vector2(-halfW, -halfH * 0.5f), // parede esquerda (baixa)
            new Vector2(-halfW * 0.5f, halfH),  // topo (esquerdo)
            new Vector2( halfW * 0.5f, halfH),  // topo (direito)
            new Vector2(-halfW, halfH)          // canto superior esquerdo
        };

        for (int laneIndex = 0; laneIndex < origins.Length; laneIndex++)
        {
            StarLane lane = lanes[laneIndex];
            lane.Origin = origins[laneIndex];
            lane.Length = LaneLength(lane.Origin, halfW, halfH, size);

            int needed = Mathf.Max(1, Mathf.FloorToInt(lane.Length / starSpacing) + 1);
            while (lane.Stars.Count < needed)
            {
                StarParticle star = TakeStar();
                star.Lane = lane;
                lane.Stars.Add(star);
            }
            while (lane.Stars.Count > needed)
            {
                StarParticle extra = lane.Stars[lane.Stars.Count - 1];
                lane.Stars.RemoveAt(lane.Stars.Count - 1);
                extra.Lane = null;
                extra.Rect.gameObject.SetActive(false);
            }

            // Fase aleatória: a linha toda é preenchida a partir dela.
            lane.Phase = Random.Range(0f, starSpacing);
            for (int i = 0; i < lane.Stars.Count; i++)
            {
                StarParticle star = lane.Stars[i];
                star.Slot = i;
                float distance = lane.Phase + i * starSpacing;
                star.Spin = RandomSpin();
                star.Rect.gameObject.SetActive(true);
                star.Rect.sizeDelta = new Vector2(size, size);
                star.Rect.localRotation = Quaternion.Euler(
                    0f, 0f, Random.Range(0f, 360f));
                star.Rect.anchoredPosition =
                    lane.Origin + Direction * distance;

                if (star.Image != null)
                {
                    star.Image.sprite = SpriteFor(i);
                    star.Image.color = starColor;
                }
            }
        }
    }

    private float LaneLength(Vector2 origin, float halfW, float halfH, float size)
    {
        float untilRight =
            Direction.x > 0f ? (halfW + size - origin.x) / Direction.x : float.MaxValue;
        float untilBottom =
            Direction.y < 0f ? (origin.y + halfH + size) / -Direction.y : float.MaxValue;
        return Mathf.Max(0f, Mathf.Min(untilRight, untilBottom));
    }

    private float RandomSpin()
    {
        float magnitude = Random.Range(minSpinSpeed, maxSpinSpeed);
        return Random.value < 0.5f ? -magnitude : magnitude;
    }

    private Sprite SpriteFor(int slot)
    {
        if (starSprites == null || starSprites.Length == 0)
            return null;
        // StarIcon, StarIcon, StarIcon2, StarIcon2, repetindo.
        int pattern = ((slot % 4) + 4) % 4;
        return starSprites[(pattern / 2) % starSprites.Length];
    }

    // =========================================
    // SETUP
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

    private StarParticle TakeStar()
    {
        StarParticle star = stars.Find(s => s.Lane == null);
        if (star != null)
            return star;

        GameObject go = new GameObject(
            "Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(starsRoot, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();
        image.color = starColor;
        image.raycastTarget = false;

        star = new StarParticle { Rect = rect, Image = image };
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
            // Atrás da scroll, do título e do botão de voltar.
            root.transform.SetSiblingIndex(0);
        }

        starsRoot.anchorMin = Vector2.zero;
        starsRoot.anchorMax = Vector2.one;
        starsRoot.offsetMin = Vector2.zero;
        starsRoot.offsetMax = Vector2.zero;

        if (lanes.Count == 0)
        {
            for (int i = 0; i < 5; i++)
                lanes.Add(new StarLane());
        }
    }
}
