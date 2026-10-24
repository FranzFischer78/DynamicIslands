using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>Turns an IslandFile into GameObjects, in the editor or in a Raft world.</summary>
	public static class IslandSpawner
	{
		/// <summary>Raft's layer for walkable static geometry; the original landmark code used it for island terrain.</summary>
		public const int TerrainLayer = 16;

		/// <summary>Every island spawned in the current world (host and clients), so world shifts can move them.</summary>
		public static readonly List<GameObject> SpawnedRoots = new List<GameObject>();

		/// <summary>
		/// Raft finds a tree or pickup that a remote player harvests or picks up by its ObjectIndex in NetworkIDManager
		/// (Message_AxeHit, Message_PickupObjectManager_RemoveItem). Every machine spawns the same island objects in
		/// the same order, so indexes built from the island's id (shared by host and clients) plus the object's place
		/// in the hierarchy match everywhere. The high base keeps them clear of Raft's own counter-based indexes.
		/// The top bit stays clear: Message_AxeHit carries the tree's index as an int, and Raft ignores a negative one
		/// (seen in a two-player test with 0xC0000000: a client's axe did nothing on custom islands' trees, and the
		/// host's chops never reached clients).
		/// </summary>
		public static void RegisterNetworkIds(GameObject root, int islandId)
		{
			uint baseIndex = 0x40000000u | ((uint)(islandId & 0x3FFF) << 16);
			uint n = 0;
			// (an object this Raft no longer has keeps the numbers of its pickups - PickupGap, R2b)
			Component[] found = root.GetComponentsInChildren<PickupGap>(true).Length == 0
				? (Component[])root.GetComponentsInChildren<PickupItem_Networked>(true) : root.GetComponentsInChildren<MonoBehaviour>(true);
			foreach (Component c in found)
			{
				var gap = c as PickupGap;
				if (gap != null) { n += (uint)gap.Count; continue; }
				var pn = c as PickupItem_Networked;
				if (pn == null) continue;
				pn.ObjectIndex = baseIndex + (++n);
				// Picked up = disabled rather than destroyed, so the island's objects keep their order and state can be recorded
				pn.spawnType = ObjectSpawnType.GameObject;
				NetworkIDManager.AddNetworkID(pn, typeof(PickupItem_Networked));
			}
		}

		/// <summary>Removes a spawned island from the world (and its objects from Raft's network registry).</summary>
		public static void Despawn(GameObject root)
		{
			if (root == null) return;
			BuriedTreasure.OnDespawn(root);
			// (a claim the host granted for this island's chest or zone and not used yet is forgotten: kept, it let the
			// thing through without asking when the island came back, long after the host stopped holding it - AU41)
			IslandWorldState.Entry entry = IslandWorldState.Islands.FirstOrDefault(x => x.Root == root);
			if (entry != null) Claims.ForgetIsland(entry.Id);
			try { CreatureSpawner.OnIslandDespawned(root); }
			catch (System.Exception e) { Debug.LogError("[CUSTOM ISLANDS] Removing the island's creatures: " + e); }
			foreach (PickupItem_Networked pn in root.GetComponentsInChildren<PickupItem_Networked>(true))
			{
				try { NetworkIDManager.RemoveNetworkID(pn, typeof(PickupItem_Networked)); }
				catch (System.Exception) { } // never registered
			}
			SpawnedRoots.Remove(root);
			Object.Destroy(root);
		}

		#region Elevation (flying and underwater islands)

		public const float MinElevation = -100f, MaxElevation = 250f;
		/// <summary>Islands raised more than this above the sea are built as flying islands (holes + rocky underside).</summary>
		public const float FlyingThreshold = 3f;
		/// <summary>Clearance kept between a flying island's underside and the sea, so a raft can pass beneath.</summary>
		const float UndersideClearance = 8f;

		public static string DescribeElevation(float e)
		{
			if (e > FlyingThreshold) return "flying " + e.ToString("F0") + " m above the sea";
			if (e < -0.5f) return "under water, top of the land " + (-e).ToString("F0") + " m deeper than normal";
			return "normal (at sea level)";
		}

		/// <summary>The elevation saved in an island file (0 if it can't be read).</summary>
		public static float ElevationOf(string islandName)
		{
			try { return IslandFile.Load(PathFor(islandName)).Elevation; }
			catch { return 0f; }
		}

		/// <summary>
		/// Flying island: cuts holes in the terrain wherever it lies below the editor's sea level (so no flat square of
		/// seabed floats in the sky) and hangs a rocky underside below the remaining land, from the coastline down to a
		/// point, with a collider so a low island still stops a raft.
		/// </summary>
		static void MakeFlying(TerrainData data, Transform terrainTransform, float waterLevel, float elevation)
		{
			int res = data.heightmapResolution, cells = res - 1;
			float[,] h = data.GetHeights(0, 0, res, res);
			float spacing = data.size.x / cells, height = data.size.y;
			float water = waterLevel / height;

			// Land samples
			var land = new bool[res, res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) land[z, x] = h[z, x] > water;

			// Holes: keep only cells whose four corners are land
			var solid = new bool[cells, cells];
			int kept = 0;
			for (int z = 0; z < cells; z++)
				for (int x = 0; x < cells; x++)
				{
					solid[z, x] = land[z, x] && land[z, x + 1] && land[z + 1, x] && land[z + 1, x + 1];
					if (solid[z, x]) kept++;
				}
			if (kept == 0) return;
			data.SetHoles(0, 0, solid);

			// The rim: every sample on the edge of the kept cells (a corner of a cut-away cell, or on the terrain's border).
			// It stays on the ground so the underside meets it all round; each other sample's distance (in samples) to the
			// rim sets how far it hangs. (Measured from the land's edge instead, a sample only diagonally next to the sea
			// counted as inside and hung down while still on a cut edge: zigzag gaps to the sky along a diagonal coast,
			// seen from below - Lantern Spire 10-16.)
			var inside = new bool[res, res];
			for (int z = 1; z < cells; z++)
				for (int x = 1; x < cells; x++)
					inside[z, x] = solid[z - 1, x - 1] && solid[z - 1, x] && solid[z, x - 1] && solid[z, x];
			int[,] dist = DistanceToEdge(inside, res);

			// Underside: same grid as the kept cells; the rim follows the surface, the inside hangs down like a cone
			// that is deepest under the middle of the land (never reaching down to the sea)
			float maxDepth = Mathf.Max(2f, elevation - UndersideClearance);
			int dMax = 1;
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) if (land[z, x] && dist[z, x] > dMax) dMax = dist[z, x];
			float coneDepth = Mathf.Min(maxDepth, dMax * spacing * 0.9f);
			var index = new int[res, res];
			var verts = new List<Vector3>();
			var uvs = new List<Vector2>();
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					index[z, x] = -1;
					if (!land[z, x]) continue;
					float surface = h[z, x] * height;
					int d = dist[z, x]; // 0 on the rim
					float noise = Mathf.PerlinNoise(x * 0.07f, z * 0.07f);
					// Relative to sea level (terrain y = waterLevel); rounded near the rim, ragged with noise
					float depth = Mathf.Min(coneDepth * Mathf.Pow(d / (float)dMax, 0.75f) * (0.8f + 0.4f * noise), maxDepth);
					float y = d == 0 ? surface : Mathf.Min(surface, waterLevel) - depth;
					index[z, x] = verts.Count;
					verts.Add(new Vector3(x * spacing, y, z * spacing));
					uvs.Add(new Vector2((x * spacing + y) / 12f, (z * spacing + y) / 12f));
				}
			var tris = new List<int>();
			for (int z = 0; z < cells; z++)
				for (int x = 0; x < cells; x++)
				{
					if (!solid[z, x]) continue;
					int a = index[z, x], b = index[z, x + 1], c = index[z + 1, x], e = index[z + 1, x + 1];
					// Wound to face downwards
					tris.Add(a); tris.Add(b); tris.Add(c);
					tris.Add(b); tris.Add(e); tris.Add(c);
				}

			var mesh = new Mesh { name = "CI_FlyingUnderside" };
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(verts);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(tris, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			var go = new GameObject("Underside");
			go.layer = TerrainLayer;
			go.transform.SetParent(terrainTransform, false);
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			go.AddComponent<MeshRenderer>().sharedMaterial = UndersideMaterial(TerrainPainter.StyleOf(terrainTransform.GetComponent<Terrain>()));
			go.AddComponent<MeshCollider>().sharedMesh = mesh;
		}

		/// <summary>Chamfer distance (in samples) from each true cell to the nearest false cell.</summary>
		static int[,] DistanceToEdge(bool[,] inside, int res)
		{
			const int Far = 1 << 20;
			var d = new int[res, res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) d[z, x] = inside[z, x] ? Far : 0;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					if (d[z, x] == 0) continue;
					int v = d[z, x];
					if (x > 0) v = Mathf.Min(v, d[z, x - 1] + 1); else v = 1;
					if (z > 0) v = Mathf.Min(v, d[z - 1, x] + 1); else v = 1;
					d[z, x] = v;
				}
			for (int z = res - 1; z >= 0; z--)
				for (int x = res - 1; x >= 0; x--)
				{
					if (d[z, x] == 0) continue;
					int v = d[z, x];
					if (x < res - 1) v = Mathf.Min(v, d[z, x + 1] + 1); else v = 1;
					if (z < res - 1) v = Mathf.Min(v, d[z + 1, x] + 1); else v = 1;
					d[z, x] = v;
				}
			return d;
		}

		static readonly Dictionary<int, Material> undersideMaterials = new Dictionary<int, Material>();

		/// <summary>Rock-textured material for the underside (the style's steep-ground layer when Raft's textures are in use).</summary>
		static Material UndersideMaterial(int style)
		{
			Material undersideMaterial;
			if (undersideMaterials.TryGetValue(style, out undersideMaterial) && undersideMaterial != null) return undersideMaterial;
			Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
			undersideMaterial = new Material(shader) { name = "CI_FlyingUnderside_" + TerrainPainter.StyleName(style) };
			undersideMaterials[style] = undersideMaterial;
			TerrainLayer rock = TerrainPainter.LayersFor(style)[TerrainPainter.Rock];
			if (rock != null && rock.diffuseTexture != null)
			{
				undersideMaterial.mainTexture = rock.diffuseTexture;
				if (rock.normalMapTexture != null && undersideMaterial.HasProperty("_BumpMap"))
				{
					undersideMaterial.SetTexture("_BumpMap", rock.normalMapTexture);
					undersideMaterial.EnableKeyword("_NORMALMAP");
				}
			}
			else undersideMaterial.color = new Color(0.45f, 0.42f, 0.38f);
			if (undersideMaterial.HasProperty("_Glossiness")) undersideMaterial.SetFloat("_Glossiness", 0.05f);
			return undersideMaterial;
		}

		#endregion

		public static string PathFor(string islandName)
		{
			return Path.Combine(DynamicIslands.assetpath, islandName + IslandFile.Extension);
		}

		public static IEnumerable<string> ListSavedIslands()
		{
			if (!Directory.Exists(DynamicIslands.assetpath)) return Enumerable.Empty<string>();
			// (kept until the folder changes, and 30 s at most: a scan with 1,600 islands took 60 ms - each random island's
			// pick did several - US1)
			lock (savedLock)
			{
				System.DateTime changed = Directory.GetLastWriteTimeUtc(DynamicIslands.assetpath);
				if (savedNames == null || changed != savedChanged || System.Environment.TickCount - savedAt > 30000)
				{
					savedNames = Directory.GetFiles(DynamicIslands.assetpath, "*" + IslandFile.Extension)
						.Select(Path.GetFileNameWithoutExtension)
						.OrderBy(n => n).ToArray();
					savedChanged = changed;
					savedAt = System.Environment.TickCount;
				}
				return savedNames;
			}
		}

		static readonly object savedLock = new object();
		static string[] savedNames;
		static System.DateTime savedChanged;
		static int savedAt;

		public static TerrainData CreateTerrainData(Vector3 size, int heightmapResolution)
		{
			var data = new TerrainData();
			// Resolution must be set before size (setting resolution rescales size)
			data.heightmapResolution = heightmapResolution;
			data.size = size;
			return data;
		}

		/// <summary>
		/// Instantiates the island's objects under parent. Positions in the file are relative to the terrain origin,
		/// so parent should sit at the terrain origin. Returns the number of objects that could not be found.
		/// </summary>
		/// <summary>
		/// A foundation of Raft's in a world: the deck a player walks on. On the player's raft Raft adds one for each foundation
		/// (its raft collider, 0.22 m above the block); a copy on a custom island has only the block's own collider, which
		/// players fall through - so a ghost raft or a wreck had no floor. This is the same deck, solid like the island's land.
		/// </summary>
		static void AddDeck(GameObject foundation)
		{
			var deck = new GameObject("CustomIslands_Deck");
			deck.layer = TerrainLayer;
			deck.transform.SetParent(foundation.transform, false);
			BoxCollider box = deck.AddComponent<BoxCollider>();
			Vector3 s = foundation.transform.lossyScale;
			box.size = new Vector3(PlacementOptions.GridSize / Mathf.Max(0.01f, Mathf.Abs(s.x)), 0.3f / Mathf.Max(0.01f, Mathf.Abs(s.y)), PlacementOptions.GridSize / Mathf.Max(0.01f, Mathf.Abs(s.z)));
			box.center = new Vector3(0f, (PlacementOptions.FoundationTop - 0.15f) / Mathf.Max(0.01f, Mathf.Abs(s.y)), 0f);
		}

		static readonly HashSet<string> toldMissing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

		/// <summary>Running numbers while an island's objects are made (creatures, chests, zones and treasure are numbered in
		/// file order, the same on every machine).</summary>
		class Counts { public int missing, creature, loot, zone, treasure; }

		public static int SpawnObjects(IslandFile island, Transform parent, bool editable, bool skipUnderwater = false)
		{
			var c = new Counts();
			for (int index = 0; index < island.Objects.Count; index++) SpawnOne(island, index, parent, editable, skipUnderwater, c);
			return c.missing;
		}

		/// <summary>
		/// SpawnObjects spread over frames (ROADMAP P1: up to 12 000 objects were made in one frame, a long stutter when a big
		/// island streamed in): as many as fit in budgetMs a frame. The caller keeps the parent inactive meanwhile.
		/// </summary>
		public static System.Collections.IEnumerator SpawnObjectsSliced(IslandFile island, Transform parent, bool skipUnderwater, float budgetMs, System.Action<int> done)
		{
			var c = new Counts();
			var clock = System.Diagnostics.Stopwatch.StartNew();
			for (int index = 0; index < island.Objects.Count; index++)
			{
				if (parent == null) yield break; // (removed meanwhile)
				try { SpawnOne(island, index, parent, false, skipUnderwater, c); }
				catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Object " + index + " of '" + island.Name + "': " + e.Message); }
				if (clock.Elapsed.TotalMilliseconds > budgetMs) { yield return null; clock.Reset(); clock.Start(); }
			}
			done(c.missing);
		}

		static void SpawnOne(IslandFile island, int index, Transform parent, bool editable, bool skipUnderwater, Counts c)
		{
				int missing = 0, creature = c.creature, loot = c.loot, zone = c.zone, treasure = c.treasure;
				try { SpawnBody(island, index, parent, editable, skipUnderwater, ref missing, ref creature, ref loot, ref zone, ref treasure); }
				catch
				{
					// (an object that failed still takes its number: the later chests, animals, zones and treasure keep theirs,
					// the same as on the other machines and in the saved state - review 2026-10-06)
					if (!editable)
					{
						IslandObject o = island.Objects[index];
						if (ContentCatalog.IsCreature(o.Name)) { if (creature == c.creature) creature++; }
						else if (ContentCatalog.IsZone(o.Name)) { if (o.Name == ContentCatalog.TriggerZone && zone == c.zone) zone++; }
						else if (ContentCatalog.IsTreasure(o.Name)) { if (treasure == c.treasure) treasure++; }
						else if (ObjectProps.IsLoot(o.Name, o.Props) && loot == c.loot) loot++;
					}
					throw;
				}
				finally { c.missing += missing; c.creature = creature; c.loot = loot; c.zone = zone; c.treasure = treasure; }
		}

		/// <summary>
		/// Left out of a flying island, which has no sea round it: what lies under its own sea level, and the shore ice laid at
		/// the waterline (its mesh hangs metres down into the sea: with the land under the waterline cut away its outer edge
		/// stood 1-5.5 m in the air, CIGroundIslands gen-snowy-701141).
		/// </summary>
		static bool UnderFlyingSea(IslandObject o, float waterLevel)
		{
			return o.Position.y < waterLevel - 0.5f || (o.Position.y < waterLevel + 2.5f && o.Name.StartsWith("TP_IceShore", System.StringComparison.Ordinal));
		}

		static void SpawnBody(IslandFile island, int index, Transform parent, bool editable, bool skipUnderwater, ref int missing, ref int creature, ref int loot, ref int zone, ref int treasure)
		{
			{
				IslandObject o = island.Objects[index];
				// A flying island has no sea around it: what lies under its own sea level (a lagoon's turtles, an underwater
				// zone) would hang in the air - left out, but still counted, so the numbers of the others stay the same
				bool underSea = skipUnderwater && UnderFlyingSea(o, island.WaterLevel);
				// Creatures: in a world only their spawn point exists (the host brings the live animals, CreatureSpawner).
				// They are numbered in file order, which is the same on every machine.
				if (!editable && ContentCatalog.IsCreature(o.Name))
				{
					if (underSea) { creature++; return; }
					CreatureSpawnPoint point = CreatureSpawnPoint.Create(parent, o, creature++);
					if (point != null) Behaviours.Attach(point.gameObject, o.Name, o.Props, index);
					return;
				}
				// Zones are invisible in a world
				if (!editable && ContentCatalog.IsZone(o.Name))
				{
					if (underSea) { if (o.Name == ContentCatalog.TriggerZone) zone++; return; }
					GameObject zgo;
					if (o.Name == ContentCatalog.TriggerZone) zgo = TriggerZone.Create(parent, o, zone++).gameObject;
					else
					{
						zgo = new GameObject(o.Name);
						zgo.transform.SetParent(parent, false);
						zgo.transform.position = parent.position + o.Position;
						if (o.Name == ContentCatalog.AtmosphereZoneName) zgo.AddComponent<AtmosphereZone>().Configure(o.Props ?? new Dictionary<string, string>());
						else if (o.Name == ContentCatalog.SoundZoneName) zgo.AddComponent<SoundZone>().Configure(o.Props ?? new Dictionary<string, string>());
					}
					Behaviours.Attach(zgo, o.Name, o.Props, index);
					return;
				}
				// Buried treasure: Raft's own treasure point, made once the island's state is known (BuriedTreasure.OnIslandReady)
				if (!editable && ContentCatalog.IsTreasure(o.Name))
				{
					if (underSea) { treasure++; return; }
					var tgo = new GameObject(o.Name);
					tgo.transform.SetParent(parent, false);
					tgo.transform.position = parent.position + o.Position;
					BuriedTreasure bt = tgo.AddComponent<BuriedTreasure>();
					bt.Number = treasure++;
					int kind;
					bt.Kind = int.TryParse(ObjectProps.Get(o.Props, ObjectProps.TreasureKind), out kind) ? kind : 0;
					bt.Loot = ObjectProps.Loot(o.Props, ObjectProps.TreasureLoot);
					return;
				}
				// Invisible walls and ramps: only their collision in a world
				if (!editable && ContentCatalog.IsHelper(o.Name))
				{
					if (underSea) return;
					GameObject hgo = ContentCatalog.SpawnHelperSolid(o.Name, parent);
					if (hgo == null) return;
					hgo.transform.position = parent.position + o.Position;
					hgo.transform.rotation = Quaternion.Euler(o.EulerRotation);
					hgo.transform.localScale = o.Scale;
					Behaviours.Attach(hgo, o.Name, o.Props, index);
					return;
				}
				// A flying island has no sea around it: corals and the like would hang in the air
				if (skipUnderwater && UnderFlyingSea(o, island.WaterLevel)) return;
				GameObject go = PlaceableCatalog.Spawn(o.Name, parent, !editable); // gameplay scripts only in a world
				if (go == null)
				{
					missing++;
					Debug.LogWarning("[CUSTOM ISLANDS] Object '" + o.Name + "' is not in the object catalog - " + (editable ? "a placeholder keeps it" : "left out"));
					// (in the editor a placeholder keeps it, so saving doesn't drop it from the island for good: after a Raft update
					// renamed or removed an object, a later version may have it again)
					if (editable) MissingPlaceholder(o, parent);
					else
					{
						// (in a world a chest still takes its number: the chests after it keep their saved state)
						if (ObjectProps.IsLoot(o.Name, o.Props)) loot++;
						// (and so do its pickups, when Raft had them: the trees and items after it keep theirs - R2b)
						int had = StableIds.ShippedPickups(o.Name);
						if (had > 0)
						{
							var gap = new GameObject(o.Name + " (" + MissingTag + ")");
							gap.transform.SetParent(parent, false);
							gap.AddComponent<PickupGap>().Count = had;
						}
					}
					return;
				}
				go.transform.position = parent.position + o.Position;
				go.transform.rotation = Quaternion.Euler(o.EulerRotation);
				// (the randomizer's set pieces stand at the size of Raft's own - some of Raft's are scaled in their scene, like
				// Balboa's caves: one made before its piece was loaded was saved at scale 1; the copy has Raft's scale)
				if (o.Scale != Vector3.one || o.Props == null || !o.Props.ContainsKey("set.piece")) go.transform.localScale = o.Scale;
				EnableColliders(go);
				if (!editable && o.Name.StartsWith("Block_Foundation", System.StringComparison.Ordinal)) AddDeck(go);
				CaveBacks.Add(go, o.Name); // (a cave piece's rock, or a Selene room's wall, seen from outside too - X7)
				if (!editable) CaveMountain.Add(parent, go, o.Name); // (a rock mountain over the island's caves - AU83)
				if (!editable) CaveGround.Add(parent, go, o.Name); // (and the ground cut out of their rooms - X7)

				if (editable)
					EditorGameObject.Attach(go, o.Name, o.Props ?? new Dictionary<string, string>()).Uid = o.Uid;
				else
				{
					ObjectProps.ApplyTint(go, o.Props);
					ZiplineEnds.Apply(go, o.Props, parent.position);
					if (ObjectProps.IsNote(o.Name, o.Props))
					{
						CustomNote.Attach(go, o.Props);
						ContentCatalog.ShowSignText(go, ObjectProps.Get(o.Props, ObjectProps.NoteTitle)); // a sign shows its title
							ContentCatalog.ShowScribble(go, o.Name, true, ObjectProps.Get(o.Props, ObjectProps.NoteTitle)); // a board shows writing
					}
					// Numbered in file order like the creatures (the same on every machine)
					if (ObjectProps.IsLoot(o.Name, o.Props)) LootCrate.Attach(go, o.Name, o.Props, loot++);
					// A trader's stall (its stock under the object's index)
					else if (TradeStand.IsTrade(o.Props)) TradeStand.Attach(go, o.Props, index);
					// A survivor's cage (RF3: its state under the object's index)
					else if (Survivors.IsCage(o.Name)) SurvivorCage.Attach(go, o.Props ?? new Dictionary<string, string>(), index);
					// Raft's trading post (SL7: its store works; numbered and stocked in RaftStore.OnIslandReady)
					else if (RaftStore.Is(o.Name)) RaftStore.Attach(go, index);
					// Raft's vending machine (SL7b: it works; numbered in RaftVending.OnIslandReady)
					else if (RaftVending.Is(o.Name)) RaftVending.Attach(go, index);
					// Names, movement, "players can use it", collision, events (Behaviours)
					Behaviours.Attach(go, o.Name, o.Props, index);
				}
			}
		}


		/// <summary>Name of the placeholders for objects this Raft doesn't have (tests).</summary>
		public const string MissingTag = "MissingObject";

		/// <summary>
		/// An object of the island that this Raft version doesn't have: a red box in its place with its name, position, turn,
		/// size and settings - saved back into the island exactly as it was. It can be moved or deleted like any object.
		/// </summary>
		static void MissingPlaceholder(IslandObject o, Transform parent)
		{
			GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = o.Name + " (" + MissingTag + ")";
			go.transform.SetParent(parent, false);
			go.transform.position = parent.position + o.Position;
			go.transform.rotation = Quaternion.Euler(o.EulerRotation);
			go.transform.localScale = o.Scale;
			Renderer r = go.GetComponent<Renderer>();
			Shader shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
			if (r != null && shader != null) r.material = new Material(shader) { color = new Color(0.9f, 0.15f, 0.1f, 1f) };
			EditorGameObject.Attach(go, o.Name, o.Props != null ? new Dictionary<string, string>(o.Props) : new Dictionary<string, string>()).Uid = o.Uid;
		}

		/// <summary>
		/// Centre (terrain-local x/z) of the parts of the island above the water level, so the land
		/// rather than the middle of the whole terrain square is what ends up where we spawn.
		/// Falls back to the terrain centre when nothing is above water.
		/// </summary>
		public static Vector2 LandCentre(IslandFile island)
		{
			int res = island.HeightmapResolution;
			float water = island.WaterLevel / island.TerrainSize.y;
			double sx = 0, sz = 0; long n = 0;
			for (int y = 0; y < res; y++)
				for (int x = 0; x < res; x++)
					if (island.Heights[y, x] > water) { sx += x; sz += y; n++; }
			if (n == 0)
			{
				// The randomizer's extras on one of Raft's islands: centred on Raft's island, wherever the objects are
				string[] c = ObjectProps.Get(island.Props, IslandProps.Centre).Split(',');
				float cx, cz;
				if (c.Length == 2 && float.TryParse(c[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cx) &&
					float.TryParse(c[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cz)) return new Vector2(cx, cz);
				// No land (e.g. an abandoned raft built from Raft blocks on the water): centre on the objects
				if (island.Objects.Count > 0)
					return new Vector2(island.Objects.Average(o => o.Position.x), island.Objects.Average(o => o.Position.z));
				return new Vector2(island.TerrainSize.x / 2f, island.TerrainSize.z / 2f);
			}
			float step = 1f / (res - 1);
			return new Vector2((float)(sx / n) * step * island.TerrainSize.x, (float)(sz / n) * step * island.TerrainSize.z);
		}

		/// <summary>
		/// How far (m) the shaped part of the island reaches from its land centre (see LandCentre), i.e. the radius
		/// of the circle around the spawn position that the island covers. 0 if nothing was shaped.
		/// </summary>
		public static float LandRadius(IslandFile island)
		{
			int res = island.HeightmapResolution;
			// (ground less than 20 m under the sea: on a deep sea floor the island's foot reaches much further, down in the dark)
			float threshold = Mathf.Max(ShapedThresholdMetres, island.WaterLevel - IslandFile.DefaultWaterLevel) / island.TerrainSize.y;
			float step = 1f / (res - 1);
			Vector2 centre = LandCentre(island);
			float maxSq = 0f;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					if (island.Heights[z, x] > threshold)
					{
						float dx = x * step * island.TerrainSize.x - centre.x, dz = z * step * island.TerrainSize.z - centre.y;
						float sq = dx * dx + dz * dz;
						if (sq > maxSq) maxSq = sq;
					}
			if (maxSq == 0f && island.Objects.Count > 0) // no land: how far the objects reach
				foreach (IslandObject o in island.Objects)
					maxSq = Mathf.Max(maxSq, (o.Position.x - centre.x) * (o.Position.x - centre.x) + (o.Position.z - centre.y) * (o.Position.z - centre.y));
			return Mathf.Sqrt(maxSq);
		}

		/// <summary>True if anything of the terrain was raised above the flat seabed (islands of only objects have no land).</summary>
		public static bool HasLand(IslandFile island)
		{
			float threshold = ShapedMetres(island) / island.TerrainSize.y;
			foreach (float h in island.Heights) if (h > threshold) return true;
			return false;
		}

		/// <summary>Anything raised more than this above the flat seabed (height 0) counts as part of the island.</summary>
		const float ShapedThresholdMetres = 1f;
		/// <summary>How deep the sea floor of an island on a deep sea floor is kept in a world (m): below it, in the dark, it ends.</summary>
		const float DeepestKept = 110f;

		/// <summary>Ground higher than this (m above the terrain's base) is part of the island: anything raised off a shallow seabed; on a deep sea floor, ground less than DeepestKept under the sea.</summary>
		static float ShapedMetres(IslandFile island) { return Mathf.Max(ShapedThresholdMetres, island.WaterLevel - DeepestKept); }
		/// <summary>Extra heightmap samples kept around the shaped area so slopes don't end in a cliff.</summary>
		const int CropMarginSamples = 16;

		/// <summary>
		/// Square block of the heightmap (size 2^n + 1, as Unity terrains require) covering everything that was
		/// raised above the seabed plus a margin. Falls back to the whole heightmap if nothing was shaped.
		/// </summary>
		public static void GetCropArea(IslandFile island, out int x0, out int z0, out int size)
		{
			int res = island.HeightmapResolution;
			float threshold = ShapedMetres(island) / island.TerrainSize.y;
			int minX = res, minZ = res, maxX = -1, maxZ = -1;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					if (island.Heights[z, x] > threshold)
					{
						if (x < minX) minX = x; if (x > maxX) maxX = x;
						if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
					}
			if (maxX < 0) { x0 = 0; z0 = 0; size = res; return; }

			int needed = Mathf.Max(maxX - minX, maxZ - minZ) + 1 + 2 * CropMarginSamples;
			size = 33;
			while (size < needed && size < res) size = (size - 1) * 2 + 1;
			size = Mathf.Min(size, res);

			// Centre the block on the shaped area, then keep it inside the heightmap
			x0 = Mathf.Clamp((minX + maxX) / 2 - size / 2, 0, res - size);
			z0 = Mathf.Clamp((minZ + maxZ) / 2 - size / 2, 0, res - size);
		}

		/// <summary>
		/// Builds the island in the current (game) scene with its sea level at worldPosition.y and the
		/// centre of its land at worldPosition horizontally. The catalog must already be built.
		/// </summary>
		public static GameObject SpawnInWorld(IslandFile island, Vector3 worldPosition)
		{
			bool flying;
			GameObject root = MakeRoot(island, worldPosition, out flying);
			var objects = new GameObject("Objects");
			objects.transform.SetParent(root.transform, false);
			int missing = SpawnObjects(island, objects.transform, false, flying);
			CaveGround.Cut(objects.transform); // (X7)
			Spawned(island, worldPosition, flying, missing);
			return root;
		}

		/// <summary>
		/// The colliders of an object's active parts switched on. Its parent may be inactive (the sliced spawn): the parts are
		/// looked at by their own switch, as GetComponentsInChildren without inactive ones found none there (review 2026-10-06).
		/// </summary>
		static void EnableColliders(GameObject go)
		{
			foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
			{
				Transform t = c.transform;
				bool on = true;
				for (; t != null && on; t = t == go.transform ? null : t.parent) on = t.gameObject.activeSelf;
				if (on) c.enabled = true;
			}
		}

		/// <summary>Tests (CIPerfChecks): how many frames the last sliced spawn took.</summary>
		internal static int LastSpawnFrames;
		internal static string LastSpawnTiming = "";

		/// <summary>
		/// SpawnInWorld over several frames (ROADMAP P1): the land at once, then the objects a few milliseconds a frame under
		/// a parent kept inactive until all are there (nothing of the island runs half made). result gets the root, or null
		/// when it was removed meanwhile.
		/// </summary>
		public static System.Collections.IEnumerator SpawnInWorldSliced(IslandFile island, Vector3 worldPosition, System.Action<GameObject> result, float budgetMs = 6f)
		{
			bool flying;
			var clock = System.Diagnostics.Stopwatch.StartNew();
			GameObject root = null;
			int rootsBefore = SpawnedRoots.Count;
			// (an error making the land - a broken heightmap - ends here with null: the caller marks the island failed instead of
			// waiting for it for good - review 2026-10-06)
			try { root = MakeRoot(island, worldPosition, out flying); }
			catch (System.Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Making the land of '" + island.Name + "' failed: " + e);
				while (SpawnedRoots.Count > rootsBefore) { GameObject half = SpawnedRoots[SpawnedRoots.Count - 1]; SpawnedRoots.RemoveAt(SpawnedRoots.Count - 1); if (half != null) UnityEngine.Object.Destroy(half); }
				result(null);
				yield break;
			}
			long land = clock.ElapsedMilliseconds;
			var objects = new GameObject("Objects");
			objects.SetActive(false);
			objects.transform.SetParent(root.transform, false);
			int missing = 0, startFrame = Time.frameCount;
			yield return SpawnObjectsSliced(island, objects.transform, flying, budgetMs, m => missing = m);
			LastSpawnFrames = Time.frameCount - startFrame + 1;
			if (root == null || objects == null) { result(null); yield break; }
			clock.Reset(); clock.Start();
			objects.SetActive(true);
			LastSpawnTiming = "land " + land + " ms, objects over " + LastSpawnFrames + " frames, switching them on " + clock.ElapsedMilliseconds + " ms";
			CaveGround.Cut(objects.transform); // (the ground out of the caves' rooms: their rock can be hit now - X7)
			Spawned(island, worldPosition, flying, missing);
			result(root);
		}

		static void Spawned(IslandFile island, Vector3 worldPosition, bool flying, int missing)
		{
			int wanted = flying ? island.Objects.Count(o => o.Position.y >= island.WaterLevel - 0.5f) : island.Objects.Count;
			// (the player is told once per island and Raft start: an object Raft doesn't have any more is left out)
			if (missing > 0 && toldMissing.Add(island.Name ?? ""))
				DynamicIslands.Notify("The island '" + island.Name + "' has " + missing + " object(s) this Raft version doesn't have: they are left out", true);
			Debug.Log("[CUSTOM ISLANDS] Spawned island '" + island.Name + "' at " + worldPosition + " with " +
				(wanted - missing) + "/" + wanted + " objects" + (worldPosition.y != 0f ? ", " + DescribeElevation(worldPosition.y) : "") +
				(wanted < island.Objects.Count ? " (" + (island.Objects.Count - wanted) + " under-water objects left out)" : ""));
		}

		/// <summary>The island's root in the world and its land (the objects come after).</summary>
		static GameObject MakeRoot(IslandFile island, Vector3 worldPosition, out bool flying)
		{
			var root = new GameObject("CustomIsland_" + island.Name);
			SpawnedRoots.Add(root);
			IslandInfo.Tag(root, island);
			// Terrain origin is its corner; shift so the land centre lands on worldPosition,
			// and down so the editor's water level lines up with the sea
			Vector2 land = LandCentre(island);
			root.transform.position = worldPosition - new Vector3(land.x, island.WaterLevel, land.y);

			flying = worldPosition.y > FlyingThreshold;
			if (HasLand(island)) // an island of only objects (e.g. an abandoned raft of Raft blocks) gets no terrain
			{
				// Only bring the part of the heightmap that has been shaped, not the whole flat 1000 x 1000 m seabed
				int cropX, cropZ, cropSize;
				GetCropArea(island, out cropX, out cropZ, out cropSize);
				float spacing = island.TerrainSize.x / (island.HeightmapResolution - 1);
				var cropped = new float[cropSize, cropSize];
				for (int z = 0; z < cropSize; z++)
					for (int x = 0; x < cropSize; x++)
						cropped[z, x] = island.Heights[cropZ + z, cropX + x];

				TerrainData data = CreateTerrainData(new Vector3(spacing * (cropSize - 1), island.TerrainSize.y, spacing * (cropSize - 1)), cropSize);
				data.SetHeights(0, 0, cropped);
				GameObject terrainGO = Terrain.CreateTerrainGameObject(data);
				terrainGO.name = "Terrain";
				terrainGO.layer = TerrainLayer;
				terrainGO.transform.SetParent(root.transform, false);
				// Object positions in the file are relative to the full terrain's corner, which the root still represents
				terrainGO.transform.localPosition = new Vector3(cropX * spacing, 0, cropZ * spacing);
				Terrain spawnedTerrain = terrainGO.GetComponent<Terrain>();
				// (a second style mixed in: its textures are layers 5-8 of the saved paint)
				TerrainPainter.SetStyle(spawnedTerrain, TerrainPainter.StyleIndex(island.Style), island.HasMix ? TerrainPainter.StyleIndex(island.MixStyle) : -1);
				// Saved paint covers the full terrain; take the block matching the heightmap crop
				// (alphamap pixels line up with heightmap cells: resolution = heightmap resolution - 1)
				int cells = island.HeightmapResolution - 1;
				if (island.HasPaint && island.AlphamapResolution % cells == 0)
				{
					int scale = island.AlphamapResolution / cells;
					TerrainPainter.ApplySaved(spawnedTerrain, island.GetAlphamapBlock(cropX * scale, cropZ * scale, (cropSize - 1) * scale));
				}
				else
				{
					TerrainPainter.Setup(spawnedTerrain, worldPosition.y);
				}

				if (flying) MakeFlying(data, terrainGO.transform, island.WaterLevel, worldPosition.y);
			}

			return root;
		}
	}

	/// <summary>
	/// In a world, in the place of an object this Raft no longer has (a Raft update removed it): how many pickups it had
	/// (StableIds.ShippedPickups). RegisterNetworkIds skips that many numbers, so the pickups after it keep the numbers
	/// the world's saved state uses (ROADMAP R2b).
	/// </summary>
	public class PickupGap : MonoBehaviour
	{
		public int Count;
	}
}
