using System;
using UnityEngine;

// Add to an enemy or destructible root; child colliders forward hits to this component.
public class LocalHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    // Raised after health changes, before a lethal hit deactivates the object. The source may be null.
    public event Action<LocalHealth, float, GameObject> Damaged;

    private void Awake() => CurrentHealth = maxHealth;

    public void TakeDamage(float damage, GameObject source = null)
    {
        if (damage <= 0f || CurrentHealth <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        Damaged?.Invoke(this, damage, source);
        if (CurrentHealth == 0f)
            gameObject.SetActive(false);
    }
}
