using UnityEngine;

/// <summary>
/// Dados de uma comida: preco, saciedade e buffs futuros.
/// Ajuste tudo pelo Inspector sem tocar em codigo.
/// </summary>
[CreateAssetMenu(fileName = "Food", menuName = "Clickergochi/Food Item")]
public class FoodItemData : ScriptableObject
{
    [Tooltip("Ordem na loja (menor aparece primeiro).")]
    public int order;
    public string foodName = "Comida";
    public string iconEmoji = "🍎";
    [Min(0)] public int price = 10;
    [Range(0f, 1f)] public float hungerRestore = 0.2f;

    [Header("Buffs (futuro)")]
    [Tooltip("0 = sem buff.")]
    public float buffDurationMinutes;
    public float gooMultiplier = 1f;
    public float activeProductionMultiplier = 1f;
    public float idleProductionMultiplier = 1f;
}
