using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of the story chain (StoryChain): a world plan's own islands in Raft's Receiver chain, Raft's story off,
	/// story islands left out or replaced, islands on their own Receiver frequency - the rules alone (CIStoryChainUnit),
	/// in a world through Raft's own unlock and Receiver calls (CIStoryChainWorld), the plan editor's story controls
	/// (CIStoryChainEditor), and for every player (CIStoryChainCheck, mpstorychain.ps1).
	/// </summary>
	public static partial class DevTests
	{
		static readonly ChunkPointType[] Chain = StoryOrder.Chain;

		static string Steps(IEnumerable<string> steps) { return string.Join(",", steps.ToArray()); }

		static string RaftSteps(params string[] keys) { return string.Join(",", keys.Select(k => k.Contains(":") ? k : "raft:" + k).ToArray()); }

		[ConsoleCommand(name: "CIStoryChainUnit", docs: "Dev, anywhere: the story chain's rules without a world - rule lines with a story place, done-when and the Receiver both ways (old lines still read); plan lines story = off and storyleaveout; the chain built for Raft's own story, islands left out, in place of one, after one, first, after another plan island, the story off; the templates' chains; the number a note shows; the blueprints a left-out island had")]
		public static void StoryChainUnitCommand()
		{
			bool ok = true;
			// Rule lines
			IntroRule r = IntroRule.Parse("detour | type:camp | start | receiver:650 | A signal. | Old camp | after:Caravan Town | step:2");
			Check(ref ok, r != null && r.Where == "receiver" && Mathf.Approximately(r.Distance, 650f) && r.StoryPlace == "after:CaravanTown" && r.StoryDone == "step:2" && r.Special && r.InStory,
				"a rule with a story place, done-when and the Receiver read: " + (r != null ? r.Where + " " + r.Distance + " / " + r.StoryPlace + " / " + r.StoryDone : "null"));
			IntroRule back = r != null ? IntroRule.Parse(r.ToLine()) : null;
			Check(ref ok, back != null && back.ToLine() == r.ToLine(), "... and written back the same: " + (r != null ? r.ToLine() : ""));
			IntroRule old = IntroRule.Parse("camp | type:camp | start | ahead:350 | Smoke. | Old camp");
			Check(ref ok, old != null && old.StoryPlace == "" && old.StoryDone == "" && !old.Special && old.ToLine() == "camp | type:camp | start | ahead:350 | Smoke. | Old camp", "an older rule line (six parts) reads and writes as before");
			IntroRule sail = IntroRule.Parse("s | type:wreck | km:2 | sailing:300 | | ");
			Check(ref ok, sail != null && sail.Where == "sailing" && sail.Special && !sail.InStory, "a rule by chance while sailing");
			Check(ref ok, IntroRule.NormalPlace("instead:Caravan Town") == "instead:CaravanTown" && IntroRule.NormalPlace("instead:nowhere") == "" && IntroRule.NormalPlace("after:camp") == "after:camp" &&
				IntroRule.NormalPlace("FIRST") == "first" && IntroRule.NormalPlace("after:varuna") == "after:VarunaPoint", "story places: Raft's islands by any of their names, other names are plan islands, 'instead' only for Raft's");
			Check(ref ok, IntroRule.NormalDone("Quest") == "quest" && IntroRule.NormalDone("zone:gate") == "zone:gate" && IntroRule.NormalDone("nonsense") == "", "done-when read");

			// Plan lines
			WorldPlan p = WorldPlan.Parse("t", "story = off\nstoryleaveout = Balboa, varuna, nowhere\nrule = a | type:camp | start | receiver:600 | | | first | quest\n");
			Check(ref ok, !p.RaftStory && p.LeaveOut.SetEquals(new[] { "Balboa", "VarunaPoint" }) && p.ChangesStory && p.HasStory, "a plan with Raft's story off and two islands left out (an unknown one ignored)");
			WorldPlan p2 = WorldPlan.Parse("t", p.ToText());
			Check(ref ok, !p2.RaftStory && p2.LeaveOut.SetEquals(p.LeaveOut) && p2.Rules.Count == 1 && p2.Rules[0].ToLine() == p.Rules[0].ToLine(), "... written and read again the same");
			WorldPlan plain = WorldPlan.Parse("t", "rule = camp | type:camp | start | ahead:350 | | \n");
			Check(ref ok, plain.RaftStory && plain.LeaveOut.Count == 0 && !plain.ChangesStory && !plain.HasStory, "an older plan leaves Raft's story alone");

			// Chains
			var none = new HashSet<string>();
			string all = RaftSteps("RadioTower", "Vasagatan", "Balboa", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia");
			Check(ref ok, Steps(StoryChain.BuildSteps(true, none, new IntroRule[0])) == all, "Raft's own chain: " + all);
			Check(ref ok, Steps(StoryChain.BuildSteps(true, new HashSet<string> { "Balboa" }, new IntroRule[0])) == RaftSteps("RadioTower", "Vasagatan", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia"), "Balboa left out");
			Func<string, string, IntroRule> rule = (id, place) => new IntroRule { Id = id, What = "type", WhatArg = "camp", Where = "receiver", StoryPlace = IntroRule.NormalPlace(place) };
			string got = Steps(StoryChain.BuildSteps(true, none, new[] { rule("x", "instead:Balboa") }));
			Check(ref ok, got == RaftSteps("RadioTower", "Vasagatan", "rule:x", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia"), "an island in Balboa's place: " + got);
			got = Steps(StoryChain.BuildSteps(true, none, new[] { rule("a", "after:Vasagatan"), rule("b", "first"), rule("c", "after:a"), rule("d", "after:a"), rule("e", "after:Utopia") }));
			Check(ref ok, got == RaftSteps("rule:b", "RadioTower", "Vasagatan", "rule:a", "rule:c", "rule:d", "Balboa", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia", "rule:e"),
				"islands first, after Vasagatan, after another plan island (two, in the plan's order), after Utopia: " + got);
			got = Steps(StoryChain.BuildSteps(false, none, new[] { rule("one", "first"), rule("two", "after:one"), rule("three", "after:two"), rule("mid", "instead:Tangaroa") }));
			Check(ref ok, got == "rule:one,rule:two,rule:three,rule:mid", "Raft's story off: only the plan's islands (one in Tangaroa's place keeps that place): " + got);
			got = Steps(StoryChain.BuildSteps(true, none, new[] { rule("lost", "after:nobody") }));
			Check(ref ok, got.EndsWith(",rule:lost"), "after an island that isn't in the story: at the end");

			// Templates
			var tpl = new Dictionary<string, string>
			{
				{ CIReceiver, "rule:camp,rule:islets,rule:beast,rule:treasure" },
				{ "Detour in Raft's story", RaftSteps("RadioTower", "Vasagatan", "rule:detour", "Balboa", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia") },
				{ "Balboa replaced", RaftSteps("RadioTower", "Vasagatan", "rule:forest", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia") },
			};
			foreach (var kv in tpl)
			{
				WorldPlan t = kv.Key == CIReceiver ? CIPlanOf(kv.Key) : WorldPlanTemplates.Get(kv.Key);
				string s = t != null ? Steps(StoryChain.BuildSteps(t.RaftStory, t.LeaveOut, t.Rules)) : "missing";
				Check(ref ok, s == kv.Value && t.Rules.All(x => x.Where == "receiver"), "template '" + kv.Key + "': " + s);
			}

			// The number a note shows (a chain set by hand for a moment)
			var stepsBefore = StoryChain.Steps.ToList();
			var freqBefore = StoryChain.Frequencies.ToList();
			try
			{
				StoryChain.Steps.Clear();
				StoryChain.Steps.AddRange(StoryChain.BuildSteps(true, new HashSet<string> { "Tangaroa" }, new[] { rule("detour", "instead:Balboa") }));
				StoryChain.Frequencies.Clear();
				StoryChain.Frequencies.Add(new KeyValuePair<string, int[]>("detour", new[] { 1, 2, 3, 4 }));
				Check(ref ok, StoryChain.FrequencyTextForNote(ChunkPointType.Landmark_Balboa) == "#1234", "Vasagatan's note (Raft's way to Balboa) shows the frequency of the island in Balboa's place: " + StoryChain.FrequencyTextForNote(ChunkPointType.Landmark_Balboa));
				// (ROADMAP LM1: the label next to the number names where the note now leads)
				string toBalboa = StoryChain.NextNameForNote(ChunkPointType.Landmark_Balboa), toTangaroa = StoryChain.NextNameForNote(ChunkPointType.Landmark_Tangaroa);
				Check(ref ok, toBalboa != null && toBalboa.Contains("detour") && toTangaroa == "Varuna Point", "the notes' labels name where they lead now: Raft's way to Balboa -> " + toBalboa + ", to Tangaroa -> " + toTangaroa);
				Check(ref ok, StoryChain.FrequencyTextForNote(ChunkPointType.Landmark_VarunaPoint) == "#----", "Tangaroa is left out: its own note (Raft's way to Varuna Point) is never found and shows no number: " + StoryChain.FrequencyTextForNote(ChunkPointType.Landmark_VarunaPoint));
				if (RecieverFrequency.AllFrequencies != null)
				{
					RecieverFrequency varuna = RecieverFrequency.AllFrequencies.FirstOrDefault(f => f != null && f.chunkPointType == ChunkPointType.Landmark_VarunaPoint);
					Check(ref ok, varuna != null && StoryChain.FrequencyTextForNote(ChunkPointType.Landmark_Tangaroa) == varuna.ToString(), "Caravan Town's note (Raft's way to Tangaroa, left out) shows Varuna Point's frequency " + (varuna != null ? varuna.ToString() : "?"));
				}
				else Log("  (Raft's frequencies aren't made yet here: the numbers of Raft's own islands are checked in a world)");
			}
			finally
			{
				StoryChain.Steps.Clear(); StoryChain.Steps.AddRange(stepsBefore);
				StoryChain.Frequencies.Clear(); StoryChain.Frequencies.AddRange(freqBefore);
			}

			// What a left-out island had
			List<string> balboa = StoryChain.NeededBlueprintsOn("Balboa");
			Check(ref ok, balboa.Contains("Machete") && balboa.Contains("Fueltank"), "Balboa holds the story's " + string.Join(", ", balboa.ToArray()));
			Check(ref ok, StoryChain.NeededBlueprintsOn("Temperance").Count == 0, "Temperance holds nothing the story needs");
			if (ok) Log("PASS: story chain rules"); else Fail("story chain rules");
		}

		[ConsoleCommand(name: "CIStoryChainWorld", docs: "Dev, world (host): a plan's story chain in a world, through Raft's own calls - Balboa left out and a sandbar on its own Receiver frequency after Vasagatan: the Receiver's note unlocks the Radio Tower, its note Vasagatan, Vasagatan's the sandbar (not Balboa: its frequency leads nowhere) with a banner, a journal page and the number on the note; Raft's frequency list keeps the mod's through a save's restore; the Receiver tuned to it (ChunkManager.AddChunkPointForcibly) brings the island once; players reaching it unlock Caravan Town with a banner; Caravan's note Tangaroa; the world file's lines give the same chain back. What the world had is put back after")]
		public static void StoryChainWorldCommand() { StartTest(StoryChainWorldRoutine()); }

		static List<ChunkPointType> ChainTypes() { return NoteBook.unlockedChunkPointType.Where(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase).ToList(); }

		static string Names(IEnumerable<ChunkPointType> types) { return string.Join(",", types.Select(t => (int)t >= StoryChain.ModTypeBase ? "#" + (int)t : StoryOrder.Name(t)).ToArray()); }

		static IEnumerator StoryChainWorldRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("story chain world: host, in a world"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			var unlockedBefore = NoteBook.unlockedChunkPointType.ToList();
			var indexesBefore = NoteBook.unlockedNoteBookIndexes.ToList();
			RecieverFrequency[] freqBefore = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.ToArray() : null;
			Network_Player me = RAPI.GetLocalPlayer();
			Vector3 home = me != null ? me.transform.position : Vector3.zero;
			IslandWorldState.Entry island = null;
			try
			{
				// A clean slate: no story note found, nothing of Raft's story unlocked
				Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
				NoteBook.unlockedNoteBookIndexes.RemoveAll(i => notes.ContainsKey(i));
				NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
				StoryChain.Reset();
				WorldPlan plan = WorldPlan.Parse("CI chain", "storyleaveout = Balboa\nrule = detour | type:sandbar | start | receiver:400 | A test signal. | Detour | after:Vasagatan | visit\n");
				StoryChain.FromPlan(plan);
				Check(ref ok, StoryChain.Active && Steps(StoryChain.Steps) == RaftSteps("RadioTower", "Vasagatan", "rule:detour", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia"), "the world's chain: " + Steps(StoryChain.Steps));
				Check(ref ok, ChainTypes().Count == 0, "nothing unlocked yet: " + Names(ChainTypes()));

				NoteBook.UnlockFrequency(ChunkPointType.Landmark_RadioTower);
				Check(ref ok, Names(ChainTypes()) == "Radio Tower", "the Receiver's note: " + Names(ChainTypes()));
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_Vasagatan);
				Check(ref ok, Names(ChainTypes()) == "Radio Tower,Vasagatan", "the Radio Tower's note: " + Names(ChainTypes()));

				string onNote = StoryOrder.FrequencyText(ChunkPointType.Landmark_Balboa);
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_Balboa); // (Vasagatan's note: Raft's way to Balboa)
				Check(ref ok, !ChainTypes().Contains(ChunkPointType.Landmark_Balboa) && StoryChain.Done.Contains("raft:Vasagatan") && StoryChain.Unlocked.Contains("rule:detour"),
					"Vasagatan's note: Vasagatan done, the sandbar's step unlocked, Balboa not (" + Names(ChainTypes()) + ")");
				StoryChain.Tick();
				int type = StoryChain.TypeOfRule("detour");
				string freq = StoryChain.FrequencyOf("detour");
				Check(ref ok, type == StoryChain.ModTypeBase && ChainTypes().Contains((ChunkPointType)type), "the sandbar's frequency " + freq + " unlocked (island type " + type + "): " + Names(ChainTypes()));
				Check(ref ok, onNote == freq, "the number on Vasagatan's note is the sandbar's: " + onNote);
				Check(ref ok, StoryChain.LastBanner != null && StoryChain.LastBanner.Contains(freq), "a banner: " + StoryChain.LastBanner);
				int[] digits = StoryChain.Frequencies[0].Value;
				var dialled = new RecieverFrequency((ChunkPointType)type, false) { numbers = (int[])digits.Clone() };
				Check(ref ok, dialled.IsValid(), "dialling " + freq + " finds a valid frequency on the Receiver");
				RecieverFrequency balboa = RecieverFrequency.AllFrequencies.FirstOrDefault(f => f != null && f.chunkPointType == ChunkPointType.Landmark_Balboa);
				Check(ref ok, balboa != null && !balboa.IsValid(), "Balboa's frequency " + (balboa != null ? balboa.ToString() : "?") + " leads nowhere");
				Check(ref ok, RecieverFrequency.AllFrequencies.Count(f => f != null && (int)f.chunkPointType < StoryChain.ModTypeBase) == 8, "Raft's eight frequencies are still there");

				// Raft saving and restoring its frequencies (a save loaded): the mod's stays
				var rgd = new RGD_RecieverFrequencies();
				rgd.RestoreFrequencies();
				Check(ref ok, RecieverFrequency.AllFrequencies.Any(f => f != null && (int)f.chunkPointType == type && f.numbers.SequenceEqual(digits)) && RecieverFrequency.AllFrequencies.Length == 9,
					"after Raft restores its frequencies the mod's is there once more (" + RecieverFrequency.AllFrequencies.Length + " in all)");

				// The Receiver tuned to it: Raft asks for an island of that type, the mod brings the sandbar
				ChunkManager cm = ComponentManager<ChunkManager>.Value;
				ChunkPoint cp = cm.AddChunkPointForcibly((ChunkPointType)type);
				Check(ref ok, cp == null, "Raft adds no island of its own for the mod's frequency");
				yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == "detour" && e.Root != null), 60f);
				island = IslandWorldState.Islands.FirstOrDefault(e => e.Rule == "detour");
				Check(ref ok, island != null && island.Root != null && StoryChain.Brought.Contains("detour"), "the sandbar came" + (island != null ? " (" + island.HostName + ")" : ""));
				cm.AddChunkPointForcibly((ChunkPointType)type);
				yield return new WaitForSeconds(2f);
				Check(ref ok, IslandWorldState.Islands.Count(e => e.Rule == "detour") == 1, "tuned again: still one");

				// Players reach it: done, Caravan Town unlocked with a banner
				Check(ref ok, !StoryChain.Done.Contains("rule:detour") && !ChainTypes().Contains(ChunkPointType.Landmark_CaravanIsland), "before anyone reaches it, Caravan Town isn't unlocked");
				if (island != null && me != null) yield return PutPlayer(me, island.Position + new Vector3(0f, 15f, 0f), false);
				yield return WaitFor(() => StoryChain.Done.Contains("rule:detour"), 20f);
				RecieverFrequency caravan = RecieverFrequency.AllFrequencies.FirstOrDefault(f => f != null && f.chunkPointType == ChunkPointType.Landmark_CaravanIsland);
				Check(ref ok, StoryChain.Done.Contains("rule:detour") && ChainTypes().Contains(ChunkPointType.Landmark_CaravanIsland), "players at the sandbar: done, Caravan Town unlocked (" + Names(ChainTypes()) + ")");
				Check(ref ok, caravan != null && StoryChain.LastBanner != null && StoryChain.LastBanner.Contains(caravan.ToString()), "the banner gives Caravan Town's frequency: " + StoryChain.LastBanner);
				Check(ref ok, caravan != null && StoryBook.Pages.Any(p => p.Key == "storyfreq:raft:CaravanTown" && p.Text.Contains(caravan.ToString())), "the journal keeps Caravan Town's frequency too (no note of Raft's gives it now)");
				Check(ref ok, freq != null && StoryBook.Pages.Any(p => p.Key == "storyfreq:detour" && p.Text.Contains(freq)), "and the sandbar's");
				if (me != null) yield return PutPlayer(me, home, false);

				NoteBook.UnlockFrequency(ChunkPointType.Landmark_Tangaroa); // (Caravan's note)
				Check(ref ok, ChainTypes().Contains(ChunkPointType.Landmark_Tangaroa) && StoryChain.Done.Contains("raft:CaravanTown"), "Caravan Town's note: Tangaroa (" + Names(ChainTypes()) + ")");

				// The world file's lines give the same back (a save loaded, or another host)
				List<string> lines = StoryChain.WriteLines().ToList();
				string stateBefore = Steps(StoryChain.Steps) + "/" + Steps(StoryChain.Unlocked.OrderBy(x => x)) + "/" + Steps(StoryChain.Done.OrderBy(x => x)) + "/" + Steps(StoryChain.Brought) + "/" + StoryChain.FrequencyOf("detour");
				string typesBefore = Names(ChainTypes().OrderBy(t => t));
				StoryChain.Reset();
				NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
				foreach (string l in lines) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				StoryChain.OnWorldRead();
				string stateAfter = Steps(StoryChain.Steps) + "/" + Steps(StoryChain.Unlocked.OrderBy(x => x)) + "/" + Steps(StoryChain.Done.OrderBy(x => x)) + "/" + Steps(StoryChain.Brought) + "/" + StoryChain.FrequencyOf("detour");
				Check(ref ok, stateAfter == stateBefore && Names(ChainTypes().OrderBy(t => t)) == typesBefore, "read back from the world file's " + lines.Count + " lines: the same chain, steps, frequency and unlocked islands (" + typesBefore + ")");
			}
			finally
			{
				if (island != null) IslandWorldState.RemoveIds(new List<int> { island.Id }, true);
				if (me != null) me.transform.position = home;
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				NoteBook.unlockedNoteBookIndexes.Clear(); NoteBook.unlockedNoteBookIndexes.AddRange(indexesBefore);
				NoteBook.unlockedChunkPointType.Clear(); NoteBook.unlockedChunkPointType.AddRange(unlockedBefore);
				if (freqBefore != null) RecieverFrequency.AllFrequencies = freqBefore;
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
			}
			if (ok) Log("PASS: story chain in a world"); else Fail("story chain in a world");
		}

		[ConsoleCommand(name: "CIStoryChainAdventure", docs: "Dev, world (host): a new adventure instead of Raft's story - Raft's story off, an island first in the chain that comes by chance while sailing, one after it that comes ahead, and a Receiver island outside the story: the Receiver's note unlocks none of Raft's islands; the first is unlocked from the start but comes only after some sailing; players reaching it bring the second; the island outside the story has its frequency from the start. What the world had is put back after")]
		public static void StoryChainAdventureCommand() { StartTest(StoryChainAdventureRoutine()); }

		static IEnumerator StoryChainAdventureRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("story chain adventure: host, in a world"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			var unlockedBefore = NoteBook.unlockedChunkPointType.ToList();
			var indexesBefore = NoteBook.unlockedNoteBookIndexes.ToList();
			RecieverFrequency[] freqBefore = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.ToArray() : null;
			float sailedBefore = WorldDirector.Sailed;
			Network_Player me = RAPI.GetLocalPlayer();
			Vector3 home = me != null ? me.transform.position : Vector3.zero;
			try
			{
				Dictionary<int, ChunkPointType> notes = StoryOrder.FrequencyNotes();
				NoteBook.unlockedNoteBookIndexes.RemoveAll(i => notes.ContainsKey(i));
				NoteBook.unlockedChunkPointType.RemoveAll(t => Chain.Contains(t) || (int)t >= StoryChain.ModTypeBase);
				StoryChain.Reset();
				WorldPlan plan = WorldPlan.Parse("CI adventure", "story = off\n" +
					"rule = first | type:sandbar | start | sailing:300 | Something lies ahead... | Sail | first | visit\n" +
					"rule = second | type:sandbar | start | ahead:350 | | Next | after:first | visit\n" +
					"rule = free | type:sandbar | km:0 | receiver:400 | | Free\n");
				StoryChain.FromPlan(plan);
				Check(ref ok, StoryChain.Active && Steps(StoryChain.Steps) == "rule:first,rule:second" && StoryChain.Rules.Count == 3, "the chain without Raft's story: " + Steps(StoryChain.Steps) + " (and one island outside it)");
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_RadioTower);
				StoryChain.Tick();
				Check(ref ok, !ChainTypes().Any(t => Chain.Contains(t)), "the Receiver's note unlocks none of Raft's islands: " + Names(ChainTypes()));
				Check(ref ok, StoryOrder.FrequencyText(ChunkPointType.Landmark_RadioTower) == "#----", "... and shows no number (the first island comes by sailing): " + StoryOrder.FrequencyText(ChunkPointType.Landmark_RadioTower));
				int freeType = StoryChain.TypeOfRule("free");
				Check(ref ok, freeType >= StoryChain.ModTypeBase && ChainTypes().Contains((ChunkPointType)freeType) && StoryChain.Fired.Contains("free"), "the Receiver island outside the story has its frequency " + StoryChain.FrequencyOf("free") + " from the start");
				Check(ref ok, StoryChain.Unlocked.Contains("rule:first") && StoryChain.Fired.Contains("first") && !StoryChain.Brought.Contains("first") && !StoryChain.Unlocked.Contains("rule:second"),
					"the first island is unlocked from the start, not here yet; the second waits");
				yield return new WaitForSeconds(3f);
				Check(ref ok, !IslandWorldState.Islands.Any(e => e.Rule == "first"), "without sailing, it doesn't come");
				WorldDirector.Sailed = sailedBefore + 2000f;
				StoryChain.Tick();
				yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == "first" && e.Root != null), 60f);
				IslandWorldState.Entry first = IslandWorldState.Islands.FirstOrDefault(e => e.Rule == "first");
				Check(ref ok, first != null && first.Root != null && StoryChain.Brought.Contains("first"), "after 2 km of sailing it came up ahead" + (first != null ? " (" + first.HostName + ")" : ""));
				if (first != null && me != null) yield return PutPlayer(me, first.Position + new Vector3(0f, 15f, 0f), false);
				yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == "second" && e.Root != null), 60f);
				Check(ref ok, StoryChain.Done.Contains("rule:first") && StoryChain.Unlocked.Contains("rule:second") && IslandWorldState.Islands.Any(e => e.Rule == "second"),
					"players reached the first: the second is unlocked and came ahead");
				Check(ref ok, !ChainTypes().Any(t => Chain.Contains(t)), "still none of Raft's islands: " + Names(ChainTypes()));
			}
			finally
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(e => e.Rule == "first" || e.Rule == "second" || e.Rule == "free").Select(e => e.Id).ToList(), true);
				if (me != null) me.transform.position = home;
				WorldDirector.Sailed = sailedBefore;
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				NoteBook.unlockedNoteBookIndexes.Clear(); NoteBook.unlockedNoteBookIndexes.AddRange(indexesBefore);
				NoteBook.unlockedChunkPointType.Clear(); NoteBook.unlockedChunkPointType.AddRange(unlockedBefore);
				if (freqBefore != null) RecieverFrequency.AllFrequencies = freqBefore;
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
			}
			if (ok) Log("PASS: story chain adventure"); else Fail("story chain adventure");
		}

		[ConsoleCommand(name: "CIStoryChainEdited", docs: "Dev, world (host, a new test world 'CI ...' without custom islands): the player edits the world's plan in World Plans and the world is read again as when loading it: leaving one more story island out (no rule changed) counts as an edit and the chain follows it, what was unlocked stays; a story island added to the plan comes into the chain after its place, the Receiver island keeps its frequency. The plan file is deleted after")]
		public static void StoryChainEditedCommand() { StartTest(StoryChainEditedRoutine()); }

		static IEnumerator StoryChainEditedRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ") || IslandWorldState.Islands.Count > 0)
			{ Fail("story chain edited: host, in a new test world 'CI ...' without custom islands"); yield break; }
			bool ok = true;
			const string name = "CI story edit";
			string path = WorldPlan.PathFor(name);
			string planBefore = WorldDirector.PlanName;
			float sailedBefore = WorldDirector.Sailed;
			try
			{
				File.WriteAllText(path, "storyleaveout = Balboa\nrule = detour | type:sandbar | start | receiver:400 | | Detour | after:Vasagatan | visit\n");
				WorldDirector.SetPlan(name, false);
				WorldDirector.PlanOwner = 0;
				StoryChain.FromPlan(WorldDirector.Plan);
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_RadioTower);
				string digits = StoryChain.FrequencyOf("detour");
				WorldDirector.Sailed = Mathf.Max(WorldDirector.Sailed, 10f); // (read again as a saved world, not a new one)
				IslandWorldState.Save();
				Check(ref ok, Steps(StoryChain.Steps) == RaftSteps("RadioTower", "Vasagatan", "rule:detour", "CaravanTown", "Tangaroa", "VarunaPoint", "Temperance", "Utopia") && StoryChain.Unlocked.Contains("raft:RadioTower"),
					"the world's chain from the plan, the Radio Tower unlocked: " + Steps(StoryChain.Steps));

				// Only a story setting changed (no rule): still an edit
				File.WriteAllText(path, "storyleaveout = Balboa, Tangaroa\nrule = detour | type:sandbar | start | receiver:400 | | Detour | after:Vasagatan | visit\n");
				IslandWorldState.OnWorldLoaded();
				yield return null;
				Check(ref ok, WorldDirector.PlanWasEdited && !StoryChain.Steps.Contains("raft:Tangaroa") && StoryChain.Unlocked.Contains("raft:RadioTower") && ChainTypes().Contains(ChunkPointType.Landmark_RadioTower),
					"Tangaroa left out in the plan: the world's chain follows, the Radio Tower still unlocked (" + Steps(StoryChain.Steps) + ")");

				// A story island added
				IslandWorldState.Save();
				File.WriteAllText(path, "storyleaveout = Balboa, Tangaroa\nrule = detour | type:sandbar | start | receiver:400 | | Detour | after:Vasagatan | visit\n" +
					"rule = extra | type:sandbar | start | receiver:500 | | Extra | after:detour | visit\n");
				IslandWorldState.OnWorldLoaded();
				yield return null;
				Check(ref ok, WorldDirector.PlanWasEdited && Steps(StoryChain.Steps) == RaftSteps("RadioTower", "Vasagatan", "rule:detour", "rule:extra", "CaravanTown", "VarunaPoint", "Temperance", "Utopia"),
					"an island added after the detour: " + Steps(StoryChain.Steps));
				Check(ref ok, StoryChain.FrequencyOf("detour") == digits && StoryChain.FrequencyOf("extra") != null && StoryChain.FrequencyOf("extra") != digits,
					"the detour keeps its frequency " + digits + ", the new island gets its own " + StoryChain.FrequencyOf("extra"));

				// Not edited: read again, the same
				IslandWorldState.Save();
				IslandWorldState.OnWorldLoaded();
				yield return null;
				Check(ref ok, !WorldDirector.PlanWasEdited && Steps(StoryChain.Steps).Contains("rule:extra"), "read again unchanged: not an edit, the same chain");
			}
			finally
			{
				if (File.Exists(path)) File.Delete(path);
				WorldDirector.SetPlan(planBefore, false);
				StoryChain.FromPlan(WorldDirector.Plan);
				WorldDirector.Sailed = sailedBefore;
				IslandWorldState.Save();
			}
			if (ok) Log("PASS: story chain edited"); else Fail("story chain edited");
		}

		[ConsoleCommand(name: "CIStoryChainEditor", docs: "Dev, editor: the World plans window's story controls clicked as a builder does, on a test plan 'CI story plan' (deleted after): Raft's story off and on, Balboa left out and back, a rule put in Balboa's place from the list, the Receiver chosen for it; Check shows the chain and the tips (the Radio Tower first, Utopia last, Balboa's blueprints); saved and read back")]
		public static void StoryChainEditorCommand() { StartTest(StoryChainEditorRoutine()); }

		static IEnumerator StoryChainEditorRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("story chain editor: in the editor"); yield break; }
			bool ok = true;
			const string name = "CI story plan";
			string path = WorldPlan.PathFor(name);
			try
			{
				new WorldPlan { Name = name, Rules = new List<IntroRule> { new IntroRule { Id = "mine", What = "type", WhatArg = "forest" } } }.Save();
				WorldPlanWindow.Open(name);
				yield return null;
				// (Raft's story row is on the Settings tab since 5b; the rule card on Other islands)
				WorldPlanWindow.ShowTab(WorldPlanWindow.TabSettings);
				yield return null;
				WorldPlan plan = WorldPlanWindow.Plan;
				Button story = WorldPlanWindow.StoryButton, balboa = WorldPlanWindow.StoryIslandButton("Balboa");
				Check(ref ok, plan != null && plan.Name == name && story != null && balboa != null && story.gameObject.activeInHierarchy, "the window shows the plan with Raft's story row");
				story.onClick.Invoke();
				yield return null;
				Check(ref ok, !plan.RaftStory && !balboa.interactable && UIKit.LabelOf(story).text.EndsWith("off"), "Raft's story off: the islands' buttons can't be clicked (" + UIKit.LabelOf(story).text + ")");
				story.onClick.Invoke();
				yield return null;
				balboa.onClick.Invoke();
				yield return null;
				Check(ref ok, plan.RaftStory && plan.LeaveOut.Contains("Balboa"), "Balboa left out");
				balboa.onClick.Invoke();
				yield return null;
				Check(ref ok, plan.LeaveOut.Count == 0, "... and back");
				WorldPlanWindow.SetStoryPlace(0, "instead:Balboa");
				yield return null;
				plan.Rules[0].Where = "receiver";
				Button check = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.gameObject.activeInHierarchy && UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == "Check");
				if (check != null) check.onClick.Invoke();
				yield return null;
				string shown = WorldPlanWindow.LastCheck ?? "";
				Check(ref ok, shown.Contains("Vasagatan > 'mine' > Caravan Town") && shown.Contains("Machete"), "Check shows the chain with the island in Balboa's place and what Balboa had: " + shown.Replace("\n", " | "));
				// (in Balboa's place the rule is on the Main story line tab, not Other islands)
				WorldPlanWindow.ShowTab(WorldPlanWindow.TabStory);
				yield return null;
				Button place = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == "StoryPlace");
				Check(ref ok, place != null && UIKit.LabelOf(place).text.StartsWith("in place of Balboa", StringComparison.OrdinalIgnoreCase) && UIKit.LabelOf(balboa).text == "Balboa (yours)", "the rule card's story button and the story row say so: " + (place != null ? UIKit.LabelOf(place).text : "none") + " / " + UIKit.LabelOf(balboa).text);
				Button save = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.gameObject.activeInHierarchy && UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == "Save");
				if (save != null) save.onClick.Invoke();
				yield return null;
				WorldPlan read = WorldPlan.Load(name);
				Check(ref ok, read != null && read.Rules.Count == 1 && read.Rules[0].StoryPlace == "instead:Balboa" && read.Rules[0].Where == "receiver" && read.RaftStory,
					"saved and read back: " + (read != null ? read.Rules[0].ToLine() : "missing"));
				WorldPlanWindow.Close();
			}
			finally
			{
				WorldPlanWindow.Close();
				if (File.Exists(path)) File.Delete(path);
			}
			if (ok) Log("PASS: story chain editor"); else Fail("story chain editor");
		}

		[ConsoleCommand(name: "CIStoryChainShot", docs: "Dev, editor: the World plans window on a plan (a template written as 'CI story shot', deleted after), Check clicked, a screenshot shot_story_chain.png for the guide: CIStoryChainShot <template name>")]
		public static void StoryChainShotCommand(string[] args) { StartTest(StoryChainShotRoutine(args != null ? string.Join(" ", args) : "Balboa replaced")); }

		static IEnumerator StoryChainShotRoutine(string template)
		{
			if (!DynamicIslands.InEditor()) { Fail("story chain shot: in the editor"); yield break; }
			const string name = "CI story shot";
			WorldPlan t = WorldPlanTemplates.Get(template);
			if (t == null) { Fail("story chain shot: no template '" + template + "'"); yield break; }
			t.Name = name;
			t.Save();
			try
			{
				WorldPlanWindow.Open(name);
				yield return null;
				Button check = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.gameObject.activeInHierarchy && UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == "Check");
				if (check != null) check.onClick.Invoke();
				yield return new WaitForSeconds(0.5f);
				Screenshot(new[] { "story_chain" });
				yield return new WaitForSeconds(1.5f);
			}
			finally
			{
				WorldPlanWindow.Close();
				string path = WorldPlan.PathFor(name);
				if (File.Exists(path)) File.Delete(path);
			}
			Log("PASS: story chain shot");
		}

		[ConsoleCommand(name: "CIStoryChainPlan", docs: "Dev, world (host): gives this world a story chain from a template (WorldPlanTemplates) as WorldPlan <name> would, for the two-player test: CIStoryChainPlan <template name> - only in a test world named 'CI ...'")]
		public static void StoryChainPlanCommand(string[] args)
		{
			if (!Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("story chain plan: host, in a test world 'CI ...'"); return; }
			string which = args != null ? string.Join(" ", args) : "";
			// ("ci": the world test's chain - Balboa left out, a sandbar on its own frequency after Vasagatan, done when reached)
			WorldPlan t = which == "ci" ? WorldPlan.Parse("CI chain", CIChainPlan) : WorldPlanTemplates.Get(which);
			if (t == null) { Fail("story chain plan: no template '" + (args != null ? string.Join(" ", args) : "") + "'"); return; }
			StoryChain.FromPlan(t);
			IslandWorldState.Save();
			Log("Story chain: " + StoryChain.Describe().Replace("\n", " | "));
			Log("PASS: story chain plan");
		}

		const string CIChainPlan = "storyleaveout = Balboa\nrule = detour | type:sandbar | start | receiver:400 | A test signal. | Detour | after:Vasagatan | visit\n";

		[ConsoleCommand(name: "CIStoryChainTune", docs: "Dev, world (host): the host's Receiver tuned to a plan island's frequency asks Raft for its island (ChunkManager.AddChunkPointForcibly, as Reciever.Update does whoever dialled it): CIStoryChainTune <rule id>")]
		public static void StoryChainTuneCommand(string[] args)
		{
			int type = StoryChain.TypeOfRule(args != null && args.Length > 0 ? args[0] : "");
			if (!Raft_Network.IsHost || type < 0) { Fail("story chain tune: host, a rule on the Receiver"); return; }
			ComponentManager<ChunkManager>.Value.AddChunkPointForcibly((ChunkPointType)type);
			Log("Tuned to " + StoryChain.FrequencyOf(args[0]) + " (island type " + type + ")");
			Log("PASS: story chain tune");
		}

		[ConsoleCommand(name: "CIStoryChainCheck", docs: "Dev, in game (either player): logs STORYCHAIN lines - the chain, the steps unlocked, the notebook's story islands, the mod's frequencies in Raft's list and the number on each note - to compare the host's with a player's; CIStoryChainCheck <expected unlocked islands, comma list or 'none'> checks the notebook")]
		public static void StoryChainCheckCommand(string[] args)
		{
			bool ok = true;
			string types = Names(ChainTypes().OrderBy(t => t));
			string freqs = RecieverFrequency.AllFrequencies == null ? "none" : string.Join(",", RecieverFrequency.AllFrequencies.Where(f => f != null && (int)f.chunkPointType >= StoryChain.ModTypeBase).Select(f => (int)f.chunkPointType + "=" + f).ToArray());
			string onNotes = string.Join(",", Chain.Skip(1).Select(t => StoryOrder.Name(t) + ":" + (StoryOrder.FrequencyText(t) ?? "?")).ToArray());
			Log("STORYCHAIN chain " + (StoryChain.Active ? "on" : "off") + " " + Steps(StoryChain.Steps));
			Log("STORYCHAIN unlocked " + Steps(StoryChain.Unlocked.OrderBy(x => x)));
			Log("STORYCHAIN notebook " + types);
			Log("STORYCHAIN frequencies " + freqs);
			Log("STORYCHAIN notes " + onNotes);
			Log("STORYCHAIN islands " + string.Join(",", IslandWorldState.Islands.Where(e => e.Label.Length > 0).Select(e => e.Label + "=" + e.HostName).OrderBy(x => x).ToArray()));
			if (args != null && args.Length > 0)
			{
				string want = string.Join(" ", args).Trim();
				Check(ref ok, want == "none" ? types.Length == 0 : types == want, "the notebook's story islands: " + types + " (expected " + want + ")");
			}
			if (ok) Log("PASS: story chain check"); else Fail("story chain check");
		}
	}
}
