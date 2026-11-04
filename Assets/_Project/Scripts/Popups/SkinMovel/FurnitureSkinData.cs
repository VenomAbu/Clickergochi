
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewFurnitureSkin",
    menuName = "Game/Items/Furniture Skin"
)]
public class FurnitureSkinData : ScriptableObject
{
    [Header("Skin Information")]
    public string skinName;
    public Sprite skinImage;
}
