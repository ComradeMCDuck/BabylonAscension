using System.Collections.Generic;
using UnityEngine;

/// <summary>When a removal noted by <see cref="DontRespawn"/> becomes permanent.</summary>
public enum RemovalPersistence
{
    /// <summary>Once the player leaves the level alive, by any exit (forward, back, a secret door). Dying drops it.</summary>
    WhenLeavingAlive = 0,
    /// <summary>At once, even if the player dies afterwards. For opened shortcuts, solved puzzles, picked-up keys.</summary>
    Immediately = 1,
    /// <summary>Only when the player goes on to a later level in the Build Profiles scene list. Dying or going back drops it.</summary>
    WhenReachingLaterLevel = 2,
}

/// <summary>
/// Which level objects are gone for good during this run: killed enemies, solved puzzles, opened doors.
/// Every scene load recreates the level from the scene file, so this static store carries the list across, the
/// same way <see cref="PlayerProgress"/> carries the player. <see cref="DontRespawn"/> writes to it and reads it.
/// Each removal says when it becomes permanent (<see cref="RemovalPersistence"/>). Until then it is pending:
/// leaving the level alive (<see cref="Commit"/>) keeps it, dying (<see cref="DiscardPending"/>) throws it away,
/// so the level starts over. The build order is only used by <see cref="RemovalPersistence.WhenReachingLaterLevel"/>.
/// It lives in memory only: it is forgotten on the way to the main menu and when the game closes.
/// </summary>
public static class LevelMemory
{
    // Kept for the rest of the run.
    private static readonly HashSet<string> remembered = new HashSet<string>();
    // Removed during the current visit; kept when the player leaves the level alive by any exit.
    private static readonly HashSet<string> pendingUntilExit = new HashSet<string>();
    // Removed during the current visit; kept only when the player moves on to a later level.
    private static readonly HashSet<string> pendingUntilLaterLevel = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();

    /// <summary>Forgets everything, e.g. when returning to the main menu, so the next run starts with full levels.</summary>
    public static void Clear()
    {
        remembered.Clear();
        DiscardPending();
    }

    /// <summary>Notes a removal. Unless it is <see cref="RemovalPersistence.Immediately"/>, it only lasts if <see cref="Commit"/> follows.</summary>
    public static void Remember(string key, RemovalPersistence persistence)
    {
        switch (persistence)
        {
            case RemovalPersistence.Immediately:
                remembered.Add(key);
                break;
            case RemovalPersistence.WhenReachingLaterLevel:
                pendingUntilLaterLevel.Add(key);
                break;
            default:
                pendingUntilExit.Add(key);
                break;
        }
    }

    public static void Forget(string key)
    {
        pendingUntilExit.Remove(key);
        pendingUntilLaterLevel.Remove(key);
        remembered.Remove(key);
    }

    /// <summary>True for permanent removals: made by an earlier visit, or <see cref="RemovalPersistence.Immediately"/> ones.</summary>
    public static bool Contains(string key) => remembered.Contains(key);

    /// <summary>
    /// The player left the level alive. Makes this visit's removals permanent; the ones waiting for a later level
    /// only if <paramref name="reachedLaterLevel"/>, otherwise they are dropped.
    /// </summary>
    public static void Commit(bool reachedLaterLevel)
    {
        remembered.UnionWith(pendingUntilExit);
        if (reachedLaterLevel) remembered.UnionWith(pendingUntilLaterLevel);
        DiscardPending();
    }

    /// <summary>Drops this visit's removals that aren't permanent yet. Called when the player dies (the level reloads).</summary>
    public static void DiscardPending()
    {
        pendingUntilExit.Clear();
        pendingUntilLaterLevel.Clear();
    }
}
