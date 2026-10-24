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
		const string StoryPiecesIsland = "cistorypieces";

		[ConsoleCommand(name: "CIStoryPieces", docs: "Dev, in game (host): Raft's quest tools on a custom island - machete vines (not cut by hand, cut with the machete, which the player keeps; still cut after a reload) and Raft's zipline line (the zipline tool rides it)")]
		public static void StoryPiecesCommand(string[] args) { StartTest(StoryPiecesRoutine()); }

		static IEnumerator StoryPiecesRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			Check(ref ok, ContentCatalog.ItemExists(ContentCatalog.MacheteItem) && ContentCatalog.ItemExists(ContentCatalog.ZiplineItem), "Raft's items '" + ContentCatalog.MacheteItem + "' and '" + ContentCatalog.ZiplineItem + "' exist");
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { ContentCatalog.MacheteVines, "ZiplinePath_Landmark" });

			// A small island: vines by the middle, Raft's zipline line across it
			var s = new IslandGenSettings { Seed = 8282, Radius = 55f, Height = 18f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, StoryPiecesIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			Dictionary<string, string> vp = ObjectProps.Defaults(ContentCatalog.MacheteVines);
			vp[BehaviourProps.Name] = "vines";
			f.Objects.Add(new IslandObject { Name = ContentCatalog.MacheteVines, Position = new Vector3(c.x + 4f, ground(c.x + 4f, c.y), c.y), Props = vp });
			f.Objects.Add(new IslandObject { Name = "ZiplinePath_Landmark", Position = new Vector3(c.x - 6f, ground(c.x - 6f, c.y - 6f), c.y - 6f) });
			// A second line with its far end moved (zip.to): from near the top 35 m out towards the beach
			Vector3 top = new Vector3(c.x, ground(c.x, c.y) + 3f, c.y + 2f);
			Vector3 far = new Vector3(c.x + 35f, ground(c.x + 35f, c.y + 2f), c.y + 2f);
			f.Objects.Add(new IslandObject { Name = "ZiplinePath_Landmark", Position = top, EulerRotation = Vector3.zero,
				Props = new Dictionary<string, string> { { ZiplineEnds.ZipTo, ZiplineEnds.Text(far) }, { BehaviourProps.Name, "zip2" } } });
			IslandWorldState.Remove(StoryPiecesIsland);
			f.Save(IslandSpawner.PathFor(StoryPiecesIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(StoryPiecesIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(StoryPiecesIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == StoryPiecesIsland);
			if (e == null || e.Root == null) { Fail(StoryPiecesIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);

			// Vines: not without the machete; with it they go, and the machete stays the player's
			PlayerInventory inv = player.Inventory;
			inv.RemoveItem(ContentCatalog.MacheteItem, 99);
			IslandObjectRef vines = ScObjOf(e, "vines");
			Check(ref ok, vines != null, "the vines are on the island");
			if (vines != null)
			{
				ScUse(e, "vines");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, vines.gameObject.activeInHierarchy, "without a machete the vines stay");
				inv.AddItem(ContentCatalog.MacheteItem, 1);
				ScUse(e, "vines");
				yield return new WaitForSeconds(0.8f);
				Check(ref ok, !vines.gameObject.activeInHierarchy, "with the machete they are cut away");
				Check(ref ok, inv.GetItemCount(ContentCatalog.MacheteItem) == 1, "the player keeps the machete for the next vines (" + inv.GetItemCount(ContentCatalog.MacheteItem) + ")");
			}

			// The zipline line: Raft's own, ridden with the zipline tool
			MeshPath_Zipline line = e.Root.GetComponentsInChildren<MeshPath_Zipline>(true).FirstOrDefault();
			Check(ref ok, line != null, "Raft's zipline line is on the island with its own script");
			if (line != null)
			{
				inv.AddItem(ContentCatalog.ZiplineItem, 1);
				ZiplinePlayer zp = player.GetComponentInChildren<ZiplinePlayer>(true);
				PutPlayerNear(line.transform, 1f);
				yield return new WaitForSeconds(0.5f);
				Vector3 before = player.transform.position;
				if (zp != null) zp.AttachToZipline(line, 0);
				yield return new WaitForSeconds(2f);
				Vector3 after = player.transform.position;
				const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
				string state = zp == null ? "no ZiplinePlayer" : string.Join(", ", zp.GetType().GetFields(all).Where(x => x.FieldType == typeof(bool) || x.FieldType == typeof(MeshPath_Zipline)).Select(x => x.Name + "=" + x.GetValue(zp)).ToArray());
				Check(ref ok, zp != null && Vector3.Distance(before, after) > 2f, "riding the line moves the player " + Vector3.Distance(before, after).ToString("F1") + " m in 2 s (" + state + ")");
				try { zp.DetachFromCurrentZipline(); } catch { }
			}
			// The moved line: its far end where zip.to says, and ridden there
			MeshPath_Zipline moved = e.Root.GetComponentsInChildren<MeshPath_Zipline>(true).Skip(1).FirstOrDefault();
			if (moved != null)
			{
				Vector3 farWorld = e.Root.transform.position + far;
				FieldInfo pa = typeof(MeshPath_Zipline_Landmark).GetField("pointA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				Transform a = pa.GetValue(moved) as Transform;
				float off = a != null ? Vector3.Distance(new Vector3(a.position.x, 0, a.position.z), new Vector3(farWorld.x, 0, farWorld.z)) : 99f;
				Check(ref ok, off < 1f, "the moved line's far end is where zip.to says (" + off.ToString("F2") + " m off)");
				ZiplinePlayer zp2 = player.GetComponentInChildren<ZiplinePlayer>(true);
				try { zp2.DetachFromCurrentZipline(); } catch { }
				yield return new WaitForSeconds(0.5f);
				PutPlayerNear(moved.transform, 1f);
				yield return new WaitForSeconds(0.5f);
				Vector3 b0 = player.transform.position;
				zp2.AttachToZipline(moved, 0);
				yield return new WaitForSeconds(5f);
				Vector3 b1 = player.transform.position;
				Check(ref ok, Vector3.Distance(b0, b1) > 15f && Vector3.Distance(b1, farWorld) < Vector3.Distance(b0, farWorld) - 15f, "riding it carries the player " + Vector3.Distance(b0, b1).ToString("F1") + " m towards the far end (" + Vector3.Distance(b1, farWorld).ToString("F1") + " m left)");
				try { zp2.DetachFromCurrentZipline(); } catch { }
			}
			else Check(ref ok, false, "the second line is there");
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			IslandObjectRef again = ScObjOf(e, "vines");
			Check(ref ok, again != null && !again.gameObject.activeInHierarchy, "after a reload the vines stay cut");
			IslandWorldState.Remove(StoryPiecesIsland);
			if (ok) Log("PASS: story pieces"); else Fail("story pieces");
		}
	}
}
