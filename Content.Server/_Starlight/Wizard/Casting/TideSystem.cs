using System.Linq;
using Content.Server._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>
/// Every wizard has their own tides: a cost multiplier per school that drifts over the round. A school running hot
/// costs more to cast from, one running cool costs less, so the best spell to cast is never quite the same twice.
/// </summary>
public sealed partial class TideSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const float MinTide = 0.8f;
    private const float MaxTide = 1.3f;
    private const float DriftStrength = 0.5f;
    private const float MinShiftMinutes = 5f;
    private const float MaxShiftMinutes = 9f;
    private const float AnnounceThreshold = 0.12f;

    private readonly Dictionary<EntityUid, TimeSpan> _nextShift = [];
    private TimeSpan _nextCheck;

    /// <summary>Gives a caster fresh tides, if they have none.</summary>
    public void EnsureTides(EntityUid caster, SpellcraftComponent craft)
    {
        if (craft.Tides.Count > 0)
            return;

        foreach (var school in AllSchools())
            craft.Tides[school] = _random.NextFloat(MinTide + 0.05f, MaxTide - 0.1f);

        _nextShift[caster] = NextShiftTime();
        Dirty(caster, craft);
    }

    private IEnumerable<string> AllSchools()
        => _proto.EnumeratePrototypes<SpellGlyphPrototype>().SelectMany(g => g.Schools).Distinct();

    private TimeSpan NextShiftTime()
        => _timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(MinShiftMinutes, MaxShiftMinutes));

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(5);

        var query = EntityQueryEnumerator<SpellcraftComponent>();
        while (query.MoveNext(out var uid, out var craft))
        {
            if (craft.Tides.Count == 0)
                continue;

            if (!_nextShift.TryGetValue(uid, out var due))
            {
                _nextShift[uid] = NextShiftTime();
                continue;
            }

            if (_timing.CurTime < due)
                continue;

            Shift(uid, craft);
            _nextShift[uid] = NextShiftTime();
        }
    }

    /// <summary>Drifts each tide toward a new random value and tells the caster about any that moved a lot.</summary>
    private void Shift(EntityUid caster, SpellcraftComponent craft)
    {
        string? hottest = null;
        string? coolest = null;
        var hotChange = 0f;
        var coolChange = 0f;

        foreach (var school in craft.Tides.Keys.ToList())
        {
            var old = craft.Tides[school];
            var target = _random.NextFloat(MinTide, MaxTide);
            var next = Math.Clamp(old + ((target - old) * DriftStrength), MinTide, MaxTide);
            craft.Tides[school] = next;

            var change = next - old;
            if (change > hotChange)
            {
                hotChange = change;
                hottest = school;
            }

            if (change < coolChange)
            {
                coolChange = change;
                coolest = school;
            }
        }

        Dirty(caster, craft);
        _spells.RefreshActions(caster);

        if (hottest != null && hotChange > AnnounceThreshold)
            _popup.PopupEntity(Loc.GetString("tide-hot", ("school", Loc.GetString($"spellcraft-school-{hottest.ToLowerInvariant()}"))), caster, caster, PopupType.Medium);

        if (coolest != null && coolChange < -AnnounceThreshold)
            _popup.PopupEntity(Loc.GetString("tide-cool", ("school", Loc.GetString($"spellcraft-school-{coolest.ToLowerInvariant()}"))), caster, caster, PopupType.Medium);
    }
}
