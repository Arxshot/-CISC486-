using UnityEngine;

// The only object in AA_MainMenu: a live, slowly drifting test chamber behind the code-built menu.
// Highlighting a chamber in the menu regenerates the backdrop so you can preview it.
public class MenuBootstrap : MonoBehaviour
{
    [SerializeField] private LabStyle style;

    private Camera menuCamera;
    private ArenaBuilder builder;
    private float yaw;

    private void Awake()
    {
        if (style == null)
        {
            Debug.LogError("MenuBootstrap has no LabStyle. Run Apex Arsenal > Build Scenes & Assets.", this);
            return;
        }
        Time.timeScale = 1f;
        LabScene.UseStyle(style);
        LabScene.SetupLighting();
        menuCamera = LabScene.CreateCamera();
        menuCamera.fieldOfView = 40f;

        builder = new GameObject("Backdrop Chamber").AddComponent<ArenaBuilder>();
        builder.Initialize(style);
        Preview(ArenaPresetId.Crossfire, 0);

        AudioDirector.Instance.PlayMusic();
        AudioDirector.Instance.SetMusicPitch(1f);
        MainMenuScreen.Create(Preview);
    }

    private void Preview(ArenaPresetId preset, int seed)
    {
        if (builder.Layout != null && builder.Layout.Preset.Id == preset && (preset != ArenaPresetId.Random || builder.Layout.Seed == seed))
            return;
        builder.Build(ArenaLayout.Generate(preset, seed));
        // A few idle sentries for life; with no players around they just look about.
        int count = Mathf.Min(3, builder.TurretSockets.Count);
        for (int i = 0; i < count; i++)
            SentryTurret.Create(style, builder.Root, builder.TurretSockets[i * 2 % builder.TurretSockets.Count], 0);
        // And a patrol of lab bots wandering the chamber (same team, so they leave each other alone).
        // They live under the generated chamber, so previewing another one clears them.
        if (style.botPrefab != null)
            for (int i = 0; i < Mathf.Min(3, builder.PvpSpawns.Count); i++)
                LabBots.Spawn(style, builder.Root, builder.PvpSpawns[i], builder.transform.position, 0);
    }

    private void LateUpdate()
    {
        if (menuCamera == null || builder == null || builder.Layout == null)
            return;
        yaw += Time.unscaledDeltaTime * 4f;
        Vector2 size = builder.Layout.SizeMeters;
        float distance = Mathf.Max(size.x, size.y) * 1.25f;
        var rotation = Quaternion.Euler(38f, -25f + Mathf.Sin(yaw * Mathf.Deg2Rad * 3f) * 18f, 0f);
        Vector3 focus = builder.transform.position;
        // Shift the chamber to the right of the screen, away from the menu on the left.
        Vector3 position = focus - rotation * Vector3.forward * distance - rotation * Vector3.right * (size.x * 0.32f);
        menuCamera.transform.SetPositionAndRotation(position, rotation);
    }
}
