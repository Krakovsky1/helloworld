// Unity: Assets > Tropical Resort > Setup Buildings
// Skopíruj priečinok TropicalResort_Buildings do Assets/. Skript vytvorí jeden zdieľaný materiál s paletou a prefaby.
using UnityEngine;
using UnityEditor;
using System.IO;

public static class BuildingsSetup
{
    const string Root = "Assets/TropicalResort_Buildings";
    [MenuItem("Assets/Tropical Resort/Setup Buildings")]
    public static void Setup()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
        Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Prefabs");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Palette.png");
        var mat = new Material(sh) { name = "Buildings_Palette", enableInstancing = true };
        mat.SetTexture("_BaseMap", tex); mat.SetTexture("_MainTex", tex); mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, Root + "/Materials/Buildings_Palette.mat");
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); if (!path.EndsWith(".obj")) continue;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            imp.importNormals = ModelImporterNormals.Import; imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Palette"), mat); imp.SaveAndReimport();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + Path.GetFileNameWithoutExtension(path) + ".prefab"); Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Buildings: hotovo.");
    }
}
