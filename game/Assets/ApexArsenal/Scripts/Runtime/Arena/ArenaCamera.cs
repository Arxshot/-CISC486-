using System.Collections.Generic;
using UnityEngine;

// Shared-screen 3/4 camera. Frames the whole chamber, then leans gently towards the living players
// and pulls in a little when they bunch up. Adds trauma-based screen shake.
[RequireComponent(typeof(Camera))]
public class ArenaCamera : MonoBehaviour
{
    [SerializeField, Range(30f, 80f)] private float pitch = 56f;
    [SerializeField] private float margin = 1.5f;
    [SerializeField, Range(0f, 1f)] private float followAmount = 0.25f;
    [SerializeField, Range(0f, 0.4f)] private float zoomInAmount = 0.12f;
    [SerializeField] private float smoothTime = 0.35f;

    private Camera cam;
    private Bounds arena;
    private readonly List<Transform> targets = new();
    private Vector3 focus;
    private Vector3 focusVelocity;
    private float distance;
    private float distanceVelocity;
    private float trauma;

    public static ArenaCamera Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        cam.fieldOfView = 35f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Frame(Bounds arenaBounds, bool snap = true)
    {
        arena = arenaBounds;
        focus = arena.center;
        distance = FitDistance(1f);
        if (snap)
            Apply(Vector3.zero);
    }

    public void SetTargets(IEnumerable<Transform> players)
    {
        targets.Clear();
        targets.AddRange(players);
    }

    public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    private void LateUpdate()
    {
        if (arena.size == Vector3.zero)
            return;
        Vector3 centroid = arena.center;
        float spread = 1f;
        int count = 0;
        var playerBounds = new Bounds();
        foreach (var target in targets)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;
            if (count == 0)
                playerBounds = new Bounds(target.position, Vector3.zero);
            else
                playerBounds.Encapsulate(target.position);
            count++;
        }
        if (count > 0)
        {
            centroid = Vector3.Lerp(arena.center, playerBounds.center, followAmount);
            centroid.y = arena.center.y;
            spread = Mathf.Clamp01(Mathf.Max(playerBounds.size.x / arena.size.x, playerBounds.size.z / arena.size.z) * 1.6f);
        }
        float zoom = Mathf.Lerp(1f - zoomInAmount, 1f, spread);
        focus = Vector3.SmoothDamp(focus, centroid, ref focusVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        distance = Mathf.SmoothDamp(distance, FitDistance(zoom), ref distanceVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 1.6f);
        float shake = trauma * trauma;
        float t = Time.unscaledTime * 25f;
        var offset = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.2f, t) - 0.5f, 0f) * (shake * 0.6f);
        Apply(offset);
    }

    private void Apply(Vector3 shakeOffset)
    {
        var rotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance + rotation * shakeOffset, rotation);
    }

    private float FitDistance(float zoom)
    {
        float vertical = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontal = Mathf.Atan(Mathf.Tan(vertical) * Mathf.Max(cam.aspect, 0.1f));
        float halfWidth = arena.extents.x + margin;
        // The floor's depth is foreshortened by the pitch; walls add a little height at the back.
        float halfDepth = arena.extents.z * Mathf.Sin(pitch * Mathf.Deg2Rad) + margin + 1.5f;
        float fit = Mathf.Max(halfWidth / Mathf.Tan(horizontal), halfDepth / Mathf.Tan(vertical));
        return fit * zoom;
    }
}
