using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSLockAndKey;

/// <summary>
/// A held container item. Right-clicking it (open air, no block targeted) opens its
/// own inventory dialog - see GuiDialogKeyring and VSLockAndKeyModSystem's
/// OpenKeyringPacket/KeyringContentsPacket handlers for the open flow. This class
/// owns the storage format (an attribute-tree-backed slot array on the itemstack
/// itself), which both that flow and KeyAccessUtil's inventory scan read directly.
/// </summary>
public class ItemKeyring : Item
{
    public const string ContentsAttr = "vlkContents";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (blockSel == null)
        {
            handling = EnumHandHandling.PreventDefault;

            if (byEntity.World.Api.Side == EnumAppSide.Client)
            {
                (byEntity.World.Api as ICoreClientAPI)?.Network
                    .GetChannel(VSLockAndKeyModSystem.NetworkChannelId)
                    .SendPacket(new OpenKeyringPacket());
            }
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public static ItemStack?[] GetContents(ItemStack keyringStack, IWorldAccessor? world = null)
    {
        ModConfig? config = VSLockAndKeyModSystem.Config;
        string material = keyringStack.Collectible.Variant["material"];
        int slots = config != null && config.KeyringSlotsByMaterial.TryGetValue(material, out int configured)
            ? configured
            : config?.DefaultKeyringSlots ?? 4;

        // StartPre already clamps negative config values to 0, but this is called far
        // more often than that guard runs - defend here too rather than assume every
        // caller went through it.
        if (slots < 0) slots = 0;

        ItemStack?[] stacks = new ItemStack?[slots];

        ITreeAttribute? tree = keyringStack.Attributes.GetTreeAttribute(ContentsAttr);
        if (tree == null) return stacks;

        for (int i = 0; i < slots; i++)
        {
            ItemStack? stack = tree.GetItemstack(i.ToString());
            stack?.ResolveBlockOrItem(world ?? VSLockAndKeyModSystem.Api?.World);
            stacks[i] = stack;
        }

        return stacks;
    }

    public static void SetContents(ItemStack keyringStack, ItemStack?[] stacks)
    {
        ITreeAttribute tree = keyringStack.Attributes.GetOrAddTreeAttribute(ContentsAttr);
        for (int i = 0; i < stacks.Length; i++)
        {
            tree.SetItemstack(i.ToString(), stacks[i]);
        }
    }
}
