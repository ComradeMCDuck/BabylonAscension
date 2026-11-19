using UnityEngine;

/// <summary>
/// A player value that altar upgrades can change. Add a new entry at the END (assets store the number), then read it
/// where it's used through BowUpgrades.Apply. Each entry says which base value Apply should be given.
/// </summary>
public enum BowStat
{
    /// <summary>Seconds of holding needed to reach full charge (BowCombat's Max Charge Time).</summary>
    MaxChargeTime,
    /// <summary>Multiplier on the arrow's max speed (ArrowData's Max Arrow Speed). Base value is 1.</summary>
    FullChargeArrowSpeed,
    /// <summary>The player's max health (EntityData's Max Health). Use Add, e.g. 25.</summary>
    MaxHealth,
    /// <summary>Seconds the perfect (full draw) shot window lasts (BowCombat's Full Draw Bonus Window). Use Add.</summary>
    FullDrawBonusWindow,
    /// <summary>Damage multiplier for basic (left mouse button) arrows. Base value is 1; use Multiply, e.g. 1.25.</summary>
    BasicArrowDamage,
    /// <summary>
    /// Damage multiplier for every arrow while the player is close to death (EntityData's Low Health Threshold).
    /// Base value is 1; use Multiply, e.g. 1.5.
    /// </summary>
    LowHealthDamage,
    /// <summary>
    /// Second Chance: a hit that would kill the player restores some health and makes them invincible for a moment
    /// instead, then recharges for a while (EntityData's Second Chance values). Base value is 0; use Add 1.
    /// </summary>
    SecondChance,
    /// <summary>Chance (0..1) that a killed enemy drops two genii instead of one. Base value is 0; use Add, e.g. 0.25.</summary>
    DoubleGeniiChance,
    /// <summary>Multiplier on melee damage the player takes. Base value is 1; use Multiply, e.g. 0.75 for 25% less.</summary>
    MeleeDamageTaken,
    /// <summary>Multiplier on projectile damage the player takes. Base value is 1; use Multiply, e.g. 0.75 for 25% less.</summary>
    ProjectileDamageTaken,
}

public enum BowUpgradeMode
{
    /// <summary>Adds the amount to the value. Use a negative amount to reduce it.</summary>
    Add,
    /// <summary>Multiplies the value by the amount, e.g. 1.3 for +30%.</summary>
    Multiply,
}

/// <summary>
/// A permanent upgrade the player can take at an altar (bow, health, defence or loot). It only describes the change;
/// the owned upgrades are applied on top of the base values when they're used, so no data asset is ever modified.
/// </summary>
[CreateAssetMenu(fileName = "BowUpgrade", menuName = "Babylon Ascension/Bow Upgrade")]
public class BowUpgrade : ScriptableObject
{
    [Tooltip("Name shown to the player.")]
    [SerializeField] private string displayName;
    [Tooltip("Short description shown to the player, e.g. \"Draw time -0.2 s\".")]
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Genii needed to buy this upgrade at an altar.")]
    [UnityEngine.Serialization.FormerlySerializedAs("soulCost")]
    [SerializeField, Min(0)] private int geniiCost = 1;

    [Header("Effect")]
    [Tooltip("Which value this upgrade changes. Hover the entries in BowUpgrade.cs to see each one's base value.")]
    [SerializeField] private BowStat stat;
    [SerializeField] private BowUpgradeMode mode;
    [Tooltip("Add: added to the value (negative reduces it). Multiply: the value is multiplied by this.")]
    [SerializeField] private float amount;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int GeniiCost => geniiCost;
    public BowStat Stat => stat;
    public BowUpgradeMode Mode => mode;
    public float Amount => amount;
}
