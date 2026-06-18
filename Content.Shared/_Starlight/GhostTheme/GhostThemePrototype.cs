using Content.Shared._Starlight.Abstract.Conditions;
using Content.Shared._Starlight.Trail;
using Content.Shared.Starlight.Utility;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.GhostTheme;

[Prototype]
public sealed partial class GhostThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public string Description { get; private set; } = string.Empty;

    [DataField(required: true)]
    public ExtendedSpriteSpecifier SpriteSpecifier { get; private set; } = default!;

    [DataField]
    public bool Colorizeable = false;

    [DataField]
    public bool Private = false;

    [DataField]
    public TrailSettings? Trail = null;

    [DataField]
    public List<BaseRequirement> Requirements = [];
}
