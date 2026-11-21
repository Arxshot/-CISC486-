using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Cartoon upgrade for Pierre's player, added by my prefab variant without changing his files:
// ink outline around the character, a player-coloured floor ring, a blob shadow, a "P1" tag with an
// overhead health bar, a flash when hit and a "poof" when eliminated.
// Also dresses the lab's AI bots (no PlayerInput): call Configure before the first frame.
public class PlayerCosmetics : MonoBehaviour
{
    private Color? customColor;
    private string customTag;

    private PlayerInput input;
    private LocalHealth health;
    private Transform overhead;
    private Transform healthFill;
    private Transform ring;
    private Material outlineMaterial;
    private TextMeshPro tagLabel;
    private float maxHealth;
    private float lastHealth;
    private float flash;
    private bool quitting;

    public Color Color => customColor ?? LabPalette.Player(input != null ? input.playerIndex : 0);

    public void Configure(Color color, string tag)
    {
        customColor = color;
        customTag = tag;
    }

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        health = GetComponent<LocalHealth>();
    }

    private void Start()
    {
        var style = LabStyle.Active;
        if (style == null)
            return;
        Color color = Color;

        // Outline: duplicate each skinned mesh with the same bones and an inverted-hull material.
        outlineMaterial = new Material(style.outline);
        foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (skinned.name.EndsWith("Outline"))
                continue;
            var copy = new GameObject(skinned.name + " Outline").AddComponent<SkinnedMeshRenderer>();
            copy.transform.SetParent(skinned.transform.parent, false);
            copy.transform.SetLocalPositionAndRotation(skinned.transform.localPosition, skinned.transform.localRotation);
            copy.transform.localScale = skinned.transform.localScale;
            copy.sharedMesh = skinned.sharedMesh;
            copy.bones = skinned.bones;
            copy.rootBone = skinned.rootBone;
            copy.localBounds = skinned.localBounds;
            copy.updateWhenOffscreen = true;
            copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var materials = new Material[Mathf.Max(1, skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = outlineMaterial;
            copy.sharedMaterials = materials;
        }

        ring = FlatQuad("Player Ring", style.Tinted(style.playerRing, color), 1.5f, 0.03f).transform;
        FlatQuad("Blob Shadow", style.shadowBlob, 1.3f, 0.02f);

        overhead = new GameObject("Overhead").transform;
        overhead.SetParent(transform, false);
        overhead.localPosition = new Vector3(0f, 2.35f, 0f);

        tagLabel = new GameObject("Tag").AddComponent<TextMeshPro>();
        tagLabel.transform.SetParent(overhead, false);
        tagLabel.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        if (style.font != null)
            tagLabel.font = style.font;
        tagLabel.text = customTag ?? $"P{(input != null ? input.playerIndex : 0) + 1}";
        tagLabel.fontSize = 5f;
        tagLabel.fontStyle = FontStyles.Bold;
        tagLabel.alignment = TextAlignmentOptions.Center;
        tagLabel.color = color;
        tagLabel.outlineWidth = 0.3f;
        tagLabel.outlineColor = LabPalette.Ink;
        tagLabel.rectTransform.sizeDelta = new Vector2(3f, 1f);

        if (health != null)
        {
            maxHealth = lastHealth = Mathf.Max(1f, health.CurrentHealth);
            Bar("Health Back", style.ink, new Vector3(1.24f, 0.2f, 1f), 0.002f);
            healthFill = Bar("Health Fill", style.Tinted(style.neonBlue, color), new Vector3(1.1f, 0.1f, 1f), 0f).transform;
        }
    }

    private GameObject FlatQuad(string name, Material material, float size, float height)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = name;
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = new Vector3(0f, height, 0f);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(size, size, 1f);
        var renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad;
    }

    private GameObject Bar(string name, Material material, Vector3 scale, float depth)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(bar.GetComponent<Collider>());
        bar.name = name;
        bar.transform.SetParent(overhead, false);
        bar.transform.localPosition = new Vector3(0f, 0f, depth);
        bar.transform.localScale = scale;
        var renderer = bar.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return bar;
    }

    private void LateUpdate()
    {
        if (overhead != null && Camera.main != null)
            overhead.rotation = Camera.main.transform.rotation;

        if (health != null && healthFill != null)
        {
            float current = health.CurrentHealth;
            if (current < lastHealth)
            {
                flash = 1f;
                // Only players getting hurt shakes the camera; hitting a bot shouldn't.
                if (input != null)
                {
                    AudioDirector.PlayAt(Sfx.GateWarn, transform.position, 0.5f);
                    if (ArenaCamera.Instance != null)
                        ArenaCamera.Instance.Shake(0.25f);
                }
            }
            lastHealth = current;
            float fraction = Mathf.Clamp01(current / maxHealth);
            healthFill.localScale = new Vector3(1.1f * fraction, 0.1f, 1f);
            healthFill.localPosition = new Vector3(-0.55f * (1f - fraction), 0f, 0f);
        }

        flash = Mathf.Max(0f, flash - Time.deltaTime * 6f);
        if (outlineMaterial != null)
            outlineMaterial.SetColor("_OutlineColor", Color.Lerp(LabPalette.Ink, Color.white, flash));
        if (ring != null)
        {
            float pulse = 1.5f + Mathf.Sin(Time.time * 4f) * 0.06f + flash * 0.4f;
            ring.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDisable()
    {
        // LocalHealth deactivates the player at 0 HP: celebrate the elimination with a poof.
        if (quitting || health == null || health.CurrentHealth > 0f || !gameObject.scene.isLoaded)
            return;
        Poof.Spawn(transform.position + Vector3.up, Color);
        AudioDirector.PlayAt(Sfx.Eliminated, transform.position);
        if (ArenaCamera.Instance != null)
            ArenaCamera.Instance.Shake(input != null ? 0.6f : 0.35f);
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }
}
