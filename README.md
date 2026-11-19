# SonOfCompany Custom Patches

**Version:** `1.4.0` | **BepInEx Plugin** | **Lethal Company v81+**

> A curated patch plugin bundled with the **SonOfCompany** modpack. It fixes vanilla engine bugs, resolves cross-mod conflicts, rebalances store items, and adds new gameplay mechanics that tie together PiggyVarietyMod, LethalRPG, CombatMode, and the Oxygen mod into a cohesive experience.

---

## Table of Contents
1. [Requirements](#requirements)
2. [Installation](#installation)
3. [What's New in v1.4.0](#whats-new-in-v140)
4. [Feature Reference](#feature-reference)
   - [Bug Fixes](#bug-fixes)
   - [Store & Item Changes](#store--item-changes)
   - [Russian Roulette (Revolver)](#russian-roulette-revolver)
   - [Execution Bullet / Execution Revolver](#execution-bullet--execution-revolver)
   - [Ammo as Scrap](#ammo-as-scrap)
   - [Enemy Scaling](#enemy-scaling)
   - [Interior Oxygen Stations](#interior-oxygen-stations)
   - [CombatMode Enhancement](#combatmode-enhancement)
   - [LethalRPG Rebalance](#lethalrpg-rebalance)
   - [Custom Items](#custom-items)
5. [Configuration](#configuration)
6. [Soft Dependencies](#soft-dependencies)
7. [Changelog](#changelog)
8. [Known Issues & Compatibility](#known-issues--compatibility)

---

## Requirements

| Dependency | Version | Required? |
|---|---|---|
| BepInEx | 5.4.x | ✅ Required |
| PiggyVarietyMod | any | ⚠️ Soft (gun patches need it) |
| LethalRPG (Cucumis) | any | ⚠️ Soft (RPG rebalance needs it) |
| BetterStamina (FlipMods) | any | ⚠️ Soft |
| SellBodiesFixed (Entity378) | any | ⚠️ Soft |
| WeatherRegistry (mrov) | any | ⚠️ Soft |
| CombatMode (Borgez) | any | ⚠️ Soft (toggle + Brawler synergy) |
| Oxygen (consequential) | any | ⚠️ Soft (interior oxy stations) |
| LethalLevelLoader | 1.17.3+ | ⚠️ Soft (compat patches auto-apply) |

> All soft dependencies are **optional**. The plugin loads cleanly without them — only the patches for the missing mod are skipped.

---

## Installation

### Mod Manager (Recommended)
Install the **SonOfCompany** modpack from Thunderstore. This plugin is bundled automatically.

### Manual Installation
1. Download the latest release zip from Thunderstore.
2. Extract the zip — it contains three BepInEx plugin folders:
   ```
   BepInEx/plugins/
   ├── SonOfCompany_CustomPatches/
   │   ├── SonOfCompany_CustomPatches.dll
   │   └── roulette_spin.mp3
   ├── Cucumis-LethalRPG/
   │   └── LethalRPG.dll
   └── FlipMods-BetterStamina/
       └── BetterStamina.dll
   ```
3. Drop all three folders into your `BepInEx/plugins/` directory.
4. Launch the game — the plugin will generate its config file on first run at:
   ```
   BepInEx/config/com.sonofcompany.custompatches.cfg
   ```

---

## What's New in v1.4.0

| Category | Change |
|---|---|
| **New Item** | Execution Bullet — \$9999 store item, instant-kill special ammo |
| **Revolver Mechanic** | Roulette trigger key changed **E → R** |
| **Revolver Mechanic** | All HUD popup tips removed for immersion |
| **Enemy** | Spore Lizard HP fixed to **30 hits** |
| **Scaling** | Monster HP **+1** per 3 quotas fulfilled |
| **Scaling** | Moon scrap min/max **+2** per 4 quotas fulfilled |
| **Interior** | Oxygen Stations spawn in B+ hazard interiors |
| **CombatMode** | Toggle mode (press once) instead of hold |
| **CombatMode** | Brawler Lv3 synergy: **+1 punch damage** in CombatMode |
| **LethalRPG** | Marksman now applies to **all guns** (Shotgun, Revolver, Rifle) |
| **LethalRPG** | Unique/Ultimate XP requirement **+25** across all skills |
| **Store** | Jet Wings removed from store |
| **Store** | Zappinator price → **\$650** |
| **Store** | Revolver ammo, Rifle magazine, Shotgun shell → **scrap only** (removed from store) |
| **Fix** | Scrap Anomaly (all-same-item jackpot) completely blocked |
| **Fix** | Scrap value \$0 / blank scan node fixed (instant sync, 11s delay removed) |

---

## Feature Reference

### Bug Fixes

#### Scrap Anomaly Fix
Vanilla Lethal Company has a 4% chance in `RoundManager.SpawnScrapInLevel()` to trigger an "anomaly" that sets every scrap spawned to the **same item type** (e.g., 15 wine bottles). This patch:
- Wraps `AnomalyRandom` with a custom `System.Random` subclass that returns `500` for the first `Next(0, 500)` call (the anomaly check threshold is ≤ 20), permanently disabling the jackpot branch.
- Also forces `increasedScrapSpawnRateIndex = -1` before each spawn to prevent the rarity-boost side effect.

#### Scrap Value $0 / Blank Scan Fix
Vanilla waits **11 seconds** in a coroutine (`waitForScrapToSpawnToSync`) before calling `SyncScrapValuesClientRpc`. If you pick up items or the round ends in that window, scrap shows \$0 and the Performance Report shows "Collected: 0".
- This patch replaces the coroutine to call `SyncScrapValuesClientRpc` **immediately** (0 s delay).
- A postfix also updates `ScanNodeProperties.scrapValue` and `subText` directly on sync.

#### LevelLoading Watchdog
A watchdog timer detects hung level loads (stuck on "Loading...") and forces a recovery, preventing indefinite black screens after joining or travelling between moons.

#### LethalLevelLoader 1.17.3 Compatibility
Applies targeted Harmony patches to handle API changes between LethalLevelLoader versions so the modpack loads cleanly on 1.17.3+.

#### LethalModDataLib NullReference Fix
Prevents the `NullReferenceException` in `ModDataAttributeCollector.RegisterModDataAttributes()` that fires on game initialization when no mod data attributes are registered yet.

#### MaskedEnemy Suit Copy Fix
Fixes the bug where Masked enemies copy the wrong player suit texture, causing visual corruption.

#### Memory Leak / Aggressive Cleanup
Between moon trips and new days, forces a `GC.Collect()` and `Resources.UnloadUnusedAssets()` to free textures, audio clips, and prefabs that the vanilla game leaks between scenes.

---

### Store & Item Changes

| Item | Change |
|---|---|
| Revolver | \$888 |
| M16A Rifle | \$999 |
| Double-Barreled Shotgun | \$800 |
| Zappinator | \$650 |
| Jet Wings | ❌ Removed from store |
| Revolver Ammo | ❌ Removed from store (scrap only) |
| Rifle Magazine | ❌ Removed from store (scrap only) |
| Shotgun Shell | ❌ Removed from store (scrap only) |
| **Execution Bullet** | ✅ **NEW** — \$9999 |

> BetterArmory's duplicate Revolver / Assault Rifle store entries are automatically cleaned up to prevent duplicate listings.

---

### Russian Roulette (Revolver)

The vanilla PiggyVarietyMod Revolver roulette mechanic is enhanced:

- **Trigger key changed: `E` → `R`** — press `R` while holding the Revolver (with cylinder closed, 1 bullet loaded) to initiate roulette.
- All HUD popup tips and tutorial messages about roulette are **removed** for immersion.
- Dry-fire click sound still plays; the cylinder advances normally.
- The roulette condition and shot probability are unchanged from vanilla PiggyVarietyMod.

---

### Execution Bullet / Execution Revolver

A new special ammunition available in the terminal store for **\$9999**.

#### How it Works
1. **Buy** an Execution Bullet from the terminal.
2. **Reload** the Execution Bullet into your Revolver.
3. The Revolver becomes an **Execution Revolver**:
   - 🔴 Roulette mechanic is **disabled** — no cylinder-spin risk.
   - 🔴 Only additional **Execution Bullets** can be reloaded (no regular ammo).
   - 🔴 Any shot fired deals **instant death** to **ALL monsters**, including normally immortal enemies.
4. When all Execution Bullets are spent, the Revolver **immediately reverts** to a standard Revolver and regular ammo can be reloaded again.

---

### Ammo as Scrap

Ammunition no longer appears in the terminal store. Instead, it spawns as scrap during moon exploration:

| Item | Scrap Rarity |
|---|---|
| Revolver Bullet | 70 (common) |
| Rifle Magazine | 70 (common) |
| Shotgun Shell | 50 (uncommon) |

Higher rarity = more frequent appearance in moon scrap pools.

---

### Enemy Scaling

#### Spore Lizard HP
Spore Lizard (PufferAI) base HP is set to **30 hits** (vanilla is 3).

#### Progressive Monster Scaling
Every time the quota is fulfilled:
- **Per 3 quotas** — All monster max HP increases by **+1**.
- **Per 4 quotas** — All moons' `minScrap` and `maxScrap` increase by **+2** for that round (restored after spawn so the ScriptableObject is not permanently modified).

This scaling applies on `EnemyAI.Start()`, meaning each newly spawned enemy gets the current quota-adjusted HP.

---

### Interior Oxygen Stations

When the Oxygen mod is installed, stationary refill stations spawn inside interiors on moons with **hazard level B or higher** (B, A, S, S+).

| Property | Value |
|---|---|
| Uses per station | 2 |
| Oxygen refilled per use | 50% of max tank |
| Movable? | ❌ No (kinematic, immovable) |
| Spawns on | B, A, S, S+ hazard moons only |
| Interaction | Press `E` to refill |

Stations are built at runtime using the Oxygen mod's existing charger prefab (accessed via reflection, no hard dependency).

---

### CombatMode Enhancement

When the CombatMode mod is installed, two enhancements apply:

#### Toggle Mode
Instead of **holding** the stance key to stay in Combat Mode, pressing the key once **toggles** Combat Mode on/off. The stance key is whatever you configured in CombatMode's config (default: `Left Alt`).

Configurable — set `ToggleStance = false` in `com.sonofcompany.custompatches.cfg` to revert to hold behavior.

#### Brawler Lv3 Synergy
When a player has **Brawler at Level 3** in LethalRPG and is actively in CombatMode stance, punch damage gains **+1** per hit.

---

### LethalRPG Rebalance

#### Marksman — All Guns
Marksman skill now reduces reload time for **all gun-type items**: Shotgun, Revolver, and M16A Rifle (not just Shotgun as vanilla).

The skill description in the LethalRPG UI is updated to reflect this:
> *"Reload all guns faster (Shotgun, Revolver, Rifle)."*

#### Strength — Custom Formula
Strength carry weight uses an **arithmetic progression** formula instead of fixed increments:

```
Capacity(L) = 5 + L × i + L × (L - 1)
```

where `i` is configurable (default `1.0`). At max level 5 with default `i`:
- Lv1: 5 lb → Lv2: 7 lb → Lv3: 11 lb → Lv4: 17 lb → Lv5: 25 lb

Only weight **above** the Strength threshold causes movement penalty.

#### Unique / Ultimate XP +25
All Unique and Ultimate tier skills require **+25 XP** more than their vanilla default. This applies in the buy screen, skill menu sorting, and XP cost display.

---

### Custom Items

| Item | Description |
|---|---|
| **Speedloader** | Reload all 6 Revolver chambers at once instead of one-by-one |
| **Tactical Belt** | Wearable ammo pouch; starts at 3 slots, upgradable to 6 via terminal (`buy larger belt slot` for \$50/slot) |
| **Ammo Box** | Consumable box containing multiple ammo rounds |
| **Execution Bullet** | Special \$9999 store item — instant-death for all monsters when loaded into a Revolver |

---

## Configuration

The plugin generates `BepInEx/config/com.sonofcompany.custompatches.cfg` on first launch:

```ini
[LethalRPG - Strength]
## Maximum level for Strength skill.
MaxLevel = 5

## Initial progression increment 'i' (supports up to 2 decimal places).
InitialStep_i = 1.0

[Tactical Belt]
## Default ammunition capacity for Tactical Belt.
DefaultCapacity = 3

## Maximum upgraded ammunition capacity for Tactical Belt.
MaxCapacity = 6

## Store price per larger belt slot upgrade.
SlotUpgradeCost = 50

[Performance]
## Automatically garbage-collect and unload unused assets between moons.
AggressiveMemoryCleanup = true

[CombatMode]
## If true, pressing the stance key toggles Combat Mode on/off instead of holding.
ToggleStance = true
```

A comprehensive modpack-wide config is also available at `BepInEx/config/SOC_config_general.cfg`, which provides a human-readable overview of all mod configs in the SonOfCompany modpack.

---

## Soft Dependencies

The plugin uses `[BepInDependency(..., SoftDependency)]` for all optional mods. If a soft dependency is not installed, its patches are skipped silently — no errors, no crashes.

| GUID | Mod |
|---|---|
| `DevEisia.PiggyVarietyMod` | PiggyVarietyMod (guns, roulette) |
| `cucumis.lethalrpg` | LethalRPG |
| `FlipMods.BetterStamina` | BetterStamina |
| `Entity378.SellBodiesFixed` | SellBodiesFixed |
| `mrov.WeatherRegistry` | WeatherRegistry |
| `Borgez.CombatMode` | CombatMode |
| `consequential.Oxygen` | Oxygen |

---

## Changelog

### v1.4.0 (2026-11-19)
- **NEW**: Execution Bullet item (\$9999 store) + Execution Revolver mechanic (instant kill all monsters, no roulette, immortal enemies included)
- **NEW**: Interior Oxygen Stations (spawn in B+ hazard moons, 2 uses, 50% refill, immovable)
- **NEW**: Enemy scaling — Monster HP +1 per 3 quotas, moon scrap +2 per 4 quotas
- **NEW**: Spore Lizard HP = 30 (was 3)
- **NEW**: CombatMode toggle (press once to activate/deactivate instead of hold)
- **NEW**: Brawler Lv3 synergy — +1 punch damage in CombatMode
- **FIX**: Scrap Anomaly (all items same type jackpot) completely blocked via AnomalyRandom wrapper
- **FIX**: Scrap value \$0 / blank scan node — 11s delay removed, instant sync
- **CHANGE**: Roulette trigger key E → R
- **CHANGE**: All Roulette HUD popup tips removed
- **CHANGE**: Marksman applies to all guns (Shotgun, Revolver, Rifle) + UI description updated
- **CHANGE**: Unique/Ultimate XP requirement +25
- **CHANGE**: Revolver ammo, Rifle magazine, Shotgun shell removed from store (scrap-only drop)
- **CHANGE**: Jet Wings removed from store
- **CHANGE**: Zappinator \$650
- **CHANGE**: Revolver \$888, Rifle \$999, Shotgun \$800

### v1.3.3
- SetScrapValue parameter fix (HarmonyX "setValue" patch)
- LethalModDataLib NullReference fix
- LevelLoading watchdog timer
- LethalLevelLoader 1.17.3 compatibility
- MaskedEnemy suit copy fix
- Memory leak / aggressive cleanup between moons
- WeatherRegistry patches
- Anomalies (EyeMonster, BeetleBull, SimonSays) compatibility

### v1.3.0
- Initial public release
- Strength formula rebalance, Tactical Belt, Speedloader, Ammo Box
- BetterArmory duplicate cleanup

---

## Known Issues & Compatibility

| Issue | Status |
|---|---|
| WeatherTweaks warning: `AccessTools.Method: Could not find method for type TimeOfDay and name Instance` | Harmless warning from WeatherTweaks itself; not caused by this plugin |
| 618_Eventide bundle path warning (`AccretionMoonConfig`) | Cosmetic warning from 618_Eventide mod; no gameplay impact |
| Oxygen Station appearing outside intended moons | Fixed in v1.4.0 (hazard level check via reflection) |
| CombatMode toggle may desync on clients in multiplayer | Known; CombatMode's own network sync handles stance — toggle state is local only |

> If you encounter a bug, please include your full `BepInEx/LogOutput.log` and describe the steps to reproduce it.

---

*Made with ♥ for the SonOfCompany crew. — yuugito*
