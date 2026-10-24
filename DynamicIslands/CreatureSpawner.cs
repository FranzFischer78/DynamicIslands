using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace DynamicIslands.Editor
{
	/// <summary>Where a creature of an island lives in a world: its type, its settings and the live animals from it.</summary>
	public class CreatureSpawnPoint : MonoBehaviour
	{
		public ContentCatalog.CreatureKind Kind;
		public Dictionary<string, string> Props = new Dictionary<string, string>();
		/// <summary>Place among the island's creatures (file order: the same on every machine).</summary>
		public int Ordinal;
		/// <summary>Host: the animals spawned here that are still wild (not caught), dead ones included until removed.</summary>
		public readonly List<AI_NetworkBehaviour> Spawned = new List<AI_NetworkBehaviour>();
		/// <summary>Host: how many are alive and wild, as last recorded in the island's state; -1 = not spawned, -2 = being spawned.</summary>
		public int RecordedAlive = -1;
		public const int Spawning = -2;
		/// <summary>Host: where each animal still alive was at the last look (by instance id; it may be destroyed since).</summary>
		public readonly Dictionary<int, Vector3> LastAt = new Dictionary<int, Vector3>();
		/// <summary>Host: animals Raft took away far from every player - not killed: the island's state keeps them.</summary>
		public int Strayed;
		/// <summary>Host: the creatures a boss phase called (BossPhases): not counted for the spot's "defeat" or its state.</summary>
		public readonly List<AI_NetworkBehaviour> Summoned = new List<AI_NetworkBehaviour>();

		public static CreatureSpawnPoint Create(Transform parent, IslandObject o, int ordinal)
		{
			var go = new GameObject("CreatureSpawn_" + ordinal + "_" + o.Name);
			go.transform.SetParent(parent, false);
			go.transform.position = parent.position + o.Position;
			go.transform.rotation = Quaternion.Euler(o.EulerRotation);
			var p = go.AddComponent<CreatureSpawnPoint>();
			p.Kind = ContentCatalog.CreatureOf(o.Name);
			p.Props = o.Props != null ? new Dictionary<string, string>(o.Props) : new Dictionary<string, string>();
			p.Ordinal = ordinal;
			return p;
		}

		/// <summary>Key of this spawn point in the island's object state (above the 16 bits pickups use).</summary>
		public int StateKey { get { return CreatureSpawner.StateKeyBase + Ordinal; } }
	}

	/// <summary>
	/// Brings the creatures of custom islands to life in a world. The host spawns Raft's real, networked animals at
	/// the island's creature spawn points through Raft's own Network_Host_Entities (clients get them from Raft's
	/// networking, like any animal), with the builder's health, damage, speed, size and colour.
	///
	/// How they fit into Raft:
	///   - Each animal is tied to a switched-off LandmarkEntitySpawner at its spawn point, like the animals of Raft's own
	///     islands: it roams around that point, Raft does not put it in the world save (the island brings it back),
	///     and a domestic animal that is caught and carried off is let go by the spawner and becomes a normal raft animal.
	///   - Raft's animals walk on a NavMesh. Raft's islands come with one baked in; ours is built when the island loads
	///     (NavMeshSurface, from the island's terrain and objects) before any land animal is spawned.
	///   - When the island unloads (far away) its animals are removed the way Raft removes a landmark's animals.
	///   - Killed and caught animals are remembered per spawn point in the island's state (saved with the world);
	///     they come back after CustomIslandSpawner.RegrowDays in-game days unless the builder turned respawning off.
	/// </summary>
	public static class CreatureSpawner
	{
		public const int StateKeyBase = 0x10000;
		/// <summary>Farthest a spawned animal is placed from its spawn point when several share it (m).</summary>
		const float HerdRadius = 3f;
		/// <summary>An animal that vanishes alive farther than this from every player was taken away by Raft, not killed (m).</summary>
		const float StrayDistance = 150f;

		/// <summary>Animals spawned by custom islands that are still tied to their spawn point.</summary>
		static readonly HashSet<AI_NetworkBehaviour> ours = new HashSet<AI_NetworkBehaviour>();
		/// <summary>Host: our animals a hit left dead (instance ids), until the next look: one Raft removes at once (a
		/// stingray) was killed, not taken away (The Frontier's stingray counted as taken away, 2026-10-12).</summary>
		static readonly HashSet<int> felled = new HashSet<int>();
		/// <summary>Speed multiplier per animal's movement component (read by the movement patches).</summary>
		static readonly Dictionary<AI_Movement, float> speed = new Dictionary<AI_Movement, float>();
		static float nextTick;

		public static bool IsOurs(AI_NetworkBehaviour ai) { return ai != null && ours.Contains(ai); }

		/// <summary>A player: the host says which spot each island animal belongs to (since 2026-10-06); else, as before,
		/// the nearest spot of its kind and size is guessed.</summary>
		public static bool HostSendsSpots { get; internal set; }

		// A player: island and spot of each island animal, by its object index (AU32: Raft's shark or a screecher near an
		// island was taken for one of its animals; AU62: a plain animal next to a Boss spot got the Boss's tint and health)
		static readonly Dictionary<uint, KeyValuePair<int, int>> spotOf = new Dictionary<uint, KeyValuePair<int, int>>();

		/// <summary>Host: the spots message for one animal, or for all of them (null).</summary>
		internal static IslandNetMessage SpotsMessage(AI_NetworkBehaviour only)
		{
			var parts = new List<string>();
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) continue;
				foreach (CreatureSpawnPoint p in e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true))
					foreach (AI_NetworkBehaviour ai in p.Spawned)
						if (ai != null && (only == null || ai == only))
							parts.Add(ai.ObjectIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + e.Id + ":" + p.Ordinal);
			}
			if (only != null && parts.Count == 0) return null;
			return new IslandNetMessage { Kind = IslandNetMessage.CreatureSpots, Data = string.Join(";", parts.ToArray()) };
		}

		/// <summary>A player: the host's spots for its island animals.</summary>
		internal static void OnSpots(string data)
		{
			foreach (string part in (data ?? "").Split(';'))
			{
				string[] f = part.Split(':');
				uint idx; int island, spot;
				if (f.Length == 3 && uint.TryParse(f[0], out idx) && int.TryParse(f[1], out island) && int.TryParse(f[2], out spot))
					spotOf[idx] = new KeyValuePair<int, int>(island, spot);
			}
			if (spotOf.Count > 4096)
			{
				// (animals long gone: only those still in the world are kept)
				var live = new HashSet<uint>(UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null).Select(a => a.ObjectIndex));
				foreach (uint gone in spotOf.Keys.Where(k => !live.Contains(k)).ToList()) spotOf.Remove(gone);
			}
		}

		/// <summary>A player: the loaded spot the host said this animal belongs to (null: not known, or its island isn't loaded).</summary>
		static CreatureSpawnPoint SentSpot(AI_NetworkBehaviour ai)
		{
			KeyValuePair<int, int> s;
			if (ai == null || !spotOf.TryGetValue(ai.ObjectIndex, out s)) return null;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == s.Key);
			if (e == null || e.Root == null) return null;
			return e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).FirstOrDefault(p => p.Ordinal == s.Value);
		}

		/// <summary>The spot an animal of a custom island belongs to, on any machine (the host: its own lists; a player: what
		/// the host said, else the nearest loaded spot of its kind within 150 m) - null for Raft's own animals.</summary>
		public static CreatureSpawnPoint SpotOf(AI_NetworkBehaviour ai)
		{
			if (ai == null) return null;
			if (Raft_Network.IsHost)
			{
				if (!IsOurs(ai)) return null;
				foreach (GameObject r in IslandSpawner.SpawnedRoots)
				{
					if (r == null) continue;
					foreach (CreatureSpawnPoint p in SpotsOf(r)) if (p != null && (p.Spawned.Contains(ai) || p.Summoned.Contains(ai))) return p;
				}
				return null;
			}
			CreatureSpawnPoint sent = SentSpot(ai);
			if (sent != null || HostSendsSpots || ai.connectedSpawner != null) return sent;
			CreatureSpawnPoint best = null;
			float bestD = 150f * 150f;
			foreach (GameObject r in IslandSpawner.SpawnedRoots)
			{
				if (r == null) continue;
				foreach (CreatureSpawnPoint p in SpotsOf(r))
				{
					if (p == null || p.Kind == null || p.Kind.Type != ai.behaviourType) continue;
					float d = (p.transform.position - ai.transform.position).sqrMagnitude;
					if (d < bestD) { bestD = d; best = p; }
				}
			}
			return best;
		}

		/// <summary>
		/// Whether an animal belongs to a custom island (it has the builder's looks): the host knows its own; a client is
		/// told by the host (or, with an older host, goes by a loaded spawn point of its kind near it - Raft's own islands'
		/// animals come tied to their island's spawner).
		/// </summary>
		public static bool IsOnCustomIsland(AI_NetworkBehaviour ai)
		{
			if (ai == null) return false;
			if (Raft_Network.IsHost) return IsOurs(ai);
			if (HostSendsSpots) return spotOf.ContainsKey(ai.ObjectIndex);
			if (ai.connectedSpawner != null) return false;
			foreach (GameObject r in IslandSpawner.SpawnedRoots)
			{
				if (r == null) continue;
				foreach (CreatureSpawnPoint p in SpotsOf(r))
					if (p != null && p.Kind != null && p.Kind.Type == ai.behaviourType && (p.transform.position - ai.transform.position).sqrMagnitude < 150f * 150f) return true;
			}
			return false;
		}

		public static int LiveCount { get { return ours.Count(a => a != null); } }

		static Network_Host_Entities HostEntities { get { try { return ComponentManager<Network_Host_Entities>.Value; } catch { return null; } } }

		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		/// <summary>A world finished loading: nothing of the last world is ours any more.</summary>
		public static void OnWorldLoaded()
		{
			ours.Clear();
			speed.Clear();
			HealthBefore.Clear();
			clientTinted.Clear();
			spotOf.Clear();
			clientHealth.Clear();
			spawning.Clear();
			// (not sentHealth: a player who joins gets the host's animals before this runs - its entries go by age)
			Network_Host_Entities h = HostEntities;
			if (h != null) ContentCatalog.CacheModels(h.AINetworkBehaviourPrefabs);
		}

		#region Spawning (host)

		/// <summary>Host: an island was spawned or reloaded; brings its creatures once the ground is ready for them.</summary>
		public static void OnIslandReady(IslandWorldState.Entry entry)
		{
			if (entry == null || entry.Root == null || !Raft_Network.IsHost) return;
			if (entry.Root.GetComponentInChildren<CreatureSpawnPoint>(true) == null) return;
			DynamicIslands.instance.StartCoroutine(OneAtATime(entry, entry.Root));
		}

		/// <summary>Islands with a spawn run going (host).</summary>
		static readonly HashSet<GameObject> spawning = new HashSet<GameObject>();

		/// <summary>One spawn run per island at a time: a second one (a zone fired while the first built the NavMesh) saw the
		/// NavMesh's surface there already and spawned its land animals before the NavMesh was built. (Not waited for longer
		/// than a run can take: a run that stopped half way doesn't hold the island for ever.)</summary>
		static IEnumerator OneAtATime(IslandWorldState.Entry entry, GameObject root)
		{
			for (float until = Time.realtimeSinceStartup + 180f; root != null && spawning.Contains(root) && Time.realtimeSinceStartup < until; ) yield return null;
			if (root == null) yield break;
			spawning.RemoveWhere(r => r == null);
			spawning.Add(root);
			try { yield return SpawnRoutine(entry, root); }
			finally { spawning.Remove(root); }
		}

		static IEnumerator SpawnRoutine(IslandWorldState.Entry entry, GameObject root)
		{
			Network_Host_Entities host = null;
			for (float t = 0; t < 30f && (host = HostEntities) == null; t += 0.5f) yield return new WaitForSeconds(0.5f);
			if (host == null) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's creature manager is missing: no creatures on '" + entry.HostName + "'"); yield break; }
			ContentCatalog.CacheModels(host.AINetworkBehaviourPrefabs);

			// Spawn points not handled yet (this runs again when a zone fires), except those still waiting for their zone
			List<CreatureSpawnPoint> points = root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null && p.RecordedAlive == -1 && !WaitsForZone(entry, root, p)).ToList();
			var wanted = new Dictionary<CreatureSpawnPoint, int>();
			foreach (CreatureSpawnPoint p in points)
			{
				int n = HowManyNow(entry, p);
				// (as Raft's own islands: in a game mode without screechers or puffer fish there are none here either)
				if (n > 0 && !SpawnsInThisMode(p.Kind.Type)) { Debug.Log("[CUSTOM ISLANDS] '" + entry.HostName + "': no " + p.Kind.Label + " in this game mode (as on Raft's islands)"); n = 0; }
				if (n > 0) wanted[p] = n;
				// (counted only once they exist: building the NavMesh first takes a while, and the watcher must not take
				// the animals it doesn't see yet for defeated ones)
				p.RecordedAlive = n > 0 ? CreatureSpawnPoint.Spawning : 0;
			}
			if (wanted.Count == 0) { if (points.Count > 0) Debug.Log("[CUSTOM ISLANDS] '" + entry.HostName + "': " + points.Count + " creature spot(s) to fill, none has animals left"); yield break; }

			// Land animals need a NavMesh for each kind of agent Raft gives them
			var agentTypes = new HashSet<int>();
			foreach (CreatureSpawnPoint p in wanted.Keys)
			{
				AI_NetworkBehaviour prefab = Prefab(host, p.Kind.Type);
				NavMeshAgent agent = prefab != null ? prefab.GetComponentInChildren<NavMeshAgent>(true) : null;
				if (agent != null) agentTypes.Add(agent.agentTypeID);
			}
			// (built already for animals spawned earlier)
			agentTypes.RemoveWhere(t => root.GetComponents<NavMeshSurface>().Any(s => s.agentTypeID == t));
			// The randomizer's extras on one of Raft's islands: their animals walk on Raft's island. Its own NavMesh is used
			// where it has one for them (Raft's big islands), otherwise one is built from Raft's island's ground.
			Landmark under = WorldRandomizer.IsExtras(entry) ? WorldRandomizer.IslandAt(entry.Position) : null;
			// (not with a cave in them: Raft's NavMesh doesn't know its walls)
			if (under != null && !root.GetComponentsInChildren<Transform>(true).Any(t => RaftProps.Get(t.name) != null && RaftProps.Get(t.name).IsCave))
				agentTypes.RemoveWhere(t => wanted.Keys.Where(p => { AI_NetworkBehaviour pf = Prefab(host, p.Kind.Type); NavMeshAgent a = pf != null ? pf.GetComponentInChildren<NavMeshAgent>(true) : null; return a != null && a.agentTypeID == t; })
					.All(p => { NavMeshHit h; return NavMesh.SamplePosition(p.transform.position, out h, 4f, new NavMeshQueryFilter { agentTypeID = t, areaMask = NavMesh.AllAreas }); }));
			if (agentTypes.Count > 0)
			{
				float started = Time.realtimeSinceStartup;
				yield return BuildNavMesh(root, agentTypes, under);
				if (root == null) yield break;
				List<CreatureSpawnPoint> walkers = wanted.Keys.Where(p => { AI_NetworkBehaviour pf = Prefab(host, p.Kind.Type); return pf != null && pf.GetComponentInChildren<NavMeshAgent>(true) != null; }).ToList();
				NavMeshHit probe;
				if (walkers.Count > 0 && !walkers.Any(p => NavMesh.SamplePosition(p.transform.position, out probe, 8f, NavMesh.AllAreas)))
				{
					Debug.LogWarning("[CUSTOM ISLANDS] '" + entry.HostName + "': no NavMesh at the animals' spots after building it; building it again");
					foreach (NavMeshSurface s in root.GetComponents<NavMeshSurface>()) UnityEngine.Object.Destroy(s);
					yield return null;
					if (root == null) yield break;
					yield return BuildNavMesh(root, agentTypes, under);
					if (root == null) yield break;
				}
				Debug.Log("[CUSTOM ISLANDS] '" + entry.HostName + "': NavMesh for " + agentTypes.Count + " kind(s) of animal built in " + (Time.realtimeSinceStartup - started).ToString("F1") + " s");
			}
			if (root == null || entry.Root != root || !Raft_Network.IsHost) yield break;

			int spawned = 0;
			foreach (var w in wanted)
			{
				int here = 0;
				for (int i = 0; i < w.Value; i++)
				{
					AI_NetworkBehaviour ai = Spawn(host, w.Key, i, w.Value);
					if (ai == null) continue;
					here++;
					// (a boss whose fight was in its second phase when the world was saved: back in it - RF5)
					BossPhases.Resume(entry, w.Key, ai);
				}
				w.Key.RecordedAlive = here;
				w.Key.Strayed = 0;
				spawned += here;
			}
			Debug.Log("[CUSTOM ISLANDS] '" + entry.HostName + "': " + spawned + " creature(s) spawned at " + wanted.Count + " spawn point(s)");
		}

		/// <summary>
		/// Whether Raft spawns this kind in the current game mode: its landmark spawners leave screechers and puffer fish out
		/// when the mode says so (LandmarkEntitySpawner.ShouldEntityBeSpawnedInGamemode); every other kind always spawns
		/// (the mode changes how it behaves). The host's mode counts: only the host spawns.
		/// </summary>
		public static bool SpawnsInThisMode(AI_NetworkBehaviourType type)
		{
			try { return SpawnsInMode(type, GameModeValueManager.GetCurrentGameModeValue()); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reading the game mode: " + e.Message); }
			return true;
		}

		/// <summary>The same for a given game mode (CIModeParity compares it with Raft's own rule for every mode).</summary>
		public static bool SpawnsInMode(AI_NetworkBehaviourType type, SO_GameModeValue mode)
		{
			if (mode == null) return true;
			if (type == AI_NetworkBehaviourType.StoneBird) return mode.stonebirdVariables == null || mode.stonebirdVariables.shouldSpawn;
			if (type == AI_NetworkBehaviourType.PufferFish) return mode.pufferfishVariables == null || mode.pufferfishVariables.shouldSpawn;
			return true;
		}

		/// <summary>The game modes a kind doesn't come in (Raft's rule, as SpawnsInMode): Check tells builders - a screecher
		/// step can't be done in a Creative world. Raft's own values once its modes are set up, else what they are today.</summary>
		public static List<string> ModesWithout(AI_NetworkBehaviourType type)
		{
			var off = new List<string>();
			try
			{
				var modes = typeof(GameModeValueManager).GetField("gameModeValues", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as SO_GameModeValue[];
				if (modes != null && modes.Any(m => m != null))
				{
					foreach (SO_GameModeValue m in modes.Where(m => m != null))
						if (!SpawnsInMode(type, m) && !off.Contains(m.gameMode.ToString())) off.Add(m.gameMode.ToString());
					return off;
				}
			}
			catch (Exception) { } // (not set up yet: the main menu before any world)
			if (type == AI_NetworkBehaviourType.StoneBird || type == AI_NetworkBehaviourType.PufferFish) off.Add(GameMode.Creative.ToString());
			return off;
		}

		/// <summary>An ambush: the creature waits until a player sets off its trigger zone (a zone that isn't on the island doesn't hold it back).</summary>
		static bool WaitsForZone(IslandWorldState.Entry entry, GameObject root, CreatureSpawnPoint p)
		{
			if (!p.gameObject.activeSelf) return true; // hidden until an action shows it (Behaviours)
			string id = ObjectProps.Get(p.Props, ObjectProps.CreatureZone);
			if (id.Length == 0) return false;
			TriggerZone zone = root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == id);
			return zone != null && !ContentState.IsUsed(entry, zone.StateKey);
		}

		/// <summary>How many animals a spawn point should have now: all of them, or what is left of them unless they have grown back.</summary>
		static int HowManyNow(IslandWorldState.Entry entry, CreatureSpawnPoint p)
		{
			int count = ObjectProps.Count(p.Props);
			ObjectState s;
			if (!entry.State.TryGetValue(p.StateKey, out s)) return count;
			int days = IslandRules.RegrowDays(entry);
			if (ObjectProps.Respawns(p.Props) && days > 0 && Today - s.Day >= days)
			{
				entry.State.Remove(p.StateKey);
				return count;
			}
			return Mathf.Clamp(s.Yield, 0, count);
		}

		static AI_NetworkBehaviour Prefab(Network_Host_Entities host, AI_NetworkBehaviourType type)
		{
			AI_NetworkBehaviour[] list = host.AINetworkBehaviourPrefabs;
			return list != null ? list.FirstOrDefault(b => b != null && b.behaviourType == type) : null;
		}

		/// <summary>Spawns one live animal at a spawn point (index i of n: a herd stands in a ring around the point).</summary>
		public static AI_NetworkBehaviour Spawn(Network_Host_Entities host, CreatureSpawnPoint p, int i, int n)
		{
			return Spawn(host, p, p.Kind, p.transform.position, i, n, p.Props, false);
		}

		/// <summary>Spawn, of a kind around a centre with these settings; summon: one a boss phase called (p.Summoned).</summary>
		static AI_NetworkBehaviour Spawn(Network_Host_Entities host, CreatureSpawnPoint p, ContentCatalog.CreatureKind kind, Vector3 centre, int i, int n, IDictionary<string, string> props, bool summon)
		{
			try
			{
				AI_NetworkBehaviour prefab = Prefab(host, kind.Type);
				if (prefab == null) { Debug.LogWarning("[CUSTOM ISLANDS] Raft has no " + kind.Type + " to spawn"); return null; }

				Vector3 pos = centre;
				if (n > 1 || summon)
				{
					float a = i * Mathf.PI * 2f / Mathf.Max(1, n), r = HerdRadius * Mathf.Max(1f, ObjectProps.Size(p.Props)) * (summon ? 1.5f : 1f);
					pos += new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
				}
				if (prefab.GetComponentInChildren<NavMeshAgent>(true) != null)
				{
					NavMeshHit hit;
					if (NavMesh.SamplePosition(pos, out hit, 8f, NavMesh.AllAreas)) pos = hit.position;
					else if (NavMesh.SamplePosition(centre, out hit, 8f, NavMesh.AllAreas)) pos = hit.position;
					else if (NavMesh.SamplePosition(p.transform.position, out hit, 8f, NavMesh.AllAreas)) pos = hit.position;
					else Debug.LogWarning("[CUSTOM ISLANDS] No walkable ground near the " + kind.Label + " spawn point at " + p.transform.position);
				}

				// The animal's home: a switched-off spawner at its spawn point (see the class comment)
				var home = new GameObject((summon ? "Summoned_" : "Home_") + i);
				home.SetActive(false);
				home.transform.SetParent(p.transform, false);
				home.transform.position = pos;
				home.transform.rotation = p.transform.rotation;
				LandmarkEntitySpawner spawner = home.AddComponent<LandmarkEntitySpawner>();
				spawner.entityType = kind.Type;
				spawner.setStartRotation = true;
				// (a sea animal that swims rounds gets rounds of its own around its spot: see Rounds)
				WaypointHandler rounds = prefab.GetComponentInChildren<AI_State_Waypoint_Circulation>(true) != null ? Rounds(home.transform, p, pos) : null;
				if (rounds != null) Traverse.Create(spawner).Field("waypointHandler").SetValue(rounds);

				float scale = -1f;
				try { scale = prefab.localScaleInterval.GetRandomValue(); } catch { }
				if (scale <= 0f) scale = 1f;
				scale *= ObjectProps.Size(props);

				AI_NetworkBehaviour ai = host.CreateAINetworkBehaviour(kind.Type, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), spawner);
				if (ai == null) return null;
				spawner.spawnedEntityBehaviour = ai;
				ours.Add(ai);
				if (summon) p.Summoned.Add(ai); else p.Spawned.Add(ai);
				if (rounds != null) KeepRounds(ai, rounds);
				ApplyStats(ai, props);
				ObjectProps.ApplyTint(ai.gameObject, props);

				// Tell the other players, as Raft's own CreateAINetworkBehaviour does (without the spawner: it isn't a landmark's)
				Raft_Network network = ComponentManager<Raft_Network>.Value;
				if (network != null)
				{
					// (which island spot it is first - AU32/AU62: the two go on different channels, and an animal whose spot hadn't
					// come yet was taken for one of Raft's by the players' randomizer)
					IslandNetMessage spots = SpotsMessage(ai);
					if (spots != null) IslandNetwork.SendToEveryone(spots);
					var msg = new Message_CreateAINetworkBehaviour(Messages.CreateAINetworkBehaviour, network.NetworkIDManager, host.ObjectIndex, pos, ai, null);
					network.RPC(msg, Target.Other, Steamworks.EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
				}
				return ai;
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Spawning a " + (kind != null ? kind.Label : "creature") + " failed: " + e);
				return null;
			}
		}

		/// <summary>Host: a boss phase calls n creatures of a kind around the boss (Raft's own stats, no tint); returns how many came.</summary>
		internal static int Summon(IslandWorldState.Entry e, CreatureSpawnPoint p, ContentCatalog.CreatureKind kind, int n, Vector3 around)
		{
			Network_Host_Entities host = HostEntities;
			if (host == null || kind == null || n <= 0) return 0;
			if (!SpawnsInThisMode(kind.Type)) { Debug.Log("[CUSTOM ISLANDS] '" + e.HostName + "': no " + kind.Label + " in this game mode - the boss calls none"); return 0; }
			int came = 0;
			for (int i = 0; i < n; i++)
				if (Spawn(host, p, kind, around, i, n, new Dictionary<string, string>(), true) != null) came++;
			return came;
		}

		/// <summary>Host: the creatures a spot's boss phase called that are still about (the tests).</summary>
		public static int SummonedAlive(CreatureSpawnPoint p)
		{
			return p == null ? 0 : p.Summoned.Count(ai => ai != null && (ai.networkEntity == null || !ai.networkEntity.IsDead));
		}

		/// <summary>Host: multiplies an animal's speed (on top of the builder's: a boss phase).</summary>
		internal static void MultiplySpeed(AI_NetworkBehaviour ai, float multiplier)
		{
			if (ai == null) return;
			foreach (AI_Movement m in ai.GetComponentsInChildren<AI_Movement>(true)) speed[m] = Mathf.Clamp(SpeedOf(m) * multiplier, ObjectProps.MinMultiplier, 4f);
		}

		/// <summary>An animal's speed multiplier now (its first movement's; 1 = Raft's own) - the tests.</summary>
		public static float SpeedOf(AI_NetworkBehaviour ai)
		{
			AI_Movement m = ai != null ? ai.GetComponentInChildren<AI_Movement>(true) : null;
			return SpeedOf(m);
		}

		/// <summary>Waypoints in a ring around a sea animal's spot: as many, and how far out at most (m).</summary>
		const int RoundPoints = 6;
		const float RoundRadius = 8f;

		/// <summary>
		/// Raft's angler fish swim rounds (AI_State_Waypoint_Circulation) along waypoints a landmark has for them - those
		/// of the spawner they come from, or of a Raft island registered for them. A built island had neither: Raft
		/// logged "Unable to find proper waypoint manager", its state threw every frame (a NullReferenceException in
		/// UpdateState) and the fish hung in the water. Found with the Abyss's lair. Each such animal now gets a ring of
		/// waypoints around its spot, in open water: below the surface, clear of the ground and of objects - a point is
		/// pulled in towards the spot until it is (a trench's lair is narrow). Under the switched-off home the handler's
		/// own start-up (which would register it for Raft's islands) doesn't run.
		/// </summary>
		static WaypointHandler Rounds(Transform home, CreatureSpawnPoint p, Vector3 centre)
		{
			var go = new GameObject("CI_Rounds");
			go.transform.SetParent(home, false);
			IslandSettings island = p.GetComponentInParent<IslandSettings>();
			float sea = island != null ? island.transform.position.y + island.WaterLevel : 0f;
			centre.y = Mathf.Min(centre.y, sea - 1.5f);
			go.transform.position = centre;
			int mask = (int)LayerMasks.MASK_GroundMask_NonRaft | (int)LayerMasks.MASK_Obstruction | (1 << IslandSpawner.TerrainLayer);
			float radius = Mathf.Clamp(4f * ObjectProps.Size(p.Props), 4f, RoundRadius);
			var points = new List<Waypoint>();
			for (int i = 0; i < RoundPoints; i++)
			{
				float a = i * Mathf.PI * 2f / RoundPoints;
				Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), at = centre;
				for (float r = radius; r >= 1f; r -= 1f)
				{
					Vector3 q = centre + dir * r;
					if (Physics.Linecast(centre, q, mask, QueryTriggerInteraction.Ignore) || Physics.CheckSphere(q, 1f, mask, QueryTriggerInteraction.Ignore)) continue;
					at = q;
					break;
				}
				var w = new GameObject("Waypoint_" + i);
				w.transform.SetParent(go.transform, false);
				w.transform.position = at;
				points.Add(w.AddComponent<Waypoint>());
			}
			WaypointHandler handler = go.AddComponent<WaypointHandler>();
			// (a loop: Raft's state goes on to the next one, or the one before, when it reaches a point)
			for (int i = 0; i < points.Count; i++)
			{
				Traverse w = Traverse.Create(points[i]);
				w.Field("next").SetValue(points[(i + 1) % points.Count]);
				w.Field("previous").SetValue(points[(i + points.Count - 1) % points.Count]);
				w.Field("waypointHandler").SetValue(handler);
			}
			Traverse h = Traverse.Create(handler);
			h.Field("autoHandleRegistrationInAwake").SetValue(false);
			h.Field("waypoints").SetValue(points);
			h.Field("calculationWayPoints").SetValue(new List<Waypoint>(points));
			return handler;
		}

		/// <summary>
		/// The animal's swimming states use its spot's rounds (set before they start, so they don't look for a Raft
		/// island's), and don't switch to other rounds later: their list of rounds to switch between is emptied (Raft's
		/// own islands' rounds are far away, or not there).
		/// </summary>
		static void KeepRounds(AI_NetworkBehaviour ai, WaypointHandler rounds)
		{
			foreach (AI_State_Waypoint_Circulation s in ai.GetComponentsInChildren<AI_State_Waypoint_Circulation>(true))
			{
				Traverse t = Traverse.Create(s);
				t.Field("waypointHandler").SetValue(rounds);
				IList switches = t.Field("cirulationIDInterestTypes").GetValue() as IList;
				if (switches != null) switches.Clear();
			}
		}

		/// <summary>The rounds an animal's swimming state follows (null: none, or no such state) - for the tests.</summary>
		public static WaypointHandler RoundsOf(AI_NetworkBehaviour ai)
		{
			AI_State_Waypoint_Circulation s = ai != null ? ai.GetComponentInChildren<AI_State_Waypoint_Circulation>(true) : null;
			return s != null ? Traverse.Create(s).Field("waypointHandler").GetValue() as WaypointHandler : null;
		}

		/// <summary>The points of a ring of rounds (for the tests).</summary>
		public static List<Waypoint> PointsOf(WaypointHandler rounds)
		{
			return rounds != null ? (Traverse.Create(rounds).Field("waypoints").GetValue() as List<Waypoint>) ?? new List<Waypoint>() : new List<Waypoint>();
		}

		#endregion

		#region Stats

		/// <summary>Host: the builder's health, damage and speed for a freshly spawned animal (Raft's values times the multipliers).
		/// raftBites false: the damage it does to raft blocks stays Raft's (Big Bruce: the raft stays as in Raft - AU69).</summary>
		public static void ApplyStats(AI_NetworkBehaviour ai, IDictionary<string, string> props, bool raftBites = true)
		{
			float hp = ObjectProps.Health(props), dmg = ObjectProps.Damage(props), spd = ObjectProps.Speed(props);
			if (!Mathf.Approximately(hp, 1f)) DynamicIslands.instance.StartCoroutine(ApplyHealth(ai, hp));
			if (!Mathf.Approximately(dmg, 1f)) ScaleDamage(ai.gameObject, dmg, raftBites);
			if (!Mathf.Approximately(spd, 1f))
				foreach (AI_Movement m in ai.GetComponentsInChildren<AI_Movement>(true)) speed[m] = spd;
		}

		/// <summary>Raft's own maximum health of animals whose health was changed (the automated tests compare with it).</summary>
		public static readonly Dictionary<AI_NetworkBehaviour, float> HealthBefore = new Dictionary<AI_NetworkBehaviour, float>();

		/// <summary>Sets the health now and again after the animal's first frames (in case its own start-up sets it again).</summary>
		static IEnumerator ApplyHealth(AI_NetworkBehaviour ai, float multiplier)
		{
			float applied = -1f;
			for (int frame = 0; frame < 3; frame++)
			{
				if (ai == null || ai.networkEntity == null || ai.networkEntity.stat_health == null) yield break;
				Stat_Health h = ai.networkEntity.stat_health;
				if (!Mathf.Approximately(h.Max, applied) && h.Max > 0f)
				{
					HealthBefore[ai] = h.Max;
					applied = h.Max * multiplier;
					h.SetMaxValue(applied);
					h.SetToMaxValue();
				}
				yield return null;
			}
		}

		static readonly string[] NotDamage = { "taken", "threshold", "treshold", "range", "radius", "frequency", "time", "particle", "state", "reached", "sound", "box", "event", "cooldown", "delay" };

		/// <summary>
		/// Multiplies the damage an animal deals: Raft keeps it in the animal's own attack states and damage boxes
		/// (fields like attackDamage, chargeDamage, explosionDamage, damage). Only this animal's copies are changed.
		/// raftBites false: its attacks on raft blocks (the shark's AI_State_Attack_Block_Shark.attackBlockDamage) are left
		/// as they are. Returns the names of the fields that were scaled.
		/// </summary>
		public static List<string> ScaleDamage(GameObject root, float multiplier, bool raftBites = true)
		{
			var scaled = new List<string>();
			foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (mb == null) continue;
				Type t = mb.GetType();
				if (!(t.Name.StartsWith("AI_State") || typeof(DamageBox).IsAssignableFrom(t))) continue;
				if (!raftBites && t.Name.StartsWith("AI_State_Attack_Block")) continue;
				for (Type c = t; c != null && c != typeof(MonoBehaviour); c = c.BaseType)
					foreach (FieldInfo f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					{
						string n = f.Name.ToLowerInvariant();
						if (!n.Contains("damage") || NotDamage.Any(n.Contains)) continue;
						if (f.FieldType == typeof(float)) f.SetValue(mb, (float)f.GetValue(mb) * multiplier);
						else if (f.FieldType == typeof(int)) f.SetValue(mb, Mathf.RoundToInt((int)f.GetValue(mb) * multiplier));
						else continue;
						scaled.Add(t.Name + "." + f.Name);
					}
			}
			return scaled;
		}

		public static float SpeedOf(AI_Movement m)
		{
			float s;
			return m != null && speed.TryGetValue(m, out s) ? s : 1f;
		}

		#endregion

		#region NavMesh

		/// <summary>
		/// Builds a NavMesh per kind of agent from the island's colliders (terrain and objects), in the background.
		/// Raft's object meshes can't be read at runtime, so those colliders count as their bounding boxes. The data
		/// is registered through a NavMeshSurface on the island, which moves it along with Raft's world shifts.
		/// </summary>
		/// <summary>Raft's object meshes can't be read at runtime: those colliders count as their bounding boxes.</summary>
		static void UnreadableAsBoxes(List<NavMeshBuildSource> sources)
		{
			for (int i = 0; i < sources.Count; i++)
			{
				NavMeshBuildSource s = sources[i];
				Mesh mesh = s.shape == NavMeshBuildSourceShape.Mesh ? s.sourceObject as Mesh : null;
				if (mesh == null || mesh.isReadable) continue;
				s.shape = NavMeshBuildSourceShape.Box;
				s.size = mesh.bounds.size;
				s.transform = s.transform * Matrix4x4.Translate(mesh.bounds.center);
				s.sourceObject = null;
				sources[i] = s;
			}
		}

		/// <summary>
		/// Raft's cave pieces (RaftProps) as passages animals can walk in: their colliders would count as solid boxes
		/// (unreadable meshes) and put the animals on the roof, so each piece gives a floor along its passage and walls
		/// beside it instead.
		/// </summary>
		static void CaveFloors(List<NavMeshBuildSource> sources)
		{
			var caves = new HashSet<Transform>();
			for (int i = sources.Count - 1; i >= 0; i--)
			{
				Component c = sources[i].component;
				Transform cave = c != null ? CaveRoot(c.transform) : null;
				if (cave == null) continue;
				caves.Add(cave);
				sources.RemoveAt(i);
			}
			foreach (Transform t in caves)
			{
				PropInfo p = RaftProps.Get(t.name);
				bool alongX = p.Axis == 0;
				// The passage runs through the measured point inside it, from the closed end (or the outcrop's edge) to the other
				float centre = alongX ? p.Centre.x : p.Centre.z, half = (alongX ? p.Size.x : p.Size.z) / 2f, inside = alongX ? p.Inside.x : p.Inside.z;
				float lo = Mathf.Max(centre - half, inside - Mathf.Min(p.ToMinus, 2f * half)), hi = Mathf.Min(centre + half, inside + Mathf.Min(p.ToPlus, 2f * half));
				float len = Mathf.Max(1f, hi - lo), mid = (lo + hi) / 2f;
				Vector3 local = alongX ? new Vector3(mid, p.Floor - 0.15f, p.Inside.z) : new Vector3(p.Inside.x, p.Floor - 0.15f, mid);
				Quaternion rot = t.rotation;
				Vector3 axis = rot * (alongX ? Vector3.right : Vector3.forward), across = rot * (alongX ? Vector3.forward : Vector3.right);
				Vector3 floor = t.position + rot * local;
				Func<Vector3, Vector3, NavMeshBuildSource> box = (size, at) => new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = size, transform = Matrix4x4.TRS(at, rot, Vector3.one) };
				sources.Add(box(alongX ? new Vector3(len, 0.3f, p.Width) : new Vector3(p.Width, 0.3f, len), floor));
				foreach (int s in new[] { -1, 1 })
					sources.Add(box(alongX ? new Vector3(len, p.Headroom, 0.6f) : new Vector3(0.6f, p.Headroom, len), floor + across * s * (p.Width / 2f + 0.3f) + Vector3.up * (p.Headroom / 2f)));
				// The back wall of a den
				if (p.ToMinus < 90f) sources.Add(box(alongX ? new Vector3(0.6f, p.Headroom, p.Width) : new Vector3(p.Width, p.Headroom, 0.6f), floor - axis * (len / 2f + 0.3f) + Vector3.up * (p.Headroom / 2f)));
				if (p.ToPlus < 90f) sources.Add(box(alongX ? new Vector3(0.6f, p.Headroom, p.Width) : new Vector3(p.Width, p.Headroom, 0.6f), floor + axis * (len / 2f + 0.3f) + Vector3.up * (p.Headroom / 2f)));
			}
		}

		static Transform CaveRoot(Transform t)
		{
			for (; t != null; t = t.parent)
			{
				PropInfo p = RaftProps.Get(t.name);
				if (p != null) return p.IsCave ? t : null;
			}
			return null;
		}

		/// <summary>How deep below the sea's surface an animal's NavMesh reaches: the wet sand at the water's edge, no more.</summary>
		const float WadeDepth = 0.4f;

		static IEnumerator BuildNavMesh(GameObject root, IEnumerable<int> agentTypes, Landmark ground = null)
		{
			// The island was made this frame: let physics catch up with where its colliders were moved to
			yield return new WaitForFixedUpdate();
			if (root == null) yield break;
			var sources = new List<NavMeshBuildSource>();
			Bounds local = new Bounds();
			if (ground != null)
			{
				// Raft's island under the randomizer's extras: its ground and everything on it, over its land
				try
				{
					Physics.SyncTransforms();
					NavMeshBuilder.CollectSources(ground.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
					var onTop = new List<NavMeshBuildSource>();
					NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), onTop);
					sources.AddRange(onTop);
					UnreadableAsBoxes(sources);
					CaveFloors(sources);
					var land = new LandGround(ground);
					bool any = false;
					for (float x = -land.Radius; x <= land.Radius; x += 6f)
						for (float z = -land.Radius; z <= land.Radius; z += 6f)
						{
							Vector3 p, n;
							if (!land.Hit(land.Centre.x + x, land.Centre.z + z, out p, out n) || p.y < -4f) continue;
							if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
						}
					if (!any) { Debug.LogWarning("[CUSTOM ISLANDS] No land on '" + ground.name + "' for its animals' NavMesh"); yield break; }
					local.Expand(new Vector3(16f, 12f, 16f));
					local.center -= root.transform.position;
					// (on dry land only, the sea at y 0: llamas walked into the sea - AU84)
					float shore = Mathf.Max(local.min.y, -WadeDepth - root.transform.position.y);
					if (shore < local.max.y) local.SetMinMax(new Vector3(local.min.x, shore, local.min.z), local.max);
					Debug.Log("[CUSTOM ISLANDS] NavMesh sources on Raft's island '" + ground.name + "': " + sources.Count + ", land " + local.size.ToString("F0"));
				}
				catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Collecting the ground of Raft's island for a NavMesh failed: " + e); yield break; }
			}
			else try
			{
				Physics.SyncTransforms();
				NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
				UnreadableAsBoxes(sources);
					CaveFloors(sources);
				// Everything with a collider, relative to the island (whose root is never turned)
				bool any = false;
				foreach (Collider c in root.GetComponentsInChildren<Collider>())
				{
					if (c.isTrigger) continue;
					Bounds b = c.bounds;
					b.center -= root.transform.position;
					if (!any) { local = b; any = true; } else local.Encapsulate(b);
				}
				local.Expand(4f);
				// Animals walk on the land only, never into the sea (they went 6 m deep and llamas were seen walking in the
				// water - AU84): and an island on a deep sea floor has a terrain reaching 160 m down and hundreds of metres
				// out, which would make the NavMesh many times slower to build
				IslandSettings island = root.GetComponent<IslandSettings>();
				Terrain terrain = root.GetComponentInChildren<Terrain>();
				if (island != null && terrain != null && terrain.terrainData != null)
				{
					float lowest = island.WaterLevel - WadeDepth; // (root-local height)
					TerrainData td = terrain.terrainData;
					int res = td.heightmapResolution;
					float[,] h = td.GetHeights(0, 0, res, res);
					Vector3 origin = terrain.transform.position - root.transform.position;
					float cell = td.size.x / (res - 1);
					int minX = res, maxX = -1, minZ = res, maxZ = -1;
					for (int z = 0; z < res; z++)
						for (int x = 0; x < res; x++)
							if (origin.y + h[z, x] * td.size.y > lowest) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (z < minZ) minZ = z; if (z > maxZ) maxZ = z; }
					if (maxX >= 0)
					{
						Vector3 min = new Vector3(Mathf.Max(local.min.x, origin.x + minX * cell - 8f), Mathf.Max(local.min.y, lowest), Mathf.Max(local.min.z, origin.z + minZ * cell - 8f));
						Vector3 max = new Vector3(Mathf.Min(local.max.x, origin.x + maxX * cell + 8f), local.max.y, Mathf.Min(local.max.z, origin.z + maxZ * cell + 8f));
						if (max.x > min.x && max.y > min.y && max.z > min.z) local.SetMinMax(min, max);
					}
				}
				// (an island without a terrain: the same line at the sea)
				if (island != null && local.min.y < island.WaterLevel - WadeDepth && island.WaterLevel - WadeDepth < local.max.y)
					local.SetMinMax(new Vector3(local.min.x, island.WaterLevel - WadeDepth, local.min.z), local.max);
				Debug.Log("[CUSTOM ISLANDS] NavMesh sources: " + sources.Count + " (" + sources.Count(s => s.shape == NavMeshBuildSourceShape.Terrain) + " terrain), area " + local.min.ToString("F0") + " to " + local.max.ToString("F0") + " around the island's corner");
			}
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Collecting the island's ground for its NavMesh failed: " + e); yield break; }

			foreach (int agentType in agentTypes)
			{
				if (root == null) yield break;
				AsyncOperation op = null;
				try
				{
					NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentType);
					// A little coarser than Unity's default: much quicker for a whole island, still fine for animals
					settings.overrideVoxelSize = true;
					settings.voxelSize = Mathf.Max(0.2f, settings.agentRadius / 2f);
					var data = new NavMeshData(agentType) { position = root.transform.position, rotation = root.transform.rotation };
					NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
					surface.agentTypeID = agentType;
					surface.navMeshData = data;
					surface.AddData();
					op = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, local);
				}
				catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Building the island's NavMesh failed: " + e); }
				float until = Time.realtimeSinceStartup + 60f;
				while (op != null && !op.isDone && root != null)
				{
					if (Time.realtimeSinceStartup > until) { Debug.LogWarning("[CUSTOM ISLANDS] The island's NavMesh is taking more than a minute; spawning anyway"); break; }
					yield return null;
				}
			}
		}

		#endregion

		#region Watching the animals (host) and colouring them (clients)

		/// <summary>Each island's spawn points (by its root), with its hierarchy size then: looked up again only when something
		/// was added under it or taken away (an island still spawning its objects, an object removed) - the looks every second
		/// walked every island's whole hierarchy.</summary>
		static readonly Dictionary<GameObject, KeyValuePair<int, CreatureSpawnPoint[]>> spotsOf = new Dictionary<GameObject, KeyValuePair<int, CreatureSpawnPoint[]>>();

		static CreatureSpawnPoint[] SpotsOf(GameObject root)
		{
			int shape = root.transform.hierarchyCount;
			KeyValuePair<int, CreatureSpawnPoint[]> c;
			if (spotsOf.TryGetValue(root, out c) && c.Key == shape) return c.Value;
			if (c.Value == null) foreach (GameObject gone in spotsOf.Keys.Where(k => k == null).ToList()) spotsOf.Remove(gone);
			CreatureSpawnPoint[] spots = root.GetComponentsInChildren<CreatureSpawnPoint>(true);
			spotsOf[root] = new KeyValuePair<int, CreatureSpawnPoint[]>(shape, spots);
			return spots;
		}

		/// <summary>Every frame from the mod: once a second, records killed and caught animals.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + 1f;
			if (!LoadSceneManager.IsGameSceneLoaded) return;
			if (Raft_Network.IsHost) Watch();
			else TintRemote();
		}

		static void Watch()
		{
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) continue;
				foreach (CreatureSpawnPoint p in SpotsOf(e.Root))
				{
					if (p == null || p.RecordedAlive < 0) continue; // not spawned yet
					// A "hide" action on the spot: its animals go too (not counted as defeated), and come back when it is shown
					if (!p.gameObject.activeInHierarchy)
					{
						int gone = RemoveAnimals(p);
						p.RecordedAlive = -1;
						p.Strayed = 0;
						if (gone > 0) Debug.Log("[CUSTOM ISLANDS] The spot of " + gone + " " + p.Kind.Label + "(s) on '" + e.HostName + "' was hidden: they are gone until it is shown");
						continue;
					}
					BossPhases.Check(e, p);
					Account(e, p);
				}
			}
		}

		/// <summary>Host: counts a spot's animals killed and caught since the last look - for quests, the "defeat" event and
		/// what comes back when the island loads again. Also right before an island unloads (a kill in its last second was
		/// lost: the animal came back and the kill didn't count).</summary>
		static void Account(IslandWorldState.Entry e, CreatureSpawnPoint p)
		{
			if (p.RecordedAlive < 0) return;
			int caught = 0, strayed = 0;
			var seen = new HashSet<int>();
			for (int i = p.Spawned.Count - 1; i >= 0; i--)
			{
				AI_NetworkBehaviour ai = p.Spawned[i];
				if (ai == null) { p.Spawned.RemoveAt(i); continue; }
				int id = ai.GetInstanceID();
				// Caught (Raft lets go of it when it is carried): it belongs to the players now
				if (ai.connectedSpawner == null)
				{
					p.Spawned.RemoveAt(i);
					p.LastAt.Remove(id);
					ours.Remove(ai);
					caught++;
					Debug.Log("[CUSTOM ISLANDS] A " + p.Kind.Label + " of '" + e.HostName + "' was caught");
					continue;
				}
				if (ai.networkEntity == null || !ai.networkEntity.IsDead) { p.LastAt[id] = ai.transform.position; seen.Add(id); }
				else { p.LastAt.Remove(id); felled.Remove(id); }
			}
			// Gone while alive (Raft may drop it from the list before the object goes, so this goes by the ones last seen
			// alive): far from every player - or one of Raft's harmless sea animals, which it takes away on its own timer
			// even close by - Raft took it away (Sargasso Town's dolphins and turtles were "defeated" as the raft sailed
			// off, 2026-10-11). Not killed; it comes back with the island.
			foreach (int id in p.LastAt.Keys.Where(k => !seen.Contains(k)).ToList())
			{
				Vector3 at = p.LastAt[id];
				if (felled.Remove(id)) { p.LastAt.Remove(id); continue; }
				if (Harmless(p.Kind.Type) || !Players.All.Any(pl => pl != null && Vector2.Distance(new Vector2(pl.transform.position.x, pl.transform.position.z), new Vector2(at.x, at.z)) < StrayDistance)) strayed++;
				p.LastAt.Remove(id);
			}
			// (just spawned, Raft may not have set up its network entity yet: that one is alive, not killed)
			int alive = p.Spawned.Count(ai => ai != null && (ai.networkEntity == null || !ai.networkEntity.IsDead));
			if (alive == p.RecordedAlive) return;
			// For quests: the ones that are gone and weren't caught were killed
			int killed = p.RecordedAlive - alive - caught - strayed;
			if (strayed > 0)
			{
				p.Strayed += strayed;
				Debug.Log("[CUSTOM ISLANDS] " + strayed + " " + p.Kind.Label + "(s) of '" + e.HostName + "' taken away by Raft (not defeated; back with the island)");
			}
			if (killed > 0) Debug.Log("[CUSTOM ISLANDS] " + killed + " " + p.Kind.Label + "(s) of '" + e.HostName + "' defeated (" + alive + " left; animals " + string.Join(", ", p.Spawned.Select(a => a == null ? "gone" : a.networkEntity == null ? "no entity" : a.networkEntity.IsDead ? "dead" : "alive").ToArray()) + ")");
			if (caught > 0) QuestTracker.Event(e, "catch", p.Kind.Label, caught);
			if (killed > 0) QuestTracker.Event(e, "kill", p.Kind.Label, killed);
			// None left - killed or caught (the last chicken netted clears the spot as much as the last one killed): the
			// spot's "defeat" actions
			if (alive == 0 && (killed > 0 || caught > 0)) { BossPhases.OnDefeat(e, p); IslandObjectRef r = p.GetComponent<IslandObjectRef>(); if (r != null) Behaviours.FireFromHost(e, r.Index, "defeat"); }
			Record(e, p, alive);
		}

		/// <summary>Raft's harmless sea animals: players can't kill them, Raft takes them away after a while.</summary>
		/// <summary>Host, after a hit: notes one of our animals the hit left dead (see felled).</summary>
		public static void Hit(Network_Entity entity)
		{
			if (entity == null || !(entity.IsDead || (entity.stat_health != null && entity.stat_health.Value <= 0f))) return;
			AI_NetworkBehaviour ai = entity.GetComponentInParent<AI_NetworkBehaviour>();
			if (ai == null) ai = entity.GetComponentInChildren<AI_NetworkBehaviour>();
			if (ai != null && ours.Contains(ai)) felled.Add(ai.GetInstanceID());
		}

		/// <summary>Host, after any hit on one of our animals (a guest's too: Raft's DamageEntity runs here for it): a boss
		/// phase starts at once, not up to a second later.</summary>
		public static void HitPhase(Network_Entity entity)
		{
			if (entity == null || !Raft_Network.IsHost || entity.IsDead) return;
			AI_NetworkBehaviour ai = entity.GetComponentInParent<AI_NetworkBehaviour>();
			if (ai == null) ai = entity.GetComponentInChildren<AI_NetworkBehaviour>();
			if (ai == null || !ours.Contains(ai)) return;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) continue;
				foreach (CreatureSpawnPoint p in SpotsOf(e.Root))
					if (p != null && p.Spawned.Contains(ai)) { BossPhases.Check(e, p); return; }
			}
		}

		static bool Harmless(AI_NetworkBehaviourType t)
		{
			return t == AI_NetworkBehaviourType.Dolphin || t == AI_NetworkBehaviourType.Turtle || t == AI_NetworkBehaviourType.Stingray || t == AI_NetworkBehaviourType.Whale;
		}

		static void Record(IslandWorldState.Entry e, CreatureSpawnPoint p, int alive)
		{
			p.RecordedAlive = alive;
			// (the ones Raft took away far off are still the island's)
			int left = alive + p.Strayed;
			if (left >= ObjectProps.Count(p.Props)) e.State.Remove(p.StateKey);
			else e.State[p.StateKey] = new ObjectState { Active = left > 0, Yield = left, Day = Today };
		}

		/// <summary>Host: an island is being unloaded or removed; its wild animals (and their bodies) go with it.</summary>
		public static void OnIslandDespawned(GameObject root)
		{
			if (root == null || !Raft_Network.IsHost) return;
			int removed = 0;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Root == root);
			foreach (CreatureSpawnPoint p in root.GetComponentsInChildren<CreatureSpawnPoint>(true))
			{
				// (the last second's kills and catches counted first: the watch looks only once a second)
				if (e != null && p.gameObject.activeInHierarchy) try { Account(e, p); } catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Counting the animals of '" + e.HostName + "': " + ex.Message); }
				removed += RemoveAnimals(p);
			}
			if (removed > 0) Debug.Log("[CUSTOM ISLANDS] Removed " + removed + " creature(s) with their island");
		}

		/// <summary>Host: takes a spot's wild animals away (for every player). Caught ones belong to the players and stay.</summary>
		static int RemoveAnimals(CreatureSpawnPoint p)
		{
			int removed = 0;
			foreach (AI_NetworkBehaviour ai in p.Spawned.Concat(p.Summoned).ToList())
			{
				if (ai == null || ai.connectedSpawner == null) continue;
				ours.Remove(ai);
				foreach (AI_Movement m in ai.GetComponentsInChildren<AI_Movement>(true)) speed.Remove(m);
				try { NetworkIDManager.SendIDBehaviourDead(ai.ObjectIndex, typeof(AI_NetworkBehaviour), true); removed++; }
				catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Removing a " + p.Kind.Label + ": " + ex.Message); UnityEngine.Object.Destroy(ai.gameObject); }
			}
			p.Spawned.Clear();
			p.Summoned.Clear();
			p.LastAt.Clear();
			return removed;
		}

		static readonly HashSet<AI_NetworkBehaviour> clientTinted = new HashSet<AI_NetworkBehaviour>();

		/// <summary>
		/// Clients get the animals from Raft's networking without their colour or their builder's health: each tinted or
		/// tougher spawn point of a loaded island dresses the nearest animal of its kind near it (animals stay near their
		/// spawn point) - its colour, and its health as the host has it (MatchHealth).
		/// </summary>
		static void TintRemote()
		{
			List<CreatureSpawnPoint> spots = IslandSpawner.SpawnedRoots.Where(r => r != null)
				.SelectMany(r => SpotsOf(r)).Where(p => p != null && p.Kind != null).ToList();
			if (!spots.Any(p => ObjectProps.HasTint(p.Props) || !Mathf.Approximately(ObjectProps.Health(p.Props), 1f))) return;
			AI_NetworkBehaviour[] all = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>();
			Network_Host_Entities host = HostEntities;
			foreach (AI_NetworkBehaviour ai in all)
			{
				if (ai == null || clientTinted.Contains(ai) || ai.connectedSpawner != null) continue;
				// (the host said whose it is: that spot, or none - not an island's animal, or its island isn't loaded yet)
				if (HostSendsSpots)
				{
					CreatureSpawnPoint sent = SentSpot(ai);
					if (sent == null) continue;
					clientTinted.Add(ai);
					if (ObjectProps.HasTint(sent.Props)) ObjectProps.ApplyTint(ai.gameObject, sent.Props);
					float shp = ObjectProps.Health(sent.Props);
					if (!Mathf.Approximately(shp, 1f)) MatchHealth(ai, shp);
					continue;
				}
				// The animal's own spot: the nearest of its kind whose size it has (Raft sends the size; hostile
				// animals roam far from their spot while chasing players, so the nearest tinted spot alone could
				// colour an untinted animal of a spot next to it). Only that spot's tint counts. Raft rolls some kinds'
				// size (bears 0.7-0.8, llamas 0.8-1.2, rats, roaches...): the spot's size times that range.
				float size = ai.transform.localScale.x;
				float lo = 1f, hi = 1f;
				AI_NetworkBehaviour prefab = host != null ? Prefab(host, ai.behaviourType) : null;
				try { if (prefab != null && prefab.localScaleInterval.maxValue > 0f) { lo = Mathf.Min(prefab.localScaleInterval.minValue, prefab.localScaleInterval.maxValue); hi = Mathf.Max(prefab.localScaleInterval.minValue, prefab.localScaleInterval.maxValue); } } catch { }
				CreatureSpawnPoint best = null;
				float bestDist = 150f;
				foreach (CreatureSpawnPoint p in spots)
				{
					float s = ObjectProps.Size(p.Props); // (clamped as the host spawned it: a file's x5 came as x3)
					if (p.Kind.Type != ai.behaviourType || size < s * lo - 0.05f || size > s * hi + 0.05f) continue;
					float d = Vector3.Distance(p.transform.position, ai.transform.position);
					if (d < bestDist) { bestDist = d; best = p; }
				}
				if (best == null) continue;
				clientTinted.Add(ai);
				if (ObjectProps.HasTint(best.Props)) ObjectProps.ApplyTint(ai.gameObject, best.Props);
				float hp = ObjectProps.Health(best.Props);
				if (!Mathf.Approximately(hp, 1f)) MatchHealth(ai, hp);
			}
			clientTinted.RemoveWhere(a => a == null);
			foreach (AI_NetworkBehaviour gone in clientHealth.Keys.Where(a => a == null).ToList()) clientHealth.Remove(gone);
		}

		// Client: the multiplier each animal's copy got (MatchHealth), and the health the host sent with each animal it
		// created (Message_CreateAINetworkBehaviour.entityHealth, by the animal's object index) with when it came
		static readonly Dictionary<AI_NetworkBehaviour, float> clientHealth = new Dictionary<AI_NetworkBehaviour, float>();
		static readonly Dictionary<uint, KeyValuePair<float, float>> sentHealth = new Dictionary<uint, KeyValuePair<float, float>>();
		/// <summary>How long a health the host sent is trusted (an island's spots may load a while after its animals came).</summary>
		const float SentHealthSeconds = 600f;

		/// <summary>Client: the multiplier this animal's health got here to match the host's (1 = none).</summary>
		public static float ClientHealthOf(AI_NetworkBehaviour ai) { float m; return ai != null && clientHealth.TryGetValue(ai, out m) ? m : 1f; }

		/// <summary>
		/// Client: gives an animal's copy the maximum health the host gave it (a built creature's toughness, a randomizer
		/// alpha). Raft creates other players' copies with its own maximum, sets the health the host sent cut down to that,
		/// and then takes each hit's damage off it (Message_NetworkEntity_Damage) - so without this a tough animal dies on
		/// their screen while it still fights on the host's. What Raft cut off is added back, which stays right when hits
		/// have landed since (both machines take the same damage off).
		/// </summary>
		public static void MatchHealth(AI_NetworkBehaviour ai, float multiplier)
		{
			if (Raft_Network.IsHost || ai == null) return;
			// (the host's own numbers came before the animal did: those, not a guess)
			float[] exact;
			if (exactHealth.TryGetValue(ai.ObjectIndex, out exact))
			{
				exactHealth.Remove(ai.ObjectIndex);
				if (Time.realtimeSinceStartup - exact[2] < ExactHealthSeconds && SetHostHealth(ai, exact[0], exact[1])) return;
			}
			if (clientHealth.ContainsKey(ai) || Mathf.Approximately(multiplier, 1f)) return;
			Network_Entity entity = ai.networkEntity;
			if (entity == null || entity.stat_health == null || entity.IsDead) return;
			Stat_Health h = entity.stat_health;
			float plain = h.Max;
			if (plain <= 0f) return;
			float value = h.Value;
			KeyValuePair<float, float> sent;
			bool known = sentHealth.TryGetValue(ai.ObjectIndex, out sent) && Time.realtimeSinceStartup - sent.Value < SentHealthSeconds;
			// (an animal made while players were here - an alpha appearing - was sent with Raft's plain health, before the
			// host gave it its multiplier: taken as the host's, it had a third of its health on their screens. A sent health
			// below the plain one times the multiplier isn't the host's final one - AU31. Only Raft's plain health itself: a tough
			// animal already hurt on the host, sent to a player who joined, came between the two and got full health there)
			if (known && multiplier > 1f && Mathf.Abs(sent.Key - plain) <= plain * 0.01f) known = false;
			float target = known ? value + Mathf.Max(0f, sent.Key - plain) : value * multiplier;
			HealthBefore[ai] = plain;
			h.SetMaxValue(plain * multiplier);
			h.Value = Mathf.Clamp(target, 0f, plain * multiplier);
			clientHealth[ai] = multiplier;
			Debug.Log("[CUSTOM ISLANDS] " + ai.behaviourType + " #" + ai.ObjectIndex + ": the host's health here, " + value.ToString("F0") + "/" + plain.ToString("F0") + " -> " +
				h.Value.ToString("F0") + "/" + h.Max.ToString("F0") + (known ? " (the host sent " + sent.Key.ToString("F0") + ")" : " (the host's number not known: kept the share)"));
		}

		// Client: the host's health for an animal that turned alpha (message AlphaHealth) that wasn't here yet: { max, health, when it came }
		static readonly Dictionary<uint, float[]> exactHealth = new Dictionary<uint, float[]>();
		/// <summary>How long a host's alpha health waits for its animal (Raft's message creating it may come a little after ours).</summary>
		const float ExactHealthSeconds = 60f;

		/// <summary>Host: tells every other player an animal's health as it is now (it just turned alpha: WorldRandomizer, Big Bruce
		/// too) - once, not at every change; hits after it go through Raft's own damage messages on every machine.</summary>
		public static void SendHostHealth(AI_NetworkBehaviour ai)
		{
			if (!Raft_Network.IsHost || ai == null || ai.networkEntity == null || ai.networkEntity.stat_health == null) return;
			Stat_Health h = ai.networkEntity.stat_health;
			if (h.Max <= 0f) return;
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.AlphaHealth, Index = unchecked((int)ai.ObjectIndex), Offsets = new[] { h.Max, h.Value } });
		}

		/// <summary>Client: the host's health for an animal that turned alpha (message AlphaHealth): set exactly; an animal not here yet
		/// gets it when it comes (MatchHealth). MatchHealth's guess stays for players who join later (the host sends Raft's health then).</summary>
		public static void HostHealth(uint index, float max, float value)
		{
			if (Raft_Network.IsHost || max <= 0f) return;
			AI_NetworkBehaviour ai = null;
			try { ai = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(a => a != null && a.ObjectIndex == index); } catch { }
			if (ai != null && SetHostHealth(ai, max, value)) { exactHealth.Remove(index); return; }
			if (exactHealth.Count > 256)
				foreach (uint old in exactHealth.Where(kv => Time.realtimeSinceStartup - kv.Value[2] >= ExactHealthSeconds).Select(kv => kv.Key).ToList()) exactHealth.Remove(old);
			exactHealth[index] = new[] { max, value, Time.realtimeSinceStartup };
			Debug.Log("[CUSTOM ISLANDS] Animal #" + index + ": the host's alpha health " + value.ToString("F0") + "/" + max.ToString("F0") + " kept until it is here");
		}

		static bool SetHostHealth(AI_NetworkBehaviour ai, float max, float value)
		{
			Network_Entity entity = ai.networkEntity;
			if (entity == null || entity.stat_health == null || entity.IsDead) return false;
			Stat_Health h = entity.stat_health;
			// (Raft's own maximum: kept from an earlier MatchHealth, or what this copy has before we change it)
			float plain;
			if (!HealthBefore.TryGetValue(ai, out plain) || plain <= 0f) plain = h.Max;
			if (plain <= 0f) return false;
			float had = h.Value, hadMax = h.Max;
			HealthBefore[ai] = plain;
			h.SetMaxValue(max);
			h.Value = Mathf.Clamp(value, 0f, max);
			clientHealth[ai] = max / plain;
			Debug.Log("[CUSTOM ISLANDS] " + ai.behaviourType + " #" + ai.ObjectIndex + ": the host's alpha health here, " + had.ToString("F0") + "/" + hadMax.ToString("F0") + " -> " +
				h.Value.ToString("F0") + "/" + h.Max.ToString("F0") + " (sent by the host)");
			return true;
		}

		/// <summary>Client (Harmony postfix on Network_Host_Entities.Deserialize): remembers the health the host sent with an animal.</summary>
		static void CreateOnClientPostfix(Message_NetworkBehaviour msg)
		{
			try
			{
				if (Raft_Network.IsHost) return;
				var m = msg as Message_CreateAINetworkBehaviour;
				if (m == null) return;
				if (sentHealth.Count > 4096)
					foreach (uint old in sentHealth.Where(kv => Time.realtimeSinceStartup - kv.Value.Value >= SentHealthSeconds).Select(kv => kv.Key).ToList()) sentHealth.Remove(old);
				sentHealth[m.behaviourObjectIndex] = new KeyValuePair<float, float>(m.entityHealth, Time.realtimeSinceStartup);
			}
			catch { }
		}

		#endregion

		#region Harmony patches (applied by hand, so a Raft update that renames something only disables that part)

		public static void Patch(Harmony harmony)
		{
			Try(harmony, typeof(AI_Movement), "SetMovementSpeed", "SetSpeedPrefix", null);
			Try(harmony, typeof(AI_Movement), "ChangeMovementSpeedTowards", "TargetPrefix", null, "GuardFinalizer");
			Try(harmony, typeof(AI_Movement), "LerpMovementSpeedTowards", "TargetPrefix", null, "GuardFinalizer");
			Try(harmony, typeof(AI_Movement), "HandleLerpData", "GuardPrefix", null, "GuardFinalizer");
			Try(harmony, typeof(AI_NetworkBehaviour_Animal), "Serialize_CreateFromIDManager", null, "CreateForJoinersPostfix");
			Try(harmony, typeof(Network_Host_Entities), "Deserialize", null, "CreateOnClientPostfix");
		}

		static void Try(Harmony harmony, Type type, string method, string prefix, string postfix, string finalizer = null)
		{
			try
			{
				MethodInfo original = AccessTools.Method(type, method);
				if (original == null) { Debug.LogWarning("[CUSTOM ISLANDS] " + type.Name + "." + method + " not found: creature settings may not fully apply"); return; }
				harmony.Patch(original,
					prefix != null ? new HarmonyMethod(typeof(CreatureSpawner).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)) : null,
					postfix != null ? new HarmonyMethod(typeof(CreatureSpawner).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)) : null,
					null,
					finalizer != null ? new HarmonyMethod(typeof(CreatureSpawner).GetMethod(finalizer, BindingFlags.Static | BindingFlags.NonPublic)) : null);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not patch " + type.Name + "." + method + ": " + e.Message); }
		}

		// Speed: every speed an animal's states ask for is multiplied once. Moving towards a target speed (or lerping)
		// multiplies the target, and the steps it takes on the way are left alone.
		[ThreadStatic] static int guard;

		static void SetSpeedPrefix(AI_Movement __instance, ref float value) { if (guard == 0) value *= SpeedOf(__instance); }
		static void TargetPrefix(AI_Movement __instance, ref float target) { guard++; target *= SpeedOf(__instance); }
		static void GuardPrefix() { guard++; }
		// (a Finalizer, not a Postfix: a Postfix is skipped when Raft's method throws, and the guard then stayed up - every
		// creature speed setting off until Raft restarted. Audit 2026-10-06, as AU41)
		static Exception GuardFinalizer(Exception __exception) { if (guard > 0) guard--; return __exception; }

		/// <summary>
		/// Raft sends the animals that exist to a player who joins, except those tied to a landmark's spawner (the
		/// landmark brings them). Ours are tied to our own spawner, so they are sent here instead.
		/// </summary>
		static void CreateForJoinersPostfix(AI_NetworkBehaviour_Animal __instance, List<Message_NetworkBehaviour> msgs)
		{
			try
			{
				if (!IsOurs(__instance) || !__instance.ConnectedToSpawner || msgs == null) return;
				Network_Host_Entities host = HostEntities;
				Raft_Network network = ComponentManager<Raft_Network>.Value;
				if (host == null || network == null) return;
				msgs.Add(new Message_CreateAINetworkBehaviour(Messages.CreateAINetworkBehaviour, network.NetworkIDManager, host.ObjectIndex, __instance.transform.position, __instance, null));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Sending a creature to a joining player: " + e.Message); }
		}

		#endregion
	}

	/// <summary>
	/// AU61: a game mode that takes players' damage away (PlayerSpecificVariables.negateOutgoingPlayerDamage: Raft's
	/// Network_Host.DamageEntity then makes every hit on an enemy 0) would leave an island's kill steps and lairs that can
	/// never be done, and no EXP. None of Raft's own modes has it on (Peaceful, Easy, Normal, Hard and Creative, read from
	/// Raft's game files 2026-10-06 - in Peaceful the monsters are tame, they don't fight back), but another mod's mode
	/// could: for a player's hit on an island creature it is off, so the island's animals still fall to players' hits.
	/// The rest of what the mode does to the hit (Hard's x0.8 for players' hits on monsters) stays: Raft applies it on the
	/// hitting player's machine, before the hit goes to the host, as for its own islands.
	/// First: the hit's EXP (LevelDamagePatch, last) reads the switch as Raft will.
	/// </summary>
	[HarmonyPatch(typeof(Network_Host), "DamageEntity")]
	static class IslandCreatureHitPatch
	{
		[HarmonyPriority(Priority.First)]
		static void Prefix(Network_Entity entity, EntityType damageInflictorEntityType, out PlayerSpecificVariables __state)
		{
			__state = null;
			try
			{
				if (entity == null || damageInflictorEntityType != EntityType.Player || entity.entityType != EntityType.Enemy) return;
				SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
				PlayerSpecificVariables v = mode != null ? mode.playerSpecificVariables : null;
				if (v == null || !v.negateOutgoingPlayerDamage) return;
				AI_NetworkBehaviour ai = entity.GetComponentInParent<AI_NetworkBehaviour>();
				if (ai == null) ai = entity.GetComponentInChildren<AI_NetworkBehaviour>();
				if (!CreatureSpawner.IsOnCustomIsland(ai)) return;
				v.negateOutgoingPlayerDamage = false;
				__state = v;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] A hit on an island creature: " + e.Message); }
		}

		static void Postfix(Network_Entity entity)
		{
			try { CreatureSpawner.Hit(entity); CreatureSpawner.HitPhase(entity); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] After a hit on an island creature: " + e.Message); }
		}

		// (a Finalizer: the mode gets its switch back also when Raft's DamageEntity throws)
		static Exception Finalizer(Exception __exception, PlayerSpecificVariables __state)
		{
			if (__state != null) __state.negateOutgoingPlayerDamage = true;
			return __exception;
		}
	}
}
