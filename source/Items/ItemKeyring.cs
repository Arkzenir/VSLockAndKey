using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VSLockAndKey;

/// <summary>
/// A held container item. The actual bag behavior lives in CollectibleBehaviorKeyring
/// (registered on this item via JSON "behaviors"); this class only owns the storage
/// format so KeyAccessUtil can read a keyring's contents directly, without needing an
/// open inventory, while scanning a player for a matching key.
/// </summary>
public class ItemKeyring : Item
{
    public const string ContentsAttr = "vlkContents";

    public static ItemStack?[] GetContents(ItemStack keyringStack, IWorldAccessor? world = null)
    {
        int slots = keyringStack.Collectible.Attributes?["keyringSlots"].AsInt(4) ?? 4;
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
