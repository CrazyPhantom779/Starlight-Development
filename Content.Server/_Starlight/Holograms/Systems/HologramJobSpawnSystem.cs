using Content.Server._Starlight.Holograms.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind.Components;
using Content.Shared.Power;
using Content.Shared.Roles.Jobs;
using Content.Shared.Preferences;
using Robust.Shared.Containers;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Installs hologram job minds into mapped/runtime blade servers.
/// The player job entity is the brain chip; projection is left to consoles/projectors.
/// </summary>
public sealed class HologramJobSpawnSystem : EntitySystem
{
    private const string HologramJobId = "Hologram";

    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new []{ typeof(ContainerSpawnPointSystem) });
        SubscribeLocalEvent<HologramJobSpawnComponent, ContainerSpawnEvent>(OnContainerSpawn);
        SubscribeLocalEvent<HologramBladeServerComponent, PowerChangedEvent>(OnBladePowerChanged);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null || args.Job?.ToString() != HologramJobId)
            return;

        if (!TryFindFreeBlade(args.Station, null, out var bladeServerUid, out var bladeServer, out var brainSlot))
            return;

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
            return;
        }

        SetupInstalledChip(bladeServerUid, bladeServer, args.SpawnResult.Value, brainChip, mindId, args.HumanoidCharacterProfile);
    }

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

        // Defensive fallback for admin/runtime testing: if something still spawns the job chip
        // at arrivals, move it into a free hologram blade instead of leaving the player as a chip.
        var query = EntityQueryEnumerator<HologramBrainChipComponent, MindContainerComponent>();
        while (query.MoveNext(out var chip, out var brainChip, out var mindContainer))
        {
            if (_container.IsEntityInContainer(chip))
                continue;

            if (mindContainer.Mind is not { } mindId)
                continue;

            TryInstallStrayJobChip(chip, brainChip, mindId);
        }
    }

    private bool TryInstallStrayJobChip(EntityUid chip, HologramBrainChipComponent brainChip, EntityUid mindId)
    => TryInstallStrayJobChip(chip, brainChip, mindId, sameGridOnly: true) ||
        TryInstallStrayJobChip(chip, brainChip, mindId, sameGridOnly: false);

    private bool TryInstallStrayJobChip(EntityUid chip, HologramBrainChipComponent brainChip, EntityUid mindId, bool sameGridOnly)
    {
        var chipGrid = Transform(chip).GridUid;

        if (!TryFindFreeBlade(null, sameGridOnly ? chipGrid : null, out var bladeServerUid, out var bladeServer, out var brainSlot))
            return false;

        if (!_itemSlots.TryInsert(bladeServerUid, brainSlot, chip, user: null))
            return false;

        SetupInstalledChip(bladeServerUid, bladeServer, chip, brainChip, mindId, null);
        return true;
    }

    private bool TryFindFreeBlade(
        EntityUid? station,
        EntityUid? grid,
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

            if (grid != null && xform.GridUid != grid)
                continue;

            if (!_itemSlots.TryGetSlot(uid, bladeComp.BrainChipSlot, out var slot, slots))
                continue;

            if (slot.Item != null)
                continue;

            bladeServerUid = uid;
            bladeServer = bladeComp;
            brainSlot = slot;
            return true;
        }

        return false;
    }

    private void SetupInstalledChip(
        EntityUid bladeServerUid,
        HologramBladeServerComponent bladeServer,
        EntityUid _,
        HologramBrainChipComponent brainChip,
        EntityUid mindId,
        HumanoidCharacterProfile? profile)
    {
        brainChip.HoloMind = mindId;
        brainChip.IsPowered = IsBladeServerPowered(bladeServerUid);
        profile ??= ResolveProfileForMind(mindId);
        EnsureBodyChip(bladeServerUid, bladeServer, mindId, profile);

        // Do not spawn a hologram here. The mind stays in the chip until projected.
        if (bladeServer.ActiveHologram is { } active && Exists(active))
            ReturnMindAndKill(bladeServerUid, bladeServer, active);

        bladeServer.ActiveHologram = null;
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

        return null;
    }

    private void EnsureBodyChip(EntityUid bladeServerUid, HologramBladeServerComponent bladeServer, EntityUid mindId, HumanoidCharacterProfile? profile)
    {
        if (!TryComp<ItemSlotsComponent>(bladeServerUid, out var slots))
            return;

        if (!_itemSlots.TryGetSlot(bladeServerUid, bladeServer.BodyChipSlot, out var bodySlot, slots))
            return;

        if (bodySlot.Item is { } existing)
        {
            SetupBodyChip(existing, mindId, profile);
            return;
        }

        var chip = Spawn("HologramJobBodyChip", Transform(bladeServerUid).Coordinates);
        if (!_itemSlots.TryInsert(bladeServerUid, bodySlot, chip, user: null))
        {
            Del(chip);
            return;
        }

        SetupBodyChip(chip, mindId, profile);
    }

    private void SetupBodyChip(EntityUid bodyChip, EntityUid mindId, HumanoidCharacterProfile? profile)
    {
        if (!TryComp<HologramBodyChipComponent>(bodyChip, out var bodyComp))
            return;

        if (profile != null)
            bodyComp.HologramProfile = profile;

        if (!string.IsNullOrWhiteSpace(bodyComp.HologramName) && bodyComp.HologramName != "hologram")
            return;

        if (TryComp<MindComponent>(mindId, out var mind))
            bodyComp.HologramName = mind.CharacterName;

        if (string.IsNullOrWhiteSpace(bodyComp.HologramName) && profile != null)
            bodyComp.HologramName = profile.Name;
    }

    private bool IsBladeServerPowered(EntityUid bladeServerUid)
    {
        if (TryComp<HologramBladeServerComponent>(bladeServerUid, out var bladeServer))
            return bladeServer.IsPowered;

        return false;
    }

    private void OnBladePowerChanged(EntityUid uid, HologramBladeServerComponent component, ref PowerChangedEvent args)
    {
        component.IsPowered = args.Powered;

        if (args.Powered)
            return;

        if (component.ActiveHologram is { } hologram && Exists(hologram))
            ReturnMindAndKill(uid, component, hologram);

        component.ActiveHologram = null;
    }

    private void ReturnMindAndKill(EntityUid bladeServerUid, HologramBladeServerComponent bladeServer, EntityUid hologram)
    {
        if (TryGetBrainChip(bladeServerUid, bladeServer, out var brainChip))
            _hologram.TryReturnMindToBrainChip(hologram, brainChip);

        _hologram.DoKillHologram(hologram);
    }

    private bool TryGetBrainChip(EntityUid bladeServerUid, HologramBladeServerComponent bladeServer, out EntityUid brainChip)
    {
        brainChip = default;

        if (!TryComp<ItemSlotsComponent>(bladeServerUid, out var slots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeServerUid, bladeServer.BrainChipSlot, out var brainSlot, slots) || brainSlot.Item is not { } chip)
            return false;

        brainChip = chip;
        return true;
    }
}
