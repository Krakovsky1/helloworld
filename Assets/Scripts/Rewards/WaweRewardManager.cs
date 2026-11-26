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

    // Každý hosť má vlastný handler, aby sme pri novej potrebe poznali jeho VIP násobiteľ.
    private readonly Dictionary<GuestNeeds, Action<GuestNeed>> needAddedHandlers =
        new Dictionary<GuestNeeds, Action<GuestNeed>>();

    private readonly HashSet<GuestNeed> countedNeeds =
        new HashSet<GuestNeed>();

    private int roomCount;
    private int relaxCount;
    private int foodCount;
    private int drinkCount;

    private int guestCount;

    // Oddelené VIP počty umožňujú násobiť iba odmeny, ktoré vytvoril VIP hosť.
    private int vipGuestCount;
    private int vipRoomCount;
    private int vipRelaxCount;
    private int vipFoodCount;
    private int vipDrinkCount;
    private float vipGuestMultiplierSum;
    private float vipRoomMultiplierSum;
    private float vipRelaxMultiplierSum;
    private float vipFoodMultiplierSum;
    private float vipDrinkMultiplierSum;

    // Po obnovení nevyzdvihnutej odmeny použijeme presné uložené sumy.
    private bool hasRestoredRewardTotals;
    private int restoredTotalCoins;
    private int restoredTotalEnergy;
    private int restoredTotalResortValue;

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

    private int BaseTotalCoins => Mathf.FloorToInt(
        (roomCount - vipRoomCount) * roomReward + vipRoomMultiplierSum * roomReward +
        (relaxCount - vipRelaxCount) * relaxReward + vipRelaxMultiplierSum * relaxReward +
        (foodCount - vipFoodCount) * foodReward + vipFoodMultiplierSum * foodReward +
        (drinkCount - vipDrinkCount) * drinkReward + vipDrinkMultiplierSum * drinkReward
    );

    private int BaseTotalEnergy => Mathf.FloorToInt(
        (guestCount - vipGuestCount) * energyPerGuest + vipGuestMultiplierSum * energyPerGuest
    );

    private int BaseTotalResortValue => Mathf.FloorToInt(
        (guestCount - vipGuestCount) * resortValuePerGuest + vipGuestMultiplierSum * resortValuePerGuest
    );

    // Základné odmeny bez VIP násobiteľa - používajú sa na výpočet
    // samostatne zobraziteľného VIP bonusu.
    private int BaseCoinsWithoutVipBonus =>
        roomCount * roomReward +
        relaxCount * relaxReward +
        foodCount * foodReward +
        drinkCount * drinkReward;

    private int BaseEnergyWithoutVipBonus => guestCount * energyPerGuest;
    private int BaseResortValueWithoutVipBonus => guestCount * resortValuePerGuest;

    // VIP násobiteľ sa uplatní pred všeobecnou penalizáciou hodnotení.
    public int TotalCoins => hasRestoredRewardTotals ? restoredTotalCoins : ApplyRatingPenalty(BaseTotalCoins);
    public int TotalEnergy => hasRestoredRewardTotals ? restoredTotalEnergy : ApplyRatingPenalty(BaseTotalEnergy);
    public int TotalResortValue => hasRestoredRewardTotals ? restoredTotalResortValue : ApplyRatingPenalty(BaseTotalResortValue);

    // Hodnoty, ktoré hráč získal navyše výhradne vďaka VIP násobiteľu.
    // Sú už po prípadnej rating penalizácii, takže sa rovnajú rozdielu
    // medzi konečnou odmenou s VIP a odmenou bez VIP.
    public bool HasVipGuest => vipGuestCount > 0;
    public float VipRewardMultiplier => vipGuestCount > 0
        ? vipGuestMultiplierSum / vipGuestCount
        : 1f;
    public int VipBonusCoins => hasRestoredRewardTotals ? 0 :
        TotalCoins - ApplyRatingPenalty(BaseCoinsWithoutVipBonus);
    public int VipBonusEnergy => hasRestoredRewardTotals ? 0 :
        TotalEnergy - ApplyRatingPenalty(BaseEnergyWithoutVipBonus);
    public int VipBonusResortValue => hasRestoredRewardTotals ? 0 :
        TotalResortValue - ApplyRatingPenalty(BaseResortValueWithoutVipBonus);

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

        // Uložené sumy už obsahujú VIP násobiteľ aj rating penalizáciu.
        hasRestoredRewardTotals = true;
        restoredTotalCoins = save.pendingCoins;
        restoredTotalEnergy = save.pendingEnergy;
        restoredTotalResortValue = save.pendingResortValue;

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

        foreach (GameObject guest in guests)
        {
            if (guest == null)
                continue;

            GuestNeeds needs = guest.GetComponent<GuestNeeds>();

            if (needs == null)
                continue;

            VipGuestMarker vipMarker = guest.GetComponent<VipGuestMarker>();
            bool isVip = vipMarker != null && vipMarker.Personality != null;
            float multiplier = isVip
                ? Mathf.Max(1f, vipMarker.Personality.rewardMultiplier)
                : 1f;

            if (isVip)
            {
                vipGuestCount++;
                vipGuestMultiplierSum += multiplier;
                Debug.Log("WaveRewardManager: VIP " + vipMarker.Personality.displayName +
                          " | reward multiplier x" + multiplier);
            }

            Action<GuestNeed> handler = need => OnNeedCompleted(need, isVip, multiplier);
            // Odmenu za službu započítame až vtedy, keď je potreba skutočne dokončená.
            needs.NeedCompleted += handler;
            needAddedHandlers[needs] = handler;
            subscribedGuests.Add(needs);
        }

        Debug.Log(
            "WaveRewardManager: Evidencia vlny spustená. " +
            "Hostia: " + guestCount +
            ", Guaranteed rooms: " + guestCount
        );
    }

    private void OnWaveAccepted(IReadOnlyList<GameObject> guests)
    {
        waveAccepted = true;

        // Kazdy host ma automaticky izbu: pocet izieb sa rovna velkosti vlny.
        // VIP izby si zachovavaju VIP nasobitel, rovnako ako ostatne VIP odmeny.
        roomCount = guestCount;
        vipRoomCount = vipGuestCount;
        vipRoomMultiplierSum = vipGuestMultiplierSum;

        foreach (GameObject guest in guests)
        {
            if (guest == null)
                continue;

            GuestNeeds needs = guest.GetComponent<GuestNeeds>();

            if (needs == null)
                continue;

            VipGuestMarker vipMarker = guest.GetComponent<VipGuestMarker>();
            bool isVip = vipMarker != null && vipMarker.Personality != null;
            float multiplier = isVip
                ? Mathf.Max(1f, vipMarker.Personality.rewardMultiplier)
                : 1f;

            foreach (GuestNeed need in needs.Needs)
            {
                if (need != null && need.IsCompleted)
                    RegisterNeed(need, isVip, multiplier);
            }
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

    private void OnNeedCompleted(GuestNeed need, bool isVip, float multiplier)
    {
        if (!waveAccepted || need == null)
            return;

        RegisterNeed(need, isVip, multiplier);
    }

    private void RegisterNeed(GuestNeed need, bool isVip, float multiplier)
    {
        if (need == null || !countedNeeds.Add(need))
            return;

        switch (need.Type)
        {
            case GuestNeedType.Room:
                // Izba je garantovana pre kazdeho hosta pri prijati vlny.
                // Dokoncenie Room potreby preto nesmie zapocitat druhu izbu.
                break;

            case GuestNeedType.Relax:
                relaxCount++;
                if (isVip)
                {
                    vipRelaxCount++;
                    vipRelaxMultiplierSum += multiplier;
                }
                break;

            case GuestNeedType.Food:
                foodCount++;
                if (isVip)
                {
                    vipFoodCount++;
                    vipFoodMultiplierSum += multiplier;
                }
                break;

            case GuestNeedType.Drink:
                drinkCount++;
                if (isVip)
                {
                    vipDrinkCount++;
                    vipDrinkMultiplierSum += multiplier;
                }
                break;
        }

        Debug.Log(
            "WaveRewardManager: Received " + need.Type +
            (isVip ? " from VIP (x" + multiplier + ")" : " from regular guest") +
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
            "\nCoins: " + TotalCoins + " (VIP bonus: +" + VipBonusCoins + ")" +
            "\nEnergy: " + TotalEnergy + " (VIP bonus: +" + VipBonusEnergy + ")" +
            "\nResort value: " + TotalResortValue + " (VIP bonus: +" + VipBonusResortValue + ")"
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

        vipGuestCount = 0;
        vipRoomCount = 0;
        vipRelaxCount = 0;
        vipFoodCount = 0;
        vipDrinkCount = 0;
        vipGuestMultiplierSum = 0f;
        vipRoomMultiplierSum = 0f;
        vipRelaxMultiplierSum = 0f;
        vipFoodMultiplierSum = 0f;
        vipDrinkMultiplierSum = 0f;

        hasRestoredRewardTotals = false;
        restoredTotalCoins = 0;
        restoredTotalEnergy = 0;
        restoredTotalResortValue = 0;

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
            if (needs != null && needAddedHandlers.TryGetValue(needs, out Action<GuestNeed> handler))
                needs.NeedCompleted -= handler;
        }

        subscribedGuests.Clear();
        needAddedHandlers.Clear();
    }
}