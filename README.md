Rules to use the repository:
1. Don't push any changes without the knowledge of the team.
2. Before working on your project in a branch, update from the main branch to stay up to date. (Branch -> Update from main)
3. Work in your designated branches

Try and perform tasks 2 and 3 everytime before you start working and 3rd a few times during long work time

## Balance JSON (runtime tuning without recompiling)

- `Assets/Resources/BalanceRegistry.asset` lists the data assets (`EntityData`, `ArrowData`, `BossData`, boss attack data, `BowUpgrade`) that JSON may override. Context menu **Collect all balance assets** fills it.
- `Tools > Balance > Export baseline JSON from assets` (edit mode) writes `Assets/StreamingAssets/Balance/<asset name>.json`. Those files are committed; designers edit them.
- A JSON file only needs the fields it changes. Saving it while the game runs applies it within about a second. A typo in a field name prints a console warning; invalid JSON keeps the previous values.
- In the Editor the assets are restored when play mode ends, so `git status` should never show modified `.asset` files from this.
- Limits: `[Min]`/`[Range]` are not enforced for JSON values; only numbers, bools and Vector2 are exported; a deleted file keeps the last values until restart; values copied once at `Awake` (e.g. current health) only change for new objects; WebGL is not supported.

## Analytics (GA4 Measurement Protocol)

- Copy `Assets/StreamingAssets/analytics.example.json` to `analytics.json` and fill in the GA4 Measurement ID and API secret. `analytics.json` is git-ignored: **never commit the secret**. The secret is visible in a shipped build, which is acceptable for school playtests only.
- Without `analytics.json` analytics is off. In the Editor events are only printed unless `sendInEditor` is true (turn it off again afterwards, so development sessions do not pollute the data). `validateOnly` posts to GA4's debug endpoint and prints its validation messages instead of recording events. `debugMode` tags events with `debug_mode=1` so they appear in GA4 DebugView; turn it off for real playtests.
- There is no consent prompt; `analytics.json` alone decides whether events are sent (`enabled`). Only anonymous gameplay events are sent: level names, item names and numbers, plus `balance_version` (hash of the applied balance JSON) so balance variants can be compared.
- Events: `first_launch` (once per anonymous id, with device info: platform, OS, language, screen, GPU, RAM, CPU cores; no device name or id), `level_start`, `level_complete`, `player_death`, `enemy_killed`, `arrow_fired`, `shop_purchase`, `boss_fight_start`, `boss_won`. Schema and BigQuery examples: `docs/HANDOFF_balance_analytics.md`, `docs/ga4_babylon_queries.sql`.
- Events queued in the last few seconds before the game quits may be lost (there is no blocking flush on quit).
