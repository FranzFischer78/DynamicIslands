using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The island's ground where it runs through a cave piece's rooms - the hill a cave was set into, the ground beside a
	/// passage - over the shell's floor and under its roof, is taken out (X7): every point of it in a room is let down under
	/// the room's floor, and the cells wholly in a room are cut away (terrain holes), so the room is the rock's own floor,
	/// walls and roof. Raft draws the ground from above only: from inside a room it lay across, it was a window out. In a world
	/// once the island's objects are there; in the editor whenever the caves or the ground over them have changed - there the
	/// ground's own heights are kept (put back before each new cut, and saved as they were).
	/// </summary>
	public class CaveGround : MonoBehaviour
	{
		/// <summary>How far under a room's floor the ground in it is let down (m).</summary>
		const float Under = 0.6f;
		/// <summary>A cell steep enough that its ground can run through a room with none of its corners in it (m from its lowest corner to its highest), and how finely one is looked over.</summary>
		const float Steep = 2f;
		const int Looks = 8;

		readonly List<Transform> caves = new List<Transform>();

		/// <summary>Tests: how many ground cells the last cut took out, and how many points of the ground it let down.</summary>
		public int LastCut, LastDug;

		/// <summary>A cave piece spawned in a world under this island's objects.</summary>
		public static void Add(Transform objects, GameObject piece, string name)
		{
			PropInfo p = RaftProps.Get(name ?? "");
			if (objects == null || piece == null || p == null || !p.IsCave) return;
			CaveGround g = objects.GetComponent<CaveGround>() ?? objects.gameObject.AddComponent<CaveGround>();
			g.caves.Add(piece.transform);
		}

		/// <summary>The island's objects all there and switched on (their colliders can be hit): the ground in its caves' rooms taken out.</summary>
		public static void Cut(Transform objects)
		{
			CaveGround g = objects != null ? objects.GetComponent<CaveGround>() : null;
			if (g == null) return;
			try { g.CutNow(); }
			catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Cave ground: " + e.Message); }
		}

		/// <summary>Cuts it again now (tests).</summary>
		public void CutNow()
		{
			caves.RemoveAll(t => t == null);
			LastCut = LastDug = 0;
			if (caves.Count == 0) return;
			Transform island = transform.parent != null ? transform.parent : transform;
			float started = Time.realtimeSinceStartup;
			foreach (Terrain land in island.GetComponentsInChildren<Terrain>())
			{
				int dug;
				LastCut += CutIn(land, caves, null, null, out dug);
				LastDug += dug;
			}
			if (LastCut + LastDug > 0)
				Debug.Log("[CUSTOM ISLANDS] '" + island.name + "': the ground taken out of " + caves.Count + " cave piece(s)' rooms (" + LastDug + " points let down, " + LastCut +
					" cells cut) in " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("F0") + " ms");
		}

		static float nextLook;
		static double lastLook = double.NaN;
		static bool changed;
		static Terrain lastLand;
		static readonly List<Vector2Int> editorCut = new List<Vector2Int>();
		// (the editor's ground let down: point (z * resolution + x) -> its own height, and the height it was let down to)
		static readonly Dictionary<int, Vector2> editorDug = new Dictionary<int, Vector2>();

		/// <summary>
		/// The editor: the same cut in the island being made, so its caves look there as they will in a world. The ground is
		/// sculpted and the caves moved at any time: once a second the caves' places and the ground over them are compared with
		/// the last look, and once they have stopped changing the cut is made again (the last one put back first). busy: a brush
		/// stroke under way.
		/// </summary>
		public static void EditorTick(Terrain land, bool busy)
		{
			if (land == null || busy || Time.realtimeSinceStartup < nextLook) return;
			nextLook = Time.realtimeSinceStartup + 1f;
			try
			{
				List<Transform> caves = EditorCaves();
				double look = Look(land, caves);
				if (land != lastLand || look != lastLook) { lastLook = look; changed = true; if (land != lastLand) { lastLand = land; editorCut.Clear(); editorDug.Clear(); } return; }
				if (!changed) return;
				changed = false;
				EditorCut(land, caves);
			}
			catch (System.Exception e) { changed = false; Debug.LogWarning("[CUSTOM ISLANDS] Cave ground (editor): " + e.Message); }
		}

		/// <summary>The editor's cut made now (tests). How many cells are cut; dug: how many points are let down.</summary>
		public static int EditorNow(Terrain land, out int dug)
		{
			dug = 0;
			if (land == null) return 0;
			if (land != lastLand) { lastLand = land; editorCut.Clear(); editorDug.Clear(); }
			changed = false;
			dug = EditorCut(land, EditorCaves());
			return editorCut.Count;
		}

		/// <summary>The editor's island was just replaced (a new one, one loaded): its heights are its own, the ones let down are forgotten; cut again soon.</summary>
		public static void EditorForget()
		{
			editorDug.Clear();
			lastLook = double.NaN;
		}

		/// <summary>The editor's island being saved: its heights (all of them, [z, x]) as they are without the cut - the ground's own.</summary>
		public static void EditorOwnHeights(Terrain land, float[,] heights)
		{
			if (land == null || land != lastLand || heights == null || editorDug.Count == 0) return;
			int res = heights.GetLength(0);
			foreach (KeyValuePair<int, Vector2> d in editorDug)
			{
				int z = d.Key / res, x = d.Key % res;
				if (z < res && x < res && Mathf.Abs(heights[z, x] - d.Value.y) < 1e-6f) heights[z, x] = d.Value.x;
			}
		}

		static List<Transform> EditorCaves()
		{
			var caves = new List<Transform>();
			GameObject placed = GameObject.Find("PlacedObjects");
			if (placed != null)
				foreach (EditorGameObject e in placed.GetComponentsInChildren<EditorGameObject>(false))
				{
					PropInfo p = RaftProps.Get(e.GameObjectName ?? "");
					if (p != null && p.IsCave) caves.Add(e.transform);
				}
			return caves;
		}

		/// <summary>The last cut put back (holes filled, the ground let down - and not sculpted since - at its own height), and made again. How many points are let down.</summary>
		static int EditorCut(Terrain land, List<Transform> caves)
		{
			TerrainData data = land.terrainData;
			int res = data.heightmapResolution, cells = res - 1;
			if (editorCut.Count > 0)
			{
				bool[,] solid = data.GetHoles(0, 0, cells, cells);
				foreach (Vector2Int c in editorCut)
					if (c.x < cells && c.y < cells) solid[c.y, c.x] = true;
				data.SetHoles(0, 0, solid);
				editorCut.Clear();
			}
			if (editorDug.Count > 0)
			{
				float[,] h = data.GetHeights(0, 0, res, res);
				int back = 0;
				foreach (KeyValuePair<int, Vector2> d in editorDug)
				{
					int z = d.Key / res, x = d.Key % res;
					if (z < res && x < res && Mathf.Abs(h[z, x] - d.Value.y) < 1e-6f) { h[z, x] = d.Value.x; back++; }
				}
				// (none of them still where they were let down: the island was replaced - they are another island's)
				if (back == 0) editorDug.Clear();
				else data.SetHeights(0, 0, h);
			}
			int dug = 0;
			if (caves.Count > 0) CutIn(land, caves, editorCut, editorDug, out dug);
			lastLook = Look(land, caves); // (as it is now, cut: the cut itself is no change)
			return dug;
		}

		/// <summary>The caves' places and the ground in their reach, as one number: changed when either has.</summary>
		static double Look(Terrain land, List<Transform> caves)
		{
			TerrainData data = land.terrainData;
			double look = caves.Count;
			for (int i = 0; i < caves.Count; i++)
			{
				Transform t = caves[i];
				Vector3 p = t.position, r = t.eulerAngles, s = t.lossyScale;
				look += (i + 1) * (p.x * 1.31 + p.y * 1.73 + p.z * 1.97 + r.x * 0.37 + r.y * 0.41 + r.z * 0.43 + s.x * 3.1 + s.y * 3.7 + s.z * 4.3);
				HashSet<Collider> own;
				Bounds b;
				int x0, x1, z0, z1;
				if (!Reach(land, t, out own, out b, out x0, out x1, out z0, out z1)) continue;
				float[,] h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
				double sum = 0;
				foreach (float v in h) sum += v;
				look += (i + 1) * sum * 7.0;
			}
			return look;
		}

		/// <summary>A cave piece's colliders, their bounds, and the ground points under them (false: none).</summary>
		static bool Reach(Terrain land, Transform cave, out HashSet<Collider> own, out Bounds b, out int x0, out int x1, out int z0, out int z1)
		{
			own = new HashSet<Collider>(cave.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger));
			b = new Bounds();
			x0 = x1 = z0 = z1 = 0;
			if (own.Count == 0) return false;
			b = own.First().bounds;
			foreach (Collider c in own) b.Encapsulate(c.bounds);
			TerrainData data = land.terrainData;
			int cells = data.heightmapResolution - 1;
			float sx = data.size.x / cells, sz = data.size.z / cells;
			Vector3 o = land.transform.position;
			x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - o.x) / sx)); x1 = Mathf.Min(cells, Mathf.CeilToInt((b.max.x - o.x) / sx));
			z0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - o.z) / sz)); z1 = Mathf.Min(cells, Mathf.CeilToInt((b.max.z - o.z) / sz));
			return x0 < x1 && z0 < z1;
		}

		/// <summary>
		/// Takes the ground of land out of the caves' rooms: every point of it in a room let down under the room's floor, and the
		/// cells wholly in rooms cut. cut: gets each cell cut (x, z); dugTo: gets each point let down (its own height, and the new
		/// one). How many cells are cut; dug: how many points let down.
		/// </summary>
		static int CutIn(Terrain land, List<Transform> caves, List<Vector2Int> cut, Dictionary<int, Vector2> dugTo, out int dug)
		{
			dug = 0;
			TerrainData data = land.terrainData;
			int res = data.heightmapResolution, cells = res - 1;
			float sx = data.size.x / cells, sz = data.size.z / cells, sy = data.size.y;
			Vector3 o = land.transform.position;
			bool backs0 = Physics.queriesHitBackfaces;
			Physics.SyncTransforms();
			// (each point of the ground: in one of the rooms - a piece's own roof over it and its floor under it, both seen
			// from inside - and the height of the floor under it)
			var inside = new bool[res, res];
			var floor = new Dictionary<int, float>();
			int lo = res, hi = -1, left = res, right = -1;
			try
			{
				foreach (Transform cave in caves)
				{
					HashSet<Collider> own;
					Bounds b;
					int x0, x1, z0, z1;
					if (cave == null || !Reach(land, cave, out own, out b, out x0, out x1, out z0, out z1)) continue;
					float[,] h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
					for (int z = z0; z <= z1; z++)
						for (int x = x0; x <= x1; x++)
						{
							if (inside[z, x]) continue;
							Vector3 p = new Vector3(o.x + x * sx, o.y + h[z - z0, x - x0] * sy, o.z + z * sz);
							float under, over;
							if (p.y <= b.min.y || p.y >= b.max.y || !Inside(p, own, b, out under, out over)) continue;
							inside[z, x] = true;
							floor[z * res + x] = p.y - under;
							lo = Mathf.Min(lo, z); hi = Mathf.Max(hi, z); left = Mathf.Min(left, x); right = Mathf.Max(right, x);
						}
				}
			}
			finally { Physics.queriesHitBackfaces = backs0; }
			if (hi >= 0)
			{
				// (each point in a room let down under its floor: the ground between it and one outside the room then runs down
				// into the rock, under the floor, seen from above - not across the room, seen from below - however few of a cell's
				// corners are in it)
				int w = right - left + 1, d = hi - lo + 1;
				float[,] heights = data.GetHeights(left, lo, w, d), own0 = dugTo != null ? (float[,])heights.Clone() : null;
				foreach (KeyValuePair<int, float> f in floor)
				{
					int z = f.Key / res, x = f.Key % res;
					float to = Mathf.Clamp01((f.Value - Under - o.y) / sy);
					if (to >= heights[z - lo, x - left]) continue;
					heights[z - lo, x - left] = to;
					dug++;
				}
				if (dug > 0)
				{
					data.SetHeights(left, lo, heights);
					if (dugTo != null)
					{
						float[,] now = data.GetHeights(left, lo, w, d); // (as kept: the heights are stored rounded)
						foreach (KeyValuePair<int, float> f in floor)
						{
							int z = f.Key / res, x = f.Key % res;
							if (now[z - lo, x - left] < own0[z - lo, x - left]) dugTo[f.Key] = new Vector2(own0[z - lo, x - left], now[z - lo, x - left]);
						}
					}
				}
			}
			// (and the cells whose ground still runs steeply through a room between their corners)
			var across = new List<Vector2Int>();
			try { Across(land, caves, inside, across); }
			finally { Physics.queriesHitBackfaces = backs0; }
			if (hi < 0 && across.Count == 0) return 0;
			// (a cell goes when all four of its corners are in a room: none is cut out past a wall, where it would open a gap
			// beside the rock)
			bool[,] solid = data.GetHoles(0, 0, cells, cells);
			int n = 0;
			for (int z = lo; z < hi; z++)
				for (int x = left; x < right; x++)
					if (solid[z, x] && inside[z, x] && inside[z, x + 1] && inside[z + 1, x] && inside[z + 1, x + 1])
					{
						solid[z, x] = false;
						n++;
						if (cut != null) cut.Add(new Vector2Int(x, z));
					}
			foreach (Vector2Int c in across)
				if (solid[c.y, c.x])
				{
					solid[c.y, c.x] = false;
					n++;
					if (cut != null) cut.Add(c);
				}
			if (n > 0) data.SetHoles(0, 0, solid);
			return n;
		}

		/// <summary>
		/// The cells whose ground runs steeply through a room between their corners, none of them in it - a slope from over a
		/// passage's roof down under its floor, where it ends in a hole in the ground (X7): from inside, seen from behind, a
		/// window out. Only where the room is under the cell's middle, so none opens a gap beside the rock.
		/// </summary>
		static void Across(Terrain land, List<Transform> caves, bool[,] inside, List<Vector2Int> found)
		{
			TerrainData data = land.terrainData;
			int cells = data.heightmapResolution - 1;
			float sx = data.size.x / cells, sz = data.size.z / cells, sy = data.size.y;
			Vector3 o = land.transform.position;
			var seen = new HashSet<int>();
			foreach (Transform cave in caves)
			{
				HashSet<Collider> own;
				Bounds b;
				int x0, x1, z0, z1;
				if (cave == null || !Reach(land, cave, out own, out b, out x0, out x1, out z0, out z1)) continue;
				float[,] h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
				for (int z = z0; z < z1; z++)
					for (int x = x0; x < x1; x++)
					{
						if (seen.Contains(z * cells + x) || (inside[z, x] && inside[z, x + 1] && inside[z + 1, x] && inside[z + 1, x + 1])) continue;
						float c00 = h[z - z0, x - x0], c10 = h[z - z0, x + 1 - x0], c01 = h[z + 1 - z0, x - x0], c11 = h[z + 1 - z0, x + 1 - x0];
						float top = o.y + Mathf.Max(Mathf.Max(c00, c10), Mathf.Max(c01, c11)) * sy, bottom = o.y + Mathf.Min(Mathf.Min(c00, c10), Mathf.Min(c01, c11)) * sy;
						if (top - bottom < Steep || top <= b.min.y || bottom >= b.max.y) continue;
						// (the room under the cell's middle, if any, between the heights its ground runs through - a room is
						// more than a metre high)
						Vector3 mid = new Vector3(o.x + (x + 0.5f) * sx, 0f, o.z + (z + 0.5f) * sz);
						float floorY = 0f, roofY = 0f, under, over;
						bool room = false;
						for (float y = Mathf.Max(bottom, b.min.y) + 0.05f; y < Mathf.Min(top, b.max.y) && !room; y += 1f)
						{
							mid.y = y;
							if (Inside(mid, own, b, out under, out over)) { room = true; floorY = y - under; roofY = y + over; }
						}
						if (!room) continue;
						// (and its ground in between its corners somewhere in that room)
						bool through = false;
						for (int i = 1; i < Looks && !through; i++)
							for (int j = 1; j < Looks && !through; j++)
							{
								Vector3 p = new Vector3(o.x + (x + (float)i / Looks) * sx, 0f, o.z + (z + (float)j / Looks) * sz);
								p.y = o.y + land.SampleHeight(p);
								through = p.y > floorY + 0.05f && p.y < roofY - 0.05f && Inside(p, own, b, out under, out over);
							}
						if (!through) continue;
						seen.Add(z * cells + x);
						found.Add(new Vector2Int(x, z));
					}
			}
		}

		/// <summary>A point of the ground in the piece's room: its nearest faces straight up and straight down both seen from their fronts (the shells face inwards). under, over: how far down the floor is and how far up the roof.</summary>
		internal static bool Inside(Vector3 p, HashSet<Collider> own, Bounds b, out float under, out float over)
		{
			Vector3 from = p + Vector3.up * 0.02f;
			under = over = 0f;
			if (!FrontFirst(from, Vector3.down, from.y - b.min.y + 1f, own, out under) || !FrontFirst(from, Vector3.up, b.max.y - from.y + 1f, own, out over)) return false;
			under -= 0.02f;
			over += 0.02f;
			return true;
		}

		static bool FrontFirst(Vector3 from, Vector3 dir, float len, HashSet<Collider> own, out float front)
		{
			front = Nearest(from, dir, len, own, false);
			return front < float.PositiveInfinity && front <= Nearest(from, dir, len, own, true) + 0.01f;
		}

		static float Nearest(Vector3 from, Vector3 dir, float len, HashSet<Collider> own, bool backs)
		{
			Physics.queriesHitBackfaces = backs;
			float best = float.PositiveInfinity;
			foreach (RaycastHit hit in Physics.RaycastAll(from, dir, len, ~0, QueryTriggerInteraction.Ignore))
				if (hit.distance < best && own.Contains(hit.collider)) best = hit.distance;
			return best;
		}
	}
}
