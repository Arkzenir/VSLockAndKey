using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VSLockAndKey;

/// <summary>
/// Wires ItemKeyring into the vanilla held-bag system (the same mechanism vanilla
/// quivers/pouches use): put a keyring anywhere in your own inventory and its slots
/// show up as extra inventory slots automatically, no custom GUI/networking needed.
/// </summary>
public class CollectibleBehaviorKeyring : CollectibleBehavior, IHeldBag
{
    public CollectibleBehaviorKeyring(CollectibleObject collObj) : base(collObj)
    {
    }

    public bool IsEmpty(ItemStack bagstack)
    {
        var contents = ItemKeyring.GetContents(bagstack);
        foreach (var stack in contents)
        {
            if (stack != null) return false;
        }
        return true;
    }

    public int GetQuantitySlots(ItemStack bagstack)
    {
        return bagstack.Collectible.Attributes?["keyringSlots"].AsInt(4) ?? 4;
    }

    public ItemStack[] GetContents(ItemStack bagstack, IWorldAccessor world)
    {
        return ItemKeyring.GetContents(bagstack, world)!;
    }

    public List<ItemSlotBagContent> GetOrCreateSlots(ItemStack bagstack, InventoryBase parentinv, int bagIndex, IWorldAccessor world)
    {
        var contents = ItemKeyring.GetContents(bagstack, world);
        List<ItemSlotBagContent> slots = new();

        for (int i = 0; i < contents.Length; i++)
        {
            ItemSlotKeyring slot = new(parentinv, bagIndex, i, GetStorageFlags(bagstack))
            {
                Itemstack = contents[i]
            };
            slots.Add(slot);
        }

        return slots;
    }

    public void Store(ItemStack bagstack, ItemSlotBagContent slot)
    {
        var contents = ItemKeyring.GetContents(bagstack);
        if (slot.SlotIndex < contents.Length)
        {
            contents[slot.SlotIndex] = slot.Itemstack;
        }
        ItemKeyring.SetContents(bagstack, contents);
    }

    public void Clear(ItemStack bagstack)
    {
        bagstack.Attributes.RemoveAttribute(ItemKeyring.ContentsAttr);
    }

    public string GetSlotBgColor(ItemStack bagstack)
    {
        return null;
    }

    public EnumItemStorageFlags GetStorageFlags(ItemStack bagstack)
    {
        return EnumItemStorageFlags.General;
    }

    public TagSet GetStorageTags(ItemStack bagStack)
    {
        return TagSet.Empty;
    }
}

/// <summary>
/// Only accepts ItemKey stacks. Restriction is done here rather than via the tag
/// system, since that requires pre-registering a global tag id we have no need for
/// otherwise.
/// </summary>
public class ItemSlotKeyring : ItemSlotBagContent
{
    public ItemSlotKeyring(InventoryBase inventory, int bagIndex, int slotIndex, EnumItemStorageFlags storageType)
        : base(inventory, bagIndex, slotIndex, storageType)
    {
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        return sourceSlot?.Itemstack?.Collectible is ItemKey && base.CanHold(sourceSlot);
    }
}
