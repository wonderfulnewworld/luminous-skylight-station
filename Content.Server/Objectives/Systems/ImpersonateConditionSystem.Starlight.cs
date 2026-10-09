using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Mind;

namespace Content.Server.Objectives.Systems;

public sealed partial class ImpersonateConditionSystem
{
    [Dependency] private KillPersonConditionSystem _slEliminate = default!;

    private float SLGetImpersonationProgress(EntityUid uid, MindComponent mind, ImpersonateConditionComponent comp)
    {
        if (!_target.GetTarget(uid, out var target))
            return 0;
        return Math.Min(GetProgress(mind, comp), _slEliminate.GetEliminationProgress(target.Value));
    }
}
