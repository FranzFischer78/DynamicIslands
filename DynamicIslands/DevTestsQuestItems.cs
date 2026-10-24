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
		const string QuestItemsIsland = "ciquestitems";

		[ConsoleCommand(name: "CIIndexScan", docs: "Dev, editor: scans Raft's island scenes for the object index now if it is out of date (after a Raft update or a new index version) and waits for it")]
		public static void IndexScanCommand(string[] args) { StartTest(IndexScanRoutine()); }

		static IEnumerator IndexScanRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			yield return PlaceableCatalog.EnsureIndex();
			while (PlaceableCatalog.Indexing) yield return new WaitForSecondsRealtime(1f);
			if (PlaceableCatalog.IndexIsCurrent) Log("PASS: index scan"); else Fail("index scan (in the editor)");
		}

		[ConsoleCommand(name: "CIQuestItemPickups", docs: "Dev, in game (host): ROADMAP LM12 - Raft's quest item pickups as editor objects (each knows its quest item), placed on an island: used, each gives the crew Raft's quest item as a story item (name, picture) and goes; Raft's wild beehive is a thing to gather (honeycomb)")]
		public static void QuestItemPickupsCommand(string[] args) { StartTest(QuestItemPickupsRoutine()); }

		static IEnumerator QuestItemPickupsRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureBuilt();
			yield return PlaceableCatalog.EnsureIndex();
			List<string> models = PlaceableCatalog.Browse().SelectMany(cat => cat.Value).Select(en => en.Name).Where(QuestItemPickups.IsModel).Distinct().OrderBy(n => n).ToList();
			Check(ref ok, models.Count >= 20, models.Count + " of Raft's quest item pickups in the object list");
			var pick = new[] { "QuestItemPickup_Vasagatan_KeyCardOffice", "QuestItemPickup_VP_CraneKey", "QuestItemPickup_Utopia_Cable", "QuestItemPickup_Tangaroa_Tape" }.Where(models.Contains).ToList();
			var finds = new[] { "Pickup_Landmark_Beehive" }.ToList();
			yield return PlaceableCatalog.EnsureLoaded(pick.Concat(finds).ToList());
			foreach (string m in pick) Check(ref ok, QuestItemPickups.StoryId(m) != null && StoryItems.Find(QuestItemPickups.StoryId(m)) != null, m + " gives " + (QuestItemPickups.StoryId(m) ?? "nothing known") + " (" + StoryItems.Label(QuestItemPickups.StoryId(m) ?? "") + ")");
			Check(ref ok, finds.All(n => PlaceableCatalog.Get(n) != null && PlaceableCatalog.IsHarvestableName(n)), "Raft's wild beehive is a thing to gather");

			var s = new IslandGenSettings { Seed = 8585, Radius = 40f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, QuestItemsIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			int k = 0;
			foreach (string n in pick.Concat(finds))
			{
				float x = c.x + (k % 4) * 5f - 7.5f, z = c.y + (k / 4) * 5f;
				Dictionary<string, string> props = ObjectProps.Defaults(n);
				props[BehaviourProps.Name] = "piece" + k;
				f.Objects.Add(new IslandObject { Name = n, Position = new Vector3(x, ground(x, z) + 0.2f, z), Scale = PlaceableCatalog.Get(n).transform.localScale, Props = props.Count > 0 ? props : null });
				k++;
			}
			IslandWorldState.Remove(QuestItemsIsland);
			f.Save(IslandSpawner.PathFor(QuestItemsIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(QuestItemsIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(QuestItemsIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == QuestItemsIsland);
			if (e == null || e.Root == null) { Fail(QuestItemsIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1.5f);

			for (int i = 0; i < pick.Count; i++)
			{
				string id = QuestItemPickups.StoryId(pick[i]);
				IslandObjectRef r = ScObjOf(e, "piece" + i);
				if (r == null) { Check(ref ok, false, pick[i] + " on the island"); continue; }
				Log("  piece" + i + " props: " + string.Join("; ", r.Props.Select(kv => kv.Key + "=" + kv.Value).ToArray()));
				ScUse(e, "piece" + i);
				yield return new WaitForSeconds(0.8f);
				bool held = StoryBook.Items.Any(h => h.Def.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && h.Count > 0);
				Check(ref ok, held && !r.gameObject.activeInHierarchy, pick[i] + ": picked up, the crew holds '" + StoryItems.Label(id) + "' (" + held + "), the model gone (" + !r.gameObject.activeInHierarchy + ")");
			}
			Pickup pickup = player.GetComponentInChildren<Pickup>(true);
			foreach (string n in finds)
			{
				PickupItem_Networked pn = e.Root.GetComponentsInChildren<PickupItem_Networked>(true).FirstOrDefault(p => p.gameObject.activeInHierarchy && p.GetComponentsInParent<Transform>(true).Any(tr => tr.name.StartsWith(n, StringComparison.Ordinal)));
				if (pn == null)
				{
					Transform piece = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(tr => tr.name.StartsWith(n, StringComparison.Ordinal));
					Check(ref ok, false, n + ": none to pick" + (piece != null ? " (" + piece.name + " active " + piece.gameObject.activeInHierarchy + ": " + string.Join(", ", piece.GetComponentsInChildren<Component>(true).Select(cp => cp != null ? cp.GetType().Name : "-").Distinct().ToArray()) + ")" : " (not spawned)"));
					continue;
				}
				PutPlayerNear(pn.transform, 1.2f);
				yield return new WaitForSeconds(0.3f);
				Dictionary<string, int> before = Items(player);
				pickup.PickupItemByType(pn.GetComponent<PickupItem>(), true);
				yield return new WaitForSeconds(1f);
				string got = Gained(before, Items(player));
				Check(ref ok, got.Length > 0 || !pn.gameObject.activeInHierarchy, n + ": picked, gives " + (got.Length > 0 ? got : "nothing in the inventory") + " (gone: " + !pn.gameObject.activeInHierarchy + ")");
			}
			OnRaftCommand();
			IslandWorldState.Remove(QuestItemsIsland);
			if (ok) Log("PASS: quest item pickups"); else Fail("quest item pickups");
		}
	}
}
