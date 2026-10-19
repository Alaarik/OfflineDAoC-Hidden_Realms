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
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Trainer
{
	/// <summary>
	/// Hibernia Council Warden Trainer (White Council Liaison)
	/// </summary>
	[NPCGuildScript("Council Warden Trainer", eRealm.Hibernia)]
	[NPCGuildScript("White Council Liaison", eRealm.Hibernia)]
	[NPCGuildScript("Vampiir Trainer", eRealm.Hibernia)]
	public class VampiirTrainer : GameTrainer
	{
		public override eCharacterClass TrainedClass
		{
			get { return eCharacterClass.Vampiir; }
		}

		public VampiirTrainer()
			: base()
		{
		}

		public override bool CanPromotePlayer(GamePlayer player)
		{
			if (player == null || player.Realm != eRealm.Hibernia ||
				player.Level < 5 ||
				player.CharacterClass?.ID != (int)eCharacterClass.Guardian)
				return false;

			var targetClass = ScriptMgr.FindCharacterClass((int)TrainedClass);
			return targetClass != null &&
				targetClass.EligibleRaces.Exists(r => r.ID == (eRace)player.Race) &&
				(!GlobalConstants.CLASS_GENDER_CONSTRAINTS_DICT.TryGetValue(TrainedClass, out eGender gender) ||
					gender == player.Gender);
		}

		public bool IsCapitalLiaison =>
			CurrentRegionID is 201 or 200 ||
			string.Equals(GuildName, "White Council Liaison", StringComparison.OrdinalIgnoreCase);

		public override bool CanTrain(GamePlayer player)
		{
			if (player?.CharacterClass != null &&
				player.Realm == eRealm.Hibernia &&
				player.CharacterClass.ID == (int)eCharacterClass.Guardian &&
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
		/// Interact with capital liaison
		/// </summary>
		public override bool Interact(GamePlayer player)
		{
			if (!base.Interact(player)) return false;

			if (player.CharacterClass.ID == (int) TrainedClass)
			{
				if (IsCapitalLiaison)
				{
					player.Out.SendMessage(
						$"{Name} says, \"Stand vigilant, Warden {player.Name}. As a Liaison in Tir na Nog, I do not conduct Warden training here—all instruction takes place under Drillmaster Callan Boyd in the Sanctuary of Edinburgh. Whisper [open a Way] if you need me to open a passage to Edinburgh for you.\"",
						eChatType.CT_Say, eChatLoc.CL_PopupWindow);
				}
				else
				{
					player.Out.SendMessage(this.Name + " says, \"Stand vigilant, Warden. Hone your blade, your Evocation, and your Abjuration against the enemies of the Laws.\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
				}
				DOL.GS.Quests.CouncilWardenEpicQuestRuntime.HandleTrainerInteraction(player, this);
			}
			else
			{
				if (CanPromotePlayer(player))
				{
					player.Out.SendMessage(this.Name + " says, \"The White Council watches from Edinburgh, " + player.Name + ". Do you swear to uphold the Seven Laws and take up the Grey Cloak as a [Council Warden]?\"", eChatType.CT_System, eChatLoc.CL_PopupWindow);
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
		/// Talk to capital liaison
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
				string.Equals(cmd, "Vampiir", StringComparison.OrdinalIgnoreCase))
			{
				if (CanPromotePlayer(player))
				{
					string promoMsg = IsCapitalLiaison
						? "By the authority of Edinburgh and the Senior Council, I name you a Council Warden. Because Warden training is conducted only within the Sanctuary of Edinburgh, I am opening a Way for you now—step through and report to Drillmaster Callan Boyd in the Warden Barracks!"
						: "By the authority of Edinburgh and the Senior Council, I name you a Council Warden. Wear this Grey Cloak with honor, and guard the mortal realm well.";

					PromotePlayer(player, (int)eCharacterClass.Vampiir, promoMsg, null);
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
