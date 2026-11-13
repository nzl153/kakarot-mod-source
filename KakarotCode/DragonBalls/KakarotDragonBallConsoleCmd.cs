#nullable enable
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KakarotMod.KakarotCode.DragonBalls;

// 控制台：dragonballs <数量> [earth|namek|super|blackstar]。带上套名会同时换套并清掉已许的愿望。
public class KakarotDragonBallConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dragonballs";
    public override string Args => "<count:int> [set:earth|namek|super|blackstar]";
    public override string Description => "Set Kakarot's Dragon Ball count, optionally switching set and clearing the wish.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var relic = issuingPlayer?.GetRelic<KakarotSevenDragonBalls>();
        if (relic == null)
        {
            return new CmdResult(success: false, "This player has no Seven Dragon Balls relic.");
        }

        if (args.Length < 1 || !int.TryParse(args[0], out int count))
        {
            return new CmdResult(success: false, "A ball count is required.");
        }

        KakarotDragonBallSet? set = null;
        if (args.Length > 1)
        {
            if (!TryParseEnum(args[1], out KakarotDragonBallSet parsed) || parsed == KakarotDragonBallSet.None)
            {
                return new CmdResult(success: false, "Set must be earth, namek, super or blackstar.");
            }

            set = parsed;
        }

        relic.DebugSet(count, set);
        return new CmdResult(success: true, $"Dragon Balls: {relic.SelectedSet} {relic.BallCount}/7");
    }
}
