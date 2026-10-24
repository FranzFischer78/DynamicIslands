using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Private storages" (WorldOptions.PrivateStorage): a storage opens only for the player who built it.
	///   - Who built which: Raft places a block on every machine through the builder's own BlockCreator.CreateBlock (the
	///     host first, then each player from its message), so every machine notes the builder of each storage itself, by
	///     the block's ObjectIndex (the same everywhere and kept in saves). The host keeps the list in the world file
	///     ("@storages=index:builder;...") and gives it to each player who joins (with the options, kind 17).
	///   - Opening: a player looking at someone else's storage is told whose it is instead of "Open"; and Raft's
	///     StorageManager.OpenStorage (on the host, which also passes the opening on to the others, and on each machine)
	///     refuses it for anyone but the builder - so a request that slips through (an older client) does nothing either.
	///   - Storages built while the option was off (or before this version) have no builder: they open for everyone.
	/// </summary>
	public static class PrivateStorage
	{
		static readonly Dictionary<uint, ulong> builders = new Dictionary<uint, ulong>();
		// (names of builders seen in this world, for the hint - a player who left keeps their name)
		static readonly Dictionary<ulong, string> names = new Dictionary<ulong, string>();

		/// <summary>Tests: the last refusal ("storage 123: player 456 is not its builder 789").</summary>
		public static string LastRefusal { get; private set; }

		public static int Count { get { return builders.Count; } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [storage] " + msg); }

		internal static void Reset() { builders.Clear(); LastRefusal = null; }

		/// <summary>Who built this storage (0: nobody known - it opens for everyone).</summary>
		public static ulong BuilderOf(uint objectIndex) { ulong b; return builders.TryGetValue(objectIndex, out b) ? b : 0UL; }

		public static string NameOf(ulong id)
		{
			// (Raft shows no name tag over the local player: their Steam name)
			Network_Player local = RAPI.GetLocalPlayer();
			if (local != null && local.steamID.Id == id) { try { names[id] = Steamworks.SteamFriends.GetPersonaName(); return names[id]; } catch { } }
			foreach (Network_Player p in UnityEngine.Object.FindObjectsOfType<Network_Player>())
				if (p != null && p.steamID.Id == id && p.playerNameTextMesh != null && !string.IsNullOrEmpty(p.playerNameTextMesh.text)) { names[id] = p.playerNameTextMesh.text; return names[id]; }
			string n;
			return names.TryGetValue(id, out n) ? n : "another player";
		}

		/// <summary>Whether this player may open this storage.</summary>
		public static bool MayOpen(uint objectIndex, ulong player)
		{
			if (!WorldOptions.On(WorldOptions.PrivateStorage)) return true;
			ulong b = BuilderOf(objectIndex);
			if (b == 0UL || b == player) return true;
			// (ROADMAP T9: the host may open the storage of a builder who isn't in the game - it was locked for everyone for
			// good when its builder never came back)
			return player == HostId() && !InGame(b);
		}

		/// <summary>The game's host (its Steam id; 0 unknown).</summary>
		static ulong HostId()
		{
			try
			{
				Raft_Network network = ComponentManager<Raft_Network>.Value;
				if (Raft_Network.IsHost) { Network_Player local = RAPI.GetLocalPlayer(); return local != null ? local.steamID.Id : 0UL; }
				return network != null ? network.HostID.Id : 0UL;
			}
			catch { return 0UL; }
		}

		/// <summary>Whether a player is in the game now.</summary>
		static bool InGame(ulong id) { return UnityEngine.Object.FindObjectsOfType<Network_Player>().Any(p => p != null && p.steamID.Id == id); }

		#region The world file and the network

		internal static bool ReadLine(string key, string value)
		{
			if (key != "storages") return false;
			Decode(value);
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			Prune();
			if (builders.Count > 0) yield return "@storages=" + Encode();
		}

		/// <summary>
		/// Host, while Raft saves the world: forgets the builders of storages that are gone (taken down, sunk with their
		/// foundation) - the list only grew before (AU41). Careful not to forget a storage that is there: only in Raft's own
		/// save (the world is loaded then, never part way through loading), with the game scene and the raft there and at
		/// least one block on it, and a storage counts as there if any object of the scene has its number (inactive ones too).
		/// </summary>
		static void Prune()
		{
			if (builders.Count == 0) return;
			try
			{
				if (!Raft_Network.IsHost || !WorldCopy.InRaftSave || !LoadSceneManager.IsGameSceneLoaded || !CustomIslandSpawner.RaftPosition.HasValue) return;
				if (UnityEngine.Object.FindObjectsOfType<Block>().Length == 0) return; // (the raft's blocks aren't known yet)
				var there = new HashSet<uint>(Resources.FindObjectsOfTypeAll<Storage_Small>().Where(s => s != null).Select(s => s.ObjectIndex));
				List<uint> gone = builders.Keys.Where(k => !there.Contains(k)).ToList();
				if (gone.Count == 0) return;
				foreach (uint k in gone) builders.Remove(k);
				Log("Forgot the builders of " + gone.Count + " storage(s) no longer in the world (" + builders.Count + " left)");
				// (the players' lists came with the options at their join: they forget the same ones now)
				WorldOptions.Broadcast();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [storage] Looking for storages that are gone: " + e.Message); }
		}

		internal static bool HasState { get { return builders.Count > 0; } }

		public static string Encode()
		{
			return string.Join(";", builders.Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture) + ":" + kv.Value.ToString(CultureInfo.InvariantCulture)).ToArray());
		}

		internal static void Decode(string data)
		{
			builders.Clear();
			foreach (string p in (data ?? "").Split(';'))
			{
				string[] kv = p.Split(':');
				uint idx; ulong who;
				if (kv.Length == 2 && uint.TryParse(kv[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out idx) && ulong.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out who))
					builders[idx] = who;
			}
		}

		#endregion

		/// <summary>Every machine: a block was placed by this player (not a load or a join's copy of the raft).</summary>
		internal static void OnPlaced(Block block, Network_Player builder, bool replicating)
		{
			if (replicating || block == null || builder == null || !(block is Storage_Small)) return;
			if (!WorldOptions.On(WorldOptions.PrivateStorage)) return;
			uint idx = block.ObjectIndex;
			builders[idx] = builder.steamID.Id;
			NameOf(builder.steamID.Id);
			Log("Storage " + idx + " (" + block.buildableItem?.UniqueName + ") built by " + NameOf(builder.steamID.Id) + " (" + builder.steamID.Id + ")");
		}

		/// <summary>Tests and the check below: whether an opening by this player is refused (and why).</summary>
		internal static bool Refuses(Storage_Small storage, Network_Player player)
		{
			if (storage == null || player == null) return false;
			if (MayOpen(storage.ObjectIndex, player.steamID.Id)) return false;
			LastRefusal = "storage " + storage.ObjectIndex + ": " + player.steamID.Id + " is not its builder " + BuilderOf(storage.ObjectIndex);
			return true;
		}
	}

	/// <summary>Raft placed a block through a player's BlockCreator (on every machine): note the builder of a storage.</summary>
	[HarmonyPatch(typeof(BlockCreator), "CreateBlock")]
	static class PrivateStoragePlaced
	{
		static void Postfix(BlockCreator __instance, Block __result, bool replicating)
		{
			try
			{
				if (__result == null || !(__result is Storage_Small)) return;
				Network_Player builder = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
				PrivateStorage.OnPlaced(__result, builder, replicating);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [storage] Noting a builder: " + e.Message); }
		}
	}

	/// <summary>Any machine: Raft opens a storage for a player (the host passes it on to the others): not for anyone but its builder.</summary>
	[HarmonyPatch(typeof(StorageManager), "OpenStorage")]
	static class PrivateStorageOpen
	{
		static bool Prefix(StorageManager __instance, Storage_Small storage, ref bool __result)
		{
			try
			{
				Network_Player player = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
				if (!PrivateStorage.Refuses(storage, player)) return true;
				Debug.Log("[CUSTOM ISLANDS] [storage] Refused: " + PrivateStorage.LastRefusal);
				__result = false;
				return false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [storage] " + e.Message); return true; }
		}
	}

	/// <summary>
	/// The remover's machine: the hammer takes a storage down (RemovePlaceables.PickupBlock - it gives the cost back and asks
	/// the host to remove it): not someone else's private one (AU42 - it dropped what was inside for the taker).
	/// </summary>
	[HarmonyPatch(typeof(RemovePlaceables), "PickupBlock")]
	static class PrivateStorageRemove
	{
		static bool Prefix(RemovePlaceables __instance, Block block)
		{
			try
			{
				Storage_Small storage = block as Storage_Small;
				if (storage == null || !WorldOptions.On(WorldOptions.PrivateStorage)) return true;
				Network_Player player = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
				if (!PrivateStorage.Refuses(storage, player)) return true;
				Debug.Log("[CUSTOM ISLANDS] [storage] Not taken down: " + PrivateStorage.LastRefusal);
				DisplayTextManager hints = CustomNote.Hints;
				if (hints != null && player != null && player.IsLocalPlayer) hints.ShowText(PrivateStorage.NameOf(PrivateStorage.BuilderOf(storage.ObjectIndex)) + "'s storage (private): only they can take it down", KeyCode.None, 0, 0, false);
				return false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [storage] " + e.Message); return true; }
		}
	}

	/// <summary>The local player looks at someone else's storage: whose it is, instead of "Open" (and E does nothing).</summary>
	[HarmonyPatch(typeof(Storage_Small), "OnIsRayed")]
	static class PrivateStorageLook
	{
		static bool Prefix(Storage_Small __instance)
		{
			try
			{
				if (!WorldOptions.On(WorldOptions.PrivateStorage)) return true;
				Network_Player local = RAPI.GetLocalPlayer();
				if (local == null || !PrivateStorage.Refuses(__instance, local)) return true;
				DisplayTextManager hints = CustomNote.Hints;
				if (hints != null) hints.ShowText(PrivateStorage.NameOf(PrivateStorage.BuilderOf(__instance.ObjectIndex)) + "'s storage (private)", KeyCode.None, 0, 0, false);
				return false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [storage] " + e.Message); return true; }
		}
	}
}
