using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonOfCompany_CustomPatches.Items
{
    public class TacticalBeltItem : GrabbableObject
    {
        public static TacticalBeltItem LocalBelt { get; private set; }

        public int currentCapacity = 3;
        public int maxCapacity = 6;
        public int storedAmmoCount = 0;
        public string storedAmmoType = "Rounds";

        // UI Overlay
        private static GameObject _beltUiRoot;
        private static TextMeshProUGUI _beltText;

        public override void Start()
        {
            base.Start();
            itemProperties.weight = 1.02f; // 2 lb (1.0f + 0.02f)
            itemProperties.creditsWorth = 50;
            currentCapacity = CustomPatchesPlugin.BeltDefaultCapacity?.Value ?? 3;
            maxCapacity = CustomPatchesPlugin.BeltMaxCapacity?.Value ?? 6;
            customGrabTooltip = "Tactical Belt : [E]";

            EnsureUI();
        }

        public override void EquipItem()
        {
            base.EquipItem();
            if (playerHeldBy == StartOfRound.Instance.localPlayerController)
            {
                LocalBelt = this;
                UpdateUI();
            }
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
            if (LocalBelt == this)
            {
                LocalBelt = null;
                HideUI();
            }
        }

        public static bool CanStoreAmmo(string ammoName)
        {
            if (string.IsNullOrWhiteSpace(ammoName)) return false;
            string lower = ammoName.ToLower();

            // Tactical Belt KHÔNG hỗ trợ RPG-7!
            if (lower.Contains("rpg") || lower.Contains("rocket"))
                return false;

            // Hỗ trợ ammo và magazine của PiggyVarietyMod và BetterArmory
            return lower.Contains("ammo") || lower.Contains("round") || lower.Contains("bullet") || 
                   lower.Contains("shell") || lower.Contains("magazine") || lower.Contains("speedloader");
        }

        public bool TryDepositAmmo(string type, int count)
        {
            if (!CanStoreAmmo(type)) return false;

            if (storedAmmoCount >= currentCapacity) return false;

            if (storedAmmoCount == 0 || storedAmmoType == type)
            {
                storedAmmoType = type;
                int canAdd = Mathf.Min(count, currentCapacity - storedAmmoCount);
                storedAmmoCount += canAdd;
                UpdateUI();
                return true;
            }
            return false;
        }

        public bool TryWithdrawAmmo(string requestedType, out int withdrawn)
        {
            withdrawn = 0;
            if (storedAmmoCount > 0)
            {
                withdrawn = 1;
                storedAmmoCount--;
                if (storedAmmoCount == 0)
                {
                    storedAmmoType = "Empty";
                }
                UpdateUI();
                return true;
            }
            return false;
        }

        public void UpgradeBeltCapacity()
        {
            if (currentCapacity < maxCapacity)
            {
                currentCapacity++;
                CustomPatchesPlugin.Log.LogInfo($"Tactical Belt upgraded! New capacity: {currentCapacity}");
                UpdateUI();
            }
        }

        // Tạo UI HUD hiển thị 1 slot belt
        public static void EnsureUI()
        {
            if (_beltUiRoot != null) return;

            HUDManager hud = HUDManager.Instance;
            if (hud == null) return;

            // Tìm Canvas từ HUDAnimator hoặc bất kỳ Canvas nào thuộc UI camera
            Canvas canvas = hud.HUDAnimator != null ? hud.HUDAnimator.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            }
            if (canvas == null) return;

            _beltUiRoot = new GameObject("TacticalBelt_HUD_Slot");
            _beltUiRoot.transform.SetParent(canvas.transform, false);

            RectTransform rect = _beltUiRoot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.85f, 0.15f);
            rect.anchorMax = new Vector2(0.95f, 0.22f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            GameObject textObj = new GameObject("BeltText");
            textObj.transform.SetParent(_beltUiRoot.transform, false);
            _beltText = textObj.AddComponent<TextMeshProUGUI>();
            _beltText.fontSize = 15;
            _beltText.alignment = TextAlignmentOptions.Center;
            _beltText.color = Color.white;
            _beltText.text = "BELT: [Empty]";

            _beltUiRoot.SetActive(false);
        }

        public static void UpdateUI()
        {
            EnsureUI();
            if (_beltUiRoot == null || _beltText == null) return;

            if (LocalBelt != null)
            {
                _beltUiRoot.SetActive(true);
                _beltText.text = $"BELT [{LocalBelt.storedAmmoType}]\n{LocalBelt.storedAmmoCount}/{LocalBelt.currentCapacity}";
            }
            else
            {
                _beltUiRoot.SetActive(false);
            }
        }

        public static void HideUI()
        {
            if (_beltUiRoot != null)
                _beltUiRoot.SetActive(false);
        }
    }
}
