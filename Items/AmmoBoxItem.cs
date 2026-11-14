using System;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Items
{
    public class AmmoBoxItem : GrabbableObject
    {
        public int remainingSpeedloaders = 4;
        private bool _isOpen = false;

        public override void Start()
        {
            base.Start();
            itemProperties.weight = 1.03f; // 3 lb (1.0f + 0.03f)
            itemProperties.creditsWorth = 300;
            customGrabTooltip = "Pick up Revolver Ammo Box : [E]";
        }

        public override void Update()
        {
            base.Update();

            if (isHeld && playerHeldBy != null)
            {
                // Sau khi người chơi nhặt box: thao tác tương tác chuyển thành "Open [E]"
                customGrabTooltip = remainingSpeedloaders > 0 ? 
                    $"Open ({remainingSpeedloaders} Speedloaders left) : [E]" : 
                    "Empty Box";
            }
            else
            {
                customGrabTooltip = "Pick up Revolver Ammo Box : [E]";
            }
        }

        public override void ItemInteractLeftRight(bool right)
        {
            base.ItemInteractLeftRight(right);

            // Khi nhấn E / ItemInteract khi đang cầm hộp
            if (isHeld && playerHeldBy != null && playerHeldBy == StartOfRound.Instance.localPlayerController)
            {
                OpenBoxAndTakeSpeedloader();
            }
        }

        public void OpenBoxAndTakeSpeedloader()
        {
            if (remainingSpeedloaders <= 0)
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.DisplayTip("Ammo Box", "The ammo box is completely empty!", true, false, "LC_Tip1");
                }
                return;
            }

            remainingSpeedloaders--;
            CustomPatchesPlugin.Log.LogInfo($"Ammo box opened! Remaining Speedloaders: {remainingSpeedloaders}");

            // Spawn 1 Speedloader cho người chơi hoặc đặt trước mặt
            if (ItemRegistryPatches.SpeedloaderItemRef != null && playerHeldBy != null)
            {
                Vector3 spawnPos = playerHeldBy.transform.position + playerHeldBy.transform.forward * 0.7f + Vector3.up * 0.3f;
                GameObject spawned = Instantiate(ItemRegistryPatches.SpeedloaderItemRef.spawnPrefab, spawnPos, Quaternion.identity);
                GrabbableObject grabbable = spawned.GetComponent<GrabbableObject>();
                if (grabbable != null)
                {
                    grabbable.fallTime = 0f;
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    {
                        spawned.GetComponent<NetworkObject>()?.Spawn();
                    }
                }
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.DisplayTip("Speedloader Acquired", $"Took 1 Speedloader from box! ({remainingSpeedloaders} left)", false, false, "LC_Tip1");
            }
        }
    }
}

