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
        _window.OnWeave += (graph, discipline, output) => SendMessage(new SpellcraftWeaveMessage(graph, discipline, output));
        _window.OnRote += id => SendMessage(new SpellcraftRoteMessage(id));
        _window.OnForget += action => SendMessage(new SpellcraftForgetMessage(action));
        _window.OnDrawCard += () => SendMessage(new SpellcraftDrawCardMessage());
        _window.OnCircle += () => SendMessage(new SpellcraftCircleMessage());
        _window.OnRite += id => SendMessage(new SpellcraftRiteMessage(id));
        _window.OnRefresh += () => SendMessage(new SpellcraftRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SpellcraftBuiState msg)
            _window?.UpdateState(msg);
    }
}
