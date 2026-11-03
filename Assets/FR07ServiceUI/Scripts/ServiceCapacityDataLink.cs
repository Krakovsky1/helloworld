using UnityEngine;

/// <summary>
/// FR-07: Updates the existing ServiceCapacityPanel without changing its UI,
/// colors, object hierarchy, wave link, or prefab.
/// </summary>
[RequireComponent(typeof(ServiceCapacityPanel))]
[RequireComponent(typeof(ServiceCapacityTracker))]
public class ServiceCapacityDataLink : MonoBehaviour
{
    private ServiceCapacityPanel panel;
    private ServiceCapacityTracker tracker;

    private void OnEnable()
    {
        panel = GetComponent<ServiceCapacityPanel>();
        tracker = GetComponent<ServiceCapacityTracker>();
        if (tracker != null) tracker.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (tracker != null) tracker.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (panel == null) return;

        if (tracker == null)
        {
            panel.SetUsage(0, 0, 0, 0, 0, 0);
            panel.SetDemoMode(true);
            return;
        }

        // Unreported categories have capacity 0; panel shows '— / —'.
        panel.SetUsage(tracker.ServicesUsed, tracker.ServicesCapacity,
                       tracker.DrinksUsed, tracker.DrinksCapacity,
                       tracker.FoodUsed, tracker.FoodCapacity);

        // Test or incomplete data stay labelled DEMO, never falsely LIVE.
        panel.SetDemoMode(!tracker.HasAllRealValues);
    }
}
