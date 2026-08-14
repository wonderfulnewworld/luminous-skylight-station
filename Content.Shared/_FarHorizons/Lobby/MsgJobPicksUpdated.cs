using Content.Shared.Roles;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Lobby;

public sealed class MsgJobPicksUpdated : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> JobPicks = default!; // Starlight, no factions, and like, better names

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        JobPicks = new Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)>(count); // Starlight, no factions

        for (int i = 0; i < count; i++)
        {
            var protoId = buffer.ReadString();
            //var high = buffer.ReadVariableInt32(); // Starlight
            //var med = buffer.ReadVariableInt32(); // Starlight
            var low = buffer.ReadVariableInt32();
            #region Starlight
            // cleaning this up a little
            var medium = buffer.ReadVariableInt32();
            var high = buffer.ReadVariableInt32();

            JobPicks.Add(new ProtoId<JobPrototype>(protoId), (low, medium, high));
            #endregion
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(JobPicks.Count);

        foreach (var (protoId, picks) in JobPicks)
        {
            buffer.Write(protoId.Id);
            buffer.WriteVariableInt32(picks.Low); // Starlight
            buffer.WriteVariableInt32(picks.Medium); // Starlight
            buffer.WriteVariableInt32(picks.High); // Starlight
        }
    }
}
