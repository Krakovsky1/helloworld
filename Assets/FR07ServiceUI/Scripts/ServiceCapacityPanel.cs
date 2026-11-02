using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FR-07: HUD for service, drinks, and food occupancy.
/// Visuals are editable in the scene/prefab; values can be supplied later
/// by another gameplay component via SetUsage(...).
/// </summary>
public class ServiceCapacityPanel : MonoBehaviour
{
    [System.Serializable]
    public class Meter
    {
        public TextMeshProUGUI valueLabel;
        public Image progressFill;
    }

    [Header("UI references (assigned by FR-07 builder)")]
    [SerializeField] private Meter services = new Meter();
    [SerializeField] private Meter drinks = new Meter();
    [SerializeField] private Meter food = new Meter();
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private TextMeshProUGUI waveLabel;

    [Header("Temporary preview, not real game state")]
    [SerializeField] private bool demoMode = true;
    [SerializeField, Min(0)] private int servicesUsed = 6;
    [SerializeField, Min(0)] private int servicesCapacity = 10;
    [SerializeField, Min(0)] private int drinksUsed = 3;
    [SerializeField, Min(0)] private int drinksCapacity = 8;
    [SerializeField, Min(0)] private int foodUsed = 7;
    [SerializeField, Min(0)] private int foodCapacity = 12;
    [SerializeField, Min(0)] private int waveNumber = 0;

    private void Awake() => Refresh();

    private void OnValidate()
    {
        // Keeps the Inspector preview in sync during Play Mode.
        if (Application.isPlaying) Refresh();
    }

    /// <summary>
    /// Call when the real occupancy / capacity system becomes available.
    /// Values are sanitized and the DEMO badge switches to LIVE.
    /// </summary>
    public void SetUsage(int currentServices, int maxServices,
                         int currentDrinks, int maxDrinks,
                         int currentFood, int maxFood)
    {
        servicesUsed = Mathf.Max(0, currentServices);
        servicesCapacity = Mathf.Max(0, maxServices);
        drinksUsed = Mathf.Max(0, currentDrinks);
        drinksCapacity = Mathf.Max(0, maxDrinks);
        foodUsed = Mathf.Max(0, currentFood);
        foodCapacity = Mathf.Max(0, maxFood);
        demoMode = false;
        Refresh();
    }

    public void SetWaveNumber(int number)
    {
        waveNumber = Mathf.Max(0, number);
        Refresh();
    }

    /// <summary>Useful for changing placeholder values during a UI review.</summary>
    public void SetDemoMode(bool enabled)
    {
        demoMode = enabled;
        Refresh();
    }

    // Called by the editor builder once the UI references are created.
    public void BindUI(Meter servicesMeter, Meter drinksMeter, Meter foodMeter,
                       TextMeshProUGUI status, TextMeshProUGUI wave)
    {
        services = servicesMeter;
        drinks = drinksMeter;
        food = foodMeter;
        statusLabel = status;
        waveLabel = wave;
        Refresh();
    }

    public void Refresh()
    {
        UpdateMeter(services, servicesUsed, servicesCapacity);
        UpdateMeter(drinks, drinksUsed, drinksCapacity);
        UpdateMeter(food, foodUsed, foodCapacity);

        if (statusLabel != null) statusLabel.text = demoMode ? "DEMO" : "LIVE";
        if (waveLabel != null)
            waveLabel.text = waveNumber == 0 ? "NO ACTIVE WAVE" : "WAVE  " + waveNumber;
    }

    private static void UpdateMeter(Meter meter, int used, int capacity)
    {
        if (meter == null) return;
        used = Mathf.Max(0, used);
        capacity = Mathf.Max(0, capacity);

        if (meter.valueLabel != null)
            meter.valueLabel.text = capacity == 0 ? "— / —" : used + " / " + capacity;

        if (meter.progressFill == null) return;

        // Keep the visible bar clipped at 100%, even if the game is over capacity.
        float proportion = capacity == 0 ? 0f : Mathf.Clamp01((float)used / capacity);
        RectTransform fill = meter.progressFill.rectTransform;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(proportion, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        meter.progressFill.gameObject.SetActive(proportion > 0.0001f);
    }
}
