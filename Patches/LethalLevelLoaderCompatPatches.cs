using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class LethalLevelLoaderCompatPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                // Find ExtendedMod and ExtendedFootstepSurface types dynamically
                Type extendedModType = AccessTools.TypeByName("LethalLevelLoader.ExtendedMod");
                Type extendedFootstepType = AccessTools.TypeByName("LethalLevelLoader.ExtendedFootstepSurface");
                Type extendedContentType = AccessTools.TypeByName("LethalLevelLoader.ExtendedContent");

                if (extendedModType != null)
                {
                    MethodInfo tryThrowMethod = AccessTools.Method(extendedModType, "TryThrowInvalidContentException");
                    if (tryThrowMethod != null)
                    {
                        MethodInfo prefix = AccessTools.Method(typeof(LethalLevelLoaderCompatPatches), nameof(TryThrowInvalidContentException_Prefix));
                        harmony.Patch(tryThrowMethod, prefix: new HarmonyMethod(prefix));
                        CustomPatchesPlugin.Log.LogInfo("[LLL-Compat] Patched ExtendedMod.TryThrowInvalidContentException successfully.");
                    }

                    if (extendedFootstepType != null)
                    {
                        MethodInfo registerFootstepMethod = AccessTools.Method(extendedModType, "RegisterExtendedContent", new Type[] { extendedFootstepType });
                        if (registerFootstepMethod != null)
                        {
                            MethodInfo prefix = AccessTools.Method(typeof(LethalLevelLoaderCompatPatches), nameof(RegisterExtendedFootstepSurface_Prefix));
                            harmony.Patch(registerFootstepMethod, prefix: new HarmonyMethod(prefix));
                            CustomPatchesPlugin.Log.LogInfo("[LLL-Compat] Patched ExtendedMod.RegisterExtendedContent(ExtendedFootstepSurface) successfully.");
                        }
                    }
                }

                if (extendedFootstepType != null)
                {
                    MethodInfo validateMethod = AccessTools.Method(extendedFootstepType, "TryValidateContent");
                    if (validateMethod != null)
                    {
                        MethodInfo prefix = AccessTools.Method(typeof(LethalLevelLoaderCompatPatches), nameof(TryValidateContent_Prefix));
                        harmony.Patch(validateMethod, prefix: new HarmonyMethod(prefix));
                        CustomPatchesPlugin.Log.LogInfo("[LLL-Compat] Patched ExtendedFootstepSurface.TryValidateContent successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError("[LLL-Compat] Failed to apply LethalLevelLoaderCompatPatches: " + ex);
            }
        }

        // Suppress throwing exceptions for failed validation checks on legacy custom content
        private static bool TryThrowInvalidContentException_Prefix(object extendedContent, object result)
        {
            if (result != null)
            {
                try
                {
                    // result is ValueTuple<ContentStatus, string>
                    var fields = result.GetType().GetFields();
                    string reason = "";
                    foreach (var f in fields)
                    {
                        if (f.FieldType == typeof(string))
                        {
                            reason = (string)f.GetValue(result) ?? "";
                            break;
                        }
                    }

                    CustomPatchesPlugin.Log.LogWarning($"[LLL-Compat] Intercepted invalid content validation ({extendedContent}): {reason}. Preventing crash/exception.");
                }
                catch { }
            }
            // Skip the original method so no ArgumentException is thrown!
            return false;
        }

        // If an ExtendedFootstepSurface has empty associated terrain names/materials, ensure it doesn't crash validation
        private static void RegisterExtendedFootstepSurface_Prefix(object extendedFootstepSurface)
        {
            if (extendedFootstepSurface == null) return;
            try
            {
                var dictProp = AccessTools.Property(extendedFootstepSurface.GetType(), "AssociatedTerrainNames");
                if (dictProp != null)
                {
                    var dict = dictProp.GetValue(extendedFootstepSurface);
                    if (dict == null)
                    {
                        dictProp.SetValue(extendedFootstepSurface, new Dictionary<string, int>());
                    }
                }
            }
            catch { }
        }

        // Return a successful validation tuple (ContentStatus.Valid, "") instead of failing
        private static bool TryValidateContent_Prefix(object __instance, ref object __result, MethodBase __originalMethod)
        {
            if (__instance == null) return true;
            try
            {
                var returnType = ((MethodInfo)__originalMethod).ReturnType;
                // If the return type is a ValueTuple<ContentStatus, string>
                Type[] typeArgs = returnType.GenericTypeArguments;
                if (typeArgs != null && typeArgs.Length > 0)
                {
                    Type statusType = typeArgs[0];
                    object validStatus = null;

                    if (statusType.IsEnum)
                    {
                        Array values = Enum.GetValues(statusType);
                        foreach (var v in values)
                        {
                            if (v.ToString().Equals("Valid", StringComparison.OrdinalIgnoreCase))
                            {
                                validStatus = v;
                                break;
                            }
                        }
                        if (validStatus == null && values.Length > 0)
                        {
                            validStatus = values.GetValue(0);
                        }
                    }
                    else
                    {
                        // Fallback if not directly an enum
                        validStatus = Activator.CreateInstance(statusType);
                    }

                    __result = Activator.CreateInstance(returnType, validStatus, string.Empty);
                    return false; // Skip original validation that throws on empty materials
                }

                return true;
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning("[LLL-Compat] Non-fatal in TryValidateContent_Prefix: " + ex.Message);
                return true;
            }
        }
    }
}
