using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.Voxel3D;

/// <summary>Draws the finished 3D picture over the game view, below the interface.</summary>
public sealed class Voxel3DOverlay : Robust.Client.Graphics.Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <summary>The latest picture, or null to draw nothing.</summary>
    public Texture? Frame;

    public Voxel3DOverlay()
        => ZIndex = 100;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Frame == null)
            return;

        var bounds = args.ViewportBounds;
        args.ScreenHandle.DrawTextureRect(Frame, new UIBox2(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }
}
