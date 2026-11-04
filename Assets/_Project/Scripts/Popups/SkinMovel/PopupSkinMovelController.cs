
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
    private List<FurnitureSkinData> availableSkins;

    private int currentSkinIndex;

    // =========================================
    // INITIALIZATION
    // =========================================

    public void SetFurnitureData(FurnitureData furnitureData)
    {
        currentFurniture = furnitureData;

        availableSkins = currentFurniture != null
            ? currentFurniture.skins
            : null;

        currentSkinIndex = 0;

        UpdateCarousel();
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
            nameTMP.text = selectedSkin.skinName;
            SetImage(skinImage, selectedSkin.skinImage);
        }
        else
        {
            nameTMP.text = string.Empty;
            SetImage(skinImage, null);
        }

        // Prévia da skin anterior.
        SetImage(
            leftSkin,
            previousSkin != null ? previousSkin.skinImage : null
        );

        // Prévia da skin selecionada.
        SetImage(
            middleSkin,
            selectedSkin != null ? selectedSkin.skinImage : null
        );

        // Prévia da próxima skin.
        SetImage(
            rightSkin,
            nextSkin != null ? nextSkin.skinImage : null
        );
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

    private void SetImage(Image target, Sprite sprite)
    {
        if (target == null)
            return;

        target.sprite = sprite;
        target.enabled = sprite != null;
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
