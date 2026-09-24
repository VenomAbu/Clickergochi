using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gerenciador de baratas baseado em GameObjects da Scene.
/// Não cria elementos de UI e não depende de UIDocument/UI Toolkit.
/// As tres referencias abaixo são GameObjects previamente colocar na Scene.
/// </summary>
public class CockroachSceneManager : MonoBehaviour
{
    [Header("Sistema")]
    public SlimeManager slimeManager;
    public CockroachUnit[] roaches;

    [Header("Configuracao")]
    [Min(1)] public int maxCritters = 3;
    public float moveSpeedMin = 1f;
    public float moveSpeedMax = 5f;
    [Min(1)] public int hitsRequired = 5;
    [Range(0f, 1f)] public float hygienePerKill = 0.25f;
    public float maintainInterval = 0.5f;
    public float edgePadding = 0.25f;

    private Vector2[] directions;
    private float[] speeds;
    private float maintainTimer;
    private CockroachUnit justKilled;
    private SlimeManager subscribedSlimeManager;

    public int ActiveRoachCount
    {
        get
        {
            int count = 0;
            if (roaches != null)
                foreach (CockroachUnit roach in roaches)
                    if (roach != null && roach.gameObject.activeInHierarchy)
                        count++;
            return count;
        }
    }

    private void Awake()
    {
        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();
        InitializeState();
    }

    private void OnEnable()
    {
        InitializeState();
        SubscribeToSlime();
        Maintain();
    }

    private void OnDisable()
    {
        UnsubscribeFromSlime();
    }

    private void SubscribeToSlime()
    {
        if (subscribedSlimeManager == slimeManager)
            return;
        UnsubscribeFromSlime();
        subscribedSlimeManager = slimeManager;
        if (subscribedSlimeManager != null)
            subscribedSlimeManager.StateChanged += HandleSlimeStateChanged;
    }

    private void UnsubscribeFromSlime()
    {
        if (subscribedSlimeManager == null)
            return;
        subscribedSlimeManager.StateChanged -= HandleSlimeStateChanged;
        subscribedSlimeManager = null;
    }

    private void HandleSlimeStateChanged()
    {
        if (isActiveAndEnabled)
            Maintain();
    }

    private void Update()
    {
        UpdateMovement();
        HandleClicks();

        maintainTimer -= Time.deltaTime;
        if (maintainTimer <= 0f)
        {
            maintainTimer = Mathf.Max(0.05f, maintainInterval);
            Maintain();
        }
    }

    private void InitializeState()
    {
        int count = roaches?.Length ?? 0;
        directions = new Vector2[count];
        speeds = new float[count];

        for (int i = 0; i < count; i++)
        {
            directions[i] = RandomDirection();
            speeds[i] = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax);
        }
    }

    private void UpdateMovement()
    {
        if (roaches == null || roaches.Length == 0)
            return;

        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic
            ? camera.orthographicSize - edgePadding
            : 4.5f;
        float halfWidth = camera != null && camera.orthographic
            ? halfHeight * camera.aspect
            : 2.7f;

        for (int i = 0; i < roaches.Length; i++)
        {
            CockroachUnit roach = roaches[i];
            if (roach == null || !roach.gameObject.activeInHierarchy)
                continue;

            Vector2 position = roach.transform.position;
            position += directions[i] * speeds[i] * Time.deltaTime;
            bool bounced = false;

            if (position.x < -halfWidth || position.x > halfWidth)
            {
                position.x = Mathf.Clamp(position.x, -halfWidth, halfWidth);
                directions[i].x = -directions[i].x;
                bounced = true;
            }
            if (position.y < -halfHeight || position.y > halfHeight)
            {
                position.y = Mathf.Clamp(position.y, -halfHeight, halfHeight);
                directions[i].y = -directions[i].y;
                bounced = true;
            }

            if (bounced)
                directions[i] = RandomDirection();

            roach.transform.position = new Vector3(position.x, position.y, roach.transform.position.z);
        }
    }

    private void HandleClicks()
    {
        if (!TryGetPointerPressPosition(out Vector2 screenPosition))
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, 100f);
        if (hit.collider == null)
            return;

        CockroachUnit roach = hit.collider.GetComponent<CockroachUnit>();
        roach?.RegisterHit();
    }

    private static bool TryGetPointerPressPosition(out Vector2 screenPosition)
    {
        // No Android, o mouse pode existir como dispositivo virtual, mas os
        // toques chegam pelo Touchscreen. Tratar os dois evita depender
        // de mouse.leftButton para o comando de acertar a barata.
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    public void OnCockroachKilled(CockroachUnit roach)
    {
        if (roach == null)
            return;

        // Desativa exatamente o objeto que sofreu os cinco acertos.
        justKilled = roach;
        roach.gameObject.SetActive(false);
        roach.ResetCockroach();
        if (slimeManager != null)
            slimeManager.AddHygiene(hygienePerKill);

        Maintain();
        justKilled = null;
    }

    public void Maintain()
    {
        if (roaches == null || slimeManager == null)
            return;

        int desired = slimeManager.Hygiene >= 0.999f
            ? 0
            : Mathf.Clamp(Mathf.CeilToInt((1f - slimeManager.Hygiene) * maxCritters), 0, maxCritters);
        desired = Mathf.Min(desired, roaches.Length);

        var active = new List<CockroachUnit>();
        for (int i = 0; i < roaches.Length; i++)
            if (roaches[i] != null && roaches[i].gameObject.activeSelf)
                active.Add(roaches[i]);

        // Se houver excesso, remove unidades ativas, mas nunca escolhe a
        // unidade que acabou de ser acertada como uma substituta imediata.
        while (active.Count > desired)
        {
            int removeIndex = active.Count - 1;
            if (active[removeIndex] == justKilled)
            {
                removeIndex = active.FindIndex(unit => unit != justKilled);
                if (removeIndex < 0)
                    break;
            }
            CockroachUnit remove = active[removeIndex];
            remove.gameObject.SetActive(false);
            active.RemoveAt(removeIndex);
        }

        // Ativa outra barata da pool para preservar a que foi morta.
        while (active.Count < desired)
        {
            CockroachUnit candidate = null;
            for (int i = 0; i < roaches.Length; i++)
            {
                CockroachUnit unit = roaches[i];
                if (unit != null && unit != justKilled && !unit.gameObject.activeSelf)
                {
                    candidate = unit;
                    break;
                }
            }
            if (candidate == null)
                break;
            candidate.ResetCockroach();
            candidate.gameObject.SetActive(true);
            active.Add(candidate);
        }
    }

    private static Vector2 RandomDirection()
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        return direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
    }
}
