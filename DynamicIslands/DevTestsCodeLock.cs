using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIMapReach", docs: "Dev, anywhere: ROADMAP LM3 - every map type's islands (3 seeds each): no chest or note left where players can't get without building (the reach check moves them)")]
		public static void MapReachCommand(string[] args) { StartTest(MapReachRoutine()); }

		static IEnumerator MapReachRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			int total = 0, movedAll = 0;
			foreach (MapType type in MapTypes.All)
				for (int seed = 1; seed <= 3; seed++)
				{
					float elevation;
					IslandGenSettings s = MapTypes.Roll(type, new System.Random(seed * 7919 + type.Name.Length), out elevation);
					IslandFile f = MapTypes.Create(type, s, elevation, "cimapreach");
					if (elevation != 0f) continue;
					// (the check again on the result: what is still out of reach)
					int moved = IslandReach.MoveContentWithinReach(f);
					int content = f.Objects.Count(o => ContentCatalog.IsLootObject(o.Name) || ContentCatalog.IsNoteObject(o.Name));
					total += content; movedAll += moved;
					if (moved > 0) Check(ref ok, false, type.Name + " seed " + seed + ": " + moved + " of " + content + " chests/notes still out of reach after Create");
					yield return null;
				}
			Check(ref ok, total > 0, total + " chests and notes on the map types' islands, all where players reach them");
			if (ok) Log("PASS: map reach"); else Fail("map reach");
		}

		[ConsoleCommand(name: "CIRewardLater", docs: "Dev, in game (host): ROADMAP LM8 - a quest done while this player is far away: no reward then; coming to the island gives it, once (done again or coming again gives nothing)")]
		public static void RewardLaterCommand(string[] args) { StartTest(RewardLaterRoutine()); }

		static IEnumerator RewardLaterRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "cirewardlater";
			var s = new IslandGenSettings { Seed = 8787, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			f.Props[IslandQuest.KeyTitle] = "CI reward later";
			f.Props[IslandQuest.KeySteps] = "reach|cirewardzone|1|";
			f.Props[IslandQuest.KeyReward] = "Plank*7";
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); yield break; }
			// (far away: 500 m off the island, in the water)
			Vector3 away = e.Position + (e.Position - (CustomIslandSpawner.RaftPosition ?? e.Position + Vector3.forward)).normalized * 500f;
			player.transform.position = new Vector3(away.x, 0.5f, away.z);
			yield return new WaitForSeconds(1f);
			Dictionary<string, int> before = Items(player);
			QuestTracker.Set(e, 1, 0, true);
			yield return new WaitForSeconds(1f);
			string got = Gained(before, Items(player));
			Check(ref ok, got.Length == 0 && QuestRewards.Owed(e.HostName), "done while far away: nothing given yet, the reward kept (" + (got.Length > 0 ? got : "nothing") + ")");
			PutPlayerNear(e.Root.transform, 2f);
			Vector3 c = e.Position + new Vector3(0f, 0f, 0f);
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
			yield return new WaitForSeconds(2.5f);
			got = Gained(before, Items(player));
			Check(ref ok, got.Contains("Plank") && !QuestRewards.Owed(e.HostName) && QuestRewards.Rewarded(e.HostName), "coming to the island gives it: " + (got.Length > 0 ? got : "nothing"));
			Dictionary<string, int> mid = Items(player);
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
			yield return new WaitForSeconds(2f);
			Check(ref ok, Gained(mid, Items(player)).Length == 0, "coming again gives nothing more");
			OnRaftCommand();
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: reward later"); else Fail("reward later");
		}

		[ConsoleCommand(name: "CILightColours", docs: "Dev, in game (host): ROADMAP LM5 - four real warthogs: as they are, the randomizer's light snow and cream (brightened textures) and its charcoal; a picture shot_light_colours.png")]
		public static void LightColoursCommand(string[] args) { StartTest(LightColoursRoutine()); }

		static IEnumerator LightColoursRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			const string name = "cilightcolours";
			var s = new IslandGenSettings { Seed = 8888, Radius = 40f, Height = 4f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			for (int i = 0; i < 4; i++)
			{
				float x = c.x + (i - 1.5f) * 3f, z = c.y + 6f;
				f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = new Vector3(x, ground(x, z), z), Props = new Dictionary<string, string> { { ObjectProps.CreatureCount, "1" } } });
			}
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); yield break; }
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			Vector3 mid = LandSpot(e);
			player.transform.position = mid + Vector3.up * 2f;
			yield return ScWaitAnimals(e, "Warthog", 4, 15f);
			List<AI_NetworkBehaviour> hogs = ScAnimals(e, "Warthog").OrderBy(a => a.transform.position.x).ToList();
			if (hogs.Count < 4) { Fail("only " + hogs.Count + " warthogs"); yield break; }
			string[] looks = { null, "snow", "cream", "charcoal" };
			bool ok = true;
			for (int i = 0; i < 4; i++)
			{
				AI_NetworkBehaviour a = hogs[i];
				// (still for the picture: in a row in front of the player)
				foreach (MonoBehaviour m in a.GetComponents<MonoBehaviour>()) if (m != a && m.GetType().Name.StartsWith("AI_State")) m.enabled = false;
				a.transform.position = mid + new Vector3((i - 1.5f) * 2.6f, 0.2f, 6f);
				a.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
				if (looks[i] != null) ok &= WorldRandomizer.ApplyColourForTest(a, looks[i]);
			}
			player.transform.position = mid + new Vector3(0f, 0.5f, 0f);
			player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
			Camera cam = Camera.main;
			if (cam != null) cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.15f, 1f));
			yield return new WaitForSeconds(0.4f);
			Screenshot(new[] { "light_colours" });
			yield return new WaitForSeconds(0.8f);
			OnRaftCommand();
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: light colours"); else Fail("light colours");
		}

		[ConsoleCommand(name: "CIMoreQuests", docs: "Dev, in game (host): ROADMAP LM4 - an island with a main quest and a second one: each counts its own steps, the second done first gives its reward and leaves the main quest open, the panel shows the one not done; both stay done after a reload; a plan rule waits for 'quest 2'")]
		public static void MoreQuestsCommand(string[] args) { StartTest(MoreQuestsRoutine()); }

		static IEnumerator MoreQuestsRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "cimorequests";
			QuestRewards.ForgetIsland(name); // (an earlier run in this world was paid already)
			var s = new IslandGenSettings { Seed = 8989, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			new IslandQuest { Title = "Main", Steps = { new IslandQuest.Step { Type = "reach", Target = "gate" }, new IslandQuest.Step { Type = "reach", Target = "tower" } }, Reward = "Rope*2" }.To(f.Props, 0);
			new IslandQuest { Title = "The lost goat", Steps = { new IslandQuest.Step { Type = "reach", Target = "pen" } }, Reward = "Plank*5" }.To(f.Props, 1);
			// (CB11: two steps asking for the same kill, both animals defeated before the first came)
			new IslandQuest { Title = "Two bears", Steps = { new IslandQuest.Step { Type = "reach", Target = "den" }, new IslandQuest.Step { Type = "kill", Target = "Polar Bear" },
				new IslandQuest.Step { Type = "reach", Target = "cave" }, new IslandQuest.Step { Type = "kill", Target = "Polar Bear" } } }.To(f.Props, 2);
			// (CB11: a collect step the crew already has enough for moves on with the step before it, not half a second later)
			new IslandQuest { Title = "Held already", Steps = { new IslandQuest.Step { Type = "reach", Target = "dock" }, new IslandQuest.Step { Type = "collect", Target = "story:cimqheld", Count = 1 } } }.To(f.Props, 3);
			Check(ref ok, IslandQuest.CountIn(f.Props) == 4 && f.Props.ContainsKey("quest2.steps"), "four quests in the island's settings (quest2.steps)");
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); yield break; }
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
			yield return new WaitForSeconds(1.5f);
			Dictionary<string, int> before = Items(player);
			QuestTracker.Event(e, "reach", "pen");
			yield return new WaitForSeconds(1f);
			string got = Gained(before, Items(player));
			Check(ref ok, QuestTracker.IsDone(e, 1) && !QuestTracker.IsDone(e, 0) && QuestTracker.StepOf(e, 0) == 0, "the second quest done on its own: the main quest still at its first step");
			Check(ref ok, got.Contains("Plank") && !got.Contains("Rope"), "the second quest's reward: " + got);
			QuestTracker.Event(e, "reach", "gate");
			QuestTracker.Event(e, "reach", "tower");
			yield return new WaitForSeconds(1f);
			Check(ref ok, QuestTracker.IsDone(e, 0), "the main quest done with its own steps");
			QuestTracker.Event(e, "kill", "Polar Bear");
			QuestTracker.Event(e, "kill", "Polar Bear");
			QuestTracker.Event(e, "reach", "den");
			int bearStep = QuestTracker.StepOf(e, 2);
			QuestTracker.Event(e, "reach", "cave");
			Check(ref ok, bearStep == 2 && QuestTracker.IsDone(e, 2), "two bears defeated early count once each: both kill steps done (step after the den " + (bearStep + 1) + ", done " + QuestTracker.IsDone(e, 2) + ")");
			StoryBook.Give("cimqheld", 1);
			QuestTracker.Event(e, "reach", "dock");
			Check(ref ok, QuestTracker.IsDone(e, 3), "an item held already: its collect step is done with the reach before it, at once (step " + QuestTracker.StepOf(e, 3) + ")");
			StoryBook.Take("cimqheld", 1);
			OnRaftCommand();
			yield return new WaitForSeconds(0.5f);
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, QuestTracker.IsDone(e, 0) && QuestTracker.IsDone(e, 1), "after a reload both stay done");
			IntroRule r = IntroRule.Parse("after2 | island:" + name + " | quest:" + name + ":2 | ahead:300 | | test");
			Check(ref ok, r != null && r.ToLine().Contains("quest:" + name + ":2") && WorldDirector.Happened(r, e), "a plan rule waiting for quest 2 of the island (" + (r != null ? r.ToLine() : "no rule") + ") sees it done");
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: more quests"); else Fail("more quests");
		}

		[ConsoleCommand(name: "CIPoolNoPlanIslands", docs: "Dev, anywhere: ROADMAP CW4 - an island the world plan brings by name is never picked by chance (the pool leaves it out; the whole pool still lists it)")]
		public static void PoolNoPlanIslandsCommand(string[] args)
		{
			bool ok = true;
			List<string> all = CustomIslandSpawner.Pool(false).Select(p => p.Key).Where(n => !n.StartsWith("type:") && n != CustomIslandSpawner.GeneratedEntry).ToList();
			// (the islands with weight 0 aren't in the pool at all: then any saved island, with the pool's own line for it)
			string name = all.Count > 0 ? all[0] : IslandSpawner.ListSavedIslands().FirstOrDefault(n => !n.StartsWith("gen-") && !n.StartsWith("ci"));
			if (name == null) { Fail("no saved islands"); return; }
			bool inWhole = all.Count > 0;
			WorldPlan keep = WorldDirector.Plan;
			try
			{
				WorldDirector.Plan = new WorldPlan { Name = "CI pool", Random = true, Rules = { IntroRule.Parse("planned | island:" + name + " | start | ahead:300 | | ") } };
				Check(ref ok, CustomIslandSpawner.PlanIslandNames().Contains(name), "the plan's island '" + name + "' is known as the plan's");
				Check(ref ok, !CustomIslandSpawner.Pool(true).Any(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)), "the pool for this world leaves it out");
				if (inWhole) Check(ref ok, CustomIslandSpawner.Pool(false).Any(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)), "the whole pool (the New Game box's list) still has it");
				else Log("  (the pool has no islands with weight here: only the plan's names checked)");
			}
			finally { WorldDirector.Plan = keep; }
			if (ok) Log("PASS: pool without plan islands"); else Fail("pool without plan islands");
		}

		[ConsoleCommand(name: "CIQuestCountToCome", docs: "Dev, in game (host): ROADMAP CT9 - the journal's quest count has every island of this world's random pool that has a quest and hasn't come (random islands on); with them off, none")]
		public static void QuestCountToComeCommand(string[] args)
		{
			bool ok = true, was = CustomIslandSpawner.Enabled;
			try
			{
				CustomIslandSpawner.Enabled = true;
				WorldPlan plan = WorldDirector.Plan;
				List<string> expected = CustomIslandSpawner.Pool(true).Select(p => p.Key)
					.Where(n => IslandSpawner.ListSavedIslands().Contains(n) && IslandQuest.From(IslandCache.Props(n)).Steps.Count > 0
						&& !IslandWorldState.Islands.Any(e => e.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || e.HostName.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
				List<QuestCount.Quest> quests = QuestCount.All();
				int toCome = quests.Count(q => q.Group == QuestCount.ToCome);
				int done, total;
				QuestCount.Count(quests, out done, out total);
				Log("  pool " + CustomIslandSpawner.Pool(true).Count + ", with a quest not come " + expected.Count + "; counted to come " + toCome + "; QUESTS " + done + "/" + total);
				if (plan != null && !plan.Random) Check(ref ok, toCome == 0, "the plan has random islands off: none to come counted");
				else Check(ref ok, toCome > 0 && toCome <= expected.Count && quests.Where(q => q.Group == QuestCount.ToCome).All(q => !q.Done), "the pool's quest islands still to come count, not done (" + toCome + " of " + expected.Count + " - those another rule already counts aren't twice)");
				HashSet<string> plans = CustomIslandSpawner.AllPlansIslandNames();
				List<string> inPlan = QuestCount.StillToCome().Where(n => plans.Contains(n) || CustomIslandSpawner.PlanIslandNames().Contains(n)).ToList();
				Log("  plan islands " + plans.Count + ", among those to come " + inPlan.Count + (inPlan.Count > 0 ? " (" + string.Join(", ", inPlan.ToArray()) + ")" : ""));
				Check(ref ok, inPlan.Count == 0, "no island of a world plan (story plans too) is among those to come");
				CustomIslandSpawner.Enabled = false;
				Check(ref ok, !QuestCount.All().Any(q => q.Group == QuestCount.ToCome), "random islands off: none to come");
			}
			finally { CustomIslandSpawner.Enabled = was; }
			if (ok) Log("PASS: quest count to come"); else Fail("quest count to come");
		}

		[ConsoleCommand(name: "CIRandomizerNames", docs: "Dev, in game: ROADMAP CT11 - the randomizer's extras (rnd-...) show a readable name in the journal, never the file name")]
		public static void RandomizerNamesCommand(string[] args) { StartTest(RandomizerNamesRoutine()); }

		static IEnumerator RandomizerNamesRoutine()
		{
			bool ok = true;
			Check(ref ok, WorldRandomizer.TitleFor(new[] { "a few animals", "a castaway's stash" }) == "A castaway's stash", "a stash's extras are called A castaway's stash");
			Check(ref ok, WorldRandomizer.TitleFor(new[] { "loot" }) == "Randomizer finds", "extras without a find: Randomizer finds");
			Check(ref ok, WorldRandomizer.Readable("rnd-782549770-8803") == "Randomizer finds", "an old page's file name reads Randomizer finds");
			Check(ref ok, WorldRandomizer.Readable("Gullsong Bazaar") == "Gullsong Bazaar", "other names stay");
			// (a page saved with the file name, as before - the journal's line and the page's foot read the readable name)
			StoryBook.AddPage("note:rnd-782549770-8803:0:ci", "Castaway's diary", "Day 3. Still no ship.", "rnd-782549770-8803");
			JournalWindow.Open();
			yield return null; yield return null;
			string[] texts = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>().Where(t => t.gameObject.activeInHierarchy && t.GetComponentInParent<JournalWindow>() != null).Select(t => t.text).ToArray();
			Check(ref ok, texts.Any(t => t.StartsWith("Randomizer finds")), "the journal has a line Randomizer finds");
			foreach (string t in texts.Where(x => x.Contains("rnd-") || x.StartsWith("Randomizer"))) Log("  text: " + t);
			Check(ref ok, !texts.Any(t => t.Contains("rnd-")), "no rnd- file name in the journal");
			JournalWindow.Close();
			StoryBook.Page added = StoryBook.Pages.FirstOrDefault(x => x.Key == "note:rnd-782549770-8803:0:ci"); if (added != null) StoryBook.Pages.Remove(added);
			if (ok) Log("PASS: randomizer names"); else Fail("randomizer names");
		}

		[ConsoleCommand(name: "CIEscGuard", docs: "Dev, in game: one Esc closes only our window - Raft's pause menu skips the press while the journal is open and the frame after it closed, then takes Esc again")]
		public static void EscGuardCommand(string[] args) { StartTest(EscGuardRoutine()); }

		static IEnumerator EscGuardRoutine()
		{
			bool ok = true;
			yield return null;
			var info = HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(PauseMenu), "Update"));
			Check(ref ok, info != null && info.Prefixes.Any(x => x.PatchMethod.DeclaringType == typeof(EscGuard)), "Raft's pause menu Update has the guard");
			Check(ref ok, !EscGuard.Blocks(), "no window of ours: the pause menu takes Esc");
			JournalWindow.Open();
			yield return null;
			Check(ref ok, JournalWindow.IsOpen && EscGuard.Blocks(), "journal open: the pause menu skips Esc");
			JournalWindow.Close();
			Check(ref ok, !JournalWindow.IsOpen && EscGuard.Blocks(), "journal closed on this press: still skipped (the same Esc)");
			yield return null;
			yield return null;
			Check(ref ok, !EscGuard.Blocks(), "two frames later: the pause menu takes Esc again");
			if (ok) Log("PASS: esc guard"); else Fail("esc guard");
		}

		[ConsoleCommand(name: "CIQuestCountCheck", docs: "Dev, in game (either player): ROADMAP CW3 - this machine's quest count as the journal shows it: QCOUNT <done>/<total> <fingerprint> (two players compare)")]
		public static void QuestCountCheckCommand(string[] args)
		{
			List<QuestCount.Quest> quests = QuestCount.All();
			int done, total;
			QuestCount.Count(quests, out done, out total);
			string text = string.Join(";", quests.Select(q => q.Group + "/" + q.Name + "/" + q.Done).ToArray());
			Log("QCOUNT " + done + "/" + total + " " + Fnv(text).ToString("X8"));
			Log("PASS: quest count check");
		}

		[ConsoleCommand(name: "CIQuestCountMP", docs: "Dev, in game (host): ROADMAP CW3 for two players - start: an island with two quests next to the raft (kept); done: its second quest done")]
		public static void QuestCountMPCommand(string[] args) { StartTest(QuestCountMPRoutine(args != null && args.Length > 0 ? args[0] : "start")); }

		static IEnumerator QuestCountMPRoutine(string what)
		{
			const string name = "ciqcount";
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("quest count mp (host, in a world)"); yield break; }
			if (what == "done")
			{
				IslandWorldState.Entry d = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
				if (d == null) { Fail("quest count mp: no island"); yield break; }
				QuestTracker.Event(d, "reach", "pen");
				yield return new WaitForSeconds(1f);
				if (QuestTracker.IsDone(d, 1)) Log("PASS: quest count mp"); else Fail("quest count mp: not done");
				yield break;
			}
			var s = new IslandGenSettings { Seed = 9090, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			new IslandQuest { Title = "Main", Steps = { new IslandQuest.Step { Type = "reach", Target = "gate" } }, Reward = "Rope*1" }.To(f.Props, 0);
			new IslandQuest { Title = "Side", Steps = { new IslandQuest.Step { Type = "reach", Target = "pen" } }, Reward = "Plank*1" }.To(f.Props, 1);
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("quest count mp: no open sea"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			yield return new WaitForSeconds(2f);
			Log("PASS: quest count mp");
		}

		[ConsoleCommand(name: "CISpotlightProbe", docs: "Dev, in game (host): ROADMAP E13 - Varuna Point's spotlight on an island next to the player: what it is made of, and whether the player is hurt in 12 s")]
		public static void SpotlightProbeCommand(string[] args) { StartTest(SpotlightProbeRoutine(args != null && args.Length > 0 ? args[0] : "VP_Spotlight")); }

		static IEnumerator SpotlightProbeRoutine(string what)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { what });
			const string name = "cispotlight";
			var s = new IslandGenSettings { Seed = 9191, Radius = 30f, Height = 4f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			float gy = IslandGenerator.SampleHeights(f.Heights, res, step, c.x + 6f, c.y) * f.TerrainSize.y;
			f.Objects.Add(new IslandObject { Name = what, Position = new Vector3(c.x + 6f, gy, c.y), Scale = PlaceableCatalog.Get(what).transform.localScale });
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail("no island"); yield break; }
			Transform lightT = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(tr => tr.name.StartsWith(what));
			if (lightT != null)
				foreach (Transform tr in lightT.GetComponentsInChildren<Transform>(true))
					Log("spot: " + tr.name + " tag=" + tr.tag + " layer=" + LayerMask.LayerToName(tr.gameObject.layer) + " comps: " + string.Join(", ", tr.GetComponents<Component>().Select(cp => cp != null ? cp.GetType().Name : "-").ToArray()));
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
			float h0 = player.Stats.stat_health.Value;
			yield return new WaitForSeconds(12f);
			float h1 = player.Stats.stat_health.Value;
			Log("spot: health " + h0.ToString("F0") + " -> " + h1.ToString("F0"));
			OnRaftCommand();
			IslandWorldState.Remove(name);
			if (h1 >= h0 - 0.5f) Log("PASS: spotlight probe"); else Fail("spotlight probe: the player was hurt");
		}

		[ConsoleCommand(name: "CIBlockProbe", docs: "Dev, anywhere: ROADMAP E11 - Raft's door wall blocks as Raft makes them: every child with a collider, its layer, size and trigger flag")]
		public static void BlockProbeCommand(string[] args)
		{
			foreach (Item_Base item in ItemManager.GetAllItems().Where(i => i != null && i.UniqueName != null && i.UniqueName.StartsWith("Block_") && (i.UniqueName.Contains("Door") || i.UniqueName == "Block_Wall_Wood")))
			{
				Block[] blocks;
				try { blocks = item.settings_buildable.GetBlockPrefabs(); } catch { continue; }
				Block prefab = blocks != null ? blocks.FirstOrDefault(b => b != null) : null;
				if (prefab == null) continue;
				foreach (Collider col in prefab.GetComponentsInChildren<Collider>(true))
				{
					Bounds b = col is BoxCollider ? new Bounds(((BoxCollider)col).center, ((BoxCollider)col).size) : new Bounds();
					Log("block: " + item.UniqueName + " / " + col.name + " " + col.GetType().Name + " layer=" + LayerMask.LayerToName(col.gameObject.layer) + " trigger=" + col.isTrigger + " enabled=" + col.enabled + (col is BoxCollider ? " size=" + b.size + " centre=" + b.center : ""));
				}
			}
			foreach (Item_Base item in ItemManager.GetAllItems().Where(i => i != null && i.UniqueName == "Block_Wall_Door_Wood"))
			{
				Block prefab = item.settings_buildable.GetBlockPrefabs().FirstOrDefault(b => b != null);
				foreach (Transform tr in prefab.GetComponentsInChildren<Transform>(true))
				{
					Renderer rr = tr.GetComponent<Renderer>();
					Log("block part: " + tr.name + " parent=" + (tr.parent != null ? tr.parent.name : "-") + " local=" + tr.localPosition + " rot=" + tr.localEulerAngles + (rr != null ? " bounds=" + rr.bounds.size + " at " + (rr.bounds.center - prefab.transform.position) : "") + " comps: " + string.Join(", ", tr.GetComponents<Component>().Select(cp => cp != null ? cp.GetType().Name : "-").ToArray()));
				}
			}
			Log("PASS: block probe");
		}

		[ConsoleCommand(name: "CIDoorway", docs: "Dev, in game (host): ROADMAP E11 - Raft's door walls on an island: the doorway lets a player through (nothing at chest height), the lintel over it still blocks; a picture shot_doorway.png")]
		public static void DoorwayCommand(string[] args) { StartTest(DoorwayRoutine()); }

		static IEnumerator DoorwayRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureBuilt();
			const string name = "cidoorway";
			var s = new IslandGenSettings { Seed = 9292, Radius = 30f, Height = 4f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			float gy = IslandGenerator.SampleHeights(f.Heights, res, step, c.x, c.y) * f.TerrainSize.y;
			string[] kinds = { "Block_Wall_Door_Wood", "Block_Wall_Door_Thatch", "Block_Wall_Door_Tier3" };
			for (int i = 0; i < kinds.Length; i++)
				f.Objects.Add(new IslandObject { Name = kinds[i], Position = new Vector3(c.x + i * 4f - 4f, gy, c.y), Props = new Dictionary<string, string> { { BehaviourProps.Name, "door" + i } } });
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail("no island"); yield break; }
			yield return new WaitForSeconds(1f);
			Physics.SyncTransforms();
			for (int i = 0; i < kinds.Length; i++)
			{
				IslandObjectRef r = ScObjOf(e, "door" + i);
				if (r == null) { Check(ref ok, false, kinds[i] + " on the island"); continue; }
				Vector3 at = r.transform.position;
				Func<float, bool> blocked = h => Physics.RaycastAll(at + new Vector3(0f, h, -2f), Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore).Any(hit => hit.collider.transform.IsChildOf(r.transform));
				Check(ref ok, !blocked(1.0f) && !blocked(1.7f), kinds[i] + ": the doorway is open (nothing at 1.0 or 1.7 m)");
				Check(ref ok, blocked(2.17f), kinds[i] + ": the lintel over it still blocks (2.17 m)");
			}
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			IslandObjectRef first = ScObjOf(e, "door0");
			if (first != null)
			{
				player.transform.position = first.transform.position + new Vector3(0f, 0.5f, -5f);
				Camera cam = Camera.main;
				if (cam != null) cam.transform.rotation = Quaternion.LookRotation(first.transform.position + Vector3.up * 1.2f - cam.transform.position);
				yield return new WaitForSeconds(0.5f);
				Screenshot(new[] { "doorway" });
				yield return new WaitForSeconds(0.6f);
			}
			OnRaftCommand();
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: doorway"); else Fail("doorway");
		}

		[ConsoleCommand(name: "CILockFirst", docs: "Dev, in game (host): ROADMAP E12 - a 'find 2 gems' step whose gems a lock used up before the step came: it is done when it comes (found in all counts), and the count is saved with the world")]
		public static void LockFirstCommand(string[] args) { StartTest(LockFirstRoutine()); }

		static IEnumerator LockFirstRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "cilockfirst";
			var s = new IslandGenSettings { Seed = 9393, Radius = 30f, Height = 4f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			new IslandQuest { Title = "Gems", Steps = { new IslandQuest.Step { Type = "reach", Target = "gate" }, new IslandQuest.Step { Type = "collect", Target = "story:cigem", Count = 2 } } }.To(f.Props, 0);
			f.Props[StoryItems.Key] = "cigem|Gem||A test gem.";
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail("no island"); yield break; }
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
			yield return new WaitForSeconds(1f);
			// The gems found, and a lock uses them up - before the step that asks for them
			StoryBook.Give("cigem", 2);
			yield return new WaitForSeconds(0.3f);
			StoryBook.Take("cigem", 2);
			yield return new WaitForSeconds(0.3f);
			Check(ref ok, StoryBook.Count("cigem") == 0 && StoryBook.FoundCount("cigem") == 2, "held 0 after the lock, found 2 in all");
			QuestTracker.Event(e, "reach", "gate");
			yield return new WaitForSeconds(2f);
			Log("  entry at " + e.Position + ", root at " + e.Root.transform.position + ", player at " + player.transform.position + ", land radius " + CustomIslandSpawner.LandRadius(e.Name) + ", found " + QuestTracker.Found(e, QuestTracker.QuestOf(e).Steps[1]));
			Check(ref ok, QuestTracker.IsDone(e, 0), "the 'find 2 gems' step is done when it comes (step " + QuestTracker.StepOf(e) + " of 2)");
			string line = StoryBook.WriteLines().FirstOrDefault(l => l.StartsWith("@story.item=cigem"));
			// (id|count|name|icon|description|found|Raft item: the found count is the 6th field - a Raft item one came after it in X8)
			Check(ref ok, line != null && line.Split('|').Length >= 6 && line.Split('|')[5] == "2", "saved with the world: " + line);
			OnRaftCommand();
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: lock first"); else Fail("lock first");
		}

		[ConsoleCommand(name: "CIStorageAbsent", docs: "Dev, in game (host): ROADMAP T9 - with private storages on, a storage whose builder isn't in the game opens for the host, not for another player; the builder's own still opens for them")]
		public static void StorageAbsentCommand(string[] args)
		{
			Network_Player local = RAPI.GetLocalPlayer();
			if (local == null || !Raft_Network.IsHost) { Fail("run in a world, as the host"); return; }
			bool ok = true;
			bool had = WorldOptions.On(WorldOptions.PrivateStorage);
			string keep = PrivateStorage.Encode();
			try
			{
				WorldOptions.Current.Add(WorldOptions.PrivateStorage);
				const ulong gone = 76561190000000001UL, other = 76561190000000002UL;
				PrivateStorage.Decode("4242:" + gone + ";4343:" + other);
				Check(ref ok, PrivateStorage.MayOpen(4242, local.steamID.Id), "the host opens the storage of a builder who left");
				Check(ref ok, !PrivateStorage.MayOpen(4242, other), "another player still can't");
				Check(ref ok, PrivateStorage.MayOpen(4343, other), "the builder opens their own");
			}
			finally
			{
				if (!had) WorldOptions.Current.Remove(WorldOptions.PrivateStorage);
				PrivateStorage.Decode(keep);
			}
			if (ok) Log("PASS: storage absent"); else Fail("storage absent");
		}

		/// <summary>A spot on the island's land, near its middle (the ground there, from a ray down).</summary>
		static Vector3 LandSpot(IslandWorldState.Entry e)
		{
			IslandInfoTag tag = e.Root != null ? e.Root.GetComponent<IslandInfoTag>() : null;
			Vector3 c = tag != null ? e.Root.transform.position + tag.LocalCentre : e.Position;
			RaycastHit hit;
			return Physics.Raycast(new Vector3(c.x, 200f, c.z), Vector3.down, out hit, 400f, LayerMasks.MASK_GroundMask, QueryTriggerInteraction.Ignore) ? hit.point : c + Vector3.up * 3f;
		}

		const string CodeLockIsland = "cicodelock";

		[ConsoleCommand(name: "CICodeLock", docs: "Dev, in game (host): a keypad code lock (lock.code) - used, the keypad opens; a wrong code keeps it shut, the right one runs the use (shows a hidden chest); unlocked it stays so after a reload")]
		public static void CodeLockCommand(string[] args) { StartTest(CodeLockRoutine()); }

		static IEnumerator CodeLockRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureBuilt();
			var s = new IslandGenSettings { Seed = 8686, Radius = 40f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, CodeLockIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			f.Objects.Add(new IslandObject { Name = "RT_PowerBox", Position = new Vector3(c.x, ground(c.x, c.y), c.y), Props = new Dictionary<string, string> {
				{ BehaviourProps.Name, "keypad" }, { BehaviourProps.Use, "Type the code" }, { CodeLock.Code, "4711" }, { BehaviourProps.EventKey("use"), "show|safe" } } });
			f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = new Vector3(c.x + 3f, ground(c.x + 3f, c.y), c.y), Props = new Dictionary<string, string> {
				{ BehaviourProps.Name, "safe" }, { BehaviourProps.Hidden, "1" }, { ObjectProps.LootItems, "Scrap*3" }, { ObjectProps.NoteTitle, "Safe" } } });
			IslandWorldState.Remove(CodeLockIsland);
			f.Save(IslandSpawner.PathFor(CodeLockIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(CodeLockIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(CodeLockIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == CodeLockIsland);
			if (e == null || e.Root == null) { Fail(CodeLockIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			IslandObjectRef pad = ScObjOf(e, "keypad"), safe = ScObjOf(e, "safe");
			if (pad == null || safe == null) { Fail("keypad or safe missing"); yield break; }
			PutPlayerNear(pad.transform, 1.2f);
			Check(ref ok, !safe.gameObject.activeInHierarchy, "the safe is hidden at first");
			CodeLock.Use(e, pad.Index, ObjectProps.Get(pad.Props, CodeLock.Code), pad.transform);
			yield return new WaitForSeconds(0.4f);
			Check(ref ok, CodeLock.IsOpen, "using it opens the keypad");
			foreach (string k in new[] { "1", "2", "3", "4", "OK" }) CodeLock.Press(k);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, CodeLock.IsOpen && !safe.gameObject.activeInHierarchy && !CodeLock.Unlocked(e, pad.Index), "a wrong code keeps it shut");
			foreach (string k in new[] { "4", "7", "1", "1", "OK" }) CodeLock.Press(k);
			yield return new WaitForSeconds(0.8f);
			Check(ref ok, !CodeLock.IsOpen && safe.gameObject.activeInHierarchy && CodeLock.Unlocked(e, pad.Index), "the right code opens it: the safe shows");
			OnRaftCommand();
			yield return new WaitForSeconds(0.5f);
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			IslandObjectRef safe2 = ScObjOf(e, "safe");
			Check(ref ok, CodeLock.Unlocked(e, pad.Index) && safe2 != null && safe2.gameObject.activeInHierarchy, "after a reload it stays unlocked, the safe there");
			IslandWorldState.Remove(CodeLockIsland);
			if (ok) Log("PASS: code lock"); else Fail("code lock");
		}
	}
}
