using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// HUD principal da nova cena UGUI (MainScene) - port do MainHUDController
/// (UI Toolkit), mantendo as duas cenas funcionando em paralelo.
///
/// A cena tem 3 painéis (Room, Room (1), Room (2)) com um RawImage da slime
/// cada, mas UM único HUD compartilhado (SlimeConter, LvlTMP, medidores).
/// Este controlador: anexa SlimeClickRelay no ClickButton invisível (ou no
/// RawImage como fallback nos painéis ainda sem botão), alimenta os
/// elementos do HUD e empurra estado para os 3 RoomControllers (todos
/// apontam para os mesmos medidores/barras).
///
/// Fase 1: clique na slime, contadores, medidores, barra do comodo, borda
/// dourada da euforia e audio (SFX + musica + mute). Loja, comida, stickers,
/// skins e paraquedas entram na Fase 2.
/// </summary>
public class MainHUDUGUIController : MonoBehaviour
{
    public static MainHUDUGUIController Instance { get; private set; }

    [Header("Referencias (auto-find quando vazio)")]
    public SlimeManager slimeManager;
    public TMP_Text slimeCounter;   // "x 123"
    public TMP_Text levelLabel;     // "Nv 3"
    public Transform slimeRoot;     // Slimes3D (borda da euforia)

    [Header("Audio - SFX")]
    public AudioClip slimeClip;
    public AudioClip clickClip;
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioClip buyClip;
    [Range(0f, 1f)] public float uiVolume = 0.5f;

    [Header("Audio - Musica de fundo")]
    public AudioClip musicClip;
    [Range(0f, 1f)] public float musicVolume = 0.25f;

    [Header("Euforia")]
    [Tooltip("Cor da borda (Outline) da slime durante a euforia.")]
    public Color euphoriaColor = new Color(1f, 0.7921569f, 0.2745098f); // rgb(255,202,70)

    // ---- estado interno ----
    private int _slimeCount;
    private int _level = 1;
    private float _levelProgress;
    private bool _muted;
    private bool _euphoriaActive;
    private float _euphoriaCheckTimer;

    private RoomController[] _roomControllers = new RoomController[0];
    private FurnitureManager _furniture;
    private SaveManager _subscribedSaveManager;
    private AudioSource _audio;
    private AudioSource _musicSource;
    private GameObject _musicSourceObject;
    private readonly Dictionary<string, AudioClip> _synthCache = new();

    private Outline[] _outlines;
    private Color[] _outlinesOriginal;

    // =========================================
    // CICLO DE VIDA
    // =========================================

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (slimeManager != null)
            slimeManager.StateChanged += HandleStateChanged;

        _subscribedSaveManager = SaveManager.Instance;
        if (_subscribedSaveManager != null)
            _subscribedSaveManager.SaveDataChanged += HandleSaveDataChanged;

        foreach (var rc in _roomControllers)
        {
            if (rc != null)
                rc.MuteChanged += HandleMuteChanged;
        }

        if (_furniture != null)
            _furniture.StateChanged += HandleStateChanged;

        StartBackgroundMusic();
        PullFromManager();
    }

    private void OnDisable()
    {
        if (slimeManager != null)
            slimeManager.StateChanged -= HandleStateChanged;
        if (_subscribedSaveManager != null)
            _subscribedSaveManager.SaveDataChanged -= HandleSaveDataChanged;
        _subscribedSaveManager = null;
        foreach (var rc in _roomControllers)
        {
            if (rc != null)
                rc.MuteChanged -= HandleMuteChanged;
        }
        if (_furniture != null)
            _furniture.StateChanged -= HandleStateChanged;
        StopBackgroundMusic();
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
        if (slimeCounter == null)
            slimeCounter = FindComponentByName<TMP_Text>("SlimeConter");
        if (levelLabel == null)
            levelLabel = FindComponentByName<TMP_Text>("LvlTMP");
        if (slimeRoot == null)
        {
            Transform t = FindTransformByName("Slimes3D");
            if (t != null)
                slimeRoot = t;
        }

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
        if (slimeCounter == null || levelLabel == null)
        {
            Debug.LogWarning(
                "[MainHUDUGUI] Elementos do HUD nao encontrados: " +
                $"counter={(slimeCounter != null)}, level={(levelLabel != null)}", this);
        }

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

    private static T FindComponentByName<T>(string name) where T : Component
    {
        Transform t = FindTransformByName(name);
        return t != null ? t.GetComponent<T>() : null;
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

    private void HandleMuteChanged(bool muted)
    {
        _muted = muted;
        AudioListener.pause = muted;
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

        _slimeCount = Mathf.FloorToInt(save != null ? save.goo : slimeManager.Goo);

        if (slimeCounter != null)
            slimeCounter.text = $"x {_slimeCount}";
        if (levelLabel != null)
            levelLabel.text = $"Nv {_level}";

        foreach (var rc in _roomControllers)
        {
            if (rc == null)
                continue;
            rc.SetLevelProgress(_levelProgress);
            rc.SetMeasures(
                slimeManager.Hunger, slimeManager.Fun, slimeManager.Hygiene);
        }

        RefreshEuphoria();
    }

    private void RefreshEuphoria(bool force = false)
    {
        if (slimeManager == null || _outlines == null || _outlines.Length == 0)
            return;

        bool euphoric = slimeManager.IsEuphoric;
        if (!force && euphoric == _euphoriaActive)
            return;

        _euphoriaActive = euphoric;
        for (int i = 0; i < _outlines.Length; i++)
        {
            if (_outlines[i] == null)
                continue;
            _outlines[i].outlineColor = euphoric
                ? euphoriaColor
                : _outlinesOriginal[i];
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
        PlaySlime();
    }

    // =========================================
    // AUDIO (SFX + musica + mute)
    // =========================================

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (_musicSource != null)
            _musicSource.volume = musicVolume;
    }

    private AudioSource GetMusicSource()
    {
        if (_musicSource != null)
            return _musicSource;

        _musicSourceObject = new GameObject("BackgroundMusic");
        _musicSourceObject.transform.SetParent(transform, false);
        _musicSource = _musicSourceObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicSource.volume = musicVolume;
        return _musicSource;
    }

    private void StartBackgroundMusic()
    {
        if (!Application.isPlaying || musicClip == null)
            return;

        AudioSource source = GetMusicSource();
        source.clip = musicClip;
        source.loop = true;
        source.volume = musicVolume;
        if (!source.isPlaying)
            source.Play();
    }

    private void StopBackgroundMusic()
    {
        if (_musicSource != null && _musicSource.isPlaying)
            _musicSource.Stop();
    }

    private AudioSource Audio
    {
        get
        {
            if (_audio == null)
            {
                _audio = GetComponent<AudioSource>();
                if (_audio == null)
                    _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
            }
            return _audio;
        }
    }

    public void PlayClick() => PlayClip(clickClip, "ui-click", 720f, 540f, 0.07f, 0.35f);

    public void PlaySlime()
    {
        if (slimeClip != null)
        {
            PlayClip(slimeClip);
        }
        else
        {
            PlayClip(null, "ui-pop", 300f, 660f, 0.16f, 0.5f);
            PlayClip(null, "ui-pop-hi", 660f, 990f, 0.1f, 0.25f);
        }
    }

    public void PlayOpen() => PlayClip(openClip, "ui-open", 440f, 680f, 0.12f, 0.35f);
    public void PlayClose() => PlayClip(closeClip, "ui-close", 520f, 330f, 0.12f, 0.35f);
    public void PlayBuy() => PlayClip(buyClip, "ui-buy", new[] { 523f, 659f, 784f }, 0.09f, 0.4f);

    private void PlayClip(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null || _muted)
            return;
        Audio.volume = uiVolume;
        Audio.PlayOneShot(clip);
    }

    private void PlayClip(AudioClip clip, string key, float f0, float f1, float dur, float vol)
    {
        PlayClip(clip != null ? clip : SynthTone(key, f0, f1, dur, vol));
    }

    private void PlayClip(AudioClip clip, string key, float[] notes, float noteDur, float vol)
    {
        PlayClip(clip != null ? clip : SynthMelody(key, notes, noteDur, vol));
    }

    private AudioClip SynthTone(string key, float f0, float f1, float dur, float vol)
    {
        if (_synthCache.TryGetValue(key, out var cached))
            return cached;
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * dur));
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / rate;
            float env = Mathf.Exp(-4f * t) * Mathf.Min(1f, t * 40f);
            data[i] = Mathf.Sin(phase) * env * vol;
        }
        var clip = AudioClip.Create(key, n, 1, rate, false);
        clip.SetData(data, 0);
        _synthCache[key] = clip;
        return clip;
    }

    private AudioClip SynthMelody(string key, float[] notes, float noteDur, float vol)
    {
        if (_synthCache.TryGetValue(key, out var cached))
            return cached;
        const int rate = 44100;
        int per = Mathf.Max(1, (int)(rate * noteDur));
        int n = per * notes.Length;
        var data = new float[n];
        for (int m = 0; m < notes.Length; m++)
        {
            float phase = 0f;
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / per;
                phase += 2f * Mathf.PI * notes[m] / rate;
                float env = Mathf.Exp(-3f * t) * Mathf.Min(1f, t * 40f);
                data[m * per + i] = Mathf.Sin(phase) * env * vol;
            }
        }
        var clip = AudioClip.Create(key, n, 1, rate, false);
        clip.SetData(data, 0);
        _synthCache[key] = clip;
        return clip;
    }
}
