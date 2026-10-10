// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.Linq;
using System.Numerics;

namespace Content.Client._Starlight.Voxel3D.Engine;

public static class ModelBuilder
{
    /// <summary>
    /// Carves a solid out of the directional pictures: a voxel exists only where every picture agrees there is something.
    /// Pictures that are missing are filled in so any sprite works:
    /// north defaults to the mirrored south picture; east and west default to the south picture (the front picture wrapped
    /// round the sides). Axes: X east, Y up, Z north, the thing facing south.
    /// </summary>
    /// <param name="min">Where the model's minimum corner sits relative to the thing's ground point, in meters.</param>
    public static VoxelModel Carve(ViewSet views, float voxelSize, Vector3 min, int alphaThreshold = 128)
    {
        RgbaImage south = views.South;
        RgbaImage north = views.North ?? Mirror(south);
        RgbaImage east = views.East ?? south;
        RgbaImage west = views.West ?? south;

        int sx = south.Width, sy = south.Height, sz = east.Width;
        if (north.Width != sx || north.Height != sy)
            throw new ArgumentException("North and south pictures must be the same size.");
        if (east.Height != sy || west.Height != sy || west.Width != sz)
            throw new ArgumentException("East and west pictures must be the same size and as tall as the south picture.");

        var solid = new bool[checked(sx * sy * sz)];
        for (var z = 0; z < sz; z++)
        {
            for (var y = 0; y < sy; y++)
            {
                var row = sy - 1 - y;
                for (var x = 0; x < sx; x++)
                {
                    if (south[x, row].A < alphaThreshold) continue;
                    if (north[sx - 1 - x, row].A < alphaThreshold) continue;
                    if (east[sz - 1 - z, row].A < alphaThreshold) continue;
                    if (west[z, row].A < alphaThreshold) continue;
                    solid[x + (sx * (y + (sy * z)))] = true;
                }
            }
        }

        return new VoxelModel(sx, sy, sz, voxelSize, min, solid, south, north, east, west);
    }

    /// <summary>
    /// A model from a picture lying on the floor (floor tiles, decals, anything drawn from straight above).
    /// The picture's top edge is north. Thickness is in voxels.
    /// </summary>
    public static VoxelModel TopDown(RgbaImage picture, float voxelSize, Vector3 min, int thickness = 1, int alphaThreshold = 128)
    {
        int sx = picture.Width, sz = picture.Height, sy = Math.Max(1, thickness);
        var solid = new bool[checked(sx * sy * sz)];
        for (var z = 0; z < sz; z++)
        {
            for (var x = 0; x < sx; x++)
            {
                if (picture[x, sz - 1 - z].A < alphaThreshold) continue;
                for (var y = 0; y < sy; y++) solid[x + sx * (y + sy * z)] = true;
            }
        }

        return new VoxelModel(sx, sy, sz, voxelSize, min, solid, picture);
    }

    public static RgbaImage Mirror(RgbaImage image)
    {
        var result = new RgbaImage(image.Width, image.Height);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                result[x, y] = image[image.Width - 1 - x, y];
        return result;
    }
}
