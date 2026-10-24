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
		const string DirtHoneyIsland = "cidirthoney";

		/// <summary>A small island with three of Raft's dirt spots and a wild beehive on its land.</summary>
		static IslandFile MakeDirtHoneyIsland()
		{
			var s = new IslandGenSettings { Seed = 8181, Radius = 40f, Height = 10f, Trees = 0.1f, Bushes = 0.1f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, DirtHoneyIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			for (int i = 0; i < 3; i++)
			{
				float x = c.x + 6f * Mathf.Cos(i * 2.1f), z = c.y + 6f * Mathf.Sin(i * 2.1f);
				f.Objects.Add(new IslandObject { Name = "Pickup_Landmark_DirtPickup", Position = new Vector3(x, ground(x, z), z) });
			}
			float hx = c.x - 8f, hz = c.y + 3f;
			f.Objects.Add(new IslandObject { Name = ContentCatalog.WildHive, Position = new Vector3(hx, ground(hx, hz), hz),
				Props = new Dictionary<string, string> { { ObjectProps.LootItems, ContentCatalog.WildHiveLoot }, { ObjectProps.NoteTitle, "Wild beehive" } } });
			return f;
		}

		[ConsoleCommand(name: "CIDirtHoney", docs: "Dev, in game (host): dirt and honey on islands - Raft's dirt spots (dug with the shovel: tag and layer kept) give dirt, stay gone after a reload and come back after the regrow days; a wild beehive gives honeycomb and refills")]
		public static void DirtHoneyCommand(string[] args) { StartTest(DirtHoneyRoutine()); }

		static IEnumerator DirtHoneyRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			Check(ref ok, PlaceableCatalog.IsHarvestableName("Pickup_Landmark_DirtPickup") && ContentCatalog.IsLootObject(ContentCatalog.WildHive), "dirt spot is a thing to gather, the wild beehive a container");
			IslandWorldState.Remove(DirtHoneyIsland);
			MakeDirtHoneyIsland().Save(IslandSpawner.PathFor(DirtHoneyIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(DirtHoneyIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(DirtHoneyIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == DirtHoneyIsland);
			if (e == null || e.Root == null) { Fail(DirtHoneyIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);

			// Dirt: Raft's shovel digs what is tagged Pickup_Shovel on the Item layer
			List<PickupItem_Networked> dirt = PickupsOf(e, "DirtPickup");
			Check(ref ok, dirt.Count == 3, "3 dirt spots on the island (" + dirt.Count + ")");
			Check(ref ok, dirt.All(d => d.GetComponentsInChildren<Transform>(true).Any(t => t.CompareTag("Pickup_Shovel") && t.gameObject.layer == LayerMask.NameToLayer("Item"))), "each one diggable by Raft's shovel (tag Pickup_Shovel, layer Item)");
			Pickup pickup = player.GetComponentInChildren<Pickup>(true);
			int picked = -1;
			if (dirt.Count > 0 && pickup != null)
			{
				PickupItem_Networked pn = dirt[0];
				PutPlayerNear(pn.transform, 1f);
				yield return new WaitForSeconds(0.3f);
				Dictionary<string, int> before = Items(player);
				pickup.PickupItemByType(pn.GetComponent<PickupItem>(), true);
				yield return new WaitForSeconds(1f);
				string got = Gained(before, Items(player));
				picked = (int)(pn.ObjectIndex & 0xFFFF);
				Check(ref ok, got.Contains("Dirt") && !pn.gameObject.activeInHierarchy, "digging a dirt spot gives " + (got.Length > 0 ? got : "nothing") + " and it is gone");
			}

			// Honey: the wild beehive
			List<string> honey = ScOpenChest(e, "Wild beehive");
			Check(ref ok, honey != null && honey.Any(h => h.IndexOf("honeycomb", StringComparison.OrdinalIgnoreCase) >= 0), "the wild beehive gives " + (honey != null ? string.Join(", ", honey.ToArray()) : "nothing"));
			OnRaftCommand();
			yield return new WaitForSeconds(1f);

			// After a reload the dirt spot stays dug; regrow days later the dirt and the honey are back
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			Check(ref ok, picked < 0 || PickupsOf(e, "DirtPickup").Any(p => (int)(p.ObjectIndex & 0xFFFF) == picked && !p.gameObject.activeInHierarchy), "after a reload the dug spot is still gone");
			int days = IslandRules.RegrowDays(e) + 1;
			IslandObjectState.Capture(e);
			foreach (ObjectState st in e.State.Values) st.Day -= days;
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			Check(ref ok, picked < 0 || PickupsOf(e, "DirtPickup").Any(p => (int)(p.ObjectIndex & 0xFFFF) == picked && p.gameObject.activeInHierarchy), days + " days later the dirt is back");
			List<string> again = ScOpenChest(e, "Wild beehive");
			Check(ref ok, again != null && again.Any(h => h.IndexOf("honeycomb", StringComparison.OrdinalIgnoreCase) >= 0), days + " days later the hive has honey again: " + (again != null ? string.Join(", ", again.ToArray()) : "nothing"));
			OnRaftCommand();
			IslandWorldState.Remove(DirtHoneyIsland);
			if (ok) Log("PASS: dirt and honey"); else Fail("dirt and honey");
		}
	}
}
