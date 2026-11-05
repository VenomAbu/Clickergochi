
using UnityEngine;

public class VerticalNavegationController : MonoBehaviour
{
    [Header("Horizontal Navigation")]
    [SerializeField] private HorizontalNavegationController horizontalNavigation;

    [Header("Vertical Panels")]
    [SerializeField] private GameObject[] verticalPanels;

    [Header("Shop Panel")]
    [SerializeField] private GameObject shopPanel;
    [Header("Shop Sub Panels")]
    [SerializeField] private GameObject slimeSkinPanel;
    [SerializeField] private GameObject packPanel;
    [SerializeField] private GameObject furnitureSkinPanel;

    [Header("Transitions")]
    [SerializeField] private UITransitionController transitionController;

    private void Awake()
    {
        CloseAllPanels();
    }

    // =========================================
    // OPEN PANEL
    // =========================================

    public void OpenPanel(GameObject panel)
    {
        if (panel == null)
            return;

        if (transitionController != null)
        {
            transitionController.OpenVerticalPanel(
                panel,
                verticalPanels
            );

            return;
        }

        // Fallback caso o controlador de transições não esteja configurado.
        CloseAllPanels();
        panel.SetActive(true);
    }

    public void OpenSlimeSkinPanel()
    {
        OpenPanel(slimeSkinPanel);
    }

    public void OpenPackPanel()
    {
        OpenPanel(packPanel);
    }

    public void OpenFurnitureSkinPanel()
    {
        OpenPanel(furnitureSkinPanel);
    }

    // =========================================
    // CLOSE ALL PANELS
    // =========================================

    public void CloseAllPanels()
    {
        if (verticalPanels == null)
            return;

        foreach (GameObject panel in verticalPanels)
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }

    // =========================================
    // BACK TO HORIZONTAL PANEL
    // =========================================

    public void BackBtt()
    {
        if (transitionController != null)
        {
            transitionController.ReturnToHorizontal(verticalPanels);
            return;
        }

        CloseAllPanels();

        if (horizontalNavigation == null)
        {
            Debug.LogWarning(
                "VerticalNavegationController: HorizontalNavigation não configurado."
            );

            return;
        }

        GameObject currentHorizontalPanel = horizontalNavigation.GetCurrentPanel();

        if (currentHorizontalPanel != null)
            currentHorizontalPanel.SetActive(true);
    }

    // =========================================
    // BACK TO SHOP
    // =========================================

    public void BackBttToShop()
    {
        if (shopPanel != null)
        {
            OpenPanel(shopPanel);
        }
        else
        {
            Debug.LogWarning(
                "VerticalNavegationController: ShopPanel não configurado."
            );
        }
    }
}
