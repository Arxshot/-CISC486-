using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The AI bots inside Apex Arsenal: teams, co-op waves in AA_Arena, and taking cover.
public class LabBotTests
{
    private const string ArenaScenePath = "Assets/ApexArsenal/Scenes/AA_Arena.unity";
    private const string BotPrefabPath = "Assets/PlayerControls/LocalBot.prefab";

    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<InputDevice> devices = new List<InputDevice>();
    private InputSettings.BackgroundBehavior savedBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorInputBehavior;

    [SetUp]
    public void SetUp()
    {
        // See LocalGameplayTests.SetUp: simulated keys must work without Game view focus.
        savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        savedEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach (var device in InputSystem.devices)
        {
            if (!device.enabled)
                InputSystem.EnableDevice(device);
            InputSystem.ResetDevice(device);
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInputBehavior;
        MatchSettings.Mode = GameMode.Coop;
        MatchSettings.Preset = ArenaPresetId.Crossfire;
        MatchSettings.Seed = 486;
        for (int i = PlayerInput.all.Count - 1; i >= 0; i--)
            Object.Destroy(PlayerInput.all[i].gameObject);
        foreach (var obj in objects)
            if (obj != null)
                Object.Destroy(obj);
        yield return null;
        foreach (var device in devices)
            if (device.added)
                InputSystem.RemoveDevice(device);
        objects.Clear();
        devices.Clear();
    }

    [Test]
    public void TeamsDecideWhoIsHostile()
    {
        var a = CreateCombatant("A");
        var b = CreateCombatant("B");
        Assert.That(a.IsHostileTo(b), Is.True, "Free-for-all by default, as in testScene.");
        Assert.That(a.IsHostileTo(a), Is.False);

        a.Team = LabBots.LabTeam;
        b.Team = LabBots.LabTeam;
        Assert.That(a.IsHostileTo(b), Is.False, "Lab bots are allies.");
        b.Team = LabBots.PlayerTeam;
        Assert.That(a.IsHostileTo(b), Is.True);
        Assert.That(b.IsHostileTo(a), Is.True);
    }

    [UnityTest]
    public IEnumerator CoopWavesSpawnLabBotsThatMoveAndHuntThePlayer()
    {
        MatchSettings.Mode = GameMode.Coop;
        MatchSettings.Preset = ArenaPresetId.Crossfire;
        MatchSettings.Seed = 486;
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            ArenaScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        var director = MatchDirector.Instance;
        Assert.That(director, Is.Not.Null);

        // Join with Enter, then press Enter again to skip the lobby wait.
        var keyboard = InputSystem.AddDevice<Keyboard>();
        devices.Add(keyboard);
        yield return Tap(keyboard, Key.Enter);
        Assert.That(director.Players.Count, Is.EqualTo(1));
        var player = director.Players[0].GetComponent<LocalCombatant>();
        Assert.That(player.Team, Is.EqualTo(LabBots.PlayerTeam));
        yield return new WaitForSeconds(0.6f);
        yield return Tap(keyboard, Key.Enter);
        float wait = 0f;
        while (director.Current != MatchDirector.Phase.Playing && wait < 5f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        Assert.That(director.Current, Is.EqualTo(MatchDirector.Phase.Playing));
        Assert.That(director.Wave, Is.EqualTo(1));

        var bots = new List<LocalBot>(director.Bots);
        Assert.That(bots.Count, Is.EqualTo(2), "Wave 1 with one player: two bots.");
        Assert.That(director.EnemiesLeft, Is.EqualTo(director.TurretsLeft + 2));
        var starts = new List<Vector3>();
        foreach (var bot in bots)
        {
            var self = bot.GetComponent<LocalCombatant>();
            Assert.That(self.Team, Is.EqualTo(LabBots.LabTeam));
            Assert.That(self.IsHostileTo(player), Is.True);
            Assert.That(Vector3.Distance(bot.transform.position, player.transform.position), Is.GreaterThan(7.9f), "No spawn camping.");
            starts.Add(bot.transform.position);
        }
        Assert.That(bots[0].GetComponent<LocalCombatant>().IsHostileTo(bots[1].GetComponent<LocalCombatant>()), Is.False);

        // The player stands still; the bots should go looking and lock on.
        bool hunted = false;
        float elapsed = 0f;
        while (!hunted && elapsed < 15f && player.isActiveAndEnabled)
        {
            foreach (var bot in bots)
                if (LabBots.Alive(bot) && bot.Target == player)
                    hunted = true;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Assert.That(hunted, Is.True, "A bot should find and target the player.");
        float moved = 0f;
        for (int i = 0; i < bots.Count; i++)
            if (LabBots.Alive(bots[i]))
                moved = Mathf.Max(moved, Vector3.Distance(starts[i], bots[i].transform.position));
        Assert.That(moved, Is.GreaterThan(1f), "Bots should move around, not stand still like turrets.");
    }

    [UnityTest]
    public IEnumerator MainMenuBackdropHasBotsPatrollingTheChamber()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/ApexArsenal/Scenes/AA_MainMenu.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        var bots = Object.FindObjectsByType<LocalBot>();
        Assert.That(bots.Length, Is.EqualTo(3));
        var starts = new Vector3[bots.Length];
        for (int i = 0; i < bots.Length; i++)
            starts[i] = bots[i].transform.position;
        yield return new WaitForSeconds(3f);
        float moved = 0f;
        for (int i = 0; i < bots.Length; i++)
        {
            Assert.That(bots[i].State, Is.EqualTo(BotState.Wander), "Nobody to fight in the menu.");
            moved = Mathf.Max(moved, Vector3.Distance(starts[i], bots[i].transform.position));
        }
        Assert.That(moved, Is.GreaterThan(1f));
    }

    [UnityTest]
    public IEnumerator HurtBotHidesBehindCoverFromItsAttacker()
    {
        // A private corner of the world: floor, one cover block, the bot and a stationary attacker.
        var origin = new Vector3(500f, 0f, 500f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(floor);
        floor.transform.position = origin + Vector3.down * 0.5f;
        floor.transform.localScale = new Vector3(40f, 1f, 40f);
        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(cover);
        cover.name = "Cover";
        cover.transform.position = origin + new Vector3(3f, 1.1f, 0f);
        cover.transform.localScale = new Vector3(1.64f, 2.2f, 1.64f); // ArenaBuilder's cover block size.

        var attacker = CreateCombatant("Attacker");
        attacker.Team = LabBots.PlayerTeam;
        attacker.transform.position = origin + new Vector3(0f, 0f, 8f);
        var body = attacker.gameObject.AddComponent<CapsuleCollider>();
        body.center = Vector3.up;
        body.height = 2f;

        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath);
#endif
        Assert.That(prefab, Is.Not.Null);
        var botObject = Object.Instantiate(prefab, origin, Quaternion.LookRotation(Vector3.forward));
        objects.Add(botObject);
        botObject.GetComponent<LocalCombatant>().Team = LabBots.LabTeam;
        var bot = botObject.GetComponent<LocalBot>();
        var health = botObject.GetComponent<LocalHealth>();

        float elapsed = 0f;
        while (bot.Target != attacker && elapsed < 2f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Assert.That(bot.Target, Is.SameAs(attacker), "The bot should spot the attacker in front of it.");

        health.TakeDamage(health.MaxHealth * 0.8f, attacker.gameObject); // Below the retreat threshold.
        elapsed = 0f;
        while (!bot.IsInCover && elapsed < 6f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Assert.That(bot.IsInCover, Is.True, $"The bot should reach cover; it is in {bot.State}.");
        Vector3 eye = bot.transform.position + Vector3.up * LocalCombatant.AimHeight;
        Vector3 threat = attacker.AimPoint;
        Assert.That(Physics.Linecast(threat, eye, out RaycastHit hit), Is.True);
        Assert.That(hit.collider.gameObject, Is.SameAs(cover), "The cover block should be between the bot and the attacker.");
    }

    private LocalCombatant CreateCombatant(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        obj.AddComponent<LocalHealth>();
        return obj.AddComponent<LocalCombatant>();
    }

    private static IEnumerator Tap(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }
}
