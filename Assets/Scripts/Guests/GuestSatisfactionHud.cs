using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// FR-10: priebežná spokojnosť hostí počas vlny.
// Vpravo hore je aktuálne percento. Každý hosť podľa neho mení výraz.
public class GuestSatisfactionHud : MonoBehaviour
{
    static GuestSatisfactionHud instance;
    static bool sceneHooked;

    GuestWaveManager wave;
    Text satisfactionValue;
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
            var go = new GameObject("GuestSatisfactionHud");
            DontDestroyOnLoad(go);
            go.AddComponent<GuestSatisfactionHud>();
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
        if (satisfactionValue != null)
            satisfactionValue.transform.parent.gameObject.SetActive(hasGuests);
        if (!hasGuests)
            return;

        float score = GuestSatisfaction.Average(wave.CurrentWaveGuests, IsBottleneck);
        ApplyToGuests();
        satisfactionValue.text = Mathf.RoundToInt(score * 100f) + " %";
    }

    void ApplyToGuests()
    {
        var guests = wave.CurrentWaveGuests;
        for (int i = 0; i < guests.Count; i++)
        {
            GameObject guest = guests[i];
            if (guest == null)
                continue;

            var anim = guest.GetComponent<CharacterAnimator>();
            if (anim == null)
                continue;

            anim.SetSatisfaction(GuestSatisfaction.ForGuest(guest.GetComponent<GuestNeeds>(), IsBottleneck));
        }
    }

    static bool IsBottleneck(GuestNeedType type)
    {
        if (BottleneckMonitor.Instance == null)
            return false;
        if (!GuestSatisfaction.TryMap(type, out BottleneckService service))
            return false;
        return BottleneckMonitor.Instance.GetState(service) == BottleneckState.Bottleneck;
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

        var pill = CreateUi("Satisfaction", transform);
        var pillRect = pill.GetComponent<RectTransform>();
        pillRect.anchorMin = new Vector2(1f, 1f);
        pillRect.anchorMax = new Vector2(1f, 1f);
        pillRect.pivot = new Vector2(1f, 1f);
        pillRect.anchoredPosition = new Vector2(-36f, -24f);
        pillRect.sizeDelta = new Vector2(460f, 78f);

        var pillImage = pill.AddComponent<Image>();
        pillImage.color = new Color(0.16f, 0.1f, 0.06f, 0.94f);
        pillImage.raycastTarget = false;

        var title = CreateText("Title", pill.transform, "Spokojnosť hostí", 18, new Color(0.98f, 0.86f, 0.45f, 1f));
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(16f, 0f);
        titleRect.offsetMax = new Vector2(-16f, -8f);

        satisfactionValue = CreateText("Value", pill.transform, "100 %", 32, new Color(1f, 0.97f, 0.88f, 1f));
        satisfactionValue.fontStyle = FontStyle.Bold;
        var valueRect = satisfactionValue.rectTransform;
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(1f, 0.55f);
        valueRect.offsetMin = new Vector2(16f, 8f);
        valueRect.offsetMax = new Vector2(-16f, 0f);

        pill.SetActive(false);
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
