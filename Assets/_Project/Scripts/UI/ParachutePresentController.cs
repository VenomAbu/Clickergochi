using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presente com paraquedas da MainScene.
///
/// A cada 2 minutos desce do topo, balancando para os lados como vento e
/// inclinando o presente/paraquedas na direcao do balanco. Leva 5 cliques;
/// cada clique causa stretch/squash, piscada branca e particulas de estrela.
/// Ao levar 5 cliques: pulo de tamanho, estrelas e fade. Ao encostar no
/// chao: dissolve, achata a partir da base e some sem particulas.
/// </summary>
public sealed class ParachutePresentController : MonoBehaviour, IPointerClickHandler
{
    [Header("Referencias")]
    public RectTransform canvasRect;
    public RectTransform giftRect;
    public RectTransform parachuteRect;
    public Image giftImage;
    public Image parachuteImage;
    public Sprite starSprite;
    public Material whiteFlashMaterial;

    [Header("Queda")]
    [Tooltip("Segundos entre um presente e outro (120 = 2 minutos).")]
    [SerializeField] private float spawnInterval = 120f;
    [SerializeField] private float fallSpeed = 70f;
    [SerializeField] private float swayAmplitude = 80f;
    [SerializeField] private float swaySpeed = 0.45f;

    [Header("Inclinacao")]
    [Tooltip("Presente e paraquedas giram juntos para o lado para onde estao pendendo.")]
    [SerializeField] private float tiltPerPixelPerSecond = 0.28f;
    [SerializeField] private float maxTilt = 20f;
    [SerializeField] private float tiltSmoothing = 8f;

    [SerializeField] private float groundMargin = 110f;
    [SerializeField] private float spawnMargin = 150f;

    [Header("Dano")]
    [SerializeField] private int hitsToDestroy = 5;
    [SerializeField] private float damageDuration = 0.3f;
    [SerializeField] private int particleCount = 8;
    [SerializeField] private float particleSize = 44f;
    [SerializeField] private float particleSpeed = 260f;
    [SerializeField] private float particleLife = 0.55f;
    [SerializeField] private Color particleColor = new Color(1f, 0.84f, 0.2f, 1f);

    [Header("Destruicao")]
    [Tooltip("5 cliques: pulo de tamanho, estrelas e fade.")]
    [SerializeField] private float clickDestroyDuration = 0.6f;
    [Tooltip("Chao: dissolve, amassa a partir de baixo e fade. Sem estrelas.")]
    [SerializeField] private float groundDestroyDuration = 0.9f;

    private RectTransform _rect;
    private CanvasGroup _group;
    private Image _giftFlash;
    private Image _chuteFlash;

    private int _hits;
    private float _baseX;
    private float _swayPhase;
    private float _damageWave;
    private float _tilt;
    private float _giftBaseX;
    private float _chuteBaseX;
    private Vector2 _destroyStartPos;
    private float _groundBottomOffset;
    private bool _active;
    private bool _destroying;
    private Coroutine _destroyRoutine;

    private Material _dissolveMaterial;
    private Material _giftOriginalMaterial;
    private Material _chuteOriginalMaterial;

    public int HitsLeft => Mathf.Max(0, hitsToDestroy - _hits);

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        if (canvasRect == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
        }

        if (giftRect == null)
            giftRect = transform.Find("Presente") as RectTransform;
        if (parachuteRect == null)
            parachuteRect = transform.Find("Paquedas") as RectTransform;
        if (giftImage == null && giftRect != null)
            giftImage = giftRect.GetComponent<Image>();
        if (parachuteImage == null && parachuteRect != null)
            parachuteImage = parachuteRect.GetComponent<Image>();

        _giftBaseX = giftRect != null ? giftRect.anchoredPosition.x : 0f;
        _chuteBaseX = parachuteRect != null ? parachuteRect.anchoredPosition.x : 0f;

        if (starSprite == null)
        {
            foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite.name == "StarIcon_0")
                {
                    starSprite = sprite;
                    break;
                }
            }
        }

        if (whiteFlashMaterial == null)
        {
            Shader shader = Shader.Find("Clickergochi/UIWhiteFlash");
            if (shader != null)
                whiteFlashMaterial = new Material(shader);
        }

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        if (giftRect != null && giftImage != null)
            CreateFlash(giftRect, giftImage, out _giftFlash);
        if (parachuteRect != null && parachuteImage != null)
            CreateFlash(parachuteRect, parachuteImage, out _chuteFlash);
    }

    private void Start()
    {
        _active = false;
        _destroying = false;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        StartCoroutine(SpawnLoop());
    }

    private void OnDestroy()
    {
        if (_dissolveMaterial != null)
            Destroy(_dissolveMaterial);
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnNow();

            while (_active || _destroying)
                yield return null;
        }
    }

    /// <summary>Forca o surgimento imediato (util para testar no Play).</summary>
    [ContextMenu("Spawn Now")]
    public void SpawnNow()
    {
        if (!Application.isPlaying || canvasRect == null || _active || _destroying)
            return;

        Vector2 half = canvasRect.rect.size * 0.5f;
        _hits = 0;
        _damageWave = 0f;
        _tilt = 0f;
        _baseX = Random.Range(-half.x + spawnMargin, half.x - spawnMargin);
        _swayPhase = Random.Range(0f, Mathf.PI * 2f);

        _rect.anchoredPosition = new Vector2(_baseX, half.y + spawnMargin);
        _rect.localEulerAngles = Vector3.zero;
        _rect.localScale = Vector3.one;
        if (parachuteRect != null)
        {
            parachuteRect.localEulerAngles = Vector3.zero;
            parachuteRect.anchoredPosition = new Vector2(
                _chuteBaseX, parachuteRect.anchoredPosition.y);
        }
        if (giftRect != null)
        {
            giftRect.localEulerAngles = Vector3.zero;
            giftRect.anchoredPosition = new Vector2(
                _giftBaseX, giftRect.anchoredPosition.y);
        }
        _group.alpha = 1f;
        _group.blocksRaycasts = true;

        ClearFlash();
        _active = true;
    }

    private void Update()
    {
        if (!Application.isPlaying || !_active || _destroying || canvasRect == null)
            return;

        float dt = Time.deltaTime;
        Vector2 half = canvasRect.rect.size * 0.5f;

        _swayPhase += swaySpeed * dt;
        float offset = Mathf.Sin(_swayPhase) * swayAmplitude;
        float horizontalVelocity = Mathf.Cos(_swayPhase) * swayAmplitude * swaySpeed;

        Vector2 pos = _rect.anchoredPosition;
        pos.x = Mathf.Clamp(_baseX + offset,
            -half.x + spawnMargin, half.x - spawnMargin);
        pos.y -= fallSpeed * dt;
        _rect.anchoredPosition = pos;

        // Presente e paraquedas giram juntos: o topo aponta para o lado para
        // onde o conjunto esta indo e o presente fica pendendo embaixo.
        float targetTilt = Mathf.Clamp(
            -horizontalVelocity * tiltPerPixelPerSecond, -maxTilt, maxTilt);
        _tilt = Mathf.Lerp(_tilt, targetTilt,
            1f - Mathf.Exp(-tiltSmoothing * dt));

        // Os filhos nao tem rotacao propria; basta girar o root.
        if (parachuteRect != null)
        {
            parachuteRect.localEulerAngles = Vector3.zero;
            parachuteRect.anchoredPosition = new Vector2(
                _chuteBaseX, parachuteRect.anchoredPosition.y);
        }
        if (giftRect != null)
        {
            giftRect.localEulerAngles = Vector3.zero;
            giftRect.anchoredPosition = new Vector2(
                _giftBaseX, giftRect.anchoredPosition.y);
        }
        _rect.localEulerAngles = new Vector3(0f, 0f, _tilt);

        ApplyDamageScale();
        UpdateFlash();

        if (pos.y <= -half.y + groundMargin)
            StartGroundDestroy();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_active || _destroying)
            return;

        Vector2 origin = _rect.anchoredPosition;
        if (eventData != null && canvasRect != null)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, eventData.pressEventCamera, out local))
            {
                origin = local;
            }
        }

        _hits++;
        StartCoroutine(DamageRoutine());
        SpawnStars(origin, particleCount);
        GameAudioManager.Instance?.PlayClick();

        if (_hits >= Mathf.Max(1, hitsToDestroy))
            StartClickDestroy();
    }

    private IEnumerator DamageRoutine()
    {
        float time = 0f;
        while (time < damageDuration)
        {
            if (!_active || _destroying)
                yield break;

            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / damageDuration);
            _damageWave = Mathf.Sin(k * Mathf.PI * 3f) * (1f - k);
            ApplyDamageScale();
            yield return null;
        }

        _damageWave = 0f;
        ApplyDamageScale();
    }

    private void ApplyDamageScale()
    {
        float x = 1f + 0.35f * _damageWave;
        float y = 1f - 0.25f * _damageWave;
        _rect.localScale = new Vector3(
            Mathf.Max(0.2f, x), Mathf.Max(0.2f, y), 1f);
    }

    private void UpdateFlash()
    {
        float alpha = Mathf.PingPong(Time.time * 30f, 1f) *
                      Mathf.Clamp01(1f - Mathf.Abs(_damageWave));
        if (_damageWave <= 0.001f)
            alpha = 0f;

        Color flash = new Color(1f, 1f, 1f, alpha);
        if (_giftFlash != null)
            _giftFlash.color = flash;
        if (_chuteFlash != null)
            _chuteFlash.color = flash;
    }

    private void ClearFlash()
    {
        if (_giftFlash != null)
            _giftFlash.color = Color.clear;
        if (_chuteFlash != null)
            _chuteFlash.color = Color.clear;
    }

    private bool BeginDestroy()
    {
        if (_destroying)
            return false;

        _destroying = true;
        _damageWave = 0f;
        _rect.localScale = Vector3.one;
        ClearFlash();
        _group.blocksRaycasts = false;
        if (_destroyRoutine != null)
            StopCoroutine(_destroyRoutine);
        return true;
    }

    private void FinishDestroy()
    {
        RestoreMaterials();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _rect.localScale = Vector3.one;
        _rect.localEulerAngles = Vector3.zero;
        _tilt = 0f;
        _destroying = false;
        _active = false;
        _destroyRoutine = null;
    }

    /// <summary>Animacao dos 5 cliques: pulo de tamanho, estrelas e fade.</summary>
    private void StartClickDestroy()
    {
        if (!BeginDestroy())
            return;

        // So o sucesso (5 cliques) da o bonus; deixar cair no chao nao da.
        PresentClickBonusController bonus = PresentClickBonusController.Instance;
        if (bonus == null)
            bonus = FindFirstObjectByType<PresentClickBonusController>();
        bonus?.Activate();

        _destroyRoutine = StartCoroutine(ClickDestroyRoutine());
    }

    private IEnumerator ClickDestroyRoutine()
    {
        Vector2 origin = _rect.anchoredPosition;
        SpawnStars(origin, particleCount * 2);

        float time = 0f;
        float starTimer = 0f;
        while (time < clickDestroyDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / clickDestroyDuration);

            // Encolhe um pouco, incha um pouco e termina menor.
            float scale;
            if (k < 0.3f)
                scale = Mathf.Lerp(1f, 0.78f,
                    Mathf.SmoothStep(0f, 1f, k / 0.3f));
            else if (k < 0.6f)
                scale = Mathf.Lerp(0.78f, 1.14f,
                    Mathf.SmoothStep(0f, 1f, (k - 0.3f) / 0.3f));
            else
                scale = Mathf.Lerp(1.14f, 0.9f,
                    Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));

            _rect.localScale = Vector3.one * scale;

            float fade = Mathf.Clamp01((k - 0.45f) / 0.55f);
            _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, fade);

            starTimer += Time.deltaTime;
            if (starTimer >= 0.14f && k < 0.65f)
            {
                starTimer = 0f;
                SpawnStars(_rect.anchoredPosition, 4);
            }

            yield return null;
        }

        FinishDestroy();
    }

    /// <summary>Animacao do chao: dissolve, amassa de baixo para cima e fade.</summary>
    private void StartGroundDestroy()
    {
        if (!BeginDestroy())
            return;

        _destroyStartPos = _rect.anchoredPosition;
        _groundBottomOffset = giftRect != null
            ? giftRect.anchoredPosition.y - giftRect.rect.height * giftRect.pivot.y
            : -_rect.rect.height * _rect.pivot.y;

        PrepareDissolve();
        GameAudioManager.Instance?.PlayClick();
        _destroyRoutine = StartCoroutine(GroundDestroyRoutine());
    }

    /// <summary>Material de dissolve em runtime, igual ao usado nos packs.</summary>
    private void PrepareDissolve()
    {
        if (giftImage == null)
            return;

        if (_dissolveMaterial == null)
        {
            Shader shader = Shader.Find("UI/UIDissolve");
            if (shader == null)
                return;

            _giftOriginalMaterial = giftImage.material;
            _chuteOriginalMaterial = parachuteImage != null
                ? parachuteImage.material
                : null;

            _dissolveMaterial = new Material(shader)
            {
                name = "Present_UIDissolve",
                hideFlags = HideFlags.DontSave
            };
            _dissolveMaterial.SetFloat("_EdgeWidth", 0.06f);
            _dissolveMaterial.SetFloat("_NoiseScale", 16f);
            _dissolveMaterial.SetColor("_EdgeColor", new Color(1f, 0.9f, 0.45f, 1f));
        }

        // Reaplica a cada queda: o material de runtime sobrevive ao ciclo,
        // mas os Images voltam ao material original ao fim da destruicao.
        _dissolveMaterial.SetFloat("_Progress", 0f);
        giftImage.material = _dissolveMaterial;
        if (parachuteImage != null)
            parachuteImage.material = _dissolveMaterial;
    }

    private void RestoreMaterials()
    {
        if (giftImage != null && _giftOriginalMaterial != null)
            giftImage.material = _giftOriginalMaterial;
        if (parachuteImage != null && _chuteOriginalMaterial != null)
            parachuteImage.material = _chuteOriginalMaterial;
        if (_dissolveMaterial != null)
            _dissolveMaterial.SetFloat("_Progress", 0f);
    }

    private IEnumerator GroundDestroyRoutine()
    {
        float startRotation = _tilt;

        float time = 0f;
        while (time < groundDestroyDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / groundDestroyDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, k);

            // Enfim a inclinacao da queda para ficar achatado no chao.
            float rotate = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.25f));
            _rect.localEulerAngles = new Vector3(
                0f, 0f, Mathf.Lerp(startRotation, 0f, rotate));

            float scaleX = Mathf.Lerp(1f, 1.55f, smooth);
            float scaleY = Mathf.Lerp(1f, 0.08f, smooth);
            _rect.localScale = new Vector3(scaleX, scaleY, 1f);

            // Mantem a base do presente no mesmo ponto enquanto ele achata.
            Vector2 pos = _rect.anchoredPosition;
            pos.x = _destroyStartPos.x;
            pos.y = _destroyStartPos.y + _groundBottomOffset * (1f - scaleY);
            _rect.anchoredPosition = pos;

            if (_dissolveMaterial != null)
                _dissolveMaterial.SetFloat("_Progress", smooth);
            _group.alpha = 1f - smooth;

            // Sem estrelas: essa animacao e so desintegracao no chao.
            yield return null;
        }

        FinishDestroy();
    }

    // =========================================
    // EFEITOS
    // =========================================

    private void CreateFlash(RectTransform parent, Image source, out Image flash)
    {
        GameObject go = new GameObject("WhiteFlash", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        flash = go.GetComponent<Image>();
        flash.sprite = source.sprite;
        flash.preserveAspect = true;
        flash.raycastTarget = false;
        flash.color = Color.clear;
        if (whiteFlashMaterial != null)
            flash.material = whiteFlashMaterial;
    }

    private void SpawnStars(Vector2 origin, int count)
    {
        if (starSprite == null || canvasRect == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("PresentStar", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvasRect, false);
            go.transform.SetAsLastSibling();

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * particleSize;
            rect.anchoredPosition = origin;

            Image image = go.GetComponent<Image>();
            image.sprite = starSprite;
            image.color = particleColor;
            image.raycastTarget = false;

            float angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.25f, 0.25f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            StartCoroutine(StarRoutine(rect, image, direction));
        }
    }

    private IEnumerator StarRoutine(RectTransform rect, Image image, Vector2 direction)
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
            rect.localEulerAngles = new Vector3(0f, 0f, k * 180f);
            rect.localScale = Vector3.one * (1f - 0.55f * k);
            image.color = new Color(start.r, start.g, start.b, start.a * (1f - k));
            yield return null;
        }

        if (rect != null)
            Destroy(rect.gameObject);
    }
}
