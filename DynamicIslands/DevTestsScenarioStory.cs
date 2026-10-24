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
	/// <summary>
	/// Scenario tests of Raft's story with a plan (TEST_PROTOCOL §5j SC29-SC35, SC63): the woven story walked to Utopia,
	/// a chain world followed by a plain one, the chain changed mid-game, a missing replacing island, odd ids.
	/// Raft's notes are played with NoteBook.UnlockFrequency (what reading a note calls) and tuning the Receiver with
	/// ChunkManager.AddChunkPointForcibly (what the Receiver calls), as CIStoryChainWorld does.
	/// </summary>
	public static partial class DevTests
	{
		const string WeavePlan = "CI Story Weave", WeaveHarbor = "ciweave-harbor", WeaveBeyond = "ciweave-beyond";

		static string WeavePlanText()
		{
			return "description = A test: Balboa left out, a harbor instead of Vasagatan, an island after Tangaroa\nrandom = off\nstory = on\nstoryleaveout = Balboa\n" +
				"rule = harbor | island:" + WeaveHarbor + " | start | receiver:500 | A harbor calls on the radio. | Harbor | instead:Vasagatan | visit\n" +
				"rule = beyond | island:" + WeaveBeyond + " | start | receiver:500 | Something beyond Tangaroa. | Beyond | after:Tangaroa | quest\n";
		}

		/// <summary>The story's state in one line (tests compare it before and after saving).</summary>
		static string ChainState()
		{
			return Steps(StoryChain.Steps) + "/" + Steps(StoryChain.Unlocked.OrderBy(x => x)) + "/" + Steps(StoryChain.Done.OrderBy(x => x)) + "/" + Steps(StoryChain.Brought.OrderBy(x => x)) +
				"/" + StoryChain.FrequencyOf("harbor") + "/" + StoryChain.FrequencyOf("beyond");
		}

		/// <summary>Raft's story note that names the island of this type (reading it unlocks that frequency).</summary>
		static void PlayNote(ChunkPointType unlocks)
		{
			NoteBook.UnlockFrequency(unlocks);
		}

		/// <summary>Tunes the host's Receiver to a plan rule's frequency and waits for its island.</summary>
		/// <summary>The Receiver tuned to a plan island's frequency; waits until it is brought (and loaded, unless loaded is
		/// false: brought beyond the load distance - its spot ahead taken - it loads only as players come near).</summary>
		static IEnumerator TuneTo(string rule, float seconds = 60f, bool loaded = true)
		{
			StoryChain.Tick();
			int type = StoryChain.TypeOfRule(rule);
			if (type < StoryChain.ModTypeBase) { Log("  (no frequency for '" + rule + "')"); yield break; }
			ChunkManager cm = ComponentManager<ChunkManager>.Value;
			if (cm != null) cm.AddChunkPointForcibly((ChunkPointType)type);
			yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == rule && (!loaded || e.Root != null)), seconds);
		}

		[ConsoleCommand(name: "CIStoryWeave", docs: "Dev, world (host, a test world 'CI ...'): SC63/SC34 - the woven story (Balboa left out, a harbor instead of Vasagatan, an island after Tangaroa) walked with Raft's own calls. CIStoryWeave part1 = to Caravan Town (then the runner saves and loads); CIStoryWeave part2 = on to Utopia; CIStoryWeave clean = the world's story back to Raft's")]
		public static void StoryWeaveCommand(string[] args)
		{
			string part = args != null && args.Length > 0 ? args[0] : "part1";
			StartTest(part == "clean" ? StoryWeaveClean() : part == "part2" ? StoryWeavePart2() : StoryWeavePart1());
		}

		static IEnumerator StoryWeavePart1()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("story weave part1: host, in a world"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("story weave part1: only in a test world 'CI ...' (it changes the world's story)"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			// The two islands: a harbor (reached = done) and an island with a quest (done when the quest is)
			IslandFile harbor = RuleIsland(WeaveHarbor, "The Harbor");
			harbor.Props.Remove(IslandQuest.KeyTitle); harbor.Props.Remove(IslandQuest.KeySteps);
			harbor.Save(IslandSpawner.PathFor(WeaveHarbor));
			RuleIsland(WeaveBeyond, "Beyond Tangaroa").Save(IslandSpawner.PathFor(WeaveBeyond));
			WorldPlan plan = WorldPlan.Parse(WeavePlan, WeavePlanText());
			plan.Save();
			// Check tells the builder what leaving Balboa out costs (its blueprints Raft's story needs)
			List<PlanChecker.Finding> findings = PlanChecker.Check(plan, false, false, null);
			Check(ref ok, findings.Any(x => x.Text.IndexOf("Balboa", StringComparison.OrdinalIgnoreCase) >= 0 && x.Text.IndexOf("blueprint", StringComparison.OrdinalIgnoreCase) >= 0),
				"Check names Balboa's blueprints Raft's story needs (" + string.Join(" | ", findings.Where(x => x.Text.IndexOf("Balboa", StringComparison.OrdinalIgnoreCase) >= 0).Select(x => x.Level + ": " + x.Text).ToArray()) + ") - SC34");
			Check(ref ok, !findings.Any(x => x.Level == PlanChecker.Level.Problem), "Check finds no problem in the plan (" + string.Join(" | ", findings.Where(x => x.Level == PlanChecker.Level.Problem).Select(x => x.Text).ToArray()) + ")");

			// A clean slate: nothing of Raft's story found yet, then the world gets the plan
			Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
			NoteBook.unlockedNoteBookIndexes.RemoveAll(i => notes.ContainsKey(i));
			NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => x.HostName == WeaveHarbor || x.HostName == WeaveBeyond).Select(x => x.Id).ToList(), true);
			StoryChain.Reset();
			WorldDirector.Done.Clear();
			Check(ref ok, ScSetPlan(WeavePlan, true), "the world gets the plan '" + WeavePlan + "'");
			yield return new WaitForSeconds(1f);
			Check(ref ok, StoryChain.Active && Steps(StoryChain.Steps) == RaftSteps("RadioTower", "rule:harbor", "CaravanTown", "Tangaroa", "rule:beyond", "VarunaPoint", "Temperance", "Utopia"),
				"the chain: " + Steps(StoryChain.Steps));

			PlayNote(ChunkPointType.Landmark_RadioTower); // (the Receiver's own note)
			Check(ref ok, Names(ChainTypes()) == "Radio Tower", "the Receiver's note: " + Names(ChainTypes()));
			PlayNote(ChunkPointType.Landmark_Vasagatan); // (the Radio Tower's note - Raft's way to Vasagatan)
			StoryChain.Tick();
			string freq = StoryChain.FrequencyOf("harbor");
			Check(ref ok, StoryChain.Done.Contains("raft:RadioTower") && StoryChain.Unlocked.Contains("rule:harbor") && !ChainTypes().Contains(ChunkPointType.Landmark_Vasagatan),
				"the Radio Tower's note: the harbor's step unlocked, Vasagatan never (" + Names(ChainTypes()) + ")");
			Check(ref ok, freq != null && StoryChain.LastBanner != null && StoryChain.LastBanner.Contains(freq), "a banner gives the harbor's frequency: " + StoryChain.LastBanner);
			Check(ref ok, StoryBook.Pages.Any(p => p.Key == "storyfreq:harbor"), "... and the journal keeps it");
			yield return TuneTo("harbor");
			IslandWorldState.Entry h = IslandWorldState.Islands.FirstOrDefault(x => x.Rule == "harbor");
			Check(ref ok, h != null && h.Root != null, "tuned to " + freq + ": the harbor comes");
			if (h != null && h.Root != null)
			{
				yield return StandRoutine(h.Root);
				yield return WaitFor(() => StoryChain.Done.Contains("rule:harbor"), 20f);
			}
			Check(ref ok, StoryChain.Done.Contains("rule:harbor") && ChainTypes().Contains(ChunkPointType.Landmark_CaravanIsland) && !ChainTypes().Contains(ChunkPointType.Landmark_Balboa),
				"reaching the harbor: Caravan Town unlocked, Balboa never (" + Names(ChainTypes()) + ")");
			OnRaftCommand();
			IslandWorldState.Save();
			Log("WEAVE state: " + ChainState());
			if (ok) Log("PASS: story weave part1"); else Fail("story weave part1");
		}

		static IEnumerator StoryWeavePart2()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("story weave part2: host, in a world"); yield break; }
			yield return new WaitForSeconds(3f);
			yield return EnsureAlive();
			bool ok = true;
			// After a save and a load: the same chain, frequencies and unlocked islands
			Check(ref ok, StoryChain.Active && StoryChain.Done.Contains("rule:harbor") && StoryChain.Unlocked.Contains("raft:CaravanTown"), "after loading: the woven chain is where it was (" + ChainState() + ")");
			Check(ref ok, ChainTypes().Contains(ChunkPointType.Landmark_CaravanIsland) && !ChainTypes().Contains(ChunkPointType.Landmark_Vasagatan) && !ChainTypes().Contains(ChunkPointType.Landmark_Balboa),
				"... and the Receiver offers what it did (" + Names(ChainTypes()) + ")");
			string harborFreq = StoryChain.FrequencyOf("harbor");
			Check(ref ok, harborFreq != null && StoryBook.Pages.Any(p => p.Key == "storyfreq:harbor" && p.Text.Contains(harborFreq)), "the harbor's frequency is the same as before the load (" + harborFreq + ")");

			PlayNote(ChunkPointType.Landmark_Tangaroa); // (Caravan Town's note)
			Check(ref ok, StoryChain.Done.Contains("raft:CaravanTown") && ChainTypes().Contains(ChunkPointType.Landmark_Tangaroa), "Caravan Town's note: Tangaroa (" + Names(ChainTypes()) + ")");
			PlayNote(ChunkPointType.Landmark_VarunaPoint); // (Tangaroa's note - Raft's way to Varuna Point)
			StoryChain.Tick();
			Check(ref ok, StoryChain.Done.Contains("raft:Tangaroa") && StoryChain.Unlocked.Contains("rule:beyond") && !ChainTypes().Contains(ChunkPointType.Landmark_VarunaPoint),
				"Tangaroa's note: the island after Tangaroa unlocked, Varuna Point not yet (" + Names(ChainTypes()) + ")");
			string freq = StoryChain.FrequencyOf("beyond");
			Check(ref ok, freq != null && StoryChain.LastBanner != null && StoryChain.LastBanner.Contains(freq), "a banner gives its frequency: " + StoryChain.LastBanner);
			yield return TuneTo("beyond");
			IslandWorldState.Entry b = IslandWorldState.Islands.FirstOrDefault(x => x.Rule == "beyond");
			Check(ref ok, b != null && b.Root != null, "tuned: the island after Tangaroa comes");
			if (b != null && b.Root != null)
			{
				Check(ref ok, !StoryChain.Done.Contains("rule:beyond"), "reaching it isn't enough: its quest must be done");
				TriggerZone z = b.Root.GetComponentInChildren<TriggerZone>();
				if (z != null) { PutPlayerNear(z.transform, 0.3f); z.Enter(); }
				yield return WaitFor(() => StoryChain.Done.Contains("rule:beyond"), 20f);
			}
			Check(ref ok, StoryChain.Done.Contains("rule:beyond") && ChainTypes().Contains(ChunkPointType.Landmark_VarunaPoint), "its quest done: Varuna Point unlocked (" + Names(ChainTypes()) + ")");
			OnRaftCommand();
			PlayNote(ChunkPointType.Landmark_Temperance); // (Varuna Point's note)
			PlayNote(ChunkPointType.Landmark_Utopia); // (Temperance's note)
			Check(ref ok, StoryChain.Done.Contains("raft:VarunaPoint") && StoryChain.Done.Contains("raft:Temperance") && ChainTypes().Contains(ChunkPointType.Landmark_Utopia),
				"Varuna Point's and Temperance's notes: Utopia unlocked (" + Names(ChainTypes()) + ")");
			Check(ref ok, !ChainTypes().Contains(ChunkPointType.Landmark_Balboa) && !ChainTypes().Contains(ChunkPointType.Landmark_Vasagatan), "Balboa and Vasagatan never came on the Receiver");
			IslandWorldState.Save();
			if (ok) Log("PASS: story weave part2"); else Fail("story weave part2");
		}

		/// <summary>The test world's story back to Raft's own (after part2), the test's islands and plan removed.</summary>
		static IEnumerator StoryWeaveClean()
		{
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => x.HostName == WeaveHarbor || x.HostName == WeaveBeyond).Select(x => x.Id).ToList(), true);
			StoryChain.Reset();
			Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
			NoteBook.unlockedNoteBookIndexes.RemoveAll(i => notes.ContainsKey(i));
			NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
			WorldDirector.Done.Clear();
			WorldDirector.SetPlan(WorldPlan.RandomName, false);
			StoryChain.OnWorldRead();
			foreach (string f in new[] { WeaveHarbor, WeaveBeyond }) if (System.IO.File.Exists(IslandSpawner.PathFor(f))) System.IO.File.Delete(IslandSpawner.PathFor(f));
			if (System.IO.File.Exists(WorldPlan.PathFor(WeavePlan))) System.IO.File.Delete(WorldPlan.PathFor(WeavePlan));
			IslandWorldState.Save();
			yield return null;
			Log("PASS: story weave clean (the world's story is Raft's again)");
		}

		#region SC29 - a chain world, then a plain world

		[ConsoleCommand(name: "CIScChainCarry", docs: "Dev, world (host, 'CI ...'): SC29 - CIScChainCarry prep = this world gets a story chain (Balboa left out, a Receiver island) and the first notes; then the runner goes to the menu and loads a plain world: CIScChainCarry check = its Receiver has only Raft's own frequencies and its notes unlock as Raft's (AU48)")]
		public static void ScChainCarryCommand(string[] args)
		{
			StartTest(args != null && args.Length > 0 && args[0] == "check" ? ScChainCarryCheck() : ScChainCarryPrep());
		}

		static IEnumerator ScChainCarryPrep()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("scenario chain carry prep: host, in a world"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario chain carry prep: only in a test world 'CI ...'"); yield break; }
			bool ok = true;
			const string plan = "CI Chain Carry";
			WorldPlan.Parse(plan, "random = off\nstory = on\nstoryleaveout = Balboa\nrule = carry | type:sandbar | start | receiver:400 | A test signal. | Carry | after:Vasagatan | visit\n").Save();
			Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
			NoteBook.unlockedNoteBookIndexes.RemoveAll(i => notes.ContainsKey(i));
			NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
			StoryChain.Reset();
			Check(ref ok, ScSetPlan(plan, true), "the world gets a story chain");
			PlayNote(ChunkPointType.Landmark_RadioTower);
			PlayNote(ChunkPointType.Landmark_Vasagatan);
			PlayNote(ChunkPointType.Landmark_Balboa); // (Vasagatan's note: the chain's own step after it)
			StoryChain.Tick();
			Check(ref ok, StoryChain.Active && StoryChain.Unlocked.Contains("rule:carry") && ChainTypes().Any(t => (int)t >= StoryChain.ModTypeBase), "the chain world: the Receiver has the mod's frequency (" + Names(ChainTypes()) + ")");
			IslandWorldState.Save();
			yield return null;
			if (ok) Log("PASS: scenario chain carry prep"); else Fail("scenario chain carry prep");
		}

		static IEnumerator ScChainCarryCheck()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("scenario chain carry check: in a world"); yield break; }
			yield return new WaitForSeconds(4f);
			bool ok = true;
			int modTypes = NoteBook.unlockedChunkPointType.Count(t => (int)t >= StoryChain.ModTypeBase);
			int modFreqs = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.Count(f => f != null && (int)f.chunkPointType >= StoryChain.ModTypeBase) : 0;
			Check(ref ok, !StoryChain.Active, "the plain world loaded after the chain world has no story chain (active: " + StoryChain.Active + ", steps " + Steps(StoryChain.Steps) + ") - AU48");
			Check(ref ok, modTypes == 0 && modFreqs == 0, "... and its Receiver has only Raft's frequencies (mod islands unlocked " + modTypes + ", mod frequencies " + modFreqs + ")");
			bool hadRadio = NoteBook.unlockedChunkPointType.Contains(ChunkPointType.Landmark_RadioTower);
			Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
			int radioNote = notes.Where(kv => kv.Value == ChunkPointType.Landmark_RadioTower).Select(kv => kv.Key).FirstOrDefault();
			if (!hadRadio)
			{
				PlayNote(ChunkPointType.Landmark_RadioTower);
				Check(ref ok, NoteBook.unlockedChunkPointType.Contains(ChunkPointType.Landmark_RadioTower), "... and the Receiver's note unlocks the Radio Tower as in Raft");
				NoteBook.unlockedChunkPointType.Remove(ChunkPointType.Landmark_RadioTower);
			}
			if (System.IO.File.Exists(WorldPlan.PathFor("CI Chain Carry"))) System.IO.File.Delete(WorldPlan.PathFor("CI Chain Carry"));
			if (ok) Log("PASS: scenario chain carry check"); else Fail("scenario chain carry check");
		}

		#endregion

		#region SC30-SC32 - the chain changed mid-game; after Utopia; a missing replacing island

		[ConsoleCommand(name: "CIScChainEdit", docs: "Dev, world (host, 'CI ...'): SC30-SC32 - a step added after a done one unlocks (AU49); a rule after Utopia: Check says it can't come (AU51); the replacing island's file missing: the host is told and the chain goes on (AU50)")]
		public static void ScChainEditCommand() { StartTest(ScChainEditRoutine()); }

		static IEnumerator ScChainEditRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("scenario chain edit: host, in a world"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario chain edit: only in a test world 'CI ...'"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			var unlockedBefore = NoteBook.unlockedChunkPointType.ToList();
			var indexesBefore = NoteBook.unlockedNoteBookIndexes.ToList();
			string planBefore = WorldDirector.PlanName;
			var doneBefore = WorldDirector.Done.ToList();
			var made = new List<IslandWorldState.Entry>();
			try
			{
				Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
				Action clean = () =>
				{
					NoteBook.unlockedNoteBookIndexes.RemoveAll(i => notes.ContainsKey(i));
					NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
					StoryChain.Reset();
				};
				// (a) Vasagatan done, then the plan gets a step after Vasagatan
				clean();
				StoryChain.FromPlan(WorldPlan.Parse("CI edit a", "story = on\nstoryleaveout = Balboa\n"));
				PlayNote(ChunkPointType.Landmark_RadioTower);
				PlayNote(ChunkPointType.Landmark_Vasagatan);
				PlayNote(ChunkPointType.Landmark_Balboa); // (Vasagatan's note)
				Check(ref ok, StoryChain.Done.Contains("raft:Vasagatan") || ChainTypes().Contains(ChunkPointType.Landmark_CaravanIsland), "Vasagatan done (" + Names(ChainTypes()) + ")");
				StoryChain.FromPlan(WorldPlan.Parse("CI edit b", "story = on\nstoryleaveout = Balboa\nrule = x | type:sandbar | start | receiver:400 | | X | after:Vasagatan | visit\n"));
				StoryChain.Tick();
				Check(ref ok, StoryChain.Unlocked.Contains("rule:x"), "the plan changed mid-game: a step added after the done Vasagatan unlocks (" + Steps(StoryChain.Unlocked.OrderBy(s => s)) + ") - AU49");

				// (b) A rule after Utopia: Utopia never counts as done - Check must say the rule can't come
				WorldPlan after = WorldPlan.Parse("CI edit c", "story = on\nrule = y | type:sandbar | start | receiver:400 | | Y | after:Utopia | visit\n");
				List<PlanChecker.Finding> f = PlanChecker.Check(after, false, false, null);
				Check(ref ok, f.Any(x => (x.Level == PlanChecker.Level.Problem || x.Level == PlanChecker.Level.Warning) && x.Text.IndexOf("Utopia", StringComparison.OrdinalIgnoreCase) >= 0),
					"a rule after Utopia: Check warns that it can never come (" + string.Join(" | ", f.Where(x => x.Text.IndexOf("Utopia", StringComparison.OrdinalIgnoreCase) >= 0).Select(x => x.Level + ": " + x.Text).ToArray()) + ") - AU51");

				// (c) The replacing island's file is missing
				clean();
				const string gone = "ciscgonechain";
				if (System.IO.File.Exists(IslandSpawner.PathFor(gone))) System.IO.File.Delete(IslandSpawner.PathFor(gone));
				var notices = new List<string>();
				Application.LogCallback watch = (text, trace, type) => { if (text.Contains(gone)) notices.Add(text); };
				Application.logMessageReceived += watch;
				try
				{
					StoryChain.FromPlan(WorldPlan.Parse("CI edit d", "story = on\nrule = h | island:" + gone + " | start | receiver:400 | | Harbor | instead:Vasagatan | visit\n"));
					PlayNote(ChunkPointType.Landmark_RadioTower);
					PlayNote(ChunkPointType.Landmark_Vasagatan); // (the Radio Tower's note: the harbor's step)
					StoryChain.Tick();
					yield return new WaitForSeconds(2f);
					StoryChain.Tick();
					Check(ref ok, notices.Count > 0 || (WorldDirector.MissingIslands != null && WorldDirector.MissingIslands.Any(m => m.Contains(gone))), "the replacing island's file is missing: the host is told before anyone tunes (" + notices.Count + " messages) - AU50");
					int type = StoryChain.TypeOfRule("h");
					ChunkManager cm = ComponentManager<ChunkManager>.Value;
					if (cm != null && type >= StoryChain.ModTypeBase) cm.AddChunkPointForcibly((ChunkPointType)type);
					yield return new WaitForSeconds(3f);
					StoryChain.Tick();
					Check(ref ok, ChainTypes().Contains(ChunkPointType.Landmark_CaravanIsland) || ChainTypes().Contains(ChunkPointType.Landmark_Vasagatan),
						"... and the story goes on (Raft's Vasagatan instead, or the next island): " + Names(ChainTypes()) + " - AU50");
				}
				finally { Application.logMessageReceived -= watch; }
			}
			finally
			{
				ScRemove(made);
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				NoteBook.unlockedNoteBookIndexes.Clear(); NoteBook.unlockedNoteBookIndexes.AddRange(indexesBefore);
				NoteBook.unlockedChunkPointType.Clear(); NoteBook.unlockedChunkPointType.AddRange(unlockedBefore);
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
			}
			if (ok) Log("PASS: scenario chain edit"); else Fail("scenario chain edit");
		}

		#endregion
	}
}
