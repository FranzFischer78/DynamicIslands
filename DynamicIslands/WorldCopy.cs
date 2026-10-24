using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Lets any player host a world later ("host swap"). Raft keeps a world only on the PC of the player who hosts it;
	/// to host it another time, the group copies its folder (Raft's World\&lt;name&gt;) to the next host's PC. The mod's own
	/// state of the world (Mods\DynamicIslands\worlds\&lt;world id&gt;.txt: custom islands, what was used, quests, the
	/// journal, levels, world rules, randomizer, options...) lives beside it on the host's PC, so it would stay behind.
	/// Two copies make it travel:
	///  - the host writes the world file into Raft's world folder too (CustomIslands.txt), so it moves with the folder;
	///  - the host sends it to every player each time Raft saves (and to a player who joins), and they keep it as their
	///    own worlds\&lt;world id&gt;.txt, so the world can be hosted by any of them with its latest state.
	/// When a world loads, the newest of the copies (by the "@savecount" counter in it) is read. Island files come along with
	/// joining already (a player keeps them as &lt;name&gt;_&lt;hash&gt;.island); the list says each island's hash, so a new host
	/// finds its copy and the island keeps its name (rules, quests, journal pages refer to it).
	///
	/// Older saves: Raft keeps the last 8 saves of a world (World\&lt;name&gt;\&lt;date&gt;, the newest "-Latest"), and its
	/// Load Game box can load any of them. So that the mod's state goes back with Raft's (a chest looted after that save
	/// is full again, a quest is where it was), the copy is also written into the save's own folder with Raft's stamp of
	/// that save ("@raftsave=", RGD_Game.lastPlayedDateTicks); loading a save reads the copy with the same stamp.
	///
	/// Writes between Raft's saves (a story step, a Receiver unlock, a setting changed) say so ("@between=" the stamp of
	/// Raft's save they follow, instead of "@raftsave="). After a crash or Alt+F4 such a copy is newer than Raft's save
	/// (the players' inventories): loading that save then takes the islands' state - chests, quests, story - from the
	/// copy written with it, and only the settings (SettingKeys) from the newer copy, so no loot or reward is lost - AU3.
	/// "Newest" goes by the world's save counter ("@savecount=", one more on every write, carried in every copy and sent
	/// with it), the PC's clock ("@savedat=") only breaks a tie: two PCs' clocks needn't agree - AU4.
	/// </summary>
	public static class WorldCopy
	{
		public const string FileName = "CustomIslands.txt";
		const int ChunkChars = 3000;

		/// <summary>The copy's stamp line: "@savedat=&lt;UTC ticks&gt;".</summary>
		public static string StampLine() { return "@savedat=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture); }

		/// <summary>Raft's stamp of the save being written (CreateRGDGame) and of the save being loaded (RestoreRGDGame); 0 = none.</summary>
		public static long SavingStamp, LoadingStamp;
		/// <summary>True while the world file is written because Raft saves the world (not for a setting changed in between).</summary>
		public static bool InRaftSave;

		/// <summary>The line naming Raft's save this state belongs to (null outside Raft's own save).</summary>
		public static string RaftSaveLine() { return InRaftSave && SavingStamp != 0 ? "@raftsave=" + SavingStamp.ToString(CultureInfo.InvariantCulture) : null; }

		/// <summary>The world's save counter: the highest of its copies when it loaded, one more on every write (AU4).</summary>
		public static long SaveCount;
		/// <summary>The save time of the copy SaveCount was counted from (the highest's): a player's copy with the same counter is
		/// only newer than it, not than the one the world loaded (an older save, or the state of a save after a crash).</summary>
		static long newestStamp;
		/// <summary>Raft's save the world's state follows: the one loaded, then each one Raft writes (0 = not known).</summary>
		static long followsSave;

		/// <summary>The stamp lines of a write of the world file: the clock, the counter (one more), and Raft's save it is
		/// written with ("@raftsave=") or, between Raft's saves, the one it follows ("@between=" - AU3).</summary>
		public static string[] StampLines()
		{
			SaveCount++;
			string raft = RaftSaveLine();
			if (raft != null) followsSave = SavingStamp;
			return new[] { StampLine(), "@savecount=" + SaveCount.ToString(CultureInfo.InvariantCulture), raft ?? "@between=" + followsSave.ToString(CultureInfo.InvariantCulture) };
		}

		public static bool ReadLine(string key, string value)
		{
			// (which PC saved the world: the plan's owner for a world that names none - AU25)
			if (key.Equals("savedby", StringComparison.OrdinalIgnoreCase)) { WorldDirector.ReadSavedBy(value); return true; }
			return key.Equals("savedat", StringComparison.OrdinalIgnoreCase) || key.Equals("raftsave", StringComparison.OrdinalIgnoreCase) ||
				key.Equals("savecount", StringComparison.OrdinalIgnoreCase) || key.Equals("between", StringComparison.OrdinalIgnoreCase);
		}

		static long StampOf(string[] lines) { return LongLine(lines, "@savedat="); }
		static long RaftSaveOf(string[] lines) { return LongLine(lines, "@raftsave="); }
		static long CountOf(string[] lines) { return LongLine(lines, "@savecount="); }

		/// <summary>Copy a is newer than b: by the save counter, the clock only when the counters are equal (AU4).</summary>
		static bool Newer(string[] a, string[] b)
		{
			long ca = CountOf(a), cb = CountOf(b);
			return ca != cb ? ca > cb : StampOf(a) > StampOf(b);
		}

		/// <summary>Written between Raft's saves: no "@raftsave=" line (older versions wrote none there either - AU3).</summary>
		static bool IsBetween(string[] lines) { return RaftSaveOf(lines) == 0; }

		/// <summary>The world's settings (the rules, options, levels on/off, randomizer, islands left out...): a crash doesn't
		/// take them back to Raft's save - they aren't loot or progress (AU3).</summary>
		static readonly string[] SettingKeys = { "auto", "monsters", "buildcost", "regrow", "options", "optionsused", "levels", "randomizer", "islandsoff", "raftgap" };

		static bool IsSetting(string line)
		{
			int eq = line.IndexOf('=');
			return line.StartsWith("@") && eq > 1 && SettingKeys.Contains(line.Substring(1, eq - 1).Trim().ToLowerInvariant());
		}

		/// <summary>The state of one copy with the settings of another (AU3).</summary>
		static string[] Merge(string[] state, string[] settings)
		{
			return state.TakeWhile(l => l.StartsWith("#")).Concat(settings.Where(IsSetting)).Concat(state.SkipWhile(l => l.StartsWith("#")).Where(l => !IsSetting(l))).ToArray();
		}

		static bool futureTold;

		/// <summary>A copy stamped more than a day ahead of this PC's clock (a PC's clock set wrong): logged, and the host is
		/// told once - the save counter decides which copy is newest, but the clock still breaks ties (AU4).</summary>
		static void WarnFuture(string[] lines, string where)
		{
			if (lines == null) return;
			bool ahead = StampOf(lines) > DateTime.UtcNow.Ticks + TimeSpan.TicksPerDay ||
				Math.Max(RaftSaveOf(lines), LongLine(lines, "@between=")) > DateTime.Now.Ticks + TimeSpan.TicksPerDay;
			if (!ahead) return;
			Debug.LogWarning("[CUSTOM ISLANDS] The world's copy " + where + " is stamped more than a day in the future: a PC's clock is set wrong?");
			if (Raft_Network.IsHost && !futureTold)
			{
				futureTold = true;
				DynamicIslands.Notify("A copy of this world's custom islands is dated more than a day in the future: is a PC's clock set wrong? (see the log, F10)", true);
			}
		}

		static long LongLine(string[] lines, string prefix)
		{
			foreach (string l in lines)
				if (l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					long t;
					if (long.TryParse(l.Substring(prefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) return t;
				}
			return 0L;
		}

		/// <summary>The folder of Raft's newest save of the world being played (World\&lt;name&gt;\&lt;date&gt;-Latest), or null.</summary>
		static string LatestSaveFolder(string worldFolder)
		{
			try { return Directory.GetDirectories(worldFolder).FirstOrDefault(d => d.EndsWith("-Latest", StringComparison.OrdinalIgnoreCase)); }
			catch { return null; }
		}

		/// <summary>The last load read an older save's copy (tests): "" or the save folder's name.</summary>
		public static string LastOlderSave { get; private set; }

		/// <summary>The folder Raft loaded the world from (its Load Game list), or null (a new world, a joined one).</summary>
		public static string LoadedFolder { get; set; }

		/// <summary>Raft's folder of the world being played (World\&lt;name&gt;), or null.</summary>
		public static string RaftWorldFolder
		{
			get
			{
				try
				{
					if (string.IsNullOrEmpty(SaveAndLoad.WorldPath) || string.IsNullOrEmpty(SaveAndLoad.CurrentGameFileName)) return null;
					string dir = Path.Combine(SaveAndLoad.WorldPath, SaveAndLoad.CurrentGameFileName);
					if (Directory.Exists(dir)) return dir;
					// (a folder renamed by hand: Raft plays it under the name kept in its save, and writes its next save to a
					// folder of that name; until then the copy that came with the world is in the folder it was loaded from - IM41)
					return LoadedFolder != null && Directory.Exists(LoadedFolder) ? LoadedFolder : null;
				}
				catch { return null; }
			}
		}

		/// <summary>The last copy's source when a world was read (tests): "mod folder", "Raft's world folder", or "none".</summary>
		public static string LastSource { get; private set; }

		/// <summary>A file's lines (null when it isn't there); tried a few times while it is in use, then the error is thrown.</summary>
		static string[] ReadThere(string file)
		{
			for (int i = 0; ; i++)
			{
				try { return File.Exists(file) ? File.ReadAllLines(file) : null; }
				catch (IOException e)
				{
					if (i >= 4) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + file + ": " + e.Message); throw; }
					System.Threading.Thread.Sleep(100);
				}
			}
		}

		/// <summary>
		/// Host, loading a world: the newest of the mod's own file and the copy in Raft's world folder (null if neither).
		/// </summary>
		public static string[] Choose(string modFile)
		{
			string[] chosen = ChooseCopy(modFile);
			// (what a player who joins is sent until the next save: the copy the world loaded, not the mod's file, which is
			// older when the copy in Raft's world folder was newer - a player's kept copy looked newer than it - AU26)
			if (chosen != null) { last = chosen; lastKey = SaveAndLoad.WorldGuid.ToString(); }
			return chosen;
		}

		static string[] ChooseCopy(string modFile)
		{
			// (a file that is there but can't be read - locked by an antivirus or a sync - throws: the world then loads as
			// "couldn't be read" and isn't saved, instead of as "no file", which the next save deleted - review 2026-10-06)
			string[] mine = ReadThere(modFile);
			WarnIfShared(mine);
			string folder = RaftWorldFolder;
			string copy = folder != null ? Path.Combine(folder, FileName) : null;
			string[] travelled = copy != null ? ReadThere(copy) : null;
			LastOlderSave = "";
			long loading = LoadingStamp;
			LoadingStamp = 0; // (used once: a new world made afterwards has no save of its own yet)
			followsSave = loading;
			var saves = new List<KeyValuePair<string, string[]>>();
			// (also with no stamp when both main copies are gone: a save's copy is then all there is)
			if (folder != null && (loading != 0 || (mine == null && travelled == null)))
				try
				{
					foreach (string dir in Directory.GetDirectories(folder))
					{
						string f = Path.Combine(dir, FileName);
						if (File.Exists(f)) saves.Add(new KeyValuePair<string, string[]>(dir, File.ReadAllLines(f)));
					}
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking through the world's saves: " + e.Message); }
			List<string[]> all = new[] { mine, travelled }.Concat(saves.Select(s => s.Value)).Where(l => l != null).ToList();
			// (the counter goes on from the highest of every copy: the next write is the newest, whatever the clocks say - AU4)
			SaveCount = all.Select(CountOf).DefaultIfEmpty(0L).Max();
			newestStamp = all.Where(l => CountOf(l) == SaveCount).Select(StampOf).DefaultIfEmpty(0L).Max();
			WarnFuture(mine, "in the mod's folder");
			WarnFuture(travelled, "in Raft's world folder");
			// (Raft is loading an OLDER save than the world's newest - one the player picked in Raft's Load Game box: the
			// state written with that save, so the world fits together. The newest save is read as always: the newest
			// copy, which also has what changed after it - a setting, or the copy another host sent.)
			if (loading != 0)
			{
				long newest = all.Select(RaftSaveOf).DefaultIfEmpty(0L).Max();
				KeyValuePair<string, string[]> match = saves.Where(s => RaftSaveOf(s.Value) == loading).Aggregate(default(KeyValuePair<string, string[]>), (b, s) => b.Value == null || Newer(s.Value, b.Value) ? s : b);
				// (older: another Raft save's copy was written after this one's - by the counter, not the clock - AU4)
				bool older = match.Value != null ? all.Any(l => RaftSaveOf(l) != 0 && RaftSaveOf(l) != loading && Newer(l, match.Value)) : loading < newest;
				// (an older save without a copy of its own - the first save of a new world, or one made before saves had
				// copies: its state isn't known; the newest is used, and the player is told - AU27)
				if (older && match.Value == null)
				{
					Debug.LogWarning("[CUSTOM ISLANDS] The save being loaded is older than the world's newest and has no copy of the custom islands' state: the newest is used");
					DynamicIslands.Notify("This older save has no record of its custom islands: their chests, quests and story are as in the world's newest save.", true);
				}
				if (older && match.Value != null)
				{
					LastSource = "Raft's save " + Path.GetFileName(match.Key);
					LastOlderSave = Path.GetFileName(match.Key);
					Debug.Log("[CUSTOM ISLANDS] An older save of the world: its custom islands, used objects, quests and the rest go back to that save too (" + match.Key + ")");
					DynamicIslands.Notify("An older save of this world: its custom islands, chests, quests and story are as they were then too.");
					return match.Value;
				}
			}
			if (mine == null && travelled == null)
			{
				// (both main copies gone - deleted, or lost by a sync - but a Raft save has one: that save's (the one being
				// loaded, else the newest). Loaded empty, the world was saved empty and sent to players, over their good copies)
				Func<IEnumerable<KeyValuePair<string, string[]>>, KeyValuePair<string, string[]>> newestOf = list => list.Aggregate(default(KeyValuePair<string, string[]>), (n, s) => n.Value == null || Newer(s.Value, n.Value) ? s : n);
				KeyValuePair<string, string[]> best = newestOf(saves.Where(s => RaftSaveOf(s.Value) == loading));
				if (best.Value == null) best = newestOf(saves);
				if (best.Value == null) { LastSource = "none"; return null; }
				LastSource = "Raft's save " + Path.GetFileName(best.Key);
				Debug.LogWarning("[CUSTOM ISLANDS] The world's copies of its custom islands are missing: the copy in Raft's save is used (" + best.Key + ")");
				return best.Value;
			}
			string[] chosen = mine;
			LastSource = "mod folder";
			if (travelled != null && (mine == null || Newer(travelled, mine)))
			{
				chosen = travelled;
				LastSource = "Raft's world folder";
				Debug.Log("[CUSTOM ISLANDS] The world's custom islands come from the copy in Raft's world folder" + (mine != null ? " (newer than the mod's own)" : "") + ": " + copy);
			}
			// (the newest copy was written after Raft's save being loaded - a story step, a chest, a setting, then a crash or
			// Alt+F4 before Raft saved again: the players' inventories are Raft's save's, so the islands' state is the copy
			// written with that save, and only the settings come from the newer copy - AU3)
			if (loading != 0 && IsBetween(chosen))
			{
				string[] saved = all.Where(l => !IsBetween(l) && RaftSaveOf(l) == loading).Aggregate((string[])null, (b, l) => b == null || Newer(l, b) ? l : b);
				if (saved != null && Newer(chosen, saved))
				{
					LastSource += ", islands' state from Raft's save";
					Debug.LogWarning("[CUSTOM ISLANDS] The world's newest copy was written after the Raft save being loaded (a crash?): its islands, chests and quests are as in that save, its settings as in the newest copy");
					return Merge(saved, chosen);
				}
			}
			return chosen;
		}

		/// <summary>The world folders warned about this session ("world id|other folder"): once each.</summary>
		static readonly HashSet<string> warnedShared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Host, loading a world: the mod's file of this world id was last saved by another of Raft's world folders that is
		/// still there. A world folder copied (or one copied over another) keeps the world's id, and the mod's state is kept
		/// by that id: both folders then share one state of the custom islands - what is looted, done or brought in one is
		/// so in the other. The host is told once; the state isn't split (which folder had what can't be told apart). A
		/// renamed world (the other folder gone) says nothing. (AU41)
		/// </summary>
		static void WarnIfShared(string[] mine)
		{
			if (mine == null) return;
			try
			{
				string here = SaveAndLoad.CurrentGameFileName, raft = SaveAndLoad.WorldPath;
				if (string.IsNullOrEmpty(here) || string.IsNullOrEmpty(raft)) return;
				// (the world's name in the file's first line: the folder of the world that saved it)
				string other = Housekeeping.WorldName(mine, "");
				if (other.Length == 0 || other.Equals(here, StringComparison.OrdinalIgnoreCase) || other.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;
				if (!Directory.Exists(Path.Combine(raft, other))) return;
				if (!warnedShared.Add(SaveAndLoad.WorldGuid + "|" + other)) return;
				Debug.LogWarning("[CUSTOM ISLANDS] The world '" + here + "' has the same world id as '" + other + "' (a copy of its folder?): both share one state of the custom islands (" + SaveAndLoad.WorldGuid + ")");
				DynamicIslands.Notify("This world looks like a copy of the world '" + other + "' (same world id): both share one state of their custom islands - chests, quests and story. Play only one of them, or keep the copy as a backup.", true);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking for a copy of this world's folder: " + e.Message); }
		}

		/// <summary>Host, after writing the world file: the copy in Raft's world folder, and the copy for every player.</summary>
		public static void AfterSave(string[] lines)
		{
			string folder = RaftWorldFolder;
			if (folder != null)
				try { SafeFile.WriteAllLines(Path.Combine(folder, FileName), lines); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not write the world's copy into Raft's world folder: " + e.Message); }
			WriteIntoSave(folder, lines);
			last = lines;
			newestStamp = StampOf(lines);
			lastKey = SaveAndLoad.WorldGuid.ToString();
			Send(null);
			if (RaftSaveLine() != null) WorldSaveShare.AfterRaftSave(folder);
		}

		/// <summary>Host: the world file forgotten (a world without anything of the mod): the copies go too.</summary>
		public static void AfterDelete()
		{
			string folder = RaftWorldFolder;
			if (folder != null)
				try { string f = Path.Combine(folder, FileName); if (File.Exists(f)) File.Delete(f); } catch { }
			last = new[] { "# (nothing of Custom Islands in this world)" }.Concat(StampLines()).ToArray();
			// (the save still gets its note: loading it later means "nothing of the mod", not an older save's state)
			if (RaftSaveLine() != null) WriteIntoSave(folder, last);
			newestStamp = StampOf(last);
			lastKey = SaveAndLoad.WorldGuid.ToString();
			Send(null);
		}

		/// <summary>The copy in the folder of the save Raft just wrote (it moves with it when Raft makes it a backup).</summary>
		static void WriteIntoSave(string worldFolder, string[] lines)
		{
			if (worldFolder == null || RaftSaveLine() == null) return;
			string save = LatestSaveFolder(worldFolder);
			if (save == null) return;
			try { SafeFile.WriteAllLines(Path.Combine(save, FileName), lines); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not write the world's copy into Raft's save folder: " + e.Message); }
		}

		static string[] last;
		static string lastKey;

		/// <summary>Host: sends the world file (as last saved; read from disk if not saved this session) to one player or everyone.</summary>
		public static void Send(Network_UserId? to)
		{
			if (!Raft_Network.IsHost) return;
			string key = SaveAndLoad.WorldGuid.ToString();
			if (last == null || lastKey != key)
			{
				string file = IslandWorldState.WorldFilePath;
				last = File.Exists(file) ? File.ReadAllLines(file) : null;
				lastKey = key;
			}
			if (last == null || SaveAndLoad.WorldGuid == Guid.Empty) return;
			string text = string.Join("\n", last);
			string id = StampOf(last).ToString(CultureInfo.InvariantCulture);
			// (never between the two halves of a character outside the basic plane - an emoji in a page or a label: each half
			// went over the network on its own as a broken character, and the player's copy kept "?" there)
			var chunks = new List<string>();
			for (int at = 0; at < text.Length || chunks.Count == 0;)
			{
				int len = Mathf.Min(ChunkChars, text.Length - at);
				if (len > 1 && at + len < text.Length && char.IsHighSurrogate(text[at + len - 1])) len--;
				chunks.Add(text.Substring(at, len));
				at += len;
			}
			int count = chunks.Count;
			for (int i = 0; i < count; i++)
			{
				var msg = new IslandNetMessage { Name = key, Hash = id, Index = i, Count = count, Data = chunks[i] };
				IslandNetwork.SendWorldCopy(msg, to);
			}
			Debug.Log("[CUSTOM ISLANDS] [net] Sent the world's copy (" + text.Length + " characters, " + count + " part(s)) to " + (to.HasValue ? to.Value.ToString() : "every player"));
		}

		static readonly Dictionary<string, string[]> incoming = new Dictionary<string, string[]>();

		/// <summary>The last copy a player kept (tests): "&lt;world id&gt; &lt;lines&gt; lines".</summary>
		public static string LastKept { get; private set; }

		/// <summary>A player: the host's world file as it last came (null until it has, and in a new world) - the host's plan,
		/// its progress and the islands left out, for the player's WorldPlan, WorldIslands and world window (they showed
		/// the player's own, empty, state).</summary>
		public static string[] HostLines { get; private set; }
		internal static void ForgetHostLines() { HostLines = null; checkedJoin.Clear(); }
		/// <summary>A player: the host's world arrived (a join) - its first copy is compared with the kept one again (AU26).</summary>
		internal static void OnWorldReceived() { checkedJoin.Clear(); }

		/// <summary>A player: the worlds whose host's copy came since this world loaded (the kept copy is compared once - AU26).</summary>
		static readonly HashSet<string> checkedJoin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>The file a player's newer kept copy is set aside as (tests), or "".</summary>
		public static string LastSetAside { get; private set; }

		/// <summary>
		/// A player, as the host's copy arrives on joining: when this PC's kept copy of the world is newer (by the save counter,
		/// AU4), it is kept as &lt;world id&gt;.kept-&lt;its save count&gt;.txt (the newer history the host's copy replaces), the host
		/// is told (WorldCopyNewer) and so is the player. True when it was set aside (the host's copy may then be written).
		/// </summary>
		static bool KeepNewer(string path, string[] hosts, Guid guid)
		{
			string[] mine;
			try { mine = File.ReadAllLines(path); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reading this PC's copy of the world: " + e.Message); return false; }
			if (!Newer(mine, hosts)) return false;
			long count = CountOf(mine), at = StampOf(mine);
			string aside = Path.Combine(Path.GetDirectoryName(path), guid + ".kept-" + count.ToString(CultureInfo.InvariantCulture) + ".txt");
			// (one already kept with this counter but other content - another branch of the world that reached the same count:
			// kept beside it by its save time, not lost under the host's copy)
			if (File.Exists(aside) && !File.ReadAllLines(aside).SequenceEqual(mine))
				aside = Path.Combine(Path.GetDirectoryName(path), guid + ".kept-" + count.ToString(CultureInfo.InvariantCulture) + "-" + at.ToString(CultureInfo.InvariantCulture) + ".txt");
			try { if (!File.Exists(aside)) File.Copy(path, aside); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not keep this PC's newer copy of the world (" + aside + "): " + e.Message + " - the host's isn't kept over it"); return false; }
			LastSetAside = aside;
			Debug.LogWarning("[CUSTOM ISLANDS] This PC's copy of the world (save " + count + ") is newer than the host's (save " + CountOf(hosts) + "): kept as " + aside + ", the host's copy is taken");
			DynamicIslands.Notify("Your copy of this world (" + When(at) + ") is newer than the host's: the host's is played, yours is kept as " + Path.GetFileName(aside) + " in Mods\\DynamicIslands\\worlds.", true);
			IslandNetwork.SendWorldCopyNewer(new IslandNetMessage { Name = guid.ToString(), Data = count.ToString(CultureInfo.InvariantCulture) + ";" + at.ToString(CultureInfo.InvariantCulture) });
			return true;
		}

		/// <summary>A copy's "@savedat=" stamp as a local date and time ("an unknown time" for none).</summary>
		static string When(long ticks)
		{
			if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return "an unknown time";
			return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		}

		/// <summary>The last player's newer copy the host was warned about (tests), or "".</summary>
		public static string LastNewerWarning { get; private set; }

		/// <summary>
		/// Host: a player's kept copy of this world is newer than the copy it was sent (AU26). Warned when it is newer than
		/// anything this host had (the world's save counter, AU4): the world was hosted from that copy since, and this one may
		/// be missing their progress. (A copy newer only than the one sent - an older save loaded on purpose - is just logged.)
		/// </summary>
		public static void OnPlayerNewer(IslandNetMessage msg, Network_UserId from)
		{
			if (msg == null || !(msg.Name ?? "").Equals(SaveAndLoad.WorldGuid.ToString(), StringComparison.OrdinalIgnoreCase)) return;
			string[] p = (msg.Data ?? "").Split(';');
			long count, at = 0;
			if (p.Length < 1 || !long.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out count)) return;
			if (p.Length > 1) long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out at);
			string who = PrivateStorage.NameOf(from.Id);
			if (string.IsNullOrEmpty(who) || who == "another player") who = "A player"; else who = "'" + who + "'";
			// (the same counter: newer than the highest copy this host had, not than the one it loaded - an older save picked in
			// Raft's Load Game box, or a save's state after a crash, warned of progress the host had all along)
			bool newer = count > SaveCount || (count == SaveCount && at > newestStamp);
			Debug.LogWarning("[CUSTOM ISLANDS] " + who + " has a copy of this world from " + When(at) + " (save " + count + ", this world's " + SaveCount + ")" +
				(newer ? ": newer than this host's - the world may be missing their progress" : ": newer than the copy sent only (an older save loaded?)"));
			if (!newer) return;
			LastNewerWarning = who + " " + count;
			DynamicIslands.Notify(who + "'s copy of this world from " + When(at) + " is newer - your world may be missing their progress. (They keep it as a file in their Mods\\DynamicIslands\\worlds.)", true, 15);
		}

		/// <summary>A value of the host's copy ("@key=value"), or null.</summary>
		public static string HostValue(string key)
		{
			if (HostLines == null) return null;
			string prefix = "@" + key + "=";
			string line = HostLines.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
			return line != null ? line.Substring(prefix.Length).Trim() : null;
		}

		/// <summary>A player: a part of the host's world file; when all have come, it is kept as worlds\&lt;world id&gt;.txt.</summary>
		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost || string.IsNullOrEmpty(msg.Name) || msg.Count <= 0 || msg.Index < 0 || msg.Index >= msg.Count) return;
			Guid guid;
			if (!Guid.TryParse(msg.Name, out guid) || guid == Guid.Empty) return;
			string key = msg.Name + "|" + msg.Hash;
			string[] parts;
			if (!incoming.TryGetValue(key, out parts) || parts.Length != msg.Count) incoming[key] = parts = new string[msg.Count];
			parts[msg.Index] = msg.Data ?? "";
			if (parts.Any(p => p == null)) return;
			incoming.Remove(key);
			string[] lines = string.Concat(parts).Split('\n');
			WarnFuture(lines, "the host sent");
			// (newer by the world's save counter, not the PCs' clocks - AU4)
			if (HostLines == null || !Newer(HostLines, lines)) HostLines = lines;
			string path = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), guid + ".txt");
			try
			{
				// (the host's first copy since joining: this player's kept copy may be newer - the world hosted from it since,
				// by this player or another - and the host plays an older one. It is kept beside it and the host is warned,
				// then the host's copy is taken: the world goes on from what the host has - AU26)
				bool joining = checkedJoin.Add(guid.ToString());
				bool keptNewer = joining && File.Exists(path) && KeepNewer(path, lines, guid);
				// (never over a newer copy: a late message from an older save)
				if (!keptNewer && File.Exists(path) && Newer(File.ReadAllLines(path), lines)) return;
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				SafeFile.WriteAllLines(path, lines);
				LastKept = guid + " " + lines.Length + " lines";
				Debug.Log("[CUSTOM ISLANDS] Kept the host's copy of this world (" + lines.Length + " lines) to host it later: " + path);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not keep the world's copy: " + e.Message); }
		}

		/// <summary>
		/// Host, reading an island line of the world file: the local file for an island saved by another host. Its own file
		/// when that has the recorded hash (or no hash was recorded), else a copy downloaded while joining (name_hash), else
		/// any island file with that content (a pack's island renamed on import - AU6). When there is none, the players are
		/// asked for it (AU6): with no file of that name here the island waits for it (name_hash, the name it arrives as);
		/// with another version of it here, that one plays until a player sends the right one.
		/// </summary>
		public static string LocalFileFor(string name, string hash)
		{
			if (string.IsNullOrEmpty(hash)) return name;
			string own = IslandNetwork.HashOf(name);
			if (own == hash) return name;
			string downloaded = IslandNetwork.DownloadName(name, hash);
			if (File.Exists(IslandSpawner.PathFor(downloaded))) { Debug.Log("[CUSTOM ISLANDS] '" + name + "' is played from the copy downloaded from its first host: " + downloaded); return downloaded; }
			string same = IslandNetwork.IsHash(hash) ? IslandNetwork.FileWithHash(hash, null) : null;
			if (same != null) { Debug.Log("[CUSTOM ISLANDS] '" + name + "' is played from " + same + ", the same island under another name"); return same; }
			if (IslandNetwork.IsHash(hash) && Raft_Network.IsHost) IslandNetwork.WantFromPlayers(name, hash);
			if (own != null) { Debug.LogWarning("[CUSTOM ISLANDS] '" + name + "' differs from the one this world was saved with (" + own + ", not " + hash + "): playing this PC's own until a player has that one"); return name; }
			return IslandNetwork.IsHash(hash) && Raft_Network.IsHost ? downloaded : name;
		}
	}
}
