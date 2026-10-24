using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Daily quest" (WorldOptions.DailyQuest): each new in-game day, once it is light, the crew gets one
	/// small task for the day; done before dark, everyone gets a small reward (the user: basic resources or food, never
	/// enough to unbalance the game). Not done by dark, it runs out.
	///   - The tasks: gather N of a basic resource (from the sea or islands: what the player picks up or hooks), catch N
	///     fish with the rod, or defeat N monsters. What, how many and the reward come from the world's seed and the day.
	///   - The crew's together: every player's part counts (a player's amount goes to the host, who adds it up and tells
	///     everyone). Each player gets the reward once, also one who joins later that day.
	///   - Host decides (a new day, done, run out); saved in the world file ("@daily="), sent as network kind 26.
	///   - Shown as a banner when it comes, as it moves on and when done or run out, and at the top of the journal's quest list.
	/// </summary>
	public static class DailyQuest
	{
		public const string Gather = "gather", Fish = "fish", Monsters = "monsters";
		/// <summary>What may be asked for (Raft's names), how many at least and at most.</summary>
		static readonly string[] GatherItems = { "Plank", "Plastic", "Thatch", "Scrap" };
		static readonly int[] GatherMin = { 12, 10, 10, 4 }, GatherMax = { 20, 16, 16, 8 };
		/// <summary>The rewards: small, basic (one or two of these).</summary>
		static readonly string[] RewardItems = { "Plank", "Rope", "Nail", "Scrap", "Raw_Pomfret", "Raw_Mackerel", "Watermelon", "Clay" };
		static readonly int[] RewardMin = { 6, 2, 4, 2, 1, 1, 1, 3 }, RewardMax = { 10, 4, 8, 4, 2, 2, 2, 5 };
		/// <summary>The EXP a done quest is worth too (with the level up system on).</summary>
		public const int Xp = 40;

		public class Task
		{
			public int Day = -1;
			public string Kind = "";
			/// <summary>Gather: the item (Raft's name).</summary>
			public string Target = "";
			public int Need, Have;
			/// <summary>"open", "done" or "out" (ran out at dark).</summary>
			public string State = "open";
			/// <summary>"Item:n,Item:n".</summary>
			public string Reward = "";
			/// <summary>Players (Steam ids) who got the reward.</summary>
			public readonly HashSet<ulong> Rewarded = new HashSet<ulong>();

			public bool Open { get { return Day >= 0 && State == "open"; } }

			public string Encode()
			{
				return string.Join(";", new[] { Day.ToString(CultureInfo.InvariantCulture), Kind, Target, Need.ToString(CultureInfo.InvariantCulture), Have.ToString(CultureInfo.InvariantCulture), State, Reward, string.Join(",", Rewarded.Select(r => r.ToString(CultureInfo.InvariantCulture)).ToArray()) });
			}

			public static Task Decode(string s)
			{
				var t = new Task();
				string[] p = (s ?? "").Split(';');
				if (p.Length < 7) return t;
				int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.Day);
				t.Kind = p[1]; t.Target = p[2];
				int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.Need);
				int.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.Have);
				t.State = p[5]; t.Reward = p[6];
				if (p.Length > 7) foreach (string r in p[7].Split(',')) { ulong id; if (ulong.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id != 0UL) t.Rewarded.Add(id); }
				return t;
			}
		}

		/// <summary>Today's (or the last day's) task; Day -1 = none yet.</summary>
		public static Task Current = new Task();
		/// <summary>This machine gave its player the reward of this day already (a client: until the host's list has them).</summary>
		static int gotDay = -1;
		static float nextCheck;

		/// <summary>Tests: the day whatever Raft's counter says (null = Raft's).</summary>
		internal static int? TestDay;

		public static bool On { get { return WorldOptions.On(WorldOptions.DailyQuest); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [daily] " + msg); }

		public static int Today
		{
			get
			{
				if (TestDay.HasValue) return TestDay.Value;
				try { return WorldManager.DayCounter; } catch { return 0; }
			}
		}

		internal static void Reset() { Current = new Task(); gotDay = -1; TestDay = null; nextCheck = 0f; }

		#region What a day asks for

		/// <summary>The task of a day in a world with this seed (the same on every machine, and again after a load).</summary>
		public static Task Make(int seed, int day)
		{
			var rnd = new System.Random(WorldOptions.Mix(seed, day, 17)); // (mixed: nearby seeds gave the same task day after day)
			var t = new Task { Day = day };
			int k = rnd.Next(100);
			if (k < 45)
			{
				int i = rnd.Next(GatherItems.Length);
				t.Kind = Gather; t.Target = GatherItems[i]; t.Need = rnd.Next(GatherMin[i], GatherMax[i] + 1);
			}
			else if (k < 75) { t.Kind = Fish; t.Need = rnd.Next(3, 7); }
			else { t.Kind = Monsters; t.Need = rnd.Next(2, 5); }
			// (one or two small rewards, never what was asked for)
			int a = rnd.Next(RewardItems.Length);
			if (RewardItems[a] == t.Target) a = (a + 1) % RewardItems.Length;
			var reward = new List<string> { RewardItems[a] + ":" + rnd.Next(RewardMin[a], RewardMax[a] + 1) };
			if (rnd.Next(2) == 0)
			{
				int b = rnd.Next(RewardItems.Length);
				if (b != a && RewardItems[b] != t.Target) reward.Add(RewardItems[b] + ":" + rnd.Next(RewardMin[b], RewardMax[b] + 1));
			}
			t.Reward = string.Join(",", reward.ToArray());
			return t;
		}

		public static List<KeyValuePair<string, int>> RewardList(Task t)
		{
			var list = new List<KeyValuePair<string, int>>();
			foreach (string r in (t.Reward ?? "").Split(','))
			{
				int c = r.IndexOf(':'); int n;
				if (c > 0 && int.TryParse(r.Substring(c + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0) list.Add(new KeyValuePair<string, int>(r.Substring(0, c), n));
			}
			return list;
		}

		static string ItemLabel(string item) { try { return ContentCatalog.ItemLabel(item); } catch { return item; } }

		/// <summary>"Gather 14 Planks", "Catch 4 fish with the rod", "Defeat 3 monsters".</summary>
		public static string Describe(Task t)
		{
			switch (t.Kind)
			{
				case Gather: return "Gather " + t.Need + " " + ItemLabel(t.Target);
				case Fish: return "Catch " + t.Need + " fish with the rod";
				case Monsters: return "Defeat " + t.Need + " monsters";
			}
			return "";
		}

		public static string DescribeReward(Task t)
		{
			string s = string.Join(", ", RewardList(t).Select(r => ItemLabel(r.Key) + " ×" + r.Value).ToArray());
			return PlayerLevels.On ? s + " and " + Xp + " EXP" : s;
		}

		/// <summary>The journal's lines (null with the option off or no task yet).</summary>
		public static string JournalText()
		{
			if (!On || Current.Day < 0) return null;
			Task t = Current;
			string head = "Today's quest (day " + t.Day + "): " + Describe(t) + " - " + Math.Min(t.Have, t.Need) + " of " + t.Need;
			if (t.State == "done") return head + ", done! Reward: " + DescribeReward(t) + ".\n\n";
			if (t.State == "out") return head + ". It ran out at dark: a new one comes in the morning.\n\n";
			return head + ". Before dark, for: " + DescribeReward(t) + ".\n\n";
		}

		/// <summary>
		/// The journal's daily quest block under its page (T9): what to do and how far along, the reward, done or not, and
		/// when the next one comes. Null with the option off; along = how far along (0-1).
		/// </summary>
		public static string PanelText(out float along)
		{
			along = 0f;
			if (!On) return null;
			Task t = Current;
			if (t.Day < 0) return "No quest yet: the first one comes at daybreak.";
			along = t.Need > 0 ? Mathf.Clamp01((float)t.Have / t.Need) : 0f;
			string state = t.State == "done" ? "Done!" + (gotDay == t.Day ? " You got the reward." : "") + " The next one comes at daybreak."
				: t.State == "out" ? "Not done: it ran out at dark. The next one comes at daybreak."
				: "Not done yet: it ends at dark.";
			return "Day " + t.Day + ": " + Describe(t) + "  -  " + Math.Min(t.Have, t.Need) + " / " + t.Need + "\nReward: " + DescribeReward(t) + "\n" + state;
		}

		#endregion

		#region The day (host)

		/// <summary>Every frame: the host starts a day's task once it's light, and ends it at dark; every machine gets its reward.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 1f;
			if (!LoadSceneManager.IsGameSceneLoaded || !On) return;
			if (Raft_Network.IsHost)
			{
				int day = Today;
				bool night = NightDanger.IsNight;
				if (!night && day > Current.Day && day > 0) Start(day);
				else if (night && Current.Open) { Current.State = "out"; Log("Ran out: " + Describe(Current)); Changed(true); }
			}
			GiveIfDue();
		}

		/// <summary>Host (and tests): the task of this day starts now.</summary>
		internal static void Start(int day)
		{
			Current = Make(WorldOptions.Seed, day);
			Log("Day " + day + ": " + Describe(Current) + " (" + Current.Reward + ")");
			Changed(true);
		}

		/// <summary>Host: the task changed - everyone hears, the world file keeps it, a banner says it (new, done, run out).</summary>
		static void Changed(bool banner)
		{
			if (Raft_Network.IsHost) { Send(Message(), null); IslandWorldState.Save(); }
			if (banner) Banner();
		}

		static void Banner()
		{
			Task t = Current;
			if (t.Day < 0) return;
			if (t.State == "open") IslandInfo.Show("Today's quest", "", Describe(t) + " before dark. Reward: " + DescribeReward(t));
			else if (t.State == "out") IslandInfo.Show("Today's quest ran out", "", Describe(t) + ": not done before dark");
		}

		#endregion

		#region Progress

		/// <summary>This player did some of it (any machine): the host adds it up.</summary>
		public static void Add(string kind, string target, int amount)
		{
			if (!On || amount <= 0 || !Current.Open || Current.Kind != kind) return;
			if (kind == Gather && !string.Equals(Current.Target, target, StringComparison.OrdinalIgnoreCase)) return;
			if (Raft_Network.IsHost) AddOnHost(Current.Day, amount);
			else
			{
				// (shown at once here; the host's total follows)
				Current.Have += amount;
				ShowProgress();
				Send(new IslandNetMessage { Name = "add", Index = Current.Day, Count = amount }, null);
			}
		}

		static void AddOnHost(int day, int amount)
		{
			if (!Current.Open || day != Current.Day) return;
			Current.Have += amount;
			if (Current.Have >= Current.Need) { Current.Have = Current.Need; Current.State = "done"; Log("Done: " + Describe(Current)); }
			else ShowProgress();
			Changed(false);
			GiveIfDue();
		}

		static void ShowProgress()
		{
			if (Current.Open) IslandInfo.ShowMessage("Today's quest: " + Describe(Current) + " - " + Math.Min(Current.Have, Current.Need) + " of " + Current.Need);
		}

		static ulong LocalId
		{
			get { try { Network_Player p = RAPI.GetLocalPlayer(); return p != null ? p.steamID.Id : 0UL; } catch { return 0UL; } }
		}

		/// <summary>The task is done and this player hasn't had its reward: given now (once).</summary>
		static void GiveIfDue()
		{
			if (Current.State != "done" || gotDay == Current.Day) return;
			ulong me = LocalId;
			if (me == 0UL || RAPI.GetLocalPlayer() == null) return;
			if (Current.Rewarded.Contains(me)) { gotDay = Current.Day; return; }
			gotDay = Current.Day;
			List<string> given = TriggerZone.Give(RewardList(Current));
			if (PlayerLevels.On) PlayerLevels.GiveXp(Xp, null);
			Log("Reward: " + string.Join(", ", given.ToArray()));
			IslandInfo.Show("Today's quest done!", "", Describe(Current) + ". Reward: " + DescribeReward(Current));
			if (Raft_Network.IsHost) { Current.Rewarded.Add(me); IslandWorldState.Save(); }
			else Send(new IslandNetMessage { Name = "got", Index = Current.Day }, null);
		}

		#endregion

		#region Network (kind 26) and the world file

		internal static IslandNetMessage Message() { return new IslandNetMessage { Kind = IslandNetMessage.DailyQuest, Name = "state", Data = Current.Encode() }; }

		/// <summary>The host to a player (or everyone), a player to the host.</summary>
		static void Send(IslandNetMessage msg, Network_UserId? to)
		{
			msg.Kind = IslandNetMessage.DailyQuest;
			IslandNetwork.SendDaily(msg, to);
		}

		internal static void OnMessage(IslandNetMessage msg, Network_UserId from)
		{
			if (Raft_Network.IsHost)
			{
				if (msg.Name == "add") AddOnHost(msg.Index, msg.Count);
				else if (msg.Name == "got" && msg.Index == Current.Day && from.Id != 0UL && Current.Rewarded.Add(from.Id)) { PlayerLevels.CreditXp(from.Id, Xp); Send(Message(), null); IslandWorldState.Save(); } // (CB2: the host credits the reward's EXP)
				return;
			}
			if (msg.Name != "state") return;
			Task before = Current;
			Current = Task.Decode(msg.Data);
			// (a banner for what changed: a new day's task, or the old one run out)
			if (Current.Day >= 0 && (Current.Day != before.Day || Current.State != before.State) && Current.State != "done") Banner();
			else if (Current.Open && Current.Have != before.Have) ShowProgress();
			GiveIfDue();
		}

		internal static bool ReadLine(string key, string value)
		{
			if (key != "daily") return false;
			Current = Task.Decode(value);
			// (the host's own reward is in the list: not given again after a load)
			gotDay = -1;
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Current.Day >= 0) yield return "@daily=" + Current.Encode();
		}

		#endregion

		[ConsoleCommand(name: "DailyQuest", docs: "The world option Daily quest: DailyQuest = today's quest; DailyQuest new [kind n [item]] = a new one now (host); DailyQuest add n = n done by this player (tests)")]
		public static void DailyQuestCommand(string[] args)
		{
			if (args != null && args.Length > 0 && args[0] == "new" && Raft_Network.IsHost)
			{
				Start(Math.Max(Current.Day + 1, Today));
				// (tests: DailyQuest new <kind> <how many> [item])
				int need;
				if (args.Length > 2 && new[] { Gather, Fish, Monsters }.Contains(args[1]) && int.TryParse(args[2], out need) && need > 0)
				{
					Current.Kind = args[1]; Current.Need = need; Current.Target = args[1] == Gather ? (args.Length > 3 ? args[3] : "Plank") : "";
					Changed(false);
				}
			}
			int n;
			if (args != null && args.Length > 1 && args[0] == "add" && int.TryParse(args[1], out n)) Add(Current.Kind, Current.Target, n);
			Debug.Log("[CUSTOM ISLANDS] Daily quest: " + (On ? (Current.Day < 0 ? "none yet" : "day " + Current.Day + ": " + Describe(Current) + " " + Current.Have + "/" + Current.Need + " " + Current.State + " (" + Current.Reward + ")") : "off in this world"));
		}
	}

	/// <summary>Daily quest: a fish caught with this player's rod.</summary>
	[HarmonyPatch(typeof(FishingRod), "PullItemsFromSea")]
	static class DailyQuestFish
	{
		static void Postfix(FishingRod __instance)
		{
			try
			{
				if (!DailyQuest.On) return;
				Network_Player p = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
				if (p != null && p.IsLocalPlayer) DailyQuest.Add(DailyQuest.Fish, "", 1);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [daily] " + e.Message); }
		}
	}

	/// <summary>
	/// Daily quest: what this player picks up (by hand, or with the hook) of the asked-for item: the count in their inventory
	/// before and after (crafting and rewards don't go this way). The outermost of the two counts: the hook may pick up through Pickup.
	/// </summary>
	static class DailyQuestGather
	{
		static int depth, before;

		static PlayerInventory Inventory(Network_Player p) { return p != null && p.IsLocalPlayer ? p.Inventory : null; }

		internal static void Before(Network_Player p)
		{
			depth++;
			if (depth != 1) return;
			PlayerInventory inv = DailyQuest.On && DailyQuest.Current.Open && DailyQuest.Current.Kind == DailyQuest.Gather ? Inventory(p) : null;
			before = inv != null ? inv.GetItemCount(DailyQuest.Current.Target) : -1;
		}

		internal static void After(Network_Player p)
		{
			depth = Math.Max(0, depth - 1);
			if (depth != 0 || before < 0) return;
			PlayerInventory inv = Inventory(p);
			if (inv == null) return;
			int got = inv.GetItemCount(DailyQuest.Current.Target) - before;
			before = -1;
			if (got > 0) DailyQuest.Add(DailyQuest.Gather, DailyQuest.Current.Target, got);
		}
	}

	[HarmonyPatch(typeof(Pickup), "AddItemToInventory")]
	static class DailyQuestPickup
	{
		static void Prefix(Pickup __instance) { try { DailyQuestGather.Before(Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>()); } catch { } }
		static void Postfix(Pickup __instance) { try { DailyQuestGather.After(Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>()); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [daily] " + e.Message); } }
	}

	[HarmonyPatch(typeof(Hook), "FinishGathering")]
	static class DailyQuestHook
	{
		static void Prefix(Hook __instance) { try { DailyQuestGather.Before(Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>()); } catch { } }
		static void Postfix(Hook __instance) { try { DailyQuestGather.After(Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>()); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [daily] " + e.Message); } }
	}
}
