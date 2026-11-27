using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado de "sticker novo" do album. Um sticker conquistado entra como
/// nao visto; so deixa de ser novo quando o card dele e aberto no album.
/// A fonte real e SaveData.seenStickers (o mesmo save de ownedStickers).
/// </summary>
public static class StickerUnseenTracker
{
    /// <summary>Disparado quando um sticker e visto ou a colecao muda.</summary>
    public static event Action Changed;

    private static SaveData fallback;

    static StickerUnseenTracker()
    {
        // Ganhar/resetar sticker muda a quantidade de nao vistos.
        StickerCollection.Changed += NotifyChanged;
    }

    private static SaveData Data()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager != null)
        {
            SaveData data = manager.GetSaveData();
            if (data != null)
                return data;
        }
        return fallback ??= new SaveData();
    }

    private static List<string> Seen()
    {
        SaveData data = Data();
        return data.seenStickers ??= new List<string>();
    }

    /// <summary>Este sticker foi conquistado e ainda nao teve o card aberto?</summary>
    public static bool IsUnseen(StickerData sticker)
    {
        if (!StickerCollection.IsOwned(sticker))
            return false;

        string key = sticker.Key;
        return !string.IsNullOrEmpty(key) && !Seen().Contains(key);
    }

    /// <summary>Quantos stickers conquistados ainda nao foram vistos.</summary>
    public static int UnseenCount()
    {
        StickerData[] catalog = StickerManager.Catalog;
        if (catalog == null)
            return 0;

        int count = 0;
        for (int i = 0; i < catalog.Length; i++)
        {
            if (IsUnseen(catalog[i]))
                count++;
        }
        return count;
    }

    /// <summary>Marca o sticker como visto ao abrir o popup dele no album.</summary>
    public static void MarkSeen(StickerData sticker)
    {
        if (sticker == null || !StickerCollection.IsOwned(sticker))
            return;

        string key = sticker.Key;
        if (string.IsNullOrEmpty(key))
            return;

        List<string> seen = Seen();
        if (seen.Contains(key))
            return;

        seen.Add(key);
        SaveManager.Instance?.RequestSave();
        NotifyChanged();
    }

    /// <summary>
    /// Limpa os vistos quando o album e resetado. Sem isso, um sticker
    /// zerado e conquistado de novo voltaria como "ja visto".
    /// </summary>
    public static void ClearSeen()
    {
        List<string> seen = Seen();
        if (seen.Count == 0)
            return;

        seen.Clear();
        SaveManager.Instance?.RequestSave();
        NotifyChanged();
    }

    /// <summary>Forca atualizacao das bolinhas sem alterar o save.</summary>
    public static void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
