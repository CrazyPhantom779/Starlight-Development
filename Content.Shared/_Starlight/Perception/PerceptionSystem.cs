using System.Diagnostics.CodeAnalysis;
using Content.Shared.Examine;
using Content.Shared.Random.Helpers;
using Content.Shared.Whitelist;

namespace Content.Shared._Starlight.Perception;

/// <summary>
/// Resolves <see cref="PerceptionVariantComponent"/> for a given observer and feeds the result into examine.
/// Other systems can call <see cref="TryGetVariant"/> to ask what a specific viewer perceives.
/// </summary>
public sealed partial class PerceptionSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    /// <summary>
    /// Finds the variant the given viewer perceives for this entity, if any.
    /// </summary>
    public bool TryGetVariant(Entity<PerceptionVariantComponent> ent,
        EntityUid viewer,
        [NotNullWhen(true)] out PerceptionVariant? variant)
    {
        variant = null;

        List<PerceptionVariant>? matches = null;
        foreach (var candidate in ent.Comp.Variants)
        {
            if (!Matches(candidate, viewer))
                continue;

            if (ent.Comp.Mode == PerceptionSelectionMode.First)
            {
                variant = candidate;
                return true;
            }

            matches ??= [];
            matches.Add(candidate);
        }

        if (matches == null || matches.Count == 0)
            return false;

        var hash = SharedRandomExtensions.HashCodeCombine(GetNetEntity(viewer).Id, GetNetEntity(ent).Id, ent.Comp.Salt);
        variant = matches[(int) ((uint) hash % (uint) matches.Count)];
        return true;
    }

    private bool Matches(PerceptionVariant variant, EntityUid viewer)
        => (variant.Whitelist == null
            || _whitelist.IsValid(variant.Whitelist, viewer))
            && (variant.Blacklist == null
            || !_whitelist.IsValid(variant.Blacklist, viewer));

    [SubscribeLocalEvent]
    private void OnGetDescription(Entity<PerceptionVariantComponent> ent, ref GetPerceivedDescriptionEvent args)
    {
        if (!TryGetVariant(ent, args.Examiner, out var variant) || variant.Description == null)
            return;

        args.Description = Loc.TryGetString(variant.Description, out var loc) ? loc : variant.Description;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<PerceptionVariantComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetVariant(ent, args.Examiner, out var variant))
            return;

        foreach (var line in variant.Append)
        {
            var text = Loc.TryGetString(line, out var loc) ? loc : line;
            args.PushMarkup(text, variant.AppendPriority);
        }
    }
}
