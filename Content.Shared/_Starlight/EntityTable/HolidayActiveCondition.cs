using Content.Shared._Starlight.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.EntityTable.Conditions;
using Robust.Shared.Prototypes;
using Content.Shared.EntityTable.Conditions;
using Robust.Shared.Serialization;
using Content.Shared.EntityTable.Conditions;

namespace Content.Shared._Starlight.EntityTable;

/// <summary>
/// EntityTable condition: spawns if the specified holiday is currently active.
/// </summary>
[DataDefinition]
public sealed partial class HolidayActiveCondition : EntityTableCondition
{
    [DataField("holiday", required: true)]
    public string Holiday = string.Empty;

    protected override bool EvaluateImplementation(EntityTableSelector root, IEntityManager entMan, IPrototypeManager proto, EntityTableContext ctx)
    {
        var sys = entMan.System<HolidayConditionSystem>();
        return sys.CheckHoliday(Holiday);
    }
}
