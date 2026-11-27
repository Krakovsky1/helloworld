using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// FR-22: Okno ponuky vylepšenia budovy.
// Otvorí sa po kliknutí na ikonku H / R / B nad budovou (BuildingUpgradeManager).
// Ukáže úroveň pred a po vylepšení, cenu v minciach a energii a tlačidlá VYLEPŠIŤ / ZAVRIEŤ.
// FR-29: ukáže aj čas stavby a druhé tlačidlo "ZA [drahokam] N" (zaplatiť drahokamami namiesto mincí).
//        Pri platbe drahokamami sa energia neplatí.
//        Počas stavby okno ukazuje odpočet, priebeh a tlačidlo "DOKONČIŤ [drahokam] N" (okamžite hotové).
// Dizajn je rovnaký ako panel Mince / Energia hore (ResourceHud): tmavé drevo so zlatým okrajom.
// Celé UI sa vytvára v kóde, v scéne netreba nič nastavovať.
public class UpgradePopup : MonoBehaviour
{
    // Údaje, ktoré okno zobrazí (pripravuje ich BuildingUpgradeManager).
    public struct Info
    {
        public string nameNominative;   // "Reštaurácia" - pre text pri MAX úrovni
        public string nameAccusative;   // "Reštauráciu" - pre nadpis "Vylepšiť ..."
        public Sprite icon;             // ikonka budovy (H / R / B)
        public int level;               // aktuálna úroveň, 1 = Level 1
        public int maxLevel;            // počet úrovní budovy
        public int coinCost;            // cena vylepšenia v minciach
        public int energyCost;          // cena vylepšenia v energii
        public bool isNew;              // budova ešte nie je postavená (napr. bazén) -> okno "Postaviť"
        public string blockedReason;    // ak nie je prázdne, vylepšiť sa nedá (napr. "Najprv vylepši Hotel na Level 3")
        public int gemCost;             // FR-29: cena v drahokamoch namiesto mincí a energie (0 = len mince)
        public int buildSeconds;        // FR-29: ako dlho bude stavba trvať
        public Construction construction; // FR-29: prebiehajúca stavba (null = budova sa nestavia)
        public bool IsBuilding => construction != null;
        public bool IsMax => !isNew && level >= maxLevel && construction == null;
    }

    // ---------- farby (rovnaké ako ResourceHud) ----------
    static readonly Color Wood = new Color(0.16f, 0.1f, 0.06f, 0.98f);
    static readonly Color WoodTop = new Color(0.28f, 0.18f, 0.1f, 0.98f);
    static readonly Color Gold = new Color(0.93f, 0.72f, 0.28f, 1f);
    static readonly Color GoldLight = new Color(1f, 0.9f, 0.55f, 1f);
    static readonly Color TitleColor = new Color(0.98f, 0.86f, 0.45f, 1f);
    static readonly Color ValueColor = new Color(1f, 0.97f, 0.88f, 1f);
    static readonly Color SubColor = new Color(0.91f, 0.84f, 0.69f, 1f);
    static readonly Color OutlineColor = new Color(0.16f, 0.08f, 0.03f, 0.95f);
    static readonly Color ShortColor = new Color(1f, 0.42f, 0.38f, 1f);

    // ---------- rozmery (referenčné rozlíšenie 1920 x 1080) ----------
    // Kompaktné rozloženie (FR-29): menší panel, menšie písmo a tlačidlá.
    const float PanelWidth = 720f;
    const float PanelHeight = 500f;
    const float PanelHeightMax = 380f;
    const float ButtonY = -435f;
    const float PanelHeightBuilding = 440f;   // odpočet stavby
    const float ButtonYBuilding = -375f;
    const float NoteY = -365f;
    const float NoteYBuilding = -312f;
    const float ButtonHeight = 66f;
    const float WideButton = 270f;     // 2 tlačidlá vedľa seba
    const float NarrowButton = 212f;   // 3 tlačidlá vedľa seba

    Info info;
    Action onUpgrade;
    Action onUpgradeGems;
    Action onSpeedUp;
    bool built;
    float openAnim;

    GameObject root;
    RectTransform panel;
    Image badge;
    Text title;
    Text subtitle;
    GameObject levelsGroup;
    Text currentLevel;
    Text nextLevel;
    Text nextCaption;
    Text costLabel;
    CostRow coinRow;
    CostRow energyRow;
    Text note;
    GameObject maxGroup;
    Text maxMessage;
    Button upgradeButton;
    Image upgradeImage;
    Text upgradeText;
    Button closeButton;
    RectTransform closeRect;
    CostRow timeRow;
    GemButton gemButton;
    GameObject buildGroup;
    Text countdown;
    RectTransform progressFill;
    int shownSpeedUpCost = -1;
    int languageRevision = -1;

    // Fialové tlačidlo s drahokamom: [prefix] [ikonka] [číslo]
    class GemButton
    {
        public Button button;
        public Image image;
        public Shadow shadow;
        public RectTransform rect;
        public Text prefix;
        public Text value;
    }

    class CostRow
    {
        public GameObject root;
        public Text value;
    }

    public bool IsOpen => root != null && root.activeSelf;

    // FR-29: ukazuje okno práve túto stavbu? (po dokončení ho budova zavrie)
    public bool IsShowing(Construction construction) => IsOpen && construction != null && info.construction == construction;

    // Vytvorí okno ako samostatný Canvas (nad HUD aj ikonkami budov).
    public static UpgradePopup Create(Transform parent)
    {
        var go = new GameObject("UpgradePopup");
        go.transform.SetParent(parent, false);
        var popup = go.AddComponent<UpgradePopup>();
        popup.Build();
        return popup;
    }

    public void Show(Info data, Action upgradeAction)
    {
        Show(data, upgradeAction, null, null);
    }

    // FR-29: upgradeWithGems = zaplatiť drahokamami namiesto mincí, speedUp = okamžite dokončiť stavbu.
    public void Show(Info data, Action upgradeAction, Action upgradeWithGems, Action speedUp)
    {
        Build();
        info = data;
        onUpgrade = upgradeAction;
        onUpgradeGems = upgradeWithGems;
        onSpeedUp = speedUp;
        shownSpeedUpCost = -1;

        badge.sprite = data.icon;
        badge.enabled = data.icon != null;

        bool isMax = data.IsMax;
        bool isBuilding = data.IsBuilding;
        bool offer = !isMax && !isBuilding;
        GameTextView.Set(title, () =>
        {
            string key = GameText.BuildingKey(data.nameNominative);
            if (isMax || isBuilding) return GameText.BuildingName(data.nameNominative);
            return key != null ? GameText.Get((data.isNew ? "build_" : "upgrade_") + key) : data.nameNominative;
        });
        if (isBuilding)
            GameTextView.Set(subtitle, () => data.isNew ? GameText.Get("build_new_level") : GameText.Get("upgrade_to_level", data.construction.targetLevel));
        else
            GameTextView.Set(subtitle, () => data.isNew ? GameText.Get("new_building_max", data.maxLevel) : GameText.Get("level_progress", data.level, data.maxLevel));

        levelsGroup.SetActive(offer);
        costLabel.gameObject.SetActive(offer);
        coinRow.root.SetActive(offer);
        energyRow.root.SetActive(offer);
        timeRow.root.SetActive(offer);
        upgradeButton.gameObject.SetActive(offer);
        maxGroup.SetActive(isMax);
        buildGroup.SetActive(isBuilding);

        bool gemsOffered = offer && onUpgradeGems != null && data.gemCost > 0;
        gemButton.button.gameObject.SetActive(gemsOffered || (isBuilding && onSpeedUp != null));

        if (isMax)
        {
            GameTextView.Set(maxMessage, () => GameText.Get("building_max_level", GameText.BuildingName(data.nameNominative)));
            note.gameObject.SetActive(false);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeightMax);
            closeRect.anchoredPosition = new Vector2(0f, -310f);
            closeRect.sizeDelta = new Vector2(WideButton, ButtonHeight);
        }
        else if (isBuilding)
        {
            // Odpočet stavby: [DOKONČIŤ drahokam N] [ZAVRIEŤ]
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeightBuilding);
            SetNoteY(NoteYBuilding);
            GameTextView.Set(gemButton.prefix, () => GameText.Get("finish"));
            gemButton.rect.anchoredPosition = new Vector2(-145f, ButtonYBuilding);
            gemButton.rect.sizeDelta = new Vector2(WideButton, ButtonHeight);
            closeRect.anchoredPosition = new Vector2(onSpeedUp != null ? 145f : 0f, ButtonYBuilding);
            closeRect.sizeDelta = new Vector2(WideButton, ButtonHeight);
            UpdateBuilding();
        }
        else
        {
            GameTextView.Set(currentLevel, () => data.isNew ? GameText.Get("not_built") : GameText.Get("level", data.level));
            currentLevel.fontSize = data.isNew ? 22 : 28;
            GameTextView.Set(nextLevel, () => GameText.Get("level", data.level + 1));
            GameTextView.Set(nextCaption, () => GameText.Get(data.isNew ? "after_build" : "after_upgrade"));
            GameTextView.Set(costLabel, () => GameText.Get(data.isNew ? "build_cost_time" : "upgrade_cost_time"));
            GameTextView.Set(upgradeText, () => GameText.Get(data.isNew ? "build" : "upgrade"));
            coinRow.value.text = Format(data.coinCost);
            energyRow.value.text = Format(data.energyCost);
            GameTextView.Set(timeRow.value, () => data.buildSeconds > 0 ? BuildTimers.FormatDuration(data.buildSeconds) : GameText.Get("instant"));
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            SetNoteY(NoteY);

            if (gemsOffered)
            {
                // [POSTAVIŤ / VYLEPŠIŤ] [ZA drahokam N] [ZAVRIEŤ]
                GameTextView.Set(gemButton.prefix, () => GameText.Get("for_cost"));
                gemButton.value.text = Format(data.gemCost);
                SetButton(upgradeButton, new Vector2(-224f, ButtonY), NarrowButton);
                gemButton.rect.anchoredPosition = new Vector2(0f, ButtonY);
                gemButton.rect.sizeDelta = new Vector2(NarrowButton, ButtonHeight);
                closeRect.anchoredPosition = new Vector2(224f, ButtonY);
                closeRect.sizeDelta = new Vector2(NarrowButton, ButtonHeight);
            }
            else
            {
                SetButton(upgradeButton, new Vector2(-145f, ButtonY), WideButton);
                closeRect.anchoredPosition = new Vector2(145f, ButtonY);
                closeRect.sizeDelta = new Vector2(WideButton, ButtonHeight);
            }
        }

        root.SetActive(true);
        openAnim = 1f;
        panel.localScale = Vector3.one * 0.85f;

        if (ResortResources.Instance != null)
        {
            ResortResources.Instance.Changed -= Refresh;
            ResortResources.Instance.Changed += Refresh;
        }
        Refresh();
    }

    public void Close()
    {
        if (ResortResources.Instance != null)
            ResortResources.Instance.Changed -= Refresh;
        onUpgrade = null;
        onUpgradeGems = null;
        onSpeedUp = null;
        if (root != null) root.SetActive(false);
    }

    // Prepočíta, či je dosť mincí a energie (volá sa aj pri každej zmene zdrojov).
    public void Refresh()
    {
        if (!IsOpen || info.IsMax) return;
        if (info.IsBuilding)
        {
            UpdateBuilding();
            return;
        }

        var res = ResortResources.Instance;
        int coins = res != null ? res.Coins : 0;
        int gems = res != null ? res.Gems : 0;
        int energy = res != null ? res.Energy : 0;
        bool coinsOk = res != null && coins >= info.coinCost;
        bool energyOk = res != null && energy >= info.energyCost;
        bool gemsOffered = onUpgradeGems != null && info.gemCost > 0;
        bool gemsOk = gemsOffered && res != null && gems >= info.gemCost;

        coinRow.value.color = coinsOk ? ValueColor : ShortColor;
        energyRow.value.color = energyOk ? ValueColor : ShortColor;

        bool blocked = !string.IsNullOrEmpty(info.blockedReason);
        bool canUpgrade = coinsOk && energyOk && !blocked;
        upgradeButton.interactable = canUpgrade;
        upgradeImage.sprite = canUpgrade ? Sprites.Green : Sprites.Gray;
        upgradeText.color = canUpgrade ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);

        // Pri platbe drahokamami sa energia neplatí - stačí mať dosť drahokamov.
        if (gemsOffered) SetGemButtonState(gemsOk && !blocked, gemsOk);

        string message = "";
        bool hint = false;
        bool missing = !coinsOk || !energyOk;
        if (blocked)
            message = info.blockedReason.StartsWith("Najprv vylepši Hotel na Level ") ? GameText.Get("upgrade_hotel_first", info.level + 1) : info.blockedReason;
        else if (res == null)
            message = GameText.Get("resources_unavailable");
        else if (missing && gemsOk)
        {
            message = GameText.Get(!coinsOk && !energyOk ? "missing_both_pay_gems" : !coinsOk ? "missing_coins_pay_gems" : "missing_energy_pay_gems");
            hint = true;
        }
        else if (missing && gemsOffered)
            message = GameText.Get(!coinsOk && !energyOk ? "missing_all_resources" : !coinsOk ? "missing_coins_and_gems" : "missing_energy_and_gems");
        else if (!coinsOk && !energyOk)
            message = GameText.Get("missing_coins_energy");
        else if (!coinsOk)
            message = GameText.Get("missing_coins_amount", Format(info.coinCost - coins));
        else if (!energyOk)
            message = GameText.Get("missing_energy_amount", Format(info.energyCost - energy));

        note.text = message;
        note.color = blocked || hint ? TitleColor : ShortColor;   // podmienka / rada = zlatá, nedostatok = červená
        note.gameObject.SetActive(message.Length > 0);
    }

    // FR-29: odpočet, priebeh a cena urýchlenia (volá sa každý snímok, kým sa stavia).
    void UpdateBuilding()
    {
        var c = info.construction;
        if (c == null) return;

        bool waiting = c.WaitingForServer;
        countdown.text = waiting ? "--:--" : BuildTimers.FormatCountdown(c.Remaining);
        progressFill.anchorMax = new Vector2(Mathf.Max(0.02f, c.Progress), 1f);

        // Bez internetu odpočet stojí - len vtedy sa ukáže vysvetlenie (v riadku pod priebehom).
        string WaitingText = GameText.Get("waiting_for_internet");

        if (onSpeedUp == null)
        {
            SetNote(waiting ? WaitingText : "", TitleColor);
            return;
        }

        int cost = c.SpeedUpCost;
        var res = ResortResources.Instance;
        int gems = res != null ? res.Gems : 0;
        bool gemsOk = res != null && gems >= cost;
        bool done = c.IsDone;

        if (cost != shownSpeedUpCost)
        {
            shownSpeedUpCost = cost;
            gemButton.value.text = Format(Mathf.Max(1, cost));
        }
        SetGemButtonState(gemsOk && !done, gemsOk);

        if (!gemsOk) SetNote(GameText.Get("missing_gems_amount", Format(cost - gems)), ShortColor);
        else SetNote(waiting ? WaitingText : "", TitleColor);
    }

    void SetNote(string message, Color color)
    {
        if (note.text != message) note.text = message;
        note.color = color;
        note.gameObject.SetActive(message.Length > 0);
    }

    void SetNoteY(float y)
    {
        var rect = note.rectTransform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    void SetGemButtonState(bool interactable, bool enough)
    {
        gemButton.button.interactable = interactable;
        gemButton.image.sprite = interactable ? Sprites.Purple : Sprites.Gray;
        gemButton.shadow.effectColor = interactable ? new Color(0.2f, 0.07f, 0.4f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);
        gemButton.prefix.color = interactable ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);
        gemButton.value.color = enough ? (interactable ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f)) : ShortColor;
    }

    static void SetButton(Button button, Vector2 position, float width)
    {
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, ButtonHeight);
    }

    void OnUpgradeClicked()
    {
        onUpgrade?.Invoke();
    }

    void OnGemClicked()
    {
        if (info.IsBuilding) onSpeedUp?.Invoke();
        else onUpgradeGems?.Invoke();
    }

    void OnDestroy()
    {
        if (ResortResources.Instance != null)
            ResortResources.Instance.Changed -= Refresh;
    }

    void Update()
    {
        if (!IsOpen) return;

        if (openAnim > 0f)
        {
            openAnim = Mathf.Max(0f, openAnim - Time.unscaledDeltaTime * 7f);
            float t = 1f - openAnim;
            float back = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);   // easeOutBack
            float s = 0.85f + 0.15f * back;
            panel.localScale = Vector3.one * s;
        }

        if (languageRevision != GameText.Revision) { languageRevision = GameText.Revision; Refresh(); }
        if (info.IsBuilding) UpdateBuilding();

        if (EscapePressed()) Close();
    }

    static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    // =====================================================================
    // Stavba UI
    // =====================================================================

    void Build()
    {
        if (built) return;
        built = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // nad HUD (100) aj ikonkami budov (50)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Stmavenie hry - zároveň blokuje kliky na ostrov, HUD a START WAVE.
        root = CreateUi("Root", transform);
        Stretch((RectTransform)root.transform);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.04f, 0.02f, 0.01f, 0.55f);

        // Panel
        var panelGo = CreateUi("Panel", root.transform);
        panel = (RectTransform)panelGo.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -10f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.sprite = Sprites.Panel;
        panelImage.type = Image.Type.Sliced;
        var panelShadow = panelGo.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        panelShadow.effectDistance = new Vector2(0f, -12f);

        // Ikonka budovy prekrývajúca horný okraj
        var badgeGo = CreateUi("Badge", panel);
        Place(badgeGo, new Vector2(0f, 0f), new Vector2(118f, 118f));
        badge = badgeGo.AddComponent<Image>();
        badge.preserveAspect = true;
        badge.raycastTarget = false;
        var badgeShadow = badgeGo.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        badgeShadow.effectDistance = new Vector2(0f, -6f);

        title = CreateText("Title", panel, new Vector2(0f, -88f), new Vector2(660f, 50f), 38, TitleColor, TextAnchor.MiddleCenter);
        subtitle = CreateText("Subtitle", panel, new Vector2(0f, -124f), new Vector2(600f, 26f), 19, SubColor, TextAnchor.MiddleCenter);

        // Úroveň teraz -> po vylepšení
        levelsGroup = CreateUi("Levels", panel);
        Place(levelsGroup, new Vector2(0f, -190f), new Vector2(600f, 80f));
        currentLevel = CreateLevelBox(levelsGroup.transform, "Current", -165f, "now", Sprites.LevelBox, out _);
        nextLevel = CreateLevelBox(levelsGroup.transform, "Next", 165f, "after_upgrade", Sprites.LevelBoxNext, out nextCaption);
        var arrowGo = CreateUi("Arrow", levelsGroup.transform);
        Place(arrowGo, new Vector2(0f, -40f), new Vector2(52f, 40f));
        var arrow = arrowGo.AddComponent<Image>();
        arrow.sprite = Sprites.Arrow;
        arrow.raycastTarget = false;
        AddOutline(arrowGo, 1.5f);

        // Cena
        costLabel = CreateText("CostLabel", panel, new Vector2(0f, -262f), new Vector2(600f, 26f), 18, TitleColor, TextAnchor.MiddleCenter);
        GameTextView.Set(costLabel, () => GameText.Get("upgrade_cost"));
        // [minca] 80   [energia] 10   [hodiny] 8 min
        coinRow = CreateCostRow(panel, "CoinCost", "UI/Icons/coin", new Vector2(-200f, -312f));
        energyRow = CreateCostRow(panel, "EnergyCost", "UI/Icons/energy", new Vector2(-20f, -312f));
        timeRow = CreateCostRow(panel, "BuildTime", "UI/Icons/clock", new Vector2(160f, -312f));

        // FR-29: prebiehajúca stavba - odpočet a priebeh
        buildGroup = CreateUi("Building", panel);
        Place(buildGroup, new Vector2(0f, -250f), new Vector2(640f, 180f));
        var clockGo = CreateUi("Clock", buildGroup.transform);
        Place(clockGo, new Vector2(-125f, -45f), new Vector2(70f, 70f));
        var clock = clockGo.AddComponent<Image>();
        clock.sprite = Resources.Load<Sprite>("UI/Icons/clock");
        clock.preserveAspect = true;
        clock.raycastTarget = false;
        countdown = CreateText("Countdown", buildGroup.transform, new Vector2(35f, -45f), new Vector2(260f, 66f), 56, ValueColor, TextAnchor.MiddleCenter);

        var track = CreateUi("ProgressTrack", buildGroup.transform);
        Place(track, new Vector2(0f, -108f), new Vector2(520f, 28f));
        var trackImage = track.AddComponent<Image>();
        trackImage.sprite = Sprites.LevelBox;
        trackImage.type = Image.Type.Sliced;
        trackImage.raycastTarget = false;
        var fillGo = CreateUi("Fill", track.transform);
        progressFill = (RectTransform)fillGo.transform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0.5f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = new Vector2(4f, 4f);
        progressFill.offsetMax = new Vector2(-4f, -4f);
        var fill = fillGo.AddComponent<Image>();
        fill.sprite = Sprites.Green;
        fill.type = Image.Type.Sliced;
        fill.raycastTarget = false;

        buildGroup.SetActive(false);

        note = CreateText("Note", panel, new Vector2(0f, NoteY), new Vector2(660f, 26f), 19, ShortColor, TextAnchor.MiddleCenter);
        note.gameObject.SetActive(false);

        // MAX úroveň
        maxGroup = CreateUi("Max", panel);
        Place(maxGroup, new Vector2(0f, -190f), new Vector2(660f, 120f));
        var maxTitle = CreateText("MaxTitle", maxGroup.transform, new Vector2(0f, -40f), new Vector2(600f, 44f), 32, TitleColor, TextAnchor.MiddleCenter);
        GameTextView.Set(maxTitle, () => GameText.Get("max_level"));
        maxMessage = CreateText("MaxMessage", maxGroup.transform, new Vector2(0f, -82f), new Vector2(640f, 26f), 19, ValueColor, TextAnchor.MiddleCenter);
        maxGroup.SetActive(false);

        // Tlačidlá
        upgradeButton = CreateButton(panel, "UpgradeButton", "upgrade", Sprites.Green, new Vector2(-145f, ButtonY),
            new Color(0.03f, 0.36f, 0.08f, 1f), out upgradeImage, out upgradeText);
        upgradeText.color = Color.white;
        upgradeButton.onClick.AddListener(OnUpgradeClicked);

        closeButton = CreateButton(panel, "CloseButton", "close", Sprites.WoodButton, new Vector2(145f, ButtonY),
            new Color(0.1f, 0.06f, 0.03f, 1f), out _, out _);
        closeRect = (RectTransform)closeButton.transform;
        closeButton.onClick.AddListener(Close);

        gemButton = CreateGemButton(panel);
        gemButton.button.onClick.AddListener(OnGemClicked);
        gemButton.button.gameObject.SetActive(false);

        root.SetActive(false);
    }

    // FR-29: fialové tlačidlo [text] [drahokam] [číslo] - obsah je vycentrovaný cez HorizontalLayoutGroup.
    GemButton CreateGemButton(Transform parent)
    {
        var gb = new GemButton();
        var go = CreateUi("GemButton", parent);
        Place(go, new Vector2(0f, ButtonY), new Vector2(NarrowButton, ButtonHeight));
        gb.rect = (RectTransform)go.transform;
        gb.image = go.AddComponent<Image>();
        gb.image.sprite = Sprites.Purple;
        gb.image.type = Image.Type.Sliced;
        gb.shadow = go.AddComponent<Shadow>();
        gb.shadow.effectColor = new Color(0.2f, 0.07f, 0.4f, 1f);
        gb.shadow.effectDistance = new Vector2(0f, -6f);

        gb.button = go.AddComponent<Button>();
        gb.button.targetGraphic = gb.image;
        var colors = gb.button.colors;
        colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
        colors.pressedColor = new Color(0.8f, 0.78f, 0.72f, 1f);
        colors.disabledColor = Color.white;
        gb.button.colors = colors;

        var content = CreateUi("Content", go.transform);
        Stretch((RectTransform)content.transform);
        var layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        gb.prefix = CreateLayoutText(content.transform, "Prefix", 26);
        var icon = CreateUi("Gem", content.transform);
        var iconLayout = icon.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 40f;
        iconLayout.preferredHeight = 40f;
        var iconImage = icon.AddComponent<Image>();
        iconImage.sprite = Resources.Load<Sprite>("UI/Icons/gem");
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        gb.value = CreateLayoutText(content.transform, "Value", 28);
        return gb;
    }

    static Text CreateLayoutText(Transform parent, string name, int fontSize)
    {
        var go = CreateUi(name, parent);
        var text = go.AddComponent<Text>();
        SetupText(text, fontSize, Color.white, TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;
        AddOutline(go, 2f);
        return text;
    }

    Text CreateLevelBox(Transform parent, string name, float x, string caption, Sprite sprite, out Text captionText)
    {
        var box = CreateUi(name, parent);
        Place(box, new Vector2(x, -40f), new Vector2(180f, 74f));
        var image = box.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        var cap = CreateText("Caption", box.transform, new Vector2(0f, -18f), new Vector2(170f, 20f), 14, TitleColor, TextAnchor.MiddleCenter);
        GameTextView.Set(cap, () => GameText.Get(caption));
        captionText = cap;
        return CreateText("Value", box.transform, new Vector2(0f, -48f), new Vector2(170f, 34f), 28, ValueColor, TextAnchor.MiddleCenter);
    }

    // Cena: ikonka (minca / energia) a len samotná cena; mince a energia sú vedľa seba.
    CostRow CreateCostRow(Transform parent, string name, string iconResource, Vector2 position)
    {
        var row = new CostRow();
        row.root = CreateUi(name, parent);
        Place(row.root, position, new Vector2(200f, 54f));

        var icon = CreateUi("Icon", row.root.transform);
        var iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-34f, 0f);
        iconRect.sizeDelta = new Vector2(54f, 54f);
        var iconImage = icon.AddComponent<Image>();
        iconImage.sprite = Resources.Load<Sprite>(iconResource);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var value = CreateUi("Value", row.root.transform);
        var valueRect = (RectTransform)value.transform;
        valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.anchoredPosition = new Vector2(0f, 0f);
        valueRect.sizeDelta = new Vector2(130f, 44f);
        row.value = value.AddComponent<Text>();
        SetupText(row.value, 32, ValueColor, TextAnchor.MiddleLeft);
        row.value.fontStyle = FontStyle.Bold;
        AddOutline(value, 2f);

        return row;
    }

    Button CreateButton(Transform parent, string name, string label, Sprite sprite, Vector2 position, Color shadowColor,
                        out Image image, out Text text)
    {
        var go = CreateUi(name, parent);
        Place(go, position, new Vector2(WideButton, ButtonHeight));
        image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(0f, -6f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
        colors.pressedColor = new Color(0.8f, 0.78f, 0.72f, 1f);
        colors.disabledColor = Color.white;   // vzhľad zakázaného tlačidla rieši šedý sprite
        button.colors = colors;

        text = CreateText("Label", go.transform, Vector2.zero, Vector2.zero, 26, ValueColor, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        GameTextView.Set(text, () => GameText.Get(label));
        return button;
    }

    // ---------- pomocné ----------

    static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // Umiestnenie relatívne k hornému stredu rodiča (y ide dole do mínusu).
    static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor align)
    {
        var go = CreateUi(name, parent);
        Place(go, position, size);
        var text = go.AddComponent<Text>();
        SetupText(text, fontSize, color, align);
        text.fontStyle = FontStyle.Bold;
        AddOutline(go, fontSize >= 30 ? 2f : 1.4f);
        return text;
    }

    static void SetupText(Text text, int size, Color color, TextAnchor align)
    {
        text.font = BuiltinFont();
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    static void AddOutline(GameObject target, float distance)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(distance, -distance);
    }

    static Font BuiltinFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        return font;
    }

    static string Format(int value)
    {
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo("sk-SK"); }
        catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
        return value.ToString("N0", culture);
    }

    // =====================================================================
    // Sprity kreslené v kóde (rovnaký postup ako ResourceHud.BarSprite)
    // =====================================================================
    // internal: rovnaké sprity používa aj okno VIP hostí (VipInvitePopup, FR-31).
    internal static class Sprites
    {
        static Sprite panel, bar, barShort, levelBox, levelBoxNext, green, gray, woodButton, arrow, purple;

        public static Sprite Panel => Get(ref panel, () => RoundRect(200, 200, 46f, 7f, Wood, WoodTop, Gold, GoldLight));
        public static Sprite Bar => Get(ref bar, () => RoundRect(280, 64, 30f, 4.5f, Wood, WoodTop, Gold, GoldLight));
        public static Sprite BarShort => Get(ref barShort, () => RoundRect(280, 64, 30f, 4.5f, Wood, WoodTop,
            new Color(0.88f, 0.34f, 0.31f, 1f), new Color(1f, 0.56f, 0.53f, 1f)));
        public static Sprite LevelBox => Get(ref levelBox, () => RoundRect(120, 100, 32f, 4f,
            new Color(0f, 0f, 0f, 0.3f), new Color(0f, 0f, 0f, 0.26f),
            new Color(0.93f, 0.72f, 0.28f, 0.6f), new Color(0.93f, 0.72f, 0.28f, 0.6f)));
        public static Sprite LevelBoxNext => Get(ref levelBoxNext, () => RoundRect(120, 100, 32f, 4.5f,
            new Color(0.93f, 0.72f, 0.28f, 0.16f), new Color(0.93f, 0.72f, 0.28f, 0.22f), GoldLight, GoldLight));
        public static Sprite Green => Get(ref green, () => RoundRect(120, 80, 20f, 4f,
            new Color(0f, 0.72f, 0.14f, 1f), new Color(0.24f, 1f, 0.39f, 1f),
            new Color(0.04f, 0.48f, 0.11f, 1f), new Color(0.04f, 0.48f, 0.11f, 1f)));
        public static Sprite Gray => Get(ref gray, () => RoundRect(120, 80, 20f, 4f,
            new Color(0.36f, 0.36f, 0.36f, 1f), new Color(0.55f, 0.55f, 0.55f, 1f),
            new Color(0.26f, 0.26f, 0.26f, 1f), new Color(0.26f, 0.26f, 0.26f, 1f)));
        public static Sprite WoodButton => Get(ref woodButton, () => RoundRect(120, 80, 20f, 4f, Wood, WoodTop, Gold, GoldLight));
        // FR-29: tlačidlo platby drahokamami (fialové ako tlačidlo VIP)
        public static Sprite Purple => Get(ref purple, () => RoundRect(120, 80, 20f, 4f,
            new Color(0.4f, 0.18f, 0.75f, 1f), new Color(0.6f, 0.36f, 0.95f, 1f),
            new Color(0.25f, 0.09f, 0.5f, 1f), new Color(0.25f, 0.09f, 0.5f, 1f)));
        public static Sprite Arrow => Get(ref arrow, () => ArrowSprite());

        // Pozor: projekt má vypnutý Domain Reload (Enter Play Mode Options), statické polia prežijú
        // koniec Play módu, ale textúry sa zničia. Preto test cez Unity "== null" (nie "??"),
        // ktorý zničený sprite rozpozná a vytvorí ho znova - inak by sa okno vykreslilo bielo.
        static Sprite Get(ref Sprite cache, Func<Sprite> create)
        {
            if (cache == null) cache = create();
            return cache;
        }

        // Zaoblený obdĺžnik: výplň s prechodom zdola nahor, okraj (horná polovica svetlejšia).
        internal static Sprite RoundRect(int width, int height, float radius, float rim,
                                Color fillBottom, Color fillTop, Color rimDark, Color rimLight)
        {
            var tex = NewTexture(width, height);
            var pixels = new Color[width * height];
            var half = new Vector2(width * 0.5f, height * 0.5f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                    float dist = SdRoundBox(p, half, radius);
                    float coverage = Mathf.Clamp01(0.9f - dist);
                    if (coverage <= 0f) continue;

                    var fill = Color.Lerp(fillBottom, fillTop, Mathf.Clamp01((p.y + half.y) / height));
                    var rimColor = p.y > 0f ? rimLight : rimDark;
                    float rimT = Mathf.Clamp01(dist + rim + 0.5f);
                    var color = Color.Lerp(fill, rimColor, rimT);
                    color.a *= coverage;
                    pixels[y * width + x] = color;
                }
            }

            float border = radius + 2f;
            return Finish(tex, pixels, width, height, new Vector4(border, border, border, border));
        }

        // Šípka "→" (zlatá), aby nezáležalo na tom, či font obsahuje znak šípky.
        static Sprite ArrowSprite()
        {
            const int width = 64, height = 48;
            var tex = NewTexture(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f - height * 0.5f;
                    bool shaft = px >= 4f && px <= 40f && Mathf.Abs(py) <= 6f;
                    bool head = px >= 34f && px <= 60f && Mathf.Abs(py) <= (60f - px) * 0.92f;
                    if (shaft || head) pixels[y * width + x] = GoldLight;
                }
            }
            return Finish(tex, pixels, width, height, Vector4.zero);
        }

        static Texture2D NewTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        static Sprite Finish(Texture2D tex, Color[] pixels, int width, int height, Vector4 border)
        {
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, border);
        }

        static float SdRoundBox(Vector2 point, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half + new Vector2(radius, radius);
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) +
                   new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - radius;
        }
    }
}
