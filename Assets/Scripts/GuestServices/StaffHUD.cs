
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaffHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StaffManager staffManager;
    [SerializeField] private TMP_Text staffCountText;
    [SerializeField] private Button hireStaffButton;
    [SerializeField] private TMP_Text hireButtonText;

    private void Awake()
    {
        if (staffManager == null)
            staffManager = FindAnyObjectByType<StaffManager>();
    }

    private void OnEnable()
    {
        if (staffManager != null)
            staffManager.OnStaffChanged += RefreshUI;

        if (hireStaffButton != null)
            hireStaffButton.onClick.AddListener(HireStaff);

        RefreshUI();
    }

    private void OnDisable()
    {
        if (staffManager != null)
            staffManager.OnStaffChanged -= RefreshUI;

        if (hireStaffButton != null)
            hireStaffButton.onClick.RemoveListener(HireStaff);
    }

    private void HireStaff()
    {
        if (staffManager == null)
            return;

        staffManager.TryHireStaff();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (staffManager == null)
            return;

        if (staffCountText != null)
        {
            GameTextBinding.Set(staffCountText, "staff_count",
                staffManager.AvailableStaff, staffManager.TotalStaff);
        }

        if (hireButtonText != null)
        {
            if (staffManager.TotalStaff >= 20)
                GameTextBinding.Set(hireButtonText, "max_staff");
            else
                GameTextBinding.Set(hireButtonText, "hire_staff_hud", 120);
        }

        if (hireStaffButton != null)
        {
            hireStaffButton.interactable =
                staffManager.TotalStaff < 20;
        }
    }
}
