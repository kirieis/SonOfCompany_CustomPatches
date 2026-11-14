using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Items
{
    public static class ItemRegistryPatches
    {
        public static Item SpeedloaderItemRef { get; private set; }
        public static Item AmmoBoxItemRef { get; private set; }
        public static Item TacticalBeltItemRef { get; private set; }
        public static Item MagazineItemRef { get; private set; }
        public static Item RifleItemRef { get; private set; }

        private static bool _registered = false;

        [HarmonyPatch(typeof(Terminal), "Start")]
        [HarmonyPostfix]
        private static void Terminal_Start_Postfix(Terminal __instance)
        {
            if (__instance == null || _registered) return;
            _registered = true;

            try
            {
                PatchStoreItems(__instance);
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogError($"[ItemRegistry] Failed to patch store items: {ex}");
            }
        }

        private static void PatchStoreItems(Terminal terminal)
        {
            if (terminal == null || terminal.buyableItemsList == null) return;

            List<Item> buyables = new List<Item>(terminal.buyableItemsList);
            int originalCount = buyables.Count;

            // --- 1. Xóa toàn bộ tàn dư BetterArmory (Assault Rifle, v.v.) ---
            int removedAR = buyables.RemoveAll(it =>
                it != null && (
                    it.itemName.Equals("Assault Rifle", StringComparison.OrdinalIgnoreCase) ||
                    it.itemName.Contains("MX16") ||
                    (it.itemName.ToLower().Contains("assault") && it.itemName.ToLower().Contains("rifle"))
                )
            );
            if (removedAR > 0)
                CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Removed {removedAR} Assault Rifle entry/entries from store.");

            // --- 2. Xóa BetterArmory Revolver khỏi store (CHỈ GIỮ LẠI Piggy Revolver) ---
            int removedBARev = buyables.RemoveAll(it =>
            {
                if (it == null) return false;
                string n = it.itemName.ToLower().Trim();
                if (n == "revolver")
                {
                    bool isPiggy = it.spawnPrefab != null && it.spawnPrefab.GetComponent("RevolverItem") != null;
                    if (!isPiggy)
                    {
                        CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Removed non-Piggy Revolver from store.");
                        return true;
                    }
                }
                return false;
            });

            // --- 3. Đặt giá Magazine = 60$ ---
            foreach (var it in buyables)
            {
                if (it == null) continue;
                string lower = it.itemName.ToLower();
                if (lower.Contains("magazine") || lower.Contains("mag"))
                {
                    it.creditsWorth = 60;
                    MagazineItemRef = it;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $60.");
                }
            }

            // --- 4. Giá các loại súng (+40$, Pistol = $125) ---
            foreach (var it in buyables)
            {
                if (it == null) continue;
                string lower = it.itemName.ToLower().Trim();

                if (lower == "pistol" || lower.Contains("x18"))
                {
                    it.creditsWorth = 125;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $125.");
                }
                else if (lower.Contains("bolt") || lower.Contains("kar98"))
                {
                    it.creditsWorth = 205;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $205.");
                }
                else if (lower.Contains("heavy shotgun"))
                {
                    it.creditsWorth = 150;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $150.");
                }
                else if (lower.Contains("double-barreled shotgun") || (lower.Contains("shotgun") && !lower.Contains("heavy")))
                {
                    it.creditsWorth = 706;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $706.");
                }
                else if (lower == "revolver")
                {
                    it.creditsWorth = 120; // 80 + 40
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $120.");
                }
            }

            // --- 5. Đảm bảo Piggy Rifle (M4) có trong store: Đổi tên thành "M16A Rifle", giá 240$ ---
            Item rifleItem = buyables.Find(it =>
                it != null && (
                    it.itemName.Equals("M16A Rifle", StringComparison.OrdinalIgnoreCase) ||
                    it.itemName.Equals("Rifle", StringComparison.OrdinalIgnoreCase) ||
                    it.itemName.Equals("M4", StringComparison.OrdinalIgnoreCase) ||
                    it.itemName.ToLower() == "rifle"
                )
            );

            if (rifleItem == null)
            {
                Item[] allItems = Resources.FindObjectsOfTypeAll<Item>();
                foreach (var item in allItems)
                {
                    if (item == null || item.spawnPrefab == null) continue;
                    string n = item.itemName.ToLower();
                    if (n == "rifle" || n == "m4" || n.Contains("m4item"))
                    {
                        rifleItem = item;
                        break;
                    }
                }

                if (rifleItem == null)
                {
                    Type m4Type = AccessTools.TypeByName("PiggyVarietyMod.Patches.M4Item");
                    if (m4Type != null)
                    {
                        foreach (var item in allItems)
                        {
                            if (item == null || item.spawnPrefab == null) continue;
                            if (item.spawnPrefab.GetComponent(m4Type) != null)
                            {
                                rifleItem = item;
                                break;
                            }
                        }
                    }
                }

                if (rifleItem != null)
                {
                    buyables.Add(rifleItem);
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Added Piggy Rifle to store.");
                }
            }

            if (rifleItem != null)
            {
                rifleItem.itemName = "M16A Rifle";
                rifleItem.creditsWorth = 240; // 200 + 40
                RifleItemRef = rifleItem;
                CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set Rifle item name to 'M16A Rifle' and price to $240.");
            }

            terminal.buyableItemsList = buyables.ToArray();

            // --- 6. Đồng bộ toàn bộ Buy Nouns & Indices ---
            SyncTerminalBuyNodes(terminal);

            CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Store patched successfully: {originalCount} → {buyables.Count} items.");
        }

        private static void SyncTerminalBuyNodes(Terminal terminal)
        {
            if (terminal == null || terminal.terminalNodes == null || terminal.terminalNodes.allKeywords == null) return;

            TerminalKeyword buyKeyword = Array.Find(terminal.terminalNodes.allKeywords, k => k != null && k.isVerb && k.word == "buy");
            if (buyKeyword == null || buyKeyword.compatibleNouns == null) return;

            List<CompatibleNoun> nounsList = new List<CompatibleNoun>(buyKeyword.compatibleNouns);

            // Tìm index của M16A Rifle trong buyableItemsList
            int m16Index = -1;
            int m16Cost = 240;
            for (int i = 0; i < terminal.buyableItemsList.Length; i++)
            {
                var it = terminal.buyableItemsList[i];
                if (it != null && (it.itemName.Contains("M16") || it.itemName.Equals("Rifle", StringComparison.OrdinalIgnoreCase) || it.itemName.Contains("M4")))
                {
                    m16Index = i;
                    m16Cost = it.creditsWorth;
                    break;
                }
            }

            for (int cnIdx = 0; cnIdx < nounsList.Count; cnIdx++)
            {
                var cn = nounsList[cnIdx];
                if (cn == null || cn.noun == null || cn.result == null) continue;
                string word = cn.noun.word.ToLower().Trim();

                if (word == "rifle" || word == "m16" || word == "m16a" || word == "m16a4")
                {
                    if (m16Index >= 0)
                    {
                        cn.result.buyItemIndex = m16Index;
                        cn.result.itemCost = m16Cost;
                    }
                }
                else
                {
                    for (int i = 0; i < terminal.buyableItemsList.Length; i++)
                    {
                        Item item = terminal.buyableItemsList[i];
                        if (item == null) continue;
                        string itemName = item.itemName.ToLower().Trim();

                        if (word == "pistol" && (itemName == "pistol" || itemName.Contains("x18")))
                        {
                            cn.result.buyItemIndex = i;
                            cn.result.itemCost = item.creditsWorth;
                            break;
                        }
                        else if (word == "revolver" && itemName == "revolver")
                        {
                            cn.result.buyItemIndex = i;
                            cn.result.itemCost = item.creditsWorth;
                            break;
                        }
                        else if (word == "magazine" && (itemName.Contains("magazine") || itemName.Contains("mag")))
                        {
                            cn.result.buyItemIndex = i;
                            cn.result.itemCost = item.creditsWorth;
                            break;
                        }
                        else if (itemName == word)
                        {
                            cn.result.buyItemIndex = i;
                            cn.result.itemCost = item.creditsWorth;
                            break;
                        }
                    }
                }
            }

            // Đảm bảo có các alias keyword cho M16A Rifle: "m16", "m16a", "m16a4"
            if (m16Index >= 0)
            {
                CompatibleNoun templateCn = nounsList.Find(n => n?.noun?.word == "rifle" || n?.noun?.word == "m16a");
                if (templateCn != null && templateCn.result != null)
                {
                    string[] aliases = new string[] { "m16", "m16a", "m16a4" };
                    foreach (var alias in aliases)
                    {
                        if (!nounsList.Exists(n => n?.noun?.word == alias))
                        {
                            TerminalKeyword newNoun = ScriptableObject.CreateInstance<TerminalKeyword>();
                            newNoun.word = alias;
                            newNoun.isVerb = false;

                            TerminalNode resultCopy = ScriptableObject.Instantiate(templateCn.result);
                            resultCopy.buyItemIndex = m16Index;
                            resultCopy.itemCost = m16Cost;

                            CompatibleNoun newCn = new CompatibleNoun(newNoun, resultCopy);
                            nounsList.Add(newCn);
                        }
                    }
                }
            }

            buyKeyword.compatibleNouns = nounsList.ToArray();
            CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Synchronized all Terminal buy noun indices and costs.");
        }

        // ---------------------------------------------------------
        // 7. FIX LỖI GIÁ 2880: CHẶN REGEX \d+ NHẬN SỐ "16" TRONG "M16" LÀ SỐ LƯỢNG MUA
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyPostfix]
        private static void Terminal_ParsePlayerSentence_Postfix(Terminal __instance, ref TerminalNode __result)
        {
            if (__instance == null) return;

            string text = "";
            try
            {
                text = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded);
            }
            catch { }

            string lower = text.ToLower().Trim();

            // Nếu câu lệnh nhập vào chứa m16, m16a, m16a4
            if (lower.Contains("m16"))
            {
                // Loại bỏ từ khóa định danh vũ khí
                string sanitized = Regex.Replace(lower, @"\bm16a4\b|\bm16a\b|\bm16\b|\brifle\b|\bbuy\b", "", RegexOptions.IgnoreCase).Trim();
                Match match = Regex.Match(sanitized, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int count) && count > 0)
                {
                    __instance.playerDefinedAmount = Mathf.Clamp(count, 1, 16);
                }
                else
                {
                    // Người chơi chỉ gõ tên vũ khí ("m16", "m16a", "m16a4", v.v.) mà KHÔNG gõ số lượng riêng -> Số lượng mua bắt buộc là 1!
                    __instance.playerDefinedAmount = 1;
                }

                // Nếu Terminal đã xử lý ra node xác nhận mua, tính lại giá tiền hiển thị đúng theo số lượng thực
                if (__result != null && __result.itemCost > 0)
                {
                    int totalCost = __result.itemCost * __instance.playerDefinedAmount;
                    if (__result.displayText != null && __result.displayText.Contains("Total cost of these items is"))
                    {
                        __result.displayText = Regex.Replace(__result.displayText, @"Total cost of these items is \*\s*\d+", $"Total cost of these items is * {totalCost}");
                        __result.displayText = Regex.Replace(__result.displayText, @"Total cost of these items is \d+", $"Total cost of these items is {totalCost}");
                    }
                }
            }
        }

        // Xử lý lệnh mua "Larger Belt Slot" trong Terminal
        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyPrefix]
        private static bool Terminal_ParsePlayerSentence_Prefix(Terminal __instance, ref TerminalNode __result)
        {
            if (__instance == null) return true;

            string text = "";
            try
            {
                text = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded);
            }
            catch { }

            string lower = text.ToLower().Trim();

            if (lower == "buy larger belt slot" || lower == "larger belt slot" || lower == "belt upgrade")
            {
                if (TacticalBeltItem.LocalBelt != null)
                {
                    if (TacticalBeltItem.LocalBelt.currentCapacity >= TacticalBeltItem.LocalBelt.maxCapacity)
                    {
                        TerminalNode nodeMax = ScriptableObject.CreateInstance<TerminalNode>();
                        nodeMax.displayText = "\nYour Tactical Belt is already at the maximum capacity (6 slots)!\n\n";
                        __result = nodeMax;
                        return false;
                    }

                    if (__instance.groupCredits >= 50)
                    {
                        __instance.groupCredits -= 50;
                        TacticalBeltItem.LocalBelt.UpgradeBeltCapacity();
                        TerminalNode nodeSuccess = ScriptableObject.CreateInstance<TerminalNode>();
                        nodeSuccess.displayText = $"\nSuccessfully purchased Larger Belt Slot for $50! New Belt Capacity: {TacticalBeltItem.LocalBelt.currentCapacity}\n\n";
                        __result = nodeSuccess;
                        return false;
                    }
                    else
                    {
                        TerminalNode nodeBroke = ScriptableObject.CreateInstance<TerminalNode>();
                        nodeBroke.displayText = "\nYou cannot afford this upgrade. Required: $50.\n\n";
                        __result = nodeBroke;
                        return false;
                    }
                }
                else
                {
                    TerminalNode nodeNoBelt = ScriptableObject.CreateInstance<TerminalNode>();
                    nodeNoBelt.displayText = "\nYou must have a Tactical Belt equipped to purchase Larger Belt Slot!\n\n";
                    __result = nodeNoBelt;
                    return false;
                }
            }

            return true;
        }
    }
}
