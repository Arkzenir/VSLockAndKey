using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VSLockAndKey;

public static class KeyAccessUtil
{
    public const string BoundPlayerUidAttr = "vlkBoundPlayerUid";
    public const string BoundGroupIdAttr = "vlkBoundGroupId";
    public const string BoundNameAttr = "vlkBoundName";
    public const string DurabilityAttr = "vlkDurability";

    public static bool IsAuthorized(BlockReinforcement bre, IPlayer player)
    {
        if (bre.PlayerUID != null && bre.PlayerUID == player.PlayerUID) return true;
        if (bre.GroupUid != 0 && player.GetGroup(bre.GroupUid) != null) return true;
        return false;
    }

    public static bool IsOwnerExempt(BlockReinforcement bre, ModConfig config, ICoreAPI api)
    {
        if (bre.PlayerUID != null && config.ExemptPlayerUids.Contains(bre.PlayerUID)) return true;

        if (bre.GroupUid != 0 && config.ExemptGroupNames.Count > 0)
        {
            string groupName = bre.LastGroupname;
            if (groupName != null && config.ExemptGroupNames.Contains(groupName)) return true;
        }

        return false;
    }

    public static bool KeyMatchesLock(ItemStack keyStack, BlockReinforcement bre)
    {
        if (keyStack?.Collectible is not ItemKey) return false;

        string boundPlayerUid = keyStack.Attributes.GetString(BoundPlayerUidAttr, null);
        int boundGroupId = keyStack.Attributes.GetInt(BoundGroupIdAttr, 0);

        if (boundPlayerUid == null && boundGroupId == 0) return false;

        if (bre.PlayerUID != null && boundPlayerUid == bre.PlayerUID) return true;
        if (bre.GroupUid != 0 && boundGroupId == bre.GroupUid) return true;

        return false;
    }

    /// <summary>
    /// Finds the first key slot (loose in inventory, or nested inside a keyring) that matches
    /// the given reinforcement. Keyrings are checked by contents, not as a single slot.
    /// </summary>
    public static ItemSlot FindMatchingKeySlot(IPlayer player, BlockReinforcement bre)
    {
        ItemSlot? found = null;

        player.Entity.WalkInventory(slot =>
        {
            if (slot?.Itemstack == null) return true;

            if (KeyMatchesLock(slot.Itemstack, bre))
            {
                found = slot;
                return false;
            }

            if (slot.Itemstack.Collectible is ItemKeyring)
            {
                var contents = ItemKeyring.GetContents(slot.Itemstack);
                for (int i = 0; i < contents.Length; i++)
                {
                    if (KeyMatchesLock(contents[i], bre))
                    {
                        found = new KeyringContentRefSlot(slot, i, contents[i]);
                        return false;
                    }
                }
            }

            return true;
        });

        return found;
    }

    /// <summary>
    /// Only ever call this server-side (see IsLockedForInteractPatch) - DestroyItem
    /// plays a break sound and spawns particles, which would double-fire if this ran
    /// on both the client's predictive call and the server's authoritative one.
    /// </summary>
    public static void DamageKey(Entity byEntity, ItemSlot keySlot, ModConfig config)
    {
        ItemStack stack = keySlot.Itemstack;
        if (stack == null) return;

        int durability = stack.Attributes.GetInt(DurabilityAttr, config.KeyDurability);
        durability -= 1;

        if (durability <= 0)
        {
            // DestroyItem plays the break sound/particles and calls MarkDirty itself.
            stack.Collectible.DestroyItem(byEntity.World, byEntity, keySlot);
            return;
        }

        stack.Attributes.SetInt(DurabilityAttr, durability);
        keySlot.MarkDirty();
    }
}

/// <summary>
/// A synthetic ItemSlot wrapping a key stack that physically lives inside a keyring's
/// own attribute-tree storage rather than a real inventory slot. Writes go back into
/// the keyring itemstack it came from, so damaging/consuming a key found this way still
/// persists correctly without needing a real InventoryBase slot for it.
/// </summary>
public class KeyringContentRefSlot : ItemSlot
{
    readonly ItemSlot keyringSlot;
    readonly int contentIndex;

    public KeyringContentRefSlot(ItemSlot keyringSlot, int contentIndex, ItemStack keyStack) : base(keyringSlot.Inventory)
    {
        this.keyringSlot = keyringSlot;
        this.contentIndex = contentIndex;
        itemstack = keyStack;
    }

    public override void MarkDirty()
    {
        ItemStack[] contents = ItemKeyring.GetContents(keyringSlot.Itemstack);
        contents[contentIndex] = itemstack;
        ItemKeyring.SetContents(keyringSlot.Itemstack, contents);
        keyringSlot.MarkDirty();
    }

    public override ItemStack TakeOut(int quantity)
    {
        ItemStack taken = itemstack;
        itemstack = null;
        MarkDirty();
        return taken;
    }
}
