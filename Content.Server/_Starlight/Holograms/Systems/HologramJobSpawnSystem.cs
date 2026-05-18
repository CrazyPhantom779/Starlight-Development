using Content.Server._Starlight.Holograms.Components;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Power;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Installs hologram job minds into blade servers.
/// Job racks start empty; a job blade is created only when a hologram player actually joins.
/// </summary>
public sealed partial class HologramJobSpawnSystem : EntitySystem
{
    private const string HologramJobId = "Hologram";
    private const string JobBladePrototype = "HologramJobBladeServer";
    private const string FailedSpawnPrototype = "MobObserver";
    private const float StrayInstallInterval = 1f;

    private float _strayInstallAccumulator;

    [Dependency] private HologramBladeLawSystem _bladeLaws = default!;
    [Dependency] private HologramSystem _hologram = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new[] { typeof(ContainerSpawnPointSystem) });
        SubscribeLocalEvent<HologramJobSpawnComponent, ContainerSpawnEvent>(OnContainerSpawn);
        SubscribeLocalEvent<HologramBladeServerComponent, PowerChangedEvent>(OnBladePowerChanged);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null || args.Job?.ToString() != HologramJobId)
            return;

        if (!TryFindOrCreateBlade(args.Station, null, out var bladeServerUid, out var bladeServer, out var brainSlot))
        {
            FailHologramSpawn(args);
            return;
        }

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            Transform(bladeServerUid).Coordinates,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);

        if (!TryComp<HologramBrainChipComponent>(args.SpawnResult.Value, out var brainChip) ||
            !TryComp<MindContainerComponent>(args.SpawnResult.Value, out var mindContainer) ||
            mindContainer.Mind is not { } mindId ||
            !_itemSlots.TryInsert(bladeServerUid, brainSlot, args.SpawnResult.Value, user: null))
        {
            if (args.SpawnResult is { } spawnResult)
                Del(spawnResult);

            FailHologramSpawn(args);
            return;
        }

        SetupInstalledChip(bladeServerUid, bladeServer, args.SpawnResult.Value, brainChip, mindId, args.HumanoidCharacterProfile);
    }

    private void FailHologramSpawn(PlayerSpawningEvent args)
        // Claim the spawn so the normal latejoin path does not place the hologram brain chip at arrivals.
        // Hologram jobs require mapped HologramJobRack infrastructure; if none exists, leave the player as an observer.
         => args.SpawnResult = Spawn(FailedSpawnPrototype, MapCoordinates.Nullspace);

    private void OnContainerSpawn(EntityUid uid, HologramJobSpawnComponent component, ref ContainerSpawnEvent args)
    {
        if (!TryComp<HologramBladeServerComponent>(uid, out var bladeServer))
            return;

        if (!TryComp<HologramBrainChipComponent>(args.Player, out var brainChip))
            return;

        if (!TryComp<MindContainerComponent>(args.Player, out var mindContainer) || mindContainer.Mind is not { } mindId)
            return;

        SetupInstalledChip(uid, bladeServer, args.Player, brainChip, mindId, null);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _strayInstallAccumulator += frameTime;
        if (_strayInstallAccumulator < StrayInstallInterval)
            return;

        _strayInstallAccumulator = 0f;

        // Defensive fallback for admin/runtime testing: if something still spawns a hologram
        // job chip loose on the station, put it into a newly-created job blade instead.
        // Only the Hologram job is eligible; scanned/ghost-role chips should not be stolen.
        var query = EntityQueryEnumerator<HologramBrainChipComponent, MindContainerComponent>();
        while (query.MoveNext(out var chip, out var brainChip, out var mindContainer))
        {
            if (_container.IsEntityInContainer(chip))
                continue;

            if (mindContainer.Mind is not { } mindId)
                continue;

            if (!_job.MindTryGetJob(mindId, out var jobPrototype) || jobPrototype.ID.ToString() != HologramJobId)
                continue;

            TryInstallStrayJobChip(chip, brainChip, mindId);
        }
    }

    private bool TryInstallStrayJobChip(EntityUid chip, HologramBrainChipComponent brainChip, EntityUid mindId)
        => TryInstallStrayJobChip(chip, brainChip, mindId, sameGridOnly: true) ||
           TryInstallStrayJobChip(chip, brainChip, mindId, sameGridOnly: false);

    private bool TryInstallStrayJobChip(EntityUid chip, HologramBrainChipComponent brainChip, EntityUid mindId, bool sameGridOnly)
    {
        var chipGrid = GetEffectiveGridUid(chip);

        if (!TryFindOrCreateBlade(null, sameGridOnly ? chipGrid : null, out var bladeServerUid, out var bladeServer, out var brainSlot))
            return false;

        if (!_itemSlots.TryInsert(bladeServerUid, brainSlot, chip, user: null))
            return false;

        SetupInstalledChip(bladeServerUid, bladeServer, chip, brainChip, mindId, null);
        return true;
    }

    private bool TryFindOrCreateBlade(
        EntityUid? station,
        EntityUid? grid,
        out EntityUid bladeServerUid,
        out HologramBladeServerComponent bladeServer,
        out ItemSlot brainSlot)
    {
        if (TryFindFreeBlade(station, grid, preferActiveBody: true, out bladeServerUid, out bladeServer, out brainSlot))
            return true;

        if (TryFindFreeBlade(station, grid, preferActiveBody: false, out bladeServerUid, out bladeServer, out brainSlot))
            return true;

        return TryCreateBladeInRack(station, grid, out bladeServerUid, out bladeServer, out brainSlot);
    }

    private bool TryFindFreeBlade(
        EntityUid? station,
        EntityUid? grid,
        bool preferActiveBody,
        out EntityUid bladeServerUid,
        out HologramBladeServerComponent bladeServer,
        out ItemSlot brainSlot)
    {
        bladeServerUid = default;
        bladeServer = default!;
        brainSlot = default!;

        var query = EntityQueryEnumerator<HologramBladeServerComponent, HologramJobSpawnComponent, ItemSlotsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bladeComp, out _, out var slots, out var xform))
        {
            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            if (grid != null && GetEffectiveGridUid(uid) != grid)
                continue;

            if (!_itemSlots.TryGetSlot(uid, bladeComp.BrainChipSlot, out var slot, slots))
                continue;

            if (slot.Item != null)
                continue;

            var hasActiveBody = bladeComp.ActiveHologram is { } active && Exists(active);
            if (preferActiveBody != hasActiveBody)
                continue;

            bladeServerUid = uid;
            bladeServer = bladeComp;
            brainSlot = slot;
            return true;
        }

        return false;
    }

    private bool TryCreateBladeInRack(
        EntityUid? station,
        EntityUid? grid,
        out EntityUid bladeServerUid,
        out HologramBladeServerComponent bladeServer,
        out ItemSlot brainSlot)
    {
        bladeServerUid = default;
        bladeServer = default!;
        brainSlot = default!;

        var rackQuery = EntityQueryEnumerator<HologramJobRackComponent, ItemSlotsComponent, BladeServerRackComponent, TransformComponent>();
        while (rackQuery.MoveNext(out var rackUid, out _, out var rackSlots, out var rack, out var xform))
        {
            if (station != null && _station.GetOwningStation(rackUid, xform) != station)
                continue;

            if (grid != null && GetEffectiveGridUid(rackUid) != grid)
                continue;

            for (var i = 0; i < rack.BladeSlots.Count; i++)
            {
                var slot = rack.BladeSlots[i];

                if (slot.Item != null)
                    continue;

                var blade = Spawn(JobBladePrototype, xform.Coordinates);
                var slotId = $"{rack.BladeSlotNamePrefix}-{i}";

                if (!_itemSlots.TryInsert(rackUid, slotId, blade, user: null, itemSlots: rackSlots))
                {
                    Del(blade);
                    continue;
                }

                if (!TryComp<HologramBladeServerComponent>(blade, out var bladeComp) ||
                    !TryComp<ItemSlotsComponent>(blade, out var bladeSlots) ||
                    !_itemSlots.TryGetSlot(blade, bladeComp.BrainChipSlot, out var chipSlot, bladeSlots))
                {
                    Del(blade);
                    continue;
                }

                bladeServerUid = blade;
                bladeServer = bladeComp;
                brainSlot = chipSlot;
                return true;
            }
        }

        return false;
    }

    private EntityUid? GetEffectiveGridUid(EntityUid uid)
    {
        var current = uid;

        while (Exists(current))
        {
            var xform = Transform(current);
            if (xform.GridUid is { } grid)
                return grid;

            if (xform.ParentUid == EntityUid.Invalid || xform.ParentUid == current)
                return null;

            current = xform.ParentUid;
        }

        return null;
    }

    private void SetupInstalledChip(
        EntityUid bladeServerUid,
        HologramBladeServerComponent bladeServer,
        EntityUid brainChipUid,
        HologramBrainChipComponent brainChip,
        EntityUid mindId,
        HumanoidCharacterProfile? profile)
    {
        brainChip.HoloMind = mindId;
        brainChip.IsPowered = IsBladeServerPowered(bladeServerUid);

        profile ??= ResolveProfileForMind(mindId);

        EnsureBodyChip(bladeServerUid, bladeServer, mindId, profile);
        _bladeLaws.ApplyBladeLaws(bladeServerUid, bladeServer, brainChipUid);

        // If latejoin enters a blade whose body was already projected as an empty/autonomous shell,
        // put the joining mind directly into that active projection.
        if (bladeServer.ActiveHologram is { } activeBody &&
            Exists(activeBody) &&
            TryComp<MindContainerComponent>(activeBody, out var activeMind) &&
            activeMind.Mind == null)
        {
            _mind.TransferTo(mindId, activeBody, ghostCheckOverride: true);
            _hologram.PrepareHardlightBody(activeBody);
            _bladeLaws.ApplyBladeLaws(bladeServerUid, bladeServer, activeBody);
        }
    }

    private void EnsureBodyChip(EntityUid bladeServerUid, HologramBladeServerComponent bladeServer, EntityUid mindId, HumanoidCharacterProfile? profile)
    {
        if (!TryComp<ItemSlotsComponent>(bladeServerUid, out var slots))
            return;

        if (!_itemSlots.TryGetSlot(bladeServerUid, bladeServer.BodyChipSlot, out var bodySlot, slots))
            return;

        EntityUid chip;
        if (bodySlot.Item is { } existing)
        {
            chip = existing;
        }
        else
        {
            chip = Spawn("HologramJobBodyChip", Transform(bladeServerUid).Coordinates);
            if (!_itemSlots.TryInsert(bladeServerUid, bodySlot, chip, user: null))
            {
                Del(chip);
                return;
            }
        }

        NameBodyChip(chip, mindId, profile);
    }

    private void NameBodyChip(EntityUid bodyChip, EntityUid mindId, HumanoidCharacterProfile? profile)
    {
        if (!TryComp<HologramBodyChipComponent>(bodyChip, out var bodyComp))
            return;

        bodyComp.HologramPrototype ??= HologramSystem.DefaultHologramPrototype;

        if (profile != null)
        {
            bodyComp.HologramProfile = profile;
            bodyComp.HologramName = profile.Name;

            // This is the key for Sparlight / characterforceprototype / forceproto style profiles:
            // the body chip stores the forced mob prototype, so the projection body becomes that shape.
            if (!string.IsNullOrWhiteSpace(profile.ForcedPrototype))
                bodyComp.HologramPrototype = new EntProtoId(profile.ForcedPrototype);
        }

        if (!string.IsNullOrWhiteSpace(bodyComp.HologramName))
            return;

        if (TryComp<MindComponent>(mindId, out var mind))
            bodyComp.HologramName = mind.CharacterName;
    }

    private HumanoidCharacterProfile? ResolveProfileForMind(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return null;

        var prefs = _prefs.GetPreferences(mind.UserId.Value);

        if (!string.IsNullOrWhiteSpace(mind.CharacterName))
        {
            foreach (var profile in prefs.Characters.Values)
            {
                if (profile is HumanoidCharacterProfile humanoid && humanoid.Name == mind.CharacterName)
                    return humanoid;
            }
        }

        if (_job.MindTryGetJob(mindId, out var jobPrototype))
            return prefs.SelectProfileForJob(jobPrototype.ID);

        return prefs.GetRandomEnabledProfile();
    }

    private bool TryGetContainingRack(EntityUid uid, out EntityUid rackUid, out BladeServerRackComponent rack)
    {
        var current = uid;

        while (Exists(current))
        {
            var xform = Transform(current);
            if (xform.ParentUid == EntityUid.Invalid || xform.ParentUid == current)
                break;

            current = xform.ParentUid;

            if (!TryComp<BladeServerRackComponent>(current, out var rackComp))
                continue;

            rackUid = current;
            rack = rackComp;
            return true;
        }

        rackUid = default;
        rack = default!;
        return false;
    }

    private bool IsBladeServerPowered(EntityUid bladeServerUid)
    {
        if (TryGetContainingRack(bladeServerUid, out var rackUid, out var rackComp))
        {
            if (!TryComp<ApcPowerReceiverComponent>(rackUid, out var rackPower) || !rackPower.Powered)
                return false;

            foreach (var slot in rackComp.BladeSlots)
            {
                if (slot.Item == bladeServerUid)
                    return slot.IsPowerEnabled;
            }

            return false;
        }

        return TryComp<ApcPowerReceiverComponent>(bladeServerUid, out var ownPower) && ownPower.Powered;
    }

    private void OnBladePowerChanged(EntityUid uid, HologramBladeServerComponent component, ref PowerChangedEvent args)
        => RefreshBladePower(uid, component);

    private void RefreshBladePower(EntityUid uid, HologramBladeServerComponent component)
    {
        component.IsPowered = IsBladeServerPowered(uid);
        _bladeLaws.SyncBladeLawsToOccupants(uid, component);
    }
}
