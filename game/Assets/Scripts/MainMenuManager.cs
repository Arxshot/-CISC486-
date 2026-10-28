using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Game Scenes")]
    [SerializeField] private string targetSceneName = "testScene.unity";

    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel;

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
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnExitClicked()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }
}