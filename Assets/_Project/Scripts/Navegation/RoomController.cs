using System;
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

    /// <summary>Dispara quando o mute e alternado (true = mudo).</summary>
    public event Action<bool> MuteChanged;

    /// <summary>Estado atual do mudo.</summary>
    public bool IsMuted => isMuted;

    [Header("Navigation")]
    [SerializeField] private VerticalNavegationController verticalNavigation;



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

    /// <summary>
    /// Alimenta os medidores com os cuidados do slime (0-1).
    /// Ordem interna de measureImages: [HapinessFill, HungerFill, HygieneFill].
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

    public void UpdateLevelBar(float value)
    {
        if (levelBar == null)
            return;

        float percentage = levelBarTotal > 0f
            ? value / levelBarTotal
            : 0f;

        levelBar.fillAmount = Mathf.Clamp01(percentage);
    }

    /// <summary>Progresso 0-1 direto no fill (independe de levelBarTotal).</summary>
    public void SetLevelProgress(float progress01)
    {
        if (levelBar == null)
            return;

        levelBar.fillAmount = Mathf.Clamp01(progress01);
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
        if (panel == null)
            return;

        if (verticalNavigation != null)
        {
            verticalNavigation.OpenPanel(panel);
        }
        else
        {
            Debug.LogWarning(
                "RoomController: VerticalNavigation n�o configurado."
            );
        }
    }

    // =========================================
    // AUDIO
    // =========================================

    public void ToggleMute()
    {
        isMuted = !isMuted;
        UpdateMuteVisual();
        MuteChanged?.Invoke(isMuted);
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
