using System.Collections.Generic;
using UnityEngine;

// Marks a player or bot that bots may hunt. Practice targets have health but are not combatants.
[RequireComponent(typeof(LocalHealth))]
public class LocalCombatant : MonoBehaviour
{
    // Chest height used for line-of-sight checks and aiming, matching the gun's height.
    public const float AimHeight = 1.2f;

    private static readonly List<LocalCombatant> active = new List<LocalCombatant>();
    public static IReadOnlyList<LocalCombatant> All => active;

    private CharacterController controller;
    private Vector3 lastPosition;
    private Vector3 trackedVelocity;

    public LocalHealth Health { get; private set; }
    public Vector3 AimPoint => transform.position + Vector3.up * AimHeight;
    public Vector3 Velocity => controller != null && controller.enabled ? controller.velocity : trackedVelocity;

    private void Awake()
    {
        Health = GetComponent<LocalHealth>();
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        trackedVelocity = Vector3.zero;
        active.Add(this);
    }

    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        // Objects moved without a CharacterController still report a velocity for pursuit prediction.
        if (Time.deltaTime > 0f)
            trackedVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    // Free-for-all: everyone else is an enemy.
    public bool IsHostileTo(LocalCombatant other) => other != null && other != this;
}
