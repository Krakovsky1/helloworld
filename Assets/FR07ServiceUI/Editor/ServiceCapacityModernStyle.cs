using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reverses the FR-07 wood/gold visual restyle without rebuilding UI.
/// Keeps the entire hierarchy, RectTransforms, label contents, service values,
/// serialized references and gameplay components unchanged.
/// </summary>
public static class ServiceCapacityModernStyle
{
    private const string MenuPath = "Tools/FR-07/Restore modern tropical style";
    private const string RoundedSpritePath = "Assets/FR07ServiceUI/Generated/rounded_white.png";

    private static readonly Color Background = Hex("#142D31");
    private static readonly Color Row = Hex("#203C40");
    private static readonly Color TextWhite = Hex("#F6F5E7");
    private static readonly Color Muted = Hex("#A3C0B9");
    private static readonly Color Gold = Hex("#FFD789");
    private static readonly Color Services = Hex("#67D5B3");
    private static readonly Color Drinks = Hex("#66BEEB");
    private static readonly Color Food = Hex("#F3A96F");

    [MenuItem(MenuPath)]
    private static void Restore()
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
        foreach (string child in required)
        {
            if (root.transform.Find(child) == null)
            {
                EditorUtility.DisplayDialog("FR-07", "Chyba objekt " + child + ". Panel nebol upraveny.", "OK");
                return;
            }
        }

        Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        if (rounded == null)
        {
            EditorUtility.DisplayDialog("FR-07", "Nenasiel sa povodny rounded_white.png v Assets/FR07ServiceUI/Generated. Panel nebol upraveny.", "OK");
            return;
        }

        Transform t = root.transform;
        SetImage(t, rounded, Background);

        Shadow shadow = root.GetComponent<Shadow>();
        if (shadow != null)
        {
            Undo.RecordObject(shadow, "FR-07 Restore Modern Shadow");
            shadow.effectColor = new Color(0f, 0.07f, 0.08f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -7f);
            EditorUtility.SetDirty(shadow);
        }

        SetImage(t.Find("Accent"), null, Gold);
        SetImage(t.Find("Divider"), null, Hex("#426063"));
        SetText(t.Find("Overline"), Muted);
        SetText(t.Find("Heading"), TextWhite);

        RestoreCard(t.Find("ServicesCard"), rounded, Services);
        RestoreCard(t.Find("DrinksCard"), rounded, Drinks);
        RestoreCard(t.Find("FoodCard"), rounded, Food);

        SetImage(t.Find("StatusBadge"), rounded, Hex("#36554B"));
        SetText(t.Find("Status"), Gold);
        SetText(t.Find("Wave"), Muted);

        if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
        EditorUtility.DisplayDialog("FR-07", "Modern Tropical vzhlad je obnoveny. Texty, rozlozenie a skripty zostali zachovane. Uloz scenu cez Ctrl+S.", "OK");
    }

    private static void RestoreCard(Transform card, Sprite rounded, Color accent)
    {
        if (card == null) return;
        SetImage(card, rounded, Row);
        SetImage(card.Find("InitialBackground"), rounded, Hex("#355356"));
        SetText(card.Find("Initial"), accent);
        SetText(card.Find("Title"), TextWhite);
        SetText(card.Find("Value"), Gold);
        Transform track = card.Find("ProgressTrack");
        SetImage(track, rounded, Hex("#3B595A"));
        if (track != null) SetImage(track.Find("ProgressFill"), rounded, accent);
    }

    private static void SetImage(Transform target, Sprite sprite, Color color)
    {
        if (target == null) return;
        Image img = target.GetComponent<Image>();
        if (img == null) return;
        Undo.RecordObject(img, "FR-07 Restore Modern Image");
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }
        img.color = color;
        EditorUtility.SetDirty(img);
    }

    private static void SetText(Transform target, Color color)
    {
        if (target == null) return;
        TMP_Text text = target.GetComponent<TMP_Text>();
        if (text == null) return;
        Undo.RecordObject(text, "FR-07 Restore Modern Text");
        text.color = color;
        EditorUtility.SetDirty(text);
    }

    private static Color Hex(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color color);
        return color;
    }
}
