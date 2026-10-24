using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using DynamicIslands.Editor;

namespace DynamicIslands
{
	/// <summary>
	/// Two-player tests of the storyline builder (TEST_CATALOGUE SL6-SL9, SL12; tools\mpstoryline.ps1): several quests on one
	/// island done by both players, a reward kept for a player who was far away (also over a restart), a world plan edited
	/// mid-story, odd characters in story texts, and steps done early by player 2. Every state command prints one line that the
	/// script compares between the two players (SLQ, SLB, SLO) and a PASS/FAIL against what the script expects.
	/// </summary>
	public static partial class DevTests
	{
		const string SmMq = "cislmq", SmEarly = "cislearly", SmOdd = "cisloddtext", SmBookPlan = "CI SL Book";

		static string SmRole { get { return Raft_Network.IsHost ? "host" : "player 2"; } }

		static IslandWorldState.Entry SmEntry(string name) { return IslandWorldState.Islands.FirstOrDefault(x => x.HostName == name); }

		static bool SmInTestWorld() { return LoadSceneManager.IsGameSceneLoaded && (!Raft_Network.IsHost || (SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")); }

		static string SmEsc(string s) { return Uri.EscapeDataString(s ?? ""); }

		/// <summary>The islands, as player 2 gets them: the host brings the island (kept, never removed by a unload), player 2 waits until its copy is loaded.</summary>
		static IEnumerator SmWaitHere(string name, float seconds)
		{
			for (float t = 0; t < seconds && (SmEntry(name) == null || SmEntry(name).Root == null); t += 0.5f) yield return new WaitForSeconds(0.5f);
		}

		/// <summary>This player to the island's land (via its edge first, so that an unloaded island comes), then onto the land.</summary>
		static IEnumerator SmGoto(string name)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			IslandWorldState.Entry e = SmEntry(name);
			if (p == null || e == null) { Fail("goto: no player or island '" + name + "'"); yield break; }
			CustomIslandSpawner.TestRaftFarAway = false;
			if (e.Root == null)
			{
				Vector3 edge = e.Position + new Vector3(CustomIslandSpawner.LandRadius(e.Name) + 8f, 0f, 0f);
				edge.y = 0.5f;
				PlayerMove.To(p, edge, ControllerType.Water);
				KeepAlive(p);
				yield return SmWaitHere(name, 40f);
			}
			if (e.Root == null) { Fail("goto: '" + name + "' did not load here"); yield break; }
			yield return new WaitForSeconds(1f);
			PlayerMove.To(p, LandSpot(e) + Vector3.up * 1.5f);
			KeepAlive(p);
			yield return new WaitForSeconds(1.5f);
			Log("SL went to '" + name + "' (" + SmRole + ")");
		}

		static int SmProgress(IslandWorldState.Entry e, int n)
		{
			ObjectState s;
			return e.State.TryGetValue(n == 0 ? QuestTracker.ProgressKey : 0x40201 + n * 2, out s) ? s.Yield : 0;
		}

		static int SmEarlyKey(int n, int step) { return n == 0 ? QuestTracker.EarlyKeyBase + step : 0x40300 + n * 0x40 + Mathf.Min(step, 0x3F); }

		static int SmCount(string item)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			return p != null && p.Inventory != null ? p.Inventory.GetItemCount(item) : -1;
		}

		static string SmRewardKey(IslandWorldState.Entry e, int n) { string island = StoryBook.PageIsland(e); return n == 0 ? island : island + "#quest" + (n + 1); }

		#region Setup and cleanup

		[ConsoleCommand(name: "CISLMq", docs: "Dev, world, two players (mpstoryline.ps1): CISLMq <token> - host: brings 'cislmq' (3 quests: Main = reach zone slz1; Second = reach zone slzone, read note 'SL Diary', collect 3 of story item slk<token> -> Plank*4; Third = reach slz3 -> Rope*3; Fourth = reach slz4 -> Scrap*3); player 2: waits for its copy, forgets its rewards of the island")]
		public static void SmMqCommand(string[] args) { StartTest(SmMqRoutine(args != null && args.Length > 0 ? args[0] : "0")); }

		static IEnumerator SmMqRoutine(string tok)
		{
			if (!SmInTestWorld()) { Fail("sl mq: in a world ('CI ...' as the host)"); yield break; }
			string item = "slk" + tok;
			QuestRewards.ForgetIsland(SmMq);
			if (Raft_Network.IsHost)
			{
				yield return EnsureAlive();
				var spots = new List<Vector3>();
				yield return ScMpSpawn(SmMq, "SL Isle", f =>
				{
					f.Objects.Add(ScObj(ContentCatalog.TriggerZone, SmDryApart(f, new Vector2(14, 0), 1, spots), ObjectProps.ZoneId, "slz1", ObjectProps.ZoneRadius, "4"));
					f.Objects.Add(ScObj(ContentCatalog.TriggerZone, SmDryApart(f, new Vector2(-14, 0), 2, spots), ObjectProps.ZoneId, "slzone", ObjectProps.ZoneRadius, "4"));
					f.Objects.Add(ScObj(ContentCatalog.TriggerZone, SmDryApart(f, new Vector2(0, 16), 3, spots), ObjectProps.ZoneId, "slz3", ObjectProps.ZoneRadius, "4"));
					f.Objects.Add(ScObj(ContentCatalog.TriggerZone, SmDryApart(f, new Vector2(0, -16), 4, spots), ObjectProps.ZoneId, "slz4", ObjectProps.ZoneRadius, "4"));
					f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(12, 12), 5), ObjectProps.NoteTitle, "SL Diary", ObjectProps.NoteText, "The keys are three. Bring them to the camp."));
					f.Props[StoryItems.Key] = item + "|SL Key||A key to the second camp.";
					new IslandQuest { Title = "SL Main", Steps = { new IslandQuest.Step { Type = "reach", Target = "slz1", Count = 1 } } }.To(f.Props, 0);
					new IslandQuest { Title = "SL Second", Reward = "Plank*4", Steps = {
						new IslandQuest.Step { Type = "reach", Target = "slzone", Count = 1 },
						new IslandQuest.Step { Type = "read", Target = "SL Diary", Count = 1 },
						new IslandQuest.Step { Type = "collect", Target = "story:" + item, Count = 3 } } }.To(f.Props, 1);
					new IslandQuest { Title = "SL Third", Reward = "Rope*3", Steps = { new IslandQuest.Step { Type = "reach", Target = "slz3", Count = 1 } } }.To(f.Props, 2);
					new IslandQuest { Title = "SL Fourth", Reward = "Scrap*3", Steps = { new IslandQuest.Step { Type = "reach", Target = "slz4", Count = 1 } } }.To(f.Props, 3);
				});
				if (SmEntry(SmMq) == null || SmEntry(SmMq).Root == null) yield break;
				QuestRewards.ForgetIsland(SmMq);
			}
			else
			{
				yield return SmWaitHere(SmMq, 60f);
				if (SmEntry(SmMq) == null || SmEntry(SmMq).Root == null) { Fail("sl mq: player 2 has no copy of '" + SmMq + "'"); yield break; }
			}
			IslandWorldState.Entry e = SmEntry(SmMq);
			Log("PASS: sl mq " + SmRole + " (" + QuestTracker.QuestsOf(e) + " quests)");
		}

		[ConsoleCommand(name: "CISLEarly", docs: "Dev, world, two players (mpstoryline.ps1): host: brings 'cislearly' (Quest 1: read notes 'SL Alpha', 'SL Beta', 'SL Gamma' in this order; Quest 2: reach zones e2a, e2b, e2c in this order); player 2: waits for its copy")]
		public static void SmEarlySetupCommand() { StartTest(SmEarlySetupRoutine()); }

		/// <summary>
		/// A dry spot (ScDry) at least SmApart metres across from the spots already taken: the 4 m zones and the notes
		/// of one test must not overlap, or entering one fires another as well (the dry-spot search reaches 30 m out).
		/// </summary>
		static Vector3 SmDryApart(IslandFile f, Vector2 offset, int seed, List<Vector3> taken)
		{
			Vector3 best = ScDry(f, offset, seed);
			float bestGap = -1f;
			for (int tries = 0; tries < 24; tries++)
			{
				float a = tries * 2.4f, r = tries == 0 ? 0f : 4f + tries * 1.5f;
				Vector3 p = ScDry(f, offset + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, seed + tries * 37);
				float gap = taken.Count == 0 ? float.MaxValue : taken.Min(q => new Vector2(p.x - q.x, p.z - q.z).magnitude);
				if (gap > bestGap) { best = p; bestGap = gap; }
				if (gap >= SmApart) break;
			}
			if (bestGap < SmApart && taken.Count > 0) Log("  (setup: only " + bestGap.ToString("0.0") + " m from the nearest spot on this island)");
			taken.Add(best);
			return best;
		}

		const float SmApart = 10f;

		static IEnumerator SmEarlySetupRoutine()
		{
			if (!SmInTestWorld()) { Fail("sl early: in a world ('CI ...' as the host)"); yield break; }
			QuestRewards.ForgetIsland(SmEarly);
			if (Raft_Network.IsHost)
			{
				yield return EnsureAlive();
				var taken = new List<Vector3>();
				yield return ScMpSpawn(SmEarly, "SL Early Isle", f =>
				{
					string[] names = { "SL Alpha", "SL Beta", "SL Gamma" };
					for (int i = 0; i < 3; i++)
					{
						f.Objects.Add(ScObj("Note_Paper", SmDryApart(f, new Vector2(-14 + i * 14, 12), 10 + i, taken), ObjectProps.NoteTitle, names[i], ObjectProps.NoteText, "Page " + names[i] + ": go on."));
						f.Objects.Add(ScObj(ContentCatalog.TriggerZone, SmDryApart(f, new Vector2(-14 + i * 14, -14), 20 + i, taken), ObjectProps.ZoneId, "e2" + "abc"[i], ObjectProps.ZoneRadius, "4"));
					}
					new IslandQuest { Title = "SL Early One", Steps = {
						new IslandQuest.Step { Type = "read", Target = "SL Alpha", Count = 1 },
						new IslandQuest.Step { Type = "read", Target = "SL Beta", Count = 1 },
						new IslandQuest.Step { Type = "read", Target = "SL Gamma", Count = 1 } } }.To(f.Props, 0);
					new IslandQuest { Title = "SL Early Two", Steps = {
						new IslandQuest.Step { Type = "reach", Target = "e2a", Count = 1 },
						new IslandQuest.Step { Type = "reach", Target = "e2b", Count = 1 },
						new IslandQuest.Step { Type = "reach", Target = "e2c", Count = 1 } } }.To(f.Props, 1);
				});
			}
			else yield return SmWaitHere(SmEarly, 60f);
			IslandWorldState.Entry e = SmEntry(SmEarly);
			if (e == null || e.Root == null) { Fail("sl early: no island here (" + SmRole + ")"); yield break; }
			Log("PASS: sl early " + SmRole + " (" + QuestTracker.QuestsOf(e) + " quests)");
		}

		[ConsoleCommand(name: "CISLEnd", docs: "Dev, world, two players (mpstoryline.ps1): ends a storyline part on this machine - far-away test off, back on the raft, the rewards of the test islands forgotten; the host also takes the test islands (cislmq, cislearly, cisloddtext) and their files away")]
		public static void SmEndCommand() { StartTest(SmEndRoutine()); }

		static IEnumerator SmEndRoutine()
		{
			CustomIslandSpawner.TestRaftFarAway = false;
			string[] names = { SmMq, SmEarly, SmOdd };
			foreach (string n in names) QuestRewards.ForgetIsland(n);
			if (Raft_Network.IsHost && LoadSceneManager.IsGameSceneLoaded)
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => names.Contains(x.HostName)).Select(x => x.Id).ToList(), true);
				foreach (string n in names) { try { if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n)); } catch { } }
				IslandCache.Forget();
			}
			if (LoadSceneManager.IsGameSceneLoaded) OnRaftCommand();
			yield return new WaitForSeconds(0.5f);
			Log("PASS: sl end " + SmRole);
		}

		#endregion

		#region Acts of one player

		[ConsoleCommand(name: "CISLAct", docs: "Dev, world, two players (either player): CISLAct goto <island> | zone <island> <zone id> | note <island> <note title> | collect <story item id> <n> - this player walks to the island's land, into a zone, reads a note (the real paths), or finds story items (sent to the host)")]
		public static void SmActCommand(string[] args) { StartTest(SmActRoutine(args ?? new string[0])); }

		static IEnumerator SmActRoutine(string[] a)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || a.Length == 0) { Fail("sl act: in a world, with goto|zone|note|collect"); yield break; }
			yield return EnsureAlive();
			switch (a[0])
			{
				case "goto":
					yield return SmGoto(a.Length > 1 ? a[1] : SmMq);
					break;
				case "zone":
				case "note":
				{
					IslandWorldState.Entry e = a.Length > 2 ? SmEntry(a[1]) : null;
					if (e == null || e.Root == null) { Fail("sl act " + a[0] + ": island '" + (a.Length > 1 ? a[1] : "") + "' isn't loaded here"); yield break; }
					string what = string.Join(" ", a.Skip(2).ToArray());
					if (a[0] == "note")
					{
						ScReadNote(e, what);
						yield return new WaitForSeconds(2f);
					}
					else
					{
						IslandObjectRef r = e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(o => string.Equals(ObjectProps.Get(o.Props, ObjectProps.ZoneId), what, StringComparison.OrdinalIgnoreCase));
						if (r == null) { Fail("sl act zone: no zone '" + what + "' on '" + a[1] + "'"); yield break; }
						PutPlayerNear(r.transform, 0.3f);
						StartTest(StayThere(r.transform));
						yield return new WaitForSeconds(3.5f);
					}
					Log("SL " + a[0] + " '" + what + "' on '" + a[1] + "' (" + SmRole + ")");
					break;
				}
				case "collect":
				{
					int n;
					if (a.Length < 3 || !int.TryParse(a[2], out n)) { Fail("sl act collect: <id> <n>"); yield break; }
					StoryBook.Give(a[1], n);
					yield return new WaitForSeconds(2f);
					Log("SL found " + n + " x " + a[1] + " (" + SmRole + ")");
					break;
				}
				default:
					Fail("sl act: goto|zone|note|collect");
					yield break;
			}
			Log("PASS: sl act " + a[0] + " " + SmRole);
		}

		#endregion

		#region State of the quests and rewards (SL7, SL8, SL9)

		[ConsoleCommand(name: "CISLState", docs: "Dev, world, two players (either player): CISLState <island> [want] [token] [early want] - prints SLQ (every quest's step/progress and the story item count/found: the same on both players), SLR (this player's own reward state, record file lines and Plank/Rope/Scrap counts), SLE (host: the early marks); want = '1=0/0,2=1/0' (quest=step/progress) is checked; early want = 'q1s2=1,q1s3=1' or 'none' (host only)")]
		public static void SmStateCommand(string[] args) { StartTest(SmStateRoutine(args ?? new string[0])); }

		static IEnumerator SmStateRoutine(string[] a)
		{
			string name = a.Length > 0 ? a[0] : SmMq;
			string want = a.Length > 1 ? a[1] : "-";
			string tok = a.Length > 2 ? a[2] : "-";
			string earlyWant = a.Length > 3 ? a[3] : "-";
			IslandWorldState.Entry e = SmEntry(name);
			if (e == null) { Fail("sl state: no island '" + name + "' in this world (" + SmRole + ")"); yield break; }
			yield return new WaitForSeconds(0.2f);
			int quests = QuestTracker.QuestsOf(e);
			var qs = new List<string>();
			for (int n = 0; n < quests; n++) qs.Add((n + 1) + "=" + QuestTracker.StepOf(e, n) + "/" + SmProgress(e, n));
			string id = "slk" + tok;
			string items = tok != "-" ? id + "=" + StoryBook.Count(id) + "/" + StoryBook.FoundCount(id) : "-";
			Log("SLQ " + SmRole + ": " + string.Join(" ", qs.ToArray()) + " | items " + items);
			bool ok = true;
			if (want != "-")
				foreach (string w in want.Split(',')) Check(ref ok, qs.Contains(w), SmRole + ": quest " + w + " (have " + string.Join(" ", qs.ToArray()) + ")");

			var rw = new List<string>();
			for (int n = 0; n < quests; n++)
			{
				string k = SmRewardKey(e, n);
				rw.Add((n + 1) + ":" + (QuestRewards.Owed(k) ? "owed" : QuestRewards.Rewarded(k) ? "paid" : "-"));
			}
			string file = "";
			try
			{
				string path = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), QuestRewards.WhereKept);
				if (File.Exists(path)) file = string.Join(",", File.ReadAllLines(path).Where(l => l.EndsWith(" " + name) || l.IndexOf(" " + name + "#", StringComparison.OrdinalIgnoreCase) >= 0).ToArray());
			}
			catch (Exception ex) { file = "unreadable " + ex.Message; }
			Log("SLR " + SmRole + ": rewards " + string.Join(" ", rw.ToArray()) + " | file [" + file + "] | Plank=" + SmCount("Plank") + " Rope=" + SmCount("Rope") + " Scrap=" + SmCount("Scrap"));

			var marks = new List<string>();
			for (int n = 0; n < quests; n++)
				for (int s = 0; s < 12; s++)
				{
					ObjectState st;
					if (e.State.TryGetValue(SmEarlyKey(n, s), out st) && st.Yield > 0) marks.Add("q" + (n + 1) + "s" + (s + 1) + "=" + st.Yield);
				}
			string m = marks.Count > 0 ? string.Join(",", marks.ToArray()) : "none";
			if (Raft_Network.IsHost) Log("SLE host: early marks " + m);
			if (Raft_Network.IsHost && earlyWant != "-") Check(ref ok, m == earlyWant, "host: early marks '" + m + "' (wanted '" + earlyWant + "')");
			if (ok) Log("PASS: sl state " + SmRole); else Fail("sl state " + SmRole);
		}

		#endregion

		#region SL6: odd characters in story texts

		const string SmOddItemId = "sloddkey", SmOddName = "Odd key 100% a=b å ö \U0001F600", SmOddDesc = "Opens it | 5% x=y ö \U0001F600",
			SmOddTitle = "Odd page | 100% a=b åö \U0001F600", SmOddText = "First line | 50% a=b\nSecond line å ö \U0001F600";

		[ConsoleCommand(name: "CISLOdd", docs: "Dev, world, two players (mpstoryline.ps1): SL6 - CISLOdd setup (host: brings 'cisloddtext': a note and a chest with a story item whose texts hold |, %, =, a line break, a/o with rings and dots and an emoji) | open (host: opens the chest) | read (any player: reads the note) | check (any player: this player's journal has the item and the page exactly as written; prints SLO, the same on both)")]
		public static void SmOddCommand(string[] args) { StartTest(SmOddRoutine(args != null && args.Length > 0 ? args[0] : "check")); }

		static IEnumerator SmOddRoutine(string what)
		{
			if (!SmInTestWorld()) { Fail("sl odd: in a world ('CI ...' as the host)"); yield break; }
			bool ok = true;
			IslandWorldState.Entry e = SmEntry(SmOdd);
			switch (what)
			{
				case "setup":
					if (!Raft_Network.IsHost) { yield return SmWaitHere(SmOdd, 60f); e = SmEntry(SmOdd); Check(ref ok, e != null && e.Root != null, "player 2 has its copy of the odd-text island"); break; }
					yield return EnsureAlive();
					yield return ScMpSpawn(SmOdd, "Odd Isle", f =>
					{
						f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(-10, 0), 1), ObjectProps.NoteTitle, SmOddTitle, ObjectProps.NoteText, SmOddText));
						f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(10, 0), 2), ObjectProps.NoteTitle, "SL Odd Chest", ObjectProps.LootItems, "story:" + SmOddItemId + "*1", ObjectProps.LootRefill, "0"));
						f.Props[StoryItems.Key] = SmOddItemId + "|" + SmOddName + "||" + SmOddDesc;
					});
					e = SmEntry(SmOdd);
					Check(ref ok, e != null && e.Root != null, "the odd-text island is here");
					break;
				case "open":
					if (e == null || e.Root == null) { Fail("sl odd open: the island isn't here"); yield break; }
					StoryBook.Reset();
					ScOpenChest(e, "SL Odd Chest");
					yield return new WaitForSeconds(2.5f);
					break;
				case "read":
					if (e == null || e.Root == null) { Fail("sl odd read: the island isn't here"); yield break; }
					ScReadNote(e, "Odd page");
					yield return new WaitForSeconds(2.5f);
					break;
				case "check":
				{
					StoryBook.Held item = StoryBook.Items.FirstOrDefault(h => h.Def.Id == SmOddItemId);
					StoryBook.Page page = StoryBook.Pages.FirstOrDefault(p => p.Key.StartsWith("note:" + SmOdd + ":"));
					Log("SLO " + SmRole + ": item " + (item != null ? SmEsc(item.Def.Name) + " / " + SmEsc(item.Def.Description) + " x" + item.Count : "none") + " | page " + (page != null ? SmEsc(page.Title) + " / " + SmEsc(page.Text) : "none"));
					Check(ref ok, item != null && item.Def.Name == SmOddName, SmRole + ": the item's name as written (" + (item != null ? SmEsc(item.Def.Name) : "none") + ")");
					Check(ref ok, item != null && item.Def.Description == SmOddDesc, SmRole + ": the item's description as written (" + (item != null ? SmEsc(item.Def.Description) : "none") + ")");
					Check(ref ok, page != null && page.Title == SmOddTitle, SmRole + ": the page's title as written (" + (page != null ? SmEsc(page.Title) : "none") + ")");
					Check(ref ok, page != null && page.Text.Replace("\r", "") == SmOddText, SmRole + ": the page's text as written, line break and all (" + (page != null ? SmEsc(page.Text) : "none") + ")");
					break;
				}
				default:
					Fail("CISLOdd setup | open | read | check");
					yield break;
			}
			if (ok) Log("PASS: sl odd " + what + " " + SmRole); else Fail("sl odd " + what + " " + SmRole);
		}

		#endregion

		#region SL12: the main story plan edited mid-story

		const string SmBookText = "random = off\nstory = off\nstoryending = The end test.\\nThank you.\n" +
			"rule = a | type:camp | start | receiver:300 | | Camp A | first | visit | Alpha Camp | 9 | Hello\\nworld\n" +
			"rule = b | type:sandbar | start | receiver:400 | | Bar | after:a | visit | Beta Bar | 4\n" +
			"rule = c | type:sandbar | start | receiver:400 | | Cove | after:b | visit | Gamma Cove | 6\n";

		[ConsoleCommand(name: "CISLBook", docs: "Dev, world, two players (mpstoryline.ps1): SL12 - CISLBook setup (host, test world: the plan 'CI SL Book' with a 3-tab main story is the world's plan, the first island brought; then CIQuestBookMP note / done a / done b) | edit (host, anywhere: the plan file changed - tab 'Alpha Camp' renamed 'Camp Alpha', the not yet reached rule c removed) | check <tabs> [edited] (any player: the book as one line SLB - tabs, note, ending, and a flip to every tab with the page it lands on; the same on both)")]
		public static void SmBookCommand(string[] args) { StartTest(SmBookRoutine(args ?? new string[0])); }

		static IEnumerator SmBookRoutine(string[] a)
		{
			string what = a.Length > 0 ? a[0] : "check";
			bool ok = true;
			switch (what)
			{
				case "setup":
				{
					if (!Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("sl book setup: host, in a test world 'CI ...'"); yield break; }
					WorldPlan.Parse(SmBookPlan, SmBookText).Save();
					WorldDirector.Done.Clear();
					StoryChain.Reset();
					Check(ref ok, ScSetPlan(SmBookPlan, true), "the world plays '" + SmBookPlan + "'");
					StoryChain.Tick();
					StoryChain.OnTuned(StoryChain.TypeOfRule("a"));
					yield return WaitFor(() => IslandWorldState.Islands.Any(x => x.Rule == "a"), 60f);
					Check(ref ok, IslandWorldState.Islands.Any(x => x.Rule == "a"), "the first island of the main story came");
					IslandWorldState.Save();
					break;
				}
				case "edit":
				{
					if (!Raft_Network.IsHost) { Fail("sl book edit: the host"); yield break; }
					WorldPlan p = WorldPlan.Load(SmBookPlan);
					if (p == null) { Fail("sl book edit: no plan '" + SmBookPlan + "'"); yield break; }
					IntroRule ra = p.Rules.FirstOrDefault(r => r.Id == "a");
					Check(ref ok, ra != null && ra.TabTitle == "Alpha Camp" && p.Rules.Any(r => r.Id == "c"), "the plan file has tab 'Alpha Camp' and rule c before the edit");
					if (ra != null) ra.TabTitle = "Camp Alpha";
					p.Rules.RemoveAll(r => r.Id == "c");
					p.Save();
					WorldPlan back = WorldPlan.Load(SmBookPlan);
					Check(ref ok, back != null && back.Rules.Count == 2 && back.Rules[0].TabTitle == "Camp Alpha" && back.Id == p.Id, "the edited plan file: 2 rules, tab 'Camp Alpha', same plan id");
					yield return null;
					break;
				}
				case "check":
				{
					if (!LoadSceneManager.IsGameSceneLoaded) { Fail("sl book check: in a world"); yield break; }
					int tabsWanted = -1;
					if (a.Length > 1) int.TryParse(a[1], out tabsWanted);
					yield return new WaitForSeconds(1f);
					QuestBook.Refresh();
					yield return new WaitForSeconds(1f);
					string tabs = string.Join(";", QuestBook.Tabs.ToArray());
					bool note = QuestBook.PageTexts.Values.Any(v => v.Any(t => t.Contains("Dear diary")));
					bool end = (QuestBook.Layout ?? "").Contains("The end[");
					var flips = new List<string>();
					NoteBookUI ui = LocalNoteBookUI();
					if (ui != null)
					{
						if (!ui.isDisplayed) ui.SetBookActive(true);
						yield return new WaitForSeconds(0.4f);
						foreach (string tab in QuestBook.Tabs.ToList())
						{
							string title = tab.Split('|')[0];
							QuestBook.PressTab(title);
							yield return new WaitForSeconds(0.4f);
							uint at = Traverse.Create(ui).Field("currentPageIndex").GetValue<uint>();
							flips.Add(title + "@" + at);
							Check(ref ok, at == QuestBook.PageOfTab(title), SmRole + ": a flip to tab '" + title + "' lands on its page (" + at + ", the tab says " + QuestBook.PageOfTab(title) + ")");
						}
						ui.SetBookActive(false);
					}
					Log("SLB " + tabs + " | note " + note + " | end " + end + " | flips " + (flips.Count > 0 ? string.Join(",", flips.ToArray()) : "none"));
					if (tabsWanted >= 0) Check(ref ok, QuestBook.Tabs.Count == tabsWanted, SmRole + ": " + QuestBook.Tabs.Count + " tabs, " + tabsWanted + " wanted");
					if (a.Length > 2 && a[2] == "edited" && Raft_Network.IsHost) Check(ref ok, WorldDirector.PlanWasEdited, "host: the world played the changed plan (PlanWasEdited)");
					break;
				}
				default:
					Fail("CISLBook setup | edit | check <tabs> [edited]");
					yield break;
			}
			if (ok) Log("PASS: sl book " + what + " " + SmRole); else Fail("sl book " + what + " " + SmRole);
		}

		#endregion
	}
}
