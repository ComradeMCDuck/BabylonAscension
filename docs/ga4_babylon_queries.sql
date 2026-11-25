-- Babylon Ascension: example BigQuery queries over the GA4 export.
-- UNTESTED: written against the documented GA4 export schema, not run on real data. Use "Dry run" in the BigQuery console first.
--
-- Replace PROJECT and PROPERTY_ID with your own: `PROJECT.analytics_PROPERTY_ID.events_*`
-- Events sent by the game (see docs/HANDOFF_balance_analytics.md). Every event also carries:
--   balance_version (string), build (string), seq (int, order within a session), session_id, engagement_time_msec
--   first_launch     platform, os, language, connection, screen, gpu (strings), ram_mb, cpu_cores (numbers); once per anonymous id
--   level_start      level (string), attempt (int)
--   level_complete   level, time_s (number), attempt, target (string: destination scene; exits also lead back), active_upgrades
--   player_death     level, x, y (numbers), time_s, attempt, active_upgrades
--   enemy_killed     level, type (string: enemy prefab name, e.g. MeleeEnemy), x, y, time_s, attempt   (bosses: see boss_won)
--   arrow_fired      level, arrow (string), charge (0..1, 2 decimals)
--   shop_purchase    level, kind (arrow|upgrade), item (string), cost (int, genii)
--   boss_fight_start / boss_won   level, time_s, attempt, active_upgrades
--   upgrade_toggled  level, item (upgrade asset name), result (activated|deactivated|slots_full), active_count (int), active_upgrades
--   active_upgrades = sorted comma-separated active upgrade names, e.g. "QuickDraw,Vitality" ("" = none)
-- balance_version is BalanceLoader.Version, so every row knows which JSON balance it was played with.
--
-- Daily tables are events_YYYYMMDD. For a live demo in class (streaming export enabled) query
-- `PROJECT.analytics_PROPERTY_ID.events_intraday_*` with the same SQL, without the _TABLE_SUFFIX filter.
-- Numbers can arrive as int_value or double_value depending on how they were written in the JSON,
-- so numeric params are read with COALESCE(value.double_value, CAST(value.int_value AS FLOAT64)).


-- 1) Funnel: how many players started and finished each level, in the order the levels were first seen.
SELECT
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level') AS level,
  COUNT(DISTINCT IF(event_name = 'level_start', user_pseudo_id, NULL)) AS players_started,
  COUNT(DISTINCT IF(event_name = 'level_complete', user_pseudo_id, NULL)) AS players_completed,
  SAFE_DIVIDE(
    COUNT(DISTINCT IF(event_name = 'level_complete', user_pseudo_id, NULL)),
    COUNT(DISTINCT IF(event_name = 'level_start', user_pseudo_id, NULL))) AS completion_rate
FROM `PROJECT.analytics_PROPERTY_ID.events_*`
WHERE _TABLE_SUFFIX BETWEEN '20261111' AND '20261231'
  AND event_name IN ('level_start', 'level_complete')
GROUP BY level
ORDER BY MIN(event_timestamp);


-- 2) Where do players die? Deaths per level in 2-meter columns (a text heatmap of difficulty spikes).
SELECT
  level,
  x_bucket,
  COUNT(*) AS deaths
FROM (
  SELECT
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level') AS level,
    FLOOR((SELECT COALESCE(value.double_value, CAST(value.int_value AS FLOAT64))
           FROM UNNEST(event_params) WHERE key = 'x') / 2) * 2 AS x_bucket
  FROM `PROJECT.analytics_PROPERTY_ID.events_*`
  WHERE _TABLE_SUFFIX BETWEEN '20261111' AND '20261231'
    AND event_name = 'player_death'
)
GROUP BY level, x_bucket
ORDER BY level, x_bucket;


-- 3) One player's path through the game, in order: level_start:L1 > player_death:L1 > player_death:L1 > level_complete:L1 > ...
SELECT
  user_pseudo_id,
  STRING_AGG(CONCAT(event_name, IFNULL(CONCAT(':', level), '')), ' > ' ORDER BY event_timestamp, seq) AS path
FROM (
  SELECT
    user_pseudo_id,
    event_name,
    event_timestamp,
    (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'seq') AS seq,
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level') AS level
  FROM `PROJECT.analytics_PROPERTY_ID.events_*`
  WHERE _TABLE_SUFFIX BETWEEN '20261111' AND '20261231'
    AND event_name IN ('level_start', 'level_complete', 'player_death')
)
GROUP BY user_pseudo_id
ORDER BY user_pseudo_id;


-- 4) Did the balance change work? Deaths per attempt and median clear time, per balance version and level.
WITH events AS (
  SELECT
    event_name,
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level') AS level,
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'balance_version') AS balance_version,
    (SELECT COALESCE(value.double_value, CAST(value.int_value AS FLOAT64)) FROM UNNEST(event_params) WHERE key = 'time_s') AS time_s
  FROM `PROJECT.analytics_PROPERTY_ID.events_*`
  WHERE _TABLE_SUFFIX BETWEEN '20261111' AND '20261231'
    AND event_name IN ('level_start', 'level_complete', 'player_death')
)
SELECT
  level,
  balance_version,
  COUNTIF(event_name = 'level_start') AS attempts,
  SAFE_DIVIDE(COUNTIF(event_name = 'player_death'), COUNTIF(event_name = 'level_start')) AS deaths_per_attempt,
  APPROX_QUANTILES(IF(event_name = 'level_complete', time_s, NULL), 100)[OFFSET(50)] AS median_clear_time_s
FROM events
GROUP BY level, balance_version
ORDER BY level, balance_version;
