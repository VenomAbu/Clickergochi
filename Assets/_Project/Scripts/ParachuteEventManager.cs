using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Evento aleatorio "Caixa de Paraquedas" (TP3): um pacote cai de
/// paraquedas ao lado da Slime e o jogador precisa clicar nele antes que
/// toque no chao.
/// - Acerto: premio = producao por segundo atual x <see cref="bonusMultiplier"/>
///   (spec: x 500), creditado na hora.
/// - Erro (toca no chao): desaparece sem premio.
/// Ocorre somente com o jogo aberto. A chance de aparecer e multiplicada
/// pelo bônus de eventos do Macarrao Cremoso (+12%/+25%).
/// </summary>
public class ParachuteEventManager : MonoBehaviour
{
    private static ParachuteEventManager _instance;

    /// <summary>
    /// Instancia unica. Se a cena nao tiver o componente, cria um
    /// GameObject em runtime (mesmo padrao do FurnitureManager).
    /// </summary>
    public static ParachuteEventManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<ParachuteEventManager>();
            if (_instance == null)
            {
                var go = new GameObject("ParachuteEventManager");
                _instance = go.AddComponent<ParachuteEventManager>();
            }
            return _instance;
        }
    }

    [Header("Sorteio")]
    [Tooltip("A cada N segundos, rola a chance de o evento ocorrer.")]
    [SerializeField, Min(1f)] private float checkIntervalSeconds = 30f;
    [Tooltip("Chance base de o evento ocorrer a cada rolagem (1 = sempre).")]
    [Range(0f, 1f)] private float spawnChance = 0.25f;

    [Header("Queda")]
    [Tooltip("Segundos de queda do topo ate o chao.")]
    [SerializeField, Min(1f)] private float fallSeconds = 5f;
    [Tooltip("Expoente da queda (2 = gravidade constante, mais forte).")]
    [SerializeField, Min(0.1f)] private float fallGravityPower = 2f;
    [Tooltip("Balanço lateral do paraquedas em pixels.")]
    [SerializeField] private float swayPixels = 12f;

    [Header("Premio")]
    [Tooltip("Premio = producao por segundo atual x este valor (GDD/TP3: 500).")]
    [SerializeField, Min(1f)] private float bonusMultiplier = 500f;

    /// <summary>Disparado quando o jogador captura a caixa (valor creditado).</summary>
    public event Action<float> Claimed;

    private UIDocument document;
    private VisualElement box;
    private IVisualElementScheduledItem fallItem;
    private bool falling;
    private float fallElapsed;
    private float startX;
    private float startY;
    private float endY;
    private float rollTimer;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        // Primeira rolagem antes de um intervalo cheio.
        rollTimer = Mathf.Max(1f, checkIntervalSeconds * 0.5f);
    }

    private void OnDisable()
    {
        ClearEvent();
    }

    private void Update()
    {
        rollTimer -= Time.unscaledDeltaTime;
        if (rollTimer > 0f)
            return;
        rollTimer = checkIntervalSeconds;

        // So um evento por vez e so com o jogo aberto (o Update nao roda
        // em segundo plano — regra do TP3 para eventos aleatorios).
        if (box != null)
            return;

        // Sem Cama o premio seria 0 (producao x 500); nao sorteia.
        FurnitureManager furniture = FurnitureManager.Instance;
        if (furniture == null || furniture.BedGooPerSecond <= 0f)
            return;

        float chance = Mathf.Clamp01(
            spawnChance * (1f + FoodBonusManager.Instance.EventChanceBonus));
        if (UnityEngine.Random.value <= chance)
            Spawn();
    }

    // ---- Evento ----

    private void Spawn()
    {
        VisualElement root = GetRoot();
        if (root == null)
            return;

        ClearEvent();

        float rootW = root.resolvedStyle.width;
        float rootH = root.resolvedStyle.height;
        if (rootW <= 100f || rootH <= 100f)
            return;

        // Ao lado da Slime, com desvio aleatorio.
        float slimeCenterX = rootW * 0.5f;
        Button slimeBtn = root.Q<Button>("SlimeButton");
        if (slimeBtn != null)
        {
            Rect bounds = slimeBtn.worldBound;
            if (bounds.width > 0f)
                slimeCenterX = bounds.x + bounds.width * 0.5f;
        }

        const float boxWidth = 72f;
        startX = Mathf.Clamp(
            slimeCenterX + UnityEngine.Random.Range(-150f, 150f),
            20f,
            Mathf.Max(21f, rootW - boxWidth - 20f));
        startY = -90f;                                   // comeca fora do topo
        endY = Mathf.Max(startY + 120f, rootH - 160f);   // chao acima da barra

        box = new VisualElement();
        box.AddToClassList("parachute-box");
        box.style.position = Position.Absolute;
        box.style.width = boxWidth;

        var chute = new Label("🪂");
        chute.AddToClassList("parachute-chute");
        var cargo = new Label("📦");
        cargo.AddToClassList("parachute-cargo");
        box.Add(chute);
        box.Add(cargo);

        // TrickleDown: o clique conta na pressao (mesmo criterio do slime).
        box.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 1)
                return;
            evt.StopPropagation();
            Claim();
        }, TrickleDown.TrickleDown);

        root.Add(box);

        fallElapsed = 0f;
        SetBoxTop(startY);
        falling = true;
        fallItem = box.schedule.Execute(FallTick).Every(16);
    }

    private void FallTick()
    {
        if (!falling || box == null)
            return;

        fallElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(fallElapsed / fallSeconds);
        float eased = Mathf.Pow(t, fallGravityPower);
        float sway = Mathf.Sin(fallElapsed * 3f) * swayPixels;
        box.style.left = startX + sway;
        SetBoxTop(Mathf.LerpUnclamped(startY, endY, eased));

        if (t >= 1f)
            Miss();
    }

    private void Claim()
    {
        FurnitureManager furniture = FurnitureManager.Instance;
        float reward = (furniture != null ? furniture.BedGooPerSecond : 0f) * bonusMultiplier;
        Vector2 at = box != null ? box.worldBound.center : default;
        ClearEvent();

        if (reward <= 0f)
            return;

        SlimeManager slime = SlimeManager.Instance;
        if (slime == null)
            return;

        slime.AddGooPassive(reward);
        SaveManager.Instance?.RequestSave();
        SpawnFloatLabel(at, $"+{FurnitureShopUI.FormatGoo(reward)}", false);
        Claimed?.Invoke(reward);
        Debug.Log($"[Parachute] Premio: +{reward:0.##} gosma (producao {reward / bonusMultiplier:0.##}/s x {bonusMultiplier:0}).");
    }

    private void Miss()
    {
        Vector2 at = box != null ? box.worldBound.center : default;
        ClearEvent();
        SpawnFloatLabel(at, "💥", true);
    }

    // ---- UI ----

    private void SetBoxTop(float top)
    {
        if (box != null)
            box.style.top = top;
    }

    private void ClearEvent()
    {
        falling = false;
        fallItem?.Pause();
        fallItem = null;
        if (box != null)
        {
            box.RemoveFromHierarchy();
            box = null;
        }
    }

    private void SpawnFloatLabel(Vector2 at, string text, bool isMiss)
    {
        VisualElement root = GetRoot();
        if (root == null)
            return;

        var label = new Label(text);
        label.AddToClassList("parachute-float");
        if (isMiss)
            label.AddToClassList("parachute-float-miss");
        label.style.position = Position.Absolute;
        label.style.left = at.x - 70f;
        label.style.top = at.y - 24f;
        label.style.width = 140f;
        root.Add(label);

        float elapsed = 0f;
        label.schedule.Execute(() =>
        {
            elapsed += 0.05f;
            label.style.top = at.y - 24f - elapsed * 42f;
            label.style.opacity = Mathf.Clamp01(1f - elapsed);
            if (elapsed >= 1f)
                label.RemoveFromHierarchy();
        }).Every(50);
    }

    private VisualElement GetRoot()
    {
        if (document == null)
            document = FindFirstObjectByType<UIDocument>();
        return document != null ? document.rootVisualElement : null;
    }
}
