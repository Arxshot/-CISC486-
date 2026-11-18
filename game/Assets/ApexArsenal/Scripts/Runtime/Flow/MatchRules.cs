public enum MatchOutcome
{
    Ongoing,
    Victory,
    Defeat,
    Winner,
    Draw,
}

// Pure win/lose rules, kept free of Unity objects so they can be unit tested.
public static class MatchRules
{
    // alive[i] is whether joined player i is still in the fight.
    // Co-op: win when every wave is cleared, lose when every player is down.
    // PvP (free-for-all): the last player standing wins; if everyone falls at once it's a draw.
    public static MatchOutcome Evaluate(GameMode mode, bool[] alive, int enemiesLeft, int wavesLeft, out int winnerIndex)
    {
        winnerIndex = -1;
        int joined = alive?.Length ?? 0;
        if (joined == 0)
            return MatchOutcome.Ongoing;

        int aliveCount = 0;
        for (int i = 0; i < joined; i++)
            if (alive[i])
            {
                aliveCount++;
                winnerIndex = i;
            }

        if (mode == GameMode.Coop)
        {
            winnerIndex = -1;
            if (aliveCount == 0)
                return MatchOutcome.Defeat;
            return enemiesLeft <= 0 && wavesLeft <= 0 ? MatchOutcome.Victory : MatchOutcome.Ongoing;
        }

        if (joined < 2)
        {
            winnerIndex = -1;
            return MatchOutcome.Ongoing;
        }
        if (aliveCount == 1)
            return MatchOutcome.Winner;
        winnerIndex = -1;
        return aliveCount == 0 ? MatchOutcome.Draw : MatchOutcome.Ongoing;
    }
}
