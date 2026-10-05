using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Mobs;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Temperature.Components;
using Content.Shared.NPC.Components;
using Robust.Server.Player;
using System.Linq;

namespace Content.Server.Blob;

public sealed partial class ZombieBlobSystem : EntitySystem
{
    private const string BlobMobTag = "BlobMob";
    private const string BlobFaction = "Blob";
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IChatManager _chatMan = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private const int ClimbingCollisionGroup = (int)CollisionGroup.BlobImpassable;

    /// <summary>
    /// Replaces the current fixtures with non-climbing collidable versions so that climb end can be detected
    /// </summary>
    private void ReplaceFixtures(EntityUid uid, ZombieBlobComponent climbingComp, FixturesComponent fixturesComp)
    {
        foreach (var (name, fixture) in fixturesComp.Fixtures)
        {
            if (climbingComp.DisabledFixtureMasks.ContainsKey(name)
                || !fixture.Hard
                || (fixture.CollisionMask & ClimbingCollisionGroup) == 0)
                continue;

            climbingComp.DisabledFixtureMasks[name] = fixture.CollisionMask & ClimbingCollisionGroup;

            _physics.SetCollisionMask(
                uid,
                name,
                fixture,
                fixture.CollisionMask & ~ClimbingCollisionGroup,
                fixturesComp);
        }
    }

    [SubscribeLocalEvent]
    private void OnStartup(EntityUid uid, ZombieBlobComponent component, ComponentStartup args)
    {
        EnsureComp<BlobMobComponent>(uid);

        var oldFactions = new List<string>();
        var factionComp = EnsureComp<NpcFactionMemberComponent>(uid);

        foreach (var factionId in factionComp.Factions.ToArray())
        {
            var id = factionId.Id;
            oldFactions.Add(id);
            _faction.RemoveFaction(uid, id);
        }

        _faction.AddFaction(uid, BlobFaction);
        component.OldFactions = oldFactions;

        var accent = EnsureComp<ReplacementAccentComponent>(uid);
        accent.Accent = "genericAggressive";

        _tagSystem.AddTag(uid, BlobMobTag);

        EnsureComp<PressureImmunityComponent>(uid);
        EnsureComp<RespiratorImmunityComponent>(uid);
        if (TryComp<TemperatureComponent>(uid, out _))
        {
            component.OldColdDamageThreshold = 0;
        }

        if (TryComp<FixturesComponent>(uid, out var fixturesComp))
        {
            ReplaceFixtures(uid, component, fixturesComp);
        }

        if (_mind.TryGetMind(uid, out var mindId, out var mind) && mind.UserId != null)
        {
            var session = _playerManager.GetSessionById(mind.UserId.Value);
            if (session != null)
            {
                _chatMan.DispatchServerMessage(session, Loc.GetString("blob-zombie-greeting"));
                _audio.PlayGlobal(component.GreetSoundNotification, session);
                return;
            }
        }

        // NPC fallback
        var htn = EnsureComp<HTNComponent>(uid);
        htn.RootTask = new HTNCompoundTask() { Task = "SimpleHostileCompound" };
        htn.Blackboard.SetValue(NPCBlackboard.Owner, uid);
        _npc.WakeNPC(uid, htn);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(EntityUid uid, ZombieBlobComponent component, ComponentShutdown args)
    {
        RemComp<BlobMobComponent>(uid);
        RemComp<HTNComponent>(uid);
        RemComp<ReplacementAccentComponent>(uid);
        RemComp<PressureImmunityComponent>(uid);
        RemComp<RespiratorImmunityComponent>(uid);

        if (TryComp<TemperatureComponent>(uid, out _) && component.OldColdDamageThreshold != null)
        {
        }

        _tagSystem.RemoveTag(uid, BlobMobTag);

        QueueDel(component.BlobPodUid);

        EnsureComp<NpcFactionMemberComponent>(uid);

        foreach (var factionId in component.OldFactions)
        {
            _faction.AddFaction(uid, factionId);
        }

        _faction.RemoveFaction(uid, BlobFaction);

        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (name, fixtureMask) in component.DisabledFixtureMasks)
            {
                if (!fixtures.Fixtures.TryGetValue(name, out var fixture))
                    continue;

                _physics.SetCollisionMask(
                    uid,
                    name,
                    fixture,
                    fixture.CollisionMask | fixtureMask);
            }

            component.DisabledFixtureMasks.Clear();
        }
    }

    [SubscribeLocalEvent]
    private void OnMobStateChanged(EntityUid uid, ZombieBlobComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            RemComp<ZombieBlobComponent>(uid);
        }
    }
}
