using Content.Shared.Atmos;
namespace Content.Server.Cargo.Components;

/// <summary>
/// Takes input gas and stores it for sale
/// </summary>
[RegisterComponent]
public sealed partial class CargoGasPalletComponent : Component, IGasMixtureHolder
{
    [DataField("inlet")]
    public string InletName { get; set; } = "pipe";

    /// <summary>
    /// A gas mixture representing the remote resivoir.
    /// </summary>
    [DataField("gasMixture")]
    public GasMixture Air { get; set; } = new GasMixture();

    /// <summary>
    /// The maximum pressure to which this will accept gasses
    /// </summary>
    [DataField]
    public float MaxPressure { get; set; } = 4500;
}
