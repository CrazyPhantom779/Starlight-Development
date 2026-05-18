using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Grants hologram-specific actions while the entity has a mind.
/// Brain chips may optionally receive the console action while installed in a powered rack-backed blade.
/// Projected holograms may receive both law and console actions.
/// </summary>
[RegisterComponent]
public sealed partial class HologramActionGrantComponent : Component
{
    [DataField]
    public EntProtoId ConsoleAction = "ActionOpenHologramConsole";

    [DataField]
    public EntProtoId LawsAction = "ActionViewLaws";

    [DataField]
    public bool GrantConsoleAction = true;

    [DataField]
    public bool GrantLawsAction = true;

    /// <summary>
    /// Allows a brain chip inside a powered rack-backed blade to open the hologram console.
    /// This should stay false for normal carried/loose items.
    /// </summary>
    [DataField]
    public bool GrantConsoleActionWhileContainedInPoweredRack;

    [ViewVariables]
    public EntityUid? ConsoleActionEntity;

    [ViewVariables]
    public EntityUid? LawsActionEntity;
}
