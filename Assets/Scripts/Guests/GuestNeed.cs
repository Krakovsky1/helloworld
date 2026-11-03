using System;
using UnityEngine;

public enum GuestNeedType
{
    Room,
    Drink,
    Food,
    Relax
}

[Serializable]
public class GuestNeed
{
    public GuestNeedType Type;
    public bool IsCompleted;
    public float CreatedAt;

    public GuestNeed(GuestNeedType type)
    {
        Type = type;
        IsCompleted = false;
        CreatedAt = Time.time;
    }
}
