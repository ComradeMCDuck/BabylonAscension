using System;
using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable, IHazardDamageable
{
    [Header("References")]
    [SerializeField] private EntityData enemyData;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Runtime Health")]
    [SerializeField] private float currentHealth;

    private bool isDead;
    /// <summary>Raised when hurt by an attack (EnemyBehavior then chases the player). Hazard damage doesn't raise it.</summary>
    public event Action Damaged;
    private Color originalColor;
    private Coroutine damageIndication;

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
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning(
                $"{name}: No SpriteRenderer assigned to EnemyHealth.",
                this
            );
        }
        else
        {
            originalColor = spriteRenderer.color;
        }
    }

    public void TakeDamage(float damage) => ApplyDamage(damage, true);

    /// <summary>Spikes, fire and other level hazards: hurts like an attack, but doesn't alert the enemy.</summary>
    public void TakeHazardDamage(float damage) => ApplyDamage(damage, false);

    private void ApplyDamage(float damage, bool raiseDamaged)
    {
        if (isDead || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        
        if (spriteRenderer != null)
        {
            // Flash the sprite red to indicate damage. Restart instead of stacking flashes
            // (e.g. poison ticks), so the sprite always returns to its real color.
            if (damageIndication != null)
                StopCoroutine(damageIndication);
            damageIndication = StartCoroutine(DamageIndication());
        }

        if (raiseDamaged) Damaged?.Invoke();

        Debug.Log(
            $"{name} took {damage} damage. " +
            $"Health: {currentHealth}/{MaxHealth}"
        );

        if (currentHealth <= 0f)
            Die();
    }

    public IEnumerator DamageIndication()
    {
        float flashDuration = 1 / enemyData.damageIndicationDuration; // Duration of the flash

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

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Health sits on a child of the enemy, so remove the whole enemy, not just this object.
        EnemyBehavior enemy = GetComponentInParent<EnemyBehavior>();
        GameObject body = enemy != null ? enemy.gameObject : gameObject;

        Debug.Log($"{body.name} died!");

        if (enemyData != null && enemyData.geniiPrefab != null)
            Instantiate(enemyData.geniiPrefab, body.transform.position, Quaternion.identity);

        Destroy(body);
    }
}
