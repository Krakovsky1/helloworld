
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuestServiceManager : MonoBehaviour
{
    [Header("Nastavenie obsluhy")]
    [SerializeField] private float serviceDuration = 15f;

    [Header("Manaér zamestnancov")]
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
            Guest != null ? Guest.gameObject.name : "Neznámy hos";

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

    // Per-wave counts for ratings from 1 to 5 stars.
    private readonly int[] ratingCounts = new int[6];

    private readonly Dictionary<ServiceRequest, Coroutine> runningServices =
        new Dictionary<ServiceRequest, Coroutine>();

    private readonly Dictionary<GuestNeeds, Action<GuestNeed>> guestHandlers =
        new Dictionary<GuestNeeds, Action<GuestNeed>>();

    public IReadOnlyList<ServiceRequest> ActiveRequests => activeRequests;

    public int GetRatingCount(int stars)
    {
        if (stars < 1 || stars > 5)
            return 0;
        return ratingCounts[stars];
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
            Debug.LogError("GuestServiceManager: StaffManager sa nenaiel.");
    }

    public void RegisterGuest(GuestNeeds guest)
    {
        if (guest == null || guestHandlers.ContainsKey(guest))
            return;

        Action<GuestNeed> handler = need =>
        {
            AddRequest(guest, need);
        };

        guestHandlers.Add(guest, handler);
        guest.NeedAdded += handler;

        // Zaregistruje aj u existujúce aktívne potreby.
        foreach (GuestNeed need in guest.Needs)
        {
            AddRequest(guest, need);
        }

        Debug.Log("Hos zaregistrovaný do systému sluieb: " +
                  guest.gameObject.name);
    }

    public void UnregisterGuest(GuestNeeds guest)
    {
        if (guest == null)
            return;

        if (guestHandlers.TryGetValue(guest, out Action<GuestNeed> handler))
        {
            guest.NeedAdded -= handler;
            guestHandlers.Remove(guest);
        }
    }

    public void AddRequest(GuestNeeds guest, GuestNeed need)
    {
        if (guest == null || need == null || need.IsCompleted)
            return;

        // Izby riei existujúci hotelový systém.
        if (need.Type == GuestNeedType.Room)
            return;

        foreach (ServiceRequest existing in activeRequests)
        {
            if (existing.Need == need)
                return;
        }

        ServiceRequest request = new ServiceRequest(guest, need);
        activeRequests.Add(request);

        Debug.Log("Nová poiadavka: " + need.Type +
                  " od hosa " + guest.gameObject.name);

        OnRequestsChanged?.Invoke();
    }

    public bool AssignRequest(ServiceRequest request)
    {
        if (request == null || request.IsAssigned)
            return false;

        if (!activeRequests.Contains(request))
            return false;

        if (staffManager == null)
        {
            Debug.LogError("GuestServiceManager: Chýba StaffManager.");
            return false;
        }

        if (!staffManager.TryAssignStaff())
            return false;

        float responseTime = Time.time - request.CreatedAt;

        request.Stars = CalculateStars(responseTime);
        request.IsAssigned = true;
        ratingCounts[request.Stars]++;

        Debug.Log("Poiadavka " + request.Type +
                  " bola priradená. Reakèný èas: " +
                  responseTime.ToString("F1") +
                  " s. Hodnotenie: " + request.Stars + "/5");

        OnRatingRecorded?.Invoke(request.Stars);

        Coroutine routine = StartCoroutine(CompleteService(request));
        runningServices.Add(request, routine);

        OnRequestsChanged?.Invoke();

        return true;
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

        if (staffManager != null)
            staffManager.ReleaseStaff();

        Debug.Log("Sluba " + request.Type +
                  " dokonèená. Zamestnanec je opä dostupný.");

        OnRequestsChanged?.Invoke();
    }

    private int CalculateStars(float responseTime)
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
    }
}
