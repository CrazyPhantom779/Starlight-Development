using Robust.Shared.GameStates;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared._Starlight.Magic.Systems;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows overriding MobThresholds (crit, death, etc.) as a status effect. Not stackable.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(MobThresholdStatusEffectSystem))]
public sealed partial class MobThresholdsStatusEffectComponent : Component
{
    [DataField(required: true)]
    public SortedDictionary<FixedPoint2, MobState> Thresholds = [];

    /// <summary>
    /// Whether or not this entity can be revived out of a dead state.
    /// </summary>
    [DataField]
    public bool AllowRevives;
}
