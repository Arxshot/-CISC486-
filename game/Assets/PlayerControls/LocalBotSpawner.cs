using System.Collections.Generic;
using UnityEngine;

// Spawns laser-tag bots when the arena starts. Bots are scene roots, not children of the arena,
// and are removed together with this component.
public class LocalBotSpawner : MonoBehaviour
{
    [SerializeField] private LocalBot botPrefab;
    [SerializeField, Range(0, 8)] private int botCount = 3;
    [Tooltip("Spawn points relative to this object. Bots reuse points if there are more bots than points.")]
    [SerializeField] private Vector3[] spawnPoints = { new Vector3(-6f, 0.1f, 5f), new Vector3(6f, 0.1f, 5f), new Vector3(-2f, 0.1f, 5f), new Vector3(2f, 0.1f, 5f) };

    private readonly List<LocalBot> bots = new List<LocalBot>();
    public IReadOnlyList<LocalBot> Bots => bots;

    private void Start()
    {
        if (botPrefab == null)
        {
            Debug.LogError("LocalBotSpawner requires a bot prefab.", this);
            return;
        }
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("LocalBotSpawner requires at least one spawn point.", this);
            return;
        }
        for (int i = 0; i < botCount; i++)
        {
            Vector3 position = transform.TransformPoint(spawnPoints[i % spawnPoints.Length]);
            // Face the middle of the arena.
            Vector3 inward = transform.position - position;
            inward.y = 0f;
            Quaternion rotation = inward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(inward) : transform.rotation;
            var bot = Instantiate(botPrefab, position, rotation);
            bot.name = $"Bot {i + 1}";
            var visuals = bot.GetComponent<LocalPlayerVisuals>();
            if (visuals != null)
                visuals.SetPlayerIndex(2 + i); // Players 1 and 2 use the first two colours.
            bots.Add(bot);
        }
    }

    public void DespawnBots()
    {
        foreach (var bot in bots)
            if (bot != null)
            {
                bot.gameObject.SetActive(false);
                Destroy(bot.gameObject);
            }
        bots.Clear();
    }

    private void OnDestroy()
    {
        // Unloading the scene destroys the bots anyway.
        if (gameObject.scene.isLoaded)
            DespawnBots();
    }
}
