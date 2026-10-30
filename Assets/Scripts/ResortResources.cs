using System;
using UnityEngine;

// Mince, energia a hodnota rezortu. HUD ich len zobrazuje.
public class ResortResources : MonoBehaviour
{
    public static ResortResources Instance { get; private set; }

    public event Action Changed;

    [SerializeField] int coins = 99999;
    [SerializeField] int energy = 99999;
    [SerializeField] int resortValue = 100;

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

    public bool CanAfford(int coinCost, int energyCost)
    {
        return coins >= Mathf.Max(0, coinCost) && energy >= Mathf.Max(0, energyCost);
    }

    // Odpočíta mince aj energiu naraz, alebo nič. Vráti false, ak nie je dosť zdrojov.
    public bool TrySpend(int coinCost, int energyCost)
    {
        coinCost = Mathf.Max(0, coinCost);
        energyCost = Mathf.Max(0, energyCost);
        if (coins < coinCost || energy < energyCost) return false;
        if (coinCost == 0 && energyCost == 0) return true;

        coins -= coinCost;
        energy -= energyCost;
        Changed?.Invoke();
        return true;
    }

    void Set(ref int field, int value)
    {
        int next = Mathf.Max(0, value);
        if (field == next) return;
        field = next;
        Changed?.Invoke();
    }
}
