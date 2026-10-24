using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A plan's ending (ROADMAP RF2): after the last main story step, every player sees the credits once - the title (the
	/// plan's endingtitle, or its name), the makers' names (credits) and the closing note (storyending, which also stays in
	/// Raft's notebook). The host watches for the story to end, shows them, tells every player (IslandNetMessage.Moment
	/// "ending") and keeps "shown" with the world, so a reload doesn't show them again. The world plays on; Raft's own
	/// Utopia ending is not touched.
	/// </summary>
	public class StoryCredits : MonoBehaviour
	{
		static StoryCredits instance;
		const char Sep = '\u001F';

		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		/// <summary>Tests: how often the credits were shown here, and what they showed.</summary>
		public static int TimesShown;
		public static string ShownTitle, ShownCredits, ShownNote;

		Text titleText, madeText, noteText;
		float openedAt;
		bool cursorWasFree;

		static bool seenNotOver;
		static float nextCheck;

		/// <summary>The plan has an ending to show: a closing note or makers' names.</summary>
		public static bool HasEnding { get { return StoryChain.StoryEnding.Trim().Length > 0 || StoryChain.Credits.Trim().Length > 0; } }

		/// <summary>Every frame (the host): the main story just ended here - the credits, once per world.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 1f;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || QuestBookPreview.Active || DynamicIslands.InEditor()) { seenNotOver = false; return; }
			if (!StoryChain.HasState || !HasEnding) return;
			// (only an ending seen happening: a world whose story was over before it loaded doesn't show them)
			if (!QuestBook.StoryOver) { seenNotOver = true; return; }
			if (!seenNotOver || StoryChain.CreditsShown) return;
			HostShow();
		}

		/// <summary>Host: show the credits to every player and remember it.</summary>
		public static void HostShow()
		{
			StoryChain.MarkCreditsShown();
			string data = StoryChain.EndingTitle + Sep + StoryChain.Credits + Sep + StoryChain.StoryEnding;
			IslandNetwork.SendToEveryone(new IslandNetMessage { Kind = IslandNetMessage.Moment, Ids = new[] { 0 }, Index = -1, Name = "ending", Data = data });
			Show(StoryChain.EndingTitle, StoryChain.Credits, StoryChain.StoryEnding);
		}

		public static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			string[] d = (msg.Data ?? "").Split(Sep);
			Show(d[0], d.Length > 1 ? d[1] : "", d.Length > 2 ? d[2] : "");
		}

		public static void Show(string title, string credits, string note)
		{
			if (instance == null) Build();
			instance.Open(title, credits, note);
		}

		public static void Close() { if (instance != null) instance.Hide(); }

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_Credits", 520);
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<StoryCredits>();
			// (a dark veil over the game, the credits on Raft's brown panel)
			RectTransform veil = UIKit.Rect("Veil", canvas.transform);
			UIKit.Stretch(veil);
			Image v = veil.gameObject.AddComponent<Image>();
			v.color = new Color(0f, 0f, 0f, 0.72f);

			RectTransform panel = UIKit.Panel(canvas.transform, "Panel", new RectOffset(26, 26, 20, 18), 10f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 620));
			Text end = UIKit.Label(panel, "THE END", 14, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold, "Kicker");
			UIKit.Size(end.gameObject, -1, 22);
			instance.titleText = UIKit.Label(panel, "", 34, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
			instance.titleText.resizeTextForBestFit = true;
			instance.titleText.resizeTextMinSize = 18;
			instance.titleText.resizeTextMaxSize = 34;
			UIKit.Size(instance.titleText.gameObject, -1, 52);
			Text made = UIKit.Label(panel, "Made by", 14, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, "MadeBy");
			UIKit.Size(made.gameObject, -1, 22);
			instance.madeText = UIKit.Label(panel, "", 20, Color.white, TextAnchor.UpperCenter, FontStyle.Normal, "Names");
			UIKit.Size(instance.madeText.gameObject, -1, 110);
			UIKit.Separator(panel);

			RectTransform sheet = UIKit.Rect("Sheet", panel);
			UIKit.ParchmentSheet(sheet);
			UIKit.Size(sheet.gameObject, -1, -1).flexibleHeight = 1f;
			UIKit.Vertical(sheet.gameObject, 0f, new RectOffset(28, 24, 22, 18));
			instance.noteText = UIKit.Label(sheet, "", 18, UIKit.ParchmentInk, TextAnchor.UpperLeft, FontStyle.Normal, "Note");
			instance.noteText.lineSpacing = 1.15f;
			instance.noteText.supportRichText = false;
			UIKit.Size(instance.noteText.gameObject, -1, -1).flexibleHeight = 1f;
			{ Shadow sh = instance.noteText.GetComponent<Shadow>(); if (sh != null) Destroy(sh); }

			RectTransform bottom = UIKit.Row(panel, 34f, 8f, "Bottom");
			Text hint = UIKit.Label(bottom, "The world goes on. The note stays in Raft's notebook (T). Esc to close", 13, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Hint");
			UIKit.Size(hint.gameObject, -1, -1, 1);
			UIKit.Button(bottom, "Back to the sea", Close, null, 150, 34);
			canvas.gameObject.SetActive(false);
		}

		void Open(string title, string credits, string note)
		{
			titleText.text = (title ?? "").Trim().Length > 0 ? title.Trim() : "The end";
			madeText.text = (credits ?? "").Trim();
			madeText.transform.parent.Find("MadeBy").gameObject.SetActive(madeText.text.Length > 0);
			madeText.gameObject.SetActive(madeText.text.Length > 0);
			noteText.text = (note ?? "").Trim();
			noteText.transform.parent.gameObject.SetActive(noteText.text.Length > 0);
			gameObject.SetActive(true);
			openedAt = Time.unscaledTime;
			cursorWasFree = Cursor.visible;
			if (!cursorWasFree) try { RAPI.ToggleCursor(true); } catch { }
			TimesShown++;
			ShownTitle = titleText.text;
			ShownCredits = madeText.text;
			ShownNote = noteText.text;
			Debug.Log("[CUSTOM ISLANDS] Credits: '" + ShownTitle + "' (" + ShownCredits.Split('\n').Count(s => s.Trim().Length > 0) + " names)");
		}

		void Hide()
		{
			if (!gameObject.activeSelf) return;
			gameObject.SetActive(false);
			if (!cursorWasFree) try { RAPI.ToggleCursor(false); } catch { }
		}

		void Update()
		{
			if (Time.unscaledTime - openedAt < 0.5f) return;
			if (EscKey.Down(this)) Hide();
			if (!LoadSceneManager.IsGameSceneLoaded) Hide();
		}
	}
}
