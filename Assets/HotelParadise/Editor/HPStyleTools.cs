// Hotel Paradise - menu tools: Hotel Paradise > Build Everything / Create Prefabs / Build Level 1 Island / Setup Isometric Camera & Light
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace HotelParadiseStyle
{

public static class HPStyleTools
{
    const string ModelDir = "Assets/HotelParadise/Models";
    const string PrefabDir = "Assets/HotelParadise/Prefabs";
    const string LayoutPath = "Assets/HotelParadise/Layout/island_level1.json";
    const string StartPath = "Assets/HotelParadise/Layout/island_level1_start.json";
    // The OBJ importer mirrors X (right-handed -> left-handed); the layout is authored in OBJ space, so mirror it back.
    const bool MirrorX = true;

    [System.Serializable] class Entry { public string prefab; public float x, y, z, rotY, scale; public string slot; public float size; }
    [System.Serializable] class Layout { public List<Entry> objects; }

    [MenuItem("Hotel Paradise/Build Everything")]
    public static void BuildEverything() { HPStyleImporter.RebuildAll(); CreatePrefabs(); BuildStarterIsland(); SetupCamera(); }

    [MenuItem("Hotel Paradise/Create Prefabs")]
    public static void CreatePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/HotelParadise", "Prefabs");
        foreach (var f in Directory.GetFiles(ModelDir, "*.obj"))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/" + name + ".obj"); if (model == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model); go.name = name;
            bool water = name.StartsWith("water");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.receiveShadows = !water;
                r.shadowCastingMode = water ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (name.StartsWith("building_") || name == "tree_big" || name.StartsWith("palm"))
            {
                var b = new Bounds(go.transform.position, Vector3.zero); foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var bc = go.AddComponent<BoxCollider>(); bc.center = go.transform.InverseTransformPoint(b.center); bc.size = b.size;
            }
            if (name == "island_terrain") foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab"); Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Hotel Paradise: prefabs created in " + PrefabDir);
    }

    [MenuItem("Hotel Paradise/Build Level 1 Starter Island")]
    public static void BuildStarterIsland() { BuildFrom(StartPath, "Island_Level1_Start"); }

    [MenuItem("Hotel Paradise/Build Demo Island (full)")]
    public static void BuildIsland() { BuildFrom(LayoutPath, "Island_Level1_Demo"); }

    static void BuildFrom(string layoutPath, string rootName)
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath); if (json == null) { Debug.LogError("Layout not found: " + layoutPath); return; }
        var layout = JsonUtility.FromJson<Layout>(json.text);
        var old = GameObject.Find(rootName); if (old) Object.DestroyImmediate(old);
        var root = new GameObject(rootName); var groups = new Dictionary<string, Transform>();
        foreach (var e in layout.objects)
        {
            string cat = !string.IsNullOrEmpty(e.slot) ? "BuildSlots" : e.prefab.StartsWith("building_") ? "Buildings" : (e.prefab.StartsWith("water") || e.prefab.StartsWith("island_terrain")) ? "Base" : "Decor";
            if (!groups.ContainsKey(cat)) { var g = new GameObject(cat); g.transform.SetParent(root.transform); groups[cat] = g.transform; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + e.prefab + ".prefab") ?? AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/" + e.prefab + ".obj");
            if (prefab == null) { Debug.LogWarning("Missing model: " + e.prefab); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, groups[cat]);
            float sx = MirrorX ? -1f : 1f;
            go.transform.position = new Vector3(e.x * sx, e.y, e.z);
            go.transform.rotation = Quaternion.Euler(0, e.rotY * sx, 0);
            go.transform.localScale = Vector3.one * (e.scale <= 0 ? 1f : e.scale);
            if (!string.IsNullOrEmpty(e.slot))
            {
                go.name = "Slot_" + e.slot;
                var bs = go.AddComponent<BuildSlot>(); bs.slotId = e.slot; bs.sizeUnits = e.size;
                var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(e.size, 0.6f, e.size);
            }
        }
        Selection.activeGameObject = root; Debug.Log("Hotel Paradise: built " + rootName + " (" + layout.objects.Count + " objects).");
    }

    [MenuItem("Hotel Paradise/Setup Isometric Camera")]
    public static void SetupCamera()
    {
        var cam = Camera.main; if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.orthographic = true; cam.orthographicSize = 11f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
        cam.transform.rotation = Quaternion.Euler(38, 45, 0);
        cam.transform.position = cam.transform.rotation * new Vector3(0, 0, -60) + new Vector3(0, 1f, 0);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.20f, 0.42f, 0.45f);
        #if UNITY_2023_1_OR_NEWER
        var light = UnityEngine.Object.FindFirstObjectByType<Light>();
#else
        var light = UnityEngine.Object.FindObjectOfType<Light>();
#endif
        if (light == null) { var lg = new GameObject("Directional Light", typeof(Light)); light = lg.GetComponent<Light>(); light.type = LightType.Directional; }
        if (light.type == LightType.Directional)
        {   // warm key light from upper-right-front, matches the preview renders
            light.transform.rotation = Quaternion.LookRotation(new Vector3(-0.133f, -0.795f, 0.595f));
            light.color = new Color(1f, 0.93f, 0.82f); light.intensity = 1.25f; light.shadows = LightShadows.Soft; light.shadowStrength = 0.8f;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.74f, 0.82f, 0.95f) * 0.85f;
        RenderSettings.ambientEquatorColor = new Color(0.68f, 0.68f, 0.68f) * 0.8f;
        RenderSettings.ambientGroundColor = new Color(0.62f, 0.55f, 0.44f) * 0.75f;
        Debug.Log("Hotel Paradise: isometric camera + light ready. Tip: enable SSAO + soft shadows in your URP renderer for the contact-shadow look.");
    }
}

}
