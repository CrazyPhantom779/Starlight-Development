using Content.Server.Popups;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Damage.Systems;

namespace Content.Server.Blob;

public sealed partial class BlobMobSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;

    [SubscribeLocalEvent]
    private void OnPulsed(EntityUid uid, BlobMobComponent component, BlobMobGetPulseEvent args) =>
        _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);

    [SubscribeLocalEvent]
    private void OnBlobAttackAttempt(EntityUid uid, BlobMobComponent component, AttackAttemptEvent args)
    {
        if (args.Cancelled || (!HasComp<BlobTileComponent>(args.Target) && !HasComp<BlobMobComponent>(args.Target)))
            return;

        // TODO: Move this to shared
        _popupSystem.PopupCursor(Loc.GetString("blob-mob-attack-blob"), uid, PopupType.Large);
        args.Cancel();
    }
}
