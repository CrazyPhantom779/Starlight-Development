using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Flock;

/// <summary>
/// Coagulated gnesis ("flockdrone_fluid") and Tealquila Sunrise. Port of the on_mob_life logic in goon's Reagents-Misc.dm:
/// - gnesis assimilates other reagents in the bloodstream (0.7u / tick)
/// - above 40u it also eats blood
/// - above 100u you get nasty effects
/// - above 200u there is a growing chance per tick to gib into flockbits
/// - Tealquila Sunrise flushes gnesis (2u / tick)
/// </summary>
public sealed partial class FlockGnesisSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FlockSystem _flock = default!;

    private static readonly ProtoId<ReagentPrototype> _gnesis = "FlockGnesis";
    private static readonly ProtoId<ReagentPrototype> _tealquila = "Tealquila";
    private static readonly ProtoId<ReagentPrototype> _blood = "Blood";
    private static readonly ProtoId<ReagentPrototype> _calomel = "Calomel";
    private static readonly ProtoId<ReagentPrototype> _pentaericAcid = "PentaericAcid";
    private const float ConversionRate = 0.7f;
    private const int GibThreshold = 200;
    private TimeSpan _next;

    private static readonly string[] _lowWhispers =
    {
        "flock-gnesis-whisper-low-1", "flock-gnesis-whisper-low-2", "flock-gnesis-whisper-low-3",
    };
    private static readonly string[] _highWhispers =
    {
        "flock-gnesis-whisper-high-1", "flock-gnesis-whisper-high-2", "flock-gnesis-whisper-high-3",
    };

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _next)
            return;
        _next = _timing.CurTime + TimeSpan.FromSeconds(1);

        var q = EntityQueryEnumerator<BloodstreamComponent>();
        while (q.MoveNext(out var uid, out var blood))
        {
            if (!_solution.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var sol))
                continue;
            var gnesis = sol.GetTotalPrototypeQuantity(_gnesis);
            if (gnesis <= 0)
                continue;
            Tick(uid, blood.BloodSolution!.Value, sol, gnesis);
        }
    }

    private void Tick(EntityUid uid, Entity<SolutionComponent> solEnt, Solution sol, FixedPoint2 gnesis)
    {
        // Tealquila neutralises gnesis
        if (sol.GetTotalPrototypeQuantity(_tealquila) > 0)
        {
            _solution.RemoveReagent(solEnt, new ReagentQuantity(new ReagentId(_gnesis, null), FixedPoint2.New(2)));
            _popup.PopupEntity(Loc.GetString("flock-gnesis-recedes"), uid, uid);
            return;
        }

        // assimilate another reagent (not blood, not tealquila - blood is handled separately below)
        var others = new List<ReagentQuantity>();
        foreach (var r in sol.Contents)
        {
            if (r.Reagent.Prototype == _gnesis ||
                r.Reagent.Prototype == _tealquila ||
                r.Reagent.Prototype == _blood ||
                r.Reagent.Prototype == _calomel ||
                r.Reagent.Prototype == _pentaericAcid)
                continue;

            others.Add(r);
        }
        if (others.Count > 0)
        {
            var pick = _random.Pick(others);
            var amt = FixedPoint2.Min(pick.Quantity, FixedPoint2.New(ConversionRate));
            _solution.RemoveReagent(solEnt, new ReagentQuantity(pick.Reagent, amt));
            _solution.TryAddReagent(solEnt, _gnesis, amt, out _);
        }

        // blood counts as raw materials when there's plenty of gnesis
        if (gnesis > 40)
        {
            var blood = sol.GetTotalPrototypeQuantity("Blood");
            var amt = FixedPoint2.Min(blood, FixedPoint2.New(ConversionRate));
            if (amt > 0)
            {
                _solution.RemoveReagent(solEnt, new ReagentQuantity(new ReagentId("Blood", null), amt));
                _solution.TryAddReagent(solEnt, _gnesis, amt, out _);
            }
        }

        if (gnesis > GibThreshold)
        {
            var chance = Math.Max(2f, ((float) gnesis - GibThreshold) / 5f) / 100f;
            if (_random.Prob(chance))
            {
                FlockbitGib(uid);
                return;
            }
        }

        // spooky stuff
        if (gnesis < 100)
        {
            if (_random.Prob(0.06f))
                _popup.PopupEntity(Loc.GetString(_random.Pick(_lowWhispers)), uid, uid);
        }
        else
        {
            if (_random.Prob(0.10f))
                _audio.PlayPvs(new SoundCollectionSpecifier("FlockDroneBeeps"), uid);
            if (_random.Prob(0.30f))
                _popup.PopupEntity(Loc.GetString(_random.Pick(_highWhispers)), uid, uid, PopupType.MediumCaution);
        }
    }

    /// <summary>The victim bursts into flockbits (goon: flockbit_gib).</summary>
    public void FlockbitGib(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        _popup.PopupCoordinates(Loc.GetString("flock-gnesis-gib", ("target", uid)), coords, PopupType.LargeCaution);
        var count = 6;
        _gibbing.Gib(uid);
        // The bits join whichever flock exists (there's normally only one).
        Entity<FlockComponent>? flock = null;
        var fq = EntityQueryEnumerator<FlockComponent>();
        if (fq.MoveNext(out var fuid, out var fcomp))
            flock = (fuid, fcomp);
        for (var i = 0; i < count; i++)
        {
            var bit = Spawn("MobFlockBit", coords.Offset(_random.NextVector2(0.8f)));
            if (flock != null)
                _flock.AddMember(flock.Value, bit);
        }
    }
}
