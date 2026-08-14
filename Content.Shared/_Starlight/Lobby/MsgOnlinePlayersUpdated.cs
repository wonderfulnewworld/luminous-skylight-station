using Content.Shared._NullLink;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Lobby;

public readonly record struct OnlinePlayerInfo(
    string Name,
    PlayerTitleCategory Category,
    string? Title);

public sealed class MsgOnlinePlayersUpdated : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public List<OnlinePlayerInfo> Players = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        Players = new List<OnlinePlayerInfo>(count);

        for (var i = 0; i < count; i++)
        {
            var name = buffer.ReadString();
            var category = (PlayerTitleCategory) buffer.ReadByte();
            var title = buffer.ReadBoolean() ? buffer.ReadString() : null;

            Players.Add(new OnlinePlayerInfo(name, category, title));
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Players.Count);

        foreach (var player in Players)
        {
            buffer.Write(player.Name);
            buffer.Write((byte) player.Category);
            buffer.Write(player.Title != null);

            if (player.Title != null)
                buffer.Write(player.Title);
        }
    }
}
