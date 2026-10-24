using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The editor's "Generate island" window, in three tabs, with a live preview map on the right:
	///   Normal             - every setting of the generator: size and height (with presets measured from Raft's own
	///                        islands), coast, land features, the sea around it, nature (a slider per kind of object),
	///                        animals and loot boxes; saved presets of one's own.
	///   Randomize existing - one of Raft's islands (with its picture): "something new like it" fills the settings in
	///                        from its measurements, "a variation of it" starts from its own ground and changes its shape.
	///   Ready-made         - the map types: whole islands with content and a story, made as a new island file.
	/// Every setting has a "?" to hover for help. Generate replaces the current island (one Ctrl+Z brings the old one
	/// back). Built in code with UIKit. Opened from the top bar's Generate button and the Island tab.
	/// </summary>
	public class GeneratorWindow : MonoBehaviour
	{
		static GeneratorWindow instance;

		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		/// <summary>The settings being edited (Normal and Randomize existing share them).</summary>
		IslandGenSettings s = new IslandGenSettings();
		public const int TabNormal = 0, TabRandomize = 1, TabReady = 2;
		int tab;
		/// <summary>The last settings generated on the Generate island tab, and the last ones a Randomize / Ready-made generate left in IslandGenerator.Last.</summary>
		static IslandGenSettings lastNormal, randomizeMade;
		/// <summary>True while the settings are the ones a Randomize / Ready-made generate left (the window opened on them).</summary>
		bool fromOtherTab;

		/// <summary>The Generate island tab's own settings in place of a Randomize / Ready-made generate's (seed and style kept).</summary>
		void OwnSettings()
		{
			if (!fromOtherTab) return;
			fromOtherTab = false;
			int seed = s.Seed, style = s.Style;
			s = lastNormal != null ? lastNormal.Copy() : IslandGenSettings.RaftLike(seed);
			s.Seed = seed;
			s.Style = style;
		}
		int mapType = 6;
		RaftIsland chosen;
		bool variation;

		InputField seedField;
		Text status, info, estimate, chosenText, likeText, generateLabel, reachText;
		float reachDue = -1f;
		RawImage preview;
		Texture2D previewTex;
		Button[] tabButtons;
		RectTransform[] tabRoots;
		RectTransform presetsRoot, variationGroup, likeGroup;
		readonly List<Action> refresh = new List<Action>();
		readonly List<KeyValuePair<Button, RaftIsland>> islandTiles = new List<KeyValuePair<Button, RaftIsland>>();
		readonly List<KeyValuePair<Button, int>> typeCards = new List<KeyValuePair<Button, int>>();
		float previewDue = -1f, confirmUntil;

		public static GeneratorWindow Instance { get { return instance; } }
		public IslandGenSettings Settings { get { return s; } }
		public int Tab { get { return tab; } }

		public static void Create(Transform canvas, GameObject unusedTemplate)
		{
			var blocker = new GameObject("GeneratorWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f); // modal: swallows clicks on the editor
			instance = blocker.AddComponent<GeneratorWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			IslandFilesWindow.Close();
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			// (after a Randomize existing or Ready-made generate - e.g. LikeIt(Tangaroa), a 139 m round island - the
			// Generate island tab doesn't open on it: it opens on its own last settings, at first the Raft-like default)
			instance.s = IslandGenerator.Last.Copy();
			instance.fromOtherTab = randomizeMade != null && ReferenceEquals(IslandGenerator.Last, randomizeMade);
			if (instance.tab == TabNormal) instance.OwnSettings();
			instance.s.Style = DynamicIslands.currentStyle; // start from the island's current style
			// (a Raft island's ground belongs to Randomize existing, with that island picked)
			if (instance.tab != TabRandomize || instance.chosen == null || !instance.variation) instance.s.Source = "";
			instance.ShowAll();
			instance.SetStatus("Generating replaces the current island. Ctrl+Z brings it back.");
		}

		public static void Close()
		{
			if (instance == null) return;
			DropList.Close();
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		void Update()
		{
			EditorInput.IsTyping = seedField != null && seedField.isFocused;
			if (TextPromptWindow.IsOpen || DropList.Busy || EditorInput.SubWindowJustClosed) return; // (its own Enter and Esc; an open list closes on Esc by itself)
			if (EscKey.Down(this)) Close();
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OnGenerate();
			if (previewDue > 0f && Time.unscaledTime >= previewDue) { previewDue = -1f; UpdatePreview(); }
			if (reachDue > 0f && Time.unscaledTime >= reachDue) { reachDue = -1f; UpdateReach(); }
		}

		// (closing the window drops a Generate still waiting for Raft's scenes: reopened, it generated anyway - CA14)
		void OnDisable() { EditorInput.IsTyping = false; loadingBuildings = false; waitingRun++; }

		int waitingRun;

		#region Building

		const float Width = 1070f, HeightPx = 736f, LeftWidth = 640f;

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 12, 14), 8f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, HeightPx));

			RectTransform head = UIKit.Row(panel, 34f, 10f, "Head");
			UIKit.Size(UIKit.Label(head, "GENERATE ISLAND", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).gameObject, 230);
			tabButtons = UIKit.Tabs(head, new[] { "Normal", "Randomize existing", "Ready-made (with content)" },
				new[] { "Make an island from scratch: every setting of the generator", "Pick one of Raft's own islands: make something new like it, or a variation of its own shape", "Whole islands with chests, creatures and a quest (map types), made as a new island file" },
				SetTab, 32f, 14);

			RectTransform body = UIKit.Row(panel, HeightPx - 12 - 14 - 34 - 8 - 8, 14f, "Body");
			body.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
			RectTransform left = UIKit.Rect("Left", body);
			UIKit.Size(left.gameObject, LeftWidth);
			tabRoots = new RectTransform[3];
			for (int i = 0; i < 3; i++)
			{
				RectTransform holder = UIKit.Rect("Tab" + i, left);
				UIKit.Stretch(holder);
				ScrollRect scroll;
				tabRoots[i] = UIKit.ScrollList(holder, out scroll, 8f);
				UIKit.Stretch((RectTransform)scroll.transform);
			}
			BuildNormal(tabRoots[TabNormal]);
			BuildRandomize(tabRoots[TabRandomize]);
			BuildReady(tabRoots[TabReady]);
			BuildRight(body);
			SetTab(TabNormal);
		}

		/// <summary>The right column: the preview map, what it shows, the seed, and Generate / Close.</summary>
		void BuildRight(Transform body)
		{
			RectTransform right = UIKit.Rect("Right", body);
			UIKit.Vertical(right.gameObject, 8f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(right.gameObject, -1, -1, 1);
			RectTransform pg = UIKit.Group(right, "Preview");
			pg.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
			preview = UIKit.Picture(pg, null, 290f, 290f, "PreviewMap");
			info = UIKit.Label(pg, "", 13, UIKit.TextColor, TextAnchor.UpperLeft);
			info.lineSpacing = 1.05f;
			estimate = UIKit.Label(pg, "", 12, UIKit.TextMuted, TextAnchor.UpperLeft);

			// Can a player on a raft get onto it? (IslandReach, from the same ground the generator makes), above the seed
			RectTransform seedGroup = UIKit.Group(right, "Reach and seed", "Group_Seed");
			RectTransform reachRow = UIKit.Row(seedGroup, 48f, 4f, "Reach");
			reachRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
			reachText = UIKit.Label(reachRow, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "ReachText");
			reachText.resizeTextForBestFit = true; reachText.resizeTextMinSize = 9; reachText.resizeTextMaxSize = 12;
			reachText.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Help(reachRow, "Whether a player arriving on a raft can get onto the island, worked out from the ground these settings make and Raft's own player: " +
				"the steepest slope they walk up (" + IslandReach.WalkSlope.ToString("F0") + "°), how high a jump lifts them (" + IslandReach.JumpUp.ToString("0.0") + " m), the highest ledge they get onto by jumping out of the water (" +
				IslandReach.SwimLedge.ToString("0.0") + " m), and the highest they get onto hopping on up the steep rock, from the water or a raft pushed against the coast (" + IslandReach.ClimbLedge.ToString("0.0") + " m; measured with Raft's own player). " +
				"Beaches and low coasts are easy; cliffs with a low ledge need a jump; higher cliffs need building (stairs, a ladder, foundations). Stretches of cliff, Plateau and Sea stacks make islands harder to get onto; Beach and Cliffs change it most.");
			RectTransform seedRow = UIKit.Row(seedGroup, 30f, 6f, "Seed");
			seedField = UIKit.Field(seedRow, "any number", "", 30f, "The same seed and settings always give the same island");
			seedField.contentType = InputField.ContentType.IntegerNumber;
			seedField.characterLimit = 9;
			seedField.onEndEdit.AddListener(v => { int n; if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) { s.Seed = n; Changed(); } });
			UIKit.Button(seedRow, "Random", () => { s.Seed = UnityEngine.Random.Range(1, 999999); seedField.text = s.Seed.ToString(CultureInfo.InvariantCulture); Changed(); }, "A new random seed: a different island with the same settings", 86);
			UIKit.Help(seedRow, "The seed picks which island of all the possible ones you get. The same seed with the same settings always makes exactly the same island, so you can note a seed you like, or share it. Random gives a new one.");

			status = UIKit.Label(right, "", 13, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Status");
			UIKit.Size(status.gameObject, -1, 52);
			RectTransform buttons = UIKit.Row(right, 36f, 8f, "Buttons");
			Button gen = UIKit.Button(buttons, "Generate", OnGenerate, "Replace the current island with a generated one (Enter; Ctrl+Z undoes)", -1, 36, 16);
			generateLabel = UIKit.LabelOf(gen);
			UIKit.Primary(gen);
			UIKit.Button(buttons, "Close", Close, "Close (Esc)", 100, 36, 16);
		}

		#endregion

		#region Normal tab

		void BuildNormal(Transform root)
		{
			RectTransform mine = UIKit.Group(root, "My presets");
			presetsRoot = UIKit.Rect("Presets", mine);
			UIKit.Vertical(presetsRoot.gameObject, 4f, new RectOffset(0, 0, 0, 0));
			RectTransform pr = UIKit.Row(mine, 26f, 4f, "PresetButtons");
			UIKit.Button(pr, "Save these settings...", SavePreset, "Keep all of these settings (not the seed) as a preset of your own, to use again for other islands", -1, 26f, 12);
			UIKit.Button(pr, "Defaults", () => { int seed = s.Seed; int style = s.Style; s = IslandGenSettings.RaftLike(seed); s.Style = style; ShowAll(); SetStatus("All settings back to their defaults (the seed and style stay)."); }, "Every setting back to its default (the seed and style stay)", 90, 26f, 12);
			UIKit.Help(pr, "Presets are your own recipes: Save these settings keeps everything on this tab (style, layout, size, coast, nature, animals, loot) under a name. Click a preset to use it again with any seed. The × next to one deletes it (click it twice). Presets live in Mods\\DynamicIslands\\generator_presets.");
			RefreshPresets();

			RectTransform island = UIKit.Group(root, "Island");
			Stepper(island, "Style", "Ground textures and the objects that grow: palms, snowy pines, cacti, birches, or a volcano",
				"Style sets the ground textures (sand, grass, snow, desert, forest floor, ash) and which objects grow: tropical palms and bushes, snowy pines and drifts, desert cacti and grass, forest birches and pines, or volcanic rock with a crater on the highest peak. Animals and loot follow the style too, unless you choose them.",
				() => TerrainPainter.StyleName(s.Style) + (TerrainPainter.HasStyle(s.Style) ? "" : " (textures not loaded)"), step => { int n = TerrainPainter.Styles.Length; s.Style = ((s.Style + step) % n + n) % n; },
				Enumerable.Range(0, TerrainPainter.Styles.Length).Select(i => new DropList.Option(i.ToString(), TerrainPainter.StyleName(i), StyleHints[Mathf.Min(i, StyleHints.Length - 1)])).ToList(), () => s.Style, i => s.Style = i);
			Stepper(island, "Layout", "Like one of Raft's small or big islands, or one island, an atoll, an archipelago, sea stacks, a plateau, a marsh, a crescent or twin peaks",
				"Layout is the basic shape of the land. Raft small island (the first one): shaped like one of Raft's own small islands, the seed picking which - an uneven coast, a beach all round to wade onto from the raft, a hill in the middle. Raft large island: the same from Raft's big islands. Round: one island with peaks. Atoll: a ring of low land around a lagoon. Archipelago: several islets on a shallow shelf. Sea stacks: steep rock pillars. Plateau: a flat-topped mesa with a ramp up. Marsh: low land with pools. Crescent: a curved island around a sheltered bay (like Raft's Cresent). Twin peaks: two tall peaks (like Raft's Twin peak). Every other setting shapes it further.",
				() => IslandShapes.Names[s.Shape], step => { int[] o = IslandShapes.Order; int n = o.Length, at = Mathf.Max(0, Array.IndexOf(o, s.Shape)); s.Shape = o[((at + step) % n + n) % n]; SetStatus(IslandShapes.Hints[s.Shape] + "."); },
				IslandShapes.Order.Select(i => new DropList.Option(i.ToString(), IslandShapes.Names[i], IslandShapes.Hints[i])).ToList(), () => s.Shape, i => { s.Shape = i; SetStatus(IslandShapes.Hints[s.Shape] + "."); });

			RectTransform size = UIKit.Group(root, "Size and height");
			Slider(size, "Size", IslandGenSettings.MinRadius, IslandGenSettings.MaxRadius, () => s.Radius, v => s.Radius = v, v => "land about " + (v * 2f).ToString("F0") + " m across",
				"How big the land is", "How big the land is, as the width of a round island this big. Stretch (Coast) makes it longer and narrower with the same area. The buttons set Raft's own sizes, measured from its islands: its small islands (a sand patch with a few palms), its big islands, and Balboa. The build area is 1000 m, so the biggest islands (with Stretch) are cut off at its edge.");
			RectTransform sizes = UIKit.Row(size, 26f, 4f, "SizePresets");
			PresetButton(sizes, "Small island", () => RaftIslands.SmallRadius, v => s.Radius = v, v => (v * 2f).ToString("F0") + " m", "Raft's small islands: ");
			PresetButton(sizes, "Large island", () => RaftIslands.LargeRadius, v => s.Radius = v, v => (v * 2f).ToString("F0") + " m", "Raft's big islands: ");
			PresetButton(sizes, "Balboa", () => RaftIslands.BalboaRadius, v => s.Radius = v, v => (v * 2f).ToString("F0") + " m", "Raft's Balboa island: ");
			Slider(size, "Highest point", IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight, () => s.Height, v => s.Height = v, v => v.ToString("F0") + " m above the sea",
				"Height of the tallest peak above the sea", "The top of the island is exactly this high above the sea; everything else scales with it. The buttons set the heights measured on Raft's islands: small islands are about a house high, big islands a small hill, Balboa a real mountain.");
			RectTransform heights = UIKit.Row(size, 26f, 4f, "HeightPresets");
			PresetButton(heights, "Small island", () => RaftIslands.SmallTop, v => s.Height = v, v => v.ToString("F0") + " m", "Raft's small islands: ");
			PresetButton(heights, "Large island", () => RaftIslands.LargeTop, v => s.Height = v, v => v.ToString("F0") + " m", "Raft's big islands: ");
			PresetButton(heights, "Balboa", () => RaftIslands.BalboaTop, v => s.Height = v, v => v.ToString("F0") + " m", "Raft's Balboa island: ");
			Slider(size, "Peaks", 1, IslandGenSettings.MaxPeaks, () => s.Peaks, v => s.Peaks = Mathf.RoundToInt(v), v => v.ToString("F0"), "How many hills",
				"How many hills or mountains the round layouts get. The first is the highest; the others are lower and further out. (Twin peaks always has two.)", true);
			Slider(size, "Peak shape", 0f, 1f, () => s.PeakShape, v => s.PeakShape = v, v => v < 0.15f ? "full, rounded" : v < 0.45f ? "hills" : v < 0.75f ? "pointed" : "spires",
				"Rounded hills or sharp spires", "Left: full, rounded hills that fill the island. Middle: normal hills. Right: sharp peaks and spires over flat lowland, like a volcano plug or Raft's Twin peak.");
			Slider(size, "Hills", 0f, 1f, () => s.Roughness, v => s.Roughness = v, v => v < 0.2f ? "smooth" : v < 0.5f ? "gentle" : v < 0.8f ? "rugged" : "wild",
				"Smooth or bumpy, ridged hills", "How bumpy the land is between the peaks: smooth slopes on the left, rugged, ridged ground on the right. (Coast raggedness follows this unless you set Coast below.)");

			RectTransform coast = UIKit.Group(root, "Coast and outline");
			Slider(coast, "Coast", 0f, 1f, () => s.CoastAmount, v => s.Coast = v, v => v < 0.2f ? "smooth" : v < 0.5f ? "wavy" : v < 0.8f ? "ragged" : "wild",
				"How ragged the coastline is", "How ragged the coastline is: a smooth round coast on the left, points, coves and small islets on the right. Raft's small islands are quite ragged; its big ones in between.");
			Slider(coast, "Bays", 0f, 1f, () => s.Bays, v => s.Bays = v, v => v <= 0.01f ? "none" : v < 0.4f ? "a few coves" : v < 0.75f ? "bays" : "deep inlets",
				"Inlets cut into the coast", "Bays and inlets cut into the coast: more and deeper towards the right. Good harbours for a raft.");
			Slider(coast, "Beach", 0f, 1f, () => s.BeachWidth, v => s.BeachWidth = v, v => v < 0.2f ? "narrow" : v < 0.6f ? "normal" : "wide",
				"Width of the sandy band before the hills", "How wide the flat, sandy band is between the water and the hills. Wide beaches give room to land a raft and build; narrow ones make the hills start at the water.");
			Slider(coast, "Cliffs", 0f, 1f, () => s.Cliffs, v => v <= 0.01f ? "none" : (v * 100f).ToString("F0") + " % of the coast", v => s.Cliffs = v,
				"Share of the coast that drops steeply", "Parts of the coast become cliffs that drop straight into deeper water instead of a beach. 100 % is cliffs all around (you'll need a ladder or a ramp).");
			Slider(coast, "Stretch", 1f, IslandGenSettings.MaxStretch, () => s.Stretch, v => s.Stretch = v, v => v < 1.05f ? "round" : v.ToString("0.0") + " times as long as wide",
				"Long and narrow instead of round", "Makes the island longer and narrower (the area stays the same), like a ridge or a long reef. Direction turns it.");
			Slider(coast, "Direction", 0f, 180f, () => s.StretchAngle, v => s.StretchAngle = v, v => v.ToString("F0") + "°",
				"Which way a stretched island points", "Which way the long axis of a stretched island points (0° = along the x axis). Only matters with Stretch above 1.");

			RectTransform land = UIKit.Group(root, "Land features");
			Slider(land, "Valleys", 0f, 1f, () => s.Valleys, v => s.Valleys = v, v => v <= 0.01f ? "none" : (1 + Mathf.RoundToInt(v * 4f)) + " valley(s)",
				"River valleys from the top down to the sea", "Winding valleys cut from near the highest point down to the sea, like dry river beds. More and wider to the right. They end in little inlets.");
			Slider(land, "Lakes", 0f, 1f, () => s.Lakes, v => s.Lakes = v, v => v <= 0.01f ? "none" : "up to " + (1 + Mathf.RoundToInt(v * 3f)) + ", " + (v < 0.5f ? "small" : "big"),
				"Lakes in the lower land", "Lakes in the lower land, below sea level so the sea's water fills them (Raft has one sea level). More and bigger to the right.");
			Slider(land, "Terraces", 0f, 1f, () => s.Terraces, v => s.Terraces = v, v => v <= 0.01f ? "none" : v < 0.5f ? "soft steps" : "sharp steps",
				"Flat steps in the slopes", "Turns slopes into flat terraces with steep steps between them, like rice terraces or layered rock. Good places to build.");
			Slider(land, "Erosion", 0f, 1f, () => s.Erosion, v => s.Erosion = v, v => v <= 0.01f ? "none" : v < 0.5f ? "light" : "heavy",
				"Rain-worn gullies", "Simulated rain washes soil down the slopes: natural gullies, softer ridges and fans of soil at their feet. Heavier erosion takes a moment longer to generate.");

			RectTransform sea = UIKit.Group(root, "Under water");
			SeaFloorChoice(sea);
			Slider(sea, "Shallow water", 0f, 1f, () => s.Shelf, v => s.Shelf = v, v => v < 0.2f ? "a narrow rim" : v < 0.6f ? "normal" : "wide flats",
				"Width of the shallow water around the island", "How far the shallow shelf (about 10 m deep at its edge) reaches from the coast before the drop-off. Wide flats are good for corals and wading; a narrow rim drops off quickly. Raft's islands have 15-30 m of it. (Round layouts.)");
			Slider(sea, "Drop-off", 0f, 1f, () => s.DropOff, v => s.DropOff = v, v => v < 0.2f ? "a long, gentle slope" : v < 0.45f ? "a slope" : v < 0.75f ? "steep" : "a sheer wall",
				"How steeply the sea floor falls away", "Deep sea floor: how the ground falls from the shelf's edge to the sea floor 160 m below. Left: a long, gentle slope, like Raft's big islands; right: a sheer wall into the dark, like its small islands. Spurs, gullies and big rock formations break it up either way.");
			Choice(sea, "Seabed", new[] { "Sand", "Rocky", "Reef ring" }, () => s.Seabed, v => s.Seabed = v,
				"Sand: a smooth sandy shelf, as around Raft's islands. Rocky: bumps and outcrops under the water. Reef ring: a ring of reef just under the surface around the island, with gaps to sail through.");

			BuildContent(root);
		}

		/// <summary>Nature, animals and loot: on the Normal tab and on Randomize existing (the same settings).</summary>
		void BuildContent(Transform root)
		{
			RectTransform nature = UIKit.Group(root, "Nature");
			RectTransform quick = UIKit.Row(nature, 26f, 4f, "Quick");
			// (each quick button against Like Raft: every kind as thick on the ground as on Raft's own islands of the style)
			foreach (var q in new[] { new KeyValuePair<string, float>("None", 0f), new KeyValuePair<string, float>("Sparse", 0.55f), new KeyValuePair<string, float>("Like Raft", 1f), new KeyValuePair<string, float>("Dense", 1.9f), new KeyValuePair<string, float>("Jungle", -1f) })
			{
				float f = q.Value;
				UIKit.Button(quick, q.Key, () =>
				{
					if (f < 0f) { s.Trees = s.Bushes = 1f; s.Rocks = s.Harvest = 0.75f; s.BeachThings = 0.6f; }
					else
					{
						IslandGenerator.NatureLikeRaft(s);
						s.Trees = Mathf.Clamp01(s.Trees * f); s.Bushes = Mathf.Clamp01(s.Bushes * f); s.Rocks = Mathf.Clamp01(s.Rocks * f);
						s.Harvest = Mathf.Clamp01(s.Harvest * f); s.BeachThings = Mathf.Clamp01(s.BeachThings * f);
					}
					ShowAll();
				}, "Set the land's object sliders at once" + (f < 0f ? " (max: barely walkable)" : f == 1f ? " (as on Raft's own islands of the style)" : ""), -1, 26f, 12);
			}
			UIKit.Help(quick, "Quick settings for all the land's object sliders at once (not the life under water). Like Raft sets each kind as thick on the ground as on Raft's own islands of the style - measured on them: a small island like Raft's small ones (five times the bushes and three times the trees for its land, green nearly down to the water), a big one like its big ones (sandy beaches, few things to pick up on the land). Sparse is about a third of that, Dense about three and a half times; Jungle is the maximum: trees about 3 m apart with bushes between, barely walkable. Fine-tune each kind below.");
			// (each slider's word against its own Like Raft)
			Func<string, Func<float, string>> amount = cat => v =>
			{
				float r = v / Mathf.Max(0.01f, IslandGenerator.LikeRaftAmount(s.Style, cat));
				return v <= 0.01f ? "none" : r < 0.75f ? "less than Raft" : r < 1.3f ? "like Raft" : v >= 0.97f ? "jungle" : r < 2f ? "dense" : "very dense";
			};
			Slider(nature, "Trees", 0f, 1f, () => IslandGenerator.AmountOf(s, IslandGenerator.CatTrees), v => s.Trees = v, amount(IslandGenerator.CatTrees), "Palms, pines, birches, cacti... by style",
				"Trees of the style, where Raft's own islands have them (measured): bamboo by the beach, palms from a few metres inland, mango trees further in (tropical); snowy pines; bushy trees and cacti (desert); birches and pines (forest) - on grass, not on the bare beach or rocky cliffs. At the top they stand about 3 m apart: a jungle. Harvestable palms and trees give Raft's wood and fruit.");
			Slider(nature, "Bushes and plants", 0f, 1f, () => IslandGenerator.AmountOf(s, IslandGenerator.CatBushes), v => s.Bushes = v, amount(IslandGenerator.CatBushes), "Undergrowth between the trees",
				"Bushes, ferns, grass and other undergrowth (snow drifts on snowy islands). They fill the gaps between the trees; dense undergrowth makes an island feel wild.");
			Slider(nature, "Rocks", 0f, 1f, () => IslandGenerator.AmountOf(s, IslandGenerator.CatRocks), v => s.Rocks = v, amount(IslandGenerator.CatRocks), "Boulders on steep and high ground",
				"Boulders and stones, mostly on steep slopes and high ground, some on the land and beach. Big ones are scaled down so they don't swamp the island.");
			Slider(nature, "Beach things", 0f, 1f, () => IslandGenerator.AmountOf(s, IslandGenerator.CatBeach), v => s.BeachThings = v, amount(IslandGenerator.CatBeach), "Driftwood and stones on the beach",
				"Driftwood logs and small stones on the beach and the wet sand.");
			Slider(nature, "Harvestables", 0f, 1f, () => IslandGenerator.AmountOf(s, IslandGenerator.CatHarvest), v => s.Harvest = v, amount(IslandGenerator.CatHarvest), "Stone, clay, sand, berries, pineapples on the land",
				"Things players collect with Raft's tools on the land and beach: stones, clay and sand, berry bushes or pineapples by style. (What lies under water is below, in Life under water.)");
			Slider(nature, "Things to gather", 0f, 1f, () => s.Gather, v => s.Gather = v, v => v <= 0.01f ? "none" : v < 0.35f ? "a little" : v < 0.7f ? "some" : "much",
				"Trees to cut, fruit, berries and flowers that suit the style", "More of what players gather and cut on the land, chosen by the style: palms, mango trees, pineapples, watermelons, bananas and flowers (tropical); pines, berry bushes and flowers (snowy); palms, pineapples, watermelons and flowers (desert); birches, pines, berry bushes and flowers (forest); palms, mango trees, pineapples and black flowers (volcanic). On fairly flat land above the sea, kept off the island's other objects; at the top about " + GenGather.LandPer1000.ToString("F0") + " per 1000 m2 of land. Off (none) as before.");
			GatherKinds(nature, GenGather.LandKeys, GenGather.LandLabels, true);
			Slider(nature, "Groups", 0f, 1f, () => s.Clusters, v => s.Clusters = v, v => v < 0.15f ? "spread evenly" : v < 0.6f ? "some groves" : "groves and clearings",
				"Spread evenly, or in groves with clearings", "How much the objects gather: evenly spread on the left; groves of trees, fields of bushes and rock piles with open clearings between them on the right, and corals in reefs with sand between them. The amounts stay the same.");
			UIKit.Label(nature, "Very dense islands take a few seconds to generate; one island gets at most " + IslandGenerator.MaxObjects + " objects (land and sea together).", 12, UIKit.TextMuted);

			// Under water: dressed like Raft's own islands of the style (RaftUnderwater: measured with CIMeasureUnderwater)
			RectTransform life = UIKit.Group(root, "Life under water");
			RectTransform sq = UIKit.Row(life, 26f, 4f, "QuickSea");
			foreach (var q in new[] { new KeyValuePair<string, float>("None", 0f), new KeyValuePair<string, float>("Sparse", 0.3f), new KeyValuePair<string, float>("Like Raft", 0.5f), new KeyValuePair<string, float>("Rich", 0.72f), new KeyValuePair<string, float>("Teeming", 1f) })
			{
				float v = q.Value;
				UIKit.Button(sq, q.Key, () => { s.Water = s.SeaRocks = s.SeaFinds = s.Sunken = v; ShowAll(); }, "Set every slider of the life under water at once" + (v == 0.5f ? " (as around Raft's islands)" : ""), -1, 26f, 12);
			}
			UIKit.Help(sq, "The sea around the island is dressed like Raft's own islands of its style, from measurements of them: which corals, sea plants, rocks, pickups and sunken things lie there, how dense they are at each depth, how far from the coast, and on how steep ground. " +
				"Tropical islands get Raft's coral reefs, sea vines with seaweed, stones, clay, sand, scrap, metal and copper ore on the steep slopes and giant clams; forest islands Balboa's sunken barrels and ore; desert islands Caravan's sea vines and red rock; snowy islands Temperance's bare, icy rock. " +
				"Like Raft sets them as dense as there; Teeming is twice that.");
			Func<float, string> sea = v => v <= 0.01f ? "none" : v < 0.3f ? "sparse" : v < 0.44f ? "less than Raft" : v < 0.57f ? "like Raft" : v < 0.8f ? "richer than Raft" : "teeming";
			Slider(life, "Corals and plants", 0f, 1f, () => s.AmountSea(s.Water), v => s.Water = v, sea, "Corals, sea vines (with seaweed) and kelp",
				"Corals of Raft's reefs, sea vines with seaweed to pick, and tall kelp, at the depths they grow at around Raft's islands (most 4-25 m down). They gather in reefs (Groups). Separate from the land, so an island can have a rich reef and a bare top.");
			Slider(life, "Rocks", 0f, 1f, () => s.AmountSea(s.SeaRocks), v => s.SeaRocks = v, sea, "Boulders near the shore, rock formations on the drop-off",
				"Boulders in the shallow water and big rock formations sunk into the drop-off further down, as around Raft's islands.");
			Slider(life, "Things to collect", 0f, 1f, () => s.AmountSea(s.SeaFinds), v => s.SeaFinds = v, sea, "Stones, clay, sand, scrap, ores, giant clams",
				"Raft's pickups under water: stones, clay and sand on the shelf, scrap on the sea floor, metal and copper ore on the steep slopes further down, giant clams and silver algae now and then. Players dive for them.");
			Slider(life, "Finds in the shallows", 0f, 1f, () => s.Shallows, v => s.Shallows = v, v => v <= 0.01f ? "none" : v < 0.35f ? "a little" : v < 0.7f ? "some" : "much",
				"Sand, clay, stone, ores, scrap, clams and seaweed just off the shore", "Raft's sea finds where players reach them easily, 0.6-6 m under water just off the shore (How far out below sets the depth): sand, clay, stones, metal and copper ore, scrap, giant clams and seaweed - as the library's islands have them. On top of Things to collect above; at the top about " + GenGather.SeaPer1000.ToString("F0") + " per 1000 m2 of shallows. Off (none) as before.");
			GatherKinds(life, GenGather.SeaKeys, GenGather.SeaLabels, false);
			Slider(life, "How far out", GenGather.MinDepth, GenGather.MaxDepth, () => s.ShallowsDepth, v => s.ShallowsDepth = v, v => "down to " + v.ToString("F0") + " m" + (v < 4f ? " (at the shore)" : v < 8f ? " (just off it)" : v < 13f ? " (out on the shelf)" : " (far out)"),
				"How far out (how deep) the finds in the shallows go", "Finds in the shallows lie from 0.6 m under water down to this depth: on the left only right at the shore, where players wade for them; to the right further out over the shelf, where they dive. 6 m as before. The same amount (Finds in the shallows) spread over more water.");
			Slider(life, "Sunken barrels", 0f, 1f, () => s.AmountSea(s.Sunken), v => s.Sunken = v, sea, "Barrels, containers, buoys and wreckage on the sea floor",
				"Sunken barrels, containers, buoys and bits of wreckage, as around Balboa and Caravan Island (Raft's tropical islands have few). Decoration: the loot boxes are under Loot.");

			RectTransform animals = UIKit.Group(root, "Animals");
			Slider(animals, "Hostile creatures", 0, IslandGenSettings.MaxCreatureSpots, () => s.Hostiles, v => s.Hostiles = Mathf.RoundToInt(v), v => v < 0.5f ? "none" : v.ToString("F0") + " spot(s)",
				"Spots where hostile animals live", "Places on the land where hostile animals live (a spot can hold a small pack: rats and hyenas come in twos and threes). They are Raft's own animals in a world. Below: which kinds, and how tough.", true);
			RectTransform kinds = UIKit.Rect("Kinds", animals);
			UIKit.Vertical(kinds.gameObject, 3f, new RectOffset(0, 0, 0, 0));
			RectTransform row = null;
			var hostile = ContentCatalog.Creatures.Where(c => c.Category == ContentCatalog.HostileCategory).ToList();
			var own = UIKit.Row(kinds, 24f, 3f, "Own");
			Button styleOwn = UIKit.Button(own, "The style's own", () => { s.HostileKinds = ""; ShowAll(); }, "Use the animals that fit the island's style", -1, 24f, 11);
			UIKit.Help(own, "Which hostile animals appear. \"The style's own\": warthogs and screechers on tropical islands, polar bears on snowy ones, hyenas and rats in the desert, bears, warthogs and bees in the forest, rats, roaches and screechers on volcanic islands. Or click the kinds you want (several at once).");
			refresh.Add(() => UIKit.SetActive(styleOwn, (s.HostileKinds ?? "").Length == 0));
			for (int i = 0; i < hostile.Count; i++)
			{
				if (i % 4 == 0) row = UIKit.Row(kinds, 24f, 3f, "KindRow");
				string type = hostile[i].Type.ToString();
				Button b = UIKit.Button(row, hostile[i].Label, () => ToggleKind(type), "Hostile " + hostile[i].Label.ToLowerInvariant() + ": " + hostile[i].Hint, -1, 24f, 11);
				refresh.Add(() => UIKit.SetActive(b, KindChosen(type)));
			}
			Choice(animals, "Toughness", ObjectProps.Presets.Select(p => p.Key).ToArray(), () => Array.FindIndex(ObjectProps.Presets, p => p.Key == s.Difficulty), v => s.Difficulty = ObjectProps.Presets[v].Key,
				"How tough the hostile animals are: Easy (half health and damage), Normal (Raft's own), Hard (twice the health, 1.5 x damage) or Boss (four times the health). Each spot can still be changed by hand afterwards.");
			Slider(animals, "Friendly animals", 0, IslandGenSettings.MaxCreatureSpots, () => s.Friendly, v => s.Friendly = Mathf.RoundToInt(v), v => v < 0.5f ? "none" : v.ToString("F0") + " spot(s)",
				"Chickens, goats, llamas to catch", "Animals players can catch with Raft's net launcher and keep on the raft: chickens, goats and llamas, by style. They live on flat ground.", true);
			Slider(animals, "Sea creatures", 0, IslandGenSettings.MaxCreatureSpots, () => s.SeaLife, v => s.SeaLife = Mathf.RoundToInt(v), v => v < 0.5f ? "none" : v.ToString("F0") + " spot(s)",
				"Turtles, stingrays, puffer fish, dolphins", "Sea life in the shallow water around the island: turtles, stingrays, puffer fish (which explode next to divers) and dolphins.", true);

			RectTransform loot = UIKit.Group(root, "Loot");
			Slider(loot, "Loot boxes", 0, IslandGenSettings.MaxLoot, () => s.Loot, v => s.Loot = Mathf.RoundToInt(v), v => v < 0.5f ? "none" : v.ToString("F0"),
				"Boxes with items to find", "Boxes, chests and crates with items for players to find. About one in five lies sunken in the shallow water. Their tier decides what is inside and what they look like. Each box can be changed by hand afterwards.", true);
			Slider(loot, "Lowest tier", 1, 5, () => s.LootMin, v => { s.LootMin = Mathf.RoundToInt(v); if (s.LootMax < s.LootMin) { s.LootMax = s.LootMin; ShowAll(); } }, v => TierName(Mathf.RoundToInt(v)),
				"The poorest boxes", "Boxes are rolled between the lowest and the highest tier. Tier 1: planks, plastic, thatch, rope (a barrel or box). 2: nails, stone, scrap and some food (a small chest). 3: metal and copper ore, bolts, hinges (a crate). 4: metal and copper ingots, circuit boards (a chest). 5: titanium, explosive goo, batteries, good healing salves (a large chest).", true);
			Slider(loot, "Highest tier", 1, 5, () => s.LootMax, v => { s.LootMax = Mathf.RoundToInt(v); if (s.LootMin > s.LootMax) { s.LootMin = s.LootMax; ShowAll(); } }, v => TierName(Mathf.RoundToInt(v)),
				"The richest boxes", "The best tier a box can have (see Lowest tier for what each tier holds). Set both the same for boxes of one tier only.", true);
			Choice(loot, "Placed", new[] { "In the open", "Hidden" }, () => s.LootHidden ? 1 : 0, v => s.LootHidden = v == 1,
				"In the open: boxes stand on clear ground where players see them. Hidden: tucked in next to trees, bushes and rocks, so players have to search.");

			// Buildings and caves (the user, 2026-10-03)
			RectTransform built = UIKit.Group(root, "Buildings and caves");
			Choice(built, "Buildings", new[] { "Off", "On" }, () => s.Buildings ? 1 : 0, v => s.Buildings = v == 1,
				"Buildings on the island's open, level land: huts and cabins of Raft's building blocks (on foundations, the ground built up under them, a roof on their walls and pillars) or scenes from the quest islands, each with a chest and often a note. Kind and How many choose them.");
			List<string> buildKinds = GenBuildings.Kinds;
			Stepper(built, "Kind", "Which buildings: mixed, huts, cabins or one of the quest islands' scenes",
				"Mixed: a bit of everything that suits the island's style. Wrecks and landmarks: boats run aground on the beach or sunk off the coast, plane wrecks on the land or under water, small boats pulled up the beach, vans, caravans, shacks, statues on high ground and rocket debris - Raft's own set pieces, a chest by the ones on land. Castaway huts: Raft's thatch walls and roof, a hammock and a chest. Wooden cabins: Raft's wooden walls and roof, a bed, a chest and a log. The scenes (castaways' camp, caravan outpost, radio outpost, scrapyard, old market, bear country, frozen camp, hotel garden) are made of the quest islands' own props, with a chest and a note - a scene whose props this Raft hasn't loaded is left out.",
				() => GenBuildings.Label(s.BuildingKind) + (s.Buildings ? "" : " (Buildings off)"), step => { int i = Mathf.Max(0, buildKinds.IndexOf(s.BuildingKind)); s.BuildingKind = buildKinds[((i + step) % buildKinds.Count + buildKinds.Count) % buildKinds.Count]; },
				Enumerable.Range(0, buildKinds.Count).Select(i => new DropList.Option(i.ToString(), GenBuildings.Label(buildKinds[i]), GenBuildings.Hint(buildKinds[i]))).ToList(), () => Mathf.Max(0, buildKinds.IndexOf(s.BuildingKind)), i => s.BuildingKind = buildKinds[i]);
			Slider(built, "How many", 0, GenBuildings.MaxCount, () => s.BuildingCount, v => s.BuildingCount = Mathf.RoundToInt(v), v => v.ToString("F0"),
				"How many buildings or scenes", "How many buildings, scenes, wrecks and landmarks the island gets (0-" + GenBuildings.MaxCount + "), at least 22 m apart: buildings on open, level land, wrecks also on the beach and the sea floor. A small or steep island has room for fewer: the report under the preview says how many found a spot.", true);
			Choice(built, "Caves", new[] { "Off", "On" }, () => s.Caves ? 1 : 0, v => s.Caves = v == 1,
				"A cave set into the land: one of Raft's own cave pieces (Balboa's), its mouth towards open, level ground, with a guard (a bear, polar bear, hyena, warthog or rat by style) and a hoard inside. It needs a hill next to open, level land; if none fits, the report says so.");
			QuestGroup(root);
		}

		/// <summary>The kinds of things to gather the builder switches on and off (GatherOff, ROADMAP LM9): on the land only the
		/// style's own (the others hidden), in the shallows all of Raft's sea finds. "All" switches the group's kinds on again.</summary>
		void GatherKinds(Transform parent, string[] keys, string[] labels, bool land)
		{
			RectTransform own = UIKit.Row(parent, 24f, 3f, land ? "GatherAll" : "ShallowsAll");
			Button all = UIKit.Button(own, "All kinds", () => { foreach (string k in keys) if (!GenGather.IsOn(s.GatherOff, k)) s.GatherOff = GenGather.Toggle(s.GatherOff, k); ShowAll(); },
				land ? "Every kind of thing to gather the style has" : "Every kind of find in the shallows", -1, 24f, 11);
			UIKit.Help(own, land
				? "Which things to gather come: click a kind to switch it off or on again (lit = on). Only the island's style's own kinds are listed: palms, mangoes, pineapples, watermelons, bananas, flowers, dirt and wild beehives (tropical); pines, berries and flowers (snowy); palms, pineapples, watermelons and flowers (desert); birches, pines, berries, flowers, dirt and beehives (forest); palms, mangoes, pineapples, flowers, dirt and beehives (volcanic). With every kind off, Things to gather adds nothing. Saved with the settings and in recipes (GatherOff); all on as before."
				: "Which finds lie in the shallows: click a kind to switch it off or on again (lit = on) - sand, clay, stone, iron and copper ore, scrap, giant clams and seaweed. With every kind off, Finds in the shallows adds nothing. Saved with the settings and in recipes (GatherOff); all on as before.");
			refresh.Add(() => UIKit.SetActive(all, keys.All(k => GenGather.IsOn(s.GatherOff, k))));
			const int perRow = 6;
			for (int r = 0; r * perRow < keys.Length; r++)
			{
				RectTransform row = UIKit.Row(parent, 24f, 3f, land ? "GatherKinds" : "ShallowsKinds");
				var inRow = new List<KeyValuePair<Button, string>>();
				for (int i = r * perRow; i < Math.Min(keys.Length, (r + 1) * perRow); i++)
				{
					string key = keys[i];
					Button b = UIKit.Button(row, labels[i], () => { s.GatherOff = GenGather.Toggle(s.GatherOff, key); ShowAll(); },
						labels[i] + ": switch " + (land ? "this thing to gather" : "this find") + " on or off", -1, 24f, 11);
					b.name = "Gather_" + key;
					inRow.Add(new KeyValuePair<Button, string>(b, key));
				}
				GameObject rowObject = row.gameObject;
				refresh.Add(() =>
				{
					List<string> offered = land ? GenGather.LandKeysOf(s.Style) : null;
					bool any = false;
					foreach (var kb in inRow)
					{
						bool shown = offered == null || offered.Contains(kb.Value);
						kb.Key.gameObject.SetActive(shown);
						UIKit.SetActive(kb.Key, GenGather.IsOn(s.GatherOff, kb.Value));
						any |= shown;
					}
					rowObject.SetActive(any);
				});
			}
		}

		/// <summary>A quest made with the island: how many steps (Normal and Randomize existing tabs; the user, 2026-10-03).</summary>
		void QuestGroup(Transform root)
		{
			RectTransform quest = UIKit.Group(root, "Quest");
			Slider(quest, "Quest steps", 0, GenQuest.MaxSteps, () => s.QuestSteps, v => s.QuestSteps = Mathf.RoundToInt(v), v => v < 0.5f ? "no quest" : v.ToString("F0") + " step" + (v < 1.5f ? "" : "s"),
				"A quest made with the island", "A quest of this many steps, made with the island and its things: first a castaway's note to read where players come ashore, then (by the seed) a lookout to climb to, monsters to defeat, map pieces to collect from small chests, a torn page to find, a supply crate, animals to catch - and last the castaway's hoard at the top of the island. One step: just the hidden hoard. It replaces the island's quest; the Island tab's Quest shows it and you can change it there. A step whose place doesn't fit on a small island is left out (the report says so).", true);
			Slider(quest, "Raft's features", 0, GenFeatures.Max, () => s.Features, v => s.Features = Mathf.RoundToInt(v), v => v < 0.5f ? "none" : v.ToString("F0"),
				"Things Raft's story islands have the player do", "Things Raft's story islands have the player do, put on the island (different ones first): a cache behind vines for the machete, treasure buried for the metal detector and the shovel, a zipline from the top down to the beach, a strongbox behind a code panel (its code on a note elsewhere), a grove of wild beehives, dirt for the shovel. Past six, Raft's story machinery: a generator to start with a part from a toolbox and a radio that then works, an engine to start with fuel from a crate, a chest in a cage for Raft's bolt cutters, a lift up a cliff. An explorer's chest near the shore holds the tools they need.", true);
		}

		/// <summary>Deep sea floor like Raft's, or the older flat seabed 20 m down.</summary>
		void SeaFloorChoice(Transform parent)
		{
			Choice(parent, "Sea floor", new[] { "Deep, like Raft", "Shallow (20 m)" }, () => s.SeaFloor, v => s.SeaFloor = v,
				"Deep, like Raft: the island rises from a sea floor 160 m down, as Raft's own islands do - a shallow shelf around it, then a drop-off into the dark blue with spurs, gullies and big rock formations " +
				"(a Raft island's own ground keeps its real depths). Shallow: a flat seabed 20 m down all around, as islands had before; easier to build on under water.");
		}

		static string TierName(int t)
		{
			switch (t)
			{
				case 1: return "1: planks, plastic";
				case 2: return "2: nails, scrap, food";
				case 3: return "3: ores, bolts, hinges";
				case 4: return "4: ingots, circuits";
				default: return "5: titanium, batteries";
			}
		}

		bool KindChosen(string type) { return (s.HostileKinds ?? "").Split(',').Contains(type); }

		void ToggleKind(string type)
		{
			var list = (s.HostileKinds ?? "").Split(',').Where(k => k.Length > 0).ToList();
			if (list.Contains(type)) list.Remove(type); else list.Add(type);
			s.HostileKinds = string.Join(",", list.ToArray());
			if (s.Hostiles == 0 && list.Count > 0) s.Hostiles = 3;
			ShowAll();
		}

		#endregion

		#region Randomize existing tab

		void BuildRandomize(Transform root)
		{
			RectTransform pick = UIKit.Group(root, "Raft's islands");
			var offered = RaftIslands.Offered;
			if (offered.Count == 0) UIKit.Label(pick, "<i>No measured islands: raft_islands.txt is missing (the dev command CIMeasureIslands makes it).</i>", 13, UIKit.TextMuted);
			RectTransform row = null;
			for (int i = 0; i < offered.Count; i++)
			{
				if (i % 3 == 0) row = UIKit.Row(pick, 132f, 6f, "IslandRow");
				RaftIsland isl = offered[i];
				Button tile = UIKit.Button(row, "", () => Choose(isl), isl.Label + ": land about " + isl.Length.ToString("F0") + " x " + isl.Width.ToString("F0") + " m, " + isl.Top.ToString("F0") + " m high, " + isl.Style.ToLowerInvariant(), -1, 132f, 12);
				UIKit.Slot(tile);
				Text label = UIKit.LabelOf(tile);
				label.text = isl.Label;
				label.alignment = TextAnchor.LowerCenter;
				label.rectTransform.offsetMin = new Vector2(4, 3); label.rectTransform.offsetMax = new Vector2(-4, -3);
				RectTransform pic = UIKit.Rect("Thumb", tile.transform);
				pic.anchorMin = new Vector2(0, 0); pic.anchorMax = new Vector2(1, 1); pic.offsetMin = new Vector2(5, 22); pic.offsetMax = new Vector2(-5, -5);
				var img = pic.gameObject.AddComponent<RawImage>();
				img.raycastTarget = false;
				img.texture = RaftIslands.Thumbnail(isl);
				if (img.texture == null) img.color = new Color(0.2f, 0.4f, 0.55f, 1f);
				islandTiles.Add(new KeyValuePair<Button, RaftIsland>(tile, isl));
			}
			// (an unfilled last row keeps its tiles at the same width)
			if (row != null) for (int k = offered.Count % 3; k > 0 && k < 3; k++) UIKit.Size(UIKit.Rect("Spacer", row).gameObject, -1, 132f);

			RectTransform mode = UIKit.Group(root, "The chosen island");
			chosenText = UIKit.Label(mode, "Pick an island above.", 13, UIKit.TextColor);
			RectTransform modes = UIKit.Row(mode, 30f, 6f, "Modes");
			Button like = UIKit.Button(modes, "Something new like it", () => SetVariation(false), "A new island with its size, height, peaks, coast, style and object mix (a random shape)", -1, 30f, 13);
			Button vari = UIKit.Button(modes, "A variation of it", () => SetVariation(true), "Starts from the island's own ground: scale, stretch, roughen, erode and reshape it", -1, 30f, 13);
			UIKit.Help(modes, "Something new like it: its measurements (land size, highest point, peaks, slopes, coast, stretch, shallow water, style, how dense its trees, bushes and rocks are) fill in the generator's settings, and a new random shape is made. A variation of it: the island's own ground (measured from Raft) is the start, and you change its shape below.");
			refresh.Add(() => { UIKit.SetActive(like, chosen != null && !variation); UIKit.SetActive(vari, chosen != null && variation); });

			likeGroup = UIKit.Group(root, "Something new like it");
			likeText = UIKit.Label(likeGroup, "", 12, UIKit.TextMuted);
			UIKit.Button(likeGroup, "Change these settings in the Normal tab", () => SetTab(TabNormal), "All of the generator's settings, filled in from the island", -1, 26f, 12);

			variationGroup = UIKit.Group(root, "Change its shape");
			Slider(variationGroup, "Size", IslandGenSettings.MinRadius, IslandGenSettings.MaxSourceRadius, () => s.Radius, v => s.Radius = v, v => "land about " + (v * 2f).ToString("F0") + " m across",
				"Scales the island", "Scales the island's ground to this size (the heights follow Highest point). Its own size is the starting value.");
			Slider(variationGroup, "Highest point", IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight, () => s.Height, v => s.Height = v, v => v.ToString("F0") + " m above the sea",
				"Raises or lowers its hills", "Raises or lowers the land so its top is this high; the coast stays where it is.");
			RectTransform original = UIKit.Row(variationGroup, 26f, 4f, "Original");
			UIKit.Button(original, "Its own size and height", () => { if (chosen != null) { s.Radius = chosen.Radius; s.Height = chosen.Top; ShowAll(); } }, "Back to the island's measured size and height", -1, 26f, 12);
			UIKit.Button(original, "Mirror", () => { s.Mirror = !s.Mirror; ShowAll(); }, "Mirror it (left becomes right)", 90, 26f, 12);
			Button mirror = original.GetComponentsInChildren<Button>().Last();
			refresh.Add(() => UIKit.SetActive(mirror, s.Mirror));
			Slider(variationGroup, "Stretch", 1f, IslandGenSettings.MaxStretch, () => s.Stretch, v => s.Stretch = v, v => v < 1.05f ? "as it is" : v.ToString("0.0") + " times longer",
				"Longer and narrower", "Stretches the island along Direction (the area stays the same).");
			Slider(variationGroup, "Direction", 0f, 180f, () => s.StretchAngle, v => s.StretchAngle = v, v => v.ToString("F0") + "°", "Which way it is stretched", "Which way the stretch goes (0° = along the x axis).");
			Slider(variationGroup, "Roughen", 0f, 1f, () => s.SourceRoughen, v => s.SourceRoughen = v, v => v <= 0.01f ? "as it is" : v < 0.5f ? "bumpier" : "much bumpier",
				"Bumps on its land", "Adds bumps and ridges to its land (not the sea floor).");
			Slider(variationGroup, "Coast wobble", 0f, 1f, () => s.SourceWobble, v => s.SourceWobble = v, v => v <= 0.01f ? "as it is" : v < 0.5f ? "a little" : "a lot",
				"Bends its outline", "Bends and warps its outline, so the coast goes its own way while the island stays recognisable.");
			Slider(variationGroup, "Peak shape", 0f, 1f, () => s.PeakShape, v => s.PeakShape = v, v => v < 0.15f ? "fuller" : v < 0.45f ? "as it is" : v < 0.75f ? "pointed" : "spires",
				"Fuller hills or sharper peaks", "Left: fuller, rounder hills. Middle: as it is. Right: sharper peaks over flatter lowland.");
			Slider(variationGroup, "Valleys", 0f, 1f, () => s.Valleys, v => s.Valleys = v, v => v <= 0.01f ? "none" : (1 + Mathf.RoundToInt(v * 4f)) + " valley(s)", "River valleys", "Valleys cut from its top down to the sea.");
			Slider(variationGroup, "Lakes", 0f, 1f, () => s.Lakes, v => s.Lakes = v, v => v <= 0.01f ? "none" : "up to " + (1 + Mathf.RoundToInt(v * 3f)), "Lakes in its lower land", "Lakes in its lower land (below sea level, so the sea fills them).");
			Slider(variationGroup, "Terraces", 0f, 1f, () => s.Terraces, v => s.Terraces = v, v => v <= 0.01f ? "none" : v < 0.5f ? "soft steps" : "sharp steps", "Flat steps in its slopes", "Turns its slopes into terraces.");
			Slider(variationGroup, "Erosion", 0f, 1f, () => s.Erosion, v => s.Erosion = v, v => v <= 0.01f ? "none" : v < 0.5f ? "light" : "heavy", "Rain-worn gullies", "Rain washes soil down its slopes: gullies and softer ridges.");
			SeaFloorChoice(variationGroup);
			Choice(variationGroup, "Seabed", new[] { "Sand", "Rocky", "Reef ring" }, () => s.Seabed, v => s.Seabed = v, "The shallow sea floor around it: as measured (Sand), with rocky bumps, or with a reef ring just under the surface.");

			// Rebuild it: a story island's structure made anew from its own pieces (the user, 2026-10-03: the Radio Tower's
			// pieces can become another radio tower, an oil rig, a lighthouse under construction)
			rebuildGroup = UIKit.Group(root, "Rebuild it");
			UIKit.Label(rebuildGroup, "Built anew from the island's own pieces on the new ground, different with every seed.", 12, UIKit.TextMuted);
			Stepper(rebuildGroup, "Design", "What the island's pieces become", "Each of Raft's story islands rebuilds something like it from its own pieces: the Radio Tower's become a new radio tower, an oil rig or a lighthouse being built; Balboa's a logging camp or a relay station; Caravan Town's a caravan circle or a market on scaffold decks; Tangaroa's a seaside café or the founder's garden; Varuna Point's a building site or a half-built tower; Temperance's an igloo village or a weather outpost; Utopia's a water station or a market yard; the Vasagatan's its lounge or its engine yard on the land. Random design picks one by the seed. None keeps only the ground and nature.",
				() => RemakeLabel(), step => { List<string> ids = RemakeIds(); int i = Mathf.Max(0, ids.IndexOf(s.Remake ?? "")); s.Remake = ids[((i + step) % ids.Count + ids.Count) % ids.Count]; },
				RemakeOptions(), () => Mathf.Max(0, RemakeIds().IndexOf(s.Remake ?? "")), i => { List<string> ids = RemakeIds(); if (i < ids.Count) s.Remake = ids[i]; });
			// (the list follows the chosen island)
			DropdownButton designDrop = rebuildGroup.GetComponentsInChildren<DropdownButton>(true).FirstOrDefault(x => x.name == "Drop_Design");
			RaftIsland designsFor = null;
			refresh.Add(() => { if (designDrop != null && chosen != designsFor) { designsFor = chosen; designDrop.Options = RemakeOptions(); } });
			refresh.Add(() => rebuildGroup.gameObject.SetActive(chosen != null && Remakes.For(chosen.Scene).Count > 0));
			// (this tab remakes the chosen island: the Normal tab's other groups aren't shown here - the user, 2026-10-03)
			QuestGroup(root);
			refresh.Add(() =>
			{
				likeGroup.gameObject.SetActive(chosen != null && !variation);
				variationGroup.gameObject.SetActive(chosen != null && variation);
				foreach (var t in islandTiles) UIKit.SetLook(t.Key, t.Value == chosen ? UIKit.Look.Chosen : UIKit.Look.Slot);
			});
		}

		void Choose(RaftIsland island)
		{
			chosen = island;
			List<Remakes.Design> designs = Remakes.For(island.Scene);
			s.Remake = designs.Count > 0 ? designs[0].Scene + Remakes.Any : "";
			ApplyChosen();
			chosenText.text = "<color=#eddeba>" + island.Label + "</color>  (" + island.Style.ToLowerInvariant() + ")\nLand about " + island.Length.ToString("F0") + " x " + island.Width.ToString("F0") + " m, highest point " + island.Top.ToString("F0") +
				" m, " + island.Peaks + " peak(s), shallow water to " + island.Shelf.ToString("F0") + " m from its middle.\n" +
				string.Join(", ", new[] { RaftIslands.Trees, RaftIslands.Bushes, RaftIslands.Rocks, RaftIslands.Harvest }.Where(k => island.Count(k) > 0).Select(k => island.Count(k) + " " + k).ToArray()) +
				(Remakes.LookFor(island.Scene) != null ? "\n<i>Its look: " + Remakes.LookFor(island.Scene).Hint.ToLowerInvariant() + "</i>" : island.Kind == "mesh" ? "\n<i>(a built place: its buildings count as ground)</i>" : "");
			SetStatus((variation ? "A variation of " : "Something new like ") + island.Label + ". Generate makes it.");
		}

		void SetVariation(bool on)
		{
			variation = on;
			if (chosen != null) ApplyChosen();
			else SetStatus("Pick one of Raft's islands first.");
		}

		void ApplyChosen()
		{
			if (chosen == null) return;
			s = variation ? RaftIslands.VariationOf(chosen, s) : RaftIslands.LikeIt(chosen, s);
			if (!variation)
				likeText.text = string.Format(CultureInfo.InvariantCulture, "Settings from {0}: {1} layout, land about {2:F0} m across, {3:F0} m high, {4} peak(s), coast {5:P0}, cliffs {6:P0}, stretch {7:0.0}, trees {8}, bushes {9}, rocks {10}. A new random seed makes a new shape each time.",
					chosen.Label, IslandShapes.Names[s.Shape], s.Radius * 2f, s.Height, s.Peaks, s.CoastAmount, s.Cliffs, s.Stretch, Pct(s.Trees), Pct(s.Bushes), Pct(s.Rocks));
			ShowAll();
		}

		static string Pct(float v) { return (v * 100f).ToString("F0", CultureInfo.InvariantCulture) + " %"; }

		#endregion

		#region Ready-made tab

		readonly List<KeyValuePair<RawImage, int>> cardPictures = new List<KeyValuePair<RawImage, int>>();
		bool cardsDrawn;

		void BuildReady(Transform root)
		{
			RectTransform about = UIKit.Group(root, "Ready-made islands");
			UIKit.Label(about, "A ready-made island is a whole island with a story: its layout and style, and content placed by the generator - chests with loot, notes, creatures, trigger zones, atmosphere and a quest. " +
				"Make saves it as a new island file (gen-<type>-<seed>) and opens it here to change as you like. World plans and the islands that appear while sailing can bring these types too.", 12, UIKit.TextMuted);
			RectTransform row = null;
			for (int i = 0; i < MapTypes.All.Count; i++)
			{
				if (i % 2 == 0) row = UIKit.Row(root, 104f, 6f, "CardRow");
				int index = i;
				MapType t = MapTypes.All[i];
				Button card = UIKit.Button(row, "", () => { mapType = index; ShowAll(); SetStatus(t.Label + ": " + t.Description + ". Make creates it."); }, t.Label + ": " + t.Description, -1, 104f, 12);
				UIKit.Slot(card);
				Text label = UIKit.LabelOf(card);
				label.text = "<b>" + t.Label + "</b>\n<size=11>" + t.Description + "</size>";
				label.supportRichText = true;
				Destroy(label.GetComponent<UIKit.FontFallback>()); // (the body font, not the title font's capitals)
				label.font = UIKit.Font; label.fontSize = 13; label.resizeTextForBestFit = true; label.resizeTextMaxSize = 13; label.resizeTextMinSize = 9;
				label.alignment = TextAnchor.UpperLeft;
				label.rectTransform.offsetMin = new Vector2(104, 6); label.rectTransform.offsetMax = new Vector2(-6, -6);
				RectTransform pic = UIKit.Rect("Picture", card.transform);
				pic.anchorMin = new Vector2(0, 0.5f); pic.anchorMax = new Vector2(0, 0.5f); pic.pivot = new Vector2(0, 0.5f);
				pic.sizeDelta = new Vector2(92, 92); pic.anchoredPosition = new Vector2(6, 0);
				var img = pic.gameObject.AddComponent<RawImage>();
				img.raycastTarget = false;
				cardPictures.Add(new KeyValuePair<RawImage, int>(img, i));
				typeCards.Add(new KeyValuePair<Button, int>(card, i));
			}
			refresh.Add(() => { foreach (var c in typeCards) UIKit.SetLook(c.Key, c.Value == mapType ? UIKit.Look.Chosen : UIKit.Look.Slot); });
		}

		/// <summary>The cards' pictures (each type from a fixed seed), drawn the first time the tab shows.</summary>
		void DrawCards()
		{
			if (cardsDrawn) return;
			cardsDrawn = true;
			foreach (var c in cardPictures)
			{
				try { c.Key.texture = TypePreview(MapTypes.All[c.Value], 20260925, 72); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Picture of map type " + MapTypes.All[c.Value].Name + ": " + e.Message); }
			}
		}

		/// <summary>A small map of what a map type makes from a seed (land from its settings; objects-only types show open water).</summary>
		static Texture2D TypePreview(MapType type, int seed, int res)
		{
			float elevation;
			IslandGenSettings ts = MapTypes.Roll(type, new System.Random(seed), out elevation);
			float span = Mathf.Clamp(IslandGenerator.Reach(ts) * 2.1f, 80f, IslandGenerator.BuildArea.x);
			float[,] m = type.Build != null ? new float[res, res] : IslandGenerator.HeightsMetres(ts, IslandGenerator.BuildArea, res, span);
			return IslandGenerator.Preview(m, span / (res - 1), ts.Style, null, ts.WaterLevel);
		}

		#endregion

		#region Controls with bindings

		/// <summary>A slider bound to a setting: shows it again whenever the settings change (ShowAll).</summary>
		UIKit.SliderRow Slider(Transform parent, string label, float min, float max, Func<float> get, Action<float> set, Func<float, string> format, string hint, string help, bool whole = false)
		{
			UIKit.SliderRow row = UIKit.Slider(parent, label, min, max, get(), format, v => { set(v); Changed(); }, hint, whole, help);
			refresh.Add(() => { float v = Mathf.Clamp(get(), min, max); row.Slider.SetValueWithoutNotify(v); row.Value.text = format(v); });
			return row;
		}

		/// <summary>(the same with the value text and setter swapped, as the Cliffs row reads more naturally)</summary>
		UIKit.SliderRow Slider(Transform parent, string label, float min, float max, Func<float> get, Func<float, string> format, Action<float> set, string hint, string help)
		{
			return Slider(parent, label, min, max, get, set, format, hint, help);
		}

		/// <summary>What each style grows (the style list's lines).</summary>
		static readonly string[] StyleHints =
		{
			"Sand and grass: palms, bushes, Raft's tropical islands", "Snow: pines and drifts", "Sand and dry grass: cacti and desert plants",
			"Forest floor: birches and pines", "Ash and rock: a crater on the highest peak",
		};

		/// <summary>"Label (?)  &lt;  value \u25BC  &gt;": the arrows step through the choices, the middle opens a list of all of them.</summary>
		void Stepper(Transform parent, string label, string hint, string help, Func<string> text, Action<int> step, List<DropList.Option> options, Func<int> get, Action<int> set)
		{
			RectTransform row = UIKit.Row(parent, 30f, 4f, label);
			UIKit.Size(UIKit.Label(row, label, 14, UIKit.TextMuted).gameObject, 62);
			UIKit.Help(row, help);
			UIKit.Button(row, "<", () => { step(-1); ShowAll(); }, "Previous " + label.ToLowerInvariant(), 32);
			Button b = DropList.Make(row, "Drop_" + label, options, get().ToString(), v => { set(int.Parse(v)); ShowAll(); }, -1, hint + " (click for the list)", 30f, 14);
			Text t = UIKit.LabelOf(b);
			t.alignment = TextAnchor.MiddleCenter;
			UIKit.Size(b.gameObject, -1, -1, 1);
			UIKit.Button(row, ">", () => { step(1); ShowAll(); }, "Next " + label.ToLowerInvariant(), 32);
			DropdownButton d = b.GetComponent<DropdownButton>();
			refresh.Add(() => { t.text = text(); if (d != null) d.Value = get().ToString(); });
		}

		/// <summary>"Label (?)  [a] [b] [c]": one of a few choices.</summary>
		void Choice(Transform parent, string label, string[] options, Func<int> get, Action<int> set, string help)
		{
			RectTransform row = UIKit.Row(parent, 26f, 4f, label);
			UIKit.Size(UIKit.Label(row, label, 14, UIKit.TextMuted).gameObject, 84);
			UIKit.Help(row, help);
			var buttons = new Button[options.Length];
			for (int i = 0; i < options.Length; i++)
			{
				int index = i;
				buttons[i] = UIKit.Button(row, options[i], () => { set(index); ShowAll(); Changed(); }, label + ": " + options[i], -1, 26f, 12);
			}
			refresh.Add(() => { int v = get(); for (int i = 0; i < buttons.Length; i++) UIKit.SetActive(buttons[i], i == v); });
		}

		/// <summary>A button that sets a value measured from Raft's islands, labelled with it.</summary>
		void PresetButton(Transform row, string name, Func<float> value, Action<float> set, Func<float, string> format, string hintPrefix)
		{
			Button b = UIKit.Button(row, name, () => { set(value()); ShowAll(); Changed(); SetStatus(name + ": " + format(value()) + ", measured on Raft's own islands."); }, hintPrefix + format(value()), -1, 26f, 12);
			Text t = UIKit.LabelOf(b);
			refresh.Add(() => t.text = name + " (" + format(value()) + ")");
		}

		/// <summary>Shows the settings on every control, and the tab's state; the preview follows.</summary>
		void ShowAll()
		{
			if (seedField != null && !seedField.isFocused) seedField.text = s.Seed.ToString(CultureInfo.InvariantCulture);
			foreach (Action a in refresh)
				try { a(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Generator window: " + e.Message); }
			Changed();
		}

		void Changed()
		{
			previewDue = Time.unscaledTime + 0.15f;
			// (the reach line looks at the ground at full detail: a little later, once a slider stops moving)
			reachDue = Time.unscaledTime + 0.4f;
			if (reachText != null && reachText.text.Length > 0 && !reachText.text.StartsWith("<color=#8c8c8c>")) reachText.text = "<color=#8c8c8c>" + StripColour(reachText.text) + "</color>";
		}

		static string StripColour(string t) { return System.Text.RegularExpressions.Regex.Replace(t ?? "", "</?color[^>]*>", ""); }

		void SetTab(int t)
		{
			tab = t;
			for (int i = 0; i < tabRoots.Length; i++) tabRoots[i].parent.parent.parent.gameObject.SetActive(i == t);
			for (int i = 0; i < tabButtons.Length; i++) UIKit.SetActive(tabButtons[i], i == t);
			if (generateLabel != null) generateLabel.text = t == TabReady ? "Make" : "Generate";
			// (Normal makes islands from a layout; the variation of a Raft island lives on the other tab)
			if (t == TabNormal) OwnSettings();
			if (t == TabNormal && s.Source.Length > 0) { s.Source = ""; SetStatus("The Normal tab makes islands from a layout (the Raft island's ground stays on Randomize existing)."); }
			// (back on Randomize existing, a variation starts from its island's ground again; the other settings stay as they were)
			if (t == TabRandomize && chosen != null && variation) s.Source = chosen.Scene;
			if (t == TabReady) DrawCards();
			ShowAll();
		}

		#endregion

		#region Preview

		void UpdatePreview()
		{
			if (preview == null) return;
			try
			{
				const int Res = 160;
				IslandGenSettings ps;
				float elevation = 0f;
				MapType type = null;
				if (tab == TabReady)
				{
					type = MapTypes.All[Mathf.Clamp(mapType, 0, MapTypes.All.Count - 1)];
					ps = MapTypes.Roll(type, new System.Random(s.Seed), out elevation);
				}
				else ps = tab == TabRandomize ? Effective() : s; // (Randomize: its extra under-water kinds count in the estimate too - CA29)
				Vector3 terrainSize = IslandGenerator.AreaFor(ps); // (LM7: a variation of Temperance is bigger than the build area)
				float span = Mathf.Clamp(IslandGenerator.Reach(ps) * 2.1f, 60f, terrainSize.x);
				float step = span / (Res - 1);
				float[,] m = type != null && type.Build != null ? new float[Res, Res] : IslandGenerator.HeightsMetres(ps, terrainSize, Res, span);
				previewTex = IslandGenerator.Preview(m, step, ps.Style, previewTex, ps.WaterLevel);
				preview.texture = previewTex;
				// What it shows
				int land = 0; float top = 0f;
				foreach (float v in m) if (v > ps.WaterLevel) { land++; top = Mathf.Max(top, v - ps.WaterLevel); }
				float area = land * step * step;
				string where = elevation > 1f ? ", floats " + elevation.ToString("F0") + " m up" : elevation < -1f ? ", " + (-elevation).ToString("F0") + " m under water" : "";
				info.text = "View " + span.ToString("F0") + " m across · " + (type != null && type.Build != null ? "no land: " + type.Description.ToLowerInvariant() :
					"land " + area.ToString("N0", CultureInfo.InvariantCulture) + " m² (about " + (2f * Mathf.Sqrt(area / Mathf.PI)).ToString("F0") + " m across) · top " + top.ToString("F0") + " m") + where +
					"\n" + (type != null ? type.Label + " from seed " + s.Seed : TerrainPainter.StyleName(ps.Style) + ", " + (ps.Source.Length > 0 && chosen != null ? "from " + chosen.Label : IslandShapes.Names[ps.Shape].ToLowerInvariant()) + ", seed " + ps.Seed);
				if (type == null)
				{
					Dictionary<string, int> est = IslandGenerator.Estimate(ps, m, step);
					int total = est.Values.Sum();
					estimate.text = "About " + total.ToString("N0", CultureInfo.InvariantCulture) + " objects: " + string.Join(", ", IslandGenerator.Categories.Where(c => est[c] > 0).Select(c => est[c].ToString("N0", CultureInfo.InvariantCulture) + " " + IslandGenerator.CategoryLabel(c)).ToArray()) +
						(ps.Hostiles + ps.Friendly + ps.SeaLife > 0 ? "; " + (ps.Hostiles + ps.Friendly + ps.SeaLife) + " creature spots" : "") + (ps.Loot > 0 ? "; " + ps.Loot + " loot boxes" : "");
				}
				else estimate.text = "Content: chests, creatures and a quest of this type.";
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Generator preview: " + e);
				info.text = "(no preview: " + e.Message + ")";
			}
		}

		ReachResult lastReach;

		/// <summary>The "can players reach it" line above the seed: from the ground the settings (or the ready-made type) make.</summary>
		void UpdateReach()
		{
			if (reachText == null) return;
			try
			{
				IslandGenSettings ps;
				float elevation = 0f;
				if (tab == TabReady)
				{
					MapType type = MapTypes.All[Mathf.Clamp(mapType, 0, MapTypes.All.Count - 1)];
					if (type.Build != null)
					{
						lastReach = new ReachResult { Level = IslandReach.Easy, Text = "Easy: no land, just a raft of Raft's blocks on open water - swim over and climb aboard." };
						reachText.text = "<color=" + IslandReach.ColourOf(lastReach.Level) + ">" + lastReach.Text + "</color>";
						return;
					}
					ps = MapTypes.Roll(type, new System.Random(s.Seed), out elevation);
				}
				else if (tab == TabRandomize && chosen == null) { lastReach = null; reachText.text = "<color=#8c8c8c>Pick one of Raft's islands to see whether players can get onto it.</color>"; return; }
				else ps = Effective(); // (what Generate makes: on Randomize existing no caves or buildings, so the line matches the ground)
				float t0 = Time.realtimeSinceStartup;
				lastReach = IslandReach.Assess(ps, elevation);
				reachText.text = "<color=" + IslandReach.ColourOf(lastReach.Level) + ">" + lastReach.Text + "</color>";
				ReachSeconds = Time.realtimeSinceStartup - t0;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Generator reach: " + e);
				reachText.text = "(could not work out whether players can reach it: " + e.Message + ")";
			}
		}

		/// <summary>How long the last reach check took (tests).</summary>
		public static float ReachSeconds;

		/// <summary>The reach line for the window's settings, worked out now (tests).</summary>
		public static ReachResult ReachNow() { if (instance == null) return null; instance.UpdateReach(); return instance.lastReach; }

		/// <summary>The reach line's text as shown (tests).</summary>
		public static string ReachText { get { return instance != null && instance.reachText != null ? StripColour(instance.reachText.text) : ""; } }

		#endregion

		#region Presets of one's own

		public static string PresetFolder { get { return Path.Combine(DynamicIslands.assetpath, "generator_presets"); } }

		public static List<string> PresetNames()
		{
			try { return Directory.Exists(PresetFolder) ? Directory.GetFiles(PresetFolder, "*.txt").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList() : new List<string>(); }
			catch { return new List<string>(); }
		}

		void RefreshPresets()
		{
			if (presetsRoot == null) return;
			foreach (Transform child in presetsRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			List<string> names = PresetNames();
			if (names.Count == 0) { UIKit.Label(presetsRoot, "<i>None yet: set things up below, then Save these settings.</i>", 12, UIKit.TextMuted); return; }
			RectTransform row = null;
			for (int i = 0; i < names.Count; i++)
			{
				if (i % 3 == 0) row = UIKit.Row(presetsRoot, 26f, 4f, "PresetRow");
				string name = names[i];
				UIKit.Button(row, name, () => UsePreset(name), "Use your preset '" + name + "' (the seed stays)", -1, 26f, 12);
				Button del = UIKit.Button(row, "×", () => DeletePreset(name), "Delete the preset '" + name + "' (click twice)", 24, 26f, 13);
				UIKit.DangerButton(del);
			}
		}

		string saveArmed;
		float saveUntil;

		void SavePreset()
		{
			TextPromptWindow.Open("Save generator preset", "All of the generator's settings on this tab (not the seed) are kept under this name, to use again with any seed.", "my island", name =>
			{
				string safe = new string((name ?? "").Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
				if (safe.Length == 0) { SetStatus("A preset needs a name."); return; }
				// (a name in use: replaced only when saved under it again - two steps, like ×)
				if (File.Exists(Path.Combine(PresetFolder, safe + ".txt")) && (!safe.Equals(saveArmed, StringComparison.OrdinalIgnoreCase) || Time.unscaledTime > saveUntil))
				{
					saveArmed = safe; saveUntil = Time.unscaledTime + 10f;
					SetStatus("There is a preset '" + safe + "' already: Save these settings under that name again to replace it.");
					return;
				}
				saveArmed = null;
				Directory.CreateDirectory(PresetFolder);
				IslandGenSettings keep = s.Copy();
				keep.Source = ""; // (a variation of a Raft island isn't a preset for the Normal tab)
				SafeFile.WriteAllText(Path.Combine(PresetFolder, safe + ".txt"), keep.ToText());
				RefreshPresets();
				SetStatus("Saved the preset '" + safe + "'.");
			});
		}

		void UsePreset(string name)
		{
			try
			{
				IslandGenSettings p = IslandGenSettings.FromText(File.ReadAllText(Path.Combine(PresetFolder, name + ".txt")));
				p.Seed = s.Seed;
				s = p;
				ShowAll();
				SetStatus("Using the preset '" + name + "'. Generate makes it.");
			}
			catch (Exception e) { SetStatus("Could not read the preset '" + name + "': " + e.Message); }
		}

		string deleteArmed;
		float deleteUntil;

		void DeletePreset(string name)
		{
			if (deleteArmed != name || Time.unscaledTime > deleteUntil)
			{
				deleteArmed = name; deleteUntil = Time.unscaledTime + 4f;
				SetStatus("Click × again to delete the preset '" + name + "'.");
				return;
			}
			deleteArmed = null;
			// (to deleted\presets, not deleted for good, like the rest of the mod's files - AU41)
			try { string f = Path.Combine(PresetFolder, name + ".txt"); if (File.Exists(f)) PiecesFiles.MoveToDeleted(f, "presets"); } catch (Exception e) { SetStatus("Could not delete: " + e.Message); return; }
			RefreshPresets();
			SetStatus("Deleted the preset '" + name + "'.");
		}

		#endregion

		#region Generate and Make

		void OnGenerate()
		{
			int seed;
			if (int.TryParse(seedField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) s.Seed = seed;
			else { s.Seed = UnityEngine.Random.Range(1, 999999); seedField.text = s.Seed.ToString(CultureInfo.InvariantCulture); }
			if (tab == TabReady) { OnMakeType(); return; }
			if (tab == TabRandomize && chosen == null) { SetStatus("Pick one of Raft's islands first."); return; }
			// (buildings and caves, and on Randomize existing the other kinds under water (LM7), use objects of Raft's own
			// islands: their scenes load first, then it generates)
			List<string> scenes = PlaceableCatalog.ScenesNeededFor(NeededNames());
			if (scenes.Count > 0)
			{
				if (loadingBuildings) { SetStatus("Still loading Raft's islands (" + scenes.Count + " scene" + (scenes.Count == 1 ? "" : "s") + ")... it generates when they are in."); return; }
				loadingBuildings = true;
				SetStatus("Loading Raft's islands for " + (tab == TabRandomize ? "what lies under water" : "the buildings and caves") + " (" + scenes.Count + " scene" + (scenes.Count == 1 ? "" : "s") + ")... it generates when they are in.");
				DynamicIslands.instance.StartCoroutine(LoadThenGenerate());
				return;
			}
			Generate();
		}

		bool loadingBuildings;
		RectTransform rebuildGroup;

		/// <summary>The design choices of the chosen island (ROADMAP CW1: each story island has its own): random, each
		/// design, none.</summary>
		List<string> RemakeIds()
		{
			List<Remakes.Design> designs = chosen != null ? Remakes.For(chosen.Scene) : new List<Remakes.Design>();
			if (designs.Count == 0) designs = Remakes.All.Take(3).ToList();
			var ids = new List<string> { designs[0].Scene + Remakes.Any };
			ids.AddRange(designs.Select(d => d.Id));
			ids.Add("");
			return ids;
		}

		List<DropList.Option> RemakeOptions() { return Enumerable.Range(0, RemakeIds().Count).Select(i => new DropList.Option(i.ToString(), RemakeLabel(i), RemakeHint(i))).ToList(); }

		string RemakeLabel() { return RemakeLabel(Mathf.Max(0, RemakeIds().IndexOf(s.Remake ?? ""))); }

		string RemakeLabel(int i)
		{
			List<string> ids = RemakeIds();
			if (i >= ids.Count) return "-";
			string id = ids[i];
			if (id.Length == 0) return "None (ground and nature only)";
			if (id.EndsWith(Remakes.Any)) return "Random design";
			Remakes.Design d = Remakes.All.FirstOrDefault(x => x.Id == id);
			return d != null ? d.Label : id;
		}

		string RemakeHint(int i)
		{
			List<string> ids = RemakeIds();
			if (i >= ids.Count) return "";
			Remakes.Design d = Remakes.All.FirstOrDefault(x => x.Id == ids[i]);
			return d != null ? d.Hint : ids[i].Length == 0 ? "Only the island's ground and nature, made anew" : "One of the designs, picked by the seed";
		}

		/// <summary>The settings Generate uses: on the Randomize existing tab only what it shows (not the Normal tab's buildings and caves).</summary>
		IslandGenSettings Effective()
		{
			if (tab != TabRandomize)
			{
				if (string.IsNullOrEmpty(s.Remake) && string.IsNullOrEmpty(s.Look)) return s;
				IslandGenSettings normal = s.Copy();
				normal.Remake = "";
				normal.Look = "";
				return normal;
			}
			IslandGenSettings run = s.Copy();
			if (chosen == null || Remakes.For(chosen.Scene).Count == 0) run.Remake = "";
			run.Buildings = false;
			run.Caves = false;
			run.RaftSeaKinds = true; // (LM7: every kind Raft's islands have under water that the catalog has)
			return run;
		}

		/// <summary>Objects of Raft's scenes that Generate needs loaded first: the buildings' and caves', and the extra kinds under water.</summary>
		List<string> NeededNames()
		{
			IslandGenSettings run = Effective();
			return GenBuildings.NeededNames(run).Concat(IslandGenerator.SeaNamesNeeded(run)).Distinct().ToList();
		}

		/// <summary>What the last Generate made (the window closes after it; tests read it).</summary>
		public static string LastResult { get; private set; }

		System.Collections.IEnumerator LoadThenGenerate()
		{
			int run = waitingRun;
			yield return PlaceableCatalog.EnsureLoaded(NeededNames());
			if (this == null || run != waitingRun) yield break;
			loadingBuildings = false;
			if (gameObject.activeInHierarchy) Generate();
		}

		void Generate()
		{
			LastResult = null; // (a test reading it saw the last run's result - CB13)
			try
			{
				IslandGenSettings run = Effective();
				int n = IslandGenerator.GenerateInEditor(run);
				if (tab == TabNormal) { lastNormal = run.Copy(); randomizeMade = null; }
				else randomizeMade = IslandGenerator.Last;
				IslandGenerator.FrameCamera(run);
				EditorUI.RefreshIsland();
				GenReport r = IslandGenerator.LastReport;
				string done = "Island " + s.Seed + " generated in " + r.Seconds.ToString("F1") + " s: land about " + r.LandLength.ToString("F0") + " x " + r.LandWidth.ToString("F0") + " m, " + r.Top.ToString("F0") + " m high; " +
					n + " objects (" + r.Describe() + ")" + (r.Wanted > IslandGenerator.MaxObjects ? " - thinned from " + r.Wanted : "") + ". Ctrl+Z undoes it.";
				SetStatus(done);
				// (the window steps aside so the new island is in view at once - GENERATE opens it again; the user, 2026-10-03)
				LastResult = done;
				Close();
				DynamicIslands.Notify(done);
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Generating failed: " + e);
				SetStatus("Generating failed - see the console (F10).");
			}
		}

		void OnMakeType()
		{
			MapType type = MapTypes.All[Mathf.Clamp(mapType, 0, MapTypes.All.Count - 1)];
			if (Time.unscaledTime > confirmUntil)
			{
				confirmUntil = Time.unscaledTime + 6f;
				SetStatus("Make opens a new island (" + type.Label.ToLowerInvariant() + "): save the current one first. Click Make again to go ahead.");
				return;
			}
			confirmUntil = 0f;
			string name = MakeType(type, s.Seed);
			if (name == null) { SetStatus("Making the island failed - see the console (F10)."); return; }
			if (DynamicIslands.LoadIsland(name)) { Close(); DynamicIslands.Notify("Made a " + type.Label.ToLowerInvariant() + ": '" + name + "'. Change it as you like and Save as with your own name - gen-... islands are left out of plans and random islands.");  }
		}

		/// <summary>Makes an island of a map type from a seed and saves it; returns its name (null if it failed).</summary>
		public static string MakeType(MapType type, int seed)
		{
			try
			{
				float elevation;
				IslandGenSettings ms = MapTypes.Roll(type, new System.Random(seed), out elevation);
				string name = MapTypes.FreeFileName(type, ms);
				IslandFile file = MapTypes.Create(type, ms, elevation, name);
				file.Save(IslandSpawner.PathFor(name));
				return name;
			}
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Making a '" + type.Name + "' island failed: " + e); return null; }
		}

		void SetStatus(string message) { if (status != null) status.text = message; }

		#endregion

		#region For the tests

		/// <summary>Switches the tab (tests and CIShot pictures of each tab).</summary>
		public static void ShowTab(int t) { if (instance != null) instance.SetTab(Mathf.Clamp(t, 0, 2)); }

		/// <summary>Sets the window's settings as if the controls were used (tests).</summary>
		public static void Use(IslandGenSettings settings) { if (instance != null) { instance.s = settings.Copy(); instance.fromOtherTab = false; instance.ShowAll(); } }

		/// <summary>The window's settings now (tests).</summary>
		public static IslandGenSettings Current { get { return instance != null ? instance.s.Copy() : null; } }

		/// <summary>Picks a Raft island on the Randomize existing tab, like clicking its tile (tests).</summary>
		public static void Pick(RaftIsland island, bool asVariation) { if (instance == null) return; instance.variation = asVariation; instance.Choose(island); }

		/// <summary>The preview as drawn now (tests), after drawing it.</summary>
		public static Texture PreviewNow() { if (instance == null) return null; instance.UpdatePreview(); return instance.preview.texture; }

		#endregion
	}
}
