namespace Content.Shared._Starlight.Wizard.Wind;

/// <summary>
/// Raised on a performer after a spell pushed their Wind below zero.
/// </summary>
/// <param name="Deficit">How far below zero Wind now is (positive number).</param>
[ByRefEvent]
public readonly record struct WindOverdrawnEvent(EntityUid Performer, float Deficit);
