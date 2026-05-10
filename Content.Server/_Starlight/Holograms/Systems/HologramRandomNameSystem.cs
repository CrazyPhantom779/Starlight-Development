using Content.Server._Starlight.Holograms.Components;
using Content.Shared.Dataset;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed class HologramRandomNameSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramRandomNameComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, HologramRandomNameComponent component, MapInitEvent args)
    {
        var dataset = _prototype.Index<LocalizedDatasetPrototype>(component.NameDataset);
        _meta.SetEntityName(uid, _random.Pick(dataset));
    }
}
