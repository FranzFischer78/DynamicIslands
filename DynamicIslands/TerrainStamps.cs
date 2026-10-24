using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Terrain stamps: ready-made land shapes (hill, peak, crater, mesa, lagoon, ridge) and the builder's own, saved
	/// from the terrain under the brush. A click puts the shape down at the brush, as big as the brush; it is added to
	/// the ground that is already there (so a crater stamped on a slope stays on the slope). Q/E turn it.
	///
	/// A stamp is a square grid of heights in units of its radius, relative to the ground at its rim, zero outside the
	/// circle. Saved stamps are files in Mods\DynamicIslands\stamps: "CIST", int32 version 1, int32 N, float[N*N].
	/// </summary>
	public static class TerrainStamps
	{
		public const int Size = 65;
		const uint Magic = 0x54534943; // "CIST"

		public class Stamp
		{
			public string Name;
			public bool BuiltIn;
			public float[,] Heights = new float[Size, Size]; // [z, x], in units of the radius
		}

		public static readonly List<Stamp> All = new List<Stamp>();
		public static int Selected;
		/// <summary>Turn of the stamp in degrees (Q/E while stamping).</summary>
		public static float Rotation;

		public static string Folder { get { return Path.Combine(DynamicIslands.assetpath, "stamps"); } }

		public static Stamp Current { get { return All.Count > 0 ? All[Mathf.Clamp(Selected, 0, All.Count - 1)] : null; } }

		static float Cos(float d) { return 0.5f * (1f + Mathf.Cos(Mathf.PI * Mathf.Clamp01(d))); }

		static readonly KeyValuePair<string, Func<float, float, float>>[] Shapes =
		{
			new KeyValuePair<string, Func<float, float, float>>("Hill", (u, v) => 0.3f * Cos(Mathf.Sqrt(u * u + v * v))),
			new KeyValuePair<string, Func<float, float, float>>("Peak", (u, v) => 0.7f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Sqrt(u * u + v * v)), 1.8f)),
			new KeyValuePair<string, Func<float, float, float>>("Crater", (u, v) =>
			{
				float d = Mathf.Sqrt(u * u + v * v);
				return 0.22f * Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.16f, 2f)) - 0.18f * Mathf.Max(0f, 1f - Mathf.Pow(d / 0.55f, 2f));
			}),
			new KeyValuePair<string, Func<float, float, float>>("Mesa", (u, v) => 0.25f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.82f, Mathf.Sqrt(u * u + v * v))))),
			new KeyValuePair<string, Func<float, float, float>>("Lagoon", (u, v) => -0.12f * Cos(Mathf.Sqrt(u * u + v * v))),
			new KeyValuePair<string, Func<float, float, float>>("Ridge", (u, v) => 0.3f * Mathf.Exp(-Mathf.Pow(v / 0.18f, 2f)) * Cos(Mathf.Abs(u))),
		};

		/// <summary>The built-in shapes, then the saved stamps (again after saving one).</summary>
		public static void Load()
		{
			All.Clear();
			foreach (var s in Shapes)
			{
				var st = new Stamp { Name = s.Key, BuiltIn = true };
				for (int z = 0; z < Size; z++)
					for (int x = 0; x < Size; x++)
					{
						float u = x / (Size - 1f) * 2f - 1f, v = z / (Size - 1f) * 2f - 1f;
						st.Heights[z, x] = u * u + v * v <= 1f ? s.Value(u, v) : 0f;
					}
				All.Add(st);
			}
			if (!Directory.Exists(Folder)) return;
			foreach (string file in Directory.GetFiles(Folder, "*.stamp").OrderBy(f => f))
			{
				try
				{
					using (var r = new BinaryReader(File.OpenRead(file)))
					{
						if (r.ReadUInt32() != Magic) continue;
						r.ReadInt32();
						if (r.ReadInt32() != Size) continue;
						var st = new Stamp { Name = Path.GetFileNameWithoutExtension(file) };
						for (int z = 0; z < Size; z++) for (int x = 0; x < Size; x++) st.Heights[z, x] = r.ReadSingle();
						All.Add(st);
					}
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Stamp " + file + ": " + e.Message); }
			}
		}

		/// <summary>The ground in a circle as a stamp: heights relative to the average height of its rim.</summary>
		public static Stamp Capture(Terrain terrain, Vector3 centre, float radius, string name)
		{
			float rim = 0f;
			const int RimSamples = 64;
			for (int i = 0; i < RimSamples; i++)
			{
				float a = i * Mathf.PI * 2f / RimSamples;
				rim += terrain.SampleHeight(centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius);
			}
			rim /= RimSamples;
			var st = new Stamp { Name = name };
			for (int z = 0; z < Size; z++)
				for (int x = 0; x < Size; x++)
				{
					float u = x / (Size - 1f) * 2f - 1f, v = z / (Size - 1f) * 2f - 1f;
					if (u * u + v * v > 1f) continue;
					st.Heights[z, x] = (terrain.SampleHeight(centre + new Vector3(u, 0, v) * radius) - rim) / radius;
				}
			return st;
		}

		static string PathFor(string name) { return Path.Combine(Folder, name + ".stamp"); }

		public static bool Exists(string name) { return File.Exists(PathFor(name)); }

		/// <summary>Renames a saved stamp (ROADMAP T4); the list is read again, the same stamp stays chosen.</summary>
		public static void Rename(string from, string to)
		{
			Stamp current = Current;
			PiecesFiles.Move(PathFor(from), PathFor(to));
			Load();
			int i = All.FindIndex(s => !s.BuiltIn && s.Name == (current != null && !current.BuiltIn && current.Name == from ? to : current != null ? current.Name : ""));
			Selected = i >= 0 ? i : 0;
		}

		/// <summary>Deletes a saved stamp (ROADMAP T4): moved to Mods\DynamicIslands\deleted\stamps, where it can be got back.</summary>
		public static void MoveToDeleted(string name)
		{
			Stamp current = Current;
			PiecesFiles.MoveToDeleted(PathFor(name), "stamps");
			Load();
			int i = current == null ? -1 : All.FindIndex(s => s.BuiltIn == current.BuiltIn && s.Name == current.Name);
			Selected = i >= 0 ? i : 0;
		}

		public static void Save(Stamp st)
		{
			Directory.CreateDirectory(Folder);
			// (written whole, then in the file's place in one step - SafeFile: a crash mid-save left a broken stamp, AU41)
			var bytes = new MemoryStream();
			using (var w = new BinaryWriter(bytes))
			{
				w.Write(Magic); w.Write(1); w.Write(Size);
				for (int z = 0; z < Size; z++) for (int x = 0; x < Size; x++) w.Write(st.Heights[z, x]);
			}
			SafeFile.WriteAllBytes(Path.Combine(Folder, st.Name + ".stamp"), bytes.ToArray());
		}

		/// <summary>Height of a stamp at (u, v) in -1..1 (bilinear), in units of its radius.</summary>
		public static float Sample(Stamp st, float u, float v)
		{
			float fx = (u + 1f) * 0.5f * (Size - 1), fz = (v + 1f) * 0.5f * (Size - 1);
			int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, Size - 2), z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, Size - 2);
			float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
			float a = Mathf.Lerp(st.Heights[z0, x0], st.Heights[z0, x0 + 1], tx), b = Mathf.Lerp(st.Heights[z0 + 1, x0], st.Heights[z0 + 1, x0 + 1], tx);
			return Mathf.Lerp(a, b, tz);
		}

		/// <summary>Adds a stamp to the terrain at a world point, as big as the radius, turned by degrees.</summary>
		public static void Apply(Terrain terrain, Stamp st, Vector3 world, float radius, float degrees)
		{
			TerrainData data = terrain.terrainData;
			int res = data.heightmapResolution;
			float spacing = data.size.x / (res - 1);
			Vector3 local = world - terrain.transform.position;
			float cx = local.x / spacing, cz = local.z / spacing, rs = Mathf.Max(1f, radius / spacing);
			int x0 = Mathf.Clamp(Mathf.FloorToInt(cx - rs), 0, res - 1), z0 = Mathf.Clamp(Mathf.FloorToInt(cz - rs), 0, res - 1);
			int x1 = Mathf.Clamp(Mathf.CeilToInt(cx + rs), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt(cz + rs), 0, res - 1);
			if (x1 <= x0 || z1 <= z0) return;
			float[,] h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
			float cos = Mathf.Cos(-degrees * Mathf.Deg2Rad), sin = Mathf.Sin(-degrees * Mathf.Deg2Rad);
			for (int z = 0; z <= z1 - z0; z++)
				for (int x = 0; x <= x1 - x0; x++)
				{
					float du = (x0 + x - cx) / rs, dv = (z0 + z - cz) / rs, d = Mathf.Sqrt(du * du + dv * dv);
					if (d >= 1f) continue;
					float u = du * cos - dv * sin, v = du * sin + dv * cos;
					float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, d)); // no step at the rim
					h[z, x] = Mathf.Clamp01(h[z, x] + Sample(st, u, v) * radius * edge / data.size.y);
				}
			data.SetHeights(x0, z0, h);
		}
	}
}
