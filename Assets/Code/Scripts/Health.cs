using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damage);
}

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private EntityData entityData;
    private float currentHealth;

    void Awake()
    {
        currentHealth = entityData.maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Current health: {currentHealth}/{entityData.maxHealth}");
        if (currentHealth <= 0)
        {
            Debug.Log($"{gameObject.name} has been destroyed.");
            Destroy(gameObject);
        }
    }
}
