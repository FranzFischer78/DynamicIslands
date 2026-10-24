using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The island's story items (Island tab, "Story items..."): a name, a picture (Raft's quest items or any Raft item),
	/// a description, and the id that chests, "give" actions and checks use ("story:&lt;id&gt;"). Below, story sets: ready
	/// pieces of a story the editor places around the view in one undo step - a locked door with its key in a chest,
	/// or a trail of notes that leads to a hidden chest.
	/// </summary>
	public class StoryItemsWindow : MonoBehaviour
	{
		static StoryItemsWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		List<StoryItemDef> defs = new List<StoryItemDef>();
		RectTransform body;
		readonly List<InputField> fields = new List<InputField>();
		int pickingIconFor = -1;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("StoryItemsWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
			instance = blocker.AddComponent<StoryItemsWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			instance.defs = StoryItems.Of(DynamicIslands.currentIslandProps).Select(d => d.Copy()).ToList();
			instance.pickingIconFor = -1;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.Rebuild();
			instance.savedPrint = StoryItems.Text(instance.defs);
		}

		/// <summary>What Esc compares: the items as they would be saved.</summary>
		string savedPrint;

		/// <summary>The items differ from the island's (Esc asks before it closes).</summary>
		public static bool Unsaved
		{
			get
			{
				if (!IsOpen) return false;
				instance.Keep();
				return StoryItems.Text(instance.defs) != instance.savedPrint;
			}
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		void Update()
		{
			if (ItemPickerWindow.IsOpen || ChoiceWindow.IsOpen) return;
			EditorInput.IsTyping = fields.Any(f => f != null && f.isFocused);
			if (EscKey.Down(this)) { if (pickingIconFor >= 0) { pickingIconFor = -1; Rebuild(); } else if (Unsaved) EscKey.AskUnsaved("The story items", Save, Close); else Close(); }
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		#region Building

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "STORY ITEMS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Label(head, "Keys, map pieces, logs... the crew keeps them: in Raft's notebook (main story) or the journal (side quests)", 12, UIKit.TextMuted, TextAnchor.MiddleRight);
			RectTransform box = UIKit.Rect("Body", panel);
			UIKit.Size(box.gameObject, -1, 560);
			ScrollRect scroll;
			body = UIKit.ScrollList(box, out scroll, 8f);
			UIKit.Stretch((RectTransform)scroll.transform);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Size(UIKit.Label(buttons, "Put them in chests or \"give\" actions (the item picker lists them), and ask for them in an event's \"Only if...\"", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic).gameObject, -1, -1, 1);
			Button save = UIKit.Button(buttons, "Save", Save, "Keep the story items (save the island, Ctrl+S)", 110, 34);
			UIKit.Primary(save);
			UIKit.Button(buttons, "Cancel", Close, "Close without changing anything (Esc)", 110, 34);
		}

		void Rebuild()
		{
			foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			fields.Clear();
			RectTransform g = UIKit.Group(body, "This island's story items");
			for (int i = 0; i < defs.Count; i++)
			{
				ItemRow(g, i);
				if (i == pickingIconFor) IconGrid(g, i);
			}
			if (defs.Count == 0) UIKit.Label(g, "<i>None yet. A story item can be a key for a locked door, a piece of a map, a letter to bring somewhere...</i>", 12, UIKit.TextMuted);
			UIKit.Button(g, "+ Add a story item", () =>
			{
				Keep();
				var d = new StoryItemDef { Name = "Old key", Icon = StoryItems.QuestIcon + "Vasagatan_GreenKey", Description = "" };
				d.Id = StoryItems.NewId(defs, d.Name);
				defs.Add(d);
				Rebuild();
			}, "Another story item", 170, 26f, 12);
			SetsGroup();
		}

		void ItemRow(Transform g, int index)
		{
			StoryItemDef d = defs[index];
			RectTransform row = UIKit.Row(g, 40f, 6f, "Item");
			Button icon = UIKit.Button(row, "", () => { Keep(); pickingIconFor = pickingIconFor == index ? -1 : index; Rebuild(); }, "Its picture: click to choose one", 40, 40f);
			UIKit.Slot(icon);
			RectTransform pic = UIKit.Rect("Picture", icon.transform);
			UIKit.Stretch(pic, 4, 4, 4, 4);
			Image img = pic.gameObject.AddComponent<Image>();
			img.sprite = StoryItems.IconSprite(d.Icon);
			img.preserveAspect = true; img.raycastTarget = false;
			if (img.sprite == null) img.color = new Color(0, 0, 0, 0.2f);
			InputField name = Field(row, "Name players see", d.Name, 170, "The item's name in the journal and messages", v => d.Name = v.Trim());
			name.characterLimit = 40;
			InputField desc = Field(row, "What it is (shown in the journal)", d.Description, -1, "A line or two about it", v => d.Description = v.Trim());
			desc.characterLimit = 200;
			Text id = UIKit.Label(row, "story:" + d.Id, 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Id");
			UIKit.Size(id.gameObject, 150);
			UIKit.Hint(id.gameObject, "What chests, actions and checks call it");
			// (T8: a Raft item players can take it out of the journal as - "Take out" in the journal; none: a journal item only)
			bool hasRaft = (d.RaftItem ?? "").Trim().Length > 0;
			UIKit.Button(row, hasRaft ? "As " + ContentCatalog.ItemLabel(d.RaftItem.Trim()) : "Raft item...", () => { Keep(); ItemPickerWindow.PickOne(item => { defs[index].RaftItem = item; Rebuild(); }); }, "Optional: one of Raft's own items players can take it out of the journal as (Take out in the journal puts that item in their inventory; the crew has one less). None: it stays a journal item.", 120, 28f, 12);
			if (hasRaft) UIKit.Button(row, "-", () => { Keep(); defs[index].RaftItem = ""; Rebuild(); }, "Not a Raft item: it stays in the journal", 24, 28f, 12);
			Button del = UIKit.Button(row, "\u00D7", () => { Keep(); defs.RemoveAt(index); pickingIconFor = -1; Rebuild(); }, "Remove this story item (what uses it stops working)", 28, 28f, 12);
			UIKit.DangerButton(del);
		}

		/// <summary>The pictures to choose from: Raft's quest items, or any Raft item.</summary>
		void IconGrid(Transform g, int index)
		{
			RectTransform wrap = UIKit.Rect("Icons", g);
			UIKit.Background(wrap.gameObject, UIKit.GroupBg, 6);
			UIKit.Vertical(wrap.gameObject, 6f, new RectOffset(8, 8, 6, 8));
			RectTransform grid = UIKit.Rect("Grid", wrap);
			var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
			gl.cellSize = new Vector2(44f, 44f);
			gl.spacing = new Vector2(4f, 4f);
			gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			gl.constraintCount = 18;
			foreach (SO_QuestItem q in StoryItems.QuestItems)
			{
				string icon = StoryItems.QuestIcon + q.questItemType;
				Button b = UIKit.Button(grid, "", () => { defs[index].Icon = icon; pickingIconFor = -1; Rebuild(); }, q.questItemType.ToString().Replace('_', ' '), 44, 44f);
				UIKit.Slot(b);
				RectTransform pic = UIKit.Rect("Picture", b.transform);
				UIKit.Stretch(pic, 4, 4, 4, 4);
				Image img = pic.gameObject.AddComponent<Image>();
				img.sprite = q.itemImage; img.preserveAspect = true; img.raycastTarget = false;
			}
			RectTransform more = UIKit.Row(wrap, 26f, 6f, "More");
			UIKit.Label(more, "Raft's quest items above, or any of Raft's items:", 12, UIKit.TextMuted);
			UIKit.Button(more, "Raft items...", () => ItemPickerWindow.PickOne(item => { defs[index].Icon = item; pickingIconFor = -1; Rebuild(); }), "Use the picture of one of Raft's items", 130, 26f, 12);
		}

		void SetsGroup()
		{
			RectTransform g = UIKit.Group(body, "Story sets");
			UIKit.Label(g, "Ready pieces of a story, placed around the middle of the view (one undo step). Change them afterwards like any objects.", 12, UIKit.TextMuted);
			RectTransform row = UIKit.Row(g, 30f, 8f, "Sets");
			UIKit.Button(row, "Locked door and its key", () => Place(StorySets.LockedDoor), "A door that only opens for the crew with the key, and the key in a chest nearby (with a note)", -1, 30f, 13);
			UIKit.Button(row, "A trail of notes", () => Place(StorySets.NoteTrail), "Three notes that lead to a hidden chest with a story item; they go into the journal", -1, 30f, 13);
			UIKit.Button(row, "A treasure map", () => Place(StorySets.TreasureMap), "A message in a bottle gives a treasure map; walking to the spot with it digs up a buried chest", -1, 30f, 13);
			UIKit.Button(row, "A locked chest", () => Place(StorySets.LockedChest), "A chest that only opens with a small key, and driftwood nearby that hides the key", -1, 30f, 13);
		}

		void Place(Func<List<StoryItemDef>, Vector3, List<GameObject>> set)
		{
			Keep();
			Vector3 at = StorySets.ViewCentre();
			List<GameObject> placed = set(defs, at);
			if (placed.Count == 0) { DynamicIslands.Notify("Could not place the story set here", true); return; }
			// (its story items in the same undo step as its objects: the first Ctrl+Z took only the key away and left the door)
			SaveDefs(true);
			Close();
			DynamicIslands.Notify("Story set placed: " + placed.Count + " objects (Ctrl+Z removes them)");
		}

		InputField Field(Transform row, string placeholder, string text, float width, string hint, Action<string> set)
		{
			InputField f = UIKit.Field(row, placeholder, text, 28f, hint);
			if (width > 0) UIKit.Size(f.gameObject, width, 28);
			((Text)f.placeholder).fontSize = 13;
			f.textComponent.fontSize = 13;
			f.onEndEdit.AddListener(v => set(v));
			fields.Add(f);
			return f;
		}

		void Keep()
		{
			foreach (InputField f in fields.Where(f => f != null && f.isFocused).ToList()) f.onEndEdit.Invoke(f.text);
		}

		#endregion

		void SaveDefs(bool withLastStep = false)
		{
			string text = StoryItems.Text(defs);
			Action change = () =>
			{
				if (text.Length == 0) DynamicIslands.currentIslandProps.Remove(StoryItems.Key);
				else DynamicIslands.currentIslandProps[StoryItems.Key] = text;
			};
			if (!withLastStep) { IslandSettingsUndo.Change(change); return; }
			CommandUndoRedo.ICommand step = IslandSettingsUndo.Record(change);
			if (step != null) CommandUndoRedo.UndoRedoManager.AppendToLast(step);
		}

		void Save()
		{
			Keep();
			SaveDefs();
			EditorUI.RefreshIsland();
			DynamicIslands.Notify(defs.Count + " story item(s) kept (save the island, Ctrl+S)");
			Close();
		}

		/// <summary>Presses Save (tests).</summary>
		public static void SaveNow() { if (IsOpen) instance.Save(); }

		/// <summary>The story items being edited (tests).</summary>
		public static List<StoryItemDef> Editing { get { return instance != null ? instance.defs : null; } }
	}

	/// <summary>Ready pieces of a story (the "story item generator"): objects with their settings, placed in the editor.</summary>
	public static class StorySets
	{
		/// <summary>Where the editor camera looks at the ground (or the build area's middle).</summary>
		public static Vector3 ViewCentre()
		{
			Terrain t = terraineditor.terrain;
			Camera cam = Camera.main;
			RaycastHit hit;
			if (cam != null && t != null && t.GetComponent<TerrainCollider>() != null && t.GetComponent<TerrainCollider>().Raycast(new Ray(cam.transform.position, cam.transform.forward), out hit, 2000f)) return hit.point;
			if (t == null) return Vector3.zero;
			Vector3 mid = t.transform.position + t.terrainData.size / 2f;
			return Ground(mid);
		}

		static Vector3 Ground(Vector3 p)
		{
			Terrain t = terraineditor.terrain;
			if (t != null) p.y = t.transform.position.y + t.SampleHeight(p);
			return p;
		}

		static GameObject Put(List<GameObject> list, string name, Vector3 pos, float yaw, Dictionary<string, string> props)
		{
			// (the object limit, as for placing by hand: a set that can't be placed whole is taken away again)
			if (!ObjectLimit.Allow(1)) return null;
			Transform root = GameObject.Find("PlacedObjects") != null ? GameObject.Find("PlacedObjects").transform : null;
			GameObject go = PlaceableCatalog.Spawn(name, root);
			if (go == null) { Debug.LogWarning("[CUSTOM ISLANDS] Story set: no object " + name); return null; }
			go.transform.position = Ground(pos);
			go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
			foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = true;
			Dictionary<string, string> p = ObjectProps.Defaults(name);
			foreach (var kv in props) p[kv.Key] = kv.Value;
			EditorGameObject.Attach(go, name, p);
			list.Add(go);
			return go;
		}

		static Dictionary<string, string> P(params string[] kv)
		{
			var d = new Dictionary<string, string>();
			for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
			return d;
		}

		static string FreeName(string baseName)
		{
			GameObject placed = GameObject.Find("PlacedObjects");
			var names = placed == null ? new HashSet<string>() : new HashSet<string>(placed.GetComponentsInChildren<EditorGameObject>(true).Select(e => ObjectProps.Get(e.Props, BehaviourProps.Name)), StringComparer.OrdinalIgnoreCase);
			string n = baseName;
			for (int i = 2; names.Contains(n); i++) n = baseName + "-" + i;
			return n;
		}

		static string DoorObject()
		{
			foreach (string n in new[] { "Block_Wall_Wood", "Block_Wall_Thatch", "Block_Wall_Door_Wood" })
				if (PlaceableCatalog.Names.Contains(n)) return n;
			return PlaceableCatalog.Names.FirstOrDefault(n => n.StartsWith("Block_Wall"));
		}

		/// <summary>A door that opens (and closes) only for a crew with the key; the key is in a chest 12 m away, with a note on it.</summary>
		public static List<GameObject> LockedDoor(List<StoryItemDef> defs, Vector3 at)
		{
			var list = new List<GameObject>();
			var key = new StoryItemDef { Name = "Rusty key", Icon = StoryItems.QuestIcon + "Caravan_KeyInfirmary", Description = "An old key. It must open a door somewhere on this island." };
			key.Id = StoryItems.NewId(defs, key.Name);
			string door = FreeName("locked-door");
			string doorObject = DoorObject();
			if (doorObject == null) return list;
			Put(list, doorObject, at, 0f, P(BehaviourProps.Name, door, BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "1.2", BehaviourProps.Use, "Open the door",
				BehaviourProps.CheckKey("use"), "has|" + StoryItems.Ref(key.Id) + "|1",
				BehaviourProps.ElseKey("use"), "message||It's locked. Maybe there's a key somewhere...",
				BehaviourProps.EventKey("use"), "switch||\nmessage||The " + key.Name.ToLowerInvariant() + " turns in the lock."));
			Put(list, "Loot_ChestSmall", at + new Vector3(12f, 0f, 6f), 200f, P(ObjectProps.LootItems, StoryItems.Ref(key.Id) + "*1", ObjectProps.NoteTitle, "Chest"));
			Put(list, "Note_Paper", at + new Vector3(10.5f, 0f, 5f), 30f, P(ObjectProps.NoteTitle, "Note", ObjectProps.NoteText, "I locked the door and hid the key in the chest. Nobody gets in without it."));
			if (list.Count < 3) { foreach (GameObject go in list) UnityEngine.Object.Destroy(go); return new List<GameObject>(); }
			defs.Add(key);
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(list, true));
			return list;
		}

		/// <summary>
		/// A message in a bottle that gives the treasure map; 25 m away a hidden chest and a zone around it: a player
		/// with the map who walks in digs the chest up (the zone checks for the map and fires every time until then).
		/// </summary>
		public static List<GameObject> TreasureMap(List<StoryItemDef> defs, Vector3 at)
		{
			var list = new List<GameObject>();
			var map = new StoryItemDef { Name = "Treasure map", Icon = StoryItems.QuestIcon + "Vasagatan_FourDigitCode", Description = "A torn map with an X on it. The X is on this island." };
			map.Id = StoryItems.NewId(defs, map.Name);
			string chest = FreeName("buried-treasure");
			Vector3 spot = at + new Vector3(20f, 0f, -15f);
			Put(list, "Note_Bottle", at, 0f, P(ObjectProps.NoteTitle, "A message in a bottle", ObjectProps.NoteText, "Rolled up inside the bottle is a map. Someone marked a spot on this island with an X.",
				BehaviourProps.EventKey("read"), "give||" + StoryItems.Ref(map.Id) + "*1"));
			Put(list, "Loot_ChestSmall", spot, 160f, P(BehaviourProps.Name, chest, BehaviourProps.Hidden, "1", ObjectProps.LootItems, "Scrap*6;Nail*12;Plank*8", ObjectProps.NoteTitle, "Chest"));
			Put(list, ContentCatalog.TriggerZone, spot, 0f, P(ObjectProps.ZoneId, "x-marks-the-spot", ObjectProps.ZoneRadius, "5", ObjectProps.ZoneRepeat, "1",
				BehaviourProps.CheckKey("enter"), "has|" + StoryItems.Ref(map.Id) + "|1\n!state|" + chest + "|shown",
				BehaviourProps.EventKey("enter"), "show|" + chest + "|\nmessage||The map leads here: something is buried under the sand!"));
			if (list.Count < 3) { foreach (GameObject go in list) UnityEngine.Object.Destroy(go); return new List<GameObject>(); }
			defs.Add(map);
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(list, true));
			return list;
		}

		/// <summary>A chest that opens only with a small key (used up), and driftwood 10 m away that hides the key (while the crew hasn't got it).</summary>
		public static List<GameObject> LockedChest(List<StoryItemDef> defs, Vector3 at)
		{
			var list = new List<GameObject>();
			var key = new StoryItemDef { Name = "Small key", Icon = StoryItems.QuestIcon + "Vasagatan_BlueKey", Description = "A small key, the kind that opens a chest." };
			key.Id = StoryItems.NewId(defs, key.Name);
			Put(list, "Loot_Chest", at, 180f, P(ObjectProps.LootItems, "Scrap*5;Plank*10;Rope*4", ObjectProps.NoteTitle, "Chest",
				BehaviourProps.CheckKey("open"), "take|" + StoryItems.Ref(key.Id) + "|1", BehaviourProps.ElseKey("open"), "message||The chest is locked."));
			Put(list, "Log", at + new Vector3(10f, 0f, 4f), 70f, P(BehaviourProps.Use, "Search the driftwood",
				BehaviourProps.CheckKey("use"), "!has|" + StoryItems.Ref(key.Id) + "|1",
				BehaviourProps.EventKey("use"), "give||" + StoryItems.Ref(key.Id) + "*1\nmessage||Something glints between the branches: a small key!",
				BehaviourProps.ElseKey("use"), "message||Nothing else here."));
			if (list.Count < 2) { foreach (GameObject go in list) UnityEngine.Object.Destroy(go); return new List<GameObject>(); }
			defs.Add(key);
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(list, true));
			return list;
		}

		/// <summary>Three notes 15 m apart, each pointing to the next; the last one shows a hidden chest with a story item.</summary>
		public static List<GameObject> NoteTrail(List<StoryItemDef> defs, Vector3 at)
		{
			var list = new List<GameObject>();
			var log = new StoryItemDef { Name = "Captain's log", Icon = StoryItems.QuestIcon + "Vasagatan_Recorder", Description = "The last pages of the captain's log. Someone wanted it found." };
			log.Id = StoryItems.NewId(defs, log.Name);
			string chest = FreeName("hidden-chest");
			Put(list, "Note_Bottle", at, 0f, P(ObjectProps.NoteTitle, "A message in a bottle", ObjectProps.NoteText, "If you find this, walk towards the rising sun. I left more for you there."));
			Put(list, "Note_Paper", at + new Vector3(15f, 0f, 0f), 90f, P(ObjectProps.NoteTitle, "A torn page", ObjectProps.NoteText, "You're getting closer. Look to the north: an open book waits by the rocks."));
			Put(list, "Note_Book", at + new Vector3(15f, 0f, 15f), 180f, P(ObjectProps.NoteTitle, "The captain's diary", ObjectProps.NoteText, "Here it is. The chest behind me holds what is left of my log. Keep it safe.",
				BehaviourProps.EventKey("read"), "show|" + chest + "|\njournal|The captain's trail|Three notes led to a hidden chest behind the open book."));
			Put(list, "Loot_Chest", at + new Vector3(18f, 0f, 18f), 225f, P(BehaviourProps.Name, chest, BehaviourProps.Hidden, "1", ObjectProps.LootItems, StoryItems.Ref(log.Id) + "*1;Plank*6", ObjectProps.NoteTitle, "Chest"));
			if (list.Count < 4) { foreach (GameObject go in list) UnityEngine.Object.Destroy(go); return new List<GameObject>(); }
			defs.Add(log);
			CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(list, true));
			return list;
		}
	}
}
