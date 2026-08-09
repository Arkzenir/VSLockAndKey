using Vintagestory.API.Common;

namespace VSLockAndKey;

/// <summary>
/// One slot of a keyring's InventoryGeneric, restricted to holding keys - used on
/// both sides so the client rejects a bad drop locally instead of flashing it in
/// then having the server revert it. Server-side, backingSlot is the player's real
/// hotbar slot holding the keyring, and every modification is written straight
/// back into that keyring itemstack's own attribute tree (the same storage
/// ItemKeyring.GetContents/SetContents already reads elsewhere, e.g. KeyAccessUtil's
/// inventory scan), so the keyring stays correct even if the dialog session ends
/// without a clean close (disconnect, crash, etc). Client-side, backingSlot is null
/// - there's nothing to persist to, the client copy is just a rendering mirror.
/// </summary>
public class KeyringGridSlot : ItemSlotSurvival
{
    readonly ItemSlot? backingSlot;
    readonly int index;

    public KeyringGridSlot(InventoryGeneric inventory, int index, ItemSlot? backingSlot) : base(inventory)
    {
        this.index = index;
        this.backingSlot = backingSlot;
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        return sourceSlot?.Itemstack?.Collectible is ItemKey && base.CanHold(sourceSlot);
    }

    public override void OnItemSlotModified(ItemStack sinkStack)
    {
        base.OnItemSlotModified(sinkStack);
        Persist();
    }

    void Persist()
    {
        if (backingSlot == null) return;

        // The keyring itself may have been dropped, given away, or destroyed while
        // its dialog was still open - if so, there's nothing left to write back to.
        if (backingSlot.Itemstack?.Collectible is not ItemKeyring) return;

        var contents = ItemKeyring.GetContents(backingSlot.Itemstack);
        if (index < contents.Length)
        {
            contents[index] = Itemstack;
            ItemKeyring.SetContents(backingSlot.Itemstack, contents);
        }

        backingSlot.MarkDirty();
    }
}
