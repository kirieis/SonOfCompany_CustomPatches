using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class MemoryLeakFixes
    {
        // Chỉ chạy dọn dẹp bộ nhớ (garbage collection & unload unused assets) khi ship cất cánh rời khỏi moon
        [HarmonyPatch(typeof(StartOfRound), "ShipHasLeft")]
        [HarmonyPostfix]
        private static void StartOfRound_ShipHasLeft_Postfix()
        {
            if (!CustomPatchesPlugin.AggressiveMemoryCleanup.Value) return;
            TriggerGarbageCollection("Ship Has Left Moon");
        }

        [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
        [HarmonyPostfix]
        private static void StartOfRound_EndOfGame_Postfix()
        {
            if (!CustomPatchesPlugin.AggressiveMemoryCleanup.Value) return;
            TriggerGarbageCollection("End of Game Round");
        }

        public static void TriggerGarbageCollection(string reason)
        {
            try
            {
                CustomPatchesPlugin.Log.LogInfo($"[MemoryOptimizer] Triggering asset unload and garbage collection ({reason})...");
                Resources.UnloadUnusedAssets();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                CustomPatchesPlugin.Log.LogInfo("[MemoryOptimizer] Memory cleanup complete!");
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[MemoryOptimizer] Cleanup encountered error: {ex.Message}");
            }
        }
    }
}

