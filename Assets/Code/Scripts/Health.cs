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
    private Color originalColor;
    private Coroutine damageIndication;

    void Awake()
    {
        currentHealth = entityData.maxHealth;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning($"{gameObject.name}: No SpriteRenderer assigned to Health.", this);
            }
        }

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }
    
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Current health: {currentHealth}/{entityData.maxHealth}");
        // Restart instead of stacking flashes (e.g. poison ticks), so the sprite always returns to its real color.
        if (spriteRenderer != null)
        {
            if (damageIndication != null)
                StopCoroutine(damageIndication);
            damageIndication = StartCoroutine(DamageIndication());
        }
        if (currentHealth <= 0)
        {
            Debug.Log($"{gameObject.name} has been destroyed.");
            Destroy(gameObject);
        }
    }

    public IEnumerator DamageIndication()
    {
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
