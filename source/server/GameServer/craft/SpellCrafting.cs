using System;
using System.Collections;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS
{
    public class SpellCrafting : AdvancedCraftingSkill
	{
        protected override String Profession
        {
            get
            {
                return "CraftersProfession.Spellcrafter";
            }
        }

        private static readonly int[,] itemMaxBonusLevel =  // taken from mythic Spellcraft calculator
		{
			{0,1,1,1,1,1,1},
			{1,1,1,1,1,2,2},
			{1,1,1,2,2,2,2},
			{1,1,2,2,2,3,3},
			{1,2,2,2,3,3,4},
			{1,2,2,3,3,4,4},
			{2,2,3,3,4,4,5},
			{2,3,3,4,4,5,5},
			{2,3,3,4,5,5,6},
			{2,3,4,4,5,6,7},
			{2,3,4,5,6,6,7},
			{3,4,4,5,6,7,8},
			{3,4,5,6,6,7,9},
			{3,4,5,6,7,8,9},
			{3,4,5,6,7,8,10},
			{3,5,6,7,8,9,10},
			{4,5,6,7,8,10,11},
			{4,5,6,8,9,10,12},
			{4,6,7,8,9,11,12},
			{4,6,7,8,10,11,13},
			{4,6,7,9,10,12,13},
			{5,6,8,9,11,12,14},
			{5,7,8,10,11,13,15},
			{5,7,9,10,12,13,15},
			{5,7,9,10,12,14,16},
			{5,8,9,11,12,14,16},
			{6,8,10,11,13,15,17},
			{6,8,10,12,13,15,18},
			{6,8,10,12,14,16,18},
			{6,9,11,12,14,16,19},
			{6,9,11,13,15,17,20},
			{7,9,11,13,15,17,20},
			{7,10,12,14,16,18,21},
			{7,10,12,14,16,19,21},
			{7,10,12,14,17,19,22},
			{7,10,13,15,17,20,23},
			{8,11,13,15,17,20,23},
			{8,11,13,16,18,21,24},
			{8,11,14,16,18,21,24},
			{8,11,14,16,19,22,25},
			{8,12,14,17,19,22,26},
			{9,12,15,17,20,23,26},
			{9,12,15,18,20,23,27},
			{9,13,15,18,21,24,27},
			{9,13,16,18,21,24,28},
			{9,13,16,19,22,25,29},
			{10,13,16,19,22,25,29},
			{10,14,17,20,23,26,30},
			{10,14,17,20,23,27,31},
			{10,14,17,20,23,27,31},
			{10,15,18,21,24,28,32},
		};

        private static readonly int[] OCStartPercentages = { 0, 10, 20, 30, 50, 70 };
        private static readonly int[] ItemQualOCModifiers = { 0, 0, 6, 8, 10, 18, 26 };

		public SpellCrafting()
		{
			Icon = 0x0D;
			Name = LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, 
				"Crafting.Name.Evocation");
			eSkill = eCraftingSkill.SpellCrafting;
		}

		#region Classic craft functions
		public override void GainCraftingSkillPoints(GamePlayer player, Recipe recipe)
		{
			if (Util.Chance(CalculateChanceToGainPoint(player, recipe.Level)))
			{
				player.GainCraftingSkill(eCraftingSkill.SpellCrafting, 1);

				if (player.GetCraftingSkillValue(eCraftingSkill.GemCutting) < subSkillCap)
	                player.GainCraftingSkill(eCraftingSkill.GemCutting, 1);

				player.Out.SendUpdateCraftingSkills();
			}
		}

		#endregion

		#region Requirement check

		/// <summary>
		/// This function is called when player accept the combine
		/// </summary>
		/// <param name="player"></param>
		/// <param name="item"></param>
		public override bool IsAllowedToCombine(GamePlayer player, DbInventoryItem item)
		{
			if (!base.IsAllowedToCombine(player, item)) return false;

			if (((DbInventoryItem)player.TradeWindow.TradeItems[0]).Model == 525) // first ingredient to combine is Dust => echantement
			{
				if (item.Level < 15)
				{
					player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, 
						"SpellCrafting.IsAllowedToCombine.NoEnchanted"), eChatType.CT_System, 
						eChatLoc.CL_SystemWindow);

					return false;
				}

				lock (player.TradeWindow.Lock)
				{
					foreach (DbInventoryItem materialToCombine in player.TradeWindow.TradeItems)
					{
						if (materialToCombine.Model != 525)
						{
							player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, 
								"SpellCrafting.IsAllowedToCombine.FalseMaterial", 
								materialToCombine.Name), 
								eChatType.CT_System, eChatLoc.CL_SystemWindow);

							return false;
						}
					}
				}
			}
			else
			{
				ArrayList coreBonuses = new ArrayList(4);
				ArrayList toaBonuses = new ArrayList(2);
				ArrayList allBonuses = new ArrayList(6);

				// Read existing bonuses on item
				for (int slot = 1; slot <= 4; slot++)
				{
					int bType = slot switch { 1 => item.Bonus1Type, 2 => item.Bonus2Type, 3 => item.Bonus3Type, 4 => item.Bonus4Type, _ => 0 };
					if (bType != 0)
					{
						if (IsToABonus(bType)) toaBonuses.Add(bType);
						else coreBonuses.Add(bType);
						allBonuses.Add(bType);
					}
				}
				for (int slot = 5; slot <= 6; slot++)
				{
					int bType = slot == 5 ? item.Bonus5Type : item.Bonus6Type;
					if (bType != 0)
					{
						if (IsToABonus(bType)) toaBonuses.Add(bType);
						else coreBonuses.Add(bType);
						allBonuses.Add(bType);
					}
				}

				int draughtCount = 0;
				lock (player.TradeWindow.Lock)
				{
					for (int i = 0; i < player.TradeWindow.ItemsCount; i++)
					{
						DbInventoryItem currentItem = (DbInventoryItem)player.TradeWindow.TradeItems[i];

						if (currentItem.Id_nb == "reagent_stability_draught")
						{
							draughtCount++;
							if (draughtCount > 1)
							{
								player.Out.SendMessage("You may only use one Draught of Greater Stability per combine!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
								return false;
							}
							continue;
						}

						if (currentItem.Object_Type != (int)eObjectType.SpellcraftGem)
						{
							player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.FalseItem"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
							return false;
						}

						if (allBonuses.Contains(currentItem.Bonus1Type))
						{
							player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.NoSameBonus"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
							return false;
						}

						if (IsToABonus(currentItem.Bonus1Type))
						{
							if (toaBonuses.Count >= 2)
							{
								player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.DifferentTypes", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
								return false;
							}
							toaBonuses.Add(currentItem.Bonus1Type);
						}
						else
						{
							if (coreBonuses.Count >= 4)
							{
								player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.DifferentTypes", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
								return false;
							}
							coreBonuses.Add(currentItem.Bonus1Type);
						}

						allBonuses.Add(currentItem.Bonus1Type);
					}
				}

				int coreBonusLevel = GetTotalCoreImbuePoints(player, item);
				int maxCoreBonusLevel = GetItemMaxImbuePoints(item);
				int toaBonusLevel = GetTotalToAImbuePoints(player, item);
				int maxToABonusLevel = GetItemMaxToAImbuePoints(item);

				if (coreBonusLevel > player.GetCraftingSkillValue(eCraftingSkill.SpellCrafting) / 20)
				{
					player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.NotEnoughSkill"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
					return false;
				}

				if (coreBonusLevel - maxCoreBonusLevel > 6)
				{
					player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.NoMoreLevels", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
					return false;
				}

				if (toaBonusLevel - maxToABonusLevel > 4)
				{
					player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.IsAllowedToCombine.NoMoreLevels", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
					return false;
				}
			}

			return true;
		}

		public static bool IsToABonus(int bonusType)
		{
			return (bonusType >= (int)eProperty.ToABonus_First && bonusType <= (int)eProperty.ToABonus_Last)
				|| (bonusType >= (int)eProperty.StatCapBonus_First && bonusType <= (int)eProperty.StatCapBonus_Last)
				|| (bonusType >= (int)eProperty.ResCapBonus_First && bonusType <= (int)eProperty.ResCapBonus_Last)
				|| bonusType == (int)eProperty.SpellRange
				|| bonusType == (int)eProperty.MeleeSpeed
				|| bonusType == (int)eProperty.MeleeDamage;
		}

		#endregion

		#region Apply magical effect

		/// <summary>
		/// Apply all needed magical bonus to the item
		/// </summary>
		/// <param name="player"></param>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override void ApplyMagicalEffect(GamePlayer player, DbInventoryItem item)
		{
			if (item == null || player.TradeWindow.TradeItems[0] == null) return; // be sure at least one item in each side

			if (((DbInventoryItem)player.TradeWindow.TradeItems[0]).Model == 525) // Echant item
			{
				ApplyMagicalDusts(player, item);
			}
			else if (((DbInventoryItem)player.TradeWindow.TradeItems[0]).Object_Type == (int)eObjectType.SpellcraftGem) // Spellcraft item
			{
				ApplySpellcraftGems(player, item);
			}

			if (item.Template is DbItemUnique)
			{
				GameInventoryObjectExtensions.SaveItem(item);
				GameServer.Database.SaveObject(item.Template as DbItemUnique);
			}
			else
			{
				ChatUtil.SendErrorMessage(player, "Spellcrafting error: Item was not an ItemUnique, crafting changes not saved to DB!");
				log.ErrorFormat("Spellcrafting error: Item {0} was not an ItemUnique for player {1}, crafting changes not saved to DB!", item.Id_nb, player.Name);
			}
		}

		/// <summary>
		/// Apply all magical dust
		/// </summary>
		/// <param name="player"></param>
		/// <param name="item"></param>
		/// <returns></returns>
		private void ApplyMagicalDusts(GamePlayer player, DbInventoryItem item)
		{
			int spellCrafterLevel = player.GetCraftingSkillValue(eCraftingSkill.SpellCrafting);

			int bonusCap;
			if (spellCrafterLevel < 300 || item.Level < 15) bonusCap = 0;
			else if (spellCrafterLevel < 400 || item.Level < 20) bonusCap = 5;
			else if (spellCrafterLevel < 500 || item.Level < 25) bonusCap = 10;
			else if (spellCrafterLevel < 600 || item.Level < 30) bonusCap = 15;
			else if (spellCrafterLevel < 700 || item.Level < 35) bonusCap = 20;
			else if (spellCrafterLevel < 800 || item.Level < 40) bonusCap = 25;
			else if (spellCrafterLevel < 900 || item.Level < 45) bonusCap = 30;
			else bonusCap = 35;

			player.Inventory.BeginChanges();

			int bonusMod = item.Bonus;
			lock (player.TradeWindow.Lock)
			{
				foreach (DbInventoryItem gemme in player.TradeWindow.TradeItems)
				{
					switch (gemme.Name)
					{
						case "coppery imbued dust": bonusMod += 2; break;
						case "coppery enchanted dust": bonusMod += 4; break;
						case "coppery glowing dust": bonusMod += 6; break;
						case "coppery ensorcelled dust": bonusMod += 8; break;

						case "silvery imbued dust": bonusMod += 10; break;
						case "silvery enchanted dust": bonusMod += 12; break;
						case "silvery glowing dust": bonusMod += 14; break;
						case "silvery ensorcelled dust": bonusMod += 16; break;

						case "golden imbued dust": bonusMod += 18; break;
						case "golden enchanted dust": bonusMod += 20; break;
						case "golden glowing dust": bonusMod += 22; break;
						case "golden ensorcelled dust": bonusMod += 24; break;

						case "mithril imbued dust": bonusMod += 26; break;
						case "mithril enchanted dust": bonusMod += 28; break;
						case "mithril glowing dust": bonusMod += 30; break;
						case "mithril ensorcelled dust": bonusMod += 32; break;

						case "platinum imbued dust": bonusMod += 34; break;
						case "platinum enchanted dust": bonusMod += 36; break;
						case "platinum glowing dust": bonusMod += 38; break;
						case "platinum ensorcelled dust": bonusMod += 40; break;

					}
					player.Inventory.RemoveCountFromStack(gemme, 1);
					InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gemme.Template);
				}
			}

			item.Bonus = Math.Min(bonusMod, bonusCap);

			player.Inventory.CommitChanges();
		}

		/// <summary>
		/// Apply all spellcraft gems bonus
		/// </summary>
		/// <param name="player"></param>
		/// <param name="item"></param>
		/// <returns></returns>
		private void ApplySpellcraftGems(GamePlayer player, DbInventoryItem item)
		{
			int maxCoreBonusLevel = GetItemMaxImbuePoints(item);
			int coreBonusLevel = GetTotalCoreImbuePoints(player, item);
			int maxToABonusLevel = GetItemMaxToAImbuePoints(item);
			int toaBonusLevel = GetTotalToAImbuePoints(player, item);

			int coreDiff = Math.Max(0, coreBonusLevel - maxCoreBonusLevel);
			int toaDiff = Math.Max(0, toaBonusLevel - maxToABonusLevel);
			int totalDiff = coreDiff + toaDiff;

			int sucessChances = 100;
			if (totalDiff > 0)
				sucessChances = CalculateChanceToOverchargeItem(player, item, maxCoreBonusLevel, coreBonusLevel, maxToABonusLevel, toaBonusLevel);
			int destroyChance = 100 - CalculateChanceToPreserveItem(player, item, maxCoreBonusLevel, coreBonusLevel, maxToABonusLevel, toaBonusLevel);

			GamePlayer tradePartner = player.TradeWindow.Partner;
			bool inSanctum = LeyLineSanctum.IsInSanctum(player, out _);
			if (inSanctum)
			{
				string hum = "[Ley-Line Confluence] You feel the raw hum of the earth vibrating through the ancient stones.";
				player.Out.SendMessage(hum, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				tradePartner?.Out.SendMessage(hum, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
			}

			if (Util.Chance(sucessChances))
			{
				lock (player.TradeWindow.Lock)
				{
					player.Inventory.BeginChanges();
					foreach (DbInventoryItem gem in (ArrayList)player.TradeWindow.TradeItems.Clone())
					{
						if (gem.Id_nb == "reagent_stability_draught")
						{
							if (totalDiff <= 2)
							{
								player.Out.SendMessage("The magical weave was stable; your Draught of Greater Stability was preserved.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
								continue;
							}
							else
							{
								player.Out.SendMessage("The Draught of Greater Stability vaporizes into a protective ward, absorbing the magical backlash.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
								player.Inventory.RemoveCountFromStack(gem, 1);
								InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gem.Template);
								continue;
							}
						}

						if (IsToABonus(gem.Bonus1Type))
						{
							if (item.Bonus5Type == 0)
							{
								item.Bonus5Type = gem.Bonus1Type;
								item.Bonus5 = gem.Bonus1;
							}
							else if (item.Bonus6Type == 0)
							{
								item.Bonus6Type = gem.Bonus1Type;
								item.Bonus6 = gem.Bonus1;
							}
						}
						else
						{
							if (item.Bonus1Type == 0)
							{
								item.Bonus1Type = gem.Bonus1Type;
								item.Bonus1 = gem.Bonus1;
							}
							else if (item.Bonus2Type == 0)
							{
								item.Bonus2Type = gem.Bonus1Type;
								item.Bonus2 = gem.Bonus1;
							}
							else if (item.Bonus3Type == 0)
							{
								item.Bonus3Type = gem.Bonus1Type;
								item.Bonus3 = gem.Bonus1;
							}
							else if (item.Bonus4Type == 0)
							{
								item.Bonus4Type = gem.Bonus1Type;
								item.Bonus4 = gem.Bonus1;
							}
						}
						player.Inventory.RemoveCountFromStack(gem, 1);
						InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gem.Template);
					}
					player.Inventory.CommitChanges();
				}

				if (tradePartner != null)
				{
					tradePartner.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.ImbuedItem", player.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				}
				player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.ImbuedItem", player.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
			}
			else if (Util.Chance(destroyChance))
			{
				player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.PowerExplodes"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

				lock (player.TradeWindow.Lock)
				{
					player.Inventory.BeginChanges();
					foreach (DbInventoryItem gem in (ArrayList)player.TradeWindow.TradeItems.Clone())
					{
						if (gem.Id_nb == "reagent_stability_draught")
						{
							player.Inventory.RemoveCountFromStack(gem, 1);
							InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gem.Template);
							continue;
						}
						
						if (gem.Object_Type == (int)eObjectType.SpellcraftGem)
						{
							player.Inventory.RemoveCountFromStack(gem, 1);
							InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gem.Template);
						}
					}
					player.Inventory.CommitChanges();
				}

				player.Emote(eEmote.SpellGoBoom);
				player.Health = 0;
				player.Die(player); // On official you take damages
				player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.Failed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

				if (tradePartner != null)
				{
					if (Util.Chance(40))
					{
						tradePartner.Emote(eEmote.SpellGoBoom);
						tradePartner.Health = 0;
						tradePartner.Die(player);
					}
					tradePartner.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.Failed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				}
			}
			else
			{
				lock (player.TradeWindow.Lock)
				{
					player.Inventory.BeginChanges();
					foreach (DbInventoryItem gem in (ArrayList)player.TradeWindow.TradeItems.Clone())
					{
						if (gem.Id_nb == "reagent_stability_draught")
						{
							if (totalDiff <= 2)
							{
								player.Out.SendMessage("The magical weave was stable; your Draught of Greater Stability was preserved.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
								continue;
							}
							else
							{
								player.Inventory.RemoveCountFromStack(gem, 1);
								InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gem.Template);
								continue;
							}
						}

						if (gem.Object_Type == (int)eObjectType.SpellcraftGem)
						{
							player.Inventory.RemoveCountFromStack(gem, 1);
							InventoryLogging.LogInventoryAction(player, "(craft)", eInventoryActionType.Craft, gem.Template);
						}
					}
					player.Inventory.CommitChanges();
				}

				player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.Failed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				player.Out.SendMessage("The magical weave destabilizes and dissipates harmlessly into the ether; the item is spared.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				if (tradePartner != null)
				{
					tradePartner.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ApplySpellcraftGems.Failed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
					tradePartner.Out.SendMessage("The magical weave destabilizes and dissipates harmlessly into the ether; the item is spared.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				}
			}
		}

		#endregion

		# region Show informations to player

		public virtual void ShowSpellCraftingInfo(GamePlayer player, DbInventoryItem item)
		{
			// 525 == dust.
			if ((player.TradeWindow.TradeItems[0] as DbInventoryItem).Model == 525)
				return;

			ArrayList spellCraftingInfo = new(16);
			int maxCoreCharges = GetItemMaxImbuePoints(item);
			int totalCoreCharges = GetTotalCoreImbuePoints(player, item);
			int maxToACharges = GetItemMaxToAImbuePoints(item);
			int totalToACharges = GetTotalToAImbuePoints(player, item);

			spellCraftingInfo.Add($"*** Spellcrafting: {item.Name} ***");
			spellCraftingInfo.Add($"* Core Imbue Capacity: {totalCoreCharges}/{maxCoreCharges} pts (Slots 1-4)");
			spellCraftingInfo.Add($"* ToA Imbue Capacity: {totalToACharges}/{maxToACharges} pts (Slots 5-6)");
			spellCraftingInfo.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ShowSpellCraftingInfo.CurrentBonus", item.Name));

			for (int slot = 1; slot <= 6; slot++)
			{
				int bType = slot switch {
					1 => item.Bonus1Type,
					2 => item.Bonus2Type,
					3 => item.Bonus3Type,
					4 => item.Bonus4Type,
					5 => item.Bonus5Type,
					6 => item.Bonus6Type,
					_ => 0
				};
				int bVal = slot switch {
					1 => item.Bonus1,
					2 => item.Bonus2,
					3 => item.Bonus3,
					4 => item.Bonus4,
					5 => item.Bonus5,
					6 => item.Bonus6,
					_ => 0
				};
				if (bType != 0)
				{
					string suffix = GetBonusSuffix(bType);
					string slotLabel = slot <= 4 ? $"Slot {slot} (Core)" : $"Slot {slot} (ToA)";
					spellCraftingInfo.Add($"* [{slotLabel}] {SkillBase.GetPropertyName((eProperty)bType)}: +{bVal}{suffix}");
				}
			}

			spellCraftingInfo.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ShowSpellCraftingInfo.GemBonuses"));

			lock (player.TradeWindow.Lock)
			{
				for (int i = 0; i < player.TradeWindow.ItemsCount; i++)
				{
					DbInventoryItem currentGem = player.TradeWindow.TradeItems[i] as DbInventoryItem;
					string suffix = GetBonusSuffix(currentGem.Bonus1Type);
					string cat = IsToABonus(currentGem.Bonus1Type) ? "ToA" : "Core";
					spellCraftingInfo.Add($"* [{cat}] {currentGem.Name} - {SkillBase.GetPropertyName((eProperty)currentGem.Bonus1Type)}: ({GetGemImbuePoints(currentGem.Bonus1Type, currentGem.Bonus1)} pts) +{currentGem.Bonus1}{suffix}");
				}
			}

			bool isOvercharged = (totalCoreCharges > maxCoreCharges) || (totalToACharges > maxToACharges);
			if (isOvercharged)
			{
				int coreDiff = Math.Max(0, totalCoreCharges - maxCoreCharges);
				int toaDiff = Math.Max(0, totalToACharges - maxToACharges);
				int totalDiff = coreDiff + toaDiff;
				spellCraftingInfo.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ShowSpellCraftingInfo.Penalty", GetOverchargePenality(maxCoreCharges, maxCoreCharges + totalDiff)));
				spellCraftingInfo.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ShowSpellCraftingInfo.Success", CalculateChanceToOverchargeItem(player, item, maxCoreCharges, totalCoreCharges, maxToACharges, totalToACharges)));
			}

			GamePlayer partner = player.TradeWindow.Partner;

			foreach (string line in spellCraftingInfo)
			{
				player.Out.SendMessage(line, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				partner?.Out.SendMessage(line, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
			}

			if (LeyLineSanctum.IsInSanctum(player, out _))
			{
				string hum = "[Ley-Line Confluence] You feel the raw hum of the earth vibrating through the ancient stones.";
				player.Out.SendMessage(hum, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				partner?.Out.SendMessage(hum, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
			}

			if (isOvercharged)
				player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellCrafting.ShowSpellCraftingInfos.Modified"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
		}

		private static string GetBonusSuffix(int bonusType)
		{
			if (bonusType >= (int)eProperty.Resist_First && bonusType <= (int)eProperty.Resist_Last)
				return "%";
			if (bonusType >= (int)eProperty.ResCapBonus_First && bonusType <= (int)eProperty.ResCapBonus_Last)
				return "% cap";
			if (bonusType >= (int)eProperty.StatCapBonus_First && bonusType <= (int)eProperty.StatCapBonus_Last)
				return " cap";
			if (bonusType == (int)eProperty.SpellRange ||
			    bonusType == (int)eProperty.CastingSpeed ||
			    bonusType == (int)eProperty.SpellDamage ||
			    bonusType == (int)eProperty.SpellDuration ||
			    bonusType == (int)eProperty.MeleeDamage ||
			    bonusType == (int)eProperty.MeleeSpeed ||
			    bonusType == (int)eProperty.StyleDamage ||
			    bonusType == (int)eProperty.HealingEffectiveness ||
			    bonusType == (int)eProperty.BuffEffectiveness ||
			    bonusType == (int)eProperty.PowerPool ||
			    (bonusType >= (int)eProperty.ToABonus_First && bonusType <= (int)eProperty.ToABonus_Last))
				return "%";

			return " pts";
		}

		# endregion

		#region Calcul functions

		/// <summary>
        /// Get the sucess chance to overcharge the item
		/// </summary>
		/// <param name="player"></param>
		/// <param name="item"></param>
		/// <param name="maxBonusLevel"></param>
		/// <param name="bonusLevel"></param>
		/// <returns></returns>
		protected int CalculateChanceToOverchargeItem(GamePlayer player, DbInventoryItem item, int maxBonusLevel, int bonusLevel)
		{
            // Luhz Crafting Update:
            // Overcharge success rate is now dependent only on item quality, points overcharged, and spellcrafter skill.
            // This formula is taken from Kort's Spellcrafting Calculator, which appears to be correct (for 1.87). Much Thanks!
            if(bonusLevel - maxBonusLevel > 5) return 0;
            if(bonusLevel - maxBonusLevel < 0) return 100;

            try {
            int success = 34 + ItemQualOCModifiers[item.Quality - 94];
            success -= OCStartPercentages[bonusLevel-maxBonusLevel];
            int skillbonus = (player != null ? player.GetCraftingSkillValue(eCraftingSkill.SpellCrafting) : 1000) / 10;
            if (skillbonus > 100) 
                skillbonus = 100;
            success += skillbonus;
            int fudgefactor = (int)(100.0 * ((skillbonus / 100.0 - 1.0) * (OCStartPercentages[bonusLevel-maxBonusLevel] / 200.0)));
            success += fudgefactor;
            if (success > 100)
                success = 100;
            return success;
            }
            catch(Exception)
            {
                // Array access exception: someone is trying to imbue 100 < item.Quality < 94
                // Probably a GM or someone testing; I suppose we should just let them.
                return 100;
            }
		}

		public int CalculateChanceToOverchargeItem(GamePlayer player, DbInventoryItem item, int maxCoreBonus, int coreBonus, int maxToABonus, int toaBonus, bool forceSanctum = false)
		{
			int coreDiff = Math.Max(0, coreBonus - maxCoreBonus);
			int toaDiff = Math.Max(0, toaBonus - maxToABonus);
			int totalDiff = coreDiff + toaDiff;

			if (totalDiff <= 0) return 100;
			if (totalDiff > 5) return 0;

			int success = CalculateChanceToOverchargeItem(player, item, maxCoreBonus, maxCoreBonus + totalDiff);
			bool inSanctum = forceSanctum || (player != null && LeyLineSanctum.IsInSanctum(player, out _));
			if (inSanctum)
			{
				success += 15; // Ley-Line Sanctum convergence: +15% overcharge success rate
				if (success > 100) success = 100;
			}

			return success;
		}

		public int CalculateChanceToPreserveItem(GamePlayer player, DbInventoryItem item, int maxCoreBonus, int coreBonus, int maxToABonus, int toaBonus, bool forceDraught = false, bool forceSanctum = false)
		{
			int coreDiff = Math.Max(0, coreBonus - maxCoreBonus);
			int toaDiff = Math.Max(0, toaBonus - maxToABonus);
			int totalDiff = coreDiff + toaDiff;

			if (totalDiff <= 2)
				return 100; // Option B: Grace Period (0% destroy chance on overcharge <= 2)

			return CalculateChanceToPreserveItem(player, item, maxCoreBonus, maxCoreBonus + totalDiff, forceDraught, forceSanctum);
		}

		/// <summary>
		/// Get the maximum bonus level the item can hold (Core slots)
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public int GetItemMaxImbuePoints(DbInventoryItem item)
		{
			if (item.Level < 1) return 0;
            
            if (item.Quality == 100)
	            return (int)Math.Round(0.625 * item.Level);
            if (item.Quality == 99)
	            return (int)Math.Round(0.55 * item.Level);
            if (item.Quality == 98)
	            return (int)Math.Round(0.475 * item.Level);
            if (item.Quality == 97)
	            return (int)Math.Round(0.42 * item.Level);
            if (item.Quality == 96)
	            return (int)Math.Round(0.353 * item.Level);
            if (item.Quality == 95)
	            return (int)Math.Round(0.295 * item.Level);
            
            return (int)Math.Round(0.2 * item.Level);
		}

		/// <summary>
		/// Get the maximum ToA bonus level the item can hold (ToA slots 5 & 6)
		/// Baseline: 16 safe points at Level 51 100% Masterpiece
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public int GetItemMaxToAImbuePoints(DbInventoryItem item)
		{
			if (item.Level < 1) return 0;

			if (item.Quality >= 100)
				return (int)Math.Round(0.313 * item.Level);
			if (item.Quality == 99)
				return (int)Math.Round(0.275 * item.Level);
			if (item.Quality == 98)
				return (int)Math.Round(0.235 * item.Level);
			if (item.Quality == 97)
				return (int)Math.Round(0.200 * item.Level);
			if (item.Quality == 96)
				return (int)Math.Round(0.160 * item.Level);
			if (item.Quality == 95)
				return (int)Math.Round(0.120 * item.Level);

			return (int)Math.Round(0.100 * item.Level);
		}

		/// <summary>
		/// Get gems bonus level (Core)
		/// </summary>
		/// <param name="player">player spellcrafting</param>
		/// <param name="item">item to calculate imbues for</param>
		/// <returns></returns>
		public int GetTotalImbuePoints(GamePlayer player, DbInventoryItem item)
		{
			return GetTotalCoreImbuePoints(player, item);
		}

		public int GetTotalCoreImbuePoints(GamePlayer player, DbInventoryItem item)
		{
			int totalGemBonus = 0;
			int biggerGemCharge = 0;

			for (int slot = 1; slot <= 4; slot++)
			{
				int bType = slot switch { 1 => item.Bonus1Type, 2 => item.Bonus2Type, 3 => item.Bonus3Type, 4 => item.Bonus4Type, _ => 0 };
				int bVal = slot switch { 1 => item.Bonus1, 2 => item.Bonus2, 3 => item.Bonus3, 4 => item.Bonus4, _ => 0 };

				if (bType != 0 && !IsToABonus(bType))
				{
					int currentBonusImbuePoint = GetGemImbuePoints(bType, bVal);
					totalGemBonus += currentBonusImbuePoint;
					if (currentBonusImbuePoint > biggerGemCharge) biggerGemCharge = currentBonusImbuePoint;
				}
			}

			lock (player.TradeWindow.Lock)
			{
				for (int i = 0; i < player.TradeWindow.TradeItems.Count; i++)
				{
					DbInventoryItem currentGem = (DbInventoryItem)player.TradeWindow.TradeItems[i];
					if (currentGem.Object_Type == (int)eObjectType.SpellcraftGem && !IsToABonus(currentGem.Bonus1Type))
					{
						int currentGemCharge = GetGemImbuePoints(currentGem.Bonus1Type, currentGem.Bonus1);
						if (currentGemCharge > biggerGemCharge) biggerGemCharge = currentGemCharge;

						totalGemBonus += currentGemCharge;
					}
				}
			}
			totalGemBonus += biggerGemCharge;
			totalGemBonus /= 2;

			return (int)Math.Floor((double)totalGemBonus);
		}

		public int GetTotalToAImbuePoints(GamePlayer player, DbInventoryItem item)
		{
			int totalToAGemPoints = 0;

			for (int slot = 5; slot <= 6; slot++)
			{
				int bType = slot == 5 ? item.Bonus5Type : item.Bonus6Type;
				int bVal = slot == 5 ? item.Bonus5 : item.Bonus6;

				if (bType != 0 && IsToABonus(bType))
				{
					totalToAGemPoints += GetGemImbuePoints(bType, bVal);
				}
			}

			lock (player.TradeWindow.Lock)
			{
				for (int i = 0; i < player.TradeWindow.TradeItems.Count; i++)
				{
					DbInventoryItem currentGem = (DbInventoryItem)player.TradeWindow.TradeItems[i];
					if (currentGem.Object_Type == (int)eObjectType.SpellcraftGem && IsToABonus(currentGem.Bonus1Type))
					{
						totalToAGemPoints += GetGemImbuePoints(currentGem.Bonus1Type, currentGem.Bonus1);
					}
				}
			}

			return totalToAGemPoints;
		}

		/// <summary>
		/// Get how much the gem uses in imbue points
		/// </summary>
		/// <param name="bonusType"></param>
		/// <param name="bonusValue"></param>
		/// <returns></returns>
		public int GetGemImbuePoints(int bonusType, int bonusValue)
		{
			int gemBonus;
			if (bonusType >= (int)eProperty.Stat_First && bonusType <= (int)eProperty.Stat_Last) 
				gemBonus = (int)(((bonusValue - 1) * 2 / 3) + 1); // stat
			else if (bonusType == (int)eProperty.MaxMana) 
				gemBonus = (int)((bonusValue * 2) - 2); // mana
			else if (bonusType == (int)eProperty.MaxHealth) 
				gemBonus = (int)(bonusValue / 4); // HP
			else if (bonusType >= (int)eProperty.Resist_First && bonusType <= (int)eProperty.Resist_Last) 
				gemBonus = (int)((bonusValue * 2) - 2); // resist
			else if (bonusType >= (int)eProperty.Skill_First && bonusType <= (int)eProperty.Skill_Last) 
				gemBonus = (int)((bonusValue - 1) * 5); // skill
			else if (bonusType >= (int)eProperty.StatCapBonus_First && bonusType <= (int)eProperty.StatCapBonus_Last)
				gemBonus = bonusType == (int)eProperty.MaxHealthCapBonus ? Math.Max(1, bonusValue / 5) : bonusValue * 2; // Stat cap (+1..+5 -> 2..10), HP cap (+10..+50 -> 2..10)
			else if (bonusType >= (int)eProperty.ResCapBonus_First && bonusType <= (int)eProperty.ResCapBonus_Last)
				gemBonus = bonusValue * 2; // Resist cap (+1%..+5% -> 2..10)
			else if (bonusType == (int)eProperty.SpellRange ||
			         bonusType == (int)eProperty.CastingSpeed ||
			         bonusType == (int)eProperty.SpellDamage ||
			         bonusType == (int)eProperty.SpellDuration ||
			         bonusType == (int)eProperty.MeleeDamage ||
			         bonusType == (int)eProperty.MeleeSpeed ||
			         bonusType == (int)eProperty.StyleDamage ||
			         bonusType == (int)eProperty.HealingEffectiveness ||
			         bonusType == (int)eProperty.BuffEffectiveness ||
			         bonusType == (int)eProperty.PowerPool ||
			         (bonusType >= (int)eProperty.ToABonus_First && bonusType <= (int)eProperty.ToABonus_Last))
				gemBonus = bonusValue; // Percentage bonuses (+2%..+10% -> 2..10)
			else 
				gemBonus = 1; // focus or other

			if (gemBonus < 1) gemBonus = 1;

			return gemBonus;
		}

		/// <summary>
		/// Get the % overcharge penality
		/// </summary>
		/// <param name="maxBonusLevel"></param>
		/// <param name="bonusLevel"></param>
		/// <returns></returns>
		protected int GetOverchargePenality(int maxBonusLevel, int bonusLevel)
		{
			int diff = bonusLevel - maxBonusLevel;
			if (diff == 1) return -10;
			else if (diff == 2) return -20;
			else if (diff == 3) return -30;
			else if (diff == 4) return -50;
			else return -70;
		}

		/// <summary>
        /// Get the chance to preserve item while overcharging
		/// </summary>
		/// <param name="player"></param>
		/// <param name="item"></param>
		/// <param name="maxBonusLevel"></param>
		/// <param name="bonusLevel"></param>
		/// <returns></returns>
		public int CalculateChanceToPreserveItem(GamePlayer player, DbInventoryItem item, int maxBonusLevel, int bonusLevel, bool forceDraught = false, bool forceSanctum = false)
		{
			int baseChance = 0;

			double gemModifier = 0;
			bool hasDraught = forceDraught;
			if (player?.TradeWindow != null)
			{
				lock (player.TradeWindow.Lock)
				{
					foreach (DbInventoryItem gem in player.TradeWindow.TradeItems)
					{
						if (gem.Id_nb == "reagent_stability_draught")
						{
							hasDraught = true;
						}
						else
						{
							gemModifier += (gem.Quality - 92) * 1.8;
						}
					}
				}
			}

			gemModifier = gemModifier * item.Quality / 100;

			int intSkill = player != null ? player.GetCraftingSkillValue(eCraftingSkill.SpellCrafting) : 1000;
			int intSkillMod;
			if (intSkill < 50) { intSkillMod = -50; }
			else if (intSkill < 100) { intSkillMod = -45; }
			else if (intSkill < 150) { intSkillMod = -40; }
			else if (intSkill < 200) { intSkillMod = -35; }
			else if (intSkill < 250) { intSkillMod = -30; }
			else if (intSkill < 300) { intSkillMod = -25; }
			else if (intSkill < 350) { intSkillMod = -20; }
			else if (intSkill < 400) { intSkillMod = -15; }
			else if (intSkill < 450) { intSkillMod = -10; }
			else if (intSkill < 500) { intSkillMod = -5; }
			else if (intSkill < 550) { intSkillMod = 0; }
			else if (intSkill < 600) { intSkillMod = 5; }
			else if (intSkill < 650) { intSkillMod = 10; }
			else if (intSkill < 700) { intSkillMod = 15; }
			else if (intSkill < 750) { intSkillMod = 20; }
			else if (intSkill < 800) { intSkillMod = 25; }
			else if (intSkill < 850) { intSkillMod = 30; }
			else if (intSkill < 900) { intSkillMod = 35; }
			else if (intSkill < 950) { intSkillMod = 40; }
			else if (intSkill < 1000) { intSkillMod = 45; }
			else { intSkillMod = 50; }

			int stabilizationBonus = 0;
			bool inSanctum = forceSanctum || (player != null && LeyLineSanctum.IsInSanctum(player, out _));

			// Stacking: Draught (+35%) and Ley-Line Sanctum (+35%) stack up to +70%
			if (inSanctum)
			{
				stabilizationBonus += 35;
			}
			if (hasDraught)
			{
				stabilizationBonus += 35;
			}

			int finalChances = (int)(intSkillMod + gemModifier + baseChance + stabilizationBonus);
			if (finalChances > 100) finalChances = 100;
			else if (finalChances < 0) finalChances = 0;

			return finalChances;
		}

		#endregion

	}
}
