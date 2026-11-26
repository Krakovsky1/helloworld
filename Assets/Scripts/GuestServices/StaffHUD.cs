using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class StaffHUD : MonoBehaviour
{
    [Header("References (Button = this object)")]
    [SerializeField] private StaffManager staffManager;
    [SerializeField] private TMP_Text staffCountText;
    [SerializeField] private Button hireStaffButton;
    [SerializeField] private TMP_Text hireButtonText;

    private ResortResources subscribedResources;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (staffManager != null)
        {
            staffManager.OnStaffChanged -= RefreshUI;
            staffManager.OnStaffChanged += RefreshUI;
        }
        else
        {
            Debug.LogError("StaffHUD: Nenasiel sa StaffManager. Prirad ho v Inspectore.");
        }

        SubscribeToResources();

        RefreshUI();
    }

    private void OnDisable()
    {
        if (staffManager != null)
            staffManager.OnStaffChanged -= RefreshUI;

        if (subscribedResources != null)
            subscribedResources.Changed -= OnResourcesChanged;

        subscribedResources = null;
    }

    private void ResolveReferences()
    {
        // Najbezpecnejsie: StaffHUD je priamo na HireStaffButton.
        if (hireStaffButton == null)
            hireStaffButton = GetComponent<Button>();

        if (staffManager == null)
            staffManager = FindFirstObjectByType<StaffManager>();

        if (staffCountText == null)
        {
            GameObject countObject = GameObject.Find("StaffCountText");
            if (countObject != null)
                staffCountText = countObject.GetComponent<TMP_Text>();
        }

        if (hireButtonText == null && hireStaffButton != null)
            hireButtonText = hireStaffButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void SubscribeToResources()
    {
        ResortResources current = ResortResources.Instance;

        if (subscribedResources == current)
            return;

        if (subscribedResources != null)
            subscribedResources.Changed -= OnResourcesChanged;

        subscribedResources = current;

        if (subscribedResources != null)
            subscribedResources.Changed += OnResourcesChanged;
        else
            Debug.LogError("StaffHUD: ResortResources.Instance je null. Skontroluj, ci je v scene aktivny objekt ResortResources.");
    }

    private void OnResourcesChanged()
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        ResolveReferences();

        if (staffManager == null)
            return;

        if (staffCountText != null)
        {
            staffCountText.text = "STAFF: " +
                                  staffManager.AvailableStaff + "/" +
                                  staffManager.TotalStaff;
        }

        int price = staffManager.HireCost;
        bool atMax = staffManager.TotalStaff >= staffManager.MaxStaff;

        if (hireButtonText != null)
        {
            hireButtonText.text = atMax
                ? "MAX STAFF"
                : "+ HIRE STAFF\n" + price + " COINS";
        }

        // Pri chybajucom Resource manageri nedeaktivujeme Button potichu:
        // kliknutie zachyti handler a do Console vypise konkretny problem.
        ResortResources resources = ResortResources.Instance;
        if (hireStaffButton != null)
        {
            bool canAfford = resources != null && resources.Coins >= price;
            hireStaffButton.interactable = !atMax && (resources == null || canAfford);
        }
    }
}
