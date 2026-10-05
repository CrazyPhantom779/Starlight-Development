namespace Content.Shared._Starlight.Perception;

/// <summary>
/// Raised on an examined entity before its description is built, so systems can replace the description
/// for one specific examiner. Set <see cref="Description"/> to override the entity's metadata description.
/// </summary>
[ByRefEvent]
public record struct GetPerceivedDescriptionEvent(EntityUid Examiner, string? Description = null);
