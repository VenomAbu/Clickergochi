using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Toca/pausa um VideoPlayer de acordo com a visibilidade do painel de UI.
/// Necessario porque playOnAwake nao dispara enquanto o GameObject esta
/// desativado (e os paineis de navegacao ficam desativados fora da aba atual).
///
/// O Prepare e explicito: so chamar Play() em um VideoPlayer ainda nao
/// preparado pode deixar o video parado por alguns segundos no celular.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class CanvasVideoBackground : MonoBehaviour
{
    private VideoPlayer player;
    private bool wantsPlay;

    private void Awake()
    {
        player = GetComponent<VideoPlayer>();
        if (player != null)
            player.prepareCompleted += OnPrepared;
    }

    private void OnEnable()
    {
        if (player == null)
            player = GetComponent<VideoPlayer>();
        if (player == null || player.clip == null)
            return;

        wantsPlay = true;
        if (player.isPrepared)
            player.Play();
        else
            player.Prepare();
    }

    private void OnDisable()
    {
        wantsPlay = false;
        if (player != null && player.isPlaying)
            player.Pause();
    }

    private void OnDestroy()
    {
        if (player != null)
            player.prepareCompleted -= OnPrepared;
    }

    private void OnPrepared(VideoPlayer source)
    {
        if (wantsPlay && source != null && source.gameObject.activeInHierarchy)
            source.Play();
    }
}
