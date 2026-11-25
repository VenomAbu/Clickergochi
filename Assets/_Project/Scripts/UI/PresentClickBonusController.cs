using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bonus por destruir o presente a clique: o clique na slime fica x500 por
/// 7 segundos e o jogo usa exatamente o visual da Euforia (borda dourada e
/// efeito de estrelas). Nao altera o estado real da Euforia.
///
/// Enquanto o bonus dura, BonusTime mostra o tempo restante no Fill Amount e
/// o MultiplierText do SlimeConter (HUD) mostra "x500". Ao terminar, os dois somem.
/// </summary>
public sealed class PresentClickBonusController : MonoBehaviour
{
    public static PresentClickBonusController Instance { get; private set; }

    private const string MultiplierId = "present_click_bonus";

    [Header("Bonus do presente")]
    [Tooltip("Multiplicador aplicado ao clique da slime enquanto o bonus dura.")]
    [SerializeField, Min(1f)] private float clickMultiplier = 500f;
    [Tooltip("Duracao do bonus em segundos.")]
    [SerializeField, Min(0.1f)] private float durationSeconds = 7f;

    [Header("Visual do bonus")]
    [Tooltip("Image Filled de Canvas/Effects/BonusTime.")]
    [SerializeField] private Image bonusTimeFill;

    private Coroutine _routine;
    private bool _multiplierRegistered;

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
        SetBonusUi(false);
    }

    private void OnDestroy()
    {
        // Na destruicao da cena os objetos de UI ja podem estar sendo
        // destruidos; apenas libera o multiplicador, sem chamar SetActive.
        ClearBonus(false);
        GameAudioManager.Instance?.StopPresentBonusAudio(true);
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Dispara (ou reinicia) o bonus de clique do presente.</summary>
    public void Activate()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        GameAudioManager.Instance?.BeginPresentBonusAudio(durationSeconds);
        _routine = StartCoroutine(BonusRoutine());
    }

    private IEnumerator BonusRoutine()
    {
        SlimeManager slime = SlimeManager.Instance;
        if (slime == null)
        {
            _routine = null;
            yield break;
        }

        // Mesmo ID: se o bonus for reativado, substitui o valor em vez de
        // empilhar outro multiplicador.
        slime.RegisterMultiplier(MultiplierId, clickMultiplier);
        _multiplierRegistered = true;
        SetEuphoriaVisual(true);

        // O multiplicador aparece no TMP do SlimeConter da HUD, no lugar do
        // texto grande de Effects usado antes.
        MainHUDUGUIController.Instance?.SetBonusMultiplierText(
            $"x{clickMultiplier:0}");
        if (bonusTimeFill != null)
            bonusTimeFill.fillAmount = 1f;
        SetBonusUi(true);

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            if (bonusTimeFill != null)
                bonusTimeFill.fillAmount = Mathf.Clamp01(
                    (durationSeconds - elapsed) / durationSeconds);
            yield return null;
        }

        // O fade da musica e agendado pelo GameAudioManager e termina 1s
        // depois do efeito; nao interromper aqui.
        ClearBonus();
        _routine = null;
    }

    private void ClearBonus(bool cleanupVisuals = true)
    {
        if (_multiplierRegistered)
        {
            SlimeManager.Instance?.UnregisterMultiplier(MultiplierId);
            _multiplierRegistered = false;
        }

        if (!cleanupVisuals)
            return;

        SetBonusUi(false);
        if (Application.isPlaying)
            SetEuphoriaVisual(false);
        MainHUDUGUIController.Instance?.SetBonusMultiplierText(null);
    }

    private void SetBonusUi(bool active)
    {
        if (bonusTimeFill != null)
        {
            if (!active)
                bonusTimeFill.fillAmount = 0f;
            bonusTimeFill.gameObject.SetActive(active);
        }
    }

    private static void SetEuphoriaVisual(bool active)
    {
        if (MainHUDUGUIController.Instance != null)
            MainHUDUGUIController.Instance.SetTemporaryEuphoriaVisual(active);

        EuphoriaEffectsController effects = EuphoriaEffectsController.Instance;
        if (effects == null)
            effects = FindFirstObjectByType<EuphoriaEffectsController>();
        if (effects != null)
            effects.SetTemporaryEuphoriaVisual(active);
    }

    private void ResolveReferences()
    {
        if (bonusTimeFill == null)
        {
            Transform t = FindInactiveChild("BonusTime");
            if (t != null)
                bonusTimeFill = t.GetComponent<Image>();
        }
    }

    private static Transform FindInactiveChild(string name)
    {
        foreach (Transform t in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }
}
