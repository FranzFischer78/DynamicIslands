using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world travels with the players (AU43). After each of Raft's saves, and to a player who joins, the host sends its
	/// newest save of the world (Raft's World\&lt;name&gt;\&lt;date&gt;-Latest folder, packed and deflated) to the players. A player
	/// files it in their own Raft World folder, so any of them can load and host the latest world from Load Game without
	/// copying folders by hand (the mod's own copy, WorldCopy.cs, already travels).
	/// Where it goes: World\&lt;the host's world name&gt; when that folder doesn't exist or holds this world (MarkerFile names the
	/// world id); else World\&lt;name&gt; (&lt;first 8 of the world id&gt;) - a player's own world of the same name is never touched.
	/// Raft's own "-Latest" folder there becomes a plain dated backup, as Raft does on a save; of the folders received
	/// (ReceivedFile in them) only the newest KeepReceived stay. A pack over MaxBytes isn't sent (logged).
	/// </summary>
	public static class WorldSaveShare
	{
		/// <summary>In a world folder: the world id it holds ("&lt;guid&gt;"), written by the host on each save and by a player who received it.</summary>
		public const string MarkerFile = "CustomIslands-world.txt";
		/// <summary>In a save folder a player received from the host (only those are ever removed, oldest first).</summary>
		public const string ReceivedFile = "CustomIslands-received.txt";
		public const int KeepReceived = 3;
		public const int MaxBytes = 64 * 1024 * 1024;
		const int ChunkChars = 3000;
		const int Magic = 0x43495753; // "CIWS"

		/// <summary>Tests: the last save a player filed ("&lt;folder&gt;", relative to Raft's World folder), or null.</summary>
		public static string LastFiled { get; private set; }
		/// <summary>Tests: the last pack the host sent ("&lt;files&gt; files, &lt;bytes&gt; bytes"), or null.</summary>
		public static string LastSent { get; private set; }

		static int sendGeneration;
		static bool toldTooBig;

		#region Host

		/// <summary>Host, after Raft saved: marks the world folder with its id and sends the new save to every player.</summary>
		public static void AfterRaftSave(string worldFolder)
		{
			if (!Raft_Network.IsHost || worldFolder == null || SaveAndLoad.WorldGuid == Guid.Empty) return;
			try { SafeFile.WriteAllLines(Path.Combine(worldFolder, MarkerFile), new[] { SaveAndLoad.WorldGuid.ToString() }); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not mark Raft's world folder with its id: " + e.Message); }
			// (a frame later: Raft's save is complete on disk by then)
			if (IslandNetwork.InGame && DynamicIslands.instance != null) DynamicIslands.instance.StartCoroutine(SendNextFrame());
		}

		static IEnumerator SendNextFrame()
		{
			yield return null;
			Send(null);
		}

		/// <summary>Host: sends the newest save of the world being played to one player (who joined) or everyone.</summary>
		public static void Send(Network_UserId? to)
		{
			if (!Raft_Network.IsHost || SaveAndLoad.WorldGuid == Guid.Empty || DynamicIslands.instance == null) return;
			string world = WorldCopy.RaftWorldFolder;
			string save = world != null ? LatestSave(world) : null;
			if (save == null) return;
			byte[] packed;
			try { packed = Pack(Path.GetFileName(world), save); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not pack the world's save to send: " + e.Message); return; }
			if (packed.Length > MaxBytes)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] The world's save is too big to send to the players (" + packed.Length + " bytes)");
				if (!toldTooBig) { toldTooBig = true; DynamicIslands.Notify("This world's save is too big (" + (packed.Length >> 20) + " MB, over " + (MaxBytes >> 20) + " MB) to send to the players: whoever hosts it next needs a copy of the save folder", true, 8); }
				return;
			}
			// (a newer save to everyone stops one still going out to everyone: the players want the newest only)
			int gen = to.HasValue ? sendGeneration : ++sendGeneration;
			DynamicIslands.instance.StartCoroutine(SendParts(SaveAndLoad.WorldGuid.ToString(), Path.GetFileName(save), packed, to, gen));
		}

		static IEnumerator SendParts(string guid, string saveName, byte[] packed, Network_UserId? to, int gen)
		{
			string text = Convert.ToBase64String(packed);
			int count = Mathf.Max(1, (text.Length + ChunkChars - 1) / ChunkChars);
			float started = Time.realtimeSinceStartup;
			for (int i = 0; i < count; i++)
			{
				if (!to.HasValue && gen != sendGeneration) yield break;
				if (!Raft_Network.IsHost) yield break;
				var msg = new IslandNetMessage { Name = guid, Hash = saveName, Index = i, Count = count, Data = text.Substring(i * ChunkChars, Mathf.Min(ChunkChars, text.Length - i * ChunkChars)) };
				try { IslandNetwork.SendWorldSave(msg, to); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [net] Sending the world's save, part " + i + ": " + e.Message); }
				if ((i + 1) % IslandNetwork.ChunksPerFrame == 0) yield return null;
			}
			LastSent = saveName + ", " + packed.Length + " bytes";
			Debug.Log("[CUSTOM ISLANDS] [net] Sent the world's save " + saveName + " (" + packed.Length + " bytes, " + count + " parts in " + (Time.realtimeSinceStartup - started).ToString("F1") + " s) to " + (to.HasValue ? to.Value.ToString() : "every player"));
		}

		/// <summary>Raft's newest save folder of a world folder (&lt;date&gt;-Latest), or null.</summary>
		public static string LatestSave(string worldFolder)
		{
			try { return Directory.GetDirectories(worldFolder).FirstOrDefault(d => d.EndsWith("-Latest", StringComparison.OrdinalIgnoreCase)); }
			catch { return null; }
		}

		/// <summary>The pack: magic, world name, save folder name, then each file (path relative to the save folder with "/",
		/// length, bytes), all deflated after the magic.</summary>
		public static byte[] Pack(string worldName, string saveFolder)
		{
			string[] files = Directory.GetFiles(saveFolder, "*", SearchOption.AllDirectories).Where(f => !Path.GetFileName(f).Equals(ReceivedFile, StringComparison.OrdinalIgnoreCase)).ToArray();
			using (var ms = new MemoryStream())
			{
				using (var head = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) head.Write(Magic);
				using (var deflate = new DeflaterOutputStream(ms) { IsStreamOwner = false })
				using (var w = new BinaryWriter(deflate))
				{
					w.Write(worldName);
					w.Write(Path.GetFileName(saveFolder));
					w.Write(files.Length);
					foreach (string f in files)
					{
						byte[] bytes = System.IO.File.ReadAllBytes(f);
						w.Write(f.Substring(saveFolder.Length).TrimStart('\\', '/').Replace('\\', '/'));
						w.Write(bytes.Length);
						w.Write(bytes);
					}
				}
				return ms.ToArray();
			}
		}

		#endregion

		#region Player

		static readonly Dictionary<string, string[]> incoming = new Dictionary<string, string[]>();

		/// <summary>A player: a part of the host's save; when all have come, it is filed in Raft's World folder.</summary>
		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost || string.IsNullOrEmpty(msg.Name) || msg.Count <= 0 || msg.Index < 0 || msg.Index >= msg.Count) return;
			Guid guid;
			if (!Guid.TryParse(msg.Name, out guid) || guid == Guid.Empty) return;
			string key = msg.Name + "|" + msg.Hash;
			string[] parts;
			if (!incoming.TryGetValue(key, out parts) || parts.Length != msg.Count)
			{
				// (a newer save of this world: the parts of an older one won't be wanted)
				foreach (string old in incoming.Keys.Where(k => k.StartsWith(msg.Name + "|") && k != key).ToList()) incoming.Remove(old);
				incoming[key] = parts = new string[msg.Count];
			}
			parts[msg.Index] = msg.Data ?? "";
			if (parts.Any(p => p == null)) return;
			incoming.Remove(key);
			try
			{
				string where = File(Convert.FromBase64String(string.Concat(parts)), guid, SaveAndLoad.WorldPath);
				Debug.Log("[CUSTOM ISLANDS] Kept the host's save of this world, to host it later: " + where);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not keep the host's save of this world: " + e.Message); }
		}

		/// <summary>Unpacks a pack into &lt;worldsRoot&gt;\&lt;world folder&gt;\&lt;save folder&gt; (see the class) and returns that folder.
		/// Every name in it is checked before it becomes a path.</summary>
		public static string File(byte[] packed, Guid guid, string worldsRoot)
		{
			if (string.IsNullOrEmpty(worldsRoot)) throw new InvalidOperationException("no Raft World folder");
			using (var ms = new MemoryStream(packed))
			{
				using (var head = new BinaryReader(ms, System.Text.Encoding.UTF8, true))
					if (head.ReadInt32() != Magic) throw new InvalidDataException("not a world save");
				using (var inflate = new InflaterInputStream(ms) { IsStreamOwner = false })
				using (var r = new BinaryReader(inflate))
				{
					string worldName = r.ReadString(), saveName = r.ReadString();
					string problem = FileNames.ReceivedProblem(worldName) ?? FileNames.ReceivedProblem(saveName);
					if (problem != null) throw new InvalidDataException("a name " + problem);
					int n = r.ReadInt32();
					if (n < 0 || n > 10000) throw new InvalidDataException("bad file count " + n);
					var files = new List<KeyValuePair<string, byte[]>>();
					long total = 0;
					for (int i = 0; i < n; i++)
					{
						string rel = r.ReadString();
						string[] segs = rel.Split('/');
						string bad = segs.Select(s => FileNames.ReceivedProblem(s)).FirstOrDefault(p => p != null);
						if (bad != null) throw new InvalidDataException("a file's name " + bad + ": " + rel);
						int len = r.ReadInt32();
						total += len;
						if (len < 0 || total > 4L * MaxBytes) throw new InvalidDataException("too big");
						byte[] bytes = r.ReadBytes(len);
						if (bytes.Length != len) throw new InvalidDataException("cut short");
						files.Add(new KeyValuePair<string, byte[]>(Path.Combine(segs), bytes));
					}

					string world = WorldFolderFor(worldsRoot, worldName, guid);
					Directory.CreateDirectory(world);
					SafeFile.WriteAllLines(Path.Combine(world, MarkerFile), new[] { guid.ToString() });
					string target = Path.Combine(world, saveName);
					// (written beside it first: a half-written save never looks like the newest)
					string part = target + ".part";
					if (Directory.Exists(part)) Directory.Delete(part, true);
					foreach (var f in files)
					{
						string path = Path.Combine(part, f.Key);
						Directory.CreateDirectory(Path.GetDirectoryName(path));
						System.IO.File.WriteAllBytes(path, f.Value);
					}
					System.IO.File.WriteAllText(Path.Combine(part, ReceivedFile), DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
					// (the same save again - sent at a join: the new one replaces it; another "-Latest" becomes a dated backup, as Raft does)
					if (Directory.Exists(target)) Directory.Delete(target, true);
					foreach (string latest in Directory.GetDirectories(world).Where(d => d.EndsWith("-Latest", StringComparison.OrdinalIgnoreCase)))
					{
						string plain = latest.Substring(0, latest.Length - "-Latest".Length);
						if (!Directory.Exists(plain)) Directory.Move(latest, plain);
						else if (System.IO.File.Exists(Path.Combine(latest, ReceivedFile))) Directory.Delete(latest, true);
					}
					Directory.Move(part, target);
					Prune(world);
					LastFiled = Path.GetFileName(world) + "\\" + saveName;
					return target;
				}
			}
		}

		/// <summary>The world folder a received save goes in: the host's name if free or holding this world, else the name with the id.</summary>
		public static string WorldFolderFor(string worldsRoot, string worldName, Guid guid)
		{
			string plain = Path.Combine(worldsRoot, worldName);
			if (!Directory.Exists(plain) || HoldsWorld(plain, guid)) return plain;
			return Path.Combine(worldsRoot, worldName + " (" + guid.ToString().Substring(0, 8) + ")");
		}

		static bool HoldsWorld(string folder, Guid guid)
		{
			try
			{
				string marker = Path.Combine(folder, MarkerFile);
				return System.IO.File.Exists(marker) && System.IO.File.ReadAllLines(marker).Any(l => l.Trim().Equals(guid.ToString(), StringComparison.OrdinalIgnoreCase));
			}
			catch { return false; }
		}

		/// <summary>Of the save folders a player received, only the newest KeepReceived stay (Raft's own and the player's own saves are never removed).</summary>
		static void Prune(string world)
		{
			var received = Directory.GetDirectories(world).Where(d => System.IO.File.Exists(Path.Combine(d, ReceivedFile)))
				.OrderByDescending(d => Directory.GetLastWriteTimeUtc(Path.Combine(d, ReceivedFile))).ToList();
			foreach (string old in received.Skip(KeepReceived))
				try { Directory.Delete(old, true); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not remove an old received save: " + e.Message); }
		}

		#endregion
	}
}
