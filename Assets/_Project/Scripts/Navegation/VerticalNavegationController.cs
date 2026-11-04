
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
        CloseAllPanels();

        if (horizontalNavigation == null)
        {
            Debug.LogWarning(
                "VerticalNavegationController: HorizontalNavigation não configurado."
            );
            return;
        }

        GameObject currentHorizontalPanel =
            horizontalNavigation.GetCurrentPanel();

        if (currentHorizontalPanel != null)
            currentHorizontalPanel.SetActive(true);
    }

    // =========================================
    // BACK TO SHOP
    // =========================================

    public void BackBttToShop()
    {
        CloseAllPanels();

        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "VerticalNavegationController: ShopPanel não configurado."
            );
        }
    }
}
