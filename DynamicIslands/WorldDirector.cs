using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// One rule that brings an island into a world: what appears, when, where, and what players are told.
	/// Rules live in world plans (plans\&lt;name&gt;.plan) and in islands (their "bring.rules" setting, e.g. "when my
	/// quest is done, bring island X 600 m north of me"). One line each:
	///   id | what | when | where | message | receiver label
	///   what:  island:&lt;name&gt;   type:&lt;map type&gt;   pool   oneof:&lt;name&gt;, &lt;name&gt;...
	///   when:  start   km:&lt;n&gt;[:again]   day:&lt;n&gt;[:again]   quest:&lt;ref&gt;   step:&lt;ref&gt;:&lt;n&gt;   zone:&lt;ref&gt;:&lt;zone&gt;   visit:&lt;ref&gt;   rule:&lt;id&gt;
	///   where: ahead:&lt;m&gt;   near:&lt;ref&gt;:&lt;m&gt;:&lt;direction&gt;   receiver:&lt;m&gt;   sailing:&lt;m&gt;
	/// A ref names an island in the world: "self" (the island the rule belongs to), the id of the rule that brought
	/// it, or its island name.
	/// Plan rules may have two more parts (StoryChain): | story place | done when
	///   receiver: the island gets its own frequency on Raft's Receiver and comes when a player tunes to it;
	///   sailing: it comes up ahead by chance while sailing;
	///   story place: first, after:&lt;Raft story island or rule id&gt;, instead:&lt;Raft story island&gt; (its place in Raft's
	///   Receiver chain); done when: quest, visit, step:&lt;n&gt;, zone:&lt;zone&gt;, signal:&lt;signal&gt; (empty = its quest if it has
	///   one, else when players reach it) - then the next island of the chain is unlocked.
	/// </summary>
	public class IntroRule
	{
		public const string Self = "self";
		public static readonly string[] WhatKinds = { "island", "type", "pool", "oneof" };
		public static readonly string[] WhenKinds = { "start", "km", "day", "quest", "step", "zone", "visit", "rule", "signal" };
		public static readonly string[] WhereKinds = { "ahead", "near", "receiver", "sailing" };
		public static readonly string[] DoneKinds = { "", "quest", "visit", "step", "zone", "signal", "note" };
		/// <summary>Raft's notebook has ten tab colours (sprites NoteBook_Thumbnail_1..10); 0 = one chosen by the island's place.</summary>
		public const int TabColours = 10;
		public static readonly string[] Directions = { "any", "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

		public string Id = "";
		public string What = "island", WhatArg = "";
		public string When = "start", WhenRef = "", WhenArg = "";
		public string Where = "ahead", WhereRef = "";
		public float Distance = 300f;
		public string Direction = "any";
		/// <summary>SL9: a blueprint the players need before the island's Receiver dot shows and before it may come to
		/// the side ("" = none, Raft's way for its story islands). Until the host has it, the island comes straight ahead
		/// of the raft (players may have no engine yet). Written as a last WHERE part: "ahead:300:needs=engine".</summary>
		public string Needs = "";
		/// <summary>Short names for the blueprints a Needs part usually names.</summary>
		public static readonly string[][] NeedsAliases = { new[] { "engine", "Blueprint_MotorWheel" }, new[] { "steering", "Blueprint_SteeringWheel" }, new[] { "controls", "Blueprint_EngineControls" } };
		/// <summary>A Needs part as written ("engine", "Blueprint_SteeringWheel"...) to the blueprint's name; "" stays "".</summary>
		public static string NeedsBlueprint(string s)
		{
			s = (s ?? "").Trim();
			foreach (string[] a in NeedsAliases) if (s.Equals(a[0], StringComparison.OrdinalIgnoreCase)) return a[1];
			return s;
		}
		/// <summary>The short name for a Needs blueprint when it has one ("Blueprint_MotorWheel" -> "engine").</summary>
		public static string NeedsShort(string bp)
		{
			foreach (string[] a in NeedsAliases) if (string.Equals(bp, a[1], StringComparison.OrdinalIgnoreCase)) return a[0];
			return bp ?? "";
		}
		/// <summary>True when the rule needs no blueprint or this machine's player has it.</summary>
		public bool NeedsMet { get { return Needs.Length == 0 || BlueprintSteps.Has(Needs); } }
		public string Message = "", Label = "";
		/// <summary>Its place in Raft's Receiver chain (StoryChain): "", "first", "after:&lt;island or rule id&gt;", "instead:&lt;Raft story island&gt;".</summary>
		public string StoryPlace = "";
		/// <summary>When it counts as done in the chain: "" (its quest, or reaching it), quest, visit, step:n, zone:name, signal:name.</summary>
		public string StoryDone = "";
		/// <summary>Main story islands (in the chain) get a tab in Raft's notebook (QuestBook): its title ("" = the receiver
		/// label, else the island's name), its colour (1-10, 0 = by its place) and the intro on its first page ("" = an automatic line).</summary>
		public string TabTitle = "", TabIntro = "";
		public int TabColour;
		/// <summary>A km / day rule: its island comes back every WhenArg km / days while the players aren't done with it -
		/// never reached, or its quest not finished (ROADMAP SR1; written "day:5:again"; older versions read "day:5").</summary>
		public bool Again;
		/// <summary>Again, on a WHEN that has it (km or day).</summary>
		public bool Repeats { get { return Again && (When == "km" || When == "day"); } }

		/// <summary>Handled by StoryChain rather than brought straight away: in the story chain, on the Receiver, or by chance while sailing.</summary>
		public bool Special { get { return InStory || Where == "receiver" || Where == "sailing"; } }
		/// <summary>In Raft's Receiver chain (first / after / instead).</summary>
		public bool InStory { get { return StoryPlace.Length > 0 && !Beside; } }
		/// <summary>Main story beside Raft's story: in Raft's notebook, but not in the Receiver chain - its own WHEN brings it
		/// (an expedition that runs alongside Raft's story, each island when the one before is done).</summary>
		public bool Beside { get { return StoryPlace == "beside"; } }
		/// <summary>Main story = in the story chain, or beside it: its island goes into Raft's notebook; every other island is a side quest (the journal).</summary>
		public bool MainStory { get { return InStory || Beside; } }

		/// <summary>The tab's title in Raft's notebook.</summary>
		public string TabName { get { return TabTitle.Length > 0 ? TabTitle : Label.Length > 0 ? Label : What == "island" && WhatArg.Length > 0 ? WhatArg : Id; } }

		/// <summary>Text with line breaks in one part of a line: a break is written "\n", "|" becomes "/".</summary>
		public static string Multi(string s) { return (s ?? "").Replace("\r", "").Replace("|", "/").Replace("\n", "\\n").Trim(); }
		public static string UnMulti(string s) { return (s ?? "").Replace("\\n", "\n").Trim(); }

		public IntroRule Clone() { return (IntroRule)MemberwiseClone(); }

		/// <summary>Degrees clockwise from north (+z), or NaN for "any".</summary>
		public static float DirectionAngle(string direction)
		{
			int i = Array.FindIndex(Directions, d => d.Equals((direction ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
			if (i > 0) return (i - 1) * 45f;
			float deg;
			return float.TryParse(direction, NumberStyles.Float, CultureInfo.InvariantCulture, out deg) ? Mathf.Repeat(deg, 360f) : float.NaN;
		}

		/// <summary>"north-east" for an angle clockwise from north.</summary>
		public static string DirectionName(float degrees)
		{
			return Directions[1 + Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 45f) % 8];
		}

		static string Part(string s) { return (s ?? "").Replace("|", "/").Replace(":", "-").Replace("\n", " ").Replace("\r", "").Trim(); }
		static string Text(string s) { return (s ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "").Trim(); }
		static string Num(float f) { return f.ToString("0.#", CultureInfo.InvariantCulture); }

		/// <summary>A rule's name as rules are saved and the story keeps it: no ":" "," ";" "|" (they separate parts - a rule
		/// "camp,1" broke the story's list after a save).</summary>
		public static string CleanId(string s) { return (s ?? "").Replace("|", "-").Replace(":", "-").Replace(",", "-").Replace(";", "-").Replace("\n", " ").Replace("\r", "").Trim(); }

		public string ToLine()
		{
			string what = What == "pool" ? "pool" : What + ":" + (What == "oneof" ? Text(WhatArg) : Part(WhatArg));
			string when;
			switch (When)
			{
				case "start": when = "start"; break;
				case "km": case "day": when = When + ":" + Part(WhenArg) + (Again ? ":again" : ""); break;
				case "step": case "zone": case "signal": when = When + ":" + Part(WhenRef.Length > 0 ? WhenRef : Self) + ":" + Text(WhenArg); break;
				// ("quest:<island>:2" - the island's second quest, ROADMAP LM4; the main quest without a number, as before)
				case "quest": when = "quest:" + Part(WhenRef.Length > 0 ? WhenRef : Self) + (WhenArg.Trim().Length > 0 ? ":" + Text(WhenArg) : ""); break; // (a zone may be called "cave:1": the reading joins what comes after the island again)
				default: when = When + ":" + Part(WhenRef.Length > 0 ? WhenRef : (When == "rule" ? "" : Self)); break;
			}
			string where = Where == "near" ? "near:" + Part(WhereRef.Length > 0 ? WhereRef : Self) + ":" + Num(Distance) + ":" + Part(Direction) : Where + ":" + Num(Distance);
			if (Needs.Length > 0) where += ":needs=" + Part(NeedsShort(Needs));
			var parts = new List<string> { Part(Id), what, when, where, Text(Message), Text(Label) };
			bool tab = TabTitle.Length > 0 || TabColour > 0 || TabIntro.Length > 0;
			if (StoryPlace.Length > 0 || StoryDone.Length > 0 || tab) { parts.Add(Text(StoryPlace)); parts.Add(Text(StoryDone)); }
			if (tab) { parts.Add(Text(TabTitle)); parts.Add(TabColour > 0 ? TabColour.ToString(CultureInfo.InvariantCulture) : ""); parts.Add(Multi(TabIntro)); }
			return string.Join(" | ", parts.ToArray());
		}

		/// <summary>A rule from its line, or null if the line isn't one.</summary>
		public static IntroRule Parse(string line)
		{
			string[] p = (line ?? "").Split('|').Select(x => x.Trim()).ToArray();
			if (p.Length < 4) return null;
			var r = new IntroRule { Id = CleanId(p[0]), Message = p.Length > 4 ? p[4] : "", Label = p.Length > 5 ? p[5] : "",
				StoryPlace = p.Length > 6 ? NormalPlace(p[6]) : "", StoryDone = p.Length > 7 ? NormalDone(p[7]) : "",
				TabTitle = p.Length > 8 ? p[8] : "", TabIntro = p.Length > 10 ? UnMulti(p[10]) : "" };
			int colour;
			if (p.Length > 9 && int.TryParse(p[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out colour)) r.TabColour = Mathf.Clamp(colour, 0, TabColours);

			string[] what = p[1].Split(new[] { ':' }, 2);
			r.What = what[0].Trim().ToLowerInvariant();
			r.WhatArg = what.Length > 1 ? what[1].Trim() : "";
			if (!WhatKinds.Contains(r.What)) return null;

			string[] when = p[2].Split(':').Select(x => x.Trim()).ToArray();
			r.When = when[0].ToLowerInvariant();
			if (!WhenKinds.Contains(r.When)) return null;
			if (r.When == "km" || r.When == "day")
			{
				r.WhenArg = when.Length > 1 ? when[1] : "0";
				r.Again = when.Length > 2 && when[2].Equals("again", StringComparison.OrdinalIgnoreCase);
			}
			else if (r.When != "start")
			{
				r.WhenRef = when.Length > 1 ? when[1] : "";
				r.WhenArg = when.Length > 2 ? string.Join(":", when.Skip(2).ToArray()) : "";
				if (r.When == "rule") r.WhenRef = CleanId(r.WhenRef); // (a rule's name, as the rule's own)
			}

			string[] where = p[3].Split(':').Select(x => x.Trim()).ToArray();
			string needs = where.FirstOrDefault(x => x.StartsWith("needs=", StringComparison.OrdinalIgnoreCase));
			if (needs != null) { r.Needs = NeedsBlueprint(needs.Substring(6)); where = where.Where(x => x != needs).ToArray(); }
			r.Where = where[0].ToLowerInvariant();
			if (!WhereKinds.Contains(r.Where)) return null;
			float d;
			if (r.Where != "near") r.Distance = where.Length > 1 && float.TryParse(where[1], NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : r.Where == "receiver" ? 600f : 300f;
			else
			{
				r.WhereRef = where.Length > 1 ? where[1] : Self;
				r.Distance = where.Length > 2 && float.TryParse(where[2], NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 600f;
				r.Direction = where.Length > 3 && where[3].Length > 0 ? where[3].ToLowerInvariant() : "any";
			}
			r.Distance = Mathf.Clamp(r.Distance, 50f, 5000f);
			return r;
		}

		/// <summary>"first", "after:X" or "instead:X" (a Raft island by any of its names: "Caravan Town", "caravanisland"...), else "".</summary>
		public static string NormalPlace(string s)
		{
			s = (s ?? "").Trim();
			if (s.Equals("first", StringComparison.OrdinalIgnoreCase)) return "first";
			if (s.Equals("beside", StringComparison.OrdinalIgnoreCase)) return "beside";
			int c = s.IndexOf(':');
			if (c <= 0) return "";
			string kind = s.Substring(0, c).Trim().ToLowerInvariant(), what = s.Substring(c + 1).Trim();
			if ((kind != "after" && kind != "instead") || what.Length == 0) return "";
			ChunkPointType t = StoryOrder.Parse(what);
			if (t != ChunkPointType.None) what = StoryOrder.Key(t);
			else if (kind == "instead") return "";
			return kind + ":" + what;
		}

		public static string NormalDone(string s)
		{
			s = (s ?? "").Trim();
			string kind = s.Split(':')[0].Trim().ToLowerInvariant();
			if (!DoneKinds.Contains(kind) || kind.Length == 0) return "";
			return kind == "quest" || kind == "visit" ? kind : kind + ":" + (s.Contains(":") ? s.Substring(s.IndexOf(':') + 1).Trim() : "");
		}

		public static List<IntroRule> ParseLines(string text)
		{
			return (text ?? "").Split('\n').Select(Parse).Where(r => r != null).ToList();
		}

		public static string ToLines(IEnumerable<IntroRule> rules)
		{
			return string.Join("\n", rules.Select(r => r.ToLine()).ToArray());
		}

		static string RefName(string r) { return r.Length == 0 || r == Self ? "this island" : "'" + r + "'"; }

		public string DescribeWhat()
		{
			switch (What)
			{
				case "type": MapType t = MapTypes.Get(WhatArg); return "a new " + (t != null ? t.Label.ToLowerInvariant() : "'" + WhatArg + "' island");
				case "pool": return "an island from the spawn pool";
				case "oneof": return "one of " + WhatArg;
				default: return "island '" + WhatArg + "'";
			}
		}

		public string DescribeWhen()
		{
			switch (When)
			{
				case "start": return "When the world starts";
				case "km": return "After sailing " + WhenArg + " km" + (Again ? " (and again every " + WhenArg + " km until players are done with it)" : "");
				case "day": return "On day " + WhenArg + (Again ? " (and again every " + WhenArg + " days until players are done with it)" : "");
				case "quest": return "When " + (WhenArg.Trim().Length > 0 && WhenArg.Trim() != "1" ? "quest " + WhenArg.Trim() : "the quest") + " of " + RefName(WhenRef) + " is done";
				case "step": return "When " + WhenArg + " step(s) of the quest of " + RefName(WhenRef) + " are done";
				case "zone": return "When zone '" + WhenArg + "' of " + RefName(WhenRef) + " fires";
				case "visit": return "When players first reach " + RefName(WhenRef);
				case "rule": return "After rule '" + WhenRef + "'";
				case "signal": return "When the signal '" + WhenArg + "' is sent on " + RefName(WhenRef);
			}
			return When;
		}

		public string DescribeWhere()
		{
			string w = DescribePlace();
			return Needs.Length == 0 ? w : w + "; its Receiver dot and any side placing wait for the " + PlanChecker.Pretty(Needs) + " blueprint (straight ahead until then)";
		}

		string DescribePlace()
		{
			if (Where == "ahead") return Num(Distance) + " m ahead of the raft";
			if (Where == "receiver") return "on its own Receiver frequency (it comes " + Num(Distance) + " m ahead when a player tunes to it)";
			if (Where == "sailing") return "by chance while sailing (ahead of the raft)";
			float a = DirectionAngle(Direction);
			return Num(Distance) + " m " + (float.IsNaN(a) ? "from " : DirectionName(a) + " of ") + RefName(WhereRef);
		}

		public string DescribeStory()
		{
			if (StoryPlace.Length == 0) return "";
			if (Beside) return "main story beside Raft's story (its WHEN brings it)";
			string place = StoryPlace == "first" ? "first in the story" : StoryPlace.StartsWith("instead:") ? "in place of " + StoryOrder.NameOfKey(StoryPlace.Substring(8)) :
				"after " + StoryOrder.NameOfKey(StoryPlace.Substring(6));
			return place + "; done when " + DescribeDone();
		}

		public string DescribeDone()
		{
			string k = StoryDone.Split(':')[0], arg = StoryDone.Contains(":") ? StoryDone.Substring(StoryDone.IndexOf(':') + 1) : "";
			switch (k)
			{
				case "quest": return "its quest is done";
				case "visit": return "players reach it";
				case "step": return arg + " step(s) of its quest are done";
				case "zone": return "its zone '" + arg + "' fires";
				case "signal": return "its signal '" + arg + "' is sent";
				case "note": return "its note #" + arg + " is read";
			}
			return "its quest is done (or players reach it, if it has none)";
		}

		public string Describe()
		{
			string story = DescribeStory();
			return (InStory ? "Story: " + story + ". " + (When == "start" ? "Once unlocked" : DescribeWhen() + ", once unlocked") : DescribeWhen()) + ": bring " + DescribeWhat() + ", " + DescribeWhere();
		}
	}

	/// <summary>
	/// A world plan: which islands a world gets, when and where (its rules), and whether random islands keep
	/// appearing while sailing as well. Chosen when a world is created (Raft's New Game box) or with WorldPlan.
	/// Stored as Mods\DynamicIslands\plans\&lt;name&gt;.plan (text). "Random islands" and "No custom islands" are built in.
	/// </summary>
	public class WorldPlan
	{
		public const string RandomName = "Random islands", NoneName = "No custom islands", Extension = ".plan";

		public string Name = "", Description = "";
		/// <summary>Random islands from the spawn pool appear while sailing (today's behaviour), as well as the rules.</summary>
		public bool Random = true;
		public List<IntroRule> Rules = new List<IntroRule>();
		/// <summary>Raft's story islands come (Radio Tower ... Utopia, on the Receiver), as in any Raft world. Off: only the plan's own.</summary>
		public bool RaftStory = true;
		/// <summary>Raft's story islands left out of this plan's story (StoryOrder.Key names: "Balboa"...).</summary>
		public HashSet<string> LeaveOut = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>The last page of the main story in Raft's notebook, shown (with a banner) when the last main story island is done ("" = none).</summary>
		public string StoryEnding = "";
		/// <summary>The credits after the last main story step (RF2): their title ("" = the plan's name) and the makers' names (one per line).</summary>
		public string EndingTitle = "", Credits = "";
		/// <summary>Lines this version can't read (a newer one's settings or rules): written back as they are (AU5).</summary>
		public List<string> Kept = new List<string>();
		/// <summary>The mod version that saved the plan ("" before 2026-10-06).</summary>
		public string ModVersion = "";
		/// <summary>Which plan this is, whatever its name (UW2): made with the plan (New, Copy... make their own), kept by edits.
		/// A world keeps it ("@planid="): a different plan given the old name later - one made after the first was copied
		/// away and deleted - isn't taken for an edit of it. "" in plans from before 2026-10-10 and in library plans (those
		/// go by their entry's source).</summary>
		public string Id = "";
		public static string NewId() { return Guid.NewGuid().ToString("N").Substring(0, 12); }
		static readonly HashSet<string> toldNewer = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>The plan changes Raft's story chain (StoryChain takes it over in its worlds).</summary>
		public bool ChangesStory { get { return !RaftStory || LeaveOut.Count > 0 || Rules.Any(r => r.InStory); } }
		/// <summary>The plan has rules StoryChain handles (the story chain, Receiver frequencies, islands by chance).</summary>
		public bool HasStory { get { return ChangesStory || Rules.Any(r => r.Special || r.Beside); } }

		public bool BuiltIn { get { return IsBuiltIn(Name); } }
		public static bool IsBuiltIn(string name) { return name.Equals(RandomName, StringComparison.OrdinalIgnoreCase) || name.Equals(NoneName, StringComparison.OrdinalIgnoreCase); }

		public static string Folder { get { return Path.Combine(DynamicIslands.assetpath, "plans"); } }
		public static string PathFor(string name) { return Path.Combine(Folder, name + Extension); }

		/// <summary>The built-in plans first, then the saved ones.</summary>
		public static List<string> All()
		{
			var names = new List<string> { RandomName, NoneName };
			try
			{
				if (Directory.Exists(Folder))
					names.AddRange(Directory.GetFiles(Folder, "*" + Extension).Select(Path.GetFileNameWithoutExtension).Where(n => !IsBuiltIn(n)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not list world plans: " + e.Message); }
			return names;
		}

		/// <summary>A plan by name, or null if there's no such plan.</summary>
		public static WorldPlan Load(string name)
		{
			name = (name ?? "").Trim();
			if (name.Equals(RandomName, StringComparison.OrdinalIgnoreCase)) return new WorldPlan { Name = RandomName, Description = "Islands appear by chance while you sail", Random = true };
			if (name.Equals(NoneName, StringComparison.OrdinalIgnoreCase)) return new WorldPlan { Name = NoneName, Description = "Only islands you spawn yourself", Random = false };
			string path = PathFor(name);
			if (name.Length == 0 || !File.Exists(path)) return null;
			try { return Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read world plan '" + name + "': " + e.Message); return null; }
		}

		public static WorldPlan Parse(string name, string text)
		{
			var plan = new WorldPlan { Name = name, Random = false };
			foreach (string raw in (text ?? "").Split('\n'))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;
				int eq = line.IndexOf('=');
				if (eq <= 0) continue;
				string key = line.Substring(0, eq).Trim().ToLowerInvariant(), value = line.Substring(eq + 1).Trim();
				switch (key)
				{
					case "description": plan.Description = value; break;
					case "random": plan.Random = value.Equals("on", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase); break;
					case "story": plan.RaftStory = !(value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase)); break;
					case "storyleaveout":
						foreach (string s in value.Split(','))
						{
							ChunkPointType t = StoryOrder.Parse(s);
							if (t != ChunkPointType.None) plan.LeaveOut.Add(StoryOrder.Key(t));
						}
						break;
					case "storyending": plan.StoryEnding = IntroRule.UnMulti(value); break;
					case "endingtitle": plan.EndingTitle = IntroRule.UnMulti(value); break;
					case "credits": plan.Credits = IntroRule.UnMulti(value); break;
					case "modversion": plan.ModVersion = value; break;
					case "id": plan.Id = value; break;
					case "rule":
						IntroRule r = IntroRule.Parse(value);
						if (r != null) plan.Rules.Add(r);
						else { plan.Kept.Add(line); Debug.LogWarning("[CUSTOM ISLANDS] World plan '" + name + "': a rule this version can't read is kept as it is: " + value); }
						break;
					default: plan.Kept.Add(line); break;
				}
			}
			if (plan.ModVersion.Length > 0 && LibraryPack.CompareVersions(plan.ModVersion, LibraryPack.ModVersion) > 0 && toldNewer.Add(name))
			{
				Debug.LogWarning("[CUSTOM ISLANDS] World plan '" + name + "' was saved by Custom Islands " + plan.ModVersion + ", newer than this " + LibraryPack.ModVersion);
				if (plan.Kept.Count > 0) DynamicIslands.Notify("The plan '" + name + "' was made with a newer Custom Islands (" + plan.ModVersion + "): " + plan.Kept.Count + " line(s) this version can't use are kept as they are. Please update the mod.", true);
			}
			// Rules need distinct ids (other rules and the world's state refer to them)
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < plan.Rules.Count; i++)
			{
				if (plan.Rules[i].Id.Length == 0) plan.Rules[i].Id = "rule" + (i + 1);
				while (!seen.Add(plan.Rules[i].Id)) plan.Rules[i].Id += "b";
			}
			return plan;
		}

		public const string Help =
@"# Custom Islands world plan: which islands a world gets, when and where.
# Choose it when creating a world (Raft's New Game box), or in a world with: WorldPlan <name>
#
# description = shown when choosing the plan
# random = on|off   (islands from spawnpool.txt also appear by chance while sailing)
# rule = id | what | when | where | message | receiver label
#   what:  island:<saved island>   type:<map type>   pool   oneof:<island>, <island>, ...
#   when:  start   km:<km sailed>   day:<day>   quest:<ref>   step:<ref>:<steps done>
#          zone:<ref>:<zone name>   signal:<ref>:<signal name>   visit:<ref>   rule:<rule id>
#          km:<n>:again   day:<n>:again   (again until done: back every <n> km / days until reached and its quest done)
#   where: ahead:<metres>   near:<ref>:<metres>:<direction>   (any, north, north-east, east, ... or degrees)
#          receiver:<metres>   (its own frequency on Raft's Receiver: it comes when a player tunes to it)
#          sailing:<metres>    (it comes up ahead by chance while sailing)
#   <ref> is an island in the world: the id of the rule that brought it, or its island name.
#   The message is shown to every player when the island appears; the label is its name on the Receiver.
#   Two more parts put the island into Raft's story (the Receiver chain):
#   rule = ... | label | first / after:<story island or rule id> / instead:<story island> / beside | done when
#          beside = main story beside Raft's story: in Raft's notebook, brought by its own WHEN (not the Receiver chain)
#          done when: quest, visit, step:<n>, zone:<zone>, signal:<signal>, note:<note number> (empty: its quest,
#          or reaching it) - then the next island's coordinates are found
#   An island in the story is MAIN STORY: it gets a tab in Raft's notebook (its quest steps, intro and notes there);
#   every other island is a side quest (the journal). Three more parts style the tab:
#   rule = ... | place | done when | tab title | tab colour 1-10 (not 3) | tab intro (\n = a new line)
# storyending = the last page of the main story in Raft's notebook (\n = a new line); also the credits' closing note
# endingtitle = the credits' title, shown once to every player after the last main story step (empty = the plan's name)
# credits = the makers' names on the credits (\n = a new line)
# story = on|off        (Raft's story islands: Radio Tower, Vasagatan, Balboa, Caravan Town, Tangaroa,
#                         Varuna Point, Temperance, Utopia. off = only the plan's own islands: a new adventure)
# storyleaveout = Balboa, Tangaroa   (story islands left out: the note before them leads to the one after)
";

		public string ToText()
		{
			var lines = new List<string> { Help, "description = " + (Description ?? "").Replace("\n", " "), "random = " + (Random ? "on" : "off"), "story = " + (RaftStory ? "on" : "off") };
			if (LeaveOut.Count > 0) lines.Add("storyleaveout = " + string.Join(", ", StoryOrder.Chain.Select(StoryOrder.Key).Where(k => LeaveOut.Contains(k)).ToArray()));
			if ((StoryEnding ?? "").Trim().Length > 0) lines.Add("storyending = " + IntroRule.Multi(StoryEnding));
			if ((EndingTitle ?? "").Trim().Length > 0) lines.Add("endingtitle = " + IntroRule.Multi(EndingTitle));
			if ((Credits ?? "").Trim().Length > 0) lines.Add("credits = " + IntroRule.Multi(Credits));
			lines.Add("");
			lines.AddRange(Rules.Select(r => "rule = " + r.ToLine()));
			lines.AddRange(Kept);
			if ((Id ?? "").Length > 0) lines.Add("id = " + Id);
			lines.Add("modversion = " + LibraryPack.ModVersion);
			return string.Join("\r\n", lines.ToArray()) + "\r\n";
		}

		public void Save()
		{
			// (the player's own plans get an id at their first save; library plans keep their file as the entry has it)
			if ((Id ?? "").Length == 0 && !BuiltIn && LibrarySource.OfPlan(Name) == null) Id = NewId();
			Directory.CreateDirectory(Folder);
			SafeFile.WriteAllText(PathFor(Name), ToText());
		}
	}

	/// <summary>
	/// Settings of saved island files, read without spawning them (for islands that are far away and unloaded):
	/// their island settings (quests, rules) and zone names. Cached by file time.
	/// </summary>
	public static class IslandCache
	{
		class Info
		{
			public DateTime Time;
			public Dictionary<string, string> Props;
			public List<string> Zones;
			/// <summary>Signals its objects and island events send ("send a signal" actions).</summary>
			public List<string> Signals;
			/// <summary>Its notes with a text (their object numbers): each gives a journal page when read.</summary>
			public List<int> Notes;
			/// <summary>Those notes' titles and texts (the quest book's preview shows notes not read yet).</summary>
			public Dictionary<int, KeyValuePair<string, string>> NoteTexts;
			/// <summary>When the file's time was last looked at (ROADMAP P3: behaviours asked every 0.5 s, each a disk check).</summary>
			public float CheckedAt;
			/// <summary>The journal pages its events write ("object number:title", as the page keys "act:&lt;island&gt;:..." end).</summary>
			public List<string> EventPages;
			/// <summary>Those pages' texts ("object number:title" -> text).</summary>
			public Dictionary<string, string> EventPageTexts;
		}

		static readonly Dictionary<string, Info> cache = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, DateTime> broken = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
		/// <summary>Islands whose file was missing or broken when last looked at, and when (looked at again after 2 s, as cached ones).</summary>
		static readonly Dictionary<string, float> none = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

		static Info Get(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;
			string path = IslandSpawner.PathFor(name);
			try
			{
				Info info;
				// (looked at again on disk at most every 2 s: a file saved meanwhile is read within that)
				if (cache.TryGetValue(name, out info) && Time.unscaledTime - info.CheckedAt < 2f && Time.unscaledTime >= info.CheckedAt) return info;
				// (a file that is missing or broken is looked for again at most every 2 s too - AU41: the director, the
				// behaviours and the quest book ask for unloaded islands every tick, and only a file that was there was cached)
				float noneAt;
				if (none.TryGetValue(name, out noneAt) && Time.unscaledTime - noneAt < 2f && Time.unscaledTime >= noneAt) return null;
				none.Remove(name);
				if (!File.Exists(path)) { none[name] = Time.unscaledTime; return null; }
				DateTime t = File.GetLastWriteTimeUtc(path);
				if (info != null && info.Time == t) { info.CheckedAt = Time.unscaledTime; return info; }
				// (a broken file is read again only once it changed - AU37: it was read and logged on every director tick)
				DateTime brokenAt;
				if (broken.TryGetValue(name, out brokenAt) && brokenAt == t) { none[name] = Time.unscaledTime; return null; }
				IslandFile f;
				try { f = IslandFile.Load(path); }
				catch (Exception e) { broken[name] = t; none[name] = Time.unscaledTime; Debug.LogWarning("[CUSTOM ISLANDS] Could not read island '" + name + "' (not read again until it changes): " + e.Message); return null; }
				info = new Info { Time = t, Props = f.Props, Zones = f.Objects.Where(o => o.Name == ContentCatalog.TriggerZone).Select(o => ObjectProps.Get(o.Props, ObjectProps.ZoneId)).ToList() };
				info.Signals = f.Objects.Select(o => o.Props).Concat(new[] { f.Props }).Where(p => p != null)
					.SelectMany(p => p.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix)))
					.SelectMany(kv => ObjAction.ParseLines(kv.Value)).Where(a => a.Verb == "signal" && a.Arg.Trim().Length > 0)
					.Select(a => a.Arg.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				info.Notes = Enumerable.Range(0, f.Objects.Count).Where(i => ObjectProps.IsNote(f.Objects[i].Name, f.Objects[i].Props) && ObjectProps.Get(f.Objects[i].Props, ObjectProps.NoteText).Trim().Length > 0).ToList();
				info.NoteTexts = info.Notes.ToDictionary(i => i, i => new KeyValuePair<string, string>(ObjectProps.Get(f.Objects[i].Props, ObjectProps.NoteTitle).Trim(), ObjectProps.Get(f.Objects[i].Props, ObjectProps.NoteText).Trim()));
				Func<int, IDictionary<string, string>, IEnumerable<string>> journal = (i, p) => p == null ? Enumerable.Empty<string>() : p
					.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix))
					.SelectMany(kv => ObjAction.ParseLines(kv.Value)).Where(a => a.Verb == "journal" && a.Target.Length > 0).Select(a => i + ":" + a.Target);
				info.EventPages = Enumerable.Range(0, f.Objects.Count).SelectMany(i => journal(i, f.Objects[i].Props)).Concat(journal(Behaviours.IslandIndex, f.Props)).Distinct().ToList();
				// (and what those pages say: the quest book's preview shows them)
				Func<int, IDictionary<string, string>, IEnumerable<KeyValuePair<string, string>>> journalText = (i, p) => p == null ? Enumerable.Empty<KeyValuePair<string, string>>() : p
					.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix))
					.SelectMany(kv => ObjAction.ParseLines(kv.Value)).Where(a => a.Verb == "journal" && a.Target.Length > 0)
					.Select(a => new KeyValuePair<string, string>(i + ":" + a.Target, (a.Arg ?? "").Replace("\\n", "\n")));
				info.EventPageTexts = new Dictionary<string, string>();
				foreach (KeyValuePair<string, string> kv in Enumerable.Range(0, f.Objects.Count).SelectMany(i => journalText(i, f.Objects[i].Props)).Concat(journalText(Behaviours.IslandIndex, f.Props)))
					if (!info.EventPageTexts.ContainsKey(kv.Key)) info.EventPageTexts[kv.Key] = kv.Value;
				info.CheckedAt = Time.unscaledTime;
				cache[name] = info;
				return info;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read island '" + name + "': " + e.Message); return null; }
		}

		/// <summary>Forgets what was read (files were installed or removed).</summary>
		public static void Forget() { cache.Clear(); none.Clear(); notBeforeAt.Clear(); rulesAt.Clear(); IslandSummaries.Recheck(); }

		#region Rules, kept on disk (ROADMAP P6)

		// name -> (file time, its rules' text): reading every island's rules for Tidy up, Delete and Rename loaded each whole
		// file (heights, objects) - 8 s for 1286 islands. Kept in Mods\DynamicIslands\rulescache.txt between sessions.
		static Dictionary<string, KeyValuePair<long, string>> rules;
		static readonly Dictionary<string, float> rulesAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		static bool rulesDirty;
		static string RulesPath { get { return Path.Combine(DynamicIslands.assetpath, "rulescache.txt"); } }

		/// <summary>An island's own rules (as in its settings), read from the file only when it changed since last time.</summary>
		public static List<IntroRule> RulesOf(string name) { return RulesOf(name, 0); }

		/// <summary>The same, the file's time (UTC ticks) already known from a folder listing (Tidy up: 0 = look it up).</summary>
		public static List<IntroRule> RulesOf(string name, long fileTicks)
		{
			if (rules == null) LoadRules();
			string path = IslandSpawner.PathFor(name);
			try
			{
				KeyValuePair<long, string> had;
				bool known = rules.TryGetValue(name, out had);
				float at;
				// (the file's time looked at again at most every 2 s, as Get does: the director asks it of every island each tick)
				if (known && rulesAt.TryGetValue(name, out at) && Time.unscaledTime - at < 2f && Time.unscaledTime >= at)
					return WorldDirector.RulesFromProps(new Dictionary<string, string> { { WorldDirector.IslandRulesKey, had.Value } });
				if (fileTicks <= 0 && !File.Exists(path)) return new List<IntroRule>();
				long t = fileTicks > 0 ? fileTicks : File.GetLastWriteTimeUtc(path).Ticks;
				rulesAt[name] = Time.unscaledTime;
				string text;
				if (known && had.Key == t) text = had.Value;
				else
				{
					// (a file that couldn't be read - locked, broken - isn't kept as "no rules" until it changes - review 2026-10-06)
					// (only the settings read, not the whole island: 1,000 new islands took Tidy up 5-10 s)
					Dictionary<string, string> props;
					try { props = IslandFile.LoadProps(path); }
					catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read island '" + name + "': " + e.Message); return new List<IntroRule>(); }
					text = ObjectProps.Get(props, WorldDirector.IslandRulesKey);
					rules[name] = new KeyValuePair<long, string>(t, text);
					rulesDirty = true;
				}
				return WorldDirector.RulesFromProps(new Dictionary<string, string> { { WorldDirector.IslandRulesKey, text } });
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Rules of '" + name + "': " + e.Message); return new List<IntroRule>(); }
		}

		static void LoadRules() { rules = LoadKept(RulesPath); }

		/// <summary>A cache kept on disk: one line per island, "name TAB file time TAB the value in base64".</summary>
		static Dictionary<string, KeyValuePair<long, string>> LoadKept(string path)
		{
			var kept = new Dictionary<string, KeyValuePair<long, string>>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (!File.Exists(path)) return kept;
				foreach (string line in File.ReadAllLines(path))
				{
					string[] p = line.Split('\t');
					long t;
					if (p.Length != 3 || !long.TryParse(p[1], out t)) continue;
					kept[p[0]] = new KeyValuePair<long, string>(t, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[2])));
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reading " + Path.GetFileName(path) + ": " + e.Message); }
			return kept;
		}

		static void SaveKept(string path, Dictionary<string, KeyValuePair<long, string>> kept)
		{
			SafeFile.WriteAllLines(path, kept.Select(kv => kv.Key + "\t" + kv.Value.Key + "\t" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(kv.Value.Value ?? ""))).ToArray());
		}

		/// <summary>Writes what RulesOf read since (after a look through every island).</summary>
		public static void SaveRules()
		{
			if (rules == null || !rulesDirty) return;
			try
			{
				SaveKept(RulesPath, rules);
				rulesDirty = false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Writing rulescache.txt: " + e.Message); }
		}

		#endregion

		#region "Not before", kept on disk (IM43)

		// name -> (file time, its "not before" km): the spawn pool asks it of every island in the pool at each pick, and the
		// first pick of a session read each of those whole files (a fresh host with 130 islands in the pool: 0.5-0.7 s a pick
		// on average over 20, IM43). Kept in Mods\DynamicIslands\notbeforecache.txt between sessions.
		static Dictionary<string, KeyValuePair<long, string>> notBefore;
		static readonly Dictionary<string, float> notBeforeAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		static bool notBeforeDirty;
		static string NotBeforePath { get { return Path.Combine(DynamicIslands.assetpath, "notbeforecache.txt"); } }

		/// <summary>An island's "not before" setting (IslandProps.NotBeforeKm; "" when it has none or the file is missing),
		/// read from the file only when it changed since last time.</summary>
		public static string NotBeforeKm(string name)
		{
			if (string.IsNullOrEmpty(name)) return "";
			if (notBefore == null) notBefore = LoadKept(NotBeforePath);
			KeyValuePair<long, string> had;
			bool known = notBefore.TryGetValue(name, out had);
			float at;
			// (the file's time looked at again at most every 2 s, as Get does)
			if (known && notBeforeAt.TryGetValue(name, out at) && Time.unscaledTime - at < 2f && Time.unscaledTime >= at) return had.Value;
			try
			{
				string path = IslandSpawner.PathFor(name);
				if (!File.Exists(path)) return "";
				long t = File.GetLastWriteTimeUtc(path).Ticks;
				notBeforeAt[name] = Time.unscaledTime;
				if (known && had.Key == t) return had.Value;
				Info read = Get(name);
				if (read == null) return "";
				string v = ObjectProps.Get(read.Props, IslandProps.NotBeforeKm);
				notBefore[name] = new KeyValuePair<long, string>(t, v);
				notBeforeDirty = true;
				return v;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] 'Not before' of '" + name + "': " + e.Message); return ""; }
		}

		/// <summary>Writes what NotBeforeKm read since (after a pick from the pool).</summary>
		public static void SaveNotBefore()
		{
			if (notBefore == null || !notBeforeDirty) return;
			try { SaveKept(NotBeforePath, notBefore); notBeforeDirty = false; }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Writing notbeforecache.txt: " + e.Message); }
		}

		#endregion

		/// <summary>Forgets one island file (it was just saved: read it again next time, not after the 2 s wait).</summary>
		public static void ForgetFile(string path)
		{
			try { string name = System.IO.Path.GetFileNameWithoutExtension(path); cache.Remove(name); none.Remove(name); notBeforeAt.Remove(name); rulesAt.Remove(name); IslandSummaries.Forget(name); } catch { }
		}

		/// <summary>The island's own settings (empty if the file is missing).</summary>
		public static Dictionary<string, string> Props(string name)
		{
			Info i = Get(name);
			return i != null ? i.Props : new Dictionary<string, string>();
		}

		/// <summary>Settings of a world's island: from the spawned island if it's loaded, else from its file.</summary>
		public static Dictionary<string, string> PropsOf(IslandWorldState.Entry e)
		{
			if (e == null) return new Dictionary<string, string>();
			IslandSettings s = e.Root != null ? e.Root.GetComponent<IslandSettings>() : null;
			return s != null ? s.Props : Props(e.Name);
		}

		/// <summary>The names of the island's trigger zones (empty if the file is missing): the plan editor's lists.</summary>
		public static List<string> ZonesOf(string name) { Info i = Get(name); return i != null ? i.Zones.Where(z => !string.IsNullOrEmpty(z)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>(); }

		/// <summary>The signals the island's objects and events send (empty if none or the file is missing).</summary>
		public static List<string> SignalsOf(string name) { Info i = Get(name); return i != null ? i.Signals : new List<string>(); }

		/// <summary>The island's notes with a text, by object number (the journal's "note:&lt;island&gt;:&lt;n&gt;" pages; empty if the file is missing).</summary>
		public static List<int> NotesOf(string name) { Info i = Get(name); return i != null && i.Notes != null ? i.Notes : new List<int>(); }

		/// <summary>The journal pages the island's events write, with their texts ("object number:title" -> text; empty if the file is missing).</summary>
		public static Dictionary<string, string> EventPageTextsOf(string name) { Info i = Get(name); return i != null && i.EventPageTexts != null ? i.EventPageTexts : new Dictionary<string, string>(); }

		/// <summary>The island's notes' titles and texts by object number (empty if the file is missing).</summary>
		public static Dictionary<int, KeyValuePair<string, string>> NoteTextsOf(string name) { Info i = Get(name); return i != null && i.NoteTexts != null ? i.NoteTexts : new Dictionary<int, KeyValuePair<string, string>>(); }

		/// <summary>The quest of a saved island (an empty one if the file is missing).</summary>
		public static IslandQuest QuestOfFile(string name) { return IslandQuest.From(Props(name)); }

		/// <summary>The journal pages the island's events write ("object number:title"; empty if the file is missing).</summary>
		public static List<string> EventPagesOf(string name) { Info i = Get(name); return i != null && i.EventPages != null ? i.EventPages : new List<string>(); }

		/// <summary>The island's quest (none if the file is missing).</summary>
		public static IslandQuest QuestOf(string name) { return IslandQuest.From(Props(name)); }

		/// <summary>Places of the trigger zones with this name among the island's zones (empty if none): two zones may share a name.</summary>
		public static List<int> ZoneOrdinals(string name, string zoneId)
		{
			Info i = Get(name);
			var found = new List<int>();
			if (i != null) for (int k = 0; k < i.Zones.Count; k++) if ((i.Zones[k] ?? "").Equals((zoneId ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) found.Add(k);
			return found;
		}
	}

	/// <summary>
	/// The world director (host only): brings islands into the world by rules - the world's plan and the rules of the
	/// islands already in it - when their moment comes (world start, km sailed, a day, a quest or quest step done, a
	/// zone fired, an island first visited, another rule done). Conditions are checked against the world's state
	/// every second, so events from clients (quests, zones) count as soon as they reach the host, and nothing is
	/// missed across saves. A rule that fired is remembered (plan rules in the world file, island rules in the
	/// island's state) so it never brings its island twice. New islands go through the normal paths: clients get
	/// them (and their files) like any other island, and every player is told where the new island is.
	/// </summary>
	public static class WorldDirector
	{
		/// <summary>Island setting holding the island's own rules (lines, see IntroRule).</summary>
		public const string IslandRulesKey = "bring.rules";
		/// <summary>State keys: a player has been at the island; island rule i has fired (RuleKeyBase + i).</summary>
		public const int VisitKey = 0x50000, RuleKeyBase = 0x50001;
		const float TickInterval = 1f, StartDelay = 4f, RetrySeconds = 20f, VisitMargin = 25f;

		/// <summary>The current world's plan.</summary>
		public static string PlanName = WorldPlan.RandomName;
		public static WorldPlan Plan = WorldPlan.Load(WorldPlan.RandomName);
		/// <summary>
		/// Where the world's plan came from: "library:&lt;id&gt;@&lt;version&gt;", "import:&lt;id&gt;@&lt;version&gt;", or "" (the
		/// player's own plan). Saved with the world ("@planfrom="), so a host missing one of its islands can be told where to get it.
		/// </summary>
		public static string PlanFrom = "";
		/// <summary>The id of the plan the world was made with (WorldPlan.Id; "" for worlds and plans from before it).</summary>
		public static string PlanId = "";
		/// <summary>True when the plan was read from the world's own copy (not from the plans folder).</summary>
		public static bool PlanFromWorld { get; private set; }
		/// <summary>
		/// Steam id of the player who made the world ("@planowner=", 0 = not known). Their own plan's file is theirs to
		/// change: on their PC an edited plan plays in the world. Another player hosting the world later may have a
		/// different plan with the same name, so there the world's own copy plays.
		/// </summary>
		public static ulong PlanOwner;
		/// <summary>
		/// The content hash of each island the world's plan brings, by island name ("@planhash=&lt;island&gt;:&lt;hash&gt;", AU6):
		/// recorded when the world takes the plan (from the files of the PC that picked it), when a world from before is loaded
		/// (from the islands it already has, or on the plan owner's PC its files), and when a rule first brings an island.
		/// Another host then brings that very island - not their own, different island of the same name - and finds it under
		/// another name too (a pack's island renamed on import). An older version keeps the lines as they are.
		/// </summary>
		public static readonly Dictionary<string, string> PlanHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>True when the last load found the plan file changed since the world was saved (and plays the file).</summary>
		public static bool PlanWasEdited { get; private set; }
		/// <summary>Steam id of the PC that saved the world last ("@savedby=", 0 = not known): the plan's owner when the world names none.</summary>
		static ulong savedBy;

		static ulong LocalSteamId { get { try { return Steamworks.SteamUser.GetSteamID().m_SteamID; } catch { return 0UL; } } }
		/// <summary>The plan as the world file keeps it ("@planrandom=", "@plandesc=", "@planrule=" lines), while it is read.</summary>
		static WorldPlan stored;
		/// <summary>Ids of the plan's rules that have fired in this world.</summary>
		public static readonly HashSet<string> Done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>Metres the raft has sailed in this world (for "km" rules).</summary>
		public static float Sailed;
		/// <summary>When this world was last told about library updates of its islands (UTC ticks, "@libseen="; T10).</summary>
		public static long LibSeen;
		/// <summary>Chosen in Raft's New Game box for the world being created (null = DefaultPlan).</summary>
		public static string PendingPlan;
		/// <summary>The plan new worlds get when none was chosen (defaultPlan in spawnpool.txt).</summary>
		public static string DefaultPlan = WorldPlan.RandomName;

		/// <summary>Raised on the host when a rule brings an island (tests listen).</summary>
		public static event Action<IslandWorldState.Entry, IntroRule> Brought;
		/// <summary>The last "new island" announcement shown on this machine (tests look at it).</summary>
		public static string LastAnnouncement { get; private set; }

		static float nextTick, loadedAt;
		static readonly Dictionary<string, float> retryAt = new Dictionary<string, float>();
		static readonly HashSet<string> warned = new HashSet<string>();

		internal static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }
		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [director] " + msg); }

		#region World file

		/// <summary>Before a world's island list is read.</summary>
		internal static void Reset()
		{
			PlanName = WorldPlan.RandomName;
			Plan = null;
			PlanFrom = "";
			PlanId = "";
			PlanFromWorld = false;
			PlanWasEdited = false;
			PlanOwner = 0;
			savedBy = 0;
			stored = null;
			missingNoted.Clear();
			PlanHashes.Clear();
			Done.Clear();
			Sailed = 0f;
			LibSeen = 0;
			retryAt.Clear(); noRoom.Clear();
			warned.Clear();
		}

		/// <summary>A "@key=value" line of the world file; false if it isn't ours.</summary>
		internal static bool ReadLine(string key, string value)
		{
			switch (key)
			{
				case "plan": PlanName = value.Trim(); return true;
				case "libseen": long ls; if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ls)) LibSeen = ls; return true;
				case "sailed": float s; if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out s)) Sailed = s; return true;
				case "done": foreach (string id in value.Split(',')) if (id.Trim().Length > 0) Done.Add(id.Trim()); return true;
				// The world's own copy of its plan (written since worlds keep one)
				case "planfrom": PlanFrom = value.Trim(); return true;
				case "planid": PlanId = value.Trim(); return true;
				case "planowner": ulong o; if (ulong.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out o)) PlanOwner = o; return true;
				case "planhash":
					// ("<island>:<hash>" - the island's name may have a ':' itself, the hash never - AU6)
					int colon = value.LastIndexOf(':');
					string hashed = colon > 0 ? value.Substring(colon + 1).Trim() : "";
					if (!IslandNetwork.IsHash(hashed)) return false; // (kept as it is - AU5)
					PlanHashes[value.Substring(0, colon).Trim()] = hashed;
					return true;
				case "planrandom": Stored().Random = value.Trim().Equals("on", StringComparison.OrdinalIgnoreCase); return true;
				case "plandesc": Stored().Description = value.Trim(); return true;
				case "planrule":
					IntroRule r = IntroRule.Parse(value);
					if (r != null) { Stored().Rules.Add(r); return true; }
					// (a rule this version can't read - a newer one's: the line is kept as it is in the world file - AU5)
					Debug.LogWarning("[CUSTOM ISLANDS] The world's copy of its plan: a rule this version can't read is kept as it is: " + value);
					return false;
			}
			return false;
		}

		static WorldPlan Stored() { if (stored == null) stored = new WorldPlan { Random = false }; return stored; }

		/// <summary>The world file's "@savedby=" line (read by WorldCopy).</summary>
		internal static void ReadSavedBy(string value)
		{
			ulong id;
			if (ulong.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) savedBy = id;
		}

		internal static IEnumerable<string> WriteLines()
		{
			yield return "@plan=" + PlanName;
			// (the plan itself goes with the world: whoever hosts it later, and whatever happens to the plan's file, the
			// world goes on with the plan it was made with)
			if (Plan != null && !Plan.BuiltIn)
			{
				yield return "@planrandom=" + (Plan.Random ? "on" : "off");
				if ((Plan.Description ?? "").Length > 0) yield return "@plandesc=" + Plan.Description.Replace("\r", " ").Replace("\n", " ");
				foreach (IntroRule r in Plan.Rules) yield return "@planrule=" + r.ToLine();
				// (which island each name is, by its content - AU6; on the plan owner's PC its own files, edits too)
				if (Raft_Network.IsHost) RecordPlanHashes(OwnsPlan);
				foreach (string n in PlanIslands(Plan))
				{
					string h;
					if (PlanHashes.TryGetValue(n, out h)) yield return "@planhash=" + n + ":" + h;
				}
			}
			if (PlanFrom.Length > 0) yield return "@planfrom=" + PlanFrom;
			if (PlanId.Length > 0) yield return "@planid=" + PlanId;
			// (a world without an owner gets the saving PC's id at its next load - "@savedby=", AU25: written now, so the file
			// reads back the same and saves the same again)
			ulong owner = PlanOwner != 0 ? PlanOwner : LocalSteamId;
			if (owner != 0) yield return "@planowner=" + owner.ToString(CultureInfo.InvariantCulture);
			yield return "@sailed=" + Sailed.ToString("F0", CultureInfo.InvariantCulture);
			if (LibSeen > 0) yield return "@libseen=" + LibSeen.ToString(CultureInfo.InvariantCulture);
			if (Done.Count > 0) yield return "@done=" + string.Join(",", Done.ToArray());
		}

		/// <summary>True when the world file is needed for the director's state alone.</summary>
		internal static bool HasState { get { return Done.Count > 0 || !PlanName.Equals(WorldPlan.RandomName, StringComparison.OrdinalIgnoreCase); } }

		/// <summary>The world in the game scene has been set up (a saved world's list read, or a new world started).</summary>
		static bool worldHandled;
		/// <summary>For tests: the world in the game scene has been set up (a new world's start resets the story book and the rest).</summary>
		internal static bool WorldHandled { get { return worldHandled; } }
		/// <summary>The game scene was there at the last tick.</summary>
		static bool inWorld;

		/// <summary>
		/// Raft raises SaveAndLoad.LoadComplete only when it restores a saved world, never for a brand-new one. A new
		/// world is noticed here instead: the first moment in a game with Raft's "new game" flag set (the New Game box
		/// sets it, loading a world clears it), once the raft exists.
		/// </summary>
		static void CheckNewWorld()
		{
			bool isNew = false;
			try { isNew = GameManager.IsInNewGame; } catch { }
			if (!isNew || CustomIslandSpawner.RaftPosition == null || SaveAndLoad.WorldGuid == Guid.Empty) return;
			Log("A new world has started");
			IslandWorldState.OnWorldLoaded();
			try { CreatureSpawner.OnWorldLoaded(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Creatures in the new world: " + e.Message); }
			worldHandled = true;
		}

		/// <summary>Once per update: the world's islands the library updated since it was last played (it has the new version -
		/// a world that kept the old one plays a copy under another name, so it isn't named here).</summary>
		internal static string TellLibraryUpdates()
		{
			string text = null;
			try
			{
				Dictionary<string, string> updated = LibraryPack.UpdatedSince(LibSeen);
				List<string> here = IslandWorldState.Islands.Select(x => x.Name).Where(n => n != null && updated.ContainsKey(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				if (here.Count > 0)
				{
					text = (here.Count == 1 ? "'" + here[0] + "' was" : string.Join(", ", here.Take(4).Select(n => "'" + n + "'").ToArray()) + (here.Count > 4 ? " and " + (here.Count - 4) + " more were" : " were")) +
						" updated from the library since this world was last played: it has the new version now";
					DynamicIslands.Notify(text, false, 8);
					Log(text);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Library updates for this world: " + e.Message); }
			LibSeen = DateTime.UtcNow.Ticks;
			return text;
		}

		/// <summary>After a world's island list was read (host): a brand-new world gets the plan chosen for it.</summary>
		internal static void OnWorldLoaded()
		{
			worldHandled = true;
			loadedAt = Time.unscaledTime;
			if (!Raft_Network.IsHost) return;
			bool isNew = false;
			try { isNew = GameManager.IsInNewGame && Sailed <= 0f && IslandWorldState.Islands.Count == 0 && Done.Count == 0; } catch { }
			if (isNew)
			{
				string chosen = PendingPlan ?? DefaultPlan;
				PendingPlan = null;
				if (WorldPlan.Load(chosen) == null) { Debug.LogWarning("[CUSTOM ISLANDS] No world plan '" + chosen + "'; using " + WorldPlan.RandomName); chosen = WorldPlan.RandomName; }
				SetPlan(chosen, true);
				StoryChain.FromPlan(Plan);
				Log("New world '" + SaveAndLoad.CurrentGameFileName + "': plan '" + PlanName + "'");
				WorldRandomizer.OnNewWorld();
				LibSeen = DateTime.UtcNow.Ticks;
				return;
			}
			TellLibraryUpdates();
			// (a world that names no owner of its plan - saved before it did: the PC that saved it is the owner. Before, a world
			// without an owner took the plan file of whoever hosted it, another player's plan of the same name too - AU25)
			if (PlanOwner == 0 && savedBy != 0) PlanOwner = savedBy;
			AdoptOwnerlessPlan();
			if (stored != null && !WorldPlan.IsBuiltIn(PlanName))
			{
				stored.Name = PlanName;
				WorldPlan edited = PlanFileToPlay();
				if (edited != null)
				{
					// The player changed their plan since the world was saved: the changed plan plays (rules that fired
					// stay fired; new ones come; the world keeps a copy of the new plan from its next save)
					ForgetChangedRules(stored, edited);
					// (random islands on/off changed in the plan: the world follows it - it kept its old switch, CB10)
					if (edited.Random != stored.Random) CustomIslandSpawner.Enabled = edited.Random;
					Plan = edited;
					PlanWasEdited = true;
					if (PlanId.Length == 0) PlanId = edited.Id;
					// (its story chain too: what is unlocked and done stays, the islands keep their frequencies)
					StoryChain.FromPlan(edited);
					Log("The plan '" + PlanName + "' was changed since the world was saved: the changed plan plays");
					DynamicIslands.Notify("You changed the plan '" + PlanName + "' since this world was last saved: the changed plan plays from now on.");
				}
				else
				{
					// The world's own copy: the plan as the world was made with it (another host's plan of the same
					// name, a library update, or the file gone doesn't change it)
					Plan = stored;
					PlanFromWorld = true;
				}
			}
			else
			{
				// A world saved before worlds kept their plan: the plan file, copied into the world with the next save
				Plan = WorldPlan.Load(PlanName);
				if (Plan == null)
				{
					Debug.LogWarning("[CUSTOM ISLANDS] This world's plan '" + PlanName + "' is missing (Mods\\DynamicIslands\\plans); its rules are paused");
					DynamicIslands.Notify("This world's plan '" + PlanName + "' isn't on this PC, and the world was saved before worlds kept their own copy of it. " +
						"Its story can't go on here until the plan is in Mods\\DynamicIslands\\plans (the player who made the world has it).", true);
				}
			}
			stored = null;
			// (a world saved before plans kept their islands' hashes - or a rule added since: what can be known is recorded - AU6)
			RecordPlanHashes(OwnsPlan);
			// (a world from before the story chain, whose plan changes Raft's story: the chain from the plan now)
			if (Plan != null && !StoryChain.HasSnapshot && Plan.HasStory) StoryChain.FromPlan(Plan);
			if (Plan != null && Plan.Rules.Count > 0)
				Log("Plan '" + PlanName + "'" + (PlanFromWorld ? " (the world's own copy)" : " (from its file; the world keeps a copy from the next save)") + ": " +
					Plan.Rules.Count(r => !Done.Contains(r.Id)) + " of " + Plan.Rules.Count + " rule(s) still to come");
		}

		/// <summary>
		/// Loading a saved world: the plan file on this PC when the player changed it since the world was saved and it is
		/// the same plan - the player's own on the PC of the player who made the world, or the same version of a library or
		/// imported entry (edited in World Plans) - else null (the world's own copy plays).
		/// </summary>
		static WorldPlan PlanFileToPlay()
		{
			WorldPlan file = WorldPlan.Load(PlanName);
			if (file == null || stored == null) return null;
			// (another plan given the same name since - the first copied away and deleted, a new one made: not this world's - UW2)
			if (PlanId.Length > 0 && file.Id.Length > 0 && !file.Id.Equals(PlanId, StringComparison.OrdinalIgnoreCase)) return null;
			if (IntroRule.ToLines(file.Rules) == IntroRule.ToLines(stored.Rules) && file.Random == stored.Random && !StoryChain.DiffersFrom(file)) return null;
			string source = LibrarySource.OfPlan(PlanName) ?? "";
			// (a library or imported plan: only while this PC has the version the world was made with - an update of the
			// entry doesn't change worlds already started, just as their islands keep their version)
			if (PlanFrom.Length > 0 || source.Length > 0) return source.Equals(PlanFrom, StringComparison.OrdinalIgnoreCase) ? file : null;
			// (the owner's PC only: with no owner known the world's own copy plays - AU25)
			ulong me = LocalSteamId;
			return PlanOwner != 0 && PlanOwner == me ? file : null;
		}

		/// <summary>The islands the host was told are missing (tests).</summary>
		public static IEnumerable<string> MissingIslands { get { return missingNoted; } }

		/// <summary>Islands a rule couldn't bring because their file isn't on this PC (told once per island).</summary>
		static readonly HashSet<string> missingNoted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Host: a rule's island is missing on this PC - say so on the screen once, and where to get it.</summary>
		/// <summary>The rule's island isn't on this PC (a rule waiting for it, the story chain): the host is told once.</summary>
		internal static void NoteMissingIf(string why, IntroRule r, IslandWorldState.Entry owner)
		{
			if (why != null && (why.StartsWith(NoIslandPrefix) || why.StartsWith(NoneOfPrefix))) NoteMissing(r, owner);
		}

		/// <summary>Host: whether the rule's island isn't on this PC (the story chain's, told at once - it was told only when
		/// someone tuned the Receiver to it), and if so the host is told once.</summary>
		internal static bool NoteIfMissing(IntroRule r)
		{
			if (r == null) return false;
			bool missing = r.What == "island" ? r.WhatArg.Trim().Length > 0 && FileFor(r.WhatArg.Trim()) == null
				: r.What == "oneof" && !r.WhatArg.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).Any(n => FileFor(n) != null);
			if (missing) NoteMissing(r, null);
			return missing;
		}

		/// <summary>Whether Bring's answer means the island's file isn't on this PC.</summary>
		internal static bool IsMissing(string why) { return why != null && (why.StartsWith(NoIslandPrefix) || why.StartsWith(NoneOfPrefix)); }

		static void NoteMissing(IntroRule r, IslandWorldState.Entry owner)
		{
			string name = r.What == "island" ? r.WhatArg.Trim() : r.WhatArg;
			if (!missingNoted.Add(name)) return;
			string from = owner == null ? PlanFrom : "";
			string where = from.StartsWith("library:") ? "It comes with the library entry '" + LibrarySource.Describe(from) + "' - download it in the island library."
				: from.StartsWith("import:") ? "It came with '" + LibrarySource.Describe(from) + "' - import that again."
				: "Ask the player who made this world for it (they can export the plan or island), then import it.";
			// (this PC has an island of that name, but another one than the world's plan was made with - AU6)
			bool other = r.What == "island" && PlanHashes.ContainsKey(name) && File.Exists(IslandSpawner.PathFor(name));
			string what = other ? "The world's story needs the island '" + name + "' it was made with - this PC's '" + name + "' is another island - so it can't appear yet. "
				: "The world's story needs the island '" + name + "', which isn't on this PC, so it can't appear yet. ";
			// (the players are asked for it: one who joined this world before may have it - AU6)
			if (r.What == "island" && PlanHashes.ContainsKey(name)) where += " A player who has it can bring it: it is asked of everyone who joins.";
			DynamicIslands.Notify(what + where, true);
		}

		/// <summary>
		/// A plan coming in place of another (the same plan edited, or another plan): a rule that fired under an id that now
		/// brings something else is another rule under an old name, and comes. Rule ids are reused - World Plans numbers
		/// new rules, so a deleted "rule3" and a new "rule3"; another plan has its own "rule1" - and the new one never came.
		/// The same rule edited (its words, when or where) stays done.
		/// </summary>
		static void ForgetChangedRules(WorldPlan before, WorldPlan after)
		{
			if (before == null || after == null) return;
			foreach (IntroRule r in after.Rules.Where(x => Done.Contains(x.Id)).ToList())
			{
				IntroRule old = before.Rules.FirstOrDefault(x => x.Id.Equals(r.Id, StringComparison.OrdinalIgnoreCase));
				if (old == null || (old.What == r.What && old.WhatArg.Trim().Equals(r.WhatArg.Trim(), StringComparison.OrdinalIgnoreCase))) continue;
				Done.Remove(r.Id);
				Log("Rule '" + r.Id + "' now brings " + r.DescribeWhat() + " (it brought " + old.DescribeWhat() + "): another rule under that name - it comes");
			}
		}

		/// <summary>Host: gives this world a plan (applyRandom: random islands on or off as the plan says).</summary>
		public static bool SetPlan(string name, bool applyRandom)
		{
			WorldPlan plan = WorldPlan.Load(name);
			if (plan == null) return false;
			ForgetChangedRules(Plan, plan);
			Plan = plan;
			PlanName = plan.Name;
			PlanFromWorld = false;
			PlanFrom = LibrarySource.OfPlan(plan.Name) ?? "";
			PlanOwner = LocalSteamId; // (the host who picks the plan: their plan file is the one an edit happens in)
			// (a plan of the player's own from before plans had ids: given one now, so a later plan of the same name is told apart)
			if (plan.Id.Length == 0 && !plan.BuiltIn && PlanFrom.Length == 0) { try { plan.Save(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not give the plan '" + plan.Name + "' its id: " + e.Message); } }
			PlanId = plan.Id;
			// (the plan's islands as this PC has them: the ones any later host brings - AU6)
			PlanHashes.Clear();
			RecordPlanHashes(true);
			if (applyRandom) CustomIslandSpawner.Enabled = plan.Random;
			retryAt.Clear();
			return true;
		}

		#endregion

		#region Runtime

		/// <summary>Every frame from the mod.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + TickInterval;
			if (!LoadSceneManager.IsGameSceneLoaded)
			{
				worldHandled = false;
				// (the world was left: its randomizer, options and waiting banners go with it - the next world used them until
				// its own were read, a new one for a second)
				if (inWorld) { inWorld = false; WorldRandomizer.Reset(); WorldOptions.Forget(); IslandInfo.Forget(); }
				return;
			}
			inWorld = true;
			if (!worldHandled) CheckNewWorld();
			if (!Raft_Network.IsHost || Time.unscaledTime - loadedAt < StartDelay) return;
			if (!CustomIslandSpawner.RaftPosition.HasValue) return;
			UpdateVisits();
			Evaluate();
			IslandCache.SaveRules();
			StoryChain.Tick();
			ReturningIslands.Tick();
			QuestMilestones.Tick();
		}

		/// <summary>Checks every rule that hasn't fired yet (host; tests call it directly).</summary>
		public static void Evaluate()
		{
			// (Receiver, by-chance and story rules are brought by the story chain: done once their island is in the world - CB10)
			if (Plan != null)
				foreach (IntroRule r in Plan.Rules)
				{
					if (Done.Contains(r.Id) || r.Special) continue; // (the story chain, the Receiver and islands by chance: StoryChain)
					IntroRule rule = r;
					TryRule(rule, null, "plan/" + rule.Id, () => Done.Add(rule.Id));
				}
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.ToList())
			{
				if (e.Failed || e.WaitingForFile) continue;
				List<IntroRule> rules = RulesOf(e);
				for (int i = 0; i < rules.Count; i++)
				{
					int key = RuleKeyBase + i;
					if (e.State.ContainsKey(key)) continue;
					IslandWorldState.Entry owner = e;
					TryRule(rules[i], e, e.Id + "/" + i, () => owner.State[key] = new ObjectState { Active = false, Yield = 0, Day = Today });
				}
			}
		}

		/// <summary>The island's own rules (from its settings).</summary>
		public static List<IntroRule> RulesOf(IslandWorldState.Entry e)
		{
			// (an island not loaded: its rules from the cache kept on disk, not its whole file - a fresh host's first look
			// through a world of 152 islands read every file: 9.4 s, IM43)
			if (e != null && (e.Root == null || e.Root.GetComponent<IslandSettings>() == null)) return IslandCache.RulesOf(e.Name);
			return RulesFromProps(IslandCache.PropsOf(e));
		}

		public static List<IntroRule> RulesFromProps(IDictionary<string, string> props)
		{
			string text;
			return props != null && props.TryGetValue(IslandRulesKey, out text) ? IntroRule.ParseLines(text) : new List<IntroRule>();
		}

		public static void SetRulesInProps(IDictionary<string, string> props, IEnumerable<IntroRule> rules)
		{
			List<IntroRule> list = rules.ToList();
			if (list.Count == 0) props.Remove(IslandRulesKey);
			else props[IslandRulesKey] = IntroRule.ToLines(list);
		}

		static readonly Dictionary<string, int> noRoom = new Dictionary<string, int>();
		static bool wideSearch;

		/// <summary>Copies of an island its own rules may bring into a world (ROADMAP CB3: a rule bringing its own island
		/// fired again on each copy - with "on day 2" every second, without end).</summary>
		public const int MaxSelfCopies = 10;

		/// <summary>An island rule that brings the island it is on, when the world has MaxSelfCopies of it already.</summary>
		internal static bool TooManyCopies(IntroRule r, IslandWorldState.Entry owner)
		{
			if (owner == null || owner.HostName == null) return false;
			bool self = r.What == "island" ? r.WhatArg.Trim().Equals(owner.HostName, StringComparison.OrdinalIgnoreCase)
				: r.What == "oneof" && r.WhatArg.Split(',').All(n => n.Trim().Length == 0 || n.Trim().Equals(owner.HostName, StringComparison.OrdinalIgnoreCase));
			return self && IslandWorldState.Islands.Count(e => owner.HostName.Equals(e.HostName, StringComparison.OrdinalIgnoreCase)) >= MaxSelfCopies;
		}

		static void TryRule(IntroRule r, IslandWorldState.Entry owner, string key, Action markDone)
		{
			IslandWorldState.Entry at;
			if (!Met(r, owner, out at)) return;
			if (TooManyCopies(r, owner))
			{
				markDone();
				Log("Rule '" + r.Id + "' of '" + owner.HostName + "' brings its own island: the world has " + MaxSelfCopies + " of it already, so it is done without one");
				return;
			}
			float t;
			if (retryAt.TryGetValue(key, out t) && Time.unscaledTime < t) return;
			// (no room after a few tries: looked for further out and all round - AU28)
			int tries;
			noRoom.TryGetValue(key, out tries);
			wideSearch = tries >= 3;
			string why;
			try { why = Bring(r, owner, at); }
			finally { wideSearch = false; }
			if (why == null) { markDone(); retryAt.Remove(key); noRoom.Remove(key); return; }
			retryAt[key] = Time.unscaledTime + RetrySeconds;
			if (why.StartsWith("no free spot"))
			{
				noRoom[key] = tries + 1;
				// (the host is told once: it only waited in the log before)
				if (tries + 1 == 3 && Raft_Network.IsHost) DynamicIslands.Notify("A rule of the world's plan ('" + r.Id + "') has no room for its island near the raft: it keeps looking, further out", true);
			}
			if (warned.Add(key + why)) Log("Rule '" + r.Id + "' (" + r.Describe() + ") waits: " + why);
			if (why.StartsWith(NoIslandPrefix) || why.StartsWith(NoneOfPrefix)) NoteMissing(r, owner);
		}

		/// <summary>The islands a ref points at: "self" = the rule's own island, else islands brought by that rule, else by island name.</summary>
		public static List<IslandWorldState.Entry> Refs(string reference, IslandWorldState.Entry owner)
		{
			reference = (reference ?? "").Trim();
			if (reference.Length == 0 || reference.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase))
				return owner != null ? new List<IslandWorldState.Entry> { owner } : new List<IslandWorldState.Entry>();
			var byRule = IslandWorldState.Islands.Where(e => e.Rule.Equals(reference, StringComparison.OrdinalIgnoreCase)).ToList();
			return byRule.Count > 0 ? byRule : IslandWorldState.Islands.Where(e => e.HostName.Equals(reference, StringComparison.OrdinalIgnoreCase)).ToList();
		}

		static float Number(string s)
		{
			float f;
			return float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f;
		}

		/// <summary>Whether the rule's moment has come; at = the island where it happened (if any).</summary>
		public static bool Met(IntroRule r, IslandWorldState.Entry owner, out IslandWorldState.Entry at)
		{
			at = null;
			switch (r.When)
			{
				case "start": return true;
				case "km": return Sailed >= Number(r.WhenArg) * 1000f;
				case "day": return Today >= Number(r.WhenArg);
				case "rule":
					// (a Receiver, sailing or story rule is StoryChain's: done once its island came - it never enters Done)
					if (Done.Contains(r.WhenRef) || StoryChain.Brought.Contains(r.WhenRef)) return true;
					// An island's rule may wait for another of its own rules
					if (owner != null)
					{
						int i = RulesOf(owner).FindIndex(x => x.Id.Equals(r.WhenRef, StringComparison.OrdinalIgnoreCase));
						return i >= 0 && owner.State.ContainsKey(RuleKeyBase + i);
					}
					return false;
			}
			foreach (IslandWorldState.Entry e in Refs(r.WhenRef, owner))
				if (Happened(r, e)) { at = e; return true; }
			return false;
		}

		internal static bool Happened(IntroRule r, IslandWorldState.Entry e)
		{
			switch (r.When)
			{
				case "quest":
					// ("quest" = the main quest; "quest 2".. = the island's further quests - ROADMAP LM4)
					int qn = r.WhenArg.Trim().Length > 0 ? Mathf.Clamp((int)Number(r.WhenArg) - 1, 0, IslandQuest.MaxQuests - 1) : 0;
					return QuestTracker.IsDone(e, qn);
				case "step": return QuestTracker.StepOf(e) >= Mathf.Max(1, (int)Number(r.WhenArg));
				case "zone":
					// (any zone of that name: with two zones called 'gate' the quest counted either, the rule only the first)
					return IslandCache.ZoneOrdinals(e.Name, r.WhenArg).Any(o => ContentState.IsUsed(e, TriggerZone.KeyBase + o));
				case "visit": return e.State.ContainsKey(VisitKey);
				case "signal": return Behaviours.HasSignal(e, r.WhenArg);
			}
			return false;
		}

		/// <summary>Host: marks islands a player has come to (for "visit" rules; kept with the island's state).</summary>
		static void UpdateVisits()
		{
			List<Vector3> players = Players.All.Where(p => p != null).Select(p => p.transform.position).ToList();
			if (players.Count == 0) return;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.State.ContainsKey(VisitKey) || e.Failed) continue;
				float r = CustomIslandSpawner.LandRadius(e.Name);
				if (r < 0f) continue;
				if (players.Any(p => new Vector2(p.x - e.Position.x, p.z - e.Position.z).magnitude < r + VisitMargin))
				{
					e.State[VisitKey] = new ObjectState { Active = true, Yield = 0, Day = Today };
					Log("Players reached '" + e.HostName + "'");
				}
			}
		}

		#endregion

		#region Bringing an island

		const string NoIslandPrefix = "there is no saved island ", NoneOfPrefix = "none of the islands ";

		/// <summary>
		/// The file a rule plays for an island named so. When the world's plan recorded the island's content hash (AU6): the
		/// file with that content - this PC's own when it matches, else its name_hash copy, else any island file with it (a
		/// pack's island renamed "Camp (Pack title)" on import) - and on the plan owner's PC their own file (their edits play,
		/// as before); else null: the island is missing here (and the players are asked for it), rather than another host's
		/// own, different island of the same name being brought. Without a recorded hash (an island's own rules, a world from
		/// before): its own, else the newest copy of it this PC has (name_hash: a player who hosts a world after another did -
		/// host swap - may have only the copy they downloaded when they joined). Null when there is neither.
		/// </summary>
		internal static string FileFor(string name)
		{
			string hash;
			if (PlanHashes.TryGetValue(name, out hash))
			{
				string found = IslandNetwork.FileWithHash(hash, name);
				if (found != null) return found;
				if (OwnsPlan && File.Exists(IslandSpawner.PathFor(name))) return name;
				if (Raft_Network.IsHost) IslandNetwork.WantFromPlayers(name, hash);
				return null;
			}
			return AnyFileFor(name);
		}

		/// <summary>An island's own file, else the newest name_hash copy of it here (any version), else null.</summary>
		static string AnyFileFor(string name)
		{
			if (File.Exists(IslandSpawner.PathFor(name))) return name;
			string prefix = name + "_";
			return IslandSpawner.ListSavedIslands().Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.Length == prefix.Length + 12 && IslandNetwork.IsDownloadName(n))
				.OrderByDescending(n => File.GetLastWriteTimeUtc(IslandSpawner.PathFor(n))).FirstOrDefault();
		}

		static ulong localId;

		/// <summary>This PC picked the world's plan (or saved the world when no owner is known): its island files are the plan's (AU6).</summary>
		static bool OwnsPlan
		{
			get
			{
				if (localId == 0) localId = LocalSteamId;
				ulong owner = PlanOwner != 0 ? PlanOwner : savedBy;
				return owner != 0 && owner == localId;
			}
		}

		/// <summary>
		/// A world that names no owner at all - saved before worlds kept who saved them (@savedby, 09-29): the host loading it
		/// owns its plan from now on, as its next save writes anyway (@planowner). Its island files are then the plan's and their
		/// hashes are recorded at once, as for a world made today - before, it found the plan's islands by name (pre-10-06).
		/// </summary>
		internal static void AdoptOwnerlessPlan()
		{
			if (PlanOwner != 0 || savedBy != 0 || !Raft_Network.IsHost) return;
			ulong me = LocalSteamId;
			if (me == 0) return;
			PlanOwner = me;
			Log("This world names no owner of its plan (saved before worlds kept one): this PC's islands are the plan's from now on");
		}

		/// <summary>For tests: the world file's @savedby id.</summary>
		internal static ulong SavedByForTest { get { return savedBy; } set { savedBy = value; } }

		/// <summary>For tests: the library's installed entries to use instead of installed.json.</summary>
		internal static List<LibraryInstalled> InstalledForTest;

		/// <summary>
		/// The content hash of a plan island the library installed with the world's plan (@planfrom = that entry's source and
		/// version) and that is unchanged here since: every PC with that version has the same island, so its hash is known
		/// without the plan's owner (pre-10-06 worlds hosted by another player). Null when not known that way.
		/// </summary>
		static string LibraryHashFor(string name, ref List<LibraryInstalled> installed)
		{
			if (PlanFrom.Length == 0) return null;
			if (installed == null) installed = InstalledForTest ?? LibraryPack.Installed();
			LibraryInstalled entry = installed.FirstOrDefault(x => x.Source.Equals(PlanFrom, StringComparison.OrdinalIgnoreCase));
			LibraryInstalledFile f = entry == null ? null : entry.files.FirstOrDefault(x => x.kind == LibraryPack.KindIsland && x.name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (f == null || string.IsNullOrEmpty(f.sha256)) return null;
			string sha = LibraryPack.ShaOfFile(IslandSpawner.PathFor(f.name));
			if (sha == null || !sha.Equals(f.sha256, StringComparison.OrdinalIgnoreCase)) return null; // (changed here since: not the plan's any more)
			return IslandNetwork.HashOf(f.name);
		}

		/// <summary>The island names a plan's rules bring ("island" and "oneof" rules).</summary>
		internal static IEnumerable<string> PlanIslands(WorldPlan plan)
		{
			if (plan == null) return Enumerable.Empty<string>();
			return plan.Rules.SelectMany(r => r.What == "island" ? new[] { r.WhatArg.Trim() } : r.What == "oneof" ? r.WhatArg.Split(',').Select(n => n.Trim()).ToArray() : new string[0])
				.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Records the content hash of the plan's islands not recorded yet (AU6): from an island of that name already in the
		/// world, else - ownFiles: this PC picked the plan - from this PC's file of it (its own, else the newest copy). On the
		/// owner's PC its own file's hash replaces a recorded one: the owner's edited island is the plan's from then on.
		/// </summary>
		internal static void RecordPlanHashes(bool ownFiles)
		{
			List<LibraryInstalled> installed = null;
			foreach (string n in PlanIslands(Plan).ToList())
			{
				if (ownFiles && File.Exists(IslandSpawner.PathFor(n)))
				{
					string own = IslandNetwork.HashOf(n);
					if (own != null) { PlanHashes[n] = own; continue; }
				}
				if (PlanHashes.ContainsKey(n)) continue;
				IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName.Equals(n, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(x.Hash ?? IslandNetwork.HashOf(x.Name)));
				string h = e != null ? e.Hash ?? IslandNetwork.HashOf(e.Name) : null;
				if (h == null) h = LibraryHashFor(n, ref installed);
				if (h == null && ownFiles) { string f = AnyFileFor(n); if (f != null) h = IslandNetwork.HashOf(f); }
				if (h != null && IslandNetwork.IsHash(h)) PlanHashes[n] = h;
			}
		}

		/// <summary>Brings the rule's island; null when it's on its way, else why not (yet).</summary>
		internal static string Bring(IntroRule r, IslandWorldState.Entry owner, IslandWorldState.Entry at)
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue) return "no raft";
			var rnd = new System.Random(Guid.NewGuid().GetHashCode());
			string name = null;
			MapType type = null;
			switch (r.What)
			{
				case "island":
					name = r.WhatArg.Trim();
					if (FileFor(name) == null) return NoIslandPrefix + "'" + name + "'";
					break;
				case "oneof":
					var names = r.WhatArg.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0 && FileFor(n) != null).ToList();
					if (names.Count == 0) return NoneOfPrefix + "'" + r.WhatArg + "' exist";
					// Islands not in the world yet first
					var fresh = names.Where(n => !IslandWorldState.Islands.Any(e => e.HostName.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
					if (fresh.Count > 0) names = fresh;
					name = names[rnd.Next(names.Count)];
					break;
				case "pool":
					name = CustomIslandSpawner.PickFromPool();
					if (name == null) return "the spawn pool is empty";
					if (name == CustomIslandSpawner.GeneratedEntry) type = MapTypes.Get("random");
					else if (name.StartsWith(CustomIslandSpawner.TypePrefix, StringComparison.OrdinalIgnoreCase))
					{
						type = MapTypes.Get(name.Substring(CustomIslandSpawner.TypePrefix.Length));
						if (type == null) return "the spawn pool has an unknown map type '" + name + "'";
					}
					break;
				case "type":
					type = MapTypes.Get(r.WhatArg);
					if (type == null) return "there is no map type '" + r.WhatArg + "'";
					break;
			}

			IslandGenSettings gen = null;
			float elevation, radius;
			// (the file played: the island's own, or a copy of it - the world keeps the island's own name, so rules, quests
			// and the journal still find it, and the copy's hash, so a player who joins gets that version)
			string file = type == null ? FileFor(name) ?? name : name;
			if (type != null)
			{
				gen = MapTypes.Roll(type, rnd, out elevation);
				name = MapTypes.FreeFileName(type, gen);
				file = name; // (a generated island: its own new file - before, file stayed null and the world's entry had no name)
				radius = MapTypes.EstimatedRadius(gen);
			}
			else
			{
				radius = CustomIslandSpawner.LandRadius(file);
				if (radius < 0f) return "island '" + name + "' can't be read";
				elevation = CustomIslandSpawner.Elevation(file);
			}

			string why;
			Vector3? spot = Place(r, owner, at, radius, elevation, out why);
			if (!spot.HasValue) return why;

			IslandWorldState.Entry entry = IslandWorldState.Add(name, spot.Value, null, false);
			if (file != name) { entry.Name = file; entry.Hash = IslandNetwork.HashOf(file); Log("'" + name + "' as the world's plan has it isn't on this PC under its name: played from " + file); }
			entry.Rule = r.Id;
			// (the first time the world brings a plan island with no hash recorded: this one is the plan's from now on - AU6)
			if (type == null && !PlanHashes.ContainsKey(name) && PlanIslands(Plan).Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				string h = IslandNetwork.HashOf(file);
				if (h != null) PlanHashes[name] = h;
			}
			entry.Label = LabelFor(r, file, type);
			entry.Loading = true;
			if (gen != null)
			{
				MapType t = type; IslandGenSettings s = gen; float el = elevation; string n = name;
				CustomIslandSpawner.CacheSize(name, radius, elevation);
				DynamicIslands.instance.StartCoroutine(CustomIslandSpawner.GenerateAndSpawn(() => MapTypes.Create(t, s, el, n), name, entry));
			}
			else
			{
				IslandNetwork.BroadcastAdded(entry);
				DynamicIslands.instance.StartCoroutine(DynamicIslands.instance.SpawnIslandFile(file, spot.Value, true, entry));
			}
			Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
			Log("Rule '" + r.Id + "'" + (owner != null ? " of '" + owner.HostName + "'" : "") + ": bringing '" + name + "' " +
				new Vector2(spot.Value.x - raft.x, spot.Value.z - raft.z).magnitude.ToString("F0") + " m from the raft (" + r.Describe() + ")");
			Announce(entry, r, type);
			if (Brought != null) try { Brought(entry, r); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Director listener: " + e.Message); }
			return null;
		}

		/// <summary>The name shown on the Receiver: the rule's label, else the island's own name.</summary>
		static string LabelFor(IntroRule r, string name, MapType type)
		{
			if (r.Label.Trim().Length > 0) return r.Label.Trim();
			string title = type == null ? ObjectProps.Get(IslandCache.Props(name), IslandProps.Title) : "";
			return title;
		}

		/// <summary>Where the island goes: clear of the raft, the other custom islands and Raft's own islands.</summary>
		static Vector3? Place(IntroRule r, IslandWorldState.Entry owner, IslandWorldState.Entry at, float radius, float elevation, out string why)
		{
			why = null;
			Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
			var reasons = new List<string>();
			// (SL9: a rule that needs a blueprint the host hasn't got - the engine, say - comes straight ahead, near or not)
			bool waits = !r.NeedsMet;
			if (r.Where == "near" && !waits)
			{
				IslandWorldState.Entry near = r.WhereRef.Length == 0 || r.WhereRef.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase)
					? (owner ?? at) : Refs(r.WhereRef, owner).FirstOrDefault();
				if (near == null && at != null) near = at;
				if (near == null) { why = "there is no island '" + r.WhereRef + "' in the world to place it near"; return null; }
				float baseRadius = Mathf.Max(0f, CustomIslandSpawner.LandRadius(near.Name));
				float distance = Mathf.Max(r.Distance, baseRadius + radius + CustomIslandSpawner.Clearance);
				float angle = IntroRule.DirectionAngle(r.Direction);
				bool any = float.IsNaN(angle);
				if (any) angle = (float)new System.Random(Guid.NewGuid().GetHashCode()).NextDouble() * 360f;
				foreach (Vector2 o in Candidates(any))
				{
					float a = angle + o.x;
					Vector3 c = near.Position + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * distance * o.y;
					c.y = elevation;
					string no = CustomIslandSpawner.Rejects(c, radius, raft, false, 0f);
					if (no == null) return c;
					reasons.Add(no);
				}
			}
			else
			{
				Vector3 dir = CustomIslandSpawner.SailDirection();
				float distance = Mathf.Max(r.Distance, radius + CustomIslandSpawner.Clearance + CustomIslandSpawner.RaftRadius);
				foreach (Vector2 o in Candidates(false).Where(o => wideSearch || Mathf.Abs(o.x) <= 60f).OrderBy(o => waits && Mathf.Abs(o.x) > 16f ? 1 : 0))
				{
					Vector3 c = raft + Quaternion.Euler(0, o.x, 0) * dir * distance * o.y;
					c.y = elevation;
					string no = CustomIslandSpawner.Rejects(c, radius, raft, false, 0f);
					if (no == null) return c;
					reasons.Add(no);
				}
			}
			why = "no free spot (" + string.Join("; ", reasons.Distinct().Take(3).ToArray()) + ")";
			return null;
		}

		/// <summary>
		/// Places to try around the wanted spot, best first: (angle off the wanted direction, distance factor). Going a
		/// bit further out costs less than turning away from the direction the builder chose. Raft packs its own
		/// islands densely, so a big island often needs a few tries.
		/// </summary>
		static IEnumerable<Vector2> Candidates(bool anyDirection)
		{
			float[] offsets = anyDirection ? Enumerable.Range(0, 24).Select(i => i * 15f).ToArray() : new[] { 0f, 8f, -8f, 16f, -16f, 25f, -25f, 35f, -35f, 45f, -45f, 60f, -60f, 80f, -80f };
			float[] factors = wideSearch ? new[] { 1f, 1.2f, 1.5f, 2f, 2.4f, 3f, 3.6f, 4.5f } : new[] { 1f, 1.1f, 1.2f, 1.35f, 1.5f, 1.7f, 2f, 2.4f };
			return offsets.SelectMany(a => factors.Select(f => new Vector2(a, f)))
				.OrderBy(v => (anyDirection ? 0f : Mathf.Abs(v.x) / 20f) + (v.y - 1f) * 2.5f)
				.ToList();
		}

		#endregion

		#region Announcing

		/// <summary>Host: tells every player (and itself) that an island appeared, with where it is.</summary>
		static void Announce(IslandWorldState.Entry e, IntroRule r, MapType type)
		{
			string title = e.Label.Length > 0 ? e.Label : type != null ? type.Label : "A new island";
			IslandNetwork.SendAnnounce(e, title, r.Message);
			Show(title, r.Message, e.Position);
		}

		/// <summary>The banner on this machine: the message, and how far and which way the island is from the player.</summary>
		public static void Show(string title, string message, Vector3 position)
		{
			string where = "";
			Network_Player p = RAPI.GetLocalPlayer();
			if (p != null)
			{
				Vector2 d = new Vector2(position.x - p.transform.position.x, position.z - p.transform.position.z);
				float angle = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
				where = "About " + (Mathf.Round(d.magnitude / 10f) * 10f).ToString("F0") + " m to the " + IntroRule.DirectionName(angle) + (CustomIslandSpawner.ShowOnReceiver ? " (on your Receiver)" : "");
			}
			string text = message.Trim().Length > 0 ? message.Trim() + (where.Length > 0 ? "\n" + where : "") : where;
			LastAnnouncement = title + " | " + text.Replace("\n", " | ");
			IslandInfo.Show(title, "", text);
			Debug.Log("[CUSTOM ISLANDS] New island: " + LastAnnouncement);
		}

		#endregion

		#region Commands

		/// <summary>A player who joined: the host's plan and where its rules stand, from the host's copy of the world file.</summary>
		public static string DescribeForPlayer(bool withRules = true)
		{
			if (WorldCopy.HostLines == null) return "World plan: the host's (it comes with the host's next save of the world)";
			// (no plan line: the host's world has nothing of its own stored - the Random islands plan, nothing done yet)
			string plan = WorldCopy.HostValue("plan") ?? WorldPlan.RandomName;
			var done = new HashSet<string>((WorldCopy.HostValue("done") ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase);
			var brought = new HashSet<string>((WorldCopy.HostValue("storybrought") ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase);
			List<IntroRule> rules = WorldCopy.HostLines.Where(l => l.StartsWith("@planrule=")).Select(l => IntroRule.Parse(l.Substring(10))).Where(r => r != null).ToList();
			foreach (IntroRule r in rules) if (r.Special && brought.Contains(r.Id)) done.Add(r.Id);
			string auto = WorldCopy.HostValue("auto");
			var lines = new List<string> { "World plan (the host's): " + plan + "; random islands while sailing " + (auto != null && auto.Equals("off", StringComparison.OrdinalIgnoreCase) ? "off" : "on") +
				(rules.Count > 0 ? "; " + rules.Count(r => done.Contains(r.Id)) + " of " + rules.Count + " island(s) of the plan brought" : "") };
			if (withRules) foreach (IntroRule r in rules) lines.Add("  [" + (done.Contains(r.Id) ? "done" : "    ") + "] " + r.Id + ": " + r.Describe());
			if (withRules && (StoryChain.Active || StoryChain.Rules.Count > 0)) lines.Add(StoryChain.Describe());
			return string.Join("\n", lines.ToArray());
		}

		/// <summary>What the current world's plan is and where its rules stand.</summary>
		public static string Describe()
		{
			var lines = new List<string> { "World plan: " + PlanName + (Plan == null ? " (missing!)" : "") + "; random islands while sailing " + (CustomIslandSpawner.Enabled ? "on" : "off") +
				"; sailed " + (Sailed / 1000f).ToString("F1", CultureInfo.InvariantCulture) + " km, day " + Today };
			if (Plan != null)
				foreach (IntroRule r in Plan.Rules)
					lines.Add("  [" + (Done.Contains(r.Id) || (r.Special && StoryChain.Brought.Contains(r.Id)) ? "done" : "    ") + "] " + r.Id + ": " + r.Describe());
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				List<IntroRule> rules = RulesOf(e);
				for (int i = 0; i < rules.Count; i++)
					lines.Add("  [" + (e.State.ContainsKey(RuleKeyBase + i) ? "done" : "    ") + "] " + e.HostName + "/" + rules[i].Id + ": " + rules[i].Describe());
			}
			if (StoryChain.Active || StoryChain.Rules.Count > 0) lines.Add(StoryChain.Describe());
			lines.Add("Plans: " + string.Join(", ", WorldPlan.All().ToArray()) + "  (WorldPlan <name> changes this world's plan)");
			return string.Join("\n", lines.ToArray());
		}

		#endregion
	}

	/// <summary>
	/// Raft places its own islands (chunk points) as the raft sails into new sea. Custom islands can be far ahead of
	/// the raft (a plan's "600 m north of the camp"), where Raft hasn't placed anything yet: without this, one of
	/// Raft's islands could later appear inside ours. Here Raft's check for a free spot also keeps clear of them.
	/// </summary>
	[HarmonyPatch(typeof(ChunkManager), "DoesPointFit")]
	static class ChunkPointFitPatch
	{
		static void Postfix(ChunkPoint pointToCheck, ref bool __result)
		{
			if (!__result || pointToCheck == null) return;
			try
			{
				bool wreck = pointToCheck.rule != null && pointToCheck.rule.name.IndexOf("FloatingRaft", StringComparison.OrdinalIgnoreCase) >= 0;
				float overlap = pointToCheck.rule != null ? pointToCheck.rule.collisionOverlapRadius : 150f;
				Vector3 p = pointToCheck.worldPosition;
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
				{
					float r = CustomIslandSpawner.LandRadius(e.Name);
					if (r < 0f) continue;
					if (new Vector2(p.x - e.Position.x, p.z - e.Position.z).magnitude < (wreck ? 20f : overlap) + r)
					{
						__result = false;
						return;
					}
				}
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Chunk point check: " + ex.Message); }
		}
	}

	/// <summary>
	/// Islands the players still need come back. An island the raft has left behind - unloaded, past the unload distance
	/// from every player - comes back ahead of the raft after ReturnMinutes, as it was (its quest, chests and harvest
	/// kept; the other players' copies move too), when:
	///   - something still waits for it - a rule of the world's plan or of an island (its quest, a step, a zone, a signal,
	///     players reaching it), or the Receiver chain (an island it comes after): the island leads on, so it comes back
	///     every time until that happened;
	///   - or players reached it and its quest isn't done: it comes back MaxReturns times (an island left on purpose
	///     stops coming back);
	///   - or a km / day rule with "again" brought it (IntroRule.Again, ROADMAP SR1) and the players aren't done with it
	///     (Finished): it comes back every N km / days after the players last were near it, with no cap, even unvisited.
	/// Not an island a Receiver frequency brought (the Receiver shows the way to it), the randomizer's extras on Raft's
	/// islands, or an island nobody needs. Raft's current carries a raft one way: before this, an unfinished island that
	/// drifted out of reach was lost (the user, 2026-10-02: The Long Voyage's first side trip, with no Receiver yet).
	/// </summary>
	public static class ReturningIslands
	{
		/// <summary>Minutes an island waits behind the raft before it comes back (spawnpool.txt returnMinutes; 0 = never).</summary>
		public static float ReturnMinutes = 12f;
		/// <summary>How often an island nothing waits for comes back (its quest begun and not done).</summary>
		public const int MaxReturns = 3;
		/// <summary>State key: how often the island came back (kept with the island's state, saved with the world).</summary>
		public const int ReturnsKey = 0x50F00;
		/// <summary>State key of an "again" island: Day = the day it was last near the players (or brought), Yield = the metres
		/// sailed then - its next return counts from there.</summary>
		public const int AgainKey = 0x50F01;
		const float TickSeconds = 2f, RetrySeconds = 30f;

		static readonly Dictionary<int, float> awaySince = new Dictionary<int, float>();
		static float nextTick;
		static Guid clocksFor;

		/// <summary>Raised on the host when an island came back (tests listen).</summary>
		public static event Action<IslandWorldState.Entry> Returned;

		/// <summary>The clocks start again (another world: Tick does it by itself).</summary>
		public static void Reset() { awaySince.Clear(); }

		/// <summary>Host, from WorldDirector.Tick.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + TickSeconds;
			if (!Raft_Network.IsHost) return;
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue) return;
			// (another world loaded: the clocks start again - only then: reset while the director waited for a NEW world, the
			// clocks of a loaded world were wiped every second and no island ever came back, 2026-10-03)
			if (SaveAndLoad.WorldGuid != clocksFor) { awaySince.Clear(); clocksFor = SaveAndLoad.WorldGuid; }
			// (game time: a paused game doesn't bring islands back)
			float now = Time.time;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.ToList())
			{
				string why = Why(e);
				float edge = Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name));
				bool away = e.Root == null && !e.Loading && Flat(e.Position - raft.Value) - edge > WorldRules.UnloadDistance;
				if (why == "again") { TickAgain(e, raft.Value, away); continue; }
				if (why == null || !away || ReturnMinutes <= 0f) { awaySince.Remove(e.Id); continue; }
				float since;
				if (!awaySince.TryGetValue(e.Id, out since))
				{
					awaySince[e.Id] = now;
					Debug.Log("[CUSTOM ISLANDS] [returning] '" + e.HostName + "' left behind (" + Flat(e.Position - raft.Value).ToString("F0") + " m): back ahead of the raft in " +
						ReturnMinutes.ToString("0.##") + " min (" + (why == "awaited" ? "something waits for it" : "its quest is unfinished") + ")");
					continue;
				}
				if (now - since < ReturnMinutes * 60f) continue;
				if (Bring(e, raft.Value, why)) awaySince.Remove(e.Id);
				else awaySince[e.Id] = now - ReturnMinutes * 60f + RetrySeconds;
			}
		}

		/// <summary>An "again" island (Why = "again"): while it is near the players (loaded) or not yet seen by the clock its
		/// mark is today and the km sailed now; once it is left behind it comes back when the rule's number of days / km
		/// has passed since then (no ReturnMinutes, no cap).</summary>
		static void TickAgain(IslandWorldState.Entry e, Vector3 raft, bool away)
		{
			awaySince.Remove(e.Id);
			IntroRule r = RuleThatBrought(e);
			ObjectState mark;
			if (!e.State.TryGetValue(AgainKey, out mark) || !away)
			{
				if (mark == null) { mark = new ObjectState { Active = true }; e.State[AgainKey] = mark; }
				mark.Day = WorldDirector.Today;
				mark.Yield = (int)WorldDirector.Sailed;
				return;
			}
			if (r == null || Due(r, mark) > 0f) return;
			if (Bring(e, raft, "again")) { mark.Day = WorldDirector.Today; mark.Yield = (int)WorldDirector.Sailed; }
		}

		/// <summary>How many days (day rule) or km (km rule) are left until an "again" island comes back; 0 or less = now.</summary>
		static float Due(IntroRule r, ObjectState mark)
		{
			float every, n;
			every = float.TryParse(r.WhenArg, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? Mathf.Max(1f, n) : 1f;
			if (r.When == "day") return mark.Day + every - WorldDirector.Today;
			return (mark.Yield + every * 1000f - WorldDirector.Sailed) / 1000f;
		}

		/// <summary>Why the players still need this island - "awaited" (something waits for it), "again" (a km / day rule with
		/// again brought it and it isn't finished) or "quest" (begun and not done) - or null when it doesn't come back.</summary>
		public static string Why(IslandWorldState.Entry e)
		{
			if (e == null || e.Failed || e.WaitingForFile || WorldRandomizer.IsExtras(e)) return null;
			IntroRule brought = RuleThatBrought(e);
			if (brought != null && brought.Where == "receiver") return null;
			if (Awaited(e)) return "awaited";
			if (brought != null && brought.Repeats) return Finished(e) ? null : "again";
			int steps = IslandQuest.From(IslandCache.PropsOf(e)).Steps.Count, step = QuestTracker.StepOf(e);
			bool begun = e.State.ContainsKey(WorldDirector.VisitKey) || step > 0;
			if (steps == 0 || step >= steps || !begun) return null;
			return Returns(e) < MaxReturns ? "quest" : null;
		}

		/// <summary>Whether the players are done with this island: its quest is done, or - an island without a quest - they
		/// reached it. A finished island never comes again as a random island (the user, 2026-10-04: "finished islands
		/// don't show up again, unfinished/started islands can").</summary>
		public static bool Finished(IslandWorldState.Entry e)
		{
			if (e == null) return false;
			// (never reached and no step done: not finished whatever its quest - without reading its file, which the spawn
			// pool's first pick did for every island the world has, IM43)
			if (!e.State.ContainsKey(WorldDirector.VisitKey) && QuestTracker.StepOf(e) <= 0) return false;
			int steps = IslandQuest.From(IslandCache.PropsOf(e)).Steps.Count;
			if (steps > 0) return QuestTracker.StepOf(e) >= steps;
			return e.State.ContainsKey(WorldDirector.VisitKey);
		}

		/// <summary>Host: the spawn pool picked an island this world already has, not finished and left behind: it comes
		/// back ahead of the raft as it was (quest, chests and harvest kept) instead of a second, fresh copy.</summary>
		public static bool BringAgain(IslandWorldState.Entry e, Vector3 raft)
		{
			return Bring(e, raft, Why(e) ?? "again");
		}

		public static int Returns(IslandWorldState.Entry e)
		{
			ObjectState r;
			return e != null && e.State.TryGetValue(ReturnsKey, out r) ? r.Yield : 0;
		}

		internal static IntroRule RuleThatBrought(IslandWorldState.Entry e)
		{
			if (string.IsNullOrEmpty(e.Rule)) return null;
			IntroRule r = WorldDirector.Plan != null ? WorldDirector.Plan.Rules.FirstOrDefault(x => x.Id.Equals(e.Rule, StringComparison.OrdinalIgnoreCase)) : null;
			if (r != null) return r;
			foreach (IslandWorldState.Entry owner in IslandWorldState.Islands)
			{
				r = WorldDirector.RulesOf(owner).FirstOrDefault(x => x.Id.Equals(e.Rule, StringComparison.OrdinalIgnoreCase));
				if (r != null) return r;
			}
			// (a player who joined: the plan's rules from the host's copy of the world file)
			if (WorldCopy.HostLines != null)
				foreach (string l in WorldCopy.HostLines)
					if (l.StartsWith("@planrule="))
					{
						r = IntroRule.Parse(l.Substring(10));
						if (r != null && r.Id.Equals(e.Rule, StringComparison.OrdinalIgnoreCase)) return r;
					}
			return null;
		}

		/// <summary>SL9: the blueprint an island's rule needs before its Receiver dot shows ("" = none), cached a second.</summary>
		internal static string NeedsOf(IslandWorldState.Entry e)
		{
			if (e == null || string.IsNullOrEmpty(e.Rule)) return "";
			float t;
			string v;
			if (needsAt.TryGetValue(e.Id, out t) && Time.time - t < 1f && needsOf.TryGetValue(e.Id, out v)) return v;
			try { IntroRule r = RuleThatBrought(e); v = r != null ? r.Needs : ""; } catch { v = ""; }
			needsOf[e.Id] = v; needsAt[e.Id] = Time.time;
			return v;
		}

		static readonly Dictionary<int, float> needsAt = new Dictionary<int, float>();
		static readonly Dictionary<int, string> needsOf = new Dictionary<int, string>();

		/// <summary>Whether a rule that hasn't fired yet waits for something to happen on this island.</summary>
		static bool Awaited(IslandWorldState.Entry e)
		{
			if (WorldDirector.Plan != null)
				foreach (IntroRule r in WorldDirector.Plan.Rules)
				{
					if (r.StoryPlace.StartsWith("after:", StringComparison.OrdinalIgnoreCase))
					{
						// (the Receiver chain: an island the next one comes after, until the chain counts it done)
						string after = r.StoryPlace.Substring(6).Trim();
						if (StoryOrder.Parse(after) == ChunkPointType.None && WorldDirector.Refs(after, null).Contains(e) && !StoryChain.Done.Contains(StoryChain.RuleKey(e.Rule))) return true;
						continue;
					}
					if (!WorldDirector.Done.Contains(r.Id) && !r.Special && WaitsFor(r, null, e)) return true;
				}
			foreach (IslandWorldState.Entry owner in IslandWorldState.Islands)
			{
				List<IntroRule> rules = WorldDirector.RulesOf(owner);
				for (int i = 0; i < rules.Count; i++)
					if (!owner.State.ContainsKey(WorldDirector.RuleKeyBase + i) && WaitsFor(rules[i], owner, e)) return true;
			}
			return false;
		}

		static bool WaitsFor(IntroRule r, IslandWorldState.Entry owner, IslandWorldState.Entry e)
		{
			if (r.When != "quest" && r.When != "step" && r.When != "zone" && r.When != "visit" && r.When != "signal") return false;
			return WorldDirector.Refs(r.WhenRef, owner).Contains(e) && !WorldDirector.Happened(r, e);
		}

		/// <summary>Moves the island to a free spot ahead of the raft and tells everyone. False: no room there yet.</summary>
		static bool Bring(IslandWorldState.Entry e, Vector3 raft, string why)
		{
			float radius = Mathf.Max(10f, CustomIslandSpawner.LandRadius(e.Name));
			Vector3? spot = CustomIslandSpawner.SpotAhead(raft, radius, e.Position.y, e);
			if (!spot.HasValue) { Debug.Log("[CUSTOM ISLANDS] [returning] '" + e.HostName + "' can't come back yet: no free spot ahead of the raft"); return false; }
			float behind = Flat(e.Position - raft);
			e.Position = spot.Value;
			int returned = Returns(e) + 1;
			e.State[ReturnsKey] = new ObjectState { Active = true, Yield = returned, Day = WorldDirector.Today };
			// (the other players' copies move too: a known island at another place)
			IslandNetwork.BroadcastAdded(e);
			string title = TitleOf(e);
			string message = why == "awaited" ? "Back in sight: the way on starts there." : why == "quest" ? "Back in sight: you left its quest unfinished." :
				why == "again" ? "Back in sight: it still waits for you." : "Back in sight.";
			IslandNetwork.SendAnnounce(e, title, message);
			WorldDirector.Show(title, message, e.Position);
			Debug.Log("[CUSTOM ISLANDS] [returning] '" + e.HostName + "' comes back " + Flat(e.Position - raft).ToString("F0") + " m ahead of the raft (left " +
				behind.ToString("F0") + " m behind; " + (why == "awaited" ? "something waits for it" : why == "quest" ? "its quest is unfinished" : why == "again" ? "its rule brings it again until it is done" : "picked again by the spawn pool") + "; return " + returned + ")");
			if (Returned != null) Returned(e);
			return true;
		}

		/// <summary>Each island as the clock sees it: why it is needed, how far it is (from the raft, to its land's edge), away
		/// or not, and how long it has waited (tests, CIReturnState).</summary>
		public static IEnumerable<string> Describe()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.ToList())
			{
				float edge = Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name)), far = raft.HasValue ? Flat(e.Position - raft.Value) : -1f;
				float since;
				yield return "'" + e.HostName + "': " + (Why(e) ?? "not needed") + ", " + far.ToString("F0") + " m from the raft, land radius " + edge.ToString("F0") +
					(e.Root != null ? ", loaded" : e.Loading ? ", loading" : "") + (awaySince.TryGetValue(e.Id, out since) ? ", away for " + (Time.time - since).ToString("F0") + " s" : "") +
					(e.State.ContainsKey(AgainKey) && RuleThatBrought(e) != null ? ", again in " + Due(RuleThatBrought(e), e.State[AgainKey]).ToString("0.#", CultureInfo.InvariantCulture) + (RuleThatBrought(e).When == "day" ? " day(s)" : " km") : "") +
					", came back " + Returns(e) + "x";
			}
			yield return "host " + Raft_Network.IsHost + ", return after " + ReturnMinutes.ToString("0.##") + " min, unload distance " + WorldRules.UnloadDistance.ToString("F0") + " m, next look in " + (nextTick - Time.unscaledTime).ToString("F1") + " s";
		}

		static string TitleOf(IslandWorldState.Entry e)
		{
			if (!string.IsNullOrEmpty(e.Label)) return e.Label;
			string title = ObjectProps.Get(IslandCache.PropsOf(e), IslandProps.Title);
			return title.Length > 0 ? title : e.HostName;
		}

		static float Flat(Vector3 v) { return new Vector2(v.x, v.z).magnitude; }
	}
}
