using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Scenario tests of the builder's side (TEST_PROTOCOL §5j SC70-SC79): packs with odd contents, two packs that clash,
	/// Check against what the game does, rule waits the game never meets, plans and islands edited while worlds play
	/// them, a library update mid-adventure; and SC40, many players' decisions on the host.
	/// </summary>
	public static partial class DevTests
	{
		static void ScDeletePlan(params string[] plans)
		{
			foreach (string p in plans) try { if (File.Exists(WorldPlan.PathFor(p))) File.Delete(WorldPlan.PathFor(p)); } catch { }
		}

		static void ScDeleteIslands(Func<string, bool> which)
		{
			foreach (string n in IslandSpawner.ListSavedIslands().Where(which).ToList())
				try { File.Delete(IslandSpawner.PathFor(n)); } catch { }
			IslandCache.Forget();
		}

		#region SC75 - pack edge cases; SC74 - two packs clash

		[ConsoleCommand(name: "CIScPackEdges", docs: "Dev, anywhere: SC75 - a plan of map types only is exported and imported; a plan identical to the player's, imported then removed, leaves the player's; an import that renames an island keeps 'after rule' waits on a rule of that name (AU53)")]
		public static void ScPackEdgesCommand()
		{
			bool ok = true;
			const string mapsPlan = "CI Pack Maps", samePlan = "CI Pack Same", renPlan = "CI Pack Rename";
			Func<string, bool> mine = n => n.StartsWith("cipk-", StringComparison.OrdinalIgnoreCase);
			ScDeleteIslands(mine);
			ScDeletePlan(mapsPlan, samePlan, renPlan, renPlan + " (CI Tester)");
			foreach (string id in new[] { "ci-pack-maps", "ci-pack-same", "ci-pack-rename" }) try { LibraryPack.Remove(id); } catch { }
			string error;
			try
			{
				// (a) Map types only
				WorldPlan.Parse(mapsPlan, "random = off\nrule = wreck | type:wreck | start | ahead:500 | | Wreck\nrule = sky | type:sky | km:2 | ahead:900 | | Sky\n").Save();
				string zip = LibraryPack.Export(new LibraryInfo { title = "CI Pack Maps", id = "ci-pack-maps", author = "CI Tester", summary = "test", remix = true }, null, WorldPlan.Load(mapsPlan), null, null, out error);
				Check(ref ok, zip != null, "a plan of map types only is exported (" + (zip ?? error) + ")");
				LibraryPackContents pack = zip != null ? LibraryPack.Read(zip, out error) : null;
				Check(ref ok, pack != null, "... and can be imported (" + (pack != null ? "read" : error) + ") - AU53");

				// (b) The player's own plan, exported and imported on the same PC, then the pack removed
				MakeLibIsland("cipk-same", 11);
				WorldPlan.Parse(samePlan, "random = off\nrule = start | island:cipk-same | start | ahead:300 | | Same\n").Save();
				string before = File.ReadAllText(WorldPlan.PathFor(samePlan));
				zip = LibraryPack.Export(new LibraryInfo { title = "CI Pack Same", id = "ci-pack-same", author = "CI Tester", summary = "test", remix = true }, null, WorldPlan.Load(samePlan), null, null, out error);
				pack = zip != null ? LibraryPack.Read(zip, out error) : null;
				Check(ref ok, pack != null, "the player's plan exported (" + (zip ?? error) + ")");
				if (pack != null)
				{
					LibraryPack.Report r = LibraryPack.Install(pack, false, false, LibraryPack.SourceImport);
					Log("  " + r.ToString().Replace("\n", " / "));
					LibraryPack.Remove("ci-pack-same");
					Check(ref ok, File.Exists(WorldPlan.PathFor(samePlan)) && File.ReadAllText(WorldPlan.PathFor(samePlan)) == before, "imported over the identical plan and removed: the player's own plan is still there - AU53");
					Check(ref ok, File.Exists(IslandSpawner.PathFor("cipk-same")), "... and the player's own island too");
				}

				// (c) A rule named like its island; the island renamed on import ("another PC" with a different one)
				MakeLibIsland("cipk-camp", 21);
				WorldPlan.Parse(renPlan, "random = off\nrule = cipk-camp | island:cipk-camp | start | ahead:300 | | Camp\nrule = next | type:wreck | rule:cipk-camp | ahead:700 | | Next\n").Save();
				zip = LibraryPack.Export(new LibraryInfo { title = "CI Pack Rename", id = "ci-pack-rename", author = "CI Tester", summary = "test", remix = true }, null, WorldPlan.Load(renPlan), null, null, out error);
				pack = zip != null ? LibraryPack.Read(zip, out error) : null;
				Check(ref ok, pack != null, "a plan whose rule is named like its island exported (" + (zip ?? error) + ")");
				if (pack != null)
				{
					ScDeletePlan(renPlan);
					MakeLibIsland("cipk-camp", 99); // (the other PC's own, different 'cipk-camp')
					LibraryPack.Report r = LibraryPack.Install(pack, false, false, LibraryPack.SourceImport);
					Log("  " + r.ToString().Replace("\n", " / "));
					WorldPlan got = WorldPlan.Load(r.PlanName ?? renPlan);
					IntroRule camp = got != null ? got.Rules.FirstOrDefault(x => x.What == "island") : null;
					IntroRule next = got != null ? got.Rules.FirstOrDefault(x => x.Id == "next") : null;
					Check(ref ok, camp != null && camp.WhatArg.StartsWith("cipk-camp (", StringComparison.Ordinal), "the pack's island is installed under a new name and its rule brings it ('" + (camp != null ? camp.WhatArg : "?") + "')");
					Check(ref ok, next != null && camp != null && next.WhenRef == camp.Id, "the rule waiting for rule '" + (camp != null ? camp.Id : "?") + "' still waits for it ('" + (next != null ? next.WhenRef : "?") + "') - AU53");
					LibraryPack.Remove("ci-pack-rename");
				}
			}
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex.Message); }
			finally
			{
				foreach (string id in new[] { "ci-pack-maps", "ci-pack-same", "ci-pack-rename" }) try { LibraryPack.Remove(id); } catch { }
				ScDeleteIslands(mine);
				ScDeletePlan(mapsPlan, samePlan, renPlan, renPlan + " (CI Tester)");
			}
			if (ok) Log("PASS: scenario pack edges"); else Fail("scenario pack edges");
		}

		[ConsoleCommand(name: "CIScNames", docs: "Dev, anywhere: AT28 - island names the mod's own lists can't hold are refused when saving (# or @ first, = , ;), ordinary ones and other kinds of names (plans) are not (AU30)")]
		public static void ScNamesCommand()
		{
			bool ok = true;
			foreach (string bad in new[] { "#1 Base", "@home", "Rock, big", "a=b", "Cove;2" })
				Check(ref ok, FileNames.IslandProblem(bad) != null, "an island can't be called '" + bad + "' (" + (FileNames.IslandProblem(bad) ?? "allowed") + ") - AU30");
			foreach (string good in new[] { "Camp 2", "Åkerö", "Palm Cove (Library)", "gen-sandbar-123456", "Bob's raft" })
				Check(ref ok, FileNames.IslandProblem(good) == null, "an island can be called '" + good + "' (" + (FileNames.IslandProblem(good) ?? "allowed") + ")");
			Check(ref ok, FileNames.Valid("Voyage, part 2"), "other names (a plan's) may still hold a comma");
			if (ok) Log("PASS: scenario names"); else Fail("scenario names");
		}

		[ConsoleCommand(name: "CIScTwoPacks", docs: "Dev, anywhere: SC74 - two packs with an island of the same name and a story item of the same id: the second's island is renamed and its plan follows; the second's story item gets an id of its own, its quest, reward and chest follow (T8)")]
		public static void ScTwoPacksCommand()
		{
			bool ok = true;
			Func<string, bool> mine = n => n.StartsWith("cipk-twin", StringComparison.OrdinalIgnoreCase);
			ScDeleteIslands(mine);
			ScDeletePlan("CI Pack A", "CI Pack B", "CI Pack C");
			foreach (string id in new[] { "ci-pack-a", "ci-pack-b", "ci-pack-a-ci-other" }) try { LibraryPack.Remove(id); } catch { }
			string error;
			try
			{
				Func<string, int, string, string, string, LibraryPackContents> make = (plan, seed, keyName, packId, author) =>
				{
					MakeLibIsland("cipk-twin", seed);
					IslandFile f = IslandFile.Load(IslandSpawner.PathFor("cipk-twin"));
					f.Props[StoryItems.Key] = "cipkkey|" + keyName + "||A key from " + plan;
					f.Props[IslandQuest.Key(IslandQuest.KeySteps, 0)] = "collect|cipkkey|1|Find the key";
					f.Props[IslandQuest.KeyReward] = "story:cipkkey";
					f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = new Vector3(10f, 0f, 10f), Props = new Dictionary<string, string> { { ObjectProps.LootItems, "story:cipkkey*1, story:cipkkey-old*1" } } });
					f.Save(IslandSpawner.PathFor("cipk-twin"));
					WorldPlan.Parse(plan, "random = off\nrule = start | island:cipk-twin | start | ahead:300 | | Twin\n").Save();
					string z = LibraryPack.Export(new LibraryInfo { title = plan, id = packId ?? plan.ToLowerInvariant().Replace(' ', '-'), author = author ?? "CI Tester", summary = "test", remix = true }, null, WorldPlan.Load(plan), null, null, out error);
					LibraryPackContents p = z != null ? LibraryPack.Read(z, out error) : null;
					File.Delete(IslandSpawner.PathFor("cipk-twin"));
					ScDeletePlan(plan);
					return p;
				};
				LibraryPackContents a = make("CI Pack A", 31, "Red key", null, null), b = make("CI Pack B", 32, "Blue key", null, null);
				Check(ref ok, a != null && b != null, "two packs exported, each with its 'cipk-twin' and a story item 'cipkkey'");
				if (a != null && b != null)
				{
					LibraryPack.Report ra = LibraryPack.Install(a, false, false, LibraryPack.SourceImport);
					LibraryPack.Report rb = LibraryPack.Install(b, false, false, LibraryPack.SourceImport);
					Log("  A: " + ra.ToString().Replace("\n", " / ") + " || B: " + rb.ToString().Replace("\n", " / "));
					WorldPlan pb = WorldPlan.Load(rb.PlanName ?? "CI Pack B");
					IntroRule rule = pb != null ? pb.Rules.FirstOrDefault() : null;
					Check(ref ok, rule != null && rule.WhatArg != "cipk-twin" && File.Exists(IslandSpawner.PathFor(rule.WhatArg)), "B's island is installed under its own name and B's plan brings it ('" + (rule != null ? rule.WhatArg : "?") + "')");
					Check(ref ok, File.Exists(IslandSpawner.PathFor("cipk-twin")), "A's island keeps its name");
					// (T8: B's "cipkkey" gets an id of its own - definition, quest step, reward and chest - so A's key doesn't
					// open B's door; A's stays; "story:cipkkey-old" is another item and stays too)
					string text = rb.ToString();
					Func<Dictionary<string, string>, string, string> pg = (p, k) => p != null && p.ContainsKey(k) ? p[k] : "";
					IslandFile ia = IslandFile.Load(IslandSpawner.PathFor("cipk-twin")), ib = rule != null ? IslandFile.Load(IslandSpawner.PathFor(rule.WhatArg)) : null;
					string idB = ib != null ? string.Join(",", StoryItems.Of(ib.Props).Select(d => d.Id).ToArray()) : "?";
					Check(ref ok, ia != null && StoryItems.Of(ia.Props).Any(d => d.Id == "cipkkey") && idB == "cipkkey-ci-pack-b" && text.IndexOf("cipkkey-ci-pack-b", StringComparison.Ordinal) >= 0,
						"B's story item gets its own id (A: cipkkey, B: " + idB + "), and the import says so - T8");
					IslandObject chestB = ib != null ? ib.Objects.LastOrDefault(o => o.Name == "Loot_Chest") : null; string lootB = chestB != null ? pg(chestB.Props, ObjectProps.LootItems) : "?";
					Check(ref ok, ib != null && pg(ib.Props, IslandQuest.Key(IslandQuest.KeySteps, 0)).StartsWith("collect|cipkkey-ci-pack-b|") && pg(ib.Props, IslandQuest.KeyReward) == "story:cipkkey-ci-pack-b"
						&& lootB == "story:cipkkey-ci-pack-b*1, story:cipkkey-old*1", "B's quest step, reward and chest follow the new id ('" + lootB + "')");
					Check(ref ok, ia != null && pg(ia.Props, IslandQuest.KeyReward) == "story:cipkkey", "A's island is untouched");
					LibraryInstalled eb = LibraryPack.Installed().FirstOrDefault(e => e.id == "ci-pack-b");
					Check(ref ok, eb != null && eb.storyIds != null && eb.storyIds.ContainsKey("cipkkey") && eb.storyIds["cipkkey"] == "cipkkey-ci-pack-b", "the entry remembers the new id (an update keeps it)");
					// (another author's pack with A's id: an entry of its own, A's files untouched - CB4)
					LibraryPackContents c = make("CI Pack C", 33, "Green key", "ci-pack-a", "CI Other");
					LibraryPack.Report rc = c != null ? LibraryPack.Install(c, false, false, LibraryPack.SourceImport) : null;
					List<LibraryInstalled> inst = LibraryPack.Installed();
					LibraryInstalled ea = inst.FirstOrDefault(e => e.id == "ci-pack-a"), ec = inst.FirstOrDefault(e => e.id == "ci-pack-a-ci-other");
					Check(ref ok, rc != null && ea != null && ea.author == "CI Tester" && ea.title == "CI Pack A" && ec != null && ec.author == "CI Other" && File.Exists(IslandSpawner.PathFor("cipk-twin"))
						&& WorldPlan.Load("CI Pack A") != null, "another author's pack with A's id is an entry of its own ('" + (ec != null ? ec.id : "none") + "'), A's kept (" + (rc != null ? rc.ToString().Replace("\n", " / ") : "no report") + ")");
				}
			}
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex.Message); }
			finally
			{
				foreach (string id in new[] { "ci-pack-a", "ci-pack-b", "ci-pack-a-ci-other" }) try { LibraryPack.Remove(id); } catch { }
				ScDeleteIslands(mine);
				ScDeletePlan("CI Pack A", "CI Pack B", "CI Pack C", "CI Pack A (CI Tester)", "CI Pack B (CI Tester)", "CI Pack C (CI Other)");
			}
			if (ok) Log("PASS: scenario two packs"); else Fail("scenario two packs");
		}

		#endregion

		#region SC77 - Check and the game agree

		[ConsoleCommand(name: "CIScCheckRuntime", docs: "Dev, anywhere: SC77 - Check against what the game does: km:0/day:0 rules (the game plays them), a pages step whose notes have no text (the game gives no page), a creature spot with the default respawn (they come back), a screecher step (none in Creative) (AU55)")]
		public static void ScCheckRuntimeCommand()
		{
			bool ok = true;
			const string isl = "ciscchk", plan = "CI Check Runtime";
			try
			{
				var f = new IslandFile { Name = isl, TerrainSize = new Vector3(120, 40, 120), HeightmapResolution = 33, Heights = new float[33, 33] };
				for (int y = 0; y < 33; y++) for (int x = 0; x < 33; x++) f.Heights[y, x] = 0.6f;
				f.Objects.Add(ScObj("Creature_Boar", new Vector3(60, 24, 60), ObjectProps.CreatureCount, "1"));
				f.Objects.Add(ScObj("Creature_StoneBird", new Vector3(50, 24, 60), ObjectProps.CreatureCount, "1"));
				for (int i = 0; i < 3; i++) f.Objects.Add(ScObj("Note_Paper", new Vector3(40 + i * 5, 24, 40), ObjectProps.NoteTitle, "Blank " + i));
				new IslandQuest { Title = "Check me", Steps = {
					new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 2 },
					new IslandQuest.Step { Type = "pages", Target = "", Count = 3 },
					new IslandQuest.Step { Type = "kill", Target = "Screecher", Count = 1 } } }.To(f.Props);
				f.Save(IslandSpawner.PathFor(isl));
				IslandCache.Forget();
				WorldPlan p = WorldPlan.Parse(plan, "random = off\n" +
					"rule = first | island:" + isl + " | start | ahead:300 | | First\n" +
					"rule = zero | type:wreck | km:0 | ahead:600 | | Zero km\n" +
					"rule = dayzero | type:sandbar | day:0 | ahead:900 | | Day zero\n" +
					"rule = after | type:sky | quest:first | ahead:1200 | | After\n");
				List<PlanChecker.Finding> found = PlanChecker.Check(p, false, false, null);
				Func<string, IEnumerable<PlanChecker.Finding>> about = s => found.Where(x => x.Text.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
				foreach (PlanChecker.Finding x in found) Log("  Check: " + x.Level + " (rule " + x.Rule + "): " + x.Text);
				Check(ref ok, !found.Any(x => x.Level == PlanChecker.Level.Problem && (x.Rule == 1 || x.Rule == 2)), "Check doesn't call 'after 0 km' / 'on day 0' a problem - the game plays them at once - AU55");
				Check(ref ok, found.Any(x => x.Text.IndexOf("pages", StringComparison.OrdinalIgnoreCase) >= 0 && x.Level != PlanChecker.Level.Tip), "Check says notes without text give no pages (the quest needs 3) - AU55");
				Check(ref ok, !found.Any(x => x.Text.IndexOf("don't come back", StringComparison.OrdinalIgnoreCase) >= 0 && x.Text.IndexOf("Warthog", StringComparison.OrdinalIgnoreCase) >= 0),
					"Check doesn't say the warthog won't come back - by default they do - AU55");
				Check(ref ok, found.Any(x => x.Text.IndexOf("Creative", StringComparison.OrdinalIgnoreCase) >= 0), "Check says the screecher step can't be done in Creative (no screechers there) - AU55");
			}
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex.Message); }
			finally
			{
				ScDeletePlan(plan);
				if (File.Exists(IslandSpawner.PathFor(isl))) File.Delete(IslandSpawner.PathFor(isl));
				IslandCache.Forget();
			}
			if (ok) Log("PASS: scenario check runtime"); else Fail("scenario check runtime");
		}

		#endregion

		#region SC76, SC35 - rule waits the game never meets

		[ConsoleCommand(name: "CIScRuleWaits", docs: "Dev, world (host, 'CI ...'): SC76/SC35 - 'after rule' on a Receiver rule fires (AU52); a zone id with ':' survives saving (AU54); two zones with one id: the rule and the quest agree (AU54); a comma in a story rule id survives saving (AU54)")]
		public static void ScRuleWaitsCommand() { StartTest(ScRuleWaitsRoutine()); }

		static IEnumerator ScRuleWaitsRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario rule waits: run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario rule waits: only in a test world 'CI ...'"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string planBefore = WorldDirector.PlanName;
			var doneBefore = WorldDirector.Done.ToList();
			List<string> chainBefore = StoryChain.WriteLines().ToList();
			var made = new List<IslandWorldState.Entry>();
			const string plan = "CI Rule Waits", dup = "ciscdupzone";
			try
			{
				// (a) after a Receiver rule
				WorldPlan.Parse(plan, "random = off\nrule = radio | type:sandbar | start | receiver:400 | A signal. | Radio\nrule = after | type:wreck | rule:radio | ahead:700 | | After\n").Save();
				WorldDirector.Done.Clear();
				ScSetPlan(plan, true);
				yield return TuneTo("radio");
				bool radio = IslandWorldState.Islands.Any(x => x.Rule == "radio");
				for (int i = 0; i < 4; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, radio, "the Receiver rule's island came when tuned");
				Check(ref ok, IslandWorldState.Islands.Any(x => x.Rule == "after"), "'after rule radio' fires once the Receiver rule's island came - AU52");
				made.AddRange(IslandWorldState.Islands.Where(x => x.Rule == "radio" || x.Rule == "after"));

				// (b) a zone id with ':' written and read back
				IntroRule z = IntroRule.Parse("z | type:wreck | zone:self:cave:1 | ahead:300 | | Z");
				IntroRule back = z != null ? IntroRule.Parse(z.ToLine()) : null;
				Check(ref ok, back != null && back.WhenArg == "cave:1", "a rule waiting for zone 'cave:1', saved and read: still 'cave:1' ('" + (back != null ? back.WhenArg : "?") + "') - AU54");

				// (c) two zones called 'gate': the second entered - the quest counts it, so must the island's rule
				IslandFile f = ScIsland(dup, "Two Gates");
				f.Objects.Add(ScObj(ContentCatalog.TriggerZone, ScDry(f, new Vector2(-20, 0), 1), ObjectProps.ZoneId, "gate", ObjectProps.ZoneRadius, "4"));
				f.Objects.Add(ScObj(ContentCatalog.TriggerZone, ScDry(f, new Vector2(20, 0), 2), ObjectProps.ZoneId, "gate", ObjectProps.ZoneRadius, "4"));
				new IslandQuest { Title = "Gates", Steps = { new IslandQuest.Step { Type = "reach", Target = "gate", Count = 1 } } }.To(f.Props);
				WorldDirector.SetRulesInProps(f.Props, new List<IntroRule> { IntroRule.Parse("g | type:sandbar | zone:self:gate | near:self:600 | | G") });
				f.Save(IslandSpawner.PathFor(dup));
				Vector3? spot = ScSpot(dup, 400f);
				if (spot.HasValue)
				{
					yield return ScBring(dup, spot.Value, made);
					IslandWorldState.Entry e = made.LastOrDefault(x => x.HostName == dup);
					TriggerZone second = e != null && e.Root != null ? e.Root.GetComponentsInChildren<TriggerZone>(true).Where(x => x.Id == "gate").Skip(1).FirstOrDefault() : null;
					if (second != null)
					{
						PutPlayerNear(second.transform, 0.3f);
						second.Enter();
						yield return new WaitForSeconds(1.5f);
						for (int i = 0; i < 3; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
						bool quest = QuestTracker.StepOf(e) >= 1, rule = IslandWorldState.Islands.Any(x => x.Rule == "g");
						Check(ref ok, quest == rule, "two zones called 'gate', the second entered: the quest (" + (quest ? "counted" : "not") + ") and the island's rule (" + (rule ? "fired" : "waits") + ") agree - AU54");
						made.AddRange(IslandWorldState.Islands.Where(x => x.Rule == "g"));
					}
					else Check(ref ok, false, "the island with two gates came");
				}
				else Check(ref ok, false, "open sea for the island with two gates");

				// (d) a comma in a story rule's id, saved and read back
				StoryChain.Reset();
				StoryChain.FromPlan(WorldPlan.Parse("CI comma", "story = on\nrule = camp,1 | type:sandbar | start | receiver:400 | | Camp | after:Vasagatan | visit\n"));
				// (the steps compared one by one: joined with commas, "rule:camp,1" read back as two steps looked the same)
				List<string> stepsBefore = StoryChain.Steps.ToList();
				List<string> lines = StoryChain.WriteLines().ToList();
				StoryChain.Reset();
				foreach (string l in lines) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				Check(ref ok, StoryChain.Steps.SequenceEqual(stepsBefore), "a story rule 'camp,1' saved and read: the same chain (" + stepsBefore.Count + " steps: " + string.Join(" > ", stepsBefore.ToArray()) + " -> " + StoryChain.Steps.Count + " steps: " + string.Join(" > ", StoryChain.Steps.ToArray()) + ") - AU54");
			}
			finally
			{
				ScRemove(made, dup);
				ScDeletePlan(plan);
				StoryChain.Reset();
				foreach (string l in chainBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
				StoryChain.OnWorldRead();
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario rule waits"); else Fail("scenario rule waits");
		}

		#endregion

		#region SC71, SC79 - the plan edited while a world plays it; switching plans

		[ConsoleCommand(name: "CIScPlanEdit", docs: "Dev, world (host, 'CI ...'): SC71/SC79 - a rule added to a running plan fires; a fired rule deleted and a new one under its id (as World Plans numbers them) fires too (AU24); switching to a plan whose first rule has a done id fires it (AU71)")]
		public static void ScPlanEditCommand() { StartTest(ScPlanEditRoutine()); }

		static IEnumerator ScPlanEditRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario plan edit: run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario plan edit: only in a test world 'CI ...'"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string planBefore = WorldDirector.PlanName;
			var doneBefore = WorldDirector.Done.ToList();
			int islandsBefore = IslandWorldState.Islands.Count;
			const string p1 = "CI Plan Edit", p2 = "CI Plan Other";
			try
			{
				WorldPlan.Parse(p1, "random = off\nrule = rule1 | type:wreck | start | ahead:700 | | One\nrule = rule2 | type:sandbar | km:99 | ahead:800 | | Two\nrule = rule3 | type:oddity | start | ahead:1100 | | Three\n").Save();
				WorldDirector.Done.Clear();
				Check(ref ok, ScSetPlan(p1, true), "the world plays '" + p1 + "'");
				WorldDirector.Evaluate();
				Check(ref ok, WorldDirector.Done.Contains("rule1") && WorldDirector.Done.Contains("rule3") && !WorldDirector.Done.Contains("rule2"), "rule1 and rule3 fired, rule2 waits (" + string.Join(",", WorldDirector.Done.ToArray()) + ")");
				// Edit 1: a rule added
				WorldPlan p = WorldPlan.Load(p1);
				p.Rules.Add(IntroRule.Parse("rule4 | type:sky | start | ahead:1400 | | Four"));
				p.Save();
				ScSetPlan(p1, false);
				WorldDirector.Evaluate();
				Check(ref ok, WorldDirector.Done.Contains("rule4"), "a rule added to the running plan fires");
				// Edit 2: rule2 (waiting) and rule3 (fired) deleted, a new rule added - World Plans numbers it 'rule3' again
				p = WorldPlan.Load(p1);
				p.Rules.RemoveAll(r => r.Id == "rule2" || r.Id == "rule3");
				p.Rules.Add(IntroRule.Parse("rule3 | type:treasure | start | ahead:1700 | | New three"));
				p.Save();
				ScSetPlan(p1, false);
				WorldDirector.Evaluate();
				Check(ref ok, IslandWorldState.Islands.Any(x => x.Rule == "rule3" && x.Label == "New three"), "a new rule that got a fired rule's old id ('rule3') fires - AU24");
				// Switching plans: the new plan's first rule is also called 'rule1'
				WorldPlan.Parse(p2, "random = off\nrule = rule1 | type:sunken | start | ahead:2000 | | Other one\n").Save();
				ScSetPlan(p2, true);
				WorldDirector.Evaluate();
				Check(ref ok, IslandWorldState.Islands.Any(x => x.Label == "Other one"), "switched to another plan whose first rule is 'rule1' too: it fires - AU71");
			}
			finally
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(islandsBefore).Select(x => x.Id).ToList(), true);
				ScDeletePlan(p1, p2);
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
			}
			if (ok) Log("PASS: scenario plan edit"); else Fail("scenario plan edit");
		}

		#endregion

		#region SC73 - a plan whose island was deleted; SC72 - a library update mid-adventure

		[ConsoleCommand(name: "CIScMissing", docs: "Dev, world (host, 'CI ...'): SC73 - a plan rule's island file deleted: the rule waits, the host is told once which island is missing, the rest plays; the file put back, it comes")]
		public static void ScMissingCommand() { StartTest(ScMissingRoutine()); }

		static IEnumerator ScMissingRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario missing: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string planBefore = WorldDirector.PlanName;
			var doneBefore = WorldDirector.Done.ToList();
			int islandsBefore = IslandWorldState.Islands.Count;
			const string plan = "CI Missing", gone = "ciscvanish";
			var told = new List<string>();
			Application.LogCallback watch = (text, trace, type) => { if (text.Contains(gone)) told.Add(text); };
			Application.logMessageReceived += watch;
			try
			{
				if (File.Exists(IslandSpawner.PathFor(gone))) File.Delete(IslandSpawner.PathFor(gone));
				IslandCache.Forget();
				WorldPlan.Parse(plan, "random = off\nrule = vault | island:" + gone + " | start | ahead:600 | | Vault\nrule = other | type:wreck | start | ahead:900 | | Other\n").Save();
				WorldDirector.Done.Clear();
				ScSetPlan(plan, true);
				for (int i = 0; i < 6; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, !WorldDirector.Done.Contains("vault") && WorldDirector.Done.Contains("other"), "the rule whose island is missing waits; the rest plays (" + string.Join(",", WorldDirector.Done.ToArray()) + ")");
				Check(ref ok, WorldDirector.MissingIslands.Any(m => m.Contains(gone)) && told.Count >= 1 && told.Count <= 3, "the host is told which island is missing (" + told.Count + " lines over 6 checks; missing: " + string.Join(", ", WorldDirector.MissingIslands.ToArray()) + ")");
				RuleIsland(gone, "Vanished Vault").Save(IslandSpawner.PathFor(gone));
				IslandCache.Forget();
				for (int i = 0; i < 60 && !WorldDirector.Done.Contains("vault"); i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); } // (a rule that failed tries again after 20 s)
				Check(ref ok, WorldDirector.Done.Contains("vault"), "the file put back: the island comes");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(islandsBefore).Select(x => x.Id).ToList(), true);
				ScDeletePlan(plan);
				if (File.Exists(IslandSpawner.PathFor(gone))) File.Delete(IslandSpawner.PathFor(gone));
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
			}
			if (ok) Log("PASS: scenario missing"); else Fail("scenario missing");
		}

		[ConsoleCommand(name: "CIScLibUpdate", docs: "Dev, world (host, 'CI ...'): SC72 - a plan installed from a pack (v1) plays in a world; v2 (its first island changed, a rule added) installed over it: the world keeps v1's island by its hash and the plan's v1 rules, a new world would get v2")]
		public static void ScLibUpdateCommand() { StartTest(ScLibUpdateRoutine()); }

		static IEnumerator ScLibUpdateRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario lib update: run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario lib update: only in a test world 'CI ...'"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string planBefore = WorldDirector.PlanName;
			var doneBefore = WorldDirector.Done.ToList();
			int islandsBefore = IslandWorldState.Islands.Count;
			const string plan = "CI Upd Plan", a = "ciupd-a", id = "ci-upd-plan";
			Func<string, bool> mine = n => n.StartsWith("ciupd-", StringComparison.OrdinalIgnoreCase);
			string error;
			try
			{
				try { LibraryPack.Remove(id); } catch { }
				ScDeleteIslands(mine);
				ScDeletePlan(plan);
				// v1: island a with two chests
				IslandFile f = ScIsland(a, "Update Isle");
				f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(0, 0), 1), ObjectProps.NoteTitle, "Chest one", ObjectProps.LootItems, "Plank*1"));
				f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(12, 0), 2), ObjectProps.NoteTitle, "Chest two", ObjectProps.LootItems, "Plank*1"));
				f.Save(IslandSpawner.PathFor(a));
				WorldPlan.Parse(plan, "random = off\nrule = start | island:" + a + " | start | ahead:500 | | Start\n").Save();
				string zip1 = LibraryPack.Export(new LibraryInfo { title = plan, id = id, author = "CI Tester", summary = "v1", remix = true }, null, WorldPlan.Load(plan), null, null, out error);
				LibraryPackContents v1 = zip1 != null ? LibraryPack.Read(zip1, out error) : null;
				Check(ref ok, v1 != null, "v1 exported (" + (zip1 ?? error) + ")");
				if (v1 == null) yield break;
				// v2: island a without its second chest (an edit that shifts saved worlds' state), and a second rule
				f.Objects.RemoveAll(o => o.Props != null && ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Chest two");
				f.Save(IslandSpawner.PathFor(a));
				WorldPlan.Parse(plan, "random = off\nrule = start | island:" + a + " | start | ahead:500 | | Start\nrule = more | type:wreck | start | ahead:900 | | More\n").Save();
				string zip2 = LibraryPack.Export(new LibraryInfo { title = plan, id = id, author = "CI Tester", summary = "v2", remix = true, version = 2 }, null, WorldPlan.Load(plan), null, null, out error);
				LibraryPackContents v2 = zip2 != null ? LibraryPack.Read(zip2, out error) : null;
				Check(ref ok, v2 != null, "v2 exported (" + (zip2 ?? error) + ")");
				if (v2 == null) yield break;
				// The player installs v1 and plays it
				ScDeleteIslands(mine);
				ScDeletePlan(plan);
				LibraryPack.Report r1 = LibraryPack.Install(v1, false, false, LibraryPack.SourceImport);
				Log("  v1: " + r1.ToString().Replace("\n", " / "));
				WorldDirector.Done.Clear();
				ScSetPlan(r1.PlanName ?? plan, true);
				WorldDirector.Evaluate();
				yield return WaitFor(() => IslandWorldState.Islands.Any(x => x.HostName == a && x.Root != null), 30f);
				IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == a);
				Check(ref ok, e != null && e.Root != null, "v1's first island comes in the world");
				if (e == null) yield break;
				string hash1 = IslandNetwork.HashOf(a);
				ScOpenChest(e, "Chest one");
				IslandWorldState.Save();
				// v2 installed over it
				LibraryPack.Report r2 = LibraryPack.Install(v2, false, true, LibraryPack.SourceImport);
				Log("  v2: " + r2.ToString().Replace("\n", " / "));
				string hash2 = IslandNetwork.HashOf(a);
				Check(ref ok, hash2 != hash1, "v2's island is installed (" + hash1 + " -> " + hash2 + ")");
				string kept = WorldCopy.LocalFileFor(a, hash1);
				Check(ref ok, kept != null && File.Exists(IslandSpawner.PathFor(kept)), "the world playing v1 keeps v1's island (as '" + (kept ?? "?") + "')");
				Check(ref ok, WorldPlan.Load(plan) != null && WorldPlan.Load(plan).Rules.Count == 2, "a new world would get v2's plan (2 rules)");
			}
			finally
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(islandsBefore).Select(x => x.Id).ToList(), true);
				try { LibraryPack.Remove(id); } catch { }
				ScDeleteIslands(mine);
				ScDeletePlan(plan);
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
			}
			if (ok) Log("PASS: scenario lib update"); else Fail("scenario lib update");
		}

		#endregion

		#region SC70 - an island edited while a world plays it

		const string EditIsland = "ciscedit";
		static string editHashBefore = "";

		[ConsoleCommand(name: "CIScEditUsed", docs: "Dev: SC70 - an island edited while a world plays it. CIScEditUsed prep (world) = the island in the world, its first box opened, saved; safe (editor) = a rock moved, saved; checksafe (world) = the edit reached the world, the box still opened; unsafe (editor) = the second box deleted, saved; checkunsafe (world) = the world plays the new version, the opened box still opened (carried by the objects' ids, R1b)")]
		public static void ScEditUsedCommand(string[] args)
		{
			string part = args != null && args.Length > 0 ? args[0] : "prep";
			StartTest(ScEditUsedRoutine(part));
		}

		static IEnumerator ScEditUsedRoutine(string part)
		{
			bool ok = true;
			if (part == "prep")
			{
				if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario edit used " + part + ": run in a world, as the host"); yield break; }
				yield return EnsureAlive();
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => x.HostName == EditIsland).Select(x => x.Id).ToList(), true);
				IslandFile f = ScIsland(EditIsland, "Edit Isle");
				f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(0, 0), 1), ObjectProps.NoteTitle, "Box 1", ObjectProps.LootItems, "Plank*1", ObjectProps.LootRefill, "0"));
				f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(12, 0), 2), ObjectProps.NoteTitle, "Box 2", ObjectProps.LootItems, "Plank*1", ObjectProps.LootRefill, "0"));
				f.Save(IslandSpawner.PathFor(EditIsland));
				var made = new List<IslandWorldState.Entry>();
				Vector3? spot = ScSpot(EditIsland, 400f);
				if (!spot.HasValue) { Fail("scenario edit used " + part + ": no open sea"); yield break; }
				yield return ScBring(EditIsland, spot.Value, made);
				IslandWorldState.Entry e = made.FirstOrDefault();
				Check(ref ok, e != null && e.Root != null, "the island is in the world");
				if (e != null && e.Root != null) { ScOpenChest(e, "Box 1"); yield return new WaitForSeconds(1f); }
				LootCrate box = ScChest(e, "Box 1");
				Check(ref ok, box != null && box.Looted, "its first box opened");
				editHashBefore = IslandNetwork.HashOf(EditIsland);
				OnRaftCommand();
				IslandWorldState.Save();
			}
			else if (part == "safe" || part == "unsafe")
			{
				if (!DynamicIslands.InEditor()) { Fail("scenario edit used " + part + ": " + part + " in the editor"); yield break; }
				Check(ref ok, DynamicIslands.LoadIsland(EditIsland), "the island opened in the editor");
				yield return new WaitForSeconds(2f);
				Transform placed = GameObject.Find("PlacedObjects").transform;
				EditorGameObject[] objs = placed.GetComponentsInChildren<EditorGameObject>();
				if (part == "safe")
				{
					EditorGameObject rock = objs.FirstOrDefault(o => o.Props == null || o.Props.Count == 0);
					if (rock != null) rock.transform.position += new Vector3(1.5f, 0f, 0f);
					Check(ref ok, rock != null, "a rock moved 1.5 m");
				}
				else
				{
					EditorGameObject box2 = objs.FirstOrDefault(o => o.Props != null && ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Box 2");
					// (looked at before it goes: a destroyed object counts as null after DestroyImmediate)
					bool found = box2 != null;
					if (found) UnityEngine.Object.DestroyImmediate(box2.gameObject);
					Check(ref ok, found, "the second box deleted");
				}
				var notes = new List<string>();
				Application.LogCallback watch = (text, trace, type) => { if (text.IndexOf("world", StringComparison.OrdinalIgnoreCase) >= 0 && text.Contains(EditIsland)) notes.Add(text); };
				Application.logMessageReceived += watch;
				try { Check(ref ok, DynamicIslands.SaveIsland(EditIsland), "saved"); }
				finally { Application.logMessageReceived -= watch; }
				if (part == "unsafe") Check(ref ok, notes.Count > 0, "the builder is told saved worlds keep the version they have (" + (notes.Count > 0 ? notes[0] : "nothing said") + ")");
				Log("  hash now " + IslandNetwork.HashOf(EditIsland) + " (before the edits " + editHashBefore + ")");
			}
			else
			{
				if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario edit used " + part + ": check in a world, as the host"); yield break; }
				yield return new WaitForSeconds(3f);
				IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == EditIsland);
				if (e == null) { Fail("scenario edit used " + part + ": the island isn't in the world any more"); yield break; }
				if (e.Root == null) { Raft raft = UnityEngine.Object.FindObjectOfType<Raft>(); if (raft != null) { PlayerMove.To(RAPI.GetLocalPlayer(), e.Position + Vector3.up * 20f); } yield return WaitFor(() => e.Root != null, 30f); }
				LootCrate box1 = ScChest(e, "Box 1"), box2 = ScChest(e, "Box 2");
				if (part == "checksafe")
				{
					// (the island's own file, not a kept copy: the hash in the world's list is only rewritten at its next save)
					Check(ref ok, e.Name.Equals(EditIsland, StringComparison.OrdinalIgnoreCase), "a safe edit (a rock moved) reaches the world: it plays the edited island (" + e.Name + ")");
					Check(ref ok, box1 != null && box1.Looted, "the opened box stays opened");
				}
				else
				{
					// (since R1b, 10-09: the saved world's state is carried to the new version by the objects' ids - before, the
					// world kept the old version with both boxes)
					Check(ref ok, box2 == null && e.Name.Equals(EditIsland, StringComparison.OrdinalIgnoreCase), "the world plays the edited island (" + e.Name + "): the second box, deleted in the editor, is gone");
					Check(ref ok, box1 != null && box1.Looted, "... and its opened box stays opened (carried by its id)");
					IslandWorldState.RemoveIds(new List<int> { e.Id }, true);
					ScDeleteIslands(n => n.StartsWith(EditIsland, StringComparison.OrdinalIgnoreCase));
				}
				OnRaftCommand();
			}
			if (ok) Log("PASS: scenario edit used " + part); else Fail("scenario edit used " + part);
		}

		#endregion

		#region SC78 - Test in a world starts clean

		[ConsoleCommand(name: "CIScTestClean", docs: "Dev, editor: SC78 - an island with a journal note and a story item chest tried twice with Test in a world: the second time starts without the first's journal page and story item (AU71). Several minutes")]
		public static void ScTestCleanCommand() { StartTest(ScTestCleanRoutine()); }

		static IEnumerator ScTestCleanRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("scenario test clean: in the editor"); yield break; }
			bool ok = true;
			const string name = "cisctestclean";
			IslandFile f = ScIsland(name, "Clean Test");
			f.Props[StoryItems.Key] = "ciscclean|Test shell||A shell";
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(0, 0), 1), ObjectProps.NoteTitle, "Shell chest", ObjectProps.LootItems, "story:ciscclean*1"));
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(10, 0), 2), ObjectProps.NoteTitle, "Clean note", ObjectProps.NoteText, "A page.", BehaviourProps.EventPrefix + "read", "journal|Clean page|Written by the first test"));
			f.Save(IslandSpawner.PathFor(name));
			for (int round = 1; round <= 2; round++)
			{
				Check(ref ok, DynamicIslands.LoadIsland(name), "round " + round + ": the island opened");
				yield return new WaitForSeconds(1f);
				IslandTest.Start();
				for (float t = 0; t < 300f && !IslandTest.Testing; t += 1f) yield return new WaitForSeconds(1f);
				yield return new WaitForSeconds(6f);
				IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => x.HostName == name);
				Check(ref ok, IslandTest.Testing && e != null && e.Root != null, "round " + round + ": Test in a world brought the island");
				if (round == 2)
				{
					Check(ref ok, StoryBook.Count("ciscclean") == 0 && !StoryBook.Pages.Any(p => (p.Text ?? "").Contains("Written by the first test")),
						"the second Test starts clean: no story item (" + StoryBook.Count("ciscclean") + ") and no journal page from the first - AU71");
				}
				else if (e != null && e.Root != null)
				{
					ScOpenChest(e, "Shell chest");
					ScReadNote(e, "Clean note");
					yield return new WaitForSeconds(2f);
					Check(ref ok, StoryBook.Count("ciscclean") == 1, "round 1: the story item and the page came");
				}
				IslandTest.Back();
				for (float t = 0; t < 240f && !(DynamicIslands.InEditor() && !IslandTest.Busy); t += 1f) yield return new WaitForSeconds(1f);
				yield return new WaitForSeconds(2f);
			}
			try { DynamicIslands.NewIsland(); if (File.Exists(IslandSpawner.PathFor(name))) File.Delete(IslandSpawner.PathFor(name)); } catch { }
			if (ok) Log("PASS: scenario test clean"); else Fail("scenario test clean");
		}

		#endregion

		#region SC40 - eight players' decisions at once

		[ConsoleCommand(name: "CIScManyPlayers", docs: "Dev, world (host, 'CI ...'): SC40 - with seven made-up players beside the host, through the host's own handlers: five count one quest step (AU8), four pull one lever in a second (AU18), eight hit one warthog (EXP shares, one kill), eight builders' storages, a claim held after failed checks (AU41)")]
		public static void ScManyPlayersCommand() { StartTest(ScManyPlayersRoutine()); }

		static IEnumerator ScManyPlayersRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario many players: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscmany";
			ulong[] players = { 76561190000000011UL, 76561190000000012UL, 76561190000000013UL, 76561190000000014UL, 76561190000000015UL, 76561190000000016UL, 76561190000000017UL };
			bool levelsBefore = PlayerLevels.On;
			IslandFile f;
			try { f = ScIsland(isl, "Crowded Isle"); } catch (Exception ex) { Fail("scenario many players: " + ex.Message); yield break; }
			PlayerLevels.SetEnabled(true); // (a world option: islands don't turn it on)
			for (int i = 0; i < 5; i++) f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(i * 8 - 16, -10), 10 + i), ObjectProps.NoteTitle, "Crate " + i, ObjectProps.LootItems, "Plank*1"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(0, 10), 20), BehaviourProps.Name, "lever", BehaviourProps.Use, "Pull", BehaviourProps.EventPrefix + "use", "switch|gate|"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(10, 10), 21), BehaviourProps.Name, "gate", BehaviourProps.Move, "0,3,0", BehaviourProps.MoveMode, "switch"));
			f.Objects.Add(ScObj("Creature_Boar", ScDry(f, new Vector2(-15, 15), 22), ObjectProps.CreatureCount, "1", ObjectProps.CreatureRespawn, "0"));
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(16, -20), 23), ObjectProps.NoteTitle, "Locked crate", ObjectProps.LootItems, "Plank*1",
				BehaviourProps.CheckKey("open"), "take|story:ciscmanykey|1", BehaviourProps.ElseKey("open"), "message||The crate is locked."));
			new IslandQuest { Title = "Crowd", Steps = { new IslandQuest.Step { Type = "open", Target = "", Count = 5 } } }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario many players: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario many players: the island didn't come"); yield break; }
			// (the cleanup runs when a part throws too - CB13)
			try
			{
				// (a) five players each open a different crate at the same moment: each machine sends the host its amount at step 0 (as players now do)
				for (int i = 0; i < 5; i++) QuestTracker.AddFromPlayer(e.Id, 0, 1);
				ObjectState prog;
				int progress = e.State.TryGetValue(QuestTracker.ProgressKey, out prog) ? prog.Yield : 0;
				Check(ref ok, QuestTracker.StepOf(e) >= 1 || progress == 5, "five players open five crates at once: the quest counts five (step " + QuestTracker.StepOf(e) + ", progress " + progress + ") - AU8");
				// (b) four players pull one lever in the same second: the gate should end open (one toggle wins), not toggled four times
				IslandObjectRef lever = ScObjOf(e, "lever");
				if (lever != null)
				{
					for (int i = 0; i < 4; i++) Behaviours.OnEventMessage(e.Id, lever.Index, "use", false);
					yield return new WaitForSeconds(1.5f);
					IslandObjectRef gate = ScObjOf(e, "gate");
					string state = (CIObjState(e, "gate") ?? "?");
					Check(ref ok, state.Contains("open"), "four players pull one lever in the same second: the gate is open (" + state + ") - AU18");
				}
				// (c) eight players hit one warthog: each gets their share, the shares add up to its EXP, one kill
				yield return ScWaitAnimals(e, "Warthog", 1, 20f);
				AI_NetworkBehaviour boar = ScAnimals(e, "Warthog").FirstOrDefault();
				if (boar != null && PlayerLevels.On)
				{
					int worth = PlayerLevels.MonsterXp(boar);
					float max = boar.networkEntity.stat_health.Max;
					// (the host works out each player's share as their hit arrives and sends it to that player's machine, which
					// keeps their record: what the host hands out is what is counted here)
					int sum = 0, kills = 0;
					foreach (ulong p in players)
					{
						sum += PlayerLevels.OnRemoteHit(boar.networkEntity, max / 7f + 0.01f, p);
						if ((PlayerLevels.LastRemote ?? "").StartsWith(p + " ") && PlayerLevels.LastRemote.EndsWith(" kill")) kills++;
						// (the hit itself, as Raft applies it after the message - through the host's DamageEntity, which a player's hit is)
						Network_Host host = ComponentManager<Network_Host>.Value;
						if (host != null && boar != null && boar.networkEntity != null && !boar.networkEntity.IsDead)
							boar.networkEntity.stat_health.Value = Mathf.Max(0f, boar.networkEntity.stat_health.Value - (max / 7f + 0.01f));
						yield return null;
					}
					Check(ref ok, sum >= worth - players.Length && sum <= worth + players.Length, "seven players hit one warthog: their EXP adds up to its " + worth + " (" + sum + ")");
					Check(ref ok, kills == 1, "... and only one of them gets the kill (" + kills + ")");
				}
				else Log("  (no warthog or the level up system is off: EXP part skipped)");
				// (d) eight builders' storages: each opened by its builder only
				var builders = new List<KeyValuePair<uint, ulong>>();
				for (int i = 0; i < players.Length; i++) builders.Add(new KeyValuePair<uint, ulong>((uint)(990100 + i), players[i]));
				string before2 = PrivateStorage.Encode();
				PrivateStorage.Decode(string.Join(";", builders.Select(b => b.Key + ":" + b.Value).ToArray()));
				bool optionsOn = WorldOptions.On(WorldOptions.PrivateStorage);
				if (!optionsOn) Log("  (Private storages is off in this world: every storage opens for everyone - checked as such)");
				int right = builders.Sum(b => players.Count(p => PrivateStorage.MayOpen(b.Key, p) == (!optionsOn || p == b.Value)));
				Check(ref ok, right == builders.Count * players.Length, "seven builders' storages: each opens only for its builder (" + right + " of " + builders.Count * players.Length + " right)");
				PrivateStorage.Decode(before2);
				// (e) a locked chest tried without the key (the host's own player, as anyone's machine does): it isn't claimed, so
				// the next player who comes with the key isn't told someone else got it first
				LootCrate lockedChest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(l => l.GetComponent<CustomNote>() != null && l.GetComponent<CustomNote>().Title == "Locked crate");
				if (lockedChest != null)
				{
					StoryBook.Take("ciscmanykey", 99);
					List<string> got = lockedChest.Open();
					int key = lockedChest.StateKey;
					bool nextGot = Claims.HostGrant(e, key, players[1]);
					Check(ref ok, got.Count == 0 && !lockedChest.Looted && nextGot, "a locked chest tried without the key isn't claimed: the next player with the key gets it (given " + got.Count + ", the next player " + (nextGot ? "granted" : "refused") + ") - AU41");
				}
				else Check(ref ok, false, "the locked crate is on the island");
			}
			finally
			{
				ScRemove(made, isl);
				if (!levelsBefore && PlayerLevels.On) PlayerLevels.TurnOff();
			}
			if (ok) Log("PASS: scenario many players"); else Fail("scenario many players");
		}

		/// <summary>How a named object is on this machine (as CIObj logs it): "shown"/"hidden" and "open"/"closed".</summary>
		static string CIObjState(IslandWorldState.Entry e, string name)
		{
			IslandObjectRef r = ScObjOf(e, name);
			if (r == null) return null;
			string s = r.gameObject.activeInHierarchy ? "shown" : "hidden";
			ObjectState st;
			int k = Behaviours.StateBase + r.Index;
			if (e.State.TryGetValue(k, out st)) s += st.Yield == 1 ? ", open" : ", closed";
			else s += ", closed";
			return s;
		}

		#endregion
	}
}
