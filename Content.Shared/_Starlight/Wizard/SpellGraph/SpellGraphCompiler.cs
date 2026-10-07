using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

public sealed class SpellStep
{
    public SpellGlyphPrototype Glyph = default!;
    public int Repeats;
    public float RepeatInterval = SpellGraphCompiler.RepeatIntervalSeconds;
    public float Delay;
    public float Cost;

    /// <summary>Multiplies the strength of the effect.</summary>
    public float Magnitude = 1f;

    /// <summary>Extra tiles of area.</summary>
    public float RadiusBonus;

    /// <summary>How many more creatures the effect jumps to.</summary>
    public int ChainJumps;
}

/// <summary>The validated, ready-to-run form of a <see cref="SpellGraph"/>.</summary>
public sealed class SpellGraphPlan
{
    public SpellGlyphPrototype Form = default!;
    public List<SpellStep> Steps = [];
    public float Cost;
    public string Name = string.Empty;
    public int ProjectileCount = 1;
    public float CooldownMultiplier = 1f;

    /// <summary>Items that must be held and are consumed when this spell is cast.</summary>
    public List<SpellGlyphPrototype> Reagents = [];

    /// <summary>Every school any glyph in this spell belongs to.</summary>
    public HashSet<string> Schools = [];

    public SpellDelivery Delivery => Form.Delivery;
    public SpellTargetMode TargetMode => Form.TargetMode;
}

/// <summary>
/// Validates a <see cref="SpellGraph"/> and turns it into a <see cref="SpellGraphPlan"/> with a cost.
/// Pure and shared, so the client editors can preview exactly what the server will accept.
/// The server always recompiles; it never trusts a client-supplied cost.
/// </summary>
public static class SpellGraphCompiler
{
    public const int MaxEffects = 4;
    public const int MaxRepeatsPerEffect = 3;
    public const int MaxAmplifyPerEffect = 3;
    public const int MaxChainJumps = 4;
    public const int MaxExtraBolts = 4;
    public const float MaxDelaySeconds = 8f;
    public const float RepeatIntervalSeconds = 0.35f;
    public const float LingerIntervalSeconds = 1f;
    public const float RepeatCostFactor = 0.9f;
    public const float AmplifyStrength = 0.5f;
    public const float AmplifyCostFactor = 0.35f;
    public const float WidenCostFactor = 0.25f;
    public const float ChainCostPerJump = 5f;
    public const float SplitCostFactor = 0.6f;
    public const float MinCostMultiplier = 0.4f;
    public const float AffinityDiscount = 0.85f;
    public const float MaxCooldownReduction = 0.6f;

    public static bool TryCompile(IPrototypeManager protos,
        SpellGraph graph,
        int maxNodes,
        IReadOnlyCollection<ProtoId<SpellGlyphPrototype>>? known,
        [NotNullWhen(true)] out SpellGraphPlan? plan,
        [NotNullWhen(false)] out string? error)
        => TryCompile(protos, graph, maxNodes, known, null, out plan, out error);

    public static bool TryCompile(IPrototypeManager protos,
        SpellGraph graph,
        int maxNodes,
        IReadOnlyCollection<ProtoId<SpellGlyphPrototype>>? known,
        IReadOnlyCollection<string>? affinities,
        [NotNullWhen(true)] out SpellGraphPlan? plan,
        [NotNullWhen(false)] out string? error)
    {
        plan = null;

        if (graph.Nodes.Count == 0)
            return Fail("spellcraft-error-empty", out error);

        if (graph.Nodes.Count > maxNodes)
            return Fail("spellcraft-error-too-complex", out error);

        var nodes = new Dictionary<int, SpellGlyphPrototype>();
        var order = new List<int>();
        foreach (var node in graph.Nodes)
        {
            if (nodes.ContainsKey(node.Id))
                return Fail("spellcraft-error-duplicate-node", out error);

            if (!protos.TryIndex(node.Glyph, out var glyph))
                return Fail("spellcraft-error-unknown-glyph", out error);

            if (known != null && !known.Contains(node.Glyph))
                return Fail("spellcraft-error-not-known", out error);

            nodes[node.Id] = glyph;
            order.Add(node.Id);
        }

        var forms = order.Where(id => nodes[id].Category == GlyphCategory.Form).ToList();
        if (forms.Count != 1)
            return Fail("spellcraft-error-one-form", out error);

        var formId = forms[0];
        var form = nodes[formId];

        // Validate links and index them by parent.
        var children = new Dictionary<int, List<int>>();
        var hasParent = new HashSet<int>();
        var seen = new HashSet<(int, int)>();
        foreach (var link in graph.Links)
        {
            if (!nodes.TryGetValue(link.From, out var from) || !nodes.TryGetValue(link.To, out var to))
                return Fail("spellcraft-error-bad-link", out error);

            if (!seen.Add((link.From, link.To)))
                return Fail("spellcraft-error-bad-link", out error);

            var allowed = (from.Category, to.Category) switch
            {
                (GlyphCategory.Form, GlyphCategory.Effect) => true,
                (GlyphCategory.Form, GlyphCategory.Augment) => true,
                (GlyphCategory.Effect, GlyphCategory.Augment) => true,
                _ => false,
            };
            if (!allowed)
                return Fail("spellcraft-error-bad-link", out error);

            if (!children.TryGetValue(link.From, out var list))
                children[link.From] = list = [];

            list.Add(link.To);
            hasParent.Add(link.To);
        }

        // Every non-form node must be connected.
        foreach (var id in order)
        {
            if (id != formId && !hasParent.Contains(id))
                return Fail("spellcraft-error-orphan", out error);
        }

        var effectIds = order
            .Where(id => nodes[id].Category == GlyphCategory.Effect)
            .ToList();
        if (effectIds.Count == 0)
            return Fail("spellcraft-error-no-effect", out error);

        if (effectIds.Count > MaxEffects)
            return Fail("spellcraft-error-too-many-effects", out error);

        // Effects must be fed by the form, and must work with how the form delivers them.
        var formChildren = children.GetValueOrDefault(formId) ?? [];
        foreach (var id in effectIds)
        {
            if (!formChildren.Contains(id))
                return Fail("spellcraft-error-orphan", out error);

            if (!nodes[id].SupportsDelivery(form.Delivery))
                return Fail("spellcraft-error-incompatible", out error);
        }

        var steps = new Dictionary<int, SpellStep>();
        foreach (var id in effectIds)
            steps[id] = new SpellStep { Glyph = nodes[id] };

        var costMultiplier = 1f;
        var cooldownReduction = 0f;
        var extraBolts = 0;
        var flatCost = form.Cost;
        var amplifyCount = new Dictionary<SpellStep, int>();
        var widenCount = new Dictionary<SpellStep, int>();

        foreach (var id in order)
        {
            var glyph = nodes[id];
            if (glyph.Category != GlyphCategory.Augment)
                continue;

            flatCost += glyph.Cost;

            // Which effects does this augment apply to?
            var targets = new List<SpellStep>();
            if (formChildren.Contains(id))
                targets.AddRange(steps.Values);

            foreach (var effectId in effectIds)
            {
                if (children.GetValueOrDefault(effectId)?.Contains(id) == true)
                    targets.Add(steps[effectId]);
            }

            switch (glyph.Augment)
            {
                case AugmentKind.Cheaper:
                    costMultiplier *= glyph.Value;
                    continue;
                case AugmentKind.Quicken:
                    cooldownReduction += glyph.Value;
                    continue;
                case AugmentKind.Split:
                    if (form.Delivery != SpellDelivery.Bolt)
                        return Fail("spellcraft-error-needs-bolt", out error);

                    extraBolts += (int) MathF.Round(glyph.Value);
                    continue;
            }

            foreach (var step in targets.Distinct())
            {
                switch (glyph.Augment)
                {
                    case AugmentKind.Repeat:
                        step.Repeats += (int) MathF.Round(glyph.Value);
                        break;
                    case AugmentKind.Linger:
                        step.Repeats += (int) MathF.Round(glyph.Value);
                        step.RepeatInterval = LingerIntervalSeconds;
                        break;
                    case AugmentKind.Delay:
                        step.Delay += glyph.Value;
                        break;
                    case AugmentKind.Amplify:
                        step.Magnitude += AmplifyStrength * glyph.Value;
                        amplifyCount[step] = amplifyCount.GetValueOrDefault(step) + 1;
                        break;
                    case AugmentKind.Widen:
                        step.RadiusBonus += glyph.Value;
                        widenCount[step] = widenCount.GetValueOrDefault(step) + 1;
                        break;
                    case AugmentKind.Chain:
                        step.ChainJumps += (int) MathF.Round(glyph.Value);
                        break;
                }
            }
        }

        if (extraBolts > MaxExtraBolts)
            return Fail("spellcraft-error-too-many-bolts", out error);

        var total = flatCost;
        var schools = new HashSet<string>();
        var reagents = new List<SpellGlyphPrototype>();

        foreach (var glyph in nodes.Values)
        {
            foreach (var school in glyph.Schools)
                schools.Add(school);

            if (glyph.Reagent != null && !reagents.Contains(glyph))
                reagents.Add(glyph);
        }

        foreach (var id in effectIds)
        {
            var step = steps[id];
            if (step.Repeats > MaxRepeatsPerEffect)
                return Fail("spellcraft-error-too-many-repeats", out error);

            if (step.Delay > MaxDelaySeconds)
                return Fail("spellcraft-error-too-slow", out error);

            if (amplifyCount.GetValueOrDefault(step) > MaxAmplifyPerEffect)
                return Fail("spellcraft-error-too-strong", out error);

            if (step.ChainJumps > MaxChainJumps)
                return Fail("spellcraft-error-too-many-jumps", out error);

            var cost = step.Glyph.Cost * (1f + (step.Repeats * RepeatCostFactor));
            cost *= 1f + (amplifyCount.GetValueOrDefault(step) * AmplifyCostFactor);
            cost *= 1f + (widenCount.GetValueOrDefault(step) * WidenCostFactor);
            cost += step.ChainJumps * ChainCostPerJump;

            if (affinities != null && step.Glyph.Schools.Any(affinities.Contains))
                cost *= AffinityDiscount;

            step.Cost = cost;
            total += cost;
        }

        if (extraBolts > 0)
            total += form.Cost * SplitCostFactor * extraBolts;

        total = MathF.Max(1f, total * MathF.Max(MinCostMultiplier, costMultiplier));

        plan = new SpellGraphPlan
        {
            Form = form,
            Cost = MathF.Round(total, 1),
            Name = string.Join(" + ", effectIds.Select(id => Loc.GetString(nodes[id].Name))),
            ProjectileCount = 1 + extraBolts,
            CooldownMultiplier = 1f - MathF.Min(MaxCooldownReduction, cooldownReduction),
            Reagents = reagents,
            Schools = schools,
        };
        foreach (var id in effectIds)
            plan.Steps.Add(steps[id]);

        error = null;
        return true;
    }

    /// <summary>
    /// Builds a graph from an ordered chain, the way a slot-based editor produces one:
    /// the Form first, then Effects, with each Augment applying to the Effect before it
    /// (or to the whole spell if it comes before any Effect).
    /// </summary>
    public static bool TryBuildChain(IPrototypeManager protos,
        IEnumerable<ProtoId<SpellGlyphPrototype>> chain,
        [NotNullWhen(true)] out SpellGraph? graph,
        [NotNullWhen(false)] out string? error)
    {
        graph = null;
        var result = new SpellGraph();
        var nextId = 0;
        int? formId = null;
        int? lastEffect = null;

        foreach (var id in chain)
        {
            if (!protos.TryIndex(id, out var glyph))
                return Fail("spellcraft-error-unknown-glyph", out error);

            var nodeId = nextId++;
            result.Nodes.Add(new SpellNode(nodeId, id));

            switch (glyph.Category)
            {
                case GlyphCategory.Form:
                    if (formId != null)
                        return Fail("spellcraft-error-one-form", out error);

                    formId = nodeId;
                    break;
                case GlyphCategory.Effect:
                    if (formId == null)
                        return Fail("spellcraft-error-form-first", out error);

                    result.Links.Add(new SpellLink(formId.Value, nodeId));
                    lastEffect = nodeId;
                    break;
                case GlyphCategory.Augment:
                    if (formId == null)
                        return Fail("spellcraft-error-form-first", out error);

                    result.Links.Add(new SpellLink(lastEffect ?? formId.Value, nodeId));
                    break;
            }
        }

        graph = result;
        error = null;
        return true;
    }

    private static bool Fail(string locId, out string? error)
    {
        error = Loc.GetString(locId);
        return false;
    }
}
