using System.Diagnostics.CodeAnalysis;
using Content.Server._Starlight.Holograms.Components;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Power;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Installs hologram job minds into mapped hologram job blade racks.
/// Hologram jobs are not normal arrival spawns: the job entity is a brain chip
/// inserted into an existing mapped job rack blade.
/// </summary>
public sealed partial class HologramJobSpawnSystem : EntitySystem
{
    private const string HologramJobId = "Hologram";
    private const string JobBrainPrototype = "HologramBrainChip";
    private const string JobBladePrototype = "HologramJobBladeServer";
    private const string JobBodyChipPrototype = "HologramJobBodyChip";

    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private HologramBladeLawSystem _bladeLaws = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("hologram.job");

        // This is the only place where an unavailable Hologram job is converted
        // into a ghost. It has the player session, so it can use the normal ghost
        // flow and can consume the selected job slot without a station arrival
        // announcement.
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerBeforeSpawn);

        // This must run before SpawnPointSystem. SpawnPointSystem treats a null
        // SpawnResult as "nobody handled this" and will place the job mob at an
        // arrivals/job spawn point.
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: [typeof(SpawnPointSystem)]);

        SubscribeLocalEvent<HologramBladeServerComponent, PowerChangedEvent>(OnBladePowerChanged);
    }

    private void OnPlayerBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Handled || args.JobId != HologramJobId)
            return;

        // Reserve physical capacity up front. Prefer a rack owned by the selected
        // station, but accept any mapped HologramJobRack if the mapper placed it
        // on a normal/auxiliary grid.
        if (TryEnsureAvailableJobBlade(args.Station))
            return;

        LogJobRackDebug(args.Station);
        _sawmill.Warning($"Unable to place Hologram job for {args.Player.Name}: no mapped HologramJobRack with an empty job blade or rack slot was found.");

        // Consume the selected job slot, but do not run normal DoSpawn. That means
        // no station arrival announcement and no arrivals brain-chip fallback.
        if (args.Station is { } station && Exists(station))
            _stationJobs.TryAssignJob(station, HologramJobId, args.Player.UserId);

        // Use the normal observer path. GameTicker will mark the player joined
        // after this event returns because args.Handled is true.
        _gameTicker.SpawnObserver(args.Player);
        args.Handled = true;
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null || !IsHologramJob(args.Job))
            return;

        if (!TryFindOrCreateBlade(args.Station, out var bladeServerUid, out var bladeServer, out var brainSlot))
        {
            // This should only happen if PlayerBeforeSpawnEvent was bypassed or
            // capacity changed after reservation. Never leave SpawnResult null or
            // SpawnPointSystem will spawn the brain chip at arrivals.
            _sawmill.Error("Hologram job reached PlayerSpawningEvent without an available job blade. Falling back to observer spawn to prevent arrivals brain-chip spawn.");
            LogJobRackDebug(args.Station);
            args.SpawnResult = Spawn(GameTicker.ObserverPrototypeName, _gameTicker.GetObserverSpawnPoint());
            return;
        }

        var brainChipUid = Spawn(JobBrainPrototype, Transform(bladeServerUid).Coordinates);

        if (!TryComp<HologramBrainChipComponent>(brainChipUid, out var brainChip) ||
            !TryComp<ItemSlotsComponent>(bladeServerUid, out var bladeSlots) ||
            !_itemSlots.TryGetSlot(bladeServerUid, brainSlot, out var brainItemSlot, bladeSlots) ||
            !TryInsertIntoSlot(brainChipUid, brainItemSlot))
        {
            Del(brainChipUid);

            // Also should not happen after reservation, but this still prevents a
            // null SpawnResult from falling through to normal arrivals spawning.
            _sawmill.Error($"Unable to install Hologram job brain chip into {ToPrettyString(bladeServerUid)}. Falling back to observer spawn.");
            args.SpawnResult = Spawn(GameTicker.ObserverPrototypeName, _gameTicker.GetObserverSpawnPoint());
            return;
        }

        args.SpawnResult = brainChipUid;
        SetupInstalledChip(bladeServerUid, bladeServer, brainChipUid, brainChip, args.HumanoidCharacterProfile);
    }

    /// <summary>
    /// Ensures at least one usable empty job blade exists.
    /// This may create a HologramJobBladeServer inside a mapped HologramJobRack.
    /// </summary>
    private bool TryEnsureAvailableJobBlade(EntityUid? preferredStation)
    {
        if (HasFreeJobBladeInScope(preferredStation))
            return true;

        if (preferredStation != null && HasFreeJobBladeInScope(null))
            return true;

        if (TryCreateBladeInRackInScope(preferredStation, out _, out _, out _))
            return true;

        return preferredStation != null &&
               TryCreateBladeInRackInScope(null, out _, out _, out _);
    }

    private bool TryFindOrCreateBlade(
        EntityUid? preferredStation,
        out EntityUid bladeServerUid,
        out HologramBladeServerComponent bladeServer,
        out string brainSlot)
    {
        if (TryFindFreeBladeInScope(preferredStation, preferActiveBody: true, out bladeServerUid, out bladeServer, out brainSlot))
            return true;

        if (TryFindFreeBladeInScope(preferredStation, preferActiveBody: false, out bladeServerUid, out bladeServer, out brainSlot))
            return true;

        if (preferredStation != null)
        {
            if (TryFindFreeBladeInScope(null, preferActiveBody: true, out bladeServerUid, out bladeServer, out brainSlot))
                return true;

            if (TryFindFreeBladeInScope(null, preferActiveBody: false, out bladeServerUid, out bladeServer, out brainSlot))
                return true;
        }

        if (TryCreateBladeInRackInScope(preferredStation, out bladeServerUid, out bladeServer, out brainSlot))
            return true;

        return preferredStation != null &&
               TryCreateBladeInRackInScope(null, out bladeServerUid, out bladeServer, out brainSlot);
    }

    private bool HasFreeJobBladeInScope(EntityUid? station)
    {
        var query = EntityQueryEnumerator<HologramBladeServerComponent, HologramJobSpawnComponent, ItemSlotsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bladeComp, out _, out var slots, out var xform))
        {
            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            if (!TryGetContainingJobRack(uid, out _, out _))
                continue;

            if (!_itemSlots.TryGetSlot(uid, bladeComp.BrainChipSlot, out var slot, slots))
                continue;

            if (slot.Item == null && slot.ContainerSlot?.ContainedEntity == null)
                return true;
        }

        return false;
    }

    private bool TryFindFreeBladeInScope(
        EntityUid? station,
        bool preferActiveBody,
        out EntityUid bladeServerUid,
        out HologramBladeServerComponent bladeServer,
        out string brainSlot)
    {
        bladeServerUid = default;
        bladeServer = default!;
        brainSlot = string.Empty;

        var query = EntityQueryEnumerator<HologramBladeServerComponent, HologramJobSpawnComponent, ItemSlotsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bladeComp, out _, out var slots, out var xform))
        {
            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            if (!TryGetContainingJobRack(uid, out _, out _))
                continue;

            if (!_itemSlots.TryGetSlot(uid, bladeComp.BrainChipSlot, out var slot, slots))
                continue;

            if (slot.Item != null || slot.ContainerSlot?.ContainedEntity != null)
                continue;

            var hasActiveBody = bladeComp.ActiveHologram is { } active && Exists(active);
            if (preferActiveBody != hasActiveBody)
                continue;

            bladeServerUid = uid;
            bladeServer = bladeComp;
            brainSlot = bladeComp.BrainChipSlot;
            return true;
        }

        return false;
    }

    private bool TryCreateBladeInRackInScope(
        EntityUid? station,
        out EntityUid bladeServerUid,
        out HologramBladeServerComponent bladeServer,
        out string brainSlot)
    {
        bladeServerUid = default;
        bladeServer = default!;
        brainSlot = string.Empty;

        var rackQuery = EntityQueryEnumerator<HologramJobRackComponent, BladeServerRackComponent, TransformComponent>();
        while (rackQuery.MoveNext(out var rackUid, out _, out var rack, out var xform))
        {
            if (station != null && _station.GetOwningStation(rackUid, xform) != station)
                continue;

            if (!TryComp<ItemSlotsComponent>(rackUid, out _))
                continue;

            for (var i = 0; i < rack.BladeSlots.Count; i++)
            {
                var slot = rack.BladeSlots[i];

                if (slot.Item != null || slot.Slot.Item != null || slot.Slot.ContainerSlot?.ContainedEntity != null)
                    continue;

                var blade = Spawn(JobBladePrototype, xform.Coordinates);

                if (!TryInsertIntoSlot(blade, slot.Slot))
                {
                    _sawmill.Warning($"Failed to install generated hologram job blade {ToPrettyString(blade)} into {ToPrettyString(rackUid)} slot {i}.");
                    Del(blade);
                    continue;
                }

                if (!TryComp<HologramBladeServerComponent>(blade, out var bladeComp) ||
                    !TryComp<ItemSlotsComponent>(blade, out var bladeSlots) ||
                    !_itemSlots.TryGetSlot(blade, bladeComp.BrainChipSlot, out _, bladeSlots))
                {
                    Del(blade);
                    continue;
                }

                bladeServerUid = blade;
                bladeServer = bladeComp;
                brainSlot = bladeComp.BrainChipSlot;
                return true;
            }
        }

        return false;
    }

    private bool TryInsertIntoSlot(EntityUid item, ItemSlot slot)
    {
        if (slot.ContainerSlot == null)
            return false;

        return _container.Insert(item, slot.ContainerSlot);
    }

    private void SetupInstalledChip(
        EntityUid bladeServerUid,
        HologramBladeServerComponent bladeServer,
        EntityUid brainChipUid,
        HologramBrainChipComponent brainChip,
        HumanoidCharacterProfile? profile)
    {
        brainChip.IsPowered = IsBladeServerPowered(bladeServerUid);

        EnsureBodyChip(bladeServerUid, bladeServer, profile);
        _bladeLaws.ApplyBladeLaws(bladeServerUid, bladeServer, brainChipUid);
    }

    private void EnsureBodyChip(EntityUid bladeServerUid, HologramBladeServerComponent bladeServer, HumanoidCharacterProfile? profile)
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
            chip = Spawn(JobBodyChipPrototype, Transform(bladeServerUid).Coordinates);
            if (!TryInsertIntoSlot(chip, bodySlot))
            {
                Del(chip);
                return;
            }
        }

        NameBodyChip(chip, profile, null);
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
            chip = Spawn(JobBodyChipPrototype, Transform(bladeServerUid).Coordinates);
            if (!TryInsertIntoSlot(chip, bodySlot))
            {
                Del(chip);
                return;
            }
        }

        NameBodyChip(chip, profile, mindId);
    }

    private void NameBodyChip(EntityUid bodyChip, HumanoidCharacterProfile? profile, EntityUid? mindId)
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

        if (mindId is { } mind && TryComp<MindComponent>(mind, out var mindComp))
            bodyComp.HologramName = mindComp.CharacterName;

        if (string.IsNullOrWhiteSpace(bodyComp.HologramName))
            bodyComp.HologramName = "hologram";
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

        return _job.MindTryGetJob(mindId, out var jobPrototype)
            ? prefs.SelectProfileForJob(jobPrototype.ID)
            : prefs.GetRandomEnabledProfile();
    }

    private bool TryGetContainingJobRack(EntityUid uid, out EntityUid rackUid, [NotNullWhen(true)] out BladeServerRackComponent? rack)
    {
        var current = uid;

        while (Exists(current))
        {
            var xform = Transform(current);
            if (xform.ParentUid == EntityUid.Invalid || xform.ParentUid == current)
                break;

            current = xform.ParentUid;

            if (!TryComp<BladeServerRackComponent>(current, out var rackComp) ||
                !HasComp<HologramJobRackComponent>(current))
            {
                continue;
            }

            rackUid = current;
            rack = rackComp;
            return true;
        }

        rackUid = default;
        rack = null;
        return false;
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

    private void LogJobRackDebug(EntityUid? station)
    {
        var requestedStation = station is { } stationUid ? ToPrettyString(stationUid) : "<any>";
        var count = 0;
        var free = 0;

        var rackQuery = EntityQueryEnumerator<HologramJobRackComponent, BladeServerRackComponent, TransformComponent>();
        while (rackQuery.MoveNext(out var rackUid, out _, out var rack, out var xform))
        {
            count++;

            var owningStation = _station.GetOwningStation(rackUid, xform);
            var owningText = owningStation is { } owningUid ? ToPrettyString(owningUid) : "<none>";
            var freeSlots = 0;

            foreach (var slot in rack.BladeSlots)
            {
                if (slot.Item == null && slot.Slot.Item == null && slot.Slot.ContainerSlot?.ContainedEntity == null)
                    freeSlots++;
            }

            free += freeSlots;
            _sawmill.Info($"Found hologram job rack {ToPrettyString(rackUid)} station={owningText} requestedStation={requestedStation} freeSlots={freeSlots}/{rack.BladeSlots.Count} grid={xform.GridUid}");
        }

        _sawmill.Warning($"Hologram job rack scan complete: racks={count}, freeSlots={free}, requestedStation={requestedStation}.");
    }

    private void OnBladePowerChanged(EntityUid uid, HologramBladeServerComponent component, ref PowerChangedEvent args)
        => RefreshBladePower(uid, component);

    private void RefreshBladePower(EntityUid uid, HologramBladeServerComponent component)
    {
        component.IsPowered = IsBladeServerPowered(uid);
        _bladeLaws.SyncBladeLawsToOccupants(uid, component);
    }

    private static bool IsHologramJob(ProtoId<JobPrototype>? job)
        => job is { Id: HologramJobId };
}
