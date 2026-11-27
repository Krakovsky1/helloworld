using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GuestServiceUI : MonoBehaviour
{
    [Header("Manažéri")]
    [SerializeField] private GuestServiceManager serviceManager;
    [SerializeField] private StaffManager staffManager;

    [Header("UI")]
    [SerializeField] private Transform requestList;
    [SerializeField] private GameObject requestRowTemplate;
    [SerializeField] private TMP_Text staffCountText;
    [SerializeField] private TMP_Text pendingRequestsCountText;

    [Header("VIP vzhľad")]
    [SerializeField] private Color vipRowColor = new Color(0.24f, 0.18f, 0.08f, 1f);
    [SerializeField] private Color vipTextColor = new Color(1f, 0.78f, 0.28f, 1f);
    [SerializeField] private Color vipOutlineColor = new Color(1f, 0.68f, 0.16f, 1f);

    private readonly List<GameObject> createdRows = new List<GameObject>();

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

        RefreshUI();
    }

    private void OnDisable()
    {
        if (serviceManager != null)
            serviceManager.OnRequestsChanged -= RefreshUI;

        if (staffManager != null)
            staffManager.OnStaffChanged -= RefreshUI;
    }

    private void RefreshUI()
    {
        RefreshStaffCount();
        RefreshRequestList();
        RefreshPendingRequestsCount();
    }

    private void RefreshPendingRequestsCount()
    {
        if (pendingRequestsCountText == null || serviceManager == null)
            return;

        GameTextBinding.Set(pendingRequestsCountText, "pending_requests_count", serviceManager.PendingRequestCount);
    }

    private void RefreshStaffCount()
    {
        if (staffCountText == null || staffManager == null)
            return;

        GameTextBinding.Set(staffCountText, "staff_count", staffManager.AvailableStaff, staffManager.TotalStaff);
    }

    private void RefreshRequestList()
    {
        if (requestList == null || requestRowTemplate == null || serviceManager == null)
            return;

        foreach (GameObject row in createdRows)
        {
            if (row != null)
                Destroy(row);
        }
        createdRows.Clear();

        foreach (GuestServiceManager.ServiceRequest request in serviceManager.ActiveRequests)
        {
            if (request != null)
                CreateRequestRow(request);
        }
    }

    private void CreateRequestRow(GuestServiceManager.ServiceRequest request)
    {
        GameObject row = Instantiate(requestRowTemplate, requestList);
        row.name = (IsVipRequest(request) ? "VIP_Request_" : "Request_") + request.Type;
        row.SetActive(true);
        createdRows.Add(row);

        TMP_Text requestText = null;
        TMP_Text buttonText = null;

        foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "RequestText")
                requestText = text;
            else if (text.name == "Text (TMP)")
                buttonText = text;
        }

        Button assignButton = FindAssignButton(row.transform);

        if (requestText == null || assignButton == null)
        {
            Debug.LogError("GuestServiceUI: V RequestRowTemplate chýba RequestText alebo AssignButton.");
            return;
        }

        VipGuestMarker vipMarker = GetVipMarker(request);
        bool isVip = vipMarker != null;

        if (isVip)
        {
            // VIP riadok zobrazuje iba označenie VIP – meno ani požiadavku nezobrazujeme.
            requestText.text = "<b>★ VIP</b>";
            requestText.color = vipTextColor;
            ApplyVipRowStyle(row);
        }
        else
        {
            GameTextBinding.Set(requestText, GetServiceKey(request.Type));
            requestText.color = Color.white;
        }

        if (request.IsAssigned)
        {
            assignButton.interactable = false;
            if (buttonText != null)
                GameTextBinding.Set(buttonText, "serving");
        }
        else
        {
            assignButton.interactable = staffManager != null && staffManager.AvailableStaff >= request.StaffRequired;
            if (buttonText != null)
                GameTextBinding.Set(buttonText, "assign");

            assignButton.onClick.RemoveAllListeners();
            assignButton.onClick.AddListener(() =>
            {
                bool assigned = serviceManager.AssignRequest(request);
                if (!assigned)
                    Debug.Log("Požiadavku sa nepodarilo priradiť.");
            });
        }
    }

    private bool IsVipRequest(GuestServiceManager.ServiceRequest request)
    {
        return GetVipMarker(request) != null;
    }

    private VipGuestMarker GetVipMarker(GuestServiceManager.ServiceRequest request)
    {
        if (request == null || request.Guest == null)
            return null;

        VipGuestMarker marker = request.Guest.GetComponent<VipGuestMarker>();
        if (marker == null)
            marker = request.Guest.GetComponentInParent<VipGuestMarker>();
        if (marker == null)
            marker = request.Guest.GetComponentInChildren<VipGuestMarker>(true);

        return marker;
    }

    private void ApplyVipRowStyle(GameObject row)
    {
        // Uprednostníme Image priamo na riadku, aby sme omylom nezafarbili tlačidlo ASSIGN.
        Image background = row.GetComponent<Image>();

        if (background != null)
        {
            background.color = vipRowColor;

            // Outline musí byť na rovnakom GameObjecte ako Image/Graphic.
            Outline outline = background.GetComponent<Outline>();
            if (outline == null)
                outline = background.gameObject.AddComponent<Outline>();

            outline.effectColor = vipOutlineColor;
            outline.effectDistance = new Vector2(3f, 3f);
            outline.useGraphicAlpha = true;
        }
        else
        {
            // Ak template nemá Image na koreni, zvýrazníme aspoň samotný text.
            Outline outline = row.GetComponent<Outline>();
            if (outline == null)
                outline = row.AddComponent<Outline>();

            outline.effectColor = vipOutlineColor;
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = true;
        }
    }

    private Button FindAssignButton(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
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
