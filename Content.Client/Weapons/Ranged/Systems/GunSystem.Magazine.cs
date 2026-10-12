using Content.Client.Items;
using Content.Client.Weapons.Ranged.Components; // Starlight - preserve existing ammo-counter controls.
using Content.Client.Weapons.Ranged.UI;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void InitializeMagazine()
    {
        base.InitializeMagazine();

        Subs.ItemStatus<BallisticAmmoProviderComponent>(entity => HasComp<AmmoCounterComponent>(entity.Owner) ? null : new MagazineStatusControl(entity)); // Starlight
    }

    [SubscribeLocalEvent]
    private void OnMagazineAmmoUpdate(Entity<MagazineAmmoProviderComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        var magEnt = GetMagazineEntity(ent);

        if (magEnt == null)
        {
            if (args.Control is DefaultStatusControl control)
            {
                control.Update(0, 0);
            }

            return;
        }

        RaiseLocalEvent(magEnt.Value, args, false);
    }

    [SubscribeLocalEvent]
    private void OnMagazineControl(Entity<MagazineAmmoProviderComponent> ent, ref AmmoCounterControlEvent args)
    {
        var magEnt = GetMagazineEntity(ent);
        if (magEnt == null)
            return;
        RaiseLocalEvent(magEnt.Value, args, false);
    }
}
