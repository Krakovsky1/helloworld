
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

    [Header("Guest Service UI")]
    [SerializeField] private GuestServiceUI guestServiceUI;
    [SerializeField] private GameObject guestServicePanel;

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
    [SerializeField] private TMP_Text starRatingSummaryText;
    [SerializeField] private GuestServiceManager guestServiceManager;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDuration = 120f;

    private readonly Dictionary<GameObject, GuestRowUI> guestRows =
        new Dictionary<GameObject, GuestRowUI>();

    private int currentWaveGuestCount;
    private bool waveDecisionMade;

    // FR-39: obnova rozohranej vlny z uloženej hry (zostávajúci čas pobytu / cooldownu).
    private float restoreStaySeconds = -1f;
    private float restoreCooldownSeconds = -1f;

    public float CooldownDuration => cooldownDuration;

    // Prijatá vlna pokračuje v pobyte so zostávajúcim časom.
    public void RestoreAccepted(float staySeconds)
    {
        restoreStaySeconds = Mathf.Max(1f, staySeconds);
        AcceptAll();
        restoreStaySeconds = -1f;
    }

    // Pobyt už skončil (alebo bola vlna odmietnutá) - hostia odchádzajú, cooldown pokračuje.
    public void RestoreDeparture(bool accepted, float cooldownSeconds)
    {
        restoreCooldownSeconds = Mathf.Max(0f, cooldownSeconds);

        if (accepted)
        {
            if (waveManager != null && waveManager.AcceptCurrentWave())
            {
                waveDecisionMade = true;

                if (panel != null)
                    panel.SetActive(false);

                OnGuestStayTimerFinished();
            }
        }
        else
        {
            RejectAll();
        }

        restoreCooldownSeconds = -1f;
    }

    // Hostia už odplávali - beží len cooldown do ďalšej vlny.
    public void RestoreCooldown(float cooldownSeconds)
    {
        waveDecisionMade = true;

        if (panel != null)
            panel.SetActive(false);

        HideGuestServicePanel();

        if (guestStayTimer != null)
            guestStayTimer.StartCooldown(Mathf.Max(0f, cooldownSeconds));
    }

    private float NextCooldownDuration()
    {
        return restoreCooldownSeconds >= 0f ? restoreCooldownSeconds : cooldownDuration;
    }

    private void Awake()
    {
        if (guestServiceManager == null)
            guestServiceManager = FindAnyObjectByType<GuestServiceManager>();

        // GuestServicePanel mÃ´Å¾e byÅ¥ v scÃ©ne aktÃ­vny, preto ho hÄ¾adÃ¡me
        // aj v prÃ­pade, Å¾e jeho GameObject alebo rodiÄ je neaktÃ­vny.
        if (guestServiceUI == null)
            guestServiceUI = FindAnyObjectByType<GuestServiceUI>(FindObjectsInactive.Include);

        // Ak panel nie je priradenÃ½ samostatne, pouÅ¾ijeme objekt s GuestServiceUI.
        if (guestServicePanel == null && guestServiceUI != null)
            guestServicePanel = guestServiceUI.gameObject;

        if (panel != null)
            panel.SetActive(false);

        HideGuestServicePanel();

        if (guestServiceUI == null)
            Debug.LogError("GuestWaveUI: GuestServiceUI sa nenaÅ¡iel. Skontroluj komponent GuestServiceUI na GuestServicePanel.");

        if (rewardPanel != null)
            rewardPanel.SetActive(false);

        if (guestRowTemplate != null)
            guestRowTemplate.SetActive(false);
    }

    private void Start()
    {
        // Poistka po inicializÃ¡cii ostatnÃ½ch UI skriptov:
        // poÅ¾iadavky sa nesmÃº zobraziÅ¥ pred prijatÃ­m vlny.
        HideGuestServicePanel();
    }

    private void HideGuestServicePanel()
    {
        if (guestServicePanel != null)
            guestServicePanel.SetActive(false);
        else if (guestServiceUI != null)
            guestServiceUI.gameObject.SetActive(false);
    }

    private void ShowGuestServicePanel()
    {
        if (guestServicePanel != null)
            guestServicePanel.SetActive(true);
        else if (guestServiceUI != null)
            guestServiceUI.gameObject.SetActive(true);
        else
            Debug.LogError("GuestWaveUI: GuestServicePanel nie je priradenÃ½.");
    }

    private void OnEnable()
    {
        // Pri kaÅ¾dom zapnutÃ­ GuestWaveUI musÃ­ byÅ¥ obsluha skrytÃ¡,
        // kÃ½m hrÃ¡Ä neprijme aktuÃ¡lnu vlnu.
        HideGuestServicePanel();

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

        // PoÄas rozhodovania o vlne sa zobrazuje iba tabuÄ¾ka prÃ­chodu.
        HideGuestServicePanel();

        if (panel != null)
            panel.SetActive(true);

        if (rewardPanel != null)
            rewardPanel.SetActive(false);

        if (guestCountText != null)
            GameTextView.Set(guestCountText, () => GameText.Get("guest_count", currentWaveGuestCount));

        foreach (GameObject guest in guests)
        {
            if (guest == null)
                continue;

            CreateGuestRow(guest);
        }

        Debug.Log(
            "GuestWaveUI: ZobrazenÃ½ panel pre " +
            guests.Count + " hostÃ­."
        );
    }

    public void AcceptAll()
    {
        if (waveDecisionMade)
            return;

        if (currentWaveGuestCount <= 0)
        {
            Debug.LogWarning("Nie sÃº Å¾iadni hostia na prijatie.");
            return;
        }

        if (guestStayTimer == null || waveManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: ChÃ½ba GuestStayTimer alebo GuestWaveManager."
            );
            return;
        }

        if (!waveManager.AcceptCurrentWave())
            return;

        waveDecisionMade = true;

        // Reset ratings at the start of each accepted wave.
        if (guestServiceManager != null)
            guestServiceManager.ResetWaveRatings();

        if (restoreStaySeconds > 0f)
            guestStayTimer.StartStay(restoreStaySeconds);   // FR-39: pokračovanie po načítaní
        else
            guestStayTimer.StartTimer(currentWaveGuestCount);

        if (panel != null)
            panel.SetActive(false);

        // Obsluha sa sprÃ­stupnÃ­ aÅ¾ po ÃºspeÅ¡nom prijatÃ­ vlny.
        ShowGuestServicePanel();

        Debug.Log(
            "Hostia prijatÃ­. PoÄet: " + currentWaveGuestCount
        );
    }

    public void RejectAll()
    {
        if (waveDecisionMade)
            return;

        if (currentWaveGuestCount <= 0)
        {
            Debug.LogWarning("Nie sÃº Å¾iadni hostia na odmietnutie.");
            return;
        }

        if (guestStayTimer == null || waveManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: ChÃ½ba GuestStayTimer alebo GuestWaveManager."
            );
            return;
        }

        if (!waveManager.RejectCurrentWave())
            return;

        waveDecisionMade = true;

        if (panel != null)
            panel.SetActive(false);

        HideGuestServicePanel();

        guestStayTimer.StartCooldown(NextCooldownDuration());

        Debug.Log(
            "CelÃ¡ vlna bola odmietnutÃ¡. " +
            "ZaÄÃ­na 2-minÃºtovÃ½ cooldown."
        );
    }

    private void OnGuestStayTimerFinished()
    {
        if (waveManager == null || guestStayTimer == null)
            return;

        waveManager.NotifyGuestStayFinished();

        HideGuestServicePanel();

        guestStayTimer.StartCooldown(NextCooldownDuration());

        Debug.Log(
            "Pobyt hostÃ­ skonÄil. " +
            "ZaÄÃ­na cooldown do ÄalÅ¡ej vlny."
        );
    }

    private void OnCooldownFinished()
    {
        if (waveManager != null)
            waveManager.CompleteWaveCooldown();

        if (guestStayTimer != null)
            guestStayTimer.HideTimer();

        Debug.Log(
            "Cooldown skonÄil. " +
            "ÄakÃ¡ sa na dokonÄenie odchodu hostÃ­."
        );
    }

    private void OnDepartureFinished()
    {
        if (rewardManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: Nie je priradenÃ½ WaveRewardManager."
            );
            return;
        }

        if (!rewardManager.RewardsReady || rewardManager.RewardsClaimed)
            return;

        RefreshRewardPanel();

        if (rewardPanel != null)
            rewardPanel.SetActive(true);

        Debug.Log(
            "GuestWaveUI: Zobrazujem vÃ½sledky vlny."
        );
    }

    private void RefreshRewardPanel()
    {
        if (rewardManager == null)
            return;

        if (roomRewardText != null)
            GameTextView.Set(roomRewardText, () => GameText.Get("reward_room", rewardManager.RoomCount));

        if (relaxRewardText != null)
            GameTextView.Set(relaxRewardText, () => GameText.Get("reward_relax", rewardManager.RelaxCount));

        if (foodRewardText != null)
            GameTextView.Set(foodRewardText, () => GameText.Get("reward_food", rewardManager.FoodCount));

        if (drinkRewardText != null)
            GameTextView.Set(drinkRewardText, () => GameText.Get("reward_drink", rewardManager.DrinkCount));

        if (totalCoinsText != null)
            GameTextView.Set(totalCoinsText, () => GameText.Get("reward_coins", rewardManager.TotalCoins));

        if (totalEnergyText != null)
            GameTextView.Set(totalEnergyText, () => GameText.Get("reward_energy", rewardManager.TotalEnergy));

        if (totalResortValueText != null)
            GameTextView.Set(totalResortValueText, () => GameText.Get("reward_resort_value", rewardManager.TotalResortValue));

        RefreshStarRatingSummary();
    }

    // PripojenÃ© na OnClick tlaÄidla CLAIM.
    private void RefreshStarRatingSummary()
    {
        if (starRatingSummaryText == null)
            return;

        if (guestServiceManager == null)
            guestServiceManager = FindAnyObjectByType<GuestServiceManager>();

        if (guestServiceManager == null)
        {
            GameTextView.Set(starRatingSummaryText, () => GameText.Get("wave_summary_no_rating"));
            return;
        }

        // TextovÃ© oznaÄenia namiesto symbolov hviezdiÄiek:
        // Text labels are used instead of star symbols for font compatibility.
        GameTextView.Set(starRatingSummaryText, () =>
            "<b>" + GameText.Get("guest_rating") + "</b>\n" +
            GameText.Get("rating_5", guestServiceManager.GetRatingCount(5)) + "\n" +
            GameText.Get("rating_4", guestServiceManager.GetRatingCount(4)) + "\n" +
            GameText.Get("rating_3", guestServiceManager.GetRatingCount(3)) + "\n" +
            GameText.Get("rating_2", guestServiceManager.GetRatingCount(2)) + "\n" +
            GameText.Get("rating_1", guestServiceManager.GetRatingCount(1)) + "\n" +
            "--------------------\n" +
            "<b>" + GameText.Get("rating_total", guestServiceManager.TotalRatings) + "</b>\n" +
            GameText.Get("rating_below_five", guestServiceManager.RatingsBelowFive,
                guestServiceManager.TotalRatings, guestServiceManager.RatingsBelowFivePercentage.ToString("F1")) + "\n" +
            (rewardManager != null && rewardManager.RatingPenaltyApplied
                ? "<color=#FF6B6B><b>" + GameText.Get("reward_penalty") + "</b></color>"
                : "<color=#7CFF9B><b>" + GameText.Get("no_reward_penalty") + "</b></color>"));
    }

    public void ClaimRewards()
    {
        if (rewardManager == null)
        {
            Debug.LogError(
                "GuestWaveUI: Nie je priradenÃ½ WaveRewardManager."
            );
            return;
        }

        rewardManager.ClaimRewards();

        if (rewardManager.RewardsClaimed)
        {
            if (rewardPanel != null)
                rewardPanel.SetActive(false);

            Debug.Log(
                "GuestWaveUI: HrÃ¡Ä si vyzdvihol odmeny."
            );
        }
    }

    private void CreateGuestRow(GameObject guest)
    {
        if (guestList == null || guestRowTemplate == null)
        {
            Debug.LogError(
                "GuestWaveUI: ChÃ½ba GuestList alebo GuestRowTemplate."
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
                "GuestWaveUI: V GuestRowTemplate chÃ½ba " +
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
            GameTextView.Set(needsText, () => GameText.Get("no_needs"));
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
                GameTextView.Set(needsText, () => GameText.Get("no_needs"));
                return;
            }

            List<string> activeNeeds = new List<string>();

            foreach (GuestNeed need in guestNeeds.Needs)
            {
                if (need.IsCompleted)
                    continue;

                activeNeeds.Add("need_" + need.Type.ToString().ToLowerInvariant());
            }

            GameTextView.Set(needsText, () => activeNeeds.Count > 0
                ? string.Join(" • ", activeNeeds.ConvertAll(key => GameText.Get(key)))
                : GameText.Get("no_active_needs"));
        }
    }
}
