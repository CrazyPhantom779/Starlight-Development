using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Sent directed at a Hologram about to be returned to be handled by other systems.
/// </summary>
[ByRefEvent]
public record struct HologramReturnAttemptEvent(bool Cancelled = false);

/// <summary>
/// Sent directed at a Hologram before they are returned, often due to not being near a projector.
/// </summary>
public readonly record struct HologramReturnedEvent(EntityUid Projector);

/// <summary>
/// Sent directed at a Hologram about to be killed to be handled by other systems.
/// </summary>
[ByRefEvent]
public record struct HologramKillAttemptEvent(bool Cancelled = false);

/// <summary>
/// Sent directed at a Hologram being killed, often due to not having any valid projectors.
/// </summary>
public readonly record struct HologramKilledEvent();

/// <summary>
/// Sent directed at a Hologram when searching for any valid Projectors.
/// Allows for manually setting the projector to use.
/// </summary>
[ByRefEvent]
public record struct HologramGetProjectorEvent(EntityUid? ProjectorOverride = null, bool Override = false);

/// <summary>
/// Sent directed at a Hologram when they're checking if a specific projector is valid.
/// Allows for manually determining if a projector is valid for a given Hologram.
/// </summary>
[ByRefEvent]
public record struct HologramCheckProjectorValidEvent(EntityUid Projector, bool? Valid = null);

/// <summary>
/// Opens the hologram blade console UI for the blade this mind is stored in / projected from.
/// </summary>
public sealed partial class HologramOpenConsoleActionEvent : InstantActionEvent;
