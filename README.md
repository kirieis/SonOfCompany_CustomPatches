# Son of company — Custom Patches (Algorithm Fixes)

This folder contains two modified `.dll` files with updated algorithms and compatibility fixes for the **SonOfCompanys** modpack.

## 1. Modified Files

### `Cucumis-LethalRPG/LethalRPG.dll`

- **Updated Strength calculation:** Each Strength level now increases the weight-carrying capacity by an additional **5 lb (cumulative)**:
  - Level 1: +5 lb carrying capacity
  - Level 2: +10 lb carrying capacity
  - Level 3: +15 lb carrying capacity
  - Level 4: +20 lb carrying capacity
  - Level 5: +25 lb carrying capacity
- Preserves the original LethalRPG configuration and all 20+ other skills.

### `FlipMods-BetterStamina/BetterStamina.dll`

- Fixed a transpiler conflict with LethalRPG to ensure the Strength weight-calculation algorithm works correctly.
- Eliminates in-game warnings related to this compatibility issue.

---

## 2. Installation Guide

### Method 1: Thunderstore Mod Manager

1. Open Thunderstore Mod Manager and select the profile containing the **SonOfCompanys** modpack.
2. Click **Settings** (the gear icon on the left), then select **Browse profile folder**.
3. In the opened directory, navigate to `BepInEx/plugins/`.
4. Copy the two folders from this package (`Cucumis-LethalRPG` and `FlipMods-BetterStamina`) into the `plugins` directory.
5. When prompted, choose **Replace** to overwrite the existing files.
6. Launch the game and enjoy the updated mods!

### Method 2: Manual Installation

1. Open your Lethal Company installation directory.
2. Navigate to `BepInEx/plugins/`.
3. Copy the two folders (`Cucumis-LethalRPG` and `FlipMods-BetterStamina`) into the `plugins` directory.
4. When prompted, choose **Replace** to overwrite the existing files.
5. Launch Lethal Company and play as usual!
