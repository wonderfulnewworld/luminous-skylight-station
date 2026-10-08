using Content.Shared._Starlight.Objectives.Targeting;
using Robust.Shared.Random;

namespace Content.Shared.Mind;

public abstract partial class SharedMindSystem
{
    private Entity<MindComponent> SLPickHighValueTarget(HashSet<Entity<MindComponent>> candidates)
    {
        var weighted = new List<Entity<MindComponent>>();
        foreach (var candidate in candidates)
        {
            for (var i = 0; i < HighValueTargetsPool.Weight(candidate, EntityManager); i++)
                weighted.Add(candidate);
        }
        return _random.Pick(weighted);
    }
}
