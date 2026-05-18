using Content.Shared.Actions;

namespace Content.Shared._Starlight.Holograms.Events;

/// <summary>
/// Raised on a projected hologram before it is returned to a projector.
/// Handlers may cancel if another system is handling the return.
/// </summary>
[ByRefEvent]
public record struct HologramReturnAttemptEvent(bool Cancelled = false);

/// <summary>
/// Raised on a projected hologram after return validation has passed and before it is moved.
/// </summary>
public readonly record struct HologramReturnedEvent(EntityUid Projector);

/// <summary>
/// Raised on a hologram before it is killed.
/// </summary>
[ByRefEvent]
public record struct HologramKillAttemptEvent(bool Cancelled = false);

/// <summary>
/// Raised on a hologram once kill validation has passed and before server deletion/effects happen.
/// </summary>
public readonly record struct HologramKilledEvent;

/// <summary>
/// Raised when a projected hologram is choosing a projector. Set Override=true and
/// ProjectorOverride to force a specific projector; the shared system will still
/// validate that projector unless the caller handles movement itself.
/// </summary>
[ByRefEvent]
public record struct HologramGetProjectorEvent(EntityUid? ProjectorOverride = null, bool Override = false);

/// <summary>
/// Raised when checking if a specific projector is valid for a hologram. Set Valid
/// to force the decision; leave it null to use normal range/occlusion checks.
/// </summary>
[ByRefEvent]
public record struct HologramCheckProjectorValidEvent(EntityUid Projector, bool? Valid = null);

/// <summary>
/// Opens the hologram blade console UI for the blade this mind is stored in or projected from.
/// </summary>
public sealed partial class HologramOpenConsoleActionEvent : InstantActionEvent;
