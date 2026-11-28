using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Aquece os videos de fundo dos paineis verticais antes da aba abrir.
///
/// Os VideoPlayers originais ficam dentro de paineis desativados, entao nao
/// podem ser preparados (Unity recusa "Prepare a disabled VideoPlayer").
/// Este componente fica num objeto sempre ativo, cria um VideoPlayer
/// temporario com o MESMO clip e RenderTexture, prepara, renderiza o primeiro
/// frame e o descarta. Assim, quando o painel e ativado, a textura ja nao
/// esta preta.
/// </summary>
[DisallowMultipleComponent]
public sealed class PackVideoPreloader : MonoBehaviour
{
    [Tooltip("VideoPlayer do PackUI (PackBGVideo). Se vazio, procura na cena.")]
    [SerializeField] private VideoPlayer packVideoPlayer;

    [Tooltip("VideoPlayer do RoomLvlUI (BananaBGVideo). Se vazio, procura pelo clip BananaBG.")]
    [SerializeField] private VideoPlayer roomLvlVideoPlayer;

    [Tooltip("Tempo maximo esperando o Prepare, em segundos.")]
    [SerializeField, Min(1f)] private float maxPrepareSeconds = 15f;

    [Tooltip("Tempo maximo esperando o primeiro frame, em segundos.")]
    [SerializeField, Min(0.1f)] private float maxFirstFrameSeconds = 3f;

    private void Awake()
    {
        if (packVideoPlayer == null)
            packVideoPlayer = FindFirstObjectByType<VideoPlayer>(
                FindObjectsInactive.Include);

        if (roomLvlVideoPlayer == null)
        {
            foreach (VideoPlayer vp in FindObjectsByType<VideoPlayer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (vp.clip != null && vp.clip.name == "BananaBG")
                {
                    roomLvlVideoPlayer = vp;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        StartCoroutine(Preload(packVideoPlayer, "Pack"));
        StartCoroutine(Preload(roomLvlVideoPlayer, "RoomLvl"));
    }

    private IEnumerator Preload(VideoPlayer target, string label)
    {
        if (target == null || target.clip == null || target.targetTexture == null)
            yield break;

        GameObject go = new GameObject($"[VideoPreload-{label}]");
        VideoPlayer warmup = go.AddComponent<VideoPlayer>();

        warmup.playOnAwake = false;
        warmup.source = target.source;
        warmup.clip = target.clip;
        warmup.renderMode = target.renderMode;
        warmup.targetTexture = target.targetTexture;
        warmup.playbackSpeed = target.playbackSpeed;
        warmup.isLooping = false;
        warmup.waitForFirstFrame = target.waitForFirstFrame;
        warmup.sendFrameReadyEvents = true;
        warmup.audioOutputMode = VideoAudioOutputMode.None;

        bool prepared = false;
        warmup.prepareCompleted += _ => prepared = true;
        warmup.errorReceived += (_, message) =>
            Debug.LogWarning($"[VideoPreload-{label}] {message}");

        warmup.Prepare();

        float elapsed = 0f;
        while (!prepared && elapsed < maxPrepareSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!prepared)
        {
            Cleanup(warmup);
            yield break;
        }

        bool firstFrame = false;
        warmup.frameReady += (_, __) => firstFrame = true;
        warmup.Play();

        elapsed = 0f;
        while (!firstFrame && elapsed < maxFirstFrameSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Mantem o ultimo frame renderizado na RenderTexture; o Player real
        // assume a textura quando o painel for aberto.
        if (warmup != null && warmup.isPlaying)
            warmup.Pause();

        Cleanup(warmup);
    }

    private static void Cleanup(VideoPlayer warmup)
    {
        if (warmup == null)
            return;

        if (warmup.isPlaying)
            warmup.Stop();

        Destroy(warmup.gameObject);
    }
}
