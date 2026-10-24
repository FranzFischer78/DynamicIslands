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
	/// <summary>ROADMAP E9: renaming a saved island, with the worlds, plans and rules that name it.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIRenameIsland", docs: "Dev, editor: ROADMAP E9 - the Islands window's Rename: test islands, a plan, a world file, quest rewards and a spawnpool line naming 'citest-rn-a'; Rename to an existing name and a bad name refused; Rename to 'citest-rn-c' moves the file and its kept copy and changes every place that names it (island:, oneof:, quest of, near), others left alone")]
		public static void RenameIslandCommand(string[] args) { StartTest(RenameIslandRoutine()); }

		static IEnumerator RenameIslandRoutine()
		{
			if (!DynamicIslands.InEditor() || IslandFilesWindow.Root == null) { Fail("rename: in the editor"); yield break; }
			bool ok = true;
			const string a = "citest-rn-a", b = "citest-rn-b", c = "citest-rn-c", planName = "CI Rename Plan";
			string worlds = Path.Combine(DynamicIslands.assetpath, "worlds");
			string worldFile = Path.Combine(worlds, "citest-rn-world.txt"), rewardsFile = Path.Combine(worlds, "citest-rn.rewards");
			string pool = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
			string poolBefore = File.Exists(pool) ? File.ReadAllText(pool) : null;
			string source = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci") && !IslandNetwork.IsDownloadName(n));
			var made = new List<string>();
			try
			{
				foreach (string n in new[] { a, b, c }) if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n));
				File.Copy(IslandSpawner.PathFor(source), IslandSpawner.PathFor(a));
				string hash = IslandNetwork.HashOf(a);
				string copy = IslandNetwork.DownloadName(a, hash);
				File.Copy(IslandSpawner.PathFor(a), IslandSpawner.PathFor(copy), true);
				// b's rules: bring a, one of a, when a's quest is done, near a
				IslandFile fb = IslandFile.Load(IslandSpawner.PathFor(source));
				var rulesB = new List<IntroRule>
				{
					new IntroRule { Id = "rn1", What = "island", WhatArg = a, When = "visit", WhenRef = IntroRule.Self },
					new IntroRule { Id = "rn2", What = "oneof", WhatArg = "x, " + a, When = "quest", WhenRef = a, Where = "near", WhereRef = a },
					new IntroRule { Id = "rn3", What = "island", WhatArg = "citest-rn-other", When = "start" },
				};
				WorldDirector.SetRulesInProps(fb.Props, rulesB);
				fb.Save(IslandSpawner.PathFor(b));
				var plan = new WorldPlan { Name = planName, Random = false, Description = "CIRenameIsland" };
				plan.Rules.Add(new IntroRule { Id = "p1", What = "island", WhatArg = a, When = "start" });
				plan.Rules.Add(new IntroRule { Id = "p2", What = "island", WhatArg = "citest-rn-other", When = "step", WhenRef = a, WhenArg = "2" });
				plan.Save();
				Directory.CreateDirectory(worlds);
				File.WriteAllLines(worldFile, new[] { "# Custom islands in world 'CI Rename World': name|...", "@plan=" + planName, "@planrule=" + plan.Rules[0].ToLine(), a + "|1|2|3||p1||" + hash, "other|0|0|0||||" });
				File.WriteAllLines(rewardsFile, new[] { "rewarded " + a, "owed other" });
				File.WriteAllText(pool, (poolBefore ?? "") + (poolBefore != null && !poolBefore.EndsWith("\n") ? "\r\n" : "") + a + " 2\r\n");
				made.AddRange(new[] { a, b, copy });

				IslandFilesWindow.Open();
				GameObject root = IslandFilesWindow.Root;
				IslandFilesWindow.NameField.text = a;
				Check(ref ok, Click(root, "Rename") && IslandFilesWindow.StatusText.Contains("new name"), "Rename: asks for the new name (" + IslandFilesWindow.StatusText + ")");
				IslandFilesWindow.NameField.text = b;
				Click(root, "Rename");
				Check(ref ok, IslandFilesWindow.StatusText.Contains("already") && File.Exists(IslandSpawner.PathFor(a)), "a name in use is refused (" + IslandFilesWindow.StatusText + ")");
				IslandFilesWindow.NameField.text = "bad,name";
				Click(root, "Rename");
				Check(ref ok, File.Exists(IslandSpawner.PathFor(a)) && !File.Exists(IslandSpawner.PathFor("bad,name")), "a bad name is refused (" + IslandFilesWindow.StatusText + ")");
				IslandFilesWindow.NameField.text = c;
				Click(root, "Rename");
				made.Add(c); made.Add(IslandNetwork.DownloadName(c, hash));
				Check(ref ok, IslandFilesWindow.StatusText.StartsWith("Renamed"), "renamed: " + IslandFilesWindow.StatusText);
				Check(ref ok, !File.Exists(IslandSpawner.PathFor(a)) && File.Exists(IslandSpawner.PathFor(c)) && IslandNetwork.HashOf(c) == hash, "the file has the new name, its content (hash) unchanged");
				Check(ref ok, !File.Exists(IslandSpawner.PathFor(copy)) && File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(c, hash))), "the copy kept for saved worlds goes with it");
				List<IntroRule> rb = WorldDirector.RulesFromProps(IslandFile.Load(IslandSpawner.PathFor(b)).Props);
				Check(ref ok, rb.Count == 3 && rb[0].WhatArg == c && rb[0].WhenRef == IntroRule.Self && rb[1].WhatArg == "x, " + c && rb[1].WhenRef == c && rb[1].WhereRef == c && rb[2].WhatArg == "citest-rn-other",
					"another island's rules follow (island:, oneof:, quest of, near); the others left alone: " + string.Join(" / ", rb.Select(r => r.ToLine()).ToArray()));
				WorldPlan p2 = WorldPlan.Load(planName);
				Check(ref ok, p2 != null && p2.Rules[0].WhatArg == c && p2.Rules[1].WhenRef == c && p2.Rules[1].WhatArg == "citest-rn-other", "the plan follows (island:, a step of it)");
				string[] wl = File.ReadAllLines(worldFile);
				Check(ref ok, wl.Contains(c + "|1|2|3||p1||" + hash) && wl.Contains("other|0|0|0||||") && wl.Any(l => l.StartsWith("@planrule=") && IntroRule.Parse(l.Substring(10)).WhatArg == c),
					"the saved world follows: its island line (state and hash kept) and its kept plan");
				string[] rw = File.ReadAllLines(rewardsFile);
				Check(ref ok, rw.Contains("rewarded " + c) && rw.Contains("owed other"), "its quest reward follows");
				Check(ref ok, File.ReadAllLines(pool).Any(l => l.Trim() == c + " 2") && !File.ReadAllLines(pool).Any(l => l.Trim().StartsWith(a + " ")), "its spawnpool.txt line follows (with its weight)");
				Check(ref ok, IslandSpawner.ListSavedIslands().Contains(c) && !IslandSpawner.ListSavedIslands().Contains(a), "the list shows the new name");
				IslandFilesWindow.Close();
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e); }
			finally
			{
				IslandFilesWindow.Close();
				foreach (string n in made) try { if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n)); } catch { }
				foreach (string f in new[] { worldFile, rewardsFile, WorldPlan.PathFor(planName) }) try { if (File.Exists(f)) File.Delete(f); } catch { }
				try { if (poolBefore != null) File.WriteAllText(pool, poolBefore); else if (File.Exists(pool)) File.Delete(pool); CustomIslandSpawner.LoadPool(true); } catch { }
			}
			yield return null;
			if (ok) Log("PASS: rename island"); else Fail("rename island");
		}
			[ConsoleCommand(name: "CIPieces", docs: "Dev, editor: ROADMAP T4 - My groups and stamps: a test group and stamp renamed (a used name refused) and deleted (moved to deleted/groups / deleted/stamps), the object list and the stamp buttons follow; Manage... buttons in both tabs")]
		public static void PiecesCommand(string[] args) { StartTest(PiecesRoutine()); }

		static IEnumerator PiecesRoutine()
		{
			if (!DynamicIslands.InEditor() || PiecesWindow.Root == null) { Fail("pieces: in the editor"); yield break; }
			bool ok = true;
			string deleted = Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName);
			var cleanup = new List<string>
			{
				Path.Combine(GroupLibrary.Folder, "cigrp-a.group"), Path.Combine(GroupLibrary.Folder, "cigrp-b.group"), Path.Combine(GroupLibrary.Folder, "cigrp-used.group"),
				Path.Combine(TerrainStamps.Folder, "cistamp-a.stamp"), Path.Combine(TerrainStamps.Folder, "cistamp-b.stamp"),
				Path.Combine(Path.Combine(deleted, "groups"), "cigrp-b.group"), Path.Combine(Path.Combine(deleted, "stamps"), "cistamp-b.stamp"),
			};
			Check(ref ok, GameObject.Find("Button_ManageGroups") != null || Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>().Any(b => b.name == "Button_ManageGroups"), "Objects tab: Manage... beside Save as group");
			Check(ref ok, Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>().Any(b => b.name == "Button_ManageStamps"), "Terrain tab: Manage... beside Save stamp");
			// A group (empty: only its file matters here) and a stamp
			Directory.CreateDirectory(GroupLibrary.Folder);
			foreach (string g in new[] { "cigrp-a", "cigrp-used" })
				using (var w = new BinaryWriter(File.Create(Path.Combine(GroupLibrary.Folder, g + ".group")))) { w.Write(0x52474943u); w.Write(1); w.Write(0); }
			yield return GroupLibrary.Register("cigrp-a");
			TerrainStamps.Save(new TerrainStamps.Stamp { Name = "cistamp-a" });
			TerrainStamps.Load();
			EditorUI.RefreshStamps();

			PiecesWindow.Open(false);
			GameObject root = PiecesWindow.Root;
			PiecesWindow.Pick("cigrp-a");
			PiecesWindow.NameField.text = "cigrp-used";
			Click(root, "Rename");
			Check(ref ok, PiecesWindow.StatusText.Contains("already") && GroupLibrary.Exists("cigrp-a"), "a group name in use is refused (" + PiecesWindow.StatusText + ")");
			PiecesWindow.NameField.text = "cigrp-b";
			Click(root, "Rename");
			for (int i = 0; i < 30 && PlaceableCatalog.Get(GroupLibrary.Prefix + "cigrp-b") == null; i++) yield return null;
			Check(ref ok, !GroupLibrary.Exists("cigrp-a") && GroupLibrary.Exists("cigrp-b") && PlaceableCatalog.Get(GroupLibrary.Prefix + "cigrp-a") == null && PlaceableCatalog.Get(GroupLibrary.Prefix + "cigrp-b") != null,
				"the group is renamed: its file and its entry in My groups (" + PiecesWindow.StatusText + ")");
			Click(root, "Delete");
			Check(ref ok, GroupLibrary.Exists("cigrp-b") && PiecesWindow.StatusText.Contains("again"), "Delete asks first");
			Click(root, "Delete");
			Check(ref ok, !GroupLibrary.Exists("cigrp-b") && File.Exists(Path.Combine(Path.Combine(deleted, "groups"), "cigrp-b.group")) && PlaceableCatalog.Get(GroupLibrary.Prefix + "cigrp-b") == null,
				"the group is deleted: moved to deleted/groups, gone from My groups");

			Click(root, "Stamps");
			PiecesWindow.Pick("cistamp-a");
			Check(ref ok, PiecesWindow.Picked == "cistamp-a", "the Stamps tab lists the stamp");
			PiecesWindow.NameField.text = "Hill";
			Click(root, "Rename");
			Check(ref ok, TerrainStamps.Exists("cistamp-a"), "renaming to a built-in name: still there (" + PiecesWindow.StatusText + ")");
			PiecesWindow.NameField.text = "cistamp-b";
			Click(root, "Rename");
			Check(ref ok, !TerrainStamps.Exists("cistamp-a") && TerrainStamps.Exists("cistamp-b") && TerrainStamps.All.Any(s => s.Name == "cistamp-b") && !TerrainStamps.All.Any(s => s.Name == "cistamp-a"), "the stamp is renamed (the list read again)");
			Click(root, "Delete"); Click(root, "Delete");
			Check(ref ok, !TerrainStamps.Exists("cistamp-b") && File.Exists(Path.Combine(Path.Combine(deleted, "stamps"), "cistamp-b.stamp")) && !TerrainStamps.All.Any(s => s.Name == "cistamp-b"), "the stamp is deleted: moved to deleted/stamps");
			Check(ref ok, !Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>().Any(b => b.gameObject.activeInHierarchy && UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text.StartsWith("cistamp")), "the Terrain tab's stamp buttons follow");
			PiecesWindow.Close();
			PlaceableCatalog.RemoveCustom(GroupLibrary.Prefix + "cigrp-used");
			foreach (string f in cleanup) try { if (File.Exists(f)) File.Delete(f); } catch { }
			if (ok) Log("PASS: groups and stamps"); else Fail("groups and stamps");
		}
			[ConsoleCommand(name: "CIElevationPreview", docs: "Dev, editor: ROADMAP E2 - the blue sea plane follows the island's height: a saved island opened, Sunken (-30) puts the sea 30 m above its water line, Flying (60) 60 m below, 0 back; pictures shot_elev_sunken.png and shot_elev_flying.png; the island left as it was")]
		public static void ElevationPreviewCommand(string[] args) { StartTest(ElevationPreviewRoutine(args.Length > 0 ? string.Join(" ", args) : null)); }

		static IEnumerator ElevationPreviewRoutine(string island)
		{
			if (!DynamicIslands.InEditor() || DynamicIslands.WaterPlane == null) { Fail("elevation preview: in the editor"); yield break; }
			bool ok = true;
			island = island ?? IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci"));
			DynamicIslands.LoadIsland(island);
			// (opening may first load Raft scenes its objects come from: wait until it is open)
			for (float end = Time.realtimeSinceStartup + 90f; DynamicIslands.currentIslandName != island && Time.realtimeSinceStartup < end; ) yield return null;
			yield return new WaitForSecondsRealtime(1f);
			float before = DynamicIslands.currentElevation;
			Terrain terrain = terraineditor.terrain;
			float waterLine = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
			Vector3 centre = terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel, 500f);
			Transform cam = Camera.main.transform;
			foreach (float h in new[] { -30f, 60f, 0f })
			{
				IslandSettingsUndo.Change(() => DynamicIslands.currentElevation = h);
				yield return null; yield return null;
				float y = DynamicIslands.WaterPlane.transform.position.y;
				Check(ref ok, Mathf.Abs(y - (waterLine - h)) < 0.05f, "height " + h + " m: the blue plane is at the sea in a world (" + (y - waterLine).ToString("F1") + " m from the water line; terrain " + terraineditor.terrain.transform.position.y.ToString("F1") + ", sea level " + DynamicIslands.EditorWaterLevel.ToString("F1") + ", height " + DynamicIslands.currentElevation + ")");
				if (h != 0f)
				{
					cam.position = centre + new Vector3(-170f, h > 0 ? -20f : 75f, -230f);
					cam.LookAt(centre + new Vector3(0f, h > 0 ? -25f : -10f, 0f));
					yield return new WaitForSecondsRealtime(0.6f);
					Screenshot(new[] { h > 0 ? "elev_flying" : "elev_sunken" });
					yield return new WaitForSecondsRealtime(0.5f);
				}
			}
			IslandSettingsUndo.Change(() => DynamicIslands.currentElevation = before);
			yield return null;
			EditorAutosave.Saved(island); // (only the test's changes: the island's file is as it was)
			if (ok) Log("PASS: elevation preview"); else Fail("elevation preview");
		}
			[ConsoleCommand(name: "CIClipboard", docs: "Dev, editor: ROADMAP E4 - Copy a sign and a warthog herd, New island, Paste: both come back with their spacing and settings, selected, one undo step; the inspector's Place exactly fields move, turn and size an object (one undo step) and follow the gizmo; picture shot_place_exactly.png")]
		public static void ClipboardCommand(string[] args) { StartTest(ClipboardRoutine()); }

		static IEnumerator ClipboardRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			DynamicIslands.NewIsland();
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Transform placed = GameObject.Find("PlacedObjects").transform;
			var gizmo = DynamicIslands.EditorGizmoHandler;
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject sign = PlaceForTest("Note_Sign", c0, placed);
			EditorGameObject boar = PlaceForTest("Creature_Boar", c0 + new Vector3(4f, 0f, 2f), placed);
			NoteEditorWindow.Apply(sign, "Clip note", "Copied along");
			PropsCommand.Change(boar, ObjectProps.With(boar.Props, ObjectProps.CreatureCount, "3"));
			gizmo.ClearTargets(false);
			gizmo.AddTarget(sign.transform, false); gizmo.AddTarget(boar.transform, false);
			EditorUI.CopySelected();
			Check(ref ok, PlacementOptions.ClipboardCount == 2, "Copy: 2 objects on the clipboard");

			DynamicIslands.NewIsland();
			yield return null; yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Check(ref ok, placed.Cast<Transform>().Count(x => x.gameObject.activeSelf && x.GetComponent<EditorGameObject>() != null) == 0 && PlacementOptions.ClipboardCount == 2, "New island: nothing placed, the clipboard kept");
			Transform cam = Camera.main.transform;
			Vector3 c1 = c0 + new Vector3(60f, 0f, 40f);
			cam.position = c1 + new Vector3(0f, 25f, -25f);
			cam.LookAt(c1);
			EditorUI.Paste(false);
			yield return null;
			List<EditorGameObject> pasted = placed.Cast<Transform>().Where(x => x.gameObject.activeSelf).Select(x => x.GetComponent<EditorGameObject>()).Where(e => e != null).ToList();
			EditorGameObject ps = pasted.FirstOrDefault(e => e.GameObjectName == "Note_Sign"), pb = pasted.FirstOrDefault(e => e.GameObjectName == "Creature_Boar");
			Check(ref ok, pasted.Count == 2 && ps != null && pb != null, "Paste: both objects on the new island (" + pasted.Count + ")");
			if (ps != null && pb != null)
			{
				Check(ref ok, ((pb.transform.position - ps.transform.position) - new Vector3(4f, 0f, 2f)).magnitude < 0.05f, "... with their spacing kept");
				// (the clipboard's middle, at the lowest one's height, is where the screen's middle meets the ground)
				Vector3 mid = new Vector3((ps.transform.position.x + pb.transform.position.x) / 2f, Mathf.Min(ps.transform.position.y, pb.transform.position.y), (ps.transform.position.z + pb.transform.position.z) / 2f);
				Ray centre = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
				float off = Vector3.Cross(centre.direction, mid - centre.origin).magnitude;
				Check(ref ok, off < 0.5f && Mathf.Abs(terraineditor.terrain.SampleHeight(mid) + terraineditor.terrain.transform.position.y - mid.y) < 0.1f, "... where the middle of the screen meets the ground (" + off.ToString("F2") + " m off the line)");
				Check(ref ok, ObjectProps.Get(ps.Props, ObjectProps.NoteTitle) == "Clip note" && ObjectProps.Get(pb.Props, ObjectProps.CreatureCount) == "3", "... with their settings (the note, the herd of 3)");
				Check(ref ok, gizmo.SelectedRoots.Count(x => x != null) == 2, "... and selected");
				CommandUndoRedo.UndoRedoManager.Undo();
				yield return null;
				Check(ref ok, !ps.gameObject.activeSelf && !pb.gameObject.activeSelf, "one Undo takes the paste away");
				CommandUndoRedo.UndoRedoManager.Redo();
				yield return null;

				// Place exactly
				gizmo.ClearTargets(false);
				gizmo.AddTarget(ps.transform, false);
				for (int i = 0; i < 5; i++) yield return null;
				IList<UnityEngine.UI.InputField> f = ObjectInspector.PlaceFields;
				Check(ref ok, f.Count == 9, "the inspector has Place exactly: position, turn, size");
				if (f.Count == 9)
				{
					Vector3 before = ps.transform.position;
					string[] values = { "510", "3", "520.5", "0", "90", "0", "2", "2", "2" };
					for (int i = 0; i < 9; i++) f[i].text = values[i];
					f[8].onEndEdit.Invoke(f[8].text);
					Vector3 corner = terraineditor.terrain.transform.position + new Vector3(0f, DynamicIslands.EditorWaterLevel, 0f);
					Check(ref ok, (ps.transform.position - (corner + new Vector3(510f, 3f, 520.5f))).magnitude < 0.01f && Mathf.Abs(Mathf.DeltaAngle(ps.transform.eulerAngles.y, 90f)) < 0.1f && Mathf.Abs(ps.transform.lossyScale.x - 2f) < 0.01f,
						"typed: X 510, Y 3 above the sea, Z 520.5, turned 90, size 2 (" + (ps.transform.position - corner) + ")");
					cam.position = ps.transform.position + new Vector3(-6f, 4f, -8f);
					cam.LookAt(ps.transform.position + Vector3.up);
					UnityEngine.UI.ScrollRect panel = f[0].GetComponentInParent<UnityEngine.UI.ScrollRect>();
					if (panel != null) panel.verticalNormalizedPosition = 0f; // (Place exactly is the last group)
					yield return new WaitForSecondsRealtime(0.6f);
					Screenshot(new[] { "place_exactly" });
					yield return new WaitForSecondsRealtime(0.4f);
					CommandUndoRedo.UndoRedoManager.Undo();
					yield return null;
					Check(ref ok, (ps.transform.position - before).magnitude < 0.01f && Mathf.Abs(ps.transform.lossyScale.x - 1f) < 0.01f, "one Undo puts it back");
					ps.transform.position += new Vector3(1.5f, 0f, 0f);
					yield return new WaitForSecondsRealtime(0.35f);
					float x;
					Check(ref ok, float.TryParse(f[0].text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) && Mathf.Abs(x - (ps.transform.position.x - corner.x)) < 0.02f, "the fields follow a move with the gizmo (X " + f[0].text + ")");
				}
			}
			DynamicIslands.NewIsland();
			if (ok) Log("PASS: clipboard and place exactly"); else Fail("clipboard and place exactly");
		}
			[ConsoleCommand(name: "CISelectionTools", docs: "Dev, editor: ROADMAP E3 - Same kind, All, box select (a box around the herds), the Placed objects list (Hide signs, Lock herds: clicks, boxes and All pass them by; Show all / Unlock all), hidden objects still saved; pictures shot_box_select.png and shot_placed_list.png")]
		public static void SelectionToolsCommand(string[] args) { StartTest(SelectionToolsRoutine()); }

		static IEnumerator SelectionToolsRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			DynamicIslands.NewIsland();
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Transform placed = GameObject.Find("PlacedObjects").transform;
			var gizmo = DynamicIslands.EditorGizmoHandler;
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			string plainName = PlaceableCatalog.CoreNames.FirstOrDefault(n => PlaceableCatalog.CategoryOf(n) == PlaceableCatalog.NatureCategory && ContentCatalog.CreatureOf(n) == null);
			EditorGameObject s1 = PlaceForTest("Note_Sign", c0, placed), s2 = PlaceForTest("Note_Sign", c0 + new Vector3(0f, 0f, 5f), placed);
			EditorGameObject b1 = PlaceForTest("Creature_Boar", c0 + new Vector3(10f, 0f, 0f), placed), b2 = PlaceForTest("Creature_Boar", c0 + new Vector3(10f, 0f, 5f), placed);
			EditorGameObject p1 = PlaceForTest(plainName, c0 + new Vector3(-10f, 0f, 2f), placed);
			Transform cam = Camera.main.transform;
			cam.position = c0 + new Vector3(0f, 22f, -18f);
			cam.LookAt(c0 + new Vector3(0f, 0f, 3f));
			yield return null;

			SelectionTools.Select(new List<EditorGameObject> { s1 }, false);
			Check(ref ok, SelectionTools.SelectSameKind() == 2 && gizmo.SelectedRoots.Count(x => x != null) == 2, "Same kind: both signs");
			Check(ref ok, Click(EditorUI.Canvas.gameObject, "SelectAll"), "the Selection group's All button");
			Check(ref ok, gizmo.SelectedRoots.Count(x => x != null) == 5, "All: the 5 objects (" + gizmo.SelectedRoots.Count(x => x != null) + ")");
			// A box around the two herds
			Camera c = Camera.main;
			Vector3 a = c.WorldToScreenPoint(b1.transform.position), z = c.WorldToScreenPoint(b2.transform.position);
			Vector2 from = new Vector2(Mathf.Min(a.x, z.x) - 40f, Mathf.Min(a.y, z.y) - 40f), to = new Vector2(Mathf.Max(a.x, z.x) + 40f, Mathf.Max(a.y, z.y) + 40f);
			SelectionTools.DrawBox(from, to);
			int boxed = SelectionTools.BoxSelect(from, to, false);
			Check(ref ok, boxed == 2 && gizmo.SelectedRoots.Contains(b1.transform) && gizmo.SelectedRoots.Contains(b2.transform), "box select: the two herds (" + boxed + ")");
			yield return new WaitForSecondsRealtime(0.5f);
			Screenshot(new[] { "box_select" });
			yield return new WaitForSecondsRealtime(0.4f);
			SelectionTools.ShowBox(false);
			Check(ref ok, SelectionTools.BoxSelect(from, to, true) == 2 && gizmo.SelectedRoots.Count(x => x != null) == 2, "Shift + box adds (nothing new here)");

			// The list: hide the signs, lock the herds
			PlacedListWindow.Open();
			yield return null;
			GameObject root = PlacedListWindow.Root;
			Check(ref ok, Click(root, "Hide_Note_Sign"), "the list has the signs (Hide)");
			yield return null;
			Check(ref ok, SelectionTools.IsHidden(s1) && SelectionTools.IsHidden(s2) && !s1.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Hide: the signs aren't drawn");
			Check(ref ok, Click(root, "Lock_Creature_Boar"), "the list has the herds (Lock)");
			yield return null;
			yield return new WaitForSecondsRealtime(0.4f);
			Screenshot(new[] { "placed_list" });
			yield return new WaitForSecondsRealtime(0.4f);
			PlacedListWindow.Close();
			Check(ref ok, SelectionTools.SelectAll() == 1, "All passes hidden and locked objects by (1 left)");
			Ray ray = c.ScreenPointToRay(c.WorldToScreenPoint(b1.transform.position + Vector3.up * 0.5f));
			Check(ref ok, PlacementOptions.PickObject(ray, Physics.DefaultRaycastLayers) != b1.transform, "a click on a locked herd doesn't pick it");
			ray = c.ScreenPointToRay(c.WorldToScreenPoint(s1.transform.position + Vector3.up * 1f));
			Check(ref ok, PlacementOptions.PickObject(ray, Physics.DefaultRaycastLayers) != s1.transform, "nor on a hidden sign");
			Check(ref ok, SelectionTools.BoxSelect(from, to, false) == 0, "a box passes the locked herds by");
			// Still saved
			const string isl = "citest-sel";
			DynamicIslands.SaveIsland(isl);
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor(isl));
			Check(ref ok, f.Objects.Count == 5, "saved with every object, hidden and locked ones too (" + f.Objects.Count + ")");
			System.IO.File.Delete(IslandSpawner.PathFor(isl));
			PlacedListWindow.Open();
			yield return null;
			Click(PlacedListWindow.Root, "Show all"); Click(PlacedListWindow.Root, "Unlock all");
			PlacedListWindow.Close();
			Check(ref ok, SelectionTools.HiddenCount == 0 && SelectionTools.LockedCount == 0 && s1.GetComponentsInChildren<Renderer>().Any(r => r.enabled) && SelectionTools.SelectAll() == 5, "Show all and Unlock all: all 5 again");
			DynamicIslands.NewIsland();
			if (ok) Log("PASS: selection tools"); else Fail("selection tools");
		}
			[ConsoleCommand(name: "CIMyIslands", docs: "Dev, editor: ROADMAP T3 - My islands: a test island of mine (used by a test world), one from the library, a generated one and a host copy, each under its filter; its world shown; a pool weight typed (spawnpool.txt); Rename and Delete from the list; the unused generated one is what Tidy up would move (not pressed: the player's own islands stay); picture shot_my_islands.png")]
		public static void MyIslandsCommand(string[] args) { StartTest(MyIslandsRoutine()); }

		static IEnumerator MyIslandsRoutine()
		{
			if (!DynamicIslands.InEditor() || MyIslandsWindow.Root == null) { Fail("my islands: in the editor"); yield break; }
			bool ok = true;
			const string mine = "citest-my-a", renamed = "citest-my-b", lib = "citest-my-lib", gen = "gen-citest-my";
			string worlds = Path.Combine(DynamicIslands.assetpath, "worlds");
			string worldFile = Path.Combine(worlds, "citest-my-world.txt");
			string pool = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
			string poolBefore = File.Exists(pool) ? File.ReadAllText(pool) : null;
			List<LibraryInstalled> installedBefore = LibraryPack.Installed();
			string source = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci") && !IslandNetwork.IsDownloadName(n));
			var made = new List<string> { mine, renamed, lib, gen };
			try
			{
				foreach (string n in new[] { mine, lib, gen }) File.Copy(IslandSpawner.PathFor(source), IslandSpawner.PathFor(n), true);
				string hash = IslandNetwork.HashOf(mine);
				string copy = IslandNetwork.DownloadName(mine, hash);
				File.Copy(IslandSpawner.PathFor(mine), IslandSpawner.PathFor(copy), true);
				made.Add(copy); made.Add(IslandNetwork.DownloadName(renamed, hash));
				Directory.CreateDirectory(worlds);
				File.WriteAllLines(worldFile, new[] { "# Custom islands in world 'CI My World': name|...", mine + "|0|0|0||||" + hash });
				var inst = LibraryPack.Installed();
				inst.Add(new LibraryInstalled { id = "citest-my", title = "CI My Lib", files = new List<LibraryInstalledFile> { new LibraryInstalledFile { name = lib, original = lib } } });
				LibraryPack.SaveInstalled(inst);

				MyIslandsWindow.Open();
				yield return null;
				foreach (var f in new[] { new[] { MyIslandsWindow.Mine, mine }, new[] { MyIslandsWindow.Library, lib }, new[] { MyIslandsWindow.Generated, gen }, new[] { MyIslandsWindow.Hosts, copy } })
				{
					MyIslandsWindow.SetFilter(f[0]);
					List<string> l = MyIslandsWindow.Listed;
					Check(ref ok, l.Contains(f[1]) && !made.Where(m => m != f[1]).Any(l.Contains), f[0] + ": lists '" + f[1] + "' and none of the other test islands");
				}
				MyIslandsWindow.SetFilter(MyIslandsWindow.All);
				MyIslandsWindow.SearchField.text = "citest-my";
				yield return null;
				Check(ref ok, MyIslandsWindow.Listed.Count == 4, "All + search: the 4 test islands (" + string.Join(", ", MyIslandsWindow.Listed.ToArray()) + ")");
				MyIslandsWindow.Pick(mine);
				Check(ref ok, MyIslandsWindow.StatusText.Contains("CI My World"), "picked: says which world uses it (" + MyIslandsWindow.StatusText + ")");
				yield return new WaitForSecondsRealtime(0.5f);
				Screenshot(new[] { "my_islands" });
				yield return new WaitForSecondsRealtime(0.4f);

				// A weight of its own
				UnityEngine.UI.InputField wf = MyIslandsWindow.Root.GetComponentsInChildren<UnityEngine.UI.InputField>().FirstOrDefault(x => x.name == "Weight_" + mine);
				Check(ref ok, wf != null, "the row has a weight field");
				if (wf != null) { wf.text = "3"; wf.onEndEdit.Invoke("3"); }
				bool listedInPool;
				Check(ref ok, Mathf.Approximately(CustomIslandSpawner.PoolWeight(mine, out listedInPool), 3f) && listedInPool && File.ReadAllLines(pool).Any(x => x.Trim() == mine + " 3"), "weight 3 written to spawnpool.txt");
				UnityEngine.UI.InputField cf = MyIslandsWindow.Root.GetComponentsInChildren<UnityEngine.UI.InputField>().FirstOrDefault(x => x.name == "Weight_" + copy);
				Check(ref ok, cf != null && !cf.interactable && cf.text == "0", "a host copy: weight 0, not to change");

				// Rename and delete
				MyIslandsWindow.Pick(mine);
				MyIslandsWindow.NameField.text = renamed;
				Click(MyIslandsWindow.Root, "Rename");
				Check(ref ok, File.Exists(IslandSpawner.PathFor(renamed)) && !File.Exists(IslandSpawner.PathFor(mine)) && File.ReadAllLines(worldFile).Any(x => x.StartsWith(renamed + "|")) && MyIslandsWindow.Listed.Contains(renamed), "Rename: the file, the world and the list (" + MyIslandsWindow.StatusText + ")");
				Check(ref ok, File.ReadAllLines(pool).Any(x => x.Trim() == renamed + " 3"), "... and its pool weight");
				MyIslandsWindow.Pick(lib);
				MyIslandsWindow.NameField.text = lib + "2";
				Click(MyIslandsWindow.Root, "Rename");
				made.Add(lib + "2");
				Check(ref ok, LibraryPack.Installed().Any(e => e.id == "citest-my" && e.files.Any(x => x.name == lib + "2")), "renaming a library island: the library's list follows");
				MyIslandsWindow.Pick(renamed);
				Click(MyIslandsWindow.Root, "Delete");
				Check(ref ok, File.Exists(IslandSpawner.PathFor(renamed)) && MyIslandsWindow.StatusText.Contains("CI My World"), "Delete asks first and names the world that has it");
				Click(MyIslandsWindow.Root, "Delete");
				string deleted = Path.Combine(Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), renamed + IslandFile.Extension);
				Check(ref ok, !File.Exists(IslandSpawner.PathFor(renamed)) && File.Exists(deleted) && !MyIslandsWindow.Listed.Contains(renamed), "Delete: moved to the deleted folder, gone from the list");
				try { File.Delete(deleted); } catch { }
				Housekeeping.Scan scan = Housekeeping.Look();
				Check(ref ok, scan.UnusedGenerated.Contains(gen), "Tidy up would move the unused generated island (" + scan.UnusedGenerated.Count + " in all; not pressed)");
				MyIslandsWindow.Close();
			}
			finally
			{
				MyIslandsWindow.Close();
				foreach (string n in made) try { if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n)); } catch { }
				try { if (File.Exists(worldFile)) File.Delete(worldFile); } catch { }
				try { LibraryPack.SaveInstalled(installedBefore); } catch { }
				try { if (poolBefore != null) File.WriteAllText(pool, poolBefore); CustomIslandSpawner.LoadPool(true); } catch { }
				IslandCache.Forget();
			}
			if (ok) Log("PASS: my islands"); else Fail("my islands");
		}
			[ConsoleCommand(name: "CIKitInfo", docs: "Dev, editor: ROADMAP CW1 - every indexed object of the story scenes whose name has <part> (e.g. Balboa), loaded and measured: its mesh box from its pivot (min and max, metres) and its colliders; written to Mods/DynamicIslands/kitinfo_<part>.txt")]
		public static void KitInfoCommand(string[] args) { StartTest(KitInfoRoutine(args.Length > 0 ? args[0] : "")); }

		static IEnumerator KitInfoRoutine(string part)
		{
			if (!DynamicIslands.InEditor() || part.Length == 0) { Fail("kit info: CIKitInfo <scene part>, in the editor"); yield break; }
			List<string> names = PlaceableCatalog.IndexedIn(part);
			if (part.Equals("core", StringComparison.OrdinalIgnoreCase) || names.Count == 0) names = PlaceableCatalog.Names.Where(n => n.IndexOf(args0(part), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			yield return PlaceableCatalog.EnsureLoaded(names);
			var lines = new List<string>();
			Vector3 c = terraineditor.terrain.transform.position + new Vector3(500f, 300f, 500f);
			foreach (string name in names)
			{
				GameObject go = PlaceableCatalog.Spawn(name, null);
				if (go == null) { lines.Add(name + ": not loaded"); continue; }
				go.transform.position = c; go.transform.rotation = Quaternion.identity;
				Renderer[] rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
				if (rs.Length == 0) { lines.Add(name + ": no mesh"); UnityEngine.Object.Destroy(go); continue; }
				Bounds b = rs[0].bounds;
				foreach (Renderer r in rs) b.Encapsulate(r.bounds);
				Vector3 mn = b.min - c, mx = b.max - c;
				int cols = go.GetComponentsInChildren<Collider>(true).Count(x => !x.isTrigger);
				lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}	size {1:F2} x {2:F2} x {3:F2}	x {4:F2}..{5:F2}  y {6:F2}..{7:F2}  z {8:F2}..{9:F2}	{10} colliders",
					name, b.size.x, b.size.y, b.size.z, mn.x, mx.x, mn.y, mx.y, mn.z, mx.z, cols));
				UnityEngine.Object.Destroy(go);
			}
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "kitinfo_" + part + ".txt"));
			File.WriteAllLines(file, lines.ToArray());
			Log("PASS: kit info " + part + ": " + names.Count + " objects measured, written to " + file);
		}

		static string args0(string s) { return s; }
			[ConsoleCommand(name: "CIKitShow", docs: "Dev, editor: ROADMAP CW1 - a new island with flat land 2 m over the sea, the named pieces (separated by +) in a row from west to east, pivots on a line, each turned yaw (CIKitShow <yaw> <a+b+c> [camera height]); picture shot_kit.png. Leaves them placed for a look")]
		public static void KitShowCommand(string[] args) { StartTest(KitShowRoutine(args)); }

		static IEnumerator KitShowRoutine(string[] args)
		{
			if (!DynamicIslands.InEditor() || args.Length < 2) { Fail("kit show: CIKitShow <yaw> <a+b+c> [camera height]"); yield break; }
			float yaw = float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
			float camH = 0f;
			string joined = string.Join(" ", args.Skip(1).ToArray());
			string[] parts = joined.Split('+');
			if (parts.Length > 0 && float.TryParse(parts[parts.Length - 1].Trim().Split(' ').Last(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out camH) && parts[parts.Length - 1].Trim().Contains(" "))
			{
				string last = parts[parts.Length - 1].Trim();
				parts[parts.Length - 1] = last.Substring(0, last.LastIndexOf(' '));
			}
			else camH = 0f;
			List<string> names = parts.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
			EditorUI.SetTab(TAB.ObjectPlace);
			DynamicIslands.NewIsland();
			yield return null;
			Terrain terrain = terraineditor.terrain;
			TerrainData data = terrain.terrainData;
			int res = data.heightmapResolution;
			float h = (DynamicIslands.EditorWaterLevel + 2f) / data.size.y;
			var heights = new float[res, res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
				{
					float dx = (x / (float)(res - 1) - 0.5f) * data.size.x, dz = (z / (float)(res - 1) - 0.5f) * data.size.z;
					float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
					heights[z, x] = d < 150f ? h : d < 170f ? h * (1f - (d - 150f) / 20f) : 0f;
				}
			data.SetHeights(0, 0, heights);
			yield return PlaceableCatalog.EnsureLoaded(names);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			float ground = terrain.transform.position.y + DynamicIslands.EditorWaterLevel + 2f;
			Vector3 start = terrain.transform.position + new Vector3(500f, 0f, 500f);
			start.y = ground;
			// widths first: the row is centred
			var widths = new List<float>();
			foreach (string n in names)
			{
				Bounds b;
				widths.Add(GenBuildings.Measured(n, out b) ? Mathf.Max(b.size.x, b.size.z) : 4f);
			}
			float total = widths.Sum() + 3f * (names.Count - 1), x0 = -total / 2f;
			var lines = new List<string>();
			for (int i = 0; i < names.Count; i++)
			{
				Vector3 at = start + new Vector3(x0 + widths[i] / 2f, 0f, 0f);
				EditorGameObject e = PlaceForTest(names[i], at, placed);
				if (e != null) e.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
				lines.Add((i + 1) + ". " + names[i] + (e == null ? " (missing)" : "") + " at x " + (x0 + widths[i] / 2f).ToString("F1"));
				x0 += widths[i] + 3f;
			}
			float span = Mathf.Max(total, 12f);
			Transform cam = Camera.main.transform;
			cam.position = start + new Vector3(0f, camH > 0f ? camH : span * 0.35f + 4f, -span * 0.75f - 6f);
			cam.LookAt(start + new Vector3(0f, 2f, 0f));
			yield return new WaitForSecondsRealtime(1.2f);
			Screenshot(new[] { "kit" });
			Log("PASS: kit show: " + string.Join(" | ", lines.ToArray()));
		}
			[ConsoleCommand(name: "CIQuestItemTypes", docs: "Dev: lists Raft's quest item types (the story items raft-<type> they become)")]
		public static void QuestItemTypesCommand(string[] args)
		{
			Log("PASS: quest item types: " + string.Join(", ", StoryItems.QuestItems.Select(q => q.questItemType.ToString()).OrderBy(x => x).ToArray()));
		}
	}
}
