using Robust.Shared.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Shared._Moffstation.Objectives;

public sealed partial class PotentialObjectivesComponent
{
    [ViewVariables, AutoNetworkedField]
    public float MinimumDifficulty;

    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, float> Difficulties = new();

    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, HashSet<NetEntity>> Conflicts = new();

    [ViewVariables, AutoNetworkedField]
    public HashSet<NetEntity> UnavailableObjectives = new();

    [ViewVariables, AutoNetworkedField]
    public bool MulliganUsed;

    [ViewVariables, AutoNetworkedField]
    public NetEntity? RetainedObjective;
}
