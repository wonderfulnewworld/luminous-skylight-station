using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._FarHorizons.Silicons.HumanoidEMP;
using Content.Shared.Examine;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Damage;

namespace Content.Server._FarHorizons.Silicons.HumanoidEMP;

public sealed partial class HumanoidEMPSystem
{
    [Dependency] private BodySystem _body = default!;

    public void InitializeInspect() => SubscribeLocalEvent<EmpOnTriggerComponent, ExaminedEvent>(OnEmpGrenadeExamine);

    private void GatherBodyEffects(EntityUid body, int strength, ref HumanoidEMPEffect effect, ref DamageSpecifier damage)
    {
        foreach (var part in _body.GetBodyChildren(body))
            GatherTargetEffects(part.Id, strength, ref effect, ref damage);

        foreach (var organ in _body.GetBodyOrgans(body))
            GatherTargetEffects(organ.Id, strength, ref effect, ref damage);
    }

    private void GatherTargetEffects(EntityUid target, int strength, ref HumanoidEMPEffect effect, ref DamageSpecifier damage)
    {
        if (!TryComp<OrganEmpTargetComponent>(target, out var emp))
            return;

        effect += ResolveThresholds(emp.Thresholds, strength);
        damage += emp.BaseDamage * emp.BodyDamageFactor;
    }

    private void OnEmpGrenadeExamine(Entity<EmpOnTriggerComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushText(Loc.GetString("emp-grenade-strength-description", ("empStrength", ent.Comp.Strength)), 10);
    }

}
