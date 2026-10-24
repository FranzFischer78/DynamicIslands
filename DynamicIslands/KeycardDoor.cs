using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Keycard doors (RF4): Raft's key doors from the ready pieces (the Tangaroa card reader, the Utopia and Varuna key doors,
	/// the padlock...) opened by a story item. Which item opens a door, and whether it is used up on the way in, are its
	/// use check (if.use: "has|story:&lt;id&gt;|1" keeps the card, "take|story:&lt;id&gt;|1" uses it up). The inspector edits
	/// these two in one place; the door's other checks stay as they are. Opened, the door is hidden (its hide state is kept
	/// with the island and shared with the other players like any behaviour's).
	/// </summary>
	public static class KeycardDoor
	{
		/// <summary>Raft's card reader sound, played when a keycard door opens.</summary>
		public const string CardSound = "event:/raft/door/door_unlock_keycard";

		/// <summary>A ready-made key door: a door piece that wants an item.</summary>
		public static bool Is(string name)
		{
			ReadyPieces.Piece p = ReadyPieces.Of(name);
			return p != null && p.Kind == ReadyPieces.Door && p.Item != null;
		}

		/// <summary>A door that reads a keycard (its default item is one of Raft's keycards).</summary>
		public static bool IsCardReader(string name)
		{
			ReadyPieces.Piece p = ReadyPieces.Of(name);
			return p != null && p.Kind == ReadyPieces.Door && p.Item != null && p.Item.IndexOf("KeyCard", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		static string CheckText(IDictionary<string, string> props) { return ObjectProps.Get(props, BehaviourProps.CheckKey("use")); }

		static ObjCheck CardCheck(IDictionary<string, string> props)
		{
			return ObjCheck.ParseLines(CheckText(props)).FirstOrDefault(c => !c.Not && c.IsItem && StoryItems.IsStory(c.Target));
		}

		/// <summary>The story item that opens it ("story:&lt;id&gt;"), or null: no card opens it.</summary>
		public static string CardOf(IDictionary<string, string> props) { ObjCheck c = CardCheck(props); return c != null ? c.Target : null; }

		/// <summary>The card is used up when the door opens (a take check).</summary>
		public static bool UsedUp(IDictionary<string, string> props) { ObjCheck c = CardCheck(props); return c != null && c.Kind == "take"; }

		/// <summary>The settings with the door's card set (null or "": none - the door then opens for anyone) and used up or kept.
		/// The use check's other lines stay.</summary>
		public static Dictionary<string, string> WithCard(IDictionary<string, string> props, string card, bool usedUp)
		{
			string text = CheckText(props);
			bool any = ObjCheck.IsAny(text);
			List<ObjCheck> checks = ObjCheck.ParseLines(text);
			int at = checks.FindIndex(c => !c.Not && c.IsItem && StoryItems.IsStory(c.Target));
			ObjCheck old = at >= 0 ? checks[at] : null;
			if (at >= 0) checks.RemoveAt(at); else at = 0;
			if (!string.IsNullOrEmpty(card))
				checks.Insert(Math.Min(at, checks.Count), new ObjCheck { Kind = usedUp ? "take" : "has", Target = StoryItems.IsStory(card) ? card : StoryItems.Ref(card), Arg = old != null && old.Arg.Length > 0 ? old.Arg : "1" });
			return ObjectProps.With(props, BehaviourProps.CheckKey("use"), checks.Count > 0 ? ObjCheck.ToLines(checks, any && checks.Count > 1) : null);
		}

		/// <summary>The cards a door can want: the island's own story items, then Raft's quest items (keycards first), and
		/// the current one if it is none of these. Values are "story:&lt;id&gt;".</summary>
		public static List<KeyValuePair<string, string>> Choices(IDictionary<string, string> islandProps, string current)
		{
			var list = new List<KeyValuePair<string, string>>();
			foreach (StoryItemDef d in StoryItems.Of(islandProps))
				if (d.Id.Length > 0) list.Add(new KeyValuePair<string, string>(StoryItems.Ref(d.Id), d.ShownName));
			IEnumerable<string> raft = StoryItems.QuestItems.Select(q => q.questItemType.ToString()).Distinct()
				.OrderBy(t => t.IndexOf("KeyCard", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1).ThenBy(t => t, StringComparer.OrdinalIgnoreCase);
			foreach (string t in raft)
			{
				string id = StoryItems.Ref(QuestItemPickups.IdPrefix + t);
				if (list.Any(x => x.Key.Equals(id, StringComparison.OrdinalIgnoreCase))) continue;
				StoryItemDef d = QuestItemPickups.Def(QuestItemPickups.IdPrefix + t);
				list.Add(new KeyValuePair<string, string>(id, (d != null ? d.ShownName : t) + " (Raft)"));
			}
			if (!string.IsNullOrEmpty(current) && !list.Any(x => x.Key.Equals(current, StringComparison.OrdinalIgnoreCase)))
				list.Insert(0, new KeyValuePair<string, string>(current, StoryItems.Label(current)));
			return list;
		}
	}
}
