using Content.Shared.Starlight.Medical.BrainJar;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client.Starlight.Medical.BrainJar;

public sealed class BrainJarSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<BrainJarComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BrainJarComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<BrainJarComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnInit(EntityUid uid, BrainJarComponent component, ComponentInit args) =>
        UpdateOrganSprite(uid, component);

    private void OnItemInserted(EntityUid uid, BrainJarComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.SlotId)
            return;

        component.ContainedOrgan = args.Entity;
        UpdateOrganSprite(uid, component);
    }

    private void OnItemRemoved(EntityUid uid, BrainJarComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.SlotId)
            return;

        component.ContainedOrgan = null;
        UpdateOrganSprite(uid, component);
    }

    private void UpdateOrganSprite(EntityUid uid, BrainJarComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var jarSprite))
            return;

        if (!_sprite.LayerMapTryGet((uid, jarSprite), component.OrganLayerMap, out var organLayer, false))
            return;

        // If there's an organ, show its sprite
        if (component.ContainedOrgan != null && TryComp<SpriteComponent>(component.ContainedOrgan.Value, out var organSprite))
        {
            // Copy the organ's sprite to the jar's organ layer  
            if (organSprite.BaseRSI != null)
            {
                // Get the base layer
                if (_sprite.TryGetLayer((component.ContainedOrgan.Value, organSprite), 0, out var layer, false))
                {
                    _sprite.LayerSetVisible((uid, jarSprite), organLayer, true);
                    _sprite.LayerSetRsi((uid, jarSprite), organLayer, organSprite.BaseRSI);
                    _sprite.LayerSetRsiState((uid, jarSprite), organLayer, layer.State);
                }
            }
        }
        else
        {
            // No organ, hide the layer
            _sprite.LayerSetVisible((uid, jarSprite), organLayer, false);
        }
    }
}
