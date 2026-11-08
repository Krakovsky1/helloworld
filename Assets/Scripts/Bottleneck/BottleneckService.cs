// FR-08 Varovanie pred úzkym miestom.
// Služby (needy) z build dokumentu §4.4: Rooms, Drinks, Food, Relax, Ocean View.

public enum BottleneckService
{
    Rooms,
    Drinks,
    Food,
    Relax,
    OceanView
}

/// <summary>Stav jednej služby podľa build dokumentu (§4.4, "Stav jedného needu").</summary>
public enum BottleneckState
{
    Ok,          // pokoj - nič sa nezobrazuje
    Bottleneck,  // rad rastie -> červené varovanie
    Recovering,  // rad sa zmenšuje, budova je ešte plná (zobrazí sa ako "plná")
    Locked       // služba ešte nie je odomknutá (Relax pred Pool, Ocean View pred Bungalows)
}

public static class BottleneckServiceInfo
{
    public static readonly BottleneckService[] All =
    {
        BottleneckService.Rooms,
        BottleneckService.Drinks,
        BottleneckService.Food,
        BottleneckService.Relax,
        BottleneckService.OceanView
    };

    /// <summary>Názov budovy, ktorý vidí hráč.</summary>
    public static string BuildingName(BottleneckService s)
    {
        switch (s)
        {
            case BottleneckService.Rooms: return "Recepcia";
            case BottleneckService.Drinks: return "Plážový bar";
            case BottleneckService.Food: return "Reštaurácia";
            case BottleneckService.Relax: return "Bazén";
            default: return "Bungalovy";
        }
    }

    /// <summary>"Reštaurácia je plná", "Plážový bar je plný", "Bungalovy sú plné" ...</summary>
    public static string FullText(BottleneckService s)
    {
        switch (s)
        {
            case BottleneckService.Rooms: return "Recepcia je plná";
            case BottleneckService.Drinks: return "Plážový bar je plný";
            case BottleneckService.Food: return "Reštaurácia je plná";
            case BottleneckService.Relax: return "Bazén je plný";
            default: return "Bungalovy sú plné";
        }
    }

    /// <summary>"Reštaurácia je voľná", "Plážový bar je voľný", "Bungalovy sú voľné" ...</summary>
    public static string FreeText(BottleneckService s)
    {
        switch (s)
        {
            case BottleneckService.Rooms: return "Recepcia je voľná";
            case BottleneckService.Drinks: return "Plážový bar je voľný";
            case BottleneckService.Food: return "Reštaurácia je voľná";
            case BottleneckService.Relax: return "Bazén je voľný";
            default: return "Bungalovy sú voľné";
        }
    }

    /// <summary>"Reštaurácia nestíha", "Bungalovy nestíhajú" ...</summary>
    public static string OverloadedText(BottleneckService s)
    {
        return s == BottleneckService.OceanView ? "Bungalovy nestíhajú" : BuildingName(s) + " nestíha";
    }

    /// <summary>Label budovy v BuildingUpgradeManager (FR-04-FP), podľa ktorého nájdeme budovu v scéne.</summary>
    public static string UpgradeLabel(BottleneckService s)
    {
        switch (s)
        {
            case BottleneckService.Rooms: return "Hotel";
            case BottleneckService.Drinks: return "Bar";
            case BottleneckService.Food: return "Restaurant";
            case BottleneckService.Relax: return "Pool";
            default: return "Bungalows";
        }
    }

    /// <summary>Záložné názvy objektov v scéne, ak budovu nespravuje BuildingUpgradeManager.</summary>
    public static string[] SceneNames(BottleneckService s)
    {
        switch (s)
        {
            case BottleneckService.Rooms: return new[] { "Building_Reception", "BeachHotel_L1", "BeachHotel_L2", "BeachHotel_L3" };
            case BottleneckService.Drinks: return new[] { "Building_BeachBar", "BeachBar" };
            case BottleneckService.Food: return new[] { "Building_Restaurant", "Restaurant" };
            case BottleneckService.Relax: return new[] { "Building_Pool", "Pool" };
            default: return new[] { "Building_Bungalows", "Bungalow" };
        }
    }

    public static bool LockedAtStart(BottleneckService s)
    {
        return s == BottleneckService.Relax || s == BottleneckService.OceanView;
    }
}
