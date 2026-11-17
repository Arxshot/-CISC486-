using System;
using System.Collections.Generic;
using UnityEngine;

// Timed scripted event: every 45 seconds a power-up crate drops from the ceiling onto a random
// drop zone. CrateDropped is the hook for AI "weapon race" (Seek) behaviour.
public class CrateSpawner : MonoBehaviour
{
    public static event Action<Crate> CrateDropped;

    [SerializeField] private float interval = 45f;
    [SerializeField] private float firstDrop = 20f;
    [SerializeField] private float dropHeight = 9f;

    private readonly List<Vector3> anchors = new();
    private float timer;
    private int lastAnchor = -1;

    public bool Running { get; set; }
    public float TimeUntilDrop => (lastAnchor < 0 ? firstDrop : interval) - timer;

    public void Initialize(IEnumerable<Vector3> dropZones)
    {
        anchors.Clear();
        anchors.AddRange(dropZones);
    }

    private void Update()
    {
        if (!Running || anchors.Count == 0)
            return;
        timer += Time.deltaTime;
        if (timer >= (lastAnchor < 0 ? firstDrop : interval))
        {
            timer = 0f;
            Drop();
        }
    }

    public Crate Drop()
    {
        var style = LabStyle.Active;
        if (style == null || anchors.Count == 0)
            return null;
        int index = UnityEngine.Random.Range(0, anchors.Count);
        if (anchors.Count > 1 && index == lastAnchor)
            index = (index + 1) % anchors.Count;
        lastAnchor = index;
        var contents = UnityEngine.Random.value < 0.5f ? PowerUp.SpeedSurge : PowerUp.InfiniteDash;
        var crate = Crate.Create(style, anchors[index] + Vector3.up * dropHeight, contents);
        MatchHud.Announce("SUPPLY DROP!", LabPalette.NeonPink, 1.2f);
        CrateDropped?.Invoke(crate);
        return crate;
    }
}
