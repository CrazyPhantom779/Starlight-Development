using Content.Client.Pinpointer.UI;

namespace Content.Client._Starlight.Holograms.UI;

/// <summary>
/// Station nav map themed for hologram projector selection.
/// </summary>
public sealed class HologramConsoleNavMapControl : NavMapControl
{
    private readonly Color _selectedColor = Color.FromHex("#10b981");
    private readonly Color _unselectedColor = Color.FromHex("#38bdf8");

    public HologramConsoleNavMapControl()
    {
        WallColor = Color.FromHex("#66d9c4");
        TileColor = Color.FromHex("#326e64");
        BackgroundColor = Color.FromHex("#0a1612");
    }

    /// <summary>
    /// Black marks a projector that exists on the map but can't currently host a hologram -
    /// unpowered, switched off, or shut down from damage.
    /// </summary>
    public Color GetProjectorColor(bool selected, bool isFunctional = true)
        => !isFunctional ? Color.Black : selected ? _selectedColor : _unselectedColor;
}
