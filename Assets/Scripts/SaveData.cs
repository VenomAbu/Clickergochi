using System;

[Serializable]
public class SaveData
{
    public int clickCount;
    public int objectLevel;
    public string lastSaveTime;

    public SaveData()
    {
        clickCount = 0;
        objectLevel = 1;
        lastSaveTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
    }
}
