using Content.Server._Starlight.Holograms.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Power;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed class HologramJobSpawnSystem : EntitySystem
{
    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramJobSpawnComponent, ContainerSpawnEvent>(OnContainerSpawn);
        SubscribeLocalEvent<HologramJobSpawnComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<HologramJobSpawnComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnContainerSpawn(EntityUid uid, HologramJobSpawnComponent component, ref ContainerSpawnEvent args)
    {
        if (!TryComp<HologramBladeServerComponent>(uid, out var bladeServer))
            return;

        if (!TryComp<HologramBrainChipComponent>(args.Player, out var brainChip))
            return;

        if (!TryComp<MindContainerComponent>(args.Player, out var mindContainer) ||
            mindContainer.Mind is not { } mindId)
            return;

        brainChip.HoloMind = mindId;

        TryNameBodyChip(uid, bladeServer, mindId);

        if (!component.SpawnOnJoin)
            return;

        if (component.LinkedHologram != null && Exists(component.LinkedHologram.Value))
            return;

        if (!_hologram.TryGenerateHumanoidHologram(mindId, Transform(uid).Coordinates, out var holo))
            return;

        component.LinkedHologram = holo.Value;

        // Treat the blade server as this hologram's home projector.
        if (TryComp<HologramProjectedComponent>(holo.Value, out var projected))
        {
            var netServer = GetNetEntity(uid);
            projected.CurProjector = netServer;
            projected.ProjectorOverride = netServer;
            Dirty(holo.Value, projected);
        }
    }

    private void TryNameBodyChip(EntityUid uid, HologramBladeServerComponent bladeServer, EntityUid mindId)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var slots))
            return;

        if (!_itemSlots.TryGetSlot(uid, bladeServer.BodyChipSlot, out var bodySlot, slots) ||
            bodySlot.Item == null)
            return;

        if (!TryComp<HologramBodyChipComponent>(bodySlot.Item.Value, out var bodyChip))
            return;

        if (!string.IsNullOrWhiteSpace(bodyChip.HologramName))
            return;

        if (TryComp<MindComponent>(mindId, out var mind))
            bodyChip.HologramName = mind.CharacterName;
    }

    private void OnPowerChanged(EntityUid uid, HologramJobSpawnComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        KillLinkedHologram(component);
    }

    private void OnShutdown(EntityUid uid, HologramJobSpawnComponent component, ComponentShutdown args)
        => KillLinkedHologram(component);

    private void KillLinkedHologram(HologramJobSpawnComponent component)
    {
        if (component.LinkedHologram == null)
            return;

        if (Exists(component.LinkedHologram.Value))
            _hologram.DoKillHologram(component.LinkedHologram.Value);

        component.LinkedHologram = null;
    }
}
