using System;
using System.Reflection;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using LethalRPG;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class CombatModeEnhancedPatches
    {
        public static ConfigEntry<bool> CombatModeToggle { get; private set; }
        private static bool _toggledStance = false;

        public static void InitConfig(ConfigFile config)
        {
            CombatModeToggle = config.Bind("CombatMode", "ToggleStance", true, "If true, pressing the stance key toggles Combat Mode on/off instead of holding it.");
        }

        // ---------------------------------------------------------
        // 1. TOGGLE MECHANISM FOR COMBAT MODE STANCE
        // ---------------------------------------------------------
        [HarmonyPatch]
        public static class CombatInput_Update_Patch
        {
            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                Type t = AccessTools.TypeByName("CombatMode.CombatInput");
                return AccessTools.Method(t, "Update");
            }

            [HarmonyPrefix]
            private static bool Prefix(object __instance)
            {
                if (CombatModeToggle == null || !CombatModeToggle.Value)
                {
                    // Fallback to original hold behavior if toggle is disabled
                    return true;
                }

                try
                {
                    // Call CombatNet.EnsureRegistered()
                    Type netType = AccessTools.TypeByName("CombatMode.CombatNet");
                    MethodInfo ensureReg = AccessTools.Method(netType, "EnsureRegistered");
                    ensureReg?.Invoke(null, null);

                    PlayerControllerB localPlayer = GameNetworkManager.Instance?.localPlayerController;
                    if (localPlayer == null) return false;

                    Type stateType = AccessTools.TypeByName("CombatMode.CombatState");
                    MethodInfo getState = AccessTools.Method(stateType, "Get", new Type[] { typeof(PlayerControllerB) });
                    object state = getState?.Invoke(null, new object[] { localPlayer });
                    if (state == null) return false;

                    FieldInfo activeField = AccessTools.Field(stateType, "Active");
                    bool currentActive = (bool)activeField.GetValue(state);

                    // Check CanFight
                    Type inputType = AccessTools.TypeByName("CombatMode.CombatInput");
                    MethodInfo canFightMethod = AccessTools.Method(inputType, "CanFight", new Type[] { typeof(PlayerControllerB) });
                    bool canFight = (bool)(canFightMethod?.Invoke(null, new object[] { localPlayer }) ?? false);

                    var keyboard = UnityEngine.InputSystem.Keyboard.current;
                    if (keyboard != null && canFight)
                    {
                        // Get StanceKey config entry
                        Type pluginType = AccessTools.TypeByName("CombatMode.Plugin");
                        FieldInfo stanceKeyField = AccessTools.Field(pluginType, "StanceKey");
                        var stanceKeyEntry = stanceKeyField?.GetValue(null) as ConfigEntry<UnityEngine.InputSystem.Key>;
                        var key = stanceKeyEntry?.Value ?? UnityEngine.InputSystem.Key.LeftAlt;

                        var keyControl = keyboard[key];
                        if (keyControl != null && keyControl.wasPressedThisFrame)
                        {
                            _toggledStance = !_toggledStance;
                        }
                    }
                    else
                    {
                        _toggledStance = false;
                    }

                    bool desired = _toggledStance && canFight;
                    if (desired && !currentActive)
                    {
                        if (localPlayer.thisController != null && localPlayer.thisController.isGrounded)
                        {
                            activeField.SetValue(state, true);
                            MethodInfo sendStance = AccessTools.Method(netType, "SendStance", new Type[] { typeof(PlayerControllerB), typeof(bool) });
                            sendStance?.Invoke(null, new object[] { localPlayer, true });
                        }
                        else
                        {
                            _toggledStance = false;
                        }
                    }
                    else if (!desired && currentActive)
                    {
                        activeField.SetValue(state, false);
                        MethodInfo sendStance = AccessTools.Method(netType, "SendStance", new Type[] { typeof(PlayerControllerB), typeof(bool) });
                        sendStance?.Invoke(null, new object[] { localPlayer, false });
                    }

                    // Check Hit
                    PropertyInfo readyHitProp = AccessTools.Property(stateType, "ReadyToCheckHit");
                    bool readyHit = (bool)(readyHitProp?.GetValue(state) ?? false);
                    if (readyHit)
                    {
                        if (localPlayer.isPlayerControlled && !localPlayer.isPlayerDead)
                        {
                            Type hitType = AccessTools.TypeByName("CombatMode.CombatHit");
                            MethodInfo tryHit = AccessTools.Method(hitType, "TryHit", new Type[] { typeof(PlayerControllerB), stateType });
                            tryHit?.Invoke(null, new object[] { localPlayer, state });
                        }
                        else
                        {
                            FieldInfo hitCheckedField = AccessTools.Field(stateType, "HitChecked");
                            hitCheckedField?.SetValue(state, true);
                        }
                    }

                    // Check Punch input
                    bool isActiveNow = (bool)activeField.GetValue(state);
                    PropertyInfo inRecoilProp = AccessTools.Property(stateType, "InRecoil");
                    bool inRecoil = (bool)(inRecoilProp?.GetValue(state) ?? false);

                    if (isActiveNow && !inRecoil)
                    {
                        var mouse = UnityEngine.InputSystem.Mouse.current;
                        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                        {
                            FieldInfo lastPunchField = AccessTools.Field(inputType, "lastPunch");
                            float lastPunch = (float)(lastPunchField?.GetValue(__instance) ?? 0f);

                            Type pluginType = AccessTools.TypeByName("CombatMode.Plugin");
                            FieldInfo punchCooldownField = AccessTools.Field(pluginType, "PunchCooldown");
                            var punchCooldownEntry = punchCooldownField?.GetValue(null) as ConfigEntry<float>;
                            float cooldown = punchCooldownEntry?.Value ?? 0.35f;

                            if (Time.time - lastPunch >= cooldown)
                            {
                                lastPunchField?.SetValue(__instance, Time.time);

                                MethodInfo startPunch = AccessTools.Method(stateType, "StartPunch");
                                startPunch?.Invoke(state, null);

                                Type soundType = AccessTools.TypeByName("CombatMode.CombatSound");
                                MethodInfo playPunch = AccessTools.Method(soundType, "PlayPunch", new Type[] { typeof(PlayerControllerB) });
                                playPunch?.Invoke(null, new object[] { localPlayer });

                                MethodInfo sendPunch = AccessTools.Method(netType, "SendPunch", new Type[] { typeof(PlayerControllerB) });
                                sendPunch?.Invoke(null, new object[] { localPlayer });
                            }
                        }
                    }

                    return false; // Skip original Update since toggle logic fully replaced it
                }
                catch (Exception ex)
                {
                    CustomPatchesPlugin.Log.LogWarning($"[CombatModeEnhanced] Error in toggle update patch: {ex}");
                    return true;
                }
            }
        }

        // ---------------------------------------------------------
        // 2. BRAWLER LEVEL 3 SYNERGY: PUNCH DAMAGE += 1
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(EnemyAI), "HitEnemy")]
        [HarmonyPrefix]
        private static void EnemyAI_HitEnemy_BrawlerSynergy(EnemyAI __instance, ref int force, PlayerControllerB playerWhoHit)
        {
            if (playerWhoHit == null || playerWhoHit.currentlyHeldObjectServer != null) return;

            try
            {
                Type stateType = AccessTools.TypeByName("CombatMode.CombatState");
                if (stateType != null)
                {
                    MethodInfo getState = AccessTools.Method(stateType, "Get", new Type[] { typeof(PlayerControllerB) });
                    object state = getState?.Invoke(null, new object[] { playerWhoHit });
                    if (state != null)
                    {
                        FieldInfo activeField = AccessTools.Field(stateType, "Active");
                        bool isActive = (bool)(activeField?.GetValue(state) ?? false);
                        if (isActive)
                        {
                            int brawlerLevel = RpgState.Level(playerWhoHit, UpgradeType.Brawler);
                            if (brawlerLevel >= 3)
                            {
                                force += 1; // +1 extra damage in CombatMode
                                CustomPatchesPlugin.Log.LogInfo($"[CombatModeEnhanced] Brawler Lv3+ synergy applied! Punch force: {force - 1} -> {force} on {__instance.enemyType?.enemyName ?? __instance.name}.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[CombatModeEnhanced] Error in Brawler synergy: {ex}");
            }
        }
    }
}
