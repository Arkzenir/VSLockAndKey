using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using VSLockAndKey.Gui;

namespace VSLockAndKey;

public class ItemKey : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        // The decision (is this a file+key combo worth intercepting) is computed the
        // same way on both sides so client and server agree on `handling` - only the
        // dialog itself (a pure UI concern) is client-exclusive. The actual bind still
        // only ever happens server-side, via BindKeyPacket once the player confirms.
        if (blockSel == null)
        {
            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;
            if (offhandSlot?.Itemstack?.Collectible is ItemKeyFile && slot.Itemstack != null)
            {
                string keyMetal = slot.Itemstack.Collectible.Variant["metal"];
                string fileMetal = offhandSlot.Itemstack!.Collectible.Variant["metal"];

                if (MetalTier.IsAtLeast(fileMetal, keyMetal))
                {
                    handling = EnumHandHandling.PreventDefault;
                    if (byEntity.World.Api.Side == EnumAppSide.Client)
                    {
                        GuiDialogKeyFile.OpenFor(byEntity, slot);
                    }
                    return;
                }
            }
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, System.Text.StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        var config = VSLockAndKeyModSystem.Config;

        // Config is populated on both sides via StartPre (server loads from disk,
        // client reads the copy the server publishes into world.Config), so this
        // should always reflect the server's real setting. The ?? true is only a
        // safety net for the edge case of this running before StartPre completes,
        // not the normal path.
        if (config?.ShowKeyBindingInfo ?? true)
        {
            string? boundName = inSlot.Itemstack!.Attributes.GetString(KeyAccessUtil.BoundNameAttr, null);
            if (boundName != null)
            {
                dsc.AppendLine(Lang.Get("vslockandkey:key-boundto", boundName));
            }
            else
            {
                dsc.AppendLine(Lang.Get("vslockandkey:key-unbound"));
            }
        }

        if (config != null && config.LimitUnauthorisedUse)
        {
            int durability = inSlot.Itemstack!.Attributes.GetInt(KeyAccessUtil.DurabilityAttr, config.KeyDurability);
            dsc.AppendLine(Lang.Get("vslockandkey:key-durability", durability, config.KeyDurability));
        }
    }
}
