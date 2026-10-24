using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Tools for the editor's look (Raft's own menus as the model).</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIUIDump", docs: "Dev: writes Raft's UI (canvases; images with their sprites, texts with their fonts) to Mods\\DynamicIslands\\ui_dump_<scene>.txt and the sprites to ui_sprites\\. CIUIDump [part of a path]")]
		public static void UIDump(string[] args)
		{
			string filter = args != null && args.Length > 0 ? string.Join(" ", args) : "";
			string dir = Path.Combine(DynamicIslands.assetpath, "ui_sprites");
			Directory.CreateDirectory(dir);
			var sb = new StringBuilder();
			var saved = new HashSet<string>();
			var fonts = new Dictionary<string, int>();
			foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
			{
				if (c == null || !c.gameObject.scene.IsValid() || !c.isRootCanvas || c.name.StartsWith("CustomIslands")) continue;
				sb.AppendLine("=== CANVAS " + c.name + " (scene " + c.gameObject.scene.name + ", active " + c.gameObject.activeInHierarchy + ", order " + c.sortingOrder + ")");
				CanvasScaler cs = c.GetComponent<CanvasScaler>();
				if (cs != null) sb.AppendLine("    scaler " + cs.uiScaleMode + " ref " + cs.referenceResolution + " match " + cs.matchWidthOrHeight);
				foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
				{
					string path = PathOf(t, c.transform);
					if (filter.Length > 0 && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
					var parts = new List<string>();
					RectTransform rt = t as RectTransform;
					if (rt != null) parts.Add("size " + rt.rect.width.ToString("0") + "x" + rt.rect.height.ToString("0"));
					foreach (Component comp in t.GetComponents<Component>())
					{
						if (comp == null || comp is Transform || comp is CanvasRenderer) continue;
						Image img = comp as Image;
						Text txt = comp as Text;
						Selectable sel = comp as Selectable;
						Shadow sh = comp as Shadow;
						if (img != null)
						{
							Sprite s = img.sprite;
							parts.Add("Image[" + (s != null ? s.name + " tex " + (s.texture != null ? s.texture.name : "?") + " " + s.rect.width + "x" + s.rect.height + " border " + s.border + " ppu " + s.pixelsPerUnit : "no sprite") +
								" " + img.type + " col " + Hex(img.color) + (img.material != null && img.material.name != "Default UI Material" ? " mat " + img.material.name : "") + " ppuMul " + img.pixelsPerUnitMultiplier + "]");
							if (s != null && saved.Add(s.name)) SaveSprite(s, dir);
						}
						else if (txt != null)
						{
							string fn = txt.font != null ? txt.font.name : "?";
							fonts[fn] = (fonts.ContainsKey(fn) ? fonts[fn] : 0) + 1;
							parts.Add("Text[" + fn + " " + txt.fontSize + " " + txt.fontStyle + " " + Hex(txt.color) + " " + txt.alignment + " spacing " + txt.lineSpacing + " best " + txt.resizeTextForBestFit + " '" + Short(txt.text) + "']");
						}
						else if (sel != null)
						{
							ColorBlock cb = sel.colors;
							parts.Add(comp.GetType().Name + "[" + sel.transition + " n " + Hex(cb.normalColor) + " h " + Hex(cb.highlightedColor) + " p " + Hex(cb.pressedColor) + " d " + Hex(cb.disabledColor) +
								(sel.spriteState.highlightedSprite != null ? " hSprite " + sel.spriteState.highlightedSprite.name : "") + (sel.spriteState.pressedSprite != null ? " pSprite " + sel.spriteState.pressedSprite.name : "") + "]");
							foreach (Sprite s in new[] { sel.spriteState.highlightedSprite, sel.spriteState.pressedSprite, sel.spriteState.selectedSprite, sel.spriteState.disabledSprite })
								if (s != null && saved.Add(s.name)) SaveSprite(s, dir);
						}
						else if (sh != null) parts.Add(comp.GetType().Name + "[" + Hex(sh.effectColor) + " " + sh.effectDistance + "]");
						else parts.Add(comp.GetType().Name);
					}
					sb.AppendLine((t.gameObject.activeInHierarchy ? "  " : "- ") + path + "  " + string.Join("  ", parts.ToArray()));
				}
			}
			sb.AppendLine("=== FONTS used: " + string.Join(", ", fonts.Select(f => f.Key + " \u00D7" + f.Value).ToArray()));
			sb.AppendLine("=== FONTS loaded: " + string.Join(", ", Resources.FindObjectsOfTypeAll<Font>().Select(f => f.name).Distinct().ToArray()));
			string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
			string file = Path.Combine(DynamicIslands.assetpath, "ui_dump_" + scene + ".txt");
			File.WriteAllText(file, sb.ToString());
			Log("UI dump: " + file + " (" + saved.Count + " sprites saved to " + dir + ")");
		}

		[ConsoleCommand(name: "CIClock", docs: "Dev, in game: the clocks Raft shares between players (water time) next to Time.time")]
		public static void Clock()
		{
			Network_Water w = ComponentManager<Network_Water>.Value;
			Log("Clock: water " + (w != null ? w.WaterTime.ToString("0.00") : "none") + ", Time.time " + Time.time.ToString("0.00") + ", shared " + SharedClock.Now.ToString("0.00") + (SharedClock.Synced ? "" : " (not synced)") + ", day " + WorldManager.DayCounter);
		}

		[ConsoleCommand(name: "CIJoinHost", docs: "Dev, main menu (second player): joins a Steam friend's Raft game, as Steam's \"Join Game\" does: CIJoinHost [part of the friend's name, or their SteamID64] (default: the first friend hosting)")]
		public static void JoinHost(string[] args)
		{
			StartTest(JoinFriendRoutine(args != null && args.Length > 0 ? string.Join(" ", args) : null));
		}

		/// <summary>
		/// Raft's own Join World list is empty in this version (JoinGameBox.RefreshGames does nothing): friends join
		/// through Steam, which hands Raft the host's rich presence "connect" string (Raft_Network.
		/// GameRichPressenceJoinRequested). This reads that string from the friends playing Raft and does the same.
		/// </summary>
		static System.Collections.IEnumerator JoinFriendRoutine(string name)
		{
			Raft_Network net = ComponentManager<Raft_Network>.Value;
			if (net == null) { Fail("no Raft_Network (go to the main menu first)"); yield break; }
			ulong direct;
			if (name != null && ulong.TryParse(name, out direct))
			{
				Log("Joining: SteamID " + direct);
				net.ParseConnectString(direct.ToString());
				yield break;
			}
			AppId_t raft = SteamUtils.GetAppID();
			var hosts = new List<KeyValuePair<string, string>>();
			float timeout = Time.realtimeSinceStartup + 20f;
			while (Time.realtimeSinceStartup < timeout)
			{
				hosts.Clear();
				int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
				var playing = new List<string>();
				for (int i = 0; i < n; i++)
				{
					CSteamID id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
					FriendGameInfo_t game;
					if (!SteamFriends.GetFriendGamePlayed(id, out game) || game.m_gameID.AppID() != raft) continue;
					SteamFriends.RequestFriendRichPresence(id);
					string who = SteamFriends.GetFriendPersonaName(id);
					string connect = SteamFriends.GetFriendRichPresence(id, Raft_Network.PresenceKeyConnect);
					playing.Add(who + (string.IsNullOrEmpty(connect) ? " (not hosting)" : ""));
					if (!string.IsNullOrEmpty(connect)) hosts.Add(new KeyValuePair<string, string>(who, connect));
				}
				if (hosts.Count > 0 || Time.realtimeSinceStartup + 2f >= timeout)
				{
					Log("Steam friends: " + n + ", playing Raft: " + (playing.Count == 0 ? "none" : string.Join(", ", playing.ToArray())));
					if (hosts.Count == 0)
						for (int i = 0; i < n; i++)
						{
							CSteamID id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
							FriendGameInfo_t game;
							bool inGame = SteamFriends.GetFriendGamePlayed(id, out game);
							Log("  friend " + id + " '" + SteamFriends.GetFriendPersonaName(id) + "': " + SteamFriends.GetFriendPersonaState(id) +
								(inGame ? ", playing app " + game.m_gameID.AppID() : ", not in a game (as this account sees it)") + " (Raft is app " + raft + ")");
						}
					break;
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			var pick = hosts.FirstOrDefault(h => name == null || h.Key.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
			if (pick.Value == null) { Fail("no friend hosting Raft" + (name != null ? " called '" + name + "'" : "") + " (are the accounts Steam friends, and is the host in a world?)"); yield break; }
			string[] parts = pick.Value.Split(new[] { Raft_Network.CONNECT_DELIMITER }, StringSplitOptions.None);
			Log("Joining: " + pick.Key);
			net.ParseConnectString(parts.Length > 1 ? parts[1].TrimStart() : pick.Value.Trim());
		}

		[ConsoleCommand(name: "CIJoinHostMenu", docs: "Dev, main menu: joins through Raft's own Join World box (empty in this Raft version: kept to show that)")]
		public static void JoinHostMenu(string[] args)
		{
			StartTest(JoinHostRoutine(args != null && args.Length > 0 ? string.Join(" ", args) : null));
		}

		static System.Collections.IEnumerator JoinHostRoutine(string name)
		{
			JoinGameBox box = Resources.FindObjectsOfTypeAll<JoinGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null) { Fail("no Join World box (go to the main menu first)"); yield break; }
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
			// Raft asks Steam for the friends' games: wait until the list stops growing
			float timeout = Time.realtimeSinceStartup + 30f;
			int count = -1;
			while (Time.realtimeSinceStartup < timeout)
			{
				yield return new WaitForSecondsRealtime(2f);
				int now = box.joinGameSelections != null ? box.joinGameSelections.Count : 0;
				if (now > 0 && now == count) break;
				count = now;
			}
			List<JoinGame_Selection> games = box.joinGameSelections ?? new List<JoinGame_Selection>();
			Func<JoinGame_Selection, string> label = s => s.text_GameName != null ? s.text_GameName.text : s.steamID.ToString();
			Log("Games of friends: " + (games.Count == 0 ? "none (is the host a Steam friend of this account, and hosting with 'Friends can join'?)" : string.Join(", ", games.Select(label).ToArray())));
			JoinGame_Selection pick = name == null ? games.FirstOrDefault() : games.FirstOrDefault(s => label(s).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
			if (pick == null) { Fail("no game to join" + (name != null ? " called '" + name + "'" : "")); yield break; }
			Log("Joining: " + label(pick));
			box.Button_SelectGame(pick);
			yield return null;
			box.Button_Join();
		}

		[ConsoleCommand(name: "CICreatureSpots", docs: "Dev, in game (host): every creature spot of the loaded custom islands - shown, recorded alive, animals, saved state")]
		public static void CreatureSpots()
		{
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(x => x.Root != null))
				foreach (CreatureSpawnPoint p in e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true))
				{
					ObjectState s;
					bool saved = e.State.TryGetValue(p.StateKey, out s);
					Log("Spot " + e.HostName + "/" + p.name + ": active " + p.gameObject.activeSelf + ", recorded " + p.RecordedAlive + ", animals " + p.Spawned.Count(a => a != null) +
						", state " + (saved ? "alive " + s.Yield + " day " + s.Day : "none"));
				}
		}

		[ConsoleCommand(name: "CIClearInventory", docs: "Dev, in a test world (its name starts with 'CI '): empties the local player's inventory, which fills up over many test runs (items then drop instead)")]
		public static void ClearInventory()
		{
			string world = SaveAndLoad.CurrentGameFileName ?? "";
			// (a client doesn't know the host's world name: the second player of the two-player test, in Sandboxie, may)
			if (!world.StartsWith("CI ") && !(Sandboxed && !Raft_Network.IsHost)) { Fail("only in a test world (named 'CI ...'), not in '" + world + "'"); return; }
			PlayerInventory inv = RAPI.GetLocalPlayer() != null ? RAPI.GetLocalPlayer().Inventory : null;
			if (inv == null) { Fail("no player"); return; }
			int kinds = 0;
			foreach (Item_Base item in ItemManager.GetAllItems())
			{
				if (item == null || string.IsNullOrEmpty(item.UniqueName)) continue;
				int n = inv.GetItemCount(item.UniqueName);
				if (n <= 0) continue;
				try { inv.RemoveItem(item.UniqueName, n); kinds++; } catch { }
			}
			Log("Inventory emptied in '" + world + "' (" + kinds + " kinds of items)");
		}

		static string PathOf(Transform t, Transform top)
		{
			string p = t.name;
			for (Transform x = t.parent; x != null && x != top.parent; x = x.parent) p = x.name + "/" + p;
			return p;
		}

		static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGBA(c); }

		static string Short(string s)
		{
			s = (s ?? "").Replace("\n", " ");
			return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
		}

		/// <summary>Saves a sprite's part of its (maybe unreadable) texture as a PNG, through a render texture.</summary>
		static void SaveSprite(Sprite s, string dir)
		{
			try
			{
				Texture2D src = s.texture;
				if (src == null) return;
				RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
				RenderTexture prev = RenderTexture.active;
				Graphics.Blit(src, rt);
				RenderTexture.active = rt;
				Rect r = s.textureRect;
				var tex = new Texture2D(Mathf.Max(1, (int)r.width), Mathf.Max(1, (int)r.height), TextureFormat.RGBA32, false);
				tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
				tex.Apply();
				RenderTexture.active = prev;
				RenderTexture.ReleaseTemporary(rt);
				string name = new string(s.name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray());
				File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
				UnityEngine.Object.Destroy(tex);
			}
			catch (Exception e) { Debug.LogWarning("[CITEST] sprite " + s.name + ": " + e.Message); }
		}

		[ConsoleCommand(name: "CIEditorOpenCheck", docs: "Dev, main menu: opens the editor and watches every frame until it is ready - the loading box covers the screen from the click, the bundle's old screen of 2023 is never drawn, the box goes when the editor is ready (shot_editor_loading.png)")]
		public static void EditorOpenCheck()
		{
			StartTest(EditorOpenCheckRoutine());
		}

		static IEnumerator EditorOpenCheckRoutine()
		{
			if (LoadSceneManager.IsGameSceneLoaded || DynamicIslands.InEditor()) { Fail("run at the main menu"); yield break; }
			bool ok = true;
			int frames = 0, covered = 0, uncovered = 0, oldDrawn = 0;
			string firstOld = null;
			bool shot = false;
			// (the bundle's own canvases: those the editor scene brings, taken the first frame it is there - before the
			// editor's own screen is built in it)
			List<Canvas> old = null;
			float start = Time.realtimeSinceStartup;
			DynamicIslands.LoadEditor(new string[0]);
			Check(ref ok, EditorLoadingBox.Showing, "the loading box is up the moment EDITOR is clicked");
			// (until the editor is ready: every frame either the box covers the screen or the editor is ready; the bundle's
			// own canvases are never drawn)
			while (Time.realtimeSinceStartup - start < 120f)
			{
				yield return null;
				frames++;
				bool ready = DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt && EditorUI.Canvas != null && !EditorLoadingBox.Showing;
				if (EditorLoadingBox.Showing) covered++;
				else if (!ready) uncovered++;
				UnityEngine.SceneManagement.Scene editor = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Editor");
				if (old == null && editor.IsValid() && editor.isLoaded)
					old = editor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => EditorUI.Canvas == null || !c.transform.IsChildOf(EditorUI.Canvas.transform)).ToList();
				if (old != null)
					foreach (Canvas c in old)
						if (c != null && c.enabled && c.gameObject.activeInHierarchy)
						{ oldDrawn++; if (firstOld == null) firstOld = c.name + " (frame " + frames + ")"; }
				// (once the editor scene is there behind it: the box over the scene that used to show the old screen)
				if (!shot && EditorLoadingBox.Showing && old != null && Time.realtimeSinceStartup - start > 0.3f)
				{
					ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "shot_editor_loading.png")));
					shot = true;
				}
				if (ready) break;
			}
			bool done = DynamicIslands.InEditor() && PlaceableCatalog.IsBuilt && !EditorLoadingBox.Showing;
			Check(ref ok, done, "the editor is ready and the box has gone after " + (Time.realtimeSinceStartup - start).ToString("F1") + " s");
			Check(ref ok, covered > 0 && uncovered == 0, "the screen was covered by the loading box for " + covered + " of " + frames + " frames, uncovered and not ready for " + uncovered);
			Check(ref ok, old != null && old.Count > 0 && oldDrawn == 0, "the bundle's old screen (" + (old != null ? old.Count : 0) + " canvases) was never drawn" +
				(oldDrawn > 0 ? " - DRAWN in " + oldDrawn + " frame(s), first: " + firstOld : ""));
			Check(ref ok, shot, "a picture of the loading box over the editor scene (shot_editor_loading.png)");
			if (ok) Log("PASS: editor open check"); else Fail("editor open check");
		}
	}
}
