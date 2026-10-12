using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Examine;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private void ExamineEmpAmmo(EntProtoId projectile, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !ProtoManager.TryIndex(projectile, out var proto))
            return;

        int strength;
        if (proto.TryComp<HitscanEmpEffectComponent>(out var hitscan, Factory))
            strength = hitscan.Emp.Strength;
        else if (proto.TryComp<EmpOnTriggerComponent>(out var trigger, Factory))
            strength = trigger.Strength;
        else
            return;

        args.PushText(Loc.GetString("emp-grenade-strength-description", ("empStrength", strength)), 10);
    }
}
