using System.Linq;
using Content.Server._FarHorizons.Silicons.Glitching;
using Content.Server.Hands.Systems;
using Content.Server.Stunnable;
using Content.Shared._FarHorizons.Silicons.HumanoidEMP;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.Silicons.HumanoidEMP;

public sealed partial class HumanoidEMPSystem : EntitySystem
{
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GlitchingSystem _glitching = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidEMPComponent, EmpPulseEvent>(OnHumanoidEMP);
        InitializeInspect();
    }

    private void OnHumanoidEMP(Entity<HumanoidEMPComponent> ent, ref EmpPulseEvent args)
    {
        if (args.Disabled || args.Strength <= 0 || _timing.CurTime < ent.Comp.NextEffect) // Starlight
            return;

        ent.Comp.NextEffect = _timing.CurTime + ent.Comp.EffectCooldown;

        #region Starlight
        // No nubody so we have to do things differently here
        var damage = ent.Comp.BaseDamage;
        var effect = ResolveThresholds(ent.Comp.Thresholds, args.Strength);
        GatherBodyEffects(ent.Owner, args.Strength, ref effect, ref damage);

        ApplyDamage(ent.Owner, damage, args.Strength);
        ApplyEffect(ent, effect);
        #endregion
    }

    public void ApplyEffect(EntityUid ent, HumanoidEMPEffect effect)
    {
        #region Starlight
        if (effect.KnockdownAmount > TimeSpan.Zero)
            _stunSystem.TryKnockdown(ent, effect.KnockdownAmount, false, true, false, true);
        if (effect.StunAmount > TimeSpan.Zero)
            _stunSystem.TryAddStunDuration(ent, effect.StunAmount);
        #endregion
        foreach (var statusEffect in effect.AdditionalEffects)
            _status.TryAddStatusEffectDuration(ent, statusEffect.Key, out _, statusEffect.Value);

        if (effect.SlowdownAmount > TimeSpan.Zero) // Starlight
            _movementMod.TryAddMovementSpeedModDuration(ent, MovementModStatusSystem.FlashSlowdown, effect.SlowdownAmount, effect.WalkSpeedModifier, effect.SprintSpeedModifier); // Starlight
        foreach (var hand in effect.DropItemsFrom)
            _hands.TryDrop(ent, hand, null, false, false);

        if (effect.GlitchDuration <= TimeSpan.Zero) return;
        var rampTime = effect.GlitchDuration / 4;
        _glitching.ApplyGlitch(ent, effect.GlitchDuration, rampTime);
    }

    public void ApplyDamage(Entity<DamageableComponent?> ent, DamageSpecifier baseDamage, int empStrength)
    {
        var scaledDmg = baseDamage * Math.Sqrt(empStrength);
        _damageable.TryChangeDamage(ent, scaledDmg, true);
    }

    public static HumanoidEMPEffect ResolveThresholds(Dictionary<int, HumanoidEMPEffect> thresholds, int empStrength)
    {
        var res = new HumanoidEMPEffect();
        foreach (var (_, effect) in thresholds.Where(p => p.Key <= empStrength))
            res += effect;
        return res;
    }
}
