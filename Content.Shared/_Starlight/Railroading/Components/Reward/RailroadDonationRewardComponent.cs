using Content.Shared.Destructible.Thresholds;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadDonationRewardComponent : Component
{
    [DataField]
    public MinMax Amount;
}
