using Content.Client.Eui;
using Content.Shared._Starlight.Holograms;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.Holograms.UI;

[UsedImplicitly]
public sealed class AcceptHologramScanEui : BaseEui
{
    private readonly AcceptHologramScanWindow _window;

    public AcceptHologramScanEui()
    {
        _window = new AcceptHologramScanWindow();

        _window.AcceptButton.OnPressed += _ =>
        {
            SendMessage(new AcceptHologramScanChoiceMessage(AcceptHologramScanUiButton.Accept));
            _window.Close();
        };

        _window.DenyButton.OnPressed += _ =>
        {
            SendMessage(new AcceptHologramScanChoiceMessage(AcceptHologramScanUiButton.Deny));
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new AcceptHologramScanChoiceMessage(AcceptHologramScanUiButton.Deny));
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
        => _window.Close();
}
