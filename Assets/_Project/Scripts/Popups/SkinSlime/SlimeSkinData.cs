using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSlimeSkin",
    menuName = "Game/Items/Slime Skin"
)]
public class SlimeSkinData : ScriptableObject
{
    [Header("Skin Information")]
    public string skinName;

    [Tooltip("Preview no card da loja (opcional; sem arte fica o sprite do card).")]
    public Sprite skinImage;

    [Header("Shop")]
    [Tooltip("Id estavel gravado no save (ownedSlimeSkins). Vazio = sempre livre.")]
    public string skinId;

    [Tooltip("Preco em gosma na loja. 0 = padrao, sempre disponivel.")]
    public int skinPrice;

    [Tooltip("Posicao do card na loja (menor primeiro).")]
    public int order;

    [Header("Cena")]
    [Tooltip("Nome do GameObject da skin sob Slimes3D (ex.: 'Slime Magenta King').")]
    public string sceneObjectName;
}
