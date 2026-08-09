using ProtoBuf;

namespace VSLockAndKey;

[ProtoContract]
public class BindKeyPacket
{
    [ProtoMember(1)]
    public bool IsGroup;

    [ProtoMember(2)]
    public string? PlayerUid;

    [ProtoMember(3)]
    public int GroupId;

    [ProtoMember(4)]
    public string DisplayName = "";
}

/// <summary>
/// Client -> server: "open whatever keyring I'm currently holding". No slot/item
/// reference is sent - the server re-derives it from the sender's own active
/// hotbar slot, the same pattern OnBindKeyPacket uses, so nothing here needs
/// trusting.
/// </summary>
[ProtoContract]
public class OpenKeyringPacket
{
}

/// <summary>
/// Server -> client: the keyring's current contents, serialized the same way
/// vanilla's own BlockEntityContainerOpen does it (a raw TreeAttribute byte blob),
/// so the client can build a matching InventoryGeneric before opening the dialog.
/// </summary>
[ProtoContract]
public class KeyringContentsPacket
{
    [ProtoMember(1)]
    public string DialogTitle = "";

    [ProtoMember(2)]
    public int SlotCount;

    [ProtoMember(3)]
    public byte[] TreeBytes = System.Array.Empty<byte>();
}
