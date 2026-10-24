// Unity: Assets > Tropical Island > Setup
// Skopiruj cely priecinok TropicalIsland_v2 do Assets/ (subor presun do podpriecinka Editor/), potom spusti menu.
// Skript priradi textury (Textures/*.jpg|png) k materialom z OBJ a vytvori prefaby.
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public static class TropicalIslandSetup
{
    const string Root = "Assets/TropicalIsland_v2";
    [MenuItem("Assets/Tropical Island/Setup")]
    public static void Setup()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
        string matDir = Root + "/Materials", prefDir = Root + "/Prefabs";
        Directory.CreateDirectory(matDir); Directory.CreateDirectory(prefDir);
        var cache = new Dictionary<string, Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".obj")) continue;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            imp.importNormals = ModelImporterNormals.Import; imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            string mtl = Path.ChangeExtension(path, ".mtl");
            foreach (var line in File.ReadAllLines(mtl))
            {
                if (!line.StartsWith("newmtl ")) continue;
                string name = line.Substring(7).Trim();
                if (!cache.TryGetValue(name, out var mat))
                {
                    mat = new Material(sh) { name = name };
                    string tex = Root + "/Textures/" + (name == "Palette" ? "Palette.png" : name + ".jpg");
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
                    if (t != null) { mat.SetTexture("_BaseMap", t); mat.SetTexture("_MainTex", t); }
                    mat.SetFloat("_Smoothness", 0.1f);
                    AssetDatabase.CreateAsset(mat, matDir + "/" + name + ".mat"); cache[name] = mat;
                }
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            }
            imp.SaveAndReimport();
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.SaveAsPrefabAsset(go, prefDir + "/" + Path.GetFileNameWithoutExtension(path) + ".prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Tropical Island: hotovo.");
    }
}
