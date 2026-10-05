using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Wizard.Anomaly;

/// <summary>
/// The Primordial Wind anomaly. Its pulses fold nearby space; going supercritical it may awaken a wizard,
/// chosen through normal antag selection (so only players with the Wizard antag enabled for their character).
/// </summary>
[RegisterComponent, Access(typeof(PrimordialWindAnomalySystem))]
public sealed partial class PrimordialWindAnomalyComponent : Component
{
    /// <summary>Range around the anomaly affected by pulses.</summary>
    [DataField]
    public float PulseRange = 6f;

    /// <summary>Chance per pulse that two nearby creatures swap places.</summary>
    [DataField]
    public float SwapChance = 0.35f;

    /// <summary>Chance per nearby creature of getting a strange vision on a pulse.</summary>
    [DataField]
    public float VisionChance = 0.5f;

    /// <summary>Game rule started when the anomaly goes supercritical. Null disables awakening.</summary>
    [DataField]
    public EntProtoId? AwakeningRule = "PrimordialAwakening";

    [DataField]
    public EntProtoId SupercriticalEffect = "EffectFlashBluespaceSimple";

    [DataField]
    public EntProtoId PulseEffect = "EffectFlashBluespaceSimple";
}
