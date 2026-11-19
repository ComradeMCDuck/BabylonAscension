using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Connects the game's existing events to GameAnalytics, so gameplay scripts never call analytics themselves.
/// Logs: level_start, level_complete, player_death, enemy_killed, arrow_fired, shop_purchase, boss_fight_start, boss_won
/// (schema in docs/HANDOFF_balance_analytics.md; keep the names stable, students query them in BigQuery).
///
/// Scene objects (Health, BossEncounter, BowCombat) are found and subscribed every time a scene loads. They are
/// destroyed with their scene, so nothing has to be unsubscribed. The static events (LevelExit.Used,
/// AltarShopButton.Purchased, EnemyHealth.Killed) are subscribed once at boot and live as long as the game does.
/// </summary>
public static class GameAnalyticsHooks
{
    /// <summary>Scenes that are not levels (matched by substring, see Build Profiles > Scene List). Loading one starts a new run, so the attempt counters reset.</summary>
    private static readonly string[] MenuScenes = { "MainMenu" };

    private const float Decimals = 10f; // Positions and times are rounded to 0.1.
    private const float ChargeDecimals = 100f; // Charge (0..1) is rounded to 0.01.

    // How many times each level was entered in this run. A reload after a death counts as a new attempt.
    private static readonly Dictionary<string, int> attempts = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // With "Enter Play Mode > Reload Domain" off this runs again on every play, so clear and resubscribe.
        attempts.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        LevelExit.Used -= OnLevelExitUsed;
        LevelExit.Used += OnLevelExitUsed;
        AltarShopButton.Purchased -= OnPurchased;
        AltarShopButton.Purchased += OnPurchased;
        EnemyHealth.Killed -= OnEnemyKilled;
        EnemyHealth.Killed += OnEnemyKilled;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (IsMenuScene(scene.name))
        {
            attempts.Clear();
            return;
        }

        attempts.TryGetValue(scene.name, out int attempt);
        attempt++;
        attempts[scene.name] = attempt;
        GameAnalytics.Log("level_start", ("level", scene.name), ("attempt", attempt));

        // Health is only on the player (Adventurer prefab); enemies use EnemyHealth, bosses BossHealth.
        foreach (Health health in Object.FindObjectsByType<Health>())
            health.Died += OnPlayerDied;

        foreach (BossEncounter encounter in Object.FindObjectsByType<BossEncounter>())
        {
            encounter.FightStarted += OnBossFightStarted;
            encounter.Won += OnBossWon;
        }

        foreach (BowCombat bow in Object.FindObjectsByType<BowCombat>())
            bow.ArrowFired += OnArrowFired;
    }

    private static void OnPlayerDied(Health health)
    {
        Vector3 position = health.transform.position;
        GameAnalytics.Log("player_death",
            ("level", CurrentLevel()),
            ("x", Round(position.x)),
            ("y", Round(position.y)),
            ("time_s", LevelTime()),
            ("attempt", CurrentAttempt()));
    }

    private static void OnLevelExitUsed(LevelExit exit)
    {
        // Exits lead both forward and back, so "target" tells which. Students can filter on it.
        GameAnalytics.Log("level_complete",
            ("level", CurrentLevel()),
            ("time_s", LevelTime()),
            ("attempt", CurrentAttempt()),
            ("target", exit.TargetScene));
    }

    private static void OnArrowFired(GameObject arrowPrefab, float charge)
    {
        GameAnalytics.Log("arrow_fired",
            ("level", CurrentLevel()),
            ("arrow", arrowPrefab != null ? arrowPrefab.name : "unknown"),
            ("charge", Mathf.Round(charge * ChargeDecimals) / ChargeDecimals));
    }

    private static void OnPurchased(string kind, string item, int cost)
    {
        GameAnalytics.Log("shop_purchase", ("level", CurrentLevel()), ("kind", kind), ("item", item), ("cost", cost));
    }

    private static void OnEnemyKilled(GameObject enemy)
    {
        Vector3 position = enemy.transform.position;
        GameAnalytics.Log("enemy_killed",
            ("level", CurrentLevel()),
            ("type", EnemyType(enemy.name)),
            ("x", Round(position.x)),
            ("y", Round(position.y)),
            ("time_s", LevelTime()),
            ("attempt", CurrentAttempt()));
    }

    /// <summary>
    /// The prefab name of an enemy, so all copies group together in BigQuery: "MeleeEnemy (3)" and
    /// "MeleeEnemy(Clone)" both become "MeleeEnemy".
    /// </summary>
    private static string EnemyType(string objectName)
    {
        string type = objectName.Replace("(Clone)", "");
        int bracket = type.LastIndexOf(" (", System.StringComparison.Ordinal);
        if (bracket > 0 && type.EndsWith(")")) type = type.Substring(0, bracket);
        return type.Trim();
    }

    private static void OnBossFightStarted()
    {
        GameAnalytics.Log("boss_fight_start", ("level", CurrentLevel()), ("time_s", LevelTime()), ("attempt", CurrentAttempt()));
    }

    private static void OnBossWon()
    {
        GameAnalytics.Log("boss_won", ("level", CurrentLevel()), ("time_s", LevelTime()), ("attempt", CurrentAttempt()));
    }

    /// <summary>True for scenes that are not levels, e.g. the main menu.</summary>
    public static bool IsMenuScene(string sceneName)
    {
        foreach (string menu in MenuScenes)
            if (sceneName.Contains(menu)) return true;
        return false;
    }

    /// <summary>How many times the current level was entered in this run (1 = first try).</summary>
    public static int CurrentAttempt()
    {
        attempts.TryGetValue(CurrentLevel(), out int attempt);
        return attempt;
    }

    private static string CurrentLevel()
    {
        return SceneManager.GetActiveScene().name;
    }

    private static float LevelTime()
    {
        return Round(Time.timeSinceLevelLoad);
    }

    private static float Round(float value)
    {
        return Mathf.Round(value * Decimals) / Decimals;
    }
}
