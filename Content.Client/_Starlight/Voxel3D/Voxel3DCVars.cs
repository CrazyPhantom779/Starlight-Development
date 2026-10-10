using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Voxel3D;

/// <summary>Settings for the first-person voxel 3D view. All client side.</summary>
[CVarDefs]
public sealed class Voxel3DCVars
{
    /// <summary>Width, in pixels, of the picture the 3D view draws. It is stretched to fill the screen.</summary>
    public static readonly CVarDef<int> Width =
        CVarDef.Create("voxel3d.width", 320, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> Height =
        CVarDef.Create("voxel3d.height", 180, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Vertical field of view, in degrees.</summary>
    public static readonly CVarDef<float> FovDegrees =
        CVarDef.Create("voxel3d.fov", 75f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How far the 3D view looks, in meters (tiles). Anything farther is not drawn.</summary>
    public static readonly CVarDef<float> ViewDistance =
        CVarDef.Create("voxel3d.view_distance", 24f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Height of the camera above the floor, in meters.</summary>
    public static readonly CVarDef<float> EyeHeight =
        CVarDef.Create("voxel3d.eye_height", 0.8f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Radians of turn per pixel of mouse movement.</summary>
    public static readonly CVarDef<float> MouseSensitivity =
        CVarDef.Create("voxel3d.mouse_sensitivity", 0.0025f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>No sprite gets more voxels than this along any side; bigger ones get coarser voxels.</summary>
    public static readonly CVarDef<int> MaxVoxelsPerAxis =
        CVarDef.Create("voxel3d.max_voxels", 128, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How many converted models are kept. Each distinct look (sprite, direction, animation frame) is one model.</summary>
    public static readonly CVarDef<int> ModelCacheSize =
        CVarDef.Create("voxel3d.model_cache", 3000, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How many strips the picture is cut into for the worker threads.</summary>
    public static readonly CVarDef<int> Bands =
        CVarDef.Create("voxel3d.bands", 16, CVar.CLIENTONLY | CVar.ARCHIVE);
}
