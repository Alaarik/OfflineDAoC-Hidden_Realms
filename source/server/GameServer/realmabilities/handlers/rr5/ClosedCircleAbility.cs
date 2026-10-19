using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;

namespace DOL.GS.RealmAbilities
{
	/// <summary>
	/// Realm Rank 5 Ability for Council Warden: The Closed Circle.
	/// Combines Gravitic Collapse (Kinetic Singularity pulling enemies into the center and stumbling them for 3s)
	/// with The Closed Circle (15s static ground area sealing enemies in a magic suppression zone with 35% snare).
	/// </summary>
	public class ClosedCircleAbility : RR5RealmAbility
	{
		public const int PULL_RADIUS = 500;
		public const int STATIC_RADIUS = 500;
		public const uint STATIC_LIFETIME = 15; // 15 seconds
		public const int STUMBLE_DURATION = 3000; // 3 seconds
		public const int MAX_RANGE = 1500;

		public ClosedCircleAbility(DbAbility dba, int level) : base(dba, level) { }

		public override void Execute(GameLiving living)
		{
			if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED))
				return;

			if (living is not GamePlayer caster)
				return;

			if (caster.IsMoving)
			{
				caster.Out.SendMessage("You must be standing still to use this ability!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return;
			}

			// Determine epicenter: Ground Target > Target Object > Caster
			Point3D targetSpot;
			if (caster.GroundTarget.IsValid && caster.IsWithinRadius(caster.GroundTarget, MAX_RANGE))
			{
				targetSpot = new Point3D(caster.GroundTarget.X, caster.GroundTarget.Y, caster.GroundTarget.Z);
			}
			else if (caster.TargetObject is GameLiving targetLiving && caster.IsWithinRadius(targetLiving, MAX_RANGE))
			{
				targetSpot = new Point3D(targetLiving.X, targetLiving.Y, targetLiving.Z);
			}
			else
			{
				targetSpot = new Point3D(caster.X, caster.Y, caster.Z);
			}

			// Broadcast casting animation
			foreach (GamePlayer p in caster.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
			{
				if (p == caster)
					p.MessageToSelf($"You manifest {Name}!", eChatType.CT_Spell);
				else
					p.MessageFromArea(caster, $"{caster.Name} manifests {Name}!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);

				p.Out.SendSpellCastAnimation(caster, 7027, 20);
			}

			// =========================================================================
			// PHASE 1: Gravitic Collapse (Kinetic Singularity pull + 3s stumble)
			// =========================================================================
			List<GameLiving> enemies = new List<GameLiving>();

			foreach (GamePlayer enemyPlayer in caster.CurrentRegion.GetPlayersInRadius(targetSpot, PULL_RADIUS))
			{
				if (enemyPlayer != null && enemyPlayer.IsAlive && GameServer.ServerRules.IsAllowedToAttack(caster, enemyPlayer, true))
				{
					enemies.Add(enemyPlayer);
				}
			}

			foreach (GameNPC enemyNpc in caster.CurrentRegion.GetNPCsInRadius(targetSpot, PULL_RADIUS))
			{
				if (enemyNpc != null && enemyNpc.IsAlive && GameServer.ServerRules.IsAllowedToAttack(caster, enemyNpc, true))
				{
					enemies.Add(enemyNpc);
				}
			}

			foreach (GameLiving target in enemies)
			{
				// Pull enemy toward center with slight jitter to prevent exact stacking
				int pullX = targetSpot.X + Util.Random(-35, 35);
				int pullY = targetSpot.Y + Util.Random(-35, 35);
				int pullZ = targetSpot.Z;

				if (target is GamePlayer targetPlayer)
				{
					targetPlayer.MoveTo((ushort)targetPlayer.CurrentRegionID, pullX, pullY, pullZ, targetPlayer.Heading);
				}
				else if (target is GameNPC targetNpc)
				{
					targetNpc.MoveTo((ushort)targetNpc.CurrentRegionID, pullX, pullY, pullZ, targetNpc.Heading);
				}

				// Send visual pull impact
				caster.Out.SendSpellEffectAnimation(caster, target, 7027, 0, false, 1);

				// Apply 3s kinetic stumble
				new ClosedCircleStumbleEffect(STUMBLE_DURATION).Start(target);
			}

			// =========================================================================
			// PHASE 2: The Closed Circle (15s Magic Suppression Ground Area)
			// =========================================================================
			Statics.ClosedCircleBase circle = new Statics.ClosedCircleBase();
			circle.CreateStatic(caster, targetSpot, STATIC_LIFETIME, 1, STATIC_RADIUS);

			DisableSkill(living);
			caster.StopCurrentSpellcast();
		}

		public override int GetReUseDelay(int level) => 600; // 10 minutes

		public override void AddEffectsInfo(IList<string> list)
		{
			list.Add("The Closed Circle (Council Warden RR5 Capstone)");
			list.Add("");
			list.Add("Target: Area (Ground Target, Enemy, or Self)");
			list.Add("Range: 1500 units | Radius: 500 units");
			list.Add("Casting time: Instant | Reuse: 10 minutes");
			list.Add("");
			list.Add("Phase 1 - Gravitic Collapse:");
			list.Add("Violently pulls all enemies within 500 radius to the epicenter and stumbles them for 3 seconds.");
			list.Add("");
			list.Add("Phase 2 - The Closed Circle:");
			list.Add("Draws a shimmering 500-radius anti-magic circle for 15 seconds. Enemies within the circle are 100% silenced from spellcasting, slowed by 35%, and enemy summoned pets are pacified.");
		}
	}
}
