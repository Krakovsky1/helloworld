using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// FR-39: Uloženie a obnova rozohranej vlny (SaveManager).
// Animácie sa neukladajú - po načítaní sú hostia hneď v hoteli (neviditeľní) a vlna pokračuje
// z rovnakej fázy: rozhodovanie (ACCEPT / REJECT), pobyt so zostávajúcim časom, odchod alebo cooldown.
// Ukladajú sa aj hostia (model), ich potreby (splnené aj čakajúce, vek kvôli hodnoteniu)
// a priradený personál (hodnotenie a zostávajúci čas obsluhy).
public partial class GuestWaveManager
{
    // Priradenia personálu, ktoré sa obnovia až po prijatí vlny (ApplyRestoredAssignments).
    readonly List<KeyValuePair<GuestNeed, NeedSave>> pendingAssignments = new List<KeyValuePair<GuestNeed, NeedSave>>();

    public void CaptureSave(WaveSave save)
    {
        save.waveNumber = WaveNumber;

        bool hasWave = WaveRunning || currentWaveGuests.Count > 0 || WaveDecisionMade;
        save.active = hasWave;
        if (!hasWave) return;

        // Počas príchodu lode ešte nie sú vytvorení všetci hostia - počet podľa lode.
        save.guestCount = !WaveSpawningComplete && currentWaveBoat != null
            ? Mathf.Max(currentWaveBoat.guestCount, ExpectedGuests)
            : ExpectedGuests;
        save.boatIndex = currentWaveBoat != null && boats != null ? Mathf.Max(0, Array.IndexOf(boats, currentWaveBoat)) : 0;
        save.decisionMade = WaveDecisionMade;
        save.accepted = IsWaveAccepted;
        save.departureDone = departureAnimationComplete;

        // Hostia a ich potreby
        save.guests.Clear();
        if (departureAnimationComplete) return;
        if (guestServiceManager == null) guestServiceManager = FindAnyObjectByType<GuestServiceManager>();

        foreach (var guest in currentWaveGuests)
        {
            if (guest == null) continue;

            var gs = new GuestSave { modelIndex = ModelIndexOf(guest) };

            var generator = guest.GetComponent<GuestNeedsGenerator>();
            if (generator != null) gs.nextNeedIn = generator.SecondsToNextNeed;

            var needs = guest.GetComponent<GuestNeeds>();
            if (needs != null)
            {
                foreach (var need in needs.Needs)
                {
                    var ns = new NeedSave
                    {
                        type = (int)need.Type,
                        completed = need.IsCompleted,
                        age = Mathf.Max(0f, Time.time - need.CreatedAt),
                    };

                    if (!need.IsCompleted && guestServiceManager != null &&
                        guestServiceManager.TryGetAssignment(need, out int stars, out float remaining))
                    {
                        ns.assigned = true;
                        ns.stars = stars;
                        ns.serviceRemaining = remaining;
                    }

                    gs.needs.Add(ns);
                }
            }

            save.guests.Add(gs);
        }
    }

    // Index modelu hosťa podľa mena ("Guest_" + meno modelu).
    int ModelIndexOf(GameObject guest)
    {
        if (guestModels == null) return 0;
        for (int i = 0; i < guestModels.Length; i++)
        {
            var gm = guestModels[i];
            if (gm != null && gm.model != null && guest.name == "Guest_" + gm.model.name) return i;
        }
        return 0;
    }

    // Po prijatí obnovenej vlny: zamestnanci sa vrátia k obsluhe, ktorú robili pred uložením.
    public void ApplyRestoredAssignments()
    {
        if (guestServiceManager == null) guestServiceManager = FindAnyObjectByType<GuestServiceManager>();
        if (guestServiceManager == null) { pendingAssignments.Clear(); return; }

        int restored = 0;
        foreach (var pair in pendingAssignments)
        {
            if (pair.Key == null || pair.Key.IsCompleted) continue;
            if (guestServiceManager.RestoreAssignment(pair.Key, pair.Value.stars, pair.Value.serviceRemaining))
                restored++;
        }

        if (pendingAssignments.Count > 0)
            Debug.Log("FR-39 | Obnovená obsluha: " + restored + "/" + pendingAssignments.Count + " zamestnancov pri práci.");
        pendingAssignments.Clear();
    }

    public void RestoreWaveNumber(int waveNumber)
    {
        WaveNumber = Mathf.Max(0, waveNumber);
    }

    // Kým sa vlna obnovuje (čaká sa na čas servera), START WAVE nesmie ísť stlačiť.
    public void LockStartButton()
    {
        FindStartButton();
        if (startButton != null) startButton.interactable = false;
    }

    public void UnlockStartButtonIfIdle()
    {
        FindStartButton();
        if (startButton != null && !WaveRunning && currentWaveGuests.Count == 0 && !WaveDecisionMade)
            startButton.interactable = true;
    }

    void FindStartButton()
    {
        if (startButton != null) return;
        var swb = FindAnyObjectByType<StartWaveButton>();
        if (swb != null) startButton = swb.GetComponent<Button>();
    }

    // Hostia uloženej vlny sú hneď v hoteli (bez lode a chôdze). Vyvolá WaveReadyForDecision,
    // takže sa ukáže panel ACCEPT / REJECT a WaveRewardManager začne evidenciu ako pri bežnej vlne.
    public bool RestoreGuestsInHotel(int guestCount, int boatIndex, List<GuestSave> savedGuests = null)
    {
        pendingAssignments.Clear();
        if (WaveRunning || currentWaveGuests.Count > 0) return false;
        if (boats == null || boats.Length == 0 || guestModels == null || guestModels.Length == 0) return false;

        currentWaveBoat = boats[Mathf.Clamp(boatIndex, 0, boats.Length - 1)];
        if (guestCount <= 0) guestCount = currentWaveBoat.guestCount;

        ExpectedGuests = 0;
        ArrivedGuests = 0;
        WaveSpawningComplete = false;
        IsWaveReadyForDecision = false;
        WaveDecisionMade = false;
        IsWaveAccepted = false;
        departureAnimationComplete = false;
        waveCooldownComplete = false;

        LockStartButton();

        if (upgrades == null) upgrades = FindAnyObjectByType<BuildingUpgradeManager>();
        var hotel = upgrades != null ? upgrades.GetBuilding(hotelLabel) : null;
        Vector3 inside = hotel != null
            ? hotel.transform.TransformPoint(hotelDoorLocal)
            : (dock != null ? dock.position : Vector3.zero);

        bool hasSaved = savedGuests != null && savedGuests.Count > 0;
        int count = hasSaved ? savedGuests.Count : guestCount;

        for (int i = 0; i < count; i++)
        {
            var saved = hasSaved ? savedGuests[i] : null;
            int modelIndex = saved != null
                ? Mathf.Clamp(saved.modelIndex, 0, guestModels.Length - 1)
                : UnityEngine.Random.Range(0, guestModels.Length);
            var gm = guestModels[modelIndex];
            if (gm == null || gm.model == null) continue;

            var guest = BuildGuest(gm);
            guest.transform.position = inside;
            guest.SetActive(true);
            HideGuest(guest);

            var anim = guest.GetComponent<CharacterAnimator>();
            if (anim != null) anim.state = CharState.Idle;

            var generator = guest.GetComponent<GuestNeedsGenerator>();
            if (saved != null)
            {
                // Uložené potreby (vyvolajú NeedAdded -> požiadavky v GuestServiceManager)
                var needs = guest.GetComponent<GuestNeeds>();
                if (needs != null && saved.needs != null)
                {
                    foreach (var ns in saved.needs)
                    {
                        var need = needs.RestoreNeed((GuestNeedType)ns.type, ns.completed, Time.time - Mathf.Max(0f, ns.age));
                        if (ns.assigned && !ns.completed)
                            pendingAssignments.Add(new KeyValuePair<GuestNeed, NeedSave>(need, ns));
                    }
                }

                if (generator != null)
                {
                    if (saved.nextNeedIn >= 0f) generator.ResumeGenerating(saved.nextNeedIn);
                    else if (saved.needs == null || saved.needs.Count == 0) generator.StartGenerating();
                }
            }
            else if (generator != null)
            {
                generator.StartGenerating();
            }

            currentWaveGuests.Add(guest);
            ExpectedGuests++;
            ArrivedGuests++;
        }

        WaveSpawningComplete = true;
        Debug.Log("FR-39 | Obnovená vlna " + WaveNumber + ": " + ExpectedGuests + " hostí je v hoteli.");
        CheckWaveReady();
        return ExpectedGuests > 0;
    }

    // Hostia už odplávali, beží len cooldown do ďalšej vlny.
    public void RestoreCooldownOnly(bool accepted)
    {
        WaveDecisionMade = true;
        IsWaveAccepted = accepted;
        departureAnimationComplete = true;
        waveCooldownComplete = false;
        LockStartButton();
    }
}
