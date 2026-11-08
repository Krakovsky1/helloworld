using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// FR-08 Varovanie pred úzkym miestom - logika.
//
// Systém vĺn (FR-06) sem posiela, koľko jednotiek práve ČAKÁ v rade pri každej službe
// (nie koľko ľudí sedí na miestach - plné miesta sú zdravý stav):
//
//     BottleneckMonitor.Instance.ReportQueue(BottleneckService.Food, queueLength);
//     BottleneckMonitor.Instance.SetLocked(BottleneckService.Relax, false);   // po odomknutí Pool
//     BottleneckMonitor.Instance.ResetAll();                                   // na konci vlny
//
// Monitor podľa build dokumentu §4.4 prepína stavy:
//   OK ── rad > prah a rastie aspoň hysteresis sekúnd ──▶ BOTTLENECK
//   BOTTLENECK ── rad sa zmenšuje ──▶ RECOVERING
//   RECOVERING ── rad ≤ prah ──▶ OK
//   RECOVERING ── rad znova rastie aspoň hysteresis sekúnd ──▶ BOTTLENECK
// Hysterézia bráni blikaniu varovania, keď rad kolíše okolo prahu.
// Zobrazenie (farba, ikona, text) rieši BottleneckWarningUI.
public class BottleneckMonitor : MonoBehaviour
{
    public static BottleneckMonitor Instance { get; private set; }

    /// <summary>Volá sa pri každej zmene stavu služby (služba, nový stav).</summary>
    public event Action<BottleneckService, BottleneckState> StateChanged;

    [Tooltip("bottleneck_threshold: koľko čakajúcich je ešte v poriadku (TUNING)")]
    [SerializeField, Min(0)] int bottleneckThreshold = 3;

    [Tooltip("bottleneck_hysteresis_s: ako dlho musí rad rásť, kým sa zobrazí varovanie (TUNING)")]
    [SerializeField, Min(0f)] float hysteresisSeconds = 3f;

    [Tooltip("Počet miest budovy pre testovací panel (Lv1 = 3 miesta podľa build dokumentu)")]
    [SerializeField, Min(1)] int testCapacity = 3;

    [Tooltip("Testovací panel vľavo dole (len v editore a development builde)")]
    public bool showDebugPanel = true;

    class Entry
    {
        public int queue;
        public int lastQueue;
        public BottleneckState state;
        public float growTimer;
        public int peak;    // najväčší rad v stave BOTTLENECK
        public int trough;  // najmenší rad v stave RECOVERING
        public int occupied;      // koľko miest je obsadených
        public int capacity = -1; // počet miest v budove (-1 = zatiaľ neznáme)
    }

    readonly Entry[] entries = new Entry[BottleneckServiceInfo.All.Length];

    static bool sceneHooked;

    // ---------------- automatické vytvorenie na ostrove (rovnako ako ResourceHud) ----------------

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
            if (Instance != null) Instance.gameObject.SetActive(false);
            return;
        }

        if (Instance == null)
        {
            var go = new GameObject("BottleneckWarnings");
            DontDestroyOnLoad(go);
            go.AddComponent<BottleneckMonitor>();
            go.AddComponent<BottleneckWarningUI>();
            return;
        }

        Instance.gameObject.SetActive(true);
    }

    // ---------------- verejné API ----------------

    public int Threshold => bottleneckThreshold;

    public BottleneckState GetState(BottleneckService service) => entries[(int)service].state;

    public int GetQueue(BottleneckService service) => entries[(int)service].queue;

    /// <summary>Koľko jednotiek práve čaká v rade pri službe (bez tých, čo už sedia/sú obsluhovaní).</summary>
    public void ReportQueue(BottleneckService service, int waitingUnits)
    {
        entries[(int)service].queue = Mathf.Max(0, waitingUnits);
    }

    /// <summary>Obsadenosť budovy: koľko miest je obsadených a koľko ich budova má (z levelu budovy).</summary>
    public void ReportSeats(BottleneckService service, int occupied, int capacity)
    {
        var e = entries[(int)service];
        e.capacity = Mathf.Max(0, capacity);
        e.occupied = Mathf.Clamp(occupied, 0, e.capacity);
    }

    /// <summary>Počet voľných miest, alebo -1, ak obsadenosť ešte nikto nenahlásil.</summary>
    public int GetFreeSeats(BottleneckService service)
    {
        var e = entries[(int)service];
        return e.capacity < 0 ? -1 : Mathf.Max(0, e.capacity - e.occupied);
    }

    /// <summary>Zamknutá služba sa vôbec nezobrazuje (Relax pred Pool, Ocean View pred Bungalows).</summary>
    public void SetLocked(BottleneckService service, bool locked)
    {
        var e = entries[(int)service];
        if (locked)
        {
            e.queue = 0;
            SetState(service, BottleneckState.Locked);
        }
        else if (e.state == BottleneckState.Locked)
        {
            SetState(service, BottleneckState.Ok);
        }
    }

    /// <summary>Vynuluje všetky rady a varovania (napr. na konci vlny). Zamknuté služby ostanú zamknuté.</summary>
    public void ResetAll()
    {
        foreach (var s in BottleneckServiceInfo.All)
        {
            var e = entries[(int)s];
            e.queue = 0;
            e.lastQueue = 0;
            e.growTimer = 0f;
            e.occupied = 0;
            if (e.state != BottleneckState.Locked) SetState(s, BottleneckState.Ok);
        }
    }

    // ---------------- životný cyklus ----------------

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        foreach (var s in BottleneckServiceInfo.All)
        {
            entries[(int)s] = new Entry
            {
                state = BottleneckServiceInfo.LockedAtStart(s) ? BottleneckState.Locked : BottleneckState.Ok
            };
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var s in BottleneckServiceInfo.All)
            Tick(s, entries[(int)s], dt);
    }

    void Tick(BottleneckService service, Entry e, float dt)
    {
        int q = e.queue;
        bool shrinking = q < e.lastQueue;

        switch (e.state)
        {
            case BottleneckState.Ok:
                // rad nad prahom, ktorý sa nezmenšuje -> počítame, ako dlho to trvá
                if (q > bottleneckThreshold && !shrinking) e.growTimer += dt;
                else e.growTimer = 0f;

                if (e.growTimer >= hysteresisSeconds)
                {
                    e.peak = q;
                    SetState(service, BottleneckState.Bottleneck);
                }
                break;

            case BottleneckState.Bottleneck:
                if (q > e.peak) e.peak = q;
                if (q < e.peak)
                {
                    e.trough = q;
                    e.growTimer = 0f;
                    SetState(service, q <= bottleneckThreshold ? BottleneckState.Ok : BottleneckState.Recovering);
                }
                break;

            case BottleneckState.Recovering:
                if (q <= bottleneckThreshold)
                {
                    SetState(service, BottleneckState.Ok);
                    break;
                }
                if (q < e.trough) e.trough = q;

                // rad znova rastie -> po hysterézii späť do BOTTLENECK
                if (q > e.trough && !shrinking) e.growTimer += dt;
                else e.growTimer = 0f;

                if (e.growTimer >= hysteresisSeconds)
                {
                    e.peak = q;
                    SetState(service, BottleneckState.Bottleneck);
                }
                break;

            case BottleneckState.Locked:
                break;
        }

        e.lastQueue = q;
    }

    void SetState(BottleneckService service, BottleneckState state)
    {
        var e = entries[(int)service];
        if (e.state == state) return;
        e.state = state;
        e.growTimer = 0f;
        StateChanged?.Invoke(service, state);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ---------------- testovací panel (kým FR-06 neposiela skutočné rady) ----------------
    void OnGUI()
    {
        if (!showDebugPanel) return;

        float scale = Screen.height / 1080f;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float width = 470f;
        float height = 74f + entries.Length * 34f;
        float y = Screen.height / scale - height - 20f;

        GUILayout.BeginArea(new Rect(20f, y, width, height), "FR-08 test: hostia pri službách (prah radu " + bottleneckThreshold + ")", GUI.skin.window);
        foreach (var s in BottleneckServiceInfo.All)
        {
            var e = entries[(int)s];
            GUILayout.BeginHorizontal();
            string seats = e.capacity < 0 ? "" : e.occupied + "/" + e.capacity + " miest, ";
            GUILayout.Label(BottleneckServiceInfo.BuildingName(s) + ": " + seats + e.queue + " čaká", GUILayout.Width(300f));
            if (e.state == BottleneckState.Locked)
            {
                if (GUILayout.Button("Odomknúť")) SetLocked(s, false);
            }
            else
            {
                int guests = e.occupied + e.queue;
                if (GUILayout.Button("-")) SimulateGuests(s, guests - 1);
                if (GUILayout.Button("+")) SimulateGuests(s, guests + 1);
                if (GUILayout.Button("+5")) SimulateGuests(s, guests + 5);
            }
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Reset (koniec vlny)")) ResetAll();
        GUILayout.EndArea();

        GUI.matrix = oldMatrix;
    }

    // Test: hostia pri službe najprv obsadia miesta, zvyšok čaká v rade.
    void SimulateGuests(BottleneckService service, int guests)
    {
        var e = entries[(int)service];
        int capacity = e.capacity < 0 ? testCapacity : e.capacity;
        guests = Mathf.Max(0, guests);
        ReportSeats(service, Mathf.Min(guests, capacity), capacity);
        ReportQueue(service, guests - Mathf.Min(guests, capacity));
    }
#endif
}
