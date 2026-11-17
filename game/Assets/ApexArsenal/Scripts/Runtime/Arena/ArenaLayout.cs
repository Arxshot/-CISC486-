using System;
using System.Collections.Generic;
using UnityEngine;

public enum Cell : byte
{
    Floor,
    Cover, // waist-to-shoulder block that stops shots
    Pillar, // tall lab pillar with a neon strip
    Platform, // raised walkable floor
    Ramp, // slope from the floor up to a platform
}

// Pure data for one generated test chamber, mirrored left/right so PvP spawns are fair.
// Kept free of GameObjects so it can be unit tested and previewed.
public sealed class ArenaLayout
{
    public const float CellSize = 2f;
    public const float PlatformHeight = 0.5f;

    public readonly int Width;
    public readonly int Depth;
    public readonly int Seed;
    public readonly ArenaPreset Preset;
    public readonly Cell[,] Cells;
    // For each Ramp cell, the direction (in cells) towards its platform.
    public readonly Dictionary<Vector2Int, Vector2Int> RampDirections = new();
    public readonly List<Vector2Int> CoopSpawns = new();
    public readonly List<Vector2Int> PvpSpawns = new();
    public readonly List<Vector2Int> CrateAnchors = new();
    public readonly List<Vector2Int> TurretSockets = new();
    // Laser gates sit on the centre line; each covers rows [start, end] inclusive.
    public readonly List<Vector2Int> GateRows = new();

    private ArenaLayout(ArenaPreset preset, int seed)
    {
        Preset = preset;
        Seed = seed;
        Width = Mathf.Max(4, preset.HalfWidth) * 2;
        Depth = Mathf.Max(6, preset.Depth);
        Cells = new Cell[Width, Depth];
    }

    public int HalfWidth => Width / 2;

    public static ArenaLayout Generate(ArenaPresetId presetId, int matchSeed)
    {
        var preset = ArenaPresets.Get(presetId);
        int baseSeed = preset.FixedSeed != 0 ? preset.FixedSeed : matchSeed;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var layout = new ArenaLayout(preset, baseSeed + attempt * 7919);
            if (layout.Build(withObstacles: true))
                return layout;
        }
        var open = new ArenaLayout(preset, baseSeed);
        open.Build(withObstacles: false);
        return open;
    }

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Depth;

    public Cell this[Vector2Int cell] => Cells[cell.x, cell.y];

    public static bool IsWalkable(Cell cell) => cell == Cell.Floor || cell == Cell.Platform || cell == Cell.Ramp;

    public Vector2Int Mirror(Vector2Int cell) => new Vector2Int(Width - 1 - cell.x, cell.y);

    // Centre of a cell in arena-local space (y = top of the floor surface at that cell).
    public Vector3 ToLocal(Vector2Int cell)
    {
        float height = Cells[cell.x, cell.y] == Cell.Platform ? PlatformHeight : Cells[cell.x, cell.y] == Cell.Ramp ? PlatformHeight * 0.5f : 0f;
        return new Vector3((cell.x - Width * 0.5f + 0.5f) * CellSize, height, (cell.y - Depth * 0.5f + 0.5f) * CellSize);
    }

    public Vector2 SizeMeters => new Vector2(Width * CellSize, Depth * CellSize);

    private bool Build(bool withObstacles)
    {
        var random = new System.Random(Seed);
        var reserved = new HashSet<Vector2Int>();
        int half = HalfWidth;

        // Spawns: co-op players start together at the front; PvP players start in the four corners.
        CoopSpawns.AddRange(new[] { new Vector2Int(half - 2, 1), new Vector2Int(half + 1, 1), new Vector2Int(half - 3, 2), new Vector2Int(half + 2, 2) });
        PvpSpawns.AddRange(new[] { new Vector2Int(1, 1), new Vector2Int(Width - 2, Depth - 2), new Vector2Int(Width - 2, 1), new Vector2Int(1, Depth - 2) });
        foreach (var spawn in CoopSpawns)
            ReserveArea(reserved, spawn, 1);
        foreach (var spawn in PvpSpawns)
            ReserveArea(reserved, spawn, 1);

        // Crate drop anchors: the middle of the room plus a mirrored pair.
        CrateAnchors.Add(new Vector2Int(half - 1, Depth / 2));
        var anchor = new Vector2Int(Mathf.Max(2, half / 2 - 1), Depth / 2 + (random.Next(2) == 0 ? -2 : 2));
        CrateAnchors.Add(anchor);
        CrateAnchors.Add(Mirror(anchor));
        foreach (var crate in CrateAnchors)
            ReserveArea(reserved, crate, 0);

        // Laser gates across the centre line, evenly spaced front to back.
        int lanes = Mathf.Clamp(Preset.GateLanes, 0, 3);
        for (int lane = 0; lane < lanes; lane++)
        {
            int center = Mathf.RoundToInt(Depth * (lane + 1f) / (lanes + 1f));
            var rows = new Vector2Int(Mathf.Max(3, center - 1), Mathf.Min(Depth - 4, center + 1));
            if (rows.y < rows.x)
                continue;
            GateRows.Add(rows);
            for (int z = rows.x - 1; z <= rows.y + 1; z++)
                for (int x = half - 2; x <= half + 1; x++)
                    reserved.Add(new Vector2Int(x, z));
        }

        if (withObstacles)
        {
            PlacePlatforms(random, reserved);
            PlaceCover(random, reserved);
        }

        if (!AllReachable(out var reachable))
            return false;
        // Seal unreachable pockets so nothing spawns or looks walkable where players can't go.
        for (int x = 0; x < Width; x++)
            for (int z = 0; z < Depth; z++)
                if (IsWalkable(Cells[x, z]) && !reachable.Contains(new Vector2Int(x, z)))
                    Cells[x, z] = Cell.Cover;

        PlaceTurretSockets(random, reserved, reachable);
        return TurretSockets.Count >= 4;
    }

    private void PlacePlatforms(System.Random random, HashSet<Vector2Int> reserved)
    {
        int half = HalfWidth;
        for (int p = 0; p < Preset.Platforms; p++)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                int w = 2 + random.Next(2);
                int d = 2;
                int x0 = 1 + random.Next(Mathf.Max(1, half - w - 3));
                int z0 = 3 + random.Next(Mathf.Max(1, Depth - d - 6));
                var ramp = new Vector2Int(x0 + random.Next(w), z0 - 1);
                var rampDir = Vector2Int.up;
                if (random.Next(2) == 0)
                {
                    ramp = new Vector2Int(x0 + random.Next(w), z0 + d);
                    rampDir = Vector2Int.down;
                }
                if (!AreaFree(reserved, x0 - 1, z0 - 1, x0 + w, z0 + d) || reserved.Contains(ramp))
                    continue;

                for (int x = x0; x < x0 + w; x++)
                    for (int z = z0; z < z0 + d; z++)
                        SetMirrored(new Vector2Int(x, z), Cell.Platform, reserved);
                SetMirrored(ramp, Cell.Ramp, reserved);
                RampDirections[ramp] = rampDir;
                RampDirections[Mirror(ramp)] = rampDir;
                // Keep the ramp's foot clear.
                var foot = ramp - rampDir;
                reserved.Add(foot);
                reserved.Add(Mirror(foot));
                break;
            }
        }
    }

    private void PlaceCover(System.Random random, HashSet<Vector2Int> reserved)
    {
        int half = HalfWidth;
        for (int x = 0; x < half; x++)
            for (int z = 0; z < Depth; z++)
            {
                var cell = new Vector2Int(x, z);
                if (reserved.Contains(cell) || Cells[x, z] != Cell.Floor)
                    continue;
                if (random.NextDouble() >= Preset.CoverDensity)
                    continue;
                var type = random.NextDouble() < Preset.TallCoverChance ? Cell.Pillar : Cell.Cover;
                SetMirrored(cell, type, reserved);
                // Sometimes stretch cover into a short wall for nicer hiding spots.
                var extend = cell + (random.Next(2) == 0 ? Vector2Int.right : Vector2Int.up);
                if (type == Cell.Cover && random.NextDouble() < 0.45 && extend.x < half && InBounds(extend) && !reserved.Contains(extend) && Cells[extend.x, extend.y] == Cell.Floor)
                    SetMirrored(extend, Cell.Cover, reserved);
            }
    }

    private void PlaceTurretSockets(System.Random random, HashSet<Vector2Int> reserved, HashSet<Vector2Int> reachable)
    {
        var candidates = new List<Vector2Int>();
        int half = HalfWidth;
        for (int x = 1; x < half - 1; x++)
            for (int z = Depth / 3; z < Depth - 1; z++)
            {
                var cell = new Vector2Int(x, z);
                if (Cells[x, z] != Cell.Floor || reserved.Contains(cell) || !reachable.Contains(cell))
                    continue;
                bool farFromPlayers = true;
                foreach (var spawn in CoopSpawns)
                    if (Mathf.Abs(spawn.x - x) + Mathf.Abs(spawn.y - z) < 5)
                        farFromPlayers = false;
                if (farFromPlayers)
                    candidates.Add(cell);
            }
        // Fisher-Yates with the layout's seed keeps sockets deterministic.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var used = new HashSet<Vector2Int>();
        foreach (var cell in candidates)
        {
            if (TurretSockets.Count >= 10)
                break;
            bool spaced = true;
            foreach (var other in used)
                if (Mathf.Abs(other.x - cell.x) + Mathf.Abs(other.y - cell.y) < 3)
                    spaced = false;
            if (!spaced)
                continue;
            used.Add(cell);
            TurretSockets.Add(cell);
            TurretSockets.Add(Mirror(cell));
        }
    }

    // Flood fill from the first co-op spawn. Floor and platform only connect through ramps.
    public bool AllReachable(out HashSet<Vector2Int> reachable)
    {
        reachable = new HashSet<Vector2Int>();
        var start = CoopSpawns[0];
        if (!IsWalkable(this[start]))
            return false;
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        reachable.Add(start);
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var step in steps)
            {
                var next = cell + step;
                if (!InBounds(next) || reachable.Contains(next) || !CanStep(cell, next))
                    continue;
                reachable.Add(next);
                queue.Enqueue(next);
            }
        }

        foreach (var key in KeyCells())
            if (!reachable.Contains(key))
                return false;
        return true;
    }

    public IEnumerable<Vector2Int> KeyCells()
    {
        foreach (var cell in CoopSpawns)
            yield return cell;
        foreach (var cell in PvpSpawns)
            yield return cell;
        foreach (var cell in CrateAnchors)
            yield return cell;
        foreach (var cell in TurretSockets)
            yield return cell;
    }

    private bool CanStep(Vector2Int from, Vector2Int to)
    {
        Cell a = this[from], b = this[to];
        if (!IsWalkable(a) || !IsWalkable(b))
            return false;
        bool aHigh = a == Cell.Platform, bHigh = b == Cell.Platform;
        if (aHigh == bHigh && a != Cell.Ramp && b != Cell.Ramp)
            return true;
        // Ramps: enter from the foot (floor side) or the platform side, never sideways.
        if (a == Cell.Ramp)
            return RampConnects(from, to);
        if (b == Cell.Ramp)
            return RampConnects(to, from);
        return false;
    }

    private bool RampConnects(Vector2Int ramp, Vector2Int other)
    {
        if (!RampDirections.TryGetValue(ramp, out var direction))
            return false;
        var offset = other - ramp;
        if (offset == direction)
            return this[other] == Cell.Platform || this[other] == Cell.Ramp;
        if (offset == -direction)
            return this[other] == Cell.Floor;
        return false;
    }

    private void SetMirrored(Vector2Int cell, Cell type, HashSet<Vector2Int> reserved)
    {
        Cells[cell.x, cell.y] = type;
        var mirror = Mirror(cell);
        Cells[mirror.x, mirror.y] = type;
        reserved.Add(cell);
        reserved.Add(mirror);
    }

    private bool AreaFree(HashSet<Vector2Int> reserved, int x0, int z0, int x1, int z1)
    {
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                var cell = new Vector2Int(x, z);
                if (!InBounds(cell) || x >= HalfWidth - 1 || reserved.Contains(cell) || Cells[x, z] != Cell.Floor)
                    return false;
            }
        return true;
    }

    private void ReserveArea(HashSet<Vector2Int> reserved, Vector2Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                var cell = center + new Vector2Int(x, z);
                if (InBounds(cell))
                {
                    reserved.Add(cell);
                    reserved.Add(Mirror(cell));
                }
            }
    }
}
