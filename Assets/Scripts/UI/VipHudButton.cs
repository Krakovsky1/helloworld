using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// FR-31: Tlačidlo "VIP" v HUD (vľavo dole). Po kliknutí otvorí okno s ponukou VIP hostí.
// Vzhľad: ako panely Mince / Energia (zaoblený pás so zlatým okrajom a veľkou ikonkou vľavo),
// ale fialový - rovnako ako VIP tlačidlo v dizajne z Figmy.
// Poloha: vľavo dole, vo výške tlačidla START WAVE (stred na stred). Výšku si berie priamo
// z tlačidla StartWaveButton v scéne, takže sedí pri každom rozlíšení aj keď sa START WAVE posunie.
public class VipHudButton : MonoBehaviour, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    const float Width = 200f;
    const float Height = 70f;
    const float LeftMargin = 48f;      // rovnaký okraj ako panel Mince / Energia
    const float FallbackY = 100f;      // stred tlačidla nad spodkom obrazovky, ak sa START WAVE nenájde

    static readonly Color PurpleDark = new Color(0.33f, 0.15f, 0.62f, 0.98f);
    static readonly Color PurpleLight = new Color(0.52f, 0.29f, 0.86f, 0.98f);
    static readonly Color Gold = new Color(0.93f, 0.72f, 0.28f, 1f);
    static readonly Color GoldLight = new Color(1f, 0.9f, 0.55f, 1f);

    VipInvitePopup popup;
    Canvas canvas;
    RectTransform button;
    RectTransform startWave;
    float nextSearch;
    bool pressed;

    static Sprite pillSprite;

    public static VipHudButton Create(Transform parent, VipInvitePopup popup)
    {
        var go = new GameObject("VipHudButton");
        go.transform.SetParent(parent, false);
        var hud = go.AddComponent<VipHudButton>();
        hud.popup = popup;
        hud.Build();
        return hud;
    }

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 101;   // ako HUD (100), pod oknami (200)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        var go = new GameObject("VipButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        button = (RectTransform)go.transform;
        button.anchorMin = button.anchorMax = new Vector2(0f, 0f);
        button.pivot = new Vector2(0f, 0.5f);
        button.anchoredPosition = new Vector2(LeftMargin, FallbackY);
        button.sizeDelta = new Vector2(Width, Height);

        // Fialový pás (klikateľná plocha je celé tlačidlo)
        var hit = go.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);

        var bar = Child("Bar", button);
        bar.anchorMin = new Vector2(0f, 0.5f);
        bar.anchorMax = new Vector2(1f, 0.5f);
        bar.pivot = new Vector2(0.5f, 0.5f);
        bar.anchoredPosition = new Vector2(12f, 0f);
        bar.sizeDelta = new Vector2(-24f, 56f);
        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.sprite = PillSprite();
        barImage.type = Image.Type.Sliced;
        barImage.raycastTarget = false;
        var barShadow = bar.gameObject.AddComponent<Shadow>();
        barShadow.effectColor = new Color(0.12f, 0.04f, 0.25f, 0.8f);
        barShadow.effectDistance = new Vector2(0f, -4f);

        // Hviezda prekrývajúca ľavý okraj (ako ikonky mince / energie)
        var icon = Child("Icon", button);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(-8f, 3f);
        icon.sizeDelta = new Vector2(78f, 78f);
        var iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.sprite = Resources.Load<Sprite>("UI/Icons/vip_star");
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        var iconShadow = icon.gameObject.AddComponent<Shadow>();
        iconShadow.effectColor = new Color(0.12f, 0.04f, 0.25f, 0.6f);
        iconShadow.effectDistance = new Vector2(0f, -3f);

        var label = Child("Label", button);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(72f, 0f);
        label.offsetMax = new Vector2(-10f, 2f);
        var text = label.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = "VIP";
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.06f, 0.3f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    static RectTransform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // ------------------------------------------------------------------
    // Klik (pri hoveri sa tlačidlo nemení, pri stlačení sa jemne zmenší)
    // ------------------------------------------------------------------

    public void OnPointerExit(PointerEventData e) { pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }

    public void OnPointerUp(PointerEventData e)
    {
        bool click = pressed && e.pointerCurrentRaycast.gameObject != null &&
                     e.pointerCurrentRaycast.gameObject.transform.IsChildOf(transform);
        pressed = false;
        if (click && popup != null) popup.Toggle();
    }

    void LateUpdate()
    {
        if (button == null) return;

        float s = pressed ? 0.95f : 1f;
        button.localScale = new Vector3(s, s, 1f);

        UpdatePosition();
    }

    // ------------------------------------------------------------------
    // Poloha: vo výške tlačidla START WAVE
    // ------------------------------------------------------------------

    void UpdatePosition()
    {
        if (canvas == null) return;

        if (startWave == null && Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 1f;
            var swb = FindAnyObjectByType<StartWaveButton>(FindObjectsInactive.Include);
            if (swb != null) startWave = swb.transform as RectTransform;
        }

        float y = FallbackY;
        if (startWave != null)
        {
            var owner = startWave.GetComponentInParent<Canvas>(true);
            if (owner != null && owner.rootCanvas.renderMode != RenderMode.WorldSpace)
            {
                owner = owner.rootCanvas;
                var cam = owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;

                // Stred tlačidla START WAVE v pixeloch obrazovky (y = 0 dole) -> jednotky nášho canvasu.
                var corners = new Vector3[4];
                startWave.GetWorldCorners(corners);
                float bottom = RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y;
                float top = RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y;
                float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                y = (bottom + top) * 0.5f / scale;
            }
        }

        button.anchoredPosition = new Vector2(LeftMargin, y);
    }

    // ------------------------------------------------------------------

    static Sprite PillSprite()
    {
        // Pozor: vypnutý Domain Reload - zničený sprite treba spoznať cez Unity "== null".
        if (pillSprite == null)
            pillSprite = UpgradePopup.Sprites.RoundRect(280, 64, 30f, 4.5f, PurpleDark, PurpleLight, Gold, GoldLight);
        return pillSprite;
    }
}
