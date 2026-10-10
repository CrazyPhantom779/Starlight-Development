// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.Linq;
using System.Numerics;

namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>Runs <c>body(0) .. body(bands - 1)</c>, in any order and possibly at the same time, and returns when all are done.</summary>
public delegate void BandRunner(int bands, Action<int> body);

public sealed class RenderOptions
{
    /// <summary>How many horizontal bands the picture is cut into. Bands are what get handed to the runner.</summary>
    public int Bands { get; init; } = 16;

    /// <summary>How the bands are run. Null runs them one after another on the calling thread.</summary>
    public BandRunner? Runner { get; init; }
}

/// <summary>Scratch memory a renderer reuses between frames. Keep one per renderer and pass it every time.</summary>
public sealed class RenderContext
{
    internal int[][] Stamps = [];
    internal int[] RayIds = [];

    internal void Prepare(int bands, int slots)
    {
        if (Stamps.Length != bands)
        {
            Stamps = new int[bands][];
            RayIds = new int[bands];
        }

        for (int i = 0; i < bands; i++)
        {
            if (Stamps[i] is null || Stamps[i].Length < slots)
            {
                Stamps[i] = new int[slots];
                RayIds[i] = 0;
            }
        }
    }
}

/// <summary>
/// Draws a <see cref="Scene"/> by casting one ray per pixel: through the one-meter buckets the ray passes over, into each
/// model's voxel grid, and onto the floor. Software only, so it runs anywhere and every pixel is reproducible.
/// </summary>
public static class RayRenderer
{
    public static void Render(Scene scene, Camera camera, int width, int height, Rgba[] pixels,
        RenderOptions? options = null, RenderContext? context = null)
    {
        if (pixels.Length < width * height) throw new ArgumentException("Pixel buffer is too small.", nameof(pixels));

        camera.Basis(out var forward, out var right, out var up);
        float tanHalf = MathF.Tan(camera.FovY / 2f);
        float aspect = width / (float)height;
        Vector3 origin = camera.Position;

        int bands = Math.Clamp(options?.Bands ?? 16, 1, height);
        context ??= new RenderContext();
        context.Prepare(bands, scene.SlotCount);

        void RenderBand(int band)
        {
            int[] stamp = context.Stamps[band];
            int rayId = context.RayIds[band];
            int y0 = band * height / bands, y1 = (band + 1) * height / bands;
            for (int y = y0; y < y1; y++)
            {
                float sy = (1f - 2f * (y + 0.5f) / height) * tanHalf;
                for (int x = 0; x < width; x++)
                {
                    float sx = (2f * (x + 0.5f) / width - 1f) * tanHalf * aspect;
                    Vector3 dir = Vector3.Normalize(forward + right * sx + up * sy);
                    pixels[y * width + x] = Trace(scene, camera, origin, dir, stamp, ref rayId);
                }
            }

            context.RayIds[band] = rayId;
        }

        if (options?.Runner is { } runner)
        {
            runner(bands, RenderBand);
        }
        else
        {
            for (int b = 0; b < bands; b++) RenderBand(b);
        }
    }

    public static RgbaImage ToImage(Rgba[] pixels, int width, int height)
    {
        var copy = new Rgba[width * height];
        Array.Copy(pixels, copy, copy.Length);
        return new RgbaImage(width, height, copy);
    }

    private static Rgba Trace(Scene scene, Camera cam, Vector3 o, Vector3 d, int[] stamp, ref int rayId)
    {
        rayId++;
        float far = cam.Far;

        // The floor is the plane y = 0; nothing exists below it.
        float floorT = float.PositiveInfinity;
        if (scene.Floor is not null && d.Y < -1e-6f && o.Y > 0f) floorT = -o.Y / d.Y;
        float limit = MathF.Min(far, floorT);

        float bestT = float.PositiveInfinity;
        Rgba bestColor = default;
        Vector3 bestNormal = Vector3.UnitY;

        // Walk the one-meter buckets the ray passes over, nearest first.
        float cell = 1f;
        int cx = (int)MathF.Floor(o.X / cell), cz = (int)MathF.Floor(o.Z / cell);
        int stepX = d.X > 0 ? 1 : d.X < 0 ? -1 : 0;
        int stepZ = d.Z > 0 ? 1 : d.Z < 0 ? -1 : 0;
        float tDeltaX = stepX != 0 ? cell / MathF.Abs(d.X) : float.PositiveInfinity;
        float tDeltaZ = stepZ != 0 ? cell / MathF.Abs(d.Z) : float.PositiveInfinity;
        float tMaxX = stepX > 0 ? ((cx + 1) * cell - o.X) / d.X : stepX < 0 ? (cx * cell - o.X) / d.X : float.PositiveInfinity;
        float tMaxZ = stepZ > 0 ? ((cz + 1) * cell - o.Z) / d.Z : stepZ < 0 ? (cz * cell - o.Z) / d.Z : float.PositiveInfinity;

        for (int guard = 0; guard < 4096; guard++)
        {
            if (scene.TryGetBucket(Scene.Key(cx, cz), out var list))
            {
                foreach (var inst in list!)
                {
                    if (stamp[inst.Slot] == rayId) continue;
                    stamp[inst.Slot] = rayId;
                    if (!inst.Visible) continue;

                    if (IntersectInstance(inst, o, d, cam.Near, MathF.Min(limit, bestT), out float t, out Vector3 normal, out Rgba color))
                    {
                        bestT = t;
                        bestColor = color;
                        bestNormal = normal;
                    }
                }
            }

            float tExit = MathF.Min(tMaxX, tMaxZ);
            if (bestT <= tExit || tExit > limit) break;
            if (tMaxX < tMaxZ) { cx += stepX; tMaxX += tDeltaX; }
            else { cz += stepZ; tMaxZ += tDeltaZ; }
        }

        if (floorT <= far && floorT < bestT && scene.Floor!.TryGetColor(o.X + d.X * floorT, o.Z + d.Z * floorT, out Rgba floorColor))
        {
            bestT = floorT;
            bestColor = floorColor;
            bestNormal = Vector3.UnitY;
        }

        if (float.IsPositiveInfinity(bestT))
            return Sky(scene, d);

        return Shade(scene, bestColor, bestNormal, d, bestT, far);
    }

    private static Rgba Sky(Scene scene, Vector3 d)
    {
        float k = Math.Clamp(d.Y * 0.5f + 0.5f, 0f, 1f);
        return Lerp(scene.SkyBottom, scene.SkyTop, k);
    }

    private static Rgba Shade(Scene scene, Rgba color, Vector3 normal, Vector3 viewDir, float t, float far)
    {
        float diffuse = MathF.Max(0f, Vector3.Dot(normal, scene.ToSun));
        float headlight = MathF.Max(0f, Vector3.Dot(normal, -viewDir)) * 0.15f;
        float light = Math.Clamp(scene.Ambient + (1f - scene.Ambient) * diffuse * 0.9f + headlight, 0f, 1.2f);

        var lit = new Rgba(
            (byte)Math.Min(255f, color.R * light),
            (byte)Math.Min(255f, color.G * light),
            (byte)Math.Min(255f, color.B * light), 255);

        float fog = Math.Clamp(t / far, 0f, 1f);
        return Lerp(lit, scene.Fog, fog * fog);
    }

    private static Rgba Lerp(Rgba a, Rgba b, float k) => new(
        (byte)(a.R + (b.R - a.R) * k),
        (byte)(a.G + (b.G - a.G) * k),
        (byte)(a.B + (b.B - a.B) * k), 255);

    /// <summary>Casts the ray into one model. Returns the nearest solid voxel it hits between tMin and tMax.</summary>
    private static bool IntersectInstance(Instance inst, Vector3 o, Vector3 d, float tMin, float tMax,
        out float t, out Vector3 worldNormal, out Rgba color)
    {
        t = 0; worldNormal = default; color = default;
        var m = inst.Model;
        float c = inst.Cos, s = inst.Sin;

        // Into the model's own frame: move to its ground point, undo its rotation, shift to its minimum corner, count in voxels.
        float px = o.X - inst.Position.X, pz = o.Z - inst.Position.Z;
        float inv = 1f / m.VoxelSize;
        float ox = (px * c + pz * s - m.Min.X) * inv;
        float oz = (-px * s + pz * c - m.Min.Z) * inv;
        float oy = (o.Y - inst.Position.Y - m.Min.Y) * inv;
        float dx = (d.X * c + d.Z * s) * inv;
        float dz = (-d.X * s + d.Z * c) * inv;
        float dy = d.Y * inv;

        // Slab test against the model's box.
        float tEnter = tMin, tExit = tMax;
        int entryAxis = -1;
        if (!Slab(ox, dx, m.SizeX, ref tEnter, ref tExit, 0, ref entryAxis)) return false;
        if (!Slab(oy, dy, m.SizeY, ref tEnter, ref tExit, 1, ref entryAxis)) return false;
        if (!Slab(oz, dz, m.SizeZ, ref tEnter, ref tExit, 2, ref entryAxis)) return false;
        if (tEnter > tExit) return false;

        float sx = ox + dx * tEnter, sy = oy + dy * tEnter, sz = oz + dz * tEnter;
        int ix = Math.Clamp((int)MathF.Floor(sx), 0, m.SizeX - 1);
        int iy = Math.Clamp((int)MathF.Floor(sy), 0, m.SizeY - 1);
        int iz = Math.Clamp((int)MathF.Floor(sz), 0, m.SizeZ - 1);

        int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
        int stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;
        int stepZ = dz > 0 ? 1 : dz < 0 ? -1 : 0;
        float tDeltaX = stepX != 0 ? 1f / MathF.Abs(dx) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? 1f / MathF.Abs(dy) : float.PositiveInfinity;
        float tDeltaZ = stepZ != 0 ? 1f / MathF.Abs(dz) : float.PositiveInfinity;
        float tMaxX = stepX > 0 ? (ix + 1 - ox) / dx : stepX < 0 ? (ix - ox) / dx : float.PositiveInfinity;
        float tMaxY = stepY > 0 ? (iy + 1 - oy) / dy : stepY < 0 ? (iy - oy) / dy : float.PositiveInfinity;
        float tMaxZ = stepZ > 0 ? (iz + 1 - oz) / dz : stepZ < 0 ? (iz - oz) / dz : float.PositiveInfinity;

        // Which face the ray came in through. If it starts inside the box, look at the dominant direction instead.
        Face face;
        if (entryAxis < 0)
        {
            float ax = MathF.Abs(dx), ay = MathF.Abs(dy), az = MathF.Abs(dz);
            entryAxis = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
        }
        face = entryAxis switch
        {
            0 => stepX > 0 ? Face.NegX : Face.PosX,
            1 => stepY > 0 ? Face.NegY : Face.PosY,
            _ => stepZ > 0 ? Face.NegZ : Face.PosZ,
        };

        float tCurrent = tEnter;
        int maxSteps = m.SizeX + m.SizeY + m.SizeZ + 4;
        for (int i = 0; i < maxSteps; i++)
        {
            if (m.IsSolid(ix, iy, iz))
            {
                t = tCurrent;
                color = m.ColorAt(ix, iy, iz, face);
                worldNormal = ToWorldNormal(face, c, s);
                return true;
            }

            // Step into the next voxel along whichever boundary the ray reaches first.
            if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
            {
                tCurrent = tMaxX; tMaxX += tDeltaX; ix += stepX;
                face = stepX > 0 ? Face.NegX : Face.PosX;
            }
            else if (tMaxY <= tMaxZ)
            {
                tCurrent = tMaxY; tMaxY += tDeltaY; iy += stepY;
                face = stepY > 0 ? Face.NegY : Face.PosY;
            }
            else
            {
                tCurrent = tMaxZ; tMaxZ += tDeltaZ; iz += stepZ;
                face = stepZ > 0 ? Face.NegZ : Face.PosZ;
            }

            if (tCurrent > tExit || tCurrent > tMax) return false;
            if ((uint)ix >= (uint)m.SizeX || (uint)iy >= (uint)m.SizeY || (uint)iz >= (uint)m.SizeZ) return false;
        }

        return false;
    }

    private static bool Slab(float o, float d, int size, ref float tEnter, ref float tExit, int axis, ref int entryAxis)
    {
        if (MathF.Abs(d) < 1e-9f)
            return o >= 0f && o <= size;

        float t0 = (0f - o) / d, t1 = (size - o) / d;
        if (t0 > t1) (t0, t1) = (t1, t0);
        if (t0 > tEnter) { tEnter = t0; entryAxis = axis; }
        if (t1 < tExit) tExit = t1;
        return tEnter <= tExit;
    }

    private static Vector3 ToWorldNormal(Face face, float c, float s)
    {
        // Local normal rotated by the instance's yaw (counter-clockwise seen from above).
        return face switch
        {
            Face.NegX => new Vector3(-c, 0, -s),
            Face.PosX => new Vector3(c, 0, s),
            Face.NegY => new Vector3(0, -1, 0),
            Face.PosY => new Vector3(0, 1, 0),
            Face.NegZ => new Vector3(s, 0, -c),
            _ => new Vector3(-s, 0, c),
        };
    }
}
