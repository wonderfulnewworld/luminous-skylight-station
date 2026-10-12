using Content.Shared._Starlight.Medical.Body.Components;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Body.Systems;

public sealed partial class MetabolizerSystem
{
    [Dependency] private SharedBodySystem _body = default!;

    /// <summary>
    /// Checks metabolizers on an entity and its currently attached organs.
    /// </summary>
    public bool BodyHasMetabolizer(EntityUid uid, ProtoId<MetabolizerTypePrototype> type)
    {
        if (TryComp<MetabolizerComponent>(uid, out var metabolizer) &&
            metabolizer.MetabolizerTypes?.Contains(type) == true)
            return true;

        foreach (var (organ, _) in _body.GetBodyOrgans(uid))
        {
            if (TryComp<MetabolizerComponent>(organ, out var organMetabolizer) &&
                organMetabolizer.MetabolizerTypes?.Contains(type) == true)
                return true;
        }

        return false;
    }
}
