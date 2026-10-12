namespace Content.Shared.IgnitionSource;

public sealed partial class IgnitionSourceComponent
{
    /// <summary>
    /// Whether melee swings expose tiles in their arc to this source's flame.
    /// </summary>
    [DataField]
    public bool IgniteOnMelee;
}
