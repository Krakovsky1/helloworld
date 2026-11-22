using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuestServiceManager : MonoBehaviour
{
    [Header("Nastavenie obsluhy")]
    [SerializeField] private float serviceDuration = 15f;

    [Header("Manazer zamestnancov")]
    [SerializeField] private StaffManager staffManager;

    public class ServiceRequest
    {
        public GuestNeeds Guest { get; private set; }
        public GuestNeed Need { get; private set; }
        public float CreatedAt { get; private set; }
        public int Stars { get; set; }
        public bool IsAssigned { get; set; }

        public GuestNeedType Type => Need.Type;
        public string GuestName =>
            Guest != null ? Guest.gameObject.name : "Neznamy host";

        public ServiceRequest(GuestNeeds guest, GuestNeed need)
        {
            Guest = guest;
            Need = need;
            CreatedAt = need.CreatedAt;
            IsAssigned = false;
            Stars = 0;
        }
    }

    private readonly List<ServiceRequest> activeRequests =
        new List<ServiceRequest>();

    private readonly int[] ratingCounts = new int[6];
    private readonly Dictionary<ServiceRequest, Coroutine> runningServices =
        new Dictionary<ServiceRequest, Coroutine>();
    private readonly HashSet<ServiceRequest> dedicatedServices =
        new HashSet<ServiceRequest>();
    private readonly Dictionary<GuestNeeds, Action<GuestNeed>> guestHandlers =
        new Dictionary<GuestNeeds, Action<GuestNeed>>();

    private bool processingQueues;
    private bool changingTemporaryStaff;

    public IReadOnlyList<ServiceRequest> ActiveRequests => activeRequests;

    public int GetRatingCount(int stars)
    {
        return stars >= 1 && stars <= 5 ? ratingCounts[stars] : 0;
    }

    public int RatingsBelowFive =>
        ratingCounts[1] + ratingCounts[2] + ratingCounts[3] + ratingCounts[4];
    public float RatingsBelowFivePercentage =>
        TotalRatings == 0 ? 0f : (float)RatingsBelowFive / TotalRatings * 100f;
    public bool MoreThanHalfRatingsBelowFive =>
        TotalRatings > 0 && RatingsBelowFive * 2 > TotalRatings;

    public int TotalRatings
    {
        get
        {
            int total = 0;
            for (int stars = 1; stars <= 5; stars++)
                total += ratingCounts[stars];
            return total;
        }
    }

    public void ResetWaveRatings()
    {
        for (int stars = 1; stars <= 5; stars++)
            ratingCounts[stars] = 0;
    }

    public event Action OnRequestsChanged;
    public event Action<int> OnRatingRecorded;

    private void Awake()
    {
        if (staffManager == null)
            staffManager = FindAnyObjectByType<StaffManager>();
        if (staffManager == null)
            Debug.LogError("GuestServiceManager: StaffManager sa nenasiel.");
    }

    private void OnEnable()
    {
        if (staffManager == null)
            staffManager = FindAnyObjectByType<StaffManager>();
        if (staffManager != null)
            staffManager.OnStaffChanged += HandleStaffChanged;
    }

    private void Start()
    {
        ProcessQueues();
    }

    private void OnDisable()
    {
        if (staffManager != null)
            staffManager.OnStaffChanged -= HandleStaffChanged;
    }

    private void HandleStaffChanged()
    {
        if (!changingTemporaryStaff)
            ProcessQueues();
    }

    public void RegisterGuest(GuestNeeds guest)
    {
        if (guest == null || guestHandlers.ContainsKey(guest))
            return;

        Action<GuestNeed> handler = need => AddRequest(guest, need);
        guestHandlers.Add(guest, handler);
        guest.NeedAdded += handler;

        foreach (GuestNeed need in guest.Needs)
            AddRequest(guest, need);

        Debug.Log("Host zaregistrovany do sluzieb: " + guest.gameObject.name);
    }

    public void UnregisterGuest(GuestNeeds guest)
    {
        if (guest == null) return;

        if (guestHandlers.TryGetValue(guest, out Action<GuestNeed> handler))
        {
            guest.NeedAdded -= handler;
            guestHandlers.Remove(guest);
        }
    }

    public void AddRequest(GuestNeeds guest, GuestNeed need)
    {
        if (guest == null || need == null || need.IsCompleted ||
            need.Type == GuestNeedType.Room)
            return;

        foreach (ServiceRequest existing in activeRequests)
            if (existing.Need == need) return;

        activeRequests.Add(new ServiceRequest(guest, need));
        ProcessQueues();
        OnRequestsChanged?.Invoke();
    }

    // Povodne tlacidlo ASSIGN: jednorazovo priradi VOLNEHO zamestnanca
    // na 15 sekund ku konkretnej poziadavke hosta.
    public bool AssignRequest(ServiceRequest request)
    {
        if (request == null || request.IsAssigned ||
            !activeRequests.Contains(request) || staffManager == null)
            return false;

        bool assigned;
        changingTemporaryStaff = true;
        try
        {
            assigned = staffManager.TryAssignStaff();
        }
        finally
        {
            changingTemporaryStaff = false;
        }
        if (!assigned) return false;

        StartService(request, false);
        ProcessQueues();
        OnRequestsChanged?.Invoke();
        return true;
    }

    private void StartService(ServiceRequest request, bool dedicated)
    {
        float responseTime = Time.time - request.CreatedAt;
        request.Stars = CalculateStars(responseTime);
        request.IsAssigned = true;
        ratingCounts[request.Stars]++;

        if (dedicated)
            dedicatedServices.Add(request);

        OnRatingRecorded?.Invoke(request.Stars);
        runningServices.Add(request, StartCoroutine(CompleteService(request)));

        Debug.Log("Obsluha " + request.Type + " zacala. Reakcny cas: " +
                  responseTime.ToString("F1") + " s, hodnotenie " +
                  request.Stars + "/5.");
    }

    private IEnumerator CompleteService(ServiceRequest request)
    {
        yield return new WaitForSeconds(serviceDuration);

        if (request.Guest != null && request.Need != null)
        {
            for (int i = 0; i < request.Guest.Needs.Count; i++)
            {
                if (request.Guest.Needs[i] == request.Need)
                {
                    request.Guest.CompleteNeed(i);
                    break;
                }
            }
        }

        activeRequests.Remove(request);
        runningServices.Remove(request);
        bool wasDedicated = dedicatedServices.Remove(request);

        if (!wasDedicated && staffManager != null)
        {
            changingTemporaryStaff = true;
            try
            {
                staffManager.ReleaseStaff();
            }
            finally
            {
                changingTemporaryStaff = false;
            }
        }

        Debug.Log("Sluzba " + request.Type + " dokoncena.");
        ProcessQueues();
        OnRequestsChanged?.Invoke();
    }

    // FR-16: Trvalo priradeni zamestnanci automaticky beru poziadavky
    // z radu. Kazdy ma svoj sucasne vyuzivany slot kapacity.
    private void ProcessQueues()
    {
        if (processingQueues) return;
        processingQueues = true;
        bool startedAny = false;

        try
        {
            GuestNeedType[] types =
            {
                GuestNeedType.Food,
                GuestNeedType.Drink,
                GuestNeedType.Relax
            };

            foreach (GuestNeedType type in types)
            {
                int capacity = staffManager != null
                    ? staffManager.GetServiceCapacity(type, 0)
                    : 0;

                while (CountRunningDedicated(type) < capacity)
                {
                    ServiceRequest waiting = FindWaiting(type);
                    if (waiting == null) break;

                    StartService(waiting, true);
                    startedAny = true;
                }
            }

            ReportQueuesAndCapacities();
        }
        finally
        {
            processingQueues = false;
        }

        if (startedAny)
            OnRequestsChanged?.Invoke();
    }

    private ServiceRequest FindWaiting(GuestNeedType type)
    {
        foreach (ServiceRequest request in activeRequests)
        {
            if (request != null && !request.IsAssigned &&
                request.Type == type && request.Guest != null &&
                !request.Need.IsCompleted)
                return request;
        }
        return null;
    }

    private int CountRunningDedicated(GuestNeedType type)
    {
        int count = 0;
        foreach (ServiceRequest request in dedicatedServices)
            if (request.Type == type) count++;
        return count;
    }

    private int CountRunningManual(GuestNeedType type)
    {
        int count = 0;
        foreach (ServiceRequest request in runningServices.Keys)
            if (request.Type == type && !dedicatedServices.Contains(request)) count++;
        return count;
    }

    private int CountWaiting(GuestNeedType type)
    {
        int count = 0;
        foreach (ServiceRequest request in activeRequests)
            if (!request.IsAssigned && request.Type == type) count++;
        return count;
    }

    private void ReportQueuesAndCapacities()
    {
        ReportOne(GuestNeedType.Food, BottleneckService.Food);
        ReportOne(GuestNeedType.Drink, BottleneckService.Drinks);
        ReportOne(GuestNeedType.Relax, BottleneckService.Relax);

        ServiceCapacityTracker tracker = FindAnyObjectByType<ServiceCapacityTracker>();
        if (tracker != null)
        {
            int foodCapacity = GetEffectiveCapacity(GuestNeedType.Food);
            int drinkCapacity = GetEffectiveCapacity(GuestNeedType.Drink);
            int relaxCapacity = GetEffectiveCapacity(GuestNeedType.Relax);
            tracker.ReportAll(GetRunningTotal(GuestNeedType.Relax), relaxCapacity,
                              GetRunningTotal(GuestNeedType.Drink), drinkCapacity,
                              GetRunningTotal(GuestNeedType.Food), foodCapacity);
        }
    }

    private void ReportOne(GuestNeedType type, BottleneckService service)
    {
        BottleneckMonitor monitor = BottleneckMonitor.Instance;
        if (monitor == null) return;
        monitor.ReportQueue(service, CountWaiting(type));
        monitor.ReportSeats(service, GetRunningTotal(type), GetEffectiveCapacity(type));
    }

    private int GetRunningTotal(GuestNeedType type)
    {
        return CountRunningDedicated(type) + CountRunningManual(type);
    }

    private int GetEffectiveCapacity(GuestNeedType type)
    {
        int dedicatedCapacity = staffManager != null
            ? staffManager.GetServiceCapacity(type, 0)
            : 0;
        return dedicatedCapacity + CountRunningManual(type);
    }

    private static int CalculateStars(float responseTime)
    {
        if (responseTime <= 5f) return 5;
        if (responseTime <= 10f) return 4;
        if (responseTime <= 15f) return 3;
        if (responseTime <= 20f) return 2;
        return 1;
    }

    private void OnDestroy()
    {
        foreach (var pair in guestHandlers)
        {
            if (pair.Key != null)
                pair.Key.NeedAdded -= pair.Value;
        }
        foreach (var pair in runningServices)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }
        guestHandlers.Clear();
        runningServices.Clear();
        dedicatedServices.Clear();
    }
}
