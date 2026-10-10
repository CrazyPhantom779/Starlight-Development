// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.Numerics;

namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>The ground. Asked for the color at a point on the floor plane (y = 0), or says there is none (open space).</summary>
public interface IFloor
{
    bool TryGetColor(float x, float z, out Rgba color);
}

/// <summary>A floor made of one-meter tiles, each showing a picture (row 0 of the picture is the tile's north edge).</summary>
public sealed class GridFloor : IFloor
{
    private readonly Dictionary<(int, int), RgbaImage> _tiles = new();

    public void Set(int tileX, int tileZ, RgbaImage? picture)
    {
        if (picture is null) _tiles.Remove((tileX, tileZ));
        else _tiles[(tileX, tileZ)] = picture;
    }

    public int Count => _tiles.Count;

    public bool TryGetColor(float x, float z, out Rgba color)
    {
        int tx = (int)MathF.Floor(x), tz = (int)MathF.Floor(z);
        if (!_tiles.TryGetValue((tx, tz), out var pic))
        {
            color = default;
            return false;
        }

        int col = Math.Clamp((int)((x - tx) * pic.Width), 0, pic.Width - 1);
        int row = Math.Clamp((int)((1f - (z - tz)) * pic.Height), 0, pic.Height - 1);
        color = pic[col, row];
        color = color with { A = 255 };
        return true;
    }
}

/// <summary>A model placed in the world. Change the properties, then call <see cref="Scene.Update"/>.</summary>
public sealed class Instance
{
    public required VoxelModel Model { get; set; }

    /// <summary>The thing's ground point in the world, in meters.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Rotation around the vertical axis, counter-clockwise seen from above; 0 faces south.</summary>
    public float Yaw { get; set; }

    public bool Visible { get; set; } = true;

    public object? Tag { get; set; }

    internal int Slot = -1;
    internal float Cos = 1f, Sin;
    internal List<long>? Cells;
}

/// <summary>Everything that can be seen. Instances are filed into one-meter buckets so rays only look where they pass.</summary>
public sealed class Scene
{
    private readonly List<Instance?> _slots = new();
    private readonly Stack<int> _free = new();
    private readonly Dictionary<long, List<Instance>> _buckets = new();
    private Vector3 _toSun = Vector3.Normalize(new Vector3(0.35f, 1f, 0.25f));

    public IFloor? Floor { get; set; }

    public Rgba SkyTop { get; set; } = new(24, 28, 52, 255);
    public Rgba SkyBottom { get; set; } = new(8, 8, 16, 255);

    /// <summary>Distant things fade toward this color.</summary>
    public Rgba Fog { get; set; } = new(10, 10, 20, 255);

    /// <summary>Light that reaches every face, from 0 to 1.</summary>
    public float Ambient { get; set; } = 0.45f;

    /// <summary>Which way the light shines (pointing from the light toward the ground).</summary>
    public Vector3 SunDirection
    {
        get => -_toSun;
        set => _toSun = Vector3.Normalize(-value);
    }

    internal Vector3 ToSun => _toSun;

    internal int SlotCount => _slots.Count;

    public int InstanceCount => _slots.Count - _free.Count;

    public Instance Add(VoxelModel model, Vector3 position, float yaw)
    {
        var inst = new Instance { Model = model, Position = position, Yaw = yaw };
        if (_free.Count > 0)
        {
            inst.Slot = _free.Pop();
            _slots[inst.Slot] = inst;
        }
        else
        {
            inst.Slot = _slots.Count;
            _slots.Add(inst);
        }

        File(inst);
        return inst;
    }

    public void Remove(Instance inst)
    {
        if (inst.Slot < 0) return;
        Unfile(inst);
        _slots[inst.Slot] = null;
        _free.Push(inst.Slot);
        inst.Slot = -1;
    }

    /// <summary>Call after changing an instance's model, position or yaw.</summary>
    public void Update(Instance inst)
    {
        if (inst.Slot < 0) return;
        Unfile(inst);
        File(inst);
    }

    internal bool TryGetBucket(long key, out List<Instance>? list) => _buckets.TryGetValue(key, out list);

    internal static long Key(int bx, int bz) => ((long)bx << 32) | (uint)bz;

    private void File(Instance inst)
    {
        inst.Cos = MathF.Cos(inst.Yaw);
        inst.Sin = MathF.Sin(inst.Yaw);

        // Footprint of the rotated model on the ground.
        var m = inst.Model;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            float lx = (i & 1) == 0 ? m.Min.X : m.Max.X;
            float lz = (i & 2) == 0 ? m.Min.Z : m.Max.Z;
            float wx = inst.Position.X + lx * inst.Cos - lz * inst.Sin;
            float wz = inst.Position.Z + lx * inst.Sin + lz * inst.Cos;
            minX = MathF.Min(minX, wx); maxX = MathF.Max(maxX, wx);
            minZ = MathF.Min(minZ, wz); maxZ = MathF.Max(maxZ, wz);
        }

        int bx0 = (int)MathF.Floor(minX), bx1 = (int)MathF.Floor(maxX);
        int bz0 = (int)MathF.Floor(minZ), bz1 = (int)MathF.Floor(maxZ);
        inst.Cells = new List<long>((bx1 - bx0 + 1) * (bz1 - bz0 + 1));
        for (int bx = bx0; bx <= bx1; bx++)
        {
            for (int bz = bz0; bz <= bz1; bz++)
            {
                long key = Key(bx, bz);
                if (!_buckets.TryGetValue(key, out var list))
                    _buckets[key] = list = new List<Instance>(2);
                list.Add(inst);
                inst.Cells.Add(key);
            }
        }
    }

    private void Unfile(Instance inst)
    {
        if (inst.Cells is null) return;
        foreach (var key in inst.Cells)
        {
            if (!_buckets.TryGetValue(key, out var list)) continue;
            list.Remove(inst);
            if (list.Count == 0) _buckets.Remove(key);
        }
        inst.Cells = null;
    }
}
