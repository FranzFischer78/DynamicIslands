using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The level up system on screen: the EXP a hit gave floating up over the monster ("+5"); the level and its EXP
	/// bar just under Raft's own health, thirst and hunger bars (with a Stats button while Raft's inventory is open);
	/// and a box when the system turns on or the player levels up (clicking it opens the stats page).
	/// </summary>
	public class LevelHud : MonoBehaviour
	{
		const float FloatSeconds = 1.4f, FloatRise = 1.1f, AnnounceSeconds = 5f, LevelUpSeconds = 12f, PulseSeconds = 1.2f;
		static readonly Color Gold = new Color(1f, 0.86f, 0.35f, 1f);

		class Floater { public Text Text; public Vector3 At; public float Born; }

		static LevelHud instance;
		readonly List<Floater> floaters = new List<Floater>();
		RectTransform canvasRect;
		CanvasGroup banner;
		Text bannerTitle, bannerText;
		Button bannerButton;
		float bannerShownAt = -100f, bannerSeconds = AnnounceSeconds;

		// The level bar under Raft's stat bars (in Raft's own HUD, so it hides with it)
		RectTransform hud;
		Text hudLevel, hudXp;
		Button hudStats;
		CanvasHelper hudOf;
		float nextPlace, pulseAt = -100f;

		/// <summary>The texts floating now, and the last box shown (tests).</summary>
		public static string[] Floating { get { return instance == null ? new string[0] : instance.floaters.Where(f => f.Text.gameObject.activeSelf).Select(f => f.Text.text).ToArray(); } }
		public static string LastFloat { get; private set; }
		public static string LastAnnounce { get; private set; }
		/// <summary>The level bar is up next to Raft's stat bars.</summary>
		public static bool BarShown { get { return instance != null && instance.hud != null && instance.hud.gameObject.activeInHierarchy; } }
		/// <summary>What the level bar says, and where it is against Raft's stat bars (tests).</summary>
		public static string BarText { get { return BarShown ? instance.hudLevel.text + " " + instance.hudXp.text : null; } }
		public static string BarPlace { get; private set; }
		public static Button StatsButton { get { return instance != null ? instance.hudStats : null; } }
		public static Button BannerButton { get { return instance != null && instance.banner.gameObject.activeSelf ? instance.bannerButton : null; } }

		static LevelHud Get()
		{
			if (instance == null) Build();
			return instance;
		}

		/// <summary>Makes sure the HUD exists (the mod calls it every frame while the system is on).</summary>
		public static void Ensure() { Get(); }

		/// <summary>"+5 EXP" floating up from a place in the world.</summary>
		public static void Float(string text, Vector3 at)
		{
			LevelHud h = Get();
			Floater f = h.floaters.FirstOrDefault(x => !x.Text.gameObject.activeSelf);
			if (f == null)
			{
				Text t = UIKit.Label(h.canvasRect, "", 30, Gold, TextAnchor.MiddleCenter, FontStyle.Bold, "Exp");
				t.horizontalOverflow = HorizontalWrapMode.Overflow;
				UIKit.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40));
				Outline o = t.gameObject.AddComponent<Outline>();
				o.effectColor = new Color(0.2f, 0.1f, 0.03f, 0.9f);
				o.effectDistance = new Vector2(1.5f, -1.5f);
				t.raycastTarget = false;
				f = new Floater { Text = t };
				h.floaters.Add(f);
			}
			f.Text.text = text + " EXP";
			f.At = at;
			f.Born = Time.unscaledTime;
			f.Text.gameObject.SetActive(true);
			LastFloat = f.Text.text;
			h.Place(f);
		}

		/// <summary>EXP came in: the level bar lights up for a moment.</summary>
		public static void ShowBar()
		{
			LevelHud h = Get();
			h.pulseAt = Time.unscaledTime;
			h.RefreshBar();
		}

		/// <summary>A box in the middle of the top of the screen; a clickable one opens the stats page (when the mouse is free: in a menu).</summary>
		public static void Announce(string title, string text, bool clickable = false)
		{
			LevelHud h = Get();
			h.bannerTitle.text = title;
			h.bannerText.text = text;
			h.bannerShownAt = Time.unscaledTime;
			h.bannerSeconds = clickable ? LevelUpSeconds : AnnounceSeconds;
			h.bannerButton.interactable = clickable;
			h.banner.blocksRaycasts = clickable;
			h.banner.gameObject.SetActive(true);
			h.banner.alpha = 0f;
			LastAnnounce = title + " | " + text;
			Debug.Log("[CUSTOM ISLANDS] Levels: " + LastAnnounce);
		}

		/// <summary>Hides everything (leaving a world, the system off).</summary>
		public static void Clear()
		{
			if (instance == null) return;
			foreach (Floater f in instance.floaters) f.Text.gameObject.SetActive(false);
			instance.banner.gameObject.SetActive(false);
			if (instance.hud != null) instance.hud.gameObject.SetActive(false);
		}

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_Levels", 410); // (above the island banners (400): an EXP number behind one would be lost)
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<LevelHud>();
			instance.canvasRect = (RectTransform)canvas.transform;

			// The box, below where the island banners show; a level up's box opens the stats page when clicked
			RectTransform n = UIKit.Rect("LevelBanner", canvas.transform);
			UIKit.Anchor(n, new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(560, 0));
			UIKit.Surface(n);
			UIKit.Vertical(n.gameObject, 4f, new RectOffset(22, 22, 12, 14), true);
			instance.bannerTitle = UIKit.Label(n, "", 28, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
			instance.bannerText = UIKit.Label(n, "", 16, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, "Text");
			foreach (Graphic g in n.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
			Image hit = n.GetComponent<Image>() ?? n.gameObject.AddComponent<Image>();
			hit.raycastTarget = true;
			instance.bannerButton = n.gameObject.AddComponent<Button>();
			instance.bannerButton.targetGraphic = hit;
			var nav = instance.bannerButton.navigation; nav.mode = Navigation.Mode.None; instance.bannerButton.navigation = nav;
			instance.bannerButton.onClick.AddListener(() => { instance.banner.gameObject.SetActive(false); LevelWindow.OpenFromGame(); });
			UIKit.Hint(n.gameObject, "Open the stats page");
			instance.banner = n.gameObject.AddComponent<CanvasGroup>();
			n.gameObject.SetActive(false);
			PlayerLevels.Changed += () => { if (instance != null) instance.RefreshBar(); };
		}

		/// <summary>
		/// Puts the level bar into Raft's HUD with its stat bars (CanvasHelper.StatSliderParent: thirst, hunger, health):
		/// a copy of Raft's own hunger bar (its badge, frame, groove and shadow), with a star for its icon, a gold fill
		/// and the level and EXP written in it, one row above Raft's top bar. If Raft's bars ever look different
		/// inside, a plain bar of the mod's own is used instead.
		/// </summary>
		void PlaceHud()
		{
			CanvasHelper helper = null;
			try { helper = ComponentManager<CanvasHelper>.Value; } catch { }
			if (helper == null) helper = FindObjectOfType<CanvasHelper>();
			if (helper == null || helper.StatSliderParent == null) return;
			RectTransform parent = helper.StatSliderParent.transform as RectTransform;
			if (parent == null) return;
			// Raft's bars, top first
			var sliders = new[] { helper.healthSlider, helper.thirstSlider, helper.hungerSlider }.Where(s => s != null)
				.Select(s => (RectTransform)s.transform).Where(r => r.parent == parent).OrderByDescending(r => r.localPosition.y).ToList();
			if (sliders.Count == 0) return;
			if (hud == null || hudOf != helper)
			{
				if (hud != null) Destroy(hud.gameObject);
				hudOf = helper;
				hudLevel = hudXp = null;
				if (!BuildFromRaft(helper.hungerSlider ?? helper.thirstSlider, parent)) BuildOwn(parent);
				RefreshBar();
			}
			if (fromRaft)
			{
				// One row above the top bar: as far above it as the bar under it is below it
				RectTransform top = sliders[0];
				Vector3 step = sliders.Count > 1 ? top.localPosition - sliders[1].localPosition : new Vector3(0f, top.rect.height * 1.25f, 0f);
				hud.localPosition = top.localPosition + step;
				hud.SetAsLastSibling();
				BarPlace = "above Raft's " + sliders.Count + " stat bars, Raft's own bar (" + BarStyle + ")";
				return;
			}
			PlaceOwn(parent, sliders);
		}

		/// <summary>How the bar is made: "a copy of Raft's HungerBar" or "the mod's own" (tests).</summary>
		public static string BarStyle { get; private set; }
		/// <summary>The bar's icon (tests: the star).</summary>
		public static Sprite BarIcon { get { return instance != null && instance.hudIcon != null ? instance.hudIcon.sprite : null; } }

		bool fromRaft;
		Slider hudSlider;
		Image hudGlow, hudIcon, hudTrack;
		static readonly Color XpFill = new Color(0.87f, 0.68f, 0.24f, 1f); // warm gold, beside Raft's tan and red
		static readonly Color BarInk = new Color(1f, 0.95f, 0.84f, 1f), BarTextEdge = new Color(0.23f, 0.13f, 0.07f, 0.9f);

		/// <summary>A copy of one of Raft's stat bars made into the level bar; false if its parts aren't what they were.</summary>
		bool BuildFromRaft(UISlider_Stat source, RectTransform parent)
		{
			if (source == null) return false;
			GameObject holder = null;
			GameObject clone = null;
			try
			{
				// (copied under a switched-off holder, so none of Raft's scripts on it wake up)
				holder = new GameObject("CustomIslands_LevelBarHolder");
				holder.SetActive(false);
				clone = Instantiate(source.gameObject, holder.transform, false);
				clone.name = "CustomIslands_LevelBar";
				// The value slider: the one Raft's UISlider_Stat moves (sliderTransform), found by its path in the copy
				string valuePath = source.sliderTransform != null ? PathFrom(source.transform, source.sliderTransform) : null;
				Transform valueT = valuePath != null ? clone.transform.Find(valuePath) : null;
				Slider value = valueT != null ? valueT.GetComponentInParent<Slider>() ?? valueT.GetComponentInChildren<Slider>(true) : null;
				Transform icon = clone.transform.Find("StatIconBG/Image");
				if (value == null || icon == null || icon.GetComponent<Image>() == null) { Destroy(holder); return false; }
				foreach (UISlider_Stat s in clone.GetComponentsInChildren<UISlider_Stat>(true)) DestroyImmediate(s);
				foreach (Animator a in clone.GetComponentsInChildren<Animator>(true)) DestroyImmediate(a);
				// Only the bar's own parts stay: not the target slider (the preview of what food gives), bonus bars, dividers
				// (listed first: a slider can hold another one, gone with it)
				foreach (GameObject extra in clone.GetComponentsInChildren<Slider>(true).Where(s => s != value && s.transform.parent == clone.transform).Select(s => s.gameObject).ToList())
					if (extra != null) DestroyImmediate(extra);
				foreach (Transform t in clone.GetComponentsInChildren<Transform>(true).Where(t => t != null && (t.name.Contains("Bonus") || t.name.StartsWith("Divider"))).ToList())
					if (t != null) DestroyImmediate(t.gameObject);
				value.interactable = false;
				value.transition = Selectable.Transition.None;
				value.minValue = 0f; value.maxValue = 1f; value.wholeNumbers = false;
				Image fill = value.fillRect != null ? value.fillRect.GetComponent<Image>() : null;
				if (fill != null) fill.color = XpFill;
				hudSlider = value;
				hudIcon = icon.GetComponent<Image>();
				hudIcon.sprite = StarIcon();
				hudIcon.preserveAspect = true;
				Transform glow = clone.transform.Find("BlinkImage");
				hudGlow = glow != null ? glow.GetComponent<Image>() : null;
				if (hudGlow != null) hudGlow.color = new Color(1f, 0.85f, 0.4f, 0f);

				// The level and EXP, written in the bar (over its groove, the fill's area)
				RectTransform area = (RectTransform)(value.fillRect != null && value.fillRect.parent != null ? value.fillRect.parent : value.transform);
				hudLevel = BarLabel(area, "Level", TextAnchor.MiddleLeft, 0.8f);
				hudXp = BarLabel(area, "Xp", TextAnchor.MiddleRight, 0.68f);
				// While Raft's inventory is open (the mouse is free): a button for the stats page, right of the bar
				RectTransform barRect = (RectTransform)clone.transform;
				hudStats = UIKit.Button(barRect, "Stats", LevelWindow.OpenFromGame, "Your level and stat points (" + PlayerLevels.Key + ")", 64, 22f, 12);
				RectTransform sb = (RectTransform)hudStats.transform;
				UIKit.Ensure<LayoutElement>(sb.gameObject).ignoreLayout = true;
				float h = area.rect.height > 1f ? area.rect.height : barRect.rect.height * 0.5f;
				sb.anchorMin = sb.anchorMax = new Vector2(1f, 0.5f); sb.pivot = new Vector2(0f, 0.5f);
				sb.sizeDelta = new Vector2(h * 3.4f, h * 1.25f);
				sb.anchoredPosition = new Vector2(h * 0.4f, 0f);
				Text st = UIKit.LabelOf(hudStats);
				if (st != null) { st.resizeTextMaxSize = Mathf.Max(8, Mathf.RoundToInt(h * 0.8f)); st.fontSize = st.resizeTextMaxSize; }
				hudStats.gameObject.SetActive(false);
				foreach (Graphic g in clone.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = g.transform.IsChildOf(hudStats.transform);

				// Into Raft's HUD, beside its bars (a layout group there must leave it alone)
				clone.transform.SetParent(parent, false);
				UIKit.Ensure<LayoutElement>(clone).ignoreLayout = true;
				Destroy(holder);
				hud = barRect;
				fromRaft = true;
				BarStyle = "a copy of Raft's " + source.name;
				return true;
			}
			catch (System.Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Levels: Raft's stat bar could not be copied; using the mod's own bar: " + e);
				if (clone != null) Destroy(clone);
				if (holder != null) Destroy(holder);
				hudSlider = null; hudIcon = null; hudGlow = null; hudStats = null;
				return false;
			}
		}

		/// <summary>Text in the level bar: Raft's lettering, light with a dark edge, readable on the fill and on the empty groove.</summary>
		Text BarLabel(RectTransform area, string name, TextAnchor anchor, float size)
		{
			Text t = UIKit.Label(area, "", 12, BarInk, anchor, FontStyle.Normal, name);
			t.font = UIKit.TitleFont;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.verticalOverflow = VerticalWrapMode.Overflow;
			UIKit.Ensure<LayoutElement>(t.gameObject).ignoreLayout = true;
			Span(t.rectTransform, 0f, 1f, 0f, 1f);
			float pad = Mathf.Max(2f, area.rect.height * 0.35f);
			t.rectTransform.offsetMin = new Vector2(pad, 0f); t.rectTransform.offsetMax = new Vector2(-pad, 0f);
			t.fontSize = Mathf.Max(8, Mathf.RoundToInt(Mathf.Max(10f, area.rect.height) * size));
			foreach (Shadow s in t.GetComponents<Shadow>()) DestroyImmediate(s);
			Outline o = t.gameObject.AddComponent<Outline>();
			o.effectColor = BarTextEdge;
			o.effectDistance = new Vector2(1.2f, -1.2f);
			t.raycastTarget = false;
			return t;
		}

		static string PathFrom(Transform root, Transform t)
		{
			var names = new List<string>();
			for (Transform x = t; x != null && x != root; x = x.parent) names.Insert(0, x.name);
			return string.Join("/", names.ToArray());
		}

		static Sprite starIcon;

		/// <summary>
		/// The level bar's icon, drawn like Raft's stat icons (StatIcon_Health / _Hunger / _Thirst: 71 x 68, a dark
		/// reddish-brown silhouette, lighter at the top, a little highlight): a five-pointed star with rounded points.
		/// </summary>
		public static Sprite StarIcon()
		{
			if (starIcon != null) return starIcon;
			const int W = 71, H = 68;
			var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Bilinear;
			tex.name = "CustomIslands_StatIcon_Level";
			// The star: outer points at radius R, inner corners at 0.48 R, points rounded by `round`
			Vector2 c = new Vector2(W / 2f, H / 2f - 3f);
			const float R = 34.5f, round = 3.5f;
			var pts = new Vector2[10];
			for (int i = 0; i < 10; i++)
			{
				float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
				float r = (i % 2 == 0 ? R : R * 0.48f) - round;
				pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
			}
			Color top = new Color(0.36f, 0.16f, 0.07f), bottom = new Color(0.26f, 0.13f, 0.05f);
			// (Raft's drop has little highlights cut out of it: two on the star's upper left)
			Vector2 spot1 = c + new Vector2(-7.5f, 9.5f), spot2 = c + new Vector2(-10f, 3f);
			var px = new Color[W * H];
			for (int y = 0; y < H; y++)
				for (int x = 0; x < W; x++)
				{
					Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
					float d = PolygonDistance(p, pts) - round;
					float alpha = Mathf.Clamp01(0.5f - d);
					float hole = Mathf.Max(Mathf.Clamp01(3.1f - Vector2.Distance(p, spot1) + 0.5f), Mathf.Clamp01(1.7f - Vector2.Distance(p, spot2) + 0.5f));
					alpha *= 1f - hole * 0.85f;
					Color col = Color.Lerp(bottom, top, (float)y / (H - 1));
					col.a = alpha;
					px[y * W + x] = col;
				}
			tex.SetPixels(px);
			tex.Apply(false, true);
			starIcon = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
			starIcon.name = "CustomIslands_StatIcon_Level";
			return starIcon;
		}

		/// <summary>Signed distance from a point to a closed polygon (negative inside).</summary>
		static float PolygonDistance(Vector2 p, Vector2[] poly)
		{
			float best = float.MaxValue;
			bool inside = false;
			for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
			{
				Vector2 a = poly[j], b = poly[i], ab = b - a;
				float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
				best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
				if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
			}
			float dist = Mathf.Sqrt(best);
			return inside ? -dist : dist;
		}

		/// <summary>The mod's own plain bar, if Raft's can't be copied: placed under (or above) Raft's bars.</summary>
		void PlaceOwn(RectTransform parent, List<RectTransform> sliders)
		{
			float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue, rowH = 0f;
			var corners = new Vector3[4];
			foreach (RectTransform r in sliders)
			{
				r.GetWorldCorners(corners);
				Vector3 a = parent.InverseTransformPoint(corners[0]), b = parent.InverseTransformPoint(corners[2]);
				xMin = Mathf.Min(xMin, a.x, b.x); xMax = Mathf.Max(xMax, a.x, b.x);
				yMin = Mathf.Min(yMin, a.y, b.y); yMax = Mathf.Max(yMax, a.y, b.y);
				rowH = Mathf.Max(rowH, Mathf.Abs(b.y - a.y));
			}
			float h = Mathf.Max(8f, rowH * 0.8f), gap = Mathf.Max(2f, rowH * 0.25f);
			SizeOwn(h);
			hud.anchorMin = hud.anchorMax = parent.pivot;
			hud.pivot = new Vector2(0f, 1f);
			hud.sizeDelta = new Vector2(xMax - xMin, h);
			hud.anchoredPosition = new Vector2(xMin, yMin - gap);
			hud.GetWorldCorners(corners);
			Canvas c = parent.GetComponentInParent<Canvas>();
			Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
			bool above = RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y < 2f;
			if (above) { hud.pivot = new Vector2(0f, 0f); hud.anchoredPosition = new Vector2(xMin, yMax + gap); }
			hud.SetAsLastSibling();
			BarPlace = (above ? "above" : "below") + " Raft's " + sliders.Count + " stat bars, the mod's own bar, " + (xMax - xMin).ToString("F0") + " wide";
		}

		RectTransform ownFill;

		void BuildOwn(RectTransform parent)
		{
			fromRaft = false;
			BarStyle = "the mod's own";
			hud = UIKit.Rect("CustomIslands_LevelBar", parent);
			UIKit.Ensure<LayoutElement>(hud.gameObject).ignoreLayout = true;
			hudLevel = UIKit.Label(hud, "LV 1", 14, Gold, TextAnchor.MiddleLeft, FontStyle.Bold, "Level");
			hudLevel.horizontalOverflow = HorizontalWrapMode.Overflow;
			Span(hudLevel.rectTransform, 0f, 0.2f, 0f, 1f);
			RectTransform track = UIKit.Rect("Track", hud);
			Span(track, 0.2f, 0.72f, 0.3f, 0.7f);
			hudTrack = UIKit.Background(track.gameObject, new Color(0.1f, 0.06f, 0.02f, 0.75f), 4);
			ownFill = UIKit.Rect("Fill", track);
			ownFill.anchorMin = Vector2.zero; ownFill.anchorMax = new Vector2(0f, 1f);
			ownFill.offsetMin = Vector2.zero; ownFill.offsetMax = Vector2.zero;
			UIKit.Background(ownFill.gameObject, Gold, 4);
			hudXp = UIKit.Label(hud, "", 11, new Color(1f, 0.95f, 0.85f, 0.9f), TextAnchor.MiddleRight, FontStyle.Normal, "Xp");
			hudXp.horizontalOverflow = HorizontalWrapMode.Overflow;
			Span(hudXp.rectTransform, 0.74f, 1f, 0f, 1f);
			hudStats = UIKit.Button(hud, "Stats", LevelWindow.OpenFromGame, "Your level and stat points (" + PlayerLevels.Key + ")", 64, 22f, 12);
			RectTransform sb = (RectTransform)hudStats.transform;
			sb.anchorMin = new Vector2(1f, 0f); sb.anchorMax = new Vector2(1f, 1f); sb.pivot = new Vector2(0f, 0.5f);
			hudStats.gameObject.SetActive(false);
			foreach (Graphic g in hud.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = g.transform.IsChildOf(hudStats.transform);
		}

		static void Span(RectTransform r, float x0, float x1, float y0, float y1)
		{
			r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
			r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
		}

		void SizeOwn(float h)
		{
			hudLevel.fontSize = Mathf.Max(8, Mathf.RoundToInt(h * 0.62f));
			hudXp.fontSize = Mathf.Max(7, Mathf.RoundToInt(h * 0.5f));
			RectTransform sb = (RectTransform)hudStats.transform;
			sb.sizeDelta = new Vector2(h * 3.2f, h * 0.2f);
			sb.anchoredPosition = new Vector2(h * 0.3f, 0f);
			Text t = UIKit.LabelOf(hudStats);
			if (t != null) { t.resizeTextMaxSize = Mathf.Max(8, Mathf.RoundToInt(h * 0.6f)); t.fontSize = t.resizeTextMaxSize; }
		}

		void RefreshBar()
		{
			if (hud == null || hudLevel == null) return;
			LevelRecord r = PlayerLevels.Mine;
			if (r == null) return;
			int level = r.Level, start = LevelRules.TotalFor(level), need = LevelRules.XpFor(level);
			float part = Mathf.Clamp01((r.Xp - start) / (float)need);
			hudLevel.text = "LV " + level + (r.Unspent > 0 ? " +" : "");
			hudXp.text = (r.Xp - start) + " / " + need;
			if (hudSlider != null) hudSlider.normalizedValue = part;
			if (ownFill != null) ownFill.anchorMax = new Vector2(part, 1f);
		}

		void Place(Floater f)
		{
			Camera cam = Camera.main;
			float t = (Time.unscaledTime - f.Born) / FloatSeconds;
			if (cam == null || t >= 1f) { f.Text.gameObject.SetActive(false); return; }
			Vector3 world = f.At + Vector3.up * (FloatRise * t);
			Vector3 screen = cam.WorldToScreenPoint(world);
			if (screen.z <= 0.1f) { f.Text.enabled = false; return; }
			f.Text.enabled = true;
			Vector2 local;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
			f.Text.rectTransform.anchoredPosition = local;
			// Pops in, then fades out; a little smaller far away
			float scale = Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(t * 6f)) * Mathf.Clamp(12f / Mathf.Max(1f, screen.z), 0.6f, 1.2f);
			f.Text.rectTransform.localScale = Vector3.one * scale;
			Color c = f.Text.color;
			c.a = Mathf.Clamp01((1f - t) / 0.35f);
			f.Text.color = c;
		}

		void Update()
		{
			bool inWorld = LoadSceneManager.IsGameSceneLoaded && !DynamicIslands.InEditor();
			if (!inWorld || !PlayerLevels.On) { Clear(); return; }
			foreach (Floater f in floaters) if (f.Text.gameObject.activeSelf) Place(f);
			if (banner.gameObject.activeSelf)
			{
				float t = Time.unscaledTime - bannerShownAt;
				banner.alpha = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((bannerSeconds - t) / 1f);
				if (t > bannerSeconds) banner.gameObject.SetActive(false);
			}
			// The level bar: placed again now and then (Raft's HUD can change size), lit up for a moment after EXP
			// (not every frame while the bar can't be built - no Raft HUD found: a FindObjectOfType each frame. Audit 2026-10-06)
			if (Time.unscaledTime >= nextPlace) { nextPlace = Time.unscaledTime + (hud == null ? 0.5f : 2f); PlaceHud(); }
			if (hud == null) return;
			if (!hud.gameObject.activeSelf) { hud.gameObject.SetActive(true); RefreshBar(); }
			float p = Mathf.Clamp01(1f - (Time.unscaledTime - pulseAt) / PulseSeconds);
			// (Raft's own glow behind the bar, as Raft blinks a stat bar; the mod's own bar lights its groove)
			if (hudGlow != null) hudGlow.color = new Color(1f, 0.85f, 0.4f, 0.9f * p);
			if (hudTrack != null) hudTrack.color = Color.Lerp(new Color(0.1f, 0.06f, 0.02f, 0.75f), new Color(0.55f, 0.42f, 0.12f, 0.9f), p);
			bool menu = CanvasHelper.ActiveMenu == MenuType.Inventory;
			if (hudStats != null && hudStats.gameObject.activeSelf != menu) hudStats.gameObject.SetActive(menu);
		}
	}

	/// <summary>
	/// Other players' levels, kept discreet: a small "Lv 5" in gold under the name Raft shows over each other player
	/// (Network_Player.playerNameTextMesh), only while the level up system is on. Their own name tag stays Raft's.
	/// </summary>
	public static class LevelTags
	{
		const string TagName = "CustomIslands_Level";
		static readonly Color TagColor = new Color(1f, 0.84f, 0.4f, 0.8f);
		static float nextCheck;

		/// <summary>What each other player's tag says now, by Steam id (tests).</summary>
		public static readonly Dictionary<ulong, string> Shown = new Dictionary<ulong, string>();

		/// <summary>Every frame from the mod (checks twice a second).</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 0.5f;
			// (levels off: nothing to show once the tags shown are hidden)
			if (!PlayerLevels.On && Shown.Count == 0) return;
			Shown.Clear();
			Network_Player local = null;
			try { local = RAPI.GetLocalPlayer(); } catch { }
			foreach (Network_Player p in Players.All)
			{
				if (p == null || p == local || p.playerNameTextMesh == null) continue;
				TextMesh name = p.playerNameTextMesh;
				Transform t = name.transform.Find(TagName);
				int level = PlayerLevels.LevelOf(p.steamID.Id);
				if (level <= 0) { if (t != null) t.gameObject.SetActive(false); continue; }
				TextMesh tag = t != null ? t.GetComponent<TextMesh>() : Make(name);
				tag.gameObject.SetActive(true);
				string text = "Lv " + level;
				if (tag.text != text) tag.text = text;
				Shown[p.steamID.Id] = text;
			}
		}

		/// <summary>A smaller copy of the name's text just under it (same font, facing the camera with it).</summary>
		static TextMesh Make(TextMesh name)
		{
			var go = new GameObject(TagName);
			go.layer = name.gameObject.layer;
			go.transform.SetParent(name.transform, false);
			var tag = go.AddComponent<TextMesh>();
			tag.font = name.font;
			tag.fontSize = name.fontSize;
			tag.fontStyle = name.fontStyle;
			tag.characterSize = name.characterSize * 0.62f;
			tag.anchor = TextAnchor.UpperCenter;
			tag.alignment = TextAlignment.Center;
			tag.color = TagColor;
			MeshRenderer from = name.GetComponent<MeshRenderer>(), to = go.GetComponent<MeshRenderer>();
			if (from != null && to != null) { to.sharedMaterial = from.sharedMaterial; to.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; to.receiveShadows = false; }
			// Just under the name: its lowest point, in its own space (the name faces the camera, so up stays up)
			float below = 0f;
			if (from != null && from.bounds.size.y > 0.001f)
				below = name.transform.InverseTransformPoint(new Vector3(from.bounds.center.x, from.bounds.min.y, from.bounds.center.z)).y;
			else below = -name.characterSize * name.fontSize * 0.1f;
			go.transform.localPosition = new Vector3(0f, below - name.characterSize * 0.5f, 0f);
			return tag;
		}
	}

	/// <summary>
	/// The stats page (K) in a world with the level up system on: the level and EXP, the monsters defeated, and the nine stats to put the
	/// level's points into (+1% a point, at most 15 each). Points put in can be taken back until the page is closed.
	/// </summary>
	public class LevelWindow : MonoBehaviour
	{
		/// <summary>A point's box: 15 of them fit where 10 boxes of 17 px were (15 points since 2026-10-06).</summary>
		const int PipWidth = 11;

		static LevelWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		Text levelText, pointsText, xpText, totalText, killsText;
		RectTransform fill;
		readonly Image[][] pips = new Image[LevelRules.StatCount][];
		readonly Text[] values = new Text[LevelRules.StatCount];
		readonly Button[] plus = new Button[LevelRules.StatCount], minus = new Button[LevelRules.StatCount];
		int[] openedWith = new int[LevelRules.StatCount];
		static Text keyHint;
		bool cursorWasFree;

		static readonly Color PipOn = new Color(0.97f, 0.8f, 0.42f, 1f), PipOff = new Color(0.16f, 0.09f, 0.04f, 0.85f);

		/// <summary>Every frame from the mod: K opens and closes the page while the level up system is on.</summary>
		public static void Tick()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || DynamicIslands.InEditor() || !PlayerLevels.On) { if (IsOpen) instance.Hide(); return; }
			if (!Input.GetKeyDown(PlayerLevels.Key) || Typing() || NoteReader.IsOpen || JournalWindow.IsOpen || ModKeys.Listening) return;
			if (IsOpen) instance.Hide(); else Open();
		}

		static bool Typing()
		{
			GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			InputField f = sel != null ? sel.GetComponent<InputField>() : null;
			return f != null && f.isFocused;
		}

		public static void Open()
		{
			if (!PlayerLevels.On) return;
			if (instance == null) Build();
			instance.Show();
		}

		public static void Close() { if (instance != null) instance.Hide(); }

		/// <summary>
		/// Opens the page from Raft's screen (the Stats button by Raft's stat bars in the inventory, a level up's box):
		/// Raft's open menu closes first, so the page gets the mouse and gives it back to the game when it closes.
		/// </summary>
		public static void OpenFromGame()
		{
			if (!PlayerLevels.On) return;
			MenuType open = CanvasHelper.ActiveMenu;
			if (open != MenuType.None)
			{
				CanvasHelper helper = null;
				try { helper = ComponentManager<CanvasHelper>.Value; } catch { }
				if (helper == null) helper = FindObjectOfType<CanvasHelper>();
				if (helper != null) try
				{
					helper.CloseMenu(open);
					// (CloseMenu only closes a menu it finds open: anything left, Raft closes all of them)
					if (CanvasHelper.ActiveMenu != MenuType.None) helper.CloseAllMenus();
				}
				catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Closing Raft's menu: " + e.Message); }
				if (CanvasHelper.ActiveMenu != MenuType.None) Debug.LogWarning("[CUSTOM ISLANDS] Raft's " + CanvasHelper.ActiveMenu + " menu did not close for the stats page");
			}
			DynamicIslands.instance.StartCoroutine(OpenNextFrame());
		}

		static IEnumerator OpenNextFrame()
		{
			yield return null;
			yield return null;
			Open();
		}

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_Stats", 492);
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<LevelWindow>();
			PlayerLevels.Changed += () => { if (IsOpen) instance.Fill(); };

			RectTransform panel = UIKit.Panel(canvas.transform, "Panel", new RectOffset(20, 20, 14, 16), 8f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 648));
			RectTransform head = UIKit.Row(panel, 34f, 8f, "Head");
			instance.levelText = UIKit.Label(head, "LEVEL 1", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Level");
			instance.pointsText = UIKit.Label(head, "", 15, UIKit.TextColor, TextAnchor.MiddleRight, FontStyle.Normal, "Points");

			RectTransform xp = UIKit.Group(panel, "Experience");
			RectTransform track = UIKit.Rect("Track", xp);
			UIKit.Size(track.gameObject, -1, 16);
			UIKit.Background(track.gameObject, new Color(0.15f, 0.08f, 0.03f, 0.9f), 5);
			instance.fill = UIKit.Rect("Fill", track);
			instance.fill.anchorMin = Vector2.zero; instance.fill.anchorMax = new Vector2(0f, 1f);
			instance.fill.offsetMin = Vector2.zero; instance.fill.offsetMax = Vector2.zero;
			UIKit.Background(instance.fill.gameObject, PipOn, 5);
			RectTransform xpRow = UIKit.Row(xp, 20f, 6f, "XpRow");
			instance.xpText = UIKit.Label(xpRow, "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Xp");
			instance.totalText = UIKit.Label(xpRow, "", 12, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, "Total");
			instance.killsText = UIKit.Label(xp, "", 14, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Kills");

			RectTransform stats = UIKit.Group(panel, "Stats");
			for (int i = 0; i < LevelRules.StatCount; i++)
			{
				int stat = i;
				RectTransform row = UIKit.Row(stats, 30f, 6f, "Stat_" + LevelRules.StatNames[i]);
				Text name = UIKit.Label(row, LevelRules.StatNames[i], 15, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Name");
				UIKit.Size(name.gameObject, 118, -1, 0);
				RectTransform pipRow = UIKit.Rect("Pips", row);
				UIKit.Horizontal(pipRow.gameObject, 3f);
				UIKit.Size(pipRow.gameObject, LevelRules.MaxPoints * PipWidth + (LevelRules.MaxPoints - 1) * 3, 16, 0);
				instance.pips[i] = new Image[LevelRules.MaxPoints];
				for (int p = 0; p < LevelRules.MaxPoints; p++)
				{
					RectTransform pip = UIKit.Rect("Pip" + p, pipRow);
					UIKit.Size(pip.gameObject, PipWidth, 16, 0);
					instance.pips[i][p] = UIKit.Background(pip.gameObject, PipOff, 3);
				}
				instance.values[i] = UIKit.Label(row, "", 14, UIKit.Accent, TextAnchor.MiddleRight, FontStyle.Normal, "Value");
				UIKit.Size(instance.values[i].gameObject, 74, -1, 1); // (takes the row's free space: the buttons sit at its right edge)
				instance.minus[i] = UIKit.Button(row, "−", () => PlayerLevels.Spend(stat, -1), "Take back a point put into " + LevelRules.StatNames[i].ToLowerInvariant() + " (until you close this page)", 34, 28f, 16);
				instance.plus[i] = UIKit.Button(row, "+", () => PlayerLevels.Spend(stat, 1), "Put a stat point into " + LevelRules.StatNames[i].ToLowerInvariant() + ": " + LevelRules.StatHints[i].ToLowerInvariant() + " (+1% a point, at most " + LevelRules.MaxPoints + ")", 34, 28f, 16);
				UIKit.Hint(row.gameObject, LevelRules.StatHints[i]);
			}

			Text about = UIKit.Label(panel, "Hit monsters to earn EXP: the tougher the monster and the harder it bites, the more (Bruce the shark is worth " + LevelRules.BruceXp +
				"). Level 2 takes about 3 shark kills, level 3 about 5 more, level 4 about 10 more, then 5 more each level. Every level gives " + LevelRules.PointsPerLevel +
				" stat points; each point is +1% (Hunger, Thirst and Oxygen last 1% longer), at most " + LevelRules.MaxPoints + " points in a stat. With every stat full the levels go on, without points.", 12, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, "About");
			about.lineSpacing = 1.05f;
			RectTransform bottom = UIKit.Row(panel, 34f, 8f, "Bottom");
			keyHint = UIKit.Label(bottom, ModKeys.Name(PlayerLevels.Key) + " or Esc to close", 13, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "KeyHint");
			UIKit.Button(bottom, "Close", Close, null, 120, 34);
			canvas.gameObject.SetActive(false);
		}

		void Show()
		{
			gameObject.SetActive(true);
			if (keyHint != null) keyHint.text = ModKeys.Name(PlayerLevels.Key) + " or Esc to close";
			cursorWasFree = Cursor.visible;
			if (!cursorWasFree) try { RAPI.ToggleCursor(true); } catch { }
			LevelRecord r = PlayerLevels.Mine;
			openedWith = r != null ? (int[])r.Points.Clone() : new int[LevelRules.StatCount];
			Fill();
		}

		void Hide()
		{
			if (!gameObject.activeSelf) return;
			gameObject.SetActive(false);
			// (only in a world: after a disconnect it locked the cursor on the main menu - CA17)
			if (!cursorWasFree && LoadSceneManager.IsGameSceneLoaded) try { RAPI.ToggleCursor(false); } catch { }
		}

		void Update()
		{
			if (EscKey.Down(this) || !LoadSceneManager.IsGameSceneLoaded) Hide();
		}

		void Fill()
		{
			LevelRecord r = PlayerLevels.Mine;
			if (r == null) return;
			int level = r.Level, start = LevelRules.TotalFor(level), need = LevelRules.XpFor(level);
			levelText.text = "LEVEL " + level;
			pointsText.text = r.Unspent > 0 ? "<color=#f8cc6b>" + r.Unspent + "</color> stat point(s) to spend" : r.AllFull ? "Every stat is full" : "No stat points to spend";
			xpText.text = (r.Xp - start) + " / " + need + " EXP to level " + (level + 1);
			totalText.text = r.Xp + " EXP in all";
			killsText.text = "Monsters defeated: <color=#f8cc6b>" + r.Kills + "</color>";
			fill.anchorMax = new Vector2(Mathf.Clamp01((r.Xp - start) / (float)need), 1f);
			for (int i = 0; i < LevelRules.StatCount; i++)
			{
				int p = r.Points[i];
				for (int k = 0; k < LevelRules.MaxPoints; k++) pips[i][k].color = k < p ? PipOn : PipOff;
				values[i].text = p == 0 ? "+0%" : "+" + p + "%" + (p >= LevelRules.MaxPoints ? " max" : "");
				plus[i].interactable = r.Unspent > 0 && p < LevelRules.MaxPoints;
				Color plusInk = UIKit.AccentText;
				plusInk.a = plus[i].interactable ? 1f : 0.3f;
				UIKit.LabelOf(plus[i]).color = plusInk;
				// (the button stays in its place, dimmed when there is nothing to take back, so the row doesn't jump)
				minus[i].interactable = p > openedWith[i];
				Color ink = UIKit.AccentText;
				ink.a = p > openedWith[i] ? 1f : 0.3f;
				UIKit.LabelOf(minus[i]).color = ink;
			}
		}

		/// <summary>What the page shows (tests): "LEVEL 3 | 2 stat point(s) to spend | Walk speed +4% ...".</summary>
		public static string Shown
		{
			get
			{
				if (!IsOpen) return null;
				return instance.levelText.text + " | " + instance.pointsText.text + " | " + instance.xpText.text + " | " + instance.killsText.text.Replace("<color=#f8cc6b>", "").Replace("</color>", "") + " | " +
					string.Join(", ", Enumerable.Range(0, LevelRules.StatCount).Select(i => LevelRules.StatNames[i] + " " + instance.values[i].text).ToArray());
			}
		}

		/// <summary>Clicks a stat's + or - button (tests), as a player would.</summary>
		public static bool Click(int stat, bool add)
		{
			if (!IsOpen) return false;
			Button b = add ? instance.plus[stat] : instance.minus[stat];
			if (!b.gameObject.activeInHierarchy || !b.interactable) return false;
			b.onClick.Invoke();
			return true;
		}
	}
}
