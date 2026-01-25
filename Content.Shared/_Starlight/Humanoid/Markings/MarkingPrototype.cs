using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

// ReSharper disable CheckNamespace

namespace Content.Shared._Starlight.Humanoid.Markings;

public sealed partial class MarkingPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<MarkingPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? WaggingId;
    
    [DataField]
    public string? StaticId;
}
