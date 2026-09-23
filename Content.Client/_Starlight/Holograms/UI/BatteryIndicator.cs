using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Holograms.UI;

/// <summary>
/// Small battery gauge - border, fill bar, nub, and a percentage label. Drawn from plain
/// controls instead of a texture so it works with no art. Used in HologramConsoleWindow's header.
/// </summary>
public sealed class BatteryIndicator : BoxContainer
{
    private const string FullColor = "#10b981";
    private const string LowColor = "#fbbf24";
    private const string CriticalColor = "#ef4444";
    private const string EmptyColor = "#4b5563";
    private const string MutedColor = "#94a3b8";

    private readonly PanelContainer _body;
    private readonly ProgressBar _fill;
    private readonly PanelContainer _nub;
    private readonly Label _label;

    public BatteryIndicator()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 4;
        VerticalAlignment = VAlignment.Center;

        _body = new PanelContainer
        {
            MinSize = new Vector2(26, 14),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = Color.FromHex(EmptyColor),
                BorderThickness = new Thickness(2),
            },
        };

        _fill = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            Margin = new Thickness(2),
        };

        _body.AddChild(_fill);

        _nub = new PanelContainer
        {
            MinSize = new Vector2(3, 6),
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(EmptyColor) },
        };

        _label = new Label
        {
            Text = "--%",
            FontColorOverride = Color.FromHex(MutedColor),
            MinSize = new Vector2(38, 0),
        };

        AddChild(_body);
        AddChild(_nub);
        AddChild(_label);
    }

    /// <summary>
    /// Updates the gauge. Pass null when the device has no readable battery at all (still shown,
    /// per HologramBatteryDisplayMode.Shown, just with nothing to report).
    /// </summary>
    public void SetCharge(float? percent)
    {
        if (percent is not { } value)
        {
            _fill.Value = 0f;
            SetColor(MutedColor);
            _label.Text = "NO CELL";
            return;
        }

        var clamped = Math.Clamp(value, 0f, 100f);
        _fill.Value = clamped / 100f;
        _label.Text = $"{clamped:F0}%";

        var color = clamped switch
        {
            > 50 => FullColor,
            > 20 => LowColor,
            _ => CriticalColor,
        };
        SetColor(color);
    }

    private void SetColor(string hex)
    {
        var color = Color.FromHex(hex);
        _fill.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color };
        _nub.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        _label.FontColorOverride = color;
        _body.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = color,
            BorderThickness = new Thickness(2),
        };
    }
}
