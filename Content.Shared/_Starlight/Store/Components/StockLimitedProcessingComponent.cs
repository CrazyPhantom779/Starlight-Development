using Robust.Shared.GameStates;

namespace Content.Shared.Store.Components;

/// <summary>
/// This component is used to track which store listings are currently being processed.
/// This prevents spam-clicking on stock-limited items.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StockLimitedProcessingComponent : Component
{
    /// <summary>
    /// Dictionary to track which listings are currently being processed.
    /// Key is the listing ID, value is whether the listing is being processed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, bool> ProcessingListings = [];
}
