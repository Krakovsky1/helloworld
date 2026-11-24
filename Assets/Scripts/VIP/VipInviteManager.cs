using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// FR-31: Pozvanie VIP hosťa popri bežnej vlne.
//  - Hráč vidí ponuku 3 náhodných VIP hostí (losuje sa podľa vzácnosti, váhy sú vo VipCatalog.asset).
//  - Pozvať môže jedného, ak spĺňa podmienky (VipRequirements): zaplatí cenu v minciach.
//  - Pozvaný VIP patrí k ďalšej vlne. Keď hostia tejto vlny odplávajú (DepartureFinished),
//    návšteva sa skončí a vylosuje sa nová ponuka.
//  - Ponuka sa mení až po vlne (nie pri každom otvorení menu), aby sa nedala "pretáčať".
// Príchod VIP postavy na ostrov a použitie rewardMultiplier rieši ďalší tiket - stačí sa napojiť
// na udalosť VipInvited / vlastnosť InvitedVip.
// Objekt aj UI (tlačidlo VIP + okno) sa vytvoria automaticky na ostrove, v scéne netreba nič nastavovať.
public class VipInviteManager : MonoBehaviour
{
    public static VipInviteManager Instance { get; private set; }

    // Zmena ponuky alebo pozvania (UI sa prekreslí).
    public event Action Changed;
    // Hráč pozval VIP hosťa (zaplatené). Parameter = pozvaný VIP.
    public event Action<VipPersonality> VipInvited;
    // Návšteva pozvaného VIP sa skončila (jeho vlna odplávala).
    public event Action<VipPersonality> VipVisitFinished;

    readonly List<VipPersonality> offer = new List<VipPersonality>();

    public IReadOnlyList<VipPersonality> Offer => offer;

    // Pozvaný VIP (null = nikto). Naraz môže byť pozvaný len jeden.
    public VipPersonality InvitedVip { get; private set; }
    // Číslo vlny, s ktorou VIP príde (GuestWaveManager.WaveNumber).
    public int InvitedForWave { get; private set; }

    public bool HasInvitedVip => InvitedVip != null;

    GuestWaveManager waves;
    VipCatalog catalog;

    // ------------------------------------------------------------------
    // Automatické vytvorenie na ostrove (rovnako ako ResourceHud)
    // ------------------------------------------------------------------

    static bool sceneHooked;

    // Projekt má vypnutý Domain Reload - statické hodnoty treba pri štarte Play módu vynulovať.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (sceneHooked) SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneHooked = false;
        Instance = null;
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

        // Bez katalógu VIP osobností nemá tlačidlo čo ponúknuť.
        if (VipCatalog.Load() == null) return;

        var go = new GameObject("VipInvite");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<VipInviteManager>();
    }

    // ------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        catalog = VipCatalog.Load();
        RollOffer();

        // UI: tlačidlo VIP v HUD a okno s ponukou.
        var popup = VipInvitePopup.Create(transform, this);
        VipHudButton.Create(transform, popup);
    }

    void Start()
    {
        waves = FindAnyObjectByType<GuestWaveManager>();
        if (waves != null) waves.DepartureFinished += OnDepartureFinished;
        else Debug.LogWarning("VipInviteManager: GuestWaveManager sa nenašiel - ponuka VIP sa nebude obnovovať po vlne.");
    }

    void OnDestroy()
    {
        if (waves != null) waves.DepartureFinished -= OnDepartureFinished;
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Pozvanie
    // ------------------------------------------------------------------

    // Dá sa tohto VIP pozvať práve teraz? "reason" = prečo nie (pre UI), inak prázdne.
    public bool CanInvite(VipPersonality vip, out string reason)
    {
        reason = "";
        if (vip == null || !offer.Contains(vip))
        {
            reason = "Tento VIP host už nie je v ponuke";
            return false;
        }

        if (InvitedVip != null)
        {
            reason = InvitedVip == vip ? "Už je pozvaný" : "Naraz môžeš pozvať len jedného VIP hosťa";
            return false;
        }

        if (!VipRequirements.CanInvite(vip))
        {
            reason = "Nespĺňaš podmienky";
            return false;
        }

        return true;
    }

    public bool TryInvite(VipPersonality vip)
    {
        if (!CanInvite(vip, out string reason))
        {
            Debug.Log("FR-31 | VIP " + (vip != null ? vip.displayName : "?") + " sa nedá pozvať: " + reason);
            return false;
        }

        var res = ResortResources.Instance;
        if (res == null || !res.TrySpend(vip.inviteCost, 0))
        {
            Debug.Log("FR-31 | Nedostatok mincí na pozvanie " + vip.displayName);
            return false;
        }

        InvitedVip = vip;
        InvitedForWave = (waves != null ? waves.WaveNumber : 0) + 1;

        Debug.Log("FR-31 | Pozvaný VIP: " + vip.displayName + " (" + vip.profession + ") za " + vip.inviteCost +
                  " mincí, príde s vlnou " + InvitedForWave + ", násobiteľ odmien x" + vip.rewardMultiplier);

        VipInvited?.Invoke(vip);
        Changed?.Invoke();
        return true;
    }

    // Vlna odplávala z ostrova.
    void OnDepartureFinished()
    {
        int finishedWave = waves != null ? waves.WaveNumber : 0;

        if (InvitedVip != null)
        {
            // VIP pozvaný počas prebiehajúcej vlny patrí až k ďalšej - ponuku zatiaľ nemeníme.
            if (finishedWave < InvitedForWave) return;

            var vip = InvitedVip;
            InvitedVip = null;
            InvitedForWave = 0;
            Debug.Log("FR-31 | Návšteva VIP " + vip.displayName + " sa skončila.");
            VipVisitFinished?.Invoke(vip);
        }

        RollOffer();
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------
    // Losovanie ponuky podľa vzácnosti
    // ------------------------------------------------------------------

    // Najprv sa vylosuje vzácnosť (podľa váh), potom náhodný VIP tejto vzácnosti.
    // Každý VIP je v ponuke najviac raz. Ak niektorá vzácnosť už nemá voľných VIP, losuje sa z ostatných.
    public void RollOffer()
    {
        offer.Clear();
        if (catalog == null) catalog = VipCatalog.Load();
        if (catalog == null) return;

        var pool = new List<VipPersonality>();
        foreach (var vip in catalog.Personalities)
            if (vip != null) pool.Add(vip);

        int count = Mathf.Min(catalog.OfferSize, pool.Count);
        for (int i = 0; i < count; i++)
        {
            var pick = PickWeighted(pool);
            if (pick == null) break;
            offer.Add(pick);
            pool.Remove(pick);
        }

        // Zoradiť od najlacnejšieho, nech je menu prehľadné.
        offer.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : a.inviteCost.CompareTo(b.inviteCost));

        var names = new List<string>();
        foreach (var vip in offer) names.Add(vip.displayName + " (" + VipPersonality.RarityName(vip.rarity) + ")");
        Debug.Log("FR-31 | Nová ponuka VIP: " + string.Join(", ", names));
    }

    VipPersonality PickWeighted(List<VipPersonality> pool)
    {
        if (pool.Count == 0) return null;

        // Váhy len tých vzácností, z ktorých ešte niekto zostal.
        var rarities = new List<VipRarity>();
        float total = 0f;
        foreach (VipRarity rarity in Enum.GetValues(typeof(VipRarity)))
        {
            if (!pool.Exists(v => v.rarity == rarity)) continue;
            float weight = catalog.RarityWeight(rarity);
            if (weight <= 0f) continue;
            rarities.Add(rarity);
            total += weight;
        }

        // Všetky váhy 0 -> úplne náhodne.
        if (rarities.Count == 0) return pool[UnityEngine.Random.Range(0, pool.Count)];

        float roll = UnityEngine.Random.value * total;
        var chosen = rarities[rarities.Count - 1];
        foreach (var rarity in rarities)
        {
            roll -= catalog.RarityWeight(rarity);
            if (roll <= 0f) { chosen = rarity; break; }
        }

        var candidates = pool.FindAll(v => v.rarity == chosen);
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }
}
