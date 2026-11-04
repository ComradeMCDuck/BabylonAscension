using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The boss's health, set up from BossData instead of EntityData. Like EnemyHealth it can sit on the boss or a child,
/// because arrows and hitboxes look for an IDamageable in the whole character. It doesn't remove the boss itself:
/// BossBrain listens to Died, cancels attacks, drops the soul and removes the boss after BossData.deathRemoveDelay.
/// </summary>
public class BossHealth : MonoBehaviour, IDamageable
{
    [Tooltip("Sprites that flash red when the boss is hit. Empty = the BossBrain's Sprite Renderer.")]
    [SerializeField] private SpriteRenderer[] flashRenderers;

    private BossData bossData;
    private float currentHealth;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => bossData != null ? bossData.maxHealth : 1f;
    public float Health01 => currentHealth / MaxHealth;
    public bool IsDead { get; private set; }

    /// <summary>Ignores all damage while true (e.g. for a future phase change or cutscene). Off by default.</summary>
    public bool Invulnerable { get; set; }

    /// <summary>Raised on every hit that deals damage, with the damage dealt.</summary>
    public event Action<float> Damaged;
    /// <summary>Raised on every health change, with (current, max). Use it for a boss health bar.</summary>
    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        BossBrain brain = GetComponentInParent<BossBrain>();
        bossData = brain != null ? brain.Data : null;

        if (bossData == null)
            Debug.LogError($"{name}: BossHealth needs a BossBrain with a BossData on this object or a parent.", this);

        currentHealth = MaxHealth;

        if ((flashRenderers == null || flashRenderers.Length == 0) && brain != null && brain.MainSprite != null)
            flashRenderers = new[] { brain.MainSprite };

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            Debug.LogWarning($"{name}: No sprite to flash when the boss is hit.", this);
            flashRenderers = Array.Empty<SpriteRenderer>();
        }

        originalColors = new Color[flashRenderers.Length];
        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i] != null) originalColors[i] = flashRenderers[i].color;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || Invulnerable || damage <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        // Restart instead of stacking flashes (e.g. poison ticks), so the sprites always return to their real color.
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());

        Damaged?.Invoke(damage);
        HealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            Died?.Invoke();
        }
    }

    /// <summary>Tints the sprites red and back over damageIndicationDuration.</summary>
    private IEnumerator Flash()
    {
        float duration = bossData != null ? bossData.damageIndicationDuration : 0.15f;
        float half = duration * 0.5f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float amount = t < half ? t / half : 1f - (t - half) / half;
            SetTint(amount);
            yield return null;
        }

        SetTint(0f);
        flashRoutine = null;
    }

    private void SetTint(float amount)
    {
        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i] != null)
                flashRenderers[i].color = Color.Lerp(originalColors[i], Color.red, amount);
    }

    private void OnDisable()
    {
        // A flash cut short would leave the sprites red.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        if (originalColors != null) SetTint(0f);
    }
}
