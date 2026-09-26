using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Starlight.Holograms.UI;

public sealed class AcceptHologramScanWindow : DefaultWindow
{
    public readonly Button AcceptButton;
    public readonly Button DenyButton;

    public AcceptHologramScanWindow()
    {
        Title = Loc.GetString("accept-hologram-scan-window-title");

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label { Text = Loc.GetString("accept-hologram-scan-window-prompt-text-part") },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    Children =
                    {
                        (AcceptButton = new Button { Text = Loc.GetString("accept-hologram-scan-window-accept-button") }),
                        new Control { MinSize = new Vector2(20, 0) },
                        (DenyButton = new Button { Text = Loc.GetString("accept-hologram-scan-window-deny-button") }),
                    },
                },
            },
        });
    }
}
