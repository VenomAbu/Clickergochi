
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSticker",
    menuName = "Game/Items/Sticker"
)]
public class StickerData : ScriptableObject
{
    [Header("Sticker Information")]
    public string stickerName;
    public Sprite stickerImage;

    [TextArea(3, 6)]
    public string description;
}
