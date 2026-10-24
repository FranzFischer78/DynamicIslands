using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>The mod's laser beam (LaserBeam.cs): an emitter, a turning mirror and a target panel; and the action "character".</summary>
	public static partial class DevTests
	{
		const string LaserIsland = "cilaser";

		[ConsoleCommand(name: "CILaser", docs: "Dev, in game (host): the laser beam - an emitter's beam goes by a mirror to a target panel (event laser, signal laser); the mirror turned, the beam misses it; the action character finds Raft's characters by name and number (never unlocks)")]
		public static void LaserCommand(string[] args) { StartTest(LaserRoutine()); }

		static Vector3 LaserCentre(Transform t)
		{
			Renderer[] rs = t.GetComponentsInChildren<Renderer>().Where(x => !(x is LineRenderer)).ToArray();
			if (rs.Length == 0) return t.position;
			Bounds b = rs[0].bounds;
			foreach (Renderer x in rs) b.Encapsulate(x.bounds);
			return b.center;
		}

		static string LaserPath(LaserBeam b)
		{
			return b.Points.Count + " points " + string.Join(" -> ", b.Points.Select(p => p.ToString("F1")).ToArray()) + ", mirrors " + b.Mirrors.Count + ", ends on " + (b.Hit != null ? b.Hit.Name : "nothing") + (b.HitCollider != null ? " (collider " + b.HitCollider.name + ")" : "");
		}

		static void LaserMoveTo(Transform t, Vector3 centre)
		{
			t.position += centre - LaserCentre(t);
			// (a mover poses itself from where it was placed: moved here, it has to start from here too)
			IslandBehaviour mover = t.GetComponent<IslandBehaviour>();
			if (mover != null) HarmonyLib.Traverse.Create(mover).Field("startPos").SetValue(t.localPosition);
			Physics.SyncTransforms();
		}

		static IEnumerator LaserRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;

			// The action "character": Raft's characters by number and name (the unlock itself changes the player's profile: not run)
			List<SO_Character> chars = Behaviours.Characters();
			Check(ref ok, chars.Count > 0, "Raft's characters are found (" + string.Join(", ", chars.Select(x => x.index + " " + x.displayName).ToArray()) + ")");
			if (chars.Count > 0)
			{
				SO_Character last = chars.Last();
				Check(ref ok, Behaviours.CharacterOf(last.index.ToString()) == last && Behaviours.CharacterOf(" " + last.displayName.ToUpperInvariant() + " ") == last && Behaviours.CharacterOf("no such one") == null,
					"the action character finds '" + last.displayName + "' by number and by name, and nothing for a wrong name");
			}
			ObjAction act = ObjAction.Parse("character||" + (chars.Count > 0 ? chars.Last().displayName : "x"));
			Check(ref ok, act != null && act.Verb == "character" && act.Describe().StartsWith("unlock the character"), "the action reads as '" + (act != null ? act.Describe() : "?") + "'");

			yield return EnsureAlive();
			var pieces = new[] { new[] { "TP_LaserEmitter", "emitter" }, new[] { "TP_MirrorHousing_InteractableMirror", "mirror" }, new[] { "TP_LaserDoorPanel", "target" } };
			yield return PlaceableCatalog.EnsureLoaded(pieces.Select(p => p[0]).ToList());
			var s = new IslandGenSettings { Seed = 8484, Radius = 50f, Height = 14f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, LaserIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			for (int i = 0; i < pieces.Length; i++)
			{
				Dictionary<string, string> p = ObjectProps.Defaults(pieces[i][0]);
				p[BehaviourProps.Name] = pieces[i][1];
				float x = c.x - 8f + i * 8f;
				f.Objects.Add(new IslandObject { Name = pieces[i][0], Position = new Vector3(x, ground(x, c.y), c.y), Props = p });
			}
			IslandWorldState.Remove(LaserIsland);
			f.Save(IslandSpawner.PathFor(LaserIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(LaserIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(LaserIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == LaserIsland);
			if (e == null || e.Root == null) { Fail(LaserIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);

			IslandObjectRef emitter = ScObjOf(e, "emitter"), mirror = ScObjOf(e, "mirror"), target = ScObjOf(e, "target");
			LaserBeam beam = emitter != null ? emitter.GetComponent<LaserBeam>() : null;
			Check(ref ok, beam != null && mirror != null && target != null && LaserBeam.IsMirror(mirror), "an emitter with a beam, a mirror and a target panel");
			if (beam == null || mirror == null || target == null) { Fail("laser beam"); yield break; }

			// Lined up at run time along the emitter's beam, high above the ground (the island is up to 14 m high; 3 m up, the
			// beam aside from the mirror ran into the slope towards the middle): emitter -> 6 m -> mirror -> 5 m -> target
			emitter.transform.position += Vector3.up * 20f;
			Physics.SyncTransforms();
			Vector3 o = beam.Origin, dir = emitter.transform.forward;
			target.transform.position += Vector3.up * 40f;
			// (the mirror stood as the emitter does, sending the beam straight on: a quarter round, it sends it aside - so a
			// beam that misses the turned mirror can't reach the panel by going straight on)
			mirror.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up) * mirror.transform.rotation;
			IslandBehaviour mb = mirror.GetComponent<IslandBehaviour>();
			if (mb != null) HarmonyLib.Traverse.Create(mb).Field("startRot").SetValue(mirror.transform.localRotation);
			LaserMoveTo(mirror.transform, o + dir * 6f);
			beam.Trace();
			Log("  beam before the turn: mirror yaw " + mirror.transform.eulerAngles.y.ToString("0") + ", " + LaserPath(beam));
			Check(ref ok, beam.Mirrors.Contains(mirror) && beam.Points.Count >= 3, "the beam reaches the mirror and goes on (" + beam.Points.Count + " points, " + beam.Mirrors.Count + " mirrors)");
			if (beam.Points.Count >= 3)
			{
				Vector3 at = beam.Points[1], outDir = (beam.Points[2] - beam.Points[1]).normalized;
				LaserMoveTo(target.transform, at + outDir * 5f);
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, beam.Hit == target, "by the mirror the beam reaches the target panel (" + (beam.Hit != null ? beam.Hit.Name : "nothing") + ")");
				Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.LaserSignal)), "the panel answers the beam: signal 'laser'");
				ScUse(e, "mirror");
				yield return new WaitForSeconds(1.5f);
				beam.Trace();
				Log("  beam after the turn: mirror yaw " + mirror.transform.eulerAngles.y.ToString("0") + " (open " + (mb != null ? mb.Current.ToString("0.##") : "-") + "), " + LaserPath(beam));
				Check(ref ok, beam.Hit != target, "the mirror turned a quarter, the beam misses the panel (" + (beam.Hit != null ? beam.Hit.Name : "nothing") + ")");
			}
			Check(ref ok, beam.GetComponentInChildren<LineRenderer>() != null, "the beam is drawn");
			IslandWorldState.RemoveIds(new[] { e }.Select(x => x.Id).ToList(), true);
			try { System.IO.File.Delete(IslandSpawner.PathFor(LaserIsland)); } catch { }
			if (ok) Log("PASS: laser beam"); else Fail("laser beam");
		}

		const string ArenaIsland = "cibossarena";

		[ConsoleCommand(name: "CIBossArena", docs: "Dev, in game (host): the boss map type's arena - stepping in shows its stakes (the spoils hidden), the beast's defeat hides them, shows the spoils and sends boss; stepping in again leaves them down")]
		public static void BossArenaCommand(string[] args) { StartTest(BossArenaRoutine()); }

		static IEnumerator BossArenaRoutine()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (RAPI.GetLocalPlayer() == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { MapTypes.ArenaStake });
			Check(ref ok, PlaceableCatalog.Get(MapTypes.ArenaStake) != null, "the arena's stakes load from Raft's islands (" + MapTypes.ArenaStake + ")");
			float elev;
			IslandGenSettings s = MapTypes.Roll(MapTypes.Get("boss"), new System.Random(5151), out elev);
			IslandFile f = MapTypes.Create(MapTypes.Get("boss"), s, elev, ArenaIsland);
			Check(ref ok, f.Objects.Count(o => o.Props != null && ObjectProps.Get(o.Props, BehaviourProps.Name) == MapTypes.ArenaGate) >= 10, "the boss island has its ring of stakes");
			IslandWorldState.Remove(ArenaIsland);
			f.Save(IslandSpawner.PathFor(ArenaIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(ArenaIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(ArenaIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == ArenaIsland);
			if (e == null || e.Root == null) { Fail(ArenaIsland + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			Func<List<IslandObjectRef>> gates = () => e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(r => r.Name == MapTypes.ArenaGate).ToList();
			Func<bool> spoilsShown = () => { IslandObjectRef r = ScObjOf(e, "Spoils"); return r != null && r.gameObject.activeInHierarchy; };
			IslandObjectRef boss = e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(r => r.ObjectName.StartsWith("Creature_"));
			Check(ref ok, gates().Count >= 10 && gates().All(g => !g.gameObject.activeInHierarchy) && !spoilsShown() && boss != null, "before: " + gates().Count + " stakes down, the spoils hidden, the beast there");
			ScEnterZone(e, "arena");
			yield return new WaitForSeconds(1f);
			Check(ref ok, gates().All(g => g.gameObject.activeInHierarchy), "stepping in, the stakes come up (" + gates().Count(g => g.gameObject.activeInHierarchy) + ")");
			if (boss != null) Behaviours.FireFromHost(e, boss.Index, "defeat");
			yield return new WaitForSeconds(1f);
			Check(ref ok, gates().All(g => !g.gameObject.activeInHierarchy) && spoilsShown() && e.State.ContainsKey(Behaviours.SignalKey(MapTypes.BossSignal)), "the beast defeated: the stakes go down, the spoils show, signal 'boss'");
			ScEnterZone(e, "arena");
			yield return new WaitForSeconds(1f);
			Check(ref ok, gates().All(g => !g.gameObject.activeInHierarchy), "stepping in again, the stakes stay down");
			yield return EnsureAlive();
			IslandWorldState.RemoveIds(new[] { e }.Select(x => x.Id).ToList(), true);
			try { System.IO.File.Delete(IslandSpawner.PathFor(ArenaIsland)); } catch { }
			if (ok) Log("PASS: boss arena"); else Fail("boss arena");
		}
	}
}
