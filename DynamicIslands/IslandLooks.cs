using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Look-alike randomized story islands (TODO §3, PLAN_generator §5): on the generator's Randomize existing tab a
	/// "something new like it" or "a variation of it" of one of Raft's story islands follows that island's look - its
	/// own pieces, layout shape and height - not only its numbers: Caravan Town's red rock mesas with huts on pillars and
	/// rope bridges, Tangaroa's dome and the hexagon roads round its tower, Utopia's white towers, Varuna Point's
	/// motherlode floors and crane, Balboa's giant firs and boulders, Temperance's big rocks, antenna and igloo.
	/// The look is IslandGenSettings.Look (an id, "" = none: old settings build as before); Shape sets the ground's numbers
	/// (RaftIslands.LikeIt / VariationOf), Build reshapes the ground and puts the pieces (GenBuildings.Apply, before a
	/// design; its own random stream). Piece sizes were measured with CIKitInfo, the originals' layouts with CILookDump.
	/// </summary>
	public static partial class Remakes
	{
		public class Look
		{
			public string Id, Label, Hint;
			/// <summary>Scene name parts of the islands it is for (the family: Caravan Island and Caravan Small 1-3).</summary>
			public string[] Scenes;
			/// <summary>The pieces it puts (loaded before generating).</summary>
			public string[] Kit;
			/// <summary>Sets the ground's numbers for the island (variation: its own ground is the start).</summary>
			public Action<RaftIsland, IslandGenSettings, bool> Shape;
			/// <summary>Reshapes the ground and puts the pieces; what it made, or null.</summary>
			public Func<MapKit, IslandGenSettings, System.Random, string> Build;
			/// <summary>Puts what a design built later may have cleared (after the designs); what it made, or null.</summary>
			public Func<MapKit, IslandGenSettings, System.Random, string> After;
		}

		static Look[] looks;
		public static Look[] Looks { get { return looks ?? (looks = MakeLooks().ToArray()); } }

		/// <summary>The look of one of Raft's islands (its scene name), or null.</summary>
		public static Look LookFor(string scene)
		{
			scene = scene ?? "";
			return Looks.FirstOrDefault(l => l.Scenes.Any(p => scene.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
		}

		public static Look LookById(string id) { return string.IsNullOrEmpty(id) ? null : Looks.FirstOrDefault(l => l.Id == id); }

		/// <summary>The island's family scene for its designs: the small Caravan and Balboa islands use the big one's.</summary>
		public static string FamilyScene(string scene)
		{
			scene = scene ?? "";
			return scene.Replace("Landmark_CaravanSmall", "Landmark_CaravanIsland").Replace("Landmark_BalboaSmall", "Landmark_BalboaIsland");
		}

		/// <summary>Sets the settings' look from the island (after LikeIt's / VariationOf's numbers).</summary>
		public static void LookLike(RaftIsland i, IslandGenSettings s, bool variation)
		{
			Look l = LookFor(i.Scene);
			s.Look = l != null ? l.Id : "";
			s.LookTop = i.Top;
			if (l != null && l.Shape != null) l.Shape(i, s, variation);
		}

		/// <summary>The pieces the settings' look puts (loaded on demand before generating).</summary>
		public static List<string> LookNames(IslandGenSettings s)
		{
			Look l = LookById(s.Look);
			return l != null && l.Kit != null ? l.Kit.ToList() : new List<string>();
		}

		/// <summary>Builds the settings' look on the kit's file; what it made, or null.</summary>
		public static string BuildLook(MapKit k, IslandGenSettings s)
		{
			Look l = LookById(s.Look);
			if (l == null || l.Build == null) return null;
			int index = Array.IndexOf(Looks, l);
			return l.Build(k, s, new System.Random(s.Seed * 4099 + 11 + index));
		}

		/// <summary>The look's last pieces, after the designs (a design on the top clears what stood there); what it made, or null.</summary>
		public static string FinishLook(MapKit k, IslandGenSettings s)
		{
			Look l = LookById(s.Look);
			if (l == null || l.After == null) return null;
			return l.After(k, s, new System.Random(s.Seed * 4253 + 17 + Array.IndexOf(Looks, l)));
		}

		static IEnumerable<Look> MakeLooks()
		{
			yield return new Look { Id = "caravan", Label = "Caravan Town's mesas", Scenes = new[] { "Landmark_CaravanIsland", "Landmark_CaravanSmall" }, Kit = CaravanLookKit,
				Shape = CaravanShape, Build = CaravanMesas,
				Hint = "Flat-topped red rock mesas over low sand, ladders up their sides, a hut on pillars on the biggest, rope bridges between neighbours, caravans and vans at their feet, acacias and dead trees" };
			yield return new Look { Id = "tangaroa", Label = "Tangaroa's dome", Scenes = new[] { "Landmark_Tangaroa" }, Kit = TangaroaLookKit,
				Shape = TangaroaShape, Build = TangaroaDome,
				Hint = "Tangaroa's glass dome on its round floor over low land, the hexagon roads round the tower in the middle, the cockpit on top, the floats round it" };
			yield return new Look { Id = "utopia", Label = "Utopia's towers", Scenes = new[] { "Landmark_Utopia" }, Kit = UtopiaLookKit,
				Shape = UtopiaShape, Build = UtopiaTowers,
				Hint = "Utopia's tall white towers rising from the sea round a low island, its market, docks and plantation decks between them" };
			yield return new Look { Id = "varuna", Label = "Varuna Point's motherlode", Scenes = new[] { "Landmark_VarunaPoint" }, Kit = VarunaLookKit,
				Shape = VarunaShape, Build = VarunaMotherlode,
				Hint = "Varuna Point's motherlode floors on their pillars over a rocky island, the apex crane over them, a lift shaft and walls" };
			yield return new Look { Id = "balboa", Label = "Balboa's forest", Scenes = new[] { "Landmark_BalboaIsland", "Landmark_BalboaSmall" }, Kit = BalboaLookKit,
				Build = BalboaGiants,
				Hint = "Balboa's giant firs on the high ground and its huge boulders on the slopes and the shore" };
			yield return new Look { Id = "temperance", Label = "Temperance's snowfield", Scenes = new[] { "Landmark_Temperance" }, Kit = TemperanceLookKit,
				Build = TemperanceRocks,
				Hint = "Temperance's big unclimbable rocks, the telephone antenna on the top and a large igloo on the flat" };
			yield return new Look { Id = "vasagatan", Label = "The Vasagatan's beach", Scenes = new[] { "Landmark_Vasagatan" }, Kit = VasagatanLookKit,
				Build = VasagatanBeach,
				Hint = "The Vasagatan's long low island, the liner's lounge furniture and its safe washed up on the beach (its designs put the liner's rooms on the land)" };
			yield return new Look { Id = "radio", Label = "The Radio Tower's rocks", Scenes = new[] { "Landmark_Radar" }, Kit = RadioLookKit,
				Shape = RadioShape, Build = RadioRocks, After = RadioDish,
				Hint = "The Radio Tower's low rocks just over the sea, the concrete footings of its platforms on them, a satellite dish and a plastic boat (its designs build the tower)" };
		}

		#region Helpers

		static float Rf(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

		/// <summary>A random spot within radius where ok(height above the sea, slope) holds and keepApart from what was put (own stream).</summary>
		static Vector2? FindR(MapKit k, System.Random r, Vector2 around, float radius, Func<float, float, bool> ok, float keepApart)
		{
			for (int i = 0; i < 400; i++)
			{
				float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)r.NextDouble()) * radius;
				Vector2 p = around + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
				if (!k.IsFree(p, keepApart)) continue;
				if (ok(k.Ground(p) - k.Sea, k.Slope(p))) return p;
			}
			return null;
		}

		/// <summary>
		/// The ground within reach of c set to height(point, old height) (terrain heights); what stands there moves with
		/// the ground, and scattered nature left on a new steep wall goes.
		/// </summary>
		static void Reshape(MapKit k, Vector2 c, float reach, Func<Vector2, float, float> height)
		{
			IslandFile file = k.File;
			int res = file.HeightmapResolution;
			float step = file.TerrainSize.x / (res - 1), size = file.TerrainSize.y;
			var before = file.Objects.Where(o => (new Vector2(o.Position.x, o.Position.z) - c).sqrMagnitude < (reach + step) * (reach + step))
				.Select(o => new KeyValuePair<IslandObject, float>(o, k.Ground(new Vector2(o.Position.x, o.Position.z)))).ToList();
			for (int zi = Mathf.Max(0, Mathf.FloorToInt((c.y - reach) / step)); zi <= Mathf.Min(res - 1, Mathf.CeilToInt((c.y + reach) / step)); zi++)
				for (int xi = Mathf.Max(0, Mathf.FloorToInt((c.x - reach) / step)); xi <= Mathf.Min(res - 1, Mathf.CeilToInt((c.x + reach) / step)); xi++)
				{
					float h = file.Heights[zi, xi] * size, n = height(new Vector2(xi * step, zi * step), h);
					if (Mathf.Abs(n - h) > 0.001f) file.Heights[zi, xi] = Mathf.Clamp01(n / size);
				}
			foreach (var kv in before)
			{
				var p = new Vector2(kv.Key.Position.x, kv.Key.Position.z);
				float d = k.Ground(p) - kv.Value;
				if (Mathf.Abs(d) < 0.01f) continue;
				bool nature = kv.Key.Props == null || kv.Key.Props.Count == 0;
				if (nature && Mathf.Abs(d) > 1f && k.Slope(p) > 35f) file.Objects.Remove(kv.Key);
				else kv.Key.Position.y += d;
			}
		}

		/// <summary>A piece put by the middle of its mesh box at c (world), turned yaw, its pivot at height y.</summary>
		static IslandObject Centred(MapKit k, string name, Vector2 c, float yaw, float y, Dictionary<string, string> props = null)
		{
			Bounds b;
			if (!GenBuildings.Measured(name, out b)) b = new Bounds(Vector3.zero, Vector3.one);
			Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(b.center.x, 0f, b.center.z);
			return k.Add(name, new Vector3(c.x - off.x, y, c.y - off.z), yaw, props, 0f);
		}

		/// <summary>The bottom (b.min.y) of a piece's mesh box from its pivot (0 when it isn't measured).</summary>
		static float BottomOf(string name) { Bounds b; return GenBuildings.Measured(name, out b) ? b.min.y : 0f; }

		/// <summary>A piece standing by the middle of its box at c: its bottom at the lowest ground under its box, sunk a little.</summary>
		static IslandObject Stand(MapKit k, string name, Vector2 c, float yaw, float sink = 0.1f)
		{
			Bounds b;
			if (!GenBuildings.Measured(name, out b)) b = new Bounds(Vector3.zero, Vector3.one);
			// (on the part it stands on where that is smaller: the excavator's tracks, not its boom - RaftProps.SeenStands)
			PropInfo info = RaftProps.Get(name);
			float ground;
			if (info != null && info.StandSize.x > 0f)
			{
				Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(info.StandCentre.x - b.center.x, 0f, info.StandCentre.y - b.center.z);
				ground = LowestUnderBox(k, new Bounds(Vector3.zero, new Vector3(info.StandSize.x, 1f, info.StandSize.y)), c + new Vector2(off.x, off.z), yaw);
			}
			else ground = LowestUnderBox(k, b, c, yaw);
			IslandObject o = Centred(k, name, c, yaw, ground - sink - b.min.y);
			k.Stood.Add(new KeyValuePair<IslandObject, float>(o, sink));
			return o;
		}

		/// <summary>The lowest ground under a box (its middle at c, turned yaw): at its middle and near its corners.</summary>
		static float LowestUnderBox(MapKit k, Bounds b, Vector2 c, float yaw)
		{
			Quaternion q = Quaternion.Euler(0f, yaw, 0f);
			Vector3 ex = q * new Vector3(b.extents.x * 0.7f, 0f, 0f), ez = q * new Vector3(0f, 0f, b.extents.z * 0.7f);
			float ground = float.MaxValue;
			foreach (Vector3 d in new[] { Vector3.zero, ex + ez, ex - ez, -ex + ez, -ex - ez }) ground = Mathf.Min(ground, k.Ground(c + new Vector2(d.x, d.z)));
			return ground;
		}

		/// <summary>
		/// The look's standing pieces (Stand) stood on the ground again, after everything that changes the ground later: a
		/// design's pad, a building's or a cave's floor. Those carry what stands in their blend with the ground at its pivot,
		/// so a big piece on a steep blend stood in the air on its low side: a dead tree 1.1 m over a pad cut into a Caravan
		/// variation's ground (CILookAlike, seed 7, 2026-10-20). What a pad cleared away is gone and stays gone.
		/// </summary>
		public static void SettleLook(MapKit k)
		{
			if (k.Stood.Count == 0) return;
			var there = new HashSet<IslandObject>(k.File.Objects);
			foreach (KeyValuePair<IslandObject, float> kv in k.Stood)
			{
				IslandObject o = kv.Key;
				if (o == null || !there.Contains(o)) continue;
				Bounds b;
				if (!GenBuildings.Measured(o.Name, out b)) b = new Bounds(Vector3.zero, Vector3.one);
				// (its box's middle from its pivot, as Centred put it)
				float yaw = o.EulerRotation.y;
				Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(b.center.x, 0f, b.center.z);
				Vector2 c = new Vector2(o.Position.x + off.x, o.Position.z + off.z);
				o.Position.y = LowestUnderBox(k, b, c, yaw) - kv.Value - b.min.y;
			}
			k.Stood.Clear();
		}

		/// <summary>How far the ground falls and rises under a piece's box at c (any yaw): its highest less its lowest point.</summary>
		static float GroundSpread(MapKit k, string name, Vector2 c)
		{
			Bounds b;
			if (!GenBuildings.Measured(name, out b)) b = new Bounds(Vector3.zero, Vector3.one);
			float e = Mathf.Max(b.extents.x, b.extents.z), lo = float.MaxValue, hi = float.MinValue;
			for (int i = 0; i < 9; i++)
			{
				Vector2 p = i == 8 ? c : c + new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f)) * e;
				float g = k.Ground(p);
				lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
			}
			return hi - lo;
		}

		static float YawOf(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }

		#endregion

		#region Caravan Town: red rock mesas

		static readonly string[] CaravanLookKit = { "Pillars", "Roof", "CaravanHubBridge2", "CaravanIsland_BigRoundRock_1", "AcaciaTree_Big1", "AcaciaTree_Big3",
			"DeadTree1", "DeadTree2", "DeadTree3", "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01", "Van_1", "Van_2", "Van_4", "Van_5", "LandmarkLadder_6m" };

		static void CaravanShape(RaftIsland i, IslandGenSettings s, bool variation)
		{
			if (variation) return;
			// (low sand: the height is in the mesas Build carves - Raft's Caravan islands are flat sand round steep red rocks)
			s.Height = Mathf.Clamp(i.Top * 0.18f, 3.5f, 8f);
			s.Peaks = 2;
			s.PeakShape = 0.2f;
			s.Roughness = 0.25f;
			s.Cliffs = 0f;
			s.Terraces = 0f;
			s.BeachWidth = Mathf.Max(s.BeachWidth, 0.5f);
		}

		class Mesa
		{
			public Vector2 C;
			public float R, Top, Base, Apron, ApronTop, LadderAng, P1, P2, P3, Noise;
			public Mesa Twin;
			public bool Carve = true;
			public float Rim(float ang) { return R * (1f + Noise * (0.6f * Mathf.Sin(3f * ang + P1) + 0.35f * Mathf.Sin(5f * ang + P2) + 0.18f * Mathf.Sin(9f * ang + P3))); }
			public float RimToward(Vector2 dir) { return Rim(Mathf.Atan2(dir.y, dir.x)); }
			public float Reach { get { return R * (1f + Noise * 1.2f) * Mathf.Max(1f, Apron) + Wall(Top - Base) * 3f + 4f; } }
			public static float Wall(float drop) { return Mathf.Max(1.5f, drop * 0.12f + 1f); }
		}

		/// <summary>A mesa's height at p: its flat top inside the rim, a steep wall down, a narrow notch where the ladder is, a talus at its foot.</summary>
		static float MesaHeight(Mesa m, Vector2 p, float h, float step)
		{
			Vector2 d = p - m.C;
			float dist = d.magnitude, ang = Mathf.Atan2(d.y, d.x);
			float off = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, m.LadderAng)) * Mathf.Deg2Rad * dist;
			bool notch = off < 2f;
			float best = h;
			Action<float, float> tier = (rim, top) =>
			{
				float drop = top - h;
				if (drop <= 0f) return;
				float wall = notch ? step * 1.1f : Mesa.Wall(drop);
				float v;
				if (dist <= rim) v = top + (Mathf.PerlinNoise(p.x * 0.11f + m.P1, p.y * 0.11f + m.P2) - 0.5f) * 0.6f;
				else if (dist <= rim + wall) v = Mathf.Lerp(top, h, Mathf.Pow((dist - rim) / wall, 0.8f));
				else
				{
					// (the talus: a little rubble slope at the foot)
					float t = (dist - rim - wall) / (wall * 2.5f);
					v = t < 1f ? h + drop * 0.1f * (1f - t) * (1f - t) : h;
				}
				best = Mathf.Max(best, v);
			};
			float body = m.Rim(ang);
			tier(body, m.Top);
			if (m.Apron > 1f) tier(body * m.Apron, m.ApronTop);
			return best;
		}

		static string CaravanMesas(MapKit k, IslandGenSettings s, System.Random r)
		{
			float sea = k.Sea, full = Mathf.Max(s.LookTop, 12f), scale = Mathf.Clamp(s.Radius / 40f, 0.75f, 1.6f);
			float step = k.File.TerrainSize.x / (k.File.HeightmapResolution - 1);
			bool variation = !string.IsNullOrEmpty(s.Source);
			var mesas = new List<Mesa>();
			Func<Mesa> fresh = () => new Mesa { P1 = Rf(r, 0f, 6.28f), P2 = Rf(r, 0f, 6.28f), P3 = Rf(r, 0f, 6.28f), Noise = Rf(r, 0.06f, 0.11f), LadderAng = Rf(r, -180f, 180f) };
			if (variation)
			{
				// (its own ground: Raft's mesas were measured as ground - their high spots get their flat tops and steep walls back)
				float high = sea + full * 0.45f;
				var cells = new List<Vector2>();
				float cellStep = 4f, reach = Mathf.Min(s.Radius * 1.3f, IslandGenerator.BuildArea.x / 2f);
				for (float x = -reach; x <= reach; x += cellStep)
					for (float z = -reach; z <= reach; z += cellStep)
					{
						Vector2 p = k.Mid + new Vector2(x, z);
						if (k.Ground(p) > high) cells.Add(p);
					}
				while (cells.Count > 0 && mesas.Count < 10)
				{
					Vector2 seed = cells.OrderByDescending(c => k.Ground(c)).First();
					List<Vector2> group = cells.Where(c => (c - seed).magnitude < 26f).ToList();
					cells.RemoveAll(c => group.Contains(c));
					if (group.Count < 4) continue;
					Mesa m = fresh();
					m.C = new Vector2(group.Average(c => c.x), group.Average(c => c.y));
					m.R = Mathf.Clamp(Mathf.Sqrt(group.Count * cellStep * cellStep / Mathf.PI), 5f, 22f);
					List<float> hs = group.Select(c => k.Ground(c)).OrderBy(h => h).ToList();
					m.Top = hs[(int)(hs.Count * 0.8f)];
					m.Base = Mathf.Max(sea + 1f, hs[0] - 6f);
					mesas.Add(m);
				}
			}
			else
			{
				int want = Mathf.Clamp(Mathf.RoundToInt(s.Radius * s.Radius * Mathf.PI / 1500f), 2, 8);
				for (int tries = 0; tries < want * 40 && mesas.Count < want; tries++)
				{
					Mesa m = fresh();
					bool first = mesas.Count == 0;
					Mesa twin = !first && r.NextDouble() < 0.5 ? mesas[r.Next(mesas.Count)] : null;
					if (twin != null && twin.Twin != null) twin = null;
					m.R = first ? Rf(r, 12.5f, 16f) : Rf(r, 6.5f, 12.5f) * scale;
					if (first) m.Noise *= 0.6f;
					if (twin != null)
					{
						float a = Rf(r, 0f, Mathf.PI * 2f), gap = Rf(r, 9f, 15f);
						var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
						m.C = twin.C + dir * (twin.RimToward(dir) + m.R + gap);
					}
					else
					{
						float a = Rf(r, 0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)r.NextDouble()) * s.Radius * (first ? 0.25f : 0.55f);
						m.C = k.Mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
					}
					// (on the land, apart from the others and from what a design put)
					if (k.Ground(m.C) < sea + 0.8f) continue;
					int wet = 0;
					for (int q = 0; q < 8; q++) { float a = q * Mathf.PI / 4f; if (k.Ground(m.C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m.R) < sea + 0.3f) wet++; }
					if (wet > 2) continue;
					if (mesas.Any(o => (o.C - m.C).magnitude < o.R + m.R + (o == twin ? 6f : 9f))) continue;
					if (!k.IsFree(m.C, m.R + 4f)) continue;
					m.Base = k.Ground(m.C);
					m.Top = twin != null ? twin.Top : first ? sea + full : sea + full * Rf(r, 0.45f, 0.9f);
					m.Top = Mathf.Max(m.Top, m.Base + 6f);
					if (!first && r.NextDouble() < 0.5)
					{
						m.Apron = Rf(r, 1.25f, 1.5f);
						m.ApronTop = m.Base + (m.Top - m.Base) * Rf(r, 0.25f, 0.45f);
					}
					m.Twin = twin;
					if (twin != null) twin.Twin = m;
					mesas.Add(m);
				}
			}
			if (mesas.Count == 0) return null;
			foreach (Mesa m in mesas) Reshape(k, m.C, m.Reach, (p, h) => MesaHeight(m, p, h, step));

			// Dressing: ladders up the notch, a hut on pillars on the biggest top, bridges between twins, trees, rocks, caravans
			int ladders = 0, bridges = 0, huts = 0, things = 0;
			foreach (Mesa m in mesas)
			{
				float ang = m.LadderAng * Mathf.Deg2Rad;
				var n = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
				Action<float, float> climb = (rim, top) =>
				{
					Vector2 edge = m.C + n * (rim + step * 1.1f);
					float bottom = k.Ground(edge + n * 1.5f);
					if (top - bottom < 3f) return;
					var f = new Frame(edge, YawOf(-n), 0f);
					for (float b = top - 6f; b > bottom - 6f + 0.01f; b -= 6f) { P(k, f, "LandmarkLadder_6m", 0f, -0.12f, b); ladders++; }
				};
				float body = m.Rim(ang);
				if (m.Apron > 1f) { climb(body * m.Apron, m.ApronTop); climb(body, m.Top); }
				else climb(body, m.Top);
			}
			Mesa big = mesas.OrderByDescending(m => m.R).First();
			foreach (Mesa m in mesas.OrderByDescending(m => m.R))
			{
				float minRim = Enumerable.Range(0, 16).Min(q => m.Rim(q * Mathf.PI / 8f));
				if (minRim >= 11.6f && (m == big || r.NextDouble() < 0.5))
				{
					// (the hut: Raft's pillars with their roof - one pivot for both)
					k.Clear(m.C, 12f);
					float yaw = 90f * r.Next(4) + Rf(r, -8f, 8f), y = m.Top - 0.25f - BottomOf("Pillars");
					Centred(k, "Pillars", m.C, yaw, y);
					Centred(k, "Roof", m.C, yaw, y);
					huts++;
				}
				else if (minRim > 4f)
				{
					string tree = Pick(r, "AcaciaTree_Big1", "AcaciaTree_Big3", "DeadTree1", "DeadTree2", "DeadTree3");
					Vector2 at = m.C + new Vector2(Rf(r, -1f, 1f), Rf(r, -1f, 1f)) * minRim * 0.4f;
					k.Clear(at, 4f);
					Stand(k, tree, at, Rf(r, 0f, 360f), 0.3f);
					things++;
				}
				if (minRim > 8f && r.NextDouble() < 0.8)
				{
					// (a caravan or van parked on the top, off the middle - Raft's own tops are full of them)
					float a = Rf(r, 0f, Mathf.PI * 2f);
					var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					Vector2 at = m.C + dir * minRim * 0.55f;
					if (k.IsFree(at, 4f) && k.Slope(at) < 10f && Mathf.Abs(k.Ground(at) - m.Top) < 1.5f)
					{
						k.Clear(at, 4f);
						Stand(k, Pick(r, "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01", "Van_1", "Van_2", "Van_4", "Van_5"), at, YawOf(dir) + Rf(r, -30f, 30f));
						things++;
					}
				}
				if (m.Twin != null && mesas.IndexOf(m.Twin) > mesas.IndexOf(m) && Mathf.Abs(m.Top - m.Twin.Top) < 2.5f)
				{
					Vector2 u = (m.Twin.C - m.C).normalized;
					Vector2 a = m.C + u * m.RimToward(u), b = m.Twin.C - u * m.Twin.RimToward(-u);
					float gap = (b - a).magnitude;
					Bounds bb;
					if (gap > 4f && gap < 21f && GenBuildings.Measured("CaravanHubBridge2", out bb))
					{
						float top = Mathf.Min(m.Top, m.Twin.Top);
						Centred(k, "CaravanHubBridge2", (a + b) / 2f, YawOf(u), top + 0.35f - bb.max.y);
						bridges++;
					}
				}
				// (round rocks at the foot, and caravans and vans parked by it)
				int rocks = 1 + r.Next(3);
				for (int q = 0; q < rocks; q++)
				{
					float a = Rf(r, 0f, Mathf.PI * 2f);
					var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					Vector2 at = m.C + dir * (m.Rim(a) * Mathf.Max(1f, m.Apron) + Mesa.Wall(m.Top - m.Base) + Rf(r, 1f, 4f));
					if (k.Ground(at) < sea + 0.2f || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, m.LadderAng)) < 20f) continue;
					Stand(k, "CaravanIsland_BigRoundRock_1", at, Rf(r, 0f, 360f), Rf(r, 0.5f, 1.5f));
					things++;
				}
				if (r.NextDouble() < 0.6)
				{
					float a = (m.LadderAng + Rf(r, 25f, 70f) * (r.NextDouble() < 0.5 ? 1f : -1f)) * Mathf.Deg2Rad;
					var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					Vector2 at = m.C + dir * (m.Rim(a) * Mathf.Max(1f, m.Apron) + Mesa.Wall(m.Top - m.Base) + 5f);
					if (k.Ground(at) > sea + 0.6f && k.Slope(at) < 14f && k.IsFree(at, 5f))
					{
						k.Clear(at, 5f);
						Stand(k, Pick(r, "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01", "Van_1", "Van_2", "Van_4", "Van_5"), at, YawOf(dir) + 90f + Rf(r, -20f, 20f));
						things++;
					}
				}
			}
			// The low sand between the mesas: Raft's Caravan islands have caravans, vans, acacias, dead trees and round rocks
			// all over it, not only at the mesas' feet (the look was sparser than the original, X7 2026-10-17)
			int scatter = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * s.Radius * s.Radius / 260f), 6, 36);
			for (int q = 0; q < scatter; q++)
			{
				double roll = r.NextDouble();
				bool car = roll < 0.35, rock = !car && roll < 0.6;
				Vector2? p = FindR(k, r, k.Mid, s.Radius * 0.95f, (above, slope) => above > 0.5f && above < full * 0.3f && slope < (car ? 10f : 16f), car ? 7f : 5f);
				if (!p.HasValue) continue;
				if (mesas.Any(m => (p.Value - m.C).magnitude < m.Rim(Mathf.Atan2(p.Value.y - m.C.y, p.Value.x - m.C.x)) * Mathf.Max(1f, m.Apron) + Mesa.Wall(m.Top - m.Base) + 1.5f)) continue;
				k.Clear(p.Value, car ? 4f : 2.5f);
				if (car) Stand(k, Pick(r, "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01", "Van_1", "Van_2", "Van_4", "Van_5"), p.Value, Rf(r, 0f, 360f));
				else if (rock) Stand(k, "CaravanIsland_BigRoundRock_1", p.Value, Rf(r, 0f, 360f), Rf(r, 0.5f, 1.5f));
				else Stand(k, Pick(r, "AcaciaTree_Big1", "AcaciaTree_Big3", "DeadTree1", "DeadTree2", "DeadTree3"), p.Value, Rf(r, 0f, 360f), 0.3f);
				things++;
			}
			return mesas.Count + " red rock mesas (" + huts + " hut(s) on pillars, " + bridges + " bridge(s), " + ladders + " ladder(s), " + things + " rocks, trees and caravans)";
		}

		#endregion

		#region Tangaroa: the dome

		static readonly string[] TangaroaLookKit = { "Dome_Base_2", "TangaroaRoads_Combined", "Dome_Base_1", "Balcony01", "Balcony02", "Cockpit", "CockpitExt",
			"Outside", "Dome_Frame_1", "Dome_Glass_01", "Dome_Glass_02", "Dome_Glass_03", "Dome_DoorFrame01" };

		static void TangaroaShape(RaftIsland i, IslandGenSettings s, bool variation)
		{
			s.Source = "";
			s.Shape = IslandShapes.Round;
			s.Radius = Mathf.Clamp(i.Radius * 1.05f, 96f, 115f);
			s.Height = 10f;
			s.Peaks = 1;
			s.PeakShape = 0.15f;
			s.Roughness = 0.2f;
			s.Cliffs = 0f;
			s.Coast = 0.15f;
			s.Bays = 0f;
			s.Stretch = 1f;
		}

		/// <summary>
		/// The dome's pieces share Raft's own pivot (the scene's middle, 0.8 m under its sea - CILookDump Tangaroa): the
		/// base, roads and middle tower once, the frame, glass and door frames five times round (every 72 degrees).
		/// </summary>
		static readonly string[] TangaroaOnce = { "Dome_Base_2", "TangaroaRoads_Combined", "Dome_Base_1", "Balcony01", "Balcony02", "Cockpit", "CockpitExt", "Outside" };
		static readonly string[] TangaroaFive = { "Dome_Frame_1", "Dome_Glass_01", "Dome_Glass_02", "Dome_Glass_03", "Dome_DoorFrame01" };
		const float TangaroaPivot = -0.8f;

		static string TangaroaDome(MapKit k, IslandGenSettings s, System.Random r)
		{
			Bounds disc;
			if (!GenBuildings.Measured("Dome_Base_2", out disc)) return null;
			float sea = k.Sea, rim = Mathf.Max(disc.extents.x, disc.extents.z), deck = TangaroaPivot + disc.min.y - 0.15f;
			Vector2 c = k.Mid;
			// (the ground under the disc levelled just below it, sloping down to the old ground past the rim)
			Reshape(k, c, rim + 14f, (p, h) =>
			{
				float d = (p - c).magnitude, flat = sea + deck;
				if (d <= rim - 2f) return flat;
				float t = Mathf.Clamp01((d - rim + 2f) / 16f);
				return Mathf.Lerp(flat, Mathf.Min(h, flat), t * t);
			});
			k.Clear(c, rim + 2f);
			float yaw = Rf(r, 0f, 72f);
			Vector3 at = new Vector3(c.x, sea + TangaroaPivot, c.y);
			int n = 0;
			foreach (string name in TangaroaOnce)
				if (k.Add(name, at, yaw, null, 0f) != null) n++;
			foreach (string name in TangaroaFive)
				for (int i = 0; i < 5; i++)
					if (k.Add(name, at, yaw + i * 72f, null, 0f) != null) n++;
			return n + " pieces of Tangaroa's dome on a " + (rim * 2f).ToString("F0") + " m levelled disc";
		}

		#endregion

		#region Utopia: white towers

		/// <summary>
		/// Utopia's towers where Raft puts them, from the scene's pivot 0.7 m under its sea (CILookDump Utopia): the three
		/// big ones and the room on top share it, the other four stand round them (offset x, y, z, turn).
		/// </summary>
		static readonly KeyValuePair<string, Vector4>[] UtopiaParts =
		{
			new KeyValuePair<string, Vector4>("UT_Set_Tower01", new Vector4(0f, 0f, 0f, 0f)),
			new KeyValuePair<string, Vector4>("UT_Set_Tower02", new Vector4(0f, 0f, 0f, 0f)),
			new KeyValuePair<string, Vector4>("UT_Set_Tower03", new Vector4(0f, 0f, 0f, 0f)),
			new KeyValuePair<string, Vector4>("UT_Set_Tower_EndRoom", new Vector4(0f, 0f, 0f, 0f)),
			new KeyValuePair<string, Vector4>("UT_Set_Tower_EndRoom_Glass", new Vector4(0f, 0f, 0f, 0f)),
			new KeyValuePair<string, Vector4>("UT_Set_TowerDeco01", new Vector4(-85.6f, -101.2f, -153.2f, 0f)),
			new KeyValuePair<string, Vector4>("UT_Set_TowerDeco02", new Vector4(25.4f, -60f, -158f, 180f)),
			new KeyValuePair<string, Vector4>("UT_Set_TowerDeco03", new Vector4(159.1f, -13.2f, -19.9f, 270f)),
			new KeyValuePair<string, Vector4>("UT_Set_TowerHarpoon", new Vector4(-128.84f, -43.86f, -49.45f, 339.7f)),
		};
		const float UtopiaPivot = -0.7f;
		static readonly string[] UtopiaLookKit = UtopiaParts.Select(kv => kv.Key).ToArray();

		static void UtopiaShape(RaftIsland i, IslandGenSettings s, bool variation)
		{
			s.Source = "";
			s.Shape = IslandShapes.Round;
			s.Radius = Mathf.Clamp(i.Radius * 0.6f, 30f, 70f);
			s.Height = 8f;
			s.Peaks = 2;
			s.PeakShape = 0.25f;
			s.Roughness = 0.3f;
			s.Cliffs = 0.1f;
			s.Stretch = 1f;
		}

		static string UtopiaTowers(MapKit k, IslandGenSettings s, System.Random r)
		{
			// (the towers' middle - of their boxes - put on the island's middle, the whole set turned by the seed)
			var mids = new List<Vector2>();
			foreach (var kv in UtopiaParts)
			{
				Bounds b;
				if (kv.Key.Contains("EndRoom") || !GenBuildings.Measured(kv.Key, out b)) continue;
				Vector3 m = new Vector3(kv.Value.x, 0f, kv.Value.z) + Quaternion.Euler(0f, kv.Value.w, 0f) * new Vector3(b.center.x, 0f, b.center.z);
				mids.Add(new Vector2(m.x, m.z));
			}
			if (mids.Count == 0) return null;
			Vector2 mid = new Vector2(mids.Average(v => v.x), mids.Average(v => v.y));
			float yaw = Rf(r, 0f, 360f);
			Quaternion q = Quaternion.Euler(0f, yaw, 0f);
			Vector3 off = q * new Vector3(mid.x, 0f, mid.y);
			Vector2 c = k.Mid;
			Vector3 origin = new Vector3(c.x - off.x, k.Sea + UtopiaPivot, c.y - off.z);
			int n = 0;
			foreach (var kv in UtopiaParts)
			{
				Bounds b;
				if (!GenBuildings.Measured(kv.Key, out b)) continue;
				Vector3 at = origin + q * new Vector3(kv.Value.x, kv.Value.y, kv.Value.z);
				Vector3 tc = at + q * (Quaternion.Euler(0f, kv.Value.w, 0f) * new Vector3(b.center.x, 0f, b.center.z));
				if (at.y + b.min.y < k.Sea) k.Clear(new Vector2(tc.x, tc.z), Mathf.Max(b.extents.x, b.extents.z) + 2f);
				if (k.Add(kv.Key, at, yaw + kv.Value.w, null, 0f) != null) n++;
			}
			return n + " of Utopia's white towers and their pieces";
		}

		#endregion

		#region Varuna Point: the motherlode

		static readonly string[] VarunaLookKit = { "VP_ApexCrane", "VP_MotherlodeFloor1", "VP_MotherlodeFloor2", "VP_ElevatorShaft", "VP_MotherlodeBuildingPieces_Wall",
			"VP_MotherlodePillar1_Stage1", "VP_MotherlodePillar2_Stage1", "VP_MotherlodePillar3_Stage1", "VP_RubblePile_Large01", "VP_RubblePile_Medium01",
			"VP_RubblePile_Medium02", "VP_BrickStack01", "VP_BrickStack02", "VP_Excavator" };

		static void VarunaShape(RaftIsland i, IslandGenSettings s, bool variation)
		{
			s.Source = "";
			s.Shape = IslandShapes.Round;
			s.Radius = Mathf.Clamp(i.Radius, 70f, 95f);
			s.Height = Mathf.Clamp(i.Top * 0.2f, 10f, 24f);
			s.Peaks = 1;
			s.Roughness = 0.6f;
			s.Cliffs = 0.6f;
			s.Stretch = 1f;
		}

		/// <summary>
		/// Varuna Point's motherlode as Raft builds it (CILookDump VarunaPoint): an 84 m floor, the next one 21 m up on
		/// four pillars (a pillar's pivot is the floor it hangs from), the 63 m walls between them round the sides, the
		/// apex crane on the top floor, the lift shaft and the excavator outside and rubble and bricks on both floors.
		/// </summary>
		static readonly Vector3[] VarunaWalls = { new Vector3(-27.8f, 32.39f, 180f), new Vector3(-32.4f, -27.81f, 90f), new Vector3(32.43f, 27.75f, 270f), new Vector3(27.74f, -32.44f, 0f) };
		const float VarunaStorey = 21f, VarunaRim = 0.54f;

		static string VarunaMotherlode(MapKit k, IslandGenSettings s, System.Random r)
		{
			Bounds floor;
			if (!GenBuildings.Measured("VP_MotherlodeFloor1", out floor)) return null;
			float half = Mathf.Max(floor.extents.x, floor.extents.z), g = k.Sea + Rf(r, 2.5f, 5f);
			float low = g - floor.min.y, high = low + VarunaStorey, top = low + floor.max.y, roof = high + floor.max.y;
			Vector2 c = k.Mid;
			float reach = half * 1.45f;
			Reshape(k, c, reach + 16f, (p, h) =>
			{
				float d = (p - c).magnitude;
				if (d <= reach) return g;
				return Mathf.Lerp(g, h, Mathf.Clamp01((d - reach) / 16f));
			});
			k.Clear(c, reach + 2f);
			float yaw = Rf(r, 0f, 360f);
			Quaternion q = Quaternion.Euler(0f, yaw, 0f);
			Func<float, float, Vector2> at = (x, z) => { Vector3 v = q * new Vector3(x, 0f, z); return c + new Vector2(v.x, v.z); };
			int n = 0;
			if (k.Add("VP_MotherlodeFloor1", new Vector3(c.x, low, c.y), yaw, null, 0f) != null) n++;
			if (k.Add("VP_MotherlodeFloor2", new Vector3(c.x, high, c.y), yaw, null, 0f) != null) n++;
			string[] pillars = { "VP_MotherlodePillar1_Stage1", "VP_MotherlodePillar2_Stage1", "VP_MotherlodePillar3_Stage1" };
			string pillar = pillars[r.Next(pillars.Length)];
			foreach (var corner in new[] { new Vector2(14f, 14f), new Vector2(-14f, 14f), new Vector2(14f, -14f), new Vector2(-14f, -14f) })
			{
				Vector2 p = at(corner.x, corner.y);
				if (k.Add(pillar, new Vector3(p.x, high, p.y), yaw, null, 0f) != null) n++;
			}
			// (now and then one wall is left out: a way in)
			int open = r.Next(VarunaWalls.Length + 2);
			for (int i = 0; i < VarunaWalls.Length; i++)
			{
				if (i == open) continue;
				Vector2 p = at(VarunaWalls[i].x, VarunaWalls[i].y);
				if (k.Add("VP_MotherlodeBuildingPieces_Wall", new Vector3(p.x, low, p.y), yaw + VarunaWalls[i].z, null, 0f) != null) n++;
			}
			if (Stand(k, "VP_ElevatorShaft", at(half + 5f, Rf(r, -20f, 20f)), yaw + 90f) != null) n++;
			// (the crane's pivot is its foot: on the top floor, its arm out over the side)
			Vector2 cr = at(Rf(r, -12f, 12f), Rf(r, -12f, 12f));
			if (k.Add("VP_ApexCrane", new Vector3(cr.x, roof - BottomOf("VP_ApexCrane") + 0.05f, cr.y), Rf(r, 0f, 360f), null, 0f) != null) n++;
			string[] heaps = { "VP_RubblePile_Large01", "VP_RubblePile_Medium01", "VP_RubblePile_Medium02", "VP_BrickStack01", "VP_BrickStack02" };
			// (on the ground floor's deck, 0.54 m under the top of its mesh box (its rim): the top floor is open where they
			// fell, and set on the box's top they stood in the air - CIGroundIslands, Varuna Point Remade, 2026-10-20)
			for (int i = 0; i < 8; i++)
			{
				Vector2 p = at(Rf(r, -half + 12f, half - 12f), Rf(r, -half + 12f, half - 12f));
				if ((p - cr).magnitude < 12f) continue;
				string h = heaps[r.Next(heaps.Length)];
				if (Centred(k, h, p, Rf(r, 0f, 360f), top - VarunaRim - BottomOf(h) - 0.05f) != null) n++;
			}
			if (Stand(k, "VP_Excavator", at(-half - 6f, Rf(r, -25f, 25f)), yaw + Rf(r, -30f, 30f)) != null) n++;
			return n + " of Varuna Point's motherlode pieces: two floors on four pillars inside its walls, " + (half * 2f).ToString("F0") + " m wide";
		}

		#endregion

		#region Balboa: giant firs and boulders

		static readonly string[] BalboaLookKit = { "FirTree_Big1_LodGroup", "FirTree_Big2_LodGroup", "BigRock_1", "BigRock_Low2_Sand" };

		static string BalboaGiants(MapKit k, IslandGenSettings s, System.Random r)
		{
			float area = Mathf.PI * s.Radius * s.Radius;
			int firs = Mathf.Clamp(Mathf.RoundToInt(area / 6000f), 2, 8), rocks = Mathf.Clamp(Mathf.RoundToInt(area / 15000f), 1, 4), putFirs = 0, putRocks = 0;
			for (int i = 0; i < firs; i++)
			{
				Vector2? p = FindR(k, r, k.Mid, s.Radius * 0.8f, (above, slope) => above > 6f && slope < 24f, 22f);
				if (!p.HasValue) break;
				k.Clear(p.Value, 6f);
				Stand(k, Pick(r, "FirTree_Big1_LodGroup", "FirTree_Big2_LodGroup"), p.Value, Rf(r, 0f, 360f), 1.5f);
				putFirs++;
			}
			for (int i = 0; i < rocks; i++)
			{
				Vector2? p = FindR(k, r, k.Mid, s.Radius * 0.95f, (above, slope) => above > 0.5f && slope > 8f && slope < 35f, 30f);
				if (!p.HasValue) break;
				k.Clear(p.Value, 12f);
				string name = r.NextDouble() < 0.6 ? "BigRock_1" : "BigRock_Low2_Sand";
				Stand(k, name, p.Value, Rf(r, 0f, 360f), name == "BigRock_1" ? 8f : 3f);
				putRocks++;
			}
			return putFirs + putRocks > 0 ? "Balboa's " + putFirs + " giant fir(s) and " + putRocks + " boulder(s)" : null;
		}

		#endregion

		#region Radio Tower: low rocks, concrete footings

		static readonly string[] RadioLookKit = { "RT_GroundPillar", "RT_GroundPillarCap", "RT_SatteliteDisc", "RT_PlasticBoat" };

		static void RadioShape(RaftIsland i, IslandGenSettings s, bool variation)
		{
			// (Raft's own: a few low rocks just out of the sea under the tower's platforms - not a peak)
			s.Source = "";
			s.Shape = IslandShapes.Round;
			s.Radius = Mathf.Clamp(i.Radius, 25f, 50f);
			s.Height = 3f;
			s.Peaks = 3;
			s.PeakShape = 0.5f;
			s.Roughness = 0.6f;
			s.Cliffs = 0.3f;
			s.Stretch = 1f;
			// (a wide shallow shelf round them for the tower's legs; the rocks bare, as Raft's are)
			s.Shelf = Mathf.Max(s.Shelf, 0.6f);
			s.Trees = 0f;
			s.Bushes = 0f;
			s.Harvest = 0f;
			s.Rocks = Mathf.Min(s.Rocks, 0.08f);
			s.BeachThings = Mathf.Min(s.BeachThings, 0.1f);
		}

		static string RadioRocks(MapKit k, IslandGenSettings s, System.Random r)
		{
			// (the footings away from the top: a design built later on the top clears what stands there)
			Vector2 top = k.Highest(k.Mid, s.Radius * 0.6f);
			int want = Mathf.Clamp(Mathf.RoundToInt(s.Radius / 7f), 4, 7), feet = 0;
			for (int i = 0; i < want * 8 && feet < want; i++)
			{
				Vector2? p = FindR(k, r, k.Mid, s.Radius, (above, slope) => above > 0.3f && slope < 25f, 9f);
				if (!p.HasValue) break;
				if (Vector2.Distance(p.Value, top) < 15f) continue;
				k.Clear(p.Value, 3f);
				Stand(k, Pick(r, "RT_GroundPillar", "RT_GroundPillarCap"), p.Value, 90f * r.Next(4) + Rf(r, -5f, 5f), 1.2f);
				feet++;
			}
			// (the dish: RadioDish, after the designs - a design's tower on the top cleared it, X7 2026-10-17)
			Vector2? beach = FindR(k, r, k.Mid, s.Radius * 1.1f, (above, slope) => above > 0.1f && above < 0.9f && slope < 12f, 6f);
			if (beach.HasValue) { k.Clear(beach.Value, 3f); Stand(k, "RT_PlasticBoat", beach.Value, Rf(r, 0f, 360f), 0.15f); }
			return feet + (beach.HasValue ? 1 : 0) > 0 ? "the Radio Tower's " + feet + " concrete footing(s)" + (beach.HasValue ? ", a plastic boat on the beach" : "") : null;
		}

		/// <summary>The satellite dish on the top, or (a design stands there) on the highest free rock left.</summary>
		static string RadioDish(MapKit k, IslandGenSettings s, System.Random r)
		{
			Vector2 top = k.Highest(k.Mid, s.Radius * 0.6f);
			// (only where the ground under its bowl is near level: on a sharp peak it was set down by its lowest corner and the
			// peak stood 2 m through it - Radio Tower Remade, the ground check, 2026-10-20)
			Vector2? at = k.Ground(top) > k.Sea + 1f && k.IsFree(top, 6f) && GroundSpread(k, "RT_SatteliteDisc", top) < 0.8f ? top : (Vector2?)null;
			if (!at.HasValue)
			{
				float best = float.MinValue;
				for (int i = 0; i < 12; i++)
				{
					Vector2? p = FindR(k, r, k.Mid, s.Radius, (above, slope) => above > 1f && slope < 22f, 6f);
					if (p.HasValue && GroundSpread(k, "RT_SatteliteDisc", p.Value) < 0.8f && k.Ground(p.Value) > best) { best = k.Ground(p.Value); at = p; }
				}
			}
			if (!at.HasValue) return null;
			k.Clear(at.Value, 3f);
			Stand(k, "RT_SatteliteDisc", at.Value, Rf(r, 0f, 360f), 0.2f);
			return "the Radio Tower's satellite dish";
		}

		#endregion

		#region The Vasagatan: the liner's furniture washed up

		static readonly string[] VasagatanLookKit = { "VG_LoungeCouch", "VG_LoungeCouchCornerL", "VG_LoungeTable", "VG_LoungeChair", "VG_LoungeBarStool", "VG_Safe" };

		/// <summary>
		/// None of the liner's hull is a piece the editor can place: its lounge furniture washed up on the beach in a few
		/// heaps (a couch or corner couch, a table, chairs and stools round it), the safe on its own further up.
		/// </summary>
		static string VasagatanBeach(MapKit k, IslandGenSettings s, System.Random r)
		{
			int heaps = Mathf.Clamp(Mathf.RoundToInt(s.Radius / 18f), 2, 5), put = 0, made = 0;
			for (int h = 0; h < heaps; h++)
			{
				Vector2? p = FindR(k, r, k.Mid, s.Radius * 1.05f, (above, slope) => above > 0.3f && above < 2.5f && slope < 10f, 14f);
				if (!p.HasValue) break;
				Vector2 c = p.Value;
				k.Clear(c, 4f);
				Stand(k, r.NextDouble() < 0.5 ? "VG_LoungeCouch" : "VG_LoungeCouchCornerL", c + new Vector2(Rf(r, -1.5f, 1.5f), Rf(r, -1.5f, 1.5f)), Rf(r, 0f, 360f), 0.15f);
				Stand(k, "VG_LoungeTable", c + new Vector2(Rf(r, -2.5f, 2.5f), Rf(r, -2.5f, 2.5f)), Rf(r, 0f, 360f), 0.1f);
				put += 2;
				int seats = 1 + r.Next(3);
				for (int q = 0; q < seats; q++)
				{
					float a = Rf(r, 0f, Mathf.PI * 2f), d = Rf(r, 2.5f, 4f);
					Stand(k, r.NextDouble() < 0.6 ? "VG_LoungeChair" : "VG_LoungeBarStool", c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, Rf(r, 0f, 360f), 0.08f);
					put++;
				}
				made++;
			}
			Vector2? safe = FindR(k, r, k.Mid, s.Radius * 0.9f, (above, slope) => above > 1f && slope < 12f, 10f);
			if (safe.HasValue) { k.Clear(safe.Value, 2f); Stand(k, "VG_Safe", safe.Value, Rf(r, 0f, 360f), 0.1f); put++; }
			return put > 0 ? "the Vasagatan's furniture washed up: " + made + " heap(s) on the beach" + (safe.HasValue ? ", its safe" : "") : null;
		}

		#endregion

		#region Temperance: big rocks, the antenna, an igloo

		static readonly string[] TemperanceLookKit = { "TP_BigRock04_Unclimbable", "TP_TelephoneAntenna_NoQuestItem", "TP_Igloo_Large_1" };

		static string TemperanceRocks(MapKit k, IslandGenSettings s, System.Random r)
		{
			float area = Mathf.PI * s.Radius * s.Radius;
			int rocks = Mathf.Clamp(Mathf.RoundToInt(area / 12000f), 2, 6), put = 0;
			for (int i = 0; i < rocks; i++)
			{
				Vector2? p = FindR(k, r, k.Mid, s.Radius * 0.9f, (above, slope) => above > 1f && slope < 30f, 26f);
				if (!p.HasValue) break;
				k.Clear(p.Value, 12f);
				Stand(k, "TP_BigRock04_Unclimbable", p.Value, Rf(r, 0f, 360f), 6f);
				put++;
			}
			Vector2 top = k.Highest(k.Mid, s.Radius * 0.6f);
			bool antenna = k.Ground(top) > k.Sea + 3f && k.IsFree(top, 10f);
			if (antenna) { k.Clear(top, 5f); Foot(k, new Frame(top, 0f, k.Ground(top)), "TP_TelephoneAntenna_NoQuestItem", 0f, 0f, Rf(r, 0f, 360f)); }
			Vector2? flat = FindR(k, r, k.Mid, s.Radius * 0.7f, (above, slope) => above > 2f && slope < 8f, 20f);
			if (flat.HasValue) { k.Clear(flat.Value, 12f); Stand(k, "TP_Igloo_Large_1", flat.Value, Rf(r, 0f, 360f), 0.3f); }
			return "Temperance's " + put + " big rock(s)" + (antenna ? ", the antenna on the top" : "") + (flat.HasValue ? ", a large igloo" : "");
		}

		#endregion
	}
}
