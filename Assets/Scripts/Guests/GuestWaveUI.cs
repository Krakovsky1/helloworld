
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GuestWaveUI : MonoBehaviour
{
    [Header("Wave References")]
    [SerializeField] private GuestWaveManager waveManager;
    [SerializeField] private GuestStayTimer guestStayTimer;
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform guestList;
    [SerializeField] private GameObject guestRowTemplate;
    [SerializeField] private TMP_Text guestCountText;

    [Header("Reward References")]
    [SerializeField] private WaveRewardManager rewardManager;
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private TMP_Text roomRewardText;
    [SerializeField] private TMP_Text relaxRewardText;
    [SerializeField] private TMP_Text foodRewardText;
    [SerializeField] private TMP_Text drinkRewardText;
    [SerializeField] private TMP_Text totalCoinsText;
    [SerializeField] private TMP_Text totalEnergyText;
    [SerializeField] private TMP_Text totalResortValueText;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDuration = 120f;

    private readonly Dictionary<GameObject, GuestRowUI> guestRows =
        new Dictionary<GameObject, GuestRowUI>();

    private int currentWaveGuestCount;
    private bool waveDecisionMade;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (rewardPanel != null)
            rewardPanel.SetActive(false);

        if (guestRowTemplate != null)
            guestRowTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        if (waveManager != null)
        {
            waveManager.WaveReadyForDecision += ShowWave;
        }

        if (rewardManager != null)
        {
            rewardManager.RewardsBecameReady += OnDepartureFinished;
        }

        if (guestStayTimer != null)
        {
            guestStayTimer.TimerFinished += OnGuestStayTimerFinished;
            guestStayTimer.CooldownFinished += OnCooldownFinished;
        }
    }

    private void OnDisable()
    {
        if (waveManager != null)
        {
            waveManager.WaveReadyForDecision -= ShowWave;
        }

        if (rewardManager != null)
        {
            rewardManager.RewardsBecameReady -= OnDepartureFinished;
        }

        if (guestStayTimer != null)
        {
            guestStayTimer.TimerFinished -= OnGuestStayTimerFinished;
            guestStayTimer.CooldownFinished -= OnCooldownFinished;
        }

        UnsubscribeFromGuests();
    }

    private void ShowWave(IReadOnlyList<GameObject> guests)
    {
        ClearGuestRows();

        currentWaveGuestCount = guests.Count;
        waveDecisionMade = false;

        if (panel != null)
            panel.SetActive(true);

        if (rewardPanel != null)
            rewardPanel.SetActive(false);

        if (guestCountText != null)
            guestCountText.text = guests.Count + " GUESTS";

        foreach (GameObject guest in guests)
        {
            if (guest == null)
                continue;

            CreateGuestRow(guest);
        }

        Debug.Log(
            "GuestWaveUI: Zobrazený panel pre " +
            guests.Count + " hostí."
        );
    }

    public void AcceptAll()
    {
        if (waveDecisionMade)
            return;

        if (currentWaveGuestCount <= 0)
        {
            Debug.LogWarning("Nie sú žiadni hostia na prijatie.");
            return;
        }

        if (guestStayTimer == null || waveManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: Chýba GuestStayTimer alebo GuestWaveManager."
            );
            return;
        }

        if (!waveManager.AcceptCurrentWave())
            return;

        waveDecisionMade = true;

        guestStayTimer.StartTimer(currentWaveGuestCount);

        if (panel != null)
            panel.SetActive(false);

        Debug.Log(
            "Hostia prijatí. Počet: " + currentWaveGuestCount
        );
    }

    public void RejectAll()
    {
        if (waveDecisionMade)
            return;

        if (currentWaveGuestCount <= 0)
        {
            Debug.LogWarning("Nie sú žiadni hostia na odmietnutie.");
            return;
        }

        if (guestStayTimer == null || waveManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: Chýba GuestStayTimer alebo GuestWaveManager."
            );
            return;
        }

        if (!waveManager.RejectCurrentWave())
            return;

        waveDecisionMade = true;

        if (panel != null)
            panel.SetActive(false);

        guestStayTimer.StartCooldown(cooldownDuration);

        Debug.Log(
            "Celá vlna bola odmietnutá. " +
            "Začína 2-minútový cooldown."
        );
    }

    private void OnGuestStayTimerFinished()
    {
        if (waveManager == null || guestStayTimer == null)
            return;

        waveManager.NotifyGuestStayFinished();

        guestStayTimer.StartCooldown(cooldownDuration);

        Debug.Log(
            "Pobyt hostí skončil. " +
            "Začína cooldown do ďalšej vlny."
        );
    }

    private void OnCooldownFinished()
    {
        if (waveManager != null)
            waveManager.CompleteWaveCooldown();

        if (guestStayTimer != null)
            guestStayTimer.HideTimer();

        Debug.Log(
            "Cooldown skončil. " +
            "Čaká sa na dokončenie odchodu hostí."
        );
    }

    private void OnDepartureFinished()
    {
        if (rewardManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: Nie je priradený WaveRewardManager."
            );
            return;
        }

        if (!rewardManager.RewardsReady || rewardManager.RewardsClaimed)
            return;

        RefreshRewardPanel();

        if (rewardPanel != null)
            rewardPanel.SetActive(true);

        Debug.Log(
            "GuestWaveUI: Zobrazujem výsledky vlny."
        );
    }

    private void RefreshRewardPanel()
    {
        if (rewardManager == null)
            return;

        if (roomRewardText != null)
            roomRewardText.text =
                "Room: " + rewardManager.RoomCount;

        if (relaxRewardText != null)
            relaxRewardText.text =
                "Relax: " + rewardManager.RelaxCount;

        if (foodRewardText != null)
            foodRewardText.text =
                "Food: " + rewardManager.FoodCount;

        if (drinkRewardText != null)
            drinkRewardText.text =
                "Drink: " + rewardManager.DrinkCount;

        if (totalCoinsText != null)
            totalCoinsText.text =
                "Coins: " + rewardManager.TotalCoins;

        if (totalEnergyText != null)
            totalEnergyText.text =
                "Energy: +" + rewardManager.TotalEnergy;

        if (totalResortValueText != null)
            totalResortValueText.text =
                "Resort Value: +" + rewardManager.TotalResortValue;
    }

    // Pripojené na OnClick tlačidla CLAIM.
    public void ClaimRewards()
    {
        if (rewardManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: Nie je priradený WaveRewardManager."
            );
            return;
        }

        rewardManager.ClaimRewards();

        if (rewardManager.RewardsClaimed)
        {
            if (rewardPanel != null)
                rewardPanel.SetActive(false);

            Debug.Log(
                "GuestWaveUI: Hráč si vyzdvihol odmeny."
            );
        }
    }

    private void CreateGuestRow(GameObject guest)
    {
        if (guestList == null || guestRowTemplate == null)
        {
            Debug.LogError(
                "GuestWaveUI: Chýba GuestList alebo GuestRowTemplate."
            );
            return;
        }

        GameObject row = Instantiate(guestRowTemplate, guestList);
        row.name = guest.name + "_Row";
        row.SetActive(true);

        TMP_Text[] texts =
            row.GetComponentsInChildren<TMP_Text>(true);

        TMP_Text nameText = null;
        TMP_Text needsText = null;

        foreach (TMP_Text text in texts)
        {
            if (text.name == "GuestName")
                nameText = text;

            if (text.name == "GuestNeedsText")
                needsText = text;
        }

        if (nameText == null || needsText == null)
        {
            Debug.LogError(
                "GuestWaveUI: V GuestRowTemplate chýba " +
                "GuestName alebo GuestNeedsText."
            );

            Destroy(row);
            return;
        }

        nameText.text = guest.name.Replace("Guest_", "");

        GuestNeeds needs = guest.GetComponent<GuestNeeds>();

        GuestRowUI rowUI = new GuestRowUI(needsText, needs);
        guestRows.Add(guest, rowUI);

        if (needs != null)
        {
            needs.NeedAdded += rowUI.UpdateNeeds;
            needs.NeedCompleted += rowUI.UpdateNeeds;

            rowUI.Refresh();
        }
        else
        {
            needsText.text = "No needs";
        }
    }

    private void ClearGuestRows()
    {
        UnsubscribeFromGuests();

        if (guestList != null)
        {
            foreach (Transform child in guestList)
            {
                if (child.gameObject != guestRowTemplate)
                    Destroy(child.gameObject);
            }
        }

        guestRows.Clear();
    }

    private void UnsubscribeFromGuests()
    {
        foreach (var entry in guestRows)
        {
            GuestNeeds needs = entry.Key != null
                ? entry.Key.GetComponent<GuestNeeds>()
                : null;

            if (needs != null)
            {
                needs.NeedAdded -= entry.Value.UpdateNeeds;
                needs.NeedCompleted -= entry.Value.UpdateNeeds;
            }
        }
    }

    private class GuestRowUI
    {
        private TMP_Text needsText;
        private GuestNeeds guestNeeds;

        public GuestRowUI(TMP_Text text, GuestNeeds needs)
        {
            needsText = text;
            guestNeeds = needs;
        }

        public void UpdateNeeds(GuestNeed need)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (needsText == null)
                return;

            if (guestNeeds == null)
            {
                needsText.text = "No needs";
                return;
            }

            List<string> activeNeeds = new List<string>();

            foreach (GuestNeed need in guestNeeds.Needs)
            {
                if (need.IsCompleted)
                    continue;

                activeNeeds.Add(need.Type.ToString());
            }

            needsText.text = activeNeeds.Count > 0
                ? string.Join(" • ", activeNeeds)
                : "No active needs";
        }
    }
}
