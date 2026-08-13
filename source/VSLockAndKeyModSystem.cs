using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
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

    // Namespaced so this can't collide with another mod's own key in the same
    // world.Config tree - see PLANNING.md for why this, not a custom packet, is
    // the right way to get a server-authoritative config value to clients.
    const string WorldConfigKey = "vslockandkey:config";

    public static ICoreAPI? Api { get; private set; }
    public static ModConfig? Config { get; private set; }

    Harmony? harmony;
    ICoreServerAPI? sapi;
    ICoreClientAPI? capi;

    /// <summary>
    /// Runs before Start() on both sides. Server: load from disk (writing defaults
    /// if missing) and publish into world.Config, which the engine syncs to every
    /// client as part of the normal join handshake. Client: read that same synced
    /// value instead of maintaining (and trusting) its own local copy - this is
    /// the same pattern examples/Thievery's ConfigManager uses, not something
    /// invented for this mod. Server writes here are guaranteed to complete before
    /// any client, including the local one in singleplayer, reads world.Config,
    /// since a world's server side always finishes initializing before a client
    /// begins actual gameplay against it - true for dedicated MP by construction
    /// (nothing can connect before the server is up) and equally true for the
    /// embedded singleplayer server.
    /// </summary>
    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);

        if (api.Side == EnumAppSide.Server)
        {
            ModConfig config = api.LoadModConfig<ModConfig>(ConfigFileName);
            if (config == null)
            {
                config = new ModConfig();
                api.StoreModConfig(config, ConfigFileName);
                api.Logger.Notification($"[VSLockAndKey] No config found, wrote defaults to {ConfigFileName}.");
            }

            // A hand-edited config that explicitly empties KeyringSlotsByMaterial (as
            // opposed to just omitting it, which deserialization already leaves at its
            // field-initializer default) would otherwise leave every keyring falling
            // back to DefaultKeyringSlots regardless of material - almost certainly not
            // the intent, so treat "present but empty" the same as "absent".
            if (config.KeyringSlotsByMaterial == null || config.KeyringSlotsByMaterial.Count == 0)
            {
                config.KeyringSlotsByMaterial = ModConfig.DefaultKeyringSlotsByMaterial();
                api.Logger.Warning($"[VSLockAndKey] {ConfigFileName}'s KeyringSlotsByMaterial was empty - using built-in defaults.");
            }

            // A slot count becomes the array length ItemKeyring.GetContents allocates -
            // a negative value would throw there. Rather than treating <= 0 as
            // malformed, it's honored as an intentional "disable this keyring type":
            // clamped to exactly 0 (never left negative), which GetContents/
            // GuiDialogKeyring both handle explicitly (an empty dialog instead of a
            // slot grid) rather than crashing on it.
            if (config.DefaultKeyringSlots < 0)
            {
                api.Logger.Warning($"[VSLockAndKey] {ConfigFileName}'s DefaultKeyringSlots ({config.DefaultKeyringSlots}) is negative - clamping to 0 (disabled).");
                config.DefaultKeyringSlots = 0;
            }

            foreach (string material in new List<string>(config.KeyringSlotsByMaterial.Keys))
            {
                if (config.KeyringSlotsByMaterial[material] < 0)
                {
                    api.Logger.Warning($"[VSLockAndKey] {ConfigFileName}'s KeyringSlotsByMaterial[\"{material}\"] ({config.KeyringSlotsByMaterial[material]}) is negative - clamping to 0 (disabled).");
                    config.KeyringSlotsByMaterial[material] = 0;
                }
            }

            // ExemptPlayerUids/ExemptGroupNames are read via .Contains()/.Count in
            // KeyAccessUtil.IsOwnerExempt on every locked-block interaction (the
            // Harmony patch calls it unconditionally) - a config that explicitly sets
            // either to JSON null (same "present but null" case as
            // KeyringSlotsByMaterial above, just previously unguarded here) would
            // otherwise NullReferenceException on the very next lock check.
            if (config.ExemptPlayerUids == null)
            {
                api.Logger.Warning($"[VSLockAndKey] {ConfigFileName}'s ExemptPlayerUids was null - using an empty list.");
                config.ExemptPlayerUids = new List<string>();
            }

            if (config.ExemptGroupNames == null)
            {
                api.Logger.Warning($"[VSLockAndKey] {ConfigFileName}'s ExemptGroupNames was null - using an empty list.");
                config.ExemptGroupNames = new List<string>();
            }

            // AdminBypassPrivilege is passed straight to IPlayer.HasPrivilege, whose
            // null-handling isn't visible from the API surface (closed-source
            // implementation) - safer to not find out the hard way.
            if (string.IsNullOrEmpty(config.AdminBypassPrivilege))
            {
                api.Logger.Warning($"[VSLockAndKey] {ConfigFileName}'s AdminBypassPrivilege was empty - using \"commandplayer\".");
                config.AdminBypassPrivilege = "commandplayer";
            }

            Config = config;

            // world.Config's underlying StringAttribute does not escape correctly when
            // the engine converts it to a JToken (e.g. when the main menu's "Modify
            // World" screen re-parses the whole world config as one JSON document) -
            // storing our own raw JSON directly under this key corrupts that outer
            // parse. Base64-encoding sidesteps it entirely; same fix examples/Thievery's
            // ConfigManager uses for the identical problem.
            string serialized = JsonConvert.SerializeObject(config);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(serialized));
            api.World.Config.SetString(WorldConfigKey, encoded);
        }
        else
        {
            string encoded = api.World.Config.GetString(WorldConfigKey);
            if (string.IsNullOrEmpty(encoded))
            {
                Config = new ModConfig();
            }
            else
            {
                try
                {
                    string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    Config = JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
                }
                catch (Exception ex)
                {
                    // The server always writes a fresh, valid value before any client can
                    // read it, so this should never actually trigger - but an uncaught
                    // exception here would crash the client mid-join, so treat a decode
                    // failure the same as "nothing synced yet" instead of taking the
                    // client down over what only ever gates cosmetic tooltip text.
                    api.Logger.Warning($"[VSLockAndKey] Failed to decode synced config, using defaults: {ex.Message}");
                    Config = new ModConfig();
                }
            }
        }
    }

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

    void OnBindKeyPacket(IServerPlayer fromPlayer, BindKeyPacket packet)
    {
        ItemSlot keySlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
        ItemSlot fileSlot = fromPlayer.Entity.LeftHandItemSlot;

        if (keySlot?.Itemstack?.Collectible is not ItemKey) return;
        if (fileSlot?.Itemstack?.Collectible is not ItemKeyFile) return;

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

        // Files aren't consumed by filing, but they do wear down like any other tool
        // (durabilitybytype already declares their max durability - this is the only
        // place that ever spends it). DamageItem breaks the file automatically once
        // it hits zero (with the normal break sound/particles) and calls MarkDirty
        // itself.
        fileSlot.Itemstack.Collectible.DamageItem(fromPlayer.Entity.World, fromPlayer.Entity, fileSlot);
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
