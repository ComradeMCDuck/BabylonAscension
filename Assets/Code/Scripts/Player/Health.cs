using System.Collections;
using DYP;
using UnityEngine;

/// <summary>What kind of attack dealt the damage, so the player's resistance upgrades can tell them apart.</summary>
public enum DamageKind
{
    /// <summary>Anything else: hazards, area attacks like the boss roar.</summary>
    Other,
    /// <summary>Hitbox attacks: slashes, bites, tail whips, charges.</summary>
    Melee,
    /// <summary>Arrows and other ArrowProjectile shots, including their explosions.</summary>
    Projectile,
}

public interface IDamageable
{
    void TakeDamage(float damage);

    /// <summary>
    /// Damage of a known kind. Only the player's Health uses the kind (resistance upgrades); everything else
    /// takes it as normal damage through this default.
    /// </summary>
    void TakeDamage(float damage, DamageKind kind) => TakeDamage(damage);
}

/// <summary>
/// Optional for IDamageables that react differently to level hazards (spikes, fire) than to attacks:
/// an enemy hurt by a hazard shouldn't take it as the player hitting it and start chasing.
/// Targets without it take hazard damage through TakeDamage.
/// </summary>
public interface IHazardDamageable
{
    void TakeHazardDamage(float damage);
}

/// <summary>
/// The player's health. It also applies the altar upgrades that concern health (read from the player's BowCombat):
/// more max health, less melee or projectile damage taken, and Second Chance, which turns a killing hit into a small
/// heal plus a moment of invincibility (the sprites blink white).
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private EntityData entityData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private float currentHealth;
    public event System.Action<Health> Died;
    /// <summary>Raised when Second Chance saves the player from a killing hit.</summary>
    public event System.Action<Health> SecondChanceUsed;
    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHealth;
    /// <summary>EntityData's Max Health plus the max health upgrades.</summary>
    public float MaxHealth => bow != null ? Mathf.Max(1f, bow.Upgrades.Apply(BowStat.MaxHealth, BaseMaxHealth)) : BaseMaxHealth;
    /// <summary>True while hits do nothing (after Second Chance saved the player).</summary>
    public bool IsInvincible => Time.time < invincibleUntil;
    /// <summary>True at or below EntityData's Low Health Threshold, for the low-health damage upgrade.</summary>
    public bool IsCloseToDeath =>
        !IsDead && entityData != null && currentHealth <= MaxHealth * entityData.lowHealthThreshold;

    private float BaseMaxHealth => entityData != null ? entityData.maxHealth : 100f;

    private Color originalColor;
    private Coroutine damageIndication;

    // The upgrades live on the player's bow. Null for a Health without one, which then gets no upgrades.
    private BowCombat bow;
    // Max health when last checked, to notice max health upgrades being switched on or off.
    private float lastMaxHealth;
    // Health cut off when max health last dropped (an upgrade switched off). Switching it back on returns only this,
    // so toggling an upgrade at the altar can't be used to heal.
    private float healthCutByMaxDrop;
    // Time.time from which Second Chance can save the player again.
    private float secondChanceReadyTime = float.NegativeInfinity;
    private float invincibleUntil = float.NegativeInfinity;

    /// <summary>True when the Second Chance upgrade is active and not recharging.</summary>
    public bool IsSecondChanceReady =>
        bow != null && bow.Upgrades.Apply(BowStat.SecondChance, 0f) >= 1f && Time.time >= secondChanceReadyTime;
    /// <summary>Seconds until Second Chance can save the player again (0 when ready).</summary>
    public float SecondChanceCooldownLeft => Mathf.Max(0f, secondChanceReadyTime - Time.time);

    void Awake()
    {
        FindBow();
        RestoreHealth();
        lastMaxHealth = MaxHealth;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) Debug.LogWarning($"{gameObject.name}: No SpriteRenderer assigned to Health.", this);
        }

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    /// <summary>Health and BowCombat sit on different objects of the player, so search the whole character.</summary>
    private void FindBow()
    {
        bow = GetComponentInParent<BowCombat>();
        if (bow != null) return;

        BasicMovementController2D character = GetComponentInParent<BasicMovementController2D>();
        Transform root = character != null ? character.transform : transform;
        bow = root.GetComponentInChildren<BowCombat>(true);
    }

    private void OnEnable()
    {
        if (bow != null) bow.Upgrades.Changed += OnUpgradesChanged;
    }

    private void OnDisable()
    {
        if (bow != null) bow.Upgrades.Changed -= OnUpgradesChanged;
    }

    /// <summary>
    /// Max health upgrade switched on or off. Switching it off cuts health down to the new max; switching it back on
    /// returns what was cut, never more, so a full player stays full and nobody heals by toggling.
    /// </summary>
    private void OnUpgradesChanged()
    {
        float max = MaxHealth;

        if (max < lastMaxHealth)
        {
            float cut = Mathf.Max(0f, currentHealth - max);
            healthCutByMaxDrop += cut;
            currentHealth -= cut;
        }
        else if (max > lastMaxHealth && !IsDead)
        {
            float returned = Mathf.Min(healthCutByMaxDrop, max - lastMaxHealth);
            healthCutByMaxDrop -= returned;
            currentHealth += returned;
        }

        lastMaxHealth = max;
    }

    public void RestoreHealth()
    {
        currentHealth = MaxHealth;
        IsDead = false;
        // A new life: Second Chance is recharged.
        secondChanceReadyTime = float.NegativeInfinity;
        invincibleUntil = float.NegativeInfinity;
        healthCutByMaxDrop = 0f;
        // Cancel a flash that was interrupted (e.g. by death) so the sprite doesn't respawn tinted red.
        if (damageIndication != null)
        {
            StopCoroutine(damageIndication);
            damageIndication = null;
            spriteRenderer.color = originalColor;
        }
    }

    /// <summary>Sets health directly, clamped to 1..max, e.g. to carry the player's health into the next scene.</summary>
    public void SetHealth(float value)
    {
        if (IsDead) return;
        currentHealth = Mathf.Clamp(value, 1f, MaxHealth);
    }

    /// <summary>Adds health, never above max. Does nothing once dead, so a pickup can't undo a death.</summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
    }

    public void TakeDamage(float damage) => TakeDamage(damage, DamageKind.Other);

    /// <summary>Damage reduced by the resistance upgrade for its kind. Ignored while invincible.</summary>
    public void TakeDamage(float damage, DamageKind kind)
    {
        if (IsDead || damage <= 0f || IsInvincible) return;

        damage *= GetDamageTakenMultiplier(kind);
        if (damage <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"{gameObject.name} took {damage} {kind} damage. Current health: {currentHealth}/{MaxHealth}");
        // Restart instead of stacking flashes (e.g. poison ticks), so the sprite always returns to its real color.
        if (spriteRenderer != null)
        {
            if (damageIndication != null)
                StopCoroutine(damageIndication);
            damageIndication = StartCoroutine(DamageIndication());
        }
        if (currentHealth <= 0)
        {
            if (TryUseSecondChance()) return;

            IsDead = true;
            if (Died != null) Died.Invoke(this);
            else Destroy(gameObject);
        }
    }

    private float GetDamageTakenMultiplier(DamageKind kind)
    {
        if (bow == null) return 1f;

        switch (kind)
        {
            case DamageKind.Melee: return Mathf.Max(0f, bow.Upgrades.Apply(BowStat.MeleeDamageTaken, 1f));
            case DamageKind.Projectile: return Mathf.Max(0f, bow.Upgrades.Apply(BowStat.ProjectileDamageTaken, 1f));
            default: return 1f;
        }
    }

    /// <summary>
    /// Second Chance upgrade: instead of dying, get back a part of max health and become invincible for a moment,
    /// shown by the sprites blinking white. It then recharges for EntityData's Second Chance Cooldown; a new life
    /// (respawn or scene load) recharges it at once.
    /// </summary>
    private bool TryUseSecondChance()
    {
        if (entityData == null || !IsSecondChanceReady) return false;

        secondChanceReadyTime = Time.time + entityData.secondChanceCooldown;
        currentHealth = Mathf.Max(1f, MaxHealth * entityData.secondChanceHealFraction);
        invincibleUntil = Time.time + entityData.secondChanceInvincibilityTime;

        // Flash every sprite of the character, not only the one the red damage flash uses.
        BasicMovementController2D character = GetComponentInParent<BasicMovementController2D>();
        GameObject root = character != null ? character.gameObject : gameObject;
        SpriteFlash.For(root).Blink(entityData.secondChanceInvincibilityTime, entityData.secondChanceBlinkInterval);

        SecondChanceUsed?.Invoke(this);
        return true;
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
        damageIndication = null;
    }

    public float CurrentHP => currentHealth;   // read‑only public accessor
    public float MaxHP => MaxHealth;
}
