public enum ArenaPresetId
{
    Crossfire,
    Gauntlet,
    Overlook,
    Random,
}

// Tuning for each named test chamber. The generator mixes these with a seed, so every preset has
// a fixed signature look while Random Lab rolls a fresh one each time.
public readonly struct ArenaPreset
{
    public readonly ArenaPresetId Id;
    public readonly string Title;
    public readonly string Tagline;
    public readonly int HalfWidth; // cells either side of the centre line (x)
    public readonly int Depth; // cells front to back (z)
    public readonly float CoverDensity;
    public readonly float TallCoverChance;
    public readonly int Platforms; // raised platforms per half
    public readonly int GateLanes; // laser gates across the centre line
    public readonly int FixedSeed; // 0 = use the match seed

    public ArenaPreset(ArenaPresetId id, string title, string tagline, int halfWidth, int depth, float coverDensity, float tallCoverChance, int platforms, int gateLanes, int fixedSeed)
    {
        Id = id;
        Title = title;
        Tagline = tagline;
        HalfWidth = halfWidth;
        Depth = depth;
        CoverDensity = coverDensity;
        TallCoverChance = tallCoverChance;
        Platforms = platforms;
        GateLanes = gateLanes;
        FixedSeed = fixedSeed;
    }
}

public static class ArenaPresets
{
    public static readonly ArenaPreset[] All =
    {
        new ArenaPreset(ArenaPresetId.Crossfire, "CHAMBER 01: CROSSFIRE", "Open floor, light cover. Keep moving.", 7, 11, 0.07f, 0.2f, 0, 1, 1101),
        new ArenaPreset(ArenaPresetId.Gauntlet, "CHAMBER 02: GAUNTLET", "Tight lanes and twin laser gates.", 6, 13, 0.13f, 0.45f, 0, 2, 2202),
        new ArenaPreset(ArenaPresetId.Overlook, "CHAMBER 03: OVERLOOK", "Raised platforms. Take the high ground.", 8, 12, 0.09f, 0.3f, 2, 1, 3303),
        new ArenaPreset(ArenaPresetId.Random, "RANDOM LAB", "A fresh chamber, generated just for you.", 7, 12, 0.1f, 0.35f, 1, 1, 0),
    };

    public static ArenaPreset Get(ArenaPresetId id)
    {
        foreach (var preset in All)
            if (preset.Id == id)
                return preset;
        return All[0];
    }
}
