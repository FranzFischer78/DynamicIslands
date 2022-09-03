using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using Newtonsoft.Json.Linq;
using System;
using HarmonyLib;
using UnityEngine.Analytics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Steamworks;
using System.Linq;
using System.Threading.Tasks;
using AsyncOperation = UnityEngine.AsyncOperation;
using UnityEngine.Playables;
using System.Reflection.Emit;
using RaftGame.Private;

public class landmarkBundle
{
	public string name;
	public string path;
	public AssetBundle bundle;
}

/*public static class MyIslands
{
	public const ChunkPointType TheBestIslandOfAllTime = (ChunkPointType)71;
	public const ChunkPointType DoNotLoadThisIslandItWillCrash = (ChunkPointType)72;
}*/

[System.Serializable]
public class IslandMessage:Message
{
	public string[] Islandtoload;
}

public class DynamicIslands : Mod
{
	public static List<landmarkBundle> landmarkBundles = new List<landmarkBundle>();
	static string assetpath = @"Mods\DynamicIslands\";
	public static LoadSceneManager loadSceneManagerinstance;

	//ChunkPointType lol = MyIslands.TheBestIslandOfAllTime;

	static DynamicIslands instance;
	public void Start()
	{
		instance = this;
		loadSceneManagerinstance= FindObjectOfType<LoadSceneManager>();
		var harmony = new Harmony("com.franzfischer.dynamicislands");
		harmony.PatchAll();

		//INIT FOLDER
		if (!Directory.Exists(assetpath))
		{
			Directory.CreateDirectory(assetpath);
		}
		if(Directory.EnumerateFiles(assetpath).Count() == 0) {
			Debug.LogWarning("There are no custom Landmarks installed!");
		}

		//AssetBundle bundle = AssetBundle.LoadFromMemory(File.ReadAllBytes(assetpath + @"\Shader\builtincustomshaders.assets"));


		Debug.Log("[DYNAMIC ISLANDS] Loading custom Landmarks");

		RefreshLandmarkBundles(new string[1]);



		Debug.Log("[DYNAMIC ISLANDS] Mod DynamicIslands has been loaded!");

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
		foreach (landmarkBundle bundle in landmarkBundles)
		{
			bundle.bundle.Unload(true);
		}
		Debug.Log("Mod DynamicIslands has been unloaded!");
	}

	public static async Task readBundles()
	{
		List<landmarkBundle> bundles = new List<landmarkBundle>();

		foreach(string asset in Directory.EnumerateFiles(assetpath, "*.assets"))
		{
			landmarkBundle bundle = new landmarkBundle();
			bundle.path = asset;
			bundle.name = Path.GetFileNameWithoutExtension(asset);
			AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(File.ReadAllBytes(asset));
			await request;
			bundle.bundle = request.assetBundle;
			Debug.Log("Loaded Bundle");
			bundles.Add(bundle);
			Debug.Log(asset);
		}


		landmarkBundles= bundles;

	}


	[ConsoleCommand(name: "RefreshLandmarkBundles", docs: "Refreshes the Bundle cache")]
	public static async void RefreshLandmarkBundles(string[] args)
	{
		HNotification notification = FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.spinning, "Loading CustomIslands...");

		if (landmarkBundles.Count != 0)
		{
			foreach(landmarkBundle bundle in landmarkBundles)
			{
				bundle.bundle.Unload(true);
			}
		}

		Task readbundletask = (Task)readBundles();
		await readbundletask;
		//landmarkBundles = readBundles();

		notification.Close();

	}



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

			switch(landmark)
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


	[ConsoleCommand(name: "TpToLandmark", docs: "ddssdccsscd")]
	public static void tptolandmark()
	{

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
				shed.SpawnSnowmobileNetwork();
			}
		}



		//RAPI.GetLocalPlayer().transform.position = CustomLandmark.GetComponentInChildren<Transform>().position;
		//We just need the first. Keep for later if we want to load multiple
		/*foreach (string scene in scenePath)
		{
			Debug.Log("scene" + scene);
			SceneManager.LoadScene(scene, LoadSceneMode.Additive);
		}*/
	}

}




//HARMONY PATCHES
//Scene Loader


/*[HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadScenes))]
class AdditionalSceneLoadingPatch{

	static void Postfix(ref SceneLoader __instance)
	{
		Debug.Log("SCENE LOADER POSTFIX");
		foreach (landmarkBundle landmark in DynamicIslands.landmarkBundles)
		{
			AssetBundle bundle = landmark.bundle;     
			string[] scenePath = bundle.GetAllScenePaths();
			Debug.Log("Loading landmark from scene " + scenePath[0]);
			SceneManager.LoadScene(scenePath[0], LoadSceneMode.Additive);
			string scenePathByBuildIndex = scenePath[0];
			int num = scenePathByBuildIndex.LastIndexOf('/');
			string text = scenePathByBuildIndex.Substring(num + 1);
			int length = text.LastIndexOf('.');
			string sceneName = text.Substring(0, length);
			RaftGame.Private.PrivateAccessor_SceneLoader.OnSceneActivate(__instance, SceneManager.GetSceneByName(sceneName));
			//Traverse.Create(__instance).Method("OnSceneActivate",);
			Helper.LogBuild("SceneLoader: Finished " + sceneName);
		}

	}

}*/

[HarmonyPatch(typeof(Snowmobile), nameof(RaftGame.Private.PrivateAccessor_Snowmobile.Start))]
class snowmoobilenosound
{
	static void Postfix(ref Snowmobile __instance)
	{
		if(__instance.GetEmitter_engine() == null)
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


//This i created on my own and totally not based on the other one XD
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

