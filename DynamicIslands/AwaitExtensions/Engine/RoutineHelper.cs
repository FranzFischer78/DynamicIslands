using UnityEngine;

namespace Redcode.Awaiting.Engine
{
    /// <summary>
    /// Help run coroutines. Auto created, not destroyable and not visible in hierarchy.
    /// </summary>
    public class RoutineHelper : MonoBehaviour
    {
        /// <summary>
        /// Return instance of this class.
        /// </summary>
        public static RoutineHelper Instance { get; private set; }

        /// <summary>
        /// Create and save one instance of this class (singleton pattern). <br/>
        /// Created object will not be visible in hierarchy and do not destroyed between scenes.
        /// </summary>
        /// Unity never runs [RuntimeInitializeOnLoadMethod] for assemblies loaded after startup
        /// (RML compiles mods at runtime), so the mod calls this from Start().
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new GameObject("RoutineHelper (Awaiters)").AddComponent<RoutineHelper>();
            Instance.gameObject.hideFlags = HideFlags.HideInHierarchy;

            DontDestroyOnLoad(Instance.gameObject);
        }
    }
}