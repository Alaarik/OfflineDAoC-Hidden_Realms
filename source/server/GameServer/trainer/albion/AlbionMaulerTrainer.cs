/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Database;

namespace DOL.GS.Trainer
{
	/// <summary>
	/// Albion Council Warden Trainer (White Council Liaison)
	/// </summary>
	[NPCGuildScript("Council Warden Trainer", eRealm.Albion)]
	[NPCGuildScript("White Council Liaison", eRealm.Albion)]
	[NPCGuildScript("Mauler Trainer", eRealm.Albion)]
	public class AlbionMaulerTrainer : GameTrainer
	{
		public override eCharacterClass TrainedClass
		{
			get { return eCharacterClass.MaulerAlb; }
		}

		public override bool CanPromotePlayer(GamePlayer player)
		{
			if (player == null || player.Realm != eRealm.Albion ||
				player.Level < 5 ||
				player.CharacterClass?.ID != (int)eCharacterClass.Fighter)
				return false;

			var targetClass = ScriptMgr.FindCharacterClass((int)TrainedClass);
			return targetClass != null &&
				targetClass.EligibleRaces.Exists(r => r.ID == (eRace)player.Race) &&
				(!GlobalConstants.CLASS_GENDER_CONSTRAINTS_DICT.TryGetValue(TrainedClass, out eGender gender) ||
					gender == player.Gender);
		}

		public bool IsCapitalLiaison =>
			CurrentRegionID == 10 ||
			string.Equals(GuildName, "White Council Liaison", StringComparison.OrdinalIgnoreCase);

		public override bool CanTrain(GamePlayer player)
		{
			if (player?.CharacterClass != null &&
				player.Realm == eRealm.Albion &&
				player.CharacterClass.ID == (int)eCharacterClass.Fighter &&
				player.Level < 5)
			{
				return true;
			}

			if (IsCapitalLiaison)
				return false;

			return base.CanTrain(player);
		}

		public override eQuestIndicator GetQuestIndicator(GamePlayer player)
		{
			eQuestIndicator indicator = base.GetQuestIndicator(player);
			if (indicator != eQuestIndicator.None)
				return indicator;

			return DOL.GS.Quests.CouncilWardenEpicQuestRuntime.GetTrainerQuestIndicator(player, this);
		}

		/// <summary>
		/// Interact with trainer or capital liaison
		/// </summary>
		public override bool Interact(GamePlayer player)
		{
			if (!base.Interact(player)) return false;

			if (player.CharacterClass.ID == (int)TrainedClass)
			{
				if (IsCapitalLiaison)
				{
					player.Out.SendMessage(
						$"{Name} says, \"Keep your voice low, Warden {player.Name}. With the Crown's inquisition watching Camelot, we do not train openly in the capital. Report to Drillmaster Callan Boyd in the Sanctuary of Edinburgh—or seek out Warden Vance at our hidden outpost in Campacorentin Forest. Whisper [open a Way] if you need me to open a passage to Edinburgh for you.\"",
						eChatType.CT_Say, eChatLoc.CL_PopupWindow);
				}
				else
				{
					player.Out.SendMessage(
						$"{Name} says, \"Welcome to our hidden outpost in Campacorentin Forest, Warden. Far from Camelot's prying eyes, we can hone your blade, your Evocation, and your Abjuration—or whisper [open a Way] if you need passage to Edinburgh.\"",
						eChatType.CT_Say, eChatLoc.CL_ChatWindow);
				}
				DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerInteraction(player, this);
			}
			else
			{
				if (CanPromotePlayer(player))
				{
					player.Out.SendMessage(this.Name + " says, \"The White Council watches from Edinburgh, " + player.Name + ". Do you swear to uphold the Seven Laws and take up the Grey Cloak as a [Council Warden]?\"", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
					if (!player.IsLevelRespecUsed)
					{
						OfferRespecialize(player);
					}
				}
				else
				{
					CheckChampionTraining(player);
				}
			}
			return true;
		}

		/// <summary>
		/// Talk to trainer or capital liaison
		/// </summary>
		public override bool WhisperReceive(GameLiving source, string text)
		{
			if (!base.WhisperReceive(source, text)) return false;
			GamePlayer player = source as GamePlayer;
			if (player == null) return false;

			if (DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerWhisper(player, this, text))
				return true;

			string cmd = text.Trim();
			if (cmd.Equals("open a Way", StringComparison.OrdinalIgnoreCase) ||
				cmd.Equals("open way", StringComparison.OrdinalIgnoreCase) ||
				cmd.Equals("edinburgh", StringComparison.OrdinalIgnoreCase) ||
				cmd.Equals("way", StringComparison.OrdinalIgnoreCase))
			{
				if (DOL.GS.Spells.CouncilWardenMechanics.IsCouncilWarden(player) ||
					DOL.GS.Spells.OpenWayResonanceTable.IsEligibleCasterClass(player.CharacterClass.ID))
				{
					DOL.GS.Spells.OpenWayResonanceTable.OpenDirectWayToEdinburgh(player, this);
					return true;
				}
			}

			if (string.Equals(cmd, "Council Warden", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(cmd, "join the White Council", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(cmd, "join the Temple of the Iron Fist", StringComparison.OrdinalIgnoreCase))
			{
				if (CanPromotePlayer(player))
				{
					string promoMsg = IsCapitalLiaison
						? "By the authority of Edinburgh and the Senior Council, I name you a Council Warden. Because we do not train openly in Camelot, I am opening a Way for you now—step into the Sanctuary of Edinburgh and report to Drillmaster Callan Boyd in the Warden Barracks!"
						: "By the authority of Edinburgh and the Senior Council, I name you a Council Warden. Wear this Grey Cloak with honor, and guard the mortal realm well.";

					PromotePlayer(player, (int)eCharacterClass.MaulerAlb, promoMsg, null);
					player.VerifySpecPoints();
					player.Out.SendUpdatePoints();
					DOL.GS.Quests.CouncilWardenEpicArmor.GivePiece(player, DOL.GS.Quests.CouncilWardenEpicArmor.InitiateGreyCloak, this);

					if (IsCapitalLiaison)
					{
						DOL.GS.Spells.OpenWayResonanceTable.OpenDirectWayToEdinburgh(player, this);
					}
				}
			}
			return true;
		}
	}
}
