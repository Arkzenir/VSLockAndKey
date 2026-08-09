using System.IO;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using VSLockAndKey.Gui;

[assembly: ModInfo("VSLockAndKey", "vslockandkey")]

namespace VSLockAndKey;

public class VSLockAndKeyModSystem : ModSystem
{
    public const string NetworkChannelId = "vslockandkey";
    const string HarmonyId = "vslockandkey.islockedforinteract";
    const string ConfigFileName = "vslockandkey.json";

    public static ICoreAPI? Api { get; private set; }
    public static ModConfig? Config { get; private set; }

    Harmony? harmony;
    ICoreServerAPI? sapi;
    ICoreClientAPI? capi;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        Api = api;

        api.RegisterItemClass("ItemKey", typeof(ItemKey));
        api.RegisterItemClass("ItemKeyFile", typeof(ItemKeyFile));
        api.RegisterItemClass("ItemKeyring", typeof(ItemKeyring));

        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<BindKeyPacket>()
            .RegisterMessageType<OpenKeyringPacket>()
            .RegisterMessageType<KeyringContentsPacket>();

        api.Logger.Debug("[VSLockAndKey] Mod loaded.");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;

        api.Network.GetChannel(NetworkChannelId).SetMessageHandler<KeyringContentsPacket>(OnKeyringContentsPacket);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;

        LoadConfig(api);

        harmony = new Harmony(HarmonyId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        api.Network.GetChannel(NetworkChannelId)
            .SetMessageHandler<BindKeyPacket>(OnBindKeyPacket)
            .SetMessageHandler<OpenKeyringPacket>(OnOpenKeyringPacket);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }

    void LoadConfig(ICoreServerAPI api)
    {
        ModConfig config = api.LoadModConfig<ModConfig>(ConfigFileName);
        if (config == null)
        {
            config = new ModConfig();
            api.StoreModConfig(config, ConfigFileName);
            api.Logger.Notification($"[VSLockAndKey] No config found, wrote defaults to {ConfigFileName}.");
        }

        Config = config;
    }

    void OnBindKeyPacket(IServerPlayer fromPlayer, BindKeyPacket packet)
    {
        ItemSlot keySlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
        ItemSlot fileSlot = fromPlayer.Entity.LeftHandItemSlot;

        if (keySlot?.Itemstack?.Collectible is not ItemKey) return;
        if (fileSlot?.Itemstack?.Collectible is not ItemKeyFile) return;

        string keyMetal = keySlot.Itemstack.Collectible.Variant["metal"];
        string fileMetal = fileSlot.Itemstack.Collectible.Variant["metal"];
        if (!MetalTier.IsAtLeast(fileMetal, keyMetal)) return;

        if (packet.IsGroup)
        {
            if (Config!.GroupFilingRequiresOwnerOrOp)
            {
                PlayerGroupMembership membership = fromPlayer.GetGroup(packet.GroupId);
                if (membership == null || (membership.Level != EnumPlayerGroupMemberShip.Owner && membership.Level != EnumPlayerGroupMemberShip.Op))
                {
                    return;
                }
            }
            else if (fromPlayer.GetGroup(packet.GroupId) == null)
            {
                return;
            }

            keySlot.Itemstack.Attributes.SetInt(KeyAccessUtil.BoundGroupIdAttr, packet.GroupId);
            keySlot.Itemstack.Attributes.RemoveAttribute(KeyAccessUtil.BoundPlayerUidAttr);
        }
        else
        {
            if (packet.PlayerUid != fromPlayer.PlayerUID) return;

            keySlot.Itemstack.Attributes.SetString(KeyAccessUtil.BoundPlayerUidAttr, packet.PlayerUid);
            keySlot.Itemstack.Attributes.RemoveAttribute(KeyAccessUtil.BoundGroupIdAttr);
        }

        keySlot.Itemstack.Attributes.SetString(KeyAccessUtil.BoundNameAttr, packet.DisplayName);
        keySlot.Itemstack.Attributes.SetInt(KeyAccessUtil.DurabilityAttr, Config!.KeyDurability);
        keySlot.MarkDirty();
    }

    /// <summary>
    /// No slot/item is passed in the packet - the keyring being opened is always
    /// whichever item is currently the sender's active hotbar item, since that's
    /// the only way ItemKeyring.OnHeldInteractStart fires in the first place. This
    /// matches OnBindKeyPacket's approach: re-derive from the server's own state
    /// rather than trust anything the client could have gotten stale or spoofed.
    /// </summary>
    void OnOpenKeyringPacket(IServerPlayer fromPlayer, OpenKeyringPacket packet)
    {
        if (sapi == null) return;

        ItemSlot keyringSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
        if (keyringSlot?.Itemstack?.Collectible is not ItemKeyring) return;

        ItemStack?[] contents = ItemKeyring.GetContents(keyringSlot.Itemstack, fromPlayer.Entity.World);
        int slotCount = contents.Length;

        InventoryGeneric inv = new(slotCount, "keyring-" + fromPlayer.PlayerUID, sapi,
            (id, self) => new KeyringGridSlot(self, id, keyringSlot));

        for (int i = 0; i < slotCount; i++)
        {
            inv[i].Itemstack = contents[i];
        }

        fromPlayer.InventoryManager.OpenInventory(inv);

        TreeAttribute tree = new();
        inv.ToTreeAttributes(tree);
        using MemoryStream ms = new();
        using (BinaryWriter writer = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            tree.ToBytes(writer);
        }

        sapi.Network.GetChannel(NetworkChannelId).SendPacket(new KeyringContentsPacket
        {
            DialogTitle = keyringSlot.Itemstack.GetName(),
            SlotCount = slotCount,
            TreeBytes = ms.ToArray()
        }, fromPlayer);
    }

    void OnKeyringContentsPacket(KeyringContentsPacket packet)
    {
        if (capi == null) return;

        InventoryGeneric inv = new(packet.SlotCount, "keyring-" + capi.World.Player.PlayerUID, capi,
            (id, self) => new KeyringGridSlot(self, id, null));

        TreeAttribute tree = new();
        using (MemoryStream ms = new(packet.TreeBytes))
        using (BinaryReader reader = new(ms))
        {
            tree.FromBytes(reader);
        }
        inv.FromTreeAttributes(tree);
        inv.ResolveBlocksOrItems();

        capi.World.Player.InventoryManager.OpenInventory(inv);

        GuiDialogKeyring dlg = new(packet.DialogTitle, inv, capi);
        dlg.TryOpen();
    }
}
