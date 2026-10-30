using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ikonky nad budovami na ostrove. Klik na ikonku = upgrade budovy na model ďalšej úrovne
// (napr. BeachHotel_L1 -> BeachHotel_L2). Upgrade stojí mince a energiu (ResortResources).
// Budovy sa v scéne hľadajú podľa mena modelu medzi koreňovými objektmi scény,
// takže netreba nič ručne priraďovať na budovy.
public class BuildingUpgradeManager : MonoBehaviour
{
    [Serializable]
    public class BuildingType
    {
        public string label = "Hotel";

        [Tooltip("Cesta v priečinku Resources bez prípony, napr. UI/Icons/building_hotel")]
        public string iconResource;

        [Tooltip("Modely úrovní od najnižšej (L1) po najvyššiu. Meno modelu sa používa na nájdenie budovy v scéne.")]
        public GameObject[] levels;

        [Tooltip("Cena upgradu v minciach: index 0 = z úrovne 1 na 2, index 1 = z 2 na 3, ...")]
        public int[] upgradeCoins;

        [Tooltip("Cena upgradu v energii (rovnaké indexovanie ako mince)")]
        public int[] upgradeEnergy;
    }

    [SerializeField] BuildingType[] types;
    [SerializeField] float iconSize = 78f;
    [Tooltip("O koľko metrov nad strechou budovy sa ikonka zobrazí")]
    [SerializeField] float heightAboveBuilding = 0.8f;

    static readonly Color TextColor = new Color(1f, 0.97f, 0.88f, 1f);
    static readonly Color ShortColor = new Color(1f, 0.42f, 0.38f, 1f);

    class Building
    {
        public BuildingType type;
        public GameObject instance;
        public int level;
        public RectTransform root;
        public Text levelText;
        public GameObject costRow;
        public Text coinText;
        public Text energyText;
        public int coinCost;
        public int energyCost;
        public int affordState = -1;
        public Vector3 anchor;
        public float punch;
        public float shake;
    }

    readonly List<Building> buildings = new List<Building>();
    Canvas canvas;
    Camera cam;

    void Start()
    {
        if (types == null || types.Length == 0)
        {
            Debug.LogWarning("BuildingUpgradeManager: nie sú nastavené typy budov.");
            return;
        }

        BuildCanvas();

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var type in types)
            {
                int level = LevelOf(type, root.name);
                if (level < 0) continue;
                AddBuilding(type, root, level);
                break;
            }
        }
    }

    // Vráti index úrovne podľa mena objektu (aj "BeachHotel_L2 (1)"), alebo -1 ak to nie je táto budova.
    static int LevelOf(BuildingType type, string objectName)
    {
        if (type.levels == null) return -1;
        for (int i = 0; i < type.levels.Length; i++)
        {
            var model = type.levels[i];
            if (model == null) continue;
            if (objectName == model.name || objectName.StartsWith(model.name + " (", StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    static int CostAt(int[] costs, int index)
    {
        return costs != null && index >= 0 && index < costs.Length ? Mathf.Max(0, costs[index]) : 0;
    }

    void BuildCanvas()
    {
        var go = new GameObject("BuildingIcons");
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
    }

    void AddBuilding(BuildingType type, GameObject instance, int level)
    {
        var b = new Building { type = type, instance = instance, level = level };

        var icon = new GameObject(type.label + "Icon", typeof(RectTransform));
        icon.transform.SetParent(canvas.transform, false);
        b.root = (RectTransform)icon.transform;
        b.root.sizeDelta = new Vector2(iconSize, iconSize);

        var image = icon.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>(type.iconResource);
        image.preserveAspect = true;
        if (image.sprite == null)
            Debug.LogWarning("BuildingUpgradeManager: chýba ikonka Resources/" + type.iconResource);

        var button = icon.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => Upgrade(b));

        // úroveň pod ikonkou
        b.levelText = CreateText(icon.transform, "Level", 22, TextAnchor.UpperCenter);
        var levelRect = b.levelText.rectTransform;
        levelRect.anchorMin = new Vector2(0.5f, 0f);
        levelRect.anchorMax = new Vector2(0.5f, 0f);
        levelRect.pivot = new Vector2(0.5f, 1f);
        levelRect.anchoredPosition = new Vector2(0f, -2f);
        levelRect.sizeDelta = new Vector2(200f, 28f);

        // cena upgradu: [minca] 50  [energia] 10
        b.costRow = new GameObject("Cost", typeof(RectTransform));
        b.costRow.transform.SetParent(icon.transform, false);
        var rowRect = (RectTransform)b.costRow.transform;
        rowRect.anchorMin = new Vector2(0.5f, 0f);
        rowRect.anchorMax = new Vector2(0.5f, 0f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -32f);
        rowRect.sizeDelta = new Vector2(200f, 30f);

        CreateCostIcon(b.costRow.transform, "CoinIcon", "UI/Icons/coin", -84f);
        b.coinText = CreateCostText(b.costRow.transform, "CoinCost", -68f);
        CreateCostIcon(b.costRow.transform, "EnergyIcon", "UI/Icons/energy", 12f);
        b.energyText = CreateCostText(b.costRow.transform, "EnergyCost", 28f);

        b.anchor = AnchorFor(instance);
        UpdateLabel(b);
        buildings.Add(b);
    }

    static Text CreateText(Transform parent, string name, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = align;
        text.color = TextColor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.08f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        return text;
    }

    static void CreateCostIcon(Transform parent, string name, string resource, float x)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(26f, 26f);
        var image = go.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>(resource);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    static Text CreateCostText(Transform parent, string name, float x)
    {
        var text = CreateText(parent, name, 22, TextAnchor.MiddleLeft);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(64f, 30f);
        return text;
    }

    Vector3 AnchorFor(GameObject building)
    {
        var renderers = building.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return building.transform.position + Vector3.up * 5f;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return new Vector3(bounds.center.x, bounds.max.y + heightAboveBuilding, bounds.center.z);
    }

    static void UpdateLabel(Building b)
    {
        bool isMax = b.level + 1 >= b.type.levels.Length;
        b.levelText.text = (isMax ? "MAX Level" : ("Level " + (b.level + 1)));
        //b.costRow.SetActive(!isMax);
        b.costRow.SetActive(false);
        b.coinCost = CostAt(b.type.upgradeCoins, b.level);
        b.energyCost = CostAt(b.type.upgradeEnergy, b.level);
        b.coinText.text = b.coinCost.ToString();
        b.energyText.text = b.energyCost.ToString();
        b.affordState = -1;
    }

    // Červené číslo = tohto zdroja nie je dosť.
    static void UpdateAffordability(Building b)
    {
        if (!b.costRow.activeSelf) return;

        var res = ResortResources.Instance;
        bool coinsOk = res == null || res.Coins >= b.coinCost;
        bool energyOk = res == null || res.Energy >= b.energyCost;
        int state = (coinsOk ? 1 : 0) | (energyOk ? 2 : 0);
        if (state == b.affordState) return;

        b.affordState = state;
        b.coinText.color = coinsOk ? TextColor : ShortColor;
        b.energyText.color = energyOk ? TextColor : ShortColor;
    }

    void Upgrade(Building b)
    {
        if (b.instance == null) return;

        int nextIndex = b.level + 1;
        if (nextIndex >= b.type.levels.Length || b.type.levels[nextIndex] == null)
        {
            b.punch = 1f;
            Debug.Log(b.type.label + ": už je na maximálnej úrovni (" + (b.level + 1) + ").");
            return;
        }

        var res = ResortResources.Instance;
        if (res == null)
        {
            b.shake = 1f;
            Debug.LogWarning("BuildingUpgradeManager: v scéne nie je ResortResources, upgrade sa nedá zaplatiť.");
            return;
        }

        if (!res.TrySpend(b.coinCost, b.energyCost))
        {
            b.shake = 1f;
            Debug.Log(b.type.label + ": nedostatok zdrojov. Treba " + b.coinCost + " mincí a " + b.energyCost +
                      " energie, máš " + res.Coins + " a " + res.Energy + ".");
            return;
        }

        var next = b.type.levels[nextIndex];
        var old = b.instance.transform;
        var upgraded = Instantiate(next, old.position, old.rotation, old.parent);
        upgraded.name = next.name;
        upgraded.transform.localScale = old.localScale;
        Destroy(b.instance);

        b.instance = upgraded;
        b.level = nextIndex;
        b.anchor = AnchorFor(upgraded);
        b.punch = 1f;
        UpdateLabel(b);
        Debug.Log(b.type.label + ": upgrade na úroveň " + (b.level + 1) + "/" + b.type.levels.Length +
                  " (-" + (CostAt(b.type.upgradeCoins, nextIndex - 1)) + " mincí, -" +
                  (CostAt(b.type.upgradeEnergy, nextIndex - 1)) + " energie)");
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        foreach (var b in buildings)
        {
            if (b.instance == null) continue;

            var screen = cam.WorldToScreenPoint(b.anchor);
            bool visible = screen.z > 0f;
            if (b.root.gameObject.activeSelf != visible) b.root.gameObject.SetActive(visible);
            if (!visible) continue;

            float dx = 0f;
            if (b.shake > 0f)
            {
                b.shake = Mathf.Max(0f, b.shake - Time.unscaledDeltaTime * 2.5f);
                dx = Mathf.Sin(b.shake * 40f) * 10f * b.shake;
            }
            b.root.position = new Vector3(screen.x + dx, screen.y, 0f);

            float scale = 1f;
            if (b.punch > 0f)
            {
                b.punch = Mathf.Max(0f, b.punch - Time.unscaledDeltaTime * 3f);
                scale = 1f + Mathf.Sin(b.punch * Mathf.PI) * 0.25f;
            }
            b.root.localScale = new Vector3(scale, scale, 1f);

            UpdateAffordability(b);
        }
    }
}
