using System.Linq;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.AutoMod;

[AnyCommand]
public sealed class AutoModReloadCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _ent = default!;

    public string Command => "automodreload";
    public string Description => "Reload AutoMod rules.";
    public string Help => "automodreload";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _ent.System<AutoModSystem>().ReloadRules();
        shell.WriteLine("AutoMod rules reloaded.");
    }
}

[AnyCommand]
public sealed class AutoModTestCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _ent = default!;

    public string Command => "automodtest";
    public string Description => "Test AutoMod against a message.";
    public string Help => "automodtest <channel> <message>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        var channel = args[0];
        var msg = string.Join(' ', args.Skip(1));
        var result = _ent.System<AutoModSystem>().Test(msg, channel, null, 0);
        shell.WriteLine($"Matched={result.Matched} Rule={result.RuleId} Action={result.ActionSummary} Cancel={result.CancelSpeech} Normalized={result.NormalizedText}");
    }
}
