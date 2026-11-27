using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Aquece o video de fundo do PackUI antes da aba abrir.
///
/// O VideoPlayer original fica dentro do PackUI desativado, entao nao pode ser
/// preparado (Unity recusa "Prepare a disabled VideoPlayer"). Este componente
/// fica num objeto sempre ativo, cria um VideoPlayer temporario com o MESMO
/// clip e RenderTexture, prepara, renderiza o primeiro frame e o descarta.
/// Assim, quando o PackUI e ativado, a textura ja nao esta preta.
/// </summary>
[DisallowMultipleComponent]
public sealed class PackVideoPreloader : MonoBehaviour
{
    [Tooltip("VideoPlayer do PackUI (PackBGVideo). Se vazio, procura na cena.")]
    [SerializeField] private VideoPlayer packVideoPlayer;

    [Tooltip("Tempo maximo esperando o Prepare, em segundos.")]
    [SerializeField, Min(1f)] private float maxPrepareSeconds = 15f;

    [Tooltip("Tempo maximo esperando o primeiro frame, em segundos.")]
    [SerializeField, Min(0.1f)] private float maxFirstFrameSeconds = 3f;

    private VideoPlayer _warmup;

    private void Awake()
    {
        if (packVideoPlayer == null)
            packVideoPlayer = FindFirstObjectByType<VideoPlayer>(
                FindObjectsInactive.Include);
    }

    private void Start()
    {
        StartCoroutine(Preload());
    }

    private IEnumerator Preload()
    {
        if (packVideoPlayer == null || packVideoPlayer.clip == null ||
            packVideoPlayer.targetTexture == null)
        {
            yield break;
        }

        GameObject go = new GameObject("[PackVideoPreload]");
        _warmup = go.AddComponent<VideoPlayer>();

        _warmup.playOnAwake = false;
        _warmup.source = packVideoPlayer.source;
        _warmup.clip = packVideoPlayer.clip;
        _warmup.renderMode = packVideoPlayer.renderMode;
        _warmup.targetTexture = packVideoPlayer.targetTexture;
        _warmup.playbackSpeed = packVideoPlayer.playbackSpeed;
        _warmup.isLooping = false;
        _warmup.waitForFirstFrame = packVideoPlayer.waitForFirstFrame;
        _warmup.sendFrameReadyEvents = true;
        _warmup.audioOutputMode = VideoAudioOutputMode.None;

        bool prepared = false;
        _warmup.prepareCompleted += _ => prepared = true;
        _warmup.errorReceived += (_, message) =>
            Debug.LogWarning($"[PackVideo] {message}");

        _warmup.Prepare();

        float elapsed = 0f;
        while (!prepared && elapsed < maxPrepareSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!prepared)
        {
            Cleanup();
            yield break;
        }

        bool firstFrame = false;
        _warmup.frameReady += (_, __) => firstFrame = true;
        _warmup.Play();

        elapsed = 0f;
        while (!firstFrame && elapsed < maxFirstFrameSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Mantem o ultimo frame renderizado na RenderTexture; o Player real
        // assume a textura quando o PackUI for aberto.
        if (_warmup != null && _warmup.isPlaying)
            _warmup.Pause();

        Cleanup();
    }

    private void Cleanup()
    {
        if (_warmup == null)
            return;

        if (_warmup.isPlaying)
            _warmup.Stop();

        Destroy(_warmup.gameObject);
        _warmup = null;
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}
