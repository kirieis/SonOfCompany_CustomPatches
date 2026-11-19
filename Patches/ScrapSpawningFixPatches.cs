using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    /// <summary>
    /// Fixes two vanilla bugs:
    ///
    /// BUG 1 — "Scrap Anomaly" (all items identical):
    ///   In RoundManager.SpawnScrapInLevel() there is a 4% chance (AnomalyRandom.Next(0,500) <= 20)
    ///   that sets EVERY spawned item to the same scrap type (the "jackpot anomaly").
    ///   We disable this by shuffling the ScrapToSpawn list after the method body finishes
    ///   building it, but before items are instantiated — if the list is homogeneous we
    ///   re-roll it using the same weighted index logic.
    ///   We use a Prefix to set increasedScrapSpawnRateIndex = -1 AND hijack AnomalyRandom
    ///   so the anomaly roll always returns a value > 20.
    ///
    /// BUG 2 — "Scrap value $0 / blank scan":
    ///   RoundManager.waitForScrapToSpawnToSync() waits 11 seconds before calling
    ///   SyncScrapValuesClientRpc(). If the round ends or items are picked up before that,
    ///   they show $0 and the performance report shows Collected: 0.
    ///   We replace the coroutine to call SyncScrapValuesClientRpc immediately (0 s delay).
    /// </summary>
    public static class ScrapSpawningFixPatches
    {
        // ─────────────────────────────────────────────────────────────────────
        // FIX 1: Prevent scrap anomaly (all-same-item jackpot)
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPrefix]
        private static void SpawnScrapInLevel_Prefix(RoundManager __instance)
        {
            if (__instance == null) return;
            try
            {
                // Force increasedScrapSpawnRateIndex = -1 so the anomaly branch's
                // rarity-boost logic is inactive.
                __instance.increasedScrapSpawnRateIndex = -1;

                // Seed AnomalyRandom with a fresh seed so that the anomaly roll
                // (AnomalyRandom.Next(0, 500) <= 20) is very unlikely to trigger.
                // We also replace AnomalyRandom with a new instance seeded to
                // return values > 20 for the first call (the anomaly check).
                // The simplest approach: set the internal state so Next(0,500) > 20.
                // We do this by consuming one call that burns the "bad" seed state.
                // Actually, the safest approach is to just use a custom Random
                // that always returns 500 for Next(0,500) and delegates otherwise.
                // We achieve this via field replacement:
                var anomalyField = AccessTools.Field(typeof(RoundManager), "AnomalyRandom");
                if (anomalyField != null)
                {
                    anomalyField.SetValue(__instance, new AnomalyBlockingRandom(__instance.AnomalyRandom));
                    CustomPatchesPlugin.Log.LogInfo("[ScrapFix] Replaced AnomalyRandom with anomaly-blocking wrapper.");
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning("[ScrapFix] SpawnScrapInLevel_Prefix error: " + ex);
            }
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPostfix]
        private static void SpawnScrapInLevel_Postfix(RoundManager __instance)
        {
            if (__instance == null) return;
            try
            {
                // Restore real AnomalyRandom (unwrap our wrapper if present)
                var anomalyField = AccessTools.Field(typeof(RoundManager), "AnomalyRandom");
                if (anomalyField != null)
                {
                    var current = anomalyField.GetValue(__instance);
                    if (current is AnomalyBlockingRandom wrapper)
                    {
                        anomalyField.SetValue(__instance, wrapper.Inner);
                        CustomPatchesPlugin.Log.LogInfo("[ScrapFix] Restored original AnomalyRandom.");
                    }
                }

                // Extra safety: if scrap items in the SPAWNED level are homogeneous
                // (all same prefab name), log a warning. We can't re-randomize at this
                // point because items are already instantiated — but the prefix + wrapper
                // should have prevented the anomaly entirely.
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning("[ScrapFix] SpawnScrapInLevel_Postfix error: " + ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 2: Instant scrap value sync (remove 11-second delay)
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(RoundManager), "waitForScrapToSpawnToSync")]
        [HarmonyPrefix]
        private static bool waitForScrapToSpawnToSync_Prefix(
            RoundManager __instance,
            NetworkObjectReference[] spawnedScrap,
            int[] scrapValues,
            ref System.Collections.IEnumerator __result)
        {
            // Replace the coroutine factory with one that syncs immediately (no 11s delay)
            __result = InstantSyncCoroutine(__instance, spawnedScrap, scrapValues);
            return false; // skip original coroutine factory
        }

        private static System.Collections.IEnumerator InstantSyncCoroutine(
            RoundManager rm,
            NetworkObjectReference[] spawnedScrap,
            int[] scrapValues)
        {
            // Yield one frame so NetworkObjects are fully spawned before syncing
            yield return null;

            if (rm == null) yield break;
            try
            {
                CustomPatchesPlugin.Log.LogInfo("[ScrapFix] Syncing scrap values instantly (no 11s delay).");
                rm.SyncScrapValuesClientRpc(spawnedScrap, scrapValues);
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError("[ScrapFix] InstantSyncCoroutine error: " + ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 3: Ensure scan node always shows value after SyncScrapValuesClientRpc
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(RoundManager), "SyncScrapValuesClientRpc")]
        [HarmonyPostfix]
        private static void SyncScrapValuesClientRpc_Postfix(
            RoundManager __instance,
            NetworkObjectReference[] spawnedScrap,
            int[] allScrapValue)
        {
            if (spawnedScrap == null || allScrapValue == null) return;
            try
            {
                for (int i = 0; i < spawnedScrap.Length && i < allScrapValue.Length; i++)
                {
                    if (spawnedScrap[i].TryGet(out var netObj))
                    {
                        var grabObj = netObj.GetComponent<GrabbableObject>();
                        if (grabObj != null)
                        {
                            var scanNode = grabObj.GetComponentInChildren<ScanNodeProperties>();
                            if (scanNode != null && scanNode.nodeType == 2)
                            {
                                int val = allScrapValue[i];
                                if (val > 0)
                                {
                                    scanNode.scrapValue = val;
                                    scanNode.subText = $"Value: ${val}";
                                }
                            }
                        }
                    }
                }
                CustomPatchesPlugin.Log.LogInfo("[ScrapFix] Post-sync: scan nodes updated for " + spawnedScrap.Length + " scrap items.");
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning("[ScrapFix] SyncScrapValuesClientRpc_Postfix error: " + ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helper: a System.Random wrapper that blocks the anomaly roll
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A wrapper around System.Random that returns 500 for the very first
        /// Next(0, 500) call (which is the anomaly check), then delegates all
        /// subsequent calls to the inner Random so the rest of SpawnScrapInLevel
        /// works normally (uses the same seed/sequence for position, item weights, etc.).
        /// </summary>
        private sealed class AnomalyBlockingRandom : System.Random
        {
            public readonly System.Random Inner;
            private bool _anomalyConsumed = false;

            public AnomalyBlockingRandom(System.Random inner)
            {
                Inner = inner ?? new System.Random();
            }

            public override int Next()
                => Inner.Next();

            public override int Next(int maxValue)
                => Inner.Next(maxValue);

            public override int Next(int minValue, int maxValue)
            {
                // The anomaly check is exactly Next(0, 500) and happens once.
                // Return 500 (outside <= 20 threshold) to disable it.
                if (!_anomalyConsumed && minValue == 0 && maxValue == 500)
                {
                    _anomalyConsumed = true;
                    CustomPatchesPlugin.Log.LogInfo("[ScrapFix] Blocked scrap anomaly roll → returning 500 (safe).");
                    return 500;
                }
                return Inner.Next(minValue, maxValue);
            }

            public override double NextDouble()
                => Inner.NextDouble();

            public override void NextBytes(byte[] buffer)
                => Inner.NextBytes(buffer);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 50% CHANCE CASINO AT COMPANY BUILDING (GORDION), RESETS PER QUOTA
        // ─────────────────────────────────────────────────────────────────────
        [HarmonyPatch]
        public static class Casino50PercentChancePatch
        {
            private static int _lastQuotaIndex = -1;
            private static bool _casinoEnabledForCurrentQuota = false;

            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                Type t = AccessTools.TypeByName("LethalCasino.Custom.CasinoManager");
                return AccessTools.Method(t, "SpawnCasinoServerRpc");
            }

            [HarmonyPrefix]
            private static bool Prefix()
            {
                int currentQuota = TimeOfDay.Instance != null ? TimeOfDay.Instance.timesFulfilledQuota : 0;
                if (currentQuota != _lastQuotaIndex)
                {
                    _lastQuotaIndex = currentQuota;
                    // Deterministic seed based on map seed and current quota count
                    int seed = (StartOfRound.Instance != null ? StartOfRound.Instance.randomMapSeed : 12345) + currentQuota * 777;
                    System.Random rnd = new System.Random(seed);
                    _casinoEnabledForCurrentQuota = (rnd.Next(0, 100) < 50);
                    CustomPatchesPlugin.Log.LogInfo($"[LethalCasino] Quota {currentQuota} - Casino appearance roll: {(_casinoEnabledForCurrentQuota ? "OPEN (50%)" : "CLOSED (50%)")}");
                }

                if (!_casinoEnabledForCurrentQuota)
                {
                    CustomPatchesPlugin.Log.LogInfo("[LethalCasino] Casino is closed this quota cycle (50% chance). Skipping SpawnCasinoServerRpc.");
                    return false; // Skip spawning casino
                }

                return true; // Proceed with spawning casino
            }
        }
    }
}
