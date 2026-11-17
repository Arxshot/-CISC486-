using System.Collections.Generic;
using UnityEngine;

// Procedural 9-sliced rounded rectangles and circles, so the cartoon UI needs no image files.
public static class RoundedSprite
{
    private static readonly Dictionary<(int, int), Sprite> Cache = new();
    private static Sprite circle;

    // radius: corner radius in pixels. Use Image.Type.Sliced so it scales to any size.
    public static Sprite Get(int radius = 24, int border = 0)
    {
        if (Cache.TryGetValue((radius, border), out var cached) && cached != null)
            return cached;

        int size = radius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"Rounded {radius}/{border}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded square filling the texture.
                float dx = Mathf.Max(Mathf.Abs(x - center) - (half - radius - 0.5f), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - center) - (half - radius - 0.5f), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);
                if (border > 0)
                    alpha *= Mathf.Clamp01(distance + border + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float edge = radius + 1;
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        Cache[(radius, border)] = sprite;
        return sprite;
    }

    public static Sprite Circle()
    {
        if (circle == null)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - (size * 0.5f - 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            circle.hideFlags = HideFlags.DontSave;
        }
        return circle;
    }
}
