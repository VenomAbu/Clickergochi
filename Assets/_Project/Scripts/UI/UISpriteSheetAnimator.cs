using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toca os frames de um spritesheet em uma Image de UI.
/// Usado para comparar animacoes lado a lado com o mesmo FPS.
/// </summary>
[RequireComponent(typeof(Image))]
public class UISpriteSheetAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float fps = 12f;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool loop = true;

    private Image image;
    private float timer;
    private int index;
    private bool playing;

    private void Awake()
    {
        image = GetComponent<Image>();
        playing = playOnAwake && frames != null && frames.Length > 0;
        ApplyFrame();
    }

    private void OnEnable()
    {
        if (playOnAwake && frames != null && frames.Length > 0)
            Play();
    }

    private void Update()
    {
        if (!playing || frames == null || frames.Length == 0 || fps <= 0f)
            return;

        timer += Time.deltaTime;
        float frameDuration = 1f / fps;
        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            index++;

            if (index >= frames.Length)
            {
                if (!loop)
                {
                    index = frames.Length - 1;
                    playing = false;
                    break;
                }
                index = 0;
            }
        }
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (image == null || frames == null || frames.Length == 0)
            return;
        image.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0)
            return;
        playing = true;
    }

    public void Stop()
    {
        playing = false;
    }

    /// <summary>Configura os frames em runtime (usado pelas baratas geradas).</summary>
    public void Configure(Sprite[] newFrames, float newFps = 0f, bool newLoop = true)
    {
        frames = newFrames ?? System.Array.Empty<Sprite>();
        if (newFps > 0f)
            fps = newFps;
        loop = newLoop;
        playOnAwake = true;

        if (image == null)
            image = GetComponent<Image>();

        index = 0;
        timer = 0f;
        playing = frames.Length > 0;
        ApplyFrame();
    }
}
