using UnityEditor;
using UnityEngine;

// FR-39: Menu na prácu s uloženou hrou (testovanie).
public static class SaveMenu
{
    [MenuItem("Tools/Tropical Resort/Vymazať uloženú hru")]
    static void DeleteSave()
    {
        if (!EditorUtility.DisplayDialog("Vymazať uloženú hru",
                "Naozaj vymazať uložený postup (zdroje, budovy, personál, vlnu)?\n\n" + SaveManager.SavePath,
                "Vymazať", "Zrušiť"))
            return;

        SaveManager.DeleteSave();
    }

    [MenuItem("Tools/Tropical Resort/Otvoriť priečinok s uloženou hrou")]
    static void RevealSave()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
