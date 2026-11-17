using TMPro;
using UnityEngine;

// Asset references the code-built scenes need. Created and filled by Apex Arsenal > Build Scenes & Assets,
// so the materials and shaders are real assets and survive into player builds.
[CreateAssetMenu(menuName = "Apex Arsenal/Lab Style")]
public class LabStyle : ScriptableObject
{
    [Header("Surfaces")]
    public Material panelWhite;
    public Material panelGrey;
    public Material floor;
    public Material floorLine;
    public Material ink;

    [Header("Glow")]
    public Material neonBlue;
    public Material neonPink;
    public Material laser;
    public Material glass;

    [Header("Characters")]
    public Material outline;
    public Material playerRing;
    public Material shadowBlob;

    [Header("UI")]
    public TMP_FontAsset font;

    [Header("Menu Art (from Assets/UI_Graphics)")]
    public Sprite logo;
    public ButtonArt coopButton = new();
    public ButtonArt pvpButton = new();
    public ButtonArt settingsButton = new();
    public ButtonArt exitButton = new();

    [Header("Prefabs")]
    public GameObject coopPlayerPrefab;
    public GameObject pvpPlayerPrefab;

    // A hand-drawn button: one sprite per state. Hover and pressed fall back to idle when missing.
    [System.Serializable]
    public class ButtonArt
    {
        public Sprite idle;
        public Sprite hover;
        public Sprite pressed;

        public bool IsValid => idle != null;
    }

    // The style of the running scene, set by the scene bootstrap.
    public static LabStyle Active { get; set; }

    // Tints a copy of a glow material, e.g. a player-coloured ring. Copies are cached per colour.
    private readonly System.Collections.Generic.Dictionary<(Material, Color), Material> tinted = new();

    public Material Tinted(Material source, Color color)
    {
        if (source == null)
            return null;
        if (tinted.TryGetValue((source, color), out var cached) && cached != null)
            return cached;
        var copy = new Material(source) { name = $"{source.name} ({ColorUtility.ToHtmlStringRGB(color)})" };
        SetColor(copy, color);
        tinted[(source, color)] = copy;
        return copy;
    }

    public static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 2.2f);
    }
}
