using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Body.Prototypes;

[Prototype]
public sealed partial class BodyPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = "";

    [DataField] public string Root { get; private set; } = string.Empty;

    [DataField] public Dictionary<string, BodyPrototypeSlot> Slots { get; private set; } = [];

    private BodyPrototype() { }

    public BodyPrototype(string id, string name, string root, Dictionary<string, BodyPrototypeSlot> slots)
    {
        ID = id;
        Name = name;
        Root = root;
        Slots = slots;
    }
}

[DataRecord]
public sealed partial record BodyPrototypeSlot(EntProtoId? Part, HashSet<string> Connections, Dictionary<string, string> Organs);
