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
	/// <summary>
	/// Automated smoke tests, run from the RML console. Results are logged with a [CITEST] prefix
	/// (visible in the F10 console and in Player.log).
	///   CITest       - main menu or editor: opens the editor, sculpts, places objects, saves, wipes, loads, verifies.
	///   CITestWorld  - in a world (host): spawns the island saved by CITest in front of the raft and verifies it.
	/// </summary>
	public static partial class DevTests
	{
		public const string TestIsland = "citest";
		const int ObjectsToPlace = 3;
		const int PaintOffset = 20, PaintSize = 12; // hand-painted test patch, in alphamap pixels from the centre

		static void Log(string msg) { Debug.Log("[CITEST] " + msg); }
		static void Fail(string msg) { Debug.LogError("[CITEST] FAIL: " + msg); }

		// Every test coroutine runs through this, so ci.ps1 can end a run at "[CITEST] IDLE"
		// (logged when the last running test ends) instead of guessing from PASS/FAIL lines.
		static int runningTests;
		// Lines of the command file still to run, and an IDLE held back for them: "CIA;CIB" logs IDLE once, after CIB
		// (ci.ps1 -done used to stop at CIA's IDLE while CIB ran on, and the next run's test overlapped it)
		static int pendingCommands;
		static bool idleOwed;
		static Coroutine StartTest(IEnumerator e)
		{
			if (exclusiveTest != null && exclusiveTest != e) StopExclusive();
			runningTests++;
			int id = ++testSerial;
			runningIds.Add(id);
			if (exclusiveTest == e) exclusiveId = id;
			Coroutine run = DynamicIslands.instance.StartCoroutine(Track(e, id));
			if (exclusiveTest == e) exclusiveRun = run;
			// (a test that ended at once is gone already)
			if (runningIds.Contains(id)) runningRuns[id] = new KeyValuePair<IEnumerator, Coroutine>(e, run);
			return run;
		}

		// Every running test by its number: CIStopTests ends them all - the runner sends it after a suite's timeout, as the
		// test's coroutine ran on under the next suites (CIRoundTrip 44 min, 5 suites broken - RUN2 10-20)
		static readonly Dictionary<int, KeyValuePair<IEnumerator, Coroutine>> runningRuns = new Dictionary<int, KeyValuePair<IEnumerator, Coroutine>>();

		[ConsoleCommand(name: "CIStopTests", docs: "Dev: stops every running test (its finally runs: options etc. put back) - alltests sends it when a suite times out, so the test can't run on under the next ones")]
		public static void StopTestsCommand()
		{
			// (where a long test was when stopped: CIButtons' screen and press - RUN2 10-21, 45 min with one line)
			if (buttonRun != null && buttonRun.Active) Log("  buttons: stopped at " + buttonRun.Where());
			var names = new List<string>();
			foreach (var kv in runningRuns.ToList())
			{
				IEnumerator e = kv.Value.Key;
				if (kv.Value.Value != null) DynamicIslands.instance.StopCoroutine(kv.Value.Value);
				runningRuns.Remove(kv.Key);
				runningIds.Remove(kv.Key);
				if (exclusiveTest == e) { exclusiveTest = null; exclusiveRun = null; }
				names.Add(TestName(e));
				try { IDisposable d = e as IDisposable; if (d != null) d.Dispose(); }
				catch (Exception ex) { Debug.LogException(ex); }
				if (--runningTests < 0) runningTests = 0;
			}
			Log("Stopped " + names.Count + " running test(s)" + (names.Count > 0 ? ": " + string.Join(", ", names.ToArray()) : ""));
			if (runningTests == 0) { if (pendingCommands > 0) idleOwed = true; else Log("IDLE (all tests done)"); }
		}

		/// <summary>A test routine's name from its iterator class ("&lt;RoundTripRoutine&gt;d__12" -> RoundTripRoutine).</summary>
		static string TestName(IEnumerator e)
		{
			string n = e.GetType().Name;
			int a = n.IndexOf('<'), b = n.IndexOf('>');
			return a >= 0 && b > a ? n.Substring(a + 1, b - a - 1) : n;
		}

		[ConsoleCommand(name: "CIStopCheck", docs: "Dev: CIStopTests ends a running test, runs its finally and logs IDLE (stops any other running test too)")]
		public static void StopCheckCommand() { DynamicIslands.instance.StartCoroutine(StopCheckRoutine()); }
		static bool stopCheckFinally;
		static IEnumerator StopCheckDummy()
		{
			try { while (true) yield return null; }
			finally { stopCheckFinally = true; }
		}
		// (not a test itself: CIStopTests would end it with the dummy)
		static IEnumerator StopCheckRoutine()
		{
			stopCheckFinally = false;
			StartTest(StopCheckDummy());
			yield return null; yield return null;
			bool running = runningTests > 0 && runningRuns.Count > 0;
			StopTestsCommand();
			yield return null; yield return null;
			bool ok = running && runningTests == 0 && runningRuns.Count == 0 && runningIds.Count == 0 && stopCheckFinally;
			if (ok) Log("PASS: stop tests (the running test ended, its finally ran)");
			else Fail("stop tests (was running " + running + ", still running " + runningTests + ", finally ran " + stopCheckFinally + ")");
		}

		// Each running test's number: a command file's later lines wait only for the tests its own earlier lines started
		static int testSerial;
		static readonly HashSet<int> runningIds = new HashSet<int>();

		// A long test that keeps changing the world (CIOptionsMatrix) stops when the next test starts: after a runner's
		// timeout it went on switching options under the next tests (CIBlueprintsWorld, CIWorldOptionsCheck failed)
		static IEnumerator exclusiveTest;
		static Coroutine exclusiveRun;
		static string exclusiveName;
		static int exclusiveId;
		static Coroutine StartExclusiveTest(string name, IEnumerator e)
		{
			if (exclusiveTest != null) StopExclusive();
			exclusiveTest = e;
			exclusiveName = name;
			return StartTest(e);
		}

		static void StopExclusive()
		{
			IEnumerator e = exclusiveTest;
			if (e == null) return;
			exclusiveTest = null;
			if (exclusiveRun != null) DynamicIslands.instance.StopCoroutine(exclusiveRun);
			exclusiveRun = null;
			runningIds.Remove(exclusiveId);
			runningRuns.Remove(exclusiveId);
			// (not a FAIL line: it lands in the next test's output and failed that one; the runner has its timeout already)
			Log("STOPPED: " + exclusiveName + " - another test started (the runner moved on, e.g. after its timeout)");
			// (Dispose runs the test's finally: the world's own options etc. back now, not over the next test)
			try { IDisposable d = e as IDisposable; if (d != null) d.Dispose(); }
			catch (Exception ex) { Debug.LogException(ex); }
			if (--runningTests < 0) runningTests = 0;
		}

		static IEnumerator Track(IEnumerator e, int id)
		{
			while (true)
			{
				bool more;
				try { more = e.MoveNext(); }
				catch (Exception ex) { Fail("exception: " + ex.Message); Debug.LogException(ex); more = false; }
				if (!more) break;
				yield return e.Current;
			}
			if (exclusiveTest == e) { exclusiveTest = null; exclusiveRun = null; }
			runningIds.Remove(id);
			runningRuns.Remove(id);
			if (--runningTests <= 0) { runningTests = 0; if (pendingCommands > 0) idleOwed = true; else Log("IDLE (all tests done)"); }
		}

		[ConsoleCommand(name: "CITest", docs: "Dev: automated editor test (sculpt, place, save, load, verify)")]
		public static void RunEditorTest()
		{
			StartTest(EditorTest());
		}

		[ConsoleCommand(name: "CITestWorld", docs: "Dev: spawns the island saved by CITest in front of the raft and verifies it")]
		public static void RunWorldTest()
		{
			StartTest(WorldTest());
		}

		[ConsoleCommand(name: "CILook", docs: "Dev: renders spawned custom islands from a temporary camera to Mods\\DynamicIslands\\view_<island>.png")]
		public static void RenderIslandViews(string[] args)
		{
			StartTest(RenderViews(args != null && args.Contains("low")));
		}

		/// <param name="low">film from below the hilltop (to see flying islands' undersides); files get a number per island</param>
		static IEnumerator RenderViews(bool low = false)
		{
			int n = 0;
			yield return new WaitForEndOfFrame();
			Camera main = Camera.main;
			if (main != null)
				Log("Main camera: far clip " + main.farClipPlane + ", renders layer " + IslandSpawner.TerrainLayer + ": " + ((main.cullingMask & (1 << IslandSpawner.TerrainLayer)) != 0));

			foreach (GameObject root in UnityEngine.Object.FindObjectsOfType<GameObject>().Where(g => g.transform.parent == null && g.name.StartsWith("CustomIsland_")))
			{
				Terrain terrain = root.GetComponentInChildren<Terrain>();
				if (terrain == null) continue;
				Vector3 centre = HighestPoint(terrain);
				Log(root.name + ": terrain enabled=" + terrain.enabled + ", material=" + (terrain.materialTemplate != null ? terrain.materialTemplate.shader.name : "(default)") + ", hill top y=" + centre.y.ToString("F1"));

				var camGO = new GameObject("CITest_ViewCamera");
				Camera cam = camGO.AddComponent<Camera>();
				if (main != null) cam.CopyFrom(main);
				cam.farClipPlane = 3000f;
				cam.transform.position = low ? new Vector3(centre.x, 4f, centre.z - 330f) : centre + new Vector3(0, 40f, -180f);
				cam.transform.LookAt(low ? new Vector3(centre.x, centre.y * 0.55f, centre.z) : centre);
				var rt = new RenderTexture(1280, 720, 24);
				cam.targetTexture = rt;
				cam.Render();

				RenderTexture.active = rt;
				var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
				tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
				tex.Apply();
				RenderTexture.active = null;

				string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "view_" + root.name.Substring("CustomIsland_".Length) + "_" + (++n) + ".png"));
				File.WriteAllBytes(file, tex.EncodeToPNG());
				Log("Rendered view to " + file);

				cam.targetTexture = null;
				UnityEngine.Object.Destroy(camGO);
				UnityEngine.Object.Destroy(rt);
				UnityEngine.Object.Destroy(tex);
			}
		}

		[ConsoleCommand(name: "CIDump", docs: "Dev: logs renderers/shaders/bounds of loaded scenes whose name contains <text>, e.g. CIDump demoisland1")]
		public static void DumpScene(string[] args)
		{
			string filter = args != null && args.Length > 0 ? args[0] : "";
			for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
			{
				var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
				if (filter.Length > 0 && scene.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
				foreach (GameObject root in scene.GetRootGameObjects())
				{
					Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
					Log("Scene '" + scene.name + "' root '" + root.name + "' active=" + root.activeInHierarchy + " pos=" + root.transform.position +
						" scale=" + root.transform.lossyScale + " renderers=" + renderers.Length + " terrains=" + root.GetComponentsInChildren<Terrain>(true).Length +
						" colliders=" + root.GetComponentsInChildren<Collider>(true).Length);
					if (renderers.Length == 0) continue;
					Bounds b = renderers[0].bounds;
					foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
					Log("  bounds centre " + b.center + " size " + b.size + ", enabled renderers " + renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy));
					foreach (var g in renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).GroupBy(m => m.shader == null ? "(no shader)" : m.shader.name))
					{
						Shader sh = g.First().shader;
						Log("  shader '" + g.Key + "' supported=" + (sh != null && sh.isSupported) + " materials=" + g.Count() +
							" found-by-name=" + (Shader.Find(g.Key) != null));
					}
					int nullMats = renderers.Sum(r => r.sharedMaterials.Count(m => m == null));
					if (nullMats > 0) Log("  " + nullMats + " missing (null) materials");
				}
			}
		}

		[ConsoleCommand(name: "CIUndo", docs: "Dev: tests undo/redo for sculpt, paint, placing and deleting, and the islands window (run in the editor)")]
		public static void RunUndoTest()
		{
			StartTest(UndoTest());
		}

		static float MaxDiff(float[,] a, float[,] b)
		{
			float m = 0;
			for (int z = 0; z < a.GetLength(0); z++) for (int x = 0; x < a.GetLength(1); x++) m = Mathf.Max(m, Mathf.Abs(a[z, x] - b[z, x]));
			return m;
		}

		static IEnumerator UndoTest()
		{
			Log("Undo test started");
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first (CITest does that)"); yield break; }
			var editor = Camera.main.GetComponent<terraineditor>();
			Terrain terrain = terraineditor.terrain;
			TerrainData data = terrain.terrainData;
			CommandUndoRedo.UndoRedoManager.Clear();
			var savedAction = terraineditor.modificationAction;
			bool ok = true;

			// 1. Sculpt: raise a flat spot away from the test hill, undo, redo
			Vector3 spot = terrain.transform.position + new Vector3(data.size.x * 0.8f, 0, data.size.z * 0.8f);
			spot.y = terrain.SampleHeight(spot) + terrain.transform.position.y;
			int res = data.heightmapResolution;
			float[,] h0 = data.GetHeights(0, 0, res, res);
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Raise;
			editor.SimulateStroke(spot, 30, 0.033f);
			float[,] h1 = data.GetHeights(0, 0, res, res);
			float raised = MaxDiff(h0, h1) * data.size.y;
			CommandUndoRedo.UndoRedoManager.Undo();
			float afterUndo = MaxDiff(h0, data.GetHeights(0, 0, res, res));
			CommandUndoRedo.UndoRedoManager.Redo();
			float afterRedo = MaxDiff(h1, data.GetHeights(0, 0, res, res));
			bool sculptOk = raised > 1f && afterUndo < 1e-6f && afterRedo < 1e-6f;
			Log("Sculpt: raised " + raised.ToString("F1") + " m; undo error " + afterUndo + ", redo error " + afterRedo + " (" + (sculptOk ? "ok" : "WRONG") + ")");
			ok &= sculptOk;
			CommandUndoRedo.UndoRedoManager.Undo(); // leave the terrain as it was
			yield return null;

			// 2. Paint: rock stroke, undo restores texture and paint mask
			int ares = data.alphamapResolution;
			Vector3 local = spot - terrain.transform.position;
			int px = Mathf.FloorToInt(local.x / data.size.x * ares), pz = Mathf.FloorToInt(local.z / data.size.z * ares);
			float rockBefore = data.GetAlphamaps(px, pz, 1, 1)[0, 0, TerrainPainter.Rock];
			terraineditor.paintLayer = TerrainPainter.Rock;
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.PaintLayer;
			editor.SimulateStroke(spot, 60, 0.033f);
			float rockPainted = data.GetAlphamaps(px, pz, 1, 1)[0, 0, TerrainPainter.Rock];
			float maskPainted = terraineditor.paintMask[pz, px];
			CommandUndoRedo.UndoRedoManager.Undo();
			float rockUndone = data.GetAlphamaps(px, pz, 1, 1)[0, 0, TerrainPainter.Rock];
			float maskUndone = terraineditor.paintMask[pz, px];
			bool paintOk = rockPainted > 0.9f && maskPainted > 0.5f && Mathf.Abs(rockUndone - rockBefore) < 0.01f && maskUndone < 0.5f;
			Log("Paint: rock " + rockBefore.ToString("F2") + " -> " + rockPainted.ToString("F2") + " -> undo " + rockUndone.ToString("F2") + ", mask " + maskPainted + " -> " + maskUndone + " (" + (paintOk ? "ok" : "WRONG") + ")");
			ok &= paintOk;
			yield return null;

			// 3. Place + delete objects
			Transform placed = GameObject.Find("PlacedObjects").transform;
			string objectName = PlaceableCatalog.Names.First();
			GameObject obj = PlaceableCatalog.Spawn(objectName, placed);
			obj.transform.position = spot;
			obj.AddComponent<EditorGameObject>().GameObjectName = objectName;
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(new[] { obj }, true)); // what ObjectPlacer does
			CommandUndoRedo.UndoRedoManager.Undo();
			bool hiddenByUndo = !obj.activeSelf;
			CommandUndoRedo.UndoRedoManager.Redo();
			bool shownByRedo = obj.activeSelf;

			DynamicIslands.EditorGizmoHandler.AddTarget(obj.transform, false);
			DynamicIslands.EditorGizmoHandler.DeleteSelection();
			bool deleted = !obj.activeSelf;
			int savedWhileDeleted = IslandFile.Capture("undo-test", terrain, placed).Objects.Count(o => o.Name == objectName && (o.Position - (spot - terrain.transform.position)).sqrMagnitude < 0.01f);
			CommandUndoRedo.UndoRedoManager.Undo();
			bool restored = obj.activeSelf;
			bool objectsOk = hiddenByUndo && shownByRedo && deleted && restored && savedWhileDeleted == 0;
			Log("Objects: place-undo hides " + hiddenByUndo + ", redo shows " + shownByRedo + ", delete hides " + deleted + " (saved while deleted: " + savedWhileDeleted + "), undo restores " + restored + " (" + (objectsOk ? "ok" : "WRONG") + ")");
			ok &= objectsOk;
			UnityEngine.Object.Destroy(obj);
			yield return null;

			// 4. Islands window
			IslandFilesWindow.Open();
			bool opened = IslandFilesWindow.IsOpen;
			yield return new WaitForSeconds(0.5f);
			IslandFilesWindow.Close();
			bool windowOk = opened && !IslandFilesWindow.IsOpen;
			Log("Islands window opens and closes: " + windowOk);
			ok &= windowOk;

			terraineditor.modificationAction = savedAction;
			CommandUndoRedo.UndoRedoManager.Clear();
			if (ok) Log("PASS: undo/redo and islands window");
			else Fail("undo/redo or islands window");
		}

		[ConsoleCommand(name: "CIChunkInfo", docs: "Dev, in game: Raft's island spawn points near the raft - rule, overlap radius, distance")]
		public static void ChunkInfo()
		{
			Vector3? pos = CustomIslandSpawner.RaftPosition;
			ChunkManager cm = ComponentManager<ChunkManager>.Value;
			if (!pos.HasValue || cm == null) { Fail("not in a world"); return; }
			var points = cm.GetAllChunkPointsList();
			Log(points.Count + " chunk points in total");
			foreach (var g in points.Where(p => p.rule != null).GroupBy(p => p.rule.name))
			{
				var near = g.Select(p => Flat(p.worldPosition - pos.Value).magnitude).OrderBy(d => d).ToList();
				Log(string.Format("  {0}: overlap radius {1:F0} m, view distance {2:F0} m, {3} points, {4} within 1 km, nearest {5}",
					g.Key, g.First().rule.collisionOverlapRadius, g.First().rule.viewDistance, near.Count, near.Count(d => d < 1000f),
					string.Join(", ", near.Take(4).Select(d => d.ToString("F0") + " m").ToArray())));
			}
		}

		[ConsoleCommand(name: "CISpawnGenerated", docs: "Dev, in game (host): the automatic spawner generates a brand-new island ahead of the raft now; checks its file, style and objects. CISpawnGenerated [keep]")]
		public static void SpawnGenerated(string[] args)
		{
			StartTest(SpawnGeneratedRoutine(args != null && args.Contains("keep")));
		}

		static IEnumerator SpawnGeneratedRoutine(bool keep)
		{
			Vector3? pos = CustomIslandSpawner.RaftPosition;
			if (!pos.HasValue) { Fail("not in a world"); yield break; }
			int before = IslandWorldState.Islands.Count;
			CustomIslandSpawner.ForceNextPick = CustomIslandSpawner.GeneratedEntry;
			string result;
			CustomIslandSpawner.TestAllRound = true;
			try { result = CustomIslandSpawner.TrySpawn(pos.Value, true); }
			finally { CustomIslandSpawner.TestAllRound = false; }
			Log(result);
			IslandWorldState.Entry e = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (e == null) { Fail("no island was added (" + result + ")"); yield break; }
			float t = 0f;
			while (e.Root == null && !e.Failed && t < 60f) { yield return new WaitForSeconds(0.5f); t += 0.5f; }
			bool fileOk = File.Exists(IslandSpawner.PathFor(e.Name));
			IslandFile file = fileOk ? IslandFile.Load(IslandSpawner.PathFor(e.Name)) : null;
			Terrain terrain = e.Root != null ? e.Root.GetComponentInChildren<Terrain>() : null;
			string style = terrain != null ? TerrainPainter.StyleName(TerrainPainter.StyleOf(terrain)) : "?";
			bool ok = fileOk && e.Root != null && e.Name.StartsWith(CustomIslandSpawner.GeneratedPrefix) && file.Objects.Count > 0 &&
				style.Equals(string.IsNullOrEmpty(file.Style) ? "Tropical" : file.Style) && e.Name.Contains(style.ToLowerInvariant());
			Log((ok ? "PASS" : "FAIL") + ": generated '" + e.Name + "' (" + (file != null ? file.Objects.Count + " objects, style " + style + (file.Elevation > 0 ? ", flying " + file.Elevation.ToString("F0") + " m" : "") : "no file") +
				") and spawned it " + (e.Root != null ? "at " + e.Position : "- it did not spawn") + " after " + t.ToString("F1") + " s");
			if (!keep)
			{
				IslandWorldState.RemoveIds(new[] { e.Id }, true);
				if (fileOk) File.Delete(IslandSpawner.PathFor(e.Name));
			}
		}

		[ConsoleCommand(name: "CIRadarTest", docs: "Dev, in game (host): places a Receiver next to the raft with its radar on and checks there is a dot per custom island, pointing the right way")]
		public static void RadarTest()
		{
			StartTest(RadarTestRoutine());
		}

		static IEnumerator RadarTestRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue) { Fail("not in a world"); yield break; }
			if (IslandWorldState.Islands.Count == 0)
			{
				// (the tests' own sample island, not whichever island the player has saved)
				string name = SaveSampleIsland();
				Vector3 at = raftPos.Value + new Vector3(0, 0, 300f);
				yield return DynamicIslands.instance.SpawnIslandFile(name, at, true);
			}
			// A receiver: Raft's own block prefab, from whichever buildable item carries a Reciever
			Reciever prefab = null;
			foreach (Item_Base item in ItemManager.GetAllItems())
			{
				try
				{
					if (item == null || item.settings_buildable == null || !item.settings_buildable.Placeable) continue;
					Block[] blocks = item.settings_buildable.GetBlockPrefabs();
					prefab = blocks != null ? blocks.Where(b => b != null).Select(b => b.GetComponentInChildren<Reciever>(true)).FirstOrDefault(rc => rc != null) : null;
					if (prefab != null) { Log("Receiver block: " + item.UniqueName); break; }
				}
				catch { }
			}
			if (prefab == null) { Fail("could not find Raft's receiver block"); yield break; }
			GameObject go = UnityEngine.Object.Instantiate(prefab.transform.root.gameObject, raftPos.Value + Vector3.up * 3f, Quaternion.identity);
			Reciever r = go.GetComponentInChildren<Reciever>(true);
			yield return null;
			if (r.radarSection != null) r.radarSection.SetActive(true);
			IslandRadar.Draw(r);
			var list = IslandRadar.DotsOf(r).Where(d => d != null && d.gameObject.activeSelf).ToList();
			// (LM2: far islands nobody needs any more are left off - one dot per island the radar shows)
			List<IslandWorldState.Entry> shown = IslandRadar.Shown;
			bool ok = list.Count == shown.Count && shown.Count > 0;
			// Each dot points towards its island, seen from the way the receiver faces (dot i is shown island i - CB13)
			float worst = 0f;
			for (int i = 0; ok && i < list.Count; i++)
			{
				Vector3 toIsland = shown[i].Position - r.transform.position;
				Vector3 seen = Quaternion.Euler(0f, -r.transform.eulerAngles.y, 0f) * new Vector3(toIsland.x, 0f, toIsland.z);
				Vector2 dotDir = ((RectTransform)list[i].transform).anchoredPosition;
				float angle = Vector2.Angle(new Vector2(seen.x, seen.z), -dotDir); // Raft's dot maths mirrors the vector
				if (i == 0) Log("Island 0 is at " + new Vector2(seen.x, seen.z) + " from the receiver, its dot at " + dotDir + " (angle " + angle.ToString("F0") + ")");
				if (dotDir.sqrMagnitude > 0.01f) worst = Mathf.Max(worst, angle);
			}
			Log((ok && worst < 3f ? "PASS" : "FAIL") + ": every dot points towards its island (worst " + worst.ToString("F1") + " degrees off)");
			Log((ok ? "PASS" : "FAIL") + ": the receiver shows " + list.Count + " custom island dot(s) for the " + shown.Count + " island(s) within reach or still needed (" + IslandWorldState.Islands.Count + " in the world)");
			// (ROADMAP LM2: with a short receiverDistance only the islands within it, and those still needed, show)
			float keep = CustomIslandSpawner.ReceiverDistance;
			CustomIslandSpawner.ReceiverDistance = 1f;
			IslandRadar.Draw(r);
			int near = IslandRadar.DotsOf(r).Count(d => d != null && d.gameObject.activeSelf);
			int needed = IslandWorldState.Islands.Count(e => ReturningIslands.Why(e) != null);
			CustomIslandSpawner.ReceiverDistance = keep;
			Log((near == needed ? "PASS" : "FAIL") + ": receiverDistance 1 m: " + near + " dot(s), the " + needed + " island(s) still needed");
			UnityEngine.Object.Destroy(go);
		}

		[ConsoleCommand(name: "CIRaftDemo", docs: "Dev, editor: builds a small abandoned raft from Raft's blocks on the water (an undoable placement) and points the camera at it")]
		public static void RaftDemo()
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); return; }
			Transform placed = GameObject.Find("PlacedObjects").transform;
			Terrain terrain = terraineditor.terrain;
			// Open water near a corner of the build area, on the 1.5 m grid
			Vector3 origin = terrain.transform.position + new Vector3(240f, 0f, 240f);
			float g = PlacementOptions.GridSize, sea = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
			var spawned = new System.Collections.Generic.List<GameObject>();
			System.Func<string, Vector3, float, GameObject> put = (name, pos, yaw) =>
			{
				GameObject go = PlaceableCatalog.Spawn(name, placed);
				if (go == null) { Log("missing " + name); return null; }
				go.transform.position = pos;
				go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
				go.AddComponent<EditorGameObject>().GameObjectName = name;
				spawned.Add(go);
				return go;
			};
			for (int x = 0; x < 4; x++)
				for (int z = 0; z < 3; z++)
					put("Block_Foundation", PlacementOptions.FloatIfBlock("Block_Foundation", origin + new Vector3(x * g, 0, z * g)), 0);
			float deck = sea + PlacementOptions.FoundationFloat + PlacementOptions.FoundationPlanks; // the foundations' planks, where walls stand
			put("Block_Pillar_Wood", origin + new Vector3(-g / 2, deck, -g / 2), 0);
			put("Block_Pillar_Wood", origin + new Vector3(g * 1.5f, deck, -g / 2), 0);
			put("Block_Pillar_Wood", origin + new Vector3(-g / 2, deck, g / 2), 0);
			put("Block_Pillar_Wood", origin + new Vector3(g * 1.5f, deck, g / 2), 0);
			put("Block_Wall_Thatch", origin + new Vector3(0, deck, -g / 2), 0);
			// (a roof on the four pillars, as the wreck map type's shelter has: Raft's roof blocks as its building puts them)
			RaftRoof.Hip((n, p, ry) => put(n, p, ry), origin + new Vector3(0, deck + RaftRoof.OnWalls, 0), 2, 1);
			put("Block_Ladder", origin + new Vector3(g * 3, deck, g), 90);
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(spawned, true));
			Transform cam = Camera.main.transform;
			cam.position = origin + new Vector3(-8f, sea + 8f, -12f);
			cam.LookAt(origin + new Vector3(g * 1.5f, sea, g));
			bool floating = spawned.Where(s => s.name == "Block_Foundation").All(s => Mathf.Abs(s.transform.position.y - (sea + PlacementOptions.FoundationFloat)) < 0.01f);
			Log((floating && spawned.Count == 20 ? "PASS" : "FAIL") + ": built an abandoned raft of " + spawned.Count + " Raft blocks; foundations float at the sea surface: " + floating);
		}

		[ConsoleCommand(name: "CIRoofDemo", docs: "Dev, editor: RaftRoof's hipped roofs of Raft's roof blocks (as the generator's huts get them) over huts of 1 x 1 to 5 x 4 cells in a row through the island's middle, Raft's pillars at every outer corner (an undoable placement): each pillar's top is under the roof (a ray up from it meets the roof within 0.3 m) and doesn't stick out of it (a ray down from above meets the roof first, over the pillar's top) - no roof floating over its pillars (the user, 2026-10-03). old: the generator's huts' roofs before (one straight piece in each cell's middle) - must FAIL. CIRoofDemo [wood|old]")]
		public static void RoofDemo(string[] args)
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); return; }
			bool wood = args != null && args.Contains("wood"), old = args != null && args.Contains("old");
			Transform placed = GameObject.Find("PlacedObjects").transform;
			Terrain terrain = terraineditor.terrain;
			Vector2 mid = EditorLandCentre();
			float g = PlacementOptions.GridSize;
			var spawned = new List<GameObject>();
			var pillars = new List<KeyValuePair<GameObject, Vector3>>();
			Func<string, Vector3, float, GameObject> put = (name, pos, yaw) =>
			{
				GameObject go = PlaceableCatalog.Spawn(name, placed);
				if (go == null) { Log("missing " + name); return null; }
				go.transform.position = pos;
				go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
				go.AddComponent<EditorGameObject>().GameObjectName = name;
				spawned.Add(go);
				return go;
			};
			int[][] sizes = { new[] { 1, 1 }, new[] { 2, 1 }, new[] { 1, 3 }, new[] { 2, 2 }, new[] { 3, 2 }, new[] { 3, 3 }, new[] { 4, 3 }, new[] { 5, 4 } };
			float x0 = mid.x - 27f;
			foreach (int[] s in sizes)
			{
				int w = s[0], d = s[1];
				float ground = terrain.SampleHeight(new Vector3(x0, 0f, mid.y)) + terrain.transform.position.y;
				Vector3 o = new Vector3(x0 + g / 2f, ground, mid.y - d * g / 2f + g / 2f);
				float deck = ground + PlacementOptions.FoundationPlanks;
				for (int x = 0; x < w; x++)
					for (int z = 0; z < d; z++)
						put("Block_Foundation", o + new Vector3(x * g, 0f, z * g), 0f);
				for (int x = 0; x <= w; x++)
					for (int z = 0; z <= d; z++)
						if (x == 0 || z == 0 || x == w || z == d)
							pillars.Add(new KeyValuePair<GameObject, Vector3>(put("Block_Pillar_Wood", new Vector3(o.x + (x - 0.5f) * g, deck, o.z + (z - 0.5f) * g), 0f), o + new Vector3((w - 1) * g / 2f, 0f, (d - 1) * g / 2f)));
				if (old) { for (int x = 0; x < w; x++) for (int z = 0; z < d; z++) put("Block_Roof_Straight_Thatch", new Vector3(o.x + x * g, deck + 2.4f, o.z + z * g), 0f); }
				else RaftRoof.Hip((n, p, ry) => put(n, p, ry), new Vector3(o.x, deck + RaftRoof.OnWalls, o.z), w, d, wood);
				x0 += w * g + 3f;
			}
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(spawned, true));
			// (on the roof blocks' meshes - their colliders sit lower than the thatch at the eaves, under a corner pillar's top: a
			// pillar is under the roof when its top - 6 cm in from its middle towards the hut's - is under the eave of a roof
			// block: within 0.3 m of the block's edge, the block's lowest edge within 10 cm of the pillar's top (a block of the
			// old huts in its cell's middle had a corner pillar 0.8 m from its edges, under thatch 0.6 m up); inside it when no
			// block over it comes lower than 25 cm under its top)
			var roofs = spawned.Where(go => go != null && go.GetComponent<EditorGameObject>().GameObjectName.StartsWith("Block_Roof"))
				.Select(go => go.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; })).ToList();
			int under = 0, through = 0;
			foreach (KeyValuePair<GameObject, Vector3> pc in pillars.Where(pc => pc.Key != null))
			{
				Bounds b = pc.Key.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
				Vector3 inward = new Vector3(pc.Value.x - b.center.x, 0f, pc.Value.z - b.center.z).normalized * 0.06f;
				Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z) + inward;
				var over = roofs.Where(rb => top.x >= rb.min.x && top.x <= rb.max.x && top.z >= rb.min.z && top.z <= rb.max.z).ToList();
				string at = "  the pillar at " + Num(top.x - mid.x) + " " + Num(top.z - mid.y) + " (top h=" + Num(top.y) + ")";
				if (over.Count == 0) { Log(at + ": no roof over it"); continue; }
				Func<Bounds, float> fromEdge = rb => Mathf.Min(Mathf.Min(top.x - rb.min.x, rb.max.x - top.x), Mathf.Min(top.z - rb.min.z, rb.max.z - top.z));
				if (over.Any(rb => fromEdge(rb) <= 0.3f && Mathf.Abs(rb.min.y - top.y) <= 0.1f)) under++;
				else Log(at + ": under no roof block's eave (" + string.Join(", ", over.Select(rb => Num(fromEdge(rb)) + " m from its edge, its eave " + Num(rb.min.y - top.y) + " m up").ToArray()) + ") - floating over it");
				float low = over.Min(rb => rb.min.y) - top.y;
				if (low >= -0.25f) through++; else Log(at + ": goes " + Num(-low) + " m up past a roof block's lowest edge - through it");
			}
			Transform cam = Camera.main.transform;
			cam.position = new Vector3(mid.x, terrain.SampleHeight(new Vector3(mid.x, 0f, mid.y)) + terrain.transform.position.y + 14f, mid.y - 26f);
			cam.LookAt(new Vector3(mid.x, cam.position.y - 12f, mid.y));
			int count = pillars.Count(pc => pc.Key != null);
			Log((count > 0 && under == count && through == count ? "PASS" : "FAIL") + ": roofs of " + sizes.Length + " huts (" + (old ? "the old ones" : wood ? "wood" : "thatch") + "), " + spawned.Count + " blocks: " + under + " of " + count + " pillars right under the roof, " + through + " of " + count + " inside it");
		}

		[ConsoleCommand(name: "CIPlaceTest", docs: "Dev, editor: object list search, Ground (with and without Slope) and its undo")]
		public static void PlaceTest()
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first (and wait for the objects to load)"); return; }
			StartTest(PlaceTestRoutine());
		}

		static IEnumerator PlaceTestRoutine()
		{
			bool ok = true;
			// 1. Search
			EditorUI.SetTab(TAB.ObjectPlace);
			yield return null;
			ObjectBrowser browser = ObjectBrowser.Instance;
			if (browser == null) { Fail("no object browser"); yield break; }
			browser.Search = "";
			int all = browser.VisibleObjects().Count;
			browser.Search = "cactus";
			System.Collections.Generic.List<string> found = browser.VisibleObjects();
			bool onlyCacti = found.All(f => (PlaceableCatalog.DisplayName(f) + f).ToLower().Contains("cactus"));
			browser.Search = "";
			Check(ref ok, found.Count > 0 && found.Count != all && onlyCacti && browser.VisibleObjects().Count == all, "search: 'cactus' shows " + found.Count + " objects (" + all + " in the open categories), clearing shows the list again");

			// 2. Ground: objects lifted into the air land on the terrain, as one undo step
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 3, ObjectDensity = 0f });
			Terrain terrain = terraineditor.terrain;
			Vector3 c = terrain.transform.position + new Vector3(500f, 0, 500f);
			var objs = new System.Collections.Generic.List<Transform>();
			for (int i = 0; i < 3; i++)
			{
				GameObject go = PlaceableCatalog.Spawn("SmallBoulder2", GameObject.Find("PlacedObjects").transform) ?? PlaceableCatalog.Spawn(PlaceableCatalog.Names.First(), GameObject.Find("PlacedObjects").transform);
				go.AddComponent<EditorGameObject>().GameObjectName = go.name;
				go.transform.position = c + new Vector3(30f + i * 20f, 200f, 40f);
				objs.Add(go.transform);
			}
			var gizmo = DynamicIslands.EditorGizmoHandler;
			gizmo.ClearTargets(false);
			foreach (Transform t in objs) gizmo.AddTarget(t, false);
			PlacementOptions.AlignToSlope = false;
			int n = PlacementOptions.DropSelectionToGround();
			// (without Slope an object keeps its own rotation; some of Raft's rocks come tilted; it goes down to the lowest
			// ground under its base, so on a slope its low side doesn't stand in the air; one whose pivot is well above its
			// bottom - the boulder's, 3.4 m - rests on its bottom there, RestingPivotY)
			float off = objs.Max(t => Mathf.Abs(t.position.y - PlacementOptions.RestingPivotY(t.gameObject)));
			CommandUndoRedo.UndoRedoManager.Undo();
			bool undone = objs.All(t => t.position.y > 150f);
			Check(ref ok, n == 3 && off < 0.2f && undone, "Ground put " + n + " objects on the terrain (largest height difference " + off.ToString("F2") + " m); Ctrl+Z lifted them back: " + undone);

			// 3. With Slope on they lean with the ground
			PlacementOptions.AlignToSlope = true;
			PlacementOptions.DropSelectionToGround();
			float worst = objs.Max(t =>
			{
				Vector3 point, normal;
				PlacementOptions.GroundAt(t.position, out point, out normal);
				return Vector3.Angle(t.up, normal);
			});
			PlacementOptions.AlignToSlope = false;
			Check(ref ok, worst < 2f, "with Slope on, objects lean with the ground (largest difference " + worst.ToString("F1") + " degrees)");
			gizmo.ClearTargets(false);
			foreach (Transform t in objs) UnityEngine.Object.Destroy(t.gameObject);
			if (ok) Log("PASS: placement tools test"); else Fail("placement tools test");
		}

		[ConsoleCommand(name: "CIObjInfo", docs: "Dev, editor: spawns catalog objects whose name contains <text> (or 'all') at the build-area centre and logs their rendered size; they are removed again")]
		public static void ObjInfo(string[] args)
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); return; }
			string filter = args != null && args.Length > 0 ? string.Join(" ", args) : "all";
			var lines = new System.Collections.Generic.List<string>();
			Vector3 c = terraineditor.terrain.transform.position + new Vector3(500f, 0, 500f);
			c.y = terraineditor.terrain.SampleHeight(c);
			foreach (string name in PlaceableCatalog.Names.Where(n => filter == "all" || n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
			{
				GameObject go = PlaceableCatalog.Spawn(name, null);
				if (go == null) continue;
				go.transform.position = c;
				Renderer[] rs = go.GetComponentsInChildren<Renderer>();
				Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(c, Vector3.zero);
				foreach (Renderer r in rs) b.Encapsulate(r.bounds);
				LODGroup lod = go.GetComponentInChildren<LODGroup>();
				lines.Add(string.Format("{0} [{1}]: size {2:F1} x {3:F1} x {4:F1} m, centre offset {5}, {6} renderers ({7} enabled){8}",
					name, PlaceableCatalog.CategoryOf(name), b.size.x, b.size.y, b.size.z, (b.center - c).ToString("F1"), rs.Length, rs.Count(r => r.enabled),
					lod != null ? ", LODGroup " + lod.lodCount + " levels, enabled=" + lod.enabled : ""));
				foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
					lines.Add(string.Format("    collider {0} {1}: enabled={2} activeInHierarchy={3} trigger={4} layer={5}",
						col.name, col.GetType().Name, col.enabled, col.gameObject.activeInHierarchy, col.isTrigger, LayerMask.LayerToName(col.gameObject.layer)));
				UnityEngine.Object.Destroy(go);
			}
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "objinfo.txt"));
			File.WriteAllLines(file, lines.ToArray());
			Log(lines.Count + " objects measured, written to " + file);
		}

		[ConsoleCommand(name: "CIStyleTest", docs: "Dev, editor: island styles - textures found, new object categories, and for every style: generate, save, load (files cistyle_<style>.island are deleted again)")]
		public static void StyleTest()
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first (and wait for the objects to load)"); return; }
			StartTest(StyleTestRoutine());
		}

		static IEnumerator StyleTestRoutine()
		{
			bool ok = true;
			var styles = TerrainPainter.Styles;
			Check(ref ok, Enumerable.Range(0, styles.Length).All(TerrainPainter.HasStyle),
				"Raft's textures found for the styles: " + string.Join(", ", Enumerable.Range(0, styles.Length).Select(i => styles[i].Name + (TerrainPainter.HasStyle(i) ? "" : " (missing)")).ToArray()));
			var cats = PlaceableCatalog.ByCategory().ToDictionary(c => c.Key, c => c.Value.Count);
			int ores = PlaceableCatalog.HarvestableNames.Count(n => System.Text.RegularExpressions.Regex.IsMatch(n, "Copper|Iron|Clay|Sand"));
			Check(ref ok, cats.ContainsKey(PlaceableCatalog.SnowCategory) && cats.ContainsKey(PlaceableCatalog.DesertCategory) && cats.ContainsKey(PlaceableCatalog.ForestCategory) && ores > 0,
				"object list: " + string.Join(", ", cats.Select(c => c.Value + " " + c.Key.ToLower()).ToArray()) + "; " + ores + " ore/clay/sand harvestables");

			Terrain terrain = terraineditor.terrain;
			int startStyle = DynamicIslands.currentStyle;
			for (int style = 0; style < styles.Length; style++)
			{
				var s = new IslandGenSettings { Seed = 11 + style, Radius = 90f, Height = 45f, Roughness = 0.5f, Peaks = 2, ObjectDensity = 0.5f, Style = style };
				int n = IslandGenerator.GenerateInEditor(s);
				string[] tex = terrain.terrainData.terrainLayers.Select(l => l != null && l.diffuseTexture != null ? l.diffuseTexture.name : "?").ToArray();
				bool texOk = Enumerable.Range(0, TerrainPainter.LayerCount).All(i => tex[i].StartsWith(styles[style].Textures[i]));
				string name = "cistyle_" + styles[style].Name.ToLower();
				DynamicIslands.SaveIsland(name);
				string saved = IslandFile.Load(IslandSpawner.PathFor(name)).Style;
				DynamicIslands.SetEditorStyle(TerrainPainter.Tropical);
				DynamicIslands.LoadIsland(name);
				yield return null;
				bool loaded = DynamicIslands.currentStyle == style && TerrainPainter.StyleOf(terrain) == style;
				Check(ref ok, texOk && n > 5 && (style == TerrainPainter.Tropical ? saved == "" : saved == styles[style].Name) && loaded,
					styles[style].Name + ": textures " + string.Join("/", tex) + ", " + n + " objects, saved as '" + saved + "', loads back as " + TerrainPainter.StyleName(DynamicIslands.currentStyle));
				File.Delete(IslandSpawner.PathFor(name));
			}

			// Generating is one undo step, including the style change
			DynamicIslands.SetEditorStyle(TerrainPainter.Forest);
			CommandUndoRedo.UndoRedoManager.Clear();
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 5, Style = TerrainPainter.Snowy, ObjectDensity = 0.2f });
			CommandUndoRedo.UndoRedoManager.Undo();
			bool undone = DynamicIslands.currentStyle == TerrainPainter.Forest;
			CommandUndoRedo.UndoRedoManager.Redo();
			Check(ref ok, undone && DynamicIslands.currentStyle == TerrainPainter.Snowy, "Ctrl+Z after generating also brings back the previous style");
			if (ok) Log("PASS: island styles test"); else Fail("island styles test");
		}

		[ConsoleCommand(name: "CIGenTest", docs: "Dev, editor: generates an island and checks it (same seed = same island, land above sea, fits the build area, objects, undo/redo, save/load as cigen.island)")]
		public static void GenTest()
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first (and wait for the objects to load)"); return; }
			bool ok = true;
			try
			{
				TerrainData data = terraineditor.terrain.terrainData;
				int res = data.heightmapResolution;
				var s = new IslandGenSettings { Seed = 4242, Radius = 120f, Height = 40f, Roughness = 0.5f, Peaks = 2, ObjectDensity = 0.5f };

				// 1. Deterministic, and seeds differ
				float[,] a = IslandGenerator.Heights(s, data.size, res), b = IslandGenerator.Heights(s, data.size, res);
				float[,] c = IslandGenerator.Heights(new IslandGenSettings { Seed = 777, Radius = 120f, Height = 40f, Roughness = 0.5f, Peaks = 2 }, data.size, res);
				bool same = a.Cast<float>().SequenceEqual(b.Cast<float>()), differs = !a.Cast<float>().SequenceEqual(c.Cast<float>());
				Check(ref ok, same && differs, "same seed gives the same island, another seed a different one");

				// 2. Shape: land above sea, peak near the requested height, flat seabed at the edges
				float sea = s.WaterLevel / data.size.y;
				float peak = a.Cast<float>().Max() * data.size.y - s.WaterLevel;
				int land = a.Cast<float>().Count(h => h > sea);
				float landArea = land * (data.size.x / (res - 1)) * (data.size.x / (res - 1));
				bool edgesFlat = Enumerable.Range(0, res).All(i => a[0, i] == 0f && a[res - 1, i] == 0f && a[i, 0] == 0f && a[i, res - 1] == 0f);
				Check(ref ok, peak > s.Height * 0.6f && peak < s.Height * 1.5f && landArea > 10000f && edgesFlat,
					string.Format("peak {0:F0} m above sea (asked {1:F0}), land area {2:F0} m², seabed flat at the edges: {3}", peak, s.Height, landArea, edgesFlat));

				// 3. Into the editor, as one undo step
				Transform placed = GameObject.Find("PlacedObjects").transform;
				float[,] before = data.GetHeights(0, 0, res, res);
				int n = IslandGenerator.GenerateInEditor(s);
				float[,] after = data.GetHeights(0, 0, res, res);
				bool applied = after.Cast<float>().Zip(a.Cast<float>(), (x, y) => Mathf.Abs(x - y)).Max() < 0.0001f;
				int visible = placed.GetComponentsInChildren<EditorGameObject>(false).Length;
				Check(ref ok, applied && n > 10 && visible == n, "generated into the editor with " + n + " objects (" + visible + " visible)");

				CommandUndoRedo.UndoRedoManager.Undo();
				bool undone = data.GetHeights(0, 0, res, res).Cast<float>().Zip(before.Cast<float>(), (x, y) => Mathf.Abs(x - y)).Max() < 0.0001f;
				int visibleAfterUndo = placed.GetComponentsInChildren<EditorGameObject>(false).Length;
				CommandUndoRedo.UndoRedoManager.Redo();
				bool redone = data.GetHeights(0, 0, res, res).Cast<float>().Zip(a.Cast<float>(), (x, y) => Mathf.Abs(x - y)).Max() < 0.0001f
					&& placed.GetComponentsInChildren<EditorGameObject>(false).Length == n;
				Check(ref ok, undone && visibleAfterUndo != n && redone, "Ctrl+Z restores the previous island (" + visibleAfterUndo + " objects), Ctrl+Y the generated one");

				// 4. Save, load, and check it would spawn cropped to the island (not the whole 1000 m square)
				DynamicIslands.SaveIsland("cigen");
				IslandFile file = IslandFile.Load(IslandSpawner.PathFor("cigen"));
				int cx, cz, size;
				IslandSpawner.GetCropArea(file, out cx, out cz, out size);
				float radius = IslandSpawner.LandRadius(file);
				Check(ref ok, file.Objects.Count == n && size <= res,
					"saved as cigen.island with " + file.Objects.Count + " objects; spawns as a " + (size - 1) * data.size.x / (res - 1) + " m terrain block, land radius " + radius.ToString("F0") + " m");
				DynamicIslands.LoadIsland("cigen");
				StartTest(GenTestLoaded(ok, a, n, s));
			}
			catch (Exception e) { Fail("exception: " + e); Fail("island generator test"); }
		}

		/// <summary>Checks the loaded island a frame later, once the replaced objects are really destroyed.</summary>
		static IEnumerator GenTestLoaded(bool ok, float[,] expected, int n, IslandGenSettings s)
		{
			yield return null;
			TerrainData data = terraineditor.terrain.terrainData;
			int res = data.heightmapResolution;
			float diff = data.GetHeights(0, 0, res, res).Cast<float>().Zip(expected.Cast<float>(), (x, y) => Mathf.Abs(x - y)).Max() * data.size.y;
			int objects = GameObject.Find("PlacedObjects").GetComponentsInChildren<EditorGameObject>(false).Length;
			Check(ref ok, diff < 0.05f && objects == n, "loading cigen gives the same heights (largest difference " + diff.ToString("F3") + " m) and objects (" + objects + ")");
			IslandGenerator.FrameCamera(s);
			if (ok) Log("PASS: island generator test"); else Fail("island generator test");
		}

		static void Check(ref bool ok, bool condition, string what)
		{
			Log((condition ? "PASS: " : "FAIL: ") + what);
			ok &= condition;
		}

		/// <summary>
		/// A flying island seen from below: its rocky underside must meet the ground all along the coast, or the sky shows
		/// through between them (Lantern Spire 10-16: zigzag gaps where the coast ran diagonally). Two looks: the underside's
		/// rim points on every coast edge sit on the ground, and a ray from below and outside aimed into the rock just inside
		/// each coast edge passes the underside on its way in. Null when closed, else what is open.
		/// </summary>
		static string CellsRound(bool[,] solid, HashSet<int> meshCells, int cells, int px, int pz)
		{
			string s = "";
			for (int z = pz - 1; z <= pz; z++)
				for (int x = px - 1; x <= px; x++)
					s += x < 0 || z < 0 || x >= cells || z >= cells ? "-/- " : (solid[z, x] ? "1" : "0") + "/" + (meshCells.Contains(z * cells + x) ? "1" : "0") + " ";
			return s.Trim();
		}

		internal static string FlyingOpenFromBelow(GameObject root)
		{
			Terrain terrain = root != null ? root.GetComponentInChildren<Terrain>() : null;
			Transform under = terrain != null ? terrain.transform.Find("Underside") : null;
			MeshCollider uc = under != null ? under.GetComponent<MeshCollider>() : null;
			if (uc == null) return "no underside";
			Physics.SyncTransforms();
			TerrainData data = terrain.terrainData;
			int cells = data.holesResolution, res = data.heightmapResolution;
			if (res - 1 != cells) return null; // (not the grid MakeFlying works on)
			bool[,] solid = data.GetHoles(0, 0, cells, cells);
			float spacing = data.size.x / cells;
			// the underside's points by grid sample (in the terrain's own space)
			var rimY = new Dictionary<int, float>();
			foreach (Vector3 v in under.GetComponent<MeshFilter>().sharedMesh.vertices)
				rimY[Mathf.RoundToInt(v.z / spacing) * res + Mathf.RoundToInt(v.x / spacing)] = v.y;
			// the cells the underside covers (from its triangles)
			Mesh um = under.GetComponent<MeshFilter>().sharedMesh;
			Vector3[] uv = um.vertices;
			int[] ut = um.triangles;
			var meshCells = new HashSet<int>();
			for (int i = 0; i + 2 < ut.Length; i += 3)
			{
				float mx = Mathf.Min(uv[ut[i]].x, Mathf.Min(uv[ut[i + 1]].x, uv[ut[i + 2]].x)), mz = Mathf.Min(uv[ut[i]].z, Mathf.Min(uv[ut[i + 1]].z, uv[ut[i + 2]].z));
				meshCells.Add(Mathf.RoundToInt(mz / spacing) * cells + Mathf.RoundToInt(mx / spacing));
			}
			int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
			int edges = 0, seams = 0, rays = 0, through = 0, byGround = 0;
			TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
			string first = null;
			Vector3 origin = terrain.transform.position;
			for (int z = 0; z < cells; z++)
				for (int x = 0; x < cells; x++)
				{
					if (!solid[z, x]) continue;
					for (int k = 0; k < 4; k++)
					{
						int nx = x + dx[k], nz = z + dz[k];
						if (nx >= 0 && nz >= 0 && nx < cells && nz < cells && solid[nz, nx]) continue;
						edges++;
						// the edge's two ends (grid samples)
						int ax = dx[k] == 1 ? x + 1 : x, az = dz[k] == 1 ? z + 1 : z;
						int bx = dx[k] != 0 ? ax : x + 1, bz = dx[k] != 0 ? z + 1 : az;
						foreach (var s in new[] { new[] { ax, az }, new[] { bx, bz } })
						{
							float y;
							float ground = data.GetHeight(s[0], s[1]);
							if (!rimY.TryGetValue(s[1] * res + s[0], out y) || Mathf.Abs(y - ground) > 0.05f)
							{
								seams++;
								if (first == null) first = "the underside is " + (rimY.ContainsKey(s[1] * res + s[0]) ? (ground - y).ToString("F1") + " m under the ground" : "missing") + " at the coast point " + s[0] + "," + s[1] + " (cells round it, holes map / underside: " + CellsRound(solid, meshCells, cells, s[0], s[1]) + ")";
							}
						}
						// the ray: into the rock half a cell in from the edge, half way between the ground and the underside
						Vector3 outward = new Vector3(dx[k], 0f, dz[k]);
						Vector3 inner = origin + new Vector3((x + 0.5f) * spacing, 0f, (z + 0.5f) * spacing);
						float top = terrain.SampleHeight(inner) + origin.y;
						RaycastHit hit;
						if (!uc.Raycast(new Ray(new Vector3(inner.x, top - 500f, inner.z), Vector3.up), out hit, 500f)) continue;
						if (top - hit.point.y < 0.1f) continue; // (too thin to aim into)
						Vector3 target = new Vector3(inner.x, (top + hit.point.y) / 2f, inner.z);
						Vector3 from = target + (outward + Vector3.down) * 4f; // (at 45 degrees from below: through the coast face, under the ground)
						rays++;
						if (!uc.Raycast(new Ray(from, target - from), out hit, (target - from).magnitude))
						{
							// (the ground's own face first, from its open side: a coast cell where the terrain splits its square on the
							// other diagonal than the underside, so the point lies in a sliver between them and the ray comes in over the
							// rim - seen from below that is the ground, not the sky; Lodestone Array, Altitude Market 10-20)
							if (tc != null && tc.Raycast(new Ray(from, target - from), out hit, (target - from).magnitude) && Vector3.Dot(hit.normal, target - from) < 0f) { byGround++; continue; }
							through++;
							if (first == null) first = "a ray from below reaches the rock at " + (target - origin).ToString("F1") + " without passing the underside";
						}
					}
				}
			if (seams == 0 && through == 0) return null;
			int differ = 0;
			for (int z = 0; z < cells; z++) for (int x = 0; x < cells; x++) if (solid[z, x] != meshCells.Contains(z * cells + x)) differ++;
			first += "; " + differ + " cells differ between the holes map and the underside, " + meshCells.Count + " under it";
			return seams + " of " + (edges * 2) + " coast points open, " + through + " of " + rays + " rays from below get in (" + byGround + " met the ground's face first); first: " + first;
		}

		[ConsoleCommand(name: "CIDemo", docs: "Dev: scatters sample nature objects over the editor terrain (as undoable placements) and frames the camera")]
		public static void Demo()
		{
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); return; }
			Terrain terrain = terraineditor.terrain;
			Transform placed = GameObject.Find("PlacedObjects").transform;
			Vector3 c = terrain.transform.position + new Vector3(terrain.terrainData.size.x / 2f, 0, terrain.terrainData.size.z / 2f);
			string[] kinds = { "Pickup_Landmark_Tree_Palm 1", "Pickup_Landmark_Tree_Palm 2", "Pickup_Landmark_Tree_Palm 3", "Pickup_Landmark_Tree_Palm 4", "BigPalm1", "BigPalm3", "Pickup_Landmark_MangoTree", "Pickup_Landmark_Rock 1", "Pickup_Landmark_BerryBush", "BigBoulder1_Low", "BigBoulder3_Low",
				"SmallBoulder2", "Bush", "Bush2", "Monstera_1", "Banana_Bush_1", "Banana_Bush_2", "Log", "BigRock_Low1_Sand", "TableCoral_1", "LeafCoral_1", "CauliCoral" };
			var rnd = new System.Random(7);
			var spawned = new System.Collections.Generic.List<GameObject>();
			for (int i = 0; i < 60; i++)
			{
				string kind = kinds[i % kinds.Length];
				if (PlaceableCatalog.Get(kind) == null) continue;
				bool coral = kind.Contains("Coral");
				float r = coral ? 75f + (float)rnd.NextDouble() * 25f : (float)rnd.NextDouble() * 55f;
				float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
				Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
				p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
				GameObject go = PlaceableCatalog.Spawn(kind, placed);
				go.transform.position = p;
				go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
				go.AddComponent<EditorGameObject>().GameObjectName = kind;
				spawned.Add(go);
			}
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(spawned, true));
			Transform cam = Camera.main.transform;
			cam.position = c + new Vector3(-60f, DynamicIslands.EditorWaterLevel + 30f, -95f);
			cam.LookAt(c + new Vector3(0, DynamicIslands.EditorWaterLevel + 5f, 0));
			Log("Placed " + spawned.Count + " sample objects (Ctrl+Z removes them)");
		}

		static string LayerList(int mask)
		{
			var names = new System.Collections.Generic.List<string>();
			for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) names.Add(i + ":" + LayerMask.LayerToName(i));
			return string.Join(", ", names.ToArray());
		}

		static void Describe(System.Collections.Generic.List<string> lines, Transform t, int depth, int maxDepth)
		{
			string pad = new string(' ', 2 + depth * 2);
			var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c =>
			{
				string s = c.GetType().Name;
				Collider col = c as Collider;
				if (col != null) s += (col.isTrigger ? "(trigger)" : "") + (col.enabled ? "" : "(disabled)");
				return s;
			}).ToArray();
			lines.Add(pad + t.name + "  [layer " + t.gameObject.layer + ":" + LayerMask.LayerToName(t.gameObject.layer) + ", tag " + t.tag + (t.gameObject.activeSelf ? "" : ", INACTIVE") + "]  " + string.Join(", ", comps));
			if (depth < maxDepth) foreach (Transform c in t) Describe(lines, c, depth + 1, maxDepth);
		}

		[ConsoleCommand(name: "CIInspect", docs: "Dev: CIInspect <scene> <name>[,<name>...] - layers, tags and components of objects in a Raft scene, plus Raft's layer masks")]
		public static void Inspect(string[] args)
		{
			if (args == null || args.Length < 2) { Log("Usage: CIInspect <scene> <name>[,<name>...]"); return; }
			StartTest(InspectRoutine(args[0], string.Join(" ", args.Skip(1).ToArray()).Split(',')));
		}

		static IEnumerator InspectRoutine(string sceneName, string[] names)
		{
			var lines = new System.Collections.Generic.List<string>();
			lines.Add("LAYERS:");
			for (int i = 0; i < 32; i++) { string n = LayerMask.LayerToName(i); if (!string.IsNullOrEmpty(n)) lines.Add("  " + i + " " + n); }
			lines.Add("LAYERMASKS (static, as currently set):");
			foreach (var fi in typeof(LayerMasks).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
				if (fi.FieldType == typeof(LayerMask)) lines.Add("  " + fi.Name + " = " + LayerList(((LayerMask)fi.GetValue(null)).value));

			var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
			bool loadedByUs = !scene.isLoaded;
			if (loadedByUs)
			{
				AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
				if (op == null) { Fail("cannot load scene " + sceneName); yield break; }
				while (!op.isDone) yield return null;
				scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
			}
			lines.Add("RAFT colliders:");
			Raft raftForInspect = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftForInspect != null)
				foreach (var g in raftForInspect.body.GetComponentsInChildren<Collider>(true).GroupBy(c => LayerMask.LayerToName(c.gameObject.layer) + " " + c.GetType().Name + (c.isTrigger ? " trigger" : "")))
					lines.Add("  " + g.Count() + "x " + g.Key + " e.g. " + g.First().name);
			lines.Add("Layer collisions with Obstruction(16) / RaftCollision(9):");
			for (int i = 0; i < 32; i++) if (!string.IsNullOrEmpty(LayerMask.LayerToName(i))) lines.Add("  " + i + " " + LayerMask.LayerToName(i) + ": " + !Physics.GetIgnoreLayerCollision(i, 16) + " / " + !Physics.GetIgnoreLayerCollision(i, 9));
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				lines.Add("OBJECTS ON LAYER 9 (RaftCollision) in " + root.name + ":");
				foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.gameObject.layer == 9).Take(40))
					lines.Add("  " + Path_(t) + "  " + string.Join(", ", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name + (c is MeshCollider ? "(" + (((MeshCollider)c).sharedMesh != null ? ((MeshCollider)c).sharedMesh.name + " " + ((MeshCollider)c).sharedMesh.vertexCount + "v" : "no mesh") + ")" : "")).ToArray()));
				lines.Add("ROOT:"); Describe(lines, root.transform, 0, 1);
				foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true)) { lines.Add("TERRAIN:"); Describe(lines, t.transform, 0, 1); }
				foreach (string wanted in names)
				{
					string w = wanted.Trim();
					Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => PlaceableCatalog.CleanName(t.name) == w);
					if (found != null) { lines.Add("OBJECT " + w + " (" + Path_(found) + "):"); Describe(lines, found, 0, 3); }
					else lines.Add("OBJECT " + w + ": not found");
				}
			}
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "inspect_" + sceneName.Replace("#", "_") + ".txt"));
			File.WriteAllLines(file, lines.ToArray());
			Log("Inspected " + sceneName + ", written to " + file);
			if (loadedByUs)
			{
				AsyncOperation unload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
				if (unload != null) while (!unload.isDone) yield return null;
			}
		}

		static GameObject NearestIsland(Vector3 from)
		{
			return UnityEngine.Object.FindObjectsOfType<GameObject>().Where(g => g.transform.parent == null && g.name.StartsWith("CustomIsland_"))
				.OrderBy(g => (g.transform.position - from).sqrMagnitude).FirstOrDefault();
		}

		[ConsoleCommand(name: "CIFlyTest", docs: "Dev, in game (host): spawns an island flying at 60 m and one under water and checks them. CIFlyTest [island] [keep] (keep = leave them spawned)")]
		public static void FlyTest(string[] args)
		{
			// (no island named: the tests' own sample island, saved as cisample - not whichever island the player has saved)
			string name = args != null && args.Length > 0 && args[0] != "keep" ? args[0] : SaveSampleIsland();
			bool keep = args != null && args.Contains("keep");
			StartTest(FlyTestRoutine(name, keep));
		}

		static IEnumerator FlyTestRoutine(string name, bool keep)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue) { Fail("not in a world"); yield break; }
			bool ok = true;
			const float Flying = 60f, Sunken = -45f;
			int obstruction = 1 << IslandSpawner.TerrainLayer;

			// Flying island 250 m ahead
			Vector3 pos = raftPos.Value + Vector3.forward * 250f; pos.y = Flying;
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile(name, pos, true);
			IslandWorldState.Entry fly = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (fly == null || fly.Root == null) { Fail("flying island did not spawn"); yield break; }
			yield return new WaitForSeconds(0.5f);
			Terrain terrain = fly.Root.GetComponentInChildren<Terrain>();
			TerrainData data = terrain.terrainData;
			bool[,] holes = data.GetHoles(0, 0, data.holesResolution, data.holesResolution);
			int holeCount = holes.Cast<bool>().Count(solid => !solid);
			MeshFilter underside = fly.Root.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m => m.name == "Underside");
			Bounds ub = underside != null ? underside.GetComponent<Renderer>().bounds : new Bounds();
			Check(ref ok, holeCount > 0 && underside != null && underside.sharedMesh.vertexCount > 0 && ub.min.y >= 7.5f,
				"flying island: " + holeCount + "/" + holes.Length + " terrain cells cut away, underside " + (underside != null ? underside.sharedMesh.vertexCount + " vertices, lowest point " + ub.min.y.ToString("F1") + " m above the sea" : "missing"));

			Vector3 top = HighestPoint(terrain);
			RaycastHit hit;
			bool fromAbove = Physics.Raycast(new Vector3(top.x, 400f, top.z), Vector3.down, out hit, 500f, obstruction) && hit.collider.GetComponent<Terrain>() == terrain;
			float topY = fromAbove ? hit.point.y : 0f;
			bool fromBelow = Physics.Raycast(new Vector3(top.x, 1f, top.z), Vector3.up, out hit, 400f, obstruction) && hit.collider.name == "Underside";
			float bottomY = fromBelow ? hit.point.y : 0f;
			// A corner of the terrain block is open sea in the editor: it must be a hole now
			Vector3 corner = terrain.transform.position + new Vector3(3f, 0, 3f);
			bool cornerOpen = !Physics.Raycast(new Vector3(corner.x, 400f, corner.z), Vector3.down, out hit, 500f, obstruction) || hit.collider.transform.root != fly.Root.transform;
			Check(ref ok, fromAbove && fromBelow && cornerOpen && topY > Flying,
				string.Format("from above you land on the top ({0:F0} m), from below you hit the underside ({1:F0} m), the old seabed corner is open: {2}", topY, bottomY, cornerOpen));
			string open = FlyingOpenFromBelow(fly.Root);
			Check(ref ok, open == null, "seen from below the underside meets the ground all round the coast" + (open != null ? ": " + open : ""));
			int low = fly.Root.GetComponentsInChildren<EditorGameObject>(true).Length + fly.Root.transform.Find("Objects").Cast<Transform>().Count(t => t.position.y < Flying - 1f);
			Check(ref ok, low == 0, "no objects hang below the flying island (" + fly.Root.transform.Find("Objects").childCount + " objects)");

			yield return StandRoutine(); // puts the player on the nearest island's peak and logs PASS/FAIL

			// Under water, 250 m to the side
			pos = raftPos.Value + Vector3.right * 300f; pos.y = Sunken;
			before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile(name, pos, true);
			IslandWorldState.Entry sunk = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (sunk == null || sunk.Root == null) { Fail("underwater island did not spawn"); yield break; }
			Terrain t2 = sunk.Root.GetComponentInChildren<Terrain>();
			Vector3 top2 = HighestPoint(t2);
			bool noHoles = t2.terrainData.GetHoles(0, 0, t2.terrainData.holesResolution, t2.terrainData.holesResolution).Cast<bool>().All(solid => solid);
			Check(ref ok, top2.y < 0f && noHoles && sunk.Root.GetComponentsInChildren<MeshFilter>().All(m => m.name != "Underside"),
				"underwater island: highest point " + top2.y.ToString("F1") + " m (below the surface), no holes, no underside");

			// Saved with the world like any other island (the position's y is the elevation)
			// (read back from the saved file: the entries in memory always had them - CB13)
			IslandWorldState.Save();
			string[] saved = File.Exists(IslandWorldState.WorldFilePath) ? File.ReadAllLines(IslandWorldState.WorldFilePath) : new string[0];
			Func<IslandWorldState.Entry, float, bool> keeps = (e, y) => saved.Any(l => { string[] p = l.Split('|'); float v; return p.Length > 3 && p[0] == e.HostName && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && Mathf.Abs(v - y) < 0.01f; });
			Check(ref ok, keeps(fly, Flying) && keeps(sunk, Sunken), "the world's saved island list keeps both heights");

			if (!keep) { IslandWorldState.RemoveIds(new[] { fly.Id, sunk.Id }, true); if (name == SampleSaved) RemoveSampleIsland(); }
			if (ok) Log("PASS: flying and underwater islands test" + (keep ? " (islands kept)" : "")); else Fail("flying and underwater islands test");
		}

		[ConsoleCommand(name: "CIStand", docs: "Dev, in game: puts the local player on the highest point of the nearest custom island and logs whether they stay standing")]
		public static void Stand()
		{
			StartTest(StandRoutine());
		}

		static IEnumerator StandRoutine(GameObject on = null)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("no local player (not in a world?)"); yield break; }
			GameObject island = on != null ? on : NearestIsland(player.transform.position);
			if (island == null) { Fail("no custom island spawned"); yield break; }
			Terrain terrain = island.GetComponentInChildren<Terrain>();
			Vector3 target;
			if (terrain != null)
			{
				Vector3 top = HighestPoint(terrain);
				target = top + Vector3.up * 1.5f + new Vector3(3f, 0, 3f); // a little off the peak
				// (a generated peak can be a needle or a cliff edge: the highest gentle spot near it, where a player can stand)
				TerrainData td = terrain.terrainData;
				float best = float.MinValue;
				for (float dx = -24f; dx <= 24f; dx += 3f)
					for (float dz = -24f; dz <= 24f; dz += 3f)
					{
						Vector3 p = top + new Vector3(dx, 0f, dz);
						Vector3 local = p - terrain.transform.position;
						float u = local.x / td.size.x, v = local.z / td.size.z;
						if (u < 0f || u > 1f || v < 0f || v > 1f || td.GetSteepness(u, v) > 25f) continue;
						float y = terrain.SampleHeight(p);
						if (y > best) { best = y; target = p; }
					}
				target.y = terrain.SampleHeight(target) + terrain.transform.position.y + 1.5f;
			}
			else
			{
				// No land (an island of objects, e.g. a raft of Raft blocks): stand on top of them, in the middle
				Collider[] cols = island.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
				if (cols.Length == 0) { Fail("the island has no land and no colliders"); yield break; }
				Bounds b = cols[0].bounds;
				foreach (Collider c in cols) b.Encapsulate(c.bounds);
				target = new Vector3(b.center.x, b.max.y + 3f, b.center.z);
				RaycastHit hit;
				if (Physics.Raycast(target, Vector3.down, out hit, 20f) && hit.collider.transform.IsChildOf(island.transform)) target.y = hit.point.y + 1.5f;
				// (a raft of foundations: on a deck tile with open sky over it as wide as a player, the one nearest the middle - a
				// wreck's middle can be one of its missing foundations, and the player put above it fell through the hole into the
				// sea: The Abyss Expedition's test, 2026-10-02; CIWreckDeck shows the deck itself holds)
				Func<BoxCollider, Vector3> topOf = d => new Vector3(d.bounds.center.x, d.bounds.max.y, d.bounds.center.z);
				BoxCollider tile = island.GetComponentsInChildren<BoxCollider>().Where(d => d.name == "CustomIslands_Deck").OrderBy(d => (topOf(d) - b.center).sqrMagnitude)
					.FirstOrDefault(d => { RaycastHit h; return Physics.SphereCast(topOf(d) + Vector3.up * 8f, 0.6f, Vector3.down, out h, 9f, ~0, QueryTriggerInteraction.Ignore) && h.collider == d; });
				if (tile != null) target = topOf(tile) + Vector3.up * 1.5f;
			}

			// (a player who is down has no character controller and hangs where put: player 2 in IM15, killed by the boar it hit)
			yield return EnsureAlive();
			// (as the mod moves a player: off the raft first - a player still a child of the rocking raft dropped through the island -
			// and walking: a swimmer stays in Raft's swimming mode after a teleport, held at the sea surface)
			PlayerMove.To(player, target);
			Log("Teleported player to " + target + " on " + island.name);
			int exceptions = 0; string firstException = null;
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception) { exceptions++; if (firstException == null) firstException = msg + " " + trace.Split('\n').FirstOrDefault(); } };
			Application.logMessageReceived += counter;

			for (int i = 0; i < 12; i++)
			{
				yield return new WaitForSeconds(0.5f);
				PersonController pc = player.PersonController;
				Collider ground = pc.groundRaycastHit.collider;
				Log(string.Format("t={0:F1}s pos={1} grounded={2} standing on {3} (layer {4}){5}", (i + 1) * 0.5f, player.transform.position, pc.IsGrounded,
					ground != null ? ground.name : "nothing", ground != null ? LayerMask.LayerToName(ground.gameObject.layer) : "-", pc.HasRaftAsParent ? ", a child of the raft" : ""));
			}
			// (in the air at the last look: an animal knocked the player up a moment before - Thornwood, 2026-10-17: pushed 4 m, 2 m up,
			// 'grounded=False' at 6 s. Up to 2 s more to land)
			for (float t = 0f; t < 2f && !player.PersonController.IsGrounded; t += 0.25f) yield return new WaitForSeconds(0.25f);
			Application.logMessageReceived -= counter;
			float drop = target.y - player.transform.position.y;
			Log("Exceptions while standing: " + exceptions + (firstException != null ? " (first: " + firstException + ")" : ""));
			// (pushed along the island - the creature island's warthogs charge and knocked the player 4 m downhill - is still
			// standing on it: grounded on the island's own ground at the new spot, not under it and not in the sea)
			Collider under = player.PersonController.groundRaycastHit.collider;
			bool onIsland = under != null && under.transform.IsChildOf(island.transform);
			float surface = float.NegativeInfinity;
			foreach (Terrain t in island.GetComponentsInChildren<Terrain>(true)) surface = Mathf.Max(surface, t.SampleHeight(player.transform.position) + t.transform.position.y);
			bool aboveGround = float.IsNegativeInfinity(surface) || player.transform.position.y > surface - 1f;
			// (on the island and still a child of the raft: the raft's drift and rocking carry the player off - IslandGroundParentPatch)
			bool raftChild = player.PersonController.HasRaftAsParent && onIsland;
			if (raftChild) Log("  (standing on the island but a child of the raft: a moving raft would carry them)");
			bool ok = player.PersonController.IsGrounded && exceptions == 0 && !raftChild && (drop < 3f || (onIsland && aboveGround));
			if (ok && drop >= 3f) Log("  (moved off the spot - pushed or slid - but still standing on the island's ground)");
			if (ok) Log("PASS: player stands on the custom island (dropped " + drop.ToString("F2") + " m)");
			else
			{
				// (what was under the spot: every terrain of the island and what a ray from high above finds there - the ground
				// was sometimes missing under a player put on a library island, and was there the next time)
				foreach (Terrain t in island.GetComponentsInChildren<Terrain>(true))
				{
					TerrainCollider tc = t.GetComponent<TerrainCollider>();
					Log("  terrain '" + t.name + "' under " + (t.transform.parent != null ? t.transform.parent.name : "-") + " at " + t.transform.position.ToString("F1") + " size " + t.terrainData.size.ToString("F0") +
						" active " + t.gameObject.activeInHierarchy + " collider " + (tc == null ? "none" : "enabled " + tc.enabled + ", same data " + (tc.terrainData == t.terrainData) + ", bounds y " + tc.bounds.min.y.ToString("F1") + ".." + tc.bounds.max.y.ToString("F1")) +
						", height at the spot " + (t.SampleHeight(target) + t.transform.position.y).ToString("F1"));
				}
				foreach (RaycastHit h in Physics.RaycastAll(target + Vector3.up * 60f, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Take(6))
					Log("  a ray down from 60 m above finds " + h.collider.name + " (" + h.collider.GetType().Name + ", layer " + LayerMask.LayerToName(h.collider.gameObject.layer) + ") at y " + h.point.y.ToString("F1"));
				// (held still in the air - no fall at all - is Raft's PersonController.Update stopping early: these are its checks)
				PersonController hp = player.PersonController;
				Log("  the player's controller: enabled " + hp.enabled + ", type " + hp.controllerType + ", character controller " + (hp.controller != null ? hp.controller.enabled.ToString() : "none") +
					", local " + player.IsLocalPlayer + ", dead " + (player.PlayerScript != null && player.PlayerScript.IsDead) + ", flight camera " + (player.flightCamera != null && player.flightCamera.enabled) +
					", parent " + (player.transform.parent != null ? player.transform.parent.name : "none") + ", cursor free " + Cursor.visible + ", time scale " + Time.timeScale);
				Fail("player did not stay on the island (dropped " + drop.ToString("F1") + " m, grounded=" + player.PersonController.IsGrounded + ")");
			}
		}

		[ConsoleCommand(name: "CIProbe", docs: "Dev, in game: what is below the local player and on each custom island's terrain (colliders, layers) - for walking problems")]
		public static void Probe()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("no local player"); return; }
			Vector3 p = player.transform.position;
			Log("Player at " + p + ", grounded=" + player.PersonController.IsGrounded + ", in water " + player.PersonController.transform.position.y.ToString("F1") + ", camera " + (Camera.main != null ? Camera.main.transform.position.ToString() : "?"));
			foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 50f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
				Log(string.Format("  below: {0} ({1}) at y {2:F1}, layer {3}, enabled {4}", h.collider.name, h.collider.GetType().Name, h.point.y, LayerMask.LayerToName(h.collider.gameObject.layer), h.collider.enabled));
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) { Log(e.Name + ": not loaded"); continue; }
				foreach (Terrain t in e.Root.GetComponentsInChildren<Terrain>(true))
				{
					TerrainCollider tc = t.GetComponent<TerrainCollider>();
					Log(string.Format("{0}: terrain {1} at {2}, active {3}, layer {4}, collider {5} enabled {6} data {7}", e.Name, t.name, t.transform.position, t.gameObject.activeInHierarchy,
						LayerMask.LayerToName(t.gameObject.layer), tc != null, tc != null && tc.enabled, tc != null && tc.terrainData != null ? tc.terrainData.size.ToString() : "none"));
				}
			}
		}

		[ConsoleCommand(name: "CIRaftWatch", docs: "Dev, in game: logs the raft's position and speed every 2 s for <seconds> (default 60)")]
		public static void RaftWatch(string[] args)
		{
			float seconds = 60f; float s;
			if (args != null && args.Length > 0 && float.TryParse(args[0], out s)) seconds = s;
			StartTest(RaftWatchRoutine(seconds));
		}

		static IEnumerator RaftWatchRoutine(float seconds)
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null) { Fail("no raft"); yield break; }
			Transform body = raft.body != null ? raft.body.transform : raft.transform; // the raft moves through its rigidbody
			Vector3 last = body.position;
			for (float t = 0; t < seconds; t += 2f)
			{
				yield return new WaitForSeconds(2f);
				Vector3 p = body.position;
				RaycastHit hit;
				string below = Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out hit, 40f, 1 << IslandSpawner.TerrainLayer) ? hit.collider.name + " at depth " + (p.y - hit.point.y).ToString("F1") : "open water";
				GameObject island = NearestIsland(p);
				string dist = island != null ? (island.GetComponentInChildren<Terrain>() != null ? " | distance to island peak " + Vector3.Distance(new Vector3(p.x, 0, p.z), Flat(HighestPoint(island.GetComponentInChildren<Terrain>()))).ToString("F0") + " m" : "") : "";
				Log(string.Format("raft t={0:F0}s pos={1} speed={2:F2} m/s below: {3}{4}", t + 2f, p, (p - last).magnitude / 2f, below, dist));
				last = p;
			}
		}

		static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0, v.z); }

		[ConsoleCommand(name: "CIWorldWatch", docs: "Dev, in game: logs how floating items, landmarks and chunk points move over 10 s (does the world move, or the raft?)")]
		public static void WorldWatch()
		{
			StartTest(WorldWatchRoutine());
		}

		static IEnumerator WorldWatchRoutine()
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			var watched = new System.Collections.Generic.List<Transform>();
			watched.AddRange(UnityEngine.Object.FindObjectsOfType<Landmark>().Select(l => l.transform).Take(3));
			watched.AddRange(UnityEngine.Object.FindObjectsOfType<PickupItem_Networked>().Where(p => p.GetComponentInParent<Landmark>() == null).Select(p => p.transform).Take(5));
			ChunkManager cm = UnityEngine.Object.FindObjectOfType<ChunkManager>();
			var points = cm != null ? cm.GetAllChunkPointsList() : null;
			Log("Watching " + watched.Count + " objects; chunk points: " + (points != null ? points.Count.ToString() : "?"));
			var start = watched.ToDictionary(t => t, t => t.position);
			Vector3 raftStart = raft.body.transform.position;
			Vector3 pointStart = points != null && points.Count > 0 ? points[0].worldPosition : Vector3.zero;
			yield return new WaitForSeconds(10f);
			Log("raft moved " + (raft.body.transform.position - raftStart));
			foreach (Transform t in watched) if (t != null) Log("  " + t.name + " moved " + (t.position - start[t]) + " now at " + t.position);
			if (points != null && points.Count > 0) Log("  first chunk point (" + points[0].rule.name + ") moved " + (points[0].worldPosition - pointStart) + " now at " + points[0].worldPosition);
		}

		[ConsoleCommand(name: "CIRaftInfo", docs: "Dev, in game: logs the raft's movement state (anchors, speeds, rigidbody, direction)")]
		public static void RaftInfo()
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null) { Fail("no raft"); return; }
			Rigidbody rb = raft.body;
			Log("Raft: pos=" + raft.transform.position + " anchors=" + raft._anchorCount + " speed=" + raft.speed + " currentMovementSpeed=" + raft.currentMovementSpeed +
				" maxSpeed=" + raft.maxSpeed + " waterDriftSpeed=" + raft.waterDriftSpeed + " moveDirection=" + raft.moveDirection + " Raft.direction=" + Raft.direction);
			if (rb != null) Log("Raft rigidbody: kinematic=" + rb.isKinematic + " velocity=" + rb.velocity + " mass=" + rb.mass + " constraints=" + rb.constraints + " layer=" + LayerMask.LayerToName(raft.gameObject.layer));
			foreach (Collider c in raft.GetComponentsInChildren<Collider>().Where(c => c.gameObject.layer == 9 || c.GetType() != typeof(BoxCollider)).Take(8))
				Log("  collider " + c.name + " " + c.GetType().Name + " layer " + LayerMask.LayerToName(c.gameObject.layer) + (c.isTrigger ? " trigger" : ""));
			var raftCols = UnityEngine.Object.FindObjectsOfType<Collider>().Where(c => c.gameObject.layer == 9).ToArray();
			Log("Colliders on layer RaftCollision in the scene: " + raftCols.Length + ", attached to the raft body: " + raftCols.Count(c => c.attachedRigidbody == rb) +
				(raftCols.Length > 0 ? " (e.g. " + Path_(raftCols[0].transform) + " " + raftCols[0].GetType().Name + (raftCols[0].enabled ? "" : " disabled") + ", rigidbody " + (raftCols[0].attachedRigidbody != null ? raftCols[0].attachedRigidbody.name : "none") + ")" : ""));
			Log("Blocks on the raft: " + UnityEngine.Object.FindObjectsOfType<Block>().Length + ", body children: " + (rb != null ? rb.transform.childCount : -1) + ", body path " + (rb != null ? Path_(rb.transform) : "-"));
			Log("Physics: RaftCollision(9) vs Obstruction(16) collide = " + !Physics.GetIgnoreLayerCollision(9, 16) + ", Default(0) vs Obstruction = " + !Physics.GetIgnoreLayerCollision(0, 16));
		}

		[ConsoleCommand(name: "CIShiftTest", docs: "Dev, in game (host): performs a world shift through Raft's WorldShiftManager and checks custom islands move with the world")]
		public static void ShiftTest()
		{
			StartTest(ShiftTestRoutine());
		}

		static IEnumerator ShiftTestRoutine()
		{
			WorldShiftManager wsm = UnityEngine.Object.FindObjectOfType<WorldShiftManager>();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			GameObject island = NearestIsland(raft.body.transform.position);
			ChunkManager cm = UnityEngine.Object.FindObjectOfType<ChunkManager>();
			if (wsm == null || island == null || cm == null) { Fail("need WorldShiftManager, a raft, a chunk manager and a spawned custom island"); yield break; }
			ChunkPoint point = cm.GetAllChunkPointsList().FirstOrDefault();
			Vector3 islandToRaft = island.transform.position - raft.body.transform.position;
			Vector3 islandToPoint = island.transform.position - point.worldPosition;
			Vector3 shift = new Vector3(100f, 0, -50f);
			wsm.ResetToCenter(shift);
			yield return new WaitForSeconds(0.5f);
			Vector3 e1 = (island.transform.position - raft.body.transform.position) - islandToRaft;
			Vector3 e2 = (island.transform.position - point.worldPosition) - islandToPoint;
			var entry = IslandWorldState.Islands.FirstOrDefault(i => i.Root == island);
			bool savedFollows = entry == null || (entry.Position - (island.transform.position + (entry.Position - island.transform.position))).sqrMagnitude < 1f;
			Log("After a world shift of " + shift + ": island vs raft drift " + e1 + ", island vs chunk point drift " + e2);
			bool ok = e1.magnitude < 2f && e2.magnitude < 0.01f; // the raft moves a little by itself during the wait
			if (ok) Log("PASS: custom island follows world shifts");
			else Fail("custom island does not follow world shifts");
		}

		[ConsoleCommand(name: "CIPushRaft", docs: "Dev, in game: pushes the raft's rigidbody towards the nearest custom island (like drifting) for up to <seconds> and logs where it stops")]
		public static void PushRaft(string[] args)
		{
			float seconds = 60f; float s;
			if (args != null && args.Length > 0 && float.TryParse(args[0], out s)) seconds = s;
			StartTest(PushRaftRoutine(seconds));
		}

		static IEnumerator PushRaftRoutine(float seconds)
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			GameObject island = NearestIsland(raft.body.position);
			if (island == null) { Fail("no custom island"); yield break; }
			yield return PushTowards(HighestPoint(island.GetComponentInChildren<Terrain>()), seconds, island.name);
		}

		[ConsoleCommand(name: "CIPushTo", docs: "Dev, in game: pushes the raft towards world point <x> <z> for up to <seconds> and logs where it stops")]
		public static void PushTo(string[] args)
		{
			float x = float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture), z = float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
			float seconds = args.Length > 2 ? float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 60f;
			StartTest(PushTowards(new Vector3(x, 0, z), seconds, "(" + x + ", " + z + ")"));
		}

		[ConsoleCommand(name: "CISail", docs: "Dev, in game (host, Normal world): sails the raft straight ahead for <seconds> (default 120) at <m/s> (default 15) and logs custom islands spawning / unloading")]
		public static void Sail(string[] args)
		{
			float seconds = args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 120f;
			float speed = args != null && args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 15f;
			RunInBackground();
			StartTest(SailRoutine(seconds, speed));
		}

		[ConsoleCommand(name: "CICloseRmlMenu", docs: "Dev: closes RML's own menu (the one F9 opens, open when Raft starts) without a key press - tests with Raft minimised (tools\\quiet.ps1)")]
		public static void CloseRmlMenuCommand()
		{
			try
			{
				Type t = HarmonyLib.AccessTools.TypeByName("RaftModLoader.MainMenu");
				UnityEngine.Object menu = t != null ? UnityEngine.Object.FindObjectOfType(t) : null;
				if (menu == null) { Log("RML menu: not found (" + (t == null ? "no RaftModLoader.MainMenu" : "no instance") + ")"); return; }
				System.Reflection.MethodInfo close = new[] { "CloseMenu", "HideMenu", "Close" }.Select(n => HarmonyLib.AccessTools.Method(t, n, new Type[0])).FirstOrDefault(m => m != null);
				if (close == null)
				{
					Log("RML menu: no close method; it has " + string.Join(", ", t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly).Select(m => m.Name + "(" + m.GetParameters().Length + ")").ToArray()));
					return;
				}
				close.Invoke(close.IsStatic ? null : menu, null);
				Log("RML menu: closed (" + t.Name + "." + close.Name + ")");
			}
			catch (Exception e) { Log("RML menu: " + e.GetType().Name + " " + e.Message); }
		}

		[ConsoleCommand(name: "CIBackground", docs: "Dev: keeps the game running while its window is not in front, its sound off (for automated tests: the user works on the PC meanwhile)")]
		public static void RunInBackground()
		{
			Application.runInBackground = true;
			Log("The game keeps running in the background now" + (SetMuted(true) ? ", its sound off" : ""));
		}

		[ConsoleCommand(name: "CIMute", docs: "Dev: the game's own sound off for this session (CIMute off: on again) - Raft's sound engine and Unity's, never Windows' volume")]
		public static void MuteCommand(string[] args)
		{
			bool off = args != null && args.Length > 0 && args[0] == "off";
			Log(SetMuted(!off) ? "Sound " + (off ? "on" : "off") : "Sound: could not be changed");
		}

		/// <summary>Tests: Raft's FMOD master bus muted (or not) and Unity's own audio with it; for this session only.</summary>
		static bool SetMuted(bool muted)
		{
			bool ok = false;
			try { FMODUnity.RuntimeManager.GetBus("bus:/").setMute(muted); ok = true; } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Muting: " + e.Message); }
			AudioListener.volume = muted ? 0f : 1f;
			return ok;
		}


		/// <summary>Sails the raft straight to a point that follows Raft's world shifts (a Func: asked again every step), at most 200 s.</summary>
		static IEnumerator SailTo(Func<Vector3> target, float speed)
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null || raft.body == null) { Fail("no raft"); yield break; }
			Rigidbody body = raft.body;
			Network_Player player = RAPI.GetLocalPlayer();
			if (player != null) player.transform.position = body.position + Vector3.up * 3f;
			for (float t = 0; t < 200f; t += Time.fixedDeltaTime)
			{
				yield return new WaitForFixedUpdate();
				KeepAlive(player);
				Vector3 to = Flat(target() - body.position);
				if (to.magnitude < 3f) break;
				body.MovePosition(body.position + to.normalized * Mathf.Min(speed * Time.fixedDeltaTime, to.magnitude));
			}
			body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; // (a raft dragged at test speed through the sea keeps no spin either)
			Log("Sailed back: the raft " + Flat(target() - body.position).magnitude.ToString("F0") + " m from where it was");
		}
		static IEnumerator SailRoutine(float seconds, float speed)
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null || raft.body == null) { Fail("no raft"); yield break; }
			Rigidbody body = raft.body;
			Vector3 dir = Flat(body.velocity).sqrMagnitude > 0.04f ? Flat(body.velocity).normalized : (Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward);
			int startCount = IslandWorldState.Islands.Count;
			Log("Sailing " + dir + " at " + speed + " m/s for " + seconds + " s; custom islands in the world: " + startCount + ", auto " + (CustomIslandSpawner.Enabled ? "on" : "off"));
			float t = 0, sailed = 0, nextLog = 10f;
			Vector3 last = body.position;
			Network_Player player = RAPI.GetLocalPlayer();
			// Start on the raft (dying pauses a single-player game, which would stall the test)
			if (player != null) player.transform.position = body.position + Vector3.up * 3f;
			while (t < seconds)
			{
				yield return new WaitForFixedUpdate();
				t += Time.fixedDeltaTime;
				KeepAlive(player);
				// Raft's own physics caps the raft's speed, so move the body directly (the player on it moves along)
				body.MovePosition(body.position + dir * speed * Time.fixedDeltaTime);
				Vector3 d = Flat(body.position - last);
				if (d.magnitude < 100f) sailed += d.magnitude; // world shifts jump the position
				last = body.position;
				if (t >= nextLog)
				{
					nextLog += 10f;
					int loaded = IslandWorldState.Islands.Count(e => e.Root != null);
					Log(string.Format("t={0:F0}s sailed {1:F0} m; custom islands {2} ({3} loaded)", t, sailed, IslandWorldState.Islands.Count, loaded));
				}
			}
			body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; // (a raft dragged at test speed through the sea keeps no spin either)
			Log("Done: sailed " + sailed.ToString("F0") + " m, custom islands " + startCount + " -> " + IslandWorldState.Islands.Count + ", loaded now " + IslandWorldState.Islands.Count(e => e.Root != null));
		}

		/// <summary>Long test runs in a Normal world would otherwise starve the test player.</summary>
		static void KeepAlive(Network_Player player)
		{
			if (player == null || player.Stats == null) return;
			PlayerStats s = player.Stats;
			s.stat_hunger.Normal.Value = s.stat_hunger.Normal.Max;
			s.stat_thirst.Normal.Value = s.stat_thirst.Normal.Max;
			s.stat_health.Value = s.stat_health.Max;
			s.stat_oxygen.Value = s.stat_oxygen.Max;
		}

		/// <summary>Sends a message through RML's own serializer (as the network would) and reads it back.</summary>
		static IslandNetMessage RoundTrip(IslandNetMessage m, out int bytes)
		{
			var writer = new Unity.Netcode.FastBufferWriter(1024, Unity.Collections.Allocator.Temp, 1 << 20);
			try
			{
				new RMessage("citest", m).SerializeFast(writer);
				bytes = writer.Length;
				var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp);
				try
				{
					reader.Seek(2); // RML's message marker, read by its network patch before DeserializeFast
					var back = new RMessage();
					back.DeserializeFast(reader);
					return back.realMsg as IslandNetMessage;
				}
				finally { reader.Dispose(); }
			}
			finally { writer.Dispose(); }
		}

		[ConsoleCommand(name: "CINetTest", docs: "Dev, in game (host): island messages survive RML's serializer, and an island file transfer (looped back locally) arrives intact")]
		public static void NetTest()
		{
			bool ok = true;
			try
			{
				// 1. The island list, as a client that joins would get it
				IslandNetMessage list = IslandNetwork.IslandsMessage(IslandWorldState.Islands, true);
				int size;
				IslandNetMessage back = RoundTrip(list, out size);
				bool same = back != null && back.Kind == list.Kind && back.FullList && back.Ids.SequenceEqual(list.Ids) && back.Names.SequenceEqual(list.Names) &&
					back.Hashes.SequenceEqual(list.Hashes) && back.Offsets.SequenceEqual(list.Offsets) && back.States.SequenceEqual(list.States) &&
					(back.Labels ?? new string[0]).SequenceEqual(list.Labels ?? new string[0]) && (back.Rules ?? new string[0]).SequenceEqual(list.Rules ?? new string[0]);
				Log((same ? "PASS" : "FAIL") + ": island list with " + list.Ids.Length + " island(s) survives RML's serializer (" + size + " bytes)" +
					(back == null ? " - came back as null" : ""));
				ok &= same;

				// 2. File transfer: host sends chunks, the client puts them back together under a hash-suffixed name
				// (citest if the editor test made it, else the tests' own sample island - not whichever island the player has saved)
				string name = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == TestIsland) ?? SaveSampleIsland();
				string hash = IslandNetwork.HashOf(name);
				var sent = new System.Collections.Generic.List<IslandNetMessage>();
				IslandNetwork.Loopback = sent.Add;
				try { IslandNetwork.SendFile(name, hash, default(Network_UserId)); }
				finally { IslandNetwork.Loopback = null; }
				string target = IslandSpawner.PathFor(IslandNetwork.DownloadName(name, hash));
				if (File.Exists(target)) File.Delete(target);
				IslandNetwork.ExpectFile(hash);
				int maxChunk = 0;
				foreach (IslandNetMessage chunk in sent)
				{
					IslandNetMessage c = RoundTrip(chunk, out size);
					maxChunk = Math.Max(maxChunk, size);
					IslandNetwork.ReceiveChunk(c);
				}
				bool arrived = File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(IslandSpawner.PathFor(name)));
				Log((arrived ? "PASS" : "FAIL") + ": island file '" + name + "' (" + new FileInfo(IslandSpawner.PathFor(name)).Length + " bytes) sent in " + sent.Count +
					" chunk(s) of up to " + maxChunk + " bytes and saved intact as " + Path.GetFileName(target));
				ok &= arrived;
				if (File.Exists(target)) File.Delete(target);

				// 2b. The host plays the island from a copy (name_hash: kept for a saved world, or downloaded as a player before a
				// host swap) while its own file is another version: the player must get it as name_hash - it was sent as
				// name_hash and saved as name_hash_hash, and the island never loaded
				const string copyTest = "citest-netcopy";
				string copyName = IslandNetwork.DownloadName(copyTest, hash);
				try
				{
					File.WriteAllBytes(IslandSpawner.PathFor(copyTest), File.ReadAllBytes(IslandSpawner.PathFor(name)).Concat(new byte[] { 1, 2, 3 }).ToArray()); // (another version)
					File.Copy(IslandSpawner.PathFor(name), IslandSpawner.PathFor(copyName), true);
					var sent2 = new System.Collections.Generic.List<IslandNetMessage>();
					IslandNetwork.Loopback = sent2.Add;
					try { IslandNetwork.SendFile(copyTest, hash, default(Network_UserId)); }
					finally { IslandNetwork.Loopback = null; }
					File.Delete(IslandSpawner.PathFor(copyName)); // (the player's side: nothing yet)
					IslandNetwork.ExpectFile(hash);
					foreach (IslandNetMessage chunk in sent2) IslandNetwork.ReceiveChunk(RoundTrip(chunk, out size));
					bool right = sent2.Count > 0 && File.Exists(IslandSpawner.PathFor(copyName)) && File.ReadAllBytes(IslandSpawner.PathFor(copyName)).SequenceEqual(File.ReadAllBytes(IslandSpawner.PathFor(name)))
						&& !File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(copyName, hash)));
					Log((right ? "PASS" : "FAIL") + ": the host playing a kept copy sends it as '" + copyTest + "' and the player saves it as " + copyName + " (" + sent2.Count + " chunk(s))");
					ok &= right;
				}
				finally
				{
					foreach (string n in new[] { copyTest, copyName, IslandNetwork.DownloadName(copyName, hash) })
						if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n));
					if (name == SampleSaved) RemoveSampleIsland();
				}

				// 3. Trees and pickups on loaded islands can be found the way Raft finds what a remote player harvested / picked up
				int found = 0, total = 0;
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(i => i.Root != null))
					foreach (PickupItem_Networked pn in e.Root.GetComponentsInChildren<PickupItem_Networked>(true))
					{
						total++;
						if (NetworkIDManager.GetNetworkIDFromObjectIndex<PickupItem_Networked>(pn.ObjectIndex) == pn) found++;
					}
				bool ids = total > 0 && found == total;
				Log((ids ? "PASS" : total == 0 ? "SKIP" : "FAIL") + ": " + found + "/" + total + " harvestables and pickups on loaded custom islands are in Raft's network registry" +
					(total == 0 ? " (no loaded island with any; CIMPIsland first)" : ""));
				ok &= ids || total == 0;

				// 4. Every other kind of message, with the fields it uses (catalogue IX6): the world's rules, story and
				// randomizer as the host builds them, and the rest as they are sent
				var kinds = new List<IslandNetMessage>
				{
					new IslandNetMessage { Kind = IslandNetMessage.Remove, Ids = new[] { 3, 4 } },
					new IslandNetMessage { Kind = IslandNetMessage.SyncRequest },
					new IslandNetMessage { Kind = IslandNetMessage.FileRequest, Name = "cimpfull", Hash = "0123abcd" },
					new IslandNetMessage { Kind = IslandNetMessage.ObjectUsed, Ids = new[] { 7 }, Index = 0x20003, Count = 12 },
					new IslandNetMessage { Kind = IslandNetMessage.QuestStep, Ids = new[] { 7 }, Index = 2, Count = 1 },
					new IslandNetMessage { Kind = IslandNetMessage.Announce, Ids = new[] { 7 }, Name = "Sandbar", Data = "A sandbar rose", Offsets = new[] { 1.5f, 0f, -300.25f } },
					new IslandNetMessage { Kind = IslandNetMessage.ObjectSet, Ids = new[] { 7 }, Index = 14, Count = 3 },
					new IslandNetMessage { Kind = IslandNetMessage.EventFired, Ids = new[] { 7 }, Index = Behaviours.IslandIndex, Name = "quest!", FullList = true },
					new IslandNetMessage { Kind = IslandNetMessage.PlayerPlace, Ids = new[] { 7 }, Offsets = new[] { 3f, 12.5f, -4f } },
					new IslandNetMessage { Kind = IslandNetMessage.Claim, Ids = new[] { 7 }, Index = TriggerZone.KeyBase + 2, Count = 1 },
					StoryBook.StateMessage(), WorldRandomizer.Message(), WorldRules.Message(),
					// (the kinds added since: the level up system, the world's options, the world's copy, the story chain)
					new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "mine", Data = "250|0,0,0,1,0,1,0,0,0|4" },
					WorldOptions.Message(),
					new IslandNetMessage { Kind = IslandNetMessage.WorldCopy, Name = Guid.NewGuid().ToString(), Hash = "0123abcd", Index = 0, Count = 1, Data = "# Custom islands in world 'CI Net'\n@plan=Random islands\n@savedat=1" },
					StoryChain.Message("A new signal\nTune the Receiver to #1234"),
					// (and the kinds after those - IX6: 20-29)
					new IslandNetMessage { Kind = IslandNetMessage.QuestCount, Data = "Story\tThe lighthouse\t1\nSide\tLost crate\t0" },
					new IslandNetMessage { Kind = IslandNetMessage.CreatureSpots, Data = "1234:7:2;1235:7:3" },
					new IslandNetMessage { Kind = IslandNetMessage.ObjectHarvest, Ids = new[] { 7, 0, -1 }, Index = 41, Count = 12 },
					new IslandNetMessage { Kind = IslandNetMessage.FileWanted, Name = "cimpfull", Hash = "0123abcd" },
					new IslandNetMessage { Kind = IslandNetMessage.WorldCopyNewer, Name = Guid.NewGuid().ToString(), Data = "5;638640000000000000" },
					new IslandNetMessage { Kind = IslandNetMessage.QuestStepMore, Ids = new[] { 7, 2 }, Index = 1, Count = 3 },
					new IslandNetMessage { Kind = IslandNetMessage.DailyQuest, Name = "add", Index = 12, Count = 4 },
					new IslandNetMessage { Kind = IslandNetMessage.RogueShark, Name = "spawn", Index = 5678, Count = 20 },
					new IslandNetMessage { Kind = IslandNetMessage.WorldSave, Name = Guid.NewGuid().ToString(), Hash = "CI Net", Index = 1, Count = 3, Data = Convert.ToBase64String(new byte[] { 0, 1, 2, 250, 251 }) },
					new IslandNetMessage { Kind = IslandNetMessage.UpgradeTable, Name = "learned", Index = 3, Data = "76561197992629925" },
				};
				var broken = new List<string>();
				foreach (IslandNetMessage m in kinds)
				{
					IslandNetMessage r = RoundTrip(m, out size);
					Func<int[], int[], bool> ints = (a, b) => (a == null) == (b == null) && (a == null || a.SequenceEqual(b));
					Func<float[], float[], bool> floats = (a, b) => (a == null) == (b == null) && (a == null || a.SequenceEqual(b));
					if (r == null || r.Kind != m.Kind || !ints(r.Ids, m.Ids) || r.Index != m.Index || r.Count != m.Count || r.FullList != m.FullList ||
						r.Name != m.Name || r.Hash != m.Hash || r.Data != m.Data || !floats(r.Offsets, m.Offsets))
						broken.Add(m.Kind.ToString());
				}
				Log((broken.Count == 0 ? "PASS" : "FAIL") + ": " + kinds.Count + " kinds of message survive RML's serializer with their fields (kinds " + string.Join(", ", kinds.Select(k => k.Kind.ToString()).Distinct().ToArray()) + ")" +
					(broken.Count > 0 ? "; broken: " + string.Join(", ", broken.ToArray()) : ""));
				ok &= broken.Count == 0;
			}
			catch (Exception e) { Fail("exception: " + e); ok = false; }
			if (ok) Log("PASS: network self test"); else Fail("network self test");
		}

		[ConsoleCommand(name: "CIStateTest", docs: "Dev, in game (host): chops a tree and picks up an item on the nearest loaded custom island, reloads the island and checks they stay used; also checks regrowing")]
		public static void StateTest()
		{
			StartTest(StateTestRoutine());
		}

		static IEnumerator StateTestRoutine()
		{
			Vector3 raftPos = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			IslandWorldState.Entry e = IslandWorldState.Islands.Where(i => i.Root != null && i.Root.GetComponentsInChildren<HarvestableTree>().Any())
				.OrderBy(i => Vector3.Distance(i.Position, raftPos)).FirstOrDefault();
			if (e == null) { Fail("no loaded custom island with trees (CIMPIsland first)"); yield break; }

			HarvestableTree tree = e.Root.GetComponentsInChildren<HarvestableTree>().First(t => !t.Depleted);
			int treeOrd = (int)(tree.GetComponent<PickupItem_Networked>().ObjectIndex & 0xFFFF);
			tree.Harvest(null); // one chop, no items
			int yieldLeft = tree.GetComponent<PickupItem>().yieldHandler.Yield.Count;
			PickupItem_Networked pickup = e.Root.GetComponentsInChildren<PickupItem_Networked>()
				.First(p => p.GetComponent<HarvestableTree>() == null && p.gameObject.activeSelf);
			int pickupOrd = (int)(pickup.ObjectIndex & 0xFFFF);
			bool removed = PickupObjectManager.RemovePickupItem(pickup);
			Log("On '" + e.HostName + "': chopped tree #" + treeOrd + " once (" + yieldLeft + " harvests left), picked up " + pickup.name + " #" + pickupOrd +
				" (removed=" + removed + ", still exists=" + (pickup != null) + ", active=" + (pickup != null && pickup.gameObject.activeSelf) + ")");

			// Unload and load again, the way the streamer does
			IslandObjectState.Capture(e);
			Log("Recorded state: " + IslandObjectState.Encode(e.State));
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return null;
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			if (e.Root == null) { Fail("island did not load again"); yield break; }

			PickupItem_Networked tree2 = e.Root.GetComponentsInChildren<PickupItem_Networked>(true).First(p => (p.ObjectIndex & 0xFFFF) == treeOrd);
			PickupItem_Networked pickup2 = e.Root.GetComponentsInChildren<PickupItem_Networked>(true).First(p => (p.ObjectIndex & 0xFFFF) == pickupOrd);
			int yieldAfter = tree2.GetComponent<PickupItem>().yieldHandler.Yield.Count;
			bool ok = yieldAfter == yieldLeft && !pickup2.gameObject.activeSelf;
			Log((ok ? "PASS" : "FAIL") + ": after reloading, the tree has " + yieldAfter + " harvests left (expected " + yieldLeft + ") and the pickup is " +
				(pickup2.gameObject.activeSelf ? "back (wrong)" : "still gone"));

			// Regrowing: on a copy of the state, pretend it all happened long ago (the real state stays for save/load checks)
			var old = IslandObjectState.Decode(IslandObjectState.Encode(e.State));
			foreach (ObjectState s in old.Values) s.Day -= 100;
			int stale = IslandObjectState.DropRegrown(old, 3);
			var fresh = IslandObjectState.Decode(IslandObjectState.Encode(e.State));
			int kept = fresh.Count - IslandObjectState.DropRegrown(fresh, 3);
			// (only trees and pickups regrow: doors, chests, zones, quests and signals of the island keep their state)
			int harvests = e.State.Keys.Count(k => k < CreatureSpawner.StateKeyBase);
			bool regrown = stale == harvests && old.Count == e.State.Count - harvests && kept == e.State.Count;
			Log((regrown ? "PASS" : "FAIL") + ": state older than 3 days is dropped, so the island regrows on its next load; today's stays (" + stale + " dropped, " + kept + " kept)");

			var round = IslandObjectState.Decode("5,0,-1,12;7,1,2,13");
			bool codec = round.Count == 2 && !round[5].Active && round[5].Yield == -1 && round[7].Yield == 2 && round[7].Day == 13 && IslandObjectState.Encode(round) == "5,0,-1,12;7,1,2,13";
			Log((codec ? "PASS" : "FAIL") + ": state text round trip");
			if (ok && regrown && codec) Log("PASS: island object state test"); else Fail("island object state test");
		}

		[ConsoleCommand(name: "CISpawnNow", docs: "Dev, in game (host): places an island from the spawn pool ahead of the raft now, with the automatic spawner's checks")]
		public static void SpawnNow()
		{
			Vector3? pos = CustomIslandSpawner.RaftPosition;
			if (!pos.HasValue) { Fail("no raft"); return; }
			int before = IslandWorldState.Islands.Count;
			string said = CustomIslandSpawner.TrySpawn(pos.Value, true);
			Log(said);
			StartTest(SpawnNowBuilt(IslandWorldState.Islands.Skip(before).ToList(), said));
		}

		/// <summary>CISpawnNow's end: a PASS line once the islands it placed are built (60 s at most), or that none came.</summary>
		static IEnumerator SpawnNowBuilt(List<IslandWorldState.Entry> added, string said)
		{
			if (added.Count == 0) { Log("PASS: spawn now - no new island (" + said + ")"); yield break; }
			float start = Time.realtimeSinceStartup, end = start + 60f;
			while (Time.realtimeSinceStartup < end && added.Any(e => e.Root == null && !e.Failed && !e.Excluded)) yield return null;
			List<IslandWorldState.Entry> bad = added.Where(e => e.Root == null && !e.Excluded).ToList(); // (excluded: not meant to load here)
			string names = string.Join(", ", added.Select(e => "'" + e.Name + "'").ToArray());
			if (bad.Count == 0) Log("PASS: spawn now - " + names + " built in " + Took(start));
			else Fail("spawn now: " + string.Join(", ", bad.Select(e => "'" + e.Name + "'" + (e.Failed ? " failed" : " not built")).ToArray()) + " after " + Took(start));
		}

		[ConsoleCommand(name: "CIRealIsland",docs: "Dev, in game (host): spawns one of Raft's own small islands <distance> m east of the raft (for comparisons)")]
		public static void RealIsland(string[] args)
		{
			float d = args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 200f;
			ComponentManager<ChunkManager>.Value.AddChunkPointCheat(ChunkPointType.Landmark_Small, new Vector3(d, 0, 0));
			StartTest(ReportLandmarks());
		}

		static IEnumerator ReportLandmarks()
		{
			yield return new WaitForSeconds(8f);
			foreach (Landmark l in UnityEngine.Object.FindObjectsOfType<Landmark>())
			{
				Terrain t = l.GetComponentInChildren<Terrain>();
				Log("Real landmark " + l.name + " at " + l.transform.position + (t != null ? ", highest point " + HighestPoint(t) : ""));
			}
		}

		static IEnumerator PushTowards(Vector3 target, float seconds, string label)
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Rigidbody body = raft.body;
			Vector3 peak = target;
			Vector3 dir = Flat(peak - body.position).normalized;
			Log("Pushing raft towards " + label + " at 4 m/s; layers RaftCollision/BakedBlocks vs Obstruction collide: " + !Physics.GetIgnoreLayerCollision(9, 16) + "/" + !Physics.GetIgnoreLayerCollision(18, 16));
			Vector3 lastLogged = body.position; float stuckTime = 0; float t = 0;
			while (t < seconds)
			{
				yield return new WaitForFixedUpdate();
				t += Time.fixedDeltaTime;
				Vector3 before = body.position;
				body.velocity = dir * 4f + Vector3.up * body.velocity.y;
				if (Mathf.Repeat(t, 2f) < Time.fixedDeltaTime)
				{
					float moved = Flat(body.position - lastLogged).magnitude;
					RaycastHit hit;
					string below = Physics.Raycast(body.position + Vector3.up * 5f, Vector3.down, out hit, 40f, 1 << IslandSpawner.TerrainLayer) ? "ground " + (body.position.y - hit.point.y).ToString("F1") + " m below" : "open water";
					Log(string.Format("t={0:F0}s raft {1} moved {2:F1} m in 2 s, {3}, {4:F0} m from peak", t, body.position, moved, below, Flat(peak - body.position).magnitude));
					stuckTime = moved < 0.5f ? stuckTime + 2f : 0f;
					lastLogged = body.position;
					if (stuckTime >= 6f) { Log("PASS: the raft is stopped by the island (" + Flat(peak - body.position).magnitude.ToString("F0") + " m from the peak)"); body.velocity = Vector3.zero; yield break; }
				}
			}
			body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; // (a raft dragged at test speed through the sea keeps no spin either)
			Fail("raft was not stopped within " + seconds + " s");
		}

		[ConsoleCommand(name: "CIHarvestProbe", docs: "Dev, in game: places a harvestable Raft palm next to the player (outside any landmark) and tries to harvest it")]
		public static void HarvestProbe()
		{
			StartTest(HarvestProbeRoutine());
		}

		static IEnumerator HarvestProbeRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			Log("Harvestable prototypes: " + string.Join(", ", PlaceableCatalog.HarvestableNames.ToArray()));
			Network_Player player = RAPI.GetLocalPlayer();
			string name = PlaceableCatalog.HarvestableNames.FirstOrDefault(n => n.Contains("Palm"));
			if (player == null || name == null) { Fail("need a player and a harvestable palm"); yield break; }
			int exceptions = 0; string first = null;
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception || type == LogType.Error) { exceptions++; if (first == null) first = msg + " | " + trace.Split('\n').FirstOrDefault(); } };
			Application.logMessageReceived += counter;
			GameObject islandForHarvest = NearestIsland(player.transform.position);
			HarvestableTree onIsland = islandForHarvest != null ? islandForHarvest.GetComponentsInChildren<HarvestableTree>().FirstOrDefault() : null;
			GameObject tree;
			if (onIsland != null) { tree = onIsland.transform.root == onIsland.transform ? onIsland.gameObject : onIsland.gameObject; Log("Using harvestable " + onIsland.name + " placed on " + islandForHarvest.name + " (" + islandForHarvest.GetComponentsInChildren<HarvestableTree>().Length + " harvestable trees, " + islandForHarvest.GetComponentsInChildren<PickupItem>().Length + " pickups on the island)"); }
			else { tree = PlaceableCatalog.SpawnHarvestable(name, null); tree.transform.position = player.transform.position + player.transform.forward * 3f; }
			yield return new WaitForSeconds(1f);
			Log("Spawned " + name + " with exceptions so far: " + exceptions + (first != null ? " (" + first + ")" : ""));
			HarvestableTree ht = tree.GetComponentInChildren<HarvestableTree>();
			PickupItem_Networked net = tree.GetComponentInChildren<PickupItem_Networked>();
			Log("HarvestableTree=" + (ht != null) + " PickupItem_Networked=" + (net != null) + " canBePickedUp=" + (net != null ? net.CanBePickedUp().ToString() : "-") + " index=" + (net != null ? net.uniqueSpawnableIndex.ToString() : "-"));
			int before = player.Inventory.allSlots.Where(sl => sl != null && sl.itemInstance != null && sl.itemInstance.baseItem != null).Sum(sl => sl.itemInstance.Amount);
			try { if (ht != null) ht.Harvest(player.Inventory); }
			catch (Exception e) { Log("Harvest threw: " + e.GetType().Name + ": " + e.Message); }
			yield return new WaitForSeconds(2f);
			int after = player.Inventory.allSlots.Where(sl => sl != null && sl.itemInstance != null && sl.itemInstance.baseItem != null).Sum(sl => sl.itemInstance.Amount);
			Application.logMessageReceived -= counter;
			Log("Inventory items " + before + " -> " + after + ", tree active=" + (tree != null && tree.activeInHierarchy) + ", errors/exceptions: " + exceptions + (first != null ? " (first: " + first + ")" : ""));
		}

		[ConsoleCommand(name: "CISave", docs: "Dev, in game (host): saves the world now")]
		public static void SaveNow()
		{
			SaveAndLoad sl = UnityEngine.Object.FindObjectOfType<SaveAndLoad>();
			if (sl == null) { Fail("SaveAndLoad not found"); return; }
			// (at the main menu Raft's save made an empty save folder - and an empty "world" - before it threw; the Load box
			// then picked the empty folder as the newest save and kept Load disabled)
			if (!LoadSceneManager.IsGameSceneLoaded || RAPI.GetLocalPlayer() == null) { Fail("not in a world: nothing saved"); return; }
			sl.SaveGame(false);
			Log("World save requested (" + SaveAndLoad.CurrentGameFileName + ", " + SaveAndLoad.WorldGuid + ")");
		}

		[ConsoleCommand(name: "CIScenes", docs: "Dev: lists all scenes in Raft's build")]
		public static void ListScenes()
		{
			var lines = new System.Collections.Generic.List<string>();
			for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
				lines.Add(i + ": " + UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "scan_scenes.txt"));
			File.WriteAllLines(file, lines.ToArray());
			Log(lines.Count + " scenes, written to " + file);
		}

		[ConsoleCommand(name: "CIScan", docs: "Dev: loads a Raft scene additively and writes its renderable objects and terrain setup to Mods\\DynamicIslands\\scan_<scene>.txt")]
		public static void ScanScene(string[] args)
		{
			if (args == null || args.Length == 0) { Log("Usage: CIScan <scene name>"); return; }
			StartTest(Scan(string.Join(" ", args)));
		}

		static IEnumerator Scan(string sceneName)
		{
			var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
			bool loadedByUs = !scene.isLoaded;
			if (loadedByUs)
			{
				AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
				if (op == null) { Fail("cannot load scene " + sceneName); yield break; }
				while (!op.isDone) yield return null;
				scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
			}

			var lines = new System.Collections.Generic.List<string>();
			var counts = new System.Collections.Generic.Dictionary<string, int>();
			var examplePath = new System.Collections.Generic.Dictionary<string, string>();
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
				{
					TerrainData td = t.terrainData;
					lines.Add("TERRAIN " + Path_(t.transform) + " size=" + td.size + " hres=" + td.heightmapResolution + " ares=" + td.alphamapResolution +
						" material=" + (t.materialTemplate != null ? t.materialTemplate.name + " / " + t.materialTemplate.shader.name : "(none)"));
					if (td.terrainLayers != null)
						foreach (TerrainLayer l in td.terrainLayers)
							if (l != null) lines.Add("  LAYER " + l.name + " diffuse=" + (l.diffuseTexture != null ? l.diffuseTexture.name + " " + l.diffuseTexture.width + "x" + l.diffuseTexture.height + " readable=" + l.diffuseTexture.isReadable : "none") +
								" normal=" + (l.normalMapTexture != null ? l.normalMapTexture.name : "none") + " tile=" + l.tileSize);
					if (t.materialTemplate != null)
						foreach (string p in t.materialTemplate.GetTexturePropertyNames())
						{
							Texture tex = t.materialTemplate.GetTexture(p);
							if (tex != null) lines.Add("  MATTEX " + p + " = " + tex.name + " (" + tex.GetType().Name + " " + tex.width + "x" + tex.height + ")");
						}
					lines.Add("  TREES prototypes=" + td.treePrototypes.Length + " instances=" + td.treeInstanceCount + " details=" + td.detailPrototypes.Length);
					foreach (TreePrototype tp in td.treePrototypes) if (tp.prefab != null) lines.Add("  TREEPROTO " + tp.prefab.name);
				}

				// Top-most renderable nodes, as the placeable catalog would pick them
				var stack = new System.Collections.Generic.Stack<Transform>();
				stack.Push(root.transform);
				while (stack.Count > 0)
				{
					Transform t = stack.Pop();
					if (t != root.transform && (t.GetComponent<Renderer>() != null || t.GetComponent<LODGroup>() != null))
					{
						string n = PlaceableCatalog.CleanName(t.name);
						int c; counts.TryGetValue(n, out c); counts[n] = c + 1;
						if (!examplePath.ContainsKey(n)) examplePath[n] = Path_(t);
						continue;
					}
					foreach (Transform child in t) stack.Push(child);
				}
			}
			lines.Add("OBJECTS (name, count, example path):");
			foreach (var kv in counts.OrderByDescending(k => k.Value)) lines.Add("  " + kv.Value + "x " + kv.Key + "    " + examplePath[kv.Key]);

			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "scan_" + sceneName.Replace("#", "_") + ".txt"));
			File.WriteAllLines(file, lines.ToArray());
			Log("Scanned " + sceneName + ": " + counts.Count + " distinct objects, written to " + file);

			if (loadedByUs)
			{
				AsyncOperation unload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
				if (unload != null) while (!unload.isDone) yield return null;
			}
		}

		static string Path_(Transform t)
		{
			string p = t.name;
			while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
			return p;
		}

		/// <summary>World position of the terrain's highest heightmap sample.</summary>
		static Vector3 HighestPoint(Terrain terrain)
		{
			TerrainData d = terrain.terrainData;
			int res = d.heightmapResolution;
			float[,] h = d.GetHeights(0, 0, res, res);
			int bx = 0, bz = 0;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					if (h[z, x] > h[bz, bx]) { bx = x; bz = z; }
			return terrain.transform.position + new Vector3(bx / (float)(res - 1) * d.size.x, h[bz, bx] * d.size.y, bz / (float)(res - 1) * d.size.z);
		}

		static IEnumerator EditorTest()
		{
			Log("Editor test started");
			if (!DynamicIslands.InEditor())
			{
				Log("Opening editor");
				DynamicIslands.LoadEditor(new string[0]);
			}

			float timeout = Time.realtimeSinceStartup + 90f;
			while (!(DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt && DynamicIslands.EditorGizmoHandler != null))
			{
				if (Time.realtimeSinceStartup > timeout)
				{
					Fail("editor/catalog not ready after 90s (InEditor=" + DynamicIslands.InEditor() + ", catalog=" + PlaceableCatalog.IsBuilt + ")");
					yield break;
				}
				yield return new WaitForSeconds(0.5f);
			}
			Log("Editor ready. Catalog has " + PlaceableCatalog.Names.Count() + " objects: " + string.Join(", ", PlaceableCatalog.Names.Take(15).ToArray()) + (PlaceableCatalog.Names.Count() > 15 ? ", ..." : ""));

			Terrain terrain = terraineditor.terrain;
			TerrainData data = terrain.terrainData;
			Log("Terrain size " + data.size + ", resolution " + data.heightmapResolution);

			// 1. Sculpt a round hill in the middle that rises 15 m above the water level
			int res = data.heightmapResolution;
			float[,] heights = data.GetHeights(0, 0, res, res);
			float peak = (DynamicIslands.EditorWaterLevel + 15f) / data.size.y;
			int cx = res / 2, cy = res / 2, radius = res / 8;
			for (int y = 0; y < res; y++)
				for (int x = 0; x < res; x++)
				{
					float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
					if (d < 1f) heights[y, x] = Mathf.Max(heights[y, x], peak * Mathf.SmoothStep(1f, 0f, d));
				}
			data.SetHeights(0, 0, heights);
			terraineditor.paintMask = null;
			TerrainPainter.Setup(terrain, DynamicIslands.EditorWaterLevel);
			int ares = data.alphamapResolution;
			terraineditor.paintMask = new float[ares, ares];
			yield return null;

			// 1b. Hand-paint a Rock patch on the grassy hilltop, then check automatic re-texturing leaves it alone
			int bx = ares / 2 + PaintOffset, bz = ares / 2 + PaintOffset;
			var rockBlock = new float[PaintSize, PaintSize, TerrainPainter.LayerCount];
			for (int z = 0; z < PaintSize; z++)
				for (int x = 0; x < PaintSize; x++) { rockBlock[z, x, TerrainPainter.Rock] = 1f; terraineditor.paintMask[bz + z, bx + x] = 1f; }
			data.SetAlphamaps(bx, bz, rockBlock);
			TerrainPainter.PaintWorldArea(terrain, DynamicIslands.EditorWaterLevel, terrain.transform.position, terrain.transform.position + data.size, terraineditor.paintMask);
			float rockAfterAuto = data.GetAlphamaps(bx + PaintSize / 2, bz + PaintSize / 2, 1, 1)[0, 0, TerrainPainter.Rock];
			float grassBeside = data.GetAlphamaps(bx - 6, bz - 6, 1, 1)[0, 0, TerrainPainter.Grass];
			bool maskRespected = rockAfterAuto > 0.98f;
			Log("Paint: rock patch after auto re-texturing = " + rockAfterAuto.ToString("F2") + " (" + (maskRespected ? "kept" : "OVERWRITTEN") + "), grass next to it = " + grassBeside.ToString("F2"));

			// 2. Place objects on top of the hill
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;

			string[] names = PlaceableCatalog.Names.Take(ObjectsToPlace).ToArray();
			if (names.Length == 0) { Fail("catalog is empty"); yield break; }
			Vector3 centre = terrain.transform.position + new Vector3(data.size.x / 2f, 0, data.size.z / 2f);
			for (int i = 0; i < names.Length; i++)
			{
				Vector3 pos = centre + new Vector3((i - 1) * 8f, 0, 0);
				pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
				GameObject go = PlaceableCatalog.Spawn(names[i], placed);
				go.transform.position = pos;
				go.transform.rotation = Quaternion.Euler(0, i * 45f, 0);
				go.AddComponent<EditorGameObject>().GameObjectName = names[i];
			}
			Log("Placed " + names.Length + " objects: " + string.Join(", ", names));

			// Point the camera at the hill for a screenshot
			try
			{
				Transform cam = Camera.main.transform;
				cam.position = centre + new Vector3(0, DynamicIslands.EditorWaterLevel + 40f, -70f);
				cam.LookAt(centre + new Vector3(0, DynamicIslands.EditorWaterLevel, 0));
			}
			catch (Exception e) { Log("Could not move camera: " + e.Message); }
			yield return new WaitForSeconds(1f);

			float[,] savedHeights = data.GetHeights(0, 0, res, res);
			IslandFile expected = IslandFile.Capture(TestIsland, terrain, placed);

			// 3. Save
			string path = IslandSpawner.PathFor(TestIsland);
			if (File.Exists(path)) File.Delete(path);
			DynamicIslands.SaveIsland(TestIsland);
			if (!File.Exists(path)) { Fail("save did not create " + path); yield break; }
			Log("Saved " + Path.GetFullPath(path) + " (" + new FileInfo(path).Length + " bytes)");

			// 4. Wipe: flatten terrain, reset texturing to automatic, remove objects
			data.SetHeights(0, 0, new float[res, res]);
			terraineditor.paintMask = null;
			TerrainPainter.Setup(terrain, DynamicIslands.EditorWaterLevel);
			terraineditor.paintMask = new float[ares, ares];
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			Log("Wiped terrain and objects (" + placed.GetComponentsInChildren<EditorGameObject>().Length + " objects left)");

			// 5. Load and compare
			DynamicIslands.LoadIsland(TestIsland);
			yield return null;
			yield return null;

			float[,] loaded = data.GetHeights(0, 0, res, res);
			float maxDiff = 0;
			for (int y = 0; y < res; y++)
				for (int x = 0; x < res; x++)
					maxDiff = Mathf.Max(maxDiff, Mathf.Abs(loaded[y, x] - savedHeights[y, x]));
			bool heightsOk = maxDiff < 0.0005f;
			Log("Heights: max difference after load = " + maxDiff.ToString("F6") + " (" + (heightsOk ? "ok" : "TOO LARGE") + ")");

			IslandFile actual = IslandFile.Capture(TestIsland, terrain, placed);
			bool countOk = actual.Objects.Count == expected.Objects.Count;
			float maxPosDiff = 0;
			if (countOk)
			{
				foreach (IslandObject e in expected.Objects)
				{
					IslandObject match = actual.Objects.Where(a => a.Name == e.Name).OrderBy(a => (a.Position - e.Position).sqrMagnitude).FirstOrDefault();
					maxPosDiff = Mathf.Max(maxPosDiff, match == null ? float.MaxValue : (match.Position - e.Position).magnitude);
				}
			}
			bool objectsOk = countOk && maxPosDiff < 0.01f;
			Log("Objects: " + actual.Objects.Count + "/" + expected.Objects.Count + " restored, max position error " + maxPosDiff.ToString("F4") + " m (" + (objectsOk ? "ok" : "WRONG") + ")");

			float rockLoaded = data.GetAlphamaps(bx + PaintSize / 2, bz + PaintSize / 2, 1, 1)[0, 0, TerrainPainter.Rock];
			bool maskLoaded = terraineditor.paintMask != null && terraineditor.paintMask[bz + 1, bx + 1] > 0.5f && terraineditor.paintMask[bz - 6, bx - 6] < 0.5f;
			bool paintOk = maskRespected && rockLoaded > 0.98f && maskLoaded;
			Log("Paint after load: rock = " + rockLoaded.ToString("F2") + ", paint mask restored = " + maskLoaded + " (" + (paintOk ? "ok" : "WRONG") + ")");

			if (heightsOk && objectsOk && paintOk) Log("PASS: editor save/load round trip");
			else Fail("editor save/load round trip");
		}

		static IEnumerator WorldTest()
		{
			Log("World test started");
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("not in a game world"); yield break; }
			if (!File.Exists(IslandSpawner.PathFor(TestIsland))) { Fail("run CITest in the editor first (no " + TestIsland + IslandFile.Extension + ")"); yield break; }

			// (the island this spawn adds, not one of that name already in the world: by its entry, not the object's name - CB13)
			var before = new HashSet<int>(IslandWorldState.Islands.Select(e => e.Id));
			DynamicIslands.SpawnIslandCommand(new[] { TestIsland });

			GameObject root = null;
			float timeout = Time.realtimeSinceStartup + 90f;
			while (root == null && Time.realtimeSinceStartup < timeout)
			{
				yield return new WaitForSeconds(0.5f);
				IslandWorldState.Entry added = IslandWorldState.Islands.FirstOrDefault(e => !before.Contains(e.Id) && e.Name == TestIsland);
				root = added != null && !added.Loading ? added.Root : null;
			}
			if (root == null) { Fail("island root did not appear within 90s"); yield break; }

			Terrain terrain = root.GetComponentInChildren<Terrain>();
			int objects = root.transform.Find("Objects") != null ? root.transform.Find("Objects").childCount : 0;
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Vector3 top = HighestPoint(terrain);
			float hillTop = top.y;

			Log("Island root at " + root.transform.position + ", terrain " + terrain.terrainData.size + " (" + terrain.terrainData.heightmapResolution + " samples) at " + terrain.transform.position +
				", highest point " + top + ", raft at " + (raft != null ? raft.transform.position.ToString() : "?"));
			Log("Terrain layer " + terrain.gameObject.layer + ", hill top at world Y " + hillTop.ToString("F1") + " (expected about 15 above sea level)");
			Log("Objects spawned: " + objects);

			// The hand-painted rock patch must be at the same place on the (cropped) spawned terrain
			IslandFile file = IslandFile.Load(IslandSpawner.PathFor(TestIsland));
			float u = (file.AlphamapResolution / 2 + PaintOffset + PaintSize / 2 + 0.5f) / file.AlphamapResolution;
			Vector3 patchWorld = root.transform.position + new Vector3(u * file.TerrainSize.x, 0, u * file.TerrainSize.z);
			TerrainData td = terrain.terrainData;
			Vector3 local = patchWorld - terrain.transform.position;
			int px = Mathf.FloorToInt(local.x / td.size.x * td.alphamapResolution), pz = Mathf.FloorToInt(local.z / td.size.z * td.alphamapResolution);
			float rock = (px >= 0 && pz >= 0 && px < td.alphamapResolution && pz < td.alphamapResolution) ? td.GetAlphamaps(px, pz, 1, 1)[0, 0, TerrainPainter.Rock] : -1f;
			Log("Spawned terrain alphamap " + td.alphamapResolution + " px; rock at the hand-painted spot = " + rock.ToString("F2"));

			bool ok = terrain != null && objects > 0 && hillTop > 5f && hillTop < 30f && rock > 0.9f;
			if (ok) Log("PASS: island spawned in world");
			else Fail("island spawned but looks wrong (objects=" + objects + ", hillTop=" + hillTop + ")");
		}

		#region UI, catalog, screenshots and the command file (dev)

		/// <summary>
		/// Dev builds only: every second, runs the console commands in Mods\DynamicIslands\dev_commands.txt (one per
		/// line; the file is deleted when read), so tests can be driven without typing into Raft's console, even while
		/// Raft is in the background. Called by the mod at startup through reflection (release builds have no DevTests).
		/// </summary>
		public static void Init()
		{
			Log("Command file: " + CommandFile + (Sandboxed ? " (second player: running in Sandboxie)" : ""));
			// A Raft that isn't in front pauses, and then never reads the command file (not even a CIBackground):
			// dev builds always keep running, so a Raft started behind other windows can still be driven.
			Application.runInBackground = true;
			DynamicIslands.instance.StartCoroutine(PollCommandFile());
		}

		[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
		static extern IntPtr GetModuleHandle(string moduleName);

		/// <summary>This Raft runs in a Sandboxie box (the second player of a two-player test on one PC).</summary>
		public static bool Sandboxed
		{
			get { try { return GetModuleHandle("SbieDll.dll") != IntPtr.Zero; } catch { return false; } }
		}

		/// <summary>
		/// dev_commands.txt for the normal Raft, dev_commands_2.txt for a second Raft in Sandboxie (its box must open
		/// that one file to the real folder: OpenFilePath, see TEST_PROTOCOL.md §5), so tests can drive each player.
		/// </summary>
		static string CommandFile { get { return Path.Combine(DynamicIslands.assetpath, Sandboxed ? "dev_commands_2.txt" : "dev_commands.txt"); } }

		static IEnumerator PollCommandFile()
		{
			while (true)
			{
				// (every 0.2 s - a second before, 10-20: each ci.ps1 step waited half a second on average for it)
				yield return new WaitForSecondsRealtime(0.2f);
				string[] lines = null;
				try
				{
					// (a file written this instant may not be complete yet: the next look reads it)
					if (File.Exists(CommandFile) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(CommandFile)).TotalSeconds > 0.1)
					{
						lines = File.ReadAllLines(CommandFile);
						File.Delete(CommandFile);
					}
				}
				catch (Exception e) { Debug.LogWarning("[CITEST] command file: " + e.Message); }
				if (lines == null) continue;
				Application.runInBackground = true; // commands arrive while Raft is in the background
				// One line after another: a line waits until the tests the earlier lines started have ended (two tests
				// that both generate or load islands at once spoil each other - CIGenRaftFeatures next to CIGenParity, 10-16)
				List<string> todo = lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
				// (only the tests this file's lines start: a test left running from before - one that never ended - held
				// every later file's second line for good, RUN1 10-20; CIPollerWait)
				int firstTest = testSerial + 1;
				for (int li = 0; li < todo.Count; li++)
				{
					string line = todo[li];
					pendingCommands = todo.Count - li; // (this line too, until it runs)
					if (li > 0) while (runningIds.Any(i => i >= firstTest)) yield return null;
					// "@<unix ms> <command>": run at that moment of the PC's clock - two Rafts on one PC then act in the same
					// frame or the next, not up to a second apart as the file poll and the script's jobs leave them (IM28)
					long at;
					int sp = line.IndexOf(' ');
					if (line.StartsWith("@") && sp > 1 && long.TryParse(line.Substring(1, sp - 1), out at))
					{
						line = line.Substring(sp + 1).Trim();
						DateTime when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(at);
						if ((when - DateTime.UtcNow).TotalSeconds > 30) { Fail("'@" + at + "' is more than 30 s ahead"); continue; }
						while (DateTime.UtcNow < when) yield return null;
					}
					Log("> " + line);
					pendingCommands = todo.Count - li - 1;
					try { RunCommand(line); }
					catch (Exception e) { Fail("command '" + line + "': " + (e.InnerException ?? e)); }
				}
				pendingCommands = 0;
				if (idleOwed && runningTests == 0) Log("IDLE (all tests done)");
				idleOwed = false;
			}
		}

		[ConsoleCommand(name: "CIPollerWait", docs: "Dev: a command file's second line doesn't wait for a test left running from before the file (it held every later file for good after a never-ending test, RUN1 10-20): this test keeps running while it writes a two-line command file of CIPollerMark lines; both must run within 5 s")]
		public static void PollerWaitCommand() { StartTest(PollerWaitRoutine()); }

		static int pollerMarks;
		[ConsoleCommand(name: "CIPollerMark", docs: "Dev, helper of CIPollerWait: counts one mark")]
		public static void PollerMarkCommand() { pollerMarks++; Log("poller mark " + pollerMarks); }

		static IEnumerator PollerWaitRoutine()
		{
			pollerMarks = 0;
			// (the poller reads a file only once it is 0.1 s old, and this test's own file is gone by now)
			yield return new WaitForSecondsRealtime(0.3f);
			File.WriteAllLines(CommandFile, new[] { "CIPollerMark", "CIPollerMark" });
			float end = Time.realtimeSinceStartup + 5f;
			while (pollerMarks < 2 && Time.realtimeSinceStartup < end) yield return null;
			if (pollerMarks == 2) Log("PASS: poller wait (both lines of a new file ran while an older test was still running)");
			else Fail("poller wait: " + pollerMarks + " of 2 lines ran in 5 s while an older test was still running");
		}

		/// <summary>Runs one of this mod's console commands by name (they are static methods with a ConsoleCommand attribute).</summary>
		static void RunCommand(string line)
		{
			HelpLinks.Automated = true; // (a Raft driven by the command file opens no browser or Explorer windows)
			string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (Type type in typeof(DevTests).Assembly.GetTypes())
				foreach (System.Reflection.MethodInfo m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
					foreach (System.Reflection.CustomAttributeData a in m.GetCustomAttributesData())
					{
						if (a.AttributeType.Name != "ConsoleCommand") continue;
						string name = a.ConstructorArguments.Count > 0 ? a.ConstructorArguments[0].Value as string : null;
						if (name == null) foreach (var na in a.NamedArguments) if (na.MemberName == "name") name = na.TypedValue.Value as string;
						if (!string.Equals(name, parts[0], StringComparison.OrdinalIgnoreCase)) continue;
						object[] args = m.GetParameters().Length == 0 ? new object[0] : new object[] { parts.Skip(1).ToArray() };
						m.Invoke(null, args);
						return;
					}
			Fail("no console command called " + parts[0]);
		}

		[ConsoleCommand(name: "CIShot", docs: "Dev: saves a screenshot of the game (with the editor's panels) to Mods\\DynamicIslands\\shot_<name>.png")]
		public static void Screenshot(string[] args)
		{
			string name = args != null && args.Length > 0 ? args[0] : DateTime.Now.ToString("HHmmss");
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "shot_" + name + ".png"));
			ScreenCapture.CaptureScreenshot(file);
			Log("Screenshot: " + file + " (" + Screen.width + "x" + Screen.height + (gfxLow ? ", low graphics: CIGfx high first for a full-size picture" : "") + ")");
		}

		[ConsoleCommand(name: "CIQuit", docs: "Dev: closes Raft cleanly (for automated test runs)")]
		public static void Quit()
		{
			Log("Quitting Raft");
			Application.Quit();
		}

		// A stand-in for a Raft that never reads its CIQuit (10-20 RUN2: a paged-out box Raft; the runner then started a
		// second Raft beside it): quitting - CIQuit or its window closed - is refused until the hold ends. For the
		// runner's restart guard (alltests.ps1 quitguard phase: EndSingleRaft must close its window, one Raft only)
		static float quitHoldEnd;
		static bool quitHoldHooked;

		[ConsoleCommand(name: "CIQuitHold", docs: "Dev test: Raft refuses to quit (CIQuit, its window closed) for <seconds> (default 60)")]
		public static void QuitHoldCommand(string[] args)
		{
			int s;
			if (args.Length == 0 || !int.TryParse(args[0], out s)) s = 60;
			quitHoldEnd = Time.realtimeSinceStartup + s;
			if (!quitHoldHooked) { Application.wantsToQuit += QuitHeld; quitHoldHooked = true; }
			Log("Quit held for " + s + " s");
		}

		static bool QuitHeld()
		{
			float left = quitHoldEnd - Time.realtimeSinceStartup;
			if (left <= 0f) return true;
			Log("quit held (" + Mathf.CeilToInt(left) + " s left)");
			return false;
		}

		[ConsoleCommand(name: "CIEditor", docs: "Dev: opens the editor (from the main menu) and waits until it is ready")]
		public static void OpenEditor()
		{
			StartTest(WaitForEditor(true));
		}

		static IEnumerator WaitForEditor(bool report)
		{
			if (!DynamicIslands.InEditor()) DynamicIslands.LoadEditor(new string[0]);
			float timeout = Time.realtimeSinceStartup + 120f;
			while (!(DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt && DynamicIslands.EditorGizmoHandler != null && EditorUI.Canvas != null))
			{
				if (Time.realtimeSinceStartup > timeout) { Fail("editor not ready after 120 s"); yield break; }
				yield return new WaitForSecondsRealtime(0.5f);
			}
			// (unsaved work from an earlier run is offered in a window over the editor a second later: the tests click
			// "Not now" - the autosaves stay - or every mouse test after it would hit the window)
			if (EditorAutosave.Waiting().Count > 0)
			{
				for (float until = Time.realtimeSinceStartup + 5f; !ChoiceWindow.IsOpen && Time.realtimeSinceStartup < until; ) yield return new WaitForSecondsRealtime(0.25f);
				if (ChoiceWindow.IsOpen) { ChoiceWindow.Close(); Log("The offer of unsaved work closed (tests): " + string.Join(", ", EditorAutosave.Waiting().ToArray())); }
			}
			if (report) Log("Editor ready: " + PlaceableCatalog.Names.Count() + " objects loaded");
		}

		[ConsoleCommand(name: "CITab", docs: "Dev, editor: switches the editor tab: CITab terrain|objects|island")]
		public static void SwitchTab(string[] args)
		{
			string t = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			EditorUI.SetTab(t.StartsWith("o") ? TAB.ObjectPlace : t.StartsWith("i") ? TAB.Island : TAB.TerrainEdit);
		}

		[ConsoleCommand(name: "CILoadWorld", docs: "Dev, main menu: loads a saved world through Raft's own Load Game box (no clicking). CILoadWorld [part of the world's name] [backup:N] (default: the newest world; backup:1 = its save before the newest, as Raft's Load Game box offers them)")]
		public static void LoadWorld(string[] args)
		{
			PlayerTrust.ForTests = true; // (CB2: the CI tests set players' records and use things from afar - CITrust turns it off)
			int backup = 0;
			var words = (args ?? new string[0]).ToList();
			string last = words.LastOrDefault();
			if (last != null && last.StartsWith("backup:") && int.TryParse(last.Substring(7), out backup)) words.RemoveAt(words.Count - 1);
			StartTest(LoadWorldRoutine(words.Count > 0 ? string.Join(" ", words.ToArray()) : null, backup));
		}

		static IEnumerator LoadWorldRoutine(string name, int backup = 0)
		{
			LoadGameBox box = Resources.FindObjectsOfTypeAll<LoadGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("no Load Game box (go to the main menu first)"); yield break; }
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			// Raft fills the list asynchronously: wait until it stops growing
			// (with many saved worlds Raft takes a while - 77 worlds took over 20 s - so up to 90 s)
			// (Raft reads every saved world's newest save before it lists any: with 200 worlds that took over 90 s - never
			// open it again meanwhile, that starts the reading over; old test worlds are best moved away)
			float timeout = Time.realtimeSinceStartup + 180f, said = Time.realtimeSinceStartup + 30f;
			int count = -1;
			while (Time.realtimeSinceStartup < timeout)
			{
				yield return new WaitForSecondsRealtime(1f);
				int now = box.loadGameSelections != null ? box.loadGameSelections.Count : 0;
				if (now > 0 && now == count) break;
				count = now;
				if (now == 0 && Time.realtimeSinceStartup > said) { Log("  (Raft is still reading the saved worlds for its Load list)"); said = Time.realtimeSinceStartup + 30f; }
			}
			if (box.loadGameSelections == null || box.loadGameSelections.Count == 0) { Fail("no saved worlds listed"); yield break; }
			System.Func<LoadGame_Selection, string> worldName = s => s.text_GameName != null && !string.IsNullOrEmpty(s.text_GameName.text) ? s.text_GameName.text : s.directoryInfo != null ? s.directoryInfo.Name : "?";
			LoadGame_Selection pick = name == null ? box.loadGameSelections[0]
				: box.loadGameSelections.FirstOrDefault(s => worldName(s).Equals(name, StringComparison.OrdinalIgnoreCase))
				?? box.loadGameSelections.FirstOrDefault(s => worldName(s).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
			if (pick == null) { Fail("no saved world called '" + name + "'; there are: " + string.Join(", ", box.loadGameSelections.Select(worldName).ToArray())); yield break; }
			if (backup > 0)
			{
				// (an older save of the world, as Raft's Load Game box offers them: World\<name>\<date>, newest first)
				var older = (pick.directoryInfo != null ? pick.directoryInfo.GetDirectories() : new DirectoryInfo[0])
					.Where(d => !d.Name.EndsWith("-Latest") && d.GetFiles("*.rgd").Length > 0)
					.Select(d => { DateTime t; return new KeyValuePair<DirectoryInfo, DateTime>(d, SaveAndLoad.ConvertBackupFolderNameToDateTime(d.Name, out t) ? t : DateTime.MinValue); })
					.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
				if (older.Count < backup) { Fail("the world '" + worldName(pick) + "' has " + older.Count + " older save(s), not " + backup); yield break; }
				RGD_Game game = SaveAndLoad.Load<RGD_Game>(older[backup - 1].GetFiles("*.rgd")[0].FullName);
				if (game == null) { Fail("could not read the older save " + older[backup - 1].Name); yield break; }
				pick.rgdGame = game;
				Log("Loading an older save: " + older[backup - 1].Name);
			}
			Log("Loading world: " + worldName(pick));
			// (Button_Select only scrolls the list for gamepads; Button_SelectLoad picks the world to load)
			box.Button_SelectLoad(pick);
			yield return null;
			if (box.selectedGame != pick || SaveAndLoad.WorldToLoad != pick.rgdGame) { Fail("could not pick the world '" + worldName(pick) + "': nothing loaded"); yield break; }
			// (the Load button may come on a moment after the pick - right after leaving the editor it was still off)
			for (float until = Time.realtimeSinceStartup + 10f; box.loadButton != null && !box.loadButton.interactable && Time.realtimeSinceStartup < until; ) yield return new WaitForSecondsRealtime(0.5f);
			if (box.loadButton != null && !box.loadButton.interactable) { Fail("Load is disabled (Steam offline?)"); yield break; }
			box.Button_LoadGame();
			yield return WaitForWorld();
		}

		/// <summary>
		/// Until the started world is in (with its player), so the command's "[CITEST] IDLE" (ci.ps1 ends a run there) means
		/// the next test can run.
		/// </summary>
		static IEnumerator WaitForWorld()
		{
			float timeout = Time.realtimeSinceStartup + 180f;
			System.Func<bool> isIn = () => { try { return LoadSceneManager.IsGameSceneLoaded && RAPI.GetLocalPlayer() != null; } catch { return false; } };
			float start = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup < timeout && !isIn()) yield return null;
			if (!isIn()) { Fail("the world was not in after 180 s"); yield break; }
			// Islands near the raft come in the first seconds: ready once the spawner has streamed them in this world (on
			// this world's list) and none it started is still loading - instead of a fixed 3 s (10-20); 10 s at most
			float inAt = Time.realtimeSinceStartup, end = inAt + 10f;
			System.Func<bool> near = () => CustomIslandSpawner.StreamTicks > 0 && !IslandWorldState.Islands.Any(e => e.Loading);
			while (Time.realtimeSinceStartup < end && !near()) yield return null;
			yield return new WaitForFixedUpdate();
			Log("World ready (in after " + Took(start) + ", islands near the raft " + (near() ? "in" : "still loading") + " after " + Took(inAt) + " more)");
		}

		[ConsoleCommand(name: "CIClick", docs: "Dev, editor: presses an editor button by its label, e.g. CIClick Main menu / CIClick New / CIClick Duplicate")]
		public static void Click(string[] args)
		{
			string label = args != null ? string.Join(" ", args) : "";
			if (EditorUI.Canvas == null) { Fail("the editor is not open"); return; }
			UnityEngine.UI.Button b = EditorUI.Canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false)
				.FirstOrDefault(x => x.name.Equals("Button_" + label, StringComparison.OrdinalIgnoreCase));
			if (b == null) { Fail("no visible button '" + label + "'"); return; }
			Log("Click " + label);
			b.onClick.Invoke();
		}

		[ConsoleCommand(name: "CINewWorld", docs: "Dev, main menu: creates and starts a new world through Raft's own New Game box (default game mode). CINewWorld <name>")]
		public static void NewWorld(string[] args)
		{
			PlayerTrust.ForTests = true; // (CB2: the CI tests set players' records and use things from afar - CITrust turns it off)
			string name = args != null && args.Length > 0 ? string.Join(" ", args) : "CI Test";
			StartTest(NewWorldOpen(name));
		}

		/// <summary>The main menu's New Game box, or null (in a world, or the menu still loading).</summary>
		static NewGameBox MenuNewGameBox()
		{
			if (LoadSceneManager.IsGameSceneLoaded) return null;
			return Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded);
		}

		/// <summary>Waits for the main menu (a CINewWorld straight after CIMainMenu: up to 30 s - it failed at once before
		/// and the scripts slept "to be safe", 10-20), then opens the New Game box.</summary>
		static IEnumerator NewWorldOpen(string name)
		{
			float end = Time.realtimeSinceStartup + 30f;
			NewGameBox box;
			while ((box = MenuNewGameBox()) == null && Time.realtimeSinceStartup < end) yield return new WaitForSecondsRealtime(0.1f);
			if (box == null) { Fail("no New Game box (go to the main menu first)"); yield break; }
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			yield return NewWorldCreate(box, name);
		}

		/// <summary>
		/// A moment after the box opens (as a player's click comes): straight after leaving a world Raft's game mode tabs
		/// aren't set up in the same frame, and Create threw inside Raft (no world, IsInNewGame never set).
		/// </summary>
		static IEnumerator NewWorldCreate(NewGameBox box, string name)
		{
			// (ready: its tab group has a tab selected - the game mode tabs set up; 2 frames at least, 1 s at most as before)
			TabGroup tabs = HarmonyLib.Traverse.Create(box).Field("tabGroup").GetValue<TabGroup>();
			yield return null; yield return null;
			for (float end = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < end; )
			{
				if (tabs != null && HarmonyLib.Traverse.Create(tabs).Field("selectedTabButton").GetValue() != null) break;
				yield return null;
			}
			yield return null;
			box.inputfield_GameName.text = name;
			box.GameNameEndEdit(name);
			for (float t = 0; t < 5f && box.createGameButton != null && !box.createGameButton.interactable; t += 0.5f) yield return new WaitForSecondsRealtime(0.5f);
			if (box.createGameButton != null && !box.createGameButton.interactable) { Fail("Create is disabled (name taken, or Steam offline?)"); yield break; }
			Log("Creating world '" + name + "'");
			// (a test world gets defaultPlan unless a test chose one: not the plan the last world was made with, ROADMAP CB5)
			if (WorldDirector.PendingPlan == null) WorldDirector.PendingPlan = WorldDirector.DefaultPlan;
			box.Button_CreateNewGame();
			yield return WaitForWorld();
		}

		[ConsoleCommand(name: "CIUITest", docs: "Dev, editor: the editor screen - panels per tab, buttons in their groups, windows, object browser; screenshots ui_<tab>.png")]
		public static void UITest()
		{
			StartTest(UITestRoutine());
		}

		static IEnumerator UITestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			Canvas canvas = EditorUI.Canvas;
			Check(ref ok, canvas != null && canvas.isActiveAndEnabled, "the editor canvas exists");
			GameObject oldToolbar = GameObject.Find("Toolbar");
			Check(ref ok, oldToolbar == null, "the bundle's old toolbar is hidden");
			string[] groups = { "TopBar/Group_File", "TopBar/Group_Edit", "TopBar/Group_Tabs", "TopBar/Group_App", "ToolPanelFrame/Viewport/ToolPanel/TerrainTools/Group_Sculpt", "ToolPanelFrame/Viewport/ToolPanel/TerrainTools/Group_Paint ground",
				"ToolPanelFrame/Viewport/ToolPanel/TerrainTools/Group_Brush", "ToolPanelFrame/Viewport/ToolPanel/ObjectTools/Group_Transform", "ToolPanelFrame/Viewport/ToolPanel/ObjectTools/Group_Selection", "ToolPanelFrame/Viewport/ToolPanel/ObjectTools/Group_Placing",
				"ToolPanelFrame/Viewport/ToolPanel/IslandTools/Group_Island", "ToolPanelFrame/Viewport/ToolPanel/IslandTools/Group_Generate", "StatusBar" };
			foreach (string g in groups)
			{
				Transform t = canvas.transform.Find(g);
				bool bordered = g == "StatusBar" || (t != null && t.Find("Border") != null);
				Check(ref ok, t != null && bordered, g + (t != null ? " (" + t.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length + " buttons" + (bordered ? ", bordered" : ", NO border") + ")" : " missing"));
			}

			// Each tab shows its own panel (and the object browser only on the Objects tab); screenshots of each
			string[] names = { "terrain", "objects", "island" };
			Transform tools = canvas.transform.Find("ToolPanelFrame/Viewport/ToolPanel");
			Transform browser = canvas.transform.Find("ObjectBrowser");
			for (int i = 0; i < 3; i++)
			{
				EditorUI.SetTab((TAB)i);
				yield return null;
				bool right = tools.Find("TerrainTools").gameObject.activeSelf == (i == 0) && tools.Find("ObjectTools").gameObject.activeSelf == (i == 1) &&
					tools.Find("IslandTools").gameObject.activeSelf == (i == 2) && browser.gameObject.activeSelf == (i == 1);
				Check(ref ok, right, "tab " + names[i] + " shows its panel" + (i == 1 ? " and the object browser" : ""));
				yield return new WaitForSecondsRealtime(i == 1 ? 3f : 0.5f); // thumbnails render a few per frame
				Screenshot(new[] { "ui_" + names[i] });
				yield return new WaitForSecondsRealtime(0.5f);
			}

			// Panels fit on the screen
			EditorUI.SetTab(TAB.ObjectPlace);
			yield return null;
			Vector3[] c = new Vector3[4];
			EditorUI.ToolFrame.GetWorldCorners(c);
			Check(ref ok, c[0].y >= 0 && c[2].x <= Screen.width, "the tool panel fits on the screen (bottom at " + c[0].y.ToString("F0") + " px)");
			((RectTransform)browser).GetWorldCorners(c);
			Check(ref ok, c[0].x > 0 && c[2].x <= Screen.width + 1 && c[0].y >= 0, "the object browser fits on the screen");

			// Windows open and close
			IslandFilesWindow.Open(); yield return null;
			Screenshot(new[] { "ui_islands" }); yield return new WaitForSecondsRealtime(0.5f);
			bool files = IslandFilesWindow.IsOpen; IslandFilesWindow.Close();
			GeneratorWindow.Open(); yield return null;
			Screenshot(new[] { "ui_generator" }); yield return new WaitForSecondsRealtime(0.5f);
			bool gen = GeneratorWindow.IsOpen; GeneratorWindow.Close();
			Check(ref ok, files && gen && !IslandFilesWindow.IsOpen && !GeneratorWindow.IsOpen, "the islands and generator windows open and close");

			// Brush buttons set the tool
			Transform sculpt = canvas.transform.Find("ToolPanelFrame/Viewport/ToolPanel/TerrainTools/Group_Sculpt");
			sculpt.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Button_Flatten").onClick.Invoke();
			Check(ref ok, terraineditor.modificationAction == terraineditor.TerrainModificationAction.Flatten, "the Flatten button picks the flatten brush");
			sculpt.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Button_Raise").onClick.Invoke();

			EditorUI.SetTab(TAB.TerrainEdit);
			if (ok) Log("PASS: editor UI"); else Fail("editor UI");
		}

		[ConsoleCommand(name: "CICatalogTest", docs: "Dev, editor: all of Raft's objects - index of every island scene, buildables, loading a category on demand, placing, saving and loading an island with such objects (cicatalog.island). CICatalogTest [category]")]
		public static void CatalogTest(string[] args)
		{
			StartTest(CatalogTestRoutine(args != null && args.Length > 0 ? string.Join(" ", args) : null));
		}

		static IEnumerator CatalogTestRoutine(string category)
		{
			yield return WaitForEditor(false);
			bool ok = true;
			float t0 = Time.realtimeSinceStartup;
			yield return PlaceableCatalog.EnsureIndex();
			Log("Index " + (PlaceableCatalog.IndexIsCurrent ? "current" : "NOT current") + " after " + (Time.realtimeSinceStartup - t0).ToString("F0") + " s");
			var browse = PlaceableCatalog.Browse();
			foreach (var cat in browse)
				Log(string.Format("  {0}: {1} objects, {2} loaded", cat.Key, cat.Value.Count, cat.Value.Count(e => e.Loaded)));
			Check(ref ok, PlaceableCatalog.IndexIsCurrent && browse.Sum(cb => cb.Value.Count(e => !e.Loaded)) > 100, "the index lists objects of Raft's other islands");
			int buildables = PlaceableCatalog.CoreNames.Count(n => PlaceableCatalog.CategoryOf(n) == PlaceableCatalog.BuildablesCategory);
			Check(ref ok, buildables > 20, buildables + " of Raft's buildable items (storage, furniture, decorations...) can be placed");

			// Open one on-demand category (default: the smallest that isn't loaded yet)
			var target = category != null ? browse.FirstOrDefault(cb => cb.Key.Equals(category, StringComparison.OrdinalIgnoreCase))
				: browse.Where(cb => cb.Value.Any(e => !e.Loaded)).OrderBy(cb => cb.Value.Count).FirstOrDefault();
			if (target.Key == null) { Fail("no category to load"); yield break; }
			t0 = Time.realtimeSinceStartup;
			yield return PlaceableCatalog.EnsureCategory(target.Key);
			int loaded = target.Value.Count(e => PlaceableCatalog.IsLoaded(e.Name));
			Check(ref ok, loaded == target.Value.Count, "category " + target.Key + ": " + loaded + " of " + target.Value.Count + " objects loaded in " + (Time.realtimeSinceStartup - t0).ToString("F1") + " s");

			// Place three of them on the island, save, wipe, load
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			Terrain terrain = terraineditor.terrain;
			var names = target.Value.Where(e => PlaceableCatalog.IsLoaded(e.Name)).Take(3).Select(e => e.Name).ToList();
			for (int i = 0; i < names.Count; i++)
			{
				GameObject go = PlaceableCatalog.Spawn(names[i], placed);
				go.transform.position = terrain.transform.position + new Vector3(480f + i * 15f, DynamicIslands.EditorWaterLevel + 2f, 500f);
				go.AddComponent<EditorGameObject>().GameObjectName = names[i];
			}
			bool saved = DynamicIslands.SaveIsland("cicatalog");
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			DynamicIslands.LoadIsland("cicatalog");
			yield return new WaitForSecondsRealtime(1f);
			var back = placed.GetComponentsInChildren<EditorGameObject>().Select(e => e.GameObjectName).ToList();
			Check(ref ok, saved && names.Count > 0 && names.All(back.Contains), "an island with " + string.Join(", ", names.Select(PlaceableCatalog.DisplayName).ToArray()) + " saves and loads (" + back.Count + " objects back)");

			// Pictures for the browser
			if (names.Count > 0)
			{
				var raw = new GameObject("CITestThumb", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
				raw.transform.SetParent(EditorUI.Canvas.transform, false);
				ObjectThumbnails.Request(names[0], raw.GetComponent<UnityEngine.UI.RawImage>());
				yield return new WaitForSecondsRealtime(0.5f);
				Check(ref ok, ObjectThumbnails.Has(names[0]), "a picture of " + PlaceableCatalog.DisplayName(names[0]) + " was rendered");
				UnityEngine.Object.Destroy(raw);
			}

			if (ok) Log("PASS: object catalog"); else Fail("object catalog");
		}

		#endregion
	}
}
