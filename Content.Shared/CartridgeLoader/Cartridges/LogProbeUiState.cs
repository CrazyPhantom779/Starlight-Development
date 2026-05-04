using Robust.Shared.Serialization;
using Content.Shared._Starlight.CartridgeLoader.Cartridges; // Starlight

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class LogProbeUiState : BoundUserInterfaceState
{
    /// <summary>
    /// The name of the scanned entity.
    /// </summary>
    public string EntityName;

    /// <summary>
    /// The list of probed network devices
    /// </summary>
    public List<PulledAccessLog> PulledLogs;

    // Starlight Start
    /// <summary>
    /// The NanoChat data if a card was scanned, null otherwise
    /// </summary>
    public NanoChatData? NanoChatData { get; }
    // Starlight End

    public LogProbeUiState(string entityName, List<PulledAccessLog> pulledLogs, NanoChatData? nanoChatData = null) // Starlight Edit: NanoChat support
    {
        EntityName = entityName;
        PulledLogs = pulledLogs;
        NanoChatData = nanoChatData; // Starlight
    }
}

[Serializable, NetSerializable, DataRecord]
public sealed partial class PulledAccessLog
{
    public readonly TimeSpan Time;
    public readonly string Accessor;

    public PulledAccessLog(TimeSpan time, string accessor)
    {
        Time = time;
        Accessor = accessor;
    }
}
