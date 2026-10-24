using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The quest editor: the island's quest with its steps (go to a zone, read a note, open a chest, defeat or catch
	/// animals), a reward and messages. Steps name things placed on the island; the window lists those names.
	/// Save writes the quest into the island's settings (saved with the island).
	/// </summary>
	public class QuestEditorWindow : MonoBehaviour
	{
		const int MaxSteps = IslandQuest.MaxStepCount;
		static QuestEditorWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		IslandQuest quest = new IslandQuest();
		/// <summary>Which of the island's quests is shown (0 = the main quest; ROADMAP LM4) and the edits of each.</summary>
		int number;
		readonly Dictionary<int, IslandQuest> working = new Dictionary<int, IslandQuest>();
		RectTransform questTabs, bringGroup;
		InputField titleField, introField, doneField;
		RectTransform stepsRoot;
		Text rewardText, namesText;
		readonly List<InputField> fields = new List<InputField>();
		bool pickingReward;

		/// <summary>"When the quest is done: bring ..." (a rule of the island, WorldDirector); null = nothing.</summary>
		IntroRule bring;
		Button bringKindButton, bringWhatButton, bringDirButton;
		InputField bringDistField, bringMessageField, bringLabelField;
		RectTransform bringDetails;
		public const string BringRuleId = "quest-done";
		static readonly string[] BringKinds = { "nothing", "island", "type" };

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("QuestEditorWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<QuestEditorWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			instance.working.Clear();
			int count = IslandQuest.CountIn(DynamicIslands.currentIslandProps);
			for (int i = 0; i < count; i++) instance.working[i] = IslandQuest.From(DynamicIslands.currentIslandProps, i, true);
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.Show(0);
			instance.bring = QuestBringRule(DynamicIslands.currentIslandProps);
			instance.bring = instance.bring != null ? instance.bring.Clone() : null;
			instance.ShowBring();
			instance.Keep();
			instance.KeepBring();
			instance.savedPrint = instance.Print();
		}

		/// <summary>What Esc compares: the quests and the bring rule as they would be saved.</summary>
		string savedPrint;

		string Print()
		{
			var d = new Dictionary<string, string>();
			foreach (var kv in working.OrderBy(e => e.Key)) kv.Value.To(d, kv.Key);
			return string.Join("\n", d.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Key + "=" + e.Value).ToArray()) + "\nbring=" + (bring != null ? bring.ToLine() : "");
		}

		/// <summary>The quests differ from the island's (Esc asks before it closes).</summary>
		public static bool Unsaved
		{
			get
			{
				if (!IsOpen) return false;
				instance.Keep();
				instance.KeepBring();
				return instance.Print() != instance.savedPrint;
			}
		}

		/// <summary>Shows quest n of the island (its edits kept while another is shown).</summary>
		void Show(int n)
		{
			number = n;
			IslandQuest q;
			if (!working.TryGetValue(n, out q)) working[n] = q = new IslandQuest();
			if (!q.Exists) q.Steps.Add(new IslandQuest.Step());
			quest = q;
			titleField.text = quest.Title;
			introField.text = quest.Intro;
			doneField.text = quest.Done;
			ShowSteps();
			ShowReward();
			ShowNames();
			ShowTabs();
			// ("bring a new island" belongs to the main quest)
			if (bringGroup != null) bringGroup.gameObject.SetActive(n == 0);
		}

		/// <summary>The fields' text into the quest shown.</summary>
		void Keep()
		{
			quest.Title = titleField.text;
			quest.Intro = introField.text;
			quest.Done = doneField.text;
			working[number] = quest;
		}

		/// <summary>The buttons for the island's quests: one per quest, another quest, remove this one.</summary>
		void ShowTabs()
		{
			foreach (Transform c in questTabs) Destroy(c.gameObject);
			int count = working.Keys.Count == 0 ? 1 : working.Keys.Max() + 1;
			for (int i = 0; i < count; i++)
			{
				int n = i;
				IslandQuest q;
				string label = n == 0 ? "Main quest" : "Quest " + (n + 1);
				if (working.TryGetValue(n, out q) && q.Title.Trim().Length > 0) label += ": " + (q.Title.Trim().Length > 16 ? q.Title.Trim().Substring(0, 15) + "\u2026" : q.Title.Trim());
				Button b = UIKit.Button(questTabs, label, () => { Keep(); Show(n); }, n == 0 ? "The island's main quest: the panel shows it first, world plans and the story wait for it" : "Another quest of the island, with its own steps and reward", -1, 26f, 12);
				if (n == number) UIKit.SetActive(b, true);
			}
			if (count < IslandQuest.MaxQuests)
				UIKit.Button(questTabs, "+ Another quest", () => { Keep(); Show(count); }, "A side quest of this island: its own steps, reward and messages. Players see one quest at a time in the panel (the first not done yet)", 130, 26f, 12);
			if (number > 0)
				UIKit.Button(questTabs, "Remove this quest", () =>
				{
					// (the ones after it move up one: "Quest 3" becomes "Quest 2")
					for (int i = number; i < count - 1; i++) { IslandQuest next; if (working.TryGetValue(i + 1, out next)) working[i] = next; else working.Remove(i); }
					working.Remove(count - 1);
					Show(0);
				}, "Takes this quest off the island (Save to keep it so)", 140, 26f, 12);
		}

		/// <summary>The island's rule that fires when its own main quest is done, or null (a rule waiting for Quest 2 or
		/// later is left alone: ROADMAP CB6).</summary>
		public static IntroRule QuestBringRule(IDictionary<string, string> props)
		{
			return IntroRule.ParseLines(ObjectProps.Get(props, WorldDirector.IslandRulesKey))
				.FirstOrDefault(r => r.When == "quest" && (r.WhenRef.Length == 0 || r.WhenRef.Equals(IntroRule.Self, StringComparison.OrdinalIgnoreCase))
					&& (r.WhenArg.Trim().Length == 0 || r.WhenArg.Trim() == "1"));
		}

		/// <summary>Sets (or with null removes) the island's "when the quest is done, bring ..." rule, keeping its other rules.</summary>
		public static void SetQuestBringRule(IDictionary<string, string> props, IntroRule rule)
		{
			List<IntroRule> rules = IntroRule.ParseLines(ObjectProps.Get(props, WorldDirector.IslandRulesKey));
			IntroRule old = QuestBringRule(props);
			if (old != null) rules.RemoveAll(r => r.ToLine() == old.ToLine());
			if (rule != null) rules.Insert(0, rule);
			if (rules.Count == 0) props.Remove(WorldDirector.IslandRulesKey);
			else props[WorldDirector.IslandRulesKey] = IntroRule.ToLines(rules);
		}

		public static void Close()
		{
			if (instance == null) return;
			DropList.Close();
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		/// <summary>Writes a quest into the island being edited (the tests use it too).</summary>
		public static void Apply(IslandQuest q)
		{
			q.To(DynamicIslands.currentIslandProps);
			EditorUI.RefreshIsland();
		}

		void Save()
		{
			Keep();
			foreach (IslandQuest q in working.Values) q.Steps.RemoveAll(s => s.Type == "reach" && s.Target.Trim().Length == 0 && s.Text.Trim().Length == 0); // ("go to" nowhere)
			KeepBring();
			IslandQuest main;
			if (!working.TryGetValue(0, out main)) main = new IslandQuest();
			// (the further quests in order, the empty ones left out: "Quest 2" ... as many as have steps)
			List<IslandQuest> more = working.Where(kv => kv.Key > 0 && kv.Value.Exists).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
			IslandSettingsUndo.Change(() =>
			{
				SetQuestBringRule(DynamicIslands.currentIslandProps, main.Exists && bring != null && bring.WhatArg.Trim().Length > 0 ? bring : null);
				Apply(main);
				for (int i = 1; i < IslandQuest.MaxQuests; i++)
					(i - 1 < more.Count ? more[i - 1] : new IslandQuest()).To(DynamicIslands.currentIslandProps, i);
				EditorUI.RefreshIsland();
			});
			DynamicIslands.Notify(main.Exists ? "Quest \"" + main.ShownTitle + "\" with " + main.Steps.Count + " step(s)" + (more.Count > 0 ? " and " + more.Count + " more quest(s)" : "") + (bring != null && bring.WhatArg.Length > 0 ? ", bringing " + bring.DescribeWhat() + " when done," : "") +
				" saved with the island (Ctrl+S)" : more.Count > 0 ? more.Count + " quest(s) saved with the island (Ctrl+S); no main quest" : "The island has no quest now");
			Close();
		}

		/// <summary>
		/// "Remove quest": every quest of the island as one undo step. (It wrote the shown quest, emptied, over quest 0: on
		/// the Quest 2 tab it took the main quest away and left Quest 2, and Ctrl+Z couldn't bring it back.)
		/// </summary>
		void RemoveAll()
		{
			IslandSettingsUndo.Change(() =>
			{
				SetQuestBringRule(DynamicIslands.currentIslandProps, null);
				for (int i = 0; i < IslandQuest.MaxQuests; i++) new IslandQuest().To(DynamicIslands.currentIslandProps, i);
				EditorUI.RefreshIsland();
			});
			DynamicIslands.Notify("The island has no quest now");
			Close();
		}

		void Update()
		{
			if (ItemPickerWindow.IsOpen || ChoiceWindow.IsOpen || DropList.Busy || EditorInput.SubWindowJustClosed) return;
			if (pickingReward) { pickingReward = false; ShowReward(); }
			EditorInput.IsTyping = fields.Any(f => f != null && f.isFocused);
			if (EscKey.Down(this)) { if (Unsaved) EscKey.AskUnsaved("The quest", Save, Close); else Close(); }
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "QUEST EDITOR", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Label(head, "Players see the quest when they come to the island; steps are done in order", 12, UIKit.TextMuted, TextAnchor.MiddleRight);
			// (the island's quests: the main one and up to eight more - LM4)
			questTabs = UIKit.Row(panel, 26f, 6f, "Quests");

			// (each field says what it is, also once it's filled in)
			RectTransform top = UIKit.Row(panel, 30f, 8f, "Top");
			UIKit.Size(UIKit.Label(top, "Title", 13, UIKit.TextMuted).gameObject, 96);
			titleField = UIKit.Field(top, "Quest title (e.g. The lost captain)", "", 30f, "Shown at the top of the quest");
			titleField.characterLimit = 48;
			fields.Add(titleField);
			RectTransform introRow = UIKit.Row(panel, 30f, 8f, "Intro");
			UIKit.Size(UIKit.Label(introRow, "Introduction", 13, UIKit.TextMuted).gameObject, 96);
			introField = UIKit.Field(introRow, "Shown when players arrive (optional)", "", 30f, "A line or two to start the story");
			introField.characterLimit = 200;
			fields.Add(introField);

			RectTransform stepsGroup = UIKit.Group(panel, "Steps");
			stepsRoot = UIKit.Rect("Steps", stepsGroup);
			UIKit.Vertical(stepsRoot.gameObject, 4f, new RectOffset(0, 0, 0, 0));
			RectTransform addRow = UIKit.Row(stepsGroup, 26f, 6f, "Add");
			UIKit.Button(addRow, "+ Add a step", () => { if (quest.Steps.Count < MaxSteps) { Keep(); quest.Steps.Add(new IslandQuest.Step()); ShowSteps(); } }, "Another step (up to " + MaxSteps + ")", 140, 26f, 12);
			namesText = UIKit.Label(addRow, "", 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Names");
			namesText.verticalOverflow = VerticalWrapMode.Truncate;

			RectTransform reward = UIKit.Group(panel, "Reward and ending");
			RectTransform rewardRow = UIKit.Row(reward, 26f, 6f, "Reward");
			rewardText = UIKit.Fit(UIKit.Label(rewardRow, "", 12, UIKit.TextColor), 8);
			UIKit.Button(rewardRow, "Choose reward...", () =>
			{
				pickingReward = true;
				ItemPickerWindow.OpenFor(() => quest.Reward, v => quest.Reward = v);
			}, "Items every player near the island gets when the quest is done", 140, 26f, 12);
			UIKit.Button(rewardRow, "None", () => { quest.Reward = ""; ShowReward(); }, "No reward", 60, 26f, 12);
			RectTransform doneRow = UIKit.Row(reward, 30f, 8f, "Done");
			UIKit.Size(UIKit.Label(doneRow, "When done", 13, UIKit.TextMuted).gameObject, 96);
			doneField = UIKit.Field(doneRow, "Message when the quest is done (optional)", "", 30f, "Shown with \"Quest complete\"");
			doneField.characterLimit = 200;
			fields.Add(doneField);

			// A new island when the quest is done (the island's own rule; works in any world, and for every player)
			RectTransform next = UIKit.Group(panel, "When the quest is done, bring a new island");
			bringGroup = next;
			RectTransform kindRow = UIKit.Row(next, 26f, 6f, "Kind");
			bringKindButton = DropList.Make(kindRow, "Drop_BringKind", new List<DropList.Option>
			{
				new DropList.Option("nothing", "Nothing", "The quest brings no island"),
				new DropList.Option("island", "A saved island:", "One of your saved islands appears near this one"),
				new DropList.Option("type", "A new island:", "A new island of a map type (a camp, a wreck...) is made for the world"),
			}, "nothing", kind =>
			{
				KeepBring();
				if (kind == "nothing") bring = null;
				else
				{
					if (bring == null) bring = new IntroRule { Id = BringRuleId, When = "quest", WhenRef = IntroRule.Self, Where = "near", WhereRef = IntroRule.Self, Distance = 600f, Direction = "any" };
					bring.What = kind;
					bring.WhatArg = kind == "type" ? "random" : "";
				}
				ShowBring();
			}, 150, "What the quest brings when it's done: nothing, a saved island, or a new island of a map type (generated)", 26f, 12);
			bringWhatButton = UIKit.Button(kindRow, "", PickBring, "Which island (click to choose)", -1, 26f, 12);
			bringDetails = UIKit.Rect("Details", next);
			UIKit.Vertical(bringDetails.gameObject, 6f, new RectOffset(0, 0, 0, 0));
			RectTransform whereRow = UIKit.Row(bringDetails, 26f, 6f, "Where");
			bringDistField = UIKit.Field(whereRow, "600", "", 26f, "How far from this island it appears (metres, centre to centre; at least clear of both islands)");
			UIKit.Size(bringDistField.gameObject, 70, 26);
			bringDistField.contentType = InputField.ContentType.IntegerNumber;
			bringDistField.characterLimit = 4;
			fields.Add(bringDistField);
			UIKit.Size(UIKit.Label(whereRow, "m", 13, UIKit.TextMuted).gameObject, 18);
			bringDirButton = DropList.Make(whereRow, "Drop_BringDir", IntroRule.Directions.Select(d => new DropList.Option(d, d == "any" ? "any direction" : d, d == "any" ? "Wherever there's room" : "")).ToList(), "any", d =>
			{
				KeepBring();
				if (bring == null) return;
				bring.Direction = d;
				ShowBring();
			}, 130, "Which way from this island (any = wherever there's room)", 26f, 12);
			UIKit.Label(whereRow, "of this island (kept clear of Raft's islands)", 12, UIKit.TextMuted);
			RectTransform tellRow = UIKit.Row(bringDetails, 28f, 6f, "Tell");
			UIKit.Size(UIKit.Label(tellRow, "Message", 12, UIKit.TextMuted).gameObject, 60);
			bringMessageField = UIKit.Field(tellRow, "To every player when it appears (e.g. The map points to an island in the north...)", "", 28f, "Shown with how far and which way the new island is");
			bringMessageField.characterLimit = 160;
			fields.Add(bringMessageField);
			UIKit.Size(UIKit.Label(tellRow, "On the Receiver", 12, UIKit.TextMuted, TextAnchor.MiddleRight).gameObject, 104);
			bringLabelField = UIKit.Field(tellRow, "name (optional)", "", 28f, "The new island's green dot on Raft's Receiver shows this name");
			UIKit.Size(bringLabelField.gameObject, 150, 28);
			bringLabelField.characterLimit = 18;
			fields.Add(bringLabelField);

			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Button(buttons, "Remove quest", RemoveAll, "Delete the quest from the island", 130, 34);
			UIKit.Size(UIKit.Label(buttons, "", 12, UIKit.TextMuted).gameObject, -1, -1, 1);
			Button save = UIKit.Button(buttons, "Save", Save, "Keep the quest (save the island to keep it for good)", 110, 34);
			UIKit.Primary(save);
			UIKit.Button(buttons, "Cancel", Close, "Close without changing the quest", 110, 34);
		}

		static readonly Dictionary<string, string> TypeLabels = new Dictionary<string, string>
		{
			{ "reach", "Go to" }, { "read", "Read" }, { "open", "Open" }, { "kill", "Defeat" }, { "catch", "Catch" }, { "collect", "Collect" }, { "pages", "Find pages" }, { "blueprint", "Blueprint" },
		};

		/// <summary>What each kind of step asks (the step list's lines).</summary>
		static readonly Dictionary<string, string> TypeHints = new Dictionary<string, string>
		{
			{ "reach", "Walk into a trigger zone (by its name)" }, { "read", "Read a note (by its title)" },
			{ "open", "Open a chest (by its note title; empty = any chest)" }, { "kill", "Defeat a number of animals of a kind (e.g. Warthog)" },
			{ "catch", "Catch a number of animals with Raft's net launcher (e.g. Llama)" }, { "collect", "Have a number of a story item (the journal counts them)" },
			{ "pages", "Find a number of journal pages: notes read on this island (or \"all\": anywhere)" },
			{ "blueprint", "Find one of Raft's blueprints: picked up (and carried) or already learned (e.g. Blueprint_Fueltank)" },
		};

		static readonly Dictionary<string, string> TargetHints = new Dictionary<string, string>
		{
			{ "reach", "trigger zone name" }, { "read", "note title" }, { "open", "chest's note title (empty = any chest; not the abandoned raft crate)" },
			{ "kill", "creature (e.g. Warthog; empty = any)" }, { "catch", "animal (e.g. Llama; empty = any)" },
			{ "collect", "story item (\u2026 to choose)" }, { "pages", "empty = this island, all = any island" },
			{ "blueprint", "Blueprint_ name (\u25BE lists Raft's; empty = any story blueprint)" },
		};

		/// <summary>Takes what was typed into the step rows before they are rebuilt.</summary>
		void ShowSteps()
		{
			foreach (Transform child in stepsRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			fields.RemoveAll(f => f == null || f.transform.IsChildOf(stepsRoot));
			for (int i = 0; i < quest.Steps.Count; i++)
			{
				int index = i;
				IslandQuest.Step s = quest.Steps[i];
				RectTransform row = UIKit.Row(stepsRoot, 28f, 4f, "Step");
				UIKit.Size(UIKit.Label(row, (i + 1) + ".", 13, UIKit.TextMuted, TextAnchor.MiddleRight).gameObject, 22);
				DropList.Make(row, "Drop_StepType", IslandQuest.Types.Select(t => new DropList.Option(t, TypeLabels[t], TypeHints[t])).ToList(), s.Type, v =>
				{
					s.Type = v;
					ShowSteps();
				}, 104, "What the player must do: go to, read, open, defeat, catch, collect story items, find journal pages", 28f, 12);
				InputField target = UIKit.Field(row, TargetHints[s.Type], s.Target, 28f, "Must match a name on the island exactly: \u25BE lists the ones it has");
				UIKit.Size(target.gameObject, 196, 28);
				target.onEndEdit.AddListener(v => s.Target = v.Trim());
				fields.Add(target);
				List<DropList.Option> names = NamesFor(s.Type);
				if (names.Count > 0)
				{
					// (a small \u25BE: the island's zones, notes, chests or creatures that this kind of step can point at)
					Button pick = DropList.Make(row, "Pick_Target", names, s.Target, v => { Keep(); s.Target = v; ShowSteps(); }, 26, "Pick one of the names on this island", 28f, 11);
					UIKit.LabelOf(pick).text = "";
				}
				if (s.Type == "collect") UIKit.Button(row, "\u2026", () => { Keep(); ItemPickerWindow.PickOne(v => { s.Target = v; ShowSteps(); }, true); }, "Choose one of the island's story items", 28, 28f, 12);
				if (s.Type == "kill" || s.Type == "catch" || IslandQuest.Counted(s.Type))
				{
					InputField count = UIKit.Field(row, "1", s.Count.ToString(), 28f, "How many");
					UIKit.Size(count.gameObject, 44, 28);
					count.contentType = InputField.ContentType.IntegerNumber;
					count.characterLimit = 2;
					count.onEndEdit.AddListener(v => { int n; s.Count = int.TryParse(v, out n) ? Mathf.Clamp(n, 1, 99) : 1; });
					fields.Add(count);
				}
				InputField text = UIKit.Field(row, s.Describe(), s.Text, 28f, "What players read for this step (empty: made from the step)");
				text.characterLimit = 80;
				text.onEndEdit.AddListener(v => s.Text = v.Trim());
				fields.Add(text);
				UIKit.Button(row, "\u25B2", () => { if (index > 0) { Keep(); quest.Steps.Reverse(index - 1, 2); ShowSteps(); } }, "Earlier", 26, 28f, 11);
				UIKit.Button(row, "\u25BC", () => { if (index < quest.Steps.Count - 1) { Keep(); quest.Steps.Reverse(index, 2); ShowSteps(); } }, "Later", 26, 28f, 11);
				Button del = UIKit.Button(row, "\u00D7", () => { Keep(); quest.Steps.RemoveAt(index); ShowSteps(); }, "Remove this step", 26, 28f, 12);
				UIKit.DangerButton(del);
			}
		}

		/// <summary>Takes what was typed into the "bring" fields.</summary>
		void KeepBring()
		{
			if (bring == null) return;
			float d;
			if (float.TryParse(bringDistField.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) bring.Distance = Mathf.Clamp(d, 50f, 5000f);
			bring.Message = bringMessageField.text.Trim();
			bring.Label = bringLabelField.text.Trim();
		}

		void ShowBring()
		{
			string kind = bring == null ? "nothing" : bring.What;
			UIKit.LabelOf(bringKindButton).text = kind == "nothing" ? "Nothing" : kind == "island" ? "A saved island:" : "A new island:";
			SetDropValue(bringKindButton, kind);
			bringWhatButton.gameObject.SetActive(bring != null);
			bringDetails.gameObject.SetActive(bring != null);
			if (bring == null) return;
			MapType t = bring.What == "type" ? MapTypes.Get(bring.WhatArg) : null;
			UIKit.LabelOf(bringWhatButton).text = bring.WhatArg.Length == 0 ? "<i>Choose an island...</i>" : t != null ? t.Label + " (generated)" : bring.WhatArg;
			bringDistField.text = bring.Distance.ToString("0");
			UIKit.LabelOf(bringDirButton).text = bring.Direction == "any" ? "any direction" : bring.Direction;
			SetDropValue(bringDirButton, bring.Direction);
			bringMessageField.text = bring.Message;
			bringLabelField.text = bring.Label;
		}

		void PickBring()
		{
			if (bring == null) return;
			KeepBring();
			if (bring.What == "type") ChoiceWindow.Open("Map type", ChoiceWindow.Types(), v => { if (bring != null) { bring.WhatArg = v; ShowBring(); } });
			else ChoiceWindow.OpenIslands("Bring which island", () => ChoiceWindow.Islands().Where(c => !c.Value.Equals(DynamicIslands.currentIslandName, StringComparison.OrdinalIgnoreCase)),
				v => { if (bring != null) { bring.WhatArg = v; ShowBring(); } });
		}

		void ShowReward()
		{
			List<KeyValuePair<string, int>> items = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, quest.Reward } });
			rewardText.text = items.Count == 0 ? "<i>No reward</i>" : "Reward: " + string.Join(", ", items.Select(l => ContentCatalog.ItemLabel(l.Key) + " \u00D7" + l.Value).ToArray());
		}

		/// <summary>The names on the island a step of this kind can point at (the \u25BE list beside its name).</summary>
		static List<DropList.Option> NamesFor(string type)
		{
			GameObject placed = GameObject.Find("PlacedObjects");
			List<EditorGameObject> all = placed != null ? placed.GetComponentsInChildren<EditorGameObject>().ToList() : new List<EditorGameObject>();
			IEnumerable<string> names;
			switch (type)
			{
				case "reach": names = ContentCatalog.ZoneIdsInEditor(); break;
				case "read": names = all.Where(e => ObjectProps.IsNote(e.GameObjectName, e.Props)).Select(e => ObjectProps.Get(e.Props, ObjectProps.NoteTitle)); break;
				case "open": names = all.Where(e => ObjectProps.IsLoot(e.GameObjectName, e.Props)).Select(e => ObjectProps.Get(e.Props, ObjectProps.NoteTitle)); break;
				case "kill": names = all.Select(e => ContentCatalog.CreatureOf(e.GameObjectName)).Where(k => k != null).Select(k => k.Label); break;
				case "catch": names = all.Select(e => ContentCatalog.CreatureOf(e.GameObjectName)).Where(k => k != null && k.Category == ContentCatalog.CatchableCategory).Select(k => k.Label); break;
				case "pages": return new List<DropList.Option> { new DropList.Option("", "This island", "Pages from this island's notes"), new DropList.Option("all", "all", "Pages from any island") };
				case "blueprint": return BlueprintSteps.All().Select(b => new DropList.Option(b, PlanChecker.Pretty(b), b)).ToList();
				default: return new List<DropList.Option>();
			}
			return names.Where(n => n != null && n.Trim().Length > 0).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => new DropList.Option(n, n)).ToList();
		}

		/// <summary>A drop-down shows this value as the chosen one (after the window filled it from the island).</summary>
		static void SetDropValue(Button b, string value)
		{
			DropdownButton d = b != null ? b.GetComponent<DropdownButton>() : null;
			if (d != null) d.Value = value;
		}

		/// <summary>The names on the island that steps can point at.</summary>
		void ShowNames()
		{
			GameObject placed = GameObject.Find("PlacedObjects");
			List<EditorGameObject> all = placed != null ? placed.GetComponentsInChildren<EditorGameObject>().ToList() : new List<EditorGameObject>();
			var zones = ContentCatalog.ZoneIdsInEditor();
			var notes = all.Where(e => ObjectProps.IsNote(e.GameObjectName, e.Props)).Select(e => ObjectProps.Get(e.Props, ObjectProps.NoteTitle)).Where(t => t.Length > 0).Distinct().ToList();
			var creatures = all.Select(e => ContentCatalog.CreatureOf(e.GameObjectName)).Where(k => k != null).Select(k => k.Label).Distinct().ToList();
			var parts = new List<string>();
			if (zones.Count > 0) parts.Add("Zones: " + string.Join(", ", zones.ToArray()));
			if (notes.Count > 0) parts.Add("Notes: " + string.Join(", ", notes.Take(6).Select(n => "\"" + n + "\"").ToArray()));
			if (creatures.Count > 0) parts.Add("Creatures: " + string.Join(", ", creatures.ToArray()));
			namesText.text = parts.Count > 0 ? string.Join("  \u00B7  ", parts.ToArray()) : "Place trigger zones, notes, chests or creatures first: steps point at them by name.";
		}
	}
}
