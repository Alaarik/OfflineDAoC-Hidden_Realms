using System.Collections.Generic;
using DOL.Database;

namespace DOL.GS.RealmAbilities
{
	public class AtlasOF_ClosedCircle : ClosedCircleAbility
	{
		public AtlasOF_ClosedCircle(DbAbility dba, int level) : base(dba, level) { }

		public override int MaxLevel => 1;

		public override int CostForUpgrade(int level) => 0;

		public override bool CheckRequirement(GamePlayer player) => player.RealmLevel >= 40;

		public override int GetReUseDelay(int level) => 600; // 10 minutes
	}
}
