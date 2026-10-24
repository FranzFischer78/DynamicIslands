using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>How a player on a raft gets onto a generated island (IslandReach.Assess).</summary>
	public class ReachResult
	{
		/// <summary>IslandReach.Easy ... IslandReach.No.</summary>
		public int Level;
		/// <summary>One or two sentences for the generator window.</summary>
		public string Text = "";
		/// <summary>Share of the land players can walk on after swimming ashore; with jumps up ledges; and (for the tricky ways in) at all.</summary>
		public float Walk, WithJumps, Any;
		/// <summary>Share of the coast where a player can walk out of the water.</summary>
		public float Beaches;
		/// <summary>Height above the sea of the lowest place a player could stand on after getting out of the water (m; -1 = none).</summary>
		public float LowestLedge = -1f;
		public bool TopByWalking, TopAtAll;
	}

	/// <summary>
	/// Can a player on a raft get onto the island these settings make? Looks at the ground the generator would make (at
	/// the editor's resolution) with Raft's player: the slope they can walk up, the step they climb without jumping, how
	/// high a jump lifts them from the ground, from the water, and from the deck of a raft pushed against the coast
	/// (measured in game with the dev command CIPlayerJump). Wading ashore on a gentle slope is easy; a cliff with a
	/// low ledge needs a jump out of the water; a slightly higher one a jump from the raft; anything higher needs
	/// building (stairs, a ladder, foundations up the cliff).
	/// </summary>
	public static class IslandReach
	{
		public const int Easy = 0, Mostly = 1, Tricky = 2, VeryTricky = 3, No = 4;

		// Raft's player (CIPlayerJump)
		/// <summary>Steepest ground (degrees) the player walks up.</summary>
		public static float WalkSlope = 45f;
		/// <summary>A step the player climbs without jumping (m).</summary>
		public static float StepUp = 0.3f;
		/// <summary>How high a jump from the ground lifts the feet (m): Raft's jumpSpeed 7 and gravity 20 give 1.23 m.</summary>
		public static float JumpUp = 1.25f;
		/// <summary>The highest ledge (m above the sea) a swimmer gets onto with one jump out of the water. They float with the feet
		/// about 1.5 m down and the jump (1.25 x the jump speed) lifts them to about 0.35 m above the sea, but a coast as steep
		/// as the island's ground can be (its 1.95 m grid) catches them on the way up: measured with Raft's own controller
		/// (CIReachWorld), one jump usually gets onto 0.4-1.2 m ledges (sometimes a few, with the bobbing), 1.6 m takes two or three.</summary>
		public static float SwimLedge = 1.2f;
		/// <summary>The highest ledge a player gets onto at all without building: jumping out of the water (or from a raft pushed
		/// against the coast) and hopping on up the steep rock face, where Raft counts them as standing. Measured (CIReachWorld):
		/// 2.4 m in 2-4 jumps; 2.7 m and more never, from the water or from a raft's deck.</summary>
		public static float ClimbLedge = 2.4f;
		/// <summary>The raft's deck (m above the sea; the top of its foundations, measured 0.2-0.46 m as it bobs).</summary>
		public static float RaftDeck = 0.3f;
		/// <summary>Water a player wades in with their head above it (m).</summary>
		const float Wade = 1.5f;

		/// <summary>Assesses the settings (at the editor's ground resolution). elevation: the island floats this high (&gt; 0) or lies this deep (&lt; 0).</summary>
		public static ReachResult Assess(IslandGenSettings settings, float elevation = 0f)
		{
			IslandGenSettings s = settings.Copy();
			s.Clamp();
			float step = IslandGenerator.BuildArea.x / (IslandGenerator.BuildResolution - 1);
			// Enough ground around the land for its coast and shallow water, on the editor's own grid
			float extent = s.Source.Length > 0 ? IslandGenerator.Reach(s) * 0.85f : s.Radius * (1.15f + 0.35f * s.CoastAmount) * Mathf.Sqrt(Mathf.Max(1f, s.Stretch)) + 20f;
			int res = Mathf.Clamp(Mathf.RoundToInt(extent * 2f / step) | 1, 33, 385);
			float span = (res - 1) * step;
			float[,] m = IslandGenerator.HeightsMetres(s, IslandGenerator.BuildArea, res, span);
			ReachResult r = Assess(m, step, s.WaterLevel);
			Describe(r, elevation, s);
			return r;
		}

		/// <summary>The last Assess's cells a player gets to without building (wading or swimming ashore, walking and jumping up ledges).</summary>
		internal static bool[] LastReachable;

		/// <summary>
		/// Moves a generated island's chests and notes that stand where players can't get without building (on a stack's
		/// top, a cliff ledge) to the nearest place they can, within 40 m (ROADMAP LM3). Returns how many moved.
		/// </summary>
		public static int MoveContentWithinReach(IslandFile f)
		{
			if (f == null || f.Heights == null || f.Elevation != 0f) return 0;
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			var m = new float[res, res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) m[z, x] = f.Heights[z, x] * f.TerrainSize.y;
			Assess(m, step, f.WaterLevel);
			bool[] ok = LastReachable;
			if (ok == null) return 0;
			int moved = 0;
			foreach (IslandObject o in f.Objects)
			{
				bool content = ContentCatalog.IsLootObject(o.Name) || ContentCatalog.IsNoteObject(o.Name) || (o.Props != null && (o.Props.ContainsKey(ObjectProps.LootItems) || o.Props.ContainsKey(ObjectProps.NoteTitle)));
				if (!content || o.Props != null && o.Props.ContainsKey("set.piece")) continue;
				int cx = Mathf.Clamp(Mathf.RoundToInt(o.Position.x / step), 0, res - 1), cz = Mathf.Clamp(Mathf.RoundToInt(o.Position.z / step), 0, res - 1);
				// (under water, or above the ground - in a building, on a deck: as it was put)
				if (m[cz, cx] < f.WaterLevel + 0.05f || o.Position.y > m[cz, cx] + 1f || ok[cz * res + cx]) continue;
				int best = -1; float bestD = float.MaxValue;
				int r = Mathf.CeilToInt(40f / step);
				for (int dz = -r; dz <= r; dz++)
					for (int dx = -r; dx <= r; dx++)
					{
						int x = cx + dx, z = cz + dz;
						if (x < 1 || z < 1 || x >= res - 1 || z >= res - 1) continue;
						int i = z * res + x;
						if (!ok[i] || m[z, x] < f.WaterLevel + 0.4f) continue;
						float d = dx * dx + dz * dz;
						if (d < bestD) { bestD = d; best = i; }
					}
				if (best < 0) continue;
				int bx = best % res, bz = best / res;
				o.Position = new Vector3(bx * step, m[bz, bx], bz * step);
				moved++;
			}
			return moved;
		}

		/// <summary>Assesses heights (m above the terrain's base, sea at waterLevel) on a grid of this step (m).</summary>
		public static ReachResult Assess(float[,] m, float step, float waterLevel)
		{
			var r = new ReachResult();
			int res = m.GetLength(0), n = res * res;
			var h = new float[n];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) h[z * res + x] = m[z, x] - waterLevel;
			// Where a player can stand: walkable ground on land, and in water shallow enough to wade in
			var stand = new bool[n];
			var land = new bool[n];
			int landCount = 0;
			float top = float.MinValue;
			for (int z = 1; z < res - 1; z++)
				for (int x = 1; x < res - 1; x++)
				{
					int i = z * res + x;
					float gx = (h[i + 1] - h[i - 1]) / (2f * step), gz = (h[i + res] - h[i - res]) / (2f * step);
					float slope = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
					land[i] = h[i] > 0.02f;
					if (land[i]) { landCount++; top = Mathf.Max(top, h[i]); }
					stand[i] = slope <= WalkSlope && h[i] > -Wade;
				}
			if (landCount == 0) { r.Level = No; return r; }

			// Ways in: wading ashore (standing ground in shallow water), jumping out of deeper water onto a ledge, or
			// jumping from the raft's deck onto one
			var wade = new List<int>();
			var swimJump = new List<int>();
			var raftJump = new List<int>();
			int coast = 0, beachCoast = 0;
			float lowest = float.MaxValue;
			for (int z = 2; z < res - 2; z++)
				for (int x = 2; x < res - 2; x++)
				{
					int i = z * res + x;
					if (stand[i] && h[i] <= 0.1f && h[i] > -Wade && NextTo(h, i, res, v => v < -0.9f)) wade.Add(i);
					if (!land[i]) continue;
					bool atWater = NextTo(h, i, res, v => v < -0.9f, 2);
					bool coastCell = NextTo(h, i, res, v => v <= 0.02f);
					if (coastCell) { coast++; if (NextTo(h, i, res, v => v <= 0.1f && v > -Wade, 2) && WadeAround(h, stand, i, res)) beachCoast++; }
					if (!stand[i] || !atWater) continue;
					lowest = Mathf.Min(lowest, h[i]);
					if (h[i] <= SwimLedge) swimJump.Add(i);
					if (h[i] <= ClimbLedge) raftJump.Add(i);
				}
			r.Beaches = coast > 0 ? beachCoast / (float)coast : 0f;
			r.LowestLedge = lowest < float.MaxValue ? lowest : -1f;

			bool[] walk = Flood(h, stand, res, step, wade, false);
			var jumpStarts = new List<int>(wade); jumpStarts.AddRange(swimJump);
			bool[] jumps = Flood(h, stand, res, step, jumpStarts, true);
			LastReachable = jumps;
			var anyStarts = new List<int>(jumpStarts); anyStarts.AddRange(raftJump);
			bool[] any = Flood(h, stand, res, step, anyStarts, true);
			int w = 0, j = 0, a = 0;
			float topWalk = float.MinValue, topAny = float.MinValue;
			for (int i = 0; i < n; i++)
			{
				if (!land[i]) continue;
				if (walk[i]) { w++; topWalk = Mathf.Max(topWalk, h[i]); }
				if (jumps[i]) j++;
				if (any[i]) { a++; topAny = Mathf.Max(topAny, h[i]); }
			}
			r.Walk = w / (float)landCount; r.WithJumps = j / (float)landCount; r.Any = a / (float)landCount;
			// (the top counts as reached within a couple of metres of it: rocks and spires aren't the island's top)
			float near = Mathf.Max(2f, top * 0.06f);
			r.TopByWalking = topWalk >= top - near;
			r.TopAtAll = topAny >= top - near;

			// (a scrap of beach under a cliff isn't getting onto the island: at least a tenth of the land must be reached)
			const float Some = 0.1f;
			if (r.Walk >= Some) r.Level = r.Walk >= 0.8f && r.TopByWalking ? Easy : Mostly;
			else if (r.WithJumps >= Some) r.Level = Tricky;
			else if (r.Any >= Some) r.Level = VeryTricky;
			else r.Level = No;
			return r;
		}

		/// <summary>True if a neighbour of cell i (within reach cells) has a height that matches.</summary>
		static bool NextTo(float[] h, int i, int res, Func<float, bool> match, int reach = 1)
		{
			int x = i % res, z = i / res;
			for (int dz = -reach; dz <= reach; dz++)
				for (int dx = -reach; dx <= reach; dx++)
				{
					if (dx == 0 && dz == 0) continue;
					int xx = x + dx, zz = z + dz;
					if (xx < 0 || zz < 0 || xx >= res || zz >= res) continue;
					if (match(h[zz * res + xx])) return true;
				}
			return false;
		}

		/// <summary>A coast cell with standing ground in the shallow water beside it (a beach, not a cliff foot).</summary>
		static bool WadeAround(float[] h, bool[] stand, int i, int res)
		{
			int x = i % res, z = i / res;
			for (int dz = -2; dz <= 2; dz++)
				for (int dx = -2; dx <= 2; dx++)
				{
					int xx = x + dx, zz = z + dz;
					if (xx < 0 || zz < 0 || xx >= res || zz >= res) continue;
					int k = zz * res + xx;
					if (h[k] <= 0.1f && h[k] > -Wade && stand[k]) return true;
				}
			return false;
		}

		/// <summary>Everywhere a player gets from the starts: walking between standing cells (up the walkable slope and small steps), with jumps also up ledges a jump high.</summary>
		static bool[] Flood(float[] h, bool[] stand, int res, float step, List<int> starts, bool jumping)
		{
			var seen = new bool[h.Length];
			var queue = new Queue<int>();
			foreach (int s in starts) if (!seen[s]) { seen[s] = true; queue.Enqueue(s); }
			float walkRise = Mathf.Tan(WalkSlope * Mathf.Deg2Rad);
			while (queue.Count > 0)
			{
				int i = queue.Dequeue();
				int x = i % res, z = i / res;
				int reach = jumping ? 2 : 1;
				for (int dz = -reach; dz <= reach; dz++)
					for (int dx = -reach; dx <= reach; dx++)
					{
						if (dx == 0 && dz == 0) continue;
						int xx = x + dx, zz = z + dz;
						if (xx < 1 || zz < 1 || xx >= res - 1 || zz >= res - 1) continue;
						int k = zz * res + xx;
						if (seen[k] || !stand[k]) continue;
						float dist = Mathf.Sqrt(dx * dx + dz * dz) * step, rise = h[k] - h[i];
						bool adjacent = Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1;
						bool ok = adjacent && rise <= walkRise * dist + StepUp;
						if (!ok && jumping) ok = rise <= JumpUp && dist <= 2.9f * step;
						if (!ok) continue;
						seen[k] = true;
						queue.Enqueue(k);
					}
			}
			return seen;
		}

		static string Pct(float v) { return Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + " %"; }

		static string M(float v) { return v.ToString(v < 10f ? "0.0" : "0", CultureInfo.InvariantCulture) + " m"; }

		/// <summary>The window's sentence for a result (and the island's height above or below the sea).</summary>
		static void Describe(ReachResult r, float elevation, IslandGenSettings s)
		{
			if (elevation > 1f)
			{
				r.Level = No;
				r.Text = "Not from the raft: it floats " + elevation.ToString("F0", CultureInfo.InvariantCulture) + " m above the sea. Players have to build up to it (a tower or stairs on the raft).";
				return;
			}
			if (elevation < -1f)
			{
				r.Level = Tricky;
				r.Text = "By diving: it lies under water, its top about " + Mathf.Max(1f, -elevation - s.Height).ToString("F0", CultureInfo.InvariantCulture) + " m down.";
				return;
			}
			string top = r.TopByWalking ? "" : r.TopAtAll ? " The top takes some climbing and jumping." : " The top can only be reached by building.";
			switch (r.Level)
			{
				case Easy:
					r.Text = "Easy: swim ashore and walk up - " + (r.Beaches >= 0.01f ? "beaches along " + Pct(r.Beaches) + " of the coast" : "the coast is low enough to walk out") + (r.TopByWalking ? ", and a walk to the top." : "." + top);
					break;
				case Mostly:
					r.Text = "Reachable: wade ashore " + (r.Beaches >= 0.01f ? "on the beaches (" + Pct(r.Beaches) + " of the coast)" : "where the coast is low") + "; " + Pct(r.Walk) + " of the land is walkable from there" +
						(r.WithJumps > r.Walk + 0.05f ? ", " + Pct(r.WithJumps) + " with jumps up ledges." : ".") +
						(r.Any >= 0.97f ? top : r.TopAtAll ? top + " The rest needs building." : " The rest, and the top, need building (stairs, ladders).");
					break;
				case Tricky:
					r.Text = "Possible but tricky: no beach to walk out of the water. A player has to jump out of the sea or up from the shallows onto a low ledge" +
						(r.LowestLedge >= 0f ? " (the lowest is " + M(r.LowestLedge) + " above the water)" : "") + "." + (r.WithJumps < 0.5f ? " Most of the island still needs building." : "");
					break;
				case VeryTricky:
					r.Text = "Possible but unlikely: the only way up is a ledge " + M(Mathf.Max(0f, r.LowestLedge)) +
						" above the water - jumping out of the sea (or off a raft pushed right against it) and hopping on up the steep rock. That would be extremely tricky.";
					break;
				default:
					r.Text = "Not from the raft without building: cliffs all around" + (r.LowestLedge >= 0f ? " (the lowest ledge is " + M(r.LowestLedge) + " above the water)" : "") +
						". Players need stairs, a ladder or foundations built up the cliff.";
					break;
			}
		}

		/// <summary>Colour of the window's line for a level.</summary>
		public static string ColourOf(int level)
		{
			switch (level)
			{
				case Easy: return "#9fd88a";
				case Mostly: return "#cfe08a";
				case Tricky: return "#f0c060";
				case VeryTricky: return "#f09a50";
				default: return "#f07060";
			}
		}
	}
}
