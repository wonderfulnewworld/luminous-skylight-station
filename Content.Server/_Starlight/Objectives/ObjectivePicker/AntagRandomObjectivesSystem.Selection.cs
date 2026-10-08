using System.Linq;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _slNextAvailability)
            return;
        _slNextAvailability = _timing.CurTime + TimeSpan.FromSeconds(1);
        SLRefreshPickers();
        SLUpdateProgression();
    }

    private void SLRefreshPickers()
    {
        var query = EntityQueryEnumerator<PotentialObjectivesComponent, MindComponent, ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out var uid, out var offers, out var mind, out var config))
        {
            if (config.Finished)
                continue;
            var previous = offers.UnavailableObjectives;
            var count = offers.ObjectiveOptions.Count;
            var counts = SLObjectiveCounts(uid);
            SLUpdateOfferState(uid, mind, offers, counts);
            // Keep depleted cards visible and disabled, but supply replacements so a late chooser can finish.
            if (!SLOffersViable(offers))
                SLFillOffers(uid, mind, offers, config, counts);
            if (!previous.SetEquals(offers.UnavailableObjectives) || count != offers.ObjectiveOptions.Count)
                Dirty(uid, offers);
        }
    }

    private void SLSubmitObjectives(ObjectivePickerSelected ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _) || GetNetEntity(mindId) != ev.MindId)
            return;

        var accepted = SLApplySelectedObjectives(mindId, ev.SelectedObjectives);
        RaiseNetworkEvent(new ObjectivePickerReply
        {
            Accepted = accepted,
            Finished = accepted,
            Message = accepted ? null : "objective-picker-selection-rejected",
        }, args.SenderSession);
    }

    private bool SLApplySelectedObjectives(EntityUid mindId, HashSet<NetEntity> selected)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) ||
            !TryComp<PotentialObjectivesComponent>(mindId, out var offers) || offers.ObjectiveOptions.Count == 0)
            return false;

        SLUpdateOfferState(mindId, mind, offers, SLObjectiveCounts(mindId));
        Dirty(mindId, offers);
        if (!ObjectivePickerSelection.Valid(offers, selected))
            return false;

        // Only confirmed kill/lesson offers get targets. Temporary membership lets target filters
        // see earlier selections in this batch; no after-assignment side effects run until all succeed.
        if (!TryComp<ObjectivePickerConfigurationComponent>(mindId, out var config) || config.Finished)
            return false;
        var initialized = new List<NetEntity>();
        var temporary = new List<EntityUid>();
        var success = true;
        foreach (var id in selected.Where(id => !config.DeferredTargets.Contains(id)))
        {
            var uid = GetEntity(id);
            mind.Objectives.Add(uid);
            temporary.Add(uid);
        }
        foreach (var id in selected.Where(config.DeferredTargets.Contains)
                     .OrderByDescending(id => Prototype(GetEntity(id))?.ID.EndsWith("HeadObjective") == true))
        {
            var uid = GetEntity(id);
            initialized.Add(id);
            var assigned = new ObjectiveAssignedEvent(mindId, mind);
            RaiseLocalEvent(uid, ref assigned);
            if (assigned.Cancelled || !TryComp<TargetObjectiveComponent>(uid, out var target) || target.Target == null)
            {
                success = false;
                break;
            }
            mind.Objectives.Add(uid);
            temporary.Add(uid);
        }
        foreach (var uid in temporary)
            mind.Objectives.Remove(uid);

        if (!success)
        {
            // Discard partially initialized targets without disclosing any of them or marking teachers.
            foreach (var id in initialized)
            {
                var uid = GetEntity(id);
                var prototype = Prototype(uid)?.ID;
                SLRemoveOffer(offers, config, id);
                Del(uid);
                if (prototype != null && SLCreateOffer(mindId, mind, prototype, SLObjectiveCounts(mindId),
                        out var replacement, out var deferred))
                    SLAddOffer(mindId, mind, offers, config, replacement, deferred);
            }
            SLFillOffers(mindId, mind, offers, config, SLObjectiveCounts(mindId));
            Dirty(mindId, offers);
            return false;
        }

        foreach (var id in initialized)
        {
            var uid = GetEntity(id);
            var after = new ObjectiveAfterAssignEvent(mindId, mind, Comp<ObjectiveComponent>(uid), MetaData(uid));
            RaiseLocalEvent(uid, ref after);
        }
        foreach (var id in selected)
            _mind.AddObjective(mindId, mind, GetEntity(id));
        foreach (var id in offers.ObjectiveOptions.Keys.Where(id => !selected.Contains(id)))
            TryQueueDel(GetEntity(id));

        // Clear synchronously to make repeat submissions harmless before deferred removal.
        offers.ObjectiveOptions.Clear();
        SLRecordConfirmedBatch(mindId, config, selected);
        config.Finished = true;
        Dirty(mindId, mind);
        RemCompDeferred<PotentialObjectivesComponent>(mindId);
        SLRefreshPickers();
        return true;
    }

    public void SLAutoSelect(EntityUid mindId, PotentialObjectivesComponent offers)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return;
        SLUpdateOfferState(mindId, mind, offers, SLObjectiveCounts(mindId));
        var order = offers.ObjectiveOptions.Keys.OrderBy(_ => _random.Next()).ToArray();
        var retained = offers.RetainedObjective is { } id && ObjectivePickerSelection.Available(offers, id)
            ? new[] { id } : Array.Empty<NetEntity>();
        if (ObjectivePickerSelection.TryComplete(offers, order, retained, out var selected) ||
            ObjectivePickerSelection.TryComplete(offers, order, Array.Empty<NetEntity>(), out selected))
            SLApplySelectedObjectives(mindId, selected);
    }

    private void SLMulligan(ObjectivePickerMulligan ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out var mind) || GetNetEntity(mindId) != ev.MindId)
            return;

        var accepted = SLTryMulligan(mindId, mind, ev.RetainedObjective);
        RaiseNetworkEvent(new ObjectivePickerReply
        {
            Accepted = accepted,
            Finished = false,
            Message = accepted ? "objective-picker-mulligan-success" : "objective-picker-mulligan-rejected",
        }, args.SenderSession);
    }

    private bool SLTryMulligan(EntityUid mindId, MindComponent mind, NetEntity retained)
    {
        if (!TryComp<PotentialObjectivesComponent>(mindId, out var offers) || offers.MulliganUsed ||
            !TryComp<ObjectivePickerConfigurationComponent>(mindId, out var oldConfig))
            return false;

        var counts = SLObjectiveCounts(mindId);
        SLUpdateOfferState(mindId, mind, offers, counts);
        if (!ObjectivePickerSelection.Available(offers, retained))
            return false;

        var rerolled = new PotentialObjectivesComponent
        {
            MinimumDifficulty = offers.MinimumDifficulty,
            ObjectiveOptions = new() { [retained] = offers.ObjectiveOptions[retained] },
            Difficulties = new() { [retained] = offers.Difficulties[retained] },
        };
        var config = new ObjectivePickerConfigurationComponent
        {
            Weights = oldConfig.Weights,
            StoryWeights = oldConfig.StoryWeights,
            PreferredOptions = oldConfig.PreferredOptions,
        };
        if (oldConfig.DeferredTargets.Contains(retained))
            config.DeferredTargets.Add(retained);

        if (!SLFillOffers(mindId, mind, rerolled, config, counts, retained))
        {
            foreach (var id in rerolled.ObjectiveOptions.Keys.Where(id => id != retained))
                TryQueueDel(GetEntity(id));
            return false;
        }

        foreach (var id in offers.ObjectiveOptions.Keys.Where(id => id != retained))
            TryQueueDel(GetEntity(id));
        offers.ObjectiveOptions = rerolled.ObjectiveOptions;
        offers.Difficulties = rerolled.Difficulties;
        offers.Conflicts = rerolled.Conflicts;
        offers.UnavailableObjectives = rerolled.UnavailableObjectives;
        offers.MulliganUsed = true;
        oldConfig.MulliganUsed = true;
        offers.RetainedObjective = retained;
        oldConfig.DeferredTargets = config.DeferredTargets;
        Dirty(mindId, offers);
        return true;
    }

    private static void SLRemoveOffer(PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, NetEntity id)
    {
        offers.ObjectiveOptions.Remove(id);
        offers.Difficulties.Remove(id);
        config.DeferredTargets.Remove(id);
        if (offers.RetainedObjective == id)
            offers.RetainedObjective = null;
    }
}
