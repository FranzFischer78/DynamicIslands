using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of the island library window (LibraryWindow, LibraryClient): against a small library written to disk
	/// (file://) - the list, pictures, Download, Update, Remove, a damaged file, no connection, a newer mod's entry - and
	/// once against the real library on GitHub (libraryonline.ps1, TEST_CATALOGUE G45-G47).
	/// </summary>
	public static partial class DevTests
	{
		static string TestLibraryFolder { get { return Path.GetFullPath(Path.Combine(LibraryPack.LibraryFolder, "citest-lib")); } }

		static byte[] TestJpg(Color c)
		{
			var tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
			tex.SetPixels(Enumerable.Repeat(c, 64 * 64).ToArray());
			tex.Apply();
			byte[] b = tex.EncodeToJPG(80);
			UnityEngine.Object.Destroy(tex);
			return b;
		}

		/// <summary>Writes an entry folder into the test library and returns its index.json object.</summary>
		static Dictionary<string, object> TestEntry(string kind, LibraryInfo info, Dictionary<string, byte[]> files)
		{
			string path = kind + "s/" + info.id;
			string dir = Path.Combine(TestLibraryFolder, path.Replace('/', Path.DirectorySeparatorChar));
			if (Directory.Exists(dir)) Directory.Delete(dir, true);
			Directory.CreateDirectory(dir);
			foreach (var f in files) File.WriteAllBytes(Path.Combine(dir, f.Key), f.Value);
			var o = LibraryJson.Parse(info.ToJson()) as Dictionary<string, object>;
			o["path"] = path;
			o["islands"] = files.Keys.Count(k => k.EndsWith(".island"));
			o["size"] = files.Values.Sum(b => (long)b.Length);
			o["updated"] = "2026-09-28";
			o["files"] = files.OrderBy(f => f.Key).Select(f => (object)new Dictionary<string, object> { { "name", f.Key }, { "size", f.Value.Length }, { "sha256", LibraryPack.Sha256(f.Value) } }).ToList();
			return o;
		}

		static void WriteTestIndex(params Dictionary<string, object>[] entries)
		{
			File.WriteAllText(Path.Combine(TestLibraryFolder, "index.json"), LibraryJson.Write(new Dictionary<string, object> { { "format", 1 }, { "library", "CI test library" }, { "download", "" }, { "entries", entries.Cast<object>().ToList() } }));
		}

		static IEnumerator WaitNotBusy(float seconds)
		{
			for (float t = 0; LibraryWindow.Busy && t < seconds; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
		}

		static IEnumerator LoadList(bool force)
		{
			bool doneLoading = false;
			yield return LibraryClient.LoadIndex(force, e => doneLoading = true);
			for (float t = 0; !doneLoading && t < 30f; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
		}

		[ConsoleCommand(name: "CILibraryBrowse", docs: "Dev, main menu: the ISLAND LIBRARY window against a library written to disk (file://): the main menu's button opens it, the plans and islands tabs, icons and pictures, Download installs, the button says Installed; a newer version in the list says Update and updates; a damaged file is refused and nothing installed; an entry from a newer mod says Download anyway; Remove asks first; no connection says so; the window fits the screen. Cleans up")]
		public static void LibraryBrowseCommand() { StartTest(LibraryBrowseRoutine()); }

		static IEnumerator LibraryBrowseRoutine()
		{
			bool ok = true;
			CleanLibTests();
			if (Directory.Exists(TestLibraryFolder)) Directory.Delete(TestLibraryFolder, true);
			Directory.CreateDirectory(TestLibraryFolder);
			try
			{
				// A plan (a brings b, b brings c) and an island, made and exported as a creator would
				MakeLibIsland(LibC, 3); MakeLibIsland(LibB, 2, LibC); MakeLibIsland(LibA, 1, LibB);
				var plan = new WorldPlan { Name = LibPlan, Description = "a test plan", Random = false };
				plan.Rules.Add(new IntroRule { Id = "start", What = "island", WhatArg = LibA, When = "start", Where = "ahead", Distance = 300 });
				plan.Save();
				MakeLibIsland("cilib-solo", 9);
				string error;
				string planZip = LibraryPack.Export(new LibraryInfo { id = "ci-lib-pack", title = "CI Lib Pack", author = "CI Tester", summary = "a test plan", description = "Three islands in a chain.", tags = new[] { "test", "quest" } }, null, WorldPlan.Load(LibPlan), TestJpg(Color.green), TestJpg(Color.blue), out error);
				string soloZip = LibraryPack.Export(new LibraryInfo { id = "ci-lib-solo", title = "CI Lib Solo", author = "CI Tester", summary = "a test island" }, "cilib-solo", null, TestJpg(Color.red), TestJpg(Color.yellow), out error);
				LibraryPackContents pp = LibraryPack.Read(planZip, out error), sp = LibraryPack.Read(soloZip, out error);
				Check(ref ok, pp != null && sp != null, "two packs exported (a plan with 3 islands, an island)");
				if (pp == null || sp == null) yield break;
				Func<LibraryPackContents, Dictionary<string, byte[]>> filesOf = p => p.Files.Where(f => f.Key != "info.json").ToDictionary(f => f.Key, f => f.Value);
				Dictionary<string, object> planEntry = TestEntry(LibraryPack.KindPlan, pp.Info, filesOf(pp)), soloEntry = TestEntry(LibraryPack.KindIsland, sp.Info, filesOf(sp));
				WriteTestIndex(planEntry, soloEntry);
				// (a PC without them)
				DeleteLib(LibA, LibB, LibC, "cilib-solo");
				File.Delete(WorldPlan.PathFor(LibPlan));
				foreach (string z in new[] { planZip, soloZip }) File.Delete(z);

				LibraryClient.TestAddress = "file:///" + TestLibraryFolder.Replace('\\', '/') + "/";
				LibraryClient.Forget();
				// The main menu's button
				GameObject menuButton = GameObject.Find("MainMenuCanvas/MenuButtons/LIBRARY");
				Check(ref ok, menuButton != null && menuButton.activeInHierarchy, "the main menu has ISLAND LIBRARY");
				if (menuButton != null) menuButton.GetComponent<Button>().onClick.Invoke(); else LibraryWindow.Open();
				yield return LoadList(false);
				yield return new WaitForSecondsRealtime(1f);
				Check(ref ok, LibraryWindow.IsOpen && LibraryClient.Entries != null && LibraryClient.Entries.Count == 2, "the window opens and lists the library's 2 entries (" + LibraryWindow.Status + ")");
				Check(ref ok, LibraryWindow.Shown().SequenceEqual(new[] { "ci-lib-pack" }), "the plans tab shows the plan: " + string.Join(", ", LibraryWindow.Shown().ToArray()));
				Button pr = LibraryWindow.Row("ci-lib-pack");
				RawImage icon = pr != null ? pr.GetComponentsInChildren<RawImage>(true).FirstOrDefault(i => i.name == "Icon") : null;
				Check(ref ok, icon != null && icon.texture != null, "its row has its icon");
				Check(ref ok, LibraryWindow.Selected != null && LibraryWindow.Selected.Info.id == "ci-lib-pack" && LibraryWindow.Picture != null, "it is selected, with its picture");
				Check(ref ok, UIKit.LabelOf(LibraryWindow.MainButton).text == "Download" && !LibraryWindow.RemoveButton.gameObject.activeSelf, "the button says Download (no Remove yet)");
				// (the user, 2026-10-19: the detail says whether an island has a quest - text only)
				Check(ref ok, LibraryWindow.QuestText == "" && LibraryWindow.QuestLine(new LibraryInfo { tags = new[] { "Quest" } }) == "Quest: yes" && LibraryWindow.QuestLine(new LibraryInfo { tags = new[] { "test" } }).StartsWith("No quest") && LibraryWindow.QuestLine(new LibraryInfo { kind = LibraryPack.KindPlan, tags = new[] { "quest" } }) == "", "the quest line: a plan shows none; an island says Quest: yes by the quest tag (any case), else No quest");
				Screenshot(new[] { "library_window" });
				yield return new WaitForSecondsRealtime(0.6f);
				var off = OffScreen(LibraryWindow.Root.gameObject, Screen.width, Screen.height);
				Check(ref ok, off.Count == 0, "the window fits the screen" + (off.Count > 0 ? " - off: " + string.Join(", ", off.Take(4).ToArray()) : ""));
				// (another entry clicked: the plan's picture goes at once - a slow download left it up on every entry after it)
				Texture planPicture = LibraryWindow.Picture;
				LibraryWindow.Select("ci-lib-solo");
				Check(ref ok, planPicture != null && LibraryWindow.Picture != planPicture, "another entry selected: the plan's picture is not kept while its own loads");
				yield return new WaitForSecondsRealtime(1f);
				Check(ref ok, LibraryWindow.Picture != null && LibraryWindow.Picture != planPicture, "then that entry's own picture shows");
				LibraryWindow.Select("ci-lib-pack");
				yield return new WaitForSecondsRealtime(0.5f);

				// Download
				LibraryWindow.MainButton.onClick.Invoke();
				yield return WaitNotBusy(30f);
				Check(ref ok, new[] { LibA, LibB, LibC }.All(n => File.Exists(IslandSpawner.PathFor(n))) && WorldPlan.Load(LibPlan) != null && LibraryPack.Installed().Any(e => e.id == "ci-lib-pack" && e.source == LibraryPack.SourceLibrary),
					"Download installs the plan and its 3 islands: " + LibraryWindow.Progress);
				Check(ref ok, UIKit.LabelOf(LibraryWindow.MainButton).text == "Installed" && !LibraryWindow.MainButton.interactable && LibraryWindow.RemoveButton.gameObject.activeSelf, "then the button says Installed, and Remove is there");

				// A newer version in the library: Update
				pp.Info.version = 2; pp.Info.summary = "version 2";
				planEntry = TestEntry(LibraryPack.KindPlan, pp.Info, filesOf(pp));
				WriteTestIndex(planEntry, soloEntry);
				yield return LoadList(true);
				LibraryWindow.Select("ci-lib-pack");
				yield return null;
				Check(ref ok, LibraryClient.StateOf(LibraryWindow.Selected) == LibraryClient.State.Update && UIKit.LabelOf(LibraryWindow.MainButton).text == "Update", "a newer version in the list: the button says Update");
				LibraryWindow.MainButton.onClick.Invoke();
				yield return WaitNotBusy(30f);
				Check(ref ok, LibraryPack.Installed().Any(e => e.id == "ci-lib-pack" && e.version == 2) && UIKit.LabelOf(LibraryWindow.MainButton).text == "Installed", "Update installs version 2: " + LibraryWindow.Progress);

				// An island the player changed since, then version 3: the first click says so (and how to keep it), the
				// second replaces it with the library's
				MakeLibIsland(LibB, 7, LibC);
				pp.Info.version = 3; pp.Info.summary = "version 3";
				planEntry = TestEntry(LibraryPack.KindPlan, pp.Info, filesOf(pp));
				WriteTestIndex(planEntry, soloEntry);
				yield return LoadList(true);
				LibraryWindow.Select("ci-lib-pack");
				yield return null;
				Check(ref ok, LibraryPack.ChangedFiles("ci-lib-pack").SequenceEqual(new[] { LibB }), "the changed island is found: " + string.Join(", ", LibraryPack.ChangedFiles("ci-lib-pack").ToArray()));
				string changedSha = LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB)));
				LibraryWindow.MainButton.onClick.Invoke();
				yield return null;
				Check(ref ok, UIKit.LabelOf(LibraryWindow.MainButton).text == "Update, keep a copy" && LibraryWindow.Progress.Contains("'" + LibB + "'") && LibraryWindow.Progress.Contains("(yours)") && LibraryWindow.KeepButton.gameObject.activeSelf &&
					LibraryPack.Installed().Any(e => e.id == "ci-lib-pack" && e.version == 2) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB))) == changedSha,
					"Update on a changed island asks first, names it and offers Update, keep a copy / Keep my changes / Cancel (nothing changed yet): " + LibraryWindow.Progress);
				LibraryWindow.MainButton.onClick.Invoke();
				yield return WaitNotBusy(30f);
				Check(ref ok, LibraryPack.Installed().Any(e => e.id == "ci-lib-pack" && e.version == 3) && LibraryPack.ChangedFiles("ci-lib-pack").Count == 0 &&
					LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB))) == LibraryPack.Sha256(pp.Files[LibB + IslandFile.Extension]) &&
					File.Exists(IslandSpawner.PathFor(LibB + " (yours)")) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB + " (yours)"))) == changedSha,
					"the second click updates to version 3, replaces the changed island with the library's and keeps the player's as '" + LibB + " (yours)': " + LibraryWindow.Progress);

				// A damaged file: refused, nothing installed
				var files = LibraryJson.Objects(soloEntry, "files");
				Dictionary<string, object> islandFile = files.First(f => LibraryJson.Str(f, "name").EndsWith(".island"));
				islandFile["sha256"] = new string('0', 64);
				WriteTestIndex(planEntry, soloEntry);
				yield return LoadList(true);
				LibraryWindow.Select("ci-lib-solo");
				LibraryWindow.MainButton.onClick.Invoke();
				yield return WaitNotBusy(30f);
				Check(ref ok, !File.Exists(IslandSpawner.PathFor("cilib-solo")) && !LibraryPack.Installed().Any(e => e.id == "ci-lib-solo") && LibraryWindow.Progress.Contains("Nothing was installed"),
					"a file that isn't what the list says is refused, nothing installed: " + LibraryWindow.Progress);

				// An entry made with a newer mod: Download anyway
				soloEntry = TestEntry(LibraryPack.KindIsland, sp.Info, filesOf(sp));
				soloEntry["minModVersion"] = "99.0";
				WriteTestIndex(planEntry, soloEntry);
				yield return LoadList(true);
				LibraryWindow.Select("ci-lib-solo");
				yield return null;
				Check(ref ok, UIKit.LabelOf(LibraryWindow.MainButton).text == "Download anyway", "an entry from a newer mod version: 'Download anyway' with a warning");

				// Remove asks first
				LibraryWindow.Select("ci-lib-pack");
				yield return null;
				LibraryWindow.RemoveButton.onClick.Invoke();
				yield return null;
				Check(ref ok, UIKit.LabelOf(LibraryWindow.RemoveButton).text == "Sure? Remove" && File.Exists(IslandSpawner.PathFor(LibA)), "Remove asks first");
				LibraryWindow.RemoveButton.onClick.Invoke();
				yield return null;
				Check(ref ok, !File.Exists(IslandSpawner.PathFor(LibA)) && !LibraryPack.Installed().Any(e => e.id == "ci-lib-pack") && UIKit.LabelOf(LibraryWindow.MainButton).text == "Download", "then removes it (Download again)");

				// No connection
				LibraryClient.TestAddress = "file:///" + Path.Combine(TestLibraryFolder, "nothing-here").Replace('\\', '/') + "/";
				yield return LoadList(true);
				Check(ref ok, LibraryClient.Entries == null && LibraryClient.LastError != null && LibraryClient.LastError.Contains("Can't reach"), "no connection: " + LibraryClient.LastError);
				LibraryWindow.Close();
			}
			finally
			{
				LibraryClient.TestAddress = null;
				LibraryClient.Forget();
				if (LibraryWindow.IsOpen) LibraryWindow.Close();
				CleanLibTests();
				foreach (string n in IslandSpawner.ListSavedIslands().Where(n => n.StartsWith("cilib-")).ToList()) DeleteLib(n);
				RemovePoolTestLines();
				try { if (Directory.Exists(TestLibraryFolder)) Directory.Delete(TestLibraryFolder, true); } catch { }
			}
			if (ok) Log("PASS: library browse"); else Fail("library browse");
		}

		[ConsoleCommand(name: "CIMakeMapIsland", docs: "Dev, in a world or the editor (the object catalog built): saves a new island of a map type from a seed under a name, with a title for its arrival banner - for the island library's entries: CIMakeMapIsland <map type>|<seed>|<name>|<title>")]
		public static void MakeMapIslandCommand(string[] args)
		{
			string[] p = string.Join(" ", args ?? new string[0]).Split('|').Select(x => x.Trim()).ToArray();
			int seed;
			if (p.Length < 4 || !int.TryParse(p[1], out seed)) { Fail("CIMakeMapIsland <map type>|<seed>|<name>|<title>"); return; }
			MapType type = MapTypes.Get(p[0]);
			if (type == null) { Fail("no map type '" + p[0] + "'"); return; }
			StartTest(MakeMapIslandRoutine(type, seed, p[2], p[3]));
		}

		static IEnumerator MakeMapIslandRoutine(MapType type, int seed, string name, string title)
		{
			// (without the object catalog the generator places no plants, trees or rocks: a bare island)
			yield return PlaceableCatalog.EnsureBuilt();
			string[] p = { type.Name, seed.ToString(), name, title };
			try
			{
				float elevation;
				IslandGenSettings s = MapTypes.Roll(type, new System.Random(seed), out elevation);
				s.Seed = seed;
				IslandFile file = MapTypes.Create(type, s, elevation, p[2]);
				file.Name = p[2];
				file.Props[IslandProps.Title] = p[3];
				file.Save(IslandSpawner.PathFor(p[2]));
				IslandCache.Forget();
				Log("MADE '" + p[2] + "' (" + type.Name + ", seed " + seed + ", " + file.Objects.Count + " objects, elevation " + elevation.ToString("F0") + ", quest '" + (file.Props.ContainsKey(IslandQuest.KeyTitle) ? file.Props[IslandQuest.KeyTitle] : "") + "')");
				Log("PASS: map island made");
			}
			catch (Exception e) { Fail("making the island: " + e.Message); }
		}

		[ConsoleCommand(name: "CILibraryDownload", docs: "Dev, main menu: downloads entries from the real island library as the window's Download does (each file checked, installed like Import) and checks their files are there; each install logs GOT plan|<plan>|<island;island> or GOT island|<name>|<elevation>: CILibraryDownload <id,id,...>, or CILibraryDownload pick:<plans>,<islands> for that many of each spread over the list by title (installed ones first)")]
		public static void LibraryDownloadCommand(string[] args) { StartTest(LibraryDownloadRoutine(string.Join(" ", args ?? new string[0]).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList())); }

		static IEnumerator LibraryDownloadRoutine(List<string> ids)
		{
			bool ok = true;
			LibraryClient.TestAddress = null;
			yield return LoadList(true);
			Check(ref ok, LibraryClient.Entries != null, "the library's list: " + (LibraryClient.Entries != null ? LibraryClient.Entries.Count + " entries" : LibraryClient.LastError));
			if (LibraryClient.Entries == null) { Fail("library download"); yield break; }
			// (pick:P,I - the library's entries change (Franz's Drive folder since 10-14), so no fixed ids: P plans and I islands spread
			// over its list by title, from those already installed when there are enough - the test's downloads land in the game's mods)
			List<LibraryEntry> chosen = new List<LibraryEntry>();
			if (ids.Count > 0 && ids[0].StartsWith("pick:", StringComparison.OrdinalIgnoreCase))
			{
				int np, ni; int.TryParse(ids[0].Substring(5), out np); int.TryParse(ids.Count > 1 ? ids[1] : "0", out ni);
				Func<string, int, IEnumerable<LibraryEntry>> spread = (kind, count) =>
				{
					List<LibraryEntry> all = LibraryClient.Entries.Where(x => x.Info.kind == kind).OrderBy(x => x.Info.title, StringComparer.OrdinalIgnoreCase).ToList();
					List<LibraryEntry> have = all.Where(x => LibraryClient.StateOf(x) != LibraryClient.State.NotInstalled).ToList();
					if (have.Count >= count) all = have;
					count = Math.Min(count, all.Count);
					return Enumerable.Range(0, count).Select(k => all[(int)((k + 0.5f) * all.Count / count)]).ToList();
				};
				chosen.AddRange(spread(LibraryPack.KindPlan, np));
				chosen.AddRange(spread(LibraryPack.KindIsland, ni));
				Check(ref ok, chosen.Count == np + ni && chosen.Count > 0, "picked " + chosen.Count + " of " + (np + ni) + ": " + string.Join(", ", chosen.Select(x => x.Info.title).ToArray()));
			}
			else foreach (string id in ids)
			{
				LibraryEntry f = LibraryClient.Entries.FirstOrDefault(x => x.Info.id == id);
				if (f == null) Check(ref ok, false, "'" + id + "' is in the library"); else chosen.Add(f);
			}
			foreach (LibraryEntry e in chosen)
			{
				LibraryPack.Report report = null; string error = null; bool finished = false;
				yield return LibraryClient.Download(e, false, false, _ => { }, (r, err) => { report = r; error = err; finished = true; });
				for (float t = 0; !finished && t < 60f; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
				bool files = report != null && report.Islands.All(n => File.Exists(IslandSpawner.PathFor(n))) && (report.PlanName == null || File.Exists(WorldPlan.PathFor(report.PlanName)));
				Check(ref ok, error == null && files && LibraryClient.StateOf(e) == LibraryClient.State.Installed,
					"'" + e.Info.title + "' downloaded and installed: " + (error ?? (report == null ? "no answer in 60 s" : report.Islands.Count + " island(s)" + (report.PlanName != null ? " + the plan '" + report.PlanName + "'" : ""))));
				if (error != null || !files) continue;
				// (for libraryall.ps1: what to try in a world - a plan by its name, an island by its name, under the sea when it stands low)
				if (report.PlanName != null) Log("GOT plan|" + report.PlanName + "|" + string.Join(";", report.Islands.ToArray()));
				else foreach (string n in report.Islands)
				{
					float elevation = 0; try { elevation = IslandFile.Load(IslandSpawner.PathFor(n)).Elevation; } catch { }
					Log("GOT island|" + n + "|" + elevation.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));
				}
			}
			if (ok) Log("PASS: library download"); else Fail("library download");
		}

		[ConsoleCommand(name: "CIIslandPicture", docs: "Dev, in a world: a picture of a spawned island with Raft's own camera from out at sea (Raft's water, no HUD, no held tool): <file>-pic.jpg 1280x720 and <file>-icon.jpg 256x256 in Mods\\DynamicIslands. CIIslandPicture <island>|<file>|<angle 0-360 from north>|<distance x land radius, default 2.2>|<height x land radius, default 0.45>|<aim above the sea x height of the top, default 0.35>")]
		public static void IslandPictureCommand(string[] args) { StartTest(IslandPictureRoutine(string.Join(" ", args ?? new string[0]).Split('|').Select(x => x.Trim()).ToArray())); }

		static IEnumerator IslandPictureRoutine(string[] p)
		{
			if (p.Length < 2) { Fail("CIIslandPicture <island>|<file>|<angle>|<distance>|<height>|<aim>"); yield break; }
			Func<int, float, float> num = (i, d) => { float v; return p.Length > i && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : d; };
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName.Equals(p[0], StringComparison.OrdinalIgnoreCase) && x.Root != null);
			Camera cam = Camera.main;
			if (e == null || cam == null) { Fail("no spawned island '" + p[0] + "' (or no camera)"); yield break; }
			Terrain terrain = e.Root.GetComponentInChildren<Terrain>();
			Vector3 top = terrain != null ? HighestPoint(terrain) : e.Root.transform.position;
			float radius = Mathf.Max(30f, CustomIslandSpawner.LandRadius(e.Name));
			float angle = num(2, 200f) * Mathf.Deg2Rad, dist = num(3, 1.3f) * radius, height = num(4, 0.3f) * radius, aim = num(5, 0.3f);
			Vector3 centre = new Vector3(top.x, e.Root.transform.position.y, top.z);
			float sea = e.Position.y;
			Vector3 pos = new Vector3(centre.x + Mathf.Sin(angle) * dist, Mathf.Max(sea, 0f) + height + Mathf.Max(0f, top.y - sea) * 0.25f, centre.z + Mathf.Cos(angle) * dist);
			Vector3 look = new Vector3(centre.x, sea + (top.y - sea) * aim, centre.z);
			yield return new WaitForEndOfFrame();
			// (the camera's own children - the arms and the held tool - left out of the picture)
			int mask = cam.cullingMask;
			foreach (Renderer r in cam.GetComponentsInChildren<Renderer>(true)) if (r.gameObject.layer != 0) cam.cullingMask &= ~(1 << r.gameObject.layer);
			Vector3 lp = cam.transform.localPosition; Quaternion lr = cam.transform.localRotation;
			float far = cam.farClipPlane;
			// (Raft's haze hides and tints anything a few hundred metres off: off for the picture)
			bool fog = RenderSettings.fog;
			// (and Raft's own haze on the camera - its effects named like fog, scattering or atmosphere)
			var hazes = cam.GetComponents<Behaviour>().Where(b => b != null && b.enabled && System.Text.RegularExpressions.Regex.IsMatch(b.GetType().Name, "Fog|Scatter|Atmos|Haze|Mist", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
			Log("Camera effects: " + string.Join(", ", cam.GetComponents<Behaviour>().Where(b => b != null).Select(b => b.GetType().Name + (b.enabled ? "" : " (off)")).ToArray()) + "; switched off for the picture: " + (hazes.Count == 0 ? "none" : string.Join(", ", hazes.Select(b => b.GetType().Name).ToArray())));
			byte[] pic = null, icon = null;
			try
			{
				cam.transform.position = pos;
				cam.transform.LookAt(look);
				cam.farClipPlane = Mathf.Max(far, dist * 3f);
				RenderSettings.fog = false;
				foreach (Behaviour b in hazes) b.enabled = false;
				pic = RenderCam(cam, 1280, 720).EncodeToJPG(86);
				icon = RenderCam(cam, 256, 256).EncodeToJPG(88);
			}
			finally
			{
				cam.transform.localPosition = lp; cam.transform.localRotation = lr;
				cam.cullingMask = mask; cam.farClipPlane = far;
				RenderSettings.fog = fog;
				foreach (Behaviour b in hazes) if (b != null) b.enabled = true;
			}
			string stem = Path.Combine(DynamicIslands.assetpath, p[1]);
			File.WriteAllBytes(stem + "-pic.jpg", pic);
			File.WriteAllBytes(stem + "-icon.jpg", icon);
			Log("PICTURE " + Path.GetFullPath(stem + "-pic.jpg") + " (from " + dist.ToString("F0") + " m, " + pos.y.ToString("F0") + " m up)");
			Log("PASS: island picture");
		}

		static Texture2D RenderCam(Camera cam, int w, int h)
		{
			var rt = new RenderTexture(w, h, 24);
			RenderTexture before = cam.targetTexture, active = RenderTexture.active;
			cam.targetTexture = rt;
			cam.aspect = w / (float)h;
			bool gfx = GfxShotBegin(); // (CIGfx low: the picture at the user's quality all the same)
			try { cam.Render(); } finally { GfxShotEnd(gfx); }
			cam.targetTexture = before;
			cam.ResetAspect();
			RenderTexture.active = rt;
			var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
			tex.Apply();
			RenderTexture.active = active;
			UnityEngine.Object.Destroy(rt);
			return tex;
		}

		[ConsoleCommand(name: "CIExportEntry", docs: "Dev: exports a library entry through the Share path (LibraryPack.Export) from a JSON file: an info.json plus \"source\" (the island's or plan's name), \"iconFile\" and \"pictureFiles\" (JPG paths). Logs EXPORTED <zip>: CIExportEntry <path to the JSON>")]
		public static void ExportEntryCommand(string[] args)
		{
			string path = string.Join(" ", args ?? new string[0]).Trim();
			try
			{
				var o = LibraryJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
				LibraryInfo info = LibraryInfo.From(o);
				string source = LibraryJson.Str(o, "source");
				byte[] icon = File.ReadAllBytes(LibraryJson.Str(o, "iconFile"));
				var pictures = LibraryJson.Strings(o, "pictureFiles").Select(File.ReadAllBytes).ToList();
				WorldPlan plan = info.IsPlan ? WorldPlan.Load(source) : null;
				if (info.IsPlan && plan == null) { Fail("no plan '" + source + "'"); return; }
				string error;
				string zip = LibraryPack.ExportWith(info, info.IsPlan ? null : source, plan, icon, pictures, out error);
				if (zip == null) { Fail("export: " + error); return; }
				Log("EXPORTED " + Path.GetFullPath(zip));
				Log("PASS: entry exported");
			}
			catch (Exception e) { Fail("export: " + e.Message); }
		}

		[ConsoleCommand(name: "CILibraryOnline", docs: "Dev, main menu: the real island library (its default address, Franz's Google Drive folder): its list reads, an island's icon and picture download, and that island downloads and installs and is removed again (a file of that name that was here before stays)")]
		public static void LibraryOnlineCommand() { StartTest(LibraryOnlineRoutine()); }

		static IEnumerator LibraryOnlineRoutine()
		{
			bool ok = true;
			LibraryClient.TestAddress = null;
			LibraryClient.Forget();
			LibraryWindow.Open(1);
			yield return LoadList(true);
			yield return new WaitForSecondsRealtime(3f);
			// (any island it lists with an icon and a picture, one not installed here if there is one: the library moved to the
			// Drive folder on 10-01, which has neither the old sample plan nor Palm Cove)
			List<LibraryEntry> islands = LibraryClient.Entries == null ? new List<LibraryEntry>() : LibraryClient.Entries.Where(e => e.Info != null && e.Info.kind == LibraryPack.KindIsland && e.Info.icon != "" && e.Info.pictures.Length > 0).ToList();
			Check(ref ok, LibraryClient.Entries != null && LibraryClient.Entries.Count >= 2 && islands.Count > 0,
				"the library's list reads from " + LibraryClient.Address + ": " + (LibraryClient.Entries != null ? LibraryClient.Entries.Count + " entries, " + islands.Count + " islands with an icon and a picture" : LibraryClient.LastError));
			if (islands.Count == 0) { Fail("library online"); yield break; }
			LibraryEntry pick = islands.FirstOrDefault(e => !LibraryPack.Installed().Any(i => i.id == e.Info.id)) ?? islands[0];
			string id = pick.Info.id, title = pick.Info.title;
			bool wasInstalled = LibraryPack.Installed().Any(i => i.id == id);
			LibraryWindow.Select(id);
			yield return new WaitForSecondsRealtime(4f);
			Button row = LibraryWindow.Row(id);
			RawImage icon = row != null ? row.GetComponentsInChildren<RawImage>(true).FirstOrDefault(i => i.name == "Icon") : null;
			Check(ref ok, icon != null && icon.texture != null && LibraryWindow.Picture != null && LibraryWindow.Picture.width >= 320, title + ": its icon and picture download (" + (LibraryWindow.Picture != null ? LibraryWindow.Picture.width + "x" + LibraryWindow.Picture.height : "none") + ")");
			Screenshot(new[] { "library_online" });
			yield return new WaitForSecondsRealtime(0.6f);
			if (wasInstalled) { Log(title + " was installed here already: left as it is (every island of the library is)"); LibraryWindow.Close(); if (ok) Log("PASS: library online"); else Fail("library online"); yield break; }
			bool had = File.Exists(IslandSpawner.PathFor(title));
			string before = had ? LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(title))) : null;
			if (LibraryWindow.Selected == pick && LibraryClient.StateOf(pick) == LibraryClient.State.NotInstalled)
			{
				LibraryWindow.MainButton.onClick.Invoke();
				yield return WaitNotBusy(60f);
			}
			LibraryInstalled inst = LibraryPack.Installed().FirstOrDefault(i => i.id == id);
			Check(ref ok, inst != null && inst.files.Count > 0 && inst.files.All(f => File.Exists(LibraryPack.PathOf(f))), title + " downloads and installs: " + LibraryWindow.Progress);
			LibraryWindow.RemoveButton.onClick.Invoke(); yield return null;
			LibraryWindow.RemoveButton.onClick.Invoke(); yield return null;
			bool stays = !had || (File.Exists(IslandSpawner.PathFor(title)) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(title))) == before);
			Check(ref ok, !LibraryPack.Installed().Any(e => e.id == id) && stays, "removed again" + (had ? " (the " + title + " that was here before stays)" : ""));
			LibraryWindow.Close();
			if (ok) Log("PASS: library online"); else Fail("library online");
		}

		[ConsoleCommand(name: "CILibraryDriveUnit", docs: "Dev, anywhere: the library in a Google Drive folder (LibraryDrive) without the network: folder links read, a folder's public listing parsed (files, folders, names with & and '), a path found folder by folder in a made-up folder tree (names in any case, a missing one named), download addresses with and without an API key")]
		public static void LibraryDriveUnitCommand() { StartTest(LibraryDriveUnitRoutine()); }

		static IEnumerator LibraryDriveUnitRoutine()
		{
			bool ok = true;
			string id;
			Check(ref ok, LibraryDrive.IsDrive("https://drive.google.com/drive/folders/1qXHPud7BAUILVrcEDPJn4FnGggrZdswi?usp=sharing", out id) && id == "1qXHPud7BAUILVrcEDPJn4FnGggrZdswi", "a shared folder link gives its id (" + id + ")");
			Check(ref ok, LibraryDrive.IsDrive("https://drive.google.com/drive/u/0/folders/AbC-123_x", out id) && id == "AbC-123_x" && LibraryDrive.IsDrive("gdrive:AbC", out id) && id == "AbC", "... also a signed-in link and gdrive:<id>");
			Check(ref ok, !LibraryDrive.IsDrive("https://raw.githubusercontent.com/SwedenJohansson/CustomIslands-Library/main/", out id) && !LibraryDrive.IsDrive("file:///D:/lib/", out id), "a web folder or a local copy isn't Drive");
			Check(ref ok, LibraryDrive.IsDrive(LibraryClient.DefaultAddress, out id), "the mod's default library is the Drive folder");
			// A listing as Drive's embedded folder view writes it (one folder, two files)
			string html = "<div class=\"flip-entries\">" +
				"<div class=\"flip-entry\" id=\"entry-F1\" tabindex=\"0\" role=\"link\"><div class=\"flip-entry-info\"><a href=\"https://drive.google.com/drive/folders/F1\" target=\"_blank\"><div class=\"flip-entry-visual\"></div><div class=\"flip-entry-title\">islands</div></a></div><div class=\"flip-entry-last-modified\"><div>11:36 PM</div></div></div>" +
				"<div class=\"flip-entry\" id=\"entry-X9\" tabindex=\"0\" role=\"link\"><div class=\"flip-entry-info\"><a href=\"https://drive.google.com/file/d/X9/view?usp=drive_web\" target=\"_blank\"><div class=\"flip-entry-title\">index.json</div></a></div></div>" +
				"<div class=\"flip-entry\" id=\"entry-Y7\" tabindex=\"0\" role=\"link\"><div class=\"flip-entry-info\"><a href=\"https://drive.google.com/file/d/Y7/view?usp=drive_web\" target=\"_blank\"><div class=\"flip-entry-title\">Bob&#39;s Rock &amp; Cove.island</div></a></div></div></div>";
			List<LibraryDrive.Item> items = LibraryDrive.ParseListing(html);
			Check(ref ok, items.Count == 3 && items[0].Folder && items[0].Id == "F1" && items[0].Name == "islands" && !items[1].Folder && items[1].Id == "X9" && items[1].Name == "index.json",
				"a folder's public listing: a folder and files, with their ids (" + string.Join(", ", items.Select(i => i.Name + "=" + i.Id + (i.Folder ? "/" : "")).ToArray()) + ")");
			Check(ref ok, items.Count == 3 && items[2].Name == "Bob's Rock & Cove.island", "... names with & and ' read as written");
			Check(ref ok, LibraryDrive.ParseListing("<div class=\"flip-entries\"></div>").Count == 0, "an empty folder lists nothing");
			// A made-up folder tree: root -> index.json, islands/ -> palm-cove/ -> icon.jpg
			var tree = new Dictionary<string, List<LibraryDrive.Item>>
			{
				{ "ROOT", new List<LibraryDrive.Item> { new LibraryDrive.Item { Id = "IDX", Name = "index.json" }, new LibraryDrive.Item { Id = "ISL", Name = "islands", Folder = true } } },
				{ "ISL", new List<LibraryDrive.Item> { new LibraryDrive.Item { Id = "PC", Name = "Palm-Cove", Folder = true } } },
				{ "PC", new List<LibraryDrive.Item> { new LibraryDrive.Item { Id = "OLD", Name = "icon.jpg" }, new LibraryDrive.Item { Id = "ICON", Name = "icon.jpg" } } },
			};
			LibraryDrive.TestLister = f => tree.ContainsKey(f) ? tree[f] : null;
			LibraryDrive.Forget();
			try
			{
				string got = null, error = null;
				yield return LibraryDrive.Find("ROOT", "index.json", null, (i, e) => { got = i; error = e; });
				Check(ref ok, got == "IDX", "index.json at the folder's top (" + (got ?? error) + ")");
				yield return LibraryDrive.Find("ROOT", "islands/palm-cove/icon.jpg", null, (i, e) => { got = i; error = e; });
				Check(ref ok, got == "ICON", "a path folder by folder, names in any case; a file there twice: the last listed (" + (got ?? error) + ")");
				yield return LibraryDrive.Find("ROOT", "islands/skyreach/icon.jpg", null, (i, e) => { got = i; error = e; });
				Check(ref ok, got == null && (error ?? "").Contains("islands/skyreach"), "a missing folder is named (" + error + ")");
			}
			finally { LibraryDrive.TestLister = null; LibraryDrive.Forget(); }
			Check(ref ok, LibraryDrive.DownloadUrl("X9", null) == "https://drive.usercontent.google.com/download?id=X9&export=download&confirm=t" &&
				LibraryDrive.DownloadUrl("X9", "K1") == "https://www.googleapis.com/drive/v3/files/X9?alt=media&key=K1", "download addresses without and with an API key");
			if (ok) Log("PASS: library drive unit"); else Fail("library drive unit");
		}

		[ConsoleCommand(name: "CILibraryDriveLive", docs: "Dev, anywhere (online): reads the real library's Google Drive folder (library.txt's address or the default): its listing, and index.json with its entries when it is there - or says the list isn't in the folder yet")]
		public static void LibraryDriveLiveCommand() { StartTest(LibraryDriveLiveRoutine()); }

		static IEnumerator LibraryDriveLiveRoutine()
		{
			string root;
			if (!LibraryDrive.IsDrive(LibraryClient.Address, out root)) { Fail("library drive live: the library address isn't a Google Drive folder (" + LibraryClient.Address + ")"); yield break; }
			LibraryDrive.Forget();
			List<LibraryDrive.Item> items = null; string error = null;
			yield return LibraryDrive.List(root, null, (i, e) => { items = i; error = e; });
			if (items == null) { Fail("library drive live: the folder can't be read (" + error + ")"); yield break; }
			Log("DRIVE folder " + root + ": " + items.Count + " item(s): " + string.Join(", ", items.Select(i => i.Name + (i.Folder ? "/" : "")).ToArray()));
			string loadError = null;
			LibraryClient.Forget();
			yield return LibraryClient.LoadIndex(true, e => loadError = e);
			if (loadError != null) Log("DRIVE list: " + loadError);
			else Log("DRIVE list: " + LibraryClient.Entries.Count + " entries: " + string.Join(", ", LibraryClient.Entries.Select(e => e.Info.title).ToArray()));
			Log("PASS: library drive live");
		}
	}
}
