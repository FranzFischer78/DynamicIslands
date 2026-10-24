using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommandUndoRedo;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using RuntimeGizmos;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Coverage tests (COV1-COV13): features that had no dev test, or a weak one. Collision modes, the actions character and
	/// sound, the spin around an object's own axis, the giant rogue shark's bite, the journal/stats keys, the update check,
	/// the editor's shortcuts waiting behind a window, the Share window's link, Long voyage's spacing, the PC check on fake
	/// paths, the ambience zones in a world and the notice of a patch that failed. Each says in its docs line where it runs.
	/// Every test puts back what it changed (profile unlocks, world_rules.txt, library.txt, test islands, sharks, flags).
	/// </summary>
	public static partial class DevTests
	{
		#region Helpers

		/// <summary>The bytes of a file, or null when it isn't there (to put it back after a test).</summary>
		static byte[] CvBytes(string path)
		{
			try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
			catch { return null; }
		}

		/// <summary>Puts a file back as CvBytes found it (null: it wasn't there).</summary>
		static void CvRestore(string path, byte[] bytes)
		{
			try
			{
				if (bytes == null) { if (File.Exists(path)) File.Delete(path); }
				else File.WriteAllBytes(path, bytes);
			}
			catch (Exception e) { Log("(could not put " + path + " back: " + e.Message + ")"); }
		}

		static bool CvInTestWorld(string who)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || RAPI.GetLocalPlayer() == null) { Fail(who + ": run in a world"); return false; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail(who + ": only in a test world 'CI ...' (it moves the player)"); return false; }
			return true;
		}

		/// <summary>An FMOD one-shot event (3D, not music or menu sounds) at least minMs long, else the longest below 40 s; null when there is none.</summary>
		static string CvOneShot(int minMs)
		{
			string best = null;
			int bestLength = 0;
			foreach (string path in SoundLibrary.Events)
			{
				string low = path.ToLowerInvariant();
				if (low.Contains("music") || low.Contains("/ui") || low.Contains("menu") || low.Contains("voice") || low.Contains("story") || low.Contains("radio") || low.Contains("snapshot")) continue;
				try
				{
					FMOD.Studio.EventDescription d = FMODUnity.RuntimeManager.GetEventDescription(path);
					bool oneshot, is3d;
					int ms, instances;
					if (d.isOneshot(out oneshot) != FMOD.RESULT.OK || !oneshot || d.is3D(out is3d) != FMOD.RESULT.OK || !is3d || d.getLength(out ms) != FMOD.RESULT.OK) continue;
					if (d.getInstanceCount(out instances) != FMOD.RESULT.OK || instances != 0 || ms > 40000) continue;
					if (ms >= minMs) return path;
					if (ms > bestLength) { best = path; bestLength = ms; }
				}
				catch { }
			}
			return best;
		}

		/// <summary>The player on the ground at centre + (dx, 0, 0) (the ground there, else the centre's height), then a wait.</summary>
		static IEnumerator CvStandAt(Network_Player player, Vector3 centre, float dx, float wait)
		{
			Vector3 at = centre + new Vector3(dx, 0f, 0f);
			RaycastHit hit;
			if (Physics.Raycast(at + Vector3.up * 40f, Vector3.down, out hit, 120f, 1 << IslandSpawner.TerrainLayer)) at.y = hit.point.y + 1.2f;
			else at.y = centre.y + 1.2f;
			KeepAlive(player);
			PlayerMove.To(player, at);
			yield return new WaitForSeconds(wait);
		}

		/// <summary>A sample-island copy with its objects cleared and one usable object per entry (name, settings): "lever" style props at dry spots 10 m apart.</summary>
		static IslandFile CvLeverIsland(string island, params string[][] levers)
		{
			IslandFile f = ScIsland(island, "Coverage");
			f.Objects.Clear();
			Vector3 b = ScDry(f, Vector2.zero, 1);
			for (int i = 0; i < levers.Length; i++)
			{
				var kv = new List<string> { BehaviourProps.Name, levers[i][0], BehaviourProps.Use, "Pull", BehaviourProps.EventPrefix + "use", levers[i][1] };
				Vector3 at = b + new Vector3(i * 6f, 0f, 0f);
				at.y = ScGround(f, new Vector2(at.x, at.z));
				f.Objects.Add(ScObj("Log", at, kv.ToArray()));
			}
			f.Save(IslandSpawner.PathFor(island));
			return f;
		}

		#endregion

		#region COV1 - Collision modes box, solid and none

		[ConsoleCommand(name: "CICollisionModes", docs: "Dev, world (host, a test world 'CI ...'): COV1 - four crates on an island with col.mode box, solid, none and solid-without-a-collider: the colliders each mode makes (box: Raft's own off and one CI_Solid box on the ground's layer around it; solid: Raft's own kept, a box only where it has none; none: all off), the real player walking into each (stopped by box and solid, through none), the island unloaded and loaded again and the saved file keep them (the whole world save and load isn't run)")]
		public static void CollisionModesCommand() { StartTest(CollisionModesRoutine()); }

		/// <summary>Whether an object (the catalog's, before it is spawned) has a collider that isn't a trigger. (The spawner switches every collider on, so enabled or not doesn't matter here.)</summary>
		static bool CvSolid(GameObject go) { return go != null && go.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger); }

		static List<Collider> CvOwnColliders(GameObject go) { return go.GetComponentsInChildren<Collider>(true).Where(c => c.gameObject.name != "CI_Solid").ToList(); }

		static List<BoxCollider> CvSolidBoxes(GameObject go) { return go.GetComponentsInChildren<BoxCollider>(true).Where(c => c.gameObject.name == "CI_Solid").ToList(); }

		/// <summary>The local player walks along +x into the crate from 3 m before its near face, 0.08 m a frame, until 3 m past its far face. result[0]: how far past the crate's middle they got (m).</summary>
		static IEnumerator CvWalkInto(Network_Player player, Vector3 centre, float half, float[] result)
		{
			Vector3 start = centre - Vector3.right * (half + 3f);
			yield return CvStandAt(player, start, 0f, 0.8f);
			CharacterController cc = player.PersonController.controller;
			int steps = Mathf.CeilToInt((half * 2f + 6f) / 0.08f);
			for (int i = 0; i < steps; i++)
			{
				if (cc != null && cc.enabled) cc.Move(Vector3.right * 0.08f);
				yield return null;
			}
			result[0] = player.transform.position.x - centre.x;
		}

		static IEnumerator CollisionModesRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (!CvInTestWorld("collision modes")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost || player.PersonController == null) { Fail("collision modes: run as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "cicovcol";
			string[] solidCandidates = { "VP_MetalCrate01", "VP_MetalCrate02", "UT_CoveredCrate02", "FL_Crate", "TP_Moontown_TarpCrate01" };
			string[] hollowCandidates = { "VP_ExplosiveBarrel_Pile", "CaravanRocket", "UT_MarketStackBox01" };
			yield return PlaceableCatalog.EnsureLoaded(solidCandidates.Concat(hollowCandidates));
			string crate = solidCandidates.FirstOrDefault(n => CvSolid(PlaceableCatalog.Get(n)));
			string hollow = hollowCandidates.FirstOrDefault(n => PlaceableCatalog.Get(n) != null && !CvSolid(PlaceableCatalog.Get(n)));
			if (crate == null) { Fail("collision modes: none of " + string.Join(", ", solidCandidates) + " has a collider in this Raft"); yield break; }
			Log("  crate " + crate + (hollow != null ? ", a prop without a collider: " + hollow : ", (no prop without a collider found: the solid-box part is skipped)"));

			IslandFile f = ScIsland(isl, "Collision Isle");
			f.Objects.Clear();
			Vector3 b = ScDry(f, Vector2.zero, 1);
			// (rows 7 m apart around the dry spot, each walked along +x)
			var rows = new List<string[]> { new[] { "cbox", crate, "box" }, new[] { "csolid", crate, "solid" }, new[] { "cnone", crate, "none" } };
			if (hollow != null) rows.Add(new[] { "chollow", hollow, "solid" });
			for (int i = 0; i < rows.Count; i++)
			{
				Vector3 at = b + new Vector3(0f, 0f, (i - 1) * 7f);
				at.y = ScGround(f, new Vector2(at.x, at.z));
				f.Objects.Add(ScObj(rows[i][1], at, BehaviourProps.Name, rows[i][0], BehaviourProps.Collision, rows[i][2]));
			}
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("collision modes: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("collision modes: the island didn't come"); yield break; }
			try
			{
				yield return new WaitForFixedUpdate();
				Physics.SyncTransforms();

				// The saved file has the modes
				IslandFile back = IslandFile.Load(IslandSpawner.PathFor(isl));
				Check(ref ok, back.Objects.Count == rows.Count && Enumerable.Range(0, rows.Count).All(i => ObjectProps.Get(back.Objects[i].Props, BehaviourProps.Collision) == rows[i][2]),
					"the saved island keeps col.mode: " + string.Join(", ", back.Objects.Select(o => ObjectProps.Get(o.Props, BehaviourProps.Collision)).ToArray()));

				// The colliders each mode makes
				Func<string, string> colliders = name =>
				{
					IslandObjectRef r = ScObjOf(e, name);
					if (r == null) return "no object";
					List<Collider> own = CvOwnColliders(r.gameObject);
					return own.Count(c => c.enabled && !c.isTrigger) + "/" + own.Count + " own enabled, " + CvSolidBoxes(r.gameObject).Count + " CI_Solid";
				};
				Func<IslandWorldState.Entry, bool> checkModes = en =>
				{
					bool good = true;
					IslandObjectRef rb = ScObjOf(en, "cbox"), rs = ScObjOf(en, "csolid"), rn = ScObjOf(en, "cnone"), rh = ScObjOf(en, "chollow");
					if (rb == null || rs == null || rn == null) { Check(ref good, false, "the crates are on the island"); return false; }
					List<Collider> ownB = CvOwnColliders(rb.gameObject), ownS = CvOwnColliders(rs.gameObject), ownN = CvOwnColliders(rn.gameObject);
					List<BoxCollider> boxB = CvSolidBoxes(rb.gameObject);
					Bounds rend = new Bounds(rb.transform.position, Vector3.zero);
					foreach (Renderer rr in rb.GetComponentsInChildren<Renderer>(true)) rend.Encapsulate(rr.bounds);
					Check(ref good, ownB.Count > 0 && ownB.All(c => !c.enabled) && boxB.Count == 1 && boxB[0].enabled && !boxB[0].isTrigger && boxB[0].gameObject.layer == IslandSpawner.TerrainLayer,
						"box: Raft's own colliders off, one CI_Solid box on the ground's layer (" + colliders("cbox") + ")");
					Check(ref good, boxB.Count == 1 && Mathf.Abs(boxB[0].bounds.size.y - rend.size.y) < 0.5f && Mathf.Abs(boxB[0].bounds.size.x - rend.size.x) < 0.5f && Mathf.Abs(boxB[0].bounds.size.z - rend.size.z) < 0.5f,
						"box: the box is as big as the crate (" + (boxB.Count == 1 ? boxB[0].bounds.size.ToString("F1") : "none") + " against " + rend.size.ToString("F1") + ")");
					Check(ref good, ownS.Any(c => c.enabled && !c.isTrigger) && CvSolidBoxes(rs.gameObject).Count == 0, "solid on a crate with a collider: Raft's own kept, no extra box (" + colliders("csolid") + ")");
					Check(ref good, ownN.Count > 0 && ownN.All(c => !c.enabled) && CvSolidBoxes(rn.gameObject).Count == 0, "none: every collider off, no box (" + colliders("cnone") + ")");
					if (hollow != null)
						Check(ref good, rh != null && !CvOwnColliders(rh.gameObject).Any(c => c.enabled && !c.isTrigger) && CvSolidBoxes(rh.gameObject).Count == 1 && CvSolidBoxes(rh.gameObject)[0].enabled,
							"solid on '" + hollow + "', which has no collider: a box is made (" + (rh != null ? colliders("chollow") : "no object") + ")");
					return good;
				};
				ok &= checkModes(e);

				// The real player walking into each
				var walk = new Dictionary<string, float>();
				foreach (string[] row in rows)
				{
					IslandObjectRef r = ScObjOf(e, row[0]);
					Bounds rend = new Bounds(r.transform.position, Vector3.zero);
					foreach (Renderer rr in r.GetComponentsInChildren<Renderer>(true)) rend.Encapsulate(rr.bounds);
					float half = Mathf.Clamp(rend.extents.x, 0.3f, 3f);
					var result = new float[1];
					yield return CvWalkInto(player, new Vector3(rend.center.x, r.transform.position.y, rend.center.z), half, result);
					walk[row[0]] = result[0];
					bool passes = result[0] > half + 0.2f, stopped = result[0] < 0f;
					if (row[2] == "none") Check(ref ok, passes, "none: the player walks through '" + row[0] + "' (ends " + result[0].ToString("F1") + " m past its middle, it is " + (half * 2f).ToString("F1") + " m wide)");
					else Check(ref ok, stopped, row[2] + ": the player is stopped by '" + row[0] + "' (ends " + result[0].ToString("F1") + " m from its middle, on the near side)");
				}

				// The island unloaded and loaded again (what the world's save does with it): the modes come back
				IslandObjectState.Capture(e);
				IslandSpawner.Despawn(e.Root);
				e.Root = null;
				yield return null;
				e.Loading = true;
				yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
				yield return new WaitForSeconds(0.5f);
				yield return new WaitForFixedUpdate();
				Physics.SyncTransforms();
				Check(ref ok, e.Root != null, "the island loads again");
				if (e.Root != null) ok &= checkModes(e);
			}
			finally
			{
				ScRemove(made, isl);
				try { OnRaftCommand(); } catch { }
			}
			if (ok) Log("PASS: collision modes"); else Fail("collision modes");
		}

		#endregion

		#region COV2 - The action character

		[ConsoleCommand(name: "CICharacterAction", docs: "Dev, world (host, a test world 'CI ...'): COV2 - levers with character|<number>, character|<NAME in capitals> and character|<no such one>: Raft's list of unlocked characters (CharacterManager.GetUnlockedCharacterIndexes) gets the character by number and by name and stays as it was for a wrong name; the profile's unlock is put back to how it was")]
		public static void CharacterActionCommand() { StartTest(CharacterActionRoutine()); }

		/// <summary>Raft's list of the characters the profile has unlocked (never null).</summary>
		static List<int> CvUnlocked() { return CharacterManager.GetUnlockedCharacterIndexes() ?? new List<int>(); }

		static IEnumerator CharacterActionRoutine()
		{
			if (!CvInTestWorld("character action")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("character action: run as the host"); yield break; }
			bool ok = true;
			List<SO_Character> chars = Behaviours.Characters();
			if (chars.Count == 0) { Fail("character action: Raft lists no characters"); yield break; }
			// One character that can be locked again (the first ones are Raft's default and stay unlocked): from the last
			SO_Character c = null;
			bool wasUnlocked = false;
			foreach (SO_Character candidate in Enumerable.Reverse(chars))
			{
				bool was = CvUnlocked().Contains(candidate.index);
				if (was) CharacterManager.ResetCharacterUnlock(candidate.index);
				if (!CvUnlocked().Contains(candidate.index)) { c = candidate; wasUnlocked = was; break; }
			}
			if (c == null) { Fail("character action: no character of Raft's can be locked (" + string.Join(", ", chars.Select(x => x.index + " " + x.displayName).ToArray()) + ")"); yield break; }
			var made = new List<IslandWorldState.Entry>();
			const string isl = "cicovchar";
			try
			{
				CvLeverIsland(isl, new[] { "bynumber", "character||" + c.index }, new[] { "byname", "character||  " + c.displayName.ToUpperInvariant() + " " }, new[] { "wrong", "character||Nobody Such" });
				Vector3? spot = ScSpot(isl, 320f);
				if (!spot.HasValue) { Fail("character action: no open sea near the raft"); yield break; }
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("character action: the island didn't come"); yield break; }
				Func<bool> unlocked = () => CvUnlocked().Contains(c.index);
				Log("  character " + c.index + " " + c.displayName + ", " + (wasUnlocked ? "unlocked" : "locked") + " in the profile before; locked for the test");
				Check(ref ok, !unlocked(), "'" + c.displayName + "' is locked before");
				List<int> others = CvUnlocked().OrderBy(i => i).ToList();
				ScUse(e, "wrong");
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, !unlocked() && CvUnlocked().OrderBy(i => i).SequenceEqual(others), "a name no character has: nothing is unlocked");
				ScUse(e, "bynumber");
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, unlocked(), "character|" + c.index + " unlocks '" + c.displayName + "' (the profile's list: " + string.Join(",", CvUnlocked().Select(i => i.ToString()).ToArray()) + ")");
				Check(ref ok, CvUnlocked().Count == others.Count + 1, "... and only that one");
				CharacterManager.ResetCharacterUnlock(c.index);
				Check(ref ok, !unlocked(), "locked again for the next lever");
				ScUse(e, "byname");
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, unlocked(), "character|" + c.displayName.ToUpperInvariant() + " (capitals, spaces round it) unlocks it by name");
			}
			finally
			{
				// (the profile as it was)
				if (wasUnlocked) CharacterManager.UnlockCharacterByIndex(c.index); else CharacterManager.ResetCharacterUnlock(c.index);
				ScRemove(made, isl);
				try { OnRaftCommand(); } catch { }
			}
			Check(ref ok, CvUnlocked().Contains(c.index) == wasUnlocked, "the profile is as it was ('" + c.displayName + "' " + (wasUnlocked ? "unlocked" : "locked") + ")");
			if (ok) Log("PASS: character action"); else Fail("character action");
		}

		#endregion

		#region COV3 - The action sound

		[ConsoleCommand(name: "CISoundAction", docs: "Dev, world (host, a test world 'CI ...'): COV3 - a lever with sound|<an FMOD event of Raft's> (the mod plays Raft's own FMOD events, no AudioSource): one more instance of that event is playing at the lever at volume 0.9, SoundLibrary.PlayOnce was called with it; a lever with an event Raft doesn't have warns and breaks nothing")]
		public static void SoundActionCommand() { StartTest(SoundActionRoutine()); }

		static IEnumerator SoundActionRoutine()
		{
			if (!CvInTestWorld("sound action")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("sound action: run as the host"); yield break; }
			bool ok = true;
			string ev = CvOneShot(3000);
			if (ev == null) { Fail("sound action: Raft has no 3D one-shot event to try"); yield break; }
			const string nothing = "event:/ci/does_not_exist";
			var made = new List<IslandWorldState.Entry>();
			const string isl = "cicovsound";
			var warnings = new List<string>();
			Application.LogCallback watch = (text, trace, type) => { if (type == LogType.Warning && text.Contains("Can't play")) warnings.Add(text); };
			Application.logMessageReceived += watch;
			FMOD.Studio.EventInstance[] started = new FMOD.Studio.EventInstance[0];
			try
			{
				CvLeverIsland(isl, new[] { "bell", "sound||" + ev }, new[] { "ghost", "sound||" + nothing });
				Vector3? spot = ScSpot(isl, 320f);
				if (!spot.HasValue) { Fail("sound action: no open sea near the raft"); yield break; }
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.FirstOrDefault();
				if (e == null || e.Root == null) { Fail("sound action: the island didn't come"); yield break; }
				ObjAction parsed = ObjAction.Parse("sound||" + ev);
				Check(ref ok, parsed != null && parsed.Verb == "sound" && parsed.Arg == ev && parsed.Describe().StartsWith("play "), "the action reads as '" + (parsed != null ? parsed.Describe() : "?") + "'");

				FMOD.Studio.EventDescription desc = FMODUnity.RuntimeManager.GetEventDescription(ev);
				int before, after;
				desc.getInstanceCount(out before);
				int played = SoundLibrary.Played;
				ScUse(e, "bell");
				FMODUnity.RuntimeManager.StudioSystem.flushCommands();
				desc.getInstanceCount(out after);
				Check(ref ok, SoundLibrary.Played == played + 1 && SoundLibrary.LastPlayed == ev, "using the lever plays " + ev + " (PlayOnce called " + (SoundLibrary.Played - played) + " time(s), last '" + SoundLibrary.LastPlayed + "')");
				Check(ref ok, after == before + 1, "Raft's sound engine has one more instance of it (" + before + " -> " + after + ")");
				// where and how loud
				Vector3 lever = ScObjOf(e, "bell").transform.position;
				FMOD.Studio.EventInstance[] list;
				desc.getInstanceList(out list);
				started = list ?? new FMOD.Studio.EventInstance[0];
				bool found = false;
				string seen = "no instance";
				foreach (FMOD.Studio.EventInstance inst in started)
				{
					if (!inst.isValid()) continue;
					FMOD.ATTRIBUTES_3D a;
					float volume;
					FMOD.Studio.PLAYBACK_STATE state;
					if (inst.get3DAttributes(out a) != FMOD.RESULT.OK || inst.getVolume(out volume) != FMOD.RESULT.OK || inst.getPlaybackState(out state) != FMOD.RESULT.OK) continue;
					var at = new Vector3(a.position.x, a.position.y, a.position.z);
					seen = "at " + at.ToString("F1") + ", volume " + volume.ToString("F2") + ", " + state + "; lever at " + lever.ToString("F1");
					if ((at - lever).magnitude < 0.6f && Mathf.Abs(volume - 0.9f) < 0.03f && state != FMOD.Studio.PLAYBACK_STATE.STOPPED) found = true;
				}
				Check(ref ok, found, "... playing at the lever, volume 0.9 (" + seen + ")");

				// An event Raft doesn't have
				int playedBefore = SoundLibrary.Played;
				warnings.Clear();
				ScUse(e, "ghost");
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, warnings.Any(w => w.Contains(nothing)), "an event Raft doesn't have: a warning, no error (" + (warnings.Count > 0 ? warnings[0] : "none") + ")");
				Check(ref ok, SoundLibrary.Played == playedBefore + 1, "... and the lever worked as far as asking for it");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				foreach (FMOD.Studio.EventInstance inst in started) { try { if (inst.isValid()) inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); } catch { } }
				ScRemove(made, isl);
				try { OnRaftCommand(); } catch { }
			}
			if (ok) Log("PASS: sound action"); else Fail("sound action");
		}

		#endregion

		#region COV4 - beh.spinOwn

		[ConsoleCommand(name: "CISpinOwn", docs: "Dev, anywhere: COV4 - a tilted block with beh.spin 90: beh.spinOwn 1 turns it about its own up axis (the turn, seen from the block, is about +Y; seen from the world, about the tilted axis), beh.spinOwn 0 about the world's up; both 90 degrees in a second and neither moves")]
		public static void SpinOwnCommand() { StartTest(SpinOwnRoutine()); }

		static IEnumerator SpinOwnRoutine()
		{
			bool ok = true;
			var root = new GameObject("CI_SpinOwnTest").transform;
			root.position = new Vector3(0f, -400f, 0f);
			try
			{
				Quaternion tilt = Quaternion.Euler(40f, 25f, 15f);
				float tiltAngle = Vector3.Angle(Vector3.up, tilt * Vector3.up);
				var blocks = new Dictionary<string, Transform>();
				foreach (string mode in new[] { "1", "0" })
				{
					GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
					go.name = "CI_Spin" + mode;
					UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
					go.transform.SetParent(root, false);
					go.transform.localPosition = new Vector3(mode == "1" ? 0f : 5f, 0f, 0f);
					go.transform.localRotation = tilt;
					var props = new Dictionary<string, string> { { BehaviourProps.Spin, "90" }, { BehaviourProps.SpinOwn, mode } };
					Behaviours.Attach(go, "Cube", props, mode == "1" ? 0 : 1);
					blocks[mode] = go.transform;
				}
				Check(ref ok, blocks["1"].GetComponent<IslandBehaviour>() != null && blocks["1"].GetComponent<IslandBehaviour>().SpinOwn && !blocks["0"].GetComponent<IslandBehaviour>().SpinOwn,
					"beh.spinOwn reaches the block's behaviour (1: own axis, 0: the world's)");
				Vector3 p1 = blocks["1"].localPosition, p0 = blocks["0"].localPosition;
				yield return null; yield return null;
				Quaternion a1 = blocks["1"].localRotation, a0 = blocks["0"].localRotation;
				float t0 = SharedClock.Now;
				yield return new WaitForSeconds(1f);
				yield return null;
				float dt = SharedClock.Now - t0;
				Quaternion b1 = blocks["1"].localRotation, b0 = blocks["0"].localRotation;
				float expected = Mathf.Repeat(90f * dt, 360f);

				// seen from the block: the turn between the two poses
				float angle, angle0;
				Vector3 axis, axis0;
				(Quaternion.Inverse(a1) * b1).ToAngleAxis(out angle, out axis);
				(Quaternion.Inverse(a0) * b0).ToAngleAxis(out angle0, out axis0);
				// (a quaternion and its negative are the same turn: 92 degrees about up can come out as 268 about down)
				if (angle > 180f) angle = 360f - angle;
				if (angle0 > 180f) angle0 = 360f - angle0;
				Check(ref ok, Mathf.Abs(angle - expected) < 6f && Mathf.Abs(angle0 - expected) < 6f, "after " + dt.ToString("F2") + " s both turned about " + expected.ToString("F0") + " degrees (own: " + angle.ToString("F0") + ", world: " + angle0.ToString("F0") + ")");
				float ownOffAxis = Mathf.Min(Vector3.Angle(axis, Vector3.up), Vector3.Angle(axis, Vector3.down));
				Check(ref ok, ownOffAxis < 2f, "spinOwn 1: the turn is about the block's own up axis (" + ownOffAxis.ToString("F1") + " degrees off)");
				// seen from the world: the turn is about the tilted axis (own) or straight up (world)
				float angleW, angleW0;
				Vector3 axisW, axisW0;
				(b1 * Quaternion.Inverse(a1)).ToAngleAxis(out angleW, out axisW);
				(b0 * Quaternion.Inverse(a0)).ToAngleAxis(out angleW0, out axisW0);
				float ownFromUp = Mathf.Min(Vector3.Angle(axisW, Vector3.up), Vector3.Angle(axisW, Vector3.down));
				float worldFromUp = Mathf.Min(Vector3.Angle(axisW0, Vector3.up), Vector3.Angle(axisW0, Vector3.down));
				Check(ref ok, Mathf.Abs(ownFromUp - tiltAngle) < 3f, "spinOwn 1: seen from the world it turns about the tilted axis (" + ownFromUp.ToString("F0") + " degrees from vertical, tilted " + tiltAngle.ToString("F0") + ")");
				Check(ref ok, worldFromUp < 2f, "spinOwn 0: it turns about the world's up (" + worldFromUp.ToString("F1") + " degrees off vertical)");
				Check(ref ok, (blocks["1"].localPosition - p1).magnitude < 0.001f && (blocks["0"].localPosition - p0).magnitude < 0.001f, "neither block moved");
			}
			finally { UnityEngine.Object.Destroy(root.gameObject); }
			if (ok) Log("PASS: spin around its own axis"); else Fail("spin around its own axis");
		}

		#endregion

		#region COV5 - The giant rogue shark's bite

		[ConsoleCommand(name: "CIGiantShark", docs: "Dev, world (host, a test world 'CI ...'): COV5 - a plain shark and a giant rogue shark side by side: the giant's bite on a player (AI_State_Attack_Entity_Shark.AttackPlayerDamage, the number Raft sends) is 1.5 times the plain one's, the health the player loses from each (the damage path DamageEntity, no real bite) is in that ratio, and the bite on raft blocks (attackBlockDamage) is the same; both sharks are killed and the rogue's state put back")]
		public static void GiantSharkCommand() { StartTest(GiantSharkRoutine()); }

		static IEnumerator CvBite(Network_Player player, Network_Host host, float damage, float[] lost)
		{
			PlayerStats stats = player.Stats;
			KeepAlive(player);
			stats.stat_BonusHealth.statTarget.Value = 0f;
			float h0 = stats.stat_health.Value;
			host.DamageEntity(stats, player.transform, damage, player.transform.position, Vector3.up, EntityType.Enemy, null);
			yield return new WaitForSeconds(0.3f);
			lost[0] = h0 - stats.stat_health.Value;
			KeepAlive(player);
			stats.buffManager.ClearBuffs();
		}

		static IEnumerator GiantSharkRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host host = null;
			Network_Host_Entities entities = null;
			try { host = ComponentManager<Network_Host>.Value; entities = ComponentManager<Network_Host_Entities>.Value; } catch { }
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!CvInTestWorld("giant shark")) yield break;
			if (!Raft_Network.IsHost || host == null || entities == null || !raft.HasValue) { Fail("giant shark: run as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string rogueBefore = RogueShark.WriteLines().FirstOrDefault() ?? "@rogueshark=0;0";
			AI_NetworkBehavior_Shark plain = null, giant = null;
			try
			{
				plain = entities.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, new Vector3(raft.Value.x + 60f, -10f, raft.Value.z), null) as AI_NetworkBehavior_Shark;
				giant = RogueShark.Spawn(true);
				yield return new WaitForSeconds(0.5f);
				if (plain == null || giant == null) { Fail("giant shark: Raft made no shark (plain " + (plain != null) + ", giant " + (giant != null) + ")"); yield break; }
				Check(ref ok, RogueShark.Giant && RogueShark.IsRogue(giant) && !RogueShark.IsRogue(plain), "a giant rogue #" + giant.ObjectIndex + " and a plain shark #" + plain.ObjectIndex);

				AI_State_Attack_Entity_Shark biteP = plain.GetComponentInChildren<AI_State_Attack_Entity_Shark>(true), biteG = giant.GetComponentInChildren<AI_State_Attack_Entity_Shark>(true);
				AI_State_Attack_Block_Shark blockP = plain.GetComponentInChildren<AI_State_Attack_Block_Shark>(true), blockG = giant.GetComponentInChildren<AI_State_Attack_Block_Shark>(true);
				if (biteP == null || biteG == null || blockP == null || blockG == null) { Fail("giant shark: the sharks' attack states weren't found"); yield break; }
				int bitePlain = biteP.AttackPlayerDamage, biteGiant = biteG.AttackPlayerDamage;
				int blockPlain = Traverse.Create(blockP).Field("attackBlockDamage").GetValue<int>(), blockGiant = Traverse.Create(blockG).Field("attackBlockDamage").GetValue<int>();
				Check(ref ok, bitePlain > 0 && Mathf.Abs(biteGiant - RogueShark.GiantDamage * bitePlain) <= 1f,
					"the giant's bite on a player is " + RogueShark.GiantDamage + " times the plain shark's (" + biteGiant + " against " + bitePlain + ")");
				Check(ref ok, blockPlain > 0 && blockGiant == blockPlain, "its bite on raft blocks is Raft's own (" + blockGiant + " against " + blockPlain + ")");
				float damagePlain = PlayerLevels.DamageOf(plain.gameObject, plain.behaviourType), damageGiant = PlayerLevels.DamageOf(giant.gameObject, giant.behaviourType);
				// (the biggest damage number of the animal, which may be a block bite: only told)
				Log("  the level system reads the damage as " + damageGiant + " against " + damagePlain);

				// The health a player loses from each bite
				var lostPlain = new float[1];
				var lostGiant = new float[1];
				yield return CvBite(player, host, bitePlain, lostPlain);
				yield return CvBite(player, host, biteGiant, lostGiant);
				if (lostPlain[0] <= 0.01f) Log("  (the game mode takes no damage from a bite: the health ratio isn't checked)");
				else Check(ref ok, Mathf.Abs(lostGiant[0] / lostPlain[0] - RogueShark.GiantDamage) < 0.1f, "the player loses " + lostGiant[0].ToString("F1") + " health from the giant's bite and " + lostPlain[0].ToString("F1") + " from the plain one's (x" + (lostGiant[0] / lostPlain[0]).ToString("F2") + ")");
			}
			finally
			{
				foreach (AI_NetworkBehavior_Shark s in new[] { giant, plain })
					if (s != null && s.networkEntity != null && !s.networkEntity.IsDead) s.networkEntity.Damage(100000f, s.transform.position, Vector3.up, EntityType.Player, true);
				KeepAlive(player);
			}
			yield return new WaitForSeconds(2.5f);
			RogueShark.ReadLine("rogueshark", rogueBefore.Substring(rogueBefore.IndexOf('=') + 1));
			IslandWorldState.Save();
			if (ok) Log("PASS: giant shark bite"); else Fail("giant shark bite");
		}

		#endregion

		#region COV6 - The journal and stats keys

		[ConsoleCommand(name: "CIModKeys", docs: "Dev, world (a test world 'CI ...'): COV6 - the journal and stats keys set as Defaults > Keys sets them (ModKeys.Set): the key is read back (ModKeys, JournalWindow.Key, PlayerLevels.Key), kept in world_rules.txt, named on the hotbar's key tabs, the same key twice / Esc / a number key refused with the reason, a bad value in the file means J and K, Reset gives J and K; world_rules.txt is put back as it was")]
		public static void ModKeysCommand() { StartTest(ModKeysRoutine()); }

		static string CvTabText(string tab)
		{
			Component tmp = null;
			foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
				if (rt != null && rt.name == tab && rt.gameObject.scene.IsValid()) { tmp = rt.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == "TextMeshProUGUI"); break; }
			return tmp != null ? Traverse.Create(tmp).Property("text").GetValue<string>() : null;
		}

		static void CvForgetKeys()
		{
			Traverse.Create(typeof(ModKeys)).Field("journal").SetValue(null);
			Traverse.Create(typeof(ModKeys)).Field("stats").SetValue(null);
		}

		static IEnumerator ModKeysRoutine()
		{
			if (!CvInTestWorld("mod keys")) yield break;
			bool ok = true;
			string file = Path.Combine(DynamicIslands.assetpath, WorldRules.DefaultFileName);
			byte[] saved = CvBytes(file);
			KeyCode journal0 = ModKeys.Journal, stats0 = ModKeys.Stats;
			try
			{
				KeyCode[] free = new[] { KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F4, KeyCode.F5 }.Where(k => ModKeys.Problem(k) == null && k != journal0 && k != stats0).ToArray();
				if (free.Length < 2) { Fail("mod keys: no two free function keys"); yield break; }
				KeyCode j = free[0], s = free[1];
				float until = Time.realtimeSinceStartup + 6f;
				while ((CvTabText(HotkeyHints.JournalName) == null || CvTabText(HotkeyHints.StatsName) == null) && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(0.5f);
				Check(ref ok, CvTabText(HotkeyHints.JournalName) != null && CvTabText(HotkeyHints.StatsName) != null, "the hotbar has the journal's and the stats page's key tabs");

				Check(ref ok, ModKeys.Set(true, j) == null && ModKeys.Set(false, s) == null, "Set journal " + j + " and stats " + s + ": accepted");
				Check(ref ok, ModKeys.Journal == j && ModKeys.Stats == s && JournalWindow.Key == j && PlayerLevels.Key == s, "ModKeys, the journal and the stats page use " + ModKeys.Journal + " and " + ModKeys.Stats);
				Check(ref ok, WorldRules.ReadDefault(ModKeys.JournalSetting) == j.ToString() && WorldRules.ReadDefault(ModKeys.StatsSetting) == s.ToString(), "world_rules.txt says " + WorldRules.ReadDefault(ModKeys.JournalSetting) + " and " + WorldRules.ReadDefault(ModKeys.StatsSetting));
				Check(ref ok, CvTabText(HotkeyHints.JournalName) == ModKeys.Name(j) && CvTabText(HotkeyHints.StatsName) == ModKeys.Name(s), "the key tabs say '" + CvTabText(HotkeyHints.JournalName) + "' and '" + CvTabText(HotkeyHints.StatsName) + "'");
				// It is read from the file again (a new start)
				CvForgetKeys();
				Check(ref ok, ModKeys.Journal == j && ModKeys.Stats == s, "after a restart (read from the file) the keys are still " + ModKeys.Journal + " and " + ModKeys.Stats);

				// Refused
				string same = ModKeys.Set(true, s), esc = ModKeys.Set(true, KeyCode.Escape), number = ModKeys.Set(false, KeyCode.Alpha7), mouse = ModKeys.Set(false, KeyCode.Mouse1);
				Check(ref ok, same != null && same.Contains("already opens the stats page"), "the stats key for the journal too: '" + same + "'");
				Check(ref ok, esc != null && esc.Contains("Esc"), "Esc: '" + esc + "'");
				Check(ref ok, number != null, "a number key (hotbar): '" + number + "'");
				Check(ref ok, mouse != null && mouse.Contains("mouse"), "a mouse button: '" + mouse + "'");
				Check(ref ok, ModKeys.Journal == j && ModKeys.Stats == s, "... and the keys stay " + ModKeys.Journal + " and " + ModKeys.Stats);

				// A bad value in the file
				ModKeys.Reset();
				File.WriteAllLines(file, new[] { "journalkey=Banana", "statskey=7" });
				CvForgetKeys();
				Check(ref ok, ModKeys.Journal == KeyCode.J && ModKeys.Stats == KeyCode.K, "journalkey=Banana and statskey=7 in the file mean J and K (" + ModKeys.Journal + ", " + ModKeys.Stats + ")");

				// Reset
				ModKeys.Set(true, j);
				ModKeys.Reset();
				Check(ref ok, ModKeys.Journal == KeyCode.J && ModKeys.Stats == KeyCode.K && CvTabText(HotkeyHints.JournalName) == "J" && CvTabText(HotkeyHints.StatsName) == "K", "Reset: J and K, on the tabs too ('" + CvTabText(HotkeyHints.JournalName) + "', '" + CvTabText(HotkeyHints.StatsName) + "')");
			}
			finally
			{
				CvRestore(file, saved);
				CvForgetKeys();
				try { HotkeyHints.KeysChanged(); } catch { }
			}
			Check(ref ok, ModKeys.Journal == journal0 && ModKeys.Stats == stats0, "the keys are as they were (" + ModKeys.Journal + ", " + ModKeys.Stats + ")");
			if (ok) Log("PASS: journal and stats keys"); else Fail("journal and stats keys");
		}

		#endregion

		#region COV7 - No update check; the version notice

		[ConsoleCommand(name: "CIUpdateCheck", docs: "Dev, anywhere: COV7 - the mod has no update check any more (Franz, 2026-10-17: the only source is raftmodding.com): no UpdateCheck type, no GitHub releases address in the mod's code, the main menu's hook asks nothing; MismatchText's wording for the host and for a client (both versions, which one to install, raftmodding.com when a version can't be told)")]
		public static void UpdateCheckTestCommand() { StartTest(UpdateCheckRoutine()); }

		static IEnumerator UpdateCheckRoutine()
		{
			bool ok = true;
			Check(ref ok, typeof(VersionMismatch).Assembly.GetType("DynamicIslands.Editor.UpdateCheck") == null, "no UpdateCheck type in the mod");
			// (every .cs the .rmod ships, but the tests: none asks GitHub's releases)
			var files = DynamicIslands.instance != null ? DynamicIslands.instance.modlistEntry.modinfo.modFiles : null;
			int read = 0;
			var asking = new List<string>();
			if (files != null)
				foreach (var f in files)
				{
					if (!f.Key.EndsWith(".cs") || f.Key.StartsWith("DevTests")) continue;
					read++;
					string text = System.Text.Encoding.UTF8.GetString(f.Value);
					if (text.Contains("releases/latest") || text.Contains("UpdateCheck.")) asking.Add(f.Key);
				}
			if (read > 0) Check(ref ok, asking.Count == 0, read + " source files read, none asks for a newer version" + (asking.Count > 0 ? ": " + string.Join(", ", asking.ToArray()) : ""));
			else Log("  (the .rmod's files can't be read here: only the type is checked)");
			string host1 = VersionMismatch.MismatchText(true, "Anna", "3.0", "3.1"), host2 = VersionMismatch.MismatchText(true, "Anna", "3.2", "3.1");
			Check(ref ok, host1.StartsWith("Anna joined with Custom Islands 3.0 - you have 3.1.") && host1.Contains("Anna should install Custom Islands 3.1.") && host1.EndsWith("or not arrive at all."), "host, the player has an older one: '" + host1 + "'");
			Check(ref ok, host2.Contains("Anna joined with Custom Islands 3.2 - you have 3.1.") && host2.Contains("Update to Custom Islands 3.2 (or they install 3.1)."), "host, the player has a newer one: '" + host2 + "'");
			string client1 = VersionMismatch.MismatchText(false, "", "3.2", "3.1"), client2 = VersionMismatch.MismatchText(false, "", "3.0", "3.1");
			Check(ref ok, client1.StartsWith("The host has Custom Islands 3.2 - you have 3.1.") && client1.Contains("Install Custom Islands 3.2 to match the host."), "client, the host is newer: '" + client1 + "'");
			Check(ref ok, client2.Contains("The host has Custom Islands 3.0 - you have 3.1.") && client2.Contains("The host should update to Custom Islands 3.1 (or you install 3.0)."), "client, the host is older: '" + client2 + "'");
			string old = VersionMismatch.MismatchText(false, "", VersionMismatch.Older, "3.1"), unknown = VersionMismatch.MismatchText(true, "Bo", VersionMismatch.Unknown, "3.1");
			Check(ref ok, old.Contains("The host has Custom Islands an older version - you have 3.1.") && old.Contains("The host should update to Custom Islands 3.1"), "a host from before versions were sent: '" + old + "'");
			Check(ref ok, unknown.Contains("both should install the newest Custom Islands from raftmodding.com.") && !unknown.Contains("GitHub"), "a version that can't be told: '" + unknown + "'");
			yield return null;
			if (ok) Log("PASS: update check"); else Fail("update check");
		}

		#endregion

		#region COV8 - Editor shortcuts wait while typing or behind a window

		[ConsoleCommand(name: "CIShortcutsWait", docs: "Dev, editor: COV8 - Ctrl+Z, Ctrl+V and Delete (simulated through EditorInput.TestKeys, no real key presses, one frame each) change nothing while a text field has focus (EditorInput.IsTyping) or a modal window is open (the pieces window with its Rename); with neither they undo, paste and delete (so the test is not empty); the editor is left empty")]
		public static void ShortcutsWaitCommand() { StartTest(ShortcutsWaitRoutine()); }

		/// <summary>The keys count as pressed in the next frame only; waits until that frame and the next have run.</summary>
		static IEnumerator CvPress(params KeyCode[] keys)
		{
			EditorInput.TestKeys = new HashSet<KeyCode>(keys);
			EditorInput.TestFrame = Time.frameCount + 1;
			yield return null;
			yield return null;
			EditorInput.TestKeys = null;
		}

		static IEnumerator ShortcutsWaitRoutine()
		{
			yield return WaitForEditor(false);
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			UndoRedoManager.Clear();
			TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			gizmo.ClearTargets(false);
			Func<int> count = () => placed.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.GetComponent<EditorGameObject>() != null);
			Func<string> snapshot = () => count() + " objects, undo " + UndoRedoManager.UndoCount + ", redo " + UndoRedoManager.RedoCount + ", " + UndoRedoManager.Changes + " changes";
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			try
			{
				EditorGameObject a = PlaceForTest("Log", c0, placed), b = PlaceForTest("Log", c0 + new Vector3(4f, 0f, 0f), placed);
				if (a == null || b == null) { Fail("shortcuts wait: could not place two logs"); yield break; }
				UndoRedoManager.Execute(new ObjectVisibilityCommand(new[] { a.gameObject }, true));
				UndoRedoManager.Execute(new ObjectVisibilityCommand(new[] { b.gameObject }, true));
				gizmo.ClearTargets(false);
				gizmo.AddTarget(a.transform, false);
				EditorUI.CopySelected();
				Check(ref ok, PlacementOptions.ClipboardCount == 1 && count() == 2 && UndoRedoManager.UndoCount == 2, "two logs placed, one copied (" + snapshot() + ")");
				string start = snapshot();

				// While a text field has focus
				EditorInput.IsTyping = true;
				yield return CvPress(KeyCode.LeftControl, KeyCode.Z);
				yield return CvPress(KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.Z);
				yield return CvPress(KeyCode.LeftControl, KeyCode.Y);
				yield return CvPress(KeyCode.LeftControl, KeyCode.V);
				yield return CvPress(KeyCode.Delete);
				Check(ref ok, snapshot() == start && a.gameObject.activeSelf && b.gameObject.activeSelf, "typing: Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y, Ctrl+V and Delete change nothing (" + snapshot() + ")");
				EditorInput.IsTyping = false;

				// Behind a modal window (not typing in it)
				PiecesWindow.Open(false);
				yield return null;
				Check(ref ok, EditorInput.WindowOpen && !EditorInput.IsTyping, "the pieces window is open (WindowOpen), no field has focus");
				yield return CvPress(KeyCode.LeftControl, KeyCode.Z);
				yield return CvPress(KeyCode.LeftControl, KeyCode.V);
				yield return CvPress(KeyCode.Delete);
				Check(ref ok, snapshot() == start && a.gameObject.activeSelf && b.gameObject.activeSelf, "a window open: Ctrl+Z, Ctrl+V and Delete change nothing (" + snapshot() + ")");
				PiecesWindow.Close();
				yield return null;
				Check(ref ok, !EditorInput.WindowOpen && !EditorInput.IsTyping, "the window closed: the shortcuts are free");

				// Free: they work (the test isn't empty)
				yield return CvPress(KeyCode.LeftControl, KeyCode.Z);
				Check(ref ok, !b.gameObject.activeSelf && UndoRedoManager.UndoCount == 1 && UndoRedoManager.RedoCount == 1, "free: Ctrl+Z undoes the last placing (" + snapshot() + ")");
				yield return CvPress(KeyCode.LeftControl, KeyCode.Y);
				Check(ref ok, b.gameObject.activeSelf && UndoRedoManager.RedoCount == 0, "free: Ctrl+Y redoes it (" + snapshot() + ")");
				int before = count();
				yield return CvPress(KeyCode.LeftControl, KeyCode.V);
				Check(ref ok, count() == before + 1, "free: Ctrl+V pastes the copied log (" + before + " -> " + count() + " objects)");
				before = count();
				yield return CvPress(KeyCode.Delete);
				Check(ref ok, count() == before - 1, "free: Delete takes the pasted (selected) log away (" + before + " -> " + count() + " objects)");
			}
			finally
			{
				EditorInput.TestKeys = null;
				EditorInput.IsTyping = false;
				if (PiecesWindow.IsOpen) PiecesWindow.Close();
				gizmo.ClearTargets(false);
				foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
				UndoRedoManager.Clear();
			}
			yield return null;
			if (ok) Log("PASS: editor shortcuts wait"); else Fail("editor shortcuts wait");
		}

		#endregion

		#region COV9 - The Share window's link

		[ConsoleCommand(name: "CIShareUrl", docs: "Dev, editor: COV9 - the Share window (LibraryExportWindow): Share... is off until Export has written the pack; then it selects the exported .zip in its folder and opens the library's submit form (SubmitUrl: github.com SwedenJohansson/CustomIslands-Library, issues/new, template submit.yml - the same address every time, no pack data in it: the pack is attached by hand); nothing is opened outside Raft (test mode); the zip and exports.json are put back")]
		public static void ShareUrlCommand() { StartTest(ShareUrlRoutine()); }

		static IEnumerator ShareUrlRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			string exports = Path.Combine(LibraryPack.ExportFolder, "exports.json");
			byte[] exportsBefore = CvBytes(exports);
			bool testMode = HelpLinks.TestMode;
			string lastOpened = HelpLinks.LastOpened;
			var opened = new List<string>();
			Application.LogCallback watch = (text, trace, type) => { int at = text.IndexOf("[CUSTOM ISLANDS] Opening "); if (at >= 0) opened.Add(text.Substring(at + 25)); };
			Application.logMessageReceived += watch;
			string zip = null;
			try
			{
				HelpLinks.TestMode = true;
				CleanLibTests();
				MakeLibIsland(LibA, 5);
				LibraryExportWindow.OpenIsland(LibA);
				yield return null;
				GameObject win = LibraryExportWindow.Window.gameObject;
				Check(ref ok, LibraryExportWindow.IsOpen, "the Share window opens for '" + LibA + "'");
				Check(ref ok, !Click(win, "Share..."), "Share... can't be clicked before Export");
				Check(ref ok, TypeInto(win, "e.g. First Voyage", "CI Share Pack") && TypeInto(win, "your name", "CI Tester") && TypeInto(win, "one line for the list", "a test"), "title, author and summary typed");
				Check(ref ok, Click(win, "Export"), "Export clicked");
				yield return null;
				zip = LibraryExportWindow.LastZip;
				Check(ref ok, zip != null && File.Exists(zip) && Path.GetFileName(zip) == LibraryPack.IdFrom("CI Share Pack") + ".zip" && LibraryPack.IdFrom("CI Share Pack") == "ci-share-pack", "the pack is written as its id: " + (zip ?? LibraryExportWindow.Status));
				HelpLinks.LastOpened = null;
				opened.Clear();
				Check(ref ok, Click(win, "Share..."), "Share... is clickable after Export");
				string url = LibraryExportWindow.SubmitUrl;
				Uri uri = null;
				try { uri = new Uri(url); } catch { }
				Check(ref ok, HelpLinks.LastOpened == url, "Share... opens the submit address (" + HelpLinks.LastOpened + ")");
				Check(ref ok, uri != null && uri.Scheme == "https" && uri.Host == "github.com" && uri.AbsolutePath == "/SwedenJohansson/CustomIslands-Library/issues/new" && uri.Query == "?template=submit.yml",
					"... the library's issue form with its submit template (" + (uri != null ? uri.Host + uri.AbsolutePath + uri.Query : "not an address") + ")");
				Check(ref ok, !Uri.UnescapeDataString(url).ToLowerInvariant().Contains("ci-share-pack") && !Uri.UnescapeDataString(url).Contains("CI Tester"), "... with no pack data in it (the .zip is attached by hand)");
				Check(ref ok, zip != null && opened.Any(o => o.StartsWith(zip)) && opened.Any(o => o.StartsWith(url)), "the exported .zip is selected in its folder as well (" + string.Join(" | ", opened.Select(o => o.Length > 70 ? o.Substring(0, 70) + "..." : o).ToArray()) + ")");
				Check(ref ok, LibraryExportWindow.Status.Contains("drag the .zip"), "the window tells what to do in the browser: " + LibraryExportWindow.Status);
			}
			finally
			{
				Application.logMessageReceived -= watch;
				HelpLinks.TestMode = testMode;
				HelpLinks.LastOpened = lastOpened;
				LibraryExportWindow.Close();
				try { if (zip != null && File.Exists(zip)) File.Delete(zip); } catch { }
				CvRestore(exports, exportsBefore);
				CleanLibTests();
			}
			if (ok) Log("PASS: share url"); else Fail("share url");
		}

		#endregion

		#region COV10 - Long voyage

		[ConsoleCommand(name: "CILongVoyage", docs: "Dev, anywhere: COV10 - the world option Long voyage doubles both spacings: WorldIslands.VoyageFactor 2 against 1, the number of Raft's islands between random custom islands (WorldIslands.NewTarget, 300 draws each) twice the span with it on, and the spawner's spacing (CustomIslandSpawner.Rejects: an island 1.5 times the spacing from another is far enough with it off and too close with it on, 1.9 times is still too close, 2.5 times is far enough); the option, the gap state and the island list are put back")]
		public static void LongVoyageCommand() { StartTest(LongVoyageRoutine()); }

		static IEnumerator LongVoyageRoutine()
		{
			bool ok = true;
			if (CustomIslandSpawner.TestAllRound) { Fail("long voyage: CustomIslandSpawner.TestAllRound is on (another test left it)"); yield break; }
			bool was = WorldOptions.Current.Contains(WorldOptions.LongVoyage);
			int since = WorldIslands.RaftIslandsSince, target = WorldIslands.Target;
			// (an island 50 km off, so Raft's own islands and the raft are nowhere near the test points)
			var other = new IslandWorldState.Entry { Name = "generated_sample", HostName = "generated_sample", Position = new Vector3(50000f, 0f, 50000f) };
			IslandWorldState.Islands.Add(other);
			try
			{
				WorldOptions.Current.Remove(WorldOptions.LongVoyage);
				int plainFactor = WorldIslands.VoyageFactor;
				var plain = new List<int>();
				for (int i = 0; i < 300; i++) { WorldIslands.NewTarget(); plain.Add(WorldIslands.Target); }
				string plainText = WorldIslands.DescribeGap();
				// (distances as multiples of the spacing: spawnpool.txt may have changed it from 800 m)
				float spacing = CustomIslandSpawner.MinSpacing;
				Func<float, string> reject = factor => CustomIslandSpawner.Rejects(other.Position + new Vector3(spacing * factor, 0f, 0f), 10f, Vector3.zero, false);
				string plainNear = reject(0.75f), plainMid = reject(1.5f), plainFar = reject(2.5f);
				WorldOptions.Current.Add(WorldOptions.LongVoyage);
				int longFactor = WorldIslands.VoyageFactor;
				var longer = new List<int>();
				for (int i = 0; i < 300; i++) { WorldIslands.NewTarget(); longer.Add(WorldIslands.Target); }
				string longText = WorldIslands.DescribeGap();
				string longMid = reject(1.5f), longNearTwice = reject(1.9f), longFar = reject(2.5f);
				const string tooClose = "custom island 'generated_sample' ";

				Check(ref ok, plainFactor == 1 && longFactor == 2, "the factor is " + plainFactor + " without Long voyage and " + longFactor + " with it");
				Check(ref ok, plain.Min() >= WorldIslands.GapMin && plain.Max() <= WorldIslands.GapMax && longer.Min() >= WorldIslands.GapMin * 2 && longer.Max() <= WorldIslands.GapMax * 2,
					"Raft's islands between random custom islands: " + plain.Min() + "-" + plain.Max() + " without, " + longer.Min() + "-" + longer.Max() + " with (the span " + WorldIslands.GapMin + "-" + WorldIslands.GapMax + ")");
				Check(ref ok, longer.All(n => n % 2 == 0) && longer.Average() > plain.Average() * 1.8f && longer.Average() < plain.Average() * 2.2f, "... twice as many on average (" + plain.Average().ToString("F1") + " -> " + longer.Average().ToString("F1") + ")");
				Check(ref ok, longText.Contains("Long voyage") && !plainText.Contains("Long voyage"), "the gap's text says so only with the option: '" + longText + "'");
				Check(ref ok, plainNear != null && plainNear.StartsWith(tooClose), "spacing " + spacing.ToString("F0") + " m, without: 0.75 times that from another island is too close (" + plainNear + ")");
				Check(ref ok, plainMid == null && plainFar == null, "... 1.5 and 2.5 times that are far enough (" + (plainMid ?? "ok") + ", " + (plainFar ?? "ok") + ")");
				Check(ref ok, longMid != null && longMid.StartsWith(tooClose), "with Long voyage: 1.5 times the spacing is too close (" + longMid + ")");
				Check(ref ok, longNearTwice != null && longNearTwice.StartsWith(tooClose) && longFar == null, "... so is 1.9 times (twice is needed), 2.5 times is far enough (" + (longFar ?? "ok") + ")");
			}
			finally
			{
				IslandWorldState.Islands.Remove(other);
				if (was) WorldOptions.Current.Add(WorldOptions.LongVoyage); else WorldOptions.Current.Remove(WorldOptions.LongVoyage);
				WorldIslands.RaftIslandsSince = since;
				WorldIslands.Target = target;
			}
			if (ok) Log("PASS: long voyage"); else Fail("long voyage");
		}

		#endregion

		#region COV11 - The PC start check on fake paths

		[ConsoleCommand(name: "CIPcCheckPaths", docs: "Dev, anywhere: COV11 (AU45) - PcCheck.Problems on fake mod folders: a clean one has none; a folder on a drive that isn't there under 'Program Files' names the save problem and Program Files; one under a file names only the save problem; OneDrive, Dropbox, Google Drive and iCloud paths name the synced folder; a folder with modinfo.json and a .cs file names the unzipped .rmod; a part of the mod that didn't start is named - one problem each, nothing is left on disk")]
		public static void PcCheckPathsCommand() { StartTest(PcCheckPathsRoutine()); }

		static IEnumerator PcCheckPathsRoutine()
		{
			bool ok = true;
			string tmp = Path.Combine(Path.GetTempPath(), "ci_pccheck_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string low = tmp.ToLowerInvariant();
			if (new[] { "onedrive", "dropbox", "google drive", "icloud", "program files" }.Any(low.Contains)) { Fail("pc check paths: the temp folder " + tmp + " has a word the check looks for in its name"); yield break; }
			List<string> startFailures = DynamicIslands.StartFailures.ToList();
			try
			{
				DynamicIslands.StartFailures.Clear();
				// Clean
				List<string> clean = PcCheck.Problems(Path.Combine(tmp, "Raft", "Mods", "DynamicIslands"));
				Check(ref ok, clean.Count == 0, "a folder that can be written and is nowhere special: no problem" + (clean.Count > 0 ? " - " + string.Join(" / ", clean.ToArray()) : ""));
				// Program Files, where nothing can be written (a drive that isn't there)
				var drives = new HashSet<char>(DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])));
				char free = "ZYXWVUTSRQPONMLKJIHGFED".FirstOrDefault(c => !drives.Contains(c));
				if (free == '\0') Log("  (no free drive letter: the Program Files part is skipped)");
				else
				{
					List<string> pf = PcCheck.Problems(free + @":\Program Files (x86)\Steam\steamapps\common\Raft\Mods\DynamicIslands");
					Check(ref ok, pf.Count == 2 && pf[0].Contains("can't save in") && pf[1].Contains("installed under Program Files"), "under Program Files and unwritable: the save problem and Program Files, nothing else (" + pf.Count + ": " + string.Join(" / ", pf.Select(x => x.Length > 60 ? x.Substring(0, 60) : x).ToArray()) + ")");
				}
				// A folder that can't be made (a file in the way)
				Directory.CreateDirectory(tmp);
				string blocker = Path.Combine(tmp, "afile.txt");
				File.WriteAllText(blocker, "x");
				List<string> blocked = PcCheck.Problems(Path.Combine(blocker, "Mods", "DynamicIslands"));
				Check(ref ok, blocked.Count == 1 && blocked[0].Contains("can't save in"), "a folder that can't be written: the save problem only (" + blocked.Count + ")" + (blocked.Count > 0 ? " - " + blocked[0].Substring(0, Math.Min(60, blocked[0].Length)) : ""));
				// Synced folders
				foreach (string sync in new[] { "OneDrive", "Dropbox", "Google Drive", "iCloud" })
				{
					List<string> synced = PcCheck.Problems(Path.Combine(tmp, sync, "Raft", "Mods", "DynamicIslands"));
					Check(ref ok, synced.Count == 1 && synced[0].Contains("synced folder (" + sync.ToLowerInvariant() + ")"), "a path in " + sync + ": one problem, the synced folder (" + synced.Count + ")");
				}
				// An unzipped .rmod
				string unzipped = Path.Combine(tmp, "unzipped");
				Directory.CreateDirectory(unzipped);
				File.WriteAllText(Path.Combine(unzipped, "modinfo.json"), "{ \"not\": \"the mod's own\" }");
				File.WriteAllText(Path.Combine(unzipped, "Stray.cs"), "// x");
				List<string> stray = PcCheck.Problems(unzipped);
				Check(ref ok, stray.Count == 1 && stray[0].Contains("unzipped .rmod") && stray[0].Contains("modinfo.json") && stray[0].Contains("Stray.cs"), "modinfo.json and a .cs file in the folder: the unzipped .rmod, naming both (" + stray.Count + ")");
				// A part that didn't start
				DynamicIslands.StartFailures.Add("CI fake part");
				List<string> failed = PcCheck.Problems(Path.Combine(tmp, "Raft", "Mods", "DynamicIslands"));
				Check(ref ok, failed.Count == 1 && failed[0].Contains("didn't start: CI fake part"), "a part that didn't start: one problem naming it (" + failed.Count + ")");
				DynamicIslands.StartFailures.Clear();
				// The real folder still checks out as before
				Check(ref ok, PcCheck.Problems().SequenceEqual(PcCheck.Problems(DynamicIslands.assetpath)), "Problems() is Problems(the mod's folder)");
			}
			finally
			{
				DynamicIslands.StartFailures.Clear();
				DynamicIslands.StartFailures.AddRange(startFailures);
				try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception e) { Log("(could not remove " + tmp + ": " + e.Message + ")"); }
			}
			if (ok) Log("PASS: pc check paths"); else Fail("pc check paths");
		}

		#endregion

		#region COV12 - Ambience zones in a world

		[ConsoleCommand(name: "CIAmbienceZones", docs: "Dev, world (host, a test world 'CI ...'): COV12 - an island with a sound zone (a loop, radius 20), an 'enter' sound zone (radius 10) and a dark atmosphere zone (radius 30), the player put at several distances: the loop's volume (the FMOD instance's own) falls from the middle to the edge and it stops outside; the 'enter' sound plays once per walk-in (twice for two, not while staying); the atmosphere fades out towards its edge and its dark tint dims the sun while the camera draws, the sun's colour coming back outside")]
		public static void AmbienceZonesCommand() { StartTest(AmbienceZonesRoutine()); }

		static float CvZoneVolume(SoundZone z)
		{
			try
			{
				FMOD.Studio.EventInstance inst = Traverse.Create(z).Field("instance").GetValue<FMOD.Studio.EventInstance>();
				float volume, final;
				if (inst.isValid() && inst.getVolume(out volume, out final) == FMOD.RESULT.OK) return volume;
			}
			catch { }
			return -1f;
		}

		static Light CvSun()
		{
			if (RenderSettings.sun != null) return RenderSettings.sun;
			return UnityEngine.Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
		}

		static IEnumerator AmbienceZonesRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (!CvInTestWorld("ambience zones")) yield break;
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("ambience zones: run as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string loop = new[] { "event:/amb/grass", "event:/amb/ocean" }.FirstOrDefault(p => SoundLibrary.Events.Contains(p) && SoundLibrary.IsLooping(p)) ?? SoundLibrary.Events.FirstOrDefault(SoundLibrary.IsLooping);
			string shot = CvOneShot(1000);
			if (loop == null || shot == null) { Fail("ambience zones: Raft has no loop (" + loop + ") or no one-shot (" + shot + ")"); yield break; }
			Log("  loop " + loop + ", enter sound " + shot);
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciambzones";
			IslandFile f = ScIsland(isl, "Zones Isle");
			f.Objects.Clear();
			Vector3 c = ScDry(f, Vector2.zero, 1);
			c.y = ScGround(f, new Vector2(c.x, c.z));
			f.Objects.Add(new IslandObject { Name = ContentCatalog.SoundZoneName, Position = c, Props = P(ObjectProps.ZoneRadius, "20", ObjectProps.SoundEvent, loop, ObjectProps.SoundVolume, "0.8") });
			f.Objects.Add(new IslandObject { Name = ContentCatalog.SoundZoneName, Position = c, Props = P(ObjectProps.ZoneRadius, "10", ObjectProps.SoundEvent, shot, ObjectProps.SoundMode, "enter") });
			f.Objects.Add(new IslandObject { Name = ContentCatalog.AtmosphereZoneName, Position = c, Props = P(ObjectProps.ZoneRadius, "30", ObjectProps.AtmoLight, "#101010", ObjectProps.AtmoLightAmount, "0.3") });
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("ambience zones: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("ambience zones: the island didn't come"); yield break; }
			SoundZone[] zones = e.Root.GetComponentsInChildren<SoundZone>(true);
			SoundZone loopZone = zones.FirstOrDefault(z => !z.Once), enterZone = zones.FirstOrDefault(z => z.Once);
			AtmosphereZone atmo = e.Root.GetComponentInChildren<AtmosphereZone>(true);
			Check(ref ok, loopZone != null && enterZone != null && atmo != null && Mathf.Abs(loopZone.Radius - 20f) < 0.01f && Mathf.Abs(enterZone.Radius - 10f) < 0.01f && Mathf.Abs(atmo.Radius - 30f) < 0.01f,
				"the island has the three zones (" + zones.Length + " sound zones, atmosphere " + (atmo != null)  + ")");
			if (loopZone == null || enterZone == null || atmo == null) { ScRemove(made, isl); Fail("ambience zones"); yield break; }
			Vector3 zonePos = loopZone.transform.position;
			Light sun = null;
			try
			{
				// Far away: nothing
				yield return CvStandAt(player, zonePos, 60f, 1.0f);
				Check(ref ok, !loopZone.Playing && atmo.WeightAt(Camera.main.transform.position) < 0.001f, "60 m off: no sound, no atmosphere");
				int played0 = SoundLibrary.Played;

				// In the middle: the loop at full volume, the enter sound once, the atmosphere at full
				yield return CvStandAt(player, zonePos, 0f, 0.9f);
				float v0 = CvZoneVolume(loopZone);
				Check(ref ok, loopZone.Playing && Mathf.Abs(v0 - 0.8f) < 0.05f, "in the middle the loop plays at its volume (" + v0.ToString("F2") + " of 0.8)");
				Check(ref ok, SoundLibrary.Played == played0 + 1 && SoundLibrary.LastPlayed == shot, "walking into the 'enter' zone plays its sound once (" + (SoundLibrary.Played - played0) + ")");
				float weightIn;
				Check(ref ok, atmo.WeightAt(Camera.main.transform.position) > 0.99f && AtmosphereZone.Strongest(Camera.main.transform.position, out weightIn) == atmo && weightIn > 0.99f, "the atmosphere acts fully in the middle");
				yield return new WaitForSeconds(1.6f);
				Check(ref ok, SoundLibrary.Played == played0 + 1, "staying in the zone does not play it again (" + (SoundLibrary.Played - played0) + ")");

				// The dark tint dims the sun while the camera draws, and weaker tint dims it less
				sun = CvSun();
				atmo.LightAmount = 0.3f;
				yield return new WaitForSeconds(0.6f);
				float before1 = AtmosphereZone.SunBefore.grayscale, drawn1 = AtmosphereZone.SunWhileDrawn.grayscale;
				atmo.LightAmount = 0.9f;
				yield return new WaitForSeconds(0.6f);
				float before2 = AtmosphereZone.SunBefore.grayscale, drawn2 = AtmosphereZone.SunWhileDrawn.grayscale;
				float r1 = before1 > 0f ? drawn1 / before1 : 1f, r2 = before2 > 0f ? drawn2 / before2 : 1f;
				Check(ref ok, sun != null && before2 > 0f && r2 < 0.3f && r1 > r2 + 0.2f, "the dark tint dims the sun while the camera draws in the zone: x" + r1.ToString("F2") + " at 0.3, x" + r2.ToString("F2") + " at 0.9 (the sun " + before2.ToString("F2") + " -> " + drawn2.ToString("F2") + ")");

				// Out towards the edge: the loop's volume falls
				yield return CvStandAt(player, zonePos, 12f, 0.9f);
				float v1 = CvZoneVolume(loopZone), d1 = Vector3.Distance(player.transform.position, zonePos);
				yield return CvStandAt(player, zonePos, 17f, 0.9f);
				float v2 = CvZoneVolume(loopZone), d2 = Vector3.Distance(player.transform.position, zonePos);
				yield return CvStandAt(player, zonePos, 19.6f, 0.9f);
				float v3 = loopZone.Playing ? CvZoneVolume(loopZone) : 0f, d3 = Vector3.Distance(player.transform.position, zonePos);
				Check(ref ok, v1 > v2 && v2 > v3 && v1 < v0 && v1 > v0 * 0.5f && v2 < v0 * 0.5f && v3 < v0 * 0.15f,
					"the loop's volume falls to the edge: " + v0.ToString("F2") + " (0 m) > " + v1.ToString("F2") + " (" + d1.ToString("F1") + " m) > " + v2.ToString("F2") + " (" + d2.ToString("F1") + " m) > " + v3.ToString("F2") + " (" + d3.ToString("F1") + " m)");
				Check(ref ok, SoundLibrary.Played == played0 + 1, "... and the 'enter' sound didn't play again outside its own zone");

				// Outside the sound zones, still in the atmosphere's outer third
				yield return CvStandAt(player, zonePos, 26f, 1.0f);
				float edge = atmo.WeightAt(Camera.main.transform.position);
				Check(ref ok, !loopZone.Playing && edge > 0.02f && edge < 0.9f, "26 m out: the loop has stopped, the atmosphere is fading (" + edge.ToString("F2") + ")");

				// Walking in again: the enter sound plays a second time, once
				yield return CvStandAt(player, zonePos, 2f, 0.9f);
				Check(ref ok, SoundLibrary.Played == played0 + 2, "walking in a second time plays it a second time (" + (SoundLibrary.Played - played0) + " in all)");
				Check(ref ok, loopZone.Playing, "the loop starts again");
				yield return new WaitForSeconds(1.4f);
				Check(ref ok, SoundLibrary.Played == played0 + 2, "... and still not while staying");

				// Out again: everything stops, the sun is its own colour
				yield return CvStandAt(player, zonePos, 60f, 1.2f);
				float w;
				AtmosphereZone.Strongest(Camera.main.transform.position, out w);
				Check(ref ok, !loopZone.Playing && w < 0.001f, "60 m off again: the loop has stopped, the atmosphere is gone");
				Check(ref ok, sun != null && before2 > 0f && Mathf.Abs(sun.color.grayscale - before2) < before2 * 0.1f, "the sun has its own colour outside (" + (sun != null ? sun.color.grayscale.ToString("F2") : "no sun") + " against " + before2.ToString("F2") + ")");
			}
			finally
			{
				ScRemove(made, isl);
				try { OnRaftCommand(); } catch { }
			}
			if (ok) Log("PASS: ambience zones in a world"); else Fail("ambience zones in a world");
		}

		#endregion

		#region COV13 - The notice of a patch that failed

		[ConsoleCommand(name: "CIPatchNotice", docs: "Dev, anywhere: COV13 - a fake failed patch is marked in PatchHealth (not applied; the log line of a real failure isn't made) and ShowIfFailed opens the box: its title, a bullet naming the feature in players' words (two patches of one feature give one bullet, an unknown patch is named by its class), Report a problem and Close; it comes once per start; the fake patches, the 'told' flag and the box are put back")]
		public static void PatchNoticeCommand() { StartTest(PatchNoticeRoutine()); }

		static IEnumerator PatchNoticeRoutine()
		{
			bool ok = true;
			List<string> failed = Traverse.Create(typeof(PatchHealth)).Field("failed").GetValue<List<string>>();
			bool toldBefore = Traverse.Create(typeof(PatchHealth)).Field("told").GetValue<bool>();
			int realFailures = failed.Count;
			bool boxWasOpen = InfoWindow.IsOpen;
			string[] fakes = { "CI_FakeBlueprintPatch", "CI_FakeScrambledBlueprintPatch2", "CI_FakeGhostRaftPatch", "CIOddOne" };
			try
			{
				if (boxWasOpen) InfoWindow.Close();
				yield return null;
				failed.AddRange(fakes);
				Traverse.Create(typeof(PatchHealth)).Field("told").SetValue(false);
				Check(ref ok, PatchHealth.FailedPatches.Count == realFailures + fakes.Length, "four fake failed patches are marked (" + PatchHealth.FailedPatches.Count + " in all)");
				PatchHealth.ShowIfFailed();
				yield return null;
				Check(ref ok, InfoWindow.IsOpen && string.Equals(InfoWindow.Title, "Some parts of Custom Islands are off", StringComparison.OrdinalIgnoreCase), "the box opens: '" + InfoWindow.Title + "'");
				string body = InfoWindow.Body;
				Check(ref ok, body.Contains("\u2022  scrambled blueprints") && body.Contains("\u2022  ghost rafts"), "it names the features in players' words (scrambled blueprints, ghost rafts)");
				Check(ref ok, body.Split(new[] { "scrambled blueprints" }, StringSplitOptions.None).Length == 2, "... two patches of one feature give one bullet");
				Check(ref ok, body.Contains("a part of the mod (CIOddOne)") && !body.Contains("CI_Fake"), "... a patch no feature fits is named by its class, the others by their feature");
				Check(ref ok, InfoWindow.ButtonNamed("Report a problem") != null && InfoWindow.ButtonNamed("Close") != null, "it has Report a problem and Close (" + string.Join(", ", InfoWindow.ButtonLabels.ToArray()) + ")");
				// Once per start
				InfoWindow.Close();
				yield return null;
				PatchHealth.ShowIfFailed();
				yield return null;
				Check(ref ok, !InfoWindow.IsOpen, "the box does not come again in the same start");
				// With nothing failed it stays shut
				failed.RemoveAll(fakes.Contains);
				Traverse.Create(typeof(PatchHealth)).Field("told").SetValue(false);
				if (realFailures == 0)
				{
					PatchHealth.ShowIfFailed();
					yield return null;
					Check(ref ok, !InfoWindow.IsOpen, "no failed patch: no box");
				}
			}
			finally
			{
				failed.RemoveAll(fakes.Contains);
				Traverse.Create(typeof(PatchHealth)).Field("told").SetValue(toldBefore);
				if (InfoWindow.IsOpen) InfoWindow.Close();
			}
			Check(ref ok, PatchHealth.FailedPatches.Count == realFailures, "the fake patches are gone again (" + PatchHealth.FailedPatches.Count + " left)");
			if (ok) Log("PASS: patch failure notice"); else Fail("patch failure notice");
		}

		#endregion
	}
}
