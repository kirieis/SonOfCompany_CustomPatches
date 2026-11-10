# SonOfCompany — Custom Patches & Fixes

This repository contains the standalone custom patches plugin and compatibility fixes for the **SonOfCompany** modpack (Lethal Company v81).

The main plugin **`SonOfCompany_CustomPatches.dll`** is designed as an **independent standalone patch mod** placed directly into the `BepInEx/plugins/` directory. When the game launches, the plugin automatically hooks and overrides default mods and dependencies downloaded by the modpack without requiring any direct modification to their original source code.

---

## 1. New Patch Features (`SonOfCompany_CustomPatches.dll`)

### 🔫 PiggyVarietyMod (Weapons & Ammunition)
- **Distance-Based Revolver Damage**:
  - Monster damage based on distance: `<10m`: 4.5 hits | `<25m`: 3.5 hits | `>=25m`: 2.0 hits (fractional damage accumulates across shots).
  - Player damage: `<10m`: 55 HP | `<25m`: 35 HP | `>=25m`: 20 HP (Headshot multiplier doubles damage to 150 / 100 / 70 HP).
- **Piggy Axe**: Balanced hit force alternating between 2 and 3 to achieve an exact average of **2.5**.
- **Speedloader Item**: Quickly reloads 6 bullets into the Revolver cylinder when opened (`R: to use speedloader`). Price: $80, Weight: 0.5 lb.
- **Ammo Box Item**: Ammo case containing 4 Speedloaders. Price: $300, Weight: 3 lb.
- **Animation Conflict Fix**: Automatically restores the player's original animator controller when stowing/dropping PiggyVarietyMod weapons or switching to BetterArmory, resolving pose lock bugs.

### 🛡️ BetterArmory & Store
- **Weapon Statistics Rebalance**:
  - Grenade Launcher: Outer blast radius extended to 8m.
  - Kar98K (Bolt-action): 60 body / 140 headshot (close), 40 body / 80 headshot (far).
  - X18 Pistol: 28 body / 68 headshot (close), 18 body / 40 headshot (far).
  - RPG-7: 5m kill radius, 3m splash damage radius.
  - Chainsaw: 2 damage / tick.
  - Removed MX16A4 (Assault Rifle) from the Terminal Store.
- **Silver Bullets ($500)**: Can shoot down and banish the Ghost Girl (`DressGirlAI`).
- **Independent Save Bridge**: Automatically resolves and stores reserve ammo keys based directly on the active save file name (`currentSaveFileName`), eliminating `SaveIdentityApi` missing warnings.

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
