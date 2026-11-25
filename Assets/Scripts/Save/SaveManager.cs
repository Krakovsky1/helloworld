using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// FR-39: Priebežné ukladanie hry.
//
// Čo sa ukladá: zdroje (mince, drahokamy, energia, hodnota rezortu), úrovne budov aj bazéna / wellness,
// prebiehajúce stavby (koniec v čase servera), počet zamestnancov, vylosovaní VIP hostia a rozohraná vlna
// (fáza, hostia a ich potreby, priradený personál, zostávajúci pobyt / cooldown, počty obslúžených potrieb,
// hodnotenia, nevyzdvihnuté odmeny).
//
// Kedy sa ukladá: po každej akcii - zmena zdrojov (nákup, odmeny, najatie personálu), začiatok aj koniec
// stavby, štart vlny, príchod hostí, prijatie / odmietnutie, koniec pobytu, odchod hostí, koniec cooldownu.
// Navyše každých 15 s a pri prepnutí / zatvorení aplikácie. Viac zmien v jednom snímku = jedno uloženie.
//
// Súbor: Application.persistentDataPath/save.json (zapisuje sa cez dočasný súbor, aby sa nepoškodil).
// Vymazanie uloženej hry: Tools > Tropical Resort > Vymazať uloženú hru.
// Objekt sa vytvorí sám na ostrove, v scéne netreba nič nastavovať.
public class SaveManager : MonoBehaviour
{
    public const string FileName = "save.json";
    const float AutosaveInterval = 15f;
    const float WaitForServerTime = 6f;   // s - pri obnove cooldownu počkáme na čas servera (offline sa pokračuje bez neho)

    public static SaveManager Instance { get; private set; }
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    // true po načítaní uloženej hry (pred tým sa nesmie ukladať, inak by sa prepísala počiatočnými hodnotami)
    public bool IsReady { get; private set; }

    static bool dirty;
    static bool suppressed;   // uložená hra bola vymazaná počas hry - do reštartu sa neukladá

    float autosaveTimer;

    ResortResources subscribedResources;
    VipInviteManager subscribedVip;
    BuildingUpgradeManager upgrades;
    StaffManager staff;
    GuestWaveManager waves;
    GuestWaveUI waveUI;
    GuestStayTimer stayTimer;
    WaveRewardManager rewards;
    GuestServiceManager services;

    // Žiada o uloženie (vykoná sa na konci snímku). Dá sa volať odkiaľkoľvek, aj keď SaveManager ešte nie je.
    public static void RequestSave()
    {
        dirty = true;
    }

    // Vymaže uloženú hru. Počas Play módu sa potom už neukladá (aby sa súbor hneď nevytvoril znova).
    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(SavePath + ".tmp")) File.Delete(SavePath + ".tmp");
            Debug.Log("FR-39 | Uložená hra vymazaná: " + SavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("FR-39 | Uloženú hru sa nepodarilo vymazať: " + e.Message);
        }

        if (Application.isPlaying) suppressed = true;
    }

    // ------------------------------------------------------------------
    // Automatické vytvorenie na ostrove
    // ------------------------------------------------------------------

    static bool sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // Projekt má vypnutý Domain Reload - statické hodnoty treba pri štarte Play módu vynulovať.
        if (sceneHooked) SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneHooked = false;
        Instance = null;
        dirty = false;
        suppressed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!sceneHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneHooked = true;
        }

        TryCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate(scene);
    }

    static void TryCreate(Scene scene)
    {
        if (Instance != null) return;

        bool onIsland = scene.IsValid() &&
            (scene.name.IndexOf("Island", StringComparison.OrdinalIgnoreCase) >= 0 ||
             FindAnyObjectByType<BuildPlot>() != null);
        if (!onIsland) return;

        var go = new GameObject("SaveManager");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<SaveManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServerClock.EnsureExists();
    }

    // ------------------------------------------------------------------
    // Načítanie
    // ------------------------------------------------------------------

    IEnumerator Start()
    {
        // Najprv nech sa inicializujú ostatné systémy (Start budov, ikoniek, vlny...).
        yield return null;
        yield return null;

        FindSystems();
        var data = ReadFile();

        if (data != null)
        {
            RestoreImmediate(data);

            // Rozohraná vlna: pobyt pokračuje s uloženým zostávajúcim časom (kým je hra zatvorená, stojí),
            // cooldown po vlne beží podľa času servera aj počas zatvorenej hry - chvíľu počkáme na čas servera.
            if (data.wave != null && data.wave.active && waves != null)
            {
                if (data.wave.cooldownEndUnix >= 0.0)
                {
                    waves.LockStartButton();
                    float waited = 0f;
                    while (!ServerClock.IsSynced && waited < WaitForServerTime)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                RestoreWave(data);
            }

            // Odmeny, ktoré hráč pred zatvorením hry nevyzdvihol: znova sa ukáže panel - hráč sa rozhodne sám.
            RestorePendingRewards(data);
        }
        else
        {
            Debug.Log("FR-39 | Uložená hra neexistuje - začína sa nová hra (" + SavePath + ")");
        }

        Subscribe(true);
        IsReady = true;
        dirty = true;   // hneď ulož stav po načítaní
    }

    void FindSystems()
    {
        upgrades = FindAnyObjectByType<BuildingUpgradeManager>();
        staff = FindAnyObjectByType<StaffManager>(FindObjectsInactive.Include);
        waves = FindAnyObjectByType<GuestWaveManager>();
        waveUI = FindAnyObjectByType<GuestWaveUI>(FindObjectsInactive.Include);
        stayTimer = FindAnyObjectByType<GuestStayTimer>(FindObjectsInactive.Include);
        rewards = FindAnyObjectByType<WaveRewardManager>(FindObjectsInactive.Include);
        services = FindAnyObjectByType<GuestServiceManager>(FindObjectsInactive.Include);
    }

    SaveData ReadFile()
    {
        string path = SavePath;
        if (!File.Exists(path)) return null;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null) throw new Exception("prázdny súbor");
            Debug.Log("FR-39 | Načítaná uložená hra: " + path);
            return data;
        }
        catch (Exception e)
        {
            // Poškodený súbor odložíme bokom, aby sa hra dala hrať ďalej.
            Debug.LogWarning("FR-39 | Uloženú hru sa nepodarilo načítať (" + e.Message + "). Začína sa nová hra.");
            try { File.Copy(path, path + ".corrupt", true); } catch (Exception) { }
            return null;
        }
    }

    // Zdroje, budovy, stavby, personál (bez čakania).
    void RestoreImmediate(SaveData data)
    {
        var res = ResortResources.Instance;
        if (res != null && data.resources != null)
            res.Restore(data.resources.coins, data.resources.gems, data.resources.energy, data.resources.resortValue);

        if (upgrades != null) upgrades.RestoreSave(data.buildings);

        if (data.buildSpots != null)
        {
            foreach (var spot in FindObjectsByType<BuildSpot>(FindObjectsSortMode.None))
            {
                var saved = data.buildSpots.Find(s => s.label == spot.Label);
                if (saved != null) spot.RestoreSave(saved);
            }
        }

        if (staff != null && data.staff != null) staff.RestoreStaff(data.staff.totalStaff);

        // Vylosovaní VIP hostia pre pripravenú vlnu
        if (VipInviteManager.Instance != null && data.vip != null) VipInviteManager.Instance.RestoreSave(data.vip);

        var wave = data.wave;
        if (wave != null)
        {
            if (waves != null) waves.RestoreWaveNumber(wave.waveNumber);
        }
    }

    // Rozohraná vlna - pokračuje z uloženej fázy, časovače podľa času servera.
    void RestoreWave(SaveData data)
    {
        var w = data.wave;
        if (waves == null || waveUI == null)
        {
            Debug.LogWarning("FR-39 | GuestWaveManager alebo GuestWaveUI chýba - rozohraná vlna sa neobnoví.");
            if (waves != null) waves.UnlockStartButtonIfIdle();
            return;
        }

        double now = ServerClock.IsSynced ? ServerClock.NowUnix : -1.0;

        // Hostia už odplávali, beží len cooldown.
        if (w.departureDone)
        {
            float cooldown = CooldownRemaining(w, now);
            waves.RestoreCooldownOnly(w.accepted);
            waveUI.RestoreCooldown(Mathf.Max(0f, cooldown));
            Debug.Log("FR-39 | Obnovený cooldown do ďalšej vlny: " + Mathf.CeilToInt(Mathf.Max(0f, cooldown)) + " s");
            return;
        }

        if (!waves.RestoreGuestsInHotel(w.guestCount, w.boatIndex, w.guests))
        {
            waves.UnlockStartButtonIfIdle();
            return;
        }

        // Ešte nerozhodnuté - panel ACCEPT / REJECT sa ukázal sám (WaveReadyForDecision).
        if (!w.decisionMade) return;

        if (w.accepted)
        {
            bool stayWasRunning = w.stayEndUnix >= 0.0 || w.stayRemaining >= 0f;
            float stay = stayWasRunning ? StayRemaining(w) : 0f;

            if (stay > 0.5f)
            {
                waveUI.RestoreAccepted(stay);
                if (services != null) services.RestoreRatings(w.ratings);
                waves.ApplyRestoredAssignments();   // personál sa vráti k rozbehnutej obsluhe
                Debug.Log("FR-39 | Obnovený pobyt hostí, zostáva " + Mathf.CeilToInt(stay) + " s");
            }
            else
            {
                // Pobyt skončil (aj počas toho, ako bola hra zatvorená) -> hostia odchádzajú.
                float cooldown = stayWasRunning
                    ? waveUI.CooldownDuration + stay   // stay <= 0: o toľko už cooldown beží
                    : CooldownRemaining(w, now);
                waveUI.RestoreDeparture(true, Mathf.Max(0f, cooldown));
                Debug.Log("FR-39 | Pobyt hostí skončil, hostia odchádzajú.");
            }

            if (services != null) services.RestoreRatings(w.ratings);
            if (rewards != null) rewards.RestoreCounts(w);
        }
        else
        {
            float cooldown = CooldownRemaining(w, now);
            waveUI.RestoreDeparture(false, Mathf.Max(0f, cooldown));
            Debug.Log("FR-39 | Odmietnutá vlna - hostia odchádzajú.");
        }
    }

    void RestorePendingRewards(SaveData data)
    {
        var w = data.wave;
        if (w == null || !w.rewardsPending) return;

        if (rewards != null)
        {
            if (services != null) services.RestoreRatings(w.ratings);   // súhrn hodnotení v paneli
            rewards.RestorePendingRewards(w);
        }
        else
        {
            Debug.LogWarning("FR-39 | WaveRewardManager chýba - nevyzdvihnuté odmeny sa nedajú zobraziť.");
        }
    }

    // Pobyt hostí: kým je hra zatvorená, stojí - platí uložený zostávajúci čas.
    // (Počas hry plynie podľa skutočného času, aj pri alt-tabe - viď GuestStayTimer.)
    static float StayRemaining(WaveSave w)
    {
        return w.stayRemaining >= 0f ? w.stayRemaining : 0f;
    }

    // Cooldown po vlne: beží podľa času servera aj keď je hra zatvorená (ako stavby budov).
    // Ak čas servera nie je známy (offline), použije sa uložený zostávajúci čas.
    static float CooldownRemaining(WaveSave w, double now)
    {
        if (w.cooldownEndUnix >= 0.0 && now >= 0.0) return (float)(w.cooldownEndUnix - now);
        return w.cooldownRemaining >= 0f ? w.cooldownRemaining : 0f;
    }

    // ------------------------------------------------------------------
    // Uloženie
    // ------------------------------------------------------------------

    void LateUpdate()
    {
        if (!IsReady || suppressed) return;

        autosaveTimer += Time.unscaledDeltaTime;
        if (autosaveTimer >= AutosaveInterval) dirty = true;

        if (dirty) Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveNowIfReady();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) SaveNowIfReady();
    }

    void OnApplicationQuit()
    {
        SaveNowIfReady();
    }

    void SaveNowIfReady()
    {
        if (IsReady && !suppressed) Save();
    }

    public void Save()
    {
        dirty = false;
        autosaveTimer = 0f;
        if (suppressed) return;

        try
        {
            var data = Capture();
            string json = JsonUtility.ToJson(data, true);
            string path = SavePath;
            string tmp = path + ".tmp";

            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("FR-39 | Hru sa nepodarilo uložiť: " + e.Message);
        }
    }

    SaveData Capture()
    {
        if (upgrades == null) FindSystems();

        var data = new SaveData();
        data.savedAtUnix = ServerClock.IsSynced ? ServerClock.NowUnix : -1.0;
        double now = data.savedAtUnix;

        var res = ResortResources.Instance;
        if (res != null)
        {
            data.resources.coins = res.Coins;
            data.resources.gems = res.Gems;
            data.resources.energy = res.Energy;
            data.resources.resortValue = res.ResortValue;
        }

        if (upgrades != null) upgrades.CaptureSave(data.buildings);
        foreach (var spot in FindObjectsByType<BuildSpot>(FindObjectsSortMode.None))
            data.buildSpots.Add(spot.CaptureSave());

        if (staff != null) data.staff.totalStaff = staff.TotalStaff;

        var w = data.wave;
        if (waves != null) waves.CaptureSave(w);

        if (w.active && stayTimer != null && stayTimer.IsRunning)
        {
            float remaining = Mathf.Max(0f, stayTimer.RemainingTime);
            if (stayTimer.IsCooldown)
            {
                w.cooldownRemaining = remaining;
                w.cooldownEndUnix = now >= 0.0 ? now + remaining : -1.0;
            }
            else
            {
                w.stayRemaining = remaining;
                w.stayEndUnix = now >= 0.0 ? now + remaining : -1.0;
            }
        }

        if (rewards != null) rewards.CaptureSave(w);
        if (services != null) w.ratings = services.CaptureRatings();

        if (VipInviteManager.Instance != null) VipInviteManager.Instance.CaptureSave(data.vip);

        return data;
    }

    // ------------------------------------------------------------------
    // Udalosti, po ktorých sa ukladá
    // ------------------------------------------------------------------

    void Subscribe(bool on)
    {
        if (on)
        {
            subscribedResources = ResortResources.Instance;
            if (subscribedResources != null) subscribedResources.Changed += OnAnyChange;
            subscribedVip = VipInviteManager.Instance;
            if (subscribedVip != null) subscribedVip.Changed += OnAnyChange;
            if (staff != null) staff.OnStaffChanged += OnAnyChange;
            if (stayTimer != null)
            {
                stayTimer.TimerFinished += OnAnyChange;
                stayTimer.CooldownFinished += OnAnyChange;
            }
            if (rewards != null) rewards.RewardsBecameReady += OnAnyChange;
            if (waves != null)
            {
                waves.WaveStarted += OnAnyChange;
                waves.WaveFinished += OnAnyChange;
                waves.GuestArrived += OnAnyChange;
                waves.DepartureFinished += OnAnyChange;
                waves.WaveCycleCompleted += OnAnyChange;
                waves.WaveReadyForDecision += OnGuestsChange;
                waves.WaveAccepted += OnGuestsChange;
                waves.WaveRejected += OnGuestsChange;
                waves.GuestStayFinished += OnGuestsChange;
            }
        }
        else
        {
            if (subscribedResources != null) subscribedResources.Changed -= OnAnyChange;
            subscribedResources = null;
            if (subscribedVip != null) subscribedVip.Changed -= OnAnyChange;
            subscribedVip = null;
            if (staff != null) staff.OnStaffChanged -= OnAnyChange;
            if (stayTimer != null)
            {
                stayTimer.TimerFinished -= OnAnyChange;
                stayTimer.CooldownFinished -= OnAnyChange;
            }
            if (rewards != null) rewards.RewardsBecameReady -= OnAnyChange;
            if (waves != null)
            {
                waves.WaveStarted -= OnAnyChange;
                waves.WaveFinished -= OnAnyChange;
                waves.GuestArrived -= OnAnyChange;
                waves.DepartureFinished -= OnAnyChange;
                waves.WaveCycleCompleted -= OnAnyChange;
                waves.WaveReadyForDecision -= OnGuestsChange;
                waves.WaveAccepted -= OnGuestsChange;
                waves.WaveRejected -= OnGuestsChange;
                waves.GuestStayFinished -= OnGuestsChange;
            }
        }
    }

    void OnAnyChange() => dirty = true;
    void OnGuestsChange(IReadOnlyList<GameObject> guests) => dirty = true;

    void OnDestroy()
    {
        // Pozn.: tu sa už neukladá - pri zatváraní scény môžu byť budovy už zničené
        // (uloženie je v OnApplicationQuit / OnApplicationPause a po každej akcii).
        Subscribe(false);
        if (Instance == this) Instance = null;
    }
}
