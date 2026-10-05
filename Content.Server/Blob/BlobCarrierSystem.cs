using Content.Server.Actions;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.Blob;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Content.Shared.Mind.Components;

namespace Content.Server.Blob;

public sealed partial class BlobCarrierSystem : EntitySystem
{
    private const string TransformToBlobActionId = "TransformToBlob";

    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    [SubscribeLocalEvent]
    private static void OnMindAdded(EntityUid uid, BlobCarrierComponent component, MindAddedMessage args)
        => component.HasMind = true;

    [SubscribeLocalEvent]
    private static void OnMindRemove(EntityUid uid, BlobCarrierComponent component, MindRemovedMessage args)
        => component.HasMind = false;

    [SubscribeLocalEvent]
    private void OnTransformToBlobChanged(EntityUid uid, BlobCarrierComponent component, TransformToBlobActionEvent args)
        => TransformToBlob(uid, component);

    [SubscribeLocalEvent]
    private void OnStartup(EntityUid uid, BlobCarrierComponent component, ComponentStartup args)
    {
        _action.AddAction(uid, TransformToBlobActionId);

        var ghostRole = EnsureComp<GhostRoleComponent>(uid);
        EnsureComp<GhostTakeoverAvailableComponent>(uid);

        ghostRole.RoleName = Loc.GetString("blob-carrier-role-name");
        ghostRole.RoleDescription = Loc.GetString("blob-carrier-role-desc");
        ghostRole.RoleRules = Loc.GetString("blob-carrier-role-rules");
    }

    [SubscribeLocalEvent]
    private static void OnShutdown(EntityUid uid, BlobCarrierComponent component, ComponentShutdown args)
    {
    }

    [SubscribeLocalEvent]
    private void OnMobStateChanged(EntityUid uid, BlobCarrierComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            TransformToBlob(uid, component);
    }

    private void TransformToBlob(EntityUid uid, BlobCarrierComponent carrier)
    {
        var xform = Transform(uid);
        if (_mind.TryGetMind(uid, out _, out var mind))
        {
            if (mind.UserId == null)
            {
                Spawn(carrier.CoreBlobGhostRolePrototype, xform.Coordinates);
                QueueDel(uid);
                return;
            }

            var core = Spawn(carrier.CoreBlobPrototype, xform.Coordinates);

            if (!TryComp<BlobCoreComponent>(core, out var blobCoreComponent))
                return;

            _blobCoreSystem.CreateBlobObserver(core, mind.UserId.Value, blobCoreComponent);
        }
        else
        {
            Spawn(carrier.CoreBlobGhostRolePrototype, xform.Coordinates);
        }

        QueueDel(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobCarrierComponent>();

        while (query.MoveNext(out var ent, out var comp))
        {
            if (!comp.HasMind)
                continue;

            comp.TransformationTimer += frameTime;

            if (_gameTiming.CurTime < comp.NextAlert)
                continue;

            var remainingTime = Math.Round(comp.TransformationDelay - comp.TransformationTimer, 0);

            _popup.PopupEntity(
                Loc.GetString("carrier-blob-alert", ("second", remainingTime)),
                ent,
                ent,
                PopupType.LargeCaution);

            comp.NextAlert = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.AlertInterval);

            if (comp.TransformationTimer >= comp.TransformationDelay)
                TransformToBlob(ent, comp);
        }
    }
}
