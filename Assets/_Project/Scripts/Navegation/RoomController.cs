using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RoomController : MonoBehaviour
{
    [Header("Measurements")]
    [SerializeField] private Image[] measureImages = new Image[3];
    [SerializeField] private float[] measureValues = new float[3];
    [SerializeField]
   
    private float[] measureMaxValues = new float[3]
    {
        100f, 100f, 100f
    };

    [Header("Level Bar")]
    [SerializeField] private Image levelBar;
    [SerializeField] private float levelBarTotal = 100f;

    [Header("Panels")]
    [SerializeField] private GameObject albumPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject foodPanel;

    [Header("Audio")]
    [SerializeField] private Image muteImage;
    [SerializeField] private Image unmuteImage;


    [Header("Measurement Dialog Bubbles")]
    [SerializeField] private GameObject happinessBubble;
    [SerializeField] private GameObject hygieneBubble;
    [SerializeField] private GameObject hungerBubble;
    [SerializeField] private float bubbleDuration = 1.5f;

    private bool isMuted;
    private Coroutine bubbleCoroutine;



    private void Awake()
    {
        UpdateAllMensure();
        UpdateMuteVisual();
        HideAllBubbles();
    }

    // =========================================
    // MEASUREMENTS
    // =========================================

    public void UpdateAllMensure()
    {
        if (measureImages == null || measureValues == null)
            return;

        int count = Mathf.Min(
            measureImages.Length,
            measureValues.Length
        );

        for (int i = 0; i < count; i++)
        {
            if (measureImages[i] == null)
                continue;

            float maxValue =
                measureMaxValues != null &&
                i < measureMaxValues.Length
                    ? measureMaxValues[i]
                    : 100f;

            float percentage = maxValue > 0f
                ? measureValues[i] / maxValue
                : 0f;

            measureImages[i].fillAmount =
                Mathf.Clamp01(percentage);
        }
    }

    // =========================================
    // LEVEL BAR
    // =========================================

    public void UpdateLevelBar(float value)
    {
        if (levelBar == null)
            return;

        float percentage = levelBarTotal > 0f
            ? value / levelBarTotal
            : 0f;

        levelBar.fillAmount = Mathf.Clamp01(percentage);
    }

    // =========================================
    // OPEN PANELS
    // =========================================

    public void OpenAlbum()
    {
        OpenPanel(albumPanel);
    }

    public void OpenShop()
    {
        OpenPanel(shopPanel);
    }

    public void OpenFood()
    {
        OpenPanel(foodPanel);
    }

    private void OpenPanel(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(true);
    }

    // =========================================
    // AUDIO
    // =========================================

    public void ToggleMute()
    {
        isMuted = !isMuted;
        UpdateMuteVisual();
    }

    private void UpdateMuteVisual()
    {
        if (muteImage != null)
            muteImage.gameObject.SetActive(isMuted);

        if (unmuteImage != null)
            unmuteImage.gameObject.SetActive(!isMuted);
    }

    // =========================================
    // MEASUREMENT DIALOG BUBBLES
    // =========================================

    public void MensureHappinessBtt()
    {
        ShowBubble(happinessBubble);
    }

    public void MensureHygieneBtt()
    {
        ShowBubble(hygieneBubble);
    }

    public void MensureHungerBtt()
    {
        ShowBubble(hungerBubble);
    }

    private void ShowBubble(GameObject bubble)
    {
        if (bubble == null)
            return;

        if (bubbleCoroutine != null)
            StopCoroutine(bubbleCoroutine);

        HideAllBubbles();

        bubble.SetActive(true);

        bubbleCoroutine = StartCoroutine(
            HideBubbleAfterDelay(bubble)
        );
    }

    private IEnumerator HideBubbleAfterDelay(GameObject bubble)
    {
        yield return new WaitForSeconds(bubbleDuration);

        if (bubble != null)
            bubble.SetActive(false);

        bubbleCoroutine = null;
    }

    private void HideAllBubbles()
    {
        if (happinessBubble != null)
            happinessBubble.SetActive(false);

        if (hygieneBubble != null)
            hygieneBubble.SetActive(false);

        if (hungerBubble != null)
            hungerBubble.SetActive(false);
    }




}
