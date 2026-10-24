using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>More tests for the level up system and the randomizer's Big Bruce: the level bar at several screen sizes (IL18), the editor's
	/// Island tab without a level up switch (IL26), the world keeping levels when the island's rule changes (IL28), and Big Bruce's look (IR2).</summary>
	public static partial class DevTests
	{
		#region IL18 - the level bar at several screen sizes

		static readonly Vector2Int[] LevelBarSizes =
		{
			new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440),
		};

		/// <summary>A rect in screen pixels (y up), whatever the canvas it is on.</summary>
		static Rect BarScreenRect(RectTransform r)
		{
			var c = new Vector3[4];
			r.GetWorldCorners(c);
			Canvas cv = r.GetComponentInParent<Canvas>();
			Canvas root = cv != null ? cv.rootCanvas : null;
			Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
			Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
			return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
		}

		static CanvasHelper BarHelper()
		{
			CanvasHelper helper = null;
			try { helper = ComponentManager<CanvasHelper>.Value; } catch { }
			if (helper == null) helper = UnityEngine.Object.FindObjectOfType<CanvasHelper>();
			return helper;
		}

		static string BarRectText(Rect r) { return "(" + r.xMin.ToString("F0") + "," + r.yMin.ToString("F0") + " to " + r.xMax.ToString("F0") + "," + r.yMax.ToString("F0") + ")"; }

		/// <summary>What is wrong with the level bar's place now (empty = fine): in <paramref name="info"/> the numbers.</summary>
		static List<string> LevelBarProblems(int w, int h, out string info)
		{
			var bad = new List<string>();
			info = "";
			CanvasHelper helper = BarHelper();
			if (helper == null || helper.StatSliderParent == null) { bad.Add("no Raft HUD (CanvasHelper)"); return bad; }
			RectTransform parent = helper.StatSliderParent.transform as RectTransform;
			RectTransform bar = parent != null ? parent.Find("CustomIslands_LevelBar") as RectTransform : null;
			if (bar == null || !LevelHud.BarShown) { bad.Add("the level bar is not shown"); return bad; }
			var sliders = new[] { helper.healthSlider, helper.thirstSlider, helper.hungerSlider }.Where(s => s != null).Select(s => (RectTransform)s.transform).ToList();
			if (sliders.Count < 3) { bad.Add("only " + sliders.Count + " of Raft's stat bars found"); return bad; }
			Rect b = BarScreenRect(bar);
			Rect stats = BarScreenRect(sliders[0]);
			foreach (RectTransform s in sliders.Skip(1)) { Rect x = BarScreenRect(s); stats = Rect.MinMaxRect(Mathf.Min(stats.xMin, x.xMin), Mathf.Min(stats.yMin, x.yMin), Mathf.Max(stats.xMax, x.xMax), Mathf.Max(stats.yMax, x.yMax)); }
			float oneWidth = sliders.Max(s => BarScreenRect(s).width);
			info = "bar " + BarRectText(b) + " " + b.width.ToString("F0") + " wide, Raft's three bars " + BarRectText(stats) + " " + oneWidth.ToString("F0") + " wide (" + LevelHud.BarPlace + ")";
			// Inside the screen
			if (b.xMin < -1f || b.yMin < -1f || b.xMax > w + 1f || b.yMax > h + 1f) bad.Add("off the screen " + w + "x" + h);
			// Above or below Raft's bars, not over them
			bool above = b.yMin >= stats.yMax - 3f, below = b.yMax <= stats.yMin + 3f;
			if (!above && !below) bad.Add("over Raft's stat bars (not above or below them)");
			// As wide as them
			if (b.width < 8f) bad.Add("no width (" + b.width.ToString("F1") + ")");
			if (Mathf.Abs(b.width - oneWidth) > Mathf.Max(3f, oneWidth * 0.04f)) bad.Add("not as wide as Raft's bars (" + b.width.ToString("F0") + " against " + oneWidth.ToString("F0") + ")");
			if (b.center.x < stats.xMin - 3f || b.center.x > stats.xMax + 3f) bad.Add("not in line with Raft's bars (centre x " + b.center.x.ToString("F0") + ")");
			// Not over the hotbar
			GameObject hb = GameObject.Find("_CanvasGame_New/InventoryParent/Hotbar");
			RectTransform hbr = hb != null ? hb.transform as RectTransform : null;
			if (hbr == null) info += "; hotbar not found (not checked)";
			else
			{
				Rect hr = BarScreenRect(hbr);
				if (hr.height > h * 0.25f)
				{
					// (a container: the hotbar is what is drawn in it)
					bool any = false;
					Rect u = new Rect();
					foreach (Graphic g in hbr.GetComponentsInChildren<Graphic>(false))
					{
						if (g.color.a < 0.05f || g.rectTransform == hbr) continue;
						Rect gr = BarScreenRect(g.rectTransform);
						if (gr.height > h * 0.25f || gr.width > w * 0.5f) continue;
						u = any ? Rect.MinMaxRect(Mathf.Min(u.xMin, gr.xMin), Mathf.Min(u.yMin, gr.yMin), Mathf.Max(u.xMax, gr.xMax), Mathf.Max(u.yMax, gr.yMax)) : gr;
						any = true;
					}
					if (any) hr = u;
				}
				info += "; hotbar " + BarRectText(hr);
				Rect b2 = Rect.MinMaxRect(b.xMin + 1f, b.yMin + 1f, b.xMax - 1f, b.yMax - 1f), h2 = Rect.MinMaxRect(hr.xMin + 1f, hr.yMin + 1f, hr.xMax - 1f, hr.yMax - 1f);
				if (b2.Overlaps(h2)) bad.Add("over the hotbar " + BarRectText(hr));
			}
			return bad;
		}

		[ConsoleCommand(name: "CILevelBarSizes", docs: "Dev, world (host or not): the player-level bar (LevelHud) at 1280x720, 1920x1080, 2560x1440 and 3440x1440 (windowed) and with the HUD scaled 0.8x and 1.25x at 1920x1080 (IL18) - each time the bar is inside the screen, directly above (or below) Raft's three stat bars (not over them), as wide as them and in line with them, and not over the hotbar; with Raft's HUD (the stat bars' parent) hidden the bar is hidden too and comes back with it. Levels are switched on for the test and the record, the screen size and the HUD scale put back after; pictures shot_levelbar_<w>x<h>. Left out: Raft 1.1 has no UI scale setting that can be set in code (none found in its code), so the scale is the HUD canvas's CanvasScaler changed; if the canvas has none the scale step says so and is skipped. Hiding the HUD is done by switching off Raft's StatSliderParent (the bar's parent), not by a Raft key")]
		public static void LevelBarSizesCommand() { StartTest(LevelBarSizesRoutine()); }

		static IEnumerator LevelBarSizesRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || RAPI.GetLocalPlayer() == null) { Fail("level bar sizes: run in a world"); yield break; }
			bool ok = true;
			int w0 = Screen.width, h0 = Screen.height;
			FullScreenMode mode0 = Screen.fullScreenMode;
			bool wasOn = PlayerLevels.On;
			LevelRecord mine = wasOn && PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : new LevelRecord();
			if (!wasOn) PlayerLevels.TurnOn(false);
			PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(5) });
			CanvasScaler scaler = null;
			Action restoreScale = null;
			GameObject statParent = null;
			try
			{
				LevelHud.ShowBar();
				CanvasHelper helper = BarHelper();
				if (helper == null || helper.StatSliderParent == null) { Check(ref ok, false, "Raft's HUD (CanvasHelper.StatSliderParent) not found"); }
				else
				{
					statParent = helper.StatSliderParent.gameObject;
					Canvas cv = statParent.GetComponentInParent<Canvas>();
					scaler = cv != null && cv.rootCanvas != null ? cv.rootCanvas.GetComponent<CanvasScaler>() : null;
				}
				// A first wait: the bar is built and placed (placed again every 2 s)
				yield return new WaitForSecondsRealtime(2.6f);
				bool hidingDone = false;
				foreach (Vector2Int size in LevelBarSizes)
				{
					Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
					yield return new WaitForSecondsRealtime(1.2f);
					LevelHud.ShowBar();
					yield return new WaitForSecondsRealtime(2.6f);
					Canvas.ForceUpdateCanvases();
					int w = Screen.width, h = Screen.height;
					string sz = w + "x" + h + (w != size.x || h != size.y ? " (asked " + size.x + "x" + size.y + ")" : "");
					string info;
					List<string> bad = LevelBarProblems(w, h, out info);
					Check(ref ok, bad.Count == 0, sz + " the level bar: " + (bad.Count == 0 ? "fine - " : string.Join("; ", bad.ToArray()) + " - ") + info);
					Screenshot(new[] { "levelbar_" + w + "x" + h });
					yield return new WaitForSecondsRealtime(0.6f);
					// Hidden with Raft's HUD (once, at the first size)
					if (!hidingDone && statParent != null)
					{
						hidingDone = true;
						statParent.SetActive(false);
						yield return new WaitForSecondsRealtime(0.5f);
						Check(ref ok, !LevelHud.BarShown, "Raft's HUD hidden: the level bar is hidden with it");
						statParent.SetActive(true);
						LevelHud.ShowBar();
						yield return new WaitForSecondsRealtime(2.6f);
						Check(ref ok, LevelHud.BarShown, "Raft's HUD back: the level bar is back");
					}
				}
				// The HUD's scale (Raft 1.1 has no UI scale setting to set in code: its canvas scaler is changed)
				Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
				yield return new WaitForSecondsRealtime(1.2f);
				if (scaler == null) Log("  (no CanvasScaler on Raft's HUD canvas: the UI scale step is left out)");
				else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPhysicalSize) Log("  (Raft's HUD canvas scales by physical size: the UI scale step is left out)");
				else
				{
					float scale0 = scaler.scaleFactor;
					Vector2 reference0 = scaler.referenceResolution;
					bool constant = scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize;
					restoreScale = () => { scaler.scaleFactor = scale0; scaler.referenceResolution = reference0; };
					foreach (float f in new[] { 0.8f, 1.25f })
					{
						if (constant) scaler.scaleFactor = scale0 * f; else scaler.referenceResolution = reference0 / f;
						LevelHud.ShowBar();
						yield return new WaitForSecondsRealtime(2.6f);
						Canvas.ForceUpdateCanvases();
						string info;
						List<string> bad = LevelBarProblems(Screen.width, Screen.height, out info);
						Check(ref ok, bad.Count == 0, "HUD scale x" + f.ToString("0.00", CultureInfo.InvariantCulture) + " at " + Screen.width + "x" + Screen.height + ": " + (bad.Count == 0 ? "fine - " : string.Join("; ", bad.ToArray()) + " - ") + info);
						Screenshot(new[] { "levelbar_scale" + f.ToString("0.00", CultureInfo.InvariantCulture) });
						yield return new WaitForSecondsRealtime(0.6f);
					}
					restoreScale();
					restoreScale = null;
				}
			}
			finally
			{
				if (restoreScale != null) restoreScale();
				if (statParent != null) statParent.SetActive(true);
				Screen.SetResolution(w0, h0, mode0);
				PlayerLevels.SetMine(mine);
				if (!wasOn) PlayerLevels.TurnOff();
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Check(ref ok, Screen.width == w0 && Screen.height == h0, "the screen size put back: " + Screen.width + "x" + Screen.height);
			if (ok) Log("PASS: level bar sizes"); else Fail("level bar sizes");
		}

		#endregion

		#region IL26 - the Island tab has no Level up switch (a world option only since 10-16)

		[ConsoleCommand(name: "CILevelIslandTab", docs: "Dev, editor: the level up system is a world option only - the Island tab has no Level up system switch; an older island saved with rules.levels=on opens, keeps its other values, and the system stays off in the editor")]
		public static void LevelIslandTabCommand() { StartTest(LevelIslandTabRoutine()); }

		static IEnumerator LevelIslandTabRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			const string nameA = "cileveltab_a";
			TAB tab0 = EditorUI.CurrentTab;
			EditorUI.SetTab(TAB.Island);
			yield return null; yield return null;
			try
			{
				Check(ref ok, Traverse.Create(typeof(EditorUI)).Field("infoLevelsOn").GetValue() == null && !EditorUI.Canvas.GetComponentsInChildren<Transform>(true).Any(x => x.name == "LevelUp"), "the Island tab has no Level up switch");
				Screenshot(new[] { "level_island_tab" });
				yield return new WaitForSecondsRealtime(0.5f);
				DynamicIslands.NewIsland();
				yield return null; yield return null;
				DynamicIslands.currentIslandProps[IslandProps.Title] = "Older level island";
				Check(ref ok, DynamicIslands.SaveIsland(nameA), "saved '" + nameA + "'");
				IslandFile f = IslandFile.Load(IslandSpawner.PathFor(nameA));
				f.Props[IslandProps.Levels] = "on"; // (what an older editor wrote)
				f.Save(IslandSpawner.PathFor(nameA));
				DynamicIslands.NewIsland();
				yield return null; yield return null;
				Check(ref ok, DynamicIslands.LoadIsland(nameA), "Open '" + nameA + "' (an older island with rules.levels=on)");
				for (float w = 0f; w < 8f && DynamicIslands.currentIslandName != nameA; w += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
				yield return new WaitForSecondsRealtime(0.4f);
				Check(ref ok, DynamicIslands.currentIslandName == nameA && ObjectProps.Get(DynamicIslands.currentIslandProps, IslandProps.Title) == "Older level island", "it opens with its title");
				Check(ref ok, !PlayerLevels.On, "the level up system stays off");
			}
			finally
			{
				if (File.Exists(IslandSpawner.PathFor(nameA))) File.Delete(IslandSpawner.PathFor(nameA));
				if (DynamicIslands.InEditor())
				{
					DynamicIslands.NewIsland();
					EditorUI.SetTab(tab0);
				}
			}
			if (ok) Log("PASS: level island tab"); else Fail("level island tab");
		}

		#endregion

		#region IL28 - the world's levels don't depend on the level island's rule

		[ConsoleCommand(name: "CILevelIslandRuleWorld", docs: "Dev, world (host): levels are the world's, not the island's (IL28) - a level island comes into the world with the system switched on by the host (an older island's rule on, ignored); the host then changes the island's rule on its file (to off): PlayerLevels.On stays on and the player's record (level 3) is kept, the world's lines still hold @levels=on; the island leaves the world: still on, record kept; the island (its file now says off) comes again: still on; the rule back to on in the file and the island once more: still on, same record. The record and the system are put back after. Left out: the change is made on the island file (what the editor's switch writes - the editor side is CILevelIslandTab), not clicked in the editor; this test is in the world only")]
		public static void LevelIslandRuleWorldCommand() { StartTest(LevelIslandRuleWorldRoutine()); }

		static IEnumerator LevelIslandRuleWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			if (!raftPos.HasValue || !Raft_Network.IsHost || player == null) { Fail("level island rule world: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			var boars = new List<AI_NetworkBehaviour>();
			var entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 1, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			var entries = new List<IslandWorldState.Entry> { entry };
			try
			{
				Check(ref ok, PlayerLevels.On, "the level island (rule on) switched the system on in the world");
				PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(3) });
				int xp0 = PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp : -1;
				string path = IslandSpawner.PathFor(LevelIsland);
				IslandFile f = IslandFile.Load(path);
				f.Props[IslandProps.Levels] = "on"; // (an older island's rule: ignored since 10-16, the level up system is a world option)
				f.Save(path);

				// The host changes the rule on the island (what the editor's Off writes)
				f.Props.Remove(IslandProps.Levels);
				f.Save(path);
				Check(ref ok, !IslandFile.Load(path).Props.ContainsKey(IslandProps.Levels), "the island's file now says off");
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == xp0, "rule changed to off on the file: the world stays on, level " + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Level : 0) + " kept (" + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp : -1) + " of " + xp0 + " EXP)");
				Check(ref ok, PlayerLevels.WriteLines().Contains("@levels=on"), "the world's lines still hold @levels=on");

				// The island leaves the world
				IslandWorldState.RemoveIds(new[] { entry.Id }, true);
				entries.Clear();
				yield return new WaitForSeconds(1f);
				Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == xp0, "the island gone from the world: still on, record kept");
				OnRaftCommand();
				yield return new WaitForSeconds(0.5f);

				// It comes again (its file says off now)
				Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(LevelIsland), 390f);
				if (!spot.HasValue) { Check(ref ok, false, "no open sea near the raft for the island again"); }
				else
				{
					int before = IslandWorldState.Islands.Count;
					yield return DynamicIslands.instance.SpawnIslandFile(LevelIsland, spot.Value, true);
					IslandWorldState.Entry again = IslandWorldState.Islands.Skip(before).FirstOrDefault(e => e.HostName == LevelIsland);
					if (again != null) entries.Add(again);
					Check(ref ok, again != null, "the island (rule off in its file) is in the world again");
					yield return new WaitForSeconds(1f);
					Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == xp0, "an island with the rule off in a world that has levels: still on, record kept");
					if (again != null) { IslandWorldState.RemoveIds(new[] { again.Id }, true); entries.Clear(); }
					yield return new WaitForSeconds(0.5f);
				}

				// The rule back on, the island once more
				IslandFile g = IslandFile.Load(path);
				g.Props[IslandProps.Levels] = "on";
				g.Save(path);
				Vector3? spot2 = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(LevelIsland), 390f);
				if (!spot2.HasValue) { Check(ref ok, false, "no open sea near the raft for the island a third time"); }
				else
				{
					int before2 = IslandWorldState.Islands.Count;
					yield return DynamicIslands.instance.SpawnIslandFile(LevelIsland, spot2.Value, true);
					IslandWorldState.Entry third = IslandWorldState.Islands.Skip(before2).FirstOrDefault(e => e.HostName == LevelIsland);
					if (third != null) entries.Add(third);
					yield return new WaitForSeconds(1f);
					Check(ref ok, third != null && PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == xp0, "the rule on again and the island once more: on, the same record (" + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp : -1) + " EXP)");
				}

				// The world's file
				IslandWorldState.Save();
				string worldFile = Directory.GetFiles(Path.Combine(DynamicIslands.assetpath, "worlds"), SaveAndLoad.WorldGuid + ".txt").FirstOrDefault();
				string[] saved = worldFile != null ? File.ReadAllLines(worldFile) : new string[0];
				Check(ref ok, saved.Contains("@levels=on"), "the world's island file holds @levels=on");
			}
			finally
			{
				PlayerLevels.SetMine(new LevelRecord());
			}
			OnRaftCommand();
			LevelCleanup(entries.FirstOrDefault());
			foreach (IslandWorldState.Entry e in entries.Skip(1)) IslandWorldState.RemoveIds(new[] { e.Id }, true);
			KeepAlive(player);
			if (ok) Log("PASS: level island rule world"); else Fail("level island rule world");
		}

		#endregion

		#region IR2 - Big Bruce's look

		const string BruceLookFile = "cibrucelook.txt";

		/// <summary>The renderer of Big Bruce's body with the shader property that carries his texture, and the copy in its block.</summary>
		static string BruceTexProp(Renderer r)
		{
			var block = new MaterialPropertyBlock();
			r.GetPropertyBlock(block);
			foreach (string p in new[] { "_Diffuse", "_MainTex" }) if (block.GetTexture(p) != null) return p;
			return null;
		}

		static float TexLuma(Texture t)
		{
			RenderTexture tmp = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32);
			RenderTexture prev = RenderTexture.active;
			Texture2D read = new Texture2D(8, 8, TextureFormat.RGBA32, false);
			try
			{
				Graphics.Blit(t, tmp);
				RenderTexture.active = tmp;
				read.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
				read.Apply();
				Color[] px = read.GetPixels();
				float sum = 0f;
				foreach (Color c in px) sum += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
				return sum / px.Length;
			}
			finally
			{
				RenderTexture.active = prev;
				RenderTexture.ReleaseTemporary(tmp);
				UnityEngine.Object.Destroy(read);
			}
		}

		/// <summary>All the material's number properties as the renderer shows them (its block first, else the material).</summary>
		static Dictionary<string, string> BruceNumbers(Renderer r)
		{
			var d = new Dictionary<string, string>();
			Material m = r.sharedMaterial;
			if (m == null) return d;
			var block = new MaterialPropertyBlock();
			r.GetPropertyBlock(block);
			Shader sh = m.shader;
			for (int i = 0; i < sh.GetPropertyCount(); i++)
			{
				string name = sh.GetPropertyName(i);
				ShaderPropertyType type = sh.GetPropertyType(i);
				if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range)
					d[name] = (block.HasProperty(name) ? block.GetFloat(name) : m.GetFloat(name)).ToString("F4", CultureInfo.InvariantCulture);
				else if (type == ShaderPropertyType.Vector || type == ShaderPropertyType.Color)
					d[name] = (block.HasProperty(name) ? block.GetVector(name) : m.GetVector(name)).ToString("F3");
			}
			return d;
		}

		static bool WoundName(string n)
		{
			string l = n.ToLowerInvariant();
			return l.Contains("damage") || l.Contains("wound") || l.Contains("hurt") || l.Contains("blood") || l.Contains("injur") || l.Contains("health");
		}

		/// <summary>Finds a live non-rogue shark and makes the world's randomizer roll it Big Bruce (a seed that does, Wild with alphas): sharkOut[0], the settings before in oldOut[0].</summary>
		static IEnumerator BruceMake(AI_NetworkBehavior_Shark[] sharkOut, RandomizerSettings[] oldOut)
		{
			AI_NetworkBehavior_Shark shark = null;
			Network_Player player = RAPI.GetLocalPlayer();
			for (int i = 0; i < 60 && shark == null; i++)
			{
				shark = LiveSharks().Where(s => !RogueShark.IsRogue(s) && s.networkEntity != null && s.networkEntity.stat_health != null).OrderBy(s => (s.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
				if (shark == null) { KeepAlive(player); yield return new WaitForSeconds(1f); }
			}
			// (none: an earlier suite killed him - CILevelBruce, CILevelKillsAll - and Raft sends the next one only within about 5 minutes
			// (CISharkReroll): Raft's own CreateAINetworkBehaviour makes one by the raft, as CIBossesNightmare does its bosses. Run i
			// 2026-10-20 failed here 6 minutes after those kills)
			if (shark == null)
			{
				Network_Host_Entities host = null;
				try { host = ComponentManager<Network_Host_Entities>.Value; } catch { }
				Vector3? raft = CustomIslandSpawner.RaftPosition;
				if (host != null && raft.HasValue) shark = host.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, raft.Value + new Vector3(45f, -10f, 0f), null) as AI_NetworkBehavior_Shark;
				Log("  (no live shark within a minute - killed by an earlier suite: " + (shark != null ? "Raft made shark #" + shark.ObjectIndex + " by the raft)" : "Raft made none)"));
				if (shark != null) yield return new WaitForSeconds(2f);
			}
			if (shark == null || shark.networkEntity == null || shark.networkEntity.stat_health == null) { Fail("bruce look: no live shark (Bruce) within a minute, and Raft made none"); yield break; }
			RandomizerSettings old = WorldRandomizer.Current.Copy();
			oldOut[0] = old;
			uint idx = shark.ObjectIndex;
			int found = 0;
			for (int seed = 1; seed < 30000 && found == 0; seed++)
			{
				WorldRandomizer.Current = new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = seed };
				WorldRandomizer.Variant v = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Shark, idx);
				if (v != null && v.Alpha) found = seed;
			}
			WorldRandomizer.Current = old;
			if (found == 0) { Fail("bruce look: no seed makes shark #" + idx + " a Big Bruce"); yield break; }
			WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = found });
			for (float t = 0f; t < 20f; t += 0.5f)
			{
				string name;
				if (WorldRandomizer.VariantOfIndex.TryGetValue(idx, out name) && name == "big bruce") break;
				yield return new WaitForSeconds(0.5f);
			}
			sharkOut[0] = shark;
		}

		[ConsoleCommand(name: "CIBruceLook", docs: "Dev, world (host): Big Bruce's look (IR2). CIBruceLook = a live shark is made Big Bruce (a randomizer seed, Wild with alphas, that rolls his index an alpha; the randomizer is put back after): his texture (_Diffuse) is a render texture copy of his own (CI_Tinted_*, in his property block) while Raft's material keeps its own texture, the copy is darker than Raft's (his colour), his hurt texture (_DiffuseDamaged) is a copy too; hit four times for 15% of his health the wound shader still works (a wound-named property of his material changes - else any number does, and the log says which) and his colour is still the same copy after. CIBruceLook save = the same, then it keeps the randomizer on (it is saved with the world) and writes his index, variant, seed and brightness to Mods\\DynamicIslands\\cibrucelook.txt; then CISave, CIMainMenu, CILoadWorld (the same world), and CIBruceLook check = after the load the randomizer has the same seed and a shark with that index is Big Bruce again with the same colour (waits up to 3 minutes for that shark; Raft decides when he comes - if only other sharks come it checks one of them that rolls Big Bruce, else fails and says so). Left out: the wound LOOK is not seen (screenshot shot_bruce_hurt, look at it), only that the material's numbers change; whether Raft gives a shark the same index after a load is what the check shows. The plain CIBruceLook and save end 'PASS: bruce look' / 'PASS: bruce look saved'; the pair's verdict is the check")]
		public static void BruceLookCommand(string[] args) { StartTest(BruceLookRoutine(args != null && args.Length > 0 ? args[0] : "")); }

		static IEnumerator BruceLookRoutine(string what)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || player == null || host == null) { Fail("bruce look: run in a world, as the host"); yield break; }
			if (what == "check") { yield return BruceLookCheck(player); yield break; }
			if (what.Length > 0 && what != "save") { Fail("CIBruceLook [save|check]"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var sharkOut = new AI_NetworkBehavior_Shark[1];
			var oldOut = new RandomizerSettings[1];
			yield return BruceMake(sharkOut, oldOut);
			AI_NetworkBehavior_Shark shark = sharkOut[0];
			if (shark == null) { if (oldOut[0] != null) WorldRandomizer.Set(oldOut[0]); yield break; }
			uint idx = shark.ObjectIndex;
			try
			{
				string variant;
				WorldRandomizer.VariantOfIndex.TryGetValue(idx, out variant);
				Check(ref ok, variant == "big bruce", "shark #" + idx + " is Big Bruce (" + (variant ?? "none") + ")");
				List<Renderer> body = WorldRandomizer.BodyOf(shark);
				Renderer r = body.FirstOrDefault(x => BruceTexProp(x) != null);
				Check(ref ok, r != null, "his body has a texture of his own in its property block (" + body.Count + " body renderer(s))");
				if (r == null)
				{
					Material mm = body.Count > 0 ? body[0].sharedMaterial : null;
					if (mm != null) Log("  (material " + mm.name + ", shader " + mm.shader.name + ")");
				}
				else
				{
					string prop = BruceTexProp(r);
					var block = new MaterialPropertyBlock();
					r.GetPropertyBlock(block);
					Texture own = block.GetTexture(prop), raft = r.sharedMaterial.GetTexture(prop);
					Check(ref ok, own is RenderTexture && own.name.StartsWith("CI_Tinted_"), "his " + prop + " is a render texture copy of his own (" + own.name + ", " + own.GetType().Name + ")");
					Check(ref ok, raft != null && !(raft is RenderTexture) && raft != own, "Raft's material keeps Raft's texture (" + (raft != null ? raft.name + ", " + raft.GetType().Name : "none") + "): other sharks are not coloured");
					float lumaOwn = raft != null ? TexLuma(own) : 0f, lumaRaft = raft != null ? TexLuma(raft) : 1f;
					Check(ref ok, lumaRaft > 0.01f && lumaOwn < lumaRaft * 0.6f, "his colour is the dark Big Bruce's (brightness " + lumaOwn.ToString("F3") + " against Raft's " + lumaRaft.ToString("F3") + ")");
					Material m = r.sharedMaterial;
					if (m.HasProperty("_DiffuseDamaged"))
					{
						Texture hurt = block.GetTexture("_DiffuseDamaged");
						Check(ref ok, hurt is RenderTexture && hurt != m.GetTexture("_DiffuseDamaged"), "his hurt texture (_DiffuseDamaged) is a copy of his own too");
					}
					else Log("  (his material has no _DiffuseDamaged: " + m.shader.name + ")");

					// The wound shader
					Dictionary<string, string> before = BruceNumbers(r);
					Stat_Health hp = shark.networkEntity.stat_health;
					float hp0 = hp.Value;
					for (int i = 0; i < 4 && !shark.networkEntity.IsDead; i++)
					{
						host.DamageEntity(shark.networkEntity, shark.transform, hp.Max * 0.15f, shark.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
						yield return new WaitForSeconds(0.4f);
					}
					yield return new WaitForSeconds(1.5f);
					Check(ref ok, hp.Value < hp0, "he was hit: health " + hp0.ToString("F0") + " -> " + hp.Value.ToString("F0") + " of " + hp.Max.ToString("F0"));
					Screenshot(new[] { "bruce_hurt" });
					Dictionary<string, string> after = BruceNumbers(r);
					List<string> changed = after.Where(kv => before.ContainsKey(kv.Key) && before[kv.Key] != kv.Value).Select(kv => kv.Key + " " + before[kv.Key] + " -> " + kv.Value).ToList();
					List<string> woundChanged = changed.Where(c => WoundName(c.Split(' ')[0])).ToList();
					Check(ref ok, changed.Count > 0, "the wound shader works: " + (woundChanged.Count > 0 ? "wound properties change on damage: " + string.Join(", ", woundChanged.ToArray()) :
						changed.Count > 0 ? "no property named like a wound, but these changed: " + string.Join(", ", changed.Take(6).ToArray()) : "NO property of his material changed (" + before.Count + " number properties, " + before.Keys.Count(WoundName) + " named like a wound: " + string.Join(", ", before.Keys.Take(20).ToArray()) + ")"));
					var blockAfter = new MaterialPropertyBlock();
					r.GetPropertyBlock(blockAfter);
					Check(ref ok, blockAfter.GetTexture(prop) == own, "his colour is still the same copy after the wounds (" + (blockAfter.GetTexture(prop) != null ? blockAfter.GetTexture(prop).name : "none") + ")");

					if (what == "save" && ok)
					{
						string line = idx + "|" + variant + "|" + WorldRandomizer.Current.Seed + "|" + lumaOwn.ToString("F4", CultureInfo.InvariantCulture) + "|" + own.name;
						File.WriteAllText(Path.Combine(DynamicIslands.assetpath, BruceLookFile), line);
						Log("  (saved to " + BruceLookFile + ": " + line + "; the randomizer stays on for the world's save)");
					}
				}
			}
			finally
			{
				if (what != "save" || !ok) { if (oldOut[0] != null) WorldRandomizer.Set(oldOut[0]); }
			}
			KeepAlive(player);
			if (!ok) { Fail("bruce look"); yield break; }
			Log(what == "save" ? "PASS: bruce look saved - now CISave, CIMainMenu, CILoadWorld, then CIBruceLook check" : "PASS: bruce look");
		}

		static IEnumerator BruceLookCheck(Network_Player player)
		{
			string file = Path.Combine(DynamicIslands.assetpath, BruceLookFile);
			if (!File.Exists(file)) { Fail("bruce look check: no " + BruceLookFile + " (run CIBruceLook save first)"); yield break; }
			string[] parts = File.ReadAllText(file).Trim().Split('|');
			uint idx;
			int seed;
			float luma;
			if (parts.Length < 5 || !uint.TryParse(parts[0], out idx) || !int.TryParse(parts[2], out seed) || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out luma)) { Fail("bruce look check: " + BruceLookFile + " unreadable: " + string.Join("|", parts)); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			RandomizerSettings now = WorldRandomizer.Current;
			Check(ref ok, now.On && now.Seed == seed && now.Has(RandomizerSettings.Alphas), "after the load the randomizer is " + now.Describe() + ", seed " + now.Seed + " (saved " + seed + ")");
			AI_NetworkBehavior_Shark shark = null;
			bool sameIndex = false;
			for (float t = 0f; t < 180f && shark == null; t += 1f)
			{
				shark = LiveSharks().FirstOrDefault(s => s.ObjectIndex == idx);
				if (shark == null) { KeepAlive(player); yield return new WaitForSeconds(1f); }
			}
			sameIndex = shark != null;
			if (shark == null)
			{
				// (another shark: any that this seed rolls Big Bruce)
				shark = LiveSharks().FirstOrDefault(s => !RogueShark.IsRogue(s) && WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Shark, s.ObjectIndex) != null && WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Shark, s.ObjectIndex).Alpha);
				Log("  (no shark with index " + idx + " came within 3 minutes; " + (shark != null ? "checking shark #" + shark.ObjectIndex + ", which this seed rolls Big Bruce" : "no live shark rolls Big Bruce") + ")");
			}
			Check(ref ok, shark != null, "a shark that is Big Bruce is here" + (sameIndex ? " (the same index, " + idx + ")" : ""));
			if (shark != null)
			{
				string variant = null;
				for (float t = 0f; t < 20f; t += 0.5f)
				{
					if (WorldRandomizer.VariantOfIndex.TryGetValue(shark.ObjectIndex, out variant) && variant == "big bruce") break;
					yield return new WaitForSeconds(0.5f);
				}
				Check(ref ok, variant == "big bruce", "shark #" + shark.ObjectIndex + " is Big Bruce again (" + (variant ?? "none") + ")");
				Renderer r = WorldRandomizer.BodyOf(shark).FirstOrDefault(x => BruceTexProp(x) != null);
				Check(ref ok, r != null, "his body has a texture of his own again");
				if (r != null)
				{
					var block = new MaterialPropertyBlock();
					r.GetPropertyBlock(block);
					Texture own = block.GetTexture(BruceTexProp(r));
					float l = TexLuma(own);
					Check(ref ok, own is RenderTexture && Mathf.Abs(l - luma) < 0.05f, "his colour is the one before the save (brightness " + l.ToString("F3") + ", saved " + luma.ToString("F3") + ", " + own.name + ")");
					Screenshot(new[] { "bruce_loaded" });
				}
			}
			KeepAlive(player);
			if (ok) { File.Delete(file); Log("PASS: bruce look"); } else Fail("bruce look");
		}

		#endregion
		#region IW6: bosses at Nightmare

		[ConsoleCommand(name: "CIBossesNightmare", docs: "Dev, in game (host): Raft's bosses at Nightmare (IW6) - the Hyena boss, the Mama bear, the Tangaroa rat and Varuna are made with Raft's CreateAINetworkBehaviour (Varuna in the sea, the others over the level island) at Normal and at Nightmare and hit with a tenth of their health per hit (EntityType.Player) until they die: at Nightmare each takes about twice the hits and still dies, and the Hyena boss's own phase counter (damageTaken against damageThreshold: its acid pools) fires as often as at Normal. Varuna is invulnerable outside its scripted fight, so the test lifts that flag for its hits (logged). The difficulty and animals are put back / removed after")]
		public static void BossesNightmareCommand() { StartTest(BossesNightmareRoutine()); }

		static IEnumerator BossesNightmareRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host_Entities ents = ComponentManager<Network_Host_Entities>.Value;
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!raftPos.HasValue || !Raft_Network.IsHost || player == null || ents == null || host == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			int difficulty0 = MonsterDifficulty.Current;
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 1, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			Vector3 land = boars[0].transform.position;
			var bosses = new[] { AI_NetworkBehaviourType.HyenaBoss, AI_NetworkBehaviourType.MamaBear, AI_NetworkBehaviourType.Rat_Tangaroa, AI_NetworkBehaviourType.Boss_Varuna };
			var spawned = new List<AI_NetworkBehaviour>();
			try
			{
				int n = 0;
				foreach (AI_NetworkBehaviourType kind in bosses)
				{
					int[] hits = new int[2], phases = new int[2];
					float[] seen = new float[2], lost = new float[2], peak = new float[2], limit = new float[2];
					bool[] dead = new bool[2];
					string[] note = new string[2];
					for (int pass = 0; pass < 2; pass++)
					{
						MonsterDifficulty.Current = pass == 0 ? MonsterDifficulty.Normal : MonsterDifficulty.Nightmare;
						bool sea = kind == AI_NetworkBehaviourType.Boss_Varuna;
						Vector3 pos = sea ? raftPos.Value + new Vector3(50f, -5f, 30f * (pass == 0 ? 1f : -1f)) : land + new Vector3(2f + (n % 3) * 3f, 1.5f, 2f + (n / 3) * 3f);
						n++;
						AI_NetworkBehaviour ai = null;
						try { ai = ents.CreateAINetworkBehaviour(kind, pos, null); }
						catch (Exception e) { note[pass] = "not made: " + (e.InnerException ?? e).Message; continue; }
						if (ai == null) { note[pass] = "Raft made none"; continue; }
						spawned.Add(ai);
						yield return new WaitForSeconds(0.3f);
						Network_Entity ne = ai.networkEntity != null ? ai.networkEntity : ai.GetComponentInChildren<Network_Entity>(true);
						if (ne == null || ne.stat_health == null || ne.stat_health.Max <= 0f) { note[pass] = "no entity to hit"; continue; }
						if (ne.IsInvurnerable) { ne.IsInvurnerable = false; note[pass] = "invulnerable, lifted for the test"; }
						AI_StateMachine_HyenaBoss hyena = ai.GetComponentInChildren<AI_StateMachine_HyenaBoss>(true);
						if (hyena != null) limit[pass] = Traverse.Create(hyena).Field("damageThreshold").GetValue<float>();
						Traverse taken = hyena != null ? Traverse.Create(hyena).Field("damageTaken") : null;
						float hit = ne.stat_health.Max * 0.1f, last = taken != null ? taken.GetValue<float>() : 0f;
						while (!ne.IsDead && hits[pass] < 60)
						{
							if (ne.IsInvurnerable) ne.IsInvurnerable = false;
							host.DamageEntity(ne, ne.transform, hit, ne.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
							hits[pass]++;
							if (taken != null)
							{
								float now = taken.GetValue<float>();
								if (now < last) phases[pass]++;
								else if (hits[pass] == 1) { seen[pass] = now - last; lost[pass] = ne.stat_health.Max - ne.stat_health.Value; }
								peak[pass] = Mathf.Max(peak[pass], now); last = now;
							}
							KeepAlive(player);
							yield return new WaitForSeconds(0.05f);
						}
						dead[pass] = ne.IsDead;
						if (note[pass] == null) note[pass] = "health " + ne.stat_health.Max.ToString("F0");
						UnityEngine.Object.Destroy(ai.gameObject);
						yield return null;
					}
					if (note[0] != null && note[0].StartsWith("not made") || note[0] == "Raft made none") { Log("  " + kind + ": " + note[0] + " (skipped)"); continue; }
					Check(ref ok, dead[0] && dead[1] && hits[1] >= hits[0] * 2 - 1 && hits[1] <= hits[0] * 2 + 1,
						kind + ": dies after " + hits[0] + " hits at Normal and " + hits[1] + " at Nightmare (want about twice) - " + note[0] + " / " + note[1]);
					if (kind == AI_NetworkBehaviourType.HyenaBoss)
					{
						// (its counter adds the damage the hit really did - halved at Nightmare - so a phase comes after the same share of
						// its health; whether Raft then switches to the acid pools depends on the state it is in, so the count is logged)
						Check(ref ok, seen[0] > 0f && seen[1] > 0f && Mathf.Abs(seen[0] - lost[0]) < 0.5f && Mathf.Abs(seen[1] - lost[1]) < 0.5f,
							kind + ": its phase counter takes what a hit really did: " + seen[0].ToString("F1") + " of " + lost[0].ToString("F1") + " health at Normal, " + seen[1].ToString("F1") + " of " + lost[1].ToString("F1") + " at Nightmare (threshold " + limit[0].ToString("F0") + ")");
						Log("  " + kind + ": acid pool phases " + phases[0] + " at Normal, " + phases[1] + " at Nightmare (the counter's highest " + peak[0].ToString("F0") + " / " + peak[1].ToString("F0") + ")");
					}
				}
			}
			finally
			{
				MonsterDifficulty.Current = difficulty0;
				foreach (AI_NetworkBehaviour a in spawned) if (a != null) { try { UnityEngine.Object.Destroy(a.gameObject); } catch { } }
			}
			KeepAlive(player);
			yield return null;
			LevelCleanup(entry);
			if (ok) Log("PASS: bosses nightmare"); else Fail("bosses nightmare");
		}

		#endregion
		#region IL17: the level tags under other players' names

		[ConsoleCommand(name: "CILevelTagLook", docs: "Dev, in game with another player (IL17): the 'Lv n' tags - with the level up system on, each other player's tag sits just under Raft's name tag, says that player's level, and hides with the name (Raft's SetNameTagVisibility(false), as for distance, the pause menu and a hidden HUD) and comes back with it; this player's own name has none. CILevelTagLook off = the system is off: no tag shown anywhere. Logs 'PASS: level tag look'")]
		public static void LevelTagLookCommand(string[] args) { StartTest(LevelTagLookRoutine(args != null && args.Length > 0 && args[0] == "off")); }

		static IEnumerator LevelTagLookRoutine(bool off)
		{
			Network_Player local = RAPI.GetLocalPlayer();
			if (local == null) { Fail("run in a world"); yield break; }
			yield return new WaitForSeconds(1.2f); // (two tag checks)
			bool ok = true;
			var others = Players.All.Where(p => p != null && p != local && p.playerNameTextMesh != null).ToList();
			Transform own = local.playerNameTextMesh != null ? local.playerNameTextMesh.transform.Find("CustomIslands_Level") : null;
			Check(ref ok, own == null || !own.gameObject.activeInHierarchy, "this player's own name has no level tag");
			if (off)
			{
				Check(ref ok, !PlayerLevels.On, "the level up system is off here");
				foreach (Network_Player p in others)
				{
					Transform t = p.playerNameTextMesh.transform.Find("CustomIslands_Level");
					Check(ref ok, t == null || !t.gameObject.activeSelf, "no tag under " + p.name + " (" + (t == null ? "never made" : "hidden") + ")");
				}
				Check(ref ok, LevelTags.Shown.Count == 0, "no tag listed (" + LevelTags.Shown.Count + ")");
				if (ok) Log("PASS: level tag look"); else Fail("level tag look");
				yield break;
			}
			if (!PlayerLevels.On || others.Count == 0) { Fail("the level up system on and another player in the world wanted (on " + PlayerLevels.On + ", others " + others.Count + ")"); yield break; }
			foreach (Network_Player p in others)
			{
				TextMesh name = p.playerNameTextMesh;
				Transform t = name.transform.Find("CustomIslands_Level");
				int level = PlayerLevels.LevelOf(p.steamID.Id);
				TextMesh tag = t != null ? t.GetComponent<TextMesh>() : null;
				bool good = tag != null && t.gameObject.activeInHierarchy == name.gameObject.activeInHierarchy && tag.text == "Lv " + level;
				Check(ref ok, good,
					p.name + ": the tag says '" + (tag != null ? tag.text : "none") + "' (level " + level + "), shown with the name (" + (t != null && t.gameObject.activeInHierarchy) + " / " + name.gameObject.activeInHierarchy + ")");
				if (!good) continue;
				Check(ref ok, t.localPosition.y < 0f && Mathf.Abs(t.localPosition.x) < 0.01f && tag.characterSize < name.characterSize,
					p.name + ": just under the name and smaller (at " + t.localPosition.ToString("F2") + ", size " + tag.characterSize.ToString("F3") + " < " + name.characterSize.ToString("F3") + ")");
				bool was = name.gameObject.activeInHierarchy;
				p.SetNameTagVisibility(false);
				yield return null;
				bool hid = !t.gameObject.activeInHierarchy;
				p.SetNameTagVisibility(true);
				yield return new WaitForSeconds(0.6f);
				// (Raft hides a far player's name again at once, so "back" = the tag follows the name, whichever way it is now)
				bool back = t.gameObject.activeInHierarchy == name.gameObject.activeInHierarchy;
				Check(ref ok, hid && back, p.name + ": hides with Raft's name tag (" + hid + ") and comes back with it (tag " + t.gameObject.activeInHierarchy + ", name " + name.gameObject.activeInHierarchy + "; the name was " + (was ? "shown" : "hidden") + ")");
			}
			if (ok) Log("PASS: level tag look"); else Fail("level tag look");
		}

		#endregion
		#region IR14: the shark after Bruce

		/// <summary>A shark's look here: its roll in this world, whether its body has our tinted copy, its health.</summary>
		static string SharkLookOf(AI_NetworkBehavior_Shark s, out bool fits)
		{
			WorldRandomizer.Variant want = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Shark, s.ObjectIndex);
			// (a shark that got its look before the randomizer changed live keeps it - IM34 "coloured animals stay": the look
			// it was given counts, not what today's settings would roll)
			string given;
			bool kept = WorldRandomizer.VariantOfIndex.TryGetValue(s.ObjectIndex, out given) && given != null && (want == null || want.Name != given);
			bool tinted = false;
			foreach (Renderer r in WorldRandomizer.BodyOf(s))
			{
				string prop = BruceTexProp(r);
				if (prop == null) continue;
				var block = new MaterialPropertyBlock();
				r.GetPropertyBlock(block);
				Texture t = block.GetTexture(prop);
				if (t != null && t.name.StartsWith("CI_Tinted_")) tinted = true;
			}
			float max = s.networkEntity != null && s.networkEntity.stat_health != null ? s.networkEntity.stat_health.Max : 0f;
			bool rogue = RogueShark.IsRogue(s);
			// (an alpha: x2.5 health; the Rogue shark has its own colour)
			bool wantLook = kept || want != null, wantAlpha = kept ? given == "big bruce" : want != null && want.Alpha;
			fits = rogue || (tinted == wantLook && wantAlpha == (max > 160f * 1.5f));
			return "#" + s.ObjectIndex + " " + (rogue ? "rogue" : kept ? given + " (kept)" : want != null ? want.Name : "Raft's own") + ", " + (tinted ? "tinted" : "not tinted") + ", health " + max.ToString("F0");
		}

		[ConsoleCommand(name: "CISharkLook", docs: "Dev, in game (either player): every live shark here - its roll in this world's randomizer (the colour or Big Bruce from the seed and its index), whether its body really has that look (our tinted texture copy) and an alpha's health; logs 'SHARKS ...' and 'PASS: shark look' when every shark's look fits its roll (IR14: compare the line on both players)")]
		public static void SharkLookCommand() { StartTest(SharkLookRoutine()); }

		static IEnumerator SharkLookRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("in a world"); yield break; }
			yield return null;
			bool ok = true;
			var parts = new List<string>();
			foreach (AI_NetworkBehavior_Shark s in LiveSharks().OrderBy(x => x.ObjectIndex))
			{
				bool fits;
				string look = SharkLookOf(s, out fits);
				parts.Add(look);
				Check(ref ok, fits, "shark " + look);
			}
			Log("SHARKS " + string.Join("; ", parts.ToArray()));
			Check(ref ok, parts.Count > 0, parts.Count + " live shark(s)");
			if (ok) Log("PASS: shark look"); else Fail("shark look");
		}

		[ConsoleCommand(name: "CIBruceStay", docs: "Dev, world (host): the live shark is made Big Bruce (a Wild randomizer seed that rolls his index an alpha) and left alive, the randomizer left on that seed - then CISharkLook on player 2 (after 4 s: a player looks at a shark 3 s after the host's settings) must say 'big bruce, tinted' as on the host (IR14). For a CI world only: nothing is put back")]
		public static void BruceStayCommand() { StartTest(BruceStayRoutine()); }

		static IEnumerator BruceStayRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("bruce stay: run in a world, as the host"); yield break; }
			var sharkOut = new AI_NetworkBehavior_Shark[1];
			var oldOut = new RandomizerSettings[1];
			yield return BruceMake(sharkOut, oldOut);
			if (sharkOut[0] == null) yield break;
			bool fits;
			string look = SharkLookOf(sharkOut[0], out fits);
			if (fits && look.Contains("big bruce")) Log("PASS: bruce stays: " + look); else Fail("bruce stay: " + look);
		}

		[ConsoleCommand(name: "CISharkReroll",docs: "Dev, in game (host): Bruce killed, the next shark has its own roll (IR14) - the live shark is made Big Bruce (a Wild randomizer seed, as CIBruceLook), killed with one hit, and the next shark Raft sends (up to 5 minutes; Raft may reuse the dead one's object) must look as its own index rolls: no tinted copy or alpha health left over from Bruce. CISharkReroll keep = leave the randomizer on that seed (for CISharkLook on player 2); else it is put back")]
		public static void SharkRerollCommand(string[] args) { StartTest(SharkRerollRoutine(args != null && args.Contains("keep"))); }

		static IEnumerator SharkRerollRoutine(bool keep)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || player == null || host == null) { Fail("shark reroll: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var sharkOut = new AI_NetworkBehavior_Shark[1];
			var oldOut = new RandomizerSettings[1];
			yield return BruceMake(sharkOut, oldOut);
			AI_NetworkBehavior_Shark bruce = sharkOut[0];
			if (bruce == null) { if (oldOut[0] != null) WorldRandomizer.Set(oldOut[0]); yield break; }
			try
			{
				bool fits;
				string was = SharkLookOf(bruce, out fits);
				Check(ref ok, fits && was.Contains("big bruce"), "Bruce: " + was);
				int id = bruce.GetInstanceID();
				uint idx = bruce.ObjectIndex;
				// (a killed shark floats until it decays, then Raft sends the next one: both waits shortened for the test)
				AI_State_Decay_Shark decay = bruce.GetComponentInChildren<AI_State_Decay_Shark>(true);
				if (decay != null)
				{
					Traverse body = Traverse.Create(decay).Field("maxTimeInDecayState");
					float bodyWas = body.GetValue<float>(), nextWas = decay.decayRespawnTime;
					body.SetValue(3f); decay.decayRespawnTime = 5f;
					Log("SHARKS Bruce's body decays after 3 s, the next shark 5 s later (Raft's " + bodyWas.ToString("0", CultureInfo.InvariantCulture) + " s and " + nextWas.ToString("0", CultureInfo.InvariantCulture) + " s, shortened for the test)");
				}
				// (before that the body floats in Raft's dead state, which waits removeBodyTime and then hands over to the decay)
				AI_State_Dead dead = bruce.GetComponentInChildren<AI_State_Dead>(true);
				if (dead != null)
				{
					Traverse floatFor = Traverse.Create(dead).Field("removeBodyTime");
					Log("SHARKS Bruce's body floats 2 s before it decays (Raft's " + floatFor.GetValue<float>().ToString("0", CultureInfo.InvariantCulture) + " s, removeBody " + Traverse.Create(dead).Field("removeBody").GetValue<bool>() + ", shortened for the test)");
					floatFor.SetValue(2f);
				}
				host.DamageEntity(bruce.networkEntity, bruce.transform, bruce.networkEntity.stat_health.Max * 10f, bruce.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
				yield return new WaitForSeconds(1f);
				AI_StateMachine bsm = bruce == null ? null : bruce.GetComponentInChildren<AI_StateMachine>(true);
				AI_State now = bsm == null ? null : Traverse.Create(bsm).Field("currentState").GetValue<AI_State>();
				// (Raft's dead shark may get its health back while it floats: the state is what counts)
				Check(ref ok, bruce == null || bruce.networkEntity.IsDead || now is AI_State_Dead || now is AI_State_Decay, "Bruce is dead (" + (now == null ? "gone" : now.GetType().Name) + ", health " + (bruce == null ? "-" : bruce.networkEntity.stat_health.Value.ToString("0", CultureInfo.InvariantCulture)) + ")");
				AI_NetworkBehavior_Shark next = null;
				float t0 = Time.realtimeSinceStartup;
				while (next == null && Time.realtimeSinceStartup - t0 < 300f)
				{
					next = LiveSharks().FirstOrDefault(s => s.ObjectIndex != idx && !RogueShark.IsRogue(s) && s.networkEntity != null && !s.networkEntity.IsDead);
					KeepAlive(player);
					if (next == null && (int)(Time.realtimeSinceStartup - t0) % 20 < 2)
					{
						// (what Raft's respawn waits on: it sends a shark when the body decays and it counts one shark)
						string state = "gone";
						if (bruce != null)
						{
							AI_StateMachine sm = bruce.GetComponentInChildren<AI_StateMachine>(true);
							AI_State cur = sm == null ? null : Traverse.Create(sm).Field("currentState").GetValue<AI_State>();
							state = (cur == null ? "no state" : cur.GetType().Name) + (decay == null ? "" : ", decay " + Traverse.Create(decay).Field("removeBodyTimeProgress").GetValue<float>().ToString("0.0", CultureInfo.InvariantCulture) + " s");
						}
						int count = -1;
						try { count = ComponentManager<Network_Host_Entities>.Value.SharkCount; } catch { }
						Log("SHARKS waiting " + (Time.realtimeSinceStartup - t0).ToString("F0") + " s: Bruce " + state + "; Raft counts " + count + " shark(s), " + LiveSharks().Count + " live");
					}
					if (next == null) yield return new WaitForSeconds(2f);
				}
				if (next == null) { Check(ref ok, false, "a new shark within 5 minutes"); }
				else
				{
					yield return new WaitForSeconds(4f); // (the randomizer looks at new animals now and then)
					string look = SharkLookOf(next, out fits);
					Check(ref ok, fits, "the next shark after " + (Time.realtimeSinceStartup - t0).ToString("F0") + " s (" + (next.GetInstanceID() == id ? "Bruce's own object reused" : "a new object") + "): " + look + " - as its own roll");
				}
			}
			finally { if (!keep && oldOut[0] != null) WorldRandomizer.Set(oldOut[0]); }
			if (ok) Log("PASS: shark reroll"); else Fail("shark reroll");
		}

		#endregion
		#region Two plans with one name; a plan replaced under its name (UW2)

		const string Uw2Own = "CI UW2 Plan", Uw2Lib = "CI UW2 Plan (CI Author)";

		static void Uw2MoveAway(string name)
		{
			string from = WorldPlan.PathFor(name);
			if (!File.Exists(from)) return;
			string to = Path.Combine(DynamicIslands.assetpath, "deleted", "plans");
			Directory.CreateDirectory(to);
			File.Move(from, Path.Combine(to, name + " " + DateTime.Now.ToString("yyyyMMddHHmmssfff") + WorldPlan.Extension));
		}

		[ConsoleCommand(name: "CIPlanSameName", docs: "Dev (UW2): CIPlanSameName prep (main menu: the player's 'CI Adventure' and 'CI Adventure (CI Author)' written, each with its own id) | swap (main menu: 'CI Adventure' copied as World Plans' Copy... does, the old file deleted, a new plan made under the old name) | check <plan> on|off [fromworld] (world, host: the world's plan, its random switch, the plan id it keeps) | tidy")]
		public static void PlanSameName(string[] args)
		{
			string mode = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			bool ok = true;
			if (mode == "prep" || mode == "tidy")
			{
				foreach (string n in new[] { Uw2Own, Uw2Lib, Uw2Own + " copy" }) Uw2MoveAway(n);
				if (mode == "tidy") { Log("PASS: plan same name tidy"); return; }
				new WorldPlan { Name = Uw2Own, Description = "The player's own adventure", Random = true }.Save();
				new WorldPlan { Name = Uw2Lib, Description = "The library's adventure", Random = false }.Save();
				WorldPlan a = WorldPlan.Load(Uw2Own), b = WorldPlan.Load(Uw2Lib);
				Check(ref ok, a != null && b != null && a.Id.Length > 0 && b.Id.Length > 0 && a.Id != b.Id, "two plans, each with its own id (" + (a != null ? a.Id : "-") + ", " + (b != null ? b.Id : "-") + ")");
				List<string> all = WorldPlan.All();
				Check(ref ok, all.Count(n => n == Uw2Own) == 1 && all.Count(n => n == Uw2Lib) == 1, "the plan list has each once");
				if (ok) Log("PASS: plan same name prep"); else Fail("plan same name prep");
				return;
			}
			if (mode == "swap")
			{
				WorldPlan old = WorldPlan.Load(Uw2Own);
				Check(ref ok, old != null && old.Id.Length > 0, "'" + Uw2Own + "' is there");
				if (old == null) { Fail("plan same name swap"); return; }
				// (as World Plans' Copy... does it, then Delete, then New with the old name)
				var c = WorldPlan.Parse(Uw2Own + " copy", old.ToText()); c.Id = WorldPlan.NewId(); c.Save();
				Uw2MoveAway(Uw2Own);
				new WorldPlan { Name = Uw2Own, Description = "Another plan, the same name", Random = false }.Save();
				WorldPlan copy = WorldPlan.Load(Uw2Own + " copy"), now = WorldPlan.Load(Uw2Own);
				Check(ref ok, copy != null && copy.Id.Length > 0 && copy.Id != old.Id && copy.Random == old.Random, "the copy has its own id (" + (copy != null ? copy.Id : "-") + ") and the plan's settings");
				Check(ref ok, now != null && now.Id.Length > 0 && now.Id != old.Id && !now.Random, "the new '" + Uw2Own + "' has its own id (" + (now != null ? now.Id : "-") + ", the old one " + old.Id + ") and random off");
				if (ok) Log("PASS: plan same name swap"); else Fail("plan same name swap");
				return;
			}
			if (mode == "check" && args.Length >= 3)
			{
				bool fromWorld = args[args.Length - 1].Equals("fromworld", StringComparison.OrdinalIgnoreCase);
				int end = args.Length - (fromWorld ? 1 : 0);
				bool random = args[end - 1].Equals("on", StringComparison.OrdinalIgnoreCase);
				string plan = string.Join(" ", args.Skip(1).Take(end - 2).ToArray());
				Check(ref ok, LoadSceneManager.IsGameSceneLoaded && Raft_Network.IsHost, "in a world, as its host");
				WorldPlan file = WorldPlan.Load(plan), p = WorldDirector.Plan;
				Check(ref ok, WorldDirector.PlanName == plan && p != null, "the world's plan: '" + WorldDirector.PlanName + "' (want '" + plan + "')");
				Check(ref ok, p != null && p.Random == random, "its random islands: " + (p != null && p.Random ? "on" : "off") + " (want " + (random ? "on" : "off") + ")");
				Check(ref ok, CustomIslandSpawner.Enabled == random, "the spawner follows it (" + CustomIslandSpawner.Enabled + ")");
				if (fromWorld)
					Check(ref ok, WorldDirector.PlanFromWorld && !WorldDirector.PlanWasEdited && WorldDirector.PlanId.Length > 0 && file != null && file.Id != WorldDirector.PlanId,
						"the world's own copy plays, not the other plan now called '" + plan + "' (world id " + WorldDirector.PlanId + ", the file's " + (file != null ? file.Id : "none") + ")");
				else
					Check(ref ok, file != null && WorldDirector.PlanId.Length > 0 && WorldDirector.PlanId == file.Id, "the world keeps the plan's id (" + WorldDirector.PlanId + ")");
				if (ok) Log("PASS: plan same name check"); else Fail("plan same name check");
				return;
			}
			Fail("CIPlanSameName prep|swap|check <plan> on|off [fromworld]|tidy");
		}

		#endregion
		#region The world window's size (Esc > Custom Islands, the compact layout)

		[ConsoleCommand(name: "CIWorldWindowShots", docs: "Dev, world: the world window (Esc > Custom Islands) at 1920x1080 and 1366x768 - inside the screen, no text cut off, tick boxes big enough; a picture of each (shot_worldwindow_<w>x<h>.png), the screen size put back")]
		public static void WorldWindowShots()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world"); return; }
			StartTest(WorldWindowShotsRoutine());
		}

		static IEnumerator WorldWindowShotsRoutine()
		{
			bool ok = true;
			int w0 = Screen.width, h0 = Screen.height;
			FullScreenMode mode0 = Screen.fullScreenMode;
			bool wasOpen = WorldWindow.IsOpen;
			try
			{
				WorldWindow.Open();
				RectTransform panel = Traverse.Create(typeof(WorldWindow)).Field("panel").GetValue<RectTransform>();
				Check(ref ok, WorldWindow.IsOpen && panel != null, "the world window opens");
				if (panel == null) yield break;
				foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) })
				{
					Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
					yield return new WaitForSecondsRealtime(1.5f);
					WorldWindow.Refresh();
					yield return new WaitForSecondsRealtime(1.2f);
					Canvas.ForceUpdateCanvases();
					int w = Screen.width, h = Screen.height;
					var c = new Vector3[4];
					panel.GetWorldCorners(c);
					Canvas root = panel.GetComponentInParent<Canvas>().rootCanvas;
					Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
					Vector2 lo = RectTransformUtility.WorldToScreenPoint(cam, c[0]), hi = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
					bool inside = lo.x >= -1f && lo.y >= -1f && hi.x <= w + 1f && hi.y <= h + 1f;
					Check(ref ok, inside, w + "x" + h + ": the window inside the screen (" + lo.x.ToString("F0") + "," + lo.y.ToString("F0") + " to " + hi.x.ToString("F0") + "," + hi.y.ToString("F0") + ", " + (hi.x - lo.x).ToString("F0") + " x " + (hi.y - lo.y).ToString("F0") + " px)");
					// Text cut off: more lines than its box holds (truncating), or characters not shown
					var cut = new List<string>();
					int texts = 0;
					foreach (Text t in panel.GetComponentsInChildren<Text>(false))
					{
						if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy) continue;
						texts++;
						RectTransform rt = t.rectTransform;
						bool tooTall = t.verticalOverflow == VerticalWrapMode.Truncate && t.preferredHeight > rt.rect.height + 2f;
						bool tooWide = t.horizontalOverflow == HorizontalWrapMode.Overflow && LayoutUtility.GetPreferredWidth(rt) > rt.rect.width + 2f && t.alignment != TextAnchor.MiddleCenter;
						if (tooTall || tooWide) cut.Add(t.name + " '" + (t.text.Length > 30 ? t.text.Substring(0, 30) + "..." : t.text) + "' (" + (tooTall ? "needs " + t.preferredHeight.ToString("F0") + " px high, has " + rt.rect.height.ToString("F0") : "too wide") + ")");
					}
					Check(ref ok, cut.Count == 0, w + "x" + h + ": " + texts + " texts, none cut off" + (cut.Count > 0 ? ": " + string.Join("; ", cut.Take(6).ToArray()) + (cut.Count > 6 ? " (+" + (cut.Count - 6) + ")" : "") : ""));
					// The left column (2890217): no HostSettings group (regrow, Receiver, Defaults) and no gap where it was
					Transform left = panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Left");
					if (left != null)
					{
						var shown = Enumerable.Range(0, left.childCount).Select(i => left.GetChild(i) as RectTransform).Where(x => x != null && x.gameObject.activeSelf).ToList();
						float gap = 0f;
						for (int i = 1; i < shown.Count; i++) gap = Mathf.Max(gap, (shown[i - 1].localPosition.y + shown[i - 1].rect.yMin) - (shown[i].localPosition.y + shown[i].rect.yMax));
						bool empty = shown.Any(x => x.GetComponentsInChildren<Text>(false).All(t => string.IsNullOrEmpty(t.text)) && x.rect.height > 2f);
						Check(ref ok, left.GetComponentsInChildren<Transform>(true).All(x => x.name != "HostSettings") && gap <= 6f && !empty,
							w + "x" + h + ": the left column " + string.Join(", ", shown.Select(x => x.name).ToArray()) + " - no HostSettings, the widest gap " + gap.ToString("F0") + " px" + (empty ? ", an empty block" : ""));
					}
					else Check(ref ok, false, w + "x" + h + ": the left column is found");
					// Islands while sailing (98f2fae): the two tick boxes of every line start at the same x as the lines above
					var lines = panel.GetComponentsInChildren<Transform>(false).Where(x => x.name.StartsWith("Line") && x.childCount >= 2 && x.GetChild(1).GetComponent<Button>() != null && x.GetComponentsInParent<Transform>(true).Any(a => a.name == "IslandList")).ToList();
					if (lines.Count > 1)
					{
						var corners = new Vector3[4];
						Func<Transform, int, float> boxX = (line, col) =>
						{
							// (the Check row's Box, on every line: its Tick, inset 3 px, shows only when ticked - the smallest Image
							// measured the tick on ticked lines and the box on the others, 3 px x the UI scale apart - 10-21)
							Transform cell = line.GetChild(col), box = cell.Find("Box");
							if (box == null) { Image least = cell.GetComponentsInChildren<Image>(false).Where(im => im.transform != cell).OrderBy(im => ((RectTransform)im.transform).rect.width).FirstOrDefault(); box = least != null ? least.transform : cell; }
							((RectTransform)box).GetWorldCorners(corners);
							return corners[0].x;
						};
						float spread0 = lines.Max(l => boxX(l, 0)) - lines.Min(l => boxX(l, 0)), spread1 = lines.Max(l => boxX(l, 1)) - lines.Min(l => boxX(l, 1));
						Check(ref ok, spread0 <= 1.5f && spread1 <= 1.5f, w + "x" + h + ": the island tick boxes line up over " + lines.Count + " lines (first column within " + spread0.ToString("F1") + " px, second within " + spread1.ToString("F1") + " px)");
					}
					else Check(ref ok, false, w + "x" + h + ": the island lines are found (" + lines.Count + ")");
					// The tick boxes: the box of each check row, in screen pixels
					float smallest = float.MaxValue; int boxes = 0;
					foreach (string name in RandomizerSettings.Features.Select(f => "Part_" + f).Concat(WorldOptions.All.Select(o => "Option_" + o)))
					{
						Button b = WorldWindow.ButtonNamed(name);
						if (b == null) continue;
						foreach (Image img in b.GetComponentsInChildren<Image>(true).Where(i => i.gameObject != b.gameObject))
						{
							Vector3[] bc = new Vector3[4];
							img.rectTransform.GetWorldCorners(bc);
							float px = (RectTransformUtility.WorldToScreenPoint(cam, bc[2]) - RectTransformUtility.WorldToScreenPoint(cam, bc[0])).y;
							if (px > 4f) { smallest = Mathf.Min(smallest, px); boxes++; }
						}
					}
					if (boxes > 0) Check(ref ok, smallest >= 10f, w + "x" + h + ": the smallest tick box is " + smallest.ToString("F0") + " px high (" + boxes + " looked at)");
					else Log("  (no tick box found by name to measure)");
					Screenshot(new[] { "worldwindow_" + w + "x" + h });
					yield return new WaitForSecondsRealtime(0.8f);
				}
			}
			finally
			{
				Screen.SetResolution(w0, h0, mode0);
				if (!wasOpen) WorldWindow.Close();
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Check(ref ok, Screen.width == w0 && Screen.height == h0, "the screen size put back: " + Screen.width + "x" + Screen.height);
			if (ok) Log("PASS: world window shots"); else Fail("world window shots");
		}

		#endregion
	}
}
