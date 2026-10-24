using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AI;

namespace DynamicIslands
{
	/// <summary>Tests of the world randomizer (WorldRandomizer): Raft's own islands, animals and sharks in a normal world.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIProbeLandmarks", docs: "Dev, world: lists Raft's spawned islands near the raft - their items, animal spawners and NavMesh - and every animal in the world, for the randomizer. CIProbeLandmarks [max distance, default 1500]")]
		public static void ProbeLandmarksCommand(string[] args)
		{
			float max = 1500f;
			if (args != null && args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out max);
			try { ProbeLandmarks(max); }
			catch (Exception e) { Fail("probe: " + e); }
		}

		#region The randomizer's rules (anywhere)

		[ConsoleCommand(name: "CIRandomizerUnit", docs: "Dev, anywhere: the world randomizer's settings text, levels, and animal colours and alphas: how often, the same for the same animal, different per world seed")]
		public static void RandomizerUnitCommand()
		{
			RandomizerSettings keep = WorldRandomizer.Current;
			bool ok = true;
			try
			{
				// Settings survive their text (world file, network message)
				RandomizerSettings s = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 424242 };
				s.Disabled.Add(RandomizerSettings.Alphas); s.Disabled.Add(RandomizerSettings.Bosses);
				RandomizerSettings back = RandomizerSettings.Decode(s.Encode());
				Check(ref ok, back.Level == s.Level && back.Seed == s.Seed && back.Disabled.SetEquals(s.Disabled), "settings text round trip: '" + s.Encode() + "'");
				Check(ref ok, RandomizerSettings.Decode("level=off").Level == RandomizerSettings.Off && !RandomizerSettings.Decode("").On && RandomizerSettings.Decode("level=Light;seed=5").Level == RandomizerSettings.Light, "levels by name, empty = off");
				Check(ref ok, !back.Has(RandomizerSettings.Alphas) && back.Has(RandomizerSettings.Colours) && !new RandomizerSettings().Has(RandomizerSettings.Colours), "parts on and off; nothing when off");
				Check(ref ok, back.Describe().StartsWith("Wild: ") && back.Describe().Contains("colours") && !back.Describe().Contains("alphas"), "description: " + back.Describe());
				Log("Hash(1,2,3) = " + WorldRandomizer.Hash(1, 2, 3) + " (integer maths only: the same on every machine)");

				// How often animals change, per level (the chances in VariantOf)
				foreach (int level in new[] { RandomizerSettings.Light, RandomizerSettings.Normal, RandomizerSettings.Wild })
				{
					WorldRandomizer.Current = new RandomizerSettings { Level = level, Seed = 777 };
					int n = 4000, coloured = 0, alpha = 0, bruce = 0, gold = 0;
					for (uint i = 1; i <= n; i++)
					{
						WorldRandomizer.Variant v = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Boar, i);
						if (v != null && v.Alpha) alpha++; else if (v != null) coloured++;
						WorldRandomizer.Variant sv = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Shark, i);
						if (sv != null && sv.Alpha) bruce++;
						if (sv != null && sv.Name == "gold") gold++;
					}
					float c = WorldRandomizer.Current.Pick(0.15f, 0.3f, 0.55f), a = WorldRandomizer.Current.Pick(0.03f, 0.06f, 0.12f), b = WorldRandomizer.Current.Pick(0.06f, 0.12f, 0.22f);
					float fc = coloured / (float)n, fa = alpha / (float)n, fb = bruce / (float)n;
					Check(ref ok, Mathf.Abs(fa - a) < 0.025f && Mathf.Abs(fc - c * (1f - a)) < 0.04f && Mathf.Abs(fb - b) < 0.03f,
						RandomizerSettings.LevelNames[level] + ": " + (fc * 100f).ToString("F0") + "% of warthogs coloured, " + (fa * 100f).ToString("F1") + "% alphas, " + (fb * 100f).ToString("F1") + "% of sharks a big Bruce, " + gold + " golden sharks");
				}
				// Never for bosses, people, bugs
				WorldRandomizer.Current = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 5 };
				bool none = true;
				foreach (AI_NetworkBehaviourType t in new[] { AI_NetworkBehaviourType.Boss_Varuna, AI_NetworkBehaviourType.MamaBear, AI_NetworkBehaviourType.HyenaBoss, AI_NetworkBehaviourType.NPC_Johan, AI_NetworkBehaviourType.ButlerBot, AI_NetworkBehaviourType.BugSwarm_Bee, AI_NetworkBehaviourType.Whale })
					for (uint i = 1; i < 300; i++) if (WorldRandomizer.VariantOf(t, i) != null) none = false;
				Check(ref none, none, "bosses, people, bugs and the whale keep Raft's looks");
				ok &= none;
				// The same animal looks the same every time; another world seed gives other animals other looks
				int same = 0, differs = 0;
				for (uint i = 1; i <= 500; i++)
				{
					WorldRandomizer.Variant v1 = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Goat, i), v2 = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Goat, i);
					if ((v1 == null ? "" : v1.Name) == (v2 == null ? "" : v2.Name)) same++;
				}
				WorldRandomizer.Current = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 6 };
				var other = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 5 };
				for (uint i = 1; i <= 500; i++)
				{
					WorldRandomizer.Variant v1 = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Goat, i);
					WorldRandomizer.Current = other;
					WorldRandomizer.Variant v2 = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Goat, i);
					WorldRandomizer.Current = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 6 };
					if ((v1 == null ? "" : v1.Name) != (v2 == null ? "" : v2.Name)) differs++;
				}
				Check(ref ok, same == 500 && differs > 150, "the same animal always looks the same (" + same + "/500); another world seed changes " + differs + "/500");
				// Parts switched off
				var noColours = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 9 };
				noColours.Disabled.Add(RandomizerSettings.Colours);
				WorldRandomizer.Current = noColours;
				int colouredOff = 0, alphasOn = 0;
				for (uint i = 1; i <= 1000; i++) { WorldRandomizer.Variant v = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Bear, i); if (v != null && !v.Alpha) colouredOff++; if (v != null && v.Alpha) alphasOn++; }
				var noAlphas = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 9 };
				noAlphas.Disabled.Add(RandomizerSettings.Alphas);
				WorldRandomizer.Current = noAlphas;
				int alphasOff = 0;
				for (uint i = 1; i <= 1000; i++) { WorldRandomizer.Variant v = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Bear, i); if (v != null && v.Alpha) alphasOff++; }
				Check(ref ok, colouredOff == 0 && alphasOn > 50 && alphasOff == 0, "colours off: no colours (" + colouredOff + "), alphas still (" + alphasOn + "); alphas off: none (" + alphasOff + ")");
			}
			catch (Exception e) { Fail("randomizer rules: " + e); ok = false; }
			finally { WorldRandomizer.Current = keep; }
			if (ok) Log("PASS: randomizer rules"); else Fail("randomizer rules");
		}

		[ConsoleCommand(name: "CIOddities", docs: "Dev, anywhere: every oddity island and the boss lair can be made: set piece, loot, note, title; lair with a much tougher named beast, guards, hoard and quest; every object exists in this Raft")]
		public static void OdditiesCommand() { StartTest(OdditiesRoutine()); }

		static IEnumerator OdditiesRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			var expected = new Dictionary<string, string>
			{
				{ "van", "^Van" }, { "caravan", "^Caravan_" }, { "planecrash", "^Airplane$" }, { "boatwreck", "^BoatStranded$" }, { "shack", "^Balboa_Shack$" },
				{ "statue", "^(TangaroaFounderStatue|RaftMonument)$" }, { "rocket", "^CaravanRocket$" }, { "hut", "^Block_Foundation$" },
			};
			var files = new List<IslandFile>();
			var names = new HashSet<string>();
			foreach (var kv in expected.Concat(new[] { new KeyValuePair<string, string>("lair", "^Creature_") }))
			{
				MapType type = MapTypes.Get(kv.Key);
				if (type == null) { Fail("no map type '" + kv.Key + "'"); ok = false; continue; }
				float el;
				IslandGenSettings s = MapTypes.Roll(type, new System.Random(kv.Key.GetHashCode() & 0xFFFF), out el);
				IslandFile f = null;
				try { f = MapTypes.Create(type, s, el, MapTypes.FileName(type, s)); }
				catch (Exception e) { Fail(kv.Key + ": " + e); ok = false; continue; }
				files.Add(f);
				foreach (IslandObject o in f.Objects) names.Add(o.Name);
				int loot = f.Objects.Count(o => ContentCatalog.IsLootObject(o.Name));
				int notes = f.Objects.Count(o => ContentCatalog.IsNoteObject(o.Name));
				IslandObject piece = f.Objects.FirstOrDefault(o => System.Text.RegularExpressions.Regex.IsMatch(o.Name, kv.Value));
				string title = ObjectProps.Get(f.Props, IslandProps.Title);
				bool good = piece != null && loot > 0 && title.Length > 0 && (kv.Key == "lair" || notes > 0);
				if (kv.Key == "lair")
				{
					IslandObject beast = f.Objects.FirstOrDefault(o => o.Name.StartsWith("Creature_") && ObjectProps.GetFloat(o.Props, ObjectProps.CreatureHealth, 1f) >= 6f);
					good &= beast != null && f.Objects.Count(o => o.Name.StartsWith("Creature_")) >= 3 && f.Props.ContainsKey(IslandQuest.KeyTitle) && f.Objects.Any(o => ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Hoard");
					Log("  lair: '" + title + "', beast " + (beast != null ? beast.Name + " x" + ObjectProps.GetFloat(beast.Props, ObjectProps.CreatureHealth, 1f) + " health, size " + ObjectProps.GetFloat(beast.Props, ObjectProps.CreatureSize, 1f) : "none") +
						", " + f.Objects.Count(o => o.Name.StartsWith("Creature_")) + " creature spots, quest '" + ObjectProps.Get(f.Props, IslandQuest.KeyTitle) + "'");
				}
				// Set pieces stand on the ground: their bottom within a metre or so of it (sunk a little, never floating)
				Check(ref ok, good, kv.Key + ": '" + title + "', " + f.Objects.Count + " objects, " + (piece != null ? piece.Name : "NO set piece") + ", " + loot + " loot, " + notes + " note(s)");
			}
			// The generic "oddity" type makes all kinds
			var kinds = new HashSet<string>();
			for (int i = 0; i < 40; i++)
			{
				MapType t = MapTypes.Get("oddity");
				float el;
				IslandGenSettings s = MapTypes.Roll(t, new System.Random(1000 + i), out el);
				IslandFile f = MapTypes.Create(t, s, el, "test");
				kinds.Add(ObjectProps.Get(f.Props, IslandProps.Title));
			}
			Check(ref ok, kinds.Count >= 6, "'oddity' makes " + kinds.Count + " kinds in 40 islands: " + string.Join(", ", kinds.ToArray()));
			// Every object exists in this Raft (the set pieces come from Raft's other islands)
			yield return PlaceableCatalog.EnsureLoaded(names.ToList());
			List<string> missing = names.Where(n => PlaceableCatalog.Get(n) == null).ToList();
			Check(ref ok, missing.Count == 0, names.Count + " kinds of objects, missing: " + (missing.Count == 0 ? "none" : string.Join(", ", missing.ToArray())));
			if (ok) Log("PASS: oddities and lairs"); else Fail("oddities and lairs");
		}

		#endregion

		#region In a world (host)

		[ConsoleCommand(name: "CIRandomizerWorld", docs: "Dev, world (host): turns the randomizer on (wild, a fixed seed) and checks Raft's islands: crates and clams moved (the same again), extras laid over them (animals, loot, finds) that load and spawn animals, coloured animals, an alpha's spoils, an oddity and a boss lair. CIRandomizerWorld [keep] [shots]")]
		public static void RandomizerWorldCommand(string[] args)
		{
			bool keep = args != null && args.Any(a => a.Equals("keep", StringComparison.OrdinalIgnoreCase));
			bool shots = args != null && args.Any(a => a.Equals("shots", StringComparison.OrdinalIgnoreCase));
			StartTest(RandomizerWorldRoutine(keep, shots));
		}

		static IEnumerator RandomizerWorldRoutine(bool keep, bool shots)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			RandomizerSettings before = WorldRandomizer.Current.Copy();
			int islandsBefore = IslandWorldState.Islands.Count;
			WorldRandomizer.ForgetSeen();
			WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 20260926 });
			Log("Randomizer: " + WorldRandomizer.Current.Describe());
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;

			// Raft's islands get handled (a second after they are seen)
			yield return new WaitForSeconds(4f);
			List<Landmark> spawned = WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned).ToList();
			List<Landmark> nat = spawned.Where(WorldRandomizer.IsNatural).ToList();
			Check(ref ok, nat.Count > 0, spawned.Count + " of Raft's islands spawned, " + nat.Count + " plain ones randomized: " + string.Join(", ", nat.Select(l => l.name).Take(8).ToArray()));

			// Crates and clams: some moved; Raft still finds each by its old place; the same spots again
			var moved = new List<KeyValuePair<LandmarkItem, Vector3>>();
			// (each item's island as the loop found it: GetComponentInParent<Landmark>() gave null for one (an item not under
			// its island's transform) and OriginalOf threw - RUN2 10-20)
			var islandOf = new Dictionary<LandmarkItem, Landmark>();
			int lootItems = 0;
			bool findable = true;
			foreach (Landmark l in nat)
				foreach (LandmarkItem i in (l.landmarkItems ?? new LandmarkItem[0]).Where(WorldRandomizer.IsRaftLoot))
				{
					lootItems++;
					Vector3? o = WorldRandomizer.OriginalOf(l, i);
					if (o.HasValue && (o.Value - i.transform.position).magnitude > 1f) { moved.Add(new KeyValuePair<LandmarkItem, Vector3>(i, i.transform.position)); islandOf[i] = l; }
					if (l.GetLandmarkItemByLocalPosition(i.localLandmarkPosition) != i) findable = false;
				}
			Check(ref ok, moved.Count > 0 && moved.Count <= lootItems, moved.Count + " of " + lootItems + " crates and clams lie somewhere else (wild: about 80%)");
			Check(ref ok, findable, "Raft still finds every crate and clam by the place it knows (picking up, saving and the network use it)");
			bool land = moved.All(m => { Vector3? o = WorldRandomizer.OriginalOf(islandOf[m.Key], m.Key); return !o.HasValue || (o.Value.y < -0.5f) == (m.Value.y < -0.5f); });
			Check(ref ok, land, "crates on land stay on land, under-water ones under water");
			WorldRandomizer.Rehandle();
			yield return new WaitForSeconds(3f);
			float drift = moved.Count == 0 ? 0f : moved.Max(m => (m.Key.transform.position - m.Value).magnitude);
			Check(ref ok, drift < 0.05f, "handled again (as after a reload or on another player's machine): the same spots (max difference " + drift.ToString("F3") + " m)");

			// Extras over Raft's islands
			List<IslandWorldState.Entry> extras = IslandWorldState.Islands.Where(WorldRandomizer.IsExtras).ToList();
			for (float t = 0; t < 20f && extras.Count == 0; t += 1f) { yield return new WaitForSeconds(1f); extras = IslandWorldState.Islands.Where(WorldRandomizer.IsExtras).ToList(); }
			Check(ref ok, extras.Count > 0, extras.Count + " of Raft's islands got extras (" + WorldRandomizer.SeenCount + " looked at)");
			Check(ref ok, extras.All(e => WorldRandomizer.IslandAt(e.Position) != null), "each lies over one of Raft's islands, centred on it");
			// The near ones load (like any custom island within 800 m)
			Network_Player player = RAPI.GetLocalPlayer();
			List<IslandWorldState.Entry> near = extras.Where(e => Flat(e.Position - raft).magnitude < CustomIslandSpawner.UnloadDistance - 200f).ToList();
			for (float t = 0; t < 30f && near.Any(e => e.Root == null && !e.Failed); t += 1f) yield return new WaitForSeconds(1f);
			Check(ref ok, near.All(e => e.Root != null), near.Count(e => e.Root != null) + " of " + near.Count + " near ones loaded");
			// Their animals come (on Raft's small islands a NavMesh is built for them first)
			List<CreatureSpawnPoint> spots = near.Where(e => e.Root != null).SelectMany(e => e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true)).ToList();
			for (float t = 0; t < 90f && spots.Any(p => p != null && p.RecordedAlive < 0); t += 1f) yield return new WaitForSeconds(1f);
			int alive = spots.Where(p => p != null).Sum(p => Math.Max(0, p.RecordedAlive));
			Check(ref ok, spots.Count == 0 || alive > 0, spots.Count + " extra animal spot(s) on near islands, " + alive + " animal(s) spawned: " +
				string.Join(", ", spots.Where(p => p != null).Select(p => p.Kind.Label + " " + p.RecordedAlive).ToArray()));
			int loot = near.Where(e => e.Root != null).Sum(e => e.Root.GetComponentsInChildren<LootCrate>(true).Length);
			Log("  loot containers on near extras: " + loot + "; all extras: " + string.Join("; ", extras.Select(e => e.HostName + (e.Root != null ? " (loaded)" : "")).ToArray()));

			// Animals: some have other colours (on their body's materials)
			yield return new WaitForSeconds(1.5f);
			int coloured = 0, checkedTint = 0;
			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>())
			{
				string v;
				if (!WorldRandomizer.VariantOfIndex.TryGetValue(ai.ObjectIndex, out v)) continue;
				coloured++;
				Renderer r = WorldRandomizer.BodyOf(ai).FirstOrDefault();
				if (r == null) continue;
				var block = new MaterialPropertyBlock();
				r.GetPropertyBlock(block);
				Material m = r.sharedMaterial;
				if (m != null && m.HasProperty("_Color") && block.GetColor("_Color") != m.GetColor("_Color")) checkedTint++;
				else if (m != null && m.HasProperty("_Diffuse") && block.GetTexture("_Diffuse") is RenderTexture) checkedTint++; // (the shark: a coloured copy of its texture)
				if (coloured <= 6) Log("  " + ai.behaviourType + " #" + ai.ObjectIndex + ": " + v);
			}
			Check(ref ok, coloured > 0 && checkedTint > 0, coloured + " animal(s) with another look, " + checkedTint + " checked on their material");

			// An alpha's spoils: Raft's own dropped items, for every player
			Vector3 dropAt = player.transform.position + player.transform.forward * 2f;
			List<string> dropped = WorldRandomizer.DropSpoils(AI_NetworkBehaviourType.Boar, dropAt, 99);
			yield return new WaitForSeconds(1f);
			int pickups = UnityEngine.Object.FindObjectsOfType<PickupItem>().Count(p => p.isDropped && (p.transform.position - dropAt).magnitude < 6f);
			Check(ref ok, dropped.Count >= 3 && pickups >= dropped.Count, "an alpha warthog's spoils: " + string.Join(", ", dropped.ToArray()) + " (" + pickups + " dropped item(s) near)");
			foreach (PickupItem p in UnityEngine.Object.FindObjectsOfType<PickupItem>().Where(p => p.isDropped && (p.transform.position - dropAt).magnitude < 6f))
				PickupObjectManager.RemovePickupItemNetwork(p.GetComponent<PickupItem_Networked>());

			// An oddity island and a boss lair appear as the randomizer brings them
			var brought = new List<IslandWorldState.Entry>();
			foreach (string type in new[] { "oddity", "lair" })
			{
				int n = IslandWorldState.Islands.Count;
				string r = CustomIslandSpawner.TrySpawn(raft, true, CustomIslandSpawner.TypePrefix + type);
				IslandWorldState.Entry e = IslandWorldState.Islands.Count > n ? IslandWorldState.Islands[IslandWorldState.Islands.Count - 1] : null;
				// (Raft's sea is crowded: ahead of the raft may be full; the randomizer then tries again later. Here: the nearest free spot)
				if (e == null) { e = SpawnTypeNear(type, raft); r += "; placed in a free spot nearby instead"; }
				for (float t = 0; e != null && t < 60f && e.Root == null && !e.Failed; t += 1f) yield return new WaitForSeconds(1f);
				Check(ref ok, e != null && e.Root != null, type + ": " + r + (e != null && e.Root != null ? " - loaded, '" + (e.Root.GetComponent<IslandInfoTag>() != null ? e.Root.GetComponent<IslandInfoTag>().Title : "") + "', " + e.Root.GetComponentsInChildren<IslandObjectRef>(true).Length + " objects" : ""));
				if (e != null) brought.Add(e);
				// (the next one needs the room: custom islands keep 800 m apart)
				if (shots && e != null && e.Root != null) yield return RandomizerShots(new List<IslandWorldState.Entry>(), new List<IslandWorldState.Entry> { e });
				if (e != null) IslandWorldState.RemoveIds(new[] { e.Id }, true);
			}
			if (shots) yield return RandomizerShots(near, new List<IslandWorldState.Entry>());

			// Sailing far enough makes an oddity island due: it comes as soon as there is room ahead
			int count = IslandWorldState.Islands.Count;
			WorldRandomizer.OnSailed(20000f, CustomIslandSpawner.RaftPosition ?? raft);
			Check(ref ok, WorldRandomizer.OddityDue || IslandWorldState.Islands.Count > count, "after 20 km of sailing an oddity island is " + (IslandWorldState.Islands.Count > count ? "on its way: " + IslandWorldState.Islands[IslandWorldState.Islands.Count - 1].HostName : "due (no room ahead yet: tried again every 200 m)"));

			if (!keep)
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(islandsBefore).Select(e => e.Id).ToList(), true);
				WorldRandomizer.Set(before);
				WorldRandomizer.ForgetSeen();
				Log("Removed the test's islands; randomizer back to " + WorldRandomizer.Current.Describe());
			}
			if (ok) Log("PASS: randomizer in a world"); else Fail("randomizer in a world");
		}

		/// <summary>A new island of a map type in the nearest free spot (the test's stand-in for sailing on until there is room).</summary>
		static IslandWorldState.Entry SpawnTypeNear(string typeName, Vector3 raft)
		{
			MapType type = MapTypes.Get(typeName);
			float el;
			IslandGenSettings s = MapTypes.Roll(type, new System.Random(), out el);
			string name = MapTypes.FileName(type, s);
			float radius = MapTypes.EstimatedRadius(s);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft, radius, 780f);
			if (!spot.HasValue) return null;
			CustomIslandSpawner.CacheSize(name, radius, el);
			IslandWorldState.Entry e = IslandWorldState.Add(name, spot.Value, null, false);
			e.Loading = true;
			StartTest(CustomIslandSpawner.GenerateAndSpawn(() => MapTypes.Create(type, s, el, name), name, e));
			return e;
		}

		/// <summary>Pictures: the extras on Raft's islands, the oddity island and the lair, and coloured animals.</summary>
		static IEnumerator RandomizerShots(List<IslandWorldState.Entry> extras, List<IslandWorldState.Entry> brought)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) yield break;
			SetHour(11f);
			int n = 0;
			foreach (IslandWorldState.Entry e in extras.Concat(brought).Where(x => x.Root != null))
			{
				// Some metres from the set piece (or a container), looking at it
				Transform target = e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(o => o.Props != null && o.Props.ContainsKey("set.piece")).Select(o => o.transform).FirstOrDefault() ??
					e.Root.GetComponentsInChildren<LootCrate>(true).Select(c => c.transform).FirstOrDefault() ?? e.Root.transform;
				Vector3 at = target.position + new Vector3(7f, 0f, 7f);
				RaycastHit hit;
				if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out hit, 120f, ~0, QueryTriggerInteraction.Ignore)) at.y = hit.point.y + 1.8f;
				at.y = Mathf.Max(at.y, target.position.y + 3f);
				yield return PutPlayer(player, at, at.y < 0.5f);
				Vector3 d = target.position - at;
				Look(player, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 25f);
				yield return new WaitForSeconds(2f);
				string name = "rnd_" + (n++) + "_" + e.HostName.Replace(" ", "_");
				ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(System.IO.Path.Combine(DynamicIslands.assetpath, "shot_" + name + ".png")));
				Log("Screenshot shot_" + name + ".png");
				yield return new WaitForSeconds(0.6f);
				if (n >= 6) break;
			}
			// A coloured animal
			AI_NetworkBehaviour ai = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => WorldRandomizer.VariantOfIndex.ContainsKey(a.ObjectIndex) && a.behaviourType != AI_NetworkBehaviourType.Shark && a.transform.position.y > 0f)
				.OrderBy(a => (a.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
			if (ai != null)
			{
				Vector3 at = ai.transform.position + new Vector3(3f, 2f, 3f);
				yield return PutPlayer(player, at, false);
				Vector3 d = ai.transform.position - at;
				Look(player, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 20f);
				yield return new WaitForSeconds(1f);
				ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(System.IO.Path.Combine(DynamicIslands.assetpath, "shot_rnd_animal.png")));
				Log("Screenshot shot_rnd_animal.png: " + ai.behaviourType + " " + WorldRandomizer.VariantOfIndex[ai.ObjectIndex]);
				yield return new WaitForSeconds(0.6f);
			}
			// Back on the raft
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
		}

		[ConsoleCommand(name: "CIRandomizerCheck", docs: "Dev, world: the randomizer's settings and extras as saved: CIRandomizerCheck <level> [min extras] (after CIRandomizerWorld keep, a save and a reload)")]
		public static void RandomizerCheckCommand(string[] args)
		{
			string level = args != null && args.Length > 0 ? args[0] : "wild";
			int min = args != null && args.Length > 1 ? int.Parse(args[1]) : 1;
			bool ok = true;
			Check(ref ok, WorldRandomizer.Current.LevelName.Equals(level, StringComparison.OrdinalIgnoreCase) && WorldRandomizer.Current.Seed != 0, "settings: " + WorldRandomizer.Current.Encode());
			int extras = IslandWorldState.Islands.Count(WorldRandomizer.IsExtras);
			Check(ref ok, extras >= min, extras + " extras in the world's list (at least " + min + ")");
			Log(WorldRandomizer.Describe().Replace("\n", " | "));
			if (ok) Log("PASS: randomizer check"); else Fail("randomizer check");
		}

		[ConsoleCommand(name: "CIRandomizerSig", docs: "Dev, world: what this machine worked out for the randomizer (settings, where Raft's crates and clams lie, how animals look, the extras): lines 'SIG ...' to compare between host and client")]
		public static void RandomizerSigCommand()
		{
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			Log("SIG set " + WorldRandomizer.Current.Encode());
			foreach (Landmark l in WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned && WorldRandomizer.GroundOn(x) && WorldRandomizer.IsNatural(x)))
			{
				var spots = (l.landmarkItems ?? new LandmarkItem[0]).Where(WorldRandomizer.IsRaftLoot).Select(i => l.transform.InverseTransformPoint(i.transform.position))
					.Select(v => v.x.ToString("F1", inv) + "," + v.y.ToString("F1", inv) + "," + v.z.ToString("F1", inv)).ToArray();
				if (spots.Length > 0) Log("SIG loot " + WorldRandomizer.SpawnKey(l) + " " + string.Join(" ", spots));
			}
			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().OrderBy(a => a.ObjectIndex))
			{
				if (CreatureSpawner.IsOnCustomIsland(ai)) continue;
				string v;
				Log("SIG animal " + ai.ObjectIndex + " " + ai.behaviourType + " " + (WorldRandomizer.VariantOfIndex.TryGetValue(ai.ObjectIndex, out v) ? v : "-"));
			}
			var extras = IslandWorldState.Islands.Where(WorldRandomizer.IsExtras).OrderBy(e => e.HostName).ToList();
			Log("SIG extras " + string.Join(" ", extras.Select(e => e.HostName).ToArray()));
			Log("SIG loaded " + extras.Count(e => e.Root != null) + " objects " + extras.Where(e => e.Root != null).Sum(e => e.Root.GetComponentsInChildren<IslandObjectRef>(true).Length) +
				" crates " + extras.Where(e => e.Root != null).Sum(e => e.Root.GetComponentsInChildren<LootCrate>(true).Length));
			Log("PASS: randomizer signature");
		}

		/// <summary>The moved crates and clams of Raft's spawned plain islands, with their island and place in its item list.</summary>
		static IEnumerable<KeyValuePair<string, LandmarkItem>> MovedLoot()
		{
			foreach (Landmark l in WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned && WorldRandomizer.GroundOn(x) && WorldRandomizer.IsNatural(x) && x.landmarkItems != null))
				for (int i = 0; i < l.landmarkItems.Length; i++)
				{
					LandmarkItem it = l.landmarkItems[i];
					if (!WorldRandomizer.IsRaftLoot(it)) continue;
					Vector3? o = WorldRandomizer.OriginalOf(l, it);
					if (o.HasValue && (o.Value - it.transform.position).magnitude > 1f) yield return new KeyValuePair<string, LandmarkItem>(WorldRandomizer.SpawnKey(l) + "/" + i, it);
				}
		}

		[ConsoleCommand(name: "CIPickMoved", docs: "Dev, world (either player): picks up one of Raft's crates or clams the randomizer moved, as a player would; logs its key (island/item)")]
		public static void PickMovedCommand()
		{
			var m = MovedLoot().FirstOrDefault(x => x.Value.gameObject.activeInHierarchy && x.Value.GetComponentInChildren<PickupItem>() != null);
			if (m.Value == null) { Fail("no moved crate or clam to pick up"); return; }
			PickupItem item = m.Value.GetComponentInChildren<PickupItem>();
			Pickup pickup = RAPI.GetLocalPlayer().GetComponentInChildren<Pickup>(true);
			PutPlayerNear(item.transform);
			pickup.PickupItemByType(item, true);
			Log("Picked up the moved " + m.Value.name + " " + m.Key + " (" + (Raft_Network.IsHost ? "host" : "client") + ")");
			Log("PASS: picked moved " + m.Key);
		}

		[ConsoleCommand(name: "CIMovedState", docs: "Dev, world: whether one of Raft's moved crates or clams is still there: CIMovedState <island/item>")]
		public static void MovedStateCommand(string[] args)
		{
			string key = args != null && args.Length > 0 ? args[0] : "";
			var m = MovedLoot().FirstOrDefault(x => x.Key == key);
			if (m.Value == null)
			{
				// (picked up: Raft may have switched it off, then it is still in the list but not active)
				foreach (Landmark l in WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned && x.landmarkItems != null))
				{
					string[] k = key.Split('/');
					int i;
					if (k.Length == 2 && WorldRandomizer.SpawnKey(l).ToString() == k[0] && int.TryParse(k[1], out i) && i < l.landmarkItems.Length) { m = new KeyValuePair<string, LandmarkItem>(key, l.landmarkItems[i]); break; }
				}
			}
			if (m.Value == null) { Fail("no item " + key); return; }
			PickupItem p = m.Value.GetComponentInChildren<PickupItem>(true);
			bool there = m.Value.gameObject.activeInHierarchy && (p == null || p.gameObject.activeInHierarchy);
			Log("MOVED " + key + " " + (there ? "there" : "gone") + " (" + m.Value.name + ")");
			Log("PASS: moved state");
		}

		[ConsoleCommand(name: "CIRandomizerClean", docs: "Dev, world (host): removes the randomizer's extras from this world and switches it off (after the persistence test)")]
		public static void RandomizerCleanCommand()
		{
			int n = IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(WorldRandomizer.IsExtras).Select(e => e.Id).ToList(), true);
			WorldRandomizer.Set(new RandomizerSettings());
			WorldRandomizer.ForgetSeen();
			Log("Removed " + n + " extras; randomizer off");
			Log("PASS: randomizer clean");
		}


		[ConsoleCommand(name: "CIRandomizerOff", docs: "Dev, world (host): with the randomizer Off, Raft's islands are Raft's (catalogue IR16): handled again and sailed 6 km, nothing is coloured, made an alpha, moved or added, every crate and clam lies where Raft put it, no oddity, lair or large island is due. Run CIRandomizerClean first")]
		public static void RandomizerOffCommand() { StartTest(RandomizerOffRoutine()); }

		static IEnumerator RandomizerOffRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			bool ok = true;
			RandomizerSettings before = WorldRandomizer.Current.Copy();
			WorldRandomizer.Set(new RandomizerSettings());
			WorldRandomizer.ForgetSeen();
			WorldRandomizer.Rehandle();
			int coloured = WorldRandomizer.ColouredCount, alphas = WorldRandomizer.AlphaCount, moved = WorldRandomizer.MovedCount, extras = WorldRandomizer.ExtrasCount;
			int entries = IslandWorldState.Islands.Count(WorldRandomizer.IsExtras), all = IslandWorldState.Islands.Count;
			// (the randomizer looks at Raft's islands and new animals every few seconds; sailing brings its islands)
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			for (int i = 0; i < 12; i++) { WorldRandomizer.OnSailed(500f, raft); yield return new WaitForSeconds(1f); }
			Check(ref ok, !WorldRandomizer.Current.On, "the randomizer is Off: " + WorldRandomizer.Describe());
			Check(ref ok, WorldRandomizer.ColouredCount == coloured && WorldRandomizer.AlphaCount == alphas, "no animal coloured or made an alpha (" + (WorldRandomizer.ColouredCount - coloured) + ", " + (WorldRandomizer.AlphaCount - alphas) + ")");
			Check(ref ok, WorldRandomizer.MovedCount == moved && WorldRandomizer.ExtrasCount == extras && IslandWorldState.Islands.Count(WorldRandomizer.IsExtras) == entries, "nothing moved or added to Raft's islands (" + (WorldRandomizer.MovedCount - moved) + " moved, " + (WorldRandomizer.ExtrasCount - extras) + " extras)");
			int items = 0, away = 0;
			foreach (Landmark l in WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned && l.landmarkItems != null))
				foreach (LandmarkItem it in l.landmarkItems.Where(x => x != null))
				{
					Vector3? o = WorldRandomizer.OriginalOf(l, it);
					if (!o.HasValue) continue;
					items++;
					if ((it.transform.position - o.Value).magnitude > 0.05f) away++;
				}
			Check(ref ok, away == 0, "every crate and clam the randomizer ever looked at lies where Raft put it (" + items + " looked at, " + away + " elsewhere)");
			Check(ref ok, !WorldRandomizer.OddityDue && IslandWorldState.Islands.Count == all, "6 km sailed: no oddity, lair or large island due or brought (" + (IslandWorldState.Islands.Count - all) + " new islands)");
			WorldRandomizer.Set(before);
			if (ok) Log("PASS: randomizer off is Raft"); else Fail("randomizer off is Raft");
		}

		[ConsoleCommand(name: "CIRandomizerStream", docs: "Dev, world (host): Raft's island and its extras stream out and in (catalogue IR6, IR7): the nearest of Raft's plain islands gets its extras, one of their chests is opened; the raft sails 1.5 km away (Raft's island and the extras go) and back: the chest is still opened, every moved crate and clam lies where it lay before, the island has the same extras")]
		public static void RandomizerStreamCommand() { StartTest(RandomizerStreamRoutine()); }

		static IEnumerator RandomizerStreamRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			RandomizerSettings before = WorldRandomizer.Current.Copy();
			WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 4242 });
			Func<Landmark> nearest = () => WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned && WorldRandomizer.IsNatural(x) && WorldRandomizer.GroundOn(x)).OrderBy(x => (x.transform.position - (CustomIslandSpawner.RaftPosition ?? raft)).sqrMagnitude).FirstOrDefault();
			Landmark l = nearest();
			// (none near: sail on until one of Raft's plain islands is, about 750 m at a time)
			for (int i = 0; i < 8 && l == null; i++) { yield return SailRoutine(30f, 25f); l = nearest(); }
			if (l == null) { Fail("randomizer streaming: none of Raft's plain islands near the raft with its ground on after 6 km"); WorldRandomizer.Set(before); yield break; }
			uint key = WorldRandomizer.SpawnKey(l);
			string kind = WorldRandomizer.KindOf(l);
			// Its extras (with a chest: loot forced on) and its crates handled
			WorldRandomizer.Rehandle();
			RandomizerContent.ForceBigFinds = true; RandomizerContent.ForceFind = "stash"; // (a stash fits on any island: a chest to open)
			yield return WorldRandomizer.ForceExtras(l, 99);
			RandomizerContent.ForceBigFinds = false; RandomizerContent.ForceFind = null;
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => WorldRandomizer.IsExtras(x) && WorldRandomizer.IslandAt(x.Position) == l);
			for (float t = 0; e != null && e.Root == null && !e.Failed && t < 60f; t += 1f) yield return new WaitForSeconds(1f);
			for (float t = 0; t < 10f && !WorldRandomizer.MovedAny(l); t += 1f) yield return new WaitForSeconds(1f);
			if (e == null || e.Root == null) { Fail("randomizer streaming: '" + l.name + "': its extras didn't load"); WorldRandomizer.Set(before); yield break; }
			LootCrate chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c => !c.Looted);
			if (chest != null) { PutPlayerNear(chest.transform); chest.Open(); NoteReader.Close(); }
			var crates = new Dictionary<uint, Vector3>();
			foreach (LandmarkItem it in l.landmarkItems.Where(x => x != null && WorldRandomizer.OriginalOf(l, x).HasValue))
			{
				PickupItem_Networked pn = it.GetComponent<PickupItem_Networked>();
				if (pn != null) crates[pn.ObjectIndex] = l.transform.InverseTransformPoint(it.transform.position);
			}
			string extrasName = e.Name;
			Log("  '" + l.name + "' (" + kind + "): extras '" + extrasName + "', " + (chest != null ? "a chest opened" : "no chest") + ", " + crates.Count + " crates and clams looked at");
			// Away and back: Raft switches the island's ground off about 1 km away and may take it back into its pool
			Network_Player player = RAPI.GetLocalPlayer();
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
			// (where the raft is beside the island: it sails back to exactly there - Raft moves its world as the raft goes, the extras' place follows it)
			Vector3 besides = raftObj != null ? Flat(raftObj.transform.position - e.Position) : Vector3.zero;
			yield return SailRoutine(100f, 25f);
			Log("  2.5 km away: the extras " + (e.Root == null ? "unloaded" : "still there") + ", Raft's island " + (l == null || !l.isSpawned ? "taken back" : WorldRandomizer.GroundOn(l) ? "ground on" : "ground off"));
			yield return SailTo(() => e.Position + besides, 25f);
			Landmark back = null;
			for (float t = 0; t < 60f; t += 1f)
			{
				// (Raft's island where it was - found by its place: Raft's pool may give it another network id)
				back = WorldRandomizer.IslandAt(e.Position);
				if (back != null && (!back.isSpawned || !WorldRandomizer.GroundOn(back))) back = null;
				if (back != null && e.Root != null) break;
				yield return new WaitForSeconds(1f);
			}
			if (back == null)
			{
				// (where Raft's islands are now against where the extras think theirs is: a world shift both must follow)
				Landmark near = WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned).OrderBy(x => new Vector2(x.transform.position.x - e.Position.x, x.transform.position.z - e.Position.z).sqrMagnitude).FirstOrDefault();
				Log("  the extras at " + e.Position + (e.Root != null ? " (their root at " + e.Root.transform.position + ")" : "") + "; the nearest of Raft's islands: " + (near != null ? near.name + " at " + near.transform.position + ", " + new Vector2(near.transform.position.x - e.Position.x, near.transform.position.z - e.Position.z).magnitude.ToString("F1") + " m off, ground " + (WorldRandomizer.GroundOn(near) ? "on" : "off") : "none"));
			}
			// Raft doesn't bring back an island it has sailed far past (its sea ahead is made new): then the extras must stay
			// away too - never floating alone in the sea. When it does bring it back, its extras come back as they were.
			if (back == null)
				Check(ref ok, e.Root == null, "back again: Raft didn't bring its island back (as Raft does with islands sailed far past) - its extras stay away too, nothing floating alone (" + (e.Root == null ? "not loaded" : "LOADED") + ")");
			else
				Check(ref ok, e.Root != null, "back again: Raft's island is back (" + back.name + ") and so are its extras");
			if (back != null) Log("  back at '" + back.name + "': its key " + WorldRandomizer.SpawnKey(back) + " (before " + key + ")");
			if (back != null && e.Root != null)
			{
				for (float t = 0; t < 10f && !WorldRandomizer.MovedAny(back); t += 1f) yield return new WaitForSeconds(1f);
				LootCrate again = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c => chest != null && c.Ordinal == chest.Ordinal);
				Check(ref ok, chest == null || (again != null && again.Looted), "the chest opened before is still opened");
				int same = 0, moved = 0, missing = 0;
				foreach (KeyValuePair<uint, Vector3> c in crates)
				{
					LandmarkItem it = back.landmarkItems.FirstOrDefault(x => x != null && x.GetComponent<PickupItem_Networked>() != null && x.GetComponent<PickupItem_Networked>().ObjectIndex == c.Key);
					if (it == null) { missing++; continue; }
					if ((back.transform.InverseTransformPoint(it.transform.position) - c.Value).magnitude < 0.1f) same++; else moved++;
				}
				Check(ref ok, moved == 0, "every crate and clam lies where it lay before: " + same + " the same, " + moved + " elsewhere, " + missing + " picked up or not spawned");
				Check(ref ok, IslandWorldState.Islands.Count(x => WorldRandomizer.IsExtras(x) && (x.Position - e.Position).magnitude < 10f) == 1, "the island has its one extras again, not a second");
			}
			IslandWorldState.RemoveIds(new[] { e.Id }, true);
			WorldRandomizer.Set(before);
			if (ok) Log("PASS: randomizer streaming"); else Fail("randomizer streaming");
		}

		[ConsoleCommand(name: "CIColourHit", docs: "Dev, world (host, the randomizer on): a coloured animal near the raft hit by a player (Raft's DamageEntity, 1 health): Raft's damage flash plays and its colour is still there 2 s later (catalogue IR13)")]
		public static void ColourHitCommand() { StartTest(ColourHitRoutine()); }

		static string LookOf(AI_NetworkBehaviour a)
		{
			var parts = new List<string>();
			foreach (Renderer r in WorldRandomizer.BodyOf(a))
			{
				var block = new MaterialPropertyBlock();
				r.GetPropertyBlock(block);
				foreach (string p in new[] { "_Color", "_BaseColor", "_Tint" }) { Color c = block.GetColor(p); if (c != default(Color)) parts.Add(p + "=" + ColorUtility.ToHtmlStringRGB(c)); }
				foreach (string p in new[] { "_Diffuse", "_MainTex" }) { Texture t = block.GetTexture(p); if (t != null) parts.Add(p + "=" + t.name); }
				Material m = r.sharedMaterial;
				if (m != null && m.HasProperty("_Color")) parts.Add("material=" + ColorUtility.ToHtmlStringRGB(m.color));
			}
			return string.Join(" ", parts.ToArray());
		}

		static IEnumerator ColourHitRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			AI_NetworkBehaviour a = null;
			for (float t = 0; t < 30f && a == null; t += 1f)
			{
				a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(x => x != null && x.networkEntity != null && !x.networkEntity.IsDead && x.behaviourType != AI_NetworkBehaviourType.Shark &&
					WorldRandomizer.VariantOfIndex.ContainsKey(x.ObjectIndex) && WorldRandomizer.VariantOfIndex[x.ObjectIndex] != null).OrderBy(x => (x.transform.position - raft).sqrMagnitude).FirstOrDefault();
				if (a == null) yield return new WaitForSeconds(1f);
			}
			// (which animals are near depends on where the raft is: a coloured shark will do, else Wild for a moment until a
			// new animal comes with a look)
			RandomizerSettings was = null;
			Func<bool, AI_NetworkBehaviour> find = sharks => UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(x => x != null && x.networkEntity != null && !x.networkEntity.IsDead &&
				(sharks || x.behaviourType != AI_NetworkBehaviourType.Shark) && WorldRandomizer.VariantOfIndex.ContainsKey(x.ObjectIndex) && WorldRandomizer.VariantOfIndex[x.ObjectIndex] != null)
				.OrderBy(x => (x.transform.position - raft).sqrMagnitude).FirstOrDefault();
			if (a == null) a = find(true);
			if (a == null)
			{
				was = WorldRandomizer.Current.Copy();
				WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = was.Seed != 0 ? was.Seed : 777 });
				for (float t = 0; t < 120f && a == null; t += 2f) { yield return SailRoutine(2f, 15f); a = find(true); }
			}
			if (was != null && a == null) WorldRandomizer.Set(was);
			if (a == null) { Fail("no coloured animal (the randomizer on, Wild, near Raft's islands)"); yield break; }
			string look = LookOf(a), variant = WorldRandomizer.VariantOfIndex[a.ObjectIndex];
			Network_Host host = ComponentManager<Network_Host>.Value;
			float hp = a.networkEntity.stat_health.Value;
			host.DamageEntity(a.networkEntity, a.transform, 1f, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			yield return new WaitForSeconds(0.1f);
			string during = LookOf(a);
			yield return new WaitForSeconds(2f);
			string after = a != null ? LookOf(a) : "(gone)";
			bool ok = true;
			Check(ref ok, a != null && a.networkEntity.stat_health.Value < hp, "the " + a.behaviourType + " (" + variant + ") was hit: health " + hp.ToString("F0") + " -> " + a.networkEntity.stat_health.Value.ToString("F0"));
			Check(ref ok, after == look, "its colour 2 s after the hit is its colour before (" + look + ")" + (after == look ? "" : " - now " + after) + (during != look ? "; during the flash: " + during : ""));
			if (was != null) WorldRandomizer.Set(was);
			if (ok) Log("PASS: colour after a hit"); else Fail("colour after a hit");
		}

		[ConsoleCommand(name: "CIRandomizerTypes", docs: "Dev, world (host): every oddity kind, the boss lair and a large island loaded in a world in turn (catalogue IR12): each loads, its set pieces from other scenes load on demand, nothing is missing from the catalog, no error; pictures shot_type_*")]
		public static void RandomizerTypesCommand() { StartTest(RandomizerTypesRoutine()); }

		static IEnumerator RandomizerTypesRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			var problems = new List<string>();
			Application.LogCallback watch = (text, trace, type) =>
			{
				if (text.Contains("is not in the object catalog") || (type == LogType.Exception || type == LogType.Error) && text.Contains("CUSTOM ISLANDS")) problems.Add(text.Length > 160 ? text.Substring(0, 160) : text);
			};
			var types = RandomizerContent.Oddities.Select(o => o[0]).Concat(new[] { "lair", "large" }).ToList();
			foreach (string t in types)
			{
				Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
				if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
				Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
				problems.Clear();
				Application.logMessageReceived += watch;
				IslandWorldState.Entry e = SpawnTypeNear(t, raft);
				for (float s = 0; e != null && e.Root == null && !e.Failed && s < 120f; s += 1f) yield return new WaitForSeconds(1f);
				yield return new WaitForSeconds(2f);
				Application.logMessageReceived -= watch;
				if (e == null) { Check(ref ok, false, t + ": no free sea near the raft"); continue; }
				var refs = e.Root != null ? e.Root.GetComponentsInChildren<IslandObjectRef>(true).ToList() : new List<IslandObjectRef>();
				var pieces = refs.Where(r => r.Props != null && r.Props.ContainsKey("set.piece")).ToList();
				IslandInfoTag tag = e.Root != null ? e.Root.GetComponent<IslandInfoTag>() : null;
				Check(ref ok, e.Root != null && (pieces.Count > 0 || t == "lair") && problems.Count == 0, t + ": " + (e.Root != null ? "'" + (tag != null ? tag.Title : e.Name) + "' loaded, " + pieces.Count + " set pieces" : "didn't load") + ", problems " + problems.Count +
					(problems.Count > 0 ? ": " + string.Join(" | ", problems.Take(3).ToArray()) : ""));
				// (its main set piece: the largest)
				IslandObjectRef main = pieces.Where(r => RaftProps.Get(r.ObjectName) != null).OrderByDescending(r => RaftProps.Get(r.ObjectName).Size.sqrMagnitude).FirstOrDefault();
				if (main != null) yield return ShotOf(player, main.transform, "type_" + t, 10f);
				IslandWorldState.RemoveIds(new[] { e.Id }, true);
				yield return new WaitForSeconds(2f);
			}
			if (ok) Log("PASS: every randomizer island type in a world"); else Fail("every randomizer island type in a world");
		}

		[ConsoleCommand(name: "CIRandomizerSail", docs: "Dev, world (host): sailing brings the randomizer's islands by itself (catalogue IR3): Wild, 40 km sailed in steps of 250 m (OnSailed, with its retry every 200 m) - an oddity, a boss lair and a large island come, none on top of Raft's islands")]
		public static void RandomizerSailCommand() { StartTest(RandomizerSailRoutine()); }

		static IEnumerator RandomizerSailRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			bool ok = true;
			// (sailing is simulated where the raft is: at one of Raft's islands nothing may come - the mod waits for open sea -
			// so sail off it first, as a player would)
			for (int i = 0; i < 6 && ChunkManager.RaftIsInsideChunkPoint; i++) yield return SailRoutine(20f, 20f);
			Check(ref ok, !ChunkManager.RaftIsInsideChunkPoint, "the raft on open sea, away from Raft's islands");
			RandomizerSettings before = WorldRandomizer.Current.Copy();
			WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 777 });
			var known = new HashSet<int>(IslandWorldState.Islands.Select(e => e.Id));
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			var brought = new List<IslandWorldState.Entry>();
			for (int i = 0; i < 160; i++)
			{
				WorldRandomizer.OnSailed(250f, raft);
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(e => !known.Contains(e.Id)).ToList())
				{
					known.Add(e.Id);
					if (!WorldRandomizer.IsExtras(e)) brought.Add(e);
				}
				// (each is placed ahead of the raft: take it away again, so the sea ahead is free for the next)
				foreach (IslandWorldState.Entry e in brought.Where(x => x.Root != null).ToList()) IslandWorldState.RemoveIds(new[] { e.Id }, true);
				yield return new WaitForSeconds(0.25f);
			}
			Func<string, int> count = kind => brought.Count(e => (e.Name ?? "").StartsWith("gen-" + kind + "-") || kind == "oddity" && RandomizerContent.Oddities.Any(o => (e.Name ?? "").StartsWith("gen-" + o[0] + "-")));
			Log("  brought: " + string.Join(", ", brought.Select(e => e.Name).ToArray()));
			Check(ref ok, count("oddity") > 0 && count("lair") > 0 && count("large") > 0, "40 km on Wild: " + count("oddity") + " oddities, " + count("lair") + " lairs, " + count("large") + " large islands");
			// (none on top of Raft's islands: CustomIslandSpawner keeps clear of Raft's island spawn points)
			var near = new List<string>();
			foreach (IslandWorldState.Entry e in brought)
				foreach (Landmark l in WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned))
				{
					float d = new Vector2(l.transform.position.x - e.Position.x, l.transform.position.z - e.Position.z).magnitude;
					if (d < CustomIslandSpawner.LandRadius(e.Name) + 40f) near.Add(e.Name + " " + d.ToString("F0") + " m from " + l.name);
				}
			Check(ref ok, near.Count == 0, "none on top of Raft's islands" + (near.Count > 0 ? ": " + string.Join(", ", near.ToArray()) : ""));
			IslandWorldState.RemoveIds(brought.Select(e => e.Id).ToList(), true);
			WorldRandomizer.Set(before);
			if (ok) Log("PASS: randomizer islands while sailing"); else Fail("randomizer islands while sailing");
		}

		[ConsoleCommand(name: "CIForceAlpha", docs: "Dev, world (host): makes the nearest live warthog (or <kind>) of Raft's own an alpha the way a world does - a seed whose roll makes it one (every player works it out from the seed): CIForceAlpha [kind]. Logs ALPHA <index>")]
		public static void ForceAlphaCommand(string[] args) { StartTest(ForceAlphaRoutine(args != null && args.Length > 0 ? args[0] : "Boar")); }

		static IEnumerator ForceAlphaRoutine(string kind)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			AI_NetworkBehaviourType type;
			try { type = (AI_NetworkBehaviourType)Enum.Parse(typeof(AI_NetworkBehaviourType), kind, true); } catch { Fail("no animal kind '" + kind + "'"); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			// (one of Raft's own animals: those of custom islands keep their builder's looks and are never alphas)
			AI_NetworkBehaviour a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(x => x != null && x.behaviourType == type && x.ObjectIndex != 0 && x.networkEntity != null && !x.networkEntity.IsDead && !CreatureSpawner.IsOnCustomIsland(x))
				.OrderBy(x => (x.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
			if (a == null) { Fail("no live " + type + " in the world (CIRandomizerLarge keep brings warthogs)"); yield break; }
			var s = new RandomizerSettings { Level = RandomizerSettings.Wild };
			int seed = 0;
			for (int tryseed = 1; tryseed < 200000 && seed == 0; tryseed++)
			{
				s.Seed = tryseed;
				WorldRandomizer.Current = s;
				WorldRandomizer.Variant v = WorldRandomizer.VariantOf(type, a.ObjectIndex);
				if (v != null && v.Alpha) seed = tryseed;
			}
			if (seed == 0) { Fail("no seed makes #" + a.ObjectIndex + " an alpha"); yield break; }
			WorldRandomizer.Set(s); // (sent to every player: they work out the same alpha)
			for (float t = 0; t < 10f && !(WorldRandomizer.VariantOfIndex.ContainsKey(a.ObjectIndex) && WorldRandomizer.VariantOfIndex[a.ObjectIndex] == "alpha"); t += 0.5f) yield return new WaitForSeconds(0.5f);
			Log("ALPHA " + a.ObjectIndex + " " + type + " (seed " + seed + "), health " + a.networkEntity.stat_health.Value.ToString("F0") + "/" + a.networkEntity.stat_health.Max.ToString("F0") + ", size " + a.transform.localScale.x.ToString("F2"));
			Log("PASS: alpha forced");
		}

		[ConsoleCommand(name: "CIKillAlpha", docs: "Dev, world (either player): this player defeats the animal with network index <index> as a weapon would (Raft's DamageEntity; a client's goes to the host): CIKillAlpha <index>")]
		public static void KillAlphaCommand(string[] args)
		{
			uint idx;
			if (args == null || args.Length == 0 || !uint.TryParse(args[0], out idx)) { Fail("CIKillAlpha <index>"); return; }
			AI_NetworkBehaviour a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.ObjectIndex == idx);
			if (a == null || a.networkEntity == null) { Fail("no animal #" + idx + " here"); return; }
			PutPlayerNear(a.transform, 3f);
			ComponentManager<Network_Host>.Value.DamageEntity(a.networkEntity, a.transform, 99999f, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("Killed #" + idx + " (" + (Raft_Network.IsHost ? "host" : "client: sent to the host") + ")");
			Log("PASS: kill");
		}

		[ConsoleCommand(name: "CIAlphaSpoils", docs: "Dev, world (either player): the dropped items lying around the animal with network index <index> (dead or alive) on this machine: CIAlphaSpoils <index>")]
		public static void AlphaSpoilsCommand(string[] args)
		{
			uint idx;
			if (args == null || args.Length == 0 || !uint.TryParse(args[0], out idx)) { Fail("CIAlphaSpoils <index>"); return; }
			AI_NetworkBehaviour a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.ObjectIndex == idx);
			if (a == null) { Fail("no animal #" + idx + " here"); return; }
			// (the spoils: an alpha's kinds of items, dropped where it died - the body may slide or be carried off after)
			var spoil = new System.Text.RegularExpressions.Regex(@"^(Head_\w+|Raw_GenericMeat|Raw_Shark|Leather|Feather|Wool|TitaniumIngot)$");
			var near = UnityEngine.Object.FindObjectsOfType<PickupItem>().Where(p => p != null && p.isDropped && p.itemInstance != null && p.itemInstance.baseItem != null && spoil.IsMatch(p.itemInstance.baseItem.UniqueName) && (p.transform.position - a.transform.position).magnitude < 30f).ToList();
			var names = near.Select(p => p.itemInstance != null && p.itemInstance.baseItem != null ? p.itemInstance.baseItem.UniqueName + "*" + p.itemInstance.Amount : p.name).OrderBy(n => n).ToList();
			Log("SPOILS #" + idx + " " + (a.networkEntity != null && a.networkEntity.IsDead ? "dead" : "alive") + ": " + near.Count + " dropped item(s): " + string.Join(", ", names.ToArray()));
			Log("PASS: spoils");
		}

		[ConsoleCommand(name: "CIAlphaHealth", docs: "Dev, world (either player): the health of the animal with network index <index> on this machine, as HEALTH #<index> <health>/<max> (an alpha that turned while player 2 was connected has the host's numbers exactly there: message AlphaHealth): CIAlphaHealth <index>")]
		public static void AlphaHealthCommand(string[] args)
		{
			uint idx;
			if (args == null || args.Length == 0 || !uint.TryParse(args[0], out idx)) { Fail("CIAlphaHealth <index>"); return; }
			AI_NetworkBehaviour a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.ObjectIndex == idx);
			if (a == null || a.networkEntity == null || a.networkEntity.stat_health == null) { Fail("no animal #" + idx + " here"); return; }
			Stat_Health h = a.networkEntity.stat_health;
			Log("HEALTH #" + idx + " " + h.Value.ToString("F1") + "/" + h.Max.ToString("F1") + " (" + a.behaviourType + (Raft_Network.IsHost ? ", host" : ", player 2") + ")");
			Log("PASS: alpha health");
		}

		[ConsoleCommand(name: "CIModeWorld", docs: "Dev, world (host): the randomizer's animals follow Raft's game mode in a world (catalogue IR15): a large island loaded in Creative has no puffer fish or screechers (Raft's own islands in Creative have none), its warthogs and animals to catch are there; in Peaceful the puffer fish are there too (as on Raft's islands). The world's mode is put back after")]
		public static void ModeWorldCommand() { StartTest(ModeWorldRoutine()); }

		static IEnumerator ModeWorldRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			GameMode before = GameManager.GameMode;
			Network_Player player = RAPI.GetLocalPlayer();
			foreach (GameMode mode in new[] { GameMode.Creative, GameMode.Peaceful })
			{
				GameModeValueManager.SelectCurrentGameMode(mode);
				Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
				if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
				IslandWorldState.Entry e = SpawnTypeNear("large", CustomIslandSpawner.RaftPosition ?? Vector3.zero);
				for (float t = 0; e != null && e.Root == null && !e.Failed && t < 120f; t += 1f) yield return new WaitForSeconds(1f);
				if (e == null || e.Root == null) { Check(ref ok, false, mode + ": the large island didn't load"); GameModeValueManager.SelectCurrentGameMode(before); yield break; }
				yield return new WaitForSeconds(15f);
				float reach = CustomIslandSpawner.LandRadius(e.Name) + 60f;
				var alive = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead &&
					new Vector2(a.transform.position.x - e.Position.x, a.transform.position.z - e.Position.z).magnitude < reach).GroupBy(a => a.behaviourType).ToDictionary(g => g.Key, g => g.Count());
				Func<AI_NetworkBehaviourType, int> n = t => alive.ContainsKey(t) ? alive[t] : 0;
				var spots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null).GroupBy(p => p.Kind.Type).ToDictionary(g => g.Key, g => g.Count());
				string seen = string.Join(", ", alive.Select(kv => kv.Key + " " + kv.Value).ToArray());
				int others = alive.Where(kv => kv.Key != AI_NetworkBehaviourType.PufferFish && kv.Key != AI_NetworkBehaviourType.StoneBird).Sum(kv => kv.Value);
				if (mode == GameMode.Creative)
					Check(ref ok, n(AI_NetworkBehaviourType.PufferFish) == 0 && n(AI_NetworkBehaviourType.StoneBird) == 0 && others > 0, "Creative: no puffer fish, no screechers (as Raft), the rest there: " + seen);
				else
					Check(ref ok, (n(AI_NetworkBehaviourType.PufferFish) > 0 || !spots.ContainsKey(AI_NetworkBehaviourType.PufferFish)) && others > 0, "Peaceful: puffer fish and the rest there (as Raft): " + seen);
				IslandWorldState.RemoveIds(new[] { e.Id }, true);
				yield return new WaitForSeconds(3f);
			}
			GameModeValueManager.SelectCurrentGameMode(before);
			if (ok) Log("PASS: creatures follow the game mode"); else Fail("creatures follow the game mode");
		}

		[ConsoleCommand(name: "CITreasureHunt", docs: "Dev, world (host): the randomizer's treasure hunt on one of Raft's islands played through (catalogue IR4): the nearest plain island gets it (forced), the bottle's map read, the X reached, the buried chest opened - the quest moves on at each and is done. CITreasureHunt [keep]; CITreasureHunt half = only the map read, kept (IR4); CITreasureHunt rest <name> = that hunt played on by this player (after a load, or player 2); CITreasureHunt state <name> = its step here (3 = done); logs Treasure extras: '<name>'")]
		public static void TreasureHuntCommand(string[] args)
		{
			args = args ?? new string[0];
			// (IR4: "half" = the map read only, all kept, for a save and load; "rest <name>" = the rest played by this player,
			// host or not; "state <name>" = where that hunt is on this machine)
			if (args.Length >= 2 && (args[0] == "rest" || args[0] == "state")) { StartTest(TreasureHuntRestRoutine(string.Join(" ", args.Skip(1).ToArray()), args[0] == "rest")); return; }
			StartTest(TreasureHuntRoutine(args.Any(a => a == "keep" || a == "half"), args.Any(a => a == "half")));
		}

		static IEnumerator TreasureHuntRoutine(bool keep, bool half = false)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			RandomizerSettings before = WorldRandomizer.Current.Copy();
			if (!WorldRandomizer.Current.Has(RandomizerSettings.Finds)) WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Normal, Seed = 99 });
			// (a treasure hunt needs a spot 3 m up for its X and a beach for the bottle: a low, flat island gets a stash
			// instead, as it should - then the next nearest island is tried)
			// (up to 8: near the raft there can be four low, flat small islands in a row - seen 2026-10-01)
			List<Landmark> near = WorldManager.AllLandmarks.Where(lm => lm != null && lm.isSpawned && WorldRandomizer.IsNatural(lm)).OrderBy(lm => (lm.transform.position - raft).sqrMagnitude).Take(8).ToList();
			if (near.Count == 0) { Fail("none of Raft's plain islands near the raft (sail on)"); WorldRandomizer.Set(before); yield break; }
			Landmark l = null;
			IslandWorldState.Entry e = null;
			IslandQuest q = null;
			CustomNote map = null;
			TriggerZone x = null;
			LootCrate chest = null;
			var tried = new List<string>();
			foreach (Landmark candidate in near)
			{
				l = candidate;
				yield return PutPlayer(player, l.transform.position + Vector3.up * 60f, false);
				for (float t = 0; t < 30f && !WorldRandomizer.GroundOn(l); t += 1f) yield return new WaitForSeconds(1f);
				// (the player's arrival lets the randomizer give the island its own extras first: let that finish, the forced
				// ones then replace them - found by their name, never the arrival's)
				yield return new WaitForSeconds(6f);
				var extrasBefore = new HashSet<int>(IslandWorldState.Islands.Select(i => i.Id));
				string forcedName = WorldRandomizer.ExtrasPrefix + WorldRandomizer.Current.Seed + "-" + (WorldRandomizer.SpawnKey(l) ^ 4711u);
				Func<IslandWorldState.Entry> mine = () => IslandWorldState.Islands.LastOrDefault(i => WorldRandomizer.IsExtras(i) && !extrasBefore.Contains(i.Id) && i.Name == forcedName);
				RandomizerContent.ForceFind = "treasure";
				StartTest(WorldRandomizer.ForceExtras(l, 4711));
				for (float t = 0; t < 30f && mine() == null; t += 0.5f) yield return new WaitForSeconds(0.5f);
				RandomizerContent.ForceFind = null;
				e = mine();
				for (float t = 0; e != null && e.Root == null && !e.Failed && t < 60f; t += 1f) yield return new WaitForSeconds(1f);
				if (e == null || e.Root == null) { tried.Add(l.name + ": the extras didn't load"); continue; }
				Log("Treasure extras: '" + e.Name + "'");
				q = QuestTracker.QuestOf(e);
				map = e.Root.GetComponentsInChildren<CustomNote>(true).FirstOrDefault(n => n.GetComponent<LootCrate>() == null && (n.Title ?? "") == "Treasure map");
				x = e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "x");
				chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle) == "Buried treasure"; });
				if (map != null && x != null && chest != null) break;
				tried.Add(l.name + ": no room for a treasure hunt (a stash instead)");
			}
			if (tried.Count > 0) Log("  (islands tried first: " + string.Join("; ", tried.ToArray()) + ")");
			if (e == null || e.Root == null) { Fail("the extras didn't load: " + string.Join("; ", tried.ToArray())); WorldRandomizer.Set(before); yield break; }
			Check(ref ok, q.Exists && q.Steps.Count == 3 && map != null && x != null && chest != null, "'" + l.name + "' has a treasure hunt: quest '" + q.ShownTitle + "' (" + q.Steps.Count + " steps), the map in a bottle " + (map != null) + ", the X " + (x != null) + ", the buried chest " + (chest != null));
			if (map == null || x == null || chest == null) { WorldRandomizer.Set(before); yield break; }
			// Played as a player would: the map, the X, the chest
			PutPlayerNear(map.transform);
			NoteReader.Open(map); NoteReader.Close();
			yield return new WaitForSeconds(1f);
			int s1 = QuestTracker.StepOf(e);
			if (half)
			{
				Check(ref ok, s1 == 1 && !x.HasFired && !chest.Looted, "half done: the map read (step " + s1 + "), the X and the chest left");
				Log("TREASURE hunt kept: '" + e.Name + "'");
				Raft rh = UnityEngine.Object.FindObjectOfType<Raft>();
				if (rh != null) yield return PutPlayer(player, rh.transform.position + Vector3.up * 2f, false);
				if (ok) Log("PASS: treasure hunt half"); else Fail("treasure hunt half");
				yield break;
			}
			yield return PutPlayer(player, x.transform.position + Vector3.up * 1.5f, false);
			yield return new WaitForSeconds(2f);
			int s2 = QuestTracker.StepOf(e);
			PutPlayerNear(chest.transform);
			List<string> got = chest.Open(); NoteReader.Close();
			yield return new WaitForSeconds(1f);
			int s3 = QuestTracker.StepOf(e);
			Check(ref ok, s1 == 1 && s2 == 2 && s3 == 3, "the quest moves on: the map read " + s1 + ", the X reached " + s2 + ", the chest opened " + s3 + " (1, 2, 3 = done)");
			Check(ref ok, got.Count > 0 && x.HasFired && chest.Looted, "the treasure: " + string.Join(", ", got.ToArray()));
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
			if (!keep) { IslandWorldState.RemoveIds(new[] { e.Id }, true); WorldRandomizer.Set(before); }
			if (ok) Log("PASS: treasure hunt"); else Fail("treasure hunt");
		}
		/// <summary>IR4: a treasure hunt left half done (the map read) - after a save and load, or by another player - played on.</summary>
		static IEnumerator TreasureHuntRestRoutine(string name, bool play)
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			IslandWorldState.Entry e = null;
			for (float t = 0; t < 20f && (e = IslandWorldState.Islands.FirstOrDefault(i => i.Name == name)) == null; t += 1f) yield return new WaitForSeconds(1f);
			if (e == null) { Fail("no island '" + name + "' in this world here"); yield break; }
			if (!play)
			{
				TriggerZone zx = e.Root != null ? e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "x") : null;
				Log("TREASURE '" + name + "': step " + QuestTracker.StepOf(e) + (zx != null ? ", the X " + (zx.HasFired ? "reached" : "not reached") : ", not loaded here"));
				if (QuestTracker.StepOf(e) == 3) Log("PASS: treasure hunt state"); else Fail("treasure hunt state: step " + QuestTracker.StepOf(e) + " (3 wanted)");
				yield break;
			}
			if (e.Root == null) yield return PutPlayer(player, e.Position + Vector3.up * 60f, false);
			for (float t = 0; t < 60f && e.Root == null; t += 1f) yield return new WaitForSeconds(1f);
			if (e.Root == null) { Fail("'" + name + "' didn't load here"); yield break; }
			yield return new WaitForSeconds(2f);
			TriggerZone x = e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "x");
			LootCrate chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle) == "Buried treasure"; });
			if (x == null || chest == null) { Fail("'" + name + "': the X " + (x != null) + ", the chest " + (chest != null)); yield break; }
			int s1 = QuestTracker.StepOf(e);
			Check(ref ok, s1 == 1 && !x.HasFired && !chest.Looted, "left half done: step " + s1 + " (the map read), the X " + (x.HasFired ? "reached" : "not reached") + ", the chest " + (chest.Looted ? "opened" : "shut"));
			yield return PutPlayer(player, x.transform.position + Vector3.up * 1.5f, false);
			int s2 = 0;
			for (float t = 0; t < 6f && (s2 = QuestTracker.StepOf(e)) < 2; t += 0.5f) yield return new WaitForSeconds(0.5f);
			PutPlayerNear(chest.transform);
			List<string> got = chest.Open(); NoteReader.Close();
			int s3 = 0;
			for (float t = 0; t < 6f && (s3 = QuestTracker.StepOf(e)) < 3; t += 0.5f) yield return new WaitForSeconds(0.5f);
			Check(ref ok, s2 == 2 && s3 == 3, "played on: the X reached " + s2 + ", the chest opened " + s3 + " (2, 3 = done)");
			if (got.Count == 0) got = chest.LastGiven; // (a client opens when the host says yes, a moment later)
			Check(ref ok, got.Count > 0, "the treasure: " + string.Join(", ", got.ToArray()));
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
			if (ok) Log("PASS: treasure hunt rest"); else Fail("treasure hunt rest");
		}

		[ConsoleCommand(name: "CIRandomizerPending", docs: "Dev, main menu: the randomizer settings the next new world gets (as if chosen in the New Game box): CIRandomizerPending <off|light|normal|wild> [-part ...]")]
		public static void RandomizerPendingCommand(string[] args)
		{
			var s = new RandomizerSettings();
			foreach (string a in args ?? new string[0])
			{
				int l = Array.FindIndex(RandomizerSettings.LevelNames, n => n.Equals(a, StringComparison.OrdinalIgnoreCase));
				if (l >= 0) s.Level = l;
				else if (a.StartsWith("-")) s.Disabled.Add(a.Substring(1).ToLowerInvariant());
			}
			WorldRandomizer.Pending = s;
			Log("The next new world's randomizer: " + s.Describe());
			Log("PASS: randomizer pending");
		}

		#endregion

		[ConsoleCommand(name: "CIProbeGround", docs: "Dev, world: what the randomizer's ground finder sees on Raft's spawned plain islands (land, heights, slopes), and the shark's shader properties")]
		public static void ProbeGroundCommand()
		{
			foreach (Landmark l in WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned && WorldRandomizer.IsNatural(x)))
			{
				var g = new LandGround(l);
				int hits = 0, dry = 0, beach = 0, under = 0;
				float top = float.MinValue;
				var owners = new Dictionary<string, int>();
				for (float x = -g.Radius; x <= g.Radius; x += 8f)
					for (float z = -g.Radius; z <= g.Radius; z += 8f)
					{
						Vector3 p, n;
						if (!g.Hit(g.Centre.x + x, g.Centre.z + z, out p, out n)) continue;
						hits++;
						float slope = Vector3.Angle(n, Vector3.up);
						if (p.y > 1.2f && slope < 26f) dry++;
						if (p.y > 0.25f && p.y < 2.2f) beach++;
						if (p.y < -3f && p.y > -14f) under++;
						top = Mathf.Max(top, p.y);
					}
				foreach (Collider c in l.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.GetComponentInParent<LandmarkItem>() == null))
				{
					string k = c.GetType().Name + " L" + c.gameObject.layer;
					int v; owners.TryGetValue(k, out v); owners[k] = v + 1;
				}
				Log("ground '" + l.name + "' at " + l.transform.position.ToString("F0") + ": centre " + g.Centre.ToString("F0") + " radius " + g.Radius.ToString("F0") + ", " + hits + " hits, top " + top.ToString("F1") +
					", dry " + dry + ", beach " + beach + ", under water " + under + "; colliders " + string.Join(", ", owners.Select(kv => kv.Key + " x" + kv.Value).ToArray()));
				// What a ray straight down at the middle hits
				RaycastHit[] all = Physics.RaycastAll(new Vector3(l.transform.position.x, 250f, l.transform.position.z), Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
				Log("  down the middle: " + string.Join("; ", all.OrderBy(h => h.distance).Take(5).Select(h => h.collider.name + " (" + h.collider.GetType().Name + ", child " + h.collider.transform.IsChildOf(l.transform) + ") y " + h.point.y.ToString("F1")).ToArray()));
			}
			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a.behaviourType == AI_NetworkBehaviourType.Shark))
				foreach (Renderer r in WorldRandomizer.BodyOf(ai))
					foreach (Material m in r.sharedMaterials.Where(m => m != null))
					{
						var props = new List<string>();
						for (int i = 0; i < m.shader.GetPropertyCount(); i++) props.Add(m.shader.GetPropertyName(i) + ":" + m.shader.GetPropertyType(i));
						Log("shark material " + m.name + " / " + m.shader.name + ": " + string.Join(", ", props.ToArray()));
					}
			Log("PASS: probed ground");
		}

		[ConsoleCommand(name: "CIPlanPickerWindow", docs: "Dev, main menu: the New Game box's plan picker (CT5) - every plan listed, pictures, select / double-click / keys / search / cancel; shot_planpicker.png")]
		public static void PlanPickerWindowCommand() { StartTest(PlanPickerWindowTest()); }

		static IEnumerator PlanPickerWindowTest()
		{
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("plan picker: no New Game box (main menu?)"); yield break; }
			string before = WorldDirector.PendingPlan;
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			yield return new WaitForSecondsRealtime(1f);
			bool ok = true;
			Transform row = box.transform.Find("CustomIslands_Plan");
			UnityEngine.UI.Button choose = row != null ? row.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.name == "Button_ChoosePlan") : null;
			Check(ref ok, choose != null && row.GetComponentsInChildren<UnityEngine.UI.Button>(true).All(x => x.name != "Drop_Plan"), "the box has Choose plan... and no drop-down list");
			Check(ref ok, ChosenPlanText(row) == NewWorldOptions.Selected, "the box shows the chosen plan (" + ChosenPlanText(row) + ")");
			if (choose == null) { Fail("plan picker"); yield break; }
			string start = NewWorldOptions.Selected;
			choose.onClick.Invoke();
			yield return null;
			List<string> plans = WorldPlan.All(), shown = PlanPickerWindow.Shown();
			Check(ref ok, PlanPickerWindow.IsOpen, "Choose plan... opens the window");
			Check(ref ok, shown.Count == plans.Count && plans.All(shown.Contains), "every plan is listed (" + shown.Count + " of " + plans.Count + ")");
			Check(ref ok, PlanPickerWindow.Highlighted == start, "the box's plan is selected when it opens (" + PlanPickerWindow.Highlighted + ")");
			// (fits the screen: the panel's corners inside it)
			RectTransform panel = PlanPickerWindow.Root.Find("Panel") as RectTransform;
			var pc = new Vector3[4]; if (panel != null) panel.GetWorldCorners(pc);
			Canvas cv = PlanPickerWindow.Root.GetComponent<Canvas>();
			Vector2 lo = RectTransformUtility.WorldToScreenPoint(cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera, pc[0]), hi = RectTransformUtility.WorldToScreenPoint(cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera, pc[2]);
			Check(ref ok, panel != null && lo.x >= -1 && lo.y >= -1 && hi.x <= Screen.width + 1 && hi.y <= Screen.height + 1, "the window fits the screen (" + Screen.width + "x" + Screen.height + ": " + lo + "-" + hi + ")");
			// every plan: its row selects it, the title and a picture follow
			var noPicture = new List<string>(); var maps = new List<string>(); var wrong = new List<string>();
			UnityEngine.UI.Text title = PlanPickerWindow.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true).FirstOrDefault(t => t.name == "PlanTitle");
			foreach (string n in plans)
			{
				UnityEngine.UI.Button r = PlanPickerWindow.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.name == PlanPickerWindow.RowPrefix + n);
				if (r == null) { wrong.Add(n + " (no row)"); continue; }
				r.onClick.Invoke();
				yield return null;
				if (PlanPickerWindow.Highlighted != n || title == null || title.text != n) wrong.Add(n);
				if (PlanPickerWindow.Picture == null) noPicture.Add(n);
				else if (!PlanPickerWindow.PictureIsPlaceholder) maps.Add(n);
				yield return new WaitForSecondsRealtime(0.35f); // (no double-click)
			}
			Check(ref ok, wrong.Count == 0, "clicking each plan shows it" + (wrong.Count > 0 ? " - not: " + string.Join(", ", wrong.ToArray()) : ""));
			Check(ref ok, noPicture.Count == 0 && maps.Count > 0, "each plan has a picture or the placeholder; " + maps.Count + " with a picture (" + string.Join(", ", maps.Take(8).ToArray()) + ")");
			Check(ref ok, NewWorldOptions.Selected == start, "clicking alone doesn't choose (" + NewWorldOptions.Selected + ")");
			// keys
			string h0 = PlanPickerWindow.Highlighted;
			PlanPickerWindow.Move(-1);
			string h1 = PlanPickerWindow.Highlighted;
			PlanPickerWindow.Move(1);
			Check(ref ok, h1 != h0 && PlanPickerWindow.Highlighted == h0, "up/down move the selection (" + h0 + " > " + h1 + " > " + PlanPickerWindow.Highlighted + ")");
			// search
			string pick = maps.FirstOrDefault(m => m != start) ?? plans.First(m => m != start);
			PlanPickerWindow.Search.text = pick;
			yield return null;
			List<string> found = PlanPickerWindow.Shown();
			Check(ref ok, found.Contains(pick) && found.Count < plans.Count, "search '" + pick + "' shows " + found.Count + " plan(s), with it");
			PlanPickerWindow.Search.text = "zzz-no-such-plan";
			yield return null;
			Check(ref ok, PlanPickerWindow.Shown().Count == 0, "a search with no match lists none");
			PlanPickerWindow.Search.text = "";
			yield return null;
			// picture of a plan with a picture
			PlanPickerWindow.Select(pick);
			yield return new WaitForSecondsRealtime(0.5f);
			string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(DynamicIslands.assetpath, "shot_planpicker.png"));
			ScreenCapture.CaptureScreenshot(file);
			Log("Screenshot " + file);
			yield return new WaitForSecondsRealtime(0.5f);
			// cancel keeps the choice
			UnityEngine.UI.Button cancel = PlanPickerWindow.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.name == "Button_Cancel");
			if (cancel != null) cancel.onClick.Invoke();
			Check(ref ok, cancel != null && !PlanPickerWindow.IsOpen && NewWorldOptions.Selected == start, "Cancel closes it and keeps " + start);
			// double-click chooses
			choose.onClick.Invoke();
			yield return null;
			UnityEngine.UI.Button pr = PlanPickerWindow.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.name == PlanPickerWindow.RowPrefix + pick);
			if (pr != null) { pr.onClick.Invoke(); pr.onClick.Invoke(); }
			yield return null;
			Check(ref ok, !PlanPickerWindow.IsOpen && NewWorldOptions.Selected == pick && ChosenPlanText(row) == pick, "a double-click chooses '" + pick + "' (" + NewWorldOptions.Selected + ", the box shows " + ChosenPlanText(row) + ")");
			// Select chooses
			Check(ref ok, ChoosePlanInPicker(choose, start) && ChosenPlanText(row) == start, "Select chooses '" + start + "' again");
			WorldDirector.PendingPlan = before;
			NewWorldOptions.Refresh();
			box.Button_Close();
			if (ok) Log("PASS: plan picker window"); else Fail("plan picker window");
		}

		[ConsoleCommand(name: "CINewGameBoxShot", docs: "Dev, main menu: opens Raft's New Game box with the plan and randomizer panels, checks they fit inside it, takes shot_newgame.png and closes it")]
		public static void NewGameBoxShotCommand() { StartTest(NewGameBoxShot()); }

		static IEnumerator NewGameBoxShot()
		{
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("no New Game box (main menu?)"); yield break; }
			RandomizerSettings pending = WorldRandomizer.Pending;
			var shown = new RandomizerSettings { Level = RandomizerSettings.Normal };
			shown.Disabled.Add(RandomizerSettings.Alphas);
			WorldRandomizer.Pending = shown;
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			yield return new WaitForSeconds(1f);
			bool ok = true;
			RectTransform boxRect = (RectTransform)box.transform;
			// (the box keeps Raft's own things, the plan and the World settings button; the rest is in the window)
			foreach (string n in new[] { "CustomIslands_Plan", WorldSettingsWindow.ButtonName })
			{
				RectTransform r = box.transform.Find(n) as RectTransform;
				Vector3[] c = new Vector3[4], b = new Vector3[4];
				if (r != null) { r.GetWorldCorners(c); boxRect.GetWorldCorners(b); }
				bool inside = r != null && c[0].x >= b[0].x - 1f && c[0].y >= b[0].y - 1f && c[2].x <= b[2].x + 1f && c[2].y <= b[2].y + 1f;
				Check(ref ok, r != null && r.gameObject.activeInHierarchy && inside, n + (r == null ? " missing" : inside ? " inside the box" : " sticks out of the box"));
			}
			// (CT2: the heading on one line, the description in full or with its hint, nothing of Raft's over the panel)
			RectTransform plan = box.transform.Find("CustomIslands_Plan") as RectTransform;
			if (plan != null)
			{
				UnityEngine.UI.Text title = plan.GetComponentsInChildren<UnityEngine.UI.Text>(true).FirstOrDefault(t => t.name == "Title");
				UnityEngine.UI.Text detail = plan.GetComponentsInChildren<UnityEngine.UI.Text>(true).FirstOrDefault(t => t.name == "Detail");
				Check(ref ok, title != null && title.cachedTextGenerator.lineCount == 1, "the plan heading is on one line");
				Check(ref ok, detail != null && detail.raycastTarget, "the plan description has its whole text as a hover hint");
				var pc = new Vector3[4]; plan.GetWorldCorners(pc);
				foreach (UnityEngine.UI.Text t in box.GetComponentsInChildren<UnityEngine.UI.Text>(true).Where(t => !t.transform.IsChildOf(plan) && t.text.Length > 0))
				{
					var tc = new Vector3[4]; t.rectTransform.GetWorldCorners(tc);
					// (where the letters are, not the label's whole rect)
					float pw = Mathf.Min(t.preferredWidth * t.rectTransform.lossyScale.x, tc[2].x - tc[0].x), mid = (tc[0].x + tc[2].x) / 2f;
					int col = (int)t.alignment % 3;
					float x0 = col == 0 ? tc[0].x : col == 1 ? mid - pw / 2f : tc[2].x - pw, x1 = x0 + pw;
					bool over = x0 < pc[2].x && x1 > pc[0].x && tc[0].y < pc[2].y && tc[2].y > pc[0].y;
					if (over || t.text.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0)
						Log("  text \"" + t.text.Replace("\n", " ") + "\" (" + t.name + ", active " + t.gameObject.activeInHierarchy + ") at " + tc[0] + "-" + tc[2] + (over ? " OVER the plan panel " + pc[0] + "-" + pc[2] : ""));
					if (over && t.gameObject.activeInHierarchy) Check(ref ok, false, "Raft's text \"" + t.text + "\" lies over the plan panel");
				}
			}
			Check(ref ok, box.transform.Find("CustomIslands_Randomizer") == null && box.transform.Find(NewWorldRulesBox.PanelName) == null, "the box is Raft's own: no randomizer and no world rules in it");
			int buttons = (NewWorldOptions.LevelButton != null ? 1 : 0) + NewWorldOptions.PartButtons.Count;
			bool inWindow = NewWorldOptions.LevelButton != null && WorldSettingsWindow.Window != null && NewWorldOptions.LevelButton.transform.IsChildOf(WorldSettingsWindow.Window);
			Check(ref ok, buttons == 1 + RandomizerSettings.Features.Length && inWindow, buttons + " randomizer buttons (level + " + RandomizerSettings.Features.Length + " parts), in the World settings window");
			string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(DynamicIslands.assetpath, "shot_newgame.png"));
			ScreenCapture.CaptureScreenshot(file);
			Log("Screenshot " + file);
			yield return new WaitForSeconds(1f);
			// (and the World settings window)
			WorldSettingsWindow.Open();
			yield return new WaitForSeconds(0.5f);
			Screenshot(new[] { "newgame_settings" });
			yield return new WaitForSeconds(1f);
			WorldSettingsWindow.Close();
			box.gameObject.SetActive(false);
			WorldRandomizer.Pending = pending;
			if (ok) Log("PASS: New Game box"); else Fail("New Game box");
		}

		[ConsoleCommand(name: "CIRandomizerChips", docs: "Dev, main menu: the World settings window's randomizer parts clicked one by one as a player does (each click leaves its part out and greys it, a second puts it back and lights it), at Off every part is greyed and can't be clicked; then leaves Normal without Alphas and Bosses for the next new world - CIRandomizerChipsCheck in that world (IR5)")]
		public static void RandomizerChipsCommand() { StartTest(RandomizerChipsRoutine()); }

		static IEnumerator RandomizerChipsRoutine()
		{
			NewGameBox box = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null || LoadSceneManager.IsGameSceneLoaded) { Fail("no New Game box (main menu?)"); yield break; }
			bool ok = true;
			WorldRandomizer.Pending = new RandomizerSettings { Level = RandomizerSettings.Normal };
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open();
			yield return new WaitForSeconds(0.5f);
			WorldSettingsWindow.Open();
			yield return new WaitForSeconds(0.5f);
			IList<UnityEngine.UI.Button> parts = NewWorldOptions.PartButtons;
			Check(ref ok, parts.Count == RandomizerSettings.Features.Length && parts.All(b => b != null && b.gameObject.activeInHierarchy && b.interactable), parts.Count + " part buttons, all shown and clickable at Normal");
			if (parts.Count != RandomizerSettings.Features.Length) { WorldSettingsWindow.Close(); box.gameObject.SetActive(false); Fail("randomizer chips"); yield break; }
			Func<UnityEngine.UI.Button, Sprite> look = b => b.targetGraphic is UnityEngine.UI.Image ? ((UnityEngine.UI.Image)b.targetGraphic).sprite : null;
			Sprite lit = look(parts[0]);
			for (int i = 0; i < parts.Count; i++)
			{
				string f = RandomizerSettings.Features[i], label = RandomizerSettings.FeatureLabels[i];
				parts[i].onClick.Invoke();
				yield return null;
				bool offOk = NewWorldOptions.Randomizer.Disabled.Contains(f) && look(parts[i]) != lit && parts.Where((b, k) => k != i).All(b => look(b) == lit);
				parts[i].onClick.Invoke();
				yield return null;
				bool onOk = !NewWorldOptions.Randomizer.Disabled.Contains(f) && look(parts[i]) == lit;
				Check(ref ok, offOk && onOk, label + ": a click leaves it out and greys it (" + offOk + "), a second puts it back (" + onOk + ")");
			}
			NewWorldOptions.Randomizer.Level = RandomizerSettings.Off;
			NewWorldOptions.Refresh();
			yield return null;
			Check(ref ok, parts.All(b => !b.interactable && look(b) != lit), "at Off every part is greyed and can't be clicked");
			NewWorldOptions.Randomizer.Level = RandomizerSettings.Normal;
			NewWorldOptions.Refresh();
			yield return null;
			Check(ref ok, parts.All(b => b.interactable && look(b) == lit), "back at Normal: every part lit and clickable again");
			// (for the next new world: Alphas and Bosses left out)
			parts[Array.IndexOf(RandomizerSettings.Features, RandomizerSettings.Alphas)].onClick.Invoke();
			parts[Array.IndexOf(RandomizerSettings.Features, RandomizerSettings.Bosses)].onClick.Invoke();
			yield return null;
			Screenshot(new[] { "randomizer_chips" });
			yield return new WaitForSeconds(1f);
			Log("The next new world's randomizer: " + NewWorldOptions.Randomizer.Describe());
			WorldSettingsWindow.Close();
			box.gameObject.SetActive(false);
			if (ok) Log("PASS: randomizer chips"); else Fail("randomizer chips");
		}

		[ConsoleCommand(name: "CIRandomizerChipsCheck", docs: "Dev, world: the world created after CIRandomizerChips has exactly the parts left lit - Normal, everything but Alphas and Bosses - and none of their extras; then the New Game box's remembered choice goes back to Off (IR5)")]
		public static void RandomizerChipsCheckCommand()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world"); return; }
			bool ok = true;
			RandomizerSettings s = WorldRandomizer.Current;
			string[] off = RandomizerSettings.Features.Where(f => s.Disabled.Contains(f)).ToArray();
			Check(ref ok, s.Level == RandomizerSettings.Normal && off.Length == 2 && off.Contains(RandomizerSettings.Alphas) && off.Contains(RandomizerSettings.Bosses), "this world's randomizer: " + s.Describe() + " (want Normal without Alphas and Bosses)");
			WorldRandomizer.SaveDefaults(new RandomizerSettings());
			WorldRandomizer.Pending = null;
			if (ok) Log("PASS: randomizer chips check"); else Fail("randomizer chips check");
		}

		[ConsoleCommand(name: "CITime", docs: "Dev, world: sets the time of day (hour 0-24), as Raft's own cheat does: CITime <hour>")]
		public static void TimeCommand(string[] args) { SetHour(args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 12f); Log("PASS: time set"); }

		static void SetHour(float hour)
		{
			UnityEngine.AzureSky.AzureSkyController sky = ComponentManager<UnityEngine.AzureSky.AzureSkyController>.Value;
			if (sky != null && sky.timeOfDay != null) sky.timeOfDay.hour = hour;
		}

		[ConsoleCommand(name: "CIProbeCollider", docs: "Dev, world: why rays do or don't hit Raft's spawned islands: their terrain colliders, scenes and physics scenes")]
		public static void ProbeColliderCommand()
		{
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			Log("time scale " + Time.timeScale + ", auto sync " + Physics.autoSyncTransforms + ", auto simulation " + Physics.autoSimulation + ", raft at " + raft.ToString("F0"));
			foreach (Landmark l in WorldManager.AllLandmarks.Where(x => x != null && x.isSpawned))
			{
				foreach (TerrainCollider tc in l.GetComponentsInChildren<TerrainCollider>(true))
				{
					Vector3 c = tc.bounds.center;
					RaycastHit hit;
					bool own = tc.Raycast(new Ray(new Vector3(c.x, 300f, c.z), Vector3.down), out hit, 600f);
					bool world = Physics.Raycast(new Vector3(c.x, 300f, c.z), Vector3.down, out hit, 600f);
					bool scene = tc.gameObject.scene.GetPhysicsScene() == Physics.defaultPhysicsScene;
					Log(l.name + " at " + (l.transform.position - raft).magnitude.ToString("F0") + " m: terrain '" + tc.name + "' enabled " + tc.enabled + " active " + tc.gameObject.activeInHierarchy + " scene '" + tc.gameObject.scene.name +
						"' default physics " + scene + ", bounds " + tc.bounds.center.ToString("F0") + " size " + tc.bounds.size.ToString("F0") + ", terrain at " + tc.transform.position.ToString("F0") +
						", own ray " + own + ", world ray " + world + (world ? " (" + hit.collider.name + ")" : "") + ", data " + (tc.terrainData != null ? tc.terrainData.size.ToString("F0") : "none"));
				}
			}
			Log("PASS: probed colliders");
		}

		#region Like Raft's islands, large islands, caves

		static readonly System.Text.RegularExpressions.Regex Cuttable = new System.Text.RegularExpressions.Regex(@"^Pickup_Landmark_(Tree_\w+( \d+)?|MangoTree|Palmtree \d+)$");

		/// <summary>Raft's plain tropical islands from raft_land.txt: land (m²), land objects, trees to cut, harvestables on land.</summary>
		static List<float[]> RaftIslandNumbers()
		{
			var land = new Dictionary<string, float>();
			var counts = new Dictionary<string, float[]>();
			var seen = new HashSet<string>();
			byte[] bytes = RaftIslands.ModFile(RaftLand.FileName);
			if (bytes == null) return new List<float[]>();
			foreach (string raw in System.Text.Encoding.UTF8.GetString(bytes).Split('\n'))
			{
				string[] f = raw.TrimEnd('\r').Split('\t');
				if (f.Length < 5 || !System.Text.RegularExpressions.Regex.IsMatch(f[1], @"^(Big|Big island .+|Small island \d+)$")) continue;
				if (f[0] == "land") land[f[1]] = f[4].Split(' ').Where(x => x.Contains(':')).Sum(x => float.Parse(x.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture));
				else if (f[0] == "lobj" && seen.Add(f[1] + "|" + f[2]))
				{
					float[] c;
					if (!counts.TryGetValue(f[1], out c)) counts[f[1]] = c = new float[3];
					float n = float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture);
					c[0] += n;
					if (Cuttable.IsMatch(f[2])) c[1] += n;
					if (f[2].StartsWith("Pickup_Landmark_")) c[2] += n;
				}
			}
			return land.Where(kv => counts.ContainsKey(kv.Key)).Select(kv => new[] { kv.Value, counts[kv.Key][0], counts[kv.Key][1], counts[kv.Key][2] }).ToList();
		}

		/// <summary>A generated island file's land (m²), land objects (nature, not content), trees to cut, harvestables on land, flowers.</summary>
		static float[] IslandNumbers(IslandFile f)
		{
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1), sea = f.WaterLevel;
			int cells = 0;
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) if (f.Heights[z, x] * f.TerrainSize.y > sea + 0.1f) cells++;
			var nature = f.Objects.Where(o => o.Position.y > sea + 0.2f && (o.Props == null || o.Props.Count == 0)).ToList();
			return new float[] { cells * step * step, nature.Count, nature.Count(o => Cuttable.IsMatch(o.Name)), nature.Count(o => o.Name.StartsWith("Pickup_Landmark_")), nature.Count(o => o.Name.Contains("Flower")) };
		}

		[ConsoleCommand(name: "CIIslandsLikeRaft", docs: "Dev, anywhere: the randomizer's islands fit in Raft's world: land objects per 1000 m² and trees to cut compared with Raft's own small and big islands (raft_land.txt); large islands have puffer fish, warthogs, animals to catch, scenes and a cave")]
		public static void IslandsLikeRaftCommand() { StartTest(IslandsLikeRaftRoutine()); }

		static IEnumerator IslandsLikeRaftRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			List<float[]> raft = RaftIslandNumbers();
			List<float[]> small = raft.Where(x => x[0] < 5000f).ToList(), big = raft.Where(x => x[0] >= 5000f).ToList();
			Func<List<float[]>, int, float, float> per = (list, i, scale) => list.Count == 0 ? 0f : list.Average(x => x[i] / x[0] * scale);
			Log("Raft's small islands: " + small.Count + ", " + (small.Count > 0 ? small.Min(x => x[0]).ToString("F0", inv) + "-" + small.Max(x => x[0]).ToString("F0", inv) : "?") + " m² of land, " +
				(small.Count > 0 ? small.Min(x => x[1] / x[0] * 1000f).ToString("F0", inv) + "-" + small.Max(x => x[1] / x[0] * 1000f).ToString("F0", inv) : "?") + " land objects per 1000 m², " +
				(small.Count > 0 ? small.Min(x => x[2]).ToString("F0", inv) + "-" + small.Max(x => x[2]).ToString("F0", inv) : "?") + " trees to cut");
			Log("Raft's big islands: " + big.Count + ", " + (big.Count > 0 ? big.Min(x => x[1] / x[0] * 1000f).ToString("F0", inv) + "-" + big.Max(x => x[1] / x[0] * 1000f).ToString("F0", inv) : "?") + " land objects per 1000 m², " +
				(big.Count > 0 ? big.Min(x => x[2] / x[0] * 1000f).ToString("F2", inv) + "-" + big.Max(x => x[2] / x[0] * 1000f).ToString("F2", inv) : "?") + " trees to cut per 1000 m²");
			if (small.Count == 0 || big.Count == 0) { Fail("raft_land.txt has no plain islands"); yield break; }
			float smallMin = small.Min(x => x[1] / x[0] * 1000f), smallMax = small.Max(x => x[1] / x[0] * 1000f);
			float bigMin = big.Min(x => x[1] / x[0] * 1000f), bigMax = big.Max(x => x[1] / x[0] * 1000f);
			float cutMin = big.Min(x => x[2] / x[0] * 1000f), cutMax = big.Max(x => x[2] / x[0] * 1000f);

			var types = RandomizerContent.Oddities.Select(o => o[0]).Concat(new[] { "lair" }).Concat(Enumerable.Repeat("large", 8)).ToList();
			int flowers = 0, tropical = 0;
			for (int i = 0; i < types.Count; i++)
			{
				MapType type = MapTypes.Get(types[i]);
				float el;
				IslandGenSettings s = MapTypes.Roll(type, new System.Random(4200 + i), out el);
				IslandFile f = null;
				try { f = MapTypes.Create(type, s, el, "likeraft"); }
				catch (Exception e) { Fail(types[i] + ": " + e); ok = false; continue; }
				float[] n = IslandNumbers(f);
				float dens = n[1] / n[0] * 1000f, cut = n[2];
				bool isBig = n[0] >= 5000f;
				// Small islands: objects as dense as on Raft's small islands (loosely), a few trees to cut; big: as Raft's big ones
				// (trees to cut: Raft's snowy islands have none, volcanic is the mod's own style)
				bool noCut = s.Style == TerrainPainter.Snowy || s.Style == TerrainPainter.Volcanic;
				bool good = isBig ? dens >= bigMin * 0.6f && dens <= bigMax * 1.6f && (noCut || cut / n[0] * 1000f >= cutMin * 0.5f && cut / n[0] * 1000f <= cutMax * 2f)
					: dens >= smallMin * 0.4f && dens <= smallMax * 1.5f && (s.Style != TerrainPainter.Tropical || (cut >= 1 && cut <= 14));
				if (s.Style == TerrainPainter.Tropical) { tropical++; if (n[4] > 0) flowers++; }
				string extra = "";
				if (types[i] == "large")
				{
					int puffer = f.Objects.Count(o => o.Name == "Creature_PufferFish"), boars = f.Objects.Count(o => o.Name == "Creature_Boar" || o.Name.StartsWith("Creature_") && o.Position.y > f.WaterLevel && ContentCatalog.CreatureOf(o.Name) != null && ContentCatalog.CreatureOf(o.Name).Category == ContentCatalog.HostileCategory);
					int tame = f.Objects.Count(o => ContentCatalog.CreatureOf(o.Name) != null && ContentCatalog.CreatureOf(o.Name).Category == ContentCatalog.CatchableCategory);
					int scene = f.Objects.Count(o => o.Props != null && o.Props.ContainsKey("set.piece") && !o.Props.ContainsKey("cave"));
					int cave = f.Objects.Count(o => o.Props != null && o.Props.ContainsKey("cave"));
					int loot = f.Objects.Count(o => ContentCatalog.IsLootObject(o.Name));
					extra = "; '" + ObjectProps.Get(f.Props, IslandProps.Title) + "' " + TerrainPainter.StyleName(s.Style) + ", " + puffer + " puffer fish, " + boars + " hostile spots, " + tame + " to catch, " + scene + " scene props, " + cave + " cave pieces, " + loot + " loot";
					// (as much land as Raft's big islands have, roughly: theirs 17 000 - 33 000 m²)
					good &= n[0] >= 14000f && puffer >= 3 && boars >= 2 && tame >= 2 && loot >= 4 && scene >= 3 && (cave >= 1 || !RandomizerIslands.CanBuildCaves);
				}
				Check(ref ok, good, types[i] + ": " + n[0].ToString("F0", inv) + " m² of land, " + n[1].ToString("F0", inv) + " land objects (" + dens.ToString("F0", inv) + " per 1000 m²), " + cut.ToString("F0", inv) + " trees to cut, " +
					n[3].ToString("F0", inv) + " harvestables, " + n[4].ToString("F0", inv) + " flowers" + extra);
				if (!good)
					Log("  its land objects: " + string.Join(", ", f.Objects.Where(o => o.Position.y > f.WaterLevel + 0.2f && (o.Props == null || o.Props.Count == 0)).GroupBy(o => o.Name)
						.OrderByDescending(g => g.Count()).Take(14).Select(g => g.Key + " " + g.Count()).ToArray()) + "; radius " + s.Radius.ToString("F0", inv) + ", shape " + s.Shape);
				yield return null;
			}
			// Every prop the scenes use is measured, stands on the ground (nothing that hangs from its pivot), and each theme has a centrepiece
			var themeProps = RandomizerIslands.Themes.SelectMany(t => t.Anchors.Concat(t.Medium).Concat(t.Small)).Distinct().ToList();
			var unmeasured = themeProps.Where(n => !RaftProps.Has(n)).ToList();
			var hanging = themeProps.Where(n => RaftProps.Has(n) && !RaftProps.Standable(n)).ToList();
			var bare = RandomizerIslands.Themes.Where(t => !t.Anchors.Any(RaftProps.Standable)).Select(t => t.Name).ToList();
			Check(ref ok, unmeasured.Count == 0 && hanging.Count == 0 && bare.Count == 0, themeProps.Count + " scene props in " + RandomizerIslands.Themes.Length + " themes; not measured: " + (unmeasured.Count == 0 ? "none" : string.Join(", ", unmeasured.ToArray())) +
				"; hanging: " + (hanging.Count == 0 ? "none" : string.Join(", ", hanging.ToArray())) + "; themes without a centrepiece: " + (bare.Count == 0 ? "none" : string.Join(", ", bare.ToArray())));
			Check(ref ok, RandomizerIslands.CanBuildCaves, "caves: " + string.Join(", ", RandomizerIslands.Dens.Where(n => RaftProps.Get(n) != null && RaftProps.Get(n).IsCave).ToArray()) + " measured as dens");
			Check(ref ok, flowers > 0 || !PlaceableCatalog.IsLoaded("Pickup_Landmark_Flower_Yellow"), "Raft's flowers grow on " + flowers + " of " + tropical + " tropical islands" + (PlaceableCatalog.IsLoaded("Pickup_Landmark_Flower_Yellow") ? "" : " (this Raft's catalog has none)"));
			if (ok) Log("PASS: islands like Raft's"); else Fail("islands like Raft's");
		}


		[ConsoleCommand(name: "CIIslandsSound", docs: "Dev, anywhere: the randomizer's islands are sound: nothing of the land floats or is buried (also where a den levelled the ground), no scene prop, set piece or den stands over the sea, scene props don't stand inside each other, a den's floor is on its levelled ground (8 oddities, the lair, 6 large islands)")]
		public static void IslandsSoundCommand() { StartTest(IslandsSoundRoutine()); }

		static IEnumerator IslandsSoundRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			// (rocks and flotsam sink as Raft sinks them: up to 60% of their size - IslandGenerator, from raft_land.txt)
			var rock = new System.Text.RegularExpressions.Regex("Boulder|Rock|Stalagmite|Pillar|^FL_|Log|Plank|Driftwood", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			var types = RandomizerContent.Oddities.Select(o => o[0]).Concat(new[] { "lair" }).Concat(Enumerable.Repeat("large", 6)).ToList();
			for (int i = 0; i < types.Count; i++)
			{
				MapType type = MapTypes.Get(types[i]);
				float el;
				IslandGenSettings s = MapTypes.Roll(type, new System.Random(5100 + i), out el);
				IslandFile f = null;
				try { f = MapTypes.Create(type, s, el, "sound"); }
				catch (Exception e) { Fail(types[i] + ": " + e); ok = false; continue; }
				var k = new MapKit(f, 0);
				Func<IslandObject, float> above = o => o.Position.y - k.Ground(new Vector2(o.Position.x, o.Position.z));
				// Nature on the land (objects without settings): on the ground, big rocks sunk up to a third of their size
				var nature = f.Objects.Where(o => (o.Props == null || o.Props.Count == 0) && k.Ground(new Vector2(o.Position.x, o.Position.z)) > k.Sea + 0.2f).ToList();
				// (by the mesh where it's known: its bottom in the air floats - ice at the shore hangs below its pivot - and a plant
				// sunk deeper than its pivot rule is still sound while a quarter of it shows)
				Func<IslandObject, float> bottom = o => { Bounds b; return PlaceableCatalog.LocalBounds(o.Name, out b) ? above(o) + b.min.y * Mathf.Abs(o.Scale.y) : above(o); };
				Func<IslandObject, bool> shows = o => { Bounds b; return PlaceableCatalog.LocalBounds(o.Name, out b) && above(o) + b.max.y * Mathf.Abs(o.Scale.y) > 0.25f * b.size.y * Mathf.Abs(o.Scale.y); };
				var floating = nature.Where(o => bottom(o) > 0.5f && !System.Text.RegularExpressions.Regex.IsMatch(o.Name, "^(Block_(Roof|Wall|Pillar|Stair)|Placeable_)")).ToList(); // (a hut's walls and roof stand on it)
				var buried = nature.Where(o => above(o) < (rock.IsMatch(o.Name) ? -6f : -1.2f) && (rock.IsMatch(o.Name) || !shows(o))).ToList();
				// Set pieces and dens: over land, not the sea
				var pieces = f.Objects.Where(o => o.Props != null && o.Props.ContainsKey("set.piece")).ToList();
				// (a plane or boat sunk on purpose lies 4 m or more under the sea - IslandGenerator.Landmark)
				var overSea = pieces.Where(o => k.Ground(new Vector2(o.Position.x, o.Position.z)) < k.Sea + 0.3f
					&& !(System.Text.RegularExpressions.Regex.IsMatch(o.Name, "^(Airplane|BoatStranded)$") && k.Ground(new Vector2(o.Position.x, o.Position.z)) < k.Sea - 3f)).ToList();
				// Scene props inside each other: the inner part of their footprints (a third of the smaller side) apart
				var props = pieces.Where(o => !o.Props.ContainsKey("cave") && RaftProps.Get(o.Name) != null).ToList();
				var inside = new List<string>();
				for (int a = 0; a < props.Count; a++)
					for (int b = a + 1; b < props.Count; b++)
					{
						PropInfo pa = RaftProps.Get(props[a].Name), pb = RaftProps.Get(props[b].Name);
						float ra = Mathf.Min(pa.Size.x, pa.Size.z) / 3f, rb = Mathf.Min(pb.Size.x, pb.Size.z) / 3f;
						if (new Vector2(props[a].Position.x - props[b].Position.x, props[a].Position.z - props[b].Position.z).magnitude < ra + rb) inside.Add(props[a].Name + "/" + props[b].Name);
						// (a scene's own props by their turned boxes, as the generator spaces them: two metal crates 2.4 m apart stood
						// 0.35 m into each other, gen-large-37435 2026-10-21)
						else if (props[a].Props.ContainsKey(RandomizerIslands.SceneProp) && props[b].Props.ContainsKey(RandomizerIslands.SceneProp))
						{
							float into = RandomizerIslands.Into(RandomizerIslands.FootOf(props[a].Name, new Vector2(props[a].Position.x, props[a].Position.z), props[a].EulerRotation),
								RandomizerIslands.FootOf(props[b].Name, new Vector2(props[b].Position.x, props[b].Position.z), props[b].EulerRotation));
							if (into > 0.06f) inside.Add(props[a].Name + "/" + props[b].Name + " (boxes " + into.ToString("F2", inv) + " m)");
						}
					}
				// A den's floor on the ground levelled for it (a point in its passage)
				string den = "";
				foreach (IslandObject c in pieces.Where(o => o.Props.ContainsKey("cave")))
				{
					PropInfo p = RaftProps.Get(c.Name);
					if (p == null || !p.IsCave) continue;
					Vector3 inner = c.Position + Quaternion.Euler(c.EulerRotation) * Vector3.Scale(p.Inside, Vector3.one);
					float floor = c.Position.y + p.Floor, ground = k.Ground(new Vector2(inner.x, inner.z));
					den = ", den floor " + (ground - floor).ToString("+0.0;-0.0", inv) + " m from the ground in it";
					if (Mathf.Abs(ground - floor) > 0.6f) { ok = false; den += " (TOO FAR)"; }
				}
				bool good = floating.Count == 0 && buried.Count == 0 && overSea.Count == 0 && inside.Count == 0;
				Check(ref ok, good, types[i] + " '" + ObjectProps.Get(f.Props, IslandProps.Title) + "': " + nature.Count + " land objects, floating " + floating.Count + ", buried " + buried.Count + "; " + pieces.Count + " set pieces, over the sea " + overSea.Count + ", inside each other " + inside.Count + den);
				if (!good)
					Log("  " + string.Join("; ", floating.Take(4).Select(o => "floats " + o.Name + " " + bottom(o).ToString("F1", inv)).Concat(buried.Take(4).Select(o => "buried " + o.Name + " " + above(o).ToString("F1", inv)))
						.Concat(overSea.Take(4).Select(o => "over the sea " + o.Name + " (ground " + (k.Ground(new Vector2(o.Position.x, o.Position.z)) - k.Sea).ToString("F1", inv) + " m)")).Concat(inside.Take(6).Select(x => "inside " + x)).ToArray()));
				yield return null;
			}
			if (ok) Log("PASS: islands are sound"); else Fail("islands are sound");
		}
		[ConsoleCommand(name: "CIRandomizerLarge", docs: "Dev, world (host): a large island near the raft - loads, its animals (warthogs, animals to catch, puffer fish), scenes, and its cave: players walk in, the guard wakes inside it; pictures shot_large_*. CIRandomizerLarge [keep]")]
		public static void RandomizerLargeCommand(string[] args) { StartTest(RandomizerLargeRoutine(args != null && args.Any(a => a == "keep"))); }

		static IEnumerator RandomizerLargeRoutine(bool keep)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			SetHour(11f);
			IslandWorldState.Entry e = SpawnTypeNear("large", raft);
			if (e == null) { Fail("no free spot for a large island near the raft (sail on)"); yield break; }
			for (float t = 0; t < 90f && e.Root == null && !e.Failed; t += 1f) yield return new WaitForSeconds(1f);
			if (e.Root == null) { Fail("the large island didn't load"); yield break; }
			GameObject root = e.Root;
			// (since 10-19 only an island with a quest has an IslandInfoTag: loaded = its IslandSettings, the title from those)
			IslandSettings settings = root.GetComponent<IslandSettings>();
			string title = settings != null && settings.Props != null ? ObjectProps.Get(settings.Props, IslandProps.Title) : "?";
			var refs = root.GetComponentsInChildren<IslandObjectRef>(true).ToList();
			int scene = refs.Count(r => r.Props != null && r.Props.ContainsKey("set.piece") && !r.Props.ContainsKey("cave"));
			List<Transform> caves = root.GetComponentsInChildren<Transform>(true).Where(t => RaftProps.Get(t.name) != null && RaftProps.Get(t.name).IsCave).ToList();
			Log("Large island: '" + e.Name + "'");
			Check(ref ok, settings != null && scene >= 3, "'" + title + "' loaded: " + refs.Count + " objects with settings, " + scene + " scene props, " + caves.Count + " cave pieces");
			// Its animals (not the cave's guard: it waits)
			List<CreatureSpawnPoint> spots = root.GetComponentsInChildren<CreatureSpawnPoint>(true).ToList();
			for (float t = 0; t < 120f && spots.Any(p => p.RecordedAlive == CreatureSpawnPoint.Spawning || (p.RecordedAlive == -1 && ObjectProps.Get(p.Props, ObjectProps.CreatureZone).Length == 0)); t += 1f) yield return new WaitForSeconds(1f);
			Check(ref ok, spots.Count(p => p.RecordedAlive > 0) >= 5, spots.Count + " creature spots, alive: " + string.Join(", ", spots.GroupBy(p => p.Kind.Label).Select(g => g.Key + " " + g.Sum(p => Math.Max(0, p.RecordedAlive))).ToArray()));
			Network_Player player = RAPI.GetLocalPlayer();
			// Pictures: from above, of each scene
			Vector3 mid = root.transform.position + LocalLandCentre(root, e.Name);
			yield return CameraShot(mid + new Vector3(0f, 110f, -150f), mid, "large_overview", true);
			foreach (IslandObjectRef r in refs.Where(x => x.Props != null && x.Props.ContainsKey("set.piece") && !x.Props.ContainsKey("cave") && RaftProps.Get(x.ObjectName) != null && RandomizerIslands.Themes.Any(th => th.Anchors.Contains(x.ObjectName))).Take(3))
				yield return ShotOf(player, r.transform, "large_scene_" + r.ObjectName.Replace(" ", "_"), 9f);
			// The cave: the mouth, then inside; the guard wakes and stands in the passage
			if (caves.Count > 0)
			{
				TriggerZone zone = root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "cave");
				Transform first = caves[0];
				// (the hoard: the box nearest the den piece; the way in: from the zone at the mouth towards it)
				LootCrate hoardBox = root.GetComponentsInChildren<LootCrate>(true).OrderBy(c => (c.transform.position - first.position).sqrMagnitude).FirstOrDefault();
				Log("  the den " + first.name + " at " + (first.position - root.transform.position) + ", its zone at " + (zone != null ? (zone.transform.position - root.transform.position).ToString() : "none") +
					", the nearest box at " + (hoardBox != null ? (hoardBox.transform.position - root.transform.position).ToString() : "none") + " (from the island's root)");
				if (zone != null)
				{
					// The mouth from in front of it, then inside looking towards the hoard
					yield return DenShots(player, zone.transform.position, hoardBox != null ? hoardBox.transform.position : first.position, "large_cave");
					string denAt; float denOff = DenOffset(first, zone.transform.position, out denAt);
					Check(ref ok, Mathf.Abs(denOff) < 1.5f, "the den stands on its floor: " + denAt);
					string rimAt; float rimGap = DenRimGap(first, out rimAt);
					Check(ref ok, rimGap < 0.6f, "the den's rim meets the ground all round, nothing to see into under it (AU83): " + rimAt);
					var leftIn = DenLeftovers(first);
					Check(ref ok, leftIn.Count == 0, "nothing of Raft's story or animals inside the den" + (leftIn.Count > 0 ? ": " + string.Join(", ", leftIn.ToArray()) : ""));
					CreatureSpawnPoint guard = spots.FirstOrDefault(p => ObjectProps.Get(p.Props, ObjectProps.CreatureZone) == "cave");
					for (float t = 0; guard != null && t < 60f && guard.RecordedAlive <= 0; t += 1f) yield return new WaitForSeconds(1f);
					AI_NetworkBehaviour ai = guard != null ? guard.Spawned.FirstOrDefault(a => a != null) : null;
					float off = ai != null ? Mathf.Abs(ai.transform.position.y - zone.transform.position.y) : 99f;
					Check(ref ok, guard == null || (ai != null && off < 2.5f), guard == null ? "no guard in this cave" : ai == null ? "the cave's guard didn't come" : "the cave's " + guard.Kind.Label + " woke inside it (" + off.ToString("F1") + " m from the floor)");
					// The guard can't leave through the walls or the back (catalogue IR8): a path from inside the den to the
					// ground behind it goes round through the mouth (much longer than straight), or there is none
					if (ai != null && hoardBox != null)
					{
						Vector3 inward = Vector3.ProjectOnPlane(hoardBox.transform.position - zone.transform.position, Vector3.up).normalized;
						NavMeshHit from, to;
						Vector3 behind = hoardBox.transform.position + inward * 14f;
						if (NavMesh.SamplePosition(ai.transform.position, out from, 3f, NavMesh.AllAreas) && NavMesh.SamplePosition(behind, out to, 8f, NavMesh.AllAreas))
						{
							var path = new NavMeshPath();
							NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
							float len = 0f;
							for (int c = 1; c < path.corners.Length; c++) len += Vector3.Distance(path.corners[c - 1], path.corners[c]);
							float straight = Vector3.Distance(from.position, to.position);
							Check(ref ok, path.status != NavMeshPathStatus.PathComplete || len > straight * 1.4f, "the guard can't walk out through the den's back: " + (path.status == NavMeshPathStatus.PathComplete ? "the way behind it is " + len.ToString("F0") + " m, straight " + straight.ToString("F0") + " m (round through the mouth)" : "no way behind it (" + path.status + ")"));
						}
						else Log("  (no NavMesh behind the den to walk to: the back is in the hill)");
					}
					// (the hoard lies deeper in than the zone, inside the den: its deepest den is 42 m long)
					float toHoard = hoardBox != null ? Vector3.Distance(hoardBox.transform.position, zone.transform.position) : 99f, fromDen = hoardBox != null ? Vector3.Distance(hoardBox.transform.position, first.position) : 99f;
					Check(ref ok, hoardBox != null && toHoard > 3f && toHoard < 36f && fromDen < 24f, "a hoard at the end of the cave (" + toHoard.ToString("F1") + " m in from the zone, " + fromDen.ToString("F1") + " m from the den's pivot)");
				}
				else Check(ref ok, false, "the cave has no zone");
			}
			else Log("  (no cave: " + (RandomizerIslands.CanBuildCaves ? "no spot fit" : "no measured dens") + ")");
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
			if (!keep) IslandWorldState.RemoveIds(new[] { e.Id }, true);
			if (ok) Log("PASS: a large island"); else Fail("a large island");
		}

		/// <summary>Raft's pickups, blueprints and animal models left inside a den copy (none should be).</summary>
		static List<string> DenLeftovers(Transform den)
		{
			return den.GetComponentsInChildren<Transform>(true).Where(t => t != den && (t.name.StartsWith("Pickup_") || t.GetComponent<SkinnedMeshRenderer>() != null || t.GetComponent<PickupItem>() != null || t.GetComponent<LandmarkItem>() != null))
				.Select(t => t.name).Distinct().ToList();
		}

		/// <summary>
		/// AU83: walks in to a den (or any cave piece) over the ground from 24 ways round it (not from its mouths) to where its rock first stands
		/// there: a wall that meets the ground (a ray along the ground hits it: no gap), or rock overhead (the gap up to
		/// it - its rim floating over the ground, where you saw into the shell). The largest gap (m).
		/// </summary>
		static float DenRimGap(Transform den, out string detail)
		{
			PropInfo p = RaftProps.Get(den.name);
			List<Collider> cols = den.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToList();
			if (p == null || !p.IsCave || cols.Count == 0) { detail = "not measured or no colliders"; return 99f; }
			Bounds b = cols[0].bounds;
			foreach (Collider c in cols) b.Encapsulate(c.bounds);
			Vector3 o3 = den.rotation * (p.Axis == 0 ? Vector3.right : Vector3.forward);
			Vector2 outDir = new Vector2(o3.x, o3.z).normalized, mid = new Vector2(b.center.x, b.center.z);
			Func<Vector2, float> ground = q =>
			{
				float best = float.MinValue;
				foreach (RaycastHit h in Physics.RaycastAll(new Vector3(q.x, b.max.y + 5f, q.y), Vector3.down, b.size.y + 80f, ~0, QueryTriggerInteraction.Ignore))
					if (!cols.Contains(h.collider) && h.collider.GetComponentInParent<AI_NetworkBehaviour>() == null && h.collider.GetComponentInParent<Network_Player>() == null) best = Mathf.Max(best, h.point.y);
				return best;
			};
			Func<Vector3, Vector3, float, float> rock = (from, dir, len) =>
			{
				float best = float.MaxValue;
				RaycastHit h;
				foreach (Collider c in cols) if (c.Raycast(new Ray(from, dir), out h, len)) best = Mathf.Min(best, h.distance);
				return best;
			};
			float worst = 0f;
			int sides = 0, buried = 0;
			string at = "";
			bool backs = Physics.queriesHitBackfaces;
			Physics.queriesHitBackfaces = true;
			try
			{
				float start = new Vector2(b.extents.x, b.extents.z).magnitude + 2f;
				for (int i = 0; i < 24; i++)
				{
					float a = i * Mathf.PI / 12f;
					Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					if ((p.OpenPlus && Vector2.Dot(dir, outDir) > 0.6f) || (p.OpenMinus && Vector2.Dot(dir, outDir) < -0.6f)) continue;
					Vector3 prev = Vector3.zero;
					bool hasPrev = false;
					for (float t = start; t > 0f; t -= 0.5f)
					{
						Vector2 q = mid + dir * t;
						float g = ground(q);
						if (g == float.MinValue) { hasPrev = false; continue; }
						Vector3 here = new Vector3(q.x, g, q.y);
						if (hasPrev)
						{
							Vector3 d = here + Vector3.up * 0.3f - prev;
							if (d.sqrMagnitude > 0.0001f && rock(prev, d.normalized, d.magnitude) < float.MaxValue) { sides++; break; }
						}
						float up = rock(here + Vector3.up * 0.05f, Vector3.up, 40f);
						if (up < float.MaxValue)
						{
							sides++;
							if (up > worst) { worst = up; at = " (worst " + up.ToString("F1") + " m, " + (a * Mathf.Rad2Deg).ToString("F0") + "° round, at " + here.ToString("F0") + ")"; }
							break;
						}
						// (the shell under the ground here: the hill covers that side - no rim to see, no gap)
						if (rock(here + Vector3.up * 0.05f, Vector3.down, 40f) < float.MaxValue) { sides++; buried++; break; }
						prev = here + Vector3.up * 0.3f;
						hasPrev = true;
					}
				}
			}
			finally { Physics.queriesHitBackfaces = backs; }
			detail = sides + " sides walked (" + buried + " under the hill), the largest gap under its rim " + worst.ToString("F1") + " m" + at;
			return sides > 0 ? worst : 99f;
		}

		/// <summary>
		/// How far a den sits off where it should (m, + = too high): the lowest point of its rock against its floor (the
		/// zone at its mouth stands on the floor), compared with raft_props.txt's bottom and floor.
		/// </summary>
		static float DenOffset(Transform den, Vector3 zoneAt, out string detail)
		{
			PropInfo p = RaftProps.Get(den.name);
			Renderer[] rs = den.GetComponentsInChildren<Renderer>(true);
			if (p == null || rs.Length == 0) { detail = "not measured"; return 0f; }
			Bounds b = rs[0].bounds;
			foreach (Renderer rr in rs) b.Encapsulate(rr.bounds);
			float want = zoneAt.y + (p.Bottom - p.Floor);
			detail = "its rock's lowest point " + (b.min.y - zoneAt.y).ToString("F1") + " m from its floor (" + (p.Bottom - p.Floor).ToString("F1") + " m wanted), pivot " + (den.position.y - zoneAt.y).ToString("F1") + " m above the floor (" + (-p.Floor).ToString("F1") + " wanted)";
			return b.min.y - want;
		}

		/// <summary>Pictures of a den: from in front of its mouth looking in, and from inside (by the zone) looking towards the hoard.</summary>
		static IEnumerator DenShots(Network_Player player, Vector3 zoneAt, Vector3 hoardAt, string prefix)
		{
			Vector3 inward = Vector3.ProjectOnPlane(hoardAt - zoneAt, Vector3.up).normalized;
			if (inward == Vector3.zero) inward = Vector3.forward;
			float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
			Vector3 front = zoneAt - inward * 12f;
			RaycastHit hit;
			if (Physics.Raycast(front + Vector3.up * 40f, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) front.y = Mathf.Max(hit.point.y, zoneAt.y - 1f);
			yield return PutPlayer(player, front + Vector3.up * 1.8f, false);
			Look(player, yaw, 5f);
			yield return new WaitForSeconds(2f);
			Shot(prefix + "_mouth");
			yield return new WaitForSeconds(0.6f);
			yield return PutPlayer(player, zoneAt + Vector3.up * 1.0f, false);
			Look(player, yaw, 5f);
			yield return new WaitForSeconds(4f);
			Shot(prefix + "_inside");
			yield return new WaitForSeconds(0.6f);
		}

		static void Shot(string name)
		{
			ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(System.IO.Path.Combine(DynamicIslands.assetpath, "shot_" + name + ".png")));
			Log("Screenshot shot_" + name + ".png");
		}

		/// <summary>A picture of t from about distance metres away on the ground, looking at it.</summary>
		static IEnumerator ShotOf(Network_Player player, Transform t, string name, float distance)
		{
			// (a free camera, not the player: it stays where it is put, and it looks from a side it can see the thing from)
			Bounds b = new Bounds(t.position, Vector3.one);
			foreach (Renderer rr in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds);
			Vector3 target = b.center;
			float d = Mathf.Max(distance, b.extents.magnitude * 1.6f);
			Vector3 from = target + new Vector3(0.6f, 0.45f, 0.6f).normalized * d;
			for (int i = 0; i < 16; i++)
			{
				Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * new Vector3(0f, 0.4f, 1f).normalized;
				Vector3 c = target + dir * d;
				RaycastHit hit;
				// (clear line of sight to the thing, and not under the ground)
				if (Physics.Linecast(c, target, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(t) && hit.distance < d - b.extents.magnitude * 0.5f) continue;
				if (Physics.Raycast(c + Vector3.up * 60f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > c.y - 0.5f) continue;
				from = c; break;
			}
			yield return CameraShot(from, target, name);
		}

		/// <summary>A picture through Raft's own camera (its sea and sky), moved for one frame from one point at another: no HUD, no falling.</summary>
		static bool effectsLogged;

		static IEnumerator CameraShot(Vector3 from, Vector3 at, string name, bool ownCamera = false)
		{
			yield return new WaitForEndOfFrame();
			// (ownCamera: a camera of its own, without Raft's fog - far views; it draws no sea)
			GameObject own = ownCamera ? new GameObject("CITEST_ShotCamera") : null;
			Camera cam = own != null ? own.AddComponent<Camera>() : Camera.main;
			if (own != null && Camera.main != null) { cam.CopyFrom(Camera.main); own.transform.position = Camera.main.transform.position; }
			if (cam == null) { Log("  (no picture " + name + ": no camera)"); yield break; }
			Transform ct = cam.transform;
			Vector3 pos = ct.position; Quaternion rot = ct.rotation;
			float fov = cam.fieldOfView, far = cam.farClipPlane;
			RenderTexture before = cam.targetTexture;
			RenderTexture rt = new RenderTexture(1920, 1080, 24);
			Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
			try
			{
				ct.position = from;
				ct.rotation = Quaternion.LookRotation(at - from);
				cam.fieldOfView = 60f;
				cam.farClipPlane = Mathf.Max(far, 2500f);
				cam.targetTexture = rt;
				// (the player's underwater and wobble effects on Raft's camera would bend the picture: off for this one frame)
				var effects = cam.GetComponents<Behaviour>().Where(b => b != null && b.enabled && !(b is Camera) && System.Text.RegularExpressions.Regex.IsMatch(b.GetType().Name, "(?i)underwater|wobble|distort|refract|drunk|blur|wave")).ToList();
				if (!effectsLogged) { effectsLogged = true; Log("  (the camera's effects: " + string.Join(", ", cam.GetComponents<Behaviour>().Where(b => b != null && !(b is Camera)).Select(b => b.GetType().Name + (b.enabled ? "" : " off")).ToArray()) + "; off for pictures: " + effects.Count + ")"); }
				foreach (Behaviour b in effects) b.enabled = false;
				bool gfx = GfxShotBegin(); // (CIGfx low: the user's quality)
				try { cam.Render(); } finally { foreach (Behaviour b in effects) b.enabled = true; GfxShotEnd(gfx); }
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
				tex.Apply();
				RenderTexture.active = null;
				System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(DynamicIslands.assetpath, "shot_" + name + ".png")), tex.EncodeToPNG());
				Log("Screenshot shot_" + name + ".png");
			}
			catch (Exception e) { Log("  (no picture " + name + ": " + e.Message + ")"); }
			finally
			{
				cam.targetTexture = before;
				ct.position = pos; ct.rotation = rot;
				cam.fieldOfView = fov; cam.farClipPlane = far;
				if (own != null) UnityEngine.Object.Destroy(own);
				rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
			}
		}

		[ConsoleCommand(name: "CIGrotto", docs: "Dev, world (host): the nearest of Raft's big islands gets a den (one of Balboa's cave outcrops) and an outpost (the randomizer's extras, forced): they load, the guard wakes inside the cave; pictures shot_grotto_*")]
		public static void GrottoCommand(string[] args) { StartTest(GrottoRoutine(args != null && args.Any(a => a == "keep"))); }

		/// <summary>Raft adds one of its big islands ahead (as its timer does) and the raft sails up to it until it spawns.</summary>
		static IEnumerator BringBigIsland(Action<Landmark> found)
		{
			ChunkManager chunks = ComponentManager<ChunkManager>.Value ?? UnityEngine.Object.FindObjectOfType<ChunkManager>();
			ChunkPoint point = chunks != null ? chunks.AddChunkPointForcibly(ChunkPointType.Landmark_Big) : null;
			if (point == null) { Log("  (Raft brought no big island)"); yield break; }
			Log("  (Raft brings a big island " + Flat(point.worldPosition - CustomIslandSpawner.RaftPosition.Value).magnitude.ToString("F0") + " m away)");
			// (Raft shifts the world back to its middle as the raft moves: the point follows those shifts, as in CIBlueprintsWorld)
			Vector3 p0 = point.worldPosition, shifted = Vector3.zero;
			Action<Vector3> onShift = d => shifted += d;
			WorldShiftManager.OnWorldShift += onShift;
			Func<Vector3> target = () => point.worldPosition != p0 ? point.worldPosition : p0 + shifted;
			// (by its place, not by the object: Raft reuses its island objects - the one tried before may come back as this one)
			Func<Landmark> at = () => WorldManager.AllLandmarks.FirstOrDefault(l => l != null && l.isSpawned && WorldRandomizer.KindOf(l) == "Landmark_Big" && Flat(l.transform.position - target()).magnitude < 300f);
			Func<bool> spawned = () => at() != null;
			try
			{
				for (int leg = 0; leg < 6 && !spawned(); leg++)
				{
					yield return SailTo(() => target() + (CustomIslandSpawner.RaftPosition.Value - target()).normalized * 200f, 60f);
					yield return WaitFor(spawned, 20f);
				}
			}
			finally { WorldShiftManager.OnWorldShift -= onShift; }
			found(at());
		}

		static IEnumerator GrottoRoutine(bool keep)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			Func<Landmark> nearestBig = () => WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned && WorldRandomizer.KindOf(l) == "Landmark_Big").OrderBy(l => (l.transform.position - (CustomIslandSpawner.RaftPosition ?? raft)).sqrMagnitude).FirstOrDefault();
			Landmark big = nearestBig();
			// (custom islands off meanwhile: placed ahead of the raft, a dense world's islands could leave Raft's big one no
			// room to fit)
			bool autoBefore = CustomIslandSpawner.Enabled;
			CustomIslandSpawner.Enabled = false;
			// (none near: Raft brings one, as its own timer does - it adds big islands by time, not by distance sailed: in
			// three worlds none came within 25 km of fast sailing)
			if (big == null) { yield return BringBigIsland(l => big = l); if (big == null) big = nearestBig(); }
			if (big == null) { CustomIslandSpawner.Enabled = autoBefore; Fail("a cave on Raft's island: Raft brought none of its big islands"); yield break; }
			RandomizerSettings before = WorldRandomizer.Current.Copy();
			if (!WorldRandomizer.Current.On) WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Normal, Seed = 77 });
			int n = IslandWorldState.Islands.Count;
			IslandWorldState.Entry e = null;
			List<Transform> caves = new List<Transform>();
			// (a bumpy or crowded island - Twin peak - may have no spot a den fits on at all: then the next of Raft's big islands)
			var tried = new List<Landmark>();
			for (int isle = 0; isle < 3 && big != null; isle++)
			{
				tried.Add(big);
				// Go there first: Raft switches the ground of far islands off
				yield return PutPlayer(player, big.transform.position + Vector3.up * 80f, false);
				for (float t = 0; t < 30f && !WorldRandomizer.GroundOn(big); t += 1f) yield return new WaitForSeconds(1f);
				SetHour(11f);
				RandomizerContent.ForceBigFinds = true;
				// (the den's spot is a roll: on a crowded or bumpy island of Raft's one roll may find none - a few rolls, as a
				// world's islands would each have)
				foreach (int salt in new[] { 1234, 2345, 3456, 4567 })
				{
					RandomizerContent.ForceBigFinds = true;
					try { StartTest(WorldRandomizer.ForceExtras(big, salt)); }
					finally { }
					yield return new WaitForSeconds(3f);
					RandomizerContent.ForceBigFinds = false;
					e = IslandWorldState.Islands.LastOrDefault(x => WorldRandomizer.IsExtras(x));
					for (float t = 0; e != null && t < 60f && e.Root == null && !e.Failed; t += 1f) yield return new WaitForSeconds(1f);
					if (e == null || e.Root == null) continue;
					caves = e.Root.GetComponentsInChildren<Transform>(true).Where(t => RaftProps.Get(t.name) != null && RaftProps.Get(t.name).IsCave).ToList();
					if (caves.Count > 0 || !RandomizerIslands.CanBuildCaves) break;
					Log("  (no den with roll " + salt + ": another)");
				}
				if (caves.Count > 0 || !RandomizerIslands.CanBuildCaves) break;
				Landmark last = big;
				big = WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned && WorldRandomizer.KindOf(l) == "Landmark_Big" && !tried.Contains(l)).OrderBy(l => (l.transform.position - last.transform.position).sqrMagnitude).FirstOrDefault();
				// (none other loaded: back on the raft, and Raft brings another, as at the start)
				if (big == null)
				{
					Raft onRaft = UnityEngine.Object.FindObjectOfType<Raft>();
					if (onRaft != null) yield return PutPlayer(player, onRaft.transform.position + Vector3.up * 2f, false);
					yield return BringBigIsland(l => big = l);
				}
				if (big == null) { big = last; break; }
				Log("  (no den on '" + last.name + "': the next of Raft's big islands, '" + big.name + "')");
			}
			CustomIslandSpawner.Enabled = autoBefore;
			if (e == null || e.Root == null) { Fail(caves.Count == 0 && tried.Count > 1 ? "no den fitted on " + tried.Count + " of Raft's big islands (4 rolls each)" : "the extras didn't load"); yield break; }
			Log("Den extras: '" + e.Name + "'");
			// (everything of the extras over Raft's island and its reef: nothing far out at sea)
			Collider farthest = e.Root.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).OrderByDescending(c => new Vector2(c.bounds.center.x - e.Position.x, c.bounds.center.z - e.Position.z).sqrMagnitude).FirstOrDefault();
			float farOut = farthest != null ? new Vector2(farthest.bounds.center.x - e.Position.x, farthest.bounds.center.z - e.Position.z).magnitude : 0f;
			Check(ref ok, farOut < 250f, "everything of the extras within 250 m of Raft's island's middle (the farthest: " + (farthest != null ? farthest.transform.parent != null ? farthest.transform.parent.name : farthest.name : "none") + ", " + farOut.ToString("F0") + " m)");
			int outpost = e.Root.GetComponentsInChildren<IslandObjectRef>(true).Count(r => r.Props != null && r.Props.ContainsKey("set.piece") && !r.Props.ContainsKey("cave") && RaftProps.Get(r.ObjectName) != null && !RaftProps.Get(r.ObjectName).Name.Contains("Boulder"));
			Log("  the extras hold: " + string.Join(", ", e.Root.GetComponentsInChildren<IslandObjectRef>(true).GroupBy(r => r.ObjectName + (r.Props != null && r.Props.ContainsKey("set.piece") ? " (set piece)" : "")).Select(g => g.Key + " x" + g.Count()).ToArray()));
			Check(ref ok, caves.Count >= 1 || !RandomizerIslands.CanBuildCaves, "'" + big.name + "': " + (caves.Count > 0 ? "a den (" + caves[0].name + ")" : "no den") + (RandomizerIslands.CanBuildCaves ? "" : " (no measured cave pieces)") + ", " + outpost + " outpost props");
			if (caves.Count > 0)
			{
				yield return ShotOf(player, caves[0], "grotto_mound", 12f);
				TriggerZone zone = e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "cave");
				LootCrate hoardBox = e.Root.GetComponentsInChildren<LootCrate>(true).OrderBy(c => (c.transform.position - caves[0].position).sqrMagnitude).FirstOrDefault();
				if (zone != null)
				{
					yield return DenShots(player, zone.transform.position, hoardBox != null ? hoardBox.transform.position : caves[0].position, "grotto");
					string denAt; float denOff = DenOffset(caves[0], zone.transform.position, out denAt);
					Check(ref ok, Mathf.Abs(denOff) < 1.5f, "the den stands on its floor: " + denAt);
					string rimAt; float rimGap = DenRimGap(caves[0], out rimAt);
					Check(ref ok, rimGap < 0.6f, "the den's rim meets the ground all round, nothing to see into under it (AU83): " + rimAt);
					var leftIn = DenLeftovers(caves[0]);
					Check(ref ok, leftIn.Count == 0, "nothing of Raft's story or animals inside the den" + (leftIn.Count > 0 ? ": " + string.Join(", ", leftIn.ToArray()) : ""));
					if (Mathf.Abs(denOff) >= 1.5f)
					{
						Log("  the den " + caves[0].name + ": scale " + caves[0].lossyScale + ", rotation " + caves[0].rotation.eulerAngles + ", parent " + (caves[0].parent != null ? caves[0].parent.name : "none"));
						foreach (Renderer rr in caves[0].GetComponentsInChildren<Renderer>(true).OrderByDescending(x => x.bounds.size.sqrMagnitude).Take(8))
							Log("    " + rr.name + " (" + rr.GetType().Name + (rr.enabled && rr.gameObject.activeInHierarchy ? "" : ", off") + "): " + (rr.bounds.min.y - zone.transform.position.y).ToString("F1") + " to " + (rr.bounds.max.y - zone.transform.position.y).ToString("F1") + " m from the floor, size " + rr.bounds.size);
					}
					// (what Raft's island has under the zone: every collider a ray down meets, below the den's own)
					foreach (RaycastHit h in Physics.RaycastAll(zone.transform.position + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
						if (!h.collider.transform.IsChildOf(e.Root.transform))
							Log("  under the zone: " + h.collider.name + " (" + h.collider.GetType().Name + ", layer " + LayerMask.LayerToName(h.collider.gameObject.layer) + ") at " + (h.point.y - zone.transform.position.y).ToString("F1") + " m from the floor" +
								(h.collider.GetComponentInParent<LandmarkItem>() != null ? ", a LandmarkItem" : "") + (h.collider.transform.IsChildOf(big.transform) ? ", Raft's island" : ", not Raft's island"));
					float toHoard = hoardBox != null ? Vector3.Distance(hoardBox.transform.position, zone.transform.position) : 99f, fromDen = hoardBox != null ? Vector3.Distance(hoardBox.transform.position, caves[0].position) : 99f;
					Check(ref ok, hoardBox != null && toHoard > 3f && toHoard < 36f && fromDen < 24f, "a hoard at the end of the den (" + toHoard.ToString("F1") + " m in from the zone, " + fromDen.ToString("F1") + " m from the den's pivot)");
					CreatureSpawnPoint guard = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).FirstOrDefault(p => ObjectProps.Get(p.Props, ObjectProps.CreatureZone) == "cave");
					for (float t = 0; guard != null && t < 60f && guard.RecordedAlive <= 0; t += 1f) yield return new WaitForSeconds(1f);
					AI_NetworkBehaviour ai = guard != null ? guard.Spawned.FirstOrDefault(a => a != null) : null;
					float off = ai != null ? Mathf.Abs(ai.transform.position.y - zone.transform.position.y) : 99f;
					Check(ref ok, ai != null && off < 2.5f, guard == null ? "no guard" : ai == null ? "the guard didn't come" : "the " + guard.Kind.Label + " woke inside the cave (" + off.ToString("F1") + " m from the floor)");
				}
			}
			Transform scene = e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(r => r.Props != null && r.Props.ContainsKey("set.piece") && RandomizerIslands.Themes.Any(th => th.Anchors.Contains(r.ObjectName))).Select(r => r.transform).FirstOrDefault();
			if (scene != null) yield return ShotOf(player, scene, "grotto_outpost", 9f);
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null) yield return PutPlayer(player, raftObj.transform.position + Vector3.up * 2f, false);
			if (!keep)
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(n).Select(x => x.Id).ToList(), true);
				IslandWorldState.RemoveIds(new[] { e.Id }, true);
				WorldRandomizer.Set(before);
			}
			if (ok) Log("PASS: a cave on Raft's island"); else Fail("a cave on Raft's island");
		}

		[ConsoleCommand(name: "CIModeParity", docs: "Dev, anywhere: the mod spawns creatures in each of Raft's game modes exactly as Raft's own islands do (LandmarkEntitySpawner.ShouldEntityBeSpawnedInGamemode, every mode x every animal kind)")]
		public static void ModeParityCommand()
		{
			bool ok = true;
			var modes = new List<SO_GameModeValue>();
			try
			{
				var arr = typeof(GameModeValueManager).GetField("gameModeValues", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null) as SO_GameModeValue[];
				if (arr != null) modes.AddRange(arr.Where(m => m != null));
				foreach (string f in new[] { "creative", "peaceful", "normal", "hard" })
				{
					var m = typeof(GameModeValueManager).GetField(f, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null) as SO_GameModeValue;
					if (m != null && !modes.Contains(m)) modes.Add(m);
				}
			}
			catch (Exception e) { Fail("Raft's game modes: " + e.Message); return; }
			if (modes.Count == 0) { Fail("Raft's game modes aren't set up yet (main menu once?)"); return; }
			var go = new GameObject("CI_ModeParity");
			go.SetActive(false);
			var spawner = go.AddComponent<LandmarkEntitySpawner>();
			var raft = typeof(LandmarkEntitySpawner).GetMethod("ShouldEntityBeSpawnedInGamemode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			int compared = 0, differ = 0;
			try
			{
				foreach (SO_GameModeValue m in modes)
				{
					var off = new List<string>();
					foreach (AI_NetworkBehaviourType t in Enum.GetValues(typeof(AI_NetworkBehaviourType)))
					{
						bool theirs = (bool)raft.Invoke(spawner, new object[] { m, t }), ours = CreatureSpawner.SpawnsInMode(t, m);
						compared++;
						if (theirs != ours) { differ++; Log("  " + m.gameMode + " " + t + ": Raft " + theirs + ", the mod " + ours); }
						if (!theirs) off.Add(t.ToString());
					}
					Log("  " + m.gameMode + ": Raft leaves out " + (off.Count == 0 ? "nothing" : string.Join(", ", off.ToArray())));
				}
			}
			finally { UnityEngine.Object.Destroy(go); }
			Check(ref ok, differ == 0 && compared > 0, compared + " mode x kind pairs compared with Raft's own rule, " + differ + " differ");
			if (ok) Log("PASS: creatures per game mode as Raft"); else Fail("creatures per game mode as Raft");
		}

		[ConsoleCommand(name: "CIStorySafe", docs: "Dev, main menu or editor: opens each of Raft's island scenes and checks the randomizer changes only its plain islands (big and small), never a story island, the stranded boat, the pilot's island or a floating raft")]
		public static void StorySafeCommand() { StartTest(StorySafeRoutine()); }

		static IEnumerator StorySafeRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			int plain = 0, story = 0;
			var wrong = new List<string>();
			foreach (string scene in PlaceableCatalog.LandmarkSceneNames())
			{
				string sceneName = scene;
				yield return PlaceableCatalog.VisitScene(sceneName, s => StorySafeScene(s, sceneName, wrong, n => { if (n) plain++; else story++; }));
				yield return null;
			}
			Check(ref ok, plain > 0 && story > 0 && wrong.Count == 0, plain + " plain island(s) randomized, " + story + " left alone; wrong: " + (wrong.Count == 0 ? "none" : string.Join("; ", wrong.ToArray())));
			if (ok) Log("PASS: Raft's story islands left alone"); else Fail("Raft's story islands left alone");
		}

		static IEnumerator StorySafeScene(UnityEngine.SceneManagement.Scene s, string sceneName, List<string> wrong, Action<bool> count)
		{
			foreach (GameObject root in s.GetRootGameObjects())
				foreach (Landmark l in root.GetComponentsInChildren<Landmark>(true))
				{
					// (a scene's island is named like the scene: "32#Landmark_Big#..." - that is what the randomizer reads)
					string saved = l.name;
					l.name = sceneName;
					bool natural = WorldRandomizer.IsNatural(l);
					l.name = saved;
					bool shouldBe = System.Text.RegularExpressions.Regex.IsMatch(sceneName, "#Landmark_(Big|Small)#") ||
						(System.Text.RegularExpressions.Regex.IsMatch(sceneName, "Small") && !System.Text.RegularExpressions.Regex.IsMatch(sceneName, "Radar|Vasagatan|Tangaroa|Varuna|Utopia|Pilot|Boat|#Landmark_Raft#"));
					Log("  " + sceneName + ": " + (natural ? "randomized" : "left alone"));
					count(natural);
					if (natural && !shouldBe) wrong.Add(sceneName + " would be randomized");
					if (!natural && System.Text.RegularExpressions.Regex.IsMatch(sceneName, "#Landmark_(Big|Small)#")) wrong.Add(sceneName + " (a plain island) would be left alone");
				}
			yield break;
		}


		[ConsoleCommand(name: "CIVisitIsland", docs: "Dev, in game (either player): goes to an island of the list, loaded or not (far away: it loads when the player comes), and stands on it: CIVisitIsland <island>")]
		public static void GoToIslandCommand(string[] args)
		{
			string name = args != null ? string.Join(" ", args) : "";
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => string.Equals(i.HostName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
			if (e == null) { Fail("no island called '" + name + "' in the list"); return; }
			StartTest(GoToIslandRoutine(e));
		}

		static IEnumerator GoToIslandRoutine(IslandWorldState.Entry e)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("no player"); yield break; }
			if (e.Root == null) yield return PutPlayer(player, e.Position + Vector3.up * 3f, true);
			for (float t = 0; e.Root == null && t < 60f; t += 1f) yield return new WaitForSeconds(1f);
			if (e.Root == null) { Fail("'" + e.HostName + "' didn't load"); yield break; }
			if (WorldRandomizer.IsExtras(e))
			{
				// (extras have no land of their own: stand on Raft's island under their middle)
				Collider far = e.Root.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).OrderByDescending(c => new Vector2(c.bounds.center.x - e.Position.x, c.bounds.center.z - e.Position.z).sqrMagnitude).FirstOrDefault();
				if (far != null) Log("  (its farthest collider: " + far.name + " " + new Vector2(far.bounds.center.x - e.Position.x, far.bounds.center.z - e.Position.z).magnitude.ToString("F0") + " m from its middle, size " + far.bounds.size + ")");
				RaycastHit hit;
				Vector3 at = e.Position + Vector3.up * 200f;
				if (Physics.Raycast(at, Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore)) yield return PutPlayer(player, hit.point + Vector3.up * 1.5f, false);
			}
			else yield return StandRoutine(e.Root);
			Log("Went to island '" + e.HostName + "' (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}
		[ConsoleCommand(name: "CICaveState", docs: "Dev, world (either player): a cave of an island as this machine has it: its guard (awake? animals of its kind in the den) and its hoard (opened?): CICaveState <island>")]
		public static void CaveStateCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			Transform den = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => RaftProps.Get(t.name) != null && RaftProps.Get(t.name).IsCave);
			if (den == null) { Fail("no cave on '" + e.HostName + "'"); return; }
			PropInfo p = RaftProps.Get(den.name);
			float reach = Mathf.Max(p.Size.x, p.Size.z) * 0.5f;
			Vector3 middle = den.position + den.rotation * p.Centre;
			CreatureSpawnPoint guard = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).FirstOrDefault(s => ObjectProps.Get(s.Props, ObjectProps.CreatureZone) == "cave");
			int inside = guard == null ? 0 : UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Count(a => a.behaviourType == guard.Kind.Type && a.networkEntity != null && !a.networkEntity.IsDead && new Vector2(a.transform.position.x - middle.x, a.transform.position.z - middle.z).magnitude < reach);
			TriggerZone zone = e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == "cave");
			LootCrate hoard = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle).EndsWith("hoard"); });
			Log("CAVE '" + e.HostName + "' " + den.name + ": zone " + (zone == null ? "none" : ContentState.IsUsed(e, zone.StateKey) ? "fired" : "waiting") + ", guard " + (guard == null ? "none" : guard.Kind.Label + " " + inside + " in the den") +
				(Raft_Network.IsHost && guard != null ? " (recorded alive " + guard.RecordedAlive + ")" : "") + ", hoard " + (hoard == null ? "none" : hoard.Looted ? "opened" : "closed"));
			Log("PASS: cave state");
		}

		#endregion

		/// <summary>Raft's objects the randomizer builds with: set pieces and props of the quest islands, and cave pieces.</summary>
		internal static readonly string[] MeasuredProps =
		{
			// Set pieces (oddity islands)
			"Van_1", "Van_2", "Van3", "Van_4", "Van_5", "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Green_02", "Caravan_Yellow_01", "CaravanBarebones1", "Airplane", "BoatStranded",
			"Balboa_Shack", "Balboa_DecorationPrefabBase_SimpleTent", "CaravanRocket", "CaravanRocketDebris_Body1", "CaravanRocketDebris_Body2", "CaravanRocketDebris_Top1", "CaravanRocketDebris_Exhaust",
			"CaravanRocketDebris_Leg1", "CaravanRocketDebris_Door", "CaravanRocketDebris_Canister", "TangaroaFounderStatue", "RaftMonument", "Well", "Tire_02", "Tire_03", "Pallet", "RT_PlasticBoat",
			"WingBroken", "BackWingBroken", "PropellerBlade", "BoatLandmark_Flag",
			// Scrapyard (Varuna Point)
			"VP_Excavator", "VP_Forklift", "VP_Dumpster01", "VP_Dumpster02", "VP_ConcretePipe01", "VP_ConcretePipe02", "VP_BrickStack01", "VP_BrickStack02", "VP_BrickStack03",
			"VP_MetalCrate01", "VP_MetalCrate02", "VP_MetalCrate03", "VP_ExplosiveBarrel_Pile", "VP_FoldingLadder_Tall", "VP_Cable_Roll01",
			// Market (Utopia)
			"UT_MarketBasket01", "UT_MarketBasket02", "UT_MarketBasket03", "UT_MarketCrateSmall01", "UT_MarketCrateSmall02", "UT_MarketStackBox01", "UT_ClothOverhangBig04", "UT_ClothOverhangBig05",
			"UT_DreamCatcher01", "UT_DreamCatcher02", "UT_DanglyDecoration01", "UT_LightBottle01", "UT_CoveredCrate01", "UT_CoveredCrate02", "UT_DIY_Decoration01", "UT_Generator01",
			"Tangaroa_OutdoorFurnitureSilver_Bench", "Tangaroa_OutdoorFurnitureSilver_Table", "Tangaroa_OutdoorFurnitureSilver_Chair", "Tire_01",
			// Caravan outpost (Caravan Town)
			"Banner_01", "BenchTable_01", "PlasticChair_01", "Cableroll_01", "Cableroll_02", "Crate_Big_01", "Crate_Big_02", "Crate_Small_02", "Scaffolding_2x2m", "Scaffolding_6x4m",
			"LandmarkLadder_6m", "FishingNets_02", "MetalSheet_2", "MetalSheet_3", "PulauKafilahFlag", "RopeFence_Short", "Sign_01", "Sign_02", "Bench_01", "AcaciaTree_Big1", "DeadTree1", "DeadTree2",
			"CaravanIsland_BigRoundRock_1", "Candle_01", "MetalTable_01",
			// Bear country (Balboa)
			"Balboa_DecorationPrefabBase_Fence_Mid", "Balboa_DecorationPrefabBase_Fence_EndL", "Balboa_DecorationPrefabBase_Fence_EndR", "Balboa_DecorationPrefabBase_Fence_EndBroken",
			"Balboa_DecorationPrefabBase_Fence_Sign", "Balboa_DecorationPrefabBase_Lantern", "Balboa_DecorationPrefabBase_OldCouch", "Balboa_DecorationPrefabBase_Old Stove",
			"Balboa_DecorationPrefabBase_Wooden Spikes", "Balboa_DecorationPrefabBase_ToxicBarrel", "Balboa_DecorationPrefabBase_Generator", "Balboa_DecorationPrefabBase_Trashcan",
			"Balboa_DecorationPrefabBase_Table", "Balboa_DecorationPrefabBase_Chair", "BearSign1", "BearSign2 Variant", "Firewood_1", "Antenna_dish", "Balboa_DirectionSign",
			// Frozen camp (Temperance)
			"TP_Igloo_Small", "TP_Igloo_Small_1", "TP_Igloo_Medium_1", "TP_Igloo_Large_1", "TP_IcePillar01", "TP_IcePillar02", "TP_FoldingTable_Large", "TP_FoldingStool", "TP_Moontown_Barrel02",
			"TP_Moontown_SealedCrate02_Clean", "TP_Moontown_TarpCrate02_Clean", "SnowmobileShedMesh",
			// Hotel garden (Tangaroa)
			"LargePlant1", "LargePlant2", "MediumPlant1", "MediumPlant2", "SmallPlant1", "Flowerpot_01", "Flowerpot_02", "Flowerpot_03", "RestaSunshadeGround", "RestaFence", "Table_Common_01", "Dinnerchair_01",
			// Radio outpost (the radio tower)
			"RT_WindMill", "RT_SatteliteDisc", "RT_Floodlight_WithoutLightSource", "RT_Fence", "RT_SharkCage", "RT_PowerBox", "RT_Signs", "RT_Spotlight",
			// Floating rafts
			"campfire_1", "Scarecrow", "Bird_Nest", "Bed_Simple", "buoy",
			// Caves
			"TP_UnderwaterCaveTunnel_Straight", "TP_UnderwaterCaveTunnel_StraightLong", "TP_UnderwaterCaveTunnel_Corner", "TP_UnderwaterCaveTunnel_CornerLong", "TP_UnderwaterCaveTunnel_CrossT",
			"BalboaCave_Entrance", "BalboaCave_DeadEnd", "BalboaCave_Bear", "BalboaCave_Vines", "TP_CaveTunnel_SurfaceStaircase", "Ravine_Cave", "CaveVine1", "CaveVine2", "CaveVine3", "UndergroundToSurface_Tunnel",
			// Rocks for mounds over free-standing caves, and lights
			"BigBoulder1_Low", "BigBoulder2_Low", "BigBoulder3_Low", "BigBoulder4_Low", "SmallBoulder1", "SmallBoulder2", "SmallBoulder3", "SmallBoulder4", "BigRock_1", "BigRock_2", "BigRock_Low1_Sand",
			"TP_BigRock02", "TP_BigRock03", "TP_BigRock04", "BigSharpRock_1", "BigSharpRock_2", "CaravanIsland_BigRoundRock_1",
			"Placeable_Lantern_FireBasket", "Placeable_Lantern_Basic", "Placeable_Lantern_Metal", "Placeable_Lantern_Fireplace", "Placeable_StringLight_Horizontal", "Placeable_Utopia_LightBottleYellow",
		};

		[ConsoleCommand(name: "CIPropColliders", docs: "Dev, any time: a copy of each named object (comma separated) as it spawns - its scale, its meshes' box and each solid collider's box, from the pivot (min..max, m): what a scene's spacing must keep apart. CIPropColliders <name>[,<name>...]")]
		public static void PropCollidersCommand(string[] args)
		{
			if (args == null || args.Length < 1) { Fail("CIPropColliders <name>[,<name>...]"); return; }
			StartTest(PropCollidersRoutine(string.Join(" ", args)));
		}

		static IEnumerator PropCollidersRoutine(string names)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			Func<Vector3, string> v = x => Num(x.x) + "/" + Num(x.y) + "/" + Num(x.z);
			foreach (string n in names.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
			{
				GameObject proto = PlaceableCatalog.Get(n);
				if (proto == null) { Log("colliders " + n + ": not in this Raft"); continue; }
				GameObject copy = UnityEngine.Object.Instantiate(proto, new Vector3(0f, 3000f, 0f), Quaternion.identity);
				copy.SetActive(true);
				Vector3 at = copy.transform.position;
				var parts = new List<string>();
				Renderer[] rs = global::DynamicIslands.Editor.PlacementOptions.ShapeRenderers(copy);
				if (rs.Length > 0) { Bounds m = rs[0].bounds; foreach (Renderer r in rs) m.Encapsulate(r.bounds); parts.Add("meshes " + v(m.min - at) + " .. " + v(m.max - at)); }
				foreach (Collider c in copy.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger))
					parts.Add(c.name + " " + c.GetType().Name + (c.enabled && c.gameObject.activeInHierarchy ? "" : " (off)") + " " + v(c.bounds.min - at) + " .. " + v(c.bounds.max - at));
				Log("colliders " + n + ": scale " + v(copy.transform.lossyScale) + "; " + string.Join("; ", parts.ToArray()));
				UnityEngine.Object.Destroy(copy);
			}
			Log("PASS: prop colliders");
		}

		[ConsoleCommand(name: "CIMeasureProps", docs: "Dev, main menu or editor: measures Raft's objects the randomizer builds with (size, bottom; the inside of cave pieces) into raft_props.txt in Mods\\DynamicIslands. CIMeasureProps [names, comma separated]")]
		public static void MeasurePropsCommand(string[] args)
		{
			string[] names = args != null && args.Length > 0 ? string.Join(" ", args).Split(',').Select(s => s.Trim()).ToArray() : MeasuredProps;
			StartTest(MeasureProps(names));
		}

		/// <summary>Measures objects into raft_props.txt (the randomizer's list), or into another file (fileName: a path; the
		/// randomizer's list is then left alone - the recipes' object lists).</summary>
		static IEnumerator MeasureProps(string[] names, string fileName = null)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			yield return PlaceableCatalog.EnsureLoaded(names);
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			var lines = new List<string>
			{
				"# Raft's objects the randomizer builds with (CIMeasureProps, raft=" + Application.version + ")",
				"# prop\t<name>\t<scene>\t<size x,y,z>\t<centre x,y,z>\t<bottom below the pivot>\t<solid colliders>   (at the scale the object spawns with)",
				"# cave\t<name>\t<axis of the passage 0=x 2=z>\t<floor above the pivot>\t<headroom>\t<width>\t<open at +axis>\t<open at -axis>\t<a point in the passage, from the pivot>\t<room from it towards +axis>\t<and towards -axis> (99 = open)",
			};
			var lab = new GameObject("CI_PropLab");
			lab.transform.position = new Vector3(0f, 3000f, 0f);
			int measured = 0, missing = 0;
			foreach (string n in names)
			{
				GameObject proto = PlaceableCatalog.Get(n);
				Bounds b;
				if (proto == null || !PlaceableCatalog.LocalBounds(n, out b)) { Log("prop " + n + ": not in this Raft"); missing++; continue; }
				Vector3 s = proto.transform.localScale;
				var size = Vector3.Scale(b.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
				var centre = Vector3.Scale(b.center, s);
				float bottom = b.min.y * s.y;
				int colliders = proto.GetComponentsInChildren<Collider>(true).Count(c => !c.isTrigger);
				lines.Add(string.Join("\t", new[] { "prop", n, PlaceableCatalog.SceneOf(n) ?? "", RaftProps.Text(size), RaftProps.Text(centre), bottom.ToString("0.###", inv), colliders.ToString(inv) }));
				measured++;
				if (!System.Text.RegularExpressions.Regex.IsMatch(n, "Cave|Tunnel|Ravine")) continue;

				// Cave pieces: where the passage is. Rays across it along x and along z at a few heights: a passage lets them
				// in (far, or all the way through); the floor is the highest thing a ray from inside finds below the middle.
				GameObject go = PlaceableCatalog.Spawn(n, lab.transform, false);
				if (go == null) continue;
				go.transform.localPosition = Vector3.zero;
				go.transform.localRotation = Quaternion.identity;
				yield return new WaitForFixedUpdate();
				Physics.SyncTransforms();
				Bounds w = new Bounds(go.transform.position, Vector3.zero);
				bool any = false;
				foreach (Collider c in go.GetComponentsInChildren<Collider>()) { if (c.isTrigger) continue; if (!any) { w = c.bounds; any = true; } else w.Encapsulate(c.bounds); }
				if (!any) { Log("cave " + n + ": no colliders"); UnityEngine.Object.Destroy(go); continue; }
				// (Raft's cave meshes face inwards: rays must see back faces to find them from outside or inside)
				bool backfaces = Physics.queriesHitBackfaces;
				Physics.queriesHitBackfaces = true;
				try
				{
					Func<Vector3, Vector3, float, float> depth = (from, dir, max) =>
					{
						float best = max;
						foreach (RaycastHit h in Physics.RaycastAll(from, dir, max, ~0, QueryTriggerInteraction.Ignore))
							if (h.collider.transform.IsChildOf(go.transform) && h.distance < best) best = h.distance;
						return best;
					};
					// A point inside the passage: a floor below and a roof above it, walls close on two sides, far or open on the others
					int bestAxis = -1; float bestAlong = 0f, bestWidth = 0f, bestHead = 0f, bestFloor = 0f, toPlus = 0f, toMinus = 0f; bool plus = false, minus = false;
					Vector3 bestAt = w.center;
					float reach = Mathf.Max(w.size.x, w.size.z) + 2f;
					foreach (Vector2 o in new[] { Vector2.zero, new Vector2(0.25f, 0f), new Vector2(-0.25f, 0f), new Vector2(0f, 0.25f), new Vector2(0f, -0.25f) })
						for (int k = 1; k <= 24; k++)
						{
							Vector3 q = new Vector3(w.center.x + o.x * w.size.x, w.min.y + w.size.y * k / 25f, w.center.z + o.y * w.size.z);
							float dd = depth(q, Vector3.down, w.size.y + 1f), du = depth(q, Vector3.up, w.size.y + 1f);
							if (dd > w.size.y || du > w.size.y || dd + du < 1.8f || dd > 3f) continue; // (standing height over its floor)
							float px = depth(q, Vector3.right, reach), nx = depth(q, Vector3.left, reach), pz = depth(q, Vector3.forward, reach), nz = depth(q, Vector3.back, reach);
							bool alongX = px + nx > pz + nz;
							float along = alongX ? px + nx : pz + nz, across = alongX ? pz + nz : px + nx;
							if (across < 1.5f || across > 20f) continue;
							if (along <= bestAlong + 0.01f) continue;
							bestAlong = along; bestAxis = alongX ? 0 : 2; bestWidth = across; bestHead = dd + du; bestFloor = q.y - dd - go.transform.position.y; bestAt = q;
							toPlus = Mathf.Min(alongX ? px : pz, 99f); toMinus = Mathf.Min(alongX ? nx : nz, 99f);
							// Open where a ray leaves the piece without meeting anything
							plus = alongX ? px >= reach - 0.01f || q.x + px > w.max.x + 0.4f : pz >= reach - 0.01f || q.z + pz > w.max.z + 0.4f;
							minus = alongX ? nx >= reach - 0.01f || q.x - nx < w.min.x - 0.4f : nz >= reach - 0.01f || q.z - nz < w.min.z - 0.4f;
						}
					if (bestAxis < 0) { Log("cave " + n + ": no passage found; size " + RaftProps.Text(size)); UnityEngine.Object.Destroy(go); continue; }
					lines.Add(string.Join("\t", new[] { "cave", n, bestAxis.ToString(inv), bestFloor.ToString("0.##", inv), bestHead.ToString("0.##", inv), bestWidth.ToString("0.##", inv), plus ? "1" : "0", minus ? "1" : "0",
						RaftProps.Text(bestAt - go.transform.position), toPlus.ToString("0.##", inv), toMinus.ToString("0.##", inv) }));
					Log("cave " + n + ": passage along " + (bestAxis == 0 ? "x" : "z") + " (" + bestAlong.ToString("0.0", inv) + " m seen), floor " + bestFloor.ToString("0.0", inv) + " m above the pivot, headroom " + bestHead.ToString("0.0", inv) +
						" m, width " + bestWidth.ToString("0.0", inv) + " m, open at " + (plus ? "+" : "") + (minus ? "-" : "") + (plus || minus ? "" : "neither") + "; size " + RaftProps.Text(size) + ", centre " + RaftProps.Text(centre) +
						", probe at " + RaftProps.Text(bestAt - go.transform.position));
				}
				finally { Physics.queriesHitBackfaces = backfaces; }
				UnityEngine.Object.Destroy(go);
			}
			UnityEngine.Object.Destroy(lab);
			string path = fileName ?? System.IO.Path.Combine(DynamicIslands.assetpath, RaftProps.FileName);
			System.IO.File.WriteAllLines(path, lines.ToArray());
			if (fileName == null) RaftProps.Reload();
			Log("Measured " + measured + " props (" + missing + " not in this Raft): " + System.IO.Path.GetFullPath(path));
			if (measured > 0) Log("PASS: measured props"); else Fail("measured no props");
		}

		[ConsoleCommand(name: "CIProbeSetPieces", docs: "Dev: size and footprint of Raft objects used as set pieces on oddity islands, and item names for rewards. CIProbeSetPieces [names, comma separated]")]
		public static void ProbeSetPiecesCommand(string[] args)
		{
			string[] names = args != null && args.Length > 0 ? string.Join(" ", args).Split(',').Select(s => s.Trim()).ToArray() :
				new[] { "Van_1", "Van_2", "Van3", "Van_4", "Van_5", "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Green_02", "Caravan_Yellow_01", "CaravanBarebones1", "Airplane", "BoatStranded", "BoatMini",
					"Balboa_Shack", "Balboa_DecorationPrefabBase_SimpleTent", "CaravanRocket", "CaravanRocketDebris_Body1", "CaravanRocketDebris_Body2", "CaravanRocketDebris_Top1", "CaravanRocketDebris_Exhaust",
					"CaravanRocketDebris_Leg1", "CaravanRocketDebris_Door", "CaravanRocketDebris_Canister", "TangaroaFounderStatue", "RaftMonument", "Well", "RT_PlasticBoat", "RT_SharkCage", "Tire_02", "Pallet",
					"ReefHuts_Wall2", "ReefHuts_Tarpaulin", "Crate_Big_01", "Scarecrow", "RT_WindMill", "Scaffolding_2x2m" };
			StartTest(ProbeSetPieces(names));
		}

		static IEnumerator ProbeSetPieces(string[] names)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			yield return PlaceableCatalog.EnsureLoaded(names);
			foreach (string n in names)
			{
				Bounds b;
				GameObject proto = PlaceableCatalog.Get(n);
				if (proto == null) { Log("piece " + n + ": not in the catalog"); continue; }
				if (!PlaceableCatalog.LocalBounds(n, out b)) { Log("piece " + n + ": no meshes"); continue; }
				Log("piece " + n + ": size " + b.size.ToString("F1") + " centre " + b.center.ToString("F1") + " bottom " + b.min.y.ToString("F2") + " scale " + proto.transform.localScale.ToString("F2") +
					" colliders " + proto.GetComponentsInChildren<Collider>(true).Count(c => !c.isTrigger));
			}
			var items = new List<string>();
			try { foreach (Item_Base i in ItemManager.GetAllItems()) items.Add(i.UniqueName); } catch (Exception e) { Log("items: " + e.Message); }
			Log("items " + items.Count + ": " + string.Join(" ", items.Where(i => System.Text.RegularExpressions.Regex.IsMatch(i, "Shark|Pork|Leather|Feather|Wool|Meat|Bear|Trophy|Head|Raw_|Hide|Tooth|Fur|Egg|Milk|Titan|Gold|Map", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToArray()));
			Log("PASS: probed set pieces");
		}

		static void ProbeLandmarks(float max)
		{
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			Network_Host_Entities host = ComponentManager<Network_Host_Entities>.Value;
			var agentOf = new Dictionary<AI_NetworkBehaviourType, int>();
			if (host != null && host.AINetworkBehaviourPrefabs != null)
				foreach (AI_NetworkBehaviour p in host.AINetworkBehaviourPrefabs)
				{
					if (p == null) continue;
					NavMeshAgent a = p.GetComponentInChildren<NavMeshAgent>(true);
					Log("prefab " + p.behaviourType + " " + p.GetType().Name + (a != null ? " agent " + a.agentTypeID + " (" + NavMesh.GetSettingsNameFromID(a.agentTypeID) + ")" : " no agent") +
						" scale " + p.localScaleInterval.minValue.ToString("0.##") + "-" + p.localScaleInterval.maxValue.ToString("0.##"));
					if (a != null) agentOf[p.behaviourType] = a.agentTypeID;
				}

			ChunkManager cm = ComponentManager<ChunkManager>.Value;
			if (cm != null)
				foreach (ChunkPoint cp in cm.GetAllChunkPointsList().Where(c => (c.worldPosition - raft).magnitude < max).OrderBy(c => (c.worldPosition - raft).magnitude))
					Log("point " + (cp.rule != null ? cp.rule.name : "?") + " at " + (cp.worldPosition - raft).magnitude.ToString("F0") + " m, type " + cp.rule?.ChunkPointType + ", remove at " + cp.RemoveDistanceFromRaft.ToString("F0") +
						", spawned " + (cp.spawnedObject != null ? cp.spawnedObject.name + " #" + cp.spawnedObject.ObjectIndex + " at " + (cp.spawnedObject.transform.position - cp.worldPosition).magnitude.ToString("F1") + " m from the point" : "nothing"));

			foreach (Landmark l in WorldManager.AllLandmarks.Where(l => l != null))
			{
				float d = (l.transform.position - raft).magnitude;
				if (!l.isSpawned || d > max) continue;
				var sb = new StringBuilder();
				PickupItem_Networked net = l.GetComponent<PickupItem_Networked>();
				sb.Append("landmark '").Append(l.name).Append("' unique ").Append(l.uniqueLandmarkIndex).Append(" #").Append(net != null ? net.ObjectIndex.ToString() : "-").Append(" at ").Append(d.ToString("F0")).Append(" m");
				Log(sb.ToString());
				if (l.landmarkItems != null)
					foreach (var g in l.landmarkItems.Where(i => i != null).GroupBy(i => i.GetType().Name + " " + PlaceableCatalog.CleanName(i.name)).OrderByDescending(g => g.Count()))
						Log("  item " + g.Key + " x" + g.Count() + " (y " + string.Join(",", g.Take(4).Select(i => (i.transform.position.y).ToString("F0")).ToArray()) + ")");
				if (l.spawners != null)
					foreach (LandmarkEntitySpawner s in l.spawners.Where(s => s != null))
					{
						NavMeshHit hit;
						int agent;
						bool nav = agentOf.TryGetValue(s.entityType, out agent) && NavMesh.SamplePosition(s.transform.position, out hit, 6f, new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas });
						Log("  spawner " + s.GetType().Name + " " + s.entityType + " [" + string.Join(",", (s.spawnabeEntityTypes ?? new AI_NetworkBehaviourType[0]).Select(t => t.ToString()).ToArray()) + "] should " + s.ShouldSpawn +
							" has " + (s.spawnedEntityBehaviour != null) + " nav " + nav + " at " + s.transform.localPosition.ToString("F0"));
					}
				// NavMesh per agent kind around the island
				foreach (var kv in agentOf)
				{
					int found = 0;
					for (int i = 0; i < 40; i++)
					{
						float a = i * 2.4f, r = 10f + (i % 8) * 12f;
						Vector3 p = l.transform.position + new Vector3(Mathf.Cos(a) * r, 30f, Mathf.Sin(a) * r);
						NavMeshHit hit;
						if (NavMesh.SamplePosition(p, out hit, 60f, new NavMeshQueryFilter { agentTypeID = kv.Value, areaMask = NavMesh.AllAreas })) found++;
					}
					if (found > 0) Log("  navmesh for " + kv.Key + ": " + found + "/40 probes");
				}
			}

			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>())
			{
				var mats = ai.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList();
				Log("animal " + ai.behaviourType + " " + ai.GetType().Name + " #" + ai.ObjectIndex + " at " + (ai.transform.position - raft).magnitude.ToString("F0") + " m, scale " + ai.transform.localScale.x.ToString("0.##") +
					", spawner " + (ai.connectedSpawner != null ? ai.connectedSpawner.name + (ai.connectedSpawner.landmark != null ? " of " + ai.connectedSpawner.landmark.name : "") : "none") +
					", health " + (ai.networkEntity != null && ai.networkEntity.stat_health != null ? ai.networkEntity.stat_health.Max.ToString("F0") : "?") +
					", materials " + string.Join(", ", mats.Select(m => m.name + "/" + m.shader.name + (m.HasProperty("_Color") ? "" : " (no _Color)")).Distinct().Take(4).ToArray()));
			}
			foreach (AI_NetworkBehaviour ai in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a.behaviourType == AI_NetworkBehaviourType.Shark))
			{
				Log("shark #" + ai.ObjectIndex + " active " + ai.gameObject.activeInHierarchy + " at " + ai.transform.position.ToString("F0") + ", children " + ai.transform.childCount);
				foreach (Renderer r in ai.GetComponentsInChildren<Renderer>(true))
					Log("  renderer " + r.GetType().Name + " '" + r.name + "' enabled " + r.enabled + " active " + r.gameObject.activeInHierarchy + ": " + string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name + "/" + m.shader.name).ToArray()));
			}
			foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>().Where(r => r.name.IndexOf("shark", StringComparison.OrdinalIgnoreCase) >= 0 || (r.sharedMaterial != null && r.sharedMaterial.name.IndexOf("shark", StringComparison.OrdinalIgnoreCase) >= 0)))
				Log("shark-like renderer '" + r.name + "' under '" + (r.transform.root != null ? r.transform.root.name : "") + "' at " + (r.transform.position - raft).magnitude.ToString("F0") + " m: " + string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name + "/" + m.shader.name).ToArray()));
			Log("PASS: probed");
		}
	}
}
