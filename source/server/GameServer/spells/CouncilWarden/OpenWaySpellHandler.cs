using System;
using System.Collections.Generic;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Spells
{
    public enum FaerieResonance
    {
        Summer,
        Winter,
        EdinburghWay,
        StepIntoEdinburgh,
        ReturnToNevernever,
        ReturnToMortal
    }

    public readonly record struct OpenWayDestination(
        ushort RegionId,
        int X,
        int Y,
        int Z,
        ushort Heading,
        FaerieResonance Resonance,
        string ArrivalMessage);

    /// <summary>
    /// Dynamic Resonance System for the "Open Way" (Apertura Viae) spell and the
    /// Silver Thimble of the Veil black-market talisman.
    /// Maps lush/sunny mortal zones to Summer Faerie, bleak/frozen zones to Winter Faerie,
    /// Hadrian's Wall to the Edinburgh Crossing, and Nevernever/Edinburgh back to the mortal world.
    /// </summary>
    public static class OpenWayResonanceTable
    {
        public const int OpenWaySpellId = 62090;

        public const ushort EdinburghTier1Region = 65;
        public const ushort EdinburghTier2Region = 192;
        public const ushort NeverneverCentralRegion = 197;
        public const ushort SummerCourtRegion = 196;
        public const ushort WinterCourtRegion = 195;
        public const ushort QueensLabyrinthRegion = 94;

        private const string ReturnRegionKey = "OpenWay.ReturnRegion";
        private const string ReturnXKey = "OpenWay.ReturnX";
        private const string ReturnYKey = "OpenWay.ReturnY";
        private const string ReturnZKey = "OpenWay.ReturnZ";
        private const string ReturnHeadingKey = "OpenWay.ReturnHeading";

        // Landings inside Region 197 (The Nevernever Central Crossing & Borderlands)
        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) SummerWaystone =
            (NeverneverCentralRegion, 42134, 34920, 16000, 1024);

        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) WinterWaystone =
            (NeverneverCentralRegion, 26134, 26920, 16000, 3072);

        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) EdinburghWaystoneInNevernever =
            (NeverneverCentralRegion, 32000, 32000, 16000, 2048);

        // Landing inside Region 65 (Edinburgh Tier 1: The Catacombs & Diplomatic Cloister - The Ways Ingress)
        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) EdinburghWaysIngress =
            (EdinburghTier1Region, 37210, 29150, 16400, 0);

        // Default Mortal Realm Safe Starter Villages (Cotswold, Mularn, Mag Mell) & Neutral Frontier Hillfort
        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) AlbionMortalNexus =
            (1, 564976, 509200, 2694, 2048); // Cotswold Village

        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) MidgardMortalNexus =
            (100, 808830, 725039, 4815, 2048); // Vale of Mularn

        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) HiberniaMortalNexus =
            (200, 348198, 492819, 5290, 2048); // Mag Mell Village

        public static readonly (ushort RegionId, int X, int Y, int Z, ushort Heading) FrontierHillfortNexus =
            (1, 635904, 336640, 4260, 180); // Hadrian's Wall Hillfort (Neutral Frontier)

        // Lush / sunny / temperate zones that resonate with the Summer Court (Seelie Faerie)
        private static readonly HashSet<ushort> SummerZones = new()
        {
            // Albion: Camelot Hills, Salisbury Plains, Cornwall, Campacorentin, Lyonesse, Forest Sauvage, Camelot, SI Lush
            0, 1, 6, 8, 10, 11, 27, 51, 52, 55,
            // Midgard: Vale of Mularn, East Svealand, Gotar, Jordheim, Aegirhamn
            100, 101, 103, 125, 151,
            // Hibernia: Lough Derg, Silvermine Mts., Shannon Estuary, Connacht, Bri Leith, Valley of Bri Leith, Tir na Nog, Domnann
            200, 201, 202, 204, 205, 206, 225, 181
        };

        // Bleak / frozen / marsh / mountain zones that resonate with the Winter Court (Unseelie Faerie)
        private static readonly HashSet<ushort> WinterZones = new()
        {
            // Albion: Black Mtns South/North, Dartmoor, Llyn Barfog, Avalon Marsh, Snowdonia, Pennine Mts, Devwy, Gwyddneau
            2, 3, 4, 7, 9, 12, 14, 53, 54,
            // Midgard: Skona Ravine, Myrkwood, Raumarik, Uppland, Yggdra, Jamtland, Odin's Gate, Vanern Swamp, Malmohus, Glacier
            102, 104, 105, 106, 107, 108, 111, 112, 115, 152, 153, 160,
            // Hibernia: Cliffs of Moher, Bog of Cullen, Cursed Forest, Breifine, Cruachan Gorge, Mount Collory, Emain Macha
            203, 207, 208, 210, 211, 212, 214, 182, 183
        };

        public static bool IsNeverneverOrEdinburgh(ushort regionId) =>
            regionId is EdinburghTier1Region or EdinburghTier2Region
                or NeverneverCentralRegion or SummerCourtRegion
                or WinterCourtRegion or QueensLabyrinthRegion;

        /// <summary>
        /// Classifies a mortal region and zone into its corresponding Faerie resonance.
        /// </summary>
        public static FaerieResonance ClassifyZone(ushort regionId, ushort zoneId)
        {
            // Hadrian's Wall (Zone 15 in Region 1) resonates directly with the Edinburgh Crossing
            if (regionId == 1 && zoneId == 15)
                return FaerieResonance.EdinburghWay;

            // Capital cities (Camelot 10, Jordheim 101, Tir na Nog 201) resonate with Summer Borderlands
            if (regionId is 10 or 101 or 201)
                return FaerieResonance.Summer;

            if (WinterZones.Contains(zoneId))
                return FaerieResonance.Winter;

            if (SummerZones.Contains(zoneId))
                return FaerieResonance.Summer;

            // Fallback: Midgard defaults to Winter Faerie unless in a lush zone; Albion/Hibernia default to Summer
            return regionId is 100 or 150 ? FaerieResonance.Winter : FaerieResonance.Summer;
        }

        public static (ushort RegionId, int X, int Y, int Z, ushort Heading) GetDefaultMortalNexus(eRealm realm) =>
            realm switch
            {
                eRealm.Midgard => MidgardMortalNexus,
                eRealm.Hibernia => HiberniaMortalNexus,
                _ => AlbionMortalNexus
            };

        public static OpenWayDestination ResolveDestination(
            ushort regionId,
            ushort zoneId,
            int x,
            int y,
            eRealm realm,
            (ushort RegionId, int X, int Y, int Z, ushort Heading)? savedMortalOrigin = null)
        {
            if (IsNeverneverOrEdinburgh(regionId))
            {
                // If casting inside Edinburgh (Tier 1 Catacombs 65 or Tier 2 Grand Conclave 192):
                // Open Way ALWAYS leads back into The Nevernever at the Edinburgh Waystone.
                // (Route C eliminated - players must head back into the Nevernever from Edinburgh).
                if (regionId is EdinburghTier1Region or EdinburghTier2Region)
                {
                    return new OpenWayDestination(
                        EdinburghWaystoneInNevernever.RegionId,
                        EdinburghWaystoneInNevernever.X,
                        EdinburghWaystoneInNevernever.Y,
                        EdinburghWaystoneInNevernever.Z,
                        EdinburghWaystoneInNevernever.Heading,
                        FaerieResonance.ReturnToNevernever,
                        "You channel the Open Way, stepping through the warded veil out of Edinburgh and into The Nevernever at the Edinburgh Waystone.");
                }

                // If casting near the Edinburgh Waystone inside Region 197, step into Edinburgh Tier 1 (Catacombs)
                if (regionId == NeverneverCentralRegion)
                {
                    long dx = x - EdinburghWaystoneInNevernever.X;
                    long dy = y - EdinburghWaystoneInNevernever.Y;
                    if (dx * dx + dy * dy <= 800L * 800L)
                    {
                        return new OpenWayDestination(
                            EdinburghWaysIngress.RegionId,
                            EdinburghWaysIngress.X,
                            EdinburghWaysIngress.Y,
                            EdinburghWaysIngress.Z,
                            EdinburghWaysIngress.Heading,
                            FaerieResonance.StepIntoEdinburgh,
                            "The Waystone's silver runes flare! You channel the Open Way and step through the warded veil into the Catacombs of Edinburgh.");
                    }
                }

                // When casting elsewhere in The Nevernever or outer borderlands:
                // Safely return to the player's realm starter village (Cotswold, Mularn, or Mag Mell)
                var safeTown = GetDefaultMortalNexus(realm);
                return new OpenWayDestination(
                    safeTown.RegionId,
                    safeTown.X,
                    safeTown.Y,
                    safeTown.Z,
                    safeTown.Heading,
                    FaerieResonance.ReturnToMortal,
                    "You part the shimmering curtain of the Nevernever and step safely back into your home realm.");
            }

            FaerieResonance resonance = ClassifyZone(regionId, zoneId);
            return resonance switch
            {
                FaerieResonance.EdinburghWay => new OpenWayDestination(
                    EdinburghWaystoneInNevernever.RegionId,
                    EdinburghWaystoneInNevernever.X,
                    EdinburghWaystoneInNevernever.Y,
                    EdinburghWaystoneInNevernever.Z,
                    EdinburghWaystoneInNevernever.Heading,
                    FaerieResonance.EdinburghWay,
                    "The ancient Roman wards along Hadrian's Wall answer your call, opening a Way directly to the Edinburgh Crossing in the Nevernever!"),

                FaerieResonance.Winter => new OpenWayDestination(
                    WinterWaystone.RegionId,
                    WinterWaystone.X,
                    WinterWaystone.Y,
                    WinterWaystone.Z,
                    WinterWaystone.Heading,
                    FaerieResonance.Winter,
                    "Hoarfrost crystallizes in the air as you tear open a Way into the freezing borderlands of Winter Faerie! Stick to the warded cobblestone road!"),

                _ => new OpenWayDestination(
                    SummerWaystone.RegionId,
                    SummerWaystone.X,
                    SummerWaystone.Y,
                    SummerWaystone.Z,
                    SummerWaystone.Heading,
                    FaerieResonance.Summer,
                    "Golden pollen and wild fragrance spill through the rift as you step into the borderlands of Summer Faerie! Stick to the warded cobblestone road!")
            };
        }

        public static OpenWayDestination ResolveForPlayer(GamePlayer player)
        {
            if (player == null)
                return ResolveDestination(1, 1, 0, 0, eRealm.Albion);

            ushort regionId = player.CurrentRegionID;
            ushort zoneId = player.CurrentZone?.ID ?? 0;

            return ResolveDestination(regionId, zoneId, player.X, player.Y, player.Realm);
        }

        /// <summary>
        /// Opens a direct Way to Edinburgh Tier 1 (The Ways Ingress in the Catacombs) on behalf of a
        /// White Council Liaison, recording the player's mortal origin so they can return via Open Way.
        /// </summary>
        public static bool OpenDirectWayToEdinburgh(GamePlayer player, GameNPC liaison = null)
        {
            if (player == null)
                return false;

            if (player.InCombat || GameRelic.IsPlayerCarryingRelic(player))
            {
                player.Out?.SendMessage("You cannot traverse a Way while in combat!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!IsNeverneverOrEdinburgh(player.CurrentRegionID) && player.TempProperties != null)
            {
                player.TempProperties.SetProperty(ReturnRegionKey, (int)player.CurrentRegionID);
                player.TempProperties.SetProperty(ReturnXKey, player.X);
                player.TempProperties.SetProperty(ReturnYKey, player.Y);
                player.TempProperties.SetProperty(ReturnZKey, player.Z);
                player.TempProperties.SetProperty(ReturnHeadingKey, (int)player.Heading);
            }

            string actorName = !string.IsNullOrEmpty(liaison?.Name) ? liaison.Name : "The White Council Liaison";
            player.Out?.SendMessage(
                $"{actorName} traces a silver sigil in the air, tearing open a direct Way into the Sanctuary of Edinburgh!",
                eChatType.CT_Important,
                eChatLoc.CL_SystemWindow);

            var dest = EdinburghWaysIngress;
            if (WorldMgr.GetRegion(dest.RegionId) != null)
            {
                player.LeaveHouse();
                player.MoveTo(dest.RegionId, dest.X, dest.Y, dest.Z, dest.Heading);
            }

            return true;
        }

        /// <summary>
        /// True if the character class is an Arcane or Primal caster granted "Open Way" at Level 5.
        /// </summary>
        public static bool IsEligibleCasterClass(int classId) =>
            (eCharacterClass)classId is
                // Council Wardens (Albion 60, Midgard 61, Hibernia 58)
                eCharacterClass.MaulerAlb or eCharacterClass.MaulerMid or eCharacterClass.Vampiir or
                // Albion Arcane Casters
                eCharacterClass.Wizard or eCharacterClass.Theurgist or eCharacterClass.Sorcerer or
                eCharacterClass.Cabalist or eCharacterClass.Heretic or eCharacterClass.Necromancer or
                eCharacterClass.Elementalist or eCharacterClass.Mage or
                // Midgard Arcane & Primal Casters
                eCharacterClass.Runemaster or eCharacterClass.Spiritmaster or eCharacterClass.Bonedancer or
                eCharacterClass.Warlock or eCharacterClass.Shaman or eCharacterClass.Mystic or
                // Hibernia Arcane & Primal Casters
                eCharacterClass.Eldritch or eCharacterClass.Enchanter or eCharacterClass.Mentalist or
                eCharacterClass.Animist or eCharacterClass.Valewalker or eCharacterClass.Bainshee or
                eCharacterClass.Sluaghbinder or eCharacterClass.Druid or eCharacterClass.Warden or
                eCharacterClass.Magician;
    }

    /// <summary>
    /// Handler for "Open Way (Apertura Viae)" (SpellID 62090) and the Silver Thimble of the Veil.
    /// </summary>
    [SpellHandler(eSpellType.CouncilWardenOpenWay)]
    public class OpenWaySpellHandler : SpellHandler
    {
        public OpenWaySpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget)
        {
            if (!base.CheckBeginCast(selectedTarget))
                return false;

            if (Caster is GamePlayer player)
            {
                if (player.InCombat || GameRelic.IsPlayerCarryingRelic(player))
                {
                    MessageToCaster("You cannot focus the subtle resonances to open a Way while in combat!", eChatType.CT_SpellResisted);
                    return false;
                }

                if (player.IsMoving)
                {
                    MessageToCaster("You must stand still to align the mortal and Faerie resonances.", eChatType.CT_SpellResisted);
                    return false;
                }
            }

            return true;
        }

        public override void FinishSpellCast(GameLiving target)
        {
            Caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override void ApplyEffectOnTarget(GameLiving target)
        {
            GamePlayer player = target as GamePlayer ?? Caster as GamePlayer;
            if (player == null)
                return;

            if (player.InCombat || GameRelic.IsPlayerCarryingRelic(player))
            {
                player.Out.SendMessage(
                    LanguageMgr.GetTranslation(player.Client?.Account?.Language ?? "EN", "GamePlayer.UseSlot.CantUseInCombat"),
                    eChatType.CT_System,
                    eChatLoc.CL_SystemWindow);
                return;
            }

            OpenWayDestination destination = OpenWayResonanceTable.ResolveForPlayer(player);
            SendEffectAnimation(player, 0, false, 1);

            player.Out.SendMessage(
                destination.ArrivalMessage,
                eChatType.CT_Important,
                eChatLoc.CL_SystemWindow);

            if (WorldMgr.GetRegion(destination.RegionId) != null)
            {
                player.LeaveHouse();
                player.MoveTo(destination.RegionId, destination.X, destination.Y, destination.Z, destination.Heading);
            }
        }
    }
}
