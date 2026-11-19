using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class LethalModDataLibFixPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Type collectorType = AccessTools.TypeByName("LethalModDataLib.Features.ModDataAttributeCollector");
                if (collectorType != null)
                {
                    MethodInfo target = AccessTools.Method(collectorType, "RegisterModDataAttributes", Type.EmptyTypes);
                    if (target != null)
                    {
                        MethodInfo prefix = AccessTools.Method(typeof(LethalModDataLibFixPatches), nameof(RegisterModDataAttributes_Prefix));
                        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                        CustomPatchesPlugin.Log.LogInfo("[ModDataLib-Fix] Patched ModDataAttributeCollector.RegisterModDataAttributes successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError("[ModDataLib-Fix] Failed to patch LethalModDataLib: " + ex);
            }
        }

        // Replace the default scan with a safe iteration that ignores null instances or missing types
        private static bool RegisterModDataAttributes_Prefix()
        {
            try
            {
                Type collectorType = AccessTools.TypeByName("LethalModDataLib.Features.ModDataAttributeCollector");
                Type helperType = AccessTools.TypeByName("LethalModDataLib.Helpers.AssemblyExtensions");
                MethodInfo getLoadableTypes = helperType != null ? AccessTools.Method(helperType, "GetLoadableTypes", new Type[] { typeof(Assembly) }) : null;
                MethodInfo register4 = collectorType != null ? AccessTools.Method(collectorType, "RegisterModDataAttributes", new Type[] { typeof(string), typeof(Type), typeof(object), typeof(string) }) : null;

                if (collectorType == null || register4 == null) return true;

                foreach (KeyValuePair<string, PluginInfo> pair in Chainloader.PluginInfos)
                {
                    PluginInfo pluginInfo = pair.Value;
                    if (pluginInfo == null || pluginInfo.Instance == null) continue;

                    Assembly assembly = null;
                    try
                    {
                        assembly = pluginInfo.Instance.GetType().Assembly;
                    }
                    catch { continue; }

                    if (assembly == null) continue;

                    string guid = pluginInfo.Metadata != null ? pluginInfo.Metadata.GUID : pair.Key;

                    IEnumerable<Type> types = null;
                    try
                    {
                        if (getLoadableTypes != null)
                        {
                            types = getLoadableTypes.Invoke(null, new object[] { assembly }) as IEnumerable<Type>;
                        }
                        else
                        {
                            types = assembly.GetTypes();
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types;
                    }
                    catch { continue; }

                    if (types == null) continue;

                    foreach (Type t in types)
                    {
                        if (t == null) continue;
                        try
                        {
                            register4.Invoke(null, new object[] { guid, t, null, null });
                        }
                        catch { }
                    }
                }

                CustomPatchesPlugin.Log.LogInfo("[ModDataLib-Fix] Safely registered all ModDataAttribute fields.");
                return false; // Skip original method that throws NullReferenceException!
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError("[ModDataLib-Fix] Error in RegisterModDataAttributes_Prefix: " + ex);
                return true;
            }
        }
    }
}
