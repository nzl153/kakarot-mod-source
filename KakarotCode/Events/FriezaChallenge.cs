#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Encounters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Events;

public sealed class FriezaChallenge : CustomEventModel
{
    public override string CustomInitialPortraitPath =>
        "res://images/events/kakarotmod-frieza_challenge.png";

    public override bool IsShared => true;

    public override bool IsAllowed(MegaCrit.Sts2.Core.Runs.IRunState runState)
    {
        // This challenge is injected after the final act and must never enter a normal event pool.
        return false;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, AcceptChallenge, $"{Id.Entry}.pages.INITIAL.options.ACCEPT"),
            new EventOption(this, DeclineChallenge, $"{Id.Entry}.pages.INITIAL.options.DECLINE"),
        ];
    }

    public override Task Resume(AbstractRoom room)
    {
        SetEventState(
            L10NLookup($"{Id.Entry}.pages.VICTORY.description"),
            [
                new EventOption(
                    this,
                    ScheduleEnterArchitect,
                    "PROCEED",
                    disableOnChosen: false,
                    isProceed: true),
            ]);
        return Task.CompletedTask;
    }

    private async Task AcceptChallenge()
    {
        if (Owner == null)
        {
            return;
        }

        if (LocalContext.IsMe(Owner))
        {
            foreach (var player in Owner.RunState.Players.Where(static player => player.Creature.IsAlive))
            {
                await CreatureCmd.Heal(player.Creature, player.Creature.MaxHp - player.Creature.CurrentHp);
            }
        }

        EnterCombatWithoutExitingEvent<FriezaBossEncounter>(
            Array.Empty<Reward>(),
            shouldResumeAfterCombat: true);
    }

    private Task DeclineChallenge()
    {
        return ScheduleEnterArchitect();
    }

    private Task ScheduleEnterArchitect()
    {
        if (Owner == null || !LocalContext.IsMe(Owner))
        {
            return Task.CompletedTask;
        }

        // Leaving an event from inside its option task deadlocks EventRoom.Exit.
        // Run the transition next frame, after the option task has completed.
        Callable.From(() =>
        {
            TaskHelper.RunSafely(EnterArchitect());
        }).CallDeferred();
        return Task.CompletedTask;
    }

    private Task EnterArchitect()
    {
        if (Owner == null || !LocalContext.IsMe(Owner))
        {
            return Task.CompletedTask;
        }

        return RunManager.Instance.EnterRoom(new EventRoom(ModelDb.Event<TheArchitect>()));
    }
}
