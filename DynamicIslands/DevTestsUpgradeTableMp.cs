using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Two-player tests of the upgrade research table (TEST_CATALOGUE RT; driven by _ProjectDocs/tools/mpupgradetable.ps1).
	/// Every command runs on the Raft it is given to (host = player 1, the box = player 2) and ends with PASS/FAIL lines.
	/// The test upgrade is the registered one with the fewest research items; "idx" is an index into its item list
	/// (CIUTSetup prints it). Two upgrade tables are placed by the host (the host uses the first, player 2 the second:
	/// Raft's table slot is the researching player's own).
	/// </summary>
	public static partial class DevTests
	{
		static HashSet<string> utOptionsBefore;
		static string utRaftPool;
		static readonly List<Slot> utFillSlots = new List<Slot>();
		static Item_Base utFiller;
		static Slot utHeldSlot;
		static ResearchTable utHeldTable;
		static int utHeldIdx, utHeldStart;

		static ExtraUpgrades.Upgrade UtUpgrade()
		{
			return UpgradeTable.Teaches.OrderBy(u => UpgradeTable.ResearchItems(u).Distinct().Count()).ThenBy(u => u.Index).FirstOrDefault();
		}

		static List<Item_Base> UtItems(ExtraUpgrades.Upgrade u)
		{
			return u == null ? new List<Item_Base>() : UpgradeTable.ResearchItems(u).Distinct().OrderBy(i => i.UniqueIndex).ToList();
		}

		/// <summary>"0,1" or "all" to indexes of the item list; "-" is none.</summary>
		static List<int> UtIdx(string s, int count)
		{
			var r = new List<int>();
			if (string.IsNullOrEmpty(s) || s == "-") return r;
			if (s == "all") { for (int i = 0; i < count; i++) r.Add(i); return r; }
			foreach (string p in s.Split(','))
			{
				int i;
				if (int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out i) && i >= 0 && i < count) r.Add(i);
			}
			return r;
		}

		static string UtRole { get { return Raft_Network.IsHost ? "host" : "client"; } }

		static List<ResearchTable> UtTables()
		{
			return UnityEngine.Object.FindObjectsOfType<ResearchTable>().Where(t => t != null && UpgradeTableMenu.IsUpgradeTable(t)).OrderBy(t => t.transform.position.x).ToList();
		}

		/// <summary>This player's table: the host's is the first, player 2's the second.</summary>
		static ResearchTable UtMyTable()
		{
			List<ResearchTable> ts = UtTables();
			int i = Raft_Network.IsHost ? 0 : 1;
			return i < ts.Count ? ts[i] : null;
		}

		static string UtRaftPool()
		{
			var inv = ComponentManager<Inventory_ResearchTable>.Value;
			if (inv == null) return "?";
			return string.Join(",", inv.GetResearchedItems().Where(i => i != null).Select(i => i.UniqueIndex).OrderBy(i => i).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
		}

		static void UtBroadcast() { if (Raft_Network.IsHost) IslandNetwork.SendUpgradeTable(UpgradeTable.Message(), null); }

		static long UtNowMs() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

		static IEnumerator UtWaitAt(string[] args)
		{
			foreach (string a in args)
			{
				long at;
				if (a.StartsWith("at=", StringComparison.Ordinal) && long.TryParse(a.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out at))
					while (UtNowMs() < at) yield return null;
			}
		}

		[ConsoleCommand(name: "CIUTSetup", docs: "Dev, two-player test of the upgrade table (host first, then player 2): option on, pools emptied (host), two upgrade tables placed (host), Raft's own research pool noted. Prints the test upgrade and its items")]
		public static void UtSetupCommand(string[] args) { StartTest(UtSetupRoutine()); }

		static IEnumerator UtSetupRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			var inv = ComponentManager<Inventory_ResearchTable>.Value;
			Item_Base table = ExtraUpgrades.TableItem;
			if (player == null || inv == null || table == null || !CustomIslandSpawner.RaftPosition.HasValue) { Fail("ut setup: in a world with the upgrade table registered"); yield break; }
			bool ok = true;
			if (Raft_Network.IsHost)
			{
				if (utOptionsBefore == null) utOptionsBefore = new HashSet<string>(WorldOptions.Current);
				var set = new HashSet<string>(WorldOptions.Current) { WorldOptions.Upgrades };
				WorldOptions.Set(set);
				yield return new WaitForSeconds(0.5f);
				UpgradeTable.SetState("|");
				UtBroadcast();
				List<ResearchTable> have = UtTables();
				for (int i = have.Count; i < 2; i++)
				{
					player.BlockCreator.CreateBlockCheat(table, new Vector3(4f * i, 30f, 0f), Vector3.zero, DPS.Default, 0);
					yield return new WaitForSeconds(0.5f);
				}
			}
			float wait = 0f;
			while (wait < 12f && (UtTables().Count < 2 || !WorldOptions.On(WorldOptions.Upgrades) || UpgradeTable.State() != "|")) { yield return new WaitForSeconds(0.5f); wait += 0.5f; }
			yield return new WaitForSeconds(0.5f);
			utRaftPool = UtRaftPool();
			ExtraUpgrades.Upgrade u = UtUpgrade();
			List<Item_Base> items = UtItems(u);
			Check(ref ok, WorldOptions.On(WorldOptions.Upgrades), UtRole + ": the option 'upgrades' is on");
			Check(ref ok, UtTables().Count >= 2 && UtMyTable() != null && UtMyTable().slot != null, UtRole + ": two upgrade tables, mine has a slot (" + UtTables().Count + " tables)");
			Check(ref ok, UpgradeTable.State() == "|", UtRole + ": the pools are empty (" + UpgradeTable.State() + ")");
			Check(ref ok, u != null && items.Count > 0, UtRole + ": test upgrade " + (u != null ? u.Display : "none") + " with " + items.Count + " research items: " + string.Join(", ", items.Select((it, i) => i + "=" + it.UniqueName).ToArray()));
			Log("UT raft pool " + UtRole + ": [" + utRaftPool + "]");
			if (ok) Log("PASS: ut setup " + UtRole); else Fail("ut setup " + UtRole);
		}

		[ConsoleCommand(name: "CIUTGive", docs: "Dev, upgrade table test: gives this player <n> of the test upgrade's research items. CIUTGive <idx,idx|all> <n>")]
		public static void UtGiveCommand(string[] args) { StartTest(UtGiveRoutine(args)); }

		static IEnumerator UtGiveRoutine(string[] args)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			List<Item_Base> items = UtItems(UtUpgrade());
			int n = 1;
			if (args != null && args.Length > 1) int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
			bool ok = true;
			foreach (int i in UtIdx(args != null && args.Length > 0 ? args[0] : "all", items.Count))
			{
				int before = player.Inventory.GetItemCount(items[i]);
				player.Inventory.AddItem(items[i].UniqueName, n);
				Check(ref ok, player.Inventory.GetItemCount(items[i]) >= before + n || before + n > 1000, UtRole + ": gave " + n + " " + items[i].UniqueName + " (" + before + " -> " + player.Inventory.GetItemCount(items[i]) + ")");
			}
			yield return null;
			if (ok) Log("PASS: ut give " + UtRole); else Fail("ut give " + UtRole);
		}

		[ConsoleCommand(name: "CIUTResearch", docs: "Dev, upgrade table test: this player puts 2 of item <idx> in their table's slot and presses Research (UpgradeTable.AskResearch), then takes the rest back. CIUTResearch <idx> <ok|refused|any> [at=<unix ms>]: expected outcome; 'at' = wait until that wall-clock time first (both Rafts share one PC clock)")]
		public static void UtResearchCommand(string[] args) { StartTest(UtResearchRoutine(args)); }

		static IEnumerator UtResearchRoutine(string[] args)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			List<Item_Base> items = UtItems(UtUpgrade());
			int idx = args != null && args.Length > 0 ? UtIdx(args[0], items.Count).DefaultIfEmpty(-1).First() : -1;
			string expect = args != null && args.Length > 1 ? args[1] : "any";
			ResearchTable rt = UtMyTable();
			if (player == null || idx < 0 || rt == null || rt.slot == null) { Fail("ut research " + UtRole + ": no player / item index / table slot"); yield break; }
			Item_Base it = items[idx];
			bool ok = true;
			int n0 = player.Inventory.GetItemCount(it);
			int take = Math.Min(2, n0);
			Check(ref ok, take > 0, UtRole + ": has " + n0 + " " + it.UniqueName + " to put in the table");
			if (take <= 0) { Fail("ut research " + UtRole); yield break; }
			player.Inventory.RemoveItem(it.UniqueName, take);
			rt.slot.SetItem(new ItemInstance(it, take, it.MaxUses));
			yield return UtWaitAt(args);
			bool did = UpgradeTable.AskResearch(rt.slot);
			yield return new WaitForSeconds(1.5f);
			int left = rt.slot.IsEmpty ? 0 : rt.slot.itemInstance.Amount;
			if (left > 0) { rt.slot.RemoveItem(left); player.Inventory.AddItem(it.UniqueName, left); }
			int lost = n0 - player.Inventory.GetItemCount(it);
			Log("UT research " + UtRole + ": " + it.UniqueName + " did=" + did + " lost=" + lost + " slotLeft=" + left + " researchedHere=" + UpgradeTable.IsResearched(it));
			if (expect == "ok") Check(ref ok, did, UtRole + ": research of " + it.UniqueName + " accepted");
			else if (expect == "refused") Check(ref ok, !did, UtRole + ": research of " + it.UniqueName + " refused");
			Check(ref ok, lost == (did ? 1 : 0), UtRole + ": the item left the slot exactly when researched (took " + take + ", " + left + " back in the slot, lost " + lost + ", accepted " + did + ")");
			if (ok) Log("PASS: ut research " + UtRole); else Fail("ut research " + UtRole);
		}

		[ConsoleCommand(name: "CIUTLearn", docs: "Dev, upgrade table test: this player presses Learn on the test upgrade (UpgradeTable.AskLearn). CIUTLearn <ok|refused>: whether the upgrade could be learned just before")]
		public static void UtLearnCommand(string[] args) { StartTest(UtLearnRoutine(args)); }

		static IEnumerator UtLearnRoutine(string[] args)
		{
			ExtraUpgrades.Upgrade u = UtUpgrade();
			if (u == null) { Fail("ut learn: no upgrade"); yield break; }
			bool ok = true;
			bool can = UpgradeTable.CanLearn(u) && !UpgradeTable.IsLearned(u);
			string expect = args != null && args.Length > 0 ? args[0] : "ok";
			Check(ref ok, can == (expect == "ok"), UtRole + ": " + u.Display + " " + (can ? "can" : "can't") + " be learned now (expected " + expect + ")");
			yield return UtWaitAt(args ?? new string[0]);
			UpgradeTable.AskLearn(u);
			yield return new WaitForSeconds(2f);
			Log("UT learn " + UtRole + ": " + u.Display + " learned=" + UpgradeTable.IsLearned(u));
			if (ok) Log("PASS: ut learn " + UtRole); else Fail("ut learn " + UtRole);
		}

		[ConsoleCommand(name: "CIUTCheck", docs: "Dev, upgrade table test: checks this Raft's pools. CIUTCheck <researched idx,idx|all|-> <learned 0|1> <craftable 0|1> [noraft]: exactly those items researched here, the test upgrade learned/craftable or not, Raft's own research pool as CIUTSetup noted it. Prints 'UT state' for comparing the two players")]
		public static void UtCheckCommand(string[] args) { StartTest(UtCheckRoutine(args)); }

		static IEnumerator UtCheckRoutine(string[] args)
		{
			ExtraUpgrades.Upgrade u = UtUpgrade();
			List<Item_Base> items = UtItems(u);
			if (u == null) { Fail("ut check: no upgrade"); yield break; }
			yield return null;
			bool ok = true;
			List<int> want = UtIdx(args != null && args.Length > 0 ? args[0] : "-", items.Count);
			bool wantLearned = args != null && args.Length > 1 && args[1] == "1", wantCraft = args != null && args.Length > 2 && args[2] == "1";
			bool noRaft = args != null && args.Contains("noraft");
			var wantIdx = new HashSet<int>(want.Select(i => items[i].UniqueIndex));
			var have = new HashSet<int>(UpgradeTable.Researched);
			Log("UT state " + UtRole + ": " + UpgradeTable.State());
			Check(ref ok, have.SetEquals(wantIdx), UtRole + ": researched here [" + string.Join(",", have.OrderBy(i => i).Select(i => i.ToString()).ToArray()) + "], expected [" + string.Join(",", wantIdx.OrderBy(i => i).Select(i => i.ToString()).ToArray()) + "]");
			Check(ref ok, UpgradeTable.IsLearned(u) == wantLearned, UtRole + ": " + u.Display + " learned " + UpgradeTable.IsLearned(u) + ", expected " + wantLearned);
			Check(ref ok, ExtraUpgrades.Craftable(u) == wantCraft, UtRole + ": " + u.Display + " craftable " + ExtraUpgrades.Craftable(u) + ", expected " + wantCraft);
			if (!noRaft && utRaftPool != null)
			{
				string now = UtRaftPool();
				Check(ref ok, now == utRaftPool, UtRole + ": Raft's own table pool unchanged [" + now + "] vs [" + utRaftPool + "]");
			}
			if (ok) Log("PASS: ut check " + UtRole); else Fail("ut check " + UtRole);
		}

		[ConsoleCommand(name: "CIUTFill", docs: "Dev, upgrade table test: fills every empty inventory slot of this player with a filler item (CIUTUnfill takes them out again)")]
		public static void UtFillCommand(string[] args) { StartTest(UtFillRoutine(true)); }

		[ConsoleCommand(name: "CIUTUnfill", docs: "Dev, upgrade table test: removes the filler items CIUTFill put in")]
		public static void UtUnfillCommand(string[] args) { StartTest(UtFillRoutine(false)); }

		static IEnumerator UtFillRoutine(bool fill)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("ut fill: no player"); yield break; }
			bool ok = true;
			if (fill)
			{
				HashSet<Item_Base> ours = UpgradeTable.Researchable();
				utFiller = new[] { "Stone", "Rope", "Scrap", "Plastic", "Brick_Dry", "Thatch" }.Select(n => ItemManager.GetItemByName(n)).FirstOrDefault(i => i != null && !ours.Contains(i));
				if (utFiller == null) { Fail("ut fill: no filler item"); yield break; }
				utFillSlots.Clear();
				foreach (Slot s in player.Inventory.allSlots.Where(s => s != null && s.IsEmpty).ToList())
				{
					s.SetItem(new ItemInstance(utFiller, 1, utFiller.MaxUses));
					utFillSlots.Add(s);
				}
				int empty = player.Inventory.allSlots.Count(s => s != null && s.IsEmpty);
				Check(ref ok, empty == 0, "inventory full: filled " + utFillSlots.Count + " slots with " + utFiller.UniqueName + ", " + empty + " empty left");
			}
			else
			{
				foreach (Slot s in utFillSlots) if (s != null && !s.IsEmpty && s.itemInstance.baseItem == utFiller) s.RemoveItem(s.itemInstance.Amount);
				Check(ref ok, true, "removed the filler from " + utFillSlots.Count + " slots");
				utFillSlots.Clear();
			}
			if (ok) Log("PASS: ut fill"); else Fail("ut fill");
		}

		[ConsoleCommand(name: "CIUTDie", docs: "Dev, upgrade table test: this player dies and respawns (Raft's own death, then the respawn button), then checks the table and the pools are still there")]
		public static void UtDieCommand(string[] args) { StartTest(UtDieRoutine()); }

		static IEnumerator UtDieRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Player body = player != null ? player.GetComponentInChildren<Player>(true) : null;
			if (body == null) { Fail("ut die: no player body"); yield break; }
			bool ok = true;
			string stateBefore = UpgradeTable.State();
			body.Kill(false);
			for (float t = 0; t < 6f && !body.IsDead; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			Check(ref ok, body.IsDead, UtRole + ": the player died");
			yield return new WaitForSecondsRealtime(2f);
			BedManager beds = ComponentManager<BedManager>.Value ?? UnityEngine.Object.FindObjectOfType<BedManager>() ?? Resources.FindObjectsOfTypeAll<BedManager>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (beds != null) beds.Button_Respawn(); else body.RespawnWithoutBed(false);
			for (float t = 0; t < 10f && body.IsDead; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			yield return new WaitForSecondsRealtime(2f);
			yield return EnsureAlive();
			Check(ref ok, !body.IsDead, UtRole + ": the player is up again");
			Check(ref ok, UpgradeTable.State() == stateBefore, UtRole + ": the upgrade table's pools are the same after respawning (" + UpgradeTable.State() + ")");
			Check(ref ok, UtMyTable() != null && UtMyTable().slot != null, UtRole + ": my upgrade table is still there");
			if (ok) Log("PASS: ut die " + UtRole); else Fail("ut die " + UtRole);
		}

		[ConsoleCommand(name: "CIUTHold", docs: "Dev, upgrade table test: this player moves 2 of item <idx> into their table's slot, as with the menu open, and keeps the slot (CIUTRelease presses Research on it later)")]
		public static void UtHoldCommand(string[] args) { StartTest(UtHoldRoutine(args)); }

		static IEnumerator UtHoldRoutine(string[] args)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			List<Item_Base> items = UtItems(UtUpgrade());
			int idx = args != null && args.Length > 0 ? UtIdx(args[0], items.Count).DefaultIfEmpty(-1).First() : -1;
			ResearchTable rt = UtMyTable();
			if (player == null || idx < 0 || rt == null || rt.slot == null) { Fail("ut hold: no player / index / table"); yield break; }
			Item_Base it = items[idx];
			int n0 = player.Inventory.GetItemCount(it), take = Math.Min(2, n0);
			if (take <= 0) { Fail("ut hold: none of " + it.UniqueName); yield break; }
			utHeldStart = n0; utHeldIdx = idx;
			player.Inventory.RemoveItem(it.UniqueName, take);
			rt.slot.SetItem(new ItemInstance(it, take, it.MaxUses));
			utHeldSlot = rt.slot;
			// (as with the menu open: the table is this player's while it holds the slot - the give back on a taken table needs it)
			HarmonyLib.Traverse.Create(rt).Field("occupyingPlayerID").SetValue(player.steamID);
			utHeldTable = rt;
			yield return null;
			Log("PASS: ut hold " + UtRole + ": " + take + " " + it.UniqueName + " in the table slot (" + n0 + " owned before)");
		}

		[ConsoleCommand(name: "CIUTRelease", docs: "Dev, upgrade table test: presses Research on the slot CIUTHold kept (the table may be gone), takes what is left back, and checks no item is lost or doubled and nothing throws")]
		public static void UtReleaseCommand(string[] args) { StartTest(UtReleaseRoutine()); }

		static IEnumerator UtReleaseRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			List<Item_Base> items = UtItems(UtUpgrade());
			// (ReferenceEquals: once the table is picked up Unity calls the slot null, and that is the case tested)
			if (player == null || ReferenceEquals(utHeldSlot, null) || utHeldIdx < 0 || utHeldIdx >= items.Count) { Fail("ut release: nothing held"); yield break; }
			Item_Base it = items[utHeldIdx];
			bool ok = true, did = false, threw = false;
			string slotInfo = "slot gone";
			try { did = UpgradeTable.AskResearch(utHeldSlot); }
			catch (Exception e) { threw = true; Debug.LogException(e); Log("UT release: AskResearch threw " + e.GetType().Name + ": " + e.Message); }
			yield return new WaitForSeconds(1.5f);
			int left = 0;
			try { left = utHeldSlot.IsEmpty ? 0 : utHeldSlot.itemInstance.Amount; slotInfo = "slot has " + left; if (left > 0) utHeldSlot.RemoveItem(left); }
			catch (Exception e) { slotInfo = "slot unreadable (" + e.GetType().Name + ")"; left = 0; }
			if (left > 0) player.Inventory.AddItem(it.UniqueName, left);
			int total = player.Inventory.GetItemCount(it) + (did ? 1 : 0);
			Log("UT release " + UtRole + ": " + it.UniqueName + " did=" + did + ", " + slotInfo + ", inventory " + player.Inventory.GetItemCount(it) + ", started with " + utHeldStart);
			Check(ref ok, !threw, UtRole + ": nothing thrown pressing Research on a slot whose table was picked up");
			Check(ref ok, total <= utHeldStart, UtRole + ": nothing doubled (inventory + researched " + total + " <= " + utHeldStart + ")");
			Check(ref ok, total >= utHeldStart, UtRole + ": nothing lost (inventory + researched " + total + " >= " + utHeldStart + ")");
			utHeldSlot = null;
			if (utHeldTable != null) HarmonyLib.Traverse.Create(utHeldTable).Field("occupyingPlayerID").SetValue(default(Network_UserId));
			utHeldTable = null;
			if (ok) Log("PASS: ut release " + UtRole); else Fail("ut release " + UtRole);
		}

		[ConsoleCommand(name: "CIUTRemoveTable", docs: "Dev, upgrade table test (host): picks up upgrade table <n> (0 = the host's, 1 = player 2's; default all)")]
		public static void UtRemoveTableCommand(string[] args) { StartTest(UtRemoveTableRoutine(args)); }

		static IEnumerator UtRemoveTableRoutine(string[] args)
		{
			if (!Raft_Network.IsHost) { Fail("ut remove table: host only"); yield break; }
			List<ResearchTable> ts = UtTables();
			int which = -1;
			if (args != null && args.Length > 0) int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out which);
			int removed = 0;
			for (int i = 0; i < ts.Count; i++)
			{
				if (which >= 0 && i != which) continue;
				Block b = ts[i].GetComponent<Block>();
				if (b != null) { BlockCreator.RemoveBlockNetwork(b, null, true); removed++; }
			}
			yield return new WaitForSeconds(1f);
			Log("PASS: ut remove table: picked up " + removed + " (" + UtTables().Count + " left)");
		}

		[ConsoleCommand(name: "CIUTEnd", docs: "Dev, upgrade table test (host): puts everything back - tables picked up, pools emptied (sent to player 2), the world options as before")]
		public static void UtEndCommand(string[] args) { StartTest(UtEndRoutine()); }

		static IEnumerator UtEndRoutine()
		{
			if (!Raft_Network.IsHost) { Log("PASS: ut end (not the host: nothing to put back)"); yield break; }
			foreach (ResearchTable t in UtTables())
			{
				Block b = t.GetComponent<Block>();
				if (b != null) BlockCreator.RemoveBlockNetwork(b, null, true);
			}
			UpgradeTable.SetState("|");
			UtBroadcast();
			if (utOptionsBefore != null) { WorldOptions.Set(utOptionsBefore); utOptionsBefore = null; }
			yield return new WaitForSeconds(1f);
			Log("PASS: ut end: " + UtTables().Count + " tables left, pools " + UpgradeTable.State());
		}
	}
}
