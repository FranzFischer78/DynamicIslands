using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Check's report (the World Plans window's Check): what can't work (problems), what may not work as meant
	/// (warnings) and what is good to know (tips), each with the rule it is about, why, how to fix it, and a button that
	/// shows that rule's card.
	/// </summary>
	public class PlanCheckWindow : MonoBehaviour
	{
		static PlanCheckWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		Text titleText, summaryText;
		RectTransform list;
		ScrollRect scroll;
		Action again;
		Action<int> showRule;

		/// <summary>What the report shows now (tests).</summary>
		public static List<PlanChecker.Finding> Shown { get; private set; }

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("PlanCheckWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
			instance = blocker.AddComponent<PlanCheckWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		/// <summary>Shows the findings of a check of the plan called name; again = Check again, showRule(i) = show rule i's card.</summary>
		public static void Open(string name, List<PlanChecker.Finding> findings, Action again, Action<int> showRule)
		{
			if (instance == null) return;
			instance.again = again;
			instance.showRule = showRule;
			instance.Fill(name, findings);
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
		}

		public static void Close()
		{
			if (instance == null) return;
			if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			instance.gameObject.SetActive(false);
		}

		void Update()
		{
			if (EscKey.Down(this)) Close();
		}

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			titleText = UIKit.Label(head, "CHECK", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			summaryText = UIKit.Label(panel, "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Summary");
			summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(summaryText.gameObject, -1, 40);
			RectTransform box = UIKit.Rect("Findings", panel);
			UIKit.Size(box.gameObject, -1, 470);
			UIKit.Background(box.gameObject, UIKit.GroupBg, 6);
			list = UIKit.ScrollList(box, out scroll, 6f);
			UIKit.Stretch((RectTransform)scroll.transform, 6, 6, 6, 6);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Label(buttons, "Problems: the rule can't work.  Warnings: it may not work as you mean.  Tips: good to know.", 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Button(buttons, "Check again", () => { if (again != null) again(); }, "Check the plan again (after changing it)", 130, 34f, 13);
			Button close = UIKit.Button(buttons, "Close", Close, "Back to the plan (Esc)", 110, 34f, 13);
			UIKit.Primary(close);
		}

		static readonly Color ProblemColor = new Color(1f, 0.55f, 0.48f, 1f), WarningColor = new Color(1f, 0.85f, 0.45f, 1f), TipColor = new Color(0.72f, 0.85f, 0.95f, 1f);

		public static Color ColorOf(PlanChecker.Level l) { return l == PlanChecker.Level.Problem ? ProblemColor : l == PlanChecker.Level.Warning ? WarningColor : TipColor; }

		void Fill(string name, List<PlanChecker.Finding> findings)
		{
			Shown = findings;
			foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			titleText.text = "CHECK: " + name.ToUpperInvariant();
			int problems = findings.Count(f => f.Level == PlanChecker.Level.Problem), warnings = findings.Count(f => f.Level == PlanChecker.Level.Warning), tips = findings.Count(f => f.Level == PlanChecker.Level.Tip);
			summaryText.text = problems == 0 && warnings == 0
				? "<color=#8fdc8f>√ Every rule can work.</color> " + (tips > 0 ? tips + " tip" + (tips == 1 ? "" : "s") + " below." : "Nothing to add.") + " Check can't play the quests for you: create a world with the plan and try it."
				: Count(problems, "problem", ProblemColor) + ", " + Count(warnings, "warning", WarningColor) + ", " + Count(tips, "tip", TipColor) + ". " +
					(problems > 0 ? "Fix the problems first: a rule with a problem never brings its island." : "The warnings may be what you mean - read them.");
			foreach (PlanChecker.Finding f in findings) Item(f);
			if (findings.Count == 0) UIKit.Label(list, "<i>Nothing found.</i>", 13, UIKit.TextMuted);
			scroll.verticalNormalizedPosition = 1f;
		}

		static string Count(int n, string word, Color c) { return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + n + " " + word + (n == 1 ? "" : "s") + "</color>"; }

		void Item(PlanChecker.Finding f)
		{
			RectTransform item = UIKit.Rect("Finding", list);
			UIKit.Background(item.gameObject, new Color(0.2f, 0.11f, 0.05f, 0.45f), 6);
			UIKit.Vertical(item.gameObject, 3f, new RectOffset(10, 10, 6, 8));
			RectTransform top = UIKit.Row(item, 22f, 8f, "Top");
			Text tag = UIKit.Label(top, f.Level == PlanChecker.Level.Problem ? "PROBLEM" : f.Level == PlanChecker.Level.Warning ? "WARNING" : "TIP", 12, ColorOf(f.Level), TextAnchor.MiddleLeft, FontStyle.Bold, "Level");
			UIKit.Size(tag.gameObject, 80);
			UIKit.Label(top, f.Rule >= 0 ? "" : "The plan", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			if (f.Rule >= 0 && showRule != null)
			{
				int rule = f.Rule;
				UIKit.Button(top, "Show rule " + (rule + 1), () => { Close(); showRule(rule); }, "Close the report and scroll to this rule's card", 120, 22f, 11);
			}
			Text text = UIKit.Label(item, f.Text, 13, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Text");
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			if (f.Fix.Length > 0)
			{
				Text fix = UIKit.Label(item, "<b>How to fix:</b> " + f.Fix, 12, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, "Fix");
				fix.horizontalOverflow = HorizontalWrapMode.Wrap;
			}
		}
	}
}
