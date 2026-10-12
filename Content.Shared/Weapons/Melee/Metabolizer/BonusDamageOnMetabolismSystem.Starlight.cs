using System.Linq;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Weapons.Melee.Metabolizer;

public sealed partial class BonusDamageOnMetabolismSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, AttackedEvent>(OnAttacked);
    }

    // Evaluate each target separately so a matching metabolism does not poison every target of a wide swing.
    private void OnAttacked(Entity<DamageableComponent> target, ref AttackedEvent args)
    {
        if (!TryComp<BonusDamageOnMetabolismComponent>(args.Used, out var bonus) ||
            bonus.SelectedMetabolizer is not { } selectedMetabolizer)
            return;

        if (bonus.ValidMobStates != null &&
            TryComp<MobStateComponent>(target, out var mobState) &&
            !bonus.ValidMobStates.Contains(mobState.CurrentState))
            return;

        if (!_metabolizer.BodyHasMetabolizer(target.Owner, selectedMetabolizer))
            return;

        args.BonusDamage += bonus.Damage;
    }
}
