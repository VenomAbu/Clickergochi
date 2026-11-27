using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado do album de stickers baseado no save (nao nos assets). O sorteado
/// ao abrir um pacote entra em ownedStickers; o album, os efeitos e o gacha
/// consultam a mesma fonte. O botao do Album chama ResetOwned() depois da
/// confirmacao no PopUpConfirmTV.
/// </summary>
public static class StickerCollection
{
    /// <summary>Disparado quando a colecao muda.</summary>
    public static event Action Changed;

    private static SaveData fallback;

    /// <summary>Quantidade de stickers ja conquistados no save atual.</summary>
    public static int OwnedCount => Data().ownedStickers.Count;

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

    private static void Persist()
        => SaveManager.Instance?.RequestSave();

    private static void Notify()
        => Changed?.Invoke();

    public static bool IsOwned(StickerData sticker)
    {
        if (sticker == null)
            return false;
        string key = sticker.Key;
        return !string.IsNullOrEmpty(key) && Data().ownedStickers.Contains(key);
    }

    /// <summary>Registra o sticker ganho no pacote e avisa o album.</summary>
    public static void Unlock(StickerData sticker)
    {
        if (sticker == null)
            return;

        string key = sticker.Key;
        if (string.IsNullOrEmpty(key))
            return;

        List<string> owned = Data().ownedStickers;
        if (owned.Contains(key))
            return;

        owned.Add(key);
        Persist();
        Notify();
    }

    /// <summary>Zera todos os stickers do album (acao do botao apos confirmar).</summary>
    public static void ResetOwned()
    {
        List<string> owned = Data().ownedStickers;
        if (owned.Count > 0)
        {
            owned.Clear();
            Persist();
        }

        // Zera tambem os "vistos": ao ganhar de novo o sticker volta a ser novo.
        StickerUnseenTracker.ClearSeen();
        Notify();
    }
}
