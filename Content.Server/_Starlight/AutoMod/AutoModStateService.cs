using System.Linq;
using Content.Shared._Starlight.AutoMod;
using Robust.Shared.Network;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModStateService
{
    private readonly List<AutoModIncidentRecord> _incidents = new();
    private readonly List<AutoModAdjustmentRecord> _adjustments = new();
    private readonly Dictionary<(NetUserId, string), int> _cachedPoints = new();

    public IReadOnlyList<AutoModIncidentRecord> Incidents => _incidents;
    public IReadOnlyList<AutoModAdjustmentRecord> Adjustments => _adjustments;

    public int GetActivePoints(NetUserId player, string scope, DateTime nowUtc)
    {
        var total = 0;
        foreach (var incident in _incidents)
        {
            if (incident.PlayerUserId != player || incident.EscalationScope != scope)
                continue;

            if (incident.Status is AutoModIncidentStatus.FalsePositive or AutoModIncidentStatus.ManuallyDisabled or AutoModIncidentStatus.RejectedByAdmin)
                continue;

            if (incident.DecaysAtUtc is { } decay && decay <= nowUtc)
                continue;

            total += incident.Points;
        }
        return total;
    }

    public void RecordIncident(AutoModIncidentRecord incident)
    {
        if (_incidents.Any(x => x.IncidentId == incident.IncidentId))
            return;

        _incidents.Add(incident);
        if (_incidents.Count > 10000)
            _incidents.RemoveRange(0, _incidents.Count - 10000);
    }

    public bool MarkFalsePositive(Guid incidentId, NetUserId admin, string reason)
    {
        var idx = _incidents.FindIndex(x => x.IncidentId == incidentId);
        if (idx < 0)
            return false;

        var old = _incidents[idx];
        _incidents[idx] = old with { Status = AutoModIncidentStatus.FalsePositive, SyncStatus = AutoModSyncStatus.Pending };
        _adjustments.Add(new AutoModAdjustmentRecord(Guid.NewGuid(), incidentId, admin, DateTime.UtcNow, "Status", old.Status.ToString(), AutoModIncidentStatus.FalsePositive.ToString(), reason));
        return true;
    }

    public List<AutoModIncidentRecord> GetRecent(int count)
    {
        return _incidents.OrderByDescending(x => x.CreatedAtUtc).Take(count).ToList();
    }

    public int CountUnsynced()
    {
        return _incidents.Count(x => x.SyncStatus is AutoModSyncStatus.Pending or AutoModSyncStatus.LocalOnly or AutoModSyncStatus.Failed);
    }
}
