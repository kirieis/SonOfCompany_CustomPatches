# SonOfCompany — Custom Patches & Fixes

This repository contains the standalone custom patches plugin and compatibility fixes for the **SonOfCompany** modpack (Lethal Company v81).

The main plugin **`SonOfCompany_CustomPatches.dll`** is designed as an **independent standalone patch mod** placed directly into the `BepInEx/plugins/` directory. When the game launches, the plugin automatically hooks and overrides default mods and dependencies downloaded by the modpack without requiring any direct modification to their original source code.

---

## 1. New Patch Features (`SonOfCompany_CustomPatches.dll`)

### 🔫 PiggyVarietyMod (Weapons & Ammunition)
- **Russian Roulette Mechanic**:
  - Exclusive to Piggy's Revolver when loaded with **exactly 1 bullet** and cylinder closed.
  - Press **[E]** to spin the cylinder: plays custom ratchet spin sound (`roulette_spin.mp3`) and places the bullet randomly into 1 of the 6 chambers.
  - Can only be spun once per cylinder cycle. Opening cylinder (**[Q]**) immediately cancels the roulette cycle.
  - Triggering an empty chamber plays a dry-fire click, advances the chamber sequentially, and consumes no ammo.
  - Triggering the loaded chamber fires the shot, deals full damage, and consumes the bullet.
- **Distance-Based Revolver Monster Damage**:
  - `≤ 10m`: **5 damage**
  - `> 10m - <= 25m`: **4 damage**
  - `> 25m`: **3 damage**
- **Piggy M16A Rifle**:
  - Renamed from "Rifle" to **"M16A Rifle"** in the Terminal Store to avoid conflict with `rifle rounds`.
  - Available for **$240** ($200 + $40) via terminal command: `buy m16a` or `buy m16a rifle`.
  - Magazine available for **$60**.
  - Pressing **[Q]** displays exact remaining ammo loaded (e.g., `[Check ammo : [Q] [18/30]]`).
- **Piggy Axe**: Balanced hit force alternating between 2 and 3 to achieve an exact average of **2.5**. Consumes 0.10f stamina.
- **Speedloader Item**: Quickly reloads 6 bullets into the Revolver cylinder when opened (`R: to use speedloader`). Price: $80, Weight: 0.5 lb.
- **Ammo Box Item**: Ammo case containing 4 Speedloaders. Price: $300, Weight: 3 lb.
- **Animation Conflict Fix**: Automatically restores the player's original animator controller when stowing/dropping PiggyVarietyMod weapons or switching to BetterArmory, resolving pose lock bugs.

### 🛡️ BetterArmory & Store
- **Weapon Statistics & Store Prices**:
  - Fire Axe: Hit force = 5, stamina consumption = 0.20f (2x Piggy Axe). Price: $150.
  - Grenade Launcher: Outer blast radius extended to 8m.
  - Kar98K (Bolt-action): 60 body / 140 headshot (close), 40 body / 80 headshot (far). Store price: **$205** ($165 + $40).
  - Heavy Shotgun: Store price: **$150** ($110 + $40).
  - X18 Pistol: 28 body / 68 headshot (close), 18 body / 40 headshot (far). Store price: **$125**.
  - Double-Barreled Shotgun: Store price: **$706** ($666 + $40).
  - Flame Thrower: Store price: **$160** ($120 + $40).
  - RPG-7: 5m kill radius, 3m splash damage radius. Store price: **$500** (unchanged).
  - Chainsaw: 2 damage / tick. Price: $400.
  - Removed MX16A4 (Assault Rifle) and BetterArmory duplicate Revolver from the Terminal Store (`Purchasable In Shop = false`).
- **Terminal Store Index & Price Synchronization**:
  - Dynamically synchronizes all `buy` command keywords and `buyItemIndex` pointers in the Terminal with `buyableItemsList`.
  - Fixes the issue where selecting Pistol at $80 wrongly prompted a $500 confirmation for an RPG-7. All items now correctly purchase their intended item at their exact listed price.
- **Independent Save Bridge**: Automatically resolves and stores reserve ammo keys based directly on the active save file name (`currentSaveFileName`), eliminating `SaveIdentityApi` missing warnings.

### ⚡ Performance & Memory Optimization
- Memory leak protection and unused asset unloading now triggers **only when the ship leaves a moon** (`ShipHasLeft`), eliminating periodic 3-minute freeze and stutter spikes during exploration.

### 🎒 Tactical Belt (Ammo Belt & HUD)
- Wearable ammunition belt featuring a custom HUD slot displaying loaded ammo type and current count.
- Default capacity: 3 ammunition/magazine slots (RPG-7 rockets excluded).
- Upgrade capacity up to a maximum of 6 slots directly in the Terminal via command: `buy larger belt slot` ($50/level).

### 📈 LethalRPG (Strength Skill Overhaul)
- **Cumulative Progression Formula**:
  $$\text{Strength}(L) = 5 + L \times i + L \times (L - 1)$$
  (where $L$ is the Strength level, with initial progression step $i = 1$):
  - Level 0: 5 lb free weight
  - Level 1: 6 lb free weight
  - Level 2: 9 lb free weight
  - Level 3: 14 lb free weight
  - Level 4: 21 lb free weight
  - Level 5: 30 lb free weight
- **Excess Weight Penalty Mechanic**: Carrying weight within your Strength capacity incurs **zero stamina penalty**. Only weight exceeding your Strength threshold receives vanilla carry penalties.
- Detailed weight breakdown displayed in Daily Status Reports.

### 👾 Anomalies (Creature HP & Killability Balance)
- Adjusted hits required to eliminate monsters:
  - **Moder**: 40 hits
  - **Beetle Bull**: 12 hits
  - **Harmonic**: 6 hits
  - **Eye Monster**: 8 hits
  - **Crooked Choir**: 10 hits
- **Unkillable Entities**:
  - **Passenger** (`PoltergeistAI`), **Snipjack** (`ScissorsCreatureAI`), **Simon Says** (`SimonSaysAI`), and **Hushdoll** (`CreepyDollAI`) are completely immune to damage and death calls, preserving their true vanilla horror behavior.

### ⚡ RAM Optimization & Memory Leak Prevention
- Automatically triggers Garbage Collection (`GC.Collect()`) and unloads unused visual assets (`Resources.UnloadUnusedAssets()`):
  - Periodically every 3 minutes in-game.
  - At round start and round end, during ship takeoff from moons, and when despawning scrap items.
  - Keeps memory usage flat and prevents cumulative RAM growth from crashing long expeditions.

---

## 2. Installation Guide (How to Patch)

Your modpack installed via Thunderstore/Mod Manager automatically downloads vanilla dependencies and base mods. This patch overrides them in memory and adds all new features.

### Method 1: Using Thunderstore Mod Manager / r2modman (Recommended)
1. Open Thunderstore Mod Manager and select your **SonOfCompany** profile.
2. Click **Settings** (gear icon on the left) -> Select **Browse profile folder**.
3. In the opened folder, navigate to: `BepInEx/plugins/`.
4. Copy the `SonOfCompany_CustomPatches/` folder (containing `SonOfCompany_CustomPatches.dll`) from this repository into `BepInEx/plugins/`.
5. *(Optional for legacy standalone patches)*: Overwrite existing DLLs in `BetterArmory/`, `Cucumis-LethalRPG/`, and `FlipMods-BetterStamina/` with their counterparts from this repo.
6. Click **Start Modded** and enjoy!

### Method 2: Manual Installation
1. Navigate to your Lethal Company directory (or standalone modded game folder).
2. Go to: `Lethal Company/BepInEx/plugins/`.
3. Copy the `SonOfCompany_CustomPatches/` folder into `BepInEx/plugins/`.
4. Launch the game normally.

---

## 3. Repository Structure
- `SonOfCompany_CustomPatches/`: Contains `SonOfCompany_CustomPatches.dll` (all-in-one standalone patch mod).
- `Cucumis-LethalRPG/`: Standalone modified LethalRPG build with the updated Strength formula.
- `FlipMods-BetterStamina/`: Standalone modified BetterStamina build with Transpiler conflict resolution.
- `BetterArmory/`: Standalone modified BetterArmory build with individual save key support.
