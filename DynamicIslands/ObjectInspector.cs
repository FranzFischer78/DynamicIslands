using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The Objects tab's inspector: when exactly one placed object is selected it shows that object's settings,
	/// so every editor sits in the same place as the objects themselves:
	///   Creature  - herd size, difficulty presets, health / damage / speed / size, respawning (the creature editor)
	///   Note      - title and a preview of the text, "Edit note..." opens the note editor; any object can be made readable
	///   Colour    - a tint for any object, creatures included (swatches, strength, custom RGB)
	/// Every change is an undo step (slider drags merge into one).
	/// </summary>
	public static partial class ObjectInspector
	{
		static RectTransform root;
		static EditorGameObject shown;
		static bool dirty, typing, showRgb;
		static InputField titleField;

		/// <summary>The inspector is visible (an object with settings is selected); the Objects tab hides its tips then.</summary>
		public static bool Visible { get { return root != null && root.gameObject.activeSelf; } }

		public static EditorGameObject Target { get { return shown; } }

		/// <summary>Days until things come back on the island being edited: its rule (Island tab) or the world's setting.</summary>
		static int EditorRegrowDays
		{
			get
			{
				int d;
				return int.TryParse(ObjectProps.Get(DynamicIslands.currentIslandProps, IslandProps.RegrowDays), out d) ? Mathf.Max(0, d) : CustomIslandSpawner.RegrowDays;
			}
		}

		public static readonly Color[] Swatches =
		{
			new Color(1f, 0.35f, 0.3f), new Color(1f, 0.62f, 0.25f), new Color(1f, 0.9f, 0.35f), new Color(0.45f, 0.9f, 0.4f),
			new Color(0.3f, 0.85f, 0.85f), new Color(0.35f, 0.55f, 1f), new Color(0.7f, 0.45f, 1f), new Color(1f, 0.5f, 0.8f),
			new Color(0.35f, 0.35f, 0.38f),
		};

		public static void Build(Transform parent)
		{
			root = UIKit.Rect("Inspector", parent);
			UIKit.Vertical(root.gameObject, 10f, new RectOffset(0, 0, 0, 0));
			root.gameObject.SetActive(false);
			shown = null;
		}

		/// <summary>Shows the settings again (after an undo, or a change made elsewhere).</summary>
		public static void Refresh() { dirty = true; }

		public static void Tick()
		{
			if (root == null) return;
			var g = DynamicIslands.EditorGizmoHandler;
			EditorGameObject sel = null;
			if (g != null)
			{
				List<Transform> roots = g.SelectedRoots.Where(t => t != null).ToList();
				if (roots.Count == 1) sel = roots[0].GetComponent<EditorGameObject>();
			}
			// (a shown object that was destroyed, e.g. by loading an island, compares equal to null: check it too)
			bool gone = !ReferenceEquals(shown, null) && shown == null;
			if (sel != shown || dirty || gone) Rebuild(sel);

			bool nowTyping = (titleField != null && titleField.isFocused) || amountFields.Any(f => f != null && f.isFocused) || placeFields.Any(f => f != null && f.isFocused);
			if (!nowTyping) ShowPlace();
			if (nowTyping) EditorInput.IsTyping = true;
			else if (typing && !NoteEditorWindow.IsOpen && !ItemPickerWindow.IsOpen) EditorInput.IsTyping = false;
			typing = nowTyping;
		}

		static void Rebuild(EditorGameObject target)
		{
			dirty = false;
			if (target == null) target = null; // a destroyed object becomes a real null
			shown = target;
			titleField = null;
			amountFields.Clear();
			placeFields.Clear();
			foreach (Transform child in root) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
			root.gameObject.SetActive(target != null);
			if (target == null) return;
			if (target.Props == null) target.Props = new Dictionary<string, string>();
			Groups(target);
			PlaceGroup(target);
		}

		static void Groups(EditorGameObject target)
		{
			ToolsHint(target);
			ContentCatalog.CreatureKind kind = ContentCatalog.CreatureOf(target.GameObjectName);
			if (ContentCatalog.IsZone(target.GameObjectName))
			{
				// (nothing to colour: zones are invisible in a world)
				if (target.GameObjectName == ContentCatalog.AtmosphereZoneName) AtmosphereGroup(target);
				else if (target.GameObjectName == ContentCatalog.SoundZoneName) SoundGroup(target);
				else { ZoneGroup(target); HazardGroup(target); LootGroup(target, true); }
				BehaviourGroup(target);
				return;
			}
			if (ContentCatalog.IsHelper(target.GameObjectName)) { BehaviourGroup(target); return; }
			if (ContentCatalog.IsTreasure(target.GameObjectName)) { TreasureGroup(target); return; }
			if (KeycardDoor.Is(target.GameObjectName)) KeycardGroup(target);
			if (kind != null) CreatureGroup(target, kind);
			else
			{
				bool note = ObjectProps.IsNote(target.GameObjectName, target.Props), loot = ObjectProps.IsLoot(target.GameObjectName, target.Props);
				// (Raft's abandoned raft crate: Raft's own loot - what it can't do, instead of making it readable or a chest)
				if (target.GameObjectName == PlaceableCatalog.RaftCrate) RaftCrateGroup();
				else
				{
					if (note) NoteGroup(target);
					if (loot) LootGroup(target, false);
					if (!note || !loot) AddFeatureGroup(target, note, loot);
				}
				if (target.GameObjectName.StartsWith("ZiplinePath", System.StringComparison.Ordinal)) ZiplineGroup(target);
				if (Survivors.IsCage(target.GameObjectName)) SurvivorGroup(target);
			}
			GapGroup(target, kind);
			BehaviourGroup(target);
			ColourGroup(target, kind != null);
		}

		/// <summary>What the object does in a world (name, movement, use, collision, events), and the button for the behaviour window.</summary>
		static void BehaviourGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, "Behaviour & events");
			Text t = UIKit.Label(g, BehaviourWindow.Summary(target.Props, target.GameObjectName), 12, UIKit.TextMuted);
			t.lineSpacing = 1.05f;
			Button b = UIKit.Button(g, "Behaviour & events...", () => BehaviourWindow.Open(target), "A name, movement (spin, bob, a door or lift), hidden at first, players can use it, collision, and what happens when...", -1, 26f, 13);
			if (BehaviourProps.Any(target.Props)) UIKit.SetActive(b, true);
			// A keypad code lock (CodeLock): players type the code before the use runs
			if (ObjectProps.Get(target.Props, BehaviourProps.Use).Length > 0)
			{
				InputField code = UIKit.Field(g, "Keypad code (none)", ObjectProps.Get(target.Props, CodeLock.Code), 26f, "Digits a player types on a keypad before using it (Tangaroa's launch keypad, Vasagatan's code): put the code on a note somewhere. Empty: no keypad");
				code.contentType = InputField.ContentType.IntegerNumber;
				code.onEndEdit.AddListener(v => Set(target, CodeLock.Code, v.Trim().Length > 0 ? v.Trim() : null, null));
				amountFields.Add(code); // (typing the code: Delete hid the object, 1-4 changed the gizmo, WASD flew)
			}
		}

		/// <summary>A key door (RF4): the story item that opens it, and whether it is used up.</summary>
		static void KeycardGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, KeycardDoor.IsCardReader(target.GameObjectName) ? "Keycard door" : "Key door");
			string card = KeycardDoor.CardOf(target.Props);
			bool usedUp = KeycardDoor.UsedUp(target.Props);
			UIKit.Label(g, card != null ? "Opens for a player who has " + StoryItems.Label(card) + (usedUp ? " (the card is used up)." : " (they keep it).") + " Open, it stays open for everyone."
				: "<i>No card opens it: it opens for anyone.</i>", 12, card != null ? UIKit.TextMuted : UIKit.Danger);
			var options = new List<DropList.Option> { new DropList.Option("", "None (opens for anyone)") };
			options.AddRange(KeycardDoor.Choices(DynamicIslands.currentIslandProps, card).Select(c => new DropList.Option(c.Key, c.Value)));
			RectTransform row = UIKit.Row(g, 26f, 4f, "CardRow");
			UIKit.Label(row, "Card", 12, UIKit.TextMuted);
			DropList.Make(row, "Drop_Card", options, card ?? "", v => { PropsCommand.Change(target, KeycardDoor.WithCard(target.Props, v, KeycardDoor.UsedUp(target.Props))); Refresh(); }, 200,
				"The story item that opens it: one of the island's own (Story items in the island's settings) or one of Raft's quest items");
			Button used = UIKit.Check(g, "Card used up", () =>
			{
				string c = KeycardDoor.CardOf(target.Props);
				if (c == null) return;
				PropsCommand.Change(target, KeycardDoor.WithCard(target.Props, c, !KeycardDoor.UsedUp(target.Props)));
				Refresh();
			}, "Ticked: the card goes when the door opens. Not ticked: the player keeps it (for other doors too)");
			UIKit.SetCheck(used, usedUp);
		}

		/// <summary>Buried treasure (RF4): what it is, and extra loot of its own in its chest.</summary>
		static void TreasureGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, "Buried treasure");
			List<KeyValuePair<string, int>> loot = ObjectProps.Loot(target.Props, ObjectProps.TreasureLoot);
			UIKit.Label(g, "Only Raft's metal detector finds it: it beeps faster nearer. Three digs with the shovel bring up a chest with Raft's treasure loot"
				+ (loot.Count > 0 ? ", and also: " + string.Join(", ", loot.Select(l => (StoryItems.IsStory(l.Key) ? StoryItems.Label(l.Key) : ContentCatalog.ItemLabel(l.Key)) + " \u00D7" + l.Value).ToArray()) + "." : ".")
				+ (loot.Any(l => StoryItems.IsStory(l.Key)) ? " It holds a story item, so it never comes back once dug up." : " Dug up, it comes back after the regrow days."), 12, UIKit.TextMuted);
			RectTransform row = UIKit.Row(g, 26f, 4f, "TreasureLoot");
			Button add = UIKit.Button(row, "Extra loot...", () => ItemPickerWindow.OpenFor(() => ObjectProps.Get(target.Props, ObjectProps.TreasureLoot),
				v => PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.TreasureLoot, string.IsNullOrEmpty(v) ? null : v))),
				"Items (or story items) the chest gives besides Raft's treasure loot, once, to whoever picks it up", -1, 26f, 12);
			UIKit.SetActive(add, loot.Count == 0);
			if (loot.Count > 0) UIKit.Button(row, "None", () => { PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.TreasureLoot, null)); Refresh(); }, "Only Raft's treasure loot", 60, 26f, 12);
		}

		/// <summary>Raft's zipline line: where its far end stands (ZiplineEnds; in a world Raft makes the line there).</summary>
		/// <summary>A feature that wants one of Raft's tools (RaftFeatures): which, so the builder gives it on this island or
		/// one met before - the plan's Check follows it.</summary>
		static void ToolsHint(EditorGameObject target)
		{
			RaftFeatures.Feature f = RaftFeatures.Of(target.GameObjectName, target.Props);
			if (f == null) return;
			RectTransform g = UIKit.Group(root, "Raft's tools");
			string tools = string.Join(f.Any ? " or the " : " and the ", f.Tools.Select(RaftFeatures.LabelOf).ToArray());
			UIKit.Label(g, (f.Advisory ? "Players will want the " : "Players need the ") + tools + " here (" + f.Label + "). Give it on this island or one met before it: the plan's Check warns when no island does.", 12, UIKit.TextMuted).name = "ToolsHint";
		}

		static void ZiplineGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, "Zipline");
			Terrain terrain = terraineditor.terrain;
			Vector3? far = ZiplineEnds.FarEnd(target.Props);
			string where = far.HasValue && terrain != null
				? "Far end " + Vector3.Distance(terrain.transform.position + far.Value, target.transform.position).ToString("F0") + " m away, " + (terrain.transform.position.y + far.Value.y - target.transform.position.y).ToString("+0;-0") + " m up"
				: "Raft's own line (as in its scene). Players ride it with the zipline tool.";
			UIKit.Label(g, where, 12, UIKit.TextMuted);
			// (the ends' places, as the Position fields count them - X Y Z, Y above the sea - so the near end can be set on a deck)
			Vector3? nearFloor = ZiplineEnds.NearFloor(target.gameObject);
			UIKit.Label(g, "Near end " + (nearFloor.HasValue ? ZiplineEnds.Text(nearFloor.Value - Corner) : "?") + (far.HasValue && terrain != null ? " · far end " + ZiplineEnds.Text(terrain.transform.position + far.Value - Corner) : ""), 12, UIKit.TextMuted).name = "ZipEnds";
			RectTransform nearRow = UIKit.Row(g, 26f, 4f, "ZipNearRow");
			UIKit.Button(nearRow, "Near end: where I look", () => { if (NearEndTo(target, ScatterTool.ViewCentre())) Refresh(); },
				"Moves the whole zipline so its near end's floor stands where the camera looks (on a deck, a roof, the ground). Look at the spot, then click. Ctrl+Z undoes it", -1, 26f, 12).name = "Button_ZipNearEnd";
			RectTransform row = UIKit.Row(g, 26f, 4f, "ZipRow");
			UIKit.Button(row, "Far end: where I look", () =>
			{
				Vector3? at = ScatterTool.ViewCentre();
				if (!at.HasValue || terrain == null) return;
				PropsCommand.Change(target, ObjectProps.With(target.Props, ZiplineEnds.ZipTo, ZiplineEnds.Text(at.Value - terrain.transform.position)));
			}, "Puts the line's far end (its lower floor) on the ground the camera looks at; in a world the line runs from this object to there. Keep it lower than this end, as a zipline runs downhill.", -1, 26f, 12);
			if (far.HasValue) UIKit.Button(row, "Raft's own", () => PropsCommand.Change(target, ObjectProps.With(target.Props, ZiplineEnds.ZipTo, null)), "Back to the line as Raft made it", 90, 26f, 12);
		}

		/// <summary>Moves the zipline object so its near end's floor stands at <paramref name="at"/> (one undo step; also tests).</summary>
		internal static bool NearEndTo(EditorGameObject target, Vector3? at)
		{
			Vector3? nearFloor = target != null ? ZiplineEnds.NearFloor(target.gameObject) : null;
			if (!at.HasValue || !nearFloor.HasValue) return false;
			var gizmo = DynamicIslands.EditorGizmoHandler;
			var cmd = new RuntimeGizmos.TransformCommand(gizmo, target.transform);
			target.transform.position += at.Value - nearFloor.Value;
			cmd.StoreNewTransformValues();
			CommandUndoRedo.UndoRedoManager.Insert(cmd);
			if (gizmo != null) gizmo.SetPivotPoint();
			nextPlaceShow = 0f;
			return true;
		}

		#region Creature editor

		static void CreatureGroup(EditorGameObject target, ContentCatalog.CreatureKind kind)
		{
			Dictionary<string, string> p = target.Props;
			RectTransform g = UIKit.Group(root, "Creature: " + kind.Label);
			Text about = UIKit.Label(g, Capital(kind.Hint) + ".", 12, UIKit.TextMuted);
			about.lineSpacing = 1.05f;

			UIKit.Slider(g, "Animals here", 1, ObjectProps.MaxCount, ObjectProps.Count(p), v => v <= 1 ? "1" : v.ToString("F0") + " (a herd)",
				v => Set(target, ObjectProps.CreatureCount, ((int)v).ToString(), "1", false), "How many of them live at this spot", true);

			RectTransform presets = UIKit.Row(g, 26f, 4f, "Presets");
			string current = ContentCatalog.Summary(p);
			foreach (var preset in ObjectProps.Presets)
			{
				float[] v = preset.Value;
				Button b = UIKit.Button(presets, preset.Key, () =>
				{
					Dictionary<string, string> np = ObjectProps.With(target.Props, ObjectProps.CreatureHealth, ObjectProps.Format(v[0]), "1");
					np = ObjectProps.With(np, ObjectProps.CreatureDamage, ObjectProps.Format(v[1]), "1");
					np = ObjectProps.With(np, ObjectProps.CreatureSpeed, ObjectProps.Format(v[2]), "1");
					PropsCommand.Change(target, np);
					Refresh();
				}, preset.Key + ": health \u00D7" + ObjectProps.Format(v[0]) + ", damage \u00D7" + ObjectProps.Format(v[1]) + ", speed \u00D7" + ObjectProps.Format(v[2]), -1, 26f, 12);
				UIKit.SetActive(b, preset.Key == current || (preset.Key == "Normal" && current.Length == 0));
			}

			Multiplier(g, target, "Health", ObjectProps.CreatureHealth, ObjectProps.MinMultiplier, ObjectProps.MaxHealth, ObjectProps.Health(p), "How much damage it takes to kill (Raft's own health times this)");
			Multiplier(g, target, "Damage", ObjectProps.CreatureDamage, 0f, ObjectProps.MaxMultiplier, ObjectProps.Damage(p), "How hard it hits players (0 = harmless)");
			Multiplier(g, target, "Speed", ObjectProps.CreatureSpeed, ObjectProps.MinMultiplier, 2.5f, ObjectProps.Speed(p), "How fast it walks, runs and swims");
			Multiplier(g, target, "Size", ObjectProps.CreatureSize, ObjectProps.MinSize, ObjectProps.MaxSize, ObjectProps.Size(p), "How big it is: x0.5 to x3 of Raft's size (also shown on the marker). Its body and hit box grow with it; its health doesn't - set Health for a tougher one");

			RectTransform rrow = UIKit.Row(g, 26f, 4f, "Respawn");
			UIKit.Label(rrow, "Comes back after", 13, UIKit.TextMuted);
			bool respawns = ObjectProps.Respawns(p);
			Button on = UIKit.Button(rrow, (EditorRegrowDays > 0 ? EditorRegrowDays + " days" : "never (island rule)"), () => { Set(target, ObjectProps.CreatureRespawn, null); Refresh(); },
				"Killed or caught animals come back after the island's regrow days (its Island tab rule, else the world's setting), like trees", 110, 26f, 12);
			Button off = UIKit.Button(rrow, "Never", () => { Set(target, ObjectProps.CreatureRespawn, "0"); Refresh(); }, "Once killed or caught, gone for good in that world", 64, 26f, 12);
			UIKit.SetActive(on, respawns);
			UIKit.SetActive(off, !respawns);

			// Ambush: wait for a trigger zone (the button steps through "at once" and the island's zones)
			string waits = ObjectProps.Get(p, ObjectProps.CreatureZone);
			List<string> choices = new[] { "" }.Concat(ContentCatalog.ZoneIdsInEditor()).ToList();
			if (waits.Length > 0 && !choices.Contains(waits)) choices.Add(waits);
			RectTransform wrow = UIKit.Row(g, 26f, 4f, "Wake");
			UIKit.Size(UIKit.Label(wrow, "Appears", 13, UIKit.TextMuted).gameObject, 64);
			Button wake = UIKit.Button(wrow, waits.Length == 0 ? "at once" : "when '" + waits + "' fires", () =>
			{
				string next = choices[(choices.IndexOf(waits) + 1) % choices.Count];
				Set(target, ObjectProps.CreatureZone, next.Length > 0 ? next : null);
				Refresh();
			}, choices.Count > 1 ? "Click to choose: at once, or when a player walks into one of the island's trigger zones (an ambush)" : "Place a trigger zone (Zones & triggers) to make this an ambush", -1, 26f, 12);
			UIKit.SetActive(wake, waits.Length > 0);
			BossPhaseRows(g, target);
		}

		/// <summary>A boss phase (RF5): at a share of its health the creature calls more, gets faster and sends a signal.</summary>
		static void BossPhaseRows(RectTransform g, EditorGameObject target)
		{
			Dictionary<string, string> p = target.Props;
			bool has = BossPhases.Has(p);
			RectTransform row = UIKit.Row(g, 26f, 4f, "BossPhase");
			UIKit.Size(UIKit.Label(row, "Boss phase", 13, UIKit.TextMuted).gameObject, 80);
			Button b = UIKit.Button(row, has ? BossPhases.Describe(p) : "none", () =>
			{
				Dictionary<string, string> np = ObjectProps.With(target.Props, BossPhases.Share, has ? null : "0.5");
				if (!has) { np = ObjectProps.With(np, BossPhases.Summon, "2"); np = ObjectProps.With(np, BossPhases.Speed, "1.3"); }
				else { np = ObjectProps.With(np, BossPhases.Summon, null); np = ObjectProps.With(np, BossPhases.Speed, null); np = ObjectProps.With(np, BossPhases.SummonKind, null); np = ObjectProps.With(np, BossPhases.Signal, null); }
				PropsCommand.Change(target, np);
				Refresh();
			}, "Click to turn on or off: once per fight, when one of its animals falls to a share of its health, it calls more creatures, gets faster, sends a signal (for world plans) and runs its 'phase' event", -1, 26f, 12);
			UIKit.SetActive(b, has);
			if (!has) return;
			UIKit.Slider(g, "Starts at health", 10, 90, Mathf.Round(BossPhases.ShareOf(p) * 100f), v => v.ToString("F0") + " %",
				v => Set(target, BossPhases.Share, ObjectProps.Format(Mathf.Round(v) / 100f), null, false), "The share of its health it falls to before the phase starts", true);
			UIKit.Slider(g, "Calls", 0, BossPhases.MaxSummon, BossPhases.SummonCount(p), v => v < 1 ? "none" : v.ToString("F0") + " more",
				v => Set(target, BossPhases.Summon, ((int)v).ToString(), "0", false), "How many creatures it calls (they come around it, with Raft's own stats)", true);
			// The kind it calls: steps through its own kind and the hostile ones
			string kindName = ObjectProps.Get(p, BossPhases.SummonKind);
			List<string> kinds = new[] { "" }.Concat(ContentCatalog.Creatures.Where(k => k.Category == ContentCatalog.HostileCategory).Select(k => k.Name)).Distinct().ToList();
			if (kindName.Length > 0 && !kinds.Contains(kindName)) kinds.Add(kindName);
			RectTransform krow = UIKit.Row(g, 26f, 4f, "BossKind");
			UIKit.Size(UIKit.Label(krow, "Of kind", 13, UIKit.TextMuted).gameObject, 80);
			ContentCatalog.CreatureKind called = kindName.Length > 0 ? ContentCatalog.CreatureOf(kindName) : null;
			UIKit.Button(krow, called != null ? called.Label : "its own", () =>
			{
				string next = kinds[(kinds.IndexOf(kindName) + 1) % kinds.Count];
				Set(target, BossPhases.SummonKind, next.Length > 0 ? next : null);
				Refresh();
			}, "Click to step through: its own kind, or one of the hostile creatures", -1, 26f, 12);
			UIKit.SliderRow s = UIKit.Slider(g, "Speed then", BossPhases.MinSpeed, BossPhases.MaxSpeed, BossPhases.SpeedOf(p), v => "×" + v.ToString("0.00"), null, "How much faster it gets (on top of its speed above)");
			s.Slider.onValueChanged.AddListener(v => Set(target, BossPhases.Speed, ObjectProps.Format(Mathf.Round(v * 20f) / 20f), "1", false));
			InputField sig = UIKit.Field(g, "Signal (phase)", ObjectProps.Get(p, BossPhases.Signal), 26f, "The signal it sends when the phase starts (world plans and 'signal' checks can wait for it). Empty: 'phase'");
			sig.onEndEdit.AddListener(v => Set(target, BossPhases.Signal, v.Trim().Length > 0 ? v.Trim() : null, null));
			amountFields.Add(sig);
		}

		/// <summary>A survivor's cage (RF3): who is in it, what they say, the story item that opens it, the signal.</summary>
		static void SurvivorGroup(EditorGameObject target)
		{
			Dictionary<string, string> p = target.Props;
			RectTransform g = UIKit.Group(root, "Survivor");
			InputField name = SurvivorField(g, "Name", ObjectProps.Get(p, Survivors.NameProp, Survivors.DefaultName), 26f, "The survivor's name (in the bubble and on the raft)");
			name.characterLimit = 32;
			name.onEndEdit.AddListener(v => Set(target, Survivors.NameProp, v.Trim().Length > 0 ? v.Trim() : null, null));
			amountFields.Add(name);
			// Who it is: steps through Raft's characters (none: one picked by the survivor's id)
			string ch = ObjectProps.Get(p, Survivors.CharacterProp);
			List<string> chars = new[] { "" }.Concat(Behaviours.Characters().Where(c => c != null).Select(c => c.displayName)).Distinct().ToList();
			if (ch.Length > 0 && !chars.Contains(ch)) chars.Add(ch);
			RectTransform crow = UIKit.Row(g, 26f, 4f, "SurvivorCharacter");
			UIKit.Size(UIKit.Label(crow, "Looks like", 13, UIKit.TextMuted).gameObject, 80);
			UIKit.Button(crow, ch.Length > 0 ? ch : "any (by its id)", () =>
			{
				string next = chars[(chars.IndexOf(ch) + 1) % chars.Count];
				Set(target, Survivors.CharacterProp, next.Length > 0 ? next : null);
				Refresh();
			}, "Click to step through Raft's characters", -1, 26f, 12);
			UIKit.Size(UIKit.Label(g, "What the survivor says when freed", 12, UIKit.TextMuted).gameObject, -1, 16);
			InputField line = UIKit.TextArea(g, "A line in a speech bubble", 46f, "Shown over the survivor for a few seconds, then the survivor is on the raft");
			line.text = ObjectProps.Get(p, Survivors.LineProp, Survivors.DefaultLine);
			line.characterLimit = 160;
			line.onEndEdit.AddListener(v => Set(target, Survivors.LineProp, v.Trim().Length > 0 ? v.Trim() : null));
			amountFields.Add(line);
			InputField key = SurvivorField(g, "Key item", StoryItems.IdOf(ObjectProps.Get(p, Survivors.KeyProp)), 26f, "The story item that opens the cage (used up). Empty: the cage isn't locked");
			key.onEndEdit.AddListener(v => Set(target, Survivors.KeyProp, v.Trim().Length > 0 ? StoryItems.Ref(StoryItems.IdOf(v.Trim())) : null, null));
			amountFields.Add(key);
			InputField id = SurvivorField(g, "Survivor id", ObjectProps.Get(p, Survivors.IdProp), 26f, "One survivor per id in a world (empty: the island's name and the cage). Two cages with one id free one survivor");
			id.onEndEdit.AddListener(v => Set(target, Survivors.IdProp, v.Trim().Length > 0 ? v.Trim() : null, null));
			amountFields.Add(id);
			InputField sig = SurvivorField(g, "Signal", ObjectProps.Get(p, Survivors.SignalProp), 26f, "The signal sent when the survivor is freed (world plans and 'signal' checks can wait for it). Empty: 'survivor'");
			sig.onEndEdit.AddListener(v => Set(target, Survivors.SignalProp, v.Trim().Length > 0 ? v.Trim() : null, null));
			amountFields.Add(sig);
		}

		static InputField SurvivorField(RectTransform g, string label, string text, float height, string hint)
		{
			RectTransform row = UIKit.Row(g, height, 4f, label);
			UIKit.Size(UIKit.Label(row, label, 13, UIKit.TextMuted).gameObject, 80);
			return UIKit.Field(row, label, text, height, hint);
		}

		static void Multiplier(Transform g, EditorGameObject target, string label, string key, float min, float max, float value, string hint)
		{
			UIKit.SliderRow s = UIKit.Slider(g, label, min, max, value, v => "\u00D7" + v.ToString("0.00"), null, hint);
			// Rounded to steps of 0.05 so values like 1.00 and 2.00 are easy to hit
			s.Slider.onValueChanged.AddListener(v =>
			{
				float r = Mathf.Round(v * 20f) / 20f;
				Set(target, key, ObjectProps.Format(r), "1", false);
			});
		}

		static string Capital(string s) { return string.IsNullOrEmpty(s) ? "" : char.ToUpper(s[0]) + s.Substring(1); }

		#endregion

		#region Notes

		static void NoteGroup(EditorGameObject target)
		{
			Dictionary<string, string> p = target.Props;
			RectTransform g = UIKit.Group(root, "Note");
			titleField = UIKit.Field(g, "Title", ObjectProps.Get(p, ObjectProps.NoteTitle, ""), 30f, "The note's title (Enter keeps it)");
			titleField.characterLimit = NoteEditorWindow.MaxTitle;
			titleField.onEndEdit.AddListener(v => Set(target, ObjectProps.NoteTitle, v.Trim(), null));
			string text = ObjectProps.Get(p, ObjectProps.NoteText, "");
			string preview = text.Length == 0 ? "<i>(no text yet)</i>" : Escape(text.Length > 140 ? text.Substring(0, 140).TrimEnd() + "\u2026" : text);
			Text pt = UIKit.Label(g, preview, 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Preview");
			pt.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(pt.gameObject, -1, 48);
			RectTransform row = UIKit.Row(g, UIKit.RowHeight, 4f);
			Button edit = UIKit.Button(row, "Edit note...", () => NoteEditorWindow.Open(target), "Write the note's text in a big window, with a preview");
			UIKit.SetActive(edit, true);
			if (!ContentCatalog.IsNoteObject(target.GameObjectName))
				UIKit.Button(row, "Remove", () =>
				{
					Dictionary<string, string> np = ObjectProps.With(target.Props, ObjectProps.NoteTitle, null);
					PropsCommand.Change(target, ObjectProps.With(np, ObjectProps.NoteText, null));
					Refresh();
				}, "This object is no longer readable", 80);
		}

		static string Escape(string s) { return s.Replace("<", "\u2039").Replace(">", "\u203A"); }

		/// <summary>The abandoned raft crate: what it gives, and what it can't do.</summary>
		static void RaftCrateGroup()
		{
			RectTransform g = UIKit.Group(root, "Abandoned raft crate");
			Text t = UIKit.Label(g, "Raft's own crate from the abandoned rafts. Players take it whole with the interact key (E) and get a handful of random items from Raft's loot for those rafts (now and then a cooking recipe or a mystery package).", 12, UIKit.TextMuted);
			t.lineSpacing = 1.05f;
			Text w = UIKit.Label(g, "• <b>What's inside can't be chosen</b>: for chosen loot, place a chest (Loot & chests) instead.\n• <b>It doesn't count for a quest's \"Open a chest\" step</b> - a chest does.", 12, UIKit.Accent);
			w.lineSpacing = 1.05f;
		}

		/// <summary>What players can do with this object in a world, for the features it doesn't have yet.</summary>
		static void AddFeatureGroup(EditorGameObject target, bool note, bool loot)
		{
			RectTransform g = UIKit.Group(root, note || loot ? "Also make it..." : PlaceableCatalog.DisplayName(target.GameObjectName));
			if (!note && !loot) UIKit.Label(g, "Players use it in a world with the interact key (E):", 12, UIKit.TextMuted);
			RectTransform row = UIKit.Row(g, UIKit.RowHeight, 4f);
			if (!note)
				UIKit.Button(row, "Readable...", () =>
				{
					NoteEditorWindow.Apply(target, "Note", "");
					NoteEditorWindow.Open(target);
				}, "Write a note on this object (a sign, a book, a crate...)");
			if (!loot)
				UIKit.Button(row, "A chest...", () =>
				{
					PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.LootItems, ""));
					Refresh();
					ItemPickerWindow.Open(target);
				}, "Put items in this object: players open it and take them");
		}

		#endregion

		#region Trigger zones

		static void ZoneGroup(EditorGameObject target)
		{
			Dictionary<string, string> p = target.Props;
			RectTransform g = UIKit.Group(root, "Trigger zone");
			RectTransform idRow = UIKit.Row(g, 28f, 4f, "Id");
			UIKit.Size(UIKit.Label(idRow, "Name", 13, UIKit.TextMuted).gameObject, 44);
			string id = ObjectProps.Get(p, ObjectProps.ZoneId);
			InputField idField = UIKit.Field(idRow, "zone name", id, 28f, "Creatures (and quests) link to the zone by this name");
			idField.characterLimit = 24;
			idField.onEndEdit.AddListener(v => RenameZone(target, id, v.Trim()));
			amountFields.Add(idField);

			UIKit.Slider(g, "Size (radius)", ObjectProps.MinZoneRadius, ObjectProps.MaxZoneRadius, ObjectProps.Radius(p), v => v.ToString("0") + " m",
				v => Set(target, ObjectProps.ZoneRadius, ObjectProps.Format(Mathf.Round(v)), "6", false), "How close players must come (the yellow sphere)", true);

			// (what the box is, also once it has text)
			UIKit.Size(UIKit.Label(g, "Message when a player walks in", 12, UIKit.TextMuted).gameObject, -1, 16);
			InputField msg = UIKit.TextArea(g, "Message shown to the player who walks in (optional)", 46f, "A line or two, shown at the top of the screen");
			msg.text = ObjectProps.Get(p, ObjectProps.ZoneMessage);
			msg.characterLimit = 200;
			msg.onEndEdit.AddListener(v => Set(target, ObjectProps.ZoneMessage, v.Trim().Length > 0 ? v.Trim() : null));
			amountFields.Add(msg);

			RectTransform fires = UIKit.Row(g, 26f, 4f, "Fires");
			UIKit.Size(UIKit.Label(fires, "Fires", 13, UIKit.TextMuted).gameObject, 44);
			bool repeats = ObjectProps.Repeats(p), onceEver = ObjectProps.Get(p, ObjectProps.ZoneRepeat) == ObjectProps.ZoneOnceEver;
			Button once = UIKit.Button(fires, "Once", () => { Set(target, ObjectProps.ZoneRepeat, null); Refresh(); }, "Once, for the first player - and ready again after the island's regrow days (like loot)", -1, 26f, 12);
			Button ever = UIKit.Button(fires, "Once ever", () => { Set(target, ObjectProps.ZoneRepeat, ObjectProps.ZoneOnceEver); Refresh(); }, "Once in the world, never again - for the story: an ambush, a bridge shown", -1, 26f, 12);
			Button every = UIKit.Button(fires, "Every time", () => { Set(target, ObjectProps.ZoneRepeat, "1"); Refresh(); }, "Each time a player walks in (at most every half minute)", -1, 26f, 12);
			UIKit.SetActive(once, !repeats && !onceEver);
			UIKit.SetActive(ever, onceEver);
			UIKit.SetActive(every, repeats);

			// (an air pocket: a diver breathes inside it, as in Raft's air pockets under Caravan Town)
			RectTransform air = UIKit.Row(g, 26f, 4f, "Air");
			UIKit.Size(UIKit.Label(air, "Air", 13, UIKit.TextMuted).gameObject, 44);
			bool isAir = ObjectProps.GetBool(p, ObjectProps.ZoneAir, false);
			Button noAir = UIKit.Button(air, "None", () => { Set(target, ObjectProps.ZoneAir, null); Refresh(); }, "An ordinary zone: no air in it", -1, 26f, 12);
			Button yesAir = UIKit.Button(air, "Air pocket", () => { Set(target, ObjectProps.ZoneAir, "1"); Refresh(); }, "An air pocket: a player inside breathes - breath back to full, while the zone is shown (for a dive, as in Raft's air pockets under Caravan Town)", -1, 26f, 12);
			UIKit.SetActive(noAir, !isAir);
			UIKit.SetActive(yesAir, isAir);

			GameObject placed = GameObject.Find("PlacedObjects");
			int linked = placed == null || id.Length == 0 ? 0 : placed.GetComponentsInChildren<EditorGameObject>().Count(e => ObjectProps.Get(e.Props, ObjectProps.CreatureZone) == id);
			UIKit.Label(g, linked > 0 ? linked + " creature spot(s) wait for this zone (an ambush)." : "<i>Creatures can wait for it: select one and set \"Appears\".</i>", 12, UIKit.TextMuted);
		}

		static readonly Color[] FogColors =
		{
			new Color(0.92f, 0.94f, 0.96f), new Color(0.55f, 0.58f, 0.62f), new Color(0.45f, 0.6f, 0.8f), new Color(0.45f, 0.6f, 0.35f),
			new Color(1f, 0.6f, 0.35f), new Color(0.6f, 0.4f, 0.75f), new Color(0.7f, 0.2f, 0.15f),
		};
		static readonly Color[] LightColors =
		{
			new Color(1f, 0.85f, 0.6f), new Color(0.6f, 0.75f, 1f), new Color(0.6f, 1f, 0.6f), new Color(0.8f, 0.55f, 1f),
			new Color(1f, 0.45f, 0.35f), new Color(0.35f, 0.35f, 0.45f),
		};

		static void RadiusSlider(Transform g, EditorGameObject target)
		{
			UIKit.Slider(g, "Size (radius)", ObjectProps.MinZoneRadius, ObjectProps.MaxZoneRadius, ObjectProps.Radius(target.Props), v => v.ToString("0") + " m",
				v => Set(target, ObjectProps.ZoneRadius, ObjectProps.Format(Mathf.Round(v)), "6", false), "How far it reaches (the sphere)", true);
		}

		/// <summary>A row of colour swatches (with None) setting a colour key; strength slider below when a colour is set.</summary>
		static void ColourChoice(Transform g, EditorGameObject target, string title, string key, string amountKey, float defaultAmount, Color[] colours, string hint)
		{
			UIKit.Size(UIKit.Label(g, title, 13, UIKit.TextMuted).gameObject, -1, 16);
			RectTransform row = UIKit.Row(g, 22f, 3f, title);
			bool has = ObjectProps.HasColor(target.Props, key);
			Button none = UIKit.Button(row, "None", () => { Set(target, key, null); Refresh(); }, "No " + title.ToLowerInvariant(), 42, 22f, 11);
			UIKit.SetActive(none, !has);
			foreach (Color c in colours)
			{
				Color col = c;
				UIKit.ColorButton(row, c, () => { Set(target, key, ObjectProps.ColorText(col)); Refresh(); }, hint, 22f);
			}
			if (has)
				UIKit.Slider(g, "Strength", 0.05f, 1f, Mathf.Clamp01(ObjectProps.GetFloat(target.Props, amountKey, defaultAmount)), v => (v * 100f).ToString("F0") + " %",
					v => Set(target, amountKey, ObjectProps.Format(Mathf.Round(v * 100f) / 100f), null, false), "How strong it is in the middle of the zone");
		}

		static void AtmosphereGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, "Atmosphere zone");
			UIKit.Label(g, "Fly the camera into the sphere to see it.", 12, UIKit.TextMuted);
			RadiusSlider(g, target);
			ColourChoice(g, target, "Fog", ObjectProps.AtmoFog, ObjectProps.AtmoFogAmount, 0.6f, FogColors, "Fog of this colour, thicker towards the middle");
			ColourChoice(g, target, "Light", ObjectProps.AtmoLight, ObjectProps.AtmoLightAmount, 0.5f, LightColors, "Tints the light (warm, cold, eerie...)");
			UIKit.Size(UIKit.Label(g, "Particles", 13, UIKit.TextMuted).gameObject, -1, 16);
			RectTransform row = UIKit.Row(g, 24f, 3f, "Particles");
			string current = ObjectProps.Get(target.Props, ObjectProps.AtmoParticles, "none");
			foreach (string kind in AtmosphereZone.ParticleKinds)
			{
				string k = kind;
				Button b = UIKit.Button(row, char.ToUpper(k[0]) + k.Substring(1), () => { Set(target, ObjectProps.AtmoParticles, k == "none" ? null : k); Refresh(); }, "Particles: " + k, -1, 24f, 10);
				UIKit.SetActive(b, k == current);
			}
		}

		static void SoundGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, "Sound zone");
			string ev = ObjectProps.Get(target.Props, ObjectProps.SoundEvent);
			Text name = UIKit.Label(g, ev.Length > 0 ? ev : "<i>No sound chosen yet</i>", 12, ev.Length > 0 ? UIKit.TextColor : UIKit.TextMuted);
			name.horizontalOverflow = HorizontalWrapMode.Wrap;
			RectTransform row = UIKit.Row(g, 26f, 4f, "Choose");
			Button choose = UIKit.Button(row, "Choose sound...", () => SoundPickerWindow.Open(target), "Pick one of Raft's sounds (birds, wind, waves, music...)", -1, 26f, 12);
			UIKit.SetActive(choose, ev.Length == 0);
			if (ev.Length > 0)
			{
				UIKit.Button(row, "\u25BA", () => SoundLibrary.Preview(ev), "Listen", 32, 26f, 12);
				UIKit.Button(row, "\u25A0", SoundLibrary.StopPreview, "Stop", 32, 26f, 12);
			}
			UIKit.Slider(g, "Volume", 0.05f, 1f, Mathf.Clamp01(ObjectProps.GetFloat(target.Props, ObjectProps.SoundVolume, 0.8f)), v => (v * 100f).ToString("F0") + " %",
				v => Set(target, ObjectProps.SoundVolume, ObjectProps.Format(Mathf.Round(v * 100f) / 100f), "0.8", false), "How loud (in the middle of the zone)");
			RectTransform mode = UIKit.Row(g, 24f, 4f, "Mode");
			bool once = ObjectProps.Get(target.Props, ObjectProps.SoundMode) == "enter";
			Button loop = UIKit.Button(mode, "While inside", () => { Set(target, ObjectProps.SoundMode, null); Refresh(); }, "Plays (loops) while a player is in the zone, louder towards the middle", -1, 24f, 11);
			Button enter = UIKit.Button(mode, "Once on entering", () => { Set(target, ObjectProps.SoundMode, "enter"); Refresh(); }, "Plays once each time a player walks in", -1, 24f, 11);
			UIKit.SetActive(loop, !once);
			UIKit.SetActive(enter, once);
			RadiusSlider(g, target);
		}

		/// <summary>Renames a zone; the creatures that waited for the old name follow (one undo step each).</summary>
		static void RenameZone(EditorGameObject target, string oldId, string newId)
		{
			if (newId.Length == 0 || newId == oldId) { Refresh(); return; }
			Set(target, ObjectProps.ZoneId, newId);
			GameObject placed = GameObject.Find("PlacedObjects");
			if (placed != null && oldId.Length > 0)
				foreach (EditorGameObject e in placed.GetComponentsInChildren<EditorGameObject>().Where(e => ObjectProps.Get(e.Props, ObjectProps.CreatureZone) == oldId).ToList())
					PropsCommand.Change(e, ObjectProps.With(e.Props, ObjectProps.CreatureZone, newId));
			Refresh();
		}

		#endregion

		#region Loot

		/// <param name="zone">a trigger zone's items (given on entering): no refill or "not a chest" rows</param>
		static void LootGroup(EditorGameObject target, bool zone)
		{
			List<KeyValuePair<string, int>> loot = ObjectProps.Loot(target.Props);
			RectTransform g = UIKit.Group(root, (zone ? "Gives on entering" : "Loot") + (loot.Count > 0 ? " (" + loot.Count + ")" : ""));
			Transform rows = g;
			if (loot.Count > 5)
			{
				// Many kinds of items: a short scrolling list, so the panel stays on the screen
				RectTransform box = UIKit.Rect("Items", g);
				UIKit.Size(box.gameObject, -1, 5 * 30f);
				ScrollRect scroll;
				rows = UIKit.ScrollList(box, out scroll, 4f);
				UIKit.Stretch((RectTransform)scroll.transform);
			}
			if (loot.Count == 0) UIKit.Label(g, zone ? "<i>Nothing: add items to give players who walk in.</i>" : "<i>Empty: add items, or pick a ready-made set.</i>", 12, UIKit.TextMuted);
			for (int i = 0; i < loot.Count; i++)
			{
				int index = i;
				string item = loot[i].Key;
				RectTransform row = UIKit.Row(rows, 26f, 3f, "Item");
				RectTransform pic = UIKit.Rect("Icon", row);
				UIKit.Size(pic.gameObject, 24, 24);
				Image icon = pic.gameObject.AddComponent<Image>();
				icon.sprite = ContentCatalog.ItemSprite(item); icon.preserveAspect = true; icon.raycastTarget = false;
				if (icon.sprite == null) icon.color = new Color(1, 1, 1, 0.1f);
				Text label = UIKit.Label(row, ContentCatalog.ItemLabel(item), 12, ContentCatalog.ItemExists(item) ? UIKit.TextColor : UIKit.Danger);
				UIKit.Fit(label, 8);
				InputField amount = UIKit.Field(row, "1", loot[i].Value.ToString(), 24f, "How many");
				UIKit.Size(amount.gameObject, 46, 24);
				amount.contentType = InputField.ContentType.IntegerNumber;
				amount.characterLimit = 3;
				amount.onEndEdit.AddListener(v =>
				{
					int n;
					if (!int.TryParse(v, out n)) n = 1;
					SetLootAmount(target, index, Mathf.Clamp(n, 0, ObjectProps.MaxLootAmount));
				});
				amountFields.Add(amount);
				Button remove = UIKit.Button(row, "\u00D7", () => SetLootAmount(target, index, 0), "Take it out of the chest", 24, 24, 13);
				UIKit.DangerButton(remove);
			}
			RectTransform add = UIKit.Row(g, 26f, 4f, "Add");
			Button more = UIKit.Button(add, "Add items...", () => ItemPickerWindow.Open(target), "Choose from all of Raft's items, with pictures", -1, 26f, 12);
			UIKit.SetActive(more, true);
			UIKit.Button(add, "Empty", () => { PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.LootItems, "")); Refresh(); }, "Take everything out", 60, 26f, 12);
			RectTransform presets = UIKit.Row(g, 24f, 3f, "Presets");
			foreach (var preset in ContentCatalog.LootPresets)
			{
				string[] items = preset.Value;
				UIKit.Button(presets, preset.Key, () =>
				{
					PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.LootItems, ContentCatalog.PresetLoot(items)));
					Refresh();
				}, "Fill it with a ready-made set: " + string.Join(", ", items.Select(x => x.Split('*')[0]).ToArray()), -1, 24f, 11);
			}
			if (zone) return;
			RectTransform refill = UIKit.Row(g, 24f, 4f, "Refill");
			UIKit.Label(refill, "Fills up again", 12, UIKit.TextMuted);
			bool refills = ObjectProps.LootRefills(target.Props);
			Button on = UIKit.Button(refill, EditorRegrowDays > 0 ? "after " + EditorRegrowDays + " days" : "never (rule)", () => { Set(target, ObjectProps.LootRefill, null); Refresh(); },
				"Once looted, it fills up again after the island's regrow days (its Island tab rule, else the world's setting)", 96, 24f, 11);
			Button off = UIKit.Button(refill, "Never", () => { Set(target, ObjectProps.LootRefill, "0"); Refresh(); }, "Once looted, it stays empty in that world", 56, 24f, 11);
			UIKit.SetActive(on, refills);
			UIKit.SetActive(off, !refills);
			// (story items: a second key or log would come - such a chest never fills up again, whatever is chosen)
			if (refills && ObjectProps.Loot(target.Props).Any(l => StoryItems.IsStory(l.Key)))
				UIKit.Label(g, "<i>It holds story items: it never fills up again (the story's key or log comes once).</i>", 11, UIKit.TextMuted);
			if (!ContentCatalog.IsLootObject(target.GameObjectName))
				UIKit.Button(g, "Not a chest", () =>
				{
					PropsCommand.Change(target, ObjectProps.With(ObjectProps.With(target.Props, ObjectProps.LootItems, null), ObjectProps.LootRefill, null));
					Refresh();
				}, "This object no longer holds loot", -1, 24f, 11);
		}

		static readonly List<InputField> amountFields = new List<InputField>();

		#region Place exactly

		/// <summary>Position X, Y, Z / Turn X, Y, Z / Size X, Y, Z of the shown object (ROADMAP E4), in that order.</summary>
		static readonly List<InputField> placeFields = new List<InputField>();
		static readonly string[] PlaceRows = { "Position", "Turn", "Size" };
		static float nextPlaceShow;

		/// <summary>Tests: the place fields (position, turn, size; x, y, z each).</summary>
		internal static IList<InputField> PlaceFields { get { return placeFields; } }

		/// <summary>
		/// "Place exactly": the object's position (metres from the build area's corner, Y above the sea), turn (degrees)
		/// and size, typed. Enter (or leaving a field) moves it - one undo step.
		/// </summary>
		static void PlaceGroup(EditorGameObject target)
		{
			RectTransform g = UIKit.Group(root, "Place exactly");
			UIKit.Hint(g.gameObject, "Type where the object is: X and Z in metres across the build area, Y in metres above the sea; its turn in degrees; its size (1 = as made). Enter moves it (Ctrl+Z undoes).");
			for (int r = 0; r < 3; r++)
			{
				RectTransform row = UIKit.Row(g, 24f, 3f, PlaceRows[r]);
				Text l = UIKit.Label(row, PlaceRows[r], 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, "Label");
				UIKit.Size(l.gameObject, 52, 24);
				for (int a = 0; a < 3; a++)
				{
					InputField f = UIKit.Field(row, "xyz"[a].ToString(), "", 24f, PlaceRows[r] + " " + "XYZ"[a]);
					f.name = "Place_" + PlaceRows[r] + "_" + "XYZ"[a];
					f.contentType = InputField.ContentType.DecimalNumber;
					f.characterLimit = 9;
					f.onEndEdit.AddListener(v => ApplyPlace(target));
					placeFields.Add(f);
				}
			}
			nextPlaceShow = 0f;
			ShowPlace();
		}

		static Vector3 Corner { get { return terraineditor.terrain != null ? terraineditor.terrain.transform.position + new Vector3(0f, DynamicIslands.EditorWaterLevel, 0f) : new Vector3(0f, DynamicIslands.EditorWaterLevel, 0f); } }

		static string Num(float v) { return (Mathf.Round(v * 100f) / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

		/// <summary>The fields show where the object is now (it may have been moved with the gizmo).</summary>
		static void ShowPlace()
		{
			if (placeFields.Count != 9 || shown == null || Time.unscaledTime < nextPlaceShow) return;
			nextPlaceShow = Time.unscaledTime + 0.2f;
			Transform t = shown.transform;
			Vector3 p = t.position - Corner, e = t.rotation.eulerAngles, s = t.lossyScale;
			float[] v = { p.x, p.y, p.z, e.x, e.y, e.z, s.x, s.y, s.z };
			for (int i = 0; i < 9; i++)
			{
				string text = Num(i >= 3 && i < 6 && v[i] > 180f ? v[i] - 360f : v[i]);
				if (placeFields[i].text != text) placeFields[i].text = text;
			}
		}

		/// <summary>The typed place applied to the object, one undo step.</summary>
		internal static void ApplyPlace(EditorGameObject target)
		{
			if (target == null || placeFields.Count != 9) return;
			var v = new float[9];
			for (int i = 0; i < 9; i++)
				if (!float.TryParse(placeFields[i].text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i])) { nextPlaceShow = 0f; return; }
			Transform t = target.transform;
			Vector3 pos = Corner + new Vector3(v[0], v[1], v[2]);
			Quaternion rot = Quaternion.Euler(v[3], v[4], v[5]);
			Vector3 scale = new Vector3(Mathf.Clamp(v[6], 0.01f, 10000f), Mathf.Clamp(v[7], 0.01f, 10000f), Mathf.Clamp(v[8], 0.01f, 10000f));
			if ((pos - t.position).sqrMagnitude < 1e-6f && Quaternion.Angle(rot, t.rotation) < 0.01f && (scale - t.lossyScale).sqrMagnitude < 1e-6f) return;
			var gizmo = DynamicIslands.EditorGizmoHandler;
			var cmd = new RuntimeGizmos.TransformCommand(gizmo, t);
			t.position = pos;
			t.rotation = rot;
			t.localScale = t.parent != null && t.parent.lossyScale != Vector3.one ? Vector3.Scale(scale, new Vector3(1f / t.parent.lossyScale.x, 1f / t.parent.lossyScale.y, 1f / t.parent.lossyScale.z)) : scale;
			cmd.StoreNewTransformValues();
			CommandUndoRedo.UndoRedoManager.Insert(cmd);
			if (gizmo != null) gizmo.SetPivotPoint();
			nextPlaceShow = 0f;
		}

		#endregion

		static void SetLootAmount(EditorGameObject target, int index, int amount)
		{
			List<KeyValuePair<string, int>> loot = ObjectProps.Loot(target.Props);
			if (index < 0 || index >= loot.Count) return;
			if (amount <= 0) loot.RemoveAt(index);
			else loot[index] = new KeyValuePair<string, int>(loot[index].Key, amount);
			PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.LootItems, ObjectProps.LootText(loot)));
			Refresh();
		}

		#endregion

		#region Colour

		static void ColourGroup(EditorGameObject target, bool creature)
		{
			Dictionary<string, string> p = target.Props;
			RectTransform g = UIKit.Group(root, "Colour");
			// (None and nine swatches just fit the panel's width: 40 + 9 x 20 + 9 x 3)
			RectTransform row = UIKit.Row(g, 22f, 3f, "Swatches");
			Button none = UIKit.Button(row, "None", () => SetTint(target, null), "Its own colours", 40, 22f, 11);
			UIKit.SetActive(none, !ObjectProps.HasTint(p));
			foreach (Color c in Swatches)
			{
				Color col = c;
				UIKit.ColorButton(row, c, () => SetTint(target, col), "Tint it this colour" + (creature ? " (every animal of this spot)" : ""), 20f);
			}
			if (!ObjectProps.HasTint(p) && !showRgb)
			{
				UIKit.Button(g, "Custom colour...", () => { showRgb = true; Refresh(); }, "Mix your own tint", -1, 24f, 12);
				return;
			}
			UIKit.Slider(g, "Strength", 0f, 1f, ObjectProps.HasTint(p) ? ObjectProps.TintStrength(p) : 1f, v => (v * 100f).ToString("F0") + " %",
				v => { if (ObjectProps.HasTint(target.Props)) Set(target, ObjectProps.TintAmount, ObjectProps.Format(Mathf.Round(v * 100f) / 100f), "1", false); },
				"How strongly the colour shows");
			if (showRgb)
			{
				Color cur = ObjectProps.HasTint(p) ? ObjectProps.Tint(p) : Color.white;
				string[] names = { "Red", "Green", "Blue" };
				for (int i = 0; i < 3; i++)
				{
					int ch = i;
					UIKit.Slider(g, names[i], 0f, 1f, cur[ch], v => Mathf.RoundToInt(v * 255f).ToString(), v =>
					{
						Color c = ObjectProps.HasTint(target.Props) ? ObjectProps.Tint(target.Props) : Color.white;
						c[ch] = v;
						Set(target, ObjectProps.TintColor, ObjectProps.ColorText(c), null, false, "tint.rgb");
					}, "The tint's " + names[i].ToLowerInvariant() + " (the object's own colours are multiplied by it)");
				}
			}
			else UIKit.Button(g, "Custom colour...", () => { showRgb = true; Refresh(); }, "Mix your own tint", -1, 24f, 12);
		}

		static void SetTint(EditorGameObject target, Color? c)
		{
			Dictionary<string, string> np = ObjectProps.With(target.Props, ObjectProps.TintColor, c.HasValue ? ObjectProps.ColorText(c.Value) : null);
			if (!c.HasValue) np = ObjectProps.With(np, ObjectProps.TintAmount, null);
			PropsCommand.Change(target, np);
			Refresh();
		}

		#endregion

		/// <summary>
		/// Sets one setting as an undo step. rebuild = false while dragging a slider (rebuilding would drop the drag).
		/// mergeKey groups the changes of one slider drag into one step.
		/// </summary>
		static void Set(EditorGameObject target, string key, string value, string defaultValue = null, bool rebuild = true, string mergeKey = null)
		{
			if (target == null) return;
			PropsCommand.Change(target, ObjectProps.With(target.Props, key, value, defaultValue), rebuild ? null : (mergeKey ?? key));
			// The command refreshes the inspector; while dragging, keep the controls as they are
			if (!rebuild) dirty = false;
		}
	}
}
