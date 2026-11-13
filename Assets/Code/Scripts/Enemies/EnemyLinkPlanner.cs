using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Picks which EnemyLinks a ground enemy takes to reach the player's platform, possibly several in a row
/// (floor -> ledge -> upper platform). Platforms are the ground colliders, with colliders that touch at the same
/// height merged into one (a floor built from several boxes is walked across, not linked). Every usable link
/// direction is a step; a step can follow another if it starts on the platform the other ends on. Dijkstra finds the
/// cheapest chain, costed in metres: walking to each link's start, plus each link's length plus a flat LinkCost.
/// If no chain reaches the player's platform, the route instead ends at the reachable spot closest to the player,
/// where the enemy waits.
/// </summary>
public static class EnemyLinkPlanner
{
    // Colliders whose tops are this close in height...
    private const float FlushHeightTolerance = 0.05f;
    // ...and that are this close side by side (or overlap) are one platform.
    private const float FlushGapTolerance = 0.1f;
    // Extra cost of taking a link, in metres of walking, so the enemy doesn't take links it doesn't need.
    private const float LinkCost = 2f;
    // When waiting at the closest reachable spot, it stays this far in from the platform's edge.
    private const float WaitEdgeInset = 0.5f;
    // Reachable spots closer to the player than this to each other count as equally close; the cheaper one wins.
    private const float SameDistanceTolerance = 0.25f;

    /// <summary>One link, taken in one direction.</summary>
    public struct Step
    {
        public EnemyLink Link;
        public bool Reverse;

        public Vector2 From => Link.From(Reverse);
        public Vector2 To => Link.To(Reverse);
        public Collider2D FromGround => Link.FromGround(Reverse);
        public Collider2D ToGround => Link.ToGround(Reverse);
        public EnemyLink.Traversal Traversal => Link.GetTraversal(Reverse);
    }

    private struct Platform
    {
        public float MinX;
        public float MaxX;
        public float Top;
    }

    private static readonly Dictionary<Collider2D, int> s_GroupOf = new Dictionary<Collider2D, int>();
    private static readonly List<Platform> s_Platforms = new List<Platform>();
    private static int s_GroupsMask = -1;
    private static bool s_Subscribed;

    private static readonly List<Step> s_Steps = new List<Step>();
    private static float[] s_Cost = new float[16];
    private static int[] s_Previous = new int[16];
    private static bool[] s_Done = new bool[16];
    private static int[] s_FromGroup = new int[16];
    private static int[] s_ToGroup = new int[16];

    /// <summary>True if both colliders are part of the same platform (the same collider, or flush neighbours).</summary>
    public static bool SamePlatform(Collider2D a, Collider2D b, int groundMask)
    {
        if (a == null || b == null) return false;
        return a == b || GroupOf(a, groundMask) == GroupOf(b, groundMask);
    }

    /// <summary>The platform a ground collider belongs to, or -1 for none.</summary>
    public static int GroupOf(Collider2D collider, int groundMask)
    {
        if (collider == null) return -1;
        BuildGroups(groundMask);

        if (s_GroupOf.TryGetValue(collider, out int group)) return group;

        // A collider that wasn't in the level when the groups were built is a platform of its own.
        Bounds bounds = collider.bounds;
        s_Platforms.Add(new Platform { MinX = bounds.min.x, MaxX = bounds.max.x, Top = bounds.max.y });
        group = s_Platforms.Count - 1;
        s_GroupOf[collider] = group;
        return group;
    }

    /// <summary>
    /// Fills route with the links to take from where the enemy stands (fromGround, fromFeet) toward the player.
    /// Returns true if the route ends on the player's platform. Otherwise returns false, and the route ends on the
    /// platform with the reachable spot closest to the player. goal is that spot (or the end of the last link).
    /// Links that the enemy isn't allowed to use, or that are in blockedUntil with a time still to come, are skipped.
    /// </summary>
    public static bool Plan(Collider2D fromGround, Vector2 fromFeet, Collider2D playerGround, Vector2 playerPosition,
        EntityData enemyData, int groundMask, Dictionary<EnemyLink, float> blockedUntil, List<Step> route, out Vector2 goal)
    {
        route.Clear();
        int fromGroup = GroupOf(fromGround, groundMask);
        int playerGroup = GroupOf(playerGround, groundMask);

        CollectSteps(enemyData, groundMask, blockedUntil);
        RunDijkstra(fromGroup, fromFeet);

        // Cheapest way onto the player's platform.
        int best = -2;
        float bestCost = float.PositiveInfinity;
        if (playerGroup >= 0)
        {
            if (playerGroup == fromGroup)
            {
                best = -1;
                bestCost = Mathf.Abs(playerPosition.x - fromFeet.x);
            }

            for (int i = 0; i < s_Steps.Count; i++)
            {
                if (!s_Done[i] || s_ToGroup[i] != playerGroup) continue;
                float cost = s_Cost[i] + Mathf.Abs(playerPosition.x - s_Steps[i].To.x);
                if (cost >= bestCost) continue;
                best = i;
                bestCost = cost;
            }
        }

        if (best != -2)
        {
            BuildRoute(best, route);
            goal = best >= 0 ? s_Steps[best].To : fromFeet;
            return true;
        }

        // No way there: the reachable spot closest to the player, preferring the cheaper one when about as close.
        best = -1;
        goal = ClosestSpot(fromGroup, fromFeet, playerPosition);
        float bestDistance = Vector2.Distance(goal, playerPosition);
        bestCost = 0f;

        for (int i = 0; i < s_Steps.Count; i++)
        {
            if (!s_Done[i]) continue;

            Vector2 spot = ClosestSpot(s_ToGroup[i], s_Steps[i].To, playerPosition);
            float distance = Vector2.Distance(spot, playerPosition);
            bool closer = distance < bestDistance - SameDistanceTolerance;
            bool asCloseButCheaper = distance < bestDistance + SameDistanceTolerance && s_Cost[i] < bestCost;
            if (!closer && !asCloseButCheaper) continue;

            best = i;
            goal = spot;
            bestDistance = distance;
            bestCost = s_Cost[i];
        }

        BuildRoute(best, route);
        return false;
    }

    private static void CollectSteps(EntityData enemyData, int groundMask, Dictionary<EnemyLink, float> blockedUntil)
    {
        s_Steps.Clear();
        foreach (EnemyLink link in EnemyLink.Active)
        {
            if (!link.IsValid || !link.Allows(enemyData)) continue;
            if (blockedUntil != null && blockedUntil.TryGetValue(link, out float until) && Time.time < until) continue;

            s_Steps.Add(new Step { Link = link, Reverse = false });
            if (link.Bidirectional) s_Steps.Add(new Step { Link = link, Reverse = true });
        }

        int count = s_Steps.Count;
        if (s_Cost.Length < count)
        {
            int size = Mathf.NextPowerOfTwo(count);
            s_Cost = new float[size];
            s_Previous = new int[size];
            s_Done = new bool[size];
            s_FromGroup = new int[size];
            s_ToGroup = new int[size];
        }

        for (int i = 0; i < count; i++)
        {
            s_FromGroup[i] = GroupOf(s_Steps[i].FromGround, groundMask);
            s_ToGroup[i] = GroupOf(s_Steps[i].ToGround, groundMask);
        }
    }

    /// <summary>s_Cost[i]: cheapest cost of standing at the end of step i, coming from fromFeet. Links number in the tens, so O(n^2) is fine.</summary>
    private static void RunDijkstra(int fromGroup, Vector2 fromFeet)
    {
        int count = s_Steps.Count;
        for (int i = 0; i < count; i++)
        {
            s_Previous[i] = -1;
            s_Done[i] = false;
            s_Cost[i] = fromGroup >= 0 && s_FromGroup[i] == fromGroup
                ? Mathf.Abs(s_Steps[i].From.x - fromFeet.x) + StepCost(s_Steps[i])
                : float.PositiveInfinity;
        }

        while (true)
        {
            int current = -1;
            for (int i = 0; i < count; i++)
            {
                if (s_Done[i] || float.IsPositiveInfinity(s_Cost[i])) continue;
                if (current < 0 || s_Cost[i] < s_Cost[current]) current = i;
            }
            if (current < 0) return;

            s_Done[current] = true;
            Vector2 landing = s_Steps[current].To;

            for (int next = 0; next < count; next++)
            {
                if (s_Done[next] || s_FromGroup[next] != s_ToGroup[current]) continue;

                float cost = s_Cost[current] + Mathf.Abs(s_Steps[next].From.x - landing.x) + StepCost(s_Steps[next]);
                if (cost >= s_Cost[next]) continue;
                s_Cost[next] = cost;
                s_Previous[next] = current;
            }
        }
    }

    private static float StepCost(Step step) => LinkCost + Vector2.Distance(step.From, step.To);

    private static void BuildRoute(int last, List<Step> route)
    {
        for (int i = last; i >= 0; i = s_Previous[i]) route.Add(s_Steps[i]);
        route.Reverse();
    }

    /// <summary>The point on a platform's top closest to the player, kept a little in from its edges.</summary>
    private static Vector2 ClosestSpot(int group, Vector2 fallback, Vector2 playerPosition)
    {
        if (group < 0) return fallback;

        Platform platform = s_Platforms[group];
        float min = platform.MinX + WaitEdgeInset;
        float max = platform.MaxX - WaitEdgeInset;
        float x = min <= max ? Mathf.Clamp(playerPosition.x, min, max) : (platform.MinX + platform.MaxX) * 0.5f;
        return new Vector2(x, platform.Top);
    }

    /// <summary>
    /// Merges the level's ground colliders into platforms, once per scene (and again if asked for a different mask).
    /// </summary>
    private static void BuildGroups(int groundMask)
    {
        if (s_GroupsMask == groundMask) return;

        Subscribe();
        s_GroupsMask = groundMask;
        s_GroupOf.Clear();
        s_Platforms.Clear();

        var colliders = new List<Collider2D>();
        foreach (Collider2D collider in Object.FindObjectsByType<Collider2D>())
        {
            if (!collider.enabled || collider.isTrigger || (groundMask & (1 << collider.gameObject.layer)) == 0) continue;
            colliders.Add(collider);
        }

        // Union-find over the colliders; the level has tens of them, so comparing every pair is fine.
        var parent = new int[colliders.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        for (int i = 0; i < colliders.Count; i++)
        {
            Bounds a = colliders[i].bounds;
            for (int j = i + 1; j < colliders.Count; j++)
            {
                Bounds b = colliders[j].bounds;
                if (Mathf.Abs(a.max.y - b.max.y) > FlushHeightTolerance) continue;
                float gap = Mathf.Max(a.min.x, b.min.x) - Mathf.Min(a.max.x, b.max.x);
                if (gap > FlushGapTolerance) continue;
                parent[Find(parent, i)] = Find(parent, j);
            }
        }

        var groupOfRoot = new Dictionary<int, int>();
        for (int i = 0; i < colliders.Count; i++)
        {
            int root = Find(parent, i);
            Bounds bounds = colliders[i].bounds;

            if (!groupOfRoot.TryGetValue(root, out int group))
            {
                group = s_Platforms.Count;
                groupOfRoot[root] = group;
                s_Platforms.Add(new Platform { MinX = bounds.min.x, MaxX = bounds.max.x, Top = bounds.max.y });
            }
            else
            {
                Platform platform = s_Platforms[group];
                platform.MinX = Mathf.Min(platform.MinX, bounds.min.x);
                platform.MaxX = Mathf.Max(platform.MaxX, bounds.max.x);
                platform.Top = Mathf.Max(platform.Top, bounds.max.y);
                s_Platforms[group] = platform;
            }

            s_GroupOf[colliders[i]] = group;
        }
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }

    private static void Subscribe()
    {
        if (s_Subscribed) return;
        s_Subscribed = true;
        // Removed first, so a play mode restart without domain reload can't subscribe twice.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // A new level has different platforms.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => s_GroupsMask = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_GroupsMask = -1;
        s_Subscribed = false;
        s_GroupOf.Clear();
        s_Platforms.Clear();
    }
}
