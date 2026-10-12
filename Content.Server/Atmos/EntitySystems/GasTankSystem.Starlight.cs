using Content.Server.Explosion.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Organ;
using Content.Shared.Cargo;
using Content.Shared.CCVar;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class GasTankSystem : SharedGasTankSystem
{
    private void OnGasTankStartup(Entity<GasTankComponent> ent, ref ComponentStartup args) => UpdateTankPressure(ent);

    private void UpdateTankPressure(Entity<GasTankComponent> ent)
    {
        var pressure = ent.Comp.Air?.Pressure ?? 0f;
        if (ent.Comp.TankPressure == pressure)
            return;

        ent.Comp.TankPressure = pressure;
        DirtyField(ent.Owner, ent.Comp, nameof(GasTankComponent.TankPressure));
    }
}
