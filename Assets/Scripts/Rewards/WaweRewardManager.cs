using System;
using System.Collections.Generic;
using UnityEngine;

public class WaveRewardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GuestWaveManager waveManager;
    [SerializeField] private GuestServiceManager guestServiceManager;

    [Header("Rating penalty")]
    [SerializeField, Range(0f, 100f)] private float ratingPenaltyPercent = 20f;

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
    private bool ratingPenaltyApplied;
    private int ratingsBelowFiveAtWaveEnd;
    private int totalRatingsAtWaveEnd;

    // Event sa vyvolá a po pripravení vetkých odmien.
    public event Action RewardsBecameReady;

    public int RoomCount => roomCount;
    public int RelaxCount => relaxCount;
    public int FoodCount => foodCount;
    public int DrinkCount => drinkCount;

    private int BaseTotalCoins =>
        roomCount * roomReward +
        relaxCount * relaxReward +
        foodCount * foodReward +
        drinkCount * drinkReward;

    private int BaseTotalEnergy => guestCount * energyPerGuest;

    private int BaseTotalResortValue =>
        guestCount * resortValuePerGuest;

    // Penalizácia sa vyhodnotí po skončení vlny a platí pre všetky odmeny.
    public int TotalCoins => ApplyRatingPenalty(BaseTotalCoins);
    public int TotalEnergy => ApplyRatingPenalty(BaseTotalEnergy);
    public int TotalResortValue => ApplyRatingPenalty(BaseTotalResortValue);

    public bool RatingPenaltyApplied => ratingPenaltyApplied;
    public int RatingsBelowFiveAtWaveEnd => ratingsBelowFiveAtWaveEnd;
    public int TotalRatingsAtWaveEnd => totalRatingsAtWaveEnd;
    public float RatingsBelowFivePercentage =>
        totalRatingsAtWaveEnd == 0
            ? 0f
            : (float)ratingsBelowFiveAtWaveEnd / totalRatingsAtWaveEnd * 100f;

    public bool RewardsReady => rewardsReady;
    public bool RewardsClaimed => rewardsClaimed;

    // FR-39: uloženie hry - počty obslúžených potrieb aktuálnej vlny.
    public void CaptureSave(WaveSave save)
    {
        save.roomCount = roomCount;
        save.relaxCount = relaxCount;
        save.foodCount = foodCount;
        save.drinkCount = drinkCount;

        save.rewardsPending = rewardsReady && !rewardsClaimed;
        save.pendingCoins = save.rewardsPending ? TotalCoins : 0;
        save.pendingEnergy = save.rewardsPending ? TotalEnergy : 0;
        save.pendingResortValue = save.rewardsPending ? TotalResortValue : 0;
        save.rewardGuestCount = guestCount;
        save.ratingPenaltyApplied = ratingPenaltyApplied;
        save.ratingsBelowFiveAtWaveEnd = ratingsBelowFiveAtWaveEnd;
        save.totalRatingsAtWaveEnd = totalRatingsAtWaveEnd;
    }

    // FR-39: odmeny, ktoré hráč pred zatvorením hry nevyzdvihol - znova sa ukáže panel s CLAIM.
    public void RestorePendingRewards(WaveSave save)
    {
        if (save == null || !save.rewardsPending)
            return;

        ResetRewards();

        roomCount = save.roomCount;
        relaxCount = save.relaxCount;
        foodCount = save.foodCount;
        drinkCount = save.drinkCount;
        guestCount = save.rewardGuestCount;

        ratingPenaltyApplied = save.ratingPenaltyApplied;
        ratingsBelowFiveAtWaveEnd = save.ratingsBelowFiveAtWaveEnd;
        totalRatingsAtWaveEnd = save.totalRatingsAtWaveEnd;

        waveAccepted = true;
        rewardsReady = true;
        rewardsClaimed = false;

        Debug.Log("FR-39 | Nevyzdvihnuté odmeny čakajú: " + TotalCoins + " mincí, " + TotalEnergy +
                  " energie, " + TotalResortValue + " hodnoty rezortu.");
        RewardsBecameReady?.Invoke();
    }

    // FR-39: po obnovení prijatej vlny vráti počty obslúžených potrieb (nové potreby sa pripočítajú).
    public void RestoreCounts(WaveSave save)
    {
        roomCount = Mathf.Max(roomCount, save.roomCount);
        relaxCount = Mathf.Max(relaxCount, save.relaxCount);
        foodCount = Mathf.Max(foodCount, save.foodCount);
        drinkCount = Mathf.Max(drinkCount, save.drinkCount);
    }

    private void OnEnable()
    {
        if (guestServiceManager == null)
            guestServiceManager = FindAnyObjectByType<GuestServiceManager>();

        if (guestServiceManager == null)
            Debug.LogError("WaveRewardManager: GuestServiceManager sa nenašiel. Penalizácia hodnotení nebude dostupná.");

        if (waveManager == null)
        {
            Debug.LogError(
                "WaveRewardManager: Chýba GuestWaveManager!"
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
            "WaveRewardManager: Evidencia vlny spustená. " +
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

        Debug.Log("WaveRewardManager: Vlna prijatá.");
    }

    private void OnWaveRejected(IReadOnlyList<GameObject> guests)
    {
        waveAccepted = false;
        UnsubscribeFromGuests();

        Debug.Log(
            "WaveRewardManager: Vlna odmietnutá. " +
            "Odmeny sa neude¾ujú."
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
            "WaveRewardManager: Evidencia sluieb ukonèená."
        );
    }

    // Odmeny sa pripravia po dokonèení odchodu lode.
    private void OnDepartureFinished()
    {
        if (!waveAccepted || rewardsReady)
            return;

        EvaluateRatingPenalty();
        rewardsReady = true;

        Debug.Log(
            "WAVE REWARDS READY\n" +
            "Ratings below 5: " + ratingsBelowFiveAtWaveEnd +
            "/" + totalRatingsAtWaveEnd +
            " (" + RatingsBelowFivePercentage.ToString("F1") + "%)" +
            "\nPenalty applied: " + ratingPenaltyApplied +
            "\nCoins: " + TotalCoins +
            "\nEnergy: " + TotalEnergy +
            "\nResort value: " + TotalResortValue
        );

        // A teraz upozorníme UI, e môe zobrazi panel.
        RewardsBecameReady?.Invoke();
    }

    public void ClaimRewards()
    {
        if (!rewardsReady || rewardsClaimed)
        {
            Debug.LogWarning(
                "WaveRewardManager: Odmeny nie sú dostupné " +
                "alebo u boli vyplatené."
            );
            return;
        }

        if (ResortResources.Instance == null)
        {
            Debug.LogError(
                "WaveRewardManager: ResortResources sa nenaiel!"
            );
            return;
        }

        // Zamedzenie dvojitému vyplateniu.
        rewardsClaimed = true;
        SaveManager.RequestSave();   // FR-39

        ResortResources.Instance.AddCoins(TotalCoins);
        ResortResources.Instance.AddEnergy(TotalEnergy);
        ResortResources.Instance.AddResortValue(TotalResortValue);

        Debug.Log(
            "WaveRewardManager: Odmeny vyplatené. " +
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
        ratingPenaltyApplied = false;
        ratingsBelowFiveAtWaveEnd = 0;
        totalRatingsAtWaveEnd = 0;
    }

    private void EvaluateRatingPenalty()
    {
        if (guestServiceManager == null)
        {
            ratingPenaltyApplied = false;
            ratingsBelowFiveAtWaveEnd = 0;
            totalRatingsAtWaveEnd = 0;
            Debug.LogWarning("WaveRewardManager: Bez GuestServiceManager sa penalizácia nedá vyhodnotiť.");
            return;
        }

        ratingsBelowFiveAtWaveEnd = guestServiceManager.RatingsBelowFive;
        totalRatingsAtWaveEnd = guestServiceManager.TotalRatings;

        // Presne 50 % nestačí. Penalizácia platí iba pri viac ako 50 %.
        ratingPenaltyApplied =
            totalRatingsAtWaveEnd > 0 &&
            ratingsBelowFiveAtWaveEnd * 2 > totalRatingsAtWaveEnd;
    }

    private int ApplyRatingPenalty(int baseReward)
    {
        if (!ratingPenaltyApplied)
            return baseReward;

        float multiplier = 1f - ratingPenaltyPercent / 100f;
        return Mathf.FloorToInt(baseReward * multiplier);
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