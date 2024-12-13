using HarmonyLib;
using RaftGame.Private;
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
using Newtonsoft.Json;
using System.Globalization;

namespace DynamicIslands
{
	public class landmarkBundle
	{
		public string name;
		public string path;
		public AssetBundle bundle;
	}

	public static class MyIslands
	{
		public const ChunkPointType Landmark_TestIsland = (ChunkPointType)100;
	}


	public static class ChunkPointTypeExtensions
	{
		public static ChunkPointType AddValue(this ChunkPointType type, string value)
		{
			return (ChunkPointType)Enum.Parse(typeof(ChunkPointType), value, true);
		}
	}

	[System.Serializable]
	public class IslandMessage : Message
	{
		public string[] Islandtoload;
	}

	public class DynamicIslands : Mod
	{
		public static List<landmarkBundle> landmarkBundles = new List<landmarkBundle>();
		static string assetpath = @"Mods\DynamicIslands\";
		public static LoadSceneManager loadSceneManagerinstance;

		public AssetBundle mainbundle;
		public AssetBundle helperbundle;


		public static List<GameObject> GlobalPrefabList = new List<GameObject>();

		public static List<Shader> _shaders = new List<Shader>();
		public static TransformGizmo EditorGizmoHandler;
		//ChunkPointType lol = MyIslands.TheBestIslandOfAllTime;

		public static DynamicIslands instance;



		#region IslandObjectDefinition

		List<GameObject> VasagatanDefinitions = new List<GameObject>();



		



		#endregion


		public void Start()
		{
			//Pushing notification for mod loading
			HNotification DynamicIslandsLoad = FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.spinning, "Loading Custom Islands...");

			//ChunkPointType newValue = MyIslands.Landmark_TestIsland.AddValue("Landmark_NewValue");

			instance = this;
			loadSceneManagerinstance = FindObjectOfType<LoadSceneManager>();
			var harmony = new Harmony("com.franzfischer.customislands");
			harmony.PatchAll();

			//INIT FOLDER
			if (!Directory.Exists(assetpath))
			{
				Directory.CreateDirectory(assetpath);
			}
			if (Directory.EnumerateFiles(assetpath).Count() == 0)
			{
				Debug.LogWarning("There are no custom Islands installed!");
			}

			if (GetEmbeddedFileBytes("editorsceneci.assets").Length == 0)
			{
				Debug.Log("embeddedfilebytes are null");
			}

			mainbundle = AssetBundle.LoadFromMemory(GetEmbeddedFileBytes("editorsceneci.assets"));
			helperbundle = AssetBundle.LoadFromMemory(GetEmbeddedFileBytes("maincustomislandsbundle.assets"));

			//Adding the Editor button to the main menu
			HookUI();

			DynamicIslandsLoad.Close();
			DynamicIslandsLoad = FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "Custom Islands has been loaded!", 5);
			Debug.Log("[CUSTOM ISLANDS] Mod Custom Islands has been loaded successfully!");
		}

		private void HookUI()
		{
			GameObject MainMenuParent = GameObject.Find("MainMenuCanvas");

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
				ModpacksButton.transform.SetSiblingIndex(3);
				Debug.Log("namebutton: " + ModpacksButton.name);
				ModpacksButton.GetComponentInChildren<Text>().text = "EDITOR";
				ModpacksButton.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();

				ModpacksButton.GetComponent<Button>().onClick.RemoveAllListeners();

				ModpacksButton.GetComponent<Button>().onClick.AddListener(() =>
				{
					Debug.Log("Editor");
					LaunchEditor();
				});


			}
			catch (Exception e)
			{
				Debug.Log("Error adding button to main menu raft ui: " + e);
			}




		}

		public void LaunchEditor()
		{
			string[] str = new string[] { };
			LoadEditor(str);
		}

		private void Update()
		{
			

			if (!Raft_Network.IsHost)
			{
				// Choose a unique ID for the channel id to not interfer with other mods.
				NetworkMessage netMessage = RAPI.ListenForNetworkMessagesOnChannel(6969);
				if (netMessage != null)
				{
					CSteamID id = netMessage.steamid;
					Message message = netMessage.message;
					// Do your stuff with the message now that you know 
					// its yours and its the wanted type.
					Debug.Log("Host asked to instantiate a new island");
					IslandMessage msg = message as IslandMessage;
					Debug.Log("Loading island from Host: " + msg.Islandtoload[0]);
					ForceSpawnNewLandmark(msg.Islandtoload);
				}
			}
		}

		public void OnModUnload()
		{
			//The mod will not be able to be unloaded, therefore this will be unused
			Debug.Log("Mod Custom Islands has been unloaded!");
		}



		[ConsoleCommand(name: "LoadEditor", docs: "Loads into the Editor via Command")]
		public static async void LoadEditor(string[] args)
		{
			if (instance.mainbundle == null)
			{
				Debug.Log("Mainbundle is null");

			}
			string[] scenePath = instance.mainbundle.GetAllScenePaths();
			if (scenePath.Length == 0)
			{
				Debug.Log("scenepath is null");

			}

			var scene = new Scene();
			foreach (string sceneName in scenePath)
			{
				if(Utils.SceneNameFromPath(sceneName) == "Editor")
				{
					SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
					scene  = SceneManager.GetSceneByName(Utils.SceneNameFromPath(sceneName));
				}
			}

			Debug.Log(scenePath[0]);
			Debug.Log("Loading landmark from scene " + scenePath[0]);



			try
			{
				//Debug.Log("check if scene is loaded");

				while (!scene.isLoaded)
				{
					//Debug.Log("scene not loaded, waiting");
					await new WaitForSeconds(.1f);
				}
				Debug.Log("scene loaded");
				await new WaitForSeconds(1f);

				GameObject[] rootgoeditor = SceneManager.GetSceneByName(Utils.SceneNameFromPath(scenePath[0])).GetRootGameObjects();
				GameObject Canvas = rootgoeditor[0].gameObject.transform.Find("Canvas").gameObject;
				GameObject EditorNavbar = Canvas.gameObject.transform.Find("EditorNavbar").gameObject;
				GameObject Toolbar = Canvas.gameObject.transform.Find("Toolbar").gameObject;

				EditorNavbar.transform.Find("Button").GetComponent<Button>().onClick.AddListener(() =>
				{
					Debug.Log("Loading Main Menu");
				});
			}
			catch { }
			await Task.Delay(1000);
			//need to get all gameobjects
			//process these and add them as buttons
			/*	Debug.Log("processing gameobjects");


				foreach (GameObject go in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
				{
					GlobalPrefabList.Add(go);
					Debug.Log(go.name);
				}*/

			Debug.Log("Adding cam move");
			RAPI.ToggleCursor(true);
			// Get a reference to the main camera
			
			Camera mainCamera = Camera.main;
			terraineditor terrainEditor = mainCamera.gameObject.AddComponent<terraineditor>();
			RTSCamera cam = mainCamera.gameObject.AddComponent<RTSCamera>();
			// Check if the main camera has a TerrainEditor component
			Debug.Log("Added Cam");


			//Name should be changed when further working with the hierarchy
			GameObject DropdownMenuSelector = GameObject.Find("DropdownMenu");
			DropdownMenuSelector.GetComponent<Dropdown>().onValueChanged.AddListener((int index) =>
			{
				switch (index)
				{
					case 0:
						//Going back to the main menu
						SceneManager.LoadScene("MainMenuScene", LoadSceneMode.Single);
						break;
					case 1:
						//Save the island
						SaveIsland();
						break;
					case 2:
						//Load the island
						LoadIsland("test.json");
						break;
				}
			});

			try
			{

				TabSelector tabbSelector = GameObject.Find("TabSelector").AddComponent<TabSelector>();
				tabbSelector.SelectedTab = TAB.ObjectPlace;
				tabbSelector.ToolList = GameObject.Find("ToolList");
				for (int i = 0; i < tabbSelector.ToolList.transform.childCount; i++)
				{
					int temp = i;

					tabbSelector.transform.GetChild(temp).gameObject.GetComponent<Button>().onClick.AddListener(() =>
					{
						tabbSelector.UpdateTabSelection(temp);


					});
					Debug.Log("added event to " + tabbSelector.transform.GetChild(temp).name);
				}
			}
			catch (Exception e) { }

			SceneManager.LoadScene("44#Landmark_Vasagatan", LoadSceneMode.Additive);
			await Task.Delay(1000);
			Scene sceneVasagatan = SceneManager.GetSceneByName("44#Landmark_Vasagatan");

			GameObject vasagatanBoat = sceneVasagatan.GetRootGameObjects()[0].transform.Find("Boat related").gameObject;
			if (vasagatanBoat == null) Debug.Log("Boat is null");

			string[] vasagatanFile = File.ReadAllLines("vasagatanboat.goodv1.txt");

			foreach (Transform el in vasagatanBoat.GetComponentsInChildren<Transform>())
			{
				if (vasagatanFile.Contains(el.name))
				{
					//Debug.Log("Found instance of Gameobject with name: "+el.name);
					instance.VasagatanDefinitions.Add(el.gameObject);
				}
			}

			//SceneManager.UnloadScene(sceneVasagatan); 

			//Add gameobjects to the gameobject list in the editor
			GameObject ContentGO = GameObject.Find("ToolList").transform.Find("ObjectTool").transform.Find("Scroll View").transform.Find("Viewport").transform.Find("Content").gameObject;
			GameObject ButtonTemplate = ContentGO.transform.Find("Button").gameObject;

			for(int i = 0; i<instance.VasagatanDefinitions.Count; i++)
			{
				var tmp = i;
				GameObject CurrentGO = instance.VasagatanDefinitions[tmp];
				GameObject newButton = Instantiate(ButtonTemplate, ContentGO.transform);
				newButton.GetComponentInChildren<Text>().text = CurrentGO.name;
				newButton.GetComponent<Button>().onClick.AddListener(() => { Debug.Log("Go placing: " + CurrentGO.name); EditorGizmoHandler.placingObject = true; PlaceObject(CurrentGO); });
				
			}

			HideSceneGameObjects(sceneVasagatan);

			//Load shaders
			UnityEngine.Object[] shaders = instance.helperbundle.LoadAllAssets(typeof(Shader));
			foreach(UnityEngine.Object sh in shaders)
			{
				Debug.Log("File: " + sh.name);
				_shaders.Add((Shader)sh);
			}


			EditorGizmoHandler = Camera.main.gameObject.AddComponent<TransformGizmo>();




			//Test load go to list
			//SceneManager.LoadSceneAsync()

		}

		public static void SaveIsland()
		{
			//Get a list of all items and store their x, y and z + their name
			List<IslandData> islandData = new List<IslandData>();

			List<EditorGameObject> PlacedObjects = GameObject.Find("PlacedObjects").GetComponentsInChildren<EditorGameObject>().ToList();

			Debug.Log(PlacedObjects + "" + PlacedObjects.Count);
			foreach(EditorGameObject EGO in PlacedObjects)
			{
				IslandData item = new IslandData();

				item.Name = EGO.GameObjectName;
				Debug.Log(EGO.transform.localPosition.x);
				item.TransformData = UtilityMethods.IslandTransformDataConverter(EGO.transform);
				Debug.Log(item.Name);
				Debug.Log(item.TransformData._position.Length);
				Debug.Log(item.TransformData._position[0].ToString());
				Debug.Log(item);
				islandData.Add(item);
			}

			IslandDataList islandDataList = new IslandDataList();

			islandDataList.list = islandData;

			// Get a reference to the active terrain
			Terrain terrain = terraineditor.terrain;

			// Get the terrain data
			TerrainData terrainData = terrain.terrainData;

			// Get the heightmap data
			float[,] heightmap = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);

			// Convert the heightmap data to a string
			string heightmapString = "";

			for (int y = 0; y < terrainData.heightmapResolution; y++)
			{
				for (int x = 0; x < terrainData.heightmapResolution; x++)
				{
					heightmapString += heightmap[y, x].ToString() + " ";
				}
				heightmapString += "\n";
			}
			islandDataList.TerrainBinaryString = heightmapString;

			string JsonOutput = JsonConvert.SerializeObject(islandDataList);

			Debug.Log(JsonOutput);

			File.WriteAllText("test.json", JsonOutput);


		}


		public static void LoadAndCacheDefinitions()
		{
			Scene sceneVasagatan = SceneManager.GetSceneByName("44#Landmark_Vasagatan");

			GameObject vasagatanBoat = sceneVasagatan.GetRootGameObjects()[0].transform.Find("Boat related").gameObject;
			if (vasagatanBoat == null) Debug.Log("Boat is null");

			string[] vasagatanFile = File.ReadAllLines("vasagatanboat.goodv1.txt");

			foreach (Transform el in vasagatanBoat.GetComponentsInChildren<Transform>())
			{
				if (vasagatanFile.Contains(el.name))
				{
					//Debug.Log("Found instance of Gameobject with name: "+el.name);
					instance.VasagatanDefinitions.Add(el.gameObject);
				}
			}
		}
		
		public static void LoadIsland(string path)
		{
			string datafile = File.ReadAllText(path);
			List<IslandData> islandData = new List<IslandData>();



			IslandDataList islandDataList = JsonConvert.DeserializeObject<IslandDataList>(datafile);
			islandData = islandDataList.list;

			Debug.Log(islandData.Count);

			foreach (Transform child in GameObject.Find("PlacedObjects").transform)
			{
				Destroy(child.gameObject);
			}


			foreach (IslandData data in islandData)
			{
				string searchGoName = data.Name;
				GameObject gotoplace = instance.VasagatanDefinitions.Find(go => go.name == searchGoName);

				if(gotoplace == null)
				{
					Debug.LogError("COULDN'T PLACE GAMEOBJECT WITH NAME: " +  searchGoName);
					return;
				}

				//Place the object into the scene
				//Placing object at the current position
				GameObject GoInst = Instantiate(gotoplace, new Vector3(data.TransformData._position[0], data.TransformData._position[1], data.TransformData._position[2]), Quaternion.Euler(data.TransformData._rotation[0], data.TransformData._rotation[1], data.TransformData._rotation[2]));
				GoInst.transform.localScale = new Vector3(data.TransformData._scale[0], data.TransformData._scale[1], data.TransformData._scale[2]);
				try { GoInst.gameObject.GetComponent<Collider>().enabled = true; } catch (Exception e) { }

				GoInst.gameObject.AddComponent<Editor.EditorGameObject>();
				GoInst.gameObject.GetComponent<Editor.EditorGameObject>().GameObjectName = searchGoName;


				GoInst.gameObject.transform.parent = GameObject.Find("PlacedObjects").transform;



			}

			string[] rows = islandDataList.TerrainBinaryString.Split(new char['\n'], StringSplitOptions.RemoveEmptyEntries);
			int resolution = rows.Length;
			float[,] heightmap = new float[resolution, resolution];
			var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
			culture.NumberFormat.NumberDecimalSeparator = ",";

			for (int y = 0; y < resolution; y++)
			{
				string[] values = rows[y].Split(new char[' '], StringSplitOptions.RemoveEmptyEntries);
				for (int x = 0; x < resolution; x++)
				{
					heightmap[y, x] = float.Parse(values[x], culture);
				}
			}
			terraineditor.terrain.terrainData.SetHeights(0, 0, heightmap);
		}

		public static void HideSceneGameObjects(Scene scene)
		{
			foreach (GameObject sceneObject in scene.GetRootGameObjects())
			{
				sceneObject.SetActive(false);
			}
		}

		public static void PlaceObject(GameObject GO)
		{
			//if any are present
			try
			{
				Destroy(FindObjectOfType<ObjectPlacer>().gameObject);
			}
			catch (Exception e) { }
			GameObject NewObjectToPlace = Instantiate(GO);
			NewObjectToPlace.AddComponent<ObjectPlacer>();
			NewObjectToPlace.GetComponent<ObjectPlacer>().GameObjectName=GO.name;
		}

		[ConsoleCommand(name: "SpawnPrefabTest", docs: "Refreshes the Bundle cache")]
		public static async void SpawnPrefabTest(string[] args)
		{


			Instantiate(GlobalPrefabList[System.Convert.ToInt32(args[0])]);




		}

		/*ItemManager.GetAllItems().ForEach(i =>
		{
			try
			{
				//GameObject go = i.settings_buildable.GetBlockPrefab(0).gameObject;
				Debug.Log("got gameobject" + i.GetUniqueName() + i.GetUniqueIndex());
				GlobalPrefabList.Add(i);
			}
			catch { }
		}
		);*/



		[ConsoleCommand(name: "SpawnCustomLandmark", docs: "Spawns a custom landmark")]
		public static void SpawnNewLandmark(string[] args)
		{
			if (Raft_Network.IsHost && LoadSceneManager.IsGameSceneLoaded)
			{
				IEnumerator coroutine = instance.customlandmarkienum(args);
				instance.StartCoroutine(coroutine);
			}
			else
			{
				Debug.LogWarning("You're not the host or you're not ingame");
			}
		}

		public static void ForceSpawnNewLandmark(string[] args)
		{
			if (LoadSceneManager.IsGameSceneLoaded)
			{
				IEnumerator coroutine = instance.customlandmarkienum(args);
				instance.StartCoroutine(coroutine);
			}
			else
			{
				Debug.LogWarning("You're not ingame");
			}
		}

		//csrun
		[ConsoleCommand(name: "spawnlandmarkcheat", docs: "Toggle the itemspawner menu.")]
		public void SpawnLandmark(string[] args)
		{
			string landmark = args[0];
			ChunkPointType cpt = ChunkPointType.None;

			switch (landmark)
			{
				case "balboa":
					cpt = ChunkPointType.Landmark_Balboa;
					break;
				default:
					cpt = ChunkPointType.None;
					break;
			}

			if (!Raft_Network.IsHost)
			{
				FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "You are not the host!", 3, HNotify.ErrorSprite);
				return;
			}
			SO_ChunkSpawnRuleAsset sO_ChunkSpawnRuleAsset = new SO_ChunkSpawnRuleAsset();


			SO_ChunkSpawnRuleAsset ruleFromPointType = ComponentManager<ChunkManager>.Value.GetRuleFromPointType(cpt);
			if (ruleFromPointType)
			{
				int value = 200;
				switch (cpt)
				{
					case ChunkPointType.Landmark_Balboa:
						value = 400;
						break;
				}

				ComponentManager<ChunkManager>.Value.AddChunkPointCheat(cpt, Raft.direction * value);
				FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "Landmark successfully spawned!", 3, HNotify.CheckSprite);
			}
			else
			{
				FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "This island is in the game but isn't fully implemented currently!", 3, HNotify.ErrorSprite);
			}
		}



		public IEnumerator customlandmarkienum(string[] args)
		{
			Debug.Log("Loading custom landmark");

			landmarkBundle bundletoload = new landmarkBundle();

			if (args[0].IsNullOrEmpty())
			{
				Debug.LogWarning("Invalid Landmark! Check if you spelled the name correctly!");
				yield break;
			}

			foreach (landmarkBundle bundle1 in landmarkBundles)
			{
				if (bundle1.name == args[0])
				{
					bundletoload.name = bundle1.name;
					bundletoload.path = bundle1.path;
					bundletoload.bundle = bundle1.bundle;
					break;
				}
			}


			if (bundletoload.path == null)
			{
				Debug.LogWarning("Invalid Landmark! Check if you spelled the name correctly or if the file really exists!");
				yield break;
			}
			//Debug.Log("got data preparing scene load");
			//AssetBundle bundle = AssetBundle.LoadFromMemory(File.ReadAllBytes(bundletoload.path));
			AssetBundle bundle = bundletoload.bundle;

			if (bundle == null)
			{
				Debug.LogWarning("Invalid AssetBundle! The file might be broken!");
				yield break;
			}

			Debug.Log("Loading scene");

			string[] scenePath = bundle.GetAllScenePaths();
			Debug.Log("Loading landmark from scene " + scenePath[0]);
			SceneManager.LoadScene(scenePath[0], LoadSceneMode.Additive);

			var scene = SceneManager.GetSceneByName(Utils.SceneNameFromPath(scenePath[0]));

			//Debug.Log("check if scene is loaded");

			while (!scene.isLoaded)
			{
				//Debug.Log("scene not loaded, waiting");
				yield return new WaitForSeconds(.1f);
			}
			//Debug.Log("scene loaded");

			GameObject[] rootgoisland = SceneManager.GetSceneByName(Utils.SceneNameFromPath(scenePath[0])).GetRootGameObjects();


			//bundle.Unload(true);

			Vector3 spawnOffset = Raft.direction * 200;
			Debug.Log("spawn offset" + spawnOffset);
			foreach (GameObject go in rootgoisland)
			{
				//Debug.Log("go" + go.name);
			}
			//Debug.Log(bundletoload.name + "CustomLandmark");
			GameObject CustomLandmark = rootgoisland[0];
			//Debug.Log("found landmark" + CustomLandmark.name);
			try
			{
				Vector3 spawnpos = FindObjectOfType<Raft>().gameObject.transform.position + spawnOffset;
				Debug.Log(spawnpos);
				CustomLandmark.transform.position = spawnpos;
			}
			catch (NullReferenceException e)
			{
				Debug.LogWarning(e);
			}
			CustomLandmark.GetComponentInChildren<Terrain>().gameObject.layer = (LayerMask)16;
			//Debug.Log("Layer is on " + CustomLandmark.GetComponentInChildren<Terrain>().gameObject.layer.ToString());
			Debug.Log("Landmark spawned successfully");

			if (Raft_Network.IsHost)
			{
				Debug.Log("Sending to spawn request to other players");

				// This will send your network message to all players.
				IslandMessage islandMessage = new IslandMessage();
				islandMessage.Islandtoload = args;

				RAPI.SendNetworkMessage(islandMessage, 6969, EP2PSend.k_EP2PSendReliable);
			}

			//REAPPLY SHADERS
			CustomLandmark.AddComponent<ReApplyShaders>();
			//Debug.Log(CustomLandmark.GetComponentInChildren<Terrain>().gameObject.name + "thats my name XD");



			//spawn snowmobiles if there are any
			if (CustomLandmark.GetComponentsInChildren<SnowmobileShed>().Length > 0)
			{

				SnowmobileShed prefabClass = new SnowmobileShed();

				if (GameManager.GameMode == GameMode.Creative)
				{
					Debug.Log("We're in creative. Load temperance");

					SceneManager.LoadScene("55#Landmark_Temperance#", LoadSceneMode.Additive);

					var sceneTemperance = SceneManager.GetSceneByName("55#Landmark_Temperance#");
					if (!sceneTemperance.isLoaded)
					{
						Debug.Log("scene not loaded, waiting");
						yield return new WaitForSeconds(.1f);
					}
					prefabClass = sceneTemperance.GetRootGameObjects()[0].GetComponentsInChildren<SnowmobileShed>()[0];

					Destroy(sceneTemperance.GetRootGameObjects()[0]);
				}
				else
				{

					var sceneTemperance = SceneManager.GetSceneByName("55#Landmark_Temperance#");
					if (!sceneTemperance.isLoaded)
					{
						Debug.Log("scene not loaded, waiting");
						yield return new WaitForSeconds(.1f);
					}
					prefabClass = sceneTemperance.GetRootGameObjects()[0].GetComponentsInChildren<SnowmobileShed>()[0];
				}


				foreach (SnowmobileShed shed in CustomLandmark.GetComponentsInChildren<SnowmobileShed>())
				{
					Debug.Log(shed.gameObject.name);
					try
					{
						Debug.Log("tempfix");
						//shed.gameObject.transform.GetChild(1).transform.position = new Vector3(0, 1, 0);
						//shed.gameObject.transform.GetChild(1).transform.localPosition = new Vector3(0, 1, 0);
					}
					catch { };
					shed.SetSnowmobilePrefab(prefabClass.GetSnowmobilePrefab());
					if (Raft_Network.IsHost)
					{
						shed.SpawnSnowmobileNetwork();
					}
				}
			}

			List<string> content = new List<string>();
			Resources.FindObjectsOfTypeAll<GameObject>().ToList().ForEach(x => content.Add(x.name));
			Debug.Log(content.ToArray().Length);
			System.IO.File.WriteAllLines("allresources.txt", content.ToArray());

			//RAPI.GetLocalPlayer().transform.position = CustomLandmark.GetComponentInChildren<Transform>().position;
			//We just need the first. Keep for later if we want to load multiple
			/*foreach (string scene in scenePath)
			{
				Debug.Log("scene" + scene);
				SceneManager.LoadScene(scene, LoadSceneMode.Additive);
			}*/
		}


		public IEnumerator WaitforSceneLoading(Scene scene, string customIsland)
		{
			while (!scene.isLoaded)
			{
				yield return new WaitForSeconds(.01f);
			}

			//scene.name = Path.GetFileNameWithoutExtension(customIsland);
			if(VasagatanDefinitions.Count == 0)
			{
				Debug.Log("No definition for Vasagatan, creating them");
				LoadAndCacheDefinitions();
			}


			GameObject PlacedObjectsHolder = new GameObject();
			PlacedObjectsHolder.name = "PlacedObjects";

			Instantiate(PlacedObjectsHolder, scene.GetRootGameObjects()[0].gameObject.transform);

			LoadIsland(customIsland);
			
			//PlaceObjects on island

			yield return 0;
		}


		#region OnlineIslandDatabaseHandling
		//Logic to up or download custom islands from the custom islands server.

		[ConsoleCommand(name: "UploadFileTest", docs: "Upload Custom Island to the Server (left here for further development, is currently unused)")]
		public static void uploadIsland(string[] args)
		{
			instance.StartCoroutine(instance.UploadIsland("user", "password", "arandomisland"));
		}

		IEnumerator UploadIsland(string username, string password, string islandname)
		{


			WWWForm form = new WWWForm();
			form.AddBinaryData("islandfile", File.ReadAllBytes(@"Mods\demoisland1.assets"), "demoisland1.assets", "binary/octet-stream");
			form.AddField("username", username);
			form.AddField("password", System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password)));
			form.AddField("islandname", islandname);

			var uwr = new UnityWebRequest();
			uwr = UnityWebRequest.Post("http://localhost/CustomIslandsWebapp/upload.php", form);

			//uwr.uploadHandler = new UploadHandlerFile(@"Mods\hello.txt");
			yield return uwr.SendWebRequest();
			if (uwr.isNetworkError || uwr.isHttpError)
				Debug.LogError(uwr.error);
			else
			{
				// file data successfully sent
				Debug.Log("file uploaded" + uwr.downloadHandler.text + " with code " + uwr.responseCode);
			}

		}


		#endregion


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
		[ConsoleCommand(name: "SetToSample", docs: "Change Terrain Edit to sample the terrain")]
		public static void TerrainSample()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Sample;
		}
		[ConsoleCommand(name: "SetToSampleAverage", docs: "Change Terrain Edit to average sample the terrain")]
		public static void TerrainSampleAverage()
		{
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.SampleAverage;
		}
		[ConsoleCommand(name: "ChangeHeight", docs: "Change height of the Terrain Editing Brush")]
		public static void TerrainHeight(string[] args)
		{
			int value = int.Parse(args[0]);

			terraineditor.brushHeight = value;

		}
		[ConsoleCommand(name: "ChangeWidth", docs: "Change width of the Terrain Editing Brush")]
		public static void TerrainWidth(string[] args)
		{
			int value = int.Parse(args[0]);

			terraineditor.brushWidth = value;

		}
		[ConsoleCommand(name: "ChangeStrength", docs: "Change strength of the Terrain Editing Brush")]
		public static void TerrainStrength(string[] args)
		{
			float value = float.Parse(args[0]);

			if (value > 1)
			{
				terraineditor.strength = 1f;
			}

			if (value < 0.1f)
			{
				terraineditor.strength = 0.1f;
			}

			terraineditor.strength = value;

		}

		[ConsoleCommand(name: "EnableEditing", docs: "Enable the use of Terrain Edit")]
		public static void TerrainEnableEdit()
		{
			if (RAPI.IsCurrentSceneMainMenu()) { Debug.LogWarning($"x: Cant change value while in Main Menu"); return; }

			terraineditor.allowEditing = true;

			// GameObject canvasObj = canvasBundle.LoadAsset<GameObject>("TerrainEdit_Canvas");
			//
			// if (customCanvas == null)
			// {
			//     customCanvas = Instantiate(canvasObj);
			//     print($"{modName}: Couldn't find existing canvas, creating new one..");
			// }

		}

		[ConsoleCommand(name: "DisableEditing", docs: "Disable the use of Terrain Edit")]
		public static void TerrainDisableEdit()
		{
			if (RAPI.IsCurrentSceneMainMenu()) { Debug.LogWarning($"y: Cant change value while in Main Menu"); return; }

			terraineditor.allowEditing = false;

			// if(customCanvas == null){print("Issue trying to reference Custom Canvas..");}
			// else { customCanvas.SetActive(false); print($"{modName}:Custom Canvas now disabled!" + customCanvas); }

		}

		#endregion

	}

	//HARMONY PATCHES
	//Scene Loader
	[HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadScenes))]
	class AdditionalSceneLoadingPatch{

		static void Postfix(ref SceneLoader __instance)
		{
			Debug.Log("SCENE LOADER POSTFIX");

			Debug.Log("Loading scene");

			string[] scenePath = DynamicIslands.instance.mainbundle.GetAllScenePaths();

			string[] customIslands = Directory.GetFiles(@"Mods\DynamicIslands\");

			foreach (string island in customIslands)
			{
				var scene = new Scene();
				foreach (string sceneName in scenePath)
				{
					if (Utils.SceneNameFromPath(sceneName) == "CustomIsland")
					{
						Debug.Log("loading scene " + sceneName);
						SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
						scene = SceneManager.GetSceneByName(Utils.SceneNameFromPath(sceneName));
						break;
					}
				}
				IEnumerator coroutine = DynamicIslands.instance.WaitforSceneLoading(scene, island);
				DynamicIslands.instance.StartCoroutine(coroutine);
			}
			
		}

	}

	#region MiscStuff

	[HarmonyPatch(typeof(Snowmobile), nameof(RaftGame.Private.PrivateAccessor_Snowmobile.Start))]
	class snowmoobilenosound
	{
		static void Postfix(ref Snowmobile __instance)
		{
			if (__instance.GetEmitter_engine() == null)
			{
				Debug.Log("EMMITER ENGINE IS NULL");
			}
			if (__instance.GetEmitter_impact() == null)
			{
				Debug.Log("EMMITER impact IS NULL");
			}
		}
	}

	//SNOWMOBILES ANYWHERE!

	/*[HarmonyPatch(typeof(Snowmobile), "Update")]
	static class Patch_Snowmobile_Update
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var code = instructions.ToList();
			code.Insert(code.FindLastIndex(code.FindIndex(x => x.opcode == OpCodes.Call && (x.operand as MethodInfo).Name == "Raycast"), x => x.opcode == OpCodes.Ldsfld && (x.operand as FieldInfo).Name == "MASK_Obstruction") + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_Snowmobile_Update), nameof(EditMask))));
			return code;
		}
		public static LayerMask EditMask(LayerMask original) => original | (LayerMask)1;
		/*static void Postfix(Snowmobile __instance, Transform ___groundCheckPoint, Rigidbody ___body)
		{
			var flag = Physics.Raycast(___groundCheckPoint.position, Vector3.down, out var hit, 100, (LayerMask)16) && hit.collider.transform.IsChildOf(SingletonGeneric<GameManager>.Singleton.lockedPivot);
			if (___body.transform.ParentedToRaft() != flag)
				___body.transform.SetParent(flag ? SingletonGeneric<GameManager>.Singleton.lockedPivot : null, true);
		}*/
	//}




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
	public class ReApplyShaders : MonoBehaviour
	{
		public Renderer[] renderers;
		public Material[] materials;
		public string[] shaders;

		void Awake()
		{
			Debug.Log("Getting renderers");
			renderers = GetComponentsInChildren<Renderer>();
		}

		void Start()
		{
			Debug.Log("FIXING SHADERS");
			foreach (var rend in renderers)
			{
				try
				{
					materials = rend.sharedMaterials;
					shaders = new string[materials.Length];

					for (int i = 0; i < materials.Length; i++)
					{
						try
						{
							shaders[i] = materials[i].shader.name;
						}
						catch { }
					}

					for (int i = 0; i < materials.Length; i++)
					{
						try
						{
							materials[i].shader = Shader.Find(shaders[i]);
						}
						catch { }
					}
				}
				catch
				{

				}
			}
		}
	}


	public class UnityAssetBundleRequestAwaiter : INotifyCompletion
	{
		private AssetBundleCreateRequest asyncOp;
		private Action continuation;

		public UnityAssetBundleRequestAwaiter(AssetBundleCreateRequest asyncOp)
		{
			this.asyncOp = asyncOp;
			asyncOp.completed += OnRequestCompleted;
		}

		public bool IsCompleted { get { return asyncOp.isDone; } }

		public void GetResult() { }

		public void OnCompleted(Action continuation)
		{
			this.continuation = continuation;
		}

		private void OnRequestCompleted(AsyncOperation obj)
		{
			continuation();
		}
	}


	public static class ExtensionMethods
	{

		public static UnityAssetBundleRequestAwaiter GetAwaiter(this AssetBundleCreateRequest asyncOp)
		{
			return new UnityAssetBundleRequestAwaiter(asyncOp);
		}
	}



	#endregion

}