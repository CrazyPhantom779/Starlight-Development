using Content.Server.Blob.NPC.BlobPod;
using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Timing;

namespace Content.Server.Blob;

public sealed partial class BlobFactorySystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;

    [SubscribeLocalEvent]
    private static void OnStartup(EntityUid uid, BlobFactoryComponent component, ComponentStartup args)
    {
    }

    [SubscribeLocalEvent]
    private void OnDestruction(EntityUid uid, BlobFactoryComponent component, DestructionEventArgs args)
    {
        if (TryComp<BlobbernautComponent>(component.Blobbernaut, out var blobbernautComponent))
        {
            blobbernautComponent.Factory = null;
        }
    }

    [SubscribeLocalEvent]
    private void OnProduceBlobbernaut(EntityUid uid, BlobFactoryComponent component, ProduceBlobbernautEvent args)
    {
        if (component.Blobbernaut != null)
            return;

        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out var blobCoreComponent))
            return;

        var xform = Transform(uid);
        var blobbernaut = Spawn(component.BlobbernautId, xform.Coordinates);

        component.Blobbernaut = blobbernaut;

        if (TryComp<BlobbernautComponent>(blobbernaut, out var blobbernautComponent))
        {
            blobbernautComponent.Factory = uid;
            blobbernautComponent.Color = blobCoreComponent.ChemColors[blobCoreComponent.CurrentChem];

            Dirty(blobbernaut, blobbernautComponent, null);
        }

        if (TryComp<MeleeWeaponComponent>(blobbernaut, out var meleeWeaponComponent))
        {
            var blobbernautDamage = new DamageSpecifier();

            foreach (var keyValuePair in blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem].DamageDict)
            {
                blobbernautDamage.DamageDict.Add(keyValuePair.Key, keyValuePair.Value * 0.8f);
            }

            meleeWeaponComponent.Damage = blobbernautDamage;
        }
    }

    [SubscribeLocalEvent]
    private void OnPulsed(EntityUid uid, BlobFactoryComponent component, BlobTileGetPulseEvent args)
    {
        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out _))
            return;

        if (component.SpawnedCount >= component.SpawnLimit)
            return;

        if (_gameTiming.CurTime < component.NextSpawn)
            return;

        var xform = Transform(uid);
        var pod = Spawn(component.Pod, xform.Coordinates);

        component.BlobPods.Add(pod);

        var blobPod = EnsureComp<BlobPodComponent>(pod);
        blobPod.Core = blobTileComponent.Core.Value;

        component.SpawnedCount += 1;
        component.NextSpawn = _gameTiming.CurTime + TimeSpan.FromSeconds(component.SpawnRate);
    }
}
