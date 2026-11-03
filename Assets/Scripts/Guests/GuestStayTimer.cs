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

    public bool IsRunning => isRunning;

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
            timerText.text = "NEXT WAVE IN:\n02:00";
        }

        timerCoroutine = StartCoroutine(
            Countdown(duration, isCooldown)
        );
    }

    private IEnumerator Countdown(float duration, bool isCooldown)
    {
        remainingTime = duration;
        isRunning = true;

        while (remainingTime > 0f)
        {
            UpdateTimerDisplay(isCooldown);

            yield return new WaitForSeconds(1f);

            remainingTime -= 1f;
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

        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);

        string time = $"{minutes:00}:{seconds:00}";

        if (isCooldown)
            timerText.text = "NEXT WAVE IN:\n" + time;
        else
            timerText.text = time;
    }
}