using System.Linq;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Content.Server._NullLink.Helpers;
using Content.Server.Objectives;
using Content.Shared._Starlight.Achievement;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Achievement;

/// <summary>
/// Evaluates antagonist difficulty achievements at round end.
/// Progress comes from this round's mind objectives.
/// </summary>
public sealed partial class DifficultyAntagAchievementSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private AchievementSystem _achievements = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundEndTextAppendEvent args)
    {
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var uid, out var mind))
        {
            if ((mind.OriginalOwnerUserId ?? mind.UserId) is not { } player ||
                !_players.TryGetSessionById(player, out var session))
                continue;
            foreach (var achievement in GetEligibleAchievements((uid, mind)))
                _achievements.TryUnlockAchievementAsync(session, achievement.ID, mind.CharacterName)
                    .AsTask().FireAndForget();
        }
    }

    /// <summary>
    /// Returns the configured awards earned by this mind in the current round.
    /// </summary>
    public IEnumerable<AchievementPrototype> GetEligibleAchievements(Entity<MindComponent> mind)
    {
        var score = new ObjectiveDifficultyScore();
        foreach (var objective in mind.Comp.Objectives)
        {
            if (TryComp<ObjectiveComponent>(objective, out var comp))
                score = score.Add(comp.Difficulty, _objectives.IsCompleted(objective, mind));
        }
        var roles = mind.Comp.MindRoleContainer.ContainedEntities.Select(uid => Prototype(uid))
            .OfType<EntityPrototype>().SelectMany(role =>
                _prototypes.EnumerateParents<EntityPrototype>(role.ID, includeSelf: true)).Select(role => role.ID).ToHashSet();
        return _prototypes.EnumeratePrototypes<AchievementPrototype>().Where(achievement =>
            achievement.DifficultyAntag is { } requirement && roles.Contains(requirement.Role.Id) &&
            float.IsFinite(requirement.RequiredDifficulty) && requirement.RequiredDifficulty > 0 &&
            score.Completed + ObjectivePickerSelection.Tolerance >= requirement.RequiredDifficulty);
    }
}
