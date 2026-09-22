using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Baratas da higiene: aparecem com higiene menor que 100%, quicam nas
/// bordas da tela principal, 5 cliques destroem (+25% higiene cada).
/// Base extensivel para futuros elementos de sujeira (coco).
/// </summary>
public class CockroachManager : MonoBehaviour
{
    [Header("Referencias")]
    public Sprite cockroachSprite;
    public SlimeManager slimeManager;

    [Header("Baratas")]
    [SerializeField] private int maxCritters = 3;
    [SerializeField] private float moveSpeedMin = 140f;
    [SerializeField] private float moveSpeedMax = 260f;
    [SerializeField] private int hitsRequired = 5;
    [SerializeField, Range(0f, 1f)] private float hygienePerKill = 0.25f;
    [SerializeField] private float critterWidth = 68f;
    [SerializeField] private float maintainInterval = 0.5f;

    public System.Action onCritterHit;
    public System.Action onCritterKilled;
    public int CritterCount => critters.Count;

    private class Critter
    {
        public Button btn;
        public Vector2 dir;
        public float speed;
        public int hits;
        public float pop;
        public float w, h;
        public Vector2 lastPos;
        public float stuckTimer;
    }

    private readonly List<Critter> critters = new();
    private VisualElement layer;
    private VisualElement root;
    private Texture2D cleanTexture;
    private float maintainTimer;

    private void OnEnable()
    {
        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();
        root = GetComponent<UIDocument>()?.rootVisualElement
            ?? FindFirstObjectByType<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[Baratas] UIDocument nao achado.", this);
            return;
        }
        layer = root.Q<VisualElement>("CritterLayer");
        if (layer == null)
        {
            layer = new VisualElement { name = "CritterLayer" };
            layer.AddToClassList("critter-layer");
            root.Insert(0, layer);
        }
        PrepareTexture();
    }

    /// <summary>Recorta a area do sprite (sem leitura de pixels: via RenderTexture).</summary>
    private void PrepareTexture()
    {
        if (cockroachSprite == null || cleanTexture != null)
            return;
        try
        {
            var tex = cockroachSprite.texture;
            var r = cockroachSprite.textureRect;
            int w = Mathf.Max(1, Mathf.RoundToInt(r.width));
            int h = Mathf.Max(1, Mathf.RoundToInt(r.height));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt,
                new Vector2(r.width / tex.width, r.height / tex.height),
                new Vector2(r.x / tex.width, r.y / tex.height));
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            cleanTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cleanTexture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            cleanTexture.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Baratas] recorte falhou, usando textura cheia: {e.Message}", this);
            cleanTexture = cockroachSprite.texture;
        }
    }

    private void Update()
    {
        if (layer == null || slimeManager == null)
            return;
        float dt = Time.deltaTime;
        float areaW = layer.resolvedStyle.width;
        float areaH = layer.resolvedStyle.height;
        foreach (var c in critters)
        {
            if (areaW > 0f && areaH > 0f)
            {
                float x = c.btn.resolvedStyle.left + c.dir.x * c.speed * dt;
                float y = c.btn.resolvedStyle.top + c.dir.y * c.speed * dt;
                bool bounced = false;
                if (x <= 0f) { x = 0f; bounced = true; }
                else if (x >= areaW - c.w) { x = areaW - c.w; bounced = true; }
                if (y <= 0f) { y = 0f; bounced = true; }
                else if (y >= areaH - c.h) { y = areaH - c.h; bounced = true; }
                if (bounced)
                {
                    // Estilo DVD: ao triscar a borda, direcao sempre aleatoria para dentro.
                    c.dir = RandomBounceDirection(x, y, areaW, areaH, c.w, c.h);
                    c.pop = Mathf.Max(c.pop, 0.4f);
                    c.stuckTimer = 0f;
                }
                c.btn.style.left = x;
                c.btn.style.top = y;
                // Anti-travamento: parado > 1.5s => nova direcao aleatoria.
                if (Vector2.Distance(new Vector2(x, y), c.lastPos) < 2f)
                {
                    c.stuckTimer += dt;
                    if (c.stuckTimer > 1.5f)
                    {
                        c.stuckTimer = 0f;
                        c.dir = SanitizeDirection(c.dir * 0.5f + Random.insideUnitCircle);
                        c.pop = Mathf.Max(c.pop, 0.6f);
                    }
                }
                else
                {
                    c.stuckTimer = 0f;
                }
                c.lastPos = new Vector2(x, y);
            }
            if (c.pop > 0f)
            {
                c.pop = Mathf.Max(0f, c.pop - dt * 6f);
                float s = 1f - 0.25f * c.pop;
                c.btn.style.scale = new StyleScale(new Scale(new Vector2(s, s)));
            }
        }
        maintainTimer -= dt;
        if (maintainTimer <= 0f)
        {
            maintainTimer = maintainInterval;
            Maintain();
        }
    }

    /// <summary>Garante direcao valida (nunca zero/NaN): anti-travamento.</summary>
    public static Vector2 SanitizeDirection(Vector2 d)
    {
        if (float.IsNaN(d.x) || float.IsNaN(d.y) || d.sqrMagnitude < 0.0001f)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }
        return d.normalized;
    }

    /// <summary>Direcao aleatoria para dentro da tela a partir do ponto de quique.</summary>
    public static Vector2 RandomBounceDirection(float x, float y, float areaW, float areaH, float w, float h)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f);
        Vector2 d = new(Mathf.Cos(ang), Mathf.Sin(ang));
        if (x <= 0f) d.x = Mathf.Abs(d.x);
        else if (x >= areaW - w) d.x = -Mathf.Abs(d.x);
        if (y <= 0f) d.y = Mathf.Abs(d.y);
        else if (y >= areaH - h) d.y = -Mathf.Abs(d.y);
        // Evita saidas quase paralelas a parede (deslize infinito).
        if (Mathf.Abs(d.x) < 0.35f) d.x = (d.x >= 0f ? 0.35f : -0.35f);
        if (Mathf.Abs(d.y) < 0.35f) d.y = (d.y >= 0f ? 0.35f : -0.35f);
        return SanitizeDirection(d);
    }

    /// <summary>Quantidade ideal = deficit de higiene; 100% = zero baratas.</summary>
    public void Maintain()
    {
        if (layer == null || slimeManager == null)
            return;
        float hygiene = slimeManager.Hygiene;
        int desired = hygiene >= 1f ? 0 : Mathf.Clamp(Mathf.CeilToInt((1f - hygiene) * maxCritters), 1, maxCritters);
        while (critters.Count < desired)
            SpawnCritter();
        while (critters.Count > desired)
            RemoveCritter(critters[0], false);
    }

    private void SpawnCritter()
    {
        float areaW = layer.resolvedStyle.width;
        float areaH = layer.resolvedStyle.height;
        if (areaW <= 10f || areaH <= 10f)
        {
            if (root == null)
                return;
            areaW = root.resolvedStyle.width;
            areaH = root.resolvedStyle.height;
            if (areaW <= 10f || areaH <= 10f)
                return;
        }
        var btn = new Button { text = "" };
        btn.AddToClassList("critter-btn");
        float aspect = 512f / 378f;
        float w = critterWidth;
        float h = critterWidth * aspect;
        btn.style.width = w;
        btn.style.height = h;
        if (cleanTexture != null)
            btn.style.backgroundImage = new StyleBackground(cleanTexture);
        btn.style.left = Random.Range(0f, Mathf.Max(1f, areaW - w));
        btn.style.top = Random.Range(0f, Mathf.Max(1f, areaH - h));
        var critter = new Critter
        {
            btn = btn,
            dir = SanitizeDirection(Random.insideUnitCircle),
            speed = Random.Range(moveSpeedMin, moveSpeedMax),
            hits = 0,
            pop = 0f,
            w = w,
            h = h,
            lastPos = new Vector2(float.MaxValue, float.MaxValue),
            stuckTimer = 0f
        };
        btn.clicked += () => RegisterHit(critter);
        layer.Add(btn);
        critters.Add(critter);
    }

    private void RegisterHit(Critter c)
    {
        if (!critters.Contains(c))
            return;
        c.hits++;
        c.pop = 1f;
        if (c.hits >= hitsRequired)
        {
            slimeManager.AddHygiene(hygienePerKill);
            onCritterKilled?.Invoke();
            RemoveCritter(c, true);
        }
        else
        {
            onCritterHit?.Invoke();
        }
    }

    private void RemoveCritter(Critter c, bool killed)
    {
        critters.Remove(c);
        if (c.btn != null && c.btn.panel != null)
            c.btn.RemoveFromHierarchy();
    }

    public Button GetCritter(int index)
    {
        if (index < 0 || index >= critters.Count)
            return null;
        return critters[index].btn;
    }
}
