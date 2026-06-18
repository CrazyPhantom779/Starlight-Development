namespace Content.Shared._Starlight.VentCrawl.Components;

[RegisterComponent, Virtual]
public partial class VentCrawlJunctionComponent : Component
{
    /// <summary>
    ///     The angles to connect to.
    /// </summary>
    [DataField] public List<Angle> Degrees = [];
}
