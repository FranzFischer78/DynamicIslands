using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Everything on the islands as they are spawned in a world stands on something (TEST_CATALOGUE IG1-IG4, tools\groundall.ps1):
	/// no structure, cave, prop or plant floating over the land or the sea floor, no base half in the air on a slope, nothing
	/// buried whole, every cave rim on the ground. CIFloating measures the same in the editor; these measure the spawned result
	/// - the land as cropped and placed, the randomizer's extras on Raft's own islands, plan and story islands, new generated ones.
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIGroundWorld", docs: "Dev, in a world: every object on the loaded custom islands (the randomizer's extras too) stands on something - nothing more than 0.5 m over what is under it and touching nothing, no base on the ground with part of it more than 0.6 m in the air, nothing buried whole, every cave rim on the ground (IG1). Problems listed per island; all of them in Mods\\DynamicIslands\\ground_world.txt. CIGroundWorld [part of the island name]")]
		public static void GroundWorldCommand(string[] args) { WorldCheck(args, GroundCheck); }

		[ConsoleCommand(name: "CIRenderers", docs: "Dev: an object's parts as spawned - each renderer (kind, on/off, shadow mode, its box from the pivot) and each collider - to see what 'sit' and the ground checks measure. CIRenderers <object> [scale]")]
		public static void RenderersCommand(string[] args)
		{
			if (args == null || args.Length == 0) { Fail("CIRenderers <object> [scale]"); return; }
			StartTest(RenderersRoutine(args[0], args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1f));
		}

		static IEnumerator RenderersRoutine(string name, float scale)
		{
			yield return PlaceableCatalog.EnsureLoaded(new[] { name });
			GameObject g = PlaceableCatalog.Spawn(name, null);
			if (g == null) { Fail("CIRenderers: no object " + name); yield break; }
			g.transform.position = new Vector3(0f, 500f, 0f);
			g.transform.localScale = Vector3.one * scale;
			yield return null;
			Physics.SyncTransforms();
			Vector3 p = g.transform.position;
			foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
				Log("  renderer " + r.GetType().Name + " '" + r.name + "' " + (r.enabled && r.gameObject.activeInHierarchy ? "on" : "off") + " shadows=" + r.shadowCastingMode +
					" min " + Num(r.bounds.min.y - p.y) + " max " + Num(r.bounds.max.y - p.y) + " size " + Num(r.bounds.size.x) + "x" + Num(r.bounds.size.y) + "x" + Num(r.bounds.size.z));
			foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
				Log("  collider " + c.GetType().Name + " '" + c.name + "' " + (c.enabled && c.gameObject.activeInHierarchy ? "on" : "off") + (c.isTrigger ? " trigger" : "") +
					" min " + Num(c.bounds.min.y - p.y) + " max " + Num(c.bounds.max.y - p.y));
			UnityEngine.Object.Destroy(g);
			Log("PASS: renderers of " + name + " listed");
		}

		/// <summary>A check of the objects on spawned islands (CIGround* / CIOverlap*): its name in the results, its files'
		/// prefix, what a pass says, the measure.</summary>
		class IslandCheck { public string Label, File, Pass; public IslandMeasure Measure; }
		delegate List<string> IslandMeasure(IslandWorldState.Entry e, out int count);
		static readonly IslandCheck GroundCheck = new IslandCheck { Label = "ground", File = "ground", Pass = "all on something", Measure = GroundProblems };

		static void WorldCheck(string[] args, IslandCheck c)
		{
			string only = args != null && args.Length > 0 && args[0] != "*" ? args[0] : "";
			var report = new StringBuilder();
			int islands = 0, bad = 0, problems = 0, objects = 0;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(x => x.Root != null && !x.Loading).ToList())
			{
				if (only.Length > 0 && (e.HostName ?? e.Name ?? "").IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) continue;
				islands++;
				int n;
				List<string> lines = c.Measure(e, out n);
				objects += n;
				report.AppendLine(e.Name + ": " + n + " objects, " + lines.Count + " problems");
				foreach (string l in lines) report.AppendLine("  " + l);
				if (lines.Count == 0) continue;
				bad++;
				problems += lines.Count;
				Log("  '" + e.Name + "': " + lines.Count + " problem(s) of " + n + " objects");
				foreach (string l in lines.Take(5)) Log("    " + l);
			}
			File.WriteAllText(Path.Combine(DynamicIslands.assetpath, c.File + "_world.txt"), report.ToString());
			if (islands == 0) Fail(c.Label + ": no loaded custom island" + (only.Length > 0 ? " named *" + only + "*" : ""));
			else if (bad > 0) Fail(c.Label + ": " + bad + " of " + islands + " islands with " + problems + " problems (" + c.File + "_world.txt)");
			else Log("PASS: " + c.Label + " - " + islands + " islands, " + objects + " objects, " + c.Pass);
		}

		[ConsoleCommand(name: "CIGroundIslands", docs: "Dev, world (host, a test world 'CI ...'): saved islands brought next to the raft one by one and measured as CIGroundWorld does, then taken away (IG2/IG3). <which>: part of the name (several joined by | - Geiger|Gannet_Rock, _ for a space), 'plans' (every island of every plan - the story lines), 'gen' (the saved generated ones) or * (all but the test islands, kept copies and randomizer extras). Results in Mods\\DynamicIslands\\ground_islands.txt. CIGroundIslands <which> [most] [skip the first n] [keep: left there, where each is logged - to look at it]")]
		public static void GroundIslandsCommand(string[] args) { IslandsCheck(args, GroundCheck); }

		static void IslandsCheck(string[] args, IslandCheck c)
		{
			string which = args != null && args.Length > 0 ? args[0] : "*";
			int most = args != null && args.Length > 1 ? int.Parse(args[1]) : 9999;
			int skip = args != null && args.Length > 2 ? int.Parse(args[2]) : 0;
			bool keep = args != null && args.Length > 3 && args[3] == "keep";
			StartTest(GroundIslandsRoutine(c, which, most, skip, keep));
		}

		static IEnumerator GroundIslandsRoutine(IslandCheck c, string which, int most, int skip, bool keep = false)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail(c.Label + " islands: host, in a world"); yield break; }
			var kept = new System.Text.RegularExpressions.Regex("_[0-9a-f]{12}$");
			var saved = new HashSet<string>(IslandSpawner.ListSavedIslands(), StringComparer.OrdinalIgnoreCase);
			IEnumerable<string> pick;
			// (several names in one run, joined by | - SPEED_RULES 6, gap check 2026-10-20; _ stands for a space, as a
			// command's words are split at spaces)
			string[] parts = which.Split('|').Select(p => p.Replace('_', ' ').Trim()).Where(p => p.Length > 0).ToArray();
			if (which == "plans") pick = CustomIslandSpawner.AllPlansIslandNames().Where(n => saved.Contains(n));
			else pick = saved.Where(n => !n.StartsWith("ci", StringComparison.Ordinal) && !kept.IsMatch(n) && !n.StartsWith(WorldRandomizer.ExtrasPrefix, StringComparison.OrdinalIgnoreCase) &&
				(which == "*" || parts.Any(p => n.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)));
			List<string> names = pick.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).Skip(skip).Take(most).ToList();
			Log(c.Label + " check of " + names.Count + " saved islands (" + which + ", from " + skip + ")");
			var report = new StringBuilder();
			int done = 0, bad = 0, problems = 0, missed = 0;
			foreach (string name in names)
			{
				var left = IslandWorldState.Islands.Where(x => string.Equals(x.HostName, name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToList();
				if (left.Count > 0) yield return RemoveIslandsAndWait(left);
				Vector3? spot = ScSpot(name, 400f);
				if (!spot.HasValue) { Log("  " + name + ": no open sea near the raft"); missed++; continue; }
				var made = new List<IslandWorldState.Entry>();
				yield return ScBring(name, spot.Value, made);
				IslandWorldState.Entry e = made.LastOrDefault();
				if (e == null || e.Root == null) { Log("  " + name + ": didn't come"); missed++; continue; }
				yield return WaitIslandSettled(e, 0.5f, 2f); // (the cave mountains and Raft's physics settle: until they have, 2 s at most)
				int n;
				List<string> lines = c.Measure(e, out n);
				done++;
				report.AppendLine(name + ": " + n + " objects, " + lines.Count + " problems");
				foreach (string l in lines) report.AppendLine("  " + l);
				if (lines.Count > 0)
				{
					bad++;
					problems += lines.Count;
					Log("  " + name + ": " + lines.Count + " problem(s) of " + n + " objects");
					foreach (string l in lines.Take(3)) Log("    " + l);
				}
				if (keep) { Log("  kept '" + name + "' at " + e.Root.transform.position.ToString("F1") + ", turned " + e.Root.transform.eulerAngles.y.ToString("F0") + "°"); continue; }
				yield return RemoveIslandsAndWait(made.Select(x => x.Id).ToList());
			}
			File.WriteAllText(Path.Combine(DynamicIslands.assetpath, c.File + "_islands.txt"), report.ToString());
			string sum = done + " of " + names.Count + " islands measured" + (missed > 0 ? " (" + missed + " didn't come)" : "");
			if (names.Count == 0) Fail(c.Label + " islands: none matched '" + which + "'");
			else if (bad > 0 || missed > 0) Fail(c.Label + " islands: " + bad + " with " + problems + " problems; " + sum + " (" + c.File + "_islands.txt)");
			else Log("PASS: " + c.Label + " islands - " + sum + ", " + c.Pass);
		}

		[ConsoleCommand(name: "CIGroundTypes", docs: "Dev, world (host, a test world 'CI ...'): brand-new islands of each map type made next to the raft by the generator as it is now, measured as CIGroundWorld does, then taken away (IG6) - a saved generated island keeps what an older generator made. Results in Mods\\DynamicIslands\\ground_types.txt. CIGroundTypes [types, comma-separated, or *] [islands of each, 2]")]
		public static void GroundTypesCommand(string[] args) { TypesCheck(args, GroundCheck); }

		static void TypesCheck(string[] args, IslandCheck c)
		{
			string which = args != null && args.Length > 0 ? args[0] : "*";
			int each = args != null && args.Length > 1 ? int.Parse(args[1]) : 2;
			StartTest(GroundTypesRoutine(c, which, each));
		}

		static IEnumerator GroundTypesRoutine(IslandCheck c, string which, int each)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail(c.Label + " types: host, in a world"); yield break; }
			List<string> types = (which == "*" ? MapTypes.All.Select(t => t.Name).Where(t => t != "random") : which.Split(',').Select(t => t.Trim()).Where(t => MapTypes.Get(t) != null)).ToList();
			if (types.Count == 0) { Fail(c.Label + " types: no map type '" + which + "'"); yield break; }
			Log(c.Label + " check of new islands: " + string.Join(", ", types.ToArray()) + ", " + each + " of each");
			var report = new StringBuilder();
			int done = 0, bad = 0, problems = 0, missed = 0;
			foreach (string type in types)
				for (int i = 0; i < each; i++)
				{
					Network_Player player = RAPI.GetLocalPlayer();
					Vector3 raft = CustomIslandSpawner.RaftPosition ?? (player != null ? player.transform.position : Vector3.zero);
					IslandWorldState.Entry e = SpawnTypeNear(type, raft);
					for (float t = 0; e != null && t < 90f && e.Root == null && !e.Failed; t += 0.1f) yield return new WaitForSeconds(0.1f);
					if (e == null || e.Root == null)
					{
						Log("  " + type + ": " + (e == null ? "no open sea near the raft" : "didn't come"));
						missed++;
						if (e != null) IslandWorldState.RemoveIds(new[] { e.Id }, true);
						continue;
					}
					yield return WaitIslandSettled(e, 0.5f, 2f); // (the cave mountains and Raft's physics settle: until they have, 2 s at most)
					int n;
					List<string> lines = c.Measure(e, out n);
					done++;
					report.AppendLine(type + " (" + e.Name + "): " + n + " objects, " + lines.Count + " problems");
					foreach (string l in lines) report.AppendLine("  " + l);
					if (lines.Count > 0)
					{
						bad++;
						problems += lines.Count;
						Log("  " + e.Name + ": " + lines.Count + " problem(s) of " + n + " objects");
						foreach (string l in lines.Take(3)) Log("    " + l);
					}
					yield return RemoveIslandsAndWait(new[] { e.Id });
				}
			File.WriteAllText(Path.Combine(DynamicIslands.assetpath, c.File + "_types.txt"), report.ToString());
			string sum = done + " of " + types.Count * each + " new islands measured" + (missed > 0 ? " (" + missed + " didn't come)" : "");
			if (bad > 0 || missed > 0) Fail(c.Label + " types: " + bad + " with " + problems + " problems; " + sum + " (" + c.File + "_types.txt)");
			else Log("PASS: " + c.Label + " types - " + sum + ", " + c.Pass);
		}

		[ConsoleCommand(name: "CIScrapyardExcavator", docs: "Dev, world (host, a test world 'CI ...'): large islands (the only map type with themed scenes) made by the generator as it is now, one after another, until one has the scrapyard scene with the excavator as its centrepiece (a large island has a scrapyard in about one of four and its centrepiece is one of excavator / forklift / dumpster, so a dozen fresh islands may have none - GAP22). That island is brought next to the raft, measured as CIGroundTypes does (the excavator's own lines decide) and for how deep its tracks sit (fails when even the shallowest of 5 spots is over 0.35 m in - GAP25), the excavator pictured (scrapyard_excavator) and taken away. CIScrapyardExcavator [islands to try, 80] [keep]")]
		public static void ScrapyardExcavatorCommand(string[] args)
		{
			int tries = 80, t;
			if (args != null && args.Length > 0 && int.TryParse(args[0], out t)) tries = t;
			StartTest(ScrapyardExcavatorRoutine(tries, args != null && args.Contains("keep")));
		}

		static IEnumerator ScrapyardExcavatorRoutine(int tries, bool keep)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("scrapyard excavator: host, in a world"); yield break; }
			Theme yard = RandomizerIslands.ThemeOf("scrapyard");
			if (yard == null || !yard.Anchors.Contains("VP_Excavator")) { Fail("scrapyard excavator: the scrapyard scene has no excavator centrepiece any more"); yield break; }
			if (!RaftProps.Standable("VP_Excavator")) { Fail("scrapyard excavator: VP_Excavator isn't standable, so the generator never picks it"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			// (only a large island gets themed scenes - RandomizerIslands.Large -> Dress - and no map type sets IslandGenSettings.Buildings,
			// so the other types never have the scrapyard: with the tries spread over every type, most were wasted and a run found none, 2026-10-21)
			List<MapType> types = MapTypes.All.Where(x => x.Name == "large" || x.ContentFrom == "large").ToList();
			if (types.Count == 0) { Fail("scrapyard excavator: no map type makes large islands, the only ones with themed scenes"); yield break; }
			var rnd = new System.Random();
			var seen = new Dictionary<string, int>();
			IslandFile found = null;
			MapType foundType = null;
			IslandGenSettings foundS = null;
			float foundEl = 0f, start = Time.realtimeSinceStartup;
			int made = 0, failed = 0;
			Log("scrapyard excavator: making up to " + tries + " islands until one has the scrapyard's excavator");
			for (int i = 0; i < tries && found == null && Time.realtimeSinceStartup - start < 300f; i++)
			{
				MapType type = types[i % types.Count];
				float el;
				IslandGenSettings s = MapTypes.Roll(type, rnd, out el);
				IslandFile f = null;
				try { f = MapTypes.Create(type, s, el, MapTypes.FileName(type, s)); made++; }
				catch (Exception ex) { failed++; Log("  " + type.Name + ": generating failed - " + ex.Message); }
				if (f != null)
				{
					// (the scene's centrepiece is a set piece; the Varuna look's excavator stands without one)
					foreach (IslandObject o in f.Objects.Where(o => o.Props != null && o.Props.ContainsKey("set.piece") && yard.Anchors.Contains(o.Name)))
					{
						int c;
						seen[o.Name] = (seen.TryGetValue(o.Name, out c) ? c : 0) + 1;
						if (o.Name == "VP_Excavator" && found == null) { found = f; foundType = type; foundS = s; foundEl = el; }
					}
				}
				yield return null;
			}
			string stats = made + " island(s) made" + (failed > 0 ? ", " + failed + " failed" : "") + "; scrapyard centrepieces: " +
				(seen.Count == 0 ? "none" : string.Join(", ", seen.Select(x => x.Key + " " + x.Value).ToArray()));
			Log("  " + stats);
			if (found == null) { Fail("scrapyard excavator: none in " + stats); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? (player != null ? player.transform.position : Vector3.zero);
			string name = MapTypes.FileName(foundType, foundS);
			float radius = MapTypes.EstimatedRadius(foundS);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft, radius, 780f);
			if (!spot.HasValue) { Fail("scrapyard excavator: no open sea near the raft for " + name); yield break; }
			CustomIslandSpawner.CacheSize(name, radius, foundEl);
			IslandWorldState.Entry e = IslandWorldState.Add(name, spot.Value, null, false);
			e.Loading = true;
			IslandFile ready = found;
			StartTest(CustomIslandSpawner.GenerateAndSpawn(() => ready, name, e));
			for (float w = 0; w < 90f && e.Root == null && !e.Failed; w += 0.1f) yield return new WaitForSeconds(0.1f);
			if (e.Root == null) { Fail("scrapyard excavator: " + name + " didn't come"); IslandWorldState.RemoveIds(new[] { e.Id }, true); yield break; }
			yield return WaitIslandSettled(e, 0.5f, 2f);
			int n;
			List<string> lines = GroundProblems(e, out n);
			List<string> mine = lines.Where(l => l.Contains("VP_Excavator")).ToList();
			Transform exc = e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(r => r.ObjectName == "VP_Excavator" && r.Props != null && r.Props.ContainsKey("set.piece")).Select(r => r.transform).FirstOrDefault();
			if (exc != null && player != null) { SetHour(11f); yield return ShotOf(player, exc, "scrapyard_excavator", 12f); }
			foreach (string l in lines.Take(5)) Log("    " + l);
			// How deep its tracks sit: they start 0.8 m over the pivot (RaftProps.SeenBottoms); measured under the middle and 1.5 m
			// out to each side, so a slope counts. Sunk = even the shallowest spot over 0.35 m in (Raft's own Varuna Point: 0-0.1 m;
			// the full run of 2026-10-21 saw gen-large-233873's pivot 1.1 m under the sand, GAP25)
			string sinkNote = "";
			bool sunk = false;
			Terrain land = exc != null ? e.Root.GetComponentInChildren<Terrain>() : null;
			if (land != null)
			{
				float tracks = exc.position.y + 0.8f * exc.lossyScale.y;
				List<float> depths = new[] { Vector3.zero, exc.right, -exc.right, exc.forward, -exc.forward }
					.Select(d => land.SampleHeight(exc.position + d * 1.5f) + land.transform.position.y - tracks).ToList();
				sinkNote = "; tracks " + string.Join("/", depths.Select(x => Num(x)).ToArray()) + " m in the ground (middle, sides)";
				sunk = depths.Min() > 0.35f;
				Log("  excavator" + sinkNote.Substring(1));
				// (where its parts start, from the pivot: is SeenBottoms' 0.8 right?)
				float groundMid = land.SampleHeight(exc.position) + land.transform.position.y;
				Log("  excavator pivot " + Num(groundMid - exc.position.y) + " m under the ground, scale " + Num(exc.lossyScale.y) + "; parts' bottoms over the pivot: " +
					string.Join(", ", exc.GetComponentsInChildren<Renderer>(true).Select(r => r.name + (r.enabled && r.gameObject.activeInHierarchy ? "" : " (hidden)") + " " +
						Num(r.bounds.min.y - exc.position.y)).Take(12).ToArray()));
			}
			// (read before the removal: a destroyed Transform compares equal to null, 2026-10-21)
			bool hasExc = exc != null;
			if (!keep) yield return RemoveIslandsAndWait(new[] { e.Id });
			if (!hasExc) Fail("scrapyard excavator: " + name + " came without its excavator (" + n + " objects)");
			else if (mine.Count > 0) Fail("scrapyard excavator (" + name + "): " + string.Join("; ", mine.ToArray()));
			else if (sunk) Fail("scrapyard excavator (" + name + "): sunk into the ground" + sinkNote);
			else Log("PASS: scrapyard excavator - on " + name + " (" + foundType.Name + "), on the ground" + sinkNote +
				(lines.Count > 0 ? "; " + lines.Count + " other problem(s) on the island, not the excavator's (CIGroundTypes covers them)" : "") + "; " + stats);
		}

		[ConsoleCommand(name: "CIShackDish", docs: "Dev, world (host, a test world 'CI ...'): a hermit's shack island made by the generator as it is now, with the bear scene's satellite dish forced in as the first of the props sprinkled round the shack (by chance only about one shack in four gets one - 78f903d), brought next to the raft and measured: the dish's pivot on its antenna pole's axis (within 0.05 m) and 6.0 m (within 0.1) over the pole's base, the pole's base on the ground (0.2 m over to 0.6 m under it at most) and no ground-check line about the dish or the pole. Taken away after. CIShackDish [shacks to try for one with the dish placed, 6] [keep]")]
		public static void ShackDishCommand(string[] args)
		{
			int tries = 6, t;
			if (args != null && args.Length > 0 && int.TryParse(args[0], out t)) tries = t;
			StartTest(ShackDishRoutine(tries, args != null && args.Contains("keep")));
		}

		static IEnumerator ShackDishRoutine(int tries, bool keep)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("shack dish: host, in a world"); yield break; }
			MapType shack = MapTypes.BuiltIn("shack");
			string themeName;
			Theme bear = RandomizerIslands.OddityTheme.TryGetValue("shack", out themeName) ? RandomizerIslands.ThemeOf(themeName) : null;
			if (shack == null || bear == null || !bear.Medium.Concat(bear.Small).Contains("Antenna_dish")) { Fail("shack dish: the shack island, or the dish among its theme's props, is gone"); yield break; }
			if (!RaftProps.Standable("Antenna_dish")) { Fail("shack dish: Antenna_dish isn't standable, so the generator never places it"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			// (the shack type's own content, with the dish forced as the first prop of the touch of its theme - Oddity -> Sprinkle -> DishOnPole)
			var forced = new MapType { Name = shack.Name, Label = shack.Label, Title = shack.Title,
				Content = (mk, ms) => { RandomizerContent.Oddity(mk, ms, "shack", "Antenna_dish"); RandomizerIslands.TreesToCut(mk, ms, true); } };
			var rnd = new System.Random();
			IslandFile found = null;
			IslandGenSettings foundS = null;
			float foundEl = 0f;
			int made = 0;
			for (int i = 0; i < tries && found == null; i++)
			{
				float el;
				IslandGenSettings gs = MapTypes.Roll(shack, rnd, out el);
				IslandFile f = null;
				try { f = MapTypes.Create(forced, gs, el, MapTypes.FileName(shack, gs)); made++; }
				catch (Exception ex) { Log("  shack: generating failed - " + ex.Message); }
				// (the dish is a set piece; the sprinkle can find no level spot in its ring round the shack - then the next shack)
				if (f != null && f.Objects.Any(o => o.Name == "Antenna_dish" && o.Props != null && o.Props.ContainsKey("set.piece"))) { found = f; foundS = gs; foundEl = el; }
				yield return null;
			}
			if (found == null) { Fail("shack dish: none of " + made + " shack island(s) got the forced dish placed"); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? (player != null ? player.transform.position : Vector3.zero);
			string name = MapTypes.FileName(shack, foundS);
			float radius = MapTypes.EstimatedRadius(foundS);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft, radius, 780f);
			if (!spot.HasValue) { Fail("shack dish: no open sea near the raft for " + name); yield break; }
			CustomIslandSpawner.CacheSize(name, radius, foundEl);
			IslandWorldState.Entry e = IslandWorldState.Add(name, spot.Value, null, false);
			e.Loading = true;
			IslandFile ready = found;
			StartTest(CustomIslandSpawner.GenerateAndSpawn(() => ready, name, e));
			for (float w = 0; w < 90f && e.Root == null && !e.Failed; w += 0.1f) yield return new WaitForSeconds(0.1f);
			if (e.Root == null) { Fail("shack dish: " + name + " didn't come"); IslandWorldState.RemoveIds(new[] { e.Id }, true); yield break; }
			yield return WaitIslandSettled(e, 0.5f, 2f);
			Physics.SyncTransforms();
			Terrain land = e.Root.GetComponentInChildren<Terrain>();
			IslandObjectRef[] refs = e.Root.GetComponentsInChildren<IslandObjectRef>(true);
			List<IslandObjectRef> dishes = refs.Where(x => x.ObjectName == "Antenna_dish" && x.Props != null && x.Props.ContainsKey("set.piece")).ToList();
			Func<Vector3, Vector3, float> flat = (a, b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
			var bad = new List<string>();
			var good = new List<string>();
			int count;
			List<string> lines = GroundProblems(e, out count);
			foreach (string l in lines.Where(q => q.Contains("Antenna_")).Take(5)) bad.Add("ground check: " + l);
			if (dishes.Count == 0) bad.Add("the dish didn't come with the island");
			foreach (IslandObjectRef d in dishes)
			{
				Vector3 dp = d.transform.position;
				IslandObjectRef pole = refs.Where(x => x.ObjectName == "Antenna_pole").OrderBy(x => flat(x.transform.position, dp)).FirstOrDefault();
				if (pole == null || flat(pole.transform.position, dp) > 0.5f)
				{ bad.Add("no antenna pole under the dish" + (pole == null ? "" : " (the nearest " + Num(Math.Round(flat(pole.transform.position, dp), 2)) + " m off)")); continue; }
				// (the pole's base: its lowest mesh; its pivot when it has none)
				Renderer[] parts = pole.GetComponentsInChildren<Renderer>(false).Where(x => x is MeshRenderer && x.enabled && x.name != ContentCatalog.MarkerOnly).ToArray();
				float baseY = parts.Length > 0 ? parts.Min(x => x.bounds.min.y) : pole.transform.position.y;
				float off = flat(pole.transform.position, dp), up = dp.y - baseY;
				float gap = land != null ? baseY - (land.SampleHeight(pole.transform.position) + land.transform.position.y) : float.NaN;
				string msg = "dish " + Num(Math.Round(off, 2)) + " m off the pole's axis, " + Num(Math.Round(up, 2)) + " m over its base, base " + (float.IsNaN(gap) ? "no ground" : Num(Math.Round(Mathf.Abs(gap), 2)) + (gap > 0f ? " m over" : " m under") + " the ground");
				if (off > 0.05f || Mathf.Abs(up - 6f) > 0.1f || float.IsNaN(gap) || gap > 0.2f || gap < -0.6f) bad.Add(msg); else good.Add(msg);
			}
			if (!keep) yield return RemoveIslandsAndWait(new[] { e.Id });
			if (bad.Count > 0) Fail("shack dish (" + name + "): " + string.Join("; ", bad.ToArray()));
			else Log("PASS: shack dish - on " + name + ": " + string.Join("; ", good.ToArray()) + "; " + count + " objects measured, " + lines.Count + " other ground problem(s), not the dish's");
		}

		static readonly IslandCheck OverlapCheck = new IslandCheck { Label = "overlap", File = "overlap", Pass = "nothing through anything", Measure = OverlapProblems };

		static readonly IslandCheck OneSidedCheck = new IslandCheck { Label = "one-sided", File = "onesided", Pass = "no wall seen through from behind", Measure = OneSidedProblems };

		[ConsoleCommand(name: "CIOneSidedWorld", docs: "Dev, in a world: no wall-like piece on the loaded custom islands is drawn from one side only with nothing behind its open side (a facade panel seen from inside its room: see-through, IG8). Results in Mods\\DynamicIslands\\onesided_world.txt.")]
		public static void OneSidedWorldCommand(string[] args) { WorldCheck(args, OneSidedCheck); }

		[ConsoleCommand(name: "CIOneSidedIslands", docs: "Dev, world (host, a test world 'CI ...'): saved islands brought next to the raft one by one and measured as CIOneSidedWorld does, then taken away (IG8). Same arguments as CIOverlapIslands. Results in Mods\\DynamicIslands\\onesided_islands.txt.")]
		public static void OneSidedIslandsCommand(string[] args) { IslandsCheck(args, OneSidedCheck); }

		/// <summary>A piece's look across its thinnest side, per mesh + materials: +1 drawn only towards +axis, -1 only towards -axis, 0 both (or too little drawn).</summary>
		static readonly Dictionary<string, int> OneSidedLook = new Dictionary<string, int>();
		static Camera sideCam;
		static RenderTexture sideRt;
		static Texture2D sideTex;
		const int SideLayer = 31, SidePx = 32;

		/// <summary>How the renderer looks drawn alone from +dir and from -dir (an orthographic camera, its own layer, a 32 px
		/// picture): what isn't the background colour counts. Works for any mesh or shader (most meshes can't be read).</summary>
		static int RenderSide(Renderer r, Vector3 centre, Vector3 dir, Vector3 up, float size)
		{
			if (sideCam == null)
			{
				var go = new GameObject("CI one-sided camera");
				UnityEngine.Object.DontDestroyOnLoad(go);
				sideCam = go.AddComponent<Camera>();
				sideCam.enabled = false;
				sideCam.orthographic = true;
				sideCam.cullingMask = 1 << SideLayer;
				sideCam.clearFlags = CameraClearFlags.SolidColor;
				sideCam.backgroundColor = new Color(1f, 0f, 1f, 1f);
				sideCam.nearClipPlane = 0.01f;
				sideCam.farClipPlane = 20f;
				sideRt = new RenderTexture(SidePx, SidePx, 16, RenderTextureFormat.ARGB32);
				sideTex = new Texture2D(SidePx, SidePx, TextureFormat.RGBA32, false);
				sideCam.targetTexture = sideRt;
			}
			int layer = r.gameObject.layer;
			r.gameObject.layer = SideLayer;
			sideCam.orthographicSize = size / 2f * 1.05f;
			var seen = new int[2];
			try
			{
				for (int k = 0; k < 2; k++)
				{
					Vector3 from = k == 0 ? dir : -dir;
					sideCam.transform.position = centre + from * 8f;
					sideCam.transform.rotation = Quaternion.LookRotation(-from, up);
					sideCam.Render();
					RenderTexture old = RenderTexture.active;
					RenderTexture.active = sideRt;
					sideTex.ReadPixels(new Rect(0, 0, SidePx, SidePx), 0, 0);
					sideTex.Apply();
					RenderTexture.active = old;
					foreach (Color32 c in sideTex.GetPixels32())
						if (!(c.r > 240 && c.g < 15 && c.b > 240)) seen[k]++;
				}
			}
			finally { r.gameObject.layer = layer; }
			int most = Math.Max(seen[0], seen[1]), least = Math.Min(seen[0], seen[1]);
			if (most < SidePx * SidePx / 8 || least > most * 0.15f) return 0;
			return seen[0] > seen[1] ? 1 : -1;
		}

		/// <summary>
		/// Walls seen through from behind (IG8): each wall-like mesh (thin across a level direction, at least 1 m each other way)
		/// whose faces all look one way and whose material doesn't draw both sides, with no building piece or other solid within
		/// 0.3 m behind the middle of its open side. One line each.
		/// </summary>
		static List<string> OneSidedProblems(IslandWorldState.Entry e, out int count)
		{
			var lines = new List<string>();
			count = 0;
			Transform objects = e.Root.transform.Find("Objects");
			if (objects == null) return lines;
			Physics.SyncTransforms();
			foreach (Transform o in objects)
			{
				if (o == null || !o.gameObject.activeInHierarchy) continue;
				string n = o.name.Replace("(Clone)", "").Trim();
				if (ContentCatalog.IsCreature(n) || n.StartsWith("Zone_") || n.StartsWith("Note_") || n.StartsWith("Loot_") || n.StartsWith("Pickup_")) continue;
				if (Vegetation.IsMatch(n) || (OverlapIgnore.IsMatch(n) && !Structural.IsMatch(n))) continue;
				count++;
				foreach (Renderer r in o.GetComponentsInChildren<Renderer>(false))
				{
					if (!(r is MeshRenderer) || !r.enabled || r.name == ContentCatalog.MarkerOnly) continue;
					MeshFilter mf = r.GetComponent<MeshFilter>();
					if (mf == null || mf.sharedMesh == null) continue;
					if (r.name == CaveBacks.Name || r.transform.Find(CaveBacks.Name) != null) continue; // (its back drawn already: Selene's and cave pieces)
					Bounds lb = mf.sharedMesh.bounds;
					Vector3[] ax = { r.transform.TransformVector(new Vector3(lb.size.x, 0, 0)), r.transform.TransformVector(new Vector3(0, lb.size.y, 0)), r.transform.TransformVector(new Vector3(0, 0, lb.size.z)) };
					int t = 0;
					for (int i = 1; i < 3; i++) if (ax[i].magnitude < ax[t].magnitude) t = i;
					int a1 = (t + 1) % 3, a2 = (t + 2) % 3;
					if (ax[t].magnitude > 0.4f || ax[a1].magnitude < 1f || ax[a2].magnitude < 1f) continue;
					Vector3 dir = ax[t].magnitude > 1e-5f ? ax[t].normalized : r.transform.TransformDirection(Vector3.forward);
					if (Mathf.Abs(dir.y) > 0.5f) continue;
					string key = (r.transform.lossyScale.x * r.transform.lossyScale.y * r.transform.lossyScale.z < 0 ? "-" : "") + mf.sharedMesh.GetInstanceID() + ":" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "0" : m.GetInstanceID().ToString()).ToArray());
					Vector3 centre = r.transform.TransformPoint(lb.center);
					int side;
					if (!OneSidedLook.TryGetValue(key, out side))
						OneSidedLook[key] = side = RenderSide(r, centre, dir, ax[a1].normalized, Mathf.Max(ax[a1].magnitude, ax[a2].magnitude));
					if (side == 0) continue;
					// (the open side: the one it isn't drawn towards)
					Vector3 open = -dir * side;
					float reach = (ax[t].magnitude / 2f + 0.32f) / 2f;
					Vector3 face = centre + open * reach;
					// (a box from its middle to 32 cm behind the open side - a twin turned round in its place, a lining - over the middle
					// 60% of the piece: in the frame thin / up / along)
					Quaternion rot = Quaternion.LookRotation(ax[a2].normalized, ax[a1].normalized);
					Vector3 half = new Vector3(reach, ax[a1].magnitude * 0.3f, ax[a2].magnitude * 0.3f);
					bool covered = Physics.OverlapBox(face, half, rot, ~0, QueryTriggerInteraction.Ignore).Any(c => c.enabled && !c.transform.IsChildOf(o));
					// (the piece's own colliders cover it too where they reach 3 cm past its open side - a sign board nailed onto its own
					// cabin's logs, Balboa_Shack's 'IF YOU LIVED HERE' - but not a collider on the board's own GameObject, and not the
					// board's own thickness: a lone panel's collider, on a parent or a sibling, stays inside that)
					if (!covered)
					{
						float near = ax[t].magnitude / 2f + 0.03f, far = ax[t].magnitude / 2f + 0.32f;
						Vector3 behind = centre + open * ((near + far) / 2f);
						Vector3 halfBehind = new Vector3((far - near) / 2f, half.y, half.z);
						covered = Physics.OverlapBox(behind, halfBehind, rot, ~0, QueryTriggerInteraction.Ignore).Any(c => c.enabled && c.transform.IsChildOf(o) && c.gameObject != r.gameObject);
					}
					if (covered) continue;
					lines.Add("one-sided: " + n + " at " + Num(Math.Round(face.x, 2)) + " " + Num(Math.Round(face.z, 2)) + " y=" + Num(Math.Round(face.y, 2)) + " - drawn from one side only, nothing behind its open side (facing " + Num(Math.Round(open.x, 2)) + " " + Num(Math.Round(open.z, 2)) + ")");
					break;
				}
			}
			return lines;
		}

		[ConsoleCommand(name: "CIOneSidedSelf", docs: "Dev, world (host, a test world 'CI ...'): the one-sided check (IG8) on an island made for it: a lone Apartment_Ext_BaseMidMidWall facade panel, nothing behind it, is found; a Balboa_Shack (its sign board nailed onto the shack's own logs) isn't (TEST_CATALOGUE GAP21). The island is taken away after.")]
		public static void OneSidedSelfCommand(string[] args) { StartTest(OneSidedSelfRoutine()); }

		static IEnumerator OneSidedSelfRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !CustomIslandSpawner.RaftPosition.HasValue) { Fail("one-sided self: host, in a world"); yield break; }
			const string isl = "cionesided", wall = "Apartment_Ext_BaseMidMidWall", shack = "Balboa_Shack";
			yield return PlaceableCatalog.EnsureLoaded(new[] { wall, shack });
			if (PlaceableCatalog.Get(wall) == null || PlaceableCatalog.Get(shack) == null) { Fail("one-sided self: " + wall + " or " + shack + " isn't in this Raft's catalogue"); yield break; }
			var made = new List<IslandWorldState.Entry>();
			IslandFile f = ScIsland(isl, "One-Sided Self");
			f.Objects.Clear();
			Vector3 b = ScDry(f, Vector2.zero, 1);
			// (16 m apart, on the ground: neither covers the other)
			foreach (var put in new[] { new { Name = wall, Dx = -8f }, new { Name = shack, Dx = 8f } })
			{
				Vector3 p = b + new Vector3(put.Dx, 0f, 0f);
				p.y = ScGround(f, new Vector2(p.x, p.z));
				f.Objects.Add(ScObj(put.Name, p));
			}
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("one-sided self: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.LastOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("one-sided self: the island didn't come"); yield break; }
			int n = 0;
			List<string> lines;
			try
			{
				yield return WaitIslandSettled(e, 0.5f, 2f);
				lines = OneSidedProblems(e, out n);
			}
			finally { ScRemove(made, isl); }
			foreach (string l in lines) Log("  " + l);
			bool ok = true;
			Check(ref ok, n >= 2, "both pieces measured (" + n + " objects)");
			Check(ref ok, lines.Count(l => l.IndexOf(wall, StringComparison.Ordinal) >= 0) == 1, "the lone " + wall + ", nothing behind it, is found");
			Check(ref ok, !lines.Any(l => l.IndexOf(shack, StringComparison.Ordinal) >= 0), "the " + shack + "'s sign board, against the shack's own logs, isn't");
			if (ok) Log("PASS: one-sided self"); else Fail("one-sided self");
		}

		[ConsoleCommand(name: "CIOverlapWorld", docs: "Dev, in a world: nothing on the loaded custom islands passes through anything else - no post, pillar, wall, floor or roof piece more than 6 cm inside a piece of furniture, a prop, a chest, a machine, a tree or a plant (IG7; the user 2026-10-18: posts through a piano and a pool table). Things resting on floors, roofs on their posts, things on tables don't count. Also no themed scene's prop, chest or note more than 6 cm into another of the scenes (their collider boxes as they stand turned; lines 'scene <theme>: ...'; two metal crates, gen-large-37435). Problems per island; all in Mods\\DynamicIslands\\overlap_world.txt. CIOverlapWorld [part of the island name]")]
		public static void OverlapWorldCommand(string[] args) { WorldCheck(args, OverlapCheck); }

		[ConsoleCommand(name: "CIOverlapIslands", docs: "Dev, world (host, a test world 'CI ...'): saved islands brought next to the raft one by one and measured as CIOverlapWorld does, then taken away (IG7). <which>: part of the name, 'plans' (every island of every plan), 'gen' or * (all but the test islands, kept copies and randomizer extras). Results in Mods\\DynamicIslands\\overlap_islands.txt. CIOverlapIslands <which> [most] [skip] [keep]")]
		public static void OverlapIslandsCommand(string[] args) { IslandsCheck(args, OverlapCheck); }

		[ConsoleCommand(name: "CIOverlapTypes", docs: "Dev, world (host, a test world 'CI ...'): brand-new islands of each map type made next to the raft by the generator as it is now, measured as CIOverlapWorld does, then taken away (IG7). Results in Mods\\DynamicIslands\\overlap_types.txt. CIOverlapTypes [types, comma-separated, or *] [islands of each, 2]")]
		public static void OverlapTypesCommand(string[] args) { TypesCheck(args, OverlapCheck); }

		/// <summary>Building pieces: what must not pass through the things below (posts, pillars, walls, floors, decks, roofs).</summary>
		static readonly Regex Structural = new Regex("Pillar|Post(?!er|card|box)|Column|Wall|Roof|Beam|Fence|Pole|Railing|Floor|Foundation|Block_|Deck|Plank|Pier|Stilt|Scaffold|Girder|Truss|Apartment_Ext", RegexOptions.IgnoreCase);
		/// <summary>Not measured either way: the land's own (rocks, ground plants, caves), what goes through things on purpose (pipes,
		/// cables, ladders, doors in their walls), flat or loose things (rugs, puddles), lights and effects.</summary>
		static readonly Regex OverlapIgnore = new Regex("Rock|Boulder|Stone|Cliff|Cave|Tunnel|Sand|Ground|Terrain|Water|Mountain|Hill|Kelp|Coral|Seaweed|Grass|Fern|Flower|Reed|Vine|Moss|Shell|Drift|Pipe|Cable|Rope|Chain|Wire|Ladder|Door|Hatch|Gate|Window|Curtain|Rug|Carpet|Decal|Puddle|Stain|Light|Lamp|Fire|Smoke|Steam|Splash|Bubble|Cobweb|Hammock|Banner|Flag|Tent(?!acle)|Awning|Canopy|Stair|Zipline|Log(?!o)|(^|_)Ice|Dangly|Antenna|Sat+eliteDisc|Dish|Scrap", RegexOptions.IgnoreCase);
		/// <summary>A thing's box this big (any side, metres) is a building or a set piece, not furniture: measured by its colliders
		/// instead, and a mesh collider this big (not a solid box) not at all.</summary>
		const float OverlapBig = 3.5f;
		/// <summary>Trees and big plants: measured by their colliders (the trunk), not by the box round the crown.</summary>
		static readonly Regex Vegetation = new Regex("Tree|Bush|Palm|Plant|Cactus|Bamboo|Monstera|Shrub|Hedge|Pine|Birch|Oak", RegexOptions.IgnoreCase);
		/// <summary>How far one solid must be inside another to count (a chair pushed against a wall, a pot sunk into a floor: not).</summary>
		const float OverlapMargin = 0.06f;

		/// <summary>A solid box of a thing: a renderer's mesh box or a collider's box, in its own frame.</summary>
		class OverlapBox { public Transform T; public Vector3 Min, Max; }

		/// <summary>A thing's boxes from its meshes; null when one is <see cref="OverlapBig"/> or more (a set piece: use its colliders), unless withBig.</summary>
		static List<OverlapBox> MeshBoxes(Transform o, bool withBig = false)
		{
			var boxes = new List<OverlapBox>();
			foreach (Renderer r in o.GetComponentsInChildren<Renderer>(false))
			{
				if (!(r is MeshRenderer) || !r.enabled || r.name == ContentCatalog.MarkerOnly) continue;
				MeshFilter mf = r.GetComponent<MeshFilter>();
				if (mf == null || mf.sharedMesh == null) continue;
				Bounds mb = mf.sharedMesh.bounds;
				if (!withBig && Big(mb.size, r.transform)) return null;
				boxes.Add(new OverlapBox { T = r.transform, Min = mb.min, Max = mb.max });
			}
			return boxes;
		}

		/// <summary>A thing's boxes from its solid colliders (a tree's trunk; a set piece's parts) - a big mesh collider left out, unless withBig.</summary>
		static List<OverlapBox> ColliderBoxes(Transform o, bool withBig = false)
		{
			var boxes = new List<OverlapBox>();
			foreach (Collider c in o.GetComponentsInChildren<Collider>(false))
			{
				if (!c.enabled || c.isTrigger) continue;
				Vector3 lc, ls;
				if (c is BoxCollider) { lc = ((BoxCollider)c).center; ls = ((BoxCollider)c).size; }
				else if (c is CapsuleCollider)
				{
					var cc = (CapsuleCollider)c;
					lc = cc.center;
					ls = Vector3.one * cc.radius * 2f;
					ls[cc.direction] = Mathf.Max(cc.height, cc.radius * 2f);
				}
				else if (c is SphereCollider) { lc = ((SphereCollider)c).center; ls = Vector3.one * ((SphereCollider)c).radius * 2f; }
				else if (c is MeshCollider && ((MeshCollider)c).sharedMesh != null)
				{
					lc = ((MeshCollider)c).sharedMesh.bounds.center; ls = ((MeshCollider)c).sharedMesh.bounds.size;
					if (!withBig && Big(ls, c.transform)) continue;
				}
				else continue;
				boxes.Add(new OverlapBox { T = c.transform, Min = lc - ls / 2f, Max = lc + ls / 2f });
			}
			return boxes;
		}

		static bool Big(Vector3 size, Transform t)
		{
			Vector3 s = t.lossyScale;
			return Mathf.Max(Mathf.Abs(size.x * s.x), Mathf.Abs(size.y * s.y), Mathf.Abs(size.z * s.z)) >= OverlapBig;
		}

		/// <summary>
		/// What passes through what on a spawned island (IG7): the middle of each building piece's material - rays across its
		/// thinnest side against its own colliders, half-way between where they go in and come out (a post's axis, a wall's or a
		/// floor's middle, a roof slab's middle; nothing in a doorway) - and every point of it more than <see cref="OverlapMargin"/>
		/// inside a thing's solid box (furniture, props, chests, machines; a tree's trunk). One line per pair, the deepest point.
		/// </summary>
		/// <summary>Pieces a thing may stand on top of (the overlap check, IG7).</summary>
		static readonly Regex OnAPost = new Regex("Pillar|Post|Pole|Column", RegexOptions.IgnoreCase);

		static List<string> OverlapProblems(IslandWorldState.Entry e, out int count)
		{
			List<string> lines = PieceOverlaps(e, out count);
			Transform objects = e.Root.transform.Find("Objects");
			if (objects != null) lines.AddRange(SceneOverlaps(objects, e.Root.transform));
			return lines;
		}

		static List<string> PieceOverlaps(IslandWorldState.Entry e, out int count)
		{
			var lines = new List<string>();
			count = 0;
			Transform objects = e.Root.transform.Find("Objects");
			if (objects == null) return lines;
			Transform frame = e.Root.transform;
			Physics.SyncTransforms();
			var pieces = new List<Transform>();
			var things = new List<KeyValuePair<Transform, List<OverlapBox>>>();
			var thingBox = new List<Bounds>();
			foreach (Transform o in objects)
			{
				if (o == null || !o.gameObject.activeInHierarchy) continue;
				string n = o.name.Replace("(Clone)", "").Trim();
				if (ContentCatalog.IsCreature(n) || n.StartsWith("Zone_") || n.StartsWith("Note_") || n.StartsWith("Loot_") || n.StartsWith("Pickup_") || OverlapIgnore.IsMatch(n)) continue;
				if (RaftProps.Get(n) != null && RaftProps.Get(n).IsCave) continue;
				count++;
				if (Structural.IsMatch(n)) { pieces.Add(o); continue; }
				List<OverlapBox> boxes = Vegetation.IsMatch(n) ? null : MeshBoxes(o);
				if (boxes == null) boxes = ColliderBoxes(o);
				// (shrunk by the margin, in each box's own scale; too thin to hold a margin: no solid to pass through)
				var solid = new List<OverlapBox>();
				foreach (OverlapBox b in boxes)
				{
					Vector3 s = b.T.lossyScale, m = new Vector3(OverlapMargin / Mathf.Max(1e-4f, Mathf.Abs(s.x)), OverlapMargin / Mathf.Max(1e-4f, Mathf.Abs(s.y)), OverlapMargin / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
					Vector3 lo = b.Min + m, hi = b.Max - m;
					if (lo.x < hi.x && lo.y < hi.y && lo.z < hi.z) solid.Add(new OverlapBox { T = b.T, Min = lo, Max = hi });
				}
				if (solid.Count == 0) continue;
				Bounds w = new Bounds(solid[0].T.TransformPoint((solid[0].Min + solid[0].Max) / 2f), Vector3.zero);
				foreach (OverlapBox b in solid)
					for (int k = 0; k < 8; k++)
						w.Encapsulate(b.T.TransformPoint(new Vector3((k & 1) == 0 ? b.Min.x : b.Max.x, (k & 2) == 0 ? b.Min.y : b.Max.y, (k & 4) == 0 ? b.Min.z : b.Max.z)));
				things.Add(new KeyValuePair<Transform, List<OverlapBox>>(o, solid));
				thingBox.Add(w);
			}
			if (things.Count == 0) return lines;
			foreach (Transform piece in pieces)
			{
				string pn = piece.name.Replace("(Clone)", "").Trim();
				Collider[] own = piece.GetComponentsInChildren<Collider>(false).Where(c => c.enabled && !c.isTrigger).ToArray();
				var deepest = new Dictionary<int, float>();
				var at = new Dictionary<int, Vector3>();
				foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(false))
				{
					if (!(r is MeshRenderer) || !r.enabled || r.name == ContentCatalog.MarkerOnly) continue;
					MeshFilter mf = r.GetComponent<MeshFilter>();
					if (mf == null || mf.sharedMesh == null) continue;
					Bounds lb = mf.sharedMesh.bounds;
					Bounds rb = r.bounds;
					rb.Expand(0.1f);
					List<int> near = Enumerable.Range(0, things.Count).Where(i => thingBox[i].Intersects(rb) && things[i].Key != piece).ToList();
					if (near.Count == 0) continue;
					// (its box's sides in the world; the thinnest one is crossed by the rays)
					Vector3[] ax = { r.transform.TransformVector(new Vector3(lb.size.x, 0, 0)), r.transform.TransformVector(new Vector3(0, lb.size.y, 0)), r.transform.TransformVector(new Vector3(0, 0, lb.size.z)) };
					int t = 0;
					for (int i = 1; i < 3; i++) if (ax[i].magnitude < ax[t].magnitude) t = i;
					int a1 = (t + 1) % 3, a2 = (t + 2) % 3;
					int n1 = Mathf.Clamp(Mathf.CeilToInt(ax[a1].magnitude / 0.25f), 1, 40), n2 = Mathf.Clamp(Mathf.CeilToInt(ax[a2].magnitude / 0.25f), 1, 40);
					Vector3 centre = r.transform.TransformPoint(lb.center), dir = ax[t].magnitude > 1e-4f ? ax[t].normalized : Vector3.up;
					float thick = ax[t].magnitude;
					for (int i = 0; i < n1; i++)
						for (int j = 0; j < n2; j++)
						{
							Vector3 q = centre + ax[a1] * ((i + 0.5f) / n1 - 0.5f) + ax[a2] * ((j + 0.5f) / n2 - 0.5f);
							Vector3 p = q;
							if (own.Length > 0)
							{
								// (in and out of its own colliders across the thin side: the middle of the material, or nothing - a doorway)
								float inD = float.MaxValue, outD = float.MaxValue;
								RaycastHit h;
								Ray fwd = new Ray(q - dir * (thick / 2f + 0.2f), dir), back = new Ray(q + dir * (thick / 2f + 0.2f), -dir);
								foreach (Collider c in own)
								{
									if (c.Raycast(fwd, out h, thick + 0.4f) && h.distance < inD) inD = h.distance;
									if (c.Raycast(back, out h, thick + 0.4f) && h.distance < outD) outD = h.distance;
								}
								if (inD == float.MaxValue || outD == float.MaxValue) continue;
								Vector3 pin = fwd.GetPoint(inD), pout = back.GetPoint(outD);
								if (Vector3.Dot(pout - pin, dir) < 0f) continue;
								p = (pin + pout) / 2f;
							}
							foreach (int k in near)
							{
								if (!thingBox[k].Contains(p)) continue;
								foreach (OverlapBox b in things[k].Value)
								{
									Vector3 l = b.T.InverseTransformPoint(p), s = b.T.lossyScale;
									if (l.x <= b.Min.x || l.x >= b.Max.x || l.y <= b.Min.y || l.y >= b.Max.y || l.z <= b.Min.z || l.z >= b.Max.z) continue;
									float d = OverlapMargin + Mathf.Min(Mathf.Min((l.x - b.Min.x) * Mathf.Abs(s.x), (b.Max.x - l.x) * Mathf.Abs(s.x)),
										Mathf.Min(Mathf.Min((l.y - b.Min.y) * Mathf.Abs(s.y), (b.Max.y - l.y) * Mathf.Abs(s.y)), Mathf.Min((l.z - b.Min.z) * Mathf.Abs(s.z), (b.Max.z - l.z) * Mathf.Abs(s.z))));
									float was;
									if (!deepest.TryGetValue(k, out was) || d > was) { deepest[k] = d; at[k] = p; }
								}
							}
						}
				}
				// (a post's top in a thing that stands on it - a lever on its stand, a bell on its pole: the thing's box, not the thing, round the top)
				bool post = OnAPost.IsMatch(pn);
				float top = float.MinValue;
				if (post) foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(false)) if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
				foreach (KeyValuePair<int, float> hit in deepest.OrderByDescending(x => x.Value))
				{
					if (post && at[hit.Key].y > top - 0.3f && thingBox[hit.Key].center.y > top) continue;
					Transform th = things[hit.Key].Key;
					// (in the island's own frame, as its file and the editor have it)
					Vector3 p = frame.InverseTransformPoint(at[hit.Key]), pp = frame.InverseTransformPoint(piece.position), tp = frame.InverseTransformPoint(th.position);
					lines.Add("through: " + pn + " at " + Num(Math.Round(pp.x, 2)) + " " + Num(Math.Round(pp.z, 2)) + " goes " + Num(Math.Round(hit.Value, 2)) + " m into " +
						th.name.Replace("(Clone)", "").Trim() + " at " + Num(Math.Round(tp.x, 2)) + " " + Num(Math.Round(tp.z, 2)) + " (the point " + Num(Math.Round(p.x, 2)) + " " + Num(Math.Round(p.z, 2)) + " y=" + Num(Math.Round(p.y, 2)) + ")");
				}
			}
			return lines;
		}

		/// <summary>
		/// A themed scene's pieces against each other (the generator's and the randomizer's scenes, marked with
		/// <see cref="RandomizerIslands.SceneProp"/>; chest and note too): each one's solid boxes as they stand - all its colliders,
		/// a big mesh collider's box too (the generator spaces them by their measured boxes), its meshes when it has none - and a
		/// pair more than <see cref="OverlapMargin"/> into each other, the deepest of their boxes (separating axes). Two metal
		/// crates 0.35 m into each other, gen-large-37435 2026-10-21. An island made before scenes were marked: the set pieces that
		/// are the themes' props.
		/// </summary>
		static List<string> SceneOverlaps(Transform objects, Transform frame)
		{
			var lines = new List<string>();
			var all = new List<IslandObjectRef>();
			foreach (Transform o in objects)
			{
				if (o == null || !o.gameObject.activeInHierarchy) continue;
				IslandObjectRef r = o.GetComponent<IslandObjectRef>();
				if (r != null && r.Props != null) all.Add(r);
			}
			bool marked = all.Any(r => r.Props.ContainsKey(RandomizerIslands.SceneProp));
			var themed = new HashSet<string>(RandomizerIslands.Themes.SelectMany(t => t.Anchors.Concat(t.Medium).Concat(t.Small)).Concat(new[] { "Antenna_pole" }));
			List<IslandObjectRef> scene = all.Where(r => marked ? r.Props.ContainsKey(RandomizerIslands.SceneProp)
				: r.Props.ContainsKey("set.piece") && !r.Props.ContainsKey("cave") && themed.Contains(r.ObjectName)).ToList();
			var boxes = new List<List<OverlapBox>>();
			var near = new List<Bounds>();
			foreach (IslandObjectRef r in scene)
			{
				List<OverlapBox> b = ColliderBoxes(r.transform, true);
				if (b.Count == 0) b = MeshBoxes(r.transform, true);
				boxes.Add(b);
				Bounds w = new Bounds(r.transform.position, Vector3.zero);
				foreach (OverlapBox x in b)
					for (int k = 0; k < 8; k++)
						w.Encapsulate(x.T.TransformPoint(new Vector3((k & 1) == 0 ? x.Min.x : x.Max.x, (k & 2) == 0 ? x.Min.y : x.Max.y, (k & 4) == 0 ? x.Min.z : x.Max.z)));
				near.Add(w);
			}
			for (int a = 0; a < scene.Count; a++)
				for (int b = a + 1; b < scene.Count; b++)
				{
					if (boxes[a].Count == 0 || boxes[b].Count == 0 || !near[a].Intersects(near[b])) continue;
					string na = scene[a].ObjectName, nb = scene[b].ObjectName;
					if (na != nb && (na == "Antenna_dish" || na == "Antenna_pole") && (nb == "Antenna_dish" || nb == "Antenna_pole")) continue; // (the dish hangs on its pole)
					float deepest = 0f;
					foreach (OverlapBox x in boxes[a])
						foreach (OverlapBox y in boxes[b]) deepest = Mathf.Max(deepest, BoxDepth(x, y));
					if (deepest <= OverlapMargin) continue;
					Vector3 pa = frame.InverseTransformPoint(scene[a].transform.position), pb = frame.InverseTransformPoint(scene[b].transform.position);
					string what;
					scene[a].Props.TryGetValue(RandomizerIslands.SceneProp, out what);
					lines.Add("scene" + (what != null ? " " + what : "") + ": " + na + " at " + Num(Math.Round(pa.x, 2)) + " " + Num(Math.Round(pa.z, 2)) + " goes " + Num(Math.Round(deepest, 2)) +
						" m into " + nb + " at " + Num(Math.Round(pb.x, 2)) + " " + Num(Math.Round(pb.z, 2)));
				}
			return lines;
		}

		/// <summary>How far two boxes (each in its own frame) are inside each other: the shortest way out of all the separating
		/// axes (their sides and the crossings of their edges); 0 when apart.</summary>
		static float BoxDepth(OverlapBox x, OverlapBox y)
		{
			Vector3 hx = (x.Max - x.Min) / 2f, hy = (y.Max - y.Min) / 2f;
			Vector3[] ux = { x.T.TransformVector(new Vector3(hx.x, 0f, 0f)), x.T.TransformVector(new Vector3(0f, hx.y, 0f)), x.T.TransformVector(new Vector3(0f, 0f, hx.z)) };
			Vector3[] uy = { y.T.TransformVector(new Vector3(hy.x, 0f, 0f)), y.T.TransformVector(new Vector3(0f, hy.y, 0f)), y.T.TransformVector(new Vector3(0f, 0f, hy.z)) };
			Vector3 d = y.T.TransformPoint((y.Min + y.Max) / 2f) - x.T.TransformPoint((x.Min + x.Max) / 2f);
			var axes = new List<Vector3>(ux.Concat(uy));
			foreach (Vector3 p in ux) foreach (Vector3 q in uy) axes.Add(Vector3.Cross(p, q));
			float depth = float.MaxValue;
			foreach (Vector3 axis in axes)
			{
				float len = axis.magnitude;
				if (len < 1e-6f) continue;
				Vector3 n = axis / len;
				float o = -Mathf.Abs(Vector3.Dot(d, n));
				foreach (Vector3 v in ux) o += Mathf.Abs(Vector3.Dot(v, n));
				foreach (Vector3 v in uy) o += Mathf.Abs(Vector3.Dot(v, n));
				if (o <= 0f) return 0f;
				depth = Mathf.Min(depth, o);
			}
			return depth == float.MaxValue ? 0f : depth;
		}

		/// <summary>What stands on a mount narrower than itself: a dish on a mast, an antenna on a roof - one mesh, its box wider
		/// than its foot (Balboa Remade's and Coral Observatory's dishes on their masts read "partly in the air", 2026-10-12; Raft's
		/// Utopia tower top room and its glass on their tower, 152 m over the ground beside it - a false alarm FLOAT2 checked
		/// with a picture, Utopia Remade, 2026-10-20).</summary>
		/// <summary>Rocks and stones placed as objects (not a Placeable_, not a cave or a stalagmite): measured under their whole base.</summary>
		static readonly Regex LooseRock = new Regex("^(?!Placeable_)(?!.*(Cave|Tunnel|Stalag|Wall|Floor|Block_)).*(Rock|Boulder|Stone)", RegexOptions.IgnoreCase);
		static readonly Regex OnAMount =new Regex("Sattelite|Satellite|Dish|Antenna|Tower_EndRoom", RegexOptions.IgnoreCase);

		/// <summary>What on a spawned island doesn't stand on anything: one line per problem; <paramref name="count"/> = the objects measured.</summary>
		static List<string> GroundProblems(IslandWorldState.Entry e, out int count)
		{
			var lines = new List<string>();
			count = 0;
			Transform objects = e.Root.transform.Find("Objects");
			if (objects == null) return lines;
			// (a flying island's objects stand on its land like any other; only the sea under it is far away - it is measured the same)
			Terrain land = e.Root.GetComponentInChildren<Terrain>();
			Vector3 mid = e.Position;
			float sea = 0f; // (Raft's sea level in the world)
			Physics.SyncTransforms();
			// (what holds what: an object is on the ground only when a chain of supports reaches the land, the sea or the
			// island's rock - a pillar is not held by the floor that rests on it, a dish not by the table it floats over. Before
			// 2026-10-19 any collider beside an object counted, so pillars and their floor held each other up in the air)
			var nodes = new Dictionary<Transform, SupportNode>();
			var banners = new List<BannerEnd>();
			foreach (Transform o in objects)
			{
				if (o == null || !o.gameObject.activeInHierarchy) continue;
				string n = o.name.Replace("(Clone)", "").Trim();
				bool structural = ChainLink.IsMatch(n), banner = n.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0;
				if (ContentCatalog.IsCreature(n) || n.StartsWith("Zone_") || n.StartsWith("Note_") || n.StartsWith("Loot_") || n.StartsWith("Pickup_") ||
					n.IndexOf("Ladder", StringComparison.OrdinalIgnoreCase) >= 0 || (Hangs.IsMatch(n) && !structural && !banner)) continue;
				// (cave pieces: their rim, as CICaveRims - AU83)
				if (RaftProps.Get(n) != null && RaftProps.Get(n).IsCave)
				{
					count++;
					string at;
					float gap = DenRimGap(o, out at);
					if (gap >= 0.6f) lines.Add("cave rim off the ground: " + n + " - " + at);
					// (and from inside: nothing seen through its walls or roof - X7)
					Vector3 eye, look;
					if (CaveSeeThrough(o, RaftProps.Get(n), out at, out eye, out look) > 0) lines.Add("cave seen through from inside: " + n + " - " + at);
					continue;
				}
				Renderer[] rs = o.GetComponentsInChildren<Renderer>(false).Where(r => r is MeshRenderer && r.enabled && r.name != ContentCatalog.MarkerOnly).ToArray();
				if (rs.Length == 0) continue;
				count++;
				float bottom = rs.Min(r => r.bounds.min.y);
				Bounds whole = rs[0].bounds;
				foreach (Renderer r in rs) whole.Encapsulate(r.bounds);
				// (its base: the parts that reach down to its bottom - not a windmill's sails above it)
				Renderer[] baseParts = rs.Where(r => r.bounds.min.y < bottom + 0.6f).ToArray();
				Bounds b = baseParts[0].bounds;
				foreach (Renderer r in baseParts) b.Encapsulate(r.bounds);
				string where = Num(b.center.x - mid.x) + " " + Num(b.center.z - mid.z) + " h=" + Num(b.min.y - sea);
				// A banner's rope: each end tied to a post, a pole, a wall or a roof that stands itself (the rope is the banner's
				// top edge; Gannet Cliff's banners hung from nothing, 2026-10-19)
				if (banner)
				{
					BannerEnd[] ends = RopeEnds(o, rs, n, where, objects);
					if (ends != null) banners.AddRange(ends);
					continue;
				}
				var node = new SupportNode { Name = n, Where = where, Box = whole, Link = structural };
				nodes[o] = node;
				Supports(o, b, whole, objects, land, sea, node);
				// Floors, decks, roofs, stairs: measured only as links of the chain (they lie on posts and walls, not on the ground)
				if (structural) continue;
				float groundMid = land != null ? land.SampleHeight(b.center) + land.transform.position.y : float.MinValue;
				// Buried whole: its top under the land (not Raft's rocks and corals, which are set into it; not what is under the sea)
				if (!NotABase.IsMatch(n) && land != null && whole.max.y < groundMid - 0.05f && whole.max.y > sea - 1f)
				{ node.Reported = node.Direct = true; lines.Add("buried: " + n + " at " + where + ", its top " + Num(groundMid - whole.max.y) + " m under the ground"); continue; }
				// A zipline: each end on its own (its base reaches from one end's floor to the other's, over the dip between them -
				// the Lodestone Array's lines read "partly in the air" 3.7 m over their moat, 2026-10-20): every piece no wider than
				// 4 m (not the rope) with nothing at its foot within 0.5 m
				if (n.StartsWith("ZiplinePath"))
				{
					foreach (Renderer r in rs.Where(r => Mathf.Max(r.bounds.size.x, r.bounds.size.z) < 4f))
					{
						Bounds rb = r.bounds;
						if (rs.Any(q => q != r && Mathf.Max(q.bounds.size.x, q.bounds.size.z) < 4f && q.bounds.min.y < rb.min.y - 0.3f && Mathf.Abs(q.bounds.center.x - rb.center.x) < 2f && Mathf.Abs(q.bounds.center.z - rb.center.z) < 2f)) continue;
						float g = GapUnder(o, new Vector3(rb.center.x, rb.min.y, rb.center.z), land);
						if (g > 0.5f && rb.min.y > sea + 0.3f)
						{ node.Reported = true; lines.Add("floating: " + n + " end " + r.name + " at " + Num(rb.center.x - mid.x) + " " + Num(rb.center.z - mid.z) + " h=" + Num(rb.min.y - sea) + ", " + (g > 999f ? "nothing under it" : Num(g) + " m over what is under it")); break; }
					}
					continue;
				}
				// A rock or stone in the air: its underside measured over what is under it at nine points, not "does it touch
				// something" - a row of big rocks touching each other and a wall held each other up over the snow, their shadows
				// apart from them (Coldhold Vault, 2026-10-23)
				if (LooseRock.IsMatch(n) && b.min.y > sea + 0.3f)
				{
					int seen = 0, high = 0; float most = 0f;
					float allow = 0.3f + 0.15f * Mathf.Max(b.extents.x, b.extents.z);
					for (int k = 0; k < 9; k++)
					{
						float px = Mathf.Lerp(b.min.x, b.max.x, 0.25f + 0.25f * (k % 3)), pz = Mathf.Lerp(b.min.z, b.max.z, 0.25f + 0.25f * (k / 3));
						float g = RockGap(o, px, pz, b, land);
						if (float.IsNaN(g)) continue;
						seen++;
						if (g > allow) { high++; most = Mathf.Max(most, g); }
					}
					if (seen > 0 && (high >= 3 || high * 2 > seen))
					{ node.Reported = true; lines.Add("floating rock: " + n + " at " + where + ", its underside up to " + Num(most) + " m over what is under it (" + high + " of " + seen + " points)"); continue; }
				}
				// Floating: nothing at its base (another object, a wall it hangs on, the land) and the first thing under it more than 0.5 m down
				float gapUnder = GapUnder(o, new Vector3(b.center.x, b.min.y, b.center.z), land);
				bool touches = Touches(o, b, objects);
				bool inSea = b.min.y <= sea + 0.3f; // (a dock post, a buoy, a wreck: in the water, held by it)
				if (!touches && !inSea && gapUnder > 0.5f)
				{ node.Reported = true; lines.Add("floating: " + n + " at " + where + ", " + (gapUnder > 999f ? "nothing under it" : Num(gapUnder) + " m over what is under it")); continue; }
				// On a steep wall: a small thing (a chest, a box, a crate) held by nothing but the land, with most of its base over
				// the drop - its foot set into the slope reads "on the ground" (the Listening Mirrors' altar on its chasm wall); a
				// tall, thin leg or post (three times taller than wide) is driven into the slope, as the tower legs on Varuna Point
				if (!NotABase.IsMatch(n) && land != null && Mathf.Max(b.size.x, b.size.z) < 1.5f && b.size.y < Mathf.Max(b.size.x, b.size.z) * 3f && node.Under.Count == 0 && node.Over.Count == 0 && b.min.y > sea + 0.3f)
				{
					int over = 0;
					float most = 0f;
					for (int c = 0; c < 4; c++)
					{
						var p = new Vector3((c & 1) == 0 ? Mathf.Lerp(b.min.x, b.max.x, 0.1f) : Mathf.Lerp(b.min.x, b.max.x, 0.9f), b.min.y, (c & 2) == 0 ? Mathf.Lerp(b.min.z, b.max.z, 0.1f) : Mathf.Lerp(b.min.z, b.max.z, 0.9f));
						float g = GapUnder(o, p, land);
						if (g > 0.6f) { over++; most = Mathf.Max(most, g); }
					}
					if (over >= 2)
					{ node.Reported = true; lines.Add("on a steep wall: " + n + " at " + where + ", " + over + " of its 4 corners over the drop (up to " + (most > 999f ? "nothing under it" : Num(most) + " m") + ")"); continue; }
				}
				// Partly in the air: on the ground with part of its base, the rest more than 0.6 m over it (a house on a slope)
				if (NotABase.IsMatch(n) || OnAMount.IsMatch(n) || land == null || Mathf.Max(b.size.x, b.size.z) < 1.5f || b.size.y < 0.6f || b.min.y < sea - 0.5f) continue;
				float lowest = float.MaxValue, highest = float.MinValue;
				Vector3 worst = Vector3.zero;
				int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(b.size.x, b.size.z)), 3, 20);
				// (its base in its own frame: a turned machine's world box reaches past its tracks - VP_Excavator read the ground
				// beside them, 2026-10-12)
				Vector3 lmin = Vector3.one * float.MaxValue, lmax = Vector3.one * float.MinValue;
				foreach (Renderer r in baseParts)
				{
					MeshFilter mf = r.GetComponent<MeshFilter>();
					if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name.StartsWith("Combined Mesh")) { lmin = Vector3.one; lmax = Vector3.zero; break; }
					Bounds mb = mf.sharedMesh.bounds;
					for (int c = 0; c < 8; c++)
					{
						Vector3 l = o.InverseTransformPoint(r.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f))));
						lmin = Vector3.Min(lmin, l); lmax = Vector3.Max(lmax, l);
					}
				}
				bool own = lmin.x <= lmax.x && lmin.z <= lmax.z;
				for (int i = 0; i <= steps; i++)
					for (int j = 0; j <= steps; j++)
					{
						var p = new Vector3(Mathf.Lerp(b.min.x + b.size.x * 0.2f, b.max.x - b.size.x * 0.2f, i / (float)steps), b.min.y, Mathf.Lerp(b.min.z + b.size.z * 0.2f, b.max.z - b.size.z * 0.2f, j / (float)steps));
						if (own)
						{
							Vector3 sx = lmax - lmin, w = o.TransformPoint(new Vector3(Mathf.Lerp(lmin.x + sx.x * 0.2f, lmax.x - sx.x * 0.2f, i / (float)steps), lmin.y, Mathf.Lerp(lmin.z + sx.z * 0.2f, lmax.z - sx.z * 0.2f, j / (float)steps)));
							p = new Vector3(w.x, b.min.y, w.z);
						}
						float g = GapUnder(o, p, land);
						// (a railing straddling a deck's edge, as Raft's own towers: held by the face over the deck)
						if (b.size.x < 0.3f) g = Mathf.Min(GapUnder(o, new Vector3(b.min.x, p.y, p.z), land), GapUnder(o, new Vector3(b.max.x, p.y, p.z), land));
						if (b.size.z < 0.3f) g = Mathf.Min(GapUnder(o, new Vector3(p.x, p.y, b.min.z), land), GapUnder(o, new Vector3(p.x, p.y, b.max.z), land));
						if (g < lowest) lowest = g;
						if (g > highest) { highest = g; worst = p; }
					}
				if (lowest <= 0.4f && highest > 0.6f)
					lines.Add("partly in the air: " + n + " at " + where + ", its base up to " + (highest > 999f ? "over nothing" : Num(highest) + " m over the ground") + " (at " + Num(worst.x - mid.x) + " " + Num(worst.z - mid.z) + ")");
			}
			// The chain of supports: grounded = on the land, the sea or the island's rock, or on something grounded (what isn't
			// measured - a ladder, a lamp, a cave piece - counts as grounded)
			for (bool changed = true; changed;)
			{
				changed = false;
				foreach (SupportNode s in nodes.Values)
					if (!s.Grounded && (s.Direct || s.Under.Any(u => { SupportNode t; return !nodes.TryGetValue(u, out t) || t.Grounded; }) ||
						s.Over.Any(u => { SupportNode t; return !nodes.TryGetValue(u, out t) || t.Grounded; }) ||
						// (a floor or roof piece joined edge to edge to one that is held, no higher than it: a hip roof's sides on its
						// corners - Vaporhold Reservoir, 2026-10-20)
						(s.Link && nodes.Values.Any(t => t != s && t.Link && t.Grounded && t.Box.min.y <= s.Box.min.y + 0.3f && Grow(t.Box, 0.1f).Intersects(s.Box)))))
					{ s.Grounded = true; changed = true; }
			}
			foreach (SupportNode s in nodes.Values.Where(x => !x.Grounded && !x.Reported))
				lines.Add("held up by nothing that reaches the ground: " + s.Name + " at " + s.Where +
					(s.Under.Count > 0 ? " (rests on " + string.Join(", ", s.Under.Select(u => u.name.Replace("(Clone)", "").Trim()).Distinct().Take(3).ToArray()) + ", not grounded either)" : ""));
			foreach (BannerEnd be in banners)
			{
				bool held = be.ByLand || be.By.Any(u => { SupportNode t; return !nodes.TryGetValue(u, out t) || t.Grounded; });
				if (!held) lines.Add("banner rope end held by nothing: " + be.Name + " at " + be.Where + ", its " + be.Side + " end (" + (be.By.Count == 0 ? "no post or wall there" : "on " + string.Join(", ", be.By.Select(u => u.name.Replace("(Clone)", "").Trim()).Distinct().Take(3).ToArray()) + ", not grounded") + ")");
			}
			// What hangs: zipline and wire ends, hanging props, solid things that show nothing (IG9)
			lines.AddRange(HangProblems(objects, land, mid, sea));
			lines.AddRange(FlyingLeftOut(e.Name));
			return lines;
		}

		/// <summary>Floors, decks, roofs, stairs: links of the support chain, reported when the chain doesn't reach the ground.</summary>
		static readonly Regex ChainLink = new Regex("Floor|Deck|Plank|Bridge|Ramp|Stair|Roof|Scaffold", RegexOptions.IgnoreCase);

		static Bounds Grow(Bounds b, float by) { b.Expand(by * 2f); return b; }
		static readonly Regex FixedAbove = new Regex("Floor|Deck|Roof|Beam|Wall|Ceiling", RegexOptions.IgnoreCase);
		class SupportNode { public string Name, Where; public bool Direct, Grounded, Reported, Link; public Bounds Box; public HashSet<Transform> Under = new HashSet<Transform>(), Over = new HashSet<Transform>(); }
		class BannerEnd { public string Name, Where, Side; public bool ByLand; public HashSet<Transform> By = new HashSet<Transform>(); }

		/// <summary>The placed object (a child of <paramref name="objects"/>) a collider belongs to; null = not a placed object
		/// (the land, the island's rock, the sea floor).</summary>
		static Transform PlacedRoot(Transform t, Transform objects)
		{
			for (; t != null; t = t.parent) if (t.parent == objects) return t;
			return null;
		}

		/// <summary>What holds <paramref name="o"/> up: on the land, the sea or the island's own rock (<see cref="SupportNode.Direct"/>),
		/// and the placed objects at its base or at its sides that reach down at least as low as its base (a table under a box, a
		/// wall a lamp hangs on - not a floor resting on a pillar's top).</summary>
		static void Supports(Transform o, Bounds b, Bounds whole, Transform objects, Terrain land, float sea, SupportNode node)
		{
			// (fixed under a floor, roof, beam or wall above it - a rib under a deck, a ceiling lamp: held if that is held - Colossus of
			// the Strait, Coral Observatory, 2026-10-20)
			// (from 0.15 m under its top to 0.5 m over it: a lamp's mount may stop short of the ceiling's collider)
			var top = new Vector3(Mathf.Max(0.05f, whole.extents.x * 0.8f), 0.325f, Mathf.Max(0.05f, whole.extents.z * 0.8f));
			foreach (Collider c in Physics.OverlapBox(new Vector3(whole.center.x, whole.max.y + 0.175f, whole.center.z), top, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
			{
				if (c.transform.IsChildOf(o) || c.bounds.max.y < whole.max.y - 0.1f || c.bounds.min.y < whole.min.y) continue; // (its top in a thin ceiling)
				Transform r = PlacedRoot(c.transform, objects);
				if (r != null && FixedAbove.IsMatch(r.name)) node.Over.Add(r);
			}
			node.Direct = b.min.y <= sea + 0.3f;
			float reach = b.min.y + 0.3f;
			var half = new Vector3(Mathf.Max(0.05f, b.extents.x * 0.9f), 0.15f, Mathf.Max(0.05f, b.extents.z * 0.9f));
			foreach (Collider c in Physics.OverlapBox(new Vector3(b.center.x, b.min.y, b.center.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
			{
				if (c.transform.IsChildOf(o)) continue;
				Transform r = PlacedRoot(c.transform, objects);
				if (r == null) node.Direct = true;
				else if (c.bounds.min.y < reach) node.Under.Add(r);
			}
			foreach (Collider c in Physics.OverlapBox(b.center, b.extents + Vector3.one * 0.08f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
			{
				if (c.transform.IsChildOf(o) || c.GetComponent<Terrain>() != null || c.bounds.min.y >= reach) continue;
				Transform r = PlacedRoot(c.transform, objects);
				if (r == null) node.Direct = true; else node.Under.Add(r);
			}
			// (right under its middle and its corners: the land within 0.5 m, or the first thing there)
			for (int i = 0; i < 5; i++)
			{
				var p = i == 4 ? new Vector3(b.center.x, b.min.y, b.center.z) :
					new Vector3((i & 1) == 0 ? Mathf.Lerp(b.min.x, b.max.x, 0.15f) : Mathf.Lerp(b.min.x, b.max.x, 0.85f), b.min.y, (i & 2) == 0 ? Mathf.Lerp(b.min.z, b.max.z, 0.15f) : Mathf.Lerp(b.min.z, b.max.z, 0.85f));
				if (land != null)
				{
					float g = p.y - (land.SampleHeight(p) + land.transform.position.y);
					Vector3 lp = land.transform.position, ls = land.terrainData.size;
					if (g <= 0.5f && (g > -0.3f || (p.x >= lp.x && p.x <= lp.x + ls.x && p.z >= lp.z && p.z <= lp.z + ls.z))) { node.Direct = true; continue; }
				}
				RaycastHit best = default(RaycastHit);
				float bestGap = float.MaxValue;
				foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 0.3f, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
					if (!h.collider.transform.IsChildOf(o) && p.y - h.point.y < bestGap) { bestGap = p.y - h.point.y; best = h; }
				if (bestGap > 0.5f) continue;
				Transform r = PlacedRoot(best.collider.transform, objects);
				if (r == null) node.Direct = true; else node.Under.Add(r);
			}
		}

		/// <summary>A banner's two rope ends (the ends of its top edge, along its long side) and what each is tied to: a collider
		/// within 0.35 m beside it and 0.6 m above or below. Null when its mesh can't be read.</summary>
		static BannerEnd[] RopeEnds(Transform o, Renderer[] rs, string n, string where, Transform objects)
		{
			Vector3 lmin = Vector3.one * float.MaxValue, lmax = Vector3.one * float.MinValue;
			foreach (Renderer r in rs)
			{
				MeshFilter mf = r.GetComponent<MeshFilter>();
				if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name.StartsWith("Combined Mesh")) return null;
				Bounds mb = mf.sharedMesh.bounds;
				for (int c = 0; c < 8; c++)
				{
					Vector3 l = o.InverseTransformPoint(r.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f))));
					lmin = Vector3.Min(lmin, l); lmax = Vector3.Max(lmax, l);
				}
			}
			bool alongX = lmax.x - lmin.x >= lmax.z - lmin.z;
			var ends = new BannerEnd[2];
			for (int k = 0; k < 2; k++)
			{
				Vector3 l = alongX ? new Vector3(k == 0 ? lmin.x + 0.1f : lmax.x - 0.1f, lmax.y, (lmin.z + lmax.z) * 0.5f)
					: new Vector3((lmin.x + lmax.x) * 0.5f, lmax.y, k == 0 ? lmin.z + 0.1f : lmax.z - 0.1f);
				var e = new BannerEnd { Name = n, Where = where, Side = k == 0 ? "left" : "right" };
				foreach (Collider c in Physics.OverlapBox(o.TransformPoint(l), new Vector3(0.35f, 0.6f, 0.35f), o.rotation, ~0, QueryTriggerInteraction.Ignore))
				{
					if (c.transform.IsChildOf(o)) continue;
					Transform r = PlacedRoot(c.transform, objects);
					if (r == null) e.ByLand = true; else e.By.Add(r);
				}
				ends[k] = e;
			}
			return ends;
		}

		/// <summary>How far the first thing under <paramref name="foot"/> that isn't <paramref name="self"/> is (the land, another object,
		/// the sea floor); 9999 = nothing.</summary>
		[ConsoleCommand(name: "CICaveInside", docs: "Dev, editor or world: every cave piece (the editor's island, or the loaded custom islands) looked round from inside at eye height - nothing seen through: no face seen from its back (Raft draws the shells, the ground and the hill over them from one side only), no sky but out through an open end (X7). CICaveInside [part of the name] [shots: a picture of each piece's worst look, shot_caveinside_<n>.png]")]
		public static void CaveInsideCommand(string[] args)
		{
			string only = args != null && args.Length > 0 && args[0] != "*" ? args[0] : "";
			bool shots = args != null && args.Length > 1 && args[1] == "shots";
			StartTest(CaveInsideRoutine(only, shots));
		}

		static IEnumerator CaveInsideRoutine(string only, bool shots)
		{
			var caves = new List<KeyValuePair<string, Transform>>(); // (island: piece)
			if (DynamicIslands.InEditor())
			{
				int dug, cut = CaveGround.EditorNow(terraineditor.terrain, out dug); // (the ground out of the rooms now, not in a second)
				Log("  the ground taken out of the caves' rooms: " + dug + " points let down, " + cut + " cells cut");
				foreach (EditorGameObject e in PlacedEditorObjects())
					if (e != null && RaftProps.Get(e.GameObjectName ?? "") != null && RaftProps.Get(e.GameObjectName).IsCave) caves.Add(new KeyValuePair<string, Transform>("editor", e.transform));
			}
			else
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(x => x.Root != null && !x.Loading).ToList())
					foreach (Transform t in e.Root.GetComponentsInChildren<Transform>().Where(t => RaftProps.Get(t.name.Replace("(Clone)", "").Trim()) != null && RaftProps.Get(t.name.Replace("(Clone)", "").Trim()).IsCave))
						caves.Add(new KeyValuePair<string, Transform>(e.Name, t));
			caves = caves.Where(c => only.Length == 0 || c.Key.IndexOf(only, StringComparison.OrdinalIgnoreCase) >= 0 || c.Value.name.IndexOf(only, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			int n = 0, bad = 0;
			foreach (var c in caves)
			{
				if (c.Value == null) continue;
				n++;
				string name = c.Value.name.Replace("(Clone)", "").Trim(), detail;
				Vector3 eye, look;
				int seen = CaveSeeThrough(c.Value, RaftProps.Get(name), out detail, out eye, out look);
				Log("  " + n + " '" + c.Key + "' " + name + " at " + c.Value.position.ToString("F0") + ": " + detail);
				if (seen > 0) bad++;
				if (shots && eye != Vector3.zero) yield return CameraShot(eye, eye + look * 5f, "caveinside_" + n);
				yield return null;
			}
			if (n == 0) Fail("cave inside: no cave pieces" + (only.Length > 0 ? " named *" + only + "*" : ""));
			else if (bad > 0) Fail("cave inside: " + bad + " of " + n + " cave pieces seen through from inside (above)");
			else Log("PASS: cave inside - " + n + " cave pieces, nothing seen through from inside");
		}

		[ConsoleCommand(name: "CIShotFrom", docs: "Dev, editor or world: a picture through Raft's camera from a point, and what the looks round from it meet - for checking a see-through CICaveInside found (X7). CIShotFrom <x> <y> <z> <yaw> <pitch> [name]: shot_from_<name>.png; logs the first things hit (front or back, metres) every 45 degrees round at that pitch, and straight up and down")]
		public static void ShotFromCommand(string[] args)
		{
			var v = new float[5];
			for (int i = 0; i < 5; i++)
				if (args == null || args.Length <= i || !float.TryParse(args[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i])) { Fail("shot from: CIShotFrom <x> <y> <z> <yaw> <pitch> [name]"); return; }
			StartTest(ShotFromRoutine(new Vector3(v[0], v[1], v[2]), v[3], v[4], args.Length > 5 ? args[5] : "1"));
		}

		[ConsoleCommand(name: "CISeleneBacks", docs: "Dev, editor: Selene's room pieces (wall, entrance, ceiling, floor, door frame, large door, tunnel) placed get their backs - an outer wall or a ceiling seen from outside no longer shows the room through it - and a radio tower wall (drawn from both sides already) gets none (X7)")]
		public static void SeleneBacksCommand()
		{
			StartTest(SeleneBacksRoutine());
		}

		static IEnumerator SeleneBacksRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			Transform placed = GameObject.Find("PlacedObjects").transform;
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 2f, 500f);
			string[] selene = { "TP_Selene_Int_Room_Wall", "TP_Selene_Int_Room_Entrance", "TP_Selene_Int_Room_Ceiling", "TP_Selene_Int_Room_Floor_Plain", "TP_Selene_Int_DoorFrame", "TP_Selene_Int_DoorLargeL", "TP_Selene_Int_Tunnel_Straight" };
			yield return PlaceableCatalog.EnsureLoaded(selene.Concat(new[] { "RT_Wall1" })); // (Selene's pieces come from its scene, loaded when first wanted)
			var made = new List<EditorGameObject>();
			for (int i = 0; i < selene.Length; i++)
			{
				EditorGameObject ego = PlaceForTest(selene[i], c0 + new Vector3(i * 8f, 0f, 0f), placed);
				if (ego == null) { Check(ref ok, false, selene[i] + ": could not be placed"); continue; }
				made.Add(ego);
				yield return null;
				int backs = ego.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.name == CaveBacks.Name && r.enabled);
				Check(ref ok, CaveBacks.Has(ego.transform) && backs > 0, selene[i] + ": drawn from both sides (" + backs + " backs)");
			}
			EditorGameObject rt = PlaceForTest("RT_Wall1", c0 + new Vector3(0f, 0f, 10f), placed);
			if (rt != null)
			{
				made.Add(rt);
				Check(ref ok, !rt.GetComponentsInChildren<Transform>(true).Any(t => t.name == CaveBacks.Name), "RT_Wall1: no backs added (Raft draws it from both sides)");
			}
			foreach (EditorGameObject ego in made) UnityEngine.Object.Destroy(ego.gameObject);
			if (ok) Log("PASS: Selene pieces drawn from both sides"); else Fail("Selene pieces drawn from both sides");
		}

		[ConsoleCommand(name: "CIRoomLooks", docs: "Dev, in game: a room of a custom island seen from inside (X7) - from eye height over the floor at <x> <z> (m from its middle, the island's own axes) and four places 1.5 m round it, 81 looks each: those that meet nothing (the sky) or the back of a face first (seen through). Pictures to four corners and toward the highest of those looks (shot_room_<name>_<n>.png). CIRoomLooks <island> <x> <z> <floor h> [name]")]
		public static void RoomLooksCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			var v = new float[3];
			for (int i = 0; i < 3; i++)
				if (args.Length <= i + 1 || !float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i])) { Fail("room looks: CIRoomLooks <island> <x> <z> <floor h> [name]"); return; }
			StartTest(RoomLooksRoutine(e, v[0], v[1], v[2], args.Length > 4 ? args[4] : args[0]));
		}

		static IEnumerator RoomLooksRoutine(IslandWorldState.Entry e, float x, float z, float h, string name)
		{
			float turnY = e.Root.transform.eulerAngles.y;
			Quaternion turn = Quaternion.Euler(0f, turnY, 0f);
			Vector3 mid = e.Position + turn * new Vector3(x, 0f, z);
			mid.y = h;
			// (what can be seen: not animals or players)
			Func<Collider, bool> counts = c => c != null && !c.isTrigger && c.GetComponentInParent<AI_NetworkBehaviour>() == null && c.GetComponentInParent<Network_Player>() == null;
			var looks = new List<Vector3>();
			foreach (float pitch in new[] { -20f, 0f, 15f, 35f, 60f })
				for (int i = 0; i < 16; i++) looks.Add(Quaternion.Euler(-pitch, turnY + i * 22.5f, 0f) * Vector3.forward);
			looks.Add(Vector3.up);
			var eyes = new List<Vector3>();
			var what = new Dictionary<string, int>();
			int sky = 0, back = 0;
			Vector3 worstEye = Vector3.zero, worstLook = Vector3.zero;
			float worstUp = -2f;
			bool backs0 = Physics.queriesHitBackfaces;
			Physics.SyncTransforms();
			try
			{
				foreach (Vector3 off in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
				{
					Vector3 p = mid + turn * off * 1.5f;
					// (the floor: the nearest face up under the given height + 1 m)
					Physics.queriesHitBackfaces = false;
					RaycastHit? floor = null;
					foreach (RaycastHit fh in Physics.RaycastAll(p + Vector3.up, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
						if (counts(fh.collider) && fh.normal.y > 0.6f && (floor == null || fh.distance < floor.Value.distance)) floor = fh;
					if (floor == null) { Log("  no floor at " + p.ToString("F1")); continue; }
					Vector3 eye = floor.Value.point + Vector3.up * 1.6f;
					eyes.Add(eye);
					foreach (Vector3 d in looks)
					{
						Physics.queriesHitBackfaces = true;
						List<RaycastHit> all = Physics.RaycastAll(eye, d, 150f, ~0, QueryTriggerInteraction.Ignore).Where(hh => counts(hh.collider)).OrderBy(hh => hh.distance).ToList();
						Physics.queriesHitBackfaces = false;
						RaycastHit[] fronts = Physics.RaycastAll(eye, d, 150f, ~0, QueryTriggerInteraction.Ignore);
						string w = null;
						if (all.Count == 0) { w = "sky"; sky++; }
						else if (all[0].distance > 0.3f && !fronts.Any(f => f.collider == all[0].collider && Mathf.Abs(f.distance - all[0].distance) < 0.02f) && !CaveBacks.DrawnBothSides(all[0].collider))
						{
							w = (all[0].collider is TerrainCollider ? "the ground from below" : all[0].collider.name + "'s back");
							back++;
						}
						if (w == null) continue;
						float yaw = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - turnY, 360f);
						string k = w + " at " + yaw.ToString("F0") + "° round, " + (Mathf.Asin(d.y) * Mathf.Rad2Deg).ToString("F0") + "° up";
						what[k] = what.ContainsKey(k) ? what[k] + 1 : 1;
						if (d.y > worstUp) { worstUp = d.y; worstEye = eye; worstLook = d; }
					}
				}
			}
			finally { Physics.queriesHitBackfaces = backs0; }
			if (eyes.Count == 0) { Fail("room looks: " + name + " - no floor near " + mid.ToString("F1")); yield break; }
			Log("ROOM " + name + ": " + eyes.Count + " places, " + (eyes.Count * looks.Count) + " looks: " + sky + " to the sky, " + back + " through a back (island turned " + turnY.ToString("F0") + "°, eye " + eyes[0].ToString("F1") + ")");
			foreach (var kv in what.OrderByDescending(q => q.Key.StartsWith("sky") ? 0 : 1).ThenBy(q => q.Key).Take(40)) Log("  " + kv.Key + " (" + kv.Value + "x)");
			for (int i = 0; i < 4; i++)
				yield return CameraShot(eyes[0], eyes[0] + Quaternion.Euler(-25f, turnY + 45f + i * 90f, 0f) * Vector3.forward * 5f, "room_" + name + "_" + (i + 1));
			if (worstUp > -2f) yield return CameraShot(worstEye, worstEye + worstLook * 5f, "room_" + name + "_worst");
			Log("PASS: room looks " + name);
		}

		static IEnumerator ShotFromRoutine(Vector3 at, float yaw, float pitch, string name)
		{
			bool backs0 = Physics.queriesHitBackfaces;
			Physics.SyncTransforms();
			try
			{
				var dirs = new List<KeyValuePair<string, Vector3>>();
				for (int i = 0; i < 8; i++) dirs.Add(new KeyValuePair<string, Vector3>((yaw + i * 45f).ToString("F0") + "°", Quaternion.Euler(-pitch, yaw + i * 45f, 0f) * Vector3.forward));
				dirs.Add(new KeyValuePair<string, Vector3>("up", Vector3.up));
				dirs.Add(new KeyValuePair<string, Vector3>("down", Vector3.down));
				foreach (var d in dirs)
				{
					Physics.queriesHitBackfaces = true;
					List<RaycastHit> all = Physics.RaycastAll(at, d.Value, 80f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Take(4).ToList();
					Physics.queriesHitBackfaces = false;
					RaycastHit[] fronts = Physics.RaycastAll(at, d.Value, 80f, ~0, QueryTriggerInteraction.Ignore);
					Log("  " + d.Key + ": " + (all.Count == 0 ? "nothing" : string.Join(", ", all.Select(h => h.collider.name + (h.collider.isTrigger ? " (trigger)" : "") +
						(fronts.Any(f => f.collider == h.collider && Mathf.Abs(f.distance - h.distance) < 0.02f) ? "" : " (back)") + " " + h.distance.ToString("F1")).ToArray())));
				}
			}
			finally { Physics.queriesHitBackfaces = backs0; }
			yield return CameraShot(at, at + Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward * 5f, "from_" + name);
			Log("PASS: shot from " + at.ToString("F1"));
		}

		/// <summary>A collider of a cave piece's rock (any piece's).</summary>
		static bool CaveRock(Collider c)
		{
			for (Transform t = c != null ? c.transform : null; t != null; t = t.parent)
			{
				PropInfo q = RaftProps.Get(t.name.Replace("(Clone)", "").Trim());
				if (q != null && q.IsCave) return true;
			}
			return false;
		}

		/// <summary>
		/// A cave piece seen from where a player stands in it (X7): from eye height over every floor under its roof, every look
		/// round meets the shell's inside, the ground from above or something else from its front - Raft draws the ground and
		/// most things from one side only: a look goes on through the back of a face, and that is a window only when it then
		/// leaves the room (not through an open end) or meets the shell's own back where it isn't drawn - and no sky but out
		/// through an open end. The looks that see through (0: none); detail: how many, and the worst place.
		/// </summary>
		static int CaveSeeThrough(Transform cave, PropInfo p, out string detail, out Vector3 eye, out Vector3 look)
		{
			eye = look = Vector3.zero;
			List<Collider> own = cave.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToList();
			if (p == null || !p.IsCave || own.Count == 0) { detail = "not measured: no colliders"; return 0; }
			Bounds b = own[0].bounds;
			foreach (Collider c in own) b.Encapsulate(c.bounds);
			Vector3 along = cave.rotation * (p.Axis == 0 ? Vector3.right : Vector3.forward);
			along.y = 0f; along.Normalize();
			// (what can be seen: not animals, players, or the editor's handles of zones)
			Func<Collider, bool> counts = c => c != null && !c.isTrigger && c.GetComponentInParent<AI_NetworkBehaviour>() == null && c.GetComponentInParent<Network_Player>() == null &&
				(c.GetComponentInParent<EditorGameObject>() == null || !(c.GetComponentInParent<EditorGameObject>().GameObjectName ?? "").StartsWith("Zone_"));
			Func<Vector3, Vector3, float, bool, RaycastHit?> first = (from, dir, len, backs) =>
			{
				Physics.queriesHitBackfaces = backs;
				RaycastHit? best = null;
				foreach (RaycastHit h in Physics.RaycastAll(from, dir, len, ~0, QueryTriggerInteraction.Ignore))
					if (counts(h.collider) && (best == null || h.distance < best.Value.distance)) best = h;
				return best;
			};
			Func<Vector3, Vector3, bool, List<RaycastHit>> hits = (from, dir, backs) =>
			{
				Physics.queriesHitBackfaces = backs;
				return Physics.RaycastAll(from, dir, 80f, ~0, QueryTriggerInteraction.Ignore).Where(h => counts(h.collider)).OrderBy(h => h.distance).ToList();
			};
			// (drawn from both sides: a cave piece's shell once it has its outside, and the mountain over the caves - X7)
			Func<Collider, bool> bothSides = c =>
			{
				return c.name == CaveMountain.SkinName || CaveBacks.DrawnBothSides(c);
			};
			var eyes = new List<Vector3>();
			var looks = new List<Vector3>();
			foreach (float pitch in new[] { -20f, 0f, 15f, 35f, 60f })
				for (int i = 0; i < 16; i++) looks.Add(Quaternion.Euler(-pitch, i * 22.5f, 0f) * Vector3.forward);
			looks.Add(Vector3.up);
			int bad = 0, worstBad = 0;
			string worst = "";
			var what = new Dictionary<string, int>();
			bool backs0 = Physics.queriesHitBackfaces;
			Physics.SyncTransforms();
			try
			{
				for (float x = b.min.x + 0.5f; x < b.max.x; x += 1.5f)
					for (float z = b.min.z + 0.5f; z < b.max.z; z += 1.5f)
					{
						Physics.queriesHitBackfaces = false;
						RaycastHit[] downs = Physics.RaycastAll(new Vector3(x, b.max.y + 2f, z), Vector3.down, b.size.y + 4f, ~0, QueryTriggerInteraction.Ignore);
						foreach (RaycastHit h in downs)
						{
							if (!counts(h.collider) || h.normal.y < 0.6f) continue; // (somewhere to stand)
							// (room to stand: nothing within 1.7 m over the floor)
							if (first(h.point + Vector3.up * 0.1f, Vector3.up, 1.6f, true) != null) continue;
							Vector3 e = h.point + Vector3.up * 1.6f;
							// (in the cave: its roof over it, seen from below - on the shell's own floor or on the ground; not in a gap
							// under the shell, where its floor's back is over it, nor on a rock of the piece's own that stands out of the
							// shell, under its outside - Balboa's dead end has one, under the ground and in the sea)
							RaycastHit? up = first(e, Vector3.up, 30f, true), upFront = first(e, Vector3.up, 30f, false);
							if (up == null || !own.Contains(up.Value.collider)) continue;
							if (upFront == null || upFront.Value.distance > up.Value.distance + 0.05f) continue;
							// (and walled in by it: most looks round meet its inside first, not its back - not on top of its rock,
							// under an overhang of its outside)
							int walls = 0;
							for (int i = 0; i < 16; i++)
							{
								Vector3 wd = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
								RaycastHit? w = null;
								Physics.queriesHitBackfaces = true;
								foreach (RaycastHit x2 in Physics.RaycastAll(e, wd, 30f, ~0, QueryTriggerInteraction.Ignore))
									if (own.Contains(x2.collider) && (w == null || x2.distance < w.Value.distance)) w = x2;
								RaycastHit? wf = first(e, wd, 30f, false);
								if (w != null && wf != null && wf.Value.collider == w.Value.collider && Mathf.Abs(wf.Value.distance - w.Value.distance) < 0.02f) walls++;
							}
							if (walls < 8) continue;
							eyes.Add(e);
						}
					}
				if (eyes.Count > 150) eyes = eyes.Where((e, i) => i % (eyes.Count / 150 + 1) == 0).ToList();
				foreach (Vector3 e in eyes)
				{
					int here = 0;
					Vector3 hereLook = Vector3.zero;
					string hereWhat = "";
					foreach (Vector3 d in looks)
					{
						// (what is seen that way: the nearest face from its front, or drawn from both sides - a face from its back
						// isn't drawn at all: the look goes on through it)
						List<RaycastHit> all = hits(e, d, true), fronts = hits(e, d, false);
						RaycastHit? seen = null, through = null;
						foreach (RaycastHit h in all)
						{
							if (fronts.Any(f => f.collider == h.collider && Mathf.Abs(f.distance - h.distance) < 0.02f) || bothSides(h.collider)) { seen = h; break; }
							if (through == null) through = h;
						}
						float o = Vector3.Dot(new Vector3(d.x, 0f, d.z), along);
						bool openEnd = (p.OpenPlus && o > 0.25f) || (p.OpenMinus && o < -0.25f);
						// (nothing at all that way: out of a doorway - one beside the place too, where a passage bends to its open
						// end; not one behind it, towards a closed end)
						bool doorway = (p.OpenPlus && o > -0.25f) || (p.OpenMinus && o < 0.25f);
						bool sky = seen == null;
						if (through == null || through.Value.distance < 0.3f)
						{
							if (!sky || openEnd || doorway) continue; // (a face from its front; out through an open end)
						}
						else if (!own.Contains(through.Value.collider))
						{
							// (through the ground from below, or another one-sided face, to the piece's own rock - or something in
							// front of it, in the room: the ground's underside hides nothing that should be seen. Only when the look
							// then goes out of the room, not through an open end, does the room have a window there)
							if (!sky && all.Any(h => own.Contains(h.collider) && h.distance >= seen.Value.distance - 0.05f)) continue;
							// (an air bell's skin or another thing's one-sided face, a joined passage's rock behind it: nothing of
							// the outside either)
							if (!sky && !(through.Value.collider is TerrainCollider) && CaveRock(seen.Value.collider)) continue;
							if (openEnd) continue;
						}
						Collider wc = through != null ? through.Value.collider : null;
						string w = wc == null ? "sky" : (own.Contains(wc) ? "the shell's back" : (wc is TerrainCollider ? "the ground from below" : wc.name + "'s back") + (sky ? " to the sky" : " to " + seen.Value.collider.name));
						what[w] = what.ContainsKey(w) ? what[w] + 1 : 1;
						bad++;
						if (here++ == 0) { hereLook = d; hereWhat = w + (wc == null ? "" : " " + through.Value.distance.ToString("F1") + " m off"); }
					}
					if (here > worstBad)
					{
						worstBad = here;
						eye = e;
						look = hereLook;
						Vector3 l = cave.InverseTransformPoint(e);
						worst = here + " of " + looks.Count + " looks at " + e.ToString("F1") + " (in the piece " + l.ToString("F1") + "), e.g. " + hereWhat + " looking " +
							(Mathf.Atan2(hereLook.x, hereLook.z) * Mathf.Rad2Deg).ToString("F0") + "° round, " + (Mathf.Asin(hereLook.y) * Mathf.Rad2Deg).ToString("F0") + "° up";
					}
				}
			}
			finally { Physics.queriesHitBackfaces = backs0; }
			if (eyes.Count == 0) { detail = "no place to stand under its roof found"; return 0; }
			detail = eyes.Count + " places to stand, " + (eyes.Count * looks.Count) + " looks: " + bad + " see through" +
				(bad > 0 ? " (" + string.Join(", ", what.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value).ToArray()) + "); worst " + worst : "");
			return bad;
		}

		/// <summary>A rock's underside over what is under it at x z: NaN where the rock isn't over that point or is set into
		/// the ground there; else the gap in metres (the land or the first other thing under it).</summary>
		static float RockGap(Transform self, float x, float z, Bounds b, Terrain land)
		{
			float under = float.MinValue;
			if (land != null) under = land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y;
			foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, b.max.y + 0.1f, z), Vector3.down, b.size.y + 400f, ~0, QueryTriggerInteraction.Ignore))
				if (!h.collider.transform.IsChildOf(self) && h.point.y < b.max.y && h.point.y > under) under = h.point.y;
			if (under == float.MinValue || under >= b.max.y) return float.NaN;
			float underside = float.MaxValue;
			foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, under + 0.01f, z), Vector3.up, b.max.y - under + 0.1f, ~0, QueryTriggerInteraction.Ignore))
				if (h.collider.transform.IsChildOf(self) && h.point.y < underside) underside = h.point.y;
			if (underside == float.MaxValue) return float.NaN; // (not over this point, or into the ground here: the ray starts inside it)
			return Mathf.Max(0f, underside - under);
		}

		static float GapUnder(Transform self, Vector3 foot, Terrain land)
		{
			float best = float.MaxValue;
			foreach (RaycastHit h in Physics.RaycastAll(foot + Vector3.up * 0.3f, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
				if (!h.collider.transform.IsChildOf(self) && foot.y - h.point.y < best) best = Mathf.Max(0f, foot.y - h.point.y);
			if (land != null)
			{
				float g = foot.y - (land.SampleHeight(foot) + land.transform.position.y);
				if (g > -0.3f && g < best) best = Mathf.Max(0f, g);
				// (its foot under the land's surface: set into the ground - a rock sunk into a beach. The ray starts under the
				// land then and finds nothing, which read as "nothing under it")
				Vector3 lp = land.transform.position, ls = land.terrainData.size;
				if (g <= -0.3f && foot.x >= lp.x && foot.x <= lp.x + ls.x && foot.z >= lp.z && foot.z <= lp.z + ls.z) best = 0f;
			}
			return best == float.MaxValue ? 9999f : best;
		}

		/// <summary>Whether <paramref name="self"/>'s base touches a collider of something else (stands on it, or hangs on a wall).</summary>
		static bool Touches(Transform self, Bounds b, Transform objects = null)
		{
			// (only what shows holds: a crane that never showed but left its colliders held Altitude Market's ballast stone, 2026-10-22)
			if (objects != null) return TouchesShown(self, b, objects);
			var half = new Vector3(Mathf.Max(0.05f, b.extents.x * 0.9f), 0.15f, Mathf.Max(0.05f, b.extents.z * 0.9f));
			if (Physics.OverlapBox(new Vector3(b.center.x, b.min.y, b.center.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => !c.transform.IsChildOf(self))) return true;
			// (hung on a wall or a post: its sides touch something)
			return Physics.OverlapBox(b.center, b.extents + Vector3.one * 0.08f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(c => !c.transform.IsChildOf(self) && c.GetComponent<Terrain>() == null);
		}
	}
}
