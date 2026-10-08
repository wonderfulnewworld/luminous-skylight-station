using System.Linq;
using Content.Shared._Moffstation.Objectives;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Objectives.Systems;

public sealed partial class PotentialObjectivesSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AntagRandomObjectivesSystem _antagObjectives = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PotentialObjectivesComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.AutoSelectionTime || comp.ObjectiveOptions.Count == 0) // Starlight, skip consumed offers awaiting deferred removal.
                continue;

            #region Starlight
            // Timeout must obey the same difficulty and compatibility rules as a player.
            _antagObjectives.SLAutoSelect(uid, comp);
            #endregion
        }
    }
}
