# CLAUDE.md

Babylon Ascension is a 2D side-scrolling action platformer with a bow, made in **Unity 6 (6000.6.3f1)** with URP. The player charges and fires different arrow types at enemies. The level is built flat on the XY plane. A "tower bend" shader can curve it for display.

## Team / repo rules (from README.md)
- Don't push without the team knowing. Pull `main` before you start, and work on your own branch.
- Only commit or push when the user asks.
- `Assets/Code/AIPromptsUsed.txt` is a log of the AI prompts the team has used. Don't edit it unless asked.

## Building / running
- There is no command-line build or test setup. Work happens in the Unity Editor. Scripts compile when the Editor reloads, so Claude can't compile or run the game from the shell. Check C# changes carefully, and use `mcp__claude-vscode__getDiagnostics` if it's available.
- The only scene in Build Settings is `Assets/Scenes/PlaytestLevel3.0.unity`, the current playtest level. `MetricZoo.unity` is a sandbox for measuring movement and platforms. The other `PlaytestLevel*` and `PlayerCurving` scenes are older or experimental.
- Fixed timestep is 0.02 s. All gameplay (movement, enemy AI, projectiles) runs in `FixedUpdate`.

## Assemblies (important)
There are three assemblies, and they reference each other in only one direction:

1. **`BabylonAscension.EntityData`** (`Assets/Code/Scripts/EntityData/*.asmdef`) contains only `EntityData.cs`. It can't reference any other game code.
2. **`com.ta-david-yu.2d-platformer-hunter`**, an *embedded, modified* package in `Packages/` (namespace `DYP`). It references `Unity.InputSystem` and `BabylonAscension.EntityData`.
3. **`Assembly-CSharp`** holds everything else in `Assets/` (no asmdef). It can use both of the assemblies above.

So package code can use `EntityData`, but it **can't** see `ArrowData`, `BowCombat`, `EnemyBehavior` and similar types. Package code talks to game code through events, for example `OnArrowSelected` and `OnDash`.

## The modified platformer package (`Packages/com.ta-david-yu.2d-platformer-hunter/Runtime/Core`)
This started as the open-source 2D Platformer Hunter controller, but the team has changed it a lot. Treat it as project code.
- `BasicMovementController2D` (namespace `DYP`) is the kinematic character controller that the **player and every ground enemy** use. Its settings come from an `EntityData` asset (`applyEntityData`, refreshed through `EntityData.Changed`). Added features: `AddImpulse` (recoil, capped by `recoilMaxPlanarSpeed`), `ApplyKnockback` (stun plus lift), crouch and sliding (`IsSliding`), `SetFrozen`, `ResetForRespawn`, `AimFacingDirection` (lets a dash go toward the cursor), and `SelectArrow` → `OnArrowSelected`.
- `CharacterMotor2D` / `Raycaster` set `transform.position` directly in FixedUpdate. Use `MotionInterpolator` for smooth visuals, not Rigidbody2D interpolation.
- `PlayerBasicMovementInput` uses the new Input System (`Assets/InputSystem_Actions.inputactions`). Move, Jump, Sprint (dash) and Crouch work as expected. Keys 1–5 select arrows through the actions `Previous`, `Next`, `ThirdArrow`, `FourthArrow` and `FifthArrow`.
- Ladders use `Ladder`, `LadderZoneSetter` and `Area2D`, with reference scenes in the package's `Samples/`.

## Game code (`Assets/Code/Scripts`)
**Data (ScriptableObjects, tuned in the Inspector):**
- `EntityData` holds every tunable number for a character: health, the controller's movement, jump, wall and recoil settings, enemy AI, and the per-attack sections (Slash, Jump Attack, Charge, Flight). Each field has a tooltip. Assets: `PlayerData`, `MeleeEnemyData`, `ShooterEnemyData`, `HumbabaEnemyData 1`, `KililiData`. Add new character tuning values here, with a `[Tooltip]`.
- `ArrowData` covers per-arrow damage, speed, gravity, knockback and recoil, each as a min/max pair interpolated by charge. It also covers bounces, penetrations, explosion (`explosionRadius > 0`) and poison (`poisonTime > 0`). Assets in `ArrowData/`: Basic, Bouncing, Explosive, Penetration, Poison. Every arrow prefab has its own `ArrowData`.
- Code must **never modify these shared assets at runtime**. Copy the values into fields first, as `ArrowProjectile` does with bounces. The exception is `DebugMenu`, which deliberately writes to `PlayerData` and then calls `NotifyChanged()`.

**Player** (`Assets/Prefabs/Adventurer.prefab`, layer **"Triggers"**, tag "Player"):
- `BowCombat` aims at the mouse on the XY plane. Holding the left mouse button charges the shot (it must be held at least `minDrawTime`). It spawns the selected arrow prefab, calls `ArrowProjectile.Launch(dir, charge, shooter)`, and applies recoil through `AddImpulse`.
- `PlayerFacing` flips the body sprite toward the cursor and sets `AimFacingDirection`.
- `Player/AdventurerAnimationController` connects the controller's events to the Animator.
- `MotionInterpolator` smooths visual children between physics steps. Call `Snap()` after teleporting.
- `Health` (implements `IDamageable`) raises `Died`, which `GameManager` handles.

**Enemies** (prefabs: `Enemy`, `MeleeEnemy`, `ShooterEnemy`, `HumbabaEnemy`, `Kilili`; layer "Enemy"):
- `EnemyBehavior` is one shared brain for all enemies (~1300 lines). It's a FixedUpdate state machine with the states Patrol, Chase, Attack, JumpAttack, Charge, Stunned, Flying and FlyingHome. Movement goes through the same `BasicMovementController2D`. Chasing remembers which platforms the player stood on, then simulates jump arcs to plan routes (`PlanRoute` → `TryFindRoute` / `TryFindRouteTowardPlayer` / `TryPredictRoute`). Getting damaged makes the enemy chase.
- Enemy types are put together from components, not subclasses:
  - `EnemyAttack` (abstract) is the basic attack. It handles windup and cooldown, and subclasses override `PerformAttack` or `PerformAttackRoutine`. `ProjectileAttack` fires `EntityData.attackEffectPrefab`, which must have `ArrowProjectile`. `SlashAttack` swings a `DamageHitbox` around a pivot.
  - Optional special attacks: `JumpAttack` (leap), `ChargeAttack` (Humbaba's bull charge, which stuns the enemy if it hits a wall), and `EnemyFlight` (Kilili). `EnemyFlight` switches off the ground controller and moves the enemy itself.
- `EnemyHealth` (implements `IDamageable`) sits on a **child** of the enemy. It raises `Damaged` and destroys the root that has the `EnemyBehavior`.

**Combat plumbing:**
- `IDamageable.TakeDamage(float)` (declared in `Health.cs`) is how anything takes damage. To find a target from a collider, try `GetComponentInParent`, and if that fails, try `GetComponentInChildren` under the attached Rigidbody2D.
- `ArrowProjectile` is used by both the player and enemies. It has no Rigidbody or collider. Each FixedUpdate it moves itself with a circle cast along its path, so thin walls can't be skipped and kinematic bodies still get hit. It handles bounces (Environment layer), penetration, explosions with falloff and cover checks, poison, and knockback. Enemy shots use `enemyShotHitLayers` and calculate a ballistic launch angle.
- `DamageHitbox` is the melee hitbox. It checks overlaps every physics step instead of relying on trigger callbacks, because kinematic-vs-kinematic contacts send no trigger events. It damages each target at most once per `Activate`. Put it on the "Ignore Raycast" layer.
- `PoisonEffect` is added to the target at runtime. It deals damage over time, and hitting the target again resets the timer instead of stacking.

**Other:**
- `GameManager` (execution order -2000): Escape pauses (`timeScale = 0`). When the player dies, the screen fades out, the scene reloads (resetting enemies), and the screen fades back in. Gameplay scripts check `Time.timeScale == 0f` to ignore input while paused.
- `DebugMenu`: Tab toggles a runtime panel, built in code with legacy UGUI, for editing `PlayerData` values. It also respawns enemies that were killed, from snapshots taken in `Start`. Add rows with `AddSection`, `AddFloatRow`, `AddIntRow`, `AddBoolRow` and `AddButton`.
- `BasicDashModule` (namespace `DYP`) is a ScriptableObject dash module that follows a speed curve.
- `Assets/TowerBend/` contains `TowerBendController` and the shaders that curve the flat level into a cylinder for display only. `Assets/Code/Shaders/` holds `Curving.shader`, a sprite shadow shader graph, and the URP renderer assets.
- Loose files in the `Assets/` root (`intro.cs`, `intro end.cs`, test materials, images) are intro and experimental leftovers.

## Physics layers
Environment (6), PlayerTrigger (7), **Triggers (8, the player's layer)**, Enemy (10), Projectile (11), plus Player (3), which exists but isn't the Adventurer's layer. Scripts fill in empty layer masks in `Awake` (for example `LayerMask.GetMask("Enemy", "Environment")`). When adding hit logic that should reach the player, include both "Player" and "Triggers". The 2D and 3D collision matrices live in `ProjectSettings/Physics2DSettings.asset` and `DynamicsManager.asset`.

## Code conventions
- Inspector fields are `[SerializeField] private`, with `[Tooltip]`, `[Header]` and `[Min]`/`[Range]`. Expose them through read-only properties (`public bool IsX => ...`).
- Game code uses camelCase private fields. Package code (`DYP`) uses the `m_` prefix and `s_` for statics; follow whichever style the file already uses.
- Use XML `/// <summary>` comments on classes and non-obvious methods to explain *why*. Name magic numbers as `private const` with a comment.
- Validate references in `Awake`, log the error with `Debug.LogError(..., this)`, and disable the component or fall back to a default.
- Use event-based decoupling (`event Action`), and always unsubscribe in `OnDisable`/`OnDestroy`.
- Reuse static buffers for physics queries (`List<RaycastHit2D>`, `ContactFilter2D`) instead of allocating every frame.
- When moving or renaming assets or scripts, keep the `.meta` files together with them. Scene and prefab references depend on the GUIDs.
