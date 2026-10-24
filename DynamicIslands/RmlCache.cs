using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// ROADMAP CW2 - the slow start. RML compiles a mod (about a minute for this one) and keeps the result in
	/// %APPDATA%\RaftModLoader\cache\mods\&lt;hash&gt;&lt;date&gt;&lt;name&gt;&lt;Raft version&gt;&lt;cache version&gt;.cachedmod. But every start, before any
	/// mod loads, RML deletes each file there whose name doesn't end with Raft's version + ".cachedmod" - and its own names
	/// end with the cache version after that ("1.1.010.cachedmod") - so the kept copy is always gone and every start
	/// compiles again. That delete is in a try/catch: a read-only file survives it, and RML then loads it in a few seconds.
	/// So once RML has written this mod's file, it is made read-only; this mod's older files (another .rmod) lose it and
	/// go, as RML itself would remove them. A start that compiled is told once, in the main menu: only the first start
	/// after installing or updating is slow. Nothing of the mod waits for this, and when anything fails it only logs.
	/// </summary>
	public static class RmlCache
	{
		/// <summary>RML's file name part for this mod: its name and author, letters and digits only.</summary>
		public const string Key = "CustomIslandsFranzFischer78MegaMatrixs";
		const string Extension = ".cachedmod", NotedFile = "rml_cache_noted.txt";

		/// <summary>This start: RML compiled the mod (its file was written after Raft started); null until known.</summary>
		public static bool? Compiled;
		/// <summary>The kept file's name (tests, the log).</summary>
		public static string Kept;
		public static float StartedAt;

		public static string Folder
		{
			get
			{
				try
				{
					Type t = Type.GetType("HMLLibrary.HLib, HMLCoreLibrary") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HMLLibrary.HLib")).FirstOrDefault(x => x != null);
					FieldInfo f = t != null ? t.GetField("path_cacheFolder_mods", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
					string p = f != null ? f.GetValue(null) as string : null;
					if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
				}
				catch { }
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RaftModLoader", "cache", "mods");
			}
		}

		/// <summary>From the mod's Start (the main menu).</summary>
		public static void Begin(MonoBehaviour host)
		{
			StartedAt = Time.realtimeSinceStartup;
			host.StartCoroutine(Keep());
		}

		static FileInfo[] Ours(string folder)
		{
			return new DirectoryInfo(folder).GetFiles("*" + Extension).Where(f => f.Name.IndexOf(Key, StringComparison.OrdinalIgnoreCase) >= 0)
				.OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
		}

		static IEnumerator Keep()
		{
			DateTime raftStarted;
			try { raftStarted = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(); }
			catch { raftStarted = DateTime.UtcNow.AddSeconds(-Time.realtimeSinceStartup); }
			string folder = Folder;
			// (RML writes its file after the compile, in the background: looked for up to three minutes)
			for (float until = Time.realtimeSinceStartup + 180f; Time.realtimeSinceStartup < until; )
			{
				yield return new WaitForSecondsRealtime(5f);
				FileInfo[] ours;
				try { ours = Directory.Exists(folder) ? Ours(folder) : new FileInfo[0]; }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] RML's cache: " + e.Message); yield break; }
				if (ours.Length == 0) continue;
				FileInfo newest = ours[0];
				// (still being written: the next look)
				if ((DateTime.UtcNow - newest.LastWriteTimeUtc).TotalSeconds < 3) continue;
				Compiled = newest.LastWriteTimeUtc >= raftStarted;
				try
				{
					newest.Attributes |= FileAttributes.ReadOnly;
					Kept = newest.Name;
					foreach (FileInfo old in ours.Skip(1))
					{
						old.Attributes &= ~FileAttributes.ReadOnly;
						old.Delete();
						Debug.Log("[CUSTOM ISLANDS] RML's cache: an older copy of this mod is removed: " + old.Name);
					}
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] RML's cache: keeping " + newest.Name + ": " + e.Message); }
				Debug.Log("[CUSTOM ISLANDS] Started " + StartedAt.ToString("0") + " s after Raft; RML " + (Compiled == true ? "compiled the mod this start" : "loaded the mod from its cache") + " (" + newest.Name + " kept)");
				if (Compiled == true) Tell(newest.Name);
				yield break;
			}
			Debug.Log("[CUSTOM ISLANDS] RML's cache: no file of this mod in " + folder + " - every start compiles it");
		}

		/// <summary>The compile start is told once per file (a cache that can't be kept isn't told again every start).</summary>
		static void Tell(string name)
		{
			string noted = Path.Combine(DynamicIslands.assetpath, NotedFile);
			try { if (File.Exists(noted) && File.ReadAllText(noted).Trim() == name) return; File.WriteAllText(noted, name); }
			catch { }
			DynamicIslands.Notify(Note, false, 15);
		}

		/// <summary>Raft's notification shows two lines (about 80 letters): the guide says the rest.</summary>
		public const string Note = "Custom Islands is ready. Only the first start after an update is this slow.";
	}
}
