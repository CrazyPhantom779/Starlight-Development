using Content.Shared._Starlight.Devil.DamnationActions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

[Prototype]
public sealed partial class DamnationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Name of the damnation
    /// </summary>
    [DataField]
    public string Name = "DAMNATION!!!!";

    /// <summary>
    /// Description of the curse
    /// </summary>
    [DataField]
    public string Description = "THY END IS NOW!!!!";

    /// <summary>
    /// Cost of the curse. Negative are punishments, Positive are benefits.
    /// </summary>
    [DataField]
    public int Cost = 0;

    /// <summary>
    /// List of components to add to the player
    /// </summary>
    [DataField]
    public ComponentRegistry Components = [];

    /// <summary>
    /// List of components to remove from the player
    /// </summary>
    [DataField]
    public ComponentRegistry RemovedComponents = [];

    /// <summary>
    /// List of actions to run
    /// </summary>
    [DataField]
    public List<DamnationAction> Actions = [];

    /// <summary>
    /// Should the added components be removed if the damnation is undone?
    /// </summary>
    [DataField]
    public bool ReverseOnRemove = true;
}
