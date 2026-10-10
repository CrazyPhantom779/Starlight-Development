// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.Numerics;

namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>Which side of a voxel is being looked at.</summary>
public enum Face { NegX, PosX, NegY, PosY, NegZ, PosZ }

/// <summary>
/// A solid voxel model. Axes in the model's own frame, with the thing facing south: X is east, Y is up, Z is north.
/// Voxel (x, y, z) is solid if <see cref="IsSolid"/> says so; its color depends on which face you look at and comes
/// straight from the matching directional picture, so each side of the model shows its own artwork.
/// </summary>
public sealed class VoxelModel
{
    private readonly bool[] _solid;
    private readonly RgbaImage _south, _north, _east, _west; // upright models
    private readonly RgbaImage? _topDown;                     // flat models

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }

    /// <summary>Edge length of one voxel, in meters.</summary>
    public float VoxelSize { get; }

    /// <summary>Position of the model's minimum corner relative to the thing's ground point, in meters.</summary>
    public Vector3 Min { get; }

    public Vector3 Max => Min + new Vector3(SizeX, SizeY, SizeZ) * VoxelSize;

    /// <summary>True for models made from a picture lying on the floor (floor tiles, decals).</summary>
    public bool IsFlat => _topDown is not null;

    public int SolidCount { get; }

    internal VoxelModel(int sx, int sy, int sz, float voxelSize, Vector3 min, bool[] solid,
        RgbaImage south, RgbaImage north, RgbaImage east, RgbaImage west)
    {
        SizeX = sx; SizeY = sy; SizeZ = sz; VoxelSize = voxelSize; Min = min;
        _solid = solid;
        _south = south; _north = north; _east = east; _west = west;
        SolidCount = Count(solid);
    }

    internal VoxelModel(int sx, int sy, int sz, float voxelSize, Vector3 min, bool[] solid, RgbaImage topDown)
    {
        SizeX = sx; SizeY = sy; SizeZ = sz; VoxelSize = voxelSize; Min = min;
        _solid = solid;
        _topDown = topDown;
        _south = _north = _east = _west = topDown;
        SolidCount = Count(solid);
    }

    private static int Count(bool[] solid)
    {
        int n = 0;
        foreach (var b in solid) if (b) n++;
        return n;
    }

    public bool IsSolid(int x, int y, int z) =>
        (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ && _solid[x + SizeX * (y + SizeY * z)];

    /// <summary>The color of one face of a voxel. Only meaningful for solid voxels.</summary>
    public Rgba ColorAt(int x, int y, int z, Face face)
    {
        if (_topDown is not null)
            return _topDown[x, SizeZ - 1 - z];

        int row = SizeY - 1 - y;
        return face switch
        {
            Face.NegZ => _south[x, row],                      // south side: the picture seen from the front
            Face.PosZ => _north[SizeX - 1 - x, row],          // north side: seen from behind
            Face.NegX => _east[SizeZ - 1 - z, row],           // west side: the thing's right side
            Face.PosX => _west[z, row],                       // east side: the thing's left side
            _ => _south[x, row],                              // top and bottom: no picture of their own
        };
    }
}
