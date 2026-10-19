using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;

namespace DOL.GS.ServerRules
{
    /// <summary>
    /// Enforces the White Council's Two-Tier Admissions Policy at the Inner Threshold
    /// of the Seven Laws (Region 65 Tier 1 Catacombs -> Region 192 Tier 2 Grand Conclave Hall).
    /// </summary>
    public class EdinburghConclaveJumpPoint : IJumpPointHandler
    {
        public bool IsAllowedToJump(DbZonePoint targetPoint, GamePlayer player)
        {
            if (player == null)
                return false;

            if (player.Client?.Account?.PrivLevel > 1)
                return true;

            var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
            if (access.IsKillOnSight)
            {
                player.Out.SendMessage(
                    EdinburghAdmissionsPolicy.GetConclaveDenialMessage(access),
                    eChatType.CT_Important,
                    eChatLoc.CL_SystemWindow);
                EdinburghAndNeverneverWorld.AlertNearbyWardens(player);
                return false;
            }

            if (!access.CanEnterTier2)
            {
                player.Out.SendMessage(
                    EdinburghAdmissionsPolicy.GetConclaveDenialMessage(access),
                    eChatType.CT_Say,
                    eChatLoc.CL_PopupWindow);
                return false;
            }

            return true;
        }
    }
}

namespace DOL.GS
{
    public enum EdinburghAccessRole
    {
        HumanWarden,
        HumanCouncilWizard,
        TreatyEmissary,
        DivineObserver,
        MundaneGuest,
        ForeignVisitor,
        FifthLawViolator
    }

    public readonly record struct EdinburghAccessResult(
        EdinburghAccessRole Role,
        bool CanEnterTier1,
        bool CanEnterTier2,
        bool IsKillOnSight,
        string TitleInEdinburgh);

    /// <summary>
    /// Implements the Class & Racial Admissions Matrix for Edinburgh (Section 3 of edinburgh_and_nevernever_plan.md).
    /// </summary>
    public static class EdinburghAdmissionsPolicy
    {
        public const string NonHumanConclaveDenialText =
            "Hold, honored emissary. Beyond these doors sits the assembly of mortal wizards under the Laws of Merlin. Your quarters lie within the Treaty Cloisters.";

        public const string FifthLawCondemnationText =
            "Thou shalt not reach beyond the borders of life! The sentence for your magic is death, warlock!";

        public const string UnescortedMundaneDenialText =
            "Hold, traveler. Beyond these doors sits the Grand Conclave of the White Council. Only Council Wizards, Wardens, honored clerical observers, or guests escorted by a Council member may enter.";

        public static bool IsMortalHumanRace(eRace race) =>
            race is eRace.Briton or eRace.Highlander or eRace.Saracen or eRace.Avalonian or eRace.Norseman or eRace.Celt;

        public static bool IsFifthLawViolatorClass(eCharacterClass charClass) =>
            charClass is eCharacterClass.Necromancer or eCharacterClass.Bonedancer or eCharacterClass.Sluaghbinder or eCharacterClass.Spiritmaster;

        public static bool IsCouncilWardenClass(eCharacterClass charClass) =>
            charClass is eCharacterClass.MaulerAlb or eCharacterClass.MaulerMid or eCharacterClass.Vampiir;

        public static bool IsArcaneOrPrimalCasterClass(eCharacterClass charClass) =>
            OpenWayResonanceTable.IsEligibleCasterClass((int)charClass) && !IsFifthLawViolatorClass(charClass);

        public static bool IsDivineObserverClass(eCharacterClass charClass) =>
            charClass is eCharacterClass.Cleric or eCharacterClass.Healer or eCharacterClass.Friar
                or eCharacterClass.Acolyte or eCharacterClass.Seer;

        public static EdinburghAccessResult Evaluate(eRace race, eCharacterClass charClass, bool isEscortedByCouncilMember = false)
        {
            if (IsFifthLawViolatorClass(charClass))
            {
                return new EdinburghAccessResult(
                    EdinburghAccessRole.FifthLawViolator,
                    CanEnterTier1: false,
                    CanEnterTier2: false,
                    IsKillOnSight: true,
                    TitleInEdinburgh: "Fifth Law Violator (KOS)");
            }

            bool isHuman = IsMortalHumanRace(race);
            if (isHuman)
            {
                if (IsCouncilWardenClass(charClass))
                {
                    return new EdinburghAccessResult(
                        EdinburghAccessRole.HumanWarden,
                        CanEnterTier1: true,
                        CanEnterTier2: true,
                        IsKillOnSight: false,
                        TitleInEdinburgh: "Warden of the White Council");
                }

                if (IsArcaneOrPrimalCasterClass(charClass))
                {
                    return new EdinburghAccessResult(
                        EdinburghAccessRole.HumanCouncilWizard,
                        CanEnterTier1: true,
                        CanEnterTier2: true,
                        IsKillOnSight: false,
                        TitleInEdinburgh: "Full Voting Wizard of the Council");
                }

                if (IsDivineObserverClass(charClass))
                {
                    return new EdinburghAccessResult(
                        EdinburghAccessRole.DivineObserver,
                        CanEnterTier1: true,
                        CanEnterTier2: true,
                        IsKillOnSight: false,
                        TitleInEdinburgh: "Honored Clerical Observer");
                }

                return new EdinburghAccessResult(
                    EdinburghAccessRole.MundaneGuest,
                    CanEnterTier1: true,
                    CanEnterTier2: isEscortedByCouncilMember,
                    IsKillOnSight: false,
                    TitleInEdinburgh: isEscortedByCouncilMember ? "Escorted Council Retainer" : "Common Tavern Guest");
            }

            // Non-Human Races (Lurikeen, Elf, Dwarf, Kobold, Inconnu, Firbolg, Troll, etc.)
            if (IsArcaneOrPrimalCasterClass(charClass) || IsDivineObserverClass(charClass))
            {
                return new EdinburghAccessResult(
                    EdinburghAccessRole.TreatyEmissary,
                    CanEnterTier1: true,
                    CanEnterTier2: false,
                    IsKillOnSight: false,
                    TitleInEdinburgh: "Accredited Treaty Scholar & Emissary");
            }

            return new EdinburghAccessResult(
                EdinburghAccessRole.ForeignVisitor,
                CanEnterTier1: true,
                CanEnterTier2: false,
                IsKillOnSight: false,
                TitleInEdinburgh: "Foreign Treaty Visitor");
        }

        public static EdinburghAccessResult EvaluatePlayer(GamePlayer player)
        {
            if (player == null)
                return Evaluate(eRace.Briton, eCharacterClass.Fighter, false);

            eRace race = (eRace)player.Race;
            eCharacterClass charClass = (eCharacterClass)(player.CharacterClass?.ID ?? 0);

            bool escorted = false;
            if (player.Group != null)
            {
                foreach (GamePlayer member in player.Group.GetPlayersInTheGroup())
                {
                    if (member != null && member != player)
                    {
                        var memberRole = Evaluate((eRace)member.Race, (eCharacterClass)(member.CharacterClass?.ID ?? 0), false);
                        if (memberRole.Role is EdinburghAccessRole.HumanWarden or EdinburghAccessRole.HumanCouncilWizard)
                        {
                            escorted = true;
                            break;
                        }
                    }
                }
            }

            return Evaluate(race, charClass, escorted);
        }

        public static string GetConclaveDenialMessage(EdinburghAccessResult result) =>
            result.Role switch
            {
                EdinburghAccessRole.FifthLawViolator => FifthLawCondemnationText,
                EdinburghAccessRole.TreatyEmissary or EdinburghAccessRole.ForeignVisitor => NonHumanConclaveDenialText,
                _ => UnescortedMundaneDenialText
            };
    }

    /// <summary>
    /// Implements the "Warded Cobblestone" System in The Nevernever (Region 197).
    /// While a traveler sticks strictly to the ancient warded cobblestone path between Waystones
    /// and the Edinburgh portal, hostile Fae predators have a dramatically reduced aggro radius (75 units).
    /// Straying off the road dissolves the wards, exposing the traveler to full-range Fae ambushes.
    /// </summary>
    public static class NeverneverWardedRoad
    {
        public const ushort NeverneverRegionId = 197;
        public const int RoadHalfWidth = 300;
        public const int WaystoneSanctuaryRadius = 500;
        public const int WardedRoadAggroRadius = 75;

        public static readonly (int X, int Y) EdinburghPortalNode = (32000, 32000);
        public static readonly (int X, int Y) CentralCrossroadsNode = (34134, 34920);
        public static readonly (int X, int Y) SummerWaystoneNode = (42134, 34920);
        public static readonly (int X, int Y) SummerCourtGateNode = (49917, 32592);
        public static readonly (int X, int Y) WesternArchNode = (26134, 34920);
        public static readonly (int X, int Y) WinterWaystoneNode = (26134, 26920);
        public static readonly (int X, int Y) WinterCourtGateNode = (19332, 24042);

        private static readonly ((int X, int Y) Start, (int X, int Y) End)[] WardedSegments =
        {
            // Central Edinburgh Portal to Crossroads and East to Summer Waystone & Summer Court Gate
            (EdinburghPortalNode, CentralCrossroadsNode),
            (CentralCrossroadsNode, SummerWaystoneNode),
            (SummerWaystoneNode, SummerCourtGateNode),
            // Central Edinburgh Portal West and South to Winter Waystone & Winter Court Gate
            (EdinburghPortalNode, WesternArchNode),
            (WesternArchNode, WinterWaystoneNode),
            (WinterWaystoneNode, WinterCourtGateNode),
            // North-South Ley Causeway along the Central Crossing
            ((34134, 26920), CentralCrossroadsNode),
            (CentralCrossroadsNode, (34134, 42920))
        };

        private static readonly (int X, int Y)[] WaystoneSanctuaries =
        {
            EdinburghPortalNode,
            CentralCrossroadsNode,
            SummerWaystoneNode,
            WinterWaystoneNode
        };

        public static double GetDistanceToNearestSegment(int x, int y)
        {
            double minDistance = double.MaxValue;
            foreach (var (start, end) in WardedSegments)
            {
                double dist = DistancePointToSegment(x, y, start.X, start.Y, end.X, end.Y);
                if (dist < minDistance)
                    minDistance = dist;
            }
            return minDistance;
        }

        public static bool IsOnWardedRoad(int regionId, int x, int y)
        {
            if (regionId != NeverneverRegionId)
                return false;

            foreach (var node in WaystoneSanctuaries)
            {
                long dx = x - node.X;
                long dy = y - node.Y;
                if (dx * dx + dy * dy <= (long)WaystoneSanctuaryRadius * WaystoneSanctuaryRadius)
                    return true;
            }

            return GetDistanceToNearestSegment(x, y) <= RoadHalfWidth;
        }

        public static int GetEffectiveAggroRange(int regionId, int targetX, int targetY, int baseAggroRange)
        {
            if (baseAggroRange <= 0)
                return 0;

            if (IsOnWardedRoad(regionId, targetX, targetY))
                return Math.Min(baseAggroRange, WardedRoadAggroRadius);

            return baseAggroRange;
        }

        public static bool IsProtectedFromMobAggro(GameNPC mob, GameLiving target)
        {
            if (mob == null || target == null || mob.CurrentRegionID != NeverneverRegionId)
                return false;

            GameLiving realTarget = StandardMobBrain.NaturalAggroIdentity(target) ?? target;
            if (!IsOnWardedRoad(realTarget.CurrentRegionID, realTarget.X, realTarget.Y))
                return false;

            return !mob.IsWithinRadius(realTarget, WardedRoadAggroRadius);
        }

        private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double lenSq = dx * dx + dy * dy;
            if (lenSq <= 1e-6)
            {
                double ex = px - x1;
                double ey = py - y1;
                return Math.Sqrt(ex * ex + ey * ey);
            }

            double t = ((px - x1) * dx + (py - y1) * dy) / lenSq;
            t = Math.Clamp(t, 0.0, 1.0);
            double projX = x1 + t * dx;
            double projY = y1 + t * dy;
            double distX = px - projX;
            double distY = py - projY;
            return Math.Sqrt(distX * distX + distY * distY);
        }
    }

    /// <summary>
    /// White Council Warden Sentry guarding Edinburgh's thresholds and halls.
    /// Friendly/neutral to all realms and treaty visitors, but executes 5th Law violators
    /// (Necromancer, Bonedancer, Sluaghbinder) on sight.
    /// </summary>
    public class WardenSentryNPC : GameNPC
    {
        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            eQuestIndicator indicator = base.GetQuestIndicator(player);
            if (indicator != eQuestIndicator.None)
                return indicator;

            return DOL.GS.Quests.CouncilWardenEpicQuestRuntime.GetTrainerQuestIndicator(player, this);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 5000);
            var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
            if (access.IsKillOnSight)
            {
                Yell(EdinburghAdmissionsPolicy.FifthLawCondemnationText);
                StartAttack(player);
                return true;
            }

            string greeting = access.Role switch
            {
                EdinburghAccessRole.HumanWarden =>
                    $"Stand fast, Warden {player.Name}. The grey cloak commands respect in these halls. Lord-Commander Caelan Vance awaits your report in the Grand Conclave.",
                EdinburghAccessRole.HumanCouncilWizard =>
                    $"Welcome home to Edinburgh, Wizard {player.Name}. Both the Catacombs and the Grand Conclave Hall stand open to you.",
                EdinburghAccessRole.TreatyEmissary =>
                    $"Greetings, {access.TitleInEdinburgh} {player.Name}. The Diplomatic Cloisters and Treaty Trainers in the Catacombs are at your disposal. Remember that the Grand Conclave Hall beyond the Inner Threshold is reserved for mortal Council wizards.",
                EdinburghAccessRole.DivineObserver =>
                    $"Peace upon you, {player.Name}. As an honored clerical observer, you may walk both the Cloisters and the Conclave gallery.",
                _ =>
                    $"Welcome to the Sanctuary of Edinburgh, {player.Name}. Keep the peace of the White Council while within these walls."
            };

            SayTo(player, greeting);
            if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(this))
            {
                DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerInteraction(player, this);
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(this) &&
                DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerWhisper(player, this, text))
            {
                return true;
            }

            return true;
        }
    }

    public class WardenSentryBrain : StandardMobBrain
    {
        public WardenSentryBrain()
        {
            AggroLevel = 100;
            AggroRange = 650;
            ThinkInterval = 1500;
        }

        public static bool ShouldAttackTarget(GameLiving target)
        {
            if (target == null || !target.IsAlive)
                return false;

            GameLiving identity = NaturalAggroIdentity(target) ?? target;
            if (identity is GamePlayer player)
            {
                if (player.Client?.Account?.PrivLevel > 1)
                    return false;
                return EdinburghAdmissionsPolicy.EvaluatePlayer(player).IsKillOnSight;
            }

            if (identity is GameBot bot)
                return EdinburghAdmissionsPolicy.IsFifthLawViolatorClass((eCharacterClass)bot.CharacterClass.ID);

            return false;
        }

        public override bool CanAggroTarget(GameLiving target)
        {
            return ShouldAttackTarget(target);
        }

        protected override void CheckPlayerAggro()
        {
            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)AggroRange))
            {
                if (!ShouldAttackTarget(player))
                    continue;

                Body.Yell(EdinburghAdmissionsPolicy.FifthLawCondemnationText);
                AddToAggroList(player, 1000);
                Body.StartAttack(player);
                return;
            }
        }
    }

    /// <summary>
    /// Interactive Waystone / Threshold portal connecting Hadrian's Wall, Edinburgh Tier 1,
    /// Edinburgh Tier 2, and the Summer/Winter/Central Waystones of The Nevernever.
    /// </summary>
    public class EdinburghWaystonePortalNPC : GameNPC
    {
        public ushort DestinationRegion { get; set; }
        public int DestinationX { get; set; }
        public int DestinationY { get; set; }
        public int DestinationZ { get; set; }
        public ushort DestinationHeading { get; set; }
        public bool RequiresTier2ConclaveAccess { get; set; }
        public bool IsNeverneverHubWaystone { get; set; }
        public bool RequiresOpenWay { get; set; }

        public static bool CanPlayerOpenWay(GamePlayer player)
        {
            if (player == null)
                return false;

            if (player.Client?.Account?.PrivLevel > 1)
                return true;

            // Council Wardens and Arcane/Primal Casters at Level >= 5 know the Open Way spell
            if (player.Level >= 5 && OpenWayResonanceTable.IsEligibleCasterClass(player.CharacterClass?.ID ?? 0))
                return true;

            // Check if player has a Silver Thimble of the Veil in inventory
            if (player.Inventory != null)
            {
                for (int i = (int)eInventorySlot.FirstBackpack; i <= (int)eInventorySlot.LastBackpack; i++)
                {
                    var item = player.Inventory.GetItem((eInventorySlot)i);
                    if (item != null && (item.Name.Contains("Silver Thimble") || item.Id_nb == "silver_thimble_veil"))
                        return true;
                }
            }

            return false;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (player.InCombat || GameRelic.IsPlayerCarryingRelic(player))
            {
                player.Out.SendMessage("You cannot pass through the warded threshold while in combat!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }

            var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
            if (access.IsKillOnSight && (DestinationRegion is 65 or 192 || CurrentRegionID is 65 or 192))
            {
                player.Out.SendMessage(EdinburghAdmissionsPolicy.FifthLawCondemnationText, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                EdinburghAndNeverneverWorld.AlertNearbyWardens(player);
                return true;
            }

            if (RequiresTier2ConclaveAccess && !access.CanEnterTier2 && player.Client?.Account?.PrivLevel <= 1)
            {
                SayTo(player, EdinburghAdmissionsPolicy.GetConclaveDenialMessage(access));
                return true;
            }

            if (RequiresOpenWay && !CanPlayerOpenWay(player))
            {
                SayTo(player,
                    "The menhir's silver runes remain cold and dormant. Ancient wards seal this gateway. " +
                    "Only a caster channeling the Open Way spell (Apertura Viae) or carrying a Silver Thimble of the Veil can align the resonances to open this threshold into Edinburgh.");
                return true;
            }

            if (RequiresOpenWay)
            {
                SayTo(player,
                    "The silver runes flare in response to your arcane resonance! Whisper [enter edinburgh] or step through to enter the Catacombs of Edinburgh.");
                return true;
            }

            if (IsNeverneverHubWaystone)
            {
                SayTo(player,
                    "The ancient Waystones of the Nevernever hub hum with ley-line energy along the Warded Cobblestone Road.\n" +
                    "Whisper [cotswold] for Albion, [mularn] for Midgard, [mag mell] for Hibernia, [hillfort] for Hadrian's Wall, or [edinburgh] for the Council Sanctuary.");
                return true;
            }

            if (DestinationRegion > 0 && WorldMgr.GetRegion(DestinationRegion) != null)
            {
                player.LeaveHouse();
                player.MoveTo(DestinationRegion, DestinationX, DestinationY, DestinationZ, DestinationHeading);
            }
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            if (player.InCombat || GameRelic.IsPlayerCarryingRelic(player))
            {
                player.Out.SendMessage("You cannot traverse the Ways while in combat!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }

            string cmd = text.Trim().ToLowerInvariant();
            if (cmd is "edinburgh" or "enter edinburgh" or "catacombs" or "sanctuary" or "open way")
            {
                var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
                if (access.IsKillOnSight)
                {
                    player.Out.SendMessage(EdinburghAdmissionsPolicy.FifthLawCondemnationText, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    EdinburghAndNeverneverWorld.AlertNearbyWardens(player);
                    return true;
                }

                if (CurrentRegionID == 197 && !CanPlayerOpenWay(player))
                {
                    SayTo(player,
                        "The menhir remains dormant. Ancient wards seal this gateway. You must channel the Open Way spell (Apertura Viae) or possess a Silver Thimble of the Veil to enter Edinburgh.");
                    return true;
                }

                var dest = OpenWayResonanceTable.EdinburghWaysIngress;
                if (WorldMgr.GetRegion(dest.RegionId) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(dest.RegionId, dest.X, dest.Y, dest.Z, dest.Heading);
                }
                return true;
            }

            if (cmd is "cotswold" or "albion")
            {
                var dest = OpenWayResonanceTable.AlbionMortalNexus;
                if (WorldMgr.GetRegion(dest.RegionId) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(dest.RegionId, dest.X, dest.Y, dest.Z, dest.Heading);
                }
                return true;
            }

            if (cmd is "mularn" or "midgard")
            {
                var dest = OpenWayResonanceTable.MidgardMortalNexus;
                if (WorldMgr.GetRegion(dest.RegionId) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(dest.RegionId, dest.X, dest.Y, dest.Z, dest.Heading);
                }
                return true;
            }

            if (cmd is "mag mell" or "magmell" or "hibernia")
            {
                var dest = OpenWayResonanceTable.HiberniaMortalNexus;
                if (WorldMgr.GetRegion(dest.RegionId) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(dest.RegionId, dest.X, dest.Y, dest.Z, dest.Heading);
                }
                return true;
            }

            if (cmd is "hillfort" or "hadrian" or "frontier")
            {
                var dest = OpenWayResonanceTable.FrontierHillfortNexus;
                if (WorldMgr.GetRegion(dest.RegionId) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(dest.RegionId, dest.X, dest.Y, dest.Z, dest.Heading);
                }
                return true;
            }

            if (cmd is "summer" or "summer court" or "seelie")
            {
                if (WorldMgr.GetRegion(196) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(196, 25752, 21072, 16000, 2048);
                }
                return true;
            }

            if (cmd is "winter" or "winter court" or "unseelie")
            {
                if (WorldMgr.GetRegion(195) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(195, 32852, 14647, 16000, 2048);
                }
                return true;
            }

            if (cmd is "mortal" or "mortal world" or "return")
            {
                var safeDest = OpenWayResonanceTable.GetDefaultMortalNexus(player.Realm);
                if (WorldMgr.GetRegion(safeDest.RegionId) != null)
                {
                    player.LeaveHouse();
                    player.MoveTo(safeDest.RegionId, safeDest.X, safeDest.Y, safeDest.Z, safeDest.Heading);
                }
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Eccentric Hedge-Witch peddling the black-market "Silver Thimble of the Veil"
    /// so non-caster classes (Warriors, Rogues, Paladins) can enter Faerie directly.
    /// </summary>
    public class BlackMarketTalismanPeddlerNPC : GameNPC
    {
        public const long ThimblePriceCopper = 50000; // 5 gold

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 5000);
            SayTo(player,
                "Psst! Keep your voice down, deary—the Wardens in their grey cloaks have sharp ears! " +
                "Not blessed with the Wizard's Sight, are we? No matter! For a modest sum of 5 gold pieces, " +
                "I can sell you a [Silver Thimble of the Veil] spun by the Malks to prick the curtain between worlds and step straight into the Nevernever!");
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            string cmd = text.Trim().ToLowerInvariant();
            if (cmd is "silver thimble of the veil" or "thimble" or "talisman" or "buy")
            {
                DbItemTemplate template = EdinburghAndNeverneverWorld.EnsureSilverThimbleTemplate();
                if (template == null)
                    return true;

                if (player.GetCurrentMoney() < ThimblePriceCopper)
                {
                    SayTo(player, "Five gold pieces, deary! Not a copper less—do you know what Unseelie silver costs these days?");
                    return true;
                }

                DbInventoryItem item = GameInventoryItem.Create(template);
                if (!player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, item))
                {
                    SayTo(player, "Clear a space in your pack first, deary!");
                    return true;
                }

                player.RemoveMoney(ThimblePriceCopper);
                SayTo(player,
                    "There you go! Slip it onto your finger and /use it whenever you wish to tear open a Way into Faerie. " +
                    "Mind the cobblestone road once you cross over—step into the deep brambles without cold iron at your hip and the Sidhe will have your bones!");
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Allied Treaty Scholar / Emissary NPC in Tier 1 Edinburgh and Senior Council NPC in Tier 2.
    /// </summary>
    public class EdinburghCouncilNPC : GameNPC
    {
        public string LoreGreeting { get; set; } = "Welcome to the Sanctuary of Edinburgh.";

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            eQuestIndicator indicator = base.GetQuestIndicator(player);
            if (indicator != eQuestIndicator.None)
                return indicator;

            return DOL.GS.Quests.CouncilWardenEpicQuestRuntime.GetTrainerQuestIndicator(player, this);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 5000);
            var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
            if (access.IsKillOnSight)
            {
                Yell(EdinburghAdmissionsPolicy.FifthLawCondemnationText);
                EdinburghAndNeverneverWorld.AlertNearbyWardens(player);
                return true;
            }

            SayTo(player, LoreGreeting);
            if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(this))
            {
                DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerInteraction(player, this);
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(this) &&
                DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerWhisper(player, this, text))
            {
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Universal Sanctuary Trainer for Edinburgh Tier 1, supporting any magic class across Albion, Midgard,
    /// and Hibernia as well as the Council Warden class across all three realms.
    /// </summary>
    public class EdinburghMagicClassTrainer : GameTrainer
    {
        public eCharacterClass PrimaryTrainedClass { get; set; } = eCharacterClass.Unknown;
        public HashSet<eCharacterClass> AdditionalTrainedClasses { get; } = new();
        public string LoreGreeting { get; set; } = "Welcome to the Sanctuary of Edinburgh.";

        public override eCharacterClass TrainedClass => PrimaryTrainedClass;

        public override bool CanTrain(GamePlayer player)
        {
            if (player == null)
                return false;

            eCharacterClass playerClass = (eCharacterClass)player.CharacterClass.ID;
            return playerClass == PrimaryTrainedClass || AdditionalTrainedClasses.Contains(playerClass);
        }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            eQuestIndicator indicator = base.GetQuestIndicator(player);
            if (indicator != eQuestIndicator.None)
                return indicator;

            return DOL.GS.Quests.CouncilWardenEpicQuestRuntime.GetTrainerQuestIndicator(player, this);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 10000);
            var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
            if (access.IsKillOnSight)
            {
                Yell(EdinburghAdmissionsPolicy.FifthLawCondemnationText);
                EdinburghAndNeverneverWorld.AlertNearbyWardens(player);
                return true;
            }

            if (!string.IsNullOrEmpty(LoreGreeting))
                SayTo(player, LoreGreeting);

            if (CanTrain(player))
            {
                OfferTraining(player);
                if (player.Level == 5 && !player.IsLevelRespecUsed)
                    OfferRespecialize(player);

                if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(this))
                    DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerInteraction(player, this);
            }
            else if (CanPromotePlayer(player))
            {
                SayTo(player, $"If you have reached the fifth rank of your calling and wish to walk this path, whisper [join] to be inducted into the {ScriptMgr.FindCharacterClass((int)PrimaryTrainedClass)?.Name ?? PrimaryTrainedClass.ToString()} order.");
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.IsCouncilWardenQuestGiver(this) &&
                DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerWhisper(player, this, text))
            {
                return true;
            }

            if (CanPromotePlayer(player) && text.Trim().Equals("join", StringComparison.OrdinalIgnoreCase))
            {
                PromotePlayer(player, (int)PrimaryTrainedClass, $"Welcome to our order, {player.Name}. Walk in wisdom under the peace of Edinburgh.", null);
                if (DOL.GS.Spells.CouncilWardenMechanics.IsCouncilWarden(player))
                    DOL.GS.Quests.CouncilWardenEpicArmor.GivePiece(player, DOL.GS.Quests.CouncilWardenEpicArmor.InitiateGreyCloak, this);
                player.Out.SendTrainerWindow();
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Bootstraps Edinburgh (Regions 65 & 192), The Nevernever (Regions 197, 196, 195),
    /// the Surface Entrance at Hadrian's Wall, the Silver Thimble of the Veil,
    /// True Cold Iron weapons, and level 5 Open Way spell grants.
    /// </summary>
    public static class EdinburghAndNeverneverWorld
    {
        public const string SilverThimbleTemplateId = "silver_thimble_of_the_veil";
        public const string LegionsSeveringEdgeTemplateId = "legions_severing_edge";

        private static readonly List<GameNPC> SpawnedNpcs = new();
        private static readonly List<GameStaticItem> SpawnedStaticItems = new();
        private static readonly object Sync = new();

        public static DbItemTemplate BuildSilverThimbleTemplate(DbItemTemplate existing = null)
        {
            DbItemTemplate t = existing ?? new DbItemTemplate();
            t.Id_nb = SilverThimbleTemplateId;
            t.Name = "Silver Thimble of the Veil";
            t.Level = 5;
            t.Quality = 100;
            t.Durability = 50000;
            t.MaxDurability = 50000;
            t.Condition = 50000;
            t.MaxCondition = 50000;
            t.Model = 499;
            t.Object_Type = (int)eObjectType.Magical;
            t.Item_Type = (int)eInventorySlot.FirstEmptyBackpack;
            t.Realm = 0;
            t.AllowedClasses = "";
            t.SpellID = OpenWayResonanceTable.OpenWaySpellId;
            t.Charges = 50;
            t.MaxCharges = 50;
            t.Price = 50000;
            t.IsPickable = true;
            t.IsDropable = true;
            t.IsTradable = true;
            t.CanDropAsLoot = false;
            t.PackageID = "EdinburghNevernever";
            t.Description = "A tarnished silver thimble etched with smuggled Way-finding sigils. Allows even non-casters to prick the Veil and step into the Nevernever.";
            return t;
        }

        public static DbItemTemplate BuildLegionsSeveringEdgeTemplate(DbItemTemplate existing = null)
        {
            DbItemTemplate t = existing ?? new DbItemTemplate();
            t.Id_nb = LegionsSeveringEdgeTemplateId;
            t.Name = "Legion's Severing Edge";
            t.Level = 51;
            t.Quality = 100;
            t.Bonus = 35;
            t.Durability = 50000;
            t.MaxDurability = 50000;
            t.Condition = 50000;
            t.MaxCondition = 50000;
            t.Model = 3;
            t.Object_Type = (int)eObjectType.GenericWeapon;
            t.Item_Type = (int)eInventorySlot.RightHandWeapon;
            t.Type_Damage = (int)eDamageType.Slash;
            t.DPS_AF = 165;
            t.SPD_ABS = 36;
            t.Hand = 0;
            t.Realm = 0;
            t.AllowedClasses = "60;61;58";
            t.Bonus1 = 18;
            t.Bonus1Type = (int)eProperty.Strength;
            t.Bonus2 = 18;
            t.Bonus2Type = (int)eProperty.Intelligence;
            t.Bonus3 = 15;
            t.Bonus3Type = (int)eProperty.Dexterity;
            t.Bonus4 = 4;
            t.Bonus4Type = (int)eProperty.AllMeleeWeaponSkills;
            t.Bonus5 = 4;
            t.Bonus5Type = (int)eProperty.Skill_Parry;
            t.Price = 200;
            t.IsPickable = true;
            t.IsDropable = true;
            t.IsTradable = false;
            t.CanDropAsLoot = false;
            t.PackageID = "ColdIron";
            t.Description = "Forged from unrefined meteoritic Cold Iron. Deals +50% bonus damage to Fae entities, shatters their magical wards, and completely halts Fae regeneration.";
            return t;
        }

        public static DbItemTemplate EnsureSilverThimbleTemplate()
        {
            if (GameServer.Database == null)
                return BuildSilverThimbleTemplate();

            DbItemTemplate existing = GameServer.Database.FindObjectByKey<DbItemTemplate>(SilverThimbleTemplateId);
            if (existing == null)
            {
                existing = BuildSilverThimbleTemplate();
                GameServer.Database.AddObject(existing);
            }
            else
            {
                BuildSilverThimbleTemplate(existing);
                GameServer.Database.SaveObject(existing);
            }
            return existing;
        }

        public static DbItemTemplate EnsureColdIronWeaponTemplate()
        {
            if (GameServer.Database == null)
                return BuildLegionsSeveringEdgeTemplate();

            DbItemTemplate existing = GameServer.Database.FindObjectByKey<DbItemTemplate>(LegionsSeveringEdgeTemplateId);
            if (existing == null)
            {
                existing = BuildLegionsSeveringEdgeTemplate();
                GameServer.Database.AddObject(existing);
            }
            else
            {
                BuildLegionsSeveringEdgeTemplate(existing);
                GameServer.Database.SaveObject(existing);
            }
            return existing;
        }

        public const string FaeDustItemId = "fae_dust_essence";
        public const string FaeBrambleThornItemId = "fae_bramble_thorn";
        public const string FrosthoundRimeFangItemId = "frosthound_rime_fang";
        public const string MalkShadowClawItemId = "malk_shadow_claw";
        public const string RawheadBloodyBoneItemId = "rawhead_bloody_bone";
        public const string RawheadBogEyeItemId = "rawhead_bog_eye";
        public const string TarnishedSilverShardItemId = "tarnished_silver_waystone_shard";

        public static DbItemTemplate BuildFaeLootItemTemplate(string id, string name, byte level, int price, string description, bool isMagical = false)
        {
            return new DbItemTemplate
            {
                Id_nb = id,
                Name = name,
                Level = level,
                Quality = 100,
                Durability = 50000,
                MaxDurability = 50000,
                Condition = 50000,
                MaxCondition = 50000,
                Model = 499,
                Object_Type = (int)(isMagical ? eObjectType.Magical : eObjectType.GenericItem),
                Item_Type = (int)eInventorySlot.FirstEmptyBackpack,
                Realm = 0,
                Price = price,
                IsPickable = true,
                IsDropable = true,
                IsTradable = true,
                CanDropAsLoot = true,
                PackageID = "EdinburghNevernever",
                Description = description
            };
        }

        public static void EnsureFaeLootTemplates()
        {
            if (GameServer.Database == null)
                return;

            var items = new (string id, string name, byte level, int price, string desc, bool magical)[]
            {
                (FaeDustItemId, "Luminescent Fae Dust", 35, 200, "A pouch of shimmering, phosphorescent dust harvested from Nevernever wisps and sprites. Highly prized in alchemy and enchanting.", true),
                (FaeBrambleThornItemId, "Barbed Bramble Thorn", 25, 150, "A razor-sharp thorn harvested from the aggressive briars and boggarts of the Nevernever thickets.", false),
                (FrosthoundRimeFangItemId, "Rime-Covered Fang", 42, 250, "A freezing, crystalline tooth harvested from an Unseelie frosthound. Remains cold to the touch even beside a blazing hearth.", false),
                (MalkShadowClawItemId, "Shadow Malk Claw", 40, 300, "A curved obsidian-like claw from a predatory Nevernever Malk. Possesses an unnerving ability to pierce magical protections.", false),
                (RawheadBloodyBoneItemId, "Flayed Bone of Rawhead", 55, 50000, "An ancient, blood-stained bone harvested from Rawhead & Bloodybones. Radiates primal dread and ancient bog sorcery.", true),
                (RawheadBogEyeItemId, "Fetid Bog Eye of Rawhead", 55, 20000, "A milky, dead eye plucked from the depths of the stygian mire. A gruesome trophy of victory over the ancient nursery nightmare.", false),
                (TarnishedSilverShardItemId, "Waystone Resonance Shard", 50, 10000, "A fragment of silver-infused menhir stone that resonates with the resonance frequency of the Ways.", true),
            };

            foreach (var (id, name, level, price, desc, magical) in items)
            {
                DbItemTemplate existing = GameServer.Database.FindObjectByKey<DbItemTemplate>(id);
                if (existing == null)
                {
                    existing = BuildFaeLootItemTemplate(id, name, level, price, desc, magical);
                    GameServer.Database.AddObject(existing);
                }
            }
        }

        public static void AlertNearbyWardens(GamePlayer violator)
        {
            if (violator == null)
                return;

            foreach (GameNPC npc in violator.GetNPCsInRadius(1200))
            {
                if (npc is WardenSentryNPC sentry && sentry.IsAlive)
                {
                    if (sentry.Brain is StandardMobBrain brain)
                        brain.AddToAggroList(violator, 1000);
                    sentry.StartAttack(violator);
                }
            }
        }

        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            EnsureSilverThimbleTemplate();
            EnsureColdIronWeaponTemplate();
            EnsureFaeLootTemplates();
            EnsureWorldPopulated();
            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, OnPlayerEntered);
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GamePlayerEvent.GameEntered, OnPlayerEntered);
            lock (Sync)
            {
                foreach (GameNPC npc in SpawnedNpcs)
                {
                    if (npc?.ObjectState == GameObject.eObjectState.Active)
                        npc.Delete();
                }
                SpawnedNpcs.Clear();

                foreach (GameStaticItem item in SpawnedStaticItems)
                {
                    if (item?.ObjectState == GameObject.eObjectState.Active)
                        item.Delete();
                }
                SpawnedStaticItems.Clear();
            }
        }

        private static void OnPlayerEntered(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer player)
                return;

            var access = EdinburghAdmissionsPolicy.EvaluatePlayer(player);
            if (access.IsKillOnSight && player.CurrentRegionID is 65 or 192)
            {
                player.Out.SendMessage(EdinburghAdmissionsPolicy.FifthLawCondemnationText, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                AlertNearbyWardens(player);
            }
        }

        public static void EnsureWorldPopulated()
        {
            lock (Sync)
            {
                if (SpawnedNpcs.Count > 0)
                    return;

                // 1. Hadrian's Wall Surface Entrance (Region 1, inside the Neutral Hillfort Ruins)
                SpawnPortal("Outer Threshold of Edinburgh", "The White Council", 1, 635904, 336640, 4256, 2048,
                    destRegion: 65, destX: 34707, destY: 47400, destZ: 16145, destHeading: 2048);
                SpawnWardenSentry("Warden Alistair MacKinnon", "White Council Sentry", 1, 635950, 336680, 4256, 2048);

                // 2. Edinburgh Tier 1: The Catacombs & Diplomatic Cloister (Region 65)
                SpawnPortal("Hadrian's Wall Surface Exit", "Outer Ward", 65, 34707, 47500, 16145, 0,
                    destRegion: 1, destX: 635904, destY: 336640, destZ: 4260, destHeading: 180);
                SpawnPortal("The Inner Threshold of the Seven Laws", "To Grand Conclave Hall", 65, 39011, 44300, 16400, 1024,
                    destRegion: 192, destX: 35089, destY: 32498, destZ: 16166, destHeading: 2048, requiresTier2: true);
                SpawnPortal("The Ways Ingress", "To The Nevernever", 65, 37210, 29100, 16400, 2048,
                    destRegion: 197, destX: 32000, destY: 32000, destZ: 16000, destHeading: 2048);

                // Tier 1 Warden Sentries
                SpawnWardenSentry("Senior Warden Murdoch", "Keeper of the Inner Threshold", 65, 38950, 44220, 16400, 1024);
                SpawnWardenSentry("Warden Sentry Isla", "White Council Enforcer", 65, 34760, 47350, 16145, 2048);
                SpawnWardenSentry("Warden Sentry Callum", "Ways Watch", 65, 37150, 29220, 16400, 0);

                // Tier 1 Central Sanctuary Rotunda: Healer, Vault Keeper, Blacksmith, Enchanter & Recharger
                SpawnServiceNpc<GameHealer>("Sister Brigid of Iona", "Sanctuary Healer", 65, 32820, 35680, 16481, 2048, model: 46);
                SpawnServiceNpc<GameVaultKeeper>("Archivist Eamonn Vance", "Council Vault Keeper", 65, 33080, 35680, 16481, 2048, model: 41);
                SpawnServiceNpc<Blacksmith>("Master Fergus Ironhand", "Sanctuary Blacksmith", 65, 32820, 35920, 16481, 0, model: 185);
                SpawnServiceNpc<Enchanter>("Artificer Lyra Vance", "Council Enchanter", 65, 33080, 35920, 16481, 0, model: 330);
                SpawnServiceNpc<Recharger>("Recharger Katherine", "Sanctuary Recharger", 65, 32950, 35800, 16481, 0, model: 330);

                // Tier 1 Warden Barracks & Council Warden Cross-Realm Trainer
                SpawnCouncilNpc("Quartermaster Gregor MacLeod", "Warden Armory & Barracks", 65, 32480, 31950, 16465, 2048, model: 40,
                    greeting: "Welcome to the Warden Barracks! Every grey cloak and silver blade in the three realms is forged and warded right here.");
                SpawnMagicTrainer("Drillmaster Callan Boyd", "Council Warden Trainer", eCharacterClass.MaulerAlb, 65, 32610, 32050, 16465, 2048, model: 35,
                    greeting: "Steel and sigil, Warden! Whether you hail from Albion, Midgard, or Hibernia, we hone Evocation, Abjuration, and blade-work together in these barracks.",
                    eCharacterClass.MaulerMid, eCharacterClass.Vampiir);

                // Tier 1 Artisans' Quarter & Crafting Hall (East-Central Chambers)
                // Smithy & Forge Chamber
                SpawnCraftingStation("Forge", 478, 65, 34525, 40489, 15935, 0);
                SpawnServiceNpc<ArmorCraftingMaster>("Master Armorer Duncan", "Armorcrafting Master", 65, 34430, 40420, 15935, 2048, model: 40);
                SpawnServiceNpc<WeaponCraftingMaster>("Master Bladesmith Gavran", "Weaponcrafting Master", 65, 34620, 40420, 15935, 2048, model: 185);
                SpawnMerchant("Factor Baird", "Metal, Wood & Leather Merchant", "AlbMetalWoodLeather", 65, 34430, 40560, 15935, 0, model: 39);
                SpawnMerchant("Chandler Morag", "Crafting Tools & Cloth Merchant", "AlbCraftComponents", 65, 34620, 40560, 15935, 0, model: 44);

                // Alchemy & Spellcrafting Chamber
                SpawnCraftingStation("Alchemy Table", 820, 65, 34785, 42099, 15931, 0);
                SpawnServiceNpc<SpellCraftingMaster>("Magister Aurelia", "Spellcrafting Master", 65, 34690, 42020, 15931, 2048, model: 55);
                SpawnServiceNpc<AlchemistsMaster>("Apothecary Severin", "Alchemy Master", 65, 34880, 42020, 15931, 2048, model: 718);
                SpawnMerchant("Herbalist Rowan", "Alchemy & Poison Supplies", "966eda39-c819-41d3-84e0-cdf9783c1999", 65, 34690, 42180, 15931, 0, model: 342);
                SpawnMerchant("Lapidary Caelia", "Spellcrafting Gems & Powders", "4ff50218-48b4-4ec3-bab4-35959351a59c", 65, 34880, 42180, 15931, 0, model: 334);
                SpawnMerchant("Dyer Maeve", "Sanctuary Dye Merchant", "ClothDye1", 65, 34785, 42260, 15931, 0, model: 48);

                // Tailoring & Fletching Workshop
                SpawnCraftingStation("Lathe", 481, 65, 33074, 40111, 15935, 0);
                SpawnServiceNpc<FletchingMaster>("Master Bowyer Finn", "Fletching Master", 65, 32980, 40030, 15935, 2048, model: 330);
                SpawnServiceNpc<TailoringMaster>("Master Weaver Catriona", "Tailoring Master", 65, 33170, 40030, 15935, 2048, model: 46);
                SpawnMerchant("Mercer Elowen", "Tailoring Threads & Squares", "AlbTailorComponents2", 65, 32980, 40190, 15935, 0, model: 355);
                SpawnMerchant("Timberwright Torcall", "Masterwork Wood & Thread Merchant", "AlbExpensiveTrade", 65, 33170, 40190, 15935, 0, model: 192);
                SpawnMerchant("Fletcher Tamsin", "Fletching Feathers & Shafts", "AlbFeathers", 65, 33074, 40270, 15935, 0, model: 43);

                // Tier 1 Diplomatic Cloister Lore Scholars & Magic Class Trainers (16 Arcane, Primal & Song Classes)
                SpawnCouncilNpc("Chronicler Malakor", "Inconnu Crypt-Scholar", 65, 26151, 32693, 16761, 1024, model: 718,
                    greeting: "Since the Crown shuttered the Academy in Camelot after Arthur's fall, we Inconnu have preserved the displaced libraries within these volcanic vaults.");
                SpawnCouncilNpc("Master Veylan", "Displaced Academy Savant", 65, 26150, 34446, 16612, 1024, model: 35,
                    greeting: "In Camelot they fear what they cannot control. Beneath Edinburgh, the Laws of Merlin still hold the darkness at bay.");

                // Albion Magic Class Trainers (West Cloister North Wing)
                SpawnMagicTrainer("Magister Aldous Vance", "Wizard Trainer", eCharacterClass.Wizard, 65, 26020, 32580, 16761, 1024, model: 61,
                    greeting: "Fire, earth, and ice answer to disciplined geometry. Step forward if you walk the path of the Albion Wizard.");
                SpawnMagicTrainer("Preceptor Galen", "Theurgist Trainer", eCharacterClass.Theurgist, 65, 26280, 32580, 16761, 3072, model: 63,
                    greeting: "Air, ice, and earth take form at our command. Let us refine your elemental invocations, Theurgist.");
                SpawnMagicTrainer("Domina Lysandra", "Sorcerer Trainer", eCharacterClass.Sorcerer, 65, 26020, 32800, 16761, 1024, model: 55,
                    greeting: "To unravel a foe's will without breaking the Third Law requires razor-sharp focus. Come, Sorcerer, let us study.");
                SpawnMagicTrainer("Savant Malakor", "Cabalist Trainer", eCharacterClass.Cabalist, 65, 26280, 32800, 16761, 3072, model: 718,
                    greeting: "Simulacra of clay and amber obey the Laws where necromancy fails. I instruct Cabalists of the displaced Academy.");
                SpawnMagicTrainer("Troubadour Perrin", "Minstrel Trainer", eCharacterClass.Minstrel, 65, 26150, 34560, 16611, 0, model: 42,
                    greeting: "Every ancient ballad carries a thread of true magic. Let us tune your voice and blade, Minstrel.");

                // Midgard Magic Class Trainers & Treaty Scholars (Central-West Cloister Hall)
                SpawnCouncilNpc("Master Brokkr of Svartalfheim", "Dwarven Rune-Master & Treaty Scholar", 65, 28402, 33999, 15871, 1024, model: 185,
                    greeting: "Hail from Svartalfheim! Though we Dwarves are not mortal citizens of the White Council, our ancient treaties grant all Midgard runecasters and mystics sanctuary here in the Diplomatic Cloisters.");
                SpawnMagicTrainer("Runemaster Hrogar", "Runemaster Trainer", eCharacterClass.Runemaster, 65, 28280, 33900, 15871, 1024, model: 185,
                    greeting: "The runes carved beneath Din Eidyn resonate with the roots of Yggdrasil. Study well, Runemaster.");
                SpawnMagicTrainer("Gothi Torstein", "Healer Trainer", eCharacterClass.Healer, 65, 28520, 33900, 15871, 3072, model: 158,
                    greeting: "The runes of mending and the ancient sagas soothe mortal wounds alike. Hail, Healer.");
                SpawnMagicTrainer("Runekeeper Skuld", "Shaman Trainer", eCharacterClass.Shaman, 65, 28520, 34100, 15871, 3072, model: 169,
                    greeting: "Deep in the stone, the fungal spores and earth-runes whisper power to those who listen, Shaman.");

                // Hibernia Magic Class Trainers & Treaty Scholars (West Cloister South Wing)
                SpawnCouncilNpc("Emissary Finvarra of the Veil", "Lurikeen Illusionist & Sidhe Liaison", 65, 27908, 38051, 16761, 2048, model: 355,
                    greeting: "Welcome to the Treaty Cloisters! When the border mists grew dangerous, we Lurikeen and Elven scholars established this embassy with the White Council.");
                SpawnMagicTrainer("Ollamh Aelindor", "Eldritch Trainer", eCharacterClass.Eldritch, 65, 26020, 37950, 16611, 1024, model: 330,
                    greeting: "Light, sun, and the void between stars—we Elves have studied them since before Rome was founded. Welcome, Eldritch.");
                SpawnMagicTrainer("Illusionist Finvarra", "Enchanter Trainer", eCharacterClass.Enchanter, 65, 26280, 37950, 16611, 3072, model: 355,
                    greeting: "Glamour and arcane light weave together like threads on a loom. Step closer, Enchanter.");
                SpawnMagicTrainer("Mindweaver Maeve", "Mentalist Trainer", eCharacterClass.Mentalist, 65, 26020, 38150, 16611, 1024, model: 334,
                    greeting: "Walk the boundary of thought with care, Mentalist, so that healing light never strays into forbidden domination.");
                SpawnMagicTrainer("Grove-Caller Cian", "Animist Trainer", eCharacterClass.Animist, 65, 26280, 38150, 16611, 3072, model: 382,
                    greeting: "Even in volcanic rock, the forest spirits answer our call. Welcome, Animist.");
                SpawnMagicTrainer("Scythe-Weir Lorcan", "Valewalker Trainer", eCharacterClass.Valewalker, 65, 27800, 37980, 16761, 1024, model: 382,
                    greeting: "We who walk the vale with scythe and bark-ward understand the Wardens better than most. Hail, Valewalker.");
                SpawnMagicTrainer("Archdruid Cathbad", "Druid Trainer", eCharacterClass.Druid, 65, 26020, 40690, 16761, 1024, model: 302,
                    greeting: "By oak, ash, and thorn, the ancient harmonies endure beneath Din Eidyn. Peace upon you, Druid.");
                SpawnMagicTrainer("Grove-Warden Niamh", "Warden Trainer", eCharacterClass.Warden, 65, 26280, 40690, 16761, 3072, model: 307,
                    greeting: "The White Council calls its sword-mages Wardens, yet we Wardens of Hibernia's sacred groves kept that vigil first! Let us train.");
                SpawnMagicTrainer("Fili Amergin", "Bard Trainer", eCharacterClass.Bard, 65, 26151, 40890, 16761, 0, model: 303,
                    greeting: "Music bridges the three realms and soothes even the oldest feuds. Welcome, Bard.");

                // Allied Treaty Scholars (Ambient Lorekeepers in Diplomatic Cloisters)
                SpawnCouncilNpc("Dwarven Runecarver Torvald", "Allied Treaty Scholar - Midgard", 65, 28350, 34200, 15871, 1024, model: 190,
                    greeting: "Hmph. The mortal wizards call this sanctuary, but I call it good stone and quiet study. The runes carved into these vaults date back to the Second Age.");
                SpawnCouncilNpc("Lurikeen Scribe Fionn", "Allied Treaty Scholar - Hibernia", 65, 27850, 38150, 16761, 2048, model: 340,
                    greeting: "Fascinating library, isn't it? The White Council may be stiff with their 'Laws of Merlin', but their records on planar breaches and leys are second to none!");
                SpawnCouncilNpc("Inconnu Crypt-Scholar Vaelen", "Allied Treaty Scholar - Albion", 65, 26151, 33000, 16761, 1024, model: 46,
                    greeting: "The catacombs beneath Edinburgh echo with ancient resonance. We of the subterranean realms find the Council's treaties quite... agreeable.");
                SpawnCouncilNpc("Elven Illusionist Lyndis", "Allied Treaty Scholar - Hibernia", 65, 26350, 38250, 16611, 2048, model: 335,
                    greeting: "Greetings, wayfarer. The Veil between this mountain and the Nevernever is thin as spider-silk. Mind your thoughts while walking near the Ways.");

                // 3. Edinburgh Tier 2: The Grand Conclave Hall (Region 192 - 6th-Century Senior Council & Warden HQ)
                SpawnPortal("Threshold to the Catacombs", "To Diplomatic Cloister", 192, 35089, 32498, 16166, 0,
                    destRegion: 65, destX: 39011, destY: 44100, destZ: 16400, destHeading: 2048);

                // The 7 Members of the 6th-Century Senior Council:
                // 1. Greco-Roman (The Merlin)
                SpawnCouncilNpc("Aurelius Theophanes", "The Merlin of the White Council", 192, 32145, 32469, 15653, 3072, model: 51,
                    greeting: "Order, discipline, and the Seven Laws forged in the ashes of antiquity are all that stand between mortal kind and ruin. Do not give the Senior Council reason to doubt your resolve.");
                // 2. Roman-Italian (The Blackstaff)
                SpawnCouncilNpc("Cassian Valerius", "The Blackstaff", 192, 32220, 32360, 15653, 3072, model: 63,
                    greeting: "A law is only as strong as the will to enforce it—and the wisdom to know when the rot must be cut out at the root. Keep your blade sharp on the frontiers; something foul is stirring.");
                // 3. Celt
                SpawnCouncilNpc("Branwen ferch Llyr", "Senior Council", 192, 32070, 32560, 15653, 3072, model: 45,
                    greeting: "The mist-wrapped hills of Prydain remember oaths older than stone. Listen to the land before you strike, young wizard, and the Ways will not lead you astray.");
                // 4. Norse
                SpawnCouncilNpc("Halvdan Ulfsson", "Senior Council", 192, 32250, 32570, 15653, 3072, model: 137,
                    greeting: "In the northern fjords we learn early that winter shows no mercy to the unprepared. Temper your spirit like hearth-steel, or the dark beyond the gates will shatter you.");
                // 5. Arab (The Gatekeeper)
                SpawnCouncilNpc("Tariq ibn Ziyad al-Hakim", "The Gatekeeper", 192, 32330, 32470, 15653, 3072, model: 72,
                    greeting: "From the star-charts of the desert to the Furthest Reaches, I watch the Outer Gates and read the ripples in the Nevernever. Tread with vigilance when you open the Ways.");
                // 6. Hindi
                SpawnCouncilNpc("Acharya Devadatta", "Senior Council", 192, 32080, 32350, 15653, 3072, model: 42,
                    greeting: "As the sages of Takshashila teach, true mastery is the stillness at the center of the storm. Discipline your mind, for our wards are only as unbroken as the thoughts that sustain them.");
                // 7. Chinese
                SpawnCouncilNpc("Master Shen Xuanzong", "Senior Council", 192, 32160, 32640, 15653, 3072, model: 61,
                    greeting: "Across the Middle Kingdom and the western seas alike, harmony depends upon balance. Watch the unseen currents between the three realms—every shadow casts a sign before it strikes.");

                // Warden Headquarters in Tier 2
                SpawnCouncilNpc("Lord-Commander Caelan Vance", "Commander of the Wardens", 192, 32913, 32213, 15653, 1024, model: 55,
                    greeting: "Report, Warden. Our grey cloaks are stretched thin across Albion, Midgard, and Hibernia, and whispers of a hidden cabal breaking the Laws grow louder by the day.");
                SpawnWardenSentry("High Warden Torin Lachlan", "Second-in-Command of the Wardens", 192, 32980, 32120, 15653, 1024);
                SpawnWardenSentry("Senior Warden Rhona", "Warden of the White Council", 192, 35140, 32650, 16170, 0);

                // Conclave Honor Guards in Region 192
                SpawnWardenHonorGuard("Conclave Warden Guard", "Warden Honor Guard", 192, 34950, 32700, 16170, 1024);
                SpawnWardenHonorGuard("Conclave Warden Guard", "Warden Honor Guard", 192, 35230, 32700, 16170, 3072);
                SpawnWardenHonorGuard("Conclave Warden Guard", "Warden Honor Guard", 192, 35000, 32380, 16175, 2048);
                SpawnWardenHonorGuard("Conclave Warden Guard", "Warden Honor Guard", 192, 35180, 32380, 16175, 2048);

                // 4. The Nevernever (Region 197 Central Crossing, Region 196 Summer Court, Region 195 Winter Court)
                // Edinburgh Gateway Menhir (Requires Open Way spell or Silver Thimble)
                SpawnPortal("Edinburgh Gateway Menhir", "The White Council Sanctuary", 197, 32000, 32000, 16000, 0,
                    destRegion: 65, destX: 37210, destY: 29150, destZ: 16400, destHeading: 0,
                    isNeverneverHub: true, requiresOpenWay: true);

                // Dedicated Safe Realm Return Waystones in Region 197 Hub:
                // Albion -> Cotswold Village
                SpawnPortal("Albion Waystone", "To Cotswold Village", 197, 32080, 31920, 16000, 180,
                    destRegion: 1, destX: 564976, destY: 509200, destZ: 2694, destHeading: 2048);

                // Midgard -> Vale of Mularn
                SpawnPortal("Midgard Waystone", "To Vale of Mularn", 197, 31920, 31920, 16000, 180,
                    destRegion: 100, destX: 808830, destY: 725039, destZ: 4815, destHeading: 2048);

                // Hibernia -> Mag Mell Village
                SpawnPortal("Hibernia Waystone", "To Mag Mell Village", 197, 31920, 32080, 16000, 0,
                    destRegion: 200, destX: 348198, destY: 492819, destZ: 5290, destHeading: 2048);

                // Neutral Frontier -> Hadrian's Wall Hillfort
                SpawnPortal("Frontier Waystone", "To Hadrian's Wall Hillfort", 197, 32080, 32080, 16000, 0,
                    destRegion: 1, destX: 635904, destY: 336640, destZ: 4260, destHeading: 180);

                // Borderlands Waystones: Summer (196) and Winter (195)
                SpawnPortal("Summer Borderlands Waystone", "Seelie Crossing", 197, 42134, 34920, 16000, 1024,
                    destRegion: 196, destX: 25752, destY: 21072, destZ: 16000, destHeading: 2048, isNeverneverHub: true);
                SpawnPortal("Winter Borderlands Waystone", "Unseelie Crossing", 197, 26134, 26920, 16000, 3072,
                    destRegion: 195, destX: 32852, destY: 14647, destZ: 16000, destHeading: 2048, isNeverneverHub: true);

                // Return Waystones in Summer Court (196) & Winter Court (195)
                SpawnPortal("Borderlands Return Waystone", "To Central Nevernever", 196, 25752, 21072, 16000, 0,
                    destRegion: 197, destX: 42134, destY: 34920, destZ: 16000, destHeading: 1024);
                SpawnPortal("Borderlands Return Waystone", "To Central Nevernever", 195, 32852, 14647, 16000, 0,
                    destRegion: 197, destX: 26134, destY: 26920, destZ: 16000, destHeading: 3072);

                // Hub Warden Way-Watchers in Region 197
                SpawnWardenSentry("Warden Way-Watcher Kenneth", "Warden of the Ways", 197, 32040, 32040, 16000, 0);
                SpawnWardenSentry("Warden Way-Watcher Morven", "Warden of the Ways", 197, 31960, 32040, 16000, 0);
                SpawnWardenSentry("Warden Patrol Officer Alana", "Warden of the Ways", 197, 32000, 32150, 16000, 2048);

                // Guiding Way-Wisp Lanterns along the Warded Cobblestone Road
                SpawnWayWispLantern("Way-Wisp Lantern", 197, 32000, 32000, 16000);
                SpawnWayWispLantern("Way-Wisp Lantern", 197, 35000, 33000, 16000);
                SpawnWayWispLantern("Way-Wisp Lantern", 197, 38000, 34000, 16000);
                SpawnWayWispLantern("Way-Wisp Lantern", 197, 41500, 34700, 16000);
                SpawnWayWispLantern("Way-Wisp Lantern", 197, 29500, 29800, 16000);
                SpawnWayWispLantern("Way-Wisp Lantern", 197, 26500, 27200, 16000);

                // Crossroads Hedge-Witch Merchant (Silver Thimbles & Reagents)
                SpawnHedgeWitch("Griselda the Hedge-Wife", "Curiosities of the Ways", 197, 32120, 32120, 16000, 1024);

                // =========================================================================
                // NEVERNEVER FAE ECOLOGY (REGION 197 OFF-ROAD WILDERNESS)
                // =========================================================================
                // Zone 1: The Brambles & Briar Thickets (Levels 18–30, North/Northeast)
                SpawnFaePredator("Bramble Creeper", "Wyldfae Plant", 197, 34500, 39000, 16000, level: 20, model: 1480, NeverneverFaeArchetype.BriarCreeper);
                SpawnFaePredator("Bramble Creeper", "Wyldfae Plant", 197, 36200, 41500, 16000, level: 22, model: 1480, NeverneverFaeArchetype.BriarCreeper);
                SpawnFaePredator("Thorn Boggart", "Wyldfae Scavenger", 197, 35500, 38500, 16000, level: 24, model: 1481, NeverneverFaeArchetype.ThornBoggart);
                SpawnFaePredator("Thorn Boggart", "Wyldfae Scavenger", 197, 37500, 40000, 16000, level: 26, model: 1481, NeverneverFaeArchetype.ThornBoggart);
                SpawnFaePredator("Briar Sprite", "Seelie Outrider", 197, 39000, 39500, 16000, level: 25, model: 1482, NeverneverFaeArchetype.BriarSprite);
                SpawnFaePredator("Briar Sprite", "Seelie Outrider", 197, 41000, 41000, 16000, level: 28, model: 1482, NeverneverFaeArchetype.BriarSprite);
                SpawnFaePredator("Bramblewood Lurker", "Briar Beast", 197, 33500, 43000, 16000, level: 29, model: 1483, NeverneverFaeArchetype.BramblewoodLurker);
                SpawnFaePredator("Bramblewood Lurker", "Briar Beast", 197, 36000, 44000, 16000, level: 30, model: 1483, NeverneverFaeArchetype.BramblewoodLurker);

                // Zone 2: The Twilight Mist & Shadow Woods (Levels 33–46, South/Southeast)
                SpawnFaePredator("Redcap Cutthroat", "Unseelie Marauder", 197, 35500, 25500, 16000, level: 33, model: 1484, NeverneverFaeArchetype.RedcapCutthroat);
                SpawnFaePredator("Redcap Cutthroat", "Unseelie Marauder", 197, 37000, 24500, 16000, level: 35, model: 1484, NeverneverFaeArchetype.RedcapCutthroat);
                SpawnFaePredator("Redcap Marauder", "Unseelie Marauder", 197, 38500, 23500, 16000, level: 38, model: 1485, NeverneverFaeArchetype.RedcapMarauder);
                SpawnFaePredator("Redcap Marauder", "Unseelie Marauder", 197, 40000, 22500, 16000, level: 40, model: 1485, NeverneverFaeArchetype.RedcapMarauder);
                SpawnFaePredator("Shadow Malk", "Stalker of the Ways", 197, 39000, 26500, 16000, level: 39, model: 1486, NeverneverFaeArchetype.ShadowMalk);
                SpawnFaePredator("Shadow Malk", "Stalker of the Ways", 197, 41000, 25500, 16000, level: 42, model: 1486, NeverneverFaeArchetype.ShadowMalk);
                SpawnFaePredator("Twilight Fetch", "Deep Nevernever Horror", 197, 42500, 22000, 16000, level: 43, model: 1488, NeverneverFaeArchetype.TwilightFetch);
                SpawnFaePredator("Twilight Fetch", "Deep Nevernever Horror", 197, 40500, 20500, 16000, level: 46, model: 1488, NeverneverFaeArchetype.TwilightFetch);

                // Zone 3: The Dark Water Margins & Stygian Bogs (Levels 48–55, Deep Southwest)
                SpawnFaePredator("Will-o'-the-Wisp", "Corpse Candle", 197, 23000, 22000, 16000, level: 48, model: 1491, NeverneverFaeArchetype.WillOWisp);
                SpawnFaePredator("Will-o'-the-Wisp", "Corpse Candle", 197, 25000, 20000, 16000, level: 50, model: 1491, NeverneverFaeArchetype.WillOWisp);
                SpawnFaePredator("Bog Hag of the Sunder", "Wicked Coven", 197, 22500, 22500, 16000, level: 50, model: 1493, NeverneverFaeArchetype.BogHag);
                SpawnFaePredator("Bog Hag of the Sunder", "Wicked Coven", 197, 24000, 18500, 16000, level: 52, model: 1493, NeverneverFaeArchetype.BogHag);
                SpawnBossFaePredator("Rawhead & Bloodybones", "Ancient Bogeyman of the Deep", 197, 21500, 20500, 15900);

                // Zone 4: Outer Winter Borderlands (Approaching Region 195, Levels 43–52, West)
                SpawnFaePredator("Glacial Frosthound", "Unseelie Pack", 197, 24500, 28000, 16000, level: 43, model: 1495, NeverneverFaeArchetype.GlacialFrosthound);
                SpawnFaePredator("Glacial Frosthound", "Unseelie Pack", 197, 23000, 26000, 16000, level: 45, model: 1495, NeverneverFaeArchetype.GlacialFrosthound);
                SpawnFaePredator("Unseelie Border Sentinel", "The Winter Court", 197, 25000, 26000, 16000, level: 47, model: 1497, NeverneverFaeArchetype.UnseelieSentinel);
                SpawnFaePredator("Unseelie Border Sentinel", "The Winter Court", 197, 24000, 25000, 16000, level: 49, model: 1497, NeverneverFaeArchetype.UnseelieSentinel);
                SpawnFaePredator("Rime-Crusted Fetch", "Winter Shadow", 197, 22000, 27000, 16000, level: 50, model: 1489, NeverneverFaeArchetype.RimeCrustedFetch);
                SpawnFaePredator("Froststalker Wendigo", "Apex Winter Beast", 197, 20500, 26000, 16000, level: 52, model: 1496, NeverneverFaeArchetype.FroststalkerWendigo);

                // Zone 5: Outer Summer Borderlands (Approaching Region 196, Levels 43–52, East)
                SpawnFaePredator("Seelie Border Scout", "The Summer Court", 197, 43500, 36000, 16000, level: 43, model: 1499, NeverneverFaeArchetype.SeelieScout);
                SpawnFaePredator("Seelie Border Scout", "The Summer Court", 197, 44500, 34000, 16000, level: 45, model: 1499, NeverneverFaeArchetype.SeelieScout);
                SpawnFaePredator("Briar Thornsteed", "Seelie Steed", 197, 45000, 36500, 16000, level: 47, model: 1501, NeverneverFaeArchetype.BriarThornsteed);
                SpawnFaePredator("Briar Thornsteed", "Seelie Steed", 197, 46000, 33500, 16000, level: 49, model: 1501, NeverneverFaeArchetype.BriarThornsteed);
                SpawnFaePredator("Golden Willow Wisp", "Summer Court Wisp", 197, 44000, 37000, 16000, level: 49, model: 1492, NeverneverFaeArchetype.GoldenWillowWisp);
                SpawnFaePredator("Elder Gruff of the Wildwood", "Summer Champion", 197, 46500, 37000, 16000, level: 52, model: 1502, NeverneverFaeArchetype.ElderGruff);

                // =========================================================================
                // SUMMER COURT (REGION 196) & WINTER COURT (REGION 195) OUTPOSTS
                // =========================================================================
                SpawnFaePredator("Seelie Sidhe Sentinel", "Summer Court Guard", 196, 25752, 21200, 16000, level: 45, model: 1499, NeverneverFaeArchetype.SeelieScout);
                SpawnFaePredator("Elder Gruff of the Oak", "Summer Court Champion", 196, 25850, 21100, 16000, level: 50, model: 1502, NeverneverFaeArchetype.ElderGruff);
                SpawnFaePredator("Lord Taliesin of the Golden Bough", "Summer Court High Sidhe", 196, 25752, 21400, 16000, level: 55, model: 1500, NeverneverFaeArchetype.SeelieScout);
                SpawnFaePredator("Briar Sprite Outrider", "Summer Court Sprite", 196, 25650, 21300, 16000, level: 46, model: 1482, NeverneverFaeArchetype.BriarSprite);

                SpawnFaePredator("Unseelie Sidhe Arbiter", "Winter Court Guard", 195, 32852, 14750, 16000, level: 45, model: 1497, NeverneverFaeArchetype.UnseelieSentinel);
                SpawnFaePredator("Malk Stalker of Arctis Tor", "Winter Court Hunter", 195, 32950, 14650, 16000, level: 50, model: 1487, NeverneverFaeArchetype.ShadowMalk);
                SpawnFaePredator("Lord Slate of the Winter Mantle", "Winter Court High Sidhe", 195, 32852, 14950, 16000, level: 55, model: 1498, NeverneverFaeArchetype.UnseelieSentinel);
                SpawnFaePredator("Glacial Frosthound Hunter", "Winter Court Hound", 195, 32750, 14850, 16000, level: 46, model: 1495, NeverneverFaeArchetype.GlacialFrosthound);

                // 5. Black-Market Talisman Peddlers (Hedge-Witches in Albion, Midgard, and Hibernia)
                SpawnTalismanPeddler("Mother Malkin", "Curiosities of the Veil", 1, 559120, 511320, 2452, 1024, model: 46);
                SpawnTalismanPeddler("Old Grima", "Seithr & Trinkets", 100, 803350, 727020, 4682, 1024, model: 158);
                SpawnTalismanPeddler("Biddy O'Shaughnessy", "Hedge-Peddler", 200, 346240, 490920, 5200, 1024, model: 342);

                // 6. Normalize Capital Liaisons (no Warden trainers in capitals or starter towns)
                //    and ensure the secretive Campacorentin Forest outpost Council Warden Trainer is present.
                EnsureMortalWardenLiaisonsAndCampForestTrainer();
            }
        }

        private static void EnsureMortalWardenLiaisonsAndCampForestTrainer()
        {
            // Remove any legacy starter-town trainers (Cotswold, Mularn, Mag Mell)
            foreach (string legacyStarterName in new[] { "Warden Sigrun", "Warden Aisling" })
            {
                foreach (GameNPC npc in WorldMgr.GetNPCsByName(legacyStarterName, eRealm.Midgard)
                    .Concat(WorldMgr.GetNPCsByName(legacyStarterName, eRealm.Hibernia)))
                {
                    if (npc?.ObjectState == GameObject.eObjectState.Active)
                        npc.Delete();
                }
            }

            foreach (GameNPC vance in WorldMgr.GetNPCsByName("Warden Vance", eRealm.Albion))
            {
                // Remove legacy Cotswold placement (Y ~ 513580); keep Campacorentin Forest (Y ~ 587700)
                if (vance != null && vance.Y < 550000 && vance.ObjectState == GameObject.eObjectState.Active)
                    vance.Delete();
            }

            // Ensure Capital City NPCs are labeled as White Council Liaisons (not Trainers)
            foreach ((string name, eRealm realm) in new[]
            {
                ("Warden Alistair", eRealm.Albion),
                ("Warden Haldor", eRealm.Midgard),
                ("Warden Cormac", eRealm.Hibernia),
            })
            {
                foreach (GameNPC liaison in WorldMgr.GetNPCsByName(name, realm))
                {
                    if (liaison != null)
                        liaison.GuildName = "White Council Liaison";
                }
            }

            // Ensure Warden Vance (<Council Warden Trainer>) is present at the secretive magic outpost in Campacorentin Forest
            Region albRegion = WorldMgr.GetRegion(1);
            if (albRegion != null && !albRegion.IsDisabled)
            {
                bool hasCampForestTrainer = WorldMgr.GetNPCsByName("Warden Vance", eRealm.Albion)
                    .Any(n => n != null && n.ObjectState == GameObject.eObjectState.Active && n.Y >= 550000);

                if (!hasCampForestTrainer)
                {
                    var campTrainer = new DOL.GS.Trainer.AlbionMaulerTrainer
                    {
                        Name = "Warden Vance",
                        GuildName = "Council Warden Trainer",
                        Model = 33,
                        Size = 50,
                        Level = 50,
                        Realm = eRealm.Albion,
                        Flags = GameNPC.eFlags.PEACE,
                        CurrentRegionID = 1,
                        CurrentRegion = albRegion,
                        X = 497550,
                        Y = 587700,
                        Z = 2308,
                        Heading = 2048,
                        LoadedFromScript = true
                    };
                    campTrainer.AddToWorld();
                    SpawnedNpcs.Add(campTrainer);
                }
            }
        }

        private static void SpawnPortal(
            string name,
            string guild,
            ushort regionId,
            int x,
            int y,
            int z,
            ushort heading,
            ushort destRegion,
            int destX,
            int destY,
            int destZ,
            ushort destHeading,
            bool requiresTier2 = false,
            bool isNeverneverHub = false,
            bool requiresOpenWay = false)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            EdinburghWaystonePortalNPC portal = new()
            {
                Name = name,
                GuildName = guild,
                Model = 1503,
                Size = 60,
                Level = 50,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                DestinationRegion = destRegion,
                DestinationX = destX,
                DestinationY = destY,
                DestinationZ = destZ,
                DestinationHeading = destHeading,
                RequiresTier2ConclaveAccess = requiresTier2,
                IsNeverneverHubWaystone = isNeverneverHub,
                RequiresOpenWay = requiresOpenWay,
                LoadedFromScript = true
            };
            portal.AddToWorld();
            SpawnedNpcs.Add(portal);
        }

        private static void SpawnWardenSentry(string name, string guild, ushort regionId, int x, int y, int z, ushort heading)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            WardenSentryNPC sentry = new()
            {
                Name = name,
                GuildName = guild,
                Model = 35,
                Size = 52,
                Level = 65,
                Realm = eRealm.None,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                MaxSpeedBase = 220,
                RespawnInterval = 30000,
                LoadedFromScript = true
            };

            GameNpcInventoryTemplate inv = new();
            inv.AddNPCEquipment(eInventorySlot.Cloak, 443, 43); // Warden's Grey Cloak
            inv.AddNPCEquipment(eInventorySlot.TorsoArmor, 69, 43);
            inv.AddNPCEquipment(eInventorySlot.LegsArmor, 70, 43);
            inv.AddNPCEquipment(eInventorySlot.ArmsArmor, 71, 43);
            inv.AddNPCEquipment(eInventorySlot.HandsArmor, 72, 43);
            inv.AddNPCEquipment(eInventorySlot.FeetArmor, 73, 43);
            inv.AddNPCEquipment(eInventorySlot.RightHandWeapon, 3, 0); // Silver Broadsword
            sentry.Inventory = inv.CloseTemplate();
            sentry.InitializeActiveWeaponFromInventory();

            sentry.SetOwnBrain(new WardenSentryBrain());
            sentry.AddToWorld();
            SpawnedNpcs.Add(sentry);
        }

        private static void SpawnCouncilNpc(string name, string guild, ushort regionId, int x, int y, int z, ushort heading, ushort model, string greeting)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            EdinburghCouncilNPC npc = new()
            {
                Name = name,
                GuildName = guild,
                Model = model,
                Size = 50,
                Level = 60,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoreGreeting = greeting,
                LoadedFromScript = true
            };
            npc.AddToWorld();
            SpawnedNpcs.Add(npc);
        }

        private static void SpawnMagicTrainer(
            string name,
            string guild,
            eCharacterClass trainedClass,
            ushort regionId,
            int x,
            int y,
            int z,
            ushort heading,
            ushort model,
            string greeting,
            params eCharacterClass[] additionalClasses)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            EdinburghMagicClassTrainer trainer = new()
            {
                Name = name,
                GuildName = guild,
                PrimaryTrainedClass = trainedClass,
                LoreGreeting = greeting,
                Model = model,
                Size = 50,
                Level = 55,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoadedFromScript = true
            };
            if (additionalClasses != null)
            {
                foreach (eCharacterClass extra in additionalClasses)
                    trainer.AdditionalTrainedClasses.Add(extra);
            }
            trainer.AddToWorld();
            SpawnedNpcs.Add(trainer);
        }

        private static void SpawnServiceNpc<T>(
            string name,
            string guild,
            ushort regionId,
            int x,
            int y,
            int z,
            ushort heading,
            ushort model) where T : GameNPC, new()
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            T npc = new()
            {
                Name = name,
                GuildName = guild,
                Model = model,
                Size = 50,
                Level = 50,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoadedFromScript = true
            };
            npc.AddToWorld();
            SpawnedNpcs.Add(npc);
        }

        private static void SpawnMerchant(
            string name,
            string guild,
            string tradeListId,
            ushort regionId,
            int x,
            int y,
            int z,
            ushort heading,
            ushort model)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            GameMerchant merchant = new()
            {
                Name = name,
                GuildName = guild,
                Model = model,
                Size = 50,
                Level = 50,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                TradeItems = new MerchantTradeItems(tradeListId),
                LoadedFromScript = true
            };
            merchant.AddToWorld();
            SpawnedNpcs.Add(merchant);
        }

        private static void SpawnCraftingStation(
            string name,
            ushort model,
            ushort regionId,
            int x,
            int y,
            int z,
            ushort heading)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            GameStaticItem station = new()
            {
                Name = name,
                Model = model,
                CurrentRegionID = regionId,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoadedFromScript = true
            };
            station.AddToWorld();
            SpawnedStaticItems.Add(station);
        }

        private static void SpawnTalismanPeddler(string name, string guild, ushort regionId, int x, int y, int z, ushort heading, ushort model)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            BlackMarketTalismanPeddlerNPC npc = new()
            {
                Name = name,
                GuildName = guild,
                Model = model,
                Size = 48,
                Level = 35,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoadedFromScript = true
            };
            npc.AddToWorld();
            SpawnedNpcs.Add(npc);
        }

        private static void SpawnFaePredator(
            string name,
            string guild,
            ushort regionId,
            int x,
            int y,
            int z,
            byte level,
            ushort model,
            NeverneverFaeArchetype archetype = NeverneverFaeArchetype.BriarCreeper)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            NeverneverFaeNPC fae = new()
            {
                Name = name,
                GuildName = guild,
                Model = model,
                Size = (byte)(archetype == NeverneverFaeArchetype.WillOWisp || archetype == NeverneverFaeArchetype.GoldenWillowWisp ? 38 : 52),
                Level = level,
                Archetype = archetype,
                Realm = eRealm.None,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = 0,
                LoadedFromScript = true
            };
            fae.SetOwnBrain(new NeverneverFaeBrain { AggroLevel = 100, AggroRange = 550 });
            fae.AddToWorld();
            SpawnedNpcs.Add(fae);
        }

        private static void SpawnBossFaePredator(string name, string guild, ushort regionId, int x, int y, int z)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            NeverneverBossFaeNPC boss = new()
            {
                Name = name,
                GuildName = guild,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = 0,
                LoadedFromScript = true
            };
            boss.SetOwnBrain(new NeverneverFaeBrain { AggroLevel = 100, AggroRange = 650 });
            boss.AddToWorld();
            SpawnedNpcs.Add(boss);
        }

        private static void SpawnWayWispLantern(string name, ushort regionId, int x, int y, int z)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            WayWispLanternNPC wisp = new()
            {
                Name = name,
                GuildName = "Guiding Witchlight",
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = 0,
                LoadedFromScript = true
            };
            wisp.SetOwnBrain(new BlankBrain());
            wisp.AddToWorld();
            SpawnedNpcs.Add(wisp);
        }

        private static void SpawnHedgeWitch(string name, string guild, ushort regionId, int x, int y, int z, ushort heading)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            NeverneverHedgeWitchNPC witch = new()
            {
                Name = name,
                GuildName = guild,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoadedFromScript = true
            };
            witch.SetOwnBrain(new BlankBrain());
            witch.AddToWorld();
            SpawnedNpcs.Add(witch);
        }

        private static void SpawnWardenHonorGuard(string name, string guild, ushort regionId, int x, int y, int z, ushort heading, ushort model = 30)
        {
            Region region = WorldMgr.GetRegion(regionId);
            if (region == null || region.IsDisabled)
                return;

            WardenSentryNPC sentry = new()
            {
                Name = name,
                GuildName = guild,
                Model = model,
                Size = 54,
                Level = 65,
                Realm = eRealm.None,
                Flags = GameNPC.eFlags.PEACE,
                CurrentRegionID = regionId,
                CurrentRegion = region,
                X = x,
                Y = y,
                Z = z,
                Heading = heading,
                LoadedFromScript = true
            };

            GameNpcInventoryTemplate inv = new();
            inv.AddNPCEquipment(eInventorySlot.Cloak, 443, 43); // Warden's Grey Cloak
            inv.AddNPCEquipment(eInventorySlot.TorsoArmor, 69, 43);
            inv.AddNPCEquipment(eInventorySlot.LegsArmor, 70, 43);
            inv.AddNPCEquipment(eInventorySlot.ArmsArmor, 71, 43);
            inv.AddNPCEquipment(eInventorySlot.HandsArmor, 72, 43);
            inv.AddNPCEquipment(eInventorySlot.FeetArmor, 73, 43);
            inv.AddNPCEquipment(eInventorySlot.TwoHandWeapon, 25, 0); // Silver Claymore
            sentry.Inventory = inv.CloseTemplate();
            sentry.InitializeActiveWeaponFromInventory();

            sentry.SetOwnBrain(new WardenSentryBrain());
            sentry.AddToWorld();
            SpawnedNpcs.Add(sentry);
        }
    }
}
