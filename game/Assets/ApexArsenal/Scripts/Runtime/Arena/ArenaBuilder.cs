using System.Collections.Generic;
using UnityEngine;

// Turns an ArenaLayout into a lab chamber built from primitives, in the spirit of the test scene's
// LocalArena kit: floor tiles, perimeter walls, cover blocks and targets, plus the event hardware.
// Left half glows neon blue, right half neon pink, so players can call out positions.
public class ArenaBuilder : MonoBehaviour
{
    [SerializeField] private LabStyle style;
    [SerializeField] private ArenaPresetId previewPreset = ArenaPresetId.Crossfire;
    [SerializeField] private int previewSeed = 486;

    public ArenaLayout Layout { get; private set; }
    public Transform Root { get; private set; }
    public readonly List<Vector3> CoopSpawns = new();
    public readonly List<Vector3> PvpSpawns = new();
    public readonly List<Vector3> CrateAnchors = new();
    public readonly List<Vector3> TurretSockets = new();
    public readonly List<HazardGate> Gates = new();
    public Bounds WorldBounds { get; private set; }

    private const float WallThickness = 0.5f;
    private const float BackWallHeight = 4.2f;
    private const float SideWallHeight = 2.4f;
    private const float FrontWallHeight = 0.9f;

    public void Initialize(LabStyle labStyle) => style = labStyle;

    [ContextMenu("Preview Layout")]
    private void Preview() => Build(ArenaLayout.Generate(previewPreset, previewSeed));

    [ContextMenu("Clear Preview")]
    private void Clear()
    {
        var old = transform.Find("Generated Arena");
        if (old != null)
            DestroyImmediate(old.gameObject);
    }

    public void Build(ArenaLayout layout)
    {
        if (style == null)
        {
            Debug.LogError("ArenaBuilder needs a LabStyle. Run Apex Arsenal > Build Scenes & Assets.", this);
            return;
        }
        var old = transform.Find("Generated Arena");
        if (old != null)
        {
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }
        CoopSpawns.Clear();
        PvpSpawns.Clear();
        CrateAnchors.Clear();
        TurretSockets.Clear();
        Gates.Clear();

        Layout = layout;
        Root = new GameObject("Generated Arena").transform;
        Root.SetParent(transform, false);

        BuildShell(layout);
        BuildCells(layout);
        BuildMarkers(layout);
        BuildGates(layout);

        Vector2 size = layout.SizeMeters;
        WorldBounds = new Bounds(transform.position + Vector3.up, new Vector3(size.x, 2f, size.y));
    }

    private void BuildShell(ArenaLayout layout)
    {
        Vector2 size = layout.SizeMeters;
        var shell = Group("Shell");

        var floor = Block(shell, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(size.x, 0.5f, size.y), style.floor);
        floor.layer = 2; // Ignore Raycast: keeps the floor out of cursor picking, like LocalArena.

        float halfX = size.x * 0.5f + WallThickness * 0.5f;
        float halfZ = size.y * 0.5f + WallThickness * 0.5f;
        Block(shell, "Back Wall", new Vector3(0f, BackWallHeight * 0.5f, halfZ), new Vector3(size.x + WallThickness * 2f, BackWallHeight, WallThickness), style.panelWhite);
        Block(shell, "Front Wall", new Vector3(0f, FrontWallHeight * 0.5f, -halfZ), new Vector3(size.x + WallThickness * 2f, FrontWallHeight, WallThickness), style.panelWhite);
        Block(shell, "West Wall", new Vector3(-halfX, SideWallHeight * 0.5f, 0f), new Vector3(WallThickness, SideWallHeight, size.y), style.panelWhite);
        Block(shell, "East Wall", new Vector3(halfX, SideWallHeight * 0.5f, 0f), new Vector3(WallThickness, SideWallHeight, size.y), style.panelWhite);

        // Wall panel seams and neon strips.
        int panels = Mathf.Max(2, Mathf.RoundToInt(size.x / 4f));
        for (int i = 1; i < panels; i++)
        {
            float x = -size.x * 0.5f + size.x * i / panels;
            Decor(shell, "Seam", new Vector3(x, BackWallHeight * 0.5f, halfZ - WallThickness * 0.5f - 0.01f), new Vector3(0.06f, BackWallHeight, 0.02f), style.panelGrey);
        }
        Decor(shell, "Back Strip Blue", new Vector3(-size.x * 0.25f, 0.35f, halfZ - WallThickness * 0.5f - 0.02f), new Vector3(size.x * 0.5f, 0.12f, 0.04f), style.neonBlue);
        Decor(shell, "Back Strip Pink", new Vector3(size.x * 0.25f, 0.35f, halfZ - WallThickness * 0.5f - 0.02f), new Vector3(size.x * 0.5f, 0.12f, 0.04f), style.neonPink);
        Decor(shell, "West Strip", new Vector3(-halfX + WallThickness * 0.5f + 0.02f, SideWallHeight - 0.2f, 0f), new Vector3(0.04f, 0.1f, size.y), style.neonBlue);
        Decor(shell, "East Strip", new Vector3(halfX - WallThickness * 0.5f - 0.02f, SideWallHeight - 0.2f, 0f), new Vector3(0.04f, 0.1f, size.y), style.neonPink);
        Decor(shell, "Centre Line", new Vector3(0f, 0.012f, 0f), new Vector3(0.12f, 0.02f, size.y), style.floorLine);

        // Observation window high on the back wall, with a dark room behind the glass.
        float windowWidth = size.x * 0.45f;
        Decor(shell, "Observation Room", new Vector3(0f, BackWallHeight - 1.1f, halfZ - WallThickness * 0.5f - 0.03f), new Vector3(windowWidth, 1.3f, 0.02f), style.ink);
        Decor(shell, "Observation Glass", new Vector3(0f, BackWallHeight - 1.1f, halfZ - WallThickness * 0.5f - 0.06f), new Vector3(windowWidth, 1.3f, 0.02f), style.glass);
        Decor(shell, "Window Frame Top", new Vector3(0f, BackWallHeight - 0.4f, halfZ - WallThickness * 0.5f - 0.08f), new Vector3(windowWidth + 0.2f, 0.1f, 0.06f), style.panelGrey);
        Decor(shell, "Window Frame Bottom", new Vector3(0f, BackWallHeight - 1.8f, halfZ - WallThickness * 0.5f - 0.08f), new Vector3(windowWidth + 0.2f, 0.1f, 0.06f), style.panelGrey);

        // Chamber number painted on the floor, front and centre.
        Decor(shell, "Floor Stripe Blue", new Vector3(-size.x * 0.25f, 0.011f, -size.y * 0.5f + 0.3f), new Vector3(size.x * 0.5f - 0.2f, 0.02f, 0.2f), style.neonBlue);
        Decor(shell, "Floor Stripe Pink", new Vector3(size.x * 0.25f, 0.011f, -size.y * 0.5f + 0.3f), new Vector3(size.x * 0.5f - 0.2f, 0.02f, 0.2f), style.neonPink);
    }

    private void BuildCells(ArenaLayout layout)
    {
        var props = Group("Props");
        float cell = ArenaLayout.CellSize;
        for (int x = 0; x < layout.Width; x++)
            for (int z = 0; z < layout.Depth; z++)
            {
                var coord = new Vector2Int(x, z);
                Vector3 position = layout.ToLocal(coord);
                position.y = 0f;
                Material accent = x < layout.HalfWidth ? style.neonBlue : style.neonPink;
                switch (layout.Cells[x, z])
                {
                    case Cell.Cover:
                        {
                            const float height = 2.2f; // tall enough to stop shots aimed at a standing player's gun
                            var block = Block(props, "Cover", position + Vector3.up * (height * 0.5f), new Vector3(cell * 0.82f, height, cell * 0.82f), style.panelWhite);
                            Decor(block.transform, "Cap", new Vector3(0f, 0.5f + 0.02f, 0f), new Vector3(1.02f, 0.04f, 1.02f), style.panelGrey, worldScale: false);
                            Decor(block.transform, "Stripe", new Vector3(0f, -0.22f, 0f), new Vector3(1.01f, 0.05f, 1.01f), accent, worldScale: false);
                            break;
                        }
                    case Cell.Pillar:
                        {
                            const float height = 3.4f;
                            var pillar = Block(props, "Pillar", position + Vector3.up * (height * 0.5f), new Vector3(cell * 0.6f, height, cell * 0.6f), style.panelGrey);
                            Decor(pillar.transform, "Glow", new Vector3(0f, 0f, -0.51f), new Vector3(0.18f, 0.8f, 0.02f), accent, worldScale: false);
                            break;
                        }
                    case Cell.Platform:
                        {
                            float h = ArenaLayout.PlatformHeight;
                            var platform = Block(props, "Platform", position + Vector3.up * (h * 0.5f), new Vector3(cell, h, cell), style.panelGrey);
                            Decor(platform.transform, "Trim", new Vector3(0f, 0.52f, 0f), new Vector3(1.002f, 0.05f, 1.002f), accent, worldScale: false);
                            break;
                        }
                    case Cell.Ramp:
                        {
                            var direction = layout.RampDirections.TryGetValue(coord, out var d) ? d : Vector2Int.up;
                            float h = ArenaLayout.PlatformHeight;
                            float angle = Mathf.Atan2(h, cell) * Mathf.Rad2Deg;
                            var ramp = Block(props, "Ramp", position + Vector3.up * (h * 0.5f - 0.1f), new Vector3(cell, 0.2f, Mathf.Sqrt(cell * cell + h * h)), style.panelGrey);
                            ramp.transform.localRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y)) * Quaternion.Euler(-angle, 0f, 0f);
                            break;
                        }
                }
            }
    }

    private void BuildMarkers(ArenaLayout layout)
    {
        var markers = Group("Markers");
        foreach (var cell in layout.CoopSpawns)
            CoopSpawns.Add(transform.TransformPoint(layout.ToLocal(cell) + Vector3.up * 0.1f));
        foreach (var cell in layout.PvpSpawns)
            PvpSpawns.Add(transform.TransformPoint(layout.ToLocal(cell) + Vector3.up * 0.1f));

        for (int i = 0; i < layout.PvpSpawns.Count; i++)
        {
            var pad = Decor(markers, $"Spawn Pad P{i + 1}", layout.ToLocal(layout.PvpSpawns[i]) + Vector3.up * 0.015f, new Vector3(1.6f, 0.02f, 1.6f), style.Tinted(style.playerRing, LabPalette.Player(i)));
            ReplaceWithQuad(pad);
        }

        foreach (var cell in layout.CrateAnchors)
        {
            Vector3 local = layout.ToLocal(cell);
            CrateAnchors.Add(transform.TransformPoint(local));
            var hatch = Decor(markers, "Drop Zone", local + Vector3.up * 0.014f, new Vector3(1.7f, 0.02f, 1.7f), style.Tinted(style.playerRing, LabPalette.NeonPink));
            ReplaceWithQuad(hatch);
        }

        foreach (var cell in layout.TurretSockets)
        {
            Vector3 local = layout.ToLocal(cell);
            TurretSockets.Add(transform.TransformPoint(local));
            Decor(markers, "Turret Hatch", local + Vector3.up * 0.012f, new Vector3(1.1f, 0.02f, 1.1f), style.floorLine);
        }
    }

    private void BuildGates(ArenaLayout layout)
    {
        foreach (var rows in layout.GateRows)
        {
            Vector3 from = layout.ToLocal(new Vector2Int(layout.HalfWidth, rows.x));
            Vector3 to = layout.ToLocal(new Vector2Int(layout.HalfWidth, rows.y));
            from.x = to.x = 0f;
            from.z -= ArenaLayout.CellSize * 0.5f;
            to.z += ArenaLayout.CellSize * 0.5f;
            var gate = HazardGate.Create(style, Root, from, to);
            Gates.Add(gate);
        }
    }

    private Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(Root, false);
        return group;
    }

    // A solid, collidable lab block with the toon material.
    private static GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = size;
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }

    // A purely visual piece: no collider, so it never blocks movement or shots.
    // With worldScale false, position and size are relative to the parent block's unit cube.
    private static GameObject Decor(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool worldScale = true)
    {
        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        DestroyCollider(piece);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = size;
        var renderer = piece.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return piece;
    }

    // Flat markers use a Quad so the Glow shader's ring/blob shapes (UV based) render correctly.
    private static void ReplaceWithQuad(GameObject cube)
    {
        var filter = cube.GetComponent<MeshFilter>();
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        filter.sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
        DestroyObject(quad);
        Vector3 scale = cube.transform.localScale;
        cube.transform.localScale = new Vector3(scale.x, scale.z, 1f);
        cube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void DestroyCollider(GameObject piece)
    {
        var collider = piece.GetComponent<Collider>();
        if (collider == null)
            return;
        if (Application.isPlaying)
            Destroy(collider);
        else
            DestroyImmediate(collider);
    }

    private static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
