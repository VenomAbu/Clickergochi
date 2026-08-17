using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ClickerManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI clickCountText;
    [SerializeField] private ObjectStats objectStats;
    [SerializeField] private AudioClip clickSound;

    private Camera cam;
    private AudioSource audioSource;

    private void Start()
    {
        cam = Camera.main;
        audioSource = GetComponent<AudioSource>();
        var saveData = SaveManager.Instance.GetSaveData();
        objectStats.lvl = saveData.objectLevel;
        UpdateUI();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                var saveData = SaveManager.Instance.GetSaveData();
                saveData.clickCount += objectStats.lvl;
                SaveManager.Instance.Save();
                UpdateUI();

                if (clickSound != null)
                    audioSource.PlayOneShot(clickSound);
            }
        }
    }

    public void UpdateUI()
    {
        if (clickCountText != null)
            clickCountText.text = SaveManager.Instance.GetSaveData().clickCount.ToString();
    }
}
