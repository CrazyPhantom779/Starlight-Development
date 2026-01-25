using System.IO;

namespace Content.Client._Starlight.TextToSpeech;

[RegisterComponent]
[Access(typeof(TextToSpeechSystem))]
public sealed partial class ClientTTSAudioComponent : Component
{
    public MemoryStream? Stream;
}
