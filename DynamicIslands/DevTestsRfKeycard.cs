using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// RF4: keycard doors and buried treasure that only the metal detector finds. CIRfKeycard and CIRfDetector run on the
	/// host alone; CIRfDetectorMp island|dig|nodig|check|reload are the steps of the two-player test (driven by
	/// _ProjectDocs/tools/mprf4.ps1): one player digs, both see it dug, and the other finds nothing left to dig.
	/// </summary>
	public static partial class DevTests
	{
		const string RfKeyIsland = "cirfkeycard", RfDetectIsland = "cirfdetect", RfMpIsland = "cirfmp";
		const string RfCoin = "rf4-coin", RfMpCoin = "rf4-mpcoin";
		const BindingFlags RfAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

		static string RfCard { get { return StoryItems.Ref(ReadyPieces.ItemOf("PlantationDoor")); } }

		/// <summary>A generated flat test island with the given objects put on its ground around the land centre (dx, dz).</summary>
		static IslandFile RfIsland(string name, int seed, params KeyValuePair<Vector2, IslandObject>[] objects)
		{
			var s = new IslandGenSettings { Seed = seed, Radius = 40f, Height = 8f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			foreach (KeyValuePair<Vector2, IslandObject> o in objects)
			{
				float x = c.x + o.Key.x, z = c.y + o.Key.y;
				o.Value.Position = new Vector3(x, IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y, z);
				f.Objects.Add(o.Value);
			}
			return f;
		}

		static KeyValuePair<Vector2, IslandObject> RfAt(float dx, float dz, IslandObject o) { return new KeyValuePair<Vector2, IslandObject>(new Vector2(dx, dz), o); }

		/// <summary>Saves and spawns a test island near the raft (null: it failed, said so).</summary>
		static IEnumerator RfSpawn(IslandFile f, string name, Action<IslandWorldState.Entry> done)
		{
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			Vector3? spot = raft.HasValue ? CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f) : null;
			if (!spot.HasValue) { Fail("no open sea near the raft"); done(null); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail(name + " did not spawn"); done(null); yield break; }
			yield return new WaitForSeconds(1f);
			done(e);
		}

		static IslandObjectRef RfObj(IslandWorldState.Entry e, int index)
		{
			return e != null && e.Root != null ? e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(r => r.Index == index) : null;
		}

		static bool RfShown(IslandWorldState.Entry e, int index) { IslandObjectRef r = RfObj(e, index); return r != null && r.gameObject.activeInHierarchy; }

		static void RfSetCard(int count)
		{
			string id = StoryItems.IdOf(RfCard);
			int have = StoryBook.Count(id);
			if (have > count) StoryBook.Take(id, have - count);
			else if (have < count) StoryBook.Give(id, count - have);
		}

		#region Keycard doors

		[ConsoleCommand(name: "CIRfKeycard", docs: "Dev, in game (host): RF4 keycard doors - a Tangaroa card reader is shut without the keycard and opens with it (with Raft's card sound), stays open after a reload; the 'used up' setting takes the card; the inspector's card setting and Check's warning on a door no card opens")]
		public static void RfKeycardCommand(string[] args) { StartTest(RfKeycardRoutine()); }

		static IEnumerator RfKeycardRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || !CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();

			// The settings: a card reader wants Raft's keycard and plays its card sound; the card setting rewrites only that check
			Dictionary<string, string> keep = ObjectProps.Defaults("PlantationDoor");
			keep[BehaviourProps.Name] = "rfkeep";
			Check(ref ok, KeycardDoor.Is("PlantationDoor") && KeycardDoor.IsCardReader("PlantationDoor") && !KeycardDoor.IsCardReader("UT_MainEntranceDoor"), "PlantationDoor is a keycard door, the Utopia entrance a key door");
			Check(ref ok, KeycardDoor.CardOf(keep) == RfCard && !KeycardDoor.UsedUp(keep), "a new card reader wants " + KeycardDoor.CardOf(keep) + ", kept");
			Check(ref ok, ObjectProps.Get(keep, BehaviourProps.EventKey("use")).Contains(KeycardDoor.CardSound), "it plays Raft's card reader sound when it opens");
			Dictionary<string, string> use = KeycardDoor.WithCard(keep, RfCard, true);
			use[BehaviourProps.Name] = "rfuse";
			Check(ref ok, KeycardDoor.UsedUp(use) && KeycardDoor.CardOf(use) == RfCard, "'Card used up' makes it a take check: " + ObjectProps.Get(use, BehaviourProps.CheckKey("use")));
			Dictionary<string, string> none = KeycardDoor.WithCard(keep, null, false);
			Check(ref ok, KeycardDoor.CardOf(none) == null && !none.ContainsKey(BehaviourProps.CheckKey("use")), "card 'None' removes the check");
			Dictionary<string, string> two = ObjectProps.With(keep, BehaviourProps.CheckKey("use"), ObjectProps.Get(keep, BehaviourProps.CheckKey("use")) + "\nsignal|power|");
			Dictionary<string, string> other = KeycardDoor.WithCard(two, StoryItems.Ref("rf4-pass"), false);
			Check(ref ok, KeycardDoor.CardOf(other) == StoryItems.Ref("rf4-pass") && ObjectProps.Get(other, BehaviourProps.CheckKey("use")).Contains("signal|power"), "another card keeps the door's other checks: " + ObjectProps.Get(other, BehaviourProps.CheckKey("use")).Replace("\n", " / "));
			List<KeyValuePair<string, string>> choices = KeycardDoor.Choices(new Dictionary<string, string> { { StoryItems.Key, "rf4-pass|Staff pass||" } }, RfCard);
			Check(ref ok, choices.Count > 2 && choices[0].Key == StoryItems.Ref("rf4-pass") && choices.Any(c => c.Key == RfCard), "the card list has the island's own items first, then Raft's (" + choices.Count + ": " + string.Join(", ", choices.Take(4).Select(c => c.Value).ToArray()) + "...)");

			// Check: a key door that no story item opens is a warning; with its card, none
			IslandFile probe = RfIsland(RfKeyIsland, 4141, RfAt(0f, 0f, new IslandObject { Name = "PlantationDoor", Props = none }));
			PlanChecker.Facts nx = PlanChecker.FromFile(probe, RfKeyIsland, false);
			bool warned = nx.Traps.Any(t => t.Text.Contains("no story item opens"));
			Check(ref ok, warned, "Check warns about a keycard door no card opens" + (warned ? ": " + nx.Traps.First(t => t.Text.Contains("no story item opens")).Text : ""));
			probe.Objects[0].Props = keep;
			Check(ref ok, !PlanChecker.FromFile(probe, RfKeyIsland, false).Traps.Any(t => t.Text.Contains("no story item opens")), "with its card: no warning");

			// In a world: shut without the card, open with it
			RfSetCard(0);
			yield return new WaitForSeconds(1f);
			IslandFile f = RfIsland(RfKeyIsland, 4141,
				RfAt(0f, 0f, new IslandObject { Name = "PlantationDoor", Props = keep, Scale = Vector3.one }),
				RfAt(6f, 0f, new IslandObject { Name = "PlantationDoor", Props = use, Scale = Vector3.one }));
			IslandWorldState.Entry e = null;
			yield return RfSpawn(f, RfKeyIsland, x => e = x);
			if (e == null) yield break;
			Check(ref ok, RfShown(e, 0) && RfShown(e, 1), "both doors stand shut at first");
			PutPlayerNear(RfObj(e, 0).transform, 1.5f);
			Behaviours.Fire(e, 0, "use", true);
			Behaviours.Fire(e, 1, "use", true);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, RfShown(e, 0) && RfShown(e, 1), "without the keycard both stay shut");
			RfSetCard(1);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, StoryBook.Count(StoryItems.IdOf(RfCard)) == 1, "the crew has the keycard");
			Behaviours.Fire(e, 0, "use", true);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, !RfShown(e, 0), "with the keycard the first door opens");
			Check(ref ok, StoryBook.Count(StoryItems.IdOf(RfCard)) == 1, "a kept card stays with the crew (" + StoryBook.Count(StoryItems.IdOf(RfCard)) + ")");
			Behaviours.Fire(e, 1, "use", true);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, !RfShown(e, 1), "the 'used up' door opens too");
			Check(ref ok, StoryBook.Count(StoryItems.IdOf(RfCard)) == 0, "... and takes the card (" + StoryBook.Count(StoryItems.IdOf(RfCard)) + " left)");

			// A reload: both stay open
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, e.Root != null && RfObj(e, 0) != null && !RfShown(e, 0) && !RfShown(e, 1), "after a reload both doors stay open");
			IslandWorldState.Remove(RfKeyIsland);
			if (ok) Log("PASS: rf keycard"); else Fail("rf keycard");
		}

		#endregion

		#region Metal detector treasure

		static MetalDetector RfDetector(Network_Player player)
		{
			Item_Base item = ItemManager.GetItemByName(global::DynamicIslands.Editor.RaftFeatures.MetalDetectorItem);
			if (item == null) return null;
			if (player.Inventory.GetItemCount(item.UniqueName) == 0) player.Inventory.AddItem(item.UniqueName, 1);
			Hotbar hotbar = HarmonyLib.Traverse.Create(player.Inventory).Field("hotbar").GetValue<Hotbar>();
			if (hotbar != null) hotbar.SelectItem(item);
			return player.GetComponentInChildren<MetalDetector>(true);
		}

		static void RfDropDetector(Network_Player player)
		{
			player.Inventory.RemoveItem(global::DynamicIslands.Editor.RaftFeatures.MetalDetectorItem, 99);
		}

		/// <summary>Holds the detector's use button for a while where the player stands; the last beep cooldown and distance.</summary>
		static IEnumerator RfHold(MetalDetector md, float seconds, float[] result)
		{
			var holding = typeof(UsableTool).GetField("isHoldingUseButton", RfAll);
			PropertyInfo cool = typeof(MetalDetector).GetProperty("BeepCooldownByDistance", RfAll);
			FieldInfo dist = typeof(MetalDetector).GetField("distanceToClosest", RfAll);
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < seconds)
			{
				if (md != null) holding.SetValue(md, true);
				yield return null;
			}
			result[0] = md != null ? (float)cool.GetValue(md, null) : -1f;
			result[1] = md != null ? (float)dist.GetValue(md) : -1f;
		}

		static void RfRelease(MetalDetector md)
		{
			if (md != null) typeof(UsableTool).GetField("isHoldingUseButton", RfAll).SetValue(md, false);
		}

		/// <summary>What the shovel would dig here: Raft's shovel only finds revealed, buried points within 5 m of the feet.</summary>
		static TreasurePoint RfShovelFinds(TreasurePointManager tm, Network_Player player)
		{
			float d;
			return tm.GetClosestTreasurePointTo(player.FeetPosition, 5f, false, false, out d);
		}

		static IEnumerator RfDig(BuriedTreasure b)
		{
			MethodInfo progress = typeof(TreasurePoint).GetMethod("ProgressExcevation", RfAll);
			for (int i = 0; i < 3 && b.Point != null; i++) { progress.Invoke(b.Point, new object[] { 1, true }); yield return new WaitForSeconds(0.6f); }
			yield return new WaitForSeconds(1.5f);
		}

		[ConsoleCommand(name: "CIRfDetector", docs: "Dev, in game (host): RF4 buried treasure - without a detector it stays hidden and the shovel finds nothing; Raft's detector beeps faster nearer (12, 6, 1 m) and reveals it; three digs, the chest gives Raft's loot plus its own (planks and a story item) once; with a story item it never comes back")]
		public static void RfDetectorCommand(string[] args) { StartTest(RfDetectorRoutine()); }

		static IEnumerator RfDetectorRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || !CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			int coins = StoryBook.Count(RfCoin);
			if (coins > 0) { StoryBook.Take(RfCoin, coins); yield return new WaitForSeconds(1f); }
			RfDropDetector(player);
			BuriedTreasure.ForgetGiven();
			var props = new Dictionary<string, string> { { ObjectProps.TreasureLoot, "Plank*7;" + StoryItems.Ref(RfCoin) + "*1" } };
			IslandFile f = RfIsland(RfDetectIsland, 5151, RfAt(3f, 0f, new IslandObject { Name = ContentCatalog.BuriedTreasure, Props = props, Scale = Vector3.one }));
			f.Props[StoryItems.Key] = RfCoin + "|Old coin||A coin dug up on a test island.";
			Check(ref ok, !ObjectProps.IsLoot(ContentCatalog.BuriedTreasure, props), "treasure.loot does not make the marker a chest (IsLoot)");
			IslandWorldState.Entry e = null;
			yield return RfSpawn(f, RfDetectIsland, x => e = x);
			if (e == null) yield break;
			TreasurePointManager tm = UnityEngine.Object.FindObjectOfType<TreasurePointManager>();
			BuriedTreasure b = e.Root.GetComponentInChildren<BuriedTreasure>(true);
			if (b == null || b.Point == null || tm == null) { Fail("no treasure point"); yield break; }
			Check(ref ok, b.Loot.Count == 2 && b.HoldsStory, "the treasure carries its own loot (" + b.Loot.Count + ") with a story item");

			// Without a detector: standing on the spot reveals nothing, the shovel finds nothing
			PutPlayerNear(b.transform, 0.5f);
			yield return new WaitForSeconds(2f);
			Check(ref ok, !b.Point.isRevealedViaDetector && RfShovelFinds(tm, player) == null, "without a detector it stays hidden: the shovel finds nothing (revealed " + b.Point.isRevealedViaDetector + ")");

			// The detector: beeps faster nearer, reveals it close by
			MetalDetector md = RfDetector(player);
			for (int i = 0; i < 20 && (md == null || !md.isActiveAndEnabled); i++) { yield return null; md = player.GetComponentInChildren<MetalDetector>(false); }
			if (md == null) { Fail("the metal detector is not in hand"); yield break; }
			float apex = (float)typeof(MetalDetector).GetField("beepApexDistance", RfAll).GetValue(md);
			var cools = new List<float>();
			foreach (float off in new[] { 8.5f, 4.2f, 0.6f })
			{
				PutPlayerNear(b.transform, off);
				var r = new float[2];
				yield return RfHold(md, 1.5f, r);
				cools.Add(r[0]);
				TreasurePoint closest = (TreasurePoint)typeof(MetalDetector).GetField("currentClosestPoint", RfAll).GetValue(md);
				Log("detector at " + r[1].ToString("F1") + " m: beep every " + r[0].ToString("F2") + " s (closest is ours: " + (closest == b.Point) + ", apex " + apex.ToString("F1") + " m)");
			}
			RfRelease(md);
			Check(ref ok, cools[0] > cools[1] && cools[1] > cools[2], "it beeps faster nearer: " + string.Join(" > ", cools.Select(c => c.ToString("F2")).ToArray()) + " s");
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, b.Point.isRevealedViaDetector && RfShovelFinds(tm, player) == b.Point, "close by the detector reveals it, and the shovel finds it");

			// Dig and pick up: Raft's loot and its own, once
			Dictionary<string, int> before = Items(player);
			yield return RfDig(b);
			PickupItem chest = b.Point != null && b.Point.pickupNetworked != null ? b.Point.pickupNetworked.GetComponent<PickupItem>() : null;
			Check(ref ok, chest != null && !b.Point.IsBuried, "three digs bring the chest up");
			Pickup pickup = player.GetComponentInChildren<Pickup>(true);
			if (chest != null && pickup != null) pickup.PickupItemByType(chest, true);
			yield return new WaitForSeconds(2f);
			Dictionary<string, int> after = Items(player);
			int planks = (after.ContainsKey("Plank") ? after["Plank"] : 0) - (before.ContainsKey("Plank") ? before["Plank"] : 0);
			Check(ref ok, planks >= 7, "the chest gives " + Gained(before, after) + " (planks +" + planks + ")");
			Check(ref ok, StoryBook.Count(RfCoin) == 1, "... and the story item to the crew (" + StoryBook.Count(RfCoin) + ")");
			if (chest != null) BuriedTreasure.PickedUp(player, chest);
			yield return new WaitForSeconds(1f);
			Dictionary<string, int> again = Items(player);
			Check(ref ok, (again.ContainsKey("Plank") ? again["Plank"] : 0) == (after.ContainsKey("Plank") ? after["Plank"] : 0) && StoryBook.Count(RfCoin) == 1, "picked up again: nothing more (once)");
			Check(ref ok, ContentState.IsUsed(e, BuriedTreasure.KeepBase + b.Number), "marked as dug for good (it held a story item)");

			// Long after: a treasure with a story item never comes back
			int days = IslandRules.RegrowDays(e) + 1;
			yield return ReloadIslandRoutine(e);
			IslandObjectState.Capture(e);
			foreach (ObjectState st in e.State.Values) st.Day -= Math.Max(days, 30);
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			Check(ref ok, e.Root != null && BuriedTreasure.PointsOf(tm, e.Root.transform).Count == 0, "after a reload, " + Math.Max(days, 30) + " days later: still dug up (" + (e.Root != null ? BuriedTreasure.PointsOf(tm, e.Root.transform).Count : -1) + " points)");

			// An older save kept it under the old key (0xD0000 + n, before 1d72043): still dug up
			e.State.Remove(BuriedTreasure.KeepBase + b.Number);
			e.State[BuriedTreasure.OldKeepBase + b.Number] = new ObjectState { Active = false, Yield = 0, Day = Behaviours.Today };
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			Check(ref ok, e.Root != null && BuriedTreasure.PointsOf(tm, e.Root.transform).Count == 0 && BuriedTreasure.DugForGood(e, b.Number), "an older save's key (0xD0000 + n): still dug up");
			RfDropDetector(player);
			IslandWorldState.Remove(RfDetectIsland);
			if (ok) Log("PASS: rf detector"); else Fail("rf detector");
		}

		[ConsoleCommand(name: "CIRfDetectorMp", docs: "Dev, in game: RF4 two-player buried treasure steps: island (host: makes cirfmp with a treasure holding planks and a story item) | dig (this player finds it with the detector, digs, picks up: gets the loot) | nodig (this player finds nothing left to dig there) | check (this machine shows it dug, the crew has the story item) | reload (host: reload, days later: not back)")]
		public static void RfDetectorMpCommand(string[] args) { StartTest(RfDetectorMpRoutine(args != null && args.Length > 0 ? args[0] : "")); }

		static IEnumerator RfDetectorMpRoutine(string step)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("run in a world"); yield break; }
			bool ok = true;
			TreasurePointManager tm = UnityEngine.Object.FindObjectOfType<TreasurePointManager>();
			if (step == "island")
			{
				if (!Raft_Network.IsHost) { Fail("island: the host makes it"); yield break; }
				yield return EnsureAlive();
				int had = StoryBook.Count(RfMpCoin);
				if (had > 0) StoryBook.Take(RfMpCoin, had);
				// (an earlier run's cirfmp kept in the world: the players then looked at different ones - mprf4, 2026-10-18)
				int old = IslandWorldState.Remove(RfMpIsland);
				if (old > 0) { Log("Removed " + old + " earlier '" + RfMpIsland + "'"); yield return new WaitForSeconds(2f); }
				var props = new Dictionary<string, string> { { ObjectProps.TreasureLoot, "Plank*5;" + StoryItems.Ref(RfMpCoin) + "*1" } };
				IslandFile f = RfIsland(RfMpIsland, 6161, RfAt(3f, 0f, new IslandObject { Name = ContentCatalog.BuriedTreasure, Props = props, Scale = Vector3.one }));
				f.Props[StoryItems.Key] = RfMpCoin + "|Crew coin||A coin dug up by one player, for the whole crew.";
				IslandWorldState.Entry made = null;
				yield return RfSpawn(f, RfMpIsland, x => made = x);
				if (made == null) yield break;
				Log("PASS: mp island");
				yield break;
			}
			IslandWorldState.Entry e = LoadedIsland(new[] { RfMpIsland });
			if (e == null || tm == null) yield break;
			BuriedTreasure b = e.Root.GetComponentInChildren<BuriedTreasure>(true);
			if (b == null) { Fail("no treasure marker on " + RfMpIsland); yield break; }
			switch (step)
			{
				case "dig":
					{
						yield return EnsureAlive();
						BuriedTreasure.ForgetGiven();
						if (b.Point == null || !b.Point.IsBuried) { Fail("dig: nothing buried here"); yield break; }
						MetalDetector md = RfDetector(player);
						for (int i = 0; i < 20 && (md == null || !md.isActiveAndEnabled); i++) { yield return null; md = player.GetComponentInChildren<MetalDetector>(false); }
						PutPlayerNear(b.transform, 0.6f);
						yield return RfHold(md, 1.5f, new float[2]);
						RfRelease(md);
						Check(ref ok, b.Point.isRevealedViaDetector, "the detector reveals it");
						Dictionary<string, int> before = Items(player);
						yield return RfDig(b);
						PickupItem chest = b.Point != null && b.Point.pickupNetworked != null ? b.Point.pickupNetworked.GetComponent<PickupItem>() : null;
						Pickup pickup = player.GetComponentInChildren<Pickup>(true);
						if (chest != null && pickup != null) pickup.PickupItemByType(chest, true);
						yield return new WaitForSeconds(2.5f);
						Dictionary<string, int> after = Items(player);
						int planks = (after.ContainsKey("Plank") ? after["Plank"] : 0) - (before.ContainsKey("Plank") ? before["Plank"] : 0);
						// (the chest can be in the bag before this looks for it - on player 2 Raft's last dig took it, 2026-10-18: what was gained counts)
						Check(ref ok, planks >= 5 && StoryBook.Count(RfMpCoin) == 1, "dug up and picked: " + Gained(before, after) + (chest == null ? " (taken by the dig)" : ""));
						RfDropDetector(player);
						break;
					}
				case "nodig":
					{
						yield return EnsureAlive();
						MetalDetector md = RfDetector(player);
						for (int i = 0; i < 20 && (md == null || !md.isActiveAndEnabled); i++) { yield return null; md = player.GetComponentInChildren<MetalDetector>(false); }
						PutPlayerNear(b.transform, 0.6f);
						Dictionary<string, int> before = Items(player);
						yield return RfHold(md, 1.5f, new float[2]);
						RfRelease(md);
						TreasurePoint p = RfShovelFinds(tm, player);
						Check(ref ok, p == null || p != b.Point, "nothing left to dig here for this player (" + (b.Point == null ? "no point" : "buried " + b.Point.IsBuried) + ")");
						Check(ref ok, Gained(before, Items(player)).Length == 0, "and nothing given");
						RfDropDetector(player);
						break;
					}
				case "check":
					{
						float t0 = Time.realtimeSinceStartup;
						Func<bool> dug = () => b.Point == null || !b.Point.IsBuried && (b.Point.pickupNetworked == null || !b.Point.pickupNetworked.gameObject.activeInHierarchy);
						while ((!dug() || StoryBook.Count(RfMpCoin) != 1 || !ContentState.IsUsed(e, BuriedTreasure.KeepBase + b.Number)) && Time.realtimeSinceStartup - t0 < 8f) yield return new WaitForSeconds(0.5f);
						Check(ref ok, dug(), "this machine shows the treasure dug up and its chest taken");
						Check(ref ok, StoryBook.Count(RfMpCoin) == 1, "the crew has the coin once (" + StoryBook.Count(RfMpCoin) + ")");
						Check(ref ok, ContentState.IsUsed(e, BuriedTreasure.KeepBase + b.Number), "the island's state says dug for good");
						break;
					}
				case "reload":
					{
						if (!Raft_Network.IsHost) { Fail("reload: the host"); yield break; }
						IslandObjectState.Capture(e);
						foreach (ObjectState st in e.State.Values) st.Day -= 30;
						yield return ReloadIslandRoutine(e);
						yield return new WaitForSeconds(1f);
						Check(ref ok, e.Root != null && BuriedTreasure.PointsOf(tm, e.Root.transform).Count == 0, "30 days later and reloaded: not back");
						break;
					}
				case "clean":
					IslandWorldState.Remove(RfMpIsland);
					Log("PASS: mp clean");
					yield break;
				default:
					Fail("CIRfDetectorMp island|dig|nodig|check|reload|clean");
					yield break;
			}
			if (ok) Log("PASS: rf mp " + step); else Fail("rf mp " + step);
		}

		#endregion
	}
}
