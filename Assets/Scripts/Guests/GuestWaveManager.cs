using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Vlna hostí: po stlačení START WAVE pripláva k mólu loď, hostia vystúpia a prejdú do hotela.
// Loď sa vyberá podľa úrovne hotela (BuildingUpgradeManager): Level 1 -> loď 1, Level 2-3 -> loď 2, MAX -> loď 3.
//
// Trasy sú poskladané z detí objektov v scéne (dajú sa posúvať v editore, v Scene view sú vidieť ako čiary):
//   boatPath      - body, cez ktoré loď pripláva (posledný bod má byť pred kotviskom, v smere príchodu)
//   dock          - kotvisko: bod na hrane móla, kde sa zastaví PREDOK lode; modrá os (Z) = smer lode,
//                   loď stojí bokom k mólu (pierOnRightSide = mólo je po pravej ruke lode)
//   boatLeavePath - prvý bod loď dosiahne cúvaním, potom odpláva cez ostatné body
//   guestPath     - cesta hostí z móla na pláž; posledné kroky k dverám sa rátajú od aktuálneho hotela
//                   (body sú vybrané tak, aby hostia obišli kamene, palmy aj stĺpiky móla)
public class GuestWaveManager : MonoBehaviour
{
    [Serializable]
    public class BoatLevel
    {
        public string label = "Boat L1";
        public GameObject model;
        [Tooltip("Koľko hostí loď privezie")]
        public int guestCount = 3;
        [Tooltip("Výška paluby nad hladinou (m), odtiaľ hostia vystupujú")]
        public float deckHeight = 0.15f;
    }

    [Serializable]
    public class GuestModel
    {
        public GameObject model;
        [Tooltip("Rigs/<Meno>.rig.txt - polohy kĺbov pre animáciu (nepovinné)")]
        public TextAsset rig;
    }

    [Header("Lode")]
    [SerializeField] BoatLevel[] boats;
    [Tooltip("Ktorá loď (index v Boats) príde pre danú úroveň hotela: prvok 0 = hotel Level 1, prvok 1 = Level 2, ...")]
    [SerializeField] int[] boatForHotelLevel = { 0, 1, 1, 2 };
    [SerializeField] float boatSpeed = 4f;
    [Tooltip("Ako rýchlo sa loď otáča (stupne za sekundu)")]
    [SerializeField] float boatTurnSpeed = 70f;
    [Tooltip("Otočenie modelu lode tak, aby predok mieril dopredu (modely majú predok na -X)")]
    [SerializeField] float boatYawOffset = 90f;
    [Tooltip("Medzera medzi bokom lode a mólom (m)")]
    [SerializeField] float dockGap = 0.08f;
    [Tooltip("Mólo je po pravej strane lode, keď stojí pri kotvisku")]
    [SerializeField] bool pierOnRightSide = true;

    [Header("Hostia")]
    [SerializeField] GuestModel[] guestModels;
    [SerializeField] float guestScale = 0.5f;
    [SerializeField] float guestSpeed = 1.1f;
    [Tooltip("Rozostup medzi vystupujúcimi hosťami (s)")]
    [SerializeField] float guestSpawnInterval = 0.7f;

    [Header("Trasy (deti týchto objektov sú body trasy)")]
    [SerializeField] Transform boatPath;
    [SerializeField] Transform dock;
    [SerializeField] Transform boatLeavePath;
    [SerializeField] Transform guestPath;

    [Header("Hotel")]
    [SerializeField] string hotelLabel = "Hotel";
    [Tooltip("Vstup na podložku hotela, v súradniciach hotela")]
    [SerializeField] Vector3 hotelEntryLocal = new Vector3(0f, 0.1f, 2.15f);
    [Tooltip("Miesto pred dverami (tu sa host začne zmenšovať), v súradniciach hotela")]
    [SerializeField] Vector3 hotelFrontLocal = new Vector3(0f, 0.1f, 2.0f);
    [Tooltip("Dvere, kde host zmizne - pred stenou pri všetkých úrovniach hotela, v súradniciach hotela")]
    [SerializeField] Vector3 hotelDoorLocal = new Vector3(0f, 0.1f, 1.7f);

    public event Action GuestArrived;
    public event Action WaveFinished;

    public bool WaveRunning { get; private set; }
    public int WaveNumber { get; private set; }

    BuildingUpgradeManager upgrades;
    Button startButton;

    void OnEnable() { StartWaveButton.WaveRequested += StartWave; }
    void OnDisable() { StartWaveButton.WaveRequested -= StartWave; }

    void Start()
    {
        upgrades = FindAnyObjectByType<BuildingUpgradeManager>();
        var swb = FindAnyObjectByType<StartWaveButton>();
        if (swb != null) startButton = swb.GetComponent<Button>();
    }

    public void StartWave()
    {
        if (WaveRunning)
        {
            Debug.Log("Vlna už prebieha, počkaj, kým loď odpláva.");
            return;
        }
        if (boats == null || boats.Length == 0 || dock == null || boatPath == null)
        {
            Debug.LogWarning("GuestWaveManager: chýbajú lode alebo trasa (spusti Tools > Tropical Resort > Setup Guest Arrival).");
            return;
        }

        int hotelLevel = upgrades != null ? Mathf.Max(1, upgrades.GetLevel(hotelLabel)) : 1;
        int boatIndex = boatForHotelLevel != null && boatForHotelLevel.Length > 0
            ? boatForHotelLevel[Mathf.Clamp(hotelLevel - 1, 0, boatForHotelLevel.Length - 1)]
            : 0;
        boatIndex = Mathf.Clamp(boatIndex, 0, boats.Length - 1);
        if (boats[boatIndex].model == null)
        {
            Debug.LogWarning("GuestWaveManager: " + boats[boatIndex].label + " nemá priradený model.");
            return;
        }

        WaveNumber++;
        Debug.Log("Vlna " + WaveNumber + ": hotel Level " + hotelLevel + " -> " + boats[boatIndex].label +
                  " (" + boats[boatIndex].guestCount + " hostí)");
        StartCoroutine(RunWave(boats[boatIndex]));
    }

    // ---------------------------------------------------------------- loď

    IEnumerator RunWave(BoatLevel level)
    {
        WaveRunning = true;
        if (startButton != null) startButton.interactable = false;

        var boat = new GameObject("Boat (" + level.label + ")").transform;
        var model = Instantiate(level.model, boat, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, boatYawOffset, 0f);

        // Model má dĺžku v osi X a šírku v osi Z.
        float halfLength = 1f, halfBeam = 0.4f;
        var mf = model.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var ext = mf.sharedMesh.bounds.extents;
            halfLength = ext.x;
            halfBeam = ext.z;
        }

        // Predok sa zastaví na kotvisku, loď stojí bokom k mólu (dlhšia loď siaha ďalej dozadu).
        Vector3 awayFromPier = Flat(dock.right) * (pierOnRightSide ? -1f : 1f);
        Vector3 dockPos = dock.position - Flat(dock.forward) * halfLength + awayFromPier * (halfBeam + dockGap);
        var arrive = Children(boatPath);
        arrive.Add(dockPos);

        boat.position = arrive[0];
        if (arrive.Count > 1) boat.rotation = Quaternion.LookRotation(Flat(arrive[1] - arrive[0]));

        var bob = new BobState { model = model.transform };
        yield return Sail(boat, bob, arrive, false, true);

        // Zarovnať s mólom
        var dockRot = Quaternion.LookRotation(Flat(dock.forward));
        while (Quaternion.Angle(boat.rotation, dockRot) > 0.5f)
        {
            boat.rotation = Quaternion.RotateTowards(boat.rotation, dockRot, boatTurnSpeed * Time.deltaTime);
            Bob(bob);
            yield return null;
        }

        // Vystupovanie hostí
        var walk = Children(guestPath);
        Vector3 target = walk.Count > 0 ? walk[0] : dock.position;
        Vector3 f = boat.forward;
        Vector3 toTarget = target - boat.position;
        float along = Mathf.Clamp(Vector3.Dot(toTarget, f), -(halfLength - 0.35f), halfLength - 0.35f);
        Vector3 side = Flat(Vector3.ProjectOnPlane(toTarget, f));
        Vector3 exit = boat.position + f * along + side * (halfBeam * 0.45f) + Vector3.up * level.deckHeight;

        for (int i = 0; i < level.guestCount; i++)
        {
            SpawnGuest(exit, walk);
            float t = 0f;
            while (t < guestSpawnInterval) { t += Time.deltaTime; Bob(bob); yield return null; }
        }

        float wait = 0f;
        while (wait < 1.2f) { wait += Time.deltaTime; Bob(bob); yield return null; }

        // Odchod: najprv cúvanie, potom dopredu preč z ostrova
        var leave = Children(boatLeavePath);
        if (leave.Count > 0)
        {
            yield return Sail(boat, bob, new List<Vector3> { boat.position, leave[0] }, true, true);
            if (leave.Count > 1)
            {
                leave[0] = boat.position;
                yield return Sail(boat, bob, leave, false, false);
            }
        }

        Destroy(boat.gameObject);
        if (startButton != null) startButton.interactable = true;
        WaveRunning = false;
        WaveFinished?.Invoke(); // loď odplávala (hostia môžu ešte kráčať k hotelu)
    }

    class BobState { public Transform model; public float time; }

    void Bob(BobState s)
    {
        s.time += Time.deltaTime;
        s.model.localPosition = new Vector3(0f, Mathf.Sin(s.time * 1.7f) * 0.03f, 0f);
        s.model.localRotation = Quaternion.Euler(Mathf.Sin(s.time * 1.3f) * 1.5f, boatYawOffset, Mathf.Sin(s.time * 1.1f) * 2f);
    }

    // Plavba po lomenej čiare. reverse = loď ide dozadu (cúva). slowDown = spomaľuje pred posledným bodom.
    IEnumerator Sail(Transform boat, BobState bob, List<Vector3> points, bool reverse, bool slowDown)
    {
        float total = 0f;
        for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
        float travelled = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            float len = Vector3.Distance(a, b);
            float d = 0f;
            Vector3 dir = Flat(b - a);
            if (reverse) dir = -dir;

            while (d < len)
            {
                float remaining = total - travelled - d;
                float speed = reverse ? boatSpeed * 0.35f : boatSpeed;
                if (slowDown) speed *= Mathf.Clamp(remaining / 3f, 0.2f, 1f);
                if (reverse) speed *= Mathf.Clamp((travelled + d) / 1f + 0.3f, 0.3f, 1f);

                d = Mathf.Min(len, d + speed * Time.deltaTime);
                boat.position = Vector3.Lerp(a, b, d / len);
                if (dir.sqrMagnitude > 0.0001f)
                    boat.rotation = Quaternion.RotateTowards(boat.rotation, Quaternion.LookRotation(dir), boatTurnSpeed * Time.deltaTime);
                Bob(bob);
                yield return null;
            }
            travelled += len;
        }
    }

    // ---------------------------------------------------------------- hostia

    void SpawnGuest(Vector3 start, List<Vector3> path)
    {
        if (guestModels == null || guestModels.Length == 0) return;
        var gm = guestModels[UnityEngine.Random.Range(0, guestModels.Length)];
        if (gm == null || gm.model == null) return;

        var guest = BuildGuest(gm);
        guest.transform.position = start;
        if (path.Count > 0)
        {
            var dir = Flat(path[0] - start);
            if (dir.sqrMagnitude > 0.0001f) guest.transform.rotation = Quaternion.LookRotation(dir);
        }
        guest.SetActive(true);
        StartCoroutine(WalkGuest(guest, start, path));
    }

    GameObject BuildGuest(GuestModel gm)
    {
        var root = new GameObject("Guest_" + gm.model.name);
        root.SetActive(false); // CharacterAnimator.Awake beží až po nastavení kĺbov

        var model = Instantiate(gm.model, root.transform, false);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        var anim = root.AddComponent<CharacterAnimator>();
        anim.body = model.transform;
        anim.speed = 1.3f;

        if (gm.rig != null)
        {
            foreach (var raw in gm.rig.text.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length < 4) continue;
                if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                if (!float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) continue;

                var part = FindDeep(model.transform, p[0]);
                if (part == null) continue;

                // Unity pri importe OBJ zrkadlí os X (rovnako ako CharactersSetup)
                var pivot = new GameObject(p[0] + "_Pivot").transform;
                pivot.SetParent(model.transform, false);
                pivot.localPosition = new Vector3(-x, y, z);
                part.SetParent(pivot, true);

                switch (p[0])
                {
                    case "Head": anim.head = pivot; break;
                    case "ArmL": anim.armL = pivot; break;
                    case "ArmR": anim.armR = pivot; break;
                    case "LegL": anim.legL = pivot; break;
                    case "LegR": anim.legR = pivot; break;
                }
            }
        }

        anim.state = CharState.Walk;
        root.transform.localScale = Vector3.one * guestScale;
        return root;
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name || c.name.EndsWith("_" + name)) return c;
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    IEnumerator WalkGuest(GameObject guest, Vector3 start, List<Vector3> path)
    {
        var anim = guest.GetComponent<CharacterAnimator>();
        float offset = UnityEngine.Random.Range(-0.08f, 0.08f); // aby nešli všetci v jednej čiare (malý posun, aby neobtierali stĺpiky)
        float speed = guestSpeed * UnityEngine.Random.Range(0.9f, 1.1f);
        float scale = guest.transform.localScale.x;

        var points = new List<Vector3> { start };
        for (int i = 0; i < path.Count; i++) points.Add(Offset(path, i, offset));

        for (int i = 1; i < points.Count; i++)
            yield return WalkSegment(guest.transform, points[i - 1], points[i], speed);

        // Posledné kroky podľa aktuálneho hotela (po upgrade je to nový model na rovnakom mieste)
        var hotel = upgrades != null ? upgrades.GetBuilding(hotelLabel) : null;
        if (hotel != null)
        {
            var h = hotel.transform;
            Vector3 from = guest.transform.position;
            Vector3 entry = h.TransformPoint(hotelEntryLocal);
            Vector3 front = h.TransformPoint(hotelFrontLocal);
            Vector3 door = h.TransformPoint(hotelDoorLocal);

            yield return WalkSegment(guest.transform, from, entry, speed);
            yield return WalkSegment(guest.transform, entry, front, speed);

            // vojde do dverí a zmenší sa
            float len = Mathf.Max(0.01f, Vector3.Distance(front, door));
            float d = 0f;
            Face(guest.transform, door - front);
            while (d < len)
            {
                d = Mathf.Min(len, d + speed * Time.deltaTime);
                float k = d / len;
                guest.transform.position = Vector3.Lerp(front, door, k);
                guest.transform.localScale = Vector3.one * scale * (1f - k);
                yield return null;
            }
        }
        else
        {
            anim.state = CharState.Happy;
            yield return new WaitForSeconds(0.6f);
        }

        Destroy(guest);
        GuestArrived?.Invoke();
    }

    IEnumerator WalkSegment(Transform guest, Vector3 a, Vector3 b, float speed)
    {
        float len = Vector3.Distance(a, b);
        if (len < 0.001f) yield break;
        Face(guest, b - a);
        float d = 0f;
        while (d < len)
        {
            d = Mathf.Min(len, d + speed * Time.deltaTime);
            guest.position = Vector3.Lerp(a, b, d / len);
            yield return null;
        }
    }

    static void Face(Transform t, Vector3 dir)
    {
        dir = Flat(dir);
        if (dir.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(dir);
    }

    // Bod trasy posunutý bokom (kolmo na smer cesty).
    static Vector3 Offset(List<Vector3> path, int i, float amount)
    {
        if (path.Count < 2) return path[i];
        Vector3 prev = path[Mathf.Max(0, i - 1)], next = path[Mathf.Min(path.Count - 1, i + 1)];
        Vector3 dir = Flat(next - prev);
        if (dir.sqrMagnitude < 0.0001f) return path[i];
        Vector3 side = new Vector3(dir.z, 0f, -dir.x);
        return path[i] + side * amount;
    }

    // ---------------------------------------------------------------- pomocné

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.000001f ? v.normalized : Vector3.zero;
    }

    static List<Vector3> Children(Transform parent)
    {
        var list = new List<Vector3>();
        if (parent == null) return list;
        foreach (Transform c in parent) list.Add(c.position);
        return list;
    }

    void OnDrawGizmos()
    {
        DrawPath(boatPath, new Color(0.2f, 0.6f, 1f), dock);
        DrawPath(boatLeavePath, new Color(1f, 0.55f, 0.1f), null);
        DrawPath(guestPath, new Color(0.25f, 0.9f, 0.3f), null);
        if (dock != null)
        {
            // červená guľa = kde zastaví predok lode, červená čiara = smer lode,
            // žltá čiara = strana, na ktorej loď stojí (od móla)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(dock.position, 0.3f);
            Gizmos.DrawLine(dock.position, dock.position + dock.forward * 1.5f);
            Gizmos.DrawLine(dock.position, dock.position - dock.forward * 2.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(dock.position, dock.position + dock.right * (pierOnRightSide ? -1f : 1f));
        }
    }

    static void DrawPath(Transform parent, Color color, Transform end)
    {
        if (parent == null) return;
        Gizmos.color = color;
        Vector3? prev = null;
        foreach (Transform c in parent)
        {
            Gizmos.DrawSphere(c.position, 0.12f);
            if (prev.HasValue) Gizmos.DrawLine(prev.Value, c.position);
            prev = c.position;
        }
        if (end != null && prev.HasValue) Gizmos.DrawLine(prev.Value, end.position);
    }
}
