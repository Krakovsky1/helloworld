using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ikonky nad budovami na ostrove. Klik na ikonku otvorí okno ponuky vylepšenia (FR-22, UpgradePopup)
// s cenou a úrovňou; tlačidlo VYLEPŠIŤ zmení budovu na model ďalšej úrovne
// (napr. BeachHotel_L1 -> BeachHotel_L2). Upgrade stojí mince a energiu (ResortResources).
// Budovy sa v scéne hľadajú podľa mena modelu medzi koreňovými objektmi scény,
// takže netreba nič ručne priraďovať na budovy.
// FR-29: vylepšenie sa dá zaplatiť aj drahokamami (namiesto mincí) a trvá nejaký čas (BuildTimers).
// Počas stavby ikonka ukazuje odpočet; klik otvorí okno s možnosťou okamžite dokončiť za drahokamy.
// Nový model sa objaví až po dokončení stavby.
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
    [Tooltip("Posun ikonky na obrazovke (px pri 1920x1080), záporné = nižšie")]
    [SerializeField] float iconOffsetY = -22f;

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
        public Construction construction;   // FR-29: prebiehajúca stavba (null = nestavia sa)
        public string shownLabel;
    }

    readonly List<Building> buildings = new List<Building>();
    Canvas canvas;
    Camera cam;
    UpgradePopup popup;

    // Hlavná budova: žiadna iná budova (ani bazén) nesmie mať vyššiu úroveň ako hotel.
    public const string HotelLabel = "Hotel";

    // Veľkosť ikoniek H / R / B (aby mali ostatné ikonky na ostrove, napr. bazén, rovnakú veľkosť).
    public float IconSize => iconSize;

    // Aktuálna úroveň budovy podľa názvu typu (napr. "Hotel"): 1 = Level 1. Vráti 0, ak sa budova nenašla.
    public int GetLevel(string label)
    {
        foreach (var b in buildings)
            if (b.instance != null && b.type.label == label) return b.level + 1;
        return 0;
    }

    // Úroveň hotela (hlavnej budovy); 0 ak sa hotel nenašiel.
    public int HotelLevel => GetLevel(HotelLabel);

    // Najvyššia úroveň budovy (počet modelov v Inspectore); 0 ak sa budova nenašla.
    public int GetMaxLevel(string label)
    {
        foreach (var b in buildings)
            if (b.instance != null && b.type.label == label)
                return b.type.levels != null ? b.type.levels.Length : 1;
        return 0;
    }

    // Ak budova (okrem hotela) nemôže ísť na úroveň targetLevel, vráti dôvod pre hráča; inak null.
    public string LevelLimitReason(string label, int targetLevel)
    {
        if (label == HotelLabel) return null;
        int hotel = HotelLevel;
        if (hotel < 1 || targetLevel <= hotel) return null;
        return "Najprv vylepši Hotel na Level " + targetLevel;
    }

    // Aktuálny objekt budovy v scéne (po upgrade je to nový model). Vráti null, ak sa budova nenašla.
    public GameObject GetBuilding(string label)
    {
        foreach (var b in buildings)
            if (b.instance != null && b.type.label == label) return b.instance;
        return null;
    }

    void Start()
    {
        if (types == null || types.Length == 0)
        {
            Debug.LogWarning("BuildingUpgradeManager: nie sú nastavené typy budov.");
            return;
        }

        BuildCanvas();
        popup = UpgradePopup.Create(transform);

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
        button.onClick.AddListener(() => OpenPopup(b));

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
        // Pod ikonkou sa úroveň nepíše (je v okne vylepšenia); text sa ukáže len počas stavby (odpočet).
        b.levelText.text = "";
        b.levelText.color = TextColor;
        b.shownLabel = null;
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

    // FR-22: klik na ikonku -> okno s cenou a úrovňou. Upgrade sa spraví až tlačidlom VYLEPŠIŤ.
    // FR-29: počas stavby okno ukazuje odpočet a tlačidlo DOKONČIŤ za drahokamy.
    void OpenPopup(Building b)
    {
        if (b.instance == null) return;
        if (popup == null) popup = UpgradePopup.Create(transform);
        if (popup.IsOpen) return;

        var icon = b.root != null ? b.root.GetComponent<Image>() : null;
        var info = new UpgradePopup.Info
        {
            nameNominative = DisplayName(b.type.label, false),
            nameAccusative = DisplayName(b.type.label, true),
            icon = icon != null ? icon.sprite : null,
            level = b.level + 1,
            maxLevel = b.type.levels != null ? b.type.levels.Length : 1,
            coinCost = b.coinCost,
            energyCost = b.energyCost,
            blockedReason = LevelLimitReason(b.type.label, b.level + 2),
            gemCost = BuildTimers.GemPrice(b.coinCost, b.energyCost),
            buildSeconds = BuildTimers.DurationSeconds(b.level + 2),
            construction = b.construction,
        };

        if (b.construction != null)
        {
            popup.Show(info, null, null, () => SpeedUp(b));
            return;
        }

        popup.Show(info, () => OnBuyClicked(b, false), () => OnBuyClicked(b, true), null);
    }

    // Po zaplatení sa okno prepne na odpočet stavby (s možnosťou urýchlenia).
    void OnBuyClicked(Building b, bool withGems)
    {
        if (StartUpgrade(b, withGems))
        {
            popup.Close();
            if (b.construction != null) OpenPopup(b);
        }
        else popup.Refresh();
    }

    // Slovenský názov budovy do okna (label v Inspectore je "Hotel" / "Restaurant" / "Bar").
    static string DisplayName(string label, bool accusative)
    {
        switch (label)
        {
            case "Restaurant": return accusative ? "Reštauráciu" : "Reštaurácia";
            case "Bar": return "Plážový bar";
            default: return label;
        }
    }

    // FR-29: Zaplatí vylepšenie (mincami alebo drahokamami + energia) a spustí stavbu na čas.
    // Vráti true, ak sa stavba začala (alebo hneď dokončila, ak trvá 0 s).
    bool StartUpgrade(Building b, bool withGems)
    {
        if (b.instance == null || b.construction != null) return false;

        int nextIndex = b.level + 1;
        if (nextIndex >= b.type.levels.Length || b.type.levels[nextIndex] == null)
        {
            b.punch = 1f;
            Debug.Log(b.type.label + ": už je na maximálnej úrovni (" + (b.level + 1) + ").");
            return false;
        }

        string limit = LevelLimitReason(b.type.label, nextIndex + 1);
        if (limit != null)
        {
            b.shake = 1f;
            Debug.Log(b.type.label + ": " + limit + " (žiadna budova nesmie mať vyššiu úroveň ako hotel).");
            return false;
        }

        var res = ResortResources.Instance;
        if (res == null)
        {
            b.shake = 1f;
            Debug.LogWarning("BuildingUpgradeManager: v scéne nie je ResortResources, upgrade sa nedá zaplatiť.");
            return false;
        }

        int gemCost = BuildTimers.GemPrice(b.coinCost, b.energyCost);
        // Pri platbe drahokamami sa energia neplatí (drahokamy sú preto drahšie).
        bool paid = withGems ? res.TrySpendGems(gemCost) : res.TrySpend(b.coinCost, b.energyCost);
        if (!paid)
        {
            b.shake = 1f;
            if (withGems)
                Debug.Log(b.type.label + ": nedostatok drahokamov. Treba " + gemCost + ", máš " + res.Gems + ".");
            else
                Debug.Log(b.type.label + ": nedostatok zdrojov. Treba " + b.coinCost + " mincí a " + b.energyCost +
                          " energie, máš " + res.Coins + " a " + res.Energy + ".");
            return false;
        }

        int targetLevel = nextIndex + 1;
        int seconds = BuildTimers.DurationSeconds(targetLevel);
        Debug.Log(b.type.label + ": stavba Level " + targetLevel + " začala (" + BuildTimers.FormatDuration(seconds) + ", -" +
                  (withGems ? gemCost + " drahokamov" : b.coinCost + " mincí, -" + b.energyCost + " energie") + ")");

        b.construction = new Construction(targetLevel, seconds);
        b.punch = 1f;
        if (b.construction.IsDone) CompleteUpgrade(b);
        SaveManager.RequestSave();   // FR-39: začiatok stavby
        return true;
    }

    // FR-29: Okamžité dokončenie stavby za drahokamy.
    void SpeedUp(Building b)
    {
        var c = b.construction;
        if (c == null) return;

        int cost = c.SpeedUpCost;
        var res = ResortResources.Instance;
        if (res == null || !res.TrySpendGems(cost))
        {
            b.shake = 1f;
            popup.Refresh();
            return;
        }

        Debug.Log(b.type.label + ": stavba urýchlená za " + cost + " drahokamov.");
        CompleteUpgrade(b);
    }

    // Stavba je hotová: vymení model za ďalšiu úroveň.
    void CompleteUpgrade(Building b)
    {
        var c = b.construction;
        b.construction = null;
        if (popup != null && popup.IsShowing(c)) popup.Close();
        if (b.instance == null) return;

        int nextIndex = b.level + 1;
        if (!SwapToLevel(b, nextIndex)) return;

        b.punch = 1f;
        Debug.Log(b.type.label + ": stavba hotová - úroveň " + (b.level + 1) + "/" + b.type.levels.Length);
        SaveManager.RequestSave();   // FR-39: koniec stavby
    }

    // Vymení model budovy za úroveň levelIndex (0 = Level 1). Vráti false, ak model chýba.
    bool SwapToLevel(Building b, int levelIndex)
    {
        if (b.instance == null || b.type.levels == null) return false;
        if (levelIndex < 0 || levelIndex >= b.type.levels.Length || b.type.levels[levelIndex] == null) return false;
        if (levelIndex == b.level) return true;

        var next = b.type.levels[levelIndex];
        var old = b.instance.transform;
        var upgraded = Instantiate(next, old.position, old.rotation, old.parent);
        upgraded.name = next.name;
        upgraded.transform.localScale = old.localScale;
        Destroy(b.instance);

        b.instance = upgraded;
        b.level = levelIndex;
        b.anchor = AnchorFor(upgraded);
        UpdateLabel(b);
        return true;
    }

    // ---------------------------------------------------------------- FR-39: uloženie hry

    // Úrovne a prebiehajúce stavby všetkých budov (Hotel, Restaurant, Bar).
    public void CaptureSave(List<BuildingSave> into)
    {
        foreach (var b in buildings)
        {
            if (b.instance == null) continue;
            var save = new BuildingSave { label = b.type.label, level = b.level + 1 };
            if (b.construction != null)
            {
                save.constructionTarget = b.construction.targetLevel;
                save.constructionDuration = b.construction.duration;
                save.constructionEndUnix = b.construction.EndUnixForSave;
            }
            into.Add(save);
        }
    }

    // Obnoví úroveň (model) a prebiehajúcu stavbu. Ak stavba medzitým skončila, dokončí sa hneď,
    // ako je známy čas servera (napr. hru zavrieš a vrátiš sa na druhý deň).
    public void RestoreSave(List<BuildingSave> saved)
    {
        if (saved == null) return;
        foreach (var s in saved)
        {
            foreach (var b in buildings)
            {
                if (b.instance == null || b.type.label != s.label) continue;

                SwapToLevel(b, Mathf.Clamp(s.level - 1, 0, b.type.levels.Length - 1));
                b.construction = null;
                if (s.constructionTarget > b.level + 1)
                    b.construction = new Construction(s.constructionTarget, s.constructionDuration, s.constructionEndUnix);
                b.shownLabel = null;
                break;
            }
        }
    }

    // Počas stavby: "Stavba 0:45" pod ikonkou (zlatou farbou).
    static void UpdateConstructionLabel(Building b)
    {
        var c = b.construction;
        string text = c.WaitingForServer ? GameText.Get("waiting_for_time") : GameText.Get("construction_countdown", BuildTimers.FormatCountdown(c.Remaining));
        if (text == b.shownLabel) return;
        b.shownLabel = text;
        b.levelText.text = text;
        b.levelText.color = BuildingColor;
    }

    static readonly Color BuildingColor = new Color(0.98f, 0.86f, 0.45f, 1f);

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;

        // Stavby musia dobiehať aj keď kamera chýba (ikonky sa vtedy len neukážu).
        if (cam == null)
        {
            foreach (var b in buildings)
                if (b.instance != null && b.construction != null && b.construction.IsDone) CompleteUpgrade(b);
            return;
        }

        foreach (var b in buildings)
        {
            if (b.instance == null) continue;

            // FR-29: stavba na čas
            if (b.construction != null)
            {
                if (b.construction.IsDone) CompleteUpgrade(b);
                else UpdateConstructionLabel(b);
            }

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
            b.root.position = new Vector3(screen.x + dx, screen.y + iconOffsetY * canvas.scaleFactor, 0f);

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
