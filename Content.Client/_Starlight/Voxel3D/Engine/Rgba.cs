// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>8-bit-per-channel color, non-premultiplied. Alpha 0 means "nothing here".</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);
    public static readonly Rgba White = new(255, 255, 255, 255);

    public Rgba(byte r, byte g, byte b) : this(r, g, b, 255) { }

    public bool IsTransparent => A == 0;

    /// <summary>Channel-wise multiply (used for tinting). White is the identity.</summary>
    public Rgba Multiply(Rgba other) =>
        new(Mul(R, other.R), Mul(G, other.G), Mul(B, other.B), Mul(A, other.A));

    private static byte Mul(byte a, byte b) => (byte)((a * b + 127) / 255);

    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}{A:x2}";
}
