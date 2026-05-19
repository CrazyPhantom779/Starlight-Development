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
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared._Starlight.Holograms.Systems;
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
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed partial class HologramSystem : SharedHologramSystem
{
    public static readonly EntProtoId DefaultHologramPrototype = "MobHologramHardlight";

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private GrammarSystem _grammar = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private AccessSystem _access = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private OutfitSystem _outfit = default!;

    public readonly Dictionary<EntityUid, EntityUid> HologramsWaitingForMind = [];

    public override void DoKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return;

        RemoveWaitingProjection(hologram);

        var meta = MetaData(hologram);
        var holoPos = Transform(hologram).Coordinates;

        _audio.PlayPvs(holoComp.OffSound, hologram);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDisappearOther, ("name", meta.EntityName)), holoPos, Filter.PvsExcept(hologram), false, PopupType.MediumCaution);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDeathSelf), holoPos, hologram, PopupType.LargeCaution);

        QueueDel(hologram);
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

        if (!TryGetProjectingMind(mindId, out var mind, out var client))
            return false;

        var prototype = bodyChip?.HologramPrototype ?? DefaultHologramPrototype;
        var mob = Spawn(prototype, coords);

        _transform.AttachToGridOrMap(mob);
        PrepareHardlightBody(mob);

        var profile = TryApplyHumanoidProfile(mob, mindId, mind, bodyChip);
        ApplyProjectedName(mob, mind, profile, bodyChip);

        FinishProjection(mindId, mob, coords, promptConsent, client);

        if (profile != null && HasComp<HumanoidAppearanceComponent>(mob))
            ApplyHumanoidJobData(mindId, mob);

        holo = mob;
        return true;
    }

    public bool TryGenerateHumanoidHologram(
        EntityUid mindId,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
        => TryGenerateHologram(mindId, null, coords, out holo, promptConsent);

    public bool TryGenerateHumanoidHologram(
        EntityUid mindId,
        HologramBodyChipComponent? bodyChip,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
        => TryGenerateHologram(mindId, bodyChip, coords, out holo, promptConsent);

    public bool TryGeneratePrototypeHologram(
        EntityUid mindId,
        HologramBodyChipComponent bodyChip,
        EntityCoordinates coords,
        [NotNullWhen(true)] out EntityUid? holo,
        bool promptConsent = false)
        => TryGenerateHologram(mindId, bodyChip, coords, out holo, promptConsent);

    public EntityUid SpawnAutonomousHologram(HologramBodyChipComponent bodyChip, EntityCoordinates coords)
    {
        var prototype = bodyChip.HologramPrototype ?? DefaultHologramPrototype;
        var mob = Spawn(prototype, coords);

        _transform.AttachToGridOrMap(mob);
        PrepareHardlightBody(mob);
        TryApplyBodyChipProfile(mob, bodyChip);

        if (!string.IsNullOrWhiteSpace(bodyChip.HologramName))
            _meta.SetEntityName(mob, bodyChip.HologramName);
        else if (bodyChip.HologramProfile != null)
            _meta.SetEntityName(mob, bodyChip.HologramProfile.Name);

        return mob;
    }

    public bool TryReturnMindToBrainChip(EntityUid hologram, EntityUid brainChip)
    {
        if (!TryComp<MindContainerComponent>(hologram, out var hologramMind) || hologramMind.Mind is not { } mindId)
            return false;

        EnsureComp<MindContainerComponent>(brainChip);

        _mind.UnVisit(mindId);
        _mind.TransferTo(mindId, brainChip, ghostCheckOverride: true);

        if (TryComp<HologramBrainChipComponent>(brainChip, out var brainChipComp))
            brainChipComp.HoloMind = mindId;

        if (TryComp<MindComponent>(mindId, out var mind) && !string.IsNullOrWhiteSpace(mind.CharacterName))
            _meta.SetEntityName(brainChip, mind.CharacterName);

        return true;
    }

    internal void TransferMindToHologram(EntityUid mindId)
    {
        if (!HologramsWaitingForMind.TryGetValue(mindId, out var entity))
            return;

        if (!Exists(entity) ||
            !TryComp<MindContainerComponent>(entity, out var mindComp) ||
            mindComp.Mind != null)
        {
            HologramsWaitingForMind.Remove(mindId);
            return;
        }

        _mind.UnVisit(mindId);
        _mind.TransferTo(mindId, entity, ghostCheckOverride: true);
        HologramsWaitingForMind.Remove(mindId);
    }

    public void PrepareHardlightBody(EntityUid mob)
    {
        EnsureComp<HologramComponent>(mob);

        var projected = EnsureComp<HologramProjectedComponent>(mob);
        ApplyDefaultProjectionSettings(mob, projected);

        EnsureComp<MindContainerComponent>(mob);

        RemCompDeferred<RespiratorComponent>(mob);
        RemCompDeferred<BloodstreamComponent>(mob);

        if (!TryComp<GrammarComponent>(mob, out var grammar))
            return;

        _grammar.SetProperNoun((mob, grammar), true);
        _grammar.SetGender((mob, grammar), Gender.Neuter);
    }

    private bool TryGetProjectingMind(
        EntityUid mindId,
        [NotNullWhen(true)] out MindComponent? mind,
        [NotNullWhen(true)] out ICommonSession? client)
    {
        mind = null;
        client = null;

        if (!TryComp(mindId, out mind))
            return false;

        if (!CanProjectMind(mindId, mind))
            return false;

        if (mind.UserId == null || !_playerManager.TryGetSessionById(mind.UserId.Value, out client))
            return false;

        return true;
    }

    private bool CanProjectMind(EntityUid mindId, MindComponent mind)
    {
        if (HologramsWaitingForMind.TryGetValue(mindId, out var clone))
        {
            if (Exists(clone) &&
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

    private HumanoidCharacterProfile? TryApplyHumanoidProfile(
        EntityUid mob,
        EntityUid mindId,
        MindComponent mind,
        HologramBodyChipComponent? bodyChip)
    {
        if (!TryComp<HumanoidAppearanceComponent>(mob, out _))
            return null;

        var profile = GetProfileForProjection(mindId, mind, bodyChip);
        if (profile == null)
            return null;

        _humanoid.LoadProfile(mob, profile);
        _meta.SetEntityName(mob, profile.Name);
        return profile;
    }

    private void TryApplyBodyChipProfile(EntityUid mob, HologramBodyChipComponent bodyChip)
    {
        if (bodyChip.HologramProfile == null)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(mob, out _))
            return;

        _humanoid.LoadProfile(mob, bodyChip.HologramProfile);
        _meta.SetEntityName(mob, bodyChip.HologramProfile.Name);
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
        {
            var jobProfile = prefs.SelectProfileForJob(jobPrototype.ID);
            if (jobProfile != null)
                return jobProfile;
        }

        return prefs.GetRandomEnabledProfile();
    }

    private void ApplyProjectedName(
        EntityUid mob,
        MindComponent mind,
        HumanoidCharacterProfile? profile,
        HologramBodyChipComponent? bodyChip)
    {
        var name = bodyChip?.HologramName;

        if (string.IsNullOrWhiteSpace(name) || name == "hologram")
            name = mind.CharacterName;

        if (string.IsNullOrWhiteSpace(name))
            name = profile?.Name;

        if (string.IsNullOrWhiteSpace(name))
            return;

        _meta.SetEntityName(mob, name);

        bodyChip?.HologramName = name;
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

    private void RemoveWaitingProjection(EntityUid hologram)
    {
        EntityUid? mindToRemove = null;

        foreach (var (mind, waitingHologram) in HologramsWaitingForMind)
        {
            if (waitingHologram != hologram)
                continue;

            mindToRemove = mind;
            break;
        }

        if (mindToRemove is { } removedMind)
            HologramsWaitingForMind.Remove(removedMind);
    }

    private void ApplyDefaultProjectionSettings(EntityUid mob, HologramProjectedComponent projected)
    {
        projected.GracePeriod = TimeSpan.FromSeconds(2);
        projected.ValidationInterval = TimeSpan.FromSeconds(0.05);
        projected.SetEyeTarget = false;
        projected.EffectPrototype ??= "EffectHologramProjectionBeam";

        projected.ValidProjectorWhitelist = new EntityWhitelist
        {
            Tags =
            [
                "HoloProjectorServer",
                "HoloProjectorCamera",
            ],
        };

        Dirty(mob, projected);
    }
}
