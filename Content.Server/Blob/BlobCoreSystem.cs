using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Alert;
using Content.Shared.Blob;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Server.Audio;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;
using Robust.Server.GameObjects;

namespace Content.Server.Blob;

public sealed partial class BlobCoreSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private BlobObserverSystem _blobObserver = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private MapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobCoreComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobCoreComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BlobCoreComponent, DamageChangedEvent>(OnDamaged);
    }

    /// <summary>
    /// Creates the player blob controller
    /// </summary>
    public bool CreateBlobObserver(EntityUid blobCoreUid, NetUserId userId, BlobCoreComponent? core = null)
    {
        var xform = Transform(blobCoreUid);

        if (!Resolve(blobCoreUid, ref core))
            return false;

        var blobRule = EntityQuery<BlobRuleComponent>().FirstOrDefault();

        if (blobRule == null)
        {
            _gameTicker.StartGameRule("Blob", out var ruleEntity);
            blobRule = Comp<BlobRuleComponent>(ruleEntity);
        }

        var observer = Spawn(core.ObserverBlobPrototype, xform.Coordinates);
        core.Observer = observer;

        if (!TryComp<BlobObserverComponent>(observer, out var blobObserverComponent))
            return false;

        blobObserverComponent.Core = blobCoreUid;

        if (!_mindSystem.TryGetMind(userId, out var mindId, out var mind) || mindId == null)
            return false;

        var mindUid = mindId.Value;

        _mindSystem.TransferTo(mindUid, observer);

        _alerts.ShowAlert(observer, "BlobHealth",
            (short)Math.Clamp(Math.Round(core.CoreBlobTotalHealth.Float() / 10f), 0, 20));

        EnsureComp<BlobRoleComponent>(mindUid);

        SendBlobBriefing(mindUid);

        blobRule.Blobs.Add(mindUid);

        if (mind.UserId != null)
        {
            var session = _playerManager.GetSessionById(mind.UserId.Value);
            if (session != null)
                _audioSystem.PlayGlobal(core.GreetSoundNotification, session);
        }

        _blobObserver.UpdateUi(observer, blobObserverComponent);

        return true;
    }

    private void SendBlobBriefing(EntityUid mindUid)
    {
        if (!TryComp<MindComponent>(mindUid, out var mind) || mind.UserId == null)
            return;

        var session = _playerManager.GetSessionById(mind.UserId.Value);
        if (session != null)
            _chatManager.DispatchServerMessage(session, Loc.GetString("blob-role-greeting"));
    }

    private void OnDamaged(EntityUid uid, BlobCoreComponent component, DamageChangedEvent args)
    {
        var maxHealth = component.CoreBlobTotalHealth;
        var currentHealth = maxHealth - args.Damageable.TotalDamage;

        if (component.Observer != null)
        {
            _alerts.ShowAlert(component.Observer.Value, "BlobHealth",
                (short)Math.Clamp(Math.Round(currentHealth.Float() / 10f), 0, 20));
        }
    }

    private void OnStartup(EntityUid uid, BlobCoreComponent component, ComponentStartup args)
    {
        ChangeBlobPoint(uid, 0, component);

        if (TryComp<BlobTileComponent>(uid, out var blobTileComponent))
        {
            blobTileComponent.Core = uid;
            blobTileComponent.Color = component.ChemColors[component.CurrentChem];
            Dirty(uid, blobTileComponent);
        }

        component.BlobTiles.Add(uid);
        ChangeChem(uid, component.DefaultChem, component);
    }

    public void ChangeChem(EntityUid uid, BlobChemType newChem, BlobCoreComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (newChem == component.CurrentChem)
            return;

        var oldChem = component.CurrentChem;
        component.CurrentChem = newChem;

        foreach (var blobTile in component.BlobTiles)
        {
            if (!TryComp<BlobTileComponent>(blobTile, out var blobTileComponent))
                continue;

            blobTileComponent.Color = component.ChemColors[newChem];
            Dirty(blobTile, blobTileComponent);

            ChangeBlobEntChem(blobTile, newChem);
        }
    }

    private void OnDestruction(EntityUid uid, BlobCoreComponent component, DestructionEventArgs args)
    {
        if (component.Observer != null)
            QueueDel(component.Observer.Value);

        foreach (var blobTile in component.BlobTiles)
        {
            if (!TryComp<BlobTileComponent>(blobTile, out var blobTileComponent))
                continue;

            blobTileComponent.Core = null;
            blobTileComponent.Color = Color.White;
            Dirty(blobTile, blobTileComponent);
        }
    }

    private void ChangeBlobEntChem(EntityUid uid, BlobChemType newChem)
    {
        switch (newChem)
        {
            case BlobChemType.ExplosiveLattice:
                _damageable.SetDamageModifierSetId(uid, "ExplosiveLatticeBlob");
                break;
            default:
                _damageable.SetDamageModifierSetId(uid, "BaseBlob");
                break;
        }
    }

    public bool ChangeBlobPoint(EntityUid uid, FixedPoint2 amount, BlobCoreComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        component.Points += amount;

        if (component.Observer != null)
        {
            _alerts.ShowAlert(component.Observer.Value, "BlobResource",
                (short)Math.Clamp(Math.Round(component.Points.Float() / 10f), 0, 16));
        }

        return true;
    }

    public bool TryUseAbility(EntityUid uid, EntityUid coreUid, BlobCoreComponent component, FixedPoint2 abilityCost)
    {
        if (component.Points < abilityCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-not-enough-resources"), uid, uid, PopupType.Large);
            return false;
        }

        ChangeBlobPoint(coreUid, -abilityCost, component);
        return true;
    }
    public bool TransformBlobTile(
        EntityUid? oldTile,
        EntityUid coreUid,
        string prototype,
        EntityCoordinates target,
        BlobCoreComponent _,
        FixedPoint2 transformCost)
    {
        if (!TryComp(coreUid, out BlobCoreComponent? core))
            return false;

        // Deduct cost (safety check in case caller forgot)
        if (!TryUseAbility(coreUid, coreUid, core, transformCost))
            return false;

        // Delete old tile if needed
        if (oldTile.HasValue && Exists(oldTile.Value))
            QueueDel(oldTile.Value);

        // Spawn new blob tile
        var newTile = Spawn(prototype, target);

        if (!TryComp(newTile, out BlobTileComponent? blobTile))
            return false;

        blobTile.Core = coreUid;
        blobTile.Color = core.ChemColors[core.CurrentChem];
        Dirty(newTile, blobTile);

        core.BlobTiles.Add(newTile);

        return true;
    }

    public bool CheckNearNode(
        EntityUid _,
        EntityCoordinates coords,
        EntityUid gridUid,
        MapGridComponent grid,
        BlobCoreComponent component)
    {
        var radius = component.NodeRadiusLimit;

        var tiles = _mapSystem.GetLocalTilesIntersecting(
            gridUid,
            grid,
            new Box2(
                coords.Position + new Vector2(-radius, -radius),
                coords.Position + new Vector2(radius, radius)),
            false).ToArray();

        foreach (var tile in tiles)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid, grid, tile.GridIndices))
            {
                if (HasComp<BlobNodeComponent>(ent))
                    return true;
            }
        }

        return false;
    }
}
