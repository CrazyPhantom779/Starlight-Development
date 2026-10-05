using Content.Shared.Blob;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Client.Blob;

public sealed partial class BlobObserverSystem : SharedBlobObserverSystem
{
    [Dependency] private ILightManager _lightManager = default!;

    [SubscribeLocalEvent]
    private void HandleState(EntityUid uid, BlobObserverComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not BlobChemSwapComponentState state)
            return;
        component.SelectedChemId = state.SelectedChem;
    }

    [SubscribeLocalEvent]
    private void OnPlayerAttached(EntityUid uid, BlobObserverComponent component, PlayerAttachedEvent args)
        => _lightManager.DrawLighting = false;

    [SubscribeLocalEvent]
    private void OnPlayerDetached(EntityUid uid, BlobObserverComponent component, PlayerDetachedEvent args)
        => _lightManager.DrawLighting = true;

    [SubscribeNetworkEvent]
    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
        => _lightManager.DrawLighting = true;
}
