// Unity: Tools > Tropical Resort > Setup Building Upgrades
// Vytvorí (alebo opraví) BuildingUpgradeManager v otvorenej scéne a priradí mu modely úrovní a ikonky.
// Je to záloha k už pripravenej scéne StarterIsland, a tiež spôsob, ako rýchlo zapojiť manažér do inej scény.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildingUpgradeSetup
{
    const string Root = "Assets/Buildings/TropicalResort_Buildings/";

    [MenuItem("Tools/Tropical Resort/Setup Building Upgrades")]
    public static void Setup()
    {
        var manager = Object.FindFirstObjectByType<BuildingUpgradeManager>();
        if (manager == null)
        {
            var go = new GameObject("BuildingUpgradeManager");
            Undo.RegisterCreatedObjectUndo(go, "Setup Building Upgrades");
            manager = go.AddComponent<BuildingUpgradeManager>();
        }

        var so = new SerializedObject(manager);
        var types = so.FindProperty("types");
        types.arraySize = 3;

        Fill(types.GetArrayElementAtIndex(0), "Hotel", "UI/Icons/building_hotel", new[] { 30, 50, 80 }, new[] { 4, 6, 10 }, new[]
        {
            Root + "Hotels/BeachHotel_L1.obj",
            Root + "Hotels/BeachHotel_L2.obj",
            Root + "Hotels/BeachHotel_L3.obj",
            Root + "Hotels/BeachHotel_L4.obj",
        });
        Fill(types.GetArrayElementAtIndex(1), "Restaurant", "UI/Icons/building_restaurant", new[] { 25, 45 }, new[] { 3, 5 }, new[]
        {
            Root + "Gastronomy/Restaurant.obj",
            Root + "Gastronomy/Restaurant_L2.obj",
            Root + "Gastronomy/Restaurant_L3.obj",
        });
        Fill(types.GetArrayElementAtIndex(2), "Bar", "UI/Icons/building_bar", new[] { 20, 40 }, new[] { 2, 4 }, new[]
        {
            Root + "Gastronomy/BeachBar.obj",
            Root + "Gastronomy/BeachBar_L2.obj",
            Root + "Gastronomy/BeachBar_L3.obj",
        });

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("Building upgrades: manažér je zapojený (3 typy budov). Ulož scénu.");
    }

    static void Fill(SerializedProperty type, string label, string iconResource, int[] coins, int[] energy, string[] modelPaths)
    {
        type.FindPropertyRelative("label").stringValue = label;
        type.FindPropertyRelative("iconResource").stringValue = iconResource;
        SetInts(type.FindPropertyRelative("upgradeCoins"), coins);
        SetInts(type.FindPropertyRelative("upgradeEnergy"), energy);

        var levels = type.FindPropertyRelative("levels");
        levels.arraySize = modelPaths.Length;
        for (int i = 0; i < modelPaths.Length; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[i]);
            if (model == null) Debug.LogError("Chýba model: " + modelPaths[i]);
            levels.GetArrayElementAtIndex(i).objectReferenceValue = model;
        }
    }

    static void SetInts(SerializedProperty array, int[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).intValue = values[i];
    }
}
