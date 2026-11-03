using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Odchod hostí z ostrova (koniec pobytu alebo odmietnutá vlna):
//   1. po hostí pripláva loď (trasa DepartBoatPath) a zastaví pri móle (Dock),
//   2. hostia vyjdú z hotela a idú na mólo (trasa DepartGuestPath),
//      na poslednom bode trasy počkajú, kým loď nepristane,
//   3. nastúpia na loď (zmenšia sa a zmiznú),
//   4. loď cúvne od móla a odpláva preč z ostrova (trasa DepartBoatLeavePath),
//   5. zavolá sa CompleteCurrentWave() -> po cooldowne sa odomkne ďalšia vlna.
public partial class GuestWaveManager
{
    [Header("Odchod hostí (deti týchto objektov sú body trasy)")]
    [Tooltip("Cesta lode po hostí k mólu (fialová). Ak chýba, použije sa Boat Path.")]
    [SerializeField] Transform departBoatPath;

    [Tooltip("Cesta hostí z hotela na mólo (červená). Posledný bod = miesto, kde čakajú na loď. Ak chýba, použije sa Guest Path odzadu.")]
    [SerializeField] Transform departGuestPath;

    [Tooltip("Cesta lode preč z ostrova po nástupe hostí (oranžová). Prvý bod: loď k nemu cúvne od móla. Ak chýba, použije sa Boat Leave Path.")]
    [SerializeField] Transform departBoatLeavePath;

    [Tooltip("Ak by loď z nejakého dôvodu nemohla nabrať všetkých hostí, po tomto čase (s) odpláva aj tak")]
    [SerializeField] float departBoardingTimeout = 90f;

    public bool DepartureRunning { get; private set; }

    // Udalosť pre ďalšie systémy (napr. štatistiky) - loď s hosťami odplávala.
    public event System.Action DepartureFinished;

    // Loď, ktorá priviezla aktuálnu vlnu (nastavuje StartWave).
    BoatLevel currentWaveBoat;

    // Stav lode počas odchodu
    bool departBoatDocked;
    bool departBoatGone;
    Vector3 departBoardPoint;
    int departGuestsTotal;
    int departGuestsBoarded;

    void StartDeparture()
    {
        if (DepartureRunning)
            return;

        StartCoroutine(RunDeparture());
    }

    IEnumerator RunDeparture()
    {
        DepartureRunning = true;

        var guests = new List<GameObject>();
        foreach (var g in currentWaveGuests)
            if (g != null) guests.Add(g);

        BoatLevel level = currentWaveBoat;
        if ((level == null || level.model == null) && boats != null && boats.Length > 0)
            level = boats[0];

        if (level == null || level.model == null || dock == null)
        {
            Debug.LogWarning("GuestWaveManager: chýba loď alebo Dock pre odchod hostí, hostia zmiznú bez animácie.");
            foreach (var g in guests) Destroy(g);
            FinishDeparture();
            yield break;
        }

        departBoatDocked = false;
        departBoatGone = false;
        departGuestsTotal = guests.Count;
        departGuestsBoarded = 0;

        // Loď vyráža hneď, hostia vychádzajú z hotela súčasne.
        var boatRoutine = StartCoroutine(DepartureBoat(level));

        var walk = DepartGuestPoints();

        foreach (var g in guests)
        {
            StartCoroutine(GuestLeave(g, walk));

            float t = 0f;
            while (t < guestSpawnInterval) { t += Time.deltaTime; yield return null; }
        }

        yield return boatRoutine;

        FinishDeparture();
    }

    void FinishDeparture()
    {
        DepartureRunning = false;
        DepartureFinished?.Invoke();

        Debug.Log("Hostia odplávali z ostrova.");

        // Odchodová animácia je hotová; ďalšia vlna sa odomkne po skončení cooldownu.
        CompleteCurrentWave();
    }

    // ----------------------------------------------------------------
    // LOĎ
    // ----------------------------------------------------------------

    IEnumerator DepartureBoat(BoatLevel level)
    {
        var boat = new GameObject("Boat (" + level.label + ", odchod)").transform;
        var model = Instantiate(level.model, boat, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, boatYawOffset, 0f);

        float halfLength = 1f, halfBeam = 0.4f;
        var mf = model.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var ext = mf.sharedMesh.bounds.extents;
            halfLength = ext.x;
            halfBeam = ext.z;
        }

        // Rovnaké kotvisko ako pri príchode (žltá čiara na obrázku).
        Vector3 awayFromPier = Flat(dock.right) * (pierOnRightSide ? -1f : 1f);
        Vector3 dockPos = dock.position - Flat(dock.forward) * halfLength + awayFromPier * (halfBeam + dockGap);

        var arrive = Children(departBoatPath != null ? departBoatPath : boatPath);
        arrive.Add(dockPos);

        boat.position = arrive[0];
        if (arrive.Count > 1)
            boat.rotation = Quaternion.LookRotation(Flat(arrive[1] - arrive[0]));

        var bob = new BobState { model = model.transform };

        yield return Sail(boat, bob, arrive, false, true);

        var dockRot = Quaternion.LookRotation(Flat(dock.forward));
        while (Quaternion.Angle(boat.rotation, dockRot) > 0.5f)
        {
            boat.rotation = Quaternion.RotateTowards(boat.rotation, dockRot, boatTurnSpeed * Time.deltaTime);
            Bob(bob);
            yield return null;
        }

        // Miesto nástupu na palube - na boku lode smerom k čakajúcim hosťom.
        var walk = DepartGuestPoints();
        Vector3 target = walk.Count > 0 ? walk[walk.Count - 1] : dock.position;
        Vector3 f = Flat(boat.forward);
        Vector3 toTarget = target - boat.position;
        float along = Mathf.Clamp(Vector3.Dot(toTarget, f), -(halfLength - 0.35f), halfLength - 0.35f);
        Vector3 side = Flat(Vector3.ProjectOnPlane(toTarget, f));
        departBoardPoint = boat.position + f * along + side * (halfBeam * 0.45f) + Vector3.up * level.deckHeight;
        departBoatDocked = true;

        // Čakanie, kým nastúpia všetci hostia.
        float waited = 0f;
        while (departGuestsBoarded < departGuestsTotal && waited < departBoardingTimeout)
        {
            waited += Time.deltaTime;
            Bob(bob);
            yield return null;
        }

        float wait = 0f;
        while (wait < 1.0f) { wait += Time.deltaTime; Bob(bob); yield return null; }

        departBoatDocked = false;
        departBoatGone = true;

        // Cúvnutie od móla a odchod preč z ostrova (oranžová trasa).
        var leave = Children(departBoatLeavePath != null ? departBoatLeavePath : boatLeavePath);
        if (leave.Count > 0)
        {
            yield return Sail(boat, bob, new List<Vector3> { boat.position, leave[0] }, true, true);

            if (leave.Count > 1)
                yield return Sail(boat, bob, leave, false, false);
        }

        Destroy(boat.gameObject);
    }

    // ----------------------------------------------------------------
    // HOSTIA
    // ----------------------------------------------------------------

    // Cesta z hotela na mólo. Ak nie je zadaná, použije sa príchodová cesta odzadu.
    List<Vector3> DepartGuestPoints()
    {
        if (departGuestPath != null && departGuestPath.childCount > 0)
            return Children(departGuestPath);

        var back = Children(guestPath);
        back.Reverse();
        return back;
    }

    IEnumerator GuestLeave(GameObject guest, List<Vector3> path)
    {
        if (guest == null)
        {
            departGuestsBoarded++;
            yield break;
        }

        var anim = guest.GetComponent<CharacterAnimator>();
        var needsGenerator = guest.GetComponent<GuestNeedsGenerator>();
        if (needsGenerator != null)
            needsGenerator.StopGenerating();

        float speed = guestSpeed * Random.Range(0.9f, 1.1f);
        float offset = Random.Range(-0.08f, 0.08f);

        if (anim != null)
            anim.state = CharState.Walk;

        // Výstup z hotela: dvere -> pred dvere (hosť sa zväčší) -> vstup na podložku.
        var hotel = upgrades != null ? upgrades.GetBuilding(hotelLabel) : null;
        Vector3 pos = guest.transform.position;

        if (hotel != null)
        {
            var h = hotel.transform;
            Vector3 door = h.TransformPoint(hotelDoorLocal);
            Vector3 front = h.TransformPoint(hotelFrontLocal);
            Vector3 entry = h.TransformPoint(hotelEntryLocal);

            yield return WalkScaled(guest.transform, door, front, speed, 0f, guestScale);
            yield return WalkSegment(guest.transform, front, entry, speed);
            pos = entry;
        }
        else
        {
            guest.transform.localScale = Vector3.one * guestScale;
        }

        // Cesta na mólo (posledný bod = guľka, kde sa čaká na loď).
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 next = i == path.Count - 1 ? path[i] : Offset(path, i, offset);
            yield return WalkSegment(guest.transform, pos, next, speed);
            pos = next;
        }

        // Čakajúci hostia sa trochu rozostúpia, aby nestáli v sebe
        // (len kúsok dozadu na móle, nie k stĺpikom zábradlia).
        Vector3 boardFrom = pos;
        if (!departBoatDocked)
        {
            Vector3 back = path.Count > 1 ? Flat(path[path.Count - 2] - pos) : Vector3.zero;
            Vector3 sideways = new Vector3(back.z, 0f, -back.x);
            Vector3 spread =
                back * Random.Range(0f, hotelGuestSpacing * 2f) +
                sideways * Random.Range(-hotelGuestSpacing, hotelGuestSpacing) * 0.4f;
            Vector3 waitPos = pos + spread;
            yield return WalkSegment(guest.transform, pos, waitPos, speed);
            pos = waitPos;

            if (anim != null)
                anim.state = CharState.Waiting;

            while (!departBoatDocked && !departBoatGone)
                yield return null;

            // Loď už odplávala (vypršal čas) - hosť len zmizne.
            if (departBoatGone)
            {
                Destroy(guest);
                yield break;
            }

            if (anim != null)
                anim.state = CharState.Walk;

            // Späť na miesto nástupu (medzi stĺpikmi zábradlia).
            yield return WalkSegment(guest.transform, pos, boardFrom, speed);
            pos = boardFrom;
        }

        // Nástup na loď: hosť prejde na palubu, zmenší sa a zmizne.
        yield return WalkScaled(guest.transform, pos, departBoardPoint, speed, guestScale, 0f);

        departGuestsBoarded++;
        Destroy(guest);
    }

    // Chôdza z bodu a do bodu b so zmenou veľkosti (vchod do hotela, nástup na loď).
    IEnumerator WalkScaled(Transform guest, Vector3 a, Vector3 b, float speed, float fromScale, float toScale)
    {
        float len = Mathf.Max(0.01f, Vector3.Distance(a, b));
        float d = 0f;

        Face(guest, b - a);

        while (d < len)
        {
            d = Mathf.Min(len, d + speed * Time.deltaTime);
            float k = d / len;
            guest.position = Vector3.Lerp(a, b, k);
            guest.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, k);
            yield return null;
        }
    }

    // Hosť je "v hoteli": neviditeľný, ale objekt ostáva aktívny (potreby, UI).
    static void HideGuest(GameObject guest)
    {
        guest.transform.localScale = Vector3.zero;
    }
}
