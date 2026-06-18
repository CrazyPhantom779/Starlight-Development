using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.Utility;

[Serializable, NetSerializable]
[DataDefinition]
public sealed partial class EmpProperties
{

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Range = 1.0f;

    /// <summary>
    /// How much energy will be consumed per battery in range
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float EnergyConsumption;

    /// <summary>
    /// How long it disables targets in seconds
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan DisableDuration = TimeSpan.FromSeconds(60);

    public EmpProperties(float range, float energyConsumption, TimeSpan disableDuration)
    {
        Range = range;
        EnergyConsumption = energyConsumption;
        DisableDuration = disableDuration;
    }
}
