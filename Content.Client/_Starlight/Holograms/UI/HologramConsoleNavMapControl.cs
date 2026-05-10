using Content.Client.Pinpointer.UI;

namespace Content.Client._Starlight.Holograms.UI;

/// <summary>
/// NavMap control for hologram console showing projector locations.
/// </summary>
public sealed class HologramConsoleNavMapControl : NavMapControl
{
    public NetEntity? SelectedProjector;

    private readonly Color _selectedColor = Color.FromHex("#10b981");
    private readonly Color _unselectedColor = Color.FromHex("#ef4444");

    public HologramConsoleNavMapControl()
    {
        WallColor = Color.FromHex("#66d9c4");
        TileColor = Color.FromHex("#326e64");
        BackgroundColor = Color.FromHex("#0a1612");
    }

    public Color GetProjectorColor(bool isSelected) => isSelected ? _selectedColor : _unselectedColor;
}
