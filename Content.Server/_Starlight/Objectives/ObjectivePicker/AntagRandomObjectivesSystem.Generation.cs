using System.Linq;
using Content.Server.Antag;
using Content.Server.Objectives.Components;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives;
using Content.Shared.Random.Helpers;
using Content.Shared.Random;
using Content.Shared.Whitelist;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    [Dependency] private EntityWhitelistSystem _slWhitelist = default!;
    [Dependency] private IPrototypeManager _slPrototypes = default!;

    private static readonly EntProtoId SLDieObjective = "DieObjective";
    private static readonly EntProtoId SLSurviveObjective = "EscapeShuttleObjective";
    private static readonly EntProtoId SLAchieveObjective = "TraitorAchieveObjectivesObjective";
    private TimeSpan _slNextAvailability;

    private void SLInitializePicker()
    {
        SubscribeNetworkEvent<ObjectivePickerMulligan>(SLMulligan);
        SubscribeLocalEvent<PotentialObjectivesComponent, ComponentShutdown>(SLPickerShutdown);
    }

    private void SLCreatePicker(Entity<AntagRandomObjectivesComponent> rule, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null || !_mind.TryGetMind(args.Session, out var mindId, out var mind))
            return;

        // Repeated selection events must not grant another forced objective or mulligan.
        if (HasComp<PotentialObjectivesComponent>(mindId))
            return;

        if (!float.IsFinite(rule.Comp.MaxDifficulty) || rule.Comp.MaxDifficulty <= 0)
        {
            Log.Error($"Objective picker on {ToPrettyString(rule)} requires a finite, positive maxDifficulty.");
            return;
        }

        var config = EnsureComp<ObjectivePickerConfigurationComponent>(mindId);
        config.PreferredOptions = Math.Max(1, rule.Comp.MaxOptions);
        foreach (var set in rule.Comp.Sets)
        {
            if (_random.Prob(set.Prob))
                SLFlattenWeights(set.Groups, config.Weights);
        }

        var counts = SLObjectiveCounts(mindId);
        EntityUid? forcedTraitor = null;
        if (rule.Comp.SLTraitorForcedObjectives)
            forcedTraitor = SLForceTraitorObjective(mindId, mind, config.Weights, counts);

        if (rule.Comp.SLForcedObjectiveGroup is { } forcedGroup)
        {
            var forced = new Dictionary<string, float>();
            SLFlattenWeights(forcedGroup, forced);
            SLForceObjective(mindId, mind, forced, counts);
            foreach (var prototype in forced.Keys)
                config.Weights.Remove(prototype);
        }

        if (rule.Comp.SLStoryObjectiveGroup is { } storyGroup)
        {
            SLFlattenWeights(storyGroup, config.StoryWeights);
            foreach (var prototype in config.StoryWeights.Keys)
                config.Weights.Remove(prototype);
        }

        foreach (var prototype in rule.Comp.SLExcludedObjectives)
            config.Weights.Remove(prototype);

        var offers = EnsureComp<PotentialObjectivesComponent>(mindId);
        offers.MinimumDifficulty = rule.Comp.MaxDifficulty;
        offers.AutoSelectionDelay = rule.Comp.SelectionDelay;
        offers.AutoSelectionTime = _timing.CurTime + rule.Comp.SelectionDelay;
        var viable = SLFillOffers(mindId, mind, offers, config, counts);
        if (!viable && forcedTraitor is { } dagd && Prototype(dagd)?.ID == SLDieObjective.Id)
        {
            // DAGD is ineligible when the compatible combat pool cannot supply the required budget.
            // Preserve its configured rarity whenever eligible; otherwise choose a viable forced goal.
            mind.Objectives.Remove(dagd);
            Del(dagd);
            foreach (var id in offers.ObjectiveOptions.Keys)
                Del(GetEntity(id));
            offers.ObjectiveOptions.Clear();
            offers.Difficulties.Clear();
            config.DeferredTargets.Clear();
            SLForceObjective(mindId, mind, new Dictionary<string, float>
            {
                [SLSurviveObjective] = 1,
                [SLAchieveObjective] = 1,
            }, counts);
            viable = SLFillOffers(mindId, mind, offers, config, counts);
        }
        if (!viable)
            Log.Error($"No viable objective pool for {ToPrettyString(mindId)} at difficulty {offers.MinimumDifficulty}; awaiting eligible offers.");
        Dirty(mindId, offers);
        Dirty(mindId, mind);
    }

    /// <summary>
    /// Preserve each nested category's probability rather than giving large categories extra weight.
    /// </summary>
    private void SLFlattenWeights(string groupId, Dictionary<string, float> result,
        float weight = 1, HashSet<string>? path = null)
    {
        path ??= new HashSet<string>();
        if (!path.Add(groupId))
        {
            Log.Error($"Cyclic objective group: {groupId}");
            return;
        }

        if (_slPrototypes.TryIndex<WeightedRandomPrototype>(groupId, out var group))
        {
            var total = group.Weights.Values.Where(value => value > 0).Sum();
            if (total > 0)
            {
                foreach (var (id, value) in group.Weights)
                {
                    if (value > 0)
                        SLFlattenWeights(id, result, weight * value / total, path);
                }
            }
        }
        else if (_slPrototypes.HasIndex<EntityPrototype>(groupId))
        {
            result[groupId] = result.GetValueOrDefault(groupId) + weight;
        }

        path.Remove(groupId);
    }

    private EntityUid? SLForceTraitorObjective(EntityUid mindId, MindComponent mind,
        Dictionary<string, float> originalWeights, Dictionary<string, int> counts)
    {
        // Use the existing nested pool's DAGD probability, including sub-traitors' zero chance.
        var total = originalWeights.Values.Sum();
        var dieChance = total > 0 ? originalWeights.GetValueOrDefault(SLDieObjective) / total : 0;
        var forced = new Dictionary<string, float>
        {
            [SLSurviveObjective] = (1 - dieChance) / 2,
            [SLAchieveObjective] = (1 - dieChance) / 2,
        };
        if (dieChance > 0)
            forced[SLDieObjective] = dieChance;
        return SLForceObjective(mindId, mind, forced, counts);
    }

    private EntityUid? SLForceObjective(EntityUid mindId, MindComponent mind, Dictionary<string, float> weights,
        Dictionary<string, int> counts)
    {
        var candidates = new Dictionary<string, float>(weights);
        while (_random.TryPickAndTake(candidates, out var prototype))
        {
            if (!SLCreateOffer(mindId, mind, prototype, counts, out var objective, out var deferred))
                continue;

            if (deferred)
            {
                var assigned = new ObjectiveAssignedEvent(mindId, mind);
                RaiseLocalEvent(objective, ref assigned);
                if (assigned.Cancelled)
                {
                    Del(objective);
                    continue;
                }
                var after = new ObjectiveAfterAssignEvent(mindId, mind, Comp<ObjectiveComponent>(objective), MetaData(objective));
                RaiseLocalEvent(objective, ref after);
            }

            _mind.AddObjective(mindId, mind, objective);
            return objective;
        }
        Log.Error($"No eligible forced objective for {ToPrettyString(mindId)}.");
        return null;
    }

    /// <summary>
    /// Preferred option count is a starting point. Grow the pool until it has twice the budget and a
    /// compatible selection meeting the budget. Story goals are added separately and cost zero.
    /// </summary>
    private bool SLFillOffers(EntityUid mindId, MindComponent mind, PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, Dictionary<string, int> counts, NetEntity? retained = null)
    {
        var candidates = new Dictionary<string, float>(config.Weights);
        var repeatable = new Dictionary<string, float>();
        foreach (var key in offers.ObjectiveOptions.Keys)
        {
            if (TryComp<ObjectiveComponent>(GetEntity(key), out var objective) && objective.Unique &&
                Prototype(GetEntity(key)) is { } proto)
                candidates.Remove(proto.ID);
        }

        // Finite attempts also cover small DAGD pools with non-unique kill/lesson goals.
        var smallest = config.Weights.Keys.Select(id => _slPrototypes.Index<EntityPrototype>(id))
            .Select(proto => proto.TryComp<ObjectiveComponent>(out var objective, EntityManager.ComponentFactory)
                ? objective.Difficulty : 0).Where(difficulty => difficulty > 0).DefaultIfEmpty(1).Min();
        var attempts = Math.Max(config.Weights.Count * 4,
            (int) Math.Ceiling(2 * offers.MinimumDifficulty / smallest) + config.PreferredOptions * 8);
        while (attempts-- > 0)
        {
            SLUpdateOfferState(mindId, mind, offers, counts);
            if (SLOffersViable(offers, retained) &&
                offers.Difficulties.Count(pair => pair.Value > 0 && ObjectivePickerSelection.Available(offers, pair.Key))
                >= config.PreferredOptions)
                break;

            if (candidates.Count == 0)
                candidates = new Dictionary<string, float>(repeatable);
            if (!_random.TryPickAndTake(candidates, out var prototype))
                break;

            if (!SLCreateOffer(mindId, mind, prototype, counts, out var uid, out var deferred))
            {
                repeatable.Remove(prototype);
                continue;
            }

            if (!Comp<ObjectiveComponent>(uid).Unique)
            {
                // Different offers must have enough distinct eligible targets, even before any names are assigned.
                if (deferred && TryComp<PickRandomPersonComponent>(uid, out var pick))
                {
                    var targets = new HashSet<Entity<MindComponent>>();
                    var pool = pick.Pool;
                    pool.FindMinds(targets, mindId, EntityManager, _mind);
                    _mind.FilterMinds(targets, pick.Filters, mindId);
                    if (offers.ObjectiveOptions.Keys.Count(id => Prototype(GetEntity(id))?.ID == prototype) >= targets.Count)
                    {
                        Del(uid);
                        repeatable.Remove(prototype);
                        continue;
                    }
                }
                repeatable[prototype] = config.Weights[prototype];
            }

            SLAddOffer(mindId, mind, offers, config, uid, deferred);
        }

        var stories = new Dictionary<string, float>(config.StoryWeights);
        var storyCount = 0;
        foreach (var key in offers.ObjectiveOptions.Keys)
        {
            if (Prototype(GetEntity(key)) is { } proto && config.StoryWeights.ContainsKey(proto.ID))
            {
                storyCount++;
                stories.Remove(proto.ID);
            }
        }

        while (storyCount < 2 && _random.TryPickAndTake(stories, out var prototype))
        {
            if (!SLCreateOffer(mindId, mind, prototype, counts, out var uid, out var deferred))
                continue;
            SLAddOffer(mindId, mind, offers, config, uid, deferred);
            storyCount++;
        }

        SLUpdateOfferState(mindId, mind, offers, counts);
        return SLOffersViable(offers, retained);
    }

    private static bool SLOffersViable(PotentialObjectivesComponent offers, NetEntity? retained = null)
    {
        var available = offers.ObjectiveOptions.Keys.Where(id => ObjectivePickerSelection.Available(offers, id)).ToArray();
        return ObjectivePickerSelection.Difficulty(offers, available) + ObjectivePickerSelection.Tolerance
               >= 2 * offers.MinimumDifficulty &&
               ObjectivePickerSelection.TryComplete(offers, available,
                   retained is { } id ? new[] { id } : Array.Empty<NetEntity>(), out _);
    }

    private bool SLCreateOffer(EntityUid mindId, MindComponent mind, string prototype,
        Dictionary<string, int> counts, out EntityUid uid, out bool deferred)
    {
        uid = default;
        deferred = false;
        if (_objectives.TryCreateObjective(mindId, mind, prototype, assign: false) is not { } created)
            return false;
        uid = created;

        if (!_objectives.CanBeAssigned(uid, mindId, mind) ||
            !SLWithinLimit(uid, counts) || mind.Objectives.Any(other => !SLCompatible(created, other)))
        {
            Del(uid);
            return false;
        }

        deferred = HasComp<TargetObjectiveComponent>(uid) &&
                   (HasComp<KillPersonConditionComponent>(uid) || HasComp<TeachALessonConditionComponent>(uid));
        if (deferred)
        {
            // Check that a target exists without assigning, naming, or marking anyone as a lesson target.
            if (TryComp<PickRandomPersonComponent>(uid, out var pick) &&
                _mind.PickFromPool(pick.Pool, pick.Filters, mindId) == null)
            {
                Del(uid);
                return false;
            }
            return true;
        }

        var assigned = new ObjectiveAssignedEvent(mindId, mind);
        RaiseLocalEvent(uid, ref assigned);
        if (assigned.Cancelled)
        {
            Del(uid);
            return false;
        }
        var after = new ObjectiveAfterAssignEvent(mindId, mind, Comp<ObjectiveComponent>(uid), MetaData(uid));
        RaiseLocalEvent(uid, ref after);
        return true;
    }

    private void SLAddOffer(EntityUid mindId, MindComponent mind, PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, EntityUid uid, bool deferred)
    {
        var objective = Comp<ObjectiveComponent>(uid);
        var netId = GetNetEntity(uid);
        ObjectiveInfo? info;
        if (deferred && objective.Icon is { } icon)
        {
            var lesson = HasComp<TeachALessonConditionComponent>(uid);
            var head = Prototype(uid)?.ID.EndsWith("HeadObjective") == true;
            info = new ObjectiveInfo(Loc.GetString(lesson
                    ? head ? "objective-picker-hidden-lesson-head" : "objective-picker-hidden-lesson"
                    : head ? "objective-picker-hidden-kill-head" : "objective-picker-hidden-kill"),
                Loc.GetString("objective-picker-hidden-target-description"), icon, 0);
        }
        else
        {
            info = _objectives.GetInfo(uid, mindId, mind);
        }

        if (info is not { } value)
        {
            Del(uid);
            return;
        }
        offers.ObjectiveOptions[netId] = value;
        offers.Difficulties[netId] = objective.Difficulty;
        if (deferred)
            config.DeferredTargets.Add(netId);
    }

    private bool SLCompatible(EntityUid first, EntityUid second)
    {
        if (TryComp<ObjectiveBlacklistRequirementComponent>(first, out var a) &&
            _slWhitelist.IsWhitelistPass(a.Blacklist, second) ||
            TryComp<ObjectiveBlacklistRequirementComponent>(second, out var b) &&
            _slWhitelist.IsWhitelistPass(b.Blacklist, first))
            return false;

        return Prototype(first)?.ID != Prototype(second)?.ID ||
               !Comp<ObjectiveComponent>(first).Unique && !Comp<ObjectiveComponent>(second).Unique;
    }

    private Dictionary<string, int> SLObjectiveCounts(EntityUid exceptMind)
    {
        var counts = new Dictionary<string, int>();
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var uid, out var mind))
        {
            if (uid == exceptMind)
                continue;
            foreach (var prototype in mind.Objectives.Where(Exists)
                         .Select(objective => Prototype(objective)?.ID).OfType<string>().Distinct())
                counts[prototype] = counts.GetValueOrDefault(prototype) + 1;
        }
        return counts;
    }

    private bool SLWithinLimit(EntityUid uid, Dictionary<string, int> counts)
    {
        return !TryComp<ObjectiveLimitComponent>(uid, out var limit) ||
               Prototype(uid) is { } prototype && counts.GetValueOrDefault(prototype.ID) < limit.Limit;
    }

    private void SLUpdateOfferState(EntityUid mindId, MindComponent mind, PotentialObjectivesComponent offers,
        Dictionary<string, int> counts)
    {
        var unavailable = new HashSet<NetEntity>();
        var conflicts = offers.ObjectiveOptions.Keys.ToDictionary(id => id, _ => new HashSet<NetEntity>());
        var keys = offers.ObjectiveOptions.Keys.ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            var uid = GetEntity(keys[i]);
            if (!Exists(uid) || !SLWithinLimit(uid, counts) ||
                !_objectives.CanBeAssigned(uid, mindId, mind) || mind.Objectives.Any(other => !SLCompatible(uid, other)))
                unavailable.Add(keys[i]);

            for (var j = 0; j < i; j++)
            {
                if (!Exists(uid) || !Exists(GetEntity(keys[j])) || !SLCompatible(uid, GetEntity(keys[j])))
                {
                    conflicts[keys[i]].Add(keys[j]);
                    conflicts[keys[j]].Add(keys[i]);
                }
            }
        }
        offers.UnavailableObjectives = unavailable;
        offers.Conflicts = conflicts;
    }

    private void SLPickerShutdown(Entity<PotentialObjectivesComponent> ent, ref ComponentShutdown args)
    {
        foreach (var key in ent.Comp.ObjectiveOptions.Keys)
            TryQueueDel(GetEntity(key));
        RemCompDeferred<ObjectivePickerConfigurationComponent>(ent);
    }
}
