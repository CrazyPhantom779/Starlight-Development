using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.AutoMod;

[AnyCommand]
public sealed class AutoModCommand : IConsoleCommand
{
    [Dependency] private readonly EuiManager _eui = default!;

    public string Command => "automod";
    public string Description => "Opens the AutoMod admin UI.";
    public string Help => "automod";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _eui.OpenEui(new AutoModEui(), player);
    }
}
