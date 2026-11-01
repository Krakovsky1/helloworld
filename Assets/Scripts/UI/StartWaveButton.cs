
using System;
using UnityEngine;
using UnityEngine.Events;

public class StartWaveButton : MonoBehaviour
{
    [SerializeField] private UnityEvent onStartWaveRequested;

    // Na túto udalosť sa napája GuestWaveManager (príchod hostí loďou).
    public static event Action WaveRequested;

    public void StartWave()
    {
        Debug.Log("Start Wave button clicked!");

        // Požiadavka na spustenie ďalšej vlny
        onStartWaveRequested?.Invoke();
        WaveRequested?.Invoke();
    }
}

