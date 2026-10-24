using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Survivors to rescue (ROADMAP RF3): the object "Survivor in a cage" (Survivor_Cage, Raft's shark cage from Radio
	/// tower) holds one of Raft's characters, locked with a story item (survivor.key). A player whose crew holds the key
	/// uses the cage: the key is used up, the cage opens (gone, for everyone - a key of the island's state, KeyBase + the
	/// object's index, one player gets it: Claims), the survivor says its line (survivor.line) in a speech bubble, and from
	/// then on stands on the raft as decoration (no AI, no needs). The host adds the survivor to the world's list (one per
	/// survivor.id, saved in the world file as "@survivor=id|name|character" lines, sent to every player with the world's
	/// rules) and sends the signal survivor.signal ("survivor" by default) for quests, world plans and other objects.
	/// </summary>
	public static class Survivors
	{
		public const int KeyBase = 0xD0000; // (after TraderRaft.KeyBase 0xC0000 + 0xFFFF)
		public const string CageObject = "Survivor_Cage", CageSource = "RT_SharkCage";
		public const string IdProp = "survivor.id", NameProp = "survivor.name", CharacterProp = "survivor.character", LineProp = "survivor.line",
			KeyProp = "survivor.key", SignalProp = "survivor.signal";
		public const string DefaultName = "Survivor", DefaultSignal = "survivor", DefaultLine = "You came! I had stopped hoping anyone would. Thank you!";
		/// <summary>How long the bubble stays (seconds); then the survivor is on the raft.</summary>
		public const float BubbleTime = 7f;

		public class Rescued
		{
			public string Id = "", Name = DefaultName, Character = "";
			public string Encode() { return Clean(Id) + "|" + Clean(Name) + "|" + Clean(Character); }
			public static Rescued Decode(string s)
			{
				string[] p = (s ?? "").Split('|');
				if (p.Length < 1 || p[0].Trim().Length == 0) return null;
				return new Rescued { Id = p[0].Trim(), Name = p.Length > 1 && p[1].Trim().Length > 0 ? p[1].Trim() : DefaultName, Character = p.Length > 2 ? p[2].Trim() : "" };
			}
		}

		static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "").Trim(); }

		static readonly List<Rescued> list = new List<Rescued>();

		/// <summary>The world's rescued survivors, in the order they were found.</summary>
		public static List<Rescued> All { get { return list.ToList(); } }
		public static bool Has(string id) { return list.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)); }

		/// <summary>Tests: the last bubble's text on this machine and how many were shown; rescues the host recorded.</summary>
		public static string LastBubble = "";
		public static int Bubbles, RescuedCount;

		public static bool IsCage(string name) { return name == CageObject; }

		/// <summary>A placed cage's settings (ObjectProps.Defaults).</summary>
		public static void Defaults(string name, Dictionary<string, string> props)
		{
			if (!IsCage(name)) return;
			props[NameProp] = DefaultName;
			props[LineProp] = DefaultLine;
		}

		/// <summary>Adds the cage to the object catalog (ContentCatalog.Register).</summary>
		internal static bool Register()
		{
			// (Raft's shark cage is an on-demand object of the radio tower: the cage is made when that scene is loaded)
			if (PlaceableCatalog.Get(CageSource) == null && !PlaceableCatalog.IsIndexed(CageSource)) { Debug.LogWarning("[CUSTOM ISLANDS] '" + CageSource + "' not found in Raft's objects; the survivor cage is left out"); return false; }
			PlaceableCatalog.AddCustomFrom(CageObject, CageSource, ContentCatalog.LootCategory, "Survivor in a cage", TidyCage);
			return true;
		}

		/// <summary>What the survivor cage leaves out of Raft's shark cage (found by TidyCage; tests read it).</summary>
		public static readonly List<string> LeftOut = new List<string>();

		/// <summary>The survivor cage's own copy of Raft's shark cage, without the rope it hangs from at Radio tower: away
		/// from the tower's crane the rope lay on the ground beside the cage (found by the user, 2026-10-16). The rope is part
		/// of the cage's one mesh, and Raft's meshes can't be read in the game, so the mesh is read from Raft's own files
		/// (checked against the loaded one), and the copy leaves out the stretch of triangles that holds most of what lies
		/// outside the cage's frame (the footprint of its upper half): the rope - in the editor and in a world. Raft's shark
		/// cage keeps its mesh; anything unexpected shows the cage whole.</summary>
		internal static void TidyCage(GameObject cage)
		{
			LeftOut.Clear();
			MeshFilter mf0 = cage.GetComponent<MeshFilter>();
			Mesh m = mf0 != null ? mf0.sharedMesh : null;
			if (m == null) { Debug.LogWarning("[CUSTOM ISLANDS] Survivor cage: Raft's shark cage has no mesh, shown whole"); return; }
			string note;
			Mesh own;
			try { own = WithoutRope(m, out note); }
			catch (Exception e) { own = null; note = "couldn't be read (" + e.Message + "), shown whole"; }
			Debug.Log("[CUSTOM ISLANDS] Survivor cage (" + m.name + "): " + note);
			if (own == null) return;
			foreach (MeshFilter mf in cage.GetComponentsInChildren<MeshFilter>(true))
				if (mf.sharedMesh == m) mf.sharedMesh = own;
			foreach (MeshCollider mc in cage.GetComponentsInChildren<MeshCollider>(true))
				if (mc.sharedMesh == m) mc.sharedMesh = own;
			LeftOut.Add(note);
		}

		/// <summary>A mesh as read from Raft's files.</summary>
		sealed class FileMesh
		{
			public Vector3[] Pos, Normal;
			public Vector4[] Tangent;
			public Color32[] Colour;
			public Vector2[] Uv;
			public int[] Tris;
			public string From;
		}

		/// <summary>A readable copy of the mesh without the rope. Null: it couldn't be read, or no rope was found (note says which).</summary>
		static Mesh WithoutRope(Mesh m, out string note)
		{
			if (m.subMeshCount != 1 || m.GetTopology(0) != MeshTopology.Triangles) { note = "not one part of triangles, shown whole"; return null; }
			FileMesh f = ReadFromFiles(m);
			if (f == null) { note = "not found in Raft's files as it is loaded, shown whole"; return null; }
			Vector3[] pos = f.Pos;
			int[] t = f.Tris;
			int count = t.Length / 3;
			// (the cage's frame: the footprint of its upper half - the rope lies low, on the ground beside it)
			float low = pos.Min(v => v.y), high = pos.Max(v => v.y), half = (low + high) * 0.5f;
			List<Vector3> upper = pos.Where(v => v.y > half).ToList();
			Bounds frame = new Bounds(upper[0], Vector3.zero);
			foreach (Vector3 v in upper) frame.Encapsulate(v);
			frame.Expand(new Vector3(0.2f, 0f, 0.2f));
			Func<int, bool> outsideV = v => pos[v].x < frame.min.x || pos[v].x > frame.max.x || pos[v].z < frame.min.z || pos[v].z > frame.max.z;
			Func<int, bool> outside = i => outsideV(t[i * 3]) && outsideV(t[i * 3 + 1]) && outsideV(t[i * 3 + 2]);
			// (the pieces: triangles sharing a corner, or a point at the same spot, are one piece)
			int[] root = new int[pos.Length];
			for (int v = 0; v < root.Length; v++) root[v] = v;
			Func<int, int> find = v => { while (root[v] != v) { root[v] = root[root[v]]; v = root[v]; } return v; };
			Action<int, int> join = (a, b) => { a = find(a); b = find(b); if (a != b) root[a] = b; };
			var spot = new Dictionary<Vector3Int, int>();
			for (int v = 0; v < pos.Length; v++)
			{
				Vector3Int k = Vector3Int.RoundToInt(pos[v] * 200f);
				int other;
				if (spot.TryGetValue(k, out other)) join(v, other); else spot[k] = v;
			}
			for (int i = 0; i < count; i++) { join(t[i * 3], t[i * 3 + 1]); join(t[i * 3 + 1], t[i * 3 + 2]); }
			// (the stretches: triangles next to each other in the mesh's order, of one piece; the rope is the stretch that
			// holds most of the triangles outside the frame - a tube from the top of the cage, over its floor, out onto the ground)
			int all = 0, best = -1, bestEnd = -1, bestOut = 0;
			for (int i = 0; i < count; )
			{
				int r = find(t[i * 3]), j = i, n = 0;
				for (; j < count && find(t[j * 3]) == r; j++) if (outside(j)) n++;
				all += n;
				if (n > bestOut) { best = i; bestEnd = j; bestOut = n; }
				i = j;
			}
			note = "read from " + f.From + ", " + count + " triangles, frame " + frame.min.ToString("F2") + " to " + frame.max.ToString("F2") + ", " + all + " outside";
			if (best < 0 || bestOut * 5 < all * 4 || bestEnd - best > count / 2) { note += "; no one stretch holds them (not the rope), shown whole"; return null; }
			var keep = new List<int>(t.Length);
			for (int i = 0; i < count; i++)
				if (i < best || i >= bestEnd) { keep.Add(t[i * 3]); keep.Add(t[i * 3 + 1]); keep.Add(t[i * 3 + 2]); }
			Mesh own = new Mesh { name = m.name + " (survivor cage)" };
			own.SetVertices(pos);
			own.SetNormals(f.Normal);
			own.SetTangents(f.Tangent);
			own.SetColors(f.Colour);
			own.SetUVs(0, f.Uv);
			own.SetTriangles(keep, 0, true);
			note += "; the rope, triangles " + best + "-" + (bestEnd - 1) + " (" + bestOut + " of them outside), left out: kept " + keep.Count / 3;
			return own;
		}

		/// <summary>The mesh from Raft's own files (Raft_Data's sharedassets*.assets and the .resS file next to it), when
		/// its layout is the one known (the shark cage's: 16-bit indices, one stream of position, normal, tangent, colour
		/// and one uv) and its counts match the loaded mesh. Null otherwise.</summary>
		static FileMesh ReadFromFiles(Mesh m)
		{
			string dir = Application.dataPath;
			byte[] key = System.Text.Encoding.ASCII.GetBytes(m.name);
			foreach (string file in System.IO.Directory.GetFiles(dir, "sharedassets*.assets")
				.OrderBy(x => System.IO.Path.GetFileName(x) == "sharedassets6.assets" ? 0 : 1).ThenBy(x => new System.IO.FileInfo(x).Length))
			{
				byte[] d = ReadAll(file);
				for (int at = Find(d, key, 4, d.Length); at >= 0; at = Find(d, key, at + 1, d.Length))
				{
					FileMesh f = Parse(d, at, key.Length, m, file);
					if (f != null) return f;
				}
			}
			return null;
		}

		static byte[] ReadAll(string file)
		{
			using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
			{
				byte[] d = new byte[fs.Length];
				int n = 0;
				while (n < d.Length) { int r = fs.Read(d, n, d.Length - n); if (r <= 0) break; n += r; }
				return d;
			}
		}

		static int Find(byte[] d, byte[] key, int from, int to)
		{
			for (int i = Math.Max(0, from); i + key.Length <= to; i++)
			{
				int k = 0;
				while (k < key.Length && d[i + k] == key[k]) k++;
				if (k == key.Length) return i;
			}
			return -1;
		}

		/// <summary>The mesh whose name starts at <paramref name="at"/>, or null when it isn't laid out as expected.</summary>
		static FileMesh Parse(byte[] d, int at, int nameLength, Mesh m, string file)
		{
			Func<int, int> i32 = o => BitConverter.ToInt32(d, o);
			if (at < 4 || i32(at - 4) != nameLength) return null;
			int p = (at + nameLength + 3) & ~3;
			int vertexCount = m.vertexCount, indexCount = (int)m.GetIndexCount(0);
			if (p + 4 + 48 + 44 + 4 > d.Length) return null;
			// (one part: first byte 0, index count, triangles, base vertex 0, first vertex 0, vertex count, then its box)
			if (i32(p) != 1 || i32(p + 4) != 0 || i32(p + 8) != indexCount || i32(p + 12) != 0 || i32(p + 16) != 0 || i32(p + 20) != 0 || i32(p + 24) != vertexCount) return null;
			p += 4 + 48;
			// (no blend shapes, bones or compression, not readable, 16-bit indices)
			for (int k = 0; k < 11; k++) if (i32(p + k * 4) != 0) return null;
			p += 44;
			if (i32(p) != indexCount * 2 || p + 4 + indexCount * 2 > d.Length) return null;
			var tris = new int[indexCount];
			for (int k = 0; k < indexCount; k++) { tris[k] = BitConverter.ToUInt16(d, p + 4 + k * 2); if (tris[k] >= vertexCount) return null; }
			p = (p + 4 + indexCount * 2 + 3) & ~3;
			// (the vertex layout: count, 14 channels of stream/offset/format/size - position, normal, tangent, colour, uv)
			byte[] layout = { 0, 0, 0, 3, 0, 12, 1, 0x34, 0, 20, 1, 4, 0, 28, 2, 4, 0, 32, 1, 2 };
			if (p + 8 + 56 + 4 > d.Length || i32(p) != vertexCount || i32(p + 4) != 14) return null;
			for (int k = 0; k < 56; k++) if (d[p + 8 + k] != (k < layout.Length ? layout[k] : 0)) return null;
			if (i32(p + 64) != 0) return null; // (the data is in the .resS file)
			p += 68;
			const int stride = 36;
			string resName = System.IO.Path.GetFileName(file) + ".resS";
			byte[] res = System.Text.Encoding.ASCII.GetBytes(resName);
			int s = Find(d, res, p, Math.Min(d.Length, p + 4 * 1024 * 1024));
			if (s < 16 || i32(s - 4) != res.Length || BitConverter.ToUInt32(d, s - 8) != vertexCount * stride) return null;
			long offset = BitConverter.ToInt64(d, s - 16);
			string resPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file), resName);
			byte[] v = new byte[vertexCount * stride];
			using (var fs = new System.IO.FileStream(resPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
			{
				if (offset < 0 || offset + v.Length > fs.Length) return null;
				fs.Seek(offset, System.IO.SeekOrigin.Begin);
				int n = 0;
				while (n < v.Length) { int r = fs.Read(v, n, v.Length - n); if (r <= 0) return null; n += r; }
			}
			Func<int, float> h = o => HalfToFloat(BitConverter.ToUInt16(v, o));
			var f = new FileMesh
			{
				Pos = new Vector3[vertexCount], Normal = new Vector3[vertexCount], Tangent = new Vector4[vertexCount],
				Colour = new Color32[vertexCount], Uv = new Vector2[vertexCount], Tris = tris, From = System.IO.Path.GetFileName(file)
			};
			for (int k = 0; k < vertexCount; k++)
			{
				int o = k * stride;
				f.Pos[k] = new Vector3(BitConverter.ToSingle(v, o), BitConverter.ToSingle(v, o + 4), BitConverter.ToSingle(v, o + 8));
				f.Normal[k] = new Vector3(h(o + 12), h(o + 14), h(o + 16));
				f.Tangent[k] = new Vector4(h(o + 20), h(o + 22), h(o + 24), h(o + 26));
				f.Colour[k] = new Color32(v[o + 28], v[o + 29], v[o + 30], v[o + 31]);
				f.Uv[k] = new Vector2(h(o + 32), h(o + 34));
			}
			// (the points must fill the loaded mesh's box)
			Bounds b = new Bounds(f.Pos[0], Vector3.zero);
			foreach (Vector3 q in f.Pos) b.Encapsulate(q);
			if ((b.center - m.bounds.center).magnitude > 0.05f || (b.size - m.bounds.size).magnitude > 0.05f) return null;
			return f;
		}

		/// <summary>A 16-bit float.</summary>
		static float HalfToFloat(ushort x)
		{
			int sign = (x >> 15) != 0 ? -1 : 1, exp = (x >> 10) & 0x1F, man = x & 0x3FF;
			if (exp == 0) return sign * man * (1f / 16777216f);
			if (exp == 31) return man == 0 ? sign * float.PositiveInfinity : float.NaN;
			return sign * (1f + man / 1024f) * Mathf.Pow(2f, exp - 15);
		}

		/// <summary>What the inspector and the behaviour window say about a cage.</summary>
		public static string Describe(IDictionary<string, string> p)
		{
			string key = ObjectProps.Get(p, KeyProp).Trim();
			return ObjectProps.Get(p, NameProp, DefaultName) + (key.Length > 0 ? ", locked with " + StoryItems.Label(key) : ", not locked") +
				", signal '" + SignalOf(p) + "'";
		}

		public static string SignalOf(IDictionary<string, string> p) { string s = ObjectProps.Get(p, SignalProp).Trim(); return s.Length > 0 ? s : DefaultSignal; }

		/// <summary>One of Raft's characters for a survivor: by its number or name, else one picked by the survivor's id.</summary>
		public static SO_Character CharacterFor(string character, string id)
		{
			SO_Character c = Behaviours.CharacterOf(character);
			if (c != null) return c;
			List<SO_Character> all = Behaviours.Characters().Where(x => x != null && x.modelPrefab != null).ToList();
			if (all.Count == 0) return null;
			int h = 0;
			foreach (char ch in (id ?? "")) h = unchecked(h * 31 + ch);
			return all[(h & 0x7FFFFFFF) % all.Count];
		}

		#region The world's list (WorldRules: saved with the world, sent with its rules)

		/// <summary>Host: before the world file is read; client: when the host's world arrives.</summary>
		internal static void Reset()
		{
			list.Clear();
			EnsureKeeper();
		}

		internal static bool ReadLine(string key, string value)
		{
			if (!key.Equals("survivor", StringComparison.OrdinalIgnoreCase)) return false;
			Rescued r = Rescued.Decode(value);
			if (r != null && !Has(r.Id)) list.Add(r);
			return true;
		}

		internal static IEnumerable<string> WriteLines() { return list.Select(r => "@survivor=" + r.Encode()); }

		internal static bool HasState { get { return list.Count > 0; } }

		/// <summary>Host -> clients, with the world's rules (WorldRules.Message: the message's Names).</summary>
		internal static string[] Encode() { return list.Select(r => r.Encode()).ToArray(); }

		/// <summary>Client: the host's list (null: a host without survivors - an older version).</summary>
		internal static void FromHost(string[] names)
		{
			if (Raft_Network.IsHost) return;
			list.Clear();
			if (names != null) foreach (string s in names) { Rescued r = Rescued.Decode(s); if (r != null && !Has(r.Id)) list.Add(r); }
			EnsureKeeper();
		}

		/// <summary>Host: a cage was opened (here, or a client's "used" came): the survivor joins the world's list once,
		/// and the signal goes out. heal: an already open cage whose survivor is missing from the list (a world saved
		/// between the two) - back on the list only, no second signal.</summary>
		internal static void Rescue(IslandWorldState.Entry e, SurvivorCage cage, bool heal = false)
		{
			if (!Raft_Network.IsHost || cage == null) return;
			string id = cage.IdOf(e);
			if (Has(id)) return;
			list.Add(new Rescued { Id = id, Name = cage.Name, Character = cage.Character });
			if (!heal) RescuedCount++;
			WorldRules.Broadcast();
			if (!heal) Behaviours.SendSignal(e, cage.Signal);
			Debug.Log("[CUSTOM ISLANDS] Survivor '" + cage.Name + "' (" + id + ") " + (heal ? "back on the list (its cage is open)" : "rescued") + " on '" + (e != null ? e.HostName : "?") + "': " + list.Count + " on the raft now");
			EnsureKeeper();
		}

		/// <summary>Tests and the console: forgets the world's survivors (host).</summary>
		public static void Clear()
		{
			if (!Raft_Network.IsHost) return;
			list.Clear();
			WorldRules.Broadcast();
		}

		#endregion

		#region The survivors on the raft

		/// <summary>Survivors still talking at their cage (their bubble): on the raft once it's gone.</summary>
		internal static readonly HashSet<string> Talking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, SurvivorFigure> figures = new Dictionary<string, SurvivorFigure>(StringComparer.OrdinalIgnoreCase);
		static bool keeping;

		/// <summary>The survivor figures standing on the raft now (tests).</summary>
		public static int FigureCount { get { return figures.Values.Count(f => f != null); } }
		public static GameObject FigureOf(string id) { SurvivorFigure f; return figures.TryGetValue(id ?? "", out f) && f != null ? f.gameObject : null; }

		static void EnsureKeeper()
		{
			if (keeping || DynamicIslands.instance == null) return;
			keeping = true;
			DynamicIslands.instance.StartCoroutine(Keep());
		}

		static IEnumerator Keep()
		{
			bool told = false;
			while (true)
			{
				yield return new WaitForSeconds(1f);
				try { Sync(); }
				catch (Exception e) { if (!told) { told = true; Debug.LogWarning("[CUSTOM ISLANDS] Survivors on the raft: " + e); } }
			}
		}

		static void Sync()
		{
			Raft raft = LoadSceneManager.IsGameSceneLoaded ? ComponentManager<Raft>.Value : null;
			foreach (string id in figures.Where(f => f.Value == null || raft == null || !Has(f.Key) || f.Value.transform.parent != raft.transform).Select(f => f.Key).ToList())
			{
				if (figures[id] != null) UnityEngine.Object.Destroy(figures[id].gameObject);
				figures.Remove(id);
			}
			if (raft == null || list.Count == 0) return;
			List<Block> floors = null;
			var taken = new HashSet<Block>(figures.Values.Where(f => f != null && f.On != null).Select(f => f.On));
			for (int i = 0; i < list.Count; i++)
			{
				Rescued r = list[i];
				SurvivorFigure f;
				if (figures.TryGetValue(r.Id, out f) && f != null)
				{
					if (f.On != null) continue;
					// (its floor was taken away: it moves to another one)
					UnityEngine.Object.Destroy(f.gameObject);
					figures.Remove(r.Id);
				}
				if (Talking.Contains(r.Id)) continue;
				if (floors == null) floors = HeadStart.Foundations(raft).Where(b => b != null).OrderBy(b => b.transform.localPosition.x).ThenBy(b => b.transform.localPosition.z).ToList();
				Block on = Spot(raft, floors, taken, i);
				if (on == null) return;
				taken.Add(on);
				GameObject model = Model(CharacterFor(r.Character, r.Id), "CI_Survivor_" + r.Id);
				if (model == null) return;
				model.transform.SetParent(raft.transform, false);
				Vector3 local = on.transform.localPosition;
				local.y = HeadStart.FoundationTop(on, raft);
				model.transform.localPosition = local;
				// (looking out over the sea, away from the raft's middle)
				Vector3 outward = new Vector3(local.x, 0f, local.z);
				model.transform.localRotation = outward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(outward) : Quaternion.identity;
				f = model.AddComponent<SurvivorFigure>();
				f.Id = r.Id;
				f.On = on;
				figures[r.Id] = f;
				model.SetActive(true);
				Debug.Log("[CUSTOM ISLANDS] Survivor '" + r.Name + "' stands on the raft (" + local.x.ToString("0.#", CultureInfo.InvariantCulture) + ", " + local.z.ToString("0.#", CultureInfo.InvariantCulture) + ")");
			}
		}

		/// <summary>A free floor for the i-th survivor: spread over the raft, nothing standing on it.</summary>
		static Block Spot(Raft raft, List<Block> floors, HashSet<Block> taken, int i)
		{
			if (floors.Count == 0) return null;
			List<Block> others = raft.GetComponentsInChildren<Block>().Where(b => b != null && !floors.Contains(b)).ToList();
			int start = (i * 7 + 3) % floors.Count;
			for (int pass = 0; pass < 2; pass++)
				for (int k = 0; k < floors.Count; k++)
				{
					Block f = floors[(start + k) % floors.Count];
					if (taken.Contains(f)) continue;
					if (pass == 0 && Occupied(raft, f, others)) continue;
					return f;
				}
			return floors[start];
		}

		static bool Occupied(Raft raft, Block floor, List<Block> others)
		{
			Vector3 p = floor.transform.localPosition;
			float top = HeadStart.FoundationTop(floor, raft);
			foreach (Block b in others)
			{
				Vector3 q = b.transform.localPosition;
				if (new Vector2(q.x - p.x, q.z - p.z).magnitude < 0.7f && q.y > top - 0.2f && q.y < top + 1.9f) return true;
			}
			return false;
		}

		/// <summary>A still copy of a character's model (no scripts, no colliders), standing with Raft's third-person idle; inactive.</summary>
		internal static GameObject Model(SO_Character c, string name)
		{
			if (c == null || c.modelPrefab == null) return null;
			var holder = new GameObject(name);
			holder.SetActive(false);
			CharacterModelModifications src = c.modelPrefab;
			GameObject m = UnityEngine.Object.Instantiate(src.gameObject, holder.transform);
			// (named as under the player's own Animator: Raft's rig is generic, its bone paths start with this name)
			m.name = src.gameObject.name + "(Clone)";
			m.transform.localPosition = Vector3.zero;
			m.transform.localRotation = Quaternion.identity;
			CharacterModelModifications mod = m.GetComponent<CharacterModelModifications>();
			SkinnedMeshRenderer body = mod != null ? mod.fullBodyMesh : null, arm = mod != null ? mod.armMesh : null;
			GameObject bodyHazmat = mod != null ? mod.fullBodyMeshHazmat : null, armHazmat = mod != null ? mod.armMeshHazmat : null;
			PlaceableCatalog.StripScripts(m);
			foreach (Joint j in m.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
			foreach (Component x in m.GetComponentsInChildren<Component>(true).Where(x => x is Collider || x is Rigidbody || x is CharacterController || x is AudioSource || x is Camera || x is AudioListener || x is Light).ToList())
				UnityEngine.Object.DestroyImmediate(x);
			// (the Animator one level above the model, as on the player - not one of the model's own: a hat has one)
			Animator an = holder.AddComponent<Animator>();
			if (src.avatar != null) an.avatar = src.avatar;
			if (src.thirdPersonController != null) an.runtimeAnimatorController = src.thirdPersonController;
			an.applyRootMotion = false;
			an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
			if (body != null) { body.gameObject.SetActive(true); body.enabled = true; body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
			if (arm != null) arm.gameObject.SetActive(false);
			if (bodyHazmat != null) bodyHazmat.SetActive(false);
			if (armHazmat != null) armHazmat.SetActive(false);
			// (the player's own body is on a layer the first-person camera leaves out)
			foreach (Transform t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
			return holder;
		}

		/// <summary>A speech bubble over a survivor: its name and line, facing the camera; gone after BubbleTime.</summary>
		internal static GameObject Bubble(Transform over, string name, string line)
		{
			string text = name + ":\n\"" + Wrap(line, 34) + "\"";
			LastBubble = name + ": " + line;
			Bubbles++;
			var go = new GameObject("CI_SurvivorBubble");
			go.transform.SetParent(over, false);
			go.transform.localPosition = new Vector3(0f, 2.15f, 0f);
			var tm = go.AddComponent<TextMesh>();
			tm.font = UIKit.Font;
			tm.fontSize = 48;
			tm.characterSize = 0.03f;
			tm.anchor = TextAnchor.LowerCenter;
			tm.alignment = TextAlignment.Center;
			tm.color = new Color(1f, 0.97f, 0.85f);
			tm.richText = false;
			tm.text = text;
			MeshRenderer mr = go.GetComponent<MeshRenderer>();
			mr.sharedMaterial = UIKit.Font.material;
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			go.AddComponent<FaceCamera>();
			UnityEngine.Object.Destroy(go, BubbleTime);
			Debug.Log("[CUSTOM ISLANDS] Survivor '" + name + "' says: " + line);
			return go;
		}

		static string Wrap(string s, int width)
		{
			var lines = new List<string>();
			string cur = "";
			foreach (string w in (s ?? "").Split(' '))
			{
				if (cur.Length > 0 && cur.Length + 1 + w.Length > width) { lines.Add(cur); cur = w; }
				else cur = cur.Length > 0 ? cur + " " + w : w;
			}
			if (cur.Length > 0) lines.Add(cur);
			return string.Join("\n", lines.ToArray());
		}

		#endregion
	}

	/// <summary>A rescued survivor standing on the raft; On: the floor it stands on.</summary>
	public class SurvivorFigure : MonoBehaviour
	{
		public string Id;
		public Block On;
	}

	/// <summary>
	/// A survivor's cage in a world (on the cage object's interact holder): locked until a player whose crew has the key
	/// uses it; opened (for everyone, also after a reload: the island's state) it is hidden, the survivor talks and goes
	/// to the raft.
	/// </summary>
	public class SurvivorCage : MonoBehaviour, IRaycastable
	{
		public string Id = "", Name = Survivors.DefaultName, Character = "", Line = Survivors.DefaultLine, Key = "", Signal = Survivors.DefaultSignal;
		public int Index;
		GameObject cage, figure;
		bool open, seenClosed;
		float nextCheck;

		public static SurvivorCage Attach(GameObject go, IDictionary<string, string> props, int index)
		{
			GameObject holder = CustomNote.InteractHolder(go);
			SurvivorCage c = holder.AddComponent<SurvivorCage>();
			c.cage = go;
			c.Index = index;
			c.Id = ObjectProps.Get(props, Survivors.IdProp).Trim();
			c.Name = ObjectProps.Get(props, Survivors.NameProp, Survivors.DefaultName).Trim();
			if (c.Name.Length == 0) c.Name = Survivors.DefaultName;
			c.Character = ObjectProps.Get(props, Survivors.CharacterProp).Trim();
			c.Line = ObjectProps.Get(props, Survivors.LineProp, Survivors.DefaultLine).Trim();
			c.Key = ObjectProps.Get(props, Survivors.KeyProp).Trim();
			c.Signal = Survivors.SignalOf(props);
			return c;
		}

		public int KeyOf() { return Survivors.KeyBase + (Index & 0xFFFF); }

		/// <summary>The survivor's id: survivor.id, else the island's name and the cage's index.</summary>
		public string IdOf(IslandWorldState.Entry e) { return Id.Length > 0 ? Id : (e != null ? e.HostName : "island") + "#" + Index; }

		IslandWorldState.Entry Entry { get { return ContentState.EntryOf(transform); } }

		public bool IsOpen { get { return open; } }
		public GameObject Figure { get { return figure; } }
		/// <summary>The cage's own object (hidden when open).</summary>
		public GameObject Cage { get { return cage; } }

		bool HasKey { get { return Key.Length == 0 || StoryBook.Count(Key) > 0; } }
		string LockedText { get { return "The cage is locked. It needs " + StoryItems.Label(Key) + "."; } }

		void Update()
		{
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 0.4f;
			IslandWorldState.Entry e = Entry;
			if (e == null) return;
			bool used = ContentState.IsUsed(e, KeyOf());
			if (used && !open) Open(e, seenClosed);
			else if (!used && !open) { seenClosed = true; if (figure == null) PutInside(); }
			// (host: a client opened it - its "used" came; or the world was saved between the two)
			if (used && Raft_Network.IsHost && !Survivors.Has(IdOf(e))) Survivors.Rescue(e, this, Behaviours.HasSignal(e, Signal));
		}

		void PutInside()
		{
			GameObject m = Survivors.Model(Survivors.CharacterFor(Character, IdOf(Entry)), "CI_CagedSurvivor");
			if (m == null) return;
			// (beside the cage, not in it: the cage is hidden when it opens)
			m.transform.SetParent(cage.transform.parent, false);
			Bounds b;
			Vector3 at = cage.transform.position;
			if (CageBounds(out b)) at = new Vector3(b.center.x, b.min.y, b.center.z);
			m.transform.position = at;
			m.transform.rotation = Quaternion.Euler(0f, cage.transform.eulerAngles.y, 0f);
			m.SetActive(true);
			figure = m;
		}

		bool CageBounds(out Bounds b)
		{
			b = new Bounds();
			bool any = false;
			foreach (Renderer r in cage.GetComponentsInChildren<Renderer>())
			{
				if (r == null || !r.enabled) continue;
				if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
			}
			return any;
		}

		/// <summary>Opens the cage on this machine; live: seen closed here first (the survivor talks), else a reload.</summary>
		void Open(IslandWorldState.Entry e, bool live)
		{
			open = true;
			foreach (Renderer r in cage.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
			foreach (Collider c in cage.GetComponentsInChildren<Collider>(true)) c.enabled = false;
			string id = IdOf(e);
			if (live && figure != null)
			{
				Survivors.Talking.Add(id);
				Survivors.Bubble(figure.transform, Name, Line);
				StartCoroutine(ToRaft(id));
			}
			else
			{
				if (figure != null) Destroy(figure);
				figure = null;
			}
		}

		IEnumerator ToRaft(string id)
		{
			yield return new WaitForSeconds(Survivors.BubbleTime);
			Survivors.Talking.Remove(id);
			if (figure != null) Destroy(figure);
			figure = null;
			IslandInfo.ShowMessage(Name + " is safe on your raft now");
		}

		void OnDestroy()
		{
			if (figure != null) Destroy(figure);
			Survivors.Talking.Remove(IdOf(Entry));
		}

		void IRaycastable.OnIsRayed()
		{
			if (NoteReader.IsOpen || open) return;
			DisplayTextManager hints = CustomNote.Hints;
			bool has = HasKey;
			if (hints != null)
			{
				if (!has) hints.ShowText(LockedText, 0, true, 0);
				else hints.ShowText(Key.Length > 0 ? "Unlock the cage with " + StoryItems.Label(Key) : "Open the cage", CustomNote.InteractKey, 0, 0, true);
			}
			if (has && CustomNote.InteractPressed())
			{
				if (hints != null) hints.HideDisplayTexts();
				Use();
			}
		}

		void IRaycastable.OnRayEnter() { }

		void IRaycastable.OnRayExit()
		{
			DisplayTextManager hints = CustomNote.Hints;
			if (hints != null) hints.HideDisplayTexts();
		}

		/// <summary>The local player opens the cage (true: opened now; a client's waits for the host's yes, then opens).</summary>
		public bool Use()
		{
			IslandWorldState.Entry e = Entry;
			int key = KeyOf();
			if (open || ContentState.IsUsed(e, key)) return false;
			if (!HasKey) { IslandInfo.ShowMessage(LockedText); return false; }
			if (!Claims.May(e, key, yes => { if (this == null) return; if (yes) Use(); else Beaten(); }))
			{
				if (Raft_Network.IsHost) Beaten();
				return false;
			}
			if (!HasKey) return false;
			if (Key.Length > 0) StoryBook.Take(Key, 1);
			ContentState.MarkUsed(transform, key);
			Debug.Log("[CUSTOM ISLANDS] Survivor cage opened on '" + (e != null ? e.HostName : "?") + "'" + (Key.Length > 0 ? " with " + StoryItems.Label(Key) : ""));
			nextCheck = 0f;
			Update();
			return true;
		}

		void Beaten()
		{
			Debug.Log("[CUSTOM ISLANDS] That cage was opened by another player");
		}
	}
}
