using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Shared journal and progression behavior for the 9-chapter Council Warden
    /// epic mystery questline: "The Web of Shadows" (Levels 7, 13, 20, 25, 30, 35, 40, 45, 50).
    /// </summary>
    public abstract class CouncilWardenEpicQuest : BaseQuest
    {
        protected CouncilWardenEpicQuest() : base() { }
        protected CouncilWardenEpicQuest(GamePlayer player) : base(player) { }
        protected CouncilWardenEpicQuest(GamePlayer player, int step) : base(player, step) { }
        protected CouncilWardenEpicQuest(GamePlayer player, DbQuest quest) : base(player, quest) { }

        protected abstract Type Prerequisite { get; }
        protected abstract int RequiredLevel { get; }

        public CouncilWardenEpicChapterDefinition Definition =>
            CouncilWardenEpicQuestRuntime.GetDefinition(GetType());

        public override int Level => RequiredLevel;
        public override string Name => Definition.Title;

        public override bool CheckQuestQualification(GamePlayer player)
        {
            if (player == null)
                return false;

            if (!CouncilWardenMechanics.IsCouncilWarden(player))
                return false;

            if (player.IsDoingQuest(GetType()) != null)
                return true;

            if (player.Level < RequiredLevel)
                return false;

            return Prerequisite == null || player.HasFinishedQuest(Prerequisite) > 0;
        }

        public override string Description
        {
            get
            {
                var target = Definition.GetTargetForRealm(m_questPlayer?.Realm ?? eRealm.Albion);
                string liaisonText = (m_questPlayer?.Realm) switch
                {
                    eRealm.Albion => "Warden Alistair in Camelot (or Lord-Commander Caelan Vance in Edinburgh)",
                    eRealm.Midgard => "Warden Haldor in Jordheim (or Lord-Commander Caelan Vance in Edinburgh)",
                    eRealm.Hibernia => "Warden Cormac in Tir na nOg (or Lord-Commander Caelan Vance in Edinburgh)",
                    _ => "your Council Warden liaison"
                };
                return Step switch
                {
                    1 => $"{target.Clue}\n\n[Warden Briefing]: \"{target.BriefingDialogue}\"\n\nObjective: Travel to {target.ZoneName} and defeat {target.TargetName}.",
                    2 => $"[Mission Debrief]: \"{target.CompletionDialogue}\"\n\nDiscovery: {Definition.CuriosityDiscovery}\n\nObjective: Return to {liaisonText} to claim your [reward].",
                    _ => base.Description,
                };
            }
        }

        public override void OnQuestAssigned(GamePlayer player)
        {
            base.OnQuestAssigned(player);
            CouncilWardenEpicQuestRuntime.StartQuest(GetType());
            var target = Definition.GetTargetForRealm(player?.Realm ?? eRealm.Albion);
            player?.Out?.SendMessage($"[Warden Briefing]: \"{target.BriefingDialogue}\"", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player?.Out?.SendMessage($"[Objective]: Travel to {target.ZoneName} and defeat {target.TargetName}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            ShowMarker();
        }

        public void ShowMarker()
        {
            if (m_questPlayer == null)
                return;

            if (Step == 1)
            {
                var target = Definition.GetTargetForRealm(m_questPlayer.Realm);
                CouncilWardenQuestMapMarkers.Set(m_questPlayer, (ushort)target.Region, target.X, target.Y, target.Z);
            }
            else if (Step == 2)
            {
                GameNPC giver = CouncilWardenEpicQuestRuntime.GetPreferredTrainerForPlayer(m_questPlayer);
                if (giver != null)
                    CouncilWardenQuestMapMarkers.Set(m_questPlayer, giver.CurrentRegionID, giver.X, giver.Y, giver.Z);
                else
                    CouncilWardenQuestMapMarkers.Clear(m_questPlayer);
            }
            else
            {
                CouncilWardenQuestMapMarkers.Clear(m_questPlayer);
            }
        }

        public override void AbortQuest()
        {
            GamePlayer player = m_questPlayer;
            base.AbortQuest();
            CouncilWardenQuestMapMarkers.Clear(player);
        }

        public override void Notify(DOLEvent e, object sender, EventArgs args)
        {
        }

        public override void FinishQuest()
        {
            if (Step != 2)
                return;

            GamePlayer player = m_questPlayer;
            base.FinishQuest();
            CouncilWardenQuestMapMarkers.Clear(player);

            if (player != null)
            {
                long xpReward = RequiredLevel * 2500L;
                long copperReward = Money.GetMoney(0, 0, RequiredLevel * 2, 0, 0);
                player.GainExperience(eXPSource.Quest, xpReward);
                player.AddMoney(copperReward, "You are awarded {0} by the White Council!");

                GameNPC giver = CouncilWardenEpicQuestRuntime.GetPreferredTrainerForPlayer(player);
                if (this is CouncilWardenEpic50)
                {
                    CouncilWardenEpicArmor.GrantEpicSet(player, giver, reclaim: false);
                }
                else if (Definition.MilestonePiece != null)
                {
                    CouncilWardenEpicArmor.GivePiece(player, Definition.MilestonePiece, giver);
                }

                CouncilWardenEpicQuestRuntime.UpdateTrainerIndicator(player);
            }
        }
    }

    public class CouncilWardenEpic07 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic07() : base() { }
        public CouncilWardenEpic07(GamePlayer player) : base(player) { }
        public CouncilWardenEpic07(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic07(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => null;
        protected override int RequiredLevel => 7;
    }

    public sealed class CouncilWardenEpic13 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic13() : base() { }
        public CouncilWardenEpic13(GamePlayer player) : base(player) { }
        public CouncilWardenEpic13(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic13(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic07);
        protected override int RequiredLevel => 13;
    }

    public sealed class CouncilWardenEpic20 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic20() : base() { }
        public CouncilWardenEpic20(GamePlayer player) : base(player) { }
        public CouncilWardenEpic20(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic20(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic13);
        protected override int RequiredLevel => 20;
    }

    public sealed class CouncilWardenEpic25 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic25() : base() { }
        public CouncilWardenEpic25(GamePlayer player) : base(player) { }
        public CouncilWardenEpic25(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic25(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic20);
        protected override int RequiredLevel => 25;
    }

    public sealed class CouncilWardenEpic30 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic30() : base() { }
        public CouncilWardenEpic30(GamePlayer player) : base(player) { }
        public CouncilWardenEpic30(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic30(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic25);
        protected override int RequiredLevel => 30;
    }

    public sealed class CouncilWardenEpic35 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic35() : base() { }
        public CouncilWardenEpic35(GamePlayer player) : base(player) { }
        public CouncilWardenEpic35(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic35(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic30);
        protected override int RequiredLevel => 35;
    }

    public sealed class CouncilWardenEpic40 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic40() : base() { }
        public CouncilWardenEpic40(GamePlayer player) : base(player) { }
        public CouncilWardenEpic40(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic40(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic35);
        protected override int RequiredLevel => 40;
    }

    public sealed class CouncilWardenEpic45 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic45() : base() { }
        public CouncilWardenEpic45(GamePlayer player) : base(player) { }
        public CouncilWardenEpic45(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic45(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic40);
        protected override int RequiredLevel => 45;
    }

    public sealed class CouncilWardenEpic50 : CouncilWardenEpicQuest
    {
        public CouncilWardenEpic50() : base() { }
        public CouncilWardenEpic50(GamePlayer player) : base(player) { }
        public CouncilWardenEpic50(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic50(GamePlayer player, DbQuest quest) : base(player, quest) { }
        protected override Type Prerequisite => typeof(CouncilWardenEpic45);
        protected override int RequiredLevel => 50;

        public override string Description
        {
            get
            {
                var target = Definition.GetTargetForRealm(m_questPlayer?.Realm ?? eRealm.Albion);
                string liaisonText = (m_questPlayer?.Realm) switch
                {
                    eRealm.Albion => "Warden Alistair in Camelot (or Lord-Commander Caelan Vance in Edinburgh)",
                    eRealm.Midgard => "Warden Haldor in Jordheim (or Lord-Commander Caelan Vance in Edinburgh)",
                    eRealm.Hibernia => "Warden Cormac in Tir na nOg (or Lord-Commander Caelan Vance in Edinburgh)",
                    _ => "your Council Warden liaison"
                };
                return Step switch
                {
                    1 => $"{target.Clue}\n\n[Phase I - The Sanctuary Inquiry & Triangulation]: Take the Sight-Focus Crystal into Edinburgh's Heart-Stone chamber to detect the sympathetic link between Brigid and Mairead the Unbound. Then deploy the Silver Grounding Rods at the Nevernever Crossroads to reverse the sympathetic resonance and triangulate Morcant's cloaked sanctuary.\n\n[Phase II - The Nevernever Confrontation]: Step through the revealed rift into {target.ZoneName} and execute {target.TargetName} under the Seven Laws of Merlin!\n\n[Warden Briefing]: \"{target.BriefingDialogue}\"\n\nObjective: Travel to {target.ZoneName} and defeat {target.TargetName}.",
                    2 => $"[Mission Debrief]: \"{target.CompletionDialogue}\"\n\nDiscovery: {Definition.CuriosityDiscovery}\n\nObjective: Return to {liaisonText} to claim your full AF 102 Epic Armor set and Master Warden consecration.",
                    _ => base.Description,
                };
            }
        }
    }

    // Backward-compatibility alias
    public class CouncilWardenEpic10 : CouncilWardenEpic07
    {
        public CouncilWardenEpic10() : base() { }
        public CouncilWardenEpic10(GamePlayer player) : base(player) { }
        public CouncilWardenEpic10(GamePlayer player, int step) : base(player, step) { }
        public CouncilWardenEpic10(GamePlayer player, DbQuest quest) : base(player, quest) { }
    }

    public sealed record CouncilWardenRealmTarget(
        eRealm Realm,
        string TargetName,
        string ZoneName,
        string Clue,
        string BriefingDialogue,
        string CompletionDialogue,
        int Region,
        int X,
        int Y,
        int Z,
        byte Level,
        ushort Model,
        bool IsFae = false);

    public sealed class CouncilWardenEpicChapterDefinition
    {
        public required Type QuestType { get; init; }
        public required int ChapterNumber { get; init; }
        public required string Title { get; init; }
        public required byte QuestLevel { get; init; }
        public required string CuriosityDiscovery { get; init; }
        public required CouncilWardenEpicArmor.Piece MilestonePiece { get; init; }
        public required CouncilWardenRealmTarget AlbionTarget { get; init; }
        public required CouncilWardenRealmTarget MidgardTarget { get; init; }
        public required CouncilWardenRealmTarget HiberniaTarget { get; init; }

        public CouncilWardenRealmTarget GetTargetForRealm(eRealm realm) => realm switch
        {
            eRealm.Albion => AlbionTarget,
            eRealm.Midgard => MidgardTarget,
            eRealm.Hibernia => HiberniaTarget,
            _ => AlbionTarget,
        };

        public IEnumerable<CouncilWardenRealmTarget> AllTargets =>
            new[] { AlbionTarget, MidgardTarget, HiberniaTarget };
    }

    /// <summary>
    /// Manages the 9-chapter "The Web of Shadows" epic questline across Albion,
    /// Midgard, Hibernia, Edinburgh, and the Nevernever.
    /// </summary>
    public static class CouncilWardenEpicQuestRuntime
    {
        private static readonly object Sync = new();

        public static readonly IReadOnlyDictionary<Type, CouncilWardenEpicChapterDefinition> Definitions =
            new Dictionary<Type, CouncilWardenEpicChapterDefinition>
            {
                // -------------------------------------------------------------
                // Chapter 1 (Level 7): The Stolen Fire
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic07)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic07),
                    ChapterNumber = 1,
                    Title = "Chapter 1: The Stolen Fire",
                    QuestLevel = 7,
                    CuriosityDiscovery = "Fusing the warlock's chest is a Thaumaturgic Tuning Fork made of Roan-Forged Bronze—an alloy cast exclusively by Merlin and his inner circle thirty years ago!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter1Signet,
                    AlbionTarget = new(
                        eRealm.Albion, "Cerdic the Hollowed Conduit", "Camelot Hills",
                        "A village dowsing-man named Cerdic has called down an unnatural pillar of white lightning in Camelot Hills near the Roman watchtower, incinerating twelve royal armsmen in their mail.",
                        "By Arthur's tomb, an unlettered dowsing-man shouldn't possess the force to spark a hearth-fire, let alone cook twelve royal armsmen in their mail! The local barons are already whispering that the White Council cannot police its own. Ride to Camelot Hills, hunt down Cerdic before he incinerates another hamlet, and find out what burned him from within.",
                        "You struck swiftly, Warden. Look at the metal melted into his breastbone—that's Roan-Forged Bronze, an alloy Merlin and his inner circle cast thirty years ago! Why was Council metalwork inside a peasant warlock?",
                        1, 593100, 502050, 2067, 9, 33),
                    MidgardTarget = new(
                        eRealm.Midgard, "Torstein the Rune-Burned", "Vale of Mularn",
                        "A hermit carver named Torstein has drawn down a roaring tempest along the ridges of the Vale of Mularn, turning a jarl's shield-wall to smoking slag.",
                        "By Odin's eye, hermit Torstein barely knew three runes for curing sick goats, yet yesterday he called down a roaring tempest along the ridges of the Vale of Mularn that turned a jarl's shield-wall to smoking slag! Travel to the Vale of Mularn, put Torstein down before blood-feuds tear the valleys apart, and tear open his breast to see what foul forge made him a living thunderbolt.",
                        "Well struck, Warden. By Thor, look at the metal melted into his breastbone—that's Roan-Forged Bronze, an alloy Merlin and the southern wizards cast thirty winters ago! Why was Council metalwork buried in a Norse hermit's heart?",
                        100, 711250, 772050, 4572, 9, 159),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Bres the Ash-Channeled", "Lough Derg",
                        "A bog-witch named Bres has channeled a lethal emerald lightning-surge through the crannogs of Lough Derg, boiling six Celtic warders alive.",
                        "Danu preserve us, an unguided bog-gatherer shouldn't possess the spark to light an oil-lamp, yet Bres called down a searing pillar of emerald lightning along Lough Derg that boiled six Celtic warders alive! Travel to Lough Derg, stop Bres before his wildfire sets the sacred groves ablaze, and discover what unnatural force turned him into a living conduit.",
                        "The wildfire is quenched, Warden. But look at the metal driven into his breastbone—Roan-Forged Bronze, an alloy Merlin and the arch-druids cast thirty summers ago! How did ancient Council metalwork end up inside a humble bog-witch?",
                        200, 345550, 513980, 5897, 9, 302),
                },

                // -------------------------------------------------------------
                // Chapter 2 (Level 13): The Silence of the Archive
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic13)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic13),
                    ChapterNumber = 2,
                    Title = "Chapter 2: The Silence of the Archive",
                    QuestLevel = 13,
                    CuriosityDiscovery = "The Forgetfulness Wards were unlocked with an original Master Mason's Key from the founding of Edinburgh—one of four held by Merlin, Haldor, the First Captain, and Morcant of Caerleon!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter2Bracer,
                    AlbionTarget = new(
                        eRealm.Albion, "Sightless Scribe Aldous", "Tomb of Mithra",
                        "The Forgetfulness Wards around the Tomb of Mithra have dissolved. Scribes of the Unbound are transcribing the forbidden names of the Sleeper Beneath the Silt.",
                        "The Venatori spent centuries erasing the Old Gods from Roman scrolls. If those names are spoken in Camelot, madness will spread like plague. Enter the sunken delve of the Tomb of Mithra, execute Sightless Scribe Aldous, and deface the altar before the dark god's name catches like wildfire.",
                        "The altar is defaced, but look at this cylinder you pried from the vault door. That is a Master Mason's Key from the founding of Edinburgh! Only four existed: Merlin, Haldor, the First Captain, and Morcant of Caerleon... who was supposed to have perished at Camlann.",
                        1, 587200, 541100, 2350, 15, 35),
                    MidgardTarget = new(
                        eRealm.Midgard, "Sightless Scribe Fafnir", "Nisse's Lair",
                        "The ancient runic seal locking the burial mound beneath Nisse's Lair has been turned. Corrupted thralls are carving the forbidden runes of the Sleeper Beneath the Silt back into the stone.",
                        "The ancient runic seal locking the burial mound beneath Nisse's Lair has been turned! Decades ago, the Allfather and Merlin bound the frost-horrors beneath Ymir's skull. Corrupted thralls are carving the forbidden runes of the Sleeper Beneath the Silt back into the stone. Enter the depths of Nisse's Lair, strike down Sightless Scribe Fafnir, and gouge the obelisk clean before the nightmare wakes!",
                        "The stone is defaced, but look at the teeth on this key cylinder. That is a Master Mason's Key from the founding of Edinburgh! Only four were ever forged: Merlin, myself, the First Captain, and Morcant of Caerleon... who was supposed to have burned at Camlann.",
                        100, 735600, 812400, 4210, 15, 162),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Sightless Scribe Ruadan", "Muire Tomb",
                        "The Veil-wards shrouding Muire Tomb were unlatched from within. Cultists are copying forgotten ogham back onto vellum scrolls.",
                        "The Veil-wards shrouding Muire Tomb were unlatched from within! Decades ago, the Tuatha and Merlin sealed away the cult-shrines of the Sleeper Beneath the Silt to drown its memory forever. Cultists are copying forgotten ogham back onto vellum scrolls. Enter the sunken crypt of Muire Tomb, slay Sightless Scribe Ruadan, and chisel the obelisk clean before the bog-mist turns to poison.",
                        "The dark names are erased, but look at this key cylinder. It is an original Master Mason's Key from the founding of Edinburgh! Only four were ever made: Merlin, Haldor, the First Captain, and Morcant of Caerleon... who was supposed to have fallen at Camlann.",
                        200, 362400, 538900, 4900, 15, 304),
                },

                // -------------------------------------------------------------
                // Chapter 3 (Level 20): The Dragon's Compact
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic20)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic20),
                    ChapterNumber = 3,
                    Title = "Chapter 3: The Dragon's Compact",
                    QuestLevel = 20,
                    CuriosityDiscovery = "The mercenaries were withered by mass First Law death-magic without recoil, leaving behind twisted Blackened Ash Root-Bark from Mother Winter's walking stick!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter3Belt,
                    AlbionTarget = new(
                        eRealm.Albion, "Commander Gareth of the Ashen Band", "Avalon Isle",
                        "Tectonic tremors shake Avalon Isle. Pyrovax's subterranean brood is waking because a dragon's clutch-stone was ripped from the caldera.",
                        "Merlin forged the Compact of Ash with the Great Wyrms to steady the island's crust. If that drake awakens fully, half of Cornwall slides into the sea. Travel to Avalon Isle, hunt down Commander Gareth and his mercenary band, and quell the dragon's wrath before the island shatters.",
                        "You saved the coast, Warden. But look at these corpses—withered like dried leaves by massive First Law death-magic without a trace of mortal recoil. Only one focus absorbs black-magic corruption on that scale: Mother Winter's walking stick. Keep your lips sealed.",
                        51, 574620, 607150, 2171, 22, 302, IsFae: true),
                    MidgardTarget = new(
                        eRealm.Midgard, "Jarl Sigvald the Ashen", "Delling Crater",
                        "The earth-wyrms beneath Delling Crater roar in their slumber. Mercenaries stole the clutch-stone that anchors the northern volcanic veins.",
                        "The earth-wyrms beneath Delling Crater roar in their slumber! The Compact of Ash was struck to keep the fire-drakes sleeping beneath the volcanic vents. Mercenaries have stolen an egg-sized clutch-stone from the caldera. If the drake awakens fully, the northern seas will boil. March into Delling Crater, hunt down Jarl Sigvald, and quiet the wyrm's wrath!",
                        "The volcanic veins are steady again, Warden. But these dead mercenaries... withered to bone without a scratch, slaughtered by mass death-magic. Only one focus absorbs the black-curse without driving the caster mad: Mother Winter's walking stick. Keep your tongue behind your teeth.",
                        151, 770080, 890350, 4957, 22, 695, IsFae: true),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Captain Donnchadh the Ashen", "Grove of Domnann",
                        "The sacred volcanic roots beneath the Grove of Domnann are trembling. Thieves siphoned a clutch-stone from the sleeping brood of the earth-drake.",
                        "The sacred volcanic roots beneath the Grove of Domnann are trembling! Merlin's Compact of Ash bound the elder drakes to sleep beneath the roots. Mercenaries stole an egg-sized clutch-stone from the deep nest. If the dragon wakes, the western sea-shelf will fracture. Journey to the Grove of Domnann, hunt down Captain Donnchadh, return the egg to the altar, and recover whatever dark focus they left behind.",
                        "The roots of Domnann are peaceful once more. But these dead mercenaries... their life-force was stripped clean without a wound, yet the wizard walked away without a trace of warlock-madness. Only Mother Winter's walking stick absorbs that corruption. Speak of this to no one.",
                        181, 400840, 492560, 6219, 22, 302, IsFae: true),
                },

                // -------------------------------------------------------------
                // Chapter 4 (Level 25): The Chronomancer's Slip
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic25)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic25),
                    ChapterNumber = 4,
                    Title = "Chapter 4: The Chronomancer's Slip",
                    QuestLevel = 25,
                    CuriosityDiscovery = "In the chronomancer's dust lies an ancient Iron Torc of Caerleon bearing the crest of Morcant's personal retinue from fifty years ago—proving Morcant jumped forward through the timestream!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter4Necklace,
                    AlbionTarget = new(
                        eRealm.Albion, "Chronos-Weaver Claudius", "Salisbury Plains",
                        "Centurions of the lost Ninth Legion are phasing through the Salisbury mists. Someone is unraveling the Sixth Law at Merlin's standing stones.",
                        "Time is fraying across Salisbury Plains, Warden! Legionaries of the lost Ninth Legion are marching out of the mists, phasing between centuries. Someone is violating the Sixth Law at Merlin's standing stones. Take this lead-glass hourglass, travel to Salisbury Plains, draw Chronos-Weaver Claudius out of his time-bubble, and smash his reverse-flow anchor!",
                        "The chronomancer turned to dust the moment his borrowed years caught up with him. And look at this iron torc—it bears the personal badge of Morcant of Caerleon from fifty years ago. Morcant didn't die at Camlann; he leaped forward through the timestream to wait until Merlin was gone!",
                        1, 578900, 589100, 2210, 27, 40),
                    MidgardTarget = new(
                        eRealm.Midgard, "Chronos-Weaver Asbjorn", "Skona Ravine",
                        "Einherjar who fell at the Battle of the Frozen Fjord are appearing on the ridges of Skona Ravine. The temporal knots tied by Odin's seers are fraying.",
                        "The threads of the Norns are knotting backward in Skona Ravine! Einherjar who fell at the Battle of the Frozen Fjord two centuries ago are appearing along the ridges, caught in temporal loops. Someone is twisting the Sixth Law at the ancient monoliths. Take this lead-glass hourglass, journey into Skona Ravine, break the time-bubble, and defeat Chronos-Weaver Asbjorn!",
                        "The weaver turned to dust as his stolen winters caught up with him. And look at this iron torc—it bears the wolf-crest of Morcant of Caerleon! The traitor leaped forward through the timestream to strike when Merlin was gone!",
                        100, 736800, 936750, 4928, 27, 161),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Chronos-Weaver Cathaoir", "Bri Leith",
                        "Tuatha spear-singers from the First Age are marching along the cliffs of Bri Leith. A Sixth Law violation is tearing the centuries apart.",
                        "The song of time is unraveling at the standing stones of Bri Leith! Tuatha spear-singers from the First Age are marching along the cliffs, trapped in temporal echoes. A rogue wizard is violating the Sixth Law. Take this lead-glass hourglass, travel to Bri Leith, enter the time-bubble, strike down Chronos-Weaver Cathaoir, and restore the flow of the river of time!",
                        "The chronomancer withered into dust as his stolen centuries slipped away. And look at this iron torc—it bears the ancient crest of Morcant of Caerleon from fifty years ago! Morcant skipped forward through the stream of time to strike once Merlin was gone.",
                        200, 395620, 472210, 3236, 27, 305),
                },

                // -------------------------------------------------------------
                // Chapter 5 (Level 30): The Crossroads of Bone
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic30)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic30),
                    ChapterNumber = 5,
                    Title = "Chapter 5: The Crossroads of Bone",
                    QuestLevel = 30,
                    CuriosityDiscovery = "Inside a courier's satchel, a dying Warden carved a warning into his silver scabbard: 'Haldor's apprentice Torvald... met the Pale Man at the border... they call themselves the Unbound.'",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter5Ring,
                    AlbionTarget = new(
                        eRealm.Albion, "Greth the Marrow-Eater", "Frost-Thorn Gorge (The Nevernever)",
                        "Three Warden courier teams bound for Edinburgh vanished in the Ways. The Way-Beacon at the Summer-Winter border was deliberately diverted into the Frost-Thorn Gorge.",
                        "Three Warden courier teams bound for Edinburgh have vanished in the Ways. The Way-Beacon at the Summer-Winter border was deliberately diverted into the Frost-Thorn Gorge. Step through the great arch into the Nevernever, seek the blind seer Aurelius at the Crossroads, and hunt down Greth the Marrow-Eater in the Frost-Thorn Gorge.",
                        "You cleared the road, but read what the dying courier carved into his scabbard: 'Haldor's apprentice Torvald... met the Pale Man at the border... they call themselves the Unbound.' Morcant is recruiting right out of our apprentice halls.",
                        197, 26134, 24500, 16000, 32, 437, IsFae: true),
                    MidgardTarget = new(
                        eRealm.Midgard, "Greth the Marrow-Eater", "Frost-Thorn Gorge (The Nevernever)",
                        "Three Warden courier teams bound for Edinburgh vanished in the Ways. The Way-Beacon at the Summer-Winter border was deliberately diverted into the Frost-Thorn Gorge.",
                        "By the roots of Yggdrasil, our messengers didn't freeze—someone moved the runestones at the Nevernever border! Three courier units were diverted into the jaws of the Winter frost-barghests. Step through the great arch into the Nevernever, seek the blind seer Aurelius at the Crossroads, and slay Greth the Marrow-Eater in the Frost-Thorn Gorge!",
                        "The road is straight again. But read what the dying courier carved into his silver scabbard: 'Haldor's apprentice Torvald... met the Pale Man at the border... they call themselves the Unbound.' My own student... seduced by Morcant's lies.",
                        197, 26134, 24500, 16000, 32, 437, IsFae: true),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Greth the Marrow-Eater", "Frost-Thorn Gorge (The Nevernever)",
                        "Three Warden courier teams bound for Edinburgh vanished in the Ways. The Way-Beacon at the Summer-Winter border was deliberately diverted into the Frost-Thorn Gorge.",
                        "The song of the Ways is discordant! Three courier units bound for Edinburgh vanished in the mist. The Way-Beacon at the Summer-Winter border was diverted into the Frost-Thorn Gorge. Step through the great arch into the Nevernever, seek the blind seer Aurelius at the Crossroads, and slay Greth the Marrow-Eater in the Frost-Thorn Gorge.",
                        "The road is clear once more. But read what the dying courier carved into his scabbard: 'Haldor's apprentice Torvald... met the Pale Man at the border... they call themselves the Unbound.' The sickness has spread into our own apprentice classes.",
                        197, 26134, 24500, 16000, 32, 437, IsFae: true),
                },

                // -------------------------------------------------------------
                // Chapter 6 (Level 35): The Forge of the Silver Blades
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic35)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic35),
                    ChapterNumber = 6,
                    Title = "Chapter 6: The Forge of the Silver Blades",
                    QuestLevel = 35,
                    CuriosityDiscovery = "Torvald drops the First Ingot of Meteoric Silver and his bloodied smithing hammer. With his dying breath he sneers that the inner gates of Edinburgh are already unlatched for Morcant!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter6Bracer,
                    AlbionTarget = new(
                        eRealm.Albion, "Apostate Torvald", "Dartmoor",
                        "Torvald leads an Unbound strike cell against the secret meteoric foundry in Dartmoor to destroy the prototypes of the Warden silver blades.",
                        "Luccio's predecessor designed these blades to shear through enemy magic. If Torvald destroys the quenching vats, the Council will be left toothless. Ride to Dartmoor, hunt down Apostate Torvald, and defend the meteoric forge!",
                        "Torvald died spitting curses at Edinburgh. But you saved the first ingot of meteoric silver! Haldor will forge your final blade with his own hands. But heed Torvald's dying words: someone inside Edinburgh is holding the doors open for Morcant.",
                        1, 544600, 346250, 2889, 37, 44),
                    MidgardTarget = new(
                        eRealm.Midgard, "Apostate Torvald", "Uppland",
                        "Torvald has brought warlock-mercenaries into the high snows of Uppland to burn the sacred meteoric forge where our silver blades are born.",
                        "Torvald has brought warlock-mercenaries into the high snows of Uppland to burn our secret meteoric forge! That is where our smiths are casting the very first ward-cleaving silver blades. If Torvald collapses the cavern, our Wardens will have no weapons against dark sorcery. Ride to Uppland, hunt down Apostate Torvald, and protect the forge!",
                        "My boy is dead... he died for Morcant's lie. But you saved the first ingot of meteoric silver! I will stay at the anvil until my hands bleed to forge you a blade that will take Morcant's head. Watch Edinburgh closely—Torvald swore a founder holds the gates open.",
                        100, 695190, 982000, 2649, 37, 165),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Apostate Torvald", "Cursed Forest",
                        "Torvald's renegades have broken into the hidden grove foundry in the Cursed Forest, slaughtering the elven smiths to halt the forging of the silver swords.",
                        "Torvald has led a band of renegades into the deep groves of the Cursed Forest to destroy our secret meteoric foundry! That is where our smiths are casting the first ward-cleaving silver blades. If Torvald shatters the quenching vats, the Council will have no defense against black sorcery. Venture into the Cursed Forest, hunt down Apostate Torvald, and save the forge!",
                        "The foundry is saved, and you recovered the first ingot of meteoric silver! But Torvald's dying words echo like a curse: someone inside Edinburgh's inner circle is holding the doors open for Morcant.",
                        200, 362640, 659730, 3566, 37, 308),
                },

                // -------------------------------------------------------------
                // Chapter 7 (Level 40): The Bleeding Menhir
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic40)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic40),
                    ChapterNumber = 7,
                    Title = "Chapter 7: The Bleeding Menhir",
                    QuestLevel = 40,
                    CuriosityDiscovery = "The banished Outsider leaves behind an Ancient Poetic Ogham Tablet with ward calculations written in Loresinger Brigid's personal meter!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter7Jewel,
                    AlbionTarget = new(
                        eRealm.Albion, "Shaggrath of the Outer Night", "Lyonesse",
                        "The primary Continental Anchor in Lyonesse is weeping black bile. The sky has turned pitch black; the Unbound have whistled up an Outsider vanguard.",
                        "The Seventh Law has been broken! Only power from Outside reality can dissolve Merlin's wards without defensive blowback. Use your blade—it shrugs off spells! Ride to the grand menhir in Lyonesse and banish Shaggrath of the Outer Night before reality tears apart!",
                        "The Outsider is banished back into the dark! But look at this ogham tablet you recovered... ward calculations written in Ancient Poetic Ogham. Warden... only one person in Edinburgh writes ward-cipher in that meter: Loresinger Brigid.",
                        1, 391290, 717250, 240, 42, 1490, IsFae: true),
                    MidgardTarget = new(
                        eRealm.Midgard, "Shaggrath of the Outer Night", "Raumarik",
                        "The Great Menhir of Raumarik bleeds freezing ichor. Sound has died across the peak. An Outsider walker's thrall tears through the sky.",
                        "The Great Menhir of Raumarik is weeping freezing black ichor! Sound has died across the peak and the northern lights have gone black. The Unbound have broken the Seventh Law—they whistled up an Outsider vanguard to dissolve Merlin's wards without triggering the defensive runes. Ascend to the Great Menhir in Raumarik and cleave Shaggrath of the Outer Night with your cold silver blade!",
                        "The horror from beyond the World Rim is banished! But look at this ogham tablet... ward calculations written in Ancient Poetic Ogham. By the gods, only one person in Edinburgh writes ward-cipher like that: Loresinger Brigid.",
                        100, 638870, 767650, 4481, 42, 1490, IsFae: true),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Shaggrath of the Outer Night", "Sheeroe Hills",
                        "The sacred Continental Menhir in Sheeroe Hills is weeping black sap. An Outsider vanguard is anchoring itself with ritualist blood.",
                        "The sacred Continental Menhir in Sheeroe Hills is weeping black sap! The sky has turned ink-black and all bird-song has died. The Unbound have committed the ultimate sacrilege—they whistled up an Outsider vanguard to dissolve Merlin's wards without defensive blowback. Travel to the Continental Menhir in Sheeroe Hills and banish Shaggrath of the Outer Night back into the void!",
                        "The Outsider is banished back beyond the Veil! But look at this ogham tablet... ward calculations written in Ancient Poetic Ogham. Great Danu... only one person in Edinburgh writes ward-cipher in that meter: Loresinger Brigid.",
                        200, 372900, 663600, 3643, 42, 1490, IsFae: true),
                },

                // -------------------------------------------------------------
                // Chapter 8 (Level 45): The Relics of Mortality
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic45)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic45),
                    ChapterNumber = 8,
                    Title = "Chapter 8: The Relics of Mortality",
                    QuestLevel = 45,
                    CuriosityDiscovery = "Recovering Morgana's shed mortal relic breaks the Unbound's grand siphon and isolates Morcant's ritual site in the Deep Nevernever!",
                    MilestonePiece = CouncilWardenEpicArmor.Chapter8GreyCloak,
                    AlbionTarget = new(
                        eRealm.Albion, "Archon Malakor of the Void", "Caer Sidi",
                        "Infiltrate the threshold of Caer Sidi on the Isle of Avalon, cut down Archon Malakor, and recover Morgana's Athame before the Lich Lord's siphon drains the island's ley-lines.",
                        "Morcant has hidden the three mortal anchors of Morgana le Fay inside the epic citadels of the three realms. In Albion, he placed Morgana's Athame into the vaults of Caer Sidi, using its dark resonance to awaken the Lich Lord Apocalypse. Infiltrate the threshold of Caer Sidi, destroy Archon Malakor, and bring that cursed blade back to Edinburgh!",
                        "You breached Caer Sidi and recovered Morgana's Athame! Look at the cold-steel blade—it bears the raven crest of Tintagel, yet an oily, sickening void-rot clings to the edge. Morcant used her shed mortal malice to poison the roots of reality. Take the Warden's Grey Cloak. We have located his final retreat in the Nevernever!",
                        60, 31666, 35982, 18639, 47, 45),
                    MidgardTarget = new(
                        eRealm.Midgard, "Jarl Sigurd of the Void", "Tuscaran Glacier",
                        "Infiltrate the frozen threshold of Tuscaran Glacier, strike down Jarl Sigurd, and recover The Tear of Nimue before Queen Kula's siphon locks Midgard in eternal permafrost.",
                        "Morcant hid the three relics of Morgana's mortal soul inside the three grand epic fortresses of the world. In Midgard, he placed The Tear of Nimue into the howling vaults of Tuscaran Glacier, feeding Morgana's shed mortal grief to Queen Kula to freeze our lands into the Winter Nevernever. Enter Tuscaran Glacier, strike down Jarl Sigurd, and recover the sapphire teardrop!",
                        "You survived Tuscaran Glacier and secured The Tear of Nimue! The sapphire pulses with the raw, heartbroken cold Morgana wept when Merlin cast her out. Morcant fed that despair to the frost-jotuns. Don the Warden's Grey Mantle. The path to his final sanctuary in the Nevernever is opened!",
                        160, 35612, 17899, 19050, 47, 168),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Hierophant Lorcan of the Void", "Galladoria",
                        "Infiltrate the blighted roots of Galladoria, defeat Hierophant Lorcan, and recover The Root-Blood of Avalon before Olcasgean's siphon corrupts the Great Tree of HyBrasil.",
                        "Morcant has bound the three vessels of Morgana's discarded mortality inside the epic strongholds of the three realms. In Hibernia, he placed The Root-Blood of Avalon beneath the ancient boughs of Galladoria, feeding her discarded mortal blood to Olcasgean to turn the Great Tree into a psychic plague-pump. Venture into Galladoria, strike down Hierophant Lorcan, and reclaim the sacred vessel!",
                        "The rot in Galladoria is broken, and you recovered The Root-Blood of Avalon! When Morgana shed her mortal flesh to become Queen of Winter, she bled into the soil. Morcant warped that holy blood with the taint of the Outside. Take the Warden's Grey Cloak. The way into his redoubt in the Nevernever is now laid bare!",
                        191, 32020, 29622, 17045, 47, 310),
                },

                // -------------------------------------------------------------
                // Chapter 9 (Level 50): The Reckoning in the Nevernever
                // -------------------------------------------------------------
                [typeof(CouncilWardenEpic50)] = new()
                {
                    QuestType = typeof(CouncilWardenEpic50),
                    ChapterNumber = 9,
                    Title = "Chapter 9: The Reckoning in the Nevernever",
                    QuestLevel = 50,
                    CuriosityDiscovery = "By using the Sight-Focus Crystal to expose Mairead's sympathetic link and planting the Silver Grounding Rods at the Nevernever Crossroads, you triangulated Morcant's cloaked sanctuary! The First Blackstaff has fallen, his Outsider contagion shattered, and Mother Winter's Blackstaff secured in Edinburgh's deepest vaults as you are consecrated a Master Warden of the White Council!",
                    MilestonePiece = null,
                    AlbionTarget = new(
                        eRealm.Albion, "Morcant of Caerleon, the First Blackstaff", "The Deep Nevernever",
                        "Conduct the Warden's Inquiry in the Heart-Stone chamber with the Sight-Focus Crystal to expose the sympathetic mole, triangulate Morcant's cloaked redoubt using the Silver Grounding Rods at the Nevernever Crossroads, and breach the rift to execute the First Blackstaff!",
                        "With the three mortal relics recovered from the Shrouded Isles, Morcant's grand siphon is broken, but his final redoubt remains hidden behind an astral cloak. Before we strike, you must complete the investigation: take this Sight-Focus Crystal into the Heart-Stone chamber to trace the leak. Brigid's twin sister Mairead is riding their shared blood to hide Morcant's coordinates! Plant the Silver Grounding Rods at the Nevernever Crossroads to reverse the sympathetic bond, triangulate his redoubt, breach the rift into the Deep Nevernever, and bring Morcant of Caerleon to justice under the Laws of the Council!",
                        "The triangulation held and the First Blackstaff is dead! His Outsider contagion is severed, Mother Winter's Blackstaff is secured in Edinburgh's deepest vaults, and the White Council stands inviolate. Stand tall, Master Warden. Camelot and the Senior Council salute your vigil!",
                        197, 32000, 32500, 16000, 53, 63),
                    MidgardTarget = new(
                        eRealm.Midgard, "Morcant of Caerleon, the First Blackstaff", "The Deep Nevernever",
                        "Conduct the Warden's Inquiry in the Heart-Stone chamber with the Sight-Focus Crystal to expose the sympathetic mole, triangulate Morcant's cloaked redoubt using the Silver Grounding Rods at the Nevernever Crossroads, and breach the rift to execute the First Blackstaff!",
                        "The relics of Morgana are safe, but the wolf's lair remains cloaked in the void. Before drawing steel, you must finish the inquiry: take this Sight-Focus Crystal into the Heart-Stone chamber to sniff out the traitor. Brigid's twin sister Mairead has been using their shared blood to mask Morcant's hall! Take the Silver Grounding Rods to the Nevernever Crossroads, reverse the sympathetic bond to triangulate his coordinates, breach the rift into the Deep Nevernever, and sever Morcant's thread under the Laws of the Council!",
                        "The triangulation held and the traitor Morcant is slain! His Outsider contagion is shattered, Mother Winter's Blackstaff is locked beneath Edinburgh's vaults, and the White Council's honor is redeemed. Raise your horn, Master Warden—Jordheim and the Allfather honor your triumph!",
                        197, 32000, 32500, 16000, 53, 63),
                    HiberniaTarget = new(
                        eRealm.Hibernia, "Morcant of Caerleon, the First Blackstaff", "The Deep Nevernever",
                        "Conduct the Warden's Inquiry in the Heart-Stone chamber with the Sight-Focus Crystal to expose the sympathetic mole, triangulate Morcant's cloaked redoubt using the Silver Grounding Rods at the Nevernever Crossroads, and breach the rift to execute the First Blackstaff!",
                        "The sacred mortal relics are reclaimed, but Morcant's citadel remains shrouded behind a veil of false glamour. To find him, you must finish the investigation: take this Sight-Focus Crystal into the Heart-Stone chamber to unmask the leak. Brigid's twin sister Mairead has been whispering through their soul-bond to shield Morcant's sanctuary! Plant the Silver Grounding Rods at the Nevernever Crossroads, reverse the sympathetic flow to triangulate his redoubt, breach the astral rift into the Deep Nevernever, and bring the First Blackstaff to justice under the Laws of the Council!",
                        "The triangulation pierced his veil, and the First Blackstaff has fallen! His Outsider contagion is severed, the Blackstaff rests in Edinburgh's deep vaults, and the harmony of the realms is restored. Stand tall, Master Warden—the sacred groves and the White Council are forever in your debt!",
                        197, 32000, 32500, 16000, 53, 63),
                },
            };

        private static readonly Dictionary<(Type QuestType, eRealm Realm), GameNPC> ActiveTargets = new();

        public static CouncilWardenEpicChapterDefinition GetDefinition(Type questType) => Definitions[questType];

        public static bool IsCouncilWardenQuestGiver(GameNPC npc)
        {
            if (npc == null)
                return false;

            if (npc is DOL.GS.Trainer.AlbionMaulerTrainer
                or DOL.GS.Trainer.MidgardMaulerTrainer
                or DOL.GS.Trainer.VampiirTrainer)
            {
                return true;
            }

            return npc.Name is "Lord-Commander Caelan Vance" or "High Warden Torin Lachlan" or "Drillmaster Callan Boyd"
                or "First Captain Valerius" or "Loresinger Brigid" or "Runemaster Haldor"
                or "Warden Alistair" or "Warden Vance"
                or "Warden Haldor" or "Warden Sigrun"
                or "Warden Cormac" or "Warden Aisling";
        }

        public static GameNPC GetPreferredTrainerForPlayer(GamePlayer player)
        {
            if (player == null)
                return null;

            string preferredName = player.Realm switch
            {
                eRealm.Albion => "Warden Alistair",
                eRealm.Midgard => "Warden Haldor",
                eRealm.Hibernia => "Warden Cormac",
                _ => "Lord-Commander Caelan Vance",
            };

            return WorldMgr.GetNPCsByName(preferredName, player.Realm).FirstOrDefault()
                ?? WorldMgr.GetNPCsByName("Lord-Commander Caelan Vance", eRealm.None).FirstOrDefault()
                ?? WorldMgr.GetNPCsByName("Drillmaster Callan Boyd", eRealm.None).FirstOrDefault();
        }

        public static Type GetNextQuest(GamePlayer player)
        {
            if (player == null || !CouncilWardenMechanics.IsCouncilWarden(player))
                return null;

            foreach (Type questType in Definitions.Keys.OrderBy(t => Definitions[t].QuestLevel))
            {
                if (player.IsDoingQuest(questType) != null)
                    return questType;

                if (player.HasFinishedQuest(questType) > 0)
                    continue;

                var def = Definitions[questType];
                Type prereq = GetPrerequisite(questType);
                if (player.Level >= def.QuestLevel && (prereq == null || player.HasFinishedQuest(prereq) > 0))
                    return questType;
            }

            return null;
        }

        public static Type GetPrerequisite(Type questType) => questType switch
        {
            var t when t == typeof(CouncilWardenEpic13) => typeof(CouncilWardenEpic07),
            var t when t == typeof(CouncilWardenEpic20) => typeof(CouncilWardenEpic13),
            var t when t == typeof(CouncilWardenEpic25) => typeof(CouncilWardenEpic20),
            var t when t == typeof(CouncilWardenEpic30) => typeof(CouncilWardenEpic25),
            var t when t == typeof(CouncilWardenEpic35) => typeof(CouncilWardenEpic30),
            var t when t == typeof(CouncilWardenEpic40) => typeof(CouncilWardenEpic35),
            var t when t == typeof(CouncilWardenEpic45) => typeof(CouncilWardenEpic40),
            var t when t == typeof(CouncilWardenEpic50) => typeof(CouncilWardenEpic45),
            _ => null,
        };

        public static eQuestIndicator GetTrainerQuestIndicator(GamePlayer player, GameNPC trainer)
        {
            if (player == null || trainer == null || !IsCouncilWardenQuestGiver(trainer))
                return eQuestIndicator.None;

            if (!CouncilWardenMechanics.IsCouncilWarden(player))
                return eQuestIndicator.None;

            Type questType = GetNextQuest(player);
            if (questType == null)
                return eQuestIndicator.None;

            if (player.IsDoingQuest(questType) is CouncilWardenEpicQuest active)
                return active.Step == 2 ? eQuestIndicator.Finish : eQuestIndicator.None;

            return eQuestIndicator.Available;
        }

        public static void UpdateTrainerIndicator(GamePlayer player)
        {
            if (player == null)
                return;

            GameNPC trainer = GetPreferredTrainerForPlayer(player);
            if (trainer != null)
                player.Out?.SendNPCsQuestEffect(trainer, GetTrainerQuestIndicator(player, trainer));
        }

        public static void HandleTrainerInteraction(GamePlayer player, GameNPC trainer)
        {
            if (player == null || trainer == null || !CouncilWardenMechanics.IsCouncilWarden(player))
                return;

            Type questType = GetNextQuest(player);
            CouncilWardenEpicQuest active = questType == null ? null : player.IsDoingQuest(questType) as CouncilWardenEpicQuest;

            if (active != null)
            {
                var target = active.Definition.GetTargetForRealm(player.Realm);
                trainer.SayTo(player, active.Step == 1
                    ? $"\"{target.BriefingDialogue}\" Objective: Travel to {target.ZoneName} and defeat {target.TargetName}."
                    : $"\"{target.CompletionDialogue}\" {active.Definition.CuriosityDiscovery} Speak [reward] when you are ready to receive the Council's commendation.");
            }
            else if (questType != null)
            {
                var def = Definitions[questType];
                var target = def.GetTargetForRealm(player.Realm);
                trainer.SayTo(player,
                    $"Edinburgh has a priority dossier for you, Warden: {def.Title}. \"{target.BriefingDialogue}\" Whisper [begin] when you are ready to accept the assignment, or [cloak] if you need a replacement Initiate's Grey Cloak.");
            }
            else if (player.HasFinishedQuest(typeof(CouncilWardenEpic50)) > 0)
            {
                trainer.SayTo(player,
                    "You stand among the Senior Wardens of the White Council. If any piece of your Epic panoply or Silver Blade is missing, whisper [reclaim the set].");
            }
            else
            {
                trainer.SayTo(player,
                    "Keep honing your blade and your wards, Warden. More dossiers from Edinburgh will open as your experience grows (Levels 7, 13, 20, 25, 30, 35, 40, 45, and 50). Whisper [cloak] if you ever need your Initiate's Grey Cloak.");
            }
        }

        public static bool HandleTrainerWhisper(GamePlayer player, GameNPC trainer, string text)
        {
            if (player == null || trainer == null || string.IsNullOrWhiteSpace(text) || !CouncilWardenMechanics.IsCouncilWarden(player))
                return false;

            string cmd = text.Trim().ToLowerInvariant();
            Type questType = GetNextQuest(player);
            CouncilWardenEpicQuest active = questType == null ? null : player.IsDoingQuest(questType) as CouncilWardenEpicQuest;

            if (cmd is "cloak" or "initiate's grey cloak" or "grey cloak")
            {
                CouncilWardenEpicArmor.GivePiece(player, CouncilWardenEpicArmor.InitiateGreyCloak, trainer);
                return true;
            }

            if (active != null && active.Step == 2 && cmd is "reward" or "claim reward")
            {
                active.FinishQuest();
                return true;
            }

            if (cmd is "reclaim the set" or "reclaim" && player.HasFinishedQuest(typeof(CouncilWardenEpic50)) > 0)
            {
                CouncilWardenEpicArmor.GrantEpicSet(player, trainer, reclaim: true);
                return true;
            }

            if (active == null && questType != null && cmd is "begin" or "begun" or "start" or "accept")
            {
                EnsureTrainerQuestsRegistered(trainer);
                QuestMgr.ProposeQuestToPlayer(questType,
                    $"Will you accept Edinburgh's dossier for {Definitions[questType].Title}?", player, trainer);
                return true;
            }

            return false;
        }

        public static void EnsureTrainerQuestsRegistered(GameNPC trainer)
        {
            if (trainer == null)
                return;

            foreach (Type questType in Definitions.Keys)
                trainer.AddQuestToGive(questType);
        }

        private sealed class CouncilWardenQuestTargetNPC : GameNPC
        {
            protected override int RespawnTimerCallback(ECSGameTimer respawnTimer)
            {
                int result = base.RespawnTimerCallback(respawnTimer);
                if (ObjectState == eObjectState.Active)
                    RegisterTargetHandler(this);
                return result;
            }
        }

        public static void StartQuest(Type questType)
        {
            if (!Definitions.TryGetValue(questType, out var definition))
                return;

            lock (Sync)
            {
                foreach (var targetSpec in definition.AllTargets)
                {
                    SpawnOrRefreshTarget(questType, targetSpec);
                }
            }
        }

        private static void SpawnOrRefreshTarget(Type questType, CouncilWardenRealmTarget spec)
        {
            var key = (questType, spec.Realm);
            if (ActiveTargets.TryGetValue(key, out GameNPC existing))
            {
                if (existing.ObjectState == GameObject.eObjectState.Active || existing.IsRespawning)
                    return;
                ActiveTargets.Remove(key);
            }

            Region region = WorldMgr.GetRegion((ushort)spec.Region);
            if (region == null || region.IsDisabled)
                return;

            GameNPC found = WorldMgr.GetNPCsByName(spec.TargetName, eRealm.None)
                .FirstOrDefault(n => n.CurrentRegionID == spec.Region);

            GameNPC npc = found ?? new CouncilWardenQuestTargetNPC();
            npc.Name = spec.TargetName;
            npc.GuildName = string.Empty;
            npc.Model = spec.Model;
            npc.Size = 52;
            npc.Level = spec.Level;
            npc.Realm = eRealm.None;
            npc.CurrentRegionID = (ushort)spec.Region;
            npc.CurrentRegion = region;
            npc.X = spec.X;
            npc.Y = spec.Y;
            npc.Z = spec.Z;
            npc.Heading = 0;
            npc.MaxSpeedBase = 200;
            npc.RespawnInterval = 0;
            npc.BodyType = (ushort)NpcTemplateMgr.eBodyType.Humanoid;
            npc.LoadedFromScript = true;

            if (spec.IsFae)
                npc.TempProperties.SetProperty(CouncilWardenMechanics.FAE_ENTITY_PROPERTY, true);

            if (found == null)
            {
                npc.SetOwnBrain(new StandardMobBrain { AggroLevel = 100, AggroRange = 55 });
                npc.AddToWorld();
            }

            ActiveTargets[key] = npc;
            RegisterTargetHandler(npc);
        }

        private static void RegisterTargetHandler(GameNPC target)
        {
            if (target == null)
                return;

            GameEventMgr.RemoveHandler(target, GameLivingEvent.Dying, TargetDying);
            GameEventMgr.AddHandler(target, GameLivingEvent.Dying, TargetDying);
        }

        private static void TargetDying(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GameNPC target || args is not DyingEventArgs dying)
                return;

            Type questType;
            lock (Sync)
            {
                questType = ActiveTargets.FirstOrDefault(pair => ReferenceEquals(pair.Value, target)).Key.QuestType;
                if (questType == null)
                    return;
            }

            GamePlayer killer = dying.Killer as GamePlayer;
            if (killer == null && dying.Killer is GameSummonedPet pet)
                killer = pet.Owner as GamePlayer;
            if (killer == null)
                return;

            List<GamePlayer> participants = new() { killer };
            if (killer.Group != null)
                participants.AddRange(killer.Group.GetPlayersInTheGroup().Where(p => p != null && !participants.Contains(p)));

            foreach (GamePlayer player in participants)
            {
                if (player.IsDoingQuest(questType) is CouncilWardenEpicQuest quest && quest.Step == 1)
                {
                    quest.Step = 2;
                    quest.ShowMarker();
                    var realmTarget = quest.Definition.GetTargetForRealm(player.Realm);
                    string returnNpc = player.Realm switch
                    {
                        eRealm.Albion => "Warden Alistair in Camelot (or Lord-Commander Caelan Vance in Edinburgh)",
                        eRealm.Midgard => "Warden Haldor in Jordheim (or Lord-Commander Caelan Vance in Edinburgh)",
                        eRealm.Hibernia => "Warden Cormac in Tir na nOg (or Lord-Commander Caelan Vance in Edinburgh)",
                        _ => "your Council Warden liaison"
                    };
                    player.Out?.SendMessage(
                        $"{target.Name} is defeated! {realmTarget.CompletionDialogue} {quest.Definition.CuriosityDiscovery} Return to {returnNpc} for your reward.",
                        eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                }
            }
        }

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            CouncilWardenEpicArmor.EnsureTemplates();

            if (!ServerProperties.Properties.LOAD_QUESTS)
                return;

            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, PlayerEntered);
            GameEventMgr.AddHandler(GamePlayerEvent.Quit, PlayerQuit);
            GameEventMgr.AddHandler(GamePlayerEvent.AcceptQuest, AcceptQuest);

            lock (Sync)
            {
                foreach ((Type questType, var def) in Definitions)
                {
                    foreach (var targetSpec in def.AllTargets)
                    {
                        SpawnOrRefreshTarget(questType, targetSpec);
                    }
                }
            }
        }

        [ScriptUnloadedEvent]
        public static void ScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GamePlayerEvent.GameEntered, PlayerEntered);
            GameEventMgr.RemoveHandler(GamePlayerEvent.Quit, PlayerQuit);
            GameEventMgr.RemoveHandler(GamePlayerEvent.AcceptQuest, AcceptQuest);
        }

        private static void PlayerEntered(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer player && GetNextQuest(player) is Type questType &&
                player.IsDoingQuest(questType) is CouncilWardenEpicQuest quest)
            {
                quest.ShowMarker();
            }
        }

        private static void PlayerQuit(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer player)
                CouncilWardenQuestMapMarkers.Clear(player);
        }

        private static void AcceptQuest(DOLEvent e, object sender, EventArgs args)
        {
            if (e != GamePlayerEvent.AcceptQuest || args is not QuestEventArgs questArgs)
                return;

            Type questType = QuestMgr.GetQuestTypeForID(questArgs.QuestID);
            if (questType == null || !Definitions.ContainsKey(questType))
                return;

            if (questArgs.Source is GameNPC giver && IsCouncilWardenQuestGiver(giver))
            {
                EnsureTrainerQuestsRegistered(giver);
                if (giver.CanGiveQuest(questType, questArgs.Player) > 0)
                    giver.GiveQuest(questType, questArgs.Player, 1);
            }
        }
    }
}
