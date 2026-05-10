using Content.Shared._Starlight.AutoMod;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModNullLinkSync
{
    private readonly IConfigurationManager _cfg;
    public bool Healthy { get; private set; }

    public AutoModNullLinkSync(IConfigurationManager cfg)
    {
        _cfg = cfg;
    }

    public bool Enabled => _cfg.GetCVar(StarlightCCVars.AutoModNullLinkEnabled);

    public void Initialize()
    {
        Healthy = !Enabled || TryConnect();
    }

    public void QueueIncident(AutoModIncidentRecord incident)
    {
        if (!Enabled)
            return;

        // Intentionally isolated behind the CVar. Wire this to the NullLink AutoMod grain once the NullLink package adds IAutoModGrain.
        Healthy = TryConnect();
    }

    public void QueueAdjustment(AutoModAdjustmentRecord adjustment)
    {
        if (!Enabled)
            return;

        Healthy = TryConnect();
    }

    private bool TryConnect()
    {
        // Placeholder avoids hard dependency on NullLink assemblies when automod.null_link.enabled=false.
        // The package includes the NullLink contract to implement in Starlight.NullLink.
        return false;
    }
}
