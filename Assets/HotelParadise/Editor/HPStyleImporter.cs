// Hotel Paradise - material setup (auto-generated). Menu: Hotel Paradise > Rebuild Materials
using System.IO;
using UnityEditor;
using UnityEngine;
namespace HotelParadiseStyle
{

public class HPStyleImporter : AssetPostprocessor
{
    public const string Root = "Assets/HotelParadise";
    class Mat { public string name, tex; public Color color; public float gloss; public bool emit;
        public Mat(string n, float r, float g, float b, string t, float gl, int e) { name = n; color = new Color(r, g, b); tex = t; gloss = gl; emit = e == 1; } }
    static readonly Mat[] Table = {
        new Mat("grass_top", 0.498f, 0.816f, 0.290f, "", 0.12f, 0),
        new Mat("grass_mid", 0.373f, 0.722f, 0.235f, "", 0.12f, 0),
        new Mat("grass_dark", 0.247f, 0.561f, 0.196f, "", 0.12f, 0),
        new Mat("soil", 0.541f, 0.416f, 0.275f, "", 0.12f, 0),
        new Mat("soil_dark", 0.420f, 0.310f, 0.200f, "", 0.12f, 0),
        new Mat("sand", 0.937f, 0.863f, 0.706f, "tex_sand", 0.12f, 0),
        new Mat("sand_dark", 0.851f, 0.741f, 0.494f, "tex_sand", 0.12f, 0),
        new Mat("sand_wet", 0.878f, 0.776f, 0.557f, "tex_sand", 0.12f, 0),
        new Mat("water_deep", 0.165f, 0.612f, 0.753f, "", 0.65f, 0),
        new Mat("water_shallow", 0.373f, 0.839f, 0.816f, "", 0.65f, 0),
        new Mat("foam", 1.000f, 1.000f, 1.000f, "", 0.12f, 0),
        new Mat("leaf_light", 0.741f, 0.878f, 0.400f, "tex_leaf", 0.35f, 0),
        new Mat("leaf_mid", 0.498f, 0.761f, 0.271f, "tex_leaf", 0.35f, 0),
        new Mat("leaf_dark", 0.302f, 0.608f, 0.208f, "tex_leaf", 0.35f, 0),
        new Mat("frond_light", 0.596f, 0.831f, 0.322f, "tex_frond", 0.35f, 0),
        new Mat("frond_mid", 0.412f, 0.741f, 0.259f, "tex_frond", 0.35f, 0),
        new Mat("frond_dark", 0.247f, 0.604f, 0.212f, "tex_frond", 0.35f, 0),
        new Mat("trunk", 0.706f, 0.533f, 0.345f, "tex_wood", 0.12f, 0),
        new Mat("trunk_dark", 0.486f, 0.325f, 0.188f, "tex_wood", 0.12f, 0),
        new Mat("wood_light", 0.839f, 0.627f, 0.392f, "tex_wood", 0.12f, 0),
        new Mat("wood_mid", 0.698f, 0.463f, 0.251f, "tex_wood", 0.12f, 0),
        new Mat("wood_dark", 0.420f, 0.259f, 0.149f, "tex_wood", 0.12f, 0),
        new Mat("wood_red", 0.753f, 0.376f, 0.235f, "tex_wood", 0.12f, 0),
        new Mat("thatch_light", 0.925f, 0.851f, 0.671f, "tex_thatch", 0.12f, 0),
        new Mat("thatch_mid", 0.863f, 0.761f, 0.557f, "tex_thatch", 0.12f, 0),
        new Mat("thatch_dark", 0.761f, 0.643f, 0.408f, "tex_thatch", 0.12f, 0),
        new Mat("cream", 0.957f, 0.929f, 0.878f, "tex_plaster", 0.12f, 0),
        new Mat("cream_dark", 0.878f, 0.827f, 0.722f, "tex_plaster", 0.12f, 0),
        new Mat("stone", 0.827f, 0.765f, 0.659f, "tex_plaster", 0.12f, 0),
        new Mat("stone_dark", 0.659f, 0.584f, 0.478f, "tex_plaster", 0.12f, 0),
        new Mat("glass", 0.231f, 0.165f, 0.149f, "", 0.45f, 0),
        new Mat("glass_lit", 1.000f, 0.824f, 0.478f, "", 0.12f, 1),
        new Mat("rock_light", 0.847f, 0.718f, 0.580f, "tex_rock", 0.12f, 0),
        new Mat("rock_mid", 0.749f, 0.608f, 0.471f, "tex_rock", 0.12f, 0),
        new Mat("rock_dark", 0.588f, 0.435f, 0.353f, "tex_rock", 0.12f, 0),
        new Mat("moss", 0.482f, 0.749f, 0.290f, "", 0.12f, 0),
        new Mat("flower_red", 1.000f, 0.365f, 0.424f, "", 0.12f, 0),
        new Mat("flower_yellow", 1.000f, 0.827f, 0.290f, "", 0.12f, 0),
        new Mat("flower_pink", 1.000f, 0.608f, 0.816f, "", 0.12f, 0),
        new Mat("flower_white", 1.000f, 0.965f, 0.902f, "", 0.12f, 0),
        new Mat("cloth", 1.000f, 0.980f, 0.941f, "", 0.12f, 0),
        new Mat("cloth_dark", 0.851f, 0.804f, 0.722f, "", 0.12f, 0),
        new Mat("umb_a", 1.000f, 0.965f, 0.890f, "", 0.12f, 0),
        new Mat("umb_b", 0.914f, 0.863f, 0.769f, "", 0.12f, 0),
        new Mat("flame", 1.000f, 0.604f, 0.180f, "", 0.12f, 1),
        new Mat("bottle_g", 0.310f, 0.690f, 0.416f, "", 0.12f, 0),
        new Mat("bottle_o", 0.898f, 0.541f, 0.184f, "", 0.12f, 0),
        new Mat("rope", 0.788f, 0.639f, 0.416f, "", 0.12f, 0),
        new Mat("gold", 0.941f, 0.722f, 0.290f, "", 0.45f, 0),
        new Mat("metal", 0.427f, 0.463f, 0.502f, "", 0.45f, 0),
        new Mat("wicker", 0.757f, 0.604f, 0.357f, "tex_wicker", 0.12f, 0),
        new Mat("stone_pink", 0.722f, 0.541f, 0.541f, "tex_rock", 0.12f, 0),
        new Mat("terrain", 0.918f, 0.831f, 0.686f, "terrain_albedo", 0.12f, 0),
        new Mat("water_surface", 0.290f, 0.647f, 0.675f, "water_albedo", 0.65f, 0),
        new Mat("terrain_start", 0.918f, 0.831f, 0.686f, "terrain_albedo_start", 0.12f, 0),
        new Mat("water_start", 0.290f, 0.647f, 0.675f, "water_albedo_start", 0.65f, 0),
    };

    static Shader PickShader(out bool urp)
    {
        var s = Shader.Find("Universal Render Pipeline/Lit"); urp = s != null;
        if (s == null) s = Shader.Find("HotelParadise/StandardDoubleSided");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    public static Material GetOrCreate(string name)
    {
        string dir = Root + "/Materials"; if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, "Materials");
        string path = dir + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        Mat def = null; foreach (var m in Table) if (m.name == name) def = m;
        if (def == null) return null;
        bool urp; var mat = new Material(PickShader(out urp)) { name = name };
        Texture2D tex = null;
        if (!string.IsNullOrEmpty(def.tex)) { string tp = Root + "/Textures/" + def.tex + ".png"; AssetDatabase.ImportAsset(tp); tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); }
        bool baked = name.StartsWith("terrain") || name.StartsWith("water_s");
        Color tint = baked ? Color.white : (tex != null ? Color.Lerp(def.color, Color.white, 0f) * 1.12f : def.color); tint.a = 1;
        if (urp) { mat.SetColor("_BaseColor", tint); if (tex != null) mat.SetTexture("_BaseMap", tex); mat.SetFloat("_Smoothness", def.gloss); mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Cull", 0f); }
        else { mat.SetColor("_Color", tint); if (tex != null) mat.SetTexture("_MainTex", tex); if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", def.gloss); if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f); }
        if (def.emit) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", def.color * 1.5f); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    [MenuItem("Hotel Paradise/Rebuild Materials")]
    public static void RebuildAll()
    {
        string dir = Root + "/Materials";
        if (AssetDatabase.IsValidFolder(dir)) foreach (var f in Directory.GetFiles(dir, "*.mat")) AssetDatabase.DeleteAsset(f.Replace("\\", "/"));
        foreach (var m in Table) GetOrCreate(m.name);
        AssetDatabase.SaveAssets(); Debug.Log("Hotel Paradise: materials rebuilt.");
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Root + "/Models")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f; mi.importNormals = ModelImporterNormals.Import; mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.isReadable = false; mi.optimizeMeshPolygons = true; mi.optimizeMeshVertices = true; mi.importCameras = false; mi.importLights = false;
    }

    Material OnAssignMaterialModel(Material material, Renderer renderer)
    {
        if (!assetPath.StartsWith(Root + "/Models")) return null;
        return GetOrCreate(material.name);
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root + "/Textures")) return;
        var ti = (TextureImporter)assetImporter; ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear;
        bool baked = assetPath.Contains("terrain_albedo") || assetPath.Contains("water_albedo");
        ti.wrapMode = baked ? TextureWrapMode.Clamp : TextureWrapMode.Repeat; ti.maxTextureSize = baked ? 2048 : 512; ti.anisoLevel = 4;
    }
}

}
