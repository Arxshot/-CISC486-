using UnityEngine;

public enum BotState
{
    Wander,  // No enemy known: roam between random reachable points.
    Pursue,  // Enemy known but far or out of sight: head for where it is going to be.
    Strafe,  // Enemy visible and in range: circle sideways while shooting.
    Hide,    // Hurt: move to a spot that cover hides from the threat, then wait.
    Peek,    // From cover: step sideways until the threat is visible, shoot, return.
}

// A laser-tag bot that hunts players and other bots. Steering uses direct seek/pursuit,
// corner detours around box cover and whisker casts, so it needs no baked NavMesh.
[RequireComponent(typeof(CharacterController), typeof(LocalCombatant), typeof(LocalBotWeapon))]
public class LocalBot : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
    [SerializeField, Range(0.1f, 1f)] private float wanderSpeedFactor = 0.55f;
    [SerializeField, Range(0.1f, 1f)] private float strafeSpeedFactor = 0.8f;
    [SerializeField, Min(1f)] private float acceleration = 20f;
    [SerializeField, Min(1f)] private float turnSpeed = 540f;
    [SerializeField] private float gravity = -20f;

    [Header("Senses")]
    [SerializeField, Min(1f)] private float sightRange = 20f;
    [SerializeField, Range(10f, 360f)] private float fieldOfView = 160f;
    [Tooltip("Enemies this close are noticed even when behind the bot.")]
    [SerializeField, Min(0f)] private float awarenessRadius = 3f;
    [SerializeField, Min(0f)] private float memoryDuration = 4f;
    [Tooltip("How long an enemy must be visible before the first shot.")]
    [SerializeField, Min(0f)] private float reactionTime = 0.35f;
    [SerializeField, Min(0.02f)] private float thinkInterval = 0.1f;

    [Header("Combat")]
    [SerializeField, Min(1f)] private float engageRange = 9f;
    [SerializeField, Min(1f)] private float preferredRange = 6f;
    [SerializeField] private Vector2 strafeSwitchInterval = new Vector2(0.7f, 1.8f);
    [SerializeField, Range(1f, 45f)] private float fireAngle = 12f;
    [Tooltip("Random aim error, in degrees, so bots are beatable.")]
    [SerializeField, Range(0f, 10f)] private float aimError = 1.5f;
    [SerializeField, Min(0f)] private float maxPredictionTime = 1f;

    [Header("Cover")]
    [Tooltip("At or below this fraction of max health the bot keeps to cover, peeking to shoot.")]
    [SerializeField, Range(0f, 1f)] private float retreatHealthFraction = 0.25f;
    [Tooltip("Chance that a hit sends a healthy bot to cover for one peek.")]
    [SerializeField, Range(0f, 1f)] private float hideOnHitChance = 0.5f;
    [SerializeField] private Vector2 hideDuration = new Vector2(0.8f, 1.6f);
    [SerializeField, Min(0.2f)] private float peekDuration = 1.5f;
    [SerializeField, Min(1f)] private float coverSearchRadius = 10f;
    [Tooltip("Obstacles at least this tall hide a standing character.")]
    [SerializeField, Min(0.5f)] private float minCoverHeight = 1.6f;

    [Header("Obstacle avoidance")]
    [SerializeField] private LayerMask obstacleMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.1f)] private float whiskerLength = 1.2f;
    [SerializeField, Range(5f, 80f)] private float whiskerAngle = 35f;

    private static readonly float[] PeekOffsets = { 0.6f, 1f, 1.4f, 1.8f, 2.2f };
    private readonly RaycastHit[] rayHits = new RaycastHit[16];
    private readonly Collider[] coverCandidates = new Collider[64];
    private readonly Collider[] overlaps = new Collider[16];

    private CharacterController controller;
    private LocalCombatant self;
    private LocalHealth health;
    private LocalBotWeapon weapon;

    private BotState state;
    private LocalCombatant target;
    private bool targetVisible;
    private float visibleSince;
    private float lastSeenTime = float.NegativeInfinity;
    private Vector3 lastSeenPosition;
    private Vector3 lastSeenVelocity;
    private float nextThinkTime;

    private Vector3 velocity;
    private float verticalSpeed;
    private float stuckTimer;
    private Vector3 unstickDirection;
    private float unstickUntil;

    private Vector3 wanderPoint;
    private bool hasWanderPoint;
    private float wanderTimer;
    private float strafeSign = 1f;
    private float nextStrafeSwitch;

    private Vector3 coverSpot;
    private bool atCover;
    private float hideTimer;
    private float nextCoverCheck;
    private int coverCyclesRemaining;
    private Vector3 peekSpot;
    private float peekSide = 1f;
    private float peekTimer;
    private float peekShotTime = float.NegativeInfinity;

    public BotState State => state;
    public LocalCombatant Target => target;
    public bool TargetVisible => targetVisible;
    public bool IsInCover => state == BotState.Hide && atCover;
    public Vector3 CoverSpot => coverSpot;

    private Vector3 Eye => transform.position + Vector3.up * LocalCombatant.AimHeight;
    private bool LowHealth => health.CurrentHealth <= health.MaxHealth * retreatHealthFraction;
    private Vector3 ThreatPosition => targetVisible ? target.transform.position : lastSeenPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        self = GetComponent<LocalCombatant>();
        health = GetComponent<LocalHealth>();
        weapon = GetComponent<LocalBotWeapon>();
        strafeSign = Random.value < 0.5f ? -1f : 1f;
    }

    private void OnEnable()
    {
        health.Damaged += HandleDamaged;
        ForgetTarget();
        EnterWander();
    }

    private void OnDisable()
    {
        health.Damaged -= HandleDamaged;
        velocity = Vector3.zero;
        verticalSpeed = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;
        if (target == null || !target.isActiveAndEnabled)
        {
            if (target != null || state != BotState.Wander)
            {
                ForgetTarget();
                EnterWander();
            }
        }
        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + thinkInterval;
            Perceive();
            Decide();
        }

        Vector3 move;
        float speed;
        Vector3 facing;
        switch (state)
        {
            case BotState.Pursue:
                ActPursue(out move, out speed, out facing);
                break;
            case BotState.Strafe:
                ActStrafe(out move, out speed, out facing);
                break;
            case BotState.Hide:
                ActHide(dt, out move, out speed, out facing);
                break;
            case BotState.Peek:
                ActPeek(dt, out move, out speed, out facing);
                break;
            default:
                ActWander(dt, out move, out speed, out facing);
                break;
        }

        Move(move, speed, dt);
        Face(facing, dt);
        AimAndShoot();
    }

    // ---- Senses -------------------------------------------------------------------------------

    private void Perceive()
    {
        LocalCombatant best = null;
        float bestScore = float.PositiveInfinity;
        bool tracking = target != null && Time.time - lastSeenTime < 0.5f;
        foreach (var other in LocalCombatant.All)
        {
            if (!self.IsHostileTo(other))
                continue;
            Vector3 to = Flat(other.transform.position - transform.position);
            float distance = to.magnitude;
            if (distance > sightRange)
                continue;
            bool inView = distance <= awarenessRadius || Vector3.Angle(transform.forward, to) <= fieldOfView * 0.5f;
            // Keep tracking a target that was just seen, even if it slipped out of the view cone.
            if (!inView && !(tracking && other == target))
                continue;
            if (!HasLineOfSight(Eye, other.AimPoint, other.transform))
                continue;
            float score = distance - (other == target ? 2f : 0f); // Prefer the current target, then the nearest.
            if (score < bestScore)
            {
                best = other;
                bestScore = score;
            }
        }

        if (best != null)
        {
            if (best != target || !targetVisible)
                visibleSince = Time.time;
            target = best;
            targetVisible = true;
            lastSeenPosition = best.transform.position;
            lastSeenVelocity = Flat(best.Velocity);
            lastSeenTime = Time.time;
            return;
        }

        targetVisible = false;
        // A bot sheltering from a threat remembers it for longer.
        float memory = state == BotState.Hide || state == BotState.Peek ? memoryDuration * 3f : memoryDuration;
        if (target != null && Time.time - lastSeenTime > memory)
            ForgetTarget();
    }

    private void HandleDamaged(LocalHealth _, float damage, GameObject source)
    {
        var attacker = source != null ? source.GetComponentInParent<LocalCombatant>() : null;
        if (attacker != null && self.IsHostileTo(attacker) && (target == null || !targetVisible))
        {
            // Turn on whoever shot us, even from outside the view cone.
            target = attacker;
            lastSeenPosition = attacker.transform.position;
            lastSeenVelocity = Flat(attacker.Velocity);
            lastSeenTime = Time.time;
        }
        if (state != BotState.Hide && state != BotState.Peek && coverCyclesRemaining == 0 && Random.value < hideOnHitChance)
            coverCyclesRemaining = 1;
        nextThinkTime = 0f; // React this frame.
    }

    private void ForgetTarget()
    {
        target = null;
        targetVisible = false;
        lastSeenTime = float.NegativeInfinity;
        coverCyclesRemaining = 0;
    }

    // ---- Decisions ----------------------------------------------------------------------------

    private void Decide()
    {
        if (target == null)
        {
            if (state != BotState.Wander)
                EnterWander();
            return;
        }

        if (LowHealth || coverCyclesRemaining > 0)
        {
            if (state == BotState.Hide || state == BotState.Peek)
                return; // The cover states hand control back themselves.
            if (TryFindCover(ThreatPosition, out Vector3 spot))
            {
                EnterHide(spot);
                return;
            }
            coverCyclesRemaining = 0; // Nowhere to hide: fight it out.
        }

        float distance = Flat(ThreatPosition - transform.position).magnitude;
        if (targetVisible && distance <= engageRange)
        {
            if (state != BotState.Strafe)
                EnterStrafe();
        }
        else if (state != BotState.Pursue)
            state = BotState.Pursue;
    }

    private void EnterWander()
    {
        state = BotState.Wander;
        hasWanderPoint = false;
        atCover = false;
    }

    private void EnterStrafe()
    {
        state = BotState.Strafe;
        atCover = false;
        ScheduleStrafeSwitch();
    }

    private void EnterHide(Vector3 spot)
    {
        state = BotState.Hide;
        coverSpot = spot;
        atCover = false;
        hideTimer = Random.Range(hideDuration.x, hideDuration.y);
    }

    private void EnterPeek(Vector3 spot)
    {
        state = BotState.Peek;
        peekSpot = spot;
        peekTimer = peekDuration;
        peekShotTime = float.NegativeInfinity;
        atCover = false;
    }

    // ---- States -------------------------------------------------------------------------------

    private void ActWander(float dt, out Vector3 move, out float speed, out Vector3 facing)
    {
        wanderTimer -= dt;
        if (!hasWanderPoint || wanderTimer <= 0f || Arrived(wanderPoint, 0.5f))
            PickWanderPoint();
        move = SteerTowards(wanderPoint);
        speed = moveSpeed * wanderSpeedFactor;
        facing = move;
    }

    private void ActPursue(out Vector3 move, out float speed, out Vector3 facing)
    {
        Vector3 targetPosition = ThreatPosition;
        float distance = Flat(targetPosition - transform.position).magnitude;
        // Pursuit: aim for where the target will be by the time we get there, not where it is.
        float lookAhead = Mathf.Min(distance / moveSpeed, maxPredictionTime);
        if (!targetVisible)
            lookAhead = Mathf.Min(lookAhead + Time.time - lastSeenTime, maxPredictionTime * 1.5f);
        Vector3 predicted = targetPosition + (targetVisible ? Flat(target.Velocity) : lastSeenVelocity) * lookAhead;
        if (!IsStandable(predicted))
            predicted = targetPosition;

        if (!targetVisible && Arrived(predicted, 0.8f))
        {
            // Reached the last known position without finding anyone.
            ForgetTarget();
            EnterWander();
            move = Vector3.zero;
            speed = 0f;
            facing = transform.forward;
            return;
        }
        move = SteerTowards(predicted);
        speed = moveSpeed;
        facing = targetVisible ? Flat(target.transform.position - transform.position) : move;
    }

    private void ActStrafe(out Vector3 move, out float speed, out Vector3 facing)
    {
        Vector3 toTarget = Flat(target.transform.position - transform.position);
        float distance = Mathf.Max(toTarget.magnitude, 0.01f);
        toTarget /= distance;
        if (Time.time >= nextStrafeSwitch)
        {
            strafeSign = -strafeSign;
            ScheduleStrafeSwitch();
        }
        Vector3 sideways = Vector3.Cross(Vector3.up, toTarget) * strafeSign;
        if (IsBlocked(sideways, 1f))
        {
            strafeSign = -strafeSign;
            sideways = -sideways;
            ScheduleStrafeSwitch();
        }
        // Circle the target while drifting back toward the preferred range.
        float radial = Mathf.Clamp((distance - preferredRange) / 2f, -1f, 1f);
        move = AvoidObstacles((sideways + toTarget * radial).normalized);
        speed = moveSpeed * strafeSpeedFactor;
        facing = toTarget;
    }

    private void ActHide(float dt, out Vector3 move, out float speed, out Vector3 facing)
    {
        Vector3 threat = ThreatPosition;
        facing = Flat(threat - transform.position);
        if (Time.time >= nextCoverCheck)
        {
            nextCoverCheck = Time.time + 0.25f;
            // The threat may have walked around the cover; find a new spot if this one is exposed.
            if (!IsConcealed(coverSpot, threat))
            {
                if (TryFindCover(threat, out Vector3 spot))
                    EnterHide(spot);
                else
                {
                    coverCyclesRemaining = 0;
                    state = BotState.Pursue;
                }
            }
        }

        if (!Arrived(coverSpot, 0.3f))
        {
            atCover = false;
            move = SteerTowards(coverSpot);
            speed = moveSpeed;
            if (!targetVisible)
                facing = move;
            return;
        }

        atCover = true;
        move = Vector3.zero;
        speed = 0f;
        hideTimer -= dt;
        if (hideTimer > 0f)
            return;
        if (TryFindPeekSpot(threat, out Vector3 peek))
            EnterPeek(peek);
        else
            hideTimer = 0.5f; // No angle on the threat from here yet; check again shortly.
    }

    private void ActPeek(float dt, out Vector3 move, out float speed, out Vector3 facing)
    {
        peekTimer -= dt;
        facing = Flat(ThreatPosition - transform.position);
        // Duck back shortly after the shot, or when nothing came into view in time.
        bool shotFired = peekShotTime > float.NegativeInfinity;
        if (peekTimer <= 0f || (shotFired && Time.time - peekShotTime > 0.15f))
        {
            EndPeek();
            move = Vector3.zero;
            speed = 0f;
            return;
        }
        move = Arrived(peekSpot, 0.15f) ? Vector3.zero : SteerTowards(peekSpot, avoid: false);
        speed = moveSpeed;
    }

    private void EndPeek()
    {
        peekSide = -peekSide; // Vary the side, so the next peek is harder to pre-aim.
        if (!LowHealth && coverCyclesRemaining > 0)
            coverCyclesRemaining--;
        if (LowHealth || coverCyclesRemaining > 0)
            EnterHide(coverSpot);
        else
            state = BotState.Pursue; // Decide picks Strafe or Pursue on the next think.
    }

    private void AimAndShoot()
    {
        if (target == null || !targetVisible || state == BotState.Hide || state == BotState.Wander)
        {
            weapon.ClearAim();
            return;
        }
        weapon.AimAt(target.AimPoint);
        if (Time.time - visibleSince < reactionTime || !weapon.IsReady)
            return;
        Vector3 toTarget = Flat(target.transform.position - transform.position);
        float distance = toTarget.magnitude;
        if (distance > weapon.Range || Vector3.Angle(transform.forward, toTarget) > fireAngle)
            return;
        // Don't waste shots into cover the target has stepped behind since the last think.
        if (!HasLineOfSight(weapon.MuzzlePosition, target.AimPoint, target.transform))
            return;
        Vector3 aimPoint = target.AimPoint + Random.insideUnitSphere * (distance * Mathf.Tan(aimError * Mathf.Deg2Rad));
        if (weapon.TryFire(aimPoint) && state == BotState.Peek)
            peekShotTime = Time.time;
    }

    // ---- Cover --------------------------------------------------------------------------------

    private bool TryFindCover(Vector3 threat, out Vector3 bestSpot)
    {
        bestSpot = default;
        float bestCost = float.PositiveInfinity;
        Vector3 toThreat = Flat(threat - transform.position).normalized;
        float margin = controller.radius + 0.25f;
        int count = Physics.OverlapSphereNonAlloc(transform.position, coverSearchRadius, coverCandidates, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider cover = coverCandidates[i];
            if (!IsCover(cover))
                continue;
            Bounds bounds = cover.bounds;
            Vector3 center = new Vector3(bounds.center.x, transform.position.y, bounds.center.z);
            float halfX = bounds.extents.x + margin;
            float halfZ = bounds.extents.z + margin;
            // Eight spots around the box: its four faces and four corners.
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 45f * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float scale = Mathf.Min(
                    Mathf.Abs(direction.x) > 0.001f ? halfX / Mathf.Abs(direction.x) : float.PositiveInfinity,
                    Mathf.Abs(direction.z) > 0.001f ? halfZ / Mathf.Abs(direction.z) : float.PositiveInfinity);
                Vector3 spot = center + direction * scale;
                if (Flat(spot - threat).magnitude < 2.5f || !IsConcealed(spot, threat) || !IsStandable(spot))
                    continue;
                Vector3 toSpot = Flat(spot - transform.position);
                // Prefer nearby spots, and avoid running at the threat to reach one.
                float cost = toSpot.magnitude + (Vector3.Dot(toSpot.normalized, toThreat) > 0.5f ? 3f : 0f);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSpot = spot;
                }
            }
        }
        return bestCost < float.PositiveInfinity;
    }

    private bool TryFindPeekSpot(Vector3 threat, out Vector3 spot)
    {
        Vector3 lateral = Vector3.Cross(Vector3.up, Flat(threat - coverSpot).normalized);
        Vector3 threatAim = threat + Vector3.up * LocalCombatant.AimHeight;
        foreach (float side in new[] { peekSide, -peekSide })
            foreach (float offset in PeekOffsets)
            {
                Vector3 candidate = coverSpot + lateral * (side * offset);
                if (!IsStandable(candidate))
                    break; // Something solid that way; try the other side.
                // The bot must see the threat (eye at its centre) and shoot it: facing the threat,
                // the gun sits ~0.35 m to the bot's right.
                Transform ignore = target != null ? target.transform : null;
                Vector3 eye = candidate + Vector3.up * LocalCombatant.AimHeight;
                if (HasLineOfSight(eye, threatAim, ignore) && HasLineOfSight(eye + lateral * 0.35f, threatAim, ignore))
                {
                    spot = candidate;
                    peekSide = side;
                    return true;
                }
            }
        spot = default;
        return false;
    }

    private bool IsCover(Collider candidate)
    {
        if (candidate.transform.IsChildOf(transform) || candidate is CharacterController)
            return false;
        // Characters and destructible practice targets are not reliable cover.
        if (candidate.GetComponentInParent<LocalHealth>() != null)
            return false;
        Bounds bounds = candidate.bounds;
        return bounds.size.y >= minCoverHeight
            && bounds.min.y <= transform.position.y + 0.5f
            && Mathf.Max(bounds.size.x, bounds.size.z) <= 6f;
    }

    private bool IsConcealed(Vector3 spot, Vector3 threat) =>
        !HasLineOfSight(threat + Vector3.up * LocalCombatant.AimHeight, spot + Vector3.up * LocalCombatant.AimHeight,
            target != null ? target.transform : null);

    // ---- Steering -----------------------------------------------------------------------------

    private void PickWanderPoint()
    {
        hasWanderPoint = true;
        wanderTimer = 4f;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            // Bias toward the current heading so wandering looks purposeful rather than jittery.
            float angle = Random.Range(-120f, 120f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Flat(transform.forward).normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.forward;
            Vector3 point = transform.position + direction * Random.Range(2.5f, 7f);
            if (IsStandable(point))
            {
                wanderPoint = point;
                return;
            }
        }
        wanderPoint = transform.position - Flat(transform.forward).normalized * 1.5f; // Turn back.
    }

    private Vector3 SteerTowards(Vector3 destination, bool avoid = true)
    {
        Vector3 to = Flat(destination - transform.position);
        float distance = to.magnitude;
        if (distance < 0.05f)
            return Vector3.zero;
        Vector3 direction = Detour(to / distance, distance, destination);
        if (avoid && distance > 1f)
            direction = AvoidObstacles(direction);
        return direction * Mathf.Clamp01(distance / 0.5f); // Slow down on arrival.
    }

    // If an obstacle blocks the straight path, head for its nearest reachable corner instead.
    private Vector3 Detour(Vector3 direction, float distance, Vector3 destination)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (!Physics.SphereCast(origin, 0.3f, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return direction;
        if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<LocalCombatant>() != null)
            return direction; // Moving characters are left to the whiskers.
        Bounds bounds = hit.collider.bounds;
        if (bounds.size.x > 8f || bounds.size.z > 8f)
            return direction;

        float margin = controller.radius + 0.45f;
        Vector3 best = default;
        float bestCost = float.PositiveInfinity;
        foreach (float x in new[] { bounds.min.x - margin, bounds.max.x + margin })
            foreach (float z in new[] { bounds.min.z - margin, bounds.max.z + margin })
            {
                var corner = new Vector3(x, transform.position.y, z);
                Vector3 toCorner = Flat(corner - transform.position);
                float cornerDistance = toCorner.magnitude;
                if (cornerDistance < 0.2f || Physics.SphereCast(origin, 0.3f, toCorner / cornerDistance, out _, cornerDistance, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
                float cost = cornerDistance + Flat(destination - corner).magnitude;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = toCorner / cornerDistance;
                }
            }
        return bestCost < float.PositiveInfinity ? best : direction;
    }

    // Whiskers: a centre cast pushes away from what is ahead, side casts steer away from that side.
    private Vector3 AvoidObstacles(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return direction;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 avoidance = Vector3.zero;
        foreach (float angle in new[] { 0f, whiskerAngle, -whiskerAngle })
        {
            Vector3 whisker = Quaternion.Euler(0f, angle, 0f) * direction;
            float length = angle == 0f ? whiskerLength : whiskerLength * 0.7f;
            if (!Physics.SphereCast(origin, 0.25f, whisker, out RaycastHit hit, length, obstacleMask, QueryTriggerInteraction.Ignore)
                || hit.collider.transform.IsChildOf(transform))
                continue;
            float strength = 1f - hit.distance / length;
            if (angle == 0f)
                avoidance += Flat(hit.normal).normalized * (strength * 1.5f);
            else
                avoidance += Quaternion.Euler(0f, -Mathf.Sign(angle) * 90f, 0f) * direction * strength;
        }
        Vector3 result = Flat(direction + avoidance);
        return result.sqrMagnitude > 0.0001f ? result.normalized : direction;
    }

    private void Move(Vector3 direction, float speed, float dt)
    {
        Vector3 desired = Time.time < unstickUntil ? unstickDirection * moveSpeed * 0.7f : direction * speed;
        velocity = Vector3.MoveTowards(velocity, desired, acceleration * dt);
        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        verticalSpeed += gravity * dt;
        controller.Move((velocity + Vector3.up * verticalSpeed) * dt);

        // If we are pushing but not moving, sidestep briefly and re-plan.
        float actual = Flat(controller.velocity).magnitude;
        stuckTimer = desired.sqrMagnitude > 1f && actual < desired.magnitude * 0.25f ? stuckTimer + dt : 0f;
        if (stuckTimer > 0.6f)
        {
            stuckTimer = 0f;
            hasWanderPoint = false;
            strafeSign = -strafeSign;
            Vector3 heading = desired.sqrMagnitude > 0.0001f ? desired.normalized : transform.forward;
            unstickDirection = Quaternion.Euler(0f, Random.value < 0.5f ? 90f : -90f, 0f) * heading;
            unstickUntil = Time.time + 0.4f;
        }
    }

    private void Face(Vector3 direction, float dt)
    {
        direction = Flat(direction);
        if (direction.sqrMagnitude < 0.0001f)
            return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), turnSpeed * dt);
    }

    // ---- Queries ------------------------------------------------------------------------------

    private bool HasLineOfSight(Vector3 from, Vector3 to, Transform ignore)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return true;
        int count = Physics.RaycastNonAlloc(from, delta / distance, rayHits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = rayHits[i].collider.transform;
            if (hit.IsChildOf(transform) || ignore != null && hit.IsChildOf(ignore))
                continue;
            return false;
        }
        return true;
    }

    // A spot is standable when there is floor under it and room for our capsule.
    private bool IsStandable(Vector3 point)
    {
        if (!Physics.Raycast(point + Vector3.up * 0.6f, Vector3.down, out RaycastHit floor, 1.5f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
            || Mathf.Abs(floor.point.y - point.y) > 0.5f)
            return false;
        float radius = controller.radius;
        Vector3 bottom = point + Vector3.up * (radius + 0.1f);
        Vector3 top = point + Vector3.up * Mathf.Max(radius + 0.1f, controller.height - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!overlaps[i].transform.IsChildOf(transform))
                return false;
        return true;
    }

    private bool IsBlocked(Vector3 direction, float distance) =>
        Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore)
        && !hit.collider.transform.IsChildOf(transform);

    private bool Arrived(Vector3 point, float tolerance) => Flat(point - transform.position).sqrMagnitude <= tolerance * tolerance;

    private void ScheduleStrafeSwitch() => nextStrafeSwitch = Time.time + Random.Range(strafeSwitchInterval.x, strafeSwitchInterval.y);

    private static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);
}
