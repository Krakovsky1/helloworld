using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// FR-09: krátky komentár hosťa, keď niektorá služba nestíha.
// Text je veta hosťa. Systémové varovanie z FR-08 ostáva samostatne.
public class GuestFeedback : MonoBehaviour
{
    static GuestFeedback instance;
    static bool sceneHooked;

    GuestWaveManager wave;
    Text guestNote;
    GameObject noteRoot;
    float nextRefresh;

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
            (scene.name.IndexOf("Island", StringComparison.OrdinalIgnoreCase) >= 0 ||
             FindAnyObjectByType<BuildPlot>() != null);

        if (!onIsland)
        {
            if (instance != null)
                instance.gameObject.SetActive(false);
            return;
        }

        if (instance == null)
        {
            var go = new GameObject("GuestFeedback");
            DontDestroyOnLoad(go);
            go.AddComponent<GuestFeedback>();
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

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.25f;
        Refresh();
    }

    void Refresh()
    {
        if (wave == null)
            wave = FindAnyObjectByType<GuestWaveManager>();

        bool hasGuests = wave != null && wave.CurrentWaveGuests.Count > 0;
        BottleneckService problem;
        bool showNote = hasGuests && TryWorstBottleneck(out problem);
        if (noteRoot != null)
            noteRoot.SetActive(showNote);
        if (showNote && guestNote != null)
            guestNote.text = Quote(problem);
    }

    static bool TryWorstBottleneck(out BottleneckService worst)
    {
        worst = default;
        var monitor = BottleneckMonitor.Instance;
        if (monitor == null)
            return false;

        int longest = -1;
        bool found = false;
        foreach (BottleneckService service in BottleneckServiceInfo.All)
        {
            if (monitor.GetState(service) != BottleneckState.Bottleneck)
                continue;

            int queue = monitor.GetQueue(service);
            if (queue <= longest)
                continue;

            longest = queue;
            worst = service;
            found = true;
        }

        return found;
    }

    public static string Quote(BottleneckService service)
    {
        switch (service)
        {
            case BottleneckService.Rooms:
                return "Voľná izba sa uvoľní až o chvíľu.";
            case BottleneckService.Drinks:
                return "Pri bare je rad.";
            case BottleneckService.Food:
                return "Na jedlo sa čaká trochu dlho.";
            case BottleneckService.Relax:
                return "Pri bazéne nie je voľné lehátko.";
            default:
                return "Na bungalov s výhľadom sa čaká.";
        }
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        noteRoot = CreateUi("GuestNote", transform);
        var noteRect = noteRoot.GetComponent<RectTransform>();
        noteRect.anchorMin = new Vector2(1f, 1f);
        noteRect.anchorMax = new Vector2(1f, 1f);
        noteRect.pivot = new Vector2(1f, 1f);
        noteRect.anchoredPosition = new Vector2(-36f, -114f);
        noteRect.sizeDelta = new Vector2(460f, 96f);

        var noteImage = noteRoot.AddComponent<Image>();
        noteImage.color = new Color(0.98f, 0.94f, 0.86f, 0.96f);
        noteImage.raycastTarget = false;

        var speaker = CreateText("Speaker", noteRoot.transform, "Hosť", 16, new Color(0.45f, 0.24f, 0.08f, 1f));
        speaker.fontStyle = FontStyle.Bold;
        var speakerRect = speaker.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 0.62f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.offsetMin = new Vector2(16f, 0f);
        speakerRect.offsetMax = new Vector2(-16f, -8f);

        guestNote = CreateText("Quote", noteRoot.transform, "", 20, new Color(0.2f, 0.1f, 0.05f, 1f));
        var quoteRect = guestNote.rectTransform;
        quoteRect.anchorMin = new Vector2(0f, 0f);
        quoteRect.anchorMax = new Vector2(1f, 0.68f);
        quoteRect.offsetMin = new Vector2(16f, 8f);
        quoteRect.offsetMax = new Vector2(-16f, 0f);
        guestNote.horizontalOverflow = HorizontalWrapMode.Wrap;
        guestNote.verticalOverflow = VerticalWrapMode.Truncate;

        noteRoot.SetActive(false);
    }

    static Text CreateText(string name, Transform parent, string value, int size, Color color)
    {
        var go = CreateUi(name, parent);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
