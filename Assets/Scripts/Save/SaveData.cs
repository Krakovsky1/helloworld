using System;
using System.Collections.Generic;

// FR-39: Obsah uloženej hry (ukladá sa ako JSON cez JsonUtility, súbor viď SaveManager).
// Všetky časy sú čas servera (UTC, unix sekundy) z ServerClock - zmena času v PC ich neovplyvní.
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public double savedAtUnix = -1;          // čas servera pri uložení (-1 = ešte nebol známy)

    public ResourcesSave resources = new ResourcesSave();
    public List<BuildingSave> buildings = new List<BuildingSave>();     // Hotel, Restaurant, Bar
    public List<BuildingSave> buildSpots = new List<BuildingSave>();    // Pool, Wellness
    public StaffSave staff = new StaffSave();
    public WaveSave wave = new WaveSave();
    public VipSave vip = new VipSave();
}

[Serializable]
public class GuestSave
{
    public int modelIndex;                   // index v GuestWaveManager > Guest Models
    public float nextNeedIn = -1f;           // o koľko s príde ďalšia potreba (-1 = generátor nebežal)
    public List<NeedSave> needs = new List<NeedSave>();
}

[Serializable]
public class NeedSave
{
    public int type;                         // GuestNeedType
    public bool completed;
    public float age;                        // ako dlho potreba čaká (s, herný čas) - hodnotenie rýchlosti obsluhy
    public bool assigned;                    // je priradený zamestnanec
    public int stars;
    public float serviceRemaining;           // koľko s ešte potrvá obsluha
}

// Vylosovaní VIP hostia pre pripravenú (ďalšiu) vlnu a prípadne pozvaný VIP.
[Serializable]
public class VipSave
{
    public List<string> offerIds = new List<string>();   // id z VipCatalog (napr. "travel_vlogger")
    public string invitedId = "";
    public int invitedForWave;
}

[Serializable]
public class ResourcesSave
{
    public int coins;
    public int gems;
    public int energy;
    public int resortValue;
}

[Serializable]
public class BuildingSave
{
    public string label;                     // "Hotel", "Restaurant", "Bar", "Pool", "Wellness"
    public int level;                        // 1 = Level 1 (pri Pool / Wellness 0 = nepostavené)

    // Prebiehajúca stavba (constructionTarget 0 = žiadna)
    public int constructionTarget;
    public double constructionDuration;
    public double constructionEndUnix = -1;  // -1 = odpočet ešte nezačal (čakal na čas servera)
}

[Serializable]
public class StaffSave
{
    public int totalStaff = -1;              // -1 = neuložené (použije sa počiatočný počet)
}

[Serializable]
public class WaveSave
{
    public int waveNumber;

    // Rozohraná vlna
    public bool active;                      // na ostrove je vlna hostí (príchod / rozhodovanie / pobyt / odchod)
    public int guestCount;
    public int boatIndex;
    public bool decisionMade;
    public bool accepted;
    public bool departureDone;               // hostia už odplávali (beží len cooldown)

    // Časovače (koniec v čase servera; ak čas nebol známy, platí zostávajúci čas)
    public double stayEndUnix = -1;
    public float stayRemaining = -1f;
    public double cooldownEndUnix = -1;
    public float cooldownRemaining = -1f;

    // Odmeny vlny (počty obslúžených potrieb) a hodnotenia 1..5 hviezdičiek
    public int roomCount, relaxCount, foodCount, drinkCount;
    public int[] ratings = new int[6];

    // Hostia vlny a ich potreby (aj priradený personál)
    public List<GuestSave> guests = new List<GuestSave>();

    // Pripravené, ale nevyzdvihnuté odmeny - pri ďalšom spustení sa znova ukáže panel s tlačidlom CLAIM.
    public bool rewardsPending;
    public int pendingCoins, pendingEnergy, pendingResortValue;   // len na kontrolu / logy
    public int rewardGuestCount;
    public bool ratingPenaltyApplied;
    public int ratingsBelowFiveAtWaveEnd, totalRatingsAtWaveEnd;
}
