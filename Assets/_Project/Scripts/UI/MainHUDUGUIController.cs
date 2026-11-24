using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD principal da nova cena UGUI (MainScene) - port do MainHUDController
/// (UI Toolkit), mantendo as duas cenas funcionando em paralelo.
///
/// A cena tem 3 painéis (Room, Room (1), Room (2)) com um RawImage da slime
/// cada, mas UM único HUD compartilhado (SlimeConter, LvlTMP, medidores).
/// Este controlador: anexa SlimeClickRelay no ClickButton invisível (ou no
/// RawImage como fallback nos painéis ainda sem botão), alimenta os
/// elementos do HUD e é o dono dos medidores e dos balões de medida.
/// O áudio (SFX, música, volumes e mute) vive no GameAudioManager; aqui só
/// ficam os ícones dos botões de SFX e música, que reagem aos eventos
/// SfxMuteChanged e MusicMuteChanged.
/// Os RoomControllers ficam apenas com a barra de nível do comodo; os painéis
/// Album/Shop/Food são abertos pelo VerticalNavegationController.
/// </summary>
public class MainHUDUGUIController : MonoBehaviour
{
    public static MainHUDUGUIController Instance { get; private set; }

    [Header("Referencias (auto-find quando vazio)")]
    public SlimeManager slimeManager;
    public TMP_Text slimeCounter;   // "x 123"
    public TMP_Text levelLabel;     // "Nv 3"
    public Transform slimeRoot;     // Slimes3D (borda da euforia)

    [Header("Euforia")]
    [Tooltip("Cor da borda (Outline) da slime durante a euforia.")]
    public Color euphoriaColor = new Color(1f, 0.7921569f, 0.2745098f); // rgb(255,202,70)
    [Tooltip("Image do SlimeConter: troca para o fundo dourado na euforia.")]
    [SerializeField] private Image slimeCounterImage;
    [SerializeField] private Sprite slimeBgNormalSprite;
    [SerializeField] private Sprite slimeBgGoldSprite;
    [Tooltip("TMP dentro do SlimeConter que mostra o multiplicador atual.")]
    [SerializeField] private TMP_Text multiplierText;

    [Header("Measurements")]
    [Tooltip("Ordem: [HapinessFill, HungerFill, HygieneFill]. A cor de cada fill e a do Inspector; so o fillAmount e alterado.")]
    [SerializeField] private Image[] measureImages = new Image[3];
    [SerializeField] private float[] measureValues = new float[3];
    [SerializeField] private float[] measureMaxValues = new float[3] { 100f, 100f, 100f };

    [Header("Measurement Dialog Bubbles")]
    [SerializeField] private GameObject happinessBubble;
    [SerializeField] private GameObject hygieneBubble;
    [SerializeField] private GameObject hungerBubble;
    [SerializeField] private float bubbleDuration = 2f;

    [Header("Hygiene - Baratas")]
    [SerializeField] private HygieneCockroachManager hygieneCockroaches;

    [Header("Audio - Icones dos botoes (visual)")]
    [SerializeField] private GameObject soundIcon;
    [SerializeField] private GameObject noSoundIcon;
    [SerializeField] private GameObject musicIcon;
    [SerializeField] private GameObject noMusicIcon;

    // ---- estado interno ----
    private int _level = 1;
    private float _levelProgress;
    private bool _euphoriaActive;
    private bool _temporaryEuphoriaVisual;
    private bool _multiplierTextVisible;
    private float _euphoriaCheckTimer;

    private RoomController[] _roomControllers = new RoomController[0];
    private FurnitureManager _furniture;
    private SaveManager _subscribedSaveManager;
    private Coroutine _bubbleCoroutine;

    private Outline[] _outlines;
    private Color[] _outlinesOriginal;

    // =========================================
    // CICLO DE VIDA
    // =========================================

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
        UpdateAllMensure();
        UpdateSoundVisual();
        UpdateMusicVisual();
        HideAllBubbles();
    }

    private void OnEnable()
    {
        if (slimeManager != null)
            slimeManager.StateChanged += HandleStateChanged;

        _subscribedSaveManager = SaveManager.Instance;
        if (_subscribedSaveManager != null)
            _subscribedSaveManager.SaveDataChanged += HandleSaveDataChanged;

        if (_furniture != null)
            _furniture.StateChanged += HandleStateChanged;

        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.SfxMuteChanged += HandleSfxMuteChanged;
            GameAudioManager.Instance.MusicMuteChanged += HandleMusicMuteChanged;
        }
        UpdateSoundVisual();
        UpdateMusicVisual();

        PullFromManager();
    }

    private void OnDisable()
    {
        if (slimeManager != null)
            slimeManager.StateChanged -= HandleStateChanged;
        if (_subscribedSaveManager != null)
            _subscribedSaveManager.SaveDataChanged -= HandleSaveDataChanged;
        _subscribedSaveManager = null;
        if (_furniture != null)
            _furniture.StateChanged -= HandleStateChanged;
        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.SfxMuteChanged -= HandleSfxMuteChanged;
            GameAudioManager.Instance.MusicMuteChanged -= HandleMusicMuteChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Checagem periodica da euforia (borda acompanha o estado).
        _euphoriaCheckTimer += Time.unscaledDeltaTime;
        if (_euphoriaCheckTimer >= 0.5f)
        {
            _euphoriaCheckTimer = 0f;
            RefreshEuphoria();
        }
    }

    // =========================================
    // REFERENCIAS (auto-find)
    // =========================================

    private void ResolveReferences()
    {
        if (slimeManager == null)
            slimeManager = SlimeManager.Instance;
        Transform counterRoot = FindTransformByName("SlimeConter");
        if (slimeCounter == null)
        {
            // O TMP do contador esta no filho "text" do SlimeConter (o pai so
            // tem Image) — o GetComponent direto no pai retornava null e o
            // texto ficava sempre vazio.
            if (counterRoot != null)
                slimeCounter = counterRoot.GetComponentInChildren<TMP_Text>(true);
        }
        if (slimeCounterImage == null && counterRoot != null)
            slimeCounterImage = counterRoot.GetComponent<Image>();
        if (slimeBgNormalSprite == null && slimeCounterImage != null)
            slimeBgNormalSprite = slimeCounterImage.sprite;
        if (multiplierText == null && counterRoot != null)
        {
            Transform t = counterRoot.Find("MultiplierText");
            if (t != null)
                multiplierText = t.GetComponent<TMP_Text>();
        }
        if (slimeBgGoldSprite == null)
        {
            foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite.name == "SlimeBG_Gold_0")
                {
                    slimeBgGoldSprite = sprite;
                    break;
                }
            }
        }
        if (levelLabel == null)
        {
            // Busca APENAS dentro do HUD: o "LvlTMP" da cena e o do popup de
            // movel (unico existente) — antes o HUD escrevia "Nv X" dentro do
            // popup, sobrescrevendo o nivel do movel.
            levelLabel = FindInChildrenByName<TMP_Text>("LvlTMP");
        }
        if (slimeRoot == null)
        {
            Transform t = FindTransformByName("Slimes3D");
            if (t != null)
                slimeRoot = t;
        }

        if (soundIcon == null)
            soundIcon = FindTransformByName("SoundIcon")?.gameObject;
        if (noSoundIcon == null)
            noSoundIcon = FindTransformByName("NoSoundIcon")?.gameObject;
        if (musicIcon == null)
            musicIcon = FindTransformByName("MusicIcon")?.gameObject;
        if (noMusicIcon == null)
            noMusicIcon = FindTransformByName("NoMusicIcon")?.gameObject;

        if (hygieneCockroaches == null)
            hygieneCockroaches = FindFirstObjectByType<HygieneCockroachManager>();

        _furniture = FurnitureManager.Instance;
        _roomControllers = FindObjectsByType<RoomController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        // Clique na slime: prefere o ClickButton invisível (área menor que a
        // imagem, que é imensa). O RawImage do mesmo painel fica com raycast
        // desligado, senão ele (desenhado por cima) interceptaria o clique.
        // Painéis ainda sem botão mantêm o relay no RawImage (fallback).
        int relays = 0;
        foreach (var t in FindTransformsByName("ClickButton"))
        {
            var relay = t.GetComponent<SlimeClickRelay>();
            if (relay == null)
                relay = t.gameObject.AddComponent<SlimeClickRelay>();
            relay.Init(this);
            // O pop de escala vale para a imagem da slime (irmã do botão).
            relay.popTarget = t.parent != null ? t.parent.Find("RawImage") : null;
            relays++;
        }

        foreach (var t in FindTransformsByName("RawImage"))
        {
            if (t.parent != null && t.parent.Find("ClickButton") != null)
            {
                var raw = t.GetComponent<UnityEngine.UI.RawImage>();
                if (raw != null && raw.raycastTarget)
                    raw.raycastTarget = false;
                continue;
            }

            var fallback = t.GetComponent<SlimeClickRelay>();
            if (fallback == null)
                fallback = t.gameObject.AddComponent<SlimeClickRelay>();
            fallback.Init(this);
            relays++;
        }

        if (relays == 0)
            Debug.LogWarning("[MainHUDUGUI] Nenhum ClickButton nem RawImage da slime encontrado.", this);
        if (slimeManager == null)
            Debug.LogWarning("[MainHUDUGUI] SlimeManager nao encontrado na cena.", this);
        if (_roomControllers.Length == 0)
            Debug.LogWarning("[MainHUDUGUI] Nenhum RoomController na cena.", this);
        if (slimeCounter == null)
        {
            Debug.LogWarning(
                "[MainHUDUGUI] SlimeConter do HUD nao encontrado.", this);
        }
        // levelLabel e opcional: a cena ainda nao tem rotulo de nivel no HUD
        // (sem ele o HUD simplesmente nao escreve "Nv X" em lugar nenhum).

        CollectOutlines();
    }

    private static Transform FindTransformByName(string name)
    {
        foreach (var t in FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }

    private T FindInChildrenByName<T>(string name) where T : Component
    {
        foreach (var c in GetComponentsInChildren<T>(true))
        {
            if (c.name == name)
                return c;
        }
        return null;
    }

    private static List<Transform> FindTransformsByName(string name)
    {
        var result = new List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
                result.Add(t);
        }
        return result;
    }

    private void CollectOutlines()
    {
        var all = FindObjectsByType<Outline>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<Outline>();

        // Prefere os Outlines dentro de Slimes3D (a slime principal).
        if (slimeRoot != null)
        {
            foreach (var o in all)
            {
                if (o != null && o.transform.IsChildOf(slimeRoot))
                    list.Add(o);
            }
        }
        if (list.Count == 0)
            list.AddRange(all);

        _outlines = list.ToArray();
        _outlinesOriginal = new Color[_outlines.Length];
        for (int i = 0; i < _outlines.Length; i++)
            _outlinesOriginal[i] = _outlines[i] != null
                ? _outlines[i].outlineColor
                : Color.white;
    }

    // =========================================
    // EVENTOS DOS MANAGERS
    // =========================================

    private void HandleStateChanged()
    {
        if (isActiveAndEnabled)
            PullFromManager();
    }

    private void HandleSaveDataChanged(SaveData data)
    {
        if (isActiveAndEnabled)
            PullFromManager();
    }

    private void HandleSfxMuteChanged(bool muted)
    {
        UpdateSoundVisual();
    }

    private void HandleMusicMuteChanged(bool muted)
    {
        UpdateMusicVisual();
    }

    // =========================================
    // PUXAR ESTADO -> UI
    // =========================================

    /// <summary>Puxa gosma, nivel e cuidados do SlimeManager para a UI.</summary>
    public void PullFromManager()
    {
        if (slimeManager == null)
            return;

        SaveManager saveManager = SaveManager.Instance;
        SaveData save = saveManager != null ? saveManager.GetSaveData() : null;

        if (_furniture != null)
        {
            // A barra do topo acompanha o nivel do Comodo (GDD) e o
            // progresso de gosma para o proximo nivel.
            _level = _furniture.ComodoLevel;
            float nextCost = _furniture.ComodoUpgradeCost();
            _levelProgress = nextCost <= 0f
                ? 1f
                : Mathf.Clamp01(slimeManager.Goo / nextCost);
        }
        else if (save != null)
        {
            _level = Mathf.Max(1, save.objectLevel);
        }

        if (slimeCounter != null)
        {
            // Numeracao compacta de clicker (1.23K, 45.6M...) direto do saldo
            // em float, sem estourar int em valores gigantes.
            slimeCounter.text = FurnitureShopUI.FormatCompact(
                save != null ? save.goo : slimeManager.Goo);
        }
        if (levelLabel != null)
            levelLabel.text = $"Nv {_level}";

        // Medidores e baloes pertencem a este HUD; as salas apenas mantem
        // a barra de nivel do comodo.
        SetMeasures(
            slimeManager.Hunger, slimeManager.Fun, slimeManager.Hygiene);

        if (hygieneCockroaches != null)
            hygieneCockroaches.Refresh();

        foreach (var rc in _roomControllers)
        {
            if (rc == null)
                continue;
            rc.SetLevelProgress(_levelProgress);
        }

        RefreshEuphoria();
    }

    /// <summary>
    /// Borda dourada temporaria do bonus do presente. Nao altera o estado da
    /// euforia real: se ela estiver ativa, a borda continua dourada ao final.
    /// </summary>
    public void SetTemporaryEuphoriaVisual(bool active)
    {
        _temporaryEuphoriaVisual = active;
        RefreshEuphoria(true);
    }

    private void RefreshEuphoria(bool force = false)
    {
        if (slimeManager == null)
            return;

        bool realEuphoria = slimeManager.IsEuphoric;
        // O visual dourado tambem vale para o bonus do presente; o texto de
        // multiplicador e exclusivo da euforia real.
        bool euphoric = _temporaryEuphoriaVisual || realEuphoria;
        bool showMultiplier = realEuphoria;
        bool stateChanged = force || euphoric != _euphoriaActive ||
            showMultiplier != _multiplierTextVisible;

        if (stateChanged)
        {
            _euphoriaActive = euphoric;
            _multiplierTextVisible = showMultiplier;
            if (_outlines != null)
            {
                for (int i = 0; i < _outlines.Length; i++)
                {
                    if (_outlines[i] == null)
                        continue;
                    _outlines[i].outlineColor = euphoric
                        ? euphoriaColor
                        : _outlinesOriginal[i];
                }
            }

            if (slimeCounterImage != null)
            {
                slimeCounterImage.sprite = euphoric && slimeBgGoldSprite != null
                    ? slimeBgGoldSprite
                    : slimeBgNormalSprite;
            }
            if (multiplierText != null)
                multiplierText.gameObject.SetActive(showMultiplier);
        }

        // Atualiza mesmo sem troca de estado: o tier da euforia muda a cada
        // 25% de carga, entao o multiplicador pode mudar com o tempo.
        if (showMultiplier && multiplierText != null)
        {
            multiplierText.text = "x" + slimeManager.EuphoriaMultiplier()
                .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // =========================================
    // CLIQUE NA SLIME (chamado pelo SlimeClickRelay)
    // =========================================

    public void OnSlimePointerDown()
    {
        if (slimeManager != null)
        {
            slimeManager.RegisterClick();
            PullFromManager();
        }
        GameAudioManager.Instance?.PlaySlime();
    }

    // =========================================
    // MEASUREMENTS
    // =========================================

    /// <summary>
    /// Alimenta os medidores com os cuidados do slime (0-1).
    /// Ordem interna de measureImages: [HapinessFill, HungerFill, HygieneFill].
    /// O fill mostra a necessidade pendente: quanto mais atendido o cuidado,
    /// menor o fillAmount. A cor vem configurada na cena e nao e alterada.
    /// </summary>
    public void SetMeasures(float hunger, float fun, float hygiene)
    {
        if (measureValues == null || measureValues.Length < 3)
            return;

        measureValues[0] = Mathf.Clamp01(fun) * 100f;
        measureValues[1] = Mathf.Clamp01(hunger) * 100f;
        measureValues[2] = Mathf.Clamp01(hygiene) * 100f;
        UpdateAllMensure();
    }

    public void UpdateAllMensure()
    {
        if (measureImages == null || measureValues == null)
            return;

        int count = Mathf.Min(measureImages.Length, measureValues.Length);
        for (int i = 0; i < count; i++)
        {
            if (measureImages[i] == null)
                continue;

            float maxValue =
                measureMaxValues != null && i < measureMaxValues.Length
                    ? measureMaxValues[i]
                    : 100f;

            float percentage = maxValue > 0f
                ? measureValues[i] / maxValue
                : 0f;

            // Necessidade pendente: 0% atendido = anel cheio;
            // 100% atendido = anel vazio. A cor permanece a da cena.
            measureImages[i].fillAmount = Mathf.Clamp01(1f - percentage);
        }
    }

    // =========================================
    // MEASUREMENT DIALOG BUBBLES
    // =========================================

    public void MensureHappinessBtt() => ToggleBubble(happinessBubble);

    public void MensureHygieneBtt() => ToggleBubble(hygieneBubble);

    public void MensureHungerBtt() => ToggleBubble(hungerBubble);

    /// <summary>Clique no proprio dialogo tambem fecha o balao.</summary>
    public void HideBubbles()
    {
        if (_bubbleCoroutine != null)
        {
            StopCoroutine(_bubbleCoroutine);
            _bubbleCoroutine = null;
        }
        HideAllBubbles();
    }

    private void ToggleBubble(GameObject bubble)
    {
        if (bubble == null)
            return;

        // Clique repetido no mesmo botao/dialogo esconde o balao.
        if (bubble.activeSelf)
        {
            HideBubbles();
            return;
        }

        ShowBubble(bubble);
    }

    private void ShowBubble(GameObject bubble)
    {
        // StartCoroutine falha em MonoBehaviour de GameObject inativo.
        if (bubble == null || !isActiveAndEnabled)
            return;

        if (_bubbleCoroutine != null)
        {
            StopCoroutine(_bubbleCoroutine);
            _bubbleCoroutine = null;
        }

        HideAllBubbles();
        bubble.SetActive(true);
        _bubbleCoroutine = StartCoroutine(HideBubbleAfterDelay(bubble));
    }

    private IEnumerator HideBubbleAfterDelay(GameObject bubble)
    {
        yield return new WaitForSeconds(bubbleDuration);

        if (bubble != null)
            bubble.SetActive(false);

        _bubbleCoroutine = null;
    }

    private void HideAllBubbles()
    {
        if (happinessBubble != null)
            happinessBubble.SetActive(false);
        if (hungerBubble != null)
            hungerBubble.SetActive(false);
        if (hygieneBubble != null)
            hygieneBubble.SetActive(false);
    }

    // =========================================
    // AUDIO - APENAS VISUAL DOS BOTOES
    // =========================================

    private void UpdateSoundVisual()
    {
        bool muted = GameAudioManager.Instance == null ||
                     GameAudioManager.Instance.IsSfxMuted;

        if (soundIcon != null)
            soundIcon.SetActive(!muted);
        if (noSoundIcon != null)
            noSoundIcon.SetActive(muted);
    }

    private void UpdateMusicVisual()
    {
        bool muted = GameAudioManager.Instance == null ||
                     GameAudioManager.Instance.IsMusicMuted;

        if (musicIcon != null)
            musicIcon.SetActive(!muted);
        if (noMusicIcon != null)
            noMusicIcon.SetActive(muted);
    }

}
