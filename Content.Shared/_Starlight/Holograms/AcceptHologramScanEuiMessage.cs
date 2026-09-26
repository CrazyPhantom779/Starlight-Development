using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

[Serializable, NetSerializable]
public enum AcceptHologramScanUiButton
{
    Deny,
    Accept,
}

[Serializable, NetSerializable]
public sealed class AcceptHologramScanChoiceMessage(AcceptHologramScanUiButton button) : EuiMessageBase
{
    public readonly AcceptHologramScanUiButton Button = button;
}
