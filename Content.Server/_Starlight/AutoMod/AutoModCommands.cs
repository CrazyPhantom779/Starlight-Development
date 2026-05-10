using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.AutoMod;

/// <summary>
/// Opens the AutoMod editor/dashboard UI.
/// All rule management is intentionally performed through this UI, not through helper commands.
/// </summary>
[AnyCommand]
public sealed class AutoModCommand : IConsoleCommand
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public string Command => "automod";
    public string Description => "Open the AutoMod admin UI.";
    public string Help => "automod";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command must be run by an in-game admin.");
            return;
        }

        _euiManager.OpenEui(new AutoModEui(), player);
    }
}
