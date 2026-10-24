using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>Keys of an island's own settings (IslandFile.Props, format 4), edited on the Island tab.</summary>
	public static class IslandProps
	{
		public const string Title = "info.title", Author = "info.author", Description = "info.description";
		/// <summary>An island without land: the terrain-local "x,z" that goes where it is spawned (the randomizer's extras: the middle of Raft's island).</summary>
		public const string Centre = "land.centre";
		/// <summary>Rules: in-game days until harvested things, killed animals and looted chests come back on this island ("" = the world's regrowDays, 0 = never).</summary>
		public const string RegrowDays = "rules.regrow";
		/// <summary>Older islands only: "on" turned the level up system on in a world. Ignored since the level up system became a
		/// world option only (New Game > World settings, the Levels command); kept so such files load as before.</summary>
		public const string Levels = "rules.levels";
		/// <summary>Rules: the earliest the spawn pool may bring this island by chance, in km the world has sailed ("" = any time).</summary>
		public const string NotBeforeKm = "rules.notbefore";
		/// <summary>The sailing speed the editor's "about N h" uses (the user's example: 40 km, about 5 h).</summary>
		public const float SailKmPerHour = 8f;
		/// <summary>The highest "not before": 24 h of sailing.</summary>
		public const int MaxNotBeforeKm = 192;

		/// <summary>"about 5 h of sailing" for a distance in km.</summary>
		public static string SailTime(float km)
		{
			float h = km / SailKmPerHour;
			return h < 1f ? "about " + Mathf.Max(1, Mathf.RoundToInt(h * 60f)) + " min of sailing" : "about " + h.ToString(h < 10f ? "0.#" : "0", System.Globalization.CultureInfo.InvariantCulture) + " h of sailing";
		}
	}

	/// <summary>A spawned island's own settings (IslandFile.Props), for the parts of the mod that act on it in a world.</summary>
	public class IslandSettings : MonoBehaviour
	{
		public Dictionary<string, string> Props = new Dictionary<string, string>();
		/// <summary>Height of the sea above the island's terrain base (IslandFile.WaterLevel): 20 m on a shallow seabed, 160 m on a deep sea floor.</summary>
		public float WaterLevel = IslandFile.DefaultWaterLevel;
	}

	/// <summary>The island rules editor's result: per-island overrides of the world's settings.</summary>
	public static class IslandRules
	{
		/// <summary>Days until things come back on this island: its own rule, or the world's regrowDays.</summary>
		public static int RegrowDays(IslandWorldState.Entry e)
		{
			IslandSettings s = e != null && e.Root != null ? e.Root.GetComponent<IslandSettings>() : null;
			int days;
			if (s != null && int.TryParse(ObjectProps.Get(s.Props, IslandProps.RegrowDays), out days)) return Mathf.Max(0, days);
			return WorldRules.RegrowDays; // (the host's)
		}
	}

	/// <summary>What a spawned island tells players about itself, and where its land is.</summary>
	public class IslandInfoTag : MonoBehaviour
	{
		public string FileName, Title, Author, Description;
		/// <summary>Centre of the land relative to the island's root, and how far the land reaches from it.</summary>
		public Vector3 LocalCentre;
		public float Radius;
	}

	/// <summary>
	/// The island info editor's result in a world: when a player first comes close to an island that has a name or a
	/// description, a banner shows it at the top of the screen for a few seconds ("Skull Rock - by Johan - Beware of
	/// the bears"). Once per island per session.
	/// </summary>
	public static class IslandInfo
	{
		const float ShowDistance = 30f;
		static readonly HashSet<string> shown = new HashSet<string>();
		static float nextCheck;
		static CanvasGroup banner;
		static Text titleText, authorText, descText;
		static float shownAt = -100f;

		/// <summary>The text of the last banner shown (the automated tests look at it).</summary>
		public static string LastShown { get; private set; }

		/// <summary>The last banners asked for, oldest first (the automated tests look at them: one may still be waiting).</summary>
		public static readonly List<string> Recent = new List<string>();

		/// <summary>The text on the banner now - its message, else its title (null: no banner up). The tests look at it.</summary>
		public static string OnScreen { get { return banner != null && banner.gameObject.activeSelf ? (descText.text.Length > 0 && !titleText.gameObject.activeSelf ? descText.text : titleText.text) : null; } }

		/// <summary>How much of the banner shows now (0 when it is hidden or waits behind the credits).</summary>
		internal static float BannerAlpha { get { return banner != null && banner.gameObject.activeSelf ? banner.alpha : 0f; } }

		/// <summary>Forgets which islands were announced, so their banners show again (tests).</summary>
		public static void ForgetShown() { shown.Clear(); waiting.Clear(); LastShown = null; LastMessage = null; }

		public static void Tag(GameObject root, IslandFile island)
		{
			IslandSettings settings = root.AddComponent<IslandSettings>();
			settings.Props = new Dictionary<string, string>(island.Props);
			settings.WaterLevel = island.WaterLevel;
			string title = ObjectProps.Get(island.Props, IslandProps.Title), desc = ObjectProps.Get(island.Props, IslandProps.Description);
			if (title.Length == 0 && desc.Length == 0) return;
			// (the user, 2026-10-19: an island without a quest shows no name/description banner when you get near; only islands with quests keep it)
			if (!IslandQuest.HasQuest(island.Props)) return;
			IslandInfoTag tag = root.AddComponent<IslandInfoTag>();
			tag.FileName = island.Name;
			tag.Title = title;
			tag.Author = ObjectProps.Get(island.Props, IslandProps.Author);
			tag.Description = desc;
			Vector2 land = IslandSpawner.LandCentre(island);
			tag.LocalCentre = new Vector3(land.x, island.WaterLevel, land.y);
			tag.Radius = IslandSpawner.LandRadius(island);
		}

		/// <summary>Banners waiting their turn: a message that comes while another is still being read (title, author, text).</summary>
		static readonly Queue<string[]> waiting = new Queue<string[]>();
		/// <summary>How long the banner up now stays (ReadSeconds of its text).</summary>
		static float showFor = 5f;

		/// <summary>
		/// How long a banner stays up: long enough to read it - about 15 letters a second on top of 3 s, 5 s at least,
		/// 16 at most. The next one waits until it has gone (an island's description was replaced by its quest's intro
		/// after 3 s, before it could be read - CB17).
		/// </summary>
		public static float ReadSeconds(string title, string description)
		{
			return Mathf.Clamp(3f + (title.Length + description.Length) / 15f, 5f, 16f);
		}

		/// <summary>The world was left: its banners still waiting aren't shown in the next one.</summary>
		internal static void Forget() { waiting.Clear(); }

		public static void Tick()
		{
			// (the story's credits are up: the banner waits, hidden, and goes on once they close - it showed faintly behind
			// the top of the credits card, RF2)
			if (StoryCredits.IsOpen)
			{
				if (banner != null && banner.gameObject.activeSelf) { banner.alpha = 0f; shownAt += Time.unscaledDeltaTime; }
				return;
			}
			if (banner != null && banner.gameObject.activeSelf)
			{
				float t = Time.unscaledTime - shownAt;
				banner.alpha = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((showFor - t) / 1.2f);
				if (t > showFor) banner.gameObject.SetActive(false);
			}
			// (the next one only once this one has faded out: one after the other, never on top of each other)
			if ((banner == null || !banner.gameObject.activeSelf) && waiting.Count > 0) { string[] next = waiting.Dequeue(); Display(next[0], next[1], next[2]); }
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 0.5f;
			if (!LoadSceneManager.IsGameSceneLoaded) { shown.Clear(); return; }
			Network_Player player = null;
			try { player = ComponentManager<Network_Player>.Value; } catch { }
			if (player == null) return;
			foreach (GameObject root in IslandSpawner.SpawnedRoots)
			{
				if (root == null) continue;
				IslandInfoTag tag = root.GetComponent<IslandInfoTag>();
				if (tag == null || shown.Contains(tag.FileName)) continue;
				Vector3 c = root.transform.position + tag.LocalCentre, p = player.transform.position;
				if (new Vector2(c.x - p.x, c.z - p.z).magnitude > tag.Radius + ShowDistance) continue;
				shown.Add(tag.FileName);
				Show(tag.Title, tag.Author, tag.Description);
			}
		}

		/// <summary>A trigger zone's message, in the same banner (without a title).</summary>
		public static void ShowMessage(string text)
		{
			Show("", "", text);
			LastMessage = text;
		}

		/// <summary>The last zone message shown (the automated tests look at it).</summary>
		public static string LastMessage { get; private set; }

		/// <summary>
		/// A banner at the top of the screen. One that comes while another is up waits its turn (ReadSeconds): two
		/// messages at once (a zone's warning and the quest's step, a story item and the beacon) both get read -
		/// the second one used to replace the first at once (found building the library's Signal Rock).
		/// </summary>
		public static void Show(string title, string author, string description)
		{
			if (banner == null) Build();
			// (a zone's message has no title: logged as a message, not as arriving at an "Unknown island")
			bool message = title.Length == 0 && author.Length == 0 && description.Length > 0;
			LastShown = message ? description : (title.Length > 0 ? title : "Unknown island") + (author.Length > 0 ? " | by " + author : "") + (description.Length > 0 ? " | " + description : "");
			Debug.Log("[CUSTOM ISLANDS] " + (message ? "Message: " : "Arriving at: ") + LastShown);
			Recent.Add(LastShown);
			if (Recent.Count > 20) Recent.RemoveAt(0);
			bool busy = banner.gameObject.activeSelf;
			if (busy || waiting.Count > 0)
			{
				string[] next = { title, author, description };
				// (the same banner again while it is up or waiting: once)
				bool same = descText.text == description && (title.Length == 0 || titleText.text == title);
				if (!(same && busy) && !waiting.Any(w => w[0] == title && w[1] == author && w[2] == description)) waiting.Enqueue(next);
				while (waiting.Count > 4) waiting.Dequeue();
				return;
			}
			Display(title, author, description);
		}

		static void Display(string title, string author, string description)
		{
			titleText.gameObject.SetActive(title.Length > 0 || description.Length == 0);
			titleText.text = title.Length > 0 ? title : "Unknown island";
			authorText.text = author.Length > 0 ? "by " + author : "";
			authorText.gameObject.SetActive(author.Length > 0);
			descText.text = description;
			descText.gameObject.SetActive(description.Length > 0);
			banner.gameObject.SetActive(true);
			banner.alpha = 0f;
			shownAt = Time.unscaledTime;
			showFor = ReadSeconds(title, author.Length > 0 ? "by " + author + " " + description : description);
		}

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_IslandBanner", 400);
			Object.DontDestroyOnLoad(canvas.gameObject);
			RectTransform panel = UIKit.Rect("Banner", canvas.transform);
			UIKit.Anchor(panel, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(620, 0));
			UIKit.Surface(panel); // Raft's menu look
			UIKit.Vertical(panel.gameObject, 4f, new RectOffset(24, 24, 14, 16), true);
			titleText = UIKit.Label(panel, "", 30, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
			authorText = UIKit.Label(panel, "", 14, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, "Author");
			descText = UIKit.Label(panel, "", 17, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, "Description");
			descText.supportRichText = false;
			foreach (Graphic g in panel.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
			banner = canvas.gameObject.AddComponent<CanvasGroup>();
			banner.interactable = false; banner.blocksRaycasts = false;
			canvas.gameObject.SetActive(false);
		}
	}
}
