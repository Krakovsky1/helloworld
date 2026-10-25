using System;
using UnityEngine;

// Mince, energia a hodnota rezortu. HUD ich len zobrazuje.
public class ResortResources : MonoBehaviour
{
    public static ResortResources Instance { get; private set; }

    public event Action Changed;

    [SerializeField] int coins = 150;
    [SerializeField] int energy = 40;
    [SerializeField] int resortValue = 2500;

    public int Coins => coins;
    public int Energy => energy;
    public int ResortValue => resortValue;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetCoins(int value) => Set(ref coins, value);
    public void SetEnergy(int value) => Set(ref energy, value);
    public void SetResortValue(int value) => Set(ref resortValue, value);

    public void AddCoins(int amount) => SetCoins(coins + amount);
    public void AddEnergy(int amount) => SetEnergy(energy + amount);
    public void AddResortValue(int amount) => SetResortValue(resortValue + amount);

    void Set(ref int field, int value)
    {
        int next = Mathf.Max(0, value);
        if (field == next) return;
        field = next;
        Changed?.Invoke();
    }
}
