
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupSkinMovelController : MonoBehaviour
{
    [Header("Selected Skin")]
    [SerializeField] private TMP_Text nameTMP;
    [SerializeField] private Image skinImage;

    [Header("Skin Carousel")]
    [SerializeField] private Image leftSkin;
    [SerializeField] private Image middleSkin;
    [SerializeField] private Image rightSkin;

    [Header("Popup Controller")]
    [SerializeField] private PopupController popupController;

    private FurnitureData currentFurniture;
    private FurnitureKind currentKind = FurnitureKind.Bed;
    private List<FurnitureSkinData> availableSkins;

    private int currentSkinIndex;
    private bool middleWired;

    // =========================================
    // INITIALIZATION
    // =========================================

    /// <summary>
    /// Define as skins do movel e a atualmente equipada. E o caminho real
    /// do fluxo (o PopupController resolve a lista e o save).
    /// </summary>
    public void SetSkins(
        FurnitureKind kind,
        List<FurnitureSkinData> skins,
        int selectedIndex)
    {
        currentKind = kind;
        availableSkins = skins;
        currentSkinIndex = selectedIndex >= 0 &&
            skins != null && selectedIndex < skins.Count
            ? selectedIndex
            : 0;

        EnsureMiddleClickable();
        UpdateCarousel();
    }

    /// <summary>Compatibilidade com o wiring antigo baseado em FurnitureData.</summary>
    public void SetFurnitureData(FurnitureData furnitureData)
    {
        currentFurniture = furnitureData;
        SetSkins(
            furnitureData != null ? furnitureData.kind : FurnitureKind.Bed,
            furnitureData != null ? furnitureData.skins : null,
            0);
    }

    // =========================================
    // CAROUSEL
    // =========================================

    public void NextSkin()
    {
        if (!HasAvailableSkins())
            return;

        currentSkinIndex++;

        if (currentSkinIndex >= availableSkins.Count)
            currentSkinIndex = 0;

        UpdateCarousel();
    }

    public void PreviousSkin()
    {
        if (!HasAvailableSkins())
            return;

        currentSkinIndex--;

        if (currentSkinIndex < 0)
            currentSkinIndex = availableSkins.Count - 1;

        UpdateCarousel();
    }

    private void UpdateCarousel()
    {
        if (!HasAvailableSkins())
        {
            ClearCarousel();
            return;
        }

        int totalSkins = availableSkins.Count;

        int leftIndex = GetCircularIndex(currentSkinIndex - 1);
        int rightIndex = GetCircularIndex(currentSkinIndex + 1);

        FurnitureSkinData selectedSkin =
            availableSkins[currentSkinIndex];

        FurnitureSkinData previousSkin =
            availableSkins[leftIndex];

        FurnitureSkinData nextSkin =
            availableSkins[rightIndex];

        // Skin selecionada.
        if (selectedSkin != null)
        {
            // Bloqueada: nome + aviso; o equip e travado no EquipSkin.
            nameTMP.text = PopupController.IsSkinOwned(selectedSkin)
                ? selectedSkin.skinName
                : $"{selectedSkin.skinName} - Bloqueada";
            SetImage(skinImage, selectedSkin);
        }
        else
        {
            nameTMP.text = string.Empty;
            SetImage(skinImage, null);
        }

        // Prévia da skin anterior.
        SetImage(leftSkin, previousSkin);

        // Prévia da skin selecionada.
        SetImage(middleSkin, selectedSkin);

        // Prévia da próxima skin.
        SetImage(rightSkin, nextSkin);
    }

    private int GetCircularIndex(int index)
    {
        int count = availableSkins.Count;

        return (index % count + count) % count;
    }

    private bool HasAvailableSkins()
    {
        return availableSkins != null &&
               availableSkins.Count > 0;
    }

    /// <summary>Cor dos previews de skin ainda nao comprada (travada).</summary>
    private static readonly Color LockedColor =
        new Color(0.42f, 0.42f, 0.42f, 1f);

    private void SetImage(Image target, FurnitureSkinData skin)
    {
        if (target == null)
            return;

        bool visible = skin != null && skin.skinImage != null;
        target.sprite = visible ? skin.skinImage : null;
        target.enabled = visible;

        // Escurece o preview de quem nao comprou (a cor base da cena e
        // branca; escrita absoluta = idempotente entre plays).
        target.color = visible && !PopupController.IsSkinOwned(skin)
            ? LockedColor
            : Color.white;
    }

    private void ClearCarousel()
    {
        if (nameTMP != null)
            nameTMP.text = "Nenhuma skin disponível";

        SetImage(skinImage, null);
        SetImage(leftSkin, null);
        SetImage(middleSkin, null);
        SetImage(rightSkin, null);
    }

    // =========================================
    // BUTTONS
    // =========================================

    /// <summary>
    /// Clicar no card do meio (skin selecionada) equipa na hora — sem
    /// botao extra, o proprio preview e o gatilho.
    /// </summary>
    public void EquipSelected()
    {
        if (!HasAvailableSkins() || popupController == null)
            return;

        popupController.EquipSkin(currentKind, currentSkinIndex);
    }

    private void EnsureMiddleClickable()
    {
        if (middleWired || middleSkin == null)
            return;

        middleWired = true;

        // A Image do card precisa de raycast para o clique chegar.
        middleSkin.raycastTarget = true;

        // O card do meio (Main) nao tem Button na cena — adiciona em
        // runtime; o clique na Image do card sobe para o botao do pai.
        Button button = middleSkin.GetComponentInParent<Button>(true);
        if (button == null)
        {
            Transform card = middleSkin.transform.parent;
            GameObject target = card != null
                ? card.gameObject
                : middleSkin.gameObject;
            button = target.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.None;
        // Remove-then-add mantem o gancho unico mesmo com a scene
        // persistindo entre plays (sem reload de scene no Editor).
        button.onClick.RemoveListener(EquipSelected);
        button.onClick.AddListener(EquipSelected);
    }

    public void BackToFurniturePopup()
    {
        if (popupController != null)
            popupController.BackToFurniturePopup();
    }

    public void CloseAllPopups()
    {
        if (popupController != null)
            popupController.CloseAllPopups();
    }
}
