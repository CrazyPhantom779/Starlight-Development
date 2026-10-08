using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Wizard.Casting;

/// <summary>
/// An errand the Winds ask of a wizard: a small task to be done around the station. Finishing one makes the wizard
/// more powerful. Errands are separate from objectives and always come in threes, drawn at random.
/// </summary>
[Prototype]
public sealed partial class ErrandPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public float Weight = 1f;

    /// <summary>Errands with the same group are never active at once, so wizards are sent to different places.</summary>
    [DataField]
    public string Group = string.Empty;

    [DataField(required: true)]
    public ErrandCondition Condition = default!;

    [DataField(required: true)]
    public ErrandReward Reward = default!;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class ErrandCondition
{
    /// <summary>How much progress completes the errand.</summary>
    [DataField]
    public int Goal = 1;
}

/// <summary>Spend time near something found around the station. Progress is seconds.</summary>
public sealed partial class VisitNearCondition : ErrandCondition
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public float Range = 6f;
}

/// <summary>Cast spells near something found around the station.</summary>
public sealed partial class CastNearCondition : ErrandCondition
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public float Range = 8f;
}

/// <summary>Affect different creatures with your spells. Each creature counts once.</summary>
public sealed partial class AffectCreaturesCondition : ErrandCondition;

/// <summary>Cast spells that use a glyph of a school.</summary>
public sealed partial class CastSchoolCondition : ErrandCondition
{
    [DataField(required: true)]
    public string School = string.Empty;
}

/// <summary>Cast spells delivered a certain way (place runes, fire bolts, touch things).</summary>
public sealed partial class CastDeliveryCondition : ErrandCondition
{
    [DataField(required: true)]
    public Content.Shared._Starlight.Wizard.SpellGraph.SpellDelivery Delivery;
}

/// <summary>Perform rites at a ritual circle.</summary>
public sealed partial class PerformRitesCondition : ErrandCondition;

[ImplicitDataDefinitionForInheritors]
public abstract partial class ErrandReward;

public sealed partial class MaxWindReward : ErrandReward
{
    [DataField]
    public float Amount = 10f;
}

public sealed partial class RegenReward : ErrandReward
{
    [DataField]
    public float Amount = 0.25f;
}

public sealed partial class NodesReward : ErrandReward
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class SlotsReward : ErrandReward
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class GlyphReward : ErrandReward
{
    [DataField]
    public int Count = 1;
}

public sealed partial class RoteReward : ErrandReward
{
    [DataField]
    public int Count = 1;
}

public sealed partial class AttuneReward : ErrandReward;

/// <summary>One errand as the window shows it.</summary>
[Serializable, NetSerializable]
public sealed class ErrandInfo(string title, string description, int progress, int goal, string reward)
{
    public readonly string Title = title;
    public readonly string Description = description;
    public readonly int Progress = progress;
    public readonly int Goal = goal;
    public readonly string Reward = reward;
}
