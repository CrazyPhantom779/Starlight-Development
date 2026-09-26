using Content.Server.EUI;
using Content.Server._Starlight.Holograms.Systems;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Eui;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Asks a scanned person whether they consent to their mind being pulled into a hologram chip -
/// same idea as cloning's accept/deny prompt. Nothing is transferred until they answer.
/// </summary>
public sealed class AcceptHologramScanEui(EntityUid mindId, MindComponent mind, EntityUid scanner, EntityUid chip, HologramBodyScannerSystem scannerSystem) : BaseEui
{
    private readonly EntityUid _mindId = mindId;
    private readonly MindComponent _mind = mind;
    private readonly EntityUid _scanner = scanner;
    private readonly EntityUid _chip = chip;
    private readonly HologramBodyScannerSystem _scannerSystem = scannerSystem;

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AcceptHologramScanChoiceMessage choice || choice.Button == AcceptHologramScanUiButton.Deny)
        {
            _scannerSystem.OnScanConsentDenied(_scanner, _chip);
            Close();
            return;
        }

        _scannerSystem.OnScanConsentAccepted(_mindId, _mind, _scanner, _chip);
        Close();
    }
}
