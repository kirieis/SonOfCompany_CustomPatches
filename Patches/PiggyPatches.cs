using System;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using PiggyVarietyMod;
using PiggyVarietyMod.Patches;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    // ---------------------------------------------------------
    // RUSSIAN ROULETTE STATE COMPONENT - PER REVOLVER INSTANCE
    // ---------------------------------------------------------
    public class RevolverRouletteState : MonoBehaviour
    {
        public bool isRouletteActive = false;
        public bool hasSpunThisCycle = false;
        public int bulletChamberIndex = 0;   // Vị trí viên đạn duy nhất (0..5)
        public int currentChamberIndex = 0;  // Vị trí búa đập hiện tại (0..5)
        public bool wasCylinderOpen = false;

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
        // 1. RUSSIAN ROULETTE - NATIVE [E] / [Q] INTERACTION HOOK
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
                return true; // Cho phép mở cylinder bình thường
            }

            // right == true (E pressed):
            bool isCylinderOpen = false;
            if (__instance.gunAnimator != null)
            {
                isCylinderOpen = __instance.gunAnimator.GetBool("Reloading");
            }

            // KHI CYLINDER ĐANG ĐÓNG: Nhấn E để quay buồng đạn (Russian Roulette)
            if (!isCylinderOpen && !__instance.isCylinderMoving)
            {
                int ammo = GetAmmosLoaded(__instance);
                if (ammo == 1)
                {
                    if (state != null && !state.hasSpunThisCycle)
                    {
                        state.SpinRoulette();
                        RouletteAudioManager.PlaySpin(__instance.gunAudio);

                        if (HUDManager.Instance != null)
                        {
                            HUDManager.Instance.DisplayTip("Russian Roulette", "Cylinder spun! One bullet loaded.", false, false, "LC_Tip1");
                        }
                        CustomPatchesPlugin.Log.LogInfo($"[Roulette] Cylinder spun on E! Single bullet randomized to chamber {state.bulletChamberIndex}.");
                        return false; // Đã kích hoạt Roulette thành công
                    }
                }

                // Nếu cylinder đang đóng thì không reload
                return false;
            }

            // KHI CYLINDER ĐANG MỞ: Cho phép reload đạn bình thường
            return true;
        }

        // ---------------------------------------------------------
        // RUSSIAN ROULETTE - SHOOT GUN (BANG HOẶC DRY FIRE CLICK)
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
            if (state != null && state.isRouletteActive)
            {
                if (state.currentChamberIndex == state.bulletChamberIndex)
                {
                    // BANG! Trúng viên đạn!
                    CustomPatchesPlugin.Log.LogInfo($"[Roulette] BANG! Chamber {state.currentChamberIndex} hit bullet! Firing.");
                    state.isRouletteActive = false;
                    return true; // Cho phép bắn
                }
                else
                {
                    // CLICK! Buồng đạn rỗng!
                    CustomPatchesPlugin.Log.LogInfo($"[Roulette] CLICK! Chamber {state.currentChamberIndex} was empty. Advancing chamber.");
                    state.currentChamberIndex = (state.currentChamberIndex + 1) % 6;

                    // Âm thanh búa đập buồng đạn rỗng
                    AudioClip drySFX = AccessTools.Field(typeof(RevolverItem), "revolverDryFire")?.GetValue(__instance) as AudioClip
                                    ?? AccessTools.Field(typeof(RevolverItem), "noAmmoSFX")?.GetValue(__instance) as AudioClip;
                    if (__instance.gunAudio != null)
                    {
                        if (drySFX != null)
                            __instance.gunAudio.PlayOneShot(drySFX);
                        else
                            __instance.gunAudio.Play();
                    }

                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.DisplayTip("Russian Roulette", "*Click* ... Empty chamber.", false, false, "LC_Tip1");
                    }

                    return false; // Bỏ qua bắn: không tốn đạn, không tạo lửa, không gây sát thương
                }
            }

            return true;
        }

        // ---------------------------------------------------------
        // REVOLVER UPDATE - SPEEDLOADER & CYLINDER STATE TRACKING
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

            // KHI CYLINDER MỞ: Speedloader prompt khi hết đạn
            if (isCylinderOpen && ammo <= 0)
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
                            if (HUDManager.Instance != null)
                            {
                                HUDManager.Instance.DisplayTip("Speedloader", "Reloaded 6 bullets into Revolver!", false, false, "LC_Tip1");
                            }
                        }
                    }
                    else
                    {
                        if (HUDManager.Instance != null)
                        {
                            HUDManager.Instance.DisplayTip("No Speedloader", "You need a Speedloader in your inventory!", true, false, "LC_Tip1");
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

            return false; // Thay thế hoàn toàn logic gốc để không bao giờ hiện [<30], [<20], [<10]
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

        // ---------------------------------------------------------
        // 3. DAMAGE REBALANCING
        // Revolver: Monster = 4.25 (Flat), Player = 90 (Flat)
        // Rifle:    Monster = 1.25 (Flat), Player = 30 (Flat)
        // ---------------------------------------------------------
        private static int _revolverDmgStep = 0;
        private static int _rifleDmgStep = 0;

        [HarmonyPatch(typeof(EnemyAI), "HitEnemy")]
        [HarmonyPrefix]
        private static void EnemyAI_HitEnemy_Weapons(EnemyAI __instance, ref int force, PlayerControllerB playerWhoHit)
        {
            if (playerWhoHit == null || playerWhoHit.currentlyHeldObjectServer == null) return;

            var held = playerWhoHit.currentlyHeldObjectServer;

            // Revolver: Trung bình chính xác 4.25 (4, 4, 4, 5)
            if (held is RevolverItem)
            {
                _revolverDmgStep = (_revolverDmgStep + 1) % 4;
                force = (_revolverDmgStep == 0) ? 5 : 4;
            }
            // Rifle (M4): Trung bình chính xác 1.25 (1, 1, 1, 2)
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
        // 4. ANIMATION RESTORATION ON POCKET / DISCARD
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
