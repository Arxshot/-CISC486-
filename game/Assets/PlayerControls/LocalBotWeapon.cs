using UnityEngine;

// The bot's laser: the same semi-auto hitscan rules as LocalPlayerShooting, driven by LocalBot instead of input.
[DefaultExecutionOrder(50)]
public class LocalBotWeapon : MonoBehaviour
{
    private const float ShotRadius = 0.01f;
    [SerializeField, Min(0.01f)] private float shotInterval = 0.75f;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(0.1f)] private float range = 40f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private GameObject handgunVisual;
    [SerializeField] private Vector3 handgunLocalEulerAngles = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 muzzleLocalPosition = new Vector3(0f, 0.16021f, 0.249f);
    [SerializeField] private AudioClip shotSound;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private Material tracerMaterial;

    private Transform weaponMount;
    private Transform muzzle;
    private AudioSource audioSource;
    private Vector3 aimPoint;
    private bool hasAimPoint;
    private bool shotPending;
    private float nextShotTime;

    public event System.Action ShotFired;
    public Vector3 MuzzlePosition => muzzle.position;
    public bool IsReady => Time.time >= nextShotTime;
    public float Range => range;
    public float Damage
    {
        get => damage;
        set => damage = Mathf.Max(0f, value);
    }

    private LocalCombatant owner;

    private void Awake()
    {
        owner = GetComponent<LocalCombatant>();
        weaponMount = new GameObject("Handgun").transform;
        weaponMount.SetParent(transform, false);
        weaponMount.localPosition = new Vector3(0.35f, 1f, 0.4f);
        if (handgunVisual != null)
        {
            var visual = Instantiate(handgunVisual, weaponMount, false).transform;
            // The template handgun's barrel points along +X; gameplay aiming uses +Z.
            visual.localRotation = Quaternion.Euler(handgunLocalEulerAngles);
        }
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(weaponMount, false);
        muzzle.localPosition = muzzleLocalPosition;

        var visuals = GetComponent<LocalPlayerVisuals>();
        if (visuals != null)
            visuals.SetWeaponMount(weaponMount);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public void AimAt(Vector3 point)
    {
        aimPoint = point;
        hasAimPoint = true;
    }

    public void ClearAim() => hasAimPoint = false;

    // Queues a shot at the point for LateUpdate, after the hand animation has placed the gun.
    public bool TryFire(Vector3 point)
    {
        if (!IsReady)
            return false;
        AimAt(point);
        nextShotTime = Time.time + shotInterval;
        shotPending = true;
        return true;
    }

    private void LateUpdate()
    {
        if (hasAimPoint)
        {
            // Re-evaluate after rotating, because the muzzle is offset from the mount's pivot.
            for (int i = 0; i < 2; i++)
            {
                Vector3 direction = aimPoint - muzzle.position;
                if (direction.sqrMagnitude > 0.0001f)
                    weaponMount.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
        else
            weaponMount.rotation = transform.rotation;

        if (!shotPending)
            return;
        shotPending = false;
        Shoot();
    }

    private void Shoot()
    {
        Vector3 start = muzzle.position;
        Vector3 direction = hasAimPoint && (aimPoint - start).sqrMagnitude > 0.0001f ? (aimPoint - start).normalized : transform.forward;
        Vector3 chest = transform.position + Vector3.up;
        Vector3 toMuzzle = start - chest;
        // As for players, a muzzle poking through a wall must not shoot through it.
        bool blocked = Trace(chest, toMuzzle.normalized, toMuzzle.magnitude, out Vector3 end, out Collider hit, out Vector3 normal);
        if (!blocked)
            Trace(start, direction, range, out end, out hit, out normal);

        ShotFired?.Invoke();
        DrawTracer(start, end);
        if (shotSound != null)
            audioSource.PlayOneShot(shotSound);
        if (muzzleFlash != null)
            Destroy(Instantiate(muzzleFlash, muzzle.position, muzzle.rotation), 2f);
        if (hit == null)
            return;

        var health = hit.GetComponentInParent<LocalHealth>();
        // No friendly fire: a teammate standing in the line of fire soaks the shot unharmed.
        var victim = health != null ? health.GetComponent<LocalCombatant>() : null;
        bool ally = victim != null && owner != null && !owner.IsHostileTo(victim);
        if (health != null && !health.transform.IsChildOf(transform) && !ally)
            health.TakeDamage(damage, gameObject);
        if (impactEffect != null)
            Destroy(Instantiate(impactEffect, end, Quaternion.LookRotation(normal)), 2f);
    }

    private bool Trace(Vector3 origin, Vector3 direction, float distance, out Vector3 end, out Collider collider, out Vector3 normal)
    {
        // Casts skip colliders that contain their origin, so check overlaps first.
        foreach (var overlap in Physics.OverlapSphere(origin, ShotRadius, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (overlap.transform.IsChildOf(transform))
                continue;
            end = overlap.ClosestPoint(origin);
            normal = origin - end;
            normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : -transform.forward;
            collider = overlap;
            return true;
        }

        float closest = float.PositiveInfinity;
        end = origin + direction * distance;
        collider = null;
        normal = Vector3.zero;
        foreach (var hit in Physics.SphereCastAll(origin, ShotRadius, direction, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closest)
                continue;
            closest = hit.distance;
            end = hit.point;
            collider = hit.collider;
            normal = hit.normal;
        }
        return collider != null;
    }

    private void DrawTracer(Vector3 start, Vector3 end)
    {
        if (tracerMaterial == null)
            return;
        var tracer = new GameObject("Bot shot tracer").AddComponent<LineRenderer>();
        tracer.sharedMaterial = tracerMaterial;
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;
        tracer.startWidth = tracer.endWidth = ShotRadius * 2f;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        Destroy(tracer.gameObject, 0.06f);
    }

    private void OnDisable()
    {
        shotPending = false;
        hasAimPoint = false;
    }
}
