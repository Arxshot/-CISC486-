using UnityEngine;

// Small cartoon motions for UI, all on unscaled time so they run in pause menus too.
public class UIMotion : MonoBehaviour
{
    public enum Kind
    {
        PopIn, // overshoots into place whenever enabled
        Bob, // floats up and down
        Pulse, // breathes in scale
        Spin,
    }

    public Kind kind = Kind.PopIn;
    public float amount = 1f;
    public float speed = 1f;
    public float delay;

    private RectTransform rect;
    private Vector2 basePosition;
    private float startTime;
    private bool hasBase;
    private bool settled;

    private void Awake() => rect = transform as RectTransform;

    private void OnEnable()
    {
        startTime = Time.unscaledTime + delay;
        settled = false;
        if (rect != null && !hasBase)
        {
            basePosition = rect.anchoredPosition;
            hasBase = true;
        }
        if (kind == Kind.PopIn)
            transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        float t = Time.unscaledTime - startTime;
        switch (kind)
        {
            case Kind.PopIn:
                UpdatePopIn(t);
                break;
            case Kind.Bob:
                if (rect != null)
                    rect.anchoredPosition = basePosition + Vector2.up * (Mathf.Sin(t * 2f * speed) * 10f * amount);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f * speed) * 1.5f * amount);
                break;
            case Kind.Pulse:
                transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 4f * speed) * 0.04f * amount);
                break;
            case Kind.Spin:
                transform.localRotation = Quaternion.Euler(0f, 0f, -t * 90f * speed);
                break;
        }
    }

    private void UpdatePopIn(float t)
    {
        if (t < 0f)
        {
            transform.localScale = Vector3.zero;
            return;
        }
        if (settled)
            return;
        float k = Mathf.Clamp01(t * 4f * speed);
        // Back-out easing: overshoot then settle.
        const float s = 2.2f;
        float eased = 1f + (s + 1f) * Mathf.Pow(k - 1f, 3f) + s * Mathf.Pow(k - 1f, 2f);
        transform.localScale = Vector3.one * (k < 1f ? eased : 1f);
        settled = k >= 1f;
    }
}
