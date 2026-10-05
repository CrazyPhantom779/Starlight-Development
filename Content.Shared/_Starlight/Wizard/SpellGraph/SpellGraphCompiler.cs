using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

public sealed class SpellStep
{
    public SpellGlyphPrototype Glyph = default!;
    public int Repeats;
    public float Delay;
    public float Cost;
}

/// <summary>The validated, ready-to-run form of a <see cref="SpellGraph"/>.</summary>
public sealed class SpellGraphPlan
{
    public SpellGlyphPrototype Form = default!;
    public List<SpellStep> Steps = [];
    public float Cost;
    public string Name = string.Empty;
    public SpellTargetMode TargetMode => Form.TargetMode;
}

/// <summary>
/// Validates a <see cref="SpellGraph"/> and turns it into a <see cref="SpellGraphPlan"/> with a cost.
/// Pure and shared, so the client editor can preview exactly what the server will accept.
/// The server always recompiles; it never trusts a client-supplied cost.
/// </summary>
public static class SpellGraphCompiler
{
    public const int MaxEffects = 4;
    public const int MaxRepeatsPerEffect = 3;
    public const float MaxDelaySeconds = 8f;
    public const float RepeatIntervalSeconds = 0.35f;
    public const float RepeatCostFactor = 0.9f;
    public const float MinCostMultiplier = 0.4f;

    public static bool TryCompile(IPrototypeManager protos,
        SpellGraph graph,
        int maxNodes,
        IReadOnlyCollection<ProtoId<SpellGlyphPrototype>>? known,
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
                children[link.From] = list = new List<int>();

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

        // Effects must be fed by the form.
        var formChildren = children.GetValueOrDefault(formId) ?? new List<int>();
        foreach (var id in effectIds)
        {
            if (!formChildren.Contains(id))
                return Fail("spellcraft-error-orphan", out error);

            var effect = nodes[id];
            var supported = form.TargetMode == SpellTargetMode.World
                ? effect.WorldEvent != null
                : effect.InstantEvent != null;
            if (!supported)
                return Fail("spellcraft-error-incompatible", out error);
        }

        var steps = new Dictionary<int, SpellStep>();
        foreach (var id in effectIds)
            steps[id] = new SpellStep { Glyph = nodes[id] };

        var costMultiplier = 1f;
        var flatCost = form.Cost;

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

            if (glyph.Augment == AugmentKind.Cheaper)
            {
                costMultiplier *= glyph.Value;
                continue;
            }

            foreach (var step in targets.Distinct())
            {
                switch (glyph.Augment)
                {
                    case AugmentKind.Repeat:
                        step.Repeats += (int) MathF.Round(glyph.Value);
                        break;
                    case AugmentKind.Delay:
                        step.Delay += glyph.Value;
                        break;
                }
            }
        }

        var total = flatCost;
        foreach (var id in effectIds)
        {
            var step = steps[id];
            if (step.Repeats > MaxRepeatsPerEffect)
                return Fail("spellcraft-error-too-many-repeats", out error);

            if (step.Delay > MaxDelaySeconds)
                return Fail("spellcraft-error-too-slow", out error);

            step.Cost = step.Glyph.Cost * (1f + (step.Repeats * RepeatCostFactor));
            total += step.Cost;
        }

        total = MathF.Max(1f, total * MathF.Max(MinCostMultiplier, costMultiplier));

        plan = new SpellGraphPlan
        {
            Form = form,
            Cost = MathF.Round(total, 1),
            Name = string.Join(" + ", effectIds.Select(id => Loc.GetString(nodes[id].Name))),
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
