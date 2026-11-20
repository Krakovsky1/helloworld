using UnityEngine;

// FR-38: miesto na ostrove, kde sa dá postaviť wellness (celá logika je v BuildSpot).
//  - Postaviť sa dá, keď má hotel Level 3. Wellness (ako každá budova) nesmie mať vyššiu úroveň ako hotel.
//  - Level 1 -> 3: modely Wellness/Wellness_L1.obj, Wellness_L2.obj, Wellness_L3.obj.
//    Plocha je 4,4 x 4,4 m, aby sa nedotýkala hotela ani baru; modely sú zdvihnuté nad terén.
public class WellnessBuildSpot : BuildSpot
{
    public override string Label => "Wellness";

    protected override string[] DefaultModelPaths => new[]
    {
        "Assets/Buildings/TropicalResort_Buildings/Wellness/Wellness_L1.obj",
        "Assets/Buildings/TropicalResort_Buildings/Wellness/Wellness_L2.obj",
        "Assets/Buildings/TropicalResort_Buildings/Wellness/Wellness_L3.obj",
    };

    protected override Vector2 Footprint => new Vector2(4.4f, 4.4f);

#if UNITY_EDITOR
    // Predvolené hodnoty pri ručnom pridaní komponentu (v scéne sú už nastavené).
    protected override void Reset()
    {
        nameNominative = "Wellness";
        nameAccusative = "Wellness";
        requiredHotelLevel = 3;
        maxLevel = 3;
        buildCoins = 60;
        buildEnergy = 8;
        upgradeCoins = new[] { 90, 130 };
        upgradeEnergy = new[] { 12, 16 };
        builtIconResource = "UI/Icons/building_wellness";
        base.Reset();
    }
#endif
}
