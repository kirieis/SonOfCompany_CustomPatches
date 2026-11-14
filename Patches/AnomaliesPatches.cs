using System;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using Y4NGZBeetleBull.AI;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class AnomaliesPatches
    {
        // ---------------------------------------------------------
        // 1. BEETLE BULL REBALANCE
        // Spawn Count: 2 to 4 max
        // Speed: +25% (Walk 1.76, Charge 8.75)
        // Attack Damage: 90 HP
        // Attack Interval / Cooldown: -20% (CalmAfterSeconds 8.0s)
        // ---------------------------------------------------------
        private static void SetConfigValue<T>(object cfg, string propName, T val)
        {
            if (cfg == null) return;
            try
            {
                var entry = AccessTools.Property(cfg.GetType(), propName)?.GetValue(cfg);
                if (entry != null)
                {
                    AccessTools.Property(entry.GetType(), "Value")?.SetValue(entry, val);
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(BeetleBullAI), "Start")]
        [HarmonyPostfix]
        private static void BeetleBullAI_Start_Postfix(BeetleBullAI __instance)
        {
            if (__instance == null) return;

            __instance.enemyHP = 12;

            try
            {
                var cfg = AccessTools.Field(typeof(Y4NGZBeetleBull.Plugin), "ModConfig")?.GetValue(null);
                if (cfg != null)
                {
                    SetConfigValue(cfg, "MaxCount", 4);
                    SetConfigValue(cfg, "RamDamage", 90);
                    SetConfigValue(cfg, "WalkSpeed", 1.76f);
                    SetConfigValue(cfg, "ChargeSpeed", 8.75f);
                    SetConfigValue(cfg, "CalmAfterSeconds", 8.0f);
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[Anomalies] Failed to tune BeetleBullConfig: {ex}");
            }
        }

        [HarmonyPatch(typeof(BeetleBullAI), "get_RamDamage")]
        [HarmonyPrefix]
        private static bool BeetleBullAI_get_RamDamage_Prefix(ref int __result)
        {
            __result = 90; // Luôn gây 90 sát thương khi ram trúng người chơi
            return false;
        }

        [HarmonyPatch(typeof(BeetleBullAI), "get_ChargeSpeed")]
        [HarmonyPrefix]
        private static bool BeetleBullAI_get_ChargeSpeed_Prefix(ref float __result)
        {
            __result = 8.75f; // +25% tốc độ húc (gốc là 7.0f)
            return false;
        }

        // ---------------------------------------------------------
        // 2. THIẾT LẬP MÁU (HITS) BAN ĐẦU CHO CÁC MOB KHÁC TRONG ANOMALIES
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        private static void EnemyAI_Start_Postfix(EnemyAI __instance)
        {
            if (__instance == null) return;

            string typeName = __instance.GetType().FullName ?? "";

            // Moder: 40 hits
            if (typeName.Contains("ModerAI") || __instance is Y4NGZModer.AI.ModerAI)
            {
                __instance.enemyHP = 40;
                CustomPatchesPlugin.Log.LogInfo($"[Anomalies] Set Moder HP to 40 hits");
            }
            // Harmonic: 6 hits
            else if (typeName.Contains("HarmonicAI") || __instance is Y4NGZHarmonic.AI.HarmonicAI)
            {
                __instance.enemyHP = 6;
                CustomPatchesPlugin.Log.LogInfo($"[Anomalies] Set Harmonic HP to 6 hits");
            }
            // Eye Monster: 8 hits
            else if (typeName.Contains("EyeMonsterAI") || __instance is EyeMonster.AI.EyeMonsterAI)
            {
                __instance.enemyHP = 8;
                CustomPatchesPlugin.Log.LogInfo($"[Anomalies] Set Eye Monster HP to 8 hits");
            }
            // Crooked Choir: 10 hits
            else if (typeName.Contains("CrookedChoirAI") || __instance is Y4NGZCrookedChoir.AI.CrookedChoirAI)
            {
                __instance.enemyHP = 10;
                CustomPatchesPlugin.Log.LogInfo($"[Anomalies] Set Crooked Choir HP to 10 hits");
            }
        }

        // ---------------------------------------------------------
        // 3. XỬ LÝ HIT ENEMY: UNKILLABLE & KILLABLE ĐÚNG QUY CHUẨN
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(EnemyAI), "HitEnemy")]
        [HarmonyPrefix]
        private static bool EnemyAI_HitEnemy_Prefix(EnemyAI __instance, ref int force, PlayerControllerB playerWhoHit)
        {
            if (__instance == null) return true;

            string typeName = __instance.GetType().FullName ?? "";

            // CÁC MOB UNKILLABLE: Passenger, Snipjack, Simon Says, Hushdoll
            if (typeName.Contains("PoltergeistAI") || __instance is Y4NGZPoltergeist.AI.PoltergeistAI ||
                typeName.Contains("ScissorsCreatureAI") || __instance is Y4NGZScissorsCreature.ScissorsCreatureAI ||
                typeName.Contains("SimonSaysAI") || __instance is SimonSays.AI.SimonSaysAI ||
                typeName.Contains("CreepyDollAI") || __instance is Y4NGZCreepyDoll.CreepyDollAI)
            {
                CustomPatchesPlugin.Log.LogInfo($"[Anomalies] Prevented damage to unkillable mob: {typeName}");
                return false;
            }

            return true;
        }

        // Đảm bảo Passenger, Snipjack, Simon Says, Hushdoll không thể bị gọi KillEnemy
        [HarmonyPatch(typeof(EnemyAI), "KillEnemy")]
        [HarmonyPrefix]
        private static bool EnemyAI_KillEnemy_Prefix(EnemyAI __instance)
        {
            if (__instance == null) return true;

            string typeName = __instance.GetType().FullName ?? "";
            if (typeName.Contains("PoltergeistAI") || __instance is Y4NGZPoltergeist.AI.PoltergeistAI ||
                typeName.Contains("ScissorsCreatureAI") || __instance is Y4NGZScissorsCreature.ScissorsCreatureAI ||
                typeName.Contains("SimonSaysAI") || __instance is SimonSays.AI.SimonSaysAI ||
                typeName.Contains("CreepyDollAI") || __instance is Y4NGZCreepyDoll.CreepyDollAI)
            {
                return false;
            }

            return true;
        }
    }
}
