using System;
using UnityEngine;

// Mince, drahokamy, energia a hodnota rezortu. HUD ich len zobrazuje.
// FR-29: drahokamy sú prémiová mena - dajú sa použiť na urýchlenie stavby (okamžité dokončenie)
// alebo na zaplatenie stavby / vylepšenia namiesto mincí a energie (BuildTimers.GemPrice).
public class ResortResources : MonoBehaviour
{
    public static ResortResources Instance { get; private set; }

    public event Action Changed;

    [SerializeField] int coins = 1000;
    [Tooltip("FR-29: prémiová mena (drahokamy) na začiatku hry")]
    [SerializeField] int gems = 50;
    [SerializeField] int energy = 50;
    [Tooltip("Najviac energie, ktorú môže hráč mať (HUD ukazuje napr. 45/50)")]
    [SerializeField] int maxEnergy = 50;
    [SerializeField] int resortValue = 5;

    public int Coins => coins;
    public int Gems => gems;
    public int Energy => energy;
    public int MaxEnergy => Mathf.Max(1, maxEnergy);
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

    // FR-39: obnova z uloženej hry (jedna udalosť Changed na konci).
    public void Restore(int savedCoins, int savedGems, int savedEnergy, int savedResortValue)
    {
        coins = Mathf.Max(0, savedCoins);
        gems = Mathf.Max(0, savedGems);
        energy = Mathf.Clamp(savedEnergy, 0, MaxEnergy);
        resortValue = Mathf.Max(0, savedResortValue);
        Changed?.Invoke();
    }

    public void SetCoins(int value) => Set(ref coins, value);
    public void SetGems(int value) => Set(ref gems, value);
    public void SetEnergy(int value) => Set(ref energy, Mathf.Min(value, MaxEnergy));
    public void SetResortValue(int value) => Set(ref resortValue, value);

    public void AddCoins(int amount) => SetCoins(coins + amount);
    public void AddGems(int amount) => SetGems(gems + amount);
    public void AddEnergy(int amount) => SetEnergy(energy + amount);
    public void AddResortValue(int amount) => SetResortValue(resortValue + amount);

    public bool CanAfford(int coinCost, int energyCost)
    {
        return coins >= Mathf.Max(0, coinCost) && energy >= Mathf.Max(0, energyCost);
    }

    public bool CanAffordGems(int gemCost, int energyCost = 0)
    {
        return gems >= Mathf.Max(0, gemCost) && energy >= Mathf.Max(0, energyCost);
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

    // FR-29: Odpočíta drahokamy (a prípadne energiu) naraz, alebo nič.
    public bool TrySpendGems(int gemCost, int energyCost = 0)
    {
        gemCost = Mathf.Max(0, gemCost);
        energyCost = Mathf.Max(0, energyCost);
        if (gems < gemCost || energy < energyCost) return false;
        if (gemCost == 0 && energyCost == 0) return true;

        gems -= gemCost;
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
