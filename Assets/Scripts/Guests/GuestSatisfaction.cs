using System;
using System.Collections.Generic;
using UnityEngine;

// FR-10: spokojnosť jedného hosťa a priemer celej vlny.
// Hosť bez potreby je spokojný. Splnená potreba drží spokojnosť,
// čakanie ju zníži a čakanie na službe, ktorá je úzke miesto, ju zníži výrazne.
public static class GuestSatisfaction
{
    public const float NoNeeds = 1f;
    public const float CompletedNeed = 1f;
    public const float WaitingNeed = 0.6f;
    public const float BottleneckNeed = 0.15f;

    public static float ForGuest(GuestNeeds needs, Func<GuestNeedType, bool> isBottleneck)
    {
        if (needs == null || needs.Needs.Count == 0)
            return NoNeeds;

        float sum = 0f;
        int count = 0;
        foreach (GuestNeed need in needs.Needs)
        {
            count++;
            if (need.IsCompleted)
                sum += CompletedNeed;
            else if (isBottleneck != null && isBottleneck(need.Type))
                sum += BottleneckNeed;
            else
                sum += WaitingNeed;
        }

        return count == 0 ? NoNeeds : sum / count;
    }

    public static float Average(IReadOnlyList<GameObject> guests, Func<GuestNeedType, bool> isBottleneck)
    {
        if (guests == null || guests.Count == 0)
            return NoNeeds;

        float sum = 0f;
        int count = 0;
        for (int i = 0; i < guests.Count; i++)
        {
            GameObject guest = guests[i];
            if (guest == null)
                continue;

            sum += ForGuest(guest.GetComponent<GuestNeeds>(), isBottleneck);
            count++;
        }

        return count == 0 ? NoNeeds : sum / count;
    }

    public static bool TryMap(GuestNeedType type, out BottleneckService service)
    {
        switch (type)
        {
            case GuestNeedType.Room:
                service = BottleneckService.Rooms;
                return true;
            case GuestNeedType.Drink:
                service = BottleneckService.Drinks;
                return true;
            case GuestNeedType.Food:
                service = BottleneckService.Food;
                return true;
            case GuestNeedType.Relax:
                service = BottleneckService.Relax;
                return true;
            default:
                service = default;
                return false;
        }
    }
}
