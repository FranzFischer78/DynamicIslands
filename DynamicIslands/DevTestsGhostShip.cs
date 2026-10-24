using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Tests of the world option Ghost ship (GhostShip, TODO 4f).</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIGhostShip", docs: "Dev, world (host, 'CI ...'): the world option Ghost ship - nights rolled (about 1 in 4, the same for a seed); with it off nightfall brings none; asleep at nightfall none comes; forced tonight one comes beside the raft (within swimming reach, clear of the raft), with chests of good loot, the captain's note, its lights, and ghost monsters (see-through, glowing); at dawn it sinks and is gone with its chests and monsters, its file deleted. CIGhostShip [keep] (keep: stops before dawn, the ship stays - for pictures and CIGroundWorld ghostship) | CIGhostShip shots (pictures of the ship out: shot_ghostship_*.png)")]
		public static void GhostShipTestCommand(string[] args)
		{
			string a = args != null && args.Length > 0 ? args[0] : "";
			StartTest(a == "shots" ? GhostShipShotsRoutine() : GhostShipRoutine(a == "keep"));
		}

		static IEnumerator GhostShipRoutine(bool keep)
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("ghost ship: host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			Network_Player me = RAPI.GetLocalPlayer();
			GhostShip.Brought = 0;
			try
			{
				// The nights
				int seed = WorldOptions.Seed, n = Enumerable.Range(0, 4000).Count(d => GhostShip.Rolls(seed, d));
				Check(ref ok, n >= 880 && n <= 1120, "nights with a ship: " + n + " of 4000 (about 1 in 4)");
				Check(ref ok, Enumerable.Range(0, 200).All(d => GhostShip.Rolls(seed, d) == GhostShip.Rolls(seed, d)) && Enumerable.Range(0, 200).Any(d => GhostShip.Rolls(seed, d) != GhostShip.Rolls(seed + 1, d)), "the same nights for a seed, others for another seed");
				IslandWorldState.Entry old = GhostShip.Ship;
				if (old != null) GhostShip.Remove(old, "the test");

				// Off: nightfall brings none
				WorldOptions.Set(optionsBefore.Where(o => o != WorldOptions.GhostShip));
				yield return GhostNightfall(true);
				for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(2.1f); GhostShip.Tick(); }
				Check(ref ok, GhostShip.Ship == null && GhostShip.Brought == 0, "the option off: nightfall brings no ship");

				// On, asleep at nightfall: none
				WorldOptions.Set(optionsBefore.Concat(new[] { WorldOptions.GhostShip }));
				GhostShip.TestSleeping = true;
				yield return GhostNightfall(true);
				for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(2.1f); GhostShip.Tick(); }
				Check(ref ok, GhostShip.Ship == null && GhostShip.LastDecision == "the players slept", "asleep at nightfall: no ship (" + GhostShip.LastDecision + ")");
				GhostShip.TestSleeping = false;

				// On, forced tonight: it comes
				yield return GhostNightfall(true);
				IslandWorldState.Entry e = null;
				for (float t = 0f; t < 40f && (e == null || e.Root == null || e.Loading); t += 0.5f) { yield return new WaitForSeconds(0.5f); KeepAlive(me); e = GhostShip.Ship; }
				// (none: a test world crowded by the earlier suites' islands - RUN1's gap phase 2026-10-21: Bring() said "No room near the
				// raft" for 50 s; where the ship finds room isn't what this checks, so a new nightfall with any spot, as CIPoolAgain does)
				if (e == null)
				{
					MapType gt = MapTypes.Get(GhostShip.TypeName);
					float gel;
					IslandGenSettings gs = gt != null ? MapTypes.Roll(gt, new System.Random(), out gel) : null;
					if (gs != null && GhostShip.Spot(CustomIslandSpawner.RaftPosition.Value, gs.Radius) == null)
					{
						Log("  (no room near the raft in the crowded test sea - a new nightfall with any spot clear of the raft)");
						CustomIslandSpawner.TestAllRound = true;
						yield return GhostNightfall(true);
						for (float t = 0f; t < 40f && (e == null || e.Root == null || e.Loading); t += 0.5f) { yield return new WaitForSeconds(0.5f); KeepAlive(me); e = GhostShip.Ship; }
					}
				}
				Check(ref ok, e != null && e.Root != null, "nightfall brought a ghost ship (" + (e != null ? e.HostName : "none") + ", " + GhostShip.LastDecision + ")");
				if (e == null || e.Root == null) yield break;
				Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
				float away = Vector3.Distance(new Vector3(e.Position.x, 0f, e.Position.z), new Vector3(raft.x, 0f, raft.z));
				Check(ref ok, away > 20f + CustomIslandSpawner.RaftRadius && away < 220f, "beside the raft: " + away.ToString("F0") + " m away (raft reach " + CustomIslandSpawner.RaftRadius.ToString("F0") + " m)");
				List<IslandObjectRef> objs = e.Root.GetComponentsInChildren<IslandObjectRef>(true).ToList();
				List<IslandObjectRef> chests = objs.Where(o => o.ObjectName.StartsWith("Loot_Chest") && ObjectProps.Get(o.Props, ObjectProps.LootItems).Length > 0).ToList();
				Check(ref ok, chests.Count >= 3 && chests.Any(c => ObjectProps.Get(c.Props, ObjectProps.LootItems).IndexOf("TitaniumIngot", StringComparison.Ordinal) >= 0 || ObjectProps.Get(c.Props, ObjectProps.LootItems).IndexOf("CircuitBoard", StringComparison.Ordinal) >= 0 || ObjectProps.Get(c.Props, ObjectProps.LootItems).IndexOf("Battery", StringComparison.Ordinal) >= 0),
					"its chests: " + chests.Count + " with loot (" + string.Join(" | ", chests.Select(c => ObjectProps.Get(c.Props, ObjectProps.LootItems)).ToArray()) + ")");
				Check(ref ok, objs.Any(o => o.ObjectName == "Note_Paper" && ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Captain's log"), "the captain's log is aboard");
				// (the deck's pieces by their names: only objects with settings carry an IslandObjectRef)
				List<string> parts = e.Root.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToList();
				int deck = parts.Count(pn => pn.StartsWith("Block_Foundation", StringComparison.Ordinal));
				Check(ref ok, deck >= 60 && parts.Any(pn => pn.StartsWith("BoatLandmark_Flag", StringComparison.Ordinal)) && parts.Any(pn => pn.StartsWith("Placeable_Sail", StringComparison.Ordinal)), "its deck, mast and sail (" + deck + " foundations)");
				// Its ghosts
				List<AI_NetworkBehaviour> ghosts = null;
				CreatureSpawnPoint[] spots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true);
				for (float t = 0f; t < 40f; t += 1f)
				{
					yield return new WaitForSeconds(1f);
					KeepAlive(me);
					GhostShip.Tick();
					ghosts = spots.SelectMany(p => p.Spawned).Where(a => a != null).ToList();
					if (ghosts.Count >= 6 && ghosts.All(a => GhostShip.IsGhost(a.gameObject))) break;
				}
				int looks = ghosts.Count(a => GhostShip.IsGhost(a.gameObject) && a.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).All(r => r.sharedMaterials.All(m => m != null && m.shader != null && (m.shader.name.IndexOf("Additive", StringComparison.Ordinal) >= 0 || m.shader.name == "Sprites/Default"))));
				Check(ref ok, ghosts.Count >= 6 && looks == ghosts.Count, "its crew are ghosts: " + looks + " of " + ghosts.Count + " see-through and glowing (" + string.Join(", ", ghosts.Select(a => a.behaviourType.ToString()).Distinct().ToArray()) + ")");
				Check(ref ok, e.Root.GetComponentsInChildren<Light>(true).Count(l => l.color.g > 0.9f && l.transform.parent != null && l.transform.parent.name == "GhostShipGlow") >= 3, "it glows (its lights)");
				Log("GHOSTSHIP " + e.HostName + " at " + e.Position);
				if (keep) { Log(ok ? "PASS: ghost ship (kept)" : "ghost ship"); if (!ok) Fail("ghost ship"); yield break; }

				// Dawn: it sinks, then it is gone with its chests and monsters
				string path = IslandSpawner.PathFor(e.HostName);
				GameObject root = e.Root;
				float y0 = root.transform.position.y;
				GhostShip.TestNight = false;
				GhostShip.Tick();
				yield return new WaitForSeconds(GhostShip.SinkSeconds * 0.6f);
				Check(ref ok, root != null && root.transform.position.y < y0 - 2f, "at dawn it sinks (" + (root != null ? (y0 - root.transform.position.y).ToString("F1") + " m down" : "gone already") + ")");
				for (float t = 0f; t < GhostShip.SinkSeconds + 10f && GhostShip.Ship != null; t += 0.5f) { yield return new WaitForSeconds(0.5f); KeepAlive(me); }
				yield return new WaitForSeconds(1f);
				Check(ref ok, GhostShip.Ship == null && root == null && !IslandWorldState.Islands.Any(x => x.Id == e.Id), "then it is gone");
				Check(ref ok, ghosts.All(a => a == null || a.gameObject == null || !a.gameObject.activeInHierarchy), "its ghosts are gone with it (" + ghosts.Count(a => a != null && a.gameObject != null && a.gameObject.activeInHierarchy) + " left)");
				Check(ref ok, !File.Exists(path), "its file is deleted");

				// A ship out when the players go to sleep: gone at once
				yield return GhostNightfall(true);
				for (float t = 0f; t < 40f && (GhostShip.Ship == null || GhostShip.Ship.Root == null); t += 0.5f) yield return new WaitForSeconds(0.5f);
				IslandWorldState.Entry e2 = GhostShip.Ship;
				GhostShip.TestSleeping = true;
				GhostShip.Tick();
				for (float t = 0f; t < 5f && GhostShip.Ship != null; t += 0.5f) { yield return new WaitForSeconds(0.5f); GhostShip.Tick(); }
				Check(ref ok, e2 != null && GhostShip.Ship == null, "a ship out when the players sleep: gone at once");
			}
			finally
			{
				CustomIslandSpawner.TestAllRound = false;
				if (!keep) { IslandWorldState.Entry left = GhostShip.Ship; if (left != null) GhostShip.Remove(left, "the test's end"); WorldOptions.Set(optionsBefore); GhostShip.Reset(); }
				else { GhostShip.TestSleeping = null; GhostShip.TestForce = false; }
			}
			if (ok) Log("PASS: ghost ship"); else Fail("ghost ship");
		}

		/// <summary>A nightfall as the ship sees it (day, then night), forced to bring one when force is set.</summary>
		static IEnumerator GhostNightfall(bool force)
		{
			GhostShip.TestNight = false;
			yield return new WaitForSeconds(2.1f);
			GhostShip.Tick();
			GhostShip.TestForce = force && WorldOptions.On(WorldOptions.GhostShip);
			GhostShip.TestNight = true;
			yield return new WaitForSeconds(2.1f);
			GhostShip.Tick();
		}

		/// <summary>Pictures of the ship out (CIGhostShip keep first): from the raft, and of a ghost up close.</summary>
		static IEnumerator GhostShipShotsRoutine()
		{
			IslandWorldState.Entry e = GhostShip.Ship;
			Network_Player player = RAPI.GetLocalPlayer();
			if (e == null || e.Root == null || player == null) { Fail("ghost ship shots: a ship out first (CIGhostShip keep)"); yield break; }
			AzureSkyHour(23f);
			yield return new WaitForSeconds(2f);
			Vector3 ship = e.Root.GetComponentsInChildren<Transform>(true).Where(o => o.name.StartsWith("BoatLandmark_Flag", StringComparison.Ordinal)).Select(o => o.position).DefaultIfEmpty(e.Position).First();
			Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
			Vector3 dir = new Vector3(ship.x - raft.x, 0f, ship.z - raft.z).normalized;
			// On its deck at the near end, looking along it (from the water a shark came for the camera and the ship was too dark)
			Vector3 centre = new Vector3(e.Position.x, 0f, e.Position.z);
			Vector3 eye = centre - dir * 12f + Vector3.up * 30f;
			RaycastHit hit;
			eye.y = Physics.Raycast(eye, Vector3.down, out hit, 40f) && hit.point.y > -1f ? hit.point.y + 1.8f : 3f;
			PlayerMove.To(player, eye, ControllerType.Ground);
			yield return new WaitForSeconds(1f);
			float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
			Look(player, yaw, 8f);
			yield return new WaitForSeconds(2f);
			Screenshot(new[] { "ghostship_ship" });
			yield return new WaitForSeconds(1f);
			// A ghost up close
			AI_NetworkBehaviour ghost = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).SelectMany(p => p.Spawned).FirstOrDefault(a => a != null && a.gameObject.activeInHierarchy && GhostShip.IsGhost(a.gameObject));
			if (ghost != null)
			{
				Vector3 g = ghost.transform.position;
				Vector3 from = g - dir * 4f + Vector3.up * 1.2f;
				PlayerMove.To(player, from, ControllerType.Ground);
				yield return new WaitForSeconds(0.5f);
				Vector3 to = ghost.transform.position - from;
				Look(player, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 25f);
				yield return new WaitForSeconds(0.6f);
				Screenshot(new[] { "ghostship_ghost" });
				yield return new WaitForSeconds(1f);
			}
			// Back to the raft
			PlayerMove.To(player, raft + Vector3.up * 2f, ControllerType.Ground);
			Log("PASS: ghost ship shots");
		}
	}
}
