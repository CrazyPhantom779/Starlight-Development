using System.Linq;
using Content.Server._Starlight.Wizard.Casting;
using Content.Server.Administration;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Wizard.SpellGraph;

/// <summary>Lists every glyph, so admins know what to pass to spellcraft_make.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpellcraftGlyphsCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "spellcraft_glyphs";
    public override string Description => "Lists every spell glyph with its category and cost.";
    public override string Help => "spellcraft_glyphs";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        foreach (var glyph in _proto.EnumeratePrototypes<SpellGlyphPrototype>().OrderBy(g => g.Category).ThenBy(g => g.ID))
        {
            var detail = glyph.Category switch
            {
                GlyphCategory.Form => glyph.Delivery.ToString(),
                GlyphCategory.Effect => glyph.Effects.Count > 0 ? "universal" : (glyph.WorldEvent != null ? "aimed only" : "self/burst only"),
                _ => $"{glyph.Augment} {glyph.Value}",
            };
            shell.WriteLine($"{glyph.Category,-8} {glyph.ID,-24} cost {glyph.Cost,5}  {detail}");
        }
    }
}

/// <summary>
/// Debug: weave a spell onto yourself from an ordered glyph chain. Gives you Spellcraft (unrestricted) and Wind
/// if you lack them. Example: spellcraft_make FormPoint GlyphFirebolt AugmentEcho
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpellcraftMakeCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpellGraphSystem _spells = default!;

    public override string Command => "spellcraft_make";
    public override string Description => "Debug: weaves a spell on yourself from a chain of glyphs.";
    public override string Help => "spellcraft_make <glyph> [glyph...]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("You need an attached entity.");
            return;
        }

        if (args.Length == 0)
        {
            shell.WriteError("Usage: spellcraft_make <glyph> [glyph...]  (see spellcraft_glyphs)");
            return;
        }

        var craft = EntityManager.EnsureComponent<SpellcraftComponent>(player);
        craft.Unrestricted = true;
        EntityManager.Dirty(player, craft);
        EntityManager.EnsureComponent<WindComponent>(player);

        var chain = args.Select(a => new ProtoId<SpellGlyphPrototype>(a)).ToList();
        if (!SpellGraphCompiler.TryBuildChain(_proto, chain, out var graph, out var error)
            || !_spells.TryCreateSpell(player, graph, out var action, out error))
        {
            shell.WriteError(error ?? "Failed.");
            return;
        }

        shell.WriteLine($"Created {EntityManager.ToPrettyString(action.Value)}.");
    }
}

/// <summary>Debug: finishes your first errand, to test rewards and mastery.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpellcraftErrandCommand : LocalizedEntityCommands
{
    [Dependency] private ErrandSystem _errands = default!;

    public override string Command => "spellcraft_errand_done";
    public override string Description => "Debug: finishes your first errand.";
    public override string Help => "spellcraft_errand_done";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("You need an attached entity.");
            return;
        }

        _errands.EnsureErrands(player);
        shell.WriteLine(_errands.CompleteFirst(player) ? "Done." : "You have no errands.");
    }
}

/// <summary>Debug: sets one school's tide for yourself, to test costs.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpellcraftTideCommand : LocalizedEntityCommands
{
    [Dependency] private SpellGraphSystem _spells = default!;

    public override string Command => "spellcraft_tide";
    public override string Description => "Debug: sets one school's tide for yourself.";
    public override string Help => "spellcraft_tide <school> <multiplier>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player
            || !EntityManager.TryGetComponent<SpellcraftComponent>(player, out var craft))
        {
            shell.WriteError("You need an attached entity with Spellcraft (open a tome first).");
            return;
        }

        if (args.Length != 2 || !float.TryParse(args[1], out var value))
        {
            shell.WriteError("Usage: spellcraft_tide <school> <multiplier>");
            return;
        }

        craft.Tides[args[0]] = Math.Clamp(value, 0.1f, 3f);
        EntityManager.Dirty(player, craft);
        _spells.RefreshActions(player);
        shell.WriteLine($"Set {args[0]} to {craft.Tides[args[0]]}.");
    }
}
