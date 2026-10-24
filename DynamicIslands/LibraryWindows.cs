using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Share" an island or a plan (the editor's Islands window and the World plan window): what goes along (the islands
	/// its rules bring), the entry's info (title, author, summary, description, tags, players, length, remix), a picture
	/// of the editor's view (Take picture - the automatic one is the view when the window opens), then Export writes the
	/// pack (.zip) to Mods\DynamicIslands\exports and Share opens the island library's page to send it in.
	/// </summary>
	public static class LibraryExportWindow
	{
		public const string WindowName = "LibraryExportWindow";
		public const string SubmitUrl = "https://github.com/SwedenJohansson/CustomIslands-Library/issues/new?template=submit.yml";

		static RectTransform window;
		static Text title, includes, basedOn, status;
		static InputField titleField, authorField, summaryField, descriptionField, tagsField, playersField, lengthField;
		static Button remixButton, shareButton, openButton;
		static RawImage preview, iconPreview;
		static string islandName, planName, lastZip;
		static LibraryInfo info;
		static byte[] pictureJpg, iconJpg;

		public static bool IsOpen { get { return window != null && window.gameObject.activeSelf; } }
		public static RectTransform Window { get { return window; } }
		public static string LastZip { get { return lastZip; } }
		public static string Status { get { return status != null ? status.text : ""; } }

		public static void Create(Transform canvas)
		{
			window = UIKit.Rect(WindowName, canvas);
			UIKit.Stretch(window);
			window.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			RectTransform panel = UIKit.Panel(window, "Panel", new RectOffset(16, 16, 12, 14), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			title = UIKit.Label(head, "SHARE", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Label(head, "a pack (.zip) to send, or to put in the island library", 11, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Italic);
			includes = UIKit.Label(panel, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Includes");
			includes.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(includes.gameObject, -1, 36);

			RectTransform columns = UIKit.Rect("Columns", panel);
			UIKit.Horizontal(columns.gameObject, 12f).childForceExpandWidth = false;
			RectTransform left = UIKit.Rect("Left", columns);
			UIKit.Vertical(left.gameObject, 5f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(left.gameObject, 440);
			titleField = FieldRow(left, "Title", "e.g. First Voyage", "The name shown in the library");
			authorField = FieldRow(left, "Author", "your name", "Who made it (shown in the library, and kept with the islands)");
			summaryField = FieldRow(left, "Summary", "one line for the list", "One line shown in the library's list");
			UIKit.Size(UIKit.Label(left, "Description", 12, UIKit.TextMuted).gameObject, -1, 16);
			descriptionField = UIKit.TextArea(left, "What players find, how long it takes, what you had in mind (e.g. 'best with Fierce monsters')", 66);
			tagsField = FieldRow(left, "Tags", "e.g. adventure, quest, short", "Words to find it by, separated by commas");
			RectTransform pair = UIKit.Row(left, 28f, 6f, "Pair");
			UIKit.Size(UIKit.Label(pair, "Players", 12, UIKit.TextMuted).gameObject, 80);
			playersField = UIKit.Field(pair, "1-8", "", 28f, "How many players it's made for");
			UIKit.Size(UIKit.Label(pair, "Length", 12, UIKit.TextMuted).gameObject, 60);
			lengthField = UIKit.Field(pair, "about 1 hour", "", 28f, "How long it takes to play");
			remixButton = UIKit.Button(left, "", () => { info.remix = !info.remix; ShowRemix(); }, "Whether others may change it and share their version (they must credit you either way)", -1, 28f, 13);
			basedOn = UIKit.Label(left, "", 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "BasedOn");
			UIKit.Size(basedOn.gameObject, -1, 16);

			RectTransform right = UIKit.Rect("Right", columns);
			UIKit.Vertical(right.gameObject, 6f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(right.gameObject, 320);
			UIKit.Size(UIKit.Label(right, "Picture (the editor's view)", 12, UIKit.TextMuted).gameObject, -1, 16);
			preview = UIKit.Picture(right, null, 320, 180, "Preview");
			RectTransform iconRow = UIKit.Row(right, 96f, 8f, "IconRow");
			iconPreview = UIKit.Picture(iconRow, null, 96, 96, "Icon");
			Text hint = UIKit.Label(iconRow, "Move the editor's camera to a good view, then Take picture. The icon is its middle.", 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic);
			hint.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Button(right, "Take picture", TakePicture, "A picture of what the editor's camera shows now (close this window, move the view, open it again - or take it now)", -1, 30f, 13);

			status = UIKit.Label(panel, "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Italic, "Status");
			status.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(status.gameObject, -1, 36);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			Button export = UIKit.Button(buttons, "Export", Export, "Write the pack (.zip) to Mods\\DynamicIslands\\exports", 130, 34f, 15);
			UIKit.Primary(export);
			openButton = UIKit.Button(buttons, "Open folder", OpenFolder, "Show the exported pack in Windows' file explorer", 130, 34f, 13);
			shareButton = UIKit.Button(buttons, "Share...", Share, "Open the library's GitHub submit form in your browser to send the pack in (attach the .zip). Or post it on the Discord", 130, 34f, 13);
			UIKit.Size(UIKit.Label(buttons, "", 12).gameObject, -1, -1, 1);
			UIKit.Button(buttons, "Close", Close, "Close", 110, 34f, 13);
			WindowKeys keys = window.gameObject.AddComponent<WindowKeys>();
			keys.Typing = () => new[] { titleField, authorField, summaryField, descriptionField, tagsField, playersField, lengthField }.Any(f => f != null && f.isFocused);
			keys.Close = Close;
			window.gameObject.SetActive(false);
		}

		static InputField FieldRow(Transform parent, string label, string placeholder, string hint)
		{
			RectTransform row = UIKit.Row(parent, 28f, 6f, label);
			UIKit.Size(UIKit.Label(row, label, 12, UIKit.TextMuted).gameObject, 80);
			return UIKit.Field(row, placeholder, "", 28f, hint);
		}

		/// <summary>Opens the window for a saved island.</summary>
		public static void OpenIsland(string name) { Open(name, null); }

		/// <summary>Opens the window for a saved plan.</summary>
		public static void OpenPlan(string name) { Open(null, name); }

		static void Open(string island, string plan)
		{
			if (window == null) return;
			islandName = island; planName = plan; lastZip = null;
			info = LibraryPack.LastInfo(plan != null ? LibraryPack.KindPlan : LibraryPack.KindIsland, plan ?? island);
			if (info.author.Length == 0) info.author = SteamName();
			title.text = plan != null ? "SHARE A PLAN: " + plan : "SHARE AN ISLAND: " + island;
			titleField.text = info.title; authorField.text = info.author; summaryField.text = info.summary; descriptionField.text = info.description;
			tagsField.text = string.Join(", ", info.tags ?? new string[0]); playersField.text = info.players; lengthField.text = info.length;
			basedOn.text = info.basedOn.Length > 0 ? "Based on " + info.basedOn : "";
			ShowRemix();
			includes.text = Includes();
			shareButton.interactable = openButton.interactable = false;
			SetStatus(info.version > 1 ? "Exported before: this will be version " + info.version + " of the same entry ('" + info.id + "')." : "Fill in what players should know, take a picture, and Export.", false);
			window.gameObject.SetActive(true);
			window.SetAsLastSibling();
			TakePicture();
		}

		public static void Close()
		{
			if (window != null) window.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		static string SteamName()
		{
			try { return Steamworks.SteamFriends.GetPersonaName(); } catch { return ""; }
		}

		static void ShowRemix()
		{
			UIKit.LabelOf(remixButton).text = "Others may change it and share their version:  " + (info.remix ? "YES" : "no");
			UIKit.SetActive(remixButton, info.remix);
		}

		/// <summary>What goes along: the islands and, for a plan, what comes from the player's own pool.</summary>
		static string Includes()
		{
			List<string> missing;
			WorldPlan plan = planName != null ? WorldPlan.Load(planName) : null;
			List<string> islands = LibraryPack.Collect(islandName != null ? new[] { islandName } : new string[0], plan, out missing);
			string text;
			if (plan != null) text = "The plan '" + plan.Name + "' with " + islands.Count + " island(s): " + (islands.Count > 0 ? string.Join(", ", islands.ToArray()) : "none (it only uses map types)") + ".";
			else
			{
				var brought = islands.Where(n => !n.Equals(islandName, StringComparison.OrdinalIgnoreCase)).ToList();
				text = "'" + islandName + "'" + (brought.Count > 0 ? " + " + brought.Count + " island(s) it brings: " + string.Join(", ", brought.ToArray()) : "") + ".";
			}
			if (missing.Count > 0) text += " MISSING (can't export): " + string.Join(", ", missing.ToArray()) + ".";
			if (plan != null && (plan.Random || plan.Rules.Any(r => r.What == "pool"))) text += " Random islands come from each player's own islands.";
			return text;
		}

		static void Read()
		{
			info.title = titleField.text.Trim(); info.author = authorField.text.Trim(); info.summary = summaryField.text.Trim();
			info.description = descriptionField.text.Trim(); info.players = playersField.text.Trim(); info.length = lengthField.text.Trim();
			info.tags = tagsField.text.Split(',').Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToArray();
			// (a first export takes its id from the title; later ones keep it, so they update the same entry)
			if (info.version <= 1) info.id = LibraryPack.IdFrom(info.title);
		}

		/// <summary>A picture of the editor's view (1280 x 720) and its middle as the icon (256 x 256). Public for tests.</summary>
		public static void TakePicture()
		{
			try
			{
				Camera cam = EditorCamera.Instance != null ? EditorCamera.Instance.GetComponent<Camera>() : Camera.main;
				if (cam == null) { SetStatus("No camera to take a picture with.", true); return; }
				Texture2D pic = Render(cam, 1280, 720), ico = Render(cam, 256, 256);
				pictureJpg = pic.EncodeToJPG(85);
				iconJpg = ico.EncodeToJPG(88);
				if (preview.texture != null) UnityEngine.Object.Destroy(preview.texture);
				if (iconPreview.texture != null) UnityEngine.Object.Destroy(iconPreview.texture);
				preview.texture = pic; iconPreview.texture = ico;
			}
			catch (Exception e) { SetStatus("Taking the picture failed: " + e.Message, true); }
		}

		static Texture2D Render(Camera cam, int w, int h)
		{
			var rt = new RenderTexture(w, h, 24);
			RenderTexture before = cam.targetTexture, active = RenderTexture.active;
			cam.targetTexture = rt;
			cam.aspect = w / (float)h;
			cam.Render();
			cam.targetTexture = before;
			cam.ResetAspect();
			RenderTexture.active = rt;
			var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
			tex.Apply();
			RenderTexture.active = active;
			UnityEngine.Object.Destroy(rt);
			return tex;
		}

		/// <summary>Writes the pack (public for tests).</summary>
		public static void Export()
		{
			Read();
			string error;
			string path = LibraryPack.Export(info, islandName, planName != null ? WorldPlan.Load(planName) : null, iconJpg, pictureJpg, out error);
			if (path == null) { SetStatus(error, true); return; }
			lastZip = path;
			shareButton.interactable = openButton.interactable = true;
			SetStatus("Exported: " + path + " (version " + info.version + "). Send it to a friend, or Share it with the island library.", false);
		}

		static void OpenFolder()
		{
			if (lastZip == null) return;
			try { HelpLinks.OpenFolder(lastZip, true); } catch (Exception e) { SetStatus("Could not open the folder: " + e.Message, true); }
		}

		static void Share()
		{
			OpenFolder();
			HelpLinks.Open(SubmitUrl);
			SetStatus("In the browser: sign in to GitHub, drag the .zip from the folder into the page, and send it. It's looked at before it goes into the library.", false);
		}

		static void SetStatus(string text, bool error) { status.text = text; status.color = error ? UIKit.Danger : UIKit.TextColor; }
	}

	/// <summary>
	/// "Import" (the editor's Islands window and the World plan window): the packs in Mods\DynamicIslands\import - pick one,
	/// see what it holds (and whether it was made with a newer version of the mod), Install. Under it everything installed
	/// from packs, each with Remove (which keeps what a saved world still uses), and the copies of islands downloaded from
	/// multiplayer hosts, unused generated islands and the files of deleted worlds with "Tidy up" (Housekeeping). Import is never offered inside a running world.
	/// </summary>
	public static class LibraryImportWindow
	{
		public const string WindowName = "LibraryImportWindow";

		static RectTransform window, packList, installedList;
		static Text detail, status, hostCopies;
		static Button installButton, sailingButton, replaceButton;
		static string picked, pendingRemove;
		static LibraryPackContents pack;
		static bool appearWhileSailing, replaceChanged;

		public static bool IsOpen { get { return window != null && window.gameObject.activeSelf; } }
		public static RectTransform Window { get { return window; } }
		public static string Status { get { return status != null ? status.text : ""; } }
		public static string Detail { get { return detail != null ? detail.text : ""; } }

		public static void Create(Transform canvas)
		{
			window = UIKit.Rect(WindowName, canvas);
			UIKit.Stretch(window);
			window.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			RectTransform panel = UIKit.Panel(window, "Panel", new RectOffset(16, 16, 12, 14), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "IMPORT AND INSTALLED", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Label(head, "islands and plans from packs (.zip)", 11, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Italic);

			RectTransform packs = UIKit.Group(panel, "Packs to import (Mods\\DynamicIslands\\import)", "Packs");
			RectTransform packTools = UIKit.Row(packs, 28f, 6f, "PackTools");
			UIKit.Label(packTools, "Put a pack you got into the import folder, then pick it here. Worlds you have started keep their version.", 12, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Button(packTools, "Open import folder", OpenImportFolder, "Show the import folder in Windows' file explorer", 160, 28f, 12);
			UIKit.Button(packTools, "Refresh", Refresh, "Look for packs again", 90, 28f, 12);
			RectTransform packBox = UIKit.Rect("PackBox", packs);
			UIKit.Size(packBox.gameObject, -1, 110);
			UIKit.Background(packBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect s1;
			packList = UIKit.ScrollList(packBox, out s1, 3f);
			UIKit.Stretch((RectTransform)s1.transform, 4, 4, 4, 4);
			detail = UIKit.Label(packs, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Detail");
			detail.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(detail.gameObject, -1, 64);
			RectTransform installRow = UIKit.Row(packs, 30f, 6f, "InstallRow");
			sailingButton = UIKit.Button(installRow, "", () => { appearWhileSailing = !appearWhileSailing; ShowToggles(); }, "An island (not a plan's islands) may also turn up by chance while sailing - in new worlds and in the ones you've started with random islands (you can untick it for a new world in World settings)", -1, 30f, 12);
			replaceButton = UIKit.Button(installRow, "", () => { replaceChanged = !replaceChanged; ShowToggles(); }, "When updating: also replace files of the earlier version that you have changed in the editor (else yours are kept)", 250, 30f, 12);
			installButton = UIKit.Button(installRow, "Install", Install, "Install the picked pack", 140, 30f, 14);
			UIKit.Primary(installButton);

			RectTransform inst = UIKit.Group(panel, "Installed from packs", "Installed");
			RectTransform instBox = UIKit.Rect("InstalledBox", inst);
			UIKit.Size(instBox.gameObject, -1, 130);
			UIKit.Background(instBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect s2;
			installedList = UIKit.ScrollList(instBox, out s2, 3f);
			UIKit.Stretch((RectTransform)s2.transform, 4, 4, 4, 4);
			RectTransform hostRow = UIKit.Row(inst, 28f, 6f, "HostCopies");
			hostCopies = UIKit.Label(hostRow, "", 12, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "HostCopiesText");
			tidyButton = UIKit.Button(hostRow, "Tidy up", TidyUp, "Click twice: move island copies from hosts no saved world uses to the deleted folder, move generated islands (gen-...) nothing uses to the deleted folder, and move the files of worlds you deleted (Raft's own world is gone) to worlds\\removed", 150, 28f, 12);

			status = UIKit.Label(panel, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Status");
			status.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(status.gameObject, -1, 52);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Size(UIKit.Label(buttons, "", 12).gameObject, -1, -1, 1);
			UIKit.Button(buttons, "Close", Close, "Close", 110, 34f, 13);
			EscKey.Add(window.gameObject, Close);
			window.gameObject.SetActive(false);
		}

		public static void Open()
		{
			if (window == null) return;
			if (LoadSceneManager.IsGameSceneLoaded) { DynamicIslands.Notify("Import from the main menu or the island editor, not inside a world.", true); return; }
			Directory.CreateDirectory(LibraryPack.ImportFolder);
			picked = null; pack = null; pendingRemove = null;
			appearWhileSailing = false; replaceChanged = false;
			window.gameObject.SetActive(true);
			window.SetAsLastSibling();
			SetStatus("", false);
			Refresh();
		}

		public static void Close() { if (window != null) window.gameObject.SetActive(false); }

		public static void Refresh()
		{
			foreach (Transform c in packList) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
			string[] zips = Directory.Exists(LibraryPack.ImportFolder) ? Directory.GetFiles(LibraryPack.ImportFolder, "*.zip").OrderBy(f => f).ToArray() : new string[0];
			if (zips.Length == 0) UIKit.Size(UIKit.Label(packList, "No packs here yet. Put a .zip you got into the import folder.", 12, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic).gameObject, -1, 26);
			foreach (string z in zips)
			{
				string path = z;
				Button b = UIKit.Button(packList, Path.GetFileName(z), () => Pick(path), null, -1, 26f, 12);
				b.name = "Pack_" + Path.GetFileName(z);
				UIKit.LabelOf(b).alignment = TextAnchor.MiddleLeft;
				UIKit.LabelOf(b).rectTransform.offsetMin = new Vector2(10, 0);
				UIKit.SetActive(b, path == picked);
			}
			ShowPack();
			ShowInstalled();
		}

		/// <summary>Picks a pack (public for tests): reads and checks it, and shows what it holds.</summary>
		public static void Pick(string path)
		{
			picked = path;
			string error;
			pack = LibraryPack.Read(path, out error);
			if (pack == null) SetStatus("This pack can't be installed: " + error, true);
			else SetStatus("", false);
			foreach (Button b in packList.GetComponentsInChildren<Button>(true)) UIKit.SetActive(b, b.name == "Pack_" + Path.GetFileName(path));
			ShowPack();
		}

		static void ShowPack()
		{
			bool ok = pack != null;
			installButton.interactable = ok;
			if (!ok) { detail.text = picked == null ? "Pick a pack above." : ""; ShowToggles(); return; }
			LibraryInfo i = pack.Info;
			LibraryInstalled already = LibraryPack.Installed().FirstOrDefault(e => e.id.Equals(i.id, StringComparison.OrdinalIgnoreCase));
			detail.text = (i.IsPlan ? "Plan" : "Island") + " '" + i.title + "' by " + i.author + ", version " + i.version + (i.summary.Length > 0 ? " - " + i.summary : "") +
				"\nIslands: " + string.Join(", ", pack.IslandNames.ToArray()) + (i.IsPlan ? "   Plan: " + Path.GetFileNameWithoutExtension(i.plan) : "") +
				(already != null ? "\nInstalled already (version " + already.version + "): installing updates it. Worlds you've started keep the version they started with." : "") +
				(pack.Newer ? "\nMade with Custom Islands " + i.minModVersion + " - you have " + LibraryPack.ModVersion + ". Some things may be missing, and a quest that needs them may not be finishable." : "");
			UIKit.LabelOf(installButton).text = pack.Newer ? "Install anyway" : already != null ? "Update" : "Install";
			ShowToggles();
		}

		static void ShowToggles()
		{
			bool island = pack != null && !pack.Info.IsPlan;
			sailingButton.gameObject.SetActive(island);
			UIKit.LabelOf(sailingButton).text = "Also let it turn up while sailing:  " + (appearWhileSailing ? "YES" : "no");
			UIKit.SetActive(sailingButton, appearWhileSailing);
			bool update = pack != null && LibraryPack.Installed().Any(e => e.id.Equals(pack.Info.id, StringComparison.OrdinalIgnoreCase));
			replaceButton.gameObject.SetActive(update);
			UIKit.LabelOf(replaceButton).text = "Replace files I changed:  " + (replaceChanged ? "YES" : "no");
			UIKit.SetActive(replaceButton, replaceChanged);
		}

		/// <summary>Installs the picked pack (public for tests).</summary>
		public static void Install()
		{
			if (pack == null) return;
			try
			{
				LibraryPack.Report r = LibraryPack.Install(pack, appearWhileSailing, replaceChanged, LibraryPack.SourceImport);
				SetStatus(r.ToString(), false);
				if (r.PlanName != null) SetStatus(r + "\nPick the plan '" + r.PlanName + "' in the New Game box.", false);
			}
			catch (Exception e) { SetStatus("Installing failed: " + e.Message, true); Debug.LogWarning("[CUSTOM ISLANDS] " + e); }
			ShowPack();
			ShowInstalled();
		}

		/// <summary>Sets the install options (tests).</summary>
		public static void SetOptions(bool sailing, bool replace) { appearWhileSailing = sailing; replaceChanged = replace; ShowToggles(); }

		static void ShowInstalled()
		{
			foreach (Transform c in installedList) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
			List<LibraryInstalled> all = LibraryPack.Installed();
			if (all.Count == 0) UIKit.Size(UIKit.Label(installedList, "Nothing installed from packs.", 12, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic).gameObject, -1, 26);
			foreach (LibraryInstalled e in all.OrderBy(x => x.title))
			{
				string id = e.id;
				RectTransform row = UIKit.Row(installedList, 28f, 6f, "Installed_" + e.id);
				int islands = e.files.Count(f => f.kind == LibraryPack.KindIsland);
				Text t = UIKit.Label(row, e.title + "  -  by " + e.author + ", version " + e.version + ", " + islands + " island(s)" + (e.plan.Length > 0 ? " + the plan '" + e.plan + "'" : "") +
					"  (" + (e.source == LibraryPack.SourceLibrary ? "from the library" : "imported") + " " + e.date + ")", 12, UIKit.TextColor);
				t.horizontalOverflow = HorizontalWrapMode.Overflow;
				Button remove = UIKit.Button(row, pendingRemove == id ? "Sure? Remove" : "Remove", () => AskRemove(id), "Remove what this entry installed (what a saved world still uses stays)", 120, 26f, 12);
				if (pendingRemove == id) UIKit.DangerButton(remove);
			}
			// (the look through every world's saves takes a moment with many worlds: only when Tidy up is clicked - done here
			// each time the tab showed, it made the tab slow)
			hostCopies.text = "Tidy up: island copies, generated islands and the files of deleted worlds nothing uses any more.";
			tidyPending = null;
			if (tidyButton != null) { UIKit.LabelOf(tidyButton).text = "Tidy up"; UIKit.SetActive(tidyButton, false); }
		}

		static Button tidyButton;
		static Housekeeping.Scan tidyPending;

		/// <summary>Tidy up: the first click looks and says what it would clear ("Sure? Tidy up"), the second clears it (public for tests).</summary>
		public static void TidyUp()
		{
			if (tidyPending == null)
			{
				Housekeeping.Scan scan = Housekeeping.Look();
				hostCopies.text = scan.Describe();
				if (scan.Total == 0) { SetStatus("Nothing to tidy up.", false); return; }
				tidyPending = scan;
				UIKit.LabelOf(tidyButton).text = "Sure? Tidy up";
				UIKit.DangerButton(tidyButton);
				SetStatus(scan.Describe() + " Click again to clear it.", false);
				return;
			}
			// (looked at again: a world saved or an island used since the first click keeps its files)
			Housekeeping.Scan found = Housekeeping.Look().Within(tidyPending);
			tidyPending = null;
			SetStatus(Housekeeping.TidyUp(found) + " (Island copies and generated islands can be got back from the deleted folder, world files from worlds\\" + Housekeeping.RemovedWorldsFolder + ".)", false);
			ShowInstalled();
		}

		/// <summary>Remove asks first: the first click turns the button into "Sure? Remove", the second removes (public for tests).</summary>
		public static void AskRemove(string id)
		{
			if (pendingRemove != id) { pendingRemove = id; ShowInstalled(); return; }
			pendingRemove = null;
			SetStatus(LibraryPack.Remove(id).ToString(), false);
			ShowPack();
			ShowInstalled();
		}

		static void OpenImportFolder()
		{
			try { Directory.CreateDirectory(LibraryPack.ImportFolder); HelpLinks.OpenFolder(LibraryPack.ImportFolder); }
			catch (Exception e) { SetStatus("Could not open the folder: " + e.Message, true); }
		}

		static void SetStatus(string text, bool error) { status.text = text; status.color = error ? UIKit.Danger : UIKit.TextColor; }
	}

	/// <summary>
	/// A window without an Update of its own (Share, the island library): its text fields count as typing - the editor's
	/// camera flew, Delete hid the selection and 1-4 changed the gizmo while a title was typed - and Esc closes it (when given).
	/// </summary>
	class WindowKeys : MonoBehaviour
	{
		public Func<bool> Typing;
		public Action Close;
		bool typing;

		void Update()
		{
			bool now = Typing != null && Typing();
			if (now) EditorInput.IsTyping = true;
			else if (typing) EditorInput.IsTyping = false;
			typing = now;
			if (Close != null && !DropList.Busy && !InfoWindow.IsOpen && !EditorInput.SubWindowJustClosed && EscKey.Down(this)) Close();
		}

		void OnDisable() { if (typing) EditorInput.IsTyping = false; typing = false; }
	}
}
