using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Toca/pausa um VideoPlayer de acordo com a visibilidade do painel de UI.
/// Necessario porque playOnAwake nao dispara enquanto o GameObject esta
/// desativado (e os paineis de navegacao ficam desativados fora da aba atual).
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class CanvasVideoBackground : MonoBehaviour
{
    private VideoPlayer player;

    private void Awake()
    {
        player = GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        if (player == null)
            player = GetComponent<VideoPlayer>();
        if (player == null || player.clip == null)
            return;

        player.Play();
    }

    private void OnDisable()
    {
        if (player != null && player.isPlaying)
            player.Pause();
    }
}
