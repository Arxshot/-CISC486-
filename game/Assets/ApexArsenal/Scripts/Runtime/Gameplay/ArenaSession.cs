using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Local multiplayer joining for the Apex Arsenal arena, using Pierre's player through my prefab
// variants. Players press Enter (keyboard & mouse) or Start (gamepad) to join, up to four.
// Joining is only open in the lobby; MatchDirector closes it when the countdown starts.
public class ArenaSession : MonoBehaviour
{
    private PlayerInputManager manager;
    private InputAction joinAction;
    private readonly List<Vector3> spawns = new();

    public readonly List<PlayerInput> Players = new();
    public event Action<PlayerInput> Joined;

    public bool JoiningOpen { get; private set; }

    public void Initialize(GameObject playerPrefab, IEnumerable<Vector3> spawnPoints)
    {
        spawns.Clear();
        spawns.AddRange(spawnPoints);

        // Configure before enabling so joining never runs against a half-built manager.
        var managerObject = new GameObject("Player Input Manager");
        managerObject.SetActive(false);
        managerObject.transform.SetParent(transform, false);
        manager = managerObject.AddComponent<PlayerInputManager>();
        manager.playerPrefab = playerPrefab;
        manager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
        joinAction = new InputAction("Join", InputActionType.Button);
        joinAction.AddBinding("<Keyboard>/enter");
        joinAction.AddBinding("<Gamepad>/start");
        manager.joinAction = new InputActionProperty(joinAction);
        manager.onPlayerJoined += HandleJoined;
        managerObject.SetActive(true);
        SetJoining(true);
    }

    public void SetJoining(bool open)
    {
        JoiningOpen = open && Players.Count < MatchSettings.MaxPlayers;
        if (manager == null)
            return;
        if (JoiningOpen)
            manager.EnableJoining();
        else
            manager.DisableJoining();
    }

    // True if this device already belongs to a joined player (used so Start pauses instead of joining).
    public bool OwnsDevice(InputDevice device)
    {
        foreach (var player in Players)
        {
            if (player == null)
                continue;
            foreach (var owned in player.devices)
                if (owned == device)
                    return true;
        }
        return false;
    }

    private void HandleJoined(PlayerInput player)
    {
        Players.Add(player);
        int index = player.playerIndex;
        player.name = $"Player {index + 1}";
        player.neverAutoSwitchControlSchemes = true;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
        Vector3 spawn = spawns.Count > 0 ? spawns[index % spawns.Count] : transform.position;
        player.transform.position = spawn;
        player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        if (controller != null)
            controller.enabled = true;

        var visuals = player.GetComponent<LocalPlayerVisuals>();
        if (visuals != null)
            visuals.SetPlayerIndex(index);
        AudioDirector.Instance.RegisterAll(player.gameObject);
        AudioDirector.Play(Sfx.Join);
        Poof.Spawn(spawn + Vector3.up, LabPalette.Player(index), 10, 4f);

        if (Players.Count >= MatchSettings.MaxPlayers)
            SetJoining(false);
        Joined?.Invoke(player);
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.DisableJoining();
            manager.onPlayerJoined -= HandleJoined;
        }
        joinAction?.Dispose();
    }
}
