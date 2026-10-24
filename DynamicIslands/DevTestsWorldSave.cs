using System;
using System.Collections;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Tests for the world's save travelling with the players (WorldSaveShare.cs, AU43).</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIWorldSave", docs: "Dev, in a saved world (host): packs Raft's newest save of this world as it is sent to the players and files it in a folder of its own under the temp folder, as a player does: same files and bytes, a player's own world of that name left alone, older saves made backups, only the newest received ones kept")]
		public static void WorldSaveCommand()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world"); return; }
			StartTest(WorldSaveRoutine());
		}

		static IEnumerator WorldSaveRoutine()
		{
			yield return null;
			bool ok = true;
			string world = WorldCopy.RaftWorldFolder;
			string latest = world != null ? WorldSaveShare.LatestSave(world) : null;
			if (latest == null) { Fail("no saved world (save it first): " + (world ?? "no world folder")); yield break; }
			string root = Path.Combine(Path.GetTempPath(), "CIWorldSave_" + DateTime.UtcNow.Ticks);
			Guid guid = SaveAndLoad.WorldGuid, other = Guid.NewGuid();
			string name = Path.GetFileName(world);
			try
			{
				// 1. the real save, packed and filed: the same files and bytes, marked as received and as this world
				byte[] pack = WorldSaveShare.Pack(name, latest);
				string filed = WorldSaveShare.File(pack, guid, root);
				string[] src = Directory.GetFiles(latest, "*", SearchOption.AllDirectories);
				int same = src.Count(f => { string g = Path.Combine(filed, f.Substring(latest.Length).TrimStart('\\')); return File.Exists(g) && File.ReadAllBytes(g).SequenceEqual(File.ReadAllBytes(f)); });
				Log("save " + Path.GetFileName(latest) + ": " + src.Length + " files, pack " + pack.Length + " bytes, filed in " + filed.Substring(root.Length));
				Check(ref ok, src.Length > 0 && same == src.Length, "every file arrives with the same bytes (" + same + "/" + src.Length + ")");
				Check(ref ok, Path.GetFileName(Path.GetDirectoryName(filed)) == name && Path.GetFileName(filed) == Path.GetFileName(latest), "filed as World\\" + name + "\\" + Path.GetFileName(latest));
				Check(ref ok, File.Exists(Path.Combine(filed, WorldSaveShare.ReceivedFile)) && File.Exists(Path.Combine(root, name, WorldSaveShare.MarkerFile)), "marked as received and as this world");
				Check(ref ok, WorldSaveShare.File(pack, guid, root) == filed && !Directory.Exists(filed + ".part"), "the same save again replaces it");

				// 2. a player's own world of the same name: left alone, the host's goes beside it
				string own = Path.Combine(root, "CI Same Name");
				Directory.CreateDirectory(Path.Combine(own, "01-Latest"));
				File.WriteAllText(Path.Combine(own, "01-Latest", "mine.rgd"), "mine");
				string src2 = Path.Combine(root, "_src", "02-Latest");
				Directory.CreateDirectory(Path.Combine(src2, "sub"));
				File.WriteAllText(Path.Combine(src2, "sub", "a.rgd"), "host");
				string beside = WorldSaveShare.File(WorldSaveShare.Pack("CI Same Name", src2), other, root);
				Check(ref ok, Path.GetFileName(Path.GetDirectoryName(beside)) == "CI Same Name (" + other.ToString().Substring(0, 8) + ")" && File.Exists(Path.Combine(beside, "sub", "a.rgd")), "a player's own world of that name: the host's goes beside it (" + Path.GetFileName(Path.GetDirectoryName(beside)) + ")");
				Check(ref ok, File.ReadAllText(Path.Combine(own, "01-Latest", "mine.rgd")) == "mine" && !File.Exists(Path.Combine(own, WorldSaveShare.MarkerFile)), "the player's own world is untouched");

				// 3. newer saves: the older "-Latest" becomes a backup, only the newest received ones stay, the player's own save stays
				string w = Path.Combine(root, "CI Rotate");
				Directory.CreateDirectory(Path.Combine(w, "00-own"));
				File.WriteAllText(Path.Combine(w, WorldSaveShare.MarkerFile), other.ToString());
				for (int i = 1; i <= WorldSaveShare.KeepReceived + 1; i++)
				{
					string s = Path.Combine(root, "_src", "1" + i + "-Latest");
					Directory.CreateDirectory(s);
					File.WriteAllText(Path.Combine(s, "w.rgd"), "save " + i);
					WorldSaveShare.File(WorldSaveShare.Pack("CI Rotate", s), other, root);
					System.Threading.Thread.Sleep(20);
				}
				string[] left = Directory.GetDirectories(w).Select(Path.GetFileName).OrderBy(x => x).ToArray();
				string want = "00-own,12,13,14-Latest";
				Log("rotated: " + string.Join(",", left));
				Check(ref ok, string.Join(",", left) == want, "older saves made backups, the oldest received one removed, the player's own kept (want " + want + ")");

				// 4. bad names never become paths
				bool refused;
				try { WorldSaveShare.File(WorldSaveShare.Pack("..", src2), other, root); refused = false; } catch (InvalidDataException) { refused = true; }
				Check(ref ok, refused && !Directory.Exists(Path.Combine(Path.GetDirectoryName(root), "02-Latest")), "a world named \"..\" is refused");
			}
			catch (Exception e) { Fail("world save: " + e); ok = false; }
			finally
			{
				try { Directory.Delete(root, true); } catch { }
			}
			if (ok) Log("PASS: world save"); else Fail("world save");
		}
	}
}
