using Content.Shared.Actions;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

/// <summary>Raised when a woven, aimed spell is cast.</summary>
public sealed partial class SpellGraphWorldEvent : WorldTargetActionEvent;

/// <summary>Raised when a woven, self-centred spell is cast.</summary>
public sealed partial class SpellGraphInstantEvent : InstantActionEvent;
