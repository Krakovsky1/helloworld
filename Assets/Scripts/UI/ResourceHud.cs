using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Stály panel na ostrove: mince, drahokamy, energia a hodnota rezortu.
// FR-29: kompaktný vzhľad podľa Figmy - len ikonka a hodnota (napr. [minca] 100, [energia] 45/50),
// farby (tmavé drevo so zlatým okrajom) ostávajú. Všetky políčka majú rovnakú šírku (PillWidth).
public class ResourceHud : MonoBehaviour
{
    static ResourceHud instance;
    static bool sceneHooked;

    Text coinsValue;
    Text gemsValue;
    Text energyValue;
    Text resortValue;
    bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!sceneHooked)
        {
            SceneManager.sceneLoaded += (scene, _) => ApplyTo(scene);
            sceneHooked = true;
        }

        ApplyTo(SceneManager.GetActiveScene());
    }

    static void ApplyTo(Scene scene)
    {
        bool onIsland = scene.IsValid() &&
            (scene.name.IndexOf("Island", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             Object.FindAnyObjectByType<BuildPlot>() != null);

        if (!onIsland)
        {
            if (instance != null) instance.gameObject.SetActive(false);
            return;
        }

        if (instance == null)
        {
            var go = new GameObject("ResourceHud");
            DontDestroyOnLoad(go);
            go.AddComponent<ResortResources>();
            go.AddComponent<ResourceHud>();
            return;
        }

        instance.gameObject.SetActive(true);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    void OnEnable()
    {
        if (ResortResources.Instance != null)
            ResortResources.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (ResortResources.Instance != null)
            ResortResources.Instance.Changed -= Refresh;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Build()
    {
        if (built) return;
        built = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var row = CreateUi("ResourceRow", transform);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(48f, -22f);
        rowRect.sizeDelta = new Vector2(980f, 64f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateStat(row.transform, "Mince", IconKind.Coin, out coinsValue);
        CreateStat(row.transform, "Drahokamy", IconKind.Gem, out gemsValue);
        CreateStat(row.transform, "Energia", IconKind.Energy, out energyValue);
        CreateStat(row.transform, "Hodnota rezortu", IconKind.Resort, out resortValue);
    }

    void Refresh()
    {
        var data = ResortResources.Instance;
        if (data == null || coinsValue == null) return;

        SetValue(coinsValue, Format(data.Coins));
        SetValue(gemsValue, Format(data.Gems));
        SetValue(energyValue, Format(data.Energy) + "/" + Format(data.MaxEnergy));
        SetValue(resortValue, Format(data.ResortValue));
    }

    static void SetValue(Text label, string value)
    {
        label.text = value;
    }

    const float PillWidth = 196f;    // rovnaká šírka pre všetky políčka
    const float PillHeight = 64f;
    const float BarHeight = 50f;
    const float IconSize = 66f;
    const float ValueLeft = 70f;     // kde začína číslo (za ikonkou)
    const float ValueRight = 16f;    // okraj za číslom

    static string Format(int value)
    {
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo("sk-SK"); }
        catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
        return value.ToString("N0", culture);
    }

    enum IconKind { Coin, Gem, Energy, Resort }

    // Kompaktné políčko: [ikonka] hodnota
    static void CreateStat(Transform parent, string title, IconKind kind, out Text valueLabel)
    {
        var pill = CreateUi(title, parent);
        var layoutItem = pill.AddComponent<LayoutElement>();
        layoutItem.preferredWidth = PillWidth;
        layoutItem.preferredHeight = PillHeight;

        var bar = CreateUi("Bar", pill.transform);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.offsetMin = new Vector2(22f, -BarHeight * 0.5f);   // pás začína pod ikonkou
        barRect.offsetMax = new Vector2(0f, BarHeight * 0.5f);
        var barImage = bar.AddComponent<Image>();
        barImage.sprite = BarSprite();
        barImage.type = Image.Type.Sliced;
        barImage.raycastTarget = false;

        var icon = CreateUi("Icon", pill.transform);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-6f, 1f);
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        var iconImage = icon.AddComponent<Image>();
        iconImage.sprite = IconSprite(kind);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var valueGo = CreateUi("Value", pill.transform);
        var valueRect = valueGo.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.offsetMin = new Vector2(ValueLeft, 0f);
        valueRect.offsetMax = new Vector2(-ValueRight, 1f);
        valueLabel = valueGo.AddComponent<Text>();
        SetupText(valueLabel, 27, new Color(1f, 0.97f, 0.88f, 1f));
        valueLabel.fontStyle = FontStyle.Bold;
        CartoonOutline(valueGo);
    }

    static void CartoonOutline(GameObject target)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.08f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
    }

    static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static Sprite barSprite;

    static Sprite BarSprite()
    {
        if (barSprite != null) return barSprite;

        const int width = 280;
        const int height = 64;
        const float radius = 30f;
        var tex = NewTexture(width, height);
        var pixels = new Color[width * height];
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var gold = new Color(0.93f, 0.72f, 0.28f, 1f);
        var goldLight = new Color(1f, 0.9f, 0.55f, 1f);
        var wood = new Color(0.16f, 0.1f, 0.06f, 0.96f);
        var woodTop = new Color(0.28f, 0.18f, 0.1f, 0.96f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                float dist = SdRoundBox(p, half, radius);
                float coverage = Mathf.Clamp01(0.9f - dist);
                if (coverage <= 0f) continue;

                var fill = Color.Lerp(wood, woodTop, Mathf.Clamp01((p.y + half.y) / height));
                var rim = p.y > 0f ? goldLight : gold;
                var color = dist > -4.5f ? rim : fill;
                if (dist < -4.5f && p.y > 8f)
                    color = Color.Lerp(color, goldLight, 0.08f);
                color.a *= coverage;
                pixels[y * width + x] = color;
            }
        }

        barSprite = Finish(tex, pixels, width, height, new Vector4(radius, radius, radius, radius));
        return barSprite;
    }

    static Texture2D NewTexture(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static Sprite Finish(Texture2D tex, Color[] pixels, int width, int height, Vector4 border = default)
    {
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static float SdRoundBox(Vector2 point, Vector2 half, float radius)
    {
        var q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half + new Vector2(radius, radius);
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - radius;
    }

    static Sprite IconSprite(IconKind kind)
    {
        string name = kind == IconKind.Coin ? "coin" : kind == IconKind.Gem ? "gem" :
                      kind == IconKind.Energy ? "energy" : "value";
        return Resources.Load<Sprite>("UI/Icons/" + name);
    }

    static void SetupText(Text text, int size, Color color)
    {
        text.font = BuiltinFont();
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    static Font BuiltinFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        return font;
    }

    static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
