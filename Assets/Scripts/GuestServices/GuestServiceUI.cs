using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GuestServiceUI : MonoBehaviour
{
    [Header("ManaûÈri")]
    [SerializeField] private GuestServiceManager serviceManager;
    [SerializeField] private StaffManager staffManager;

    [Header("UI - poûiadavky")]
    [SerializeField] private Transform requestList;
    [SerializeField] private GameObject requestRowTemplate;

    [Header("UI - zamestnanci")]
    [SerializeField] private TMP_Text staffCountText;
    [SerializeField] private Button hireStaffButton;
    [SerializeField] private TMP_Text hireStaffButtonText;

    private readonly List<GameObject> createdRows =
        new List<GameObject>();

    private ResortResources subscribedResources;

    private void Awake()
    {
        if (serviceManager == null)
            serviceManager = FindAnyObjectByType<GuestServiceManager>();

        if (staffManager == null)
            staffManager = FindAnyObjectByType<StaffManager>();
    }

    private void OnEnable()
    {
        if (serviceManager != null)
            serviceManager.OnRequestsChanged += RefreshUI;

        if (staffManager != null)
            staffManager.OnStaffChanged += RefreshUI;

        subscribedResources = ResortResources.Instance;
        if (subscribedResources != null)
            subscribedResources.Changed += RefreshUI;

        if (hireStaffButton != null)
        {
            hireStaffButton.onClick.RemoveListener(HireStaff);
            hireStaffButton.onClick.AddListener(HireStaff);
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        if (serviceManager != null)
            serviceManager.OnRequestsChanged -= RefreshUI;

        if (staffManager != null)
            staffManager.OnStaffChanged -= RefreshUI;

        if (subscribedResources != null)
            subscribedResources.Changed -= RefreshUI;

        if (hireStaffButton != null)
            hireStaffButton.onClick.RemoveListener(HireStaff);
    }

    private void RefreshUI()
    {
        RefreshStaffUI();
        RefreshRequestList();
    }

    private void RefreshStaffUI()
    {
        if (staffManager == null)
            return;

        if (staffCountText != null)
        {
            GameTextBinding.Set(staffCountText, "staff_count",
                staffManager.AvailableStaff, staffManager.TotalStaff);
        }

        if (hireStaffButtonText != null)
            GameTextBinding.Set(hireStaffButtonText, "hire_staff_cost", staffManager.HireCost);

        if (hireStaffButton != null)
            hireStaffButton.interactable = staffManager.CanHireStaff;
    }

    private void HireStaff()
    {
        if (staffManager == null)
            return;

        staffManager.TryHireStaff();
        RefreshStaffUI();
    }

    private void RefreshRequestList()
    {
        if (requestList == null ||
            requestRowTemplate == null ||
            serviceManager == null)
            return;

        foreach (GameObject row in createdRows)
        {
            if (row != null)
                Destroy(row);
        }

        createdRows.Clear();

        foreach (GuestServiceManager.ServiceRequest request
                 in serviceManager.ActiveRequests)
        {
            if (request == null)
                continue;

            CreateRequestRow(request);
        }
    }

    private void CreateRequestRow(
        GuestServiceManager.ServiceRequest request)
    {
        GameObject row = Instantiate(requestRowTemplate, requestList);
        row.name = "Request_" + request.Type;
        row.SetActive(true);
        createdRows.Add(row);

        TMP_Text requestText = null;
        TMP_Text buttonText = null;

        foreach (TMP_Text text in
                 row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "RequestText")
                requestText = text;

            if (text.name == "Text (TMP)")
                buttonText = text;
        }

        Button assignButton = FindAssignButton(row.transform);

        if (requestText == null || assignButton == null)
        {
            Debug.LogError(
                "GuestServiceUI: V RequestRowTemplate ch˝ba " +
                "RequestText alebo AssignButton."
            );
            return;
        }

        GameTextBinding.Set(requestText, GetServiceKey(request.Type));
            

        assignButton.onClick.RemoveAllListeners();

        if (request.IsAssigned)
        {
            assignButton.interactable = false;

            if (buttonText != null)
                GameTextBinding.Set(buttonText, "serving");
        }
        else
        {
            assignButton.interactable =
                staffManager != null &&
                staffManager.AvailableStaff > 0;

            if (buttonText != null)
                GameTextBinding.Set(buttonText, "assign");

            assignButton.onClick.AddListener(() =>
            {
                bool assigned = serviceManager.AssignRequest(request);

                if (!assigned)
                    Debug.Log("Poûiadavku sa nepodarilo priradiù.");
            });
        }
    }

    private Button FindAssignButton(Transform row)
    {
        foreach (Transform child in
                 row.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "AssignButton")
                return child.GetComponent<Button>();
        }

        return null;
    }

    private string GetServiceKey(GuestNeedType type)
    {
        switch (type)
        {
            case GuestNeedType.Food:
                return "request_restaurant";

            case GuestNeedType.Drink:
                return "request_bar";

            case GuestNeedType.Relax:
                return "request_relax";

            default:
                return "request_room";
        }
    }
}
