using UnityEngine;

// Three fading after-image puffs left behind by a dash.
public class DashTrail : MonoBehaviour
{
    private float life;
    private Material material;
    private Color color;

    public static Material Template;

    public static void Spawn(Vector3 position, Vector3 direction, Color color)
    {
        if (Template == null)
            return;
        for (int i = 0; i < 3; i++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(puff.GetComponent<Collider>());
            puff.name = "Dash Puff";
            puff.transform.position = position + Vector3.up * 0.9f + direction * (i * 0.9f);
            puff.transform.localScale = Vector3.one * (0.9f - i * 0.18f);
            var trail = puff.AddComponent<DashTrail>();
            trail.color = color;
            trail.material = new Material(Template);
            puff.GetComponent<Renderer>().sharedMaterial = trail.material;
            puff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.life = 0.35f + i * 0.08f;
        }
    }

    private void Update()
    {
        life -= Time.deltaTime;
        float alpha = Mathf.Clamp01(life / 0.35f);
        LabStyle.SetColor(material, LabPalette.WithAlpha(color, alpha * 0.6f));
        transform.localScale *= 1f + Time.deltaTime * 1.5f;
        if (life <= 0f)
        {
            Destroy(material);
            Destroy(gameObject);
        }
    }
}
