using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class LevelLoadingWatchdogPatches
    {
        private static Coroutine watchdogCoroutine = null;

        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPostfix]
        private static void StartGame_Postfix(StartOfRound __instance)
        {
            if (__instance == null) return;
            if (watchdogCoroutine != null)
            {
                __instance.StopCoroutine(watchdogCoroutine);
            }
            watchdogCoroutine = __instance.StartCoroutine(LevelLoadWatchdogRoutine(__instance));
            CustomPatchesPlugin.Log.LogInfo("[Watchdog] Level loading watchdog timer started.");
        }

        private static IEnumerator LevelLoadWatchdogRoutine(StartOfRound round)
        {
            float timer = 0f;
            float maxWait = 25f; // Generous timeout to allow normal generation on large dungeons

            while (timer < maxWait)
            {
                if (round == null) yield break;

                if (round.shipHasLanded)
                {
                    CustomPatchesPlugin.Log.LogInfo("[Watchdog] Ship landed normally.");
                    yield break;
                }

                var rm = RoundManager.Instance;
                if (rm != null)
                {
                    // If server, ensure local host player is marked as finished once dungeon completes
                    bool dCompleted = false;
                    try
                    {
                        dCompleted = (bool)(AccessTools.Field(typeof(RoundManager), "dungeonCompletedGenerating")?.GetValue(rm) ?? false);
                    }
                    catch { }

                    if (dCompleted && rm.IsServer)
                    {
                        var list = AccessTools.Field(typeof(RoundManager), "playersFinishedGeneratingFloor")?.GetValue(rm) as List<ulong>;
                        if (list != null)
                        {
                            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
                            if (!list.Contains(localId))
                            {
                                list.Add(localId);
                            }
                        }
                    }
                }

                timer += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            // If we exceeded timeout and ship is still frozen in atmosphere
            if (!round.shipHasLanded)
            {
                CustomPatchesPlugin.Log.LogWarning("[Watchdog] Level load timed out! Forcing dungeon completion and enemy initialization...");
                var rm = RoundManager.Instance;
                if (rm != null)
                {
                    AccessTools.Field(typeof(RoundManager), "dungeonCompletedGenerating")?.SetValue(rm, true);
                    AccessTools.Field(typeof(RoundManager), "dungeonIsGenerating")?.SetValue(rm, false);
                    AccessTools.Field(typeof(RoundManager), "dungeonFinishedGeneratingForAllPlayers")?.SetValue(rm, true);

                    try
                    {
                        var list = AccessTools.Field(typeof(RoundManager), "playersFinishedGeneratingFloor")?.GetValue(rm) as List<ulong>;
                        list?.Clear();
                    }
                    catch { }

                    // CRUCIAL: Initialize enemy vents and lights so enemies can spawn!
                    try
                    {
                        rm.RefreshEnemyVents();
                        rm.RefreshLightsList();
                        AccessTools.Method(typeof(RoundManager), "RefreshEnemiesList")?.Invoke(rm, null);
                        AccessTools.Method(typeof(RoundManager), "ResetEnemySpawningVariables")?.Invoke(rm, null);
                    }
                    catch (Exception ex)
                    {
                        CustomPatchesPlugin.Log.LogError("[Watchdog] Error refreshing enemy vents/lists: " + ex);
                    }

                    try
                    {
                        AccessTools.Method(typeof(RoundManager), "FinishGeneratingNewLevelClientRpc")?.Invoke(rm, null);
                    }
                    catch (Exception ex)
                    {
                        CustomPatchesPlugin.Log.LogError("[Watchdog] FinishGeneratingNewLevelClientRpc exception: " + ex);
                    }
                }

                // Dismiss loading text and loading animation
                try
                {
                    if (HUDManager.Instance != null)
                    {
                        if (HUDManager.Instance.loadingText != null) HUDManager.Instance.loadingText.enabled = false;
                        if (HUDManager.Instance.LoadingScreen != null) HUDManager.Instance.LoadingScreen.SetBool("IsLoading", false);
                    }
                }
                catch { }

                // Ensure openingDoorsSequence runs
                try
                {
                    round.StartCoroutine(round.openingDoorsSequence());
                }
                catch (Exception ex)
                {
                    CustomPatchesPlugin.Log.LogError("[Watchdog] Error forcing openingDoorsSequence: " + ex);
                }
            }
        }

        // Safety fallback: ensure enemy vents and spawning are ALWAYS ready when level finishes
        [HarmonyPatch(typeof(RoundManager), "FinishGeneratingNewLevelClientRpc")]
        [HarmonyPostfix]
        private static void FinishGeneratingNewLevelClientRpc_Postfix(RoundManager __instance)
        {
            if (__instance == null) return;
            try
            {
                if (__instance.IsServer)
                {
                    if (__instance.allEnemyVents == null || __instance.allEnemyVents.Length == 0)
                    {
                        CustomPatchesPlugin.Log.LogInfo("[Watchdog] allEnemyVents was empty, refreshing vents...");
                        __instance.RefreshEnemyVents();
                    }
                    AccessTools.Method(typeof(RoundManager), "RefreshEnemiesList")?.Invoke(__instance, null);
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError("[Watchdog] FinishGeneratingNewLevelClientRpc_Postfix error: " + ex);
            }
        }

        // Ensure RoundManager.Update triggers enemy spawning if vents are present
        [HarmonyPatch(typeof(RoundManager), "Update")]
        [HarmonyPrefix]
        private static void RoundManager_Update_Prefix(RoundManager __instance)
        {
            if (__instance == null || !__instance.IsServer) return;

            // If time is past 85f and enemies haven't begun spawning, verify vents
            var timeScript = __instance.timeScript;
            if (timeScript != null && timeScript.currentDayTime > 85f && !__instance.begunSpawningEnemies)
            {
                if (__instance.allEnemyVents == null || __instance.allEnemyVents.Length == 0)
                {
                    __instance.RefreshEnemyVents();
                }
            }
        }

        // Fix scan sometimes not displaying scrap value:
        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        private static void GrabbableObject_Start_Postfix(GrabbableObject __instance)
        {
            if (__instance == null) return;
            var scanNode = __instance.GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null && scanNode.nodeType == 2)
            {
                EnsureScanNodePrice(scanNode);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "SetScrapValue")]
        [HarmonyPostfix]
        private static void GrabbableObject_SetScrapValue_Postfix(GrabbableObject __instance, int setValueTo)
        {
            if (__instance == null) return;
            var scanNode = __instance.GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null && scanNode.nodeType == 2)
            {
                scanNode.scrapValue = setValueTo;
                scanNode.subText = $"Value: ${setValueTo}";
            }
        }

        [HarmonyPatch(typeof(HUDManager), "AssignNodeToUIElement")]
        [HarmonyPrefix]
        private static void HUDManager_AssignNodeToUIElement_Prefix(ScanNodeProperties node)
        {
            EnsureScanNodePrice(node);
        }

        private static void EnsureScanNodePrice(ScanNodeProperties node)
        {
            if (node == null || node.nodeType != 2) return;

            // If subText is missing or doesn't show value, resolve from parent GrabbableObject
            if (string.IsNullOrEmpty(node.subText) || !node.subText.Contains("Value:"))
            {
                var grabbable = node.GetComponentInParent<GrabbableObject>();
                if (grabbable != null)
                {
                    int val = grabbable.scrapValue;
                    if (val <= 0 && grabbable.itemProperties != null)
                    {
                        val = grabbable.itemProperties.minValue;
                    }
                    if (val > 0)
                    {
                        node.scrapValue = val;
                        node.subText = $"Value: ${val}";
                    }
                }
            }
        }
    }
}
