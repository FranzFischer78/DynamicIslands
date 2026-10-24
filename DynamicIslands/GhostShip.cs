using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Ghost ship" (WorldOptions.GhostShip, TODO 4f): on about one night in four a glowing wreck comes up
	/// beside the raft soon after dark (the map type "ghostship": a ship's deck of Raft's foundations with plank railings, a
	/// cabin, a mast with a flag, a sail, an anchor and lanterns; chests of good loot; rats and screechers on board). Its
	/// monsters are ghosts: see-through and faintly glowing (every machine dresses them itself). At dawn the ship sinks and is
	/// gone, with whatever is still aboard.
	///   - The host decides: at each nightfall it rolls the night (WorldOptions.Unit: the same for every seed and night) and
	///     brings the ship as any generated island (IslandWorldState + the island messages), so every player gets the same
	///     ship, chests (ContentState + Claims) and monsters, and a player who joins at night gets it too.
	///   - Players asleep when it would come (or the night skipped in bed): no ship that night; a ship out goes at once.
	///   - Nothing new is saved: a ship saved with the world is the world's island; loaded by day it goes at once. A world
	///     loaded at night rolls nothing until the next nightfall.
	/// </summary>
	public static class GhostShip
	{
		public const string TypeName = "ghostship";
		public const double Chance = 0.25;
		/// <summary>Seconds after dark before it comes; how long it keeps trying for room; how long it sinks.</summary>
		public const float AfterDark = 25f, TryFor = 180f, SinkSeconds = 20f, SinkDepth = 14f;
		const int Salt = 313;

		/// <summary>Tests: night (true) or day (false) whatever the sky says; players asleep or not whatever the beds say.</summary>
		internal static bool? TestNight, TestSleeping;
		/// <summary>Tests: the next nightfall brings one, whatever the roll, with no wait.</summary>
		internal static bool TestForce;

		/// <summary>Tests: how many came since the world loaded; what the last nightfall decided.</summary>
		public static int Brought;
		public static string LastDecision = "";

		static bool? wasNight;
		static float darkSince, nextCheck;
		static int nightNo = -1;
		static bool due, seenAtNight;
		static IslandWorldState.Entry sinking;
		static int announcedId = -1;
		static readonly HashSet<int> dressed = new HashSet<int>();

		public static bool On { get { return WorldOptions.On(WorldOptions.GhostShip); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [ghost ship] " + msg); }

		internal static void Reset()
		{
			TestNight = TestSleeping = null; TestForce = false; Brought = 0; LastDecision = "";
			wasNight = null; darkSince = nextCheck = 0f; nightNo = -1; due = seenAtNight = false; sinking = null; announcedId = -1;
			dressed.Clear();
		}

		/// <summary>Whether the night that starts on this day brings a ship (the same on every machine).</summary>
		public static bool Rolls(int seed, int day) { return WorldOptions.Unit(seed, day, Salt) < Chance; }

		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		static bool Night { get { return TestNight.HasValue ? TestNight.Value : NightDanger.IsNight; } }

		public static bool IsShip(string hostName) { return hostName != null && hostName.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase); }

		/// <summary>The night's ship's file name start: gen-ghostship-night-&lt;seed&gt;, not the map type's own gen-ghostship-&lt;seed&gt;, so a
		/// ghost ship an island builder or a quest placed for good is never taken for it (nor sunk at dawn).</summary>
		public static string NamePrefix { get { return CustomIslandSpawner.GeneratedPrefix + TypeName + "-night-"; } }

		/// <summary>The ghost ship in this world (null: none).</summary>
		public static IslandWorldState.Entry Ship { get { return IslandWorldState.Islands.FirstOrDefault(e => IsShip(e.HostName)); } }

		#region Sleep

		static BedManager beds;
		static PropertyInfo slumberingProp;
		static FieldInfo slumberingField;
		static MethodInfo allSleeping;
		static bool looked;

		/// <summary>The players are asleep (Raft's night skip under way, or everyone in bed).</summary>
		public static bool Sleeping
		{
			get
			{
				if (TestSleeping.HasValue) return TestSleeping.Value;
				try
				{
					if (beds == null) beds = ComponentManager<BedManager>.Value ?? UnityEngine.Object.FindObjectOfType<BedManager>();
					if (!looked)
					{
						looked = true;
						slumberingProp = AccessTools.Property(typeof(BedManager), "Slumbering");
						if (slumberingProp == null) slumberingField = AccessTools.Field(typeof(BedManager), "Slumbering") ?? AccessTools.Field(typeof(BedManager), "slumbering");
						allSleeping = AccessTools.Method(typeof(BedManager), "AllPlayersSleeping");
						if (allSleeping != null && allSleeping.GetParameters().Length != 0) allSleeping = null;
					}
					object v = null;
					if (slumberingProp != null) { MethodInfo g = slumberingProp.GetGetMethod(true); v = g.Invoke(g.IsStatic ? null : beds, null); }
					else if (slumberingField != null && (slumberingField.IsStatic || beds != null)) v = slumberingField.GetValue(slumberingField.IsStatic ? null : beds);
					if (v is bool && (bool)v) return true;
					if (allSleeping != null && (allSleeping.IsStatic || beds != null))
					{
						object a = allSleeping.Invoke(allSleeping.IsStatic ? null : beds, null);
						if (a is bool && (bool)a) return true;
					}
				}
				catch (Exception e) { Log("Reading the beds: " + e.Message); }
				return false;
			}
		}

		#endregion

		#region Every couple of seconds

		/// <summary>Every couple of seconds in a world: (host) the nightfall's roll, the ship brought and taken away; (everyone)
		/// the ship's glow, its ghosts and its sinking at dawn.</summary>
		internal static void Tick()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Time.time < nextCheck) return;
			nextCheck = Time.time + 2f;
			bool night = Night, host = Raft_Network.IsHost;
			IslandWorldState.Entry ship = Ship;
			if (ship != null && ship.Id != announcedId && night && ship.Root != null)
			{
				announcedId = ship.Id;
				IslandInfo.Show("A ghost ship", "", "A glowing wreck drifts by in the dark, close to the raft. Its chests hold good loot - but the dead keep watch, and at dawn the ship sinks with everything still aboard.");
			}
			if (ship != null && ship.Root != null && sinking != ship)
			{
				Dress(ship.Root);
				Haunt(ship.Root, ship.Position);
			}
			if (ship != null && night) seenAtNight = true;

			// The ship goes: at dawn it sinks; asleep, the option off, or loaded by day it is gone at once (host)
			if (ship != null && sinking != ship && (!night || Sleeping || (host && !On)))
			{
				bool asleep = Sleeping;
				if (asleep || !seenAtNight || !On) { if (host) Remove(ship, asleep ? "the players slept" : !On ? "the option is off" : "loaded by day"); }
				else
				{
					sinking = ship;
					IslandInfo.Show("The ghost ship sinks", "", "Dawn: the ghost ship slips under the sea, and whatever was left aboard goes down with it.");
					DynamicIslands.instance.StartCoroutine(Sink(ship));
				}
			}

			// Nightfall (host): roll the night
			if (wasNight.HasValue && night && !wasNight.Value)
			{
				darkSince = Time.time;
				nightNo = Today;
				due = On && (TestForce || Rolls(WorldOptions.Seed, nightNo));
				LastDecision = due ? "due" : "no ship tonight";
				if (host && On) Log("Nightfall (day " + nightNo + "): " + LastDecision);
			}
			if (!night) due = false;
			wasNight = night;
			if (!host || !due || !On) return;
			if (Sleeping) { due = false; LastDecision = "the players slept"; Log("No ship tonight: the players are asleep"); return; }
			if (Time.time - darkSince < (TestForce ? 0f : AfterDark)) return;
			if (Time.time - darkSince > TryFor + AfterDark) { due = false; LastDecision = "no room"; Log("No ship tonight: no room near the raft"); return; }
			if (ship != null) { due = false; LastDecision = "one is out"; return; }
			string r = Bring();
			Log(r);
			if (r.StartsWith("Bringing")) { due = false; TestForce = false; LastDecision = "came"; }
			else nextCheck = Time.time + 10f;
		}

		/// <summary>Host: brings a ghost ship beside the raft now (a place a little ahead and to the side, so the raft drifts past it).</summary>
		internal static string Bring()
		{
			if (!Raft_Network.IsHost) return "Not the host";
			if (!CustomIslandSpawner.RaftPosition.HasValue) return "No raft";
			MapType t = MapTypes.Get(TypeName);
			if (t == null) return "No map type '" + TypeName + "'";
			float el;
			IslandGenSettings s = MapTypes.Roll(t, new System.Random(), out el);
			Vector3? spot = Spot(CustomIslandSpawner.RaftPosition.Value, s.Radius);
			if (!spot.HasValue) return "No room near the raft";
			string name = CustomIslandSpawner.FreeName(NamePrefix + s.Seed);
			IslandWorldState.Entry entry = IslandWorldState.Add(name, spot.Value, null, false);
			entry.Loading = true;
			Brought++;
			seenAtNight = true;
			DynamicIslands.instance.StartCoroutine(CustomIslandSpawner.GenerateAndSpawn(() => MapTypes.Create(t, s, el, name), name, entry));
			return "Bringing '" + name + "' " + Flat(spot.Value - CustomIslandSpawner.RaftPosition.Value).magnitude.ToString("F0") + " m from the raft";
		}

		static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

		/// <summary>A clear place beside the raft's way: ahead and to one side, so it passes within swimming reach.</summary>
		internal static Vector3? Spot(Vector3 raft, float radius)
		{
			Vector3 dir = CustomIslandSpawner.SailDirection(), side = new Vector3(dir.z, 0f, -dir.x);
			float raftR = CustomIslandSpawner.RaftRadius;
			for (int attempt = 0; attempt < 24; attempt++)
			{
				float s = attempt % 2 == 0 ? 1f : -1f;
				float lateral = radius + raftR + 16f + (attempt / 2 % 3) * 8f, ahead = 40f + (attempt / 6) * 30f;
				Vector3 c = raft + dir * ahead + side * s * lateral;
				c.y = 0f;
				// (Raft's islands and the other custom islands kept clear; the raft's own rule - 60 m clear of it - not: the ship comes close)
				if (CustomIslandSpawner.Rejects(c, radius, c + new Vector3(100000f, 0f, 0f), false, 0f) == null) return c;
			}
			return null;
		}

		/// <summary>Host: takes the ship away (for every player): its chests, loot and monsters with it, its file deleted.</summary>
		internal static void Remove(IslandWorldState.Entry e, string why)
		{
			if (e == null || !Raft_Network.IsHost) return;
			string name = e.HostName;
			IslandWorldState.RemoveIds(new[] { e.Id }, true);
			IslandWorldState.Save();
			try { string path = IslandSpawner.PathFor(name); if (System.IO.File.Exists(path)) System.IO.File.Delete(path); IslandCache.ForgetFile(path); }
			catch (Exception ex) { Log("Deleting '" + name + "': " + ex.Message); }
			if (sinking == e) sinking = null;
			Log("'" + name + "' is gone (" + why + ")");
		}

		/// <summary>Every machine: the ship goes down slowly; then (host) it is taken away.</summary>
		static IEnumerator Sink(IslandWorldState.Entry e)
		{
			GameObject root = e.Root;
			Vector3 start = root != null ? root.transform.position : Vector3.zero;
			Light[] lights = root != null ? root.GetComponentsInChildren<Light>(true) : new Light[0];
			float[] power = lights.Select(l => l != null ? l.intensity : 0f).ToArray();
			for (float t = 0f; t < SinkSeconds && root != null; t += Time.deltaTime)
			{
				float k = t / SinkSeconds;
				root.transform.position = start + Vector3.down * (SinkDepth * k * k);
				for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = power[i] * (1f - k);
				yield return null;
			}
			if (Raft_Network.IsHost && IslandWorldState.Contains(e)) Remove(e, "dawn");
			// (a player keeps it as sinking until the host's word that it is gone comes, so it never sinks twice)
			if (Raft_Network.IsHost && sinking == e) sinking = null;
		}

		#endregion

		#region The ghostly looks

		static readonly Color Glow = new Color(0.45f, 1f, 0.9f);
		const string GlowName = "GhostShipGlow";

		/// <summary>The ship's own glow: a few pale green lights over its deck, its pieces faintly lit.</summary>
		static void Dress(GameObject root)
		{
			if (root.transform.Find(GlowName) != null) return;
			var bounds = new Bounds();
			bool any = false;
			foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
			{
				if (r == null || r is ParticleSystemRenderer || r.bounds.size.magnitude > 60f) continue;
				if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
				foreach (Material m in r.materials)
					if (m != null && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Glow * 0.18f); }
			}
			if (!any) return;
			var glow = new GameObject(GlowName);
			glow.transform.SetParent(root.transform, false);
			bool alongZ = bounds.size.z >= bounds.size.x;
			float half = (alongZ ? bounds.size.z : bounds.size.x) * 0.35f;
			for (int i = -1; i <= 1; i++)
			{
				var go = new GameObject("GhostLight" + (i + 1));
				go.transform.SetParent(glow.transform, false);
				go.transform.position = new Vector3(bounds.center.x + (alongZ ? 0f : i * half), bounds.min.y + 3.5f, bounds.center.z + (alongZ ? i * half : 0f));
				Light l = go.AddComponent<Light>();
				l.type = LightType.Point; l.color = Glow; l.range = 16f; l.intensity = 1.6f; l.shadows = LightShadows.None;
			}
		}

		/// <summary>Whether a monster is one of the ship's (the host knows its own; a player goes by the ship's spots near it).</summary>
		static bool OfShip(AI_NetworkBehaviour ai, CreatureSpawnPoint[] spots)
		{
			if (Raft_Network.IsHost) return spots.Any(p => p.Spawned.Contains(ai) || p.Summoned.Contains(ai));
			if (!CreatureSpawner.IsOnCustomIsland(ai)) return false;
			return spots.Any(p => p.Kind != null && p.Kind.Type == ai.behaviourType && (p.transform.position - ai.transform.position).sqrMagnitude < 80f * 80f);
		}

		/// <summary>The ship's monsters as ghosts: see-through, pale and faintly glowing.</summary>
		static void Haunt(GameObject root, Vector3 centre)
		{
			CreatureSpawnPoint[] spots = root.GetComponentsInChildren<CreatureSpawnPoint>(true);
			if (spots.Length == 0) return;
			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>())
			{
				if (ai == null || dressed.Contains(ai.GetInstanceID()) || Flat(ai.transform.position - centre).sqrMagnitude > 150f * 150f) continue;
				if (!OfShip(ai, spots)) continue;
				dressed.Add(ai.GetInstanceID());
				try { MakeGhost(ai.gameObject); }
				catch (Exception e) { Log("A ghost's looks: " + e.Message); }
			}
		}

		static Shader ghostShader;

		/// <summary>Whether this creature has the ghost's looks (tests).</summary>
		public static bool IsGhost(GameObject go) { return go != null && go.transform.Find(GlowName) != null; }

		/// <summary>A creature made a ghost: its materials see-through and glowing (additive, its own texture, pale green), a faint light on it.</summary>
		internal static void MakeGhost(GameObject go)
		{
			if (IsGhost(go)) return;
			if (ghostShader == null) ghostShader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
			if (ghostShader != null)
				foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
				{
					if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
					Material[] mats = r.sharedMaterials;
					var ghosts = new Material[mats.Length];
					for (int i = 0; i < mats.Length; i++)
					{
						var m = new Material(ghostShader);
						if (mats[i] != null && mats[i].HasProperty("_MainTex")) m.mainTexture = mats[i].mainTexture;
						var tint = new Color(Glow.r, Glow.g, Glow.b, 0.32f);
						if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
						m.color = tint;
						ghosts[i] = m;
					}
					r.sharedMaterials = ghosts;
					r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				}
			var glow = new GameObject(GlowName);
			glow.transform.SetParent(go.transform, false);
			glow.transform.localPosition = Vector3.up * 0.8f;
			Light l = glow.AddComponent<Light>();
			l.type = LightType.Point; l.color = Glow; l.range = 5f; l.intensity = 1.2f; l.shadows = LightShadows.None;
		}

		#endregion

		#region The map type

		/// <summary>Settings of a new ghost ship (its reach, for finding room at sea).</summary>
		internal static IslandGenSettings Settings(System.Random rnd) { return new IslandGenSettings { Seed = rnd.Next(1, 999999), Height = 2f, Radius = 16f }; }

		static readonly string[] LogTexts =
		{
			"Captain's log, last page. We sailed for the lights that weren't there. The crew went one by one, and still they walk the deck at night.",
			"Captain's log. The sea took us, but the ship won't stay down. Each dawn we sink, each dark we rise. Take what you want - we can't use it.",
			"Captain's log. Whoever boards: be quick. When the sky grows light she goes under again, and all aboard with her.",
		};

		static readonly string[] Hoard = { "TitaniumIngot*3", "ExplosiveGoo*2", "CircuitBoard*2", "Battery*1", "HealingSalve_Good*2", "Jar_Honey*2", "MetalIngot*6", "CopperIngot*5", "Bolt*8", "Hinge*4" };

		static string Loot(System.Random rnd, int n)
		{
			return ContentCatalog.PresetLoot(Hoard.OrderBy(x => rnd.Next()).Take(n).ToArray());
		}

		/// <summary>A ship's deck of Raft's foundations afloat (no land): railings, a cabin at the stern, a mast, a sail and an
		/// anchor at the bow, lanterns, chests of good loot, and its ghost crew of rats and screechers. Everything stands on the deck.</summary>
		internal static IslandFile Build(IslandGenSettings s, string name)
		{
			var f = new IslandFile
			{
				Name = name, TerrainSize = IslandGenerator.BuildArea, HeightmapResolution = IslandGenerator.BuildResolution,
				Heights = new float[IslandGenerator.BuildResolution, IslandGenerator.BuildResolution],
			};
			var k = new MapKit(f, s.Seed);
			var rnd = new System.Random(s.Seed);
			float g = PlacementOptions.GridSize, sea = k.Sea;
			float floatY = sea + PlacementOptions.FoundationFloat, deck = floatY + PlacementOptions.FoundationPlanks;
			const int W = 6, L = 12, Mid = 4;
			// the hull: full width from the stern to row 9, then the bow narrows
			Func<int, int, bool> has = (x, z) => x >= 0 && z >= 0 && z < L && x < W && (z < 10 || (z == 10 && x >= 1 && x <= 4) || (z == 11 && x >= 2 && x <= 3));
			Vector3 o = new Vector3(k.Mid.x - (W - 1) * g / 2f, 0f, k.Mid.y - (L - 1) * g / 2f);
			Func<int, int, Vector3> at = (x, z) => o + new Vector3(x * g, deck, z * g);
			Func<int, int, Vector2> flat = (x, z) => new Vector2(o.x + x * g, o.z + z * g);
			Func<string, string, Dictionary<string, string>> loot = (title, items) => new Dictionary<string, string> { { ObjectProps.LootItems, items }, { ObjectProps.NoteTitle, title } };
			Action<string, Vector3, float> add = (n, p, yaw) => k.Add(n, p, yaw, null, 0f);
			for (int x = 0; x < W; x++)
				for (int z = 0; z < L; z++)
					if (has(x, z)) k.Add("Block_Foundation", o + new Vector3(x * g, floatY, z * g), 0f, null, 0f);

			// The cabin at the stern: 4 x 3 cells, wooden walls with windows, a door to the deck, a wooden hipped roof
			Func<int, int, bool> cabin = (x, z) => x >= 1 && x <= 4 && z >= 0 && z <= 2;
			for (int x = 1; x <= 4; x++)
			{
				add(x == 2 || x == 3 ? "Block_Wall_Window_Wood" : "Block_Wall_Wood", at(x, 0) + new Vector3(0f, 0f, -g / 2), 0f);
				add(x == 2 ? "Block_Wall_Door_Wood" : x == 3 ? "Block_Wall_Window_Wood" : "Block_Wall_Wood", at(x, 2) + new Vector3(0f, 0f, g / 2), 180f);
			}
			for (int z = 0; z <= 2; z++)
			{
				add("Block_Wall_Wood", at(1, z) + new Vector3(-g / 2, 0f, 0f), 90f);
				add("Block_Wall_Wood", at(4, z) + new Vector3(g / 2, 0f, 0f), 270f);
			}
			foreach (Vector3 c in new[] { at(1, 0) + new Vector3(-g / 2, 0f, -g / 2), at(4, 0) + new Vector3(g / 2, 0f, -g / 2), at(1, 2) + new Vector3(-g / 2, 0f, g / 2), at(4, 2) + new Vector3(g / 2, 0f, g / 2) })
				add("Block_Pillar_Wood", c, 0f);
			RaftRoof.Hip(add, at(1, 0) + new Vector3(0f, RaftRoof.OnWalls, 0f), 4, 3, true);

			// Railings round the deck's edge (not where the cabin's walls are), with a gap each side for a ladder
			for (int x = 0; x < W; x++)
				for (int z = 0; z < L; z++)
				{
					if (!has(x, z)) continue;
					bool gap = z == Mid && (x == 0 || x == W - 1);
					if (!has(x, z - 1) && !cabin(x, z)) add("Block_Wall_Fence_Plank", at(x, z) + new Vector3(0f, 0f, -g / 2), 0f);
					if (!has(x, z + 1)) add("Block_Wall_Fence_Plank", at(x, z) + new Vector3(0f, 0f, g / 2), 180f);
					if (!has(x - 1, z) && !gap) add("Block_Wall_Fence_Plank", at(x, z) + new Vector3(-g / 2, 0f, 0f), 90f);
					if (!has(x + 1, z) && !gap) add("Block_Wall_Fence_Plank", at(x, z) + new Vector3(g / 2, 0f, 0f), 270f);
				}
			add("Block_Ladder", at(W, Mid) + new Vector3(-g / 2, 0f, 0f), 90f);
			add("Block_Ladder", at(0, Mid) + new Vector3(-g / 2, 0f, 0f), 270f);

			// The mast with its flag amidships, a sail forward, the anchor at the bow, lanterns at the corners of the deck
			add("BoatLandmark_Flag", at(2, 6) + new Vector3(g / 2, 0f, 0f), 0f);
			add("Placeable_Sail", at(2, 9) + new Vector3(g / 2, 0f, 0f), 0f);
			add("Placeable_Anchor_Stationary", at(2, 10) + new Vector3(g / 2, 0f, g / 2), 0f);
			foreach (int[] c in new[] { new[] { 0, 3 }, new[] { 5, 3 }, new[] { 0, 9 }, new[] { 5, 9 } })
				add("Placeable_Lantern_Basic", at(c[0], c[1]), 0f);

			// The loot: the captain's hoard in the cabin, chests on deck, barrels
			k.Add("Loot_ChestLarge", at(3, 0), 0f, loot("Ghost ship hoard", Loot(rnd, 5)), 0f);
			k.Add("Loot_Chest", at(1, 0), 0f, loot("Captain's chest", Loot(rnd, 3)), 0f);
			k.Add("Note_Paper", at(2, 1) + new Vector3(0.3f, 0f, 0.3f), 0f, new Dictionary<string, string> { { ObjectProps.NoteTitle, "Captain's log" }, { ObjectProps.NoteText, LogTexts[rnd.Next(LogTexts.Length)] } }, 0f);
			k.Add("Loot_Chest", at(0, 7), 90f, loot("Ghost ship chest", Loot(rnd, 3)), 0f);
			k.Add("Loot_Chest", at(5, 7), 270f, loot("Ghost ship chest", Loot(rnd, 3)), 0f);
			k.Add("Loot_Barrel", at(0, 1), 0f, loot("Ghost ship barrel", MapKit.Loot("Food")), 0f);
			k.Add("Loot_Barrel", at(5, 1), 0f, loot("Ghost ship barrel", MapKit.Loot("Metal")), 0f);

			// The ghost crew (no clearing round them: that would take the deck away; they don't come back)
			k.Creature("Rat", flat(2, 4), 3 + rnd.Next(2), "Hard", 1f, null, false, deck, 0f);
			k.Creature("Rat", flat(3, 8), 2 + rnd.Next(2), "Hard", 1f, null, false, deck, 0f);
			k.Creature("StoneBird", flat(2, 6), 1 + rnd.Next(2), "Normal", 1f, null, false, deck + 10f, 0f);
			// A pale green haze round it
			k.Atmosphere(flat(2, 5) + new Vector2(g / 2, 0f), 30f, "#2B4A55", 0.25f, "#8CFFE8", 0.35f, "mist");
			f.Props[IslandProps.Title] = "Ghost ship";
			return f;
		}

		#endregion

		[ConsoleCommand(name: "GhostShip", docs: "The world option Ghost ship: GhostShip = whether a ship is out and the next nights that bring one; GhostShip roll <from> <to> = the nights in a range that bring one; GhostShip bring = (host, cheat) one beside the raft now; GhostShip sink = (host) the ship out goes now")]
		public static void GhostShipCommand(string[] args)
		{
			int seed = WorldOptions.Seed, from, to;
			string a0 = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (a0 == "roll" && args.Length > 2 && int.TryParse(args[1], out from) && int.TryParse(args[2], out to))
			{
				var days = Enumerable.Range(from, Math.Max(0, Math.Min(to - from + 1, 10000))).Where(d => Rolls(seed, d)).ToArray();
				Debug.Log("[CUSTOM ISLANDS] Ghost ship nights " + from + "-" + to + " (seed " + seed + "): " + days.Length + ": " + string.Join(" ", days.Select(d => d.ToString()).ToArray()));
				return;
			}
			if (a0 == "bring") { Debug.Log("[CUSTOM ISLANDS] Ghost ship: " + Bring()); return; }
			if (a0 == "sink") { TestNight = TestSleeping = null; TestForce = false; IslandWorldState.Entry e = Ship; if (e != null) Remove(e, "the command"); else Debug.Log("[CUSTOM ISLANDS] Ghost ship: none out"); return; }
			int day = Today, next = Enumerable.Range(day, 1000).FirstOrDefault(d => Rolls(seed, d));
			IslandWorldState.Entry s = Ship;
			Debug.Log("[CUSTOM ISLANDS] Ghost ship: " + (On ? "on" : "off") + ", day " + day + ", " + (Night ? "night" : "day") + (Sleeping ? ", asleep" : "") +
				", " + (s != null ? "'" + s.HostName + "' out" : "none out") + ", next night with one: " + next + ", last nightfall: " + (LastDecision.Length > 0 ? LastDecision : "-"));
		}
	}
}
