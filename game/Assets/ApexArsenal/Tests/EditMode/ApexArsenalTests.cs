using NUnit.Framework;
using UnityEngine;

public class MatchRulesTests
{
    [Test]
    public void CoopSoloPlayerKeepsPlayingWhileEnemiesRemain()
    {
        var outcome = MatchRules.Evaluate(GameMode.Coop, new[] { true }, 3, 2, out _);
        Assert.AreEqual(MatchOutcome.Ongoing, outcome);
    }

    [Test]
    public void CoopWinsWhenEveryWaveIsCleared()
    {
        var outcome = MatchRules.Evaluate(GameMode.Coop, new[] { true, false }, 0, 0, out int winner);
        Assert.AreEqual(MatchOutcome.Victory, outcome);
        Assert.AreEqual(-1, winner);
    }

    [Test]
    public void CoopLosesWhenEveryPlayerIsDown()
    {
        Assert.AreEqual(MatchOutcome.Defeat, MatchRules.Evaluate(GameMode.Coop, new[] { false, false }, 4, 1, out _));
    }

    [Test]
    public void CoopIsNotWonWhileWavesAreStillComing()
    {
        Assert.AreEqual(MatchOutcome.Ongoing, MatchRules.Evaluate(GameMode.Coop, new[] { true }, 0, 1, out _));
    }

    [Test]
    public void PvpWaitsForASecondPlayer()
    {
        Assert.AreEqual(MatchOutcome.Ongoing, MatchRules.Evaluate(GameMode.PvP, new[] { true }, 0, 0, out _));
    }

    [Test]
    public void PvpLastPlayerStandingWins()
    {
        var outcome = MatchRules.Evaluate(GameMode.PvP, new[] { false, false, true, false }, 0, 0, out int winner);
        Assert.AreEqual(MatchOutcome.Winner, outcome);
        Assert.AreEqual(2, winner);
    }

    [Test]
    public void PvpEveryoneDownIsADraw()
    {
        Assert.AreEqual(MatchOutcome.Draw, MatchRules.Evaluate(GameMode.PvP, new[] { false, false }, 0, 0, out _));
    }

    [Test]
    public void NoPlayersMeansStillInLobby()
    {
        Assert.AreEqual(MatchOutcome.Ongoing, MatchRules.Evaluate(GameMode.Coop, new bool[0], 0, 0, out _));
    }
}

public class ArenaLayoutTests
{
    [Test]
    public void SameSeedGivesSameLayout()
    {
        var a = ArenaLayout.Generate(ArenaPresetId.Random, 1234);
        var b = ArenaLayout.Generate(ArenaPresetId.Random, 1234);
        Assert.AreEqual(a.Seed, b.Seed);
        CollectionAssert.AreEqual(a.Cells, b.Cells);
        CollectionAssert.AreEqual(a.TurretSockets, b.TurretSockets);
    }

    [Test]
    public void NamedPresetsIgnoreTheMatchSeed()
    {
        var a = ArenaLayout.Generate(ArenaPresetId.Gauntlet, 1);
        var b = ArenaLayout.Generate(ArenaPresetId.Gauntlet, 999);
        CollectionAssert.AreEqual(a.Cells, b.Cells);
    }

    [Test]
    public void EveryPresetIsMirroredAndFullyReachableAcrossManySeeds([Values] ArenaPresetId preset)
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var layout = ArenaLayout.Generate(preset, seed);
            for (int x = 0; x < layout.Width; x++)
                for (int z = 0; z < layout.Depth; z++)
                {
                    var cell = new Vector2Int(x, z);
                    Assert.AreEqual(layout[cell], layout[layout.Mirror(cell)], $"{preset} seed {seed}: not mirrored at {cell}");
                }
            Assert.IsTrue(layout.AllReachable(out _), $"{preset} seed {seed}: a spawn, drop zone or turret socket is unreachable");
            Assert.AreEqual(4, layout.PvpSpawns.Count);
            Assert.AreEqual(4, layout.CoopSpawns.Count);
            Assert.GreaterOrEqual(layout.TurretSockets.Count, 4, $"{preset} seed {seed}: too few turret sockets");
            Assert.GreaterOrEqual(layout.CrateAnchors.Count, 1);
            foreach (var spawn in layout.PvpSpawns)
                Assert.IsTrue(ArenaLayout.IsWalkable(layout[spawn]));
        }
    }

    [Test]
    public void GauntletHasTwoGates()
    {
        Assert.AreEqual(2, ArenaLayout.Generate(ArenaPresetId.Gauntlet, 0).GateRows.Count);
    }

    [Test]
    public void OverlookHasPlatformsAndRamps()
    {
        var layout = ArenaLayout.Generate(ArenaPresetId.Overlook, 0);
        bool platform = false, ramp = false;
        foreach (var cell in layout.Cells)
        {
            platform |= cell == Cell.Platform;
            ramp |= cell == Cell.Ramp;
        }
        Assert.IsTrue(platform && ramp);
    }
}

public class VolumeSettingsTests
{
    [Test]
    public void GainCurveIsSilentAtZeroFullAtOneAndRising()
    {
        Assert.AreEqual(0f, VolumeSettings.ToGain(0f));
        Assert.AreEqual(1f, VolumeSettings.ToGain(1f));
        Assert.Less(VolumeSettings.ToGain(0.4f), VolumeSettings.ToGain(0.6f));
        Assert.AreEqual(1f, VolumeSettings.ToGain(3f));
    }
}
