using Robust.Shared.Audio;

namespace Content.Shared.Radio.Components
{
    [RegisterComponent]
    public sealed partial class RadioChimeComponent : Component
    {
        [DataField]
        public SoundSpecifier? Sound;
    }
}
