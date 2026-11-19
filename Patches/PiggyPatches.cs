using System;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using PiggyVarietyMod;
using PiggyVarietyMod.Patches;
using SonOfCompany_CustomPatches.Items;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    // ---------------------------------------------------------
    // RUSSIAN ROULETTE & EXECUTION STATE - PER REVOLVER INSTANCE
    // ---------------------------------------------------------
    public class RevolverRouletteState : MonoBehaviour
    {
        public bool isRouletteActive = false;
        public bool hasSpunThisCycle = false;
        public int bulletChamberIndex = 0;   // Vị trí viên đạn duy nhất (0..5)
        public int currentChamberIndex = 0;  // Vị trí búa đập hiện tại (0..5)
        public bool wasCylinderOpen = false;

        // Execution Revolver State
        public bool isExecutionMode = false;
        public int executionBulletsCount = 0;

        public void ResetRoulette()
        {
            isRouletteActive = false;
            hasSpunThisCycle = false;
            bulletChamberIndex = 0;
            currentChamberIndex = 0;
        }

        public void SpinRoulette()
        {
            bulletChamberIndex = UnityEngine.Random.Range(0, 6);
            currentChamberIndex = 0;
            isRouletteActive = true;
            hasSpunThisCycle = true;
        }
    }

    public static class PiggyPatches
    {
        public static RevolverRouletteState GetRouletteState(RevolverItem revolver)
        {
            if (revolver == null) return null;
            var state = revolver.GetComponent<RevolverRouletteState>();
            if (state == null)
            {
                state = revolver.gameObject.AddComponent<RevolverRouletteState>();
            }
            return state;
        }

        public static bool IsExecutionBullet(GrabbableObject item)
        {
            if (item == null) return false;
            if (item.itemProperties != null && !string.IsNullOrEmpty(item.itemProperties.itemName))
            {
                if (item.itemProperties.itemName.Equals("Execution Bullet", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            if (item.GetComponent<ExecutionBulletTag>() != null)
                return true;
            return false;
        }

        // Helper: get ammosLoaded field via reflection
        private static FieldInfo _ammosLoadedField = null;
        private static FieldInfo GetAmmosLoadedField()
        {
            if (_ammosLoadedField == null)
                _ammosLoadedField = AccessTools.Field(typeof(RevolverItem), "ammosLoaded");
            return _ammosLoadedField;
        }

        public static int GetAmmosLoaded(RevolverItem r)
        {
            var f = GetAmmosLoadedField();
            return f != null ? (int)f.GetValue(r) : 6;
        }

        public static void SetAmmosLoaded(RevolverItem r, int value)
        {
            GetAmmosLoadedField()?.SetValue(r, value);
        }

        // Helper: originalPlayerAnimator
        private static bool _origAnimFieldChecked = false;
        private static FieldInfo _origAnimField = null;
        private static FieldInfo GetOrigAnimField()
        {
            if (!_origAnimFieldChecked)
            {
                _origAnimFieldChecked = true;
                Type t = AccessTools.TypeByName("PiggyVarietyMod.Patches.PlayerControllerBPatch");
                if (t != null)
                    _origAnimField = AccessTools.Field(t, "originalPlayerAnimator");
            }
            return _origAnimField;
        }

        private static RuntimeAnimatorController GetOriginalPlayerAnimator()
        {
            return GetOrigAnimField()?.GetValue(null) as RuntimeAnimatorController;
        }

        // Helper: get Rifle reload key string
        public static string GetRifleReloadKeyDisplay()
        {
            try
            {
                var inputActions = AccessTools.Field(typeof(PiggyVarietyMod.Plugin), "InputActionInstance")?.GetValue(null) as PVInputActions;
                if (inputActions != null && inputActions.RifleReloadKey != null)
                {
                    string key = UnityEngine.InputSystem.InputActionRebindingExtensions.GetBindingDisplayString(inputActions.RifleReloadKey, (UnityEngine.InputSystem.InputBinding.DisplayStringOptions)0, null);
                    return key?.Replace(" | ", "") ?? "R";
                }
            }
            catch { }
            return "R";
        }

        // ---------------------------------------------------------
        // 1. REVOLVER [E] / [Q] INTERACTION HOOK
        // Roulette trigger moved from [E] to [R]!
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RevolverItem), "ItemInteractLeftRight")]
        [HarmonyPrefix]
        private static bool RevolverItem_ItemInteractLeftRight_Prefix(RevolverItem __instance, bool right)
        {
            if (__instance == null) return true;
            var state = GetRouletteState(__instance);

            // !right (Q pressed): Mở / Đóng cylinder
            if (!right)
            {
                if (state != null)
                {
                    state.ResetRoulette();
                }
                return true; // Cho phép mở/đóng cylinder bình thường
            }

            // right == true (E pressed):
            bool isCylinderOpen = false;
            if (__instance.gunAnimator != null)
            {
                isCylinderOpen = __instance.gunAnimator.GetBool("Reloading");
            }

            // KHI CYLINDER ĐANG ĐÓNG: Không reload (và roulette chuyển sang phím R)
            if (!isCylinderOpen && !__instance.isCylinderMoving)
            {
                return false;
            }

            // KHI CYLINDER ĐANG MỞ: Cho phép reload đạn
            return true;
        }

        // ---------------------------------------------------------
        // EXECUTION BULLET RELOAD FILTER
        // Khi Revolver đang ở status Execution Revolver:
        // CHỈ cho phép reload = Execution Bullet chứ không phải đạn thường!
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RevolverItem), "FindAmmoInInventory")]
        [HarmonyPrefix]
        private static bool RevolverItem_FindAmmoInInventory_Prefix(RevolverItem __instance, ref int __result)
        {
            if (__instance == null || __instance.playerHeldBy == null) return true;
            var state = GetRouletteState(__instance);
            if (state == null) return true;

            var slots = __instance.playerHeldBy.ItemSlots;
            if (slots == null) return true;

            if (state.isExecutionMode)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var item = slots[i];
                    if (item != null && IsExecutionBullet(item))
                    {
                        __result = i;
                        return false; // Tìm thấy Execution Bullet trong túi
                    }
                }

                // Đang trong mode Execution Revolver nhưng không có Execution Bullet -> Chặn reload!
                __result = -1;
                return false;
            }

            return true; // Chế độ thường: tìm ammo bình thường hoặc execution bullet
        }

        [HarmonyPatch(typeof(RevolverItem), "ReloadedGun")]
        [HarmonyPostfix]
        private static void RevolverItem_ReloadedGun_Postfix(RevolverItem __instance, bool __result)
        {
            if (!__result || __instance == null || __instance.playerHeldBy == null) return;
            var state = GetRouletteState(__instance);
            if (state == null) return;

            FieldInfo slotField = AccessTools.Field(typeof(RevolverItem), "ammoSlotToUse");
            int slot = slotField != null ? (int)slotField.GetValue(__instance) : -1;

            if (slot >= 0 && slot < __instance.playerHeldBy.ItemSlots.Length)
            {
                var ammoItem = __instance.playerHeldBy.ItemSlots[slot];
                if (IsExecutionBullet(ammoItem))
                {
                    state.isExecutionMode = true;
                    state.executionBulletsCount++;
                    state.ResetRoulette(); // Không áp dụng Roulette khi đang ở status Execution Revolver
                    CustomPatchesPlugin.Log.LogInfo($"[Execution Revolver] Loaded Execution Bullet! Total loaded: {state.executionBulletsCount}. Status changed to Execution Revolver.");
                }
            }
        }

        // ---------------------------------------------------------
        // RUSSIAN ROULETTE & EXECUTION REVOLVER SHOOTING
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RevolverItem), "ShootGun")]
        [HarmonyPrefix]
        private static bool ShootGun_Prefix(RevolverItem __instance)
        {
            if (__instance == null) return true;

            int ammo = GetAmmosLoaded(__instance);
            if (ammo <= 0 || __instance.isCylinderMoving)
                return true;

            var state = GetRouletteState(__instance);
            if (state == null) return true;

            // KHI LÀ EXECUTION REVOLVER: Bỏ qua hoàn toàn Roulette, bắn luôn!
            if (state.isExecutionMode)
            {
                state.executionBulletsCount--;
                CustomPatchesPlugin.Log.LogInfo($"[Execution Revolver] Fired Execution Bullet! Remaining in gun: {state.executionBulletsCount}.");

                if (state.executionBulletsCount <= 0 || ammo <= 1)
                {
                    state.isExecutionMode = false;
                    state.executionBulletsCount = 0;
                    CustomPatchesPlugin.Log.LogInfo("[Execution Revolver] No Execution Bullets remaining in gun. Reverted to normal Revolver.");
                }
                return true;
            }

            // KHI LÀ ROULETTE BÌNH THƯỜNG:
            if (state.isRouletteActive)
            {
                if (state.currentChamberIndex == state.bulletChamberIndex)
                {
                    // BANG! Trúng viên đạn!
                    CustomPatchesPlugin.Log.LogInfo($"[Roulette] BANG! Chamber {state.currentChamberIndex} hit bullet! Firing.");
                    state.isRouletteActive = false;
                    return true;
                }
                else
                {
                    // CLICK! Buồng đạn rỗng (không popup thông báo)
                    CustomPatchesPlugin.Log.LogInfo($"[Roulette] CLICK! Chamber {state.currentChamberIndex} was empty. Advancing chamber.");
                    state.currentChamberIndex = (state.currentChamberIndex + 1) % 6;

                    AudioClip drySFX = AccessTools.Field(typeof(RevolverItem), "revolverDryFire")?.GetValue(__instance) as AudioClip
                                    ?? AccessTools.Field(typeof(RevolverItem), "noAmmoSFX")?.GetValue(__instance) as AudioClip;
                    if (__instance.gunAudio != null)
                    {
                        if (drySFX != null)
                            __instance.gunAudio.PlayOneShot(drySFX);
                        else
                            __instance.gunAudio.Play();
                    }

                    return false; // Bỏ qua bắn: không tốn đạn, không tạo lửa, không gây sát thương
                }
            }

            return true;
        }

        // ---------------------------------------------------------
        // REVOLVER UPDATE - ROULETTE [R] KEY & SPEEDLOADER & TOOLTIPS
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RevolverItem), "Update")]
        [HarmonyPostfix]
        private static void RevolverItem_Update_Postfix(RevolverItem __instance)
        {
            if (__instance == null || !__instance.isHeld || __instance.playerHeldBy == null)
                return;

            if (StartOfRound.Instance != null && __instance.playerHeldBy != StartOfRound.Instance.localPlayerController)
                return;

            var state = GetRouletteState(__instance);
            if (state == null) return;

            bool isCylinderOpen = false;
            if (__instance.gunAnimator != null)
            {
                isCylinderOpen = __instance.gunAnimator.GetBool("Reloading");
            }

            // Khi mở cylinder: Reset Roulette
            if (isCylinderOpen && !state.wasCylinderOpen)
            {
                state.ResetRoulette();
            }
            // Khi vừa đóng cylinder lại: Cho phép bắt đầu chu kỳ spin mới
            if (!isCylinderOpen && state.wasCylinderOpen)
            {
                state.hasSpunThisCycle = false;
            }
            state.wasCylinderOpen = isCylinderOpen;

            int ammo = GetAmmosLoaded(__instance);

            // KHI CYLINDER ĐANG ĐÓNG: Nhấn [R] để kích hoạt Russian Roulette
            if (!isCylinderOpen && !__instance.isCylinderMoving)
            {
                if (!state.isExecutionMode && ammo == 1 && !state.hasSpunThisCycle)
                {
                    if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R))
                    {
                        state.SpinRoulette();
                        RouletteAudioManager.PlaySpin(__instance.gunAudio);
                        CustomPatchesPlugin.Log.LogInfo($"[Roulette] Cylinder spun on R key! Single bullet randomized to chamber {state.bulletChamberIndex}. (No tip displayed)");
                    }
                }
            }

            // Cập nhật tooltip cho Execution Revolver
            if (state.isExecutionMode && HUDManager.Instance != null && HUDManager.Instance.controlTipLines != null)
            {
                HUDManager.Instance.ChangeControlTip(0, $"Shoot : [LMB] [Execution Revolver: {state.executionBulletsCount}]");
            }

            // KHI CYLINDER MỞ: Speedloader prompt khi hết đạn
            if (isCylinderOpen && ammo <= 0 && !state.isExecutionMode)
            {
                bool hasSpeedloader = Items.SpeedloaderItem.PlayerHasSpeedloader(__instance.playerHeldBy);

                if (HUDManager.Instance != null && HUDManager.Instance.controlTipLines != null)
                {
                    HUDManager.Instance.ChangeControlTip(3, "R: to use speedloader");
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R))
                {
                    if (hasSpeedloader)
                    {
                        if (Items.SpeedloaderItem.TryConsumeSpeedloader(__instance.playerHeldBy))
                        {
                            SetAmmosLoaded(__instance, 6);
                            if (__instance.gunAnimator != null)
                                __instance.gunAnimator.SetInteger("BulletsLoaded", 6);
                            if (RoundManager.Instance != null && __instance.gunAudio != null)
                            {
                                __instance.gunAudio.PlayOneShot(__instance.gunReloadSFX);
                            }
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // 2. PIGGY RIFLE (M4) - EXACT AMMO DISPLAY ON [Q] INSPECT
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(M4Item), "SetAmmoControlTip")]
        [HarmonyPrefix]
        private static bool M4Item_SetAmmoControlTip_Prefix(M4Item __instance, bool isChecking)
        {
            if (__instance == null || !__instance.IsOwner) return true;

            string text = isChecking ? $"Check ammo : [Q] [{__instance.ammosLoaded}/30]" : "Check ammo : [Q] [??]";
            HUDManager.Instance?.ChangeControlTip(3, text);

            string reloadKey = GetRifleReloadKeyDisplay();
            HUDManager.Instance?.ChangeControlTip(2, $"Reload : [{reloadKey}]");

            return false;
        }

        [HarmonyPatch(typeof(M4Item), "KR_SetAmmoControlTip")]
        [HarmonyPrefix]
        private static bool M4Item_KR_SetAmmoControlTip_Prefix(M4Item __instance, bool isChecking)
        {
            if (__instance == null || !__instance.IsOwner) return true;

            string text = isChecking ? $"Check ammo : [Q] [{__instance.ammosLoaded}/30]" : "Check ammo : [Q] [??]";
            HUDManager.Instance?.ChangeControlTip(3, text);

            string reloadKey = GetRifleReloadKeyDisplay();
            HUDManager.Instance?.ChangeControlTip(2, $"Reload : [{reloadKey}]");

            return false;
        }

        public static float GetMarksmanFactor(PlayerControllerB player)
        {
            if (player == null) return 1.0f;
            try
            {
                int level = LethalRPG.RpgState.Level(player, LethalRPG.UpgradeType.Marksman);
                if (level <= 0) return 1.0f;
                float reduction = Mathf.Min(0.7f, level * 0.15f);
                return 1.0f - reduction;
            }
            catch { return 1.0f; }
        }

        // ---------------------------------------------------------
        // 3. MARKSMAN UPGRADE APPLIED TO REVOLVER & M4
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RevolverItem), "ReloadGunAnimation")]
        [HarmonyPrefix]
        private static void RevolverItem_ReloadGunAnimation_Prefix(RevolverItem __instance)
        {
            if (__instance != null && __instance.playerHeldBy != null)
            {
                try
                {
                    float factor = GetMarksmanFactor(__instance.playerHeldBy);
                    if (factor > 0.05f && factor < 0.999f)
                    {
                        PiggyVarietyMod.Plugin.revolverReloadSpeed = 1.0f / factor;
                    }
                    else
                    {
                        PiggyVarietyMod.Plugin.revolverReloadSpeed = 1.0f;
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(M4Item), "ReloadGunAnimation")]
        [HarmonyPrefix]
        private static void M4Item_ReloadGunAnimation_Prefix(M4Item __instance)
        {
            if (__instance != null && __instance.playerHeldBy != null)
            {
                try
                {
                    float factor = GetMarksmanFactor(__instance.playerHeldBy);
                    if (factor > 0.05f && factor < 0.999f)
                    {
                        PiggyVarietyMod.Plugin.rifleReloadSpeed = 1.0f / factor;
                    }
                    else
                    {
                        PiggyVarietyMod.Plugin.rifleReloadSpeed = 1.0f;
                    }
                }
                catch { }
            }
        }

        // ---------------------------------------------------------
        // 4. DAMAGE REBALANCING & EXECUTION INSTANT KILL
        // ---------------------------------------------------------
        private static int _revolverDmgStep = 0;
        private static int _rifleDmgStep = 0;

        [HarmonyPatch(typeof(EnemyAI), "HitEnemy")]
        [HarmonyPrefix]
        private static void EnemyAI_HitEnemy_Weapons(EnemyAI __instance, ref int force, PlayerControllerB playerWhoHit)
        {
            if (__instance == null || playerWhoHit == null || playerWhoHit.currentlyHeldObjectServer == null) return;

            var held = playerWhoHit.currentlyHeldObjectServer;

            if (held is RevolverItem revolver)
            {
                var state = GetRouletteState(revolver);
                if (state != null && state.isExecutionMode)
                {
                    // EXECUTION REVOLVER: INSTANT DEATH CHO MỌI MONSTER (BAO GỒM TẤT CẢ IMMORTAL MONSTERS)
                    CustomPatchesPlugin.Log.LogInfo($"[Execution Revolver] INSTANT DEATH executed on {__instance.enemyType?.enemyName ?? __instance.name}!");
                    __instance.enemyHP = 0;
                    __instance.isEnemyDead = true;
                    force = 999999;
                    try { __instance.KillEnemyOnOwnerClient(false); } catch { }
                    try { __instance.KillEnemyServerRpc(false); } catch { }
                    try { __instance.KillEnemy(false); } catch { }
                    return;
                }

                // Revolver thường: Trung bình 4.25
                _revolverDmgStep = (_revolverDmgStep + 1) % 4;
                force = (_revolverDmgStep == 0) ? 5 : 4;
            }
            else if (held is M4Item || held.GetType().Name == "M4Item")
            {
                _rifleDmgStep = (_rifleDmgStep + 1) % 4;
                force = (_rifleDmgStep == 0) ? 2 : 1;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayer")]
        [HarmonyPrefix]
        private static void PlayerControllerB_DamagePlayer_Weapons(PlayerControllerB __instance, ref int damageNumber, CauseOfDeath causeOfDeath)
        {
            if (causeOfDeath == (CauseOfDeath)7) // Gun shot
            {
                PlayerControllerB shooter = StartOfRound.Instance?.allPlayerScripts != null ?
                    Array.Find(StartOfRound.Instance.allPlayerScripts, p => p != null && !p.isPlayerDead && (p.currentlyHeldObjectServer is RevolverItem || p.currentlyHeldObjectServer?.GetType().Name == "M4Item")) : null;

                if (shooter != null && shooter.currentlyHeldObjectServer != null)
                {
                    if (shooter.currentlyHeldObjectServer is RevolverItem)
                    {
                        damageNumber = 90; // Flat 90 HP damage
                    }
                    else if (shooter.currentlyHeldObjectServer is M4Item || shooter.currentlyHeldObjectServer.GetType().Name == "M4Item")
                    {
                        damageNumber = 30; // Flat 30 HP damage
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // 5. ANIMATION RESTORATION ON POCKET / DISCARD
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(GrabbableObject), "PocketItem")]
        [HarmonyPostfix]
        private static void GrabbableObject_PocketItem_Postfix(GrabbableObject __instance)
        {
            if (__instance is RevolverItem || __instance.GetType().Name == "M4Item")
            {
                RestorePlayerAnimator(__instance.playerHeldBy);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "DiscardItem")]
        [HarmonyPostfix]
        private static void GrabbableObject_DiscardItem_Postfix(GrabbableObject __instance)
        {
            if (__instance is RevolverItem || __instance.GetType().Name == "M4Item")
            {
                PlayerControllerB prev = AccessTools.Field(typeof(GrabbableObject), "previousPlayerHeldBy")?.GetValue(__instance) as PlayerControllerB;
                RestorePlayerAnimator(__instance.playerHeldBy ?? prev);
            }
        }

        public static void RestorePlayerAnimator(PlayerControllerB player)
        {
            if (player == null || player.playerBodyAnimator == null)
                return;

            RuntimeAnimatorController target = null;
            if (StartOfRound.Instance != null)
            {
                target = (player == StartOfRound.Instance.localPlayerController)
                    ? StartOfRound.Instance.localClientAnimatorController
                    : StartOfRound.Instance.otherClientsAnimatorController;
            }
            if (target == null)
            {
                target = GetOriginalPlayerAnimator();
            }
            if (target != null && player.playerBodyAnimator.runtimeAnimatorController != target)
            {
                player.playerBodyAnimator.runtimeAnimatorController = target;
            }
        }
    }
}
