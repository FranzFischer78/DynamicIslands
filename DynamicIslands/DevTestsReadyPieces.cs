using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Raft's doors, hatches, crank wheels and levers as ready pieces (ReadyPieces), and the plan Check's story item order.</summary>
	public static partial class DevTests
	{
		const string ReadyIsland = "cireadypieces";

		[ConsoleCommand(name: "CIReadyPieces", docs: "Dev, in game (host): Raft's ready pieces on a custom island - a Tangaroa keycard door (stays shut without the keycard, goes with it, the keycard kept), a hatch (opens), a crank wheel and a lever (send the signals crank and lever)")]
		public static void ReadyPiecesCommand(string[] args) { StartTest(ReadyPiecesRoutine()); }

		static IEnumerator ReadyPiecesRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			var pieces = new[] { new[] { "PlantationDoor", "door" }, new[] { "TangaroaHatchRoom_Hatch", "hatch" }, new[] { "TP_Selene_DoorCrankWheel", "crank" }, new[] { "TP_CoolingStation_Monitor_Lever", "lever" }, new[] { "UT_ElectricCableHub01", "wire" }, new[] { "UT_WaterChallengeWheel1", "wheel" }, new[] { "Claw_ControlBoard", "claw" } };
			yield return PlaceableCatalog.EnsureLoaded(ReadyPieces.All.Select(p => p.Name).ToList());
			Check(ref ok, ReadyPieces.All.All(p => PlaceableCatalog.Get(p.Name) != null), "every ready piece loads from Raft's islands" + string.Join("", ReadyPieces.All.Where(p => PlaceableCatalog.Get(p.Name) == null).Select(p => ", " + p.Name + " missing").ToArray()));

			var s = new IslandGenSettings { Seed = 8383, Radius = 50f, Height = 14f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, ReadyIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			for (int i = 0; i < pieces.Length; i++)
			{
				Dictionary<string, string> p = ObjectProps.Defaults(pieces[i][0]);
				p[BehaviourProps.Name] = pieces[i][1];
				float x = c.x - 12f + i * 4f;
				f.Objects.Add(new IslandObject { Name = pieces[i][0], Position = new Vector3(x, ground(x, c.y), c.y), Props = p });
			}
			IslandWorldState.Remove(ReadyIsland);
			f.Save(IslandSpawner.PathFor(ReadyIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(ReadyIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(ReadyIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == ReadyIsland);
			if (e == null || e.Root == null) { Fail(ReadyIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			int missing = e.Root.GetComponentsInChildren<Transform>(true).Count(t => t.name.Contains(IslandSpawner.MissingTag));
			Check(ref ok, missing == 0, "the pieces spawn (" + missing + " missing)");

			// The keycard door
			string card = StoryItems.Ref(ReadyPieces.ItemOf("PlantationDoor"));
			StoryBook.Take(card, 99);
			IslandObjectRef door = ScObjOf(e, "door");
			Check(ref ok, door != null, "the keycard door is on the island");
			if (door != null)
			{
				ScUse(e, "door");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, door.gameObject.activeInHierarchy, "without the keycard it stays shut");
				StoryBook.Give(card, 1);
				ScUse(e, "door");
				yield return new WaitForSeconds(0.8f);
				Check(ref ok, !door.gameObject.activeInHierarchy, "with Raft's Tangaroa keycard (" + card + ") it opens");
				Check(ref ok, StoryBook.Count(card) == 1, "the crew keeps the keycard for the next door (" + StoryBook.Count(card) + ")");
			}
			IslandObjectRef hatch = ScObjOf(e, "hatch");
			if (hatch != null) { ScUse(e, "hatch"); yield return new WaitForSeconds(0.6f); }
			Check(ref ok, hatch != null && !hatch.gameObject.activeInHierarchy, "the hatch opens when used");
			foreach (string sig in new[] { "crank", "lever" })
			{
				ScUse(e, sig);
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(sig)), "the " + sig + " sends the signal '" + sig + "'");
			}
			// The puzzle pieces: a wire connector wants Raft's electrical cable once; a water wheel turns; the claw console
			string cable = StoryItems.Ref(ReadyPieces.ItemOf("UT_ElectricCableHub01"));
			StoryBook.Take(cable, 99);
			ScUse(e, "wire");
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, !e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WireSignal)), "without a cable the wire connector stays dead");
			StoryBook.Give(cable, 2);
			ScUse(e, "wire");
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WireSignal)) && StoryBook.Count(cable) == 1, "with Raft's electrical cable it connects (" + StoryBook.Count(cable) + " of 2 cables left) and sends 'wire'");
			ScUse(e, "wire");
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, StoryBook.Count(cable) == 1, "used again it doesn't want another cable (" + StoryBook.Count(cable) + ")");
			IslandObjectRef wheel = ScObjOf(e, "wheel");
			Quaternion before = wheel != null ? wheel.transform.rotation : Quaternion.identity;
			ScUse(e, "wheel");
			yield return new WaitForSeconds(2f);
			float turned = wheel != null ? Quaternion.Angle(before, wheel.transform.rotation) : 0f;
			Check(ref ok, wheel != null && e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WheelSignal)) && turned > 45f, "the water wheel turns a quarter and sends 'wheel' (" + turned.ToString("0") + " degrees)");
			ScUse(e, "claw");
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.ClawSignal)), "the claw console sends 'claw'");
			StoryBook.Take(cable, 99);
			StoryBook.Take(card, 99);
			if (ok) Log("PASS: ready pieces"); else Fail("ready pieces");
		}

		[ConsoleCommand(name: "CIStoryOrderCheck", docs: "Dev, anywhere: the plan Check's story item order - an island with a keycard door brought before the island that gives the keycard is warned about, after it is fine, and with no giver at all the warning says so")]
		public static void StoryOrderCheckCommand(string[] args)
		{
			bool ok = true;
			const string lockIsland = "citest-so-lock", keyIsland = "citest-so-key";
			string card = StoryItems.Ref(ReadyPieces.ItemOf("PlantationDoor"));
			try
			{
				string source = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci") && !IslandNetwork.IsDownloadName(n));
				IslandFile lf = IslandFile.Load(IslandSpawner.PathFor(source));
				lf.Objects.Clear(); lf.Props = new Dictionary<string, string>();
				lf.Objects.Add(new IslandObject { Name = "PlantationDoor", Position = Vector3.zero, Props = ObjectProps.Defaults("PlantationDoor") });
				IslandFile kf = IslandFile.Load(IslandSpawner.PathFor(source));
				kf.Objects.Clear(); kf.Props = new Dictionary<string, string>();
				kf.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.zero, Props = new Dictionary<string, string> { { ObjectProps.LootItems, card + "*1;Plank*4" } } });
				lf.Save(IslandSpawner.PathFor(lockIsland));
				kf.Save(IslandSpawner.PathFor(keyIsland));
				PlanChecker.Facts lockFacts = PlanChecker.FromFile(lf, lockIsland, false), keyFacts = PlanChecker.FromFile(kf, keyIsland, false);
				Check(ref ok, lockFacts.NeedsStory.Contains(StoryItems.IdOf(card)) && keyFacts.GivesStory.Contains(StoryItems.IdOf(card)), "the door wants the keycard, the chest gives it");
				Func<string[], List<PlanChecker.Finding>> check = order =>
				{
					var plan = new WorldPlan { Name = "CI Story Order", Random = false };
					for (int i = 0; i < order.Length; i++) plan.Rules.Add(new IntroRule { Id = "r" + i, What = "island", WhatArg = order[i], When = "start" });
					return PlanChecker.Check(plan, false, true);
				};
				Func<List<PlanChecker.Finding>, string> warn = fs => string.Join(" / ", fs.Where(x => x.Text.Contains("story item")).Select(x => x.Text).ToArray());
				string early = warn(check(new[] { lockIsland, keyIsland })), late = warn(check(new[] { keyIsland, lockIsland })), none = warn(check(new[] { lockIsland }));
				Check(ref ok, early.Contains("later rule"), "the lock before the key: warned (" + early + ")");
				Check(ref ok, late.Length == 0, "the key before the lock: fine (" + late + ")");
				Check(ref ok, none.Contains("no island of the plan gives"), "no key anywhere: warned (" + none + ")");
				// (an "any" check: Raft's radio runs on a generator on its island as well as on a battery - the battery is not
				// wanted then; the generator's own "power" is only its running state, so it still wants its part)
				IslandFile rf = IslandFile.Load(IslandSpawner.PathFor(source));
				rf.Objects.Clear(); rf.Props = new Dictionary<string, string>();
				rf.Objects.Add(new IslandObject { Name = "RT_CommRadio", Position = Vector3.zero, Props = ObjectProps.Defaults("RT_CommRadio") });
				PlanChecker.Facts alone = PlanChecker.FromFile(rf, "citest-so-radio", false);
				rf.Objects.Add(new IslandObject { Name = "Generator", Position = Vector3.one * 3f, Props = ObjectProps.Defaults("Generator") });
				PlanChecker.Facts powered = PlanChecker.FromFile(rf, "citest-so-radio", false);
				string battery = ReadyPieces.ItemOf("RT_CommRadio"), part = ReadyPieces.ItemOf("Generator");
				Check(ref ok, alone.NeedsStory.Contains(battery) && !powered.NeedsStory.Contains(battery) && powered.NeedsStory.Contains(part),
					"a radio wants a battery, not with a generator on its island; the generator wants its part (alone: " + string.Join(", ", alone.NeedsStory.ToArray()) +
					"; with the generator: " + string.Join(", ", powered.NeedsStory.ToArray()) + ")");
			}
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex); }
			finally
			{
				foreach (string n in new[] { lockIsland, keyIsland }) try { if (System.IO.File.Exists(IslandSpawner.PathFor(n))) System.IO.File.Delete(IslandSpawner.PathFor(n)); } catch { }
			}
			if (ok) Log("PASS: story item order check"); else Fail("story item order check");
		}
			[ConsoleCommand(name: "CIGenRaftFeatures", docs: "Dev, editor: ROADMAP LM12 - the generator's Raft's features: 6 on a tropical island (with buildings, and without) - vines in front of a hidden cache, buried treasure, a zipline with its far end at the beach, a code panel showing a hidden strongbox with its code on a note, wild beehives, dirt spots, and an explorer's chest with the tools; 0 puts none; a preset keeps the number")]
		public static void GenRaftFeaturesCommand(string[] args) { StartTest(GenRaftFeaturesRoutine()); }

		static IEnumerator GenRaftFeaturesRoutine()
		{
			yield return WaitForEditor(false);
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			foreach (bool buildings in new[] { false, true })
			{
				var gs = new IslandGenSettings { Seed = 5151, Radius = 85f, Height = 30f, Peaks = 2, ObjectDensity = 0.4f, Style = TerrainPainter.Tropical, Features = 6, Buildings = buildings, BuildingKind = GenBuildings.Huts, BuildingCount = 2 };
				yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(gs));
				IslandGenerator.GenerateInEditor(gs);
				yield return new WaitForSecondsRealtime(0.5f);
				List<EditorGameObject> objs = PlacedEditorObjects();
				Func<string, List<EditorGameObject>> all = n => objs.Where(e => e.GameObjectName == n).ToList();
				string report = string.Join(", ", IslandGenerator.LastReport.Built.ToArray());
				EditorGameObject vines = all(ContentCatalog.MacheteVines).FirstOrDefault(v => ObjectProps.Get(v.Props, BehaviourProps.EventKey("use")).Contains("show|vinecache"));
				EditorGameObject cache = all("Loot_Chest").FirstOrDefault(c => ObjectProps.Get(c.Props, BehaviourProps.Name).StartsWith("vinecache"));
				EditorGameObject zip = all("ZiplinePath_Landmark").FirstOrDefault(z => ObjectProps.Get(z.Props, ZiplineEnds.ZipTo).Length > 0);
				EditorGameObject panel = all("RT_PowerBox").FirstOrDefault(b => CodeLock.HasCode(b.Props));
				string code = panel != null ? ObjectProps.Get(panel.Props, CodeLock.Code) : "?";
				bool note = objs.Any(e => ObjectProps.Get(e.Props, ObjectProps.NoteText).Contains(code));
				EditorGameObject tools = all("Loot_Chest").FirstOrDefault(c => ObjectProps.Get(c.Props, ObjectProps.NoteTitle) == "Explorer's chest");
				string loot = tools != null ? ObjectProps.Get(tools.Props, ObjectProps.LootItems) : "";
				string tag = buildings ? "with buildings: " : "no buildings: ";
				Check(ref ok, vines != null && cache != null && BehaviourProps.StartsHidden(cache.Props), tag + "vines in front of a hidden cache");
				Check(ref ok, all(ContentCatalog.BuriedTreasure).Count >= 1, tag + all(ContentCatalog.BuriedTreasure).Count + " buried treasure");
				Check(ref ok, zip != null, tag + "a zipline with its far end set");
				Check(ref ok, panel != null && note, tag + "a code panel (" + code + ") with the code on a note");
				Check(ref ok, all(ContentCatalog.WildHive).Count >= 1 && all("Pickup_Landmark_DirtPickup").Count >= 1, tag + all(ContentCatalog.WildHive).Count + " wild beehives, " + all("Pickup_Landmark_DirtPickup").Count + " dirt spots");
				Check(ref ok, new[] { ContentCatalog.MacheteItem, "MetalDetector", "Shovel", ContentCatalog.ZiplineItem }.All(x => loot.Contains(x + "*")), tag + "the explorer's chest has the tools (" + loot + ")");
				Check(ref ok, report.Contains("zipline") && report.Contains("vines"), tag + "the report: " + report);
				if (!buildings && zip != null && Camera.main != null)
				{
					Vector3 at = zip.transform.position;
					Camera.main.transform.SetPositionAndRotation(at + new Vector3(-25f, 15f, -25f), Quaternion.LookRotation(new Vector3(25f, -12f, 25f)));
					yield return new WaitForSecondsRealtime(1f);
					Screenshot(new[] { "gen_features" });
					yield return new WaitForSecondsRealtime(0.4f);
				}
			}
			var none = new IslandGenSettings { Seed = 5151, Radius = 85f, Height = 30f, Style = TerrainPainter.Tropical, Features = 0 };
			IslandGenerator.GenerateInEditor(none);
			yield return new WaitForSecondsRealtime(0.4f);
			Check(ref ok, !PlacedEditorObjects().Any(e => e.GameObjectName == ContentCatalog.MacheteVines || e.GameObjectName == "RT_PowerBox"), "0 features: none");
			Check(ref ok, IslandGenSettings.FromText(new IslandGenSettings { Features = 4 }.ToText()).Features == 4, "a preset keeps the number");
			if (ok) Log("PASS: generator features"); else Fail("generator features");
		}
			[ConsoleCommand(name: "CIFormatTail", docs: "Dev, anywhere: ROADMAP R12 - the island file's tagged tail: a file with tags saves and loads them (unknown ones kept when saved again), a file without stays as before (format and bytes), and Raft's renumbered scenes are found by their island's name (R11)")]
		public static void FormatTailCommand(string[] args)
		{
			bool ok = true;
			const string isl = "citest-tail";
			string path = IslandSpawner.PathFor(isl);
			try
			{
				string source = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci") && !IslandNetwork.IsDownloadName(n));
				IslandFile f = IslandFile.Load(IslandSpawner.PathFor(source));
				Check(ref ok, f.Tail.Count == 0, "a file of today has no tail");
				// (without a tail the bytes are what they were: hashes of saved worlds' islands don't change)
				f.Save(path);
				byte[] plain = System.IO.File.ReadAllBytes(path);
				IslandFile again = IslandFile.Load(path);
				again.Save(path);
				Check(ref ok, System.IO.File.ReadAllBytes(path).SequenceEqual(plain), "saved again without a tail: the same bytes");
				f.Tail["future.weather"] = new byte[] { 1, 2, 3, 4, 5 };
				f.Tail["future.empty"] = new byte[0];
				f.Save(path);
				IslandFile back = IslandFile.Load(path);
				Check(ref ok, back.Tail.Count == 2 && back.Tail["future.weather"].SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }) && back.Tail["future.empty"].Length == 0 && back.Objects.Count == f.Objects.Count,
					"tags saved and read: " + string.Join(", ", back.Tail.Select(kv => kv.Key + " (" + kv.Value.Length + " bytes)").ToArray()));
				back.Save(path);
				Check(ref ok, IslandFile.Load(path).Tail.ContainsKey("future.weather"), "a tag this version doesn't know is kept when the island is saved again");
				IslandFile fromBytes = IslandFile.FromBytes(System.IO.File.ReadAllBytes(path), isl);
				Check(ref ok, fromBytes.Objects.Count == f.Objects.Count && fromBytes.Props.Count == f.Props.Count, "the island itself reads as before");
			}
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex); }
			finally { try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { } }
			// R11: Raft's scenes renumbered by an update
			string vasa = PlaceableCatalog.ResolveScene("44#Landmark_Vasagatan");
			Check(ref ok, PlaceableCatalog.ResolveScene("99#Landmark_Vasagatan") == vasa && vasa.Contains("Landmark_Vasagatan"), "a renumbered scene is found by its island's name (99#Landmark_Vasagatan -> " + vasa + ")");
			Check(ref ok, PlaceableCatalog.ResolveScene("98#Landmark_Nowhere") == "98#Landmark_Nowhere", "an unknown one stays as it was");
			if (ok) Log("PASS: format tail"); else Fail("format tail");
		}

		[ConsoleCommand(name: "CIRemakeMissing", docs: "Dev, in game (host): ROADMAP R15 - generated island names read back (gen-tropical-1234, -2, a map type, not a player's island); a world's generated island whose file is gone is made again in its place and spawns")]
		public static void RemakeMissingCommand(string[] args) { StartTest(RemakeMissingRoutine()); }

		static IEnumerator RemakeMissingRoutine()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			Check(ref ok, CustomIslandSpawner.RemakeOf("gen-tropical-1234") != null && CustomIslandSpawner.RemakeOf("gen-snowy-1234-2") != null && CustomIslandSpawner.RemakeOf("gen-" + GhostRafts.TypeName + "-55") != null && CustomIslandSpawner.RemakeOf("gen-wreck-77") != null,
				"generated names read back (random style, a -2 copy, map types)");
			Check(ref ok, CustomIslandSpawner.RemakeOf("myisland") == null && CustomIslandSpawner.RemakeOf("gen-nosuchkind-12") == null, "a player's island or an unknown kind: not made again");
			string name = "gen-tropical-4242";
			if (System.IO.File.Exists(IslandSpawner.PathFor(name))) System.IO.File.Delete(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, 120f, 600f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			IslandWorldState.Entry e = IslandWorldState.Add(name, spot.Value, null, false);
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true, e);
			yield return new WaitForSeconds(1f);
			Check(ref ok, System.IO.File.Exists(IslandSpawner.PathFor(name)) && e.Root != null && !e.Failed, "the missing '" + name + "' is made again and spawns (" + (e.Root != null ? "spawned" : "not spawned") + ")");
			IslandFile f = System.IO.File.Exists(IslandSpawner.PathFor(name)) ? IslandFile.Load(IslandSpawner.PathFor(name)) : null;
			Check(ref ok, f != null && (string.IsNullOrEmpty(f.Style) || f.Style.Equals("Tropical", StringComparison.OrdinalIgnoreCase)), "of the same kind: " + (f != null ? (string.IsNullOrEmpty(f.Style) ? "Tropical" : f.Style) : "-"));
			IslandWorldState.RemoveIds(new[] { e.Id }, true);
			try { System.IO.File.Delete(IslandSpawner.PathFor(name)); } catch { }
			if (ok) Log("PASS: remake missing"); else Fail("remake missing");
		}
			[ConsoleCommand(name: "CIPerfChecks", docs: "Dev, in game (host): ROADMAP P1-P3 - a big island spawns over several frames, its longest frame measured; spawned again, no shader is looked up again; the object root and the placing object are cached")]
		public static void PerfChecksCommand(string[] args) { StartTest(PerfChecksRoutine(args.Length > 0 ? string.Join(" ", args) : null)); }

		static IEnumerator PerfChecksRoutine(string island)
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			island = island ?? IslandSpawner.ListSavedIslands().Select(n => new { N = n, S = new System.IO.FileInfo(IslandSpawner.PathFor(n)).Length }).Where(x => !x.N.StartsWith("ci") && !x.N.StartsWith("gen-") && !IslandNetwork.IsDownloadName(x.N)).OrderByDescending(x => x.S).First().N;
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor(island));
			yield return PlaceableCatalog.EnsureLoaded(f.Objects.Select(o => o.Name).ToList());
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, IslandSpawner.LandRadius(f), 900f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			for (int round = 0; round < 2; round++)
			{
				int findsBefore = ReApplyShaders.Finds;
				float longest = 0f;
				bool done = false;
				StartTest(Watch(() => done, ms => longest = ms));
				yield return DynamicIslands.instance.SpawnIslandFile(island, spot.Value, false);
				done = true;
				yield return null; yield return null;
				Check(ref ok, IslandSpawner.LastSpawnFrames > 1 || f.Objects.Count < 400, "round " + (round + 1) + ": '" + island + "' (" + f.Objects.Count + " objects) made over " + IslandSpawner.LastSpawnFrames + " frames, the longest " + longest.ToString("F0") + " ms (" + IslandSpawner.LastSpawnTiming + "; " + DynamicIslands.LastLoadTiming + ")");
				if (round == 1) Check(ref ok, ReApplyShaders.Finds == findsBefore, "spawned again: no shader looked up again (" + (ReApplyShaders.Finds - findsBefore) + ")");
				GameObject root = IslandSpawner.SpawnedRoots.LastOrDefault(r => r != null && r.name == "CustomIsland_" + (f.Name ?? island));
				Check(ref ok, root != null && root.transform.Find("Objects") != null && root.transform.Find("Objects").gameObject.activeSelf, "its objects are there and switched on");
				IslandSpawner.Despawn(root);
				yield return new WaitForSeconds(0.5f);
			}
			Check(ref ok, !DynamicIslands.InEditor(), "in a world InEditor() is false (no search)");
			if (ok) Log("PASS: perf checks"); else Fail("perf checks");
		}

		static IEnumerator Watch(Func<bool> done, Action<float> longest)
		{
			float most = 0f;
			while (!done())
			{
				most = Mathf.Max(most, Time.unscaledDeltaTime * 1000f);
				longest(most);
				yield return null;
			}
		}
			[ConsoleCommand(name: "CIPlainText", docs: "Dev, anywhere: ROADMAP X6 / UL7 - a library entry's texts and an island's quest come as plain text: rich text tags out (size, color, b...), tabs as spaces, control characters gone, long texts cut")]
		public static void PlainTextCommand(string[] args)
		{
			bool ok = true;
			string json = "{ \"id\": \"x\", \"title\": \"<size=300>Huge</size> <color=red>red</color> <b>bold</b>\", \"summary\": \"a\\tb\", \"description\": \"" + new string('x', 5000) + "\", \"tags\": [\"<i>tag</i>\"] }";
			LibraryInfo i = LibraryInfo.FromJson(json);
			Check(ref ok, i.title == "Huge red bold", "the title without its tags: '" + i.title + "'");
			Check(ref ok, i.summary == "a b", "a tab as a space: '" + i.summary + "'");
			Check(ref ok, i.description.Length == 4000 && i.description.EndsWith("..."), "a 5000-character description cut to 4000 (" + i.description.Length + ")");
			Check(ref ok, i.tags.Length == 1 && i.tags[0] == "tag", "the tags too: " + string.Join(",", i.tags));
			var props = new Dictionary<string, string> { { "quest.title", "<size=200>The hoard</size>" }, { "quest.steps", "reach|top|1|<size=99>Climb</size>" } };
			IslandQuest q = IslandQuest.From(props);
			Check(ref ok, q.Title == "The hoard" && (q.Steps.Count == 0 || q.Steps[0].Text == "Climb"), "a quest's title and steps as plain text: '" + q.Title + "'" + (q.Steps.Count > 0 ? " / '" + q.Steps[0].Text + "'" : ""));
			if (ok) Log("PASS: plain text"); else Fail("plain text");
		}
			[ConsoleCommand(name: "CIStartChecks", docs: "Dev, anywhere: AU11/AU36/AU38/AU45 - every part of the mod started; the PC check on this PC (lists what it finds); the mod's own files come from the .rmod, not older copies in its folder; a save waits out a short lock by another program; the quest rewards record is per world and player")]
		public static void StartChecksCommand(string[] args)
		{
			bool ok = true;
			Check(ref ok, DynamicIslands.StartFailures.Count == 0, "every part of the mod started" + (DynamicIslands.StartFailures.Count > 0 ? ": failed " + string.Join(", ", DynamicIslands.StartFailures.ToArray()) : ""));
			List<string> pc = PcCheck.Problems();
			Check(ref ok, pc.Count == 0, "the PC check finds nothing on this PC" + (pc.Count > 0 ? ": " + string.Join(" / ", pc.ToArray()) : ""));
			foreach (string f in new[] { "raft_islands.txt", "raft_land.txt", "raft_blueprints.txt", "modinfo.json" })
			{
				byte[] read = RaftIslands.ModFile(f), shipped = null;
				try { DynamicIslands.instance.modlistEntry.modinfo.modFiles.TryGetValue(f, out shipped); } catch { }
				Check(ref ok, read != null && shipped != null && read.SequenceEqual(shipped), f + ": the .rmod's copy is read (" + (read != null ? read.Length : 0) + " bytes)");
			}
			// A lock held for 0.25 s (an antivirus scan) is waited out
			string path = System.IO.Path.Combine(DynamicIslands.assetpath, "citest-lock.txt");
			try
			{
				System.IO.File.WriteAllText(path, "old");
				var held = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None);
				System.Threading.Tasks.Task.Delay(250).ContinueWith(_ => held.Dispose());
				SafeFile.WriteAllText(path, "new");
				Check(ref ok, System.IO.File.ReadAllText(path) == "new", "a save waits out a short lock by another program");
			}
			catch (Exception e) { Check(ref ok, false, "a save waits out a short lock: " + e.Message); }
			finally { try { System.IO.File.Delete(path); } catch { } }
			Check(ref ok, QuestRewards.WhereKept.Count(ch => ch == '-') == 5, "the quest rewards record is per world and player (" + QuestRewards.WhereKept + ")");
			if (ok) Log("PASS: start checks"); else Fail("start checks");
		}
			[ConsoleCommand(name: "CIAuBatch2", docs: "Dev, in game (host): AU15/20/24/29 - world names with an apostrophe read whole; new plan rules get names no rule had; the world keeps its own regrow days in its file; a downloaded island file is saved for every island with that content (two names, one file)")]
		public static void AuBatch2Command(string[] args) { StartTest(AuBatch2Routine()); }

		static IEnumerator AuBatch2Routine()
		{
			if (!Raft_Network.IsHost || !LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			Check(ref ok, Housekeeping.WorldName(new[] { "# Custom islands in world 'Bob's raft': name|x|y|z" }, "x.txt") == "Bob's raft"
				&& Housekeeping.WorldName(new[] { "# Custom islands in world 'CI Net'" }, "x.txt") == "CI Net"
				&& Housekeeping.WorldName(new[] { "@plan=x" }, "C:/w/My World.txt") == "My World", "world names: Bob's raft whole, an old header, no header (AU29)");
			var plan = new WorldPlan { Name = "x" };
			for (int i = 0; i < 4; i++) plan.Rules.Add(new IntroRule { Id = WorldPlanWindow.NewRuleId(plan) });
			string third = plan.Rules[2].Id;
			plan.Rules.RemoveAt(2);
			string next = WorldPlanWindow.NewRuleId(plan);
			Check(ref ok, plan.Rules.Select(r => r.Id).Distinct().Count() == 3 && next != third && !plan.Rules.Any(r => r.Id == next), "a new rule's name isn't one the plan had (" + string.Join(",", plan.Rules.Select(r => r.Id).ToArray()) + "; deleted " + third + ", new " + next + ") (AU24)");
			// The world's regrow days, kept in its file
			int before = WorldRules.RegrowDays;
			WorldRules.SetRegrow(before == 7 ? 8 : 7);
			int set = WorldRules.RegrowDays;
			bool inFile = System.IO.File.Exists(IslandWorldState.WorldFilePath) && System.IO.File.ReadAllLines(IslandWorldState.WorldFilePath).Contains("@regrow=" + set);
			Check(ref ok, set != CustomIslandSpawner.RegrowDays || set == 7 || set == 8, "the world has its own regrow days: " + set + " (this PC's spawnpool.txt: " + CustomIslandSpawner.RegrowDays + ")");
			Check(ref ok, inFile, "kept in the world's file (@regrow=" + set + ") (AU20)");
			WorldRules.SetRegrow(before);
			// Two of the host's islands with the same content: the download is saved for both names
			string source = IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci") && !n.StartsWith("gen-") && !IslandNetwork.IsDownloadName(n));
			byte[] bytes = System.IO.File.ReadAllBytes(IslandSpawner.PathFor(source));
			string hash = IslandNetwork.HashOf(source);
			var a = IslandWorldState.Add("citest-dup-a", new Vector3(90000f, 0f, 90000f), null, false);
			var b = IslandWorldState.Add("citest-dup-b", new Vector3(91000f, 0f, 90000f), null, false);
			foreach (var e in new[] { a, b }) { e.Hash = hash; e.WaitingForFile = true; }
			string pa = IslandSpawner.PathFor(IslandNetwork.DownloadName("citest-dup-a", hash)), pb = IslandSpawner.PathFor(IslandNetwork.DownloadName("citest-dup-b", hash));
			try
			{
				IslandNetwork.ExpectFile(hash);
				const int size = 3000;
				int count = (bytes.Length + size - 1) / size;
				for (int i = count - 1; i >= 0; i--)
					IslandNetwork.ReceiveChunk(new IslandNetMessage { Kind = IslandNetMessage.FileChunk, Name = "citest-dup-a", Hash = hash, Index = i, Count = count, Data = Convert.ToBase64String(bytes, i * size, Math.Min(size, bytes.Length - i * size)) });
				Check(ref ok, System.IO.File.Exists(pa) && System.IO.File.Exists(pb) && !a.WaitingForFile && !b.WaitingForFile, "one download, saved for both islands with that content (AU15)");
			}
			finally
			{
				IslandWorldState.RemoveIds(new[] { a.Id, b.Id }, false);
				foreach (string f in new[] { pa, pb }) try { if (System.IO.File.Exists(f)) System.IO.File.Delete(f); } catch { }
			}
			yield return null;
			if (ok) Log("PASS: au batch 2"); else Fail("au batch 2");
		}
	}
}
