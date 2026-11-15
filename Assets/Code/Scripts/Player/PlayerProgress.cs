using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player keeps between scenes: health, genii, bow upgrades and bought arrows. The player is part of each
/// scene and is recreated on every load, so this static store carries the values across. GameManager captures
/// them when a scene change starts and applies them to the new player in Start.
/// After death only health is dropped, so the player comes back at full health but keeps genii and upgrades.
/// Starting a level straight from the Editor (nothing captured yet) uses the player's Inspector values.
/// </summary>
public static class PlayerProgress
{
    // Nothing captured yet: the first scene of a run uses the Inspector values.
    private static bool hasData;
    // NaN = no saved health (after death or at the start of a run): the player starts at full health.
    private static float health = float.NaN;
    private static int genii;
    private static readonly List<BowUpgrade> upgrades = new List<BowUpgrade>();
    private static readonly List<GameObject> ownedArrows = new List<GameObject>();
    private static GameObject selectedArrow;

    public static bool HasData => hasData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();

    /// <summary>Forgets everything, e.g. when returning to the main menu, so the next run starts fresh.</summary>
    public static void Clear()
    {
        hasData = false;
        health = float.NaN;
        genii = 0;
        upgrades.Clear();
        ownedArrows.Clear();
        selectedArrow = null;
    }

    /// <summary>
    /// Stores the player's current state. With <paramref name="keepHealth"/> false (death), health isn't kept,
    /// so the next scene starts the player at full health.
    /// </summary>
    public static void Capture(Transform player, bool keepHealth)
    {
        if (player == null) return;
        hasData = true;

        Health playerHealth = player.GetComponentInChildren<Health>();
        health = keepHealth && playerHealth != null && !playerHealth.IsDead ? playerHealth.CurrentHealth : float.NaN;

        GeniiInventory inventory = player.GetComponentInChildren<GeniiInventory>();
        if (inventory != null) genii = inventory.Count;

        BowCombat bow = player.GetComponentInChildren<BowCombat>();
        if (bow != null)
        {
            upgrades.Clear();
            upgrades.AddRange(bow.Upgrades.Owned);
            ownedArrows.Clear();
            ownedArrows.AddRange(bow.OwnedArrows);
            selectedArrow = bow.CurrentArrowPrefab;
        }
    }

    /// <summary>Gives the stored state to a freshly loaded player. Does nothing if nothing was captured.</summary>
    public static void Apply(Transform player)
    {
        if (!hasData || player == null) return;

        Health playerHealth = player.GetComponentInChildren<Health>();
        if (playerHealth != null && !float.IsNaN(health)) playerHealth.SetHealth(health);

        GeniiInventory inventory = player.GetComponentInChildren<GeniiInventory>();
        if (inventory != null) inventory.SetCount(genii);

        BowCombat bow = player.GetComponentInChildren<BowCombat>();
        if (bow != null)
        {
            foreach (BowUpgrade upgrade in upgrades) bow.Upgrades.Add(upgrade);
            foreach (GameObject arrow in ownedArrows) bow.UnlockArrow(arrow);
            if (selectedArrow != null && bow.OwnsArrow(selectedArrow)) bow.SetArrow(selectedArrow);
        }
    }
}
