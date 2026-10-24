using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using DynamicIslands.Editor;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Tests: Raft's scene scripts kept quiet while the generator loads their scenes (CIGenBuildingsQuiet), and the mod's
	/// own clickable error note with its details box and errors.txt (CIErrorNote).
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIGenBuildingsQuiet", docs: "Dev, anywhere: generating with buildings and caves on (several styles, sizes, Levels, every story remake) loads Raft's island scenes without one exception or error - the core catalogue's scenes and the story islands' (CoolingChallenge, OlofFinalRoom kept quiet); says whether the quiet patch applied")]
		public static void GenBuildingsQuietCommand() { StartTest(GenBuildingsQuietRoutine()); }

		static IEnumerator GenBuildingsQuietRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			if (!PlaceableCatalog.IsBuilt) { Fail("CIGenBuildingsQuiet: the object catalog isn't built"); yield break; }
			bool ok = true;
			int problems = 0; var firstProblems = new List<string>();
			// (Unity's own "Screen position out of view frustum" is a camera drawing while Raft is minimised, as the tests run it - not the mod's)
			Application.LogCallback counter = (msg, trace, type) =>
			{
				if (type == LogType.Exception || (type == LogType.Error && !msg.StartsWith("[CITEST]") && !msg.StartsWith("Screen position out of view frustum")))
				{
					problems++;
					if (firstProblems.Count < 5) firstProblems.Add(msg.Split('\n')[0] + " @ " + (trace ?? "").Split('\n').FirstOrDefault());
				}
			};
			int quietBefore = PlaceableCatalog.Quieted;
			Log("quiet patch: " + (PlaceableCatalog.QuietPatched.Count > 0 ? string.Join(", ", PlaceableCatalog.QuietPatched.ToArray()) : "NOT applied (no such scripts in this Raft?)"));
			Check(ref ok, PlaceableCatalog.QuietPatched.Any(m => m.StartsWith("CoolingChallenge.")) && PlaceableCatalog.QuietPatched.Any(m => m.StartsWith("OlofFinalRoom.")), "the quiet patch applied to CoolingChallenge and OlofFinalRoom");

			Func<int, int, float, bool, IslandGenSettings> make = (seed, style, radius, levels) => new IslandGenSettings
			{
				Seed = seed, Style = style, Radius = radius, Height = 14f + radius * 0.12f, Roughness = 0.35f, Peaks = 2, ObjectDensity = 0.3f,
				Buildings = true, BuildingKind = GenBuildings.Mixed, BuildingCount = 8, Caves = true, QuestSteps = 2,
			};
			var cases = new List<IslandGenSettings>
			{
				make(5101, TerrainPainter.Tropical, 60f, false), make(5102, TerrainPainter.Snowy, 110f, true), make(5103, TerrainPainter.Desert, 160f, false),
				make(5104, TerrainPainter.Forest, 110f, true), make(5105, TerrainPainter.Volcanic, 60f, true), make(5106, TerrainPainter.Tropical, 160f, true),
			};
			IslandGenSettings landmarks = make(5107, TerrainPainter.Tropical, 130f, false);
			landmarks.BuildingKind = GenBuildings.Landmarks; landmarks.BuildingCount = 25;
			cases.Add(landmarks);
			// (every story island's remake: their kits come from Pilot, the boat, Temperance, Caravan Town, Varuna Point, Utopia...)
			int n = 0;
			foreach (Remakes.Design d in Remakes.All)
			{
				IslandGenSettings s = make(5200 + n, n % 2 == 0 ? TerrainPainter.Tropical : TerrainPainter.Snowy, n % 3 == 0 ? 160f : 110f, n % 2 == 1);
				s.Remake = d.Id;
				cases.Add(s);
				n++;
			}
			List<string> names = cases.SelectMany(s => GenBuildings.NeededNames(s)).Distinct().ToList();

			Application.logMessageReceived += counter;
			try
			{
				// Each scene loaded again as the mod loads it (they stay loaded for the session once taken from: a fresh load here)
				var visited = new List<string>();
				yield return PlaceableCatalog.ReopenForTest(names, v => visited.Add(v));
				Log("loaded and unloaded " + visited.Count + " of Raft's scenes: " + string.Join(", ", visited.ToArray()));
				yield return PlaceableCatalog.EnsureLoaded(names);
				int i = 0;
				foreach (IslandGenSettings s in cases)
				{
					IslandFile f = null;
					try { f = IslandGenerator.CreateFile(s, "ci-quiet-" + i); }
					catch (Exception e) { problems++; if (firstProblems.Count < 5) firstProblems.Add("generating case " + i + ": " + e.Message); }
					if (f != null) Log("  case " + i + " (style " + s.Style + ", radius " + s.Radius + (s.Remake.Length > 0 ? ", " + s.Remake : "") + "): " + f.Objects.Count + " objects");
					i++;
					yield return null;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			finally { Application.logMessageReceived -= counter; }
			Log("Raft's story scripts kept quiet: " + (PlaceableCatalog.Quieted - quietBefore) + " time(s)");
			Check(ref ok, problems == 0, problems + " exceptions/errors while loading Raft's scenes and generating " + cases.Count + " islands" + (firstProblems.Count > 0 ? ": " + string.Join(" | ", firstProblems.ToArray()) : ""));
			if (ok) Log("PASS: generator buildings quiet"); else Fail("generator buildings quiet");
		}

		[ConsoleCommand(name: "CIErrorNote", docs: "Dev, anywhere: a test exception thrown in the mod's code shows the clickable error note; its box has the message, whose code it was, the stack, Copy, Open the log folder and where to report; errors.txt gets it; Unity's frustum message and Raft's own errors outside the mod aren't noted. CIErrorNote log: through Unity's log (the note then also shows in Player.log)")]
		public static void ErrorNoteCommand(string[] args) { StartTest(ErrorNoteRoutine(args != null && args.Contains("log"))); }

		static IEnumerator ErrorNoteRoutine(bool viaLog)
		{
			bool ok = true;
			string token = "CITEST test exception " + Guid.NewGuid().ToString("N").Substring(0, 8);
			bool testMode = HelpLinks.TestMode;
			string clipboard = GUIUtility.systemCopyBuffer;
			HelpLinks.TestMode = true;
			try
			{
				// Thrown for real in the mod's code (this method), then given to the note as Unity gives it an exception
				try { throw new InvalidOperationException(token); }
				catch (Exception e)
				{
					if (viaLog) Debug.LogException(e);
					else ErrorNotes.OnLog(e.GetType().Name + ": " + e.Message, e.StackTrace, LogType.Exception);
				}
				yield return null;
				yield return null;
				ErrorNotes.Record r = ErrorNotes.Last;
				Check(ref ok, r != null && r.Message.Contains(token) && r.Whose == "Custom Islands", "noted: " + (r != null ? "'" + r.Message.Split('\n')[0] + "', whose: " + r.Whose : "nothing"));
				Check(ref ok, ErrorNotes.NoteShown && ErrorNotes.NoteText.StartsWith(ErrorNotes.NoteLine), "the note shows: '" + ErrorNotes.NoteText + "'");

				ErrorNotes.OpenDetails();
				yield return null;
				string body = InfoWindow.Body;
				Check(ref ok, InfoWindow.IsOpen && body.Contains(token) && body.Contains("Custom Islands") && body.Contains("ErrorNoteRoutine") && body.Contains(HelpLinks.Discord) && body.Contains(HelpLinks.Issues),
					"its box: the message, whose code, the stack and where to report (" + body.Length + " characters)");
				Check(ref ok, new[] { "Copy", "Open the log folder", "Report on GitHub", "Post on Discord", "Close" }.All(l => InfoWindow.ButtonLabels.Contains(l)), "its buttons: " + string.Join(", ", InfoWindow.ButtonLabels.ToArray()));
				UnityEngine.UI.Button copy = InfoWindow.ButtonNamed("Copy"), folder = InfoWindow.ButtonNamed("Open the log folder");
				if (copy != null) copy.onClick.Invoke();
				Check(ref ok, GUIUtility.systemCopyBuffer.Contains(token) && GUIUtility.systemCopyBuffer.Contains(HelpLinks.Versions), "Copy put the error and the versions on the clipboard");
				if (folder != null) folder.onClick.Invoke();
				Check(ref ok, HelpLinks.LastOpened == HelpLinks.LogFolder, "Open the log folder opens " + HelpLinks.LastOpened);
				InfoWindow.Close();

				string file = "";
				try { file = File.Exists(ErrorNotes.FilePath) ? File.ReadAllText(ErrorNotes.FilePath) : ""; } catch (Exception e) { file = "(unreadable: " + e.Message + ")"; }
				Check(ref ok, file.Contains(token) && file.Contains("| " + HelpLinks.Versions + " | Custom Islands") && new FileInfo(ErrorNotes.FilePath).Length <= ErrorNotes.MaxFileBytes, "errors.txt has it with the time and versions (" + (file.Length / 1024) + " KB)");

				// Not noted: Unity's frustum message, and Raft's own errors while the mod isn't doing anything
				int count = ErrorNotes.Records.Sum(x => x.Count);
				ErrorNotes.OnLog("Screen position out of view frustum (screen pos 0, 0, 0)", "DynamicIslands.Editor.Test.Method ()", LogType.Exception);
				if (PlaceableCatalog.QuietSceneNames.Length == 0) ErrorNotes.OnLog("NullReferenceException: CITEST Raft's own", "CoolingChallenge.Awake () (at <00000000000000000000000000000000>:0)", LogType.Exception);
				Check(ref ok, ErrorNotes.Records.Sum(x => x.Count) == count, "the frustum message and Raft's own error outside the mod aren't noted");
				// Whose code, from the stack
				string another = ErrorNotes.WhoseFor("Exception: x", "SomeOtherMod.Patches.Prefix ()\nDynamicIslands.Editor.IslandGenerator.CreateFile (IslandGenSettings s, string name)", LogType.Exception);
				string raft = ErrorNotes.WhoseFor("NullReferenceException: x", "  at WorldManager.Spawn () [0x00000]\n  at DynamicIslands.DynamicIslands.Update () [0x00000]", LogType.Exception);
				Check(ref ok, another == "another mod (SomeOtherMod) (called by Custom Islands)" && raft == "Raft (called by Custom Islands)", "whose code: '" + another + "', '" + raft + "'");
			}
			finally
			{
				HelpLinks.TestMode = testMode;
				GUIUtility.systemCopyBuffer = clipboard;
				ErrorNotes.HideNote();
			}
			if (ok) Log("PASS: error note"); else Fail("error note");
		}
	}
}
