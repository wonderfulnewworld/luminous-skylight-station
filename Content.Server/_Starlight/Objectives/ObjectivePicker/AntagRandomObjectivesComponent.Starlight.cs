using Content.Shared.Random;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server._Moffstation.Objectives.Components;

public sealed partial class AntagRandomObjectivesComponent
{
    [DataField("forcedObjectiveGroup")]
    public ProtoId<WeightedRandomPrototype>? SLForcedObjectiveGroup;

    [DataField("traitorForcedObjectives")]
    public bool SLTraitorForcedObjectives;

    [DataField("storyObjectiveGroup")]
    public ProtoId<WeightedRandomPrototype>? SLStoryObjectiveGroup;

    [DataField("excludedObjectives")]
    public HashSet<EntProtoId> SLExcludedObjectives = new();
}
