using System;
using GameNetcodeStuff;
using UnityEngine;

namespace SonOfCompany_CustomPatches.Items
{
    public class SpeedloaderItem : GrabbableObject
    {
        public int ammoCount = 6;

        public override void Start()
        {
            base.Start();
            itemProperties.weight = 1.005f; // 0.5 lb (1.0f + 0.005f)
            itemProperties.creditsWorth = 80;
            customGrabTooltip = "Speedloader (6 rounds) : [E]";
        }

        public static bool PlayerHasSpeedloader(PlayerControllerB player)
        {
            if (player == null || player.ItemSlots == null) return false;
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                if (player.ItemSlots[i] is SpeedloaderItem loader && loader.ammoCount > 0)
                    return true;
                if (player.ItemSlots[i] != null && player.ItemSlots[i].itemProperties != null &&
                    player.ItemSlots[i].itemProperties.itemName.ToLower().Contains("speedloader"))
                    return true;
            }
            return false;
        }

        public static bool TryConsumeSpeedloader(PlayerControllerB player)
        {
            if (player == null || player.ItemSlots == null) return false;
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                if (player.ItemSlots[i] is SpeedloaderItem loader && loader.ammoCount > 0)
                {
                    loader.ammoCount = 0;
                    player.DestroyItemInSlotAndSync(i);
                    return true;
                }
                if (player.ItemSlots[i] != null && player.ItemSlots[i].itemProperties != null &&
                    player.ItemSlots[i].itemProperties.itemName.ToLower().Contains("speedloader"))
                {
                    player.DestroyItemInSlotAndSync(i);
                    return true;
                }
            }
            return false;
        }
    }
}

