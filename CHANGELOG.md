# Changelog

## [1.3.0] - 2026-11-10
### Added
- **FairAI (`TheFluff-FairAI-1.6.1`)**: Added FairAI so monsters and traps interact fairly with each other and the environment.
- **Ship Inventory (`LethalCompanyModding-ShipInventoryUpdated-2.0.10`)**: Integrated ship inventory system for organized item storage and retrieval.
- **Piggy Magazine in Store**: Added Rifle "Magazine" to the Terminal Store with default price of $60 (`RifleMagPrice = 60`).

### Changed & Fixed
- **BetterArmory Weapon Animations Fixed**:
  - Patched `InteractionAnimationCoordinator.TryValidateBodyAnimatorOwnership` in `Y4NGZInteractions` to eliminate the `player_animator_owned_externally` rejection.
  - BetterArmory first-person weapon rigs and custom holding poses now activate reliably for all weapons.
- **Removed Assault Rifle from Store**: BetterArmory's Assault Rifle (MX16A4) is completely removed from the Terminal Store (`Purchasable In Shop = false`).
- **Piggy Rifle Ammo Check [Q]**: Pressing Q now displays the exact remaining ammo count (e.g., `[Check ammo : [Q] [18/30]]`) instead of approximate ranges (`<30`, `<20`, `<10`).
- **LethalRPG Strength UI in [J] Menu**:
  - Replaced the outdated percentage reduction display (`-40% > -60% weight`) with exact progression capacity thresholds (`9 lb > 14 lb weight`).
  - Removed outdated `// max reduction: 1` detail lines and updated skill description to clearly reflect the zero-movement-penalty weight threshold mechanism.
- **BetterArmory Fire Axe Rebalanced**:
  - Enemy hit force set to **5**.
  - Swinging consumes **0.20f stamina** (exactly 2x the stamina cost of Piggy's Axe at 0.10f).
- **Removed LateGameUpgrades**: Completely stripped `LateGameUpgrades` and all associated custom silver bullet banishment hooks from the modpack and custom patches.
- **LethalConstellations Routing Fix**: Resolved empty `defaultMoon` entries for generated constellations (`Argon`, `Bloody`, `Fun`, `MoonUnlockable`, `Oldred`, `Temple`, `Trials`, `Volcanic`) ensuring routing confirmation works without errors.

---

## [1.2.0] - 2026-11-09
### Added
- **Anomalies Mod (`Y4NGZ313-Anomalies-1.3.0` & `Y4NGZ313-Y4NGZCore-1.0.13`)**:
  - Integrated new anomalous creatures: Moder, Beetle Bull, Harmonic, Eye Monster, Crooked Choir, Hushdoll, Passenger, Snipjack, and Simon Says.
  - Rebalanced hits required to defeat killable mobs and enforced true unkillable immunity via `SonOfCompany_CustomPatches`.
- **SellBodiesFixed (`Entity378-SellBodiesFixed-1.14.1`)**:
  - Replaced Enhanced Monsters with the more stable SellBodiesFixed, allowing players to drag and sell defeated enemy corpses for scrap credits.
- **New Weapons & Gear**:
  - **Speedloader**: Quickly reloads 6 bullets into Piggy's Revolver when the cylinder is open ($80, 0.5 lb).
  - **Ammo Box**: Pre-packed box containing 4 Speedloaders ($300, 3 lb).
  - **Tactical Belt**: Ammunition belt with custom HUD reserve display, upgradeable via Terminal command `buy larger belt slot` ($50/level, max 6 slots).
- **Ghost Girl Banishment**: Silver Bullets ($500) can now shoot down and banish the Ghost Girl (`DressGirlAI`).

### Changed & Fixed
- **Replaced**: Removed `VELD-Enhanced_Monsters` in favor of `Entity378-SellBodiesFixed`.
- **LethalRPG Strength Rework**:
  - Implemented progressive arithmetic formula: `Strength(L) = 5 + L*i + L*(L-1)` lb carry weight limit.
  - Zero stamina penalty applied while carrying weight within your Strength threshold.
- **Weapon Stats Rebalance**:
  - Piggy Revolver: Distance-scaled damage (close/mid/far) with double damage headshots.
  - Piggy Axe: Sits at an exact average hit force of 2.5 (alternating 2 and 3).
  - BetterArmory: Tuned Grenade Launcher (8m radius), Kar98K, X18 Pistol, RPG-7 (5m/3m), Chainsaw (2 dmg/tick); removed MX16A4 from the Store.
  - Fixed player animator controller glitches when switching between Piggy weapons and BetterArmory firearms.
- **Performance & RAM Leak Fix**:
  - Automatic Garbage Collection (`GC.Collect()`) and visual asset unloading (`Resources.UnloadUnusedAssets()`) every 3 minutes, at round boundaries, during ship takeoff, and when despawning scrap.

---

## [1.1.0] - 2026-10-31
### Added
- **Moons & Variety**: Integrated Wesley's Moons, Wesley's Interiors, Custom Story Logs, and new enemy variants.
- **Upgrades**: Added LateGame Upgrades for deeper end-game progression.
- **Optimization**: Added CullFactory and AsyncLoggers for smoother performance and FPS boost.
- **QoL**: Added Ultrawide monitor support, customizable crosshairs, and TooManySuits.

### Changed & Fixed
- **Modpack Rebrand**: Officially renamed modpack to **SonOfCompany** with a new custom icon.
- **Weapon System & Store Balance**:
  - Renamed Better Armory revolver to **Armory Revolver** (uses *Small Caliber Rounds* from HUD reserve).
  - Added Piggy's manual cylinder **Revolver** (80 credits) and **Revolver Ammo** (20 credits/round) directly to the Terminal Store.
- **Configs Optimized**: Tuned OpenBodyCams (15 FPS cap, shorter render distance) to reduce stuttering on multi-cam ships.
