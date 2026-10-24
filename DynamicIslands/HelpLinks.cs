using System;
using System.IO;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Where players get help: the guide (the PDF that comes in the .rmod, or the guide online), the mod's Discord, its
	/// GitHub issues and Raft's log folder - for the main menu's alpha box, the Report a problem box and the Help buttons
	/// of the editor's windows.
	/// </summary>
	public static class HelpLinks
	{
		public const string Discord = "https://discord.gg/U7DfKY9tN";
		public const string Repo = "https://github.com/SwedenJohansson/DynamicIslands";
		public const string Issues = Repo + "/issues";
		public const string GuideOnline = Repo + "/blob/master/docs/GUIDE.md";
		public const string GuidePdfOnline = Repo + "/blob/master/docs/Custom-Islands-Guide.pdf";
		/// <summary>The guide as a PDF: in the .rmod (pack.ps1 puts it there), written to Mods\DynamicIslands\ to open it.</summary>
		public const string PdfName = "Custom-Islands-Guide.pdf";

		/// <summary>Tests: what would have been opened (a web address, a file or a folder), without opening it.</summary>
		public static bool TestMode;
		/// <summary>This Raft is driven by the tests' command file (dev builds): nothing opens outside Raft - no browser, PDF
		/// reader or Explorer window on the screen of someone using the PC while the tests run. Links only note what they'd open.</summary>
		public static bool Automated;
		static bool NotOpened { get { return TestMode || Automated; } }
		public static string LastOpened;

		/// <summary>A web address or a file in the player's browser or PDF reader.</summary>
		public static void Open(string url)
		{
			LastOpened = url;
			Debug.Log("[CUSTOM ISLANDS] Opening " + url + (NotOpened ? " (test: not opened)" : ""));
			if (NotOpened) return;
			try { Application.OpenURL(url); }
			catch (Exception e) { DynamicIslands.Notify("Could not open " + url + ": " + e.Message, true); }
		}

		/// <summary>A section of the guide online (anchor as in GUIDE.md's contents, e.g. 72-your-first-world-plan-step-by-step).</summary>
		public static void OpenGuideSection(string anchor)
		{
			Open(GuideOnline + (string.IsNullOrEmpty(anchor) ? "" : "#" + anchor));
		}

		/// <summary>The guide's PDF of this version of the mod (from the .rmod), or the one online when the .rmod has none.</summary>
		public static void OpenGuide()
		{
			string path = GuidePdfPath();
			Open(path != null ? new Uri(Path.GetFullPath(path)).AbsoluteUri : GuidePdfOnline);
		}

		/// <summary>The PDF written out of the .rmod to Mods\DynamicIslands\ (again when it differs), or null.</summary>
		public static string GuidePdfPath()
		{
			try
			{
				var files = DynamicIslands.instance != null ? DynamicIslands.instance.modlistEntry.modinfo.modFiles : null;
				byte[] pdf;
				if (files == null || !files.TryGetValue(PdfName, out pdf) || pdf == null || pdf.Length == 0) return null;
				string path = Path.Combine(DynamicIslands.assetpath, PdfName);
				if (!File.Exists(path) || new FileInfo(path).Length != pdf.Length)
				{
					Directory.CreateDirectory(DynamicIslands.assetpath);
					SafeFile.WriteAllBytes(path, pdf); // (in one step: a PDF cut short isn't left for the reader - AU41)
				}
				return path;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not write the guide's PDF: " + e.Message); return null; }
		}

		/// <summary>Raft's log folder (Player.log, Player-prev.log): %USERPROFILE%\AppData\LocalLow\Redbeet Interactive\Raft.</summary>
		public static string LogFolder { get { return Application.persistentDataPath.Replace('/', '\\'); } }

		/// <summary>A folder in Windows' Explorer (select: that file in it); tests only note it.</summary>
		public static bool OpenFolder(string path, bool select = false)
		{
			LastOpened = path;
			Debug.Log("[CUSTOM ISLANDS] Opening " + path + (NotOpened ? " (test: not opened)" : ""));
			if (NotOpened) return true;
			System.Diagnostics.Process.Start("explorer.exe", (select ? "/select," : "") + "\"" + path + "\"");
			return true;
		}

		public static void OpenLogFolder()
		{
			string folder = LogFolder;
			LastOpened = folder;
			Debug.Log("[CUSTOM ISLANDS] Opening " + folder + (NotOpened ? " (test: not opened)" : ""));
			if (NotOpened) return;
			try { System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\""); }
			catch (Exception e) { DynamicIslands.Notify("Could not open " + folder + ": " + e.Message, true); }
		}

		/// <summary>The versions a report needs: the mod's and Raft's.</summary>
		public static string Versions { get { return "Custom Islands " + ExperimentalNotice.Version + ", Raft " + Application.version; } }

		/// <summary>The form to fill in (the guide's section 14), with the versions filled in.</summary>
		public static string ReportTemplate
		{
			get
			{
				return "What happened:\n" +
					"Steps to make it happen:\n  1.\n  2.\n  3.\n" +
					"What I expected:\n" +
					"How often: every time / now and then / once\n" +
					"Versions: " + Versions + ", mod loader:, other mods:\n" +
					"Single player or together: (host / joined, how many players)\n" +
					"World settings: (what WorldOptions, Monsters, BuildCost, Randomizer, WorldPlan, WorldIslands, SpawnPool print)\n" +
					"Islands and world plans used: (names, files or download links)\n" +
					"Attached: Player.log, Player-prev.log, screenshots, ...\n";
			}
		}

		/// <summary>A new GitHub issue with the form in it (GitHub asks the player to sign in first).</summary>
		public static string NewIssueUrl { get { return Issues + "/new?body=" + Uri.EscapeDataString(ReportTemplate); } }

		public static void CopyReportTemplate()
		{
			GUIUtility.systemCopyBuffer = ReportTemplate;
		}
	}
}
