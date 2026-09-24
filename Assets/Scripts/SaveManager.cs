using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    [Header("Save")]
    [Min(0f)] public float saveDebounceSeconds = 0.75f;
    [Min(0f)] public float maxSaveWaitSeconds = 3f;

    private static SaveManager _instance;
    private SaveData saveData;
    private string savePath;
    private bool initialized;
    private bool savePending;
    private float saveAt;
    private float firstSaveRequestAt;

    /// <summary>Notifica quando os dados serão carregados ou criados.</summary>
    public event Action<SaveData> SaveDataLoaded;

    /// <summary>
    /// Permite que sistemas de jogo apliquem alteracoes baseadas no tempo
    /// imediatamente antes de o arquivo ser gravado.
    /// </summary>
    public event Action BeforeSave;

    /// <summary>Notifica qualquer alteração efetivamente persistida.</summary>
    public event Action<SaveData> SaveDataChanged;

    public bool HasPendingSave => savePending;

    public static SaveManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<SaveManager>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    private void Update()
    {
        if (!savePending || Time.realtimeSinceStartup < saveAt)
            return;

        savePending = false;
        WriteSave();
    }

    public SaveData GetSaveData()
    {
        EnsureInitialized();
        return saveData;
    }

    /// <summary>
    /// Solicita uma gravação sem bloquear o frame. O arquivo será escrito
    /// depois que o jogador parar de alterar os dados por um curto intervalo.
    /// </summary>
    public void RequestSave()
    {
        EnsureInitialized();
        float now = Time.realtimeSinceStartup;
        if (!savePending)
            firstSaveRequestAt = now;

        savePending = true;
        float debounceAt = now + Mathf.Max(0f, saveDebounceSeconds);
        float maximumAt = firstSaveRequestAt + Mathf.Max(saveDebounceSeconds, maxSaveWaitSeconds);
        saveAt = Mathf.Min(debounceAt, maximumAt);
    }

    /// <summary>Grava imediatamente. Use em pause/quit ou testes.</summary>
    public void Save()
    {
        EnsureInitialized();
        savePending = false;
        WriteSave();
    }

    private void WriteSave()
    {
        if (saveData == null)
            saveData = new SaveData();

        // SlimeManager pode usar este evento para aplicar o decaimento que
        // ocorreu ate este instante antes de o arquivo ser escrito.
        BeforeSave?.Invoke();

        DateTime nowUtc = DateTime.UtcNow;
        saveData.lastSaveTime = nowUtc.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss");
        saveData.lastSaveUtc = nowUtc.ToString("O");
        if (string.IsNullOrEmpty(saveData.lastCareDecayUtc))
            saveData.lastCareDecayUtc = saveData.lastSaveUtc;

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(savePath, json);
        SaveDataChanged?.Invoke(saveData);
        Debug.Log($"Game saved to: {savePath}");
    }

    public void Load()
    {
        if (string.IsNullOrEmpty(savePath))
            savePath = Path.Combine(Application.persistentDataPath, "save.json");

        savePending = false;
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                saveData = JsonUtility.FromJson<SaveData>(json);
                if (saveData == null)
                    saveData = new SaveData();
                Debug.Log($"Game loaded from: {savePath}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save inválido em {savePath}; criando novos dados. {exception.Message}");
                saveData = new SaveData();
            }
        }
        else
        {
            saveData = new SaveData();
            Debug.Log("No save file found, creating new save data.");
        }

        SaveDataLoaded?.Invoke(saveData);
        SaveDataChanged?.Invoke(saveData);
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        savePath = Path.Combine(Application.persistentDataPath, "save.json");
        initialized = true;
        Load();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && initialized && savePending)
            Save();
    }

    private void OnApplicationQuit()
    {
        if (initialized && saveData != null)
            Save();
    }

    private void OnDisable()
    {
        if (initialized && savePending)
            Save();
    }
}
