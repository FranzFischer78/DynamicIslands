using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The Objects tab's object browser (right side of the editor): a search field and every placeable object of
	/// Raft by category, as tiles with a small picture. Click a tile, then click the ground to place it.
	///
	/// Categories open and close with their header. The core categories (nature, styles, Raft's buildables...)
	/// are always loaded; the categories of Raft's other places (Utopia, Tangaroa, abandoned rafts...) load their
	/// island scenes the first time they are opened (a few seconds each), which the status line shows.
	/// </summary>
	public class ObjectBrowser : MonoBehaviour
	{
		/// <summary>Tiles shown per category before a "Show all" button (keeps huge categories quick).</summary>
		const int TilesPerCategory = 120;
		/// <summary>Search results shown at most.</summary>
		const int MaxResults = 240;
		const float TileWidth = 97f, TileHeight = 112f;

		public static ObjectBrowser Instance { get; private set; }

		InputField search;
		Text countText, statusText;
		ScrollRect scroll;
		RectTransform content;
		readonly HashSet<string> open = new HashSet<string> { PlaceableCatalog.NatureCategory };
		readonly HashSet<string> showAll = new HashSet<string>();
		readonly Dictionary<string, Image> tileFrames = new Dictionary<string, Image>();
		bool dirty = true;
		float rebuildAt;
		string highlighted;

		public void Build(RectTransform panel)
		{
			Instance = this;
			RectTransform head = UIKit.Row(panel, 22f, 6f, "Header");
			UIKit.Label(head, "OBJECTS", 14, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			countText = UIKit.Label(head, "", 13, UIKit.TextMuted, TextAnchor.MiddleRight);

			search = UIKit.Field(panel, "Search all objects (palm, crate...)", "", 30f, "Type to find objects in every category, loaded or not");
			search.onValueChanged.AddListener(v => { dirty = true; rebuildAt = Time.unscaledTime + 0.2f; });

			statusText = UIKit.Label(panel, "", 13, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Italic, "Status");
			UIKit.Size(statusText.gameObject, -1, 16);

			content = UIKit.ScrollList(panel, out scroll, 6f);
			var le = UIKit.Size(scroll.gameObject, -1, -1);
			le.flexibleHeight = 1f;

			// (the scatter: many of the last picked object round the point the camera looks at - things to gather, bushes,
			// rocks - kept off what is already on the island; one undo takes them back: ROADMAP LM11, 2026-10-05)
			RectTransform sc = UIKit.Row(panel, 28f, 4f, "Scatter");
			scatterButton = UIKit.Button(sc, "Scatter", ScatterNow, "Scatter many of the last object you picked round the middle of your view", 70f, 26f, 12);
			scatterCount = UIKit.Field(sc, "how many", "8", 26f, "How many to scatter");
			scatterRadius = UIKit.Field(sc, "radius m", "15", 26f, "Within this many metres of the middle of your view");
			scatterFree = UIKit.Field(sc, "keep m", "3", 26f, "Not within this many metres of anything already on the island (its buildings, its quest's objects)");
			waterButton = UIKit.Button(sc, "On land", () => { scatterWhere = (scatterWhere + 1) % 3; UIKit.LabelOf(waterButton).text = ScatterTool.WhereNames[scatterWhere]; },
				"Where they go: on land, on the sea floor (Raft's sea finds: sand, clay, stone, ores, scrap, clams, seaweed), or anywhere (click to change)", 86f, 26f, 12);
			waterButton.name = "Button_ScatterWhere";
			// (the seed: the same seed gives the same spots, as a recipe's "scatter ... seed="; empty: new spots each time)
			scatterSeed = UIKit.Field(sc, "seed", "", 26f, "Same seed, same spots (a recipe's scatter seed=). Empty: new spots each time; the one used is shown after");
			scatterSeed.name = "Field_ScatterSeed";
			scatterSeed.contentType = InputField.ContentType.IntegerNumber;
			PlaceableCatalog.Changed += MarkDirty;
		}

		Button scatterButton, waterButton;
		InputField scatterCount, scatterRadius, scatterFree, scatterSeed;
		/// <summary>0 on land, 1 under water, 2 anywhere (ScatterTool.WhereNames).</summary>
		int scatterWhere;
		/// <summary>The object last picked in the list: what Scatter spreads.</summary>
		public static string LastPicked = "";

		void ScatterNow()
		{
			if (LastPicked.Length == 0) { DynamicIslands.Notify("Pick an object in the list first (a pineapple, a palm, a rock...), then Scatter", true); return; }
			Vector3? centre = ScatterTool.ViewCentre();
			if (centre == null) { DynamicIslands.Notify("Look at the island (or the sea floor) where they should go, then Scatter", true); return; }
			int n; float r, keep;
			if (!int.TryParse(scatterCount.text, out n)) n = 8;
			if (!float.TryParse(scatterRadius.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r)) r = 15f;
			if (!float.TryParse(scatterFree.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out keep)) keep = 3f;
			int where = ScatterTool.OnlyUnderWater(LastPicked) && scatterWhere == 0 ? 1 : scatterWhere;
			int seed;
			if (!int.TryParse(scatterSeed.text, out seed)) seed = UnityEngine.Random.Range(1, 100000);
			int made = ScatterTool.Scatter(LastPicked, centre.Value, Mathf.Clamp(n, 1, 200), Mathf.Clamp(r, 1f, 300f), Mathf.Clamp(keep, 0f, 30f), where, seed).Count;
			DynamicIslands.Notify(made == 0 ? "No room for " + PlaceableCatalog.DisplayName(LastPicked) + " there (" + ScatterTool.WhereNames[where].ToLowerInvariant() + ", away from other objects)"
				: "Scattered " + made + " " + PlaceableCatalog.DisplayName(LastPicked) + (made < n ? " (no room for more)" : "") + " with seed " + seed + " - Ctrl+Z takes them back");
		}

		void OnDestroy()
		{
			PlaceableCatalog.Changed -= MarkDirty;
			if (Instance == this) Instance = null;
		}

		void MarkDirty()
		{
			dirty = true;
			rebuildAt = Mathf.Min(rebuildAt, Time.unscaledTime + 0.3f);
		}

		/// <summary>The search text (the automated tests use this).</summary>
		public string Search
		{
			get { return search != null ? search.text : ""; }
			set { if (search != null) search.text = value; RebuildNow(); }
		}

		/// <summary>Names of the object tiles currently in the list (the automated tests use this).</summary>
		public List<string> VisibleObjects()
		{
			return content.GetComponentsInChildren<ObjectTile>(false).Select(t => t.Name).ToList();
		}

		public void RebuildNow() { dirty = false; Rebuild(); }

		void Update()
		{
			if (search != null)
			{
				bool typing = search.isFocused;
				if (typing) EditorInput.IsTyping = true;
				else if (wasTyping) EditorInput.IsTyping = false;
				wasTyping = typing;
				if (typing && Input.GetKeyDown(KeyCode.Escape)) { EscKey.Swallow(); search.text = ""; search.DeactivateInputField(); }
			}
			if (dirty && Time.unscaledTime >= rebuildAt) { dirty = false; Rebuild(); }

			string loading = string.Join(", ", PlaceableCatalog.LoadingScenes.Select(PlaceableCatalog.SceneLabel).ToArray());
			statusText.text = loading.Length > 0 ? "Loading objects from " + loading + "..." :
				PlaceableCatalog.Indexing ? (PlaceableCatalog.IndexProgress ?? "Scanning Raft's islands for objects...") :
				!PlaceableCatalog.IsBuilt ? "Loading objects..." : "";

			// The object being placed is outlined in its tile
			ObjectPlacer placer = ObjectPlacer.Current;
			string now = placer != null ? placer.GameObjectName : null;
			if (now != highlighted)
			{
				Image frame;
				if (highlighted != null && tileFrames.TryGetValue(highlighted, out frame) && frame != null) frame.color = UIKit.ButtonBorder;
				if (now != null && tileFrames.TryGetValue(now, out frame) && frame != null) frame.color = UIKit.Accent;
				highlighted = now;
			}
		}

		bool wasTyping;

		void OnDisable() { if (wasTyping) EditorInput.IsTyping = false; wasTyping = false; }

		void Rebuild()
		{
			if (content == null) return;
			float scrollY = content.anchoredPosition.y;
			// (switched off first: Destroy only happens at the end of the frame)
			foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			tileFrames.Clear();
			highlighted = null;

			List<KeyValuePair<string, List<PlaceableCatalog.Entry>>> all = PlaceableCatalog.Browse();
			int total = all.Sum(c => c.Value.Count), loaded = all.Sum(c => c.Value.Count(e => e.Loaded));
			countText.text = total == loaded ? total + " objects" : loaded + " loaded / " + total;

			string query = (search != null ? search.text : "").Trim();
			if (query.Length > 0)
			{
				string q = query.ToLowerInvariant();
				int shown = 0;
				foreach (var cat in all)
				{
					bool catMatch = cat.Key.ToLowerInvariant().Contains(q);
					List<PlaceableCatalog.Entry> hits = cat.Value.Where(e => catMatch || e.Label.ToLowerInvariant().Contains(q) || e.Name.ToLowerInvariant().Contains(q)).ToList();
					if (hits.Count == 0 || shown >= MaxResults) continue;
					Header(cat.Key, hits.Count, true, false);
					shown += Tiles(cat.Key, hits.Take(MaxResults - shown).ToList());
				}
				if (shown == 0) UIKit.Label(content, "Nothing called \"" + query + "\".", 14, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
				else if (shown >= MaxResults) UIKit.Label(content, "More objects match; type more of the name.", 12, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
			}
			else
			{
				foreach (var cat in all)
				{
					bool isOpen = open.Contains(cat.Key);
					bool unloaded = cat.Value.Any(e => !e.Loaded);
					Header(cat.Key, cat.Value.Count, isOpen, unloaded);
					if (!isOpen) continue;
					// Things to gather in groups by the islands they suit (ROADMAP LM11): all of them, under small headings
					if (cat.Key == PlaceableCatalog.HarvestableCategory)
					{
						// (by the style of island they suit; a thing that suits several styles is under each)
						foreach (var grp in cat.Value.SelectMany(e => GenGather.StylesOf(e.Name).Select(st => new KeyValuePair<string, PlaceableCatalog.Entry>(st, e)))
							.GroupBy(x => x.Key, x => x.Value).OrderBy(gr => Array.IndexOf(GenGather.Styles, gr.Key)))
						{
							UIKit.Label(content, grp.Key, 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
							Tiles(cat.Key + "_" + Array.IndexOf(GenGather.Styles, grp.Key), grp.ToList());
						}
						continue;
					}
					List<PlaceableCatalog.Entry> list = showAll.Contains(cat.Key) ? cat.Value : cat.Value.Take(TilesPerCategory).ToList();
					Tiles(cat.Key, list);
					if (list.Count < cat.Value.Count)
					{
						string c = cat.Key;
						UIKit.Button(content, "Show all " + cat.Value.Count, () => { showAll.Add(c); RebuildNow(); }, null, -1, 26f, 12);
					}
				}
				if (!PlaceableCatalog.IndexIsCurrent && !PlaceableCatalog.Indexing)
					UIKit.Label(content, "Raft's other islands (Utopia, Tangaroa, abandoned rafts...) appear here once the editor has scanned them (after a Raft update).", 13, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			}

			Canvas.ForceUpdateCanvases();
			content.anchoredPosition = new Vector2(content.anchoredPosition.x, scrollY);
		}

		/// <summary>A category header: click to open or close it (opening loads its island scenes if needed).</summary>
		void Header(string category, int count, bool isOpen, bool unloaded)
		{
			Button b = UIKit.Button(content, (isOpen ? "\u25BC  " : "\u25BA  ") + (category == PlaceableCatalog.HarvestableCategory ? "Things to gather" : category), () => Toggle(category), unloaded ? "Objects from Raft's own islands: they load when you open the category" : null, -1, 28f, 13);
			Text t = UIKit.LabelOf(b);
			t.alignment = TextAnchor.MiddleLeft;
			t.fontStyle = FontStyle.Bold;
			Text n = UIKit.Label(b.transform, count + (unloaded ? "  <color=#a08660>not loaded</color>" : ""), 13, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, "Count");
			UIKit.Stretch(n.rectTransform, 8, 10, 0, 0);
			UIKit.Flat(b);
		}

		void Toggle(string category)
		{
			if (search != null && search.text.Trim().Length > 0) { search.text = ""; open.Add(category); }
			else if (!open.Remove(category)) open.Add(category);
			if (open.Contains(category) && !PlaceableCatalog.CategoryLoaded(category))
				DynamicIslands.instance.StartCoroutine(PlaceableCatalog.EnsureCategory(category));
			RebuildNow();
		}

		/// <summary>A grid of object tiles; returns how many.</summary>
		int Tiles(string category, List<PlaceableCatalog.Entry> entries)
		{
			if (entries.Count == 0) return 0;
			RectTransform grid = UIKit.Rect("Grid_" + category, content);
			var g = grid.gameObject.AddComponent<GridLayoutGroup>();
			g.cellSize = new Vector2(TileWidth, TileHeight);
			g.spacing = new Vector2(5f, 5f);
			g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			g.constraintCount = 3;
			g.childAlignment = TextAnchor.UpperLeft;
			foreach (PlaceableCatalog.Entry e in entries) Tile(grid, e);
			return entries.Count;
		}

		void Tile(Transform grid, PlaceableCatalog.Entry e)
		{
			RectTransform r = UIKit.Rect("Tile_" + e.Name, grid);
			Image bg = UIKit.Background(r.gameObject, Color.white, 6);
			var b = r.gameObject.AddComponent<Button>();
			UIKit.Register(b);
			b.targetGraphic = bg;
			UIKit.Slot(b);
			var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
			var tile = r.gameObject.AddComponent<ObjectTile>();
			tile.Name = e.Name;

			RectTransform pic = UIKit.Rect("Picture", r);
			pic.anchorMin = new Vector2(0, 1); pic.anchorMax = new Vector2(1, 1); pic.pivot = new Vector2(0.5f, 1);
			pic.offsetMin = new Vector2(5, -(TileWidth - 10)); pic.offsetMax = new Vector2(-5, -5);
			var raw = pic.gameObject.AddComponent<RawImage>();
			raw.raycastTarget = false;
			raw.color = new Color(0.45f, 0.36f, 0.24f, 1f);
			if (e.Loaded) ObjectThumbnails.Request(e.Name, raw);
			else
			{
				Text wait = UIKit.Label(pic, "click to load", 10, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Italic, "NotLoaded");
				UIKit.Stretch(wait.rectTransform);
			}

			Text label = UIKit.Label(r, e.Label, 11, UIKit.SlotText, TextAnchor.UpperCenter, FontStyle.Normal, "Label");
			label.rectTransform.anchorMin = new Vector2(0, 0); label.rectTransform.anchorMax = new Vector2(1, 0); label.rectTransform.pivot = new Vector2(0.5f, 0);
			label.rectTransform.offsetMin = new Vector2(3, 2); label.rectTransform.offsetMax = new Vector2(-3, TileHeight - TileWidth + 4);
			label.verticalOverflow = VerticalWrapMode.Truncate;
			label.resizeTextForBestFit = true; label.resizeTextMinSize = 9; label.resizeTextMaxSize = 11;

			Image frame = UIKit.Border(r, e.Name == highlighted ? UIKit.Accent : UIKit.ButtonBorder, 6, 1.5f);
			tileFrames[e.Name] = frame;
			string where = e.Scene != null ? " (from " + PlaceableCatalog.SceneLabel(e.Scene) + ")" : "";
			string extra = ContentCatalog.Hint(e.Name);
			UIKit.Hint(r.gameObject, e.Label + where + (e.Loaded ? "" : " - not loaded yet: click to load it") + (extra != null ? " - " + extra : "") + (PlaceableCatalog.IsHarvestable(e.Name) || e.Category == PlaceableCatalog.HarvestableCategory ? " - harvestable in a world" : ""));

			string name = e.Name;
			b.onClick.AddListener(() => Pick(name));
		}

		/// <summary>Starts placing an object (loading its island scene first if needed).</summary>
		public void Pick(string name)
		{
			LastPicked = name;
			if (PlaceableCatalog.IsLoaded(name)) { StartPlacing(name); return; }
			DynamicIslands.instance.StartCoroutine(LoadThenPlace(name));
		}

		IEnumerator LoadThenPlace(string name)
		{
			DynamicIslands.Notify("Loading " + PlaceableCatalog.DisplayName(name) + " from " + PlaceableCatalog.SceneLabel(PlaceableCatalog.SceneOf(name)) + "...");
			yield return PlaceableCatalog.EnsureLoaded(new[] { name });
			if (PlaceableCatalog.IsLoaded(name)) StartPlacing(name);
			else DynamicIslands.Notify("Could not load " + PlaceableCatalog.DisplayName(name) + " - see the console (F10)", true);
		}

		internal static void StartPlacing(string name)
		{
			if (!DynamicIslands.InEditor() || DynamicIslands.EditorGizmoHandler == null) return;
			EditorUI.SetTab(TAB.ObjectPlace);
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			DynamicIslands.EditorGizmoHandler.placingObject = true;
			DynamicIslands.PlaceObject(name);
		}
	}

	/// <summary>Marks an object tile with the catalog name it places.</summary>
	public class ObjectTile : MonoBehaviour
	{
		public string Name;
	}
}
