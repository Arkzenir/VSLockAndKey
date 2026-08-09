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
