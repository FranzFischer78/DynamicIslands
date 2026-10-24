using System;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The notice of a host or player with another version of the mod (AU35): both versions, and which one to install.
	/// The mod never asks the internet for a newer version and never downloads one (Franz, 2026-10-17: the only source
	/// of the mod is raftmodding.com; RML's own check reads modinfo.json's updateUrl there). Versions are compared only
	/// between the players of a multiplayer world (IslandNetwork.CompareVersions).
	/// </summary>
	public static class VersionMismatch
	{
		/// <summary>The mod's version when modinfo.json couldn't be read (ExperimentalNotice.Version).</summary>
		public const string Unknown = "?";
		/// <summary>The version of a host or player from before the mod sent its version.</summary>
		public const string Older = "an older version";

		/// <summary>A version that can be compared: read from modinfo.json, and told (not "?", not from before versions were sent).</summary>
		public static bool IsKnown(string version)
		{
			return !string.IsNullOrEmpty(version) && version != Unknown && version != Older && char.IsDigit(version[0]);
		}

		/// <summary>
		/// The notice of a version difference in multiplayer (IslandNetwork.CompareVersions): onHost = this machine hosts and
		/// who joined with theirs; else theirs is the host's. Names both versions and which one to install (the newer of the two).
		/// </summary>
		public static string MismatchText(bool onHost, string who, string theirs, string mine)
		{
			string head = onHost
				? who + " joined with Custom Islands " + theirs + " - you have " + mine + "."
				: "The host has Custom Islands " + theirs + " - you have " + mine + ".";
			string newer = null;
			if (IsKnown(theirs) && IsKnown(mine)) newer = LibraryPack.CompareVersions(theirs, mine) > 0 ? theirs : mine;
			else if (theirs == Older && IsKnown(mine)) newer = mine;
			string advice;
			if (newer == null)
				advice = " One version can't be told: both should install the newest Custom Islands from raftmodding.com.";
			else if (onHost)
				advice = newer == mine
					? " " + who + " should install Custom Islands " + mine + "."
					: " Update to Custom Islands " + theirs + " (or they install " + mine + ").";
			else
				advice = newer == theirs
					? " Install Custom Islands " + theirs + " to match the host."
					: " The host should update to Custom Islands " + mine + " (or you install " + theirs + ").";
			return head + advice + " Until then islands, quests and settings may not match, or not arrive at all.";
		}
	}
}
