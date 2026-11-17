public enum GameMode
{
    // 1-4 players against the lab's defences. Played alone, this is single player.
    Coop,
    // 2-4 player free-for-all.
    PvP,
}

// The choices made in the main menu, read by the arena scene. Static, so it survives scene loads.
public static class MatchSettings
{
    public static GameMode Mode { get; set; } = GameMode.Coop;
    public static ArenaPresetId Preset { get; set; } = ArenaPresetId.Crossfire;
    public static int Seed { get; set; } = 486;

    // Reserved for later: AI enemies in PvP. Off, so PvP is players only.
    public static bool BotsInPvP { get; set; }

    public static bool EnemiesEnabled => Mode == GameMode.Coop || BotsInPvP;
    public static int MinPlayers => Mode == GameMode.PvP ? 2 : 1;
    public const int MaxPlayers = 4;
}
