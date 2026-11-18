# Handoff: runtime JSON balancing + GA4/BigQuery analytics for Babylon Ascension

You are Claude Code working in the BabylonAscension Unity repo (Unity 6000.6.3f1, URP, 2D platformer).
Read `CLAUDE.md` in the repo root first and follow it. The rules that matter most here:

- Work on your own branch. Do **not** commit or push unless the user asks.
- Do not edit `Assets/Code/AIPromptsUsed.txt`.
- You cannot compile or run Unity from the shell. Check C# carefully, use `mcp__claude-vscode__getDiagnostics` if available,
  and ask the user to open the Unity Editor and report compile errors or console warnings.
- Follow the code conventions in `CLAUDE.md` (`[SerializeField] private`, XML summaries that explain why, event-based
  decoupling with unsubscribe, no allocations in per-frame code).
- Simple and robust beats feature-rich. If something here seems more complicated than the problem needs, say so.

## Goal

1. **Balance data as JSON that can change at runtime**, without recompiling: designers edit a JSON file, save, and the running
   game picks the new numbers up within a second. The existing ScriptableObjects (`EntityData`, `ArrowData`, `BossData`,
   `BossAttackData`, `BowUpgrade`) stay the single source of defaults; JSON only overrides the fields it lists.
2. **Event analytics** sent to Google Analytics 4 with the Measurement Protocol (HTTP POST), because the Firebase Unity SDK does
   not support Analytics on desktop. GA4 exports the events to BigQuery, where students at Scholastika analyse the player's
   path through the game for game-design teaching. Every event carries `balance_version`, so two balance variants can be compared.

## Files in this package

The package mirrors the repo layout. Copy `Assets/` and `docs/` into the repo root (merge, do not delete anything).

| File | Purpose |
|---|---|
| `Assets/Code/Scripts/Balance/BalanceRegistry.cs` | ScriptableObject listing every asset that JSON may override. Lives in `Assets/Resources/`. |
| `Assets/Code/Scripts/Balance/BalanceLoader.cs` | Loads `StreamingAssets/Balance/<asset name>.json` before the first scene, hot-reloads on save, restores assets after Editor play mode, exposes `BalanceLoader.Version` (hash of applied JSON). |
| `Assets/Code/Scripts/Balance/Editor/BalanceExporter.cs` | Menu `Tools/Balance/Export baseline JSON from assets`: writes the current numbers of every registered asset to JSON. |
| `Assets/Code/Scripts/Analytics/GameAnalytics.cs` | Measurement Protocol client: queue, batches of 25, per-event timestamps, consent, retries, config from `StreamingAssets/analytics.json`, one-time `first_launch` event with device info. |
| `Assets/Code/Scripts/Analytics/GameAnalyticsHooks.cs` | Subscribes to existing game events and logs `level_start`, `player_death`, `boss_fight_start`, `boss_won`. |
| `Assets/StreamingAssets/analytics.example.json` | Config template. The real `analytics.json` holds the API secret and must **not** be committed. |
| `docs/ga4_babylon_queries.sql` | Example BigQuery queries (funnel, death heatmap, player path, balance comparison). |

**None of this code has been compiled or run.** It was written without access to Unity or .NET. Treat every file as a draft
that you review and fix, and say clearly what you could not verify.

## Tasks

### A. Install and compile

1. Copy the files in. Add to `.gitignore`: `/Assets/StreamingAssets/analytics.json` and `/Assets/StreamingAssets/analytics.json.meta`.
2. Review all C# files against `CLAUDE.md` conventions and the Unity 6 API. Fix compile problems you can see.
3. Ask the user to open Unity and report compile errors. Fix until clean.

### B. Balance JSON

1. Ask the user to create `Assets/Resources/BalanceRegistry.asset` (Create > Babylon Ascension > Balance Registry) and run its context menu
   **Collect all balance assets**. (A ScriptableObject asset needs Unity to create it with a valid GUID.)
2. Ask the user to run `Tools > Balance > Export baseline JSON from assets` in edit mode, and commit the generated
   `Assets/StreamingAssets/Balance/*.json` when they say so. Those files are tracked; they are the designers' working files.
3. Verify with the user (they run the game, you read the console output they paste):
   - Editing e.g. `PlayerData.json` (`maxHealth`, a movement value) and saving changes the value in the running game within about one second.
     Values that are copied once at `Awake` (for example `Health` copies `maxHealth` into `currentHealth`) apply only to new objects. That is expected; do not
     "fix" it unless it is a real problem.
   - After leaving play mode, `git status` shows **no** modified `.asset` files (the loader restores them in the Editor). If it does, fix the restore.
   - A typo in a field name prints a console warning. Invalid JSON keeps the previous values and prints a warning.
4. Things in `BalanceLoader`/`BalanceRegistry` I was **not** sure about and you should check against the Unity docs or by testing:
   - `AssetDatabase.FindAssets("t:BossAttackData")` must also return assets of the derived attack data classes.
   - `JsonUtility.FromJsonOverwrite` on a ScriptableObject only touches the fields present in the JSON, and private `[SerializeField]` fields work (`BowUpgrade.soulCost`).
   - `Application.quitting` fires when leaving play mode in the Editor, so the restore code runs.
   - `Resources.Load` at `BeforeSceneLoad` plus a static reference keeps the assets loaded, so scenes use the same patched instances.
5. Known limits (document them in a short comment or README section, do not build around them): `[Min]`/`[Range]` are not enforced for JSON values;
   enums, strings and prefab references are not exported (balance JSON is for numbers and bools); a deleted JSON file keeps the last values until restart; WebGL is not supported.

### C. Analytics

1. The real config: the user copies `analytics.example.json` to `analytics.json` and fills in the GA4 Measurement ID and the Measurement Protocol API secret
   (the teacher creates the GA4 account, a Web data stream and the secret; see the BigQuery section). Never put the secret in a committed file, in code, or in your replies.
2. **Consent.** `GameAnalytics` drops all events until `GameAnalytics.SetConsent(true)`. Add a minimal first-run prompt in the main menu that shows while
   `GameAnalytics.IsActive && !GameAnalytics.ConsentAnswered` (two buttons, one sentence of text, Czech, mentions that data are anonymous gameplay events). Keep it as small as the existing UI style allows.
3. **Add the missing events** with the smallest possible changes, using `event Action` as the repo does:
   - `BowCombat`: `public event Action<GameObject, float> ArrowFired;` (prefab, charge 0..1), raised in `ReleaseShot` after `Launch`. Log `arrow_fired` with `level`, `arrow` (the prefab name), `charge` (rounded to 2 decimals).
   - `LevelExit`: `public static event Action<LevelExit> Used;` raised in `Load()` before the scene load. Log `level_complete` with `level`, `time_s`, `attempt`.
     (Make `GameAnalyticsHooks.CurrentAttempt()` usable for this, or move the shared helpers into one place.)
   - The altar shop buttons (`ArrowPickButton`, `UpgradePickButton`, probably through their shared base class that calls `Unlock()` after `SoulInventory.TrySpend`):
     a static `Purchased` event with the item name and cost. Log `shop_purchase` with `level`, `kind` (`arrow` or `upgrade`), `item`, `cost`.
   - Subscribe in `GameAnalyticsHooks` (it already subscribes to `Health.Died` and `BossEncounter` events when a scene loads; follow that pattern and the unsubscribe rules).
   - Check `MenuScenes` in `GameAnalyticsHooks` against the real scene names in Build Settings (the main menu scene must be matched).
   - Optional, ask the user first: `enemy_killed` (`type`, `level`, `x`, `y`) and a `cause` parameter for `player_death` (needs `Health` to remember what hit it last).
4. Event schema (keep names stable; students will query them). All events also get `session_id`, `engagement_time_msec`, `seq`, `balance_version`, `build` automatically.

   | Event | Parameters |
   |---|---|
   | `first_launch` | `platform`, `os`, `language`, `connection`, `screen`, `gpu`, `ram_mb`, `cpu_cores` (sent once, when the anonymous id is created; implemented in `GameAnalytics.EnsureInstallEvent`; no device name or device id) |
   | `level_start` | `level`, `attempt` |
   | `level_complete` | `level`, `time_s`, `attempt` |
   | `player_death` | `level`, `x`, `y`, `time_s`, `attempt` |
   | `arrow_fired` | `level`, `arrow`, `charge` |
   | `shop_purchase` | `level`, `kind`, `item`, `cost` |
   | `boss_fight_start`, `boss_won` | `level`, `time_s`, `attempt` |

5. GA4 constraints to respect (from Google's Measurement Protocol reference): at most 25 events per request, 25 parameters per event, event and parameter names up to 40 characters,
   string values up to 100 characters, no parameter names starting with `_`, `firebase_`, `ga_`, `google_`, events older than 72 hours are rejected, and the endpoint
   answers 2xx even for a malformed payload, so wrong payloads fail silently. Booleans are sent as 1/0 because the reference does not say they are accepted.
   Note on `first_launch`: `app_install` and `first_open` are probably reserved GA4 names, hence the custom name. In the debug endpoint also check that `first_launch`
   and its parameters pass. `platform` in the BigQuery export will always be `WEB` (Web stream), which is why the real platform is an own parameter.
   `connection` (`Application.internetReachability`) most likely only distinguishes reachable/not reachable on desktop; keep it, but do not expect Wi-Fi vs cable.
   `client_id` must have the format `<number>.<number>` (e.g. `1234567890.1700000000`) for a Web stream; the debug endpoint rejects anything else (GUIDs included). `NewClientId()` generates it.
6. **Verify the payload, do not trust it.** The collection endpoint does not tell you when something is wrong. Read Google's current "Validate events" guide for the Measurement
   Protocol and validate a sample payload (the `logEvents` setting prints the JSON that is sent). Check in particular that `timestamp_micros` at the **event** level is accepted
   (it is used so each event keeps its exact order), and that `seq` is there as a fallback.

### D. BigQuery (done by the human, you support)

The teacher links the GA4 property to BigQuery (a Google Cloud project with the BigQuery API enabled, Editor role on the property, owner on the Cloud project,
a payment method on the Cloud project, a European dataset location that cannot be changed later, streaming export for live demos). Then:

1. Send a few test events from the Editor with `sendInEditor: true` and `debugMode: true` (adds `debug_mode=1`) and look at them in GA4 DebugView and the Realtime report. Turn `debugMode` off again afterwards.
2. After the export has run, check in BigQuery that: events are there with the expected names and params; `event_timestamp` values are distinct and in the right order;
   numeric params land in `int_value` or `double_value` (the queries use `COALESCE` for this); `balance_version` is filled.
3. Run the queries in `docs/ga4_babylon_queries.sql` (replace `PROJECT` and `PROPERTY_ID`, use Dry run first) and fix them against the real schema. They have not been run on real data.
4. Turn `sendInEditor` and `debugMode` off again so development sessions do not pollute the data.

## Out of scope

- No Firebase SDK, no Unity Gaming Services, no relay server (the API secret is visible in a shipped build; acceptable for school playtests, say so in the README).
- No WebGL support yet. (It needs the config and balance JSON loaded with `UnityWebRequest` in a boot scene.)
- No changes to gameplay numbers, existing assets or scenes beyond what is listed above.
- Do not log personal data: no names, e-mails, free text. Parameters are numbers, level names and item names.

## When you are done

Report briefly: what you changed (files), what you verified, what is still unverified or needs the user in Unity or BigQuery, and any place where you
deviated from this document and why.
