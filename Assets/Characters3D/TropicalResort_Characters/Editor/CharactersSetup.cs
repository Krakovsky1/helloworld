// Unity: Assets > Tropical Resort > Setup Characters
// Skopíruj priečinok TropicalResort_Characters do Assets/ (Scripts/ a Editor/ musia zostať v priečinku).
// Skript: vytvorí jeden zdieľaný materiál, rozdelí postavu na kĺby (pivoty) a vytvorí prefaby s CharacterAnimator.
using UnityEngine;
using UnityEditor;
using System.IO;

public static class CharactersSetup
{
    const string Root = "Assets/TropicalResort_Characters";
    [MenuItem("Assets/Tropical Resort/Setup Characters")]
    public static void Setup()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
        Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Prefabs");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Palette.png");
        var mat = new Material(sh) { name = "Characters_Palette", enableInstancing = true };
        mat.SetTexture("_BaseMap", tex); mat.SetTexture("_MainTex", tex); mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, Root + "/Materials/Characters_Palette.mat");

        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); if (!path.EndsWith(".obj")) continue;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            imp.importNormals = ModelImporterNormals.Import; imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Palette"), mat); imp.SaveAndReimport();

            string n = Path.GetFileNameWithoutExtension(path);
            var go = new GameObject(n); var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.transform.SetParent(go.transform, false); model.name = "Model";
            var anim = go.AddComponent<CharacterAnimator>(); anim.body = model.transform;

            string rigPath = Root + "/Rigs/" + n + ".rig.txt";
            if (File.Exists(rigPath))
            {
                foreach (var line in File.ReadAllLines(rigPath))
                {
                    var p = line.Split(' '); if (p.Length < 4) continue;
                    // Unity zrkadli X pri importe OBJ
                    var pos = new Vector3(-float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture));
                    var part = FindDeep(model.transform, p[0]); if (part == null) continue;
                    var pivot = new GameObject(p[0] + "_Pivot").transform; pivot.SetParent(model.transform, false); pivot.localPosition = pos;
                    part.SetParent(pivot, true);
                    switch (p[0]) { case "Head": anim.head = pivot; break; case "ArmL": anim.armL = pivot; break; case "ArmR": anim.armR = pivot; break; case "LegL": anim.legL = pivot; break; case "LegR": anim.legR = pivot; break; }
                }
            }
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + n + ".prefab"); Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Characters: hotovo (ak sa OBJ neimportoval po častiach, postava sa animuje len celá).");
    }
    static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t) { if (c.name == name || c.name.EndsWith("_" + name)) return c; var r = FindDeep(c, name); if (r) return r; }
        return null;
    }
}
