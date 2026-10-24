using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Test gaps (TEST_GAPS_2026-10-13), host only: puzzle state across a reload, trader / rogue / head start across a world
	/// file save and read, bad world file lines, bad stamp and group files, ReloadMapTypes.
	/// "Reload" of an island = unload and load it again (as sailing away and back does, ReloadIslandRoutine, the way
	/// CICodeLock does it); "reload" of the world = the world file saved and read again (X5Load, as the X5 tests do).
	/// </summary>
	public static partial class DevTests
	{
		#region Helpers

		class GwIsland { public IslandWorldState.Entry E; }

		/// <summary>Saves the file as a test island, spawns it near the raft (g.E is null when that failed).</summary>
		static IEnumerator GwSpawnFile(GwIsland g, IslandFile f, string island)
		{
			g.E = null;
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue) { Fail("no raft"); yield break; }
			IslandWorldState.Remove(island);
			f.Save(IslandSpawner.PathFor(island));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(island), 450f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(island, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == island);
			if (e == null || e.Root == null) { Fail(island + " did not spawn"); yield break; }
			g.E = e;
			yield return new WaitForSeconds(1f);
		}

		/// <summary>A flat-ish test island with these pieces (name, object name) in a row.</summary>
		static IEnumerator GwSpawnPieces(GwIsland g, string island, int seed, string[][] pieces, float x0, float dx)
		{
			yield return PlaceableCatalog.EnsureLoaded(pieces.Select(p => p[0]).ToList());
			var s = new IslandGenSettings { Seed = seed, Radius = 50f, Height = 14f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, island);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			for (int i = 0; i < pieces.Length; i++)
			{
				Dictionary<string, string> p = ObjectProps.Defaults(pieces[i][0]);
				p[BehaviourProps.Name] = pieces[i][1];
				float x = c.x + x0 + i * dx;
				f.Objects.Add(new IslandObject { Name = pieces[i][0], Position = new Vector3(x, ground(x, c.y), c.y), Props = p });
			}
			yield return GwSpawnFile(g, f, island);
		}

		static void GwDrop(GwIsland g, string island)
		{
			if (g.E != null) IslandWorldState.RemoveIds(new[] { g.E }.Select(x => x.Id).ToList(), true);
			try { if (File.Exists(IslandSpawner.PathFor(island))) File.Delete(IslandSpawner.PathFor(island)); } catch { }
		}

		/// <summary>Off the island, then the island unloaded and loaded again (CICodeLock's reload step).</summary>
		static IEnumerator GwReload(IslandWorldState.Entry e)
		{
			OnRaftCommand();
			yield return new WaitForSeconds(0.5f);
			yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
		}

		static string GwWhere(string what)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || !CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) return what + ": run in a world, as the host";
			return null;
		}

		#endregion

		#region Puzzle state across a reload

		[ConsoleCommand(name: "CIReadyPiecesReload", docs: "Dev, in game (host): puzzle state across a reload (gap: only CICodeLock had one) - a mirror turned, a water wheel and a pipe quarter-turned, a wire connected: the island unloaded and loaded again keeps each turn (within 5 degrees), the signals 'wheel' and 'wire' and the one cable the wire took; used again the wire wants no second cable and the wheel turns on from where it was")]
		public static void ReadyPiecesReloadCommand(string[] args) { StartTest(ReadyPiecesReloadRoutine()); }

		static IEnumerator ReadyPiecesReloadRoutine()
		{
			string where = GwWhere("ready pieces reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			const string island = "cipiecesreload";
			var g = new GwIsland();
			string cable = StoryItems.Ref(ReadyPieces.ItemOf("UT_ElectricCableHub01"));
			yield return EnsureAlive();
			yield return GwSpawnPieces(g, island, 8585, new[] { new[] { "TP_MirrorHousing_InteractableMirror", "mirror" }, new[] { "UT_WaterChallengeWheel1", "wheel" }, new[] { "WaterPipe_Short_1", "pipe" }, new[] { "UT_ElectricCableHub01", "wire" } }, -9f, 5f);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				string[] turning = { "mirror", "wheel", "pipe" };
				var before = new Dictionary<string, Quaternion>();
				var start = new Dictionary<string, Quaternion>();
				foreach (string n in turning)
				{
					IslandObjectRef r = ScObjOf(e, n);
					Check(ref ok, r != null, "the " + n + " is on the island");
					if (r == null) { Fail("ready pieces reload"); yield break; }
					start[n] = r.transform.rotation;
				}
				StoryBook.Take(cable, 99);
				StoryBook.Give(cable, 2);
				foreach (string n in new[] { "mirror", "wheel", "pipe", "wire" })
				{
					ScUse(e, n);
					yield return new WaitForSeconds(2.5f);
				}
				foreach (string n in turning)
				{
					before[n] = ScObjOf(e, n).transform.rotation;
					Check(ref ok, Quaternion.Angle(start[n], before[n]) > 45f, "the " + n + " turned a quarter (" + Quaternion.Angle(start[n], before[n]).ToString("0") + " degrees)");
				}
				Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WireSignal)) && StoryBook.Count(cable) == 1, "the wire connected and took one cable (" + StoryBook.Count(cable) + " of 2 left)");
				bool wheelSignal = e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WheelSignal));

				yield return GwReload(e);
				foreach (string n in turning)
				{
					IslandObjectRef r = ScObjOf(e, n);
					float off = r != null ? Quaternion.Angle(before[n], r.transform.rotation) : 999f;
					Check(ref ok, r != null && off < 5f, "after a reload the " + n + " stays turned (" + off.ToString("0") + " degrees off)");
				}
				Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WireSignal)), "after a reload the wire is still connected: signal 'wire'");
				Check(ref ok, !wheelSignal || e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.WheelSignal)), "after a reload the signal 'wheel' is still there (" + (wheelSignal ? "it was sent" : "it was never sent") + ")");
				ScUse(e, "wire");
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, StoryBook.Count(cable) == 1, "the connected wire wants no second cable after a reload (" + StoryBook.Count(cable) + ")");
				IslandObjectRef wheel = ScObjOf(e, "wheel");
				Quaternion wheelNow = wheel != null ? wheel.transform.rotation : Quaternion.identity;
				ScUse(e, "wheel");
				yield return new WaitForSeconds(2.5f);
				float more = wheel != null ? Quaternion.Angle(wheelNow, ScObjOf(e, "wheel").transform.rotation) : 0f;
				Check(ref ok, more > 45f, "the wheel turns on from where it was (" + more.ToString("0") + " degrees more)");
			}
			finally
			{
				StoryBook.Take(cable, 99);
				GwDrop(g, island);
			}
			if (ok) Log("PASS: ready pieces reload"); else Fail("ready pieces reload");
		}

		[ConsoleCommand(name: "CILaserReload", docs: "Dev, in game (host): the laser across a reload (gap: CILaser has no reload step) - the beam solved (signal 'laser') and the mirror turned a quarter: the island unloaded and loaded again keeps the signal and the mirror's turn (a quarter from the placed turn), the emitter still draws its beam, and the mirror turns on")]
		public static void LaserReloadCommand(string[] args) { StartTest(LaserReloadRoutine()); }

		static IEnumerator LaserReloadRoutine()
		{
			string where = GwWhere("laser reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			const string island = "cilaserreload";
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return GwSpawnPieces(g, island, 8686, new[] { new[] { "TP_LaserEmitter", "emitter" }, new[] { "TP_MirrorHousing_InteractableMirror", "mirror" }, new[] { "TP_LaserDoorPanel", "target" } }, -8f, 8f);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				IslandObjectRef emitter = ScObjOf(e, "emitter"), mirror = ScObjOf(e, "mirror"), target = ScObjOf(e, "target");
				LaserBeam beam = emitter != null ? emitter.GetComponent<LaserBeam>() : null;
				Check(ref ok, beam != null && mirror != null && target != null && LaserBeam.IsMirror(mirror), "an emitter with a beam, a mirror and a target panel");
				if (beam == null || mirror == null || target == null) { Fail("laser reload"); yield break; }
				Quaternion placed = mirror.transform.rotation;
				// Lined up at run time as CILaser does (emitter high up, mirror 6 m along the beam, the target where the mirror sends it)
				emitter.transform.position += Vector3.up * 20f;
				Physics.SyncTransforms();
				Vector3 o = beam.Origin, dir = emitter.transform.forward;
				target.transform.position += Vector3.up * 40f;
				mirror.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up) * mirror.transform.rotation;
				IslandBehaviour mb = mirror.GetComponent<IslandBehaviour>();
				if (mb != null) HarmonyLib.Traverse.Create(mb).Field("startRot").SetValue(mirror.transform.localRotation);
				LaserMoveTo(mirror.transform, o + dir * 6f);
				beam.Trace();
				if (beam.Points.Count >= 3)
				{
					LaserMoveTo(target.transform, beam.Points[1] + (beam.Points[2] - beam.Points[1]).normalized * 5f);
					yield return new WaitForSeconds(0.5f);
				}
				Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.LaserSignal)), "the beam reaches the panel: signal 'laser' (" + LaserPath(beam) + ")");
				Quaternion before = mirror.transform.rotation;
				ScUse(e, "mirror");
				yield return new WaitForSeconds(1.5f);
				float turned = Quaternion.Angle(before, mirror.transform.rotation);
				Check(ref ok, turned > 45f, "the mirror turns a quarter (" + turned.ToString("0") + " degrees)");

				yield return GwReload(e);
				IslandObjectRef mirror2 = ScObjOf(e, "mirror"), emitter2 = ScObjOf(e, "emitter");
				Check(ref ok, mirror2 != null && emitter2 != null, "after a reload the emitter and the mirror are there");
				if (mirror2 == null || emitter2 == null) { Fail("laser reload"); yield break; }
				// (the placed turn is the file's; the quarter the player turned it is the state: a quarter from the placed turn)
				float fromPlaced = Quaternion.Angle(placed, mirror2.transform.rotation);
				Check(ref ok, Mathf.Abs(fromPlaced - turned) < 5f, "after a reload the mirror stays turned a quarter from its placed turn (" + fromPlaced.ToString("0") + " degrees; it turned " + turned.ToString("0") + ")");
				Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey(ReadyPieces.LaserSignal)), "after a reload the signal 'laser' is still there");
				LaserBeam beam2 = emitter2.GetComponent<LaserBeam>();
				Check(ref ok, beam2 != null && beam2.GetComponentInChildren<LineRenderer>() != null, "after a reload the emitter draws its beam");
				Quaternion q3 = mirror2.transform.rotation;
				ScUse(e, "mirror");
				yield return new WaitForSeconds(1.5f);
				float more = Quaternion.Angle(q3, ScObjOf(e, "mirror").transform.rotation);
				Check(ref ok, more > 45f, "the mirror turns on after a reload (" + more.ToString("0") + " degrees more)");
			}
			finally { GwDrop(g, island); }
			if (ok) Log("PASS: laser reload"); else Fail("laser reload");
		}

		const string ArenaReloadIsland = "cibossarenareload";

		[ConsoleCommand(name: "CIBossArenaReload", docs: "Dev, in game (host): the boss arena across a reload (gap: CIBossArena has no reload step) - half fought (stepped in, stakes up, the beast alive): reloaded the stakes are down (the two minutes' wait doesn't outlast a load: what came after it runs at once), the spoils hidden, the beast there, and stepping in again shuts the stakes again; the beast defeated: reloaded the stakes stay down, the spoils show, signal 'boss' is kept, stepping in again leaves the stakes down")]
		public static void BossArenaReloadCommand(string[] args) { StartTest(BossArenaReloadRoutine()); }

		static IEnumerator BossArenaReloadRoutine()
		{
			string where = GwWhere("boss arena reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { MapTypes.ArenaStake });
			float elev;
			IslandGenSettings s = MapTypes.Roll(MapTypes.Get("boss"), new System.Random(5252), out elev);
			IslandFile f = MapTypes.Create(MapTypes.Get("boss"), s, elev, ArenaReloadIsland);
			yield return GwSpawnFile(g, f, ArenaReloadIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				Func<List<IslandObjectRef>> gates = () => e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(r => r.Name == MapTypes.ArenaGate).ToList();
				Func<bool> spoilsShown = () => { IslandObjectRef r = ScObjOf(e, "Spoils"); return r != null && r.gameObject.activeInHierarchy; };
				Func<IslandObjectRef> beast = () => e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(r => r.ObjectName.StartsWith("Creature_"));
				IslandObjectRef boss = beast();
				Check(ref ok, gates().Count >= 10 && gates().All(x => !x.gameObject.activeInHierarchy) && !spoilsShown() && boss != null, "before: " + gates().Count + " stakes down, the spoils hidden, the beast there");
				ScEnterZone(e, "arena");
				yield return new WaitForSeconds(1f);
				Check(ref ok, gates().All(x => x.gameObject.activeInHierarchy), "stepping in, the stakes come up (" + gates().Count(x => x.gameObject.activeInHierarchy) + ")");

				// (the stakes sink after a wait of two minutes; a load runs what came after a wait at once - Behaviours.ResumePending -
				// so a player who quit mid-fight isn't shut in when they come back, and the arena re-arms when they step in)
				yield return GwReload(e);
				yield return new WaitForSeconds(1f);
				Check(ref ok, gates().Count >= 10 && gates().All(x => !x.gameObject.activeInHierarchy) && !spoilsShown(), "half fought, reloaded: the stakes are down (" + gates().Count(x => x.gameObject.activeInHierarchy) + " of " + gates().Count + " up), the spoils hidden");
				boss = beast();
				Check(ref ok, boss != null, "half fought, reloaded: the beast is there");
				ScEnterZone(e, "arena");
				yield return new WaitForSeconds(1f);
				Check(ref ok, gates().All(x => x.gameObject.activeInHierarchy), "half fought, reloaded, stepping in again: the stakes come up (" + gates().Count(x => x.gameObject.activeInHierarchy) + ")");
				if (boss != null) Behaviours.FireFromHost(e, boss.Index, "defeat");
				yield return new WaitForSeconds(1f);
				Check(ref ok, gates().All(x => !x.gameObject.activeInHierarchy) && spoilsShown() && e.State.ContainsKey(Behaviours.SignalKey(MapTypes.BossSignal)), "the beast defeated: the stakes go down, the spoils show, signal 'boss'");

				yield return GwReload(e);
				Check(ref ok, gates().All(x => !x.gameObject.activeInHierarchy) && spoilsShown() && e.State.ContainsKey(Behaviours.SignalKey(MapTypes.BossSignal)), "defeated, reloaded: the stakes stay down, the spoils show, signal 'boss' is kept");
				IslandObjectRef again = beast();
				Log("  (the beast after the reload: " + (again == null ? "gone" : again.gameObject.activeInHierarchy ? "active" : "hidden") + ")");
				ScEnterZone(e, "arena");
				yield return new WaitForSeconds(1f);
				Check(ref ok, gates().All(x => !x.gameObject.activeInHierarchy), "defeated, reloaded, stepping in again: the stakes stay down");
				yield return EnsureAlive();
			}
			finally { GwDrop(g, ArenaReloadIsland); }
			if (ok) Log("PASS: boss arena reload"); else Fail("boss arena reload");
		}

		#endregion

		#region Trader, ghost raft, rogue shark and head start across a world file save and read

		[ConsoleCommand(name: "CITraderReload", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): @tradersailed and @ghostsailed across a reload of the world - set, the world file saved and read again: both lines are written and read back the same, no trader is brought by the reading. The world's file is put back after")]
		public static void TraderReloadCommand(string[] args) { StartTest(TraderReloadRoutine()); }

		static IEnumerator TraderReloadRoutine()
		{
			string where = X5Where("trader reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			string[] before = X5Backup();
			try
			{
				X5Load(new[] { "@auto=on", "@options=traderraft,ghostrafts", "@optionseed=11", "@tradersailed=12345", "@ghostsailed=6789" });
				Func<string, bool> has = line => TraderRaft.WriteLines().Concat(GhostRafts.WriteLines()).Contains(line);
				Check(ref ok, has("@tradersailed=12345") && has("@ghostsailed=6789"), "the lines are read: trader sailed 12345, ghost raft sailed 6789");
				IslandWorldState.Save();
				string[] saved = X5Saved();
				Check(ref ok, saved.Contains("@tradersailed=12345") && saved.Contains("@ghostsailed=6789"), "the save writes both lines");
				X5Load(saved);
				Check(ref ok, has("@tradersailed=12345") && has("@ghostsailed=6789"), "after the world is read again both still stand (" + string.Join(" ", TraderRaft.WriteLines().Concat(GhostRafts.WriteLines()).ToArray()) + ")");
				Check(ref ok, TraderRaft.Brought == 0 && GhostRafts.Brought == 0, "the reading brings no trader and no ghost raft");
				yield return new WaitForSeconds(2f);
				Check(ref ok, TraderRaft.Brought == 0 && GhostRafts.Brought == 0, "nothing comes by itself afterwards (not sailing)");
				IslandWorldState.Save();
				Check(ref ok, X5Saved().Contains("@tradersailed=12345") && X5Saved().Contains("@ghostsailed=6789"), "saved again: still the same two lines");
			}
			finally { X5Restore(before); }
			if (ok) Log("PASS: trader reload"); else Fail("trader reload");
		}

		[ConsoleCommand(name: "CIRogueReload", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): the rogue shark across a reload of the world - a giant flag survives the file; a live rogue (spawned) is written with its index, and after the world file is read again it is still the rogue (found, alive, not replaced); a rogue whose shark is gone (index no shark has) is dropped cleanly after ~15 s (taken over by another shark or a new one comes), no errors. The world's file is put back after")]
		public static void RogueReloadCommand(string[] args) { StartTest(RogueReloadRoutine()); }

		static IEnumerator RogueReloadRoutine()
		{
			string where = X5Where("rogue reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			string[] before = X5Backup();
			var errors = new List<string>();
			Application.LogCallback watch = (text, trace, type) => { if (type == LogType.Error || type == LogType.Exception) errors.Add(text.Length > 160 ? text.Substring(0, 160) : text); };
			Application.logMessageReceived += watch;
			AI_NetworkBehavior_Shark first = null;
			try
			{
				// A giant flag through the file (a made-up index: read, never waited on)
				X5Load(new[] { "@auto=on", "@options=rogueshark", "@optionseed=5", "@rogueshark=123;999999;1" });
				Check(ref ok, RogueShark.Index == 123 && RogueShark.Giant && RogueShark.WriteLines().FirstOrDefault() == "@rogueshark=123;999999;1", "a giant rogue is read from the file and written back the same");
				IslandWorldState.Save();
				X5Load(X5Saved());
				Check(ref ok, RogueShark.Index == 123 && RogueShark.Giant && RogueShark.QuietUntil == 999999, "the giant flag and the quiet days survive a save and a read");

				// A live rogue
				X5Load(new[] { "@auto=on", "@options=rogueshark", "@optionseed=5", "@rogueshark=0;999999" });
				first = RogueShark.Spawn(false);
				uint index = RogueShark.Index;
				Check(ref ok, first != null && index != 0 && RogueShark.Alive, "a rogue shark #" + index + " is spawned and alive");
				IslandWorldState.Save();
				Check(ref ok, X5Saved().Contains("@rogueshark=" + index + ";999999"), "the save writes its index");
				X5Load(X5Saved());
				Check(ref ok, RogueShark.Index == index && RogueShark.Find() != null && RogueShark.Alive && !RogueShark.Giant, "after the world is read again it is still the rogue: found and alive (#" + RogueShark.Index + ")");
				yield return new WaitForSeconds(3f);
				Check(ref ok, RogueShark.Index == index && RogueShark.Alive, "it is not replaced a few seconds later");

				// A rogue whose shark is gone: dropped cleanly
				uint gone = 4000000000u;
				X5Load(new[] { "@auto=on", "@options=rogueshark", "@optionseed=5", "@rogueshark=" + gone + ";999999" });
				yield return new WaitForSeconds(19f);
				bool clean = RogueShark.Index != gone && (RogueShark.Index == 0 || RogueShark.Find() != null);
				Check(ref ok, clean, "a rogue whose shark is gone is dropped after ~15 s: now " + (RogueShark.Index == 0 ? "none" : "#" + RogueShark.Index + (RogueShark.Find() != null ? " (a shark that is there)" : " (NOT there)")));
				Check(ref ok, errors.Count == 0, "no errors in the log" + (errors.Count > 0 ? ": " + errors[0] : ""));
			}
			finally
			{
				Application.logMessageReceived -= watch;
				try
				{
					RogueShark.RogueSharkCommand(new[] { "kill" });
					if (first != null && first.networkEntity != null && !first.networkEntity.IsDead) first.networkEntity.Damage(100000f, first.transform.position, Vector3.up, EntityType.Player, true);
				}
				catch (Exception ex) { Log("  (killing the test sharks: " + ex.Message + ")"); }
				X5Restore(before);
			}
			if (ok) Log("PASS: rogue reload"); else Fail("rogue reload");
		}

		static int GwPlanks(Raft raft)
		{
			int planks = 0;
			foreach (Storage_Small s in raft.GetComponentsInChildren<Block>().Where(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName.StartsWith("Placeable_Storage")).Select(b => b.GetComponentInChildren<Storage_Small>(true)).Where(s => s != null))
			{
				Inventory inv = HarmonyLib.Traverse.Create(s).Field("inventoryReference").GetValue<Inventory>();
				if (inv != null) planks += inv.GetItemCount("Plank");
			}
			return planks;
		}

		[ConsoleCommand(name: "CIHeadStartReload", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): a half-used head start across a reload of the world - level 1 built (waits for it), 5 planks taken from the storages, the world file saved and read again: @headstart=1 and @headstartdone=1 stay, nothing is built a second time (same foundations, one small storage, the planks as they were). The raft the test builds stays in the test world; the world's file is put back after")]
		public static void HeadStartReloadCommand(string[] args) { StartTest(HeadStartReloadRoutine()); }

		static IEnumerator HeadStartReloadRoutine()
		{
			string where = X5Where("head start reload");
			if (where != null) { Fail(where); yield break; }
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null) { Fail("head start reload: no raft"); yield break; }
			bool ok = true;
			string[] before = X5Backup();
			try
			{
				X5Load(new[] { "@auto=on", "@headstart=1" });
				float until = Time.realtimeSinceStartup + 40f;
				while (!HeadStart.Done && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.5f);
				bool built = HeadStart.Done && !HeadStart.LastBuild.StartsWith("skipped");
				Check(ref ok, HeadStart.Level == 1 && HeadStart.Done, "level 1 head start: " + (HeadStart.Done ? (built ? "built" : "given up on (" + HeadStart.LastBuild + ")") : "NOT built in 40 s"));
				yield return new WaitForSecondsRealtime(1f);
				int floors = HeadStart.Foundations(raft).Count;
				int blocks = raft.GetComponentsInChildren<Block>().Count(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName == "Placeable_Storage_Small");
				int planks = GwPlanks(raft);
				if (built && planks >= 5)
				{
					foreach (Storage_Small s in raft.GetComponentsInChildren<Block>().Where(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName.StartsWith("Placeable_Storage")).Select(b => b.GetComponentInChildren<Storage_Small>(true)).Where(s => s != null))
					{
						Inventory inv = HarmonyLib.Traverse.Create(s).Field("inventoryReference").GetValue<Inventory>();
						if (inv != null && inv.GetItemCount("Plank") >= 5) { inv.RemoveItem("Plank", 5); break; }
					}
					Check(ref ok, GwPlanks(raft) == planks - 5, "half used: 5 planks taken (" + planks + " -> " + GwPlanks(raft) + ")");
					planks = GwPlanks(raft);
				}
				else if (built) Check(ref ok, false, "the storages hold " + planks + " planks (want at least 5 to use)");
				IslandWorldState.Save();
				string[] saved = X5Saved();
				Check(ref ok, saved.Contains("@headstart=1") && saved.Contains("@headstartdone=1"), "the save writes @headstart=1 and @headstartdone=1");
				X5Load(saved);
				Check(ref ok, HeadStart.Level == 1 && HeadStart.Done, "after the world is read again: level " + HeadStart.Level + ", " + (HeadStart.Done ? "done" : "NOT done"));
				yield return new WaitForSecondsRealtime(6f); // (a rebuild would start ~2 s after the reading)
				Check(ref ok, HeadStart.Foundations(raft).Count == floors, "no second head start: the foundations are as they were (" + floors + " -> " + HeadStart.Foundations(raft).Count + ")");
				if (built)
				{
					Check(ref ok, raft.GetComponentsInChildren<Block>().Count(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName == "Placeable_Storage_Small") == blocks && GwPlanks(raft) == planks, "the storages and the half-used supplies are as they were (" + blocks + " small storage, " + planks + " planks)");
				}
				Check(ref ok, HeadStart.Build().Contains("built already") || !built, "asking for a build again says it is built already");
				Check(ref ok, HeadStart.WriteLines().SequenceEqual(new[] { "@headstart=1", "@headstartdone=1" }), "written again: " + string.Join(" ", HeadStart.WriteLines().ToArray()));
			}
			finally { X5Restore(before); }
			if (ok) Log("PASS: head start reload"); else Fail("head start reload");
		}

		#endregion

		#region Bad world file lines (also called by CIBadFiles)

		[ConsoleCommand(name: "CIBadWorldLines", docs: "Dev, world (host, a saved test world 'CI ...' without custom islands): hand-edited world file lines with nonsense (@options, @optionseed, @daily, @rogueshark, @storages, @headstart(done), @storychain and @story* lines, @islandsoff, @raftgap(count), @playtime, @between, @place, @tradersailed, @ghostsailed, @upgraderesearch / @upgradeslearned): the world loads, the line is ignored or defaulted, no exception, and a save and a read again hold. The world's file is put back after. Also part of CIBadFiles when that runs in such a world")]
		public static void BadWorldLinesCommand(string[] args)
		{
			bool ok = true;
			string where = X5Where("bad world lines");
			if (where != null) { Fail(where); return; }
			GwBadWorldLines(ref ok);
			if (ok) Log("PASS: bad world lines"); else Fail("bad world lines");
		}

		static string GwShown(string v) { return v.Length == 0 ? "(empty)" : v.Length > 40 ? v.Substring(0, 40) + "..." : v; }

		static bool GwFinite(string text) { float f; return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f); }

		/// <summary>Skipped (logged) when not in a CI test world.</summary>
		static void GwBadWorldLines(ref bool okRef)
		{
			bool ok = true;
			string where = X5Where("bad world lines");
			if (where != null) { Log("bad world lines: skipped - " + where); return; }
			string[] before = X5Backup();
			var problems = new List<string>();
			Application.LogCallback watch = (text, trace, type) =>
			{
				if (type == LogType.Error || type == LogType.Exception || (type == LogType.Warning && text.Contains("Could not read a line"))) problems.Add(text.Length > 200 ? text.Substring(0, 200) : text);
			};
			Application.logMessageReceived += watch;
			int rounds = 0, bad = 0;
			var noExtra = new string[0];
			var noBuild = new[] { "@headstartdone=1" }; // (a head start that is due would be built into the test world)
			// (one world file per value: the line alone after the bits that keep the world quiet; checked as read, then saved and read again)
			Action<string, string[], string[], Func<string, string>> run = (key, values, extra, verify) =>
			{
				foreach (string v in values)
				{
					rounds++;
					problems.Clear();
					string result = null;
					try
					{
						var lines = new List<string> { "@auto=on" };
						lines.AddRange(extra);
						lines.Add("@" + key + "=" + v);
						X5Load(lines);
						result = verify != null ? verify(v) : null;
						if (result == null)
						{
							IslandWorldState.Save();
							X5Load(X5Saved());
							result = verify != null ? verify(v) : null;
							if (result != null) result = "after a save and a read: " + result;
						}
					}
					catch (Exception e) { result = "threw " + e.GetType().Name + ": " + e.Message; }
					if (result == null && problems.Count > 0) result = "log: " + problems[0];
					if (result != null) bad++;
					Check(ref ok, result == null, "@" + key + "=" + GwShown(v) + ": " + (result ?? "the world loads, the line ignored or defaulted"));
				}
			};
			try
			{
				run("options", new[] { "nonsense", ",,;;", "GHOSTRAFTS, nonsense", "ghostrafts;ghostrafts;", "" }, noExtra,
					v => WorldOptions.Current.All(o => WorldOptions.All.Contains(o)) ? null : "an unknown option is on: " + string.Join(",", WorldOptions.Current.ToArray()));
				run("optionseed", new[] { "abc", "", "99999999999", "-5", "1.5" }, new[] { "@options=ghostrafts" }, null);
				run("daily", new[] { "garbage", "", "x;y", "-5;a;b;1;0;open;x" }, noExtra, v => DailyQuest.Current.Day < 0 ? null : "a task from nonsense (day " + DailyQuest.Current.Day + ")");
				run("daily", new[] { ";;;;;;;", "abc;kill;Boar;x;y;open;none" }, noExtra, null);
				run("rogueshark", new[] { "abc;xyz;1", "", "-5;-9;1", "99999999999999999999", "0;99999999999", ";;;", "0;0;1" }, new[] { "@options=rogueshark" },
					v => RogueShark.Index == 0 && !RogueShark.Giant ? null : "a rogue from nonsense (#" + RogueShark.Index + (RogueShark.Giant ? ", giant" : "") + ")");
				run("storages", new[] { "garbage", "", "1:2:3;;::;x:y;4294967296:1;-1:5", "5:abc" }, new[] { "@options=privatestorage" }, v => PrivateStorage.Count == 0 ? null : PrivateStorage.Count + " storage(s) from nonsense");
				run("headstart", new[] { "99", "-5", "abc", "", "1.5", "2" }, noBuild, v =>
				{
					int want; int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out want);
					want = Mathf.Clamp(want, 0, 3);
					return HeadStart.Level == want ? null : "level " + HeadStart.Level + ", wanted " + want;
				});
				// (nonsense never reads as done; "1" does, but with no head start - level 0 here, so nothing is built - the line
				// isn't saved: done then reads false after a save and a read, by design. CIHeadStartReload covers "1" with a level)
				run("headstartdone", new[] { "1", "abc", "", "true", "0" }, new[] { "@headstart=0" }, v => !HeadStart.Done || v.Trim() == "1" ? null : "done = True from nonsense");
				run("storychain", new[] { "garbage", "", "off;x,,y", ";;;", "on" }, noExtra, v => StoryChain.Steps.All(x => x.Trim().Length > 0) && !StoryChain.Active ? null : "empty steps or active from nonsense");
				run("storyrule", new[] { "garbage", "", "|||||", "a|b|c" }, noExtra, v => StoryChain.Rules.All(r => r != null) ? null : "a null rule");
				run("storybeside", new[] { "garbage", "", "|||||", "a|b|c" }, noExtra, null);
				run("storyending", new[] { "garbage", "", "||", ";;" }, noExtra, null);
				run("storyfreq", new[] { "garbage", "", "a:12", "a:abcd", "x:1234,y:12345,:1234,z:99999" }, noExtra, null);
				foreach (string k in new[] { "storyunlocked", "storydone", "storyfired", "storybrought" }) run(k, new[] { ",,,", " , ", "" }, noExtra, null);
				run("storydue", new[] { "a:b,c,:::,:5,x", "", "y:-5" }, noExtra, null);
				run("islandsoff", new[] { "|||", "", " | a||b | ", "x|x|X" }, noExtra, v => WorldIslands.Off.All(n => n.Trim().Length > 0) ? null : "an empty island name is left out of the world");
				run("raftgap", new[] { "abc", "", "5", "1-2-3", "-5--1", "0-0", "99999-99999", "9-3", "x-y" }, noExtra, v =>
				{
					int[] c = WorldIslands.ClampGap(WorldIslands.GapMin, WorldIslands.GapMax);
					return c[0] == WorldIslands.GapMin && c[1] == WorldIslands.GapMax && WorldIslands.GapMin <= WorldIslands.GapMax ? null : "gap " + WorldIslands.GapMin + "-" + WorldIslands.GapMax + " is out of range";
				});
				run("raftgapcount", new[] { "abc", "", "1/2/3", "-5/-7", "a/b", "99999999999/1" }, noExtra, v => WorldIslands.RaftIslandsSince >= 0 && WorldIslands.Target >= 0 ? null : "negative counts " + WorldIslands.RaftIslandsSince + "/" + WorldIslands.Target);
				run("playtime", new[] { "abc", "-5", "", "1e30" }, noExtra, v => WorldIslands.PlaySeconds >= 0f ? null : "play time " + WorldIslands.PlaySeconds);
				run("between", new[] { "abc", "", "-1", "99999999999999999999999" }, noExtra, null);
				run("place", new[] { "abc", "", "1|isle|1|2|3|4|5", "x|isle|1|2|3|4|5|6", "76561198000000000|isle|a|2|3|4|5|6", "76561198000000000|is|le|1|2|3|4|5|6|7|8", "76561198000000002|isle|NaN|2|3|4|5|6", "76561198000000003|isle|1|2|3|Infinity|5|6" }, noExtra, v =>
				{
					string[] places = PlayerPlaces.WriteLines().Where(l => l.Contains("|isle|") || l.Contains("|is|")).ToArray();
					if (places.Any(l => l.Contains("NaN") || l.Contains("Infinity"))) return "a place with NaN or Infinity is kept: " + places[0];
					return places.Length == 0 ? null : "a bad place is kept: " + places[0];
				});
				run("tradersailed", new[] { "abc", "", "-5", "NaN", "Infinity", "-Infinity", "99999999999999999999999", "1e40" }, new[] { "@options=traderraft" }, v =>
					TraderRaft.WriteLines().All(l => GwFinite(l.Substring(l.IndexOf('=') + 1))) ? null : "a trader line that is no number: " + string.Join(" ", TraderRaft.WriteLines().ToArray()));
				run("ghostsailed", new[] { "abc", "", "-5", "NaN", "Infinity", "-Infinity", "99999999999999999999999", "1e40" }, new[] { "@options=ghostrafts" }, v =>
					GhostRafts.WriteLines().All(l => GwFinite(l.Substring(l.IndexOf('=') + 1))) ? null : "a ghost line that is no number: " + string.Join(" ", GhostRafts.WriteLines().ToArray()));
				foreach (string k in new[] { "upgraderesearch", "upgradeslearned" })
					run(k, new[] { "abc", "", "1,2,,x,-1,99999999999", "0", ",,," }, noExtra, null);
			}
			catch (Exception e) { Check(ref ok, false, "bad world lines threw " + e.GetType().Name + ": " + e.Message); }
			finally
			{
				Application.logMessageReceived -= watch;
				X5Restore(before);
			}
			okRef &= ok;
			Log("bad world lines: " + rounds + " lines tried, " + bad + " not clean");
		}

		#endregion

		#region Bad stamp and group files (also called by CIBadFiles)

		[ConsoleCommand(name: "CIBadStamps", docs: "Dev, main menu or editor: broken terrain stamp files (empty, random bytes, text, cut short, a wrong size) and object group files (empty, random bytes, cut short, a huge count) are skipped and listed in the log, a good stamp next to them is still read, the built-in stamps are all there, no errors; the groups are still listed by name but not registered as objects. The temp files are deleted again")]
		public static void BadStampsCommand(string[] args) { StartTest(BadStampsRoutine()); }

		static IEnumerator BadStampsRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			var box = new[] { true };
			yield return GwBadStampsPart(box);
			if (box[0]) Log("PASS: bad stamps"); else Fail("bad stamps");
		}

		static IEnumerator GwBadStampsPart(bool[] okBox)
		{
			bool ok = true;
			var problems = new List<string>();
			var warns = new List<string>();
			Application.LogCallback watch = (text, trace, type) =>
			{
				if (type == LogType.Error || type == LogType.Exception) problems.Add(text.Length > 200 ? text.Substring(0, 200) : text);
				else if (type == LogType.Warning && text.Contains("CUSTOM ISLANDS")) warns.Add(text);
			};
			Application.logMessageReceived += watch;
			var made = new List<string>();
			int selected = TerrainStamps.Selected;
			var rng = new System.Random(77);
			byte[] noise = new byte[4096]; rng.NextBytes(noise);
			Func<Action<BinaryWriter>, byte[]> bytes = fill => { using (var m = new MemoryStream()) { using (var w = new BinaryWriter(m)) fill(w); return m.ToArray(); } };
			Func<string, byte[], string> put = (path, data) => { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, data); made.Add(path); return path; };
			const uint stampMagic = 0x54534943u, groupMagic = 0x52474943u;
			string sdir = TerrainStamps.Folder, gdir = GroupLibrary.Folder;
			string[] badStamps = { "cibadstamp-empty", "cibadstamp-noise", "cibadstamp-text", "cibadstamp-cut", "cibadstamp-size" };
			string[] badGroups = { "cibadgrp-empty", "cibadgrp-noise", "cibadgrp-cut", "cibadgrp-huge" };
			try
			{
				int builtIns = 0;
				try { TerrainStamps.Load(); builtIns = TerrainStamps.All.Count(s => s.BuiltIn); } catch (Exception e) { Check(ref ok, false, "reading the stamps threw first: " + e.Message); }
				put(Path.Combine(sdir, badStamps[0] + ".stamp"), new byte[0]);
				put(Path.Combine(sdir, badStamps[1] + ".stamp"), noise);
				put(Path.Combine(sdir, badStamps[2] + ".stamp"), System.Text.Encoding.ASCII.GetBytes("this is not a stamp, just some words"));
				put(Path.Combine(sdir, badStamps[3] + ".stamp"), bytes(w => { w.Write(stampMagic); w.Write(1); w.Write(TerrainStamps.Size); for (int i = 0; i < 100; i++) w.Write(0.5f); }));
				put(Path.Combine(sdir, badStamps[4] + ".stamp"), bytes(w => { w.Write(stampMagic); w.Write(1); w.Write(17); for (int i = 0; i < 17 * 17; i++) w.Write(0.5f); }));
				string goodStamp = Path.Combine(sdir, "cibadstamp-good.stamp");
				made.Add(goodStamp);
				TerrainStamps.Save(new TerrainStamps.Stamp { Name = "cibadstamp-good" });
				warns.Clear(); problems.Clear();
				string threw = null;
				try { TerrainStamps.Load(); } catch (Exception e) { threw = e.GetType().Name + ": " + e.Message; }
				Check(ref ok, threw == null && problems.Count == 0, "stamp files with nonsense: read without errors" + (threw != null ? " - " + threw : problems.Count > 0 ? " - " + problems[0] : ""));
				Check(ref ok, badStamps.All(n => !TerrainStamps.All.Any(s => s.Name == n)), "no broken stamp is in the stamp list (" + string.Join(", ", badStamps.Where(n => TerrainStamps.All.Any(s => s.Name == n)).ToArray()) + ")");
				Check(ref ok, TerrainStamps.All.Any(s => s.Name == "cibadstamp-good") && TerrainStamps.All.Count(s => s.BuiltIn) == builtIns && builtIns > 0, "the good stamp next to them is read, the " + builtIns + " built-in stamps are all there");
				Check(ref ok, warns.Any(w => w.Contains("cibadstamp-empty")) && warns.Any(w => w.Contains("cibadstamp-cut")), "an empty and a cut-short stamp are listed in the log (" + warns.Count(w => w.Contains("Stamp ")) + " stamp warning(s))");
				Log("  (a stamp with random bytes, plain text or a wrong size is skipped" + (warns.Any(w => w.Contains("cibadstamp-noise") || w.Contains("cibadstamp-text") || w.Contains("cibadstamp-size")) ? " with a warning" : " without a warning") + ")");

				// Object groups
				put(Path.Combine(gdir, badGroups[0] + ".group"), new byte[0]);
				put(Path.Combine(gdir, badGroups[1] + ".group"), noise);
				put(Path.Combine(gdir, badGroups[2] + ".group"), bytes(w => { w.Write(groupMagic); w.Write(1); w.Write(5); w.Write("Loot_Chest"); w.Write(1f); }));
				put(Path.Combine(gdir, badGroups[3] + ".group"), bytes(w => { w.Write(groupMagic); w.Write(1); w.Write(int.MaxValue); }));
				warns.Clear(); problems.Clear();
				Check(ref ok, badGroups.All(n => GroupLibrary.Saved().Contains(n)), "the broken group files are listed by name");
				yield return GroupLibrary.RegisterAll();
				Check(ref ok, problems.Count == 0, "group files with nonsense: registered without errors" + (problems.Count > 0 ? " - " + problems[0] : ""));
				Check(ref ok, badGroups.All(n => PlaceableCatalog.Get(GroupLibrary.Prefix + n) == null), "no broken group is an object in the catalog (" + string.Join(", ", badGroups.Where(n => PlaceableCatalog.Get(GroupLibrary.Prefix + n) != null).ToArray()) + ")");
				Check(ref ok, badGroups.All(n => warns.Any(w => w.Contains("'" + n + "'"))), "each broken group is named in the log (" + warns.Count(w => w.Contains("Group '")) + " group warning(s))");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				foreach (string p in made) try { if (File.Exists(p)) File.Delete(p); } catch { }
				try { TerrainStamps.Load(); TerrainStamps.Selected = selected; } catch { }
			}
			okBox[0] &= ok;
		}

		#endregion

		#region ReloadMapTypes

		[ConsoleCommand(name: "CIMapTypeReload", docs: "Dev, anywhere: a .maptype file edited while Raft runs - a test file is written and ReloadMapTypes reads it (the type is there, rolls in its radius range); edited and reloaded the change is seen (label, radius); a bad edit (radius = big) is not used and is listed as left out; mended and reloaded it is back; the built-in types are untouched. The file is deleted after")]
		public static void MapTypeReloadCommand(string[] args) { StartTest(MapTypeReloadRoutine()); }

		static string GwMapTypeText(string label, string radius, string extra = "")
		{
			return "label = " + label + "\r\ntitle = CI Reload\r\nsettings = ranges\r\nstyles = desert\r\nshape = round\r\nradius = " + radius + "\r\nheight = 20 - 25\r\nroughness = 0.4\r\ndensity = 0.3\r\npeaks = 2\r\n" + extra;
		}

		static IEnumerator MapTypeReloadRoutine()
		{
			bool ok = true;
			const string name = "cimtreload";
			string path = MapTypeFiles.PathFor(name);
			Action reload = () =>
			{
				try { MapTypeFiles.ReloadCommand(new string[0]); }
				catch (Exception e) { Log("  (ReloadMapTypes' message: " + e.Message + ")"); MapTypeFiles.LoadAll(); }
			};
			Func<MapType, float> radius = t => { float el; return MapTypes.Roll(t, new System.Random(3), out el).Radius; };
			try
			{
				Directory.CreateDirectory(MapTypeFiles.Folder);
				if (File.Exists(path)) File.Delete(path);
				MapTypeFiles.LoadAll();
				Check(ref ok, MapTypes.Get(name) == null, "before: no map type '" + name + "'");

				File.WriteAllText(path, GwMapTypeText("CI reload ONE", "60 - 70"));
				reload();
				MapType t1 = MapTypes.Get(name);
				Check(ref ok, t1 != null && t1.FromFile != null && (t1.Label ?? "").ToLowerInvariant().Contains("reload one"), "written and reloaded: the type is there (" + (t1 != null ? t1.Label : string.Join("; ", MapTypeFiles.Skipped.ToArray())) + ")");
				if (t1 != null) { float r = radius(t1); Check(ref ok, r >= 60f && r <= 70f, "it rolls in its radius range 60-70 (" + r.ToString("0") + ")"); }

				File.WriteAllText(path, GwMapTypeText("CI reload TWO", "80 - 90"));
				reload();
				MapType t2 = MapTypes.Get(name);
				Check(ref ok, t2 != null && (t2.Label ?? "").ToLowerInvariant().Contains("reload two"), "edited and reloaded: the new label is seen (" + (t2 != null ? t2.Label : "type gone") + ")");
				if (t2 != null) { float r = radius(t2); Check(ref ok, r >= 80f && r <= 90f, "the new radius range 80-90 is rolled (" + r.ToString("0") + ")"); }

				File.WriteAllText(path, GwMapTypeText("CI reload BAD", "big"));
				reload();
				MapType t3 = MapTypes.Get(name);
				Check(ref ok, MapTypeFiles.Skipped.Any(x => x.StartsWith(name + ":")), "a bad edit (radius = big) is listed as left out (" + string.Join("; ", MapTypeFiles.Skipped.ToArray()) + ")");
				Check(ref ok, t3 == null || (t3.Label ?? "").ToLowerInvariant().Contains("reload two"), "the bad edit is not used (" + (t3 == null ? "the type is out until mended" : "the last good one kept") + ")");
				Check(ref ok, MapTypes.Get("boss") != null && MapTypes.Get("boss").FromFile == null && MapTypes.Get("sandbar") != null && MapTypes.Get("sandbar").FromFile == null, "the built-in types are untouched");

				File.WriteAllText(path, GwMapTypeText("CI reload THREE", "40 - 45"));
				reload();
				MapType t4 = MapTypes.Get(name);
				Check(ref ok, t4 != null && (t4.Label ?? "").ToLowerInvariant().Contains("reload three") && !MapTypeFiles.Skipped.Any(x => x.StartsWith(name + ":")), "mended and reloaded: it is back (" + (t4 != null ? t4.Label : "still out") + ")");
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e); }
			finally
			{
				try { if (File.Exists(path)) File.Delete(path); } catch { }
				try { MapTypeFiles.LoadAll(); } catch { }
			}
			Check(ref ok, MapTypes.Get(name) == null, "the test file is deleted and the type is gone again");
			yield return null;
			if (ok) Log("PASS: map type reload"); else Fail("map type reload");
		}

		#endregion
	}
}
