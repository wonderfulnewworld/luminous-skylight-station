using Robust.Client.Graphics;

namespace Content.Client.UserInterface.Systems.Objectives.Controls;

public sealed partial class ObjectiveConditionsControl
{
    private static StyleBoxFlat SLObjectiveFill(Color fill)
        => new() { BackgroundColor = fill.WithAlpha(0.2f) };
}
