using UnityEngine;

/// <summary>
/// Componente de uma barata que existe como GameObject na Scene.
/// A imagem, colisor e movimento ficam no objeto; o controller da scene
/// apenas coordena quantidade, limites e cliques.
/// </summary>
public class CockroachUnit : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public int hitsRequired = 5;

    private CockroachSceneManager owner;
    private Vector3 baseScale = Vector3.one;
    private float pop;
    private int hits;

    public int Hits => hits;
    public bool IsReady => owner != null && spriteRenderer != null;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<CockroachSceneManager>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    public void Configure(CockroachSceneManager sceneManager, SpriteRenderer renderer, int requiredHits)
    {
        owner = sceneManager;
        spriteRenderer = renderer;
        hitsRequired = Mathf.Max(1, requiredHits);
        baseScale = transform.localScale;
        hits = 0;
        pop = 0f;
    }

    public void RegisterHit()
    {
        if (!isActiveAndEnabled || owner == null)
            return;

        hits++;
        pop = 1f;
        if (hits >= hitsRequired)
            owner.OnCockroachKilled(this);
    }

    public void ResetCockroach()
    {
        hits = 0;
        pop = 0f;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (pop <= 0f)
            return;

        pop = Mathf.Max(0f, pop - Time.deltaTime * 5f);
        float scale = 1f + 0.22f * pop;
        transform.localScale = baseScale * scale;
    }
}
