using Robust.Client.UserInterface;

namespace Content.Client._Starlight.AutoMod.UI;

/// <summary>
/// Kept only so existing interface registrations do not fail while AutoMod moves to EUI.
/// The actual admin UI is <see cref="AutoModEui"/>.
/// </summary>
public sealed class AutoModBoundUserInterface : BoundUserInterface
{
    private AutoModWindow? _window;

    public AutoModBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AutoModWindow>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Close();
        _window = null;
    }
}
