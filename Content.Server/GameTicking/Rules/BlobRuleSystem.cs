using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Nuke;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Blob;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Server.Player;

namespace Content.Server.GameTicking.Rules;

public sealed partial class BlobRuleSystem : GameRuleSystem<BlobRuleComponent>
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private NukeCodePaperSystem _nukeCode = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("blob-rule");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var blobRuleQuery = EntityQueryEnumerator<BlobRuleComponent>();
        while (blobRuleQuery.MoveNext(out _, out var blobRule))
        {
            var blobCoreQuery = EntityQueryEnumerator<BlobCoreComponent>();

            while (blobCoreQuery.MoveNext(out var coreUid, out var core))
            {
                if (core.BlobTiles.Count >= 50)
                {
                    if (_roundEndSystem.ExpectedCountdownEnd != null)
                    {
                        _roundEndSystem.CancelRoundEndCountdown();

                        _chatSystem.DispatchGlobalAnnouncement(
                            Loc.GetString("blob-alert-recall-shuttle"),
                            Loc.GetString("Station"),
                            false,
                            null,
                            Color.Red);
                    }
                }

                switch (blobRule.Stage)
                {
                    case BlobStage.Default when core.BlobTiles.Count < 50:
                        continue;

                    case BlobStage.Default:
                        _chatSystem.DispatchGlobalAnnouncement(
                            Loc.GetString("blob-alert-detect"),
                            Loc.GetString("Station"),
                            true,
                            blobRule.AlertAudio,
                            Color.Red);

                        blobRule.Stage = BlobStage.Begin;
                        break;

                    case BlobStage.Begin:
                        if (core.BlobTiles.Count >= 300)
                        {
                            _chatSystem.DispatchGlobalAnnouncement(
                                Loc.GetString("blob-alert-critical"),
                                Loc.GetString("Station"),
                                true,
                                blobRule.AlertAudio,
                                Color.Red);

                            var stationUid = _stationSystem.GetOwningStation(coreUid);

                            if (stationUid != null)
                                _nukeCode.SendNukeCodes(stationUid.Value);

                            blobRule.Stage = BlobStage.Critical;
                        }
                        break;

                    case BlobStage.Critical:
                        if (core.BlobTiles.Count >= 400)
                        {
                            core.Points = 99999;
                            _roundEndSystem.EndRound();
                        }
                        break;
                }
            }
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = EntityQueryEnumerator<BlobRuleComponent, GameRuleComponent>();

        while (query.MoveNext(out var uid, out var blobRule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (blobRule.Blobs.Count < 1)
                return;

            var result = Loc.GetString("blob-round-end-result",
                ("blobCount", blobRule.Blobs.Count));

            foreach (var mindUid in blobRule.Blobs)
            {
                if (!TryComp<MindComponent>(mindUid, out var mind))
                    continue;

                var characterName = mind.CharacterName;

                if (mind.UserId == null)
                    continue;

                _player.TryGetSessionById(mind.UserId.Value, out var session);
                var username = session?.Name;

                var objectives = mind.Objectives.ToArray();

                if (objectives.Length == 0)
                {
                    if (username != null)
                    {
                        result += characterName == null
                            ? "\n" + Loc.GetString("blob-user-was-a-blob", ("user", username))
                            : "\n" + Loc.GetString("blob-user-was-a-blob-named",
                                ("user", username),
                                ("name", characterName));
                    }
                    else if (characterName != null)
                    {
                        result += "\n" + Loc.GetString("blob-was-a-blob-named",
                            ("name", characterName));
                    }

                    continue;
                }

                if (username != null)
                {
                    result += characterName == null
                        ? "\n" + Loc.GetString("blob-user-was-a-blob-with-objectives",
                            ("user", username))
                        : "\n" + Loc.GetString("blob-user-was-a-blob-with-objectives-named",
                            ("user", username),
                            ("name", characterName));
                }
                else if (characterName != null)
                {
                    result += "\n" + Loc.GetString("blob-was-a-blob-with-objectives-named",
                        ("name", characterName));
                }

                foreach (var objectiveUid in mind.Objectives)
                {
                    if (!TryComp<ObjectiveComponent>(objectiveUid, out _))
                        continue;

                    var title = MetaData(objectiveUid).EntityName;

                    result += "\n- " + Loc.GetString(
                        "traitor-objective-condition-success",
                        ("condition", title),
                        ("markupColor", "green"));
                }
            }

            ev.AddLine(result);
        }
    }
}
