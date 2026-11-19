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
        public static Item RifleItemRef { get; private set; }
        public static Item ExecutionBulletItemRef { get; private set; }
        public static Item ShotgunShellItemRef { get; private set; }
        public static Item BrokenRevolverItemRef { get; private set; }

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

            // --- 3. Tìm Revolver Ammo để tạo Execution Bullet ($9999) trước khi xóa đạn thường khỏi store ---
            Item baseRevAmmo = buyables.Find(it => it != null && it.itemName.ToLower().Contains("revolver") && it.itemName.ToLower().Contains("ammo"));
            if (baseRevAmmo == null)
            {
                Item[] allItems = Resources.FindObjectsOfTypeAll<Item>();
                baseRevAmmo = Array.Find(allItems, it => it != null && it.itemName != null && it.itemName.ToLower().Contains("revolver") && it.itemName.ToLower().Contains("ammo"));
            }

            if (baseRevAmmo != null && ExecutionBulletItemRef == null)
            {
                try
                {
                    Item execBullet = ScriptableObject.Instantiate(baseRevAmmo);
                    execBullet.name = "ExecutionBulletItem";
                    execBullet.itemName = "Execution Bullet";
                    execBullet.creditsWorth = 9999;

                    if (execBullet.spawnPrefab != null)
                    {
                        execBullet.spawnPrefab = UnityEngine.Object.Instantiate(execBullet.spawnPrefab);
                        execBullet.spawnPrefab.name = "ExecutionBullet";
                        if (execBullet.spawnPrefab.GetComponent<ExecutionBulletTag>() == null)
                        {
                            execBullet.spawnPrefab.AddComponent<ExecutionBulletTag>();
                        }
                    }

                    ExecutionBulletItemRef = execBullet;
                    buyables.Add(execBullet);
                    CustomPatchesPlugin.Log.LogInfo("[ItemRegistry] Created & registered Execution Bullet in store for $9999!");
                }
                catch (Exception ex)
                {
                    CustomPatchesPlugin.Log.LogError($"[ItemRegistry] Failed to create Execution Bullet item: {ex}");
                }
            }

            // Tìm Shell Item để đảm bảo drop dưới dạng scrap sau này
            foreach (var it in buyables)
            {
                if (it != null && (it.itemName.Equals("Shell", StringComparison.OrdinalIgnoreCase) || it.itemName.ToLower().Contains("shotgun shell")))
                {
                    ShotgunShellItemRef = it;
                    break;
                }
            }
            if (ShotgunShellItemRef == null)
            {
                Item[] allItems = Resources.FindObjectsOfTypeAll<Item>();
                ShotgunShellItemRef = Array.Find(allItems, it => it != null && it.itemName != null && (it.itemName.Equals("Shell", StringComparison.OrdinalIgnoreCase) || it.itemName.ToLower().Contains("shotgun shell")));
            }

            // --- 4. Xóa Revolver bullet, Magazine, Shell, Jet Wings khỏi store ---
            int removedAmmoAndWings = buyables.RemoveAll(it =>
            {
                if (it == null) return false;
                string lower = it.itemName.ToLower().Trim();

                // Đạn thường của Revolver
                if (lower.Contains("revolver") && lower.Contains("ammo"))
                    return true;
                if (lower.Equals("revolver bullet", StringComparison.OrdinalIgnoreCase) || lower.Equals("bullet", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Magazine của Rifle
                if (lower.Contains("magazine") || lower.Contains("mag"))
                    return true;

                // Shell của Shotgun
                if (lower.Equals("shell") || lower.Contains("shotgun shell"))
                    return true;

                // Jet Wings
                if (lower.Contains("jet wings") || lower.Contains("jet wing") || lower.Equals("jet wings"))
                    return true;

                return false;
            });
            CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Removed {removedAmmoAndWings} items from store (Revolver bullet, Magazine, Shell, Jet Wings).");

            // --- 5. Đặt giá vũ khí: Revolver = $888, Rifle = $999, Double-barreled shotgun = $800, Zappinator = $650 ---
            foreach (var it in buyables)
            {
                if (it == null) continue;
                string lower = it.itemName.ToLower().Trim();

                if (lower == "revolver")
                {
                    it.creditsWorth = 888;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set Revolver price to $888.");
                }
                else if (lower.Contains("double-barreled shotgun") || (lower.Contains("shotgun") && !lower.Contains("heavy")))
                {
                    it.creditsWorth = 800;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $800.");
                }
                else if (lower.Contains("zappinator") || lower.Contains("zap gun") || lower == "zappinator")
                {
                    it.creditsWorth = 650;
                    CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set '{it.itemName}' price to $650.");
                }
                else if (lower == "pistol" || lower.Contains("x18"))
                {
                    it.creditsWorth = 125;
                }
                else if (lower.Contains("bolt") || lower.Contains("kar98"))
                {
                    it.creditsWorth = 205;
                }
                else if (lower.Contains("heavy shotgun"))
                {
                    it.creditsWorth = 150;
                }
            }

            // --- 6. Đảm bảo Piggy Rifle (M4) có trong store: Đổi tên thành "M16A Rifle", giá $999 ---
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
                rifleItem.creditsWorth = 999; // $999
                RifleItemRef = rifleItem;
                CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Set Rifle item name to 'M16A Rifle' and price to $999.");
            }

            terminal.buyableItemsList = buyables.ToArray();

            // --- 7. Đồng bộ toàn bộ Buy Nouns & Indices ---
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
            int m16Cost = 999;
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

            // Tìm index của Execution Bullet trong buyableItemsList
            int execIndex = -1;
            int execCost = 9999;
            for (int i = 0; i < terminal.buyableItemsList.Length; i++)
            {
                var it = terminal.buyableItemsList[i];
                if (it != null && it.itemName.Equals("Execution Bullet", StringComparison.OrdinalIgnoreCase))
                {
                    execIndex = i;
                    execCost = it.creditsWorth;
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

                        if (word == "revolver" && itemName == "revolver")
                        {
                            cn.result.buyItemIndex = i;
                            cn.result.itemCost = item.creditsWorth;
                            break;
                        }
                        else if (word.Contains("zappinator") && itemName.Contains("zappinator"))
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

            // Đảm bảo có noun cho Execution Bullet
            if (execIndex >= 0)
            {
                CompatibleNoun templateCn = nounsList.Find(n => n?.result != null && n.result.buyItemIndex >= 0);
                if (templateCn != null && templateCn.result != null)
                {
                    string[] aliases = new string[] { "execution", "execution bullet" };
                    foreach (var alias in aliases)
                    {
                        if (!nounsList.Exists(n => n?.noun?.word == alias))
                        {
                            TerminalKeyword newNoun = ScriptableObject.CreateInstance<TerminalKeyword>();
                            newNoun.word = alias;
                            newNoun.isVerb = false;

                            TerminalNode resultCopy = ScriptableObject.Instantiate(templateCn.result);
                            resultCopy.buyItemIndex = execIndex;
                            resultCopy.itemCost = execCost;
                            resultCopy.displayText = $"\nYou have requested to order Execution Bullet.\nTotal cost: ${execCost}.\n\nPlease CONFIRM or DENY.\n\n";

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
        // INJECT SCRAP RARITY (SHELL = 50) INTO MOONS
        // ---------------------------------------------------------
        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPrefix]
        private static void SpawnScrapInLevel_InjectAmmoScrapRarities(RoundManager __instance)
        {
            if (__instance == null || __instance.currentLevel == null) return;
            try
            {
                var scrapList = __instance.currentLevel.spawnableScrap;
                if (scrapList == null) return;

                // 1. Đảm bảo Shell có trong danh sách scrap với rarity = 50
                if (ShotgunShellItemRef != null)
                {
                    var existing = scrapList.Find(s => s != null && s.spawnableItem != null && (s.spawnableItem == ShotgunShellItemRef || s.spawnableItem.itemName.Equals("Shell", StringComparison.OrdinalIgnoreCase)));
                    if (existing != null)
                    {
                        existing.rarity = 50;
                    }
                    else
                    {
                        scrapList.Add(new SpawnableItemWithRarity(ShotgunShellItemRef, 50));
                        CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Injected Shell into {__instance.currentLevel.PlanetName} scrap pool with rarity 50.");
                    }
                }

                // 2. Đảm bảo Revolver Ammo & Rifle Mag có rarity = 70
                foreach (var s in scrapList)
                {
                    if (s == null || s.spawnableItem == null || string.IsNullOrEmpty(s.spawnableItem.itemName)) continue;
                    string n = s.spawnableItem.itemName.ToLower();
                    if (n.Contains("revolver") && n.Contains("ammo"))
                    {
                        s.rarity = 70;
                    }
                    else if (n.Contains("magazine") || n.Contains("mag"))
                    {
                        s.rarity = 70;
                    }
                }

                // 3. Khởi tạo Broken Revolver scrap item ($31 - $91) và thay thế toàn bộ Revolver scrap trên moon
                if (BrokenRevolverItemRef == null)
                {
                    Item[] allItems = Resources.FindObjectsOfTypeAll<Item>();
                    Item piggyRev = Array.Find(allItems, it => it != null && it.itemName == "Revolver" && it.spawnPrefab != null && it.spawnPrefab.GetComponent("RevolverItem") != null);
                    if (piggyRev != null)
                    {
                        BrokenRevolverItemRef = ScriptableObject.Instantiate(piggyRev);
                        BrokenRevolverItemRef.name = "BrokenRevolverItem";
                        BrokenRevolverItemRef.itemName = "Broken Revolver";
                        BrokenRevolverItemRef.minValue = 31;
                        BrokenRevolverItemRef.maxValue = 91;
                        BrokenRevolverItemRef.isScrap = true;
                        BrokenRevolverItemRef.creditsWorth = 0;
                        BrokenRevolverItemRef.requiresBattery = false;
                        BrokenRevolverItemRef.batteryUsage = 0f;

                        if (BrokenRevolverItemRef.spawnPrefab != null)
                        {
                            BrokenRevolverItemRef.spawnPrefab = UnityEngine.Object.Instantiate(BrokenRevolverItemRef.spawnPrefab);
                            BrokenRevolverItemRef.spawnPrefab.name = "BrokenRevolver";

                            var scanNode = BrokenRevolverItemRef.spawnPrefab.GetComponentInChildren<ScanNodeProperties>();
                            if (scanNode != null)
                            {
                                scanNode.headerText = "Broken Revolver";
                                scanNode.subText = "Value: ";
                                scanNode.nodeType = 2; // Scrap
                                scanNode.minRange = 1;
                                scanNode.maxRange = 13;
                            }

                            var grabbable = BrokenRevolverItemRef.spawnPrefab.GetComponent<GrabbableObject>();
                            if (grabbable != null)
                            {
                                grabbable.itemProperties = BrokenRevolverItemRef;
                                grabbable.customGrabTooltip = "Pick up Broken Revolver : [E]";
                            }
                        }
                        CustomPatchesPlugin.Log.LogInfo("[ItemRegistry] Created Broken Revolver scrap item (value $31 - $91).");
                    }
                }

                if (BrokenRevolverItemRef != null)
                {
                    for (int i = 0; i < scrapList.Count; i++)
                    {
                        var s = scrapList[i];
                        if (s != null && s.spawnableItem != null && s.spawnableItem.itemName.Equals("Revolver", StringComparison.OrdinalIgnoreCase))
                        {
                            int originalRarity = s.rarity > 0 ? s.rarity : 70;
                            scrapList[i] = new SpawnableItemWithRarity(BrokenRevolverItemRef, originalRarity);
                            CustomPatchesPlugin.Log.LogInfo($"[ItemRegistry] Replaced standard Revolver scrap with Broken Revolver on {__instance.currentLevel.PlanetName} (rarity: {originalRarity}).");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomPatchesPlugin.Log.LogWarning($"[ItemRegistry] Error injecting ammo scrap rarities: {ex}");
            }
        }

        // ---------------------------------------------------------
        // FIX LỖI GIÁ 2880: CHẶN REGEX \d+ NHẬN SỐ "16" TRONG "M16" LÀ SỐ LƯỢNG MUA
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
                string sanitized = Regex.Replace(lower, @"\bm16a4\b|\bm16a\b|\bm16\b|\brifle\b|\bbuy\b", "", RegexOptions.IgnoreCase).Trim();
                Match match = Regex.Match(sanitized, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int count) && count > 0)
                {
                    __instance.playerDefinedAmount = Mathf.Clamp(count, 1, 16);
                }
                else
                {
                    __instance.playerDefinedAmount = 1;
                }

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
