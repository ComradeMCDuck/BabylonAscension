using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which level objects are gone for good during this run: killed enemies, solved puzzles, opened doors.
/// Every scene load recreates the level from the scene file, so this static store carries the list across, the
/// same way <see cref="PlayerProgress"/> carries the player. <see cref="DontRespawn"/> writes to it and reads it.
/// Removals only become permanent when the player reaches a later level in the Build Profiles scene list
/// (<see cref="Commit"/>). Dying, or going back to an earlier level, throws them away
/// (<see cref="DiscardPending"/>), so the level starts over.
/// It lives in memory only: it is forgotten on the way to the main menu and when the game closes.
/// </summary>
public static class LevelMemory
{
    // Kept for the rest of the run.
    private static readonly HashSet<string> remembered = new HashSet<string>();
    // Removed during the current visit to a level; not permanent until the player leaves it alive.
    private static readonly HashSet<string> pending = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();

    /// <summary>Forgets everything, e.g. when returning to the main menu, so the next run starts with full levels.</summary>
    public static void Clear()
    {
        remembered.Clear();
        pending.Clear();
    }

    /// <summary>Notes a removal for the current visit. It only lasts if <see cref="Commit"/> follows.</summary>
    public static void Remember(string key) => pending.Add(key);

    public static void Forget(string key)
    {
        pending.Remove(key);
        remembered.Remove(key);
    }

    /// <summary>True for objects made permanent by an earlier visit, not for ones removed during this visit.</summary>
    public static bool Contains(string key) => remembered.Contains(key);

    /// <summary>Makes this visit's removals permanent. Called when the player moves on to a later level.</summary>
    public static void Commit()
    {
        remembered.UnionWith(pending);
        pending.Clear();
    }

    /// <summary>Drops this visit's removals. Called when the player dies or goes back to an earlier level.</summary>
    public static void DiscardPending() => pending.Clear();
}
