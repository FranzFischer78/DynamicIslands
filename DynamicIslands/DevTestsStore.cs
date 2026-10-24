using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>SL7: what Raft's trading posts are made of (which scenes hold one, the objects around the TradingPost
		/// script, its goods and prices) - to build the store the mod places on custom islands.</summary>
		[ConsoleCommand(name: "CIDumpTradingPost", docs: "Dev, main menu or editor: loads Raft's island scenes (switched off) whose name has the word (default Big) and logs every TradingPost: its path, the objects around it (meshes, bounds, scripts), and the goods Raft's trading posts sell and buy (SO_TradingPost)")]
		public static void DumpTradingPostCommand(string[] args) { StartTest(DumpTradingPostRoutine(args != null && args.Length > 0 ? args[0] : "Big")); }

		static IEnumerator DumpTradingPostRoutine(string word)
		{
			var buyables = Resources.LoadAll<SO_TradingPost_Buyable>("SO_TradingPost").OrderBy(b => b.sortingOrder).ToList();
			foreach (SO_TradingPost_Buyable b in buyables)
				Log("  buy " + b.name + ": tier " + b.tier + ", stock " + b.startStock + ", gives " + CostText(b.reward) + " for " + string.Join(" + ", (b.cost ?? new Cost[0]).Select(CostText).ToArray()));
			foreach (SO_TradingPost_Sellable s in Resources.LoadAll<SO_TradingPost_Sellable>("SO_TradingPost").OrderBy(b => b.sortingOrder))
				Log("  sell " + (s.sellableItem != null ? s.sellableItem.UniqueName : "?") + ": gives " + CostText(s.reward) + ", reputation " + s.reputationReward);
			var scenes = PlaceableCatalog.LandmarkSceneNames().Where(s => s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			Log("Scenes: " + string.Join(", ", scenes.ToArray()));
			int found = 0;
			foreach (string scene in scenes)
				yield return PlaceableCatalog.VisitScene(scene, s => DumpPosts(s, scene, n => found += n));
			if (found > 0) Log("PASS: " + found + " trading posts"); else Fail("no trading posts in " + word + " scenes");
		}

		[ConsoleCommand(name: "CIBlueprintStep", docs: "Dev, in game (host): SL8 - the 'blueprint' quest step: waits while the player lacks the blueprint, done once it is carried (or learned); one picked up before its step counts when the step comes; its text; the plan checker's problem for an unknown blueprint, its warning when nothing gives it, none when a chest on the island gives it")]
		public static void BlueprintStepCommand(string[] args) { StartTest(BlueprintStepRoutine()); }

		static IEnumerator BlueprintStepRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			List<string> free = BlueprintSteps.All().Where(b => !BlueprintSteps.Has(b) && !b.Contains("Reciever") && BlueprintSteps.Teaches(b).Count > 0).ToList();
			if (free.Count < 2) { Fail("fewer than two blueprints not found yet in this world (" + free.Count + "): use a fresh world"); yield break; }
			string bp = free[0], early = free[1];
			Log("  blueprints: " + bp + " (step after a reach), " + early + " (carried before its step)");
			const string name = "ciblueprintstep";
			QuestRewards.ForgetIsland(name);
			var s = new IslandGenSettings { Seed = 8990, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			new IslandQuest { Title = "Plans", Steps = { new IslandQuest.Step { Type = "reach", Target = "gate" }, new IslandQuest.Step { Type = BlueprintSteps.Type, Target = bp } } }.To(f.Props, 0);
			new IslandQuest { Title = "Early plans", Steps = { new IslandQuest.Step { Type = "reach", Target = "den" }, new IslandQuest.Step { Type = BlueprintSteps.Type, Target = early } } }.To(f.Props, 1);
			IslandQuest read = IslandQuest.From(f.Props, 0);
			string text = read.Steps.Count == 2 ? read.Steps[1].Describe() : "";
			Check(ref ok, read.Steps.Count == 2 && read.Steps[1].Type == BlueprintSteps.Type && read.Steps[1].Target == bp && text.StartsWith("Find the ") && text.EndsWith(" blueprint"), "the step is kept in the island's settings and reads \"" + text + "\"");
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); yield break; }
			player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
			yield return new WaitForSeconds(1.5f);
			QuestTracker.Event(e, "reach", "gate");
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.StepOf(e, 0) == 1, "without the blueprint the quest waits at its blueprint step (step " + (QuestTracker.StepOf(e, 0) + 1) + ")");
			player.Inventory.AddItem(bp, 1);
			player.Inventory.AddItem(early, 1);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.IsDone(e, 0), "the blueprint picked up: the step is done (step " + (QuestTracker.StepOf(e, 0) + 1) + ")");
			Check(ref ok, QuestTracker.StepOf(e, 1) == 0, "the other quest's blueprint step hasn't come yet: still at its first step");
			QuestTracker.Event(e, "reach", "den");
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.IsDone(e, 1), "a blueprint carried before its step: done when the step comes (step " + (QuestTracker.StepOf(e, 1) + 1) + ")");
			player.Inventory.RemoveItem(bp, 1);
			player.Inventory.RemoveItem(early, 1);
			// (the plan checker: an unknown blueprint is a problem, one nothing gives a warning or tip, one a chest gives fine)
			IslandFile g = IslandGenerator.CreateFile(s, "ciblueprintcheck");
			new IslandQuest { Steps = { new IslandQuest.Step { Type = BlueprintSteps.Type, Target = "Blueprint_NoSuchThing" } } }.To(g.Props, 0);
			Check(ref ok, PlanChecker.QuestFindings(g).Any(x => x.Level == PlanChecker.Level.Problem && x.Text.Contains("Blueprint_NoSuchThing")), "the checker: a blueprint Raft doesn't have is a problem");
			string lone = BlueprintSteps.All().FirstOrDefault(b => !ScrambledBlueprints.OnIslands.ContainsKey(b)) ?? bp;
			new IslandQuest { Steps = { new IslandQuest.Step { Type = BlueprintSteps.Type, Target = lone } } }.To(g.Props, 0);
			List<PlanChecker.Finding> nothing = PlanChecker.QuestFindings(g);
			Check(ref ok, nothing.Any(x => x.Level != PlanChecker.Level.Problem && x.Text.Contains(PlanChecker.Pretty(lone))), "the checker: " + lone + " given by nothing on the island is a warning or tip (" + string.Join(" | ", nothing.Select(x => x.Level + ": " + x.Text).ToArray()) + ")");
			g.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.zero, Props = new Dictionary<string, string> { { ObjectProps.LootItems, lone + "*1" }, { ObjectProps.NoteTitle, "Plans" } } });
			Check(ref ok, !PlanChecker.QuestFindings(g).Any(x => x.Text.Contains(PlanChecker.Pretty(lone))), "the checker: a chest on the island gives it - nothing to say");
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: blueprint step"); else Fail("blueprint step");
		}

		[ConsoleCommand(name: "CIRaftStore", docs: "Dev, in game (host): SL7 - Raft's trading post on a custom island: it keeps Raft's store (network number, Raft's goods and the story blueprints), a purchase gives the item, takes the price and lowers the stock, the count sold is in the island's state and comes back when the island is set up again, and a blueprint the player has is left out of the window")]
		public static void RaftStoreCommand(string[] args) { StartTest(RaftStoreRoutine()); }

		static IEnumerator RaftStoreRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "ciraftstore";
			var s = new IslandGenSettings { Seed = 8991, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			f.Objects.Add(new IslandObject { Name = RaftStore.ObjectName, Position = StoreSpot(f), Props = new Dictionary<string, string>() });
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			RaftStore store = e.Root.GetComponentInChildren<RaftStore>(true);
			if (store == null || store.Post == null) { Fail("the island's TradingPost has no working store (" + (e.Root.GetComponentInChildren<TradingPost>(true) != null ? "TradingPost script there" : "no TradingPost script") + ")"); IslandWorldState.Remove(name); yield break; }
			TradingPost post = store.Post;
			uint want = 0x40000000u | ((uint)(e.Id & 0x3FFF) << 16) | RaftStore.NetOffset;
			Check(ref ok, post.ObjectIndex == want, "network number " + post.ObjectIndex.ToString("X8") + " (wanted " + want.ToString("X8") + ")");
			Check(ref ok, e.Root.GetComponentInChildren<LandmarkItem>(true) == null, "Raft's landmark bookkeeping is gone (Raft doesn't save or reset it as one of its own posts)");
			int goods = post.buyableItems.Count, extra = post.buyableItems.Count(RaftStore.IsExtraBlueprint);
			Check(ref ok, goods > extra && extra > 0, goods + " goods, " + extra + " of them Raft's story blueprints: " + string.Join(", ", post.buyableItems.Where(RaftStore.IsExtraBlueprint).Select(i => i.reward.item.UniqueName).ToArray()));
			Log("  Raft's goods: " + string.Join(", ", post.buyableItems.Where(i => !RaftStore.IsExtraBlueprint(i)).Select(i => i.reward.item.UniqueName + "*" + i.reward.amount + " (" + i.tier + ", " + i.stock + " for " + string.Join("+", i.cost.Select(CostText).ToArray()) + ")").ToArray()));
			// (buy one of Raft's goods: the price paid, the item given, the stock lower, the count in the island's state)
			SO_TradingPost_Buyable.Instance buy = post.buyableItems.FirstOrDefault(i => !RaftStore.IsExtraBlueprint(i) && i.stock > 1 && i.tier == TradingPost.Tier.Tier1);
			if (buy == null) { Fail("no tier 1 goods with stock"); IslandWorldState.Remove(name); yield break; }
			string item = buy.reward.item.UniqueName;
			foreach (Cost c in buy.cost) player.Inventory.AddItem(c.item.UniqueName, c.amount);
			int had = player.Inventory.GetItemCount(item), stock = buy.stock;
			Dictionary<string, int> pay = buy.cost.ToDictionary(c => c.item.UniqueName, c => player.Inventory.GetItemCount(c.item.UniqueName));
			bool bought = false;
			try { bought = post.PurchaseItem(buy.reward.item.UniqueIndex, player); } catch (Exception x) { Log("  PurchaseItem threw (after the sale?): " + x.GetType().Name + ": " + x.Message); }
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, buy.stock == stock - 1, "bought " + item + ": stock " + stock + " -> " + buy.stock + " (PurchaseItem said " + bought + ")");
			Check(ref ok, player.Inventory.GetItemCount(item) >= had + buy.reward.amount, "the player got " + item + " (" + had + " -> " + player.Inventory.GetItemCount(item) + ")");
			Check(ref ok, buy.cost.All(c => player.Inventory.GetItemCount(c.item.UniqueName) == pay[c.item.UniqueName] - c.amount), "the price was paid: " + string.Join(", ", buy.cost.Select(c => c.item.UniqueName + " " + pay[c.item.UniqueName] + " -> " + player.Inventory.GetItemCount(c.item.UniqueName)).ToArray()));
			int key = RaftStore.SoldKeyBase + ((store.Index & 0xFF) << 16) + (buy.reward.item.UniqueIndex & 0xFFFF);
			ObjectState st;
			Check(ref ok, e.State.TryGetValue(key, out st) && st.Yield == 1, "the island's state counts 1 sold (key " + key.ToString("X") + ")");
			// (a story blueprint bought: shown no more to this player)
			SO_TradingPost_Buyable.Instance bp = post.buyableItems.Where(RaftStore.IsExtraBlueprint).FirstOrDefault(i => !BlueprintSteps.Has(i.reward.item.UniqueName));
			if (bp != null)
			{
				string bpName = bp.reward.item.UniqueName;
				bool shownBefore = store.Shown().Contains(bp);
				foreach (Cost c in bp.cost) player.Inventory.AddItem(c.item.UniqueName, c.amount);
				try { post.PurchaseItem(bp.reward.item.UniqueIndex, player); } catch (Exception x) { Log("  PurchaseItem threw: " + x.Message); }
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, shownBefore && BlueprintSteps.Has(bpName) && !store.Shown().Contains(bp) && bp.stock == 0, "a story blueprint bought (" + bpName + "): the player has it, the window leaves it out, none left");
				player.Inventory.RemoveItem(bpName, 1);
			}
			else Log("  (the player has every story blueprint: blueprint purchase skipped)");
			// (the island set up again, as after loading the world: Raft's stock less what was sold)
			RaftStore.OnIslandReady(e.Root, e);
			SO_TradingPost_Buyable.Instance again = post.buyableItems.FirstOrDefault(i => i.reward.item.UniqueName == item);
			Check(ref ok, again != null && again.stock == stock - 1, "set up again: " + item + " stock " + (again != null ? again.stock : -1) + " (Raft's " + stock + " less 1 sold)");
			Dictionary<int, ObjectState> back = IslandObjectState.Decode(IslandObjectState.Encode(e.State));
			Check(ref ok, back.TryGetValue(key, out st) && st.Yield == 1, "the count sold survives the world's save (Encode/Decode)");
			player.Inventory.RemoveItem(item, buy.reward.amount);
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: raft store"); else Fail("raft store");
		}

		[ConsoleCommand(name: "CIStoreCubes", docs: "Dev, in game (host): SL9 - store money at Raft's trading post on a custom island, through Raft's own store window: trophy fish sold give coins (trade tokens) and reputation, then goods bought with those coins and trash cubes (Raft's stores buy no trash cubes: they are part of the price)")]
		public static void StoreCubesCommand(string[] args) { StartTest(StoreCubesRoutine()); }

		static IEnumerator StoreCubesRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			TradingPostUI ui = ComponentManager<TradingPostUI>.Value;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost || ui == null) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "cistorecubes";
			var s = new IslandGenSettings { Seed = 8993, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			f.Objects.Add(new IslandObject { Name = RaftStore.ObjectName, Position = StoreSpot(f), Props = new Dictionary<string, string>() });
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			RaftStore store = e != null && e.Root != null ? e.Root.GetComponentInChildren<RaftStore>(true) : null;
			if (store == null || store.Post == null) { Fail(name + " did not spawn with a working store"); yield break; }
			PutPlayerNear(store.Post.transform, 0.3f);
			yield return new WaitForSeconds(1.5f);
			Exception thrown = null;
			try { Traverse.Create(store.Post).Method("OpenUI").GetValue(); } catch (Exception x) { thrown = x; }
			yield return new WaitForSeconds(0.5f);
			if (thrown != null) { Log("  opening threw (" + (thrown.InnerException ?? thrown).Message + "), opened through the window"); try { ui.Open(store.Post); } catch (Exception x) { Log("  ui.Open threw: " + x.Message); } }
			List<SO_TradingPost_Sellable> sellables = TradingPost.SellableItems ?? new List<SO_TradingPost_Sellable>();
			Log("  sellables: " + string.Join(", ", sellables.Where(x => x != null && x.sellableItem != null).Select(x => x.sellableItem.UniqueName + " -> " + CostText(x.reward) + " +" + x.reputationReward + " rep").ToArray()));
			// (Raft's trading post buys trophy fish only, for trade tokens: trash cubes aren't sold, they are part of the price)
			SO_TradingPost_Sellable fish = sellables.Where(x => x != null && x.sellableItem != null && x.reward != null && x.reward.item != null && x.reward.amount > 0)
				.OrderByDescending(x => x.reputationReward).FirstOrDefault();
			if (fish == null) { Fail("Raft's store buys nothing"); try { store.Post.CloseUI(); } catch { } IslandWorldState.Remove(name); yield break; }
			Check(ref ok, !sellables.Any(x => x != null && x.sellableItem != null && x.sellableItem.UniqueName == "Trashcube"), "Raft's store doesn't buy trash cubes (as on Raft's own trading posts): they are part of the price");
			string fishName = fish.sellableItem.UniqueName, coin = fish.reward.item.UniqueName, cubeName = "Trashcube";
			// (the cheapest goods paid in trash cubes and coins, of a tier the window shows now)
			TradingPost.Tier tier = ui.CurrentTier;
			SO_TradingPost_Buyable.Instance buy = store.Post.buyableItems.Where(i => i.reward != null && i.reward.item != null && i.stock > 0 && i.tier <= tier && i.cost != null && i.cost.Length > 0
					&& i.cost.All(c => c.item != null && (c.item.UniqueName == coin || c.item.UniqueName == cubeName)) && i.cost.Any(c => c.item.UniqueName == coin) && i.cost.Any(c => c.item.UniqueName == cubeName))
				.OrderBy(i => i.cost.Sum(c => c.amount)).FirstOrDefault();
			if (buy == null) { Fail("nothing at tier " + tier + " is priced in " + cubeName + " and " + coin); try { store.Post.CloseUI(); } catch { } IslandWorldState.Remove(name); yield break; }
			int price = buy.cost.Where(c => c.item.UniqueName == coin).Sum(c => c.amount), cubePrice = buy.cost.Where(c => c.item.UniqueName == cubeName).Sum(c => c.amount);
			int sales = Mathf.Max(1, Mathf.CeilToInt(price / (float)fish.reward.amount));
			int coinsStart = player.Inventory.GetItemCount(coin);
			if (coinsStart > 0) player.Inventory.RemoveItem(coin, coinsStart);
			int cubesStart = player.Inventory.GetItemCount(cubeName);
			if (cubesStart > 0) player.Inventory.RemoveItem(cubeName, cubesStart);
			player.Inventory.AddItem(fishName, sales);
			player.Inventory.AddItem(cubeName, cubePrice);
			int fishes = player.Inventory.GetItemCount(fishName), rep = ui.CurrentReputation;
			Log("  selling " + sales + " " + fishName + " for " + coin + " (" + fish.reward.amount + " each), to buy " + buy.reward.item.UniqueName + " for " + cubePrice + " " + cubeName + " + " + price + " " + coin + " (tier " + tier + ", reputation " + rep + ")");
			for (int n = 0; n < sales; n++)
			{
				try { Traverse.Create(ui).Field("selectedSellableItem").SetValue(fish); ui.Button_Sell(); } catch (Exception x) { Log("  Button_Sell threw: " + x.Message); }
				yield return new WaitForSeconds(0.4f);
			}
			int coins = player.Inventory.GetItemCount(coin);
			Check(ref ok, player.Inventory.GetItemCount(fishName) == fishes - sales, "the fish were taken (" + fishes + " -> " + player.Inventory.GetItemCount(fishName) + ")");
			Check(ref ok, coins == sales * fish.reward.amount, "coins given: " + coins + " " + coin + " (wanted " + sales * fish.reward.amount + ")");
			Check(ref ok, ui.CurrentReputation >= rep + sales * fish.reputationReward, "reputation " + rep + " -> " + ui.CurrentReputation + " (+" + fish.reputationReward + " a sale)");
			int had = player.Inventory.GetItemCount(buy.reward.item.UniqueName), stock = buy.stock;
			try { Traverse.Create(ui).Field("selectedBuyableItem").SetValue(buy); ui.Button_Buy(); } catch (Exception x) { Log("  Button_Buy threw: " + x.Message); }
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, player.Inventory.GetItemCount(buy.reward.item.UniqueName) >= had + buy.reward.amount && buy.stock == stock - 1, "bought " + buy.reward.item.UniqueName + " (carried " + had + " -> " + player.Inventory.GetItemCount(buy.reward.item.UniqueName) + ", stock " + stock + " -> " + buy.stock + ")");
			Check(ref ok, player.Inventory.GetItemCount(coin) == coins - price && player.Inventory.GetItemCount(cubeName) == 0, "paid: " + coin + " " + coins + " -> " + player.Inventory.GetItemCount(coin) + ", " + cubeName + " " + cubePrice + " -> " + player.Inventory.GetItemCount(cubeName));
			try { store.Post.CloseUI(); } catch (Exception x) { Log("  closing threw: " + x.Message); }
			player.Inventory.RemoveItem(buy.reward.item.UniqueName, buy.reward.amount);
			if (cubesStart > 0) player.Inventory.AddItem(cubeName, cubesStart);
			IslandWorldState.Remove(name);
			if (ok) Log("PASS: trophy fish sold for coins, goods bought with coins and trash cubes"); else Fail("store money");
		}

		[ConsoleCommand(name: "CIStoreIsland", docs: "Dev, two-player store test (host): spawns 'cimpstore', an island with Raft's trading post and a quest whose one step is a story blueprint neither player has yet (printed: BLUEPRINT <name>)")]
		public static void StoreIslandCommand(string[] args) { StartTest(StoreIslandRoutine()); }

		static IEnumerator StoreIslandRoutine()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			const string name = "cimpstore";
			string bp = BlueprintSteps.StoryBlueprints().FirstOrDefault(b => !BlueprintSteps.Has(b) && BlueprintSteps.Exists(b));
			if (bp == null) { Fail("the host has every story blueprint: use a fresh world"); yield break; }
			var s = new IslandGenSettings { Seed = 8992, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			f.Objects.Add(new IslandObject { Name = RaftStore.ObjectName, Position = StoreSpot(f), Props = new Dictionary<string, string>() });
			new IslandQuest { Title = "Missed plans", Steps = { new IslandQuest.Step { Type = BlueprintSteps.Type, Target = bp } } }.To(f.Props, 0);
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null || e.Root.GetComponentInChildren<RaftStore>(true) == null) { Fail(name + " did not spawn with a working store"); yield break; }
			Log("BLUEPRINT " + bp);
			Log("PASS: cimpstore spawned (island " + e.Id + ", store " + e.Root.GetComponentInChildren<RaftStore>(true).Post.ObjectIndex.ToString("X8") + ")");
		}

		[ConsoleCommand(name: "CIStoreBuy", docs: "Dev, store test (either player): this player gets the price and buys an item at the island's Raft store through Raft's own store window (TradingPostUI: Open, the item selected, Buy, Close): CIStoreBuy <island> <item>")]
		public static void StoreBuyCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			if (args.Length < 2) { Fail("CIStoreBuy <island> <item>"); return; }
			StartTest(StoreBuyRoutine(e, args[1]));
		}

		static IEnumerator StoreBuyRoutine(IslandWorldState.Entry e, string item)
		{
			RaftStore store = e.Root.GetComponentInChildren<RaftStore>(true);
			Network_Player player = RAPI.GetLocalPlayer();
			TradingPostUI ui = ComponentManager<TradingPostUI>.Value;
			if (store == null || player == null || ui == null) { Fail("no store on '" + e.HostName + "', no player or no store window"); yield break; }
			SO_TradingPost_Buyable.Instance i = store.Post.buyableItems.FirstOrDefault(x => x.reward != null && x.reward.item != null && string.Equals(x.reward.item.UniqueName, item, StringComparison.OrdinalIgnoreCase));
			if (i == null) { Fail("the store doesn't sell " + item); yield break; }
			foreach (Cost c in i.cost) player.Inventory.AddItem(c.item.UniqueName, c.amount);
			int had = player.Inventory.GetItemCount(i.reward.item.UniqueName), stock = i.stock;
			PutPlayerNear(store.Post.transform, 0.3f);
			yield return new WaitForSeconds(1f);
			// (as a player pressing E at the register: Raft's TradingPost.OpenUI opens the menu and fills the window)
			Exception thrown = null;
			try { Traverse.Create(store.Post).Method("OpenUI").GetValue(); } catch (Exception x) { thrown = x; }
			yield return new WaitForSeconds(0.5f);
			if (thrown != null) Log("  opening the store window threw (" + (thrown.InnerException ?? thrown) + "), opened again a moment later");
			Log("  window: player " + (Traverse.Create(ui).Field("localPlayer").GetValue() != null) + ", network " + (Traverse.Create(ui).Field("network").GetValue() != null) +
				", sellables " + (TradingPost.SellableItems != null ? TradingPost.SellableItems.Count : -1) + ", canvas " + (Traverse.Create(typeof(TradingPost)).Field("canvasHelper").GetValue() != null) +
				", ui " + (Traverse.Create(typeof(TradingPost)).Field("tradingPostUI").GetValue() != null) + ", goods " + store.Post.buyableItems.Count(x => x == null || x.reward == null || x.reward.item == null) + " empty");
			int rows = -1;
			try
			{
				if (thrown != null) ui.Open(store.Post);
				var menu = Traverse.Create(ui).Field("buyableMenuItems").GetValue() as System.Collections.IList;
				rows = menu != null ? menu.Count : -1;
				Traverse.Create(ui).Field("selectedBuyableItem").SetValue(i);
				ui.Button_Buy();
			}
			catch (Exception x) { Log("  the store window threw: " + x + "\n" + x.StackTrace); }
			yield return new WaitForSeconds(2f);
			try { store.Post.CloseUI(); } catch (Exception x) { Log("  closing threw: " + x.Message); }
			Log("  the window listed " + rows + " goods");
			int now = player.Inventory.GetItemCount(i.reward.item.UniqueName);
			string what = item + " (" + (Raft_Network.IsHost ? "host" : "client") + "): stock " + stock + " -> " + i.stock + ", carried " + had + " -> " + now;
			if (now >= had + i.reward.amount && i.stock == stock - 1) Log("PASS: bought " + what); else Fail("buying " + what);
		}

		[ConsoleCommand(name: "CIStoreCheck", docs: "Dev, store test (either player): the island's Raft store as this machine has it: CIStoreCheck <island> <item> <stock> [quest done|open]: the item's stock, and the island's first quest done or not")]
		public static void StoreCheckCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			int want;
			if (args.Length < 3 || !int.TryParse(args[2], out want)) { Fail("CIStoreCheck <island> <item> <stock> [done|open]"); return; }
			StartTest(StoreCheckRoutine(e, args, want));
		}

		static IEnumerator StoreCheckRoutine(IslandWorldState.Entry e, string[] args, int want)
		{
			RaftStore store = e.Root.GetComponentInChildren<RaftStore>(true);
			SO_TradingPost_Buyable.Instance i = store != null ? store.Post.buyableItems.FirstOrDefault(x => x.reward != null && x.reward.item != null && string.Equals(x.reward.item.UniqueName, args[1], StringComparison.OrdinalIgnoreCase)) : null;
			bool ok = true;
			if (store != null && args.Length > 3)
			{
				// (at the island: a blueprint step is checked by each player's own machine while they are there)
				PutPlayerNear(store.Post.transform, 0.3f);
				yield return new WaitForSeconds(2f);
				IslandQuest q = QuestTracker.QuestOf(e, 0);
				int step = QuestTracker.StepOf(e, 0);
				Network_Player p = RAPI.GetLocalPlayer();
				Log("  quest '" + q.Title + "' " + q.Steps.Count + " steps, at " + (step + 1) + (step < q.Steps.Count ? " (" + q.Steps[step].Type + " " + q.Steps[step].Target + ", has it " + BlueprintSteps.Has(q.Steps[step].Target) + ")" : "") +
					"; the player " + (p != null ? (p.transform.position - store.Post.transform.position).magnitude.ToString("F0") : "?") + " m from the store; quest ticks " + QuestTracker.TickCount);
				if (!QuestTracker.IsDone(e, 0)) { QuestTracker.CheckBlueprints(); Log("  checked here: step " + (QuestTracker.StepOf(e, 0) + 1)); }
			}
			Check(ref ok, i != null && i.stock == want, args[1] + " stock " + (i != null ? i.stock : -1) + " (wanted " + want + ")");
			if (args.Length > 3) Check(ref ok, QuestTracker.IsDone(e, 0) == (args[3] == "done"), "quest " + (QuestTracker.IsDone(e, 0) ? "done" : "open at step " + (QuestTracker.StepOf(e, 0) + 1)) + " (wanted " + args[3] + ")");
			if (ok) Log("PASS: store check"); else Fail("store check");
		}

		/// <summary>The land's middle, on the ground (where a test island's store stands).</summary>
		static Vector3 StoreSpot(IslandFile f)
		{
			Vector2 c = IslandSpawner.LandCentre(f);
			float step = f.TerrainSize.x / (f.HeightmapResolution - 1);
			return new Vector3(c.x, f.Heights[Mathf.RoundToInt(c.y / step), Mathf.RoundToInt(c.x / step)] * f.TerrainSize.y, c.y);
		}

		/// <summary>Play step "storespot [x z] [n] [size]": flat, clear ground for a store (SL7) on the play's island - the
		/// size x size m square under open sky, all of it the island's own terrain (no object's collider above), at most 0.6 m
		/// from level, 1 m or more above the sea, and no island object's renderer in the box 5 m over it. Logs the n spots
		/// nearest to (x z) (default the middle), at least size apart, in the recipe's coordinates.</summary>
		internal static void StoreSpots(IslandWorldState.Entry e, Vector2 offset, Vector2 near, int n, float size)
		{
			if (e == null || e.Root == null) { Log("  storespot: no island"); return; }
			Transform root = e.Root.transform;
			float half = size / 2f;
			var boxes = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && r.bounds.size.x < 80f && r.bounds.size.z < 80f)
				.Select(r => r.bounds).ToList();
			var spots = new List<Vector4>();
			for (float dx = -150f; dx <= 150f; dx += 2f)
				for (float dz = -150f; dz <= 150f; dz += 2f)
				{
					Vector3 c = e.Position + new Vector3(dx - offset.x, 0f, dz - offset.y);
					float lo = float.MaxValue, hi = float.MinValue;
					bool ok = true;
					for (int i = -2; i <= 2 && ok; i++)
						for (int j = -2; j <= 2 && ok; j++)
						{
							RaycastHit h;
							if (!Physics.Raycast(c + new Vector3(i * half / 2f, 400f, j * half / 2f), Vector3.down, out h, 600f, ~0, QueryTriggerInteraction.Ignore) ||
								!(h.collider is TerrainCollider) || !h.collider.transform.IsChildOf(root)) { ok = false; break; }
							lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y);
						}
					if (!ok || hi - lo > 0.6f || lo < 1f) continue;
					Bounds box = new Bounds(new Vector3(c.x, hi + 2.7f, c.z), new Vector3(size, 5f, size));
					if (boxes.Any(b => b.Intersects(box))) continue;
					spots.Add(new Vector4(dx, dz, lo - e.Position.y, hi - lo));
				}
			var picked = new List<Vector4>();
			foreach (Vector4 s in spots.OrderBy(s => (new Vector2(s.x, s.y) - near).sqrMagnitude))
			{
				if (picked.Any(p => (new Vector2(p.x, p.y) - new Vector2(s.x, s.y)).magnitude < size)) continue;
				picked.Add(s);
				if (picked.Count >= n) break;
			}
			Log("  storespot " + e.Name + ": " + spots.Count + " clear " + size + " m squares; nearest to " + near.x + " " + near.y + ":");
			foreach (Vector4 s in picked)
				Log("    store - at " + s.x + " " + s.y + " ground   (h=" + s.z.ToString("F1") + ", uneven " + s.w.ToString("F2") + " m, " + (new Vector2(s.x, s.y) - near).magnitude.ToString("F0") + " m off)");
		}

		static string CostText(Cost c) { return c == null || c.item == null ? "-" : c.item.UniqueName + "*" + c.amount; }

		static IEnumerator DumpPosts(Scene scene, string name, Action<int> count)
		{
			int n = 0;
			foreach (GameObject root in scene.GetRootGameObjects())
				foreach (TradingPost tp in root.GetComponentsInChildren<TradingPost>(true))
				{
					n++;
					string path = tp.name; for (Transform p = tp.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
					Log(name + ": TradingPost " + path + " at " + tp.transform.position + " rot " + tp.transform.eulerAngles + " scale " + tp.transform.lossyScale);
					Log("  scripts on it: " + string.Join(", ", tp.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()) + "; landmark item " + (tp.GetComponentInParent<LandmarkItem>() != null ? tp.GetComponentInParent<LandmarkItem>().name : "none"));
					Transform top = tp.transform;
					for (int up = 0; up < 2 && top.parent != null; up++) top = top.parent;
					DumpTree(tp.transform, 0, 3);
					Log("  around (2 up: " + top.name + "):");
					DumpTree(top, 0, 2);
				}
			count(n);
			yield break;
		}

		static void DumpTree(Transform t, int depth, int max)
		{
			Renderer r = t.GetComponent<Renderer>();
			var all = t.GetComponentsInChildren<Renderer>(true);
			Bounds b = all.Length > 0 ? all[0].bounds : new Bounds(t.position, Vector3.zero);
			foreach (Renderer x in all) b.Encapsulate(x.bounds);
			Log("  " + new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (off)") + " local " + t.localPosition.ToString("F2") + " size " + b.size.ToString("F2") + " renderers " + all.Length +
				" [" + string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name).ToArray()) + "]");
			if (depth >= max) return;
			foreach (Transform c in t) DumpTree(c, depth + 1, max);
		}
	}
}
