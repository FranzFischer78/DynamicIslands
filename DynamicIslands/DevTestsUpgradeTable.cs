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
	/// <summary>Upgrade research table tests (catalogue RT6-RT9, RT11, RT12; RT10 and RT13 are in CIUpgradeTable).</summary>
	public static partial class DevTests
	{
		#region Helpers

		static bool UtReady(string name, out Network_Player player, out Inventory_ResearchTable inv, out Item_Base table, out Item_Base raftTable)
		{
			player = RAPI.GetLocalPlayer();
			inv = ComponentManager<Inventory_ResearchTable>.Value;
			table = ExtraUpgrades.TableItem;
			raftTable = ItemManager.GetItemByName("Placeable_ResearchTable");
			if (player == null || inv == null || !CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail(name + ": host, in a world"); return false; }
			if (table == null || raftTable == null) { Fail(name + ": not registered (" + (raftTable == null ? "no Placeable_ResearchTable" : "no upgrade table") + ")"); return false; }
			return true;
		}

		static ResearchTable UtTableOf(Block b) { return b != null ? b.GetComponentInChildren<ResearchTable>(true) : null; }

		/// <summary>Opens a table in the one menu as Raft does (Inventory_ResearchTable.SetTable, so the mod's patch runs).</summary>
		static bool UtOpen(Inventory_ResearchTable inv, ResearchTable t)
		{
			System.Reflection.MethodInfo m = HarmonyLib.AccessTools.Method(typeof(Inventory_ResearchTable), "SetTable");
			if (m == null || t == null) return false;
			m.Invoke(inv, new object[] { t });
			return true;
		}

		/// <summary>Closes the open menu as Esc does.</summary>
		static void UtEsc()
		{
			CanvasHelper ch = ComponentManager<CanvasHelper>.Value ?? UnityEngine.Object.FindObjectOfType<CanvasHelper>();
			if (ch != null) ch.CloseMenu(CanvasHelper.ActiveMenu);
		}

		static void UtMenu(Inventory_ResearchTable inv, out List<ResearchMenuItem> entries, out List<AvaialableResearchItem> icons)
		{
			Transform content = HarmonyLib.Traverse.Create(inv).Field("content").GetValue<RectTransform>();
			Transform iconRoot = HarmonyLib.Traverse.Create(inv).Field("researchItemContent").GetValue<RectTransform>();
			entries = content.GetComponentsInChildren<ResearchMenuItem>(false).Where(m => m.transform.parent == content).ToList();
			icons = iconRoot.GetComponentsInChildren<AvaialableResearchItem>(false).Where(a => a.transform.parent == iconRoot).ToList();
		}

		static string UtTicks(ResearchMenuItem m) { return string.Join("", UpgradeTableMenu.Ticks(m).Select(b => b != null && b.BingoState ? "1" : "0").ToArray()); }

		/// <summary>Raft's own entries (name, ticks, learned) and its researched items, as one string.</summary>
		static string UtRaftSignature(Inventory_ResearchTable inv)
		{
			var parts = new List<string>();
			foreach (ResearchMenuItem m in inv.GetMenuItems())
			{
				if (m == null || UpgradeTableMenu.IsModEntry(m)) continue;
				parts.Add(m.GetItem().UniqueName + ":" + UtTicks(m) + (m.Learned ? "L" : "-"));
			}
			parts.Sort(StringComparer.Ordinal);
			var res = inv.GetResearchedItems().Where(i => i != null).Select(i => i.UniqueName).ToList();
			res.Sort(StringComparer.Ordinal);
			return string.Join(";", parts.ToArray()) + "|" + string.Join(",", res.ToArray());
		}

		/// <summary>Takes Raft's research of the items back (the list, the icons, the ticks).</summary>
		static void UtRestoreRaft(Inventory_ResearchTable inv, List<Item_Base> list, List<Item_Base> before)
		{
			foreach (Item_Base i in list.Where(i => !before.Contains(i)).ToList())
			{
				list.Remove(i);
				AvaialableResearchItem icon;
				if (UpgradeTableMenu.Icons(inv).TryGetValue(i, out icon) && icon != null) icon.SetResearchedState(false);
				foreach (ResearchMenuItem m in inv.GetMenuItems().Where(m => m != null && !UpgradeTableMenu.IsModEntry(m)))
					foreach (BingoMenuItem t in UpgradeTableMenu.Ticks(m)) if (t != null && t.BingoItem == i && t.BingoState) t.SetBingoState(false);
			}
		}

		class UtBaseline { public int Entries, Icons; public string Signature; }

		/// <summary>The open menu shows only its own kind's entries and icons, none lost or doubled.</summary>
		static void UtVerifyMenu(ref bool ok, Inventory_ResearchTable inv, bool upgrades, string label, UtBaseline raft)
		{
			List<ResearchMenuItem> entries; List<AvaialableResearchItem> icons;
			UtMenu(inv, out entries, out icons);
			HashSet<Item_Base> ours = UpgradeTable.Researchable();
			var raftItems = new HashSet<Item_Base>();
			foreach (ResearchMenuItem m in inv.GetMenuItems())
				if (m != null && !UpgradeTableMenu.IsModEntry(m))
					foreach (CostMultiple c in m.GetItem().settings_recipe.NewCost ?? new CostMultiple[0])
						foreach (Item_Base i in c.items ?? new Item_Base[0]) if (i != null) raftItems.Add(i);
			int dupEntries = entries.GroupBy(m => m.GetItem()).Count(g => g.Count() > 1), dupIcons = icons.Where(a => a.Item != null).GroupBy(a => a.Item).Count(g => g.Count() > 1);
			if (upgrades)
			{
				int teaches = UpgradeTable.Teaches.Count();
				int foreign = icons.Count(a => a.Item == null || !ours.Contains(a.Item));
				int expectedIcons = UpgradeTableMenu.Icons(inv).Count(kv => kv.Value != null && ours.Contains(kv.Key));
				Check(ref ok, entries.Count == teaches && entries.All(m => UpgradeTableMenu.UpgradeOf(m) != null) && dupEntries == 0, label + ": upgrade menu has " + entries.Count + " entries, all upgrades (" + teaches + "), doubled " + dupEntries);
				Check(ref ok, icons.Count == expectedIcons && foreign == 0 && dupIcons == 0, label + ": upgrade menu has " + icons.Count + " icons (" + expectedIcons + "), not the upgrades' " + foreign + ", doubled " + dupIcons);
				bool ticksOk = entries.All(m => UpgradeTableMenu.Ticks(m).All(b => b == null || b.BingoState == UpgradeTable.IsResearched(b.BingoItem)));
				Check(ref ok, ticksOk && entries.All(m => m.Learned == UpgradeTable.IsLearned(UpgradeTableMenu.UpgradeOf(m))), label + ": upgrade menu's ticks and learned state are the upgrade table's pools");
			}
			else
			{
				int mod = entries.Count(m => UpgradeTableMenu.IsModEntry(m));
				int onlyOurs = icons.Count(a => a.Item != null && ours.Contains(a.Item) && !raftItems.Contains(a.Item));
				Check(ref ok, mod == 0 && entries.Count == raft.Entries && dupEntries == 0, label + ": Raft's menu has " + entries.Count + " entries (" + raft.Entries + " at first), upgrades " + mod + ", doubled " + dupEntries);
				Check(ref ok, icons.Count == raft.Icons && onlyOurs == 0 && dupIcons == 0, label + ": Raft's menu has " + icons.Count + " icons (" + raft.Icons + " at first), only the upgrades' " + onlyOurs + ", doubled " + dupIcons);
				string sig = UtRaftSignature(inv);
				Check(ref ok, sig == raft.Signature, label + ": Raft's ticks, learned and researched state unchanged" + (sig == raft.Signature ? "" : " - CHANGED"));
			}
		}

		#endregion

		#region RT6, RT7: switching between the tables

		[ConsoleCommand(name: "CIUpgradeTableSwitch", docs: "Dev, world (host): Raft's research table, the upgrade table, Raft's again, several times and also closing with Esc: each menu shows only its own entries and icons (none lost or doubled), Raft's ticks and learned state unchanged; an item researched at Raft's table ticks only Raft's entries and is not researched at the upgrade table (and the other way round), Raft's learn-instantly on an upgrade's entry does nothing. Everything put back")]
		public static void UpgradeTableSwitchCommand(string[] args) { StartTest(UpgradeTableSwitchRoutine()); }

		static IEnumerator UpgradeTableSwitchRoutine()
		{
			Network_Player player; Inventory_ResearchTable inv; Item_Base table, raftTable;
			if (!UtReady("upgrade table switch", out player, out inv, out table, out raftTable)) yield break;
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			string stateBefore = UpgradeTable.State();
			List<Item_Base> raftResearched = inv.GetResearchedItems();
			var raftBefore = new List<Item_Base>(raftResearched);
			Block upBlock = null, raftBlock = null;
			try
			{
				var set = new HashSet<string>(optionsBefore); set.Add(WorldOptions.Upgrades); WorldOptions.Set(set);
				UpgradeTable.SetState("|");
				yield return null; yield return null;
				raftBlock = player.BlockCreator.CreateBlockCheat(raftTable, new Vector3(0f, 30f, 0f), Vector3.zero, DPS.Default, 0);
				upBlock = player.BlockCreator.CreateBlockCheat(table, new Vector3(8f, 30f, 0f), Vector3.zero, DPS.Default, 0);
				yield return new WaitForSeconds(0.5f);
				ResearchTable rt = UtTableOf(raftBlock), ut = UtTableOf(upBlock);
				Check(ref ok, rt != null && ut != null && !UpgradeTableMenu.IsUpgradeTable(rt) && UpgradeTableMenu.IsUpgradeTable(ut) && rt.slot != null && ut.slot != null, "placed: Raft's table and the upgrade table");
				if (rt == null || ut == null || rt.slot == null || ut.slot == null || !UtOpen(inv, rt)) { Check(ref ok, false, "the tables can be opened"); yield break; }
				yield return null;
				List<ResearchMenuItem> e0; List<AvaialableResearchItem> i0;
				UtMenu(inv, out e0, out i0);
				var raftBase = new UtBaseline { Entries = e0.Count, Icons = i0.Count, Signature = UtRaftSignature(inv) };
				Check(ref ok, raftBase.Entries > 0 && raftBase.Icons > 0 && !e0.Any(m => UpgradeTableMenu.IsModEntry(m)), "Raft's table opened: " + raftBase.Entries + " entries, " + raftBase.Icons + " icons, none an upgrade's");
				UtVerifyMenu(ref ok, inv, false, "Raft's table", raftBase);
				// Back and forth, then with Esc between
				string[] route = { "U", "R", "U", "R", "U", "esc", "R", "esc", "U", "esc", "esc", "R", "U", "R" };
				int step = 0;
				foreach (string go in route)
				{
					step++;
					if (go == "esc") { UtEsc(); yield return null; continue; }
					UtOpen(inv, go == "U" ? ut : rt);
					yield return null;
					UtVerifyMenu(ref ok, inv, go == "U", "step " + step + " (" + go + ")", raftBase);
				}
				// RT7: an item both tables use, researched at Raft's table first
				HashSet<Item_Base> ours = UpgradeTable.Researchable();
				var raftEntries = inv.GetMenuItems().Where(m => m != null && !UpgradeTableMenu.IsModEntry(m)).ToList();
				Item_Base both = ours.FirstOrDefault(i => raftEntries.Any(m => m.ContainsItem(i)) && !raftResearched.Contains(i) && !i.settings_recipe.IsBlueprint);
				if (both == null) { Check(ref ok, false, "an item both tables research, not researched at Raft's"); yield break; }
				UtOpen(inv, rt);
				yield return null;
				inv.Research(both, false);
				yield return null;
				var withItem = raftEntries.Where(m => m.ContainsItem(both)).ToList();
				bool raftTicked = withItem.Count > 0 && withItem.All(m => UpgradeTableMenu.Ticks(m).Any(b => b != null && b.BingoItem == both && b.BingoState));
				Check(ref ok, raftResearched.Contains(both) && raftTicked && !UpgradeTable.IsResearched(both), both.UniqueName + " researched at Raft's table: Raft's " + withItem.Count + " entries ticked (" + raftTicked + "), not in the upgrade table's pool");
				string sigAfter = UtRaftSignature(inv);
				UtOpen(inv, ut);
				yield return null;
				List<ResearchMenuItem> ue; List<AvaialableResearchItem> ui;
				UtMenu(inv, out ue, out ui);
				AvaialableResearchItem upIcon = ui.FirstOrDefault(a => a.Item == both);
				bool upgradeTick = ue.Any(m => UpgradeTableMenu.Ticks(m).Any(b => b != null && b.BingoItem == both && b.BingoState));
				Check(ref ok, upIcon != null && !upIcon.Researched && !upgradeTick, "upgrade table's menu after it: " + both.UniqueName + " icon " + (upIcon == null ? "missing" : upIcon.Researched ? "RESEARCHED" : "not researched") + ", an upgrade entry ticked: " + upgradeTick);
				// Raft's learn-instantly never reaches an upgrade's entry
				ResearchMenuItem upEntry = ue.FirstOrDefault(m => UpgradeTableMenu.UpgradeOf(m) != null && UpgradeTableMenu.Ticks(m).Any(b => b != null && b.BingoItem == both)) ?? ue.FirstOrDefault(m => UpgradeTableMenu.UpgradeOf(m) != null);
				ExtraUpgrades.Upgrade uu = UpgradeTableMenu.UpgradeOf(upEntry);
				if (upEntry != null)
				{
					string before = UtTicks(upEntry);
					upEntry.LearnInstantly();
					yield return null; yield return null;
					Check(ref ok, !upEntry.Learned && !UpgradeTable.IsLearned(uu) && !uu.Item.settings_recipe.Learned && before == UtTicks(upEntry),
						uu.Display + ": Raft's learn-instantly on its entry does nothing (learned " + upEntry.Learned + ", craftable " + uu.Item.settings_recipe.Learned + ", ticks " + before + " -> " + UtTicks(upEntry) + ")");
				}
				else Check(ref ok, false, "an upgrade entry to try learn-instantly on");
				// Research at the upgrade table (the Research button's path), then back at Raft's
				ut.slot.SetItem(new ItemInstance(both, 1, both.MaxUses));
				bool did = UpgradeTable.AskResearch(ut.slot);
				yield return null;
				Check(ref ok, did && UpgradeTable.IsResearched(both), both.UniqueName + " researched at the upgrade table: in its pool");
				UtVerifyMenu(ref ok, inv, true, "upgrade table after its research", raftBase);
				UtOpen(inv, rt);
				yield return null;
				List<ResearchMenuItem> re; List<AvaialableResearchItem> ri;
				UtMenu(inv, out re, out ri);
				Check(ref ok, UtRaftSignature(inv) == sigAfter && ri.Any(a => a.Item == both && a.Researched) && re.Count == raftBase.Entries,
					"back at Raft's table: its state is as before the upgrade table's research, " + both.UniqueName + " still researched there");
				UtEsc(); yield return null;
				UtOpen(inv, ut); yield return null;
				UtVerifyMenu(ref ok, inv, true, "after Esc", raftBase);
				UtOpen(inv, rt); yield return null;
				Check(ref ok, raftResearched.Contains(both) && UpgradeTable.IsResearched(both), "both pools keep the item");
				if (!ut.slot.IsEmpty) ut.slot.RemoveItem(ut.slot.itemInstance.Amount);
			}
			finally
			{
				UpgradeTable.SetState(stateBefore);
				WorldOptions.Set(optionsBefore);
				UtRestoreRaft(inv, raftResearched, raftBefore);
				UpgradeTableMenu.Show(false);
				if (upBlock != null) BlockCreator.RemoveBlockNetwork(upBlock, null, true);
				if (raftBlock != null) BlockCreator.RemoveBlockNetwork(raftBlock, null, true);
			}
			if (ok) Log("PASS: upgrade table switch"); else Fail("upgrade table switch");
		}

		#endregion

		#region RT8: two upgrade tables

		[ConsoleCommand(name: "CIUpgradeTableTwo", docs: "Dev, world (host): two upgrade tables placed share one pool (an item researched at one counts at the other); picking one up keeps the pool, and an item in its slot comes back to the player; the other table still works. Everything put back")]
		public static void UpgradeTableTwoCommand(string[] args) { StartTest(UpgradeTableTwoRoutine()); }

		static IEnumerator UpgradeTableTwoRoutine()
		{
			Network_Player player; Inventory_ResearchTable inv; Item_Base table, raftTable;
			if (!UtReady("two upgrade tables", out player, out inv, out table, out raftTable)) yield break;
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			string stateBefore = UpgradeTable.State();
			Block a = null, b = null;
			int tablesBefore = player.Inventory.GetItemCount(table.UniqueName);
			Item_Base filler = ItemManager.GetItemByName("Plank");
			int fillerBefore = filler != null ? player.Inventory.GetItemCount(filler.UniqueName) : 0;
			try
			{
				var set = new HashSet<string>(optionsBefore); set.Add(WorldOptions.Upgrades); WorldOptions.Set(set);
				UpgradeTable.SetState("|");
				yield return null; yield return null;
				a = player.BlockCreator.CreateBlockCheat(table, new Vector3(0f, 30f, 0f), Vector3.zero, DPS.Default, 0);
				b = player.BlockCreator.CreateBlockCheat(table, new Vector3(8f, 30f, 0f), Vector3.zero, DPS.Default, 0);
				yield return new WaitForSeconds(0.5f);
				ResearchTable ta = UtTableOf(a), tb = UtTableOf(b);
				Check(ref ok, ta != null && tb != null && ta != tb && UpgradeTableMenu.IsUpgradeTable(ta) && UpgradeTableMenu.IsUpgradeTable(tb) && ta.slot != null && tb.slot != null, "two upgrade tables placed");
				if (ta == null || tb == null || ta.slot == null || tb.slot == null || filler == null) { Check(ref ok, false, "tables and a filler item"); yield break; }
				List<Item_Base> items = UpgradeTable.Researchable().ToList();
				Item_Base one = items[0], two = items.Count > 1 ? items[1] : items[0];
				ta.slot.SetItem(new ItemInstance(one, 1, one.MaxUses));
				bool did = UpgradeTable.AskResearch(ta.slot);
				yield return null;
				Check(ref ok, did && UpgradeTable.IsResearched(one), one.UniqueName + " researched at table 1");
				// At table 2 the same item is already researched; a new one researches into the same pool
				tb.slot.SetItem(new ItemInstance(one, 1, one.MaxUses));
				Check(ref ok, !UpgradeTable.CanResearch(one) && !UpgradeTable.AskResearch(tb.slot), "table 2: " + one.UniqueName + " counts as researched (refused again)");
				tb.slot.RemoveItem(1);
				string poolBefore = UpgradeTable.State();
				if (two != one)
				{
					tb.slot.SetItem(new ItemInstance(two, 1, two.MaxUses));
					Check(ref ok, UpgradeTable.AskResearch(tb.slot) && UpgradeTable.IsResearched(two) && UpgradeTable.IsResearched(one), two.UniqueName + " researched at table 2: one pool holds both");
					poolBefore = UpgradeTable.State();
				}
				// An item in table 1's slot, then table 1 is picked up
				ta.slot.SetItem(new ItemInstance(filler, 3, filler.MaxUses));
				yield return null;
				BlockCreator.RemoveBlockNetwork(a, player, true);
				a = null;
				yield return new WaitForSeconds(0.7f);
				int gotFiller = player.Inventory.GetItemCount(filler.UniqueName) - fillerBefore;
				Check(ref ok, UpgradeTable.State() == poolBefore, "table 1 picked up: the pool is kept (" + UpgradeTable.State() + ")");
				Check(ref ok, gotFiller == 3, "table 1 picked up: the 3 " + filler.UniqueName + " in its slot came back to the player (" + gotFiller + ")");
				// Table 2 is unharmed
				Check(ref ok, tb != null && UpgradeTableMenu.IsUpgradeTable(tb) && tb.slot != null && UtOpen(inv, tb), "table 2 still opens");
				yield return null;
				Check(ref ok, UpgradeTableMenu.Showing && UpgradeTable.State() == poolBefore, "table 2's menu is the upgrade menu, same pool");
			}
			finally
			{
				UpgradeTable.SetState(stateBefore);
				WorldOptions.Set(optionsBefore);
				UpgradeTableMenu.Show(false);
				if (a != null) BlockCreator.RemoveBlockNetwork(a, null, true);
				if (b != null) BlockCreator.RemoveBlockNetwork(b, null, true);
				if (filler != null) { int extra = player.Inventory.GetItemCount(filler.UniqueName) - fillerBefore; if (extra > 0) player.Inventory.RemoveItem(filler.UniqueName, extra); }
				int tables = player.Inventory.GetItemCount(table.UniqueName) - tablesBefore;
				if (tables > 0) player.Inventory.RemoveItem(table.UniqueName, tables);
			}
			if (ok) Log("PASS: two upgrade tables"); else Fail("two upgrade tables");
		}

		#endregion

		#region RT9: the option off and on

		[ConsoleCommand(name: "CIUpgradeTableOption", docs: "Dev, world (host): the world option Extra upgrades switched off with upgrades learned: none craftable, the table not craftable, the upgrade table placed earlier stays and the learned set is kept; switched on again: every learned upgrade craftable again. The option and pools put back")]
		public static void UpgradeTableOptionCommand(string[] args) { StartTest(UpgradeTableOptionRoutine()); }

		static IEnumerator UpgradeTableOptionRoutine()
		{
			Network_Player player; Inventory_ResearchTable inv; Item_Base table, raftTable;
			if (!UtReady("upgrade table option", out player, out inv, out table, out raftTable)) yield break;
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			string stateBefore = UpgradeTable.State();
			Block made = null;
			try
			{
				var on = new HashSet<string>(optionsBefore); on.Add(WorldOptions.Upgrades);
				var off = new HashSet<string>(optionsBefore); off.Remove(WorldOptions.Upgrades);
				WorldOptions.Set(on);
				List<ExtraUpgrades.Upgrade> teaches = UpgradeTable.Teaches.ToList();
				foreach (ExtraUpgrades.Upgrade u in teaches) UpgradeTable.SetLearned(u, true);
				yield return null; yield return null;
				string learnedState = UpgradeTable.State();
				Check(ref ok, teaches.Count > 0 && teaches.All(u => u.Item.settings_recipe.Learned) && table.settings_recipe.Learned, "option on, " + teaches.Count + " upgrades learned: all craftable, the table craftable");
				made = player.BlockCreator.CreateBlockCheat(table, new Vector3(0f, 30f, 0f), Vector3.zero, DPS.Default, 0);
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, UtTableOf(made) != null, "an upgrade table placed");
				WorldOptions.Set(off);
				yield return null; yield return null;
				var stillCraftable = teaches.Where(u => u.Item.settings_recipe.Learned || ExtraUpgrades.Craftable(u)).Select(u => u.Display).ToList();
				Check(ref ok, stillCraftable.Count == 0 && !table.settings_recipe.Learned && !ExtraUpgrades.Craftable(ExtraUpgrades.TableUpgrade),
					"option off: no upgrade craftable (" + stillCraftable.Count + (stillCraftable.Count > 0 ? ": " + string.Join(", ", stillCraftable.Take(6).ToArray()) : "") + "), the table not craftable (" + !table.settings_recipe.Learned + ")");
				Check(ref ok, made != null && UtTableOf(made) != null && UpgradeTable.State() == learnedState, "option off: the placed table stays, the learned set is kept");
				WorldOptions.Set(on);
				yield return null; yield return null;
				var missing = teaches.Where(u => !u.Item.settings_recipe.Learned).Select(u => u.Display).ToList();
				Check(ref ok, missing.Count == 0 && table.settings_recipe.Learned && UpgradeTable.State() == learnedState, "option on again: all learned upgrades craftable again (" + (teaches.Count - missing.Count) + " of " + teaches.Count + "), the table too" + (missing.Count > 0 ? " - missing " + string.Join(", ", missing.Take(6).ToArray()) : ""));
			}
			finally
			{
				UpgradeTable.SetState(stateBefore);
				WorldOptions.Set(optionsBefore);
				if (made != null) BlockCreator.RemoveBlockNetwork(made, null, true);
			}
			if (ok) Log("PASS: upgrade table option"); else Fail("upgrade table option");
		}

		#endregion

		#region RT12: every upgrade

		[ConsoleCommand(name: "CIUpgradeTableAll", docs: "Dev, world (host): every registered upgrade (not the table): each cost line has an item that can be researched at the upgrade table (reports any that can't); all researched with the real Research path -> every upgrade learnable -> learned with its Learn button -> craftable. Pools and option put back")]
		public static void UpgradeTableAllCommand(string[] args) { StartTest(UpgradeTableAllRoutine()); }

		static IEnumerator UpgradeTableAllRoutine()
		{
			Network_Player player; Inventory_ResearchTable inv; Item_Base table, raftTable;
			if (!UtReady("upgrade table all", out player, out inv, out table, out raftTable)) yield break;
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			string stateBefore = UpgradeTable.State();
			Block made = null;
			try
			{
				var set = new HashSet<string>(optionsBefore); set.Add(WorldOptions.Upgrades); WorldOptions.Set(set);
				UpgradeTable.SetState("|");
				yield return null; yield return null;
				List<ExtraUpgrades.Upgrade> teaches = UpgradeTable.Teaches.ToList();
				Check(ref ok, teaches.Count > 0 && teaches.All(u => !ExtraUpgrades.Craftable(u)), teaches.Count + " upgrades, none craftable before learning");
				HashSet<Item_Base> researchable = UpgradeTable.Researchable();
				foreach (ExtraUpgrades.Upgrade u in teaches)
				{
					CostMultiple[] cost = u.Item.settings_recipe.NewCost ?? new CostMultiple[0];
					List<Item_Base> items = UpgradeTable.ResearchItems(u).ToList();
					bool linesOk = cost.Length > 0 && items.Count == cost.Length;
					bool canOk = items.All(i => researchable.Contains(i) && !i.settings_recipe.IsBlueprint);
					Check(ref ok, linesOk && canOk && !UpgradeTable.CanLearn(u),
						u.Display + ": " + items.Count + " of " + cost.Length + " cost lines researchable (" + string.Join(", ", items.Select(i => i.UniqueName).ToArray()) + "), not learnable yet"
						+ (linesOk ? "" : " - A COST LINE HAS NO ITEM TO RESEARCH") + (canOk ? "" : " - AN ITEM CAN'T BE RESEARCHED"));
				}
				made = player.BlockCreator.CreateBlockCheat(table, new Vector3(0f, 30f, 0f), Vector3.zero, DPS.Default, 0);
				yield return new WaitForSeconds(0.5f);
				ResearchTable rt = UtTableOf(made);
				if (rt == null || rt.slot == null || !UtOpen(inv, rt)) { Check(ref ok, false, "an upgrade table placed and opened"); yield break; }
				yield return null;
				// Research every item once, as the Research button does
				int researched = 0;
				foreach (Item_Base i in researchable.ToList())
				{
					rt.slot.SetItem(new ItemInstance(i, 1, i.MaxUses));
					bool did = UpgradeTable.AskResearch(rt.slot);
					if (!rt.slot.IsEmpty) rt.slot.RemoveItem(rt.slot.itemInstance.Amount);
					if (did && UpgradeTable.IsResearched(i)) researched++;
					else Check(ref ok, false, i.UniqueName + " could not be researched at the upgrade table");
				}
				Check(ref ok, researched == researchable.Count, researched + " of " + researchable.Count + " items researched");
				var notLearnable = teaches.Where(u => !UpgradeTable.CanLearn(u)).Select(u => u.Display).ToList();
				Check(ref ok, notLearnable.Count == 0, "all items researched: every upgrade learnable" + (notLearnable.Count > 0 ? " - not: " + string.Join(", ", notLearnable.ToArray()) : ""));
				UpgradeTableMenu.Show(true);
				foreach (ExtraUpgrades.Upgrade u in teaches)
				{
					ResearchMenuItem entry = inv.GetMenuItems().FirstOrDefault(m => UpgradeTableMenu.UpgradeOf(m) == u);
					if (entry != null) entry.LearnButton(); else UpgradeTable.AskLearn(u);
				}
				yield return null; yield return null; yield return null;
				var notLearned = teaches.Where(u => !UpgradeTable.IsLearned(u)).Select(u => u.Display).ToList();
				Check(ref ok, notLearned.Count == 0, "learned with the Learn button: all " + teaches.Count + (notLearned.Count > 0 ? " - not: " + string.Join(", ", notLearned.ToArray()) : ""));
				var notCraftable = teaches.Where(u => !ExtraUpgrades.Craftable(u) || !u.Item.settings_recipe.Learned).Select(u => u.Display).ToList();
				Check(ref ok, notCraftable.Count == 0, "learned: all craftable" + (notCraftable.Count > 0 ? " - not: " + string.Join(", ", notCraftable.ToArray()) : ""));
			}
			finally
			{
				UpgradeTable.SetState(stateBefore);
				WorldOptions.Set(optionsBefore);
				UpgradeTableMenu.Show(false);
				if (made != null) BlockCreator.RemoveBlockNetwork(made, null, true);
			}
			if (ok) Log("PASS: upgrade table all"); else Fail("upgrade table all");
		}

		#endregion

		#region RT11: bad world file lines (called by CIBadFiles)

		/// <summary>"@upgraderesearch=" / "@upgradeslearned=" lines with nonsense: read without errors, unknown left out, the rest kept.</summary>
		static void UtBadLines(ref bool ok, List<string> errors)
		{
			ExtraUpgrades.Upgrade up = ExtraUpgrades.Registered.FirstOrDefault(u => !u.Table);
			Item_Base cost = up != null ? UpgradeTable.ResearchItems(up).FirstOrDefault() : null;
			// (an item the table never researches and no upgrade: Plank once was one, then the table took all raw items)
			HashSet<Item_Base> researchable = UpgradeTable.Researchable();
			Item_Base plank = ItemManager.GetAllItems().FirstOrDefault(i => i != null && !researchable.Contains(i) && !ExtraUpgrades.Registered.Any(u => u.Index == i.UniqueIndex));
			if (up == null || cost == null || plank == null) { Log("upgrade table lines: no registered upgrade here, skipped"); return; }
			string saved = UpgradeTable.State();
			int ci = cost.UniqueIndex, ui = up.Index, pi = plank.UniqueIndex == cost.UniqueIndex ? 1 : plank.UniqueIndex;
			try
			{
				errors.Clear();
				string nonsense = "{0},99999999,abc,,{0},1.5,{1},-3, ,{0}";
				bool r1 = UpgradeTable.ReadLine("upgradeslearned", string.Format(nonsense, ui, pi));
				bool r2 = UpgradeTable.ReadLine("upgraderesearch", string.Format(nonsense, ci, pi));
				Check(ref ok, errors.Count == 0 && r1 && r2, "upgrade table lines with nonsense: read without errors" + (errors.Count > 0 ? " - " + errors[0] : ""));
				var learned = UpgradeTable.Learned.ToList(); var res = UpgradeTable.Researched.ToList();
				Check(ref ok, learned.Count == 1 && learned[0] == ui, "@upgradeslearned= with unknown, letters, doubles, a non-upgrade index: only the upgrade kept (" + string.Join(",", learned.Select(i => i.ToString()).ToArray()) + ")");
				Check(ref ok, res.Count == 1 && res[0] == ci, "@upgraderesearch= with unknown, letters, doubles, a non-upgrade index: only the researchable item kept (" + string.Join(",", res.Select(i => i.ToString()).ToArray()) + ")");
				var lines = UpgradeTable.WriteLines().ToList();
				Check(ref ok, lines.Contains("@upgradeslearned=" + ui) && lines.Contains("@upgraderesearch=" + ci), "written back clean (" + string.Join(" ", lines.ToArray()) + ")");
				UpgradeTable.ReadLine("upgradeslearned", "");
				UpgradeTable.ReadLine("upgraderesearch", null);
				Check(ref ok, errors.Count == 0 && UpgradeTable.State() == "|", "empty and missing values: read, both pools empty");
				Check(ref ok, !UpgradeTable.ReadLine("upgradeother", "1,2"), "an unknown key is not taken");
			}
			catch (Exception e) { Check(ref ok, false, "upgrade table lines threw " + e.GetType().Name + ": " + e.Message); }
			finally { UpgradeTable.SetState(saved); }
		}

		#endregion
	}
}
