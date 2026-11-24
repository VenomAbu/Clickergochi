using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dono único do áudio do jogo: clipes de UI, síntese de fallback, música,
/// volumes e mute independente de SFX e música.
///
/// Os controladores de HUD escutam SfxMuteChanged e MusicMuteChanged para
/// atualizar seus ícones. MuteChanged continua existindo como alias legado
/// do estado de SFX, para cenas antigas que ainda usam ToggleMute().
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Clipes de UI (vazio = sintetizado)")]
    [SerializeField] private AudioClip slimeClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip buyClip;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float uiVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.25f;

    [Header("Música de fundo")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private bool startMusicOnAwake = true;

    [Header("Estado inicial")]
    [SerializeField] private bool startSfxMuted = false;
    [SerializeField] private bool startMusicMuted = false;

    /// <summary>Legado: acompanha o estado de SFX.</summary>
    public event Action<bool> MuteChanged;
    public event Action<bool> SfxMuteChanged;
    public event Action<bool> MusicMuteChanged;

    public bool IsMuted => _sfxMuted;
    public bool IsSfxMuted => _sfxMuted;
    public bool IsMusicMuted => _musicMuted;
    public float UIVolume => uiVolume;
    public float MusicVolume => musicVolume;

    private bool _sfxMuted;
    private bool _musicMuted;
    private AudioSource _sfxSource;
    private AudioSource _musicSource;
    private GameObject _musicSourceObject;
    private readonly Dictionary<string, AudioClip> _synthCache = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxMuted = startSfxMuted;
        _musicMuted = startMusicMuted;

        _sfxSource = GetComponent<AudioSource>();
        if (_sfxSource == null)
            _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = uiVolume;
        _sfxSource.mute = _sfxMuted;

        EnsureMusicSource();
        ApplyMusicState();
    }

    private void Start()
    {
        // Limpa qualquer pausa global legada; agora SFX e musica sao
        // silenciados nos proprios AudioSource.
        AudioListener.pause = false;

        if (startMusicOnAwake)
            StartMusic();

        ApplyMusicState();
        SfxMuteChanged?.Invoke(_sfxMuted);
        MuteChanged?.Invoke(_sfxMuted);
        MusicMuteChanged?.Invoke(_musicMuted);
    }

    private void OnValidate()
    {
        uiVolume = Mathf.Clamp01(uiVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        if (_sfxSource != null)
        {
            _sfxSource.volume = uiVolume;
            _sfxSource.mute = _sfxMuted;
        }
        ApplyMusicState();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        AudioListener.pause = false;

        if (_musicSourceObject != null)
            Destroy(_musicSourceObject);
    }

    // =========================================
    // SFX
    // =========================================

    public void PlaySlime()
    {
        if (slimeClip != null)
        {
            PlayClip(slimeClip);
            return;
        }

        PlayClip(null, "ui-pop", 300f, 660f, 0.16f, 0.5f);
        PlayClip(null, "ui-pop-hi", 660f, 990f, 0.1f, 0.25f);
    }

    public void PlayClick() =>
        PlayClip(clickClip, "ui-click", 720f, 540f, 0.07f, 0.35f);

    public void PlayOpen() =>
        PlayClip(openClip, "ui-open", 440f, 680f, 0.12f, 0.35f);

    public void PlayClose() =>
        PlayClip(closeClip, "ui-close", 520f, 330f, 0.12f, 0.35f);

    public void PlayBuy() =>
        PlayClip(buyClip, "ui-buy", new[] { 523f, 659f, 784f }, 0.09f, 0.4f);

    /// <summary>Toca um clipe avulso respeitando volume e mute.</summary>
    public void PlayClip(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null || _sfxMuted || _sfxSource == null)
            return;

        _sfxSource.volume = uiVolume;
        _sfxSource.PlayOneShot(clip);
    }

    private void PlayClip(AudioClip clip, string key, float f0, float f1, float dur, float vol) =>
        PlayClip(clip != null ? clip : SynthTone(key, f0, f1, dur, vol));

    private void PlayClip(AudioClip clip, string key, float[] notes, float noteDur, float vol) =>
        PlayClip(clip != null ? clip : SynthMelody(key, notes, noteDur, vol));

    // =========================================
    // MUSICA
    // =========================================

    private void EnsureMusicSource()
    {
        if (_musicSource != null)
            return;

        _musicSourceObject = new GameObject("BackgroundMusic");
        _musicSourceObject.transform.SetParent(transform, false);
        _musicSource = _musicSourceObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        ApplyMusicState();
    }

    public void StartMusic()
    {
        if (!Application.isPlaying || musicClip == null)
            return;

        EnsureMusicSource();
        _musicSource.clip = musicClip;
        _musicSource.loop = true;
        ApplyMusicState();
        if (!_musicSource.isPlaying)
            _musicSource.Play();
    }

    public void StopMusic()
    {
        if (_musicSource != null && _musicSource.isPlaying)
            _musicSource.Stop();
    }

    // =========================================
    // VOLUME / MUTE
    // =========================================

    public void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        if (_sfxSource != null)
            _sfxSource.volume = uiVolume;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyMusicState();
    }

    private void ApplyMusicState()
    {
        if (_musicSource == null)
            return;

        _musicSource.volume = _musicMuted ? 0f : musicVolume;
        if (!_musicMuted && Application.isPlaying && musicClip != null &&
            !_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
    }

    public void SetSfxMuted(bool muted)
    {
        if (_sfxMuted == muted)
            return;

        _sfxMuted = muted;
        if (_sfxSource != null)
            _sfxSource.mute = muted;

        SfxMuteChanged?.Invoke(muted);
        MuteChanged?.Invoke(muted);
    }

    public void ToggleSfxMute() => SetSfxMuted(!_sfxMuted);

    public void SetMusicMuted(bool muted)
    {
        if (_musicMuted == muted)
            return;

        _musicMuted = muted;
        ApplyMusicState();
        MusicMuteChanged?.Invoke(muted);
    }

    public void ToggleMusicMute() => SetMusicMuted(!_musicMuted);

    /// <summary>Legado: alterna SFX e musica juntos.</summary>
    public void SetMuted(bool muted)
    {
        SetSfxMuted(muted);
        SetMusicMuted(muted);
    }

    /// <summary>Legado: alterna SFX e musica juntos.</summary>
    public void ToggleMute() => SetMuted(!(_sfxMuted && _musicMuted));

    // =========================================
    // SINTETIZADORES DE FALLBACK
    // =========================================

    private AudioClip SynthTone(string key, float f0, float f1, float dur, float vol)
    {
        AudioClip cached;
        if (_synthCache.TryGetValue(key, out cached))
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
        AudioClip cached;
        if (_synthCache.TryGetValue(key, out cached))
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
