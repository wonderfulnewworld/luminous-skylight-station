using Content.Client.Atmos.UI;
using Content.Client.Items;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;

namespace Content.Client.Atmos.EntitySystems;

public sealed class GasTankSystem : SharedGasTankSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasTankComponent, AfterAutoHandleStateEvent>(OnGasTankState);

        Subs.ItemStatus<GasTankComponent>(entity => new TankPressureStatusControl(entity));
    }

    private void OnGasTankState(Entity<GasTankComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (UI.TryGetOpenUi(ent.Owner, SharedGasTankUiKey.Key, out var bui))
        {
            bui.Update<GasTankBoundUserInterfaceState>();
        }
        // Starlight edit start - Show simplified UI for when the breathing organ is inaccessible
        if (UI.TryGetOpenUi(ent.Owner, SharedGasTankUiKey.OrganKey, out var organBui))
        {
            organBui.Update<GasTankBoundUserInterfaceState>();
        }
        // Starlight edit end
    }

    public override void UpdateUserInterface(Entity<GasTankComponent> ent)
    {
        if (UI.TryGetOpenUi(ent.Owner, SharedGasTankUiKey.Key, out var bui))
        {
            bui.Update<GasTankBoundUserInterfaceState>();
        }
        // Starlight edit start - Show simplified UI for when the breathing organ is inaccessible
        // if (UI.TryGetOpenUi(ent.Owner, SharedGasTankUiKey.OrganKey, out var organBui))
        // {
        //     organBui.Update<GasTankBoundUserInterfaceState>();
        // }
        // Starlight edit end
    }
}
