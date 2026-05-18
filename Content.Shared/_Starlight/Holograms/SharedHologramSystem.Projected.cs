using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared._Starlight.Holograms.Events;
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
        while (query.MoveNext(out var hologram, out var projected))
        {
            if (_entityManager.IsClientSide(hologram))
                continue;

            ProjectedUpdate(hologram, projected);
        }
    }

    /// <summary>
    /// Returns a hologram to its last valid projector, or kills it if no valid
    /// projector can be found.
    /// </summary>
    public virtual void DoReturnHologram(EntityUid hologram, HologramProjectedComponent? projected = null)
    {
        if (!Resolve(hologram, ref projected))
            return;

        if (Terminating(hologram))
            return;

        var projector = GetStoredProjector(projected);
        if (!IsHoloProjectorValid(hologram, projector, occlude: false) &&
            !TryGetHoloProjector(hologram, out projector, projected, occlude: false))
        {
            TryKillHologram(hologram);
            return;
        }

        if (projector is not { } projectorUid)
            return;

        var returnAttempt = new HologramReturnAttemptEvent();
        RaiseLocalEvent(hologram, ref returnAttempt);
        if (returnAttempt.Cancelled)
            return;

        SetCurrentProjector(hologram, projected, projectorUid);
        RaiseLocalEvent(hologram, new HologramReturnedEvent(projectorUid));
        MoveHologramToProjector(hologram, projectorUid);

        _adminLogger.Add(LogType.Mind, LogImpact.Low,
            $"{ToPrettyString(hologram):mob} was returned to projector {ToPrettyString(projectorUid):entity}");
    }

    /// <summary>
    /// Finds the nearest active projector around map coordinates.
    /// </summary>
    public bool TryGetHoloProjector(
        MapCoordinates coords,
        [NotNullWhen(true)] out EntityUid? result,
        EntityWhitelist? whitelist = null,
        bool occlude = true)
    {
        result = null;
        var candidates = new List<(float Distance, EntityUid Projector)>();

        var query = _entityManager.EntityQueryEnumerator<HologramProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var projector, out var projectorComp, out var projectorXform))
        {
            if (!CanUseProjector(projector, projectorComp, whitelist))
                continue;

            var projectorCoords = _transform.ToMapCoordinates(projectorXform.Coordinates);
            if (projectorCoords.MapId != coords.MapId)
                continue;

            var range = projectorComp.ProjectorRange;
            var distance = (projectorCoords.Position - coords.Position).LengthSquared();

            if (distance > range * range)
                continue;

            candidates.Add((distance, projector));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        foreach (var (_, projector) in candidates)
        {
            if (!IsHoloProjectorValid(coords, projector, occlude, whitelist))
                continue;

            result = projector;
            return true;
        }

        return false;
    }

    /// <remarks>
    /// This takes into consideration any projector override the hologram may have.
    /// </remarks>
    public bool TryGetHoloProjector(
        EntityUid uid,
        [NotNullWhen(true)] out EntityUid? result,
        HologramProjectedComponent? projected = null,
        bool occlude = true)
    {
        result = null;

        if (!Resolve(uid, ref projected))
            return false;

        if (projected.ProjectorOverride is { } overrideNet)
        {
            if (TryGetEntity(overrideNet, out var overrideEntity) &&
                IsHoloProjectorValid(uid, overrideEntity, occlude, projectedComp: projected))
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
            if (projectorEvent.ProjectorOverride is not { } overrideProjector ||
                !IsHoloProjectorValid(uid, overrideProjector, occlude, raiseEvent: false, projected))
            {
                return false;
            }

            result = overrideProjector;
            return true;
        }

        return TryGetHoloProjector(_transform.GetMapCoordinates(uid), out result, projected.ValidProjectorWhitelist, occlude);
    }

    /// <summary>
    /// Tests if a projector is valid for a given hologram.
    /// </summary>
    public bool IsHoloProjectorValid(
        EntityUid hologram,
        [NotNullWhen(true)] EntityUid? projector,
        bool occlude = true,
        bool raiseEvent = true,
        HologramProjectedComponent? projectedComp = null)
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
    public bool IsHoloProjectorValid(
        MapCoordinates hologram,
        [NotNullWhen(true)] EntityUid? projector,
        bool occlude = true,
        EntityWhitelist? whitelist = null)
    {
        if (projector is not { } projectorUid || !Exists(projectorUid))
            return false;

        if (!TryComp(projectorUid, out HologramProjectorComponent? projectorComp))
            return false;

        if (!CanUseProjector(projectorUid, projectorComp, whitelist))
            return false;

        var projectorCoords = _transform.ToMapCoordinates(Transform(projectorUid).Coordinates);
        if (projectorCoords.MapId != hologram.MapId)
            return false;

        if (!occlude)
            return true;

        return _examine.InRangeUnOccluded(hologram, projectorCoords, projectorComp.ProjectorRange, null);
    }

    /// <summary>
    /// Moves a hologram to a new location and forcibly breaks active pulling links.
    /// </summary>
    public void MoveHologram(EntityUid hologram, EntityCoordinates coordinates, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return;

        StopHologramPulling(hologram);

        var meta = MetaData(hologram);

        if (!_timing.InPrediction)
        {
            var oldCoords = Transform(hologram).Coordinates;
            _audio.PlayPvs(holoComp.OffSound, hologram);
            _popup.PopupCoordinates(
                Loc.GetString(holoComp.PopupDisappearOther, ("name", meta.EntityName)),
                oldCoords,
                Filter.PvsExcept(hologram),
                false,
                PopupType.MediumCaution);
        }

        _transform.SetCoordinates(hologram, coordinates);
        _transform.AttachToGridOrMap(hologram);

        if (_timing.InPrediction)
            return;

        _audio.PlayPvs(holoComp.OnSound, hologram);
        _popup.PopupEntity(
            Loc.GetString(holoComp.PopupAppearOther, ("name", meta.EntityName)),
            hologram,
            Filter.PvsExcept(hologram),
            false,
            PopupType.Medium);
        _popup.PopupEntity(
            Loc.GetString(holoComp.PopupAppearSelf, ("name", meta.EntityName)),
            hologram,
            hologram,
            PopupType.Large);
    }

    /// <inheritdoc cref="MoveHologram"/>
    public void MoveHologramToProjector(EntityUid hologram, EntityUid projector, HologramComponent? holoComp = null) =>
        MoveHologram(hologram, Transform(projector).Coordinates, holoComp);

    protected bool ProjectedUpdate(EntityUid hologram, HologramProjectedComponent projected)
    {
        if (_timing.CurTime < projected.NextProjectorCheck)
            return projected.CurrentlyInProjector || projected.VanishTime > _timing.CurTime;

        projected.NextProjectorCheck = _timing.CurTime + projected.ValidationInterval;

        if (TryGetHoloProjector(hologram, out var nearestProjector, projected) &&
            nearestProjector is { } projector)
        {
            SetCurrentProjector(hologram, projected, projector);
            return true;
        }

        if (projected.CurrentlyInProjector)
        {
            projected.CurrentlyInProjector = false;
            projected.VanishTime = _timing.CurTime + projected.GracePeriod;
            Dirty(hologram, projected);
            return true;
        }

        if (projected.VanishTime > _timing.CurTime)
            return true;

        DoReturnHologram(hologram, projected);
        return false;
    }

    private EntityUid? GetStoredProjector(HologramProjectedComponent projected)
    {
        if (projected.CurProjector is not { } projectorNet)
            return null;

        return TryGetEntity(projectorNet, out var projector) ? projector : null;
    }

    private void SetCurrentProjector(EntityUid hologram, HologramProjectedComponent projected, EntityUid projector)
    {
        var projectorNet = GetNetEntity(projector);
        var changed = projected.CurProjector != projectorNet || !projected.CurrentlyInProjector;

        projected.CurProjector = projectorNet;
        projected.CurrentlyInProjector = true;

        if (changed)
            Dirty(hologram, projected);
    }

    private bool CanUseProjector(EntityUid projector, HologramProjectorComponent projectorComp, EntityWhitelist? whitelist)
    {
        if (!projectorComp.IsActive)
            return false;

        return whitelist == null || _whitelist.IsValid(whitelist, projector);
    }

    private void StopHologramPulling(EntityUid hologram)
    {
        if (TryComp(hologram, out PullableComponent? pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(hologram, pullable);

        if (TryComp(hologram, out PullerComponent? pulling) &&
            pulling.Pulling is { } pulledEntity &&
            TryComp(pulledEntity, out PullableComponent? pulled))
        {
            _pulling.TryStopPull(pulledEntity, pulled);
        }
    }

    private void OnStoreInContainerAttempt(EntityUid uid, HologramComponent component, ref EntityStorageInsertedIntoAttemptEvent args)
    {
        if (Terminating(uid))
            return;

        if (!HasComp<HologramProjectedComponent>(uid))
            return;

        args.Cancelled = true;
        DoReturnHologram(uid);
    }
}
