using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandUndoRedo;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of the safety fixes (ROADMAP.md §2): saves replaced in one step and recovered, generated names never taken
	/// twice, patches applied one by one, the island's settings as undo steps, selecting not an undo step, objects Raft
	/// doesn't have kept as placeholders, saved worlds kept on their version when an edit would shift their state,
	/// deleted islands moved aside, the editor refused inside a world.
	/// </summary>
	public static partial class DevTests
	{
		static IslandObject Obj(string name, Dictionary<string, string> props = null) { return new IslandObject { Name = name, Props = props }; }

		[ConsoleCommand(name: "CISafetyUnit", docs: "Dev, anywhere: the safety fixes' rules - a save replaced in one step, a half-finished save recovered and a stale .tmp removed; a generated name that exists gets -2; which island edits shift a saved world's state (removed, reordered, a new chest) and which don't (moved, appended); every patch applied, none failed; the patch names in players' words")]
		public static void SafetyUnitCommand()
		{
			bool ok = true;
			string dir = Path.Combine(DynamicIslands.assetpath, "cisafety");
			Directory.CreateDirectory(dir);
			try
			{
				// Saves
				string f = Path.Combine(dir, "a.txt");
				SafeFile.WriteAllText(f, "one");
				SafeFile.WriteAllText(f, "two");
				Check(ref ok, File.ReadAllText(f) == "two" && !File.Exists(f + ".tmp"), "a file written twice: the second version, no .tmp left");
				File.WriteAllText(f + ".tmp", "three");
				File.Delete(f);
				Check(ref ok, SafeFile.Recover(f) && File.ReadAllText(f) == "three" && !File.Exists(f + ".tmp"), "Raft stopped between the steps (only the .tmp there): it becomes the file");
				File.WriteAllText(f + ".tmp", "half");
				Check(ref ok, !SafeFile.Recover(f) && File.ReadAllText(f) == "three" && !File.Exists(f + ".tmp"), "a .tmp next to its file (a write that stopped): removed, the file kept");
				File.WriteAllText(f + ".tmp", "four");
				File.Delete(f);
				Check(ref ok, SafeFile.RecoverAll(dir) == 1 && File.ReadAllText(f) == "four", "the sweep at the start finds it too");

				// Generated names
				string taken = "cisafety-gen-1";
				File.WriteAllText(IslandSpawner.PathFor(taken), "x");
				try { Check(ref ok, CustomIslandSpawner.FreeName(taken) == taken + "-2" && CustomIslandSpawner.FreeName("cisafety-free-1") == "cisafety-free-1", "a generated name that exists gets -2; a free one stays: " + CustomIslandSpawner.FreeName(taken)); }
				finally { File.Delete(IslandSpawner.PathFor(taken)); }

				// Which edits shift a saved world's state
				var loot = new Dictionary<string, string> { { ObjectProps.LootItems, "Plank:3" } };
				Func<IslandObject[], IslandFile> file = objs => new IslandFile { Objects = objs.ToList() };
				IslandFile before = file(new[] { Obj("A"), Obj("B"), Obj("C") });
				Check(ref ok, !DynamicIslands.ShiftsState(before, file(new[] { Obj("A"), Obj("B"), Obj("C") })), "the same objects (moved, other settings): no shift");
				Check(ref ok, !DynamicIslands.ShiftsState(before, file(new[] { Obj("A"), Obj("B"), Obj("C"), Obj("D") })), "an object added at the end: no shift");
				Check(ref ok, DynamicIslands.ShiftsState(before, file(new[] { Obj("A"), Obj("C") })), "an object removed: a shift");
				Check(ref ok, DynamicIslands.ShiftsState(before, file(new[] { Obj("B"), Obj("A"), Obj("C") })), "the order changed: a shift");
				Check(ref ok, DynamicIslands.ShiftsState(before, file(new[] { Obj("A"), Obj("B", loot), Obj("C") })), "an object made a chest (the chests' numbering): a shift");
				// (AU23: the island's rules and quest steps are kept by their place too)
				Func<string, string, IslandFile> withProps = (rules, steps) =>
				{
					IslandFile pf = file(new[] { Obj("A") });
					WorldDirector.SetRulesInProps(pf.Props, rules.Split(',').Where(w => w.Length > 0).Select(w => new IntroRule { When = w, What = "island", WhatArg = "X" }));
					if (steps.Length > 0) pf.Props[IslandQuest.KeySteps] = steps;
					return pf;
				};
				IslandFile ruled = withProps("start,quest", "reach|z|1|\nread|N|1|");
				Check(ref ok, !DynamicIslands.ShiftsState(ruled, withProps("start,quest,day", "reach|z|1|\nread|N|1|\nopen|C|1|")), "a rule and a step added at the end: no shift");
				Check(ref ok, !DynamicIslands.ShiftsState(ruled, withProps("start,quest", "reach|z|2|Go there\nread|N|1|")), "a step's count or text changed: no shift");
				Check(ref ok, DynamicIslands.ShiftsState(ruled, withProps("quest", "reach|z|1|\nread|N|1|")), "a rule removed: a shift");
				Check(ref ok, DynamicIslands.ShiftsState(ruled, withProps("start,quest", "read|N|1|")), "a quest step removed: a shift");
				Check(ref ok, DynamicIslands.ShiftsState(ruled, withProps("start,quest", "reach|z|1|\nread|Other note|1|")), "a step's note renamed: a shift");

				// Patches
				Check(ref ok, PatchHealth.FailedPatches.Count == 0 && PatchHealth.Applied >= 30, PatchHealth.Applied + " patches applied, " + PatchHealth.FailedPatches.Count + " failed");
				Check(ref ok, PatchHealth.Feature("StoryChainUnlock").Contains("story chain") && PatchHealth.Feature("SomethingElse").Contains("SomethingElse"), "a failed patch named in players' words: " + PatchHealth.Feature("StoryChainUnlock"));
			}
			finally { try { Directory.Delete(dir, true); } catch { } }
			if (ok) Log("PASS: safety rules"); else Fail("safety rules");
		}

		[ConsoleCommand(name: "CISafetyEditor", docs: "Dev, editor: the safety fixes in the editor - an island setting is an undo step and an unsaved change (undo brings it back); selecting objects isn't an undo step; an object Raft doesn't have loads as a red placeholder and is saved back unchanged; saving over an island a saved world uses keeps that world on its version when objects were removed, not when one was added at the end; a deleted island is moved to 'deleted'. Test files are removed after")]
		public static void SafetyEditorCommand() { StartTest(SafetyEditorRoutine()); }

		static IEnumerator SafetyEditorRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("safety editor: in the editor"); yield break; }
			bool ok = true;
			const string name = "cisafety";
			string path = IslandSpawner.PathFor(name);
			string worldFile = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), "cisafety-world.txt");
			string deleted = Path.Combine(Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), name + IslandFile.Extension);
			var cleanup = new List<string> { path, worldFile, deleted };
			try
			{
				// Island settings: an undo step, an unsaved change
				DynamicIslands.NewIsland();
				yield return null;
				int changes = UndoRedoManager.Changes, undos = UndoRedoManager.UndoCount;
				IslandSettingsUndo.Change(() => DynamicIslands.currentIslandProps[IslandProps.Title] = "Safety title");
				Check(ref ok, UndoRedoManager.Changes == changes + 1 && UndoRedoManager.UndoCount == undos + 1 && EditorAutosave.Unsaved, "a new island name is an undo step and an unsaved change");
				UndoRedoManager.Undo();
				Check(ref ok, !DynamicIslands.currentIslandProps.ContainsKey(IslandProps.Title), "Undo takes it back");
				UndoRedoManager.Redo();
				Check(ref ok, ObjectProps.Get(DynamicIslands.currentIslandProps, IslandProps.Title) == "Safety title", "Redo puts it back");
				int u = UndoRedoManager.UndoCount;
				IslandSettingsUndo.Change(() => { });
				Check(ref ok, UndoRedoManager.UndoCount == u, "nothing changed: no step");

				// Selecting isn't an undo step
				string real = PlaceableCatalog.Names.FirstOrDefault(n => !ContentCatalog.IsCreature(n) && !ContentCatalog.IsZone(n));
				var island = new IslandFile
				{
					Name = name, TerrainSize = new Vector3(120, 40, 120), HeightmapResolution = 33, Heights = new float[33, 33], WaterLevel = DynamicIslands.EditorWaterLevel,
					Objects = new List<IslandObject>
					{
						new IslandObject { Name = real, Position = new Vector3(10, 5, 10) },
						new IslandObject { Name = "NoSuchRaftObject_CI", Position = new Vector3(20, 6, 30), EulerRotation = new Vector3(0, 45, 0), Scale = new Vector3(2, 1, 2),
							Props = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Kept" } } },
						new IslandObject { Name = real, Position = new Vector3(40, 5, 40) },
					}
				};
				island.Save(path);
				Check(ref ok, DynamicIslands.LoadIsland(name), "a test island with an object Raft doesn't have opens");
				yield return null;
				var gizmo = UnityEngine.Object.FindObjectOfType<RuntimeGizmos.TransformGizmo>();
				EditorGameObject first = UnityEngine.Object.FindObjectsOfType<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == real);
				int before = UndoRedoManager.UndoCount;
				if (gizmo != null && first != null) { gizmo.AddTarget(first.transform); gizmo.ClearTargets(); }
				Check(ref ok, gizmo != null && first != null && UndoRedoManager.UndoCount == before, "selecting and deselecting aren't undo steps (" + (UndoRedoManager.UndoCount - before) + ")");

				// The placeholder
				EditorGameObject ph = UnityEngine.Object.FindObjectsOfType<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == "NoSuchRaftObject_CI");
				Check(ref ok, ph != null && ph.name.Contains(IslandSpawner.MissingTag), "the missing object is a placeholder in its place");
				Check(ref ok, DynamicIslands.SaveIsland(name), "saved");
				IslandFile back = IslandFile.Load(path);
				IslandObject kept = back.Objects.FirstOrDefault(o => o.Name == "NoSuchRaftObject_CI");
				Check(ref ok, back.Objects.Count == 3 && kept != null && ObjectProps.Get(kept.Props, ObjectProps.NoteTitle) == "Kept" && (kept.Scale - new Vector3(2, 1, 2)).magnitude < 0.01f && Mathf.Abs(kept.EulerRotation.y - 45f) < 0.5f,
					"saved back with its name, settings, size and turn (" + back.Objects.Count + " objects)");

				// A saved world that uses it
				string hash = IslandNetwork.HashOf(name);
				Directory.CreateDirectory(Path.GetDirectoryName(worldFile));
				File.WriteAllLines(worldFile, new[] { "# Custom islands in world 'CI Safety': name|x|y|z", "@auto=on", name + "|0|0|0||||" + hash });
				string copy = IslandSpawner.PathFor(IslandNetwork.DownloadName(name, hash));
				cleanup.Add(copy);
				// ... an object added at the end: the world gets it
				GameObject added = PlaceableCatalog.Spawn(real, first != null ? first.transform.parent : null, false);
				if (added != null) { added.transform.position = new Vector3(50, 5, 50); EditorGameObject.Attach(added, real, new Dictionary<string, string>()); }
				yield return null;
				DynamicIslands.SaveIsland(name);
				Check(ref ok, added != null && !File.Exists(copy), "an object added at the end: no copy kept, the world gets this version");
				// ... an object removed: the world gets this version, what was used there carried to the same objects (R1b)
				hash = IslandNetwork.HashOf(name);
				File.WriteAllLines(worldFile, new[] { "# Custom islands in world 'CI Safety': name|x|y|z", "@auto=on", name + "|0|0|0||||" + hash });
				copy = IslandSpawner.PathFor(IslandNetwork.DownloadName(name, hash));
				cleanup.Add(copy);
				cleanup.Add(Path.Combine(Path.Combine(Path.GetDirectoryName(deleted), LibraryPack.KeptVersionsFolder), Path.GetFileName(copy)));
				if (first != null) first.gameObject.SetActive(false); // (deleted in the editor = hidden, not saved)
				yield return null;
				DynamicIslands.SaveIsland(name);
				string[] line = File.ReadAllLines(worldFile).First(l => l.StartsWith(name + "|")).Split('|');
				Check(ref ok, !File.Exists(copy) && line[7] == IslandNetwork.HashOf(name), "an object removed: the world gets this version (its state carried), no copy kept");
				// ... a quest step removed: the world keeps its version
				DynamicIslands.currentIslandProps[IslandQuest.KeySteps] = "reach|z|1|\nread|N|1|";
				DynamicIslands.SaveIsland(name);
				hash = IslandNetwork.HashOf(name);
				File.WriteAllLines(worldFile, new[] { "# Custom islands in world 'CI Safety': name|x|y|z", "@auto=on", name + "|0|0|0||||" + hash });
				copy = IslandSpawner.PathFor(IslandNetwork.DownloadName(name, hash));
				cleanup.Add(copy);
				DynamicIslands.currentIslandProps[IslandQuest.KeySteps] = "read|N|1|";
				DynamicIslands.SaveIsland(name);
				Check(ref ok, File.Exists(copy) && IslandNetwork.HashOf(IslandNetwork.DownloadName(name, hash)) == hash, "a quest step removed: the world keeps the version it started with (" + Path.GetFileName(copy) + ")");
				Check(ref ok, WorldCopy.LocalFileFor(name, hash) == IslandNetwork.DownloadName(name, hash), "... and loads that one by its hash");

				// Deleting moves it aside
				DynamicIslands.NewIsland();
				IslandFilesWindow.MoveToDeleted(name);
				Check(ref ok, !File.Exists(path) && File.Exists(deleted), "a deleted island is moved to '" + IslandFilesWindow.DeletedFolderName + "'");
				TerrainData td = terraineditor.terrain.terrainData;
				Check(ref ok, td.heightmapResolution == 513 && Mathf.Approximately(td.size.x, 1000f), "New after a small island: the full build area again (" + td.size.x + " m, " + td.heightmapResolution + " heights)");
			}
			finally
			{
				foreach (string f in cleanup) try { if (File.Exists(f)) File.Delete(f); } catch { }
				try { DynamicIslands.NewIsland(); } catch { }
			}
			if (ok) Log("PASS: safety editor"); else Fail("safety editor");
		}

		[ConsoleCommand(name: "CISafetyWorld", docs: "Dev, world: LoadEditor inside a world is refused (the world stays, the player is told); Resync asks the host again without harm")]
		public static void SafetyWorldCommand() { StartTest(SafetyWorldRoutine()); }

		static IEnumerator SafetyWorldRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("safety world: in a world"); yield break; }
			bool ok = true;
			DynamicIslands.LoadEditor(new string[0]);
			yield return new WaitForSeconds(3f);
			Check(ref ok, LoadSceneManager.IsGameSceneLoaded && !DynamicIslands.InEditor(), "LoadEditor inside a world is refused: still in the world");
			if (Raft_Network.IsHost)
			{
				// (CB12: what players send can't grow without end - a long journal page is cut, holds that ran out are dropped)
				StoryBook.AddPage("ci-cb12-long", new string('T', 500), new string('x', StoryBook.MaxPageText + 5000), "ci");
				StoryBook.Page page = StoryBook.Pages.FirstOrDefault(p => p.Key == "ci-cb12-long");
				Check(ref ok, page != null && page.Text.Length == StoryBook.MaxPageText && page.Title.Length == StoryBook.MaxTitle, "a journal page too long is cut to " + StoryBook.MaxPageText + " letters (" + (page != null ? page.Text.Length.ToString() : "none") + ")");
				var fake = new IslandWorldState.Entry { Id = -77 };
				for (int i = 0; i < Claims.MaxHeld + 50; i++) Claims.HostGrant(fake, 1000 + i, 1UL);
				int full = Claims.HeldCount;
				yield return new WaitForSecondsRealtime(6.5f);
				Claims.HostGrant(fake, 5, 1UL);
				Check(ref ok, full >= Claims.MaxHeld + 50 && Claims.HeldCount < 50, "the host's claim holds: " + full + " fresh ones kept, those run out dropped (" + Claims.HeldCount + " left)");
			}
			if (!Raft_Network.IsHost) { IslandNetwork.Resync(); yield return new WaitForSeconds(8f); Check(ref ok, IslandNetwork.HasList, "Resync: the host's list came again"); }
			if (ok) Log("PASS: safety world"); else Fail("safety world");
		}
	}
}
