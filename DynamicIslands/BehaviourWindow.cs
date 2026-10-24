using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Behaviour &amp; events" for the selected object (inspector), or "Island events" (Island tab): a name, movement
	/// (spin, bob, a door or lift with Preview), whether it is there at first, whether players can use it, collision,
	/// and for each event of the object "when ... then" actions (show/hide, open/close, message, give, sound, teleport,
	/// signal, journal page, wait), "only if" checks (items, story items, an object's state, a signal, the quest) and what
	/// happens otherwise (a message). Save writes it into the object's settings as one undo step (Behaviours has the runtime).
	/// </summary>
	public class BehaviourWindow : MonoBehaviour
	{
		static BehaviourWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		EditorGameObject target;
		/// <summary>Editing the island's own events (arrive, quest) instead of an object.</summary>
		bool islandMode;
		Dictionary<string, string> props = new Dictionary<string, string>();
		readonly Dictionary<string, List<ObjAction>> actions = new Dictionary<string, List<ObjAction>>();
		readonly Dictionary<string, List<ObjCheck>> checks = new Dictionary<string, List<ObjCheck>>();
		readonly Dictionary<string, List<ObjAction>> elses = new Dictionary<string, List<ObjAction>>();
		readonly Dictionary<string, bool> anyOf = new Dictionary<string, bool>();
		Text titleText, summaryText;
		RectTransform body;
		readonly List<InputField> fields = new List<InputField>();
		bool previewing;
		// (where the previewed object stood: put back when the window closes mid-preview - the coroutine stops with it)
		Transform previewObject;
		Vector3 previewPos;
		Quaternion previewRot;

		static readonly Dictionary<string, string> VerbLabels = new Dictionary<string, string>
		{
			{ "show", "show" }, { "hide", "hide" }, { "toggle", "show/hide" }, { "open", "open" }, { "close", "close" }, { "switch", "open/close" },
			{ "message", "say" }, { "give", "give items" }, { "sound", "sound (this player)" }, { "teleport", "teleport to" }, { "signal", "send signal" },
			{ "journal", "journal page" }, { "wait", "wait" }, { "character", "unlock character" },
			{ "playsound", "play sound/music" }, { "stopsound", "stop sound" }, { "camera", "camera moment" },
			{ "suit", "protective suit" }, { "hit", "hurt creatures" },
		};

		/// <summary>What each action does (the action list's lines).</summary>
		static readonly Dictionary<string, string> VerbHints = new Dictionary<string, string>
		{
			{ "show", "Objects with a name appear (they were hidden)" }, { "hide", "Objects with a name disappear" }, { "toggle", "Hidden ones appear, shown ones disappear" },
			{ "open", "A door, gate, bridge or lift opens" }, { "close", "It closes" }, { "switch", "Open if closed, closed if open (a lever)" },
			{ "message", "Players read a message on the screen" }, { "give", "The player gets items or story items" }, { "sound", "One of Raft's sounds plays once, for the player who set it off" },
			{ "teleport", "The player is moved to an object with a name" }, { "signal", "A signal world plans and island rules can wait for" },
			{ "journal", "A page is written into the crew's journal (J) - or, on a main story island, onto its tab in Raft's notebook (T)" }, { "wait", "Waits some seconds before the actions below" },
			{ "character", "Unlocks one of Raft's characters for the player (its name or number, e.g. Rouhi or 3) - kept in the player's Raft profile" },
			{ "playsound", "One of Raft's sounds or music cues, for every player: once or on a loop, near the object or for the whole island (a loop comes back after a reload; it stops when a player leaves)" },
			{ "stopsound", "Stops the sound an object plays (all = every sound on the island)" },
			{ "camera", "The camera of players near a marker object moves there and looks at an object for some seconds (Esc skips), then comes back" },
			{ "suit", "The player who set it off wears a protective suit for some seconds: acid and fumes zones don't hurt them" },
			{ "hit", "The animals of a creature spot with that name lose a share of their health (an explosive barrel, a falling pillar): the way to hurt a boss whose phase only blasts hurt" },
		};

		/// <summary>What each check asks (the check list's lines).</summary>
		static readonly Dictionary<string, string> CheckHints = new Dictionary<string, string>
		{
			{ "has", "The player has an item or story item (it stays)" }, { "take", "The player has it, and it is used up (like a key)" },
			{ "state", "Objects with a name are open, closed, shown or hidden" }, { "signal", "A signal was sent on this island" },
			{ "quest", "The island's quest reached a step (or is done)" },
			{ "wears", "The player wears an item (a headlight, flippers, an oxygen tank...). Name* = any kind of it" },
			{ "holds", "The player holds an item in their hand (binoculars, a hook, an axe...). Name* = any kind of it" },
		};

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("BehaviourWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
			instance = blocker.AddComponent<BehaviourWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open(EditorGameObject target)
		{
			if (instance == null || target == null) return;
			instance.islandMode = false;
			instance.target = target;
			instance.Load(target.Props ?? new Dictionary<string, string>());
			instance.titleText.text = "BEHAVIOUR & EVENTS \u00B7 " + PlaceableCatalog.DisplayName(target.GameObjectName).ToUpperInvariant();
			instance.Show();
			instance.savedPrint = instance.Print();
		}

		/// <summary>The island's own events: players arriving, its quest done.</summary>
		public static void OpenIsland()
		{
			if (instance == null) return;
			instance.islandMode = true;
			instance.target = null;
			instance.Load(DynamicIslands.currentIslandProps);
			instance.titleText.text = "ISLAND EVENTS \u00B7 " + DynamicIslands.currentIslandName.ToUpperInvariant();
			instance.Show();
			instance.savedPrint = instance.Print();
		}

		/// <summary>What Esc compares: the settings as they would be saved.</summary>
		string savedPrint;

		string Print()
		{
			Keep();
			return string.Join("\n", Result().OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Key + "=" + e.Value).ToArray());
		}

		/// <summary>The settings differ from the object's or island's (Esc asks before it closes).</summary>
		public static bool Unsaved { get { return IsOpen && instance.Print() != instance.savedPrint; } }

		public static void Close()
		{
			if (instance == null) return;
			DropList.Close();
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		void Load(IDictionary<string, string> p)
		{
			props = new Dictionary<string, string>(p);
			actions.Clear();
			checks.Clear();
			elses.Clear();
			anyOf.Clear();
			foreach (string ev in Events()) actions[ev] = ObjAction.ParseLines(ObjectProps.Get(props, BehaviourProps.EventKey(ev)));
			foreach (string ev in Events())
			{
				checks[ev] = ObjCheck.ParseLines(ObjectProps.Get(props, BehaviourProps.CheckKey(ev)));
				elses[ev] = ObjAction.ParseLines(ObjectProps.Get(props, BehaviourProps.ElseKey(ev)));
				anyOf[ev] = ObjCheck.IsAny(ObjectProps.Get(props, BehaviourProps.CheckKey(ev)));
			}
		}

		void Show()
		{
			gameObject.SetActive(true);
			transform.SetAsLastSibling();
			Rebuild();
		}

		/// <summary>The events this object (or the island) can have.</summary>
		List<string> Events()
		{
			if (islandMode) return BehaviourProps.IslandEvents.ToList();
			return BehaviourProps.EventsFor(target.GameObjectName, props).Select(e => e.Key).ToList();
		}

		string EventText(string ev)
		{
			if (ev == "arrive") return "players first come to the island";
			if (ev == "quest") return "the island's quest is done";
			return BehaviourProps.EventsFor(target.GameObjectName, props).Where(e => e.Key == ev).Select(e => e.Value).FirstOrDefault() ?? ev;
		}

		void Update()
		{
			if (ChoiceWindow.IsOpen || ItemPickerWindow.IsOpen || SoundPickerWindow.IsOpen || DropList.Busy || EditorInput.SubWindowJustClosed) return;
			EditorInput.IsTyping = fields.Any(f => f != null && f.isFocused);
			if (EscKey.Down(this)) { if (Unsaved) EscKey.AskUnsaved(islandMode ? "The island's events" : "The behaviour", Save, Close); else Close(); }
		}

		void OnDisable()
		{
			EditorInput.IsTyping = false;
			if (previewing && previewObject != null) { previewObject.position = previewPos; previewObject.rotation = previewRot; }
			previewing = false;
		}

		#region Building

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			titleText = UIKit.Label(head, "BEHAVIOUR & EVENTS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Fit(titleText, 12);
			UIKit.Label(head, "What it does in a world, and what happens when players use it", 12, UIKit.TextMuted, TextAnchor.MiddleRight);
			RectTransform box = UIKit.Rect("Body", panel);
			UIKit.Size(box.gameObject, -1, 600);
			ScrollRect scroll;
			body = UIKit.ScrollList(box, out scroll, 8f);
			UIKit.Stretch((RectTransform)scroll.transform);
			summaryText = UIKit.Label(panel, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Summary");
			UIKit.Size(summaryText.gameObject, -1, 34);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Size(UIKit.Label(buttons, "", 12, UIKit.TextMuted).gameObject, -1, -1, 1);
			Button save = UIKit.Button(buttons, "Save", Save, "Keep the changes (one undo step)", 110, 34);
			UIKit.Primary(save);
			UIKit.Button(buttons, "Cancel", Close, "Close without changing anything (Esc)", 110, 34);
		}

		void Rebuild()
		{
			foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			fields.Clear();
			if (!islandMode)
			{
				string n = target.GameObjectName;
				bool zone = ContentCatalog.IsZone(n), creature = ContentCatalog.IsCreature(n), helper = ContentCatalog.IsHelper(n);
				NameGroup();
				if (!zone && !creature) MovementGroup();
				WorldGroup(zone, creature, helper);
			}
			foreach (string ev in Events()) EventGroup(ev);
			if (Events().Count == 0) UIKit.Label(body, "<i>This object has no events. Make it usable (\"Players can use it\") to give it actions.</i>", 12, UIKit.TextMuted);
			Summarise();
		}

		void NameGroup()
		{
			RectTransform g = UIKit.Group(body, "Name");
			RectTransform row = UIKit.Row(g, 28f, 6f);
			InputField f = Field(row, "e.g. door-1 (optional)", ObjectProps.Get(props, BehaviourProps.Name), 220, "Actions of other objects find it by this name. Several objects can share a name: they act together", v => SetProp(BehaviourProps.Name, v.Replace("|", "").Trim()));
			f.characterLimit = 32;
			UIKit.Label(row, "Actions refer to it by name; objects with the same name act together.", 12, UIKit.TextMuted);
		}

		void MovementGroup()
		{
			RectTransform g = UIKit.Group(body, "Movement (in a world)");
			RectTransform a = UIKit.Row(g, 28f, 6f, "SpinBob");
			UIKit.Size(UIKit.Label(a, "Spins", 13, UIKit.TextMuted).gameObject, 60);
			Field(a, "0", ObjectProps.Get(props, BehaviourProps.Spin), 70, "Degrees per second around the vertical (negative = the other way; 0 = still)", v => SetNumber(BehaviourProps.Spin, v));
			UIKit.Size(UIKit.Label(a, "\u00B0/s", 12, UIKit.TextMuted).gameObject, 30);
			bool own = ObjectProps.GetBool(props, BehaviourProps.SpinOwn, false);
			Button ownAxis = UIKit.Button(a, "Own axis", () => { if (own) props.Remove(BehaviourProps.SpinOwn); else props[BehaviourProps.SpinOwn] = "1"; Rebuild(); },
				"Spins around its own up axis instead of the vertical: tilt it first - a water wheel, a windmill's blades, a fan", 76, 26f, 12);
			UIKit.SetActive(ownAxis, own);
			UIKit.Size(UIKit.Label(a, "Bobs", 13, UIKit.TextMuted, TextAnchor.MiddleRight).gameObject, 60);
			Field(a, "0", ObjectProps.Get(props, BehaviourProps.Bob), 60, "How far it floats up and down (metres; 0 = not at all)", v => SetNumber(BehaviourProps.Bob, v));
			UIKit.Size(UIKit.Label(a, "m every", 12, UIKit.TextMuted).gameObject, 56);
			Field(a, "3", ObjectProps.Get(props, BehaviourProps.BobTime), 50, "Seconds for one up-and-down", v => SetNumber(BehaviourProps.BobTime, v));
			UIKit.Size(UIKit.Label(a, "s", 12, UIKit.TextMuted).gameObject, 14);
			UIKit.Label(a, "", 12);

			string mode = !BehaviourProps.Moves(props) ? "none" : ObjectProps.Get(props, BehaviourProps.MoveMode) == "loop" ? "loop" : "switch";
			RectTransform m = UIKit.Row(g, 28f, 6f, "Mode");
			UIKit.Size(UIKit.Label(m, "Moves", 13, UIKit.TextMuted).gameObject, 60);
			Button none = UIKit.Button(m, "No", () => { props.Remove(BehaviourProps.Move); props.Remove(BehaviourProps.Turn); props.Remove(BehaviourProps.MoveMode); props.Remove(BehaviourProps.Carry); Rebuild(); }, "It stays where it is", 60, 26f, 12);
			Button sw = UIKit.Button(m, "Opens and closes", () => { EnsureMove(); props.Remove(BehaviourProps.MoveMode); Rebuild(); }, "A door, gate, bridge or lift: actions (or using it) open and close it", 150, 26f, 12);
			Button loop = UIKit.Button(m, "Back and forth", () => { EnsureMove(); props[BehaviourProps.MoveMode] = "loop"; Rebuild(); }, "Moves to the other pose and back, for ever (a platform, a swinging sign)", 130, 26f, 12);
			UIKit.SetActive(none, mode == "none");
			UIKit.SetActive(sw, mode == "switch");
			UIKit.SetActive(loop, mode == "loop");
			if (mode == "none") return;
			// (a lift or a moving platform: the player standing on it rides along - LM12)
			bool carry = ObjectProps.GetBool(props, BehaviourProps.Carry, false);
			Button ride = UIKit.Button(m, "Carries players", () => { if (carry) props.Remove(BehaviourProps.Carry); else props[BehaviourProps.Carry] = "1"; Rebuild(); },
				"A lift or a moving platform: players standing on it ride along", 130, 26f, 12);
			UIKit.SetActive(ride, carry);

			Vector3 off = BehaviourProps.Offset(props);
			RectTransform o = UIKit.Row(g, 28f, 6f, "Offset");
			UIKit.Size(UIKit.Label(o, (mode == "loop" ? "To" : "Open") + ": move", 13, UIKit.TextMuted).gameObject, 90);
			string[] axes = { "x (east)", "y (up)", "z (north)" };
			for (int i = 0; i < 3; i++)
			{
				int axis = i;
				Field(o, axes[i], off[i] == 0f ? "" : off[i].ToString("0.##", CultureInfo.InvariantCulture), 70, "Metres along " + axes[i], v =>
				{
					Vector3 cur = BehaviourProps.Offset(props);
					float f;
					cur[axis] = float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f;
					if (cur == Vector3.zero) props.Remove(BehaviourProps.Move); else props[BehaviourProps.Move] = BehaviourProps.OffsetText(cur);
				});
			}
			UIKit.Size(UIKit.Label(o, "m, turn", 12, UIKit.TextMuted).gameObject, 52);
			Field(o, "0", ObjectProps.Get(props, BehaviourProps.Turn), 56, "Degrees it turns around its up axis (a door: 90)", v => SetNumber(BehaviourProps.Turn, v));
			UIKit.Size(UIKit.Label(o, "\u00B0 in", 12, UIKit.TextMuted).gameObject, 34);
			Field(o, "2", ObjectProps.Get(props, BehaviourProps.MoveTime), 50, "Seconds to get there", v => SetNumber(BehaviourProps.MoveTime, v));
			UIKit.Size(UIKit.Label(o, "s", 12, UIKit.TextMuted).gameObject, 14);
			UIKit.Button(o, previewing ? "..." : "Preview", () => StartCoroutine(Preview()), "Show the movement here in the editor", 90, 26f, 12);
		}

		void EnsureMove()
		{
			if (!BehaviourProps.Moves(props)) props[BehaviourProps.Turn] = "90";
		}

		void WorldGroup(bool zone, bool creature, bool helper)
		{
			RectTransform g = UIKit.Group(body, "In a world");
			bool hidden = BehaviourProps.StartsHidden(props);
			RectTransform a = UIKit.Row(g, 26f, 6f, "Start");
			UIKit.Size(UIKit.Label(a, "At first", 13, UIKit.TextMuted).gameObject, 110);
			Button there = UIKit.Button(a, "There", () => { props.Remove(BehaviourProps.Hidden); Rebuild(); }, "It is there from the start", 110, 26f, 12);
			Button gone = UIKit.Button(a, creature ? "Hidden (ambush)" : zone ? "Off until shown" : "Hidden until shown", () => { props[BehaviourProps.Hidden] = "1"; Rebuild(); },
				creature ? "Its animals appear when a \"show\" action names it" : "It appears when a \"show\" action names it (give it a name)", 150, 26f, 12);
			UIKit.SetActive(there, !hidden);
			UIKit.SetActive(gone, hidden);
			UIKit.Label(a, "", 12);
			if (zone || creature) return;

			if (!ObjectProps.IsNote(target.GameObjectName, props) && !ObjectProps.IsLoot(target.GameObjectName, props) && !helper)
			{
				string use = ObjectProps.Get(props, BehaviourProps.Use);
				RectTransform u = UIKit.Row(g, 28f, 6f, "Use");
				UIKit.Size(UIKit.Label(u, "Players can use it", 13, UIKit.TextMuted).gameObject, 110);
				Button no = UIKit.Button(u, "No", () => { props.Remove(BehaviourProps.Use); Rebuild(); }, "Nothing happens when players look at it", 60, 26f, 12);
				Button yes = UIKit.Button(u, "Yes", () => { if (use.Length == 0) props[BehaviourProps.Use] = "Use"; Rebuild(); }, "Players look at it and press the interact key (E): its \"uses it\" actions run", 60, 26f, 12);
				UIKit.SetActive(no, use.Length == 0);
				UIKit.SetActive(yes, use.Length > 0);
				if (use.Length > 0) Field(u, "Pull the lever", use, 260, "The hint players see (with the key)", v => { if (v.Trim().Length > 0) props[BehaviourProps.Use] = v.Trim(); });
				else UIKit.Label(u, "", 12);
			}

			if (helper) return;
			string mode = ObjectProps.Get(props, BehaviourProps.Collision);
			RectTransform c = UIKit.Row(g, 26f, 6f, "Collision");
			UIKit.Size(UIKit.Label(c, "Collision", 13, UIKit.TextMuted).gameObject, 110);
			string[] labels = { "Raft's own", "Walk through", "One box", "Solid" };
			string[] hints = { "As in Raft", "Players and the raft pass through it", "A simple box around it (easier to stand on)", "A box only if it has no collision of its own" };
			for (int i = 0; i < labels.Length; i++)
			{
				string m = BehaviourProps.CollisionModes[i];
				Button b = UIKit.Button(c, labels[i], () => { if (m.Length == 0) props.Remove(BehaviourProps.Collision); else props[BehaviourProps.Collision] = m; Rebuild(); }, hints[i], 110, 26f, 12);
				UIKit.SetActive(b, mode == m);
			}
			UIKit.Label(c, "", 12);
		}

		void EventGroup(string ev)
		{
			RectTransform g = UIKit.Group(body, "When " + EventText(ev));
			List<ObjAction> list = actions[ev];
			for (int i = 0; i < list.Count; i++) ActionRow(g, ev, list, i);
			if (list.Count == 0) UIKit.Label(g, ev == "use" && BehaviourProps.Switches(props) ? "<i>Nothing set: using it opens and closes it.</i>" : "<i>Nothing happens yet.</i>", 12, UIKit.TextMuted);
			FlipWarning(g, ev, list);
			RectTransform add = UIKit.Row(g, 26f, 6f, "Add");
			UIKit.Button(add, "+ Add an action", () => { Keep(); list.Add(new ObjAction { Verb = list.Count == 0 && ev != "use" ? "message" : "show" }); Rebuild(); }, "Another thing that happens", 150, 26f, 12);
			UIKit.Button(add, "+ Wait", () => { Keep(); list.Add(new ObjAction { Verb = "wait", Arg = "5" }); Rebuild(); }, "The actions after it happen that many seconds later (a gate that closes again, an ambush after a moment)", 90, 26f, 12);
			UIKit.Button(add, "+ Only if...", () => { Keep(); checks[ev].Add(new ObjCheck { Kind = "take" }); Rebuild(); }, "A check before the actions: the player has (or gives up) an item or story item, an object is open or closed, a signal was sent, the quest is far enough", 110, 26f, 12);
			UIKit.Label(add, "", 12);
			if (checks[ev].Count > 0) ChecksPart(g, ev);
		}

		/// <summary>Show/hide or open/close in an event that can happen again flips it back (what Check warns about).
		/// A zone's "enter" also gets a button for Fires: Once ever. Rebuild() runs after every action or setting change.</summary>
		void FlipWarning(Transform g, string ev, List<ObjAction> list)
		{
			if (islandMode || target == null || !list.Any(a => a.Verb == "toggle" || a.Verb == "switch")) return;
			string evOf;
			string again = PlanChecker.EventAgain(target.GameObjectName, props, PlanChecker.IslandNeverRegrows(DynamicIslands.currentIslandProps), out evOf);
			if (again.Length == 0 || evOf != ev) return;
			RectTransform row = UIKit.Row(g, 26f, 6f, "AgainWarning");
			Text w = UIKit.Label(row, "<color=#e0a040>This can happen again (" + again + "): show/hide and open/close flip it back. Use show/open, or Fires: Once ever.</color>", 11, UIKit.TextColor);
			UIKit.Size(w.gameObject, -1, -1, 1);
			if (ev == "enter")
				UIKit.Button(row, "Fires once ever", () => { Keep(); props[ObjectProps.ZoneRepeat] = ObjectProps.ZoneOnceEver; Rebuild(); }, "Fires: Once ever - the zone never fires again, so show/hide is safe", 120, 24f, 12);
		}

		static readonly Dictionary<string, string> CheckLabels =new Dictionary<string, string>
		{
			{ "has", "has item" }, { "take", "uses up item" }, { "state", "object is" }, { "signal", "signal sent" }, { "quest", "quest step" }, { "wears", "wears item" }, { "holds", "holds item" },
		};

		/// <summary>"Only if ..." (every check must pass) and "Otherwise say ...".</summary>
		void ChecksPart(Transform g, string ev)
		{
			RectTransform box = UIKit.Rect("Checks", g);
			UIKit.Background(box.gameObject, new Color(0.23f, 0.13f, 0.06f, 0.35f), 6);
			UIKit.Vertical(box.gameObject, 4f, new RectOffset(8, 8, 5, 6));
			RectTransform head = UIKit.Row(box, 24f, 6f, "Title");
			UIKit.Size(UIKit.Label(head, "ONLY IF", 12, UIKit.Tan, TextAnchor.MiddleLeft, FontStyle.Normal, "Title").gameObject, 60);
			Button mode = UIKit.Button(head, anyOf[ev] ? "any of these" : "all of these", () => { Keep(); anyOf[ev] = !anyOf[ev]; Rebuild(); },
				"All: every check must pass. Any: one passing check is enough (only its items are used up). Click to change", 120, 24f, 12);
			UIKit.SetActive(mode, anyOf[ev]);
			UIKit.Label(head, "", 12);
			List<ObjCheck> list = checks[ev];
			for (int i = 0; i < list.Count; i++) CheckRow(box, list, i);
			// (no single player sets these off: Raft items are in one player's inventory, and the host's is looked at)
			if ((ev == "defeat" || ev == "quest") && list.Any(c => (c.Kind == "has" || c.Kind == "take") && !StoryItems.IsStory(c.Target)))
				UIKit.Label(box, "<color=#e0a040>No single player sets this off: an item check here looks at the host's inventory. Use story items (the whole crew's) instead.</color>", 11, UIKit.TextColor);
			RectTransform other = UIKit.Row(box, 28f, 6f, "Otherwise");
			UIKit.Size(UIKit.Label(other, "Otherwise say", 12, UIKit.TextMuted).gameObject, 96);
			List<ObjAction> el = elses[ev];
			ObjAction msg = el.FirstOrDefault(a => a.Verb == "message");
			Field(other, "It's locked. Maybe there's a key somewhere...", msg != null ? msg.Arg : "", -1, "What the player reads when a check fails (empty = nothing happens)", v =>
			{
				ObjAction m = el.FirstOrDefault(a => a.Verb == "message");
				if (v.Trim().Length == 0) { if (m != null) el.Remove(m); }
				else if (m != null) m.Arg = v.Trim();
				else el.Insert(0, new ObjAction { Verb = "message", Arg = v.Trim() });
			});
		}

		void CheckRow(Transform g, List<ObjCheck> list, int index)
		{
			ObjCheck c = list[index];
			RectTransform row = UIKit.Row(g, 28f, 4f, "Check");
			Button not = UIKit.Button(row, c.Not ? "not" : "is", () => { Keep(); c.Not = !c.Not; Rebuild(); }, "\"not\" turns the check round: it passes when this is NOT so (the player hasn't got the key yet...)", 44, 28f, 12);
			UIKit.SetActive(not, c.Not);
			DropList.Make(row, "Drop_CheckKind", BehaviourProps.CheckKinds.Select(k => new DropList.Option(k, CheckLabels[k], CheckHints[k])).ToList(), c.Kind, kind =>
			{
				Keep();
				c.Kind = kind;
				if (c.Kind == "state" && !BehaviourProps.States.Contains(c.Arg)) c.Arg = "closed";
				else if (!c.IsItem && c.Kind != "state") c.Arg = "";
				Rebuild();
			}, 130, "The check: has an item, uses up an item, an object is open/closed/shown/hidden, a signal was sent, the quest reached a step, wears an item, holds an item", 28f, 12);
			if (c.IsItem)
			{
				Field(row, "item (\u2026 to choose; story:<id> for a story item)", c.Target, -1, "Raft's unique item name, or story:<id> for one of the island's story items", v => c.Target = v.Trim());
				UIKit.Button(row, "\u2026", () => { Keep(); ItemPickerWindow.PickOne(v => { c.Target = v; Rebuild(); }, true); }, "Choose an item or story item", 28, 28f, 12);
				UIKit.Size(UIKit.Label(row, "\u00D7", 12, UIKit.TextMuted, TextAnchor.MiddleCenter).gameObject, 14);
				InputField n = Field(row, "1", c.Arg, 40, "How many", v => c.Arg = v.Trim());
				n.contentType = InputField.ContentType.IntegerNumber;
			}
			else if (c.Kind == "state")
			{
				Field(row, "object name", c.Target, 150, "Objects with this name (all of them must be so)", v => c.Target = v.Trim());
				UIKit.Button(row, "\u2026", () => { Keep(); ChoiceWindow.Open("Objects with a name", NameChoices().Skip(1), v => { c.Target = v; Rebuild(); }); }, "Choose from the names on this island", 28, 28f, 12);
				DropList.Make(row, "Drop_State", BehaviourProps.States.Select(st => new DropList.Option(st, st)).ToList(), BehaviourProps.States.Contains(c.Arg) ? c.Arg : "open", st => { Keep(); c.Arg = st; Rebuild(); }, 90, "open, closed, shown or hidden", 28f, 12);
			}
			else if (c.Kind == "wears" || c.Kind == "holds")
			{
				Field(row, c.Kind == "wears" ? "item worn (HeadLight*, Flipper*...)" : "item in hand (Binoculars, Hook*, Axe*...)", c.Target, -1, "Raft's unique item name; a * at the end matches every kind (Axe* = any axe). The mod's upgrades count as the item they improve", v => c.Target = v.Trim());
				UIKit.Button(row, "\u2026", () => { Keep(); ItemPickerWindow.PickOne(v => { c.Target = v; Rebuild(); }, false); }, "Choose an item", 28, 28f, 12);
			}
			else if (c.Kind == "signal") Field(row, "signal name", c.Target, -1, "A signal sent on this island (a \"send signal\" action)", v => c.Target = v.Trim());
			else Field(row, "steps done (empty = the whole quest)", c.Target, -1, "How many steps of the island's quest must be done", v => c.Target = v.Trim());
			Text d = UIKit.Label(row, c.Describe(), 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Size(d.gameObject, 250);
			UIKit.Fit(d, 8);
			Button del = UIKit.Button(row, "\u00D7", () => { Keep(); list.RemoveAt(index); Rebuild(); }, "Remove this check", 26, 28f, 12);
			UIKit.DangerButton(del);
		}

		void ActionRow(Transform g, string ev, List<ObjAction> list, int index)
		{
			ObjAction a = list[index];
			RectTransform row = UIKit.Row(g, 28f, 4f, "Action");
			UIKit.Size(UIKit.Label(row, (index + 1) + ".", 12, UIKit.TextMuted, TextAnchor.MiddleRight).gameObject, 20);
			DropList.Make(row, "Drop_Verb", BehaviourProps.Verbs.Select(v => new DropList.Option(v, VerbLabels[v], VerbHints[v])).ToList(), a.Verb, verb =>
			{
				Keep();
				a.Verb = verb;
				if (a.Verb == "wait" && a.Seconds <= 0f) a.Arg = "5";
				Rebuild();
			}, 120, "What happens: show, hide, show/hide, open, close, open/close, say, give items, sound, teleport to, send signal, journal page, wait, unlock character, play sound/music, stop sound, camera moment, protective suit, hurt creatures", 28f, 12);
			if (ObjAction.HasTarget(a.Verb))
			{
				Field(row, a.Verb == "camera" ? "marker (empty = itself)" : "name (empty = itself)", a.Target, a.Verb == "camera" || a.Verb.EndsWith("sound") ? 120 : 170,
					a.Verb == "camera" ? "The object the camera moves to (players within range of it see the moment)" : a.Verb.EndsWith("sound") ? "The object the sound plays at" : "The name of the objects it acts on", v => a.Target = v.Trim());
				UIKit.Button(row, "\u2026", () => { Keep(); ChoiceWindow.Open("Objects with a name", NameChoices(), v => { a.Target = v; Rebuild(); }); }, "Choose from the names on this island", 28, 28f, 12);
			}
			if (a.Verb == "journal") Field(row, "page title", a.Target, 170, "The page's title in the crew's journal", v => a.Target = v.Trim());
			if (a.Verb == "wait")
			{
				InputField s = Field(row, "5", a.Arg, 60, "Seconds until the actions after it happen", v => a.Arg = v.Trim());
				s.contentType = InputField.ContentType.DecimalNumber;
				UIKit.Label(row, "seconds, then the actions below", 12, UIKit.TextMuted);
			}
			else if (a.Verb == "playsound") SoundFields(row, a);
			else if (a.Verb == "stopsound")
			{
				DropList.Make(row, "Drop_StopWhat", new List<DropList.Option> { new DropList.Option("", "its sound", "The sound the named object (or this one) plays"),
					new DropList.Option("all", "every sound", "Every sound played on this island") }, a.Arg.Trim().ToLowerInvariant() == "all" ? "all" : "", v => { Keep(); a.Arg = v; Rebuild(); }, 110, "Which sounds stop", 28f, 12);
			}
			else if (a.Verb == "camera") CameraFields(row, a);
			else if (ObjAction.HasArg(a.Verb))
			{
				string ph = a.Verb == "message" ? "What players read" : a.Verb == "give" ? "items (\u2026 to choose)" : a.Verb == "sound" ? "event:/... (\u2026 to choose)" : a.Verb == "journal" ? "What the page says" : a.Verb == "suit" ? "seconds (120)" : a.Verb == "hit" ? "% of their health (25)" : "signal name (world plans wait for it)";
				Field(row, ph, a.Arg, -1, a.Verb == "signal" ? "World plans and island rules can wait for it: \"signal sent at\" this island" : a.Verb == "journal" ? "Written into the crew's journal (J in a world)" : "", v => a.Arg = v.Trim());
				if (a.Verb == "give") UIKit.Button(row, "\u2026", () => { Keep(); ItemPickerWindow.OpenFor(() => a.Arg, v => { a.Arg = v; }); StartCoroutine(RebuildWhenClosed()); }, "Choose Raft's items or the island's story items", 28, 28f, 12);
				if (a.Verb == "sound") UIKit.Button(row, "\u2026", () => { Keep(); SoundPickerWindow.OpenFor(v => { a.Arg = v; Rebuild(); }); }, "Choose one of Raft's sounds (you can listen first)", 28, 28f, 12);
			}
			else UIKit.Label(row, "", 12);
			Text d = UIKit.Label(row, a.Describe(), 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Size(d.gameObject, 250);
			UIKit.Fit(d, 8);
			UIKit.Button(row, "\u25B2", () => { if (index > 0) { Keep(); list.Reverse(index - 1, 2); Rebuild(); } }, "Earlier", 26, 28f, 11);
			Button del = UIKit.Button(row, "\u00D7", () => { Keep(); list.RemoveAt(index); Rebuild(); }, "Remove this action", 26, 28f, 12);
			UIKit.DangerButton(del);
		}

		/// <summary>playsound: the sound (\u2026 picks it), its volume, once or a loop, near the object or for the whole island.</summary>
		void SoundFields(Transform row, ObjAction a)
		{
			TriggerSounds.Spec s = TriggerSounds.Parse(a.Arg);
			Action save = () => a.Arg = TriggerSounds.Join(s);
			Field(row, "event:/... (\u2026 to choose)", s.Path, -1, "One of Raft's sound events or music cues", v => { s.Path = v.Trim(); save(); });
			UIKit.Button(row, "\u2026", () => { Keep(); SoundPickerWindow.OpenFor(v => { s.Path = v; save(); Rebuild(); }); }, "Choose one of Raft's sounds or music (you can listen first)", 28, 28f, 12);
			InputField vol = Field(row, "90", Mathf.RoundToInt(s.Volume * 100f).ToString(CultureInfo.InvariantCulture), 40, "Volume, 0-100 %", v =>
			{
				float f;
				if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) { s.Volume = Mathf.Clamp01(f / 100f); save(); }
			});
			vol.contentType = InputField.ContentType.DecimalNumber;
			DropList.Make(row, "Drop_Loop", new List<DropList.Option> { new DropList.Option("once", "once", "Plays once"), new DropList.Option("loop", "loop", "Plays on until a stop sound action (kept after a reload)") },
				s.Loop ? "loop" : "once", v => { Keep(); s.Loop = v == "loop"; save(); Rebuild(); }, 62, "Once or on a loop", 28f, 12);
			DropList.Make(row, "Drop_Where", new List<DropList.Option> { new DropList.Option("near", "near it", "Heard near the object"), new DropList.Option("island", "island", "Heard the same everywhere on the island (music)") },
				s.Island ? "island" : "near", v => { Keep(); s.Island = v == "island"; save(); Rebuild(); }, 72, "Near the object, or for everyone on the island", 28f, 12);
		}

		/// <summary>camera: the object to look at, the seconds and the range around the marker.</summary>
		void CameraFields(Transform row, ObjAction a)
		{
			string look; float sec, range;
			CameraMoment.Parse(a.Arg, out look, out sec, out range);
			Action save = () => a.Arg = look + "|" + sec.ToString("0.#", CultureInfo.InvariantCulture) + "|" + range.ToString("0", CultureInfo.InvariantCulture);
			Field(row, "look at (name)", look, 110, "The object the camera looks at (empty = the way the marker faces)", v => { look = v.Trim(); save(); });
			UIKit.Button(row, "\u2026", () => { Keep(); ChoiceWindow.Open("Look at", NameChoices(), v => { look = v; save(); Rebuild(); }); }, "Choose from the names on this island", 28, 28f, 12);
			InputField s = Field(row, "4", sec.ToString("0.#", CultureInfo.InvariantCulture), 36, "Seconds the camera stays (1-30)", v =>
			{
				float f;
				if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) { sec = Mathf.Clamp(f, 1f, 30f); save(); }
			});
			s.contentType = InputField.ContentType.DecimalNumber;
			InputField r = Field(row, "50", range.ToString("0", CultureInfo.InvariantCulture), 40, "Players within this many metres of the marker see it", v =>
			{
				float f;
				if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) { range = Mathf.Clamp(f, 5f, 1000f); save(); }
			});
			r.contentType = InputField.ContentType.DecimalNumber;
			UIKit.Label(row, "s, m", 11, UIKit.TextMuted);
		}

		IEnumerator RebuildWhenClosed()
		{
			while (ItemPickerWindow.IsOpen) yield return null;
			Rebuild();
		}

		/// <summary>The names given to objects on this island (and "self").</summary>
		static IEnumerable<ChoiceWindow.Choice> NameChoices()
		{
			GameObject placed = GameObject.Find("PlacedObjects");
			var names = placed == null ? new List<EditorGameObject>() : placed.GetComponentsInChildren<EditorGameObject>(true).Where(e => ObjectProps.Get(e.Props, BehaviourProps.Name).Length > 0).ToList();
			yield return new ChoiceWindow.Choice("", "(itself)", "the object these actions belong to");
			foreach (var grp in names.GroupBy(e => ObjectProps.Get(e.Props, BehaviourProps.Name)).OrderBy(x => x.Key))
				yield return new ChoiceWindow.Choice(grp.Key, grp.Key, grp.Count() + " object(s): " + string.Join(", ", grp.Select(e => PlaceableCatalog.DisplayName(e.GameObjectName)).Distinct().Take(3).ToArray()));
		}

		InputField Field(Transform row, string placeholder, string text, float width, string hint, Action<string> set)
		{
			InputField f = UIKit.Field(row, placeholder, text, 26f, hint);
			if (width > 0) UIKit.Size(f.gameObject, width, 26);
			((Text)f.placeholder).fontSize = 12;
			f.textComponent.fontSize = 12;
			f.onEndEdit.AddListener(v => { set(v); Summarise(); });
			fields.Add(f);
			return f;
		}

		void SetProp(string key, string value) { if (string.IsNullOrEmpty(value)) props.Remove(key); else props[key] = value; }

		void SetNumber(string key, string v)
		{
			float f;
			if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && f != 0f) props[key] = ObjectProps.Format(f); else props.Remove(key);
		}

		#endregion

		#region Saving, preview

		/// <summary>Takes what is typed into a focused field.</summary>
		void Keep()
		{
			foreach (InputField f in fields.Where(f => f != null && f.isFocused).ToList()) f.onEndEdit.Invoke(f.text);
		}

		/// <summary>The settings as they will be saved.</summary>
		Dictionary<string, string> Result()
		{
			var p = new Dictionary<string, string>(props);
			foreach (var kv in actions)
			{
				List<ObjAction> list = kv.Value.Where(a => !(ObjAction.HasArg(a.Verb) && a.Arg.Length == 0)).ToList();
				if (list.Count == 0) p.Remove(BehaviourProps.EventKey(kv.Key));
				else p[BehaviourProps.EventKey(kv.Key)] = ObjAction.ToLines(list);
			}
			foreach (var kv in checks)
			{
				List<ObjCheck> list = kv.Value.Where(c => c.Target.Length > 0 || c.Kind == "quest").ToList();
				if (list.Count == 0) { p.Remove(BehaviourProps.CheckKey(kv.Key)); p.Remove(BehaviourProps.ElseKey(kv.Key)); continue; }
				p[BehaviourProps.CheckKey(kv.Key)] = ObjCheck.ToLines(list, anyOf[kv.Key]);
				List<ObjAction> otherwise = elses[kv.Key].Where(a => !(ObjAction.HasArg(a.Verb) && a.Arg.Length == 0)).ToList();
				if (otherwise.Count == 0) p.Remove(BehaviourProps.ElseKey(kv.Key));
				else p[BehaviourProps.ElseKey(kv.Key)] = ObjAction.ToLines(otherwise);
			}
			return p;
		}

		void Summarise()
		{
			if (summaryText != null) summaryText.text = Summary(Result(), islandMode ? null : target.GameObjectName);
		}

		/// <summary>One line about what an object does (the inspector shows it too).</summary>
		public static string Summary(IDictionary<string, string> p, string objectName)
		{
			var parts = new List<string>();
			string name = ObjectProps.Get(p, BehaviourProps.Name);
			if (name.Length > 0) parts.Add("named '" + name + "'");
			if (ObjectProps.GetFloat(p, BehaviourProps.Spin, 0f) != 0f) parts.Add(ObjectProps.GetBool(p, BehaviourProps.SpinOwn, false) ? "spins (own axis)" : "spins");
			if (ObjectProps.GetFloat(p, BehaviourProps.Bob, 0f) != 0f) parts.Add("bobs");
			if (BehaviourProps.Moves(p)) parts.Add(BehaviourProps.Switches(p) ? "opens and closes" : "moves back and forth");
			if (BehaviourProps.StartsHidden(p)) parts.Add("hidden at first");
			if (ObjectProps.Get(p, BehaviourProps.Use).Length > 0) parts.Add("usable (\"" + ObjectProps.Get(p, BehaviourProps.Use) + "\")");
			string col = ObjectProps.Get(p, BehaviourProps.Collision);
			if (col.Length > 0) parts.Add(col == "none" ? "walk-through" : col == "box" ? "box collision" : "solid");
			int n = p.Keys.Where(k => k.StartsWith(BehaviourProps.EventPrefix)).Sum(k => ObjAction.ParseLines(p[k]).Count);
			if (n > 0) parts.Add(n + " action(s)");
			int c = p.Keys.Where(k => k.StartsWith(BehaviourProps.CheckPrefix)).Sum(k => ObjCheck.ParseLines(p[k]).Count);
			if (c > 0) parts.Add(c + " check(s)");
			return parts.Count == 0 ? "Nothing set: it just stands there." : string.Join(" \u00B7 ", parts.ToArray());
		}

		void Save()
		{
			Keep();
			Dictionary<string, string> result = Result();
			if (islandMode)
			{
				Func<string, bool> eventKey = k => k.StartsWith(BehaviourProps.EventPrefix) || k.StartsWith(BehaviourProps.CheckPrefix) || k.StartsWith(BehaviourProps.ElsePrefix);
				IslandSettingsUndo.Change(() =>
				{
					foreach (string k in DynamicIslands.currentIslandProps.Keys.Where(eventKey).ToList()) DynamicIslands.currentIslandProps.Remove(k);
					foreach (var kv in result.Where(kv => eventKey(kv.Key))) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
				});
				EditorUI.RefreshIsland();
				DynamicIslands.Notify("Island events kept (save the island, Ctrl+S)");
			}
			else if (target != null)
			{
				PropsCommand.Change(target, result);
				DynamicIslands.Notify("Behaviour kept: " + Summary(result, target.GameObjectName));
			}
			Close();
		}

		/// <summary>Plays the movement once in the editor (open, then back) without changing anything.</summary>
		IEnumerator Preview()
		{
			if (previewing || target == null) yield break;
			Keep();
			previewing = true;
			Transform t = target.transform;
			Vector3 pos = t.position;
			Quaternion rot = t.rotation;
			previewObject = t; previewPos = pos; previewRot = rot;
			Vector3 off = BehaviourProps.Offset(props);
			float turn = ObjectProps.GetFloat(props, BehaviourProps.Turn, 0f), time = Mathf.Max(0.1f, ObjectProps.GetFloat(props, BehaviourProps.MoveTime, 2f));
			for (float phase = 0f; phase < 2f && t != null; phase += Time.unscaledDeltaTime / time)
			{
				float s = Mathf.SmoothStep(0f, 1f, phase < 1f ? phase : 2f - phase);
				t.position = pos + off * s;
				t.rotation = Quaternion.Euler(0f, turn * s, 0f) * rot;
				yield return null;
			}
			if (t != null) { t.position = pos; t.rotation = rot; }
			previewing = false;
		}

		#endregion
	}
}
