using Content.Server._Starlight.Holograms.Components;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Power;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Installs hologram job minds into blade servers.
/// Job racks start empty; a job blade is created only inside an existing mapped HologramJobRack.
/// </summary>
public sealed partial class HologramJobSpawnSystem : EntitySystem
{
    private const string HologramJobId = "Hologram";
    private const string JobBladePrototype = "HologramJobBladeServer";
    [Dependency] private HologramBladeLawSystem _bladeLaws = default!;
    [Dependency] private HologramSystem _hologram = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("hologram.job");

        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning);
        SubscribeLocalEvent<HologramBladeServerComponent, PowerChangedEvent>(OnBladePowerChanged);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null || args.Job?.ToString() != HologramJobId)
            return;

        if (!TryFindOrCreateBlade(args.Station, null, out var bladeServerUid, out var bladeServer, out var brainSlot) &&
            !TryFindOrCreateBlade(null, null, out bladeServerUid, out bladeServer, out brainSlot))
        {
            _sawmill.Warning("Unable to place Hologram job: no mapped HologramJobRack with a free blade slot was found. The Hologram job should be disabled or unmapped on maps without a job rack.");
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

            args.SpawnResult = null;
            _sawmill.Warning($"Unable to install Hologram job mind into {ToPrettyString(bladeServerUid)}.");
            return;
        }

        SetupInstalledChip(bladeServerUid, bladeServer, args.SpawnResult.Value, brainChip, mindId, args.HumanoidCharacterProfile);
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

        if (profile != null)
        {
            bodyComp.HologramProfile = profile;
            bodyComp.HologramName = profile.Name;

            // Forced/custom prototypes are copied into the body chip so the projected body uses that shape.
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

        if (TryComp<ApcPowerReceiverComponent>(bladeServerUid, out var ownPower))
            return ownPower.Powered;

        return false;
    }

    private void OnBladePowerChanged(EntityUid uid, HologramBladeServerComponent component, ref PowerChangedEvent args)
        => RefreshBladePower(uid, component);

    private void RefreshBladePower(EntityUid uid, HologramBladeServerComponent component)
    {
        component.IsPowered = IsBladeServerPowered(uid);
        _bladeLaws.SyncBladeLawsToOccupants(uid, component);
    }
}
