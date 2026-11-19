using System;
using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public class InteriorOxygenStation : MonoBehaviour
    {
        public int remainingUses = 2;
        public InteractTrigger trigger;
        public AudioSource audioSource;

        private void Start()
        {
            SetupTrigger();
        }

        public void SetupTrigger()
        {
            if (trigger == null)
            {
                trigger = GetComponentInChildren<InteractTrigger>();
                if (trigger == null)
                {
                    trigger = gameObject.AddComponent<InteractTrigger>();
                }
            }

            trigger.hoverTip = "Refill Oxygen : [E] (2 uses left)";
            trigger.disabledHoverTip = "Oxygen Station Depleted";
            trigger.holdInteraction = false;
            trigger.interactable = true;
            trigger.twoHandedItemAllowed = true;
            trigger.oneHandedItemAllowed = true;

            trigger.onInteract.RemoveAllListeners();
            trigger.onInteract.AddListener(OnStationInteracted);
        }

        private void OnStationInteracted(PlayerControllerB player)
        {
            if (remainingUses <= 0)
            {
                if (trigger != null)
                {
                    trigger.interactable = false;
                    trigger.hoverTip = "Oxygen Station Depleted";
                }
                return;
            }

            try
            {
                // Refill 50% oxy tank via reflection on OxygenLogic.OxygenAmount
                Type logicType = AccessTools.TypeByName("Oxygen.General.OxygenLogic");
                if (logicType != null)
                {
                    PropertyInfo prop = AccessTools.Property(logicType, "OxygenAmount");
                    if (prop != null)
                    {
                        float currentOxygen = (float)prop.GetValue(null);
                        float newOxygen = Mathf.Clamp01(currentOxygen + 0.5f);
                        prop.SetValue(null, newOxygen);
                        CustomPatchesPlugin.Log.LogInfo($"[InteriorOxygen] Player refilled oxygen! (50% added -> {newOxygen * 100:0.#}%). Remaining uses: {remainingUses - 1}");
                    }
                }

                remainingUses--;

                if (audioSource != null)
                {
                    AudioClip sfx = GetOxySFX();
                    if (sfx != null)
                    {
                        audioSource.PlayOneShot(sfx);
                    }
                }

                if (remainingUses > 0)
                {
                    trigger.hoverTip = $"Refill Oxygen : [E] ({remainingUses} use left)";
                }
                else
                {
                    trigger.interactable = false;
                    trigger.hoverTip = "Oxygen Station Depleted";
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[InteriorOxygen] Error on refill interaction: {ex}");
            }
        }

        private AudioClip GetOxySFX()
        {
            try
            {
                Type baseType = AccessTools.TypeByName("Oxygen.OxygenBase");
                if (baseType != null)
                {
                    object instance = AccessTools.Property(baseType, "Instance")?.GetValue(null);
                    if (instance != null)
                    {
                        AudioClip[] clips = AccessTools.Field(baseType, "oxyChargerSFX")?.GetValue(instance) as AudioClip[];
                        if (clips != null && clips.Length > 0)
                            return clips[0];
                    }
                }
            }
            catch { }
            return null;
        }
    }

    public static class InteriorOxyStationPatches
    {
        private static List<GameObject> _spawnedStations = new List<GameObject>();

        public static bool IsHazardLevelBOrHigher(SelectableLevel level)
        {
            if (level == null || string.IsNullOrEmpty(level.riskLevel)) return false;
            string risk = level.riskLevel.Trim().ToUpper();

            if (risk.StartsWith("B") || risk.StartsWith("A") || risk.StartsWith("S"))
            {
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(RoundManager), "FinishGeneratingNewLevelClientRpc")]
        [HarmonyPostfix]
        private static void FinishGeneratingNewLevelClientRpc_SpawnOxyStations(RoundManager __instance)
        {
            CleanupStations();

            if (__instance == null || __instance.currentLevel == null) return;
            if (!IsHazardLevelBOrHigher(__instance.currentLevel))
            {
                CustomPatchesPlugin.Log.LogInfo($"[InteriorOxygen] Hazard level '{__instance.currentLevel.riskLevel}' is below B. Skipping interior oxygen station spawn.");
                return;
            }

            try
            {
                var insideNodes = __instance.insideAINodes;
                if (insideNodes == null || insideNodes.Length == 0)
                {
                    CustomPatchesPlugin.Log.LogWarning("[InteriorOxygen] No insideAINodes found to spawn oxygen station.");
                    return;
                }

                GameObject prefab = null;
                Type baseType = AccessTools.TypeByName("Oxygen.OxygenBase");
                if (baseType != null)
                {
                    object instance = AccessTools.Property(baseType, "Instance")?.GetValue(null);
                    if (instance != null)
                    {
                        prefab = AccessTools.Field(baseType, "oxyCharger")?.GetValue(instance) as GameObject;
                    }
                }

                if (prefab == null)
                {
                    CustomPatchesPlugin.Log.LogWarning("[InteriorOxygen] OxygenBase.Instance.oxyCharger prefab not found.");
                    return;
                }

                int seed = (StartOfRound.Instance != null) ? StartOfRound.Instance.randomMapSeed + 8888 : 99999;
                System.Random rnd = new System.Random(seed);

                int countToSpawn = (__instance.currentLevel.riskLevel.StartsWith("S")) ? 2 : 1;

                for (int i = 0; i < countToSpawn; i++)
                {
                    int nodeIdx = rnd.Next(0, insideNodes.Length);
                    Transform targetNode = insideNodes[nodeIdx].transform;
                    Vector3 spawnPos = targetNode.position + Vector3.up * 1.0f;
                    Quaternion rot = Quaternion.Euler(0f, rnd.Next(0, 360), 0f);

                    GameObject stationObj = UnityEngine.Object.Instantiate(prefab, spawnPos, rot);
                    stationObj.name = $"InteriorOxygenStation_{i}";

                    // Xóa script OxyCharger của tàu để tránh conflict network / ship
                    var shipChargerScript = stationObj.GetComponent("OxyCharger");
                    if (shipChargerScript != null)
                    {
                        UnityEngine.Object.Destroy(shipChargerScript);
                    }

                    // Không thể bị di chuyển
                    var grab = stationObj.GetComponent<GrabbableObject>();
                    if (grab != null)
                    {
                        UnityEngine.Object.Destroy(grab);
                    }

                    var rb = stationObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                    }

                    var col = stationObj.GetComponent<Collider>();
                    if (col == null)
                    {
                        var box = stationObj.AddComponent<BoxCollider>();
                        box.size = new Vector3(0.8f, 1.2f, 0.8f);
                    }

                    var stationComp = stationObj.AddComponent<InteriorOxygenStation>();
                    stationComp.audioSource = stationObj.GetComponentInChildren<AudioSource>();
                    stationComp.SetupTrigger();

                    _spawnedStations.Add(stationObj);
                    CustomPatchesPlugin.Log.LogInfo($"[InteriorOxygen] Spawned stationary oxygen refill station at {spawnPos} on moon {__instance.currentLevel.PlanetName} (Risk: {__instance.currentLevel.riskLevel}).");
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError($"[InteriorOxygen] Failed to spawn interior oxygen stations: {ex}");
            }
        }

        [HarmonyPatch(typeof(RoundManager), "DespawnPropsAtEndOfRound")]
        [HarmonyPostfix]
        private static void DespawnPropsAtEndOfRound_Postfix()
        {
            CleanupStations();
        }

        private static void CleanupStations()
        {
            foreach (var s in _spawnedStations)
            {
                if (s != null)
                {
                    try { UnityEngine.Object.Destroy(s); } catch { }
                }
            }
            _spawnedStations.Clear();
        }
    }
}
