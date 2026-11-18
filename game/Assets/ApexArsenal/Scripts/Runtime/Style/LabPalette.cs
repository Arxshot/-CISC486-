using UnityEngine;

// The Apex Arsenal look: a clean, cartoony test lab. Glossy white panels, dark grey grid floors,
// chunky outlines and neon blue / neon pink light strips.
public static class LabPalette
{
    public static readonly Color NeonBlue = Hex(0x2EC8FF);
    public static readonly Color NeonPink = Hex(0xFF3EA5);
    public static readonly Color Lime = Hex(0x9BFF4A);
    public static readonly Color Violet = Hex(0xA66BFF);

    public static readonly Color PanelWhite = Hex(0xF2F5F8);
    public static readonly Color PanelGrey = Hex(0xC9D0D8);
    public static readonly Color Floor = Hex(0x3A4048);
    public static readonly Color FloorLine = Hex(0x2A2F36);
    public static readonly Color Ink = Hex(0x15181D);
    public static readonly Color Danger = Hex(0xFF4B5C);

    public static readonly Color[] Players = { NeonBlue, NeonPink, Lime, Violet };

    public static Color Player(int index) => Players[Mathf.Abs(index) % Players.Length];

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
