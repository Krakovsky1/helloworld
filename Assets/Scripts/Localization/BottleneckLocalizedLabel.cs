using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

// NFR-24: localized runtime warnings using the existing GameTexts table.
// This also supports legacy UI Text, without changing the layout or gameplay.
[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public sealed class BottleneckLocalizedLabel : MonoBehaviour
{
    private Text label;
    private LocalizedString message;
    private LocalizedString formatted;
    private string key;
    private string formatKey;
    private int count;
    private string currentMessage;
    private bool subscribed;

    private void Awake() => label = GetComponent<Text>();

    public void Set(string entry, string formatEntry, int value)
    {
        if (key == entry && formatKey == formatEntry && count == value) return;

        bool messageChanged = key != entry;
        key = entry;
        formatKey = formatEntry;
        count = value;

        if (messageChanged)
        {
            Unsubscribe();
            currentMessage = null;
            message = new LocalizedString("GameTexts", key);
            if (isActiveAndEnabled) Subscribe();
        }
        else if (isActiveAndEnabled && currentMessage != null)
        {
            ApplyMessage(currentMessage);
        }
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (message == null || subscribed) return;
        subscribed = true;
        message.StringChanged += ApplyMessage;
    }

    private void Unsubscribe()
    {
        if (message != null && subscribed) message.StringChanged -= ApplyMessage;
        subscribed = false;
        ReleaseFormat();
        currentMessage = null;
    }

    private void ReleaseFormat()
    {
        if (formatted != null) formatted.StringChanged -= ApplyText;
        formatted = null;
    }

    private void ApplyMessage(string value)
    {
        if (!isActiveAndEnabled) return;
        currentMessage = value;
        ReleaseFormat();
        if (string.IsNullOrEmpty(formatKey))
        {
            ApplyText(value);
            return;
        }

        // The format entry controls word order and the translated queue suffix.
        formatted = new LocalizedString("GameTexts", formatKey)
        {
            Arguments = new object[] { value, count }
        };
        formatted.StringChanged += ApplyText;
    }

    private void ApplyText(string value)
    {
        if (label != null) label.text = value;
    }
}
