using System;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class EnemyScalingPatches
    {
        private static int _lastAppliedScrapBoost = 0;

        // ---------------------------------------------------------
        // 1. SPORE LIZARD HP = 30 & MONSTER HP += 1 PER 3 QUOTAS
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        private static void EnemyAI_Start_Scaling(EnemyAI __instance)
        {
            if (__instance == null) return;

            try
            {
                int quotaHPScaling = 0;
                if (TimeOfDay.Instance != null && TimeOfDay.Instance.timesFulfilledQuota > 0)
                {
                    quotaHPScaling = TimeOfDay.Instance.timesFulfilledQuota / 3;
                }

                // Spore Lizard (PufferAI) có HP = 30 hits
                if (__instance is PufferAI || __instance.GetType().Name == "PufferAI")
                {
                    __instance.enemyHP = 30 + quotaHPScaling;
                    CustomPatchesPlugin.Log.LogInfo($"[EnemyScaling] Spore Lizard spawned with {__instance.enemyHP} HP (base 30 + quota scaling {quotaHPScaling}).");
                    return;
                }

                // Mọi monster khác tăng += 1 HP mỗi khi hoàn thành 3 quota
                if (quotaHPScaling > 0)
                {
                    int oldHP = __instance.enemyHP;
                    __instance.enemyHP += quotaHPScaling;
                    CustomPatchesPlugin.Log.LogInfo($"[EnemyScaling] Scaled {__instance.enemyType?.enemyName ?? __instance.name} HP: {oldHP} -> {__instance.enemyHP} (+{quotaHPScaling} from {TimeOfDay.Instance.timesFulfilledQuota} quotas).");
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[EnemyScaling] Error in EnemyAI_Start_Scaling: {ex}");
            }
        }

        // ---------------------------------------------------------
        // 2. SCRAP MIN & MAX += 2 PER 4 QUOTAS
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPrefix]
        private static void SpawnScrapInLevel_QuotaScaling_Prefix(RoundManager __instance)
        {
            _lastAppliedScrapBoost = 0;
            if (__instance == null || __instance.currentLevel == null) return;

            try
            {
                if (TimeOfDay.Instance != null && TimeOfDay.Instance.timesFulfilledQuota >= 4)
                {
                    int cycles = TimeOfDay.Instance.timesFulfilledQuota / 4;
                    _lastAppliedScrapBoost = cycles * 2;

                    if (_lastAppliedScrapBoost > 0)
                    {
                        __instance.currentLevel.minScrap += _lastAppliedScrapBoost;
                        __instance.currentLevel.maxScrap += _lastAppliedScrapBoost;
                        CustomPatchesPlugin.Log.LogInfo($"[EnemyScaling] Moon {__instance.currentLevel.PlanetName} min/max scrap increased by +{_lastAppliedScrapBoost} for round ({TimeOfDay.Instance.timesFulfilledQuota} quotas fulfilled).");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[EnemyScaling] Error in SpawnScrapInLevel_QuotaScaling_Prefix: {ex}");
            }
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPostfix]
        private static void SpawnScrapInLevel_QuotaScaling_Postfix(RoundManager __instance)
        {
            if (__instance == null || __instance.currentLevel == null || _lastAppliedScrapBoost <= 0) return;

            try
            {
                // Restore base values on ScriptableObject so boosts don't permanently stack
                __instance.currentLevel.minScrap -= _lastAppliedScrapBoost;
                __instance.currentLevel.maxScrap -= _lastAppliedScrapBoost;
                _lastAppliedScrapBoost = 0;
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[EnemyScaling] Error restoring moon scrap values: {ex}");
            }
        }
    }
}
