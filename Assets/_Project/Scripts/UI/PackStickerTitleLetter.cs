using TMPro;
using UnityEngine;

/// <summary>
/// Letra do título do sticker revelado. Fica na cena (filha de
/// PackStickerResult/OpenedStickerTitle) para poder ser organizada no Inspector.
/// </summary>
public class PackStickerTitleLetter : MonoBehaviour
{
    public RectTransform rect;
    public CanvasGroup group;
    public TMP_Text text;
}
