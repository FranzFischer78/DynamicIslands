using System;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Raft's own trading post placed on a custom island (SL7): the "TradingPost" object of the catalog keeps Raft's scripts
	/// (TradingPost, its register's animator, the radio), so players open Raft's store window and buy Raft's goods at Raft's
	/// prices with trash cubes and trade coins, and sell their trophy fish. The mod does what Raft's island did for it:
	/// <list type="bullet">
	/// <item>a network number the same on every machine (from the island's id, like its pickups), so a client's purchase
	/// reaches the host and the host's answer every player - Raft's own messages do the rest;</item>
	/// <item>the stock: Raft fills it from its goods list (ResetItemsForSale); what was sold is kept in the island's state
	/// (one key per store and item, the count sold), saved with the world and sent to players who join;</item>
	/// <item>Raft's story-island blueprints (raft_blueprints.txt) are on sale too, one each, for players who missed one
	/// (SL8: a "blueprint" quest step can always be done). The window lists only those this player hasn't found.</item>
	/// </list>
	/// Raft's reputation tiers are Raft's own (one total for all its trading posts), so the goods of tiers 2 and 3 show as
	/// Raft's posts would.
	/// </summary>
	public class RaftStore : MonoBehaviour
	{
		/// <summary>The catalog's name of the whole trading post (its hut, register, lantern and radio).</summary>
		public const string ObjectName = "TradingPost";
		/// <summary>Island state: SoldKeyBase + (object index &amp; 0xFF) * 0x10000 + (item index &amp; 0xFFFF) = how many sold.</summary>
		public const int SoldKeyBase = 0x2000000;
		/// <summary>Network numbers: the island's base (IslandSpawner.RegisterNetworkIds) + this + the n-th store.</summary>
		public const uint NetOffset = 0xF000u;
		/// <summary>What a missed story blueprint costs here.</summary>
		public const string BlueprintPrice = "Trashcube*6";

		/// <summary>The object's place in the island file (its state keys).</summary>
		public int Index;
		public TradingPost Post;
		bool registered;
		IslandWorldState.Entry entry;

		public static bool Is(string name) { return name == ObjectName; }

		/// <summary>The catalog's copy (inactive): Raft's landmark bookkeeping and its music zone go; the store's scripts stay.</summary>
		internal static void Prepare(GameObject proto)
		{
			foreach (LandmarkItem li in proto.GetComponentsInChildren<LandmarkItem>(true)) UnityEngine.Object.DestroyImmediate(li);
			foreach (Subregion s in proto.GetComponentsInChildren<Subregion>(true)) UnityEngine.Object.DestroyImmediate(s.gameObject);
			foreach (AreaZone z in proto.GetComponentsInChildren<AreaZone>(true)) if (z != null) UnityEngine.Object.DestroyImmediate(z);
		}

		/// <summary>A store on an island in a world (IslandSpawner): numbered and stocked once the island has its id (OnIslandReady).</summary>
		public static RaftStore Attach(GameObject go, int index)
		{
			TradingPost post = go.GetComponentInChildren<TradingPost>(true);
			if (post == null) { Debug.LogWarning("[CUSTOM ISLANDS] [store] " + go.name + " has no TradingPost script: it stays decoration"); return null; }
			RaftStore s = go.AddComponent<RaftStore>();
			s.Index = index;
			s.Post = post;
			return s;
		}

		/// <summary>Every machine, when an island got its world entry: its stores numbered, registered and stocked.</summary>
		public static void OnIslandReady(GameObject root, IslandWorldState.Entry e)
		{
			int n = 0;
			foreach (RaftStore s in root.GetComponentsInChildren<RaftStore>(true))
				try { s.Ready(e, n++); }
				catch (Exception x) { Debug.LogError("[CUSTOM ISLANDS] [store] Setting up a store on '" + e.Name + "': " + x); }
		}

		void Ready(IslandWorldState.Entry e, int n)
		{
			entry = e;
			if (registered) NetworkIDManager.RemoveNetworkID(Post);
			Post.ObjectIndex = 0x40000000u | ((uint)(e.Id & 0x3FFF) << 16) | (NetOffset + (uint)(n & 0xFFF));
			NetworkIDManager.AddNetworkID(Post);
			registered = true;
			Post.ResetItemsForSale();
			AddBlueprints(Post.buyableItems);
			int sold = 0;
			foreach (SO_TradingPost_Buyable.Instance i in Post.buyableItems)
			{
				int s = Sold(e, i);
				if (s > 0) { i.stock = Mathf.Max(0, i.stock - s); sold += s; }
			}
			Debug.Log("[CUSTOM ISLANDS] [store] '" + e.Name + "': store " + (n + 1) + " ready, network " + Post.ObjectIndex.ToString("X8") + ", " + Post.buyableItems.Count + " goods" + (sold > 0 ? ", " + sold + " sold before" : ""));
		}

		/// <summary>The story blueprints added to the goods (the same list on every machine: purchases name the item).</summary>
		static void AddBlueprints(List<SO_TradingPost_Buyable.Instance> goods)
		{
			Item_Base price = ItemManager.GetItemByName(BlueprintPrice.Split('*')[0]);
			int amount = int.Parse(BlueprintPrice.Split('*')[1]);
			if (price == null) return;
			foreach (string bp in BlueprintSteps.StoryBlueprints())
			{
				Item_Base item = ItemManager.GetItemByName(bp);
				if (item == null || goods.Any(g => g.reward != null && g.reward.item == item)) continue;
				goods.Add(new SO_TradingPost_Buyable.Instance { tier = TradingPost.Tier.Tier1, stock = 1, reward = new Cost(item, 1), cost = new[] { new Cost(price, amount) } });
			}
		}

		public static bool IsExtraBlueprint(SO_TradingPost_Buyable.Instance i)
		{
			return i != null && i.reward != null && i.reward.item != null && i.reward.item.UniqueName.StartsWith("Blueprint_") && i.stock <= 1 &&
				BlueprintSteps.StoryBlueprints().Contains(i.reward.item.UniqueName, StringComparer.OrdinalIgnoreCase);
		}

		int KeyOf(SO_TradingPost_Buyable.Instance i) { return SoldKeyBase + ((Index & 0xFF) << 16) + (i.reward.item.UniqueIndex & 0xFFFF); }

		int Sold(IslandWorldState.Entry e, SO_TradingPost_Buyable.Instance i)
		{
			ObjectState s;
			return e != null && i.reward != null && i.reward.item != null && e.State.TryGetValue(KeyOf(i), out s) ? s.Yield : 0;
		}

		/// <summary>Every machine runs Raft's purchase (the host's answer reaches all): the count sold goes into the island's state.</summary>
		internal void Bought(int itemIndex)
		{
			SO_TradingPost_Buyable.Instance i = Post.buyableItems.FirstOrDefault(x => x != null && x.reward != null && x.reward.item != null && x.reward.item.UniqueIndex == itemIndex);
			IslandWorldState.Entry e = entry ?? ContentState.EntryOf(transform);
			if (i == null || e == null) return;
			int key = KeyOf(i);
			e.State[key] = new ObjectState { Active = true, Yield = Sold(e, i) + 1, Day = ContentState.Today };
			Debug.Log("[CUSTOM ISLANDS] [store] Bought " + i.reward.item.UniqueName + " on '" + e.Name + "': " + i.stock + " left");
		}

		/// <summary>The goods this player is shown: the story blueprints they have already are left out.</summary>
		internal List<SO_TradingPost_Buyable.Instance> Shown()
		{
			return Post.buyableItems.Where(i => !IsExtraBlueprint(i) || !BlueprintSteps.Has(i.reward.item.UniqueName)).ToList();
		}

		void OnDestroy()
		{
			if (!registered || Post == null) return;
			try { NetworkIDManager.RemoveNetworkID(Post); } catch { }
		}
	}

	[HarmonyPatch(typeof(TradingPost), "PurchaseItem")]
	static class RaftStorePurchasePatch
	{
		static void Postfix(TradingPost __instance, int uniqueItemIndex, bool __result)
		{
			if (!__result || __instance == null) return;
			RaftStore s = __instance.GetComponentInParent<RaftStore>();
			if (s != null) s.Bought(uniqueItemIndex);
		}
	}

	/// <summary>The store window lists what this player may want: Raft's goods, and only the story blueprints they lack.</summary>
	[HarmonyPatch(typeof(TradingPostUI), "Open")]
	static class RaftStoreOpenPatch
	{
		static void Prefix(TradingPost tradingPost, out List<SO_TradingPost_Buyable.Instance> __state)
		{
			__state = null;
			RaftStore s = tradingPost != null ? tradingPost.GetComponentInParent<RaftStore>() : null;
			if (s == null) return;
			__state = tradingPost.buyableItems;
			tradingPost.buyableItems = s.Shown();
		}

		static void Postfix(TradingPost tradingPost, List<SO_TradingPost_Buyable.Instance> __state)
		{
			if (__state != null && tradingPost != null) tradingPost.buyableItems = __state;
		}
	}
}
