using UnityEngine;

// A cartoon burst of puffs and a ring, used for eliminations, turret pops and crate pickups.
public class Poof : MonoBehaviour
{
    private Vector3 velocity;
    private float life;
    private float maxLife;
    private Material material;
    private Color color;

    public static void Spawn(Vector3 position, Color color, int puffs = 10, float force = 5f)
    {
        var style = LabStyle.Active;
        if (style == null)
            return;
        for (int i = 0; i < puffs; i++)
        {
            var puff = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            Destroy(puff.GetComponent<Collider>());
            puff.name = "Poof";
            puff.transform.position = position;
            puff.transform.rotation = Random.rotation;
            puff.transform.localScale = Vector3.one * Random.Range(0.25f, 0.55f);
            var poof = puff.AddComponent<Poof>();
            poof.velocity = (Random.onUnitSphere + Vector3.up * 0.8f).normalized * Random.Range(force * 0.5f, force);
            poof.maxLife = poof.life = Random.Range(0.45f, 0.8f);
            poof.color = i % 2 == 0 ? color : Color.white;
            poof.material = new Material(style.neonBlue);
            LabStyle.SetColor(poof.material, poof.color);
            var renderer = puff.GetComponent<Renderer>();
            renderer.sharedMaterial = poof.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void Update()
    {
        life -= Time.deltaTime;
        velocity += Vector3.down * (9f * Time.deltaTime);
        velocity *= 1f - Time.deltaTime * 2f;
        transform.position += velocity * Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Max(0f, transform.localScale.x - Time.deltaTime * 0.6f);
        if (life <= 0f || transform.localScale.x <= 0.01f)
        {
            Destroy(material);
            Destroy(gameObject);
        }
    }
}
