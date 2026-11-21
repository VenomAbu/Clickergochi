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
    [Tooltip("Marque no Play para ATIVAR a euforia (enche os 3 cuidados); " +
             "desmarque para ENCERRAR. O jogo sempre comeca sem euforia.")]
    public bool euphoria;

    [Header("Gosma")]
    public float addGooAmount = 100f;
    [Tooltip("Marque no Play para dar a gosma acima.")]
    public bool giveGoo;

    public SlimeManager slimeManager;
    private float lastH = -1f, lastF = -1f, lastHy = -1f;
    private bool lastEuphoria;

    private void Start()
    {
        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();
        if (slimeManager == null)
            return;

        // O jogo sempre comeca sem euforia, mesmo que o campo tenha ficado
        // marcado no Inspector na ultima sessao de teste. O override manual
        // impede que os cuidados em 100% reativem a euforia no primeiro Update.
        euphoria = false;
        lastEuphoria = false;
        slimeManager.SetEuphoriaManual(false);

        if (startAtMin)
            slimeManager.SetCare(0f, 0f, 0f);

        Pull();
    }

    private void Update()
    {
        if (slimeManager == null)
            return;

        // Uma unica booleana: basta alternar o valor no Inspector.
        if (euphoria != lastEuphoria)
        {
            lastEuphoria = euphoria;
            if (euphoria)
            {
                slimeManager.SetCare(1f, 1f, 1f);
                slimeManager.SetEuphoriaManual(true);
                Pull();
            }
            else
            {
                slimeManager.SetEuphoriaManual(false);
            }
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
