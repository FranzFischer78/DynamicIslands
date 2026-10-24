using RuntimeGizmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DynamicIslands.Editor
{



	public class EditorGameObject : MonoBehaviour
	{
		public string GameObjectName;
		public float arrowLength = 5;
		/// <summary>The object's extra data (creature settings, note text, tint...), saved with the island; see ObjectProps.</summary>
		public Dictionary<string, string> Props = new Dictionary<string, string>();
		/// <summary>The object's own number in the island it was opened from (R1b, IslandObject.Uid); 0 for a new one or a copy.</summary>
		[NonSerialized] public int Uid;

		/// <summary>
		/// Marks a spawned catalog object as placed in the editor. Props are copied (null = the object's defaults: a
		/// creature or note from the list starts with its own settings).
		/// </summary>
		public static EditorGameObject Attach(GameObject go, string name, Dictionary<string, string> props = null)
		{
			EditorGameObject ego = UIKit.Ensure<EditorGameObject>(go);
			ego.GameObjectName = name;
			ego.Props = props != null ? new Dictionary<string, string>(props) : ObjectProps.Defaults(name);
			if (props == null) GapFeatures.EditorDefaults(name, ego.Props); // (placed from the list: a generator wants 3 parts, a butler bot zaps)
			ObjectProps.ApplyInEditor(go, ego.Props);
			CaveBacks.Add(go, name); // (a cave piece's rock, or a Selene room's wall, seen from outside too - X7)
			return ego;
		}

		void Start()
		{


		}


		public void ShowGizmos(GizmosType GizmoType)
		{
			switch (GizmoType)
			{
				case GizmosType.Position:
					break;

				case GizmosType.Rotation:
					break;

				case GizmosType.Scale:
					break;
			}
		}

		public enum GizmosType
		{
			Position = 0,
			Rotation =1,
			Scale = 2
		}


	}

	/// <summary>
	/// How many objects an island may have. An island file with more than IslandFile.MaxObjects can't be opened again (the
	/// loader refuses it), so placing past that is refused; past the generator's cap (12 000) the builder is told once that
	/// such an island is slow to appear in a world.
	/// </summary>
	public static class ObjectLimit
	{
		public const int Warn = IslandGenerator.MaxObjects;
		static bool warned;

		/// <summary>The objects of the island being edited (deleted ones - hidden, kept for undo - don't count).</summary>
		public static int Count()
		{
			GameObject root = GameObject.Find("PlacedObjects");
			return root == null ? 0 : root.GetComponentsInChildren<EditorGameObject>(false).Length;
		}

		/// <summary>Whether this many more objects may be added; says why not, or warns once past 12 000.</summary>
		public static bool Allow(int adding)
		{
			int n = Count();
			if (n + adding > IslandFile.MaxObjects)
			{
				DynamicIslands.Notify("The island has " + n + " objects: at most " + IslandFile.MaxObjects + " fit in an island file - delete some first", true);
				return false;
			}
			if (!warned && n + adding > Warn)
			{
				warned = true;
				DynamicIslands.Notify("More than " + Warn + " objects: the island takes longer to appear in a world (the generator stops at " + Warn + ")");
			}
			return true;
		}
	}
}
