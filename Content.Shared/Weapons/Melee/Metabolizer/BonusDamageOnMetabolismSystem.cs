using System.Linq;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Weapons.Melee.Metabolizer;

public sealed partial class BonusDamageOnMetabolismSystem : EntitySystem
{
    [Dependency] private MetabolizerSystem _metabolizer = default!;

    [SubscribeLocalEvent]
    private void OnGetVerb(Entity<BonusDamageOnMetabolismComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        //var allMetabolizers = ProtoMan.EnumeratePrototypes<MetabolizerTypePrototype>().ToList().OrderBy(x => Loc.GetString(x.LocalizedName)); // Starlight
        var allMetabolizers = ProtoMan.EnumeratePrototypes<MetabolizerTypePrototype>().OrderBy(x => x.LocalizedName); // Starlight

        byte index = 0;
        foreach (var metabolizer in allMetabolizers)
        {
            if (ent.Comp.ExcludedMetabolizers.Contains(metabolizer))
                continue;

            var currIndex = index;
            var verb = new Verb
            {
                Priority = currIndex,
                Category = VerbCategory.Metabolizers,
                Disabled = ent.Comp.SelectedMetabolizer == metabolizer,
                Act = () =>
                {
                    ent.Comp.SelectedMetabolizer = metabolizer;
                    Dirty(ent);
                },
                Text = metabolizer.LocalizedName, // Starlight
            };
            args.Verbs.Add(verb);
            index++;
        }
    }

    /* Starlight, no nubody
    [SubscribeLocalEvent]
    private void OnSwingTrigger(Entity<BonusDamageOnMetabolismComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.SelectedMetabolizer is null)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (TryComp<MobStateComponent>(hitEntity, out var mobState) && !ent.Comp.ValidMobStates.Contains(mobState.CurrentState))
                continue;

            if (!_metabolizer.BodyHasMetabolizer(hitEntity, ent.Comp.SelectedMetabolizer.Value))
                continue;

            // Add the bonus damage and quit!
            args.BonusDamage =+ ent.Comp.Damage;
            return;
        }
    }
    }*/
}
