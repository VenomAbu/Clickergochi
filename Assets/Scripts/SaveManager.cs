using UnityEngine;
using System.IO;

public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    public static SaveManager Instance => _instance;

    private SaveData saveData;
    private string savePath;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, "save.json");
        Load();
    }

    public SaveData GetSaveData()
    {
        return saveData;
    }

    public void Save()
    {
        saveData.lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(savePath, json);
        Debug.Log($"Game saved to: {savePath}");
    }

    public void Load()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            saveData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log($"Game loaded from: {savePath}");
        }
        else
        {
            saveData = new SaveData();
            Debug.Log("No save file found, creating new save data.");
        }
    }
}
