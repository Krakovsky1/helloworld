using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keeps both legacy Text and TMP labels in sync with the current table.
[DisallowMultipleComponent]
public sealed class GameTextView : MonoBehaviour
{
    private Func<string> render;
    private TMP_Text tmp;
    private Text legacy;

    public static void Set(TMP_Text label, Func<string> value)
    {
        if (label == null) return;
        var view = Get(label.gameObject);
        view.tmp = label;
        view.legacy = null;
        view.render = value;
        view.Refresh();
    }
    public static void Set(Text label, Func<string> value)
    {
        if (label == null) return;
        var view = Get(label.gameObject);
        view.legacy = label;
        view.tmp = null;
        view.render = value;
        view.Refresh();
    }
    private static GameTextView Get(GameObject go)
    {
        var view = go.GetComponent<GameTextView>();
        return view != null ? view : go.AddComponent<GameTextView>();
    }
    private void OnEnable() { GameText.Changed += Refresh; Refresh(); }
    private void OnDisable() { GameText.Changed -= Refresh; }
    private void Refresh()
    {
        if (render == null) return;
        string value = render();
        if (tmp != null && tmp.text != value) tmp.text = value;
        if (legacy != null && legacy.text != value) legacy.text = value;
    }
}
