using System;
using System.Collections.Generic;
using UnityEngine;

public class WaveRewardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GuestWaveManager waveManager;

    [Header("Service rewards")]
    [SerializeField] private int roomReward = 8;
    [SerializeField] private int relaxReward = 5;
    [SerializeField] private int foodReward = 3;
    [SerializeField] private int drinkReward = 2;

    [Header("Guest rewards")]
    [SerializeField] private int energyPerGuest = 2;
    [SerializeField] private int resortValuePerGuest = 1;

    private readonly List<GuestNeeds> subscribedGuests =
        new List<GuestNeeds>();

    private readonly HashSet<GuestNeed> countedNeeds =
        new HashSet<GuestNeed>();

    private int roomCount;
    private int relaxCount;
    private int foodCount;
    private int drinkCount;

    private int guestCount;
    private int roomLimit;

    private bool waveAccepted;
    private bool rewardsReady;
    private bool rewardsClaimed;

    // Event sa vyvol· aû po pripravenÌ vöetk˝ch odmien.
    public event Action RewardsBecameReady;

    public int RoomCount => roomCount;
    public int RelaxCount => relaxCount;
    public int FoodCount => foodCount;
    public int DrinkCount => drinkCount;

    public int TotalCoins =>
        roomCount * roomReward +
        relaxCount * relaxReward +
        foodCount * foodReward +
        drinkCount * drinkReward;

    public int TotalEnergy => guestCount * energyPerGuest;

    public int TotalResortValue =>
        guestCount * resortValuePerGuest;

    public bool RewardsReady => rewardsReady;
    public bool RewardsClaimed => rewardsClaimed;

    private void OnEnable()
    {
        if (waveManager == null)
        {
            Debug.LogError(
                "WaveRewardManager: Ch˝ba GuestWaveManager!"
            );
            return;
        }

        waveManager.WaveReadyForDecision += OnWaveReady;
        waveManager.WaveAccepted += OnWaveAccepted;
        waveManager.WaveRejected += OnWaveRejected;
        waveManager.GuestStayFinished += OnGuestStayFinished;
        waveManager.DepartureFinished += OnDepartureFinished;
    }

    private void OnDisable()
    {
        if (waveManager != null)
        {
            waveManager.WaveReadyForDecision -= OnWaveReady;
            waveManager.WaveAccepted -= OnWaveAccepted;
            waveManager.WaveRejected -= OnWaveRejected;
            waveManager.GuestStayFinished -= OnGuestStayFinished;
            waveManager.DepartureFinished -= OnDepartureFinished;
        }

        UnsubscribeFromGuests();
    }

    private void OnWaveReady(IReadOnlyList<GameObject> guests)
    {
        ResetRewards();

        guestCount = guests.Count;

        if (guestCount <= 10)
            roomLimit = 10;
        else if (guestCount <= 15)
            roomLimit = 15;
        else
            roomLimit = 20;

        foreach (GameObject guest in guests)
        {
            if (guest == null)
                continue;

            GuestNeeds needs = guest.GetComponent<GuestNeeds>();

            if (needs == null)
                continue;

            needs.NeedAdded += OnNeedReceived;
            subscribedGuests.Add(needs);
        }

        Debug.Log(
            "WaveRewardManager: Evidencia vlny spusten·. " +
            "Hostia: " + guestCount +
            ", Room limit: " + roomLimit
        );
    }

    private void OnWaveAccepted(IReadOnlyList<GameObject> guests)
    {
        waveAccepted = true;

        foreach (GameObject guest in guests)
        {
            if (guest == null)
                continue;

            GuestNeeds needs = guest.GetComponent<GuestNeeds>();

            if (needs == null)
                continue;

            foreach (GuestNeed need in needs.Needs)
                RegisterNeed(need);
        }

        Debug.Log("WaveRewardManager: Vlna prijat·.");
    }

    private void OnWaveRejected(IReadOnlyList<GameObject> guests)
    {
        waveAccepted = false;
        UnsubscribeFromGuests();

        Debug.Log(
            "WaveRewardManager: Vlna odmietnut·. " +
            "Odmeny sa neudeæuj˙."
        );
    }

    private void OnNeedReceived(GuestNeed need)
    {
        if (!waveAccepted || need == null)
            return;

        RegisterNeed(need);
    }

    private void RegisterNeed(GuestNeed need)
    {
        if (need == null || !countedNeeds.Add(need))
            return;

        switch (need.Type)
        {
            case GuestNeedType.Room:
                if (roomCount < roomLimit)
                    roomCount++;
                break;

            case GuestNeedType.Relax:
                relaxCount++;
                break;

            case GuestNeedType.Food:
                foodCount++;
                break;

            case GuestNeedType.Drink:
                drinkCount++;
                break;
        }

        Debug.Log(
            "WaveRewardManager: Received " + need.Type +
            " | Room: " + roomCount +
            " | Relax: " + relaxCount +
            " | Food: " + foodCount +
            " | Drink: " + drinkCount
        );
    }

    private void OnGuestStayFinished(IReadOnlyList<GameObject> guests)
    {
        UnsubscribeFromGuests();

        Debug.Log(
            "WaveRewardManager: Evidencia sluûieb ukonËen·."
        );
    }

    // Odmeny sa pripravia po dokonËenÌ odchodu lode.
    private void OnDepartureFinished()
    {
        if (!waveAccepted || rewardsReady)
            return;

        rewardsReady = true;

        Debug.Log(
            "WAVE REWARDS READY\n" +
            "Coins: " + TotalCoins +
            "\nEnergy: " + TotalEnergy +
            "\nResort value: " + TotalResortValue
        );

        // Aû teraz upozornÌme UI, ûe mÙûe zobraziù panel.
        RewardsBecameReady?.Invoke();
    }

    public void ClaimRewards()
    {
        if (!rewardsReady || rewardsClaimed)
        {
            Debug.LogWarning(
                "WaveRewardManager: Odmeny nie s˙ dostupnÈ " +
                "alebo uû boli vyplatenÈ."
            );
            return;
        }

        if (ResortResources.Instance == null)
        {
            Debug.LogError(
                "WaveRewardManager: ResortResources sa nenaöiel!"
            );
            return;
        }

        // Zamedzenie dvojitÈmu vyplateniu.
        rewardsClaimed = true;

        ResortResources.Instance.AddCoins(TotalCoins);
        ResortResources.Instance.AddEnergy(TotalEnergy);
        ResortResources.Instance.AddResortValue(TotalResortValue);

        Debug.Log(
            "WaveRewardManager: Odmeny vyplatenÈ. " +
            "Coins: " + TotalCoins +
            ", Energy: " + TotalEnergy +
            ", Resort value: " + TotalResortValue
        );
    }

    private void ResetRewards()
    {
        UnsubscribeFromGuests();
        countedNeeds.Clear();

        roomCount = 0;
        relaxCount = 0;
        foodCount = 0;
        drinkCount = 0;

        guestCount = 0;
        roomLimit = 0;

        waveAccepted = false;
        rewardsReady = false;
        rewardsClaimed = false;
    }

    private void UnsubscribeFromGuests()
    {
        foreach (GuestNeeds needs in subscribedGuests)
        {
            if (needs != null)
                needs.NeedAdded -= OnNeedReceived;
        }

        subscribedGuests.Clear();
    }
}