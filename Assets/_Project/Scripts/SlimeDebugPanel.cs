using UnityEngine;

/// <summary>
/// Painel de debug/teste em cena: controla os medidores em tempo real
/// no Play e centraliza atalhos (euforia, gosma).
/// </summary>
public class SlimeDebugPanel : MonoBehaviour
{
    [Header("Medidores (arraste no Play)")]
    [Range(0f, 1f)] public float hunger = 0.5f;
    [Range(0f, 1f)] public float fun = 0.5f;
    [Range(0f, 1f)] public float hygiene = 0.5f;

    [Header("Ao dar Play")]
    [Tooltip("Marcado: todos os medidores vao ao minimo e a euforia limpa.")]
    public bool startAtMin;

    [Header("Euforia")]
    [Tooltip("Marque no Play para forcar a euforia (enche os 3 cuidados).")]
    public bool triggerEuphoria;
    [Tooltip("Marque no Play para encerrar a euforia.")]
    public bool clearEuphoria;

    [Header("Gosma")]
    public float addGooAmount = 100f;
    [Tooltip("Marque no Play para dar a gosma acima.")]
    public bool giveGoo;

    public SlimeManager slimeManager;
    private float lastH = -1f, lastF = -1f, lastHy = -1f;

    private void Start()
    {
        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();
        if (startAtMin && slimeManager != null)
        {
            slimeManager.SetCare(0f, 0f, 0f);
            slimeManager.ClearEuphoria();
        }
        Pull();
    }

    private void Update()
    {
        if (slimeManager == null)
            return;
        if (triggerEuphoria)
        {
            triggerEuphoria = false;
            slimeManager.SetCare(1f, 1f, 1f);
            Pull();
            return;
        }
        if (clearEuphoria)
        {
            clearEuphoria = false;
            slimeManager.ClearEuphoria();
            return;
        }
        if (giveGoo)
        {
            giveGoo = false;
            slimeManager.AddGoo(addGooAmount);
        }
        if (!Mathf.Approximately(hunger, lastH) ||
            !Mathf.Approximately(fun, lastF) ||
            !Mathf.Approximately(hygiene, lastHy))
        {
            slimeManager.SetCare(hunger, fun, hygiene);
        }
        Pull();
    }

    private void Pull()
    {
        lastH = hunger = slimeManager.Hunger;
        lastF = fun = slimeManager.Fun;
        lastHy = hygiene = slimeManager.Hygiene;
    }
}
