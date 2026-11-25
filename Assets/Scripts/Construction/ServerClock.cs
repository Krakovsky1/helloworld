using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

// FR-29: Dôveryhodný čas pre časovače stavby.
//
// Prečo nie čas počítača (DateTime.Now)? Hráč by si posunul hodiny dopredu a stavba by bola hneď hotová.
// Preto:
//  1. Pri štarte (a potom každých pár minút / po návrate do hry) sa čas stiahne z internetu
//     (časový server pre zónu Europe/Bratislava, záloha: hlavička "Date" veľkých serverov).
//  2. Medzi synchronizáciami sa čas počíta z monotónnych hodín Unity (Time.realtimeSinceStartupAsDouble),
//     ktoré zmena času v počítači neovplyvní.
//  3. Kým sa čas zo servera nepodarí získať, časovače stoja ("Čakám na čas servera…").
//     V editore sa po neúspešných pokusoch (offline) použije čas PC, aby sa dalo testovať - v builde nie.
// Interne je všetko v UTC (unix sekundy); na zobrazenie sa čas prevádza do zóny Bratislava (CET / CEST).
// Objekt sa vytvorí sám, v scéne netreba nič nastavovať.
public class ServerClock : MonoBehaviour
{
    // Zdroje času v poradí, v akom sa skúšajú.
    static readonly string WorldTimeApi = "https://worldtimeapi.org/api/timezone/Europe/Bratislava";
    static readonly string[] DateHeaderSources =
    {
        "https://www.google.com",
        "https://www.cloudflare.com",
        "https://www.microsoft.com",
    };

    const int RequestTimeout = 6;           // s
    const float RetryInterval = 10f;        // s, kým nie je čas zo servera
    const float ResyncInterval = 600f;      // s, pravidelná kontrola
    const int EditorFallbackAfterAttempts = 2;

    static ServerClock instance;

    static bool synced;
    static double syncedUnix;               // čas servera (UTC, unix s) pri poslednej synchronizácii
    static double syncedRealtime;           // monotónne hodiny v tej chvíli
    static string source = "";
    static bool usingLocalFallback;

    // Udalosť: prvá (alebo opätovná) úspešná synchronizácia.
    public static event Action Synced;

    // Je k dispozícii dôveryhodný čas? Kým nie, časovače stavby stoja.
    public static bool IsSynced => synced;

    // Aktuálny čas servera v UTC (unix sekundy). Platný len keď IsSynced.
    public static double NowUnix => synced ? syncedUnix + (Time.realtimeSinceStartupAsDouble - syncedRealtime) : 0.0;

    // Aktuálny čas v Bratislave (na zobrazenie). Ak čas ešte nie je, vráti DateTime.MinValue.
    public static DateTime BratislavaNow => synced ? ToBratislava(NowUnix) : DateTime.MinValue;

    // Odkiaľ je čas (pre logy / UI).
    public static string Source => source;
    public static bool UsingLocalFallback => usingLocalFallback;

    // ------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // Projekt má vypnutý Domain Reload - statické hodnoty treba pri štarte Play módu vynulovať.
        instance = null;
        synced = false;
        syncedUnix = 0.0;
        syncedRealtime = 0.0;
        source = "";
        usingLocalFallback = false;
        Synced = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null) return;
        var go = new GameObject("ServerClock");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ServerClock>();
    }

    // Vyžiada novú synchronizáciu hneď (napr. po návrate do hry).
    public static void RequestResync()
    {
        EnsureExists();
        instance.resyncNow = true;
    }

    bool resyncNow;
    int failedAttempts;

    void Start()
    {
        StartCoroutine(SyncLoop());
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus) resyncNow = true;
    }

    void OnApplicationPause(bool paused)
    {
        // Na mobile sa monotónne hodiny počas pauzy nemusia hýbať - po návrate čas overíme znova.
        if (!paused) resyncNow = true;
    }

    IEnumerator SyncLoop()
    {
        while (true)
        {
            yield return TrySync();

            float wait = synced && !usingLocalFallback ? ResyncInterval : RetryInterval;
            float until = Time.realtimeSinceStartup + wait;
            resyncNow = false;
            while (Time.realtimeSinceStartup < until && !resyncNow)
                yield return null;
        }
    }

    IEnumerator TrySync()
    {
        // 1) Časový server pre Bratislavu (JSON s "unixtime")
        double sent = Time.realtimeSinceStartupAsDouble;
        using (var request = UnityWebRequest.Get(WorldTimeApi))
        {
            request.timeout = RequestTimeout;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success &&
                TryParseUnixTime(request.downloadHandler.text, out double unix))
            {
                Apply(unix, sent, "worldtimeapi.org (Europe/Bratislava)");
                yield break;
            }
        }

        // 2) Hlavička "Date" (UTC) z veľkých serverov
        foreach (var url in DateHeaderSources)
        {
            sent = Time.realtimeSinceStartupAsDouble;
            using (var request = UnityWebRequest.Head(url))
            {
                request.timeout = RequestTimeout;
                yield return request.SendWebRequest();
                // Aj odpoveď s chybovým kódom (napr. 403) má platnú hlavičku Date.
                string header = request.GetResponseHeader("Date");
                if (!string.IsNullOrEmpty(header) &&
                    DateTime.TryParse(header, CultureInfo.InvariantCulture,
                                      DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                {
                    double unix = (date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    Apply(unix, sent, url);
                    yield break;
                }
            }
        }

        failedAttempts++;
        Debug.LogWarning("ServerClock: čas zo servera sa nepodarilo získať (pokus " + failedAttempts +
                         "). Časovače stavby čakajú na pripojenie k internetu.");

#if UNITY_EDITOR
        // Len v editore: offline testovanie s časom PC. V hre (build) sa čas PC nikdy nepoužije.
        if (!synced && failedAttempts >= EditorFallbackAfterAttempts)
        {
            double pcUnix = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Apply(pcUnix, Time.realtimeSinceStartupAsDouble, "čas PC (len editor, offline)");
            usingLocalFallback = true;
            Debug.LogWarning("ServerClock: EDITOR bez internetu - dočasne používam čas PC. V builde sa to nestane.");
        }
#endif
    }

    // Čas servera + polovica doby odozvy = čas v okamihu prijatia odpovede.
    static void Apply(double serverUnix, double sentRealtime, string from)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        double latency = Math.Max(0.0, (now - sentRealtime) * 0.5);
        double serverNow = serverUnix + latency;

        if (synced && !usingLocalFallback)
        {
            // Čas nikdy necúva (ochrana proti chybnej odpovedi) - menšie posuny dopredu sú v poriadku.
            double current = NowUnix;
            if (serverNow < current) serverNow = current;
        }

        bool first = !synced || usingLocalFallback;
        syncedUnix = serverNow;
        syncedRealtime = now;
        synced = true;
        usingLocalFallback = false;
        source = from;
        if (instance != null) instance.failedAttempts = 0;

        if (first)
            Debug.Log("ServerClock: čas zo servera " + from + " = " + ToBratislava(serverNow).ToString("dd.MM.yyyy HH:mm:ss") +
                      " (Bratislava)");
        Synced?.Invoke();
    }

    static readonly Regex UnixTimeRegex = new Regex("\"unixtime\"\\s*:\\s*(\\d+)");

    static bool TryParseUnixTime(string json, out double unix)
    {
        unix = 0.0;
        if (string.IsNullOrEmpty(json)) return false;
        var match = UnixTimeRegex.Match(json);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out unix);
    }

    // ------------------------------------------------------------------
    // Zóna Bratislava: CET (UTC+1), v lete CEST (UTC+2).
    // Letný čas v EÚ: od poslednej nedele v marci 01:00 UTC do poslednej nedele v októbri 01:00 UTC.
    // Počítané ručne, aby to fungovalo rovnako na Windows, macOS, Androide aj iOS (bez databázy zón).
    // ------------------------------------------------------------------

    public static DateTime ToBratislava(double unix)
    {
        var utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
        int offset = IsEuSummerTime(utc) ? 2 : 1;
        return DateTime.SpecifyKind(utc.AddHours(offset), DateTimeKind.Unspecified);
    }

    static bool IsEuSummerTime(DateTime utc)
    {
        var start = LastSunday(utc.Year, 3).AddHours(1);
        var end = LastSunday(utc.Year, 10).AddHours(1);
        return utc >= start && utc < end;
    }

    static DateTime LastSunday(int year, int month)
    {
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        return last.AddDays(-(int)last.DayOfWeek);
    }
}
