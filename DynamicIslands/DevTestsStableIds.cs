using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIStableIds", docs: "Dev: R1b object numbers - the file's uid block (left out when plain, kept through an edit, dropped when it no longer fits), numbers for new and copied objects, a world's state carried to the same objects after objects were removed")]
		public static void StableIdsTest()
		{
			bool ok = true;
			string tmp = Path.Combine(DynamicIslands.assetpath, "ci_stableids.island");
			try
			{
				if (!PlaceableCatalog.IsBuilt) { Fail("stable ids: the object catalog isn't loaded yet"); return; }
				// (an object that brings pickups, as a tree does)
				string tree = PlaceableCatalog.LoadedNames.Where(n => !ContentCatalog.IsCreature(n) && !ContentCatalog.IsZone(n) && !ContentCatalog.IsHelper(n) && !ContentCatalog.IsTreasure(n))
					.OrderBy(n => n).FirstOrDefault(n => StableIds.PickupCount(n) > 0);
				if (tree == null) { Fail("stable ids: no loaded object with pickups"); return; }
				int k = StableIds.PickupCount(tree);
				Log("tree: " + tree + " (" + k + " pickups)");

				Func<string, float, IslandObject> obj = (n, x) => new IslandObject { Name = n, Position = new Vector3(x, 5, 0), EulerRotation = Vector3.zero, Scale = Vector3.one };
				var f1 = new IslandFile { Name = "ci_stableids", TerrainSize = new Vector3(10, 10, 10), HeightmapResolution = 33, Heights = new float[33, 33] };
				f1.Objects.AddRange(new[] { obj(tree, 0), obj("Creature_Boar", 1), obj("Loot_Chest", 2), obj(tree, 3), obj("Loot_Chest", 4), obj(ContentCatalog.TriggerZone, 5), obj(ContentCatalog.BuriedTreasure, 6), obj("Creature_Boar", 7) });
				f1.Save(tmp);
				IslandFile a = IslandFile.Load(tmp);
				Check(ref ok, !a.Tail.ContainsKey(StableIds.Tag) && !StableIds.HasOwn(a) && a.Objects.Select(o => o.Uid).SequenceEqual(Enumerable.Range(1, 8)) && a.NextUid == 9,
					"a file numbered 1, 2, 3... has no block (" + string.Join(",", a.Objects.Select(o => o.Uid.ToString()).ToArray()) + ", next " + a.NextUid + ")");

				// The edit: the first tree and the first chest removed, that chest put back at the end where it stood, a new chest
				IslandFile b = IslandFile.Load(tmp);
				b.Objects.RemoveAt(2);
				b.Objects.RemoveAt(0);
				b.Objects.Add(obj("Loot_Chest", 2));
				b.Objects.Add(obj("Loot_Chest", 9));
				StableIds.Prepare(b, a);
				Check(ref ok, b.Objects.Select(o => o.Uid).SequenceEqual(new[] { 2, 4, 5, 6, 7, 8, 3, 9 }) && b.NextUid == 10,
					"numbers kept, the chest put back gets its own, the new one the next (" + string.Join(",", b.Objects.Select(o => o.Uid.ToString()).ToArray()) + ", next " + b.NextUid + ")");
				b.Save(tmp);
				IslandFile c = IslandFile.Load(tmp);
				Check(ref ok, StableIds.HasOwn(c) && c.Objects.Select(o => o.Uid).SequenceEqual(new[] { 2, 4, 5, 6, 7, 8, 3, 9 }) && c.NextUid == 10, "the numbers load back from the block");

				// A world's state moved with its objects
				Func<bool, ObjectState> st = active => new ObjectState { Active = active, Yield = -1, Day = 3 };
				var was = new Dictionary<int, ObjectState>
				{
					{ 1, st(false) },                                  // the removed tree's pickup: dropped
					{ k + 1, st(false) },                              // the second tree's first pickup -> 1
					{ CreatureSpawner.StateKeyBase + 1, st(false) },   // the second boar -> still 1
					{ ContentState.LootKeyBase, st(false) },           // the chest put back -> loot 1
					{ ContentState.LootKeyBase + 1, st(true) },        // the second chest -> loot 0
					{ TriggerZone.KeyBase, st(false) },
					{ 0xC001, st(false) },                             // the treasure
					{ Behaviours.StateBase, st(false) },               // the removed tree's: dropped
					{ Behaviours.StateBase + 4, st(true) },            // the second chest's -> 2
					{ Behaviours.StateBase + Behaviours.IslandIndex, st(true) }, // the island's own: kept
					{ 0x40000, st(true) },                             // a quest: kept
					{ Behaviours.PendingBase + (4 << 4) + 2, st(true) }, // the second chest's -> place 2
				};
				int dropped;
				string unsure;
				string moved = StableIds.Carry(a, c, IslandObjectState.Encode(was), false, out dropped, out unsure);
				var expect = new Dictionary<int, ObjectState>
				{
					{ 1, was[k + 1] }, { CreatureSpawner.StateKeyBase + 1, was[CreatureSpawner.StateKeyBase + 1] },
					{ ContentState.LootKeyBase + 1, was[ContentState.LootKeyBase] }, { ContentState.LootKeyBase, was[ContentState.LootKeyBase + 1] },
					{ TriggerZone.KeyBase, was[TriggerZone.KeyBase] }, { 0xC001, was[0xC001] }, { Behaviours.StateBase + 2, was[Behaviours.StateBase + 4] },
					{ Behaviours.StateBase + Behaviours.IslandIndex, was[Behaviours.StateBase + Behaviours.IslandIndex] }, { 0x40000, was[0x40000] },
					{ Behaviours.PendingBase + (2 << 4) + 2, was[Behaviours.PendingBase + (4 << 4) + 2] },
				};
				Check(ref ok, moved != null && moved == IslandObjectState.Encode(expect) && dropped == 2,
					"a world's state follows its objects (dropped " + dropped + (unsure != null ? ", unsure: " + unsure : "") + ")\n    got    " + moved + "\n    wanted " + IslandObjectState.Encode(expect));

				// An older version of the mod saved it: the block no longer fits (an object removed) - numbered by place
				IslandFile d = IslandFile.Load(tmp);
				d.Objects.RemoveAt(0);
				StableIds.ReadTail(d);
				Check(ref ok, !d.Tail.ContainsKey(StableIds.Tag) && d.Objects.Select(o => o.Uid).SequenceEqual(Enumerable.Range(1, 7)) && d.NextUid == 8, "a block that doesn't fit is dropped: numbered by place");

				// A copy (the same number twice) and an object without one get new numbers, never a used one
				IslandFile e = IslandFile.Load(tmp);
				e.Objects[1].Uid = e.Objects[0].Uid;
				e.Objects.Add(obj("Creature_Boar", 11));
				StableIds.Assign(e);
				Check(ref ok, e.Objects[1].Uid == 10 && e.Objects[8].Uid == 11 && e.NextUid == 12 && e.Objects.Select(o => o.Uid).Distinct().Count() == 9, "a copy and a new object get the next numbers");

				// The pickups counted as Raft numbers them in a world
				Check(ref ok, StableIds.LayoutOf(c, false).Pickups.Count - 1 == k && StableIds.LayoutOf(a, false).Pickups.Count - 1 == 2 * k, "the pickups are counted per object");
			}
			catch (System.Exception ex) { Check(ref ok, false, "threw " + ex); }
			finally { try { File.Delete(tmp); } catch { } }
			if (ok) Log("PASS: stable ids (R1b)"); else Fail("stable ids (R1b)");
		}

		[ConsoleCommand(name: "CIMeasurePickups", docs: "Dev, editor: how many of Raft's pickups (trees, rocks, items) each object brings - the core list and the index's things to gather: raft_pickups.txt in Mods\\DynamicIslands; copy it into the mod's DynamicIslands folder (R2b)")]
		public static void MeasurePickups()
		{
			StartTest(MeasurePickupsRoutine());
		}

		static IEnumerator MeasurePickupsRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("measure pickups: in the editor"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			yield return PlaceableCatalog.EnsureIndex();
			yield return PlaceableCatalog.EnsureLoaded(PlaceableCatalog.IndexedGatherNames());
			var lines = new List<string>
			{
				"# CustomIslands pickups per object raft=" + Application.version,
				"# Raft's objects that bring pickups (trees, rocks, items): name, how many. An object a Raft update removes keeps",
				"# that many numbers in a world, so the pickups after it keep their saved state (ROADMAP R2b). Made by CIMeasurePickups.",
			};
			int objects = 0, pickups = 0;
			foreach (string n in PlaceableCatalog.LoadedNames.OrderBy(x => x, StringComparer.Ordinal).ToList())
			{
				int k = StableIds.PickupCount(n);
				if (k <= 0) continue;
				lines.Add(n + "\t" + k);
				objects++;
				pickups += k;
			}
			string path = Path.Combine(DynamicIslands.assetpath, StableIds.PickupsFile);
			File.WriteAllLines(path, lines.ToArray());
			if (objects > 0) Log("PASS: measured pickups: " + objects + " objects bring " + pickups + " - " + path);
			else Fail("measure pickups: no object with pickups");
		}

		[ConsoleCommand(name: "CIPickupGap", docs: "Dev, world (host): R2b - an object this Raft no longer has keeps its pickups' numbers: the tree after it keeps its own, the count from its file matches the world, and raft_pickups.txt lists the tree")]
		public static void PickupGapTest()
		{
			StartTest(PickupGapRoutine());
		}

		static IEnumerator PickupGapRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || DynamicIslands.InEditor()) { Fail("pickup gap: in a world"); yield break; }
			// (a world builds the catalog when its first island loads - a fresh world may have none yet)
			yield return PlaceableCatalog.EnsureBuilt();
			PickupGapCheck();
		}

		static void PickupGapCheck()
		{
			bool ok = true;
			const string gone = "CI_GoneTree";
			GameObject root = null;
			try
			{
				if (!PlaceableCatalog.IsBuilt) { Fail("pickup gap: the object catalog didn't build"); return; }
				string tree = PlaceableCatalog.LoadedNames.Where(n => PlaceableCatalog.IsHarvestable(n) && !ContentCatalog.IsCreature(n))
					.OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault(n => StableIds.PickupCount(n) > 0);
				if (tree == null) { Fail("pickup gap: no loaded object with pickups"); return; }
				int k = StableIds.PickupCount(tree), g = 3;
				Log("tree: " + tree + " (" + k + " pickups), the gone one had " + g);
				Check(ref ok, StableIds.TestPickups.Count == 0 && StableIds.ShippedPickups(tree) == k, "raft_pickups.txt lists the tree with " + k + " (" + StableIds.ShippedPickups(tree) + ")");
				StableIds.TestPickups[gone] = g;

				Func<string, float, IslandObject> obj = (n, x) => new IslandObject { Name = n, Position = new Vector3(x, 3, 0), EulerRotation = Vector3.zero, Scale = Vector3.one };
				var f = new IslandFile { Name = "ci_pickupgap", TerrainSize = new Vector3(64, 40, 64), HeightmapResolution = 33, Heights = new float[33, 33], WaterLevel = 0 };
				f.Objects.AddRange(new[] { obj(tree, -6), obj(gone, 0), obj(tree, 6) });
				StableIds.Layout l = StableIds.LayoutOf(f, false);
				Check(ref ok, l.Unsure == null && l.Pickups.Count - 1 == 2 * k + g, "the file counts the gone object's pickups (" + (l.Pickups.Count - 1) + ", wanted " + (2 * k + g) + (l.Unsure != null ? ", unsure: " + l.Unsure : "") + ")");

				Vector3 at = (RAPI.GetLocalPlayer() != null ? RAPI.GetLocalPlayer().transform.position : Vector3.zero) + new Vector3(3000, 0, 3000);
				root = IslandSpawner.SpawnInWorld(f, new Vector3(at.x, 0, at.z));
				IslandSpawner.RegisterNetworkIds(root, 0x3FFE);
				PickupItem_Networked[] all = root.GetComponentsInChildren<PickupItem_Networked>(true);
				Transform[] trees = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == tree).ToArray();
				Func<Transform, string> ords = t => string.Join(",", t.GetComponentsInChildren<PickupItem_Networked>(true).Select(p => (p.ObjectIndex & 0xFFFF).ToString()).ToArray());
				string want1 = string.Join(",", Enumerable.Range(1, k).Select(i => i.ToString()).ToArray()), want2 = string.Join(",", Enumerable.Range(k + g + 1, k).Select(i => i.ToString()).ToArray());
				Check(ref ok, all.Length == 2 * k && trees.Length == 2, "the world has both trees' pickups (" + all.Length + ")");
				Check(ref ok, trees.Length == 2 && ords(trees[0]) == want1 && ords(trees[1]) == want2,
					"the tree after the gone object keeps its numbers: " + (trees.Length == 2 ? ords(trees[0]) + " | " + ords(trees[1]) : "?") + " (wanted " + want1 + " | " + want2 + ")");
				Check(ref ok, StableIds.CheckPickups(f, root, false), "the count from the file matches the world");
			}
			catch (Exception ex) { Check(ref ok, false, "threw " + ex); }
			finally
			{
				StableIds.TestPickups.Remove(gone);
				if (root != null) IslandSpawner.Despawn(root);
			}
			if (ok) Log("PASS: pickup gap (R2b)"); else Fail("pickup gap (R2b)");
		}

		[ConsoleCommand(name: "CIStableIdsSave",docs: "Dev, editor: R1b end to end - an island a saved world used, an object before a looted chest and a picked tree removed in the editor and saved: the world plays the new file with what was used still on the same objects")]
		public static void StableIdsSaveTest()
		{
			StartTest(StableIdsSaveRoutine());
		}

		static IEnumerator StableIdsSaveRoutine()
		{
			bool ok = true;
			const string isl = "citest-uid";
			string folder = Path.Combine(DynamicIslands.assetpath, "worlds");
			Directory.CreateDirectory(folder);
			string world = Path.Combine(folder, "citest-uid-world.txt");
			var leftovers = new List<string> { IslandSpawner.PathFor(isl), world };
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("stable ids save: in the editor"); yield break; }
			string tree = PlaceableCatalog.LoadedNames.Where(n => !ContentCatalog.IsCreature(n) && !ContentCatalog.IsZone(n) && !ContentCatalog.IsHelper(n) && !ContentCatalog.IsTreasure(n))
				.OrderBy(n => n).FirstOrDefault(n => StableIds.PickupCount(n) > 0);
			int k = tree == null ? 0 : StableIds.PickupCount(tree);
			string h0 = null;
			try
			{
				var f = new IslandFile { Name = isl, TerrainSize = new Vector3(64, 40, 64), HeightmapResolution = 33, Heights = new float[33, 33], WaterLevel = 0 };
				Func<string, float, IslandObject> obj = (n, x) => new IslandObject { Name = n, Position = new Vector3(x, 3, 0), EulerRotation = Vector3.zero, Scale = Vector3.one };
				f.Objects.AddRange(new[] { obj(tree, -6), obj("Loot_Chest", -3), obj(tree, 0), obj("Loot_Chest", 3) });
				f.Save(IslandSpawner.PathFor(isl));
				h0 = IslandNetwork.HashOf(isl);
				// (the second tree picked, the second chest looted, the second tree's object state)
				string state = (k + 1) + ",0,-1,2;" + (ContentState.LootKeyBase + 1) + ",0,-1,2;" + (Behaviours.StateBase + 2) + ",1,-1,2";
				File.WriteAllLines(world, new[] { "# Custom islands in world 'CI StableIds': ...", isl + "|0|0|0|" + state + "|||" + h0 });
			}
			catch (Exception e) { Fail("stable ids save: setting up - " + e.Message); yield break; }
			if (tree == null) { Fail("stable ids save: no loaded object with pickups"); yield break; }

			Check(ref ok, DynamicIslands.LoadIsland(isl), "the island opens");
			yield return new WaitForSecondsRealtime(1f);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			List<EditorGameObject> objs = placed.GetComponentsInChildren<EditorGameObject>().ToList();
			Check(ref ok, objs.Select(e => e.Uid).OrderBy(u => u).SequenceEqual(new[] { 1, 2, 3, 4 }), "the objects have their numbers in the editor (" + string.Join(",", objs.Select(e => e.Uid.ToString()).ToArray()) + ")");
			EditorGameObject first = objs.FirstOrDefault(e => e.Uid == 1);
			if (first != null) UnityEngine.Object.Destroy(first.gameObject);
			yield return null;
			Check(ref ok, DynamicIslands.SaveIsland(isl), "saved without the first tree");
			string h1 = IslandNetwork.HashOf(isl);
			string[] line = File.ReadAllLines(world).Where(l => l.StartsWith(isl + "|")).Select(l => l.Split('|')).FirstOrDefault();
			string want = "1,0,-1,2;" + (ContentState.LootKeyBase + 1) + ",0,-1,2;" + (Behaviours.StateBase + 1) + ",1,-1,2";
			Check(ref ok, line != null && line.Length >= 8 && line[7] == h1 && h1 != h0, "the world plays the new file (" + (line != null && line.Length >= 8 ? line[7] : "?") + ", now " + h1 + ")");
			Check(ref ok, line != null && line.Length >= 8 && line[4] == want, "what was used stays on the same objects: " + (line != null && line.Length >= 8 ? line[4] : "?") + " (wanted " + want + ")");
			string copy = Path.Combine(Path.Combine(Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), LibraryPack.KeptVersionsFolder), IslandNetwork.DownloadName(isl, h0) + Path.GetExtension(IslandSpawner.PathFor(isl)));
			Check(ref ok, !File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(isl, h0))), "no copy of the old version is left for it");
			leftovers.Add(copy);
			leftovers.Add(IslandSpawner.PathFor(IslandNetwork.DownloadName(isl, h0)));

			IslandFile back = IslandFile.Load(IslandSpawner.PathFor(isl));
			Check(ref ok, back.Objects.Select(o => o.Uid).SequenceEqual(new[] { 2, 3, 4 }) && back.NextUid == 5, "the file keeps the numbers (" + string.Join(",", back.Objects.Select(o => o.Uid.ToString()).ToArray()) + ", next " + back.NextUid + ")");

			DynamicIslands.NewIsland();
			yield return null;
			foreach (string p in leftovers) try { if (File.Exists(p)) File.Delete(p); } catch { }
			if (ok) Log("PASS: stable ids save (R1b)"); else Fail("stable ids save (R1b)");
		}
	}
}
