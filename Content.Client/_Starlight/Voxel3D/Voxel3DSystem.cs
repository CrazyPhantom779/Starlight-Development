using System.Numerics;
using Content.Shared._Starlight.Voxel3D;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using SixLabors.ImageSharp.PixelFormats;
using E = Content.Client._Starlight.Voxel3D.Engine;

namespace Content.Client._Starlight.Voxel3D;

/// <summary>
/// A first-person 3D view of the game. Every sprite is turned into a solid voxel model on demand (one pixel, one voxel,
/// each side of the model taken from that direction's artwork), placed in a 3D scene, and drawn by a software renderer
/// into a texture that covers the game view. Switch it on and off with the "Toggle voxel view" key (F12) or the
/// <c>voxel3d</c> command. Mouse look can be released and recaptured with Shift+F12 or <c>voxel3d_mouse</c>.
/// </summary>
/// <remarks>
/// Only the grid the player stands on is shown, in that grid's own frame, so shuttles that turn don't need special care.
/// Everything here is read-only except the player's own looking direction, which is sent to the server so that walking
/// follows the view.
/// </remarks>
public sealed partial class Voxel3DSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IParallelManager _parallel = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float PixelsPerMeter = 32f;

    /// <summary>Sprites drawn at this depth or below are floor-level clutter (decals, pipes, puddles) and become flat slabs.</summary>
    private const int FloorLevelDrawDepth = -5;

    private const string ToggleCommand = "voxel3d";
    private const string MouseCommand = "voxel3d_mouse";

    private readonly record struct LayerData(
        string PngPath, Vector2i RsiSize, int DirCount, int Frame, Matrix3x2 Matrix, E.Rgba Tint);

    private sealed class Entry
    {
        public E.Instance Instance = default!;
        public ulong Key;
        public int LastSeen;
    }

    /// <summary>Lets the engine's thread pool run the renderer's strips.</summary>
    private sealed class BandJob : IParallelRobustJob
    {
        public Action<int>? Body;

        public void Execute(int index)
        {
            Body?.Invoke(index);
        }
    }

    public bool Active { get; private set; }

    private bool _mouseLook;
    private bool _needInitialYaw;
    private float _yaw;
    private float _pitch;
    private float _sentYaw;
    private TimeSpan _nextLookSend;

    private Voxel3DOverlay? _overlay;
    private Voxel3DMouseControl? _mouseControl;
    private OwnedTexture? _texture;
    private int _textureWidth;
    private int _textureHeight;
    private Rgba32[] _texturePixels = Array.Empty<Rgba32>();
    private E.Rgba[] _pixels = Array.Empty<E.Rgba>();

    private readonly E.Scene _scene = new();
    private readonly E.Camera _camera = new();
    private readonly E.RenderContext _context = new();
    private readonly BandJob _job = new();
    private E.RenderOptions? _renderOptions;

    private readonly Dictionary<EntityUid, Entry> _entries = new();
    private readonly Dictionary<ulong, E.VoxelModel> _models = new();
    private readonly Queue<ulong> _modelOrder = new();
    private readonly HashSet<ulong> _failedModels = new();
    private readonly Dictionary<string, E.RgbaImage> _images = new();
    private readonly HashSet<string> _failedImages = new();
    private readonly Dictionary<(int Type, byte Variant, byte RotMirror), E.RgbaImage?> _tileImages = new();
    private readonly List<LayerData> _layerBuffer = new();
    private readonly List<EntityUid> _staleBuffer = new();
    private int _frame;
    private TimeSpan _nextFloorRefresh;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleVoxelView, InputCmdHandler.FromDelegate(_ => SetActive(!Active)))
            .Bind(ContentKeyFunctions.ToggleVoxelMouseLook, InputCmdHandler.FromDelegate(_ =>
            {
                if (Active)
                    SetMouseLook(!_mouseLook);
            }))
            .Register<Voxel3DSystem>();

        _console.RegisterCommand(ToggleCommand, "Switches the first-person voxel 3D view on or off.", ToggleCommand,
            (shell, _, _) =>
            {
                SetActive(!Active);
                shell.WriteLine(Active ? "3D view on." : "3D view off.");
            });

        _console.RegisterCommand(MouseCommand, "Captures or releases the mouse in the 3D view.", MouseCommand,
            (shell, _, _) =>
            {
                if (!Active)
                {
                    shell.WriteError("The 3D view is not on. Use the voxel3d command first.");
                    return;
                }

                SetMouseLook(!_mouseLook);
                shell.WriteLine(_mouseLook ? "Mouse captured." : "Mouse released.");
            });
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<Voxel3DSystem>();
        _console.UnregisterCommand(ToggleCommand);
        _console.UnregisterCommand(MouseCommand);
        SetActive(false);
        _texture?.Dispose();
        _texture = null;
        _mouseControl?.Orphan();
        _mouseControl = null;
    }

    // ---- switching on and off ---------------------------------------------------------------------------------

    public void SetActive(bool on)
    {
        if (on == Active)
            return;

        if (on)
        {
            if (_player.LocalEntity == null)
                return; // nothing to look through yet (still in the lobby)

            EnsureObjects();
            Active = true;
            _overlays.AddOverlay((Robust.Client.Graphics.Overlay) _overlay!);
            _needInitialYaw = true;
            _pitch = 0f;
            SetMouseLook(true);
            return;
        }

        Active = false;
        SetMouseLook(false);
        if (_overlay != null)
        {
            _overlay.Frame = null;
            _overlays.RemoveOverlay((Robust.Client.Graphics.Overlay) _overlay);
        }

        ClearScene();
        SendLook(0f, false);
        if (_player.LocalEntity is { } self && TryComp(self, out InputMoverComponent? mover))
        {
            mover.TargetRelativeRotation = Angle.Zero;
            mover.RelativeRotation = Angle.Zero;
        }
    }

    private void SetMouseLook(bool on)
    {
        _mouseLook = on;
        _mouseControl?.SetCapturing(on);
        _clyde.MainWindow.SetRelativeMouseMode(on);
    }

    private void EnsureObjects()
    {
        _overlay ??= new Voxel3DOverlay();

        if (_mouseControl == null)
        {
            _mouseControl = new Voxel3DMouseControl();
            _ui.WindowRoot.AddChild(_mouseControl);
            LayoutContainer.SetAnchorPreset(_mouseControl, LayoutContainer.LayoutPreset.Wide);
        }

        _renderOptions ??= new E.RenderOptions { Bands = Math.Max(1, _cfg.GetCVar(Voxel3DCVars.Bands)), Runner = RunBands };
    }

    private void RunBands(int bands, Action<int> body)
    {
        _job.Body = body;
        _parallel.ProcessNow(_job, bands);
        _job.Body = null;
    }

    private void ClearScene()
    {
        foreach (var entry in _entries.Values)
            _scene.Remove(entry.Instance);
        _entries.Clear();
        _scene.Floor = null;
        _nextFloorRefresh = TimeSpan.Zero;
    }

    // ---- every frame ------------------------------------------------------------------------------------------

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!Active || _overlay == null || _mouseControl == null)
            return;

        if (_player.LocalEntity is not { } player)
        {
            _overlay.Frame = null;
            return;
        }

        var xform = Transform(player);
        if (_needInitialYaw)
        {
            // Start by looking where the character already faces (angle 0 faces south, which is yaw 180 degrees).
            _yaw = WrapAngle((float) xform.LocalRotation.Theta - MathF.PI);
            _needInitialYaw = false;
            ApplyLook(player);
        }

        UpdateLook(player);
        BuildScene(player, xform);
        Render();
    }

    private void UpdateLook(EntityUid player)
    {
        if (_mouseLook && _mouseControl != null)
        {
            var sensitivity = _cfg.GetCVar(Voxel3DCVars.MouseSensitivity);
            var movement = _mouseControl.Take();
            _yaw = WrapAngle(_yaw - movement.X * sensitivity);
            _pitch = Math.Clamp(_pitch - movement.Y * sensitivity, -1.5f, 1.5f);
        }

        if (MathF.Abs(WrapAngle(_yaw - _sentYaw)) > 0.005f && _timing.RealTime >= _nextLookSend)
            ApplyLook(player);
    }

    /// <summary>Tells the server where we are looking, and follows it locally too, since walking is predicted here.</summary>
    private void ApplyLook(EntityUid player)
    {
        SendLook(_yaw, true);
        if (TryComp(player, out InputMoverComponent? mover))
        {
            mover.TargetRelativeRotation = new Angle(_yaw);
            mover.RelativeRotation = new Angle(_yaw);
        }
    }

    private void SendLook(float yaw, bool active)
    {
        RaiseNetworkEvent(new Voxel3DLookEvent(yaw, active));
        _sentYaw = yaw;
        _nextLookSend = _timing.RealTime + TimeSpan.FromMilliseconds(50);
    }

    // ---- the scene --------------------------------------------------------------------------------------------

    private void BuildScene(EntityUid player, TransformComponent playerXform)
    {
        var parent = playerXform.ParentUid;
        var position = playerXform.LocalPosition;
        var distance = Math.Max(4f, _cfg.GetCVar(Voxel3DCVars.ViewDistance));

        _camera.Position = new Vector3(position.X, _cfg.GetCVar(Voxel3DCVars.EyeHeight), position.Y);
        _camera.Yaw = _yaw;
        _camera.Pitch = _pitch;
        _camera.FovY = _cfg.GetCVar(Voxel3DCVars.FovDegrees) * MathF.PI / 180f;
        _camera.Far = distance;

        _frame++;
        UpdateFloor(parent, position, distance);
        UpdateEntities(player, parent, position, distance);
    }

    private void UpdateFloor(EntityUid parent, Vector2 position, float distance)
    {
        if (_timing.RealTime < _nextFloorRefresh)
            return;

        _nextFloorRefresh = _timing.RealTime + TimeSpan.FromMilliseconds(250);

        var floor = new E.GridFloor();
        if (TryComp(parent, out MapGridComponent? grid))
        {
            // Tile pictures are laid out one per meter; grids with other tile sizes are not handled.
            var radius = (int) MathF.Ceiling(distance) + 1;
            var centerX = (int) MathF.Floor(position.X);
            var centerY = (int) MathF.Floor(position.Y);
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                for (var y = centerY - radius; y <= centerY + radius; y++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if (dx * dx + dy * dy > radius * radius)
                        continue;

                    var tile = _map.GetTileRef(parent, grid, new Vector2i(x, y));
                    if (tile.Tile.IsEmpty)
                        continue;

                    var picture = TileImage(tile.Tile);
                    if (picture != null)
                        floor.Set(x, y, picture);
                }
            }
        }

        _scene.Floor = floor;
    }

    private void UpdateEntities(EntityUid player, EntityUid parent, Vector2 position, float distance)
    {
        var maxDistanceSquared = (distance + 2f) * (distance + 2f);

        var query = EntityQueryEnumerator<SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var xform))
        {
            if (uid == player || xform.ParentUid != parent)
                continue;

            var p = xform.LocalPosition;
            var dx = p.X - position.X;
            var dy = p.Y - position.Y;
            if (dx * dx + dy * dy > maxDistanceSquared)
                continue;

            if (!sprite.Visible || sprite.ContainerOccluded || sprite.Color.A < 0.01f)
                continue;

            var model = GetModel(sprite, out var key, out var directional);
            if (model == null)
                continue;

            // A thing that never turns on screen (no-rotation sprites) only turns in 3D if it has a picture for each direction.
            var yaw = sprite.NoRotation && !directional ? 0f : (float) xform.LocalRotation.Theta;
            var world = new Vector3(p.X, 0f, p.Y);

            if (!_entries.TryGetValue(uid, out var entry))
            {
                entry = new Entry { Instance = _scene.Add(model, world, yaw), Key = key };
                _entries[uid] = entry;
            }
            else
            {
                var instance = entry.Instance;
                if (entry.Key != key || instance.Position != world || instance.Yaw != yaw)
                {
                    instance.Model = model;
                    instance.Position = world;
                    instance.Yaw = yaw;
                    entry.Key = key;
                    _scene.Update(instance);
                }
            }

            entry.LastSeen = _frame;
        }

        // Things that were not seen this frame are gone, out of range, or hidden.
        _staleBuffer.Clear();
        foreach (var (uid, entry) in _entries)
        {
            if (entry.LastSeen != _frame)
                _staleBuffer.Add(uid);
        }

        foreach (var uid in _staleBuffer)
        {
            _scene.Remove(_entries[uid].Instance);
            _entries.Remove(uid);
        }
    }

    // ---- sprite to model --------------------------------------------------------------------------------------

    /// <summary>
    /// Finds (or builds, the first time) the 3D model for how this sprite looks right now. What it looks like is reduced
    /// to a number, so every distinct look (a different sprite, direction pictures, animation frame or color) is its own
    /// cached model and identical things share one.
    /// </summary>
    private E.VoxelModel? GetModel(SpriteComponent sprite, out ulong key, out bool directional)
    {
        key = 0;
        directional = false;
        _layerBuffer.Clear();

        foreach (var layerInterface in sprite.AllLayers)
        {
            if (!layerInterface.Visible || !layerInterface.RsiState.IsValid)
                continue;

            var rsi = layerInterface.ActualRsi;
            if (rsi == null || !rsi.TryGetState(layerInterface.RsiState, out var state))
                continue;

            var layer = (SpriteComponent.Layer) layerInterface;
            var dirCount = _sprite.LayerGetDirectionCount(layer);

            // The layer's own offset, rotation and scale, plus the sprite's. (Not the entity's turn: that turns the model.)
            layer.GetLayerDrawMatrix(RsiDirection.South, out var layerMatrix);
            var matrix = Matrix3x2.Multiply(layerMatrix, sprite.LocalMatrix);

            var tint = sprite.Color * layerInterface.Color;
            _layerBuffer.Add(new LayerData(
                $"{rsi.Path}/{state.StateId}.png",
                rsi.Size,
                Math.Max(1, dirCount),
                layerInterface.AnimationFrame,
                matrix,
                new E.Rgba(tint.RByte, tint.GByte, tint.BByte, tint.AByte)));

            if (dirCount >= 4)
                directional = true;
        }

        if (_layerBuffer.Count == 0)
            return null;

        var flat = sprite.DrawDepth <= FloorLevelDrawDepth;
        key = HashLayers(flat);

        if (_models.TryGetValue(key, out var cached))
            return cached;
        if (_failedModels.Contains(key))
            return null;

        try
        {
            var model = BuildModel(flat);
            _models[key] = model;
            _modelOrder.Enqueue(key);

            var limit = Math.Max(100, _cfg.GetCVar(Voxel3DCVars.ModelCacheSize));
            while (_models.Count > limit && _modelOrder.Count > 0)
                _models.Remove(_modelOrder.Dequeue());

            return model;
        }
        catch (Exception e)
        {
            _failedModels.Add(key);
            Log.Warning($"3D view: could not build a model ({e.Message}).");
            return null;
        }
    }

    private E.VoxelModel BuildModel(bool flat)
    {
        var layers = new List<E.ComposeLayer>(_layerBuffer.Count);
        foreach (var l in _layerBuffer)
        {
            var sheet = LoadImage(l.PngPath);

            // Same sheet layout the engine uses: all directions, then frames, laid out row by row.
            var statesX = Math.Max(1, sheet.Width / l.RsiSize.X);
            var statesY = Math.Max(1, sheet.Height / l.RsiSize.Y);
            var frames = Math.Max(1, statesX * statesY / l.DirCount);
            var frame = Math.Clamp(l.Frame, 0, frames - 1);

            E.RgbaImage Cut(int direction)
            {
                var index = direction * frames + frame;
                return sheet.Crop(index % statesX * l.RsiSize.X, index / statesX * l.RsiSize.Y, l.RsiSize.X, l.RsiSize.Y);
            }

            // RSI order is south, north, east, west, then the diagonals, which are not used.
            var hasDirections = !flat && l.DirCount >= 4;
            var (offset, scale, rotation) = Decompose(l.Matrix);
            layers.Add(new E.ComposeLayer
            {
                South = Cut(0),
                North = hasDirections ? Cut(1) : null,
                East = hasDirections ? Cut(2) : null,
                West = hasDirections ? Cut(3) : null,
                Offset = offset,
                Scale = scale,
                Rotation = rotation,
                Tint = l.Tint,
            });
        }

        var options = new E.ComposeOptions
        {
            PixelsPerMeter = PixelsPerMeter,
            MaxVoxelsPerAxis = Math.Max(16, _cfg.GetCVar(Voxel3DCVars.MaxVoxelsPerAxis)),
        };

        return flat ? E.ViewComposer.BuildFlat(layers, options) : E.ViewComposer.BuildUpright(layers, options);
    }

    private E.RgbaImage LoadImage(string path)
    {
        if (_images.TryGetValue(path, out var cached))
            return cached;
        if (_failedImages.Contains(path))
            throw new InvalidOperationException($"{path} could not be read earlier");

        try
        {
            using var stream = _resources.ContentFileRead(path);
            var image = E.PngDecoder.Decode(stream);

            if (_images.Count > 2048)
                _images.Clear();

            _images[path] = image;
            return image;
        }
        catch (Exception e)
        {
            _failedImages.Add(path);
            Log.Warning($"3D view: could not read {path} ({e.Message}).");
            throw;
        }
    }

    private E.RgbaImage? TileImage(Tile tile)
    {
        var key = (tile.TypeId, tile.Variant, tile.RotationMirroring);
        if (_tileImages.TryGetValue(key, out var cached))
            return cached;

        E.RgbaImage? result = null;
        try
        {
            var definition = _tileDefs[tile.TypeId];
            if (definition.Sprite is { } path)
            {
                var sheet = LoadImage(path.ToString());
                var size = (int) PixelsPerMeter;
                var x = (tile.Variant + 1) * size <= sheet.Width ? tile.Variant * size : 0;
                var picture = sheet.Crop(x, 0, Math.Min(size, sheet.Width), Math.Min(size, sheet.Height));
                result = TurnTile(picture, tile.RotationMirroring);
            }
        }
        catch (Exception)
        {
            result = null; // already logged by LoadImage, or a tile with no usable picture
        }

        _tileImages[key] = result;
        return result;
    }

    /// <summary>Tile rotation and mirroring as SS14 stores it: 0-3 quarter turns, and 4 or more also mirrors.</summary>
    private static E.RgbaImage TurnTile(E.RgbaImage picture, int rotationMirroring)
    {
        if (rotationMirroring == 0 || picture.Width != picture.Height)
            return picture;

        var size = picture.Width;
        var image = picture;
        if (rotationMirroring > 3)
            image = E.ModelBuilder.Mirror(image);

        for (var turn = 0; turn < rotationMirroring % 4; turn++)
        {
            var turned = new E.RgbaImage(size, size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    turned[size - 1 - y, x] = image[x, y]; // a quarter turn clockwise
            }

            image = turned;
        }

        return image;
    }

    private ulong HashLayers(bool flat)
    {
        var h = 14695981039346656037UL;
        h = Mix(h, flat ? 1UL : 0UL);
        foreach (var l in _layerBuffer)
        {
            foreach (var c in l.PngPath)
                h = Mix(h, c);

            h = Mix(h, (ulong) l.Frame);
            h = Mix(h, (ulong) l.DirCount);
            h = Mix(h, Quantize(l.Matrix.M11));
            h = Mix(h, Quantize(l.Matrix.M12));
            h = Mix(h, Quantize(l.Matrix.M21));
            h = Mix(h, Quantize(l.Matrix.M22));
            h = Mix(h, Quantize(l.Matrix.M31));
            h = Mix(h, Quantize(l.Matrix.M32));
            h = Mix(h, ((ulong) l.Tint.R << 24) | ((ulong) l.Tint.G << 16) | ((ulong) l.Tint.B << 8) | l.Tint.A);
        }

        return h;
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
        return hash;
    }

    private static ulong Quantize(float value) => (ulong) (long) MathF.Round(value * 1000f);

    /// <summary>Splits a 2D transform into offset, scale and rotation, the way a sprite layer wants them.</summary>
    private static (Vector2 Offset, Vector2 Scale, float Rotation) Decompose(Matrix3x2 m)
    {
        var sx = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
        var sy = MathF.Sqrt(m.M21 * m.M21 + m.M22 * m.M22);
        if (m.M11 * m.M22 - m.M12 * m.M21 < 0f)
            sy = -sy;

        return (new Vector2(m.M31, m.M32), new Vector2(sx, sy), MathF.Atan2(m.M12, m.M11));
    }

    // ---- drawing ----------------------------------------------------------------------------------------------

    private void Render()
    {
        if (_overlay == null || _renderOptions == null)
            return;

        var width = Math.Clamp(_cfg.GetCVar(Voxel3DCVars.Width), 64, 1920);
        var height = Math.Clamp(_cfg.GetCVar(Voxel3DCVars.Height), 36, 1080);
        if (_pixels.Length != width * height)
        {
            _pixels = new E.Rgba[width * height];
            _texturePixels = new Rgba32[width * height];
        }

        E.RayRenderer.Render(_scene, _camera, width, height, _pixels, _renderOptions, _context);

        for (var i = 0; i < _pixels.Length; i++)
        {
            var p = _pixels[i];
            _texturePixels[i] = new Rgba32(p.R, p.G, p.B, 255);
        }

        if (_texture == null || _textureWidth != width || _textureHeight != height)
        {
            _texture?.Dispose();

            // Pixels are scaled up without smoothing, so the voxels stay crisp.
            var parameters = new TextureLoadParameters { SampleParameters = new TextureSampleParameters { Filter = false } };
            _texture = _clyde.CreateBlankTexture<Rgba32>(new Vector2i(width, height), "voxel3d", parameters);
            _textureWidth = width;
            _textureHeight = height;
        }

        _texture.SetSubImage(Vector2i.Zero, new Vector2i(width, height), new ReadOnlySpan<Rgba32>(_texturePixels));
        _overlay.Frame = _texture;
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.PI * 2f;
        while (angle < -MathF.PI) angle += MathF.PI * 2f;
        return angle;
    }
}
