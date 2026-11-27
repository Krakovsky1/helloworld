using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

// One shared, asynchronously loaded translation table. No blocking Addressables loads.
public static class GameText
{
    public const string PreferenceKey = "RRH.Language";
    private static StringTable table;
    private static int request;
    public static event Action Changed;
    public static int Revision { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        table = null;
        request++;
        Revision = 0;
        Changed = null;
        LocalizationSettings.SelectedLocaleChanged -= Load;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        LocalizationSettings.SelectedLocaleChanged += Load;
        var operation = LocalizationSettings.InitializationOperation;
        operation.Completed += result =>
        {
            if (result.Status != AsyncOperationStatus.Succeeded) return;
            string saved = PlayerPrefs.GetString(PreferenceKey, "");
            if (saved == "sk" || saved == "en") Select(saved, false);
            Load(LocalizationSettings.SelectedLocale);
        };
    }

    public static void Select(string code, bool remember = true)
    {
        if (code != "sk" && code != "en") return;
        if (!LocalizationSettings.InitializationOperation.IsDone) return;
        var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
        if (locale == null) return;
        if (remember)
        {
            PlayerPrefs.SetString(PreferenceKey, code);
            PlayerPrefs.Save();
        }
        LocalizationSettings.SelectedLocale = locale;
    }

    private static void Load(Locale locale)
    {
        if (locale == null) return;
        int version = ++request;
        var operation = LocalizationSettings.StringDatabase.GetTableAsync("GameTexts", locale);
        if (operation.IsDone) Finish(operation, version);
        else operation.Completed += result => Finish(result, version);
    }

    private static void Finish(AsyncOperationHandle<StringTable> result, int version)
    {
        if (version != request || result.Status != AsyncOperationStatus.Succeeded) return;
        table = result.Result;
        Revision++;
        Changed?.Invoke();
    }

    public static string Get(string key, params object[] args) => GetOr(key, key, args);

    public static string GetOr(string key, string fallback, params object[] args)
    {
        if (table == null) return fallback;
        var entry = table.GetEntry(key);
        if (entry == null) return fallback;
        return args == null || args.Length == 0 ? entry.Value : entry.GetLocalizedString(args);
    }

    public static string BuildingKey(string name)
    {
        switch (name)
        {
            case "Restaurant": case "Reštaurácia": return "restaurant";
            case "Bar": case "Plážový bar": return "bar";
            case "Pool": case "Bazén": return "pool";
            case "Wellness": return "wellness";
            case "Hotel": return "hotel";
            default: return null;
        }
    }

    public static string BuildingName(string name)
    {
        string key = BuildingKey(name);
        return key == null ? name : GetOr(key == "bar" ? "beach_bar" : key, name);
    }
}
