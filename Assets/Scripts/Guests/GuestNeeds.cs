using System;
using System.Collections.Generic;
using UnityEngine;

public class GuestNeeds : MonoBehaviour
{
    [SerializeField]
    private List<GuestNeed> needs = new List<GuestNeed>();

    public IReadOnlyList<GuestNeed> Needs => needs;

    public event Action<GuestNeed> NeedAdded;
    public event Action<GuestNeed> NeedCompleted;

    public void AddNeed(GuestNeedType type)
    {
        GuestNeed need = new GuestNeed(type);
        needs.Add(need);

        Debug.Log($"{gameObject.name} received need: {type}");
        NeedAdded?.Invoke(need);
    }

    public bool CompleteNeed(int index)
    {
        if (index < 0 || index >= needs.Count)
            return false;

        GuestNeed need = needs[index];

        if (need.IsCompleted)
            return false;

        need.IsCompleted = true;

        Debug.Log($"{gameObject.name} completed need: {need.Type}");
        NeedCompleted?.Invoke(need);
        return true;
    }

    public int ActiveNeedsCount
    {
        get
        {
            int count = 0;

            foreach (GuestNeed need in needs)
            {
                if (!need.IsCompleted)
                    count++;
            }

            return count;
        }
    }
}
