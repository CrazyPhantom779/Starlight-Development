using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

[RegisterComponent, NetworkedComponent]
public sealed partial class BlobObserverComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsProcessingMoveEvent;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Core = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool CanMove = true;

    [ViewVariables(VVAccess.ReadOnly)]
    public BlobChemType SelectedChemId = BlobChemType.ReactiveSpines;
}

[Serializable, NetSerializable]
public sealed class BlobChemSwapComponentState : ComponentState
{
    public BlobChemType SelectedChem;
}

[Serializable, NetSerializable]
public sealed class BlobChemSwapBoundUserInterfaceState(Dictionary<BlobChemType, Color> chemList, BlobChemType selectedId) : BoundUserInterfaceState
{
    public readonly Dictionary<BlobChemType, Color> ChemList = chemList;
    public readonly BlobChemType SelectedChem = selectedId;
}

[Serializable, NetSerializable]
public sealed class BlobChemSwapPrototypeSelectedMessage(BlobChemType selectedId) : BoundUserInterfaceMessage
{
    public readonly BlobChemType SelectedId = selectedId;
}

[Serializable, NetSerializable]
public enum BlobChemSwapUiKey : byte
{
    Key
}
public sealed partial class BlobCreateFactoryActionEvent : WorldTargetActionEvent
{
}
public sealed partial class BlobCreateResourceActionEvent : WorldTargetActionEvent
{
}
public sealed partial class BlobCreateNodeActionEvent : WorldTargetActionEvent
{
}
public sealed partial class BlobCreateBlobbernautActionEvent : WorldTargetActionEvent
{
}
public sealed partial class BlobSplitCoreActionEvent : WorldTargetActionEvent
{
}
public sealed partial class BlobSwapCoreActionEvent : WorldTargetActionEvent
{
}
public sealed partial class BlobToCoreActionEvent : InstantActionEvent
{
}
public sealed partial class BlobToNodeActionEvent : InstantActionEvent
{
}
public sealed partial class BlobHelpActionEvent : InstantActionEvent
{
}
public sealed partial class BlobSwapChemActionEvent : InstantActionEvent
{
}
