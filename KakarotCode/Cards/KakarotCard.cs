using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Godot;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Exceptions;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Cards;

[Pool(typeof(KakarotCardPool))]
public abstract class KakarotCard(int cost, CardType type, CardRarity rarity, TargetType target) : CustomCardModel(cost, type, rarity, target)
{
    private static readonly ConcurrentDictionary<string, string> PortraitPathCache = new();
    private static readonly ConcurrentDictionary<string, string> BigPortraitPathCache = new();

    protected static IHoverTip WildKeywordHoverTip => HoverTipFactory.FromKeyword(KakarotWildKeyword.Wild);

    protected override bool IsPlayable
    {
        get
        {
            if (!base.IsPlayable)
            {
                return false;
            }

            var hand = Owner?.PlayerCombatState?.Hand?.Cards;
            if (hand == null || hand.Count == 0)
            {
                return true;
            }

            var hasOutOfControl = hand.Any(c => c is KakarotOutOfControl);
            return !hasOutOfControl || this is KakarotOutOfControl;
        }
    }

    /// <summary>
    /// 卡面黄色关键字行来自此处。运行时 <c>AddKeyword(Wild)</c> 只更新 <c>Keywords</c>，故在此把野性并入展示列表。
    /// 子类若完全重写本属性，需自行合并野性（或调用 <see cref="KakarotWildDisplay.MergeWild"/>）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            IEnumerable<CardKeyword> intrinsic;
            try
            {
                intrinsic = base.CanonicalKeywords;
            }
            catch (CanonicalModelException)
            {
                intrinsic = [];
            }
            catch (MutableModelException)
            {
                intrinsic = [];
            }

            try
            {
                // Compendium/history canonical cards have no combat owner; skip runtime keyword merge path.
                if (Owner?.PlayerCombatState == null)
                {
                    return intrinsic;
                }

                if (Keywords != null && Keywords.Contains(KakarotWildKeyword.Wild))
                {
                    var list = intrinsic.ToList();
                    if (!list.Contains(KakarotWildKeyword.Wild))
                    {
                        list.Add(KakarotWildKeyword.Wild);
                    }

                    return list;
                }
            }
            catch (CanonicalModelException)
            {
            }
            catch (MutableModelException)
            {
            }

            return intrinsic;
        }
    }

    public override string CustomPortraitPath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return PortraitPath;
            }

            return BigPortraitPathCache.GetOrAdd(entry, e =>
            {
                var framedPath = $"{e.RemovePrefix().ToLowerInvariant()}.tres".FramedBigCardImagePath();
                if (ResourceLoader.Exists(framedPath))
                {
                    return framedPath;
                }

                var bigPath = $"{e.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
                return ResourceLoader.Exists(bigPath) ? bigPath : PortraitPath;
            });
        }
    }

    public override string PortraitPath
    {
        get
        {
            var entry = Id.Entry;
            string Fallback() => Type switch
            {
                CardType.Attack => ImageHelper.GetImagePath("packed/card_portraits/ironclad/strike_ironclad.png"),
                _ => ImageHelper.GetImagePath("packed/card_portraits/ironclad/defend_ironclad.png")
            };

            if (string.IsNullOrEmpty(entry))
            {
                return Fallback();
            }

            return PortraitPathCache.GetOrAdd(entry, e =>
            {
                var customPath = $"{e.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
                return ResourceLoader.Exists(customPath) ? customPath : Fallback();
            });
        }
    }

    public override string BetaPortraitPath => PortraitPath;
}

[Pool(typeof(ColorlessCardPool))]
public abstract class KakarotCoopColorlessCard(int cost, CardType type, CardRarity rarity, TargetType target) : CustomCardModel(cost, type, rarity, target)
{
    private static readonly ConcurrentDictionary<string, string> PortraitPathCache = new();
    private static readonly ConcurrentDictionary<string, string> BigPortraitPathCache = new();

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    /// <summary>
    /// Keep this true so multiplayer colorless generation paths can resolve this card.
    /// Single-player paths filter it through <see cref="MultiplayerConstraint"/> first.
    /// </summary>
    public override bool CanBeGeneratedInCombat => true;

    /// <summary>
    /// 联机中的药水、升级与其它修饰符仍可生成；没有队友时由原版联机约束从候选池排除。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => true;

    protected override bool IsPlayable => base.IsPlayable && HasTeammateInCombat();

    public override string CustomPortraitPath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return PortraitPath;
            }

            return BigPortraitPathCache.GetOrAdd(entry, e =>
            {
                var framedPath = $"{e.RemovePrefix().ToLowerInvariant()}.tres".FramedBigCardImagePath();
                if (ResourceLoader.Exists(framedPath))
                {
                    return framedPath;
                }

                var bigPath = $"{e.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
                return ResourceLoader.Exists(bigPath) ? bigPath : PortraitPath;
            });
        }
    }

    public override string PortraitPath
    {
        get
        {
            var entry = Id.Entry;
            string Fallback() => Type switch
            {
                CardType.Attack => ImageHelper.GetImagePath("packed/card_portraits/colorless/panic_button.png"),
                _ => ImageHelper.GetImagePath("packed/card_portraits/colorless/jack_of_all_trades.png")
            };

            if (string.IsNullOrEmpty(entry))
            {
                return Fallback();
            }

            return PortraitPathCache.GetOrAdd(entry, e =>
            {
                var customPath = $"{e.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
                return ResourceLoader.Exists(customPath) ? customPath : Fallback();
            });
        }
    }

    public override string BetaPortraitPath => PortraitPath;

    private bool HasTeammateInCombat()
    {
        var combatState = Owner?.Creature?.CombatState;
        if (combatState == null)
        {
            return false;
        }
        return combatState.Players.Any(p => p != null && p != Owner);
    }
}
