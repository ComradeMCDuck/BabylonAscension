using System.Collections;
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damage);
}

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private EntityData entityData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private float currentHealth;
    public event System.Action<Health> Died;
    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => entityData != null ? entityData.maxHealth : 100f;

    void Awake()
    {
        RestoreHealth();
        currentHealth = entityData.maxHealth;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) Debug.LogWarning($"{gameObject.name}: No SpriteRenderer assigned to Health.", this);
        }
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
        Debug.Log($"{gameObject.name} took {damage} damage. Current health: {currentHealth}/{entityData.maxHealth}");
        StartCoroutine(DamageIndication());
        if (currentHealth <= 0)
        {
            IsDead = true;
            if (Died != null) Died.Invoke(this);
            else Destroy(gameObject);
        }
    }

    public IEnumerator DamageIndication()
    {
        Color originalColor = spriteRenderer.color;
        Color startColor = spriteRenderer.color;
        float flashDuration = 1 / entityData.damageIndicationDuration; // Duration of the flash

        while (spriteRenderer.color != Color.red)
        {
            spriteRenderer.color = new Color(Mathf.MoveTowards(spriteRenderer.color.r, Color.red.r, flashDuration * Time.deltaTime),
                                             Mathf.MoveTowards(spriteRenderer.color.g, Color.red.g, flashDuration * Time.deltaTime),
                                             Mathf.MoveTowards(spriteRenderer.color.b, Color.red.b, flashDuration * Time.deltaTime),
                                             Mathf.MoveTowards(spriteRenderer.color.a, Color.red.a, flashDuration * Time.deltaTime));
            yield return null;
        }
        while (spriteRenderer.color != originalColor)
        {
            spriteRenderer.color = new Color(Mathf.MoveTowards(spriteRenderer.color.r, originalColor.r, flashDuration * Time.deltaTime),
                                             Mathf.MoveTowards(spriteRenderer.color.g, originalColor.g, flashDuration * Time.deltaTime),
                                             Mathf.MoveTowards(spriteRenderer.color.b, originalColor.b, flashDuration * Time.deltaTime),
                                             Mathf.MoveTowards(spriteRenderer.color.a, originalColor.a, flashDuration * Time.deltaTime));
            yield return null;
        }
    }
}
