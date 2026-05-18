using Content.Server._Starlight.Holograms.Components;
using Content.Shared.Dataset;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed partial class HologramRandomNameSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("hologram.names");

        SubscribeLocalEvent<HologramRandomNameComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, HologramRandomNameComponent component, MapInitEvent args)
    {
        if (!_prototype.TryIndex(component.NameDataset, out LocalizedDatasetPrototype? dataset))
        {
            _sawmill.Warning($"Unable to assign random hologram name to {ToPrettyString(uid)}: dataset {component.NameDataset} does not exist.");
            return;
        }

        _meta.SetEntityName(uid, _random.Pick(dataset));
    }
}
