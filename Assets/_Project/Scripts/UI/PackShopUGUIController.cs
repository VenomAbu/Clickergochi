using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Apresentação dos pacotes do PackUI.
///
/// Os BuyBtt não gastam gosma e não exigem saldo. Ao clicar, abrem o
/// PopUpConfirm; ao confirmar, escondem a UI do pack e mostram o pacote
/// pulsando/levitando/balançando. Um clique na tela inicia a abertura:
/// o pacote cresce, perde opacidade, desintegra (shader) e solta estrelas.
/// Depois revela um sticker e seu título, ambos com objetos na cena.
/// </summary>
public class PackShopUGUIController : MonoBehaviour
{
    [Header("Referências da UI do Pack")]
    [SerializeField] private GameObject stickerScrollPainel;
    [SerializeField] private GameObject backBtt;
    [SerializeField] private GameObject stickerPackTittle;
    [SerializeField] private GameObject cardCommun;
    [SerializeField] private GameObject cardGolden;
    [SerializeField] private RectTransform comunPack;
    [SerializeField] private RectTransform goldenPack;
    [SerializeField] private Button comunPackImageBtt;
    [SerializeField] private Button goldenPackImageBtt;

    [Header("Resultado do sticker (objetos da cena)")]
    [SerializeField] private GameObject packStickerResult;
    [SerializeField] private RectTransform openedStickerRect;
    [SerializeField] private Image openedStickerImage;
    [SerializeField] private RectTransform stickerTitleRoot;
    [SerializeField] private PackStickerTitleLetter[] titleLetters;
    [SerializeField] private GameObject stickerResultClickArea;
    [SerializeField] private GameObject stickerResultExitBtt;
    [SerializeField] private CanvasGroup stickerResultExitGroup;
    [SerializeField] private Image rarityBG;
    [SerializeField] private TMP_Text rarityText;

    [Header("Popup de pacotes")]
    [SerializeField] private string comumPackName = "Pacote Comum";
    [SerializeField] private string goldenPackName = "Pacote Golden";
    [SerializeField] private string comumPackPrice = "2.000";
    [SerializeField] private string goldenPackPrice = "10.000";
    [SerializeField] private string confirmMessageComum = "Deseja abrir um Pacote Comum?";
    [SerializeField] private string confirmMessageGolden = "Deseja abrir um Pacote Golden?";

    [Header("Pulso (tamanho)")]
    [SerializeField, Range(0f, 0.5f)] private float pulseAmplitude = 0.09f;
    [SerializeField, Min(0.1f)] private float pulsePeriod = 0.8f;

    [Header("Levitacao (altura)")]
    [SerializeField] private float levitateAmplitude = 26f;
    [SerializeField, Min(0.1f)] private float levitatePeriod = 1.6f;

    [Header("Balançada (rotation Z)")]
    [SerializeField, Min(0.01f)] private float swayAngle = 5f;
    [SerializeField, Min(0.01f)] private float swayStepDuration = 0.1f;
    [SerializeField, Min(0.1f)] private float swayInterval = 1f;

    [Header("Abertura do pacote")]
    [SerializeField, Min(0.1f)] private float openDuration = 1.4f;
    [SerializeField, Min(1f)] private float openScale = 1.7f;
    [SerializeField, Min(8)] private int burstStarCount = 22;
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Sprite starSpriteAlt;
    [SerializeField] private float starBurstRadius = 320f;
    [SerializeField] private float starBurstDuration = 1.1f;
    [SerializeField] private Color starBurstColor = new(1f, 0.92f, 0.55f, 1f);

    [Header("Chances por pacote (% - Comum / Épico / Lendário)")]
    [SerializeField] private float[] comumPackChances = { 60f, 30f, 10f };
    [SerializeField] private float[] goldenPackChances = { 30f, 40f, 30f };

    [Header("Animação do sticker")]
    [SerializeField, Min(0.1f)] private float stickerGrowDuration = 0.55f;
    [SerializeField, Min(0.01f)] private float titleCharDelay = 0.055f;
    [SerializeField, Min(0.05f)] private float titleCharDuration = 0.45f;
    [SerializeField] private float titleRiseHeight = 46f;
    [SerializeField] private float titleOvershoot = 16f;

    private sealed class PackState
    {
        public RectTransform Rect;
        public Image Image;
        public Material RuntimeMaterial;
        public Material OriginalMaterial;
        public Vector2 Position;
        public Vector3 Scale;
        public Vector3 Euler;
    }

    private readonly PackState comunState = new();
    private readonly PackState goldenState = new();
    private PackState activePack;
    private float animationTime;
    private float swayZ;
    private Coroutine swayCoroutine;
    private Coroutine openCoroutine;
    private bool waitingForOpenClick;
    private RectTransform starContainer;
    private GameObject clickBlocker;

    private StickerData openedSticker;

    /// <summary>
    /// Acao de saida do fluxo de anuncio: ao sair do sticker, volta para a sala
    /// onde o AD_Btt foi apertado em vez de apenas voltar ao PackUI.
    /// </summary>
    private Action adExitAction;

    private Vector2 stickerBasePosition;
    private Vector3 stickerBaseScale;
    private Vector3 stickerBaseEuler;
    private float[] titleBaseX = Array.Empty<float>();
    private int activeTitleCount;
    private Coroutine resultGrowCoroutine;
    private Coroutine stickerIdleCoroutine;
    private Coroutine titleRevealCoroutine;
    private Coroutine exitButtonCoroutine;

    private void Awake()
    {
        ResolveReferences();
        ResolveResultReferences();
        CaptureBaseState(comunState, comunPack);
        CaptureBaseState(goldenState, goldenPack);
        CaptureResultBaseState();
        CreateClickBlocker();
        WireBuyButtons();
        WireResultButtons();
        ResetView();
    }

    private void OnEnable()
    {
        // Cada vez que a aba do Pack é aberta, volta ao estado normal.
        ResetView();
    }

    private void OnDisable()
    {
        StopAnimations();
        StopResultAnimations();
    }

    private void OnDestroy()
    {
        StopResultAnimations();
        DestroyRuntimeMaterial(comunState);
        DestroyRuntimeMaterial(goldenState);
        if (clickBlocker != null)
            Destroy(clickBlocker);
    }

    private void Update()
    {
        // Durante a abertura quem controla transform é a OpenRoutine.
        if (openCoroutine != null)
            return;

        if (activePack == null || activePack.Rect == null ||
            !activePack.Rect.gameObject.activeInHierarchy)
            return;

        animationTime += Time.deltaTime;

        // Pulso: alterna entre maior e menor.
        float pulse = 1f + pulseAmplitude *
            Mathf.Sin(animationTime * 2f * Mathf.PI / pulsePeriod);
        activePack.Rect.localScale = activePack.Scale * pulse;

        // Levitação: alterna entre mais alto e mais baixo.
        float y = activePack.Position.y + levitateAmplitude *
            Mathf.Sin(animationTime * 2f * Mathf.PI / levitatePeriod);
        activePack.Rect.anchoredPosition =
            new Vector2(activePack.Position.x, y);
    }

    // =========================================
    // BOTOES DE COMPRA
    // =========================================

    public void BuyCommun()
    {
        AskConfirm(comunState, comumPackName, comumPackPrice, confirmMessageComum);
    }

    public void BuyGolden()
    {
        AskConfirm(goldenState, goldenPackName, goldenPackPrice, confirmMessageGolden);
    }

    /// <summary>
    /// Mostra o Golden Pack ganho por um anuncio pulsando/levitando, exatamente
    /// como no fluxo normal. A abertura so acontece quando o jogador toca na
    /// tela (clique no PackOpenClickBlocker). Quando sair do sticker, executa
    /// onExit em vez de ficar no PackUI.
    /// </summary>
    public void ShowGoldenPackFromAd(Action onExit)
    {
        adExitAction = onExit;
        ShowPack(goldenState);
    }

    private void AskConfirm(PackState pack, string packName, string price, string message)
    {
        Sprite icon = pack.Image != null ? pack.Image.sprite : null;

        PopUpConfirmPackController packPopup = PopUpConfirmPackController.Instance;
        if (packPopup == null)
            packPopup = FindFirstObjectByType<PopUpConfirmPackController>(FindObjectsInactive.Include);

        if (packPopup != null)
        {
            packPopup.Show(packName, message, price, () => ShowPack(pack), icon);
            return;
        }

        // Fallback: se o popup de pacotes não estiver na cena, usa o genérico.
        PopUpConfirmController popup = PopUpConfirmController.Instance;
        if (popup == null)
            popup = FindFirstObjectByType<PopUpConfirmController>(FindObjectsInactive.Include);

        if (popup != null)
        {
            popup.Show(packName, message, price, () => ShowPack(pack), icon);
            return;
        }

        ShowPack(pack);
    }

    private void ShowPack(PackState pack)
    {
        if (pack == null || pack.Rect == null)
            return;

        StopAnimations();
        ResetResultUI();
        ClearStars();

        SetActive(stickerScrollPainel, false);
        SetActive(backBtt, false);
        SetActive(stickerPackTittle, false);
        SetActive(cardCommun, false);
        SetActive(cardGolden, false);

        SetActive(comunPack, pack == comunState);
        SetActive(goldenPack, pack == goldenState);
        SetActive(clickBlocker, true);

        PrepareDissolveMaterial(pack);

        activePack = pack;
        animationTime = 0f;
        swayZ = pack.Euler.z;
        pack.Rect.localScale = pack.Scale;
        pack.Rect.anchoredPosition = pack.Position;
        pack.Rect.localRotation = Quaternion.Euler(pack.Euler);
        ApplySway();

        waitingForOpenClick = true;
        swayCoroutine = StartCoroutine(SwayRoutine());
    }

    // =========================================
    // ANIMACAO DE ABERTURA
    // =========================================

    private void StartOpenAnimation()
    {
        if (activePack == null || activePack.Rect == null || openCoroutine != null)
            return;

        waitingForOpenClick = false;
        StopSway();
        SetActive(clickBlocker, false);

        // Sorteia o sticker usando as chances do pacote que está sendo aberto.
        openedSticker = RollSticker(
            activePack == comunState ? comumPackChances : goldenPackChances);

        openCoroutine = StartCoroutine(OpenRoutine(activePack));
    }

    private IEnumerator OpenRoutine(PackState pack)
    {
        RectTransform rect = pack.Rect;
        Image image = pack.Image;
        Vector3 startScale = rect.localScale;
        Vector2 startPos = rect.anchoredPosition;
        Vector3 startEuler = rect.localRotation.eulerAngles;

        // Garante material de dissolve e começa totalmente visível.
        PrepareDissolveMaterial(pack);
        float progress = 0f;
        bool starsSpawned = false;
        SetDissolve(pack, progress);

        for (float elapsed = 0f; elapsed < openDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / openDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            rect.localScale = Vector3.Lerp(startScale, pack.Scale * openScale, eased);
            rect.anchoredPosition = startPos + Vector2.up * (40f * eased);
            rect.localRotation = Quaternion.Euler(
                startEuler.x, startEuler.y, Mathf.LerpAngle(startEuler.z, 0f, eased));

            progress = t;
            SetDissolve(pack, progress);

            // As estrelas começam a sair enquanto o pacote ainda se desfaz.
            if (!starsSpawned && t >= 0.55f)
            {
                SpawnStars(rect);
                starsSpawned = true;
            }

            if (image != null)
            {
                Color c = image.color;
                c.a = 1f - eased;
                image.color = c;
            }

            yield return null;
        }

        SetDissolve(pack, 1f);
        if (!starsSpawned)
            SpawnStars(rect);

        // Estrelas continuam vivendo após o pacote desaparecer.
        SetActive(rect, false);
        openCoroutine = null;

        ShowStickerResult(openedSticker);
    }

    // =========================================
    // SORTEIO DO STICKER
    // =========================================

    private StickerData RollSticker(float[] chances)
    {
        StickerData[] catalog = StickerManager.Catalog;
        if (catalog == null || catalog.Length == 0)
            return null;

        List<StickerData>[] pools = new List<StickerData>[3];
        List<StickerData>[] missingPools = new List<StickerData>[3];
        for (int i = 0; i < pools.Length; i++)
        {
            pools[i] = new List<StickerData>();
            missingPools[i] = new List<StickerData>();
        }

        List<StickerData> withImage = new();
        List<StickerData> missing = new();
        for (int i = 0; i < catalog.Length; i++)
        {
            StickerData sticker = catalog[i];
            if (sticker == null || sticker.stickerImage == null)
                continue;

            withImage.Add(sticker);
            int rarity = (int)sticker.rarity;
            if (rarity < 0 || rarity >= pools.Length)
                continue;

            pools[rarity].Add(sticker);
            // Prioriza completar o album: so entra no pool de repetidos
            // quando todos os stickers daquele tier ja foram ganhos.
            if (!StickerCollection.IsOwned(sticker))
            {
                missing.Add(sticker);
                missingPools[rarity].Add(sticker);
            }
        }

        if (withImage.Count == 0)
            return null;

        float total = 0f;
        if (chances != null)
        {
            for (int i = 0; i < chances.Length && i < pools.Length; i++)
                total += Mathf.Max(0f, chances[i]);
        }

        int chosenRarity = 0;
        if (total > 0f)
        {
            float roll = UnityEngine.Random.Range(0f, total) / total;
            float cumulative = 0f;
            for (int i = 0; i < pools.Length; i++)
            {
                float chance = (i < (chances?.Length ?? 0)) ? Mathf.Max(0f, chances[i]) : 0f;
                cumulative += chance / total;
                if (roll <= cumulative)
                {
                    chosenRarity = i;
                    break;
                }
            }
        }

        List<StickerData> pool = missingPools[chosenRarity];
        if (pool.Count == 0)
            pool = missing;
        if (pool.Count == 0)
            pool = pools[chosenRarity];
        if (pool.Count == 0)
            pool = withImage;

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    // =========================================
    // ESTRELAS
    // =========================================

    private void SpawnStars(RectTransform center)
    {
        if (starSprite == null && starSpriteAlt == null)
            return;
        if (starContainer == null)
            starContainer = CreateStarContainer(center.parent as RectTransform);

        for (int i = 0; i < burstStarCount; i++)
        {
            GameObject star = new($"PackStar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = star.GetComponent<RectTransform>();
            rt.SetParent(starContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            float angle = ((float)i / burstStarCount) * 360f +
                UnityEngine.Random.Range(-14f, 14f);
            float distance = starBurstRadius * UnityEngine.Random.Range(0.55f, 1.1f);
            Vector2 direction = new(Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));
            float size = UnityEngine.Random.Range(38f, 88f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-180f, 180f));

            Image img = star.GetComponent<Image>();
            img.sprite = (i % 2 == 0) ? starSprite : (starSpriteAlt != null ? starSpriteAlt : starSprite);
            img.color = starBurstColor;
            img.raycastTarget = false;

            float spin = UnityEngine.Random.Range(-240f, 240f);
            StartCoroutine(StarRoutine(star, rt, img, direction * distance, spin));
        }
    }

    private IEnumerator StarRoutine(GameObject star, RectTransform rt, Image img,
        Vector2 delta, float spin)
    {
        Vector2 start = Vector2.zero;
        Vector3 startEuler = rt.localRotation.eulerAngles;
        Color startColor = img.color;

        for (float elapsed = 0f; elapsed < starBurstDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / starBurstDuration);
            float easeOut = 1f - Mathf.Pow(1f - t, 2f);
            rt.anchoredPosition = Vector2.Lerp(start, delta, easeOut);
            rt.localRotation = Quaternion.Euler(
                startEuler.x, startEuler.y, startEuler.z + spin * t);

            float scale = 1f + 0.8f * Mathf.Sin(t * Mathf.PI);
            rt.localScale = Vector3.one * scale;

            Color c = startColor;
            c.a = startColor.a * (1f - t * t);
            img.color = c;

            yield return null;
        }

        if (star != null)
            Destroy(star);
    }

    private RectTransform CreateStarContainer(RectTransform parent)
    {
        GameObject go = new("PackOpenStars", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent != null ? parent : transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private void ClearStars()
    {
        if (starContainer == null)
            return;
        for (int i = starContainer.childCount - 1; i >= 0; i--)
            Destroy(starContainer.GetChild(i).gameObject);
    }

    // =========================================
    // STICKER REVELADO (OBJETOS DA CENA)
    // =========================================

    private void ShowStickerResult(StickerData sticker)
    {
        // O sticker revelado entra no save do album (e nos efeitos) na hora.
        StickerCollection.Unlock(sticker);

        ApplyStickerData(sticker);

        SetActive(packStickerResult, true);
        SetActive(stickerResultClickArea, true);
        SetActive(stickerResultExitBtt, false);

        resultGrowCoroutine = StartCoroutine(ResultGrowRoutine());
    }

    private void ApplyStickerData(StickerData sticker)
    {
        if (openedStickerImage != null)
        {
            openedStickerImage.sprite = sticker != null ? sticker.stickerImage : null;
            Color c = openedStickerImage.color;
            c.a = 0f;
            openedStickerImage.color = c;
        }

        if (openedStickerRect != null)
        {
            openedStickerRect.anchoredPosition = stickerBasePosition;
            openedStickerRect.localScale = stickerBaseScale * 0.18f;
            openedStickerRect.localRotation = Quaternion.Euler(stickerBaseEuler);
        }

        ApplyTitleText(sticker != null ? sticker.stickerName : "Sticker");
        ApplyRarity(sticker);
    }

    /// <summary>
    /// Pinta o RarityBG com a cor da raridade sorteada e escreve o label no TMP filho.
    /// </summary>
    private void ApplyRarity(StickerData sticker)
    {
        if (rarityBG == null && rarityText == null)
            return;

        Color color = sticker != null
            ? sticker.GetRarityColor()
            : StickerData.RarityComumColor;
        string label = sticker != null
            ? sticker.GetRarityLabel()
            : "Comum";

        if (rarityBG != null)
            rarityBG.color = color;
        if (rarityText != null)
            rarityText.text = label;
    }

    private void ApplyTitleText(string name)
    {
        activeTitleCount = 0;
        if (titleLetters == null || titleLetters.Length == 0)
            return;

        for (int i = 0; i < titleLetters.Length; i++)
        {
            if (titleLetters[i] != null)
                SetActive(titleLetters[i].gameObject, false);
        }

        if (string.IsNullOrEmpty(name))
            return;

        string text = name.Length <= titleLetters.Length
            ? name
            : name.Substring(0, titleLetters.Length);
        activeTitleCount = text.Length;

        float totalWidth = 0f;
        for (int i = 0; i < activeTitleCount; i++)
        {
            PackStickerTitleLetter letter = titleLetters[i];
            if (letter == null)
                continue;

            char c = text[i];
            if (letter.text != null)
            {
                letter.text.text = char.IsWhiteSpace(c) ? "\u00A0" : c.ToString();
                letter.text.ForceMeshUpdate();
                Vector2 preferred = letter.text.GetPreferredValues(letter.text.text);
                letter.rect.sizeDelta = new Vector2(
                    Mathf.Max(8f, preferred.x), Mathf.Max(1f, preferred.y));
            }

            totalWidth += letter.rect.sizeDelta.x;
            SetActive(letter.gameObject, true);
        }

        float cursor = -totalWidth * 0.5f;
        for (int i = 0; i < activeTitleCount; i++)
        {
            PackStickerTitleLetter letter = titleLetters[i];
            if (letter == null)
                continue;

            titleBaseX[i] = cursor + letter.rect.sizeDelta.x * 0.5f;
            letter.rect.anchoredPosition = new Vector2(titleBaseX[i], titleRiseHeight);
            if (letter.group != null)
                letter.group.alpha = 0f;
            cursor += letter.rect.sizeDelta.x;
        }
    }

    private IEnumerator ResultGrowRoutine()
    {
        if (openedStickerRect == null)
        {
            ShowExitButton();
            yield break;
        }

        Vector3 from = stickerBaseScale * 0.18f;
        Vector3 to = stickerBaseScale;
        Color transparent = openedStickerImage != null
            ? openedStickerImage.color
            : Color.white;
        transparent.a = 0f;
        Color opaque = transparent;
        opaque.a = 1f;

        for (float elapsed = 0f; elapsed < stickerGrowDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / stickerGrowDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            openedStickerRect.localScale = Vector3.Lerp(from, to, eased);
            if (openedStickerImage != null)
                openedStickerImage.color = Color.Lerp(transparent, opaque, Mathf.Clamp01(t * 2f));
            yield return null;
        }

        openedStickerRect.localScale = to;
        if (openedStickerImage != null)
            openedStickerImage.color = opaque;

        stickerIdleCoroutine = StartCoroutine(StickerIdleRoutine());
        titleRevealCoroutine = StartCoroutine(TitleRevealRoutine());
        resultGrowCoroutine = null;
    }

    private IEnumerator StickerIdleRoutine()
    {
        if (openedStickerRect == null)
            yield break;

        RectTransform rect = openedStickerRect;
        float time = 0f;

        while (true)
        {
            time += Time.deltaTime;

            float pulse = 1f + pulseAmplitude *
                Mathf.Sin(time * 2f * Mathf.PI / pulsePeriod);
            rect.localScale = stickerBaseScale * pulse;

            float y = stickerBasePosition.y + levitateAmplitude *
                Mathf.Sin(time * 2f * Mathf.PI / levitatePeriod);
            rect.anchoredPosition = new Vector2(stickerBasePosition.x, y);

            // Balançada: -5 → 5 → -5 → 0, repetida a cada swayInterval.
            float step = Mathf.Max(0.01f, swayStepDuration);
            float cycle = Mathf.Repeat(time, Mathf.Max(0.01f, swayInterval));
            float angle = 0f;
            if (cycle < step)
                angle = Mathf.Lerp(0f, -swayAngle, cycle / step);
            else if (cycle < step * 2f)
                angle = Mathf.Lerp(-swayAngle, swayAngle, (cycle - step) / step);
            else if (cycle < step * 3f)
                angle = Mathf.Lerp(swayAngle, -swayAngle, (cycle - step * 2f) / step);
            else if (cycle < step * 4f)
                angle = Mathf.Lerp(-swayAngle, 0f, (cycle - step * 3f) / step);

            rect.localRotation = Quaternion.Euler(
                stickerBaseEuler.x, stickerBaseEuler.y, stickerBaseEuler.z + angle);

            yield return null;
        }
    }

    private IEnumerator TitleRevealRoutine()
    {
        if (activeTitleCount == 0)
        {
            ShowExitButton();
            yield break;
        }

        float total = (activeTitleCount - 1) * titleCharDelay + titleCharDuration;
        const float downPortion = 0.65f;

        for (float elapsed = 0f; elapsed < total; elapsed += Time.deltaTime)
        {
            for (int i = 0; i < activeTitleCount; i++)
            {
                PackStickerTitleLetter letter = titleLetters[i];
                if (letter == null)
                    continue;

                float local = (elapsed - i * titleCharDelay) / titleCharDuration;

                if (local <= 0f)
                {
                    if (letter.group != null)
                        letter.group.alpha = 0f;
                    letter.rect.anchoredPosition = new Vector2(titleBaseX[i], titleRiseHeight);
                }
                else if (local >= 1f)
                {
                    if (letter.group != null)
                        letter.group.alpha = 1f;
                    letter.rect.anchoredPosition = new Vector2(titleBaseX[i], 0f);
                }
                else
                {
                    if (letter.group != null)
                        letter.group.alpha = Mathf.SmoothStep(0f, 1f, local);

                    float y;
                    if (local < downPortion)
                    {
                        y = Mathf.Lerp(titleRiseHeight, -titleOvershoot,
                            Mathf.SmoothStep(0f, 1f, local / downPortion));
                    }
                    else
                    {
                        y = Mathf.Lerp(-titleOvershoot, 0f,
                            Mathf.SmoothStep(0f, 1f, (local - downPortion) / (1f - downPortion)));
                    }
                    letter.rect.anchoredPosition = new Vector2(titleBaseX[i], y);
                }
            }
            yield return null;
        }

        for (int i = 0; i < activeTitleCount; i++)
        {
            PackStickerTitleLetter letter = titleLetters[i];
            if (letter == null)
                continue;
            if (letter.group != null)
                letter.group.alpha = 1f;
            letter.rect.anchoredPosition = new Vector2(titleBaseX[i], 0f);
        }

        titleRevealCoroutine = null;
        ShowExitButton();
    }

    private void ShowExitButton()
    {
        if (stickerResultExitBtt == null)
            return;

        SetActive(stickerResultExitBtt, true);
        if (stickerResultExitGroup == null)
            return;

        stickerResultExitGroup.alpha = 0f;
        stickerResultExitGroup.interactable = true;
        stickerResultExitGroup.blocksRaycasts = true;
        exitButtonCoroutine = StartCoroutine(FadeExitButtonRoutine());
    }

    private IEnumerator FadeExitButtonRoutine()
    {
        if (stickerResultExitBtt == null)
            yield break;

        RectTransform rect = stickerResultExitBtt.GetComponent<RectTransform>();
        Vector3 from = Vector3.one * 0.82f;
        Vector3 to = Vector3.one;
        const float duration = 0.25f;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            if (stickerResultExitGroup != null)
                stickerResultExitGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            rect.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (stickerResultExitGroup != null)
            stickerResultExitGroup.alpha = 1f;
        rect.localScale = to;
        exitButtonCoroutine = null;
    }

    private void OnExitResult()
    {
        Action exit = adExitAction;
        ResetView();
        exit?.Invoke();
    }

    private void StopResultAnimations()
    {
        if (resultGrowCoroutine != null)
        {
            StopCoroutine(resultGrowCoroutine);
            resultGrowCoroutine = null;
        }
        if (stickerIdleCoroutine != null)
        {
            StopCoroutine(stickerIdleCoroutine);
            stickerIdleCoroutine = null;
        }
        if (titleRevealCoroutine != null)
        {
            StopCoroutine(titleRevealCoroutine);
            titleRevealCoroutine = null;
        }
        if (exitButtonCoroutine != null)
        {
            StopCoroutine(exitButtonCoroutine);
            exitButtonCoroutine = null;
        }
    }

    private void ResetResultUI()
    {
        StopResultAnimations();

        SetActive(packStickerResult, false);
        SetActive(stickerResultClickArea, false);
        SetActive(stickerResultExitBtt, false);

        if (openedStickerRect != null)
        {
            openedStickerRect.anchoredPosition = stickerBasePosition;
            openedStickerRect.localScale = stickerBaseScale;
            openedStickerRect.localRotation = Quaternion.Euler(stickerBaseEuler);
        }

        if (openedStickerImage != null)
        {
            Color c = openedStickerImage.color;
            c.a = 1f;
            openedStickerImage.color = c;
        }

        if (titleLetters != null)
        {
            for (int i = 0; i < titleLetters.Length; i++)
            {
                PackStickerTitleLetter letter = titleLetters[i];
                if (letter == null)
                    continue;
                if (letter.group != null)
                    letter.group.alpha = 0f;
                letter.rect.anchoredPosition = new Vector2(titleBaseX[i], titleRiseHeight);
                SetActive(letter.gameObject, false);
            }
        }

        if (stickerResultExitGroup != null)
            stickerResultExitGroup.alpha = 0f;

        activeTitleCount = 0;
        openedSticker = null;
    }

    // =========================================
    // DISSOLVE / MATERIAL
    // =========================================

    private void PrepareDissolveMaterial(PackState pack)
    {
        if (pack.Image == null)
            return;

        if (pack.RuntimeMaterial == null)
        {
            Shader shader = Shader.Find("UI/UIDissolve");
            if (shader == null)
                return;

            pack.OriginalMaterial = pack.Image.material;
            pack.RuntimeMaterial = new Material(shader)
            {
                name = $"{pack.Image.name}_UIDissolve",
                hideFlags = HideFlags.DontSave
            };
            pack.RuntimeMaterial.SetFloat("_EdgeWidth", 0.05f);
            pack.RuntimeMaterial.SetFloat("_NoiseScale", 14f);
            pack.RuntimeMaterial.SetColor("_EdgeColor", new Color(1f, 0.9f, 0.45f, 1f));
        }

        pack.Image.material = pack.RuntimeMaterial;
        SetDissolve(pack, 0f);
    }

    private void SetDissolve(PackState pack, float progress)
    {
        if (pack.RuntimeMaterial != null)
            pack.RuntimeMaterial.SetFloat("_Progress", Mathf.Clamp01(progress));
    }

    private static void DestroyRuntimeMaterial(PackState pack)
    {
        if (pack.RuntimeMaterial != null)
        {
            Destroy(pack.RuntimeMaterial);
            pack.RuntimeMaterial = null;
        }
    }

    private void RestoreMaterials()
    {
        RestoreMaterial(comunState);
        RestoreMaterial(goldenState);
    }

    private static void RestoreMaterial(PackState pack)
    {
        if (pack.Image == null || pack.RuntimeMaterial == null)
            return;
        pack.Image.material = pack.OriginalMaterial;
    }

    // =========================================
    // BALANCADA DO PACOTE
    // =========================================

    private IEnumerator SwayRoutine()
    {
        float center = activePack.Euler.z;
        float side = Mathf.Abs(swayAngle);

        while (true)
        {
            // Duas alternâncias entre +5 e -5 a cada segundo.
            yield return TweenSway(center + side, swayStepDuration);
            yield return TweenSway(center - side, swayStepDuration);
            yield return TweenSway(center + side, swayStepDuration);
            yield return TweenSway(center, swayStepDuration);

            yield return new WaitForSeconds(
                Mathf.Max(0f, swayInterval - swayStepDuration * 4f));
        }
    }

    private IEnumerator TweenSway(float target, float duration)
    {
        float from = swayZ;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            swayZ = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            ApplySway();
            yield return null;
        }
        swayZ = target;
        ApplySway();
    }

    private void ApplySway()
    {
        if (activePack == null || activePack.Rect == null)
            return;
        activePack.Rect.localRotation = Quaternion.Euler(
            activePack.Euler.x, activePack.Euler.y, swayZ);
    }

    private void StopSway()
    {
        if (swayCoroutine != null)
        {
            StopCoroutine(swayCoroutine);
            swayCoroutine = null;
        }
    }

    private void StopAnimations()
    {
        StopSway();
        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }
        waitingForOpenClick = false;
    }

    // =========================================
    // SETUP
    // =========================================

    private void ResetView()
    {
        adExitAction = null;
        StopAnimations();
        ResetResultUI();
        ClearStars();
        activePack = null;
        animationTime = 0f;

        SetActive(stickerScrollPainel, true);
        SetActive(backBtt, true);
        SetActive(stickerPackTittle, true);
        SetActive(cardCommun, true);
        SetActive(cardGolden, true);
        SetActive(clickBlocker, false);

        RestoreMaterials();
        ResetPack(comunState);
        ResetPack(goldenState);
    }

    private void ResetPack(PackState state)
    {
        if (state == null || state.Rect == null)
            return;

        SetActive(state.Rect, false);
        state.Rect.localScale = state.Scale;
        state.Rect.anchoredPosition = state.Position;
        state.Rect.localRotation = Quaternion.Euler(state.Euler);
        if (state.Image != null)
        {
            Color c = state.Image.color;
            c.a = 1f;
            state.Image.color = c;
        }
        SetDissolve(state, 0f);
    }

    private void CaptureBaseState(PackState state, RectTransform rect)
    {
        if (rect == null)
            return;

        state.Rect = rect;
        state.Image = rect.GetComponent<Image>();
        state.Position = rect.anchoredPosition;
        state.Scale = rect.localScale;
        state.Euler = rect.localRotation.eulerAngles;
    }

    private void CaptureResultBaseState()
    {
        if (openedStickerRect != null)
        {
            stickerBasePosition = openedStickerRect.anchoredPosition;
            stickerBaseScale = openedStickerRect.localScale;
            stickerBaseEuler = openedStickerRect.localRotation.eulerAngles;
        }

        if (titleLetters != null)
            titleBaseX = new float[titleLetters.Length];
    }

    private void WireBuyButtons()
    {
        Button commun = FindButton(cardCommun, "BuyBtt");
        if (commun != null)
            commun.onClick.AddListener(BuyCommun);

        Button golden = FindButton(cardGolden, "BuyBtt");
        if (golden != null)
            golden.onClick.AddListener(BuyGolden);

        // Clique na imagem do pacote tambem abre o popup de confirmacao.
        if (comunPackImageBtt != null)
            comunPackImageBtt.onClick.AddListener(BuyCommun);
        if (goldenPackImageBtt != null)
            goldenPackImageBtt.onClick.AddListener(BuyGolden);
    }

    private void WireResultButtons()
    {
        // Clique em qualquer lugar da area transparente sai do sticker.
        if (stickerResultClickArea != null)
        {
            Button clickArea = stickerResultClickArea.GetComponent<Button>();
            if (clickArea != null)
                clickArea.onClick.AddListener(OnExitResult);
        }

        // O botão visível também sai do sticker e volta ao PackUI normal.
        if (stickerResultExitBtt != null)
        {
            Button exit = stickerResultExitBtt.GetComponent<Button>();
            if (exit != null)
                exit.onClick.AddListener(OnExitResult);
        }
    }

    private static Button FindButton(GameObject root, string name)
    {
        if (root == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].transform.name == name)
                return buttons[i];
        }
        return null;
    }

    private void ResolveReferences()
    {
        if (stickerScrollPainel == null)
            stickerScrollPainel = FindChild("StickerScrollPainel")?.gameObject;
        if (backBtt == null)
            backBtt = FindChild("BackBtt")?.gameObject;
        if (stickerPackTittle == null)
            stickerPackTittle = FindChild("StickerPackTittle")?.gameObject;
        if (cardCommun == null)
            cardCommun = FindChild("CardCommun")?.gameObject;
        if (cardGolden == null)
            cardGolden = FindChild("CardGolden")?.gameObject;
        if (comunPack == null)
            comunPack = FindChild("ComunPack") as RectTransform;
        if (goldenPack == null)
            goldenPack = FindChild("GoldenPack") as RectTransform;

        if (comunPackImageBtt == null && cardCommun != null)
            comunPackImageBtt = FindChild(cardCommun.transform, "PackImage")?.GetComponent<Button>();
        if (goldenPackImageBtt == null && cardGolden != null)
            goldenPackImageBtt = FindChild(cardGolden.transform, "PackImage")?.GetComponent<Button>();
    }

    private void ResolveResultReferences()
    {
        if (packStickerResult == null)
            packStickerResult = FindChild("PackStickerResult")?.gameObject;

        if (packStickerResult != null)
        {
            if (openedStickerRect == null)
                openedStickerRect = FindRect(packStickerResult.transform, "OpenedSticker");
            if (stickerTitleRoot == null)
                stickerTitleRoot = FindRect(packStickerResult.transform, "OpenedStickerTitle");
            if (stickerResultClickArea == null)
                stickerResultClickArea = FindChild(packStickerResult.transform, "StickerResultClickArea")?.gameObject;
            if (stickerResultExitBtt == null)
                stickerResultExitBtt = FindChild(packStickerResult.transform, "StickerResultExitBtt")?.gameObject;
            if (rarityBG == null)
            {
                Transform rarity = FindChild(packStickerResult.transform, "RarityBG");
                if (rarity != null)
                    rarityBG = rarity.GetComponent<Image>();
            }
            if (rarityText == null)
            {
                Transform rarity = FindChild(packStickerResult.transform, "RarityBG");
                if (rarity != null)
                    rarityText = rarity.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (openedStickerRect != null && openedStickerImage == null)
            openedStickerImage = openedStickerRect.GetComponent<Image>();
        if (stickerResultExitBtt != null && stickerResultExitGroup == null)
            stickerResultExitGroup = stickerResultExitBtt.GetComponent<CanvasGroup>();

        if ((titleLetters == null || titleLetters.Length == 0) && stickerTitleRoot != null)
            titleLetters = stickerTitleRoot.GetComponentsInChildren<PackStickerTitleLetter>(true);
    }

    private Transform FindChild(string name)
    {
        return FindChild(transform, name);
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
                return all[i];
        }
        return null;
    }

    private static RectTransform FindRect(Transform root, string name)
    {
        return FindChild(root, name) as RectTransform;
    }

    private void CreateClickBlocker()
    {
        if (clickBlocker != null)
            return;

        // Um Image transparente cobrindo a tela captura o clique de abertura
        // sem deixar o clique cair nos botões que foram escondidos.
        GameObject go = new("PackOpenClickBlocker",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        Image image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        EventTrigger trigger = go.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new()
        {
            eventID = EventTriggerType.PointerDown
        };
        entry.callback.AddListener(_ => StartOpenAnimation());
        trigger.triggers.Add(entry);

        clickBlocker = go;
        SetActive(clickBlocker, false);
    }

    private static void SetActive(UnityEngine.Object target, bool active)
    {
        if (target is GameObject go)
            go.SetActive(active);
        else if (target is Component component && component != null)
            component.gameObject.SetActive(active);
    }
}
