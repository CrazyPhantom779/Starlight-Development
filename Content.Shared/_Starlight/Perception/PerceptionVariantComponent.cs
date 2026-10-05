using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Perception;

/// <summary>
/// Lets an entity look different to different observers.
/// When the entity is examined, the first variant whose requirements the examiner meets is used.
/// This is a standalone module: it has no dependency on magic, wizards or any antagonist.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PerceptionVariantComponent : Component
{
    /// <summary>
    /// Variants in priority order. The first match wins unless <see cref="Mode"/> says otherwise.
    /// </summary>
    [DataField(required: true)]
    public List<PerceptionVariant> Variants = [];

    /// <summary>
    /// How to choose between several variants that all match a given observer.
    /// </summary>
    [DataField]
    public PerceptionSelectionMode Mode = PerceptionSelectionMode.First;

    /// <summary>
    /// Extra salt mixed into the per-observer choice in <see cref="PerceptionSelectionMode.StablePerObserver"/>,
    /// so two different entities don't pick the same index for the same observer.
    /// </summary>
    [DataField]
    public int Salt;
}

/// <summary>
/// One way an entity can be perceived.
/// </summary>
[DataDefinition]
public sealed partial class PerceptionVariant
{
    /// <summary>
    /// Observer must pass this whitelist. Null means anyone.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Observer must NOT match this blacklist.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Replaces the entity's normal description. Takes a localisation id or plain text.
    /// If null the normal description is kept.
    /// </summary>
    [DataField]
    public string? Description;

    /// <summary>
    /// Extra lines appended to the examine text. Each entry is a localisation id or plain text.
    /// </summary>
    [DataField]
    public List<string> Append = [];

    /// <summary>
    /// Priority of the appended lines in the examine message. Higher is nearer the top.
    /// </summary>
    [DataField]
    public int AppendPriority;
}

[Serializable, NetSerializable]
public enum PerceptionSelectionMode : byte
{
    /// <summary>The first matching variant is used.</summary>
    First,

    /// <summary>
    /// Among the matching variants, one is picked using a hash of (observer, entity, salt).
    /// The same observer always sees the same thing for the same entity, but different observers differ.
    /// </summary>
    StablePerObserver,
}
