using System.Collections.Generic;
using UnityEngine;

// FR-37: Overenie podmienok na pozvanie VIP osobnosti.
//  - dosť mincí (cena pozvania),
//  - hodnota rezortu aspoň requiredResortValue,
//  - úrovne budov (Hotel / Reštaurácia / Bar cez BuildingUpgradeManager, Bazén / Wellness cez BuildSpot).
// Mince sa tu neodpočítavajú - to spraví až pozvanie (ďalší tiket).
public static class VipRequirements
{
    public static bool CanInvite(VipPersonality vip)
    {
        return Check(vip, null);
    }

    // Vráti true, ak sú splnené všetky podmienky. Do "missing" doplní čitateľné dôvody (napr. pre UI).
    public static bool Check(VipPersonality vip, List<string> missing)
    {
        if (vip == null) return false;
        bool ok = true;

        var res = ResortResources.Instance;
        int coins = res != null ? res.Coins : 0;
        int value = res != null ? res.ResortValue : 0;

        if (coins < vip.inviteCost)
        {
            ok = false;
            missing?.Add("Mince: " + coins + " / " + vip.inviteCost);
        }

        if (value < vip.requiredResortValue)
        {
            ok = false;
            missing?.Add("Hodnota rezortu: " + value + " / " + vip.requiredResortValue);
        }

        if (vip.buildingRequirements != null)
        {
            foreach (var req in vip.buildingRequirements)
            {
                if (req == null) continue;
                int level = GetLevel(req.building);
                int max = GetMaxLevel(req.building);
                int needed = req.requireMaxLevel ? Mathf.Max(1, max) : req.minLevel;
                if (level >= needed && level > 0) continue;

                ok = false;
                missing?.Add(VipPersonality.BuildingName(req.building) + ": " +
                             (level > 0 ? "Level " + level : "nepostavená") + " / " +
                             (req.requireMaxLevel ? "MAX Level" : "Level " + needed));
            }
        }

        return ok;
    }

    // Text podmienky pre UI, napr. "Hotel MAX Level" alebo "Bar Level 2".
    public static string Describe(VipBuildingRequirement req)
    {
        return VipPersonality.BuildingName(req.building) + " " +
               (req.requireMaxLevel ? "MAX Level" : "Level " + req.minLevel);
    }

    // Aktuálna úroveň budovy; 0 = budova nie je postavená alebo sa nenašla.
    public static int GetLevel(VipBuilding building)
    {
        switch (building)
        {
            case VipBuilding.Pool: return SpotLevel("Pool", false);
            case VipBuilding.Wellness: return SpotLevel("Wellness", false);
        }

        var manager = Object.FindAnyObjectByType<BuildingUpgradeManager>();
        return manager != null ? manager.GetLevel(Label(building)) : 0;
    }

    public static int GetMaxLevel(VipBuilding building)
    {
        switch (building)
        {
            case VipBuilding.Pool: return SpotLevel("Pool", true);
            case VipBuilding.Wellness: return SpotLevel("Wellness", true);
        }

        var manager = Object.FindAnyObjectByType<BuildingUpgradeManager>();
        return manager != null ? manager.GetMaxLevel(Label(building)) : 0;
    }

    static string Label(VipBuilding building)
    {
        switch (building)
        {
            case VipBuilding.Restaurant: return "Restaurant";
            case VipBuilding.Bar: return "Bar";
            default: return BuildingUpgradeManager.HotelLabel;
        }
    }

    static int SpotLevel(string label, bool max)
    {
        foreach (var spot in Object.FindObjectsByType<BuildSpot>(FindObjectsSortMode.None))
            if (spot != null && spot.Label == label) return max ? spot.MaxLevel : spot.Level;
        return 0;
    }
}
