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
	/// <summary>Tests for another player hosting a world later (WorldCopy.cs); the flow is tools\mphostswap.ps1.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIWorldCopyInfo", docs: "Dev, in game (either player): where this world's custom islands were read from, its copy in Raft's world folder, the copy this PC keeps, and each island's local file")]
		public static void WorldCopyInfo()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world"); return; }
			string folder = WorldCopy.RaftWorldFolder;
			string travelled = folder != null ? Path.Combine(folder, WorldCopy.FileName) : null;
			string kept = IslandWorldState.WorldFilePath;
			Log("COPY world '" + SaveAndLoad.CurrentGameFileName + "' " + SaveAndLoad.WorldGuid + " (" + (Raft_Network.IsHost ? "host" : "player who joined") + "), read from: " + (WorldCopy.LastSource ?? "?"));
			Log("COPY Raft's world folder: " + (folder ?? "none") + ", copy there: " + (travelled != null && File.Exists(travelled) ? File.ReadAllLines(travelled).Length + " lines" : "none"));
			Log("COPY this PC's own: " + (File.Exists(kept) ? File.ReadAllLines(kept).Length + " lines" : "none") + (WorldCopy.LastKept != null ? ", last kept from the host: " + WorldCopy.LastKept : ""));
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.OrderBy(e => e.HostName))
				Log("COPY island " + e.HostName + " file " + e.Name + " hash " + (e.Hash ?? "-") + (e.Failed ? " (failed)" : e.WaitingForFile ? " (waiting for its file)" : ""));
			Log("COPY done");
		}

		/// <summary>
		/// Every kind of island the mod makes, for playing each with two players (tools\mpconfigs.ps1): each generator layout
		/// (the styles and both sea floors in turn, with animals, loot and the level up system on every other one), each
		/// ready-made map type, and a flying and a sunken island.
		/// </summary>
		static List<string> ConfigNames()
		{
			var list = new List<string>();
			for (int i = 0; i < IslandShapes.Names.Length; i++) list.Add("layout " + IslandShapes.Names[i]);
			foreach (MapType t in MapTypes.All) list.Add("type " + t.Name);
			list.Add("flying");
			list.Add("sunken");
			return list;
		}

		[ConsoleCommand(name: "CIConfigList", docs: "Dev: the island configurations tools\\mpconfigs.ps1 plays with two players (numbered)")]
		public static void ConfigList()
		{
			List<string> all = ConfigNames();
			for (int i = 0; i < all.Count; i++) Log("CONFIG " + i + " " + all[i]);
			Log("CONFIG count " + all.Count);
		}

		[ConsoleCommand(name: "CIConfigSpawn", docs: "Dev, in game (host): makes island configuration <n> (CIConfigList) as a file cicfg<n> and spawns it 170 m from the raft; logs its name. CIConfigSpawn <n>")]
		public static void ConfigSpawn(string[] args)
		{
			int n;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out n)) { Fail("CIConfigSpawn <n>"); return; }
			StartTest(ConfigSpawnRoutine(n));
		}

		static IEnumerator ConfigSpawnRoutine(int n)
		{
			List<string> all = ConfigNames();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost || n < 0 || n >= all.Count) { Fail("run in a world, as the host, with 0.." + (all.Count - 1)); yield break; }
			string what = all[n], name = "cicfg" + n;
			IslandFile f = null;
			try
			{
				if (what.StartsWith("type "))
				{
					MapType type = MapTypes.All.First(t => t.Name == what.Substring(5));
					float elevation;
					IslandGenSettings ms = MapTypes.Roll(type, new System.Random(4200 + n), out elevation);
					f = MapTypes.Create(type, ms, elevation, name);
				}
				else
				{
					var s = new IslandGenSettings { Seed = 4200 + n, Radius = 45f, Height = 18f, ObjectDensity = 0.5f };
					if (what.StartsWith("layout ")) { s.Shape = Array.IndexOf(IslandShapes.Names, what.Substring(7)); s.Style = n % TerrainPainter.Styles.Length; s.SeaFloor = n % 2; }
					s.Hostiles = n % 2 == 0 ? 2 : 0; s.Friendly = 1; s.SeaLife = 1; s.Loot = 3;
					f = IslandGenerator.CreateFile(s, name);
					if (what == "flying") f.Elevation = 60f;
					if (what == "sunken") f.Elevation = -30f;
				}
				f.Name = name;
				f.Save(IslandSpawner.PathFor(name));
			}
			catch (Exception e) { Fail("making configuration " + n + " (" + what + "): " + e); yield break; }
			IslandWorldState.Entry old = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == name);
			if (old != null) IslandWorldState.RemoveIds(new[] { old.Id }, true);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 170f);
			if (!spot.HasValue) { Fail("no open sea near the raft for " + name); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, new Vector3(spot.Value.x, f.Elevation, spot.Value.z), true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == name);
			if (entry == null || entry.Root == null) { Fail("configuration " + n + " (" + what + ") did not spawn"); yield break; }
			Log("PASS: configuration " + n + " '" + name + "' (" + what + ") spawned, " + f.Objects.Count + " objects, height " + f.Elevation);
		}

		[ConsoleCommand(name: "CIKeptCopy", docs: "Dev (a player who joined, any time): whether this PC keeps a copy of a world by its id: CIKeptCopy <world id>")]
		public static void KeptCopy(string[] args)
		{
			if (args == null || args.Length == 0) { Fail("CIKeptCopy <world id>"); return; }
			string path = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), args[0].Trim() + ".txt");
			if (File.Exists(path)) Log("KEPT " + args[0] + ": " + File.ReadAllLines(path).Length + " lines, " + File.ReadAllLines(path).Count(l => !l.StartsWith("@") && !l.StartsWith("#") && l.Contains("|")) + " island(s)");
			else Log("KEPT " + args[0] + ": none");
		}
	}
}
