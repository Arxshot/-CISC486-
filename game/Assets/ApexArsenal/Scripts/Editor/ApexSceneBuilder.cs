using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Apex Arsenal > Build Scenes & Assets. Generates everything the code-built game needs through
// Unity's API (no hand-edited scene or prefab files): materials, the LabStyle asset, prefab variants
// of Pierre's LocalPlayer (his prefab is never modified), the two scenes and the build list.
// Safe to run again at any time; it updates what's there.
public static class ApexSceneBuilder
{
    private const string Root = "Assets/ApexArsenal";
    private const string Generated = Root + "/Generated";
    private const string MaterialsFolder = Generated + "/Materials";
    private const string PrefabsFolder = Generated + "/Prefabs";
    private const string ScenesFolder = Root + "/Scenes";
    private const string StylePath = Generated + "/LabStyle.asset";
    private const string MenuArtFolder = "Assets/UI_Graphics";
    private const string BasePlayerPath = "Assets/PlayerControls/LocalPlayer.prefab";
    private const string BotPrefabPath = "Assets/PlayerControls/LocalBot.prefab";
    private const string FontPath = "Assets/Core/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    public static string MenuScenePath => $"{ScenesFolder}/{SceneFlow.MainMenuScene}.unity";
    public static string ArenaScenePath => $"{ScenesFolder}/{SceneFlow.ArenaScene}.unity";

    [MenuItem("Apex Arsenal/Build Scenes & Assets", priority = 0)]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        BuildAll();
        EditorSceneManager.OpenScene(MenuScenePath);
        EditorUtility.DisplayDialog("Apex Arsenal", "Scenes and assets are ready.\n\nOpen AA_MainMenu and press Play, or use Apex Arsenal > Play From Main Menu.", "Nice");
    }

    [MenuItem("Apex Arsenal/Play From Main Menu", priority = 1)]
    public static void PlayFromMenu()
    {
        if (!File.Exists(MenuScenePath))
            BuildFromMenu();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(MenuScenePath);
        EditorApplication.EnterPlaymode();
    }

    // Also the entry point for batch mode: -executeMethod ApexSceneBuilder.BuildAll
    public static void BuildAll()
    {
        EnsureFolder(Generated);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);
        EnsureFolder(ScenesFolder);

        var style = AssetDatabase.LoadAssetAtPath<LabStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<LabStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }

        BuildMaterials(style);
        style.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (style.font == null)
            style.font = TMP_Settings.defaultFontAsset;
        LoadMenuArt(style);
        style.coopPlayerPrefab = BuildPlayerVariant("ApexPlayer_Coop", friendlyFire: false);
        style.pvpPlayerPrefab = BuildPlayerVariant("ApexPlayer_PvP", friendlyFire: true);
        style.botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();

        BuildScene(MenuScenePath, "Menu Bootstrap", typeof(MenuBootstrap), style);
        BuildScene(ArenaScenePath, "Arena Bootstrap", typeof(ArenaBootstrap), style);
        UpdateBuildList();
        AssetDatabase.SaveAssets();
        Debug.Log("Apex Arsenal: scenes and assets built.");
    }

    // The original main-menu art (logo + neon buttons). Read-only: the files in UI_Graphics are untouched.
    private static void LoadMenuArt(LabStyle style)
    {
        style.logo = LoadSprite("GameTitle");
        style.coopButton = LoadButtonArt("btn_local_coop");
        style.pvpButton = LoadButtonArt("btn_pvp_mode");
        style.settingsButton = LoadButtonArt("btn_settings");
        style.exitButton = LoadButtonArt("btn_exit");
    }

    private static LabStyle.ButtonArt LoadButtonArt(string baseName) => new()
    {
        idle = LoadSprite(baseName),
        hover = LoadSprite(baseName + "_hover"),
        pressed = LoadSprite(baseName + "_pressed"),
    };

    // The PNGs are imported as "Multiple" sprite sheets with one sprite each, so the sprite is a sub-asset.
    private static Sprite LoadSprite(string fileName)
    {
        string path = $"{MenuArtFolder}/{fileName}.png";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite)
                return sprite;
        Debug.LogWarning($"Apex Arsenal: no sprite at {path}; that menu element falls back to the drawn style.");
        return null;
    }

    private static void BuildMaterials(LabStyle style)
    {
        style.panelWhite = Toon("Panel White", LabPalette.PanelWhite, new Color(0.7f, 0.76f, 0.9f), 0.035f);
        style.panelGrey = Toon("Panel Grey", LabPalette.PanelGrey, new Color(0.62f, 0.66f, 0.8f), 0.035f);
        style.floor = Toon("Floor", LabPalette.Floor, new Color(0.7f, 0.72f, 0.82f), 0f, grid: true);
        style.floorLine = Toon("Floor Line", LabPalette.FloorLine, Color.white, 0f);
        style.ink = Toon("Ink", LabPalette.Ink, Color.white, 0f);

        style.neonBlue = Glow("Neon Blue", LabPalette.NeonBlue, 2.2f);
        style.neonPink = Glow("Neon Pink", LabPalette.NeonPink, 2.2f);
        style.laser = Glow("Laser", LabPalette.NeonPink, 3.2f, pulseSpeed: 22f);
        style.glass = Glow("Glass", LabPalette.WithAlpha(LabPalette.NeonBlue, 0.22f), 1.2f, transparent: true, edgeGlow: 0.5f);
        style.playerRing = Glow("Player Ring", Color.white, 2f, transparent: true, shape: 2f);
        style.shadowBlob = Glow("Blob Shadow", LabPalette.WithAlpha(LabPalette.Ink, 0.55f), 1f, transparent: true, shape: 1f);

        var outline = LoadMaterial("Character Outline", "ApexArsenal/Outline", "Universal Render Pipeline/Unlit");
        outline.SetColor("_OutlineColor", LabPalette.Ink);
        if (outline.HasProperty("_OutlineWidth"))
            outline.SetFloat("_OutlineWidth", 0.018f);
        style.outline = outline;
    }

    private static Material Toon(string name, Color color, Color shade, float outline, bool grid = false)
    {
        var material = LoadMaterial(name, "ApexArsenal/Toon", "Universal Render Pipeline/Lit");
        LabStyle.SetColor(material, color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        SetIfPresent(material, "_ShadeColor", shade);
        SetIfPresent(material, "_OutlineWidth", outline);
        SetIfPresent(material, "_OutlineColor", LabPalette.Ink);
        SetIfPresent(material, "_GridStrength", grid ? 1f : 0f);
        SetIfPresent(material, "_GridColor", LabPalette.FloorLine);
        SetIfPresent(material, "_GridScale", ArenaLayout.CellSize);
        SetIfPresent(material, "_Smoothness", 0.2f);
        return material;
    }

    private static Material Glow(string name, Color color, float intensity, bool transparent = false, float shape = 0f, float pulseSpeed = 0f, float edgeGlow = 0f)
    {
        var material = LoadMaterial(name, "ApexArsenal/Glow", "Universal Render Pipeline/Unlit");
        material.SetColor("_BaseColor", color);
        SetIfPresent(material, "_Intensity", intensity);
        SetIfPresent(material, "_Shape", shape);
        SetIfPresent(material, "_PulseSpeed", pulseSpeed);
        SetIfPresent(material, "_EdgeGlow", edgeGlow);
        if (transparent)
        {
            SetIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetIfPresent(material, "_ZWrite", 0f);
            SetIfPresent(material, "_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            SetIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            SetIfPresent(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
        }
        return material;
    }

    // Loads or creates a material asset. Falls back to a stock URP shader if ours failed to compile.
    private static Material LoadMaterial(string name, string shaderName, string fallbackShader)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            Debug.LogWarning($"Apex Arsenal: shader '{shaderName}' unavailable, using '{fallbackShader}' for {name}.");
            shader = Shader.Find(fallbackShader);
        }
        string path = $"{MaterialsFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
            material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void SetIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
            material.SetColor(property, value);
    }

    // A prefab variant of Pierre's LocalPlayer with my add-ons. The base prefab is not modified.
    private static GameObject BuildPlayerVariant(string name, bool friendlyFire)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayerPath);
        if (basePrefab == null)
        {
            Debug.LogError($"Apex Arsenal: {BasePlayerPath} not found, can't build {name}.");
            return null;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try
        {
            instance.name = name;
            AddIfMissing<LocalHealth>(instance);
            AddIfMissing<PlayerDash>(instance);
            AddIfMissing<PowerUpReceiver>(instance);
            AddIfMissing<PlayerCosmetics>(instance);

            var shooting = instance.GetComponent<LocalPlayerShooting>();
            if (shooting != null)
            {
                var serialized = new SerializedObject(shooting);
                var property = serialized.FindProperty("friendlyFire");
                if (property != null)
                {
                    property.boolValue = friendlyFire;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (friendlyFire)
                    Debug.LogWarning("Apex Arsenal: LocalPlayerShooting has no 'friendlyFire' field; PvP players may not damage each other.");
            }
            return PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsFolder}/{name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void AddIfMissing<T>(GameObject target) where T : Component
    {
        if (target.GetComponent<T>() == null)
            target.AddComponent<T>();
    }

    private static void BuildScene(string path, string objectName, System.Type bootstrap, LabStyle style)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // NewScene(Single) unloads unused assets, which can leave the caller's LabStyle reference dead
        // (Unity "fake null"). Reload it by path so the scene saves a real reference.
        if (style == null)
            style = AssetDatabase.LoadAssetAtPath<LabStyle>(StylePath);
        if (style == null)
            throw new System.InvalidOperationException($"Could not load {StylePath}; scene {path} not built.");

        var go = new GameObject(objectName, bootstrap);
        var serialized = new SerializedObject(go.GetComponent(bootstrap));
        var property = serialized.FindProperty("style");
        if (property == null)
            throw new System.InvalidOperationException($"{bootstrap.Name} has no serialized 'style' field.");
        property.objectReferenceValue = style;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (property.objectReferenceValue == null)
            throw new System.InvalidOperationException($"LabStyle reference did not stick on {bootstrap.Name} in {path}.");
        EditorSceneManager.SaveScene(scene, path);
    }

    // New scenes first (main menu = index 0); keep the team's existing scenes in the list, unticked.
    private static void UpdateBuildList()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(ArenaScenePath, true),
        };
        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (existing.path == MenuScenePath || existing.path == ArenaScenePath || string.IsNullOrEmpty(existing.path) || !File.Exists(existing.path))
                continue;
            scenes.Add(new EditorBuildSettingsScene(existing.path, false));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
