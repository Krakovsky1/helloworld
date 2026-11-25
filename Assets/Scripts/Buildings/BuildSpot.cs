using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Spoločný základ pre budovy, ktoré sa na ostrove najprv postavia (bazén, wellness, ...).
//  - Kým hotel (hlavná budova) nemá požadovanú úroveň, nie je vidieť nič (bazén: hotel Level 2, wellness: Level 3).
//  - Potom je nad miestom ikonka "+". Klik otvorí okno "Postaviť ..." s cenou.
//  - Po postavení je budova na Level 1 a ikonka ukazuje písmeno budovy s úrovňou; klik otvorí okno
//    vylepšenia až po maxLevel. Žiadna budova nesmie mať vyššiu úroveň ako hotel.
// FR-29: stavba aj vylepšenie trvá nejaký čas (BuildTimers) a dá sa zaplatiť aj drahokamami.
//        Počas stavby je pod ikonkou odpočet; klik otvorí okno s okamžitým dokončením za drahokamy.
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
    [Tooltip("Posun ikonky na obrazovke (px pri 1920x1080), záporné = nižšie")]
    [SerializeField] float iconOffsetY = -22f;

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
    Canvas iconCanvas;
    Construction construction;   // FR-29: prebiehajúca stavba (null = nestavia sa)
    string shownLabel;

    static readonly Color BuildingColor = new Color(0.98f, 0.86f, 0.45f, 1f);

    // Prebieha práve stavba / vylepšenie?
    public bool IsUnderConstruction => construction != null;

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
            gemCost = BuildTimers.GemPrice(NextCoinCost(), NextEnergyCost()),
            buildSeconds = BuildTimers.DurationSeconds(Level + 1),
            construction = construction,
        };

        if (construction != null)
        {
            popup.Show(info, null, null, SpeedUp);
            return;
        }

        popup.Show(info, () => OnBuyClicked(false), () => OnBuyClicked(true), null);
    }

    // Po zaplatení sa okno prepne na odpočet stavby (s možnosťou urýchlenia).
    void OnBuyClicked(bool withGems)
    {
        if (BuildOrUpgrade(withGems))
        {
            popup.Close();
            if (construction != null) OnIconClicked();
        }
        else popup.Refresh();
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

    // Zaplatí stavbu (Level 0 -> 1) alebo vylepšenie (1 -> 2 -> 3) mincami alebo drahokamami
    // a spustí stavbu na čas (FR-29). Vráti true, ak sa stavba začala.
    bool BuildOrUpgrade(bool withGems)
    {
        if (Level >= maxLevel || construction != null) return false;

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
        int gems = BuildTimers.GemPrice(coins, energy);
        // Pri platbe drahokamami sa energia neplatí (drahokamy sú preto drahšie).
        if (withGems ? !res.TrySpendGems(gems) : !res.TrySpend(coins, energy)) return false;

        int target = Level + 1;
        int seconds = BuildTimers.DurationSeconds(target);
        Debug.Log(nameNominative + (target == 1 ? ": stavba začala" : ": vylepšenie na Level " + target + " začalo") +
                  " (" + BuildTimers.FormatDuration(seconds) + ", -" + (withGems ? gems + " drahokamov" : coins + " mincí, -" + energy + " energie") + ")");

        construction = new Construction(target, seconds);
        punch = 1f;
        shownLabel = null;
        if (construction.IsDone) CompleteConstruction();
        return true;
    }

    // FR-29: okamžité dokončenie za drahokamy
    void SpeedUp()
    {
        if (construction == null) return;
        int cost = construction.SpeedUpCost;
        var res = ResortResources.Instance;
        if (res == null || !res.TrySpendGems(cost))
        {
            popup.Refresh();
            return;
        }

        Debug.Log(nameNominative + ": stavba urýchlená za " + cost + " drahokamov.");
        CompleteConstruction();
    }

    // Stavba hotová: nová úroveň, model a udalosti.
    void CompleteConstruction()
    {
        var c = construction;
        construction = null;
        shownLabel = null;
        if (popup != null && popup.IsShowing(c)) popup.Close();

        Level++;
        UpdateModel();
        punch = 1f;
        UpdateIcon();

        if (Level == 1)
        {
            OnBuilt();
            Debug.Log(nameNominative + ": postavený na Level 1");
        }
        else
        {
            Debug.Log(nameNominative + ": vylepšený na Level " + Level + "/" + maxLevel);
        }

        LevelChanged?.Invoke(Level);
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
        iconCanvas = canvas;
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

        if (construction != null)
        {
            // FR-29: počas stavby ikonka budovy a odpočet pod ňou
            iconImage.sprite = builtSprite != null ? builtSprite : plusSprite;
            string text = construction.WaitingForServer
                ? "Čaká na čas…"
                : "Stavba " + BuildTimers.FormatCountdown(construction.Remaining);
            if (text != shownLabel)
            {
                shownLabel = text;
                label.text = text;
                label.color = BuildingColor;
            }
            return;
        }

        label.color = TextColor;
        iconImage.sprite = IsBuilt ? builtSprite : plusSprite;
        // Pod ikonkou sa úroveň nepíše (ako pri H / R / B); text je len počas stavby (odpočet).
        label.text = "";
    }

    void LateUpdate()
    {
        if (icon == null) return;
        if (cam == null) cam = Camera.main;

        // FR-29: stavba na čas
        if (construction != null)
        {
            if (construction.IsDone) CompleteConstruction();
            else UpdateIcon();
        }

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

        float scaleFactor = iconCanvas != null ? iconCanvas.scaleFactor : 1f;
        icon.position = new Vector3(screen.x, screen.y + iconOffsetY * scaleFactor, 0f);

        float scale = 1f;
        if (!IsBuilt && construction == null) scale += Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;   // jemné pulzovanie "+"
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
