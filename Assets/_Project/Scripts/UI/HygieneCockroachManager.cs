using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Baratas da higiene da MainScene (UGUI).
///
/// Quantidade conforme o medidor de Hygiene:
/// - 80%-100%: nenhuma
/// - 80%-60%: 1 barata
/// - 59%-30%: 2 baratas
/// - 29%-0%: 3 baratas
///
/// Cada barata usa o spritesheet da Barata2, quica nas paredes do canvas e
/// precisa de 3 cliques. Clique: dano com stretch/squash, flash branco,
/// particulas verdes (WhiteCircle) e mancha de SlimeIcon que some em 1s.
/// Esmagada: +35% de higiene; a ultima leva o medidor a 100%.
/// </summary>
public sealed class HygieneCockroachManager : MonoBehaviour
{
    [Header("Referencias")]
    public SlimeManager slimeManager;
    public RectTransform canvasRect;
    [Tooltip("Camada entre o HUD e o VerticalNavegationController. Se vazia, procura HUDOverlay no Canvas.")]
    [SerializeField] private RectTransform overlayLayer;
    [SerializeField] private Sprite[] walkFrames = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite whiteCircleSprite;
    [SerializeField] private Sprite slimeIconSprite;
    [SerializeField] private Material whiteFlashMaterial;

    [Header("Barata")]
    [SerializeField] private float roachSize = 150f;
    [SerializeField] private int hitsToKill = 3;
    [SerializeField] private float moveSpeedMin = 120f;
    [SerializeField] private float moveSpeedMax = 220f;
    [SerializeField] private float animationFps = 12f;
    [SerializeField] private float refreshInterval = 0.15f;
    [SerializeField] private float bounceDuration = 0.28f;
    [SerializeField, Range(0f, 30f)] private float facingHysteresis = 8f;

    [Header("Dano - visual")]
    [SerializeField] private float damageDuration = 0.3f;
    [SerializeField] private int particleCount = 8;
    [SerializeField] private float particleSize = 18f;
    [SerializeField] private float particleSpeed = 220f;
    [SerializeField] private float particleLife = 0.45f;
    [SerializeField] private Color particleColor = new Color(0.35f, 1f, 0.4f, 1f);
    [SerializeField] private float stainSize = 90f;
    [SerializeField] private float stainLife = 1f;
    [SerializeField] private Color stainColor = new Color(0.35f, 1f, 0.4f, 1f);

    private sealed class Roach
    {
        public GameObject go;
        public RectTransform rect;
        public Image image;
        public Image flash;
        public Vector2 direction;
        public float speed;
        public int hits;
        public float damageWave;
        public float bounceWave;
        public bool bounceHorizontal;
        public float facingSign = 1f;
        public Coroutine bounceRoutine;
    }

    private readonly List<Roach> _roaches = new List<Roach>();
    private RectTransform _layer;
    private float _refreshTimer;

    // AddHygiene/SetHygiene emitem StateChanged duas vezes: uma com o valor
    // antigo (ao aplicar o decaimento pendente) e outra com o valor final.
    // Sem esse bloqueio, o HUD chama Refresh no meio e spawna uma barata
    // usando a higiene antiga ainda que a final já peça menos baratas.
    private bool _suppressRefresh;

    public int ActiveRoachCount => _roaches.Count;

    /// <summary>Mapa de higiene -> quantidade de baratas.</summary>
    public static int DesiredRoachCount(float hygiene)
    {
        if (hygiene >= 0.8f)
            return 0;
        if (hygiene >= 0.6f)
            return 1;
        if (hygiene >= 0.3f)
            return 2;
        return 3;
    }

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();
        if (canvasRect == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
        }

        if (whiteFlashMaterial == null)
        {
            Shader shader = Shader.Find("Clickergochi/UIWhiteFlash");
            if (shader != null)
                whiteFlashMaterial = new Material(shader);
        }

        EnsureLayer();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            Refresh();
    }

    private void Update()
    {
        if (!Application.isPlaying || _layer == null || slimeManager == null)
            return;

        MoveRoaches(Time.deltaTime);

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = refreshInterval;
            Refresh();
        }
    }

    private void OnDestroy()
    {
        for (int i = _roaches.Count - 1; i >= 0; i--)
            RemoveRoach(_roaches[i]);
        _roaches.Clear();

        if (_layer != null)
            Destroy(_layer.gameObject);
    }

    // =========================================
    // CAMADA / SPAWN
    // =========================================

    private void EnsureLayer()
    {
        if (_layer != null || canvasRect == null)
            return;

        RectTransform parent = overlayLayer;
        if (parent == null)
        {
            // Cena: camada dedicada entre o HUD e os popups. Fallback para
            // cenas antigas: cria uma logo apos o HUD.
            foreach (Transform child in canvasRect)
            {
                if (child.name == "HUDOverlay")
                {
                    parent = child as RectTransform;
                    break;
                }
            }
        }
        if (parent == null)
        {
            GameObject holder = new GameObject("HUDOverlay", typeof(RectTransform));
            parent = holder.GetComponent<RectTransform>();
            parent.SetParent(canvasRect, false);
            parent.anchorMin = Vector2.zero;
            parent.anchorMax = Vector2.one;
            parent.offsetMin = Vector2.zero;
            parent.offsetMax = Vector2.zero;

            for (int i = 0; i < canvasRect.childCount; i++)
            {
                if (canvasRect.GetChild(i).name == "HorizontalNavegationController")
                {
                    parent.SetSiblingIndex(i + 1);
                    break;
                }
            }
        }

        GameObject go = new GameObject("HygieneRoachLayer", typeof(RectTransform));
        _layer = go.GetComponent<RectTransform>();
        _layer.SetParent(parent, false);
        _layer.anchorMin = Vector2.zero;
        _layer.anchorMax = Vector2.one;
        _layer.offsetMin = Vector2.zero;
        _layer.offsetMax = Vector2.zero;
        _layer.SetAsLastSibling();
    }

    /// <summary>
    /// Cria apenas as baratas que faltam. Nunca remove as que ja estao na
    /// cena: apos esmagar uma, as sobreviventes continuam onde estao para nao
    /// parecer teleporte. A remocao acontece somente no clique da propria barata.
    /// </summary>
    public void Refresh()
    {
        if (_suppressRefresh || !Application.isPlaying ||
            _layer == null || slimeManager == null)
            return;

        int desired = DesiredRoachCount(slimeManager.Hygiene);
        while (_roaches.Count < desired)
            SpawnRoach();
    }

    private void SpawnRoach()
    {
        if (walkFrames == null || walkFrames.Length == 0)
            return;

        GameObject go = new GameObject("HygieneRoach", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(_layer, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one * roachSize;
        rect.anchoredPosition = RandomPosition();

        Image image = go.GetComponent<Image>();
        image.sprite = walkFrames[0];
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        UISpriteSheetAnimator animator = go.AddComponent<UISpriteSheetAnimator>();
        animator.Configure(walkFrames, animationFps, true);

        GameObject flashGo = new GameObject("WhiteFlash", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        flashGo.transform.SetParent(rect, false);
        RectTransform flashRect = flashGo.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        Image flash = flashGo.GetComponent<Image>();
        flash.sprite = walkFrames[0];
        flash.preserveAspect = true;
        flash.raycastTarget = false;
        flash.color = Color.clear;
        if (whiteFlashMaterial != null)
            flash.material = whiteFlashMaterial;

        Roach roach = new Roach
        {
            go = go,
            rect = rect,
            image = image,
            flash = flash,
            direction = RandomDirection(),
            speed = Random.Range(moveSpeedMin, moveSpeedMax),
            hits = 0,
            damageWave = 0f,
            bounceWave = 0f,
            bounceHorizontal = true
        };

        ApplyHeading(roach);

        HygieneRoachClickHandler clickHandler =
            go.AddComponent<HygieneRoachClickHandler>();
        clickHandler.Clicked += eventData => OnRoachClicked(roach, eventData);

        _roaches.Add(roach);
    }

    private void RemoveRoach(Roach roach)
    {
        if (roach == null)
            return;
        _roaches.Remove(roach);
        if (roach.bounceRoutine != null)
        {
            StopCoroutine(roach.bounceRoutine);
            roach.bounceRoutine = null;
        }
        if (roach.go != null)
            Destroy(roach.go);
    }

    // =========================================
    // MOVIMENTO
    // =========================================

    private void MoveRoaches(float dt)
    {
        Vector2 half = _layer.rect.size * 0.5f;

        foreach (Roach roach in _roaches)
        {
            if (roach.go == null)
                continue;

            // A arte tem a cabeca apontando para a esquerda (-X). Com a
            // rotacao abaixo, o vetor local (-1,0) passa a apontar no sentido
            // do movimento.
            float currentRad = roach.rect.localEulerAngles.z * Mathf.Deg2Rad;
            float margin = roachSize * 0.5f *
                (Mathf.Abs(Mathf.Cos(currentRad)) + Mathf.Abs(Mathf.Sin(currentRad)));

            Vector2 pos = roach.rect.anchoredPosition + roach.direction * roach.speed * dt;
            bool touchedWall = false;

            if (pos.x <= -half.x + margin)
            {
                pos.x = -half.x + margin;
                roach.direction.x = Mathf.Abs(roach.direction.x);
                roach.bounceHorizontal = true;
                touchedWall = true;
            }
            else if (pos.x >= half.x - margin)
            {
                pos.x = half.x - margin;
                roach.direction.x = -Mathf.Abs(roach.direction.x);
                roach.bounceHorizontal = true;
                touchedWall = true;
            }

            if (pos.y <= -half.y + margin)
            {
                pos.y = -half.y + margin;
                roach.direction.y = Mathf.Abs(roach.direction.y);
                roach.bounceHorizontal = false;
                touchedWall = true;
            }
            else if (pos.y >= half.y - margin)
            {
                pos.y = half.y - margin;
                roach.direction.y = -Mathf.Abs(roach.direction.y);
                roach.bounceHorizontal = false;
                touchedWall = true;
            }

            if (touchedWall)
            {
                float jitter = Random.Range(-18f, 18f) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(jitter);
                float sin = Mathf.Sin(jitter);
                Vector2 d = roach.direction;
                roach.direction = SanitizeDirection(new Vector2(
                    d.x * cos - d.y * sin,
                    d.x * sin + d.y * cos));

                if (roach.bounceRoutine != null)
                    StopCoroutine(roach.bounceRoutine);
                roach.bounceRoutine = StartCoroutine(BounceRoutine(roach));
            }

            ApplyHeading(roach);
            roach.rect.anchoredPosition = pos;
            ApplyRoachScale(roach);
        }
    }

    private Vector2 RandomPosition()
    {
        Vector2 half = _layer.rect.size * 0.5f;
        float margin = roachSize * 0.5f;
        return new Vector2(
            Random.Range(-half.x + margin, Mathf.Max(-half.x + margin, half.x - margin)),
            Random.Range(-half.y + margin, Mathf.Max(-half.y + margin, half.y - margin)));
    }

    private static Vector2 RandomDirection()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static Vector2 SanitizeDirection(Vector2 direction)
    {
        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) ||
            direction.sqrMagnitude < 0.0001f)
        {
            return RandomDirection();
        }
        return direction.normalized;
    }

    /// <summary>
    /// A arte aponta para -X. Em vez de girar 180 graus (o que deixaria a
    /// barata de cabeca para baixo), espelha horizontalmente quando ela vai
    /// para a direita e mantem a rotacao dentro de +/-90 graus.
    /// </summary>
    private void ApplyHeading(Roach roach)
    {
        float angle = Mathf.Atan2(roach.direction.y, roach.direction.x) * Mathf.Rad2Deg;
        float absAngle = Mathf.Abs(angle);

        // facingSign: +1 = arte original (cabeca a esquerda), -1 = espelhada.
        // A histerese evita trocar de lado a cada frame perto da vertical.
        if (roach.facingSign > 0f)
        {
            if (absAngle < 90f - facingHysteresis)
                roach.facingSign = -1f;
        }
        else if (absAngle > 90f + facingHysteresis)
        {
            roach.facingSign = 1f;
        }

        float heading = roach.facingSign > 0f
            ? (angle > 0f ? angle - 180f : angle + 180f)
            : angle;

        roach.rect.localEulerAngles = new Vector3(0f, 0f, heading);
    }

    /// <summary>Combina o dano do clique com o quique na parede.</summary>
    private static void ApplyRoachScale(Roach roach)
    {
        float damage = roach.damageWave;
        float bounce = roach.bounceWave;

        float x = 1f + 0.35f * damage;
        float y = 1f - 0.25f * damage;

        if (roach.bounceHorizontal)
        {
            x += 0.45f * bounce;
            y -= 0.30f * bounce;
        }
        else
        {
            x -= 0.30f * bounce;
            y += 0.45f * bounce;
        }

        float sign = roach.facingSign < 0f ? -1f : 1f;
        roach.rect.localScale = new Vector3(
            Mathf.Max(0.2f, x) * sign, Mathf.Max(0.2f, y), 1f);
    }

    /// <summary>Stretch and squash rapido ao bater e trocar de direcao.</summary>
    private IEnumerator BounceRoutine(Roach roach)
    {
        float time = 0f;
        while (time < bounceDuration)
        {
            if (roach == null || roach.go == null)
                yield break;

            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / bounceDuration);
            roach.bounceWave = Mathf.Sin(k * Mathf.PI * 2f) * (1f - k);
            yield return null;
        }

        if (roach == null)
            yield break;

        roach.bounceWave = 0f;
        roach.bounceRoutine = null;
    }

    // =========================================
    // CLIQUE / DANO
    // =========================================

    private void OnRoachClicked(Roach roach, PointerEventData eventData)
    {
        if (!Application.isPlaying || roach == null || !_roaches.Contains(roach))
            return;

        Vector2 hitLocal = roach.rect.anchoredPosition;
        if (eventData != null)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _layer, eventData.position, eventData.pressEventCamera, out local))
            {
                hitLocal = local;
            }
        }

        roach.hits++;
        StartCoroutine(DamageRoutine(roach));
        SpawnParticles(hitLocal);
        SpawnStain(hitLocal);
        GameAudioManager.Instance?.PlayClick();

        if (roach.hits < Mathf.Max(1, hitsToKill))
            return;

        bool wasLastRoach = _roaches.Count == 1;

        // Bloqueia o StateChanged intermediario do decaimento pendente: so
        // deve reavaliar a quantidade de baratas com a higiene final.
        _suppressRefresh = true;
        try
        {
            RemoveRoach(roach);

            if (wasLastRoach)
                slimeManager.SetHygiene(1f);
            else
                slimeManager.AddHygiene(0.35f);
        }
        finally
        {
            _suppressRefresh = false;
        }

        GameAudioManager.Instance?.PlaySlime();
        Refresh();
    }

    private IEnumerator DamageRoutine(Roach roach)
    {
        float time = 0f;
        while (time < damageDuration)
        {
            if (roach == null || roach.go == null)
                yield break;

            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / damageDuration);
            roach.damageWave = Mathf.Sin(k * Mathf.PI * 3f) * (1f - k);
            ApplyRoachScale(roach);

            if (roach.flash != null)
            {
                roach.flash.sprite = roach.image.sprite;
                float alpha = Mathf.PingPong(time * 28f, 1f) * (1f - k);
                roach.flash.color = new Color(1f, 1f, 1f, alpha);
            }

            yield return null;
        }

        if (roach == null || roach.go == null)
            yield break;

        roach.damageWave = 0f;
        ApplyRoachScale(roach);
        if (roach.flash != null)
            roach.flash.color = Color.clear;
    }

    // =========================================
    // EFEITOS
    // =========================================

    private void SpawnParticles(Vector2 origin)
    {
        if (whiteCircleSprite == null || particleCount <= 0)
            return;

        for (int i = 0; i < particleCount; i++)
        {
            GameObject go = new GameObject("RoachParticle", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_layer, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * particleSize;
            rect.anchoredPosition = origin;

            Image image = go.GetComponent<Image>();
            image.sprite = whiteCircleSprite;
            image.color = particleColor;
            image.raycastTarget = false;

            float angle = (Mathf.PI * 2f * i / particleCount) + Random.Range(-0.2f, 0.2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            StartCoroutine(ParticleRoutine(rect, image, direction));
        }
    }

    private IEnumerator ParticleRoutine(RectTransform rect, Image image, Vector2 direction)
    {
        float speed = particleSpeed * Random.Range(0.7f, 1.3f);
        float time = 0f;
        Color start = image.color;

        while (time < particleLife)
        {
            if (rect == null)
                yield break;

            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / particleLife);
            rect.anchoredPosition += direction * speed * Time.deltaTime;
            rect.localScale = Vector3.one * (1f - 0.5f * k);
            image.color = new Color(start.r, start.g, start.b, start.a * (1f - k));
            yield return null;
        }

        if (rect != null)
            Destroy(rect.gameObject);
    }

    private void SpawnStain(Vector2 origin)
    {
        if (slimeIconSprite == null)
            return;

        GameObject go = new GameObject("SlimeStain", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_layer, false);
        // A mancha e um efeito de fundo: fica abaixo de todas as baratas.
        go.transform.SetAsFirstSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one * stainSize;
        rect.anchoredPosition = origin;

        Image image = go.GetComponent<Image>();
        image.sprite = slimeIconSprite;
        image.color = new Color(stainColor.r, stainColor.g, stainColor.b, stainColor.a);
        image.raycastTarget = false;

        StartCoroutine(StainRoutine(image));
    }

    private IEnumerator StainRoutine(Image image)
    {
        float time = 0f;
        Color start = image.color;
        while (time < stainLife)
        {
            if (image == null)
                yield break;

            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / stainLife);
            image.color = new Color(start.r, start.g, start.b, start.a * (1f - k));
            yield return null;
        }

        if (image != null)
            Destroy(image.gameObject);
    }
}

/// <summary>Repassa o clique com os dados do ponteiro para a barata.</summary>
internal sealed class HygieneRoachClickHandler : MonoBehaviour, IPointerClickHandler
{
    public System.Action<PointerEventData> Clicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(eventData);
    }
}
