namespace Content.Shared._FarHorizons.Silicons.HumanoidEMP;

public sealed partial class OrganEmpTargetComponent
{
    /// <summary>
    /// Far Horizons transfers 40% of arm/leg damage and 20% of hand/foot damage to body health.
    /// Preserve that balance without importing its separate limb injury pools, since we don't
    /// have NuBody.
    /// </summary>
    [DataField]
    public float BodyDamageFactor = 1f;
}
