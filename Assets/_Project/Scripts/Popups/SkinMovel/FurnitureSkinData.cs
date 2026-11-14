
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

    [Header("Shop")]
    [Tooltip("Id estavel gravado no save (ownedSkins). Vazio = sempre livre.")]
    public string skinId;

    [Tooltip("Preco em gosma na loja. 0 = padrao, sempre disponivel.")]
    public int skinPrice;
}
