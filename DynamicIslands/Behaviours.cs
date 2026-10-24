using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Behaviours and events of island objects (object settings, saved with the island):
	///   obj.name                        a name that actions refer to (several objects may share one: they act together)
	///   beh.spin                        turns around the vertical (degrees per second); beh.spinOwn = 1: around its own
	///                                   up axis instead (tilted first: a water wheel, a windmill's blades, a fan)
	///   beh.bob / beh.bobTime           floats up and down (metres, seconds per cycle)
	///   beh.move (x,y,z) / beh.turn / beh.moveTime / beh.moveMode
	///                                   moves by an offset and turns (degrees) over a time: "loop" = back and forth for
	///                                   ever, "switch" = a door, gate or lift that actions (or using it) open and close
	///   beh.carry = 1                   a mover that carries the player standing on it (a lift, a moving platform)
	///   beh.hidden = 1                  not there at first; a "show" action makes it appear (creatures: an ambush)
	///   beh.use                         players can use it (interact key); the text is the hint ("Pull the lever")
	///   col.mode                        collision: "" = Raft's own, "none" = walk through, "box" = one box around it,
	///                                   "solid" = a box only if it has no collider of its own
	///   on.&lt;event&gt;                     actions, one per line "verb|target|argument", when: use (it is used), enter (a trigger
	///                                   zone fires), read (a note is read the first time), open (a chest is opened),
	///                                   defeat (all the animals of a creature spot are defeated), laser (a laser beam
	///                                   reaches it - LaserBeam.cs; beh.laser = beam / mirror). The island has
	///                                   on.arrive (players first come to it) and on.quest (its quest is done).
	///   if.&lt;event&gt;                     checks that must all pass before the actions run, one per line "kind|target|argument":
	///                                   has (the player has items; "story:&lt;id&gt;" = a story item of the crew), take (has
	///                                   them, and they are used up), state (objects with the name are open / closed / shown
	///                                   / hidden), signal (was sent on this island), quest (the island's quest reached a step).
	///                                   "!kind|..." = NOT so; a line "any" = one passing check is enough. A chest checks
	///                                   "if.open" before it gives its loot (a locked chest)
	///   else.&lt;event&gt;                   actions when a check fails (usually a message: "It's locked.")
	/// Verbs: show, hide, toggle (whether objects are there), open, close, switch (movers), message, give (items, story
	/// items too), sound (one of Raft's sounds), teleport (the player to an object), signal (world plan and island rules can
	/// wait for it: "signal:&lt;island&gt;:&lt;name&gt;"), journal (a page in the crew's journal), wait (the actions after it
	/// run that many seconds later), character (unlocks one of Raft's characters for the player, by name or number).
	/// </summary>
	public static class BehaviourProps
	{
		public const string SpinOwn = "beh.spinOwn";
		/// <summary>A mover that carries the player standing on it along (a lift, a moving platform - ROADMAP LM12).</summary>
		public const string Carry = "beh.carry";
		public const string Name = "obj.name", Spin = "beh.spin", Bob = "beh.bob", BobTime = "beh.bobTime", Move = "beh.move", Turn = "beh.turn",
			MoveTime = "beh.moveTime", MoveMode = "beh.moveMode", Hidden = "beh.hidden", Use = "beh.use", Collision = "col.mode";
		public const string EventPrefix = "on.";
		public static readonly string[] ObjectEvents = { "use", "enter", "read", "open", "defeat", "laser", BossPhases.Event };
		public static readonly string[] IslandEvents = { "arrive", "quest" };
		public static readonly string[] Verbs = { "show", "hide", "toggle", "open", "close", "switch", "message", "give", "sound", "teleport", "signal", "journal", "wait", "character", "playsound", "stopsound", "camera", "suit", "hit" };
		public static readonly string[] SharedVerbs = { "show", "hide", "toggle", "open", "close", "switch", "signal", "journal", "playsound", "stopsound", "camera", "hit" };
		public const string CheckPrefix = "if.", ElsePrefix = "else.";
		public static readonly string[] CheckKinds = { "has", "take", "state", "signal", "quest", "wears", "holds" };
		public static readonly string[] States = { "open", "closed", "shown", "hidden" };
		public static readonly string[] CollisionModes = { "", "none", "box", "solid" };

		/// <summary>The setting with an event's actions; "use!" = the actions when its checks fail.</summary>
		public static string EventKey(string ev) { return ev.EndsWith("!") ? ElsePrefix + ev.TrimEnd('!') : EventPrefix + ev; }
		public static string CheckKey(string ev) { return CheckPrefix + ev.TrimEnd('!'); }
		public static string ElseKey(string ev) { return ElsePrefix + ev.TrimEnd('!'); }

		public static Vector3 Offset(IDictionary<string, string> p)
		{
			string[] v = ObjectProps.Get(p, Move).Split(',');
			float x, y, z;
			if (v.Length == 3 && float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
				float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return new Vector3(x, y, z);
			return Vector3.zero;
		}

		public static string OffsetText(Vector3 v)
		{
			return string.Join(",", new[] { v.x, v.y, v.z }.Select(f => f.ToString("0.##", CultureInfo.InvariantCulture)).ToArray());
		}

		public static bool Moves(IDictionary<string, string> p) { return Offset(p) != Vector3.zero || ObjectProps.GetFloat(p, Turn, 0f) != 0f; }
		public static bool Switches(IDictionary<string, string> p) { return Moves(p) && ObjectProps.Get(p, MoveMode) != "loop"; }
		public static bool StartsHidden(IDictionary<string, string> p) { return ObjectProps.GetBool(p, Hidden, false); }

		/// <summary>True when the object has anything this file handles (so it gets a behaviour in a world).</summary>
		public static bool Any(IDictionary<string, string> p)
		{
			return p != null && p.Keys.Any(k => k.StartsWith("beh.") || k.StartsWith("col.") || k.StartsWith(EventPrefix) || k.StartsWith(CheckPrefix) || k.StartsWith(ElsePrefix) || k == Name);
		}

		/// <summary>What the events a kind of object can have are: (event, what the builder reads).</summary>
		public static List<KeyValuePair<string, string>> EventsFor(string objectName, IDictionary<string, string> p)
		{
			var list = new List<KeyValuePair<string, string>>();
			if (ContentCatalog.IsHelper(objectName)) return list;
			if (objectName == ContentCatalog.TriggerZone) list.Add(new KeyValuePair<string, string>("enter", "a player walks into the zone"));
			else if (ContentCatalog.IsCreature(objectName))
			{
				list.Add(new KeyValuePair<string, string>("defeat", "all its animals are defeated"));
				// (a boss phase, RF5: only for a creature that has one)
				if (BossPhases.Has(p)) list.Add(new KeyValuePair<string, string>(BossPhases.Event, "its boss phase starts (" + BossPhases.Describe(p) + ")"));
			}
			else if (!ContentCatalog.IsZone(objectName))
			{
				if (ObjectProps.IsNote(objectName, p)) list.Add(new KeyValuePair<string, string>("read", "it is read (the first time)"));
				if (ObjectProps.IsLoot(objectName, p)) list.Add(new KeyValuePair<string, string>("open", "it is opened (with checks: its loot stays locked until they pass)"));
				if (!ObjectProps.IsNote(objectName, p) && !ObjectProps.IsLoot(objectName, p)) list.Add(new KeyValuePair<string, string>("use", "a player uses it (" + ObjectProps.Get(p, Use, "needs \"Players can use it\"") + ")"));
				if (ObjectProps.Get(p, LaserBeam.Prop).Length == 0) list.Add(new KeyValuePair<string, string>(LaserBeam.Event, "a laser beam reaches it (a laser emitter's beam, maybe by way of mirrors)"));
			}
			return list;
		}
	}

	/// <summary>One action: a verb, a target (object name) and an argument (text, items, sound...).</summary>
	public class ObjAction
	{
		public string Verb = "show", Target = "", Arg = "";

		public static ObjAction Parse(string line)
		{
			string[] p = (line ?? "").Split('|');
			if (p.Length < 1 || !BehaviourProps.Verbs.Contains(p[0].Trim())) return null;
			var a = new ObjAction { Verb = p[0].Trim(), Target = p.Length > 1 ? p[1].Trim() : "", Arg = p.Length > 2 ? string.Join("|", p.Skip(2).ToArray()).Trim() : "" };
			if (a.Verb == "wait" && a.Arg.Length == 0) { a.Arg = a.Target; a.Target = ""; } // "wait|5" as well as "wait||5"
			return a;
		}

		public static List<ObjAction> ParseLines(string text) { return (text ?? "").Split('\n').Select(Parse).Where(a => a != null).ToList(); }

		public static string ToLines(IEnumerable<ObjAction> actions)
		{
			return string.Join("\n", actions.Select(a => a.Verb + "|" + Clean(a.Target) + "|" + (a.Arg ?? "").Replace("\n", " ").Trim()).ToArray());
		}

		static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace("\n", " ").Trim(); }

		public bool Shared { get { return BehaviourProps.SharedVerbs.Contains(Verb); } }

		/// <summary>Needs a target object (by name).</summary>
		public static bool HasTarget(string verb) { return verb == "show" || verb == "hide" || verb == "toggle" || verb == "open" || verb == "close" || verb == "switch" || verb == "teleport" || verb == "playsound" || verb == "stopsound" || verb == "camera" || verb == "hit"; }
		public static bool HasArg(string verb) { return verb == "suit" || verb == "hit" || verb == "message" || verb == "give" || verb == "sound" || verb == "signal" || verb == "journal" || verb == "wait" || verb == "character" || verb == "playsound" || verb == "stopsound" || verb == "camera"; }

		/// <summary>A wait's seconds (0..3600).</summary>
		public float Seconds
		{
			get
			{
				float s;
				return Verb == "wait" && float.TryParse(Arg, NumberStyles.Float, CultureInfo.InvariantCulture, out s) ? Mathf.Clamp(s, 0f, 3600f) : 0f;
			}
		}

		public string Describe()
		{
			string t = Target.Length > 0 ? "'" + Target + "'" : "itself";
			switch (Verb)
			{
				case "show": return "show " + t;
				case "hide": return "hide " + t;
				case "toggle": return "show or hide " + t;
				case "open": return "open " + t;
				case "close": return "close " + t;
				case "switch": return "open or close " + t;
				case "message": return "say \"" + Arg + "\"";
				case "give": return "give " + string.Join(", ", ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, Arg } }).Select(l => ContentCatalog.ItemLabel(l.Key) + " \u00D7" + l.Value).ToArray());
				case "sound": return "play " + (Arg.Length > 0 ? Arg.Substring(Arg.LastIndexOf('/') + 1) : "a sound");
				case "teleport": return "move the player to " + t;
				case "playsound": return TriggerSounds.Describe(Arg, t);
				case "stopsound": return Arg.Trim().Equals("all", StringComparison.OrdinalIgnoreCase) ? "stop every sound played on the island" : "stop the sound of " + t;
				case "camera": return CameraMoment.Describe(Target, Arg);
				case "suit": return "put a protective suit on the player (" + (Arg.Trim().Length > 0 ? Arg.Trim() : "120") + " s)";
				case "hit": return "hurt the animals of " + t + " (" + (Arg.Trim().Length > 0 ? Arg.Trim().TrimEnd('%') : "25") + "% of their health; a shielded boss too)";
				case "signal": return "send the signal '" + Arg + "' (world plans can wait for it)";
				case "journal": return "write \"" + (Target.Length > 0 ? Target : "a page") + "\" in the journal";
				case "wait": return "wait " + Seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s, then...";
				case "character":
					SO_Character c = Behaviours.CharacterOf(Arg);
					return "unlock the character " + (c != null ? c.displayName : "'" + Arg + "' (no such character)") + " for the player";
			}
			return Verb;
		}
	}

	/// <summary>
	/// One check before an event's actions run: a kind and what it looks at.
	///   has|&lt;item&gt;|&lt;count&gt;     the player has items (Raft's unique item name, or story:&lt;id&gt; for a story item of the crew)
	///   take|&lt;item&gt;|&lt;count&gt;    the same, and they are used up when every check passes
	///   state|&lt;name&gt;|open        objects with that name are open / closed / shown / hidden
	///   signal|&lt;name&gt;            the signal was sent on this island
	///   quest|&lt;steps&gt;            the island's quest has that many steps done ("done" = all of them)
	/// </summary>
	public class ObjCheck
	{
		public string Kind = "take", Target = "", Arg = "";
		/// <summary>The check passes when what it looks for is NOT so ("!has|story:key|1": the player hasn't got the key).</summary>
		public bool Not;
		/// <summary>A line "any" among the checks: one passing check is enough (otherwise all must pass).</summary>
		public const string AnyLine = "any";

		public static ObjCheck Parse(string line)
		{
			string[] p = (line ?? "").Split('|');
			string kind = p[0].Trim();
			bool not = kind.StartsWith("!");
			kind = kind.TrimStart('!').Trim();
			if (p.Length < 2 || !BehaviourProps.CheckKinds.Contains(kind)) return null;
			return new ObjCheck { Kind = kind, Not = not, Target = p[1].Trim(), Arg = p.Length > 2 ? p[2].Trim() : "" };
		}

		public static List<ObjCheck> ParseLines(string text) { return (text ?? "").Split('\n').Select(Parse).Where(c => c != null).ToList(); }

		/// <summary>Whether the checks are "any of" (one passing is enough).</summary>
		public static bool IsAny(string text) { return (text ?? "").Split('\n').Any(l => l.Trim().Equals(AnyLine, StringComparison.OrdinalIgnoreCase)); }

		public static string ToLines(IEnumerable<ObjCheck> checks, bool any = false)
		{
			return (any ? AnyLine + "\n" : "") + string.Join("\n", checks.Select(c => (c.Not ? "!" : "") + c.Kind + "|" + (c.Target ?? "").Replace("|", "/").Trim() + "|" + (c.Arg ?? "").Replace("|", "/").Trim()).ToArray());
		}

		/// <summary>How many items it wants (at least 1).</summary>
		public int Count
		{
			get
			{
				int n;
				return int.TryParse(Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0 ? n : 1;
			}
		}

		public bool IsItem { get { return Kind == "has" || Kind == "take"; } }

		public string Describe() { return (Not ? "NOT: " : "") + What(); }

		string What()
		{
			switch (Kind)
			{
				case "has": return "the player has " + ItemText();
				case "take": return "the player has " + ItemText() + (Not ? "" : " (used up)");
				case "state": return "'" + Target + "' is " + (BehaviourProps.States.Contains(Arg) ? Arg : "open");
				case "signal": return "the signal '" + Target + "' was sent";
				case "quest": return Target == "done" || Target.Length == 0 ? "the island's quest is done" : "quest step " + Target + " is done";
				case "wears": return "the player wears " + (Target.Length > 0 ? ContentCatalog.ItemLabel(Target.TrimEnd('*')) + (Target.EndsWith("*") ? " (any kind)" : "") : "(no item)");
				case "holds": return "the player holds " + (Target.Length > 0 ? ContentCatalog.ItemLabel(Target.TrimEnd('*')) + (Target.EndsWith("*") ? " (any kind)" : "") : "(no item)") + " in their hand";
			}
			return Kind;
		}

		string ItemText() { return (Count > 1 ? Count + " \u00D7 " : "") + (Target.Length > 0 ? ContentCatalog.ItemLabel(Target) : "(no item)"); }
	}

	/// <summary>An island object with settings, in a world: its place in the island file (the same on every machine) and settings.</summary>
	public class IslandObjectRef : MonoBehaviour
	{
		public int Index;
		public string ObjectName = "";
		public Dictionary<string, string> Props = new Dictionary<string, string>();
		public string Name { get { return ObjectProps.Get(Props, BehaviourProps.Name); } }
	}

	/// <summary>
	/// An object's movement in a world: spin, bob, and a mover (door, gate, lift) between its placed pose and the pose
	/// moved by an offset and turned. A switch mover goes where its shared state says; a loop mover goes back and
	/// forth by itself. Every machine animates its own copy, on the clock the players share (<see cref="SharedClock"/>),
	/// so loops, spins and bobbing are in step for everyone.
	/// </summary>
	public class IslandBehaviour : MonoBehaviour
	{
		public float SpinSpeed, BobHeight, BobTime = 3f, MoveTime = 2f;
		public bool Loop;
		/// <summary>Spins around its own up axis (a tilted water wheel turns like one), not the vertical.</summary>
		public bool SpinOwn;
		/// <summary>Carries this machine's player when they stand on it (a lift): each machine carries its own player.</summary>
		public bool Carry;
		public Vector3 Offset;
		public float TurnDegrees;
		/// <summary>0 = closed (placed pose), 1 = open.</summary>
		public float Target, Current;
		Vector3 startPos;
		Quaternion startRot;
		float phase;

		public void Configure(IDictionary<string, string> p, int index)
		{
			SpinSpeed = ObjectProps.GetFloat(p, BehaviourProps.Spin, 0f);
			SpinOwn = ObjectProps.GetBool(p, BehaviourProps.SpinOwn, false);
			BobHeight = ObjectProps.GetFloat(p, BehaviourProps.Bob, 0f);
			BobTime = Mathf.Max(0.2f, ObjectProps.GetFloat(p, BehaviourProps.BobTime, 3f));
			Offset = BehaviourProps.Offset(p);
			TurnDegrees = ObjectProps.GetFloat(p, BehaviourProps.Turn, 0f);
			MoveTime = Mathf.Max(0.1f, ObjectProps.GetFloat(p, BehaviourProps.MoveTime, 2f));
			Loop = ObjectProps.Get(p, BehaviourProps.MoveMode) == "loop";
			Carry = ObjectProps.GetBool(p, BehaviourProps.Carry, false);
			startPos = transform.localPosition;
			startRot = transform.localRotation;
			// Objects start at different points of their cycles, the same on every machine (from their place in the island file)
			phase = Mathf.Repeat(index * 0.618034f, 1f) * 10f;
		}

		/// <summary>Jumps to the open or closed pose (an island loading with its saved state).</summary>
		public void Snap(bool open) { Target = Current = open ? 1f : 0f; Pose(); }

		bool Animates { get { return SpinSpeed != 0f || BobHeight != 0f || Offset != Vector3.zero || TurnDegrees != 0f; } }

		void Update()
		{
			if (!Animates) return;
			// (a door, gate or lift at rest: already posed - it was set again every frame, moving its colliders in the physics
			// scene each time, for every mover on every loaded island. Audit 2026-10-06)
			if (!Loop && SpinSpeed == 0f && BobHeight == 0f && Current == Target) return;
			float t = SharedClock.Now + phase;
			Vector3 before = transform.position;
			if (Loop) Current = Mathf.PingPong(t / MoveTime, 1f);
			else if (Current != Target) Current = Mathf.MoveTowards(Current, Target, Time.deltaTime / MoveTime);
			Pose();
			if (Carry) CarryPlayer(transform.position - before);
		}

		/// <summary>The local player stands on this mover (a ray down from just above their feet meets it).</summary>
		public bool Carries(Transform player)
		{
			if (player == null) return false;
			foreach (RaycastHit hit in Physics.RaycastAll(player.position + Vector3.up * 1.2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
				if (hit.collider != null && hit.collider.transform.IsChildOf(transform)) return true;
			return false;
		}

		void CarryPlayer(Vector3 delta)
		{
			if (delta.sqrMagnitude < 1e-10f) return;
			Network_Player p = null;
			try { p = RAPI.GetLocalPlayer(); } catch { }
			if (p == null || !Carries(p.transform)) return;
			p.transform.position += delta;
			// (the player's character controller reads its place from the physics scene: told now, it doesn't step back)
			Physics.SyncTransforms();
		}

		void Pose()
		{
			float now = SharedClock.Now + phase;
			float s = Mathf.SmoothStep(0f, 1f, Current);
			Transform parent = transform.parent;
			// The offset is in world directions (as the builder sees them); the island root isn't turned, so local = world
			Vector3 pos = startPos + Offset * s;
			if (BobHeight != 0f) pos += Vector3.up * BobHeight * Mathf.Sin(now * Mathf.PI * 2f / BobTime);
			float spin = Mathf.Repeat(SpinSpeed * now, 360f);
			Quaternion rot = SpinOwn ? Quaternion.Euler(0f, TurnDegrees * s, 0f) * startRot * Quaternion.Euler(0f, spin, 0f)
				: Quaternion.Euler(0f, TurnDegrees * s + spin, 0f) * startRot;
			transform.localPosition = pos;
			transform.localRotation = rot;
		}
	}

	/// <summary>
	/// A clock that runs the same on every machine of a game: Raft's water time, which the host sends to everyone
	/// (Network_Water, in its regular world update), followed smoothly from this machine's own time so it never
	/// jumps from frame to frame. Without water (the editor, the main menu) it is the local time.
	/// </summary>
	public static class SharedClock
	{
		static float offset;
		static bool synced;
		static int frame = -1;

		/// <summary>Seconds on the shared clock.</summary>
		public static float Now { get { Follow(); return Time.time + offset; } }

		/// <summary>Whether the clock follows the host's (false: only this machine's time).</summary>
		public static bool Synced { get { Follow(); return synced; } }

		static void Follow()
		{
			if (frame == Time.frameCount) return;
			frame = Time.frameCount;
			Network_Water water = ComponentManager<Network_Water>.Value;
			if (water == null) { synced = false; offset = 0f; return; }
			float target;
			try { target = water.WaterTime - Time.time; }
			catch { return; }
			// A new game or a big correction: jump there; small differences (network delay) are eased out
			if (!synced || Mathf.Abs(target - offset) > 2f) offset = target;
			else offset = Mathf.Lerp(offset, target, Mathf.Min(1f, Time.deltaTime * 0.5f));
			synced = true;
		}
	}

	/// <summary>"Players can use it": Raft's interact hint on the object, and the use event when the key is pressed.</summary>
	public class UseInteract : MonoBehaviour, IRaycastable
	{
		public string Hint = "Use";
		public IslandObjectRef Ref;
		float cooldown;

		void IRaycastable.OnIsRayed()
		{
			if (NoteReader.IsOpen) return;
			DisplayTextManager hints = CustomNote.Hints;
			if (hints != null) hints.ShowText(Hint, CustomNote.InteractKey, 0, 0, true);
			if (CustomNote.InteractPressed() && Time.time > cooldown)
			{
				cooldown = Time.time + 0.5f;
				if (hints != null) hints.HideDisplayTexts();
				// (a keypad code lock: the keypad first - CodeLock)
				if (Ref != null && CodeLock.HasCode(Ref.Props)) CodeLock.Use(ContentState.EntryOf(transform), Ref.Index, ObjectProps.Get(Ref.Props, CodeLock.Code), transform);
				else Use();
			}
		}

		/// <summary>The use event. One of Raft's quest items is one player's: claimed from the host as a chest's loot is, and
		/// marked picked up (two players more than a second apart both got it - the host's same-moment check was all).</summary>
		void Use()
		{
			IslandWorldState.Entry e = ContentState.EntryOf(transform);
			if (Ref != null && e != null && QuestItemPickups.IsModel(Ref.ObjectName) && Behaviours.WouldAllow(e, Ref.Index, "use"))
			{
				int key = QuestItemPickups.KeyBase + Ref.Index;
				if (ContentState.IsUsed(e, key)) { Beaten(); return; }
				if (!Claims.May(e, key, yes => { if (this == null) return; if (yes) Use(); else Beaten(); })) { if (Raft_Network.IsHost) Beaten(); return; }
				ContentState.MarkUsed(transform, key);
			}
			Behaviours.Fire(e, Ref != null ? Ref.Index : -1, "use", true);
		}

		static void Beaten() { IslandInfo.ShowMessage("Someone else picked it up first"); }

		void IRaycastable.OnRayEnter() { }

		void IRaycastable.OnRayExit()
		{
			DisplayTextManager hints = CustomNote.Hints;
			if (hints != null) hints.HideDisplayTexts();
		}
	}

	/// <summary>
	/// Events and actions in a world, and the shared state they change (which objects are there, which movers are
	/// open), kept in the island's state (saved with the world, sent to players who join) under 0x60000 + object.
	/// Multiplayer: the machine where something happens does the personal actions (a message, items, a sound, a
	/// teleport) for its own player; the host does the shared ones and tells everyone (IslandNetMessage.ObjectSet).
	/// A client asks the host with IslandNetMessage.EventFired. Events the host notices itself (defeated animals) are
	/// passed on so every player near the island gets the personal part.
	/// </summary>
	public static class Behaviours
	{
		public const int StateBase = 0x60000, DoneBase = 0x70000, IslandIndex = 0xFFFF;
		/// <summary>
		/// Signals' keys: 0x1000000 + 20 bits of the name's hash, above every other key (AU41). They were 0x7F000 + 12 bits:
		/// 4096 slots inside the "done" keys (DoneBase + object), and one of them was the island's own "arrive" (DoneBase +
		/// IslandIndex) - a signal with that hash counted as arrived, and arriving as that signal sent.
		/// </summary>
		public const int SignalBase = 0x1000000;
		/// <summary>Where signals were kept before (read still, so worlds saved before keep the signals they had).</summary>
		const int OldSignalBase = 0x7F000;
		/// <summary>Host only: the shared part of an event that happens once (a note read, arriving) has run - once, however many players set it off together.</summary>
		public const int SharedOnceBase = 0x80000;
		const float NearDistance = 120f;

		/// <summary>Raised on every machine when an event's actions run here (tests listen): island id, object index (-1 = the island), event.</summary>
		public static event Action<int, int, string> Fired;
		/// <summary>The last message an action showed here (tests).</summary>
		public static string LastMessage { get; private set; }

		internal static int Today { get { try { return WorldManager.DayCounter; } catch { return 0; } } }

		/// <summary>The key a signal is kept under in the island's state.</summary>
		public static int SignalKey(string name) { return SignalBase + (SignalHash(name) & 0xFFFFF); }

		static int SignalHash(string name)
		{
			int h = 17;
			foreach (char c in (name ?? "").Trim().ToLowerInvariant()) h = h * 31 + c;
			return h;
		}

		/// <summary>Host: sends a signal from code (a boss phase, a rescued survivor), as the "signal" action does.</summary>
		public static void SendSignal(IslandWorldState.Entry e, string name)
		{
			if (e == null || string.IsNullOrEmpty(name) || !Raft_Network.IsHost) return;
			int key = SignalKey(name);
			// (Active false, as clients get it through the "used" message: only its presence counts)
			e.State[key] = new ObjectState { Active = false, Day = Today };
			IslandNetwork.SendUsed(e.Id, key, Today);
			Debug.Log("[CUSTOM ISLANDS] Signal '" + name + "' on '" + e.HostName + "'");
		}

		/// <summary>Whether the island has had this signal: under its key, or under the key it had before (not the one that was "arrive").</summary>
		public static bool HasSignal(IslandWorldState.Entry e, string name)
		{
			if (e == null) return false;
			if (e.State.ContainsKey(SignalKey(name))) return true;
			int old = OldSignalBase + (SignalHash(name) & 0xFFF);
			return old != DoneBase + IslandIndex && e.State.ContainsKey(old);
		}

		#region Spawning (IslandSpawner)

		/// <summary>A spawned object with settings: remembers its place and settings, and gets its behaviours (world only).</summary>
		public static void Attach(GameObject go, string objectName, IDictionary<string, string> props, int index)
		{
			if (go == null || props == null || props.Count == 0) return;
			IslandObjectRef r = go.AddComponent<IslandObjectRef>();
			r.Index = index;
			r.ObjectName = objectName;
			r.Props = new Dictionary<string, string>(props);
			if (!BehaviourProps.Any(props)) return;
			if (ObjectProps.GetFloat(props, BehaviourProps.Spin, 0f) != 0f || ObjectProps.GetFloat(props, BehaviourProps.Bob, 0f) != 0f || BehaviourProps.Moves(props))
				go.AddComponent<IslandBehaviour>().Configure(props, index);
			if (ObjectProps.Get(props, LaserBeam.Prop) == LaserBeam.Beam) go.AddComponent<LaserBeam>();
			if (ObjectProps.Get(props, BehaviourProps.Use).Length > 0 && !ObjectProps.IsNote(objectName, props) && !ObjectProps.IsLoot(objectName, props) && !ContentCatalog.IsZone(objectName) && !ContentCatalog.IsCreature(objectName))
			{
				UseInteract u = CustomNote.InteractHolder(go).AddComponent<UseInteract>();
				u.Hint = ObjectProps.Get(props, BehaviourProps.Use);
				u.Ref = r;
			}
			ApplyCollision(go, ObjectProps.Get(props, BehaviourProps.Collision));
			if (BehaviourProps.StartsHidden(props)) go.SetActive(false);
		}

		/// <summary>Collision as the builder chose it (the interaction box of notes, chests and usable objects is left alone).</summary>
		public static void ApplyCollision(GameObject go, string mode)
		{
			if (string.IsNullOrEmpty(mode)) return;
			List<Collider> own = go.GetComponentsInChildren<Collider>(true).Where(c => !UnderHolder(c.transform, go.transform)).ToList();
			if (mode == "none") { foreach (Collider c in own) c.enabled = false; return; }
			if (mode == "solid" && own.Any(c => c.enabled && !c.isTrigger)) return;
			if (mode == "box") foreach (Collider c in own) c.enabled = false;
			Bounds b;
			if (!CustomNote.LocalBounds(go, out b)) return;
			var solid = new GameObject("CI_Solid");
			solid.transform.SetParent(go.transform, false);
			solid.layer = IslandSpawner.TerrainLayer;
			var box = solid.AddComponent<BoxCollider>();
			box.center = b.center;
			box.size = b.size;
		}

		static bool UnderHolder(Transform t, Transform top)
		{
			for (; t != null && t != top; t = t.parent) if (t.name == CustomNote.HolderName) return true;
			return false;
		}

		/// <summary>An island loaded (every machine): objects take their saved state before anything else looks at them.</summary>
		public static void OnIslandReady(IslandWorldState.Entry e)
		{
			if (e == null || e.Root == null) return;
			foreach (IslandObjectRef r in e.Root.GetComponentsInChildren<IslandObjectRef>(true))
			{
				bool visible, open;
				StateOf(e, r, out visible, out open);
				if (r.gameObject.activeSelf != visible) r.gameObject.SetActive(visible);
				IslandBehaviour b = r.GetComponent<IslandBehaviour>();
				if (b != null && !b.Loop) b.Snap(open);
			}
			arrived.Remove(e.Id);
			try { ResumePending(e); } catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Actions after a wait on '" + e.HostName + "': " + ex.Message); }
		}

		#endregion

		#region State

		static void StateOf(IslandWorldState.Entry e, IslandObjectRef r, out bool visible, out bool open)
		{
			ObjectState s;
			if (e.State.TryGetValue(StateBase + r.Index, out s)) { visible = s.Active; open = s.Yield == 1; return; }
			visible = !BehaviourProps.StartsHidden(r.Props);
			open = false;
		}

		static void Apply(IslandWorldState.Entry e, int index, bool visible, bool open)
		{
			IslandObjectRef def = RefsOf(e).FirstOrDefault(r => r.Index == index);
			bool defaultVisible = def == null || !BehaviourProps.StartsHidden(def.Props);
			// (a copy not loaded here can't say how the object was placed: its state is kept as it is, not dropped as "as placed"
			// - a bridge shown while a player's copy was away came back hidden when it loaded - AU63)
			if (def != null && visible == defaultVisible && !open) e.State.Remove(StateBase + index);
			else e.State[StateBase + index] = new ObjectState { Active = visible, Yield = open ? 1 : 0, Day = Today };
			if (def == null) return;
			if (def.gameObject.activeSelf != visible) def.gameObject.SetActive(visible);
			IslandBehaviour b = def.GetComponent<IslandBehaviour>();
			if (b != null) b.Target = open ? 1f : 0f;
		}

		/// <summary>From the network (the host changed an object).</summary>
		public static void ApplyRemote(int islandId, int index, int bits)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e != null) Apply(e, index, (bits & 1) != 0, (bits & 2) != 0);
		}

		internal static IEnumerable<IslandObjectRef> RefsOf(IslandWorldState.Entry e)
		{
			return e != null && e.Root != null ? e.Root.GetComponentsInChildren<IslandObjectRef>(true) : Enumerable.Empty<IslandObjectRef>();
		}

		#endregion

		#region Events

		static Dictionary<string, string> PropsOf(IslandWorldState.Entry e, int index)
		{
			if (index < 0 || index == IslandIndex) return IslandCache.PropsOf(e);
			IslandObjectRef r = RefsOf(e).FirstOrDefault(x => x.Index == index);
			return r != null ? r.Props : new Dictionary<string, string>();
		}

		public static List<ObjAction> ActionsOf(IslandWorldState.Entry e, int index, string ev)
		{
			Dictionary<string, string> p = PropsOf(e, index);
			List<ObjAction> list = ObjAction.ParseLines(ObjectProps.Get(p, BehaviourProps.EventKey(ev)));
			// A door or lever that is used without actions of its own opens and closes itself
			if (list.Count == 0 && ev == "use" && BehaviourProps.Switches(p)) list.Add(new ObjAction { Verb = "switch" });
			return list;
		}

		public static List<ObjCheck> ChecksOf(IslandWorldState.Entry e, int index, string ev)
		{
			return ObjCheck.ParseLines(ObjectProps.Get(PropsOf(e, index), BehaviourProps.CheckKey(ev)));
		}

		/// <summary>The last check that failed here, described (tests).</summary>
		public static string LastFailedCheck { get; private set; }

		/// <summary>Whether the event's checks are "any of" (one passing is enough).</summary>
		public static bool AnyOf(IslandWorldState.Entry e, int index, string ev)
		{
			return ObjCheck.IsAny(ObjectProps.Get(PropsOf(e, index), BehaviourProps.CheckKey(ev)));
		}

		/// <summary>
		/// Whether the checks pass for this machine's player: all of them, or with any, at least one. Only then "take"
		/// checks use their items up (Raft's items from this player's inventory, story items from the crew's): all of
		/// them, or with any, only the one that passed first.
		/// </summary>
		public static bool Passes(IslandWorldState.Entry e, int index, List<ObjCheck> checks, bool any = false, bool take = true)
		{
			PlayerInventory inv = RAPI.GetLocalPlayer() != null ? RAPI.GetLocalPlayer().Inventory : null;
			var passed = new List<ObjCheck>();
			foreach (ObjCheck c in checks)
			{
				bool ok;
				try { ok = Holds(e, index, c, inv) != c.Not; }
				catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Check '" + c.Kind + "': " + ex.Message); ok = false; }
				if (ok) { passed.Add(c); if (any) break; }
				else if (!any) { LastFailedCheck = c.Describe(); return false; }
			}
			if (passed.Count == 0) { LastFailedCheck = "none of: " + string.Join(" / ", checks.Select(c => c.Describe()).ToArray()); return false; }
			if (!any)
			{
				// Several lines about one item add up: "take 3 planks" and "take 2 planks" need 5 (each line alone passed
				// with 3 and then 5 were taken); a "has" line still needs at least its own number
				foreach (IGrouping<string, ObjCheck> g in passed.Where(x => x.IsItem && !x.Not && x.Target.Length > 0).GroupBy(x => x.Target, StringComparer.OrdinalIgnoreCase))
				{
					if (g.Count() < 2) continue;
					int need = Math.Max(g.Where(x => x.Kind == "take").Sum(x => x.Count), g.Max(x => x.Count));
					int have = StoryItems.IsStory(g.Key) ? StoryBook.Count(StoryItems.IdOf(g.Key)) : inv != null ? inv.GetItemCount(g.Key) : 0;
					if (have < need) { LastFailedCheck = "the player has " + need + " × " + (StoryItems.IsStory(g.Key) ? StoryItems.Label(g.Key) : ContentCatalog.ItemLabel(g.Key)); return false; }
				}
			}
			if (!take) return true;
			foreach (ObjCheck c in passed.Where(x => x.Kind == "take" && !x.Not))
			{
				if (StoryItems.IsStory(c.Target)) StoryBook.Take(StoryItems.IdOf(c.Target), c.Count);
				else if (inv != null) inv.RemoveItem(c.Target, c.Count);
			}
			return true;
		}

		/// <summary>Whether the event's checks would pass now - nothing taken, nothing said (a chest looks before it claims
		/// its loot: a player without the key held a locked chest from the others for a few seconds).</summary>
		public static bool WouldAllow(IslandWorldState.Entry e, int index, string ev)
		{
			if (e == null) return true;
			if (index < 0) index = IslandIndex;
			List<ObjCheck> checks = ChecksOf(e, index, ev);
			string failedBefore = LastFailedCheck;
			bool ok = checks.Count == 0 || Passes(e, index, checks, AnyOf(e, index, ev), false);
			LastFailedCheck = failedBefore;
			return ok;
		}

		/// <summary>
		/// Checks an event before it happens (a chest checks before it gives its loot): true when there are no checks or
		/// they pass; otherwise the "otherwise" actions run and it is false. The event is then fired with skipChecks.
		/// </summary>
		public static bool Allows(IslandWorldState.Entry e, int index, string ev)
		{
			if (e == null) return true;
			if (index < 0) index = IslandIndex;
			List<ObjCheck> checks = ChecksOf(e, index, ev);
			if (checks.Count == 0 || Passes(e, index, checks, AnyOf(e, index, ev))) return true;
			Otherwise(e, index, ev, true);
			return false;
		}

		/// <summary>One check now, for this machine's player (a hazard zone's protection lines): nothing is taken.</summary>
		public static bool CheckNow(IslandWorldState.Entry e, int index, ObjCheck c)
		{
			try
			{
				Network_Player p = RAPI.GetLocalPlayer();
				return Holds(e, index, c, p != null ? p.Inventory : null) != c.Not;
			}
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] A check: " + ex.Message); return false; }
		}

		static bool Holds(IslandWorldState.Entry e, int index, ObjCheck c, PlayerInventory inv)
		{
			switch (c.Kind)
			{
				case "has":
				case "take":
					if (StoryItems.IsStory(c.Target)) return StoryBook.Count(StoryItems.IdOf(c.Target)) >= c.Count;
					return inv != null && c.Target.Length > 0 && inv.GetItemCount(c.Target) >= c.Count;
				case "state":
					List<IslandObjectRef> refs = Targets(e, index, c.Target);
					if (refs.Count == 0) return false;
					foreach (IslandObjectRef r in refs)
					{
						bool visible, open;
						StateOf(e, r, out visible, out open);
						bool holds = c.Arg == "closed" ? !open : c.Arg == "shown" ? visible : c.Arg == "hidden" ? !visible : open;
						if (!holds) return false;
					}
					return true;
				case "signal":
					return HasSignal(e, c.Target);
				case "wears": return GapChecks.Wears(c.Target);
				case "holds": return GapChecks.Holds(c.Target);
				case "quest":
					int steps;
					int need = int.TryParse(c.Target, NumberStyles.Integer, CultureInfo.InvariantCulture, out steps) ? steps : QuestTracker.QuestOf(e).Steps.Count;
					return QuestTracker.StepOf(e) >= Mathf.Max(1, need);
			}
			return false;
		}

		/// <summary>
		/// Something happened to an island object (index; -1 = the island itself). localPlayer: this machine's player
		/// did it (gets the personal actions). The checks are made here first; when one fails, the "otherwise" actions
		/// run instead. Shared actions run on the host (a client asks it). A wait puts the actions after it off.
		/// </summary>
		public static void Fire(IslandWorldState.Entry e, int index, string ev, bool localPlayer, bool skipChecks = false)
		{
			if (e == null) return;
			if (index < 0) index = IslandIndex;
			List<ObjAction> actions = ActionsOf(e, index, ev);
			List<ObjCheck> checks = ChecksOf(e, index, ev);
			if (actions.Count == 0 && checks.Count == 0) return;
			// Reading a note counts once per world
			bool once = ev == "read" || ev == "arrive";
			int key = DoneBase + index;
			// (read again - by anyone: its messages and sounds only - its items and teleports came again at every read, and
			// its checks were skipped: "uses up 5 scrap, gives titanium" was free from the second read)
			if (once && e.State.ContainsKey(key)) { if (localPlayer) Schedule(e, index, actions, false, true, ev); return; }
			// (with the player's own items, a move or a price: the host's word first, as for a chest - two players arriving or
			// reading a note within a message's time each got its items, before either heard the other had)
			if (once && localPlayer && (checks.Any(c => c.Kind == "take") || actions.Any(a => a.Verb == "give" || a.Verb == "teleport")) &&
				!Claims.May(e, key, yes => { if (yes) Fire(e, index, ev, true, skipChecks); else Schedule(e, index, actions, false, true, ev); }))
				return;
			if (!skipChecks && checks.Count > 0 && !Passes(e, index, checks, AnyOf(e, index, ev)))
			{
				Otherwise(e, index, ev, localPlayer);
				return;
			}
			if (once)
			{
				e.State[key] = new ObjectState { Active = false, Day = Today };
				IslandNetwork.SendUsed(e.Id, key, Today);
			}
			Run(e, index, ev, actions, localPlayer);
			if (Fired != null) try { Fired(e.Id, index, ev); } catch { }
		}

		/// <summary>A check failed: the event's "otherwise" actions (usually a message for the player who tried).</summary>
		static void Otherwise(IslandWorldState.Entry e, int index, string ev, bool localPlayer)
		{
			string otherwise = ev.TrimEnd('!') + "!";
			Debug.Log("[CUSTOM ISLANDS] '" + ev + "' on '" + e.HostName + "': not yet (" + LastFailedCheck + ")");
			Run(e, index, otherwise, ActionsOf(e, index, otherwise), localPlayer);
			if (Fired != null) try { Fired(e.Id, index, otherwise); } catch { }
		}

		/// <summary>The personal part here (for this machine's player), the shared part on the host. A player's event whose
		/// crew checks the host makes again waits for its answer: the personal part (items, a teleport) came at once, also
		/// when the host then refused it (another player used the crew's last key at the same moment).</summary>
		static void Run(IslandWorldState.Entry e, int index, string ev, List<ObjAction> actions, bool localPlayer)
		{
			if (actions.Count == 0) return;
			if (localPlayer && !Raft_Network.IsHost && IslandNetwork.HostAnswersEvents && HostRechecks(e, index, ev))
			{
				IslandNetwork.SendEvent(e.Id, index, ev, false, true);
				return;
			}
			if (localPlayer) Schedule(e, index, actions, false, false, ev);
			if (HasSharedPart(actions))
			{
				if (Raft_Network.IsHost) { if (SharedOnce(e, index, ev)) Schedule(e, index, actions, true, false, ev); }
				else IslandNetwork.SendEvent(e.Id, index, ev, false);
			}
		}

		/// <summary>Whether the host makes the event's checks again (the crew's story items: StoryChecksHold) and may refuse it.</summary>
		static bool HostRechecks(IslandWorldState.Entry e, int index, string ev)
		{
			return !ev.EndsWith("!") && ChecksOf(e, index, ev).Any(c => (c.Kind == "has" || c.Kind == "take") && StoryItems.IsStory(c.Target));
		}

		/// <summary>Whether the host has something to do: shared actions, or story items given (the crew's, given once by the host).</summary>
		static bool HasSharedPart(List<ObjAction> actions) { return actions.Any(a => a.Shared || GivesStory(a)); }

		internal static bool GivesStory(ObjAction a) { return a.Verb == "give" && ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, a.Arg } }).Any(l => StoryItems.IsStory(l.Key)); }

		/// <summary>
		/// Host: whether the shared part of this event runs now. An event that happens once (a note read, arriving at the
		/// island) is set off on every machine whose player does it - several players arriving on the raft together each
		/// ask - and its shared part must run once: a "toggle" asked for by an even number of players would end where it began.
		/// </summary>
		internal static bool SharedOnce(IslandWorldState.Entry e, int index, string ev)
		{
			if (ev != "read" && ev != "arrive") return NotSameMoment(e, index, ev);
			int key = SharedOnceBase + index;
			if (e.State.ContainsKey(key)) return false;
			e.State[key] = new ObjectState { Active = false, Day = Today };
			return true;
		}

		/// <summary>When the same object's event last ran its shared part on the host (island/object/event).</summary>
		static readonly Dictionary<string, float> sharedAt = new Dictionary<string, float>();
		const float SameMoment = 1f;

		/// <summary>
		/// Host: several players using one thing within the network's delay - four pulls of one lever in the same second -
		/// run its shared part once: each machine let its own player through, and a switch toggled four times ended where
		/// it began. Within a second, the first one counts.
		/// </summary>
		static bool NotSameMoment(IslandWorldState.Entry e, int index, string ev)
		{
			string k = e.Id + "/" + index + "/" + ev;
			float t, now = Time.unscaledTime;
			if (sharedAt.TryGetValue(k, out t) && now - t < SameMoment)
			{
				Debug.Log("[CUSTOM ISLANDS] '" + ev + "' on '" + e.HostName + "' again within a second (another player at the same moment): its shared part ran already");
				return false;
			}
			if (sharedAt.Count > 512) foreach (string old in sharedAt.Where(x => now - x.Value >= SameMoment).Select(x => x.Key).ToList()) sharedAt.Remove(old);
			sharedAt[k] = now;
			return true;
		}

		/// <summary>Raft's characters (the ones a player can be), in order.</summary>
		public static List<SO_Character> Characters()
		{
			if (CharacterManager.SO_Characters != null && CharacterManager.SO_Characters.Count > 0) return CharacterManager.SO_Characters;
			return Resources.LoadAll<SO_Character>("SO_Character").Where(c => c != null).OrderBy(c => c.index).ToList();
		}

		/// <summary>One of Raft's characters by its number or name (the action "character"), or null.</summary>
		public static SO_Character CharacterOf(string arg)
		{
			arg = (arg ?? "").Trim();
			if (arg.Length == 0) return null;
			int i;
			if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return Characters().FirstOrDefault(c => c.index == i);
			return Characters().FirstOrDefault(c => string.Equals(c.displayName, arg, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>The host noticed something no single player did (animals defeated): everyone near the island gets the personal part.</summary>
		static readonly HashSet<string> toldPersonal = new HashSet<string>();

		public static void FireFromHost(IslandWorldState.Entry e, int index, string ev)
		{
			if (e == null || !Raft_Network.IsHost) return;
			List<ObjAction> actions = ActionsOf(e, index, ev);
			List<ObjCheck> checks = ChecksOf(e, index, ev);
			if (actions.Count == 0 && checks.Count == 0) return;
			// (no player does these - the quest done, a spot's animals defeated: a check of a player's own items looked in the
			// host's inventory and took from it. Only the crew's checks count here - story items, states, signals, the
			// quest - AU17)
			List<ObjCheck> personal = checks.Where(c => ((c.Kind == "has" || c.Kind == "take") && !StoryItems.IsStory(c.Target)) || c.Kind == "wears" || c.Kind == "holds").ToList();
			if (personal.Count > 0)
			{
				checks = checks.Except(personal).ToList();
				if (toldPersonal.Add(e.HostName + "/" + index + "/" + ev))
					Debug.LogWarning("[CUSTOM ISLANDS] '" + e.HostName + "': the " + ev + " event's check of a player's items (" + string.Join(", ", personal.Select(c => c.Target).ToArray()) + ") is left out - no player does it; use a story item (the crew's) instead");
			}
			if (checks.Count > 0 && !Passes(e, index, checks, AnyOf(e, index, ev))) ev += "!";
			actions = ActionsOf(e, index, ev);
			if (Near(e)) Schedule(e, index, actions, false, false, ev);
			Schedule(e, index, actions, true, false, ev);
			IslandNetwork.SendEvent(e.Id, index, ev, true);
			if (Fired != null) try { Fired(e.Id, index, ev); } catch { }
		}

		/// <summary>From the network: a client's event (host: do the shared part), or the host's (client: the personal part if near).</summary>
		public static void OnEventMessage(int islandId, int index, string ev, bool fromHost) { OnEventMessage(islandId, index, ev, fromHost, null); }

		/// <summary>OnEventMessage, with the player who sent it (the host tells them when their event is refused).</summary>
		public static void OnEventMessage(int islandId, int index, string ev, bool fromHost, Network_UserId? from) { OnEventMessage(islandId, index, ev, fromHost, from, false); }

		/// <summary>OnEventMessage; answer: a player who waits for the host's yes or no (host), or that answer (player: their
		/// own part, wherever they are now).</summary>
		public static void OnEventMessage(int islandId, int index, string ev, bool fromHost, Network_UserId? from, bool answer)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e == null) return;
			List<ObjAction> actions = ActionsOf(e, index, ev);
			// (the client made the checks already - except the crew's story items, made again here: AU18)
			if (Raft_Network.IsHost && !fromHost)
			{
				string failed;
				if (!StoryChecksHold(e, index, ev, from.HasValue ? from.Value.Id : 0UL, out failed))
				{
					Debug.Log("[CUSTOM ISLANDS] A player's '" + ev + "' on '" + e.HostName + "' is refused: " + failed + " no longer holds for the crew (another player used it at the same moment)");
					// (the player who tried gets the event's "otherwise" part, as when their own check fails)
					if (from.HasValue) IslandNetwork.SendEventTo(from.Value, e.Id, index, ev.TrimEnd('!') + "!");
					return;
				}
				if (HasSharedPart(actions) && SharedOnce(e, index, ev)) Schedule(e, index, actions, true, false, ev);
				// (yes: the player's own part runs now)
				if (answer && from.HasValue) IslandNetwork.SendEventTo(from.Value, e.Id, index, ev);
			}
			else if (fromHost && (answer || Near(e))) Schedule(e, index, actions, false, false, ev);
		}

		/// <summary>
		/// Host, a player's event: its checks of the crew's story items made again against the crew's StoryBook (the
		/// host's is the one that counts). Two players using the last story key within the network's delay both passed
		/// their own check; the host took the key for the first one only (StoryBook.TakeRefused), and a "has" check sees
		/// what is left now. Other checks (a player's own items, states, signals) stay the player's. An event's "otherwise"
		/// part ("use!") has nothing to make again.
		/// </summary>
		static bool StoryChecksHold(IslandWorldState.Entry e, int index, string ev, ulong player, out string failed)
		{
			failed = null;
			if (ev.EndsWith("!")) return true;
			List<ObjCheck> checks = ChecksOf(e, index, ev);
			List<ObjCheck> story = checks.Where(c => (c.Kind == "has" || c.Kind == "take") && StoryItems.IsStory(c.Target)).ToList();
			if (story.Count == 0) return true;
			bool any = AnyOf(e, index, ev);
			if (any && story.Count < checks.Count) return true; // (another kind of check may be the one that passed: not known here)
			var taken = new HashSet<string>(story.Where(c => c.Kind == "take" && !c.Not).Select(c => StoryItems.IdOf(c.Target)), StringComparer.OrdinalIgnoreCase);
			var refused = new HashSet<string>(taken.Where(id => StoryBook.TakeRefused(player, id)).ToList(), StringComparer.OrdinalIgnoreCase);
			bool passed = false;
			foreach (ObjCheck c in story)
			{
				string id = StoryItems.IdOf(c.Target);
				bool ok;
				if (c.Kind == "take" && !c.Not) ok = !refused.Contains(id);
				else if (!c.Not && taken.Contains(id)) ok = !refused.Contains(id); // (its own take used it up here already)
				else ok = (StoryBook.Count(id) >= c.Count) != c.Not;
				if (ok) passed = true;
				else if (!any) { failed = c.Describe(); return false; }
			}
			if (!passed) { failed = string.Join(" / ", story.Select(c => c.Describe()).ToArray()); return false; }
			return true;
		}

		/// <summary>
		/// Runs one part of the actions (shared or personal), the ones after a wait that many seconds later.
		/// The personal part after a wait is for the player who was there: not after they died (a respawned player was
		/// teleported back to the island) or went away from the island.
		/// The shared part after a wait (the host's: a door opened, a bridge shown, a signal) is kept with the island until
		/// it has run: when the island unloads during the wait, or the host saves and quits, it runs when the island loads
		/// again (ev: the event, to find its actions then; firstPart: the part these actions start at, when resumed).
		/// </summary>
		static void Schedule(IslandWorldState.Entry e, int index, List<ObjAction> actions, bool shared, bool messagesOnly, string ev = null, int firstPart = 0)
		{
			float delay = 0f;
			int deathsThen = deaths, partNo = 0, parts = actions.Count(a => a.Verb == "wait") + 1;
			bool keep = shared && ev != null && Raft_Network.IsHost && PendingEventNo(ev) >= 0;
			int token = ++pendingTokens; // (the latest scheduling of an event is the one that runs: resumed after a reload, the old timers stop)
			var part = new List<ObjAction>();
			foreach (ObjAction a in actions.Concat(new[] { new ObjAction { Verb = "wait", Arg = "0" } }))
			{
				if (a.Verb != "wait") { part.Add(a); continue; }
				if (part.Count > 0)
				{
					List<ObjAction> now = part;
					int no = firstPart + partNo;
					if (delay <= 0f) RunPart(e, index, now, shared, messagesOnly);
					else
					{
						if (keep) KeepPending(e, index, ev, no, token);
						DynamicIslands.instance.StartCoroutine(Later(delay, () =>
						{
							if (!IslandWorldState.Contains(e) || !LoadSceneManager.IsGameSceneLoaded) return;
							if (!shared && (deaths != deathsThen || wasDead || !Near(e)))
							{
								Debug.Log("[CUSTOM ISLANDS] What comes after the wait on '" + e.HostName + "' is left out for this player: " + (deaths != deathsThen || wasDead ? "they died meanwhile" : "they left the island"));
								return;
							}
							// (unloaded meanwhile: it runs when the island loads again - kept in its state)
							if (keep && (e.Root == null || !IsPending(e, index, ev, token))) return;
							RunPart(e, index, now, shared, messagesOnly);
							if (keep) PendingDone(e, index, ev, no, firstPart + parts - 1, token);
						}));
					}
				}
				part = new List<ObjAction>();
				delay += a.Seconds;
				partNo++;
			}
		}

		/// <summary>An event's shared actions still to come after a wait (host): PendingBase + object index * 16 + the
		/// event's number; Yield = the part (counted in waits) they go on from; Day = the scheduling's token (this session).</summary>
		public const int PendingBase = 0x900000;
		static int pendingTokens;
		static readonly string[] PendingEvents = BehaviourProps.ObjectEvents.Concat(BehaviourProps.IslandEvents).ToArray();

		static int PendingEventNo(string ev)
		{
			int n = Array.IndexOf(PendingEvents, (ev ?? "").TrimEnd('!'));
			return n < 0 ? -1 : n + ((ev ?? "").EndsWith("!") ? PendingEvents.Length : 0);
		}

		static int PendingKey(int index, string ev) { return PendingBase + ((index & 0xFFFF) << 4) + PendingEventNo(ev); }

		static void KeepPending(IslandWorldState.Entry e, int index, string ev, int part, int token)
		{
			int key = PendingKey(index, ev);
			ObjectState s;
			if (!e.State.TryGetValue(key, out s) || s.Day != token || s.Yield > part) e.State[key] = new ObjectState { Active = true, Yield = part, Day = token };
		}

		static bool IsPending(IslandWorldState.Entry e, int index, string ev, int token)
		{
			ObjectState s;
			return e.State.TryGetValue(PendingKey(index, ev), out s) && s.Day == token;
		}

		static void PendingDone(IslandWorldState.Entry e, int index, string ev, int part, int lastPart, int token)
		{
			int key = PendingKey(index, ev);
			ObjectState s;
			if (!e.State.TryGetValue(key, out s) || s.Day != token || s.Yield > part) return;
			if (part >= lastPart) e.State.Remove(key);
			else e.State[key] = new ObjectState { Active = true, Yield = part + 1, Day = token };
		}

		/// <summary>Host, an island loaded: what was still to come after a wait when it unloaded (or the host saved and
		/// quit) goes on now - it was lost, and the note, zone or chest counted as used: a door the story needed never opened.</summary>
		static void ResumePending(IslandWorldState.Entry e)
		{
			if (!Raft_Network.IsHost) return;
			foreach (KeyValuePair<int, ObjectState> kv in e.State.Where(k => k.Key >= PendingBase && k.Key < PendingBase + 0x100000).ToList())
			{
				int rel = kv.Key - PendingBase, index = rel >> 4, evNo = rel & 15;
				string ev = evNo < PendingEvents.Length ? PendingEvents[evNo] : evNo < PendingEvents.Length * 2 ? PendingEvents[evNo - PendingEvents.Length] + "!" : null;
				e.State.Remove(kv.Key);
				if (ev == null) continue;
				int from = kv.Value.Yield, waits = 0;
				var rest = new List<ObjAction>();
				foreach (ObjAction a in ActionsOf(e, index == 0xFFFF ? IslandIndex : index, ev))
				{
					if (waits >= from) rest.Add(a);
					if (a.Verb == "wait") waits++;
				}
				if (rest.Count == 0) continue;
				Debug.Log("[CUSTOM ISLANDS] '" + e.HostName + "': what came after a wait on '" + ev + "' goes on now (the island unloaded or the world was left meanwhile)");
				Schedule(e, index == 0xFFFF ? IslandIndex : index, rest, true, false, ev, from);
			}
		}

		static System.Collections.IEnumerator Later(float seconds, Action then)
		{
			yield return new WaitForSeconds(seconds);
			try { then(); }
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Waiting actions: " + ex.Message); }
		}

		static void RunPart(IslandWorldState.Entry e, int index, List<ObjAction> actions, bool shared, bool messagesOnly)
		{
			if (shared) RunShared(e, index, actions); else RunPersonal(e, index, actions, messagesOnly);
		}

		static void RunShared(IslandWorldState.Entry e, int index, List<ObjAction> actions)
		{
			bool creaturesShown = false;
			// Story items belong to the crew: given once, here on the host (each player near would give them again)
			foreach (ObjAction a in actions.Where(GivesStory))
				foreach (KeyValuePair<string, int> l in ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, a.Arg } }).Where(l => StoryItems.IsStory(l.Key)))
					StoryBook.Give(l.Key, l.Value, e);
			foreach (ObjAction a in actions.Where(x => x.Shared))
			{
				if (a.Verb == "hit") { GapFeatures.HitCreatures(e, Targets(e, index, a.Target), a.Arg); continue; }
				if (a.Verb == "signal")
				{
					int key = SignalKey(a.Arg);
					// (Active false, as clients get it through the "used" message: only its presence counts)
					e.State[key] = new ObjectState { Active = false, Day = Today };
					IslandNetwork.SendUsed(e.Id, key, Today); // (clients check signals too)
					Debug.Log("[CUSTOM ISLANDS] Signal '" + a.Arg + "' on '" + e.HostName + "'");
					continue;
				}
				if (a.Verb == "playsound" || a.Verb == "stopsound")
				{
					// (no target name: the object itself - or the island, for its own events)
					List<int> at = a.Target.Trim().Length == 0 || a.Target.Trim().Equals("self", StringComparison.OrdinalIgnoreCase) ? new List<int> { index }
						: Targets(e, index, a.Target).Select(r => r.Index).ToList();
					TriggerSounds.HostRun(e, a.Verb == "playsound", a.Arg, at);
					continue;
				}
				if (a.Verb == "camera") { CameraMoment.HostRun(e, index, a.Target, a.Arg); continue; }
				if (a.Verb == "journal")
				{
					StoryBook.AddPage("act:" + StoryBook.PageIsland(e) + ":" + index + ":" + a.Target, a.Target, a.Arg.Replace("\\n", "\n"), IslandTitle(e));
					continue;
				}
				foreach (IslandObjectRef r in Targets(e, index, a.Target))
				{
					bool visible, open;
					StateOf(e, r, out visible, out open);
					switch (a.Verb)
					{
						case "show": visible = true; break;
						case "hide": visible = false; break;
						case "toggle": visible = !visible; break;
						case "open": open = true; break;
						case "close": open = false; break;
						case "switch": open = !open; break;
					}
					Apply(e, r.Index, visible, open);
					IslandNetwork.SendObjectSet(e.Id, r.Index, (visible ? 1 : 0) | (open ? 2 : 0));
					if (visible && r.GetComponent<CreatureSpawnPoint>() != null) creaturesShown = true;
				}
			}
			if (creaturesShown) CreatureSpawner.OnIslandReady(e); // hidden animals appear: an ambush
		}

		/// <summary>The island's name as players see it (its title, or the file name).</summary>
		public static string IslandTitle(IslandWorldState.Entry e)
		{
			string t = ObjectProps.Get(IslandCache.PropsOf(e), IslandProps.Title);
			return t.Length > 0 ? t : e.Label.Length > 0 ? e.Label : WorldRandomizer.Readable(e.HostName);
		}

		static void RunPersonal(IslandWorldState.Entry e, int index, List<ObjAction> actions, bool messagesOnly)
		{
			foreach (ObjAction a in actions.Where(x => !x.Shared))
			{
				try
				{
					switch (a.Verb)
					{
						case "suit":
							if (messagesOnly) break;
							Hazards.PutOnSuit(a.Arg);
							break;
						case "message":
							LastMessage = a.Arg;
							IslandInfo.ShowMessage(a.Arg);
							break;
						case "give":
							if (messagesOnly) break;
							// (story items: the host gives them to the crew, once - RunShared)
							TriggerZone.Give(ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, a.Arg } }).Where(l => !StoryItems.IsStory(l.Key)));
							break;
						case "sound":
							IslandObjectRef src = RefsOf(e).FirstOrDefault(r => r.Index == index);
							SoundLibrary.PlayOnce(a.Arg, src != null ? src.transform.position : (RAPI.GetLocalPlayer() != null ? RAPI.GetLocalPlayer().transform.position : Vector3.zero), 0.9f);
							break;
						case "teleport":
							if (messagesOnly) break;
							IslandObjectRef to = Targets(e, index, a.Target).FirstOrDefault(r => r.gameObject.activeInHierarchy);
							if (to != null) Teleport(to.transform.position + Vector3.up * 1.2f);
							break;
						case "character":
							if (messagesOnly) break;
							SO_Character c = CharacterOf(a.Arg);
							if (c == null) Debug.LogWarning("[CUSTOM ISLANDS] No character '" + a.Arg + "' (Raft's: " + string.Join(", ", Characters().Select(x => x.index + " " + x.displayName).ToArray()) + ")");
							else CharacterManager.UnlockCharacterByIndex(c.index);
							break;
					}
				}
				catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Action '" + a.Verb + "': " + ex.Message); }
			}
		}

		/// <summary>The objects an action works on: those with the name, or the object itself when there's no name.</summary>
		static List<IslandObjectRef> Targets(IslandWorldState.Entry e, int index, string target)
		{
			target = (target ?? "").Trim();
			if (target.Length == 0 || target.Equals("self", StringComparison.OrdinalIgnoreCase)) return RefsOf(e).Where(r => r.Index == index).ToList();
			return RefsOf(e).Where(r => r.Name.Equals(target, StringComparison.OrdinalIgnoreCase)).ToList();
		}

		public static void Teleport(Vector3 to)
		{
			PlayerMove.To(RAPI.GetLocalPlayer(), to);
		}

		internal static bool Near(IslandWorldState.Entry e)
		{
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null) return false;
			float r = Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name));
			return new Vector2(p.transform.position.x - e.Position.x, p.transform.position.z - e.Position.z).magnitude < r + NearDistance;
		}

		#endregion

		#region Island events (arrive, quest)

		static readonly HashSet<int> arrived = new HashSet<int>();
		static float nextTick;

		/// <summary>Every frame from the mod: players arriving at islands with "on.arrive" actions (each machine its own player).</summary>
		/// <summary>This machine's player's deaths (this session): what comes after a wait is for the player who was there.</summary>
		static int deaths;
		static bool wasDead;

		static void WatchDeath()
		{
			Network_Player p = RAPI.GetLocalPlayer();
			// (Raft's own flag is the player script's: its stats entity doesn't say dead)
			bool dead = p != null && ((p.PlayerScript != null && p.PlayerScript.IsDead) ||
				(p.Stats != null && (p.Stats.IsDead || (p.Stats.stat_health != null && p.Stats.stat_health.Value <= 0f))));
			if (dead && !wasDead) deaths++;
			wasDead = dead;
		}

		public static void Tick()
		{
			try { if (LoadSceneManager.IsGameSceneLoaded) WatchDeath(); } catch { }
			if (Time.unscaledTime < nextTick) return;
			nextTick = Time.unscaledTime + 0.5f;
			if (!LoadSceneManager.IsGameSceneLoaded) { arrived.Clear(); return; }
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null) return;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root == null || arrived.Contains(e.Id)) continue;
				if (!IslandCache.PropsOf(e).ContainsKey(BehaviourProps.EventKey("arrive"))) { arrived.Add(e.Id); continue; }
				float r = Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name));
				if (new Vector2(p.transform.position.x - e.Position.x, p.transform.position.z - e.Position.z).magnitude > r + 30f) continue;
				arrived.Add(e.Id);
				Fire(e, -1, "arrive", true);
			}
		}

		/// <summary>Hooked to QuestTracker.Advanced (every machine): the island's quest is done.</summary>
		public static void OnQuestAdvanced(int islandId, int step)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Id == islandId);
			if (e == null) return;
			// (the host alone: its checks decide "quest" or "quest!" once and take once, and the others get the personal
			// part when near - each machine deciding for itself took a check's items once per player)
			if (step >= QuestTracker.QuestOf(e).Steps.Count && Raft_Network.IsHost) FireFromHost(e, -1, "quest");
		}

		#endregion
	}
}
