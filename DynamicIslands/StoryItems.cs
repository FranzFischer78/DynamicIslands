using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>A story item an island defines: an id (what checks and chests use), a name, a picture and a description.</summary>
	public class StoryItemDef
	{
		public string Id = "", Name = "", Icon = "", Description = "";
		/// <summary>
		/// T8: one of Raft's own items (its unique name) the story item can be taken out of the journal as - "Take out" turns
		/// one into that Raft item in the player's inventory. Empty: it stays a journal item. Story items are never new Raft
		/// items (a save with them would break without the mod); this links one to an item Raft already has.
		/// </summary>
		public string RaftItem = "";
		const string RaftField = "raft=";

		/// <summary>"id|name|icon|description", and "|raft=&lt;Raft item&gt;" at the end for one that can be taken out (T8).</summary>
		public static StoryItemDef Parse(string line)
		{
			string[] p = (line ?? "").Split('|');
			if (p.Length < 1 || p[0].Trim().Length == 0) return null;
			string raft = "";
			int last = p.Length;
			if (p.Length > 4 && p[p.Length - 1].Trim().StartsWith(RaftField, StringComparison.OrdinalIgnoreCase)) { raft = p[p.Length - 1].Trim().Substring(RaftField.Length).Trim(); last--; }
			return new StoryItemDef { Id = p[0].Trim(), Name = p.Length > 1 ? p[1].Trim() : "", Icon = p.Length > 2 ? p[2].Trim() : "", Description = last > 3 ? string.Join("|", p.Skip(3).Take(last - 3).ToArray()).Trim() : "", RaftItem = raft };
		}

		public string ToLine() { return Clean(Id) + "|" + Clean(Name) + "|" + Clean(Icon) + "|" + (Description ?? "").Replace("\n", " ").Trim() + ((RaftItem ?? "").Trim().Length > 0 ? "|" + RaftField + Clean(RaftItem) : ""); }

		static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace("\n", " ").Trim(); }

		public string ShownName { get { return Name.Length > 0 ? Name : Id; } }

		public StoryItemDef Copy() { return new StoryItemDef { Id = Id, Name = Name, Icon = Icon, Description = Description, RaftItem = RaftItem }; }
	}

	/// <summary>
	/// Story items (roadmap 1.3): things of the story that aren't Raft items - a key, a map piece, a captain's log.
	/// An island defines them (setting story.items, one "id|name|icon|description" per line); in a world the crew
	/// holds them together (StoryBook), like Raft's own quest items. Anything that takes items takes them as
	/// "story:&lt;id&gt;": chests, zones, "give" actions and quest rewards give them, "has" and "take" checks look for them.
	/// The picture is one of Raft's quest items ("quest:&lt;type&gt;": keys, key cards, tools...) or any Raft item.
	/// Raft's own quest items can't be used for this: they are a fixed list, and new Raft items would break saves
	/// when the mod is missing.
	/// </summary>
	public static class StoryItems
	{
		public const string Key = "story.items", Prefix = "story:", QuestIcon = "quest:";

		public static bool IsStory(string item) { return item != null && item.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase); }
		public static string IdOf(string item) { return IsStory(item) ? item.Substring(Prefix.Length).Trim() : (item ?? "").Trim(); }
		public static string Ref(string id) { return Prefix + id; }

		public static List<StoryItemDef> Of(IDictionary<string, string> props)
		{
			return ObjectProps.Get(props, Key).Split('\n').Select(StoryItemDef.Parse).Where(d => d != null).ToList();
		}

		public static string Text(IEnumerable<StoryItemDef> defs) { return string.Join("\n", defs.Where(d => d.Id.Length > 0).Select(d => d.ToLine()).ToArray()); }

		/// <summary>A story item's definition: held by the crew, on the island being edited, or on any island of the world.</summary>
		public static StoryItemDef Find(string id)
		{
			id = IdOf(id);
			if (id.Length == 0) return null;
			StoryItemDef d = StoryBook.DefOf(id);
			if (d != null) return d;
			// (Raft's own quest items: "raft-<type>", known without a definition on the island - QuestItemPickups)
			d = QuestItemPickups.Def(id);
			if (d != null) return d;
			if (DynamicIslands.InEditor() && DynamicIslands.currentIslandProps != null)
			{
				d = Of(DynamicIslands.currentIslandProps).FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
				if (d != null) return d;
			}
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				d = Of(IslandCache.PropsOf(e)).FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
				if (d != null) return d;
			}
			return null;
		}

		public static string Label(string item)
		{
			StoryItemDef d = Find(item);
			return d != null ? d.ShownName : IdOf(item);
		}

		/// <summary>A free id for a new story item, from its name ("Old key" -> "old-key", "old-key-2"...).</summary>
		public static string NewId(IEnumerable<StoryItemDef> existing, string name)
		{
			string b = new string((name ?? "item").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
			if (b.Length == 0) b = "item";
			string id = b;
			for (int i = 2; existing.Any(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); i++) id = b + "-" + i;
			return id;
		}

		/// <summary>Gives story items new ids in an island (T8 - a pack whose "key" another installed pack has too): the
		/// definitions, every "story:&lt;id&gt;" in the island's and its objects' settings (chests, zones, behaviours,
		/// rewards), and quest steps that collect the bare id. map: old id -> new id. True when anything changed.</summary>
		public static bool RenameIds(IslandFile f, IDictionary<string, string> map)
		{
			if (f == null || map == null || map.Count == 0) return false;
			var m = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
			bool changed = false;
			if (f.Props != null)
			{
				List<StoryItemDef> defs = Of(f.Props);
				string to;
				foreach (StoryItemDef d in defs) if (m.TryGetValue(d.Id, out to)) { d.Id = to; changed = true; }
				if (changed) f.Props[Key] = Text(defs);
				for (int n = 0; n < IslandQuest.MaxQuests; n++)
				{
					string k = IslandQuest.Key(IslandQuest.KeySteps, n), steps;
					if (!f.Props.TryGetValue(k, out steps) || steps.Length == 0) continue;
					string[] lines = steps.Split('\n');
					bool any = false;
					for (int i = 0; i < lines.Length; i++)
					{
						string[] p = lines[i].Split('|');
						if (p.Length < 2 || p[0] != "collect" || IsStory(p[1]) || !m.TryGetValue(p[1].Trim(), out to)) continue;
						p[1] = to;
						lines[i] = string.Join("|", p);
						any = true;
					}
					if (any) { f.Props[k] = string.Join("\n", lines); changed = true; }
				}
				changed |= RenameRefs(f.Props, m);
			}
			foreach (IslandObject o in f.Objects) if (o.Props != null) changed |= RenameRefs(o.Props, m);
			return changed;
		}

		static bool RenameRefs(Dictionary<string, string> props, Dictionary<string, string> map)
		{
			bool changed = false;
			foreach (string k in props.Keys.ToList())
			{
				string v = props[k];
				if (v == null || v.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase) < 0) continue;
				// ("story:<id>" as a whole: "story:key" isn't part of "story:key-2")
				string nv = System.Text.RegularExpressions.Regex.Replace(v, @"(?<![A-Za-z0-9_\-])story:([A-Za-z0-9_\-]+)(?![A-Za-z0-9_\-])",
					x => { string to; return map.TryGetValue(x.Groups[1].Value, out to) ? Prefix + to : x.Value; }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				if (nv != v) { props[k] = nv; changed = true; }
			}
			return changed;
		}

		#region Pictures

		static SO_QuestItem[] questItems;

		/// <summary>Raft's quest items (their pictures are story item pictures).</summary>
		public static SO_QuestItem[] QuestItems
		{
			get
			{
				if (questItems == null)
				{
					try { questItems = Resources.LoadAll<SO_QuestItem>("SO_QuestItems").Where(q => q != null && q.itemImage != null).ToArray(); }
					catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's quest items: " + e.Message); questItems = new SO_QuestItem[0]; }
				}
				return questItems;
			}
		}

		public static Sprite IconSprite(string icon)
		{
			if (string.IsNullOrEmpty(icon)) return null;
			if (icon.StartsWith(QuestIcon))
			{
				string type = icon.Substring(QuestIcon.Length);
				SO_QuestItem q = QuestItems.FirstOrDefault(x => x.questItemType.ToString() == type);
				return q != null ? q.itemImage : null;
			}
			return ContentCatalog.ItemSprite(icon);
		}

		#endregion
	}

	/// <summary>
	/// The crew's story items and journal pages in a world. Saved in the world's island list file (@story.item and
	/// @story.page lines), kept by the host and sent to everyone (IslandNetMessage.Story): a client asks the host for
	/// a change and shows it at once; the host's copy wins. Pages come from custom notes (the first time someone
	/// reads one) and from "journal" actions.
	/// </summary>
	public static class StoryBook
	{
		public class Held
		{
			public StoryItemDef Def;
			public int Count;
			/// <summary>How many the crew has found in all, also those used up since (ROADMAP E12: a "find N" step counts these).</summary>
			public int Found;
			/// <summary>How many were found on each island copy (PageIsland: "Cove", "Cove|2"; given away from an island: not
			/// here). Two copies of one island give the same story items: a collect step on one counts the crew's finds but not
			/// those of the other copies (they were for its own quest).</summary>
			public Dictionary<string, int> From = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		}

		public class Page
		{
			public string Key = "", Title = "", Text = "", Island = "";
			public int Day;
		}

		static readonly Dictionary<string, Held> held = new Dictionary<string, Held>(StringComparer.OrdinalIgnoreCase);
		static readonly List<Page> pages = new List<Page>();

		/// <summary>Raised on every machine when the items or pages change (the journal and tests listen).</summary>
		public static event Action Changed;

		public static IEnumerable<Held> Items { get { return held.Values.Where(h => h.Count > 0).OrderBy(h => h.Def.ShownName, StringComparer.OrdinalIgnoreCase); } }
		/// <summary>Every story item the crew has found, also ones used up since (kept at 0, saved with the world).</summary>
		public static IEnumerable<string> FoundIds { get { return held.Keys; } }
		public static IList<Page> Pages { get { return pages; } }
		public static bool HasState { get { return held.Count > 0 || pages.Count > 0; } }

		/// <summary>
		/// The island's part of its page keys ("note:&lt;island&gt;:n", "act:&lt;island&gt;:..."): its name, and for a second
		/// copy of the same island in the world its name and "|2" (and so on: no file name has a "|"). Two copies shared their
		/// pages - a note read on one was found on the other, and its own reading added nothing (AU41). The first copy keeps
		/// the plain name, as in worlds saved before. Copies count in the world's list order (kept in saves, and the host's
		/// order on every machine).
		/// </summary>
		public static string PageIsland(IslandWorldState.Entry e)
		{
			if (e == null) return "";
			int copy = 1;
			foreach (IslandWorldState.Entry x in IslandWorldState.Islands)
			{
				if (x == e) break;
				if (x.HostName.Equals(e.HostName, StringComparison.OrdinalIgnoreCase)) copy++;
			}
			return copy == 1 ? e.HostName : e.HostName + "|" + copy.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// How many of a story item (null: of every item, as held now) a collect step on island e may not count: those found on
		/// the other copies of the same island (PageIsland). 0 when the island has no other copy, or for finds from before
		/// 2026-10-23 (no island kept: counted for every copy, as before).
		/// </summary>
		public static int FoundOnOtherCopies(string id, IslandWorldState.Entry e)
		{
			if (e == null || e.HostName == null) return 0;
			string own = PageIsland(e), name = e.HostName;
			Func<Held, int> other = h => h.From.Where(kv => !kv.Key.Equals(own, StringComparison.OrdinalIgnoreCase) && IslandPart(kv.Key).Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(kv => kv.Value);
			if (id != null)
			{
				Held h;
				return held.TryGetValue(StoryItems.IdOf(id), out h) ? other(h) : 0;
			}
			// (every item, as held now: each item's other copies' finds, at most what is held of it)
			return held.Values.Sum(h => Mathf.Min(h.Count, other(h)));
		}

		/// <summary>The island's name in a PageIsland ("Cove|2" -> "Cove").</summary>
		static string IslandPart(string pageIsland)
		{
			int bar = (pageIsland ?? "").IndexOf('|');
			return bar >= 0 ? pageIsland.Substring(0, bar) : pageIsland ?? "";
		}

		/// <summary>The custom island this machine's player is on or at (its land and 60 m around), or null.</summary>
		internal static IslandWorldState.Entry IslandHere()
		{
			Network_Player p;
			try { p = RAPI.GetLocalPlayer(); } catch { return null; }
			if (p == null) return null;
			IslandWorldState.Entry best = null;
			float bestOut = 60f;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null) continue;
				IslandInfoTag tag = e.Root.GetComponent<IslandInfoTag>();
				Vector3 c = tag != null ? e.Root.transform.position + tag.LocalCentre : e.Position;
				float r = tag != null ? tag.Radius : Mathf.Max(30f, CustomIslandSpawner.LandRadius(e.Name));
				float outside = new Vector2(c.x - p.transform.position.x, c.z - p.transform.position.z).magnitude - r;
				if (outside < bestOut) { bestOut = outside; best = e; }
			}
			return best;
		}

		/// <summary>The island a page key's island part names (PageIsland): that copy, else the first island of the name; null if none.</summary>
		public static IslandWorldState.Entry EntryOfPageIsland(string pageIsland)
		{
			string name = pageIsland ?? "";
			int copy = 1, bar = name.IndexOf('|');
			if (bar >= 0) { if (!int.TryParse(name.Substring(bar + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out copy)) copy = 1; name = name.Substring(0, bar); }
			List<IslandWorldState.Entry> same = IslandWorldState.Islands.Where(x => x.HostName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
			return copy >= 1 && copy <= same.Count ? same[copy - 1] : same.FirstOrDefault();
		}

		/// <summary>A page key with the copy number taken out ("note:Cove|2:5" -> "note:Cove:5"): for counts by island name.</summary>
		public static string PlainKey(string key)
		{
			key = key ?? "";
			int a = key.IndexOf(':'), b = a >= 0 ? key.IndexOf(':', a + 1) : -1, bar = a >= 0 ? key.IndexOf('|', a + 1) : -1;
			return bar > a && bar < b ? key.Substring(0, bar) + key.Substring(b) : key;
		}

		public static int Count(string id)
		{
			Held h;
			return held.TryGetValue(StoryItems.IdOf(id), out h) ? h.Count : 0;
		}

		/// <summary>How many of a story item the crew has found in all (used up or not).</summary>
		public static int FoundCount(string id)
		{
			Held h;
			return held.TryGetValue(StoryItems.IdOf(id), out h) ? Mathf.Max(h.Found, h.Count) : 0;
		}

		public static StoryItemDef DefOf(string id)
		{
			Held h;
			return held.TryGetValue(StoryItems.IdOf(id), out h) ? h.Def : null;
		}

		static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		public static void Reset()
		{
			held.Clear();
			pages.Clear();
			Raise();
		}

		static void Raise() { if (Changed != null) try { Changed(); } catch { } }

		#region Changes

		/// <summary>The crew gets story items (this machine's player found them: they are told). from: the island they came
		/// from; null = the island this machine's player is at, if any.</summary>
		public static void Give(string id, int count) { Give(id, count, null); }

		public static void Give(string id, int count, IslandWorldState.Entry from)
		{
			id = StoryItems.IdOf(id);
			if (id.Length == 0 || count <= 0) return;
			StoryItemDef d = StoryItems.Find(id) ?? new StoryItemDef { Id = id };
			if (from == null) from = IslandHere();
			Change("give", Fields(d.Id, count.ToString(CultureInfo.InvariantCulture), d.Name, d.Icon, d.Description, d.RaftItem, from != null ? PageIsland(from) : ""));
			// (the main story's items are in Raft's notebook's Found items, the side quests' in the journal)
			IslandInfo.ShowMessage("Story item: " + d.ShownName + (count > 1 ? " \u00D7" + count : "") + (QuestBook.MainItemIds().Contains(d.Id) ? "   (T: notebook)" : "   (J: journal)"));
		}

		/// <summary>The crew's story items are used up (a "take" check passed).</summary>
		public static void Take(string id, int count)
		{
			id = StoryItems.IdOf(id);
			if (id.Length == 0 || count <= 0) return;
			Change("take", Fields(id, count.ToString(CultureInfo.InvariantCulture)));
		}

		/// <summary>A page in the journal (a page with the same key is only added once).</summary>
		public static void AddPage(string key, string title, string text, string island)
		{
			if (pages.Any(p => p.Key == key)) return;
			Change("page", Fields(key, Today.ToString(CultureInfo.InvariantCulture), title ?? "", island ?? "", text ?? ""));
		}

		/// <summary>Whether a held story item can be taken out as a Raft item (T8): it names one Raft has, and the crew holds one.</summary>
		public static bool CanTakeOut(string id)
		{
			StoryItemDef d = DefOf(id);
			return d != null && Count(id) > 0 && RaftItemOf(d) != null;
		}

		static Item_Base RaftItemOf(StoryItemDef d)
		{
			string name = d != null ? (d.RaftItem ?? "").Trim() : "";
			if (name.Length == 0) return null;
			try { return ItemManager.GetItemByName(name); } catch { return null; }
		}

		/// <summary>
		/// T8: one of the crew's story items taken out of the journal as the Raft item it names, into this player's
		/// inventory. The host decides (two players taking out the last one at once get one item between them): a client
		/// asks ("out") and gets the item when the host says yes ("outok"); on the host it happens at once.
		/// </summary>
		public static bool TakeOut(string id)
		{
			id = StoryItems.IdOf(id);
			if (!CanTakeOut(id)) return false;
			if (Raft_Network.IsHost)
			{
				string raft = DefOf(id).RaftItem.Trim();
				Change("take", Fields(id, "1"));
				GiveRaftItem(id, raft);
			}
			else IslandNetwork.SendStory(new IslandNetMessage { Name = "out", Data = Fields(id) });
			return true;
		}

		static void GiveRaftItem(string id, string raft)
		{
			List<string> given = TriggerZone.Give(new[] { new KeyValuePair<string, int>(raft, 1) });
			Debug.Log("[CUSTOM ISLANDS] Story item '" + id + "' taken out as " + raft + (given.Count > 0 ? "" : " (not given)"));
			if (given.Count > 0) IslandInfo.ShowMessage("Taken out of the journal: " + given[0]);
		}

		static void Change(string op, string data)
		{
			bool changed = Apply(op, data);
			var msg = new IslandNetMessage { Name = op, Data = data };
			if (Raft_Network.IsHost) { if (changed) IslandNetwork.SendStory(StateMessage()); }
			else IslandNetwork.SendStory(msg);
			if (changed) Raise();
		}

		/// <summary>The most journal pages and story item kinds a world keeps, and the longest page (CB12).</summary>
		internal const int MaxPages = 2000, MaxItems = 500, MaxTitle = 200, MaxPageText = 8000;
		/// <summary>The most island copies one story item's finds are kept for.</summary>
		const int MaxSources = 64;

		static bool Apply(string op, string data)
		{
			string[] f = Split(data);
			int n;
			switch (op)
			{
				case "give":
					if (f.Length < 2 || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return false;
					Held h;
					if (!held.TryGetValue(f[0], out h))
					{
						if (held.Count >= MaxItems) { Debug.LogWarning("[CUSTOM ISLANDS] Story item '" + f[0] + "' left out: the crew holds " + MaxItems + " kinds already"); return false; }
						held[f[0]] = h = new Held { Def = new StoryItemDef { Id = f[0] } };
					}
					if (f.Length >= 5)
					{
						// (what the giver knew about it; empty fields keep what is known)
						if (f[2].Length > 0) h.Def.Name = f[2];
						if (f[3].Length > 0) h.Def.Icon = f[3];
						if (f[4].Length > 0) h.Def.Description = f[4];
						if (f.Length >= 6 && f[5].Length > 0) h.Def.RaftItem = f[5];
					}
					h.Count += n;
					h.Found = Mathf.Max(h.Found, h.Count - n) + n;
					// (the island copy it was found on: the 7th field since 2026-10-23)
					if (f.Length >= 7 && f[6].Length > 0 && (h.From.Count < MaxSources || h.From.ContainsKey(f[6])))
					{
						int had;
						h.From.TryGetValue(f[6], out had);
						h.From[f[6]] = had + n;
					}
					Debug.Log("[CUSTOM ISLANDS] Story item '" + h.Def.ShownName + "': " + h.Count);
					return true;
				case "take":
					Held t;
					if (f.Length < 2 || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || !held.TryGetValue(f[0], out t) || t.Count <= 0) return false;
					t.Count = Mathf.Max(0, t.Count - n);
					Debug.Log("[CUSTOM ISLANDS] Story item '" + t.Def.ShownName + "' used: " + t.Count + " left");
					return true;
				case "page":
					if (f.Length < 5 || pages.Any(p => p.Key == f[0])) return false;
					// (CB12: a world's journal stays readable and its state message small, whatever a player sends)
					if (pages.Count >= MaxPages) { Debug.LogWarning("[CUSTOM ISLANDS] Journal page '" + f[2] + "' left out: the journal holds " + MaxPages + " pages already"); return false; }
					if (f[2].Length > MaxTitle) f[2] = f[2].Substring(0, MaxTitle);
					if (f[4].Length > MaxPageText) f[4] = f[4].Substring(0, MaxPageText);
					int day;
					int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out day);
					pages.Add(new Page { Key = f[0], Day = day, Title = f[2], Island = f[3], Text = f[4] });
					Debug.Log("[CUSTOM ISLANDS] Journal page '" + f[2] + "'");
					return true;
			}
			return false;
		}

		/// <summary>From the network.</summary>
		public static void OnMessage(IslandNetMessage msg) { OnMessage(msg, 0UL); }

		/// <summary>Host: a player's "take" the crew no longer had enough for, by player and item, and when (TakeRefused).</summary>
		static readonly Dictionary<string, float> refusedTakes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		const float RefusedTakeSeconds = 10f;

		/// <summary>
		/// Host: whether this player's last take of the item was refused in the last few seconds (forgotten once asked). Two
		/// players using the last story key within the network's delay both passed their own check; the host took it for the
		/// first one only, and the second one's event must not run its shared part too (AU18).
		/// </summary>
		internal static bool TakeRefused(ulong player, string id)
		{
			string k = player + "/" + StoryItems.IdOf(id);
			float t;
			if (!refusedTakes.TryGetValue(k, out t)) return false;
			refusedTakes.Remove(k);
			return Time.unscaledTime - t < RefusedTakeSeconds && Time.unscaledTime >= t;
		}

		/// <summary>From the network (from: the player who sent it, for a client's take the host refuses).</summary>
		public static void OnMessage(IslandNetMessage msg, ulong from) { OnMessage(msg, from, null); }

		/// <summary>From the network (fromUser: the sender, for the host's answer to a take-out).</summary>
		public static void OnMessage(IslandNetMessage msg, ulong from, Network_UserId? fromUser)
		{
			// (T8: the host's answer to this player's take-out: the Raft item, or no - the crew had none left)
			if (msg.Name == "outok" || msg.Name == "outno")
			{
				if (Raft_Network.IsHost) return;
				string[] o = Split(msg.Data);
				if (msg.Name == "outok" && o.Length >= 2) GiveRaftItem(o[0], o[1]);
				else IslandInfo.ShowMessage("The crew has none of it left to take out");
				return;
			}
			if (msg.Name == "all")
			{
				if (Raft_Network.IsHost) return;
				held.Clear();
				pages.Clear();
				foreach (string line in (msg.Data ?? "").Split('\n'))
				{
					int eq = line.IndexOf('=');
					if (eq > 0) ReadLine(line.Substring(0, eq), line.Substring(eq + 1));
				}
				Raise();
				return;
			}
			if (!Raft_Network.IsHost) return;
			if (msg.Name == "out")
			{
				// (T8: a player takes a story item out as a Raft item: one is taken here, and only they get the item)
				string[] o = Split(msg.Data);
				string oid = o.Length > 0 ? StoryItems.IdOf(o[0]) : "";
				bool ok = CanTakeOut(oid);
				string raft = ok ? DefOf(oid).RaftItem.Trim() : "";
				if (ok && Apply("take", Fields(oid, "1"))) { IslandNetwork.SendStory(StateMessage()); Raise(); }
				Debug.Log("[CUSTOM ISLANDS] A player's take-out of story item '" + oid + "': " + (ok ? "given as " + raft : "refused (none left)"));
				if (fromUser.HasValue) IslandNetwork.SendStoryTo(new IslandNetMessage { Name = ok ? "outok" : "outno", Data = Fields(oid, raft) }, fromUser.Value);
				return;
			}
			// (a take the crew no longer has enough for - another player used the item up at the same moment: refused, not
			// partly taken, and remembered for that player's event; everyone gets the state again, so their count is right)
			if (msg.Name == "take")
			{
				string[] f = Split(msg.Data);
				int n;
				if (f.Length >= 2 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0 && Count(f[0]) < n)
				{
					refusedTakes[from + "/" + StoryItems.IdOf(f[0])] = Time.unscaledTime;
					Debug.Log("[CUSTOM ISLANDS] A player's use of story item '" + f[0] + "' x" + n + " is refused: the crew has " + Count(f[0]) + " (another player used it at the same moment)");
					IslandNetwork.SendStory(StateMessage());
					return;
				}
			}
			// The host: a client's change; everyone gets the new state
			if (Apply(msg.Name ?? "", msg.Data ?? "")) { IslandNetwork.SendStory(StateMessage()); Raise(); }
		}

		public static IslandNetMessage StateMessage()
		{
			return new IslandNetMessage { Kind = IslandNetMessage.Story, Name = "all", Data = string.Join("\n", Lines().ToArray()) };
		}

		#endregion

		#region Saving (in the world's island list file)

		static IEnumerable<string> Lines()
		{
			foreach (Held h in held.Values)
				yield return "story.item=" + Fields(h.Def.Id, h.Count.ToString(CultureInfo.InvariantCulture), h.Def.Name, h.Def.Icon, h.Def.Description, Mathf.Max(h.Found, h.Count).ToString(CultureInfo.InvariantCulture), h.Def.RaftItem, EncodeFrom(h.From));
			foreach (Page p in pages)
				yield return "story.page=" + Fields(p.Key, p.Day.ToString(CultureInfo.InvariantCulture), p.Title, p.Island, p.Text);
		}

		/// <summary>Lines for the world's file ("@" + key = value).</summary>
		public static IEnumerable<string> WriteLines() { return Lines().Select(l => "@" + l); }

		/// <summary>A line of the world's file; true if it was a story line.</summary>
		public static bool ReadLine(string key, string value)
		{
			string[] f = Split(value);
			int n;
			if (key == "story.item")
			{
				if (f.Length < 2 || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return true;
				int found;
				// (found in all: the 6th field since 2026-10-06; a world from before counts what it holds)
				if (f.Length < 6 || !int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out found)) found = n;
				held[f[0]] = new Held { Count = n, Found = Mathf.Max(found, n), Def = new StoryItemDef { Id = f[0], Name = f.Length > 2 ? f[2] : "", Icon = f.Length > 3 ? f[3] : "", Description = f.Length > 4 ? f[4] : "", RaftItem = f.Length > 6 ? f[6] : "" }, From = DecodeFrom(f.Length > 7 ? f[7] : "") };
				return true;
			}
			if (key == "story.page")
			{
				if (f.Length < 5) return true;
				int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
				if (!pages.Any(p => p.Key == f[0])) pages.Add(new Page { Key = f[0], Day = n, Title = f[2], Island = f[3], Text = f[4] });
				return true;
			}
			return false;
		}

		/// <summary>The island copies an item was found on, as "Cove=2;Cove%7C2=1" (names escaped).</summary>
		static string EncodeFrom(Dictionary<string, int> from)
		{
			return string.Join(";", from.Where(kv => kv.Value > 0).Select(kv => Uri.EscapeDataString(kv.Key) + "=" + kv.Value.ToString(CultureInfo.InvariantCulture)).ToArray());
		}

		static Dictionary<string, int> DecodeFrom(string text)
		{
			var from = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (string part in (text ?? "").Split(';'))
			{
				int eq = part.LastIndexOf('='), n;
				if (eq <= 0 || !int.TryParse(part.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) continue;
				string island;
				try { island = Uri.UnescapeDataString(part.Substring(0, eq)); } catch { continue; }
				if (from.Count < MaxSources) from[island] = n;
			}
			return from;
		}

		/// <summary>Fields joined by '|', each escaped (texts may hold '|', '=' and new lines).</summary>
		static string Fields(params string[] fields) { return string.Join("|", fields.Select(x => Uri.EscapeDataString(x ?? "")).ToArray()); }

		static string[] Split(string data) { return (data ?? "").Split('|').Select(x => { try { return Uri.UnescapeDataString(x); } catch { return x; } }).ToArray(); }

		#endregion
	}

	/// <summary>
	/// The crew's journal in a world (J): the story items with their pictures, and the pages found (custom notes
	/// read, "journal" actions). In the look of Raft's menus, with the page on paper.
	/// </summary>
	/// <summary>
	/// The world's custom quests, for the journal's count (the user, 2026-10-02: "35/100% completed quests"; 2026-10-03:
	/// "it should count all quest islands, not the original Raft quests" - the journal is the custom islands' book):
	/// - the plan's islands in the story (done as the story counts them: their quest, by default) and the saved islands its
	///   other rules bring that have a quest - known from the start of the world, also before they come;
	/// - every other island with a quest that has come: by chance, a map type's, an island's own rule, the randomizer's.
	/// An island counts once, however many rules or copies name it.
	/// </summary>
	public static class QuestCount
	{
		public const string PlanStory = "The plan's story", PlanIslands = "The plan's other islands", Met = "Other islands with a quest",
			ToCome = "Islands with a quest still to come";

		public class Quest
		{
			public string Name = "", Group = "";
			public bool Done;
		}

		// (CW3: the host counts and every player shows its count - each counting from its own copy of the world, players
		// saw other numbers than the host)
		static List<Quest> fromHost;
		static string lastSent;
		static float nextCount;

		/// <summary>Every custom quest of this world, in order: the plan's islands in the story, the plan's other islands, the
		/// rest - the host's list on a player's machine (ROADMAP CW3), this machine's own count on the host.</summary>
		public static List<Quest> All()
		{
			if (!Raft_Network.IsHost && fromHost != null && LoadSceneManager.IsGameSceneLoaded) return fromHost.Select(q => new Quest { Name = q.Name, Group = q.Group, Done = q.Done }).ToList();
			return CountHere();
		}

		/// <summary>The host's list as a message (Data: "group\tname\t1|0" lines).</summary>
		internal static IslandNetMessage Message()
		{
			return new IslandNetMessage { Kind = IslandNetMessage.QuestCount, Data = Text(CountHere()) };
		}

		static string Text(List<Quest> quests) { return string.Join("\n", quests.Select(q => Clean(q.Group) + "\t" + Clean(q.Name) + "\t" + (q.Done ? "1" : "0")).ToArray()); }

		static string Clean(string s) { return (s ?? "").Replace("\t", " ").Replace("\n", " "); }

		/// <summary>A player: the host's count arrived.</summary>
		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			var list = new List<Quest>();
			foreach (string line in (msg.Data ?? "").Split('\n'))
			{
				string[] p = line.Split('\t');
				if (p.Length < 3) continue;
				list.Add(new Quest { Group = p[0], Name = p[1], Done = p[2] == "1" });
			}
			fromHost = list;
		}

		/// <summary>A player's world was left or another came: the host's count goes until the next arrives.</summary>
		internal static void Reset() { fromHost = null; lastSent = null; }

		/// <summary>Host, every few seconds: the count sent to everyone when it changed.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextCount) return;
			nextCount = Time.unscaledTime + 5f;
			if (!LoadSceneManager.IsGameSceneLoaded) { Reset(); return; }
			if (!Raft_Network.IsHost) return;
			string text = Text(CountHere());
			if (text == lastSent) return;
			lastSent = text;
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.QuestCount, Data = text });
		}

		/// <summary>This machine's own count (the host's, the one it sends).</summary>
		internal static List<Quest> CountHere()
		{
			var list = new List<Quest>();
			var counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			WorldPlan plan = WorldDirector.Plan;
			if (StoryChain.Active && StoryChain.Steps.Count > 0)
			{
				// (a plan changed the story: the chain says what is done - its own islands only, not Raft's)
				foreach (string step in StoryChain.Steps)
				{
					if (StoryChain.IsRaft(step)) continue;
					string id = StoryChain.RuleIdOf(step);
					IntroRule r = StoryChain.RuleOf(id) ?? (plan != null ? plan.Rules.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) : null);
					List<IslandWorldState.Entry> at = WorldDirector.Refs(id, null);
					if (at.Count > 0) counted.Add(at[0].HostName);
					if (r != null && r.What == "island" && r.WhatArg.Length > 0) counted.Add(r.WhatArg);
					list.Add(new Quest { Name = NameOf(at, r, id), Group = PlanStory, Done = StoryChain.Done.Contains(step) });
				}
			}
			// The plan's other rules that bring a saved island with a quest (known before they come); main story islands beside
			// Raft's story count with the story
			if (plan != null)
				foreach (IntroRule r in plan.Rules.Where(x => !x.InStory && x.What == "island" && x.WhatArg.Length > 0).OrderBy(x => x.Beside ? 0 : 1))
				{
					if (counted.Contains(r.WhatArg)) continue;
					List<IslandWorldState.Entry> at = WorldDirector.Refs(r.Id, null);
					if (at.Count == 0) at = WorldDirector.Refs(r.WhatArg, null);
					IslandQuest q = IslandQuest.From(at.Count > 0 ? IslandCache.PropsOf(at[0]) : IslandCache.Props(r.WhatArg));
					if (q.Steps.Count == 0) continue;
					counted.Add(r.WhatArg);
					if (at.Count > 0) counted.Add(at[0].HostName);
					list.Add(new Quest { Name = NameOf(at, r, r.Id), Group = r.Beside ? PlanStory : PlanIslands, Done = at.Any(e => QuestTracker.StepOf(e) >= q.Steps.Count) });
				}
			// Every other island with a quest that has come
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Failed || counted.Contains(e.HostName)) continue;
				IslandQuest q = IslandQuest.From(IslandCache.PropsOf(e));
				if (q.Steps.Count == 0) continue;
				counted.Add(e.HostName);
				List<IslandWorldState.Entry> same = IslandWorldState.Islands.Where(x => x.HostName.Equals(e.HostName, StringComparison.OrdinalIgnoreCase)).ToList();
				list.Add(new Quest { Name = Behaviours.IslandTitle(e) + (q.Title.Length > 0 ? " \u2013 " + q.Title : ""), Group = same.Any(QuestBook.IsMainIsland) ? PlanStory : Met, Done = same.Any(x => QuestTracker.StepOf(x) >= q.Steps.Count) });
			}
			// The islands this world takes part with while sailing that have a quest and haven't come yet (ROADMAP CT9 - the user,
			// 2026-10-06: 47 islands ticked, the journal said 1/12: only the plan's and the islands that had come were counted)
			if (CustomIslandSpawner.Enabled && (plan == null || plan.Random))
			{
				// (a copy of a main story island in the pool - a library import's "Thornwood (The Long Voyage)" - is the story's,
				// told in Raft's notebook, not a side quest still to come: ROADMAP X8)
				var mainTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (IntroRule r in StoryChain.BookRules.Where(x => x.What == "island" && x.WhatArg.Length > 0))
				{
					string t;
					mainTitles.Add(r.WhatArg);
					if (IslandCache.Props(r.WhatArg).TryGetValue(IslandProps.Title, out t) && !string.IsNullOrEmpty(t)) mainTitles.Add(t);
				}
				foreach (string name in StillToCome())
				{
					if (counted.Contains(name) || IslandWorldState.Islands.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
					IslandQuest q = IslandQuest.From(IslandCache.Props(name));
					if (q.Steps.Count == 0) continue;
					counted.Add(name);
					string title;
					IslandCache.Props(name).TryGetValue(IslandProps.Title, out title);
					if (mainTitles.Contains(string.IsNullOrEmpty(title) ? name : title)) continue;
					list.Add(new Quest { Name = (string.IsNullOrEmpty(title) ? name : title) + (q.Title.Length > 0 ? " – " + q.Title : ""), Group = ToCome });
				}
			}
			// The islands' further quests (ROADMAP LM4), each on its own line
			var further = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Failed || !further.Add(e.HostName)) continue;
				int count = QuestTracker.QuestsOf(e);
				List<IslandWorldState.Entry> same = IslandWorldState.Islands.Where(x => x.HostName.Equals(e.HostName, StringComparison.OrdinalIgnoreCase)).ToList();
				for (int n = 1; n < count; n++)
				{
					IslandQuest q = QuestTracker.QuestOf(e, n);
					if (!q.Exists) continue;
					int qn = n;
					list.Add(new Quest { Name = Behaviours.IslandTitle(e) + " \u2013 " + (q.Title.Length > 0 ? q.Title : "quest " + (n + 1)), Group = same.Any(QuestBook.IsMainIsland) ? PlanStory : Met, Done = same.Any(x => QuestTracker.IsDone(x, qn)) });
				}
			}
			return list;
		}

		static List<string> toCome = new List<string>();
		static float toComeAt = -100f;

		/// <summary>The saved islands of this world's random pool (looked at again every 10 s at most: Pool reads the folder).</summary>
		internal static List<string> StillToCome()
		{
			if (Time.unscaledTime - toComeAt < 10f) return toCome;
			toComeAt = Time.unscaledTime;
			return toCome = CustomIslandSpawner.Pool(true).Select(p => p.Key).Where(n => n != CustomIslandSpawner.GeneratedEntry && !n.StartsWith(CustomIslandSpawner.TypePrefix, StringComparison.OrdinalIgnoreCase)).ToList();
		}

		/// <summary>The saved islands ticked for this world's sailing that can still come, when the world brings islands by
		/// chance (no plan, or a plan with random islands) - what the Quests count adds (CT9), also for the journal's islands,
		/// notes, story items and pages (CA31). Host only: the choice is the host's, a player's own pool isn't this world's.</summary>
		internal static List<string> TickedToCome()
		{
			WorldPlan plan = WorldDirector.Plan;
			if (!Raft_Network.IsHost || !CustomIslandSpawner.Enabled || (plan != null && !plan.Random)) return new List<string>();
			return StillToCome().Where(n => !IslandWorldState.Islands.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
		}

		/// <summary>Dev tests: the next StillToCome reads the pool again.</summary>
		internal static void Recount() { toComeAt = -100f; }

		/// <summary>"Wreckers' Cove - The False Light": the island (as it came, or as the rule names it) and its quest.</summary>
		static string NameOf(List<IslandWorldState.Entry> at, IntroRule r, string id)
		{
			if (at.Count > 0)
			{
				IslandQuest q = IslandQuest.From(IslandCache.PropsOf(at[0]));
				return Behaviours.IslandTitle(at[0]) + (q.Title.Length > 0 ? " \u2013 " + q.Title : "");
			}
			if (r != null && r.What == "island" && r.WhatArg.Length > 0)
			{
				Dictionary<string, string> props = IslandCache.Props(r.WhatArg);
				string title = ObjectProps.Get(props, IslandProps.Title);
				IslandQuest q = IslandQuest.From(props);
				return (title.Length > 0 ? title : r.WhatArg) + (q.Title.Length > 0 ? " \u2013 " + q.Title : "");
			}
			return r != null && r.Label.Length > 0 ? r.Label : "'" + id + "'";
		}

		/// <summary>Done and all.</summary>
		public static void Count(List<Quest> quests, out int done, out int total)
		{
			done = quests.Count(q => q.Done);
			total = quests.Count;
		}

		/// <summary>"7 of 20 quests done (35%)"</summary>
		public static string Summary(List<Quest> quests)
		{
			int done, total;
			Count(quests, out done, out total);
			return total == 0 ? "No quests yet" : done + " of " + total + " quests done (" + Percent(done, total) + "%)";
		}

		public static int Percent(int done, int total) { return total == 0 ? 0 : Mathf.FloorToInt(100f * done / total); }
	}

	/// <summary>
	/// The world's notes for the journal ("5 / 38 notes found", the user, 2026-10-03): every note with a text on the
	/// islands in the world and on the saved islands the world plan names (also before they come) - found once its page is
	/// in the journal. An island made new from a map type counts once it has come. Raft's own notes are Raft's notebook's.
	/// </summary>
	public static class NoteCount
	{
		public static void Count(out int found, out int total)
		{
			found = total = 0;
			// (island name as the journal's page keys have it -> its file)
			var islands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
				if (!e.Failed && !islands.ContainsKey(e.HostName)) islands[e.HostName] = e.Name;
			IEnumerable<IntroRule> rules = (WorldDirector.Plan != null ? WorldDirector.Plan.Rules : new List<IntroRule>()).Concat(StoryChain.Rules);
			foreach (IntroRule r in rules)
				if (r.What == "island" && r.WhatArg.Length > 0 && !islands.ContainsKey(r.WhatArg)) islands[r.WhatArg] = r.WhatArg;
			foreach (string name in QuestCount.TickedToCome())   // (CA31)
				if (!islands.ContainsKey(name)) islands[name] = name;
			// (by island name: a note found on any copy of the island - AU41 page keys)
			var pages = new HashSet<string>(StoryBook.Pages.Select(p => StoryBook.PlainKey(p.Key)), StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, string> island in islands)
				foreach (int n in IslandCache.NotesOf(island.Value))
				{
					total++;
					if (pages.Contains("note:" + island.Key + ":" + n)) found++;
				}
		}

		/// <summary>One island's notes found of its notes with a text (the island by its name in the journal's page keys, the
		/// name of the island it came as): the journal's line over its pages, "(5/7 notes)" - so players know there are some left.
		/// False when the island isn't in the world or has no notes.</summary>
		public static bool OfIsland(string hostName, out int found, out int total)
		{
			found = total = 0;
			// (a page key's island part may name a copy, "Cove|2": by the island's name, found on any copy - AU41)
			hostName = (hostName ?? "").Split('|')[0];
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => !x.Failed && x.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase));
			if (e == null) return false;
			var pages = new HashSet<string>(StoryBook.Pages.Select(p => StoryBook.PlainKey(p.Key)), StringComparer.OrdinalIgnoreCase);
			foreach (int n in IslandCache.NotesOf(e.Name))
			{
				total++;
				if (pages.Contains("note:" + e.HostName + ":" + n)) found++;
			}
			return total > 0;
		}

		/// <summary>"5 / 38 notes found"</summary>
		public static string Summary()
		{
			int found, total;
			Count(out found, out total);
			return found + " / " + total + " notes found";
		}
	}

	/// <summary>
	/// The world's progress for the journal's Progress panel (the user, 2026-10-03: "the total progress for each category,
	/// detailed, so the player can follow the progression - 3/10, 63/100%, 3/15 found"): quests done, custom islands
	/// reached, notes found, story items found (used-up ones too), journal pages, and all of them together. What counts is
	/// what this world has: the custom islands in it and the saved islands its plan brings (counted before they come);
	/// an island made new from a map type, or one that comes by chance, counts once it has come.
	/// </summary>
	public static class WorldProgress
	{
		public class Row
		{
			public string Name, Help;
			public int Done, Total;
			public int Percent { get { return QuestCount.Percent(Done, Total); } }
		}

		/// <summary>The rows: Quests, Islands reached, Notes found, Story items found, Journal pages, and Overall (the five together).</summary>
		public static List<Row> Rows()
		{
			var rows = new List<Row>();
			int done, total;
			QuestCount.Count(QuestCount.All(), out done, out total);
			rows.Add(new Row { Name = "Quests", Done = done, Total = total, Help = "The custom islands' quests done, of all this world has: the world plan's islands with a quest (counted from the start, also before they come) and every other island with a quest that has come. Raft's own story isn't counted. Click the bar at the top for the list." });
			// The islands: in the world (not Raft's islands' extras) and the plan's saved islands still to come
			var islands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			int reached = 0;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Failed || WorldRandomizer.IsExtras(e) || islands.ContainsKey(e.HostName)) continue;
				islands[e.HostName] = e.Name;
				if (IslandWorldState.Islands.Any(x => x.HostName.Equals(e.HostName, StringComparison.OrdinalIgnoreCase) && x.State.ContainsKey(WorldDirector.VisitKey))) reached++;
			}
			WorldPlan plan = WorldDirector.Plan;
			foreach (IntroRule r in (plan != null ? plan.Rules : new List<IntroRule>()).Concat(StoryChain.Rules))
				if (r.What == "island" && r.WhatArg.Length > 0 && !islands.ContainsKey(r.WhatArg)) islands[r.WhatArg] = r.WhatArg;
			// (and the saved islands ticked for this world's sailing still to come - CA31: a world with ~50 ticked showed 3/19)
			foreach (string name in QuestCount.TickedToCome())
				if (!islands.ContainsKey(name)) islands[name] = name;
			rows.Add(new Row { Name = "Islands reached", Done = reached, Total = islands.Count, Help = "Custom islands someone of the crew has set foot on, of the custom islands in this world, the ones its plan will still bring and the saved islands ticked for this world that can still turn up while sailing. Brand-new generated islands add to it as they come. Raft's own islands aren't counted." });
			int nf, nt;
			NoteCount.Count(out nf, out nt);
			rows.Add(new Row { Name = "Notes found", Done = nf, Total = nt, Help = "Notes read (by anyone of the crew) of the notes with a text on those islands. Each island's line under Quest Pages says how many of its own you have found." });
			// Story items: every one those islands give, found - also used up since
			var found = new HashSet<string>(StoryBook.FoundIds, StringComparer.OrdinalIgnoreCase);
			var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, string> island in islands)
			{
				IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName.Equals(island.Key, StringComparison.OrdinalIgnoreCase));
				foreach (StoryItemDef d in StoryItems.Of(e != null ? IslandCache.PropsOf(e) : IslandCache.Props(island.Value))) if (d.Id.Length > 0) items.Add(d.Id);
			}
			rows.Add(new Row { Name = "Story items found", Done = items.Count(found.Contains), Total = items.Count, Help = "Story items found (keys, map pieces, logs...) of those the islands have - one used up since (a key a door took) still counts as found." });
			// Journal pages: the islands' notes and the pages their events write (by island name: found on any copy - AU41)
			var pages = new HashSet<string>(StoryBook.Pages.Select(p => StoryBook.PlainKey(p.Key)), StringComparer.OrdinalIgnoreCase);
			int pt = 0, pf = 0;
			foreach (KeyValuePair<string, string> island in islands)
			{
				string file = IslandWorldState.Islands.Where(x => x.HostName.Equals(island.Key, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).FirstOrDefault() ?? island.Value;
				foreach (int n in IslandCache.NotesOf(file)) { pt++; if (pages.Contains("note:" + island.Key + ":" + n)) pf++; }
				foreach (string ev in IslandCache.EventPagesOf(file)) { pt++; if (pages.Contains("act:" + island.Key + ":" + ev)) pf++; }
			}
			rows.Add(new Row { Name = "Journal pages", Done = pf, Total = pt, Help = "The pages all the islands can give: their notes, and the pages their events write when something happens - a side quest's in this journal, a main story island's in Raft's notebook (T). (The frequencies a plan gives out aren't counted here.)" });
			int sd = rows.Sum(r => r.Done), st = rows.Sum(r => r.Total);
			rows.Add(new Row { Name = "Overall", Done = sd, Total = st, Help = "All of it together: quests, islands, notes, story items and pages done of all there is." });
			return rows;
		}
	}

	/// <summary>
	/// Host: a banner for every player when the world's quests (QuestCount) reach 90 % - "You are nearing the end" - and
	/// 100 % - "You have completed the whole quest line" (the user, 2026-10-03). Each once per world (marks in the plan's
	/// done list, saved with the world); straight to 100 % shows only that one. Not in a world with fewer than MinQuests
	/// quests, where one island's quest would already be "the whole quest line".
	/// </summary>
	public static class QuestMilestones
	{
		public const int MinQuests = 5, NearPercent = 90;
		public const string NearMark = "milestone:90", AllMark = "milestone:100";
		public const string NearTitle = "Nearing the end", AllTitle = "The whole quest line is done";
		const float CheckSeconds = 5f;
		static float next;

		/// <summary>From the director's tick (host, in a world).</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < next) return;
			next = Time.unscaledTime + CheckSeconds;
			try { Check(QuestCount.All()); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [quests] " + e.Message); }
		}

		/// <summary>Shows the banner a count has reached and marks it; returns its title, or null (tests call it with their own counts).</summary>
		public static string Check(List<QuestCount.Quest> quests)
		{
			int done, total;
			QuestCount.Count(quests, out done, out total);
			if (total < MinQuests) return null;
			HashSet<string> marks = WorldDirector.Done;
			if (done >= total)
			{
				if (marks.Contains(AllMark)) return null;
				marks.Add(AllMark);
				marks.Add(NearMark);
				StoryChain.Announce(AllTitle, "You have completed the whole quest line: all " + total + " quests of this world are done!");
				return AllTitle;
			}
			if (done * 100 < NearPercent * total || marks.Contains(NearMark)) return null;
			marks.Add(NearMark);
			StoryChain.Announce(NearTitle, "You are nearing the end: " + done + " of " + total + " quests done (" + QuestCount.Percent(done, total) + "%). The journal (J) lists what is left.");
			return NearTitle;
		}
	}

	public class JournalWindow : MonoBehaviour
	{
		static JournalWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		/// <summary>J unless changed in Defaults (Keys, ModKeys).</summary>
		public static KeyCode Key { get { return ModKeys.Journal; } }

		static readonly Color Ink = UIKit.ParchmentInk;

		RectTransform itemGrid, pageList;
		Text countText, readTitle, readText, emptyItems, emptyPages;
		Image readIcon;
		string shownKey;
		/// <summary>T8: "Take out" on a story item that names a Raft item (StoryBook.TakeOut).</summary>
		Button takeOut;
		bool cursorWasFree, dirty;
		/// <summary>The quests' count in the head: a button (the list on the paper) with a bar behind its words.</summary>
		Button questButton;
		RectTransform questFill;
		/// <summary>The daily quest under the page (T9): the block, its bar's fill and its lines.</summary>
		RectTransform dailyBlock, dailyFill;
		Text dailyText;
		/// <summary>The Progress panel's lines, one per WorldProgress row.</summary>
		readonly List<Text> progressText = new List<Text>();
		float questRefreshAt;
		const string QuestsKey = "quests";
		/// <summary>The page each page button shows (two islands may have a page of the same title).</summary>
		readonly Dictionary<Button, string> pageKeys = new Dictionary<Button, string>();

		/// <summary>Every frame from the mod: J opens and closes the journal in a world.</summary>
		public static void Tick()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || DynamicIslands.InEditor()) { if (IsOpen) instance.Hide(); return; }
			if (!Input.GetKeyDown(Key) || Typing() || NoteReader.IsOpen || ModKeys.Listening) return;
			if (IsOpen) instance.Hide(); else Open();
		}

		/// <summary>A text field (chat, console) has the keyboard.</summary>
		static bool Typing()
		{
			GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			InputField f = sel != null ? sel.GetComponent<InputField>() : null;
			return f != null && f.isFocused;
		}

		public static void Open()
		{
			if (instance == null) Build();
			instance.Show();
		}

		public static void Close() { if (instance != null) instance.Hide(); }

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_Journal", 490);
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<JournalWindow>();
			StoryBook.Changed += () => { if (instance != null) instance.dirty = true; };

			RectTransform panel = UIKit.Panel(canvas.transform, "Panel", new RectOffset(18, 18, 14, 16), 10f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 620));
			RectTransform head = UIKit.Row(panel, 34f, 8f, "Head");
			UIKit.Label(head, "JOURNAL", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			// (the world's quests done: Raft's story islands, the plan's and every other island's with a quest - click for the list)
			instance.questButton = UIKit.Button(head, "", () => instance.ShowQuests(), "The custom islands' quests done in this world: the world plan's islands with quests (counted from the start) and every other island with a quest that has come - not Raft's own story. Click for the list.", 360, 30, 14);
			RectTransform fill = UIKit.Rect("Fill", instance.questButton.transform);
			fill.SetAsFirstSibling();
			fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
			fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);
			Image fillImage = fill.gameObject.AddComponent<Image>();
			fillImage.color = new Color(UIKit.Good.r, UIKit.Good.g, UIKit.Good.b, 0.55f);
			fillImage.raycastTarget = false;
			instance.questFill = fill;
			instance.countText = UIKit.Label(head, "", 13, UIKit.TextMuted, TextAnchor.MiddleRight);
			UIKit.Separator(panel);

			RectTransform body = UIKit.Row(panel, 490f, 14f, "Body");
			// Left: items and pages
			RectTransform left = UIKit.Rect("Left", body);
			UIKit.Size(left.gameObject, 400);
			UIKit.Vertical(left.gameObject, 8f, new RectOffset(0, 0, 0, 0));
			RectTransform items = UIKit.Group(left, "Story items");
			UIKit.Size(items.gameObject, -1, 214);
			RectTransform itemBox = UIKit.Rect("ItemBox", items);
			UIKit.Size(itemBox.gameObject, -1, 176);
			ScrollRect s1;
			RectTransform itemContent = UIKit.ScrollList(itemBox, out s1, 4f);
			UIKit.Stretch((RectTransform)s1.transform);
			instance.itemGrid = UIKit.Rect("Grid", itemContent);
			var g = instance.itemGrid.gameObject.AddComponent<GridLayoutGroup>();
			g.cellSize = new Vector2(88f, 84f);
			g.spacing = new Vector2(6f, 6f);
			g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			g.constraintCount = 4;
			instance.emptyItems = UIKit.Label(itemContent, "<i>No story items yet. Look for them in chests and on the islands.</i>", 13, UIKit.TextMuted);

			// (each island under it is its quest line: "Quest Pages", the user, 2026-10-03; the object keeps its name)
			RectTransform pagesGroup = UIKit.Group(left, "Quest Pages", "Group_Pages");
			UIKit.Size(pagesGroup.gameObject, -1, 268);
			RectTransform pageBox = UIKit.Rect("PageBox", pagesGroup);
			UIKit.Size(pageBox.gameObject, -1, 230);
			ScrollRect s2;
			instance.pageList = UIKit.ScrollList(pageBox, out s2, 4f);
			UIKit.Stretch((RectTransform)s2.transform);
			instance.emptyPages = UIKit.Label(instance.pageList, "<i>Notes you read on the islands are written down here.</i>", 13, UIKit.TextMuted);

			// Right: the page (or item) on paper
			// Right: the world's progress (the user, 2026-10-03), and under it the page (or item) on paper
			RectTransform right = UIKit.Rect("Right", body);
			UIKit.Vertical(right.gameObject, 8f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(right.gameObject, -1, -1).flexibleWidth = 1f;
			RectTransform progress = UIKit.Group(right, "Progress");
			UIKit.Size(progress.gameObject, -1, 108);
			RectTransform grid = UIKit.Rect("Grid", progress);
			var pg = grid.gameObject.AddComponent<GridLayoutGroup>();
			// (three cells in the right column's 530 px less the group's padding: at 176 px the panel ran 22 px past the journal)
			pg.cellSize = new Vector2(166f, 26f);
			pg.spacing = new Vector2(4f, 2f);
			pg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			pg.constraintCount = 3;
			UIKit.Size(grid.gameObject, -1, 56);
			foreach (WorldProgress.Row row in WorldProgress.Rows())
			{
				RectTransform cell = UIKit.Rect("Row_" + row.Name, grid);
				UIKit.Horizontal(cell.gameObject, 2f, new RectOffset(0, 0, 0, 0));
				Text t = UIKit.Label(cell, "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Value");
				UIKit.Size(t.gameObject, 166);
				t.resizeTextForBestFit = true; t.resizeTextMinSize = 10; t.resizeTextMaxSize = 13;
				// (hover the line itself for what it counts - the user, 2026-10-03: instead of a ? after each)
				t.raycastTarget = true;
				t.gameObject.AddComponent<UIKit.HelpMark>().Text = row.Help;
				instance.progressText.Add(t);
			}
			RectTransform sheet = UIKit.Rect("Sheet", right);
			UIKit.Size(sheet.gameObject, -1, -1).flexibleHeight = 1f;
			UIKit.ParchmentSheet(sheet);
			UIKit.Vertical(sheet.gameObject, 10f, new RectOffset(28, 28, 22, 18));
			RectTransform titleRow = UIKit.Row(sheet, 44f, 10f, "TitleRow");
			RectTransform iconRect = UIKit.Rect("Icon", titleRow);
			UIKit.Size(iconRect.gameObject, 44, 44);
			instance.readIcon = iconRect.gameObject.AddComponent<Image>();
			instance.readIcon.preserveAspect = true;
			instance.readIcon.raycastTarget = false;
			instance.readTitle = UIKit.Label(titleRow, "", 24, Ink, TextAnchor.MiddleLeft, FontStyle.Normal, "Title");
			instance.takeOut = UIKit.Button(titleRow, "Take out", () => instance.TakeOutShown(), "Take one out of the journal as Raft's own item, into your inventory (the crew has one less)", 230, 34, 14);
			instance.takeOut.gameObject.SetActive(false);
			ScrollRect s3;
			RectTransform textContent = UIKit.ScrollList(sheet, out s3, 0f);
			UIKit.Size(s3.gameObject, -1, -1).flexibleHeight = 1f;
			instance.readText = UIKit.Label(textContent, "", 17, Ink, TextAnchor.UpperLeft, FontStyle.Normal, "Text");
			instance.readText.lineSpacing = 1.15f;
			instance.readText.supportRichText = false;
			foreach (Text t in new[] { instance.readTitle, instance.readText }) { Shadow sh = t.GetComponent<Shadow>(); if (sh != null) Destroy(sh); }
			// The daily quest under the page (T9, the user 2026-10-10: in the empty space below the page's text): only with the
			// world's Daily quest option on - what to do and how far along, the reward, done or not, when the next one comes
			RectTransform daily = UIKit.Rect("Daily", sheet);
			UIKit.Size(daily.gameObject, -1, 92);
			UIKit.Vertical(daily.gameObject, 3f, new RectOffset(0, 0, 0, 0));
			RectTransform rule = UIKit.Rect("Rule", daily);
			UIKit.Size(rule.gameObject, -1, 1);
			Image ruleImage = rule.gameObject.AddComponent<Image>();
			ruleImage.color = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
			ruleImage.raycastTarget = false;
			Text dailyHead = UIKit.Label(daily, "DAILY QUEST", 13, Ink, TextAnchor.MiddleLeft, FontStyle.Bold, "Head");
			UIKit.Size(dailyHead.gameObject, -1, 18);
			RectTransform bar = UIKit.Rect("Bar", daily);
			UIKit.Size(bar.gameObject, -1, 6);
			Image barBack = bar.gameObject.AddComponent<Image>();
			barBack.color = new Color(Ink.r, Ink.g, Ink.b, 0.18f);
			barBack.raycastTarget = false;
			RectTransform dailyFill = UIKit.Rect("Fill", bar);
			dailyFill.anchorMin = Vector2.zero; dailyFill.anchorMax = new Vector2(0f, 1f);
			dailyFill.offsetMin = Vector2.zero; dailyFill.offsetMax = Vector2.zero;
			Image dailyFillImage = dailyFill.gameObject.AddComponent<Image>();
			dailyFillImage.color = new Color(UIKit.Good.r * 0.75f, UIKit.Good.g * 0.75f, UIKit.Good.b * 0.75f, 0.9f);
			dailyFillImage.raycastTarget = false;
			instance.dailyText = UIKit.Label(daily, "", 14, Ink, TextAnchor.UpperLeft, FontStyle.Normal, "Text");
			UIKit.Size(instance.dailyText.gameObject, -1, 58);
			instance.dailyText.supportRichText = false;
			foreach (Text t in new[] { dailyHead, instance.dailyText }) { Shadow sh = t.GetComponent<Shadow>(); if (sh != null) Destroy(sh); }
			instance.dailyBlock = daily;
			instance.dailyFill = dailyFill;

			RectTransform bottom = UIKit.Row(panel, 34f, 8f, "Bottom");
			UIKit.Label(bottom, "J or Esc to close", 13, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Button(bottom, "Close", Close, null, 120, 34);
			canvas.gameObject.SetActive(false);
		}

		void Show()
		{
			gameObject.SetActive(true);
			cursorWasFree = Cursor.visible;
			if (!cursorWasFree) try { RAPI.ToggleCursor(true); } catch { }
			Fill();
		}

		void Hide()
		{
			if (!gameObject.activeSelf) return;
			gameObject.SetActive(false);
			if (!cursorWasFree) try { RAPI.ToggleCursor(false); } catch { }
		}

		void Update()
		{
			if (EscKey.Down(this)) { Hide(); return; }
			if (dirty) Fill();
			else if (Time.unscaledTime >= questRefreshAt) RefreshQuests();
		}

		/// <summary>Fills the lists from the story book.</summary>
		void Fill()
		{
			dirty = false;
			foreach (Transform c in itemGrid) Destroy(c.gameObject);
			foreach (Transform c in pageList) if (c != emptyPages.transform) Destroy(c.gameObject);
			// (the main story's items and pages are in Raft's notebook - QuestBook - the journal keeps the side quests')
			HashSet<string> mainItems = QuestBook.MainItemIds();
			List<StoryBook.Held> items = StoryBook.Items.Where(h => !mainItems.Contains(h.Def.Id)).ToList();
			List<StoryBook.Page> pages = StoryBook.Pages.Where(p => !QuestBook.IsMainPage(p)).ToList();
			foreach (StoryBook.Held h in items) ItemTile(h);
			emptyItems.gameObject.SetActive(items.Count == 0);
			// (by island - the island seen last first - under its name and its quest: pages of several islands in one list got
			// mixed up, the user found, 2026-10-02)
			pageKeys.Clear();
			foreach (IGrouping<string, StoryBook.Page> island in Enumerable.Reverse(pages).GroupBy(p => p.Island ?? ""))
			{
				IslandHeader(WorldRandomizer.Readable(island.Key), island.First(), island);
				foreach (StoryBook.Page p in island) PageButton(p);
			}
			emptyPages.gameObject.SetActive(pages.Count == 0);
			countText.text = items.Count + " story item(s) \u00B7 " + pages.Count + " page(s)" + (QuestBook.InNotebook ? " \u00B7 main story: Raft's notebook" : "");
			NoteRefresh();
			RefreshQuests();
			if (shownKey == QuestsKey) ShowQuests();
			else if (shownKey == null || (!pages.Any(p => p.Key == shownKey) && !items.Any(h => "item:" + h.Def.Id == shownKey)))
			{
				if (pages.Count > 0) ShowPage(pages.Last());
				else if (items.Count > 0) ShowItem(items[0]);
				else { shownKey = null; readIcon.enabled = false; readTitle.text = "Journal"; readText.text = "Nothing here yet. Read notes on the islands and look for story items: they are kept here for the whole crew."; }
			}
			else if (shownKey.StartsWith("item:")) ShowItem(items.First(h => "item:" + h.Def.Id == shownKey)); // (its count may have changed)
			RefreshTakeOut();
		}

		/// <summary>T8: the Take out button, on a shown story item that names a Raft item the crew holds.</summary>
		void RefreshTakeOut()
		{
			string id = shownKey != null && shownKey.StartsWith("item:") ? shownKey.Substring(5) : null;
			bool can = id != null && StoryBook.CanTakeOut(id);
			takeOut.gameObject.SetActive(can);
			if (can) UIKit.LabelOf(takeOut).text = "Take out: " + ContentCatalog.ItemLabel(StoryBook.DefOf(id).RaftItem.Trim());
		}

		void TakeOutShown()
		{
			if (shownKey != null && shownKey.StartsWith("item:")) StoryBook.TakeOut(shownKey.Substring(5));
		}

		/// <summary>For tests: the Take out button's label, or null when it is not shown.</summary>
		internal static string TakeOutLabel { get { return IsOpen && instance.takeOut.gameObject.activeSelf ? UIKit.LabelOf(instance.takeOut).text : null; } }

		/// <summary>For tests: clicks the Take out button.</summary>
		internal static void ClickTakeOut() { if (IsOpen && instance.takeOut.gameObject.activeSelf) instance.takeOut.onClick.Invoke(); }

		void ItemTile(StoryBook.Held h)
		{
			RectTransform r = UIKit.Rect("Item_" + h.Def.Id, itemGrid);
			Image bg = UIKit.Background(r.gameObject, Color.white, 6);
			var b = r.gameObject.AddComponent<Button>();
			UIKit.Register(b);
			b.targetGraphic = bg;
			UIKit.Slot(b);
			RectTransform pic = UIKit.Rect("Icon", r);
			UIKit.Anchor(pic, new Vector2(0.5f, 1f), new Vector2(0, -5), new Vector2(46, 46));
			Image icon = pic.gameObject.AddComponent<Image>();
			icon.sprite = StoryItems.IconSprite(h.Def.Icon);
			icon.preserveAspect = true; icon.raycastTarget = false;
			if (icon.sprite == null) icon.color = new Color(0.3f, 0.2f, 0.1f, 0.35f);
			Text t = UIKit.Label(r, h.Def.ShownName, 11, UIKit.SlotText, TextAnchor.LowerCenter, FontStyle.Normal, "Label");
			UIKit.Stretch(t.rectTransform, 3, 3, 52, 3);
			t.resizeTextForBestFit = true; t.resizeTextMinSize = 8; t.resizeTextMaxSize = 11;
			if (h.Count > 1)
			{
				Text n = UIKit.Label(r, "\u00D7" + h.Count, 12, UIKit.SlotText, TextAnchor.UpperRight, FontStyle.Bold, "Count");
				UIKit.Stretch(n.rectTransform, 4, 6, 3, 60);
			}
			StoryBook.Held held = h;
			b.onClick.AddListener(() => ShowItem(held));
		}

		void PageButton(StoryBook.Page p)
		{
			Button b = UIKit.Button(pageList, p.Title.Length > 0 ? p.Title : "(a page)", () => ShowPage(p), p.Island.Length > 0 ? "Found on " + WorldRandomizer.Readable(p.Island) : null, -1, 30f, 13);
			pageKeys[b] = p.Key;
			if (p.Key == shownKey) UIKit.SetActive(b, true); else UIKit.Flat(b);
			Text t = UIKit.LabelOf(b);
			t.alignment = TextAnchor.MiddleLeft;
		}

		/// <summary>An island's line over its pages: its name and its quest (a tick once done).</summary>
		void IslandHeader(string island, StoryBook.Page sample, IEnumerable<StoryBook.Page> pages)
		{
			bool done;
			string quest = QuestOf(sample, out done);
			// (its notes found of its notes - the user, 2026-10-03: "so you would know if you have some left on it")
			string host = pages.Select(p => (p.Key ?? "").Split(':')).Where(k => k.Length >= 3 && (k[0] == "note" || k[0] == "act")).Select(k => k[1]).FirstOrDefault();
			int found, total;
			string notes = host != null && NoteCount.OfIsland(host, out found, out total) ? "  (" + found + "/" + total + " notes)" : "";
			Text t = UIKit.Label(pageList, (island.Length > 0 ? island : "Other pages") + (quest.Length > 0 ? "  \u00B7  " + quest + (done ? "  \u221a done" : "") : "") + notes, 13, UIKit.Accent, TextAnchor.LowerLeft, FontStyle.Bold, "Island");
			// a finished quest's line in light green (the user, 2026-10-07)
			if (done) t.color = new Color(0.62f, 0.9f, 0.5f);
			UIKit.Size(t.gameObject, -1, 24);
		}

		/// <summary>The quest of the island a page came from (the island's name is in the page's key: note:&lt;island&gt;:&lt;n&gt;,
		/// act:&lt;island&gt;:...), and whether it is done; "" when the page isn't an island's or the island has no quest.</summary>
		public static string QuestOf(StoryBook.Page p, out bool done)
		{
			done = false;
			string[] k = (p != null ? p.Key ?? "" : "").Split(':');
			if (k.Length < 3 || (k[0] != "note" && k[0] != "act")) return "";
			IslandWorldState.Entry e = StoryBook.EntryOfPageIsland(k[1]);
			if (e == null) return "";
			IslandQuest q = IslandQuest.From(IslandCache.PropsOf(e));
			if (q.Steps.Count == 0) return "";
			done = QuestTracker.StepOf(e) >= q.Steps.Count;
			return q.Title;
		}

		void ShowPage(StoryBook.Page p)
		{
			shownKey = p.Key;
			readIcon.enabled = false;
			readTitle.text = p.Title.Length > 0 ? p.Title : "A page";
			bool done;
			string quest = QuestOf(p, out done);
			readText.text = (p.Text.Length > 0 ? p.Text : "(The page is empty.)") + (p.Island.Length > 0 ? "\n\n\u2014 " + WorldRandomizer.Readable(p.Island) + (quest.Length > 0 ? " (" + quest + ")" : "") + ", day " + p.Day : "");
			RefreshSelection();
		}

		void ShowItem(StoryBook.Held h)
		{
			shownKey = "item:" + h.Def.Id;
			readIcon.sprite = StoryItems.IconSprite(h.Def.Icon);
			readIcon.enabled = readIcon.sprite != null;
			readTitle.text = h.Def.ShownName + (h.Count > 1 ? "  \u00D7" + h.Count : "");
			readText.text = h.Def.Description.Length > 0 ? h.Def.Description : "A story item. The crew holds it together.";
			RefreshSelection();
		}

		void RefreshSelection()
		{
			foreach (Button b in pageList.GetComponentsInChildren<Button>())
			{
				string key;
				if (pageKeys.TryGetValue(b, out key) && key == shownKey) UIKit.SetActive(b, true); else UIKit.Flat(b);
				UIKit.LabelOf(b).alignment = TextAnchor.MiddleLeft;
			}
			RefreshTakeOut();
		}

		/// <summary>The count in the head, and its bar (when the journal opens, and every 2 s while it is open: quests done
		/// elsewhere, by other players, count at once).</summary>
		void RefreshQuests()
		{
			questRefreshAt = Time.unscaledTime + 2f;
			List<QuestCount.Quest> quests = QuestCount.All();
			int done, total;
			QuestCount.Count(quests, out done, out total);
			UIKit.LabelOf(questButton).text = total == 0 ? "QUESTS: NONE YET" : "QUESTS  " + done + " / " + total + "  \u00B7  " + QuestCount.Percent(done, total) + "%" + (done == total ? "  \u00B7  ALL DONE" : "");
			questFill.anchorMax = new Vector2(total == 0 ? 0f : (float)done / total, 1f);
			NoteRefresh();
			DailyRefresh();
			if (shownKey == QuestsKey) readText.text = QuestList(quests);
		}

		/// <summary>The daily quest under the page (T9): shown with the option on, its lines and bar now.</summary>
		void DailyRefresh()
		{
			float along;
			string text = DailyQuest.PanelText(out along);
			dailyBlock.gameObject.SetActive(text != null);
			if (text == null) return;
			dailyText.text = text;
			dailyFill.anchorMax = new Vector2(along, 1f);
		}

		/// <summary>The daily quest block's lines now, null when it isn't shown (tests).</summary>
		public static string DailyShown { get { return IsOpen && instance.dailyBlock.gameObject.activeSelf ? instance.dailyText.text : null; } }

		/// <summary>The top right: story items, notes found of the world's notes, pages (the user, 2026-10-03: "5/38 notes found").</summary>
		void NoteRefresh()
		{
			// (the counts are in the Progress panel now: the line of story items, notes found and pages read like a puzzle)
			countText.text = "";
			List<WorldProgress.Row> rows = WorldProgress.Rows();
			for (int i = 0; i < rows.Count && i < progressText.Count; i++)
			{
				WorldProgress.Row r = rows[i];
				progressText[i].text = "<color=#f7cc6b>" + r.Name + "</color>  " + (r.Total == 0 ? "-" : r.Done + "/" + r.Total + (r.Name == "Quests" || r.Name == "Overall" ? "  \u00B7  " + r.Percent + "%" : ""));
			}
		}

		/// <summary>The Progress panel's lines' hover help, and how many ? marks it has (tests: none - the lines themselves carry it).</summary>
		public static List<string> ProgressHelp { get { return IsOpen ? instance.progressText.Select(t => { UIKit.HelpMark m = t.GetComponent<UIKit.HelpMark>(); return m != null ? m.Text : null; }).ToList() : new List<string>(); } }
		public static int ProgressMarks { get { return IsOpen && instance.progressText.Count > 0 ? instance.progressText[0].transform.parent.parent.GetComponentsInChildren<Button>(true).Length : -1; } }

		/// <summary>Tests: the story items' list scrolls - its content's and view's heights, and whether its last tile is in
		/// view once scrolled to the bottom ("content|view|True"); null when the journal is closed.</summary>
		public static string ItemsScrollCheck()
		{
			if (!IsOpen || instance.itemGrid.childCount == 0) return null;
			ScrollRect sr = instance.itemGrid.GetComponentInParent<ScrollRect>();
			if (sr == null) return "no scroll list";
			Canvas.ForceUpdateCanvases();
			RectTransform view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
			float ch = sr.content.rect.height, vh = view.rect.height;
			sr.verticalNormalizedPosition = 0f;
			Canvas.ForceUpdateCanvases();
			var last = (RectTransform)instance.itemGrid.GetChild(instance.itemGrid.childCount - 1);
			Vector3[] c = new Vector3[4], v = new Vector3[4];
			last.GetWorldCorners(c);
			view.GetWorldCorners(v);
			bool visible = c[0].y >= v[0].y - 1f && c[1].y <= v[1].y + 1f;
			sr.verticalNormalizedPosition = 1f;
			return ch.ToString("F0", CultureInfo.InvariantCulture) + "|" + vh.ToString("F0", CultureInfo.InvariantCulture) + "|" + visible;
		}

		/// <summary>The Progress panel's lines now (tests).</summary>
		public static List<string> ProgressShown { get { return IsOpen ? instance.progressText.Select(t => System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]+>", "")).ToList() : new List<string>(); } }

		/// <summary>The top right's text now (tests).</summary>
		public static string CountsShown { get { return IsOpen ? instance.countText.text : null; } }

		/// <summary>The world's quests on the paper: done and still to do, by kind.</summary>
		void ShowQuests()
		{
			shownKey = QuestsKey;
			readIcon.enabled = false;
			RefreshTakeOut();
			List<QuestCount.Quest> quests = QuestCount.All();
			readTitle.text = "Quests: " + QuestCount.Summary(quests).Replace(" quests done", " done");
			readText.text = QuestList(quests);
			RefreshSelection();
		}

		internal static string QuestList(List<QuestCount.Quest> quests)
		{
			string daily = (DailyQuest.JournalText() ?? "") + AnimalLooks.JournalText();
			if (quests.Count == 0) return daily + "No custom quests in this world yet. Islands with a quest count here when they come; a world plan's are counted from the start.";
			var sb = new System.Text.StringBuilder(daily);
			foreach (IGrouping<string, QuestCount.Quest> g in quests.GroupBy(q => q.Group))
			{
				// (the main story's steps are in Raft's notebook: only how far it is, here)
				if (g.Key == QuestCount.PlanStory && QuestBook.InNotebook)
				{
					sb.Append("Main story: ").Append(g.Count(q => q.Done)).Append(" of ").Append(g.Count()).Append(" done - its quests, notes and items are in Raft's notebook\n\n");
					continue;
				}
				sb.Append(g.Key).Append(" (").Append(g.Count(q => q.Done)).Append(" of ").Append(g.Count()).Append(")\n");
				foreach (QuestCount.Quest q in g) sb.Append(q.Done ? "   \u221a  " : "   \u2013  ").Append(q.Name).Append(q.Done ? "" : "").Append('\n');
				sb.Append('\n');
			}
			sb.Append("\u221a done   \u2013 still to do. The custom islands' quests: a plan's islands count from the start; other islands with a quest count once they have come. Raft's own story isn't counted here.");
			return sb.ToString();
		}

		/// <summary>The quests' count in the head now (tests).</summary>
		public static string QuestsShown { get { return IsOpen ? UIKit.LabelOf(instance.questButton).text : null; } }

		/// <summary>Shows the quest list on the paper (tests: as a click on the count).</summary>
		public static void ShowQuestList() { if (IsOpen) instance.ShowQuests(); }

		/// <summary>What the journal shows now (tests).</summary>
		public static string ShownTitle { get { return IsOpen ? instance.readTitle.text : null; } }

		/// <summary>The text on the paper now (tests).</summary>
		public static string ShownText { get { return IsOpen ? instance.readText.text : null; } }

		/// <summary>The pages list as it reads now, top to bottom: an island's line as "# " + its text, a page by its title (tests).</summary>
		public static List<string> ListLines()
		{
			var lines = new List<string>();
			if (!IsOpen) return lines;
			foreach (Transform c in instance.pageList)
			{
				if (!c.gameObject.activeSelf || c == instance.emptyPages.transform) continue;
				Button b = c.GetComponent<Button>();
				Text t = b != null ? UIKit.LabelOf(b) : c.GetComponent<Text>();
				if (t != null) lines.Add((b == null ? "# " : "") + t.text);
			}
			return lines;
		}

		/// <summary>For tests (T8): shows a held story item, as a click on its tile does.</summary>
		internal static bool ShowItemOf(string id)
		{
			StoryBook.Held h = StoryBook.Items.FirstOrDefault(x => x.Def.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
			if (!IsOpen || h == null) return false;
			instance.ShowItem(h);
			return true;
		}

		/// <summary>Shows the page with this key on the paper (tests: as a click on it).</summary>
		public static bool ShowKey(string key)
		{
			StoryBook.Page p = StoryBook.Pages.FirstOrDefault(x => x.Key == key);
			if (!IsOpen || p == null) return false;
			instance.ShowPage(p);
			return true;
		}
	}
}
