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
	/// <summary>
	/// Two-player tests of the ready pieces, the boss arena, the moving / traversal pieces and the action "character"
	/// (TEST_GAPS 2026-10-13 rows 1-4 and the unlock-character row; driven by _ProjectDocs/tools/mppieces.ps1).
	/// The host makes a test island with CIGPIsland (pieces | arena | traverse | char | clean); every other command runs on
	/// the Raft it is given to (host = player 1, the box = player 2): "act as this player" commands (CIGPKeypad, CIGPLaser,
	/// CIGPEnter, CIGPKill, CIGPDie, CIGPGear, CIGPOnLift, CIGPZip, CIGPStory; CIUse / CIPick / CIOpenChest / CIReloadIsland
	/// are the older ones) and check commands (CIGPCheck, CIGPPos, CIGPInv, CIGPChar) that end in PASS / FAIL lines.
	/// CIGPCheck tokens (object names with "_" for a space), each waited for up to 8 s:
	///   hidden:N shown:N (all objects of that name)  open:N closed:N (the switch state, a mover settled)  sig:N nosig:N (a signal)
	///   unlocked:N locked:N (a keypad)  laser:hit laser:miss  story:card=1 story:cable=0  dirt=N (active dirt spots)
	///   chestopen:T chestshut:T (a chest by its title)  animals=N (live animals of the island's creature spots)
	/// </summary>
	public static partial class DevTests
	{
		const string GpPieces = "cigppieces", GpArena = "cigparena", GpTrav = "cigptrav", GpCharIsland = "cigpchar";
		static string GpRole { get { return Raft_Network.IsHost ? "host" : "client"; } }

		static IslandObject GpObj(string name, Vector3 at, string objName, params string[] kv)
		{
			Dictionary<string, string> p = ObjectProps.Defaults(name);
			p[BehaviourProps.Name] = objName;
			for (int i = 0; i + 1 < kv.Length; i += 2) p[kv[i]] = kv[i + 1];
			GameObject proto = PlaceableCatalog.Get(name);
			return new IslandObject { Name = name, Position = at, EulerRotation = Vector3.zero, Scale = proto != null ? proto.transform.localScale : Vector3.one, Props = p };
		}

		/// <summary>Per set (A: player 2 acts, B: the host acts) the same pieces; each set's signals get the letter, so the two sets tell apart.</summary>
		static readonly string[][] GpKinds =
		{
			new[] { "PlantationDoor", "door" }, new[] { "TangaroaHatchRoom_Hatch", "hatch" }, new[] { "TP_Selene_DoorCrankWheel", "crank" }, new[] { "TP_CoolingStation_Monitor_Lever", "lever" },
			new[] { "UT_WaterChallengeWheel1", "wheel" }, new[] { "WaterPipe_Short_1", "pipe" }, new[] { "UT_ElectricCableHub01", "wire" }, new[] { "Claw_ControlBoard", "claw" }, new[] { "TangaroaScaleModel", "scales" }
		};

		static List<string> GpNeeded(string kind)
		{
			var l = new List<string>();
			if (kind == "pieces") { l.AddRange(GpKinds.Select(k => k[0])); l.AddRange(new[] { "RT_PowerBox", "Note_Sign", "TP_LaserEmitter", "TP_MirrorHousing_InteractableMirror", "TP_LaserDoorPanel" }); }
			else if (kind == "arena") l.Add(MapTypes.ArenaStake);
			else if (kind == "traverse") l.AddRange(new[] { "Elevator", ContentCatalog.MacheteVines, "ZiplinePath_Landmark", "Pickup_Landmark_DirtPickup", ContentCatalog.WildHive });
			else if (kind == "char") l.Add("Note_Sign");
			return l;
		}

		static IslandFile GpPiecesFile()
		{
			IslandFile f = ScIsland(GpPieces, "Pieces Isle");
			int seed = 1;
			string[] sets = { "A", "B" };
			for (int s = 0; s < 2; s++)
			{
				for (int i = 0; i < GpKinds.Length; i++)
				{
					IslandObject o = GpObj(GpKinds[i][0], ScDry(f, new Vector2(-24f + i * 5f, -14f + s * 8f), seed++), GpKinds[i][1] + sets[s]);
					string ev = BehaviourProps.EventKey("use");
					if (o.Props.ContainsKey(ev)) o.Props[ev] = System.Text.RegularExpressions.Regex.Replace(o.Props[ev], @"signal\|\|(\w+)", "signal||$1" + sets[s]);
					f.Objects.Add(o);
				}
				f.Objects.Add(ScObj("RT_PowerBox", ScDry(f, new Vector2(-24f + 45f, -14f + s * 8f), seed++), BehaviourProps.Name, "kp" + sets[s], BehaviourProps.Use, "Type the code", CodeLock.Code, "4711", BehaviourProps.EventKey("use"), "show|safe" + sets[s]));
				f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(-24f + 50f, -14f + s * 8f), seed++), BehaviourProps.Name, "safe" + sets[s], BehaviourProps.Hidden, "1"));
			}
			f.Objects.Add(GpObj("TP_LaserEmitter", ScDry(f, new Vector2(-10f, 12f), seed++), "emitter"));
			f.Objects.Add(GpObj("TP_MirrorHousing_InteractableMirror", ScDry(f, new Vector2(0f, 12f), seed++), "mirror"));
			f.Objects.Add(GpObj("TP_LaserDoorPanel", ScDry(f, new Vector2(10f, 12f), seed++), "target"));
			return f;
		}

		static IslandFile GpTravFile()
		{
			IslandFile f = ScIsland(GpTrav, "Traverse Isle");
			f.Objects.Add(GpObj("Elevator", ScDry(f, new Vector2(-14f, -10f), 1), "lift"));
			f.Objects.Add(GpObj(ContentCatalog.MacheteVines, ScDry(f, new Vector2(-4f, -10f), 2), "vinesA"));
			f.Objects.Add(GpObj(ContentCatalog.MacheteVines, ScDry(f, new Vector2(2f, -10f), 3), "vinesB"));
			f.Objects.Add(ScObj("ZiplinePath_Landmark", ScDry(f, new Vector2(10f, -10f), 4), BehaviourProps.Name, "zip"));
			f.Objects.Add(ScObj("Pickup_Landmark_DirtPickup", ScDry(f, new Vector2(-14f, 6f), 5)));
			f.Objects.Add(ScObj("Pickup_Landmark_DirtPickup", ScDry(f, new Vector2(-8f, 6f), 6)));
			f.Objects.Add(ScObj(ContentCatalog.WildHive, ScDry(f, new Vector2(0f, 6f), 7), ObjectProps.LootItems, ContentCatalog.WildHiveLoot, ObjectProps.NoteTitle, "Hive A"));
			f.Objects.Add(ScObj(ContentCatalog.WildHive, ScDry(f, new Vector2(8f, 6f), 8), ObjectProps.LootItems, ContentCatalog.WildHiveLoot, ObjectProps.NoteTitle, "Hive B"));
			return f;
		}

		static IslandFile GpArenaFile()
		{
			float elev;
			IslandGenSettings s = MapTypes.Roll(MapTypes.Get("boss"), new System.Random(5151), out elev);
			IslandFile f = MapTypes.Create(MapTypes.Get("boss"), s, elev, GpArena);
			// (the stakes stay up 30 s, not two minutes: a test can wait that)
			IslandObject zone = f.Objects.FirstOrDefault(o => o.Name == ContentCatalog.TriggerZone && ObjectProps.Get(o.Props, ObjectProps.ZoneId) == "arena");
			if (zone != null) zone.Props[BehaviourProps.EventKey("enter")] = zone.Props[BehaviourProps.EventKey("enter")].Replace("wait||120", "wait||30");
			return f;
		}

		static IslandFile GpCharFile()
		{
			IslandFile f = ScIsland(GpCharIsland, "Character Isle");
			List<SO_Character> chars = Behaviours.Characters();
			string who = chars.Count > 0 ? chars.Last().displayName : "x";
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(0f, 0f), 1), BehaviourProps.Name, "pull", BehaviourProps.Use, "Pull", BehaviourProps.EventKey("use"), "character||" + who));
			return f;
		}

		[ConsoleCommand(name: "CIGPIsland", docs: "Dev, world (host): makes and keeps a test island for the two-player pieces tests: CIGPIsland pieces|arena|traverse|char (ready pieces in sets A/B, laser trio, keypads | boss arena with 30 s stakes | lift, vines, zipline, dirt, hives | a lever that unlocks a character), or clean (all four away)")]
		public static void GpIslandCommand(string[] args) { StartTest(GpIslandRoutine(args != null && args.Length > 0 ? args[0] : "")); }

		static IEnumerator GpIslandRoutine(string kind)
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("gp island: run in a world, as the host"); yield break; }
			string[] all = { GpPieces, GpArena, GpTrav, GpCharIsland };
			if (kind == "clean")
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => all.Contains(x.HostName)).Select(x => x.Id).ToList(), true);
				foreach (string n in all) try { if (System.IO.File.Exists(IslandSpawner.PathFor(n))) System.IO.File.Delete(IslandSpawner.PathFor(n)); } catch { }
				Log("PASS: gp island clean");
				yield break;
			}
			string name = kind == "pieces" ? GpPieces : kind == "arena" ? GpArena : kind == "traverse" ? GpTrav : kind == "char" ? GpCharIsland : null;
			if (name == null) { Fail("gp island: CIGPIsland pieces|arena|traverse|char|clean"); yield break; }
			yield return PlaceableCatalog.EnsureLoaded(GpNeeded(kind));
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => x.HostName == name).Select(x => x.Id).ToList(), true);
			IslandFile f;
			try
			{
				f = kind == "pieces" ? GpPiecesFile() : kind == "arena" ? GpArenaFile() : kind == "traverse" ? GpTravFile() : GpCharFile();
				f.Save(IslandSpawner.PathFor(name));
			}
			catch (Exception ex) { Fail("gp island " + name + ": " + ex); yield break; }
			// (open sea further out rather than a raft jump: islands go to player 2 relative to the host's raft - as ScMpSpawn)
			Vector3? spot = null;
			// (within 750 m: player 2 unloads islands past 800 m; mppieces.ps1 sails on and asks again when the sea there is full)
			for (float d = 400f; !spot.HasValue && d <= 750f; d += 175f)
			{
				Vector3? s = CustomIslandSpawner.FindClearSpot(CustomIslandSpawner.RaftPosition.Value, CustomIslandSpawner.LandRadius(name), d);
				if (s.HasValue) spot = new Vector3(s.Value.x, 0f, s.Value.z);
			}
			if (!spot.HasValue) { Fail("mp island " + name + ": no open sea near the raft"); yield break; }
			var made = new List<IslandWorldState.Entry>();
			yield return ScBring(name, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e != null && e.Root != null) Log("PASS: mp island '" + name + "' " + ScFlat(e.Position, CustomIslandSpawner.RaftPosition.Value).ToString("F0") + " m from the raft");
			else Fail("mp island " + name + ": it didn't come");
		}

		// ---- checks ----

		static List<IslandObjectRef> GpRefs(IslandWorldState.Entry e, string name)
		{
			if (e == null || e.Root == null) return new List<IslandObjectRef>();
			string spaced = name.Replace('_', ' ');
			return e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, spaced, StringComparison.OrdinalIgnoreCase)).ToList();
		}

		static bool GpOpen(IslandWorldState.Entry e, IslandObjectRef r)
		{
			ObjectState s;
			return e.State.TryGetValue(Behaviours.StateBase + r.Index, out s) && s.Yield == 1;
		}

		static bool GpLaserHits(IslandWorldState.Entry e, out string detail)
		{
			IslandObjectRef emitter = ScObjOf(e, "emitter"), target = ScObjOf(e, "target");
			LaserBeam beam = emitter != null ? emitter.GetComponent<LaserBeam>() : null;
			detail = "no beam";
			if (beam == null || target == null) return false;
			beam.Trace();
			detail = LaserPath(beam);
			return beam.Hit == target;
		}

		static bool GpEval(IslandWorldState.Entry e, string tok, out string detail)
		{
			detail = "";
			int colon = tok.IndexOf(':'), eq = tok.IndexOf('=');
			string kind = colon >= 0 && (eq < 0 || colon < eq) ? tok.Substring(0, colon) : eq >= 0 ? tok.Substring(0, eq) : tok;
			string arg = colon >= 0 && (eq < 0 || colon < eq) ? tok.Substring(colon + 1) : eq >= 0 ? tok.Substring(eq + 1) : "";
			int want;
			if (kind == "dirt" || kind == "animals")
			{
				int.TryParse(arg, out want);
				int have = kind == "dirt" ? PickupsOf(e, "DirtPickup").Count(p => p.gameObject.activeInHierarchy)
					: AnimalsOf(e, "", 150f).Count(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead);
				detail = "have " + have;
				return have == want;
			}
			if (kind == "story")
			{
				int q = arg.IndexOf('=');
				if (q < 0) return false;
				int.TryParse(arg.Substring(q + 1), out want);
				int have = StoryBook.Count(StoryItems.Ref(ReadyPieces.ItemOf(arg.Substring(0, q) == "card" ? "PlantationDoor" : "UT_ElectricCableHub01")));
				detail = "have " + have;
				return have == want;
			}
			if (kind == "laser")
			{
				bool hit = GpLaserHits(e, out detail);
				return arg == "hit" ? hit : !hit;
			}
			if (kind == "sig" || kind == "nosig")
			{
				bool has = e.State.ContainsKey(Behaviours.SignalKey(arg));
				return kind == "sig" ? has : !has;
			}
			if (kind == "chestopen" || kind == "chestshut")
			{
				string title = arg.Replace('_', ' ');
				LootCrate chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c =>
				{
					IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>();
					return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle).Equals(title, StringComparison.OrdinalIgnoreCase);
				});
				if (chest == null) { detail = "no chest"; return false; }
				return kind == "chestopen" ? chest.Looted : !chest.Looted;
			}
			List<IslandObjectRef> refs = GpRefs(e, arg);
			if (refs.Count == 0) { detail = "no object '" + arg + "'"; return false; }
			detail = refs.Count + " object(s)";
			switch (kind)
			{
				case "hidden": return refs.All(r => !r.gameObject.activeInHierarchy);
				case "shown": return refs.All(r => r.gameObject.activeInHierarchy);
				case "open":
				case "closed":
					{
						bool open = kind == "open";
						return refs.All(r =>
						{
							IslandBehaviour b = r.GetComponent<IslandBehaviour>();
							return GpOpen(e, r) == open && (b == null || (open ? b.Current > 0.99f : b.Current < 0.01f));
						});
					}
				case "unlocked": return refs.All(r => CodeLock.Unlocked(e, r.Index));
				case "locked": return refs.All(r => !CodeLock.Unlocked(e, r.Index));
			}
			detail = "unknown token";
			return false;
		}

		[ConsoleCommand(name: "CIGPCheck", docs: "Dev, in game (either player): checks what this machine shows of a test island: CIGPCheck <island> <token>... - tokens hidden:N shown:N open:N closed:N sig:N nosig:N unlocked:N locked:N laser:hit laser:miss story:card=1 dirt=1 chestopen:Hive_A animals=0; each waited for up to 8 s; PASS / FAIL per token")]
		public static void GpCheckCommand(string[] args) { StartTest(GpCheckRoutine(args)); }

		static IEnumerator GpCheckRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) yield break;
			bool ok = true;
			foreach (string tok in args.Skip(1))
			{
				string detail = "";
				bool pass = false;
				for (float t = 0f; t <= 8f; t += 0.5f)
				{
					try { pass = GpEval(e, tok, out detail); } catch (Exception ex) { pass = false; detail = ex.Message; }
					if (pass) break;
					yield return new WaitForSeconds(0.5f);
				}
				Check(ref ok, pass, GpRole + ": " + tok + (detail.Length > 0 ? " (" + detail + ")" : ""));
			}
			if (ok) Log("PASS: gp check " + GpRole); else Fail("gp check " + GpRole);
		}

		// ---- act as this player ----

		[ConsoleCommand(name: "CIGPStory", docs: "Dev, in game (either player): the crew's story items: CIGPStory give|take card|cable <n> (card = Tangaroa keycard, cable = electrical cable); shared with the other player")]
		public static void GpStoryCommand(string[] args) { StartTest(GpStoryRoutine(args)); }

		static IEnumerator GpStoryRoutine(string[] args)
		{
			if (args == null || args.Length < 2) { Fail("CIGPStory give|take card|cable <n>"); yield break; }
			int n = 1;
			if (args.Length > 2) int.TryParse(args[2], out n);
			string id = StoryItems.Ref(ReadyPieces.ItemOf(args[1] == "card" ? "PlantationDoor" : "UT_ElectricCableHub01"));
			if (args[0] == "give") StoryBook.Give(id, n); else StoryBook.Take(id, n);
			yield return new WaitForSeconds(1f);
			Log("GP story " + args[1] + " " + StoryBook.Count(id) + " (" + GpRole + ")");
			Log("PASS: gp story " + GpRole);
		}

		[ConsoleCommand(name: "CIGPKeypad", docs: "Dev, in game (either player): this player uses a keypad and types a code: CIGPKeypad <island> <keypad name> right|wrong")]
		public static void GpKeypadCommand(string[] args) { StartTest(GpKeypadRoutine(args)); }

		static IEnumerator GpKeypadRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) yield break;
			IslandObjectRef pad = args.Length > 1 ? ScObjOf(e, args[1]) : null;
			if (pad == null) { Fail("gp keypad: no such keypad"); yield break; }
			string code = ObjectProps.Get(pad.Props, CodeLock.Code);
			bool right = args.Length < 3 || args[2] != "wrong";
			PutPlayerNear(pad.transform, 1.2f);
			CodeLock.Use(e, pad.Index, code, pad.transform);
			yield return new WaitForSeconds(0.4f);
			bool ok = true;
			Check(ref ok, CodeLock.IsOpen || CodeLock.Unlocked(e, pad.Index), GpRole + ": the keypad opened");
			foreach (char c in right ? code : "0000") CodeLock.Press(c.ToString());
			CodeLock.Press("OK");
			yield return new WaitForSeconds(0.8f);
			if (right) Check(ref ok, CodeLock.Unlocked(e, pad.Index), GpRole + ": the right code unlocked it here");
			else { Check(ref ok, !CodeLock.Unlocked(e, pad.Index), GpRole + ": a wrong code keeps it shut"); CodeLock.Press("C"); }
			if (ok) Log("PASS: gp keypad " + GpRole); else Fail("gp keypad " + GpRole);
		}

		[ConsoleCommand(name: "CIGPLaser", docs: "Dev, in game (either player): lines the laser trio of 'cigppieces' up on this machine (emitter and target raised, the mirror 6 m on) as CILaser does, so the beam reaches the target by the mirror; once per load of the island: CIGPLaser <island>")]
		public static void GpLaserCommand(string[] args) { StartTest(GpLaserRoutine(args)); }

		static IEnumerator GpLaserRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) yield break;
			IslandObjectRef emitter = ScObjOf(e, "emitter"), mirror = ScObjOf(e, "mirror"), target = ScObjOf(e, "target");
			LaserBeam beam = emitter != null ? emitter.GetComponent<LaserBeam>() : null;
			if (beam == null || mirror == null || target == null) { Fail("gp laser: emitter, mirror or target missing"); yield break; }
			bool ok = true;
			IslandBehaviour mb = mirror.GetComponent<IslandBehaviour>();
			// (a mirror already turned - the world loaded with it so - is put back to its placed pose first, then turned again after)
			bool wasOpen = mb != null && mb.Target > 0.5f;
			if (mb != null) mb.Snap(false);
			emitter.transform.position += Vector3.up * 20f;
			Physics.SyncTransforms();
			Vector3 o = beam.Origin, dir = emitter.transform.forward;
			target.transform.position += Vector3.up * 40f;
			mirror.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up) * mirror.transform.rotation;
			if (mb != null) HarmonyLib.Traverse.Create(mb).Field("startRot").SetValue(mirror.transform.localRotation);
			LaserMoveTo(mirror.transform, o + dir * 6f);
			beam.Trace();
			Check(ref ok, beam.Mirrors.Contains(mirror) && beam.Points.Count >= 3, GpRole + ": the beam reaches the mirror and goes on (" + LaserPath(beam) + ")");
			if (beam.Points.Count >= 3)
			{
				Vector3 at = beam.Points[1], outDir = (beam.Points[2] - beam.Points[1]).normalized;
				LaserMoveTo(target.transform, at + outDir * 5f);
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, beam.Hit == target, GpRole + ": by the mirror the beam reaches the target (" + (beam.Hit != null ? beam.Hit.Name : "nothing") + ")");
			}
			if (mb != null && wasOpen) mb.Snap(true);
			if (ok) Log("PASS: gp laser " + GpRole); else Fail("gp laser " + GpRole);
		}

		[ConsoleCommand(name: "CIGPEnter", docs: "Dev, in game (either player): this player steps into a trigger zone: CIGPEnter <island> <zone id>")]
		public static void GpEnterCommand(string[] args) { StartTest(GpEnterRoutine(args)); }

		static IEnumerator GpEnterRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) yield break;
			bool entered = args.Length > 1 && ScEnterZone(e, args[1]);
			yield return new WaitForSeconds(1f);
			if (entered) Log("PASS: gp enter " + GpRole); else Fail("gp enter " + GpRole + ": no such zone");
		}

		[ConsoleCommand(name: "CIGPKill", docs: "Dev, in game (either player): this player defeats every live animal of the island's creature spots, as a weapon would (the damage goes to the host); waits up to 45 s for them to come: CIGPKill <island>")]
		public static void GpKillCommand(string[] args) { StartTest(GpKillRoutine(args)); }

		static IEnumerator GpKillRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) yield break;
			List<AI_NetworkBehaviour> animals = null;
			for (float until = Time.realtimeSinceStartup + 45f; Time.realtimeSinceStartup < until; )
			{
				animals = AnimalsOf(e, "", 150f).Where(a => a.networkEntity != null && !a.networkEntity.IsDead).ToList();
				if (animals.Count > 0) break;
				yield return new WaitForSeconds(0.5f);
			}
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (animals == null || animals.Count == 0 || host == null) { Fail("gp kill " + GpRole + ": no live animal at '" + e.HostName + "'"); yield break; }
			PutPlayerNear(animals[0].transform);
			foreach (AI_NetworkBehaviour a in animals)
				host.DamageEntity(a.networkEntity, a.transform, 9999f, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("GP killed " + animals.Count + " animal(s) (" + GpRole + ": " + (Raft_Network.IsHost ? "the host's own hit" : "sent to the host") + ")");
			Log("PASS: gp kill " + GpRole);
		}

		[ConsoleCommand(name: "CIGPDie", docs: "Dev, in game (either player): this player dies (Raft's own death) and respawns")]
		public static void GpDieCommand(string[] args) { StartTest(GpDieRoutine()); }

		static IEnumerator GpDieRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Player body = player != null ? player.GetComponentInChildren<Player>(true) : null;
			if (body == null) { Fail("gp die: no player body"); yield break; }
			bool ok = true;
			body.Kill(false);
			for (float t = 0; t < 6f && !body.IsDead; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			Check(ref ok, body.IsDead, GpRole + ": the player died");
			yield return new WaitForSecondsRealtime(2f);
			BedManager beds = ComponentManager<BedManager>.Value ?? UnityEngine.Object.FindObjectOfType<BedManager>() ?? Resources.FindObjectsOfTypeAll<BedManager>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (beds != null) beds.Button_Respawn(); else body.RespawnWithoutBed(false);
			for (float t = 0; t < 10f && body.IsDead; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			yield return new WaitForSecondsRealtime(2f);
			yield return EnsureAlive();
			Check(ref ok, !body.IsDead, GpRole + ": the player is up again");
			if (ok) Log("PASS: gp die " + GpRole); else Fail("gp die " + GpRole);
		}

		[ConsoleCommand(name: "CIGPGear", docs: "Dev, in game (either player): the local player's tools: CIGPGear machete|zipline|none (none takes both away)")]
		public static void GpGearCommand(string[] args) { StartTest(GpGearRoutine(args != null && args.Length > 0 ? args[0] : "none")); }

		static IEnumerator GpGearRoutine(string what)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("gp gear: no player"); yield break; }
			PlayerInventory inv = player.Inventory;
			inv.RemoveItem(ContentCatalog.MacheteItem, 99);
			inv.RemoveItem(ContentCatalog.ZiplineItem, 99);
			if (what == "machete") inv.AddItem(ContentCatalog.MacheteItem, 1);
			else if (what == "zipline") inv.AddItem(ContentCatalog.ZiplineItem, 1);
			yield return null;
			Log("PASS: gp gear " + what + " " + GpRole);
		}

		[ConsoleCommand(name: "CIGPInv", docs: "Dev, in game (either player): how many of the local player's items have this part in their name: CIGPInv <part> - logs GP inv <part> <n>")]
		public static void GpInvCommand(string[] args) { StartTest(GpInvRoutine(args != null && args.Length > 0 ? args[0] : "")); }

		static IEnumerator GpInvRoutine(string part)
		{
			int n = Items(RAPI.GetLocalPlayer()).Where(kv => kv.Key.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0).Sum(kv => kv.Value);
			Log("GP inv " + part + " " + n + " (" + GpRole + ")");
			Log("PASS: gp inv " + GpRole);
			yield return null;
		}

		[ConsoleCommand(name: "CIGPPos", docs: "Dev, in game (either player): height of the local player and of a named object, and their flat distance: CIGPPos <island> <name> - logs GP pos <name> player y <y> object y <y> flat <d>")]
		public static void GpPosCommand(string[] args) { StartTest(GpPosRoutine(args)); }

		static IEnumerator GpPosRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			Network_Player player = RAPI.GetLocalPlayer();
			IslandObjectRef r = e != null && args.Length > 1 ? ScObjOf(e, args[1]) : null;
			if (e == null) yield break;
			if (r == null || player == null) { Fail("gp pos: no such object"); yield break; }
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			Log("GP pos " + args[1] + " player y " + player.transform.position.y.ToString("F2", ci) + " object y " + r.transform.position.y.ToString("F2", ci) + " flat " + FlatDistance(player.transform.position, r.transform.position).ToString("F1", ci) + " (" + GpRole + ")");
			Log("PASS: gp pos " + GpRole);
		}

		[ConsoleCommand(name: "CIGPOnLift", docs: "Dev, in game (either player): puts the local player on top of a lift and says whether the lift carries them (beh.carry): CIGPOnLift <island> <name>")]
		public static void GpOnLiftCommand(string[] args) { StartTest(GpOnLiftRoutine(args)); }

		static IEnumerator GpOnLiftRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			Network_Player player = RAPI.GetLocalPlayer();
			IslandObjectRef r = e != null && args.Length > 1 ? ScObjOf(e, args[1]) : null;
			if (e == null) yield break;
			if (r == null || player == null) { Fail("gp onlift: no such lift"); yield break; }
			Renderer[] rs = r.GetComponentsInChildren<Renderer>().Where(x => !(x is LineRenderer)).ToArray();
			IslandBehaviour mb = r.GetComponent<IslandBehaviour>();
			bool carried = false;
			// a moving lift can slide out from under the drop point in the second it takes to land, so try up to 3 times
			for (int tries = 0; tries < 3 && !carried; tries++)
			{
				Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(r.transform.position, Vector3.one);
				foreach (Renderer x in rs) b.Encapsulate(x.bounds);
				Vector3 at = new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
				// the other player may still stand on the lift (they rode it last): landing on their head, the ray misses the lift, so land beside them
				foreach (Network_Player o in UnityEngine.Object.FindObjectsOfType<Network_Player>().Where(x => x != null && x != player))
				{
					if (FlatDistance(o.transform.position, at) > 1f || o.transform.position.y < b.min.y - 1f || o.transform.position.y > b.max.y + 3f) continue;
					Vector3 dir = b.extents.x >= b.extents.z ? Vector3.right : Vector3.forward;
					if (Vector3.Dot(o.transform.position - b.center, dir) > 0f) dir = -dir;
					at += dir * Mathf.Min(1.2f, Mathf.Max(b.extents.x, b.extents.z) * 0.6f);
				}
				PlayerMove.To(player, at);
				KeepAlive(player);
				yield return new WaitForSeconds(1f);
				carried = mb != null && mb.Carry && mb.Carries(player.transform);
			}
			Log("GP onlift carried=" + carried + " (" + GpRole + ")");
			bool ok = true;
			Check(ref ok, carried, GpRole + ": the lift carries the player standing on it");
			if (ok) Log("PASS: gp onlift " + GpRole); else Fail("gp onlift " + GpRole);
		}

		[ConsoleCommand(name: "CIGPZip", docs: "Dev, in game (either player): the local player rides the island's first zipline line for 2 s (needs the zipline tool: CIGPGear zipline); PASS when it moved them: CIGPZip <island>")]
		public static void GpZipCommand(string[] args) { StartTest(GpZipRoutine(args)); }

		static IEnumerator GpZipRoutine(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			Network_Player player = RAPI.GetLocalPlayer();
			if (e == null) yield break;
			MeshPath_Zipline line = e.Root.GetComponentsInChildren<MeshPath_Zipline>(true).FirstOrDefault();
			ZiplinePlayer zp = player != null ? player.GetComponentInChildren<ZiplinePlayer>(true) : null;
			if (line == null || zp == null) { Fail("gp zip: no zipline or no ZiplinePlayer"); yield break; }
			bool ok = true;
			Check(ref ok, player.Inventory.GetItemCount(ContentCatalog.ZiplineItem) > 0, GpRole + ": the player has the zipline tool");
			PutPlayerNear(line.transform, 1f);
			yield return new WaitForSeconds(0.5f);
			Vector3 before = player.transform.position;
			zp.AttachToZipline(line, 0);
			yield return new WaitForSeconds(2f);
			float moved = Vector3.Distance(before, player.transform.position);
			Check(ref ok, moved > 2f, GpRole + ": riding the line moved the player " + moved.ToString("F1") + " m in 2 s");
			try { zp.DetachFromCurrentZipline(); } catch { }
			if (ok) Log("PASS: gp zip " + GpRole); else Fail("gp zip " + GpRole);
		}

		static List<int> gpCharWas = new List<int>();

		[ConsoleCommand(name: "CIGPChar", docs: "Dev, in game (either player): the unlock-character test on this player's profile: CIGPChar prep (remember the profile, lock the character 'cigpchar' unlocks) | check yes|no (is it unlocked) | end (profile as it was)")]
		public static void GpCharCommand(string[] args) { StartTest(GpCharRoutine(args)); }

		static IEnumerator GpCharRoutine(string[] args)
		{
			List<SO_Character> chars = Behaviours.Characters();
			if (chars.Count == 0) { Fail("gp char: no characters found"); yield break; }
			SO_Character c = chars.Last();
			string what = args != null && args.Length > 0 ? args[0] : "";
			bool ok = true;
			if (what == "prep")
			{
				gpCharWas = CharacterManager.GetUnlockedCharacterIndexes() != null ? new List<int>(CharacterManager.GetUnlockedCharacterIndexes()) : new List<int>();
				CharacterManager.ResetCharacterUnlock(c.index);
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, !CvUnlocked().Contains(c.index), GpRole + ": '" + c.displayName + "' is locked for this player");
			}
			else if (what == "check")
			{
				bool want = args.Length > 1 && args[1] == "yes";
				bool have = false;
				for (float t = 0f; t <= 6f; t += 0.5f) { have = CvUnlocked().Contains(c.index); if (have == want) break; yield return new WaitForSeconds(0.5f); }
				Check(ref ok, have == want, GpRole + ": '" + c.displayName + "' " + (have ? "unlocked" : "locked") + " for this player (expected " + (want ? "unlocked" : "locked") + ")");
			}
			else if (what == "end")
			{
				if (gpCharWas.Contains(c.index)) CharacterManager.UnlockCharacterByIndex(c.index); else CharacterManager.ResetCharacterUnlock(c.index);
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, CvUnlocked().Contains(c.index) == gpCharWas.Contains(c.index), GpRole + ": the profile is as it was");
			}
			else { Fail("CIGPChar prep|check yes|no|end"); yield break; }
			if (ok) Log("PASS: gp char " + what + " " + GpRole); else Fail("gp char " + what + " " + GpRole);
		}
	}
}
