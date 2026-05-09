using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Database;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Holograms;

public partial class SharedHologramSystem
{
    private void InitializeProjected() =>
        SubscribeLocalEvent<HologramComponent, EntityStorageInsertedIntoAttemptEvent>(OnStoreInContainerAttempt);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = _entityManager.EntityQueryEnumerator<HologramProjectedComponent>();
        while (query.MoveNext(out var hologram, out var hologramProjectedComp))
        {
            if (_entityManager.IsClientSide(hologram))
                continue;

            ProjectedUpdate(hologram, hologramProjectedComp);
        }
    }

    /// <summary>
    ///     Returns a hologram to its last visited projector, or kills it if the projector is invalid.
    /// </summary>
    public virtual void DoReturnHologram(EntityUid hologram, HologramProjectedComponent? holoProjectedComp = null)
    {
        if (!Resolve(hologram, ref holoProjectedComp))
            return;

        if (Terminating(hologram))
            return;

        EntityUid? curProjectorEntity = null;
        if (holoProjectedComp.CurProjector is { } curProjectorNet)
            TryGetEntity(curProjectorNet, out curProjectorEntity);

        if (!IsHoloProjectorValid(hologram, curProjectorEntity, false) &&
            !TryGetHoloProjector(hologram, out curProjectorEntity, holoProjectedComp, false))
        {
            TryKillHologram(hologram);
            return;
        }

        if (curProjectorEntity is not { } projector)
            return;

        holoProjectedComp.CurProjector = GetNetEntity(projector);
        Dirty(hologram, holoProjectedComp);

        var returnedEvent = new HologramReturnAttemptEvent();
        RaiseLocalEvent(hologram, ref returnedEvent);
        if (returnedEvent.Cancelled)
            return;

        RaiseLocalEvent(hologram, new HologramReturnedEvent(projector));
        MoveHologramToProjector(hologram, projector);

        _adminLogger.Add(LogType.Mind, LogImpact.Low,
            $"{ToPrettyString(hologram):mob} was returned to projector {ToPrettyString(projector):entity}");
    }

    /// <summary>
    ///     Tests for the nearest projector to a set of coords.
    /// </summary>
    public bool TryGetHoloProjector(MapCoordinates coords, [NotNullWhen(true)] out EntityUid? result, EntityWhitelist? whiteList = null, bool occlude = true)
    {
        result = null;
        var nearProjList = new List<(float Distance, EntityUid Projector)>();

        var query = _entityManager.EntityQueryEnumerator<HologramProjectorComponent>();
        while (query.MoveNext(out var projector, out var projComp))
        {
            if (!projComp.IsActive)
                continue;

            var dist = (_transform.GetWorldPosition(projector) - coords.Position).LengthSquared();
            nearProjList.Add((dist, projector));
        }

        nearProjList.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        foreach (var (Distance, Projector) in nearProjList)
        {
            if (!IsHoloProjectorValid(coords, Projector, occlude, whiteList))
                continue;

            result = Projector;
            return true;
        }

        return false;
    }

    /// <remarks>
    ///     This takes into consideration any ProjectorOverride the hologram may have.
    /// </remarks>
    public bool TryGetHoloProjector(EntityUid uid, [NotNullWhen(true)] out EntityUid? result, HologramProjectedComponent? projectedComp = null, bool occlude = true)
    {
        result = null;

        if (!Resolve(uid, ref projectedComp))
            return false;

        if (projectedComp.ProjectorOverride is { } overrideNet)
        {
            if (TryGetEntity(overrideNet, out var overrideEntity) &&
                IsHoloProjectorValid(uid, overrideEntity, occlude))
            {
                result = overrideEntity;
                return true;
            }

            return false;
        }

        var projectorEvent = new HologramGetProjectorEvent();
        RaiseLocalEvent(uid, ref projectorEvent);
        if (projectorEvent.Override)
        {
            result = projectorEvent.ProjectorOverride;
            return result != null;
        }

        return TryGetHoloProjector(_transform.GetMapCoordinates(uid), out result, projectedComp.ValidProjectorWhitelist, occlude);
    }

    /// <summary>
    ///     Tests if a projector is valid for a given hologram.
    /// </summary>
    public bool IsHoloProjectorValid(EntityUid hologram, [NotNullWhen(true)] EntityUid? projector, bool occlude = true, bool raiseEvent = true, HologramProjectedComponent? projectedComp = null)
    {
        if (!Resolve(hologram, ref projectedComp) || projector is not { } projectorUid || !Exists(projectorUid))
            return false;

        if (raiseEvent)
        {
            var validCheckEvent = new HologramCheckProjectorValidEvent(projectorUid);
            RaiseLocalEvent(hologram, ref validCheckEvent);
            if (validCheckEvent.Valid is { } valid)
                return valid;
        }

        return IsHoloProjectorValid(_transform.GetMapCoordinates(hologram), projectorUid, occlude, projectedComp.ValidProjectorWhitelist);
    }

    /// <inheritdoc cref="IsHoloProjectorValid(EntityUid, EntityUid?, bool, bool, HologramProjectedComponent?)"/>
    public bool IsHoloProjectorValid(MapCoordinates hologram, [NotNullWhen(true)] EntityUid? projector, bool occlude = true, EntityWhitelist? whitelist = null)
    {
        if (projector is not { } projectorUid || !Exists(projectorUid))
            return false;

        if (!TryComp(projectorUid, out HologramProjectorComponent? projComp))
            return false;

        if (!projComp.IsActive)
            return false;

        if (whitelist != null && !_whitelist.IsValid(whitelist, projectorUid))
            return false;

        var range = projComp.ProjectorRange;

        if (occlude && !_examine.InRangeUnOccluded(hologram, _transform.ToMapCoordinates(Transform(projectorUid).Coordinates), range, null))
            return false;

        return true;
    }

    /// <summary>
    ///     Moves a hologram to a new location.
    /// </summary>
    public void MoveHologram(EntityUid hologram, EntityCoordinates projector, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return;

        if (TryComp(hologram, out PullableComponent? pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(hologram, pullable);

        if (TryComp(hologram, out PullerComponent? pulling) &&
            pulling.Pulling is { } pullingEntity &&
            TryComp(pullingEntity, out PullableComponent? subjectPulling))
            _pulling.TryStopPull(pullingEntity, subjectPulling);

        var meta = MetaData(hologram);

        if (!_timing.InPrediction)
        {
            var holoPos = Transform(hologram).Coordinates;
            _audio.PlayPvs(holoComp.OffSound, hologram);
            _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDisappearOther, ("name", meta.EntityName)), holoPos, Filter.PvsExcept(hologram), false, PopupType.MediumCaution);
        }

        _transform.SetCoordinates(hologram, projector);
        _transform.AttachToGridOrMap(hologram);

        if (!_timing.InPrediction)
        {
            _audio.PlayPvs(holoComp.OnSound, hologram);
            _popup.PopupEntity(Loc.GetString(holoComp.PopupAppearOther, ("name", meta.EntityName)), hologram, Filter.PvsExcept(hologram), false, PopupType.Medium);
            _popup.PopupEntity(Loc.GetString(holoComp.PopupAppearSelf, ("name", meta.EntityName)), hologram, hologram, PopupType.Large);
        }
    }

    /// <inheritdoc cref="MoveHologram"/>
    public void MoveHologramToProjector(EntityUid hologram, EntityUid projector, HologramComponent? holoComp = null) =>
        MoveHologram(hologram, Transform(projector).Coordinates, holoComp);

    protected bool ProjectedUpdate(EntityUid hologram, HologramProjectedComponent hologramProjectedComp)
    {
        if (TryGetHoloProjector(hologram, out var nearProj, hologramProjectedComp) && nearProj is { } projector)
        {
            hologramProjectedComp.CurProjector = GetNetEntity(projector);
            hologramProjectedComp.CurrentlyInProjector = true;
            Dirty(hologram, hologramProjectedComp);
            return true;
        }

        if (hologramProjectedComp.CurrentlyInProjector)
        {
            hologramProjectedComp.CurrentlyInProjector = false;
            hologramProjectedComp.VanishTime = _timing.CurTime + hologramProjectedComp.GracePeriod;
        }

        if (hologramProjectedComp.VanishTime > _timing.CurTime)
        {
            Dirty(hologram, hologramProjectedComp);
            return true;
        }

        DoReturnHologram(hologram);
        Dirty(hologram, hologramProjectedComp);
        return false;
    }

    private void OnStoreInContainerAttempt(EntityUid uid, HologramComponent component, ref EntityStorageInsertedIntoAttemptEvent args)
    {
        if (Terminating(uid))
            return;

        if (!HasComp<HologramProjectedComponent>(uid))
            return;

        DoReturnHologram(uid);
        args.Cancelled = true;
    }
}
