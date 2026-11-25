
using UnityEngine;

public class VerticalNavegationController : MonoBehaviour
{
    [Header("Horizontal Navigation")]
    [SerializeField] private HorizontalNavegationController horizontalNavigation;

    [Header("Vertical Panels")]
    [SerializeField] private GameObject[] verticalPanels;

    [Header("HUD Panels")]
    [Tooltip("Painéis abertos pelos botões do HUD. Centralizados aqui porque este controlador decide o que abre.")]
    [SerializeField] private GameObject albumPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject foodPanel;
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

        // Musica fica de fora enquanto qualquer ambiente vertical estiver aberto.
        GameAudioManager.Instance?.SetMusicSuppressed(true);

        // Reinicia os efeitos das telas ao abri-las (AlbumUI e SlimeSkinUI).
        AlbumEffectsController.NotifyPanelOpened(panel);
        SlimeSkinEffectsController.NotifyPanelOpened(panel);

        if (transitionController != null)
        {
            transitionController.OpenVerticalPanel(
                panel,
                verticalPanels
            );

            return;
        }

        // Fallback caso o controlador de transi��es n�o esteja configurado.
        CloseAllPanels();
        panel.SetActive(true);
    }

    // =========================================
    // HUD PANEL BUTTONS
    // =========================================

    public void OpenAlbum()
    {
        OpenPanel(albumPanel);
    }

    public void OpenShop()
    {
        OpenPanel(shopPanel);
    }

    public void OpenFood()
    {
        OpenPanel(foodPanel);
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
        // Voltou para uma sala horizontal: libera a musica novamente.
        GameAudioManager.Instance?.SetMusicSuppressed(false);

        if (transitionController != null)
        {
            transitionController.ReturnToHorizontal(verticalPanels);
            return;
        }

        CloseAllPanels();

        if (horizontalNavigation == null)
        {
            Debug.LogWarning(
                "VerticalNavegationController: HorizontalNavigation n�o configurado."
            );

            return;
        }

        GameObject currentHorizontalPanel = horizontalNavigation.GetCurrentPanel();

        if (currentHorizontalPanel != null)
            currentHorizontalPanel.SetActive(true);
    }

    /// <summary>
    /// Fecha o painel vertical e volta para uma sala horizontal especifica.
    /// Usado pelo AD_Btt apos o anuncio, para nao ficar no PackUI.
    /// </summary>
    public void ReturnToRoom(GameObject roomPanel)
    {
        if (roomPanel != null && horizontalNavigation != null)
            horizontalNavigation.SetCurrentPanel(roomPanel);

        BackBtt();
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
                "VerticalNavegationController: ShopPanel n�o configurado."
            );
        }
    }
}
