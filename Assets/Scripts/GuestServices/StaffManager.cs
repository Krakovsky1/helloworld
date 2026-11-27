using System;
using UnityEngine;

public class StaffManager : MonoBehaviour
{
    [Header("Nastavenie zamestnancov")]
    [SerializeField, Min(1)] private int startingStaff = 6;
    [SerializeField, Min(1)] private int maxStaff = 20;

    [Header("Cena najimania")]
    [SerializeField, Min(0)] private int firstHireCost = 120;
    [SerializeField, Min(0)] private int hireCostIncrease = 20;

    private int totalStaff;
    private int availableStaff;

    public int TotalStaff => totalStaff;
    public int AvailableStaff => availableStaff;
    public int AssignedStaff => Mathf.Max(0, totalStaff - availableStaff);
    public int MaxStaff => maxStaff;

    // Prvy dokupeny zamestnanec stoji 120, dalsi 140, potom 160 atd.
    public int HireCost
    {
        get
        {
            int hiredSinceStart = Mathf.Max(0, totalStaff - startingStaff);
            return firstHireCost + hiredSinceStart * hireCostIncrease;
        }
    }

    public bool CanHireStaff
    {
        get
        {
            ResortResources resources = ResortResources.Instance;
            return totalStaff < maxStaff &&
                   resources != null &&
                   resources.Coins >= HireCost;
        }
    }

    public event Action OnStaffChanged;

    private void Awake()
    {
        maxStaff = Mathf.Max(1, maxStaff);
        startingStaff = Mathf.Clamp(startingStaff, 1, maxStaff);
        firstHireCost = Mathf.Max(0, firstHireCost);
        hireCostIncrease = Mathf.Max(0, hireCostIncrease);

        totalStaff = startingStaff;
        availableStaff = totalStaff;

        Debug.Log("StaffManager inicializovany: " + availableStaff + "/" + totalStaff +
                  ". Cena prveho dokupeneho zamestnanca: " + HireCost + " coins.");

        NotifyChanged();
    }

    // Unity UI Button OnClick() entry point.
    // Button invokes void methods; the actual purchase logic stays in TryHireStaff().
    public void HireStaffFromButton()
    {
        TryHireStaff();
    }

    public bool TryHireStaff()
    {
        if (totalStaff >= maxStaff)
        {
            Debug.LogWarning("StaffManager: Dosiahol si maximalny pocet zamestnancov (" + maxStaff + ").");
            return false;
        }

        ResortResources resources = ResortResources.Instance;
        if (resources == null)
        {
            Debug.LogError("StaffManager: ResortResources.Instance je null. Skontroluj objekt ResortResources v scene.");
            return false;
        }

        int price = HireCost;

        if (!resources.TrySpend(price, 0))
        {
            Debug.LogWarning("StaffManager: Nedostatok coins. Potrebujes " +
                             price + ", aktualne mas " + resources.Coins + ".");
            return false;
        }

        totalStaff++;
        availableStaff++;

        Debug.Log("StaffManager: Najaty zamestnanec za " + price +
                  " coins. Stav: " + availableStaff + "/" + totalStaff +
                  ". Dalsi bude stat " + HireCost + " coins.");

        NotifyChanged();
        SaveManager.RequestSave();
        return true;
    }

    // Bezna poziadavka potrebuje jedneho zamestnanca.
    public bool TryAssignStaff()
    {
        return TryAssignStaff(1);
    }

    // VIP poziadavka moze potrebovat dvoch zamestnancov.
    public bool TryAssignStaff(int count)
    {
        if (count <= 0)
        {
            Debug.LogWarning("StaffManager: Pocet priradovanych zamestnancov musi byt vacsi ako 0.");
            return false;
        }

        if (availableStaff < count)
        {
            Debug.Log("StaffManager: Nedostatok volnych zamestnancov. Potrebnych: " +
                      count + ", dostupnych: " + availableStaff + ".");
            return false;
        }

        availableStaff -= count;
        NotifyChanged();
        return true;
    }

    public void ReleaseStaff()
    {
        ReleaseStaff(1);
    }

    public void ReleaseStaff(int count)
    {
        if (count <= 0)
        {
            Debug.LogWarning("StaffManager: Pocet uvolnovanych zamestnancov musi byt vacsi ako 0.");
            return;
        }

        int assigned = totalStaff - availableStaff;
        int releaseCount = Mathf.Min(count, assigned);

        if (releaseCount <= 0)
        {
            Debug.LogWarning("StaffManager: Nie su ziadni priradeni zamestnanci na uvolnenie.");
            return;
        }

        availableStaff += releaseCount;
        NotifyChanged();
    }

    // FR-39: obnova personálu zo save. Nula v starom/neuplnom save
    // povazujeme za chybajuci udaj, nie za skutocny pocet zamestnancov.
    public void RestoreStaff(int savedTotal)
    {
        int restoredTotal = savedTotal <= 0 ? startingStaff : savedTotal;
        totalStaff = Mathf.Clamp(restoredTotal, startingStaff, maxStaff);
        availableStaff = totalStaff;

        Debug.Log("StaffManager: Obnoveny personal zo save: " +
                  availableStaff + "/" + totalStaff + ".");

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnStaffChanged?.Invoke();
    }
}
