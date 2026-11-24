using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Spoločný základ pre budovy, ktoré sa na ostrove najprv postavia (bazén, wellness, ...).
//  - Kým hotel (hlavná budova) nemá požadovanú úroveň, nie je vidieť nič (bazén: hotel Level 2, wellness: Level 3).
//  - Potom je nad miestom ikonka "+". Klik otvorí okno "Postaviť ..." s cenou.
//  - Po postavení je budova na Level 1 a ikonka ukazuje písmeno budovy s úrovňou; klik otvorí okno
//    vylepšenia až po maxLevel. Žiadna budova nesmie mať vyššiu úroveň ako hotel.
// Pozícia a otočenie budovy = pozícia a otočenie objektu s týmto komponentom (dá sa posunúť v Scene view).
// Konkrétne budovy: PoolBuildSpot (bazén), WellnessBuildSpot (wellness).
public abstract class BuildSpot : MonoBehaviour
{
    [Header("Budova")]
    [SerializeField] protected string nameNominative = "Bazén";
    [SerializeField] protected string nameAccusative = "Bazén";

    [Tooltip("Od akej úrovne hotela (hlavnej budovy) sa dá budova postaviť")]
    [FormerlySerializedAs("requiredResortLevel")]
    [SerializeField] protected int requiredHotelLevel = 2;

    [Tooltip("Najvyššia úroveň budovy")]
    [SerializeField] protected int maxLevel = 3;

    [Header("Cena")]
    [SerializeField] protected int buildCoins = 25;
    [SerializeField] protected int buildEnergy = 3;

    [Tooltip("Cena vylepšenia v minciach: index 0 = z Level 1 na 2, index 1 = z Level 2 na 3 (zatiaľ orientačné)")]
    [SerializeField] protected int[] upgradeCoins = { 45, 70 };

    [Tooltip("Cena vylepšenia v energii (rovnaké indexovanie ako mince)")]
    [SerializeField] protected int[] upgradeEnergy = { 5, 8 };

    [Header("Modely")]
    [Tooltip("Modely podľa úrovne: 0 = Level 1, 1 = Level 2, 2 = Level 3. Prázdne miesto = použije sa model nižšej úrovne.")]
    [SerializeField] protected GameObject[] levelModels;

    [Header("Ikonka")]
    [SerializeField] protected string plusIconResource = "UI/Icons/build_plus";
    [FormerlySerializedAs("poolIconResource")]
    [SerializeField] protected string builtIconResource = "UI/Icons/building_pool";
    [Tooltip("Použije sa len vtedy, keď v scéne nie je BuildingUpgradeManager; inak má ikonka rovnakú veľkosť ako H / R / B")]
    [SerializeField] float iconSize = 48f;
    [Tooltip("Výška ikonky „+“ nad miestom, kým budova nie je postavená (m)")]
    [SerializeField] float plusHeight = 1.2f;
    [Tooltip("O koľko metrov nad budovou je ikonka po postavení")]
    [SerializeField] float heightAboveModel = 0.8f;

    static readonly Color TextColor = new Color(1f, 0.97f, 0.88f, 1f);

    // Názov budovy pre limit úrovne podľa hotela a pre logy (napr. "Pool", "Wellness").
    public abstract string Label { get; }

    // Predvolené modely podľa úrovne. V editore sa samé doplnia do "Level Models",
    // ak tam niektorý chýba (netreba ich ťahať ručne).
    protected abstract string[] DefaultModelPaths { get; }

    // Volá sa po postavení budovy (Level 1).
    protected virtual void OnBuilt() { }

    // Veľkosť plochy budovy v metroch (len pre modrý obdĺžnik v Scene view).
    protected virtual Vector2 Footprint => new Vector2(5.2f, 5.2f);

    // 0 = ešte nepostavená, 1..maxLevel = úroveň budovy
    public int Level { get; private set; }
    public bool IsBuilt => Level > 0;
    public int MaxLevel => maxLevel;

    // Volá sa po postavení (1) a po každom vylepšení (2, 3).
    public event Action<int> LevelChanged;

    BuildingUpgradeManager buildings;
    UpgradePopup popup;
    Camera cam;
    GameObject model;

    RectTransform icon;
    Image iconImage;
    Text label;
    Sprite plusSprite;
    Sprite builtSprite;
    Vector3 anchor;
    bool wasUnlocked;
    float punch;

    void Start()
    {
        buildings = FindAnyObjectByType<BuildingUpgradeManager>();
        if (buildings != null) iconSize = buildings.IconSize;   // rovnako veľká ako ikonky H / R / B
        plusSprite = Resources.Load<Sprite>(plusIconResource);
        builtSprite = Resources.Load<Sprite>(builtIconResource);
        if (plusSprite == null) Debug.LogWarning(GetType().Name + ": chýba ikonka Resources/" + plusIconResource);
        if (builtSprite == null) Debug.LogWarning(GetType().Name + ": chýba ikonka Resources/" + builtIconResource);

        BuildIcon();
        popup = UpgradePopup.Create(transform);
        anchor = transform.position + Vector3.up * plusHeight;
        UpdateIcon();
    }

    // Úroveň hotela (hlavnej budovy); 1 ak sa hotel nenašiel.
    public int HotelLevel()
    {
        if (buildings == null) buildings = FindAnyObjectByType<BuildingUpgradeManager>();
        if (buildings == null) return 1;
        return Mathf.Max(1, buildings.HotelLevel);
    }

    // Postaviť sa dá, keď má hotel aspoň requiredHotelLevel. Vylepšenia ďalej obmedzuje limit
    // "žiadna budova nesmie mať vyššiu úroveň ako hotel" (LevelLimitReason).
    public bool IsUnlocked => IsBuilt || HotelLevel() >= requiredHotelLevel;

    // ---------------------------------------------------------------- okno

    void OnIconClicked()
    {
        if (popup == null || popup.IsOpen) return;

        var info = new UpgradePopup.Info
        {
            nameNominative = nameNominative,
            nameAccusative = nameAccusative,
            icon = builtSprite != null ? builtSprite : plusSprite,
            level = Level,
            maxLevel = maxLevel,
            coinCost = NextCoinCost(),
            energyCost = NextEnergyCost(),
            isNew = !IsBuilt,
            blockedReason = LevelLimitReason(Level + 1),
        };

        popup.Show(info, () =>
        {
            if (BuildOrUpgrade()) popup.Close();
            else popup.Refresh();
        });
    }

    // Budova nesmie mať vyššiu úroveň ako hotel (hlavná budova).
    string LevelLimitReason(int targetLevel)
    {
        if (buildings == null) buildings = FindAnyObjectByType<BuildingUpgradeManager>();
        return buildings != null ? buildings.LevelLimitReason(Label, targetLevel) : null;
    }

    int NextCoinCost()
    {
        if (!IsBuilt) return Mathf.Max(0, buildCoins);
        return CostAt(upgradeCoins, Level - 1);
    }

    int NextEnergyCost()
    {
        if (!IsBuilt) return Mathf.Max(0, buildEnergy);
        return CostAt(upgradeEnergy, Level - 1);
    }

    static int CostAt(int[] costs, int index)
    {
        return costs != null && index >= 0 && index < costs.Length ? Mathf.Max(0, costs[index]) : 0;
    }

    // Postaví budovu (Level 0 -> 1) alebo ho vylepší (1 -> 2 -> 3). Vráti true pri úspechu.
    bool BuildOrUpgrade()
    {
        if (Level >= maxLevel) return false;

        string limit = LevelLimitReason(Level + 1);
        if (limit != null)
        {
            Debug.Log(nameNominative + ": " + limit + " (žiadna budova nesmie mať vyššiu úroveň ako hotel).");
            return false;
        }

        var res = ResortResources.Instance;
        if (res == null)
        {
            Debug.LogWarning(GetType().Name + ": v scéne nie je ResortResources, stavba sa nedá zaplatiť.");
            return false;
        }

        int coins = NextCoinCost();
        int energy = NextEnergyCost();
        if (!res.TrySpend(coins, energy)) return false;

        Level++;
        UpdateModel();
        punch = 1f;
        UpdateIcon();

        if (Level == 1)
        {
            OnBuilt();
            Debug.Log(nameNominative + ": postavený na Level 1 (-" + coins + " mincí, -" + energy + " energie)");
        }
        else
        {
            Debug.Log(nameNominative + ": vylepšený na Level " + Level + "/" + maxLevel +
                      " (-" + coins + " mincí, -" + energy + " energie)");
        }

        LevelChanged?.Invoke(Level);
        return true;
    }

    // Model pre aktuálnu úroveň; ak pre ňu model ešte nie je, použije sa najbližší nižší.
    void UpdateModel()
    {
        GameObject prefab = null;
        if (levelModels != null)
        {
            for (int i = Mathf.Min(Level, levelModels.Length) - 1; i >= 0; i--)
            {
                if (levelModels[i] != null) { prefab = levelModels[i]; break; }
            }
        }

        if (Level - 1 >= 0 && (levelModels == null || Level - 1 >= levelModels.Length || levelModels[Level - 1] == null))
            Debug.LogWarning(nameNominative + ": pre Level " + Level + " nie je v " + GetType().Name + " > Level Models priradený model.");

        if (prefab == null)
        {
            anchor = transform.position + Vector3.up * plusHeight;
            return;
        }

        if (model != null && model.name == prefab.name)
        {
            anchor = AnchorFor(model);
            return;   // model pre túto úroveň ešte nie je, ostáva doterajší
        }

        if (model != null) Destroy(model);
        model = Instantiate(prefab, transform.position, transform.rotation);
        model.name = prefab.name;
        anchor = AnchorFor(model);
    }

    Vector3 AnchorFor(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return target.transform.position + Vector3.up * plusHeight;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return new Vector3(bounds.center.x, bounds.max.y + heightAboveModel, bounds.center.z);
    }

    // ---------------------------------------------------------------- ikonka

    void BuildIcon()
    {
        var canvasGo = new GameObject(Label + "BuildIcon");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // rovnako ako ikonky H / R / B
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        icon = (RectTransform)go.transform;
        icon.sizeDelta = new Vector2(iconSize, iconSize);

        iconImage = go.AddComponent<Image>();
        iconImage.preserveAspect = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = iconImage;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
        button.colors = colors;
        button.onClick.AddListener(OnIconClicked);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var rect = (RectTransform)labelGo.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -2f);
        rect.sizeDelta = new Vector2(200f, 28f);
        label = labelGo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = TextColor;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = labelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.08f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);

        go.SetActive(false);
    }

    void UpdateIcon()
    {
        if (iconImage == null) return;
        iconImage.sprite = IsBuilt ? builtSprite : plusSprite;
        // Kým budova nie je postavená, je tam len "+" bez textu; potom úroveň ako pri H / R / B.
        if (!IsBuilt) label.text = "";
        else label.text = Level >= maxLevel ? "MAX Level" : "Level " + Level;
    }

    void LateUpdate()
    {
        if (icon == null) return;
        if (cam == null) cam = Camera.main;

        bool unlocked = IsUnlocked;
        if (unlocked && !wasUnlocked)
        {
            punch = 1f;   // "+" práve pribudlo
            Debug.Log(nameNominative + ": hotel má Level " + HotelLevel() + ", dá sa postaviť.");
        }
        wasUnlocked = unlocked;

        bool visible = unlocked && cam != null;
        Vector3 screen = Vector3.zero;
        if (visible)
        {
            screen = cam.WorldToScreenPoint(anchor);
            visible = screen.z > 0f;
        }

        if (icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
        if (!visible) return;

        icon.position = new Vector3(screen.x, screen.y, 0f);

        float scale = 1f;
        if (!IsBuilt) scale += Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;   // jemné pulzovanie "+"
        if (punch > 0f)
        {
            punch = Mathf.Max(0f, punch - Time.unscaledDeltaTime * 3f);
            scale += Mathf.Sin(punch * Mathf.PI) * 0.25f;
        }
        icon.localScale = new Vector3(scale, scale, 1f);
    }

#if UNITY_EDITOR
    // Doplní chýbajúce predvolené modely (DefaultModelPaths) do Level Models - pri pridaní komponentu
    // aj pri načítaní scény v editore. Ručne priradené modely nemení.
    protected virtual void Reset() { FillDefaultModels(); }
    void OnValidate() { FillDefaultModels(); }

    protected void FillDefaultModels()
    {
        int count = Mathf.Max(maxLevel, DefaultModelPaths.Length);
        if (levelModels == null || levelModels.Length < count)
        {
            var resized = new GameObject[count];
            if (levelModels != null) Array.Copy(levelModels, resized, levelModels.Length);
            levelModels = resized;
        }

        bool changed = false;
        for (int i = 0; i < DefaultModelPaths.Length && i < levelModels.Length; i++)
        {
            if (levelModels[i] != null) continue;
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPaths[i]);
            if (asset == null) continue;
            levelModels[i] = asset;
            changed = true;
        }
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.16f, 0.6f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(new Vector3(0f, 0.05f, 0f), new Vector3(Footprint.x, 0.1f, Footprint.y));
        Gizmos.color = new Color(0.16f, 0.6f, 1f, 1f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(Footprint.x, 0.1f, Footprint.y));
    }
}
