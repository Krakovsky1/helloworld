using System;
using System.Collections.Generic;
using UnityEngine;

public class StaffManager : MonoBehaviour
{
    [Header("Nastavenie zamestnancov")]
    [SerializeField] private int startingStaff = 6;
    [SerializeField] private int maxStaff = 20;
    [SerializeField] private int hireCost = 120;

    [Header("FR-16 - Kapacita sluzieb")]
    [SerializeField, Min(1)] private int capacityBonusPerStaff = 1;

    private int totalStaff;
    private int availableStaff;

    // Dlhodobo priradeni zamestnanci (na rozdiel od docasnej obsluhy poziadavky).
    private readonly Dictionary<GuestNeedType, int> staffByService =
        new Dictionary<GuestNeedType, int>();

    public int TotalStaff => totalStaff;
    public int AvailableStaff => availableStaff;
    public int AssignedStaff => totalStaff - availableStaff;
    public int HireCost => hireCost;
    public int MaxStaff => maxStaff;

    public bool CanHireStaff
    {
        get
        {
            ResortResources resources = ResortResources.Instance;
            return totalStaff < maxStaff && resources != null &&
                   resources.Coins >= hireCost;
        }
    }

    public event Action OnStaffChanged;

    private void Awake()
    {
        maxStaff = Mathf.Max(1, maxStaff);
        startingStaff = Mathf.Clamp(startingStaff, 0, maxStaff);
        hireCost = Mathf.Max(0, hireCost);
        capacityBonusPerStaff = Mathf.Max(1, capacityBonusPerStaff);

        totalStaff = startingStaff;
        availableStaff = totalStaff;
        staffByService.Clear();
        NotifyChanged();
    }

    public bool TryHireStaff()
    {
        if (totalStaff >= maxStaff)
        {
            Debug.Log("Dosiahol si maximalny pocet zamestnancov.");
            return false;
        }

        ResortResources resources = ResortResources.Instance;
        if (resources == null)
        {
            Debug.LogError("StaffManager: ResortResources sa nenasiel.");
            return false;
        }

        if (!resources.TrySpend(hireCost, 0))
        {
            Debug.Log("Nemas dostatok minci na najatie zamestnanca.");
            return false;
        }

        totalStaff++;
        availableStaff++;
        Debug.Log("Zamestnanec najaty. Dostupni: " + availableStaff + "/" + totalStaff);
        NotifyChanged();
        return true;
    }

    // Povodne docasne priradenie k JEDNEJ poziadavke hosta.
    // Tuto metodu GuestServiceUI a GuestServiceManager uz pouzivaju.
    public bool TryAssignStaff()
    {
        if (availableStaff <= 0)
        {
            Debug.Log("Ziadny volny zamestnanec.");
            return false;
        }

        availableStaff--;
        NotifyChanged();
        return true;
    }

    // Uvolni docasne prideleneho zamestnanca po skonceni obsluhy.
    public void ReleaseStaff()
    {
        if (availableStaff >= totalStaff)
        {
            Debug.LogWarning("Vsetci zamestnanci su uz volni.");
            return;
        }

        availableStaff++;
        NotifyChanged();
    }

    // FR-16: Trvale pridelenie pracownika ku konkretnej sluzbe.
    // Zamestnanec zostane obsadeny; po skonceni obsluhy sa NEuvolnuje.
    public bool TryAssignToService(GuestNeedType type)
    {
        if (!IsSupportedService(type) || availableStaff <= 0)
            return false;

        availableStaff--;
        staffByService[type] = GetAssignedStaffToService(type) + 1;

        Debug.Log("FR-16: Priradeny zamestnanec k " + type +
                  ". Pocet: " + staffByService[type] +
                  ", volna kapacita personalu: " + availableStaff);
        NotifyChanged();
        return true;
    }

    public int GetAssignedStaffToService(GuestNeedType type)
    {
        return staffByService.TryGetValue(type, out int count) ? count : 0;
    }

    public int GetServiceCapacity(GuestNeedType type, int baseCapacity = 0)
    {
        int staffCount = GetAssignedStaffToService(type);
        return Mathf.Max(0, baseCapacity) +
               staffCount * Mathf.Max(1, capacityBonusPerStaff);
    }

    private static bool IsSupportedService(GuestNeedType type)
    {
        return type == GuestNeedType.Food ||
               type == GuestNeedType.Drink ||
               type == GuestNeedType.Relax;
    }

    // Iba pre overenie FR-16 v Unity Inspectore pocas Play Mode.
    [ContextMenu("FR-16 / TEST / Priradit do restauracie")]
    private void TestAssignFood()
    {
        if (!Application.isPlaying) return;
        TryAssignToService(GuestNeedType.Food);
    }

    [ContextMenu("FR-16 / TEST / Priradit do baru")]
    private void TestAssignDrink()
    {
        if (!Application.isPlaying) return;
        TryAssignToService(GuestNeedType.Drink);
    }

    [ContextMenu("FR-16 / TEST / Priradit do relax sluzby")]
    private void TestAssignRelax()
    {
        if (!Application.isPlaying) return;
        TryAssignToService(GuestNeedType.Relax);
    }

    private void NotifyChanged()
    {
        OnStaffChanged?.Invoke();
    }
}
