// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>
/// The pictures a sprite shows when the thing it depicts faces each direction, as SS14 stores them: the "south" picture is
/// the thing seen from the front, "north" from behind, "east" from its right-hand side, "west" from its left-hand side.
/// All pictures share the same height. South and north share a width (the thing's width); east and west share a width
/// (the thing's depth). Only <see cref="South"/> is required.
/// </summary>
public sealed class ViewSet
{
    public required RgbaImage South { get; init; }
    public RgbaImage? North { get; init; }
    public RgbaImage? East { get; init; }
    public RgbaImage? West { get; init; }

    /// <summary>True when the sprite really has more than one direction.</summary>
    public bool HasDirections => North is not null || East is not null || West is not null;
}
