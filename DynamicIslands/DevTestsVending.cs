using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using DynamicIslands.Editor;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIRaftVending", docs: "Dev, in game (host): SL7b - Raft's vending machine on a custom island: it keeps Raft's scripts (network number, buttons, goods and prices), paying at a button takes the price and puts the package in the hatch, the hatch gives the goods, the waiting package is in the island's state and comes back when the island is set up again")]
		public static void RaftVendingCommand(string[] args) { StartTest(RaftVendingRoutine()); }

		static IEnumerator RaftVendingRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "civending";
			IslandWorldState.Entry e = null;
			yield return SpawnVendingIsland(name, 8993, raft.Value, 450f, x => e = x);
			if (e == null) yield break;
			RaftVending v = e.Root.GetComponentInChildren<RaftVending>(true);
			VendingMachine m = v.Machine;
			uint want = 0x40000000u | ((uint)(e.Id & 0x3FFF) << 16) | RaftVending.NetOffset;
			Check(ref ok, m.ObjectIndex == want, "network number " + m.ObjectIndex.ToString("X8") + " (wanted " + want.ToString("X8") + ")");
			Check(ref ok, e.Root.GetComponentInChildren<LandmarkItem>(true) == null, "Raft's landmark bookkeeping is gone");
			VendingMachine_Element[] goods = m.GetComponentsInChildren<VendingMachine_Element>(true);
			Check(ref ok, goods.Length > 0 && m.hatchButton != null, goods.Length + " buttons, hatch " + (m.hatchButton != null));
			Log("  Raft's goods: " + string.Join(", ", goods.Select(VendText).ToArray()));
			VendingMachine_Element el = goods.FirstOrDefault(g => g.interactRequirement != null && g.interactRequirement.questItem != null && g.itemExtract != null && !g.itemExtract.IsQuestItem && g.itemExtract.inventoryItem != null);
			if (el == null) { Fail("no button paid in quest items that gives an inventory item"); IslandWorldState.Remove(name); yield break; }
			QuestItemType pay = el.interactRequirement.questItem.questItemType;
			int price = el.interactRequirement.amount;
			string item = el.itemExtract.inventoryItem.UniqueName;
			QuestItemManager.AddQuestItem(pay, price, false);
			int tokens = VendTokens(pay), had = player.Inventory.GetItemCount(item);
			// (a press as Raft's network hands it to the host: the price taken, the package in the hatch, the island's state)
			Press(m, el.networked_button.buttonIndex, player);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, VendTokens(pay) == tokens - price, "paid " + pay + " " + tokens + " -> " + VendTokens(pay) + " (price " + price + ")");
			Check(ref ok, m.LatestPurchaseButtonIndex == el.networked_button.buttonIndex && m.hatchButton.gameObject.activeSelf, "the package waits in the hatch (button " + m.LatestPurchaseButtonIndex + ", hatch " + m.hatchButton.gameObject.activeSelf + ")");
			Check(ref ok, v.Waiting(e) == el.networked_button.buttonIndex, "the island's state has the package (key " + v.Key.ToString("X") + ": " + v.Waiting(e) + ")");
			// (a second press while the hatch is full does nothing, as on Tangaroa)
			QuestItemManager.AddQuestItem(pay, price, false);
			int before = VendTokens(pay);
			Press(m, el.networked_button.buttonIndex, player);
			yield return new WaitForSeconds(0.3f);
			Check(ref ok, VendTokens(pay) == before, "a second press with the hatch full takes nothing (" + before + " -> " + VendTokens(pay) + ")");
			QuestItemManager.AddQuestItem(pay, -price, false);
			// (the island set up again, as after loading the world: the package still waits)
			Traverse.Create(m).Field("latestPurchase").SetValue(null);
			m.hatchButton.gameObject.SetActive(false);
			RaftVending.OnIslandReady(e.Root, e);
			Check(ref ok, m.LatestPurchaseButtonIndex == el.networked_button.buttonIndex && m.hatchButton.gameObject.activeSelf, "set up again: the package is back in the hatch (button " + m.LatestPurchaseButtonIndex + ")");
			Dictionary<int, ObjectState> back = IslandObjectState.Decode(IslandObjectState.Encode(e.State));
			ObjectState st;
			Check(ref ok, back.TryGetValue(v.Key, out st) && st.Yield == el.networked_button.buttonIndex + 1, "the package survives the world's save (Encode/Decode)");
			// (the hatch: the goods to the player, the hatch shut, the state cleared)
			Press(m, m.hatchButton.buttonIndex, player);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, player.Inventory.GetItemCount(item) >= had + el.itemExtract.amount, "the hatch gave " + item + " (" + had + " -> " + player.Inventory.GetItemCount(item) + ")");
			Check(ref ok, m.LatestPurchaseButtonIndex == -1 && !m.hatchButton.gameObject.activeSelf && v.Waiting(e) == -1, "the hatch is empty and shut, the state cleared");
			player.Inventory.RemoveItem(item, el.itemExtract.amount);
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: raft vending"); else Fail("raft vending");
		}

		[ConsoleCommand(name: "CIVendIsland", docs: "Dev, two-player vending test (host): spawns 'cimpvend', an island with Raft's vending machine")]
		public static void VendIslandCommand(string[] args) { StartTest(VendIslandRoutine()); }

		static IEnumerator VendIslandRoutine()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandWorldState.Entry e = null;
			yield return SpawnVendingIsland("cimpvend", 8994, raft.Value, 390f, x => e = x);
			if (e != null) Log("PASS: cimpvend spawned (island " + e.Id + ", vending machine " + e.Root.GetComponentInChildren<RaftVending>(true).Machine.ObjectIndex.ToString("X8") + ")");
		}

		[ConsoleCommand(name: "CIVendBuy", docs: "Dev, vending test (either player): this machine's player presses a button of the island's vending machine as a player would (through Raft's network; the host gets the price first), or the hatch: CIVendBuy <island> pay|hatch")]
		public static void VendBuyCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			if (args.Length < 2) { Fail("CIVendBuy <island> pay|hatch"); return; }
			StartTest(VendBuyRoutine(e, args[1] == "hatch"));
		}

		static IEnumerator VendBuyRoutine(IslandWorldState.Entry e, bool hatch)
		{
			RaftVending v = e.Root.GetComponentInChildren<RaftVending>(true);
			Network_Player player = RAPI.GetLocalPlayer();
			if (v == null || player == null) { Fail("no vending machine on '" + e.HostName + "' or no player"); yield break; }
			VendingMachine m = v.Machine;
			VendingMachine_Element el = VendButton(m);
			if (el == null) { Fail("no button paid in quest items that gives an inventory item"); yield break; }
			PutPlayerNear(m.transform, 0.3f);
			yield return new WaitForSeconds(1f);
			string item = el.itemExtract.inventoryItem.UniqueName;
			int had = player.Inventory.GetItemCount(item);
			// (the host gives the crew the price: Tangaroa's tokens are the crew's, kept by the host)
			if (!hatch && Raft_Network.IsHost) { QuestItemManager.AddQuestItem(el.interactRequirement.questItem.questItemType, el.interactRequirement.amount, false); ComponentManager<QuestItemManager>.Value.SendModifiedQuestItemsMessage(); yield return new WaitForSeconds(1f); }
			InteractableButton_Networked b = hatch ? m.hatchButton : el.networked_button;
			if (hatch && !b.gameObject.activeSelf) { Fail("the hatch is shut on this machine"); yield break; }
			try { Traverse.Create(b).Method("ButtonPressed", true).GetValue(); } catch (Exception x) { Fail("pressing threw: " + x.Message); yield break; }
			yield return new WaitForSeconds(2f);
			string who = Raft_Network.IsHost ? "host" : "client";
			if (hatch)
			{
				int now = player.Inventory.GetItemCount(item);
				if (now >= had + el.itemExtract.amount && m.LatestPurchaseButtonIndex == -1) Log("PASS: hatch emptied by the " + who + ": " + item + " " + had + " -> " + now);
				else Fail("hatch (" + who + "): " + item + " " + had + " -> " + now + ", package " + m.LatestPurchaseButtonIndex);
			}
			else if (m.LatestPurchaseButtonIndex == el.networked_button.buttonIndex) Log("PASS: paid by the " + who + ": the package waits in the hatch");
			else Fail("paying (" + who + "): no package in the hatch (" + m.LatestPurchaseButtonIndex + ")");
		}

		[ConsoleCommand(name: "CIVendCheck", docs: "Dev, vending test (either player): the island's vending machine as this machine has it: CIVendCheck <island> full|empty - the package in the hatch (and the island's state) or not")]
		public static void VendCheckCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			if (args.Length < 2) { Fail("CIVendCheck <island> full|empty"); return; }
			RaftVending v = e.Root.GetComponentInChildren<RaftVending>(true);
			if (v == null) { Fail("no vending machine on '" + e.HostName + "'"); return; }
			bool full = args[1] == "full", isFull = v.Machine.LatestPurchaseButtonIndex >= 0;
			string what = "hatch " + (isFull ? "full" : "empty") + ", shown " + v.Machine.hatchButton.gameObject.activeSelf + ", state " + v.Waiting(e) + " (" + (Raft_Network.IsHost ? "host" : "client") + ")";
			if (isFull == full && v.Machine.hatchButton.gameObject.activeSelf == full && (v.Waiting(e) >= 0) == full) Log("PASS: vending check: " + what); else Fail("vending check: " + what + ", wanted " + args[1]);
		}

		static IEnumerator SpawnVendingIsland(string name, int seed, Vector3 raft, float distance, Action<IslandWorldState.Entry> done)
		{
			var s = new IslandGenSettings { Seed = seed, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			f.Objects.Add(new IslandObject { Name = RaftVending.ObjectName, Position = StoreSpot(f), Props = new Dictionary<string, string>() });
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft, CustomIslandSpawner.LandRadius(name), distance);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			RaftVending v = e.Root.GetComponentInChildren<RaftVending>(true);
			if (v == null || v.Machine == null) { Fail("the island's VendingMachine has no working machine (" + (e.Root.GetComponentInChildren<VendingMachine>(true) != null ? "VendingMachine script there" : "no VendingMachine script") + ")"); IslandWorldState.Remove(name); yield break; }
			done(e);
		}

		static VendingMachine_Element VendButton(VendingMachine m)
		{
			return m.GetComponentsInChildren<VendingMachine_Element>(true).FirstOrDefault(g => g.interactRequirement != null && g.interactRequirement.questItem != null && g.itemExtract != null && !g.itemExtract.IsQuestItem && g.itemExtract.inventoryItem != null);
		}

		static void Press(VendingMachine m, int button, Network_Player player)
		{
			var msg = new Message_InteractableButtonPress(player.Network.NetworkIDManager, m.ObjectIndex, button, player) { success = true };
			m.Deserialize(msg, player.steamID);
		}

		static int VendTokens(QuestItemType t)
		{
			QuestItem q = QuestItemManager.GetQuestItemFromType(t);
			return q != null ? q.itemCount : 0;
		}

		static string VendText(VendingMachine_Element g)
		{
			string pay = g.interactRequirement == null ? "free" : (g.interactRequirement.questItem != null ? g.interactRequirement.questItem.questItemType.ToString() : g.interactRequirement.inventoryItem != null ? g.interactRequirement.inventoryItem.UniqueName : "?") + "*" + g.interactRequirement.amount;
			string get = g.itemExtract == null ? "-" : (g.itemExtract.IsQuestItem ? g.itemExtract.questItem.questItemType.ToString() : g.itemExtract.inventoryItem != null ? g.itemExtract.inventoryItem.UniqueName : "?") + "*" + g.itemExtract.amount;
			return get + " for " + pay;
		}
	}
}
