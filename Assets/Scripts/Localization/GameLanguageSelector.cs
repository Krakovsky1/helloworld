using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Small in-game selector, separate from Unity's editor-only locale dropdown.
public sealed class GameLanguageSelector : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);
    private static void Install(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "StarterIsland") return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null || canvas.name != "Canvas") continue;
            if (canvas.GetComponentInChildren<GameLanguageSelector>(true) != null) return;
            var panel = new GameObject("LanguageSelector", typeof(RectTransform), typeof(GameLanguageSelector));
            panel.layer = canvas.gameObject.layer;
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 35f);
            rect.sizeDelta = new Vector2(190f, 54f);
            AddButton(rect, "SK", "sk", -98f);
            AddButton(rect, "EN", "en", 0f);
            return;
        }
    }
    private static void AddButton(RectTransform parent, string caption, string code, float x)
    {
        var go = new GameObject("Language_" + caption, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(90f, 54f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.13f, 0.30f, 0.40f, 0.95f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => GameText.Select(code));
        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.layer = go.layer;
        textGo.transform.SetParent(go.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 25; text.fontStyle = FontStyle.Bold;
        text.color = Color.white; text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false; text.text = caption;
    }
}
