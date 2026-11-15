using System;
using System.Collections.Generic;

/// <summary>
/// The UI windows that Escape should close, most recently opened last. <see cref="GameManager"/> is the only
/// script that reads the Escape key: it closes the top window here first, and only toggles the pause menu when
/// nothing is open. That keeps one key press from both closing a window and pausing the game.
/// A window calls <see cref="Push"/> when it opens and <see cref="Remove"/> when it closes (for any reason).
/// </summary>
public static class EscapeCloseStack
{
    private struct Entry
    {
        public UnityEngine.Object owner;
        public Action close;
    }

    // Static, so it outlives scene loads; entries of destroyed owners are dropped when they are found.
    private static readonly List<Entry> entries = new List<Entry>();

    /// <summary>True while at least one live window is registered.</summary>
    public static bool HasOpenWindow
    {
        get
        {
            RemoveDestroyed();
            return entries.Count > 0;
        }
    }

    /// <summary>Registers an open window. <paramref name="close"/> must close it and end up calling <see cref="Remove"/>.</summary>
    public static void Push(UnityEngine.Object owner, Action close)
    {
        if (owner == null || close == null) return;
        Remove(owner);
        entries.Add(new Entry { owner = owner, close = close });
    }

    /// <summary>Unregisters a window. Harmless if it was not registered.</summary>
    public static void Remove(UnityEngine.Object owner)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            // ReferenceEquals: a destroyed owner compares equal to null, but must still match its own entry.
            if (ReferenceEquals(entries[i].owner, owner)) entries.RemoveAt(i);
        }
    }

    /// <summary>Closes the most recently opened window. Returns false when none is open.</summary>
    public static bool CloseTop()
    {
        RemoveDestroyed();
        if (entries.Count == 0) return false;

        Entry top = entries[entries.Count - 1];
        // Removed here as well, so a close callback that forgets to unregister can't trap Escape forever.
        entries.RemoveAt(entries.Count - 1);
        top.close();
        return true;
    }

    private static void RemoveDestroyed()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].owner == null) entries.RemoveAt(i);
        }
    }
}
