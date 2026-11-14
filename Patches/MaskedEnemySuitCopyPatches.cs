using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class MaskedEnemySuitCopyPatches
    {
        // ---------------------------------------------------------
        // MIMIC / MASKED ENEMY: COPY SUIT OF ONE OF THE PLAYERS IN MULTIPLAYER
        // ---------------------------------------------------------

        public static List<PlayerControllerB> GetActiveMultiplayerPlayers()
        {
            List<PlayerControllerB> list = new List<PlayerControllerB>();
            if (StartOfRound.Instance == null || StartOfRound.Instance.allPlayerScripts == null)
                return list;

            foreach (var p in StartOfRound.Instance.allPlayerScripts)
            {
                if (p != null && (p.isPlayerControlled || p.isPlayerDead))
                {
                    list.Add(p);
                }
            }

            // Sắp xếp deterministically theo playerClientId để tất cả client đồng bộ như nhau
            list.Sort((a, b) => a.playerClientId.CompareTo(b.playerClientId));
            return list;
        }

        public static void ApplyPlayerSuitToMasked(MaskedPlayerEnemy masked, PlayerControllerB targetPlayer = null)
        {
            if (masked == null) return;

            var players = GetActiveMultiplayerPlayers();
            if (players.Count == 0) return;

            if (targetPlayer == null)
            {
                if (masked.mimickingPlayer != null && players.Contains(masked.mimickingPlayer))
                {
                    targetPlayer = masked.mimickingPlayer;
                }
                else
                {
                    // Lựa chọn ngẫu nhiên nhưng đồng nhất giữa host và mọi client dựa trên NetworkObjectId
                    int index = (int)(masked.NetworkObjectId % (ulong)players.Count);
                    targetPlayer = players[index];
                    masked.mimickingPlayer = targetPlayer;
                }
            }

            if (targetPlayer == null) return;

            try
            {
                int suitId = targetPlayer.currentSuitID;
                if (suitId >= 0 && StartOfRound.Instance?.unlockablesList?.unlockables != null && suitId < StartOfRound.Instance.unlockablesList.unlockables.Count)
                {
                    masked.SetSuit(suitId);
                }

                // Copy chính xác vật liệu và texture đồ họa (bao gồm MoreSuits và ODST Suit) từ player model
                if (targetPlayer.thisPlayerModel != null)
                {
                    Material[] playerMats = targetPlayer.thisPlayerModel.sharedMaterials;
                    if (playerMats != null && playerMats.Length > 0 && playerMats[0] != null)
                    {
                        if (masked.rendererLOD0 != null) masked.rendererLOD0.sharedMaterials = playerMats;
                        if (masked.rendererLOD1 != null) masked.rendererLOD1.sharedMaterials = playerMats;
                        if (masked.rendererLOD2 != null) masked.rendererLOD2.sharedMaterials = playerMats;
                    }
                }

                CustomPatchesPlugin.Log.LogInfo($"[MaskedSuitCopy] Mimic {masked.NetworkObjectId} disguised as player '{targetPlayer.playerUsername}' (Suit ID: {targetPlayer.currentSuitID}).");
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[MaskedSuitCopy] Error copying suit to mimic: {ex}");
            }
        }

        // 1. Khi Masked Enemy spawn
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Start")]
        [HarmonyPostfix]
        private static void MaskedPlayerEnemy_Start_Postfix(MaskedPlayerEnemy __instance)
        {
            ApplyPlayerSuitToMasked(__instance);
        }

        // 2. Khi Masked Enemy spawn tự nhiên không có player ban đầu
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "SetEnemyAsHavingNoPlayerClientRpc")]
        [HarmonyPostfix]
        private static void MaskedPlayerEnemy_SetEnemyAsHavingNoPlayerClientRpc_Postfix(MaskedPlayerEnemy __instance)
        {
            ApplyPlayerSuitToMasked(__instance);
        }

        // 3. Đảm bảo vật liệu custom từ MoreSuits/ODST được giữ lại sau SetSuit
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "SetSuit")]
        [HarmonyPostfix]
        private static void MaskedPlayerEnemy_SetSuit_Postfix(MaskedPlayerEnemy __instance, int suitId)
        {
            if (__instance != null && __instance.mimickingPlayer != null && __instance.mimickingPlayer.thisPlayerModel != null)
            {
                var mats = __instance.mimickingPlayer.thisPlayerModel.sharedMaterials;
                if (mats != null && mats.Length > 0 && mats[0] != null)
                {
                    if (__instance.rendererLOD0 != null) __instance.rendererLOD0.sharedMaterials = mats;
                    if (__instance.rendererLOD1 != null) __instance.rendererLOD1.sharedMaterials = mats;
                    if (__instance.rendererLOD2 != null) __instance.rendererLOD2.sharedMaterials = mats;
                }
            }
        }

        // 4. Định kỳ kiểm tra trong AI interval nếu chưa có mimickingPlayer
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "DoAIInterval")]
        [HarmonyPostfix]
        private static void MaskedPlayerEnemy_DoAIInterval_Postfix(MaskedPlayerEnemy __instance)
        {
            if (__instance == null || __instance.isEnemyDead) return;

            if (__instance.mimickingPlayer == null)
            {
                ApplyPlayerSuitToMasked(__instance);
            }
        }
    }
}
