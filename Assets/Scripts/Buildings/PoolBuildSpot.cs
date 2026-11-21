using UnityEngine;

// Miesto na ostrove, kde sa dá postaviť bazén (celá logika je v BuildSpot).
//  - Keď má hotel Level 2, je nad miestom ikonka "+", stavba stojí 25 mincí a 3 energie.
//  - Level 1 -> 3: modely Wellness/Pool.obj, Pool_L2.obj, Pool_L3.obj.
public class PoolBuildSpot : BuildSpot
{
    public override string Label => "Pool";

    protected override string[] DefaultModelPaths => new[]
    {
        "Assets/Buildings/TropicalResort_Buildings/Wellness/Pool.obj",
        "Assets/Buildings/TropicalResort_Buildings/Wellness/Pool_L2.obj",
        "Assets/Buildings/TropicalResort_Buildings/Wellness/Pool_L3.obj",
    };

    protected override void OnBuilt()
    {
        // FR-08: služba "Bazén" (Relax) sa odomkne až po postavení bazéna.
        if (BottleneckMonitor.Instance != null)
            BottleneckMonitor.Instance.SetLocked(BottleneckService.Relax, false);
    }
}
