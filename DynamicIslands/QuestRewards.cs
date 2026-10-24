using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RaftModLoader;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Each crew member's share of a quest's Raft-item reward (ROADMAP LM8): a player who wasn't at the island when its
	/// quest was done (or joined the world later) gets the reward when they come to the island - once. This machine's own
	/// record, per world: Mods\DynamicIslands\worlds\&lt;world&gt;.rewards ("rewarded &lt;island&gt;" / "owed &lt;island&gt;").
	/// </summary>
	public static class QuestRewards
	{
		// (the world and the player it was read for: before the local player is there the id is "local" - review 2026-10-06)
		static string loadedFor;
		/// <summary>The record is there but couldn't be read (locked by an antivirus or a sync): not saved over until it is.</summary>
		static bool readFailed;
		static readonly HashSet<string> rewarded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		static readonly HashSet<string> owed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// (per world and player: a player who joins uses the host's world id, and two players on one PC - or a host who
		// later joins its own world - each have their own record)
		static string FilePath { get { return Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), SaveAndLoad.WorldGuid + "-" + PlayerId + ".rewards"); } }
		/// <summary>The record before 2026-10-06, by world only: kept by the PC that hosted the world (it was that player's).</summary>
		static string OldFilePath { get { return Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), SaveAndLoad.WorldGuid + ".rewards"); } }

		static string PlayerId
		{
			get
			{
				try { Network_Player p = RAPI.GetLocalPlayer(); if (p != null) return p.steamID.Id.ToString(System.Globalization.CultureInfo.InvariantCulture); } catch { }
				return "local";
			}
		}

		static void Load()
		{
			string key = SaveAndLoad.WorldGuid + "-" + PlayerId;
			if (loadedFor == key) return;
			// (what happened while the player wasn't known yet is kept, and saved with the player's own record)
			bool merge = loadedFor == SaveAndLoad.WorldGuid + "-local";
			loadedFor = key;
			readFailed = false;
			if (!merge) { rewarded.Clear(); owed.Clear(); }
			try
			{
				string path = FilePath;
				if (!File.Exists(path) && Raft_Network.IsHost && File.Exists(OldFilePath)) path = OldFilePath;
				if (File.Exists(path))
					foreach (string line in File.ReadAllLines(path))
					{
						if (line.StartsWith("rewarded ")) rewarded.Add(line.Substring(9).Trim());
						else if (line.StartsWith("owed ")) owed.Add(line.Substring(5).Trim());
					}
				if (merge && (rewarded.Count > 0 || owed.Count > 0)) Save();
			}
			// (what this session gives is still remembered; the file isn't written over with only that - every reward given
			// before came again)
			catch (Exception e) { readFailed = true; Debug.LogWarning("[CUSTOM ISLANDS] Quest rewards file: " + e.Message); }
		}

		/// <summary>Read the file again when next asked (an island was renamed in it).</summary>
		internal static void Forget() { loadedFor = null; }

		/// <summary>Tests: this player's rewards of an island's quests (key, key#questN) are forgotten, so a test island run again
		/// in the same world pays again.</summary>
		internal static void ForgetIsland(string island)
		{
			Load();
			Predicate<string> his = k => k.Equals(island, StringComparison.OrdinalIgnoreCase) || k.StartsWith(island + "#", StringComparison.OrdinalIgnoreCase);
			if (rewarded.RemoveWhere(his) + owed.RemoveWhere(his) > 0) Save();
		}

		/// <summary>The file this player's rewards of the world are kept in (logs).</summary>
		internal static string WhereKept { get { return Path.GetFileName(FilePath); } }

		static void Save()
		{
			// (not before the player is known: "local"'s small record would be written over the player's own later)
			if (PlayerId == "local") { Debug.LogWarning("[CUSTOM ISLANDS] Quest rewards: not saved yet - the player isn't there yet"); return; }
			if (readFailed) { Debug.LogWarning("[CUSTOM ISLANDS] Quest rewards: not saved - the file couldn't be read, so it stays as it was"); return; }
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				// (in one step: a crash part way no longer leaves half a file, and the rewards given twice - AU41)
				SafeFile.WriteAllLines(FilePath, rewarded.Select(r => "rewarded " + r).Concat(owed.Where(o => !rewarded.Contains(o)).Select(o => "owed " + o)).ToArray());
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Quest rewards file: " + e.Message); }
		}

		/// <summary>Whether this machine's player got the island's quest reward already in this world.</summary>
		public static bool Rewarded(string island) { Load(); return rewarded.Contains(island); }

		/// <summary>Whether this machine's player still has the island's reward to come.</summary>
		public static bool Owed(string island) { Load(); return owed.Contains(island) && !rewarded.Contains(island); }

		/// <summary>The quest was done: give the reward now (near) or keep it for later. False if given before.</summary>
		public static bool OnCompleted(string island, bool near, Action give)
		{
			Load();
			if (rewarded.Contains(island)) return false;
			if (near) { give(); rewarded.Add(island); owed.Remove(island); }
			else owed.Add(island);
			Save();
			return true;
		}

		/// <summary>The player has come to an island whose reward is owed: give it.</summary>
		public static void Collect(string island, Action give)
		{
			Load();
			if (!owed.Contains(island) || rewarded.Contains(island)) return;
			give();
			rewarded.Add(island); owed.Remove(island);
			Save();
		}
	}
}
