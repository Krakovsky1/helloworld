using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// FR-31: Okno "VIP hostia" - otvorí ho tlačidlo VIP v HUD (VipHudButton).
// Ukáže 3 náhodných VIP hostí z ponuky (VipInviteManager). Pri každom:
//   portrét, meno, profesia, vzácnosť, cena pozvania (mince), potrebná hodnota rezortu,
//   potrebné úrovne budov, násobiteľ odmien a tlačidlo POZVAŤ.
// Nesplnená podmienka je červená, splnená zelená/biela. Dizajn je rovnaký ako okno vylepšenia
// budovy (UpgradePopup) a panel Mince / Energia: tmavé drevo so zlatým okrajom.
public class VipInvitePopup : MonoBehaviour
{
    // ---------- farby (rovnaké ako ResourceHud / UpgradePopup) ----------
    static readonly Color Wood = new Color(0.16f, 0.1f, 0.06f, 0.98f);
    static readonly Color WoodTop = new Color(0.28f, 0.18f, 0.1f, 0.98f);
    static readonly Color Gold = new Color(0.93f, 0.72f, 0.28f, 1f);
    static readonly Color GoldLight = new Color(1f, 0.9f, 0.55f, 1f);
    static readonly Color TitleColor = new Color(0.98f, 0.86f, 0.45f, 1f);
    static readonly Color ValueColor = new Color(1f, 0.97f, 0.88f, 1f);
    static readonly Color SubColor = new Color(0.91f, 0.84f, 0.69f, 1f);
    static readonly Color OutlineColor = new Color(0.16f, 0.08f, 0.03f, 0.95f);
    static readonly Color ShortColor = new Color(1f, 0.42f, 0.38f, 1f);
    static readonly Color OkColor = new Color(0.45f, 0.95f, 0.5f, 1f);

    // ---------- rozmery (referenčné rozlíšenie 1920 x 1080) ----------
    const float PanelWidth = 1240f;
    const float PanelHeight = 910f;
    const float CardWidth = 370f;
    const float CardHeight = 600f;
    const float CardGap = 22f;
    const float CardsTop = -182f;
    const int MaxChips = 5;

    VipInviteManager manager;
    bool built;
    float openAnim;
    float refreshTimer;
    float fitScale = 1f;

    GameObject root;
    RectTransform panel;
    Text subtitle;
    readonly List<Card> cards = new List<Card>();

    class Card
    {
        public VipPersonality vip;
        public GameObject root;
        public Image frame;
        public Image portrait;
        public Text name;
        public Text profession;
        public Image rarityPill;
        public Text rarityText;
        public Text coinValue;
        public Text valueValue;
        public readonly List<Chip> chips = new List<Chip>();
        public Text noBuildings;
        public Text reward;
        public Button button;
        public Image buttonImage;
        public Text buttonText;
        public Shadow buttonShadow;
    }

    class Chip
    {
        public GameObject root;
        public Image background;
        public Image icon;
        public Text label;
    }

    public bool IsOpen => root != null && root.activeSelf;

    public static VipInvitePopup Create(Transform parent, VipInviteManager manager)
    {
        var go = new GameObject("VipInvitePopup");
        go.transform.SetParent(parent, false);
        var popup = go.AddComponent<VipInvitePopup>();
        popup.manager = manager;
        popup.Build();
        return popup;
    }

    // ------------------------------------------------------------------
    // Otvorenie / zatvorenie
    // ------------------------------------------------------------------

    public void Toggle()
    {
        if (IsOpen) Close();
        else Show();
    }

    public void Show()
    {
        Build();
        root.SetActive(true);
        openAnim = 1f;
        fitScale = FitScale();
        panel.localScale = Vector3.one * 0.85f * fitScale;

        Subscribe(true);
        Refresh();
    }

    public void Close()
    {
        Subscribe(false);
        if (root != null) root.SetActive(false);
    }

    void Subscribe(bool on)
    {
        if (ResortResources.Instance != null)
        {
            ResortResources.Instance.Changed -= Refresh;
            if (on) ResortResources.Instance.Changed += Refresh;
        }

        if (manager != null)
        {
            manager.Changed -= Refresh;
            if (on) manager.Changed += Refresh;
        }
    }

    void OnDestroy()
    {
        Subscribe(false);
    }

    void Update()
    {
        if (!IsOpen) return;

        if (openAnim > 0f)
        {
            openAnim = Mathf.Max(0f, openAnim - Time.unscaledDeltaTime * 7f);
            float t = 1f - openAnim;
            float back = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);   // easeOutBack
            panel.localScale = Vector3.one * (0.85f + 0.15f * back) * fitScale;
        }

        // Úrovne budov nemajú vlastnú udalosť - podmienky sa preto prepočítajú aj priebežne.
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) Refresh();

        if (EscapePressed()) Close();
    }

    // Na veľmi širokých displejoch (telefóny 19.5:9) by bolo okno vyššie ako obrazovka - zmenšíme ho.
    float FitScale()
    {
        var area = ((RectTransform)transform).rect;
        float needHeight = PanelHeight + 150f;   // + hviezda nad panelom a okraje
        float needWidth = PanelWidth + 60f;
        if (area.height <= 0f || area.width <= 0f) return 1f;
        return Mathf.Min(1f, area.height / needHeight, area.width / needWidth);
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

    // ------------------------------------------------------------------
    // Obnova obsahu
    // ------------------------------------------------------------------

    public void Refresh()
    {
        refreshTimer = 0.5f;
        if (!IsOpen || manager == null) return;

        var offer = manager.Offer;
        var res = ResortResources.Instance;
        int coins = res != null ? res.Coins : 0;
        int value = res != null ? res.ResortValue : 0;

        var invited = manager.InvitedVip;
        if (invited != null)
        {
            subtitle.text = invited.displayName + " príde s vlnou " + manager.InvitedForWave +
                            ". Nová ponuka bude po skončení tejto vlny.";
            subtitle.color = TitleColor;
        }
        else
        {
            subtitle.text = "Pozvi jedného VIP hosťa – príde spolu s ďalšou vlnou. Ponuka sa obmení po vlne.";
            subtitle.color = SubColor;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var vip = i < offer.Count ? offer[i] : null;
            card.root.SetActive(vip != null);
            if (vip == null) continue;
            FillCard(card, vip, coins, value);
        }

        LayoutCards(Mathf.Min(offer.Count, cards.Count));
    }

    void FillCard(Card card, VipPersonality vip, int coins, int value)
    {
        card.vip = vip;
        bool isInvited = manager.InvitedVip == vip;

        card.portrait.sprite = vip.portrait != null ? vip.portrait : Resources.Load<Sprite>("UI/Icons/vip_star");
        card.name.text = vip.displayName;
        card.profession.text = vip.profession;

        var rarityColor = VipPersonality.RarityColor(vip.rarity);
        card.rarityPill.color = rarityColor;
        card.rarityText.text = VipPersonality.RarityName(vip.rarity).ToUpperInvariant();

        card.frame.sprite = isInvited ? Sprites.CardInvited : Sprites.Card;

        // Cena a hodnota rezortu (pri pozvanom VIP je už zaplatené - nič nie je červené)
        card.coinValue.text = Format(vip.inviteCost);
        card.coinValue.color = isInvited || coins >= vip.inviteCost ? ValueColor : ShortColor;
        card.valueValue.text = Format(vip.requiredResortValue);
        card.valueValue.color = isInvited || value >= vip.requiredResortValue ? ValueColor : ShortColor;

        // Budovy
        var reqs = vip.buildingRequirements ?? new List<VipBuildingRequirement>();
        int shown = 0;
        for (int i = 0; i < card.chips.Count; i++)
        {
            var chip = card.chips[i];
            var req = i < reqs.Count ? reqs[i] : null;
            chip.root.SetActive(req != null);
            if (req == null) continue;
            shown++;

            int level = VipRequirements.GetLevel(req.building);
            int max = VipRequirements.GetMaxLevel(req.building);
            int needed = req.requireMaxLevel ? Mathf.Max(1, max) : req.minLevel;
            bool ok = isInvited || (level > 0 && level >= needed);

            chip.icon.sprite = Resources.Load<Sprite>(BuildingIcon(req.building));
            chip.label.text = req.requireMaxLevel ? "MAX" : "Lv " + req.minLevel;
            chip.label.color = ok ? OkColor : ShortColor;
            chip.background.sprite = ok ? Sprites.ChipOk : Sprites.ChipMissing;
        }

        // Rozloženie čipov na stred karty
        float spacing = 68f;
        float start = -(shown - 1) * spacing * 0.5f;
        int index = 0;
        foreach (var chip in card.chips)
        {
            if (!chip.root.activeSelf) continue;
            var rect = (RectTransform)chip.root.transform;
            rect.anchoredPosition = new Vector2(start + index * spacing, rect.anchoredPosition.y);
            index++;
        }
        card.noBuildings.gameObject.SetActive(shown == 0);

        card.reward.text = "Násobiteľ odmien  ×" + vip.rewardMultiplier.ToString("0.0#", CultureInfo.InvariantCulture);

        // Tlačidlo
        bool canInvite = manager.CanInvite(vip, out _);
        card.button.interactable = canInvite;
        if (isInvited)
        {
            card.buttonImage.sprite = Sprites.Invited;
            card.buttonText.text = "POZVANÝ";
            card.buttonText.color = ValueColor;
            card.buttonShadow.effectColor = new Color(0.4f, 0.25f, 0.04f, 1f);
        }
        else
        {
            card.buttonImage.sprite = canInvite ? UpgradePopup.Sprites.Green : UpgradePopup.Sprites.Gray;
            card.buttonText.text = "POZVAŤ";
            card.buttonText.color = canInvite ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);
            card.buttonShadow.effectColor = canInvite ? new Color(0.03f, 0.36f, 0.08f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);
        }
    }

    // Karty vycentrované vedľa seba (ak by ich bolo menej ako 3).
    void LayoutCards(int count)
    {
        float start = -(count - 1) * (CardWidth + CardGap) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var rect = (RectTransform)cards[i].root.transform;
            rect.anchoredPosition = new Vector2(start + i * (CardWidth + CardGap), CardsTop - CardHeight * 0.5f);
        }
    }

    // Pozývanie zatiaľ nie je zapnuté - tlačidlo POZVAŤ len ukazuje, či sú podmienky splnené.
    // Neskôr stačí odkomentovať riadok s TryInvite (zaplatí mince a uloží pozvaného VIP).
    void OnInviteClicked(Card card)
    {
        if (manager == null || card.vip == null) return;
        Debug.Log("FR-31 | Klik na POZVAŤ: " + card.vip.displayName + " (pozývanie zatiaľ nie je zapnuté)");
        // manager.TryInvite(card.vip);
        Refresh();
    }

    static string BuildingIcon(VipBuilding building)
    {
        switch (building)
        {
            case VipBuilding.Restaurant: return "UI/Icons/building_restaurant";
            case VipBuilding.Bar: return "UI/Icons/building_bar";
            case VipBuilding.Pool: return "UI/Icons/building_pool";
            case VipBuilding.Wellness: return "UI/Icons/building_wellness";
            default: return "UI/Icons/building_hotel";
        }
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

        var panelGo = CreateUi("Panel", root.transform);
        panel = (RectTransform)panelGo.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -20f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.sprite = UpgradePopup.Sprites.Panel;
        panelImage.type = Image.Type.Sliced;
        var panelShadow = panelGo.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        panelShadow.effectDistance = new Vector2(0f, -12f);

        // Hviezda VIP prekrývajúca horný okraj (ako ikonka budovy v okne vylepšenia)
        var badgeGo = CreateUi("Badge", panel);
        Place(badgeGo, new Vector2(0f, 4f), new Vector2(132f, 132f));
        var badge = badgeGo.AddComponent<Image>();
        badge.sprite = Resources.Load<Sprite>("UI/Icons/vip_star");
        badge.preserveAspect = true;
        badge.raycastTarget = false;
        var badgeShadow = badgeGo.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        badgeShadow.effectDistance = new Vector2(0f, -6f);

        var title = CreateText("Title", panel, new Vector2(0f, -102f), new Vector2(900f, 56f), 44, TitleColor, TextAnchor.MiddleCenter);
        title.text = "VIP HOSTIA";
        subtitle = CreateText("Subtitle", panel, new Vector2(0f, -146f), new Vector2(1160f, 30f), 21, SubColor, TextAnchor.MiddleCenter);

        for (int i = 0; i < 3; i++)
        {
            var card = CreateCard(panel, i);
            cards.Add(card);
        }
        LayoutCards(cards.Count);

        var closeButton = CreateButton(panel, "CloseButton", "ZAVRIEŤ", UpgradePopup.Sprites.WoodButton,
            new Vector2(0f, -PanelHeight + 70f), new Vector2(300f, 76f), 28, new Color(0.1f, 0.06f, 0.03f, 1f), out _, out _);
        closeButton.onClick.AddListener(Close);

        root.SetActive(false);
    }

    Card CreateCard(Transform parent, int index)
    {
        var card = new Card();
        card.root = CreateUi("Card" + (index + 1), parent);
        var rect = (RectTransform)card.root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.frame = card.root.AddComponent<Image>();
        card.frame.sprite = Sprites.Card;
        card.frame.type = Image.Type.Sliced;
        card.frame.raycastTarget = false;
        var t = card.root.transform;

        // Portrét (kruhový odznak s okrajom podľa vzácnosti)
        var portraitGo = CreateUi("Portrait", t);
        Place(portraitGo, new Vector2(0f, -86f), new Vector2(136f, 136f));
        card.portrait = portraitGo.AddComponent<Image>();
        card.portrait.preserveAspect = true;
        card.portrait.raycastTarget = false;
        var portraitShadow = portraitGo.AddComponent<Shadow>();
        portraitShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        portraitShadow.effectDistance = new Vector2(0f, -5f);

        card.name = CreateText("Name", t, new Vector2(0f, -176f), new Vector2(350f, 36f), 28, ValueColor, TextAnchor.MiddleCenter);
        card.profession = CreateText("Profession", t, new Vector2(0f, -207f), new Vector2(350f, 26f), 18, SubColor, TextAnchor.MiddleCenter);
        card.profession.fontStyle = FontStyle.Normal;

        // Vzácnosť
        var pillGo = CreateUi("Rarity", t);
        Place(pillGo, new Vector2(0f, -240f), new Vector2(150f, 30f));
        card.rarityPill = pillGo.AddComponent<Image>();
        card.rarityPill.sprite = Sprites.Pill;
        card.rarityPill.type = Image.Type.Sliced;
        card.rarityPill.raycastTarget = false;
        card.rarityText = CreateText("Label", pillGo.transform, Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter);
        Stretch(card.rarityText.rectTransform);

        // Podmienky: cena + hodnota rezortu
        var conditions = CreateText("ConditionsLabel", t, new Vector2(0f, -280f), new Vector2(350f, 24f), 16, TitleColor, TextAnchor.MiddleCenter);
        conditions.text = "PODMIENKY";
        card.coinValue = CreateStatBox(t, "Cost", "CENA POZVANIA", "UI/Icons/coin", new Vector2(-88f, -330f));
        card.valueValue = CreateStatBox(t, "ResortValue", "HODNOTA REZORTU", "UI/Icons/value", new Vector2(88f, -330f));

        // Podmienky: budovy
        var buildings = CreateText("BuildingsLabel", t, new Vector2(0f, -388f), new Vector2(350f, 22f), 15, TitleColor, TextAnchor.MiddleCenter);
        buildings.text = "BUDOVY";
        for (int i = 0; i < MaxChips; i++)
            card.chips.Add(CreateChip(t, i));
        card.noBuildings = CreateText("NoBuildings", t, new Vector2(0f, -436f), new Vector2(350f, 24f), 18, SubColor, TextAnchor.MiddleCenter);
        card.noBuildings.text = "Bez požiadaviek";
        card.noBuildings.gameObject.SetActive(false);

        card.reward = CreateText("Reward", t, new Vector2(0f, -505f), new Vector2(350f, 26f), 19, TitleColor, TextAnchor.MiddleCenter);

        card.button = CreateButton(t, "InviteButton", "POZVAŤ", UpgradePopup.Sprites.Green, new Vector2(0f, -555f),
            new Vector2(300f, 64f), 26, new Color(0.03f, 0.36f, 0.08f, 1f), out card.buttonImage, out card.buttonText);
        card.buttonText.color = Color.white;
        card.buttonShadow = card.button.GetComponent<Shadow>();
        card.button.onClick.AddListener(() => OnInviteClicked(card));

        return card;
    }

    // Políčko s nadpisom, ikonkou a číslom (cena / hodnota rezortu). Vráti text s číslom.
    Text CreateStatBox(Transform parent, string name, string caption, string iconResource, Vector2 position)
    {
        var box = CreateUi(name, parent);
        Place(box, position, new Vector2(168f, 74f));
        var image = box.AddComponent<Image>();
        image.sprite = UpgradePopup.Sprites.LevelBox;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        var cap = CreateText("Caption", box.transform, new Vector2(0f, -16f), new Vector2(160f, 18f), 13, TitleColor, TextAnchor.MiddleCenter);
        cap.text = caption;

        var icon = CreateUi("Icon", box.transform);
        var iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-6f, -48f);
        iconRect.sizeDelta = new Vector2(38f, 38f);
        var iconImage = icon.AddComponent<Image>();
        iconImage.sprite = Resources.Load<Sprite>(iconResource);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var value = CreateText("Value", box.transform, Vector2.zero, new Vector2(90f, 34f), 26, ValueColor, TextAnchor.MiddleLeft);
        var valueRect = value.rectTransform;
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.anchoredPosition = new Vector2(0f, -48f);
        return value;
    }

    // Čip budovy: ikonka (H / R / B / P / W) a potrebná úroveň pod ňou.
    Chip CreateChip(Transform parent, int index)
    {
        var chip = new Chip();
        chip.root = CreateUi("Building" + (index + 1), parent);
        Place(chip.root, new Vector2(0f, -438f), new Vector2(62f, 80f));
        chip.background = chip.root.AddComponent<Image>();
        chip.background.sprite = Sprites.ChipOk;
        chip.background.type = Image.Type.Sliced;
        chip.background.raycastTarget = false;

        var icon = CreateUi("Icon", chip.root.transform);
        Place(icon, new Vector2(0f, -27f), new Vector2(44f, 44f));
        chip.icon = icon.AddComponent<Image>();
        chip.icon.preserveAspect = true;
        chip.icon.raycastTarget = false;

        chip.label = CreateText("Level", chip.root.transform, new Vector2(0f, -63f), new Vector2(60f, 20f), 15, OkColor, TextAnchor.MiddleCenter);
        return chip;
    }

    Button CreateButton(Transform parent, string name, string label, Sprite sprite, Vector2 position, Vector2 size,
                        int fontSize, Color shadowColor, out Image image, out Text text)
    {
        var go = CreateUi(name, parent);
        Place(go, position, size);
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

        text = CreateText("Label", go.transform, Vector2.zero, Vector2.zero, fontSize, ValueColor, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.text = label;
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
        text.font = BuiltinFont();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.fontStyle = FontStyle.Bold;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        float distance = fontSize >= 30 ? 2f : 1.4f;
        outline.effectDistance = new Vector2(distance, -distance);
        return text;
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
    // Sprity okna VIP (kreslené v kóde rovnako ako v UpgradePopup)
    // =====================================================================
    static class Sprites
    {
        static Sprite card, cardInvited, pill, chipOk, chipMissing, invited;

        // Karta hosťa: tmavšia výplň so zlatým okrajom (ako políčka úrovní v okne vylepšenia).
        public static Sprite Card => Get(ref card, () => UpgradePopup.Sprites.RoundRect(160, 160, 34f, 3.5f,
            new Color(0f, 0f, 0f, 0.28f), new Color(0.12f, 0.07f, 0.03f, 0.3f),
            new Color(0.93f, 0.72f, 0.28f, 0.55f), new Color(0.93f, 0.72f, 0.28f, 0.7f)));
        // Pozvaný hosť: svetlejšia karta s jasne zlatým okrajom.
        public static Sprite CardInvited => Get(ref cardInvited, () => UpgradePopup.Sprites.RoundRect(160, 160, 34f, 5f,
            new Color(0.93f, 0.72f, 0.28f, 0.14f), new Color(0.93f, 0.72f, 0.28f, 0.22f), GoldLight, GoldLight));
        // Biely štítok - farbí sa farbou vzácnosti (Image.color).
        public static Sprite Pill => Get(ref pill, () => UpgradePopup.Sprites.RoundRect(80, 40, 15f, 2.5f,
            new Color(0.82f, 0.82f, 0.82f, 1f), Color.white,
            new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.55f)));
        public static Sprite ChipOk => Get(ref chipOk, () => UpgradePopup.Sprites.RoundRect(60, 80, 18f, 3f,
            new Color(0.1f, 0.35f, 0.12f, 0.45f), new Color(0.1f, 0.35f, 0.12f, 0.3f),
            new Color(0.45f, 0.95f, 0.5f, 0.85f), new Color(0.45f, 0.95f, 0.5f, 0.85f)));
        public static Sprite ChipMissing => Get(ref chipMissing, () => UpgradePopup.Sprites.RoundRect(60, 80, 18f, 3f,
            new Color(0.4f, 0.08f, 0.06f, 0.45f), new Color(0.4f, 0.08f, 0.06f, 0.3f),
            new Color(1f, 0.42f, 0.38f, 0.9f), new Color(1f, 0.42f, 0.38f, 0.9f)));
        // Tlačidlo "POZVANÝ": zlaté.
        public static Sprite Invited => Get(ref invited, () => UpgradePopup.Sprites.RoundRect(120, 80, 20f, 4f,
            new Color(0.78f, 0.53f, 0.12f, 1f), new Color(0.98f, 0.78f, 0.3f, 1f),
            new Color(0.55f, 0.35f, 0.06f, 1f), new Color(0.55f, 0.35f, 0.06f, 1f)));

        // Vypnutý Domain Reload: zničený sprite treba spoznať cez Unity "== null" (nie "??").
        static Sprite Get(ref Sprite cache, System.Func<Sprite> create)
        {
            if (cache == null) cache = create();
            return cache;
        }
    }
}
