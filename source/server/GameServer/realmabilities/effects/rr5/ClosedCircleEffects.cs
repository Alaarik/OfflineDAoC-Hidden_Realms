using System;
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;

namespace DOL.GS.Effects
{
	/// <summary>
	/// Kinetic Collapse Stumble Effect: 3-second physical knockdown / stumble caused by the singularity pull.
	/// </summary>
	public class ClosedCircleStumbleEffect : TimedEffect
	{
		public ClosedCircleStumbleEffect() : base(3000) { }

		public ClosedCircleStumbleEffect(int duration) : base(duration) { }

		public override void Start(GameLiving target)
		{
			base.Start(target);
			target.IsStunned = true;
			target.attackComponent?.StopAttack();
			if (target.castingComponent != null && target.movementComponent != null)
				target.StopCurrentSpellcast();
			if (target.movementComponent != null)
				target.DisableTurning(true);
			if (target is GamePlayer player)
			{
				player.Out.SendUpdateMaxSpeed();
				player.Out.SendMessage("The gravitational collapse knocks you completely off balance!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
			}
		}

		public override void Stop()
		{
			base.Stop();
			if (m_owner != null)
			{
				m_owner.IsStunned = false;
				if (m_owner.movementComponent != null)
					m_owner.DisableTurning(false);
				if (m_owner is GamePlayer player)
					player.Out.SendUpdateMaxSpeed();
			}
		}

		public override string Name => "Kinetic Collapse";

		public override ushort Icon => 7027;

		public override IList<string> DelveInfo
		{
			get
			{
				var list = new List<string>
				{
					"Violently compresses the local kinetic field, dragging you to the singularity and stumbling you for 3 seconds.",
					"",
					"Stumble Duration: 3.0 sec"
				};
				return list;
			}
		}
	}

	/// <summary>
	/// Closed Circle Suppression Effect: Complete magic silence + 35% movement speed reduction + minion pacification while inside the Closed Circle.
	/// </summary>
	public class ClosedCircleSuppressionEffect : TimedEffect
	{
		private GameLiving m_caster;

		public ClosedCircleSuppressionEffect(GameLiving caster) : base(2500)
		{
			m_caster = caster;
		}

		public ClosedCircleSuppressionEffect(GameLiving caster, int duration) : base(duration)
		{
			m_caster = caster;
		}

		public override void Start(GameLiving target)
		{
			base.Start(target);
			target.SilencedTime = target.CurrentRegion.Time + m_duration;
			if (target.castingComponent != null && target.movementComponent != null)
				target.StopCurrentSpellcast();
			target.attackComponent?.StopAttack();
			if (m_caster != null && (target is GamePlayer || (target is GameNPC npcTarget && npcTarget.Brain != null)))
				target.StartInterruptTimer(m_duration, AttackData.eAttackType.Spell, m_caster);

			target.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, 1.0 - 0.35);
			target.OnMaxSpeedChange();

			if (target is GamePlayer player)
			{
				player.Out.SendMessage("You are sealed within The Closed Circle! All magic is silenced and movement is slowed by 35%!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
				if (player.ControlledBrain != null && player.ControlledBrain.Body != null)
				{
					player.ControlledBrain.Body.attackComponent?.StopAttack();
					if (player.ControlledBrain.Body.castingComponent != null && player.ControlledBrain.Body.movementComponent != null)
						player.ControlledBrain.Body.StopCurrentSpellcast();
					player.ControlledBrain.Body.SilencedTime = target.CurrentRegion.Time + m_duration;
				}
			}
			else if (target is GameNPC npc)
			{
				if (npc.Brain is IControlledBrain)
				{
					npc.attackComponent?.StopAttack();
					if (npc.castingComponent != null && npc.movementComponent != null)
						npc.StopCurrentSpellcast();
				}
			}
		}

		public void RefreshDuration(int duration)
		{
			if (m_owner != null && m_owner.IsAlive)
			{
				m_duration = duration;
				m_owner.SilencedTime = m_owner.CurrentRegion.Time + duration;
				if (m_owner.castingComponent != null && m_owner.movementComponent != null)
					m_owner.StopCurrentSpellcast();
				if (m_caster != null && (m_owner is GamePlayer || (m_owner is GameNPC npcOwner && npcOwner.Brain != null)))
					m_owner.StartInterruptTimer(duration, AttackData.eAttackType.Spell, m_caster);

				if (m_owner is GamePlayer player && player.ControlledBrain?.Body != null)
				{
					player.ControlledBrain.Body.SilencedTime = m_owner.CurrentRegion.Time + duration;
					player.ControlledBrain.Body.attackComponent?.StopAttack();
					if (player.ControlledBrain.Body.castingComponent != null && player.ControlledBrain.Body.movementComponent != null)
						player.ControlledBrain.Body.StopCurrentSpellcast();
				}
			}
		}

		public override void Stop()
		{
			if (m_owner != null)
			{
				m_owner.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, this);
				base.Stop();
				m_owner.OnMaxSpeedChange();
				if (m_owner is GamePlayer player)
				{
					player.Out.SendMessage("You have left the boundary of The Closed Circle.", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
				}
			}
			else
			{
				base.Stop();
			}
		}

		public override string Name => "The Closed Circle";

		public override ushort Icon => 4260;

		public override IList<string> DelveInfo
		{
			get
			{
				var list = new List<string>
				{
					"Sealed within an anti-magic boundary. All spellcasting is silenced, magical summons are pacified, and movement speed is reduced by 35%.",
					"",
					"Silence: 100%",
					"Speed Reduction: 35%"
				};
				return list;
			}
		}
	}
}
