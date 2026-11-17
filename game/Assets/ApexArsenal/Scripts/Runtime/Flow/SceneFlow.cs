using UnityEngine;
using UnityEngine.SceneManagement;

// Every scene change goes through here so a paused or finished match never leaks Time.timeScale = 0.
public static class SceneFlow
{
    public const string MainMenuScene = "AA_MainMenu";
    public const string ArenaScene = "AA_Arena";

    public static void StartMatch(GameMode mode, ArenaPresetId preset, int seed)
    {
        MatchSettings.Mode = mode;
        MatchSettings.Preset = preset;
        MatchSettings.Seed = seed;
        Load(ArenaScene);
    }

    public static void Restart() => Load(SceneManager.GetActiveScene().name);

    public static void LoadMainMenu() => Load(MainMenuScene);

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not in File > Build Profiles > Scene List. Run Apex Arsenal > Build Scenes & Assets.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
