using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Shared._Starlight.Wizard.Wind;

/// <summary>
/// "Wind" is the mana-like resource spells draw from. It regenerates passively.
/// The stored <see cref="Current"/> value is only valid at <see cref="LastUpdate"/>; use
/// <see cref="SharedWindSystem.GetWind"/> to get the live value. This avoids networking every tick.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class WindComponent : Component
{
    /// <summary>Value of Wind at <see cref="LastUpdate"/>. May be negative while overdrawn.</summary>
    [DataField, AutoNetworkedField]
    public float Current = 100f;

    [DataField, AutoNetworkedField]
    public float Max = 100f;

    /// <summary>Wind regained per second.</summary>
    [DataField, AutoNetworkedField]
    public float RegenPerSecond = 2f;

    /// <summary>
    /// How far below zero Wind may be pushed ("overcasting"). Overcasting can trigger Gusts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OverdrawLimit = 25f;

    /// <summary>Multiplies the cost of every spell. Used by Aspects.</summary>
    [DataField, AutoNetworkedField]
    public float CostMultiplier = 1f;

    /// <summary>Multiplies regeneration. Used by Aspects.</summary>
    [DataField, AutoNetworkedField]
    public float RegenMultiplier = 1f;

    /// <summary>Multiplies the chance of a Gust when overcasting. Used by Aspects.</summary>
    [DataField, AutoNetworkedField]
    public float GustChanceMultiplier = 1f;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastUpdate;

    /// <summary>Server only: when to next refresh the HUD alert.</summary>
    [DataField, AutoPausedField]
    public TimeSpan NextAlertUpdate;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "Wind";
}

/// <summary>
/// Put on an action entity to make using it cost Wind.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WindCostComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public float Cost = 10f;
}
