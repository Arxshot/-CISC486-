using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Game Scenes")]
    [SerializeField] private string targetSceneName = "testScene";

    [Header("UI Panels")]
    [Tooltip("Optional. Leave empty to use the shared Apex Arsenal settings card (volume + fullscreen).")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Style for the built-in settings card (font, colours).")]
    [SerializeField] private LabStyle style;

    private SettingsView settingsView;
    private InputAction back;

    private void Awake()
    {
        // Applies the saved volume as soon as the menu opens, not only after visiting settings.
        _ = AudioDirector.Instance;

        if (settingsPanel == null)
            BuildSettingsPanel();

        back = new InputAction("Back", InputActionType.Button);
        back.AddBinding("<Keyboard>/escape");
        back.AddBinding("<Gamepad>/buttonEast");
        back.performed += _ => CloseSettings();
        back.Enable();
    }

    private void OnDestroy() => back?.Dispose();

    // Same card as the Apex Arsenal main menu and pause screen, so they all share one set of
    // saved settings (VolumeSettings / PlayerPrefs).
    private void BuildSettingsPanel()
    {
        if (style != null && LabStyle.Active == null)
            LabScene.UseStyle(style);
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("Settings Overlay", 100);
        canvas.transform.SetParent(transform, false);
        UIFactory.Dim(canvas.transform, 0.6f); // Blocks clicks to the menu behind.
        settingsView = SettingsView.Create(canvas.transform, () => settingsPanel.SetActive(false));
        UIFactory.Place((RectTransform)settingsView.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 720f));
        settingsPanel = canvas.gameObject;
        settingsPanel.SetActive(false);
    }

    public void OnPvPClicked()
    {
        Time.timeScale = 1f; // Ensures time is unpaused
        SceneManager.LoadScene(targetSceneName);
    }

    public void OnCoopClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(targetSceneName);
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel == null)
            return;
        if (settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }
        settingsPanel.SetActive(true);
        if (settingsView != null)
            settingsView.Open();
    }

    private void CloseSettings()
    {
        if (settingsPanel == null || !settingsPanel.activeSelf)
            return;
        if (settingsView != null)
            settingsView.Back(); // Plays the back sound, hides the card, then the overlay.
        else
            settingsPanel.SetActive(false);
    }

    public void OnExitClicked()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }
}
