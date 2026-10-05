using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Blob;
using Content.Shared.CombatMode;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Player;

namespace Content.Server.Blob.NPC.BlobPod;

public sealed partial class BlobPodSystem : EntitySystem
{
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private PopupSystem _popups = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;

    [SubscribeLocalEvent]
    private void OnDestruction(EntityUid uid, BlobPodComponent component, DestructionEventArgs args)
    {
        if (!TryComp<BlobCoreComponent>(component.Core, out var blobCoreComponent))
            return;
        if (blobCoreComponent.CurrentChem == BlobChemType.ExplosiveLattice)
        {
            _explosionSystem.QueueExplosion(uid, blobCoreComponent.BlobExplosive, 4, 1, 2, maxTileBreak: 0);
        }
    }

    [SubscribeLocalEvent]
    private void AddDrainVerb(EntityUid uid, BlobPodComponent component, GetVerbsEvent<InnateVerb> args)
    {
        if (args.User == args.Target)
            return;
        if (!args.CanAccess)
            return;
        if (!HasComp<HumanoidAppearanceComponent>(args.Target))
            return;
        if (_mobs.IsAlive(args.Target))
            return;

        InnateVerb verb = new()
        {
            Act = () =>
            NpcStartZombify(uid, args.Target, component),
            Text = Loc.GetString("blob-pod-verb-zombify"),
            // Icon = new SpriteSpecifier.Texture(new ("/Textures/")),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    [SubscribeLocalEvent]
    private void OnZombify(EntityUid uid, BlobPodComponent component, BlobPodZombifyDoAfterEvent args)
    {
        component.IsZombifying = false;
        if (args.Handled || args.Args.Target == null)
        {
            if (component.ZombifyStingStream != null)
            {
                _audioSystem.Stop(component.ZombifyStingStream);
                component.ZombifyStingStream = null;
            }
            return;
        }

        if (args.Cancelled)
        {
            return;
        }

        _inventory.TryGetSlotEntity(args.Args.Target.Value, "head", out var headItem);
        if (HasComp<BlobMobComponent>(headItem))
            return;

        _inventory.TryUnequip(args.Args.Target.Value, "head", true, true);
        var equipped = _inventory.TryEquip(args.Args.Target.Value, uid, "head", true, true);

        if (!equipped)
            return;

        _popups.PopupEntity(Loc.GetString("blob-mob-zombify-second-end", ("pod", uid)), args.Args.Target.Value, args.Args.Target.Value, Shared.Popups.PopupType.LargeCaution);
        _popups.PopupEntity(Loc.GetString("blob-mob-zombify-third-end", ("pod", uid), ("target", args.Args.Target.Value)), args.Args.Target.Value, Filter.PvsExcept(args.Args.Target.Value), true, Shared.Popups.PopupType.LargeCaution);

        RemComp<CombatModeComponent>(uid);

        RemComp<HTNComponent>(uid);

        EnsureComp<UnremoveableComponent>(uid);

        _audioSystem.PlayPvs(component.ZombifyFinishSoundPath, uid);

        var rejEv = new RejuvenateEvent();
        RaiseLocalEvent(args.Args.Target.Value, rejEv);

        component.ZombifiedEntityUid = args.Args.Target.Value;

        var zombieBlob = EnsureComp<ZombieBlobComponent>(args.Args.Target.Value);
        zombieBlob.BlobPodUid = uid;
    }

    public bool NpcStartZombify(EntityUid uid, EntityUid target, BlobPodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;
        if (!HasComp<HumanoidAppearanceComponent>(target))
            return false;
        if (_mobs.IsAlive(target))
            return false;
        if (!_actionBlocker.CanInteract(uid, target))
            return false;

        StartZombify(uid, target, component);
        return true;
    }

    public void StartZombify(EntityUid uid, EntityUid target, BlobPodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.ZombifyTarget = target;

        _popups.PopupEntity(
            Loc.GetString("blob-mob-zombify-second-start", ("pod", uid)),
            target,
            target,
            Shared.Popups.PopupType.LargeCaution);

        _popups.PopupEntity(
            Loc.GetString("blob-mob-zombify-third-start", ("pod", uid), ("target", target)),
            target,
            Filter.PvsExcept(target),
            true,
            Shared.Popups.PopupType.LargeCaution);

        var result = _audioSystem.PlayPvs(component.ZombifySoundPath, target);
        if (result != null)
            component.ZombifyStingStream = result.Value.Entity;

        component.IsZombifying = true;

        var ev = new BlobPodZombifyDoAfterEvent();

        var args = new DoAfterArgs(EntityManager, uid, component.ZombifyDelay, ev, uid, target: target)
        {
            BreakOnMove = true,
            DistanceThreshold = 2f,
            NeedHand = false
        };

        _doAfter.TryStartDoAfter(args);
    }
}
