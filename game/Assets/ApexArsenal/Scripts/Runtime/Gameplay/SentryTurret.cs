using UnityEngine;
using UnityEngine.InputSystem;

// Placeholder enemy for Co-op until Ethan's AI bots land: a cute lab sentry that pops out of a floor
// hatch, turns to the nearest player it can see, paints them with a telegraph laser, then fires.
// Scripted, not AI: no steering or decision tree. It has a LocalHealth, so players shoot it down.
public class SentryTurret : MonoBehaviour
{
    private enum State
    {
        Rising,
        Searching,
        Charging,
        Cooldown,
    }

    [SerializeField] private float range = 24f;
    [SerializeField] private float chargeTime = 0.75f;
    [SerializeField] private float lockTime = 0.18f; // aim stops tracking this long before firing, so you can dodge
    [SerializeField] private float cooldown = 1.8f;
    [SerializeField] private float damage = 14f;
    [SerializeField] private float turnSpeed = 140f;

    private State state;
    private float timer;
    private Transform head;
    private Transform eye;
    private LineRenderer laser;
    private Transform target;
    private Vector3 lockedDirection;
    private LocalHealth health;
    private float lastHealth;
    private Material eyeMaterial;
    private bool quitting;

    public bool Alive => gameObject.activeInHierarchy && (health == null || health.CurrentHealth > 0f);

    public static SentryTurret Create(LabStyle style, Transform parent, Vector3 position, int wave)
    {
        var root = new GameObject("Sentry Turret");
        root.transform.SetParent(parent, true);
        root.transform.position = position;
        var turret = root.AddComponent<SentryTurret>();
        turret.cooldown = Mathf.Max(0.9f, 1.8f - wave * 0.3f);
        turret.chargeTime = Mathf.Max(0.5f, 0.75f - wave * 0.08f);

        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);

        var pedestal = Primitive(PrimitiveType.Cylinder, model, "Pedestal", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.45f, 0.7f), style.panelGrey);
        Primitive(PrimitiveType.Cylinder, pedestal, "Stripe", new Vector3(0f, 0.6f, 0f), new Vector3(1.04f, 0.08f, 1.04f), style.neonPink);

        turret.head = new GameObject("Head").transform;
        turret.head.SetParent(model, false);
        turret.head.localPosition = new Vector3(0f, 1.3f, 0f);
        Primitive(PrimitiveType.Sphere, turret.head, "Shell", Vector3.zero, new Vector3(0.95f, 0.85f, 0.95f), style.panelWhite);
        Primitive(PrimitiveType.Cube, turret.head, "Visor", new Vector3(0f, 0.05f, 0.32f), new Vector3(0.7f, 0.22f, 0.3f), style.ink);
        turret.eyeMaterial = new Material(style.neonPink);
        turret.eye = Primitive(PrimitiveType.Sphere, turret.head, "Eye", new Vector3(0f, 0.05f, 0.47f), Vector3.one * 0.16f, turret.eyeMaterial);
        Primitive(PrimitiveType.Cube, turret.head, "Barrel L", new Vector3(-0.3f, -0.15f, 0.45f), new Vector3(0.1f, 0.1f, 0.4f), style.panelGrey);
        Primitive(PrimitiveType.Cube, turret.head, "Barrel R", new Vector3(0.3f, -0.15f, 0.45f), new Vector3(0.1f, 0.1f, 0.4f), style.panelGrey);

        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.height = 1.8f;
        collider.radius = 0.5f;
        turret.health = root.AddComponent<LocalHealth>();

        turret.laser = root.AddComponent<LineRenderer>();
        turret.laser.sharedMaterial = style.laser;
        turret.laser.widthMultiplier = 0.05f;
        turret.laser.positionCount = 2;
        turret.laser.useWorldSpace = true;
        turret.laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        turret.laser.enabled = false;

        model.localPosition = Vector3.down * 1.9f;
        turret.state = State.Rising;
        AudioDirector.PlayAt(Sfx.TurretRise, position, 0.8f);
        return turret;
    }

    private static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    private void Start() => lastHealth = health != null ? health.CurrentHealth : 0f;

    private void Update()
    {
        timer += Time.deltaTime;
        if (health != null && health.CurrentHealth < lastHealth)
        {
            // Hit wobble.
            head.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            lastHealth = health.CurrentHealth;
        }
        head.localScale = Vector3.Lerp(head.localScale, Vector3.one, 1f - Mathf.Exp(-Time.deltaTime * 12f));

        switch (state)
        {
            case State.Rising:
                {
                    var model = head.parent;
                    model.localPosition = Vector3.MoveTowards(model.localPosition, Vector3.zero, Time.deltaTime * 2.6f);
                    if (model.localPosition == Vector3.zero)
                        Enter(State.Searching);
                    break;
                }
            case State.Searching:
                head.Rotate(0f, 35f * Time.deltaTime, 0f, Space.World);
                if (timer > 0.25f)
                {
                    timer = 0f;
                    target = FindTarget();
                    if (target != null)
                    {
                        Enter(State.Charging);
                        AudioDirector.PlayAt(Sfx.TurretCharge, transform.position, 0.7f);
                    }
                }
                break;
            case State.Charging:
                {
                    if (target == null || !target.gameObject.activeInHierarchy)
                    {
                        Enter(State.Searching);
                        break;
                    }
                    Vector3 aimPoint = target.position + Vector3.up * 1.1f;
                    if (timer < chargeTime - lockTime)
                        lockedDirection = (aimPoint - eye.position).normalized;
                    Vector3 flat = lockedDirection;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.001f)
                        head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(flat), turnSpeed * 2f * Time.deltaTime);

                    laser.enabled = true;
                    float k = timer / chargeTime;
                    laser.widthMultiplier = Mathf.Lerp(0.02f, 0.09f, k) * (Mathf.Repeat(timer * 14f, 1f) < 0.5f ? 1f : 0.6f);
                    laser.SetPosition(0, eye.position);
                    laser.SetPosition(1, Trace(eye.position, lockedDirection, out _));
                    if (timer >= chargeTime)
                        Fire();
                    break;
                }
            case State.Cooldown:
                laser.widthMultiplier = Mathf.Max(0f, laser.widthMultiplier - Time.deltaTime * 1.2f);
                laser.enabled = laser.widthMultiplier > 0.001f;
                if (timer >= cooldown)
                    Enter(State.Searching);
                break;
        }
        LabStyle.SetColor(eyeMaterial, state == State.Charging ? Color.Lerp(LabPalette.NeonPink, Color.white, Mathf.Repeat(timer * 8f, 1f)) : LabPalette.NeonPink);
    }

    private void Fire()
    {
        Vector3 end = Trace(eye.position, lockedDirection, out var hit);
        laser.SetPosition(1, end);
        laser.widthMultiplier = 0.16f;
        AudioDirector.PlayAt(Sfx.TurretShot, eye.position, 0.8f);
        head.localScale = new Vector3(0.85f, 1.15f, 0.85f);
        if (hit != null)
        {
            var health = hit.GetComponentInParent<LocalHealth>();
            if (health != null && hit.GetComponentInParent<SentryTurret>() == null)
                health.TakeDamage(damage);
        }
        Enter(State.Cooldown);
    }

    private void Enter(State next)
    {
        state = next;
        timer = 0f;
        if (next == State.Searching)
            laser.enabled = false;
    }

    private Transform FindTarget()
    {
        Transform best = null;
        float bestDistance = range;
        foreach (var player in PlayerInput.all)
        {
            if (player == null || !player.isActiveAndEnabled)
                continue;
            Vector3 aim = player.transform.position + Vector3.up * 1.1f;
            float distance = Vector3.Distance(eye.position, aim);
            if (distance >= bestDistance)
                continue;
            Trace(eye.position, (aim - eye.position).normalized, out var hit, distance + 0.5f);
            if (hit != null && hit.GetComponentInParent<PlayerInput>() == player)
            {
                best = player.transform;
                bestDistance = distance;
            }
        }
        return best;
    }

    // Nearest hit that isn't part of this turret.
    private Vector3 Trace(Vector3 origin, Vector3 direction, out Collider hit, float distance = 0f)
    {
        if (distance <= 0f)
            distance = range;
        hit = null;
        float closest = distance;
        foreach (var candidate in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (candidate.collider.transform.IsChildOf(transform) || candidate.distance >= closest)
                continue;
            closest = candidate.distance;
            hit = candidate.collider;
        }
        return origin + direction * closest;
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDisable()
    {
        if (quitting || health == null || health.CurrentHealth > 0f || !gameObject.scene.isLoaded)
            return;
        Poof.Spawn(transform.position + Vector3.up, LabPalette.NeonPink, 14, 6f);
        AudioDirector.PlayAt(Sfx.Eliminated, transform.position);
        if (ArenaCamera.Instance != null)
            ArenaCamera.Instance.Shake(0.35f);
    }

    private void OnDestroy()
    {
        if (eyeMaterial != null)
            Destroy(eyeMaterial);
    }
}
