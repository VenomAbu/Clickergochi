using System;
using System.Collections;
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

    [Header("Clipes por acao (mais de um = escolha aleatoria)")]
    [SerializeField] private AudioClip[] slimeClips;
    [SerializeField] private AudioClip[] openPopupClips;
    [SerializeField] private AudioClip[] closePopupClips;

    [Header("Audio da abertura do pacote")]
    [SerializeField] private AudioClip packDrumsClip;
    [SerializeField] private AudioClip comumPackClip;
    [SerializeField] private AudioClip crescente1Clip;
    [SerializeField] private AudioClip crescente2Clip;
    [SerializeField] private AudioClip crescente3Clip;
    [SerializeField] private AudioClip openPackCompleteClip;
    [SerializeField] private AudioClip packMusicClip;
    [SerializeField] private AudioClip packMusicAfterClip;

    [Header("Audio dos eventos do jogo")]
    [SerializeField] private AudioClip openPresentClip;
    [SerializeField] private AudioClip lostPresentClip;
    [SerializeField] private AudioClip danoBarataClip;
    [SerializeField] private AudioClip matarBarataClip;
    [SerializeField] private AudioClip horizontalNavigationClip;
    [SerializeField] private AudioClip purchaseClip;
    [SerializeField] private AudioClip euforia1Clip;
    [SerializeField] private AudioClip euforiaMusicClip;
    [Tooltip("O fade de EuforiaMusic termina tantos segundos depois do efeito.")]
    [SerializeField, Min(0f)] private float euforiaMusicFadeEndOffsetSeconds = 1f;
    [Tooltip("Duracao do fade out de EuforiaMusic.")]
    [SerializeField, Min(0.1f)] private float euforiaMusicFadeSeconds = 2f;

    [Header("Variacao de pitch nos SFX")]
    [SerializeField, Range(0f, 0.15f)] private float sfxPitchVariation = 0.04f;

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
    private bool _musicSuppressed;
    private AudioSource _sfxSource;
    private AudioSource[] _sfxSources;
    private int _nextSfxSource;
    private AudioSource _musicSource;
    private GameObject _musicSourceObject;
    private AudioSource[] _packSources;
    private AudioSource _packDrumsSource;
    private AudioSource _packCrescenteSource;
    private AudioSource _packMusicSource;
    private AudioSource _packOneShotSource;
    private Coroutine _packCrescenteRoutine;
    private Coroutine _packMusicRoutine;
    private int _packAudioGeneration;
    private AudioSource[] _eventSources;
    private AudioSource _euforiaMusicSource;
    private Coroutine _euforiaMusicFadeStartRoutine;
    private Coroutine _euforiaMusicFadeRoutine;
    private int _euforiaAudioGeneration;
    private float _suppressCloseSoundUntil = -1f;
    private readonly Dictionary<string, AudioClip> _synthCache = new Dictionary<string, AudioClip>();

    private const int SfxSourceCount = 6;

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

        EnsureSfxSources();
        EnsurePackSources();
        EnsureEventSources();
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
        if (_sfxSources != null)
        {
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                if (_sfxSources[i] == null)
                    continue;
                _sfxSources[i].volume = uiVolume;
                _sfxSources[i].mute = _sfxMuted;
            }
        }
        else if (_sfxSource != null)
        {
            _sfxSource.volume = uiVolume;
            _sfxSource.mute = _sfxMuted;
        }
        if (_packSources != null)
        {
            for (int i = 0; i < _packSources.Length; i++)
            {
                if (_packSources[i] == null)
                    continue;
                _packSources[i].volume = uiVolume;
                _packSources[i].mute = _sfxMuted;
            }
        }
        if (_eventSources != null)
        {
            for (int i = 0; i < _eventSources.Length; i++)
            {
                if (_eventSources[i] == null)
                    continue;
                _eventSources[i].volume = uiVolume;
                _eventSources[i].mute = _sfxMuted;
            }
        }
        ApplyMusicState();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        AudioListener.pause = false;
        StopPresentBonusAudio(true);
        StopAllPackAudio();

        if (_musicSourceObject != null)
            Destroy(_musicSourceObject);
    }

    // =========================================
    // SFX
    // =========================================

    private void EnsureSfxSources()
    {
        var sources = new List<AudioSource>();

        _sfxSource = GetComponent<AudioSource>();
        if (_sfxSource != null)
            sources.Add(_sfxSource);

        // Um pequeno pool evita que a variacao de pitch de um som altere os
        // outros one-shots que ainda estao tocando no mesmo AudioSource.
        while (sources.Count < SfxSourceCount)
            sources.Add(gameObject.AddComponent<AudioSource>());

        _sfxSource = sources[0];
        for (int i = 0; i < sources.Count; i++)
        {
            AudioSource source = sources[i];
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = uiVolume;
            source.mute = _sfxMuted;
            source.pitch = 1f;
        }

        _sfxSources = sources.ToArray();
        _nextSfxSource = 0;
    }

    private AudioSource NextSfxSource()
    {
        if (_sfxSources == null || _sfxSources.Length == 0)
            EnsureSfxSources();

        AudioSource source = _sfxSources[_nextSfxSource % _sfxSources.Length];
        _nextSfxSource = (_nextSfxSource + 1) % _sfxSources.Length;
        return source;
    }

    /// <summary>Escolhe um clipe valido; com mais de um, escolhe aleatoriamente.</summary>
    private static AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                count++;
        }

        if (count == 0)
            return null;

        int target = count == 1 ? 0 : UnityEngine.Random.Range(0, count);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;
            if (target-- == 0)
                return clips[i];
        }

        return null;
    }

    private AudioClip ResolveClip(AudioClip[] clips, AudioClip legacy)
    {
        AudioClip clip = PickClip(clips);
        return clip != null ? clip : legacy;
    }

    public void PlaySlime()
    {
        AudioClip clip = ResolveClip(slimeClips, slimeClip);
        if (clip != null)
        {
            PlayClip(clip);
            return;
        }

        PlayClip(null, "ui-pop", 300f, 660f, 0.16f, 0.5f);
        PlayClip(null, "ui-pop-hi", 660f, 990f, 0.1f, 0.25f);
    }

    public void PlayClick() =>
        PlayClip(ResolveClip(null, clickClip), "ui-click", 720f, 540f, 0.07f, 0.35f);

    public void PlayOpen() =>
        PlayClip(ResolveClip(openPopupClips, openClip), "ui-open", 440f, 680f, 0.12f, 0.35f);

    public void PlayClose()
    {
        if (Time.unscaledTime < _suppressCloseSoundUntil)
            return;

        PlayClip(ResolveClip(closePopupClips, closeClip), "ui-close", 520f, 330f, 0.12f, 0.35f);
    }

    /// <summary>
    /// Chamado pelo ClosePopUpBtt: o botao de fechar fica sem som. Suprime o
    /// PlayClose que o popup dispara na mesma chamada do clique.
    /// </summary>
    public void SilenceClosePopupSound()
    {
        _suppressCloseSoundUntil = Time.unscaledTime + 0.05f;
    }

    public void PlayBuy() =>
        PlayClip(buyClip, "ui-buy", new[] { 523f, 659f, 784f }, 0.09f, 0.4f);

    /// <summary>Toca um clipe avulso respeitando volume, mute e variacao de pitch.</summary>
    public void PlayClip(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null || _sfxMuted || _sfxSources == null)
            return;

        AudioSource source = NextSfxSource();
        source.volume = uiVolume;
        source.mute = false;

        float variation = Mathf.Clamp(sfxPitchVariation, 0f, 0.15f);
        source.pitch = 1f + UnityEngine.Random.Range(-variation, variation);
        source.PlayOneShot(clip);
    }

    private void PlayClip(AudioClip clip, string key, float f0, float f1, float dur, float vol) =>
        PlayClip(clip != null ? clip : SynthTone(key, f0, f1, dur, vol));

    private void PlayClip(AudioClip clip, string key, float[] notes, float noteDur, float vol) =>
        PlayClip(clip != null ? clip : SynthMelody(key, notes, noteDur, vol));

    // =========================================
    // AUDIO DA ABERTURA DO PACOTE
    // =========================================

    private void EnsurePackSources()
    {
        _packDrumsSource = GetOrCreatePackSource("PackDrumsAudio");
        _packCrescenteSource = GetOrCreatePackSource("PackCrescenteAudio");
        _packMusicSource = GetOrCreatePackSource("PackMusicAudio");
        _packOneShotSource = GetOrCreatePackSource("PackOneShotAudio");
        _packSources = new[]
        {
            _packDrumsSource,
            _packCrescenteSource,
            _packMusicSource,
            _packOneShotSource
        };
    }

    private AudioSource GetOrCreatePackSource(string name)
    {
        Transform child = transform.Find(name);
        GameObject go;
        if (child != null)
        {
            go = child.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
        }

        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null)
            source = go.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = uiVolume;
        source.mute = _sfxMuted;
        source.pitch = 1f;
        return source;
    }

    private void ApplyPackPitch(AudioSource source)
    {
        float variation = Mathf.Clamp(sfxPitchVariation, 0f, 0.15f);
        source.pitch = 1f + UnityEngine.Random.Range(-variation, variation);
    }

    private void PlayPackLoop(AudioSource source, AudioClip clip, bool applyPitch = true)
    {
        if (source == null || clip == null)
            return;

        // Troca o clipe mesmo se o anterior ainda estiver tocando.
        source.Stop();
        source.loop = true;
        source.clip = clip;
        source.volume = uiVolume;
        source.mute = _sfxMuted;
        source.pitch = 1f;
        if (applyPitch)
            ApplyPackPitch(source);
        source.Play();
    }

    private void PlayPackOneShot(AudioClip clip)
    {
        if (_packOneShotSource == null || clip == null || _sfxMuted)
            return;

        _packOneShotSource.volume = uiVolume;
        _packOneShotSource.mute = false;
        ApplyPackPitch(_packOneShotSource);
        _packOneShotSource.PlayOneShot(clip);
    }

    private void StopPackSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.loop = false;
        source.clip = null;
        source.pitch = 1f;
    }

    /// <summary>Fase 1: pacote pulsando. Tambores em loop e, se ativado, os crescentes.</summary>
    public void StartPackPreOpenAudio(bool useCrescente)
    {
        StopAllPackAudio();
        PlayPackLoop(_packDrumsSource, packDrumsClip);

        if (useCrescente)
            _packCrescenteRoutine = StartCoroutine(CrescenteIntroRoutine(_packAudioGeneration));
    }

    private IEnumerator CrescenteIntroRoutine(int generation)
    {
        if (crescente1Clip != null)
        {
            _packCrescenteSource.Stop();
            _packCrescenteSource.loop = false;
            _packCrescenteSource.clip = crescente1Clip;
            _packCrescenteSource.volume = uiVolume;
            _packCrescenteSource.mute = _sfxMuted;
            ApplyPackPitch(_packCrescenteSource);
            _packCrescenteSource.Play();
            yield return new WaitForSecondsRealtime(crescente1Clip.length);
        }

        if (generation != _packAudioGeneration)
            yield break;

        PlayPackLoop(_packCrescenteSource, crescente2Clip);
    }

    /// <summary>Fase 2: clique no pacote. Para os loops e toca Crescente3 + OpenPackComplete.</summary>
    public void StartPackOpenAudio(bool useCrescente)
    {
        _packAudioGeneration++;
        StopPackSource(_packDrumsSource);

        if (_packCrescenteRoutine != null)
        {
            StopCoroutine(_packCrescenteRoutine);
            _packCrescenteRoutine = null;
        }
        StopPackSource(_packCrescenteSource);

        if (useCrescente)
            PlayPackOneShot(crescente3Clip);

        PlayPackOneShot(openPackCompleteClip);
    }

    /// <summary>
    /// Fase 3: Lendario toca OpenPackMusic, Epico toca ComumPack e Comum
    /// entra direto em OpenPackMusicAfter em loop.
    /// </summary>
    public void StartPackPostOpenAudio(StickerRarity rarity)
    {
        StopAllPackAudio();

        AudioClip introClip;
        if (rarity == StickerRarity.Lendario)
            introClip = packMusicClip;
        else if (rarity == StickerRarity.Epico)
            introClip = comumPackClip;
        else
            introClip = null;

        if (introClip == null)
        {
            PlayPackLoop(_packMusicSource, packMusicAfterClip, false);
            return;
        }

        _packMusicRoutine = StartCoroutine(
            PackIntroThenLoopRoutine(introClip, _packAudioGeneration));
    }

    private IEnumerator PackIntroThenLoopRoutine(AudioClip introClip, int generation)
    {
        if (introClip != null)
        {
            _packMusicSource.Stop();
            _packMusicSource.loop = false;
            _packMusicSource.clip = introClip;
            _packMusicSource.volume = uiVolume;
            _packMusicSource.mute = _sfxMuted;
            _packMusicSource.pitch = 1f;
            _packMusicSource.Play();
            yield return new WaitForSecondsRealtime(introClip.length);
        }

        if (generation != _packAudioGeneration)
            yield break;

        // Musica nao recebe a variacao de pitch aplicada aos SFX.
        PlayPackLoop(_packMusicSource, packMusicAfterClip, false);
    }

    /// <summary>Para todos os sons ligados a abertura do pacote.</summary>
    public void StopAllPackAudio()
    {
        _packAudioGeneration++;

        if (_packCrescenteRoutine != null)
        {
            StopCoroutine(_packCrescenteRoutine);
            _packCrescenteRoutine = null;
        }
        if (_packMusicRoutine != null)
        {
            StopCoroutine(_packMusicRoutine);
            _packMusicRoutine = null;
        }

        StopPackSource(_packDrumsSource);
        StopPackSource(_packCrescenteSource);
        StopPackSource(_packMusicSource);
        StopPackSource(_packOneShotSource);
    }

    // =========================================
    // AUDIO DOS EVENTOS (PRESENTE / BARATAS / EUFORIA)
    // =========================================

    private void EnsureEventSources()
    {
        Transform child = transform.Find("EuforiaMusicAudio");
        GameObject go;
        if (child != null)
        {
            go = child.gameObject;
        }
        else
        {
            go = new GameObject("EuforiaMusicAudio");
            go.transform.SetParent(transform, false);
        }

        _euforiaMusicSource = go.GetComponent<AudioSource>();
        if (_euforiaMusicSource == null)
            _euforiaMusicSource = go.AddComponent<AudioSource>();

        _euforiaMusicSource.playOnAwake = false;
        _euforiaMusicSource.loop = false;
        _euforiaMusicSource.spatialBlend = 0f;
        _euforiaMusicSource.volume = uiVolume;
        _euforiaMusicSource.mute = _sfxMuted;
        _euforiaMusicSource.pitch = 1f;
        _eventSources = new[] { _euforiaMusicSource };
    }

    public void PlayPresentOpen() => PlayClip(openPresentClip);

    public void PlayPresentLost() => PlayClip(lostPresentClip);

    public void PlayRoachDamage() => PlayClip(danoBarataClip);

    public void PlayRoachKill() => PlayClip(matarBarataClip);

    public void PlayHorizontalNavigation() => PlayClip(horizontalNavigationClip);

    public void PlayPurchase() => PlayClip(purchaseClip);

    public void PlayEuforiaStart() => PlayClip(euforia1Clip);

    /// <summary>
    /// Som de fechamento dos popups que silenciam o ClosePopUpBtt.
    /// Usa PlayClip direto para ignorar o silenciamento do botao.
    /// </summary>
    public void PlayPopupClose() =>
        PlayClip(ResolveClip(closePopupClips, closeClip));

    public void PlayPopupMovelClose() => PlayPopupClose();

    /// <summary>
    /// Inicia o audio do bonus do presente: Euforia1 e EuforiaMusic juntos.
    /// O fade da musica comeca antes do fim do efeito e termina 1s depois dele.
    /// </summary>
    public void BeginPresentBonusAudio(float effectDuration)
    {
        StopPresentBonusAudio(true);

        PlayClip(euforia1Clip);

        _euforiaAudioGeneration++;
        int generation = _euforiaAudioGeneration;

        if (euforiaMusicClip != null && _euforiaMusicSource != null)
        {
            _euforiaMusicSource.Stop();
            _euforiaMusicSource.loop = false;
            _euforiaMusicSource.clip = euforiaMusicClip;
            _euforiaMusicSource.volume = uiVolume;
            _euforiaMusicSource.mute = _sfxMuted;
            _euforiaMusicSource.pitch = 1f;
            _euforiaMusicSource.Play();
        }

        // Fade de 2s termina 1s apos o efeito: comeca 1s antes dele.
        float targetEnd = Mathf.Max(0.1f, effectDuration) +
                          Mathf.Max(0f, euforiaMusicFadeEndOffsetSeconds);
        float fadeDuration = Mathf.Clamp(
            euforiaMusicFadeSeconds, 0.1f, targetEnd);
        float fadeStart = Mathf.Max(0f, targetEnd - fadeDuration);

        _euforiaMusicFadeStartRoutine = StartCoroutine(
            EuforiaMusicFadeStartRoutine(
                fadeStart, fadeDuration, generation));
    }

    private IEnumerator EuforiaMusicFadeStartRoutine(
        float delay, float duration, int generation)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (generation != _euforiaAudioGeneration)
            yield break;

        _euforiaMusicFadeStartRoutine = null;
        StartEuforiaMusicFade(duration, generation);
    }

    /// <summary>
    /// Faz o fade out da EuforiaMusic; com immediate=true, para tudo na hora.
    /// </summary>
    public void StopPresentBonusAudio(bool immediate = false)
    {
        if (immediate)
        {
            StopPresentBonusAudioImmediate();
            return;
        }

        _euforiaAudioGeneration++;
        StartEuforiaMusicFade(
            Mathf.Max(0.1f, euforiaMusicFadeSeconds),
            _euforiaAudioGeneration);
    }

    private void StartEuforiaMusicFade(float duration, int generation)
    {
        if (_euforiaMusicFadeStartRoutine != null)
        {
            StopCoroutine(_euforiaMusicFadeStartRoutine);
            _euforiaMusicFadeStartRoutine = null;
        }
        if (_euforiaMusicFadeRoutine != null)
        {
            StopCoroutine(_euforiaMusicFadeRoutine);
            _euforiaMusicFadeRoutine = null;
        }

        if (_euforiaMusicSource == null || !_euforiaMusicSource.isPlaying ||
            !Application.isPlaying)
        {
            StopPresentBonusAudioImmediate();
            return;
        }

        _euforiaMusicFadeRoutine = StartCoroutine(
            EuforiaMusicFadeRoutine(duration, generation));
    }

    private void StopPresentBonusAudioImmediate()
    {
        _euforiaAudioGeneration++;

        if (_euforiaMusicFadeStartRoutine != null)
        {
            StopCoroutine(_euforiaMusicFadeStartRoutine);
            _euforiaMusicFadeStartRoutine = null;
        }
        if (_euforiaMusicFadeRoutine != null)
        {
            StopCoroutine(_euforiaMusicFadeRoutine);
            _euforiaMusicFadeRoutine = null;
        }

        if (_euforiaMusicSource != null)
        {
            _euforiaMusicSource.Stop();
            _euforiaMusicSource.loop = false;
            _euforiaMusicSource.clip = null;
            _euforiaMusicSource.volume = uiVolume;
            _euforiaMusicSource.pitch = 1f;
        }
    }

    private IEnumerator EuforiaMusicFadeRoutine(
        float duration, int generation)
    {
        if (_euforiaMusicSource == null || !_euforiaMusicSource.isPlaying)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (generation != _euforiaAudioGeneration)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            _euforiaMusicSource.volume = uiVolume * (1f - k);
            yield return null;
        }

        if (generation != _euforiaAudioGeneration)
            yield break;

        _euforiaMusicSource.Stop();
        _euforiaMusicSource.clip = null;
        _euforiaMusicSource.volume = uiVolume;
        _euforiaMusicFadeRoutine = null;
    }

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
        if (_musicSuppressed)
        {
            StopMusic();
            return;
        }

        _musicSource.clip = musicClip;
        _musicSource.loop = true;
        ApplyMusicState();
        if (!_musicMuted && !_musicSource.isPlaying)
            _musicSource.Play();
    }

    /// <summary>Para e zera a musica de fundo, sem alterar o mute do jogador.</summary>
    public void StopMusic()
    {
        if (_musicSource == null)
            return;

        _musicSource.Stop();
        _musicSource.clip = null;
    }

    /// <summary>
    /// Suprime a musica em propagandas, abertura de pacote e telas verticais.
    /// A musica so volta quando chamado com false ao retornar a sala horizontal.
    /// </summary>
    public void SetMusicSuppressed(bool suppressed)
    {
        if (_musicSuppressed == suppressed)
            return;

        _musicSuppressed = suppressed;
        if (suppressed)
            StopMusic();
        else
            ApplyMusicState();
    }

    // =========================================
    // VOLUME / MUTE
    // =========================================

    public void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        if (_sfxSources != null)
        {
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                if (_sfxSources[i] != null)
                    _sfxSources[i].volume = uiVolume;
            }
        }
        else if (_sfxSource != null)
        {
            _sfxSource.volume = uiVolume;
        }
        if (_packSources != null)
        {
            for (int i = 0; i < _packSources.Length; i++)
            {
                if (_packSources[i] != null)
                    _packSources[i].volume = uiVolume;
            }
        }
        if (_eventSources != null)
        {
            for (int i = 0; i < _eventSources.Length; i++)
            {
                if (_eventSources[i] != null)
                    _eventSources[i].volume = uiVolume;
            }
        }
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
        if (!_musicMuted && !_musicSuppressed && Application.isPlaying &&
            musicClip != null && !_musicSource.isPlaying)
        {
            if (_musicSource.clip == null)
                _musicSource.clip = musicClip;
            _musicSource.Play();
        }
    }

    public void SetSfxMuted(bool muted)
    {
        if (_sfxMuted == muted)
            return;

        _sfxMuted = muted;
        if (_sfxSources != null)
        {
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                if (_sfxSources[i] != null)
                    _sfxSources[i].mute = muted;
            }
        }
        else if (_sfxSource != null)
        {
            _sfxSource.mute = muted;
        }
        if (_packSources != null)
        {
            for (int i = 0; i < _packSources.Length; i++)
            {
                if (_packSources[i] != null)
                    _packSources[i].mute = muted;
            }
        }
        if (_eventSources != null)
        {
            for (int i = 0; i < _eventSources.Length; i++)
            {
                if (_eventSources[i] != null)
                    _eventSources[i].mute = muted;
            }
        }

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
