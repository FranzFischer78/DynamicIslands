using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The mod's own error note: RML's "AN ERROR OCCURED!" can't be clicked, so when an exception comes from the mod's code
	/// (or from Raft's code while the mod is doing something - a Raft script in a scene the mod loaded for its objects) a
	/// small "Custom Islands: an error happened - click for details" note shows at the bottom left. Clicking it opens a box
	/// with the message and the first stack lines, whose code it is (Raft, Custom Islands, another mod: from the stack), Copy,
	/// Open the log folder and where to report it. Each such error is also written to Mods\DynamicIslands\errors.txt (time,
	/// versions, message, stack), kept under MaxFileBytes.
	/// </summary>
	public static class ErrorNotes
	{
		public const string CanvasName = "CustomIslands_ErrorNote", FileName = "errors.txt";
		public const string NoteLine = "Custom Islands: an error happened - click for details";
		/// <summary>errors.txt is cut to its newer half past this size.</summary>
		public const int MaxFileBytes = 256 * 1024;
		/// <summary>How many times the same error (message and first stack line) is written to errors.txt in a session.</summary>
		const int SameErrorTimes = 3;
		/// <summary>How long the note stays when it isn't clicked (s).</summary>
		const float NoteSeconds = 30f;
		const int StackLinesShown = 8, StackLinesKept = 40;

		/// <summary>One error the note is about.</summary>
		public class Record
		{
			public DateTime Time;
			public string Message, Stack, Whose;
			public int Count = 1;
		}

		static readonly List<Record> records = new List<Record>();
		static readonly Dictionary<string, Record> bySignature = new Dictionary<string, Record>();
		static bool installed, inside, pending;
		/// <summary>The player closed the note or read the box: the same errors again don't bring it back, a new one does.</summary>
		static bool muted;
		static Canvas canvas;
		static Text noteText;
		static float hideAt;

		/// <summary>The errors noted this session, the newest last (at most 20).</summary>
		public static IList<Record> Records { get { return records.AsReadOnly(); } }
		public static Record Last { get { return records.Count > 0 ? records[records.Count - 1] : null; } }
		public static string FilePath { get { return Path.Combine(DynamicIslands.assetpath, FileName); } }
		/// <summary>Tests: whether the note is on the screen, and its text.</summary>
		public static bool NoteShown { get { return canvas != null && canvas.gameObject.activeSelf; } }
		public static string NoteText { get { return noteText != null ? noteText.text : ""; } }

		#region Hooked into Unity's log

		/// <summary>The mod starts: errors from now on are looked at.</summary>
		public static void Install()
		{
			if (installed) return;
			Application.logMessageReceived += OnLog;
			installed = true;
			Host.Ensure();
		}

		/// <summary>RML's Unload: the log handler and the note go.</summary>
		public static void Uninstall()
		{
			Application.logMessageReceived -= OnLog;
			installed = false;
			Host.Remove();
			if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
			canvas = null;
		}

		/// <summary>Every message of Unity's log (main thread). Never throws, never logs from inside itself.</summary>
		internal static void OnLog(string message, string stack, LogType type)
		{
			if (inside) return;
			inside = true;
			try { Consider(message ?? "", stack ?? "", type); }
			catch { }
			finally { inside = false; }
		}

		/// <summary>Whether a log message is one the note is about, and whose code it came from (null: not ours to note).</summary>
		internal static string WhoseFor(string message, string stack, LogType type)
		{
			// (Unity's own: a camera drawing while Raft is minimised)
			if (message.StartsWith("Screen position out of view frustum")) return null;
			bool ownReport = type == LogType.Error && message.StartsWith("[CUSTOM ISLANDS]") && message.Contains("Exception");
			if (type != LogType.Exception && !ownReport) return null;
			// (the mod's own "... failed: <exception>" lines carry the exception's stack in the message)
			string top; bool byMod;
			Owners(message + "\n" + stack, out top, out byMod);
			if (top == Mod) return Mod;
			if (byMod) return (top ?? "Raft") + " (called by " + Mod + ")";
			if (ownReport) return Mod;
			string scenes = PlaceableCatalog.QuietSceneNames;
			if (scenes.Length > 0 && (top == null || top == RaftCode))
				return RaftCode + " (a script in " + scenes + ", which " + Mod + " loaded to take objects from)";
			return null;
		}

		const string Mod = "Custom Islands", RaftCode = "Raft";

		static void Consider(string message, string stack, LogType type)
		{
			string whose = WhoseFor(message, stack, type);
			if (whose == null) return;
			// (the exception's own stack when the message carries one - the mod's "... failed: <exception>" lines - then where it was logged)
			string inMessage = StackInMessage(message);
			bool carries = inMessage.Contains(" at ");
			string trace = FirstLines(carries ? inMessage + "\n" + stack : stack.Trim().Length > 0 ? stack : inMessage, StackLinesKept);
			string first = message.Split('\n')[0].Trim();
			string signature = first + "|" + trace.Split('\n')[0];
			Record r;
			bool fresh = !bySignature.TryGetValue(signature, out r);
			if (!fresh) r.Count++;
			else
			{
				if (bySignature.Count >= 200) bySignature.Clear(); // (an error every frame with a new message each time)
				r = new Record { Time = DateTime.Now, Message = message.Trim(), Stack = trace, Whose = whose };
				bySignature[signature] = r;
				muted = false;
			}
			records.Remove(r);
			records.Add(r);
			while (records.Count > 20) records.RemoveAt(0);
			if (r.Count <= SameErrorTimes) Append(r);
			if (!muted) pending = true;
			Host.Ensure();
		}

		/// <summary>
		/// Whose code each stack line is: the first line that is neither Unity, .NET nor Harmony decides (top); byMod: the
		/// mod's code is somewhere on the stack (it called whatever threw).
		/// </summary>
		internal static void Owners(string text, out string top, out bool byMod)
		{
			top = null; byMod = false;
			foreach (string raw in text.Split('\n'))
			{
				string method = MethodOf(raw);
				if (method == null) continue;
				string owner = OwnerOf(method);
				if (owner == null) continue;
				if (owner == Mod) byMod = true;
				if (top == null) top = owner;
			}
		}

		/// <summary>"Ns.Type.Method" from a stack line ("  at Ns.Type.Method (...)", Unity's "Ns.Type:Method (...)", Harmony's
		/// "DMD&lt;Type::Method&gt;"); null if the line is no stack line.</summary>
		static string MethodOf(string line)
		{
			string l = line.Trim();
			if (l.StartsWith("at ")) l = l.Substring(3).Trim();
			int dmd = l.IndexOf("DMD<");
			if (dmd >= 0) { int end = l.IndexOf('>', dmd); if (end > dmd) l = l.Substring(dmd + 4, end - dmd - 4).Replace("::", "."); }
			int paren = l.IndexOf('(');
			if (paren <= 0) return null;
			l = l.Substring(0, paren).Trim().Replace(':', '.');
			if (l.Length == 0 || l.Contains(" ") || !l.Contains(".")) return null;
			return l;
		}

		static readonly string[] Framework = { "UnityEngine.", "Unity.", "System.", "Mono.", "HarmonyLib.", "MonoMod.", "TMPro.", "Microsoft.", "(wrapper" };

		/// <summary>Whose a method is: the mod, the mod loader, Raft (its scripts have no namespace), another mod (its
		/// namespace); null for Unity, .NET and Harmony.</summary>
		static string OwnerOf(string method)
		{
			if (method.StartsWith("DynamicIslands.")) return Mod;
			foreach (string f in Framework) if (method.StartsWith(f)) return null;
			if (method.StartsWith("RaftModLoader.") || method.StartsWith("HMLLibrary.")) return "the mod loader (RML)";
			int dot = method.LastIndexOf('.');
			string type = dot > 0 ? method.Substring(0, dot) : method;
			// (Raft's own scripts, and their compiler-made helpers "Type+<Method>d__3")
			if (!type.Split('+')[0].Contains(".")) return RaftCode;
			return "another mod (" + type.Split('.')[0] + ")";
		}

		static string StackInMessage(string message)
		{
			int at = message.IndexOf("\n");
			return at >= 0 ? message.Substring(at + 1) : "";
		}

		static string FirstLines(string text, int n)
		{
			return string.Join("\n", text.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).Take(n).ToArray());
		}

		#endregion

		#region errors.txt

		/// <summary>The lines errors.txt gets for an error.</summary>
		internal static string Entry(Record r)
		{
			return "=== " + r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " | " + HelpLinks.Versions + " | " + r.Whose + "\n" +
				r.Message + "\n" + (r.Stack.Length > 0 ? r.Stack + "\n" : "") + "\n";
		}

		static void Append(Record r)
		{
			try
			{
				Directory.CreateDirectory(DynamicIslands.assetpath);
				string path = FilePath;
				File.AppendAllText(path, Entry(r), Encoding.UTF8);
				if (new FileInfo(path).Length > MaxFileBytes)
				{
					// (the newer half, from an entry's start)
					string all = File.ReadAllText(path, Encoding.UTF8);
					int from = all.IndexOf("\n=== ", all.Length / 2, StringComparison.Ordinal);
					SafeFile.WriteAllText(path, "(older errors cut off)\n" + (from >= 0 ? all.Substring(from + 1) : Entry(r)));
				}
			}
			catch { } // (a full disk or a locked file: the note still shows, the log still has it)
		}

		#endregion

		#region The note and the details box

		/// <summary>Shows the note now (tests; errors show it at the next frame).</summary>
		public static void ShowNote()
		{
			if (canvas == null) Build();
			int n = records.Sum(r => r.Count);
			noteText.text = NoteLine + (n > 1 ? " (" + n + ")" : "");
			canvas.gameObject.SetActive(true);
			hideAt = Time.unscaledTime + NoteSeconds;
		}

		public static void HideNote() { if (canvas != null) canvas.gameObject.SetActive(false); }

		static void Build()
		{
			canvas = UIKit.CreateCanvas(CanvasName, 885); // (under the info box (890), which opens over it)
			UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
			RectTransform root = (RectTransform)canvas.transform;
			RectTransform panel = UIKit.Panel(root, "Note", new RectOffset(10, 8, 6, 6), 4f);
			UIKit.Anchor(panel, Vector2.zero, new Vector2(12f, 12f), new Vector2(470f, 0f));
			RectTransform row = UIKit.Row(panel, 30f, 6f, "Row");
			Button open = UIKit.Button(row, NoteLine, OpenDetails, "What went wrong, whose code it was and where to report it", -1, 30f, 13);
			open.name = "Details";
			noteText = UIKit.LabelOf(open);
			noteText.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(open.gameObject, -1, 30f, 1);
			Button close = UIKit.Button(row, "x", () => { muted = true; HideNote(); }, "Hide this note (errors.txt keeps the error; a new error shows it again)", 30, 30f, 13);
			close.name = "Close";
			canvas.gameObject.SetActive(false);
		}

		/// <summary>The details box of the newest error.</summary>
		public static void OpenDetails()
		{
			Record r = Last;
			if (r == null) return;
			muted = true;
			HideNote();
			InfoWindow.Open("Custom Islands: an error happened", DetailsText(r),
				new InfoWindow.Choice("Copy", () => { GUIUtility.systemCopyBuffer = CopyText(r); InfoWindow.SetStatus("Copied: paste it (Ctrl+V) into a GitHub issue or a Discord post"); }, "Put the error, its stack and your versions on the clipboard"),
				new InfoWindow.Choice("Open the log folder", () => { HelpLinks.OpenLogFolder(); InfoWindow.SetStatus("Attach Player.log (and Player-prev.log) from " + HelpLinks.LogFolder); }, "Raft's log folder, with Player.log and Player-prev.log"),
				new InfoWindow.Choice("Report on GitHub", () => HelpLinks.Open(HelpLinks.NewIssueUrl), "A new issue on the mod's GitHub page, with the report form in it", true),
				new InfoWindow.Choice("Post on Discord", () => HelpLinks.Open(HelpLinks.Discord), "The Custom Islands Discord server", true),
				new InfoWindow.Choice("Close", InfoWindow.Close, "Close this box (Esc)"));
		}

		/// <summary>What the details box says about an error.</summary>
		public static string DetailsText(Record r)
		{
			string stack = FirstLines(r.Stack, StackLinesShown);
			bool ours = r.Whose.StartsWith(Mod) || r.Whose.Contains("called by " + Mod) || r.Whose.Contains("which " + Mod);
			return "<b>Whose code:</b> " + Escape(r.Whose) + "\n" +
				"<b>When:</b> " + r.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + (r.Count > 1 ? " (" + r.Count + " times)" : "") + "   <b>Versions:</b> " + HelpLinks.Versions + "\n\n" +
				"<b>Message:</b>\n" + Escape(FirstLines(r.Message, 4)) + "\n\n" +
				(stack.Length > 0 ? "<b>Where (first stack lines):</b>\n<size=12>" + Escape(stack) + "</size>\n\n" : "") +
				(ours ? "Please report it, so it can be fixed: <b>Copy</b> puts the error on the clipboard; paste it into a GitHub issue (" + HelpLinks.Issues + ") " +
					"or the Custom Islands Discord (" + HelpLinks.Discord + "), with what you were doing, and attach <i>Player.log</i> from <b>Open the log folder</b>."
					: "This came from another part of the game; the log may say more.") + "\n" +
				"Every such error is also kept in " + FilePath + ".";
		}

		/// <summary>What Copy puts on the clipboard.</summary>
		public static string CopyText(Record r)
		{
			return "Custom Islands error (" + HelpLinks.Versions + ")\n" +
				"Time: " + r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + (r.Count > 1 ? " (" + r.Count + " times)" : "") + "\n" +
				"Whose code: " + r.Whose + "\n" +
				"Message: " + r.Message + "\n" +
				"Stack:\n" + r.Stack + "\n";
		}

		/// <summary>Rich text off: a message with &lt;b&gt; or a generic type's &lt;T&gt; shows as it is.</summary>
		static string Escape(string s) { return (s ?? "").Replace("<", "<​"); }

		/// <summary>Shows the note a frame after an error (not from inside Unity's log call) and hides it again after a while.</summary>
		class Host : MonoBehaviour
		{
			static Host instance;

			public static void Ensure()
			{
				if (instance != null) return;
				var go = new GameObject("CustomIslands_ErrorNotes");
				UnityEngine.Object.DontDestroyOnLoad(go);
				instance = go.AddComponent<Host>();
			}

			public static void Remove()
			{
				if (instance != null) UnityEngine.Object.Destroy(instance.gameObject);
				instance = null;
			}

			void Update()
			{
				if (pending)
				{
					pending = false;
					inside = true; // (an error while making the note isn't noted again)
					try { ShowNote(); }
					catch { }
					finally { inside = false; }
				}
				else if (NoteShown && Time.unscaledTime > hideAt) HideNote();
			}
		}

		#endregion
	}
}
