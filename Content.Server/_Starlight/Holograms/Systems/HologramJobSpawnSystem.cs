using Content.Server._Starlight.Holograms.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Power;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Converts AI-style container job spawns into usable hologram blade-server state.
/// The player job entity is the brain chip; this system records its mind and optionally projects it.
/// </summary>
public sealed class HologramJobSpawnSystem : EntitySystem
{
    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramJobSpawnComponent, ContainerSpawnEvent>(OnContainerSpawn);
        SubscribeLocalEvent<HologramBladeServerComponent, PowerChangedEvent>(OnBladePowerChanged);
    }

    private void OnContainerSpawn(EntityUid uid, HologramJobSpawnComponent component, ref ContainerSpawnEvent args)
    {
        if (!TryComp<HologramBladeServerComponent>(uid, out var bladeServer))
            return;

        if (!TryComp<HologramBrainChipComponent>(args.Player, out var brainChip))
            return;

        if (!TryComp<MindContainerComponent>(args.Player, out var mindContainer) || mindContainer.Mind is not { } mindId)
            return;

        brainChip.HoloMind = mindId;
        EnsureBodyChip(uid, bladeServer, mindId);

        if (!component.SpawnOnJoin)
            return;

        if (bladeServer.ActiveHologram is { } active && Exists(active))
            return;

        var projector = FindSameGridProjector(uid);
        var coords = projector is { } projectorUid
            ? Transform(projectorUid).Coordinates
            : Transform(uid).Coordinates;

        if (!_hologram.TryGenerateHumanoidHologram(mindId, coords, out var hologram) || hologram is not { } hologramUid)
            return;

        bladeServer.ActiveHologram = hologramUid;

        if (projector is { } linkedProjector)
            SetProjection(hologramUid, linkedProjector);
    }

    private void EnsureBodyChip(EntityUid bladeServerUid, HologramBladeServerComponent bladeServer, EntityUid mindId)
    {
        if (!TryComp<ItemSlotsComponent>(bladeServerUid, out var slots))
            return;

        if (!_itemSlots.TryGetSlot(bladeServerUid, bladeServer.BodyChipSlot, out var bodySlot, slots))
            return;

        if (bodySlot.Item is { } existing)
        {
            NameBodyChip(existing, mindId);
            return;
        }

        var chip = Spawn("HologramJobBodyChip", Transform(bladeServerUid).Coordinates);
        if (!_itemSlots.TryInsert(bladeServerUid, bodySlot, chip, user: null))
        {
            Del(chip);
            return;
        }

        NameBodyChip(chip, mindId);
    }

    private void NameBodyChip(EntityUid bodyChip, EntityUid mindId)
    {
        if (!TryComp<HologramBodyChipComponent>(bodyChip, out var bodyComp))
            return;

        if (!string.IsNullOrWhiteSpace(bodyComp.HologramName))
            return;

        if (TryComp<MindComponent>(mindId, out var mind))
            bodyComp.HologramName = mind.CharacterName;
    }

    private EntityUid? FindSameGridProjector(EntityUid bladeServer)
    {
        var grid = Transform(bladeServer).GridUid;
        if (grid == null)
            return null;

        var query = EntityQueryEnumerator<HologramProjectorComponent>();
        while (query.MoveNext(out var projector, out var projectorComp))
        {
            if (!projectorComp.IsActive)
                continue;

            if (Transform(projector).GridUid == grid)
                return projector;
        }

        return null;
    }

    private void SetProjection(EntityUid hologram, EntityUid projector)
    {
        if (!TryComp<HologramProjectedComponent>(hologram, out var projected))
            return;

        var netProjector = GetNetEntity(projector);
        projected.CurProjector = netProjector;
        projected.ProjectorOverride = null;
        projected.CurrentlyInProjector = true;
        projected.VanishTime = TimeSpan.Zero;
        Dirty(hologram, projected);
    }

    private void OnBladePowerChanged(EntityUid uid, HologramBladeServerComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        KillActive(component);
    }

    private void KillActive(HologramBladeServerComponent component)
    {
        if (component.ActiveHologram is { } hologram && Exists(hologram))
            _hologram.DoKillHologram(hologram);

        component.ActiveHologram = null;
    }
}
