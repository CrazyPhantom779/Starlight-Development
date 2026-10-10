using System.Numerics;

namespace Content.Client._Starlight.Voxel3D;

/// <summary>
/// An invisible full-screen control that collects mouse movement for looking around. While mouse look is off it ignores
/// the mouse completely, so the normal interface works. While it is on, it sits on top and takes all mouse input.
/// </summary>
public sealed class Voxel3DMouseControl : Control
{
    private Vector2 _accumulated;

    public Voxel3DMouseControl()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetCapturing(bool capturing)
    {
        MouseFilter = capturing ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
        _accumulated = Vector2.Zero;
        if (capturing)
            SetPositionLast();
    }

    /// <summary>Movement in pixels since the last call.</summary>
    public Vector2 Take()
    {
        var value = _accumulated;
        _accumulated = Vector2.Zero;
        return value;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (MouseFilter != MouseFilterMode.Stop)
            return;

        _accumulated += args.Relative;
        args.Handle();
    }
}
