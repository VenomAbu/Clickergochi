using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LevelUpManager : MonoBehaviour
{
    [SerializeField] private ObjectStats objectStats;
    [SerializeField] private TextMeshProUGUI lvlText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI clickCountText;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private ClickerManager clickerManager;

    [Header("Cost Settings")]
    [SerializeField] private int baseCost = 10;
    [SerializeField] private float costMultiplier = 1.5f;

    private void Start()
    {
        var saveData = SaveManager.Instance.GetSaveData();
        objectStats.lvl = saveData.objectLevel;
        levelUpButton.onClick.AddListener(OnLevelUpClick);
        UpdateUI();
    }

    public int GetLevelUpCost()
    {
        return Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, objectStats.lvl - 1));
    }

    private void OnLevelUpClick()
    {
        int cost = GetLevelUpCost();
        var saveData = SaveManager.Instance.GetSaveData();

        if (saveData.clickCount >= cost)
        {
            saveData.clickCount -= cost;
            objectStats.lvl++;
            saveData.objectLevel = objectStats.lvl;
            SaveManager.Instance.Save();
            UpdateUI();

            if (clickerManager != null)
                clickerManager.UpdateUI();
        }
    }

    public void UpdateUI()
    {
        lvlText.text = "Lvl " + objectStats.lvl;
        costText.text = GetLevelUpCost() + " clicks";
        if (clickCountText != null)
            clickCountText.text = SaveManager.Instance.GetSaveData().clickCount.ToString();
    }
}
