using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The main game loop: Lobby (join) -> Countdown -> Playing -> Results.
// Co-op: 1-4 players clear three waves of sentry turrets; everyone down is a defeat.
// PvP: 2-4 player free-for-all; last one standing wins.
public class MatchDirector : MonoBehaviour
{
    public enum Phase
    {
        Lobby,
        Countdown,
        Playing,
        Results,
    }

    [SerializeField] private float lobbyAutoStart = 6f;
    [SerializeField] private int waves = 3;
    [SerializeField] private float waveBreak = 3f;

    private ArenaSession session;
    private ArenaBuilder arena;
    private CrateSpawner crates;
    private readonly List<SentryTurret> turrets = new();
    private float timer;
    private float lobbyTimer;
    private int countdownStep;
    private float waveTimer;
    private bool waveQueued;
    private System.Random socketRandom;

    public Phase Current { get; private set; } = Phase.Lobby;
    public int Wave { get; private set; }
    public int TotalWaves => waves;
    public float MatchTime { get; private set; }
    public float LobbyCountdown => Mathf.Max(0f, lobbyAutoStart - lobbyTimer);
    public int TurretsLeft
    {
        get
        {
            int alive = 0;
            foreach (var turret in turrets)
                if (turret != null && turret.Alive)
                    alive++;
            return alive;
        }
    }
    public IReadOnlyList<PlayerInput> Players => session.Players;

    public static MatchDirector Instance { get; private set; }

    public void Initialize(ArenaSession arenaSession, ArenaBuilder builder, CrateSpawner crateSpawner)
    {
        Instance = this;
        session = arenaSession;
        arena = builder;
        crates = crateSpawner;
        socketRandom = new System.Random(builder.Layout.Seed);
        session.Joined += _ => lobbyTimer = 0f;
        AudioDirector.Instance.PlayMusic(0.92f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        switch (Current)
        {
            case Phase.Lobby:
                UpdateLobby();
                break;
            case Phase.Countdown:
                UpdateCountdown();
                break;
            case Phase.Playing:
                UpdatePlaying();
                break;
        }
    }

    // Back out of the lobby to the main menu, e.g. PvP when nobody else can join.
    // Only from the lobby: once the match starts, Esc / Start belong to the pause menu.
    public void LeaveLobby()
    {
        if (Current != Phase.Lobby)
            return;
        session.SetJoining(false);
        AudioDirector.Play(Sfx.UiBack, 0.8f);
        SceneFlow.LoadMainMenu();
    }

    // Esc on the keyboard or Select (View / Share) on any gamepad. Not B: joined players dash with it.
    private static bool LeavePressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;
        foreach (var gamepad in Gamepad.all)
            if (gamepad.selectButton.wasPressedThisFrame)
                return true;
        return false;
    }

    private void UpdateLobby()
    {
        if (LeavePressed())
        {
            LeaveLobby();
            return;
        }
        int joined = session.Players.Count;
        if (joined < MatchSettings.MinPlayers)
            return;
        lobbyTimer += Time.deltaTime;
        // A joined player can skip the wait by pressing their join button (Enter / Start) again.
        if (lobbyTimer >= lobbyAutoStart || joined == MatchSettings.MaxPlayers || (lobbyTimer > 0.4f && StartPressed()))
            BeginCountdown();
    }

    private bool StartPressed()
    {
        foreach (var player in session.Players)
            foreach (var device in player.devices)
            {
                if (device is Gamepad gamepad && gamepad.startButton.wasPressedThisFrame)
                    return true;
                if (device is Keyboard keyboard && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                    return true;
            }
        return false;
    }

    private void BeginCountdown()
    {
        Current = Phase.Countdown;
        session.SetJoining(false);
        timer = 0f;
        countdownStep = 3;
        MatchHud.Announce("3", LabPalette.NeonBlue, 0.8f);
        AudioDirector.Play(Sfx.CountBeep);
    }

    private void UpdateCountdown()
    {
        timer += Time.deltaTime;
        if (timer < 0.8f)
            return;
        timer = 0f;
        countdownStep--;
        if (countdownStep > 0)
        {
            MatchHud.Announce(countdownStep.ToString(), countdownStep == 2 ? LabPalette.NeonPink : LabPalette.Lime, 0.8f);
            AudioDirector.Play(Sfx.CountBeep, 1f, countdownStep == 1 ? 1.25f : 1.1f);
            return;
        }
        Current = Phase.Playing;
        MatchHud.Announce(MatchSettings.Mode == GameMode.PvP ? "FIGHT!" : "TEST BEGINS!", LabPalette.NeonPink, 1.1f);
        AudioDirector.Play(Sfx.CountGo);
        AudioDirector.Instance.SetMusicPitch(1f);
        foreach (var gate in arena.Gates)
            gate.Running = true;
        crates.Running = true;
        if (MatchSettings.EnemiesEnabled)
            StartWave(1);
    }

    private void UpdatePlaying()
    {
        MatchTime += Time.deltaTime;
        if (waveQueued)
        {
            waveTimer -= Time.deltaTime;
            if (waveTimer <= 0f)
            {
                waveQueued = false;
                StartWave(Wave + 1);
            }
        }

        int enemiesLeft = MatchSettings.EnemiesEnabled ? TurretsLeft : 0;
        if (MatchSettings.EnemiesEnabled && enemiesLeft == 0 && !waveQueued && Wave < waves)
        {
            waveQueued = true;
            waveTimer = waveBreak;
            MatchHud.Announce($"WAVE {Wave} CLEAR!", LabPalette.Lime, 1.4f);
            AudioDirector.Play(Sfx.Pickup);
        }

        var alive = new bool[session.Players.Count];
        for (int i = 0; i < alive.Length; i++)
            alive[i] = session.Players[i] != null && session.Players[i].isActiveAndEnabled;
        int wavesLeft = MatchSettings.EnemiesEnabled ? waves - Wave + (waveQueued ? 1 : 0) : 0;
        var outcome = MatchRules.Evaluate(MatchSettings.Mode, alive, enemiesLeft, wavesLeft, out int winner);
        if (outcome != MatchOutcome.Ongoing)
            End(outcome, winner);
    }

    private void StartWave(int wave)
    {
        Wave = wave;
        int playerCount = Mathf.Max(1, session.Players.Count);
        int count = Mathf.Min(arena.TurretSockets.Count, 2 + wave + playerCount);
        var sockets = new List<Vector3>(arena.TurretSockets);
        for (int i = sockets.Count - 1; i > 0; i--)
        {
            int j = socketRandom.Next(i + 1);
            (sockets[i], sockets[j]) = (sockets[j], sockets[i]);
        }
        turrets.RemoveAll(t => t == null || !t.Alive);
        for (int i = 0; i < count; i++)
            turrets.Add(SentryTurret.Create(LabStyle.Active, arena.Root, sockets[i], wave));
        MatchHud.Announce(wave == waves ? "FINAL WAVE!" : $"WAVE {wave}", LabPalette.NeonPink, 1.3f);
    }

    private void End(MatchOutcome outcome, int winner)
    {
        Current = Phase.Results;
        foreach (var gate in arena.Gates)
            gate.Running = false;
        crates.Running = false;
        bool good = outcome == MatchOutcome.Victory || outcome == MatchOutcome.Winner;
        AudioDirector.Play(good ? Sfx.Victory : Sfx.Defeat);
        AudioDirector.Instance.SetMusicPitch(good ? 1.05f : 0.85f);
        // Freeze the remaining players; the results screen takes over input via the UI.
        foreach (var player in session.Players)
            if (player != null && player.isActiveAndEnabled)
                player.DeactivateInput();
        ResultsScreen.Show(outcome, winner, MatchTime);
    }
}
