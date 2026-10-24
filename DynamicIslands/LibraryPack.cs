using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// What a library entry says about itself (info.json, the same in an exported .zip and in the island library on
	/// GitHub - _ProjectDocs\LIBRARY_TASKS.md). No game settings: the player's own New Game choices always apply.
	/// </summary>
	[Serializable]
	public class LibraryInfo
	{
		public string id = "", kind = LibraryPack.KindIsland, title = "", author = "", summary = "", description = "";
		public int version = 1;
		public string[] tags = new string[0];
		public string players = "1-8", length = "", icon = "", plan = "", minModVersion = "", created = "", basedOn = "";
		public string[] pictures = new string[0];
		public bool remix = true, featured = false;

		public bool IsPlan { get { return kind == LibraryPack.KindPlan; } }

		public string ToJson()
		{
			var o = new Dictionary<string, object>
			{
				{ "id", id }, { "kind", kind }, { "title", title }, { "author", author }, { "version", version }, { "summary", summary }, { "description", description },
				{ "tags", tags ?? new string[0] }, { "players", players }, { "length", length }, { "icon", icon }, { "pictures", pictures ?? new string[0] },
			};
			if (IsPlan) o["plan"] = plan;
			o["remix"] = remix;
			o["featured"] = featured;
			o["minModVersion"] = minModVersion;
			o["created"] = created;
			if (basedOn.Length > 0) o["basedOn"] = basedOn;
			return LibraryJson.Write(o);
		}

		/// <summary>An info from a JSON object (what's missing keeps its default).</summary>
		public static LibraryInfo From(Dictionary<string, object> o)
		{
			var d = new LibraryInfo();
			return new LibraryInfo
			{
				id = LibraryJson.Str(o, "id"), kind = LibraryJson.Str(o, "kind", d.kind), title = Plain(LibraryJson.Str(o, "title"), 80), author = Plain(LibraryJson.Str(o, "author"), 60),
				version = Math.Max(1, LibraryJson.Int(o, "version", 1)), summary = Plain(LibraryJson.Str(o, "summary"), 300), description = Plain(LibraryJson.Str(o, "description"), 4000),
				tags = LibraryJson.Strings(o, "tags").Select(x => Plain(x, 30)).ToArray(), players = LibraryJson.Str(o, "players", d.players), length = LibraryJson.Str(o, "length"), icon = LibraryJson.Str(o, "icon"),
				pictures = LibraryJson.Strings(o, "pictures"), plan = LibraryJson.Str(o, "plan"), remix = LibraryJson.Bool(o, "remix", true), featured = LibraryJson.Bool(o, "featured"),
				minModVersion = LibraryJson.Str(o, "minModVersion"), created = LibraryJson.Str(o, "created"), basedOn = LibraryJson.Str(o, "basedOn"),
			};
		}

		static readonly System.Text.RegularExpressions.Regex RichTag = new System.Text.RegularExpressions.Regex(@"</?(size|color|b|i|material|quad|sprite|link|font|mark|align|voffset|cspace|indent|line-height|pos|rotate|s|u|sub|sup|alpha|nobr|page|br)(=[^>]*)?>",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		/// <summary>
		/// An entry's text as plain text (ROADMAP X6, test UL7): Unity's rich text tags taken out - a "&lt;size=300&gt;" title
		/// covered the library window - tabs as spaces, other control characters gone, and at most max characters.
		/// </summary>
		public static string Plain(string s, int max)
		{
			if (string.IsNullOrEmpty(s)) return "";
			string t = RichTag.Replace(s, "").Replace('\t', ' ');
			t = new string(t.Where(ch => ch == '\n' || !char.IsControl(ch)).ToArray()).Trim();
			return t.Length > max ? t.Substring(0, max - 3).TrimEnd() + "..." : t;
		}

		public static LibraryInfo FromJson(string json)
		{
			var o = LibraryJson.Parse(json) as Dictionary<string, object>;
			if (o == null) throw new FormatException("not a JSON object");
			return From(o);
		}
	}

	/// <summary>One installed (downloaded or imported) entry, as library\installed.json remembers it.</summary>
	[Serializable]
	public class LibraryInstalled
	{
		public string id = "", source = LibraryPack.SourceImport, title = "", author = "", kind = LibraryPack.KindIsland, date = "", plan = "";
		public int version = 1;
		public bool remix = true;
		public List<LibraryInstalledFile> files = new List<LibraryInstalledFile>();
		/// <summary>The pack's own files as they came (name, SHA-256) - the library lists the same, so a file changed there
		/// without a new version number still shows as an update (T10). Empty for installs made before.</summary>
		public Dictionary<string, string> packed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>Story item ids this entry installed under another id (T8: another entry had the same): the pack's id -> the
		/// id here. An update keeps them, so the worlds that hold them keep them.</summary>
		public Dictionary<string, string> storyIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public string Source { get { return source + ":" + id + "@" + version.ToString(CultureInfo.InvariantCulture); } }

		public Dictionary<string, object> ToDict()
		{
			return new Dictionary<string, object>
			{
				{ "id", id }, { "source", source }, { "title", title }, { "author", author }, { "kind", kind }, { "version", version }, { "remix", remix }, { "date", date }, { "plan", plan },
				{ "files", files.Select(f => (object)new Dictionary<string, object> { { "name", f.name }, { "original", f.original }, { "kind", f.kind }, { "sha256", f.sha256 }, { "shared", f.shared } }).ToList() },
				{ "packed", packed.Select(k => (object)new Dictionary<string, object> { { "name", k.Key }, { "sha256", k.Value } }).ToList() },
				{ "storyIds", storyIds.Select(k => (object)new Dictionary<string, object> { { "from", k.Key }, { "to", k.Value } }).ToList() },
			};
		}

		public static LibraryInstalled From(Dictionary<string, object> o)
		{
			var r = new LibraryInstalled
			{
				id = LibraryJson.Str(o, "id"), source = LibraryJson.Str(o, "source", LibraryPack.SourceImport), title = LibraryJson.Str(o, "title"), author = LibraryJson.Str(o, "author"),
				kind = LibraryJson.Str(o, "kind", LibraryPack.KindIsland), version = LibraryJson.Int(o, "version", 1), remix = LibraryJson.Bool(o, "remix", true), date = LibraryJson.Str(o, "date"), plan = LibraryJson.Str(o, "plan"),
				files = LibraryJson.Objects(o, "files").Select(f => new LibraryInstalledFile { name = LibraryJson.Str(f, "name"), original = LibraryJson.Str(f, "original"), kind = LibraryJson.Str(f, "kind", LibraryPack.KindIsland), sha256 = LibraryJson.Str(f, "sha256"), shared = LibraryJson.Bool(f, "shared") }).ToList(),
			};
			foreach (Dictionary<string, object> f in LibraryJson.Objects(o, "packed")) r.packed[LibraryJson.Str(f, "name")] = LibraryJson.Str(f, "sha256");
			foreach (Dictionary<string, object> f in LibraryJson.Objects(o, "storyIds")) r.storyIds[LibraryJson.Str(f, "from")] = LibraryJson.Str(f, "to");
			return r;
		}
	}

	public class LibraryInstalledFile
	{
		/// <summary>The file's name here (an island's name, or the plan's), what the entry called it, and its SHA-256 as written.</summary>
		public string name = "", original = "", sha256 = "", kind = "island";
		/// <summary>True when the same file was already here (the player's own, or another entry's): never removed with this entry.</summary>
		public bool shared;
	}

	/// <summary>A pack read from a .zip (checked): its info and its files by name.</summary>
	public class LibraryPackContents
	{
		public LibraryInfo Info;
		public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
		public string Path;

		public IEnumerable<string> IslandNames { get { return Files.Keys.Where(f => f.EndsWith(IslandFile.Extension, StringComparison.OrdinalIgnoreCase)).Select(f => f.Substring(0, f.Length - IslandFile.Extension.Length)); } }
		/// <summary>Made with a newer version of the mod than this one (Q5: the player is warned and chooses).</summary>
		public bool Newer { get { return Info != null && LibraryPack.CompareVersions(Info.minModVersion, LibraryPack.ModVersion) > 0; } }
	}

	/// <summary>
	/// Islands and world plans as files to share (LIBRARY_TASKS.md, A): a pack is a .zip holding one folder named after the
	/// entry's id, laid out like a folder of the island library - info.json, icon.jpg, pictures, the .island files and for a
	/// plan its .plan. Export collects everything an island or a plan needs (the islands its rules bring, and theirs);
	/// import checks the pack strictly (sizes, names, file formats) and installs it without ever overwriting the player's
	/// own files: an island whose name is taken by a different one is installed as "Name (Entry title)", and the entry's
	/// plan and islands are rewritten to use that name. installed.json remembers what each entry wrote, so it can be
	/// updated (worlds in progress keep the version they started with) and removed (never what a saved world still uses).
	/// </summary>
	public static class LibraryPack
	{
		public const string KindIsland = "island", KindPlan = "plan", SourceImport = "import", SourceLibrary = "library";
		/// <summary>A file's kind in installed.json: a map type file (ROADMAP T7) a pack brought along with its islands or plan.</summary>
		public const string KindMapType = "maptype";
		public const long MaxTotalBytes = 50L * 1024 * 1024;
		public const int MaxFiles = 64;
		static readonly string[] AllowedExtensions = { IslandFile.Extension, WorldPlan.Extension, MapTypeFiles.Extension, ".json", ".jpg", ".jpeg", ".png" };

		/// <summary>Where an installed entry's file is: an island, the plan or a map type file.</summary>
		public static string PathOf(LibraryInstalledFile f) { return f.kind == KindPlan ? WorldPlan.PathFor(f.name) : f.kind == KindMapType ? MapTypeFiles.PathFor(f.name) : IslandSpawner.PathFor(f.name); }

		static string ExtensionOf(string kind) { return kind == KindPlan ? WorldPlan.Extension : kind == KindMapType ? MapTypeFiles.Extension : IslandFile.Extension; }

		/// <summary>
		/// The map types of files (MapTypeFiles) the plan's rules and the islands' own rules bring ("type:&lt;name&gt;"): they go
		/// into the pack with them, so the entry works where those types aren't. Built-in types need nothing.
		/// </summary>
		static List<string> MapTypesUsed(IEnumerable<string> islands, WorldPlan plan)
		{
			var rules = new List<IntroRule>();
			if (plan != null) rules.AddRange(plan.Rules);
			foreach (string n in islands)
				try { rules.AddRange(WorldDirector.RulesFromProps(IslandCache.Props(n))); } catch { }
			return rules.Where(r => r.What == "type").Select(r => MapTypes.Get(r.WhatArg)).Where(t => t != null && t.FromFile != null && File.Exists(t.FromFile))
				.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		public static string LibraryFolder { get { return Path.Combine(DynamicIslands.assetpath, "library"); } }
		public static string ExportFolder { get { return Path.Combine(DynamicIslands.assetpath, "exports"); } }
		public static string ImportFolder { get { return Path.Combine(DynamicIslands.assetpath, "import"); } }
		static string InstalledPath { get { return Path.Combine(LibraryFolder, "installed.json"); } }
		static string WorldsFolder { get { return Path.Combine(DynamicIslands.assetpath, "worlds"); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [library] " + msg); }

		#region Small helpers

		/// <summary>The mod's version (modinfo.json).</summary>
		public static string ModVersion { get { return TestVersion ?? ExperimentalNotice.Version; } }
		/// <summary>Dev tests (CIFakeVersion): this PC pretends to have another version of the mod.</summary>
		public static string TestVersion;

		/// <summary>Compares versions as numbers ("3.10" is newer than "3.9"); an empty or odd one counts as 0.</summary>
		public static int CompareVersions(string a, string b)
		{
			int[] x = Parts(a), y = Parts(b);
			for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
			{
				int p = i < x.Length ? x[i] : 0, q = i < y.Length ? y[i] : 0;
				if (p != q) return p.CompareTo(q);
			}
			return 0;
		}

		static int[] Parts(string v)
		{
			return (v ?? "").Split('.').Select(s => { int n; return int.TryParse(new string(s.TakeWhile(char.IsDigit).ToArray()), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0; }).ToArray();
		}

		/// <summary>An id from a title: lower case letters, digits and - (e.g. "First Voyage!" -> "first-voyage").</summary>
		public static string IdFrom(string title)
		{
			var sb = new StringBuilder();
			foreach (char c in (title ?? "").ToLowerInvariant())
			{
				if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
				else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
			}
			string id = sb.ToString().Trim('-');
			if (id.Length > 48) id = id.Substring(0, 48).Trim('-');
			if (FileNames.IsReserved(id)) id += "-entry"; // (a title "Con" or "Aux" would be a zip Windows can't write)
			return id.Length > 0 ? id : "entry";
		}

		/// <summary>A name that is safe as a file name here and in a download address (no folders, no # % ?, nothing Windows refuses).</summary>
		public static bool IsSafeFileName(string name)
		{
			if (string.IsNullOrEmpty(name) || name.Length > 120 || name != name.Trim() || name.StartsWith(".") || name.Contains("..")) return false;
			if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(new[] { '#', '%', '?', '/', '\\', ':' }) >= 0) return false;
			// (Windows' device names, also with an extension - FileNames)
			return !FileNames.IsReserved(name);
		}

		public static string Sha256(byte[] bytes)
		{
			using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
		}

		internal static string ShaOfFile(string path) { return File.Exists(path) ? Sha256(File.ReadAllBytes(path)) : null; }

		/// <summary>The format number of an island file's bytes (-1 if it isn't one).</summary>
		public static int IslandFormat(byte[] b)
		{
			if (b == null || b.Length < 8 || b[0] != 'C' || b[1] != 'I' || b[2] != 'S' || b[3] != 'L') return -1;
			return BitConverter.ToInt32(b, 4);
		}

		/// <summary>An island file's body may unpack to at most this much (the densest islands are a few MB): more is a damaged
		/// or harmful file - a few packed MB can unpack to gigabytes.</summary>
		public const long MaxIslandBytes = 64L * 1024 * 1024;

		/// <summary>
		/// Why an island file's bytes (with a valid header, IslandFormat) can't be installed - its body unpacks to more than
		/// MaxIslandBytes, or it doesn't read in full as an island - or null when it is fine (AU39).
		/// </summary>
		static string IslandBodyProblem(byte[] bytes, string label)
		{
			try
			{
				// (counted while unpacking, before IslandFile reads it: what it allocates is then bounded by real data)
				long size = 0;
				using (var m = new MemoryStream(bytes, 8, bytes.Length - 8))
				using (var inflate = new InflaterInputStream(m))
				{
					var buffer = new byte[65536];
					int n;
					while ((n = inflate.Read(buffer, 0, buffer.Length)) > 0)
					{
						size += n;
						if (size > MaxIslandBytes) return "unpacks to more than " + (MaxIslandBytes / 1024 / 1024) + " MB - it isn't a real island.";
					}
				}
				IslandFile.FromBytes(bytes, label);
				return null;
			}
			catch (Exception e) { return "is damaged and can't be read (" + e.Message + ")."; }
		}

		/// <summary>
		/// Text from a pack (its title or author) as part of a file name (AU39 - the author went into the plan's file name
		/// as it was, "/" and ".." too): only characters a file name and the mod's lists may hold, at most max characters
		/// (Windows refuses paths over 260 characters), no dot or space at the end. Empty when nothing is left.
		/// </summary>
		static string NamePart(string text, int max)
		{
			char[] invalid = Path.GetInvalidFileNameChars();
			string clean = new string((text ?? "").Where(c => !char.IsControl(c) && !invalid.Contains(c) && "#%?=,;".IndexOf(c) < 0).ToArray()).Trim();
			if (clean.Length > max) clean = clean.Substring(0, max);
			return clean.TrimEnd('.', ' ');
		}

		#endregion

		#region What an island or a plan needs

		/// <summary>The island names a rule brings (island:, oneof:).</summary>
		public static IEnumerable<string> IslandsOf(IntroRule r)
		{
			if (r.What == "island") return new[] { r.WhatArg.Trim() };
			if (r.What == "oneof") return r.WhatArg.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0);
			return new string[0];
		}

		/// <summary>
		/// Every island the start islands and the plan's rules bring, and the islands those bring, and so on (the islands'
		/// own "bring" rules). missing: named but not saved here.
		/// </summary>
		public static List<string> Collect(IEnumerable<string> start, WorldPlan plan, out List<string> missing)
		{
			var found = new List<string>();
			missing = new List<string>();
			var queue = new Queue<string>(start ?? new string[0]);
			if (plan != null) foreach (IntroRule r in plan.Rules) foreach (string n in IslandsOf(r)) queue.Enqueue(n);
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			while (queue.Count > 0)
			{
				string n = queue.Dequeue();
				if (n.Length == 0 || !seen.Add(n)) continue;
				if (!File.Exists(IslandSpawner.PathFor(n))) { missing.Add(n); continue; }
				found.Add(n);
				foreach (IntroRule r in WorldDirector.RulesFromProps(IslandCache.Props(n)))
					foreach (string m in IslandsOf(r)) queue.Enqueue(m);
			}
			return found;
		}

		#endregion

		#region Export

		static string LastExportsPath { get { return Path.Combine(ExportFolder, "exports.json"); } }

		/// <summary>exports.json: { "items": [ { "key": "plan:First Voyage", "info": { ... } } ] }.</summary>
		static List<Dictionary<string, object>> LastExports()
		{
			if (!File.Exists(LastExportsPath)) return new List<Dictionary<string, object>>();
			return LibraryJson.Objects(LibraryJson.Parse(File.ReadAllText(LastExportsPath)) as Dictionary<string, object>, "items");
		}

		/// <summary>
		/// What was filled in the last time this island or plan was exported (so a new export keeps its id and raises its
		/// version - an update of the same entry, not a new one), or a fresh info.
		/// </summary>
		public static LibraryInfo LastInfo(string kind, string name)
		{
			try
			{
				Dictionary<string, object> e = LastExports().FirstOrDefault(x => LibraryJson.Str(x, "key").Equals(kind + ":" + name, StringComparison.OrdinalIgnoreCase));
				object infoObj;
				if (e != null && e.TryGetValue("info", out infoObj) && infoObj is Dictionary<string, object>)
				{
					LibraryInfo last = LibraryInfo.From((Dictionary<string, object>)infoObj);
					last.version++;
					return last;
				}
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + LastExportsPath + ": " + ex.Message); }
			var info = new LibraryInfo { kind = kind, title = name, id = IdFrom(name), plan = kind == KindPlan ? name + WorldPlan.Extension : "" };
			// (an island or plan that came from someone else's entry: credit them)
			LibraryInstalled from = OwnerOf(kind == KindPlan ? name + WorldPlan.Extension : name + IslandFile.Extension);
			if (from != null) { info.basedOn = from.title + " by " + from.author; info.remix = from.remix; }
			return info;
		}

		static void RememberExport(string kind, string name, LibraryInfo info)
		{
			try
			{
				List<Dictionary<string, object>> items = LastExports();
				items.RemoveAll(x => LibraryJson.Str(x, "key").Equals(kind + ":" + name, StringComparison.OrdinalIgnoreCase));
				items.Add(new Dictionary<string, object> { { "key", kind + ":" + name }, { "info", LibraryJson.Parse(info.ToJson()) } });
				Directory.CreateDirectory(ExportFolder);
				SafeFile.WriteAllText(LastExportsPath, LibraryJson.Write(new Dictionary<string, object> { { "items", items } }));
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Could not remember the export: " + ex.Message); }
		}

		/// <summary>
		/// Exports an island (with every island its rules bring) or a plan (with every island it needs) as a .zip in
		/// Mods\DynamicIslands\exports. icon / picture: JPG bytes (null = none). Returns the zip's path, or null and why not.
		/// </summary>
		public static string Export(LibraryInfo info, string islandName, WorldPlan plan, byte[] icon, byte[] picture, out string error) { return ExportWith(info, islandName, plan, icon, picture != null ? new[] { picture } : new byte[0][], out error); }

		/// <summary>The same with up to 4 pictures (picture1.jpg ...).</summary>
		public static string ExportWith(LibraryInfo info, string islandName, WorldPlan plan, byte[] icon, IList<byte[]> pictures, out string error)
		{
			error = null;
			try
			{
				if (info.title.Trim().Length == 0) { error = "Give it a title."; return null; }
				if (info.author.Trim().Length == 0) { error = "Say who made it (the author)."; return null; }
				info.id = IdFrom(info.id.Length > 0 ? info.id : info.title);
				info.kind = plan != null ? KindPlan : KindIsland;
				info.minModVersion = ModVersion;
				if (info.created.Length == 0) info.created = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

				List<string> missing;
				List<string> islands = Collect(islandName != null ? new[] { islandName } : new string[0], plan, out missing);
				if (missing.Count > 0) { error = "These islands are named by its rules but aren't saved here: " + string.Join(", ", missing.ToArray()) + "."; return null; }
				if (islands.Count == 0 && plan == null) { error = "There is no saved island '" + islandName + "'."; return null; }
				// (remixes: an island that came from someone else's entry which says no)
				foreach (string n in islands)
				{
					LibraryInstalled from = OwnerOf(n + IslandFile.Extension);
					if (from != null && !from.remix && !from.author.Equals(info.author.Trim(), StringComparison.OrdinalIgnoreCase))
					{ error = "'" + n + "' comes from '" + from.title + "' by " + from.author + ", who asked for it not to be shared changed (no remixes)."; return null; }
				}
				var files = new List<KeyValuePair<string, byte[]>>();
				foreach (string n in islands)
				{
					if (!IsSafeFileName(n + IslandFile.Extension)) { error = "The island name '" + n + "' can't be shared as a file name (no # % ? / \\ :). Save it under another name first."; return null; }
					files.Add(new KeyValuePair<string, byte[]>(n + IslandFile.Extension, File.ReadAllBytes(IslandSpawner.PathFor(n))));
				}
				if (plan != null)
				{
					if (!IsSafeFileName(plan.Name + WorldPlan.Extension)) { error = "The plan name '" + plan.Name + "' can't be shared as a file name (no # % ? / \\ :)."; return null; }
					info.plan = plan.Name + WorldPlan.Extension;
					files.Add(new KeyValuePair<string, byte[]>(info.plan, Encoding.UTF8.GetBytes(plan.ToText())));
				}
				else info.plan = "";
				foreach (string t in MapTypesUsed(islands, plan))
					files.Add(new KeyValuePair<string, byte[]>(t + MapTypeFiles.Extension, File.ReadAllBytes(MapTypeFiles.PathFor(t))));
				info.icon = icon != null ? "icon.jpg" : "";
				pictures = (pictures ?? new byte[0][]).Where(p => p != null).Take(4).ToList();
				info.pictures = Enumerable.Range(1, pictures.Count).Select(i => "picture" + i + ".jpg").ToArray();
				if (icon != null) files.Add(new KeyValuePair<string, byte[]>("icon.jpg", icon));
				for (int i = 0; i < pictures.Count; i++) files.Add(new KeyValuePair<string, byte[]>("picture" + (i + 1) + ".jpg", pictures[i]));
				files.Insert(0, new KeyValuePair<string, byte[]>("info.json", Encoding.UTF8.GetBytes(info.ToJson())));
				if (files.Sum(f => (long)f.Value.Length) > MaxTotalBytes) { error = "It is over " + (MaxTotalBytes / 1024 / 1024) + " MB - too big to share."; return null; }

				Directory.CreateDirectory(ExportFolder);
				string path = Path.Combine(ExportFolder, info.id + ".zip");
				WriteZip(path, info.id, files);
				RememberExport(info.kind, plan != null ? plan.Name : islandName, info);
				Log("Exported " + info.kind + " '" + info.title + "' (" + info.id + ", version " + info.version + "): " + islands.Count + " island(s) -> " + path);
				return path;
			}
			catch (Exception e) { error = "Exporting failed: " + e.Message; Debug.LogWarning("[CUSTOM ISLANDS] " + e); return null; }
		}

		static void WriteZip(string path, string folder, IEnumerable<KeyValuePair<string, byte[]>> files)
		{
			string tmp = SafeFile.TempFor(path);
			using (var zip = new ZipOutputStream(File.Create(tmp)))
			{
				zip.SetLevel(6);
				foreach (var f in files)
				{
					var e = new ZipEntry(folder + "/" + f.Key) { DateTime = DateTime.Now, Size = f.Value.Length, IsUnicodeText = true };
					zip.PutNextEntry(e);
					zip.Write(f.Value, 0, f.Value.Length);
					zip.CloseEntry();
				}
				zip.Finish();
			}
			// (in the old export's place in one step; it was deleted, then moved - AU41)
			SafeFile.Commit(tmp, path);
		}

		#endregion

		#region Reading a pack

		/// <summary>What zip tools add on their own (macOS's __MACOSX folder and ._ files, .DS_Store, Windows' Thumbs.db and
		/// desktop.ini): passed by, so a pack repacked by one still reads.</summary>
		static bool IsZipJunk(string[] parts)
		{
			string name = parts[parts.Length - 1];
			return parts.Any(p => p.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)) || name.StartsWith("._")
				|| name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) || name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Reads and checks a pack: at most 64 files and 50 MB unpacked (counted while unpacking, whatever the zip claims),
		/// plain file names only (one folder at most, no "..", nothing that could land outside the mod's folder), known kinds
		/// of files, a valid info.json, island files this version can read, the plan and every island it names inside.
		/// Returns null and why when something is wrong; nothing is written.
		/// </summary>
		public static LibraryPackContents Read(string path, out string error)
		{
			error = null;
			var pack = new LibraryPackContents { Path = path };
			try
			{
				long total = 0;
				string folder = null;
				using (var zip = new ZipInputStream(File.OpenRead(path)))
				{
					ZipEntry e;
					var buffer = new byte[65536];
					while ((e = zip.GetNextEntry()) != null)
					{
						if (e.IsDirectory) continue;
						string full = (e.Name ?? "").Replace('\\', '/');
						string[] parts = full.Split('/');
						if (IsZipJunk(parts)) continue;
						if (parts.Length > 2 || parts.Any(p => p == ".." || p == ".")) { error = "It holds a file in a place it may not ('" + full + "')."; return null; }
						string top = parts.Length == 2 ? parts[0] : "";
						if (folder == null) folder = top;
						else if (!folder.Equals(top, StringComparison.OrdinalIgnoreCase)) { error = "It holds more than one entry's folder."; return null; }
						string name = parts[parts.Length - 1];
						if (!IsSafeFileName(name)) { error = "It holds a file with a name that isn't allowed ('" + name + "')."; return null; }
						if (!AllowedExtensions.Any(x => name.EndsWith(x, StringComparison.OrdinalIgnoreCase))) { error = "It holds a kind of file that doesn't belong in an island pack ('" + name + "')."; return null; }
						if (pack.Files.Count >= MaxFiles) { error = "It holds more than " + MaxFiles + " files."; return null; }
						if (pack.Files.ContainsKey(name)) { error = "It holds '" + name + "' twice."; return null; }
						using (var ms = new MemoryStream())
						{
							int n;
							while ((n = zip.Read(buffer, 0, buffer.Length)) > 0)
							{
								total += n;
								if (total > MaxTotalBytes) { error = "It unpacks to more than " + (MaxTotalBytes / 1024 / 1024) + " MB."; return null; }
								ms.Write(buffer, 0, n);
							}
							pack.Files[name] = ms.ToArray();
						}
					}
				}
				byte[] infoBytes;
				if (!pack.Files.TryGetValue("info.json", out infoBytes)) { error = "It has no info.json - it isn't an island pack."; return null; }
				LibraryInfo info;
				try { info = LibraryInfo.FromJson(Encoding.UTF8.GetString(infoBytes).TrimStart('﻿')); }
				catch (Exception e) { error = "Its info.json can't be read (" + e.Message + ")."; return null; }
				info.id = IdFrom(info.id.Length > 0 ? info.id : (folder ?? "").Length > 0 ? folder : info.title);
				pack.Info = info;
				return Validate(pack, out error) ? pack : null;
			}
			catch (Exception e) { error = "It can't be read as a pack (" + e.Message + ")."; return null; }
		}

		/// <summary>
		/// A pack from files downloaded from the island library (their SHA-256 checked already), checked the same way as a
		/// .zip: plain names, known kinds of files, the size limits, island files this version reads, the plan's islands.
		/// </summary>
		public static LibraryPackContents FromFiles(LibraryInfo info, Dictionary<string, byte[]> files, out string error)
		{
			error = null;
			var pack = new LibraryPackContents { Info = info, Path = "library:" + info.id };
			if (files.Count > MaxFiles) { error = "It holds more than " + MaxFiles + " files."; return null; }
			if (files.Values.Sum(b => (long)b.Length) > MaxTotalBytes) { error = "It is more than " + (MaxTotalBytes / 1024 / 1024) + " MB."; return null; }
			foreach (var f in files)
			{
				if (!IsSafeFileName(f.Key)) { error = "It holds a file with a name that isn't allowed ('" + f.Key + "')."; return null; }
				if (!AllowedExtensions.Any(x => f.Key.EndsWith(x, StringComparison.OrdinalIgnoreCase))) { error = "It holds a kind of file that doesn't belong in an island pack ('" + f.Key + "')."; return null; }
				pack.Files[f.Key] = f.Value;
			}
			info.id = IdFrom(info.id.Length > 0 ? info.id : info.title);
			return Validate(pack, out error) ? pack : null;
		}

		/// <summary>The checks every pack gets, however it came: its info, island files this version reads, a plan's islands.</summary>
		static bool Validate(LibraryPackContents pack, out string error)
		{
			error = null;
			LibraryInfo info = pack.Info;
			if (info.title.Trim().Length == 0) info.title = info.id;
			if (info.kind != KindIsland && info.kind != KindPlan) { error = "Its info.json says kind '" + info.kind + "' (island or plan)."; return false; }
			foreach (string n in pack.Files.Keys.Where(f => f.EndsWith(IslandFile.Extension, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				int format = IslandFormat(pack.Files[n]);
				if (format < 0) { error = "'" + n + "' isn't an island file."; return false; }
				if (format > IslandFile.FormatVersion) { error = "'" + n + "' was saved by a newer Custom Islands than this one - update the mod to use it."; return false; }
				// (AU39: the name as an island name here - at most 60 characters, nothing the mod's lists use - and the whole
				// file read, not only its header: a damaged or huge body installed, and failed later in the editor or a world)
				string nameProblem = FileNames.IslandProblem(n.Substring(0, n.Length - IslandFile.Extension.Length));
				if (nameProblem != null) { error = "'" + n + "' has a name this version can't use: " + nameProblem; return false; }
				string bodyProblem = IslandBodyProblem(pack.Files[n], n);
				if (bodyProblem != null) { error = "'" + n + "' " + bodyProblem; return false; }
			}
			// Map type files: names this version takes, and files it reads (a broken one would only be left out at the next start)
			foreach (string n in pack.Files.Keys.Where(f => f.EndsWith(MapTypeFiles.Extension, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				string typeName = n.Substring(0, n.Length - MapTypeFiles.Extension.Length);
				string typeProblem = MapTypeFiles.NameProblem(typeName);
				if (typeProblem != null) { error = "Its map type '" + n + "' can't be used: " + typeProblem + "."; return false; }
				string typeError;
				if (MapTypeFiles.Parse(typeName, Encoding.UTF8.GetString(pack.Files[n]), out typeError) == null) { error = "Its map type '" + n + "' can't be read (" + typeError + ") - it may need a newer Custom Islands."; return false; }
			}
			// (a plan may bring only new islands of a map type: no island file in it)
			if (!pack.IslandNames.Any() && !info.IsPlan) { error = "It holds no island."; return false; }
			if (info.IsPlan)
			{
				byte[] planBytes;
				if (info.plan.Length == 0 || !pack.Files.TryGetValue(info.plan, out planBytes)) { error = "Its plan '" + info.plan + "' isn't in it."; return false; }
				string planProblem = FileNames.Problem(Path.GetFileNameWithoutExtension(info.plan));
				if (planProblem != null) { error = "Its plan '" + info.plan + "' has a name this version can't use: " + planProblem; return false; }
				WorldPlan plan = WorldPlan.Parse(Path.GetFileNameWithoutExtension(info.plan), Encoding.UTF8.GetString(planBytes));
				var names = new HashSet<string>(pack.IslandNames, StringComparer.OrdinalIgnoreCase);
				string absent = plan.Rules.SelectMany(IslandsOf).FirstOrDefault(n => !names.Contains(n));
				if (absent != null) { error = "Its plan brings the island '" + absent + "', which isn't in it."; return false; }
			}
			return true;
		}

		#endregion

		#region Installed entries

		public static List<LibraryInstalled> Installed()
		{
			try
			{
				if (!File.Exists(InstalledPath)) return new List<LibraryInstalled>();
				return LibraryJson.Objects(LibraryJson.Parse(File.ReadAllText(InstalledPath)) as Dictionary<string, object>, "entries").Select(LibraryInstalled.From).ToList();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + InstalledPath + ": " + e.Message); return new List<LibraryInstalled>(); }
		}

		/// <summary>Writes installed.json (public for tests).</summary>
		public static void SaveInstalled(List<LibraryInstalled> entries)
		{
			Directory.CreateDirectory(LibraryFolder);
			// (an installed.json that can't be read was taken for an empty one by Installed, and this wrote over it: every
			// other entry forgotten - its files never updated or removed. A copy goes to the deleted folder first; one that
			// can't be read at all - locked - isn't written over)
			if (File.Exists(InstalledPath))
			{
				string text = null;
				try { text = File.ReadAllText(InstalledPath); } catch (Exception e) { throw new IOException(Path.GetFileName(InstalledPath) + " can't be read (" + e.Message + ") - it isn't written over", e); }
				try { LibraryJson.Objects(LibraryJson.Parse(text) as Dictionary<string, object>, "entries").Select(LibraryInstalled.From).ToList(); }
				catch (Exception)
				{
					string folder = Path.Combine(Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), "library");
					Directory.CreateDirectory(folder);
					File.Copy(InstalledPath, Path.Combine(folder, "installed " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".json"), true);
					Debug.LogWarning("[CUSTOM ISLANDS] " + InstalledPath + " couldn't be read: a copy is kept in " + folder);
				}
			}
			SafeFile.WriteAllText(InstalledPath, LibraryJson.Write(new Dictionary<string, object> { { "entries", entries.Select(e => (object)e.ToDict()).ToList() } }));
		}

		/// <summary>The entry that wrote this file (an island "Name.island" or a plan "Name.plan"), if one did.</summary>
		public static LibraryInstalled OwnerOf(string fileName)
		{
			return Installed().FirstOrDefault(e => e.files.Any(f => !f.shared && (f.name + ExtensionOf(f.kind)).Equals(fileName, StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>An installed entry's islands and plan the player has changed since (saved over in the editor or World Plans).</summary>
		public static List<string> ChangedFiles(string id)
		{
			var result = new List<string>();
			LibraryInstalled e = Installed().FirstOrDefault(x => x.id.Equals(id, StringComparison.OrdinalIgnoreCase));
			if (e == null) return result;
			foreach (LibraryInstalledFile f in e.files.Where(f => !f.shared))
			{
				string path = PathOf(f);
				try { if (File.Exists(path) && ShaOfFile(path) != f.sha256) result.Add(f.name); } catch { }
			}
			return result;
		}

		#endregion

		#region Worlds using an island

		/// <summary>A plan rule that brings this island by name (island: or one of oneof:).</summary>
		internal static bool RuleNames(IntroRule r, string island)
		{
			return (r.What == "island" && r.WhatArg.Trim().Equals(island, StringComparison.OrdinalIgnoreCase)) ||
				(r.What == "oneof" && r.WhatArg.Split(',').Any(x => x.Trim().Equals(island, StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>The saved worlds (their names) whose island list has this island, or whose kept plan brings it, or whose
		/// plan is this plan without a copy of it. The editor's test world isn't counted.</summary>
		public static List<string> WorldsUsing(string island, string plan = null)
		{
			var result = new List<string>();
			try
			{
				if (!Directory.Exists(WorldsFolder)) return result;
				foreach (string file in Directory.GetFiles(WorldsFolder, "*.txt"))
				{
					string[] lines = File.ReadAllLines(file);
					string world = Housekeeping.WorldName(lines, file);
					// (the editor's test world: counted, every edit-and-test round kept another copy of the island for it, and
					// the save notices and the Delete warning named it)
					if (world.Equals(IslandTest.WorldName, StringComparison.OrdinalIgnoreCase)) continue;
					bool uses = island != null && lines.Any(l => !l.StartsWith("@") && !l.StartsWith("#") && l.Split('|')[0].Trim().Equals(island, StringComparison.OrdinalIgnoreCase));
					// (the plan the world keeps may bring it later: its rules are in the world file - a world whose plan
					// hadn't brought the island yet lost it to a Delete or a library Remove)
					if (!uses && island != null)
						uses = lines.Where(l => l.StartsWith("@planrule=")).Select(l => IntroRule.Parse(l.Substring(10))).Any(r => r != null && RuleNames(r, island));
					if (!uses && plan != null)
						uses = lines.Any(l => l.Trim().Equals("@plan=" + plan, StringComparison.OrdinalIgnoreCase)) && !lines.Any(l => l.StartsWith("@planrule="));
					if (!uses) continue;
					result.Add(world);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking through the saved worlds: " + e.Message); }
			return result.Distinct().ToList();
		}

		#endregion

		#region Installing

		/// <summary>What an install did, for the player.</summary>
		public class Report
		{
			public readonly List<string> Lines = new List<string>();
			public string PlanName;
			public List<string> Islands = new List<string>();
			public void Add(string line) { Lines.Add(line); Log(line); }
			public override string ToString() { return string.Join("\n", Lines.ToArray()); }
		}

		/// <summary>
		/// Installs a pack (checked by Read). appearWhileSailing: an island entry's islands also turn up by chance while
		/// sailing (spawnpool.txt weight 1, else 0; a plan's islands always 0). replaceChanged: an earlier version's file the
		/// player has changed since is replaced too (else kept). source: SourceImport or SourceLibrary.
		/// An install that fails part way (a full disk, a file in use, a path Windows refuses) puts back every file it had
		/// written or removed, so no file is left that installed.json doesn't know (AU39), and throws.
		/// </summary>
		public static Report Install(LibraryPackContents pack, bool appearWhileSailing, bool replaceChanged, string source)
		{
			var undo = new InstallUndo();
			try { return InstallFiles(pack, appearWhileSailing, replaceChanged, source, undo); }
			catch (Exception e)
			{
				int put = undo.RollBack();
				IslandCache.Forget();
				Log("Installing '" + (pack.Info != null ? pack.Info.title : "?") + "' failed part way (" + e.Message + "): " + put + " file(s) put back as they were");
				throw new IOException(e.Message + " - nothing was installed (what it had written was put back)", e);
			}
		}

		/// <summary>
		/// The files an install wrote or removed, as they were before (null: the file wasn't there), to put back when it
		/// fails part way (AU39: islands were written first and installed.json last, so a failure in between left islands
		/// no entry knew - never updated, never removed with it). Not undone: a kept copy of an island for saved worlds
		/// (KeepForWorlds) - it holds the version that is put back, so those worlds play the same island either way.
		/// </summary>
		class InstallUndo
		{
			readonly List<KeyValuePair<string, byte[]>> before = new List<KeyValuePair<string, byte[]>>();

			/// <summary>Called before a file is written or deleted (once per file: the first time is how it was).</summary>
			public void Remember(string path)
			{
				if (before.Any(b => b.Key.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
				before.Add(new KeyValuePair<string, byte[]>(path, File.Exists(path) ? File.ReadAllBytes(path) : null));
			}

			/// <summary>Every remembered file as it was, the last one first. Returns how many were put back.</summary>
			public int RollBack()
			{
				int n = 0;
				for (int i = before.Count - 1; i >= 0; i--)
				{
					string path = before[i].Key;
					try
					{
						if (before[i].Value != null) SafeFile.WriteAllBytes(path, before[i].Value);
						else if (File.Exists(path)) File.Delete(path);
						n++;
					}
					catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [library] Could not put back " + path + ": " + e.Message); }
				}
				return n;
			}
		}

		static Report InstallFiles(LibraryPackContents pack, bool appearWhileSailing, bool replaceChanged, string source, InstallUndo undo)
		{
			var report = new Report();
			LibraryInfo info = pack.Info;
			List<LibraryInstalled> all = Installed();
			LibraryInstalled old = all.FirstOrDefault(e => e.id.Equals(info.id, StringComparison.OrdinalIgnoreCase));
			// (another author's pack with the same id isn't its update: it overwrote that entry's files - ROADMAP CB4. It is an
			// entry of its own, "<id>-<author>", which its own next version finds again)
			if (old != null && old.author.Trim().Length > 0 && info.author.Trim().Length > 0 && !old.author.Trim().Equals(info.author.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				string own = IdFrom(info.id + "-" + info.author);
				report.Add("'" + info.title + "' by " + info.author + " has the id of '" + old.title + "' by " + old.author + " ('" + info.id + "'): installed as an entry of its own ('" + own + "'), not as its update");
				info.id = own;
				old = all.FirstOrDefault(e => e.id.Equals(info.id, StringComparison.OrdinalIgnoreCase));
			}
			var entry = new LibraryInstalled { id = info.id, source = source, title = info.title, author = info.author, kind = info.kind, version = info.version, remix = info.remix, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
			foreach (var f in pack.Files) if (f.Key.EndsWith(IslandFile.Extension, StringComparison.OrdinalIgnoreCase) || f.Key.EndsWith(WorldPlan.Extension, StringComparison.OrdinalIgnoreCase) || f.Key.EndsWith(MapTypeFiles.Extension, StringComparison.OrdinalIgnoreCase))
				entry.packed[f.Key] = Sha256(f.Value);
			report.Add((old != null ? "Updating '" : "Installing '") + info.title + "' by " + info.author + " (version " + info.version + (old != null ? ", was " + old.version : "") + ")");

			// Where each island goes: its own name if free or the same file is here, the name it had in the earlier version,
			// else "Name (Title)" - and the entry's rules follow the new names (which can change files, so until it settles)
			var originals = pack.IslandNames.ToList();
			var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			WorldPlan plan = info.IsPlan ? WorldPlan.Parse(Path.GetFileNameWithoutExtension(info.plan), Encoding.UTF8.GetString(pack.Files[info.plan])) : null;
			// (a reference to one of the plan's rules is a rule, also when the rule has its island's name: it isn't renamed)
			var ruleIds = new HashSet<string>(plan != null ? plan.Rules.Select(r => r.Id) : Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
			foreach (string n in originals)
			{
				LibraryInstalledFile before = old != null ? old.files.FirstOrDefault(f => f.kind == KindIsland && f.original.Equals(n, StringComparison.OrdinalIgnoreCase)) : null;
				target[n] = before != null && !before.shared ? before.name : n;
			}
			Dictionary<string, string> storyIds = StoryIdsFor(pack, info, old, all, originals, report);
			entry.storyIds = storyIds;
			var content = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
			var copies = new List<string>();
			var updated = new List<string>();
			for (int round = 0; round < 8; round++)
			{
				bool changed = false;
				foreach (string n in originals)
				{
					byte[] bytes = Rewritten(pack.Files[n + IslandFile.Extension], originals, target, ruleIds, storyIds);
					content[n] = bytes;
					string t = target[n];
					string path = IslandSpawner.PathFor(t);
					bool ownsIt = old != null && old.files.Any(f => !f.shared && f.name.Equals(t, StringComparison.OrdinalIgnoreCase));
					bool takenByOther = originals.Any(o => o != n && target[o].Equals(t, StringComparison.OrdinalIgnoreCase));
					if (takenByOther || (File.Exists(path) && !ownsIt && Sha256(File.ReadAllBytes(path)) != Sha256(bytes)))
					{
						target[n] = FreeName(n, info.title, originals, target);
						changed = true;
					}
				}
				if (!changed) break;
			}

			// The islands
			foreach (string n in originals)
			{
				string t = target[n];
				string path = IslandSpawner.PathFor(t);
				byte[] bytes = content[n];
				string sha = Sha256(bytes);
				LibraryInstalledFile before = old != null ? old.files.FirstOrDefault(f => !f.shared && f.name.Equals(t, StringComparison.OrdinalIgnoreCase)) : null;
				if (File.Exists(path) && ShaOfFile(path) == sha)
				{
					entry.files.Add(new LibraryInstalledFile { name = t, original = n, sha256 = sha, kind = KindIsland, shared = before == null });
					report.Add(before != null ? "'" + t + "' is unchanged" : "'" + t + "' is already here (the same island) - shared");
				}
				else
				{
					if (File.Exists(path) && before != null)
					{
						string current = ShaOfFile(path);
						if (current != before.sha256 && !replaceChanged)
						{
							// (the hash the entry installed, not the player's: with theirs the file counted as unchanged, and the next
							// update replaced it without asking)
							entry.files.Add(new LibraryInstalledFile { name = t, original = n, sha256 = before.sha256, kind = KindIsland });
							report.Add("Kept your changed '" + t + "' (the new version's is not installed)");
							continue;
						}
						// (T10: replaced - the player's version stays as a copy of its own, out of the random pool, so nothing is lost)
						if (current != before.sha256)
						{
							string copy = YoursName(t, false);
							undo.Remember(IslandSpawner.PathFor(copy));
							SafeFile.WriteAllBytes(IslandSpawner.PathFor(copy), File.ReadAllBytes(path));
							copies.Add(copy);
							report.Add("Your changed '" + t + "' is kept as '" + copy + "'");
						}
						// (saved worlds: the same rule as an editor save - an update that keeps the objects' order reaches them, one
						// that would shift what was used there keeps them on the version they started with)
						KeepForWorlds(t, bytes, report);
						updated.Add(t);
					}
					undo.Remember(path);
					SafeFile.WriteAllBytes(path, bytes);
					// (the island open in the editor was replaced under it: the next Ctrl+S put the old one back without a word)
					if (DynamicIslands.InEditor() && t.Equals(DynamicIslands.currentIslandName, StringComparison.OrdinalIgnoreCase))
						report.Add("'" + t + "' is the island open in the editor: Open it again to see the new version (saving now would put yours back)");
					entry.files.Add(new LibraryInstalledFile { name = t, original = n, sha256 = sha, kind = KindIsland });
					report.Add(t.Equals(n, StringComparison.OrdinalIgnoreCase) ? "Installed '" + t + "'" : "Installed '" + n + "' as '" + t + "' (you have a different island called '" + n + "')");
				}
				report.Islands.Add(t);
			}

			// The plan
			if (plan != null)
			{
				foreach (IntroRule r in plan.Rules) Rename(r, target, ruleIds);
				LibraryInstalledFile before = old != null ? old.files.FirstOrDefault(f => f.kind == KindPlan && !f.shared) : null;
				string name = before != null ? before.name : plan.Name;
				string text = plan.ToText();
				bool same = false, keep = false;
				if (before == null)
				{
					string existing = WorldPlan.PathFor(name);
					if (WorldPlan.IsBuiltIn(name) || (File.Exists(existing) && File.ReadAllText(existing) != text))
					{
						name = FreePlanName(plan.Name, info.author);
						report.Add("Installed the plan '" + plan.Name + "' as '" + name + "' (you have a different plan called '" + plan.Name + "')");
					}
					// (the player's own plan, the very same: shared - removing the entry deleted the player's plan with it)
					else if (File.Exists(existing)) { same = true; report.Add("The plan '" + name + "' is already here (the same plan) - shared"); }
					else report.Add("Installed the plan '" + name + "'");
				}
				// (the player's changes to the installed plan, as for its islands: kept unless they chose to replace them - it
				// was written over at every update; the hash recorded stays the entry's, so the next update asks again)
				else if (!replaceChanged && File.Exists(WorldPlan.PathFor(name)) && ShaOfFile(WorldPlan.PathFor(name)) != before.sha256 && ShaOfFile(WorldPlan.PathFor(name)) != Sha256(Encoding.UTF8.GetBytes(text)))
					keep = true;
				else report.Add("Updated the plan '" + name + "' (worlds already started keep their own copy of it)");
				plan.Name = name;
				if (keep) report.Add("Kept your changed plan '" + name + "' (the new version's is not installed)");
				else
				{
					string planPath = WorldPlan.PathFor(name);
					if (before != null && File.Exists(planPath) && ShaOfFile(planPath) != before.sha256 && ShaOfFile(planPath) != Sha256(Encoding.UTF8.GetBytes(text)))
					{
						string copy = YoursName(name, true);
						undo.Remember(WorldPlan.PathFor(copy));
						SafeFile.WriteAllBytes(WorldPlan.PathFor(copy), File.ReadAllBytes(planPath));
						report.Add("Your changed plan '" + name + "' is kept as '" + copy + "'");
					}
					Directory.CreateDirectory(WorldPlan.Folder);
					undo.Remember(WorldPlan.PathFor(name));
					SafeFile.WriteAllText(WorldPlan.PathFor(name), text);
				}
				entry.plan = name;
				entry.files.Add(new LibraryInstalledFile { name = name, original = Path.GetFileNameWithoutExtension(info.plan), sha256 = keep ? before.sha256 : Sha256(Encoding.UTF8.GetBytes(text)), kind = KindPlan, shared = same });
				report.PlanName = name;
			}

			// Map type files (ROADMAP T7): under their own names - a different file of that name here is the player's (or another
			// entry's), kept, and said; the pack's islands made from that type are files already, only new ones would differ
			bool types = false;
			foreach (string fileName in pack.Files.Keys.Where(f => f.EndsWith(MapTypeFiles.Extension, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				string t = fileName.Substring(0, fileName.Length - MapTypeFiles.Extension.Length);
				byte[] bytes = pack.Files[fileName];
				string sha = Sha256(bytes), path = MapTypeFiles.PathFor(t);
				LibraryInstalledFile before = old != null ? old.files.FirstOrDefault(f => f.kind == KindMapType && !f.shared && f.name.Equals(t, StringComparison.OrdinalIgnoreCase)) : null;
				if (File.Exists(path) && ShaOfFile(path) == sha)
				{
					entry.files.Add(new LibraryInstalledFile { name = t, original = t, sha256 = sha, kind = KindMapType, shared = before == null });
					report.Add(before != null ? "The map type '" + t + "' is unchanged" : "The map type '" + t + "' is already here (the same file) - shared");
					continue;
				}
				if (File.Exists(path) && (before == null || (ShaOfFile(path) != before.sha256 && !replaceChanged)))
				{
					if (before != null) entry.files.Add(new LibraryInstalledFile { name = t, original = t, sha256 = before.sha256, kind = KindMapType });
					report.Add("Kept your map type '" + t + "' (the pack's is different and is not installed: new islands of that type are made from yours)");
					continue;
				}
				Directory.CreateDirectory(MapTypeFiles.Folder);
				undo.Remember(path);
				SafeFile.WriteAllBytes(path, bytes);
				entry.files.Add(new LibraryInstalledFile { name = t, original = t, sha256 = sha, kind = KindMapType });
				report.Add((before != null ? "Updated" : "Installed") + " the map type '" + t + "'");
				types = true;
			}

			// An earlier version's files the new one doesn't have any more
			if (old != null)
				foreach (LibraryInstalledFile f in old.files.Where(f => !f.shared && !entry.files.Any(x => x.name.Equals(f.name, StringComparison.OrdinalIgnoreCase) && x.kind == f.kind)))
					RemoveFile(f, entry.id, all, report, undo);

			all.RemoveAll(e => e.id.Equals(info.id, StringComparison.OrdinalIgnoreCase));
			all.Add(entry);
			SaveInstalled(all);
			NoteUpdated(updated, info.title);

			if (types || (old != null && old.files.Any(f => f.kind == KindMapType))) MapTypeFiles.LoadAll();

			// Random islands while sailing: a plan's islands never, an island entry's as the player chose (their own islands
			// untouched). (After installed.json: an install that fails before it leaves spawnpool.txt as it was)
			foreach (LibraryInstalledFile f in entry.files.Where(f => f.kind == KindIsland && !f.shared))
				SetPoolWeight(f.name, !info.IsPlan && appearWhileSailing ? 1f : 0f);
			foreach (string c in copies) SetPoolWeight(c, 0f);
			IslandCache.Forget();
			return report;
		}

		/// <summary>An island file's bytes with its "bring" rules pointing at the entry's new names (unchanged if none moved).</summary>
		/// <summary>T8: the pack's story item ids that another installed entry has too, each with an id of its own here
		/// ("key" -> "key-&lt;entry id&gt;"): a world's crew holds one of each id, so two packs' "key" was one key - found for
		/// one, it opened the other's door. An update keeps the ids its earlier version got (worlds hold them).</summary>
		static Dictionary<string, string> StoryIdsFor(LibraryPackContents pack, LibraryInfo info, LibraryInstalled old, List<LibraryInstalled> all, List<string> originals, Report report)
		{
			var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				var ours = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				var ourShas = new HashSet<string>();
				foreach (string n in originals)
				{
					byte[] bytes = pack.Files[n + IslandFile.Extension];
					ourShas.Add(Sha256(bytes));
					IslandFile f = IslandFile.FromBytes(bytes, n);
					if (f != null && f.Props != null) foreach (StoryItemDef d in StoryItems.Of(f.Props)) ours[d.Id] = d.ShownName;
				}
				if (ours.Count == 0) return map;
				if (old != null) foreach (var k in old.storyIds) if (ours.ContainsKey(k.Key)) map[k.Key] = k.Value;
				// (the other entries' ids - not from an island that is this pack's own, shared with an entry that has it too)
				var theirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (LibraryInstalled other in all.Where(e => !e.id.Equals(info.id, StringComparison.OrdinalIgnoreCase)))
					foreach (LibraryInstalledFile f in other.files.Where(f => f.kind == KindIsland && !ourShas.Contains(f.sha256)))
						foreach (StoryItemDef d in StoryItems.Of(IslandCache.Props(f.name)))
							if (!theirs.ContainsKey(d.Id)) theirs[d.Id] = other.title;
				string suffix = IdFrom(info.id);
				foreach (string id in ours.Keys.Where(theirs.ContainsKey).ToList())
				{
					if (map.ContainsKey(id)) continue;
					string to = id + "-" + suffix;
					for (int i = 2; theirs.ContainsKey(to) || ours.ContainsKey(to) || map.Values.Contains(to, StringComparer.OrdinalIgnoreCase); i++) to = id + "-" + suffix + "-" + i;
					map[id] = to;
					report.Add("Its story item '" + ours[id] + "' has the id '" + to + "' here ('" + id + "' in the pack): '" + theirs[id] + "' has a story item '" + id + "' too - in a world with both, they stay two things");
				}
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Comparing story items: " + ex.Message); map.Clear(); }
			return map;
		}

		static byte[] Rewritten(byte[] bytes, List<string> originals, Dictionary<string, string> target, HashSet<string> planRuleIds, Dictionary<string, string> storyIds)
		{
			if (!originals.Any(n => !target[n].Equals(n, StringComparison.Ordinal)) && storyIds.Count == 0) return bytes;
			string tmp = Path.Combine(LibraryFolder, "rewrite.tmp" + IslandFile.Extension);
			Directory.CreateDirectory(LibraryFolder);
			try
			{
				File.WriteAllBytes(tmp, bytes);
				IslandFile f = IslandFile.Load(tmp);
				bool changed = StoryItems.RenameIds(f, storyIds);
				string text;
				if (f.Props != null && f.Props.TryGetValue(WorldDirector.IslandRulesKey, out text))
				{
					List<IntroRule> rules = IntroRule.ParseLines(text);
					string before = IntroRule.ToLines(rules);
					var ids = new HashSet<string>(planRuleIds, StringComparer.OrdinalIgnoreCase);
					ids.UnionWith(rules.Select(x => x.Id));
					foreach (IntroRule r in rules) Rename(r, target, ids);
					if (IntroRule.ToLines(rules) != before) { WorldDirector.SetRulesInProps(f.Props, rules); changed = true; }
				}
				if (!changed) return bytes;
				f.Save(tmp);
				return File.ReadAllBytes(tmp);
			}
			finally { try { File.Delete(tmp); } catch { } }
		}

		/// <summary>A rule's island names (what it brings, and the islands it waits for or is placed near) after renames.
		/// ruleIds: rules a reference may name instead (a reference finds a rule first, then an island of that name) - those
		/// stay: "after rule 'camp'" waited for a rule that didn't exist when the island 'camp' was renamed.</summary>
		static void Rename(IntroRule r, Dictionary<string, string> target, HashSet<string> ruleIds)
		{
			Func<string, string> to = n => { string t; return target.TryGetValue(n.Trim(), out t) ? t : n.Trim(); };
			Func<string, string> refTo = n => ruleIds.Contains(n.Trim()) ? n.Trim() : to(n);
			if (r.What == "island") r.WhatArg = to(r.WhatArg);
			else if (r.What == "oneof") r.WhatArg = string.Join(", ", r.WhatArg.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).Select(to).ToArray());
			if (r.WhenRef.Length > 0 && r.When != "rule") r.WhenRef = refTo(r.WhenRef);
			if (r.WhereRef.Length > 0) r.WhereRef = refTo(r.WhereRef);
		}

		static string FreeName(string name, string title, List<string> originals, Dictionary<string, string> target)
		{
			string clean = NamePart(title, 30);
			if (clean.Length == 0) clean = "library";
			for (int i = 1; ; i++)
			{
				string candidate = Fit(name, " (" + clean + ")" + (i > 1 ? " " + i : ""));
				bool used = File.Exists(IslandSpawner.PathFor(candidate)) || originals.Any(o => target[o].Equals(candidate, StringComparison.OrdinalIgnoreCase));
				if (!used) return candidate;
			}
		}

		/// <summary>name + tail within FileNames.MaxLength (the name shortened): a longer one installed, but this version
		/// refused it when the pack was exported again and imported, and players' PCs refused it from a host.</summary>
		static string Fit(string name, string tail)
		{
			if (name.Length + tail.Length <= FileNames.MaxLength) return name + tail;
			return name.Substring(0, Math.Max(1, FileNames.MaxLength - tail.Length)).TrimEnd('.', ' ') + tail;
		}

		/// <summary>The islands updates replaced, for the worlds that have them (T10: told once when such a world loads):
		/// "island|title|UTC ticks" lines in library/updated.txt.</summary>
		public static string UpdatedLog { get { return Path.Combine(LibraryFolder, "updated.txt"); } }

		static void NoteUpdated(List<string> islands, string title)
		{
			if (islands.Count == 0) return;
			try
			{
				Directory.CreateDirectory(LibraryFolder);
				string now = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
				File.AppendAllText(UpdatedLog, string.Concat(islands.Select(n => n + "|" + (title ?? "").Replace("|", " ") + "|" + now + Environment.NewLine).ToArray()));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [library] Noting the updated islands: " + e.Message); }
		}

		/// <summary>Islands an update replaced after the given time (UTC ticks), with the entry's title: island -> title.</summary>
		public static Dictionary<string, string> UpdatedSince(long ticks)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (!File.Exists(UpdatedLog)) return result;
				foreach (string line in File.ReadAllLines(UpdatedLog))
				{
					string[] p = line.Split('|');
					long t;
					if (p.Length < 3 || !long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out t) || t <= ticks) continue;
					result[p[0]] = p[1];
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [library] Reading the updated islands: " + e.Message); }
			return result;
		}

		/// <summary>A free name for the player's own version of a file an update replaces: "name (yours)", "name (yours) 2"...</summary>
		static string YoursName(string name, bool plan)
		{
			for (int i = 1; ; i++)
			{
				string candidate = Fit(name, " (yours)" + (i > 1 ? " " + i : ""));
				if (!File.Exists(plan ? WorldPlan.PathFor(candidate) : IslandSpawner.PathFor(candidate)) && !(plan && WorldPlan.IsBuiltIn(candidate))) return candidate;
			}
		}

		static string FreePlanName(string name, string author)
		{
			string clean = NamePart(author, 30);
			if (clean.Length == 0) clean = "library";
			for (int i = 1; ; i++)
			{
				string candidate = Fit(name, " (" + clean + ")" + (i > 1 ? " " + i : ""));
				if (!File.Exists(WorldPlan.PathFor(candidate)) && !WorldPlan.IsBuiltIn(candidate)) return candidate;
			}
		}

		/// <summary>Before an island file is replaced: saved worlds that use it keep playing this version (as its hash copy).</summary>
		static void KeepForWorlds(string island, byte[] next, Report report)
		{
			List<string> worlds = WorldsUsing(island);
			string hash = IslandNetwork.HashOf(island);
			if (worlds.Count == 0 || hash == null) return;
			// (before 2026-09-29 the old version was always kept: worlds never got a fixed quest or ground from an update)
			bool shifts = true;
			try { shifts = DynamicIslands.ShiftsState(IslandFile.Load(IslandSpawner.PathFor(island)), IslandFile.FromBytes(next, island)); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Comparing the versions of '" + island + "': " + e.Message + " - keeping the old one for saved worlds"); }
			if (!shifts)
			{
				report.Add(string.Join(", ", worlds.Select(w => "'" + w + "'").ToArray()) + " get" + (worlds.Count == 1 ? "s" : "") + " the new '" + island + "' too (its objects keep their order, so what was used there stays right)");
				return;
			}
			string copy = IslandSpawner.PathFor(IslandNetwork.DownloadName(island, hash));
			// (whole or not at all: a copy Raft stopped in was never written again, and the worlds were pointed at it)
			if (!File.Exists(copy)) SafeFile.WriteAllBytes(copy, File.ReadAllBytes(IslandSpawner.PathFor(island)));
			RepointWorlds(island, hash);
			report.Add("Kept the version of '" + island + "' that " + string.Join(", ", worlds.Select(w => "'" + w + "'").ToArray()) + " started with");
		}

		/// <summary>
		/// The version just kept as a copy (hash) is the one saved worlds play when their file names a version that is no
		/// longer on this PC: an earlier save that kept the order (no copy was needed then) replaced it, so what was used
		/// there still fits the kept one. Those worlds are pointed at it - they played the newest file, onto which their
		/// used objects no longer fit. Returns how many worlds.
		/// </summary>
		internal static int RepointWorlds(string island, string hash)
		{
			int count = 0;
			try
			{
				foreach (string file in WorldStateFiles())
				{
					string[] lines = File.ReadAllLines(file);
					bool changed = false;
					for (int i = 0; i < lines.Length; i++)
					{
						if (lines[i].StartsWith("@") || lines[i].StartsWith("#")) continue;
						string[] p = lines[i].Split('|');
						if (p.Length < 8 || !p[0].Trim().Equals(island, StringComparison.OrdinalIgnoreCase)) continue;
						string had = p[7].Trim();
						if (had.Length == 0 || had == hash || IslandNetwork.HashOf(island) == had || File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(island, had)))) continue;
						p[7] = hash;
						lines[i] = string.Join("|", p);
						changed = true;
					}
					if (!changed) continue;
					SafeFile.WriteAllLines(file, lines);
					count++;
					Debug.Log("[CUSTOM ISLANDS] " + Path.GetFileName(file) + ": '" + island + "' is played from the kept version " + hash);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Pointing saved worlds at the kept version of '" + island + "': " + e.Message); }
			return count;
		}

		/// <summary>Every file with a saved world's islands: the mod's worlds\*.txt and the copies in Raft's world folders and their saves (AU27).</summary>
		static List<string> WorldStateFiles()
		{
			var files = new List<string>();
			if (Directory.Exists(WorldsFolder)) files.AddRange(Directory.GetFiles(WorldsFolder, "*.txt"));
			try
			{
				string raftWorlds = SaveAndLoad.WorldPath;
				if (!string.IsNullOrEmpty(raftWorlds) && Directory.Exists(raftWorlds)) files.AddRange(Directory.GetFiles(raftWorlds, global::DynamicIslands.Editor.WorldCopy.FileName, SearchOption.AllDirectories));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking through Raft's world folders: " + e.Message); }
			return files;
		}

		/// <summary>The hash a world line plays an island from, when it isn't the island's file now ("" otherwise).</summary>
		static string OlderHash(string line, string island, string now)
		{
			if (line.StartsWith("@") || line.StartsWith("#")) return "";
			string[] p = line.Split('|');
			if (p.Length < 8 || !p[0].Trim().Equals(island, StringComparison.OrdinalIgnoreCase)) return "";
			string had = p[7].Trim();
			return had.Length == 0 || had == now ? "" : had;
		}

		/// <summary>
		/// R1c: the saved worlds that play an island from an older version (a kept copy &lt;island&gt;_&lt;hash&gt;, see
		/// KeepForWorlds) instead of its file now - by their names, without the editor's test world. Empty when none.
		/// </summary>
		public static List<string> WorldsOnOlderVersion(string island)
		{
			var result = new List<string>();
			try
			{
				string now = IslandNetwork.HashOf(island);
				if (now == null || IslandNetwork.IsDownloadName(island)) return result;
				foreach (string file in WorldStateFiles())
				{
					string[] lines = File.ReadAllLines(file);
					// (only a hash with its kept copy here: a world whose hash has no copy plays the island's file now - an
					// ordinary save that added objects changed the hash, and the world's line names the new one at its next save)
					if (!lines.Any(l => { string had = OlderHash(l, island, now); return had.Length > 0 && File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(island, had))); })) continue;
					string world = Housekeeping.WorldName(lines, file);
					if (!world.Equals(IslandTest.WorldName, StringComparison.OrdinalIgnoreCase)) result.Add(world);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking for worlds on an older '" + island + "': " + e.Message); }
			return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		/// <summary>
		/// R1c "Give my worlds this version": every saved world that plays an older copy of the island (the mod's world
		/// files and the copies in Raft's world folders) is pointed at the island's file now, and the old copies no world
		/// names any more go to deleted\kept versions (never erased; the worlds' lines name the new hash now, so a copy put
		/// back is only another island file).
		/// R1b: what was used there is carried to the same objects in the new file (StableIds.Carry) when the old copy is
		/// here and its rules and quest steps keep their places; otherwise the world's state stays as it was.
		/// carriedOnly: only the worlds whose state can be carried (the automatic step after a save) - the others keep their copy.
		/// Returns the worlds that changed; moved gets the copies that were moved aside.
		/// </summary>
		public static List<string> GiveWorldsThisVersion(string island, out List<string> moved) { int carried; return GiveWorldsThisVersion(island, out moved, false, out carried); }

		public static List<string> GiveWorldsThisVersion(string island, out List<string> moved, bool carriedOnly, out int carried)
		{
			moved = new List<string>();
			carried = 0;
			IslandFile current = null;
			var copies = new Dictionary<string, IslandFile>(StringComparer.OrdinalIgnoreCase);
			var worlds = new List<string>();
			string now = IslandNetwork.HashOf(island);
			if (now == null) throw new FileNotFoundException("'" + island + "' has no file");
			var olds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<string> files = WorldStateFiles();
			foreach (string file in files)
			{
				string[] lines = File.ReadAllLines(file);
				bool changed = false;
				for (int i = 0; i < lines.Length; i++)
				{
					string had = OlderHash(lines[i], island, now);
					if (had.Length == 0) continue;
					string[] p = lines[i].Split('|');
					string state = CarryState(island, had, p, ref current, copies);
					if (state == null && carriedOnly) continue;
					if (state != null) { p[4] = state; carried++; }
					p[7] = now;
					lines[i] = string.Join("|", p);
					olds.Add(had);
					changed = true;
				}
				if (!changed) continue;
				// (not the editor's test world: it is made again for each test, and its next save wrote the old hash back - CA29)
				string world = Housekeeping.WorldName(lines, file);
				if (world.Equals(IslandTest.WorldName, StringComparison.OrdinalIgnoreCase)) continue;
				SafeFile.WriteAllLines(file, lines);
				worlds.Add(world);
				Debug.Log("[CUSTOM ISLANDS] " + file + ": '" + island + "' is played from its file now (" + now + ")");
			}
			// (the old copies: only those no world line names any more, as its own island or by a hash of this one)
			var stillNamed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string file in files)
				foreach (string l in File.ReadAllLines(file))
				{
					if (l.StartsWith("@") || l.StartsWith("#")) continue;
					string[] p = l.Split('|');
					stillNamed.Add(p[0].Trim());
					if (p.Length >= 8 && p[0].Trim().Equals(island, StringComparison.OrdinalIgnoreCase) && p[7].Trim().Length > 0) stillNamed.Add(IslandNetwork.DownloadName(island, p[7].Trim()));
				}
			foreach (string had in olds)
			{
				string copy = IslandNetwork.DownloadName(island, had);
				string path = IslandSpawner.PathFor(copy);
				if (stillNamed.Contains(copy) || !File.Exists(path)) continue;
				try { PiecesFiles.MoveToDeleted(path, KeptVersionsFolder); moved.Add(copy); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not move the old copy " + copy + " aside: " + e.Message); }
			}
			if (moved.Count > 0) IslandCache.Forget();
			Debug.Log("[CUSTOM ISLANDS] Gave " + worlds.Count + " world(s) the version " + now + " of '" + island + "', moved " + moved.Count + " old cop" + (moved.Count == 1 ? "y" : "ies") + " aside");
			return worlds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		/// <summary>A world line's state of the island carried from its old copy (hash had) to the island's file now; null when it can't be.</summary>
		static string CarryState(string island, string had, string[] p, ref IslandFile current, Dictionary<string, IslandFile> copies)
		{
			try
			{
				if (p.Length < 8) return null;
				IslandFile was;
				if (!copies.TryGetValue(had, out was))
				{
					string path = IslandSpawner.PathFor(IslandNetwork.DownloadName(island, had));
					copies[had] = was = File.Exists(path) ? IslandFile.Load(path) : null;
				}
				if (was == null) return null;
				if (current == null) current = IslandFile.Load(IslandSpawner.PathFor(island));
				if (!StableIds.ListsKept(was, current)) return null;
				int dropped;
				string unsure, state = StableIds.Carry(was, current, p[4], StableIds.FlyingAt(p[2]), out dropped, out unsure);
				if (state == null) Debug.Log("[CUSTOM ISLANDS] '" + island + "': a world's state can't be carried to the new version now - " + unsure);
				else if (dropped > 0) Debug.Log("[CUSTOM ISLANDS] '" + island + "': " + dropped + " saved thing(s) of removed objects left out");
				return state;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Carrying a world's state of '" + island + "': " + e.Message); return null; }
		}

		/// <summary>Where "Give my worlds this version" moves the old copies: Mods\DynamicIslands\deleted\&lt;this&gt;.</summary>
		public const string KeptVersionsFolder = "kept versions";

		/// <summary>The player's message for GiveWorldsThisVersion's result.</summary>
		public static string GaveText(string island, List<string> worlds, List<string> moved)
		{
			if (worlds.Count == 0) return "No saved world plays an older '" + island + "' - nothing was changed.";
			return "These worlds play this '" + island + "' now: " + string.Join(", ", worlds.Select(w => "'" + w + "'").ToArray()) + "." +
				(moved.Count > 0 ? " The old cop" + (moved.Count == 1 ? "y was" : "ies were") + " moved to Mods\\DynamicIslands\\" + IslandFilesWindow.DeletedFolderName + "\\" + KeptVersionsFolder + "." : "") +
				" What was picked, looted or opened there may now fit other objects.";
		}

		/// <summary>Sets an island's weight in spawnpool.txt (0 = never turns up by chance while sailing).</summary>
		public static void SetPoolWeight(string island, float weight)
		{
			try
			{
				string path = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
				CustomIslandSpawner.LoadPool(false); // (makes the file if it isn't there)
				var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
				string line = island + " " + weight.ToString(CultureInfo.InvariantCulture);
				int i = lines.FindIndex(l => PoolLineName(l).Equals(island, StringComparison.OrdinalIgnoreCase));
				if (i >= 0) lines[i] = line; else lines.Add(line);
				SafeFile.WriteAllLines(path, lines.ToArray());
				CustomIslandSpawner.LoadPool(true);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not change spawnpool.txt: " + e.Message); }
		}

		static void RemovePoolLine(string island)
		{
			try
			{
				string path = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
				if (!File.Exists(path)) return;
				var lines = File.ReadAllLines(path).ToList();
				if (lines.RemoveAll(l => PoolLineName(l).Equals(island, StringComparison.OrdinalIgnoreCase)) > 0) { SafeFile.WriteAllLines(path, lines.ToArray()); CustomIslandSpawner.LoadPool(true); }
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not change spawnpool.txt: " + e.Message); }
		}

		static string PoolLineName(string line)
		{
			string l = line.Trim();
			if (l.Length == 0 || l.StartsWith("#") || l.Contains("=")) return "";
			int sp = l.LastIndexOf(' ');
			float w;
			return sp > 0 && float.TryParse(l.Substring(sp + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out w) ? l.Substring(0, sp).Trim() : l;
		}

		#endregion

		#region Removing

		/// <summary>Removes an installed entry's files - never one that is shared or that a saved world still uses.</summary>
		public static Report Remove(string id)
		{
			var report = new Report();
			List<LibraryInstalled> all = Installed();
			LibraryInstalled entry = all.FirstOrDefault(e => e.id.Equals(id, StringComparison.OrdinalIgnoreCase));
			if (entry == null) { report.Add("Nothing called '" + id + "' is installed"); return report; }
			report.Add("Removing '" + entry.title + "' by " + entry.author);
			foreach (LibraryInstalledFile f in entry.files.Where(f => !f.shared)) RemoveFile(f, entry.id, all, report);
			all.Remove(entry);
			SaveInstalled(all);
			IslandCache.Forget();
			return report;
		}

		/// <summary>undo: an install's removal of an earlier version's file, put back if the install then fails.</summary>
		static void RemoveFile(LibraryInstalledFile f, string id, List<LibraryInstalled> all, Report report, InstallUndo undo = null)
		{
			bool plan = f.kind == KindPlan, type = f.kind == KindMapType;
			string path = PathOf(f);
			if (!File.Exists(path)) return;
			if (all.Any(e => !e.id.Equals(id, StringComparison.OrdinalIgnoreCase) && e.files.Any(x => x.kind == f.kind && x.name.Equals(f.name, StringComparison.OrdinalIgnoreCase))))
			{ report.Add("Kept '" + f.name + "': another installed entry uses it too"); return; }
			// (a map type: worlds keep the islands made from it as files - none needs the type itself)
			List<string> worlds = type ? new List<string>() : plan ? WorldsUsing(null, f.name) : WorldsUsing(f.name);
			if (worlds.Count > 0) { report.Add("Kept '" + f.name + "': " + (worlds.Count == 1 ? "the world " : "the worlds ") + string.Join(", ", worlds.Select(w => "'" + w + "'").ToArray()) + (worlds.Count == 1 ? " uses it" : " use it")); return; }
			if (undo != null) undo.Remember(path);
			// (to the deleted folder, never for good: the player may have changed it, and an update's undo is only in memory -
			// Raft stopping before installed.json was written lost the file)
			try { PiecesFiles.MoveToDeleted(path, "library"); }
			catch (Exception e) when (SafeFile.InUse(e)) { report.Add("Kept '" + f.name + "': it is in use by another program (close it there and Remove again)"); return; }
			if (!plan && !type) RemovePoolLine(f.name);
			if (type) MapTypeFiles.LoadAll();
			report.Add("Removed " + (plan ? "the plan '" : type ? "the map type '" : "'") + f.name + "' (kept in Mods\\DynamicIslands\\" + IslandFilesWindow.DeletedFolderName + "\\library)");
		}

		/// <summary>The copies of islands downloaded from multiplayer hosts (&lt;name&gt;_&lt;hash&gt;), with the saved worlds that use each
		/// (every copy of their state: the older saves in Raft's world folders too - Housekeeping).</summary>
		public static List<KeyValuePair<string, List<string>>> HostCopies() { return Housekeeping.Look().Copies; }

		/// <summary>Deletes the downloaded host copies no saved world uses. Returns how many.</summary>
		public static int RemoveUnusedHostCopies()
		{
			int n = 0;
			foreach (var c in HostCopies().Where(c => c.Value.Count == 0))
			{
				try { IslandFilesWindow.MoveToDeleted(c.Key); n++; } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not remove " + c.Key + ": " + e.Message); }
			}
			if (n > 0) { Log("Removed " + n + " unused island copies downloaded from hosts"); IslandCache.Forget(); }
			return n;
		}

		#endregion
	}

	/// <summary>Where a world's plan came from (WorldDirector.PlanFrom): an installed entry, or the player's own.</summary>
	public static class LibrarySource
	{
		/// <summary>"library:&lt;id&gt;@&lt;version&gt;" / "import:..." for a plan an entry installed, else null.</summary>
		public static string OfPlan(string planName)
		{
			LibraryInstalled e = LibraryPack.OwnerOf(planName + WorldPlan.Extension);
			return e != null ? e.Source : null;
		}

		/// <summary>An entry's title from a source string (or its id when it isn't installed here).</summary>
		public static string Describe(string source)
		{
			string rest = source.Substring(source.IndexOf(':') + 1);
			string id = rest.Split('@')[0];
			LibraryInstalled e = LibraryPack.Installed().FirstOrDefault(x => x.id.Equals(id, StringComparison.OrdinalIgnoreCase));
			return e != null ? e.title : id;
		}
	}
}
