using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// FR-09: krátky komentár, keď niektorá služba nestíha.
// Bublina vyletí z budovy, ktorá má problém, chvíľu stúpa a zmizne.
// Systémové varovanie z FR-08 ostáva samostatne.
public class GuestFeedback : MonoBehaviour
{
    const float Life = 3.2f;
    const float Rise = 78f;
    const float SpawnGap = 5.5f;
    const int MaxBubbles = 1;

    static GuestFeedback instance;
    static bool sceneHooked;
    static Sprite circle;
    static Sprite roundRect;

    GuestWaveManager wave;
    BuildingUpgradeManager upgrades;
    readonly List<Bubble> bubbles = new List<Bubble>();
    float nextSpawn;
    int nextService;

    class Bubble
    {
        public GameObject root;
        public RectTransform rect;
        public CanvasGroup group;
        public float age;
        public float rise;
        public float drift;
        public Vector2 origin;
    }

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

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = bubbles.Count - 1; i >= 0; i--)
        {
            Bubble bubble = bubbles[i];
            bubble.age += dt;
            if (bubble.age >= Life)
            {
                Destroy(bubble.root);
                bubbles.RemoveAt(i);
                continue;
            }

            float k = bubble.age / Life;
            float y = bubble.origin.y + bubble.rise * Mathf.SmoothStep(0f, 1f, k);
            float x = bubble.origin.x + Mathf.Sin(bubble.age * 1.3f) * bubble.drift;
            bubble.rect.position = new Vector3(x, y, 0f);

            float fadeIn = Mathf.Clamp01(bubble.age / 0.28f);
            float fadeOut = Mathf.Clamp01((Life - bubble.age) / 0.75f);
            bubble.group.alpha = Mathf.Min(fadeIn, fadeOut);
        }

        if (Time.unscaledTime < nextSpawn || bubbles.Count >= MaxBubbles)
            return;

        nextSpawn = Time.unscaledTime + SpawnGap;
        TrySpawn();
    }

    void TrySpawn()
    {
        if (wave == null)
            wave = FindAnyObjectByType<GuestWaveManager>();
        if (wave == null || wave.CurrentWaveGuests.Count == 0)
            return;

        if (!TryNextProblem(out BottleneckService problem))
            return;

        GameObject building = FindBuilding(problem);
        if (building == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 screen = cam.WorldToScreenPoint(BuildingTop(building));
        if (screen.z <= 0f)
            return;

        Spawn(Quote(problem), new Vector2(screen.x, screen.y));
    }

    bool TryNextProblem(out BottleneckService problem)
    {
        problem = default;
        BottleneckMonitor monitor = BottleneckMonitor.Instance;
        if (monitor == null)
            return false;

        BottleneckService[] all = BottleneckServiceInfo.All;
        for (int step = 0; step < all.Length; step++)
        {
            int index = (nextService + step) % all.Length;
            BottleneckService service = all[index];
            if (monitor.GetState(service) != BottleneckState.Bottleneck)
                continue;

            nextService = (index + 1) % all.Length;
            problem = service;
            return true;
        }

        return false;
    }

    GameObject FindBuilding(BottleneckService service)
    {
        if (upgrades == null)
            upgrades = FindAnyObjectByType<BuildingUpgradeManager>();
        if (upgrades != null)
        {
            GameObject managed = upgrades.GetBuilding(BottleneckServiceInfo.UpgradeLabel(service));
            if (managed != null)
                return managed;
        }

        foreach (string name in BottleneckServiceInfo.SceneNames(service))
        {
            GameObject named = GameObject.Find(name);
            if (named != null)
                return named;
        }

        return null;
    }

    static Vector3 BuildingTop(GameObject building)
    {
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!any)
            return building.transform.position + Vector3.up * 2f;
        return new Vector3(bounds.center.x, bounds.max.y + 0.35f, bounds.center.z);
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

    void Spawn(string quote, Vector2 screen)
    {
        var root = new GameObject("Thought", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(196f, 84f);
        rect.pivot = new Vector2(0.16f, 0.08f);
        rect.position = new Vector3(screen.x, screen.y, 0f);

        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Color edge = new Color(0.62f, 0.45f, 0.28f, 1f);
        Color paper = new Color(0.99f, 0.97f, 0.93f, 0.98f);
        // Chvost je pod bublinou, mimo textu. Najprv menšie krúžky, potom telo, text navrch.
        Blob("TailSmall", root.transform, CircleSprite(), new Vector2(10f, 7f), new Vector2(8f, 8f), edge);
        Blob("Tail", root.transform, CircleSprite(), new Vector2(24f, 18f), new Vector2(14f, 14f), edge);
        Blob("TailFill", root.transform, CircleSprite(), new Vector2(24f, 18f), new Vector2(10f, 10f), paper);
        Blob("TailSmallFill", root.transform, CircleSprite(), new Vector2(10f, 7f), new Vector2(5f, 5f), paper);
        Blob("Edge", root.transform, RoundRectSprite(), new Vector2(112f, 58f), new Vector2(196f, 60f), edge);
        Blob("Body", root.transform, RoundRectSprite(), new Vector2(112f, 58f), new Vector2(188f, 52f), paper);

        Text label = CreateText("Quote", root.transform, quote, 14, new Color(0.22f, 0.12f, 0.06f, 1f));
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(112f, 58f);
        labelRect.sizeDelta = new Vector2(168f, 42f);

        bubbles.Add(new Bubble
        {
            root = root,
            rect = rect,
            group = group,
            age = 0f,
            rise = Rise + UnityEngine.Random.Range(-18f, 28f),
            drift = UnityEngine.Random.Range(6f, 14f),
            origin = screen
        });
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 112;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    static void Blob(string name, Transform parent, Sprite sprite, Vector2 anchored, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite == roundRect ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
    }

    static Sprite RoundRectSprite()
    {
        if (roundRect != null)
            return roundRect;

        const int width = 96;
        const int height = 64;
        const float radius = 22f;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * height];
        var half = new Vector2(width * 0.5f, height * 0.5f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var point = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                float box = SdRoundBox(point, half - Vector2.one, radius);
                float alpha = Mathf.Clamp01(0.6f - box);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        roundRect = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return roundRect;
    }

    static float SdRoundBox(Vector2 point, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half + new Vector2(radius, radius);
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + Vector2.Max(q, Vector2.zero).magnitude - radius;
    }

    static Sprite CircleSprite()
    {
        if (circle != null)
            return circle;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        float radius = size * 0.5f - 1f;
        var center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - distance + 0.75f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circle;
    }

    static Text CreateText(string name, Transform parent, string value, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
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
}
