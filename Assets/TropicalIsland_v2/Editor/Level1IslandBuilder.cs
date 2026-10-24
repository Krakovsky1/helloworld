// Unity: Tools > Tropical Island > Build Level 1 Island
//
// Predpoklady:
//  1) Priecinok TropicalIsland_v2 je v Assets/.
//  2) Subory Island_Large_Clear.obj + .mtl (z tohto balika, priecinok Island/) skopiruj do
//     Assets/TropicalIsland_v2/Island/  a potom spusti povodne  Assets > Tropical Island > Setup
//     (priradi textury a vytvori prefab Island_Large_Clear.prefab).
//  3) BuildPlot.cs patri kamkolvek do Assets/Scripts, tento subor do priecinka Editor.
//
// Ostrov je model Island_Large (ten z obrazka), zvacseny a s vycistenou luckou pre budovy.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class Level1IslandBuilder
{
    const string AssetRoot = "Assets/TropicalIsland_v2";
    const float IslandScale = 2.5f;                 // model je maly (~24 m), v hre ho zvacsime
    const float PlotY = 0.7f;                       // vyska povrchu lucky po zvacseni (cca 0.28 * scale)
    static readonly Color OceanColor = new Color32(15, 135, 202, 255);

    struct PlotDef
    {
        public string name; public PlotType type; public float x, z, w, d;
        public PlotDef(string n, PlotType t, float x, float z, float w, float d) { name = n; type = t; this.x = x; this.z = z; this.w = w; this.d = d; }
    }

    // Pozicie: stred plochy v metroch od stredu ostrova (po zvacseni), w = sirka (X), d = dlzka (Z).
    // Definovane tak, ako Unity beznie importuje OBJ (os X zrkadlovo). Ak by bola os X opacne,
    // skript to rozpozna a zrkadli ich sam.
    static readonly PlotDef[] Plots =
    {
        new PlotDef("Hotel",            PlotType.Hotel,      -5f,  -4f,  10, 10),
        new PlotDef("Bazen",            PlotType.Pool,        4f,   2f,   7,  7),
        new PlotDef("Restauracia",      PlotType.Restaurant,  9.5f, 9.5f, 7,  7),
        new PlotDef("Golfove ihrisko",  PlotType.GolfCourse, -7.5f, 7.5f, 16, 8),
        new PlotDef("Bar",              PlotType.Bar,        -8f,  15f,   7,  4),
        new PlotDef("Volna plocha 1",   PlotType.Free,      -14f,  -3f,   7,  7),   // napr. Spa
        new PlotDef("Volna plocha 2",   PlotType.Free,       -0.5f, 14.5f, 7, 4),   // napr. obchody
    };

    [MenuItem("Tools/Tropical Island/Build Level 1 Island")]
    public static void Build()
    {
        var old = GameObject.Find("Island_Level1");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("Island_Level1");
        Undo.RegisterCreatedObjectUndo(root, "Build Level 1 Island");

        // ---- ostrov
        var prefab = LoadIslandPrefab();
        if (prefab == null) { Object.DestroyImmediate(root); return; }
        var island = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        island.name = "Island";
        island.transform.localPosition = Vector3.zero;
        island.transform.localRotation = Quaternion.identity;
        island.transform.localScale = Vector3.one * IslandScale;

        // Je os X importovana zrkadlovo? Povodny model ma stred X ~ +0.43 (OBJ) -> po zrkadleni ~ -0.43.
        bool flipped = true;
        var mf = island.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) flipped = mf.sharedMesh.bounds.center.x < 0f;
        float sx = flipped ? 1f : -1f;

        // ---- plochy pre budovy
        var plotsRoot = new GameObject("BuildPlots");
        plotsRoot.transform.SetParent(root.transform, false);
        foreach (var p in Plots)
        {
            var go = new GameObject("Plot_" + p.name);
            go.transform.SetParent(plotsRoot.transform, false);
            go.transform.localPosition = new Vector3(p.x * sx, PlotY, p.z);
            var bp = go.AddComponent<BuildPlot>();
            bp.type = p.type;
            bp.displayName = p.name;
            bp.size = new Vector2(p.w, p.d);
            bp.EnsureCollider();
        }

        // ---- neviditelna zem pre raycast (klikanie / umiestnovanie budov)
        var ground = new GameObject("GroundCollider");
        ground.transform.SetParent(root.transform, false);
        var box = ground.AddComponent<BoxCollider>();
        box.center = new Vector3(0, PlotY - 0.3f, 0);
        box.size = new Vector3(44f, 0.6f, 38f);

        // ---- more
        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "Ocean";
        ocean.transform.SetParent(root.transform, false);
        ocean.transform.localPosition = new Vector3(0, -0.12f, 0);
        ocean.transform.localScale = new Vector3(60f, 1f, 60f);      // 600 x 600 m
        var col = ocean.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
        ocean.GetComponent<Renderer>().sharedMaterial = OceanMaterial();

        // ---- ulozit ako prefab
        string prefDir = AssetRoot + "/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefDir)) AssetDatabase.CreateFolder(AssetRoot, "Prefabs");
        string path = prefDir + "/Island_Level1.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Selection.activeGameObject = root;
        Debug.Log("Level 1 ostrov hotovy: " + path + " (" + Plots.Length + " ploch pre budovy, scale " + IslandScale + ")");
    }

    static GameObject LoadIslandPrefab()
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "/Prefabs/Island_Large_Clear.prefab");
        if (g == null) g = AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "/Island/Island_Large_Clear.obj");
        if (g == null)
        {
            g = AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "/Island/Island_Large.obj");
            if (g != null) Debug.LogWarning("Island_Large_Clear sa nenasiel - pouziva sa povodny Island_Large (krovie a palmy budu v plochach pre budovy).");
        }
        if (g == null) Debug.LogError("Chyba Island_Large_Clear.obj v " + AssetRoot + "/Island/ (skopiruj ho tam a spusti Assets > Tropical Island > Setup).");
        return g;
    }

    static Material OceanMaterial()
    {
        string dir = AssetRoot + "/Materials";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(AssetRoot, "Materials");
        string path = dir + "/Ocean.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        mat = new Material(sh) { name = "Ocean" };
        mat.color = OceanColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", OceanColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
#endif
