using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional visual restyle for the existing FR-07 ServiceCapacityPanel.
/// Keeps children, TMP references, values and gameplay components untouched.
/// Reuses warm wood/gold colors consistent with ResourceHud.
/// </summary>
public static class ServiceCapacityResortStyle
{
    private const string MenuPath = "Tools/FR-07/Apply resort wood and gold style";
    private const string AssetFolder = "Assets/FR07ServiceUI/Generated/ResortStyle";

    private static readonly Color Cream = Hex("#FFF1CD");
    private static readonly Color SoftCream = Hex("#DCCCA9");
    private static readonly Color Gold = Hex("#F6CD72");
    private static readonly Color Leaf = Hex("#83B986");
    private static readonly Color Water = Hex("#78C7D2");
    private static readonly Color Sand = Hex("#E9B567");

    [MenuItem(MenuPath)]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("FR-07", "Najprv zastav Play Mode.", "OK");
            return;
        }

        GameObject root = Selection.activeGameObject;
        if (root == null || root.name != "ServiceCapacityPanel" || root.GetComponent<RectTransform>() == null)
        {
            EditorUtility.DisplayDialog("FR-07", "V Hierarchy vyber Canvas > ServiceCapacityPanel.", "OK");
            return;
        }

        string[] required = { "Heading", "ServicesCard", "DrinksCard", "FoodCard" };
        foreach (string name in required)
        {
            if (root.transform.Find(name) == null)
            {
                EditorUtility.DisplayDialog("FR-07", "Nenasiel sa objekt '" + name + "'. Tento nastroj je urceny pre uz vytvoreny panel FR-07.", "OK");
                return;
            }
        }

        Sprite panel = MakeSprite("resort_panel.png", 128, 128, 20f, 2.4f, 25,
                                 Hex("#25170F"), Hex("#352218"), Hex("#A27A42"), Hex("#C69C57"));
        Sprite pill = MakeSprite("resort_gold_pill.png", 160, 64, 29f, 3.2f, 20,
                                Hex("#23160F"), Hex("#39261A"), Hex("#D7A447"), Hex("#FFE1A1"));
        Sprite chip = MakeSprite("resort_chip.png", 64, 64, 27f, 2.3f, 22,
                                Hex("#674218"), Hex("#8D5B24"), Hex("#B78033"), Hex("#F5CB72"));

        StyleImage(root.transform, panel, Color.white);
        Shadow shadow = root.GetComponent<Shadow>();
        if (shadow != null)
        {
            Undo.RecordObject(shadow, "FR-07 Restyle Shadow");
            shadow.effectColor = new Color(0.12f, 0.055f, 0.018f, 0.58f);
            shadow.effectDistance = new Vector2(0f, -6f);
        }

        StyleText(root.transform, "Overline", Gold);
        StyleText(root.transform, "Heading", Cream);
        StyleImage(root.transform.Find("Accent"), null, Gold);
        StyleImage(root.transform.Find("Divider"), null, new Color(0.88f, 0.68f, 0.34f, 0.6f));

        StyleCard(root.transform.Find("ServicesCard"), pill, chip, Leaf);
        StyleCard(root.transform.Find("DrinksCard"), pill, chip, Water);
        StyleCard(root.transform.Find("FoodCard"), pill, chip, Sand);

        StyleImage(root.transform.Find("StatusBadge"), null, Hex("#6C481E"));
        StyleText(root.transform, "Status", Gold);
        StyleText(root.transform, "Wave", SoftCream);

        // We do not rebuild the UI, delete children, override labels or change meter bindings.
        EditorUtility.SetDirty(root);
        if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("FR-07: Hotovo",
            "Panel ma novy dreveno-zlaty styl. Vsetky skripty, referencie a tvoje texty zostali zachovane. Stlac Ctrl+S.",
            "OK");
    }

    private static void StyleCard(Transform card, Sprite pill, Sprite chip, Color accent)
    {
        if (card == null) return;
        StyleImage(card, pill, Color.white);
        StyleImage(card.Find("InitialBackground"), chip, Color.white, false);
        StyleText(card, "Initial", Cream);
        StyleText(card, "Title", Cream);
        StyleText(card, "Value", Gold);
        StyleImage(card.Find("ProgressTrack"), null, Hex("#583D29"));
        Transform track = card.Find("ProgressTrack");
        if (track != null) StyleImage(track.Find("ProgressFill"), null, accent);
    }

    private static void StyleText(Transform parent, string childName, Color color)
    {
        if (parent == null) return;
        Transform child = parent.Find(childName);
        if (child == null) return;
        TMP_Text text = child.GetComponent<TMP_Text>();
        if (text == null) return;
        Undo.RecordObject(text, "FR-07 Restyle Text");
        text.color = color;
        EditorUtility.SetDirty(text);
    }

    private static void StyleImage(Transform transform, Sprite sprite, Color tint, bool sliced = true)
    {
        if (transform == null) return;
        Image image = transform.GetComponent<Image>();
        if (image == null) return;
        Undo.RecordObject(image, "FR-07 Restyle Image");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        image.color = tint;
        EditorUtility.SetDirty(image);
    }

    private static Sprite MakeSprite(string filename, int width, int height,
                                     float radius, float borderThickness, int slice,
                                     Color woodBottom, Color woodTop, Color rimBottom, Color rimTop)
    {
        Directory.CreateDirectory(AssetFolder);
        string path = AssetFolder + "/" + filename;
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[width * height];
            Vector2 half = new Vector2(width * 0.5f, height * 0.5f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                float d = SdfRoundedBox(p, half, radius);
                float cover = Mathf.Clamp01(0.75f - d);
                float blend = Mathf.Clamp01((float)y / Mathf.Max(1, height - 1));
                Color interior = Color.Lerp(woodBottom, woodTop, blend);
                Color edge = Color.Lerp(rimBottom, rimTop, blend);
                Color pixel = d >= -borderThickness ? edge : interior;
                // Very subtle top sheen as in the existing ResourceHud.
                if (d < -borderThickness && p.y > 0f)
                    pixel = Color.Lerp(pixel, rimTop, 0.035f);
                pixel.a = cover;
                pixels[y * width + x] = pixel;
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            Vector4 border = new Vector4(slice, slice, slice, slice);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteBorder != border || importer.mipmapEnabled ||
                importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static float SdfRoundedBox(Vector2 point, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half + new Vector2(radius, radius);
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f) +
               new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - radius;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color result);
        return result;
    }
}
