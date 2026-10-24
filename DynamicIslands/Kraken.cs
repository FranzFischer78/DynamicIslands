using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option Kraken arm (WorldOptions.Kraken): rarely a giant tentacle rises from the sea beside the raft and
	/// grabs a foundation on its edge (never one in the middle). 1-3 more arms rise a few metres off the edge, for looks
	/// only. Players hit the grabbing arm with melee weapons or spears (HitsNeeded hits, any players together) within
	/// StaySeconds and it lets go; if not, it tears that floor away and each floor right beside it (the 4 sides, same level)
	/// with a 1 in 2 chance, rolled from the event's seed, through Raft's own block removal.
	/// Timing (host only, from the world seed): from day FirstDay on about one day in eight (Chance), at a seeded time in that
	/// day; with Storm seas on also in about one storm in four (StormChance). Rafts under MinFloors foundations are never
	/// grabbed. Never near land: a due arm waits while the raft is within NearLand m of an island (NearIsland), and is lost
	/// if the day ends first. The host decides everything and tells everyone (IslandNetMessage.Kraken); a player who joins mid-event gets
	/// the arm in the join reply. Nothing about an arm is saved: a world loaded mid-event has none.
	/// The arm is tapered capsules with a copy of Raft's shark skin, tinted purple, with pale suckers on the inner curl;
	/// its base reaches 18 m down so it always comes out of deep water.
	/// </summary>
	public static class Kraken
	{
		public const int FirstDay = 8, HitsNeeded = 10, MinFloors = 9;
		/// <summary>How close to an island's land (metres, flat) the raft may be before a due arm waits.</summary>
		public const float NearLand = 80f;
		/// <summary>The free arms' swipes: one every SwipeEvery s per arm at the nearest player within SwipeReach m of it;
		/// it rears up for a second, then slams where that player stood: SwipeDamage to a player still within SwipeRadius m.</summary>
		public const float SwipeEvery = 7f, SwipeReach = 9f, SwipeRadius = 2.2f, SwipeDamage = 5f;
		/// <summary>A green or blue kraken's swipes hurt this much more; one sighting takes at most MaxTaken health from a
		/// player, and never brings them below KeepHealth.</summary>
		public const float RareColourDamage = 1.1f, MaxTaken = 30f, KeepHealth = 25f;

		/// <summary>What one swipe takes from a player who has lost `taken` to this sighting and has `health` left (0: nothing).</summary>
		public static float SwipeHit(int colouring, float taken, float health)
		{
			float hit = SwipeDamage * (colouring == 0 ? 1f : RareColourDamage);
			hit = Mathf.Min(hit, MaxTaken - taken, health - KeepHealth);
			return hit > 0.05f ? hit : 0f;
		}
		/// <summary>Swipes that hurt this machine's player (tests).</summary>
		public static int SwipesLanded;
		public const float Chance = 1f / 8f, StormChance = 0.25f, StaySeconds = 60f, RiseSeconds = 3f, SinkSeconds = 3f;
		/// <summary>How deep the arms' bases are, in metres under the deck.</summary>
		public const float Depth = 18f;
		const int SaltDay = 727, SaltTime = 728, SaltStorm = 729, SaltEvent = 730, SaltNeighbour = 731, SaltEdge = 732, SaltExtra = 733, SaltColour = 734;

		public static bool On { get { return WorldOptions.On(WorldOptions.Kraken); } }
		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		// The arm out now (host and players alike)
		static Arm arm;
		public static bool Active { get { return arm != null && arm.Phase < 2; } }
		public static int Number { get { return arm != null ? arm.Number : 0; } }
		public static int Hits { get { return arm != null ? arm.Hits : 0; } }
		public static int Need { get { return arm != null ? arm.Need : HitsNeeded; } }
		public static uint BlockIndex { get { return arm != null ? arm.BlockIndex : 0u; } }
		public static int EventSeed { get { return arm != null ? arm.Seed : 0; } }
		/// <summary>The side of the grabbed floor the arm comes from (an index into StormSeas.Sides; open water).</summary>
		public static int Side { get { return arm != null ? arm.SideIndex : -1; } }
		public static int ExtraArms { get { return arm != null ? arm.Extras : 0; } }
		public static Arm Current { get { return arm; } }

		// Counters for the command and tests (not saved)
		public static int Brought, Beaten, Torn, LastTornCount;
		public static string LastEnd = "";
		/// <summary>Tests: a shorter stay before the arm tears.</summary>
		public static float? TestStay;

		// Host timing
		static int rolledDay = -1, seenStorms = -1, nextNumber;
		static float dueAt = -1f;
		static bool waiting;
		// A player: a "rise" whose floor hasn't arrived yet
		static IslandNetMessage pending;
		static float pendingSince;

		static void Log(string s) { Debug.Log("[CUSTOM ISLANDS] [kraken] " + s); }

		public static void Reset()
		{
			if (arm != null) UnityEngine.Object.Destroy(arm.gameObject);
			arm = null; pending = null;
			rolledDay = seenStorms = -1; dueAt = -1f;
			LastEnd = "";
		}

		/// <summary>Whether a day brings an arm (from the world seed).</summary>
		public static bool Rolls(int seed, int day) { return day >= FirstDay && WorldOptions.Unit(seed, day, SaltDay) < Chance; }
		/// <summary>Whether storm number n brings an arm (Storm seas on).</summary>
		public static bool StormRolls(int seed, int storm) { return WorldOptions.Unit(seed, storm, SaltStorm) < StormChance; }
		/// <summary>Whether the floor on side i of the grabbed one goes too when the arm tears (1 in 2, from the event's seed).</summary>
		public static bool NeighbourGoes(int eventSeed, int side) { return WorldOptions.Unit(eventSeed, side, SaltNeighbour) < 0.5; }

		#region Host: when

		public static void Tick()
		{
			if (pending != null)
			{
				IslandNetMessage p = pending; float since = pendingSince;
				pending = null;
				if (!Rise(p) && pending != null)
				{
					pendingSince = since;
					if (Time.time - since > 15f) { Log("the grabbed floor never arrived; no arm"); pending = null; }
				}
			}
			if (!Raft_Network.IsHost)
			{
				// A player: the option off (synced) lets go; a lost host doesn't leave an arm forever
				if (arm != null && arm.Phase < 2 && (!On || Time.time - arm.Started > arm.Stay + 90f)) arm.Sink(false);
				return;
			}
			if (!LoadSceneManager.IsGameSceneLoaded || RAPI.GetLocalPlayer() == null) return;
			if (!On)
			{
				if (arm != null && arm.Phase < 2) Away("the option was switched off");
				dueAt = -1f;
				return;
			}
			int seed = WorldOptions.Seed, day = Today;
			if (day != rolledDay)
			{
				rolledDay = day;
				waiting = false;
				dueAt =Rolls(seed, day) ? Time.time + 60f + 540f * (float)WorldOptions.Unit(seed, day, SaltTime) : -1f;
				if (dueAt > 0) Log("day " + day + " brings an arm in " + Mathf.RoundToInt(dueAt - Time.time) + " s");
			}
			if (StormSeas.On && StormSeas.Storms != seenStorms)
			{
				if (seenStorms >= 0 && StormSeas.Storms > seenStorms && StormRolls(seed, StormSeas.Storms))
				{
					dueAt = Time.time + 20f + 60f * (float)WorldOptions.Unit(seed, StormSeas.Storms, SaltTime);
					Log("storm " + StormSeas.Storms + " brings an arm in " + Mathf.RoundToInt(dueAt - Time.time) + " s");
				}
				seenStorms = StormSeas.Storms;
			}
			if (dueAt > 0 && Time.time >= dueAt)
			{
				string near = arm == null ? NearIsland() : null;
				if (near != null)
				{
					// Wait for open sea; checked again every 30 s until the day ends
					if (!waiting) Log("due, but waits: " + near);
					waiting = true;
					dueAt = Time.time + 30f;
				}
				else
				{
					waiting = false;
					dueAt = -1f;
					if (arm == null) Log("due: " + Bring());
				}
			}
			if (arm != null && arm.Phase < 2)
			{
				if (arm.Block == null) Away("the grabbed floor is gone");
				else if (!arm.Released && Time.time - arm.Started >= arm.Stay) TearNow();
			}
		}

		/// <summary>Why the raft is too near land for an arm, or null in open sea: within NearLand m of a custom island's land
		/// or of one of Raft's islands (its chunk point's footprint; drifting raft wrecks don't count).</summary>
		public static string NearIsland()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue) return null;
			Vector3 p = raft.Value;
			try { if (ChunkManager.RaftIsInsideChunkPoint) return "the raft is at one of Raft's islands"; } catch { }
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Failed || WorldRandomizer.IsExtras(e)) continue; // (extras sit on Raft's islands: their chunk points below)
				float land = CustomIslandSpawner.LandRadius(e.Name);
				float d = Flat(p - e.Position);
				if (d < (land > 0f ? land : 150f) + NearLand) return "custom island '" + e.Name + "' " + d.ToString("F0") + " m away";
			}
			ChunkManager cm = ComponentManager<ChunkManager>.Value;
			if (cm != null)
			{
				foreach (ChunkPoint cp in cm.GetAllChunkPointsList())
				{
					if (cp.rule != null && cp.rule.name.IndexOf("FloatingRaft", StringComparison.OrdinalIgnoreCase) >= 0) continue;
					float d = Flat(p - cp.worldPosition);
					if (d < (cp.rule != null ? cp.rule.collisionOverlapRadius : 150f) + NearLand) return "Raft's " + (cp.rule != null ? cp.rule.name : "island") + " " + d.ToString("F0") + " m away";
				}
			}
			return null;
		}

		static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

		/// <summary>Host: an arm now (the command, a due day or storm). Returns what happened.</summary>
		public static string Bring()
		{
			if (!Raft_Network.IsHost) return "Only the host can bring the kraken";
			if (!On) return "the world option Kraken arm is off";
			if (arm != null) return "an arm is out already";
			var grid = StormSeas.Grid(StormSeas.Foundations());
			if (grid.Count < MinFloors) return "the raft has " + grid.Count + " floors; the kraken leaves rafts under " + MinFloors + " alone";
			int number = ++nextNumber;
			int es = WorldOptions.Mix(WorldOptions.Seed, Today * 1000 + number, SaltEvent);
			var edges = grid.Where(kv => StormSeas.Sides.Any(s => !grid.ContainsKey(kv.Key + s))).OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).ToList();
			var keep = edges.Where(kv => StormSeas.KeepsTogether(grid, kv.Key)).ToList();
			if (keep.Count > 0) edges = keep;
			var pick = edges[(int)((uint)WorldOptions.Mix(es, 0, SaltEdge) % (uint)edges.Count)];
			var open = Enumerable.Range(0, 4).Where(i => !grid.ContainsKey(pick.Key + StormSeas.Sides[i])).ToList();
			int side = open[(int)((uint)WorldOptions.Mix(es, 1, SaltEdge) % (uint)open.Count)];
			float stay = TestStay.HasValue ? TestStay.Value : StaySeconds;
			var msg = RiseMessage(number, pick.Value.ObjectIndex, side, es, HitsNeeded, stay, 0, 0f);
			if (!Rise(msg)) return "the arm could not be made";
			Brought++;
			IslandNetwork.SendToEveryone(msg);
			return "arm " + number + " grabs the floor at " + pick.Key + " from side " + side + " (" + edges.Count + " edge floors to pick from)";
		}

		/// <summary>The arm's colouring from its event seed (the same on every PC): 0 purple (3 in 4), 1 greenish, 2 bluish.</summary>
		public static int Colouring(int seed)
		{
			double r = WorldOptions.Unit(seed, 0, SaltColour);
			return r < 0.75 ? 0 : r < 0.875 ? 1 : 2;
		}

		public static readonly string[] ColouringNames = { "purple", "green", "blue" };

		static void Colours(int colouring, out Color body, out Color band, out Color sucker)
		{
			switch (colouring)
			{
				case 1: body = new Color(0.40f, 0.60f, 0.38f); band = new Color(0.22f, 0.42f, 0.26f); sucker = new Color(0.85f, 0.92f, 0.70f); break;
				case 2: body = new Color(0.36f, 0.48f, 0.68f); band = new Color(0.20f, 0.30f, 0.55f); sucker = new Color(0.75f, 0.85f, 0.95f); break;
				default: body = new Color(0.62f, 0.38f, 0.62f); band = new Color(0.55f, 0.22f, 0.32f); sucker = new Color(0.95f, 0.72f, 0.75f); break;
			}
		}

		static IslandNetMessage RiseMessage(int number, uint block, int side, int seed, int need, float stay, int hits, float elapsed)
		{
			return new IslandNetMessage { Kind = IslandNetMessage.Kraken, Name = "rise", Index = number, Count = hits, Data = block + ";" + side + ";" + seed + ";" + need + ";" + stay.ToString(System.Globalization.CultureInfo.InvariantCulture), Offsets = new[] { elapsed } };
		}

		/// <summary>The join reply's part: the arm out now, or null.</summary>
		public static IslandNetMessage Message()
		{
			if (!Raft_Network.IsHost || arm == null || arm.Phase >= 2) return null;
			return RiseMessage(arm.Number, arm.BlockIndex, arm.SideIndex, arm.Seed, arm.Need, arm.Stay, arm.Hits, Time.time - arm.Started);
		}

		/// <summary>Host: the arm goes quietly (the option off, the floor gone, the command).</summary>
		public static void Away(string why)
		{
			if (arm == null) return;
			Log("away: " + why);
			LastEnd = "sent away (" + why + ")";
			int n = arm.Number;
			arm.Sink(false);
			if (Raft_Network.IsHost) IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Kraken, Name = "away", Index = n });
		}

		/// <summary>The floors the arm takes when it tears: the grabbed one and each side neighbour whose seeded roll says so.</summary>
		public static List<Block> Victims(Block grabbed, int eventSeed)
		{
			var list = new List<Block>();
			if (grabbed == null) return list;
			list.Add(grabbed);
			var grid = StormSeas.Grid(StormSeas.Foundations());
			var at = grid.FirstOrDefault(kv => kv.Value == grabbed);
			if (at.Value == null) return list;
			for (int i = 0; i < 4; i++)
			{
				Block n;
				if (grid.TryGetValue(at.Key + StormSeas.Sides[i], out n) && n != null && NeighbourGoes(eventSeed, i)) list.Add(n);
			}
			return list;
		}

		/// <summary>Host: time is up - the arm tears its floor (and maybe the ones beside it) away.</summary>
		public static void TearNow()
		{
			if (!Raft_Network.IsHost || arm == null || arm.Phase >= 2) return;
			List<Block> victims = Victims(arm.Block, arm.Seed);
			arm.Tear();
			int n = 0;
			foreach (Block b in victims)
			{
				if (b == null) continue;
				try { BlockCreator.RemoveBlockNetwork(b, null, true); n++; }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [kraken] removing a floor: " + e.Message); }
			}
			Torn++; LastTornCount = n;
			LastEnd = "tore " + n + " floor" + (n == 1 ? "" : "s");
			Log("arm " + arm.Number + " tore " + n + " floor(s) away");
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Kraken, Name = "tear", Index = arm.Number, Count = n });
			TornBanner(n);
		}

		static void TornBanner(int n)
		{
			IslandInfo.Show("The kraken tore the raft", "", "The arm dragged " + (n == 1 ? "a floor" : n + " floors") + " down into the deep, with all that stood on " + (n == 1 ? "it" : "them") + ".");
		}

		#endregion

		#region Hits

		static float lastHit = -10f;

		/// <summary>The local player swung a melee weapon from origin along dir: a hit if the swing reaches the arm. Counts it
		/// (host) or tells the host (a player).</summary>
		public static bool TryHit(Vector3 origin, Vector3 dir, float range)
		{
			if (arm == null || arm.Phase != 1 || arm.Released) return false;
			if (Time.time - lastHit < 0.3f) return false;
			if (!arm.Reaches(origin, dir.normalized, range)) return false;
			lastHit = Time.time;
			arm.Flinch();
			if (Raft_Network.IsHost) HostHit(arm.Number);
			else IslandNetwork.SendToTheHost(new IslandNetMessage { Kind = IslandNetMessage.Kraken, Name = "hit", Index = arm.Number });
			return true;
		}

		static void HostHit(int number)
		{
			if (arm == null || arm.Number != number || arm.Phase != 1 || arm.Released) return;
			arm.Hits++;
			arm.Struck();
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Kraken, Name = "hits", Index = number, Count = arm.Hits });
			if (arm.Hits >= arm.Need)
			{
				Beaten++;
				LastEnd = "beaten off with " + arm.Hits + " hits";
				Log("arm " + number + " beaten off");
				arm.Sink(true);
				IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Kraken, Name = "let", Index = number });
				LetBanner();
			}
		}

		static void LetBanner() { IslandInfo.Show("The kraken let go", "", "The arm slides back into the deep. The raft is whole."); }

		#endregion

		#region Messages

		public static void OnMessage(IslandNetMessage msg)
		{
			if (msg == null) return;
			if (Raft_Network.IsHost)
			{
				if (msg.Name == "hit") HostHit(msg.Index);
				return;
			}
			switch (msg.Name)
			{
				case "rise": pending = null; Rise(msg); break;
				case "hits": if (arm != null && arm.Number == msg.Index && msg.Count > arm.Hits) { arm.Hits = msg.Count; arm.Struck(); } break;
				case "let": if (arm != null && arm.Number == msg.Index) { arm.Sink(true); LetBanner(); } break;
				case "tear": Torn++; LastTornCount = msg.Count; if (arm != null && arm.Number == msg.Index) arm.Tear(); TornBanner(msg.Count); break;
				case "away": if (arm != null && arm.Number == msg.Index) arm.Sink(false); break;
			}
		}

		/// <summary>Makes the arm a "rise" message says (host and players). A player keeps it waiting while the floor hasn't arrived.</summary>
		static bool Rise(IslandNetMessage msg)
		{
			string[] f = (msg.Data ?? "").Split(';');
			uint idx; int side, seed, need; float stay;
			if (f.Length < 5 || !uint.TryParse(f[0], out idx) || !int.TryParse(f[1], out side) || !int.TryParse(f[2], out seed) || !int.TryParse(f[3], out need)
				|| !float.TryParse(f[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out stay)) return false;
			List<Block> placed = BlockCreator.GetPlacedBlocks();
			Block b = placed != null ? placed.FirstOrDefault(x => x != null && x.ObjectIndex == idx) : null;
			if (b == null)
			{
				if (pending == null || pending.Index != msg.Index) pendingSince = Time.time;
				pending = msg;
				return false;
			}
			if (arm != null) UnityEngine.Object.Destroy(arm.gameObject);
			float elapsed = msg.Offsets != null && msg.Offsets.Length > 0 ? msg.Offsets[0] : 0f;
			arm = Arm.Make(b, side & 3, seed, msg.Index, need, stay, msg.Count, elapsed);
			if (arm == null) return false;
			if (elapsed < RiseSeconds)
			{
				IslandInfo.Show("A kraken arm!", "", "A giant tentacle has grabbed the edge of the raft. Hit it " + need + " times with a melee weapon or spear within " + Mathf.RoundToInt(stay) + " seconds, or it tears the floor away - maybe more than one.");
				Sounds.Play(Sounds.Whale, "er_shootWater", arm.WaterPoint);
			}
			Log("arm " + msg.Index + " on floor " + idx + " from side " + side + ", " + arm.Extras + " more arms, " + msg.Count + "/" + need + " hits, " + elapsed.ToString("0") + " s in");
			return true;
		}

		#endregion

		[ConsoleCommand(name: "Kraken", docs: "The world option Kraken arm: Kraken = whether an arm is out and the next days that bring one; Kraken bring = (host, cheat) an arm grabs the raft now; Kraken away = (host) the arm out goes now; Kraken tear = (host, cheat) the arm out tears now")]
		public static void KrakenCommand(string[] args)
		{
			string a0 = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (a0 == "bring" || a0 == "away" || a0 == "tear")
			{
				if (!Raft_Network.IsHost) { Debug.Log("[CUSTOM ISLANDS] Kraken arm: Only the host can do that"); return; }
				if (a0 == "bring") Debug.Log("[CUSTOM ISLANDS] Kraken arm: " + Bring());
				else if (arm == null || arm.Phase >= 2) Debug.Log("[CUSTOM ISLANDS] Kraken arm: none out");
				else if (a0 == "away") Away("the command");
				else TearNow();
				return;
			}
			int seed = WorldOptions.Seed, day = Today;
			int[] next = Enumerable.Range(day, 400).Where(d => Rolls(seed, d)).Take(5).ToArray();
			string now = arm == null || arm.Phase >= 2 ? "none out" : "arm " + arm.Number + " holds floor " + arm.BlockIndex + " (" + arm.Hits + "/" + arm.Need + " hits, " + (arm.Released ? "letting go" : Mathf.Max(0, Mathf.RoundToInt(arm.Stay - (Time.time - arm.Started))) + " s left") + ", " + arm.Extras + " more arms)";
			Debug.Log("[CUSTOM ISLANDS] Kraken arm: " + (On ? "on" : "off") + ", day " + day + ", " + now + ", next days with one: " + (next.Length > 0 ? string.Join(" ", next.Select(d => d.ToString()).ToArray()) : "-") +
				(dueAt > 0 ? ", due in " + Mathf.RoundToInt(dueAt - Time.time) + " s" : "") + ", brought " + Brought + ", beaten " + Beaten + ", torn " + Torn + (LastEnd.Length > 0 ? ", last: " + LastEnd : ""));
		}

		/// <summary>Raft's own sounds, read from its prefabs.</summary>
		static class Sounds
		{
			public static UnityEngine.Object Whale { get { return Resources.FindObjectsOfTypeAll<AI_NetworkBehaviour_Whale>().FirstOrDefault(); } }
			public static UnityEngine.Object Shark { get { return Resources.FindObjectsOfTypeAll<AI_NetworkBehavior_Shark>().FirstOrDefault(); } }
			public static UnityEngine.Object Manager { get { return UnityEngine.Object.FindObjectOfType<SoundManager>(); } }

			public static void Play(UnityEngine.Object source, string field, Vector3 at)
			{
				try
				{
					if (source == null) return;
					FMODUnity.EventReference er = Traverse.Create(source).Field(field).GetValue<FMODUnity.EventReference>();
					FMODUnity.RuntimeManager.PlayOneShot(er, at);
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [kraken] sound " + field + ": " + e.Message); }
			}
		}

		/// <summary>One tentacle: tapered capsules along a Catmull-Rom curve through Ctrl (pivot space), rising from Ctrl
		/// moved Sunk metres down, swaying more toward the tip.</summary>
		public class Tentacle : MonoBehaviour
		{
			public Vector3[] Ctrl;
			public Vector3 Side, Out;
			/// <summary>0 = all under the sea, 1 = in place.</summary>
			public float Up;
			public float Thick = 1f, Sway = 1f, Phase0;
			/// <summary>The curve's t where it comes out of the water (the taper is measured from there).</summary>
			public float Surface = 0.3f;
			public Vector3 Push;
			public int N = 40;
			public Vector3[] Pts;
			readonly List<Transform> segs = new List<Transform>();
			readonly List<Transform> suckers = new List<Transform>();
			readonly List<int> suckerAt = new List<int>();
			public Material Body, Band, Pale;
			Color bodyColour, bandColour;

			public float Radius(float t)
			{
				float r = t < Surface ? Mathf.Lerp(1.2f, 0.88f, t / Surface) : Mathf.Lerp(0.88f, 0.06f, Mathf.Pow((t - Surface) / (1f - Surface), 0.85f));
				return r * Thick;
			}

			Vector3 At(float t)
			{
				int n = Ctrl.Length - 1;
				float f = Mathf.Clamp01(t) * n;
				int i = Mathf.Min((int)f, n - 1);
				float k = f - i;
				Vector3 p0 = Ctrl[Mathf.Max(i - 1, 0)], p1 = Ctrl[i], p2 = Ctrl[i + 1], p3 = Ctrl[Mathf.Min(i + 2, n)];
				return 0.5f * (2f * p1 + (-p0 + p2) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k * k + (-p0 + 3f * p1 - 3f * p2 + p3) * k * k * k);
			}

			void Points()
			{
				if (Pts == null || Pts.Length != N + 1) Pts = new Vector3[N + 1];
				float T = Time.time + Phase0;
				for (int i = 0; i <= N; i++)
				{
					float t = i / (float)N;
					float w = Mathf.Clamp01((t - Surface * 0.5f) / (1f - Surface * 0.5f)); w *= w;
					Vector3 p = At(t) + (Side * (Mathf.Sin(T * 1.1f + t * 5f) * 0.45f) + Vector3.up * (Mathf.Sin(T * 0.8f + t * 4f) * 0.25f) + Out * (Mathf.Sin(T * 0.6f + t * 3f) * 0.2f)) * (w * Sway) + Push * w;
					// Rising: the base first, the tip last, from deep down and a little out to sea
					float k = Mathf.Clamp01(Up * 1.8f - t * 0.8f);
					k = k * k * (3f - 2f * k);
					Pts[i] = Vector3.Lerp(p - Vector3.up * 16f + Out * 3f, p, k);
				}
			}

			public void Build(Material skin, Color tint, Color band, Color suckerTint)
			{
				Body = new Material(skin); Tint(Body, tint, true); bodyColour = Colour(Body);
				Band = new Material(skin); Tint(Band, band, true); bandColour = Colour(Band);
				Pale = new Material(skin); Tint(Pale, suckerTint, false);
				Material pale = Pale;
				for (int i = 0; i < N; i++)
				{
					GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
					UnityEngine.Object.Destroy(g.GetComponent<Collider>());
					g.name = "seg" + i;
					g.transform.SetParent(transform, false);
					g.GetComponent<Renderer>().sharedMaterial = i % 3 == 2 ? Band : Body;
					segs.Add(g.transform);
					if (i / (float)N > Surface + 0.08f && i % 2 == 0)
					{
						GameObject c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
						UnityEngine.Object.Destroy(c.GetComponent<Collider>());
						c.name = "sucker" + i;
						c.transform.SetParent(transform, false);
						c.GetComponent<Renderer>().sharedMaterial = pale;
						suckers.Add(c.transform); suckerAt.Add(i);
					}
				}
				Update();
			}

			/// <summary>Tests: another colouring on an arm already out (Kraken.Colouring), for pictures of each.</summary>
			public void Recolour(Material skin, int colouring)
			{
				Color tint, band, sucker;
				Colours(colouring, out tint, out band, out sucker);
				Color c = Colour(skin);
				bodyColour = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b); SetColour(Body, bodyColour);
				bandColour = new Color(c.r * band.r, c.g * band.g, c.b * band.b); SetColour(Band, bandColour);
				SetColour(Pale, sucker);
			}

			/// <summary>0..1: the skin goes toward an angry red.</summary>
			public void Redden(float k)
			{
				Color red = new Color(0.75f, 0.12f, 0.12f);
				SetColour(Body, Color.Lerp(bodyColour, red * bodyColour.maxColorComponent * 1.3f, k * 0.7f));
				SetColour(Band, Color.Lerp(bandColour, red * bandColour.maxColorComponent * 1.3f, k * 0.7f));
			}

			public void Update()
			{
				if (Ctrl == null || segs.Count == 0) return;
				Points();
				for (int i = 0; i < segs.Count; i++)
				{
					Vector3 a = Pts[i], b = Pts[i + 1], dir = b - a;
					float L = dir.magnitude, r = Radius((i + 0.5f) / N);
					Transform t = segs[i];
					t.localPosition = (a + b) * 0.5f;
					t.localRotation = Quaternion.FromToRotation(Vector3.up, dir / Mathf.Max(L, 0.0001f));
					t.localScale = new Vector3(2f * r, (L + r * 1.6f) * 0.5f, 2f * r);
				}
				for (int k = 0; k < suckers.Count; k++)
				{
					int i = suckerAt[k];
					Vector3 a = Pts[i], b = Pts[i + 1], prev = Pts[Mathf.Max(i - 1, 0)];
					Vector3 n = (b - a) - (a - prev);
					Vector3 tangent = (b - a).normalized;
					n = n - tangent * Vector3.Dot(n, tangent);
					if (n.sqrMagnitude < 1e-6f) n = -Out;
					n.Normalize();
					float r = Radius((i + 0.5f) / N);
					Transform t = suckers[k];
					t.localPosition = (a + b) * 0.5f + n * r * 0.82f;
					t.localRotation = Quaternion.FromToRotation(Vector3.up, n);
					t.localScale = new Vector3(r * 0.95f, r * 0.3f, r * 0.95f);
				}
			}

			/// <summary>Whether a swing from origin (world) along dir reaches a part of this tentacle above the water.</summary>
			public bool Reaches(Vector3 origin, Vector3 dir, float range)
			{
				if (Pts == null) return false;
				float scale = transform.lossyScale.x;
				for (int i = 0; i < N; i++)
				{
					Vector3 c = transform.TransformPoint((Pts[i] + Pts[i + 1]) * 0.5f);
					if (c.y < -0.4f) continue;
					float r = Radius((i + 0.5f) / N) * scale;
					Vector3 v = c - origin;
					float along = Vector3.Dot(v, dir);
					if (along < -r || along > range + r) continue;
					if ((v - dir * along).magnitude <= r + 0.35f) return true;
				}
				return false;
			}

			static Color Colour(Material m) { foreach (string p in Props) if (m.HasProperty(p)) return m.GetColor(p); return Color.white; }
			static void SetColour(Material m, Color c) { if (m == null) return; foreach (string p in Props) if (m.HasProperty(p)) { Color was = m.GetColor(p); m.SetColor(p, new Color(c.r, c.g, c.b, was.a)); } }
		}

		static readonly string[] Props = { "_Color", "_BaseColor", "_TintColor", "_MainColor", "_Albedo" };

		internal static void Tint(Material m, Color c, bool multiply)
		{
			foreach (string p in Props)
				if (m.HasProperty(p))
				{
					Color was = m.GetColor(p);
					m.SetColor(p, multiply ? new Color(was.r * c.r, was.g * c.g, was.b * c.b, was.a) : new Color(c.r, c.g, c.b, was.a));
				}
		}

		static Material skinCache;

		/// <summary>Raft's shark material (a live shark's, else any loaded material named shark with a texture), or a plain one.</summary>
		internal static Material Skin()
		{
			if (skinCache != null) return skinCache;
			Material m = null;
			try
			{
				AI_NetworkBehavior_Shark shark = UnityEngine.Object.FindObjectOfType<AI_NetworkBehavior_Shark>();
				if (shark != null)
				{
					Renderer r = shark.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(x => x.bounds.size.sqrMagnitude).FirstOrDefault(x => x.sharedMaterial != null);
					if (r != null) m = r.sharedMaterial;
				}
				if (m == null) m = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x != null && x.name.IndexOf("shark", StringComparison.OrdinalIgnoreCase) >= 0 && x.name.IndexOf("cage", StringComparison.OrdinalIgnoreCase) < 0 && x.mainTexture != null);
			}
			catch { }
			if (m == null) { Shader std = Shader.Find("Standard"); if (std == null) return null; m = new Material(std) { color = new Color(0.5f, 0.45f, 0.5f) }; }
			skinCache = new Material(m);
			return skinCache;
		}

		/// <summary>The kraken out now: the grabbing arm (a Tentacle over the floor) and 1-3 look-only arms off the edge, under
		/// one root on the raft's pivot (Raft's block removal never takes it). Phase 0 = rising, 1 = holding, 2 = sinking.</summary>
		public class Arm : MonoBehaviour
		{
			public int Number, Seed, SideIndex, Need, Hits, Phase, Extras;
			/// <summary>Health this machine's player lost to this sighting's swipes.</summary>
			float taken;
			public float Stay, Started;
			public uint BlockIndex;
			public Block Block;
			public bool Released, Tore;
			public Tentacle Main;
			public readonly List<Tentacle> Others = new List<Tentacle>();
			public Vector3 WaterPoint { get { return Main != null ? transform.TransformPoint(Main.Ctrl[4]) : transform.position; } }
			GameObject floorCopy;
			Vector3 floorFromTip;
			float sinkStart = -1f, flinchAt = -10f;
			Vector3 u;
			// The free arms' swipes, per arm in Others: the cycle seen, where it slams (arm space; null = nobody near), done
			readonly List<int> swipeCycle = new List<int>();
			readonly List<Vector3?> swipeAt = new List<Vector3?>();
			readonly List<bool> swipeDone = new List<bool>();
			readonly List<Vector3> swipePush = new List<Vector3>();

			public static Arm Make(Block b, int side, int seed, int number, int need, float stay, int hits, float elapsed)
			{
				Transform pivot = b.transform.parent;
				if (pivot == null) return null;
				Material skin = Skin();
				if (skin == null) return null;
				var root = new GameObject("CI_KrakenArm");
				root.transform.SetParent(pivot, false);
				var a = root.AddComponent<Arm>();
				a.Block = b; a.BlockIndex = b.ObjectIndex; a.Number = number; a.Seed = seed; a.SideIndex = side; a.Need = Mathf.Max(1, need); a.Stay = stay; a.Hits = hits;
				a.Started = Time.time - elapsed;
				a.Build(skin);
				a.Update();
				return a;
			}

			void Build(Material skin)
			{
				Vector2Int sv = StormSeas.Sides[SideIndex];
				u = new Vector3(sv.x, 0f, sv.y);
				Vector3 s = Vector3.Cross(Vector3.up, u), up = Vector3.up;
				Vector3 e = Block.transform.localPosition;
				Transform pivot = transform.parent;
				float deck = 0.35f;
				try { Renderer[] er = Block.GetComponentsInChildren<Renderer>(); if (er.Length > 0) deck = er.Max(r => pivot.InverseTransformPoint(r.bounds.max).y) - e.y; } catch { }
				Vector3 E = e + u * 0.75f, D = E + up * deck;
				Color body, band, sucker;
				Colours(Colouring(Seed), out body, out band, out sucker);

				Main = NewTentacle("main");
				Main.Ctrl = new[]
				{
					E + u * 3.6f - up * Depth + s * 0.6f,
					E + u * 3.0f - up * (Depth * 0.64f) + s * 0.3f,
					E + u * 2.4f - up * 5.5f,
					E + u * 2.2f - up * 1.5f + s * 0.1f,
					E + u * 2.3f + up * 1.2f + s * 0.2f,
					E + u * 1.9f + up * 3.9f + s * 0.5f,
					E + u * 0.8f + up * 5.2f + s * 0.4f,
					E + u * -0.3f + up * 4.4f + s * 0.1f,
					D + u * -0.9f + up * 2.4f,
					D + u * -1.2f + up * 0.8f - s * 0.1f,
					D + u * -1.35f + up * 0.12f - s * 0.25f,
					D + u * -0.85f + up * 0.1f - s * 0.45f,
					D + u * -0.55f + up * 0.35f - s * 0.3f,
				};
				Main.Surface = 3.55f / 12f; Main.N = 44; Main.Side = s; Main.Out = u;
				Main.Build(skin, body, band, sucker);
				Main.Redden(Hits / (float)Need);

				// 1-3 more arms off the edge, 3-8 m out, spread along it; none where the raft is
				var grid = StormSeas.Grid(StormSeas.Foundations());
				Vector3 o = grid.Count > 0 ? grid.First().Value.transform.localPosition : e;
				Vector2Int home = StormSeas.CellOf(e, o);
				int want = 1 + (int)((uint)WorldOptions.Mix(Seed, 0, SaltExtra) % 3u);
				float[] lanes = { -7f, -3.5f, 3.5f, 7f, -10f, 10f };
				int start = (int)((uint)WorldOptions.Mix(Seed, 1, SaltExtra) % 4u);
				for (int j = 0; j < lanes.Length && Extras < want; j++)
				{
					int k = j < 4 ? (start + j) % 4 : j;
					float dist = 3f + 5f * (float)WorldOptions.Unit(Seed, 10 + j, SaltExtra);
					float lat = lanes[k] + (float)(WorldOptions.Unit(Seed, 20 + j, SaltExtra) - 0.5) * 1.5f;
					Vector3 P = E + u * dist + s * lat;
					if (NearRaft(grid, o, P, home)) continue;
					float h = 3.5f + 3f * (float)WorldOptions.Unit(Seed, 30 + j, SaltExtra);
					float ang = (float)WorldOptions.Unit(Seed, 40 + j, SaltExtra) * Mathf.PI * 2f;
					Vector3 c = (u * Mathf.Cos(ang) + s * Mathf.Sin(ang)).normalized;
					Tentacle t = NewTentacle("extra" + Extras);
					t.Ctrl = new[]
					{
						P + u * 1.5f - up * Depth,
						P + u * 1.0f - up * (Depth * 0.6f),
						P + u * 0.5f - up * 5f,
						P - up * 1.5f,
						P + up * h * 0.35f + c * 0.3f,
						P + up * h * 0.7f + c * 0.6f,
						P + up * h + c * 0.4f - u * 0.3f,
						P + up * h * 1.05f - c * 0.6f - u * 0.8f,
						P + up * h * 0.8f - c * 1.1f - u * 0.9f,
						P + up * h * 0.62f - c * 0.7f - u * 0.5f,
					};
					t.Surface = 3.4f / 9f; t.N = 32;
					t.Thick = 0.6f + 0.25f * (float)WorldOptions.Unit(Seed, 50 + j, SaltExtra);
					t.Sway = 1.6f; t.Phase0 = 7f * (float)WorldOptions.Unit(Seed, 60 + j, SaltExtra);
					t.Side = Vector3.Cross(up, c); t.Out = c;
					t.Build(skin, body, band, sucker);
					Others.Add(t);
					swipeCycle.Add(-1); swipeAt.Add(null); swipeDone.Add(false); swipePush.Add(Vector3.zero);
					Extras++;
				}

				// A look-only copy of the floor, shown when the arm tears it away
				floorCopy = new GameObject("CI_KrakenFloor");
				floorCopy.transform.SetParent(transform, false);
				floorCopy.transform.position = Block.transform.position;
				floorCopy.transform.rotation = Block.transform.rotation;
				foreach (MeshRenderer mr in Block.GetComponentsInChildren<MeshRenderer>())
				{
					MeshFilter mf = mr.GetComponent<MeshFilter>();
					if (mf == null || mf.sharedMesh == null || !mr.enabled) continue;
					var g = new GameObject(mr.name);
					g.transform.SetParent(floorCopy.transform, false);
					g.transform.position = mr.transform.position; g.transform.rotation = mr.transform.rotation;
					g.transform.localScale = Vector3.Scale(mr.transform.lossyScale, new Vector3(1f / Mathf.Max(floorCopy.transform.lossyScale.x, 0.001f), 1f / Mathf.Max(floorCopy.transform.lossyScale.y, 0.001f), 1f / Mathf.Max(floorCopy.transform.lossyScale.z, 0.001f)));
					g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
					g.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
				}
				floorCopy.SetActive(false);
			}

			static bool NearRaft(Dictionary<Vector2Int, Block> grid, Vector3 o, Vector3 p, Vector2Int home)
			{
				Vector2Int c = StormSeas.CellOf(p, o);
				for (int dx = -2; dx <= 2; dx++)
					for (int dz = -2; dz <= 2; dz++)
						if (grid.ContainsKey(c + new Vector2Int(dx, dz))) return true;
				return false;
			}

			Tentacle NewTentacle(string name)
			{
				var g = new GameObject(name);
				g.transform.SetParent(transform, false);
				return g.AddComponent<Tentacle>();
			}

			IEnumerable<Tentacle> All() { if (Main != null) yield return Main; foreach (Tentacle t in Others) yield return t; }

			public bool Reaches(Vector3 origin, Vector3 dir, float range) { return Main != null && Main.Reaches(origin, dir, range); }

			public void Flinch() { flinchAt = Time.time; }

			/// <summary>A hit counted (host or a player hearing of it): flinch, redden, a thump.</summary>
			public void Struck()
			{
				flinchAt = Time.time;
				if (Main != null) Main.Redden(Hits / (float)Need);
				Sounds.Play(Sounds.Shark, "er_biteRaft", transform.TransformPoint(Main != null ? Main.Ctrl[6] : Vector3.zero));
			}

			/// <summary>Lets go and sinks; beaten = with a splash.</summary>
			public void Sink(bool beaten)
			{
				if (Phase >= 2) return;
				Released = true; Phase = 2; sinkStart = Time.time;
				if (beaten) Splash(0.9f);
			}

			/// <summary>Tears the floor: the copy is dragged down with the tip, and all the arms sink.</summary>
			public void Tear()
			{
				if (Tore) return;
				Tore = true;
				if (floorCopy != null && Main != null && Main.Pts != null)
				{
					floorCopy.SetActive(true);
					floorFromTip = floorCopy.transform.localPosition - Main.Pts[Main.N];
				}
				Sounds.Play(Sounds.Manager, "er_BlockBreak", transform.TransformPoint(Main != null ? Main.Ctrl[10] : Vector3.zero));
				Splash(1.2f);
				Sink(false);
			}

			void Splash(float k)
			{
				try
				{
					Vector3 w = WaterPoint; w.y = 0f;
					WhaleBreach.Splash(w, k);
					foreach (Tentacle t in Others) { Vector3 p = transform.TransformPoint(t.Ctrl[3]); p.y = 0f; WhaleBreach.Splash(p, k * 0.6f); }
				}
				catch { }
			}

			/// <summary>How far free arm t is pushed by its swipe now (zero between swipes; tests).</summary>
			public Vector3 SwipePushOf(Tentacle t) { int i = Others.IndexOf(t); return i >= 0 ? swipePush[i] : Vector3.zero; }

			static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

			/// <summary>Free arm i's swipe: every SwipeEvery s (a seeded offset per arm) while the arm holds, it picks the
			/// nearest player within SwipeReach, rears back for 1 s, slams where that player stood (0.3 s) and settles again.
			/// Every machine works it out the same way and hurts only its own player, if still within SwipeRadius.</summary>
			void Swipe(int i)
			{
				Tentacle t = Others[i];
				float ts = Time.time - Started - RiseSeconds - (1.5f + 4f * (float)WorldOptions.Unit(Seed, 70 + i, SaltExtra));
				if (Phase != 1 || Released || ts < 0f) { swipePush[i] = Vector3.zero; return; }
				int c = (int)(ts / SwipeEvery);
				float q = ts - c * SwipeEvery;
				if (c != swipeCycle[i]) { swipeCycle[i] = c; swipeDone[i] = false; swipeAt[i] = Target(t); }
				if (!swipeAt[i].HasValue || q > 2.2f) { swipePush[i] = Vector3.zero; return; }
				Vector3 to = swipeAt[i].Value, tip = t.Ctrl[t.Ctrl.Length - 1];
				Vector3 dir = to - t.Ctrl[3]; dir.y = 0f;
				dir = dir.sqrMagnitude > 0.01f ? dir.normalized : -u;
				Vector3 back = -dir * 1.2f + Vector3.up * 1.3f;
				Vector3 slam = Vector3.ClampMagnitude(to + Vector3.up * 0.6f - tip, 9f);
				if (q < 1f) swipePush[i] = back * Smooth(q);
				else if (q < 1.3f)
				{
					swipePush[i] = Vector3.Lerp(back, slam, Smooth((q - 1f) / 0.3f));
					if (!swipeDone[i] && q >= 1.22f) { swipeDone[i] = true; Slam(to); }
				}
				else swipePush[i] = Vector3.Lerp(slam, Vector3.zero, Smooth((q - 1.3f) / 0.9f));
			}

			/// <summary>Where free arm t slams this time (arm space): the nearest living player within SwipeReach of where it
			/// comes out of the sea, or null.</summary>
			Vector3? Target(Tentacle t)
			{
				Vector3 from = transform.TransformPoint(t.Ctrl[3]);
				Network_Player best = null; float bestD = SwipeReach;
				try
				{
					foreach (Network_Player p in UnityEngine.Object.FindObjectsOfType<Network_Player>())
					{
						if (p == null || p.Stats == null || p.Stats.IsDead) continue;
						Vector3 d = p.transform.position - from; d.y = 0f;
						if (d.magnitude < bestD) { bestD = d.magnitude; best = p; }
					}
				}
				catch { }
				if (best == null) return null;
				return transform.InverseTransformPoint(best.transform.position);
			}

			/// <summary>The slam lands at to (arm space): a thump, and this machine's player takes SwipeDamage if still near.</summary>
			void Slam(Vector3 to)
			{
				Vector3 w = transform.TransformPoint(to);
				Sounds.Play(Sounds.Shark, "er_biteRaft", w);
				try
				{
					Network_Player me = RAPI.GetLocalPlayer();
					if (me == null || me.Stats == null || me.Stats.IsDead) return;
					Vector3 d = me.transform.position - w;
					if (Mathf.Abs(d.y) > 2.5f) return;
					d.y = 0f;
					if (d.magnitude > SwipeRadius) return;
					// (a little only: never more than MaxTaken in one sighting, never below KeepHealth - a player can always win)
					float hit = SwipeHit(Colouring(Seed), taken, me.Stats.stat_health.Value);
					if (hit <= 0f) return;
					me.Stats.Damage(hit, me.transform.position + Vector3.up, Vector3.up, EntityType.Enemy, true, null);
					taken += hit;
					SwipesLanded++;
					Log("a free arm's swipe hit " + me.name + " for " + hit.ToString("F1") + " (" + taken.ToString("F1") + " this sighting)");
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [kraken] Slam: " + e.Message); }
			}

			public void Update()
			{
				float up;
				if (Phase < 2)
				{
					float a = (Time.time - Started) / RiseSeconds;
					up = Mathf.Clamp01(a);
					if (Phase == 0 && a >= 1f) { Phase = 1; Sounds.Play(Sounds.Shark, "er_biteRaft", transform.TransformPoint(Main.Ctrl[10])); }
				}
				else
				{
					float a = (Time.time - sinkStart) / (SinkSeconds * (Tore ? 1.4f : 1f));
					up = 1f - Mathf.Clamp01(a);
					if (a >= 1.05f)
					{
						if (arm == this) arm = null;
						Destroy(gameObject);
						return;
					}
				}
				float fl = Mathf.Clamp01(1f - (Time.time - flinchAt) / 0.5f);
				for (int i = 0; i < Others.Count; i++) Swipe(i);
				foreach (Tentacle t in All())
				{
					t.Up = up;
					int i = Others.IndexOf(t);
					t.Push = t == Main ? (u * 0.5f + Vector3.up * 0.35f) * Mathf.Sin(fl * Mathf.PI) : i >= 0 ? swipePush[i] : Vector3.zero;
				}
				if (Tore && floorCopy != null && Main != null && Main.Pts != null)
				{
					Main.Update();
					floorCopy.transform.localPosition = Main.Pts[Main.N] + floorFromTip;
					floorCopy.transform.Rotate(u, 40f * Time.deltaTime, Space.Self);
				}
			}
		}
	}

	/// <summary>A local player's melee swing (weapons and spears) that reaches the kraken arm counts as a hit.</summary>
	[HarmonyPatch(typeof(MeleeWeapon), "OnMeleeHit")]
	static class KrakenMeleePatch
	{
		static void Prefix(MeleeWeapon __instance)
		{
			try
			{
				if (Kraken.Current == null || Kraken.Current.Phase != 1) return;
				Network_Player p = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
				if (p == null || !p.IsLocalPlayer || p.CameraTransform == null) return;
				float range = 2f;
				try { range = Traverse.Create(__instance).Field("attackRange").GetValue<float>(); } catch { }
				Kraken.TryHit(p.CameraTransform.position, p.CameraTransform.forward, range + 1.5f);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [kraken] hit: " + e.Message); }
		}
	}
}
