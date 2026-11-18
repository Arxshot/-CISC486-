using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pause menu. Esc pauses from any keyboard; gamepad Start pauses only from a gamepad that already
// belongs to a joined player, so a new controller's Start still means "join".
public class PauseOverlay : MonoBehaviour
{
    private static PauseOverlay instance;

    private ArenaSession session;
    private InputAction pauseAction;
    private InputAction backAction;
    private RectTransform menu;
    private SettingsView settings;
    private Selectable first;
    private bool joiningWasOpen;

    public static bool IsPaused { get; private set; }

    public static PauseOverlay Create(ArenaSession arenaSession)
    {
        var canvas = UIFactory.CreateCanvas("Pause", 30);
        instance = canvas.gameObject.AddComponent<PauseOverlay>();
        instance.session = arenaSession;
        instance.Build(canvas.transform);
        return instance;
    }

    private void Build(Transform root)
    {
        menu = UIFactory.Rect("Menu", root);
        UIFactory.Stretch(menu);
        UIFactory.Dim(menu, 0.6f);
        var card = UIFactory.Panel("Card", menu, new Vector2(640f, 700f), LabPalette.PanelWhite);
        UIFactory.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 700f));
        UIFactory.Motion(card, UIMotion.Kind.PopIn);
        var header = UIFactory.Image("Header", card, LabPalette.NeonPink, RoundedSprite.Get(20));
        UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(400f, 96f));
        var title = UIFactory.Label("Title", header.transform, "PAUSED", 60f, Color.white);
        UIFactory.Stretch(title.rectTransform);
        var note = UIFactory.Label("Note", card, "THE TEST WILL RESUME SHORTLY", 24f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(560f, 40f));

        var column = UIFactory.Rect("Buttons", card);
        UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(520f, 520f));
        UIFactory.Column(column, 20f);
        first = UIFactory.Button("Resume", column, "RESUME", LabPalette.NeonBlue, Resume, new Vector2(500f, 96f), 42f);
        UIFactory.Button("Restart", column, "RESTART", LabPalette.Lime, SceneFlow.Restart, new Vector2(500f, 96f), 42f);
        UIFactory.Button("Settings", column, "SETTINGS", LabPalette.Violet, OpenSettings, new Vector2(500f, 96f), 42f);
        UIFactory.Button("Menu", column, "MAIN MENU", LabPalette.NeonPink, SceneFlow.LoadMainMenu, new Vector2(500f, 96f), 42f);

        settings = SettingsView.Create(root, () =>
        {
            menu.gameObject.SetActive(true);
            UIFactory.Select(first);
        });
        UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 720f));
        settings.gameObject.SetActive(false);
        menu.gameObject.SetActive(false);

        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.performed += OnPausePressed;
        pauseAction.Enable();

        backAction = new InputAction("Back", InputActionType.Button);
        backAction.AddBinding("<Gamepad>/buttonEast");
        backAction.performed += _ =>
        {
            if (!IsPaused)
                return;
            if (settings.gameObject.activeSelf)
                settings.Back();
            else
                Resume();
        };
        backAction.Enable();
    }

    private void OnPausePressed(InputAction.CallbackContext context)
    {
        var director = MatchDirector.Instance;
        if (director == null || director.Current == MatchDirector.Phase.Lobby || director.Current == MatchDirector.Phase.Results)
            return;
        if (context.control.device is Gamepad && !session.OwnsDevice(context.control.device))
            return;
        if (IsPaused)
        {
            if (settings.gameObject.activeSelf)
                settings.Back();
            else
                Resume();
        }
        else
            Pause();
    }

    private void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        joiningWasOpen = session.JoiningOpen;
        session.SetJoining(false);
        foreach (var player in session.Players)
            if (player != null && player.isActiveAndEnabled)
                player.DeactivateInput();
        AudioDirector.Play(Sfx.UiBack, 0.8f);
        AudioDirector.Instance.SetMusicPitch(0.8f);
        menu.gameObject.SetActive(true);
        UIFactory.Select(first);
    }

    private void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        settings.gameObject.SetActive(false);
        menu.gameObject.SetActive(false);
        foreach (var player in session.Players)
            if (player != null && player.isActiveAndEnabled)
                player.ActivateInput();
        if (joiningWasOpen)
            session.SetJoining(true);
        AudioDirector.Play(Sfx.UiClick, 0.8f);
        AudioDirector.Instance.SetMusicPitch(1f);
    }

    private void OpenSettings()
    {
        menu.gameObject.SetActive(false);
        settings.Open();
    }

    private void OnDestroy()
    {
        pauseAction?.Dispose();
        backAction?.Dispose();
        if (instance == this)
        {
            instance = null;
            IsPaused = false;
        }
        Time.timeScale = 1f;
    }
}
