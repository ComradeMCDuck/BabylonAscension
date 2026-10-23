using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damage);
}

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private EntityData entityData;
    private float currentHealth;
    public event System.Action<Health> Died;
    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => entityData != null ? entityData.maxHealth : 100f;

    void Awake()
    {
        RestoreHealth();
    }

    public void RestoreHealth()
    {
        currentHealth = MaxHealth;
        IsDead = false;
    }
    
    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 0)
        {
            IsDead = true;
            if (Died != null) Died.Invoke(this);
            else Destroy(gameObject);
        }
    }
}
