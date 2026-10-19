using System;
using DOL.GS;

namespace DOL.GS
{
	/// <summary>
	/// Manages ancient wilderness and cross-realm Ley-Line Confluences.
	/// Being within a confluence stabilizes spellcrafting overcharge by grounding magical backlashes.
	/// </summary>
	public static class LeyLineSanctum
	{
		public struct SanctumLocation
		{
			public string Name;
			public ushort RegionID;
			public int X;
			public int Y;
			public int Z;
			public int Radius;
			public bool WholeRegion;
		}

		public static readonly SanctumLocation[] Locations =
		[
			// Albion Wilderness
			new SanctumLocation { Name = "Stonehenge", RegionID = 1, X = 585762, Y = 585248, Z = 2109, Radius = 4500 },
			new SanctumLocation { Name = "Sunken Ruins of Avalon Marsh", RegionID = 1, X = 445000, Y = 645000, Z = 2200, Radius = 4500 },

			// Midgard Wilderness
			new SanctumLocation { Name = "Hagall Runestones", RegionID = 100, X = 704904, Y = 737619, Z = 6536, Radius = 4500 },
			new SanctumLocation { Name = "Roots of Yggdrasil", RegionID = 100, X = 693336, Y = 704000, Z = 4500, Radius = 4500 },

			// Hibernia Wilderness
			new SanctumLocation { Name = "Cromlech of Lough Gur", RegionID = 200, X = 334818, Y = 719908, Z = 4296, Radius = 4500 },
			new SanctumLocation { Name = "Dolmens of Cullen", RegionID = 200, X = 385000, Y = 745000, Z = 5000, Radius = 4500 },

			// Neutral & Dresden Sanctuaries
			new SanctumLocation { Name = "Catacombs of Edinburgh", RegionID = 65, WholeRegion = true },
			new SanctumLocation { Name = "Grand Conclave of Edinburgh", RegionID = 192, WholeRegion = true },
			new SanctumLocation { Name = "The Nevernever Ways Nexus", RegionID = 197, WholeRegion = true },
		];

		public static bool IsInSanctum(GamePlayer player, out string sanctumName)
		{
			sanctumName = null;
			if (player == null || player.CurrentRegion == null) return false;
			return IsInSanctum(player.CurrentRegionID, player.X, player.Y, out sanctumName);
		}

		public static bool IsInSanctum(ushort reg, int px, int py, out string sanctumName)
		{
			sanctumName = null;

			foreach (var loc in Locations)
			{
				if (loc.RegionID != reg) continue;

				if (loc.WholeRegion)
				{
					sanctumName = loc.Name;
					return true;
				}

				long dx = px - loc.X;
				long dy = py - loc.Y;
				if (dx * dx + dy * dy <= (long)loc.Radius * loc.Radius)
				{
					sanctumName = loc.Name;
					return true;
				}
			}

			return false;
		}
	}
}
