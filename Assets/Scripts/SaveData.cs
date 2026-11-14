using System;
using System.Collections.Generic;

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

    // Progressao do comodo e moveis (GDD: comodo permanente, moveis resetam
    // a cada nivel do comodo). Saves antigos iniciam no comodo Lv.1 com os
    // moveis zerados, sem desbloqueios automaticos.
    public int comodoLevel;
    public int bedLevel;
    public int wardrobeLevel;
    public int dresserLevel;

    // Ultima vez em que a producao da Cama foi liquidada (loop rodando ou
    // fixado ao ir para segundo plano). E a base do credito de gosma
    // offline em UTC — no Android/iOS o Update() para ao suspender.
    public string lastAfkUtc;

    // Buffs temporarios de comida (Pizza/Omelete/Macarrao). kind guarda o
    // FoodEffectKind; os buffs de comidas diferentes acumulam entre si e
    // expiram pelo relogio de parede (valiam mesmo com o jogo fechado).
    public List<ActiveFoodBuff> foodBuffs;

    // Descontos de comida (Sanduiche/Milk-shake): apenas UM vale por
    // aprimoramento — mantem o maior desconto comprado e e consumido na
    // compra bem-sucedida.
    public float nextFurnitureDiscount;
    public float nextComodoDiscount;

    // Panquecas: +stickers no proximo pacote de stickers.
    public int pendingStickerPackBonus;

    // Skin equipada em cada movel (index na lista de skins do
    // FurnitureData; 0 = padrao). Saves antigos nao tem o campo e abrem
    // com o sprite original, sem desbloqueios automaticos.
    public int[] furnitureSkinIndex;

    // Ids das skins de moveis compradas (FurnitureSkinData.skinId).
    // O padrao (index0) nunca entra: e sempre livre.
    public List<string> ownedSkins;

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
        comodoLevel = 1;
        bedLevel = 0;
        wardrobeLevel = 0;
        dresserLevel = 0;
        lastAfkUtc = "";
        foodBuffs = new List<ActiveFoodBuff>();
        nextFurnitureDiscount = 0f;
        nextComodoDiscount = 0f;
        pendingStickerPackBonus = 0;
        furnitureSkinIndex = Array.Empty<int>();
        ownedSkins = new List<string>();
    }

    /// <summary>
    /// Corrige valores invalidos vindos de saves antigos ou editados a mao.
    /// O JsonUtility nao roda o construtor ao carregar campos ausentes, entao
    /// comodoLevel pode vir 0 e precisa ser normalizado para 1.
    /// </summary>
    public void Normalize()
    {
        comodoLevel = Math.Max(1, comodoLevel);
        bedLevel = Math.Max(0, bedLevel);
        wardrobeLevel = Math.Max(0, wardrobeLevel);
        dresserLevel = Math.Max(0, dresserLevel);
        goo = Math.Max(0f, goo);
        // Campos string ausentes em saves antigos vem null (o JsonUtility
        // nao roda o construtor).
        if (lastAfkUtc == null)
            lastAfkUtc = "";
        if (foodBuffs == null)
            foodBuffs = new List<ActiveFoodBuff>();
        nextFurnitureDiscount = Math.Min(1f, Math.Max(0f, nextFurnitureDiscount));
        nextComodoDiscount = Math.Min(1f, Math.Max(0f, nextComodoDiscount));
        pendingStickerPackBonus = Math.Max(0, pendingStickerPackBonus);
        if (furnitureSkinIndex == null)
            furnitureSkinIndex = Array.Empty<int>();
        else
        {
            for (int i = 0; i < furnitureSkinIndex.Length; i++)
                furnitureSkinIndex[i] = Math.Max(0, furnitureSkinIndex[i]);
        }
        if (ownedSkins == null)
            ownedSkins = new List<string>();
        else
        {
            // Remove ids nulos/duplicados vindos de save editado a mao.
            for (int i = ownedSkins.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(ownedSkins[i]) ||
                    ownedSkins.IndexOf(ownedSkins[i]) != i)
                    ownedSkins.RemoveAt(i);
            }
        }
    }
}

/// <summary>Buff temporario de comida ativo (expira por relogio UTC).</summary>
[Serializable]
public class ActiveFoodBuff
{
    public int kind;
    public float amount;
    public string endUtc;
}
