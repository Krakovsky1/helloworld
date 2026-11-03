using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GuestWaveUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GuestWaveManager waveManager;
    [SerializeField] private GuestStayTimer guestStayTimer;
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform guestList;
    [SerializeField] private GameObject guestRowTemplate;
    [SerializeField] private TMP_Text guestCountText;

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

        if (guestRowTemplate != null)
            guestRowTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        if (waveManager != null)
            waveManager.WaveReadyForDecision += ShowWave;

        if (guestStayTimer != null)
        {
            guestStayTimer.TimerFinished += OnGuestStayTimerFinished;
            guestStayTimer.CooldownFinished += OnCooldownFinished;
        }
    }

    private void OnDisable()
    {
        if (waveManager != null)
            waveManager.WaveReadyForDecision -= ShowWave;

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

        // Najprv overíme, či manager prijatie povolí.
        if (!waveManager.AcceptCurrentWave())
            return;

        waveDecisionMade = true;

        // Spustenie času pobytu hostí.
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

        // Odmietnutie celej vlny.
        if (!waveManager.RejectCurrentWave())
            return;

        waveDecisionMade = true;

        if (panel != null)
            panel.SetActive(false);

        // Pri odmietnutí začína cooldown okamžite.
        guestStayTimer.StartCooldown(cooldownDuration);

        Debug.Log(
            "Celá vlna bola odmietnutá. Začína 2-minútový cooldown."
        );
    }

    private void OnGuestStayTimerFinished()
    {
        if (waveManager == null || guestStayTimer == null)
            return;

        // Oznámime manageru, že hosťom skončil pobyt.
        waveManager.NotifyGuestStayFinished();

        // Po skončení pobytu začína cooldown do ďalšej vlny.
        guestStayTimer.StartCooldown(cooldownDuration);

        Debug.Log(
            "Pobyt hostí skončil. Začína cooldown do ďalšej vlny."
        );
    }

    private void OnCooldownFinished()
    {
        if (waveManager != null)
            waveManager.CompleteWaveCooldown();

        if (guestStayTimer != null)
            guestStayTimer.HideTimer();

        Debug.Log(
            "Cooldown skončil. Čaká sa na dokončenie odchodu hostí."
        );
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
                "GuestWaveUI: V GuestRowTemplate chýba GuestName alebo GuestNeedsText."
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