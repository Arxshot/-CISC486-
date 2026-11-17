using UnityEngine;
using UnityEngine.InputSystem;

// Stand-in dash for Pierre's player (his scripts are untouched): a short burst through the
// CharacterController in the direction you're moving, with a cooldown and a cartoon squash.
// Keyboard: Shift or Space. Gamepad: B or left bumper. Reads only this player's paired devices.
[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerDash : MonoBehaviour
{
    [SerializeField] private float dashSpeed = 19f;
    [SerializeField] private float dashTime = 0.17f;
    [SerializeField] private float cooldown = 1.2f;

    private CharacterController controller;
    private PlayerInput input;
    private Transform cameraTransform;
    private Vector3 direction;
    private float dashTimer;
    private float cooldownTimer;
    private Transform model;

    public float CooldownFraction => cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(cooldownTimer / cooldown);
    public bool Unlimited { get; set; }
    public float SpeedMultiplier { get; set; } = 1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
        model = transform.Find("Character");
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || !input.inputIsActive)
            return;
        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            controller.Move(direction * (dashSpeed * SpeedMultiplier * Time.deltaTime));
            if (model != null)
                model.localScale = new Vector3(0.85f, 1.12f, 1.25f);
            if (dashTimer <= 0f && model != null)
                model.localScale = Vector3.one;
            return;
        }
        if (model != null && model.localScale != Vector3.one)
            model.localScale = Vector3.Lerp(model.localScale, Vector3.one, 1f - Mathf.Exp(-Time.deltaTime * 14f));

        if ((cooldownTimer <= 0f || Unlimited) && DashPressed())
            StartDash();
    }

    private void StartDash()
    {
        Vector2 stick = MoveInput();
        Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        direction = stick.sqrMagnitude > 0.04f ? (right * stick.x + forward * stick.y).normalized : transform.forward;
        direction.y = 0f;
        direction.Normalize();
        dashTimer = dashTime;
        cooldownTimer = cooldown;
        AudioDirector.PlayAt(Sfx.Dash, transform.position, 0.8f);
        DashTrail.Spawn(transform.position, direction, LabPalette.Player(input.playerIndex));
    }

    private bool DashPressed()
    {
        foreach (var device in input.devices)
        {
            if (device is Keyboard keyboard && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                return true;
            if (device is Gamepad gamepad && (gamepad.buttonEast.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame))
                return true;
        }
        return false;
    }

    private Vector2 MoveInput()
    {
        foreach (var device in input.devices)
        {
            if (device is Gamepad gamepad)
                return gamepad.leftStick.ReadValue();
            if (device is Keyboard keyboard)
            {
                var value = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                if (value != Vector2.zero)
                    return value;
            }
        }
        return Vector2.zero;
    }
}
