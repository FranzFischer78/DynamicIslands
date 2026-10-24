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
	public static partial class DevTests
	{
		const string TreasureIsland = "citreasure";

		[ConsoleCommand(name: "CIBuriedTreasure", docs: "Dev, in game (host): buried treasure - Raft's own treasure point on a custom island: found by the detector's search, dug up with three digs, the chest gives Raft's treasure loot; stays dug after a reload and a Raft save, back after the regrow days")]
		public static void BuriedTreasureCommand(string[] args) { StartTest(BuriedTreasureRoutine(args != null && args.Contains("keep"))); }

		[ConsoleCommand(name: "CITreasureCount", docs: "Dev, in game: Raft's treasure points - how many under each custom island and how many loose (after loading a world: none of ours may come back loose)")]
		public static void TreasureCountCommand(string[] args)
		{
			TreasurePointManager tm = UnityEngine.Object.FindObjectOfType<TreasurePointManager>();
			if (tm == null) { Fail("no TreasurePointManager"); return; }
			List<TreasurePoint> all = BuriedTreasure.AllPoints(tm);
			int loose = all.Count(p => p.transform.parent == null);
			int ours = 0;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(i => i.Root != null))
			{
				int n = BuriedTreasure.PointsOf(tm, e.Root.transform).Count;
				ours += n;
				if (n > 0 || e.HostName == TreasureIsland) Log("treasure count: '" + e.HostName + "' " + n);
			}
			Log("treasure count: " + all.Count + " in all, " + ours + " on custom islands, " + loose + " loose; parents: " + string.Join(", ", all.Select(p => p.transform.parent != null ? p.transform.parent.name : "-").Distinct().Take(12).ToArray()));
			if (loose == 0) Log("PASS: treasure count"); else Fail("treasure count: " + loose + " loose");
		}

		static IEnumerator BuriedTreasureRoutine(bool keep)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			var s = new IslandGenSettings { Seed = 8383, Radius = 40f, Height = 8f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, TreasureIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			f.Objects.Add(new IslandObject { Name = ContentCatalog.BuriedTreasure, Position = new Vector3(c.x + 3f, ground(c.x + 3f, c.y), c.y) });
			f.Objects.Add(new IslandObject { Name = ContentCatalog.BuriedTreasure, Position = new Vector3(c.x - 8f, ground(c.x - 8f, c.y + 4f), c.y + 4f) });
			IslandWorldState.Remove(TreasureIsland);
			f.Save(IslandSpawner.PathFor(TreasureIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(TreasureIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(TreasureIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == TreasureIsland);
			if (e == null || e.Root == null) { Fail(TreasureIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			TreasurePointManager tm = UnityEngine.Object.FindObjectOfType<TreasurePointManager>();
			List<TreasurePoint> pts = BuriedTreasure.PointsOf(tm, e.Root.transform);
			Check(ref ok, pts.Count == 2, "2 buried treasures on the island (" + pts.Count + ")");
			BuriedTreasure first = e.Root.GetComponentsInChildren<BuriedTreasure>(true).FirstOrDefault(b => b.Number == 0);
			if (first == null || first.Point == null) { Fail("no treasure point"); yield break; }
			float d;
			TreasurePoint near = tm.GetClosestTreasurePointTo(first.transform.position, 60f, true, true, out d);
			Check(ref ok, near == first.Point && d < 1f, "the detector's search finds it where the marker is (" + d.ToString("F2") + " m)");

			// Dig it up: three digs, then pick up the chest
			const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			MethodInfo progress = typeof(TreasurePoint).GetMethod("ProgressExcevation", all);
			PutPlayerNear(first.transform, 1.5f);
			for (int i = 0; i < 3; i++) { progress.Invoke(first.Point, new object[] { 1, true }); yield return new WaitForSeconds(0.6f); }
			yield return new WaitForSeconds(1.5f);
			PickupItem chest = first.Point.pickupNetworked != null ? first.Point.pickupNetworked.GetComponent<PickupItem>() : null;
			Check(ref ok, chest != null && !first.Point.IsBuried, "three digs bring the chest up (buried: " + first.Point.IsBuried + ")");
			Dictionary<string, int> before = Items(player);
			Pickup pickup = player.GetComponentInChildren<Pickup>(true);
			if (chest != null && pickup != null) pickup.PickupItemByType(chest, true);
			yield return new WaitForSeconds(1.5f);
			string got = Gained(before, Items(player));
			Check(ref ok, got.Length > 0, "the chest gives " + (got.Length > 0 ? got : "nothing"));

			// A Raft save: Raft keeps no copy of our treasure (it would come back loose in the sea)
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			SaveNow();
			yield return new WaitForSeconds(3f);
			int loose = BuriedTreasure.AllPoints(tm).Count(p => p.transform.parent == null);
			Check(ref ok, loose == 0, "no loose treasure points after the save (" + loose + ")");

			// Unload and load: the dug one stays gone, the other is still there
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			pts = BuriedTreasure.PointsOf(tm, e.Root.transform);
			Check(ref ok, pts.Count == 1, "after a reload: the dug one stays gone, 1 left (" + pts.Count + ")");
			if (keep) { SaveNow(); yield return new WaitForSeconds(3f); if (ok) Log("PASS: buried treasure (kept)"); else Fail("buried treasure"); yield break; }
			int days = IslandRules.RegrowDays(e) + 1;
			IslandObjectState.Capture(e);
			foreach (ObjectState st in e.State.Values) st.Day -= days;
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			pts = BuriedTreasure.PointsOf(tm, e.Root.transform);
			Check(ref ok, pts.Count == 2, days + " days later it is buried again: " + pts.Count);
			IslandWorldState.Remove(TreasureIsland);
			yield return new WaitForSeconds(1f);
			Check(ref ok, BuriedTreasure.PointsOf(tm, e.Root != null ? e.Root.transform : null).Count == 0, "removing the island takes its treasure out of Raft's list");
			if (ok) Log("PASS: buried treasure"); else Fail("buried treasure");
		}
	}
}
