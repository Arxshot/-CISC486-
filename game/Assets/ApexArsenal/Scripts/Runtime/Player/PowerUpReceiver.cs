using UnityEngine;

public enum PowerUp
{
    SpeedSurge,
    InfiniteDash,
}

// Timed crate power-ups that work without touching Pierre's scripts.
// Speed Surge adds extra movement on top of PlayerMovement; Infinite Dash removes the dash cooldown.
// (Pierre can add more, e.g. heal or rapid fire, by listening to Crate.Collected.)
[RequireComponent(typeof(CharacterController))]
public class PowerUpReceiver : MonoBehaviour
{
    [SerializeField] private float duration = 8f;
    [SerializeField] private float surgeBonus = 0.55f; // fraction of normal speed added

    private CharacterController controller;
    private PlayerDash dash;
    private Vector3 lastPosition;
    private Vector3 lastEcho;

    public PowerUp? Active { get; private set; }
    public float Remaining { get; private set; }
    public float RemainingFraction => duration <= 0f ? 0f : Remaining / duration;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        dash = GetComponent<PlayerDash>();
        lastPosition = transform.position;
    }

    public void Grant(PowerUp powerUp)
    {
        Active = powerUp;
        Remaining = duration;
        if (dash != null)
        {
            dash.Unlimited = powerUp == PowerUp.InfiniteDash;
            dash.SpeedMultiplier = powerUp == PowerUp.SpeedSurge ? 1.25f : 1f;
        }
    }

    private void Update()
    {
        // Movement since last frame, minus our own extra push, is what PlayerMovement did.
        Vector3 moved = transform.position - lastPosition - lastEcho;
        lastEcho = Vector3.zero;
        if (Active.HasValue && Time.deltaTime > 0f)
        {
            Remaining -= Time.deltaTime;
            if (Active == PowerUp.SpeedSurge && controller.enabled)
            {
                // Echo a share of that horizontal movement to make the player faster.
                moved.y = 0f;
                if (moved.sqrMagnitude < 1f)
                {
                    lastEcho = moved * surgeBonus;
                    controller.Move(lastEcho);
                }
            }
            if (Remaining <= 0f)
            {
                Active = null;
                if (dash != null)
                {
                    dash.Unlimited = false;
                    dash.SpeedMultiplier = 1f;
                }
            }
        }
        lastPosition = transform.position;
    }
}
