using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class MainMenuSceneTests
{
    private const string MenuScenePath = "Assets/Scenes/MainMenuScene.unity";
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior savedBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorInputBehavior;

    // See LocalGameplayTests.SetUp: let simulated keys through even when the Game view is unfocused.
    [SetUp]
    public void SetUp()
    {
        savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        savedEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInputBehavior;
        if (keyboard != null && keyboard.added)
            InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        yield return null;
    }

    private static IEnumerator LoadMenu()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            MenuScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync(MenuScenePath);
#endif
        yield return null;
    }

    private static void Click(string buttonName)
    {
        var button = GameObject.Find(buttonName).GetComponent<Button>();
        button.onClick.Invoke(); // Runs the listeners wired up in the scene, like a real click.
    }

    private static SettingsView FindSettings() =>
        Object.FindAnyObjectByType<SettingsView>(FindObjectsInactive.Include);

    [UnityTest]
    public IEnumerator SettingsButtonOpensSettingsAndBackClosesIt()
    {
        yield return LoadMenu();
        var settings = FindSettings();
        Assert.That(settings, Is.Not.Null, "The menu should build a settings card.");
        Assert.That(settings.gameObject.activeInHierarchy, Is.False, "Settings should start hidden.");

        Click("SettingsButton");
        yield return null;
        Assert.That(settings.gameObject.activeInHierarchy, Is.True);
        Assert.That(settings.GetComponentsInChildren<Slider>().Length, Is.EqualTo(3));

        Click("Back");
        yield return null;
        Assert.That(settings.gameObject.activeInHierarchy, Is.False);

        Click("SettingsButton");
        yield return null;
        Assert.That(settings.gameObject.activeInHierarchy, Is.True, "Settings should reopen after Back.");
    }

    [UnityTest]
    public IEnumerator EscapeClosesSettings()
    {
        yield return LoadMenu();
        keyboard = InputSystem.AddDevice<Keyboard>();
        Click("SettingsButton");
        yield return null;
        var settings = FindSettings();
        Assert.That(settings.gameObject.activeInHierarchy, Is.True);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Assert.That(settings.gameObject.activeInHierarchy, Is.False);
    }

    [UnityTest]
    public IEnumerator CoopButtonLoadsTheGameScene()
    {
        yield return LoadMenu();
        Click("CoopButton");
        yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("testScene"));
    }
}
