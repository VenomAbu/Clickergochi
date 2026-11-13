
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewFurniture",
    menuName = "Game/Items/Furniture"
)]
public class FurnitureData : ScriptableObject
{
    [Header("Furniture Information")]
    public string itemName;
    public Sprite itemImage;

    [Tooltip("Qual movel real da fase de jogo este asset representa.")]
    public FurnitureKind kind = FurnitureKind.Bed;

    [TextArea(3, 6)]
    public string description;

    public int level;
    public int price;

    [Header("Available Skins")]
    public List<FurnitureSkinData> skins =
        new List<FurnitureSkinData>();
}
