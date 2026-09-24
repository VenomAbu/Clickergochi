using System;

[Serializable]
public class SaveData
{
    public int clickCount;
    public int objectLevel;

    // lastSaveTime e mantido para compatibilidade/visualizacao.
    // As datas com fuso/UTC sao usadas para calcular o tempo de forma confiavel.
    public string lastSaveTime;
    public string lastSaveUtc;
    public string lastCareDecayUtc;

    // Sistemas do slime (Fase 1-2). Saves antigos sem esses campos
    // carregam 0 e as barras sao refeitas jogando.
    public float goo;
    public float hunger;
    public float fun;
    public float hygiene;
    public string euphoriaEndUtc;

    public SaveData()
    {
        clickCount = 0;
        objectLevel = 1;
        lastSaveTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
        lastSaveUtc = "";
        lastCareDecayUtc = "";
        goo = 0f;
        hunger = 0.5f;
        fun = 0.5f;
        hygiene = 0.5f;
        euphoriaEndUtc = "";
    }
}
