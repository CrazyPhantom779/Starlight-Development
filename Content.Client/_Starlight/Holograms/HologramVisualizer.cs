using System.Linq;
using Content.Shared._Starlight.Holograms;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Holograms;

public sealed class HologramVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, ComponentStartup>(OnHologramStartup);
        SubscribeLocalEvent<HologramComponent, ComponentShutdown>(OnHologramShutdown);
    }

    private void OnHologramStartup(Entity<HologramComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        ApplyHologramShader(ent.Owner, ent.Comp, sprite);
    }

    private void OnHologramShutdown(Entity<HologramComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        sprite.PostShader = null;
        sprite.RaiseShaderEvent = false;
    }

    private void ApplyHologramShader(EntityUid uid, HologramComponent component, SpriteComponent sprite)
    {
        if (!sprite.AllLayers.Any())
            return;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            if (!_sprite.TryGetLayer((uid, sprite), i, out var layer, false))
                continue;

            if (layer.ShaderPrototype == "DisplacedDraw")
                continue;

            sprite.LayerSetShader(i, "unshaded");
        }

        var textureHeight = sprite.AllLayers.Max(x => x.PixelSize.Y);

        var shader = _prototype.Index<ShaderPrototype>(component.ShaderName).InstanceUnique();
        shader.SetParameter("textureHeight", textureHeight);
        shader.SetParameter("hue", component.HologramHue);

        sprite.PostShader = shader;
        sprite.RaiseShaderEvent = false;
    }
}
