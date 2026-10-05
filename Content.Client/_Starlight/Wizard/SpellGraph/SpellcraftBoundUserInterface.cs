using Content.Shared._Starlight.Wizard.SpellGraph;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Wizard.SpellGraph;

[UsedImplicitly]
public sealed class SpellcraftBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private SpellcraftWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SpellcraftWindow>();
        _window.OnWeave += chain => SendMessage(new SpellcraftWeaveMessage(chain));
        _window.OnForget += action => SendMessage(new SpellcraftForgetMessage(action));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SpellcraftBuiState msg)
            _window?.UpdateState(msg);
    }
}
