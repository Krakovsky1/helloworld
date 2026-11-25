using System;
using UnityEngine;

public class StaffManager : MonoBehaviour
{
    [Header("Nastavenie zamestnancov")]
    [SerializeField] private int startingStaff = 6;
    [SerializeField] private int maxStaff = 20;
    [SerializeField] private int hireCost = 120;

    private int totalStaff;
    private int availableStaff;

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
            return totalStaff < maxStaff &&
                   resources != null &&
                   resources.Coins >= hireCost;
        }
    }

    public event Action OnStaffChanged;

    private void Awake()
    {
        maxStaff = Mathf.Max(1, maxStaff);
        startingStaff = Mathf.Clamp(startingStaff, 0, maxStaff);
        hireCost = Mathf.Max(0, hireCost);

        totalStaff = startingStaff;
        availableStaff = totalStaff;

        NotifyChanged();
    }

    public bool TryHireStaff()
    {
        if (totalStaff >= maxStaff)
        {
            Debug.Log("Dosiahol si maxim·lny poËet zamestnancov.");
            return false;
        }

        ResortResources resources = ResortResources.Instance;

        if (resources == null)
        {
            Debug.LogError("StaffManager: ResortResources sa nenaöiel.");
            return false;
        }

        if (!resources.TrySpend(hireCost, 0))
        {
            Debug.Log("Nem·ö dostatok mincÌ na n·kup zamestnanca.");
            return false;
        }

        totalStaff++;
        availableStaff++;

        Debug.Log("Zamestnanec bol najat˝. DostupnÌ: " +
                  availableStaff + "/" + totalStaff);

        NotifyChanged();
        return true;
    }

    public bool TryAssignStaff()
    {
        if (availableStaff <= 0)
        {
            Debug.Log("Nem·ö ûiadneho voænÈho zamestnanca.");
            return false;
        }

        availableStaff--;

        Debug.Log("Zamestnanec bol priraden˝. DostupnÌ: " +
                  availableStaff + "/" + totalStaff);

        NotifyChanged();
        return true;
    }

    public void ReleaseStaff()
    {
        if (availableStaff >= totalStaff)
        {
            Debug.LogWarning("Vöetci zamestnanci s˙ uû voænÌ.");
            return;
        }

        availableStaff++;

        Debug.Log("Zamestnanec je op‰ù dostupn˝. DostupnÌ: " +
                  availableStaff + "/" + totalStaff);

        NotifyChanged();
    }

    // FR-39: obnova z ulozenej hry - pocet najatych zamestnancov (vsetci su po nacitani volni).
    public void RestoreStaff(int savedTotal)
    {
        if (savedTotal < 0)
            return;

        totalStaff = Mathf.Clamp(savedTotal, 0, maxStaff);
        availableStaff = totalStaff;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnStaffChanged?.Invoke();
    }
}
