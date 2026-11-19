// Unity: Tools > Tropical Resort > Apply Figma Look
// Nastaví osvetlenie a post-processing v aktuálnej scéne tak, aby sa podobala návrhu z Figmy:
//  - teplé slnko s mäkkými tieňmi (a dosah tieňov pre ortografickú kameru vo vzdialenosti ~70 m),
//  - jemné "trojfarebné" ambientné svetlo (obloha / horizont / zem),
//  - Global Volume s profilom FigmaLookProfile (Neutral tonemapping, jemný bloom, sýtosť, teplota, vinetácia),
//  - post-processing a SMAA na hlavnej kamere, silnejšie SSAO v PC_Renderer.
// Všetko sa dá vrátiť cez Undo (scéna) alebo ručne v Inspectore. Spúšťaj s otvorenou scénou StarterIsland.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FigmaLookSetup
{
    const string ProfilePath = "Assets/Settings/FigmaLookProfile.asset";
    const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
    const string PcAsset = "Assets/Settings/PC_RPAsset.asset";
    const string MobileAsset = "Assets/Settings/Mobile_RPAsset.asset";

    [MenuItem("Tools/Tropical Resort/Apply Figma Look")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // ---------- 1) Volume profil ----------
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        var tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(new Color(1f, 0.93f, 0.8f));

        var color = GetOrAdd<ColorAdjustments>(profile);
        color.postExposure.Override(0.15f);
        color.contrast.Override(8f);
        color.saturation.Override(14f);
        color.colorFilter.Override(new Color(1f, 0.985f, 0.95f));

        var wb = GetOrAdd<WhiteBalance>(profile);
        wb.temperature.Override(7f);
        wb.tint.Override(2f);

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.45f);

        EditorUtility.SetDirty(profile);

        // ---------- 2) Global Volume v scéne ----------
        Volume volume = null;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.isGlobal) { volume = v; break; }
        if (volume == null)
        {
            var go = new GameObject("Global Volume (Figma Look)");
            Undo.RegisterCreatedObjectUndo(go, "Apply Figma Look");
            volume = go.AddComponent<Volume>();
        }
        Undo.RecordObject(volume, "Apply Figma Look");
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;

        // ---------- 3) Kamera ----------
        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            Undo.RecordObject(data, "Apply Figma Look");
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(data);
        }
        else Debug.LogWarning("Figma Look: v scéne nie je kamera s tagom MainCamera.");

        // ---------- 4) Slnko ----------
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            Undo.RecordObject(light, "Apply Figma Look");
            light.color = new Color(1f, 0.93f, 0.8f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            EditorUtility.SetDirty(light);
        }

        // ---------- 5) Ambientné svetlo ----------
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.46f, 0.41f, 0.33f);
        RenderSettings.ambientIntensity = 1f;

        // ---------- 6) Tiene: dosah pre vzdialenú ortografickú kameru ----------
        foreach (var path in new[] { PcAsset, MobileAsset })
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;
            var so = new SerializedObject(asset);
            SetFloat(so, "m_ShadowDistance", 120f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetFloat(so, "m_Cascade2Split", 0.6f);
            if (path == PcAsset) SetInt(so, "m_MainLightShadowmapResolution", 4096);
            so.ApplyModifiedProperties();
        }

        // ---------- 7) SSAO (už je v PC_Renderer) - výraznejšie tienenie v kútoch ----------
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(PcRenderer))
        {
            if (obj == null || obj.name != "ScreenSpaceAmbientOcclusion") continue;
            var so = new SerializedObject(obj);
            SetFloat(so, "m_Settings.Intensity", 0.9f);
            SetFloat(so, "m_Settings.Radius", 0.45f);
            SetFloat(so, "m_Settings.DirectLightingStrength", 0.35f);
            so.ApplyModifiedProperties();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = volume.gameObject;
        Debug.Log("Figma Look: hotovo (Volume profil, kamera, slnko, ambient, tiene, SSAO). Ulož scénu.");
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;
        return component;
    }

    static void SetFloat(SerializedObject so, string path, float value)
    {
        var p = so.FindProperty(path);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("Figma Look: nenašiel som nastavenie " + path);
    }

    static void SetInt(SerializedObject so, string path, int value)
    {
        var p = so.FindProperty(path);
        if (p != null) p.intValue = value;
        else Debug.LogWarning("Figma Look: nenašiel som nastavenie " + path);
    }
}
