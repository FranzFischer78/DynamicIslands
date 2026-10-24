using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Check in the World Plans window: goes through a plan (or an island's own rules) the way a world will play it and
	/// says what can't work, what may not work as meant, and what is good to know - each with the rule, why and how to fix
	/// it. It looks inside the islands too: their quests step by step (is there a zone, note, chest or creature of that
	/// name for each step?), their zones and signals, and, for new map-type islands, a sample island of that type.
	/// </summary>
	public static class PlanChecker
	{
		public enum Level { Problem, Warning, Tip }

		public class Finding
		{
			/// <summary>The rule's index (-1 = the plan as a whole).</summary>
			public int Rule = -1;
			public Level Level;
			public string Text = "", Fix = "";
			public Finding(int rule, Level level, string text, string fix = "") { Rule = rule; Level = level; Text = text; Fix = fix ?? ""; }
		}

		/// <summary>What an island has that rules and quests can point at.</summary>
		public class Facts
		{
			public string Name = "";
			/// <summary>A sample of a map type (a new island made in each world), not a saved file.</summary>
			public bool Sample;
			public IslandQuest Quest = new IslandQuest();
			/// <summary>The island's other quests (Quest 2 is [0]): a rule may wait for one of them (ROADMAP CB6).</summary>
			public List<IslandQuest> MoreQuests = new List<IslandQuest>();
			public readonly HashSet<string> Zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				Notes = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Chests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			public readonly Dictionary<string, int> Creatures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			public readonly HashSet<string> CreaturesComeBack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			public int NoteCount, ChestCount, RaftCrates, Objects;
			/// <summary>Journal pages the island can give: notes with text (an empty note gives none) and "journal" actions.</summary>
			public int PageCount;
			/// <summary>The island's own rules: plan rules may point at the islands they bring by their names.</summary>
			public List<IntroRule> Rules = new List<IntroRule>();
			public string AllText = ""; // (every setting's value: to see whether a story item is given anywhere)
			/// <summary>Story items the island's checks want (has/take story:...) and those it gives (loot, zones, give actions,
			/// Raft's quest item pickups) - the plan's order of them (Check).</summary>
			public readonly HashSet<string> NeedsStory = new HashSet<string>(StringComparer.OrdinalIgnoreCase), GivesStory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			/// <summary>Raft's tools its features want (RaftFeatures): tool item -> the features wanting it ("vines", "buried
			/// treasure"...). MainNeeds: "tool|feature" pairs its main quest can't do without (vines in front of the chest a step
			/// opens); the others are optional finds - players can come back with the tool.</summary>
			public readonly Dictionary<string, List<string>> NeedsTools = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			public readonly HashSet<string> MainNeeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			/// <summary>"tool|feature" pairs that are advice (RaftFeatures.Feature.Advisory: a dark place, a long dive).</summary>
			public readonly HashSet<string> Advice = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			/// <summary>Codes its code panels want (lock.code -> what the panel is) and the text players read on it (notes,
			/// messages, journal pages - not the panels' own codes): a code is given by an island whose text holds it.</summary>
			public readonly Dictionary<string, string> Codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			public string ReadText = "";
			/// <summary>Raft's tools the island gives: the item or its blueprint in a chest's loot, an action or the quest's reward.</summary>
			public readonly HashSet<string> GivesTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			/// <summary>The quest traps of guide 6.2 found on the island (AU47): texts in lower case, Check puts the island before them.</summary>
			public readonly List<Finding> Traps = new List<Finding>();
			public string Describe { get { return Sample ? "a new " + Name + " island (checked on a sample of that map type)" : "'" + Name + "'"; } }
		}

		#region Facts about islands

		static readonly Dictionary<string, KeyValuePair<DateTime, Facts>> saved = new Dictionary<string, KeyValuePair<DateTime, Facts>>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, Facts> samples = new Dictionary<string, Facts>(StringComparer.OrdinalIgnoreCase);

		public static Facts FromFile(IslandFile f, string name, bool sample)
		{
			var x = new Facts { Name = name, Sample = sample, Quest = IslandQuest.From(f.Props), Objects = f.Objects.Count, Rules = WorldDirector.RulesFromProps(f.Props) };
			for (int q = 1; q < IslandQuest.CountIn(f.Props); q++) x.MoreQuests.Add(IslandQuest.From(f.Props, q));
			var text = new System.Text.StringBuilder();
			foreach (var kv in f.Props) if (kv.Key != StoryItems.Key) text.Append(kv.Value).Append('\n');
			var read = new System.Text.StringBuilder(text.ToString());
			// (creatures come back by default, after the island's regrow days - unless the island says never)
			int regrow;
			bool islandNever = int.TryParse(ObjectProps.Get(f.Props, IslandProps.RegrowDays), out regrow) && regrow <= 0;
			foreach (IslandObject o in f.Objects)
			{
				IDictionary<string, string> p = o.Props ?? new Dictionary<string, string>();
				if (o.Name == ContentCatalog.TriggerZone) { string z = ObjectProps.Get(p, ObjectProps.ZoneId); if (z.Length > 0) x.Zones.Add(z); }
				if (ObjectProps.IsNote(o.Name, p))
				{
					x.NoteCount++;
					string t = ObjectProps.Get(p, ObjectProps.NoteTitle); if (t.Length > 0) x.Notes.Add(t);
					if (ObjectProps.Get(p, ObjectProps.NoteText).Trim().Length > 0) x.PageCount++;
				}
				if (ObjectProps.IsLoot(o.Name, p)) { x.ChestCount++; string t = ObjectProps.Get(p, ObjectProps.NoteTitle); if (t.Length > 0) x.Chests.Add(t); }
				if (o.Name == PlaceableCatalog.RaftCrate) x.RaftCrates++;
				ContentCatalog.CreatureKind k = ContentCatalog.CreatureOf(o.Name);
				if (k != null)
				{
					int n; if (!int.TryParse(ObjectProps.Get(p, ObjectProps.CreatureCount), out n) || n < 1) n = 1;
					int had; x.Creatures.TryGetValue(k.Label, out had); x.Creatures[k.Label] = had + n;
					if (ObjectProps.Respawns(p) && !islandNever) x.CreaturesComeBack.Add(k.Label);
				}
				string code = ObjectProps.Get(p, CodeLock.Code).Trim();
				if (code.Length > 0 && !x.Codes.ContainsKey(code)) x.Codes[code] = Thing(o, p);
				foreach (var kv in p)
				{
					text.Append(kv.Value).Append('\n');
					if (kv.Key != CodeLock.Code) read.Append(kv.Value).Append('\n');
					if (kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix))
						foreach (ObjAction a in ObjAction.ParseLines(kv.Value))
						{
							if (a.Verb == "signal" && a.Arg.Trim().Length > 0) x.Signals.Add(a.Arg.Trim());
							if (a.Verb == "journal") x.PageCount++;
						}
				}
			}
			foreach (var kv in f.Props.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix)))
				foreach (ObjAction a in ObjAction.ParseLines(kv.Value))
				{
					if (a.Verb == "signal" && a.Arg.Trim().Length > 0) x.Signals.Add(a.Arg.Trim());
					if (a.Verb == "journal") x.PageCount++;
				}
			x.AllText = text.ToString();
			x.ReadText = read.ToString();
			x.GivesTools.UnionWith(RaftFeatures.GivenIn(x.AllText));
			FindTools(f, x);
			// Story items: wanted by checks, given by everything else (loot, zone items, give actions) and by Raft's pickups
			var owners = new List<List<KeyValuePair<string, string>>> { f.Props.Where(kv => kv.Key != StoryItems.Key).ToList() };
			foreach (IslandObject o in f.Objects)
			{
				if (o.Props != null) owners.Add(o.Props.ToList());
				if (QuestItemPickups.IsModel(o.Name)) { string id = QuestItemPickups.StoryId(o.Name); if (id != null) x.GivesStory.Add(id); }
			}
			// (an "any" check is open by any one of its lines: none of its story items is wanted when one of its lines waits for
			// a signal something else on the island sends - Raft's radio runs on the island's generator as well as a battery; a
			// generator's own "power" is only its running state, so it still wants its part)
			List<HashSet<string>> sends = owners.Select(SignalsSent).ToList();
			var senders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (HashSet<string> s in sends) foreach (string sig in s) { int had; senders.TryGetValue(sig, out had); senders[sig] = had + 1; }
			for (int i = 0; i < owners.Count; i++)
			foreach (var kv in owners[i])
			{
				bool check = kv.Key.StartsWith(BehaviourProps.CheckPrefix);
				if (check && OpenBySignal(kv.Value, sends[i], senders)) continue;
				foreach (string line in (kv.Value ?? "").Split('\n'))
				{
					if (check)
					{
						System.Text.RegularExpressions.Match m = NeedRx.Match(line.Trim());
						if (m.Success) x.NeedsStory.Add(m.Groups[1].Value);
					}
					else foreach (System.Text.RegularExpressions.Match m in GiveRx.Matches(line)) x.GivesStory.Add(m.Groups[1].Value);
				}
			}
			// (a story item that is one of Raft's tools - the bolt cutters - shows as that tool too)
			foreach (string g in x.GivesStory) { RaftFeatures.Tool st = RaftFeatures.ToolOfStory(g); if (st != null) x.GivesTools.Add(st.Item); }
			if (!sample) FindTraps(f, x, islandNever);
			return x;
		}

		static HashSet<string> SignalsSent(List<KeyValuePair<string, string>> props)
		{
			var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in props)
				if (kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix))
					foreach (ObjAction a in ObjAction.ParseLines(kv.Value))
						if (a.Verb == "signal" && a.Arg.Trim().Length > 0) s.Add(a.Arg.Trim());
			return s;
		}

		/// <summary>An "any" check (ObjCheck.AnyLine first) with a line waiting for a signal another of the island's objects sends.</summary>
		static bool OpenBySignal(string check, HashSet<string> own, Dictionary<string, int> senders)
		{
			string[] lines = (check ?? "").Split('\n');
			if (lines[0].Trim() != ObjCheck.AnyLine) return false;
			foreach (string l in lines.Skip(1))
			{
				string[] w = l.Trim().Split('|');
				if (w.Length < 2 || w[0] != "signal" || w[1].Trim().Length == 0) continue;
				int n; senders.TryGetValue(w[1].Trim(), out n);
				if (n - (own.Contains(w[1].Trim()) ? 1 : 0) > 0) return true;
			}
			return false;
		}

		/// <summary>
		/// The island's features that want Raft's tools (RaftFeatures: the vines, buried treasure, dirt, ziplines, and any
		/// object whose "Only if..." wants one of the tools), and which of them its main quest can't do without: the feature
		/// is what a step wants, or it shows or opens what a step wants, or it gives the story item a step collects. Buried
		/// treasure and dirt are always optional finds (Raft's treasure loot, dirt: no step points at them).
		/// </summary>
		static void FindTools(IslandFile f, Facts x)
		{
			var named = new Dictionary<string, IslandObject>(StringComparer.OrdinalIgnoreCase);
			foreach (IslandObject o in f.Objects)
			{
				string n = o.Props != null ? ObjectProps.Get(o.Props, BehaviourProps.Name).Trim() : "";
				if (n.Length > 0 && !named.ContainsKey(n)) named[n] = o;
			}
			List<IslandQuest.Step> steps = x.Quest.Steps;
			Func<IslandObject, bool> stepWants = o =>
			{
				IDictionary<string, string> p = o.Props ?? new Dictionary<string, string>();
				string title = ObjectProps.Get(p, ObjectProps.NoteTitle).Trim();
				foreach (IslandQuest.Step st in steps)
				{
					string t = st.Target.Trim();
					if (t.Length == 0) continue;
					if (st.Type == "open" && ObjectProps.IsLoot(o.Name, p) && t.Equals(title, StringComparison.OrdinalIgnoreCase)) return true;
					if (st.Type == "read" && ObjectProps.IsNote(o.Name, p) && t.Equals(title, StringComparison.OrdinalIgnoreCase)) return true;
					if (st.Type == "reach" && o.Name == ContentCatalog.TriggerZone && t.Equals(ObjectProps.Get(p, ObjectProps.ZoneId).Trim(), StringComparison.OrdinalIgnoreCase)) return true;
					// (a chest holding the story item a step collects)
					if (st.Type == "collect" && ObjectProps.IsLoot(o.Name, p) && ObjectProps.Get(p, ObjectProps.LootItems).IndexOf(StoryItems.Ref(StoryItems.IdOf(t)) + "*", StringComparison.OrdinalIgnoreCase) >= 0) return true;
				}
				return false;
			};
			Action<string, string, bool, bool> need = (t, what, main, advice) =>
			{
				List<string> list;
				if (!x.NeedsTools.TryGetValue(t, out list)) x.NeedsTools[t] = list = new List<string>();
				if (!list.Contains(what)) list.Add(what);
				if (main) x.MainNeeds.Add(t + "|" + what);
				if (advice) x.Advice.Add(t + "|" + what);
			};
			foreach (IslandObject o in f.Objects)
			{
				IDictionary<string, string> p = o.Props ?? new Dictionary<string, string>();
				RaftFeatures.Feature feature = RaftFeatures.Of(o.Name, p);
				var tools = new List<string>(feature != null ? feature.Needs : new string[0]);
				string checks = string.Join("\n", p.Where(kv => kv.Key.StartsWith(BehaviourProps.CheckPrefix)).Select(kv => kv.Value ?? "").ToArray());
				foreach (ObjCheck ch in ObjCheck.ParseLines(checks))
				{
					RaftFeatures.Tool t = ch.IsItem && !ch.Not ? RaftFeatures.ToolOf(ch.Target) : null;
					if (t != null && t.Story == null && !tools.Contains(t.Item)) tools.Add(t.Item);
				}
				// (a story tool its own check wants - the cage's bolt cutters - is followed as the story item: CheckStoryOrder)
				tools.RemoveAll(t => { RaftFeatures.Tool rt = RaftFeatures.ToolOf(t); return rt != null && rt.Story != null && checks.IndexOf(StoryItems.Ref(rt.Story), StringComparison.OrdinalIgnoreCase) >= 0; });
				if (tools.Count == 0) continue;
				string what = feature != null ? feature.Label : Thing(o, p);
				bool main = feature != null && feature.Zone ? f.Objects.Any(y => y != o && stepWants(y) && (y.Position - o.Position).magnitude <= ObjectProps.Radius(p))
					: (feature == null || !feature.Optional) && QuestWants(o, p, named, stepWants, steps);
				foreach (string t in tools) need(t, what, main, feature != null && feature.Advisory && feature.Needs.Contains(t));
			}
			// Deep dives (G14): what a step wants, far below the sea - oxygen or flippers (advice)
			string dive = RaftFeatures.OxygenItem + "/" + RaftFeatures.FlipperItem;
			foreach (IslandObject o in f.Objects.Where(o => stepWants(o) && f.WaterLevel - o.Position.y >= RaftFeatures.DeepMetres))
				need(dive, Thing(o, o.Props ?? new Dictionary<string, string>()) + ", " + Mathf.RoundToInt(f.WaterLevel - o.Position.y) + " m under the sea", true, true);
			// Steps that name a tool (look through the binoculars, the headlight in the dark): that tool
			foreach (IslandQuest.Step st in steps)
			{
				string words = (st.Text + " " + st.Target).ToLowerInvariant();
				if (words.Contains("binocular")) need(RaftFeatures.BinocularsItem, "a lookout step", true, false);
				if (words.Contains("headlight") || words.Contains("head light") || words.Contains("head lamp")) need(RaftFeatures.HeadLightItem, "a step in the dark", true, false);
			}
		}

		/// <summary>Does the main quest want this object, or what its actions show, open or give?</summary>
		static bool QuestWants(IslandObject o, IDictionary<string, string> p, Dictionary<string, IslandObject> named, Func<IslandObject, bool> stepWants, List<IslandQuest.Step> steps)
		{
			if (steps.Count == 0) return false;
			if (stepWants(o)) return true;
			foreach (var kv in p.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix)))
				foreach (ObjAction a in ObjAction.ParseLines(kv.Value))
				{
					IslandObject y;
					if ((a.Verb == "show" || a.Verb == "open") && a.Target.Length > 0 && named.TryGetValue(a.Target, out y) && y != o && stepWants(y)) return true;
					if (a.Verb == "give" && steps.Any(st => st.Type == "collect" && st.Target.Trim().Length > 0 && a.Arg.IndexOf(StoryItems.Ref(StoryItems.IdOf(st.Target.Trim())) + "*", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
				}
			return false;
		}

		static readonly System.Text.RegularExpressions.Regex NeedRx = new System.Text.RegularExpressions.Regex(@"^(?:has|take)\|" + StoryItems.Prefix + @"([^|;*\s]+)");
		static readonly System.Text.RegularExpressions.Regex GiveRx = new System.Text.RegularExpressions.Regex(StoryItems.Prefix + @"([^|;*\s]+)");

		/// <summary>A saved island's facts (read again when its file changed), or null if there is no such island.</summary>
		public static Facts Saved(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;
			string path = IslandSpawner.PathFor(name);
			if (!File.Exists(path)) return null;
			try
			{
				DateTime t = File.GetLastWriteTimeUtc(path);
				KeyValuePair<DateTime, Facts> had;
				if (saved.TryGetValue(name, out had) && had.Key == t) return had.Value;
				Facts f = FromFile(IslandFile.Load(path), name, false);
				saved[name] = new KeyValuePair<DateTime, Facts>(t, f);
				return f;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Check: could not read '" + name + "': " + e.Message); return null; }
		}

		/// <summary>A map type's facts from a sample island of it (made once: deep = allowed to make it now), or null.</summary>
		public static Facts Sample(string type, bool deep)
		{
			Facts f;
			if (samples.TryGetValue(type ?? "", out f)) return f;
			if (!deep || !PlaceableCatalog.IsBuilt) return null;
			MapType t = MapTypes.Get(type);
			if (t == null) return null;
			try
			{
				float elevation;
				IslandGenSettings s = MapTypes.Roll(t, new System.Random(12345), out elevation);
				f = FromFile(MapTypes.Create(t, s, elevation, "check-" + t.Name), t.Label.ToLowerInvariant(), true);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Check: could not make a sample '" + type + "' island: " + e.Message); f = null; }
			samples[type] = f;
			return f;
		}

		#endregion

		#region The check

		class Ctx
		{
			public WorldPlan Plan;
			public bool IslandMode, Deep;
			public Facts Own; // (island mode: the island being edited)
			public List<Finding> Out = new List<Finding>();
			public HashSet<string> SavedNames;
			/// <summary>Rules on the islands the plan brings (and on the islands those bring), by name, with their island: a
			/// world finds the island one of them brought by the rule's name, as it does a plan rule's.</summary>
			public Dictionary<string, KeyValuePair<IntroRule, Facts>> IslandRules = new Dictionary<string, KeyValuePair<IntroRule, Facts>>(StringComparer.OrdinalIgnoreCase);
			public void Add(int rule, Level level, string text, string fix = "") { Out.Add(new Finding(rule, level, text, fix)); }
		}

		/// <summary>The rules of the islands a plan brings, followed through what those rules bring (not the plan's own names).</summary>
		static void FindIslandRules(Ctx c)
		{
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var queue = new Queue<Facts>(c.Plan.Rules.SelectMany(r => Brings(c, r)));
			while (queue.Count > 0)
			{
				Facts f = queue.Dequeue();
				if (f == null || !seen.Add((f.Sample ? "type:" : "") + f.Name)) continue;
				foreach (IntroRule ir in f.Rules)
				{
					int same = ir.Id.Length > 0 ? c.Plan.Rules.FindIndex(x => x.Id.Equals(ir.Id, StringComparison.OrdinalIgnoreCase)) : -1;
					// (a world finds an island by the name of the rule that brought it, any rule's: the two are mixed up)
					if (same >= 0 && !f.Sample)
						c.Add(same, Level.Warning, R(c, same) + " has the same name as a rule of " + f.Describe + " (its own rules): what waits for or is placed near '" + ir.Id + "' may take the island that one brings.", "Give rule " + (same + 1) + " another name.");
					if (ir.Id.Length == 0 || c.IslandRules.ContainsKey(ir.Id) || same >= 0) continue;
					c.IslandRules[ir.Id] = new KeyValuePair<IntroRule, Facts>(ir, f);
					foreach (Facts g in Brings(c, ir)) queue.Enqueue(g);
				}
			}
		}

		static string R(Ctx c, int i) { IntroRule r = c.Plan.Rules[i]; return "Rule " + (i + 1) + (r.Id.Length > 0 ? " '" + r.Id + "'" : ""); }

		/// <summary>Everything Check finds, problems first. deep: also make sample islands of the map types used (a moment each, once).</summary>
		public static List<Finding> Check(WorldPlan plan, bool islandMode, bool deep, Facts own = null)
		{
			var c = new Ctx { Plan = plan, IslandMode = islandMode, Deep = deep, Own = own, SavedNames = new HashSet<string>(IslandSpawner.ListSavedIslands(), StringComparer.OrdinalIgnoreCase) };
			if (!islandMode) FindIslandRules(c);
			List<IntroRule> rules = plan.Rules;
			if (rules.Count == 0)
			{
				if (islandMode) c.Add(-1, Level.Tip, "This island has no rules of its own: it brings no other islands.", "+ Add a rule, e.g. \"when this island's quest is done, bring a new island near it\".");
				else if (!plan.Random && !plan.RaftStory) c.Add(-1, Level.Problem, "The plan has no rules, random islands are off and Raft's story is off: a world with it gets no islands at all besides Raft's ordinary ones.", "+ Add a rule, or switch random islands on.");
				else c.Add(-1, Level.Tip, "The plan has no rules yet: worlds get " + (plan.Random ? "random islands while sailing" : "no custom islands") + (plan.RaftStory ? " and Raft's story" : "") + ".", "+ Add a rule, or Templates... for a ready-made set.");
			}
			var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < rules.Count; i++)
			{
				IntroRule r = rules[i];
				if (r.Id.Length == 0) c.Add(i, Level.Problem, R(c, i) + " has no name: other rules can't point at its island.", "Type a name in the Name field (e.g. camp).");
				else if (ids.ContainsKey(r.Id)) c.Add(i, Level.Problem, R(c, i) + " has the same name as rule " + (ids[r.Id] + 1) + ": rules that point at '" + r.Id + "' can't tell them apart.", "Give each rule its own name.");
				else ids[r.Id] = i;
			}
			for (int i = 0; i < rules.Count; i++)
			{
				CheckWhen(c, i);
				CheckBring(c, i);
				CheckWhere(c, i);
				CheckTell(c, i);
				if (!islandMode) CheckStory(c, i);
			}
			CheckTraps(c);
			CheckOrder(c);
			if (!islandMode && rules.Count > 0 && !plan.Random && !rules.Any(r => r.When == "start" || r.When == "km" || r.When == "day" || r.Where == "receiver"))
				c.Add(-1, Level.Warning, "Nothing of the plan comes by itself: no rule starts with the world, a distance or a day, and random islands are off. Every rule waits for another island - so none ever comes.",
					"Let the first rule be \"When the world starts\" (or after a distance / on a day).");
			if (islandMode && rules.Any(x => x.Special)) c.Add(-1, Level.Problem, "An island's own rules can't use Raft's story or the Receiver (a world plan can).", "Use \"Ahead of the raft\" or \"Near an island\", or make these rules in a world plan.");
			foreach (string tip in StoryTips(plan, islandMode)) c.Add(-1, tip.StartsWith("Story:") ? Level.Tip : Level.Tip, tip);
			if (!islandMode)
			{
				Progress p = Progression(c);
				CheckBlueprints(c, p);
				CheckBlueprintOrder(c, p);
				CheckTools(c, p);
				CheckStoryOrder(c);
				CheckCodes(c);
				CheckStoryClash(c);
			}
			return c.Out.OrderBy(f => f.Level).ThenBy(f => f.Rule).ToList();
		}

		/// <summary>
		/// Story items in play order (TODO "Raft's quest items used in the plans"; PlayOrder): an island whose doors or chests
		/// want a story item (a keycard, a key...) that neither it nor any island met before it gives - the player reaches the
		/// lock without the key. Raft's own quest items (raft-...) are given only by their pickups on custom islands: the ones
		/// found on Raft's story islands open only the island they were found on (the user, 2026-10-16), never the plan's locks.
		/// </summary>
		static void CheckStoryOrder(Ctx c)
		{
			var given = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			// (Raft's story islands give nothing here: their quest items are Raft's, for their own doors)
			List<Stop> stops = PlayOrder(c.Plan).Where(st => st.Rule >= 0).ToList();
			List<List<Facts>> at = stops.Select(st => Brings(c, c.Plan.Rules[st.Rule]).ToList()).ToList();
			for (int n = 0; n < stops.Count; n++)
			{
				int i = stops[n].Rule;
				foreach (Facts f in at[n]) given.UnionWith(f.GivesStory);
				foreach (Facts f in at[n])
					foreach (string need in f.NeedsStory.Where(x => !given.Contains(x)))
					{
						int later = Enumerable.Range(n + 1, stops.Count - n - 1).FirstOrDefault(m => at[m].Any(g => g.GivesStory.Contains(need)));
						RaftFeatures.Tool tool = RaftFeatures.ToolOfStory(need);
						string what = tool != null ? "the " + tool.Label + " (story item " + StoryItems.Label(need) + ")" : "the story item " + StoryItems.Label(need);
						c.Add(i, Level.Warning, R(c, i) + " brings " + f.Describe + ": a lock there wants " + what + RaftsOwn(need) +
							(later > n ? ", which only " + StopName(c, stops[later]) + " gives, later in play: players get there without it." : ", which no island of the plan gives: it stays locked."),
							"Give it on an island met earlier in the plan (a chest's loot, a zone's items, an action, or one of Raft's quest item pickups), or on this island before the lock.");
					}
			}
		}

		static readonly string[] DigitWords = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

		/// <summary>
		/// Whether a text tells a code: written whole, split by separators ("3-1-2", "3 1 2", "4.3.1.2"), or - for a code of
		/// digits - spelled out as digit words in order across the text (three logs saying FOUR, ONE, NINE tell 419).
		/// </summary>
		static bool TellsCode(string text, string code)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(code)) return false;
			var rx = System.Text.RegularExpressions.Regex.Escape(code);
			var opt = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
			if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![A-Za-z0-9])" + rx + @"(?![A-Za-z0-9])", opt)) return true;
			if (!code.All(ch => ch >= '0' && ch <= '9') || code.Length < 2) return false;
			string sep = @"[\s.,;:/\-]+";
			if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![A-Za-z0-9])" + string.Join(sep, code.Select(ch => ch.ToString()).ToArray()) + @"(?![A-Za-z0-9])", opt)) return true;
			// digit words and single digits, in order of appearance
			var seq = new List<char>();
			foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"[A-Za-z]+|[0-9]"))
			{
				int d = m.Value.Length == 1 && char.IsDigit(m.Value[0]) ? m.Value[0] - '0' : Array.IndexOf(DigitWords, m.Value.ToLowerInvariant());
				if (d >= 0) seq.Add((char)('0' + d));
			}
			int at = 0;
			foreach (char ch in seq) if (at < code.Length && ch == code[at]) at++;
			return at == code.Length;
		}

		/// <summary>
		/// Codes in play order (2026-10-24): a code panel (lock.code) whose code no text of its island or of an island met
		/// before it holds - a note, a message, a journal page - can only be guessed. Named: the first later island whose text
		/// has it.
		/// </summary>
		static void CheckCodes(Ctx c)
		{
			List<Stop> stops = PlayOrder(c.Plan).Where(st => st.Rule >= 0).ToList();
			List<List<Facts>> at = stops.Select(st => Brings(c, c.Plan.Rules[st.Rule]).ToList()).ToList();
			Func<Facts, string, bool> tells = (f, code) => TellsCode(f.ReadText, code);
			for (int n = 0; n < stops.Count; n++)
				foreach (Facts f in at[n])
					foreach (var kv in f.Codes)
					{
						if (Enumerable.Range(0, n + 1).Any(m => at[m].Any(g => tells(g, kv.Key)))) continue;
						int later = Enumerable.Range(n + 1, stops.Count - n - 1).FirstOrDefault(m => at[m].Any(g => tells(g, kv.Key)));
						c.Add(stops[n].Rule, Level.Warning, R(c, stops[n].Rule) + " brings " + f.Describe + ": its " + kv.Value + " wants the code " + kv.Key + ", and " +
							(later > n ? "only " + StopName(c, stops[later]) + " tells it, later in play: players reach the panel without it." : "no note or message of the plan tells it: players can only guess."),
							"Put the code in a note or message on this island or on one met before it.");
					}
		}

		/// <summary>For one of Raft's quest items (raft-Vasagatan_KeyCardOffice): Raft's own, on its story island, opens only that island.</summary>
		static string RaftsOwn(string id)
		{
			if (!id.StartsWith(QuestItemPickups.IdPrefix, StringComparison.OrdinalIgnoreCase)) return "";
			ChunkPointType t = StoryOrder.Parse(id.Substring(QuestItemPickups.IdPrefix.Length).Split('_')[0]);
			return t == ChunkPointType.None ? "" : " (Raft's own on " + StoryOrder.Name(t) + " opens only " + StoryOrder.Name(t) + "'s doors, never the plan's)";
		}

		/// <summary>
		/// One story item id on two islands of the plan (the user, 2026-10-16: The Long Voyage's Drowned Metropolis and Mayor's
		/// Wharf both give a 'keycard'): story items belong to the crew by their id, so the one found on either island opens
		/// the locks of both - and counts and "uses up" checks mix them up. An island that only wants an item another gives
		/// (the key for its door found elsewhere) is meant that way; two islands that each give it are not. Map-type islands
		/// are left out (a new one each world).
		/// </summary>
		static void CheckStoryClash(Ctx c)
		{
			var owners = new Dictionary<string, List<KeyValuePair<int, Facts>>>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < c.Plan.Rules.Count; i++)
				foreach (Facts f in Brings(c, c.Plan.Rules[i]).Where(f => !f.Sample))
					foreach (string id in f.GivesStory)
					{
						List<KeyValuePair<int, Facts>> list;
						if (!owners.TryGetValue(id, out list)) owners[id] = list = new List<KeyValuePair<int, Facts>>();
						if (!list.Any(o => o.Value.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase))) list.Add(new KeyValuePair<int, Facts>(i, f));
					}
			foreach (var kv in owners.Where(kv => kv.Value.Count > 1).OrderBy(kv => kv.Value[0].Key))
			{
				string islands = string.Join(" and ", kv.Value.Select(o => "'" + o.Value.Name + "' (rule " + (o.Key + 1) + " '" + c.Plan.Rules[o.Key].Id + "')").ToArray());
				bool raft = kv.Key.StartsWith(QuestItemPickups.IdPrefix, StringComparison.OrdinalIgnoreCase);
				string first = Short(kv.Value[0].Value.Name), second = Short(kv.Value[1].Value.Name);
				c.Add(kv.Value[0].Key, Level.Warning, "The story item '" + kv.Key + "' (" + StoryItems.Label(kv.Key) + ") is given on " + kv.Value.Count + " islands of the plan: " + islands +
					". The crew's story items go by their id, so the one found on either island opens the other's locks too, and counts and \"uses up\" checks mix them up.",
					raft ? "On all but one of these islands, use a story item of the island's own (Island tab > Story items...) instead of the pickup of Raft's " + StoryItems.Label(kv.Key) + ", and point that island's locks, chests and quest steps at it."
						: "Give each island its own id for it (Island tab > Story items...: e.g. '" + first + "-" + kv.Key + "' and '" + (second != first ? second : second + "2") + "-" + kv.Key +
							"'), and change that island's locks, chests and quest steps to match.");
			}
		}

		/// <summary>A short id word from an island's name ("The Drowned Metropolis" -> "metropolis").</summary>
		static string Short(string name)
		{
			string[] w = (name ?? "").ToLowerInvariant().Split(new[] { ' ', '\'', '-' }, StringSplitOptions.RemoveEmptyEntries).Where(x => x != "the" && x.All(char.IsLetterOrDigit)).ToArray();
			return w.Length > 0 ? w.OrderByDescending(x => x.Length).First() : "island";
		}

		/// <summary>Raft's progression (the user, 2026-10-05): every blueprint that lies on Raft's story islands
		/// (raft_blueprints.txt) must be found somewhere in a plan - on the story islands it keeps, or given by its own
		/// islands (quest rewards, chest loot, notes) - or the player can never build it. Says which island of the plan gives
		/// which, in play order, and warns about any never given.</summary>
		static void CheckBlueprints(Ctx c, Progress p)
		{
			if (p.All == 0) return;
			var order = p.Steps.Where(s => s.Rule >= 0 && s.Blueprints.Count > 0).Select(s => "rule " + (s.Rule + 1) + " '" + c.Plan.Rules[s.Rule].Id + "': " + string.Join(", ", s.Blueprints.Select(Pretty).ToArray())).ToList();
			var missing = p.Missing;
			var kept = p.Kept;
			if (missing.Count > 0)
				c.Add(-1, Level.Warning, "Raft's blueprints never given in this plan: " + string.Join(", ", missing.Select(b => Pretty(b) + " (on " + string.Join("/", ScrambledBlueprints.OnIslands[b].ToArray()) + " in Raft)").ToArray()) +
					". The story islands that carry them are left out or replaced, and no island of the plan gives them - the player can never build these.",
					"Give each as a quest reward, in a chest's loot or from a note on one of the plan's islands, in a sensible order (early tools early, the engine before the long legs) - or keep those story islands.");
			if (order.Count > 0)
				c.Add(-1, Level.Tip, "Raft's blueprints given by the plan's islands, in play order: " + string.Join("; ", order.ToArray()) +
					(kept.Count > 0 ? ". The story islands it keeps give " + kept.Count + " more, each at its place in the story." : "."));
		}

		/// <summary>
		/// Raft's tools in play order (the user, 2026-10-16: do it like Raft): a step of an island's main quest that can't be
		/// done because the tool its feature wants (vines the machete...) is given by no island met before it is a warning -
		/// in Raft's chain the story stops there. Optional finds (buried treasure, a cache behind vines no step wants) that want
		/// a tool given later get no finding: players come back, as in Raft; the Progression panel shows them quietly.
		/// </summary>
		static void CheckTools(Ctx c, Progress p)
		{
			foreach (Progress.Step s in p.Steps.Where(s => s.Rule >= 0))
				foreach (Progress.ToolNeed n in s.ToolNeeds.Where(n => n.Main))
				{
					RaftFeatures.Tool t = RaftFeatures.ToolOf(n.Tool);
					string label = RaftFeatures.LabelOf(n.Tool);
					var raftHas = new List<string>();
					foreach (string tool in n.Tool.Split('/'))
					{
						RaftFeatures.Tool one = RaftFeatures.ToolOf(tool);
						List<string> on;
						if (one != null) foreach (string b in one.AllBlueprints) if (ScrambledBlueprints.OnIslands.TryGetValue(b, out on)) raftHas.AddRange(on.Where(i => !raftHas.Contains(i)));
					}
					string onRaft = raftHas.Count > 0 ? " (Raft has its blueprint on " + string.Join("/", raftHas.ToArray()) + ")" : "";
					string story = t != null && t.Story != null ? " (story item " + StoryItems.Ref(t.Story) + ", or its quest item pickup)" : " (or its blueprint)";
					string late = n.Later != null ? "only " + n.Later + " gives it, later in play" : "no island of the plan gives it" + onRaft;
					if (n.Advice)
						c.Add(s.Rule, Level.Warning, "Advice: " + R(c, s.Rule) + " brings " + n.Island + ": a step of its quest leads to " + n.Feature + ", hard without the " + label + ", and " + late + ".",
							"Give the " + label + " on this island or on one met before it, so players aren't stuck there.");
					else
						c.Add(s.Rule, Level.Warning, R(c, s.Rule) + " brings " + n.Island + ": a step of its quest can't be done without the " + label + " (for " + n.Feature + "), and " +
							late + (n.Later != null && c.Plan.Rules[s.Rule].InStory ? ": the story stops here." : "."),
							"Give the " + label + story + " on this island - a chest by the landing, as the generator's explorer's chest - or on an island met before it" + (onRaft.Length > 0 && n.Later == null ? ", or keep that story island before it" : "") + ".");
				}
		}

		/// <summary>What a plan gives of Raft's progression, in play order (PlayOrder): the plan editor's Progression panel
		/// (TODO 4c step 3) and Check's blueprint and tool findings.</summary>
		public class Progress
		{
			public class Step
			{
				/// <summary>The rule (-1: one of Raft's story islands the plan keeps - Raft - at its place in the chain).</summary>
				public int Rule = -1;
				public ChunkPointType Raft = ChunkPointType.None;
				/// <summary>Raft's story-island blueprints the rule's islands (or Raft's story island) give (Blueprint_... names).</summary>
				public List<string> Blueprints = new List<string>();
				/// <summary>Story items its islands give / their locks want (labels); NeedsLate: wanted before any island gives them.</summary>
				public List<string> Gives = new List<string>(), Needs = new List<string>(), NeedsLate = new List<string>();
				/// <summary>Raft's tools given here (labels: the item or its blueprint).</summary>
				public List<string> Tools = new List<string>();
				/// <summary>Features of its islands wanting a tool no island before gives.</summary>
				public List<ToolNeed> ToolNeeds = new List<ToolNeed>();
				/// <summary>Story items its locks want that an island met before gives (label -> that stop's name): Raft's
				/// quest items and the plan's own, kept by the crew for later islands (TODO 4c "kept for later islands").</summary>
				public Dictionary<string, string> NeedsFrom = new Dictionary<string, string>();
				/// <summary>Blueprints given here before the ones they build on (RaftFeatures.BuildsOn).</summary>
				public List<Early> Early = new List<Early>();
			}
			/// <summary>A blueprint given before its bases (Bases: Blueprint_ names not given yet); Later: the first stop after
			/// it that has given them all.</summary>
			public class Early
			{
				public string Blueprint = "", Later = "";
				public List<string> Bases = new List<string>();
			}
			/// <summary>A feature wanting a tool not given yet: Main = a step of the island's main quest can't be done without
			/// it (Check warns); else an optional find, shown quietly. Later: the first island after it that gives it, or null.</summary>
			public class ToolNeed
			{
				public string Island = "", Feature = "", Tool = "", Later;
				/// <summary>Advice: players manage without it, hardly (a dark place, a long dive).</summary>
				public bool Main, Advice;
			}
			/// <summary>The rules whose islands give or want something, and Raft's story islands the plan keeps, in play order.</summary>
			public List<Step> Steps = new List<Step>();
			/// <summary>Blueprints the story islands the plan keeps give, and those never given (Blueprint_... names).</summary>
			public List<string> Kept = new List<string>(), Missing = new List<string>();
			/// <summary>Raft's story-island blueprints in all (0: raft_blueprints.txt not read).</summary>
			public int All;
		}

		/// <summary>The plan's progression (deep: also sample islands of the map types it brings, as Check does).</summary>
		public static Progress Progression(WorldPlan plan, bool deep)
		{
			return Progression(new Ctx { Plan = plan, Deep = deep, SavedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) });
		}

		static Progress Progression(Ctx c)
		{
			var p = new Progress();
			ScrambledBlueprints.Read();
			p.All = ScrambledBlueprints.OnIslands.Count;
			List<Stop> stops = PlayOrder(c.Plan);
			// What each stop gives: its islands, or the blueprints of Raft's story island (and with them its tools) - there, not at the start
			List<List<Facts>> here = stops.Select(st => st.Rule >= 0 ? Brings(c, c.Plan.Rules[st.Rule]).ToList() : new List<Facts>()).ToList();
			List<List<string>> raftBlueprints = stops.Select(st => st.Rule < 0 ? BlueprintsOn(st.Raft) : new List<string>()).ToList();
			List<HashSet<string>> toolsAt = stops.Select((st, n) => new HashSet<string>(st.Rule >= 0 ? here[n].SelectMany(f => f.GivesTools) : RaftFeatures.FromBlueprints(raftBlueprints[n]), StringComparer.OrdinalIgnoreCase)).ToList();
			var tools = new HashSet<string>(RaftFeatures.Tools.Where(t => t.Researched).Select(t => t.Item), StringComparer.OrdinalIgnoreCase);
			// (Raft's list unread: where its tools lie isn't known - no tool findings rather than wrong ones)
			if (p.All == 0) tools.UnionWith(RaftFeatures.Tools.Select(t => t.Item));
			var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var given = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var story = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var storyFrom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var bp =new System.Text.RegularExpressions.Regex(@"Blueprint_[A-Za-z0-9_]+");
			for (int n = 0; n < stops.Count; n++)
			{
				var s = new Progress.Step { Rule = stops[n].Rule, Raft = stops[n].Raft };
				tools.UnionWith(toolsAt[n]);
				s.Tools.AddRange(toolsAt[n].Select(RaftFeatures.LabelOf));
				if (s.Rule < 0)
				{
					s.Blueprints.AddRange(raftBlueprints[n]);
					kept.UnionWith(s.Blueprints);
					given.UnionWith(s.Blueprints);
					p.Steps.Add(s);
					continue;
				}
				foreach (Facts f in here[n])
				{
					foreach (System.Text.RegularExpressions.Match m in bp.Matches(f.AllText))
						if (ScrambledBlueprints.OnIslands.ContainsKey(m.Value) && !s.Blueprints.Contains(m.Value)) s.Blueprints.Add(m.Value);
					foreach (string g in f.GivesStory) { story.Add(g); if (!s.Gives.Contains(StoryItems.Label(g))) s.Gives.Add(StoryItems.Label(g)); }
				}
				foreach (Facts f in here[n])
				{
					foreach (string need in f.NeedsStory)
					{
						string label = StoryItems.Label(need);
						if (!s.Needs.Contains(label)) s.Needs.Add(label);
						if (!story.Contains(need) && !s.NeedsLate.Contains(label)) s.NeedsLate.Add(label);
						string from;
						if (storyFrom.TryGetValue(need, out from) && !s.NeedsFrom.ContainsKey(label)) s.NeedsFrom[label] = from;
					}
					foreach (var kv in f.NeedsTools.Where(kv => !RaftFeatures.Has(tools, kv.Key)))
					{
						int later = Enumerable.Range(n + 1, stops.Count - n - 1).FirstOrDefault(m => RaftFeatures.Has(toolsAt[m], kv.Key));
						foreach (string feature in kv.Value)
							s.ToolNeeds.Add(new Progress.ToolNeed { Island = f.Describe, Feature = feature, Tool = kv.Key, Main = f.MainNeeds.Contains(kv.Key + "|" + feature), Advice = f.Advice.Contains(kv.Key + "|" + feature), Later = later > n ? StopName(c, stops[later]) : null });
					}
				}
				given.UnionWith(s.Blueprints);
				// (a lock of a later island wanting one of these is opened by the one found here: kept by the crew)
				foreach (Facts f in here[n]) foreach (string g in f.GivesStory) if (!storyFrom.ContainsKey(g)) storyFrom[g] = StopName(c, stops[n]);
				if (s.Blueprints.Count > 0 || s.Gives.Count > 0 || s.Needs.Count > 0 || s.Tools.Count > 0 || s.ToolNeeds.Count > 0) p.Steps.Add(s);
			}
			p.Kept = kept.OrderBy(b => b).ToList();
			p.Missing = ScrambledBlueprints.OnIslands.Keys.Where(b => !given.Contains(b)).OrderBy(b => b).ToList();
			FindEarly(c, p);
			return p;
		}

		/// <summary>Blueprints given before the ones they build on (RaftFeatures.BuildsOn), with the first stop after that has
		/// given the bases. A base never given is left to the "never given" warning.</summary>
		static void FindEarly(Ctx c, Progress p)
		{
			var sofar = new List<HashSet<string>>();
			var acc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Progress.Step s in p.Steps) { acc.UnionWith(s.Blueprints); sofar.Add(new HashSet<string>(acc, StringComparer.OrdinalIgnoreCase)); }
			for (int n = 0; n < p.Steps.Count; n++)
				foreach (string b in p.Steps[n].Blueprints)
				{
					string[] bases;
					if (!RaftFeatures.BuildsOn.TryGetValue(b, out bases)) continue;
					List<string> late = bases.Where(x => !sofar[n].Contains(x)).ToList();
					if (late.Count == 0 || late.Any(x => p.Missing.Contains(x))) continue;
					int m = Enumerable.Range(n + 1, p.Steps.Count - n - 1).First(k => late.All(x => sofar[k].Contains(x)));
					Progress.Step at = p.Steps[m];
					p.Steps[n].Early.Add(new Progress.Early { Blueprint = b, Bases = late, Later = StopName(c, new Stop { Rule = at.Rule, Raft = at.Raft }) });
				}
		}

		/// <summary>
		/// Raft's blueprints in Raft's order (TODO 4c "every blueprint and key item, in order"): a blueprint given before the
		/// ones it builds on (the engine controls before the motor wheel and steering wheel) is a warning - players find a
		/// blueprint they can't use until a later island.
		/// </summary>
		static void CheckBlueprintOrder(Ctx c, Progress p)
		{
			foreach (Progress.Step s in p.Steps)
				foreach (Progress.Early e in s.Early)
				{
					string bases = string.Join(" and ", e.Bases.Select(Pretty).ToArray());
					string who = s.Rule >= 0 ? R(c, s.Rule) : StoryOrder.Name(s.Raft) + " (Raft's story island)";
					c.Add(s.Rule, Level.Warning, who + " gives the " + Pretty(e.Blueprint) + " blueprint before the " + bases + " it builds on: only " + e.Later +
						" gives " + (e.Bases.Count > 1 ? "them" : "it") + ", later in play - players can't use it until then.",
						"Give the " + bases + " on this island or one met before it (Raft's own order: " + RaftOrder(e) + "), or give the " + Pretty(e.Blueprint) + " later.");
				}
		}

		/// <summary>Where Raft gives a blueprint's bases, then the blueprint ("Vasagatan, then Caravan").</summary>
		static string RaftOrder(Progress.Early e)
		{
			List<string> on;
			Func<string, string> where = b => ScrambledBlueprints.OnIslands.TryGetValue(b, out on) ? string.Join("/", on.ToArray()) : "the research table";
			return string.Join(", ", e.Bases.Select(where).Distinct().ToArray()) + ", then " + where(e.Blueprint);
		}

		/// <summary>The blueprints lying on one of Raft's story islands (raft_blueprints.txt).</summary>
		static List<string> BlueprintsOn(ChunkPointType t)
		{
			return ScrambledBlueprints.OnIslands.Where(kv => kv.Value.Any(i => StoryOrder.Parse(i) == t)).Select(kv => kv.Key).OrderBy(b => b).ToList();
		}

		/// <summary>A stop as players know it: Raft's story island by its name, a rule by its number, name and island.</summary>
		public static string StopName(WorldPlan plan, Stop st)
		{
			if (st.Rule < 0) return StoryOrder.Name(st.Raft);
			IntroRule r = plan.Rules[st.Rule];
			return "rule " + (st.Rule + 1) + " '" + r.Id + "'" + (r.What == "island" && r.WhatArg.Trim().Length > 0 ? " (" + r.WhatArg.Trim() + ")" : "");
		}

		static string StopName(Ctx c, Stop st) { return StopName(c.Plan, st); }

		/// <summary>One stop of a plan in play order: one of Raft's story islands it keeps (Rule -1, Raft), or a rule's islands.</summary>
		public class Stop
		{
			public int Rule = -1;
			public ChunkPointType Raft = ChunkPointType.None;
		}

		/// <summary>About how far players sail between two of Raft's story islands, and in a day (PlayOrder: a rough guide only).</summary>
		public const float KmPerStoryIsland = 4f, DayKm = 2f;

		/// <summary>
		/// The plan in the order a player meets it (the user, 2026-10-16: not the file's order). Raft's Receiver chain as a
		/// world builds it (StoryChain.BuildSteps: first, after:X, instead:X, and Raft's story islands it keeps), and every
		/// other rule by its WHEN: the world's start before the chain's first island; a distance or a day after as many of
		/// Raft's story islands as players pass on the way, about one every KmPerStoryIsland km (a day as DayKm km; with
		/// Raft's story off, the chain's own islands); a rule waiting for another (its quest, a step, a zone, a visit, a
		/// signal, the rule) right after it. A chain rule that also waits for another comes after both. A rule waiting for an
		/// island no rule brings, or in a circle (CheckOrder), comes last.
		/// </summary>
		public static List<Stop> PlayOrder(WorldPlan plan)
		{
			List<IntroRule> rules = plan.Rules;
			List<string> chain = StoryChain.BuildSteps(plan.RaftStory, plan.LeaveOut, rules);
			double last = chain.Count + 1;
			const double After = 0.001;
			// (where a distance comes in the chain: after the n-th of Raft's story islands, or of the chain's islands without them)
			List<int> anchors = Enumerable.Range(0, chain.Count).Where(k => StoryChain.IsRaft(chain[k])).ToList();
			if (anchors.Count == 0) anchors = Enumerable.Range(0, chain.Count).ToList();
			var rank = new double[rules.Count];
			var state = new int[rules.Count]; // (0 not yet, 1 working it out, 2 known)
			Func<int, double> rankOf = null;
			rankOf = i =>
			{
				if (state[i] == 2) return rank[i];
				if (state[i] == 1) return last; // (a circle: CheckOrder says so)
				state[i] = 1;
				IntroRule r = rules[i];
				double at;
				float num;
				if (r.When == "km" || r.When == "day")
				{
					float km = Mathf.Clamp(float.TryParse(r.WhenArg, NumberStyles.Float, CultureInfo.InvariantCulture, out num) ? num : 0f, 0f, 1000f) * (r.When == "day" ? DayKm : 1f);
					int passed = Mathf.Min((int)(km / KmPerStoryIsland), anchors.Count);
					// (the start: before the chain; else just after that island, before the next - nearer ones first)
					at = (passed == 0 ? 0.1 : anchors[passed - 1] + 1.5) + km / 100000.0;
				}
				else if (r.When == "rule" || NeedsIsland(r.When))
				{
					int d = rules.FindIndex(x => x.Id.Length > 0 && x.Id.Equals(r.WhenRef, StringComparison.OrdinalIgnoreCase));
					at = d >= 0 && d != i ? rankOf(d) + After : last;
				}
				else at = 0;
				int inChain = r.InStory ? chain.FindIndex(s => s.Equals(StoryChain.RuleKey(r.Id), StringComparison.OrdinalIgnoreCase)) : -1;
				if (inChain >= 0) at = Math.Max(at, inChain + 1);
				rank[i] = at;
				state[i] = 2;
				return at;
			};
			var order = new List<KeyValuePair<double, Stop>>();
			for (int k = 0; k < chain.Count; k++)
				if (StoryChain.IsRaft(chain[k])) order.Add(new KeyValuePair<double, Stop>(k + 1, new Stop { Raft = StoryChain.TypeOfStep(chain[k]) }));
			for (int i = 0; i < rules.Count; i++) order.Add(new KeyValuePair<double, Stop>(rankOf(i), new Stop { Rule = i }));
			return order.OrderBy(kv => kv.Key).ThenBy(kv => kv.Value.Rule < 0 ? 0 : 1).ThenBy(kv => kv.Value.Rule).Select(kv => kv.Value).ToList();
		}

		/// <summary>A blueprint as players read it: "Blueprint_EngineControls" -> "Engine Controls", "Blueprint_Pipe_Fuel" -> "Pipe Fuel".</summary>
		public static string Pretty(string blueprint) { return System.Text.RegularExpressions.Regex.Replace(blueprint.Replace("Blueprint_", "").Replace("_", " "), "(?<=[a-z])(?=[A-Z0-9])", " "); }

		/// <summary>The islands a rule's reference means: a rule of the plan (what it brings), this island (self), or a saved island.</summary>
		static List<Facts> RefFacts(Ctx c, string reference, out string what, out bool known, out int ruleIndex)
		{
			what = reference; known = true; ruleIndex = -1;
			var list = new List<Facts>();
			if (reference.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase))
			{
				what = c.IslandMode ? "this island" : "self";
				known = c.IslandMode;
				if (c.Own != null) list.Add(c.Own);
				return list;
			}
			int idx = c.Plan.Rules.FindIndex(x => x.Id.Equals(reference, StringComparison.OrdinalIgnoreCase));
			if (idx >= 0)
			{
				ruleIndex = idx;
				what = "rule " + (idx + 1) + " '" + reference + "' (" + c.Plan.Rules[idx].DescribeWhat() + ")";
				list.AddRange(Brings(c, c.Plan.Rules[idx]));
				return list;
			}
			KeyValuePair<IntroRule, Facts> own;
			if (c.IslandRules.TryGetValue(reference, out own))
			{
				what = "the rule '" + reference + "' of " + own.Value.Describe + " (" + own.Key.DescribeWhat() + ")";
				list.AddRange(Brings(c, own.Key));
				return list;
			}
			if (c.SavedNames.Contains(reference)) { what = "the saved island '" + reference + "'"; Facts f = Saved(reference); if (f != null) list.Add(f); return list; }
			known = false;
			return list;
		}

		/// <summary>The islands a rule may bring (null entries dropped; a spawn pool island is unknown).</summary>
		static IEnumerable<Facts> Brings(Ctx c, IntroRule r)
		{
			if (r.What == "island") { Facts f = Saved(r.WhatArg.Trim()); if (f != null) yield return f; }
			else if (r.What == "oneof") { foreach (string n in r.WhatArg.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0)) { Facts f = Saved(n); if (f != null) yield return f; } }
			else if (r.What == "type") { Facts f = Sample(r.WhatArg.Trim(), c.Deep); if (f != null) yield return f; }
		}

		static bool NeedsIsland(string when) { return when == "quest" || when == "step" || when == "zone" || when == "visit" || when == "signal"; }

		static void CheckWhen(Ctx c, int i)
		{
			IntroRule r = c.Plan.Rules[i];
			float num;
			if (r.When == "km" || r.When == "day")
			{
				if (!float.TryParse(r.WhenArg, NumberStyles.Float, CultureInfo.InvariantCulture, out num) || num < 0f)
					c.Add(i, Level.Problem, R(c, i) + " waits " + (r.When == "km" ? "for a distance" : "for a day") + " but has no number (\"" + r.WhenArg + "\").", "Type " + (r.When == "km" ? "the km to sail, e.g. 2" : "the day, e.g. 3") + " in WHEN.");
				// (the game plays them at once: 0 km sailed and day 0 are reached when the world starts)
				else if (num <= 0f)
					c.Add(i, Level.Tip, R(c, i) + " waits for " + (r.When == "km" ? "0 km" : "day 0") + ": that is there when the world starts, so it comes at once - like \"When the world starts\".", "Fine if that is what you want; else type " + (r.When == "km" ? "the km to sail, e.g. 2" : "a later day, e.g. 3") + ".");
				else if (r.When == "km" && num > 30f || r.When == "day" && num > 20f)
					c.Add(i, Level.Tip, R(c, i) + " comes only after " + (r.When == "km" ? num + " km of sailing" : "day " + num) + ": that takes a long time in play - and to test.", "Try the plan with a small number first.");
				// (SR1: "again until done" brings the island back every N km / days - not one found on the Receiver)
				if (r.Again && r.Where == "receiver")
					c.Add(i, Level.Tip, R(c, i) + " has \"again until done\", but its island comes on the Receiver: players find it by its frequency, so it doesn't come back by itself.", "Fine as it is; for an island that comes back, bring it ahead of the raft.");
				else if (r.Again && float.TryParse(r.WhenArg, NumberStyles.Float, CultureInfo.InvariantCulture, out num) && num < 1f)
					c.Add(i, Level.Tip, R(c, i) + " comes again until done every " + (r.When == "km" ? "km" : "day") + " (its number is below 1).", "Type a larger number for longer waits between returns.");
			}
			if (r.When == "step" && (!int.TryParse(r.WhenArg, out int n) || n < 1))
				c.Add(i, Level.Problem, R(c, i) + " waits for quest steps but the number of steps (\"" + r.WhenArg + "\") isn't a number from 1 up.", "Type how many steps, e.g. 2.");
			if (r.When == "rule")
			{
				if (r.WhenRef.Length == 0) c.Add(i, Level.Problem, R(c, i) + " waits for another rule but doesn't say which.", "Choose the rule after \"rule\" in WHEN (▾ lists them).");
				else if (r.WhenRef.Equals(r.Id, StringComparison.OrdinalIgnoreCase)) c.Add(i, Level.Problem, R(c, i) + " waits for itself: it never comes.", "Choose another rule to wait for.");
				else if (!c.Plan.Rules.Any(x => x.Id.Equals(r.WhenRef, StringComparison.OrdinalIgnoreCase))) c.Add(i, Level.Problem, R(c, i) + " waits for rule '" + r.WhenRef + "', which isn't in the " + (c.IslandMode ? "island's rules" : "plan") + ".", "Pick the rule from the ▾ list, or type its name exactly.");
				return;
			}
			if (!NeedsIsland(r.When)) return;
			if (r.WhenRef.Length == 0)
			{
				if (!c.IslandMode) c.Add(i, Level.Problem, R(c, i) + " waits for something on an island but doesn't say which island.", "Choose the island after \"island\" in WHEN: a rule of this plan (the island it brings) or one of your saved islands.");
				return;
			}
			if (r.WhenRef.Equals(r.Id, StringComparison.OrdinalIgnoreCase)) { c.Add(i, Level.Problem, R(c, i) + " waits for its own island: it can't come before it is there, so it never comes.", "Wait for another rule's island."); return; }
			string what; bool known; int ruleIndex;
			List<Facts> islands = RefFacts(c, r.WhenRef, out what, out known, out ruleIndex);
			if (!known && r.WhenRef.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase)) { c.Add(i, Level.Problem, R(c, i) + " waits for \"self\", which only means something in an island's own rules (the island itself): in a world plan it points at nothing, so the rule never comes.", "Name the island instead: a rule of this plan, or one of your saved islands."); return; }
			if (!known) { c.Add(i, Level.Problem, R(c, i) + " waits for '" + r.WhenRef + "': no rule of this plan and no saved island has that name.", "Pick it from the ▾ list after the island field (it lists the plan's rules and your saved islands)."); return; }
			if (ruleIndex < 0 && !c.IslandMode && !r.WhenRef.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase) && !c.IslandRules.ContainsKey(r.WhenRef) && !c.Plan.Rules.Any(x => IslandNamed(x, r.WhenRef)))
				c.Add(i, c.Plan.Random ? Level.Warning : Level.Problem, R(c, i) + " waits for the saved island '" + r.WhenRef + "', but no rule of this plan brings it. " +
					(c.Plan.Random ? "It only works if that island turns up by chance while sailing (or another plan or an island's rule brings it)." : "Random islands are off, so it never comes into the world - and this rule never fires."),
					"Add a rule that brings '" + r.WhenRef + "', and wait for that rule's name instead.");
			if (ruleIndex >= 0 && c.Plan.Rules[ruleIndex].What == "pool")
				c.Add(i, Level.Tip, R(c, i) + " waits for " + what + ": a random island from the spawn pool, so what it has (a quest, zones) can't be checked here.", "");
			foreach (Facts f in islands) CheckIslandFor(c, i, r.When, r.WhenArg, f, "WHEN");
		}

		/// <summary>Does this rule bring an island of that name (island:, one of oneof:)?</summary>
		static bool IslandNamed(IntroRule x, string name)
		{
			return x.What == "island" && x.WhatArg.Trim().Equals(name, StringComparison.OrdinalIgnoreCase) ||
				x.What == "oneof" && x.WhatArg.Split(',').Any(n => n.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>What a WHEN (or a story "done when") needs of an island: a quest that can be finished, enough steps, the zone, the signal.</summary>
		static void CheckIslandFor(Ctx c, int i, string kind, string arg, Facts f, string part)
		{
			string who = R(c, i) + (part == "STORY" ? " (its STORY \"done when\")" : "");
			int questNo;
			if (kind == "quest" && part != "STORY" && int.TryParse(arg.Trim(), out questNo) && questNo > 1)
			{
				// (a rule waiting for Quest 2 or later of the island)
				if (questNo - 2 >= f.MoreQuests.Count || f.MoreQuests[questNo - 2].Steps.Count == 0)
					c.Add(i, Level.Problem, who + " waits for quest " + questNo + " of " + f.Describe + ", which has " + (f.MoreQuests.Count == 0 ? "only its main quest" : "quests up to " + (f.MoreQuests.Count + 1)) + ": it never comes.", "Use a quest number the island has (empty = its main quest).");
				return;
			}
			if (kind == "quest" || kind == "step")
			{
				if (!f.Quest.Exists) { c.Add(i, Level.Problem, who + " waits for the quest of " + f.Describe + ", which has no quest: it never " + (part == "STORY" ? "counts as done" : "comes") + ".", f.Sample ? "Use \"When players reach an island\" for this map type, or a map type with a quest (camp, treasure, stacks, swamp...)." : "Use \"When players reach an island\", or give '" + f.Name + "' a quest (open it in the editor: Island tab > Edit quest...)."); return; }
				int need = f.Quest.Steps.Count;
				if (kind == "step" && int.TryParse(arg, out int n)) { if (n > need) c.Add(i, Level.Problem, who + " waits for " + n + " quest steps of " + f.Describe + ", but its quest has only " + need + ".", "Wait for at most " + need + " steps."); need = Math.Min(n, need); }
				CheckQuest(c, i, f, need, who);
			}
			else if (kind == "zone")
			{
				if (arg.Trim().Length == 0) c.Add(i, Level.Problem, who + " waits for a trigger zone of " + f.Describe + " but doesn't say which.", "Choose the zone with the ▾ after the zone field.");
				else if (!f.Zones.Contains(arg.Trim())) c.Add(i, Level.Problem, who + " waits for the zone '" + arg + "' of " + f.Describe + ", which has " + (f.Zones.Count == 0 ? "no trigger zones" : "only " + List(f.Zones)) + ".", "Pick the zone from the ▾ list" + (f.Sample ? "" : ", or place a trigger zone named '" + arg + "' on the island") + ".");
			}
			else if (kind == "note")
			{
				int n;
				List<int> notes = f.Sample ? null : IslandCache.NotesOf(f.Name);
				if (!int.TryParse(arg.Trim(), out n)) c.Add(i, Level.Problem, who + " waits for a note of " + f.Describe + " but doesn't say which.", "Choose the note with the ▾ after the note field.");
				else if (notes != null && !notes.Contains(n)) c.Add(i, Level.Problem, who + " waits for note #" + n + " of " + f.Describe + ", which has " + (notes.Count == 0 ? "no notes with a text" : "only notes " + string.Join(", ", notes.Select(x => "#" + x).ToArray())) + ".", "Pick the note from the ▾ list.");
			}
			else if (kind == "signal")
			{
				if (arg.Trim().Length == 0) c.Add(i, Level.Problem, who + " waits for a signal from " + f.Describe + " but doesn't say which.", "Choose the signal with the ▾ after the signal field.");
				else if (!f.Signals.Contains(arg.Trim())) c.Add(i, Level.Problem, who + " waits for the signal '" + arg + "' from " + f.Describe + ", but nothing there sends it" + (f.Signals.Count > 0 ? " (it sends " + List(f.Signals) + ")" : "") + ".", "Pick it from the ▾ list, or give an object a \"send a signal\" action with that name (Behaviour & events...).");
			}
		}

		/// <summary>An island's own quest checked against what the island has (ROADMAP CW6: every generated quest).</summary>
		public static List<Finding> QuestFindings(IslandFile f)
		{
			var c = new Ctx { Plan = new WorldPlan(), IslandMode = true, SavedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
			Facts x = FromFile(f, f.Name ?? "island", false);
			CheckQuest(c, -1, x, x.Quest.Steps.Count, "The island");
			return c.Out;
		}

		/// <summary>The first `steps` steps of an island's quest: can each be done there?</summary>
		static void CheckQuest(Ctx c, int i, Facts f, int steps, string who)
		{
			for (int s = 0; s < steps && s < f.Quest.Steps.Count; s++)
			{
				IslandQuest.Step st = f.Quest.Steps[s];
				string t = st.Target.Trim();
				string head = who + ": step " + (s + 1) + " of the quest of " + f.Describe + " (\"" + st.Describe() + "\")";
				string tail = " - the quest can't be finished, so the rule never " + (who.Contains("STORY") ? "counts as done" : "comes") + ".";
				string fix = f.Sample ? "Choose another map type, or \"When players reach an island\"." : "Open '" + f.Name + "' in the editor and fix its quest (Island tab > Edit quest...), or the object it names.";
				switch (st.Type)
				{
					case "reach":
						if (t.Length > 0 && !f.Zones.Contains(t)) c.Add(i, Level.Problem, head + " needs a trigger zone '" + t + "', which the island doesn't have" + (f.Zones.Count > 0 ? " (it has " + List(f.Zones) + ")" : "") + tail, fix);
						else if (t.Length == 0 && f.Zones.Count == 0) c.Add(i, Level.Problem, head + " needs a trigger zone to walk into, and the island has none" + tail, fix);
						break;
					case "read":
						if (t.Length > 0 && !f.Notes.Contains(t)) c.Add(i, Level.Problem, head + " needs a note titled \"" + t + "\", which the island doesn't have" + (f.Notes.Count > 0 ? " (its notes: " + List(f.Notes) + ")" : "") + tail, fix);
						else if (t.Length == 0 && f.NoteCount == 0) c.Add(i, Level.Problem, head + " needs a note to read, and the island has none" + tail, fix);
						break;
					case "open":
						if (t.Length > 0 && !f.Chests.Contains(t)) c.Add(i, Level.Problem, head + " needs a chest titled \"" + t + "\", which the island doesn't have" + (f.Chests.Count > 0 ? " (its chests: " + List(f.Chests) + ")" : "") + tail, fix);
						else if (t.Length == 0 && f.ChestCount == 0) c.Add(i, Level.Problem, head + " needs a chest to open, and the island has none" + (f.RaftCrates > 0 ? " (its abandoned raft crates don't count: place a chest from Loot & chests)" : "") + tail, fix);
						break;
					case "kill":
					case "catch":
						int have = t.Length > 0 ? (f.Creatures.TryGetValue(t, out int h) ? h : 0) : f.Creatures.Values.Sum();
						if (have == 0) c.Add(i, Level.Problem, head + " needs " + (t.Length > 0 ? t.ToLowerInvariant() + "s" : "creatures") + ", and the island has none" + tail, fix);
						else if (st.Count > have && !(t.Length > 0 ? f.CreaturesComeBack.Contains(t) : f.CreaturesComeBack.Count > 0))
							c.Add(i, Level.Warning, head + " needs " + st.Count + ", but the island has only " + have + " and they don't come back: players can't reach " + st.Count + " there.", "Place more, or set them to come back after some days, or lower the number in the quest.");
						else if (st.Count > have)
							c.Add(i, Level.Tip, head + " needs " + st.Count + ", and the island has " + have + " at a time: players wait for them to come back (after the island's regrow days) to reach " + st.Count + ".", "Fine if that is what you want; else place more or lower the number.");
						// (Raft leaves some kinds out in some game modes - screechers and puffer fish in Creative: the step can't be done there)
						ContentCatalog.CreatureKind kind = t.Length > 0 ? ContentCatalog.Creatures.FirstOrDefault(k => k.Label.Equals(t, StringComparison.OrdinalIgnoreCase)) : null;
						List<string> without = kind != null && have > 0 ? CreatureSpawner.ModesWithout(kind.Type) : new List<string>();
						if (without.Count > 0)
							c.Add(i, Level.Tip, head + " needs " + t.ToLowerInvariant() + "s, and in " + string.Join(" and ", without.ToArray()) + " worlds Raft has none (as on its own islands): there the quest can't be finished.", "Fine for worlds in the other game modes; for every mode, use another creature.");
						break;
					case "collect":
						if (t.Length > 0 && !f.AllText.Contains(StoryItems.Ref(StoryItems.IdOf(t))))
							c.Add(i, Level.Warning, head + " needs the story item " + StoryItems.Label(t) + ", but nothing on the island gives it (no chest, zone or action). It works only if players got it on another island.", "Put it in a chest or a zone's items on this island, or make sure another island gives it first.");
						break;
					case "blueprint":
						ScrambledBlueprints.Read();
						if (t.Length > 0 && ItemManager.GetAllItems() != null && ItemManager.GetAllItems().Count > 0 && !BlueprintSteps.Exists(t))
							c.Add(i, Level.Problem, head + " needs the blueprint '" + t + "', and Raft has no item of that name" + tail, "Pick the blueprint from the list beside the step (Raft's names start with Blueprint_).");
						else if (t.Length > 0 && f.AllText.IndexOf(t + "*", StringComparison.OrdinalIgnoreCase) < 0 && !ScrambledBlueprints.OnIslands.ContainsKey(t))
							c.Add(i, Level.Warning, head + " needs the blueprint " + Pretty(t) + ", but nothing on the island gives it (no chest, zone or action) and it isn't on Raft's story islands. It works only if players got it elsewhere (another island, a Raft store).", "Put " + t + "*1 in a chest or a zone's items on this island, or make sure another island gives it first.");
						else if (t.Length > 0 && f.AllText.IndexOf(t + "*", StringComparison.OrdinalIgnoreCase) < 0)
							c.Add(i, Level.Tip, head + " needs the blueprint " + Pretty(t) + ", which this island doesn't give: it lies on Raft's story island " + string.Join("/", ScrambledBlueprints.OnIslands[t].ToArray()) + " (or a Raft store sells it to players who missed it).", "Fine if the plan keeps that island before this one; else put " + t + "*1 in a chest here.");
						break;
					case "pages":
						// (a page comes from a note with text, or a "journal" action: an empty note gives none)
						if (st.Target != "all" && f.PageCount < st.Count)
							c.Add(i, Level.Problem, head + " needs " + st.Count + " pages read on the island, and it gives only " + f.PageCount + (f.NoteCount > f.PageCount ? " (" + (f.NoteCount - f.PageCount) + " of its notes have no text: an empty note gives no page)" : "") + tail, fix);
						break;
				}
			}
		}

		/// <summary>The quest traps found on the islands (AU47): the island being edited, or each saved island the plan
		/// brings (once, at the first rule that brings it).</summary>
		static void CheckTraps(Ctx c)
		{
			if (c.IslandMode)
			{
				if (c.Own != null) foreach (Finding t in c.Own.Traps) c.Add(-1, t.Level, "This island: " + t.Text, t.Fix);
				return;
			}
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < c.Plan.Rules.Count; i++)
				foreach (Facts f in Brings(c, c.Plan.Rules[i]))
					if (!f.Sample && seen.Add(f.Name))
						foreach (Finding t in f.Traps) c.Add(i, t.Level, R(c, i) + " brings " + f.Describe + ": " + t.Text, t.Fix);
		}

		/// <summary>The when/then sounds and camera moments (RF1/RF2): a sound Raft doesn't have, a marker or look-at name no object has.</summary>
		static void FindMomentTraps(IslandFile f, Facts x, List<IDictionary<string, string>> objects)
		{
			List<string> known = SoundLibrary.Events;
			var names = new HashSet<string>(objects.Select(p => ObjectProps.Get(p, BehaviourProps.Name)).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
			var badSounds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			var badNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (IDictionary<string, string> p in objects.Concat(new[] { (IDictionary<string, string>)(f.Props ?? new Dictionary<string, string>()) }))
				foreach (var kv in p.Where(kv => kv.Key.StartsWith(BehaviourProps.EventPrefix) || kv.Key.StartsWith(BehaviourProps.ElsePrefix)))
					foreach (ObjAction a in ObjAction.ParseLines(kv.Value))
					{
						if (a.Verb == "sound" || a.Verb == "playsound")
						{
							string path = a.Verb == "sound" ? a.Arg.Trim() : TriggerSounds.Parse(a.Arg).Path;
							// (the list is empty where FMOD isn't loaded: nothing to say then)
							if (known.Count > 0 && !known.Contains(path, StringComparer.OrdinalIgnoreCase)) badSounds.Add(path.Length > 0 ? path : "(none)");
						}
						if (a.Verb == "camera")
						{
							string look; float s, r;
							CameraMoment.Parse(a.Arg, out look, out s, out r);
							foreach (string n in new[] { a.Target.Trim(), look })
								if (n.Length > 0 && !n.Equals("self", StringComparison.OrdinalIgnoreCase) && !names.Contains(n)) badNames.Add(n);
						}
					}
			if (badSounds.Count > 0)
				x.Traps.Add(new Finding(-1, Level.Warning, "an action plays a sound Raft doesn't have: " + string.Join(", ", badSounds.Select(n => "'" + n + "'").ToArray()) + " - a typo? Nothing is heard.",
					"Choose the sound with the ... button by the action (the list has every sound and music cue of Raft's)."));
			if (badNames.Count > 0)
				x.Traps.Add(new Finding(-1, Level.Warning, "a camera moment names an object no object on the island is called: " + string.Join(", ", badNames.Select(n => "'" + n + "'").ToArray()) + " - the moment doesn't happen (or looks the wrong way).",
					"Give the marker or the object to look at that name (its Name field), or choose the name with the ... button by the action."));
		}

		/// <summary>
		/// The traps of guide 6.2 that can be seen in an island's file (AU47), each with the object or step and the fix:
		/// a later quest step's chest or creatures there from the start, story items given again by something that happens
		/// again, show/hide and open/close in events that happen again, Raft item checks on events no player sets off, and
		/// Raft items given by a note. (Already fixed in the game, so not here: actions after a wait are kept with the island
		/// until they have run; a chest with story items never fills up again; shared actions run on the host for everyone;
		/// collect and pages steps count the crew's items and pages.)
		/// </summary>
		static void FindTraps(IslandFile f, Facts x, bool islandNever)
		{
			var objects = f.Objects.Select(o => new KeyValuePair<IslandObject, IDictionary<string, string>>(o, o.Props ?? new Dictionary<string, string>())).ToList();
			FindMomentTraps(f, x, objects.Select(o => o.Value).ToList());
			// A story item the island uses that isn't in its Story items list (deleted there, or a typo): another island
			// of the world may define it (StoryItems.Find), otherwise it shows with its id for a name and no picture
			var defined = new HashSet<string>(StoryItems.Of(f.Props).Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
			var used = new HashSet<string>(x.NeedsStory.Concat(x.GivesStory), StringComparer.OrdinalIgnoreCase);
			for (int n = 0, quests = IslandQuest.CountIn(f.Props); n < quests; n++)
				foreach (IslandQuest.Step st in IslandQuest.From(f.Props, n).Steps)
					if (StoryItems.IsStory(st.Target)) used.Add(StoryItems.IdOf(st.Target));
			List<string> unknown = used.Where(id => id.Length > 0 && !defined.Contains(id) && QuestItemPickups.Def(id) == null).OrderBy(id => id).ToList();
			if (unknown.Count > 0)
				x.Traps.Add(new Finding(-1, Level.Warning, "the story item" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown.Select(id => "'" + id + "'").ToArray()) +
					(unknown.Count > 1 ? " are" : " is") + " used (a quest step, a check, a chest or an action) but not in the island's Story items list - deleted, or a typo? Unless another island of the world has it, it shows with its id for a name and no picture.",
					"Island tab > Story items...: add it again (Ctrl+Z if you just deleted it), or change what uses it."));
			// A key door (RF4) whose use wants no story item: it opens for anyone, which a key door seldom means
			List<string> open = objects.Where(o => KeycardDoor.Is(o.Key.Name) && ObjectProps.Get(o.Value, BehaviourProps.Use).Length > 0 && KeycardDoor.CardOf(o.Value) == null)
				.Select(o => ObjectProps.Get(o.Value, BehaviourProps.Name).Trim().Length > 0 ? "'" + ObjectProps.Get(o.Value, BehaviourProps.Name).Trim() + "'" : PlaceableCatalog.DisplayName(o.Key.Name)).ToList();
			if (open.Count > 0)
				x.Traps.Add(new Finding(-1, Level.Warning, (open.Count > 1 ? open.Count + " keycard/key doors (" : "a keycard/key door (") + string.Join(", ", open.Distinct().Take(4).ToArray()) + ") that no story item opens: "
					+ (open.Count > 1 ? "they open" : "it opens") + " for anyone.",
					"Select the door: Keycard door > Card, and choose the story item (a keycard) that opens it."));
			// A later step's chest or creature spot that is there from the start: players get to it before the step (what
			// they do is kept and counted when the step comes - QuestTracker - but its loot and the story come too early)
			for (int n = 0, quests = IslandQuest.CountIn(f.Props); n < quests; n++)
			{
				IslandQuest q = IslandQuest.From(f.Props, n);
				string quest = n == 0 ? "the quest" : "quest " + (n + 1) + (q.Title.Trim().Length > 0 ? " '" + q.Title.Trim() + "'" : "");
				for (int s = 1; s < q.Steps.Count; s++)
				{
					IslandQuest.Step st = q.Steps[s];
					if (st.Type != "open" && st.Type != "kill" && st.Type != "catch") continue;
					string t = st.Target.Trim();
					// (an earlier step wants the same: what players do then counts for that one)
					if (q.Steps.Take(s).Any(e => e.Type == st.Type && e.Target.Trim().Equals(t, StringComparison.OrdinalIgnoreCase))) continue;
					var early = new List<string>();
					foreach (KeyValuePair<IslandObject, IDictionary<string, string>> o in objects)
					{
						if (BehaviourProps.StartsHidden(o.Value)) continue;
						if (st.Type == "open")
						{
							if (!ObjectProps.IsLoot(o.Key.Name, o.Value) || t.Length > 0 && !ObjectProps.Get(o.Value, ObjectProps.NoteTitle).Trim().Equals(t, StringComparison.OrdinalIgnoreCase)) continue;
							// (a locked chest - "Only if..." - keeps its loot until its checks pass)
							if (ObjCheck.ParseLines(ObjectProps.Get(o.Value, BehaviourProps.CheckKey("open"))).Count > 0) continue;
						}
						else
						{
							ContentCatalog.CreatureKind k = ContentCatalog.CreatureOf(o.Key.Name);
							if (k == null || t.Length > 0 && !k.Label.Equals(t, StringComparison.OrdinalIgnoreCase)) continue;
							// (an ambush: the animals wait for a trigger zone of the island)
							string zone = ObjectProps.Get(o.Value, ObjectProps.CreatureZone);
							if (zone.Length > 0 && x.Zones.Contains(zone)) continue;
						}
						early.Add(Thing(o.Key, o.Value));
					}
					if (early.Count == 0) continue;
					bool chest = st.Type == "open";
					x.Traps.Add(new Finding(-1, Level.Warning, "step " + (s + 1) + " of " + quest + " (\"" + st.Describe() + "\") wants " + Things(early) + ", which " + (early.Count == 1 ? "is" : "are") +
						" there from the start: players can " + (chest ? "open " + (early.Count == 1 ? "it" : "them") : (st.Type == "kill" ? "defeat" : "catch") + " the animals") + " before step " + (s + 1) +
						" comes. It counts when the step comes, but " + (chest ? "the loot" : "the fight") + " and the story come too early.",
						chest ? "Give it a name and set it to \"Hidden until shown\" (Behaviour & events...), and show it with a \"show\" action on what step " + s + " wants (its note, zone or chest) - or lock it: + Only if... > quest step " + s + "."
							: "Give the spot a name and set it to \"Hidden (ambush)\" (Behaviour & events...), and show it with a \"show\" action on what step " + s + " wants (its note, zone or chest) - or let it appear when a trigger zone fires (Appears: when '...' fires)."));
				}
			}
			foreach (KeyValuePair<IslandObject, IDictionary<string, string>> o in objects)
			{
				IDictionary<string, string> p = o.Value;
				string thing = Thing(o.Key, p);
				// Events that happen again: a zone ready again after the regrow days (or every time), a chest that fills up
				// again (one with story items never does - LootCrate), a spot whose animals come back (its defeat again)
				string ev, again = EventAgain(o.Key.Name, p, islandNever, out ev);
				List<ObjAction> acts = ev != null ? ObjAction.ParseLines(ObjectProps.Get(p, BehaviourProps.EventKey(ev))) : new List<ObjAction>();
				if (again.Length > 0)
				{
					// Story items given again: the crew gets a second key or log
					var story = new List<string>();
					if (ev == "enter") story.AddRange(ObjectProps.Loot(p).Where(l => StoryItems.IsStory(l.Key)).Select(l => StoryItems.Label(l.Key)));
					foreach (ObjAction a in acts.Where(Behaviours.GivesStory))
						story.AddRange(ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, a.Arg } }).Where(l => StoryItems.IsStory(l.Key)).Select(l => StoryItems.Label(l.Key)));
					if (story.Count > 0)
						x.Traps.Add(new Finding(-1, Level.Warning, thing + " gives the story item" + (story.Count > 1 ? "s " : " ") + string.Join(", ", story.Distinct().ToArray()) + ", and gives " + (story.Count > 1 ? "them" : "it") + " again " + again +
							": the crew gets a second key or log, and counts and \"uses up\" checks go wrong.",
							ev == "enter" ? "Set the zone to Fires: Once ever - it never fires again."
							: ev == "open" ? "Put the story item in the chest's loot (a chest with story items never fills up again), or set Fills up again: Never."
							: "Set the animals to come back: Never, or give the story item where it happens once (a chest's loot, a note, the quest's reward)."));
					// show/hide and open/close there flip the bridge or door back
					List<ObjAction> flips = acts.Where(a => a.Verb == "toggle" || a.Verb == "switch").ToList();
					if (flips.Count > 0)
						x.Traps.Add(new Finding(-1, Level.Warning, thing + " does " + string.Join(", ", flips.Select(a => (a.Verb == "toggle" ? "show/hide " : "open/close ") + (a.Target.Length > 0 ? "'" + a.Target + "'" : "itself")).ToArray()) +
							" " + (ev == "enter" ? "when a player walks in" : ev == "open" ? "when it is opened" : "when its animals are defeated") + ", and that happens again " + again + ": the " + (flips.Any(a => a.Verb == "toggle") ? "show/hide" : "open/close") + " flips the bridge or door back.",
							(ev == "enter" ? "For a zone the story needs only once (an ambush, a bridge shown): Fires: Once ever - it never fires again. Or: " : "") +
							"in events that can happen again, use show, open, journal page and say - not show/hide or open/close."));
				}
				// Raft item checks on a defeat event: no player sets it off, so it looked at the host's inventory - left out (AU17)
				if (ev == "defeat") ItemChecksTrap(x, p, "defeat", "the defeat event of " + thing);
				// A note's actions run once, for its first reader: Raft items given there go to one player
				if (ObjectProps.IsNote(o.Key.Name, p))
				{
					var items = new List<string>();
					foreach (ObjAction a in ObjAction.ParseLines(ObjectProps.Get(p, BehaviourProps.EventKey("read"))).Where(y => y.Verb == "give"))
						items.AddRange(ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, a.Arg } }).Where(l => !StoryItems.IsStory(l.Key)).Select(l => ContentCatalog.ItemLabel(l.Key) + " \u00D7" + l.Value));
					if (items.Count > 0)
						x.Traps.Add(new Finding(-1, Level.Warning, thing + " gives " + string.Join(", ", items.ToArray()) + " when it is read: a note's actions run once, for its first reader (reading it again shows its messages only) - the other players get nothing.",
							"For something every player should get, use the quest's reward or a chest per player (story items go to the whole crew)."));
				}
			}
			ItemChecksTrap(x, f.Props, "quest", "the island's \"when its quest is done\" event");
		}

		/// <summary>The event ("enter", "defeat", "open", or null) of a zone, creature spot or chest, and when it can happen again
		/// ("" = it can't). Shared with the behaviour window's live warnings.</summary>
		internal static string EventAgain(string objName, IDictionary<string, string> p, bool islandNever, out string ev)
		{
			ev = null;
			string again = "";
			if (objName == ContentCatalog.TriggerZone)
			{
				ev = "enter";
				if (ObjectProps.Repeats(p)) again = "every time a player walks in";
				else if (ObjectProps.Get(p, ObjectProps.ZoneRepeat) != ObjectProps.ZoneOnceEver && !islandNever) again = "when the zone is ready again after the regrow days (Fires: Once)";
			}
			else if (ContentCatalog.IsCreature(objName))
			{
				ev = "defeat";
				if (ObjectProps.Respawns(p) && !islandNever) again = "when its animals come back and are defeated again";
			}
			else if (ObjectProps.IsLoot(objName, p))
			{
				ev = "open";
				if (ObjectProps.LootRefills(p) && !ObjectProps.Loot(p).Any(l => StoryItems.IsStory(l.Key)) && !islandNever) again = "when it fills up again and is opened again";
			}
			return again;
		}

		/// <summary>True when the island never regrows (regrow days 0 or less).</summary>
		internal static bool IslandNeverRegrows(IDictionary<string, string> islandProps)
		{
			int regrow;
			return int.TryParse(ObjectProps.Get(islandProps, IslandProps.RegrowDays), out regrow) && regrow <= 0;
		}

		/// <summary>Raft item checks (has / uses up) on an event no single player sets off (the quest done, a spot's animals
		/// defeated): it fires on the host, and since AU17 such checks are left out there.</summary>
		static void ItemChecksTrap(Facts x, IDictionary<string, string> p, string ev, string what)
		{
			List<ObjCheck> items = ObjCheck.ParseLines(ObjectProps.Get(p, BehaviourProps.CheckKey(ev))).Where(c => c.IsItem && !StoryItems.IsStory(c.Target)).ToList();
			if (items.Count == 0) return;
			x.Traps.Add(new Finding(-1, Level.Warning, what + " checks " + string.Join(", ", items.Select(c => "\"" + c.Describe() + "\"").ToArray()) +
				": no single player sets it off - it fires on the host, whose inventory isn't the player's who did it - so these checks are left out (the host's log says so) and the actions run without them.",
				"Use a story item instead (Island tab > Story items...): story items belong to the whole crew and work in every event. Then + Only if... > uses up item (or has item) > the story item."));
		}

		/// <summary>An object as the builder knows it: the zone's name, the chest's or note's title, the creature kind, and its name.</summary>
		static string Thing(IslandObject o, IDictionary<string, string> p)
		{
			string what, title = ObjectProps.Get(p, ObjectProps.NoteTitle).Trim();
			ContentCatalog.CreatureKind k = ContentCatalog.CreatureOf(o.Name);
			if (o.Name == ContentCatalog.TriggerZone) { string z = ObjectProps.Get(p, ObjectProps.ZoneId).Trim(); what = z.Length > 0 ? "the trigger zone '" + z + "'" : "a trigger zone"; }
			else if (k != null) what = "a " + k.Label.ToLowerInvariant() + " spot";
			else if (ObjectProps.IsLoot(o.Name, p)) what = title.Length > 0 ? "the chest \"" + title + "\"" : "a chest";
			else if (ObjectProps.IsNote(o.Name, p)) what = title.Length > 0 ? "the note \"" + title + "\"" : "a note";
			else what = "an object";
			string name = ObjectProps.Get(p, BehaviourProps.Name).Trim();
			return what + (name.Length > 0 ? " (named '" + name + "')" : "");
		}

		static string Things(List<string> things)
		{
			var l = things.Distinct().ToList();
			return l.Count <= 3 ? string.Join(", ", l.ToArray()) : string.Join(", ", l.Take(3).ToArray()) + " and " + (l.Count - 3) + " more";
		}

		static void CheckBring(Ctx c, int i)
		{
			IntroRule r = c.Plan.Rules[i];
			string arg = r.WhatArg.Trim();
			if (r.What == "island")
			{
				if (arg.Length == 0) c.Add(i, Level.Problem, R(c, i) + " brings an island but doesn't say which.", "Choose it with the ▾ after the island field in BRING.");
				else if (!c.SavedNames.Contains(arg)) c.Add(i, Level.Problem, R(c, i) + " brings '" + arg + "', which isn't a saved island.", "Pick one of your islands with ▾, or save the island under that name.");
				else
				{
					Facts f = Saved(arg);
					if (f != null && f.Objects > 12000) c.Add(i, Level.Warning, R(c, i) + " brings '" + arg + "' with " + f.Objects + " objects: big islands take long to appear and can slow the game.", "Thin it out in the editor (the generator keeps islands under 12 000).");
					if (c.Plan.Rules.Where((x, j) => j != i && x.What == "island" && x.WhatArg.Trim().Equals(arg, StringComparison.OrdinalIgnoreCase)).Any())
						c.Add(i, Level.Tip, R(c, i) + " brings '" + arg + "', and another rule brings it too: the world gets two copies of it.", "Fine if that is what you want; else use another island in one of them.");
				}
			}
			else if (r.What == "oneof")
			{
				var names = r.WhatArg.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
				var missing = names.Where(n => !c.SavedNames.Contains(n)).ToList();
				if (names.Count == 0) c.Add(i, Level.Problem, R(c, i) + " picks one island from a list, but the list is empty.", "Add islands with the ▾ after the list.");
				else if (missing.Count == names.Count) c.Add(i, Level.Problem, R(c, i) + " picks one of " + List(names) + ", but none of them is a saved island.", "Add saved islands with the ▾ after the list.");
				else if (missing.Count > 0) c.Add(i, Level.Warning, R(c, i) + " picks one of a list, but " + List(missing) + (missing.Count == 1 ? " isn't a saved island" : " aren't saved islands") + ": only the others can come.", "Take them out of the list, or save islands with those names.");
			}
			else if (r.What == "type")
			{
				if (MapTypes.Get(arg) == null) c.Add(i, Level.Problem, R(c, i) + " brings a map type '" + arg + "', which doesn't exist.", "Choose one with the ▾ after the map type field.");
			}
			else if (r.What == "pool")
			{
				if (WorldIslands.Candidates().Count == 0) c.Add(i, Level.Problem, R(c, i) + " brings a random island from the spawn pool, but the pool is empty.", "Save some islands, or bring a map type instead.");
			}
		}

		static void CheckWhere(Ctx c, int i)
		{
			IntroRule r = c.Plan.Rules[i];
			// (SL9: the blueprint its dot waits for must be one of Raft's)
			if (r.Needs.Length > 0 && ItemManager.GetAllItems() != null && ItemManager.GetAllItems().Count > 0 && !BlueprintSteps.Exists(r.Needs))
				c.Add(i, Level.Problem, R(c, i) + " shows its dot once players have '" + r.Needs + "', which isn't one of Raft's items: its dot never shows.", "Choose a blueprint in DOT (engine, steering wheel, engine controls).");
			if (r.Distance > 3000f) c.Add(i, Level.Tip, R(c, i) + " puts its island " + r.Distance.ToString("0") + " m away: far enough that players may not find it" + (r.Label.Length == 0 ? " (it has no Receiver name)" : "") + ".", "A message saying which way helps, and a Receiver name shows it on Raft's Receiver.");
			if (r.Where != "near") return;
			bool happened = r.WhereRef.Length == 0 || r.WhereRef.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase) && !c.IslandMode;
			if (happened)
			{
				if (!c.IslandMode && !NeedsIsland(r.When))
					c.Add(i, Level.Problem, R(c, i) + " is placed near \"the island where it happened\", but " + r.DescribeWhen().ToLowerInvariant() + " happens at no island: it never finds a place.", "Name an island after \"of\" in WHERE (▾ lists them).");
				return;
			}
			if (r.WhereRef.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase)) return;
			int idx = c.Plan.Rules.FindIndex(x => x.Id.Equals(r.WhereRef, StringComparison.OrdinalIgnoreCase));
			bool islandRule = idx < 0 && c.IslandRules.ContainsKey(r.WhereRef);
			if (idx < 0 && !islandRule && !c.SavedNames.Contains(r.WhereRef)) { c.Add(i, Level.Problem, R(c, i) + " is placed near '" + r.WhereRef + "': no rule and no saved island has that name.", "Pick the island from the ▾ list after \"of\"."); return; }
			if (idx == i) { c.Add(i, Level.Problem, R(c, i) + " is placed near its own island: it can't be.", "Choose another island after \"of\"."); return; }
			if (idx < 0 && !islandRule && !c.IslandMode && !c.Plan.Rules.Any(x => IslandNamed(x, r.WhereRef)))
				c.Add(i, Level.Warning, R(c, i) + " is placed near the saved island '" + r.WhereRef + "', which no rule of this plan brings: it waits until that island is in the world" + (c.Plan.Random ? " (by chance)" : ", and random islands are off") + ".", "Place it near a rule of this plan instead (its name), or add a rule that brings '" + r.WhereRef + "'.");
		}

		static void CheckTell(Ctx c, int i)
		{
			IntroRule r = c.Plan.Rules[i];
			if (r.Where == "receiver" && r.Label.Length == 0)
				c.Add(i, Level.Tip, R(c, i) + " is found with the Receiver but has no Receiver name: its frequency's note and dot show no name.", "Type a name in TELL \"on the Receiver\".");
			else if (r.Message.Length == 0 && r.Where != "sailing")
				c.Add(i, Level.Tip, R(c, i) + " has no message: players only see that an island appeared and where.", "A line in TELL makes the story (e.g. \"Smoke rises from a small island ahead.\").");
		}

		static void CheckStory(Ctx c, int i)
		{
			IntroRule r = c.Plan.Rules[i];
			// (the user, 2026-10-04: main story islands are found by their coordinates - a frequency on the Receiver)
			if (r.MainStory && r.Where != "receiver")
				c.Add(i, Level.Problem, R(c, i) + " is main story, but it isn't on the Receiver: a main story island needs coordinates (its own frequency) - its tab in Raft's notebook shows them.",
					"WHERE: choose \"On the Receiver\" (or make it a side quest in STORY).");
			if (r.Beside) foreach (Facts f in Brings(c, r)) CheckNotebook(c, i, r, f);
			if (!r.InStory) return;
			bool endsWithUtopia = StoryChain.EndsStory(c.Plan, ChunkPointType.Landmark_Utopia);
			// (Utopia ends Raft's story: it never counts as done, so an island after it never unlocks)
			if (r.StoryPlace.StartsWith("after:") && StoryOrder.Parse(r.StoryPlace.Substring(6)) == ChunkPointType.Landmark_Utopia && endsWithUtopia)
				c.Add(i, Level.Problem, R(c, i) + " comes in the story after Utopia, but Utopia is the end of Raft's story: it never counts as done, so this island never unlocks.", "Place it after Temperance (or in place of Utopia), or leave Utopia out of the story.");
			else if (r.StoryPlace.StartsWith("after:") && StoryOrder.Parse(r.StoryPlace.Substring(6)) == ChunkPointType.None && !c.Plan.Rules.Any(o => o != r && o.InStory && o.Id.Equals(r.StoryPlace.Substring(6), StringComparison.OrdinalIgnoreCase)))
				c.Add(i, endsWithUtopia ? Level.Problem : Level.Warning, R(c, i) + " comes in the story after '" + r.StoryPlace.Substring(6) + "', which isn't in the story: it goes at the end" +
					(endsWithUtopia ? ", after Utopia - which never counts as done, so this island never unlocks." : "."), "Choose its place again in STORY.");
			string kind = r.StoryDone.Split(':')[0], arg = r.StoryDone.Contains(":") ? r.StoryDone.Substring(r.StoryDone.IndexOf(':') + 1) : "";
			foreach (Facts f in Brings(c, r))
			{
				CheckNotebook(c, i, r, f);
				if (kind == "" && !f.Quest.Exists) continue; // (no quest: reaching it counts)
				if (kind == "" || kind == "quest") CheckIslandFor(c, i, "quest", "", f, "STORY");
				else if (kind == "step" || kind == "zone" || kind == "signal" || kind == "note") CheckIslandFor(c, i, kind, arg, f, "STORY");
			}
		}

		/// <summary>Longer than this, a note's text gets small on its paper in Raft's notebook (Raft's own are up to ~350).</summary>
		public const int NotebookNoteLength = 1100;

		/// <summary>A main story island in Raft's notebook (QuestBook): notes for its pages, notes that fit the paper, its story
		/// items' pictures, a tab title that fits.</summary>
		static void CheckNotebook(Ctx c, int i, IntroRule r, Facts f)
		{
			if (f.Sample) return;
			Dictionary<int, KeyValuePair<string, string>> notes = IslandCache.NoteTextsOf(f.Name);
			if (notes.Count == 0)
				c.Add(i, Level.Tip, R(c, i) + " is main story, but '" + f.Name + "' has no notes with a text: its tab in Raft's notebook shows only its intro and quest steps.", "Place notes with a text on the island (they become the tab's pages when read), or write a tab intro (NOTEBOOK).");
			foreach (KeyValuePair<int, KeyValuePair<string, string>> n in notes)
				if (n.Value.Value.Length > NotebookNoteLength)
					c.Add(i, Level.Warning, R(c, i) + ": the note '" + (n.Value.Key.Length > 0 ? n.Value.Key : "#" + n.Key) + "' on '" + f.Name + "' is " + n.Value.Value.Length + " characters long: on its paper in Raft's notebook the writing gets very small.", "Keep notes of main story islands under " + NotebookNoteLength + " characters (split a long one in two).");
			foreach (StoryItemDef d in StoryItems.Of(IslandCache.Props(f.Name)).Where(d => d.Icon.Trim().Length == 0))
				c.Add(i, Level.Tip, R(c, i) + ": the story item '" + d.ShownName + "' of '" + f.Name + "' has no picture: in Raft's 'Found items' it shows an empty slot.", "Give it one of Raft's quest item pictures (Island tab > Story items...).");
			if (r.TabName.Length > 24)
				c.Add(i, Level.Tip, R(c, i) + ": its tab title '" + r.TabName + "' is long - on the tab the writing gets small.", "A short tab title (NOTEBOOK > tab title), up to about 18 letters.");
			if (r.TabColour == 3) c.Add(i, Level.Tip, R(c, i) + ": tab colour 3 isn't one of Raft's (Raft has no third tab picture): it gets a colour by its place.", "Choose a colour from the list (NOTEBOOK).");
		}

		/// <summary>Rules that wait for each other, and rules waiting for a rule that can't work.</summary>
		static void CheckOrder(Ctx c)
		{
			List<IntroRule> rules = c.Plan.Rules;
			Func<int, int> dep = i =>
			{
				IntroRule r = rules[i];
				if (r.When != "rule" && !NeedsIsland(r.When)) return -1;
				return rules.FindIndex(x => x.Id.Length > 0 && x.Id.Equals(r.WhenRef, StringComparison.OrdinalIgnoreCase));
			};
			var reported = new HashSet<int>();
			for (int i = 0; i < rules.Count; i++)
			{
				// (follow what the rule waits for: back to itself = a circle)
				var path = new List<int> { i };
				int j = dep(i);
				while (j >= 0 && !path.Contains(j) && path.Count <= rules.Count) { path.Add(j); j = dep(j); }
				if (j >= 0 && j == i && !reported.Contains(i))
				{
					foreach (int k in path) reported.Add(k);
					c.Add(i, Level.Problem, "Rules " + string.Join(" → ", path.Concat(new[] { i }).Select(k => "'" + (rules[k].Id.Length > 0 ? rules[k].Id : (k + 1).ToString()) + "'").ToArray()) + " wait for each other in a circle: none of them ever comes.",
						"Let one of them start another way (e.g. \"When the world starts\").");
				}
			}
			// A rule waiting for one that can't work, and "near" an island that may not be there yet
			var broken = new HashSet<int>(c.Out.Where(f => f.Level == Level.Problem && f.Rule >= 0).Select(f => f.Rule));
			for (int i = 0; i < rules.Count; i++)
			{
				int d = dep(i);
				if (d >= 0 && d != i && broken.Contains(d) && !broken.Contains(i))
					c.Add(i, Level.Warning, R(c, i) + " waits for rule " + (d + 1) + " '" + rules[d].Id + "', which has a problem (see above): while that isn't fixed, this rule never comes either.", "Fix rule " + (d + 1) + " first.");
				IntroRule r = rules[i];
				if (r.Where == "near" && r.WhereRef.Length > 0)
				{
					int n = rules.FindIndex(x => x.Id.Equals(r.WhereRef, StringComparison.OrdinalIgnoreCase));
					if (n >= 0 && n != i && !WaitsFor(dep, i, n) && !(rules[n].When == "start" && r.When != "start"))
						c.Add(i, Level.Tip, R(c, i) + " is placed near '" + r.WhereRef + "', but doesn't wait for it: if it fires first, it waits until '" + r.WhereRef + "' is in the world.", "Fine if that is what you want; else let its WHEN wait for '" + r.WhereRef + "'.");
				}
			}
		}

		/// <summary>Does rule i (through what it waits for) come after rule n?</summary>
		static bool WaitsFor(Func<int, int> dep, int i, int n)
		{
			int j = dep(i), guard = 0;
			while (j >= 0 && guard++ < 1000) { if (j == n) return true; j = dep(j); }
			return false;
		}

		static IEnumerable<string> StoryTips(WorldPlan plan, bool islandMode)
		{
			if (islandMode || !plan.ChangesStory) yield break;
			List<string> steps = StoryChain.BuildSteps(plan.RaftStory, plan.LeaveOut, plan.Rules);
			yield return "The story, in order: " + (steps.Count == 0 ? "no islands" : string.Join(" > ", steps.Select(s => StoryChain.IsRaft(s) ? StoryOrder.Name(StoryChain.TypeOfStep(s)) : "'" + StoryChain.RuleIdOf(s) + "'").ToArray()));
			if (steps.Count > 0 && steps[0] != StoryChain.RaftKey(ChunkPointType.Landmark_RadioTower)) yield return "Raft's story starts at the Radio Tower (recommended first)";
			if (steps.Count > 0 && steps[steps.Count - 1] != StoryChain.RaftKey(ChunkPointType.Landmark_Utopia)) yield return "Utopia is Raft's ending: without it last, the story has no ending";
			foreach (ChunkPointType t in StoryOrder.Chain.Where(t => !steps.Contains(StoryChain.RaftKey(t))))
			{
				List<string> needed = StoryChain.NeededBlueprintsOn(StoryOrder.Key(t));
				if (needed.Count > 0) yield return "Without " + StoryOrder.Name(t) + " there is no " + string.Join(", ", needed.ToArray()) + " blueprint: put them in a chest or a quest reward";
			}
		}

		static string List(IEnumerable<string> items)
		{
			var l = items.Select(x => "'" + x + "'").ToList();
			return l.Count <= 5 ? string.Join(", ", l.ToArray()) : string.Join(", ", l.Take(5).ToArray()) + " and " + (l.Count - 5) + " more";
		}

		#endregion
	}
}
