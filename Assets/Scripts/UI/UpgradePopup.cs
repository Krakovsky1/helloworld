using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// FR-22: Okno ponuky vylepšenia budovy.
// Otvorí sa po kliknutí na ikonku H / R / B nad budovou (BuildingUpgradeManager).
// Ukáže úroveň pred a po vylepšení, cenu v minciach a energii a tlačidlá VYLEPŠIŤ / ZAVRIEŤ.
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
        public bool IsMax => level >= maxLevel;
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
    const float PanelWidth = 840f;
    const float PanelHeight = 620f;
    const float PanelHeightMax = 470f;

    Info info;
    Action onUpgrade;
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

    class CostRow
    {
        public GameObject root;
        public Text value;
    }

    public bool IsOpen => root != null && root.activeSelf;

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
        Build();
        info = data;
        onUpgrade = upgradeAction;

        badge.sprite = data.icon;
        badge.enabled = data.icon != null;

        bool isMax = data.IsMax;
        title.text = isMax ? data.nameNominative : "Vylepšiť " + data.nameAccusative;
        subtitle.text = "Úroveň " + data.level + " z " + data.maxLevel;

        levelsGroup.SetActive(!isMax);
        costLabel.gameObject.SetActive(!isMax);
        coinRow.root.SetActive(!isMax);
        energyRow.root.SetActive(!isMax);
        upgradeButton.gameObject.SetActive(!isMax);
        maxGroup.SetActive(isMax);

        if (isMax)
        {
            maxMessage.text = data.nameNominative + " je na najvyššej úrovni.";
            note.gameObject.SetActive(false);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeightMax);
            closeRect.anchoredPosition = new Vector2(0f, -385f);
        }
        else
        {
            currentLevel.text = "Level " + data.level;
            nextLevel.text = "Level " + (data.level + 1);
            coinRow.value.text = Format(data.coinCost);
            energyRow.value.text = Format(data.energyCost);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            closeRect.anchoredPosition = new Vector2(165f, -550f);
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
        if (root != null) root.SetActive(false);
    }

    // Prepočíta, či je dosť mincí a energie (volá sa aj pri každej zmene zdrojov).
    public void Refresh()
    {
        if (!IsOpen || info.IsMax) return;

        var res = ResortResources.Instance;
        int coins = res != null ? res.Coins : 0;
        int energy = res != null ? res.Energy : 0;
        bool coinsOk = res != null && coins >= info.coinCost;
        bool energyOk = res != null && energy >= info.energyCost;

        coinRow.value.color = coinsOk ? ValueColor : ShortColor;
        energyRow.value.color = energyOk ? ValueColor : ShortColor;

        bool canUpgrade = coinsOk && energyOk;
        upgradeButton.interactable = canUpgrade;
        upgradeImage.sprite = canUpgrade ? Sprites.Green : Sprites.Gray;
        upgradeText.color = canUpgrade ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);

        string message = "";
        if (res == null)
            message = "Zdroje rezortu nie sú dostupné";
        else if (!coinsOk && !energyOk)
            message = "Nedostatok mincí aj energie";
        else if (!coinsOk)
            message = "Nedostatok mincí – chýba ti " + Format(info.coinCost - coins);
        else if (!energyOk)
            message = "Nedostatok energie – chýba ti " + Format(info.energyCost - energy);

        note.text = message;
        note.gameObject.SetActive(message.Length > 0);
    }

    void OnUpgradeClicked()
    {
        onUpgrade?.Invoke();
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
        Place(badgeGo, new Vector2(0f, 0f), new Vector2(146f, 146f));
        badge = badgeGo.AddComponent<Image>();
        badge.preserveAspect = true;
        badge.raycastTarget = false;
        var badgeShadow = badgeGo.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        badgeShadow.effectDistance = new Vector2(0f, -6f);

        title = CreateText("Title", panel, new Vector2(0f, -118f), new Vector2(760f, 60f), 44, TitleColor, TextAnchor.MiddleCenter);
        subtitle = CreateText("Subtitle", panel, new Vector2(0f, -164f), new Vector2(600f, 30f), 21, SubColor, TextAnchor.MiddleCenter);

        // Úroveň teraz -> po vylepšení
        levelsGroup = CreateUi("Levels", panel);
        Place(levelsGroup, new Vector2(0f, -250f), new Vector2(700f, 100f));
        currentLevel = CreateLevelBox(levelsGroup.transform, "Current", -215f, "TERAZ", Sprites.LevelBox);
        nextLevel = CreateLevelBox(levelsGroup.transform, "Next", 215f, "PO VYLEPŠENÍ", Sprites.LevelBoxNext);
        var arrowGo = CreateUi("Arrow", levelsGroup.transform);
        Place(arrowGo, new Vector2(0f, -50f), new Vector2(64f, 48f));
        var arrow = arrowGo.AddComponent<Image>();
        arrow.sprite = Sprites.Arrow;
        arrow.raycastTarget = false;
        AddOutline(arrowGo, 1.5f);

        // Cena
        costLabel = CreateText("CostLabel", panel, new Vector2(0f, -340f), new Vector2(600f, 30f), 21, TitleColor, TextAnchor.MiddleCenter);
        costLabel.text = "CENA VYLEPŠENIA";
        coinRow = CreateCostRow(panel, "CoinCost", "UI/Icons/coin", new Vector2(-90f, -405f));
        energyRow = CreateCostRow(panel, "EnergyCost", "UI/Icons/energy", new Vector2(110f, -405f));

        note = CreateText("Note", panel, new Vector2(0f, -468f), new Vector2(760f, 30f), 21, ShortColor, TextAnchor.MiddleCenter);
        note.gameObject.SetActive(false);

        // MAX úroveň
        maxGroup = CreateUi("Max", panel);
        Place(maxGroup, new Vector2(0f, -250f), new Vector2(760f, 140f));
        var maxTitle = CreateText("MaxTitle", maxGroup.transform, new Vector2(0f, -50f), new Vector2(600f, 50f), 36, TitleColor, TextAnchor.MiddleCenter);
        maxTitle.text = "MAX Level";
        maxMessage = CreateText("MaxMessage", maxGroup.transform, new Vector2(0f, -100f), new Vector2(740f, 30f), 21, ValueColor, TextAnchor.MiddleCenter);
        maxGroup.SetActive(false);

        // Tlačidlá
        upgradeButton = CreateButton(panel, "UpgradeButton", "VYLEPŠIŤ", Sprites.Green, new Vector2(-165f, -550f),
            new Color(0.03f, 0.36f, 0.08f, 1f), out upgradeImage, out upgradeText);
        upgradeText.color = Color.white;
        upgradeButton.onClick.AddListener(OnUpgradeClicked);

        closeButton = CreateButton(panel, "CloseButton", "ZAVRIEŤ", Sprites.WoodButton, new Vector2(165f, -550f),
            new Color(0.1f, 0.06f, 0.03f, 1f), out _, out _);
        closeRect = (RectTransform)closeButton.transform;
        closeButton.onClick.AddListener(Close);

        root.SetActive(false);
    }

    Text CreateLevelBox(Transform parent, string name, float x, string caption, Sprite sprite)
    {
        var box = CreateUi(name, parent);
        Place(box, new Vector2(x, -50f), new Vector2(210f, 88f));
        var image = box.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        var cap = CreateText("Caption", box.transform, new Vector2(0f, -22f), new Vector2(200f, 22f), 16, TitleColor, TextAnchor.MiddleCenter);
        cap.text = caption;
        return CreateText("Value", box.transform, new Vector2(0f, -56f), new Vector2(200f, 40f), 34, ValueColor, TextAnchor.MiddleCenter);
    }

    // Cena: ikonka (minca / energia) a len samotná cena; mince a energia sú vedľa seba.
    CostRow CreateCostRow(Transform parent, string name, string iconResource, Vector2 position)
    {
        var row = new CostRow();
        row.root = CreateUi(name, parent);
        Place(row.root, position, new Vector2(240f, 66f));

        var icon = CreateUi("Icon", row.root.transform);
        var iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-42f, 0f);
        iconRect.sizeDelta = new Vector2(66f, 66f);
        var iconImage = icon.AddComponent<Image>();
        iconImage.sprite = Resources.Load<Sprite>(iconResource);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var value = CreateUi("Value", row.root.transform);
        var valueRect = (RectTransform)value.transform;
        valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.anchoredPosition = new Vector2(0f, 0f);
        valueRect.sizeDelta = new Vector2(160f, 50f);
        row.value = value.AddComponent<Text>();
        SetupText(row.value, 38, ValueColor, TextAnchor.MiddleLeft);
        row.value.fontStyle = FontStyle.Bold;
        AddOutline(value, 2f);

        return row;
    }

    Button CreateButton(Transform parent, string name, string label, Sprite sprite, Vector2 position, Color shadowColor,
                        out Image image, out Text text)
    {
        var go = CreateUi(name, parent);
        Place(go, position, new Vector2(300f, 76f));
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

        text = CreateText("Label", go.transform, Vector2.zero, Vector2.zero, 28, ValueColor, TextAnchor.MiddleCenter);
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
    static class Sprites
    {
        static Sprite panel, bar, barShort, levelBox, levelBoxNext, green, gray, woodButton, arrow;

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
        static Sprite RoundRect(int width, int height, float radius, float rim,
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
