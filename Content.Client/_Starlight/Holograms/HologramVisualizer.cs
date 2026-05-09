using System.Linq;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Holograms;

public sealed class HologramVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string ShaderName = "StarlightHologram";
    private const string HideContextMenuTag = "HideContextMenu";
    private const float HologramHue = 0.64f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, ComponentInit>(OnHologramInit);
        SubscribeLocalEvent<HologramComponent, ComponentShutdown>(OnHologramShutdown);
    }

    private void OnHologramInit(Entity<HologramComponent> ent, ref ComponentInit args)
    {
        _tag.AddTag(ent.Owner, HideContextMenuTag);

        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        ApplyHologramShader(ent.Owner, sprite);
    }

    private void OnHologramShutdown(Entity<HologramComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        sprite.PostShader = null;
        sprite.RaiseShaderEvent = false;
    }

    private void ApplyHologramShader(EntityUid uid, SpriteComponent sprite)
    {
        var layers = sprite.AllLayers.ToArray();
        if (layers.Length == 0)
            return;

        for (var i = 0; i < layers.Length; i++)
        {
            if (!_sprite.TryGetLayer((uid, sprite), i, out var layer, false))
                continue;

            if (layer.ShaderPrototype == "DisplacedDraw")
                continue;

            sprite.LayerSetShader(i, "unshaded");
        }

        var textureHeight = layers.Max(x => x.PixelSize.Y);

        var shader = _prototype.Index<ShaderPrototype>(ShaderName).InstanceUnique();
        shader.SetParameter("textureHeight", textureHeight);
        shader.SetParameter("hue", HologramHue);

        sprite.PostShader = shader;
        sprite.RaiseShaderEvent = false;
    }
}
