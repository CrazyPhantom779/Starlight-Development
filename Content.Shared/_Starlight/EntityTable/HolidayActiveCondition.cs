using Content.Shared._Starlight.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityTable.Conditions;

/// <summary>
/// EntityTable condition: spawns if the specified holiday is currently active.
/// </summary>
[DataDefinition]
public sealed partial class HolidayActiveCondition : EntityTableCondition
{
    [DataField(required: true)]
    public string Holiday = string.Empty;

    protected override bool EvaluateImplementation(EntityTableSelector root, IEntityManager entMan, IPrototypeManager proto, EntityTableContext ctx)
    {
        var sys = entMan.System<HolidayConditionSystem>();
        return sys.CheckHoliday(Holiday);
    }
}
