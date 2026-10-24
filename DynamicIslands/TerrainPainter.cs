using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>Which island style a terrain is textured with (see TerrainPainter.Styles).</summary>
	public class IslandStyleTag : MonoBehaviour
	{
		public int Style;
		/// <summary>A second style mixed in as texture layers 5-8 (ROADMAP E6), or -1 for none.</summary>
		public int Mix = -1;
	}

	/// <summary>
	/// Terrain texturing. Four layers ("slots"): seabed, shore, ground and steep. By default every pixel is painted
	/// automatically from height and slope; pixels painted by hand in the editor are marked in a paint mask
	/// (1 = hand-painted) and are left alone by the automatic painter. Weights and mask are saved in the
	/// .island file (format 2); older files fall back to automatic texturing.
	///
	/// Island styles (roadmap 1.6) decide which of Raft's ground textures fill the four slots: tropical (Raft's
	/// ordinary islands), snowy (Temperance), desert (Caravan Island), forest (Balboa) and volcanic (darkened
	/// desert and Temperance rock). The textures are borrowed from those islands' terrains while the object
	/// catalog has their scenes loaded; a style whose textures are missing falls back to tropical.
	/// </summary>
	public static class TerrainPainter
	{
		public const int Seabed = 0, Sand = 1, Grass = 2, Rock = 3;
		public const int LayerCount = 4;
		public static readonly string[] LayerNames = { "Seabed", "Sand", "Grass", "Rock" };
		const int AlphamapResolution = 512;

		#region Styles

		public class Style
		{
			public string Name;
			/// <summary>Diffuse texture names for the seabed / shore / ground / steep slots.</summary>
			public string[] Textures;
			/// <summary>Button labels for the slots in the editor.</summary>
			public string[] Labels;
			/// <summary>Colour multiplied into each slot's texture (white = unchanged).</summary>
			public Color[] Tints;
			public float Tile = 8f;
		}

		public const int Tropical = 0, Snowy = 1, Desert = 2, Forest = 3, Volcanic = 4;

		static readonly Color W = Color.white;
		public static readonly Style[] Styles =
		{
			new Style { Name = "Tropical", Textures = new[] { "Dirt_basecolor", "Clean_Sand_basecolor", "Grass_basecolor", "Rock_Stylized_basecolor" },
				Labels = new[] { "Seabed", "Sand", "Grass", "Rock" }, Tints = new[] { W, W, W, W } },
			new Style { Name = "Snowy", Textures = new[] { "TP_Rock_basecolor", "TP_Snow_w_Rocks_basecolor", "TP_Snow_basecolor", "TP_Rock_basecolor" },
				Labels = new[] { "Seabed", "Rocky", "Snow", "Rock" }, Tints = new[] { new Color(0.7f, 0.72f, 0.75f), W, W, W }, Tile = 10f },
			new Style { Name = "Desert", Textures = new[] { "CaravanIsland_GravelGrayGroundTex_basecolor", "CaravanIsland_SandGroundTex_basecolor", "CaravanIsland_GravelGroundTex_basecolor", "Red_Rock_Side_basecolor" },
				Labels = new[] { "Gravel", "Sand", "Ground", "Red rock" }, Tints = new[] { W, W, W, W }, Tile = 10f },
			new Style { Name = "Forest", Textures = new[] { "Balboa_Dirt_basecolor", "Clean_Sand_basecolor", "Balboa_Grass_basecolor", "Rock_Stylized_basecolor" },
				Labels = new[] { "Seabed", "Sand", "Grass", "Rock" }, Tints = new[] { W, W, W, W } },
			new Style { Name = "Volcanic", Textures = new[] { "CaravanIsland_GravelGrayGroundTex_basecolor", "CaravanIsland_GravelGrayGroundTex_basecolor", "CaravanIsland_RockGroundTex_basecolor", "TP_Rock_basecolor" },
				Labels = new[] { "Seabed", "Cinders", "Ash", "Basalt" },
				Tints = new[] { new Color(0.42f, 0.4f, 0.38f), new Color(0.3f, 0.29f, 0.28f), new Color(0.38f, 0.35f, 0.33f), new Color(0.45f, 0.3f, 0.26f) }, Tile = 10f },
		};

		public static int StyleIndex(string name)
		{
			if (string.IsNullOrEmpty(name)) return Tropical;
			int i = Array.FindIndex(Styles, s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			return i < 0 ? Tropical : i;
		}

		public static string StyleName(int style) { return Styles[Mathf.Clamp(style, 0, Styles.Length - 1)].Name; }

		/// <summary>Editor button label for a paint slot in this style ("Snow", "Red rock"...).</summary>
		public static string SlotLabel(int style, int slot) { return Styles[Mathf.Clamp(style, 0, Styles.Length - 1)].Labels[slot]; }

		public static int StyleOf(Terrain terrain)
		{
			IslandStyleTag tag = terrain != null ? terrain.GetComponent<IslandStyleTag>() : null;
			return tag != null ? tag.Style : Tropical;
		}

		/// <summary>The second style mixed into the terrain (texture layers 5-8), or -1.</summary>
		public static int MixOf(Terrain terrain)
		{
			IslandStyleTag tag = terrain != null ? terrain.GetComponent<IslandStyleTag>() : null;
			return tag != null ? tag.Mix : -1;
		}

		/// <summary>Mixed in: a second style's four textures as layers 5-8 (the same slots: seabed, shore, ground, steep).</summary>
		public const int MixLayerCount = LayerCount * 2;

		/// <summary>
		/// Sets the terrain's style and swaps its texture layers (paint weights stay as they are). mix: a second style mixed
		/// in as layers 5-8, -1 for none, -2 to keep the terrain's.
		/// </summary>
		public static void SetStyle(Terrain terrain, int style, int mix = -2)
		{
			IslandStyleTag tag = terrain.GetComponent<IslandStyleTag>() ?? terrain.gameObject.AddComponent<IslandStyleTag>();
			tag.Style = Mathf.Clamp(style, 0, Styles.Length - 1);
			int to = mix == -2 ? tag.Mix : mix < 0 ? -1 : Mathf.Clamp(mix, 0, Styles.Length - 1);
			if (to == tag.Mix) { RefreshLayers(terrain); return; }
			// (four layers to eight or back: the paint is kept, layers 5-8 folded onto the first style's matching ones)
			TerrainData data = terrain.terrainData;
			bool painted = data.terrainLayers != null && data.terrainLayers.Length > 0;
			float[,,] paint = painted ? data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight) : null;
			tag.Mix = to;
			if (!painted) { RefreshLayers(terrain); return; }
			data.terrainLayers = LayersFor(tag.Style, to);
			ApplyMaterial(terrain);
			data.SetAlphamaps(0, 0, FitLayers(paint, data.alphamapLayers));
		}

		/// <summary>
		/// Texture weights with another number of layers: layers past four fold onto the matching first four (8 to 4), new
		/// ones start empty (4 to 8).
		/// </summary>
		public static float[,,] FitLayers(float[,,] maps, int layers)
		{
			int have = maps.GetLength(2);
			if (have == layers || layers <= 0) return maps;
			int rows = maps.GetLength(0), cols = maps.GetLength(1);
			var result = new float[rows, cols, layers];
			for (int z = 0; z < rows; z++)
				for (int x = 0; x < cols; x++)
					for (int l = 0; l < have; l++)
						result[z, x, l < layers ? l : l % LayerCount] += maps[z, x, l];
			return result;
		}

		/// <summary>True if Raft's textures for this style were found (otherwise it falls back to tropical).</summary>
		public static bool HasStyle(int style) { return BuildStyle(style) != null; }

		#endregion

		#region Raft's textures

		static TerrainLayer[] layers; // procedural fallback
		/// <summary>Every ground texture layer seen on Raft's islands, by diffuse texture name, with its footstep type.</summary>
		static readonly Dictionary<string, KeyValuePair<TerrainLayer, SO_TerrainType>> raftTextures = new Dictionary<string, KeyValuePair<TerrainLayer, SO_TerrainType>>();
		static readonly Dictionary<int, TerrainLayer[]> styleLayers = new Dictionary<int, TerrainLayer[]>();
		static readonly Dictionary<int, SO_TerrainTypeGroup> styleTypes = new Dictionary<int, SO_TerrainTypeGroup>();
		static SO_TerrainTypeGroup defaultTypes;
		static Material terrainMaterial;

		/// <summary>True once Raft's own tropical ground textures were borrowed from one of its islands (see PlaceableCatalog).</summary>
		public static bool HasRaftTextures { get { return BuildStyle(Tropical) != null; } }

		/// <summary>
		/// Remembers the texture layers of a Raft island terrain (and their footstep types) for the styles. Returns true
		/// if anything new was found.
		/// </summary>
		public static bool UseRaftTextures(TerrainLayer[] source, TerrainIdentifier identifier = null)
		{
			if (source == null) return false;
			int added = 0;
			for (int i = 0; i < source.Length; i++)
			{
				TerrainLayer l = source[i];
				if (l == null || l.diffuseTexture == null || raftTextures.ContainsKey(l.diffuseTexture.name)) continue;
				SO_TerrainType type = null;
				try { if (identifier != null && identifier.terrainTypeGroup != null) type = identifier.terrainTypeGroup.GetTerrainType(i); } catch { }
				raftTextures[l.diffuseTexture.name] = new KeyValuePair<TerrainLayer, SO_TerrainType>(l, type);
				added++;
			}
			if (added > 0)
			{
				// Styles may be complete now
				styleLayers.Clear();
				Debug.Log("[CUSTOM ISLANDS] Borrowed " + added + " of Raft's ground textures (styles available: " +
					string.Join(", ", Enumerable.Range(0, Styles.Length).Where(HasStyle).Select(StyleName).ToArray()) + ")");
			}
			return added > 0;
		}

		/// <summary>The four layers for a style, or null if Raft's textures for it haven't been seen.</summary>
		static TerrainLayer[] BuildStyle(int style)
		{
			TerrainLayer[] cached;
			if (styleLayers.TryGetValue(style, out cached)) return cached;
			Style s = Styles[style];
			if (s.Textures.Any(t => !raftTextures.ContainsKey(t))) return null;
			var result = new TerrainLayer[LayerCount];
			var types = new List<SO_TerrainType>();
			for (int slot = 0; slot < LayerCount; slot++)
			{
				var src = raftTextures[s.Textures[slot]];
				float tile = slot == Rock ? s.Tile * 1.25f : s.Tile;
				result[slot] = Copy("CI_" + s.Name + "_" + slot, src.Key, tile, s.Tints[slot]);
				types.Add(src.Value ?? ScriptableObject.CreateInstance<SO_TerrainType>());
			}
			var group = ScriptableObject.CreateInstance<SO_TerrainTypeGroup>();
			group.name = "CI_TerrainTypes_" + s.Name;
			group.groupName = "Custom Islands";
			group.terrainTypes = types;
			styleTypes[style] = group;
			styleLayers[style] = result;
			return result;
		}

		static TerrainLayer Copy(string name, TerrainLayer from, float tile, Color tint)
		{
			return new TerrainLayer
			{
				name = name,
				diffuseTexture = tint == Color.white ? from.diffuseTexture : Tinted(from.diffuseTexture, tint),
				normalMapTexture = from.normalMapTexture,
				normalScale = 1f,
				tileSize = new Vector2(tile, tile),
				smoothness = 0f,
				metallic = 0f,
			};
		}

		static Material tintMaterial;

		/// <summary>
		/// A darkened / tinted copy of a (non-readable) texture, made on the GPU: blit through a tinting material into a
		/// render texture and read that back. Used for the volcanic style, which Raft has no textures of its own for.
		/// </summary>
		static Texture2D Tinted(Texture2D source, Color tint)
		{
			try
			{
				if (tintMaterial == null) tintMaterial = new Material(Shader.Find("Sprites/Default"));
				tintMaterial.color = tint;
				var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
				Graphics.Blit(source, rt, tintMaterial);
				RenderTexture previous = RenderTexture.active;
				RenderTexture.active = rt;
				var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true) { name = source.name + "_tinted", wrapMode = TextureWrapMode.Repeat };
				copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
				copy.Apply(true, true); // mipmaps, then free the CPU copy
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(rt);
				return copy;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Could not tint " + source.name + ": " + e.Message);
				return source;
			}
		}

		/// <summary>Swaps the layers of an already painted terrain to its style's (e.g. when Raft's textures arrive after the editor opened).</summary>
		public static void RefreshLayers(Terrain terrain)
		{
			if (terrain == null) return;
			TerrainData data = terrain.terrainData;
			TerrainLayer[] wanted = LayersFor(StyleOf(terrain), MixOf(terrain));
			if (data.terrainLayers != null && data.terrainLayers.Length == wanted.Length) data.terrainLayers = wanted;
			ApplyMaterial(terrain);
		}

		/// <summary>
		/// Raft's player looks up a TerrainIdentifier on any terrain it walks on (footstep sounds, friction) and throws
		/// every frame without one. Uses the style's footstep types from Raft when borrowed, otherwise neutral defaults.
		/// </summary>
		public static void EnsureIdentifier(Terrain terrain)
		{
			int style = StyleOf(terrain);
			SO_TerrainTypeGroup group;
			if (BuildStyle(style) == null || !styleTypes.TryGetValue(style, out group))
			{
				if (BuildStyle(Tropical) == null || !styleTypes.TryGetValue(Tropical, out group))
				{
					if (defaultTypes == null)
					{
						defaultTypes = ScriptableObject.CreateInstance<SO_TerrainTypeGroup>();
						defaultTypes.name = "CI_TerrainTypes_Default";
						defaultTypes.groupName = "Custom Islands";
						defaultTypes.terrainTypes = new List<SO_TerrainType>();
						for (int i = 0; i < LayerCount; i++) defaultTypes.terrainTypes.Add(ScriptableObject.CreateInstance<SO_TerrainType>());
					}
					group = defaultTypes;
				}
			}
			// (a mixed style's layers 5-8 step like that style's ground: Raft looks up a footstep type for every layer)
			int mix = MixOf(terrain);
			if (mix >= 0)
			{
				SO_TerrainTypeGroup second;
				if (BuildStyle(mix) == null || !styleTypes.TryGetValue(mix, out second)) second = group;
				group = MixedTypes(group, second);
			}
			TerrainIdentifier id = terrain.GetComponent<TerrainIdentifier>() ?? terrain.gameObject.AddComponent<TerrainIdentifier>();
			id.terrainTypeGroup = group;
		}

		static readonly Dictionary<KeyValuePair<SO_TerrainTypeGroup, SO_TerrainTypeGroup>, SO_TerrainTypeGroup> mixedTypes = new Dictionary<KeyValuePair<SO_TerrainTypeGroup, SO_TerrainTypeGroup>, SO_TerrainTypeGroup>();

		/// <summary>Footstep types for eight layers: the first style's four, then the mixed style's.</summary>
		static SO_TerrainTypeGroup MixedTypes(SO_TerrainTypeGroup first, SO_TerrainTypeGroup second)
		{
			var key = new KeyValuePair<SO_TerrainTypeGroup, SO_TerrainTypeGroup>(first, second);
			SO_TerrainTypeGroup group;
			if (mixedTypes.TryGetValue(key, out group) && group != null) return group;
			group = ScriptableObject.CreateInstance<SO_TerrainTypeGroup>();
			group.name = first.name + "_mixed";
			group.groupName = "Custom Islands";
			group.terrainTypes = new List<SO_TerrainType>();
			for (int i = 0; i < MixLayerCount; i++)
			{
				List<SO_TerrainType> from = i < LayerCount ? first.terrainTypes : second.terrainTypes;
				group.terrainTypes.Add(from != null && from.Count > i % LayerCount ? from[i % LayerCount] : ScriptableObject.CreateInstance<SO_TerrainType>());
			}
			mixedTypes[key] = group;
			return group;
		}

		/// <summary>Raft's textures come with normal maps; use the standard terrain shader when Raft includes it.</summary>
		static void ApplyMaterial(Terrain terrain)
		{
			EnsureIdentifier(terrain);
			if (!HasRaftTextures) return;
			if (terrainMaterial == null)
			{
				Shader s = Shader.Find("Nature/Terrain/Standard");
				if (s == null) return;
				terrainMaterial = new Material(s) { name = "CI_Terrain" };
			}
			terrain.materialTemplate = terrainMaterial;
		}

		/// <summary>The tropical layers (or procedural ones when Raft's textures aren't available).</summary>
		public static TerrainLayer[] Layers { get { return LayersFor(Tropical); } }

		/// <summary>The layers for a style: Raft's textures, else tropical, else procedural ones.</summary>
		public static TerrainLayer[] LayersFor(int style)
		{
			TerrainLayer[] l = BuildStyle(style) ?? BuildStyle(Tropical);
			if (l != null) return l;
			if (layers == null || layers[0] == null)
			{
				layers = new[]
				{
					MakeLayer("CI_Seabed", new Color(0.55f, 0.52f, 0.40f), new Color(0.42f, 0.44f, 0.36f), 6f),
					MakeLayer("CI_Sand",   new Color(0.86f, 0.79f, 0.60f), new Color(0.76f, 0.68f, 0.50f), 5f),
					MakeLayer("CI_Grass",  new Color(0.36f, 0.55f, 0.22f), new Color(0.24f, 0.42f, 0.15f), 7f),
					MakeLayer("CI_Rock",   new Color(0.52f, 0.50f, 0.47f), new Color(0.36f, 0.34f, 0.32f), 9f),
				};
			}
			return layers;
		}

		static readonly Dictionary<int, TerrainLayer[]> mixLayers = new Dictionary<int, TerrainLayer[]>();

		/// <summary>
		/// The layers for a style with a second one mixed in: eight, the second style's as copies (so no layer is in the list
		/// twice). mix -1: LayersFor(style).
		/// </summary>
		public static TerrainLayer[] LayersFor(int style, int mix)
		{
			TerrainLayer[] first = LayersFor(style);
			if (mix < 0) return first;
			TerrainLayer[] second = LayersFor(mix), copies;
			int key = Mathf.Clamp(mix, 0, Styles.Length - 1);
			if (!mixLayers.TryGetValue(key, out copies) || copies == null || copies[0] == null || copies[0].diffuseTexture != second[0].diffuseTexture)
			{
				copies = second.Select(l => new TerrainLayer
				{
					name = l.name + "_mix",
					diffuseTexture = l.diffuseTexture,
					normalMapTexture = l.normalMapTexture,
					normalScale = l.normalScale,
					tileSize = l.tileSize,
					smoothness = l.smoothness,
					metallic = l.metallic,
				}).ToArray();
				mixLayers[key] = copies;
			}
			return first.Concat(copies).ToArray();
		}

		#endregion

		/// <summary>Assigns the layers and paints the whole terrain automatically (pixels with mask > 0.5 are kept).</summary>
		public static void Setup(Terrain terrain, float waterLevelWorldY, float[,] mask = null)
		{
			TerrainData data = terrain.terrainData;
			ForgetGrassLine(terrain); // (a new island: its land is measured again)
			EnsureLayers(terrain, AlphamapResolution);
			ApplyMaterial(terrain);
			Paint(terrain, waterLevelWorldY, new RectInt(0, 0, data.alphamapWidth, data.alphamapHeight), mask);
		}

		/// <summary>
		/// Applies a block of saved paint (from IslandFile.GetAlphamapBlock) to the whole terrain. A mixed island's paint has
		/// eight layers: set the terrain's mixed style first (SetStyle), or layers 5-8 fold onto the first four.
		/// </summary>
		public static void ApplySaved(Terrain terrain, float[,,] maps)
		{
			TerrainData data = terrain.terrainData;
			ForgetGrassLine(terrain);
			EnsureLayers(terrain, maps.GetLength(0));
			ApplyMaterial(terrain);
			data.SetAlphamaps(0, 0, FitLayers(maps, data.alphamapLayers));
		}

		static void EnsureLayers(Terrain terrain, int resolution)
		{
			TerrainData data = terrain.terrainData;
			TerrainLayer[] wanted = LayersFor(StyleOf(terrain), MixOf(terrain));
			if (data.alphamapResolution != resolution) data.alphamapResolution = resolution;
			if (data.terrainLayers == null || data.terrainLayers.Length != wanted.Length || data.terrainLayers[0] != wanted[0] || data.terrainLayers[wanted.Length - 1] != wanted[wanted.Length - 1]) data.terrainLayers = wanted;
		}

		/// <summary>Repaints the alphamap pixels covering the given world-space rectangle (x/z min and max).</summary>
		public static void PaintWorldArea(Terrain terrain, float waterLevelWorldY, Vector3 worldMin, Vector3 worldMax, float[,] mask = null)
		{
			TerrainData data = terrain.terrainData;
			if (data.terrainLayers == null || data.terrainLayers.Length < LayerCount) { Setup(terrain, waterLevelWorldY, mask); return; }
			RectInt area;
			if (WorldToAlphamapRect(terrain, worldMin, worldMax, out area))
				Paint(terrain, waterLevelWorldY, area, mask);
		}

		public static bool WorldToAlphamapRect(Terrain terrain, Vector3 worldMin, Vector3 worldMax, out RectInt area)
		{
			TerrainData data = terrain.terrainData;
			Vector3 origin = terrain.transform.position;
			int res = data.alphamapWidth;
			int x0 = Mathf.Clamp(Mathf.FloorToInt((worldMin.x - origin.x) / data.size.x * res), 0, res - 1);
			int z0 = Mathf.Clamp(Mathf.FloorToInt((worldMin.z - origin.z) / data.size.z * res), 0, res - 1);
			int x1 = Mathf.Clamp(Mathf.CeilToInt((worldMax.x - origin.x) / data.size.x * res), 0, res - 1);
			int z1 = Mathf.Clamp(Mathf.CeilToInt((worldMax.z - origin.z) / data.size.z * res), 0, res - 1);
			area = new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
			return x1 >= x0 && z1 >= z0;
		}

		static void Paint(Terrain terrain, float waterLevelWorldY, RectInt area, float[,] mask)
		{
			TerrainData data = terrain.terrainData;
			int count = data.alphamapLayers; // (eight with a mixed style: automatic texturing uses the first style's four)
			float[,,] maps = mask != null ? data.GetAlphamaps(area.x, area.y, area.width, area.height)
				: new float[area.height, area.width, count];
			var w = new float[LayerCount];
			for (int z = 0; z < area.height; z++)
				for (int x = 0; x < area.width; x++)
				{
					if (mask != null && mask[area.y + z, area.x + x] > 0.5f) continue; // painted by hand
					AutoWeights(terrain, waterLevelWorldY, area.x + x, area.y + z, w);
					for (int l = 0; l < count; l++) maps[z, x, l] = l < LayerCount ? w[l] : 0f;
				}
			data.SetAlphamaps(area.x, area.y, maps);
		}

		/// <summary>Automatic layer weights for one alphamap pixel.</summary>
		/// <summary>
		/// Where sand gives way to grass (m above the sea: from - to) on an island with this much land (m²), as on Raft's:
		/// its small islands are green nearly down to the water (bushes at half a metre stand on grass), its big ones have
		/// sandy beaches up to 2.5-5 m. In between it moves with the land's size.
		/// </summary>
		public static Vector2 GrassLine(float landArea)
		{
			float t = Mathf.InverseLerp(Mathf.Log(2000f), Mathf.Log(18000f), Mathf.Log(Mathf.Max(1f, landArea)));
			return new Vector2(Mathf.Lerp(0.4f, 2.5f, t), Mathf.Lerp(1.4f, 5f, t));
		}

		static readonly Dictionary<TerrainData, Vector2> grassLines = new Dictionary<TerrainData, Vector2>();

		/// <summary>The terrain's grass line, from its land (measured once per island, on a coarse grid of its heights).</summary>
		public static Vector2 GrassLineOf(Terrain terrain, float waterLevelWorldY)
		{
			TerrainData data = terrain.terrainData;
			Vector2 line;
			if (grassLines.TryGetValue(data, out line)) return line;
			int res = data.heightmapResolution, step = Mathf.Max(1, (res - 1) / 128);
			float cell = data.size.x / (res - 1) * step, water = (waterLevelWorldY - terrain.transform.position.y) / data.size.y;
			float[,] h = data.GetHeights(0, 0, res, res);
			int land = 0;
			for (int z = 0; z < res; z += step)
				for (int x = 0; x < res; x += step)
					if (h[z, x] > water) land++;
			line = GrassLine(land * cell * cell);
			grassLines[data] = line;
			return line;
		}

		/// <summary>The terrain has another island now: its grass line is measured again when it's next needed.</summary>
		public static void ForgetGrassLine(Terrain terrain) { if (terrain != null) grassLines.Remove(terrain.terrainData); }

		public static void AutoWeights(Terrain terrain, float waterLevelWorldY, int px, int pz, float[] result)
		{
			TerrainData data = terrain.terrainData;
			int res = data.alphamapWidth;
			float nx = (px + 0.5f) / res, nz = (pz + 0.5f) / res;
			float height = terrain.transform.position.y + data.GetInterpolatedHeight(nx, nz) - waterLevelWorldY; // metres above water
			float slope = data.GetSteepness(nx, nz); // degrees

			Vector2 line = GrassLineOf(terrain, waterLevelWorldY);
			float grass = Mathf.InverseLerp(line.x, line.y, height);
			float sand = 1f - grass;
			float rock = Mathf.InverseLerp(28f, 42f, slope);

			float keep = 1f - rock;
			result[Seabed] = 0f;
			result[Sand] = sand * keep;
			result[Grass] = grass * keep;
			result[Rock] = rock;
			if (height >= -0.5f) return;

			// Under water, as on Raft's islands (CIMeasureUnderwater): sand on the shelf, rock taking over with depth
			// (a tenth on the shallow shelf, a third at 10-20 m, over half below 20 m) and on the steepest slopes, in patches; a little dirt
			float depth = -height;
			float wx = terrain.transform.position.x + nx * data.size.x, wz = terrain.transform.position.z + nz * data.size.z;
			float patches = Mathf.PerlinNoise(wx / 23f + 311.7f, wz / 23f + 97.3f) - 0.5f;
			float under = Mathf.Clamp01(Mathf.Max(Mathf.InverseLerp(46f, 64f, slope) * 0.7f, Mathf.Clamp01((depth - 2f) / 60f) * 0.8f) + 0.06f + patches * 0.5f * Mathf.InverseLerp(2f, 12f, depth));
			float dirt = 0.07f * Mathf.InverseLerp(3f, 10f, depth) * (1f - under);
			float t = Mathf.InverseLerp(0.5f, 2.5f, depth); // (blends in below the waterline)
			result[Seabed] = dirt * t;
			result[Sand] = Mathf.Lerp(result[Sand] + result[Grass], 1f - under - dirt, t);
			result[Grass] = result[Grass] * (1f - t);
			result[Rock] = Mathf.Lerp(rock, under, t);
		}

		static TerrainLayer MakeLayer(string name, Color light, Color dark, float tileSize)
		{
			const int size = 128;
			var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name, wrapMode = TextureWrapMode.Repeat };
			var pixels = new Color[size * size];
			float seed = name.GetHashCode() % 1000;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					// Two octaves of tileable noise (sampled on a torus so the texture repeats seamlessly)
					float n = 0.65f * TileableNoise(x, y, size, 4f, seed) + 0.35f * TileableNoise(x, y, size, 16f, seed + 37f);
					pixels[y * size + x] = Color.Lerp(dark, light, n);
				}
			tex.SetPixels(pixels);
			tex.Apply(true);

			return new TerrainLayer
			{
				name = name,
				diffuseTexture = tex,
				tileSize = new Vector2(tileSize, tileSize),
			};
		}

		static float TileableNoise(int x, int y, int size, float frequency, float seed)
		{
			float ax = x / (float)size * Mathf.PI * 2f, ay = y / (float)size * Mathf.PI * 2f;
			float r = frequency / (Mathf.PI * 2f);
			// Map the 2D torus to 2D noise space via its angles; Perlin is smooth enough for ground textures
			float nx = Mathf.Cos(ax) * r + Mathf.Sin(ay) * r * 0.37f + seed;
			float ny = Mathf.Sin(ax) * r + Mathf.Cos(ay) * r + seed * 0.5f;
			return Mathf.PerlinNoise(nx, ny);
		}
	}
}
