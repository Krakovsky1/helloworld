using UnityEngine;

/// <summary>
/// FR-07: Displays the current guest wave in the existing ServiceCapacityPanel.
/// Does not invent service/drink/food occupancy data.
/// </summary>
[RequireComponent(typeof(ServiceCapacityPanel))]
public class ServiceCapacityWaveLink : MonoBehaviour
{
    [SerializeField] private GuestWaveManager waveManager;

    private ServiceCapacityPanel panel;
    private int lastDisplayedWave = -1;

    private void Awake()
    {
        panel = GetComponent<ServiceCapacityPanel>();
    }

    private void Update()
    {
        // The wave manager may be created later in the scene.
        if (waveManager == null)
            waveManager = FindAnyObjectByType<GuestWaveManager>();

        // 0 means no wave is currently running. WaveNumber by itself is the
        // total number of waves started, even after a wave has finished.
        int activeWave = waveManager != null && waveManager.WaveRunning
            ? waveManager.WaveNumber
            : 0;

        if (activeWave == lastDisplayedWave)
            return;

        lastDisplayedWave = activeWave;
        panel.SetWaveNumber(activeWave);
    }
}
