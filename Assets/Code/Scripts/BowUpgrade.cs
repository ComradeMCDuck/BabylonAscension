using UnityEngine;

/// <summary>A bow value that upgrades can change. Add a new entry here, then read it in BowCombat through BowUpgrades.Apply.</summary>
public enum BowStat
{
    /// <summary>Seconds of holding needed to reach full charge (BowCombat's Max Charge Time).</summary>
    MaxChargeTime,
    /// <summary>Multiplier on the arrow's max speed (ArrowData's Max Arrow Speed). Base value is 1.</summary>
    FullChargeArrowSpeed,
}

public enum BowUpgradeMode
{
    /// <summary>Adds the amount to the value. Use a negative amount to reduce it.</summary>
    Add,
    /// <summary>Multiplies the value by the amount, e.g. 1.3 for +30%.</summary>
    Multiply,
}

/// <summary>
/// A permanent bow upgrade the player can take at an altar. It only describes the change;
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
    [Tooltip("Souls needed to buy this upgrade at an altar.")]
    [SerializeField, Min(0)] private int soulCost = 1;

    [Header("Effect")]
    [Tooltip("Which bow value this upgrade changes.")]
    [SerializeField] private BowStat stat;
    [SerializeField] private BowUpgradeMode mode;
    [Tooltip("Add: added to the value (negative reduces it). Multiply: the value is multiplied by this.")]
    [SerializeField] private float amount;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int SoulCost => soulCost;
    public BowStat Stat => stat;
    public BowUpgradeMode Mode => mode;
    public float Amount => amount;
}
