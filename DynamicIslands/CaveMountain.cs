using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's cave pieces (Balboa's dens, the tunnels) are rock shells seen from inside only: from outside you look through
	/// their sides and under their thin edges, and they hang over sloping ground. In a world, an island with cave pieces gets
	/// one rock mountain over them (AU83): a surface draped over the shells' roofs that slopes down to the ground, walled where
	/// it is cut off, with the passages' open ends kept clear and a skirt under their floors down to the ground.
	/// </summary>
	public class CaveMountain : MonoBehaviour
	{
		/// <summary>Name of the mountain's object (under the island).</summary>
		public const string SkinName = "CaveSkin";
		/// <summary>Grid step, how steep the sides fall (m down per m out), how far they reach, and how far above the roofs.</summary>
		const float Cell = 1.25f, Slope = 1.5f, Reach = 16f, Over = 0.3f;

		readonly List<Transform> caves = new List<Transform>();
		readonly List<PropInfo> infos = new List<PropInfo>();
		float due = -1f;
		GameObject skin;

		/// <summary>A cave piece came and the mountain isn't (re)built yet (the tests wait for it instead of a fixed wait).</summary>
		public bool Pending { get { return due >= 0f; } }

		/// <summary>A cave piece spawned in a world under this island: (re)build the island's mountain once the island is all there.</summary>
		public static void Add(Transform island, GameObject piece, string name)
		{
			if (island == null || piece == null) return;
			if (!Wants(name)) return;
			PropInfo p = RaftProps.Get(name);
			CaveMountain m = island.GetComponent<CaveMountain>() ?? island.gameObject.AddComponent<CaveMountain>();
			m.caves.Add(piece.transform);
			m.infos.Add(p);
			m.due = Time.realtimeSinceStartup + 1f;
		}

		/// <summary>A cave piece that needs a mountain over it (the Vines cave is a whole rock with its own outside already).</summary>
		public static bool Wants(string name)
		{
			PropInfo p = RaftProps.Get(name);
			return p != null && p.IsCave && name != "BalboaCave_Vines";
		}

		/// <summary>Tests: a map of the last build (R roof, s side, c a way in, - cut off out past an open end, g lower than the ground, . nothing), one line per row.</summary>
		public string LastMap;

		/// <summary>Tests: builds it again now.</summary>
		public void Rebuild() { due = -1f; Build(); }

		/// <summary>
		/// Tests: every open end's way in, looked along from 5 m out to past where its roof ends at knee, chest and head height over
		/// its floor - where the mountain is in the way (a play walking in stuck on it - AU83). Empty: all clear.
		/// </summary>
		public List<string> BlockedWays()
		{
			var bad = new List<string>();
			Collider mine = skin != null ? skin.GetComponent<Collider>() : null;
			if (mine == null) return bad;
			Physics.SyncTransforms();
			foreach (Mouth m in lastMouths)
				for (int end = 0; end < 2; end++)
				{
					if (end == 0 ? !m.Plus : !m.Minus) continue;
					float dir = end == 0 ? 1f : -1f, start = end == 0 ? m.StartPlus : m.StartMinus;
					foreach (float up in new[] { 0.5f, 1.2f, 1.9f })
						foreach (float s in new[] { 0f, -m.HalfWidth * 0.4f, m.HalfWidth * 0.4f })
						{
							Vector3 from = m.Pos + m.Along * (m.InsideAlong + dir * (start + 5f)) + m.Side * (m.InsideSide + s);
							from.y = m.FloorY + up;
							RaycastHit hit;
							if (mine.Raycast(new Ray(from, -dir * m.Along), out hit, 6f))
								bad.Add(m.Name + (end == 0 ? " +" : " -") + " end, " + up.ToString("0.0") + " m over its floor, " + s.ToString("0.0") + " m aside: the hill " + hit.distance.ToString("0.0") + " m in");
						}
					// Out in front beside the way in: no hill over the ground there (it spread on over the ledge and the paths - Sundown
					// Canyons, the Iron Gate); over the cave's own rock, or another cave's hill reaching there, is all right
					foreach (float f in new[] { 1.5f, 3f, 5f, 8f })
						foreach (float s in new[] { 1f, 4f, 8f, -1f, -4f, -8f })
						{
							float aside = s + Mathf.Sign(s) * m.HalfWidth;
							Vector3 at = m.Pos + m.Along * (m.InsideAlong + dir * (start + f)) + m.Side * (m.InsideSide + aside);
							if (OtherHill(at)) continue;
							RaycastHit[] hits = UnderSkin(at, m.FloorY + 40f, mine);
							if (hits == null || hits[0].point.y - hits[1].point.y <= 0.6f) continue;
							// (over the cave's own rock - a shell's flank out past its roof - or at its edge, within a step: the hill
							// covering it, not the ground; the generator's dead-end caves)
							bool rock = false;
							foreach (Vector3 by in new[] { Vector3.zero, Vector3.right * Cell, Vector3.left * Cell, Vector3.forward * Cell, Vector3.back * Cell })
							{
								RaycastHit[] near = by == Vector3.zero ? hits : UnderSkin(at + by, m.FloorY + 40f, mine);
								if (near != null && OwnRock(near[1].collider)) { rock = true; break; }
							}
							if (rock) continue;
							bad.Add(m.Name + (end == 0 ? " +" : " -") + " end: hill in front, " + f.ToString("0.0") + " m out, " + aside.ToString("0.0") + " m aside, " + (hits[0].point.y - hits[1].point.y).ToString("0.0") + " m over the " + hits[1].collider.name);
						}
				}
			return bad;
		}

		bool[,] otherHill; // (last build: out past an open end, where another cave's hill reaches)
		float hillX0, hillZ0;

		/// <summary>What a ray down at a spot hits, highest first, when the hill is the top and something is under it; else null.</summary>
		static RaycastHit[] UnderSkin(Vector3 at, float fromY, Collider skin)
		{
			RaycastHit[] hits = Physics.RaycastAll(new Vector3(at.x, fromY, at.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
			System.Array.Sort(hits, (x, y) => y.point.y.CompareTo(x.point.y));
			return hits.Length >= 2 && hits[0].collider == skin ? hits : null;
		}

		bool OwnRock(Collider c)
		{
			foreach (Transform t in caves)
				if (t != null && c.transform.IsChildOf(t)) return true;
			return false;
		}

		bool OtherHill(Vector3 at)
		{
			if (otherHill == null) return false;
			int i = Mathf.RoundToInt((at.x - hillX0) / Cell), k = Mathf.RoundToInt((at.z - hillZ0) / Cell);
			return i >= 0 && k >= 0 && i < otherHill.GetLength(0) && k < otherHill.GetLength(1) && otherHill[i, k];
		}

		void Update()
		{
			if (due < 0f || Time.realtimeSinceStartup < due) return;
			due = -1f;
			try { Build(); }
			catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Cave mountain: " + e.Message); }
		}

		/// <summary>One cave piece's open ends: where the mountain must leave the way in clear.</summary>
		struct Mouth { public Vector3 Pos, Along, Side; public float InsideAlong, InsideSide, HalfWidth, FloorY, StartPlus, StartMinus; public bool Plus, Minus; public string Name; }
		readonly List<Mouth> lastMouths = new List<Mouth>();

		void Build()
		{
			if (skin != null) { DestroyImmediate(skin); skin = null; } // (its collider must not count as ground)
			var cols = new HashSet<Collider>();
			var floorOf = new Dictionary<Collider, float>();
			var ownerOf = new Dictionary<Collider, int>(); // (which cave piece: its mouth's index)
			var mouths = new List<Mouth>();
			Bounds b = new Bounds();
			bool any = false;
			Renderer look = null;
			float lookSize = 0f;
			for (int i = 0; i < caves.Count; i++)
			{
				Transform t = caves[i];
				if (t == null) continue;
				PropInfo p = infos[i];
				// (the piece's measures at its scale: a cave set bigger or smaller kept its floor, inside point and way in's width at
				// the measured size. Measured at the scale it spawns with - its catalog copy's, 3.008 for the bear den - so only the
				// scale over that one counts: by its whole scale the den's floor went 9 m under its real one, its open floor counted
				// as roof and its inside point lay 8 m out past its mouth, and the hill ran out over the ground before it - Stormwing's
				// strip, Balboa Remade's path, f72b954)
				Vector3 sc = OverMeasured(t, p.Name);
				float floorY = t.position.y + p.Floor * sc.y;
				foreach (Collider c in t.GetComponentsInChildren<Collider>())
					if (c.enabled && !c.isTrigger) { cols.Add(c); floorOf[c] = floorY; ownerOf[c] = mouths.Count; }
				foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
				{
					if (!r.enabled || !(r is MeshRenderer)) continue;
					if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
					float s = r.bounds.size.x * r.bounds.size.z;
					if (s > lookSize && r.sharedMaterial != null) { lookSize = s; look = r; }
				}
				Vector3 along = t.rotation * (p.Axis == 0 ? Vector3.right : Vector3.forward);
				along.y = 0f; along.Normalize();
				Vector3 side = new Vector3(along.z, 0f, -along.x);
				Vector3 inside = t.position + t.rotation * Vector3.Scale(p.Inside, sc);
				mouths.Add(new Mouth
				{
					Pos = t.position, Along = along, Side = side,
					InsideAlong = Vector3.Dot(inside - t.position, along), InsideSide = Vector3.Dot(inside - t.position, side),
					HalfWidth = p.Width * (p.Axis == 0 ? sc.z : sc.x) / 2f + 1f, FloorY = floorY, Plus = p.OpenPlus, Minus = p.OpenMinus, Name = t.name
				});
			}
			caves.RemoveAll(t => t == null);
			if (!any || look == null || cols.Count == 0) return;

			Physics.SyncTransforms();
			int nx = Mathf.CeilToInt((b.size.x + 2f * Reach) / Cell) + 1, nz = Mathf.CeilToInt((b.size.z + 2f * Reach) / Cell) + 1;
			float x0 = b.min.x - Reach, z0 = b.min.z - Reach, rayTop = b.max.y + 5f, rayLen = b.size.y + 60f;
			var top = new float[nx, nz];       // the shells' roofs (NaN: none)
			var ground = new float[nx, nz];    // what is under them otherwise
			var groundBy = new string[nx, nz]; // (tests: what that is)
			var corridor = new float[nx, nz];  // a way in: the floor height there (NaN: not one)
			var rimY = new float[nx, nz];      // a shell's low edge over lower ground, not a roof (NaN: none)
			var floorAt = new float[nx, nz];   // the shell's floor (its highest point under the roof line; NaN: none)
			var mouthAt = new float[nx, nz];   // in front of an open end, as wide as the passage: its floor height (NaN: not)
			var own = new int[nx, nz];         // whose roof or low edge it is (-1: none)
			int groundMask = (int)LayerMasks.MASK_GroundMask_NonRaft | (1 << IslandSpawner.TerrainLayer);
			bool backfaces = Physics.queriesHitBackfaces;
			Physics.queriesHitBackfaces = true; // (the shells face inwards: from above, a roof is its back)
			try
			{
				for (int i = 0; i < nx; i++)
					for (int k = 0; k < nz; k++)
					{
						Vector3 at = new Vector3(x0 + i * Cell, rayTop, z0 + k * Cell);
						float roof = float.NaN, low = float.NaN, gr = float.NegativeInfinity;
						int roofBy = -1, lowBy = -1;
						foreach (RaycastHit h in Physics.RaycastAll(at, Vector3.down, rayLen, ~0, QueryTriggerInteraction.Ignore))
						{
							float f;
							if (floorOf.TryGetValue(h.collider, out f))
							{
								// (only a roof counts: a floor open to the sky is the way in)
								if (h.point.y > f + 1.5f && !(h.point.y <= roof)) { roof = h.point.y; roofBy = ownerOf[h.collider]; }
								else if (!(h.point.y <= low)) { low = h.point.y; lowBy = ownerOf[h.collider]; } // (the shell below its floor too: Balboa's den goes on 11 m under it)
							}
							else if ((groundMask & (1 << h.collider.gameObject.layer)) != 0 && h.point.y > gr) { gr = h.point.y; groundBy[i, k] = h.collider.name; }
						}
						top[i, k] = roof;
						floorAt[i, k] = low;
						ground[i, k] = float.IsInfinity(gr) ? b.min.y - 2f : gr;
						// (a shell's edge under the roof line hung over the ground beside it: a 1.5 m slot into the den - AU83)
						rimY[i, k] = float.IsNaN(roof) && low > ground[i, k] + 0.3f ? low : float.NaN;
						own[i, k] = !float.IsNaN(roof) ? roofBy : lowBy;
					}
			}
			finally { Physics.queriesHitBackfaces = backfaces; }
			// The ways in: from where the roof ends along the passage, out (not from the inside point: a stretch of the den
			// with no roof over it was left open, and the den showed through the hill from above - AU83)
			var startPlus = new float[mouths.Count];
			var startMinus = new float[mouths.Count];
			for (int j = 0; j < mouths.Count; j++)
			{
				startPlus[j] = RoofEnd(mouths[j], 1f, top, x0, z0, nx, nz);
				startMinus[j] = RoofEnd(mouths[j], -1f, top, x0, z0, nx, nz);
			}
			// How far out past each cave's open ends (m past where its roof ends; under 0.5: not out past them). The hill of a
			// cave's roof stops there, a face of rock across its front: it spread on over the ledge and the paths before the
			// mouth, beside the way in (Sundown Canyons' switchbacks, the Iron Gate's shoulder - AU83)
			var ahead = new float[mouths.Count][,];
			for (int j = 0; j < mouths.Count; j++) ahead[j] = new float[nx, nz];
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
				{
					corridor[i, k] = float.NaN;
					mouthAt[i, k] = float.NaN;
					Vector3 at = new Vector3(x0 + i * Cell, 0f, z0 + k * Cell);
					for (int j = 0; j < mouths.Count; j++)
					{
						float a = Vector3.Dot(at - mouths[j].Pos, mouths[j].Along) - mouths[j].InsideAlong;
						ahead[j][i, k] = Mathf.Max(mouths[j].Plus ? a - startPlus[j] : float.NegativeInfinity, mouths[j].Minus ? -a - startMinus[j] : float.NegativeInfinity);
					}
					for (int j = 0; j < mouths.Count; j++)
					{
						Mouth m = mouths[j];
						Vector3 d = at - m.Pos;
						float a = Vector3.Dot(d, m.Along) - m.InsideAlong, s = Vector3.Dot(d, m.Side) - m.InsideSide;
						// (between the inside point and the corridor, as wide as the passage itself: where the roof ends unevenly,
						// a square with no roof there was hill with a wall down to the ground across the way in - Ranger's Rest)
						if (Mathf.Abs(s) < m.HalfWidth - 1f && ((m.Plus && a > 0f) || (m.Minus && a < 0f)) && float.IsNaN(mouthAt[i, k])) mouthAt[i, k] = m.FloorY;
						if (!float.IsNaN(top[i, k])) continue;
						if (Mathf.Abs(s) < m.HalfWidth && ((m.Plus && a > startPlus[j]) || (m.Minus && a < -startMinus[j]))) { corridor[i, k] = m.FloorY; break; }
					}
				}
			lastMouths.Clear();
			for (int j = 0; j < mouths.Count; j++) { Mouth m = mouths[j]; m.StartPlus = startPlus[j]; m.StartMinus = startMinus[j]; lastMouths.Add(m); }

			// What the surface must stay over: the highest roof or low edge in the square and the ones round it (a shell's edge
			// between two samples stuck out of a surface that fell away from the last roof sampled: a 2.2 m slot under it - AU83)
			// (not out past a cave's open end from the roof beside it: a square of hill before the mouth)
			var cover = new float[nx, nz];
			var coverBy = new int[nx, nz];
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
				{
					float c0 = float.NaN;
					int by = -1;
					for (int a = Mathf.Max(i - 1, 0); a <= Mathf.Min(i + 1, nx - 1); a++)
						for (int c = Mathf.Max(k - 1, 0); c <= Mathf.Min(k + 1, nz - 1); c++)
						{
							float y = float.IsNaN(top[a, c]) ? rimY[a, c] : top[a, c];
							if (float.IsNaN(y) || (y <= c0)) continue;
							if ((a != i || c != k) && own[a, c] >= 0 && ahead[own[a, c]][i, k] > 0.5f) continue;
							c0 = y; by = own[a, c];
						}
					cover[i, k] = c0;
					coverBy[i, k] = by;
				}
			// The surface: over a roof, the highest roof around it; elsewhere falling away from the roofs nearby
			var height = new float[nx, nz];
			var on = new bool[nx, nz];
			int reach = Mathf.CeilToInt(Reach / Cell);
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++) height[i, k] = float.NegativeInfinity;
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
				{
					if (float.IsNaN(cover[i, k]) || !float.IsNaN(corridor[i, k])) continue;
					float peak = cover[i, k] + Over;
					float[,] past = coverBy[i, k] >= 0 ? ahead[coverBy[i, k]] : null;
					for (int di = -reach; di <= reach; di++)
						for (int dk = -reach; dk <= reach; dk++)
						{
							int a = i + di, c = k + dk;
							if (a < 0 || c < 0 || a >= nx || c >= nz) continue;
							if (past != null && past[a, c] > 0.5f && (di != 0 || dk != 0)) continue;
							float h = peak - Slope * Cell * Mathf.Sqrt(di * di + dk * dk);
							if (h > height[a, c]) height[a, c] = h;
						}
				}
			// (rounded off, and a little uneven: a hill, not cones; never below a roof)
			var raw = (float[,])height.Clone();
			for (int pass = 0; pass < 2; pass++)
			{
				var soft = new float[nx, nz];
				for (int i = 0; i < nx; i++)
					for (int k = 0; k < nz; k++)
					{
						float sum = 0f; int n = 0;
						for (int di = -2; di <= 2; di++)
							for (int dk = -2; dk <= 2; dk++)
							{
								int a = Mathf.Clamp(i + di, 0, nx - 1), c = Mathf.Clamp(k + dk, 0, nz - 1);
								sum += Mathf.Max(height[a, c], ground[a, c] - 2f); n++;
							}
						soft[i, k] = sum / n;
					}
				height = soft;
			}
			// (out past an open end with no rock of its own and no other hill there: none - the rounding had spread the hill
			// on over it again; a face down to the ground stands at the cut)
			var cut = new bool[nx, nz];
			otherHill = new bool[nx, nz]; hillX0 = x0; hillZ0 = z0;
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
				{
					bool past = false;
					for (int j = 0; j < mouths.Count && !past; j++) past = ahead[j][i, k] > 0.5f;
					cut[i, k] = past && float.IsNaN(cover[i, k]) && !(raw[i, k] > ground[i, k] - 0.6f);
					otherHill[i, k] = past && float.IsNaN(cover[i, k]) && !cut[i, k];
				}
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
				{
					if (cut[i, k]) { height[i, k] = ground[i, k] - 3.5f; on[i, k] = false; continue; }
					float x = x0 + i * Cell, z = z0 + k * Cell;
					float rough = Mathf.Clamp01((height[i, k] - ground[i, k]) / 3f); // (none at its foot: no bumps through the ground around)
					height[i, k] += rough * ((Mathf.PerlinNoise(x * 0.13f, z * 0.13f) - 0.5f) * 2.2f + (Mathf.PerlinNoise(x * 0.4f + 7f, z * 0.4f) - 0.5f) * 0.6f);
					if (!float.IsNaN(cover[i, k]) && float.IsNaN(corridor[i, k]) && height[i, k] < cover[i, k] + Over) height[i, k] = cover[i, k] + Over;
					// (its foot goes on a little under the ground: no edge to see where it meets it; and only where the hill itself
					// comes near the ground - the rounding alone lifted rock out of a pit or a dug stair far from the cave, Ashfall's crypt)
					on[i, k] = float.IsNaN(corridor[i, k]) && height[i, k] > ground[i, k] - 0.6f && raw[i, k] > ground[i, k] - 0.6f - 2f * Slope * Cell;
				}
			// (one more ring round it, sunk under the ground: its edge slopes into the ground, not a stepped wall over it)
			var rim = new List<int>();
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
				{
					if (on[i, k] || !float.IsNaN(corridor[i, k]) || cut[i, k]) continue;
					bool next = false;
					for (int di = -1; di <= 1 && !next; di++)
						for (int dk = -1; dk <= 1 && !next; dk++)
						{
							int a = i + di, c = k + dk;
							next = a >= 0 && c >= 0 && a < nx && c < nz && on[a, c];
						}
					if (next) rim.Add(i * nz + k);
				}
			foreach (int r in rim)
			{
				int i = r / nz, k = r % nz;
				height[i, k] = ground[i, k] - 0.8f;
				on[i, k] = true;
			}

			var map = new System.Text.StringBuilder();
			for (int k = nz - 1; k >= 0; k--)
			{
				for (int i = 0; i < nx; i++)
					map.Append(!float.IsNaN(corridor[i, k]) ? 'c' : cut[i, k] ? '-' : on[i, k] ? (!float.IsNaN(top[i, k]) ? 'R' : 's') : float.IsNegativeInfinity(height[i, k]) || height[i, k] < ground[i, k] - 3f ? '.' : 'g');
				map.Append('\n');
			}
			var tall = new Dictionary<string, int>();
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
					if (!on[i, k] && float.IsNaN(corridor[i, k]) && height[i, k] < ground[i, k] - 3f && groundBy[i, k] != null)
						tall[groundBy[i, k]] = (tall.ContainsKey(groundBy[i, k]) ? tall[groundBy[i, k]] : 0) + 1;
			LastMap = "higher ground: " + string.Join(", ", System.Linq.Enumerable.Select(tall, kv => kv.Key + " " + kv.Value)) + "\nx " + x0.ToString("0.0") + " z " + z0.ToString("0.0") + " step " + Cell + " (rows from +z)\n" + map;
			var verts = new List<Vector3>();
			var norms = new List<Vector3>();
			var uvs = new List<Vector2>();
			var tris = new List<int>();
			Vector3 origin = new Vector3(b.center.x, b.min.y, b.center.z);
			System.Func<int, int, Vector3> P = (i, k) => new Vector3(x0 + i * Cell, height[i, k], z0 + k * Cell);
			// Top: both sides (from inside a cave, a hole in its shell shows rock)
			for (int i = 0; i + 1 < nx; i++)
				for (int k = 0; k + 1 < nz; k++)
				{
					if (!on[i, k] || !on[i + 1, k] || !on[i, k + 1] || !on[i + 1, k + 1]) continue;
					Vector3 a = P(i, k), c = P(i + 1, k), d = P(i + 1, k + 1), e = P(i, k + 1);
					Vector3 n = Vector3.Cross(e - a, c - a).normalized;
					Quad(verts, norms, uvs, tris, origin, a, e, d, c, n, true);
				}
			// Walls where the surface is cut off: down to the ground; at a way in, only a skirt under its floor
			var arch = new Arch { Shells = cols, Box = b, Own = own, Ahead = ahead, Mouths = mouths, Top = top };
			bool backs = Physics.queriesHitBackfaces;
			try
			{
				for (int i = 0; i + 1 < nx; i++)
					for (int k = 0; k + 1 < nz; k++)
					{
						if (!on[i, k] || !on[i + 1, k] || !on[i, k + 1] || !on[i + 1, k + 1]) continue;
						Edge(i, k, i + 1, k, 0, -1, nx, nz, on, top, height, ground, corridor, mouthAt, floorAt, P, verts, norms, uvs, tris, origin, arch);
						Edge(i, k + 1, i + 1, k + 1, 0, 1, nx, nz, on, top, height, ground, corridor, mouthAt, floorAt, P, verts, norms, uvs, tris, origin, arch);
						Edge(i, k, i, k + 1, -1, 0, nx, nz, on, top, height, ground, corridor, mouthAt, floorAt, P, verts, norms, uvs, tris, origin, arch);
						Edge(i + 1, k, i + 1, k + 1, 1, 0, nx, nz, on, top, height, ground, corridor, mouthAt, floorAt, P, verts, norms, uvs, tris, origin, arch);
					}
			}
			finally { Physics.queriesHitBackfaces = backs; }
			if (tris.Count == 0) return;

			var mesh = new Mesh { name = SkinName };
			if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(verts);
			mesh.SetNormals(norms);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(tris, 0);
			mesh.RecalculateBounds();
			skin = new GameObject(SkinName);
			skin.layer = IslandSpawner.TerrainLayer;
			skin.transform.position = origin;
			skin.transform.SetParent(transform, true);
			// (drawn from both sides: from inside a cave, through a gap in its shell, the mountain's underside is rock too - X7)
			skin.AddComponent<MeshFilter>().sharedMesh = TwoSided(mesh, verts, norms, uvs, tris);
			var mr = skin.AddComponent<MeshRenderer>();
			// (the island's own biggest rock, so a snow island gets snowy rock; else Raft's big rock - the shells' own is mostly dirt)
			Renderer rockLook = IslandRock();
			if (rockLook == null)
			{
				GameObject rock = PlaceableCatalog.Get("BigRock_1");
				rockLook = rock != null ? rock.GetComponentInChildren<MeshRenderer>(true) : null;
			}
			mr.sharedMaterial = rockLook != null && rockLook.sharedMaterial != null ? rockLook.sharedMaterial : look.sharedMaterial;
			mr.shadowCastingMode = look.shadowCastingMode;
			mr.receiveShadows = look.receiveShadows;
			skin.AddComponent<MeshCollider>().sharedMesh = mesh;
			Debug.Log("[CUSTOM ISLANDS] Cave mountain over " + caves.Count + " cave piece(s): " + verts.Count + " points, " + mr.sharedMaterial.name);
		}

		/// <summary>A cave piece's scale over the one raft_props measured it at (its catalog copy's); 1 when that copy isn't loaded.</summary>
		internal static Vector3 OverMeasured(Transform t, string name)
		{
			GameObject proto = name != null ? PlaceableCatalog.Get(name) : null;
			if (proto == null) return Vector3.one;
			Vector3 s = t.lossyScale, m = proto.transform.localScale;
			return new Vector3(Mathf.Abs(m.x) > 1e-4f ? s.x / m.x : 1f, Mathf.Abs(m.y) > 1e-4f ? s.y / m.y : 1f, Mathf.Abs(m.z) > 1e-4f ? s.z / m.z : 1f);
		}

		/// <summary>The mountain's look: its faces again, turned the other way (its collider keeps the one side).</summary>
		static Mesh TwoSided(Mesh one, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
		{
			int n = verts.Count;
			var v2 = new List<Vector3>(verts);
			var n2 = new List<Vector3>(norms);
			var u2 = new List<Vector2>(uvs);
			var t2 = new List<int>(tris);
			v2.AddRange(verts);
			foreach (Vector3 x in norms) n2.Add(-x);
			u2.AddRange(uvs);
			for (int i = 0; i + 2 < tris.Count; i += 3) { t2.Add(tris[i] + n); t2.Add(tris[i + 2] + n); t2.Add(tris[i + 1] + n); }
			var mesh = new Mesh { name = one.name + " (both sides)" };
			if (v2.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(v2);
			mesh.SetNormals(n2);
			mesh.SetUVs(0, u2);
			mesh.SetTriangles(t2, 0);
			mesh.bounds = one.bounds;
			return mesh;
		}

		/// <summary>The biggest rock or boulder placed on this island (not a cave piece), or null.</summary>
		Renderer IslandRock()
		{
			Renderer best = null;
			float bestSize = 0f;
			foreach (Transform o in transform)
			{
				if (o == null || caves.Contains(o) || (skin != null && o == skin.transform)) continue;
				string n = o.name.ToLowerInvariant();
				if (!(n.Contains("rock") || n.Contains("boulder") || n.Contains("cliff")) || n.Contains("cave")) continue;
				foreach (Renderer r in o.GetComponentsInChildren<MeshRenderer>())
				{
					if (!r.enabled || r.sharedMaterial == null) continue;
					Vector3 z = r.bounds.size;
					float size = z.x * z.y * z.z;
					if (size > bestSize) { bestSize = size; best = r; }
				}
			}
			return best;
		}

		/// <summary>
		/// How far from a cave's inside point its roof goes along the passage (dir 1: its plus end, -1: its minus end): the last
		/// roof on the passage's middle or either side of it, past gaps in the roof of up to 5 m.
		/// </summary>
		static float RoofEnd(Mouth m, float dir, float[,] top, float x0, float z0, int nx, int nz)
		{
			float last = 0f;
			foreach (float s in new[] { 0f, -m.HalfWidth * 0.5f, m.HalfWidth * 0.5f })
			{
				float seen = 0f;
				for (float a = 0f; a < 80f; a += Cell * 0.5f)
				{
					Vector3 p = m.Pos + m.Along * (m.InsideAlong + dir * a) + m.Side * (m.InsideSide + s);
					int i = Mathf.RoundToInt((p.x - x0) / Cell), k = Mathf.RoundToInt((p.z - z0) / Cell);
					if (i < 0 || k < 0 || i >= nx || k >= nz) break;
					if (!float.IsNaN(top[i, k])) seen = a;
					else if (a - seen > 5f) break;
				}
				last = Mathf.Max(last, seen);
			}
			return last;
		}

		/// <summary>The edge (i1,k1)-(i2,k2) of a surface square; (di,dk) points to the square beside it.</summary>
		static void Edge(int i1, int k1, int i2, int k2, int di, int dk, int nx, int nz, bool[,] on, float[,] top, float[,] height,
			float[,] ground, float[,] corridor, float[,] mouthAt, float[,] floorAt, System.Func<int, int, Vector3> P, List<Vector3> verts,
			List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 origin, Arch arch)
		{
			int a1 = i1 + di, c1 = k1 + dk, a2 = i2 + di, c2 = k2 + dk;
			bool inside1 = a1 >= 0 && c1 >= 0 && a1 < nx && c1 < nz, inside2 = a2 >= 0 && c2 >= 0 && a2 < nx && c2 < nz;
			if (inside1 && inside2 && on[a1, c1] && on[a2, c2]) return; // the surface goes on
			Vector3 n = new Vector3(di, 0f, dk);
			float g1 = ground[i1, k1] - 0.3f, g2 = ground[i2, k2] - 0.3f;
			float way = float.NaN;
			if (inside1 && !float.IsNaN(corridor[a1, c1])) way = corridor[a1, c1];
			if (inside2 && !float.IsNaN(corridor[a2, c2])) way = float.IsNaN(way) ? corridor[a2, c2] : Mathf.Min(way, corridor[a2, c2]);
			Vector3 p1 = P(i1, k1), p2 = P(i2, k2);
			bool atWay = !float.IsNaN(way) && (!float.IsNaN(top[i1, k1]) || !float.IsNaN(top[i2, k2]) || !float.IsNaN(mouthAt[i1, k1]) || !float.IsNaN(mouthAt[i2, k2]));
			if (atWay)
			{
				// The open end of a cave: its own arch is the face; under its floor, down to the ground (no higher than the
				// floor there: a lip over it stopped the way in)
				float f = Mathf.Max(float.IsNaN(floorAt[i1, k1]) ? float.NegativeInfinity : floorAt[i1, k1], float.IsNaN(floorAt[i2, k2]) ? float.NegativeInfinity : floorAt[i2, k2]);
				if (f > way - 3f && f < way) way = f;
			}
			// (beside the way in, over the cave's own roof, its arch goes on: the face there is the cave's too - a wall down to the
			// ground stood just inside a mouth wider than the way in, Deepwatch Station's tunnel)
			float floorY = atWay ? way : arch.FaceOut(i1, k1, i2, k2, a1, c1, a2, c2, inside1, inside2, n);
			if (float.IsNaN(floorY))
			{
				if (p1.y <= g1 && p2.y <= g2) return;
				Band(p1, p2, Mathf.Min(g1, p1.y), Mathf.Min(g2, p2.y), p1.y, p2.y, n, verts, norms, uvs, tris, origin);
				return;
			}
			// In strips along it: where the cave's passage is, open from its floor to its roof (rock from the hill down to its
			// roof, and from its floor down to the ground); elsewhere a skirt under the way in, or the wall down to the ground.
			// (a skirt up to the passage's floor further in, and a wall at the way in's side, boxed in the mouth of a tunnel
			// that widens and dips at its ends - Deepwatch Station)
			float lo0, hi0, lo, hi;
			bool open0 = arch.Passage(p1, floorY, out lo0, out hi0);
			for (int s = 1; s <= Arch.Strips; s++)
			{
				float t0 = (s - 1) / (float)Arch.Strips, t = s / (float)Arch.Strips;
				Vector3 e0 = Vector3.Lerp(p1, p2, t0), e = Vector3.Lerp(p1, p2, t);
				float ga = Mathf.Lerp(g1, g2, t0), gb = Mathf.Lerp(g1, g2, t);
				bool open = arch.Passage(e, floorY, out lo, out hi);
				if (open || open0)
				{
					float l = open && open0 ? Mathf.Min(lo, lo0) : open ? lo : lo0, h = open && open0 ? Mathf.Max(hi, hi0) : open ? hi : hi0;
					// (at the way in itself no higher than its floor, and nothing over it - its own arch is the face there: the floor
					// and roof found in a bear cave's mouth stood 0.5-1.2 m into the way in, a step and a lintel across it on 15
					// islands' dens; only a mouth that dips under the way in's floor takes the skirt down with it)
					if (atWay) l = Mathf.Min(l, way);
					Band(e0, e, ga, gb, l, l, n, verts, norms, uvs, tris, origin);
					if (!atWay) Band(e0, e, h, h, e0.y, e.y, n, verts, norms, uvs, tris, origin);
				}
				else if (atWay)
				{
					if (Mathf.Min(ga, gb) <= way - 0.15f) Band(e0, e, Mathf.Min(ga, way), Mathf.Min(gb, way), way, way, n, verts, norms, uvs, tris, origin);
				}
				else Band(e0, e, Mathf.Min(ga, e0.y), Mathf.Min(gb, e.y), e0.y, e.y, n, verts, norms, uvs, tris, origin);
				open0 = open; lo0 = lo; hi0 = hi;
			}
		}

		/// <summary>A face up the edge a-b (x, z), from ya0/yb0 to ya1/yb1; none where it has no height.</summary>
		static void Band(Vector3 a, Vector3 b, float ya0, float yb0, float ya1, float yb1, Vector3 n, List<Vector3> verts,
			List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 origin)
		{
			if (ya1 <= ya0 + 0.01f && yb1 <= yb0 + 0.01f) return;
			ya0 = Mathf.Min(ya0, ya1); yb0 = Mathf.Min(yb0, yb1);
			Quad(verts, norms, uvs, tris, origin, new Vector3(a.x, ya0, a.z), new Vector3(a.x, ya1, a.z), new Vector3(b.x, yb1, b.z), new Vector3(b.x, yb0, b.z), n, true);
		}

		/// <summary>The caves' open ends where the hill's faces cross them.</summary>
		sealed class Arch
		{
			public const int Strips = 8; // (per edge of a square: 0.16 m)
			static readonly float[] Probe = { 0.6f, 1.6f, 2.6f };
			public HashSet<Collider> Shells;
			public Bounds Box;
			public int[,] Own;
			public float[][,] Ahead;
			public List<Mouth> Mouths;
			public float[,] Top;

			/// <summary>The floor of the cave whose open end the face crosses, over that cave's own roof (NaN: not one).</summary>
			public float FaceOut(int i1, int k1, int i2, int k2, int a1, int c1, int a2, int c2, bool in1, bool in2, Vector3 n)
			{
				int j = !float.IsNaN(Top[i1, k1]) ? Own[i1, k1] : !float.IsNaN(Top[i2, k2]) ? Own[i2, k2] : -1;
				if (j < 0 || j >= Mouths.Count) return float.NaN;
				if (!((in1 && Ahead[j][a1, c1] > 0f) || (in2 && Ahead[j][a2, c2] > 0f))) return float.NaN;
				return Mathf.Abs(Vector3.Dot(n, Mouths[j].Along)) < 0.5f ? float.NaN : Mouths[j].FloorY;
			}

			/// <summary>Whether the spot (x, z) is in a cave's passage just over that floor: from its floor (lo) to its roof (hi).</summary>
			public bool Passage(Vector3 at, float floorY, out float lo, out float hi)
			{
				lo = hi = 0f;
				foreach (float up in Probe)
				{
					Vector3 p = new Vector3(at.x, floorY + up, at.z);
					float under, over;
					if (p.y <= Box.min.y || p.y >= Box.max.y || !CaveGround.Inside(p, Shells, Box, out under, out over)) continue;
					lo = p.y - under; hi = p.y + over;
					return true;
				}
				return false;
			}
		}

		/// <summary>A four-sided face a-b-c-d with normal n (the other side too when both).</summary>
		static void Quad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 origin,
			Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, bool both)
		{
			for (int side = 0; side < (both ? 2 : 1); side++)
			{
				int s = verts.Count;
				Vector3 nn = side == 0 ? n : -n;
				foreach (Vector3 v in new[] { a, b, c, d })
				{
					verts.Add(v - origin);
					norms.Add(nn);
					// (mapped from the side it faces most, in world metres: the rock's texture lines up across squares)
					float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
					uvs.Add(ay >= ax && ay >= az ? new Vector2(v.x, v.z) / 14f : ax >= az ? new Vector2(v.z, v.y) / 14f : new Vector2(v.x, v.y) / 14f);
				}
				// (a-b-c-d winds so that n is its front; the back side winds the other way)
				Vector3 f = Vector3.Cross(b - a, c - a);
				bool front = Vector3.Dot(f, nn) > 0f;
				if (front) { tris.Add(s); tris.Add(s + 1); tris.Add(s + 2); tris.Add(s); tris.Add(s + 2); tris.Add(s + 3); }
				else { tris.Add(s); tris.Add(s + 2); tris.Add(s + 1); tris.Add(s); tris.Add(s + 3); tris.Add(s + 2); }
			}
		}
	}
}
