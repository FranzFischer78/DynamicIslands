using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Makes saved islands appear on their own while the raft sails (host only), and streams the world's custom
	/// islands in and out by distance so long worlds don't keep every island in memory (host and clients).
	/// Settings and the island pool live in Mods\DynamicIslands\spawnpool.txt; whether it is on is stored per world.
	/// </summary>
	public static class CustomIslandSpawner
	{
		public const string PoolFileName = "spawnpool.txt";
		static string PoolPath { get { return Path.Combine(DynamicIslands.assetpath, PoolFileName); } }

		/// <summary>How often the spawner and the streaming check run, in seconds.</summary>
		const float TickInterval = 2f;
		/// <summary>An island that was unloaded is loaded again this much closer than the unload distance, so it doesn't flicker.</summary>
		const float ReloadHysteresis = 200f;
		/// <summary>How far inside the unload distance the far end of where islands appear stays (an island loads again 100 m inside it).</summary>
		const float SpawnUnloadGap = 150f;
		/// <summary>Space kept between an island's land and the raft, or Raft's own islands, when placing it.</summary>
		public const float Clearance = 60f;

		// Settings (spawnpool.txt)
		public static float ChancePerKm = 0.25f;
		public static float MinSpacing = 800f;
		public static float SpawnDistanceMin = 250f;
		public static float SpawnDistanceMax = 350f;
		public static float UnloadDistance = 800f;
		/// <summary>Harvested trees and picked-up items on custom islands grow back after this many in-game days (0 = never).</summary>
		public static int RegrowDays = 3;
		/// <summary>Custom islands appear as green dots on Raft's Receiver (IslandRadar).</summary>
		public static bool ShowOnReceiver = true;
		/// <summary>Receiver dots only for islands this close (m; 0 = all) - and every island the players still need (ROADMAP LM2).</summary>
		public static float ReceiverDistance = 2000f;
		/// <summary>Weight of brand-new, randomly generated islands in the pool (0 = never).</summary>
		public static float GeneratedWeight = 1f;
		/// <summary>Styles generated islands can have.</summary>
		public static int[] GeneratedStyles = { TerrainPainter.Tropical, TerrainPainter.Snowy, TerrainPainter.Desert, TerrainPainter.Forest, TerrainPainter.Volcanic };
		/// <summary>Chance that a generated island is a flying one.</summary>
		public static float GeneratedFlyingChance = 0.1f;
		/// <summary>The generator's things to gather on generated islands (GenGather, ROADMAP LM9): how much on the land and in
		/// the shallows (0-1, 0 = none as before), how deep the shallows' finds go (m) and the kinds switched off ("" = all on).</summary>
		public static float GeneratedGather = 0f, GeneratedShallows = 0f, GeneratedShallowsDepth = GenGather.DefaultDepth;
		public static string GeneratedGatherOff = "";

		/// <summary>A generated island's things to gather as spawnpool.txt sets them.</summary>
		internal static void GatherFor(IslandGenSettings s)
		{
			s.Gather = GeneratedGather; s.Shallows = GeneratedShallows; s.ShallowsDepth = GeneratedShallowsDepth; s.GatherOff = GeneratedGatherOff;
		}

		/// <summary>Pool entry standing for "generate a new island".</summary>
		public const string GeneratedEntry = "<generated>";
		/// <summary>Files of islands generated while sailing are named gen-&lt;style&gt;-&lt;seed&gt;.</summary>
		public const string GeneratedPrefix = "gen-";

		/// <summary>This name, or with -2, -3... when an island file (or a world's island still being made) has it already.</summary>
		public static string FreeName(string name)
		{
			string n = name;
			for (int i = 2; File.Exists(IslandSpawner.PathFor(n)) || IslandWorldState.Islands.Any(e => e.Name.Equals(n, StringComparison.OrdinalIgnoreCase)); i++) n = name + "-" + i;
			return n;
		}
		/// <summary>Pool entries "type:&lt;map type&gt;" stand for a new island of that map type (MapTypes).</summary>
		public const string TypePrefix = "type:";
		static readonly List<KeyValuePair<string, float>> poolLines = new List<KeyValuePair<string, float>>();
		static DateTime poolFileTime;

		/// <summary>Automatic spawning in the current world (stored in the world's island list).</summary>
		public static bool Enabled = true;

		static float nextTick;
		static Vector3? lastRaftPosition;
		static float sailedSinceSpawn;
		static readonly Dictionary<string, float> radiusCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

		static Raft raft;
		static Raft CurrentRaft
		{
			get
			{
				if (raft == null) raft = UnityEngine.Object.FindObjectOfType<Raft>();
				return raft;
			}
		}

		public static Vector3? RaftPosition
		{
			get
			{
				Raft r = CurrentRaft;
				if (r == null) return null;
				return r.body != null ? r.body.position : r.transform.position;
			}
		}

		/// <summary>Called when a world finished loading.</summary>
		public static void OnWorldLoaded()
		{
			raft = null;
			lastRaftPosition = null;
			TestRaftFarAway = false;
			sailedSinceSpawn = 0f;
			nextTick = 0f; // (stream the islands in at once: a player may be standing on one - PlayerHold)
			// (Raft's islands counted afresh: the same world loaded again kept its GUID, so the old set stayed - its keys are
			// instance ids of the scene left - and every island around the raft counted as met again. Audit 2026-10-06)
			raftIslandsCounted.Clear();
			countedFor = Guid.Empty;
			countPrimed = false;
			LoadPool(true);
		}

		public static void OnWorldShift(Vector3 shift)
		{
			if (lastRaftPosition.HasValue) lastRaftPosition = lastRaftPosition.Value - shift;
		}

		/// <summary>How often the islands near the players were streamed in this world (0 until the first time) - the
		/// tests' readiness check (DevTests.WaitForWorld) waits for one, instead of a fixed wait.</summary>
		public static int StreamTicks;

		/// <summary>Called every frame by the mod.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + TickInterval;
			if (!LoadSceneManager.IsGameSceneLoaded) { lastRaftPosition = null; StreamTicks = 0; return; }
			Vector3? pos = RaftPosition;
			if (!pos.HasValue) return;

			// Every machine streams its own copy of the island list; only the host adds islands
			// (host: not while the list is still the world played before - a world loading after another; its save checks the same)
			if (Raft_Network.IsHost && !IslandWorldState.ForThisWorld) return;
			StreamIslands(pos.Value);
			StreamTicks++;
			if (!Raft_Network.IsHost) { lastRaftPosition = null; return; }
			LoadPool(false);
			// (this world's play time: game time, so a paused game doesn't count)
			float gameNow = Time.time;
			if (lastGameTime > 0f && gameNow > lastGameTime && gameNow - lastGameTime < 60f && WorldIslands.PlaySeconds < WorldIslands.LongPlayed) WorldIslands.PlaySeconds += gameNow - lastGameTime;
			lastGameTime = gameNow;

			float sailed = lastRaftPosition.HasValue ? Flat(pos.Value - lastRaftPosition.Value).magnitude : 0f;
			lastRaftPosition = pos.Value;
			// A teleport or a very long frame is not sailing
			if (sailed <= 0.01f || sailed > 200f) return;
			sailedSinceSpawn += sailed;
			WorldDirector.Sailed += sailed;
			WorldRandomizer.OnSailed(sailed, pos.Value); // (its own islands, also in worlds without random custom islands)
			GhostRafts.OnSailed(sailed, pos.Value); // (the world option Ghost rafts)
			TraderRaft.OnSailed(sailed, pos.Value); // (the world option Trader raft)
			if (!Enabled || ChancePerKm <= 0f) return;
			// One random custom island after every few of Raft's own islands met (the world's span, WorldIslands.GapMin -
			// GapMax: 3-6 by default) - not by distance: however many islands are ticked, they never crowd the sea
			CountRaftIslands();
			// None in a new world's first minutes (the user: at least 10)
			if (WorldIslands.PlaySeconds < QuietMinutes * 60f) return;
			if (WorldIslands.Target <= 0) WorldIslands.NewTarget();
			if (WorldIslands.RaftIslandsSince < WorldIslands.Target) return;
			// (its turn: tried every 10 s of sailing until there's a free spot - not at one of Raft's islands)
			if (Time.unscaledTime < nextTry) return;
			nextTry = Time.unscaledTime + 10f;
			TakeTurn(pos.Value);
		}

		/// <summary>A random custom island's turn (host): one from the world's pool ahead of the raft. With nothing in the pool
		/// (every island left out in Choose islands, or the only ones ticked are here or finished) it says so once and waits
		/// quietly - it used to log "the spawn pool is empty" every 10 s of sailing (UW4). Returns what happened.</summary>
		internal static string TakeTurn(Vector3 pos)
		{
			if (Pool().Count == 0)
			{
				if (!saidEmpty) Debug.Log("[CUSTOM ISLANDS] No random custom island can come now: every island of this world's pool is left out (Choose islands), here already or finished");
				saidEmpty = true;
				return "nothing in the pool";
			}
			saidEmpty = false;
			spawned = false;
			string said = TrySpawn(pos, false);
			if (spawned)
			{
				Debug.Log("[CUSTOM ISLANDS] Random island after " + WorldIslands.RaftIslandsSince + " of Raft's islands (span " + WorldIslands.GapMin + "-" + WorldIslands.GapMax + "): " + said);
				WorldIslands.NewTarget();
				IslandWorldState.Save();
			}
			return said;
		}

		static float nextTry, lastGameTime;
		static bool spawned;
		/// <summary>The empty pool has been told about (once until something is in it again).</summary>
		internal static bool saidEmpty;
		/// <summary>No random custom island in a new world's first so many minutes of play (spawnpool.txt quietMinutes).</summary>
		public static float QuietMinutes = 10f;
		/// <summary>Raft's islands already counted (instance and spawn), and the world they were counted in.</summary>
		static readonly HashSet<long> raftIslandsCounted = new HashSet<long>();
		static Guid countedFor;
		static bool countPrimed;

		/// <summary>Host: counts Raft's own plain islands the raft meets (their ground switched on - within about a kilometre;
		/// story islands, the stranded boat, floating rafts don't count). Those around the raft when a world loads count
		/// as met already.</summary>
		static void CountRaftIslands()
		{
			if (SaveAndLoad.WorldGuid != countedFor) { raftIslandsCounted.Clear(); countedFor = SaveAndLoad.WorldGuid; countPrimed = false; }
			int added = 0;
			foreach (Landmark l in WorldManager.AllLandmarks)
			{
				if (l == null || !l.isSpawned || !WorldRandomizer.GroundOn(l) || !WorldRandomizer.IsNatural(l)) continue;
				long key = ((long)l.GetInstanceID() << 32) ^ WorldRandomizer.SpawnKey(l);
				if (raftIslandsCounted.Add(key) && countPrimed) added++;
			}
			countPrimed = true;
			if (added > 0)
			{
				WorldIslands.RaftIslandsSince += added;
				Debug.Log("[CUSTOM ISLANDS] Raft's islands met since the last random custom island: " + WorldIslands.RaftIslandsSince + " of " + WorldIslands.Target);
			}
		}

		/// <summary>Tests: count one of Raft's islands as met.</summary>
		internal static void CountRaftIslandForTest(int n) { WorldIslands.RaftIslandsSince += n; }

		#region Streaming

		/// <summary>Unloads far-away islands (keeping their entry) and loads them again when the raft comes back.</summary>
		/// <summary>Tests (SC46/SC47, player 2): stream as if the raft were far away too - only this player's position counts.</summary>
		internal static bool TestRaftFarAway;

		static void StreamIslands(Vector3 raftPos)
		{
			// (by the raft, and by the players: an island someone stands on stays, however far the raft drifts, and loads
			// for a player who comes back to a world standing on it - PlayerHold. The host keeps an island loaded while
			// any player is at it: its creatures, ambushes, shared actions and where each player stood live on the host's
			// copy - a crew that splits up, three players at an island far behind the raft, still has all of it)
			var players = new List<Vector3>();
			if (Raft_Network.IsHost) players.AddRange(Players.All.Where(p => p != null).Select(p => p.transform.position));
			else { Network_Player player = RAPI.GetLocalPlayer(); if (player != null) players.Add(player.transform.position); }
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.ToList())
			{
				// (to the land's edge, not its centre: a player at the edge of a big island dropped into the sea when the
				// centre was further away than the unload distance)
				float edge = e.WaitingForFile ? 0f : Mathf.Max(0f, LandRadius(e.Name));
				float d = TestRaftFarAway ? float.PositiveInfinity : Mathf.Max(0f, Flat(e.Position - raftPos).magnitude - edge);
				foreach (Vector3 pp in players) d = Mathf.Min(d, Mathf.Max(0f, Flat(e.Position - pp).magnitude - edge));
				// The randomizer's extras on one of Raft's islands are there only while Raft's island is
				if (WorldRandomizer.IsExtras(e) && !WorldRandomizer.HasIslandUnder(e)) d = float.PositiveInfinity;
				// (the host's distance on every machine: WorldRules)
				float unload = WorldRules.UnloadDistance;
				// Loads again well inside the unload distance, whatever the spawn distance: with a reload distance past the
				// unload distance an island loaded and unloaded every 2 s
				float reload = Mathf.Min(Mathf.Max(unload - ReloadHysteresis, SpawnDistanceMax + 50f), unload - 100f);
				if (e.Root != null && d > unload)
				{
					IslandObjectState.Capture(e);
					IslandSpawner.Despawn(e.Root);
					e.Root = null;
					Debug.Log("[CUSTOM ISLANDS] Unloaded island '" + e.Name + "' (" + d.ToString("F0") + " m away)");
				}
				else if (e.Root == null && !e.Loading && !e.Failed && !e.WaitingForFile && !e.Excluded && d < reload)
				{
					e.Loading = true;
					DynamicIslands.instance.StartCoroutine(DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e));
				}
			}
		}

		#endregion

		#region Spawning

		/// <summary>
		/// Tries to place an island from the pool ahead of the raft. Returns a message saying what happened.
		/// force: ignore "raft is inside one of Raft's islands" and, with nothing free ahead, look all round the raft (dev/testing, Traders bring).
		/// seed: what a new map type island is and where it goes ahead come from it instead of unseeded randoms (the world
		/// randomizer's islands follow the world seed - CA18); retry: this try's number for the same island (another spot,
		/// the same island).
		/// </summary>
		public static string TrySpawn(Vector3 raftPos, bool force, string pick = null, int? seed = null, int retry = 0)
		{
			System.Random placing = seed.HasValue ? new System.Random(WorldRandomizer.Hash(seed.Value, retry, 31)) : null;
			Func<float, float, float> range = (a, b) => placing != null ? a + (float)placing.NextDouble() * (b - a) : UnityEngine.Random.Range(a, b);
			if (!force && ChunkManager.RaftIsInsideChunkPoint) return Skip("the raft is at one of Raft's islands");
			// (pick: this pool entry instead of one from the pool - the world randomizer's islands)
			string name = pick ?? PickFromPool();
			if (name == null) return Skip("the spawn pool is empty");

			// A brand-new island: random settings now, the island itself is generated once a spot is found
			IslandGenSettings generate = null;
			float elevation = 0f;
			MapType mapType = null;
			if (name.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase))
			{
				// A new island of a map type (type:<name> in the pool)
				mapType = MapTypes.Get(name.Substring(TypePrefix.Length));
				if (mapType == null) return Skip("there is no map type '" + name.Substring(TypePrefix.Length) + "'");
				if (seed.HasValue)
				{
					// (rolled as RemakeOf rolls it from the name's seed: a lost file comes back as the very same settings)
					int islandSeed = 1 + (int)((uint)WorldRandomizer.Hash(seed.Value, 37) % 999998u);
					generate = MapTypes.Roll(mapType, new System.Random(islandSeed), out elevation);
					generate.Seed = islandSeed;
				}
				else generate = MapTypes.Roll(mapType, new System.Random(), out elevation);
				WorldRandomizer.GatherFor(generate, mapType.Name, WorldRandomizer.Current);
				name = MapTypes.FreeFileName(mapType, generate);
				radiusCache[name] = MapTypes.EstimatedRadius(generate);
				elevationCache[name] = elevation;
			}
			else if (name == GeneratedEntry)
			{
				// (seeded like the map-type islands when the world gives a seed - CB14)
				var rnd = seed.HasValue ? new System.Random(WorldRandomizer.Hash(seed.Value, 41)) : new System.Random();
				generate = IslandGenerator.RandomSettings(rnd, GeneratedStyles);
				GatherFor(generate);
				if (rnd.NextDouble() < GeneratedFlyingChance) elevation = 40f + (float)rnd.NextDouble() * 50f;
				name = FreeName(GeneratedPrefix + TerrainPainter.StyleName(generate.Style).ToLowerInvariant() + "-" + generate.Seed);
				// (used for placing until the real file exists)
				radiusCache[name] = MapTypes.EstimatedRadius(generate);
				elevationCache[name] = elevation;
			}
			// An island this world already has (not finished - the pool leaves those out - and left behind): it comes back
			// ahead of the raft as it was, its quest and chests kept, instead of a second, fresh copy
			if (generate == null)
			{
				IslandWorldState.Entry known = InWorld(name);
				if (known != null)
				{
					if (!ReturningIslands.BringAgain(known, raftPos)) return Skip("no free spot ahead of the raft for '" + name + "' (it comes again)");
					sailedSinceSpawn = 0f;
					spawned = true;
					return "'" + name + "' comes again, " + Flat(known.Position - raftPos).magnitude.ToString("F0") + " m ahead";
				}
			}
			float radius = LandRadius(name);
			if (radius < 0) return Skip("could not read island '" + name + "'");

			Vector3 dir = SailDirection();
			var reasons = new List<string>();
			for (int attempt = 0; attempt < (TestAllRound || force ? 24 : 8); attempt++)
			{
				// Off-centre so it's reachable but not always dead ahead; later attempts spread wider
				float side = range(0f, 1f) < 0.5f ? -1f : 1f;
				// (forced, with nothing free ahead: all round the raft, the way there may pass one of Raft's islands)
				bool round = TestAllRound || (force && attempt >= 8);
				float angle = round ? range(0f, 360f) : side * range(10f, 35f + attempt * 10f);
				// Later attempts also look a little further out
				float distance = Mathf.Max(range(SpawnDistanceMin, SpawnDistanceMax + attempt * (round ? 40f : 20f)), radius + Clearance + RaftRadius);
				// (its land not beyond where islands load: the later, further tries of a far spawn distance unloaded it at once)
				distance = Mathf.Min(distance, Mathf.Max(radius + Clearance + RaftRadius, WorldRules.UnloadDistance - 100f + radius));
				Vector3 candidate = raftPos + Quaternion.Euler(0, angle, 0) * dir * distance;
				candidate.y = Elevation(name); // 0 = sea level; flying / underwater islands keep their height

				string why = Rejects(candidate, radius, raftPos, !round);
				if (why != null) { reasons.Add(why); continue; }

				sailedSinceSpawn = 0f;
				spawned = true;
				Debug.Log("[CUSTOM ISLANDS] Auto spawn: island '" + name + "' (land radius " + radius.ToString("F0") + " m) at " + candidate +
					", " + distance.ToString("F0") + " m from the raft at " + angle.ToString("F0") + " degrees");
				// In the list straight away, so spacing checks see it while it loads (clients hear about a generated
				// island only once its file exists, so they can fetch it)
				IslandWorldState.Entry entry = IslandWorldState.Add(name, candidate, null, generate == null);
				entry.Loading = true;
				if (generate != null)
				{
					IslandGenSettings s = generate; float el = elevation; string n = name;
					MapType t = mapType;
					DynamicIslands.instance.StartCoroutine(GenerateAndSpawn(() => { if (t != null) return MapTypes.Create(t, s, el, n); IslandFile f = IslandGenerator.CreateFile(s, n); f.Elevation = el; return f; }, name, entry));
				}
				else DynamicIslands.instance.StartCoroutine(DynamicIslands.instance.SpawnIslandFile(name, candidate, true, entry));
				return "Spawning '" + name + "' " + distance.ToString("F0") + " m ahead";
			}
			return Skip("no free spot ahead of the raft for '" + name + "' (" + string.Join("; ", reasons.Distinct().Take(3).ToArray()) + ")");
		}

		/// <summary>Tests (CISpawnGenerated): look all round the raft, not only ahead (Raft's own islands ahead made the
		/// join-while-generating test depend on the sea's layout).</summary>
		internal static bool TestAllRound;

		/// <summary>Tests (CIGenDelay): a generated island's file is written this many seconds late - a player joins meanwhile (AT1).</summary>
		public static float TestGenerateDelay;

		/// <summary>Generates a new island file (roadmap 1.5; map types), saves it next to the others, tells clients, and spawns it.</summary>
		internal static System.Collections.IEnumerator GenerateAndSpawn(Func<IslandFile> create, string name, IslandWorldState.Entry entry)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			if (TestGenerateDelay > 0f) yield return new WaitForSecondsRealtime(TestGenerateDelay);
			IslandFile file;
			try { file = create(); }
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Generating island '" + name + "' failed: " + e);
				IslandWorldState.RemoveIds(new[] { entry.Id }, false);
				yield break;
			}
			// (pieces whose scenes weren't loaded yet get their own scale - the radio tower's floors were 3 x 3 m, IG6)
			yield return PlaceableCatalog.ScaleUnloaded(file);
			try
			{
				file.Save(IslandSpawner.PathFor(name));
				float estimated;
				bool hadEstimate = radiusCache.TryGetValue(name, out estimated);
				radiusCache[name] = IslandSpawner.LandRadius(file);
				// (placed by an estimated size: bigger than that, it may reach the raft or another island - moved further out
				// along the same line until it is clear - AU68)
				if (hadEstimate && radiusCache[name] > estimated + 1f && RaftPosition.HasValue && IslandWorldState.Contains(entry)) MakeRoom(entry, radiusCache[name]);
				elevationCache[name] = file.Elevation;
				Debug.Log("[CUSTOM ISLANDS] Generated island '" + name + "': " + (string.IsNullOrEmpty(file.Style) ? "Tropical" : file.Style) + ", land radius " + radiusCache[name].ToString("F0") + " m, " +
					file.Objects.Count + " objects" + (file.Elevation > 0f ? ", flying " + file.Elevation.ToString("F0") + " m up" : file.Elevation < 0f ? ", " + (-file.Elevation).ToString("F0") + " m under water" : ""));
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Generating island '" + name + "' failed: " + e);
				IslandWorldState.RemoveIds(new[] { entry.Id }, false);
				yield break;
			}
			if (!IslandWorldState.Contains(entry)) yield break; // removed meanwhile
			IslandNetwork.BroadcastAdded(entry);
			yield return DynamicIslands.instance.SpawnIslandFile(name, entry.Position, true, entry);
		}

		/// <summary>
		/// A generated island's file made again from its name (ROADMAP R15: a world whose gen-&lt;kind&gt;-&lt;seed&gt;[-n] file was
		/// deleted): a new island of the same kind and seed - the same style and seed for a random one, the map type's
		/// settings rolled from the seed for the others. Not the very same island (its size was rolled too), but one of its
		/// kind in its place. Null when the name isn't one of those.
		/// </summary>
		internal static Func<IslandFile> RemakeOf(string name)
		{
			if (name == null || !name.StartsWith(GeneratedPrefix, StringComparison.OrdinalIgnoreCase) || IslandNetwork.IsDownloadName(name)) return null;
			List<string> parts = name.Substring(GeneratedPrefix.Length).Split('-').ToList();
			int seed, extra;
			// (a name made free with -2, -3...: the seed is the number before it)
			if (parts.Count >= 3 && int.TryParse(parts[parts.Count - 1], out extra) && int.TryParse(parts[parts.Count - 2], out seed)) parts.RemoveAt(parts.Count - 1);
			if (parts.Count < 2 || !int.TryParse(parts[parts.Count - 1], out seed)) return null;
			string kind = string.Join("-", parts.Take(parts.Count - 1).ToArray());
			int style = Array.FindIndex(TerrainPainter.Styles, st => st.Name.Equals(kind, StringComparison.OrdinalIgnoreCase));
			if (style >= 0)
			{
				return () =>
				{
					IslandGenSettings s = IslandGenerator.RandomSettings(new System.Random(seed), new[] { style });
					s.Seed = seed; s.Style = style;
					GatherFor(s);
					return IslandGenerator.CreateFile(s, name);
				};
			}
			MapType type = MapTypes.Get(kind);
			if (type == null) return null;
			return () =>
			{
				float elevation;
				IslandGenSettings s = MapTypes.Roll(type, new System.Random(seed), out elevation);
				s.Seed = seed;
				WorldRandomizer.GatherFor(s, type.Name, WorldRandomizer.Current);
				return MapTypes.Create(type, s, elevation, name);
			};
		}

		/// <summary>Host: the world's generated island whose file is gone, made again in its place (its old state cleared) and spawned.</summary>
		internal static System.Collections.IEnumerator RemakeAndSpawn(Func<IslandFile> create, IslandWorldState.Entry entry)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			string name = entry.HostName;
			IslandFile file = null;
			Exception failed = null;
			try { file = create(); }
			catch (Exception e) { failed = e; }
			if (failed == null) yield return PlaceableCatalog.ScaleUnloaded(file); // (IG6)
			try
			{
				if (failed != null) throw failed;
				file.Save(IslandSpawner.PathFor(name));
				radiusCache.Remove(name); elevationCache.Remove(name);
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Making '" + name + "' again failed: " + e);
				entry.Loading = false; entry.Failed = true;
				yield break;
			}
			entry.State.Clear();
			entry.Hash = null;
			DynamicIslands.Notify("This world's island '" + name + "' was missing from Mods\\DynamicIslands: a new island of the same kind was made in its place", true);
			Debug.Log("[CUSTOM ISLANDS] Made '" + name + "' again (its file was gone)");
			yield return DynamicIslands.instance.SpawnIslandFile(name, entry.Position, true, entry);
		}

		static string Skip(string why)
		{
			Debug.Log("[CUSTOM ISLANDS] Auto spawn skipped: " + why);
			return "Skipped: " + why;
		}

		/// <summary>
		/// A place around the raft where an island of this land radius fits clear of the raft, the other custom islands
		/// and Raft's own islands (nearest first, up to maxDistance), or null. Used by tests and to warn SpawnIsland.
		/// </summary>
		public static Vector3? FindClearSpot(Vector3 raftPos, float radius, float maxDistance)
		{
			for (float d = radius + Clearance + RaftRadius + 10f; d <= Mathf.Max(maxDistance, radius + Clearance + RaftRadius + 10f); d += 25f)
				for (int i = 0; i < 24; i++)
				{
					float a = i * 15f * Mathf.Deg2Rad;
					Vector3 c = raftPos + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * d;
					c.y = 0f;
					if (Rejects(c, radius, raftPos, false) == null) return c; // (the way there may cross an island: tests teleport)
				}
			return null;
		}

		/// <summary>
		/// A free spot ahead of the raft for an island of this land radius, as a new island gets one (off-centre, a little
		/// further out each try), at height y, or null. ignore: the island that is moving there itself (ReturningIslands);
		/// other custom islands are kept clear of by their land, not the spacing new islands keep.
		/// </summary>
		internal static Vector3? SpotAhead(Vector3 raftPos, float radius, float y, IslandWorldState.Entry ignore)
		{
			Vector3 dir = SailDirection();
			for (int attempt = 0; attempt < 12; attempt++)
			{
				float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
				// (at most 80 degrees off: the later tries spread to 145, behind the raft - CIPoolAgain, 2026-10-18)
				float angle = side * UnityEngine.Random.Range(10f, Mathf.Min(80f, 35f + attempt * 10f));
				float distance = Mathf.Max(UnityEngine.Random.Range(SpawnDistanceMin, SpawnDistanceMax + attempt * 20f), radius + Clearance + RaftRadius);
				Vector3 c = raftPos + Quaternion.Euler(0, angle, 0) * dir * distance;
				c.y = y;
				if (Rejects(c, radius, raftPos, true, 0f, ignore) == null) return c;
			}
			return null;
		}

		/// <summary>Why an island of this land radius shouldn't go at candidate because one of Raft's islands is there, or null.</summary>
		public static string OverlapsRaftIsland(Vector3 candidate, float radius)
		{
			string why = Rejects(candidate, radius, candidate + Vector3.one * 100000f);
			return why != null && why.StartsWith("Raft's") ? why : null;
		}

		/// <summary>
		/// Why an island of this land radius can't go at candidate, or null if it can. minSpacing: centre-to-centre
		/// distance kept from other custom islands (-1 = the minSpacing setting; 0 = just clear of their land).
		/// </summary>
		/// <summary>A generated island turned out bigger than estimated: moved out from the raft until nothing is in its way.</summary>
		static void MakeRoom(IslandWorldState.Entry entry, float radius)
		{
			Vector3 raft = RaftPosition.Value;
			Vector3 dir = Flat(entry.Position - raft);
			if (dir.sqrMagnitude < 1f) dir = Vector3.forward;
			dir.Normalize();
			// (just clear of the other islands' land: a plan's island was placed near another one, closer than minSpacing)
			for (int i = 0; i <= 8; i++)
			{
				if (Rejects(entry.Position, radius, raft, false, 0f, entry) == null) return;
				if (i == 8) break;
				Vector3 at = entry.Position + dir * 40f;
				Debug.Log("[CUSTOM ISLANDS] Generated island '" + entry.Name + "' is bigger than estimated (" + radius.ToString("F0") + " m): moved out to " + at.ToString("F0"));
				entry.Position = at;
			}
			// (still in the way of something after the last move - one of Raft's islands further out: a free spot ahead instead)
			Vector3? free = SpotAhead(raft, radius, entry.Position.y, entry);
			if (free.HasValue) entry.Position = free.Value;
			Debug.Log("[CUSTOM ISLANDS] Generated island '" + entry.Name + "': no room along its line" + (free.HasValue ? ", moved to " + free.Value.ToString("F0") : ", left at " + entry.Position.ToString("F0")));
		}

		/// <summary>
		/// How far the raft's blocks reach from its centre (m; 0 without a raft) - the room it needs, not only its centre
		/// (AU68: a big raft ran into islands placed just clear of its middle). Looked at every 10 s.
		/// </summary>
		public static float RaftRadius
		{
			get
			{
				if (Time.unscaledTime < raftRadiusAt) return raftRadius;
				raftRadiusAt = Time.unscaledTime + 10f;
				raftRadius = 0f;
				try
				{
					Raft raft = ComponentManager<Raft>.Value;
					List<Block> blocks = BlockCreator.GetPlacedBlocks();
					if (raft == null || blocks == null) return raftRadius;
					Vector3 c = raft.transform.position;
					foreach (Block b in blocks)
						if (b != null && b.transform.IsChildOf(raft.transform)) raftRadius = Mathf.Max(raftRadius, Flat(b.transform.position - c).magnitude);
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] The raft's size: " + e.Message); }
				return raftRadius;
			}
		}
		static float raftRadius, raftRadiusAt;

		internal static string Rejects(Vector3 candidate, float radius, Vector3 raftPos, bool checkPath = true, float minSpacing = -1f, IslandWorldState.Entry ignore = null)
		{
			if (Flat(candidate - raftPos).magnitude < radius + Clearance + RaftRadius) return "too close to the raft (it reaches " + RaftRadius.ToString("F0") + " m)";
			// (tests of join timing - CISpawnGenerated: any spot clear of the raft; the sea's layout isn't what they test)
			if (TestAllRound) return null;
			float spacing = minSpacing < 0f ? MinSpacing * WorldIslands.VoyageFactor : minSpacing;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (WorldRandomizer.IsExtras(e)) continue; // (on one of Raft's islands: its chunk point below keeps the room)
				if (e == ignore) continue; // (the island moving there itself)
				float d = Flat(candidate - e.Position).magnitude;
				if (d < Mathf.Max(spacing, radius + LandRadius(e.Name) + Clearance)) return "custom island '" + e.Name + "' " + d.ToString("F0") + " m away";
			}
			ChunkManager cm = ComponentManager<ChunkManager>.Value;
			if (cm != null)
			{
				foreach (ChunkPoint cp in cm.GetAllChunkPointsList())
				{
					// The rule's overlap radius is the footprint Raft keeps free around its island; floating rafts are
					// small drifting wrecks, so they only need a little room (Raft packs its points densely: about a
					// dozen within 1 km, so being stricter leaves hardly any open sea)
					bool raftWreck = cp.rule != null && cp.rule.name.IndexOf("FloatingRaft", StringComparison.OrdinalIgnoreCase) >= 0;
					float overlap = cp.rule != null ? cp.rule.collisionOverlapRadius : 150f;
					float d = Flat(candidate - cp.worldPosition).magnitude;
					if (d < (raftWreck ? 20f : overlap) + radius) return "Raft's " + (cp.rule != null ? cp.rule.name : "island") + " " + d.ToString("F0") + " m away";
				}
				if (checkPath && cm.DoesLineIntersectWithChunkPoints(raftPos, candidate)) return "one of Raft's islands is in the way";
			}
			return null;
		}

		internal static Vector3 SailDirection()
		{
			Raft r = CurrentRaft;
			if (r != null && r.body != null)
			{
				Vector3 v = Flat(r.body.velocity);
				if (v.sqrMagnitude > 0.04f) return v.normalized;
			}
			Vector3 d = Flat(Raft.direction);
			return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
		}

		/// <summary>How far (m) the island's land reaches from its land centre; cached. -1 if the file can't be read.</summary>
		public static float LandRadius(string name)
		{
			float r;
			if (radiusCache.TryGetValue(name, out r)) return r;
			try
			{
				IslandFile file = IslandFile.Load(IslandSpawner.PathFor(name));
				r = IslandSpawner.LandRadius(file);
				elevationCache[name] = file.Elevation;
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read island '" + name + "': " + ex.Message); r = -1; }
			radiusCache[name] = r;
			return r;
		}

		static readonly Dictionary<string, float> elevationCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

		/// <summary>An island file written again: its size read from it anew.</summary>
		internal static void ForgetSize(string name) { radiusCache.Remove(name); elevationCache.Remove(name); }

		/// <summary>Placing an island whose file doesn't exist yet (being generated): its size until then.</summary>
		internal static void CacheSize(string name, float radius, float elevation)
		{
			radiusCache[name] = radius;
			elevationCache[name] = elevation;
		}

		/// <summary>The island's saved elevation above sea level (flying / underwater), cached with its radius.</summary>
		internal static float Elevation(string name)
		{
			float e;
			LandRadius(name);
			return elevationCache.TryGetValue(name, out e) ? e : 0f;
		}

		#endregion

		#region Pool (spawnpool.txt)

		/// <summary>
		/// The islands taking part and their weights, with "*" expanded to every saved island not listed. forWorld: leave out
		/// what this world left out (WorldIslands - chosen in the New Game box); without it, the whole pool (the list shown there).
		/// </summary>
		public static List<KeyValuePair<string, float>> Pool(bool forWorld = true)
		{
			var result = new List<KeyValuePair<string, float>>();
			// Copies downloaded from a multiplayer host (<name>_<hash>) and islands generated while sailing (gen-...,
			// which live on in their worlds) only join the pool when listed by name
			// (also never: the world randomizer's extras (rnd-...), which belong to one of Raft's islands in one world; and the
			// islands any world plan brings, even when listed by name - they only come in their own plans: a plan's island that
			// turned up by chance in another world was out of its story - the user, 2026-10-06)
			HashSet<string> ofPlans = AllPlansIslandNames();
			// (the islands folder read once: a pool line by name looked at it again each time - 0.2 s a pick, US1)
			var all = new HashSet<string>(IslandSpawner.ListSavedIslands(), StringComparer.OrdinalIgnoreCase);
			var saved = IslandSpawner.ListSavedIslands().Where(n => !IslandNetwork.IsDownloadName(n) && !n.StartsWith(GeneratedPrefix, StringComparison.OrdinalIgnoreCase) &&
				!n.StartsWith(WorldRandomizer.ExtrasPrefix, StringComparison.OrdinalIgnoreCase) && !ofPlans.Contains(n)).ToList();
			var listed = new HashSet<string>(poolLines.Where(p => p.Key != "*").Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
			var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var p in poolLines)
			{
				if (p.Key == "*")
				{
					foreach (string s in saved)
						if (!listed.Contains(s) && taken.Add(s)) result.Add(new KeyValuePair<string, float>(s, p.Value));
				}
				else if (all.Contains(p.Key))
				{
					if (!ofPlans.Contains(p.Key)) result.Add(p);
				}
				else if (p.Key.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase) && MapTypes.Get(p.Key.Substring(TypePrefix.Length)) != null)
					result.Add(p);
			}
			if (GeneratedWeight > 0f) result.Add(new KeyValuePair<string, float>(GeneratedEntry, GeneratedWeight));
			HashSet<string> planned = forWorld ? PlanIslandNames() : new HashSet<string>();
			// (the world's islands by name, once: each name looked through all of them - 40 ms with 150 islands, US1)
			ILookup<string, IslandWorldState.Entry> inWorld = forWorld ? IslandWorldState.Islands.ToLookup(e => e.HostName ?? e.Name ?? "", StringComparer.OrdinalIgnoreCase) : null;
			var pool = result.Where(p => p.Value > 0f && (!forWorld || (WorldIslands.TakesPart(p.Key) && !planned.Contains(p.Key) && !NotAgain(p.Key, inWorld[p.Key]) && !TooEarly(p.Key)))).ToList();
			IslandCache.SaveNotBefore();
			return pool;
		}

		/// <summary>An island whose "not before" (IslandProps.NotBeforeKm) this world hasn't sailed yet: not picked by chance until then.</summary>
		internal static bool TooEarly(string name)
		{
			if (name == GeneratedEntry || name.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase)) return false;
			float km;
			// (from a cache kept on disk: reading every pool island's file for it made a fresh host's first picks slow, IM43)
			return float.TryParse(IslandCache.NotBeforeKm(name), NumberStyles.Float, CultureInfo.InvariantCulture, out km) &&
				WorldDirector.Sailed < km * 1000f;
		}

		static HashSet<string> allPlans;
		static float allPlansAt = -100f;

		/// <summary>
		/// Every island the saved world plans bring by name, and the islands those islands' own rules bring (looked at again
		/// every 10 s at most): never in the random pool, only in their plans.
		/// </summary>
		public static HashSet<string> AllPlansIslandNames()
		{
			if (allPlans != null && Time.unscaledTime - allPlansAt < 10f) return allPlans;
			var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Action<IEnumerable<IntroRule>, Queue<string>> take = (rules, next) =>
			{
				foreach (IntroRule r in rules)
				{
					var found = new List<string>();
					if (r.What == "island" && r.WhatArg.Trim().Length > 0) found.Add(r.WhatArg.Trim());
					else if (r.What == "oneof") found.AddRange(r.WhatArg.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0));
					foreach (string n in found) if (names.Add(n)) next.Enqueue(n);
				}
			};
			try
			{
				var queue = new Queue<string>();
				foreach (string p in WorldPlan.All())
				{
					if (WorldPlan.IsBuiltIn(p)) continue;
					WorldPlan plan = WorldPlan.Load(p);
					if (plan != null) take(plan.Rules, queue);
				}
				// (islands that bring other islands: those come with the plan too)
				for (int guard = 0; queue.Count > 0 && guard < 2000; guard++) take(IslandCache.RulesOf(queue.Dequeue()), queue);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking for the world plans' islands: " + e.Message); }
			allPlans = names;
			allPlansAt = Time.unscaledTime;
			return names;
		}

		/// <summary>
		/// Islands this world's plan and its islands' own rules bring by name (ROADMAP CW4): never by chance too - a plan's
		/// island that came by chance came twice (two Signal Rocks, their quests mixed up by name).
		/// </summary>
		internal static HashSet<string> PlanIslandNames()
		{
			var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var rules = new List<IntroRule>();
			try
			{
				if (WorldDirector.Plan != null) rules.AddRange(WorldDirector.Plan.Rules);
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands) rules.AddRange(WorldDirector.RulesOf(e));
			}
			catch { }
			foreach (IntroRule r in rules)
			{
				if (r.What == "island" && r.WhatArg.Trim().Length > 0) names.Add(r.WhatArg.Trim());
				else if (r.What == "oneof") foreach (string n in r.WhatArg.Split(',')) if (n.Trim().Length > 0) names.Add(n.Trim());
			}
			return names;
		}

		/// <summary>The island of this world with this name (a saved island the spawn pool brought before), or null.</summary>
		internal static IslandWorldState.Entry InWorld(string name)
		{
			return IslandWorldState.Islands.FirstOrDefault(e => !WorldRandomizer.IsExtras(e) && !e.Failed &&
				string.Equals(e.HostName ?? e.Name, name, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Whether players haven't read all the notes on this island yet.</summary>
		internal static bool NotesLeft(IslandWorldState.Entry e)
		{
			int found, total;
			return NoteCount.OfIsland(e.HostName, out found, out total) && found < total;
		}

		/// <summary>An island the pool must not pick now: this world has it and it is finished (its quest done, or reached when
		/// it has no quest, and none of its notes left unread - it never comes again), or it is here already (loaded or loading
		/// near the players). An island the world has that isn't finished and was left behind may be picked: it comes back as
		/// it was (TrySpawn) - also one whose quest is done but with a note missed (the user, 2026-10-08).</summary>
		internal static bool NotAgain(string name) { return NotAgain(name, IslandWorldState.Islands); }

		static bool NotAgain(string name, IEnumerable<IslandWorldState.Entry> islands)
		{
			if (name == GeneratedEntry || name.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase)) return false;
			foreach (IslandWorldState.Entry e in islands)
			{
				if (WorldRandomizer.IsExtras(e) || !string.Equals(e.HostName ?? e.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
				if (e.Failed) continue;
				if ((ReturningIslands.Finished(e) && !NotesLeft(e)) || e.Root != null || e.Loading || e.WaitingForFile) return true;
			}
			return false;
		}

		/// <summary>Dev tests: the next pick, instead of a random one.</summary>
		internal static string ForceNextPick;

		internal static string PickFromPool()
		{
			if (ForceNextPick != null) { string forced = ForceNextPick; ForceNextPick = null; return forced; }
			return PickFrom(Pool(), UnityEngine.Random.value);
		}

		/// <summary>An island this world already has (left behind, not finished) counts this much of its weight: it may come
		/// back, but islands not seen yet come first (the user, 2026-10-08: the same island came several times early on).</summary>
		public const float InWorldWeight = 0.25f;

		/// <summary>A weighted pick from the pool, roll 0-1 (islands this world has count InWorldWeight of their weight).</summary>
		internal static string PickFrom(List<KeyValuePair<string, float>> pool, float roll01)
		{
			if (pool.Count == 0) return null;
			var weights = pool.Select(p => p.Value * (p.Key != GeneratedEntry && InWorld(p.Key) != null ? InWorldWeight : 1f)).ToList();
			float total = weights.Sum();
			if (total <= 0f) return null;
			float roll = roll01 * total;
			for (int i = 0; i < pool.Count; i++)
			{
				roll -= weights[i];
				if (roll <= 0f) return pool[i].Key;
			}
			return pool[pool.Count - 1].Key;
		}

		const string DefaultPool =
@"# Custom Islands: islands that appear on their own while sailing (host only).
# Changes are picked up while the game runs.

# Random islands on (any number above 0) or off (0). How often they come is the world's own setting: one after every
# 3-6 of Raft's own islands met by default (World settings > Islands while sailing; WorldIslandsGap in a world).
chancePerKm = 0.25
# No random island in a new world's first minutes of play (game time; worlds made before this don't wait)
quietMinutes = 10
# Metres kept between custom islands (centre to centre)
minSpacing = 800
# How far ahead of the raft an island appears (m). Raft's camera renders to about 400 m.
spawnDistanceMin = 250
spawnDistanceMax = 350
# Islands further than this from the raft are unloaded (and come back when the raft returns)
unloadDistance = 800
# An island the players still need - its quest begun and not done, or one a world plan waits for - that the raft left
# behind comes back ahead of the raft after this many minutes (0 = never)
returnMinutes = 12
# Harvested trees and picked-up items grow back after this many in-game days (0 = never). Checked when an island loads.
regrowDays = 3
# Show custom islands as green dots on Raft's Receiver (1 = yes, 0 = no)
showOnReceiver = 1
# ... only for islands within this many metres of the receiver (0 = all of them); an island the players still need (a
# quest begun, one the world plan waits for) shows however far it is
receiverDistance = 2000
# The world plan new worlds get when none is chosen in the New Game box (plans are in Mods\DynamicIslands\plans)
defaultPlan = Random islands

# Brand-new random islands join the pool with this weight (0 = never). Each is saved as gen-<style>-<seed>.island.
generated = 1
# Styles they can have (Tropical, Snowy, Desert, Forest, Volcanic), and the chance that one is a flying island
generatedStyles = Tropical, Snowy, Desert, Forest, Volcanic
generatedFlyingChance = 0.1
# Their things to gather (the generator's Things to gather and Finds in the shallows): how much on the land and in the
# shallows (0-1, 0 = none), how deep the finds in the shallows go (2-20 m), and kinds switched off (palm, datepalm, mango,
# pineapple, watermelon, banana, pine, birch, berry, flower, dirt, hive, sand, clay, stone, iron, copper, scrap, clam,
# seaweed; comma separated, nothing = all on)
generatedGather = 0
generatedShallows = 0
generatedShallowsDepth = 6
generatedGatherOff =

# Islands taking part, one per line: <island name> <weight>
# A higher weight makes an island more likely. Weight 0 leaves it out.
# ""*"" stands for every saved island not listed by name.
* 1

# Map types join with type:<name> <weight>: a new island of that kind each time (sandbar, atoll, archipelago,
# stacks, boss, volcano, swamp, spire, treasure, camp, sunken, sky, wreck, or tropical, snowy, desert, forest, volcanic)
type:sandbar 0.4
type:wreck 0.4
type:atoll 0.3
type:sunken 0.2
";

		/// <summary>
		/// An island's weight in the random pool (My islands - ROADMAP T3): its own line, else the "*" line's for a saved
		/// island of the player's (copies from hosts and generated islands only take part by name), else 0.
		/// </summary>
		public static float PoolWeight(string name, out bool listed)
		{
			LoadPool(false);
			var own = poolLines.Where(p => p.Key != "*" && p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
			listed = own.Count > 0;
			if (listed) return own[0].Value;
			if (IslandNetwork.IsDownloadName(name) || name.StartsWith(GeneratedPrefix, StringComparison.OrdinalIgnoreCase)) return 0f;
			var star = poolLines.Where(p => p.Key == "*").ToList();
			return star.Count > 0 ? star[0].Value : 0f;
		}

		/// <summary>Gives an island its own line in spawnpool.txt with this weight (0 leaves it out of the random pool).</summary>
		public static void SetPoolWeight(string name, float weight)
		{
			LoadPool(false);
			List<string> lines = File.Exists(PoolPath) ? File.ReadAllLines(PoolPath).ToList() : new List<string>();
			string line = name + " " + Mathf.Max(0f, weight).ToString("0.###", CultureInfo.InvariantCulture);
			bool done = false;
			for (int i = 0; i < lines.Count; i++)
			{
				string l = lines[i].Trim();
				if (l.Length == 0 || l.StartsWith("#") || l.Contains("=")) continue;
				int sp = l.LastIndexOf(' ');
				float w;
				string n = sp > 0 && float.TryParse(l.Substring(sp + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out w) ? l.Substring(0, sp).Trim() : l;
				if (!n.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
				if (done) { lines.RemoveAt(i); i--; continue; }
				lines[i] = line; done = true;
			}
			if (!done)
			{
				// (after the "*" line, where the file lists islands by name)
				int star = lines.FindIndex(l => l.Trim().StartsWith("* ") || l.Trim() == "*");
				lines.Insert(star >= 0 ? star + 1 : lines.Count, line);
			}
			SafeFile.WriteAllLines(PoolPath, lines.ToArray());
			LoadPool(true);
		}

		/// <summary>Reads spawnpool.txt (creating it with defaults if missing) when it changed, or always when force is set.</summary>
		public static void LoadPool(bool force)
		{
			try
			{
				if (!File.Exists(PoolPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(PoolPath));
					SafeFile.WriteAllText(PoolPath, DefaultPool);
				}
				DateTime t = File.GetLastWriteTimeUtc(PoolPath);
				if (!force && t == poolFileTime) return;
				poolFileTime = t;
				// (islands may have been re-saved too; an island still being generated has no file yet - its estimated size and
				// height stay, or it was spaced as unreadable and never moved clear once it came out bigger)
				foreach (string n in radiusCache.Keys.ToList()) if (File.Exists(IslandSpawner.PathFor(n))) radiusCache.Remove(n);
				foreach (string n in elevationCache.Keys.ToList()) if (File.Exists(IslandSpawner.PathFor(n))) elevationCache.Remove(n);

				poolLines.Clear();
				foreach (string raw in File.ReadAllLines(PoolPath))
				{
					string line = raw.Trim();
					if (line.Length == 0 || line.StartsWith("#")) continue;
					int eq = line.IndexOf('=');
					if (eq > 0)
					{
						string key = line.Substring(0, eq).Trim().ToLowerInvariant(), value = line.Substring(eq + 1).Trim();
						if (key == "generatedstyles")
						{
							// A list of style names, e.g. "Tropical, Snowy"
							GeneratedStyles = value.Split(',', ' ').Where(x => x.Trim().Length > 0)
								.Select(x => Array.FindIndex(TerrainPainter.Styles, st => st.Name.Equals(x.Trim(), StringComparison.OrdinalIgnoreCase)))
								.Where(i => i >= 0).Distinct().ToArray();
							if (GeneratedStyles.Length == 0) { BadLine(line); GeneratedStyles = new[] { TerrainPainter.Tropical }; }
							continue;
						}
						if (key == "generatedgatheroff") { GeneratedGatherOff = GenGather.Clean(value); continue; }
						if (key == "defaultplan") { WorldDirector.DefaultPlan = value.Length > 0 ? value : WorldPlan.RandomName; continue; }
						float v;
						if (!ParseNumber(value, out v) || float.IsNaN(v) || float.IsInfinity(v)) { BadLine(line); continue; }
						if (!SetValue(key, v)) BadLine(line);
						continue;
					}
					// "<name> <weight>" (names may contain spaces), or just "<name>" for weight 1
					int sp = line.LastIndexOf(' ');
					float w;
					if (sp > 0 && ParseNumber(line.Substring(sp + 1), out w) && !float.IsNaN(w) && !float.IsInfinity(w))
						poolLines.Add(new KeyValuePair<string, float>(line.Substring(0, sp).Trim(), w));
					else
						poolLines.Add(new KeyValuePair<string, float>(line, 1f));
				}
				foreach (var kv in TestOverrides) SetValue(kv.Key, kv.Value);
				// (islands appear well inside the unload distance: one put beyond it unloaded at once, before anyone saw it)
				if (SpawnDistanceMax > UnloadDistance - SpawnUnloadGap)
				{
					Debug.LogWarning("[CUSTOM ISLANDS] " + PoolPath + ": spawnDistanceMax " + SpawnDistanceMax.ToString("F0", CultureInfo.InvariantCulture) + " is kept at " + (UnloadDistance - SpawnUnloadGap).ToString("F0", CultureInfo.InvariantCulture) +
						" (" + SpawnUnloadGap.ToString("F0", CultureInfo.InvariantCulture) + " m inside unloadDistance " + UnloadDistance.ToString("F0", CultureInfo.InvariantCulture) + ")");
					SpawnDistanceMax = UnloadDistance - SpawnUnloadGap;
				}
				if (SpawnDistanceMin > SpawnDistanceMax) SpawnDistanceMin = SpawnDistanceMax;
				WorldRules.OnPoolChanged(); // (host: the settings every player shares go out again if they changed)
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + PoolPath + ": " + ex.Message); }
		}

		/// <summary>A number setting of spawnpool.txt by its (lower-case) key; false if there is none.</summary>
		internal static bool SetValue(string key, float v)
		{
			float? c = Clamped(key, v);
			if (!c.HasValue) return false;
			v = c.Value;
			switch (key)
			{
				case "chanceperkm": ChancePerKm = v; break;
				case "quietminutes": QuietMinutes = v; break;
				case "minspacing": MinSpacing = v; break;
				case "spawndistancemin": SpawnDistanceMin = v; break;
				case "spawndistancemax": SpawnDistanceMax = v; break;
				case "unloaddistance": UnloadDistance = v; break;
				case "returnminutes": ReturningIslands.ReturnMinutes = v; break;
				case "regrowdays": RegrowDays = Mathf.RoundToInt(v); break;
				case "showonreceiver": ShowOnReceiver = v != 0f; break;
				case "receiverdistance": ReceiverDistance = v; break;
				case "generated": GeneratedWeight = v; break;
				case "generatedflyingchance": GeneratedFlyingChance = v; break;
				case "generatedgather": GeneratedGather = v; break;
				case "generatedshallows": GeneratedShallows = v; break;
				case "generatedshallowsdepth": GeneratedShallowsDepth = v; break;
			}
			return true;
		}

		/// <summary>A number setting kept within what it may be (the same for the file and the Defaults window); null if the key (lower-case) is none.</summary>
		internal static float? Clamped(string key, float v)
		{
			switch (key)
			{
				case "chanceperkm": return Mathf.Clamp01(v);
				case "quietminutes": return Mathf.Clamp(v, 0f, 240f);
				case "minspacing": return Mathf.Max(0f, v);
				case "spawndistancemin": return Mathf.Max(20f, v);
				case "spawndistancemax": return Mathf.Max(20f, v);
				case "unloaddistance": return Mathf.Max(300f, v);
				case "returnminutes": return Mathf.Max(0f, v);
				case "regrowdays": return Mathf.Max(0, Mathf.RoundToInt(v));
				case "showonreceiver": return v != 0f ? 1f : 0f;
				case "receiverdistance": return Mathf.Max(0f, v);
				case "generated": return Mathf.Max(0f, v);
				case "generatedflyingchance": return Mathf.Clamp01(v);
				case "generatedgather": return Mathf.Clamp01(v);
				case "generatedshallows": return Mathf.Clamp01(v);
				case "generatedshallowsdepth": return Mathf.Clamp(v, GenGather.MinDepth, GenGather.MaxDepth);
				default: return null;
			}
		}

		/// <summary>The number settings of spawnpool.txt, as the file spells them (ROADMAP AU46: the Defaults window).</summary>
		public static readonly string[] NumberKeys = { "chancePerKm", "quietMinutes", "minSpacing", "spawnDistanceMin", "spawnDistanceMax", "unloadDistance",
			"returnMinutes", "regrowDays", "showOnReceiver", "receiverDistance", "generated", "generatedFlyingChance",
			"generatedGather", "generatedShallows", "generatedShallowsDepth" };

		/// <summary>A number setting's value on this machine now (by its key, any case); NaN if there is none.</summary>
		public static float ValueOf(string key)
		{
			switch (key.ToLowerInvariant())
			{
				case "chanceperkm": return ChancePerKm;
				case "quietminutes": return QuietMinutes;
				case "minspacing": return MinSpacing;
				case "spawndistancemin": return SpawnDistanceMin;
				case "spawndistancemax": return SpawnDistanceMax;
				case "unloaddistance": return UnloadDistance;
				case "returnminutes": return ReturningIslands.ReturnMinutes;
				case "regrowdays": return RegrowDays;
				case "showonreceiver": return ShowOnReceiver ? 1f : 0f;
				case "receiverdistance": return ReceiverDistance;
				case "generated": return GeneratedWeight;
				case "generatedflyingchance": return GeneratedFlyingChance;
				case "generatedgather": return GeneratedGather;
				case "generatedshallows": return GeneratedShallows;
				case "generatedshallowsdepth": return GeneratedShallowsDepth;
				default: return float.NaN;
			}
		}

		/// <summary>A number setting's value in the mod's own spawnpool.txt (the file a new install gets); NaN if there is none.</summary>
		public static float DefaultOf(string key)
		{
			foreach (string raw in DefaultPool.Split('\n'))
			{
				string line = raw.Trim();
				int eq = line.IndexOf('=');
				if (line.StartsWith("#") || eq < 1 || !line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
				float v;
				if (float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
			}
			return float.NaN;
		}

		/// <summary>
		/// Changes settings of spawnpool.txt (ROADMAP AU46 - the Defaults window and the world window, instead of editing the
		/// file by hand): key -> value, numbers (NumberKeys), generatedStyles (style names) or defaultPlan. Every value is
		/// checked and kept within its range first (the clamps LoadPool applies; spawnDistanceMax never below
		/// spawnDistanceMin), so a bad one changes nothing; then the file is written once, whole (SafeFile), its comments and
		/// island lines as they were: a setting's line is changed where it is, one the file hasn't gets a line after its last
		/// setting. The file is read again at once - and the host's players get the settings they share (OnPoolChanged).
		/// Returns the values as written. Throws ArgumentException for a bad value (nothing written).
		/// </summary>
		public static Dictionary<string, string> SetPoolValues(IDictionary<string, string> values)
		{
			LoadPool(false); // (makes the file if it isn't there)
			var write = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in values)
			{
				string key = kv.Key.Trim(), value = (kv.Value ?? "").Trim();
				if (key.Equals("generatedStyles", StringComparison.OrdinalIgnoreCase))
				{
					string[] names = value.Split(',', ' ').Where(x => x.Trim().Length > 0).Select(x => x.Trim()).ToArray();
					if (names.Length == 0 || names.Any(n => !TerrainPainter.Styles.Any(st => st.Name.Equals(n, StringComparison.OrdinalIgnoreCase))))
						throw new ArgumentException("generatedStyles: one or more of " + string.Join(", ", TerrainPainter.Styles.Select(st => st.Name).ToArray()));
					write["generatedStyles"] = string.Join(", ", names);
					continue;
				}
				if (key.Equals("generatedGatherOff", StringComparison.OrdinalIgnoreCase))
				{
					// (kinds of things to gather switched off; nothing = all on)
					string[] keys = value.Split(',', ' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
					string bad = keys.FirstOrDefault(k => !GenGather.LandKeys.Concat(GenGather.SeaKeys).Contains(k.ToLowerInvariant()));
					if (bad != null) throw new ArgumentException("generatedGatherOff: '" + bad + "' is none of " + string.Join(", ", GenGather.LandKeys.Concat(GenGather.SeaKeys).ToArray()));
					write["generatedGatherOff"] = GenGather.Clean(string.Join(",", keys));
					continue;
				}
				if (key.Equals("defaultPlan", StringComparison.OrdinalIgnoreCase))
				{
					if (value.Length == 0) throw new ArgumentException("defaultPlan: a plan's name");
					write["defaultPlan"] = value;
					continue;
				}
				string spelt = NumberKeys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
				if (spelt == null) throw new ArgumentException("'" + key + "' is not a setting of " + PoolFileName);
				float v;
				if (!ParseNumber(value, out v) || float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException(spelt + ": a number, not '" + value + "'");
				write[spelt] = FormatValue(Clamped(spelt.ToLowerInvariant(), v).Value);
			}
			// (the far end of where islands appear never nearer than the near end, as LoadPool keeps it)
			if (write.ContainsKey("spawnDistanceMin") || write.ContainsKey("spawnDistanceMax"))
			{
				string s;
				float min = write.TryGetValue("spawnDistanceMin", out s) ? float.Parse(s, CultureInfo.InvariantCulture) : SpawnDistanceMin;
				float max = write.TryGetValue("spawnDistanceMax", out s) ? float.Parse(s, CultureInfo.InvariantCulture) : SpawnDistanceMax;
				if (max < min) write["spawnDistanceMax"] = FormatValue(min);
			}
			if (write.Count == 0) return write;

			List<string> lines = File.Exists(PoolPath) ? File.ReadAllLines(PoolPath).ToList() : new List<string>();
			var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			int lastSetting = -1;
			for (int i = 0; i < lines.Count; i++)
			{
				string l = lines[i].Trim();
				int eq = l.IndexOf('=');
				if (l.Length == 0 || l.StartsWith("#") || eq < 1) continue;
				lastSetting = i;
				string k = l.Substring(0, eq).Trim(), to;
				if (!write.TryGetValue(k, out to)) continue;
				lines[i] = k + " = " + to; // (every line of it: the file's last one counts when it is read)
				done.Add(k);
			}
			lines.InsertRange(lastSetting + 1, write.Where(kv => !done.Contains(kv.Key)).Select(kv => kv.Key + " = " + kv.Value));
			SafeFile.WriteAllLines(PoolPath, lines.ToArray());
			Debug.Log("[CUSTOM ISLANDS] " + PoolFileName + ": " + string.Join(", ", write.Select(kv => kv.Key + " = " + kv.Value).ToArray()));
			LoadPool(true);
			return write;
		}

		/// <summary>A number as spawnpool.txt has it (no more than three decimals, a point).</summary>
		public static string FormatValue(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

		/// <summary>Dev tests (CISpawnPoolSet): numbers that replace this machine's spawnpool.txt ones, kept when the file is read
		/// again (each world load) - to give a player who joins other settings than the host's.</summary>
		internal static readonly Dictionary<string, float> TestOverrides = new Dictionary<string, float>();

		/// <summary>A number from the settings file: "0.5", or "0,5" as a PC set to a decimal comma writes it.</summary>
		internal static bool ParseNumber(string s, out float v)
		{
			if (s.IndexOf('.') < 0 && s.Count(c => c == ',') == 1) s = s.Replace(',', '.');
			return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
		}

		static void BadLine(string line) { Debug.LogWarning("[CUSTOM ISLANDS] Ignoring unknown line in " + PoolFileName + ": " + line); }

		public static string Describe()
		{
			LoadPool(true);
			var pool = Pool();
			float total = pool.Sum(p => p.Value);
			var lines = new List<string>
			{
				"Automatic islands in this world: " + (Enabled ? "on" : "off") + " (CustomIslandsAuto on|off)",
				"How often: " + WorldIslands.DescribeGap() + (ChancePerKm <= 0f ? " - off (chancePerKm 0)" : ""),
				"This world's play time: " + (WorldIslands.PlaySeconds >= WorldIslands.LongPlayed ? "long (an older world)" : (WorldIslands.PlaySeconds / 60f).ToString("F1") + " min") + "; none before " + QuietMinutes.ToString("0.#") + " min",
				string.Format(CultureInfo.InvariantCulture, "Min spacing {0:F0} m, appear {1:F0}-{2:F0} m ahead, unload beyond {3:F0} m, regrow after {4} day(s)",
					MinSpacing, SpawnDistanceMin, SpawnDistanceMax, UnloadDistance, RegrowDays > 0 ? RegrowDays.ToString() : "never"),
				"Sailed since the last automatic island: " + sailedSinceSpawn.ToString("F0") + " m",
				"This world's choice (New Game box, WorldIslands): " + WorldIslands.Describe(),
				"Pool (" + PoolPath + "): " + (pool.Count == 0 ? "empty" : "")
			};
			foreach (var p in pool)
				lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0}: weight {1}, {2:P0} of spawns{3}", p.Key == GeneratedEntry ? "a new generated island" : p.Key, p.Value, p.Value / total,
					p.Key != GeneratedEntry && InWorld(p.Key) != null ? " (in this world already: a quarter of that)" : ""));
			if (GeneratedWeight > 0f)
				lines.Add("Generated islands: " + string.Join(", ", GeneratedStyles.Select(TerrainPainter.StyleName).ToArray()) + ", " +
					GeneratedFlyingChance.ToString("P0", CultureInfo.InvariantCulture) + " of them flying" +
					(GeneratedGather + GeneratedShallows > 0f ? string.Format(CultureInfo.InvariantCulture, ", things to gather {0:0.##} on the land and {1:0.##} in the shallows (down to {2:0} m){3}",
						GeneratedGather, GeneratedShallows, GeneratedShallowsDepth, GeneratedGatherOff.Length > 0 ? ", off: " + GeneratedGatherOff : "") : ""));
			lines.Add("Custom islands on the Receiver: " + (ShowOnReceiver ? "shown" : "hidden"));
			// (a player in someone else's game: only the host places islands, and these come from the host's file)
			if (WorldRules.HostSettingsFromHost) lines.Add("In this game (the host's settings): " + WorldRules.HostSettingsDescribe());
			return string.Join("\n", lines.ToArray());
		}

		#endregion

		static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0, v.z); }
	}
}
