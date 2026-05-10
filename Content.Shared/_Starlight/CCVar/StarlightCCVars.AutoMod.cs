using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<bool> AutoModEnabled =
        CVarDef.Create("automod.enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<bool> AutoModShadowMode =
        CVarDef.Create("automod.shadow", true, CVar.SERVERONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<bool> AutoModNullLinkEnabled =
        CVarDef.Create("automod.null_link.enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<bool> AutoModDiscordEnabled =
        CVarDef.Create("automod.discord.enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<string> AutoModDiscordWebhook =
        CVarDef.Create("automod.discord.webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<bool> AutoModRequireApprovalForBans =
        CVarDef.Create("automod.require_approval_for_bans", true, CVar.SERVERONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<bool> AutoModIgnoreAdmined =
        CVarDef.Create("automod.ignore_admined", true, CVar.SERVERONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.Admin)]
    public static readonly CVarDef<int> AutoModDiscordBatchSeconds =
        CVarDef.Create("automod.discord.batch_seconds", 300, CVar.SERVERONLY | CVar.ARCHIVE);
}
