using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Holograms;

public sealed partial class HologramVisualizerSystem : EntitySystem
{
    private const string HideContextMenuTag = "HideContextMenu";
    private const string HologramProjectionShader = "HologramProjection";
    private const string StarlightHologramShader = "StarlightHologram";

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, ComponentInit>(OnHologramInit);
        SubscribeLocalEvent<HologramComponent, ComponentShutdown>(OnHologramShutdown);
        SubscribeLocalEvent<HologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    private void OnHologramInit(Entity<HologramComponent> ent, ref ComponentInit args)
    {
        _tag.AddTag(ent.Owner, HideContextMenuTag);

        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        ApplyHologramVisuals(ent.Owner, ent.Comp, sprite);
    }

    private void OnHologramShutdown(Entity<HologramComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        sprite.PostShader = null;
        sprite.RaiseShaderEvent = false;
        sprite.NoRotation = false;
    }

    private void OnShaderRender(Entity<HologramComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (!TryEnsureShader(ent.Comp, args.Sprite))
            return;

        UpdateHologramShader(ent.Comp, args.Sprite);
    }

    private void ApplyHologramVisuals(EntityUid uid, HologramComponent component, SpriteComponent sprite)
    {
        if (!sprite.AllLayers.Any())
            return;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            if (!_sprite.TryGetLayer((uid, sprite), i, out var layer, false))
                continue;

            // Keep displacement masks intact, but make the visible sprite layers glow like holopads.
            if (layer.ShaderPrototype == "DisplacedDraw")
                continue;

            sprite.LayerSetShader(i, "unshaded");
        }

        _sprite.SetColor((uid, sprite), Color.White);
        _sprite.SetOffset((uid, sprite), component.Offset);
        sprite.NoRotation = component.ForceHolopadFacing;

        if (!TryEnsureShader(component, sprite))
            return;

        UpdateHologramShader(component, sprite);
        sprite.RaiseShaderEvent = true;
    }

    private bool TryEnsureShader(HologramComponent component, SpriteComponent sprite)
    {
        if (sprite.PostShader != null)
            return true;

        if (!_prototype.TryIndex<ShaderPrototype>(component.ShaderName, out var shaderPrototype))
            return false;

        sprite.PostShader = shaderPrototype.InstanceUnique();
        return true;
    }

    private void UpdateHologramShader(HologramComponent component, SpriteComponent sprite)
    {
        if (!sprite.AllLayers.Any() || sprite.PostShader == null)
            return;

        var textureHeight = sprite.AllLayers.Max(layer => layer.PixelSize.Y);
        var shader = sprite.PostShader;

        if (component.ShaderName == HologramProjectionShader)
        {
            SetHologramProjectionParameters(component, shader, textureHeight);
            return;
        }

        if (component.ShaderName == StarlightHologramShader)
        {
            shader.SetParameter("hue", component.Hue);
            shader.SetParameter("textureHeight", textureHeight);
            return;
        }

        // Compatibility path for the stock/holopad Hologram shader.
        shader.SetParameter("color1", new Vector3(component.Color1.R, component.Color1.G, component.Color1.B));
        shader.SetParameter("color2", new Vector3(component.Color2.R, component.Color2.G, component.Color2.B));
        shader.SetParameter("alpha", component.Alpha);
        shader.SetParameter("intensity", component.Intensity);
        shader.SetParameter("texHeight", textureHeight);
        shader.SetParameter("t", (float) _timing.CurTime.TotalSeconds * component.ScrollRate);
    }

    private static void SetHologramProjectionParameters(HologramComponent component, ShaderInstance shader, float textureHeight)
    {
        shader.SetParameter("color1", new Vector3(component.Color1.R, component.Color1.G, component.Color1.B));
        shader.SetParameter("color2", new Vector3(component.Color2.R, component.Color2.G, component.Color2.B));
        shader.SetParameter("hue", component.Hue);
        shader.SetParameter("saturation", component.Saturation);
        shader.SetParameter("alpha", component.Alpha);
        shader.SetParameter("intensity", component.Intensity);
        shader.SetParameter("textureHeight", textureHeight);
        shader.SetParameter("colorBlend", component.ColorBlend);
        shader.SetParameter("scanlineOpacity", component.ScanlineOpacity);
        shader.SetParameter("noiseOpacity", component.NoiseOpacity);
        shader.SetParameter("flickerStrength", component.FlickerStrength);
        shader.SetParameter("lineScrollSpeed", component.LineScrollSpeed);
    }
}
