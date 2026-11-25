using System;
using UnityEngine;

// FR-29: Pravidlá stavby na čas a ceny v drahokamoch (všetko na jednom mieste, ľahko sa ladí).
//
// Čas stavby podľa úrovne, na ktorú sa budova stavia:
//   Level 0 -> 1 (nová budova) ... 1 min
//   Level 1 -> 2 ................. 2 min
//   Level 2 -> 3 ................. 10 min
//   Level 3 -> 4 a každá ďalšia .. 30 min
public static class BuildTimers
{
    // Index = cieľová úroveň (1 = postavenie novej budovy). Posledná hodnota platí pre všetky vyššie úrovne.
    static readonly int[] DurationByTargetLevel = { 0, 60, 2 * 60, 10 * 60, 30 * 60 };

    // Urýchlenie: 1 drahokam za každých začatých 5 minút zostávajúceho času (najmenej 1).
    //   1 min -> 1, 2 min -> 1, 10 min -> 2, 30 min -> 6
    public const int SecondsPerSpeedUpGem = 5 * 60;

    // Platba drahokamami namiesto mincí aj energie (pri drahokamoch sa energia neplatí):
    //   drahokamy = mince / 8 + energia / 2, zaokrúhlené nahor, najmenej 1.
    //   napr. 30 mincí + 4 energie = 6, 80 mincí + 10 energie = 15, 130 mincí + 16 energie = 25
    public const int CoinsPerGem = 8;
    public const int EnergyPerGem = 2;

    // Ako dlho trvá stavba na úroveň targetLevel (1 = nová budova).
    public static int DurationSeconds(int targetLevel)
    {
        if (targetLevel < 1) return 0;
        int index = Mathf.Min(targetLevel, DurationByTargetLevel.Length - 1);
        return DurationByTargetLevel[index];
    }

    // Koľko drahokamov stojí okamžité dokončenie pri zostávajúcom čase.
    public static int SpeedUpGemCost(double remainingSeconds)
    {
        if (remainingSeconds <= 0.0) return 0;
        return Math.Max(1, (int)Math.Ceiling(remainingSeconds / SecondsPerSpeedUpGem));
    }

    // Cena v drahokamoch namiesto mincí a energie.
    public static int GemPrice(int coinCost, int energyCost)
    {
        double gems = Math.Max(0, coinCost) / (double)CoinsPerGem + Math.Max(0, energyCost) / (double)EnergyPerGem;
        if (gems <= 0.0) return 0;
        return Math.Max(1, (int)Math.Ceiling(gems - 1e-9));
    }

    // "1 min", "8 min", "1 h", "1 h 30 min", "45 s"
    public static string FormatDuration(double seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
        if (total < 60) return total + " s";
        int hours = total / 3600;
        int minutes = (total % 3600 + 59) / 60;
        if (minutes == 60) { hours++; minutes = 0; }
        if (hours == 0) return minutes + " min";
        return minutes == 0 ? hours + " h" : hours + " h " + minutes + " min";
    }

    // Odpočet: "0:45", "7:59", "1:00:00"
    public static string FormatCountdown(double seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
        int hours = total / 3600;
        int minutes = total % 3600 / 60;
        int secs = total % 60;
        return hours > 0
            ? hours + ":" + minutes.ToString("00") + ":" + secs.ToString("00")
            : minutes + ":" + secs.ToString("00");
    }
}

// Jedna prebiehajúca stavba (postavenie alebo vylepšenie budovy).
// Koniec stavby je uložený ako čas servera (UTC unix sekundy) - zmena času v PC ho neovplyvní.
// Ak sa stavba začne skôr, ako je čas zo servera k dispozícii, odpočet sa spustí až po synchronizácii.
public class Construction
{
    public readonly int targetLevel;        // úroveň po dokončení (1 = nová budova)
    public readonly double duration;        // s
    double endUnix = double.NaN;

    public Construction(int targetLevel, double duration)
    {
        this.targetLevel = targetLevel;
        this.duration = Math.Max(0.0, duration);
        ServerClock.EnsureExists();
        TryStart();
    }

    void TryStart()
    {
        if (double.IsNaN(endUnix) && ServerClock.IsSynced)
            endUnix = ServerClock.NowUnix + duration;
    }

    // Čaká sa na čas zo servera (odpočet ešte nebeží).
    public bool WaitingForServer
    {
        get
        {
            if (duration <= 0.0) return false;
            TryStart();
            return double.IsNaN(endUnix);
        }
    }

    public double Remaining
    {
        get
        {
            if (duration <= 0.0) return 0.0;
            TryStart();
            if (double.IsNaN(endUnix)) return duration;
            return Math.Max(0.0, endUnix - ServerClock.NowUnix);
        }
    }

    public bool IsDone => !WaitingForServer && Remaining <= 0.0;

    // 0..1
    public float Progress => duration <= 0.0 ? 1f : Mathf.Clamp01(1f - (float)(Remaining / duration));

    public int SpeedUpCost => BuildTimers.SpeedUpGemCost(Remaining);

    // Čas konca v Bratislave (na zobrazenie), alebo null ak ešte nebeží.
    public DateTime? EndBratislava
    {
        get
        {
            TryStart();
            if (double.IsNaN(endUnix)) return null;
            return ServerClock.ToBratislava(endUnix);
        }
    }
}
