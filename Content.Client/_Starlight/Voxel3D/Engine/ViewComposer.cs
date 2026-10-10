// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.Numerics;

namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>One sprite layer as a game draws it, with a picture per direction.</summary>
public sealed class ComposeLayer
{
    /// <summary>The picture for each facing. Only South is required; missing directions use the south picture.</summary>
    public required RgbaImage South { get; init; }
    public RgbaImage? North { get; init; }
    public RgbaImage? East { get; init; }
    public RgbaImage? West { get; init; }

    /// <summary>Where the layer's center is, in meters, relative to the thing's ground point: X east, Y north (up the picture).</summary>
    public Vector2 Offset { get; init; }

    /// <summary>Scale applied to the picture's natural size. 2 doubles it, 0.25 shrinks it to a quarter.</summary>
    public Vector2 Scale { get; init; } = Vector2.One;

    /// <summary>Counter-clockwise rotation of the picture around its center, in radians.</summary>
    public float Rotation { get; init; }

    public Rgba Tint { get; init; } = Rgba.White;

    public bool Visible { get; init; } = true;
}

public sealed class ComposeOptions
{
    /// <summary>Pixels of a picture that make up one meter (SS14: 32).</summary>
    public float PixelsPerMeter { get; init; } = 32f;

    /// <summary>The model never gets more voxels than this along any axis; bigger sprites get coarser voxels.</summary>
    public int MaxVoxelsPerAxis { get; init; } = 256;

    /// <summary>Alpha (0-255) from which a pixel counts as solid.</summary>
    public int AlphaThreshold { get; init; } = 128;
}

/// <summary>
/// Flattens a sprite's layers into one picture per direction and carves the voxel model from them. One pixel of the
/// finest layer becomes one voxel, so a 64x64 sprite gets 64x64 voxels; a 128x128 sprite scaled down to fit a 64x64 area
/// stays inside that area but gets twice as many, finer, voxels.
/// </summary>
public static class ViewComposer
{
    private enum Dir { South, North, East, West }

    private readonly record struct Placed(ComposeLayer Layer, float Cos, float Sin);

    /// <summary>A standing model: the pictures are seen from the side, like a character or a crate. The bottom edge is the floor.</summary>
    public static VoxelModel BuildUpright(IReadOnlyList<ComposeLayer> layers, ComposeOptions? options = null)
    {
        options ??= new ComposeOptions();
        var placed = Place(layers);
        if (placed.Count == 0) throw new ArgumentException("No visible layers.", nameof(layers));

        Bounds(placed, options, out float uMin, out float uMax, out float vMin, out float vMax);
        float half = Math.Max(Math.Abs(uMin), Math.Abs(uMax));
        float extentU = 2 * half, extentV = vMax - vMin;
        float vs = VoxelSize(placed, options, extentU, extentV);

        int w = Math.Max(1, (int)Math.Ceiling(extentU / vs - 1e-4));
        int h = Math.Max(1, (int)Math.Ceiling(extentV / vs - 1e-4));
        float uOrigin = -w * vs / 2f;

        bool directional = placed.Any(p => p.Layer.North is not null || p.Layer.East is not null || p.Layer.West is not null);
        var views = new ViewSet
        {
            South = Render(placed, Dir.South, w, h, uOrigin, vMin, vs, options),
            North = directional ? Render(placed, Dir.North, w, h, uOrigin, vMin, vs, options) : null,
            East = directional ? Render(placed, Dir.East, w, h, uOrigin, vMin, vs, options) : null,
            West = directional ? Render(placed, Dir.West, w, h, uOrigin, vMin, vs, options) : null,
        };

        // Square footprint: the depth is as many voxels as the width, centered on the ground point.
        return ModelBuilder.Carve(views, vs, new Vector3(-w * vs / 2f, 0f, -w * vs / 2f), options.AlphaThreshold);
    }

    /// <summary>A flat model: the picture is seen from above, like a floor decal. Always one picture; directions don't apply.</summary>
    public static VoxelModel BuildFlat(IReadOnlyList<ComposeLayer> layers, ComposeOptions? options = null, int thickness = 1)
    {
        options ??= new ComposeOptions();
        var placed = Place(layers);
        if (placed.Count == 0) throw new ArgumentException("No visible layers.", nameof(layers));

        Bounds(placed, options, out float uMin, out float uMax, out float vMin, out float vMax);
        float extentU = uMax - uMin, extentV = vMax - vMin;
        float vs = VoxelSize(placed, options, extentU, extentV);

        int w = Math.Max(1, (int)Math.Ceiling(extentU / vs - 1e-4));
        int h = Math.Max(1, (int)Math.Ceiling(extentV / vs - 1e-4));
        var picture = Render(placed, Dir.South, w, h, uMin, vMin, vs, options);
        return ModelBuilder.TopDown(picture, vs, new Vector3(uMin, 0f, vMin), thickness, options.AlphaThreshold);
    }

    private static List<Placed> Place(IReadOnlyList<ComposeLayer> layers)
    {
        var result = new List<Placed>();
        foreach (var l in layers)
        {
            if (!l.Visible || l.Scale.X == 0 || l.Scale.Y == 0) continue;
            result.Add(new Placed(l, MathF.Cos(l.Rotation), MathF.Sin(l.Rotation)));
        }
        return result;
    }

    private static void Bounds(List<Placed> placed, ComposeOptions o, out float uMin, out float uMax, out float vMin, out float vMax)
    {
        uMin = vMin = float.MaxValue;
        uMax = vMax = float.MinValue;
        foreach (var p in placed)
        {
            var l = p.Layer;
            float hw = l.South.Width / o.PixelsPerMeter * MathF.Abs(l.Scale.X) / 2f;
            float hh = l.South.Height / o.PixelsPerMeter * MathF.Abs(l.Scale.Y) / 2f;
            float ex = MathF.Abs(p.Cos) * hw + MathF.Abs(p.Sin) * hh;
            float ey = MathF.Abs(p.Sin) * hw + MathF.Abs(p.Cos) * hh;
            uMin = MathF.Min(uMin, l.Offset.X - ex);
            uMax = MathF.Max(uMax, l.Offset.X + ex);
            vMin = MathF.Min(vMin, l.Offset.Y - ey);
            vMax = MathF.Max(vMax, l.Offset.Y + ey);
        }
    }

    private static float VoxelSize(List<Placed> placed, ComposeOptions o, float extentU, float extentV)
    {
        // The finest layer decides: its pixels become voxels one to one.
        float vs = float.MaxValue;
        foreach (var p in placed)
            vs = MathF.Min(vs, MathF.Min(MathF.Abs(p.Layer.Scale.X), MathF.Abs(p.Layer.Scale.Y)) / o.PixelsPerMeter);
        return MathF.Max(vs, MathF.Max(extentU, extentV) / o.MaxVoxelsPerAxis);
    }

    private static RgbaImage Render(List<Placed> placed, Dir dir, int w, int h, float uOrigin, float vMin, float vs, ComposeOptions o)
    {
        var accR = new float[w * h];
        var accG = new float[w * h];
        var accB = new float[w * h];
        var accA = new float[w * h];

        foreach (var p in placed)
        {
            var l = p.Layer;
            RgbaImage img = dir switch
            {
                Dir.North => l.North ?? l.South,
                Dir.East => l.East ?? l.South,
                Dir.West => l.West ?? l.South,
                _ => l.South,
            };

            float sx = l.Scale.X, sy = l.Scale.Y;
            float halfW = img.Width / 2f, halfH = img.Height / 2f;
            for (int cy = 0; cy < h; cy++)
            {
                float v = vMin + h * vs - (cy + 0.5f) * vs;
                float dv = v - l.Offset.Y;
                for (int cx = 0; cx < w; cx++)
                {
                    float u = uOrigin + (cx + 0.5f) * vs;
                    float du = u - l.Offset.X;

                    // Undo the layer's rotation and scale to find the picture pixel under this canvas pixel.
                    float lx = (du * p.Cos + dv * p.Sin) / sx;
                    float ly = (-du * p.Sin + dv * p.Cos) / sy;
                    int ix = (int)MathF.Floor(lx * o.PixelsPerMeter + halfW + 1e-4f);
                    int iy = (int)MathF.Floor(halfH - ly * o.PixelsPerMeter + 1e-4f);
                    if ((uint)ix >= (uint)img.Width || (uint)iy >= (uint)img.Height) continue;

                    var src = img[ix, iy].Multiply(l.Tint);
                    if (src.A == 0) continue;

                    // "Over" blending, so partly transparent pixels (shadows, glass) layer up sensibly.
                    int i = cy * w + cx;
                    float a = src.A / 255f;
                    float outA = a + accA[i] * (1f - a);
                    accR[i] = (src.R * a + accR[i] * accA[i] * (1f - a)) / outA;
                    accG[i] = (src.G * a + accG[i] * accA[i] * (1f - a)) / outA;
                    accB[i] = (src.B * a + accB[i] * accA[i] * (1f - a)) / outA;
                    accA[i] = outA;
                }
            }
        }

        var result = new RgbaImage(w, h);
        for (int i = 0; i < w * h; i++)
        {
            if (accA[i] <= 0f) continue;
            result.Pixels[i] = new Rgba(
                (byte)Math.Clamp((int)MathF.Round(accR[i]), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(accG[i]), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(accB[i]), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(accA[i] * 255f), 0, 255));
        }
        return result;
    }
}
