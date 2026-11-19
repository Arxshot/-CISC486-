using Blocks.Gameplay.Shooter;
using UnityEngine;
using UnityEngine.InputSystem;

// Local adaptation of SingleShotMechanism and HitscanShooting. No network lifecycle is required.
[RequireComponent(typeof(PlayerInput))]
[DefaultExecutionOrder(50)]
public class LocalPlayerShooting : MonoBehaviour
{
    public enum WeaponSlot
    {
        Shotgun,
        Rifle,
        Pistol,
    }

    // Match the visible tracer's 2 cm diameter, including grazing hits at cover edges.
    private const float ShotRadius = 0.01f;
    [SerializeField, Min(0.01f)] private float shotInterval = 0.5f;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(0.1f)] private float range = 40f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool friendlyFire;
    [SerializeField] private GameObject handgunVisual;
    [SerializeField] private Vector3 handgunLocalEulerAngles = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 muzzleLocalPosition = new Vector3(0f, 0.16021f, 0.249f);
    [SerializeField] private AudioClip shotSound;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private Material tracerMaterial;

    [Header("Shotgun")]
    [SerializeField] private GameObject shotgunVisual;
    [SerializeField] private Vector3 shotgunLocalEulerAngles = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 shotgunMuzzleLocalPosition = new Vector3(0f, 0.16021f, 0.5911f);
    [SerializeField, Min(0.01f)] private float shotgunShotInterval = 0.8f;
    [SerializeField, Min(0f)] private float shotgunPelletDamage = 6f;
    [SerializeField, Min(0.1f)] private float shotgunRange = 18f;
    [SerializeField, Range(1, 24)] private int shotgunPelletCount = 8;
    [SerializeField, Range(0f, 30f)] private float shotgunSpreadAngle = 7f;
    [SerializeField] private AudioClip shotgunShotSound;

    [Header("Rifle")]
    [SerializeField] private GameObject rifleVisual;
    [SerializeField] private Vector3 rifleLocalEulerAngles = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 rifleMuzzleLocalPosition = new Vector3(-0.0004f, 0.1505f, 0.7462f);
    [SerializeField, Min(0.01f)] private float rifleShotInterval = 0.12f;
    [SerializeField, Min(0f)] private float rifleDamage = 10f;
    [SerializeField, Min(0.1f)] private float rifleRange = 50f;
    [SerializeField] private AudioClip rifleShotSound;

    [Header("Magazines and reload audio")]
    [SerializeField] private WeaponData shotgunData;
    [SerializeField] private WeaponData rifleData;
    [SerializeField] private WeaponData pistolData;
    [SerializeField] private AudioClip shotgunReloadSound;
    [SerializeField] private AudioClip rifleReloadSound;
    [SerializeField] private AudioClip pistolReloadSound;
    private LocalMagazine[] magazines;
    private LocalMagazine Magazine => magazines[(int)currentWeapon];
    public int RoundsLoaded => Magazine.Rounds;
    public int MagazineSize => Magazine.Capacity;
    public bool IsReloading => magazines != null && Magazine.IsReloading;
    public Sprite WeaponIcon
    {
        get
        {
            var data = currentWeapon switch
            {
                WeaponSlot.Shotgun => shotgunData,
                WeaponSlot.Rifle => rifleData,
                _ => pistolData
            };
            return data != null ? data.weaponIcon : null;
        }
    }

    [Header("Cursor targeting and aim guide")]
    [SerializeField] private LayerMask cursorTargetMask = Physics.DefaultRaycastLayers;
    [SerializeField] private Material aimGuideMaterial;
    [SerializeField, Range(0.005f, 0.04f)] private float aimGuideWidth = 0.025f;

    private readonly LocalShotTrigger trigger = new LocalShotTrigger();
    [SerializeField] private WeaponSlot currentWeapon = WeaponSlot.Pistol;
    private bool shotPending;
    private Transform muzzle;
    private Transform weaponVisual;
    private AudioSource audioSource;
    private Transform weaponMount;
    private PlayerMovement movement;
    private LineRenderer aimGuide;
    private bool aimGuideHeld;
    public event System.Action ShotFired;
    public Vector3 AimOrigin => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;
    public WeaponSlot CurrentWeapon => currentWeapon;
    public float CurrentDamage => currentWeapon == WeaponSlot.Shotgun ? shotgunPelletDamage : currentWeapon == WeaponSlot.Rifle ? rifleDamage : damage;
    public float CurrentRange => currentWeapon == WeaponSlot.Shotgun ? shotgunRange : currentWeapon == WeaponSlot.Rifle ? rifleRange : range;

    public readonly struct ShotPath
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly Collider Collider;
        public readonly Vector3 Normal;
        public bool HasHit => Collider != null;

        public ShotPath(Vector3 start, Vector3 end, Collider collider, Vector3 normal)
        {
            Start = start;
            End = end;
            Collider = collider;
            Normal = normal;
        }
    }

    private void Awake()
    {
        magazines = new[]
        {
            CreateMagazine(shotgunData, 8, 2f),
            CreateMagazine(rifleData, 30, 1f),
            CreateMagazine(pistolData, 12, 2f)
        };

        weaponMount = new GameObject("WeaponMount").transform;
        movement = GetComponent<PlayerMovement>();
        weaponMount.SetParent(transform, false);
        weaponMount.localPosition = new Vector3(0.35f, 1f, 0.4f);
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(weaponMount, false);

        var visuals = GetComponent<LocalPlayerVisuals>();
        if (visuals != null)
            visuals.SetWeaponMount(weaponMount);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        EquipWeapon(currentWeapon);

        aimGuide = new GameObject("Aim guide").AddComponent<LineRenderer>();
        aimGuide.transform.SetParent(transform, false);
        aimGuide.sharedMaterial = aimGuideMaterial;
        aimGuide.useWorldSpace = true;
        aimGuide.positionCount = 2;
        aimGuide.startWidth = aimGuide.endWidth = aimGuideWidth;
        aimGuide.numCapVertices = 2;
        aimGuide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimGuide.receiveShadows = false;
        aimGuide.enabled = false;
    }

    // Fire uses Press And Release so PlayerInput.SendMessages forwards both button edges.
    public void OnFire(InputValue value) => SetFirePressed(value.isPressed);
    public void OnReload(InputValue value) { if (value.isPressed) StartReload(); }
    public void OnAimGuide(InputValue value) => SetAimGuideHeld(value.isPressed);
    public void OnSelectShotgun(InputValue value) { if (value.isPressed) SelectWeapon(WeaponSlot.Shotgun); }
    public void OnSelectRifle(InputValue value) { if (value.isPressed) SelectWeapon(WeaponSlot.Rifle); }
    public void OnSelectPistol(InputValue value) { if (value.isPressed) SelectWeapon(WeaponSlot.Pistol); }
    public void OnCycleWeapon(InputValue value) { if (value.isPressed) SelectNextWeapon(); }
    public void SetAimGuideHeld(bool held) => aimGuideHeld = held;

    public void SelectWeapon(WeaponSlot weapon)
    {
        if (weapon == currentWeapon)
            return;
        if (weapon != WeaponSlot.Shotgun && weapon != WeaponSlot.Rifle && weapon != WeaponSlot.Pistol)
            return;

        CancelReload();
        currentWeapon = weapon;
        shotPending = false;
        trigger.Release();
        if (weaponMount != null)
            EquipWeapon(weapon);
    }

    public void SelectNextWeapon() => SelectWeapon((WeaponSlot)(((int)currentWeapon + 1) % 3));

    public bool TryGetMouseAimPoint(Vector3 origin, out Vector3 point)
    {
        point = default;
        if (movement == null || !movement.TryGetMouseAimRay(out Ray ray))
            return false;
        if (TryGetHit(ray.origin, ray.direction, 1000f, cursorTargetMask, out RaycastHit hit))
        {
            point = hit.point;
            return true;
        }
        // Floors are excluded from cursor picking; empty space aims horizontally at muzzle height.
        var plane = new Plane(Vector3.up, origin);
        if (!plane.Raycast(ray, out float distance))
            return false;
        point = ray.GetPoint(distance);
        return true;
    }

    private static LocalMagazine CreateMagazine(WeaponData data, int defaultSize, float defaultTime)
    {
        return data != null ? new LocalMagazine(data.clipSize, data.reloadTime) : new LocalMagazine(defaultSize, defaultTime);
    }

    public void StartReload()
    {
        if (!isActiveAndEnabled || magazines == null || !Magazine.StartReload(Time.timeAsDouble))
            return;

        shotPending = false;
        audioSource.clip = currentWeapon switch
        {
            WeaponSlot.Shotgun => shotgunReloadSound,
            WeaponSlot.Rifle => rifleReloadSound,
            _ => pistolReloadSound
        };
        if (audioSource.clip != null)
            audioSource.Play();
    }

    private void CancelReload()
    {
        if (!IsReloading)
            return;
        Magazine.CancelReload();
        audioSource.Stop();
    }

    public void SetFirePressed(bool pressed)
    {
        // Preserve quick press/release events until LateUpdate, after movement has updated facing.
        if (magazines == null)
            return;
        Magazine.UpdateReload(Time.timeAsDouble);
        if (pressed && RoundsLoaded == 0)
            StartReload();
        shotPending |= trigger.SetPressed(pressed, Time.timeAsDouble, CurrentShotInterval, !IsReloading);
    }

    private void LateUpdate()
    {
        if (weaponMount == null)
            return;

        if (TryGetMouseAimPoint(AimOrigin, out Vector3 target))
        {
            // Aim the visual barrel as well as the ray. Re-evaluate after rotating the offset muzzle.
            for (int i = 0; i < 2; i++)
            {
                Vector3 direction = target - AimOrigin;
                if (direction.sqrMagnitude > 0.0001f)
                    weaponMount.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
        else
            weaponMount.rotation = transform.rotation;

        Magazine.UpdateReload(Time.timeAsDouble);
        if (!shotPending && !IsReloading && currentWeapon == WeaponSlot.Rifle)
            shotPending = trigger.TryRepeat(Time.timeAsDouble, CurrentShotInterval);

        if (!shotPending && !aimGuideHeld)
        {
            aimGuide.enabled = false;
            return;
        }
        ShotPath path = GetShotPath();
        aimGuide.enabled = aimGuideHeld && aimGuideMaterial != null && (path.End - path.Start).sqrMagnitude > 0.0004f;
        if (aimGuide.enabled)
        {
            aimGuide.SetPosition(0, path.Start);
            aimGuide.SetPosition(1, path.End);
        }
        if (!shotPending)
            return;
        shotPending = false;
        if (Magazine.TryConsume())
            Shoot(path);
        if (RoundsLoaded == 0)
            StartReload();
    }

    public ShotPath GetShotPath()
    {
        Vector3 start = AimOrigin;
        Vector3 direction = GetAimDirection(start);
        return TraceWithBarrel(start, direction, CurrentRange);
    }

    private Vector3 GetAimDirection(Vector3 origin)
    {
        Vector3 direction = transform.forward;
        if (TryGetMouseAimPoint(origin, out Vector3 target) && (target - origin).sqrMagnitude > 0.0001f)
            direction = (target - origin).normalized;
        return direction;
    }

    private ShotPath TraceWithBarrel(Vector3 start, Vector3 direction, float shotRange)
    {
        Vector3 chest = transform.position + Vector3.up;
        Vector3 toMuzzle = start - chest;
        // Check the barrel's path first so a muzzle poking through a wall cannot shoot through it.
        if (toMuzzle.sqrMagnitude > 0.000001f)
        {
            ShotPath barrelPath = TraceShot(chest, toMuzzle.normalized, toMuzzle.magnitude);
            if (barrelPath.HasHit)
                return barrelPath;
        }
        return TraceShot(start, direction, shotRange);
    }

    private ShotPath TraceShot(Vector3 origin, Vector3 direction, float distance)
    {
        // Unity casts skip colliders containing their origin. Check overlaps before each cast,
        // both at the chest and at the muzzle, so an embedded barrel cannot bypass cover.
        foreach (var collider in Physics.OverlapSphere(origin, ShotRadius, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (collider.transform.IsChildOf(transform))
                continue;
            Vector3 point = collider.ClosestPoint(origin);
            Vector3 normal = origin - point;
            normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : -direction;
            if (normal.sqrMagnitude < 0.000001f)
                normal = -transform.forward;
            return new ShotPath(origin, point, collider, normal);
        }

        RaycastHit closest = default;
        float closestDistance = float.PositiveInfinity;
        foreach (var hit in Physics.SphereCastAll(origin, ShotRadius, direction, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closestDistance)
                continue;
            closest = hit;
            closestDistance = hit.distance;
        }
        return closestDistance < float.PositiveInfinity
            ? new ShotPath(origin, closest.point, closest.collider, closest.normal)
            : new ShotPath(origin, origin + direction * distance, null, Vector3.zero);
    }

    private void Shoot(ShotPath path)
    {
        ShotFired?.Invoke();
        AudioClip sound = CurrentShotSound;
        if (sound != null)
            audioSource.PlayOneShot(sound);
        if (muzzleFlash != null)
            Destroy(Instantiate(muzzleFlash, AimOrigin, weaponMount.rotation), 2f);

        if (currentWeapon == WeaponSlot.Shotgun)
        {
            Vector3 start = AimOrigin;
            Quaternion aimRotation = Quaternion.LookRotation(GetAimDirection(start));
            for (int i = 0; i < shotgunPelletCount; i++)
            {
                Vector2 spread = Random.insideUnitCircle * shotgunSpreadAngle;
                Vector3 pelletDirection = aimRotation * Quaternion.Euler(spread.x, spread.y, 0f) * Vector3.forward;
                ShotPath pelletPath = TraceWithBarrel(start, pelletDirection, shotgunRange);
                DrawTracer(pelletPath.Start, pelletPath.End);
                ApplyHit(pelletPath, shotgunPelletDamage);
            }
            return;
        }

        DrawTracer(path.Start, path.End);
        ApplyHit(path, CurrentDamage);
    }

    private void ApplyHit(ShotPath path, float hitDamage)
    {
        if (!path.HasHit)
            return;

        var health = path.Collider.GetComponentInParent<LocalHealth>();
        bool isPlayer = path.Collider.GetComponentInParent<PlayerInput>() != null;
        if (health != null && (friendlyFire || !isPlayer))
            health.TakeDamage(hitDamage, gameObject);

        if (impactEffect != null)
            Destroy(Instantiate(impactEffect, path.End, Quaternion.LookRotation(path.Normal)), 2f);
    }

    private bool TryGetHit(Vector3 origin, Vector3 direction, float distance, int layers, out RaycastHit closest)
    {
        closest = default;
        float closestDistance = float.PositiveInfinity;
        // RaycastAll lets us ignore every collider on the shooter without hiding targets behind it.
        foreach (var hit in Physics.RaycastAll(origin, direction, distance, layers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closestDistance)
                continue;
            closest = hit;
            closestDistance = hit.distance;
        }
        return closestDistance < float.PositiveInfinity;
    }

    private void DrawTracer(Vector3 start, Vector3 end)
    {
        if (tracerMaterial == null)
            return;
        var tracer = new GameObject("Shot tracer").AddComponent<LineRenderer>();
        tracer.sharedMaterial = tracerMaterial;
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;
        tracer.startWidth = tracer.endWidth = ShotRadius * 2f;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        Destroy(tracer.gameObject, 0.06f);
    }

    private void EquipWeapon(WeaponSlot weapon)
    {
        GameObject visualPrefab;
        Vector3 visualRotation;
        Vector3 muzzlePosition;
        switch (weapon)
        {
            case WeaponSlot.Shotgun:
                visualPrefab = shotgunVisual;
                visualRotation = shotgunLocalEulerAngles;
                muzzlePosition = shotgunMuzzleLocalPosition;
                break;
            case WeaponSlot.Rifle:
                visualPrefab = rifleVisual;
                visualRotation = rifleLocalEulerAngles;
                muzzlePosition = rifleMuzzleLocalPosition;
                break;
            default:
                visualPrefab = handgunVisual;
                visualRotation = handgunLocalEulerAngles;
                muzzlePosition = muzzleLocalPosition;
                break;
        }

        // The firing origin belongs to the player and survives every model swap.
        if (muzzle != null)
            muzzle.localPosition = muzzlePosition;
        if (weaponVisual != null)
        {
            weaponVisual.gameObject.SetActive(false);
            Destroy(weaponVisual.gameObject);
            weaponVisual = null;
        }
        if (visualPrefab != null)
        {
            weaponVisual = Instantiate(visualPrefab, weaponMount, false).transform;
            weaponVisual.name = weapon.ToString();
            // The template weapon barrels point along +X; gameplay aiming uses +Z.
            weaponVisual.localRotation = Quaternion.Euler(visualRotation);
        }
    }

    private float CurrentShotInterval => currentWeapon == WeaponSlot.Shotgun ? shotgunShotInterval : currentWeapon == WeaponSlot.Rifle ? rifleShotInterval : shotInterval;
    private AudioClip CurrentShotSound => currentWeapon == WeaponSlot.Shotgun ? shotgunShotSound : currentWeapon == WeaponSlot.Rifle ? rifleShotSound : shotSound;

    private void OnDisable()
    {
        CancelReload();
        shotPending = false;
        trigger.Release();
        aimGuideHeld = false;
        if (aimGuide != null)
            aimGuide.enabled = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            OnDisable();
    }
}
