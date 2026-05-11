using System.Diagnostics.CodeAnalysis;
using Content.Server.Access.Systems;
using Content.Server.Body.Components;
using Content.Server.Clothing.Systems;
using Content.Server.Humanoid;
using Content.Server.Jobs;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Access.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed class HologramSystem : SharedHologramSystem
{
    private const string DefaultHologramPrototype = "MobHologramHardlight";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GrammarSystem _grammar = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly AccessSystem _access = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly OutfitSystem _outfit = default!;

    public readonly Dictionary<EntityUid, EntityUid> HologramsWaitingForMind = new();

    public override void DoKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return;

        var meta = MetaData(hologram);
        var holoPos = Transform(hologram).Coordinates;

        _audio.PlayPvs(holoComp.OffSound, hologram);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDisappearOther, ("name", meta.EntityName)), holoPos, Filter.PvsExcept(hologram), false, PopupType.MediumCaution);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDeathSelf), holoPos, hologram, PopupType.LargeCaution);

        _entityManager.QueueDeleteEntity(hologram);

        _adminLogger.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(hologram):mob} was killed!");
    }

    public bool TryGenerateHologram(
        EntityUid mindId,
        HologramBodyChipComponent? bodyChip,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
    {
        holo = null;

        if (bodyChip?.HologramPrototype is { } prototype &&
            !string.Equals(prototype.ToString(), DefaultHologramPrototype, StringComparison.Ordinal))
        {
            return TryGeneratePrototypeHologram(mindId, bodyChip, coords, out holo, promptConsent);
        }

        return TryGenerateHumanoidHologram(mindId, bodyChip, coords, out holo, promptConsent);
    }

    public bool TryGenerateHumanoidHologram(
        EntityUid mindId,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
        => TryGenerateHumanoidHologram(mindId, null, coords, out holo, promptConsent);

    public bool TryGenerateHumanoidHologram(
        EntityUid mindId,
        HologramBodyChipComponent? bodyChip,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
    {
        holo = null;

        if (!TryComp<MindComponent>(mindId, out var mind))
            return false;

        if (!CanProjectMind(mindId, mind))
            return false;

        if (mind.UserId == null || !_playerManager.TryGetSessionById(mind.UserId.Value, out var client))
            return false;

        var pref = GetProfileForProjection(mindId, mind, bodyChip);
        if (pref == null)
            return false;

        EntProtoId mobPrototype = bodyChip?.HologramPrototype ?? DefaultHologramPrototype;

        var mob = HoloFetchAndSpawn(pref, coords, mobPrototype);

        ApplyProjectedName(mob, mind, pref, bodyChip);

        FinishProjection(mindId, mob, coords, promptConsent, client);
        ApplyHumanoidJobData(mindId, mob);

        holo = mob;
        return true;
    }

    public bool TryGeneratePrototypeHologram(
        EntityUid mindId,
        HologramBodyChipComponent bodyChip,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
    {
        holo = null;

        if (bodyChip.HologramPrototype is not { } prototype)
            return false;

        if (!TryComp<MindComponent>(mindId, out var mind))
            return false;

        if (!CanProjectMind(mindId, mind))
            return false;

        if (mind.UserId == null || !_playerManager.TryGetSessionById(mind.UserId.Value, out var client))
            return false;

        var mob = Spawn(prototype, coords);
        _transform.AttachToGridOrMap(mob);

        PrepareHardlightBody(mob);

        ApplyPrototypeProjectedName(mob, mind, bodyChip);
        FinishProjection(mindId, mob, coords, promptConsent, client);

        holo = mob;
        return true;
    }

    public EntityUid SpawnAutonomousHologram(HologramBodyChipComponent bodyChip, EntityCoordinates coords)
    {
        var prototype = bodyChip.HologramPrototype ?? DefaultHologramPrototype;
        var mob = Spawn(prototype, coords);
        _transform.AttachToGridOrMap(mob);

        PrepareHardlightBody(mob);

        if (!string.IsNullOrWhiteSpace(bodyChip.HologramName))
            _meta.SetEntityName(mob, bodyChip.HologramName);

        return mob;
    }

    public bool TryReturnMindToBrainChip(EntityUid hologram, EntityUid brainChip)
    {
        if (!TryComp<MindContainerComponent>(hologram, out var hologramMind) || hologramMind.Mind is not { } mindId)
            return false;

        _mind.UnVisit(mindId);
        _mind.TransferTo(mindId, brainChip, ghostCheckOverride: true);

        if (TryComp<HologramBrainChipComponent>(brainChip, out var brainChipComp))
            brainChipComp.HoloMind = mindId;

        return true;
    }

    internal void TransferMindToHologram(EntityUid mindId)
    {
        if (!HologramsWaitingForMind.TryGetValue(mindId, out var entity) ||
            !EntityManager.EntityExists(entity) ||
            !TryComp<MindContainerComponent>(entity, out var mindComp) ||
            mindComp.Mind != null)
        {
            return;
        }

        _mind.UnVisit(mindId);
        _mind.TransferTo(mindId, entity, ghostCheckOverride: true);
        HologramsWaitingForMind.Remove(mindId);
    }

    public void PrepareHardlightBody(EntityUid mob)
    {
        EnsureComp<HologramComponent>(mob);
        EnsureComp<HologramProjectedComponent>(mob);
        EnsureComp<MindContainerComponent>(mob);

        // These are inherited from biological parents. Remove them in one place so
        // prototype YAML can keep the useful interaction/hands/inventory parents.
        RemCompDeferred<RespiratorComponent>(mob);
        RemCompDeferred<BloodstreamComponent>(mob);

        if (TryComp<GrammarComponent>(mob, out var grammar))
        {
            _grammar.SetProperNoun((mob, grammar), true);
            _grammar.SetGender((mob, grammar), Gender.Neuter);
        }
    }

    private bool CanProjectMind(EntityUid mindId, MindComponent mind)
    {
        if (HologramsWaitingForMind.TryGetValue(mindId, out var clone))
        {
            if (EntityManager.EntityExists(clone) &&
                !_mobState.IsDead(clone) &&
                TryComp<MindContainerComponent>(clone, out var cloneMindComp) &&
                (cloneMindComp.Mind == null || cloneMindComp.Mind == mindId))
            {
                return false;
            }

            HologramsWaitingForMind.Remove(mindId);
        }

        if (mind.OwnedEntity is { } ownedEntity &&
            !HasComp<HologramBrainChipComponent>(ownedEntity) &&
            (_mobState.IsAlive(ownedEntity) || _mobState.IsCritical(ownedEntity)))
        {
            return false;
        }

        return true;
    }

    private void FinishProjection(EntityUid mindId, EntityUid mob, EntityCoordinates coords, bool promptConsent, ICommonSession client)
    {
        PrepareHardlightBody(mob);

        if (promptConsent)
        {
            HologramsWaitingForMind[mindId] = mob;
            _popup.PopupEntity(Loc.GetString("hologram-transfer-consent-request"), mob, client);
        }
        else
        {
            _mind.UnVisit(mindId);
            _mind.TransferTo(mindId, mob, ghostCheckOverride: true);
        }

        var holoComp = EnsureComp<HologramComponent>(mob);
        var meta = MetaData(mob);
        var holoPos = Transform(mob).Coordinates;

        _audio.PlayPvs(holoComp.OnSound, mob);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupAppearOther, ("name", meta.EntityName)), holoPos, Filter.PvsExcept(mob), false, PopupType.Medium);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupAppearSelf), holoPos, mob, PopupType.Large);

        _adminLogger.Add(LogType.Mind, LogImpact.Medium, $"Hologram {ToPrettyString(mob):mob} was generated at {coords}");
    }

    private HumanoidCharacterProfile? GetProfileForProjection(EntityUid mindId, MindComponent mind, HologramBodyChipComponent? bodyChip)
    {
        if (bodyChip?.HologramProfile != null)
            return bodyChip.HologramProfile;

        if (mind.OwnedEntity is { } body &&
            TryComp<HumanoidAppearanceComponent>(body, out var bodyAppearance))
        {
            return _humanoid.GetBaseProfile((body, bodyAppearance));
        }

        if (mind.UserId == null)
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

    private EntityUid HoloFetchAndSpawn(HumanoidCharacterProfile pref, EntityCoordinates coords, EntProtoId mobPrototype)
    {
        var mob = Spawn(mobPrototype, coords);
        _transform.AttachToGridOrMap(mob);

        _humanoid.LoadProfile(mob, pref);
        _meta.SetEntityName(mob, pref.Name);
        PrepareHardlightBody(mob);

        return mob;
    }

    private void ApplyProjectedName(EntityUid mob, MindComponent mind, HumanoidCharacterProfile pref, HologramBodyChipComponent? bodyChip)
    {
        var name = bodyChip?.HologramName;

        if (string.IsNullOrWhiteSpace(name) || name == "hologram")
            name = mind.CharacterName;

        if (string.IsNullOrWhiteSpace(name))
            name = pref.Name;

        _meta.SetEntityName(mob, name);
    }

    private void ApplyPrototypeProjectedName(EntityUid mob, MindComponent mind, HologramBodyChipComponent bodyChip)
    {
        var name = bodyChip.HologramName;

        if (string.IsNullOrWhiteSpace(name) || name == "hologram")
            name = mind.CharacterName;

        if (!string.IsNullOrWhiteSpace(name))
            _meta.SetEntityName(mob, name);
    }

    private void ApplyHumanoidJobData(EntityUid mindId, EntityUid mob)
    {
        if (!_job.MindTryGetJob(mindId, out var jobPrototype))
            return;

        foreach (var special in jobPrototype.Special)
        {
            if (special is AddComponentSpecial addComponent)
                addComponent.AfterEquip(mob);
        }

        var extended = _station.GetOwningStation(mob) is { } station &&
                       TryComp<StationJobsComponent>(station, out var jobComp) &&
                       jobComp.ExtendedAccess;

        if (TryComp<AccessComponent>(mob, out var access))
            _access.SetAccessToJob(mob, jobPrototype, extended, access);

        if (jobPrototype.StartingGear == null)
            return;

        _outfit.SetOutfit(mob, jobPrototype.StartingGear, (_, item) =>
        {
            if (TryComp<ClothingComponent>(item, out var clothing))
            {
                if (clothing.InSlot is "back" or "pocket1" or "pocket2" or "belt" or "suitstorage" or "id")
                {
                    QueueDel(item);
                    return;
                }
            }

            if (!HasComp<HologramComponent>(item))
                AddComp<HologramComponent>(item);
        }, unremovable: true);
    }
}
