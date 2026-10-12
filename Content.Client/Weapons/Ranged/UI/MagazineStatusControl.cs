using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.Weapons.Ranged.UI;

/// <summary>
/// Displays magazine ammunition information for <see cref="BallisticAmmoProviderComponent"/>.
/// </summary>
/// <seealso cref="Content.Client.Weapons.Ranged.Systems.GunSystem"/>
public sealed partial class MagazineStatusControl : PollingItemStatusControl<MagazineStatusControl.Data>
{
    // Starlight start, show the existing self-refill timer.
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SharedGunSystem _guns;
    // Starlight end
    private readonly Entity<BallisticAmmoProviderComponent> _parent;
    private readonly RichTextLabel _label;

    public MagazineStatusControl(Entity<BallisticAmmoProviderComponent> parent)
    {
        IoCManager.InjectDependencies(this); // Starlight, read predicted ammunition and refill state from the existing systems.
        _guns = _entityManager.System<SharedGunSystem>(); // Starlight

        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
    }

    protected override Data PollData()
    {
        var ammoProvider = _parent.Comp;
        var currentRounds = _guns.GetAmmoCount(_parent.Owner); // Starlight
        var maxRounds = ammoProvider.Capacity;

        // Starlight start, only show a countdown while ammunition can regenerate.
        int? refillSeconds = null;
        if (currentRounds < maxRounds &&
            _entityManager.TryGetComponent<BallisticAmmoSelfRefillerComponent>(_parent.Owner, out var refiller) &&
            refiller.AutoRefill)
        {
            refillSeconds = (int) Math.Ceiling(Math.Max(0, (refiller.NextAutoRefill - _timing.CurTime).TotalSeconds));
        }

        return new Data(currentRounds, maxRounds, refillSeconds);
        // Starlight end
    }

    protected override void Update(in Data data)
    {
        var markup = Loc.GetString("magazine-status-rounds",
            ("current", data.CurrentRounds),
            ("max", data.MaxRounds));

        // Starlight, show the time until the next thing is generated.
        if (data.RefillSeconds is { } seconds)
            markup += "\n" + Loc.GetString("magazine-status-refill", ("seconds", seconds));

        _label.SetMarkup(markup);
    }

    public readonly record struct Data(int CurrentRounds, int MaxRounds, int? RefillSeconds); // Starlight, self-refill countdown.
}
