#nullable enable
using System.Collections.Generic;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Monsters.Frieza;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.Encounters;

public sealed class FriezaBossEncounter : CustomEncounterModel
{
    private static readonly string[] SlotNames =
        ["support1", "support2", "support3", "support4", "frieza"];

    public FriezaBossEncounter() : base(RoomType.Boss)
    {
    }

    public override string CustomScenePath =>
        "res://scenes/encounters/kakarotmod-frieza_boss_encounter.tscn";
    public override BackgroundAssets CustomEncounterBackground(ActModel parentAct, Rng rng) =>
        new("kakarotmod_frieza", rng);
    public override bool ShouldGiveRewards => false;
    public override IReadOnlyList<string> Slots => SlotNames;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<FriezaBoss>(),
        ModelDb.Monster<FriezaGuldo>(),
        ModelDb.Monster<FriezaRecoome>(),
        ModelDb.Monster<FriezaCaptainGinyu>(),
        ModelDb.Monster<FriezaBurterJeice>(),
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<FriezaBoss>().ToMutable(), "frieza"),
    ];

    public override bool IsValidForAct(ActModel act) => false;
}
