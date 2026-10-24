using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CommandUndoRedo;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The ranges a map type's settings are rolled from, as data: one of the styles, the layout, radius and height between
	/// their min and max, roughness around its value (±0.15), 1 to Peaks peaks, the object density - and Raft-like object
	/// amounts if asked. Random: the random island's own roll (IslandGenerator.RandomSettings) over the styles instead.
	/// The built-in types roll the same way as before (the same seed gives the same island).
	/// </summary>
	public class MapTypeRanges
	{
		public const string LikeRaftLarge = "large", LikeRaftSmall = "small";

		public bool Random;
		public int[] Styles = { TerrainPainter.Tropical };
		public int Shape = IslandShapes.Round;
		public float RadiusMin = 78f, RadiusMax = 113f, HeightMin = 15f, HeightMax = 30f, Roughness = 0.5f, Density = 0.5f;
		public int Peaks = 3;
		/// <summary>"" = as rolled, "large" / "small": objects as dense as on Raft's own big / small islands (RandomizerIslands).</summary>
		public string LikeRaft = "";

		static float R(System.Random rnd, float min, float max) { return min + (float)rnd.NextDouble() * (max - min); }

		public IslandGenSettings Roll(System.Random rnd)
		{
			int[] styles = Styles != null && Styles.Length > 0 ? Styles : new[] { TerrainPainter.Tropical };
			IslandGenSettings s;
			if (Random) s = IslandGenerator.RandomSettings(rnd, styles);
			else
			{
				// (one style: no roll for it, as the built-in types had it - their seeds give the islands they gave)
				int style = styles.Length == 1 ? styles[0] : styles[rnd.Next(styles.Length)];
				s = new IslandGenSettings
				{
					Seed = rnd.Next(1, 999999), Style = style, Shape = Shape, Radius = R(rnd, RadiusMin, RadiusMax), Height = R(rnd, HeightMin, HeightMax),
					Roughness = Mathf.Clamp01(Roughness + R(rnd, -0.15f, 0.15f)), Peaks = 1 + rnd.Next(Mathf.Max(1, Peaks)), ObjectDensity = Density,
				};
			}
			if (LikeRaft == LikeRaftLarge) RandomizerIslands.LikeRaft(s);
			else if (LikeRaft == LikeRaftSmall) RandomizerIslands.LikeRaftSmall(s);
			return s;
		}

		/// <summary>The same ranges (what differs, else "").</summary>
		public string Differs(MapTypeRanges o)
		{
			if (o == null) return "no ranges";
			var d = new List<string>();
			if (Random != o.Random) d.Add("settings");
			if (!(Styles ?? new int[0]).SequenceEqual(o.Styles ?? new int[0])) d.Add("styles");
			if (!Random)
			{
				if (Shape != o.Shape) d.Add("shape");
				if (RadiusMin != o.RadiusMin || RadiusMax != o.RadiusMax) d.Add("radius");
				if (HeightMin != o.HeightMin || HeightMax != o.HeightMax) d.Add("height");
				if (Roughness != o.Roughness) d.Add("roughness");
				if (Density != o.Density) d.Add("density");
				if (Peaks != o.Peaks) d.Add("peaks");
			}
			if ((LikeRaft ?? "") != (o.LikeRaft ?? "")) d.Add("likeraft");
			return string.Join(", ", d.ToArray());
		}
	}

	/// <summary>
	/// Map types of one's own (ROADMAP T7): Mods\DynamicIslands\maptypes\&lt;name&gt;.maptype, key = value lines like a plan
	/// (the help at the top of each file says them all). Read when the mod starts (and by ExportMapType / ReloadMapTypes)
	/// and added after the built-in types in MapTypes.All, so plans ("type:&lt;name&gt;"), spawnpool.txt, the quest editor and
	/// the generator's Ready-made tab use them like the built-in ones. A file with a built-in type's name, or one that can't
	/// be read, is left out with a line in the log - never more (a line with a setting this version doesn't know is kept
	/// and warned about, the rest of the file read). ExportMapType writes a built-in type as such a file (an
	/// example to start from); ReRollMapType puts a type's content (not its land) onto the island in the editor.
	/// </summary>
	public static class MapTypeFiles
	{
		public const string Extension = ".maptype";

		public static string Folder { get { return Path.Combine(DynamicIslands.assetpath, "maptypes"); } }
		public static string PathFor(string name) { return Path.Combine(Folder, name + Extension); }

		/// <summary>The files read at the last load that were left out, and why (tests, the log).</summary>
		public static readonly List<string> Skipped = new List<string>();

		/// <summary>Lines with a setting this version doesn't know, read since the last load: kept in the type, warned about (tests, the log).</summary>
		public static readonly List<string> Unknown = new List<string>();

		static readonly Regex NameRule = new Regex("^[A-Za-z0-9][A-Za-z0-9_-]{0,47}$");

		/// <summary>Why a map type file name can't be used, or null.</summary>
		public static string NameProblem(string name)
		{
			if (!NameRule.IsMatch(name ?? "")) return "a map type's name is letters, digits, - and _ (up to 48, no spaces)";
			if (MapTypes.BuiltIn(name) != null) return "'" + name + "' is a built-in map type's name";
			return null;
		}

		public const string Help =
@"# Custom Islands map type: a kind of island the generator makes on its own (ROADMAP T7).
# The file's name is the type's name: plans bring it with  type:<name>, spawnpool.txt with  type:<name> <weight>.
# Built-in types stay in the mod; a file with a built-in type's name is left out. ExportMapType <type> writes a
# built-in type as a file to start from; ReloadMapTypes reads the files again; ReRollMapType <type> [seed] puts a
# type's content onto the island open in the editor (Ctrl+Z takes it off). Lines starting with # are notes.
#
# label = shown in lists (the generator's Ready-made tab, the quest editor)      description = one line about it
# title = shown to players on arrival (empty: none)
# flying = <chance 0-1> | <min m> - <max m>     (how often it floats in the air, and how high)
# sunken = <metres>                             (under water: its top this far below the surface)
#
# The land: settings = ranges (rolled from the lines below) / random (the random island's roll over the styles)
#                      / <built-in type> (that type's own settings code, e.g. large)
#   styles = tropical, forest      (one is picked; a style twice is picked twice as often)
#   shape = round / atoll / archipelago / seastacks / plateau / marsh / crescent / twinpeaks / raftsmallisland / raftlargeisland
#   radius = 78 - 113    height = 15 - 30    (metres; one number = always that)
#   roughness = 0.45     (0-1, rolled ±0.15)     density = 0.5   (objects 0-1)     peaks = 3   (1 to this many)
#   likeraft = off / large / small   (trees, plants and pickups as dense as on Raft's own big or small islands)
#   set = <generator setting> <value>   (after rolling, any of the generator's settings by name, e.g.
#         set = Hostiles 4    set = SeaFloor 1    set = Cliffs 0.4    - as in a generator preset file)
#
# The content (on the land; each line is placed in order - a spot that can't be found leaves that line out):
#   content = <built-in type>   (that type's content code first, e.g. boss; a no-land type's raft too: wreck)
#   chest = <object> | <where> | <title> | <loot> | <note text>
#           loot: Basics / Metal / Food / Treasure, or items as in the loot editor (Plank*12;Rope*4)
#   note = <object> | <where> | <title> | <text>            (Note_Paper, Note_Bottle, Note_Sign, Note_Board...)
#   creature = <type> | <where> | <count> | <difficulty> | <size> | <zone> | <respawn yes/no>
#           type as in Creature_<type> (Bear, Rat, PolarBear, Turtle...); difficulty Easy/Normal/Hard/Boss...
#   zone = <where> | <zone name> | <radius m> | <message>
#   atmosphere = <where> | <radius m> | <fog #RRGGBB> | <fog 0-1> | <light #RRGGBB> | <light 0-1> | <particles>
#           particles: none, fireflies, mist, snow, embers, bubbles
#   object = <object name> | <where> | <turn in degrees (empty: random)>
#   clear = <where> | <radius m>                            (removes trees and plants there)
#   quest = <title> | <intro> | <done text> | <reward>      then one line per step:
#   step = <type> | <target> | <count> | <text>             (read, open, kill, reach, ... as in the quest editor)
#   Texts: \n = a new line.
#   <where>: mid (the middle)   top[:f] (the highest ground within f x radius, 0.5)   dry[:f] (dry flat ground, 1)
#            beach[:f] (1.3)   water[:f] (a lagoon, 0.4)   seabed[:f] (under water, 1.2)   near[:m] (dry ground within
#            m metres of the line before's spot, 25)   islet:<n> / stack:<n> (the n-th islet / sea stack, 1 = the biggest)
#            at:<x>,<z> (metres from the middle)   and @<m> after any of them: that many metres up (top:0.6@2)
";

		#region Reading

		/// <summary>Reads every .maptype file into MapTypes.All (after the built-in types; the files' types read before go first).</summary>
		public static int LoadAll()
		{
			MapTypes.All.RemoveAll(t => t.FromFile != null);
			Skipped.Clear();
			Unknown.Clear();
			int n = 0;
			string[] files;
			try { files = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*" + Extension).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray() : new string[0]; }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not list the map type files: " + e.Message); return 0; }
			foreach (string path in files)
			{
				string name = Path.GetFileNameWithoutExtension(path);
				try
				{
					if (MapTypes.BuiltIn(name) != null) { Skip(name, "it has the name of a built-in map type (built-in types stay as they are - name the file something else)"); continue; }
					if (MapTypes.Get(name) != null) { Skip(name, "a map type file of that name was read already"); continue; }
					string problem = NameProblem(name);
					if (problem != null) { Skip(name, problem); continue; }
					string error;
					MapType t = Parse(name, File.ReadAllText(path), out error);
					if (t == null) { Skip(name, error); continue; }
					t.FromFile = path;
					MapTypes.All.Add(t);
					n++;
				}
				catch (Exception e) { Skip(name, e.Message); }
			}
			if (n > 0) Debug.Log("[CUSTOM ISLANDS] Map types of files: " + string.Join(", ", MapTypes.All.Where(t => t.FromFile != null).Select(t => t.Name).ToArray()));
			return n;
		}

		class UnknownSettingException : FormatException { public UnknownSettingException(string m) : base(m) { } }

		static void WarnUnknown(string name, int line, string why)
		{
			Unknown.Add(name + ": line " + (line + 1) + ": " + why);
			Debug.LogWarning("[CUSTOM ISLANDS] Map type file '" + name + Extension + "' line " + (line + 1) + ": " + why + " - kept, it may need a newer Custom Islands (the rest is read)");
		}

		static void Skip(string name, string why)
		{
			Skipped.Add(name + ": " + why);
			Debug.LogWarning("[CUSTOM ISLANDS] Map type file '" + name + Extension + "' left out: " + why);
		}

		class Rule
		{
			public string Kind;
			public string[] Parts;
			public Where At;
		}

		/// <summary>A spot: kind, its number (NaN = the kind's default), x/z for at:, metres up.</summary>
		class Where
		{
			public string Kind;
			public float Arg = float.NaN, X, Z, Lift;
		}

		static readonly string[] WhereKinds = { "mid", "top", "dry", "beach", "water", "seabed", "near", "islet", "stack", "at" };
		/// <summary>The content lines and how many parts each needs at least (the first is where for zone, atmosphere, clear).</summary>
		static readonly Dictionary<string, int> RuleParts = new Dictionary<string, int>
		{
			{ "chest", 4 }, { "note", 4 }, { "creature", 2 }, { "zone", 3 }, { "atmosphere", 2 }, { "object", 2 }, { "clear", 2 }, { "quest", 1 }, { "step", 2 },
		};

		static float Num(string v)
		{
			float f;
			if (!float.TryParse((v ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) || float.IsNaN(f) || float.IsInfinity(f)) throw new FormatException("'" + v + "' isn't a number");
			return f;
		}

		static void Range(string v, out float min, out float max)
		{
			string[] p = v.Split(new[] { " - ", "-" }, StringSplitOptions.RemoveEmptyEntries);
			if (p.Length == 1) { min = max = Num(p[0]); return; }
			if (p.Length != 2) throw new FormatException("'" + v + "' isn't a range (min - max)");
			min = Num(p[0]); max = Num(p[1]);
			if (max < min) throw new FormatException("'" + v + "': the max is below the min");
		}

		static string ShapeKey(int shape) { return IslandShapes.Names[shape].Replace(" ", "").ToLowerInvariant(); }

		static int ShapeOf(string v)
		{
			string k = v.Replace(" ", "").ToLowerInvariant();
			if (k == "stacks") k = "seastacks";
			for (int i = 0; i < IslandShapes.Names.Length; i++) if (ShapeKey(i) == k) return i;
			throw new FormatException("no shape '" + v + "' (" + string.Join(", ", Enumerable.Range(0, IslandShapes.Names.Length).Select(ShapeKey).ToArray()) + ")");
		}

		static int StyleOf(string v)
		{
			for (int i = 0; i < TerrainPainter.Styles.Length; i++) if (TerrainPainter.StyleName(i).Equals(v.Trim(), StringComparison.OrdinalIgnoreCase)) return i;
			throw new FormatException("no style '" + v.Trim() + "'");
		}

		static Where ParseWhere(string v)
		{
			var w = new Where();
			v = v.Trim().ToLowerInvariant();
			int at = v.IndexOf('@');
			if (at >= 0) { w.Lift = Num(v.Substring(at + 1)); v = v.Substring(0, at); }
			int colon = v.IndexOf(':');
			w.Kind = colon >= 0 ? v.Substring(0, colon) : v;
			string arg = colon >= 0 ? v.Substring(colon + 1) : "";
			if (!WhereKinds.Contains(w.Kind)) throw new FormatException("no spot '" + w.Kind + "' (" + string.Join(", ", WhereKinds) + ")");
			if (w.Kind == "at")
			{
				string[] xz = arg.Split(',');
				if (xz.Length != 2) throw new FormatException("at:<x>,<z> needs two numbers");
				w.X = Num(xz[0]); w.Z = Num(xz[1]);
			}
			else if (arg.Length > 0) w.Arg = Num(arg);
			if ((w.Kind == "islet" || w.Kind == "stack") && !(w.Arg >= 1f)) throw new FormatException(w.Kind + ":<n> needs a number from 1");
			return w;
		}

		static bool Yes(string v) { v = v.Trim().ToLowerInvariant(); return v == "yes" || v == "on" || v == "1" || v == "true"; }

		static string Text(string v) { return (v ?? "").Trim().Replace("\\n", "\n"); }

		/// <summary>Checks a "set" line: a generator setting by name and a value it takes.</summary>
		static void CheckSet(string v)
		{
			string[] p = v.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
			if (p.Length != 2) throw new FormatException("set = <setting> <value>");
			FieldInfo f = typeof(IslandGenSettings).GetField(p[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			if (f == null) throw new UnknownSettingException("the generator has no setting '" + p[0] + "'");
			if (f.FieldType == typeof(float)) Num(p[1]);
			else if (f.FieldType == typeof(int)) { int i; if (!int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) throw new FormatException("'" + p[1] + "' isn't a whole number"); }
			else if (f.FieldType == typeof(bool)) { bool b; if (!bool.TryParse(p[1].Trim(), out b)) throw new FormatException("'" + p[1] + "' isn't true or false"); }
			else if (f.FieldType != typeof(string)) throw new FormatException("'" + p[0] + "' can't be set from a file");
		}

		/// <summary>Applies "set" lines (CheckSet's) to rolled settings.</summary>
		public static void ApplySets(IslandGenSettings s, IList<string> sets)
		{
			if (sets == null) return;
			foreach (string v in sets)
			{
				string[] p = v.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (p.Length != 2) continue;
				FieldInfo f = typeof(IslandGenSettings).GetField(p[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
				if (f == null) continue;
				string x = p[1].Trim();
				if (f.FieldType == typeof(float)) f.SetValue(s, float.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture));
				else if (f.FieldType == typeof(int)) f.SetValue(s, int.Parse(x, NumberStyles.Integer, CultureInfo.InvariantCulture));
				else if (f.FieldType == typeof(bool)) f.SetValue(s, bool.Parse(x));
				else if (f.FieldType == typeof(string)) f.SetValue(s, x);
			}
		}

		/// <summary>A map type from a file's text, or null and why (the line and what is wrong with it).</summary>
		public static MapType Parse(string name, string text, out string error)
		{
			error = null;
			var t = new MapType { Name = name, Label = name, Description = "", Title = "", Sets = new List<string>() };
			var ranges = new MapTypeRanges { Styles = new[] { TerrainPainter.Tropical } };
			string settings = "ranges";
			var rules = new List<Rule>();
			string[] lines = (text ?? "").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;
				int eq = line.IndexOf('=');
				if (eq <= 0) { error = "line " + (i + 1) + ": no '=' in '" + line + "'"; return null; }
				string key = line.Substring(0, eq).Trim().ToLowerInvariant(), value = line.Substring(eq + 1).Trim();
				try
				{
					switch (key)
					{
						case "label": t.Label = value.Length > 0 ? value : name; break;
						case "description": t.Description = value; break;
						case "title": t.Title = value; break;
						case "flying":
						{
							string[] p = value.Split('|');
							t.FlyingChance = Mathf.Clamp01(Num(p[0]));
							if (p.Length > 1) { float a, b; Range(p[1], out a, out b); t.FlyingMin = Mathf.Clamp(a, 0f, IslandSpawner.MaxElevation); t.FlyingMax = Mathf.Clamp(b, 0f, IslandSpawner.MaxElevation); }
							break;
						}
						case "sunken": t.SunkenDepth = Mathf.Clamp(Num(value), 0f, -IslandSpawner.MinElevation); break;
						case "settings":
							settings = value.ToLowerInvariant();
							if (settings != "ranges" && settings != "random" && MapTypes.BuiltIn(settings) == null) throw new FormatException("settings = ranges, random or a built-in map type (no '" + value + "')");
							break;
						case "styles": ranges.Styles = value.Split(',').Where(x => x.Trim().Length > 0).Select(StyleOf).ToArray(); if (ranges.Styles.Length == 0) throw new FormatException("no style"); break;
						case "shape": ranges.Shape = ShapeOf(value); break;
						case "radius": Range(value, out ranges.RadiusMin, out ranges.RadiusMax); break;
						case "height": Range(value, out ranges.HeightMin, out ranges.HeightMax); break;
						case "roughness": ranges.Roughness = Mathf.Clamp01(Num(value)); break;
						case "density": ranges.Density = Mathf.Clamp01(Num(value)); break;
						case "peaks": ranges.Peaks = Mathf.Clamp(Mathf.RoundToInt(Num(value)), 1, IslandGenSettings.MaxPeaks); break;
						case "likeraft":
						{
							string v = value.ToLowerInvariant();
							if (v == "off" || v == "no" || v == "") ranges.LikeRaft = "";
							else if (v == MapTypeRanges.LikeRaftLarge || v == "on" || v == "yes") ranges.LikeRaft = MapTypeRanges.LikeRaftLarge;
							else if (v == MapTypeRanges.LikeRaftSmall) ranges.LikeRaft = MapTypeRanges.LikeRaftSmall;
							else throw new FormatException("likeraft = off, large or small");
							break;
						}
						case "set":
							// (a generator setting this version doesn't know: kept - ApplySets passes it by - and warned about)
							try { CheckSet(value); }
							catch (UnknownSettingException e) { WarnUnknown(name, i, e.Message); }
							t.Sets.Add(value);
							break;
						case "content":
						{
							MapType b = MapTypes.BuiltIn(value);
							if (value.Length > 0 && !value.Equals("none", StringComparison.OrdinalIgnoreCase) && b == null) throw new FormatException("content = a built-in map type (no '" + value + "')");
							if (b != null && b.Content == null && b.Build == null) throw new FormatException("the built-in type '" + value + "' has no content");
							t.ContentFrom = b != null ? b.Name : "";
							break;
						}
						case "modversion": break;
						default:
						{
							int need;
							// (a setting this version doesn't know - a newer Custom Islands' file: the line is kept and warned about, the rest read)
							if (!RuleParts.TryGetValue(key, out need)) { WarnUnknown(name, i, "no setting '" + key + "'"); t.Rules.Add(line); break; }
							string[] parts = value.Split('|').Select(p => p.Trim()).ToArray();
							if (parts.Length < need || parts[0].Length == 0) throw new FormatException(key + " needs at least " + need + " parts split by |");
							var r = new Rule { Kind = key, Parts = parts };
							int wherePart = key == "zone" || key == "atmosphere" || key == "clear" ? 0 : key == "quest" || key == "step" ? -1 : 1;
							if (wherePart >= 0) r.At = ParseWhere(parts[wherePart]);
							// (the numbers checked now: a typo leaves the file out with its line, not an island without its content)
							if (key == "creature") { if (parts.Length > 2 && parts[2].Length > 0) Num(parts[2]); if (parts.Length > 4 && parts[4].Length > 0) Num(parts[4]); if (parts.Length > 3 && parts[3].Length > 0 && !ObjectProps.Presets.Any(p => p.Key.Equals(parts[3], StringComparison.OrdinalIgnoreCase))) throw new FormatException("no difficulty '" + parts[3] + "'"); }
							if (key == "zone" || key == "atmosphere" || key == "clear") Num(parts[key == "zone" ? 2 : 1]);
							if (key == "atmosphere") { if (parts.Length > 3 && parts[3].Length > 0) Num(parts[3]); if (parts.Length > 5 && parts[5].Length > 0) Num(parts[5]); if (parts.Length > 6 && parts[6].Length > 0 && !AtmosphereZone.ParticleKinds.Contains(parts[6].ToLowerInvariant())) throw new FormatException("no particles '" + parts[6] + "'"); }
							if (key == "object" && parts.Length > 2 && parts[2].Length > 0) Num(parts[2]);
							if (key == "step" && parts.Length > 2 && parts[2].Length > 0) Num(parts[2]);
							if (key == "step" && !rules.Any(x => x.Kind == "quest")) throw new FormatException("a step before the quest line");
							rules.Add(r);
							t.Rules.Add(key + " = " + string.Join(" | ", parts));
							break;
						}
					}
				}
				catch (Exception e) { error = "line " + (i + 1) + " (" + key + "): " + e.Message; return null; }
			}
			if (rules.Count(r => r.Kind == "quest") > 1) { error = "more than one quest line (an island has one quest)"; return null; }

			// The land
			List<string> sets = t.Sets;
			if (settings == "ranges" || settings == "random")
			{
				ranges.Random = settings == "random";
				t.Ranges = ranges;
			}
			else
			{
				// (its code, or its ranges when it has them)
				MapType from = MapTypes.BuiltIn(settings);
				t.SettingsFrom = from.Name;
				t.Settings = from.Settings;
				t.Ranges = from.Settings == null ? from.Ranges : null;
				if (t.Settings == null && t.Ranges == null) { error = "the built-in type '" + settings + "' has no settings to use"; return null; }
			}

			// The content: the built-in type's first, then the file's lines
			MapType content = t.ContentFrom.Length > 0 ? MapTypes.BuiltIn(t.ContentFrom) : null;
			if (content != null) t.Build = content.Build;
			Action<MapKit, IslandGenSettings> code = content != null ? content.Content : null;
			if (code != null || rules.Count > 0)
				t.Content = (k, s) =>
				{
					if (code != null) code(k, s);
					Apply(k, s, rules, name);
				};
			return t;
		}

		#endregion

		#region The content lines

		static Vector2? Spot(MapKit k, IslandGenSettings s, Where w, Vector2 last)
		{
			float f = w.Arg;
			switch (w.Kind)
			{
				case "mid": return k.Mid;
				case "at": return k.Mid + new Vector2(w.X, w.Z);
				case "top": return k.Highest(k.Mid, s.Radius * (float.IsNaN(f) ? 0.5f : f));
				case "dry": return k.Find(k.Mid, s.Radius * (float.IsNaN(f) ? 1f : f), MapKit.Dry);
				case "beach": return k.Find(k.Mid, s.Radius * (float.IsNaN(f) ? 1.3f : f), MapKit.Beach);
				case "water": return k.Find(k.Mid, s.Radius * (float.IsNaN(f) ? 0.4f : f), (above, slope) => above < -1.5f);
				case "seabed": return k.Find(k.Mid, s.Radius * (float.IsNaN(f) ? 1.2f : f), (above, slope) => above > -4f && above < 0f && slope < 35f, 3f);
				case "near": return k.Find(last, float.IsNaN(f) ? 25f : f, MapKit.Dry, 6f);
				case "islet":
				case "stack":
				{
					List<Vector4> spots = w.Kind == "islet" ? IslandGenerator.Islets(s) : IslandGenerator.StackSpots(s);
					int n = Mathf.RoundToInt(f) - 1;
					if (n < 0 || n >= spots.Count) return null;
					return k.Highest(k.FromMid(spots[n]), spots[n].z * (w.Kind == "islet" ? 0.4f : 0.5f));
				}
			}
			return null;
		}

		static string Part(string[] p, int i, string fallback = "") { return i < p.Length && p[i].Length > 0 ? p[i] : fallback; }
		static float Part(string[] p, int i, float fallback) { return i < p.Length && p[i].Length > 0 ? Num(p[i]) : fallback; }

		static string LootOf(string v)
		{
			var preset = ContentCatalog.LootPresets.FirstOrDefault(x => x.Key.Equals(v.Trim(), StringComparison.OrdinalIgnoreCase));
			return preset.Value != null ? ContentCatalog.PresetLoot(preset.Value) : v.Trim();
		}

		/// <summary>Places a file's content lines (in order) and its quest.</summary>
		static void Apply(MapKit k, IslandGenSettings s, List<Rule> rules, string type)
		{
			Vector2 last = k.Mid;
			string[] quest = null;
			var steps = new List<string>();
			foreach (Rule r in rules)
			{
				string[] p = r.Parts;
				try
				{
					if (r.Kind == "quest") { quest = p; continue; }
					if (r.Kind == "step") { steps.Add(Part(p, 0) + "|" + Part(p, 1) + "|" + Mathf.RoundToInt(Part(p, 2, 1f)) + "|" + Text(Part(p, 3))); continue; }
					Vector2? at = Spot(k, s, r.At, last);
					// (a chest, note or zone a quest step may need: at the island's top when its spot isn't on this island -
					// no lagoon, fewer islets - rather than a quest that can't be finished)
					if (!at.HasValue && (r.Kind == "chest" || r.Kind == "note" || r.Kind == "zone")) at = k.Highest(k.Mid, s.Radius * 0.5f);
					if (!at.HasValue) continue;
					last = at.Value;
					float lift = r.At.Lift;
					switch (r.Kind)
					{
						case "chest": k.Chest(p[0], at.Value, Text(p[2]), LootOf(p[3]), Text(Part(p, 4)), lift); break;
						case "note": k.Add(p[0], k.At(at.Value, lift), (float)k.Rnd.NextDouble() * 360f, new Dictionary<string, string> { { ObjectProps.NoteTitle, Text(p[2]) }, { ObjectProps.NoteText, Text(p[3]) } }); break;
						case "creature":
						{
							string preset = ObjectProps.Presets.Select(x => x.Key).FirstOrDefault(x => x.Equals(Part(p, 3, "Normal"), StringComparison.OrdinalIgnoreCase)) ?? "Normal";
							string zone = Part(p, 5);
							k.Creature(p[0], at.Value, Mathf.RoundToInt(Part(p, 2, 1f)), preset, Part(p, 4, 1f), zone.Length > 0 ? zone : null, p.Length <= 6 || p[6].Length == 0 || Yes(p[6]), lift);
							break;
						}
						case "zone": k.Zone(at.Value, p[1], Num(p[2]), Text(Part(p, 3))); break;
						case "atmosphere": k.Atmosphere(at.Value, Num(p[1]), Part(p, 2, "#9AA5B1"), Part(p, 3, 0.3f), Part(p, 4, "#FFFFFF"), Part(p, 5, 0.2f), Part(p, 6, "none").ToLowerInvariant()); break;
						case "object": k.Add(p[0], k.At(at.Value, lift), p.Length > 2 && p[2].Length > 0 ? Num(p[2]) : (float)k.Rnd.NextDouble() * 360f); break;
						case "clear": k.Clear(at.Value, Num(p[1])); break;
					}
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Map type '" + type + "': " + r.Kind + " = " + string.Join(" | ", p) + ": " + e.Message); }
			}
			if (quest != null) k.Quest(Text(quest[0]), Text(Part(quest, 1)), Text(Part(quest, 2)), Text(Part(quest, 3)), steps.ToArray());
		}

		#endregion

		#region Writing

		static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
		static string FRange(float a, float b) { return a == b ? F(a) : F(a) + " - " + F(b); }

		/// <summary>
		/// A type as a .maptype file's text. A built-in type's settings are written as ranges where it has them (else
		/// "settings = &lt;its name&gt;", its code), its content as "content = &lt;its name&gt;" (its code: the file's own lines
		/// can come after it or replace it). label: another label ("" = its own).
		/// </summary>
		public static string ToText(MapType t, string label = "")
		{
			var lines = new List<string> { Help, "label = " + (label.Length > 0 ? label : t.Label), "description = " + (t.Description ?? "").Replace("\n", " ") };
			lines.Add("title = " + (t.Title ?? ""));
			if (t.FlyingChance > 0f) lines.Add("flying = " + F(t.FlyingChance) + " | " + FRange(t.FlyingMin, t.FlyingMax));
			if (t.SunkenDepth > 0f) lines.Add("sunken = " + F(t.SunkenDepth));
			lines.Add("");
			string settingsFrom = t.FromFile == null ? (t.Ranges == null ? t.Name : "") : t.SettingsFrom;
			MapTypeRanges r = t.Ranges;
			if (settingsFrom.Length > 0)
			{
				lines.Add("# (the land as the built-in type '" + settingsFrom + "' rolls it, in its own code)");
				lines.Add("settings = " + settingsFrom);
			}
			else if (r != null)
			{
				lines.Add("settings = " + (r.Random ? "random" : "ranges"));
				lines.Add("styles = " + string.Join(", ", (r.Styles ?? new int[0]).Select(x => TerrainPainter.StyleName(x).ToLowerInvariant()).ToArray()));
				if (!r.Random)
				{
					lines.Add("shape = " + ShapeKey(r.Shape));
					lines.Add("radius = " + FRange(r.RadiusMin, r.RadiusMax));
					lines.Add("height = " + FRange(r.HeightMin, r.HeightMax));
					lines.Add("roughness = " + F(r.Roughness));
					lines.Add("density = " + F(r.Density));
					lines.Add("peaks = " + r.Peaks);
				}
				lines.Add("likeraft = " + ((r.LikeRaft ?? "").Length > 0 ? r.LikeRaft : "off"));
			}
			foreach (string s in t.Sets ?? new List<string>()) lines.Add("set = " + s);
			lines.Add("");
			string contentFrom = t.FromFile == null ? (t.Content != null || t.Build != null ? t.Name : "") : t.ContentFrom;
			if (contentFrom.Length > 0)
			{
				lines.Add("# (the content of the built-in type '" + contentFrom + "', in its own code: add your own lines below it, or take it out and write all of it)");
				lines.Add("content = " + contentFrom);
			}
			lines.AddRange(t.FromFile == null ? new List<string>() : t.Rules);
			lines.Add("modversion = " + LibraryPack.ModVersion);
			return string.Join("\r\n", lines.ToArray()) + "\r\n";
		}

		/// <summary>Writes a type as maptypes\&lt;fileName&gt;.maptype (not over an existing file) and reads the files again. Null and why, or the path.</summary>
		public static string Export(string type, string fileName, out string error)
		{
			error = null;
			MapType t = MapTypes.Get(type);
			if (t == null) { error = "There is no map type '" + type + "' (" + string.Join(", ", MapTypes.All.Select(x => x.Name).ToArray()) + ")."; return null; }
			fileName = (fileName ?? "").Trim();
			if (fileName.Length == 0) fileName = "my-" + t.Name;
			string problem = NameProblem(fileName);
			if (problem != null) { error = "Can't name it '" + fileName + "': " + problem + "."; return null; }
			string path = PathFor(fileName);
			if (File.Exists(path)) { error = "There is a map type file '" + fileName + Extension + "' already - give another name, or delete that file first."; return null; }
			try
			{
				Directory.CreateDirectory(Folder);
				SafeFile.WriteAllText(path, ToText(t, t.FromFile == null ? t.Label + " (" + fileName + ")" : ""));
			}
			catch (Exception e) { error = "Writing " + path + " failed: " + e.Message; return null; }
			LoadAll();
			return path;
		}

		#endregion

		#region Re-rolling content in the editor

		/// <summary>
		/// Puts a type's content (chests, notes, creatures, zones, its quest and title - not its land) onto the island open
		/// in the editor, from a seed, as one undo step. The content is placed for the land that is there (its own radius
		/// and style); scattered nature where it lands goes (hidden, so Ctrl+Z brings it back). False and why, or true and
		/// what was done.
		/// </summary>
		public static bool ReRoll(MapType type, int seed, out string message)
		{
			if (!DynamicIslands.InEditor()) { message = "ReRollMapType works in the editor"; return false; }
			if (type.Content == null) { message = "'" + type.Name + "' has no content to put on an island" + (type.Build != null ? " (it makes the whole island: use the generator's Ready-made tab)" : ""); return false; }
			if (!PlaceableCatalog.IsBuilt) { message = "The objects are still loading - try again in a moment"; return false; }
			GameObject root = GameObject.Find("PlacedObjects");
			Terrain terrain = terraineditor.terrain;
			if (root == null || terrain == null) { message = "No island is open in the editor"; return false; }
			List<GameObject> objects = root.GetComponentsInChildren<EditorGameObject>(false).Select(e => e.gameObject).ToList();
			IslandFile file = DynamicIslands.CaptureIsland(DynamicIslands.currentIslandName);
			if (file.Objects.Count != objects.Count) { message = "The island's objects changed while reading them - try again"; return false; }
			var before = new List<IslandObject>(file.Objects);
			var propsBefore = new Dictionary<string, string>(file.Props);

			float elevation;
			IslandGenSettings s = MapTypes.Roll(type, new System.Random(seed), out elevation);
			// (the land that is there: its style and how far it reaches, so "top" and "dry" find it)
			s.Style = DynamicIslands.currentStyle;
			try { float land = IslandSpawner.LandRadius(file); if (land > 5f) s.Radius = land; } catch { }
			try { type.Content(new MapKit(file, s.Seed), s); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Re-rolling '" + type.Name + "': " + e); message = "Re-rolling failed - see the console (F10)"; return false; }
			if (type.Title.Length > 0 && !file.Props.ContainsKey(IslandProps.Title)) file.Props[IslandProps.Title] = type.Title;
			try { IslandReach.MoveContentWithinReach(file); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reach check of re-rolled content: " + e.Message); }

			var kept = new HashSet<IslandObject>(file.Objects);
			var had = new HashSet<IslandObject>(before);
			List<GameObject> gone = Enumerable.Range(0, before.Count).Where(i => !kept.Contains(before[i])).Select(i => objects[i]).ToList();
			List<IslandObject> added = file.Objects.Where(o => !had.Contains(o)).ToList();

			var group = new CommandGroup();
			if (DynamicIslands.EditorGizmoHandler != null) DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			foreach (GameObject go in gone) go.SetActive(false);
			if (gone.Count > 0) group.Add(new ObjectVisibilityCommand(gone, false));
			var made = new List<GameObject>();
			if (added.Count > 0)
			{
				var only = new IslandFile { Name = file.Name, WaterLevel = file.WaterLevel, TerrainSize = file.TerrainSize, HeightmapResolution = file.HeightmapResolution, Heights = file.Heights, Objects = added };
				var holder = new GameObject("ReRolledObjects");
				holder.transform.SetParent(root.transform, false);
				holder.transform.position = terrain.transform.position;
				IslandSpawner.SpawnObjects(only, holder.transform, true);
				made = holder.GetComponentsInChildren<EditorGameObject>(true).Select(e => e.gameObject).ToList();
				if (made.Count > 0) group.Add(new ObjectVisibilityCommand(made, true));
			}
			// (the quest and title it brings: the island's settings, in the same undo step)
			ICommand props = IslandSettingsUndo.Record(() =>
			{
				foreach (string key in propsBefore.Keys.Union(file.Props.Keys).ToList())
				{
					string a, b;
					propsBefore.TryGetValue(key, out a);
					if (file.Props.TryGetValue(key, out b) && b != a) DynamicIslands.currentIslandProps[key] = b;
					else if (b == null && a != null) DynamicIslands.currentIslandProps.Remove(key);
				}
			});
			if (props != null) group.Add(props);
			UndoRedoManager.Insert(group);
			EditorUI.RefreshIsland();
			message = "Put the content of a " + type.Label.ToLowerInvariant() + " (seed " + seed + ") on this island: " + made.Count + " object(s)" +
				(gone.Count > 0 ? ", " + gone.Count + " plant(s) out of their way" : "") + (props != null ? ", its quest/title" : "") +
				". The land is as it was. Ctrl+Z takes it off again; for other spots, undo and ReRollMapType " + type.Name + " <another seed>.";
			Debug.Log("[CUSTOM ISLANDS] " + message);
			return true;
		}

		#endregion

		#region Commands

		[ConsoleCommand(name: "ExportMapType", docs: "Writes a map type as a file to start your own from: Mods\\DynamicIslands\\maptypes\\<file name>.maptype (the help is at its top). Usage: ExportMapType <type> [file name, default my-<type>]")]
		public static void ExportCommand(string[] args)
		{
			if (args == null || args.Length == 0) { DynamicIslands.Notify("ExportMapType <type> [file name] - types: " + string.Join(", ", MapTypes.All.Select(t => t.Name).ToArray()), true, 8); return; }
			string error;
			string path = Export(args[0], args.Length > 1 ? args[1] : "", out error);
			if (path == null) DynamicIslands.Notify(error, true, 8);
			else DynamicIslands.Notify("Wrote " + path + " - change it in a text editor, then ReloadMapTypes (it is the map type '" + Path.GetFileNameWithoutExtension(path) + "')", false, 10);
		}

		[ConsoleCommand(name: "ReloadMapTypes", docs: "Reads the map type files (Mods\\DynamicIslands\\maptypes\\*.maptype) again and says which were left out and why")]
		public static void ReloadCommand(string[] args)
		{
			int n = LoadAll();
			DynamicIslands.Notify(n + " map type file(s) read" + (Skipped.Count > 0 ? "; left out: " + string.Join("; ", Skipped.ToArray()) : "") + (Unknown.Count > 0 ? "; unknown settings kept: " + string.Join("; ", Unknown.ToArray()) : ""), Skipped.Count > 0 || Unknown.Count > 0, 10);
		}

		[ConsoleCommand(name: "ReRollMapType", docs: "Editor: puts a map type's content (chests, notes, creatures, zones, quest - not its land) onto the island being edited, as one undo step. Usage: ReRollMapType <type> [seed]")]
		public static void ReRollCommand(string[] args)
		{
			if (args == null || args.Length == 0) { DynamicIslands.Notify("ReRollMapType <type> [seed] - types: " + string.Join(", ", MapTypes.All.Where(t => t.Content != null).Select(t => t.Name).ToArray()), true, 8); return; }
			MapType type = MapTypes.Get(args[0]);
			if (type == null) { DynamicIslands.Notify("There is no map type '" + args[0] + "'", true); return; }
			int seed;
			if (args.Length < 2 || !int.TryParse(args[1], out seed)) seed = new System.Random().Next(1, 999999);
			string message;
			bool ok = ReRoll(type, seed, out message);
			DynamicIslands.Notify(message, !ok, ok ? 10 : 6);
		}

		#endregion
	}
}
