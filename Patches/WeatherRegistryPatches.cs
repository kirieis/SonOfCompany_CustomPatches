using System;
using HarmonyLib;
using UnityEngine;
using WeatherRegistry;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class WeatherRegistryPatches
    {
        // ---------------------------------------------------------
        // FIX: PREVENT NRE / FREEZE AT "ENTERING THE ATMOSPHERE..."
        // ---------------------------------------------------------

        // 1. Ensure GetCurrentWeather never returns null, even if called before setup is finished
        [HarmonyPatch(typeof(WeatherRegistry.WeatherManager), "GetCurrentWeather")]
        [HarmonyPrefix]
        private static bool WeatherManager_GetCurrentWeather_Prefix(SelectableLevel level, ref WeatherRegistry.Weather __result)
        {
            if (WeatherRegistry.Settings.SetupFinished && WeatherRegistry.WeatherManager.CurrentWeathers != null)
            {
                try
                {
                    var w = WeatherRegistry.WeatherManager.CurrentWeathers.GetLevelWeather(level);
                    if (w != null)
                    {
                        __result = w;
                        return false;
                    }
                }
                catch { }
            }

            // Fallback to NoneWeather so caller NEVER encounters null
            __result = WeatherRegistry.WeatherManager.NoneWeather;
            return false;
        }

        // 2. Ensure GetCurrentWeatherName never crashes with NullReferenceException
        [HarmonyPatch(typeof(WeatherRegistry.WeatherManager), "GetCurrentWeatherName")]
        [HarmonyPrefix]
        private static bool WeatherManager_GetCurrentWeatherName_Prefix(SelectableLevel level, bool ignoreOverride, ref string __result)
        {
            try
            {
                string overrideStr = WeatherRegistry.WeatherManager.WeatherDisplayOverride(level);
                if (!string.IsNullOrEmpty(overrideStr) && !ignoreOverride)
                {
                    __result = overrideStr;
                    return false;
                }

                var weather = WeatherRegistry.WeatherManager.GetCurrentWeather(level);
                if (weather != null && !string.IsNullOrEmpty(weather.Name))
                {
                    __result = weather.Name;
                    return false;
                }

                if (level != null)
                {
                    __result = level.currentWeather.ToString();
                    return false;
                }
            }
            catch { }

            __result = "None";
            return false;
        }

        // 3. Ensure WeatherManager.IsSetupFinished is restored when round/game initializes after a menu quit
        [HarmonyPatch(typeof(RoundManager), "Start")]
        [HarmonyPostfix]
        private static void RoundManager_Start_Postfix()
        {
            try
            {
                if (!WeatherRegistry.WeatherManager.IsSetupFinished)
                {
                    AccessTools.PropertySetter(typeof(WeatherRegistry.WeatherManager), "IsSetupFinished")?.Invoke(null, new object[] { true });
                    CustomPatchesPlugin.Log.LogInfo("[WeatherRegistryFix] Restored WeatherManager.IsSetupFinished = true on RoundManager Start.");
                }
            }
            catch { }
        }

        // ---------------------------------------------------------
        // 4. FOGGY WEATHER: 10% FOG INSIDE INTERIOR (90% REDUCED)
        // ---------------------------------------------------------
        private static float _originalFogMeanFreePath = -1f;
        private static bool _wasInsideFactory = false;

        [HarmonyPatch(typeof(GameNetcodeStuff.PlayerControllerB), "Update")]
        [HarmonyPostfix]
        private static void PlayerControllerB_Update_FogReduction(GameNetcodeStuff.PlayerControllerB __instance)
        {
            if (__instance == null || !__instance.isPlayerControlled || __instance != GameNetworkManager.Instance?.localPlayerController)
                return;

            try
            {
                var tod = TimeOfDay.Instance;
                if (tod == null || tod.foggyWeather == null) return;

                bool isInside = __instance.isInsideFactory;
                if (isInside != _wasInsideFactory)
                {
                    _wasInsideFactory = isInside;

                    if (isInside)
                    {
                        // Player entered facility
                        if (_originalFogMeanFreePath < 0f)
                        {
                            _originalFogMeanFreePath = tod.foggyWeather.parameters.meanFreePath;
                        }
                        // 10% fog density means 10x meanFreePath
                        tod.foggyWeather.parameters.meanFreePath = _originalFogMeanFreePath * 10.0f;
                        CustomPatchesPlugin.Log.LogInfo($"[WeatherFix] Fog reduced to 10% inside facility (meanFreePath: {_originalFogMeanFreePath} -> {tod.foggyWeather.parameters.meanFreePath}).");
                    }
                    else
                    {
                        // Player exited to outside
                        if (_originalFogMeanFreePath > 0f)
                        {
                            tod.foggyWeather.parameters.meanFreePath = _originalFogMeanFreePath;
                            CustomPatchesPlugin.Log.LogInfo($"[WeatherFix] Fog restored to 100% outside facility (meanFreePath: {_originalFogMeanFreePath}).");
                        }
                    }
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(RoundManager), "DespawnPropsAtEndOfRound")]
        [HarmonyPostfix]
        private static void DespawnPropsAtEndOfRound_ResetFog()
        {
            _originalFogMeanFreePath = -1f;
            _wasInsideFactory = false;
        }
    }
}
