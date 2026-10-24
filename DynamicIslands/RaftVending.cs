using System;
using HarmonyLib;
using UnityEngine;
using DynamicIslands.Editor;

namespace DynamicIslands
{
	/// <summary>
	/// Raft's own vending machine (Tangaroa's) placed on a custom island (SL7b): the "VendingMachine" object of the catalog
	/// keeps Raft's scripts (VendingMachine, its buttons, the hatch, the animator), so players pay Raft's price (Tangaroa
	/// tokens) at a button and take the goods from the hatch, as on Tangaroa. The mod does what Raft's island did for it:
	/// <list type="bullet">
	/// <item>a network number the same on every machine (from the island's id, like its stores), so a client's press reaches
	/// the host and the host's answer every player - Raft's own messages do the rest;</item>
	/// <item>the package waiting in the hatch (Raft saves it with its landmark): kept in the island's state, saved with the
	/// world and sent to players who join, and put back when the island is set up again.</item>
	/// </list>
	/// A vending machine has no stock (Raft's has none either): its goods and prices are Raft's.
	/// </summary>
	public class RaftVending : MonoBehaviour
	{
		/// <summary>The catalog's name of the working machine (Raft's Tangaroa vending machine with its scripts).</summary>
		public const string ObjectName = "VendingMachine";
		/// <summary>Island state: PackageKeyBase + (object index &amp; 0xFFFF) = the paid button whose package waits in the hatch, + 1 (0 = none).</summary>
		public const int PackageKeyBase = 0x3000000;
		/// <summary>Network numbers: the island's base (IslandSpawner.RegisterNetworkIds) + this + the n-th machine.</summary>
		public const uint NetOffset = 0xE000u;

		/// <summary>The object's place in the island file (its state key).</summary>
		public int Index;
		public VendingMachine Machine;
		bool registered;
		IslandWorldState.Entry entry;

		public static bool Is(string name) { return name == ObjectName; }

		/// <summary>A vending machine on an island in a world (IslandSpawner): numbered once the island has its id (OnIslandReady).</summary>
		public static RaftVending Attach(GameObject go, int index)
		{
			VendingMachine m = go.GetComponentInChildren<VendingMachine>(true);
			if (m == null) { Debug.LogWarning("[CUSTOM ISLANDS] [vending] " + go.name + " has no VendingMachine script: it stays decoration"); return null; }
			RaftVending v = go.AddComponent<RaftVending>();
			v.Index = index;
			v.Machine = m;
			return v;
		}

		/// <summary>Every machine, when an island got its world entry: its vending machines numbered, registered, the hatch as saved.</summary>
		public static void OnIslandReady(GameObject root, IslandWorldState.Entry e)
		{
			int n = 0;
			foreach (RaftVending v in root.GetComponentsInChildren<RaftVending>(true))
				try { v.Ready(e, n++); }
				catch (Exception x) { Debug.LogError("[CUSTOM ISLANDS] [vending] Setting up a vending machine on '" + e.Name + "': " + x); }
		}

		void Ready(IslandWorldState.Entry e, int n)
		{
			entry = e;
			if (registered) NetworkIDManager.RemoveNetworkID(Machine);
			Machine.ObjectIndex = 0x40000000u | ((uint)(e.Id & 0x3FFF) << 16) | (NetOffset + (uint)(n & 0xFFF));
			NetworkIDManager.AddNetworkID(Machine);
			registered = true;
			int waiting = Waiting(e);
			if (waiting >= 0 && Machine.LatestPurchaseButtonIndex != waiting)
				Machine.RestoreVendingMachine(new RGD_LandmarkItem_VendingMachine { lastPurchaseButtonIndex = waiting });
			Debug.Log("[CUSTOM ISLANDS] [vending] '" + e.Name + "': vending machine " + (n + 1) + " ready, network " + Machine.ObjectIndex.ToString("X8") + (waiting >= 0 ? ", a package in the hatch" : ""));
		}

		public int Key { get { return PackageKeyBase + (Index & 0xFFFF); } }

		/// <summary>The button whose package waits in the hatch as the island's state has it (-1 = none).</summary>
		public int Waiting(IslandWorldState.Entry e)
		{
			ObjectState s;
			return e != null && e.State.TryGetValue(Key, out s) && s.Yield > 0 ? s.Yield - 1 : -1;
		}

		/// <summary>Every machine, after Raft's machine handled a press (paid, or the hatch emptied): the hatch into the island's state.</summary>
		internal void Note()
		{
			IslandWorldState.Entry e = entry ?? ContentState.EntryOf(transform);
			if (e == null) return;
			int now = Machine.LatestPurchaseButtonIndex;
			if (Waiting(e) == now) return;
			e.State[Key] = new ObjectState { Active = true, Yield = now + 1, Day = ContentState.Today };
			if (Raft_Network.IsHost) IslandNetwork.SendVendingHatch(e.Id, Key, now + 1, ContentState.Today);
			Debug.Log("[CUSTOM ISLANDS] [vending] '" + e.Name + "': " + (now >= 0 ? "paid at button " + now + ", the package in the hatch" : "the hatch emptied"));
		}

		/// <summary>A player, from the host (IslandNetMessage.VendingHatch): the hatch into the island's state and, if the island is
		/// set up here and Raft's own message missed it (it came before the island was), into its machine (ShowLater).</summary>
		internal static void OnHatchMessage(int islandId, int key, int value, int day)
		{
			IslandWorldState.Entry e = null;
			foreach (IslandWorldState.Entry x in IslandWorldState.Islands) if (x.Id == islandId) { e = x; break; }
			if (e == null || key < PackageKeyBase || key > PackageKeyBase + 0xFFFF) return;
			e.State[key] = new ObjectState { Active = true, Yield = Math.Max(0, value), Day = day };
			if (e.Root == null) return;
			foreach (RaftVending v in e.Root.GetComponentsInChildren<RaftVending>(true))
				if (v.Key == key && v.Machine != null) v.StartCoroutine(v.ShowLater(e));
		}

		/// <summary>Raft's own message for the press usually comes too (it gives the goods to the player who emptied the hatch):
		/// the machine is only set to the host's hatch if, a moment later, it still differs.</summary>
		System.Collections.IEnumerator ShowLater(IslandWorldState.Entry e)
		{
			yield return new WaitForSeconds(3f);
			if (Machine == null) yield break;
			try { Show(Waiting(e)); }
			catch (Exception x) { Debug.LogError("[CUSTOM ISLANDS] [vending] The host's hatch on '" + e.Name + "': " + x); }
		}

		/// <summary>The machine shows this package in the hatch (-1 = none), if it doesn't already.</summary>
		void Show(int waiting)
		{
			if (Machine.LatestPurchaseButtonIndex == waiting) return;
			if (waiting >= 0) Machine.RestoreVendingMachine(new RGD_LandmarkItem_VendingMachine { lastPurchaseButtonIndex = waiting });
			else
			{
				Traverse.Create(Machine).Field("latestPurchase").SetValue(null);
				if (Machine.hatchButton != null) Machine.hatchButton.gameObject.SetActive(false);
			}
			Debug.Log("[CUSTOM ISLANDS] [vending] '" + (entry != null ? entry.Name : name) + "': the host's hatch " + (waiting >= 0 ? "(button " + waiting + ")" : "(empty)") + " put in this machine");
		}

		void OnDestroy()
		{
			if (!registered || Machine == null) return;
			try { NetworkIDManager.RemoveNetworkID(Machine); } catch { }
		}
	}

	[HarmonyPatch(typeof(VendingMachine), "Deserialize")]
	static class RaftVendingPressPatch
	{
		static void Postfix(VendingMachine __instance)
		{
			if (__instance == null) return;
			RaftVending v = __instance.GetComponentInParent<RaftVending>();
			if (v != null) v.Note();
		}
	}
}
