using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A start check of the player's PC (ROADMAP AU45): whether Mods\DynamicIslands can be written (a test file), whether
	/// Raft sits under Program Files or in a synced folder (OneDrive, Dropbox, Google Drive), whether files of an
	/// unzipped .rmod are there (modinfo.json, .cs files), and which
	/// parts of the mod failed to start (AU11). One box on the main menu naming each problem and its fix, once per start.
	/// </summary>
	public static class PcCheck
	{
		static bool told;

		/// <summary>What is wrong, each with what to do (empty = all well).</summary>
		public static List<string> Problems() { return Problems(DynamicIslands.assetpath); }

		/// <summary>The same for another folder as the mod's own (the tests feed fake paths).</summary>
		internal static List<string> Problems(string modFolder)
		{
			var found = new List<string>();
			string folder = Path.GetFullPath(modFolder);
			try
			{
				Directory.CreateDirectory(folder);
				string probe = Path.Combine(folder, ".write_test");
				File.WriteAllText(probe, "ok");
				File.Delete(probe);
			}
			catch (Exception e)
			{
				found.Add("The mod can't save in " + folder + " (" + e.GetType().Name + "): islands, plans and the worlds' islands can't be kept. Move Raft out of Program Files, or let Raft write there in your antivirus (\"controlled folder access\").");
			}
			string lower = folder.ToLowerInvariant();
			// (only when saving failed: Steam's own default folder is under Program Files and works - review 2026-10-06)
			if (found.Count > 0 && lower.Contains("program files"))
				found.Add("Raft is installed under Program Files (" + folder + "). Windows may stop the mod from saving there: in Steam, move Raft to another library folder (Steam > Settings > Storage).");
			foreach (string sync in new[] { "onedrive", "dropbox", "google drive", "icloud" })
				if (lower.Contains(sync)) { found.Add("Raft is in a synced folder (" + sync + "): the sync can lock a file while the mod saves it. Move Raft out of it, or pause the sync while you play."); break; }
			try
			{
				var unzipped = new List<string>();
				// (the mod reads its own files from the .rmod now - AU38 - so these do no harm; they only show that the .rmod was
				// unzipped into the folder, which the guide asks not to do)
				string modinfo = Path.Combine(folder, "modinfo.json");
				if (File.Exists(modinfo) && !SameAsShipped(modinfo)) unzipped.Add("modinfo.json");
				unzipped.AddRange(Directory.GetFiles(folder, "*.cs").Select(Path.GetFileName).Take(3));
				if (unzipped.Count > 0)
					found.Add("Files of an unzipped .rmod are in " + folder + " (" + string.Join(", ", unzipped.ToArray()) + "...). Put the .rmod file itself into Raft's mods folder, and delete modinfo.json, the .cs files and raft_*.txt from " + folder + " (keep your islands, plans and settings).");
			}
			catch { }
			try
			{
				// (Franz's original Custom Islands mod loaded too: both change the same parts of Raft and fight over them)
				// (by its Harmony id: an F9 reload leaves this mod's older copies in memory, so a look for the class would be wrong)
				if (HarmonyLib.Harmony.HasAnyPatches("com.franzfischer.customislands"))
					found.Add("The original Custom Islands mod (an older version of this one) is loaded too. Both change the same parts of Raft: keep only this one - remove the other .rmod from Raft's mods folder and restart Raft.");
			}
			catch { }
			if (DynamicIslands.StartFailures.Count > 0)
				found.Add("These parts of the mod didn't start: " + string.Join(", ", DynamicIslands.StartFailures.ToArray()) + " (the log, F10, says why). Restart Raft; if it stays, please report it.");
			return found;
		}

		static bool SameAsShipped(string file)
		{
			try
			{
				byte[] shipped = DynamicIslands.instance != null ? DynamicIslands.instance.GetEmbeddedFileBytes(Path.GetFileName(file)) : null;
				return shipped != null && shipped.Length > 0 && File.ReadAllBytes(file).SequenceEqual(shipped);
			}
			catch { return false; }
		}

		/// <summary>The main menu appeared: once per Raft start, the box when something is wrong.</summary>
		public static void ShowIfProblems()
		{
			// (another box is up - the experimental notice, the parts that are off: this one comes the next time the menu shows)
			if (told || InfoWindow.IsOpen) return;
			told = true;
			List<string> found;
			try { found = Problems(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] PC check: " + e.Message); return; }
			if (found.Count == 0) return;
			foreach (string f in found) Debug.LogWarning("[CUSTOM ISLANDS] PC check: " + f);
			InfoWindow.Open("Custom Islands: something to fix on this PC",
				string.Join("\n\n", found.Select(f => "•  " + f).ToArray()),
				new InfoWindow.Choice("Report a problem", InfoWindow.OpenReport, "How to report it, on GitHub or Discord", true),
				new InfoWindow.Choice("Close", InfoWindow.Close, "Close this box (Esc)"));
		}
	}
}
