
using UnityEngine;
using UnityEngine.Events;

public class StartWaveButton : MonoBehaviour
{
    [SerializeField] private UnityEvent onStartWaveRequested;

    public void StartWave()
    {
        Debug.Log("Start Wave button clicked!");

        // Požiadavka na spustenie ďalšej vlny
        onStartWaveRequested?.Invoke();
    }
}

