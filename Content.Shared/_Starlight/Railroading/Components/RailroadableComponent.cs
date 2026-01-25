using Content.Shared._Starlight.Railroading.Components;
using Content.Shared.GameTicking;
using Robust.Shared.GameStates;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;
using Content.Shared.GameTicking;

namespace Content.Shared._Starlight.Railroading.Components;

[RegisterComponent]
public sealed partial class RailroadableComponent : Component
{
    [ViewVariables]
    [NonSerialized]
    public List<Entity<RailroadCardComponent, RuleOwnerComponent>>? IssuedCards;

    [ViewVariables]
    [NonSerialized]
    public Entity<RailroadCardComponent, RuleOwnerComponent>? ActiveCard;

    [ViewVariables]
    [NonSerialized]
    public List<Entity<RailroadCardComponent, RuleOwnerComponent>>? Completed;

    [DataField]
    [NonSerialized]
    public bool Restricted = false;
}
