using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controlador da HUD principal (UI Toolkit).
/// Liga a UI (telas, popups, carrossel, contadores) + feels fofos:
/// pop de escala nos botoes, particulas e "+1" no slime, sons suaves
/// (clipes do inspector ou sintetizados). Sem logica de jogo.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainHUDController : MonoBehaviour
{
    [Header("Valores demo (sem logica de jogo)")]
    [Range(0f, 1f)] public float hunger = 0.8f;
    [Range(0f, 1f)] public float fun = 0.5f;
    [Range(0f, 1f)] public float hygiene = 1f;
    [Range(0f, 1f)] public float levelProgress = 0.4f;
    public int level = 1;
    public int slimeCount = 0;

    [Header("Sistemas")]
    public SlimeManager slimeManager;

    [Header("Sons (vazio = sintetizado fofo e calmo)")]
    public AudioClip slimeClip;
    public AudioClip clickClip;
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioClip buyClip;
    [Range(0f, 1f)] public float uiVolume = 0.5f;

    private Label _levelText;
    private VisualElement _levelFill;
    private Button _muteButton;
    private bool _muted;
    private VisualElement _furniturePopup;
    private VisualElement _skinPopup;
    private VisualElement _stickerPopup;
    private Label _furnitureTitle;
    private Label _skinTitle;
    private Label _detailTitle;
    private Label _stickerThumbLabel;
    private Label _stickerNameLabel;
    private Label _stickerDescLabel;
    private string _currentFurniture = "Cama";
    private Button[] _skinCards;
    private Button _slimeButton;
    private int _lastMoodTier = -1;
    private float _moodTimer;
    private readonly List<Label> _slimeLabels = new();
    private readonly List<VisualElement> _screens = new();
    private readonly List<Button> _stickerCards = new();

    private readonly List<string> _skins = new() { "Skin 1", "Skin 2", "Skin 3", "Skin 4", "Skin 5" };
    private int _skinIndex = 1;
    private readonly string[] _furnitureNames = { "Cama", "Sofá", "Planta" };
    private readonly string[] _shopOptions = { "Pacote de Stickers", "Skin dos Slimes", "Skin dos Móveis" };
    private readonly List<FoodItemData> foods = new();

    private readonly string[] _stickerThumbs =
    {
        "⭐", "🌈", "🔥", "🍀", "🎈", "🎵",
        "🐾", "💎", "🍩", "🚀", "🌟", "💫"
    };
    private readonly string[] _stickerNames =
    {
        "Estrela", "Arco-íris", "Foguinho", "Trevo", "Balão", "Musiquinha",
        "Patinha", "Diamante", "Rosquinha", "Foguete", "Estrelinha", "Redemoinho"
    };
    private readonly string[] _stickerDescs =
    {
        "Brilha no escuro do quarto do slime.",
        "Traz cor para os dias nublados.",
        "Aquece o slime no inverno.",
        "Dizem que dá sorte nas colheitas.",
        "Flutua pela casa sem parar.",
        "Toca uma melodia suave.",
        "Marca de um amigo peludo.",
        "Raro e muito brilhante.",
        "Quase dá pra comer de tão fofa.",
        "Pronto para decolar.",
        "Pequena mas poderosa.",
        "Gira sem parar de alegria."
    };

    // ---- Feel: tweens de escala + particulas ----
    private class ScaleTween
    {
        public VisualElement el;
        public float from, to, t, dur;
        public System.Func<float, float> ease;
    }
    private readonly List<ScaleTween> _scaleTweens = new();

    private class FeelParticle
    {
        public VisualElement el;
        public Vector2 vel;
        public float grav, t, life, px, py;
        public bool shrink;
    }
    private readonly List<FeelParticle> _feelParticles = new();
    private IVisualElementScheduledItem _feelTimer;

    private AudioSource _audio;
    private readonly Dictionary<string, AudioClip> _synthCache = new();

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[MainHUD] UIDocument sem root.", this);
            return;
        }

        // Telas (navegacao).
        foreach (var screenName in new[] { "MainScreen", "ShopScreen", "ShopDetailScreen", "StickersScreen", "FoodScreen" })
        {
            var screen = root.Q<VisualElement>(screenName);
            if (screen != null)
                _screens.Add(screen);
        }

        _levelText = root.Q<Label>("LevelText");
        _levelFill = root.Q<VisualElement>("LevelFill");
        _muteButton = root.Q<Button>("MuteButton");
        _furniturePopup = root.Q<VisualElement>("FurniturePopup");
        _skinPopup = root.Q<VisualElement>("SkinPopup");
        _stickerPopup = root.Q<VisualElement>("StickerPopup");
        _furnitureTitle = root.Q<Label>("FurnitureTitle");
        _skinTitle = root.Q<Label>("SkinTitle");
        _detailTitle = root.Q<Label>("DetailTitle");
        _stickerThumbLabel = root.Q<Label>("StickerThumbLabel");
        _stickerNameLabel = root.Q<Label>("StickerNameLabel");
        _stickerDescLabel = root.Q<Label>("StickerDescLabel");

        foreach (var labelName in new[] { "SlimeCountLabel", "ShopSlimeLabel", "DetailSlimeLabel", "StickersSlimeLabel", "FoodSlimeLabel" })
        {
            var label = root.Q<Label>(labelName);
            if (label != null)
                _slimeLabels.Add(label);
        }

        if (slimeManager == null)
            slimeManager = FindFirstObjectByType<SlimeManager>();

        // Tela principal: clique real vai para o SlimeManager (Fase 1).
        var slimeBtn = root.Q<Button>("SlimeButton");
        _slimeButton = slimeBtn;
        if (slimeBtn != null)
            slimeBtn.clicked += () =>
            {
                if (slimeManager != null)
                    SyncFromManager();
                else
                {
                    slimeCount++;
                    RefreshCounters();
                }
            };

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var fb = root.Q<Button>($"Furniture{idx}");
            if (fb != null)
                fb.clicked += () => OpenFurniturePopup(_furnitureNames[idx]);
        }

        var shop = root.Q<Button>("ShopButton");
        if (shop != null)
            shop.clicked += () => { PlayOpen(); ShowScreen("ShopScreen"); };
        var stickers = root.Q<Button>("StickerButton");
        if (stickers != null)
            stickers.clicked += () => { PlayOpen(); ShowScreen("StickersScreen"); };
        var food = root.Q<Button>("FoodButton");
        if (food != null)
            food.clicked += () => { PlayOpen(); ShowScreen("FoodScreen"); };

        if (_muteButton != null)
            _muteButton.clicked += ToggleMute;

        // Baratas: sons e refresh via sistema de audio existente.
        var roaches = FindFirstObjectByType<CockroachManager>();
        if (roaches != null)
        {
            roaches.onCritterHit += PlayClick;
            roaches.onCritterKilled += () => { PlayBuy(); PullFromManager(); };
        }

        // Popup do movel.
        var close = root.Q<Button>("CloseButton");
        if (close != null)
            close.clicked += () => { PlayClose(); Show(_furniturePopup, false); };
        var upgrade = root.Q<Button>("UpgradeButton");
        if (upgrade != null)
            upgrade.clicked += () => { PlayBuy(); Debug.Log($"[MainHUD] Upgrade {_currentFurniture} (demo, sem logica)."); };
        var skin = root.Q<Button>("SkinButton");
        if (skin != null)
            skin.clicked += OpenSkinPopup;
        var back = root.Q<Button>("SkinBackButton");
        if (back != null)
            back.clicked += () => { PlayClose(); Show(_skinPopup, false); Show(_furniturePopup, true); };

        var left = root.Q<Button>("SkinLeft");
        if (left != null)
            left.clicked += () => MoveSkin(-1);
        var right = root.Q<Button>("SkinRight");
        if (right != null)
            right.clicked += () => MoveSkin(1);

        _skinCards = new[] { root.Q<Button>("Skin0"), root.Q<Button>("Skin1"), root.Q<Button>("Skin2") };
        for (int i = 0; i < _skinCards.Length; i++)
        {
            int idx = i;
            if (_skinCards[idx] != null)
                _skinCards[idx].clicked += () => SelectVisibleSkin(idx);
        }

        // Loja: 3 opcoes -> tela de detalhe.
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var option = root.Q<Button>($"Option{idx}");
            if (option != null)
                option.clicked += () => OpenShopDetail(_shopOptions[idx]);
        }
        var shopBack = root.Q<Button>("ShopBack");
        if (shopBack != null)
            shopBack.clicked += () => { PlayClose(); ShowScreen("MainScreen"); };
        var detailBack = root.Q<Button>("DetailBack");
        if (detailBack != null)
            detailBack.clicked += () => { PlayClose(); ShowScreen("ShopScreen"); };

        // Stickers: cards -> popup + grade sempre com 3 colunas.
        for (int i = 0; i < 12; i++)
        {
            int idx = i;
            var card = root.Q<Button>($"Sticker{idx}");
            if (card == null)
                continue;
            card.clicked += () => OpenStickerPopup(idx);
            _stickerCards.Add(card);
        }
        var stickersGrid = root.Q<VisualElement>("StickersGrid");
        if (stickersGrid != null)
        {
            stickersGrid.RegisterCallback<GeometryChangedEvent>(evt => LayoutStickerGrid(stickersGrid));
            LayoutStickerGrid(stickersGrid);
        }
        var stickerClose = root.Q<Button>("StickerCloseButton");
        if (stickerClose != null)
            stickerClose.clicked += () => { PlayClose(); Show(_stickerPopup, false); };
        var stickersBack = root.Q<Button>("StickersBack");
        if (stickersBack != null)
            stickersBack.clicked += () => { PlayClose(); ShowScreen("MainScreen"); };

        // Comidas: cards montados dos FoodItemData + compra real.
        BuildFoodList();
        var foodBack = root.Q<Button>("FoodBack");
        if (foodBack != null)
            foodBack.clicked += () => { PlayClose(); ShowScreen("MainScreen"); };

        // Feel de aperto em todos os botoes (mouse + touch).
        foreach (var btn in root.Query<Button>().Build())
            WireFeel(btn, btn == slimeBtn);

        ShowScreen("MainScreen");
        RefreshCounters();
        RefreshStats();
        RefreshSkins();
        ApplyMuteIcon();
        RefreshMood();
    }

    /// <summary>Puxa gosma e barras do SlimeManager para a UI (Fase 1-2).</summary>
    public void SyncFromManager()
    {
        if (slimeManager == null)
            return;
        slimeManager.RegisterClick();
        PullFromManager();
    }

    public void PullFromManager()
    {
        if (slimeManager == null)
            return;
        slimeCount = Mathf.FloorToInt(slimeManager.Goo);
        hunger = slimeManager.Hunger;
        fun = slimeManager.Fun;
        hygiene = slimeManager.Hygiene;
        RefreshCounters();
        RefreshStats();
        RefreshMood();
    }

    private void BuildFoodList()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        var list = root?.Q<VisualElement>("FoodList");
        if (list == null)
            return;
        list.Clear();
        foods.Clear();
        var all = Resources.LoadAll<FoodItemData>("Food");
        System.Array.Sort(all, (x, y) => x.order.CompareTo(y.order));
        foods.AddRange(all);
        for (int i = 0; i < foods.Count; i++)
        {
            int idx = i;
            var item = foods[idx];
            var card = new VisualElement();
            card.AddToClassList("food-card");
            var img = new Label(item.iconEmoji);
            img.AddToClassList("food-img");
            var info = new VisualElement();
            info.AddToClassList("food-info");
            var name = new Label(item.foodName);
            name.AddToClassList("food-name");
            var price = new Label($"{item.price} slimes");
            price.AddToClassList("food-price");
            info.Add(name);
            info.Add(price);
            var buy = new Button(() => BuyFood(idx)) { text = "Comprar", name = $"Buy{idx}" };
            buy.AddToClassList("buy-btn");
            card.Add(img);
            card.Add(info);
            card.Add(buy);
            list.Add(card);
        }
    }

    private void BuyFood(int index)
    {
        if (slimeManager == null || index < 0 || index >= foods.Count)
            return;
        var item = foods[index];
        if (slimeManager.SpendGoo(item.price))
        {
            slimeManager.AddHunger(item.hungerRestore);
            PlayBuy();
            PullFromManager();
        }
        else
        {
            PlayClose();
            Debug.Log($"[MainHUD] Sem gosma para {item.foodName} ({item.price}).");
        }
    }

    private void Update()
    {
        if (slimeManager == null || _slimeButton == null)
            return;
        _moodTimer -= Time.deltaTime;
        if (_moodTimer > 0f)
            return;
        _moodTimer = 0.5f;
        RefreshMood();
    }

    /// <summary>Aplica texto/cor do humor da euforia no botao do slime (§18).</summary>
    public void RefreshMood()
    {
        if (slimeManager == null || _slimeButton == null)
            return;
        int tier = slimeManager.GetEuphoriaMoodTier();
        if (tier == _lastMoodTier)
            return;
        _lastMoodTier = tier;
        _slimeButton.text = SlimeManager.MoodTexts[tier];
        _slimeButton.style.backgroundColor = new StyleColor(SlimeManager.MoodColors[tier]);
    }

    // API publica para ligar a logica do jogo depois.
    public void SetSlimeCount(int v) { slimeCount = v; RefreshCounters(); }
    public void SetLevel(float progress, int lvl)
    {
        levelProgress = Mathf.Clamp01(progress);
        level = lvl;
        RefreshCounters();
    }
    public void SetStats(float h, float f, float hy)
    {
        hunger = Mathf.Clamp01(h);
        fun = Mathf.Clamp01(f);
        hygiene = Mathf.Clamp01(hy);
        RefreshStats();
    }

    // ---- Feel de botoes ----
    private void WireFeel(Button b, bool isSlime)
    {
        if (b == null)
            return;
        b.RegisterCallback<PointerDownEvent>(evt => StartScaleTween(b, CurrentScale(b), 0.88f, 110f, EaseOutQuad));
        b.RegisterCallback<PointerUpEvent>(evt => StartScaleTween(b, CurrentScale(b), 1f, 260f, EaseOutBack));
        b.RegisterCallback<PointerLeaveEvent>(evt => StartScaleTween(b, CurrentScale(b), 1f, 160f, EaseOutQuad));
        b.clicked += () =>
        {
            if (isSlime)
            {
                PlaySlime();
                SlimeBurst(b);
            }
            else
            {
                PlayClick();
            }
        };
    }

    private static float CurrentScale(VisualElement el)
    {
        try { return el.resolvedStyle.scale.value.x; }
        catch { return 1f; }
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private void StartScaleTween(VisualElement el, float from, float to, float durMs, System.Func<float, float> ease)
    {
        for (int i = _scaleTweens.Count - 1; i >= 0; i--)
        {
            if (_scaleTweens[i].el == el)
                _scaleTweens.RemoveAt(i);
        }
        _scaleTweens.Add(new ScaleTween { el = el, from = from, to = to, t = 0f, dur = Mathf.Max(1f, durMs), ease = ease });
        EnsureFeelTicking();
    }

    private void EnsureFeelTicking()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
            return;
        if (_feelTimer == null)
            _feelTimer = root.schedule.Execute(TickFeel).Every(16);
        _feelTimer.Resume();
    }

    private void TickFeel()
    {
        const float dt = 0.016f;
        for (int i = _scaleTweens.Count - 1; i >= 0; i--)
        {
            var tw = _scaleTweens[i];
            if (tw.el == null || tw.el.panel == null)
            {
                _scaleTweens.RemoveAt(i);
                continue;
            }
            tw.t += dt * 1000f;
            float k = Mathf.Min(1f, tw.t / tw.dur);
            float v = tw.from + (tw.to - tw.from) * tw.ease(k);
            tw.el.style.scale = new StyleScale(new Scale(new Vector2(v, v)));
            if (k >= 1f)
                _scaleTweens.RemoveAt(i);
        }
        for (int i = _feelParticles.Count - 1; i >= 0; i--)
        {
            var p = _feelParticles[i];
            if (p.el == null || p.el.panel == null)
            {
                _feelParticles.RemoveAt(i);
                continue;
            }
            p.t += dt;
            if (p.t >= p.life)
            {
                p.el.RemoveFromHierarchy();
                _feelParticles.RemoveAt(i);
                continue;
            }
            p.vel = new Vector2(p.vel.x, p.vel.y + p.grav * dt);
            p.px += p.vel.x * dt;
            p.py += p.vel.y * dt;
            p.el.style.left = p.px;
            p.el.style.top = p.py;
            p.el.style.opacity = 1f - (p.t / p.life);
            if (p.shrink)
            {
                float s = 1f - 0.5f * (p.t / p.life);
                p.el.style.scale = new StyleScale(new Scale(new Vector2(s, s)));
            }
        }
        if (_scaleTweens.Count == 0 && _feelParticles.Count == 0 && _feelTimer != null)
            _feelTimer.Pause();
    }

    /// <summary>
    /// Explosao fofa no slime: bolinhas pastéis + "+1" flutuante.
    /// </summary>
    private void SlimeBurst(Button slimeButton)
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null || !Application.isPlaying || _feelParticles.Count > 80)
            return;
        Vector2 c = slimeButton.worldBound.center;
        Color[] palette =
        {
            new Color(0.55f, 0.9f, 0.59f),
            new Color(1f, 0.7f, 0.85f),
            new Color(1f, 0.88f, 0.54f),
            new Color(0.79f, 0.72f, 0.96f),
            new Color(0.62f, 0.85f, 1f)
        };
        for (int i = 0; i < 10; i++)
        {
            var d = new VisualElement();
            d.AddToClassList("feel-dot");
            float size = Random.Range(12f, 20f);
            d.style.width = size;
            d.style.height = size;
            float r = size / 2f;
            d.style.borderTopLeftRadius = r;
            d.style.borderTopRightRadius = r;
            d.style.borderBottomLeftRadius = r;
            d.style.borderBottomRightRadius = r;
            d.style.backgroundColor = new StyleColor(palette[Random.Range(0, palette.Length)]);
            float px = c.x - size / 2f;
            float py = c.y - size / 2f;
            d.style.left = px;
            d.style.top = py;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float sp = Random.Range(140f, 300f);
            root.Add(d);
            _feelParticles.Add(new FeelParticle
            {
                el = d,
                vel = new Vector2(Mathf.Cos(ang) * sp, Mathf.Sin(ang) * sp - 120f),
                grav = 520f,
                t = 0f,
                life = Random.Range(0.45f, 0.7f),
                px = px,
                py = py,
                shrink = true
            });
        }
        var more = new Label("+1");
        more.AddToClassList("feel-float");
        float fx = c.x - 20f;
        float fy = c.y - 70f;
        more.style.left = fx;
        more.style.top = fy;
        root.Add(more);
        _feelParticles.Add(new FeelParticle
        {
            el = more,
            vel = new Vector2(0f, -150f),
            grav = 0f,
            t = 0f,
            life = 0.7f,
            px = fx,
            py = fy,
            shrink = false
        });
        EnsureFeelTicking();
    }

    // ---- Sons suaves (clipe do inspector ou sintetizado) ----
    private AudioSource Audio
    {
        get
        {
            if (_audio == null)
            {
                _audio = GetComponent<AudioSource>();
                if (_audio == null)
                    _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
            }
            return _audio;
        }
    }

    private void PlayClick() => PlayClip(clickClip, "ui-click", 720f, 540f, 0.07f, 0.35f);

    private void PlaySlime()
    {
        if (slimeClip != null)
        {
            PlayClip(slimeClip);
        }
        else
        {
            PlayClip(null, "ui-pop", 300f, 660f, 0.16f, 0.5f);
            PlayClip(null, "ui-pop-hi", 660f, 990f, 0.1f, 0.25f);
        }
    }

    private void PlayOpen() => PlayClip(openClip, "ui-open", 440f, 680f, 0.12f, 0.35f);
    private void PlayClose() => PlayClip(closeClip, "ui-close", 520f, 330f, 0.12f, 0.35f);
    private void PlayBuy() => PlayClip(buyClip, "ui-buy", new[] { 523f, 659f, 784f }, 0.09f, 0.4f);

    private void PlayClip(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null)
            return;
        Audio.volume = uiVolume;
        Audio.PlayOneShot(clip);
    }

    private void PlayClip(AudioClip clip, string key, float f0, float f1, float dur, float vol)
    {
        PlayClip(clip != null ? clip : SynthTone(key, f0, f1, dur, vol));
    }

    private void PlayClip(AudioClip clip, string key, float[] notes, float noteDur, float vol)
    {
        PlayClip(clip != null ? clip : SynthMelody(key, notes, noteDur, vol));
    }

    private AudioClip SynthTone(string key, float f0, float f1, float dur, float vol)
    {
        if (_synthCache.TryGetValue(key, out var cached))
            return cached;
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * dur));
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / rate;
            float env = Mathf.Exp(-4f * t) * Mathf.Min(1f, t * 40f);
            data[i] = Mathf.Sin(phase) * env * vol;
        }
        var clip = AudioClip.Create(key, n, 1, rate, false);
        clip.SetData(data, 0);
        _synthCache[key] = clip;
        return clip;
    }

    private AudioClip SynthMelody(string key, float[] notes, float noteDur, float vol)
    {
        if (_synthCache.TryGetValue(key, out var cached))
            return cached;
        const int rate = 44100;
        int per = Mathf.Max(1, (int)(rate * noteDur));
        int n = per * notes.Length;
        var data = new float[n];
        for (int m = 0; m < notes.Length; m++)
        {
            float phase = 0f;
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / per;
                phase += 2f * Mathf.PI * notes[m] / rate;
                float env = Mathf.Exp(-3f * t) * Mathf.Min(1f, t * 40f);
                data[m * per + i] = Mathf.Sin(phase) * env * vol;
            }
        }
        var clip = AudioClip.Create(key, n, 1, rate, false);
        clip.SetData(data, 0);
        _synthCache[key] = clip;
        return clip;
    }

    /// <summary>
    /// Grade de stickers sempre com 3 colunas: o card se ajusta a largura;
    /// se sobrar largura alem do teto, vira padding lateral.
    /// </summary>
    private void LayoutStickerGrid(VisualElement grid)
    {
        float gridW = grid.resolvedStyle.width;
        if (gridW <= 0f || _stickerCards.Count == 0)
            return;
        const float maxCard = 170f;
        const float minSide = 16f;
        const float cardMargin = 8f;
        float card = Mathf.Floor((gridW - minSide * 2f - cardMargin * 6f) / 3f);
        card = Mathf.Clamp(card, 80f, maxCard);
        float side = Mathf.Max(minSide, (gridW - (card * 3f + cardMargin * 6f)) / 2f);
        grid.style.paddingLeft = side;
        grid.style.paddingRight = side;
        foreach (var cardBtn in _stickerCards)
        {
            cardBtn.style.width = card;
            cardBtn.style.height = card * 1.2f;
        }
    }

    private void ShowScreen(string screenName)
    {
        foreach (var screen in _screens)
            screen.EnableInClassList("hidden", screen.name != screenName);
        Show(_furniturePopup, false);
        Show(_skinPopup, false);
        Show(_stickerPopup, false);
    }

    private void OpenShopDetail(string optionName)
    {
        if (_detailTitle != null)
            _detailTitle.text = optionName;
        PlayOpen();
        ShowScreen("ShopDetailScreen");
    }

    private void OpenStickerPopup(int stickerIndex)
    {
        if (_stickerThumbLabel != null)
            _stickerThumbLabel.text = _stickerThumbs[stickerIndex];
        if (_stickerNameLabel != null)
            _stickerNameLabel.text = _stickerNames[stickerIndex];
        if (_stickerDescLabel != null)
            _stickerDescLabel.text = _stickerDescs[stickerIndex];
        PlayOpen();
        Show(_stickerPopup, true);
    }

    private void RefreshCounters()
    {
        foreach (var label in _slimeLabels)
            label.text = $"x {slimeCount}";
        if (_levelText != null)
            _levelText.text = $"Nv {level} • {Mathf.RoundToInt(levelProgress * 100f)}%";
        if (_levelFill != null)
            _levelFill.style.width = new StyleLength(new Length(levelProgress * 100f, LengthUnit.Percent));
    }

    private void RefreshStats()
    {
        SetRing("HungerFill", hunger);
        SetRing("FunFill", fun);
        SetRing("HygieneFill", hygiene);
    }

    private readonly Dictionary<string, StatRingFill> _ringFills = new();
    private readonly Dictionary<string, Color> _ringColors = new()
    {
        { "HungerFill", new Color(1f, 0.667f, 0.471f) },
        { "FunFill", new Color(0.549f, 0.745f, 1f) },
        { "HygieneFill", new Color(0.549f, 0.902f, 0.863f) },
    };
    private readonly Dictionary<string, string> _ringPct = new()
    {
        { "HungerFill", "HungerPct" },
        { "FunFill", "FunPct" },
        { "HygieneFill", "HygienePct" },
    };

    private void SetRing(string fillName, float value)
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
            return;
        if (!_ringFills.TryGetValue(fillName, out var ring) || ring == null || ring.panel == null)
        {
            var host = root.Q<VisualElement>(fillName);
            if (host == null)
                return;
            host.Clear();
            host.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            host.pickingMode = PickingMode.Ignore;
            ring = new StatRingFill();
            ring.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            ring.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            host.Add(ring);
            _ringFills[fillName] = ring;
        }
        ring.SetFill(value, _ringColors[fillName]);
        if (_ringPct.TryGetValue(fillName, out var pctName))
        {
            var pct = root.Q<Label>(pctName);
            if (pct != null)
                pct.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }

    private void ToggleMute()
    {
        _muted = !_muted;
        AudioListener.pause = _muted;
        ApplyMuteIcon();
    }

    private void ApplyMuteIcon()
    {
        if (_muteButton != null)
            _muteButton.text = _muted ? "🔇" : "🔊";
    }

    private void OpenFurniturePopup(string furnitureName)
    {
        _currentFurniture = furnitureName;
        if (_furnitureTitle != null)
            _furnitureTitle.text = furnitureName;
        PlayOpen();
        Show(_skinPopup, false);
        Show(_furniturePopup, true);
    }

    private void OpenSkinPopup()
    {
        if (_skinTitle != null)
            _skinTitle.text = _currentFurniture;
        PlayOpen();
        Show(_furniturePopup, false);
        Show(_skinPopup, true);
        RefreshSkins();
    }

    private void MoveSkin(int dir)
    {
        _skinIndex = (_skinIndex + dir + _skins.Count) % _skins.Count;
        RefreshSkins();
    }

    private void SelectVisibleSkin(int visibleSlot)
    {
        // O slot do meio (1) ja e o selecionado; clicar num vizinho gira o carrossel.
        MoveSkin(visibleSlot - 1);
    }

    private void RefreshSkins()
    {
        if (_skinCards == null)
            return;
        for (int slot = 0; slot < 3; slot++)
        {
            var card = _skinCards[slot];
            if (card == null)
                continue;
            int skinIdx = (_skinIndex + slot - 1 + _skins.Count) % _skins.Count;
            card.text = $"🎨\n{_skins[skinIdx]}";
            card.EnableInClassList("skin-selected", slot == 1);
        }
    }

    private static void Show(VisualElement el, bool visible)
    {
        if (el == null)
            return;
        el.EnableInClassList("hidden", !visible);
    }
}
