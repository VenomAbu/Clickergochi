using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Responsabilidade restante do comodo: a barra de nivel.
///
/// Measurements e baloes de dialogo -> MainHUDUGUIController.
/// Audio/mute -> MainHUDUGUIController.
/// Abertura dos paineis (Album/Shop/Food) -> VerticalNavegationController.
/// </summary>
public class RoomController : MonoBehaviour
{
    [Header("Level Bar")]
    [SerializeField] private Image levelBar;
    [SerializeField] private float levelBarTotal = 100f;

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

    /// <summary>Progresso 0-1 direto no fill (independe de levelBarTotal).</summary>
    public void SetLevelProgress(float progress01)
    {
        if (levelBar == null)
            return;

        levelBar.fillAmount = Mathf.Clamp01(progress01);
    }
}
