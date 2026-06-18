using Robust.Shared.Prototypes;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;

namespace Content.Shared._Starlight.Antags.TerrorSpider;

public sealed partial class AcidVentSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> _tagGasVent = "GasVent";

    [Dependency] private TagSystem _tag = default!;
    [Dependency] private WeldableSystem _weldable = default!;
    public override void Initialize()
        => SubscribeLocalEvent<AcidVentEvent>(OnAcidVent);

    private void OnAcidVent(AcidVentEvent args)
    {
        if (!_tag.HasTag(args.Target, _tagGasVent))
            return;

        args.Handled = true;

        _weldable.SetWeldedState(args.Target, false);
    }
}
