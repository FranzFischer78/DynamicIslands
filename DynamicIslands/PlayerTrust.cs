using System;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// CB2: what the host takes from another player. A player's machine reports what it marked on an island - a code lock
	/// or signal ("used"), a fired rule, quest progress, a tree or pickup harvested - and the host passed it on to everyone.
	/// A player can only do those things on an island that is loaded on their side, which is within the unload distance of
	/// them (or the raft they are on); so the host now takes such a report only from a player that near the island
	/// (MayMark), and leaves the others out (logged). The host's own reports, players it can't see (just joined) and islands
	/// it doesn't know stay as before. Levels: see PlayerLevels ("mine" - the host keeps the EXP it credited).
	/// </summary>
	public static class PlayerTrust
	{
		/// <summary>Slack over the unload distance and the island's land radius (a report on its way while the player moves on).</summary>
		public const float Slack = 150f;

		/// <summary>
		/// Dev builds: the CI tests set a player's level record and use things on islands straight from the console, as
		/// before CB2 (the CI world commands switch it on; CITrust switches it off to test the host's checks). Never set in
		/// a release build: nothing there sets it.
		/// </summary>
		public static bool ForTests;

		/// <summary>The host's last refusal ("player what island"), and how many (tests).</summary>
		public static string LastRefused { get; private set; }
		public static int Refused { get; private set; }

		static ulong LocalId { get { try { Network_Player p = RAPI.GetLocalPlayer(); return p != null ? p.steamID.Id : 0UL; } catch { return 0UL; } } }

		/// <summary>
		/// Host: whether this player's report about the island is taken - they are within the unload distance (plus the
		/// island's land radius and some slack) of it. True for the host itself, a player the host can't see, an island it
		/// doesn't know, or with ForTests.
		/// </summary>
		public static bool MayMark(ulong player, int islandId, string what)
		{
			if (ForTests || !Raft_Network.IsHost || player == 0UL || player == LocalId) return true;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e == null) return true;
			Network_Player p = Players.All.FirstOrDefault(x => x != null && SteamOf(x) == player);
			if (p == null) return true;
			float d = Distance(p.transform.position, e);
			if (d <= Allowed(e)) return true;
			Refused++;
			LastRefused = player + " " + what + " " + e.HostName;
			Debug.Log("[CUSTOM ISLANDS] A player's " + what + " on '" + e.HostName + "' is refused: they are " + d.ToString("F0") + " m from it (more than " + Allowed(e).ToString("F0") + " m: it isn't loaded on their side)");
			return false;
		}

		/// <summary>How far from an island's centre (flat) a player may be for their report about it.</summary>
		public static float Allowed(IslandWorldState.Entry e)
		{
			float edge = e.WaitingForFile ? 0f : Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name));
			return WorldRules.UnloadDistance + edge + Slack;
		}

		static float Distance(Vector3 at, IslandWorldState.Entry e)
		{
			Vector3 d = at - e.Position;
			d.y = 0f;
			return d.magnitude;
		}

		static ulong SteamOf(Network_Player p) { try { return p.steamID.Id; } catch { return 0UL; } }

		/// <summary>Tests: forgets the count.</summary>
		public static void ResetCount() { Refused = 0; LastRefused = null; }
	}
}
