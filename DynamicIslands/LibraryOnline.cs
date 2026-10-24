using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DynamicIslands.Editor
{
	/// <summary>A file of a library entry, as index.json lists it.</summary>
	public class LibraryFileRef
	{
		public string Name = "", Sha256 = "";
		public long Size;
	}

	/// <summary>An entry of the island library (index.json): its info, where its files are, and the files.</summary>
	public class LibraryEntry
	{
		public LibraryInfo Info;
		public string Path = "", Updated = "";
		public long Size;
		public int Islands;
		/// <summary>An island entry's monsters (index.json "monsters", counted by build-index.ps1 as IslandDifficulty does), -1 when the list doesn't say.</summary>
		public int Monsters = -1;
		public List<LibraryFileRef> Files = new List<LibraryFileRef>();

		/// <summary>The files an install needs: the islands, the plan and the map types they bring (not the pictures).</summary>
		public IEnumerable<LibraryFileRef> InstallFiles
		{
			get { return Files.Where(f => f.Name.EndsWith(IslandFile.Extension, StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(WorldPlan.Extension, StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(MapTypeFiles.Extension, StringComparison.OrdinalIgnoreCase)); }
		}
	}

	/// <summary>
	/// The island library online (LIBRARY_TASKS.md, C): reads index.json from the library's address (a public GitHub
	/// repository; library.txt can point elsewhere or switch it off), downloads icons, pictures and entries, checks every
	/// file's SHA-256 and size, and installs through LibraryPack (the same rules as Import). Files come from the commit the
	/// list was built from, so the list and its files always match; if one doesn't match anyway, the list is read again once.
	/// It goes online only while the library window is open, and sends nothing but the downloads.
	/// </summary>
	public static class LibraryClient
	{
		/// <summary>The library: Franz's shared Google Drive folder (2026-10-01). Until then it was the GitHub repository below -
		/// a library.txt that still names that address (written by an older version) reads the new one.</summary>
		public const string DefaultAddress = "https://drive.google.com/drive/folders/1qXHPud7BAUILVrcEDPJn4FnGggrZdswi";
		public const string OldDefaultAddress = "https://raw.githubusercontent.com/SwedenJohansson/CustomIslands-Library/main/";
		public const int Format = 1;
		const float CacheMinutes = 10f;

		public enum State { NotInstalled, Installed, Update }

		/// <summary>Tests: an address used instead of library.txt's and the default (e.g. file:///... a local copy).</summary>
		public static string TestAddress;

		static List<LibraryEntry> entries;
		static string downloadBase;
		static DateTime fetchedAt;
		static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

		public static List<LibraryEntry> Entries { get { return entries; } }
		public static string LastError { get; private set; }

		static string SettingsPath { get { return System.IO.Path.Combine(DynamicIslands.assetpath, "library.txt"); } }
		static string CacheFolder { get { return System.IO.Path.Combine(LibraryPack.LibraryFolder, "cache"); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [library] " + msg); }

		#region Settings (library.txt)

		static string Setting(string key)
		{
			try
			{
				if (!File.Exists(SettingsPath))
					SafeFile.WriteAllText(SettingsPath, "# The island library (the ISLAND LIBRARY window). The library goes online only while that window is open,\r\n" +
						"# and sends nothing but the downloads.\r\n# online = on|off\r\nonline = on\r\n# address = where index.json is: a Google Drive folder link, or a web folder address ending in /\r\naddress = " + DefaultAddress + "\r\n" +
						"# key = a Google API key for the Drive API (optional: without one the folder's public listing is read)\r\n");
				foreach (string line in File.ReadAllLines(SettingsPath))
				{
					int eq = line.IndexOf('=');
					if (line.TrimStart().StartsWith("#") || eq < 1) continue;
					if (line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return line.Substring(eq + 1).Trim();
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + SettingsPath + ": " + e.Message); }
			return null;
		}

		/// <summary>Whether the library may go online (library.txt: online = off switches it off).</summary>
		public static bool Online { get { return TestAddress != null || !"off".Equals(Setting("online"), StringComparison.OrdinalIgnoreCase); } }

		public static string Address
		{
			get
			{
				string a = TestAddress ?? Setting("address");
				// (an older version wrote the GitHub address into library.txt: the library moved to Franz's Drive folder)
				if (string.IsNullOrEmpty(a) || a.TrimEnd('/').Equals(OldDefaultAddress.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) a = DefaultAddress;
				string id;
				if (LibraryDrive.IsDrive(a, out id)) return a;
				return a.EndsWith("/") ? a : a + "/";
			}
		}

		/// <summary>library.txt's Google API key for the Drive API, or null (the folder's public listing is read then).</summary>
		static string DriveKey { get { string k = TestAddress != null ? null : Setting("key"); return string.IsNullOrEmpty(k) ? null : k; } }

		/// <summary>Where a file of the library downloads from: a path in the Drive folder found by its id, or the web
		/// address (or local copy) beside the list. done(url or null, error or null).</summary>
		static IEnumerator FileUrl(string path, Action<string, string> done)
		{
			string root;
			if (!LibraryDrive.IsDrive(Address, out root)) { done(Url(downloadBase ?? Address, path), null); yield break; }
			string id = null, error = null;
			yield return LibraryDrive.Find(root, path, DriveKey, (i, err) => { id = i; error = err; });
			done(id != null ? LibraryDrive.DownloadUrl(id, DriveKey) : null, error);
		}

		#endregion

		#region The list

		/// <summary>A web address for a path in the library: each part encoded (spaces, å/ä/ö), the / kept.</summary>
		public static string Url(string baseUrl, string path)
		{
			return baseUrl + string.Join("/", path.Split('/').Select(Uri.EscapeDataString).ToArray());
		}

		static UnityWebRequest Get(string url)
		{
			var req = UnityWebRequest.Get(url);
			req.timeout = 30;
			return req;
		}

		static bool Failed(UnityWebRequest req) { return req.result != UnityWebRequest.Result.Success; }

		/// <summary>Reads the list (from the cache when it's under 10 minutes old, unless force). done(error or null).</summary>
		public static IEnumerator LoadIndex(bool force, Action<string> done)
		{
			LastError = null;
			if (!Online) { LastError = "The online library is switched off (online = off in Mods\\DynamicIslands\\library.txt)."; done(LastError); yield break; }
			if (!force && entries != null && (DateTime.Now - fetchedAt).TotalMinutes < CacheMinutes) { done(null); yield break; }
			string address = Address;
			string url, driveRoot;
			if (LibraryDrive.IsDrive(address, out driveRoot))
			{
				// (Drive: the folder is listed again - files copied in since show; then the list by its id)
				LibraryDrive.Forget();
				string id = null, error = null;
				yield return LibraryDrive.Find(driveRoot, "index.json", DriveKey, (i, err) => { id = i; error = err; });
				if (id == null)
				{
					LastError = "Can't read the island library: " + error + ". Packs someone sent you can still be installed: Import... in the island editor.";
					entries = null; done(LastError); yield break;
				}
				url = LibraryDrive.DownloadUrl(id, DriveKey);
			}
			// (a web address gets "?t=" so GitHub's cache hands out the newest list; a file address can't have one)
			else url = address + "index.json" + (address.StartsWith("http") ? "?t=" + DateTime.UtcNow.Ticks : "");
			string text = null;
			using (UnityWebRequest req = Get(url))
			{
				yield return req.SendWebRequest();
				if (Failed(req)) LastError = "Can't reach the island library (" + req.error + "). Packs someone sent you can still be installed: Import... in the island editor.";
				else text = req.downloadHandler.text;
			}
			if (text == null) { entries = null; done(LastError); yield break; }
			try { Parse(text, address); }
			catch (Exception e) { LastError = "The island library's list can't be read (" + e.Message + ")."; entries = null; }
			if (LastError == null) { fetchedAt = DateTime.Now; Log("The library lists " + entries.Count + " entries (" + address + ")"); }
			done(LastError);
		}

		static void Parse(string text, string address)
		{
			var root = LibraryJson.Parse(text.TrimStart('﻿')) as Dictionary<string, object>;
			if (root == null) throw new FormatException("not a JSON object");
			int format = LibraryJson.Int(root, "format", 1);
			if (format > Format) throw new FormatException("it's a newer kind of list - update Custom Islands to see the library");
			// (the files are where the list says: the commit it was built from; a local copy has them next to it)
			string download = LibraryJson.Str(root, "download");
			downloadBase = address.StartsWith("http") && download.StartsWith("http") ? (download.EndsWith("/") ? download : download + "/") : address;
			var list = new List<LibraryEntry>();
			foreach (Dictionary<string, object> o in LibraryJson.Objects(root, "entries"))
			{
				var e = new LibraryEntry
				{
					Info = LibraryInfo.From(o), Path = LibraryJson.Str(o, "path"), Updated = LibraryJson.Str(o, "updated"),
					Size = LibraryJson.Int(o, "size"), Islands = LibraryJson.Int(o, "islands"), Monsters = LibraryJson.Int(o, "monsters", -1),
					Files = LibraryJson.Objects(o, "files").Select(f => new LibraryFileRef { Name = LibraryJson.Str(f, "name"), Size = LibraryJson.Int(f, "size"), Sha256 = LibraryJson.Str(f, "sha256").ToLowerInvariant() }).ToList(),
				};
				if (e.Info.id.Length == 0) e.Info.id = LibraryJson.Str(o, "id");
				if (e.Info.id.Length == 0 || e.Path.Length == 0 || e.Path.Contains("..")) continue;
				list.Add(e);
			}
			entries = list;
		}

		/// <summary>Forgets the list (read again next time) - tests switching addresses.</summary>
		public static void Forget() { entries = null; fetchedAt = DateTime.MinValue; LibraryDrive.Forget(); }

		/// <summary>Whether an entry is installed here, and whether the library has a newer version.</summary>
		public static State StateOf(LibraryEntry e)
		{
			LibraryInstalled i = LibraryPack.Installed().FirstOrDefault(x => x.id.Equals(e.Info.id, StringComparison.OrdinalIgnoreCase));
			if (i == null) return State.NotInstalled;
			// (a file it installed was deleted since - in the editor, or by hand: it showed as Installed with Download off)
			if (i.files.Any(f => !File.Exists(LibraryPack.PathOf(f)))) return State.NotInstalled;
			if (e.Info.version > i.version) return State.Update;
			// (T10: a file changed in the library without a new version number - only for installs that recorded their files)
			return Changed(e, i) ? State.Update : State.Installed;
		}

		/// <summary>Whether the library's files differ from the ones this install came from (a file changed, added or gone).</summary>
		public static bool Changed(LibraryEntry e, LibraryInstalled i)
		{
			if (i.packed.Count == 0) return false;
			List<LibraryFileRef> now = e.InstallFiles.ToList();
			if (now.Count == 0) return false;
			if (now.Count != i.packed.Count) return true;
			string sha;
			return now.Any(f => !i.packed.TryGetValue(f.Name, out sha) || !sha.Equals(f.Sha256, StringComparison.OrdinalIgnoreCase));
		}

		#endregion

		#region Pictures

		/// <summary>An entry's picture (the icon, or picture n), from the cache or downloaded. done(texture or null).</summary>
		/// <summary>The largest side of a library picture in pixels: a small file can claim a huge picture (CB12).</summary>
		public const int MaxPictureSide = 4096;

		/// <summary>Whether a PNG or JPEG says it is larger than MaxPictureSide, read from its header before Unity unpacks it.</summary>
		public static bool TooLarge(byte[] b)
		{
			if (b == null || b.Length < 24) return false;
			if (b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G')
				return Big(b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19]) || Big(b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]);
			if (b[0] != 0xFF || b[1] != 0xD8) return false;
			// (JPEG: the frame header SOF0-SOF15, but not DHT C4, JPG C8 or DAC CC, holds height and width)
			int i = 2;
			while (i + 9 < b.Length)
			{
				if (b[i] != 0xFF) return false;
				int m = b[i + 1];
				if (m == 0xD8 || m == 0x01 || (m >= 0xD0 && m <= 0xD7)) { i += 2; continue; }
				if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC) return Big(b[i + 5] << 8 | b[i + 6]) || Big(b[i + 7] << 8 | b[i + 8]);
				i += 2 + (b[i + 2] << 8 | b[i + 3]);
			}
			return false;
		}

		static bool Big(int side) { return side <= 0 || side > MaxPictureSide; }

		public static IEnumerator Picture(LibraryEntry e, string name, Action<Texture2D> done)
		{
			LibraryFileRef f = e.Files.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (f == null || f.Size > 600 * 1024) { done(null); yield break; }
			// (a file's name in the cache: the index's id and names go through IdFrom - "..\\..\\x" or "C:\\x" from the index wrote
			// a picture anywhere)
			string key = LibraryPack.IdFrom(e.Info.id) + "-" + LibraryPack.IdFrom(f.Sha256.Length >= 12 ? f.Sha256.Substring(0, 12) : f.Name);
			Texture2D tex;
			if (textures.TryGetValue(key, out tex) && tex != null) { done(tex); yield break; }
			string cached = System.IO.Path.Combine(CacheFolder, key + ".img");
			byte[] bytes = null;
			try { if (File.Exists(cached)) bytes = File.ReadAllBytes(cached); } catch { }
			if (bytes == null)
			{
				string url = null;
				yield return FileUrl(e.Path + "/" + f.Name, (u, err) => url = u);
				if (url == null) { done(null); yield break; }
				using (UnityWebRequest req = Get(url))
				{
					yield return req.SendWebRequest();
					if (!Failed(req)) bytes = req.downloadHandler.data;
				}
				if (bytes == null || (f.Sha256.Length > 0 && LibraryPack.Sha256(bytes) != f.Sha256)) { done(null); yield break; }
				// (in one step: a half-written picture in the cache was read as the whole one next time - AU41)
				try { Directory.CreateDirectory(CacheFolder); SafeFile.WriteAllBytes(cached, bytes); } catch { }
			}
			if (TooLarge(bytes)) { Debug.LogWarning("[CUSTOM ISLANDS] A library picture of " + e.Path + " is larger than " + MaxPictureSide + " pixels a side: not shown"); done(null); yield break; }
			tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
			if (!tex.LoadImage(bytes)) { UnityEngine.Object.Destroy(tex); done(null); yield break; }
			textures[key] = tex;
			done(tex);
		}

		#endregion

		#region Downloading

		/// <summary>
		/// Downloads an entry's islands, plan and map types (each file's size and SHA-256 checked), checks them as a pack and installs
		/// it. progress(text) as it goes; done(report or null, error or null). Nothing is installed unless every file arrived.
		/// </summary>
		public static IEnumerator Download(LibraryEntry e, bool appearWhileSailing, bool replaceChanged, Action<string> progress, Action<LibraryPack.Report, string> done)
		{
			for (int attempt = 0; attempt < 2; attempt++)
			{
				var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
				List<LibraryFileRef> wanted = e.InstallFiles.ToList();
				long total = Math.Max(1, wanted.Sum(f => f.Size)), got = 0;
				string mismatch = null, failed = null;
				foreach (LibraryFileRef f in wanted)
				{
					if (!LibraryPack.IsSafeFileName(f.Name)) { done(null, "The library lists a file with a name that isn't allowed ('" + f.Name + "')."); yield break; }
					string url = null, where = null;
					yield return FileUrl(e.Path + "/" + f.Name, (u, err) => { url = u; where = err; });
					if (url == null) { failed = "'" + f.Name + "' isn't in the library (" + where + ")"; break; }
					using (UnityWebRequest req = Get(url))
					{
						UnityWebRequestAsyncOperation op = req.SendWebRequest();
						while (!op.isDone)
						{
							progress("Downloading '" + e.Info.title + "': " + Math.Min(100, (int)((got + (long)(req.downloadProgress * f.Size)) * 100 / total)) + " %");
							yield return null;
						}
						if (Failed(req)) { failed = "'" + f.Name + "' didn't download (" + req.error + ")"; break; }
						byte[] bytes = req.downloadHandler.data;
						if (bytes.Length != f.Size || LibraryPack.Sha256(bytes) != f.Sha256) { mismatch = f.Name; break; }
						files[f.Name] = bytes;
						got += f.Size;
					}
				}
				if (failed != null) { done(null, failed + ". Nothing was installed."); yield break; }
				if (mismatch != null)
				{
					// (the list and the files out of step - read the list again once, and try with it)
					if (attempt == 0)
					{
						Log("'" + mismatch + "' doesn't match the list: reading the list again");
						string err = null;
						yield return LoadIndex(true, x => err = x);
						LibraryEntry again = entries != null ? entries.FirstOrDefault(x => x.Info.id == e.Info.id) : null;
						if (err == null && again != null) { e = again; continue; }
					}
					done(null, "'" + mismatch + "' isn't what the library lists (a damaged or changed download). Nothing was installed - try again in a few minutes.");
					yield break;
				}
				progress("Installing '" + e.Info.title + "'...");
				yield return null;
				string error;
				LibraryPackContents pack = LibraryPack.FromFiles(CopyInfo(e.Info), files, out error);
				if (pack == null) { done(null, "It can't be installed: " + error); yield break; }
				LibraryPack.Report r = null;
				try { r = LibraryPack.Install(pack, appearWhileSailing, replaceChanged, LibraryPack.SourceLibrary); }
				catch (Exception ex) { done(null, "Installing failed: " + ex.Message); yield break; }
				done(r, null);
				yield break;
			}
		}

		static LibraryInfo CopyInfo(LibraryInfo i) { return LibraryInfo.FromJson(i.ToJson()); }

		#endregion
	}
}
