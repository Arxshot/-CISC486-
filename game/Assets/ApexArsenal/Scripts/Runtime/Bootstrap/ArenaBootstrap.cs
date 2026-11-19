using System.Collections.Generic;
using UnityEngine;

// The only object in AA_Arena. Builds the whole match from code, in dependency order:
// style -> lighting -> camera (PlayerMovement needs Camera.main) -> generated chamber -> events
// -> player session -> game loop -> HUD and pause.
public class ArenaBootstrap : MonoBehaviour
{
    [SerializeField] private LabStyle style;

    private void Awake()
    {
        if (style == null)
        {
            Debug.LogError("ArenaBootstrap has no LabStyle. Run Apex Arsenal > Build Scenes & Assets.", this);
            return;
        }
        Time.timeScale = 1f;
        LabScene.UseStyle(style);
        LabScene.SetupLighting();
        var camera = LabScene.CreateCamera();
        var arenaCamera = camera.gameObject.AddComponent<ArenaCamera>();

        var builder = new GameObject("Arena").AddComponent<ArenaBuilder>();
        builder.Initialize(style);
        builder.Build(ArenaLayout.Generate(MatchSettings.Preset, MatchSettings.Seed));
        arenaCamera.Frame(builder.WorldBounds);

        var crates = builder.gameObject.AddComponent<CrateSpawner>();
        crates.Initialize(builder.CrateAnchors);

        bool pvp = MatchSettings.Mode == GameMode.PvP;
        var prefab = pvp ? style.pvpPlayerPrefab : style.coopPlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("LabStyle is missing its player prefabs. Run Apex Arsenal > Build Scenes & Assets.", this);
            return;
        }
        var session = new GameObject("Session").AddComponent<ArenaSession>();
        session.Initialize(prefab, pvp ? builder.PvpSpawns : builder.CoopSpawns);
        session.Joined += _ =>
        {
            var targets = new List<Transform>();
            foreach (var player in session.Players)
                if (player != null)
                    targets.Add(player.transform);
            arenaCamera.SetTargets(targets);
        };

        var director = new GameObject("Match Director").AddComponent<MatchDirector>();
        director.Initialize(session, builder, crates);
        MatchHud.Create();
        gameObject.AddComponent<AmmoHud>();
        PauseOverlay.Create(session);
    }
}
