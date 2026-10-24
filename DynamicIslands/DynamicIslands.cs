using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using AsyncOperation = UnityEngine.AsyncOperation;
using UnityEngine.UI;
using Redcode.Awaiting;
using HMLLibrary;
using RaftModLoader;
using DynamicIslands.Editor;
using System.Reflection;
using RuntimeGizmos;

namespace DynamicIslands
{
	public class DynamicIslands : Mod
	{
		public static readonly string assetpath = FullAssetPath();

		/// <summary>
		/// Mods\DynamicIslands\ as a full path, made once (AU41: it was relative, so a working directory changed by anything
		/// in Raft redirected every save): beside Raft_Data, in Raft's folder - where it was found so far, Raft being started
		/// in its folder. Where that differs from the working directory at the start and only the latter has the folder,
		/// that one, as before.
		/// </summary>
		static string FullAssetPath()
		{
			const string relative = @"Mods\DynamicIslands\";
			string before = Path.GetFullPath(relative);
			try
			{
				string raft = Path.GetDirectoryName(Path.GetFullPath(UnityEngine.Application.dataPath));
				if (string.IsNullOrEmpty(raft)) return before;
				string beside = Path.Combine(raft, relative);
				if (!beside.Equals(before, StringComparison.OrdinalIgnoreCase) && Directory.Exists(before) && !Directory.Exists(beside))
				{
					Debug.LogWarning("[CUSTOM ISLANDS] Raft's folder is " + raft + " but its files are in " + before + ": using those");
					return before;
				}
				return beside;
			}
			// (Application.dataPath can't be read here - not on Unity's main thread: where it was found so far)
			catch (Exception) { return before; }
		}

		/// <summary>Name used by the editor's Save/Load menu entries; set by LoadIsland/SaveIsland commands.</summary>
		public static string currentIslandName = "myisland";
		/// <summary>Metres above sea level the island being edited floats at in game (negative = under water); saved with it.</summary>
		public static float currentElevation;
		/// <summary>Style of the island being edited (TerrainPainter.Styles); saved with it.</summary>
		public static int currentStyle = TerrainPainter.Tropical;
		/// <summary>Island-wide settings of the island being edited (IslandProps: name shown to players, author, description); saved with it.</summary>
		public static Dictionary<string, string> currentIslandProps = new Dictionary<string, string>();
		/// <summary>The open island file's tagged tail (IslandFile.Tail: what a newer version wrote), saved with it again.</summary>
		public static Dictionary<string, byte[]> currentIslandTail = new Dictionary<string, byte[]>();

		/// <summary>
		/// Editor Y of the sea for the island being edited: IslandFile.DefaultWaterLevel (20 m above the terrain's base)
		/// for islands on a shallow seabed, IslandFile.DeepWaterLevel for islands generated on Raft's deep sea floor.
		/// Saved with it.
		/// </summary>
		public static float EditorWaterLevel = IslandFile.DefaultWaterLevel;
		static GameObject waterPlane;

		/// <summary>Sets the sea level of the island being edited and moves the editor's blue sea plane to it.</summary>
		public static void SetEditorWaterLevel(float level)
		{
			EditorWaterLevel = Mathf.Clamp(level, 1f, IslandGenerator.BuildArea.y - 10f);
			PlaceWaterPlane();
		}

		static float planeFor = float.NaN, planeSize;
		/// <summary>Tests: the blue sea plane.</summary>
		internal static GameObject WaterPlane { get { return waterPlane; } }

		/// <summary>
		/// Editor Y of the sea as the island will meet it in a world (ROADMAP E2): the sea level less the island's height -
		/// 60 m below a flying island, 30 m above a sunken one. The blue plane is shown there.
		/// </summary>
		public static float EditorSeaInWorld { get { return (terraineditor.terrain != null ? terraineditor.terrain.transform.position.y : 0f) + EditorWaterLevel - currentElevation; } }

		/// <summary>The blue plane where the sea will be (again when the island's height or sea level changed).</summary>
		static void PlaceWaterPlane()
		{
			if (waterPlane == null) return;
			Vector3 p = waterPlane.transform.position;
			// (as big as the terrain: a variation of Temperance gets a 2000 m terrain, the plane stayed 1000 m - CA29)
			Terrain t = terraineditor.terrain;
			if (t != null && t.terrainData != null)
			{
				Vector3 size = t.terrainData.size, at = t.transform.position;
				p = new Vector3(at.x + size.x / 2f, p.y, at.z + size.z / 2f);
				waterPlane.transform.localScale = new Vector3(size.x / 10f, 1, size.z / 10f);
				planeSize = size.x;
			}
			waterPlane.transform.position = new Vector3(p.x, EditorSeaInWorld, p.z);
			planeFor = currentElevation;
		}

		/// <summary>Every frame in the editor: the island's height changed (the Island tab, undo, opening an island) - the plane follows.</summary>
		static void TickWaterPlane()
		{
			if (waterPlane == null) return;
			if (currentElevation == planeFor)
			{
				Terrain t = terraineditor.terrain;
				if (t != null && t.terrainData != null && t.terrainData.size.x != planeSize) PlaceWaterPlane();
				return;
			}
			bool first = float.IsNaN(planeFor);
			PlaceWaterPlane();
			if (!first && InEditor() && !IslandTest.Busy)
				Notify(Mathf.Abs(currentElevation) < 0.01f ? "The blue plane is the sea again, at the island's water line"
					: "The blue plane shows the sea as it will be in a world: " + Mathf.Abs(currentElevation).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " m " + (currentElevation > 0 ? "below" : "above") + " the island's water line");
		}

		/// <summary>Sets the style of the island being edited: re-skins the editor terrain and relabels the paint buttons.</summary>
		public static void SetEditorStyle(int style)
		{
			currentStyle = Mathf.Clamp(style, 0, TerrainPainter.Styles.Length - 1);
			if (terraineditor.terrain != null) TerrainPainter.SetStyle(terraineditor.terrain, currentStyle);
			EditorUI.RefreshStyle();
		}

		/// <summary>A second style mixed into the island being edited (its textures are paint layers 5-8, ROADMAP E6), or -1; saved with it.</summary>
		public static int currentMixStyle = -1;

		/// <summary>Mixes a second style into the island being edited (-1: none): the terrain gets its four textures as layers 5-8 (TerrainBrushes.SetEditorMix is the undoable one).</summary>
		public static void SetEditorMixStyle(int mix)
		{
			currentMixStyle = mix < 0 ? -1 : Mathf.Clamp(mix, 0, TerrainPainter.Styles.Length - 1);
			if (terraineditor.terrain != null) TerrainPainter.SetStyle(terraineditor.terrain, currentStyle, currentMixStyle);
			EditorUI.RefreshStyle();
		}
		public static LoadSceneManager loadSceneManagerinstance;

		public AssetBundle mainbundle;
		public AssetBundle helperbundle;



		public static List<Shader> _shaders = new List<Shader>();
		public static TransformGizmo EditorGizmoHandler;

		public static DynamicIslands instance;



		#region IslandObjectDefinition

		// Placeable objects now live in PlaceableCatalog (built from Raft's Vasagatan scene)



		



		#endregion


		/// <summary>Parts of Start that failed (AU11: named on the main menu, and the rest still starts).</summary>
		internal static readonly List<string> StartFailures = new List<string>();

		/// <summary>One step of Start on its own (ROADMAP AU11: one failure - a renamed menu, a read-only Mods folder - stopped
		/// Start before the world hooks, and worlds then loaded without their islands and the next save rewrote their file).</summary>
		static void StartStep(string what, Action step)
		{
			try { step(); }
			catch (Exception e)
			{
				StartFailures.Add(what);
				Debug.LogError("[CUSTOM ISLANDS] Starting: " + what + " failed: " + e);
			}
		}

		public void Start()
		{
			instance = this;
			HNotification loading = null;
			StartStep("the loading notice", () => loading = FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.spinning, "Loading Custom Islands..."));
			// The mod's own error note (RML's "AN ERROR OCCURED!" can't be clicked): from the start, so a step that fails shows it
			StartStep("the error notes", Editor.ErrorNotes.Install);
			// (RML compiles the mod on every start unless its cached copy is kept - ROADMAP CW2)
			StartStep("the RML cache", () => Editor.RmlCache.Begin(this));
			// The world hooks first: whatever else fails, worlds still load and save their custom islands
			StartStep("the world hooks", () =>
			{
				SceneManager.sceneLoaded += OnSceneLoaded;
				// Custom islands saved with a world come back when it loads
				SaveAndLoad.LoadComplete += OnLoadComplete;
				// An island's quest done: its "on.quest" actions
				QuestTracker.Advanced += Behaviours.OnQuestAdvanced;
				// The host saves the world when Raft is closed, and soon after quest and story steps (GUIDE 13, "Raft crashed in a world")
				SaveSoon.Hook();
			});
			StartStep("Raft's menu look", () => Editor.UIKit.CaptureRaftLook()); // Raft's menu sprites and fonts, while the main menu has them loaded
			StartStep("the await helpers", () =>
			{
				// The await helpers normally self-initialise at game startup, which never happens for a mod
				Redcode.Awaiting.Engine.ContextHelper.SaveContext();
				Redcode.Awaiting.Engine.RoutineHelper.CreateInstance();
			});
			StartStep("the scene loader", () => loadSceneManagerinstance = FindObjectOfType<LoadSceneManager>());
			StartStep("the patches", () =>
			{
				var harmony = harmonyInstance = new Harmony(HarmonyId);
				// (each patch on its own: after a Raft update one that no longer fits is named, the others still work)
				Editor.PatchHealth.PatchAll(harmony);
				try { CreatureSpawner.Patch(harmony); } catch (Exception e) { Editor.PatchHealth.Failed("CreatureSpawner", e); }
				try { PlaceableCatalog.Patch(harmony); } catch (Exception e) { Editor.PatchHealth.Failed("PlaceableCatalog", e); }
			});
			StartStep("the Mods\\DynamicIslands folder", () =>
			{
				if (!Directory.Exists(assetpath)) Directory.CreateDirectory(assetpath);
				// (saves that Raft stopped half way: brought back)
				Editor.SafeFile.RecoverAll(assetpath);
				if (Directory.EnumerateFiles(assetpath).Count() == 0) Debug.LogWarning("There are no custom Islands installed!");
			});
			StartStep("the editor's assets", () =>
			{
				if (GetEmbeddedFileBytes("editorsceneci.assets").Length == 0) Debug.Log("embeddedfilebytes are null");
				mainbundle = AssetBundle.LoadFromMemory(GetEmbeddedFileBytes("editorsceneci.assets"));
				helperbundle = AssetBundle.LoadFromMemory(GetEmbeddedFileBytes("maincustomislandsbundle.assets"));
			});
			// Adding the Editor button to the main menu (again every time the main menu scene is reloaded)
			StartStep("the main menu buttons", HookUI);
			// Map types of one's own (Mods\DynamicIslands\maptypes\*.maptype), after the built-in ones
			StartStep("the map type files", () => Editor.MapTypeFiles.LoadAll());
			// Raft's world shifts and "world received" (for clients): hooked every frame by HookRaftEvents, since
			// Raft empties these events when a game is left
			StartStep("Raft's world events", HookRaftEvents);
			// The extra upgrades' items (the large battery): before a world builds its crafting menu
			StartStep("the extra upgrades", () => Editor.ExtraUpgrades.Register());

			// Dev builds: the test commands can also be run from a file (release builds leave DevTests out)
			try
			{
				MethodInfo devInit = typeof(DynamicIslands).Assembly.GetType("DynamicIslands.DevTests")?.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
				if (devInit != null) devInit.Invoke(null, null);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Dev tests: " + e.Message); }

			StartStep("the loaded notice", () =>
			{
				if (loading != null) loading.Close();
				if (StartFailures.Count == 0) FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "Custom Islands has been loaded!", 5);
				else FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "Custom Islands started, but not all of it: " + string.Join(", ", StartFailures.ToArray()) + " (see the log, F10)", 12);
			});
			Debug.Log("[CUSTOM ISLANDS] Mod Custom Islands has been loaded" + (StartFailures.Count == 0 ? " successfully!" : " - these parts failed: " + string.Join(", ", StartFailures.ToArray())));
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (mode == LoadSceneMode.Single && GameObject.Find("MainMenuCanvas") != null)
			{
				// (the editor left - Main menu, a test: its undo steps hold the destroyed island's objects and terrain)
				CommandUndoRedo.UndoRedoManager.Clear();
				try { HookUI(); }
				catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] The main menu's buttons (Raft's menu changed?): " + e); }
			}
		}

		private void HookUI()
		{
			GameObject MainMenuParent = GameObject.Find("MainMenuCanvas");
			if (MainMenuParent == null) return;
			Transform existing = MainMenuParent.transform.Find("MenuButtons/EDITOR");
			if (existing != null) return;

			GameObject MenuButtonsParent = MainMenuParent.transform.Find("MenuButtons").gameObject;

			GameObject NewGamePanelParent = MainMenuParent.transform.Find("New Game Box").gameObject;
			GameObject LoadGamePanelParent = MainMenuParent.transform.Find("Load Game Box").gameObject;

			GameObject CreateGameButton = MainMenuParent.transform.Find("New Game Box").transform.Find("CreateGameButton").gameObject;

			Debug.Log("Hooking UI");

			try
			{
				//Hooking onto the main menu to add new buttons
				//Modpacks browser online
				GameObject ModpacksButton = Instantiate(MenuButtonsParent.transform.Find("New Game").gameObject, MenuButtonsParent.transform);
				ModpacksButton.name = "EDITOR";
				ModpacksButton.transform.SetSiblingIndex(3);
				Debug.Log("namebutton: " + ModpacksButton.name);
				ModpacksButton.GetComponentInChildren<Text>().text = "EDITOR";
				ModpacksButton.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();

				ModpacksButton.GetComponent<Button>().onClick.RemoveAllListeners();

				ModpacksButton.GetComponent<Button>().onClick.AddListener(() =>
				{
					Debug.Log("Editor");
					// (a start window over the menu: an island, a world plan, or the guide - TODO 5b)
					CreateWindow.Open();
				});

				// The island library: plans and islands others made, to download (LibraryWindow)
				GameObject library = Instantiate(MenuButtonsParent.transform.Find("New Game").gameObject, MenuButtonsParent.transform);
				library.name = "LIBRARY";
				library.transform.SetSiblingIndex(4);
				Text libraryText = library.GetComponentInChildren<Text>();
				libraryText.text = "ISLAND LIBRARY";
				// (Raft's menu words are best fit from a very large size down to what fits the button; the old smallest size (half)
				// was still too big for these longer words, and Unity then draws none. They may shrink as far as they need)
				libraryText.resizeTextForBestFit = true;
				libraryText.resizeTextMaxSize = libraryText.fontSize;
				libraryText.resizeTextMinSize = 10;
				library.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
				library.GetComponent<Button>().onClick.AddListener(() => LibraryWindow.Open());

			}
			catch (Exception e)
			{
				Debug.Log("Error adding button to main menu raft ui: " + e);
			}
			// The first release: a box telling new players it is experimental
			ExperimentalNotice.Show(MainMenuParent.transform);
			PatchHealth.ShowIfFailed(); // (a patch that no longer fits this Raft: which parts are off)
			PcCheck.ShowIfProblems(); // (the PC: a folder the mod can't write, an unzipped .rmod... - AU45)
			// The editor's start window and the world plan editor live on the menu too (back from the editor: open again)
			MenuWindows.Ensure();
			EditorEntry.OnMainMenu();
			// (an object index from before a Raft update is made again here, in the background - not only by the editor)
			PlaceableCatalog.CheckIndexInMenu();




		}

		public void LaunchEditor()
		{
			string[] str = new string[] { };
			LoadEditor(str);
		}

		static readonly Action<Vector3> onWorldShift = shift => RaftEvent("a world shift", () => IslandWorldState.OnWorldShift(shift));
		static readonly Action onWorldReceived = () => RaftEvent("the host's world arriving", IslandNetwork.OnWorldReceived);

		/// <summary>
		/// Raft's world loaded (SaveAndLoad.LoadComplete). Raft's own handlers hang on the same event - among them
		/// RaftCollisionManager.OnLoadComplete, which gives the raft the colliders that run it aground - and an exception in
		/// a handler stops the ones after it: an error of the mod's in a long-used world left the raft without them, and it
		/// drifted through islands (ROADMAP R16). The mod's part never throws into Raft's event now.
		/// </summary>
		static void OnLoadComplete()
		{
			RaftEvent("the world loading", IslandWorldState.OnWorldLoaded);
			RaftEvent("the world loading (creatures)", CreatureSpawner.OnWorldLoaded);
		}

		/// <summary>The mod's part of one of Raft's events, never throwing into it (R16).</summary>
		static void RaftEvent(string what, Action a)
		{
			try { a(); }
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Handling " + what + " failed: " + e); }
		}

		/// <summary>
		/// Keeps the mod on two of Raft's static events. Raft sets them to null when a game is left
		/// (WorldShiftManager and Raft_Network, SceneEventInterface.OnSceneEvent), which silently dropped the mod's
		/// handlers: after going back to the main menu once, custom islands stopped following world shifts, and a
		/// player joining again got no islands from the host. Found in the two-player test.
		/// </summary>
		static void HookRaftEvents()
		{
			// (looked into only when the event changed since the last frame: GetInvocationList made two arrays every frame,
			// also on the main menu - audit 2026-10-06. Delegates are immutable: the same reference is the same handlers)
			// (the custom islands follow Raft's floating-origin world shifts)
			if (WorldShiftManager.OnWorldShift == null || (object)WorldShiftManager.OnWorldShift != hookedShift)
			{
				if (WorldShiftManager.OnWorldShift == null || Array.IndexOf(WorldShiftManager.OnWorldShift.GetInvocationList(), onWorldShift) < 0)
					WorldShiftManager.OnWorldShift += onWorldShift;
				hookedShift = WorldShiftManager.OnWorldShift;
			}
			// (a client asks for the host's islands once the host's world is here: the raft is where the host's is)
			if (Raft_Network.OnWorldReceivedLate == null || (object)Raft_Network.OnWorldReceivedLate != hookedReceived)
			{
				if (Raft_Network.OnWorldReceivedLate == null || Array.IndexOf(Raft_Network.OnWorldReceivedLate.GetInvocationList(), onWorldReceived) < 0)
					Raft_Network.OnWorldReceivedLate += onWorldReceived;
				hookedReceived = Raft_Network.OnWorldReceivedLate;
			}
		}
		static object hookedShift, hookedReceived;

		private void Update()
		{
			try { HookRaftEvents(); }
			catch (Exception e) { TickError("Raft events", e); }
			try { CustomIslandSpawner.Tick(); }
			catch (Exception e) { TickError("Island spawner", e); }
			try { IslandNetwork.Tick(); }
			catch (Exception e) { TickError("Island network", e); }
			try { IslandObjectState.Tick(); }
			catch (Exception e) { TickError("Harvests", e); }
			try { PlayerHold.Tick(); PlayerPlaces.Tick(); }
			catch (Exception e) { TickError("Player hold", e); }
			try { CreatureSpawner.Tick(); }
			catch (Exception e) { TickError("Creatures", e); }
			try { GapFeatures.Tick(); }
			catch (Exception e) { TickError("Gap features", e); }
			try { RaftColliderGuard.Tick(); }
			catch (Exception e) { TickError("Raft colliders", e); }
			try { QuestTracker.Tick(); QuestCount.Tick(); DailyQuest.Tick(); RogueShark.Tick(); StormDays.Tick(); }
			catch (Exception e) { TickError("Quests", e); }
			try { SaveSoon.Tick(); }
			catch (Exception e) { TickError("World save", e); }
			try { GlowSea.Tick(); }
			catch (Exception e) { TickError("Bioluminescent sea", e); }
			try { UfoSightings.Tick(); }
			catch (Exception e) { TickError("UFO sightings", e); }
			try { WhaleBreach.Tick(); }
			catch (Exception e) { TickError("Whale breach", e); }
			try { Kraken.Tick(); }
			catch (Exception e) { TickError("Kraken arm", e); }
			try { LightningStrikes.Tick(); }
			catch (Exception e) { TickError("Lightning strikes", e); }
			try { GhostShip.Tick(); }
			catch (Exception e) { TickError("Ghost ship", e); }
			try { FogBanks.Tick(); }
			catch (Exception e) { TickError("Fog banks", e); }
			try { IslandInfo.Tick(); }
			catch (Exception e) { TickError("Island banner", e); }
			try { Behaviours.Tick(); }
			catch (Exception e) { TickError("Behaviours", e); }
			try { TriggerSounds.Tick(); StoryCredits.Tick(); }
			catch (Exception e) { TickError("Trigger sounds and credits", e); }
			try { IslandTest.Tick(); }
			catch (Exception e) { TickError("Island test", e); }
			try { StoryChain.WatchWorld(); }
			catch (Exception e) { TickError("Story chain", e); }
			try { MissingIslandsCheck.Tick(); }
			catch (Exception e) { TickError("Missing islands", e); }
			try { WorldDirector.Tick(); }
			catch (Exception e) { TickError("World director", e); }
			try { JournalWindow.Tick(); }
			catch (Exception e) { TickError("Journal", e); }
			try { if (LoadSceneManager.IsGameSceneLoaded && !InEditor()) QuestBook.Tick(); }
			catch (Exception e) { TickError("Quest book", e); }
			try { if (LoadSceneManager.IsGameSceneLoaded && !InEditor()) HotkeyHints.Tick(); }
			catch (Exception e) { TickError("Hotbar key tabs", e); }
			try { WorldRandomizer.Tick(); ScrambledBlueprints.Tick(); }
			catch (Exception e) { TickError("World randomizer", e); }
			try { Editor.ExtraUpgrades.Tick(); }
			catch (Exception e) { TickError("Extra upgrades", e); }
			try { PlayerLevels.Tick(); LevelWindow.Tick(); }
			catch (Exception e) { TickError("Levels", e); }
			try { EditorAutosave.Tick(); }
			catch (Exception e) { TickError("Autosave", e); }
			try { TickWaterPlane(); }
			catch (Exception e) { TickError("Sea plane", e); }
			try { Editor.RaftSkySea.Tick(); }
			catch (Exception e) { TickError("Raft's sky and sea", e); }
		}

		/// <summary>Messages sent with SendNetworkMessage arrive here (RML subscribes the mod to its own channel).</summary>
		public override bool OnNetworkMessage(object message, Network_UserId from, string modslug)
		{
			return IslandNetwork.OnMessage(message, from) || base.OnNetworkMessage(message, from, modslug);
		}

		/// <summary>The id of the mod's Harmony patches (OnModUnload takes off these, never another mod's).</summary>
		internal const string HarmonyId = "com.franzfischer.customislands3"; // (not the original mod's id: an Unload of one took off the other's patches)
		static Harmony harmonyInstance;

		/// <summary>
		/// RML's Unload (AU40): what the mod hooked into Raft and Unity is taken off again - its Harmony patches, its handlers
		/// on Raft's and Unity's static events (they kept calling the unloaded mod's code: after a Load, every world shift,
		/// world load and main menu ran twice, the second time on the old copy), its asset bundles (a Load after this loads
		/// them again, and Unity refuses a second copy of a loaded bundle: the editor didn't open) and Raft's own build costs.
		/// What can't be taken back cleanly - its windows, the islands and creatures already in this world, the open editor -
		/// stays until Raft restarts, so the player is told to restart Raft.
		/// </summary>
		public void OnModUnload()
		{
			UnloadStep("Raft's build costs", () => Editor.BuildCost.RestoreAll());
			UnloadStep("the extra upgrades", () => Editor.ExtraUpgrades.Unregister());
			UnloadStep("the patches", () =>
			{
				if (harmonyInstance == null) return;
				// (each patched method, only this mod's patches on it: UnpatchAll without an id took every mod's off)
				foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToList())
					harmonyInstance.Unpatch(original, HarmonyPatchType.All, HarmonyId);
				harmonyInstance = null;
			});
			UnloadStep("the error notes", Editor.ErrorNotes.Uninstall);
			UnloadStep("the world hooks", () =>
			{
				SceneManager.sceneLoaded -= OnSceneLoaded;
				SaveAndLoad.LoadComplete -= OnLoadComplete;
				QuestTracker.Advanced -= Behaviours.OnQuestAdvanced;
				WorldShiftManager.OnWorldShift -= onWorldShift;
				Raft_Network.OnWorldReceivedLate -= onWorldReceived;
				AtmosphereZone.Unhook();
				SaveSoon.Unhook();
			});
			UnloadStep("the editor's assets", () =>
			{
				// (false: what is loaded from them now - an open editor - stays until it is left)
				if (mainbundle != null) mainbundle.Unload(false);
				if (helperbundle != null) helperbundle.Unload(false);
				mainbundle = helperbundle = null;
			});
			// (RML destroys the mod's object; should it not, Update would hook Raft's events again every frame)
			enabled = false;
			Notify("Custom Islands is unloaded. Its windows and the custom islands already in this world stay until Raft restarts - restart Raft before loading the mod again.");
			Debug.Log("[CUSTOM ISLANDS] Mod Custom Islands has been unloaded (patches, event handlers and asset bundles taken off)");
		}

		/// <summary>One step of OnModUnload on its own: one that fails doesn't stop the others.</summary>
		static void UnloadStep(string what, Action step)
		{
			try { step(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Unloading: " + what + " failed: " + e.Message); }
		}



		[ConsoleCommand(name: "LoadEditor", docs: "Loads into the Editor via Command")]
		public static async void LoadEditor(string[] args)
		{
			if (instance.mainbundle == null) { Debug.LogError("[CUSTOM ISLANDS] The editor bundle is not loaded"); return; }
			// (from inside a world this would skip Raft's own leaving: no save, and the other players left hanging)
			if (LoadSceneManager.IsGameSceneLoaded)
			{
				Notify("Leave the world first (Esc > Main menu, which saves it), then click EDITOR in the main menu", true);
				return;
			}
			string[] scenePath = instance.mainbundle.GetAllScenePaths();
			string editorScene = scenePath.FirstOrDefault(p => Utils.SceneNameFromPath(p) == "Editor");
			if (editorScene == null) { Debug.LogError("[CUSTOM ISLANDS] The editor bundle has no Editor scene"); return; }
			// A loading box covers the screen until the editor is ready (EditorLoadingBox)
			EditorLoadingBox.Show("Opening the editor");
			try
			{
				await OpenEditor(editorScene);
			}
			finally
			{
				EditorLoadingBox.Hide();
				// (Raft's sky and sea kept from the menu but not taken into the editor - it didn't open: not left running)
				Editor.RaftSkySea.ReleaseHeld();
			}
		}

		/// <summary>The bundle scene's own canvases (its old screen of 2023): not drawn from the moment the scene is there,
		/// and switched off (switchOff) once the editor's own screen is built.</summary>
		static void HideOldCanvases(Scene scene, bool switchOff)
		{
			if (!scene.IsValid()) return;
			foreach (GameObject g in scene.GetRootGameObjects())
				foreach (Canvas c in g.GetComponentsInChildren<Canvas>(true))
				{
					c.enabled = false;
					if (switchOff && c.isRootCanvas) c.gameObject.SetActive(false);
				}
		}

		static async System.Threading.Tasks.Task OpenEditor(string editorScene)
		{
			string sceneName = Utils.SceneNameFromPath(editorScene);
			// (before the scene's first frame: its old screen never shows)
			UnityEngine.Events.UnityAction<Scene, LoadSceneMode> quiet = null;
			quiet = (s, m) => { if (s.name != sceneName) return; SceneManager.sceneLoaded -= quiet; HideOldCanvases(s, false); };
			SceneManager.sceneLoaded += quiet;
			// (the option "Raft's sky and sea": the menu's sky, ocean and their camera kept through the scene change - ROADMAP E1b)
			try { Editor.RaftSkySea.HoldFromMenu(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's sky and sea: " + e.Message); }
			SceneManager.LoadScene(editorScene, LoadSceneMode.Single);
			Scene scene = SceneManager.GetSceneByName(sceneName);
			while (!scene.isLoaded) await new WaitForSeconds(.1f);
			SceneManager.sceneLoaded -= quiet;
			HideOldCanvases(scene, false);
			// (before the editor's tools go on Camera.main: with Raft's sky and sea the menu's camera is the editor's camera)
			Editor.RaftSkySea.Adopt(scene);
			EditorLoadingBox.Status("Setting up the editor", 0.2f);
			await new WaitForSeconds(0.5f);

			RAPI.ToggleCursor(true);
			Camera mainCamera = Camera.main;
			mainCamera.gameObject.AddComponent<terraineditor>();
			mainCamera.gameObject.AddComponent<EditorCamera>(); // (Unity-style: right-drag look, WASD, middle-drag pan, Alt+drag orbit, wheel zooms to the cursor, F frames)

			// The editor's screen is built in code (EditorUI); the bundle's old canvas (toolbar, dropdown) is switched off
			try
			{
				// (every canvas of the bundle scene: toolbar, navbar, the old object list)
				HideOldCanvases(scene, true);
				var tabSelector = new GameObject("CustomIslandsTabs").AddComponent<TabSelector>();
				tabSelector.SelectedTab = TAB.TerrainEdit; // building an island starts with shaping land
				EditorUI.Setup(null, tabSelector);
			}
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Could not set up the editor UI: " + e); }

			//Load shaders (the transform gizmo needs them in Awake, so this must happen first)
			try
			{
				_shaders.Clear();
				foreach (UnityEngine.Object sh in instance.helperbundle.LoadAllAssets(typeof(Shader)))
				{
					_shaders.Add((Shader)sh);
				}
				Debug.Log("[CUSTOM ISLANDS] Editor shaders: " + string.Join(", ", _shaders.Select(s => s.name).ToArray()));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not load editor shaders: " + e); }

			// A fresh start every time the editor opens: the last visit's island name (Ctrl+S or Test saved the new island
			// over that file without asking), its sea level and its undo steps (Ctrl+Z replayed them) stay behind
			currentIslandName = UnnamedIsland;
			EditorWaterLevel = IslandFile.DefaultWaterLevel;
			CommandUndoRedo.UndoRedoManager.Clear();

			// The gizmo must exist before any object can be picked
			EditorGizmoHandler = Camera.main.gameObject.AddComponent<TransformGizmo>();

			CreateWaterLevelPlane();

			// Start above the middle of the (1000 x 1000) build area, looking down at it, rather than at the corner under water
			Vector3 buildCentre = new Vector3(500f, EditorWaterLevel, 500f);
			Camera.main.transform.position = buildCentre + new Vector3(0f, 60f, -120f);
			Camera.main.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

			// Islands window and generator (built on the editor's canvas, in the same style)
			try { IslandFilesWindow.Create(EditorUI.Canvas.transform, null); }
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Could not create the islands window: " + e); }
			try { GeneratorWindow.Create(EditorUI.Canvas.transform, null); }
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Could not create the generator window: " + e); }
			try { NoteEditorWindow.Create(EditorUI.Canvas.transform); ItemPickerWindow.Create(EditorUI.Canvas.transform); TextPromptWindow.Create(EditorUI.Canvas.transform); SoundPickerWindow.Create(EditorUI.Canvas.transform); QuestEditorWindow.Create(EditorUI.Canvas.transform); ChoiceWindow.Create(EditorUI.Canvas.transform); WorldPlanWindow.Create(EditorUI.Canvas.transform); MainStoryHelper.Create(EditorUI.Canvas.transform); PlanCheckWindow.Create(EditorUI.Canvas.transform); PlanProgressWindow.Create(EditorUI.Canvas.transform); LibraryExportWindow.Create(EditorUI.Canvas.transform); LibraryImportWindow.Create(EditorUI.Canvas.transform); BehaviourWindow.Create(EditorUI.Canvas.transform); StoryItemsWindow.Create(EditorUI.Canvas.transform); PiecesWindow.Create(EditorUI.Canvas.transform); PlacedListWindow.Create(EditorUI.Canvas.transform); MyIslandsWindow.Create(EditorUI.Canvas.transform); CameraSettingsWindow.Create(EditorUI.Canvas.transform); }
			catch (Exception e) { Debug.LogError("[CUSTOM ISLANDS] Could not create the note editor: " + e); }

			// (the loading box shows how far Raft's islands have been read for their objects)
			EditorLoadingBox.Status("Loading Raft's objects", 0.25f);
			await PlaceableCatalog.EnsureBuilt();
			EditorLoadingBox.Status("Almost ready", 1f);
			// Creature models seen in a world since the catalog was built replace their markers
			try { ContentCatalog.UpgradeMarkers(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Creature models: " + e.Message); }
			// The builder's saved object groups ("My groups")
			instance.StartCoroutine(GroupLibrary.RegisterAll());
			currentIslandProps = new Dictionary<string, string>();
			currentIslandTail = new Dictionary<string, byte[]>();
			// Raft's own ground textures are borrowed while the catalog loads its islands; a new island starts tropical,
			// at sea level
			currentElevation = 0f;
			SetEditorStyle(TerrainPainter.Tropical);
			SetEditorMixStyle(-1);
			EditorUI.RefreshIsland();

			try { Editor.RaftSkySea.OnEditorReady(); EditorUI.RefreshSkySea(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's sky and sea: " + e.Message); }
			try { EditorLighting.OnEditorOpened(); EditorUI.RefreshLight(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Editor light: " + e.Message); }
			Debug.Log("[CUSTOM ISLANDS] Editor ready. Console: SaveIsland <name>, LoadIsland <name>, ListIslands");
			// (work Raft closed on before it was saved: offered back)
			EditorAutosave.OnEditorReady();
			// (the island chosen in the start window: Open an island / Continue)
			EditorEntry.OnEditorReady();

			// Once per Raft version: find every object of Raft's other islands (runs in the background)
			if (!PlaceableCatalog.IndexIsCurrent) instance.StartCoroutine(PlaceableCatalog.EnsureIndex());
		}

		/// <summary>The name a new island has until it is saved with its own (Ctrl+S and Test ask for a name then).</summary>
		public const string UnnamedIsland = "myisland";
		public static bool IsUnnamed { get { return string.IsNullOrEmpty(currentIslandName) || currentIslandName.Equals(UnnamedIsland, StringComparison.OrdinalIgnoreCase); } }

		/// <summary>Editor: starts an empty island (flat seabed, no objects, tropical, at sea level).</summary>
		/// <summary>
		/// The editor's own build area (1000 x 600 x 1000 m, 513 heights) again: an island opened before - a small one, or at
		/// a lower resolution - left its own size, and New or Generate then worked in that. True when it was changed.
		/// </summary>
		public static bool ResetBuildArea()
		{
			terraineditor editor = FindObjectOfType<terraineditor>();
			TerrainData data = terraineditor.terrain != null ? terraineditor.terrain.terrainData : null;
			if (editor == null || data == null || (data.heightmapResolution == editor.heightmapResolution && data.size == editor.terrainSize)) return false;
			data.heightmapResolution = editor.heightmapResolution; // (resolution first: changing it afterwards rescales the size)
			data.size = editor.terrainSize;
			return true;
		}

		public static void NewIsland()
		{
			if (!InEditor()) return;
			KeepUnsaved();
			Terrain terrain = terraineditor.terrain;
			TerrainData data = terrain.terrainData;
			if (EditorGizmoHandler != null) EditorGizmoHandler.ClearTargets(false);
			ResetBuildArea();
			data.SetHeights(0, 0, new float[data.heightmapResolution, data.heightmapResolution]);
			CaveGround.EditorForget(); // (the old island's ground let down in its caves: gone with it)
			foreach (Transform child in GameObject.Find("PlacedObjects").transform) Destroy(child.gameObject);
			currentIslandName = UnnamedIsland;
			currentElevation = 0f;
			currentIslandProps = new Dictionary<string, string>();
			currentIslandTail = new Dictionary<string, byte[]>();
			SetEditorStyle(TerrainPainter.Tropical);
			SetEditorMixStyle(-1);
			terraineditor.paintMask = new float[data.alphamapResolution, data.alphamapResolution];
			SetEditorWaterLevel(IslandFile.DefaultWaterLevel);
			TerrainPainter.Setup(terrain, EditorWaterLevel);
			CommandUndoRedo.UndoRedoManager.Clear();
			EditorAutosave.Opened(currentIslandName, false);
			EditorUI.RefreshIsland();
			Notify("New island: shape the land on the Terrain tab, then place objects");
		}

		#region Save / load (.island files in Mods\DynamicIslands)

		static bool IsValidIslandName(string name)
		{
			return FileNames.IslandProblem(name) == null;
		}

		/// <summary>A message for the player (Raft's notification, seconds long), also in the log.</summary>
		internal static void Notify(string text, bool error = false, int seconds = 4)
		{
			if (error) Debug.LogWarning("[CUSTOM ISLANDS] " + text); else Debug.Log("[CUSTOM ISLANDS] " + text);
			try
			{
				FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, text, seconds, error ? HNotify.ErrorSprite : HNotify.CheckSprite);
			}
			catch { }
		}

		public static bool InEditor()
		{
			if (terraineditor.terrain == null) return false;
			// (the editor's object root, found once per editor scene: ROADMAP P3 - GameObject.Find several times a frame before)
			if (placedRoot == null) placedRoot = GameObject.Find("PlacedObjects");
			return placedRoot != null;
		}

		static GameObject placedRoot;

		[ConsoleCommand(name: "SaveIsland", docs: "Editor: saves the island. Usage: SaveIsland <name>  (no name = current island)")]
		public static void SaveIslandCommand(string[] args)
		{
			SaveIsland(args != null && args.Length > 0 ? string.Join(" ", args) : currentIslandName);
		}

		[ConsoleCommand(name: "LoadIsland", docs: "Editor: loads a saved island. Usage: LoadIsland <name>")]
		public static void LoadIslandCommand(string[] args)
		{
			LoadIsland(args != null && args.Length > 0 ? string.Join(" ", args) : currentIslandName);
		}

		[ConsoleCommand(name: "DeleteGroup", docs: "Editor: deletes a saved object group (Mods\\DynamicIslands\\groups). Usage: DeleteGroup <name>")]
		public static void DeleteGroupCommand(string[] args)
		{
			string name = args != null ? string.Join(" ", args) : "";
			if (name.Length == 0) { Notify("Usage: DeleteGroup <name>   Groups: " + string.Join(", ", GroupLibrary.Saved().ToArray()), true); return; }
			bool deleted = GroupLibrary.Delete(name);
			Notify(deleted ? "Deleted group '" + name + "'" : "No group called '" + name + "'", !deleted);
		}

		[ConsoleCommand(name: "ListIslands", docs: "Lists saved islands (.island files in Mods\\DynamicIslands)")]
		public static void ListIslandsCommand()
		{
			Debug.Log("[CUSTOM ISLANDS] Saved islands: " + string.Join(", ", IslandSpawner.ListSavedIslands().ToArray()));
		}

		/// <summary>The island in the editor as a file (not written yet).</summary>
		internal static IslandFile CaptureIsland(string name)
		{
			IslandFile island = IslandFile.Capture(name, terraineditor.terrain, GameObject.Find("PlacedObjects").transform, terraineditor.paintMask);
			island.Elevation = currentElevation;
			island.Tail = new Dictionary<string, byte[]>(currentIslandTail);
			island.Style = currentStyle == TerrainPainter.Tropical ? "" : TerrainPainter.StyleName(currentStyle);
			island.MixStyle = currentMixStyle >= 0 ? TerrainPainter.StyleName(currentMixStyle) : "";
			island.Props = new Dictionary<string, string>(currentIslandProps);
			return island;
		}

		public static bool SaveIsland(string name) { return SaveIsland(name, false); }

		/// <summary>offerVersions (the Save button, Ctrl+S, Save as): saved worlds on an older copy of it get the choice of this version (R1c).</summary>
		public static bool SaveIsland(string name, bool offerVersions)
		{
			if (!InEditor()) { Notify("SaveIsland only works inside the editor", true); return false; }
			if (!IsValidIslandName(name)) { Notify("Can't save '" + name + "': " + FileNames.IslandProblem(name), true); return false; }
			try
			{
				IslandFile island = CaptureIsland(name);
				// (a file with more objects couldn't be opened again)
				if (island.Objects.Count > IslandFile.MaxObjects) { Notify("Can't save: " + island.Objects.Count + " objects, at most " + IslandFile.MaxObjects + " fit in an island file - delete some first", true); return false; }
				bool overwrote = File.Exists(IslandSpawner.PathFor(name));
				IslandFile before = null;
				if (overwrote) try { before = IslandFile.Load(IslandSpawner.PathFor(name)); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reading the saved '" + name + "' before saving over it: " + e.Message); }
				// (every object keeps its number - R1b; one made again where the same object stood gets that one's)
				StableIds.Prepare(island, before);
				// (saved worlds with this island: a change that would mix up what was used there keeps them on their version)
				bool carry = false;
				bool kept = before != null && KeepForWorldsIfShifted(name, island, before, out carry);
				island.Save(IslandSpawner.PathFor(name));
				if (kept && carry) kept = !CarryWorlds(name);
				currentIslandName = name;
				EditorUI.RefreshIsland();
				Notify("Saved island '" + name + "' (" + island.Objects.Count + " objects)");
				EditorAutosave.Saved(name);
				if (overwrote && !kept) TellWorldsUsing(name);
				if (overwrote && offerVersions) OfferThisVersion(name, kept);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Saving failed: " + e);
				if (SafeFile.InUse(e)) Notify("Can't save '" + name + "': its file is in use by another program - close it there and save again (the saved island is as it was)", true);
				else Notify("Saving '" + name + "' failed - see console (F10)", true);
				return false;
			}
		}

		/// <summary>
		/// A saved world remembers what was used on an island by the objects' order in its file (trees and pickups, chests,
		/// zones, creatures, doors, journal pages). Moving objects, changing their settings or the ground, or adding objects
		/// at the end keeps that order: those worlds get the new version. Removing objects, or changing the order or which
		/// ones are chests, would put what was used there onto other objects - so those worlds keep the version they started
		/// with (a copy named &lt;island&gt;_&lt;its hash&gt;, which they play by the hash in their file, as for library updates), and
		/// new worlds get the new one. True when the copy was kept.
		/// </summary>
		internal static bool KeepForWorldsIfShifted(string name, IslandFile next)
		{
			bool carry;
			return KeepForWorldsIfShifted(name, next, IslandFile.Load(IslandSpawner.PathFor(name)), out carry);
		}

		/// <summary>carry: only objects moved (R1b) - the copy is kept, and CarryWorlds gives the worlds the new file once saved.</summary>
		internal static bool KeepForWorldsIfShifted(string name, IslandFile next, IslandFile before, out bool carry)
		{
			carry = false;
			try
			{
				List<string> worlds = LibraryPack.WorldsUsing(name);
				if (worlds.Count == 0) return false;
				if (!ShiftsState(before, next)) return false;
				string hash = IslandNetwork.HashOf(name);
				if (hash == null) return false;
				string copy = IslandSpawner.PathFor(IslandNetwork.DownloadName(name, hash));
				// (whole or not at all: a copy Raft stopped in was never written again, and the worlds were pointed at it)
				if (!File.Exists(copy)) SafeFile.WriteAllBytes(copy, File.ReadAllBytes(IslandSpawner.PathFor(name)));
				LibraryPack.RepointWorlds(name, hash);
				carry = !ListsShift(before, next);
				if (carry) { Debug.Log("[CUSTOM ISLANDS] Kept '" + name + "' " + hash + " for " + worlds.Count + " saved world(s) until their state is carried"); return true; }
				string list = string.Join(", ", worlds.Take(3).Select(w => "'" + w + "'").ToArray()) + (worlds.Count > 3 ? " and " + (worlds.Count - 3) + " more" : "");
				Notify("Saved worlds with '" + name + "' (" + list + ") keep the version they started with: objects, rules or quest steps were removed or their order changed, which would mix up what was " +
					"picked, looted, opened or done there. New worlds get this version.");
				Debug.Log("[CUSTOM ISLANDS] Kept '" + name + "' " + hash + " for " + worlds.Count + " saved world(s)");
				return true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Keeping the old version of '" + name + "' for saved worlds: " + e.Message); return false; }
		}

		/// <summary>
		/// R1b: after a save that removed objects or changed their order (but kept the rules and quest steps): the saved
		/// worlds get the new file, with what was picked, looted, opened or done there carried to the same objects (what
		/// belonged to removed objects is left out). Worlds whose state can't be carried now (an object of Raft's other
		/// islands not loaded) keep the copy. True when none kept it.
		/// </summary>
		static bool CarryWorlds(string name)
		{
			try
			{
				List<string> moved;
				int carried;
				List<string> changed = LibraryPack.GiveWorldsThisVersion(name, out moved, true, out carried);
				List<string> left = LibraryPack.WorldsOnOlderVersion(name);
				Func<List<string>, string> names = w => string.Join(", ", w.Take(3).Select(x => "'" + x + "'").ToArray()) + (w.Count > 3 ? " and " + (w.Count - 3) + " more" : "");
				if (changed.Count > 0)
					Notify("Saved worlds with '" + name + "' (" + names(changed) + ") play this version: what was picked, looted, opened or done there stays with the same objects " +
						"(objects removed take theirs with them).");
				if (left.Count > 0)
					Notify("Saved worlds with '" + name + "' (" + names(left) + ") keep the version they started with: what was used there can't be moved to the new objects now. New worlds get this version.");
				return left.Count == 0;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Giving saved worlds the new '" + name + "': " + e.Message); return false; }
		}

		/// <summary>Islands whose "Give my worlds this version" box was shown in this Raft session without a copy just kept (once each).</summary>
		static readonly HashSet<string> offeredVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// R1c: after a save, saved worlds that play an older copy of the island (kept now, or at an earlier save or library
		/// update) can be given this version - a box with "Give my worlds this version" and "Keep their version". Shown when
		/// a copy was just kept, otherwise once per island in a Raft session (not at every Ctrl+S).
		/// </summary>
		static void OfferThisVersion(string name, bool justKept)
		{
			try
			{
				if (!justKept && offeredVersions.Contains(name)) return;
				List<string> worlds = LibraryPack.WorldsOnOlderVersion(name);
				if (worlds.Count == 0) return;
				offeredVersions.Add(name);
				string list = string.Join(", ", worlds.Take(6).Select(w => "'" + w + "'").ToArray()) + (worlds.Count > 6 ? " and " + (worlds.Count - 6) + " more" : "");
				InfoWindow.Open("Saved worlds keep an older '" + name + "'",
					"These saved worlds play an older version of '" + name + "' (a copy kept for them, " + name + "_<hash>): " + list + ".\n\n" +
					(justKept ? "This save removed objects, rules or quest steps or changed their order, so they were kept on the version they started with. " : "") +
					"<b>Give my worlds this version</b> makes them play the island as it is now and moves the old copies to Mods\\DynamicIslands\\" +
					IslandFilesWindow.DeletedFolderName + "\\" + LibraryPack.KeptVersionsFolder + ". What was picked, looted or opened there stays with the same objects " +
					"(what belonged to removed objects is left out) - but if the rules or a quest's steps were removed or reordered, the fired rules and quest steps reached may land on other ones. <b>Keep their version</b> changes nothing (also later in My islands).",
					new InfoWindow.Choice("Give my worlds this version", () =>
					{
						try
						{
							List<string> moved;
							List<string> changed = LibraryPack.GiveWorldsThisVersion(name, out moved);
							string text = LibraryPack.GaveText(name, changed, moved);
							InfoWindow.SetStatus(changed.Count + " world" + (changed.Count == 1 ? "" : "s") + " changed");
							InfoWindow.Close();
							Notify(text, false, 8);
						}
						catch (Exception e) { InfoWindow.SetStatus(SafeFile.InUse(e) ? "A world file is in use by another program - close it there and try again" : "Could not change them: " + e.Message); }
					}, "Saved worlds with an older copy of this island play it as it is now", true),
					new InfoWindow.Choice("Keep their version", InfoWindow.Close, "Change nothing: they keep playing the copy (Esc)"));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Offering this version of '" + name + "' to saved worlds: " + e.Message); }
		}

		/// <summary>Whether saved state would land on other objects, rules or quest steps: the old objects aren't an unchanged
		/// beginning of the new ones - nor the island's rules, nor each quest's steps, which a world also keeps by their place
		/// (a fired rule, the step reached - AU23: a removed rule made the next one count as fired, a removed step moved the quest).</summary>
		public static bool ShiftsState(IslandFile before, IslandFile after) { return ObjectsShift(before, after) || ListsShift(before, after); }

		/// <summary>Whether the objects of the old file aren't all at the beginning of the new one, in order (ShiftsState).</summary>
		public static bool ObjectsShift(IslandFile before, IslandFile after)
		{
			Func<IslandObject, string> sig = o => o.Name + (ObjectProps.IsLoot(o.Name, o.Props) ? "|loot" : "");
			if (after.Objects.Count < before.Objects.Count) return true;
			for (int i = 0; i < before.Objects.Count; i++) if (sig(before.Objects[i]) != sig(after.Objects[i])) return true;
			return false;
		}

		/// <summary>Whether the island's rules or a quest's steps aren't all kept at their places (ShiftsState).</summary>
		public static bool ListsShift(IslandFile before, IslandFile after)
		{
			Func<List<string>, List<string>, bool> notPrefix = (was, now) => now.Count < was.Count || Enumerable.Range(0, was.Count).Any(i => was[i] != now[i]);
			Func<IslandFile, List<string>> rules = f => WorldDirector.RulesFromProps(f.Props).Select(r => r.When + "|" + r.What).ToList();
			if (notPrefix(rules(before), rules(after))) return true;
			for (int n = 0; n < IslandQuest.MaxQuests; n++)
			{
				Func<IslandFile, List<string>> steps = f => IslandQuest.From(f.Props, n, true).Steps.Select(st => st.Type + "|" + st.Target).ToList();
				if (notPrefix(steps(before), steps(after))) return true;
			}
			return false;
		}

		/// <summary>Islands whose saved worlds the player was told about in this Raft session (once each, not at every save).</summary>
		static readonly HashSet<string> toldWorldsUsing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>How often it was said (tests).</summary>
		internal static int WorldsNoticeCount;

		/// <summary>
		/// Saving over an island that saved worlds have: those worlds get this version the next time they load (so a fixed
		/// quest works there), and a base built on its ground may no longer fit if the ground was changed - said once.
		/// </summary>
		static void TellWorldsUsing(string name)
		{
			if (toldWorldsUsing.Contains(name)) return;
			List<string> worlds = LibraryPack.WorldsUsing(name);
			if (worlds.Count == 0) return;
			toldWorldsUsing.Add(name);
			WorldsNoticeCount++;
			string list = string.Join(", ", worlds.Take(3).Select(w => "'" + w + "'").ToArray()) + (worlds.Count > 3 ? " and " + (worlds.Count - 3) + " more" : "");
			Notify("Saved worlds with '" + name + "' (" + list + ") get this version when they load next. If you moved its ground, anything built on it there may no longer fit.");
		}

		/// <summary>Another island replaces the one in the editor (New, Open, a map type's Make): its unsaved changes are kept
		/// as its autosave first (offered the next time the editor opens) - they were thrown away.</summary>
		internal static void KeepUnsaved()
		{
			if (!EditorAutosave.Unsaved) return;
			string was = currentIslandName;
			if (EditorAutosave.WriteNow()) Notify("The unsaved changes to '" + was + "' are kept: the editor offers them back the next time it opens");
		}

		/// <summary>Opens a saved island in the editor (from: another file to read it from, e.g. its autosave; it keeps the name).</summary>
		public static bool LoadIsland(string name, string from = null)
		{
			if (!InEditor()) { Notify("LoadIsland only works inside the editor", true); return false; }
			string path = from ?? IslandSpawner.PathFor(name);
			if (!File.Exists(path)) { Notify("No saved island named '" + name + "'", true); return false; }
			if (!PlaceableCatalog.IsBuilt) { Notify("Objects are still loading, try again in a moment", true); return false; }
			if (from == null) KeepUnsaved();
			try
			{
				IslandFile island = IslandFile.Load(path);

				// Objects from Raft's other islands load first (their island scenes), then the island loads
				List<string> scenes = PlaceableCatalog.ScenesNeededFor(island.Objects.Select(o => o.Name));
				if (scenes.Count > 0)
				{
					Notify("Loading objects from " + string.Join(", ", scenes.Select(PlaceableCatalog.SceneLabel).ToArray()) + " for '" + name + "'...");
					instance.StartCoroutine(LoadAfter(PlaceableCatalog.EnsureLoaded(island.Objects.Select(o => o.Name).ToList()), name, from));
					return true;
				}

				Terrain terrain = terraineditor.terrain;
				if (terrain.terrainData.heightmapResolution != island.HeightmapResolution || terrain.terrainData.size != island.TerrainSize)
				{
					terrain.terrainData.heightmapResolution = island.HeightmapResolution;
					terrain.terrainData.size = island.TerrainSize;
				}
				terrain.terrainData.SetHeights(0, 0, island.Heights);
				CaveGround.EditorForget(); // (the old island's ground let down in its caves: gone with it)
				SetEditorWaterLevel(island.WaterLevel);
				SetEditorStyle(TerrainPainter.StyleIndex(island.Style)); // before painting, so the right textures go on
				SetEditorMixStyle(island.HasMix ? TerrainPainter.StyleIndex(island.MixStyle) : -1);
				if (island.HasPaint)
				{
					TerrainPainter.ApplySaved(terrain, island.GetAlphamapBlock(0, 0, island.AlphamapResolution));
					terraineditor.paintMask = island.GetPaintMask() ?? new float[island.AlphamapResolution, island.AlphamapResolution];
				}
				else
				{
					// Format 1 files have no paint: texture automatically
					terraineditor.paintMask = new float[terrain.terrainData.alphamapResolution, terrain.terrainData.alphamapResolution];
					TerrainPainter.Setup(terrain, island.WaterLevel);
				}

				// (the selection too, as New and Generate do: the gizmo stayed on the destroyed objects - Shift+click added to it, no handles)
				if (EditorGizmoHandler != null) EditorGizmoHandler.ClearTargets(false);
				Transform placed = GameObject.Find("PlacedObjects").transform;
				foreach (Transform child in placed) Destroy(child.gameObject);
				// PlacedObjects may not sit at the terrain origin; place relative to the terrain
				var holder = new GameObject("LoadedObjects");
				holder.transform.SetParent(placed, false);
				holder.transform.position = terrain.transform.position;
				int missing = IslandSpawner.SpawnObjects(island, holder.transform, true);

				currentIslandName = name;
				currentElevation = island.Elevation;
				currentIslandTail = new Dictionary<string, byte[]>(island.Tail ?? new Dictionary<string, byte[]>());
				currentIslandProps = new Dictionary<string, string>(island.Props);
				// Undo steps refer to the terrain/objects that were just replaced
				CommandUndoRedo.UndoRedoManager.Clear();
				EditorAutosave.Opened(name, from != null);
				EditorUI.RefreshIsland();
				Notify("Loaded island '" + name + "'" + (missing > 0 ? " - " + missing + " object(s) this Raft version doesn't have are red boxes, kept as they were when you save" : ""), missing > 0);
				return true;
			}
			catch (Exception e)
			{
				// (the reason on the screen: a player can't do anything with "see the console")
				string why = e is InvalidDataException ? e.Message.Replace(path, "the file").Replace(IslandSpawner.PathFor(name), "the file")
					: e is EndOfStreamException || e is ICSharpCode.SharpZipLib.SharpZipBaseException ? "the file is damaged (cut short or not an island)"
					: e is UnauthorizedAccessException || e is IOException ? "the file can't be read (" + e.Message + ")"
					: "see the console (F10)";
				Debug.LogWarning("[CUSTOM ISLANDS] Loading '" + name + "' failed: " + e.Message);
				Notify("Can't open '" + name + "': " + why, true);
				return false;
			}
		}

		static IEnumerator LoadAfter(IEnumerator loading, string name, string from)
		{
			yield return loading;
			if (InEditor()) LoadIsland(name, from);
		}

		/// <summary>Semi-transparent plane showing where the sea will be when the island is spawned in game.</summary>
		static void CreateWaterLevelPlane()
		{
			try
			{
				GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
				plane.name = "WaterLevel";
				Destroy(plane.GetComponent<Collider>()); // must not block terrain raycasts
				Vector3 size = terraineditor.terrain != null ? terraineditor.terrain.terrainData.size : new Vector3(1000, 600, 1000);
				plane.transform.position = new Vector3(size.x / 2f, EditorWaterLevel, size.z / 2f);
				waterPlane = plane;
				planeFor = float.NaN;
				plane.transform.localScale = new Vector3(size.x / 10f, 1, size.z / 10f); // Unity plane is 10x10
				Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
				if (shader != null)
				{
					plane.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.1f, 0.45f, 0.8f, 0.35f) };
					// (seen from below too: a sunken island's sea is above it - ROADMAP E2; Unity's plane has one side)
					GameObject under = GameObject.CreatePrimitive(PrimitiveType.Plane);
					under.name = "WaterLevelUnder";
					Destroy(under.GetComponent<Collider>());
					under.transform.SetParent(plane.transform, false);
					under.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
					under.GetComponent<Renderer>().sharedMaterial = plane.GetComponent<Renderer>().sharedMaterial;
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not create water level plane: " + e); }
		}

		#endregion

		public static void PlaceObject(string objectName)
		{
			//Only one object can be in "placing" mode at a time
			ObjectPlacer existing = ObjectPlacer.Current;
			if (existing != null) Destroy(existing.gameObject);

			GameObject NewObjectToPlace = PlaceableCatalog.Spawn(objectName, null);
			if (NewObjectToPlace == null) { Debug.LogWarning("[CUSTOM ISLANDS] Unknown object " + objectName); return; }
			NewObjectToPlace.AddComponent<ObjectPlacer>().GameObjectName = objectName;
		}

		#region Spawning editor islands (.island) in a world

		/// <summary>Distance in front of the raft where SpawnIsland places the island's land. Raft's camera only renders to 400 m.</summary>
		const float SpawnDistance = 250f;

		[ConsoleCommand(name: "SpawnIsland", docs: "Host, in game: spawns a saved editor island in front of the raft. Usage: SpawnIsland <name> [distance in m, default 250] [height above sea in m, default the island's own elevation]")]
		public static void SpawnIslandCommand(string[] args)
		{
			if (args == null || args.Length == 0) { Notify("Usage: SpawnIsland <name> [distance] [height]   (ListIslands shows saved islands)", true); return; }
			// Trailing numbers: distance, then height (island names may contain spaces)
			var numbers = new List<float>();
			float parsed;
			while (args.Length > 1 && numbers.Count < 2 && float.TryParse(args[args.Length - 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
			{
				numbers.Insert(0, parsed);
				args = args.Take(args.Length - 1).ToArray();
			}
			float distance = numbers.Count > 0 ? Mathf.Clamp(numbers[0], 20f, 390f) : SpawnDistance;
			float? height = numbers.Count > 1 ? Mathf.Clamp(numbers[1], IslandSpawner.MinElevation, IslandSpawner.MaxElevation) : (float?)null;
			if (!LoadSceneManager.IsGameSceneLoaded) { Notify("You need to be in a game to spawn an island", true); return; }
			if (!Raft_Network.IsHost) { Notify("Only the host can spawn islands", true); return; }

			Raft raft = FindObjectOfType<Raft>();
			Vector3 origin = raft != null ? raft.transform.position : Vector3.zero;
			Vector3 dir = Raft.direction.sqrMagnitude > 0.01f ? Raft.direction.normalized : Vector3.forward;
			Vector3 position = origin + new Vector3(dir.x, 0, dir.z).normalized * distance;
			string name = string.Join(" ", args);
			// The island's y is its elevation above sea level (0 = a normal island)
			position.y = height ?? IslandSpawner.ElevationOf(name);
			// On top of one of Raft's own islands, players can fall through the ground: say so (the automatic spawner avoids it)
			string overlap = CustomIslandSpawner.OverlapsRaftIsland(position, CustomIslandSpawner.LandRadius(name));
			if (overlap != null) Notify("Careful: " + overlap + " - the islands overlap; try another distance or direction", true);
			// (the raft itself: an island over it traps it - AU68)
			float land = CustomIslandSpawner.LandRadius(name);
			float gap = new Vector2(position.x - origin.x, position.z - origin.z).magnitude - land - CustomIslandSpawner.RaftRadius;
			if (land > 0f && gap < 10f) Notify("Careful: the island reaches " + (gap < 0f ? "over the raft" : "within " + gap.ToString("F0") + " m of the raft") + " - try a larger distance (SpawnIsland " + name + " " + Mathf.CeilToInt(land + CustomIslandSpawner.RaftRadius + 30f) + ")", true);

			instance.StartCoroutine(instance.SpawnIslandFile(name, position, true));
		}

		/// <param name="broadcast">host spawning a new island: tell clients and remember it in the world's island list</param>
		/// <param name="entry">host (re)loading an island that is already in the world's island list (automatic
		/// spawns and streaming): its Root is set once spawned, and nothing is shown to the player</param>
		static readonly Dictionary<string, KeyValuePair<float, int>> tickErrors = new Dictionary<string, KeyValuePair<float, int>>();

		/// <summary>
		/// An error in one of the per-frame ticks (ROADMAP AU37: logged every frame, Player.log grew by hundreds of MB an
		/// hour): the first in full, then once a minute how many more there were.
		/// </summary>
		static void TickError(string what, Exception e)
		{
			KeyValuePair<float, int> seen;
			if (!tickErrors.TryGetValue(what, out seen)) { tickErrors[what] = new KeyValuePair<float, int>(Time.unscaledTime, 0); Debug.LogError("[CUSTOM ISLANDS] " + what + ": " + e); return; }
			if (Time.unscaledTime - seen.Key < 60f) { tickErrors[what] = new KeyValuePair<float, int>(seen.Key, seen.Value + 1); return; }
			Debug.LogError("[CUSTOM ISLANDS] " + what + ": " + (seen.Value + 1) + " more errors in the last minute, the latest: " + e.Message);
			tickErrors[what] = new KeyValuePair<float, int>(Time.unscaledTime, 0);
		}

		static readonly HashSet<string> remaking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		/// <summary>Tests (CIPerfChecks): how long the last island file took to read.</summary>
		internal static string LastLoadTiming = "";

		public IEnumerator SpawnIslandFile(string name, Vector3 position, bool broadcast, IslandWorldState.Entry entry = null)
		{
			bool quiet = entry != null;
			string path = IslandSpawner.PathFor(name);
			IslandFile island = null;
			// The file read and unpacked on a worker thread (ROADMAP P1: 150-170 ms on the main thread for a big island, a
			// stutter each time one streamed in)
			System.Threading.Tasks.Task<IslandFile> reading = null;
			bool remake = false;
			// (world shifts while the file is read count too - review 2026-10-06)
			Vector3 shiftedBefore = IslandWorldState.ShiftedBy;
			var lc = System.Diagnostics.Stopwatch.StartNew();
			// (a world's island tidied up or deleted: its copy in the deleted folder first - a player's copy only with the host's hash)
			if (entry != null && !File.Exists(path) && (Raft_Network.IsHost || IslandNetwork.IsHash(entry.Hash)) && IslandFilesWindow.RestoreFromDeleted(name, entry.Hash))
				Notify("'" + name + "' was put back from the deleted folder for this world", false);
			if (File.Exists(path))
			{
				SafeFile.Recover(path); // (a save that Raft stopped half way: here, not on the worker)
				reading = System.Threading.Tasks.Task.Run(() => IslandFile.LoadOffThread(path));
				while (!reading.IsCompleted) yield return null;
			}
			try
			{
				if (reading != null) { island = reading.Result; LastLoadTiming = "reading the file " + lc.ElapsedMilliseconds + " ms (on a worker thread)"; }
				// (a generated island of the world whose file was deleted: made again from its name - ROADMAP R15)
				else if (entry != null && Raft_Network.IsHost && CustomIslandSpawner.RemakeOf(name) != null && !remaking.Contains(name)) { remaking.Add(name); remake = true; }
				// (one of the world's islands: said which, and that the rest plays - a player hosting a world they got as a
				// folder, without ever joining it, has none of the islands made on the other PC)
				else if (entry != null) Notify("This world's island '" + entry.HostName + "' isn't on this PC, so it is left out - the rest of the world plays. " +
					"It comes once you have it: join a game where a player who has it hosts this world, or import it.", true);
				else Notify("No saved island named '" + name + "' in " + assetpath, true);
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Could not read " + path + ": " + e);
				Notify("Could not read island '" + name + "' - see console (F10)", true);
			}
			// (this spawn's remake only: a second spawn of the name while it is remade doesn't start another - review 2026-10-06)
			if (island == null && remake)
			{
				yield return CustomIslandSpawner.RemakeAndSpawn(CustomIslandSpawner.RemakeOf(name), entry);
				remaking.Remove(name);
				yield break;
			}
			if (island == null)
			{
				if (entry != null) { entry.Loading = false; entry.Failed = true; }
				yield break;
			}

			// The core objects, plus any from Raft's other islands this island uses
			yield return PlaceableCatalog.EnsureLoaded(island.Objects.Select(o => o.Name).ToList());

			if (entry != null)
			{
				// Removed while loading, or the world changed
				if (!IslandWorldState.Contains(entry)) { entry.Loading = false; yield break; }
				// Made meanwhile by another spawn of this entry (the streaming started one in the same frame an island was
				// loaded again): a second copy would stay in the world for good, the entry knowing only one of them
				if (entry.Root != null) { entry.Loading = false; Debug.Log("[CUSTOM ISLANDS] '" + entry.HostName + "' is there already - not made twice"); yield break; }
				position = entry.Position; // follows world shifts that happened meanwhile
			}
			// (no entry - SpawnIsland, the editor's Test: the world shifts meanwhile too, or it landed hundreds of metres off)
			else position -= IslandWorldState.ShiftedBy - shiftedBefore;

			// The objects over several frames (ROADMAP P1); the entry stays "loading" meanwhile, so nothing spawns it twice
			GameObject made = null;
			// (SpawnInWorldSliced catches its own errors and gives null: a coroutine's call never throws)
			Vector3 shiftedSlicing = IslandWorldState.ShiftedBy;
			yield return IslandSpawner.SpawnInWorldSliced(island, position, r => made = r);
			// (no entry: the root follows shifts while its objects are made - SpawnedRoots - the position kept for it too)
			if (entry == null) position -= IslandWorldState.ShiftedBy - shiftedSlicing;
			if (entry != null)
			{
				entry.Loading = false;
				// (removed, or made by another spawn, while its objects were being made)
				if (!IslandWorldState.Contains(entry) || entry.Root != null) { IslandSpawner.Despawn(made); yield break; }
			}
			if (made == null)
			{
				if (entry != null) entry.Failed = true;
				Notify("Spawning '" + name + "' failed - see console (F10)", true);
				yield break;
			}

			try
			{
				GameObject root = made;
				root.AddComponent<ReApplyShaders>();
				if (entry == null && Raft_Network.IsHost && broadcast) entry = IslandWorldState.Add(name, position, root);
				if (entry != null)
				{
					entry.Root = root;
					// (the host's list says which version the world plays: an island edited in the editor since the world was
					// saved kept its old hash there until the next save)
					if (Raft_Network.IsHost) entry.Hash = IslandNetwork.HashOf(entry.Name) ?? entry.Hash;
					IslandSpawner.RegisterNetworkIds(root, entry.Id);
					StableIds.CheckPickups(island, root, position.y > IslandSpawner.FlyingThreshold);
					IslandObjectState.Apply(entry, IslandRules.RegrowDays(entry));
					BuriedTreasure.OnIslandReady(entry);
					RaftStore.OnIslandReady(root, entry); // Raft's trading posts: network number, goods, what was sold
					RaftVending.OnIslandReady(root, entry); // Raft's vending machines: network number, the package in the hatch
					Behaviours.OnIslandReady(entry); // objects shown or hidden, doors open or closed, as saved
					ContentState.OnIslandReady(entry); // first: chests refill and zones re-arm before the creatures look at them
					CreatureSpawner.OnIslandReady(entry);
				}
				if (!quiet) Notify("Spawned island '" + name + "'");
			}
			catch (Exception e)
			{
				Debug.LogError("[CUSTOM ISLANDS] Spawning '" + name + "' failed: " + e);
				if (entry != null) entry.Failed = true;
				Notify("Spawning '" + name + "' failed - see console (F10)", true);
			}
		}

		[ConsoleCommand(name: "RemoveIsland", docs: "Host, in game: removes spawned custom islands. Usage: RemoveIsland <name>   or   RemoveIsland all")]
		public static void RemoveIslandCommand(string[] args)
		{
			if (args == null || args.Length == 0) { Notify("Usage: RemoveIsland <name> | all   (ListSpawned shows them)", true); return; }
			if (!Raft_Network.IsHost) { Notify("Only the host can remove islands", true); return; }
			string name = string.Join(" ", args);
			int n = IslandWorldState.Remove(name.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : name);
			Notify(n > 0 ? "Removed " + n + " island(s). They stay gone after the next save." : "No spawned island called '" + name + "'", n == 0);
		}

		[ConsoleCommand(name: "ListSpawned", docs: "Lists the custom islands spawned in this world (saved with the world)")]
		public static void ListSpawnedCommand()
		{
			if (IslandWorldState.Islands.Count == 0) { Debug.Log("[CUSTOM ISLANDS] No custom islands spawned in this world"); return; }
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			foreach (var e in IslandWorldState.Islands)
				Debug.Log("[CUSTOM ISLANDS] " + e.HostName + " at " + e.Position +
					(raftPos.HasValue ? ", " + Vector3.Distance(new Vector3(e.Position.x, 0, e.Position.z), new Vector3(raftPos.Value.x, 0, raftPos.Value.z)).ToString("F0") + " m from the raft" : "") +
					(e.Name != e.HostName ? " [file " + e.Name + "]" : "") +
					(e.Failed ? " (island file missing or broken)" : e.WaitingForFile ? " (waiting for the file from the host)" : e.Loading ? " (loading)" : e.Root == null ? " (unloaded: far away)" : ""));
		}

		[ConsoleCommand(name: "SpawnPool", docs: "Shows which islands appear on their own while sailing, and how often (edit Mods\\DynamicIslands\\spawnpool.txt to change)")]
		public static void SpawnPoolCommand()
		{
			// (a player in a host's world: the host's pool decides, this PC's spawnpool.txt is for worlds it hosts - ROADMAP M4)
			if (LoadSceneManager.IsGameSceneLoaded && !Raft_Network.IsHost)
			{
				Debug.Log("[CUSTOM ISLANDS] In this world the host's spawnpool.txt decides which islands come while sailing (yours counts when you host). " + WorldIslands.DescribeForPlayer());
				return;
			}
			foreach (string line in CustomIslandSpawner.Describe().Split('\n')) Debug.Log("[CUSTOM ISLANDS] " + line);
		}

		[ConsoleCommand(name: "Levels", docs: "The level up system in this world: Levels = on or off; Levels on / off = switch it for every player (host; off keeps everyone's levels for when it is on again)")]
		public static void LevelsCommand(string[] args)
		{
			string a = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";
			if (a.Length > 0)
			{
				// (the word is checked first: at the main menu any other word switched it off)
				if (a != "on" && a != "off") { Notify("Levels on or Levels off", true); return; }
				if (!LoadSceneManager.IsGameSceneLoaded)
				{
					// (the next new world, like the World settings window: remembered, and the window shows it)
					PlayerLevels.Chosen = a == "on";
					PlayerLevels.SaveDefault(PlayerLevels.Chosen);
					try { WorldSettingsWindow.Show(); } catch { }
					Notify("Level up system for the next new world: " + (PlayerLevels.Chosen ? "on" : "off"));
					return;
				}
				if (!Raft_Network.IsHost) { Notify("Only the host switches the level up system", true); return; }
				PlayerLevels.SetEnabled(a == "on");
			}
			Notify("Level up system in this world: " + (PlayerLevels.On ? "on" : PlayerLevels.OffByHost ? "off (switched off; levels kept)" : "off"));
		}

		[ConsoleCommand(name: "RegrowDays", docs: "This world's days until chopped trees, picked items, animals and looted chests come back (kept with the world; an island's own rule wins). RegrowDays = show it; RegrowDays <days> = change it (host; 0 = never)")]
		public static void RegrowDaysCommand(string[] args)
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Notify("In a world: the days are kept with each world (new worlds take spawnpool.txt's regrowDays)", true); return; }
			int d;
			if (args != null && args.Length > 0)
			{
				if (!Raft_Network.IsHost) { Notify("Only the host changes the world's regrow days", true); return; }
				if (!int.TryParse(args[0], out d) || d < 0) { Notify("RegrowDays <days> (0 = never)", true); return; }
				WorldRules.SetRegrow(d);
			}
			Notify("In this world things come back after " + (WorldRules.RegrowDays > 0 ? WorldRules.RegrowDays + " day(s)" : "never") + " (an island's own rule wins)");
		}

		[ConsoleCommand(name: "Resync", docs: "A player who joined: ask the host for its custom islands again (the list and any island file that hasn't come)")]
		public static void ResyncCommand()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Notify("Resync is for a player who joined a host's world", true); return; }
			IslandNetwork.Resync();
			Notify("Asking the host for its custom islands again");
		}

		[ConsoleCommand(name: "WorldPlan", docs: "The world plan: which islands this world gets, when and where. WorldPlan = show it and its rules; WorldPlan <name> = give this world another plan (host)")]
		public static void WorldPlanCommand(string[] args)
		{
			string name = args != null ? string.Join(" ", args).Trim() : "";
			if (name.Length > 0)
			{
				if (!LoadSceneManager.IsGameSceneLoaded) { Notify("You need to be in a world (the plan is per world); for a new world, choose it in the New Game box", true); return; }
				if (!Raft_Network.IsHost) { Notify("Only the host can change the world plan", true); return; }
				if (!WorldDirector.SetPlan(name, true)) { Notify("No world plan '" + name + "'. Plans: " + string.Join(", ", WorldPlan.All().ToArray()), true); return; }
				// (its story chain replaces the world's; what is done or unlocked stays)
				StoryChain.FromPlan(WorldDirector.Plan);
				IslandWorldState.Save();
				Notify("This world now follows the plan '" + WorldDirector.PlanName + "' (kept when the world is saved)");
			}
			foreach (string line in (Raft_Network.IsHost ? WorldDirector.Describe() : WorldDirector.DescribeForPlayer()).Split('\n')) Debug.Log("[CUSTOM ISLANDS] " + line);
		}

		[ConsoleCommand(name: "Randomizer", docs: "The world randomizer (chosen in the New Game box): Randomizer = what it does in this world; Randomizer off|light|normal|wild, Randomizer -part / +part (colours, animals, alphas, loot, finds, oddities, bosses, large) = change it for this world (host)")]
		public static void RandomizerCommand(string[] args)
		{
			if (args != null && args.Length > 0)
			{
				if (!LoadSceneManager.IsGameSceneLoaded) { Notify("You need to be in a world (the randomizer is per world); for a new world, choose it in the New Game box", true); return; }
				if (!Raft_Network.IsHost) { Notify("Only the host can change the randomizer", true); return; }
				RandomizerSettings s = WorldRandomizer.Current.Copy();
				foreach (string a in args)
				{
					string w = a.Trim().ToLowerInvariant();
					int level = Array.FindIndex(RandomizerSettings.LevelNames, n => n.Equals(w, StringComparison.OrdinalIgnoreCase));
					string part = w.TrimStart('+', '-');
					if (level >= 0) s.Level = level;
					else if ((w.StartsWith("+") || w.StartsWith("-")) && RandomizerSettings.Features.Contains(part))
					{
						if (w.StartsWith("-")) s.Disabled.Add(part); else s.Disabled.Remove(part);
					}
					else { Notify("Randomizer: unknown '" + a + "'. Use off|light|normal|wild and -part / +part (" + string.Join(", ", RandomizerSettings.Features) + ")", true); return; }
				}
				WorldRandomizer.Set(s);
				Notify("World randomizer in this world: " + WorldRandomizer.Current.Describe() + " (islands already looked at keep what they got)");
			}
			foreach (string line in WorldRandomizer.Describe().Split('\n')) Debug.Log("[CUSTOM ISLANDS] " + line);
		}

		[ConsoleCommand(name: "CustomIslandsAuto", docs: "Host: custom islands appear on their own while sailing in this world. Usage: CustomIslandsAuto on|off")]
		public static void CustomIslandsAutoCommand(string[] args)
		{
			if (args == null || args.Length == 0 || !(args[0].Equals("on", StringComparison.OrdinalIgnoreCase) || args[0].Equals("off", StringComparison.OrdinalIgnoreCase)))
			{
				Notify("Automatic islands are " + (CustomIslandSpawner.Enabled ? "on" : "off") + " in this world. Usage: CustomIslandsAuto on|off");
				return;
			}
			if (!LoadSceneManager.IsGameSceneLoaded) { Notify("You need to be in a game (the setting is per world)", true); return; }
			if (!Raft_Network.IsHost) { Notify("Only the host can change this", true); return; }
			CustomIslandSpawner.Enabled = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
			IslandWorldState.Save();
			Notify("Automatic islands " + (CustomIslandSpawner.Enabled ? "on" : "off") + " in this world (kept when the world is saved)");
		}

		#endregion


		// (The 2023 "UploadFileTest" stub that posted an island with a user name and password to a local test server is
		// gone: islands are shared as packs - LibraryPack - and the island library only ever downloads.)


		#region Commands

		[ConsoleCommand(name: "SetToRaise", docs: "Change Terrain Edit to raise the terrain")]
		public static void TerrainRaise()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Raise;
		}
		[ConsoleCommand(name: "SetToLower", docs: "Change Terrain Edit to lower the terrain")]
		public static void TerrainLower()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Lower;
		}
		[ConsoleCommand(name: "SetToFlatten", docs: "Change Terrain Edit to flatten the terrain")]
		public static void TerrainFlatten()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Flatten;
		}
		[ConsoleCommand(name: "SetToSmooth", docs: "Change Terrain Edit to smooth the terrain")]
		public static void TerrainSmooth()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Smooth;
		}
		[ConsoleCommand(name: "PaintTexture", docs: "Terrain brush paints a texture by hand: PaintTexture sand|grass|rock|seabed")]
		public static void PaintTexture(string[] args)
		{
			int layer = args != null && args.Length > 0 ? Array.FindIndex(TerrainPainter.LayerNames, n => n.Equals(args[0], StringComparison.OrdinalIgnoreCase)) : -1;
			if (layer < 0) { Debug.LogWarning("Usage: PaintTexture " + string.Join("|", TerrainPainter.LayerNames).ToLower()); return; }
			terraineditor.paintLayer = layer;
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.PaintLayer;
			Debug.Log("[CUSTOM ISLANDS] Painting " + TerrainPainter.LayerNames[layer]);
		}
		[ConsoleCommand(name: "SetElevation", docs: "Editor: metres above sea level this island floats at in game (e.g. 60 = a flying island, -25 = under water, 0 = normal). Saved with the island.")]
		public static void SetElevationCommand(string[] args)
		{
			float v;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || float.IsNaN(v))
			{ Notify("Elevation is " + currentElevation + " m. Usage: SetElevation <metres>  (60 = flying, -25 = under water, 0 = normal)"); return; }
			IslandSettingsUndo.Change(() => currentElevation = Mathf.Clamp(v, IslandSpawner.MinElevation, IslandSpawner.MaxElevation)); // (an undo step, like the Island tab's)
			EditorUI.RefreshIsland();
			Notify("Island elevation: " + IslandSpawner.DescribeElevation(currentElevation) + " (saved with the island)");
		}

		[ConsoleCommand(name: "SetStyle", docs: "Editor: island style - Tropical, Snowy, Desert, Forest or Volcanic (ground textures; saved with the island)")]
		public static void SetStyleCommand(string[] args)
		{
			string names = string.Join(", ", TerrainPainter.Styles.Select(s => s.Name).ToArray());
			if (args == null || args.Length == 0) { Notify("Style is " + TerrainPainter.StyleName(currentStyle) + ". Usage: SetStyle " + names); return; }
			int i = Array.FindIndex(TerrainPainter.Styles, s => s.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase));
			if (i < 0) { Notify("Unknown style '" + args[0] + "'. Styles: " + names, true); return; }
			if (!InEditor()) { Notify("SetStyle only works inside the editor", true); return; }
			IslandSettingsUndo.Change(() => SetEditorStyle(i)); // (an undo step, like the Island tab's)
			Notify("Island style: " + TerrainPainter.StyleName(i) + (TerrainPainter.HasStyle(i) ? "" : " (its textures aren't loaded; showing tropical)"));
		}

		[ConsoleCommand(name: "GenerateIsland", docs: "Editor: generates a random island (replaces the current one; Ctrl+Z undoes). Usage: GenerateIsland [seed] [size in m] [height in m] [roughness 0-1] [peaks] [objects 0-1] [Tropical|Snowy|Desert|Forest|Volcanic]")]
		public static void GenerateIslandCommand(string[] args)
		{
			if (!InEditor()) { Notify("GenerateIsland only works inside the editor", true); return; }
			var s = new IslandGenSettings { Seed = UnityEngine.Random.Range(1, 999999) };
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			float f; int i;
			if (args != null)
			{
				if (args.Length > 0 && int.TryParse(args[0], System.Globalization.NumberStyles.Integer, ci, out i)) s.Seed = i;
				if (args.Length > 1 && FiniteArg(args[1], out f)) s.Radius = f / 2f;
				if (args.Length > 2 && FiniteArg(args[2], out f)) s.Height = f;
				if (args.Length > 3 && FiniteArg(args[3], out f)) s.Roughness = f;
				if (args.Length > 4 && int.TryParse(args[4], System.Globalization.NumberStyles.Integer, ci, out i)) s.Peaks = i;
				if (args.Length > 5 && FiniteArg(args[5], out f)) s.ObjectDensity = f;
			}
			s.Style = args != null && args.Length > 6 ? TerrainPainter.StyleIndex(args[6]) : currentStyle;
			int n = IslandGenerator.GenerateInEditor(s);
			IslandGenerator.FrameCamera(s);
			Notify("Generated island " + s.Seed + " (" + n + " objects). Ctrl+Z undoes it.");
		}

		/// <summary>A number typed in the console: NaN and Infinity read as numbers too, and NaN gets through every clamp
		/// (GenerateIsland 1 NaN NaN threw an overflow) - those are left out, as if not given.</summary>
		static bool FiniteArg(string text, out float f)
		{
			return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f);
		}

		[ConsoleCommand(name: "SetToAutoPaint", docs: "Terrain brush returns painted areas to automatic texturing")]
		public static void TerrainAutoPaint()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.AutoPaint;
		}
		[ConsoleCommand(name: "ChangeWidth", docs: "Terrain brush diameter in metres, e.g. ChangeWidth 30")]
		public static void TerrainWidth(string[] args)
		{
			float value;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || float.IsNaN(value)) { Debug.LogWarning("Usage: ChangeWidth <metres>"); return; }
			terraineditor.brushRadius = Mathf.Clamp(value / 2f, terraineditor.MinRadius, terraineditor.MaxRadius);
			EditorUI.RefreshSliders();
			Debug.Log("[CUSTOM ISLANDS] Brush diameter: " + (terraineditor.brushRadius * 2f) + " m");
		}
		[ConsoleCommand(name: "ChangeStrength", docs: "Terrain brush speed in metres per second, e.g. ChangeStrength 4")]
		public static void TerrainStrength(string[] args)
		{
			float value;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || float.IsNaN(value)) { Debug.LogWarning("Usage: ChangeStrength <metres per second>"); return; }
			terraineditor.strength = Mathf.Clamp(value, terraineditor.MinStrength, terraineditor.MaxStrength);
			EditorUI.RefreshSliders();
			Debug.Log("[CUSTOM ISLANDS] Brush strength: " + terraineditor.strength + " m/s");
		}

		#endregion

	}

	//HARMONY PATCHES
	// (The old SceneLoader.LoadScenes postfix that auto-loaded every file in Mods\DynamicIslands as a JSON island
	//  was removed: islands are spawned with the SpawnIsland command until they join Raft's spawn pool.)

	#region MiscStuff

	public static class Utils
	{
		public static string SceneNameFromPath(string path)
		{
			string scenePathByBuildIndex = path;
			int num = scenePathByBuildIndex.LastIndexOf('/');
			string text = scenePathByBuildIndex.Substring(num + 1);
			int length = text.LastIndexOf('.');
			return text.Substring(0, length);
		}
	}

	//SHADER FIX
	/// <summary>
	/// Shaders of a spawned island's objects looked up again (asset bundle copies render pink otherwise). Each material once
	/// per session and each shader name once (ROADMAP P2: it ran Shader.Find for every material of every spawned island,
	/// though the materials are the shared prototypes' and were fixed already).
	/// </summary>
	public class ReApplyShaders : MonoBehaviour
	{
		static readonly HashSet<int> done = new HashSet<int>();
		static readonly Dictionary<string, Shader> found = new Dictionary<string, Shader>();
		/// <summary>Tests (CIPerfChecks): how many materials were fixed, and Shader.Find calls made, this session.</summary>
		internal static int Fixed, Finds;

		void Start()
		{
			foreach (Renderer rend in GetComponentsInChildren<Renderer>())
			{
				Material[] materials;
				try { materials = rend.sharedMaterials; } catch { continue; }
				foreach (Material m in materials)
				{
					if (m == null || !done.Add(m.GetInstanceID())) continue;
					try
					{
						string name = m.shader != null ? m.shader.name : null;
						if (string.IsNullOrEmpty(name)) continue;
						Shader s;
						// (a shader not found isn't kept as missing: it may come later, the material looked at again - review 2026-10-06)
						if (!found.TryGetValue(name, out s)) { s = Shader.Find(name); Finds++; if (s != null) found[name] = s; }
						if (s != null) { m.shader = s; Fixed++; }
						else done.Remove(m.GetInstanceID());
					}
					catch { }
				}
			}
		}
	}



	#endregion

}
