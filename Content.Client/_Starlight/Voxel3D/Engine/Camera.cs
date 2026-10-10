// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.Numerics;

namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>
/// A first-person camera. World axes: X east, Y up, Z north. Yaw 0 looks north; a positive yaw turns toward the west
/// (counter-clockwise seen from above). Pitch looks up when positive.
/// </summary>
public sealed class Camera
{
    public Vector3 Position { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    /// <summary>Vertical field of view in radians.</summary>
    public float FovY { get; set; } = 1.2f;

    public float Near { get; set; } = 0.02f;
    public float Far { get; set; } = 24f;

    public void Basis(out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        float cp = MathF.Cos(Pitch), sp = MathF.Sin(Pitch);
        float cy = MathF.Cos(Yaw), sy = MathF.Sin(Yaw);
        forward = new Vector3(-sy * cp, sp, cy * cp);
        right = new Vector3(cy, 0f, sy);
        up = Vector3.Cross(forward, right);
    }
}
