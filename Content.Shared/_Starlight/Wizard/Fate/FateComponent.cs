using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.Fate;

/// <summary>
/// What the Winds decided about this wizard at Awakening. Its presence also means Fate has already been rolled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FateComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<AspectPrototype>> Aspects = [];

    /// <summary>Overall Instability tier from 0 (steady) upward. Stronger and less predictable as it rises.</summary>
    [DataField, AutoNetworkedField]
    public int Instability;
}
