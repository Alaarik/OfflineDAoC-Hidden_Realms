using System;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;

namespace DOL.GS.RealmAbilities.Statics
{
	/// <summary>
	/// Ground static entity for The Closed Circle.
	/// Pulses every second over a 500 radius for 15 seconds, maintaining magic suppression on all enemies within its perimeter.
	/// </summary>
	public class ClosedCircleBase : GenericBase
	{
		protected override string GetStaticName() => "The Closed Circle";

		// 3738 is the dark cosmic / maelstrom circle model in the DAoC engine
		protected override ushort GetStaticModel() => 3738;

		protected override ushort GetStaticEffect() => 5772;

		public ClosedCircleBase() : base() { }

		protected override void CastSpell(GameLiving target)
		{
			if (target == null || !target.IsAlive || m_caster == null)
				return;

			if (!GameServer.ServerRules.IsAllowedToAttack(m_caster, target, true))
				return;

			// Check if target already has ClosedCircleSuppressionEffect
			ClosedCircleSuppressionEffect existing = target.EffectList.GetOfType<ClosedCircleSuppressionEffect>();
			if (existing != null)
			{
				existing.RefreshDuration(2500);
			}
			else
			{
				ClosedCircleSuppressionEffect effect = new ClosedCircleSuppressionEffect(m_caster, 2500);
				effect.Start(target);
			}

			// Send periodic visual effect animation on targets
			if (target is GamePlayer player)
			{
				player.Out.SendSpellEffectAnimation(this, target, 5772, 0, false, 1);
			}
		}
	}
}
