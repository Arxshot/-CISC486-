using UnityEngine;

// The lab's mobile enemies: Ethan's LocalBot (wander, pursue, strafe, hide behind cover and peek),
// dressed in Apex Arsenal style and put on the lab's team so they hunt players, not each other.
public static class LabBots
{
    // Co-op teams. PvP stays free-for-all (LocalCombatant.FreeForAll).
    public const int PlayerTeam = 1;
    public const int LabTeam = 2;

    public static readonly Color BotColor = LabPalette.Danger;

    // wave 0 is the menu backdrop: harmless wanderers with nobody to hunt.
    public static LocalBot Spawn(LabStyle style, Transform parent, Vector3 position, Vector3 lookAt, int wave)
    {
        if (style == null || style.botPrefab == null)
        {
            Debug.LogError("LabStyle has no bot prefab. Run Apex Arsenal > Build Scenes & Assets.");
            return null;
        }
        Vector3 facing = lookAt - position;
        facing.y = 0f;
        var rotation = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity;
        var go = Object.Instantiate(style.botPrefab, position, rotation, parent);
        var bot = go.GetComponent<LocalBot>();
        if (bot == null)
        {
            Debug.LogError("The LabStyle bot prefab has no LocalBot component.", go);
            Object.Destroy(go);
            return null;
        }
        go.name = wave > 0 ? $"Lab Bot (wave {wave})" : "Lab Bot";
        go.GetComponent<LocalCombatant>().Team = LabTeam;

        // Sharper each wave, but always beatable. Turrets hit for 14; keep bots in that league.
        int step = Mathf.Clamp(wave - 1, 0, 3);
        bot.SetSkill(reactionSeconds: 0.55f - step * 0.1f, aimErrorDegrees: 3.5f - step * 0.8f);
        var weapon = go.GetComponent<LocalBotWeapon>();
        if (weapon != null)
            weapon.Damage = 9f + step * 2f;

        var visuals = go.GetComponent<LocalPlayerVisuals>();
        if (visuals != null)
            visuals.SetPlayerIndex(1);
        go.AddComponent<PlayerCosmetics>().Configure(BotColor, "BOT");
        AudioDirector.Instance.RegisterAll(go);
        if (wave > 0)
        {
            Poof.Spawn(position + Vector3.up, BotColor, 10, 4f);
            AudioDirector.PlayAt(Sfx.TurretRise, position, 0.7f);
        }
        return bot;
    }

    // LocalHealth deactivates the bot at 0 HP. (Freezing a bot at the end of a match only disables the script.)
    public static bool Alive(LocalBot bot) => bot != null && bot.gameObject.activeInHierarchy;
}
