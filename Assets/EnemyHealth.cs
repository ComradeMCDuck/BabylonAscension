
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IArrowDamageable
{
    [Header("References")]
    [SerializeField] private EntityData enemyData;

    [Header("Runtime Health")]
    [SerializeField] private float currentHealth;

    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => enemyData != null
        ? enemyData.maxHealth
        : 100f;

    private void Awake()
    {
        currentHealth = MaxHealth;

        if (enemyData == null)
        {
            Debug.LogWarning(
                $"{name}: No EntityData assigned to EnemyHealth.",
                this
            );
        }
    }

    public void TakeArrowDamage(float damage)
    {
        if (isDead || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        Debug.Log(
            $"{name} took {damage} damage. " +
            $"Health: {currentHealth}/{MaxHealth}"
        );

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Debug.Log($"{name} died!");

        Destroy(gameObject);
    }
}
