using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The box that covers the screen while the editor opens, from the click on EDITOR until the editor is ready. Without
	/// it, the editor scene showed the asset bundle's old screen of 2023 for a moment (Return, Mainmenu, two icon buttons, an
	/// empty "Button" panel), and then the editor without its objects while Raft's islands were read for them. In Raft's
	/// look: a title, what it is doing, and a bar while Raft's objects load.
	/// </summary>
	public static class EditorLoadingBox
	{
		static GameObject root;
		static Text status;
		static RectTransform fill;
		static string message = "";
		static float shownAt;
		// How far the bar is, and how far the current stage takes it (it moves there smoothly)
		static float shownFill, stageFill;

		public static bool Showing { get { return root != null; } }

		/// <summary>Covers the screen (kept through the scene change) until Hide.</summary>
		public static void Show(string text)
		{
			message = text ?? "";
			shownAt = Time.realtimeSinceStartup;
			shownFill = 0f; stageFill = 0.1f;
			if (root != null) return;
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_EditorLoading", 32000);
			root = canvas.gameObject;
			Object.DontDestroyOnLoad(root);

			// (the whole screen, and it takes the clicks: nothing half-made shows or reacts behind it)
			RectTransform back = UIKit.Rect("Backdrop", canvas.transform);
			UIKit.Stretch(back);
			back.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.06f, 0.035f, 1f);

			RectTransform box = UIKit.Rect("Box", canvas.transform);
			UIKit.Anchor(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 176f));
			UIKit.Surface(box, 8);

			Text title = UIKit.Label(box, "OPENING THE EDITOR", 22, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
			UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(520f, 40f));

			status = UIKit.Label(box, message, 16, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, "Status");
			UIKit.Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(520f, 44f));

			RectTransform track = UIKit.Rect("Bar", box);
			UIKit.Anchor(track, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(480f, 14f));
			UIKit.Background(track.gameObject, UIKit.FieldBg, 5);
			fill = UIKit.Rect("Fill", track);
			fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
			UIKit.Background(fill.gameObject, UIKit.Accent, 5);

			root.AddComponent<Ticker>();
		}

		/// <summary>What it is doing now (a line under the title), and how far along the bar that stage goes (0-1).</summary>
		public static void Status(string text, float upTo)
		{
			message = text ?? "";
			stageFill = Mathf.Max(stageFill, Mathf.Clamp01(upTo));
		}

		public static void Hide()
		{
			if (root != null) Object.Destroy(root);
			root = null;
		}

		/// <summary>Keeps the line and the bar up to date, and takes the box away should the editor never open.</summary>
		class Ticker : MonoBehaviour
		{
			void Update()
			{
				float shown = Time.realtimeSinceStartup - shownAt;
				// (the scene didn't change after all, or something failed on the way: never leave the screen covered)
				bool atMenu = SceneManager.GetActiveScene().name == "MainMenuScene";
				if ((atMenu && shown > 15f) || shown > 240f) { Debug.LogWarning("[CUSTOM ISLANDS] The editor's loading box was still up after " + shown.ToString("F0") + " s: taken away"); Hide(); return; }

				int done = PlaceableCatalog.BuildDone, total = PlaceableCatalog.BuildTotal;
				bool loading = total > 0 && done < total && !PlaceableCatalog.IsBuilt;
				string dots = new string('.', 1 + (int)(Time.realtimeSinceStartup * 2f) % 3);
				if (status != null)
					status.text = loading ? "Loading Raft's objects from its islands (" + done + " of " + total + ")" + dots
						: message.TrimEnd('.') + dots;
				if (fill != null)
				{
					// (the stage's share; while Raft's islands are read, their part of it - from 25 % to 95 %)
					float target = loading ? Mathf.Max(stageFill, 0.25f + 0.7f * done / total) : stageFill;
					shownFill = Mathf.MoveTowards(shownFill, target, Time.unscaledDeltaTime * 1.5f);
					fill.anchorMax = new Vector2(Mathf.Clamp01(shownFill), 1f);
				}
			}
		}
	}
}
