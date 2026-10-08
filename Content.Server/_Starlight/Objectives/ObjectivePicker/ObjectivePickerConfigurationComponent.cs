using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

[RegisterComponent]
public sealed partial class ObjectivePickerConfigurationComponent : Component
{
    public Dictionary<string, float> Weights = new();
    public Dictionary<string, float> StoryWeights = new();
    public int PreferredOptions;
    public HashSet<NetEntity> DeferredTargets = new();
    public bool Finished;
}
