using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

[Serializable, NetSerializable]
public sealed class ObjectivePickerMulligan : EntityEventArgs
{
    public NetEntity MindId;
    public NetEntity RetainedObjective;
}

[Serializable, NetSerializable]
public sealed class ObjectivePickerReply : EntityEventArgs
{
    public bool Accepted;
    public bool Finished;
    public string? Message;
}
