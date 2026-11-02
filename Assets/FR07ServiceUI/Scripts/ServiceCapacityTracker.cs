using System;
using UnityEngine;

/// <summary>
/// FR-07: Single source of occupancy data for services, drinks and food.
/// Other gameplay scripts should report REAL usage through Report... methods.
/// No guest counts are invented from GuestWaveManager events.
/// </summary>
public class ServiceCapacityTracker : MonoBehaviour
{
    private struct Entry
    {
        public int Used;
        public int Capacity;
        public bool HasValue;
        public bool IsTest;
    }

    private Entry services;
    private Entry drinks;
    private Entry food;

    public event Action Changed;

    public int ServicesUsed => services.HasValue ? services.Used : 0;
    public int ServicesCapacity => services.HasValue ? services.Capacity : 0;
    public int DrinksUsed => drinks.HasValue ? drinks.Used : 0;
    public int DrinksCapacity => drinks.HasValue ? drinks.Capacity : 0;
    public int FoodUsed => food.HasValue ? food.Used : 0;
    public int FoodCapacity => food.HasValue ? food.Capacity : 0;

    // Only call the UI 'LIVE' after ALL THREE categories have genuine values.
    public bool HasAllRealValues =>
        services.HasValue && drinks.HasValue && food.HasValue &&
        !services.IsTest && !drinks.IsTest && !food.IsTest;

    public void ReportServices(int used, int capacity) => Set(ref services, used, capacity, false);
    public void ReportDrinks(int used, int capacity) => Set(ref drinks, used, capacity, false);
    public void ReportFood(int used, int capacity) => Set(ref food, used, capacity, false);

    /// <summary>For a future gameplay manager that calculates all capacities in one place.</summary>
    public void ReportAll(int servicesUsed, int servicesCapacity,
                          int drinksUsed, int drinksCapacity,
                          int foodUsed, int foodCapacity)
    {
        Write(ref services, servicesUsed, servicesCapacity, false);
        Write(ref drinks, drinksUsed, drinksCapacity, false);
        Write(ref food, foodUsed, foodCapacity, false);
        Changed?.Invoke();
    }

    private void Set(ref Entry entry, int used, int capacity, bool isTest)
    {
        Write(ref entry, used, capacity, isTest);
        Changed?.Invoke();
    }

    private static void Write(ref Entry entry, int used, int capacity, bool isTest)
    {
        entry.Used = Mathf.Max(0, used);
        entry.Capacity = Mathf.Max(0, capacity);
        entry.HasValue = true;
        entry.IsTest = isTest;
    }

    [ContextMenu("FR-07 / TEST: Naplnit ukazkove hodnoty (Play Mode)")]
    private void LoadTestValues()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("FR-07: Test hodnoty nastavuj az pocas Play Mode.");
            return;
        }

        Write(ref services, 6, 10, true);
        Write(ref drinks, 3, 8, true);
        Write(ref food, 7, 12, true);
        Changed?.Invoke();
        Debug.Log("FR-07: Test hodnoty nacitane. Toto NIE SU realne udaje hry.");
    }

    [ContextMenu("FR-07 / TEST: Vynulovat dostupne udaje (Play Mode)")]
    private void ClearValues()
    {
        if (!Application.isPlaying) return;
        services = default;
        drinks = default;
        food = default;
        Changed?.Invoke();
    }
}
