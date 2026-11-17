using System;
using UnityEngine;
using UnityEngine.InputSystem;

// A power-up crate dropped from the ceiling. The first player to touch it gets a timed power-up.
// Events let teammates plug in: Ethan's bots can Seek CrateSpawner.CrateDropped targets, and Pierre
// can add effects by listening to Collected.
public class Crate : MonoBehaviour
{
    public static event Action<Crate, GameObject> Collected;

    [SerializeField] private float lifetime = 30f;
    [SerializeField] private float pickupRadius = 1.1f;

    private bool landed;
    private float age;
    private Transform visual;

    public PowerUp Contents { get; private set; }
    public bool Landed => landed;

    public static Crate Create(LabStyle style, Vector3 dropFrom, PowerUp contents)
    {
        var root = new GameObject("Power-up Crate");
        root.transform.position = dropFrom;
        var crate = root.AddComponent<Crate>();
        crate.Contents = contents;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * 0.9f;
        body.GetComponent<Renderer>().sharedMaterial = style.panelWhite;
        Destroy(body.GetComponent<Collider>());
        crate.visual = body.transform;

        Material accent = contents == PowerUp.SpeedSurge ? style.neonBlue : style.neonPink;
        foreach (var axis in new[] { Vector3.right, Vector3.forward })
        {
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(band.GetComponent<Collider>());
            band.name = "Band";
            band.transform.SetParent(body.transform, false);
            band.transform.localScale = axis == Vector3.right ? new Vector3(1.04f, 1.04f, 0.2f) : new Vector3(0.2f, 1.04f, 1.04f);
            band.GetComponent<Renderer>().sharedMaterial = accent;
        }

        var collider = root.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 0.9f;
        var rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.mass = 2f;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        return crate;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (landed)
            return;
        landed = true;
        AudioDirector.PlayAt(Sfx.CrateLand, transform.position);
        Poof.Spawn(transform.position, LabPalette.PanelWhite, 6, 3f);
        if (ArenaCamera.Instance != null)
            ArenaCamera.Instance.Shake(0.2f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (visual != null)
        {
            visual.localRotation = Quaternion.Euler(0f, age * 60f, 0f);
            visual.localPosition = landed ? Vector3.up * (0.15f + Mathf.Sin(age * 3f) * 0.08f) : Vector3.zero;
        }
        if (age > lifetime)
        {
            Poof.Spawn(transform.position, LabPalette.PanelGrey, 5, 2f);
            Destroy(gameObject);
            return;
        }
        if (!landed)
            return;

        // Pick-up check by distance: players use CharacterControllers, which don't send trigger events reliably.
        foreach (var player in PlayerInput.all)
        {
            if (player == null || !player.isActiveAndEnabled)
                continue;
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= pickupRadius)
            {
                Collect(player.gameObject);
                return;
            }
        }
    }

    private void Collect(GameObject collector)
    {
        var receiver = collector.GetComponent<PowerUpReceiver>();
        if (receiver != null)
            receiver.Grant(Contents);
        Collected?.Invoke(this, collector);
        AudioDirector.PlayAt(Sfx.Pickup, transform.position);
        var input = collector.GetComponent<PlayerInput>();
        Poof.Spawn(transform.position, input != null ? LabPalette.Player(input.playerIndex) : LabPalette.NeonPink, 12, 5f);
        MatchHud.Announce(Contents == PowerUp.SpeedSurge ? "SPEED SURGE!" : "INFINITE DASH!", input != null ? LabPalette.Player(input.playerIndex) : LabPalette.NeonPink, 1.2f);
        Destroy(gameObject);
    }
}
