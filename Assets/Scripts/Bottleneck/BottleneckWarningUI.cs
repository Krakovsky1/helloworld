using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// FR-08 Varovanie pred úzkym miestom - zobrazenie.
// Ako hráč chcem rozpoznať preťaženú službu podľa FARBY, IKONY a TEXTOVÉHO varovania.
//
// Každá odomknutá služba má pod panelom mincí (vľavo hore) svoj riadok:
//   NESTÍHA  -> oranžová, trojuholník "!",  "Reštaurácia nestíha (7 čaká)"
//   PLNÁ     -> červená, kruh s čiarou,     "Reštaurácia je plná"
//   VOĽNÁ    -> zelená, kruh s fajkou,      "Reštaurácia je voľná (2)"   (v zátvorke počet voľných miest)
// Pri stave NESTÍHA sa navyše nad budovou na ostrove vznáša výstražná ikona.

public class BottleneckWarningUI : MonoBehaviour
{
    enum Display { Hidden, Free, Full, Overloaded }

    class Card
    {
        public GameObject root;
        public RectTransform rect;
        public Image background;
        public Image icon;
        public Text label;
        public RectTransform marker;
        public Display shown = Display.Hidden;
        public float pop;           // 1 -> 0 po zmene stavu (krátke "vyskočenie")
        public GameObject building; // budova v scéne (môže sa zmeniť po upgrade)
        public Renderer[] renderers;
        public float nextLookup;
    }

    readonly Dictionary<BottleneckService, Card> cards = new Dictionary<BottleneckService, Card>();
    BottleneckMonitor monitor;
    BuildingUpgradeManager upgradeManager; // ak je v scéne
    bool built;

    void Awake()
    {
        Build();
    }

    void OnEnable()
    {
        monitor = GetComponent<BottleneckMonitor>();
        if (monitor == null) monitor = BottleneckMonitor.Instance;
    }

    void Update()
    {
        if (monitor == null) return;
        float t = Time.unscaledTime;

        foreach (var pair in cards)
        {
            var service = pair.Key;
            var card = pair.Value;

            Display display = Evaluate(service, out string text);
            if (display != card.shown)
            {
                ApplyLook(card, display);
                card.shown = display;
                card.pop = 1f;
            }
            if (display == Display.Hidden) continue;

            if (card.label.text != text) card.label.text = text;

            // pop po zmene stavu + jemné pulzovanie pri "nestíha" (púta pozornosť, nebliká)
            card.pop = Mathf.MoveTowards(card.pop, 0f, Time.unscaledDeltaTime * 3f);
            float pulse = display == Display.Overloaded ? 1f + 0.03f * Mathf.Sin(t * 6f) : 1f;
            float s = pulse + 0.12f * card.pop;
            card.rect.localScale = new Vector3(s, s, 1f);
        }
    }

    Display Evaluate(BottleneckService service, out string text)
    {
        text = null;
        var state = monitor.GetState(service);
        if (state == BottleneckState.Locked) return Display.Hidden;

        int queue = monitor.GetQueue(service);
        int free = monitor.GetFreeSeats(service); // -1 = obsadenosť zatiaľ nikto nenahlásil

        if (state == BottleneckState.Bottleneck)
        {
            text = BottleneckServiceInfo.OverloadedText(service) + " (" + queue + " čaká)";
            return Display.Overloaded;
        }

        if (free > 0)
        {
            text = BottleneckServiceInfo.FreeText(service) + " (" + free + ")";
            return Display.Free;
        }

        if (free == 0 || queue > 0)
        {
            text = BottleneckServiceInfo.FullText(service);
            return Display.Full;
        }

        text = BottleneckServiceInfo.FreeText(service);
        return Display.Free;
    }

    static void ApplyLook(Card card, Display display)
    {
        card.root.SetActive(display != Display.Hidden);
        switch (display)
        {
            case Display.Overloaded:
                card.background.color = BottleneckIcons.BottleneckColor;
                card.icon.sprite = BottleneckIcons.Warning;
                break;
            case Display.Full:
                card.background.color = BottleneckIcons.FullColor;
                card.icon.sprite = BottleneckIcons.Full;
                break;
            case Display.Free:
                card.background.color = BottleneckIcons.FreeColor;
                card.icon.sprite = BottleneckIcons.Free;
                break;
        }
        if (display != Display.Overloaded) card.rect.localScale = Vector3.one;
    }

    void LateUpdate()
    {
        if (monitor == null) return;
        var cam = Camera.main;
        float t = Time.unscaledTime;

        foreach (var pair in cards)
        {
            var card = pair.Value;
            // ikona nad budovou len vtedy, keď služba nestíha
            bool show = card.shown == Display.Overloaded && cam != null && FindBuilding(pair.Key, card);
            if (show)
            {
                Vector3 top = BuildingTop(card) + Vector3.up * (1.2f + 0.25f * Mathf.Sin(t * 3f));
                Vector3 screen = cam.WorldToScreenPoint(top);
                show = screen.z > 0f;
                if (show) card.marker.position = new Vector3(screen.x, screen.y, 0f);
            }
            if (card.marker.gameObject.activeSelf != show) card.marker.gameObject.SetActive(show);
        }
    }

    // ---------------- hľadanie budovy v scéne ----------------

    bool FindBuilding(BottleneckService service, Card card)
    {
        if (card.building != null && card.building.activeInHierarchy && Time.unscaledTime < card.nextLookup)
            return true;
        card.nextLookup = Time.unscaledTime + 0.5f; // po upgrade sa model vymení, preto občas hľadáme znova

        GameObject found = null;

        // 1) BuildingUpgradeManager (FR-04-FP) - po upgrade vracia aktuálny model
        if (upgradeManager == null) upgradeManager = FindAnyObjectByType<BuildingUpgradeManager>();
        if (upgradeManager != null)
            found = upgradeManager.GetBuilding(BottleneckServiceInfo.UpgradeLabel(service));

        // 2) záložne podľa názvu objektu v scéne
        if (found == null)
        {
            foreach (var n in BottleneckServiceInfo.SceneNames(service))
            {
                found = GameObject.Find(n);
                if (found != null) break;
            }
        }

        if (found != card.building)
        {
            card.building = found;
            card.renderers = found != null ? found.GetComponentsInChildren<Renderer>() : null;
        }
        return card.building != null;
    }

    static Vector3 BuildingTop(Card card)
    {
        var pos = card.building.transform.position;
        if (card.renderers == null || card.renderers.Length == 0) return pos + Vector3.up * 3f;

        bool any = false;
        Bounds b = default;
        foreach (var r in card.renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? new Vector3(b.center.x, b.max.y, b.center.z) : pos + Vector3.up * 3f;
    }

    // ---------------- stavba UI kódom (netreba upravovať scénu) ----------------

    void Build()
    {
        if (built) return;
        built = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // vrstva pre ikony nad budovami (pod kartami)
        var markers = CreateUi("Markers", transform);
        Stretch(markers.GetComponent<RectTransform>());

        // stĺpec kariet pod panelom mincí (ResourceHud: vľavo hore, výška ~86)
        var column = CreateUi("WarningColumn", transform);
        var colRect = column.GetComponent<RectTransform>();
        colRect.anchorMin = colRect.anchorMax = new Vector2(0f, 1f);
        colRect.pivot = new Vector2(0f, 1f);
        colRect.anchoredPosition = new Vector2(48f, -118f);
        colRect.sizeDelta = new Vector2(700f, 400f);

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        foreach (var s in BottleneckServiceInfo.All)
            cards[s] = CreateCard(s, column.transform, markers.transform);
    }

    Card CreateCard(BottleneckService service, Transform parent, Transform markerParent)
    {
        var card = new Card();

        card.root = CreateUi("Warning_" + service, parent);
        card.rect = card.root.GetComponent<RectTransform>();
        card.rect.sizeDelta = new Vector2(560f, 58f);
        card.rect.pivot = new Vector2(0f, 0.5f);

        card.background = card.root.AddComponent<Image>();
        card.background.sprite = BottleneckIcons.Card;
        card.background.type = Image.Type.Sliced;
        card.background.raycastTarget = false;
        var shadow = card.root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -3f);

        var iconGo = CreateUi("Icon", card.root.transform);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        iconRect.sizeDelta = new Vector2(48f, 48f);
        card.icon = iconGo.AddComponent<Image>();
        card.icon.sprite = BottleneckIcons.Warning;
        card.icon.raycastTarget = false;

        var textGo = CreateUi("Text", card.root.transform);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(70f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);
        card.label = textGo.AddComponent<Text>();
        card.label.font = BuiltinFont();
        card.label.fontSize = 26;
        card.label.fontStyle = FontStyle.Bold;
        card.label.color = Color.white;
        card.label.alignment = TextAnchor.MiddleLeft;
        card.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        card.label.verticalOverflow = VerticalWrapMode.Overflow;
        card.label.raycastTarget = false;
        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var markerGo = CreateUi("Marker_" + service, markerParent);
        card.marker = markerGo.GetComponent<RectTransform>();
        card.marker.sizeDelta = new Vector2(72f, 72f);
        var markerImage = markerGo.AddComponent<Image>();
        markerImage.sprite = BottleneckIcons.Warning;
        markerImage.raycastTarget = false;
        markerGo.SetActive(false);

        card.root.SetActive(false);
        return card;
    }

    static Font BuiltinFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 26);
        return font;
    }

    static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
