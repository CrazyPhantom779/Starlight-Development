using Content.Shared._Starlight.Holograms;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Holograms.UI;

public sealed class HologramConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private HologramConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HologramConsoleWindow>();
        _window.OnProjectHologram += OnProjectHologram;
        _window.OnRecallHologram += OnRecallHologram;
        _window.OnEjectBladeServer += OnEjectBladeServer;
        _window.OnToggleCarry += OnToggleCarry;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is HologramConsoleBoundUserInterfaceState hologramState)
            _window?.UpdateState(hologramState);
    }

    private void OnProjectHologram(NetEntity bladeServerUid, NetEntity projectorUid)
        => SendMessage(new HologramConsoleProjectHologramMessage(bladeServerUid, projectorUid));

    private void OnRecallHologram(NetEntity? bladeServerUid)
        => SendMessage(new HologramConsoleRecallMessage(bladeServerUid));

    private void OnEjectBladeServer(NetEntity bladeServerUid)
        => SendMessage(new HologramConsoleEjectBladeServerMessage(bladeServerUid));

    private void OnToggleCarry(bool allowCarry)
        => SendMessage(new HologramConsoleToggleCarryMessage(allowCarry));
}
