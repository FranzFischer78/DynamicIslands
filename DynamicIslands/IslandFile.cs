using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// One object placed on an island. Position/rotation/scale are local to the island root.
	/// </summary>
	public class IslandObject
	{
		public string Name;
		public Vector3 Position;
		public Vector3 EulerRotation;
		public Vector3 Scale = Vector3.one;
		/// <summary>
		/// Extra data of this object (format 4), or null: what makes it a creature spawn point or a readable note, its
		/// tint... Keys and values are plain strings, see ObjectProps.
		/// </summary>
		public Dictionary<string, string> Props;
		/// <summary>
		/// The object's own number in its island (ROADMAP R1b): kept through edits, so a saved world finds what was used
		/// on it after objects before it were removed or added. 0 = not given yet (a new object: the save gives one). See StableIds.
		/// </summary>
		public int Uid;
		/// <summary>
		/// Not saved. True when the generator placed this piece before Raft's piece was loaded, so Scale still lacks the
		/// piece's own scale (the radio tower's floor is 2 x 1 x 3). PlaceableCatalog.ScaleUnloaded fixes it before saving.
		/// </summary>
		public bool Unscaled;
	}

	/// <summary>
	/// A saved island: terrain heightmap + placed objects.
	///
	/// File layout (.island):
	///   4 bytes  magic "CISL"
	///   int32    format version
	///   rest     deflate-compressed body:
	///     string   island name
	///     float    water level (editor Y that becomes sea level in game)
	///     vector3  terrain size
	///     int32    heightmap resolution (N)
	///     uint16[N*N] heights, row-major [y, x], 0..65535 = 0..1
	///     int32    object count, then per object: string name, vector3 pos, vector3 euler, vector3 scale
	///   version 2 adds, after the objects:
	///     bool     has texture paint; if true:
	///       int32  alphamap resolution (R), int32 layer count (L)
	///       byte[L*R*R] layer weights, layer-major then row-major [z, x], 0..255
	///       bool   has paint mask; if true: byte[R*R] (255 = painted by hand, protected from auto texturing)
	///   version 3 adds, after the paint:
	///     float    elevation: metres above sea level the island floats at in game (negative = under water).
	///     string   style ("" = tropical; "Snowy", "Desert", "Forest", "Volcanic")
	///              Normal tropical islands (elevation 0, no style) are still written as version 2 so older versions
	///              of the mod can read them.
	///   version 4 adds, after the style (only written when the island or one of its objects has properties):
	///     int32    island property count, then per property: string key, string value (IslandProps: author, description...)
	///     int32    count of objects with properties, then per object: int32 index in the object list above,
	///              int32 property count, then per property: string key, string value (creatures, notes, tints: ObjectProps)
	///   tagged tail blocks this version writes (see Tail):
	///     "mix"    a second style mixed in as texture layers 5-8 (ROADMAP E6): string style name, int32 alphamap
	///              resolution (R), byte[4*R*R] weights of layers 5-8, layer-major then row-major. The paint above then
	///              holds layers 1-4 with layers 5-8 added onto them (5 onto 1, 6 onto 2...), so a version without mixed
	///              styles shows the first style's matching texture there; this version takes layers 5-8 back out.
	///     "uid"    stable object ids (ROADMAP R1b, StableIds): int32 version, int32 count, int32 next id, int32 hash of
	///              the object names, int32 id per object. Written only when the ids are not 1..n; a file without it
	///              (or with a wrong count or hash) numbers its objects by place.
	/// </summary>
	public class IslandFile
	{
		public const string Extension = ".island";
		const uint Magic = 0x4C534943; // "CISL" little-endian
		public const int FormatVersion = 4;
		/// <summary>At most this many objects in one island: more is a broken or harmful file (the densest generated islands have about 11,000).</summary>
		public const int MaxObjects = 100000;

		/// <summary>Editor Y coordinate that is treated as sea level when spawned in game.</summary>
		public const float DefaultWaterLevel = 20f;
		/// <summary>
		/// Water level of islands generated on a deep sea floor like Raft's own: its islands rise from a floor about
		/// 150-165 m down (measured by CIMeasureUnderwater), so the terrain's base is that far below the sea.
		/// </summary>
		public const float DeepWaterLevel = 160f;

		public string Name = "";
		public float WaterLevel = DefaultWaterLevel;
		public Vector3 TerrainSize;
		public int HeightmapResolution;
		public float[,] Heights; // [y, x], 0..1, same layout as TerrainData.GetHeights
		public List<IslandObject> Objects = new List<IslandObject>();

		/// <summary>Texture paint (null when the island only uses automatic texturing, e.g. version 1 files).</summary>
		public int AlphamapResolution;
		public int AlphamapLayers;
		public byte[] Alphamaps;   // [layer][z][x]
		public byte[] PaintMask;   // [z][x], may be null

		/// <summary>Metres above sea level the island floats at in game (negative = under water, 0 = a normal island).</summary>
		public float Elevation;

		/// <summary>Island style (TerrainPainter.Styles: "Snowy", "Desert"...); empty = tropical.</summary>
		public string Style = "";

		/// <summary>
		/// A second style mixed in (ROADMAP E6): its four textures are paint layers 5-8 (AlphamapLayers 8), saved in the
		/// tail's "mix" block. Empty = none.
		/// </summary>
		public string MixStyle = "";

		/// <summary>The tail tag of the mixed style's paint (layers 5-8).</summary>
		public const string MixTag = "mix";

		public bool HasMix { get { return !string.IsNullOrEmpty(MixStyle) && HasPaint && AlphamapLayers == TerrainPainter.MixLayerCount; } }

		/// <summary>Island-wide settings (format 4), see IslandProps for the keys.</summary>
		public Dictionary<string, string> Props = new Dictionary<string, string>();

		bool NeedsFormat4 { get { return Props.Count > 0 || Objects.Any(o => o.Props != null && o.Props.Count > 0); } }

		bool NeedsFormat3 { get { return Elevation != 0f || (!string.IsNullOrEmpty(Style) && TerrainPainter.StyleIndex(Style) != TerrainPainter.Tropical); } }

		public bool HasPaint { get { return Alphamaps != null && AlphamapResolution > 0 && AlphamapLayers > 0; } }

		/// <summary>
		/// The tagged tail (ROADMAP R12: forward compatibility): after format 4's data, any number of "tag, length, bytes"
		/// blocks, ended by an empty tag. What a later version of the mod adds goes here under a tag of its own instead of a
		/// new format number, so this version still opens the file: tags it doesn't know are skipped, and kept as they are
		/// when the island is saved again (nothing a newer version wrote is lost). Empty: nothing is written (files as before).
		/// </summary>
		public Dictionary<string, byte[]> Tail = new Dictionary<string, byte[]>();

		/// <summary>The number the next new object gets (R1b, StableIds): never one a removed object had.</summary>
		public int NextUid = 1;

		public void Save(string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
			string tmp = path + ".tmp";
			using (var file = File.Create(tmp))
			{
				var header = new BinaryWriter(file);
				header.Write(Magic);
				// (a mixed style: four layers in the paint block as before, layers 5-8 in the tail)
				byte[] paint = Alphamaps;
				int paintLayers = AlphamapLayers;
				var tail = new Dictionary<string, byte[]>(Tail ?? new Dictionary<string, byte[]>());
				tail.Remove(MixTag);
				// (the objects' own numbers: only when they aren't simply 1, 2, 3... - such files stay as before)
				StableIds.WriteTail(this, tail);
				if (HasPaint && AlphamapLayers > TerrainPainter.LayerCount)
				{
					byte[] extra;
					paint = FoldPaint(out extra);
					paintLayers = TerrainPainter.LayerCount;
					if (HasMix) tail[MixTag] = MixBlock(extra);
				}
				// Each file uses the oldest format that holds what it needs, so simple islands stay readable by older versions of the mod
				bool v4 = NeedsFormat4 || tail.Count > 0, v3 = v4 || NeedsFormat3;
				header.Write(v4 ? 4 : v3 ? 3 : 2);
				header.Flush();

				using (var deflate = new DeflaterOutputStream(file) { IsStreamOwner = false })
				using (var w = new BinaryWriter(deflate))
				{
					w.Write(Name ?? "");
					w.Write(WaterLevel);
					WriteVector(w, TerrainSize);
					w.Write(HeightmapResolution);
					for (int y = 0; y < HeightmapResolution; y++)
						for (int x = 0; x < HeightmapResolution; x++)
							w.Write((ushort)Mathf.RoundToInt(Mathf.Clamp01(Heights[y, x]) * ushort.MaxValue));

					w.Write(Objects.Count);
					foreach (IslandObject o in Objects)
					{
						w.Write(o.Name ?? "");
						WriteVector(w, o.Position);
						WriteVector(w, o.EulerRotation);
						WriteVector(w, o.Scale);
					}

					w.Write(HasPaint);
					if (HasPaint)
					{
						w.Write(AlphamapResolution);
						w.Write(paintLayers);
						w.Write(paint);
						w.Write(PaintMask != null);
						if (PaintMask != null) w.Write(PaintMask);
					}
					if (v3) { w.Write(Elevation); w.Write(Style ?? ""); }
					if (v4)
					{
						WriteProps(w, Props);
						List<int> withProps = Enumerable.Range(0, Objects.Count).Where(i => Objects[i].Props != null && Objects[i].Props.Count > 0).ToList();
						w.Write(withProps.Count);
						foreach (int i in withProps) { w.Write(i); WriteProps(w, Objects[i].Props); }
						if (tail.Count > 0)
						{
							foreach (var kv in tail.Where(kv => !string.IsNullOrEmpty(kv.Key)))
							{
								byte[] data = kv.Value ?? new byte[0];
								w.Write(kv.Key); w.Write(data.Length); w.Write(data);
							}
							w.Write("");
						}
					}
					w.Flush();
					deflate.Finish();
				}
			}
			// (in the old file's place in one step: a crash mid-save never destroys the previous file)
			SafeFile.Commit(tmp, path);
			IslandCache.ForgetFile(path);
		}

		public static IslandFile Load(string path)
		{
			SafeFile.Recover(path); // (a save that Raft stopped half way)
			using (var file = File.OpenRead(path)) return Read(file, path);
		}

		/// <summary>
		/// For a worker thread: the file read at once (its handle closed before unpacking, so a save on the main thread isn't
		/// held up) and nothing deleted - SafeFile.Recover is the caller's, on the main thread (review 2026-10-06).
		/// </summary>
		public static IslandFile LoadOffThread(string path)
		{
			byte[] bytes = File.ReadAllBytes(path);
			using (var m = new MemoryStream(bytes)) return Read(m, path);
		}

		/// <summary>An island file's bytes (a library update compares it with the one here before writing it).</summary>
		public static IslandFile FromBytes(byte[] bytes, string label)
		{
			using (var m = new MemoryStream(bytes)) return Read(m, label);
		}

		static IslandFile Read(Stream file, string path)
		{
			{
				var header = new BinaryReader(file);
				if (header.ReadUInt32() != Magic)
					throw new InvalidDataException(path + " is not a Custom Islands .island file");
				int version = header.ReadInt32();
				if (version > FormatVersion)
					throw new InvalidDataException(path + " was saved by a newer version of Custom Islands (format " + version + ")");

				using (var inflate = new InflaterInputStream(file) { IsStreamOwner = false })
				using (var r = new BinaryReader(inflate))
				{
					var island = new IslandFile();
					island.Name = r.ReadString();
					island.WaterLevel = r.ReadSingle();
					island.TerrainSize = ReadVector(r);
					int res = r.ReadInt32();
					if (res <= 0 || res > 4097)
						throw new InvalidDataException("Invalid heightmap resolution " + res);
					island.HeightmapResolution = res;
					island.Heights = new float[res, res];
					for (int y = 0; y < res; y++)
						for (int x = 0; x < res; x++)
							island.Heights[y, x] = r.ReadUInt16() / (float)ushort.MaxValue;

					int count = r.ReadInt32();
					if (count < 0 || count > MaxObjects) throw new InvalidDataException("Invalid object count " + count + " (at most " + MaxObjects + ")");
					for (int i = 0; i < count; i++)
					{
						island.Objects.Add(new IslandObject
						{
							Name = r.ReadString(),
							Position = ReadVector(r),
							EulerRotation = ReadVector(r),
							Scale = ReadVector(r),
						});
					}

					if (version >= 2 && r.ReadBoolean())
					{
						int ares = r.ReadInt32(), layers = r.ReadInt32();
						if (ares < 16 || ares > 4096 || layers < 1 || layers > 16)
							throw new InvalidDataException("Invalid texture paint data (" + ares + " px, " + layers + " layers)");
						island.AlphamapResolution = ares;
						island.AlphamapLayers = layers;
						island.Alphamaps = ReadExactly(r, layers * ares * ares);
						if (r.ReadBoolean()) island.PaintMask = ReadExactly(r, ares * ares);
					}
					if (version >= 3)
					{
						island.Elevation = r.ReadSingle();
						// The first format 3 files (flying islands, before styles) end after the elevation
						try { island.Style = r.ReadString(); } catch (EndOfStreamException) { }
					}
					if (version >= 4)
					{
						island.Props = ReadProps(r);
						int withProps = r.ReadInt32();
						if (withProps < 0 || withProps > island.Objects.Count) throw new InvalidDataException("Invalid object property count " + withProps);
						for (int n = 0; n < withProps; n++)
						{
							int i = r.ReadInt32();
							Dictionary<string, string> props = ReadProps(r);
							if (i >= 0 && i < island.Objects.Count) island.Objects[i].Props = props;
						}
						// The tagged tail, if any (files without one end here)
						try
						{
							for (int n = 0; n < 4096; n++)
							{
								string tag = r.ReadString();
								if (tag.Length == 0) break;
								int len = r.ReadInt32();
								if (len < 0 || len > 256 * 1024 * 1024) throw new InvalidDataException("Invalid tail block '" + tag + "' (" + len + " bytes)");
								island.Tail[tag] = ReadExactly(r, len);
							}
						}
						catch (EndOfStreamException) { }
						island.ReadMix();
					}
					StableIds.ReadTail(island);
					return island;
				}
			}
		}

		/// <summary>
		/// The paint as saved: layers 1-4 with layers 5-8 added onto the matching ones (what an older version shows), and
		/// layers 5-8 on their own (extra).
		/// </summary>
		byte[] FoldPaint(out byte[] extra)
		{
			int n = AlphamapResolution * AlphamapResolution, four = TerrainPainter.LayerCount;
			var folded = new byte[four * n];
			extra = new byte[four * n];
			for (int l = 0; l < AlphamapLayers; l++)
				for (int p = 0; p < n; p++)
				{
					int slot = l % four;
					folded[slot * n + p] = (byte)Math.Min(255, folded[slot * n + p] + Alphamaps[l * n + p]);
					if (l >= four && l < four * 2) extra[(l - four) * n + p] = Alphamaps[l * n + p];
				}
			return folded;
		}

		byte[] MixBlock(byte[] extra)
		{
			using (var m = new MemoryStream())
			{
				using (var w = new BinaryWriter(m))
				{
					w.Write(MixStyle ?? "");
					w.Write(AlphamapResolution);
					w.Write(extra);
				}
				return m.ToArray();
			}
		}

		/// <summary>The tail's "mix" block, if any: the mixed style, and the paint back to eight layers (it is taken out of Tail, and written again from the paint on Save).</summary>
		void ReadMix()
		{
			byte[] block;
			if (Tail == null || !Tail.TryGetValue(MixTag, out block)) return;
			Tail.Remove(MixTag);
			try
			{
				using (var r = new BinaryReader(new MemoryStream(block)))
				{
					string style = r.ReadString();
					int res = r.ReadInt32(), four = TerrainPainter.LayerCount, n = res * res;
					if (string.IsNullOrEmpty(style) || !HasPaint || res != AlphamapResolution || AlphamapLayers != four) return;
					byte[] extra = ReadExactly(r, four * n);
					var all = new byte[four * 2 * n];
					for (int l = 0; l < four; l++)
						for (int p = 0; p < n; p++)
						{
							all[l * n + p] = (byte)Math.Max(0, Alphamaps[l * n + p] - extra[l * n + p]);
							all[(l + four) * n + p] = extra[l * n + p];
						}
					Alphamaps = all;
					AlphamapLayers = four * 2;
					MixStyle = style;
				}
			}
			catch (Exception e) when (e is EndOfStreamException || e is InvalidDataException)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] The island's mixed style couldn't be read (" + e.Message + "): its first style's textures are shown");
			}
		}

		/// <summary>Captures terrain + every EditorGameObject under placedObjectsRoot.</summary>
		public static IslandFile Capture(string name, Terrain terrain, Transform placedObjectsRoot, float[,] paintMask = null)
		{
			// (the sea level of the island being edited: shallow seabed or Raft's deep sea floor)
			var island = new IslandFile { Name = name, WaterLevel = DynamicIslands.EditorWaterLevel };
			TerrainData data = terrain.terrainData;
			island.TerrainSize = data.size;
			island.HeightmapResolution = data.heightmapResolution;
			island.Heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
			CaveGround.EditorOwnHeights(terrain, island.Heights); // (not as let down in its caves' rooms: that's made again when it's loaded or spawned - X7)

			if (data.alphamapLayers > 0)
			{
				int res = data.alphamapResolution, layers = data.alphamapLayers;
				float[,,] maps = data.GetAlphamaps(0, 0, res, res);
				island.AlphamapResolution = res;
				island.AlphamapLayers = layers;
				island.Alphamaps = new byte[layers * res * res];
				for (int l = 0; l < layers; l++)
					for (int z = 0; z < res; z++)
						for (int x = 0; x < res; x++)
							island.Alphamaps[(l * res + z) * res + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(maps[z, x, l]) * 255f);
				if (paintMask != null && paintMask.GetLength(0) == res && paintMask.GetLength(1) == res)
				{
					island.PaintMask = new byte[res * res];
					for (int z = 0; z < res; z++)
						for (int x = 0; x < res; x++)
							island.PaintMask[z * res + x] = paintMask[z, x] > 0.5f ? (byte)255 : (byte)0;
				}
			}

			foreach (EditorGameObject ego in placedObjectsRoot.GetComponentsInChildren<EditorGameObject>())
			{
				Transform t = ego.transform;
				island.Objects.Add(new IslandObject
				{
					Name = ego.GameObjectName,
					Props = ego.Props != null && ego.Props.Count > 0 ? new Dictionary<string, string>(ego.Props) : null,
					// Store relative to the terrain so the island can be spawned anywhere
					Position = t.position - terrain.transform.position,
					EulerRotation = t.rotation.eulerAngles,
					Scale = t.lossyScale,
					Uid = ego.Uid,
				});
			}
			return island;
		}

		/// <summary>
		/// Texture weights for a square block of the alphamap, normalised so the layers at each pixel sum to 1.
		/// Returns [z, x, layer] as Unity's SetAlphamaps expects.
		/// </summary>
		public float[,,] GetAlphamapBlock(int x0, int z0, int size)
		{
			int res = AlphamapResolution, layers = AlphamapLayers;
			var maps = new float[size, size, layers];
			for (int z = 0; z < size; z++)
				for (int x = 0; x < size; x++)
				{
					float sum = 0;
					for (int l = 0; l < layers; l++) sum += Alphamaps[(l * res + z0 + z) * res + x0 + x];
					for (int l = 0; l < layers; l++)
						maps[z, x, l] = sum > 0 ? Alphamaps[(l * res + z0 + z) * res + x0 + x] / sum : (l == 0 ? 1f : 0f);
				}
			return maps;
		}

		/// <summary>The paint mask as floats (1 = painted by hand), or null.</summary>
		public float[,] GetPaintMask()
		{
			if (PaintMask == null) return null;
			int res = AlphamapResolution;
			var mask = new float[res, res];
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					mask[z, x] = PaintMask[z * res + x] / 255f;
			return mask;
		}

		static byte[] ReadExactly(BinaryReader r, int count)
		{
			byte[] b = r.ReadBytes(count);
			if (b.Length != count) throw new InvalidDataException("Island file is truncated");
			return b;
		}

		static void WriteProps(BinaryWriter w, Dictionary<string, string> props)
		{
			w.Write(props.Count);
			foreach (var kv in props.OrderBy(k => k.Key, StringComparer.Ordinal)) { w.Write(kv.Key ?? ""); w.Write(kv.Value ?? ""); }
		}

		/// <summary>
		/// Only the island's own settings (Props), the rest of the file passed over unread into memory: Tidy up, Delete and
		/// Rename read every island's rules, and a whole Load (heights, objects, paint) cost 4-10 ms an island. Files older
		/// than format 4 have no settings and end at the header.
		/// </summary>
		public static Dictionary<string, string> LoadProps(string path)
		{
			SafeFile.Recover(path);
			using (var file = File.OpenRead(path))
			{
				var header = new BinaryReader(file);
				if (header.ReadUInt32() != Magic) throw new InvalidDataException(path + " is not a Custom Islands .island file");
				int version = header.ReadInt32();
				if (version > FormatVersion) throw new InvalidDataException(path + " was saved by a newer version of Custom Islands (format " + version + ")");
				if (version < 4) return new Dictionary<string, string>();
				using (var inflate = new InflaterInputStream(file) { IsStreamOwner = false })
				using (var r = new BinaryReader(inflate))
				{
					byte[] buffer = new byte[65536];
					Action<long> skip = n =>
					{
						while (n > 0)
						{
							int got = r.Read(buffer, 0, (int)Math.Min(n, buffer.Length));
							if (got <= 0) throw new EndOfStreamException();
							n -= got;
						}
					};
					r.ReadString(); r.ReadSingle(); skip(12);
					int res = r.ReadInt32();
					if (res <= 0 || res > 4097) throw new InvalidDataException("Invalid heightmap resolution " + res);
					skip((long)res * res * 2);
					int count = r.ReadInt32();
					if (count < 0 || count > MaxObjects) throw new InvalidDataException("Invalid object count " + count + " (at most " + MaxObjects + ")");
					for (int i = 0; i < count; i++) { r.ReadString(); skip(36); }
					if (r.ReadBoolean())
					{
						int ares = r.ReadInt32(), layers = r.ReadInt32();
						if (ares < 16 || ares > 4096 || layers < 1 || layers > 16) throw new InvalidDataException("Invalid texture paint data (" + ares + " px, " + layers + " layers)");
						skip((long)layers * ares * ares);
						if (r.ReadBoolean()) skip((long)ares * ares);
					}
					r.ReadSingle(); r.ReadString();
					return ReadProps(r);
				}
			}
		}

		static Dictionary<string, string> ReadProps(BinaryReader r)
		{
			int n = r.ReadInt32();
			if (n < 0 || n > 10000) throw new InvalidDataException("Invalid property count " + n);
			var props = new Dictionary<string, string>();
			for (int i = 0; i < n; i++) { string k = r.ReadString(); props[k] = r.ReadString(); }
			return props;
		}

		static void WriteVector(BinaryWriter w, Vector3 v)
		{
			w.Write(v.x); w.Write(v.y); w.Write(v.z);
		}

		static Vector3 ReadVector(BinaryReader r)
		{
			return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
		}
	}
}
