
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

    [TextArea(3, 6)]
    public string description;

    public int level;
    public int price;

    [Header("Available Skins")]
    public List<FurnitureSkinData> skins =
        new List<FurnitureSkinData>();
}
