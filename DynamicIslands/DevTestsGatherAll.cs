using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		const string GatherAllIsland = "cigatherall";

		[ConsoleCommand(name: "CIGatherAll", docs: "Dev, in game (host): every thing to gather of the editor (ROADMAP LM11: also the snowy pine, seaweed, and those of Raft's story islands) on one island - each picked up (or a tree chopped down) gives Raft's items, stays used after a reload, and is back after the regrow days")]
		public static void GatherAllCommand(string[] args) { StartTest(GatherAllRoutine()); }

		static IEnumerator GatherAllRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureBuilt();
			// (every thing the editor lists under Things to gather, also those of Raft's other islands - loaded with their scene - LM11)
			List<string> names = PlaceableCatalog.GatherNames().Where(n => n != PlaceableCatalog.RaftCrate).ToList();
			yield return PlaceableCatalog.EnsureLoaded(names);
			names = names.Where(n => PlaceableCatalog.Get(n) != null).ToList();
			Check(ref ok, names.Count >= 25, names.Count + " things to gather in the catalog");

			// One of each in a grid on a flat island; the sea finds just off its shore
			var s = new IslandGenSettings { Seed = 8484, Radius = 60f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, GatherAllIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			int land = 0, wet = 0;
			foreach (string n in names)
			{
				Vector2 p;
				if (ScatterTool.OnlyUnderWater(n))
				{
					float a = wet++ * 0.5f, d = 0f;
					Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					while (d < 200f && ground(c.x + dir.x * d, c.y + dir.y * d) > f.WaterLevel - 3f) d += 0.5f;
					p = c + dir * d;
				}
				else { p = c + new Vector2((land % 6 - 2.5f) * 7f, (land / 6 - 2.5f) * 7f); land++; }
				f.Objects.Add(new IslandObject { Name = n, Position = new Vector3(p.x, ground(p.x, p.y), p.y), Scale = PlaceableCatalog.Get(n).transform.localScale });
			}
			IslandWorldState.Remove(GatherAllIsland);
			f.Save(IslandSpawner.PathFor(GatherAllIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(GatherAllIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(GatherAllIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == GatherAllIsland);
			if (e == null || e.Root == null) { Fail(GatherAllIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1.5f);

			Pickup pickup = player.GetComponentInChildren<Pickup>(true);
			var used = new Dictionary<int, string>();
			var nothing = new List<string>();
			foreach (PickupItem_Networked pn in e.Root.GetComponentsInChildren<PickupItem_Networked>(true).ToList())
			{
				if (pn == null || !pn.gameObject.activeInHierarchy) continue;
				string label = pn.name.Replace("(Clone)", "");
				PutPlayerNear(pn.transform, 1.2f);
				if (pn.transform.position.y < -0.5f) player.PersonController.SwitchControllerType(ControllerType.Water);
				yield return new WaitForSeconds(0.2f);
				Dictionary<string, int> before = Items(player);
				HarvestableTree tree = pn.GetComponent<HarvestableTree>();
				if (tree != null)
				{
					for (int i = 0; i < 12 && !tree.Depleted; i++) { tree.Harvest(player.Inventory); yield return new WaitForSeconds(0.15f); }
				}
				else pickup.PickupItemByType(pn.GetComponent<PickupItem>(), true);
				yield return new WaitForSeconds(0.6f);
				Dictionary<string, int> after = Items(player);
				string got = Gained(before, after);
				if (got.Length == 0) nothing.Add(label + " (" + string.Join(", ", pn.GetComponents<Component>().Select(cp => cp != null ? cp.GetType().Name : "-").ToArray()) + ")");
				// (what it gave goes again: a full inventory took nothing from the last few)
				foreach (var kv in after) { int b; before.TryGetValue(kv.Key, out b); if (kv.Value > b) player.Inventory.RemoveItem(kv.Key, kv.Value - b); }
				used[(int)(pn.ObjectIndex & 0xFFFF)] = label;
				player.PersonController.SwitchControllerType(ControllerType.Ground);
			}
			Check(ref ok, nothing.Count == 0, used.Count + " things gathered, each giving Raft's items" + (nothing.Count > 0 ? " - nothing from: " + string.Join(", ", nothing.ToArray()) : ""));
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			Func<int, PickupItem_Networked> byOrd = o => e.Root.GetComponentsInChildren<PickupItem_Networked>(true).FirstOrDefault(p => (int)(p.ObjectIndex & 0xFFFF) == o);
			Func<PickupItem_Networked, bool> isUsed = p => p == null || !p.gameObject.activeSelf || (p.GetComponent<HarvestableTree>() != null && p.GetComponent<HarvestableTree>().Depleted);
			string[] back = used.Where(kv => !isUsed(byOrd(kv.Key))).Select(kv => kv.Value).ToArray();
			Check(ref ok, back.Length == 0, "after a reload all " + used.Count + " stay used" + (back.Length > 0 ? " - back: " + string.Join(", ", back) : ""));
			int days = IslandRules.RegrowDays(e) + 1;
			IslandObjectState.Capture(e);
			foreach (ObjectState st in e.State.Values) st.Day -= days;
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			string[] still = used.Where(kv => isUsed(byOrd(kv.Key))).Select(kv => kv.Value).ToArray();
			Check(ref ok, still.Length == 0, days + " days later all are back" + (still.Length > 0 ? " - still used: " + string.Join(", ", still) : ""));
			IslandWorldState.Remove(GatherAllIsland);
			if (ok) Log("PASS: gather all"); else Fail("gather all");
		}
	}
}
