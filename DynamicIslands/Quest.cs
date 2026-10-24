using System;
using System.Collections.Generic;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// An island's quest (the quest editor, Island tab): a title, an introduction, steps done in order, a reward and a
	/// closing message. Stored with the island's settings (IslandFile.Props, "quest.*"). Steps point at things the
	/// island already has, by name:
	///   reach  - a trigger zone (its name)             read  - a note (its title)
	///   open   - a chest (its note title; empty = any)  kill  - creatures of a kind ("Warthog"), a number of them
	///   catch  - a catchable animal of a kind ("Llama")
	///   collect - the crew holds a number of a story item ("story:map-piece", 5 of them)
	///   pages  - the crew's journal has a number of pages found on this island (target "all": on any island)
	/// </summary>
	public class IslandQuest
	{
		public const string KeyTitle = "quest.title", KeyIntro = "quest.intro", KeySteps = "quest.steps", KeyReward = "quest.reward", KeyDone = "quest.done";
		public static readonly string[] Types = { "reach", "read", "open", "kill", "catch", "collect", "pages", "blueprint" };

		/// <summary>Steps the crew's story book counts (the host checks them), not events.</summary>
		public static bool Counted(string type) { return type == "collect" || type == "pages"; }

		public class Step
		{
			public string Type = "reach", Target = "", Text = "";
			public int Count = 1;

			/// <summary>What the player is told to do: the builder's text, or one made from the step.</summary>
			public string Describe()
			{
				if (Text.Length > 0) return Text;
				switch (Type)
				{
					case "reach": return Target.Length > 0 ? "Go to " + Target : "Explore the island";
					case "read": return Target.Length > 0 ? "Read \"" + Target + "\"" : "Read a note";
					case "open": return Target.Length > 0 ? "Open \"" + Target + "\"" : "Open a chest";
					case "kill": return "Defeat " + (Count > 1 ? Count + " " : "a ") + (Target.Length > 0 ? Target.ToLowerInvariant() + (Count > 1 ? "s" : "") : "creature" + (Count > 1 ? "s" : ""));
					case "catch": return "Catch " + (Count > 1 ? Count + " " : "a ") + (Target.Length > 0 ? Target.ToLowerInvariant() + (Count > 1 ? "s" : "") : "animal" + (Count > 1 ? "s" : ""));
					case "collect": return "Find " + (Count > 1 ? Count + " \u00D7 " : "") + (Target.Length > 0 ? StoryItems.Label(Target) : "a story item");
					case "pages": return "Find " + (Count > 1 ? Count + " pages" : "a page") + (Target == "all" ? " (on any island)" : " on this island");
					case "blueprint": return Target.Length > 0 ? "Find the " + PlanChecker.Pretty(Target.Trim()) + " blueprint" : "Find one of Raft's blueprints";
				}
				return Type;
			}
		}

		public string Title = "", Intro = "", Reward = "", Done = "";
		public List<Step> Steps = new List<Step>();

		public bool Exists { get { return Steps.Count > 0; } }

		static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace("\n", " ").Trim(); }

		/// <summary>Most quests an island has: its main quest and up to eight more (ROADMAP LM4).</summary>
		public const int MaxQuests = 9;

		/// <summary>True when any of the island's quests (0..MaxQuests-1) has steps: only such islands announce themselves with a banner.</summary>
		public static bool HasQuest(IDictionary<string, string> props)
		{
			for (int i = 0; i < MaxQuests; i++) if (ObjectProps.Get(props, Key(KeySteps, i)).Trim().Length > 0) return true;
			return false;
		}

		/// <summary>The settings' key of quest n (0 = the main quest, "quest.title"; 1 = "quest2.title" ...).</summary>
		public static string Key(string key, int n) { return n == 0 ? key : "quest" + (n + 1) + key.Substring("quest".Length); }

		/// <summary>How many quests the island has (the main one counts, with steps or not; then those with steps).</summary>
		public static int CountIn(IDictionary<string, string> props)
		{
			int n = 1;
			for (int i = 1; i < MaxQuests; i++) if (ObjectProps.Get(props, Key(KeySteps, i)).Trim().Length > 0) n = i + 1;
			return n;
		}

		public static IslandQuest From(IDictionary<string, string> props) { return From(props, 0); }

		/// <summary>
		/// Quest n of the island's settings. Its texts are made plain for showing (an island's "&lt;size=300&gt;" title covered
		/// the quest panel - ROADMAP X6); raw keeps them as written, for the quest editor, which writes them back (review
		/// 2026-10-06: saving in the editor stripped every quest's formatting and cut long texts).
		/// </summary>
		/// <summary>Steps the quest editor adds up to (a recipe's quest has more: the library's finales run to 40).</summary>
		public const int MaxStepCount = 10;

		/// <summary>Steps a quest file is read up to: a hand-edited file with thousands is cut there. (Cut at the editor's 10
		/// for a day, it cut every long library quest short - Ironreef's play, X7 2026-10-13.)</summary>
		public const int MaxFileSteps = 99;

		public static IslandQuest From(IDictionary<string, string> props, int n, bool raw = false)
		{
			Func<string, int, string> text = (s, max) => raw ? s : LibraryInfo.Plain(s, max);
			var q = new IslandQuest
			{
				Title = text(ObjectProps.Get(props, Key(KeyTitle, n)), 200), Intro = text(ObjectProps.Get(props, Key(KeyIntro, n)), 4000),
				Reward = text(ObjectProps.Get(props, Key(KeyReward, n)), 4000), Done = text(ObjectProps.Get(props, Key(KeyDone, n)), 4000)
			};
			foreach (string line in ObjectProps.Get(props, Key(KeySteps, n)).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string[] p = line.Split('|');
				if (p.Length < 1 || !Types.Contains(p[0])) continue;
				if (q.Steps.Count >= MaxFileSteps) break; // (a hand-edited file with more: cut there)
				int count;
				q.Steps.Add(new Step { Type = p[0], Target = p.Length > 1 ? p[1] : "", Count = p.Length > 2 && int.TryParse(p[2], out count) ? Mathf.Clamp(count, 1, 99) : 1, Text = p.Length > 3 ? text(p[3], 400) : "" });
			}
			return q;
		}

		/// <summary>Writes the quest into island settings (an empty quest removes the keys).</summary>
		public void To(IDictionary<string, string> props) { To(props, 0); }

		public void To(IDictionary<string, string> props, int n)
		{
			foreach (string k in new[] { KeyTitle, KeyIntro, KeySteps, KeyReward, KeyDone }) props.Remove(Key(k, n));
			if (!Exists) return;
			if (Title.Trim().Length > 0) props[Key(KeyTitle, n)] = Title.Trim();
			if (Intro.Trim().Length > 0) props[Key(KeyIntro, n)] = Intro.Trim();
			if (Reward.Length > 0) props[Key(KeyReward, n)] = Reward;
			if (Done.Trim().Length > 0) props[Key(KeyDone, n)] = Done.Trim();
			props[Key(KeySteps, n)] = string.Join("\n", Steps.Select(s => s.Type + "|" + Clean(s.Target) + "|" + s.Count + "|" + Clean(s.Text)).ToArray());
		}

		public string ShownTitle { get { return Title.Trim().Length > 0 ? Title.Trim() : "Quest"; } }
	}

	/// <summary>
	/// Quests in a world. Every machine notices what its own player does (walks into a zone, reads a note, opens a
	/// chest); the host notices killed and caught animals. The step reached is kept in the island's state (saved with
	/// the world, sent to players who join) and sent to everyone (IslandNetMessage.QuestStep). A panel shows the quest
	/// while the player is near its island; when it's done, every player near the island gets the reward.
	/// </summary>
	public static class QuestTracker
	{
		/// <summary>State keys: the step reached (Yield), and progress on counted steps (Yield).</summary>
		public const int StepKey = 0x40000, ProgressKey = 0x40001;
		const float NearDistance = 150f;
		/// <summary>How close (beyond the land) the quest panel and introduction first show; they stay until NearDistance
		/// (the user, 2026-10-07: at 150 m "you always get notified", islands should need looking around for).</summary>
		const float ShowDistance = 70f;
		/// <summary>The island whose panel is up (kept until the player is NearDistance away), or -1.</summary>
		static int panelIsland = -1;

		/// <summary>Raised on every machine when a quest moves on (tests listen): island id, new step (== steps count when done).</summary>
		public static event Action<int, int> Advanced;

		/// <summary>The last quest message shown (tests look at it).</summary>
		public static string LastMessage { get; private set; }

		/// <summary>The island's quest (also while it's unloaded here: a client may be at it while the host is far away).</summary>
		public static IslandQuest QuestOf(IslandWorldState.Entry e) { return QuestOf(e, 0); }

		/// <summary>The island's quest n (0 = the main quest).</summary>
		public static IslandQuest QuestOf(IslandWorldState.Entry e, int n)
		{
			return e != null ? IslandQuest.From(IslandCache.PropsOf(e), n) : new IslandQuest();
		}

		/// <summary>How many quests the island has (ROADMAP LM4).</summary>
		public static int QuestsOf(IslandWorldState.Entry e) { return e != null ? IslandQuest.CountIn(IslandCache.PropsOf(e)) : 0; }

		// State keys of quest n: the main quest's as before; the others after the main quest's early work (0x40100 + step)
		static int StepKeyOf(int n) { return n == 0 ? StepKey : 0x40200 + n * 2; }
		static int ProgressKeyOf(int n) { return n == 0 ? ProgressKey : 0x40201 + n * 2; }
		static int EarlyKeyOf(int n, int step) { return n == 0 ? EarlyKeyBase + step : 0x40300 + n * 0x40 + Mathf.Min(step, 0x3F); }

		public static int StepOf(IslandWorldState.Entry e) { return StepOf(e, 0); }

		public static int StepOf(IslandWorldState.Entry e, int n)
		{
			ObjectState s;
			return e != null && e.State.TryGetValue(StepKeyOf(n), out s) ? Mathf.Max(0, s.Yield) : 0; // (a hand-edited world file may hold a negative step - CB12)
		}

		static int ProgressOf(IslandWorldState.Entry e, int n = 0)
		{
			ObjectState s;
			return e != null && e.State.TryGetValue(ProgressKeyOf(n), out s) ? s.Yield : 0;
		}

		/// <summary>Whether quest n of the island is done (it has steps, all done).</summary>
		public static bool IsDone(IslandWorldState.Entry e, int n)
		{
			IslandQuest q = QuestOf(e, n);
			return q.Exists && StepOf(e, n) >= q.Steps.Count;
		}

		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		/// <summary>Something happened on an island that a quest step may be waiting for.</summary>
		public static void Event(IslandWorldState.Entry e, string type, string target, int amount = 1)
		{
			if (e == null) return;
			// (every quest of the island: what one player does may count for several)
			int count = QuestsOf(e);
			for (int n = 0; n < count; n++) Event(e, n, type, target, amount);
		}

		static void Event(IslandWorldState.Entry e, int n, string type, string target, int amount)
		{
			IslandQuest q = QuestOf(e, n);
			int step = StepOf(e, n);
			if (!q.Exists || step >= q.Steps.Count) return;
			IslandQuest.Step s = q.Steps[step];
			if (!Matches(s, type, target))
			{
				// Done before its step came (the chest opened, the animals defeated first): remembered for that step and
				// counted when it comes - it was lost, and the chest stayed empty / the animals dead
				for (int later = step + 1; later < q.Steps.Count; later++)
				{
					if (IslandQuest.Counted(q.Steps[later].Type) || !Matches(q.Steps[later], type, target)) continue;
					if (Raft_Network.IsHost) Remember(e, n, later, amount);
					else if (IslandNetwork.HostAddsCounts) IslandNetwork.SendQuestAdd(e.Id, n, later, amount); // (an older host would jump to that step)
					break;
				}
				return;
			}
			int progress = ProgressOf(e, n) + amount;
			// A player's machine moves its own view on at once and sends the host its amount, not its total: two players'
			// totals overwrote each other (two of three chests opened at once counted 1). The host counts and tells everyone.
			// (an older host takes what it gets for the total: it gets the total, as before)
			bool adds = !Raft_Network.IsHost && IslandNetwork.HostAddsCounts;
			// (more than the step needs: the rest for the next step asking for the same, kept before the quest moves there)
			if (progress > s.Count && Raft_Network.IsHost)
				for (int later = step + 1; later < q.Steps.Count; later++)
					if (!IslandQuest.Counted(q.Steps[later].Type) && Matches(q.Steps[later], type, target)) { Remember(e, n, later, progress - s.Count); break; }
			if (progress >= s.Count) Set(e, n, step + 1, 0, !adds);
			else Set(e, n, step, progress, !adds);
			if (adds) IslandNetwork.SendQuestAdd(e.Id, n, step, amount);
		}

		static bool Matches(IslandQuest.Step s, string type, string target)
		{
			return s.Type == type && (s.Target.Length == 0 || string.Equals(s.Target.Trim(), (target ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Where a later step's early work is kept in the island's state (its count in Yield).</summary>
		public const int EarlyKeyBase = 0x40100;

		/// <summary>Host: something a later step of the quest asks for was done now - kept for that step. More than the step
		/// needs goes on to the next later step asking for the same (two "defeat a polar bear" steps with both bears dead
		/// early: both went to the first, and the second step could never be done - CB11).</summary>
		static void Remember(IslandWorldState.Entry e, int quest, int step, int amount)
		{
			IslandQuest q = QuestOf(e, quest);
			while (amount > 0)
			{
				ObjectState had;
				int before = e.State.TryGetValue(EarlyKeyOf(quest, step), out had) ? had.Yield : 0;
				int next = -1;
				if (q.Exists && step < q.Steps.Count)
					for (int k = step + 1; k < q.Steps.Count && next < 0; k++)
						if (q.Steps[k].Type == q.Steps[step].Type && string.Equals(q.Steps[k].Target.Trim(), q.Steps[step].Target.Trim(), StringComparison.OrdinalIgnoreCase)) next = k;
				// (never more than the step needs: a zone entered again - a player set down inside it after a load - counted
				// twice, SL7)
				int take = !q.Exists || step >= q.Steps.Count ? amount : Mathf.Min(amount, Mathf.Max(0, q.Steps[step].Count - before));
				if (take > 0)
				{
					e.State[EarlyKeyOf(quest, step)] = new ObjectState { Active = true, Yield = before + take, Day = Today };
					Debug.Log("[CUSTOM ISLANDS] Quest " + (quest + 1) + " of '" + e.HostName + "': step " + (step + 1) + " done early (" + (before + take) + "), counted when it comes");
				}
				amount -= take;
				if (next < 0) break;
				step = next;
			}
		}

		/// <summary>Host: the quest reached this step - what was done for it early counts now (it may finish it at once).</summary>
		static void CreditEarly(IslandWorldState.Entry e, int n, IslandQuest q, int step)
		{
			ObjectState had;
			if (!Raft_Network.IsHost || step >= q.Steps.Count || !e.State.TryGetValue(EarlyKeyOf(n, step), out had) || had.Yield <= 0) return;
			e.State.Remove(EarlyKeyOf(n, step));
			Debug.Log("[CUSTOM ISLANDS] Quest " + (n + 1) + " of '" + e.HostName + "': step " + (step + 1) + " gets what was done for it early (" + had.Yield + ")");
			int progress = ProgressOf(e, n) + had.Yield;
			if (progress >= q.Steps[step].Count) Set(e, n, step + 1, 0, true);
			else Set(e, n, step, progress, true);
		}

		/// <summary>Host: a player's event counted on an island's quest - their amount at that step, added to the host's
		/// count and sent to everyone. A step the quest has moved past counted already; a later one keeps it for then.</summary>
		public static void AddFromPlayer(int islandId, int step, int amount) { AddFromPlayer(islandId, 0, step, amount); }

		public static void AddFromPlayer(int islandId, int n, int step, int amount)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e == null || amount <= 0) return;
			IslandQuest q = QuestOf(e, n);
			int now = StepOf(e, n);
			if (!q.Exists) return;
			if (step > now && step < q.Steps.Count) { Remember(e, n, step, amount); return; }
			if (step != now || now >= q.Steps.Count) { IslandNetwork.SendQuest(e.Id, n, now, ProgressOf(e, n)); return; } // (the player's view put right)
			int progress = ProgressOf(e, n) + amount;
			if (progress >= q.Steps[now].Count) Set(e, n, now + 1, 0, true);
			else Set(e, n, now, progress, true);
		}

		/// <summary>Records the quest's state here, tells the others (unless it came from them), and shows what changed.</summary>
		public static bool Set(IslandWorldState.Entry e, int step, int progress, bool send) { return Set(e, 0, step, progress, send); }

		/// <summary>Quest n: records its state here, tells the others (unless it came from them), and shows what changed.
		/// False when nothing changed: an older step, or the same step with no more progress (an older player's stale total,
		/// a message that crossed a Resync) - progress never goes down.</summary>
		public static bool Set(IslandWorldState.Entry e, int n, int step, int progress, bool send)
		{
			int before = StepOf(e, n);
			if (step < before || (step == before && progress <= ProgressOf(e, n))) return false;
			e.State[StepKeyOf(n)] = new ObjectState { Active = true, Yield = step, Day = Today };
			if (progress > 0) e.State[ProgressKeyOf(n)] = new ObjectState { Active = true, Yield = progress, Day = Today };
			else e.State.Remove(ProgressKeyOf(n));
			if (send) IslandNetwork.SendQuest(e.Id, n, step, progress);
			if (step == before) return true;
			IslandQuest q = QuestOf(e, n);
			if (step >= q.Steps.Count) Completed(e, q, n);
			else Show(q.ShownTitle, "Next: " + q.Steps[step].Describe());
			// (the main quest's "quest" event and rules as before; any quest: AdvancedAny)
			if (n == 0 && Advanced != null) try { Advanced(e.Id, step); } catch { }
			if (AdvancedAny != null) try { AdvancedAny(e.Id, n, step); } catch { }
			CreditEarly(e, n, q, step);
			// (host: a counted step the crew already has enough for moves on now, not at the next half-second check - CB11)
			if (Raft_Network.IsHost && step < q.Steps.Count && IslandQuest.Counted(q.Steps[step].Type) && Found(e, q.Steps[step]) >= q.Steps[step].Count && AnyPlayerNear(e))
				Set(e, n, step + 1, 0, true);
			return true;
		}

		/// <summary>Raised on every machine when any quest of an island moves on: island id, quest number (0 = main), new step.</summary>
		public static event Action<int, int, int> AdvancedAny;

		/// <summary>From the network (another player moved the quest on). False when it was ignored (nothing to pass on).</summary>
		public static bool Apply(int islandId, int step, int progress) { return Apply(islandId, 0, step, progress); }

		public static bool Apply(int islandId, int n, int step, int progress)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			return e != null && Set(e, n, step, progress, false);
		}

		/// <summary>Where this player's share of quest n's reward is kept (QuestRewards). By the island's copy (PageIsland: the
		/// first is its plain name, as before): a second copy of an island never paid its quest's reward, the first had it.</summary>
		static string RewardKey(IslandWorldState.Entry e, int n) { string island = StoryBook.PageIsland(e); return n == 0 ? island : island + "#quest" + (n + 1); }

		/// <summary>Whether the quest's reward has Raft's items (each player's share; story items are the crew's).</summary>
		static bool HasRaftReward(IslandQuest q)
		{
			return q.Reward.Length > 0 && ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, q.Reward } }).Any(l => !StoryItems.IsStory(l.Key));
		}

		static void Completed(IslandWorldState.Entry e, IslandQuest q, int n)
		{
			Show("Quest complete: " + q.ShownTitle, q.Done);
			if (q.Reward.Length == 0) { Debug.Log("[CUSTOM ISLANDS] Quest of '" + e.HostName + "' done: no reward"); return; }
			List<KeyValuePair<string, int>> reward = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, q.Reward } });
			// Story items are the crew's: the host gives them once (every player near giving them gave one per player);
			// Raft's items go to each player near the island
			if (Raft_Network.IsHost)
				foreach (KeyValuePair<string, int> l in reward.Where(l => StoryItems.IsStory(l.Key))) StoryBook.Give(l.Key, l.Value, e);
			// Raft's items: each player's share once - now when near, or when they come to the island (or join) later (LM8)
			if (!reward.Any(l => !StoryItems.IsStory(l.Key))) return;
			bool near = Near(e);
			if (!QuestRewards.OnCompleted(RewardKey(e, n), near, () => GiveItems(q))) Debug.Log("[CUSTOM ISLANDS] Quest reward of '" + RewardKey(e, n) + "': this player got it already in this world (" + QuestRewards.WhereKept + ")");
			else if (!near) { Debug.Log("[CUSTOM ISLANDS] Quest reward of '" + e.HostName + "' kept until this player comes to the island"); DynamicIslands.Notify("Your reward for '" + q.ShownTitle + "' waits for you at the island - sail there to get it", false, 6); }
		}

		static void GiveItems(IslandQuest q)
		{
			List<KeyValuePair<string, int>> reward = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, q.Reward } });
			List<string> given = TriggerZone.Give(reward.Where(l => !StoryItems.IsStory(l.Key)));
			Debug.Log("[CUSTOM ISLANDS] Quest reward: " + string.Join(", ", given.ToArray()));
		}

		static void Show(string title, string text)
		{
			LastMessage = title + (text.Length > 0 ? " | " + text : "");
			IslandInfo.Show(title, "", text);
			Debug.Log("[CUSTOM ISLANDS] " + LastMessage);
		}

		static bool Near(IslandWorldState.Entry e) { return Near(e, NearDistance); }

		static bool Near(IslandWorldState.Entry e, float distance)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null || e.Root == null) return false;
			// (the land's middle - where the island's entry stands - and how far its land reaches: an island without a title or
			// description has no info tag, and was measured from its terrain's corner, hundreds of metres off its land)
			IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
			Vector3 c = tag != null ? e.Root.transform.position + tag.LocalCentre : e.Position;
			float r = tag != null ? tag.Radius : Mathf.Max(30f, CustomIslandSpawner.LandRadius(e.Name));
			return new Vector2(c.x - p.transform.position.x, c.z - p.transform.position.z).magnitude < r + distance;
		}

		#region The quest panel

		static RectTransform panel, stepsView, stepsContent;
		static ScrollRect stepsScroll;
		static Text titleText;
		static readonly List<Text> stepLines = new List<Text>();
		static float nextHud;
		static int followedIsland = -1, followedStep = -1;
		static readonly HashSet<int> introduced = new HashSet<int>();
		static Button closeButton;
		/// <summary>The panel closed with its X: hidden for this quest and step while the player stays at the island.</summary>
		static int closedKey = -1, closedStep = -1;
		/// <summary>When each finished quest was first seen done (island * 16 + quest): its panel goes 30 s later (ROADMAP CT10).</summary>
		static readonly Dictionary<int, float> doneSince = new Dictionary<int, float>();
		/// <summary>How long a finished quest's panel stays up (the user, 2026-10-07: "after finished, hide it after 30sec").</summary>
		public static float DoneHideSeconds = 30f;
		/// <summary>Whether the quest panel is on screen (for the tests).</summary>
		public static bool PanelShown { get { return panel != null && panel.gameObject.activeSelf; } }
		/// <summary>How many steps the panel shows at a time; a longer quest scrolls (the user, 2026-10-03: a 32-step quest
		/// covered the right side of the screen).</summary>
		public const int VisibleSteps = 5;

		/// <summary>The quests read once per tick (the panel and CheckCounted asked for each one 4-6 times, every 0.5 s, each a
		/// full parse of its texts - audit 2026-10-06). Only within one tick: an island's settings can change between them.</summary>
		static readonly Dictionary<long, IslandQuest> tickQuests = new Dictionary<long, IslandQuest>();

		static IslandQuest TickQuest(IslandWorldState.Entry e, int n)
		{
			if (e == null) return new IslandQuest();
			long key = ((long)e.Id << 16) | (uint)(n & 0xFFFF);
			IslandQuest q;
			if (!tickQuests.TryGetValue(key, out q)) tickQuests[key] = q = QuestOf(e, n);
			return q;
		}

		/// <summary>Every frame from the mod: the panel for the quest of the island the player is at (and its introduction once).</summary>
		/// <summary>How many times the quests were checked (for the tests).</summary>
		public static int TickCount;

		public static void Tick()
		{
			if (Time.unscaledTime < nextHud) return;
			nextHud = Time.unscaledTime + 0.5f;
			tickQuests.Clear();
			if (!LoadSceneManager.IsGameSceneLoaded) { introduced.Clear(); panelIsland = -1; doneSince.Clear(); closedKey = -1; DoneHideSeconds = 30f; if (panel != null) panel.gameObject.SetActive(false); return; }
			TickCount++;
			if (Raft_Network.IsHost) CheckCounted();
			CheckBlueprints();
			// (shows within ShowDistance, stays up out to NearDistance)
			IslandWorldState.Entry at = IslandWorldState.Islands.FirstOrDefault(e => e.Root != null && Near(e, e.Id == panelIsland ? NearDistance : ShowDistance) && Enumerable.Range(0, QuestsOf(e)).Any(n => TickQuest(e, n).Exists));
			panelIsland = at != null ? at.Id : -1;
			if (at == null) { closedKey = -1; if (panel != null) panel.gameObject.SetActive(false); return; }
			int quests = QuestsOf(at);
			// (a reward kept for this player, who wasn't here when a quest was done: now - LM8. Also for a player who joined
			// after it was done: the step came with the island list, no "done" ran here, and nothing was ever owed to them)
			for (int n = 0; n < quests; n++)
			{
				IslandQuest qn = TickQuest(at, n);
				if (!qn.Exists || StepOf(at, n) < qn.Steps.Count) continue;
				string key = RewardKey(at, n);
				if (QuestRewards.Owed(key)) QuestRewards.Collect(key, () => GiveItems(qn));
				else if (!QuestRewards.Rewarded(key) && HasRaftReward(qn)) QuestRewards.OnCompleted(key, true, () => GiveItems(qn));
				else continue;
				Show("Your share of the reward: " + qn.ShownTitle, "");
			}
			// (the panel: the first quest not done yet, the main one first - LM4; all done: the main quest, ticked)
			int shown = Enumerable.Range(0, quests).FirstOrDefault(n => TickQuest(at, n).Exists && StepOf(at, n) < TickQuest(at, n).Steps.Count);
			if (!TickQuest(at, shown).Exists) shown = Enumerable.Range(0, quests).First(n => TickQuest(at, n).Exists);
			IslandQuest q = TickQuest(at, shown);
			int step = StepOf(at, shown);
			if (introduced.Add(at.Id) && step == 0 && q.Intro.Length > 0) Show(q.ShownTitle, q.Intro);
			if (panel == null) Build();
			int open = Enumerable.Range(0, quests).Count(n => n != shown && TickQuest(at, n).Exists && StepOf(at, n) < TickQuest(at, n).Steps.Count);
			// (every quest here done: the panel goes after DoneHideSeconds; closed with its X: gone until the step changes)
			int shownKey = at.Id * 16 + shown;
			bool hide = shownKey == closedKey && step == closedStep;
			if (step >= q.Steps.Count && open == 0)
			{
				float since;
				if (!doneSince.TryGetValue(shownKey, out since)) doneSince[shownKey] = since = Time.unscaledTime;
				if (Time.unscaledTime - since >= DoneHideSeconds) hide = true;
			}
			panel.gameObject.SetActive(!hide);
			if (hide) return;
			closeButton.onClick.RemoveAllListeners();
			closeButton.onClick.AddListener(() => { closedKey = shownKey; closedStep = step; panel.gameObject.SetActive(false); });
			titleText.text = q.ShownTitle + (step >= q.Steps.Count ? "  <color=#8fdc8f>\u221A done</color>" : "") + (open > 0 ? "  <color=#b39a6c>(+" + open + " more)</color>" : "");
			var lines = new List<string>();
			for (int i = 0; i < q.Steps.Count; i++)
			{
				string d = q.Steps[i].Describe();
				if (i < step) lines.Add("<color=#8fdc8f>\u221A</color> <color=#b89e70>" + d + "</color>");
				else if (i == step)
				{
					int progress = IslandQuest.Counted(q.Steps[i].Type) ? Mathf.Min(Found(at, q.Steps[i]), q.Steps[i].Count) : ProgressOf(at, shown);
					lines.Add("<color=#ffc766>\u25BA</color> " + d + (q.Steps[i].Count > 1 ? " (" + progress + "/" + q.Steps[i].Count + ")" : ""));
				}
				else lines.Add("<color=#b39a6c>\u2022 ?</color>");
			}
			ShowSteps(lines, step, at.Id * 16 + shown);
		}

		/// <summary>
		/// The steps, one line each, in a list at most VisibleSteps tall: a longer quest scrolls - by itself to the
		/// current step (the step done before it, then it and the next ones) whenever the step changes, and with the
		/// mouse wheel while the cursor is free (the inventory, the journal, Raft's menu) - never fighting the player's
		/// own scrolling between two steps.
		/// </summary>
		static void ShowSteps(List<string> lines, int step, int island)
		{
			bool changed = stepLines.Count < lines.Count;
			while (stepLines.Count < lines.Count) stepLines.Add(StepLine());
			for (int i = 0; i < stepLines.Count; i++)
			{
				bool used = i < lines.Count;
				if (stepLines[i].gameObject.activeSelf != used) { stepLines[i].gameObject.SetActive(used); changed = true; }
				if (used && stepLines[i].text != lines[i]) { stepLines[i].text = lines[i]; changed = true; }
			}
			// (nothing new since the last tick: the layout as it was - it was rebuilt every 0.5 s, audit 2026-10-06)
			if (!changed && island == followedIsland && step == followedStep) return;
			LayoutRebuilder.ForceRebuildLayoutImmediate(stepsContent);
			// (the window: one done step above the current one, so the player sees what was just done)
			int first = Mathf.Clamp(step - 1, 0, Mathf.Max(0, lines.Count - VisibleSteps));
			float spacing = stepsContent.GetComponent<VerticalLayoutGroup>().spacing, top = 0f, height = 0f;
			for (int i = 0; i < lines.Count; i++)
			{
				float h = LayoutUtility.GetPreferredHeight(stepLines[i].rectTransform);
				if (i < first) top += h + spacing;
				else if (i < first + VisibleSteps) height += h + (i > first ? spacing : 0f);
			}
			height += 4f; // (the content's bottom padding)
			LayoutElement size = stepsView.GetComponent<LayoutElement>();
			if (Mathf.Abs(size.preferredHeight - height) > 0.5f) UIKit.Size(stepsView.gameObject, -1, height);
			bool scrolls = lines.Count > VisibleSteps;
			stepsScroll.vertical = scrolls;
			if (island != followedIsland || step != followedStep || !scrolls)
			{
				followedIsland = island; followedStep = step;
				stepsContent.anchoredPosition = new Vector2(stepsContent.anchoredPosition.x, scrolls ? top : 0f);
			}
		}

		static Text StepLine()
		{
			Text t = UIKit.Label(stepsContent, "", 13, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Step");
			t.lineSpacing = 1.15f;
			return t;
		}

		/// <summary>How far a counted step is: story items the crew holds, or journal pages found on the island.</summary>
		public static int Found(IslandWorldState.Entry e, IslandQuest.Step s)
		{
			// (found in all, also those a lock or a machine used up before this step came - ROADMAP E12)
			// (not those found on another copy of the same island in the world: they were for that copy's quest - each copy
			// counts its own finds, and finds from other islands count for both)
			if (s.Type == "collect")
			{
				string id = s.Target.Length > 0 ? StoryItems.IdOf(s.Target) : null;
				int all = id != null ? StoryBook.FoundCount(id) : StoryBook.Items.Sum(h => h.Count);
				return Mathf.Max(0, all - StoryBook.FoundOnOtherCopies(id, e));
			}
			if (s.Type == "pages")
			{
				// ("all": the islands' pages - not the frequencies the story chain writes into the journal; this island: its own
				// copy's - two copies of one island shared their pages, AU41)
				if (s.Target == "all") return StoryBook.Pages.Count(p => p.Key.StartsWith("note:") || p.Key.StartsWith("act:"));
				string island = StoryBook.PageIsland(e);
				return StoryBook.Pages.Count(p => p.Key.StartsWith("note:" + island + ":") || p.Key.StartsWith("act:" + island + ":"));
			}
			return 0;
		}

		/// <summary>Host: counted steps (story items, pages) move on when the crew has enough; everyone is told.</summary>
		static void CheckCounted()
		{
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.ToList())
			{
				for (int n = 0, count = QuestsOf(e); n < count; n++)
				{
					IslandQuest q = TickQuest(e, n);
					int step = StepOf(e, n);
					if (!q.Exists || step >= q.Steps.Count || !IslandQuest.Counted(q.Steps[step].Type)) continue;
					// (only while someone is there: a quest whose items the crew already held finished the moment its island
					// appeared far away, and nobody got its reward)
					if (Found(e, q.Steps[step]) >= q.Steps[step].Count && AnyPlayerNear(e)) Set(e, n, step + 1, 0, true);
				}
			}
		}

		/// <summary>Every machine (SL8): a "blueprint" step waiting at an island this player is at is done when this player
		/// carries that blueprint or has learned it - picked up there, earlier, or bought at a store. It goes through Event like
		/// a note read, so a player's machine tells the host (and an early pick-up still counts when the step comes).</summary>
		internal static void CheckBlueprints()
		{
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null || !Near(e)) continue;
				for (int n = 0, count = QuestsOf(e); n < count; n++)
				{
					IslandQuest q = TickQuest(e, n);
					int step = StepOf(e, n);
					if (!q.Exists || step >= q.Steps.Count || q.Steps[step].Type != BlueprintSteps.Type) continue;
					IslandQuest.Step s = q.Steps[step];
					string found = BlueprintSteps.Found(s.Target);
					if (found == null) continue;
					Debug.Log("[CUSTOM ISLANDS] [quest] " + e.Name + ": blueprint step " + (step + 1) + " done (" + found + ")");
					Event(e, n, BlueprintSteps.Type, s.Target, Mathf.Max(1, s.Count - ProgressOf(e, n)));
					return; // (one a tick: the quest moved on, the next tick looks again)
				}
			}
		}

		/// <summary>Host: whether any player is at the island (its land and the same reach as the rewards).</summary>
		static bool AnyPlayerNear(IslandWorldState.Entry e)
		{
			float r = Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name)) + NearDistance;
			foreach (Network_Player p in UnityEngine.Object.FindObjectsOfType<Network_Player>())
				if (p != null && new Vector2(p.transform.position.x - e.Position.x, p.transform.position.z - e.Position.z).magnitude < r) return true;
			return false;
		}

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_Quest", 380);
			UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
			panel = UIKit.Rect("Quest", canvas.transform);
			UIKit.Anchor(panel, new Vector2(1f, 1f), new Vector2(-20, -160), new Vector2(300, 0));
			UIKit.Surface(panel); // Raft's menu look
			UIKit.Vertical(panel.gameObject, 4f, new RectOffset(12, 12, 8, 10), true);
			// (the title and an X to close the panel - ROADMAP CT10; the X takes the mouse while the cursor is free)
			RectTransform top = UIKit.Rect("Top", panel);
			UIKit.Horizontal(top.gameObject, 6f).childForceExpandWidth = false;
			titleText = UIKit.Label(top, "", 16, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.Size(titleText.gameObject);
			closeButton = UIKit.Button(top, "×", () => { }, "Close the quest panel", 24, 24f, 14);
			closeButton.name = "Close";
			stepsContent = UIKit.ScrollList(panel, out stepsScroll, 3f);
			stepsView = (RectTransform)stepsScroll.transform;
			stepsView.name = "Steps";
			UIKit.Size(stepsView.gameObject, -1, 20f);
			stepsScroll.scrollSensitivity = 18f;
			stepLines.Clear();
			followedIsland = followedStep = -1;
			foreach (Graphic g in panel.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
			foreach (Graphic g in closeButton.GetComponentsInChildren<Graphic>()) g.raycastTarget = true;
			// (only the list's own background and its scrollbar take the mouse: the wheel scrolls it while the cursor is free)
			stepsScroll.GetComponent<Image>().raycastTarget = true;
			if (stepsScroll.verticalScrollbar != null)
				foreach (Graphic g in stepsScroll.verticalScrollbar.GetComponentsInChildren<Graphic>()) g.raycastTarget = true;
		}

		#endregion
	}
}
