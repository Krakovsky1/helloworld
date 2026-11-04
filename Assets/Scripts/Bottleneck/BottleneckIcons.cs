using UnityEngine;

// FR-08: ikony a podklad varovania sa kreslia kódom (netreba importovať obrázky).
// nestíha = trojuholník s "!", plná = kruh s vodorovnou čiarou, voľná = kruh s fajkou.
// Tvar ikony sa líši, takže stav je rozpoznateľný aj bez farieb (farboslepí hráči).
public static class BottleneckIcons
{
    public static readonly Color BottleneckColor = new Color(0.86f, 0.50f, 0.10f, 0.95f);  // oranžová (nestíha, hostia čakajú)
    public static readonly Color FullColor = new Color(0.80f, 0.20f, 0.16f, 0.95f);        // červená (plná)
    public static readonly Color FreeColor = new Color(0.16f, 0.58f, 0.33f, 0.95f);        // zelená
    static readonly Color Amber = new Color(1f, 0.76f, 0.18f, 1f);
    static readonly Color Dark = new Color(0.18f, 0.08f, 0.04f, 1f);
    static readonly Color White = Color.white;
    static readonly Color Green = new Color(0.16f, 0.58f, 0.33f, 1f);
    static readonly Color Red = new Color(0.80f, 0.20f, 0.16f, 1f);

    static Sprite warning, full, free, card;

    public static Sprite Warning
    {
        get
        {
            if (warning == null) warning = Draw(128, WarningPixel);
            return warning;
        }
    }

    public static Sprite Full
    {
        get
        {
            if (full == null) full = Draw(128, FullPixel);
            return full;
        }
    }

    public static Sprite Free
    {
        get
        {
            if (free == null) free = Draw(128, FreePixel);
            return free;
        }
    }

    /// <summary>Biely zaoblený obdĺžnik (9-slice), farbí sa cez Image.color.</summary>
    public static Sprite Card
    {
        get
        {
            if (card != null) return card;
            const int w = 96, h = 64;
            const float r = 22f;
            var tex = NewTexture(w, h);
            var px = new Color[w * h];
            var half = new Vector2(w * 0.5f, h * 0.5f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                    float d = SdRoundBox(p, half, r);
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            card = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return card;
        }
    }

    // ---- kreslenie s vyhladzovaním (4x4 vzorky na pixel) ----

    delegate Color PixelFn(float x, float y); // x, y v rozsahu 0..1, y hore

    static Sprite Draw(int size, PixelFn fn)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        const int ss = 4;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = (x + (sx + 0.5f) / ss) / size;
                        float v = (y + (sy + 0.5f) / ss) / size;
                        Color c = fn(u, v);
                        // premultiplied sčítanie, aby okraje neboli tmavé
                        sum.r += c.r * c.a; sum.g += c.g * c.a; sum.b += c.b * c.a; sum.a += c.a;
                    }
                float n = ss * ss;
                Color o = sum.a > 0f
                    ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a / n)
                    : Color.clear;
                px[y * size + x] = o;
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Color WarningPixel(float x, float y)
    {
        // tmavý obrys trojuholníka, vnútri jantárová výplň, v strede "!"
        var a = new Vector2(0.5f, 0.95f);
        var b = new Vector2(0.03f, 0.08f);
        var c = new Vector2(0.97f, 0.08f);
        var p = new Vector2(x, y);
        if (!InTriangle(p, a, b, c)) return Color.clear;

        var ia = new Vector2(0.5f, 0.84f);
        var ib = new Vector2(0.12f, 0.14f);
        var ic = new Vector2(0.88f, 0.14f);
        if (!InTriangle(p, ia, ib, ic)) return Dark;

        // výkričník: zvislá palička + bodka
        bool bar = Mathf.Abs(x - 0.5f) < 0.055f && y > 0.38f && y < 0.70f;
        bool dot = (p - new Vector2(0.5f, 0.26f)).sqrMagnitude < 0.06f * 0.06f;
        return bar || dot ? Dark : Amber;
    }

    static Color FullPixel(float x, float y)
    {
        var p = new Vector2(x, y);
        float d = (p - new Vector2(0.5f, 0.5f)).magnitude;
        if (d > 0.47f) return Color.clear;
        if (d > 0.41f) return Red;

        // hrubá vodorovná čiara (ako značka "plné / vstup zakázaný")
        bool bar = Mathf.Abs(y - 0.5f) < 0.085f && Mathf.Abs(x - 0.5f) < 0.24f;
        return bar ? Red : White;
    }

    static Color FreePixel(float x, float y)
    {
        var p = new Vector2(x, y);
        float d = (p - new Vector2(0.5f, 0.5f)).magnitude;
        if (d > 0.47f) return Color.clear;
        if (d > 0.41f) return Green;

        // fajka z dvoch hrubých čiar
        bool check = SegmentDistance(p, new Vector2(0.27f, 0.50f), new Vector2(0.43f, 0.33f)) < 0.065f ||
                     SegmentDistance(p, new Vector2(0.43f, 0.33f), new Vector2(0.74f, 0.66f)) < 0.065f;
        return check ? Green : White;
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    static float SdRoundBox(Vector2 point, Vector2 half, float radius)
    {
        var q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half + new Vector2(radius, radius);
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f) +
               new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - radius;
    }

    static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
