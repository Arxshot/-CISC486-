using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Interactive hazard gate: a wall of laser beams across the centre line that switches on and off on a
// 30-second loop (off, warning blink, on). While on, it hurts anything with a LocalHealth inside it,
// and carves the NavMesh so AI bots path around it.
public class HazardGate : MonoBehaviour
{
    [SerializeField] private float offTime = 18f;
    [SerializeField] private float onTime = 12f; // offTime + onTime = the 30 s loop
    [SerializeField] private float warningTime = 3f;
    [SerializeField] private float damagePerTick = 12f;
    [SerializeField] private float tickInterval = 0.35f;

    private readonly List<Renderer> beams = new();
    private Renderer floorStrip;
    private NavMeshObstacle obstacle;
    private Vector3 halfExtents;
    private float timer;
    private float tickTimer;
    private bool warned;
    private AudioSource hum;

    public bool IsOn { get; private set; }
    public bool Running { get; set; }
    public float TimeUntilSwitch => (IsOn ? onTime : offTime) - timer;

    public static HazardGate Create(LabStyle style, Transform parent, Vector3 from, Vector3 to)
    {
        var root = new GameObject("Hazard Gate");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = (from + to) * 0.5f;
        Vector3 span = to - from;
        float length = span.magnitude;
        root.transform.localRotation = Quaternion.LookRotation(span.normalized, Vector3.up);
        var gate = root.AddComponent<HazardGate>();

        // Emitter posts at both ends.
        foreach (float end in new[] { -0.5f, 0.5f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Emitter";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 1.25f, end * (length + 0.5f));
            post.transform.localScale = new Vector3(0.5f, 2.5f, 0.5f);
            post.GetComponent<Renderer>().sharedMaterial = style.panelGrey;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cap.GetComponent<Collider>());
            cap.name = "Emitter Light";
            cap.transform.SetParent(post.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            cap.transform.localScale = new Vector3(1.05f, 0.05f, 1.05f);
            cap.GetComponent<Renderer>().sharedMaterial = style.neonPink;
        }

        foreach (float height in new[] { 0.35f, 0.8f, 1.25f, 1.7f, 2.15f })
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(beam.GetComponent<Collider>());
            beam.name = "Laser";
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, height, 0f);
            beam.transform.localScale = new Vector3(0.06f, 0.06f, length + 0.4f);
            var renderer = beam.GetComponent<Renderer>();
            renderer.sharedMaterial = style.laser;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gate.beams.Add(renderer);
        }

        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(strip.GetComponent<Collider>());
        strip.name = "Floor Warning";
        strip.transform.SetParent(root.transform, false);
        strip.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        strip.transform.localScale = new Vector3(0.9f, 0.02f, length);
        gate.floorStrip = strip.GetComponent<Renderer>();
        gate.floorStrip.sharedMaterial = style.floorLine;

        gate.obstacle = root.AddComponent<NavMeshObstacle>();
        gate.obstacle.shape = NavMeshObstacleShape.Box;
        gate.obstacle.center = new Vector3(0f, 1f, 0f);
        gate.obstacle.size = new Vector3(0.8f, 2f, length);
        gate.obstacle.carving = true;
        gate.obstacle.enabled = false;

        gate.halfExtents = new Vector3(0.4f, 1.2f, length * 0.5f);
        gate.hum = root.AddComponent<AudioSource>();
        gate.hum.clip = SynthAudio.GateHum;
        gate.hum.loop = true;
        gate.hum.spatialBlend = 0.7f;
        gate.hum.minDistance = 4f;
        gate.SetOn(false, silent: true);
        return gate;
    }

    private void Update()
    {
        if (!Running)
            return;
        timer += Time.deltaTime;

        if (!IsOn)
        {
            float untilOn = offTime - timer;
            bool blinking = untilOn <= warningTime;
            bool show = blinking && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
            foreach (var beam in beams)
                beam.enabled = show;
            if (blinking && !warned)
            {
                warned = true;
                AudioDirector.PlayAt(Sfx.GateWarn, transform.position);
            }
            if (timer >= offTime)
                SetOn(true);
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            DamageInside();
        }
        if (timer >= onTime)
            SetOn(false);
    }

    private void DamageInside()
    {
        var hit = new HashSet<LocalHealth>();
        foreach (var collider in Physics.OverlapBox(transform.position + Vector3.up * 1.2f, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            var health = collider.GetComponentInParent<LocalHealth>();
            if (health != null && hit.Add(health))
                health.TakeDamage(damagePerTick);
        }
    }

    private void SetOn(bool on, bool silent = false)
    {
        IsOn = on;
        timer = 0f;
        tickTimer = 0f;
        warned = false;
        foreach (var beam in beams)
            beam.enabled = on;
        var style = LabStyle.Active;
        if (floorStrip != null && style != null)
            floorStrip.sharedMaterial = on ? style.neonPink : style.floorLine;
        if (obstacle != null)
            obstacle.enabled = on;
        if (hum != null)
        {
            hum.volume = 0.35f * VolumeSettings.SfxGain;
            if (on)
                hum.Play();
            else
                hum.Stop();
        }
        if (on && !silent)
            AudioDirector.PlayAt(Sfx.GateOn, transform.position);
    }
}
