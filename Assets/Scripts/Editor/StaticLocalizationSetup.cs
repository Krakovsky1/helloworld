using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;

// Editor-only setup for the fixed labels in StarterIsland (NFR-24).
public static class StaticLocalizationSetup
{
    private const string TableName = "GameTexts";
    private static readonly Dictionary<string, string> Keys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "START WAVE", "start_wave" },
            { "SERVICE CAPACITY", "service_capacity" },
            { "SERVICES", "services" },
            { "DRINKS", "drinks" },
            { "FOOD", "food" },
            { "TROPICAL RESORT", "resort_title" },
            { "GUEST ARRIVAL", "guest_arrival" },
            { "ACCEPT ALL", "accept_all" },
            { "REJECT ALL", "reject_all" },
            { "CLAIM", "claim" },
            { "WAVE REWARD", "wave_reward" },
            { "WAWE REWARD", "wave_reward" },
            { "NO ACTIVE WAVE", "no_active_wave" }
        };

    [MenuItem("Tools/Tropical Resort/Localization/Link Static UI")]
    public static void LinkStaticUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before linking localization.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/StarterIsland.unity")
        {
            Debug.LogWarning("Open StarterIsland before running this tool.");
            return;
        }

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null || collection.SharedData == null)
        {
            Debug.LogError("GameTexts string table collection was not found.");
            return;
        }

        var setterMethod = typeof(TMP_Text).GetProperty("text")?.GetSetMethod();
        if (setterMethod == null)
        {
            Debug.LogError("TextMeshPro text setter was not found.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Link static UI localization");
        int linked = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string value = (label.text ?? "").Trim();
                if (!Keys.TryGetValue(value, out string key))
                {
                    var parent = label.transform.parent;
                    if (parent == null || !HasAncestor(label.transform, "ServiceCapacityPanel")) continue;
                    if (value == "S") key = "service_initial";
                    else if (value == "D" || value == "N") key = "drink_initial";
                    else if (value == "J" || value == "F") key = "food_initial";
                    else continue;
                }
                if (label.GetComponent<LocalizeStringEvent>() != null)
                    continue;
                if (collection.SharedData.GetEntry(key) == null)
                {
                    Debug.LogWarning("Missing localization key: " + key, label);
                    continue;
                }

                // A real property setter, not a lambda: Unity can serialize this listener.
                var setter = (UnityAction<string>)Delegate.CreateDelegate(
                    typeof(UnityAction<string>), label, setterMethod);
                var localizer = Undo.AddComponent<LocalizeStringEvent>(label.gameObject);
                Undo.RecordObject(localizer, "Configure localization");
                localizer.StringReference = new LocalizedString(TableName, key);
                UnityEventTools.AddPersistentListener<string>(localizer.OnUpdateString, setter);
                int index = localizer.OnUpdateString.GetPersistentEventCount() - 1;
                localizer.OnUpdateString.SetPersistentListenerState(index, UnityEventCallState.EditorAndRuntime);
                EditorUtility.SetDirty(localizer);
                if (PrefabUtility.IsPartOfPrefabInstance(localizer))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(localizer);
                Debug.Log("Localized: " + label.gameObject.name + " -> " + key, label);
                linked++;
            }
        }

        if (linked > 0) EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("Static localization: " + linked + " new labels linked. Existing links were preserved. Review the scene and save it manually.");
    }
    private static bool HasAncestor(Transform item, string name)
    {
        for (var t = item.parent; t != null; t = t.parent)
            if (t.name == name) return true;
        return false;
    }
}
