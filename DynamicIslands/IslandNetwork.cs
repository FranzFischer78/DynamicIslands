using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Steamworks;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// One message type for everything the mod sends between host and clients (RML serializes [Serializable] objects;
	/// arrays of primitives and strings are fine). Which fields are used depends on Kind.
	/// </summary>
	[Serializable]
	public class IslandNetMessage
	{
		public const int Islands = 1, Remove = 2, SyncRequest = 3, FileRequest = 4, FileChunk = 5;
		/// <summary>Host -> everyone (and each player who joins): the world's rules (WorldRules). Index = monster level, Count = build cost %.</summary>
		public const int WorldRules = 15;
		/// <summary>A player used something of an island that stays used (a looted chest, a trigger that fires once):
		/// Ids[0] = island, Index = state key, Count = in-game day. Clients send it to the host, the host to everyone.</summary>
		public const int ObjectUsed = 6;
		/// <summary>An island's quest moved on: Ids[0] = island, Index = step, Count = progress in it. Client -> host -> everyone.</summary>
		public const int QuestStep = 7;
		/// <summary>Host -> everyone: a rule brought a new island (WorldDirector). Name = title, Data = message, Offsets = where, relative to the raft.</summary>
		public const int Announce = 8;
		/// <summary>Host -> everyone: an object was shown/hidden or opened/closed (Behaviours). Ids[0] = island, Index = object, Count = bits (1 there, 2 open).</summary>
		public const int ObjectSet = 9;
		/// <summary>An object event (Behaviours): client -> host (do the shared actions), or host -> clients with FullList set
		/// (do the personal ones if near). Ids[0] = island, Index = object (0xFFFF = the island), Name = event.</summary>
		public const int EventFired = 10;
		/// <summary>The crew's story items and journal (StoryBook): client -> host a change (Name = give / take / page, Data =
		/// its fields), host -> everyone the whole state (Name = all, Data = its lines).</summary>
		public const int Story = 11;
		/// <summary>Host -> a player who just joined: where they stood on a custom island (PlayerPlaces). Ids[0] = island,
		/// Offsets = x,y,z from its middle.</summary>
		public const int PlayerPlace = 12;
		/// <summary>Host -> everyone: the world randomizer's settings and seed (WorldRandomizer). Data = RandomizerSettings.Encode().</summary>
		public const int Randomizer = 13;
		/// <summary>A thing only one player can have (Claims): client -> host "may I?" (Ids[0] = island, Index = state key),
		/// host -> that client the answer (Count 1 = granted, 0 = no, someone else has it).</summary>
		public const int Claim = 16;
		/// <summary>The level up system (PlayerLevels): host -> everyone "on"; host -> a joining player "state" (Data = their
		/// record); a player -> host "mine" (Data = their record now).</summary>
		public const int Levels = 14;
		/// <summary>Host -> everyone (and each player who joins): the world's options (WorldOptions). Data = "on=a,b;seed=n",
		/// Name = the private storages' builders (PrivateStorage).</summary>
		public const int WorldOptions = 17;
		/// <summary>Host -> everyone (after each save) and each player who joins: the world file (WorldCopy), so any player can
		/// host the world later. Name = world id, Hash = the copy's stamp, Index/Count = part, Data = text.</summary>
		public const int WorldCopy = 18;
		/// <summary>Host -> everyone (and each player who joins): the story chain (StoryChain) - Data = "on;steps|frequencies|unlocked|fired",
		/// Name = a banner to show ("title\ntext"), if any.</summary>
		public const int StoryChain = 19;
		/// <summary>Host -> everyone (when it changes, and each player who joins): the world's quest count (QuestCount, ROADMAP
		/// CW3) - Data = one line per quest, "group\tname\t1|0", so every player's journal shows the host's count.</summary>
		public const int QuestCount = 20;
		/// <summary>Host -> players: which island spot animals belong to (AU32/AU62) - Data = "objectIndex:islandId:spot;..."
		/// for one animal when it is made, for all of them to a player who joins.</summary>
		public const int CreatureSpots = 21;
		/// <summary>A tree chopped or an item picked up on a custom island (IslandObjectState, AU63): Ids = island, active (1/0),
		/// harvests left (-1 = none), Index = its ordinal, Count = in-game day. Client -> host, host -> everyone; an older
		/// version ignores it.</summary>
		public const int ObjectHarvest = 22;
		/// <summary>Host -> players: the host lacks an island file its world needs (AU6) - Name = the island's name, Hash = its
		/// content hash. A player with a file of that content sends it back as FileChunks (SendFile's path); an older one ignores it.</summary>
		public const int FileWanted = 23;
		/// <summary>Player -> host, as the host's copy of the world arrives on joining: the player's kept copy of this world is
		/// newer (AU26) - Name = world id, Data = "savecount;savedat ticks"; the host is warned.</summary>
		public const int WorldCopyNewer = 24;
		/// <summary>QuestStep for an island's further quests (n &gt; 0, LM4): Ids = island, quest number. Its own kind, so a version
		/// before LM4 ignores it - it read every QuestStep as the main quest's (a second quest's step moved the main one on).</summary>
		public const int QuestStepMore = 25;
		/// <summary>The world option Daily quest: the host's task ("state", Data = DailyQuest.Task), a player's part ("add":
		/// Index = day, Count) or reward taken ("got").</summary>
		public const int DailyQuest = 26;
		/// <summary>The world option Rogue shark, host to players: Index = the rogue's object index (0 = none), Count = no new one
		/// before this day; Name = "spawn", "dead" or "state" (a player who joins).</summary>
		public const int RogueShark = 27;
		/// <summary>Host -> everyone (after each of Raft's saves) and each player who joins: Raft's newest save of the world
		/// (AU43, WorldSaveShare): Name = world id, Hash = save folder name, Index/Count = part, Data = base64 part of the pack.</summary>
		public const int WorldSave = 28;
		/// <summary>The upgrade research table (UpgradeTable): host -> everyone "state" (Data = researched|learned item indexes)
		/// and "learned" (Index = upgrade, Data = by whom); a player -> host "research" (Index = item, Count = table's object
		/// index) and "learn" (Index = upgrade).</summary>
		public const int UpgradeTable = 29;
		/// <summary>Host -> everyone: a when/then moment (RF1/RF2). Ids[0] = island, Index = object (-1 = the island, -2 = all of
		/// it), Name = "sound" / "stop" (TriggerSounds, Data = the action's sound text), "camera" (CameraMoment, Data =
		/// marker, a line break, "look at|seconds|range") or "ending" (StoryCredits, Data = title, credits and note split by U+001F). An older version ignores it.</summary>
		public const int Moment = 33;
		/// <summary>Host -> players: Raft's vending machine on a custom island (SL7b, RaftVending) paid or emptied: Ids = island,
		/// state key, button + 1 (0 = empty), Count = day. Raft's own message can reach a player before the island is set up there
		/// (F195); this keeps the player's island state, and its machine, as the host has it. An older version ignores it.</summary>
		public const int VendingHatch = 34;
		/// <summary>Host -> everyone: the world option Storm seas (TODO 4e, StormSeas): a storm broke a raft floor. Name = "broke", Index =
		/// the storm, Count = floors broken in the world. Raft itself takes the floor away on every machine; this is the banner. An older version ignores it.</summary>
		public const int StormSeas = 35;
		/// <summary>Host -> everyone: the world option Whale breach (TODO 4j, WhaleBreach): a whale breaches. Index = its number, Offsets = { dx, dz from the host's raft, heading, roll side }. Looks only.</summary>
		public const int WhaleBreach = 36;
		/// <summary>Both ways: the world option Kraken arm (Kraken). Host -> everyone: Name "rise" (Index = the arm's number, Data = "block;side;seed;hits needed;stay s",
		/// Offsets[0] = seconds since it rose - a joiner gets it in the join reply), "hits" (Count = hits so far), "let" (beaten off), "tear" (Count = floors torn;
		/// Raft's own block removal takes the floors away), "away" (sent away, quietly). Player -> host: "hit" (Index = the arm). An older version ignores it.</summary>
		public const int Kraken = 37;
		/// <summary>Host -> everyone: an animal turned alpha (WorldRandomizer, Big Bruce too): Index = its object index, Offsets = { max health, health }
		/// as the host gave it then, so other players have it exactly (CreatureSpawner.HostHealth; MatchHealth stays for players who join later). An older version ignores it.</summary>
		public const int AlphaHealth = 38;
		public int Kind;

		// Islands: one entry per island. Offsets are x,z per island relative to the host's raft, so a world shift
		// crossing the message on the wire doesn't matter, and y the island's own height (world shifts are flat; a
		// joining player's raft is still settling - it was 11 m low, and every island arrived 11 m up: the persistence
		// test). FullList: the client drops islands not in the list.
		public int[] Ids;
		public string[] Names;
		public string[] Hashes;
		public float[] Offsets;
		/// <summary>Harvested trees / picked-up items per island (IslandObjectState.Encode), for players who join later.</summary>
		public string[] States;
		/// <summary>Names shown on the Receiver (Entry.Label).</summary>
		public string[] Labels;
		/// <summary>The rule that brought each island (Entry.Rule): players' notebooks find a main story island's quest and
		/// notes by it (QuestBook) - since 2026-10-04.</summary>
		public string[] Rules;
		public bool FullList;

		// FileRequest / FileChunk: island file with this name and content hash, chunk Index of Count (base64)
		public string Name;
		public string Hash;
		public int Index;
		public int Count;
		public string Data;
	}

	/// <summary>
	/// Keeps clients' custom islands in step with the host. The host owns the island list (IslandWorldState):
	/// it sends the whole list when a client asks after joining, each new island as it spawns, and removals.
	/// Clients keep their own copy of the list, stream the islands in and out by distance like the host does,
	/// and fetch island files they don't have from the host (saved as &lt;name&gt;_&lt;hash&gt;.island).
	/// </summary>
	public static class IslandNetwork
	{
		/// <summary>Raw bytes per file chunk (base64 makes it about a third bigger).</summary>
		const int ChunkBytes = 3000;
		const float SyncRetrySeconds = 5f;
		const int SyncMaxTries = 12;

		static int nextId = 1;
		static bool synced;
		static int syncTries;
		static float nextSyncTry;
		// Client: files being received, by hash
		static readonly Dictionary<string, string[]> incoming = new Dictionary<string, string[]>();
		static readonly HashSet<string> requested = new HashSet<string>();
		// Client: when each file was asked for (asked again when it doesn't come), and whether the player was told
		static readonly Dictionary<string, float> requestedAt = new Dictionary<string, float>();
		static readonly HashSet<string> toldWaiting = new HashSet<string>();
		// Client: the host's island names left out (not file names here - AU41), the player told once each
		static readonly HashSet<string> toldRefused = new HashSet<string>();
		static bool toldNoList;
		static float nextFileCheck;
		const float FileRetrySeconds = 30f, SlowSyncSeconds = 30f;
		// Host: content hash per island name (with the file time it was computed for)
		static readonly Dictionary<string, KeyValuePair<DateTime, string>> hashCache = new Dictionary<string, KeyValuePair<DateTime, string>>(StringComparer.OrdinalIgnoreCase);

		static bool InMultiplayerGame { get { return LoadSceneManager.IsGameSceneLoaded && RAPI.GetLocalPlayer() != null; } }

		/// <summary>In a world with the network: messages go out (or to the tests' loopback).</summary>
		public static bool InGame { get { return InMultiplayerGame || Loopback != null; } }

		// Client: the host's world has arrived (Raft_Network.OnWorldReceivedLate), so the raft is where the host's
		// is. Island offsets are relative to the raft: before this they would land around the scene's origin. Seen in
		// a two-player test: an island spawned while the client was still joining ended up 1 km away.
		static bool worldReceived, wasInGame;
		/// <summary>Client: when this player was first seen in a game without Raft's world-arrived event (-1: not so).</summary>
		static float inGameNoWorldSince = -1f;
		/// <summary>Client: how long a player may be in a game without the world-arrived event before asking anyway.</summary>
		internal const float MissedWorldSeconds = 10f;

		/// <summary>Whether this machine knows the world's islands: the host always, a client once the host's list came.</summary>
		public static bool HasList { get { return Raft_Network.IsHost || synced; } }

		/// <summary>Client: Raft has received the host's world. Starts the island sync afresh (a message that came
		/// in while joining, or the list from an earlier game, is dropped: the host's full list replaces it).</summary>
		public static void OnWorldReceived()
		{
			if (Raft_Network.IsHost) return;
			worldReceived = true;
			WorldRules.OnWorldReceived();
			WorldOptions.OnWorldReceived();
			global::DynamicIslands.Editor.StoryChain.OnWorldReceived();
			global::DynamicIslands.Editor.WorldCopy.OnWorldReceived();
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Select(e => e.Id).ToList(), false);
			synced = false;
			syncTries = 0;
			nextSyncTry = 0;
			hostToldVersion = false;
			// (a notice from an earlier world is not about this host - and the same one must be shown again here)
			VersionNotice = null;
			Log("The host's world arrived: asking for its islands");
		}

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [net] " + msg); }

		/// <summary>Host: a unique id for a new entry in the island list.</summary>
		public static int NewId() { return nextId++; }

		public static void OnWorldLoaded()
		{
			synced = Raft_Network.IsHost;
			syncTries = 0;
			nextSyncTry = 0;
			incoming.Clear();
			requested.Clear();
			requestedAt.Clear();
			toldWaiting.Clear();
			toldNoList = false;
			wanted.Clear();
			nextWantedAsk = 0f;
		}

		/// <summary>Called every frame. Clients keep asking the host for the island list until it arrives.</summary>
		public static void Tick()
		{
			// Left the game: the next one sends its world again. (Only on leaving: Raft raises OnWorldReceivedLate
			// before it counts the game scene as loaded.)
			if (LoadSceneManager.IsGameSceneLoaded) wasInGame = true;
			else if (wasInGame) { wasInGame = false; worldReceived = false; }
			// (a joined player whose mod never heard Raft's world-arrived event - the hook is put back each frame, and Raft may
			// raise it before then - never asked for the islands: player 2 sometimes had no list in the two-player tests. In
			// the game for a while without it, the world is here: ask anyway)
			if (!Raft_Network.IsHost && !worldReceived && !synced && InMultiplayerGame)
			{
				if (inGameNoWorldSince < 0f) inGameNoWorldSince = Time.unscaledTime;
				else if (Time.unscaledTime - inGameNoWorldSince > MissedWorldSeconds)
				{
					inGameNoWorldSince = -1f;
					Debug.LogWarning("[CUSTOM ISLANDS] [net] No world-arrived event from Raft " + MissedWorldSeconds + " s into the game: asking for the host's islands anyway");
					OnWorldReceived();
				}
			}
			else inGameNoWorldSince = -1f;
			if (!Raft_Network.IsHost && worldReceived && InMultiplayerGame && Time.unscaledTime >= nextFileCheck) { nextFileCheck = Time.unscaledTime + 5f; RetryLateFiles(); }
			// (the host: the island files it asked the players for, asked again now and then - AU6)
			if (Raft_Network.IsHost && wanted.Count > 0 && InMultiplayerGame && Time.unscaledTime >= nextWantedAsk) { nextWantedAsk = Time.unscaledTime + FileRetrySeconds; AskPlayers(null); }
			if (synced || Raft_Network.IsHost || !worldReceived || !InMultiplayerGame || Time.unscaledTime < nextSyncTry) return;
			// (after the first minute: still asking, slowly - a host busy loading, or a message lost, was left without islands for
			// the whole session before)
			if (syncTries >= SyncMaxTries && !toldNoList)
			{
				toldNoList = true;
				Debug.LogWarning("[CUSTOM ISLANDS] [net] The host hasn't sent its island list yet (does the host have Custom Islands?) - still asking every " + SlowSyncSeconds + " s");
				DynamicIslands.Notify("The host hasn't sent its custom islands yet (does the host have Custom Islands?). Still asking...", true);
			}
			syncTries++;
			nextSyncTry = Time.unscaledTime + (syncTries > SyncMaxTries ? SlowSyncSeconds : SyncRetrySeconds);
			// (with this player's version of the mod: the host says when they differ, and answers with its own)
			if (syncTries == 1) { HostAnswersClaims = false; HostAddsCounts = false; HostAnswersEvents = false; CreatureSpawner.HostSendsSpots = false; } // (a new host: known again from its answer)
			SendToHost(new IslandNetMessage { Kind = IslandNetMessage.SyncRequest, Name = VersionTag + LibraryPack.ModVersion, Data = BuildTag + Build });
		}

		/// <summary>A player: the host answers claims (it sent its version - claims are older than that - or answered one):
		/// a late answer is waited for, never taken as "yes" (both players looted one chest while the host was busy sending
		/// island files).</summary>
		public static bool HostAnswersClaims { get; internal set; }

		/// <summary>What this host does that older ones don't, told to players with its version ("counts": a player's quest
		/// events go to it as amounts it adds up, also for later steps - an older host took an amount for the total;
		/// "events": a player's event it checks again is answered, yes or no).</summary>
		const string HostCapabilities = "counts,spots,events";

		/// <summary>A player: the host answers an event whose crew checks it makes again (yes: the event back, no: its
		/// "otherwise" part), so the player's own part waits for that answer; else it runs at once, as before.</summary>
		public static bool HostAnswersEvents { get; internal set; }

		/// <summary>A player: the host adds quest counts up (since 2026-10-01); else the player sends its total, as before.</summary>
		public static bool HostAddsCounts { get; internal set; }

		const string VersionTag = "version:";
		const string BuildTag = "build=";

		static string build;
		/// <summary>Dev tests (CIFakeBuild): this PC pretends to be another build.</summary>
		public static string TestBuild;
		/// <summary>This build of the mod: the first 8 hex digits of a hash over every file in the .rmod (in name order), so
		/// two players with the same release have the same, and any other build differs ("?" when the files can't be read).</summary>
		public static string Build
		{
			get
			{
				if (TestBuild != null) return TestBuild;
				if (build != null) return build;
				try
				{
					var files = DynamicIslands.instance != null ? DynamicIslands.instance.modlistEntry.modinfo.modFiles : null;
					if (files == null || files.Count == 0) return "?";
					using (var md5 = System.Security.Cryptography.MD5.Create())
					{
						foreach (string name in files.Keys.OrderBy(k => k, StringComparer.Ordinal))
						{
							byte[] n = System.Text.Encoding.UTF8.GetBytes(name);
							md5.TransformBlock(n, 0, n.Length, null, 0);
							byte[] b = files[name] ?? new byte[0];
							md5.TransformBlock(b, 0, b.Length, null, 0);
						}
						md5.TransformFinalBlock(new byte[0], 0, 0);
						build = BitConverter.ToString(md5.Hash, 0, 4).Replace("-", "").ToLowerInvariant();
					}
					Log("This build: " + build);
					return build;
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [net] Build stamp: " + e.Message); return build = "?"; }
			}
		}

		/// <summary>The last version difference seen (tests), or "".</summary>
		public static string VersionNotice { get; private set; }

		/// <summary>A player: the host's version reply came (since this world arrived). A full island list without one first is
		/// from a host older than the version reply (it is sent ahead of the list) - AU35.</summary>
		static bool hostToldVersion;

		/// <summary>A player who joined (on the host) or the host (on a player) has another version of the mod: say so, once each,
		/// with both versions and which one to install. A version that couldn't be read ("?", modinfo.json unreadable) is
		/// unknown, never taken as the same (AU35).</summary>
		static void CompareVersions(string tag, string who, string data)
		{
			if (string.IsNullOrEmpty(tag) || !tag.StartsWith(VersionTag)) return;
			string theirs = tag.Substring(VersionTag.Length), mine = LibraryPack.ModVersion;
			string text;
			if (theirs == mine && VersionMismatch.IsKnown(mine))
			{
				// (the same version: every build of an alpha says the same, so the builds are compared too - a friend with a
				// 3.0 from before the upgrade table joined without a word and never saw it - AU81)
				string theirBuild = (data ?? "").Split(',').Where(x => x.StartsWith(BuildTag)).Select(x => x.Substring(BuildTag.Length)).FirstOrDefault();
				if (theirBuild == Build || Build == "?" || theirBuild == "?") return;
				text = theirBuild == null
					? (Raft_Network.IsHost ? who + " joined with an older build of Custom Islands " + mine + " (from before build stamps)." : "The host has an older build of Custom Islands " + mine + " (from before build stamps).") +
					  " Things added since may be missing for them: both should install the same, newest release."
					: (Raft_Network.IsHost ? who + " joined with another build of Custom Islands " + mine : "The host has another build of Custom Islands " + mine) +
					  " (theirs " + theirBuild + ", yours " + Build + "). Things one has may be missing for the other: both should install the same, newest release.";
			}
			else text = VersionMismatch.MismatchText(Raft_Network.IsHost, who, theirs, mine);
			if (VersionNotice == text) return;
			VersionNotice = text;
			Debug.LogWarning("[CUSTOM ISLANDS] [net] " + text);
			// (shown longer than other notices: a long text, and it explains whatever goes wrong next)
			global::DynamicIslands.DynamicIslands.Notify(text, true, 15);
		}

		#region Sending

		/// <summary>Dev tests: when set, messages go here instead of over the network.</summary>
		public static Action<IslandNetMessage> Loopback;

		static void SendToClients(IslandNetMessage msg)
		{
			if (Loopback != null) { Loopback(msg); return; }
			if (!Raft_Network.IsHost || !InMultiplayerGame) return;
			try { DynamicIslands.instance.SendNetworkMessage(msg, Target.Other, EP2PSend.k_EP2PSendReliable); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [net] Sending failed: " + e.Message); }
		}

		/// <summary>Host: a message for every other player.</summary>
		public static void SendToEveryone(IslandNetMessage msg) { SendToClients(msg); }

		/// <summary>A player: a message for the host (Kraken "hit").</summary>
		public static void SendToTheHost(IslandNetMessage msg) { if (Loopback != null) { Loopback(msg); return; } if (InMultiplayerGame) SendToHost(msg); }

		static void SendToPlayer(IslandNetMessage msg, Network_UserId player)
		{
			if (Loopback != null) { Loopback(msg); return; }
			try { DynamicIslands.instance.SendNetworkMessageToPlayer(msg, player, EP2PSend.k_EP2PSendReliable); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [net] Sending failed: " + e.Message); }
		}

		static void SendToHost(IslandNetMessage msg)
		{
			Raft_Network network = ComponentManager<Raft_Network>.Value;
			if (network == null) return;
			SendToPlayer(msg, network.HostID);
		}

		/// <summary>A story change: a client asks the host; the host sends the whole state to everyone.</summary>
		public static void SendStory(IslandNetMessage msg)
		{
			msg.Kind = IslandNetMessage.Story;
			if (Raft_Network.IsHost) SendToClients(msg);
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>Host: a story message for one player (T8: the answer to their take-out).</summary>
		public static void SendStoryTo(IslandNetMessage msg, Network_UserId player)
		{
			msg.Kind = IslandNetMessage.Story;
			if (Raft_Network.IsHost) SendToPlayer(msg, player);
		}

		/// <summary>Host: the world's rules, to everyone.</summary>
		public static void SendWorldRules(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) SendToClients(msg);
		}

		/// <summary>The level up system: the host to one player (to set) or everyone; a player to the host.</summary>
		/// <summary>The world file's copy: the host to one player (who joined) or everyone.</summary>
		public static void SendWorldCopy(IslandNetMessage msg, Network_UserId? to)
		{
			msg.Kind = IslandNetMessage.WorldCopy;
			if (!Raft_Network.IsHost) return;
			if (to.HasValue) SendToPlayer(msg, to.Value); else SendToClients(msg);
		}

		/// <summary>The upgrade research table: the host to one player or everyone; a player to the host.</summary>
		public static void SendUpgradeTable(IslandNetMessage msg, Network_UserId? to)
		{
			msg.Kind = IslandNetMessage.UpgradeTable;
			if (Raft_Network.IsHost) { if (to.HasValue) SendToPlayer(msg, to.Value); else SendToClients(msg); }
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>Raft's save of the world: the host to one player (who joined) or everyone (AU43).</summary>
		public static void SendWorldSave(IslandNetMessage msg, Network_UserId? to)
		{
			msg.Kind = IslandNetMessage.WorldSave;
			if (!Raft_Network.IsHost) return;
			if (to.HasValue) SendToPlayer(msg, to.Value); else SendToClients(msg);
		}

		/// <summary>A player -> the host: their kept copy of the world is newer than the host's (AU26).</summary>
		public static void SendWorldCopyNewer(IslandNetMessage msg)
		{
			msg.Kind = IslandNetMessage.WorldCopyNewer;
			if (!Raft_Network.IsHost && (InMultiplayerGame || Loopback != null)) SendToHost(msg);
		}

		/// <summary>Daily quest: the host to one player or everyone; a player to the host.</summary>
		public static void SendDaily(IslandNetMessage msg, Network_UserId? to)
		{
			msg.Kind = IslandNetMessage.DailyQuest;
			if (Raft_Network.IsHost) { if (to.HasValue) SendToPlayer(msg, to.Value); else SendToClients(msg); }
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		public static void SendLevels(IslandNetMessage msg, Network_UserId? to)
		{
			msg.Kind = IslandNetMessage.Levels;
			if (Raft_Network.IsHost) { if (to.HasValue) SendToPlayer(msg, to.Value); else SendToClients(msg); }
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>Host: tell clients about islands (new ones, or the whole list for a client that asked).</summary>
		internal static IslandNetMessage IslandsMessage(IEnumerable<IslandWorldState.Entry> entries, bool fullList)
		{
			// (an island still being made - its file not written yet - is left out: a player who joined in that moment got
			// it without a hash, it failed there, and its announcement later was ignored as known; it comes with that)
			var list = entries.Where(e => !e.Failed && HashOf(e.Name) != null).ToList();
			foreach (var e in list) IslandObjectState.Capture(e);
			// (the raft once for the whole message, as it is made - after the files were hashed - and the same place
			// (CustomIslandSpawner.RaftPosition) the player measures from as it arrives - AU41)
			Vector3 raft = CustomIslandSpawner.RaftPosition ?? Vector3.zero;
			var msg = new IslandNetMessage
			{
				Kind = IslandNetMessage.Islands,
				FullList = fullList,
				Ids = list.Select(e => e.Id).ToArray(),
				// (the island's own name: a host playing it from a downloaded copy - name_hash - still calls it by it)
				Names = list.Select(e => e.HostName ?? e.Name).ToArray(),
				Hashes = list.Select(e => HashOf(e.Name) ?? "").ToArray(),
				States = list.Select(e => IslandObjectState.Encode(e.State)).ToArray(),
				Labels = list.Select(e => e.Label ?? "").ToArray(),
				Rules = list.Select(e => e.Rule ?? "").ToArray(),
				Offsets = new float[list.Count * 3]
			};
			for (int i = 0; i < list.Count; i++)
			{
				Vector3 o = list[i].Position - raft;
				msg.Offsets[i * 3] = o.x; msg.Offsets[i * 3 + 1] = list[i].Position.y; msg.Offsets[i * 3 + 2] = o.z;
			}
			return msg;
		}

		public static void BroadcastAdded(IslandWorldState.Entry entry)
		{
			if (!Raft_Network.IsHost) return;
			SendToClients(IslandsMessage(new[] { entry }, false));
		}

		/// <summary>Tells the others that something of an island was used (host: to all clients; client: to the host, who passes it on).</summary>
		public static void SendUsed(int islandId, int key, int day)
		{
			var msg = new IslandNetMessage { Kind = IslandNetMessage.ObjectUsed, Ids = new[] { islandId }, Index = key, Count = day };
			if (Raft_Network.IsHost) SendToClients(msg);
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>Host: a vending machine's hatch as the host has it (RaftVending.Note), for every player.</summary>
		public static void SendVendingHatch(int islandId, int key, int value, int day)
		{
			if (Raft_Network.IsHost) SendToClients(new IslandNetMessage { Kind = IslandNetMessage.VendingHatch, Ids = new[] { islandId, key, value }, Count = day });
		}

		/// <summary>A tree or pickup was used on this machine's copy (host: to all clients; client: to the host, who passes it on).</summary>
		public static void SendHarvest(int islandId, int ord, bool active, int yield, int day)
		{
			var msg = new IslandNetMessage { Kind = IslandNetMessage.ObjectHarvest, Ids = new[] { islandId, active ? 1 : 0, yield }, Index = ord, Count = day };
			if (Raft_Network.IsHost) SendToClients(msg);
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>A player's quest event: its amount at that step, for the host to add ("add"; an older host takes it as the
		/// total, as before).</summary>
		public static void SendQuestAdd(int islandId, int step, int amount) { SendQuestAdd(islandId, 0, step, amount); }

		/// <summary>Quest n of an island (0 = the main quest): its number goes as a second id (an island's further quests, LM4).</summary>
		public static void SendQuestAdd(int islandId, int n, int step, int amount)
		{
			if (Raft_Network.IsHost) return;
			if (InMultiplayerGame || Loopback != null) SendToHost(new IslandNetMessage { Kind = QuestKind(n), Ids = n == 0 ? new[] { islandId } : new[] { islandId, n }, Index = step, Count = amount, Name = "add" });
		}

		public static void SendQuest(int islandId, int step, int progress) { SendQuest(islandId, 0, step, progress); }

		/// <summary>The message kind for quest n: QuestStep for the main quest, QuestStepMore for the others.</summary>
		static int QuestKind(int n) { return n == 0 ? IslandNetMessage.QuestStep : IslandNetMessage.QuestStepMore; }

		public static void SendQuest(int islandId, int n, int step, int progress)
		{
			var msg = new IslandNetMessage { Kind = QuestKind(n), Ids = n == 0 ? new[] { islandId } : new[] { islandId, n }, Index = step, Count = progress };
			if (Raft_Network.IsHost) SendToClients(msg);
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>Host: tells everyone a rule brought this island (the banner with its direction).</summary>
		public static void SendAnnounce(IslandWorldState.Entry e, string title, string message)
		{
			if (!Raft_Network.IsHost) return;
			Vector3 o = e.Position - (CustomIslandSpawner.RaftPosition ?? Vector3.zero);
			SendToClients(new IslandNetMessage { Kind = IslandNetMessage.Announce, Ids = new[] { e.Id }, Name = title, Data = message, Offsets = new[] { o.x, e.Position.y, o.z } });
		}

		/// <summary>Host: an object's shared state changed.</summary>
		public static void SendObjectSet(int islandId, int index, int bits)
		{
			if (!Raft_Network.IsHost) return;
			SendToClients(new IslandNetMessage { Kind = IslandNetMessage.ObjectSet, Ids = new[] { islandId }, Index = index, Count = bits });
		}

		/// <summary>An object event: a client tells the host; the host tells clients (fromHost: they do the personal part).</summary>
		public static void SendEvent(int islandId, int index, string ev, bool fromHost) { SendEvent(islandId, index, ev, fromHost, false); }

		/// <summary>SendEvent; waits: a player whose own part waits for the host's answer (Count 1 - an older host leaves it).</summary>
		public static void SendEvent(int islandId, int index, string ev, bool fromHost, bool waits)
		{
			var msg = new IslandNetMessage { Kind = IslandNetMessage.EventFired, Ids = new[] { islandId }, Index = index, Name = ev, FullList = fromHost, Count = waits ? 1 : 0 };
			if (Raft_Network.IsHost) { if (fromHost) SendToClients(msg); }
			else if (InMultiplayerGame || Loopback != null) SendToHost(msg);
		}

		/// <summary>Host -> one player: an object event's personal part for them (the "otherwise" part of an event the host
		/// refused - AU18 - or the event itself, the answer a waiting player's part waits for). Count 1: the answer to them.</summary>
		public static void SendEventTo(Network_UserId player, int islandId, int index, string ev)
		{
			if (!Raft_Network.IsHost) return;
			SendToPlayer(new IslandNetMessage { Kind = IslandNetMessage.EventFired, Ids = new[] { islandId }, Index = index, Name = ev, FullList = true, Count = 1 }, player);
		}

		/// <summary>Client: asks the host for a thing only one player can have (Claims). The question's number goes as Ids[1]:
		/// the host sends Ids back with its answer (every version that answers claims does).</summary>
		public static void SendClaim(int islandId, int key, int ask, int late)
		{
			if (!Raft_Network.IsHost && (InMultiplayerGame || Loopback != null)) SendToHost(new IslandNetMessage { Kind = IslandNetMessage.Claim, Ids = new[] { islandId, ask }, Index = key, Count = late });
		}
		public static void BroadcastRemoved(IEnumerable<int> ids)
		{
			int[] list = ids.ToArray();
			if (!Raft_Network.IsHost || list.Length == 0) return;
			SendToClients(new IslandNetMessage { Kind = IslandNetMessage.Remove, Ids = list });
		}

		#endregion

		#region Receiving

		/// <summary>Returns true if the message was ours.</summary>
		public static bool OnMessage(object message, Network_UserId from)
		{
			var msg = message as IslandNetMessage;
			if (msg == null) return false;
			try
			{
				switch (msg.Kind)
				{
					case IslandNetMessage.SyncRequest:
						// (a player: the host's answer with its version)
						if (!Raft_Network.IsHost) { if ((msg.Name ?? "").StartsWith(VersionTag)) { HostAnswersClaims = true; hostToldVersion = true; } HostAddsCounts = (msg.Data ?? "").Split(',').Contains("counts"); CreatureSpawner.HostSendsSpots = (msg.Data ?? "").Split(',').Contains("spots"); HostAnswersEvents = (msg.Data ?? "").Split(',').Contains("events"); CompareVersions(msg.Name, "The host", msg.Data); break; }
						if (Raft_Network.IsHost)
						{
							// (a player older than the version handshake sends no version; a name for who joined, if Raft shows one)
							string who = PrivateStorage.NameOf(from.Id);
							CompareVersions(string.IsNullOrEmpty(msg.Name) ? VersionTag + VersionMismatch.Older : msg.Name, who == "another player" || string.IsNullOrEmpty(who) ? "A player" : "'" + who + "'", msg.Data);
							// (the version first, alone in a message kind and with fields every version has, so an older player
							// can still read it: everything else follows it)
							SendToPlayer(new IslandNetMessage { Kind = IslandNetMessage.SyncRequest, Name = VersionTag + LibraryPack.ModVersion, Data = HostCapabilities + "," + BuildTag + Build }, from);
							Log("Sending the island list (" + IslandWorldState.Islands.Count + ") to " + from);
							// (each part on its own: one that fails is logged and the rest still goes - before, one failing part
							// dropped everything after it, and the player had the islands without the story or their levels - AU41)
							Network_UserId to = from;
							JoinPart("world rules", () => WorldRules.Message(), to);
							JoinPart("island list", () => IslandsMessage(IslandWorldState.Islands, true), to);
							JoinPart("story", () => StoryBook.StateMessage(), to);
							JoinPart("world options", () => WorldOptions.Message(), to);
							JoinPart("daily quest", () => global::DynamicIslands.Editor.DailyQuest.Message(), to);
							JoinPart("upgrade table", () => global::DynamicIslands.Editor.UpgradeTable.Message(), to);
							JoinPart("rogue shark", () => global::DynamicIslands.Editor.RogueShark.Message("state"), to);
							JoinPart("story chain", () => global::DynamicIslands.Editor.StoryChain.Message(), to);
							JoinPart("quest count", () => global::DynamicIslands.Editor.QuestCount.Message(), to);
							JoinPart("animal spots", () => CreatureSpawner.SpotsMessage(null), to);
							// (after the animal spots: the randomizer looks at every animal once it comes, and took an island's
							// animals for Raft's while their spots hadn't come yet)
							JoinPart("randomizer", () => WorldRandomizer.Message(), to);
							JoinPart("kraken arm", () => global::DynamicIslands.Editor.Kraken.Message(), to);
							// (after the list: the island it names is in the player's list then)
							JoinPart("player's place", () => PlayerPlaces.PlaceMessage(to.Id), to);
							try { IslandNetMessage levels = PlayerLevels.StateFor(to.Id); if (levels != null) SendLevels(levels, to); }
							catch (Exception e) { JoinPartFailed("levels", e); }
							// (their own copy of the world, to host it later)
							try { global::DynamicIslands.Editor.WorldCopy.Send(to); }
							catch (Exception e) { JoinPartFailed("world copy", e); }
							try { global::DynamicIslands.Editor.WorldSaveShare.Send(to); }
							catch (Exception e) { JoinPartFailed("world save", e); }
							// (island files the host lacks: the player who joined may have them - AU6)
							if (wanted.Count > 0) AskPlayers(to);
						}
						break;
					case IslandNetMessage.FileWanted:
						// (a player: the host lacks this file - sent back if this PC has one with that content, else nothing - AU6)
						if (!Raft_Network.IsHost) SendFile(msg.Name, msg.Hash, from, true);
						break;
					case IslandNetMessage.WorldCopyNewer:
						if (Raft_Network.IsHost) global::DynamicIslands.Editor.WorldCopy.OnPlayerNewer(msg, from);
						break;
					case IslandNetMessage.WorldRules:
						WorldRules.OnMessage(msg);
						break;
					case IslandNetMessage.PlayerPlace:
						if (!Raft_Network.IsHost && msg.Ids != null && msg.Ids.Length > 0 && msg.Offsets != null && msg.Offsets.Length >= 3)
						{
							IslandWorldState.Entry on = IslandWorldState.Islands.FirstOrDefault(e => e.Id == msg.Ids[0]);
							if (on != null) PlayerHold.GoTo(on.Position + new Vector3(msg.Offsets[0], msg.Offsets[1], msg.Offsets[2]), on.HostName);
						}
						break;
					case IslandNetMessage.FileRequest:
						if (Raft_Network.IsHost) SendFile(msg.Name, msg.Hash, from);
						break;
					case IslandNetMessage.Islands:
						if (!Raft_Network.IsHost) ReceiveIslands(msg);
						break;
					case IslandNetMessage.Remove:
						if (!Raft_Network.IsHost) IslandWorldState.RemoveIds(msg.Ids, false);
						break;
					case IslandNetMessage.FileChunk:
						// (the host too: a file it asked the players for - only hashes it asked for are taken - AU6)
						ReceiveChunk(msg);
						break;
					case IslandNetMessage.QuestStep:
					case IslandNetMessage.QuestStepMore:
						if (msg.Ids != null && msg.Ids.Length > 0)
						{
							// (a player's amount: the host adds it up and tells everyone the total)
							int questNo = msg.Ids.Length > 1 ? msg.Ids[1] : 0;
							// (CB2: a player's progress only on an island near them - loaded on their side)
							if (Raft_Network.IsHost && !PlayerTrust.MayMark(from.Id, msg.Ids[0], "quest step")) break;
							if (Raft_Network.IsHost && msg.Name == "add") { QuestTracker.AddFromPlayer(msg.Ids[0], questNo, msg.Index, msg.Count); break; }
							// (one it ignored - stale, or older than what it has - isn't passed on: it lowered the others' count)
							if (QuestTracker.Apply(msg.Ids[0], questNo, msg.Index, msg.Count) && Raft_Network.IsHost) SendToClients(msg);
						}
						break;
					case IslandNetMessage.ObjectSet:
						if (!Raft_Network.IsHost && msg.Ids != null && msg.Ids.Length > 0) Behaviours.ApplyRemote(msg.Ids[0], msg.Index, msg.Count);
						break;
					case IslandNetMessage.EventFired:
						// (CB2: a player's fired rule only on an island near them; the one who asked for an answer gets its "otherwise")
						if (msg.Ids != null && msg.Ids.Length > 0 && Raft_Network.IsHost && !PlayerTrust.MayMark(from.Id, msg.Ids[0], "'" + msg.Name + "'"))
						{
							if (msg.Count == 1 && !(msg.Name ?? "").EndsWith("!")) SendEventTo(from, msg.Ids[0], msg.Index, (msg.Name ?? "") + "!");
							break;
						}
						if (msg.Ids != null && msg.Ids.Length > 0) Behaviours.OnEventMessage(msg.Ids[0], msg.Index, msg.Name ?? "", msg.FullList, from, msg.Count == 1);
						break;
					case IslandNetMessage.Story:
						StoryBook.OnMessage(msg, from.Id, from);
						break;
					case IslandNetMessage.Randomizer:
						WorldRandomizer.OnMessage(msg);
						break;
					case IslandNetMessage.AlphaHealth:
						if (!Raft_Network.IsHost && msg.Offsets != null && msg.Offsets.Length >= 2) CreatureSpawner.HostHealth(unchecked((uint)msg.Index), msg.Offsets[0], msg.Offsets[1]);
						break;
					case IslandNetMessage.WorldOptions:
						WorldOptions.OnMessage(msg);
						break;
					case IslandNetMessage.DailyQuest:
						global::DynamicIslands.Editor.DailyQuest.OnMessage(msg, from);
						break;
					case IslandNetMessage.RogueShark:
						global::DynamicIslands.Editor.RogueShark.OnMessage(msg);
						break;
					case IslandNetMessage.Levels:
						PlayerLevels.OnMessage(msg, from);
						break;
					case IslandNetMessage.WorldCopy:
						global::DynamicIslands.Editor.WorldCopy.OnMessage(msg);
						break;
					case IslandNetMessage.WorldSave:
						global::DynamicIslands.Editor.WorldSaveShare.OnMessage(msg);
						break;
					case IslandNetMessage.StormSeas:
						global::DynamicIslands.Editor.StormSeas.OnMessage(msg);
						break;
					case IslandNetMessage.Kraken:
						global::DynamicIslands.Editor.Kraken.OnMessage(msg);
						break;
					case IslandNetMessage.WhaleBreach:
						global::DynamicIslands.Editor.WhaleBreach.OnMessage(msg);
						break;
					case IslandNetMessage.VendingHatch:
						if (!Raft_Network.IsHost && msg.Ids != null && msg.Ids.Length >= 3) RaftVending.OnHatchMessage(msg.Ids[0], msg.Ids[1], msg.Ids[2], msg.Count);
						break;
					case IslandNetMessage.Moment:
						if (msg.Name == "camera") global::DynamicIslands.Editor.CameraMoment.OnMessage(msg);
						else if (msg.Name == "bolt") global::DynamicIslands.Editor.LightningStrikes.OnMessage(msg);
						else if (msg.Name == "ufo") global::DynamicIslands.Editor.UfoSightings.OnMessage(msg);
						else if (msg.Name == "ending") global::DynamicIslands.Editor.StoryCredits.OnMessage(msg);
						else global::DynamicIslands.Editor.TriggerSounds.OnMessage(msg);
						break;
					case IslandNetMessage.UpgradeTable:
						global::DynamicIslands.Editor.UpgradeTable.OnMessage(msg, from);
						break;
					case IslandNetMessage.StoryChain:
						global::DynamicIslands.Editor.StoryChain.OnMessage(msg);
						break;
					case IslandNetMessage.QuestCount:
						global::DynamicIslands.Editor.QuestCount.OnMessage(msg);
						break;
					case IslandNetMessage.CreatureSpots:
						if (!Raft_Network.IsHost) CreatureSpawner.OnSpots(msg.Data);
						break;
					case IslandNetMessage.Announce:
						// (from the raft as it arrives, like an island list; none here yet: no banner pointing from the scene's origin)
						if (!Raft_Network.IsHost && worldReceived && msg.Offsets != null && msg.Offsets.Length >= 3 && CustomIslandSpawner.RaftPosition.HasValue)
							WorldDirector.Show(msg.Name ?? "", msg.Data ?? "", FromHost(CustomIslandSpawner.RaftPosition.Value, msg.Offsets, 0));
						break;
					case IslandNetMessage.Claim:
						Claims.OnMessage(msg, from.Id, answer => SendToPlayer(answer, from));
						break;
					case IslandNetMessage.ObjectHarvest:
						if (msg.Ids != null && msg.Ids.Length >= 3)
						{
							if (Raft_Network.IsHost && !PlayerTrust.MayMark(from.Id, msg.Ids[0], "harvest")) break; // (CB2)
							if (Raft_Network.IsHost) msg.Count = ContentState.Today; // (the host's day, as for ObjectUsed - AU60)
							if (IslandObjectState.OnHarvest(msg.Ids[0], msg.Index, msg.Ids[1] != 0, msg.Ids[2], msg.Count) && Raft_Network.IsHost) SendToClients(msg);
						}
						break;
					case IslandNetMessage.ObjectUsed:
						if (msg.Ids != null && msg.Ids.Length > 0)
						{
							// (a player's day isn't trusted - AU60: only the host makes things available again, and what a player
							// used gets the host's day, the one its regrow counts from - on every machine, as it is passed on so)
							if (Raft_Network.IsHost)
							{
								if (!PlayerTrust.MayMark(from.Id, msg.Ids[0], "use of " + msg.Index.ToString("X"))) break; // (CB2: code locks, signals, used things)
								if (msg.Count < 0) { Log("A player said " + msg.Index.ToString("X") + " on island " + msg.Ids[0] + " is available again: only the host decides that"); break; }
								msg.Count = ContentState.Today;
							}
							ContentState.ApplyUsed(msg.Ids[0], msg.Index, msg.Count);
							if (Raft_Network.IsHost) SendToClients(msg); // everyone else learns it from the host
						}
						break;
				}
			}
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] [net] Handling message " + msg.Kind + " failed: " + e); }
			return true;
		}

		/// <summary>Host: one part of the reply to a player who joined, made and sent on its own (nothing when it makes none).</summary>
		static void JoinPart(string what, Func<IslandNetMessage> make, Network_UserId to)
		{
			try
			{
				IslandNetMessage part = make();
				if (part != null) SendToPlayer(part, to);
			}
			catch (Exception e) { JoinPartFailed(what, e); }
		}

		static void JoinPartFailed(string what, Exception e)
		{
			Debug.LogError("[CUSTOM ISLANDS] [net] The reply to a player who joined: its " + what + " failed (the other parts are sent): " + e);
		}

		/// <summary>Where the host's island number i is here: beside this machine's raft as it is beside the host's, at its own height.</summary>
		static Vector3 FromHost(Vector3 raft, float[] offsets, int i)
		{
			return new Vector3(raft.x + offsets[i * 3], offsets[i * 3 + 1], raft.z + offsets[i * 3 + 2]);
		}

		static void ReceiveIslands(IslandNetMessage msg)
		{
			// (while joining: the raft isn't where the host's is yet; the full list asked for once the world is here has it)
			if (!worldReceived) { Log("Island message while joining: waiting for the host's world first"); return; }
			// (the host's version reply comes ahead of its list: none came, so the host is older than it - AU35)
			if (msg.FullList && !hostToldVersion) CompareVersions(VersionTag + VersionMismatch.Older, "The host", null);
			// (the raft as this message arrives, once for all its islands - the host took its own once as it made the message;
			// no raft here yet: nothing is placed around the scene's origin, the whole list is asked for again - AU41)
			Vector3? raftNow = CustomIslandSpawner.RaftPosition;
			if (!raftNow.HasValue)
			{
				Log("Island message before the raft is here: asking for the whole list again");
				synced = false; syncTries = 0; nextSyncTry = Time.unscaledTime + SyncRetrySeconds;
				return;
			}
			Vector3 raft = raftNow.Value;
			int n = msg.Ids != null ? msg.Ids.Length : 0;
			if (msg.FullList)
			{
				var keep = new HashSet<int>(msg.Ids ?? new int[0]);
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(e => !keep.Contains(e.Id)).Select(e => e.Id).ToArray(), false);
			}
			int added = 0, skipped = 0;
			for (int i = 0; i < n; i++)
			{
				// (an older or damaged message: an entry without its name or place is left out, not the whole list - AU35)
				bool whole = msg.Names != null && i < msg.Names.Length && !string.IsNullOrEmpty(msg.Names[i]) && msg.Offsets != null && i * 3 + 2 < msg.Offsets.Length;
				IslandWorldState.Entry known = IslandWorldState.Islands.FirstOrDefault(e => e.Id == msg.Ids[i]);
				if (known != null && msg.Rules != null && i < msg.Rules.Length && !string.IsNullOrEmpty(msg.Rules[i])) known.Rule = msg.Rules[i];
				if (known != null)
				{
					// (moved by the host: an island the players still need came back ahead of the raft - ReturningIslands)
					if (msg.Offsets != null && i * 3 + 2 < msg.Offsets.Length)
					{
						Vector3 at = FromHost(raft, msg.Offsets, i);
						if (new Vector2(at.x - known.Position.x, at.z - known.Position.z).magnitude > 5f)
						{
							if (known.Root != null) { IslandObjectState.Capture(known); IslandSpawner.Despawn(known.Root); known.Root = null; }
							known.Position = at;
							Log("Island " + known.Id + " '" + known.HostName + "' moved by the host to " + at.ToString("F0"));
						}
					}
					// (known without its file - it came while the host was still making it, or its file failed here: the
					// host's hash now lets it come after all; Resync didn't help before)
					string hash = msg.Hashes != null && i < msg.Hashes.Length ? msg.Hashes[i] ?? "" : "";
					if (!IsHash(hash)) hash = ""; // (part of a file's name here - AU41)
					if ((known.Failed || string.IsNullOrEmpty(known.Hash)) && hash.Length > 0 && known.Root == null && !known.Loading)
					{
						known.Hash = hash;
						known.Failed = false;
						known.WaitingForFile = false;
						ResolveFile(known);
						Log("Island " + known.Id + " '" + known.HostName + "' had no file here: trying again with the host's");
					}
					// (still waiting for its file after a Resync, which forgot the requests: asked for again - AU64)
					else if (known.WaitingForFile && known.Root == null && !string.IsNullOrEmpty(known.Hash) && !requested.Contains(known.Hash)) ResolveFile(known);
					continue;
				}
				// (the host's name and hash become a file's name here: one that isn't a plain file name - "..\..\x" wrote
				// outside the mod's folder - is left out, not written - AU41)
				string problem = ReceivedProblem(msg.Names[i], msg.Hashes != null && i < msg.Hashes.Length ? msg.Hashes[i] : null);
				if (problem != null)
				{
					Debug.LogWarning("[CUSTOM ISLANDS] [net] The host's island " + msg.Ids[i] + " is left out: " + problem);
					if (toldRefused.Add(msg.Names[i] ?? "")) DynamicIslands.Notify("An island from the host is left out: its name can't be a file name here (see the log, F10)", true);
					continue;
				}
				if (!whole) { skipped++; continue; }
				var entry = IslandWorldState.AddRemote(msg.Ids[i], msg.Names[i], msg.Hashes != null && i < msg.Hashes.Length ? msg.Hashes[i] ?? "" : "",
					FromHost(raft, msg.Offsets, i));
				if (msg.States != null && i < msg.States.Length) entry.State = IslandObjectState.Decode(msg.States[i]);
				if (msg.Labels != null && i < msg.Labels.Length) entry.Label = msg.Labels[i] ?? "";
				if (msg.Rules != null && i < msg.Rules.Length) entry.Rule = msg.Rules[i] ?? "";
				ResolveFile(entry);
				added++;
			}
			// (only once the whole list was taken: a single new island that came first no longer stops the asking, and a list
			// that failed half way is asked for again)
			if (msg.FullList) synced = true;
			Log("Host sent " + n + " island(s)" + (msg.FullList ? " (full list)" : "") + ", " + added + " new" + (skipped > 0 ? ", " + skipped + " left out (incomplete)" : ""));
		}

		/// <summary>
		/// Client: point the entry at a local file with the host's content. Uses the file of the same name when it
		/// matches, else a previously downloaded copy, else asks the host for it (the entry waits meanwhile).
		/// </summary>
		static void ResolveFile(IslandWorldState.Entry entry)
		{
			string hash = entry.Hash;
			if (string.IsNullOrEmpty(hash)) return; // host couldn't hash it; try the local file
			if (HashOf(entry.HostName) == hash) { entry.Name = entry.HostName; return; }
			string downloaded = DownloadName(entry.HostName, hash);
			if (HashOf(downloaded) == hash) { entry.Name = downloaded; return; }
			// (a kept copy whose content isn't the host's - cut short by a crash while it was written: it failed to read and the
			// island never loaded; asked for again below, the download writes over it - COV14)
			if (File.Exists(IslandSpawner.PathFor(downloaded))) Log("The kept copy " + downloaded + " isn't the host's (damaged): asking for it again");
			// (the same content downloaded for another island name - the request already answered: a copy of it, not a wait
			// of 30 s for a second download - review 2026-10-06)
			try
			{
				// (by its content too: the name alone found the damaged copy itself - COV14)
				string same = Directory.GetFiles(DynamicIslands.assetpath, "*_" + hash + IslandFile.Extension)
					.FirstOrDefault(f => HashOf(Path.GetFileNameWithoutExtension(f)) == hash);
				if (same != null)
				{
					File.Copy(same, IslandSpawner.PathFor(downloaded), true);
					IslandCache.ForgetFile(IslandSpawner.PathFor(downloaded)); // (not "missing" for 2 s more - AU41)
					entry.Name = downloaded;
					Log("Island file '" + entry.HostName + "' is the same as " + Path.GetFileName(same) + ": copied (" + hash + ")");
					return;
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Copying an island file with the same content: " + e.Message); }

			entry.Name = downloaded;
			entry.WaitingForFile = true;
			if (requested.Add(hash))
			{
				requestedAt[hash] = Time.unscaledTime;
				Log("Asking the host for island file '" + entry.HostName + "' (" + hash + ")");
				SendToHost(new IslandNetMessage { Kind = IslandNetMessage.FileRequest, Name = entry.HostName, Hash = hash });
			}
		}

		/// <summary>Dev tests: accept chunks for this hash as if we had asked for it.</summary>
		internal static void ExpectFile(string hash) { requested.Add(hash); }

		internal static string DownloadName(string name, string hash) { return name + "_" + hash; }

		/// <summary>A content hash as Hash makes it: 12 digits 0-9 a-f (no wildcards or folders for a file's name).</summary>
		public static bool IsHash(string hash)
		{
			return hash != null && hash.Length == 12 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
		}

		/// <summary>
		/// Why an island name and hash from another PC can't make a file's name here (AU41: the host's names went into paths
		/// unchecked), or null. The hash may be missing (an island the host couldn't hash).
		/// </summary>
		internal static string ReceivedProblem(string name, string hash)
		{
			string p = FileNames.ReceivedProblem(name);
			if (p != null) return "the name '" + Shown(name) + "' " + p;
			if (!string.IsNullOrEmpty(hash) && !IsHash(hash)) return "'" + Shown(hash) + "' isn't a content hash";
			return null;
		}

		static string Shown(string s) { return s == null ? "" : s.Length > 80 ? s.Substring(0, 80) + "..." : s; }

		/// <summary>
		/// True for island files downloaded from a host (or kept for a saved world): &lt;name&gt;_&lt;12 hex digits&gt;, where the
		/// digits are the file's own content hash. (A player's island named like "camp_202609281530" - a date and time are
		/// 12 digits too - is theirs: it takes part while sailing and is never cleaned up as a copy.)
		/// </summary>
		public static bool IsDownloadName(string name)
		{
			int i = name.LastIndexOf('_');
			if (i <= 0 || name.Length - i - 1 != 12 || !name.Substring(i + 1).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			// (the answer kept 10 s: the pool asks for every saved name on each pick, and each HashOf is two disk checks - with
			// hundreds of downloaded copies that was 0.1-0.7 s a pick on a player who joined many worlds, IM43)
			KeyValuePair<float, bool> known;
			if (downloadNames.TryGetValue(name, out known) && Time.unscaledTime - known.Key < 10f && Time.unscaledTime >= known.Key) return known.Value;
			string h = HashOf(name);
			bool yes = h == null || h == name.Substring(i + 1);
			downloadNames[name] = new KeyValuePair<float, bool>(Time.unscaledTime, yes);
			return yes;
		}

		static readonly Dictionary<string, KeyValuePair<float, bool>> downloadNames = new Dictionary<string, KeyValuePair<float, bool>>(StringComparer.OrdinalIgnoreCase);

		internal static void ReceiveChunk(IslandNetMessage msg)
		{
			if (!requested.Contains(msg.Hash) || msg.Count <= 0 || msg.Index < 0 || msg.Index >= msg.Count) return;
			// (the name the host sends with it is the saved file's - AU41)
			string problem = ReceivedProblem(msg.Name, msg.Hash);
			if (problem != null) { Debug.LogWarning("[CUSTOM ISLANDS] [net] An island file from the host isn't saved: " + problem); return; }
			string[] parts;
			if (!incoming.TryGetValue(msg.Hash, out parts) || parts.Length != msg.Count) incoming[msg.Hash] = parts = new string[msg.Count];
			parts[msg.Index] = msg.Data;
			// (the wait for a retry starts again with every chunk: a big file over a slow line was asked for again every 30 s
			// from the first request and never finished - AU14)
			requestedAt[msg.Hash] = Time.unscaledTime;
			if (parts.Any(p => p == null)) return;

			incoming.Remove(msg.Hash);
			byte[] bytes = Convert.FromBase64String(string.Concat(parts));
			if (Hash(bytes) != msg.Hash)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] [net] Island file '" + msg.Name + "' arrived damaged, asking again");
				// (the host keeps asking the players at its next round - AU6)
				if (Raft_Network.IsHost) return;
				requested.Remove(msg.Hash); RetryWaiting(msg.Hash); return;
			}
			string name = DownloadName(msg.Name, msg.Hash);
			Directory.CreateDirectory(DynamicIslands.assetpath);
			SafeFile.WriteAllBytes(IslandSpawner.PathFor(name), bytes);
			IslandCache.ForgetFile(IslandSpawner.PathFor(name)); // (not "missing" for 2 s more - AU41)
			CustomIslandSpawner.ForgetSize(name); // (a damaged copy read before it was fetched again kept its size as unreadable - COV14)
			Log("Received island file '" + msg.Name + "' (" + bytes.Length + " bytes), saved as " + name + IslandFile.Extension);
			foreach (var e in IslandWorldState.Islands.Where(e => e.Hash == msg.Hash))
			{
				// (two of the host's islands with the same content but other names: each entry waits for its own name's copy
				// - only the first was saved and the other never loaded, AU15)
				string want = DownloadName(e.HostName, msg.Hash);
				try { if (want != name && !File.Exists(IslandSpawner.PathFor(want))) { File.Copy(IslandSpawner.PathFor(name), IslandSpawner.PathFor(want)); IslandCache.ForgetFile(IslandSpawner.PathFor(want)); } }
				catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] [net] Copying '" + name + "' for '" + e.HostName + "': " + ex.Message); }
				// (the host: an island of its world that waited for this file - or played another version of it, not loaded
				// yet - plays the one the world was saved with now, and players who joined meanwhile get it - AU6)
				if (Raft_Network.IsHost && (e.WaitingForFile || (e.Root == null && !e.Loading && HashOf(e.Name) != msg.Hash)))
				{
					bool waited = e.WaitingForFile;
					e.Name = want;
					e.Failed = false;
					e.WaitingForFile = false;
					e.Excluded = false; // (MissingIslandsCheck: the file is here now)
					if (waited) SendToClients(IslandsMessage(new[] { e }, false));
					continue;
				}
				e.WaitingForFile = false;
				e.Excluded = false;
			}
			hashMissAt.Remove(msg.Hash);
			// (the host asked everyone: one answer is enough, a second player's parts are no longer taken)
			if (Raft_Network.IsHost) requested.Remove(msg.Hash);
			if (Raft_Network.IsHost && wanted.Remove(msg.Hash))
			{
				Log("A player had the island file '" + msg.Name + "' (" + msg.Hash + ") this host lacked: the world plays it");
				DynamicIslands.Notify("A player had the island '" + msg.Name + "' this world needs: it is on this PC now.");
			}
		}

		/// <summary>Host: island files the world needs that this PC lacks, by hash (with the island's name) - asked of the players (AU6).</summary>
		static readonly Dictionary<string, string> wanted = new Dictionary<string, string>();
		static float nextWantedAsk;

		/// <summary>The island files the host is asking the players for (tests): hashes.</summary>
		public static IEnumerable<string> Wanted { get { return wanted.Keys; } }

		/// <summary>
		/// Host: the world needs the island file with this content and this PC has none (an island saved by another host, a
		/// plan's coming island - AU6). The players are asked (now, each who joins, and every 30 s); one who has it sends it,
		/// it is kept as name_hash like a downloaded file, and islands waiting for it play it.
		/// </summary>
		public static void WantFromPlayers(string name, string hash)
		{
			if (!Raft_Network.IsHost || !IsHash(hash) || ReceivedProblem(name, hash) != null || wanted.ContainsKey(hash)) return;
			wanted[hash] = name;
			requested.Add(hash);
			Log("This host lacks the island file '" + name + "' (" + hash + "): asking the players for it");
			if (InGame) AskPlayers(null);
		}

		/// <summary>Host: asks one player (who joined) or everyone for the island files the host lacks.</summary>
		static void AskPlayers(Network_UserId? to)
		{
			foreach (var w in wanted.ToList())
			{
				requested.Add(w.Key);
				var msg = new IslandNetMessage { Kind = IslandNetMessage.FileWanted, Name = w.Value, Hash = w.Key };
				if (to.HasValue) SendToPlayer(msg, to.Value); else SendToClients(msg);
			}
		}

		static readonly Dictionary<string, float> hashMissAt = new Dictionary<string, float>();

		/// <summary>
		/// Any island file on this PC with this content (AU6): the preferred name or its name_hash copy, else another copy
		/// with that hash, else any island file of that content (an imported pack's island renamed "Camp (Pack title)").
		/// Null when there is none. Not found is remembered for 10 s: the first look hashes every island file.
		/// </summary>
		internal static string FileWithHash(string hash, string prefer)
		{
			if (!IsHash(hash)) return null;
			if (!string.IsNullOrEmpty(prefer))
			{
				if (HashOf(prefer) == hash) return prefer;
				if (HashOf(DownloadName(prefer, hash)) == hash) return DownloadName(prefer, hash);
			}
			float at;
			if (hashMissAt.TryGetValue(hash, out at) && Time.unscaledTime - at < 10f) return null;
			List<string> all;
			try { all = IslandSpawner.ListSavedIslands().ToList(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking for an island file by its content: " + e.Message); return null; }
			string found = all.Where(n => n.EndsWith("_" + hash, StringComparison.OrdinalIgnoreCase)).FirstOrDefault(n => HashOf(n) == hash) ?? all.FirstOrDefault(n => HashOf(n) == hash);
			if (found == null) hashMissAt[hash] = Time.unscaledTime; else hashMissAt.Remove(hash);
			return found;
		}

		/// <summary>
		/// Client: an island file asked for that hasn't come in 30 s (the message lost, the host busy, the file missing on the
		/// host) is asked for again; the player is told once which island is waited for. Before, it waited for good.
		/// </summary>
		static void RetryLateFiles()
		{
			foreach (var e in IslandWorldState.Islands.Where(x => x.WaitingForFile && !string.IsNullOrEmpty(x.Hash)).ToList())
			{
				float at;
				if (!requestedAt.TryGetValue(e.Hash, out at) || Time.unscaledTime - at < FileRetrySeconds) continue;
				if (toldWaiting.Add(e.Hash)) DynamicIslands.Notify("Waiting for the island '" + e.HostName + "' from the host - still asking", true);
				Log("Island file '" + e.HostName + "' (" + e.Hash + ") hasn't come: asking again (keeping the " + (incoming.ContainsKey(e.Hash) ? incoming[e.Hash].Count(x => x != null) : 0) + " parts that came)");
				requested.Remove(e.Hash);
				// (the parts that came are kept: the host's next sending fills in the rest - AU14)
				RetryWaiting(e.Hash);
			}
		}

		/// <summary>Client: asks the host for everything again (the Resync command).</summary>
		public static void Resync()
		{
			synced = false; syncTries = 0; nextSyncTry = 0; toldNoList = false;
			requested.Clear(); requestedAt.Clear(); incoming.Clear(); toldWaiting.Clear();
			Log("Resync: asking the host for its islands again");
		}

		static void RetryWaiting(string hash)
		{
			foreach (var e in IslandWorldState.Islands.Where(e => e.Hash == hash).ToList()) ResolveFile(e);
		}

		internal static void SendFile(string name, string hash, Network_UserId to) { SendFile(name, hash, to, false); }

		/// <summary>Sends an island file by its content hash (quiet: a player answering the host's FileWanted, which most
		/// players can't - nothing logged when this PC hasn't got it).</summary>
		internal static void SendFile(string name, string hash, Network_UserId to, bool quiet)
		{
			// (a host playing an island from a copy - name_hash, downloaded as a player or kept for a saved world - sends
			// that copy, under the island's own name: the player saves it as name_hash, the name its entry waits for; sent
			// as name_hash it was saved as name_hash_hash and the island never loaded)
			// (a player's name is a file's here: only plain file names in the mod's folder are sent - AU41)
			string problem = ReceivedProblem(name, hash);
			if (problem != null) { Debug.LogWarning("[CUSTOM ISLANDS] [net] " + to + " asked for an island file that isn't one: " + problem); return; }
			string file = name;
			if (HashOf(name) != hash && HashOf(DownloadName(name, hash)) == hash) file = DownloadName(name, hash);
			// (the same content under another name - a pack's island renamed on import, which a plan's island was brought
			// from: still sent under the island's own name - AU6)
			if (HashOf(file) != hash) file = FileWithHash(hash, null) ?? file;
			string path = IslandSpawner.PathFor(file);
			if (!File.Exists(path) || HashOf(file) != hash)
			{
				if (!quiet) Debug.LogWarning("[CUSTOM ISLANDS] [net] " + to + " asked for island '" + name + "' (" + hash + ") which the host no longer has");
				return;
			}
			if (quiet) Log("The host lacks island file '" + name + "' (" + hash + "): sending this PC's " + file);
			// (a player asking again while the file is still going out to them gets the one being sent, not another copy
			// alongside it - CB12; after SendingSeconds a stalled send counts as gone)
			string key = hash + "|" + to;
			float since;
			if (sending.TryGetValue(key, out since) && Time.realtimeSinceStartup - since < SendingSeconds) { Log("Island file '" + name + "' is still being sent to " + to + ": not sent twice"); return; }
			if (sending.Count >= 64) sending.Clear();
			sending[key] = Time.realtimeSinceStartup;
			byte[] bytes = File.ReadAllBytes(path);
			System.Collections.IEnumerator send = SendChunks(name, hash, bytes, to, key);
			if (Loopback != null) { while (send.MoveNext()) { } } // (tests: the loopback takes every chunk at once)
			else if (DynamicIslands.instance != null) DynamicIslands.instance.StartCoroutine(send);
		}

		/// <summary>Host: the files going out now, by hash and player, with when they started.</summary>
		static readonly Dictionary<string, float> sending = new Dictionary<string, float>();
		const float SendingSeconds = 60f;
		/// <summary>Tests: how many files are going out now.</summary>
		internal static int SendingCount { get { return sending.Count; } }

		/// <summary>Chunks sent a frame (ROADMAP M3: all ~300 chunks of a 900 KB island went out in one loop, a burst that
		/// a slow connection dropped messages of).</summary>
		internal const int ChunksPerFrame = 6;

		static System.Collections.IEnumerator SendChunks(string name, string hash, byte[] bytes, Network_UserId to, string key)
		{
			int count = Mathf.Max(1, (bytes.Length + ChunkBytes - 1) / ChunkBytes);
			float started = Time.realtimeSinceStartup;
			for (int i = 0; i < count; i++)
			{
				int len = Mathf.Min(ChunkBytes, bytes.Length - i * ChunkBytes);
				try { SendToPlayer(new IslandNetMessage { Kind = IslandNetMessage.FileChunk, Name = name, Hash = hash, Index = i, Count = count, Data = Convert.ToBase64String(bytes, i * ChunkBytes, len) }, to); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [net] Sending '" + name + "' chunk " + i + ": " + e.Message); }
				if ((i + 1) % ChunksPerFrame == 0) yield return null;
			}
			sending.Remove(key);
			Log("Sent island file '" + name + "' (" + bytes.Length + " bytes, " + count + " chunks in " + (Time.realtimeSinceStartup - started).ToString("F1") + " s) to " + to);
		}

		#endregion

		#region Hashes

		internal static string Hash(byte[] bytes)
		{
			using (var sha = SHA1.Create())
				return BitConverter.ToString(sha.ComputeHash(bytes), 0, 6).Replace("-", "").ToLowerInvariant();
		}

		/// <summary>Short content hash of a saved island file, or null if there's no such file.</summary>
		public static string HashOf(string name)
		{
			string path = IslandSpawner.PathFor(name);
			if (!File.Exists(path)) return null;
			DateTime t = File.GetLastWriteTimeUtc(path);
			KeyValuePair<DateTime, string> cached;
			if (hashCache.TryGetValue(name, out cached) && cached.Key == t) return cached.Value;
			string h = Hash(File.ReadAllBytes(path));
			hashCache[name] = new KeyValuePair<DateTime, string>(t, h);
			return h;
		}

		#endregion
	}
}
