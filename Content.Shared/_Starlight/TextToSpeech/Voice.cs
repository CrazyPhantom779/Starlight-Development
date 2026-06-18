using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.TextToSpeech;
/// <summary>
/// Prototype represent TTS voices
/// </summary>
[Prototype]
public sealed partial class VoicePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Voice { get; private set; }

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public Sex Sex { get; private set; } = default!;

    [DataField]
    public bool Silicon { get; private set; } = false;

    [DataField]
    public string? Copyright { get; private set; }

    [DataField]
    public string? License { get; private set; }
}
