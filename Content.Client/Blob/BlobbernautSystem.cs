using Content.Client.DamageState;
using Content.Shared.Blob;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.Blob;

public sealed partial class BlobbernautSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobbernautComponent, ComponentHandleState>(OnBlobTileHandleState);
    }

    private void OnBlobTileHandleState(EntityUid uid, BlobbernautComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not BlobbernautComponentState state)
            return;

        if (component.Color == state.Color)
            return;

        component.Color = state.Color;

        if (!TryComp<SpriteComponent>(uid, out _))
            return;

        foreach (var key in new[] { DamageStateVisualLayers.Base })
        {
            if (!_sprite.LayerMapTryGet(uid, key, out _, false))
                continue;

            _sprite.LayerSetColor(uid, key, component.Color);
        }
    }
}
