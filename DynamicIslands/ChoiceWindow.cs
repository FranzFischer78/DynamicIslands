using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A modal list to pick one thing from (a saved island, a map type, a world plan): a search field and one row per
	/// choice with a short description. Clicking a row picks it and closes the window.
	/// </summary>
	public class ChoiceWindow : MonoBehaviour
	{
		public class Choice
		{
			public string Value, Label, Detail;
			/// <summary>Optional: the row gets a delete (X) button that calls this; the row goes, the window stays open.</summary>
			public Action OnDelete;
			public string DeleteHint = "Delete this one";
			public Choice(string value, string label, string detail = "") { Value = value; Label = label; Detail = detail ?? ""; }
		}

		static ChoiceWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		Text titleText, countText;
		InputField search;
		RectTransform list;
		Action<string> onPick;
		readonly List<KeyValuePair<string, GameObject>> rows = new List<KeyValuePair<string, GameObject>>();
		List<Choice> shown = new List<Choice>();
		string emptyText;
		/// <summary>While a list of islands loads (OpenIslands): the look under way, what to list after, the button greyed meanwhile.</summary>
		IslandSummaries.Job loading;
		Func<IEnumerable<Choice>> loadChoices;
		Button openedBy;
		Text loadingText;

		/// <summary>Tests: the window is open and still loading its islands.</summary>
		public static bool Loading { get { return IsOpen && instance.loading != null; } }
		/// <summary>Tests: what the window says while it loads ("" when it doesn't).</summary>
		public static string LoadingText { get { return Loading && instance.loadingText != null ? instance.loadingText.text : ""; } }

		/// <summary>Tests: the title and the values listed now.</summary>
		public static string Title { get { return instance != null ? instance.titleText.text : ""; } }
		public static List<string> Values { get { Settle(); return instance != null && IsOpen ? instance.shown.Select(c => c.Value).ToList() : new List<string>(); } }
		public static string EmptyText { get { Settle(); return instance != null && IsOpen && instance.shown.Count == 0 ? instance.emptyText : null; } }
		public static List<Choice> Shown { get { Settle(); return instance != null && IsOpen ? instance.shown.ToList() : new List<Choice>(); } }

		/// <summary>Tests read or pick from the list at once: a list still loading finishes first (waiting for the worker).</summary>
		static void Settle() { if (Loading) instance.EndLoading(); }

		/// <summary>Tests: picks a value as a click on its row does (false if it isn't listed).</summary>
		public static bool PickValue(string value)
		{
			Settle();
			if (!IsOpen || !instance.shown.Any(c => c.Value == value)) return false;
			Action<string> pick = instance.onPick;
			Close();
			if (pick != null) pick(value);
			return true;
		}

		/// <summary>Tests: clicks a row's delete (X) button (false if it isn't listed or has none).</summary>
		public static bool DeleteValue(string value)
		{
			Settle();
			Choice c = IsOpen ? instance.shown.FirstOrDefault(x => x.Value == value && x.OnDelete != null) : null;
			if (c == null) return false;
			instance.Delete(c);
			return true;
		}

		void Delete(Choice c)
		{
			try { c.OnDelete(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Deleting '" + c.Value + "': " + e.Message); }
			if (!IsOpen) return; // (the delete closed the window: nothing left to show)
			Fill(shown.Where(x => x != c).ToList());
		}

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("ChoiceWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
			instance = blocker.AddComponent<ChoiceWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		/// <summary>A list to choose from; empty (optional) says why there is nothing in it.</summary>
		public static void Open(string title, IEnumerable<Choice> choices, Action<string> pick, string empty = null)
		{
			if (instance == null) return;
			instance.StopLoading();
			instance.emptyText = empty;
			instance.titleText.text = title.ToUpperInvariant();
			instance.onPick = pick;
			instance.Fill(choices.ToList());
			instance.search.text = "";
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
		}

		/// <summary>
		/// A list with saved islands in it: opens at once, saying "Loading islands... (n of N)" while their summaries are
		/// read (IslandSummaries: only the files changed since last time, on a worker thread), then lists choices(). Before,
		/// the window came only after every island's whole file was read - seconds with nothing on screen. The button that
		/// opened it (optional) is greyed meanwhile, and a second click while it loads does nothing.
		/// </summary>
		public static void OpenIslands(string title, Func<IEnumerable<Choice>> choices, Action<string> pick, string empty = null, Button from = null)
		{
			if (instance == null || Loading) return;
			instance.StopLoading();
			instance.emptyText = empty;
			instance.titleText.text = title.ToUpperInvariant();
			instance.onPick = pick;
			instance.loading = IslandSummaries.Prepare(SavedNames());
			instance.loadChoices = choices;
			instance.openedBy = from;
			if (from != null) from.interactable = false;
			instance.ShowLoading();
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
		}

		void ShowLoading()
		{
			foreach (Transform child in list) Destroy(child.gameObject);
			rows.Clear();
			shown = new List<Choice>();
			search.text = "";
			search.interactable = false;
			loadingText = UIKit.Label(list, "", 13, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, "Loading");
			loadingText.text = "Loading islands... (" + loading.Done + " of " + loading.Total + ")";
			countText.text = "";
		}

		/// <summary>The islands are read: the list is made.</summary>
		void EndLoading()
		{
			IslandSummaries.Job job = loading;
			Func<IEnumerable<Choice>> choices = loadChoices;
			StopLoading();
			IslandSummaries.Finish(job);
			List<Choice> made;
			try { made = choices != null ? choices().ToList() : new List<Choice>(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Listing the islands: " + e); made = new List<Choice>(); }
			Fill(made);
		}

		/// <summary>Leaves the loading state (the look goes on and is kept: the next list takes it).</summary>
		void StopLoading()
		{
			loading = null;
			loadChoices = null;
			loadingText = null;
			if (search != null) search.interactable = true;
			if (openedBy != null) openedBy.interactable = true;
			openedBy = null;
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.StopLoading();
			if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			instance.gameObject.SetActive(false);
			instance.onPick = null;
			EditorInput.IsTyping = false;
		}

		void Update()
		{
			if (loading != null)
			{
				if (loading.Finished || loading.Done >= loading.Total) EndLoading();
				else if (loadingText != null) loadingText.text = "Loading islands... (" + loading.Done + " of " + loading.Total + ")";
			}
			EditorInput.IsTyping = search.isFocused;
			if (EscKey.Down(this)) Close();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			titleText = UIKit.Label(head, "", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			countText = UIKit.Label(head, "", 12, UIKit.TextMuted, TextAnchor.MiddleRight);
			search = UIKit.Field(panel, "Search...", "", 30f, "Type part of a name");
			search.onValueChanged.AddListener(v => Filter());
			RectTransform box = UIKit.Rect("List", panel);
			UIKit.Size(box.gameObject, -1, 380);
			ScrollRect scroll;
			list = UIKit.ScrollList(box, out scroll, 4f);
			UIKit.Stretch((RectTransform)scroll.transform);
			RectTransform buttons = UIKit.Row(panel, 32f, 8f, "Buttons");
			UIKit.Size(UIKit.Label(buttons, "", 12, UIKit.TextMuted).gameObject, -1, -1, 1);
			UIKit.Button(buttons, "Cancel", Close, "Close without choosing (Esc)", 100, 32);
		}

		void Fill(List<Choice> choices)
		{
			foreach (Transform child in list) Destroy(child.gameObject);
			rows.Clear();
			shown = choices;
			foreach (Choice c in choices)
			{
				Choice choice = c;
				RectTransform row = choice.OnDelete != null ? UIKit.Row(list, 34f, 4f, "Row") : null;
				Button b = UIKit.Button(row != null ? row : list, "", () => { Action<string> pick = onPick; Close(); if (pick != null) pick(choice.Value); }, choice.Detail.Length > 0 ? choice.Detail : null, -1, 34f, 13);
				if (row != null)
				{
					UIKit.Size(b.gameObject, -1, -1, 1);
					UIKit.Button(row, "X", () => Delete(choice), choice.DeleteHint, 34, 34f, 13);
				}
				Text t = UIKit.LabelOf(b);
				t.alignment = TextAnchor.MiddleLeft;
				t.text = choice.Label + (choice.Detail.Length > 0 ? "   <color=#b89e70><size=11>" + choice.Detail + "</size></color>" : "");
				rows.Add(new KeyValuePair<string, GameObject>((choice.Label + " " + choice.Value + " " + choice.Detail).ToLowerInvariant(), row != null ? row.gameObject : b.gameObject));
			}
			if (choices.Count == 0)
			{
				Text none = UIKit.Label(list, "<i>" + (string.IsNullOrEmpty(emptyText) ? "Nothing to choose from yet." : emptyText) + "</i>", 13, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, "Empty");
				none.horizontalOverflow = HorizontalWrapMode.Wrap;
			}
			Filter();
		}

		void Filter()
		{
			string q = search.text.Trim().ToLowerInvariant();
			int shown = 0;
			foreach (var r in rows)
			{
				bool on = q.Length == 0 || r.Key.Contains(q);
				r.Value.SetActive(on);
				if (on) shown++;
			}
			countText.text = shown + " of " + rows.Count;
		}

		/// <summary>Saved islands to choose from (not downloaded copies or islands generated while sailing).</summary>
		public static IEnumerable<Choice> Islands()
		{
			// (the title from the summary cache: reading each whole file for it took seconds - OpenIslands reads the changed ones first)
			return SavedNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => new Choice(n, n, IslandSummaries.TitleOf(n)));
		}

		/// <summary>The editor start window's Open...: the saved islands, then the generated ones (gen-..., made while sailing
		/// or with Make) - to look at one, or keep it under a name of your own with Save as (CB16).</summary>
		public static IEnumerable<Choice> IslandsToOpen()
		{
			var generated = IslandSpawner.ListSavedIslands().Where(n => !IslandNetwork.IsDownloadName(n) && n.StartsWith(CustomIslandSpawner.GeneratedPrefix, StringComparison.OrdinalIgnoreCase))
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => new Choice(n, n, GeneratedDetail));
			return Islands().Concat(generated);
		}

		/// <summary>A generated island's line in the Open... list.</summary>
		public const string GeneratedDetail = "made by the generator (while sailing, or with Make) - Save as a name of your own to keep it";

		/// <summary>How many saved islands the player has (the editor's start window).</summary>
		public static int SavedIslandCount() { return SavedNames().Count(); }

		static IEnumerable<string> SavedNames()
		{
			return IslandSpawner.ListSavedIslands().Where(n => !IslandNetwork.IsDownloadName(n) && !n.StartsWith(CustomIslandSpawner.GeneratedPrefix, StringComparison.OrdinalIgnoreCase));
		}

		public static IEnumerable<Choice> Types()
		{
			return MapTypes.All.Select(t => new Choice(t.Name, t.Label, t.Description));
		}
	}
}
