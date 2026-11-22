using UnityEngine;

/// <summary>
/// Animacao dos cards de pacote do PackUI:
/// - SpikeImg gira continuamente e devagar;
/// - PackImage balanca a rotacao Z entre -5 e +5, duas vezes a cada intervalo.
/// Os valores ficam no Inspector de cada card.
/// </summary>
public class PackCardAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RectTransform spikeImg;
    [SerializeField] private RectTransform packImage;

    [Header("SpikeImg - rotacao lenta")]
    [Tooltip("Graus por segundo (20 = uma volta a cada 18s).")]
    [SerializeField, Min(0f)] private float spikeRotationSpeed = 20f;

    [Header("PackImage - balanco Z")]
    [SerializeField, Min(0.01f)] private float swayAngle = 5f;
    [Tooltip("Quantos ciclos completos (-5 -> +5 -> -5) por intervalo.")]
    [SerializeField, Min(1)] private int swayCycles = 2;
    [SerializeField, Min(0.1f)] private float swayInterval = 1.5f;

    private Vector3 spikeBaseEuler;
    private Vector3 packBaseEuler;
    private float spikeAngle;
    private float elapsed;

    private void Awake()
    {
        if (spikeImg != null)
            spikeBaseEuler = spikeImg.localRotation.eulerAngles;
        if (packImage != null)
            packBaseEuler = packImage.localRotation.eulerAngles;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        if (spikeImg != null && spikeRotationSpeed > 0f)
        {
            spikeAngle = Mathf.Repeat(spikeAngle + spikeRotationSpeed * dt, 360f);
            spikeImg.localRotation = Quaternion.Euler(
                spikeBaseEuler.x, spikeBaseEuler.y, spikeBaseEuler.z + spikeAngle);
        }

        if (packImage != null && swayInterval > 0f)
        {
            float phase = Mathf.Repeat(elapsed / swayInterval, 1f);
            float angle = Mathf.Sin(phase * Mathf.PI * 2f * swayCycles) * swayAngle;
            packImage.localRotation = Quaternion.Euler(
                packBaseEuler.x, packBaseEuler.y, packBaseEuler.z + angle);
        }
    }
}
