
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Vlna hostí: po stlačení START WAVE pripláva k mólu loď, hostia vystúpia a prejdú do hotela.
// FR-11: loď a počet hostí závisia od úrovne celého rezortu.
// Úroveň rezortu = najnižšia úroveň hotela, reštaurácie a baru (max. Level 3 pri súčasných budovách).
//
// Trasy sú poskladané z detí objektov v scéne (dajú sa posúvať v editore, v Scene view sú vidieť ako čiary):
//   boatPath      - body, cez ktoré loď pripláva (posledný bod má byť pred kotviskom, v smere príchodu)
//   dock          - kotvisko: bod na hrane móla, kde sa zastaví PREDOK lode; modrá os (Z) = smer lode,
//                   loď stojí bokom k mólu (pierOnRightSide = mólo je po pravej ruke lode)
//   boatLeavePath - prvý bod loď dosiahne cúvaním, potom odpláva cez ostatné body
//   guestPath     - cesta hostí z móla na pláž; posledné kroky k dverám sa rátajú od aktuálneho hotela
//                   (body sú vybrané tak, aby hostia obišli kamene, palmy aj stĺpiky móla)
// Odchod hostí z ostrova je v súbore GuestWaveManager.Departure.cs (partial trieda).
public partial class GuestWaveManager : MonoBehaviour
{
    [Serializable]
    public class BoatLevel
    {
        public string label = "Boat L1";
        public GameObject model;

        [Tooltip("Koľko hostí loď privezie")]
        public int guestCount = 3;

        [Tooltip("Výška paluby nad hladinou")]
        public float deckHeight = 0.15f;
    }

    [Serializable]
    public class GuestModel
    {
        public GameObject model;

        [Tooltip("Rigs/<Meno>.rig.txt - polohy kĺbov pre animáciu")]
        public TextAsset rig;
    }

    [Header("Lode")]
    [SerializeField] BoatLevel[] boats;
    // Staré pole ponechávame pre kompatibilitu s GuestArrivalSetup a existujúcou scénou.
    // FR-11 ho už nepoužíva; namiesto neho platí poradie lodí L1, L2, L3 v poli Boats.
    [SerializeField, HideInInspector] int[] boatForHotelLevel = { 0, 1, 1, 2 };
    [SerializeField] float boatSpeed = 4f;
    [SerializeField] float boatTurnSpeed = 70f;
    [SerializeField] float boatYawOffset = 90f;
    [SerializeField] float dockGap = 0.08f;
    [SerializeField] bool pierOnRightSide = true;

    [Header("Hostia")]
    [SerializeField] GuestModel[] guestModels;
    [SerializeField] float guestScale = 0.5f;
    [SerializeField] float guestSpeed = 1.1f;
    [SerializeField] float guestSpawnInterval = 0.7f;

    [Header("Pobyt hostí")]
    [SerializeField] float minGuestStaySeconds = 60f;
    [SerializeField] float maxGuestStaySeconds = 120f;
    [SerializeField] float hotelGuestSpacing = 0.3f;

    [Header("Trasy")]
    [SerializeField] Transform boatPath;
    [SerializeField] Transform dock;
    [SerializeField] Transform boatLeavePath;
    [SerializeField] Transform guestPath;

    [Header("Hotel")]
    [SerializeField] string hotelLabel = "Hotel";
    [SerializeField] Vector3 hotelEntryLocal = new Vector3(0f, 0.1f, 2.15f);
    [SerializeField] Vector3 hotelFrontLocal = new Vector3(0f, 0.1f, 2.0f);
    [SerializeField] Vector3 hotelDoorLocal = new Vector3(0f, 0.1f, 1.7f);

    // ----------------------------------------------------------------
    // UDALOSTI
    // ----------------------------------------------------------------

    public event Action GuestArrived;
    public event Action WaveFinished;

    // Vyvolá sa až po príchode všetkých hostí aktuálnej vlny.
    public event Action<IReadOnlyList<GameObject>> WaveReadyForDecision;

    // Rozhodnutia hráča a koniec pobytu. Filipova animácia sa môže
    // pripojiť na tieto udalosti bez zásahu do pohybu hostí.
    public event Action<IReadOnlyList<GameObject>> WaveAccepted;
    public event Action<IReadOnlyList<GameObject>> WaveRejected;
    public event Action<IReadOnlyList<GameObject>> GuestStayFinished;

    // ----------------------------------------------------------------
    // STAV VLNY
    // ----------------------------------------------------------------

    public bool WaveRunning { get; private set; }
    public int WaveNumber { get; private set; }

    private readonly List<GameObject> currentWaveGuests =
        new List<GameObject>();

    public IReadOnlyList<GameObject> CurrentWaveGuests =>
        currentWaveGuests;

    public int ExpectedGuests { get; private set; }
    public int ArrivedGuests { get; private set; }

    public bool WaveSpawningComplete { get; private set; }
    public bool IsWaveReadyForDecision { get; private set; }
    public bool WaveDecisionMade { get; private set; }
    public bool IsWaveAccepted { get; private set; }

    // Ďalšia vlna sa odomkne až po cooldown-e aj po dokončení odchodu hostí.
    private bool departureAnimationComplete;
    private bool waveCooldownComplete;

    BuildingUpgradeManager upgrades;
    Button startButton;

    void OnEnable()
    {
        StartWaveButton.WaveRequested += StartWave;
    }

    void OnDisable()
    {
        StartWaveButton.WaveRequested -= StartWave;
    }

    void Start()
    {
        upgrades = FindAnyObjectByType<BuildingUpgradeManager>();

        var swb = FindAnyObjectByType<StartWaveButton>();

        if (swb != null)
            startButton = swb.GetComponent<Button>();
    }

    // ----------------------------------------------------------------
    // SPUSTENIE VLNY
    // ----------------------------------------------------------------

    public void StartWave()
    {
        if (WaveRunning)
        {
            Debug.Log("Vlna už prebieha, počkaj, kým loď odpláva.");
            return;
        }

        if (currentWaveGuests.Count > 0)
        {
            Debug.LogWarning(
                "Pred spustením ďalšej vlny musí byť vyriešená aktuálna vlna hostí."
            );
            return;
        }

        // FR-11: úroveň rezortu odvodíme od všetkých troch budov.
        // Opakovaná vlna na rovnakej úrovni prinesie rovnaký počet hostí.
        int resortLevel = GetResortLevel();
        int boatIndex = Mathf.Clamp(resortLevel - 1, 0, boats.Length - 1);
        if (boats[boatIndex].model == null)
        {
            Debug.LogWarning(
                "GuestWaveManager: " +
                boats[boatIndex].label +
                " nemá priradený model."
            );
            return;
        }

        // Príprava evidencie novej vlny.
        currentWaveGuests.Clear();

        ExpectedGuests = 0;
        ArrivedGuests = 0;

        WaveSpawningComplete = false;
        IsWaveReadyForDecision = false;
        WaveDecisionMade = false;
        IsWaveAccepted = false;

        WaveNumber++;
        Debug.Log("FR-11 | Vlna " + WaveNumber + ": rezort Level " + resortLevel + " -> " + boats[boatIndex].label +
                  " (" + boats[boatIndex].guestCount + " hostí)");
        currentWaveBoat = boats[boatIndex]; // rovnaká loď príde aj po hostí pri odchode
        StartCoroutine(RunWave(boats[boatIndex]));
    }

    // Úroveň rezortu je najnižšia zo súčasných úrovní hotela, reštaurácie a baru.
    // Ak niektorá budova/manažér chýba, zatiaľ použijeme Level 1.
    public int GetResortLevel()
    {
        if (upgrades == null)
            upgrades = FindAnyObjectByType<BuildingUpgradeManager>();

        if (upgrades == null)
        {
            Debug.LogWarning("FR-11: BuildingUpgradeManager sa nenašiel; používam rezort Level 1.");
            return 1;
        }

        int hotel = upgrades.GetLevel(hotelLabel);
        int restaurant = upgrades.GetLevel("Restaurant");
        int bar = upgrades.GetLevel("Bar");

        if (hotel < 1 || restaurant < 1 || bar < 1)
        {
            Debug.LogWarning("FR-11: Hotel/Restaurant/Bar sa nenašiel; používam rezort Level 1.");
            return 1;
        }

        return Mathf.Min(hotel, restaurant, bar);
    }

    // ---------------------------------------------------------------- loď

    IEnumerator RunWave(BoatLevel level)
    {
        WaveRunning = true;

        if (startButton != null)
            startButton.interactable = false;

        var boat = new GameObject(
            "Boat (" + level.label + ")"
        ).transform;

        var model = Instantiate(level.model, boat, false);

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation =
            Quaternion.Euler(0f, boatYawOffset, 0f);

        float halfLength = 1f;
        float halfBeam = 0.4f;

        var mf = model.GetComponentInChildren<MeshFilter>();

        if (mf != null && mf.sharedMesh != null)
        {
            var ext = mf.sharedMesh.bounds.extents;
            halfLength = ext.x;
            halfBeam = ext.z;
        }

        Vector3 awayFromPier =
            Flat(dock.right) *
            (pierOnRightSide ? -1f : 1f);

        Vector3 dockPos =
            dock.position
            - Flat(dock.forward) * halfLength
            + awayFromPier * (halfBeam + dockGap);

        var arrive = Children(boatPath);
        arrive.Add(dockPos);

        boat.position = arrive[0];

        if (arrive.Count > 1)
        {
            boat.rotation = Quaternion.LookRotation(
                Flat(arrive[1] - arrive[0])
            );
        }

        var bob = new BobState
        {
            model = model.transform
        };

        yield return Sail(boat, bob, arrive, false, true);

        // Zarovnanie lode s mólom.
        var dockRot = Quaternion.LookRotation(Flat(dock.forward));

        while (Quaternion.Angle(boat.rotation, dockRot) > 0.5f)
        {
            boat.rotation = Quaternion.RotateTowards(
                boat.rotation,
                dockRot,
                boatTurnSpeed * Time.deltaTime
            );

            Bob(bob);
            yield return null;
        }

        // Výpočet miesta vystupovania.
        var walk = Children(guestPath);

        Vector3 target = walk.Count > 0
            ? walk[0]
            : dock.position;

        Vector3 f = boat.forward;
        Vector3 toTarget = target - boat.position;

        float along = Mathf.Clamp(
            Vector3.Dot(toTarget, f),
            -(halfLength - 0.35f),
            halfLength - 0.35f
        );

        Vector3 side = Flat(
            Vector3.ProjectOnPlane(toTarget, f)
        );

        Vector3 exit =
            boat.position
            + f * along
            + side * (halfBeam * 0.45f)
            + Vector3.up * level.deckHeight;

        // Postupné vytváranie hostí.
        for (int i = 0; i < level.guestCount; i++)
        {
            GameObject guest = SpawnGuest(exit, walk);

            if (guest != null)
            {
                currentWaveGuests.Add(guest);
                ExpectedGuests++;
            }

            float t = 0f;

            while (t < guestSpawnInterval)
            {
                t += Time.deltaTime;
                Bob(bob);
                yield return null;
            }
        }

        // Loď ešte chvíľu čaká.
        float wait = 0f;

        while (wait < 1.2f)
        {
            wait += Time.deltaTime;
            Bob(bob);
            yield return null;
        }

        // Odchod lode.
        var leave = Children(boatLeavePath);

        if (leave.Count > 0)
        {
            yield return Sail(
                boat,
                bob,
                new List<Vector3> { boat.position, leave[0] },
                true,
                true
            );

            if (leave.Count > 1)
            {
                leave[0] = boat.position;

                yield return Sail(
                    boat,
                    bob,
                    leave,
                    false,
                    false
                );
            }
        }

        Destroy(boat.gameObject);

        // Tlačidlo zostáva vypnuté až do dokončenia pobytu/odmietnutia,
        // cooldownu a odchodovej animácie hostí.
        WaveRunning = false;

        // Loď už dokončila vysádzanie.
        WaveSpawningComplete = true;

        Debug.Log(
            "Loď odplávala. Vysadených hostí: " + ExpectedGuests
        );

        WaveFinished?.Invoke();

        // Ak už všetci hostia dorazili, môžeme pripraviť rozhodnutie.
        CheckWaveReady();
    }

    // ----------------------------------------------------------------
    // LOĎ - POHYB
    // ----------------------------------------------------------------

    class BobState
    {
        public Transform model;
        public float time;
    }

    void Bob(BobState s)
    {
        s.time += Time.deltaTime;

        s.model.localPosition = new Vector3(
            0f,
            Mathf.Sin(s.time * 1.7f) * 0.03f,
            0f
        );

        s.model.localRotation = Quaternion.Euler(
            Mathf.Sin(s.time * 1.3f) * 1.5f,
            boatYawOffset,
            Mathf.Sin(s.time * 1.1f) * 2f
        );
    }

    IEnumerator Sail(
        Transform boat,
        BobState bob,
        List<Vector3> points,
        bool reverse,
        bool slowDown
    )
    {
        float total = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            total += Vector3.Distance(
                points[i - 1],
                points[i]
            );
        }

        float travelled = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = points[i - 1];
            Vector3 b = points[i];

            float len = Vector3.Distance(a, b);
            float d = 0f;

            Vector3 dir = Flat(b - a);

            if (reverse)
                dir = -dir;

            while (d < len)
            {
                float remaining = total - travelled - d;

                float speed = reverse
                    ? boatSpeed * 0.35f
                    : boatSpeed;

                if (slowDown)
                    speed *= Mathf.Clamp(
                        remaining / 3f,
                        0.2f,
                        1f
                    );

                if (reverse)
                {
                    speed *= Mathf.Clamp(
                        (travelled + d) / 1f + 0.3f,
                        0.3f,
                        1f
                    );
                }

                d = Mathf.Min(
                    len,
                    d + speed * Time.deltaTime
                );

                boat.position = Vector3.Lerp(
                    a,
                    b,
                    d / len
                );

                if (dir.sqrMagnitude > 0.0001f)
                {
                    boat.rotation = Quaternion.RotateTowards(
                        boat.rotation,
                        Quaternion.LookRotation(dir),
                        boatTurnSpeed * Time.deltaTime
                    );
                }

                Bob(bob);
                yield return null;
            }

            travelled += len;
        }
    }

    // ----------------------------------------------------------------
    // VYTVORENIE HOSŤA
    // ----------------------------------------------------------------

    GameObject SpawnGuest(Vector3 start, List<Vector3> path)
    {
        if (guestModels == null || guestModels.Length == 0)
            return null;

        var gm = guestModels[
            UnityEngine.Random.Range(0, guestModels.Length)
        ];

        if (gm == null || gm.model == null)
            return null;

        var guest = BuildGuest(gm);

        guest.transform.position = start;

        if (path.Count > 0)
        {
            var dir = Flat(path[0] - start);

            if (dir.sqrMagnitude > 0.0001f)
            {
                guest.transform.rotation =
                    Quaternion.LookRotation(dir);
            }
        }

        guest.SetActive(true);

        StartCoroutine(WalkGuest(guest, start, path));

        return guest;
    }

    GameObject BuildGuest(GuestModel gm)
    {
        var root = new GameObject(
            "Guest_" + gm.model.name
        );

        root.SetActive(false);

        var model = Instantiate(
            gm.model,
            root.transform,
            false
        );

        model.name = "Model";

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        var anim = root.AddComponent<CharacterAnimator>();

        anim.body = model.transform;
        anim.speed = 1.3f;

        // Každý hosť má vlastnú evidenciu potrieb.
        root.AddComponent<GuestNeeds>();
        root.AddComponent<GuestNeedsGenerator>();

        if (gm.rig != null)
        {
            foreach (var raw in gm.rig.text.Split('\n'))
            {
                var p = raw.Trim().Split(' ');

                if (p.Length < 4)
                    continue;

                if (!float.TryParse(
                    p[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float x))
                    continue;

                if (!float.TryParse(
                    p[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float y))
                    continue;

                if (!float.TryParse(
                    p[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float z))
                    continue;

                var part = FindDeep(model.transform, p[0]);

                if (part == null)
                    continue;

                var pivot = new GameObject(
                    p[0] + "_Pivot"
                ).transform;

                pivot.SetParent(model.transform, false);
                pivot.localPosition = new Vector3(-x, y, z);

                part.SetParent(pivot, true);

                switch (p[0])
                {
                    case "Head":
                        anim.head = pivot;
                        break;

                    case "ArmL":
                        anim.armL = pivot;
                        break;

                    case "ArmR":
                        anim.armR = pivot;
                        break;

                    case "LegL":
                        anim.legL = pivot;
                        break;

                    case "LegR":
                        anim.legR = pivot;
                        break;
                }
            }
        }

        anim.state = CharState.Walk;

        root.transform.localScale =
            Vector3.one * guestScale;

        return root;
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name || c.name.EndsWith("_" + name))
                return c;

            var r = FindDeep(c, name);

            if (r != null)
                return r;
        }

        return null;
    }

    // ----------------------------------------------------------------
    // POHYB HOSŤA A UBYTOVANIE
    // ----------------------------------------------------------------

    IEnumerator WalkGuest(
        GameObject guest,
        Vector3 start,
        List<Vector3> path
    )
    {
        var anim = guest.GetComponent<CharacterAnimator>();

        var needsGenerator =
            guest.GetComponent<GuestNeedsGenerator>();

        float offset = UnityEngine.Random.Range(-0.08f, 0.08f);

        float speed =
            guestSpeed * UnityEngine.Random.Range(0.9f, 1.1f);

        var points = new List<Vector3> { start };

        for (int i = 0; i < path.Count; i++)
        {
            points.Add(Offset(path, i, offset));
        }

        for (int i = 1; i < points.Count; i++)
        {
            yield return WalkSegment(
                guest.transform,
                points[i - 1],
                points[i],
                speed
            );
        }

        var hotel = upgrades != null
            ? upgrades.GetBuilding(hotelLabel)
            : null;

        if (hotel != null)
        {
            var h = hotel.transform;

            Vector3 from = guest.transform.position;
            Vector3 entry = h.TransformPoint(hotelEntryLocal);
            Vector3 front = h.TransformPoint(hotelFrontLocal);
            Vector3 door = h.TransformPoint(hotelDoorLocal);

            yield return WalkSegment(
                guest.transform,
                from,
                entry,
                speed
            );

            yield return WalkSegment(
                guest.transform,
                entry,
                front,
                speed
            );

            // Hosť vojde do dverí, zmenší sa a zmizne (ako predtým).
            // Objekt hosťa ostáva aktívny (len neviditeľný), aby fungovali
            // jeho potreby, panel ACCEPT/REJECT a neskôr odchod z ostrova.
            yield return WalkScaled(
                guest.transform,
                front,
                door,
                speed,
                guestScale,
                0f
            );

            HideGuest(guest);

            if (anim != null)
                anim.state = CharState.Idle;

            if (needsGenerator != null)
                needsGenerator.StartGenerating();

            Debug.Log(
                guest.name + " je ubytovaný v rezorte."
            );
        }
        else
        {
            Debug.LogWarning(
                "GuestWaveManager: Hotel sa nenašiel. Hosť zostáva na poslednom bode trasy."
            );

            if (anim != null)
                anim.state = CharState.Happy;
        }

        // Označíme hosťa ako dorazeného.
        RegisterGuestArrival();
    }

    // ----------------------------------------------------------------
    // EVIDENCIA PRÍCHODU HOSTÍ
    // ----------------------------------------------------------------

    void RegisterGuestArrival()
    {
        ArrivedGuests++;

        Debug.Log(
            "Dorazili hostia: " +
            ArrivedGuests +
            " / " +
            ExpectedGuests
        );

        GuestArrived?.Invoke();

        CheckWaveReady();
    }

    void CheckWaveReady()
    {
        if (IsWaveReadyForDecision)
            return;

        if (!WaveSpawningComplete)
            return;

        if (ExpectedGuests <= 0)
        {
            Debug.LogWarning(
                "Vlna neobsahuje žiadnych vytvorených hostí."
            );
            return;
        }

        if (ArrivedGuests < ExpectedGuests)
            return;

        IsWaveReadyForDecision = true;

        Debug.Log(
            "VLNA PRIPRAVENÁ NA ROZHODNUTIE! " +
            ArrivedGuests +
            " hostí čaká na prijatie alebo odmietnutie."
        );

        WaveReadyForDecision?.Invoke(CurrentWaveGuests);
    }

    // ----------------------------------------------------------------
    // ROZHODNUTIE O VLNE A UKONČENIE POBYTU
    // ----------------------------------------------------------------

    // Zavolá UI po kliknutí na ACCEPT ALL.
    public bool AcceptCurrentWave()
    {
        if (!IsWaveReadyForDecision || WaveDecisionMade ||
            currentWaveGuests.Count == 0)
        {
            Debug.LogWarning("Aktuálnu vlnu nie je možné prijať.");
            return false;
        }

        WaveDecisionMade = true;
        IsWaveAccepted = true;

        Debug.Log("Hráč prijal celú vlnu hostí.");
        WaveAccepted?.Invoke(CurrentWaveGuests);
        return true;
    }

    // Zavolá UI po kliknutí na REJECT ALL. Hostí zámerne nemažeme
    // ani neteleportujeme; ich odchod zabezpečí samostatná animácia.
    public bool RejectCurrentWave()
    {
        if (!IsWaveReadyForDecision || WaveDecisionMade ||
            currentWaveGuests.Count == 0)
        {
            Debug.LogWarning("Aktuálnu vlnu nie je možné odmietnuť.");
            return false;
        }

        WaveDecisionMade = true;
        IsWaveAccepted = false;

        Debug.Log("Hráč odmietol celú vlnu hostí.");
        WaveRejected?.Invoke(CurrentWaveGuests);

        // Odmietnutí hostia hneď odchádzajú z hotela na loď.
        StartDeparture();
        return true;
    }

    // GuestWaveUI zavolá túto metódu po skončení časovača pobytu.
    // Filipov skript sa môže prihlásiť na GuestStayFinished a spustiť odchod.
    public void NotifyGuestStayFinished()
    {
        if (!WaveDecisionMade || !IsWaveAccepted)
        {
            Debug.LogWarning(
                "Koniec pobytu možno vyvolať iba pri prijatej vlne."
            );
            return;
        }

        Debug.Log("Čas pobytu hostí sa skončil.");
        GuestStayFinished?.Invoke(CurrentWaveGuests);

        // Koniec pobytu: hostia odchádzajú z hotela na loď.
        StartDeparture();
    }

    // Filip zavolá túto metódu až po dokončení odchodovej animácie.
    // Samotné dokončenie animácie ešte neodomkne ďalšiu vlnu,
    // pokiaľ zároveň neskončil dvojminútový cooldown.
    public void CompleteCurrentWave()
    {
        if (!WaveDecisionMade)
        {
            Debug.LogWarning(
                "Vlna sa nedá dokončiť bez rozhodnutia hráča."
            );
            return;
        }

        departureAnimationComplete = true;
        TryCompleteWaveCycle();
    }

    // GuestWaveUI zavolá túto metódu po skončení dvojminútového cooldownu.
    public void CompleteWaveCooldown()
    {
        if (!WaveDecisionMade)
        {
            Debug.LogWarning(
                "Cooldown nemožno dokončiť bez rozhodnutia o vlne."
            );
            return;
        }

        waveCooldownComplete = true;
        TryCompleteWaveCycle();
    }

    private void TryCompleteWaveCycle()
    {
        if (!departureAnimationComplete || !waveCooldownComplete)
            return;

        currentWaveGuests.Clear();
        ExpectedGuests = 0;
        ArrivedGuests = 0;
        WaveSpawningComplete = false;
        IsWaveReadyForDecision = false;
        WaveDecisionMade = false;
        IsWaveAccepted = false;
        departureAnimationComplete = false;
        waveCooldownComplete = false;

        if (startButton != null)
            startButton.interactable = true;

        Debug.Log(
            "Vlna aj cooldown sú dokončené. Je možné spustiť ďalšiu vlnu."
        );
    }

    // ----------------------------------------------------------------
    // POHYB A POMOCNÉ METÓDY
    // ----------------------------------------------------------------

    IEnumerator WalkSegment(
        Transform guest,
        Vector3 a,
        Vector3 b,
        float speed
    )
    {
        float len = Vector3.Distance(a, b);

        if (len < 0.001f)
            yield break;

        Face(guest, b - a);

        float d = 0f;

        while (d < len)
        {
            d = Mathf.Min(
                len,
                d + speed * Time.deltaTime
            );

            guest.position = Vector3.Lerp(
                a,
                b,
                d / len
            );

            yield return null;
        }
    }

    static void Face(Transform t, Vector3 dir)
    {
        dir = Flat(dir);

        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir);
    }

    static Vector3 Offset(
        List<Vector3> path,
        int i,
        float amount
    )
    {
        if (path.Count < 2)
            return path[i];

        Vector3 prev = path[Mathf.Max(0, i - 1)];

        Vector3 next = path[
            Mathf.Min(path.Count - 1, i + 1)
        ];

        Vector3 dir = Flat(next - prev);

        if (dir.sqrMagnitude < 0.0001f)
            return path[i];

        Vector3 side = new Vector3(
            dir.z,
            0f,
            -dir.x
        );

        return path[i] + side * amount;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;

        return v.sqrMagnitude > 0.000001f
            ? v.normalized
            : Vector3.zero;
    }

    static List<Vector3> Children(Transform parent)
    {
        var list = new List<Vector3>();

        if (parent == null)
            return list;

        foreach (Transform c in parent)
            list.Add(c.position);

        return list;
    }

    // ----------------------------------------------------------------
    // GIZMOS
    // ----------------------------------------------------------------

    void OnDrawGizmos()
    {
        DrawPath(
            boatPath,
            new Color(0.2f, 0.6f, 1f),
            dock
        );

        DrawPath(
            boatLeavePath,
            new Color(1f, 0.55f, 0.1f),
            null
        );

        DrawPath(
            guestPath,
            new Color(0.25f, 0.9f, 0.3f),
            null
        );

        if (dock != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                dock.position,
                0.3f
            );

            Gizmos.DrawLine(
                dock.position,
                dock.position + dock.forward * 1.5f
            );

            Gizmos.DrawLine(
                dock.position,
                dock.position - dock.forward * 2.5f
            );

            Gizmos.color = Color.yellow;

            Gizmos.DrawLine(
                dock.position,
                dock.position + dock.right *
                (pierOnRightSide ? -1f : 1f)
            );
        }
    }

    static void DrawPath(
        Transform parent,
        Color color,
        Transform end
    )
    {
        if (parent == null)
            return;

        Gizmos.color = color;

        Vector3? prev = null;

        foreach (Transform c in parent)
        {
            Gizmos.DrawSphere(c.position, 0.12f);

            if (prev.HasValue)
                Gizmos.DrawLine(prev.Value, c.position);

            prev = c.position;
        }

        if (end != null && prev.HasValue)
            Gizmos.DrawLine(prev.Value, end.position);
    }
}
