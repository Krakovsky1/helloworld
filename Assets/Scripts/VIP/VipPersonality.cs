using System;
using System.Collections.Generic;
using UnityEngine;

// FR-37: Rôzne typy VIP osobností.
// Jedna VIP osobnosť = meno, profesia, vzácnosť, podmienky na pozvanie a odmena (násobiteľ).
// Všetky osobnosti sú v jednom súbore Assets/Resources/VIP/VipCatalog.asset (dajú sa upraviť v Inspectore).
// Samotné pozývanie (UI, príchod loďou) príde v ďalšom tikete; podmienky už vie overiť VipRequirements.

public enum VipRarity
{
    Common,     // Bežná - lacná, malé požiadavky, malý násobiteľ
    Rare,       // Vzácna
    Epic,       // Epická
    Legendary,  // Legendárna - najdrahšia, najnáročnejšia, najväčší násobiteľ
}

public enum VipBuilding
{
    Hotel,
    Restaurant,
    Bar,
    Pool,
    Wellness,
}

[Serializable]
public class VipBuildingRequirement
{
    public VipBuilding building;

    [Tooltip("Minimálna úroveň budovy (ignoruje sa, ak je zapnuté Require Max Level)")]
    [Min(1)] public int minLevel = 1;

    [Tooltip("Budova musí mať najvyššiu možnú úroveň (MAX)")]
    public bool requireMaxLevel;
}

[Serializable]
public class VipPersonality
{
    [Tooltip("Jednoznačný identifikátor (napr. pre uloženie hry)")]
    public string id;
    public string displayName;
    [Tooltip("Čím sa osobnosť živí (Travel vloggerka, DJ, ...)")]
    public string profession;
    [TextArea(2, 4)] public string description;
    public VipRarity rarity;

    [Header("Podmienky na pozvanie")]
    [Tooltip("Cena pozvania v minciach")]
    [Min(0)] public int inviteCost = 100;
    [Tooltip("Minimálna hodnota rezortu")]
    [Min(0)] public int requiredResortValue;
    public List<VipBuildingRequirement> buildingRequirements = new List<VipBuildingRequirement>();

    [Header("Odmena")]
    [Tooltip("Násobiteľ odmien od tohto hosťa (1 = ako bežný hosť)")]
    [Min(1f)] public float rewardMultiplier = 1.2f;

    [Header("Grafika")]
    public Sprite portrait;
    [Tooltip("Model postavy (TropicalResort_Characters/VIP/...)")]
    public GameObject model;
    [Tooltip("Polohy kĺbov pre animáciu (TropicalResort_Characters/Rigs/...rig.txt)")]
    public TextAsset rig;

    public static string RarityName(VipRarity rarity)
    {
        switch (rarity)
        {
            case VipRarity.Rare: return "Vzácna";
            case VipRarity.Epic: return "Epická";
            case VipRarity.Legendary: return "Legendárna";
            default: return "Bežná";
        }
    }

    public static Color RarityColor(VipRarity rarity)
    {
        switch (rarity)
        {
            case VipRarity.Rare: return new Color(0.23f, 0.56f, 0.9f);
            case VipRarity.Epic: return new Color(0.61f, 0.35f, 0.82f);
            case VipRarity.Legendary: return new Color(0.94f, 0.71f, 0.16f);
            default: return new Color(0.24f, 0.67f, 0.43f);
        }
    }

    public static string BuildingName(VipBuilding building)
    {
        switch (building)
        {
            case VipBuilding.Restaurant: return "Reštaurácia";
            case VipBuilding.Bar: return "Plážový bar";
            case VipBuilding.Pool: return "Bazén";
            case VipBuilding.Wellness: return "Wellness";
            default: return "Hotel";
        }
    }
}
