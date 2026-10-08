using Content.Server.Antag.Components;
using Robust.Shared.Player;

namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class AntagRandomObjectivesComponent : Component
{
    /// <summary>
    /// Each set of objectives to add.
    /// </summary>
    [DataField(required: true)]
    public List<AntagObjectiveSet> Sets = new();

    /// <summary>
    /// Selection time for objectives, set to 0 to have them be instantly picked randomly
    /// </summary>
    [DataField]
    public TimeSpan SelectionDelay = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The preferred number of options; Starlight expands this to meet the difficulty budget.
    /// </summary>
    [DataField]
    public int MaxOptions = 8;

    [DataField]
    public int MaxChoices = 5;

    [DataField]
    public int MinChoices = 2;

    /// <summary>
    /// Starlight: minimum selected difficulty; the offered pool must provide twice this amount.
    /// </summary>
    [DataField]
    public float MaxDifficulty = 6; // Starlight: a finite default is required for the picker budget.
}
