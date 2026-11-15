using UnityEngine;

/// <summary>
/// Builds a detailed, game-ready ember-forged trap sprite and a matching polygon collider.
/// The sprite is generated once per prefab instance; no scene objects are spawned.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(PolygonCollider2D))]
public sealed class EmberTrapVisual : MonoBehaviour
{
    private const int TextureWidth = 512;
    private const int TextureHeight = 192;
    private const float PixelsPerUnit = 128f;

    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polygonCollider;
    private float pulseTime;
    private Color baseTint = Color.white;

    private static readonly Vector2[] Silhouette =
    {
        new Vector2(30, 76), new Vector2(44, 62), new Vector2(60, 65),
        new Vector2(72, 36), new Vector2(90, 60), new Vector2(130, 54),
        new Vector2(148, 42), new Vector2(164, 58), new Vector2(348, 58),
        new Vector2(364, 42), new Vector2(380, 54), new Vector2(422, 60),
        new Vector2(440, 36), new Vector2(458, 65), new Vector2(474, 62),
        new Vector2(490, 76), new Vector2(474, 91), new Vector2(490, 106),
        new Vector2(474, 122), new Vector2(458, 126), new Vector2(440, 154),
        new Vector2(422, 130), new Vector2(380, 136), new Vector2(364, 148),
        new Vector2(348, 132), new Vector2(164, 132), new Vector2(148, 148),
        new Vector2(130, 136), new Vector2(90, 130), new Vector2(72, 154),
        new Vector2(60, 126), new Vector2(44, 122), new Vector2(30, 106),
        new Vector2(46, 91)
    };

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        polygonCollider = GetComponent<PolygonCollider2D>();
        BuildSprite();
        BuildCollider();
        baseTint = Color.white;
    }

    private void Update()
    {
        pulseTime += Time.deltaTime;
        float pulse = 0.94f + Mathf.Sin(pulseTime * 5.2f) * 0.045f;
        spriteRenderer.color = new Color(pulse, pulse * 0.985f, pulse * 0.96f, 1f);
    }

    private void BuildSprite()
    {
        Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        texture.name = "Generated_EmberForgedTrap";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[TextureWidth * TextureHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        texture.SetPixels(pixels);

        // Soft hot-metal aura behind the forged silhouette.
        DrawCapsule(texture, new Vector2(48, 96), new Vector2(464, 96), 49, new Color(1f, 0.12f, 0.015f, 0.10f));
        DrawCapsule(texture, new Vector2(64, 96), new Vector2(448, 96), 34, new Color(1f, 0.24f, 0.025f, 0.12f));

        Color ironEdge = new Color(0.055f, 0.061f, 0.071f, 1f);
        Color ironFace = new Color(0.16f, 0.18f, 0.19f, 1f);
        Color ironHighlight = new Color(0.31f, 0.33f, 0.32f, 1f);
        Color emberDark = new Color(0.48f, 0.075f, 0.018f, 1f);
        Color emberHot = new Color(1f, 0.27f, 0.025f, 1f);
        Color emberCore = new Color(1f, 0.72f, 0.20f, 1f);

        FillPolygon(texture, Silhouette, ironEdge);
        FillPolygon(texture, new[]
        {
            new Vector2(43, 84), new Vector2(61, 72), new Vector2(146, 67),
            new Vector2(160, 76), new Vector2(352, 76), new Vector2(366, 67),
            new Vector2(459, 84), new Vector2(459, 108), new Vector2(366, 125),
            new Vector2(352, 116), new Vector2(160, 116), new Vector2(146, 125),
            new Vector2(61, 108)
        }, ironFace);

        // Raised steel spine, machined bevels and segmented hot channel.
        DrawCapsule(texture, new Vector2(106, 96), new Vector2(406, 96), 20, emberDark);
        DrawCapsule(texture, new Vector2(112, 96), new Vector2(400, 96), 12, emberHot);
        DrawCapsule(texture, new Vector2(128, 96), new Vector2(384, 96), 5, emberCore);
        DrawCapsule(texture, new Vector2(112, 78), new Vector2(400, 78), 3, ironHighlight);
        DrawCapsule(texture, new Vector2(112, 114), new Vector2(400, 114), 3, new Color(0.035f, 0.04f, 0.047f, 1f));

        // Heavy toothed end wheels with hot hubs and radial forged spokes.
        DrawGear(texture, new Vector2(82, 96), 43, ironEdge, ironHighlight, emberHot, emberCore);
        DrawGear(texture, new Vector2(430, 96), 43, ironEdge, ironHighlight, emberHot, emberCore);

        // Additional teeth along the beam make the full silhouette read as a trap.
        for (int x = 158; x <= 354; x += 39)
        {
            FillTriangle(texture, new Vector2(x - 9, 67), new Vector2(x, 43), new Vector2(x + 9, 67), ironEdge);
            FillTriangle(texture, new Vector2(x - 8, 125), new Vector2(x, 149), new Vector2(x + 8, 125), ironEdge);
            DrawLine(texture, new Vector2(x - 4, 65), new Vector2(x, 51), 2, ironHighlight);
            DrawLine(texture, new Vector2(x - 4, 127), new Vector2(x, 141), 2, ironHighlight);
        }

        // Rivets and heat scoring.
        for (int x = 124; x <= 388; x += 44)
        {
            FillCircle(texture, new Vector2(x, 72), 4, new Color(0.025f, 0.03f, 0.035f, 1f));
            FillCircle(texture, new Vector2(x - 1, 73), 1.6f, ironHighlight);
            FillCircle(texture, new Vector2(x, 120), 4, new Color(0.025f, 0.03f, 0.035f, 1f));
            FillCircle(texture, new Vector2(x - 1, 121), 1.6f, ironHighlight);
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TextureWidth, TextureHeight),
            new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.Tight);
        sprite.name = "SPR_EmberForgedRotatingTrap";
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = baseTint;
        spriteRenderer.sortingOrder = 40;
    }

    private void BuildCollider()
    {
        Vector2[] points = new Vector2[Silhouette.Length];
        for (int i = 0; i < Silhouette.Length; i++)
        {
            points[i] = new Vector2(
                (Silhouette[i].x - TextureWidth * 0.5f) / PixelsPerUnit,
                (Silhouette[i].y - TextureHeight * 0.5f) / PixelsPerUnit);
        }

        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, points);
        polygonCollider.isTrigger = false;
    }

    private static void DrawGear(Texture2D t, Vector2 c, float radius, Color edge, Color highlight, Color hot, Color core)
    {
        FillCircle(t, c, radius, edge);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f;
            Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            Vector2 root = c + radial * (radius - 8f);
            FillTriangle(t, root - tangent * 7f, c + radial * (radius + 7f), root + tangent * 7f, edge);
        }

        FillCircle(t, c, radius - 8f, new Color(0.12f, 0.14f, 0.15f, 1f));
        FillCircle(t, c, radius - 13f, highlight);
        FillCircle(t, c, radius - 19f, new Color(0.055f, 0.06f, 0.065f, 1f));

        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(t, c + d * 13f, c + d * 28f, 5f, new Color(0.34f, 0.35f, 0.33f, 1f));
            FillCircle(t, c + d * 32f, 2.8f, new Color(0.035f, 0.04f, 0.045f, 1f));
        }

        FillCircle(t, c, 15f, hot);
        FillCircle(t, c, 8f, core);
        FillCircle(t, c, 3f, new Color(1f, 0.9f, 0.55f, 1f));
    }

    private static void FillCircle(Texture2D t, Vector2 c, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(c.x - radius));
        int maxX = Mathf.Min(TextureWidth - 1, Mathf.CeilToInt(c.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(c.y - radius));
        int maxY = Mathf.Min(TextureHeight - 1, Mathf.CeilToInt(c.y + radius));
        float r2 = radius * radius;
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float dx = x + 0.5f - c.x;
            float dy = y + 0.5f - c.y;
            if (dx * dx + dy * dy <= r2) BlendPixel(t, x, y, color);
        }
    }

    private static void DrawCapsule(Texture2D t, Vector2 a, Vector2 b, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius));
        int maxX = Mathf.Min(TextureWidth - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius));
        int maxY = Mathf.Min(TextureHeight - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));
        Vector2 ab = b - a;
        float lengthSq = Mathf.Max(0.0001f, ab.sqrMagnitude);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            if ((p - (a + ab * u)).sqrMagnitude <= radius * radius)
                BlendPixel(t, x, y, color);
        }
    }

    private static void DrawLine(Texture2D t, Vector2 a, Vector2 b, float width, Color color)
    {
        DrawCapsule(t, a, b, width * 0.5f, color);
    }

    private static void FillTriangle(Texture2D t, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        FillPolygon(t, new[] { a, b, c }, color);
    }

    private static void FillPolygon(Texture2D t, Vector2[] points, Color color)
    {
        float minX = TextureWidth - 1, minY = TextureHeight - 1, maxX = 0, maxY = 0;
        for (int i = 0; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, points[i].x); maxX = Mathf.Max(maxX, points[i].x);
            minY = Mathf.Min(minY, points[i].y); maxY = Mathf.Max(maxY, points[i].y);
        }

        int x0 = Mathf.Clamp(Mathf.FloorToInt(minX), 0, TextureWidth - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX), 0, TextureWidth - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, TextureHeight - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY), 0, TextureHeight - 1);

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 pi = points[i], pj = points[j];
                bool crosses = ((pi.y > y + 0.5f) != (pj.y > y + 0.5f)) &&
                    (x + 0.5f < (pj.x - pi.x) * (y + 0.5f - pi.y) / (pj.y - pi.y + 0.00001f) + pi.x);
                if (crosses) inside = !inside;
            }
            if (inside) BlendPixel(t, x, y, color);
        }
    }

    private static void BlendPixel(Texture2D t, int x, int y, Color source)
    {
        if (source.a >= 0.999f)
        {
            t.SetPixel(x, y, source);
            return;
        }

        Color destination = t.GetPixel(x, y);
        float alpha = source.a + destination.a * (1f - source.a);
        if (alpha <= 0.0001f) return;
        Color result = (source * source.a + destination * destination.a * (1f - source.a)) / alpha;
        result.a = alpha;
        t.SetPixel(x, y, result);
    }
}
