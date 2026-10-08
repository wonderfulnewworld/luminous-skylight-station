using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared.Mind.Filters;
using Robust.Shared.Random;

namespace Content.Shared.Mind;

public abstract partial class SharedMindSystem
{
    private Entity<MindComponent> SLPickObjectiveTarget(IMindPool pool, HashSet<Entity<MindComponent>> candidates)
    {
        if (pool is not (HighValueTargetsPool or WeightedAlivePool))
            return _random.Pick(candidates);

        var weighted = new List<Entity<MindComponent>>();
        foreach (var candidate in candidates)
        {
            var weight = pool is WeightedAlivePool normal
                ? normal.Weight(candidate, EntityManager) : HighValueTargetsPool.Weight(candidate, EntityManager);
            for (var i = 0; i < weight; i++)
                weighted.Add(candidate);
        }
        return _random.Pick(weighted);
    }
}
