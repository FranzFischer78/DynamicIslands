using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Small top-down pictures of saved islands (the generator's preview colours, cropped to the land), for lists such as
	/// "Islands while sailing" (IslandPickerWindow). Made once from the island's heights - the file read on a worker
	/// thread - and kept as Mods\DynamicIslands\thumbcache\&lt;island&gt;.png; made again when the island is saved after it.
	/// Also the bigger map of PlanPickerWindow (Map).
	/// </summary>
	public static class IslandThumbs
	{
		public const string FolderName = "thumbcache";
		/// <summary>The cached pictures' side in pixels.</summary>
		public const int Side = 96;

		class Kept { public Texture2D Tex; public long Stamp; }
		static readonly Dictionary<string, Kept> kept = new Dictionary<string, Kept>(StringComparer.OrdinalIgnoreCase);

		public static string Folder { get { return Path.Combine(DynamicIslands.assetpath, FolderName); } }
		public static string PngPath(string island) { return Path.Combine(Folder, island + ".png"); }

		/// <summary>The island's picture when one is made and still fresh (memory, else the .png), or null. Cheap: no island file read.</summary>
		public static Texture2D Cached(string island)
		{
			try
			{
				var src = new FileInfo(IslandSpawner.PathFor(island));
				if (!src.Exists) return null;
				long stamp = src.LastWriteTimeUtc.Ticks ^ src.Length;
				Kept k;
				if (kept.TryGetValue(island, out k) && k.Stamp == stamp && k.Tex != null) return k.Tex;
				var png = new FileInfo(PngPath(island));
				if (!png.Exists || png.LastWriteTimeUtc < src.LastWriteTimeUtc) return null;
				var t = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "CI_IslandThumb", wrapMode = TextureWrapMode.Clamp };
				if (!t.LoadImage(File.ReadAllBytes(png.FullName))) { UnityEngine.Object.Destroy(t); return null; }
				kept[island] = new Kept { Tex = t, Stamp = stamp };
				return t;
			}
			catch { return null; }
		}

		/// <summary>The island's picture: the cached one, else made (the file read off the main thread) and cached. done(null) if it can't be.</summary>
		public static IEnumerator Make(string island, Action<Texture2D> done)
		{
			Texture2D t = Cached(island);
			if (t != null) { done(t); yield break; }
			string path = IslandSpawner.PathFor(island);
			IslandFile f = null;
			var task = System.Threading.Tasks.Task.Run(() => { f = IslandFile.LoadOffThread(path); });
			while (!task.IsCompleted) yield return null;
			if (task.IsFaulted || f == null) { done(null); yield break; }
			try
			{
				var src = new FileInfo(path);
				t = Map(f, Side);
				if (t != null)
				{
					t.name = "CI_IslandThumb";
					Directory.CreateDirectory(Folder);
					SafeFile.WriteAllBytes(PngPath(island), t.EncodeToPNG());
					kept[island] = new Kept { Tex = t, Stamp = src.LastWriteTimeUtc.Ticks ^ src.Length };
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Picture of island " + island + ": " + e.Message); t = null; }
			done(t);
		}

		/// <summary>A top-down map of an island (the generator's preview colours) at most maxSide pixels a side, cropped to the
		/// land and a ring of sea round it (a small island on a big terrain would be a dot), or null.</summary>
		public static Texture2D Map(IslandFile f, int maxSide)
		{
			if (f == null || f.Heights == null) return null;
			int res = f.Heights.GetLength(0), skip = Mathf.Max(1, (res - 1) / maxSide), n = (res - 1) / skip + 1;
			var m = new float[n, n];
			for (int y = 0; y < n; y++)
				for (int x = 0; x < n; x++)
					m[y, x] = f.Heights[Mathf.Min(res - 1, y * skip), Mathf.Min(f.Heights.GetLength(1) - 1, x * skip)] * f.TerrainSize.y;
			int x0 = n, y0 = n, x1 = -1, y1 = -1;
			for (int y = 0; y < n; y++)
				for (int x = 0; x < n; x++)
					if (m[y, x] > f.WaterLevel - 1.5f) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
			if (x1 >= 0)
			{
				int side = Mathf.Clamp((int)(Math.Max(x1 - x0, y1 - y0) * 1.35f) + 8, 16, n);
				int cx = Mathf.Clamp((x0 + x1) / 2 - side / 2, 0, n - side), cy = Mathf.Clamp((y0 + y1) / 2 - side / 2, 0, n - side);
				var c = new float[side, side];
				for (int y = 0; y < side; y++)
					for (int x = 0; x < side; x++)
						c[y, x] = m[cy + y, cx + x];
				m = c;
			}
			return IslandGenerator.Preview(m, f.TerrainSize.x / (n - 1), TerrainPainter.StyleIndex(f.Style), null, f.WaterLevel);
		}

		/// <summary>One of Raft's own islands' pictures (island_thumbs), the same one for the same key - the stock picture of rows
		/// that are no saved island (map types, brand-new generated islands). Null if none are shipped.</summary>
		public static Texture2D Stock(string key)
		{
			try
			{
				List<RaftIsland> all = RaftIslands.All;
				if (all == null || all.Count == 0) return null;
				int h = 17;
				foreach (char c in key ?? "") h = unchecked(h * 31 + char.ToLowerInvariant(c));
				int start = (h & 0x7fffffff) % all.Count;
				for (int i = 0; i < all.Count; i++)
				{
					Texture2D t = RaftIslands.Thumbnail(all[(start + i) % all.Count]);
					if (t != null) return t;
				}
			}
			catch { }
			return null;
		}

		/// <summary>Shows t in img filling w x h, cropped to the box's shape (the middle of a wide picture).</summary>
		public static void Cover(UnityEngine.UI.RawImage img, Texture t, float w, float h)
		{
			img.texture = t;
			img.uvRect = new Rect(0f, 0f, 1f, 1f);
			if (t == null || t.width <= 0 || t.height <= 0 || h <= 0f) return;
			float a = (float)t.width / t.height, box = w / h;
			if (a > box) { float u = box / a; img.uvRect = new Rect((1f - u) / 2f, 0f, u, 1f); }
			else if (a < box) { float v = a / box; img.uvRect = new Rect(0f, (1f - v) / 2f, 1f, v); }
		}
	}
}
