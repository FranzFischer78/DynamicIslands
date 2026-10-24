using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of the known issues fixed 2026-10-23 (GUIDE section 12): a world's island put back from the deleted folder,
	/// the mod's own Harmony id, decimal commas in the settings files.
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIKnownIssues", docs: "Dev (KI1-KI3), anywhere: a missing island put back from the deleted folder (by hash, newest by name, never a wrong hash), the mod's own Harmony id, decimal commas")]
		public static void KnownIssuesCommand()
		{
			bool ok = true;
			string name = "ci_ki_restore";
			string file = IslandSpawner.PathFor(name);
			string deleted = Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName);
			string sub = Path.Combine(Path.Combine(deleted, LibraryPack.KeptVersionsFolder), name + " ci" + IslandFile.Extension);
			string flat = Path.Combine(deleted, name + IslandFile.Extension);
			string from = Directory.GetFiles(DynamicIslands.assetpath, "*" + IslandFile.Extension).FirstOrDefault(f => new FileInfo(f).Length < 4 * 1024 * 1024 && !Path.GetFileName(f).StartsWith("ci", StringComparison.OrdinalIgnoreCase));
			Check(ref ok, from != null, "an island file to copy: " + (from ?? "none"));
			if (from == null) { Fail("known issues"); return; }
			try
			{
				// KI1. Two copies in the deleted folder: an older one (other content) flat, the world's one in a subfolder
				Directory.CreateDirectory(Path.GetDirectoryName(sub));
				byte[] world = File.ReadAllBytes(from);
				byte[] other = world.Concat(new byte[] { 0 }).ToArray();
				File.WriteAllBytes(sub, world); File.SetLastWriteTimeUtc(sub, DateTime.UtcNow.AddDays(-2));
				File.WriteAllBytes(flat, other);
				string hash = IslandNetwork.Hash(world);
				if (File.Exists(file)) File.Delete(file);
				Check(ref ok, IslandFilesWindow.RestoreFromDeleted(name, hash) && File.Exists(file) && IslandNetwork.Hash(File.ReadAllBytes(file)) == hash, "by the world's hash: the matching copy came back (from the subfolder, not the newer one)");
				Check(ref ok, File.Exists(sub) && File.Exists(flat), "the copies in the deleted folder stay");
				Check(ref ok, !IslandFilesWindow.RestoreFromDeleted(name, hash), "a file that is there is left alone");
				File.Delete(file);
				Check(ref ok, !IslandFilesWindow.RestoreFromDeleted(name, "0123456789ab") && !File.Exists(file), "a hash no copy has: nothing put back");
				Check(ref ok, IslandFilesWindow.RestoreFromDeleted(name, null) && File.Exists(file) && IslandNetwork.Hash(File.ReadAllBytes(file)) == IslandNetwork.Hash(other), "no hash recorded: the newest copy of the name");
				File.Delete(file);
				Check(ref ok, !IslandFilesWindow.RestoreFromDeleted("ci_ki_nothing", null), "no copy at all: nothing");

				// KI2. The mod's own Harmony id, not the original mod's
				Check(ref ok, Harmony.HasAnyPatches(DynamicIslands.HarmonyId) && DynamicIslands.HarmonyId != "com.franzfischer.customislands", "patches under the mod's own id " + DynamicIslands.HarmonyId);
				Check(ref ok, !Harmony.HasAnyPatches("com.franzfischer.customislands") && !PcCheck.Problems().Any(p => p.Contains("original Custom Islands")), "no original mod here: no warning");

				// KI3. Decimal commas
				float a, b, c;
				Check(ref ok, CustomIslandSpawner.ParseNumber("0,5", out a) && Mathf.Approximately(a, 0.5f) && CustomIslandSpawner.ParseNumber("0.25", out b) && Mathf.Approximately(b, 0.25f) && !CustomIslandSpawner.ParseNumber("1,000,5", out c),
					"'0,5' reads 0.5, '0.25' reads 0.25, '1,000,5' is refused");
			}
			catch (Exception e) { Check(ref ok, false, "error: " + e); }
			finally
			{
				foreach (string f in new[] { file, sub, flat }) try { if (File.Exists(f)) File.Delete(f); } catch { }
			}
			if (ok) Log("PASS: known issues"); else Fail("known issues");
		}
	}
}
