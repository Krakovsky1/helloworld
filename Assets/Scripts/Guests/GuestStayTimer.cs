using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class GuestStayTimer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text timerText;

    private Coroutine timerCoroutine;
    private float remainingTime;
    private bool isRunning;
    private bool isCooldown;

    public bool IsRunning => isRunning;

    // FR-39: pre uloženie hry - beží cooldown (true) alebo pobyt hostí (false) a koľko zostáva.
    public bool IsCooldown => isCooldown;
    public float RemainingTime => remainingTime;

    public event Action TimerFinished;
    public event Action CooldownFinished;

    private void Awake()
    {
        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    public void StartTimer(int guestCount)
    {
        float duration;

        if (guestCount <= 10)
            duration = 3 * 60f;
        else if (guestCount <= 15)
            duration = 5 * 60f;
        else
            duration = 7 * 60f;

        BeginCountdown(duration, false);
    }

    public void StartCooldown(float durationSeconds = 120f)
    {
        BeginCountdown(durationSeconds, true);
    }

    // FR-39: pokračovanie pobytu po načítaní uloženej hry (zostávajúci čas).
    public void StartStay(float durationSeconds)
    {
        BeginCountdown(Mathf.Max(0f, durationSeconds), false);
    }

    private void BeginCountdown(float duration, bool isCooldown)
    {
        if (timerText == null)
        {
            Debug.LogError("GuestStayTimer: Timer Text nie je priradený!");
            return;
        }

        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);

        timerText.gameObject.SetActive(true);

        timerText.alignment = TextAlignmentOptions.Center;

        if (isCooldown)
        {
            GameTextView.Set(timerText, () => GameText.Get("next_wave_in", "02:00"));
        }

        timerCoroutine = StartCoroutine(
            Countdown(duration, isCooldown)
        );
    }

    // FR-39: čas plynie podľa skutočného času (monotónne hodiny, ako čas servera v ServerClock),
    // nie podľa herného času - alt-tab, pauza ani Time.timeScale odpočet nezastavia
    // a zmena času v PC ho neurýchli. Po zatvorení hry: pobyt stojí, cooldown beží ďalej podľa času servera (SaveManager).
    private IEnumerator Countdown(float duration, bool isCooldown)
    {
        double endTime = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, duration);
        remainingTime = Mathf.Max(0f, duration);
        isRunning = true;
        this.isCooldown = isCooldown;

        int shownSeconds = -1;
        while (remainingTime > 0f)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                UpdateTimerDisplay(isCooldown);
            }

            yield return null;

            remainingTime = Mathf.Max(0f, (float)(endTime - Time.realtimeSinceStartupAsDouble));
        }

        remainingTime = 0f;
        UpdateTimerDisplay(isCooldown);

        isRunning = false;
        timerCoroutine = null;

        if (isCooldown)
        {
            Debug.Log("Cooldown do ďalšej vlny sa skončil.");
            CooldownFinished?.Invoke();
        }
        else
        {
            Debug.Log("Guest stay time has ended!");
            TimerFinished?.Invoke();
        }
    }

    public void HideTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        isRunning = false;

        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    private void UpdateTimerDisplay(bool isCooldown)
    {
        if (timerText == null)
            return;

        int total = Mathf.CeilToInt(remainingTime);
        int minutes = total / 60;
        int seconds = total % 60;

        string time = $"{minutes:00}:{seconds:00}";

        if (isCooldown)
            GameTextView.Set(timerText, () => GameText.Get("next_wave_in", time));
        else
            GameTextView.Set(timerText, () => time);
    }
}