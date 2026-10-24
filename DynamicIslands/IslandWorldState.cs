using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Which custom islands exist in the current world, so they come back when the world is loaded again.
	/// Stored next to the island files as Mods\DynamicIslands\worlds\&lt;world guid&gt;.txt (one "name|x|y|z|object state" per line,
	/// plus "@auto=on|off" for automatic spawning), written whenever Raft saves the world and read when a world
	/// finishes loading. The host's list is the real one; clients hold a copy sent by the host (IslandNetwork).
	/// An entry's GameObjects exist only while the raft is near it: CustomIslandSpawner unloads and reloads them.
	/// </summary>
	public static class IslandWorldState
	{
		public class Entry
		{
			/// <summary>Given by the host for this session; clients use the host's ids.</summary>
			public int Id;
			/// <summary>Island file (without extension) this machine spawns from.</summary>
			public string Name;
			/// <summary>Client: the island's name on the host, and its file's content hash.</summary>
			public string HostName;
			public string Hash;
			/// <summary>Client: the island file is still coming from the host.</summary>
			public bool WaitingForFile;
			/// <summary>Host: its file isn't on this PC and the world plays without it (MissingIslandsCheck) - not loaded, no
			/// Receiver dot, kept in the world file; cleared when the file comes.</summary>
			public bool Excluded;
			/// <summary>Host: the fields after the eighth of its world file line (a newer version's), written back as they were (AU5).</summary>
			public string Extra;
			public Vector3 Position;
			/// <summary>The spawned island, or null while it is unloaded (far away) or still loading.</summary>
			public GameObject Root;
			public bool Loading;
			/// <summary>The island file is missing or broken: don't keep trying to load it.</summary>
			public bool Failed;
			/// <summary>Harvested trees and picked-up items, by object ordinal (IslandObjectState).</summary>
			public Dictionary<int, ObjectState> State = new Dictionary<int, ObjectState>();
			/// <summary>Client: trees and pickups the host said have grown back while the island was loaded here; they show
			/// when it loads here again (IslandObjectState), and meanwhile its unloading doesn't record them as used.</summary>
			public readonly HashSet<int> Regrown = new HashSet<int>();
			/// <summary>Id of the rule that brought the island (WorldDirector; other rules refer to it), or "".</summary>
			public string Rule = "";
			/// <summary>Its name on the Receiver ("" = just the distance).</summary>
			public string Label = "";
		}

		static readonly List<Entry> islands = new List<Entry>();
		static string loadedFor; // world guid the list belongs to

		public static IList<Entry> Islands { get { return islands; } }

		static string WorldKey { get { return SaveAndLoad.WorldGuid.ToString(); } }
		static string FilePath { get { return Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), WorldKey + ".txt"); } }
		/// <summary>The world file of the world being played (WorldCopy).</summary>
		public static string WorldFilePath { get { return FilePath; } }

		/// <summary>Host: adds a new island to the world's list and tells clients about it (unless broadcast is off: the caller does it later).</summary>
		public static Entry Add(string name, Vector3 position, GameObject root, bool broadcast = true)
		{
			EnsureCurrentWorld();
			var entry = new Entry { Id = IslandNetwork.NewId(), Name = name, HostName = name, Position = position, Root = root };
			islands.Add(entry);
			if (broadcast) IslandNetwork.BroadcastAdded(entry);
			return entry;
		}

		/// <summary>Client: an island the host told us about.</summary>
		public static Entry AddRemote(int id, string hostName, string hash, Vector3 position)
		{
			var entry = new Entry { Id = id, Name = hostName, HostName = hostName, Hash = hash, Position = position };
			islands.Add(entry);
			return entry;
		}

		public static bool Contains(Entry entry) { return islands.Contains(entry); }

		/// <summary>Host: removes (and destroys) islands with this name, or all islands when name is null, and tells clients. Returns how many.</summary>
		public static int Remove(string name)
		{
			EnsureCurrentWorld();
			int[] ids = islands.Where(e => name == null || e.HostName.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToArray();
			return RemoveIds(ids, true);
		}

		/// <summary>Removes (and destroys) the islands with these ids; the host also tells clients when broadcast is set.</summary>
		public static int RemoveIds(IList<int> ids, bool broadcast)
		{
			if (ids == null) return 0;
			List<Entry> gone = islands.Where(e => ids.Contains(e.Id)).ToList();
			foreach (Entry e in gone)
			{
				IslandSpawner.Despawn(e.Root);
				islands.Remove(e);
			}
			if (broadcast) IslandNetwork.BroadcastRemoved(gone.Select(e => e.Id));
			// (host, islands taken out on purpose - RemoveIsland, the randomizer: a story step's island comes again, AU50. Not
			// a generated island that failed: brought again it would fail again every second)
			if (broadcast && gone.Count > 0 && Raft_Network.IsHost)
				try { StoryChain.OnIslandsRemoved(gone); } catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] [story chain] An island removed: " + ex.Message); }
			return gone.Count;
		}

		/// <summary>The list is this world's: false while another world loads, until OnWorldLoaded reads the new one (the list
		/// in memory is still the world played before - streaming it then brought that world's islands into the new one).</summary>
		public static bool ForThisWorld { get { return loadedFor == WorldKey; } }

		static void EnsureCurrentWorld()
		{
			if (loadedFor != WorldKey) { islands.Clear(); loadedFor = WorldKey; }
		}

		/// <summary>
		/// Raft keeps the raft near the origin: when it drifts more than a chunk away, the host shifts the whole
		/// world back (everything does position -= shift, clients get the same shift). Custom islands must follow,
		/// on every machine, and the saved positions follow too so they stay in Raft's coordinate frame.
		/// </summary>
		/// <summary>Every world shift added up (this session): a spawn without a world entry follows the ones made while it loaded.</summary>
		public static Vector3 ShiftedBy { get; private set; }

		public static void OnWorldShift(Vector3 shift)
		{
			ShiftedBy += shift;
			foreach (GameObject root in IslandSpawner.SpawnedRoots)
				if (root != null) root.transform.position -= shift;
			IslandSpawner.SpawnedRoots.RemoveAll(r => r == null);
			foreach (Entry e in islands) e.Position -= shift;
			CustomIslandSpawner.OnWorldShift(shift);
			PlayerHold.OnWorldShift(shift);
			PlayerPlaces.OnWorldShift(shift);
			WorldRandomizer.OnWorldShift(shift);
			if (islands.Count > 0) Debug.Log("[CUSTOM ISLANDS] World shift by " + shift.ToString("F0") + ": " + islands.Count + " island(s) moved with it");
		}

		/// <summary>Writes the list for the current world (called after Raft saves the world).</summary>
		public static void Save()
		{
			if (!Raft_Network.IsHost || SaveAndLoad.WorldGuid == Guid.Empty) return;
			// Not set up for this world yet (Raft saves a brand-new world at once, before the mod notices it): what is in
			// memory - the plan, km sailed, the story book - still belongs to the world played before, and writing it
			// here gave the new world that world's journal and km, and lost the plan chosen for it (two-player test).
			if (loadedFor != WorldKey) return;
			// (the world's file couldn't be read: saving what little was read would write over the good file - AU12)
			if (loadFailed)
			{
				if (!loadFailedTold) { loadFailedTold = true; DynamicIslands.Notify("This world's custom islands couldn't be read (see the log, F10): they aren't saved, so their file stays as it was. Load the world again.", true); }
				Debug.LogWarning("[CUSTOM ISLANDS] Not saving the world's custom islands: its file couldn't be read when it loaded");
				return;
			}
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				// (not while it has lines this version couldn't read - a newer version's settings: deleting it lost them)
				if (islands.Count == 0 && keptLines.Count == 0 && CustomIslandSpawner.Enabled && !WorldDirector.HasState && !StoryBook.HasState && !WorldRandomizer.HasState && !WorldRules.HasState && !PlayerLevels.HasState && !WorldOptions.HasState && !WorldIslands.HasState && !StoryChain.HasState) { if (File.Exists(FilePath)) File.Delete(FilePath); WorldCopy.AfterDelete(); return; }
				var lines = new List<string>
				{
					"# Custom islands in world '" + SaveAndLoad.CurrentGameFileName + "': name|x|y|z|used objects (ordinal,active,yield left,day;...)|rule|receiver label|island file hash",
					"@auto=" + (CustomIslandSpawner.Enabled ? "on" : "off")
				};
				// (the clock, the save counter, and Raft's save it goes with - or, between saves, the one it follows - WorldCopy)
				lines.InsertRange(1, WorldCopy.StampLines());
				// (which PC hosted it: Tidy up moves a world's file only when this PC hosted it and its Raft world is gone -
				// a player's copy of a world they joined names the host)
				if (Housekeeping.SavedByLine() != null) lines.Add(Housekeeping.SavedByLine());
				lines.AddRange(WorldDirector.WriteLines());
					lines.AddRange(WorldRandomizer.WriteLines());
				lines.AddRange(StoryBook.WriteLines());
				lines.AddRange(PlayerPlaces.WriteLines());
				lines.AddRange(WorldRules.WriteLines());
				lines.AddRange(PlayerLevels.WriteLines());
				lines.AddRange(WorldOptions.WriteLines());
				lines.AddRange(WorldIslands.WriteLines());
				lines.AddRange(StoryChain.WriteLines());
				foreach (Entry e in islands)
				{
					IslandObjectState.Capture(e);
					// (the island file's hash: another player hosting the world later finds their copy of it - WorldCopy)
					lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}", e.HostName, e.Position.x, e.Position.y, e.Position.z, IslandObjectState.Encode(e.State),
						e.Rule.Replace("|", "/"), e.Label.Replace("|", "/"), IslandNetwork.HashOf(e.Name) ?? e.Hash ?? "") + (e.Extra != null ? "|" + e.Extra : ""));
				}
				lines.AddRange(keptLines);
				// (the version that wrote it: an older one reading it warns - AU5)
				lines.Add("@modversion=" + LibraryPack.ModVersion);
				SafeFile.WriteAllLines(FilePath, lines.ToArray());
				WorldCopy.AfterSave(lines.ToArray());
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Could not save the world's island list: " + ex.Message);
				// (the player is told once a minute at most: before, a failed save was only a line in the log - AU36)
				if (Time.unscaledTime >= nextSaveFailNotice)
				{
					nextSaveFailNotice = Time.unscaledTime + 60f;
					DynamicIslands.Notify("Custom Islands couldn't save this world's islands: " + (SafeFile.InUse(ex) ? "their file is in use by another program (an antivirus or a cloud sync?)" : ex.Message) + ". It tries again at the next save.", true);
				}
			}
		}

		/// <summary>Reads the island list of the world that just finished loading; CustomIslandSpawner spawns the ones near the raft.</summary>
		static bool loadFailed, loadFailedTold;
		static float nextSaveFailNotice;
		/// <summary>Lines of the world's file this version couldn't read: written back as they were.</summary>
		static readonly List<string> keptLines = new List<string>();

		/// <summary>The world file was written by this version (the "@modversion=" line): a newer one warns the host once.</summary>
		static void NewerFile(string version)
		{
			if (string.IsNullOrEmpty(version) || LibraryPack.CompareVersions(version, LibraryPack.ModVersion) <= 0) return;
			Debug.LogWarning("[CUSTOM ISLANDS] This world was saved by Custom Islands " + version + ", newer than this " + LibraryPack.ModVersion);
			DynamicIslands.Notify("This world was saved by a newer Custom Islands (" + version + ", you have " + LibraryPack.ModVersion + "). What this version doesn't know is kept as it is, but please update the mod before playing on.", true);
		}

		public static void OnWorldLoaded()
		{
			islands.Clear();
			loadedFor = WorldKey;
			loadFailed = loadFailedTold = false;
			keptLines.Clear();
			WorldCopy.ForgetHostLines();
			CustomIslandSpawner.Enabled = true;
			CustomIslandSpawner.OnWorldLoaded();
			IslandNetwork.OnWorldLoaded();
			WorldRules.Reset();
			WorldDirector.Reset();
			StoryBook.Reset();
			PlayerPlaces.Reset();
			Claims.Reset();
			WorldRandomizer.Reset();
			PlayerLevels.Reset();
			WorldOptions.Reset();
			WorldIslands.Reset();
			StoryChain.Reset();
			// (the newest copy: this PC's own, or the one that came with Raft's world folder from another host - WorldCopy)
			string[] fileLines = null;
			try { fileLines = Raft_Network.IsHost ? WorldCopy.Choose(FilePath) : null; }
			catch (Exception e)
			{
				loadFailed = true;
				Debug.LogError("[CUSTOM ISLANDS] Reading the world's custom islands (" + FilePath + ") failed: " + e);
				DynamicIslands.Notify("This world's custom islands couldn't be read (see the log, F10): nothing of them is saved until it loads again", true);
			}
			if (fileLines == null) { WorldRules.OnWorldRead(); WorldDirector.OnWorldLoaded(); HeadStart.OnWorldRead(); return; }
			foreach (string line in fileLines)
			try
			{
				if (line.StartsWith("#") || line.Trim().Length == 0) continue;
				if (line.StartsWith("@auto=")) { CustomIslandSpawner.Enabled = !line.Substring(6).Trim().Equals("off", StringComparison.OrdinalIgnoreCase); continue; }
				if (line.StartsWith("@modversion=")) { NewerFile(line.Substring("@modversion=".Length).Trim()); continue; }
				int eq = line.IndexOf('=');
				if (line.StartsWith("@") && eq > 1 && (WorldCopy.ReadLine(line.Substring(1, eq - 1).Trim(), line.Substring(eq + 1)) || WorldRules.ReadLine(line.Substring(1, eq - 1).Trim(), line.Substring(eq + 1)) || StoryBook.ReadLine(line.Substring(1, eq - 1).Trim(), line.Substring(eq + 1)) ||
					PlayerPlaces.ReadLine(line.Substring(1, eq - 1).Trim(), line.Substring(eq + 1)) ||
					WorldRandomizer.ReadLine(line.Substring(1, eq - 1).Trim().ToLowerInvariant(), line.Substring(eq + 1)) ||
					PlayerLevels.ReadLine(line.Substring(1, eq - 1).Trim(), line.Substring(eq + 1)) ||
					WorldOptions.ReadLine(line.Substring(1, eq - 1).Trim().ToLowerInvariant(), line.Substring(eq + 1)) ||
					WorldIslands.ReadLine(line.Substring(1, eq - 1).Trim().ToLowerInvariant(), line.Substring(eq + 1)) ||
					StoryChain.ReadLine(line.Substring(1, eq - 1).Trim().ToLowerInvariant(), line.Substring(eq + 1)) ||
					WorldDirector.ReadLine(line.Substring(1, eq - 1).Trim().ToLowerInvariant(), line.Substring(eq + 1)))) continue;
				// (a setting this version doesn't know - written by a newer one: kept as it is, so this version's save doesn't
				// erase it for everyone - AU5)
				if (line.StartsWith("@")) { keptLines.Add(line); Debug.LogWarning("[CUSTOM ISLANDS] A setting this version doesn't know is kept as it is: " + line); continue; }
				string[] p = line.Split('|');
				float x, y, z;
				if (p.Length < 4 || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
					!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
				{
					Debug.LogWarning("[CUSTOM ISLANDS] Ignoring bad line in " + FilePath + ": " + line);
					continue;
				}
				// (the name becomes a file's path, and the file may have come from another host with Raft's world folder: one
				// that isn't a plain file name - "..\..\x" - is kept as it is, not read)
				string bad = FileNames.ReceivedProblem(p[0]);
				if (bad != null) { Debug.LogWarning("[CUSTOM ISLANDS] An island of " + FilePath + " is left out (kept in the file): its name " + bad); keptLines.Add(line); continue; }
				string hash = p.Length > 7 ? p[7].Trim() : "";
				var read = new Entry { Id = IslandNetwork.NewId(), Name = WorldCopy.LocalFileFor(p[0], hash), HostName = p[0], Hash = hash.Length > 0 ? hash : null, Position = new Vector3(x, y, z),
					State = IslandObjectState.Decode(p.Length > 4 ? p[4] : null), Rule = p.Length > 5 ? p[5] : "", Label = p.Length > 6 ? p[6] : "",
						// (fields a newer version added: read past, and written back as they were - AU5)
						Extra = p.Length > 8 ? string.Join("|", p, 8, p.Length - 8) : null };
				// (no file of it on this PC: it waits for a player to send it, instead of failing - AU6)
				if (read.Hash != null && !File.Exists(IslandSpawner.PathFor(read.Name)) && IslandNetwork.Wanted.Contains(read.Hash))
				{
					// (the host is asked what to do in MissingIslandsCheck's box: the rest plays, and this one comes with a player who has it)
					read.WaitingForFile = true;
				}
				islands.Add(read);
			}
			// (one line that can't be read is left out - the rest of the world still loads; before, it stopped the reading
			// half way and the next save wrote the half over the whole file - AU12)
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read a line of " + FilePath + " (left as it is): " + line + " - " + e.Message); keptLines.Add(line); }
			Debug.Log("[CUSTOM ISLANDS] World '" + SaveAndLoad.CurrentGameFileName + "' has " + islands.Count + " custom island(s); automatic islands " +
				(CustomIslandSpawner.Enabled ? "on" : "off"));
			StoryChain.OnWorldRead();
			WorldRules.OnWorldRead();
			WorldDirector.OnWorldLoaded();
			HeadStart.OnWorldRead();
			MissingIslandsCheck.OnWorldRead(islands);
		}
	}

	/// <summary>Keeps the island list in step with Raft's own world saves.</summary>
	[HarmonyPatch(typeof(SaveAndLoad), "SaveWorld")]
	static class SaveWorldPatch
	{
		static void Postfix()
		{
			WorldCopy.InRaftSave = true;
			try { IslandWorldState.Save(); }
			finally { WorldCopy.InRaftSave = false; }
		}
	}

	/// <summary>Raft's stamp of the save it is writing (the world file names the save it belongs to - WorldCopy).</summary>
	[HarmonyPatch(typeof(SaveAndLoad), "CreateRGDGame")]
	static class CreateRGDGamePatch
	{
		static void Postfix(RGD_Game __result) { if (__result != null) WorldCopy.SavingStamp = __result.lastPlayedDateTicks; }
	}

	/// <summary>Raft's stamp of the save it is loading (an older one, if the player picked one in the Load Game box).</summary>
	[HarmonyPatch(typeof(SaveAndLoad), "RestoreRGDGame")]
	static class RestoreRGDGamePatch
	{
		static void Prefix(RGD_Game game)
		{
			WorldCopy.LoadingStamp = game != null ? game.lastPlayedDateTicks : 0L;
			string folder;
			WorldCopy.LoadedFolder = game != null && SavesFolderPatch.Folders.TryGetValue(game, out folder) ? folder : null;
		}
	}

	/// <summary>
	/// The folder each world's save in the Load Game list was read from. Raft goes by the name kept inside the save
	/// (CurrentGameFileName), so a world folder renamed by hand is played, but its folder is not found by that name.
	/// </summary>
	[HarmonyPatch(typeof(SaveAndLoad), "GetLatestRGDGameFromBackupFolders")]
	static class SavesFolderPatch
	{
		internal static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RGD_Game, string> Folders = new System.Runtime.CompilerServices.ConditionalWeakTable<RGD_Game, string>();

		static void Postfix(System.IO.DirectoryInfo worldDirInfo, RGD_Game __result)
		{
			if (__result == null || worldDirInfo == null) return;
			try { Folders.Remove(__result); Folders.Add(__result, worldDirInfo.FullName); } catch { }
		}
	}

	/// <summary>A new world: no folder from the Load Game list.</summary>
	[HarmonyPatch(typeof(NewGameBox), "Button_CreateNewGame")]
	static class NewGameFolderPatch
	{
		static void Prefix() { WorldCopy.LoadedFolder = null; }
	}
}
