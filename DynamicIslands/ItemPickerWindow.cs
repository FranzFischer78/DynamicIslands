using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Every item of Raft with its picture, to fill a chest: a search box and a grid; click an item to add it (again
	/// to add more). Opened from the inspector's Loot group. The chosen items show in the inspector at once.
	/// The island's story items ("story:&lt;id&gt;") come first. PickOne chooses a single item (a story item's picture).
	/// </summary>
	public class ItemPickerWindow : MonoBehaviour
	{
		static ItemPickerWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		EditorGameObject target;
		// What the window fills: a chest's loot, or anything else in the same "Item*n;..." form (a quest's reward)
		Func<string> getLoot;
		Action<string> setLoot;
		// Choosing one item (a story item's picture): called with it, and the window closes
		Action<string> pickOne;
		Text titleText;
		RectTransform storyGrid;
		bool withStory;
		InputField search;
		RectTransform grid;
		Text countText, lootText;
		readonly List<KeyValuePair<string, GameObject>> tiles = new List<KeyValuePair<string, GameObject>>();
		bool built;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("ItemPickerWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f); // modal
			instance = blocker.AddComponent<ItemPickerWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open(EditorGameObject target)
		{
			if (instance == null || target == null) return;
			instance.target = target;
			instance.getLoot = () => ObjectProps.Get(target.Props, ObjectProps.LootItems);
			instance.setLoot = null;
			instance.Show("ITEMS FOR THE CHEST", null);
		}

		public static void Close()
		{
			if (instance == null) return;
			if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			instance.gameObject.SetActive(false);
			instance.target = null;
			instance.pickOne = null;
			EditorInput.IsTyping = false;
			ObjectInspector.Refresh();
		}

		/// <summary>Fills a list of items that isn't an object's loot (a quest's reward): get and set use the "Item*n;..." form.</summary>
		public static void OpenFor(Func<string> get, Action<string> set)
		{
			if (instance == null) return;
			instance.target = null;
			instance.getLoot = get;
			instance.setLoot = set;
			instance.Show("ITEMS", null);
		}

		/// <summary>Chooses one of Raft's items (its unique name goes to picked; the window closes).</summary>
		public static void PickOne(Action<string> picked, bool storyItems = false)
		{
			if (instance == null) return;
			instance.target = null;
			instance.getLoot = null;
			instance.setLoot = null;
			instance.Show("CHOOSE AN ITEM", picked, storyItems);
		}

		void Show(string title, Action<string> picked, bool storyItems = true)
		{
			pickOne = picked;
			withStory = storyItems;
			titleText.text = title;
			gameObject.SetActive(true);
			transform.SetAsLastSibling();
			FillTiles();
			FillStoryTiles();
			search.text = "";
			Filter();
			lootText.text = "";
			ShowLoot();
			search.ActivateInputField();
		}

		/// <summary>The island's story items as tiles (not when choosing a picture).</summary>
		void FillStoryTiles()
		{
			foreach (Transform c in storyGrid) Destroy(c.gameObject);
			tiles.RemoveAll(t => t.Value == null || t.Value.transform.parent == storyGrid);
			if (!withStory) return;
			foreach (StoryItemDef d in StoryItems.Of(DynamicIslands.currentIslandProps))
				Tile(storyGrid, StoryItems.Ref(d.Id), d.ShownName + " (story)", StoryItems.IconSprite(d.Icon), "Story item '" + d.ShownName + "': the crew keeps it (Raft's notebook for the main story, else the journal)");
		}

		void Pick(string name)
		{
			if (pickOne != null) { Action<string> p = pickOne; Close(); p(name); return; }
			if (target != null) AddItem(target, name);
			else if (setLoot != null) setLoot(Add(getLoot != null ? getLoot() : "", name));
			ShowLoot();
		}

		/// <summary>One more of an item in an "Item*n;..." list.</summary>
		public static string Add(string lootText, string uniqueName, int amount = 1)
		{
			List<KeyValuePair<string, int>> loot = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, lootText } });
			int i = loot.FindIndex(l => l.Key == uniqueName);
			if (i >= 0) loot[i] = new KeyValuePair<string, int>(uniqueName, Mathf.Min(ObjectProps.MaxLootAmount, loot[i].Value + amount));
			else if (loot.Count < ObjectProps.MaxLootStacks) loot.Add(new KeyValuePair<string, int>(uniqueName, amount));
			return ObjectProps.LootText(loot);
		}

		/// <summary>Adds an item to a chest's loot (one more if it's there already) as an undo step.</summary>
		public static void AddItem(EditorGameObject target, string uniqueName, int amount = 1)
		{
			List<KeyValuePair<string, int>> loot = ObjectProps.Loot(target.Props);
			int i = loot.FindIndex(l => l.Key == uniqueName);
			if (i >= 0) loot[i] = new KeyValuePair<string, int>(uniqueName, Mathf.Min(ObjectProps.MaxLootAmount, loot[i].Value + amount));
			else if (loot.Count < ObjectProps.MaxLootStacks) loot.Add(new KeyValuePair<string, int>(uniqueName, amount));
			else { DynamicIslands.Notify("A chest holds at most " + ObjectProps.MaxLootStacks + " kinds of items", true); return; }
			PropsCommand.Change(target, ObjectProps.With(target.Props, ObjectProps.LootItems, ObjectProps.LootText(loot)), "loot.add");
		}

		void Update()
		{
			EditorInput.IsTyping = search.isFocused;
			if (EscKey.Down(this) || ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && !search.isFocused)) Close();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 0));

			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			titleText = UIKit.Label(head, "ITEMS FOR THE CHEST", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			countText = UIKit.Label(head, "", 12, UIKit.TextMuted, TextAnchor.MiddleRight);

			search = UIKit.Field(panel, "Search Raft's items (plank, scrap, potato...)", "", 30f, "Type part of an item's name");
			search.onValueChanged.AddListener(v => Filter());

			RectTransform listBox = UIKit.Rect("List", panel);
			UIKit.Size(listBox.gameObject, -1, 420);
			ScrollRect scroll;
			RectTransform content = UIKit.ScrollList(listBox, out scroll, 4f);
			UIKit.Stretch((RectTransform)scroll.transform);
			// The island's story items first (they change with the island: filled on every opening)
			storyGrid = UIKit.Rect("StoryGrid", content);
			var sg = storyGrid.gameObject.AddComponent<GridLayoutGroup>();
			sg.cellSize = new Vector2(80f, 92f);
			sg.spacing = new Vector2(5f, 5f);
			sg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			sg.constraintCount = 8;
			grid = UIKit.Rect("Grid", content);
			var g = grid.gameObject.AddComponent<GridLayoutGroup>();
			g.cellSize = new Vector2(80f, 92f);
			g.spacing = new Vector2(5f, 5f);
			g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			g.constraintCount = 8;

			lootText = UIKit.Label(panel, "", 13, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Loot");
			UIKit.Size(lootText.gameObject, -1, 36);

			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Label(buttons, "Click an item to add it, again to add more \u00B7 amounts can be changed in the inspector", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			Button done = UIKit.Button(buttons, "Done", Close, "Back to the editor (Ctrl+Z undoes what was added)", 110, 34);
			UIKit.Primary(done);
		}

		/// <summary>The tiles are made the first time the window opens (Raft's items are loaded by then).</summary>
		void FillTiles()
		{
			if (built) return;
			built = true;
			List<Item_Base> items;
			try { items = ItemManager.GetAllItems().Where(i => i != null && !string.IsNullOrEmpty(i.UniqueName) && i.settings_Inventory != null && i.settings_Inventory.Sprite != null && ContentCatalog.ItemLabel(i.UniqueName) != "An item").ToList(); } // (Raft's placeholder items are left out)
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's items: " + e.Message); items = new List<Item_Base>(); }
			foreach (Item_Base item in items.OrderBy(i => ContentCatalog.ItemLabel(i.UniqueName), StringComparer.OrdinalIgnoreCase))
			{
				string name = item.UniqueName, label = ContentCatalog.ItemLabel(name);
				Tile(grid, name, label, item.settings_Inventory.Sprite, label + " (" + name + ")");
			}
		}

		/// <summary>An item as a tile like Raft's inventory slots: picture, name; a click adds (or picks) it.</summary>
		void Tile(Transform parent, string name, string label, Sprite sprite, string hint)
		{
			RectTransform r = UIKit.Rect("Item_" + name, parent);
			Image bg = UIKit.Background(r.gameObject, Color.white, 6);
			var b = r.gameObject.AddComponent<Button>();
			UIKit.Register(b);
			b.targetGraphic = bg;
			UIKit.Slot(b);
			var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
			RectTransform pic = UIKit.Rect("Icon", r);
			UIKit.Anchor(pic, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(52, 52));
			Image icon = pic.gameObject.AddComponent<Image>();
			icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
			if (sprite == null) icon.color = new Color(0.3f, 0.2f, 0.1f, 0.3f);
			Text t = UIKit.Label(r, label, 10, UIKit.SlotText, TextAnchor.LowerCenter, FontStyle.Normal, "Label");
			UIKit.Stretch(t.rectTransform, 3, 3, 60, 3);
			t.resizeTextForBestFit = true; t.resizeTextMinSize = 8; t.resizeTextMaxSize = 10;
			UIKit.Border(r, UIKit.ButtonBorder, 6, 1f);
			UIKit.Hint(r.gameObject, hint);
			b.onClick.AddListener(() => Pick(name));
			tiles.Add(new KeyValuePair<string, GameObject>((label + " " + name).ToLowerInvariant(), r.gameObject));
		}

		void Filter()
		{
			string q = search.text.Trim().ToLowerInvariant();
			int shown = 0;
			foreach (var t in tiles)
			{
				bool on = q.Length == 0 || t.Key.Contains(q);
				t.Value.SetActive(on);
				if (on) shown++;
			}
			countText.text = shown + " of " + tiles.Count + " items";
		}

		void ShowLoot()
		{
			if (getLoot == null) return;
			List<KeyValuePair<string, int>> loot = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, getLoot() } });
			lootText.text = loot.Count == 0 ? "<i>Nothing yet.</i>" : "Chosen: " + string.Join(", ", loot.Select(l => ContentCatalog.ItemLabel(l.Key) + " \u00D7" + l.Value).ToArray());
		}
	}
}
