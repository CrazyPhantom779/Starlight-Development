using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Lets an entity project holograms - cameras, dedicated projector machines, etc.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HologramProjectorComponent : Component
{
    /// <summary>
    /// Range before a connected hologram starts its return-grace timer. Still needs line of sight.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float ProjectorRange = 7f;

    /// <summary>
    /// Where the beam effect spawns relative to the projector, per facing direction.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Dictionary<Direction, Vector2> EffectOffsets { get; set; } = new()
    {
        { Direction.North, Vector2.Zero },
        { Direction.East, Vector2.Zero },
        { Direction.South, Vector2.Zero },
        { Direction.West, Vector2.Zero },
    };

    /// <summary>
    /// Powered and switched on (for camera-based projectors, this also means the camera is on).
    /// Doesn't account for damage cooldown on its own - use <see cref="IsFunctional"/> for that.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool IsActive = true;

    /// <summary>
    /// True while shut down from damage. Can't start or accept a hologram, and drops whatever's
    /// currently connected the same way going out of range would.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool IsOnCooldown;

    /// <summary>
    /// When the current cooldown ends. Server-only - clients just need <see cref="IsOnCooldown"/>.
    /// </summary>
    public TimeSpan CooldownUntil = TimeSpan.Zero;

    /// <summary>
    /// Whether this projector can host or reconnect a hologram right now.
    /// </summary>
    public bool IsFunctional => IsActive && !IsOnCooldown;

    /// <summary>
    /// Cooldown seconds per point of damage, before the type multiplier below applies.
    /// </summary>
    [DataField]
    public float CooldownSecondsPerDamage = 0.75f;

    /// <summary>
    /// Longest cooldown one hit can cause, no matter how much damage it did.
    /// </summary>
    [DataField]
    public float MaxCooldownSeconds = 60f;

    /// <summary>
    /// Cooldown multiplier by damage type (DamageTypePrototype ID). Anything not listed is 1x -
    /// a wrench dents it, a stray shock actually fries the electronics.
    /// </summary>
    [DataField]
    public Dictionary<string, float> CooldownDamageTypeMultipliers = new()
    {
        { "Shock", 2f },
        { "Heat", 1.5f },
        { "Caustic", 1.25f },
    };

    /// <summary>
    /// Plays once when damage is enough to start a cooldown.
    /// </summary>
    [DataField]
    public SoundSpecifier DamagedSound = new SoundCollectionSpecifier("sparks");
}
