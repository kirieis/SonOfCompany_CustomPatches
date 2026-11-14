using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("DevEisia.PiggyVarietyMod", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("cucumis.lethalrpg", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("FlipMods.BetterStamina", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Entity378.SellBodiesFixed", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("mrov.WeatherRegistry", BepInDependency.DependencyFlags.SoftDependency)]
    public class CustomPatchesPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.sonofcompany.custompatches";
        public const string ModName = "SonOfCompany Custom Patches";
        public const string ModVersion = "1.3.2";

        public static CustomPatchesPlugin Instance { get; private set; }
        public static ManualLogSource Log => Instance.Logger;
        public static Harmony HarmonyInstance { get; private set; }

        // LethalRPG Strength Configs
        public static ConfigEntry<int> StrengthMaxLevel { get; private set; }
        public static ConfigEntry<float> StrengthInitialStep { get; private set; }

        // Tactical Belt Configs
        public static ConfigEntry<int> BeltDefaultCapacity { get; private set; }
        public static ConfigEntry<int> BeltMaxCapacity { get; private set; }
        public static ConfigEntry<int> BeltSlotUpgradeCost { get; private set; }

        // Memory / Performance
        public static ConfigEntry<bool> AggressiveMemoryCleanup { get; private set; }

        private void Awake()
        {
            Instance = this;

            // Bind Configurations
            StrengthMaxLevel = Config.Bind("LethalRPG - Strength", "MaxLevel", 5, "Maximum level for Strength skill.");
            StrengthInitialStep = Config.Bind("LethalRPG - Strength", "InitialStep_i", 1.0f, "Initial progression increment 'i' (supports up to 2 decimal places, e.g. 1.00).");

            BeltDefaultCapacity = Config.Bind("Tactical Belt", "DefaultCapacity", 3, "Default ammunition capacity for Tactical Belt.");
            BeltMaxCapacity = Config.Bind("Tactical Belt", "MaxCapacity", 6, "Maximum upgraded ammunition capacity for Tactical Belt.");
            BeltSlotUpgradeCost = Config.Bind("Tactical Belt", "SlotUpgradeCost", 50, "Store price per larger belt slot upgrade ($50).");

            AggressiveMemoryCleanup = Config.Bind("Performance", "AggressiveMemoryCleanup", true, "Automatically garbage-collect and unload unused assets between moons and days to stop memory leaks.");

            HarmonyInstance = new Harmony(ModGUID);

            try
            {
                Logger.LogInfo("Applying SonOfCompany Custom Patches v1.3.2...");
                HarmonyInstance.PatchAll(typeof(Patches.PiggyPatches));
                HarmonyInstance.PatchAll(typeof(Patches.LethalRpgPatches));
                HarmonyInstance.PatchAll(typeof(Patches.AnomaliesPatches));
                HarmonyInstance.PatchAll(typeof(Patches.WeatherRegistryPatches));
                HarmonyInstance.PatchAll(typeof(Patches.MaskedEnemySuitCopyPatches));
                HarmonyInstance.PatchAll(typeof(Patches.MemoryLeakFixes));
                HarmonyInstance.PatchAll(typeof(Items.ItemRegistryPatches));

                Logger.LogInfo("All SonOfCompany Custom Patches applied successfully!");

                // Initialize Roulette audio loading
                Patches.RouletteAudioManager.Initialize(this);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying patches: {ex}");
            }
        }
    }
}
