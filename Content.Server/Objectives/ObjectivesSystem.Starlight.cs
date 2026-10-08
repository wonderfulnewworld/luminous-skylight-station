using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Server._Starlight.Objectives.ObjectivePicker;

namespace Content.Server.Objectives;

public sealed partial class ObjectivesSystem
{
    private float SLAppendDifficultySummary(EntityUid mindId, MindComponent mind,
        System.Text.StringBuilder summary, float legacyRate)
    {
        if (!HasComp<ObjectivePickerConfigurationComponent>(mindId))
            return legacyRate;

        var score = new ObjectiveDifficultyScore();
        foreach (var objective in mind.Objectives)
        {
            if (TryComp<ObjectiveComponent>(objective, out var comp))
                score = score.Add(comp.Difficulty, IsCompleted(objective, (mindId, mind)));
        }
        summary.AppendLine(Loc.GetString("objectives-round-end-difficulty",
            ("selected", score.Selected), ("completed", score.Completed), ("percentage", score.Percentage)));
        return score.Completed;
    }
}
