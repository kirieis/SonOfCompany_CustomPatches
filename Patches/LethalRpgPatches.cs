using System;
using System.Reflection;
using System.Text;
using GameNetcodeStuff;
using HarmonyLib;
using LethalRPG;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class LethalRpgPatches
    {
        // ---------------------------------------------------------
        // 4.1 & 4.2 CÔNG THỨC MỚI - CẤP SỐ CỘNG VỚI i TĂNG DẦN
        // ---------------------------------------------------------
        public static float CalculateStrengthCapacity(int level)
        {
            float baseL0 = 5.0f;
            if (level <= 0)
                return baseL0;

            float i0 = CustomPatchesPlugin.StrengthInitialStep?.Value ?? 1.0f;
            // Strength(L) = 5 + L * i0 + L * (L - 1)
            float capacity = baseL0 + (level * i0) + (level * (level - 1));
            return (float)Math.Round(capacity, 2);
        }

        public static float GetPlayerStrengthCapacity(PlayerControllerB player)
        {
            if (player == null) return 5.0f;
            int level = RpgState.Level(player, UpgradeType.Strength);
            return CalculateStrengthCapacity(level);
        }

        // ---------------------------------------------------------
        // 4.3 WEIGHT PENALTY - CHỈ TÍNH PHẦN VƯỢT MỨC
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RpgState), "AdjustCarryWeight")]
        [HarmonyPrefix]
        private static bool AdjustCarryWeight_Prefix(float weight, PlayerControllerB player, ref float __result)
        {
            if (weight <= 1.0f)
            {
                __result = 1.0f;
                return false;
            }

            float totalWeightLb = Mathf.Max(0f, (weight - 1.0f) * 105f);
            float strengthCapacityLb = GetPlayerStrengthCapacity(player);

            if (totalWeightLb <= strengthCapacityLb)
            {
                __result = 1.0f;
                return false;
            }

            float excessLb = totalWeightLb - strengthCapacityLb;
            __result = 1.0f + (excessLb / 105f);
            return false;
        }

        [HarmonyPatch(typeof(RpgState), "EffectiveCarryWeight")]
        [HarmonyPrefix]
        private static bool EffectiveCarryWeight_Prefix(PlayerControllerB player, ref float __result)
        {
            if (player == null)
            {
                __result = 1.0f;
                return false;
            }
            __result = RpgState.AdjustCarryWeight(player.carryWeight, player);
            return false;
        }

        // ---------------------------------------------------------
        // 4.4 UI LETHALRPG - STRENGTH & MARKSMAN & UNIQUE/ULTIMATE XP
        // ---------------------------------------------------------
        private static bool _xpAdjusted = false;

        public static void ApplySkillsUi()
        {
            try
            {
                // Strength UI
                var strDef = Upgrades.Get(UpgradeType.Strength);
                if (strDef != null)
                {
                    strDef.Description = "Carry weight up to Strength threshold has zero movement penalty.";
                    strDef.FormatTotal = (float effect, int level) =>
                    {
                        float cap = CalculateStrengthCapacity(level);
                        return $"{cap:0.##} lb weight";
                    };
                    strDef.Params = Array.Empty<SkillParam>();
                }

                // Marksman UI: Áp dụng lên mọi items thuộc dạng gun
                var marksmanDef = Upgrades.Get(UpgradeType.Marksman);
                if (marksmanDef != null)
                {
                    marksmanDef.Description = "Reload all guns faster (Shotgun, Revolver, Rifle).";
                }

                // Unique / Ultimate XP requirement += 25
                if (!_xpAdjusted && Upgrades.All != null)
                {
                    _xpAdjusted = true;
                    foreach (var def in Upgrades.All)
                    {
                        if (def != null && (def.Category == UpgradeCategory.Unique || def.Category == UpgradeCategory.Ultimate))
                        {
                            def.DefaultXpPrice += 25;
                        }
                    }
                    CustomPatchesPlugin.Log.LogInfo("[LethalRPG] Applied +25 XP requirement to all Unique and Ultimate skills.");
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[LethalRPG] Error updating skills UI: {ex}");
            }
        }

        // Patch RpgSettings.XpPrice to guarantee +25 XP in all menu, sorting, and buy calculations
        [HarmonyPatch]
        public static class RpgSettings_XpPrice_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                Type t = AccessTools.TypeByName("LethalRPG.RpgSettings");
                return AccessTools.Method(t, "XpPrice", new Type[] { typeof(UpgradeType) });
            }

            [HarmonyPostfix]
            private static void Postfix(UpgradeType type, ref int __result)
            {
                try
                {
                    var def = Upgrades.Get(type);
                    if (def != null && (def.Category == UpgradeCategory.Unique || def.Category == UpgradeCategory.Ultimate))
                    {
                        __result += 25;
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        private static void StartOfRound_Awake_Postfix()
        {
            ApplySkillsUi();
        }

        [HarmonyPatch]
        public static class SkillMenu_FormatUpgrade_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target() => AccessTools.Method("LethalRPG.SkillMenu:FormatUpgrade");

            [HarmonyPrefix]
            private static bool Prefix(UpgradeDef def, object settings, int level, bool maxed, ref string __result)
            {
                if (def != null && def.Type == UpgradeType.Strength)
                {
                    if (maxed)
                    {
                        __result = $"{CalculateStrengthCapacity(level):0.##} lb weight";
                    }
                    else if (level <= 0)
                    {
                        __result = $"NEXT: {CalculateStrengthCapacity(1):0.##} lb weight";
                    }
                    else
                    {
                        float cur = CalculateStrengthCapacity(level);
                        float next = CalculateStrengthCapacity(level + 1);
                        __result = $"-{cur:0.##} lb > -{next:0.##} lb weight";
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        public static class SkillMenu_DrawDetails_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target() => AccessTools.Method("LethalRPG.SkillMenu:DrawDetails");

            [HarmonyPrefix]
            private static void Prefix()
            {
                ApplySkillsUi();
            }
        }

        // Cập nhật text trong Daily Report / Status Report
        [HarmonyPatch]
        public static class Reports_PlayerStatus_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target() => AccessTools.Method("LethalRPG.Reports:PlayerStatus");

            [HarmonyPostfix]
            private static void Postfix(PlayerControllerB player, StringBuilder stringBuilder)
            {
                if (player == null || stringBuilder == null) return;

                float totalLb = Mathf.Round((player.carryWeight - 1f) * 105f);
                float strengthLb = GetPlayerStrengthCapacity(player);

                if (totalLb > 0)
                {
                    float covered = Mathf.Min(totalLb, strengthLb);
                    float penalty = Mathf.Max(0f, totalLb - strengthLb);

                    string customInfo = $"[Strength {strengthLb:0.##} lb] Carried: {totalLb:0} lb | Covered: {covered:0.##} lb | Penalty: {penalty:0.##} lb";
                    stringBuilder.AppendLine(customInfo);
                }
            }
        }

        // Đảm bảo Max Level có thể config được
        [HarmonyPatch(typeof(Upgrades), "Leveled")]
        [HarmonyPostfix]
        private static void Upgrades_Leveled_Postfix(UpgradeType type, UpgradeDef __result)
        {
            if (type == UpgradeType.Strength && __result != null)
            {
                if (CustomPatchesPlugin.StrengthMaxLevel != null)
                {
                    __result.DefaultMaxLevel = CustomPatchesPlugin.StrengthMaxLevel.Value;
                }
            }
        }
    }
}
