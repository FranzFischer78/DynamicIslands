using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>Marks an object inside a group's preview: which catalog object it is, with its settings.</summary>
	public class GroupMember : MonoBehaviour
	{
		public string ObjectName;
		/// <summary>The object's settings, packed (Instantiate doesn't copy dictionaries): key\u001Fvalue\u001E...</summary>
		public string PropsText;

		public static string Pack(IDictionary<string, string> props)
		{
			if (props == null) return "";
			return string.Join("\u001E", props.Select(kv => kv.Key + "\u001F" + kv.Value).ToArray());
		}

		public static Dictionary<string, string> Unpack(string text)
		{
			var d = new Dictionary<string, string>();
			if (string.IsNullOrEmpty(text)) return d;
			foreach (string pair in text.Split('\u001E'))
			{
				int i = pair.IndexOf('\u001F');
				if (i > 0) d[pair.Substring(0, i)] = pair.Substring(i + 1);
			}
			return d;
		}
	}

	/// <summary>
	/// The object group editor ("prefabs"): builders save a selection of placed objects (a hut with its furniture, a
	/// camp, an ambush with its zone) as a group, which then sits in the object browser's "My groups" category and
	/// is placed like any object. When it's put down it becomes its separate objects again, with their settings, as
	/// one undo step. Groups are files in Mods\DynamicIslands\groups (share them by copying).
	///
	/// File (.group): "CIGR", int32 version 1, int32 count, then per object: string name, vector3 position (relative to
	/// the group's bottom centre), vector3 euler, vector3 scale, int32 property count, (string key, string value)...
	/// </summary>
	public static class GroupLibrary
	{
		public const string Category = "My groups";
		public const string Prefix = "Group:";
		const uint Magic = 0x52474943; // "CIGR"

		public static string Folder { get { return Path.Combine(DynamicIslands.assetpath, "groups"); } }
		static string PathFor(string name) { return Path.Combine(Folder, name + ".group"); }

		public static bool IsGroup(string catalogName) { return catalogName != null && catalogName.StartsWith(Prefix); }

		public class Member
		{
			public string Name;
			public Vector3 Position, Euler, Scale = Vector3.one;
			public Dictionary<string, string> Props = new Dictionary<string, string>();
		}

		public static IEnumerable<string> Saved()
		{
			if (!Directory.Exists(Folder)) return Enumerable.Empty<string>();
			return Directory.GetFiles(Folder, "*.group").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n);
		}

		/// <summary>Saves placed objects as a group; returns how many objects it holds (0 = nothing to save).</summary>
		public static int Save(string name, IEnumerable<EditorGameObject> objects)
		{
			List<EditorGameObject> list = objects.Where(o => o != null && !IsGroup(o.GameObjectName)).ToList();
			if (list.Count == 0) return 0;
			// The pivot: the middle of the objects, at the lowest one's height (so the group stands on the ground)
			Vector3 pivot = new Vector3(list.Average(o => o.transform.position.x), list.Min(o => o.transform.position.y), list.Average(o => o.transform.position.z));
			Directory.CreateDirectory(Folder);
			// (written whole, then in the file's place in one step - SafeFile: a crash mid-save left a broken group, AU41)
			var bytes = new MemoryStream();
			using (var w = new BinaryWriter(bytes))
			{
				w.Write(Magic); w.Write(1); w.Write(list.Count);
				foreach (EditorGameObject o in list)
				{
					w.Write(o.GameObjectName);
					Vec(w, o.transform.position - pivot); Vec(w, o.transform.rotation.eulerAngles); Vec(w, o.transform.lossyScale);
					Dictionary<string, string> p = o.Props ?? new Dictionary<string, string>();
					w.Write(p.Count);
					foreach (var kv in p) { w.Write(kv.Key); w.Write(kv.Value ?? ""); }
				}
			}
			SafeFile.WriteAllBytes(PathFor(name), bytes.ToArray());
			return list.Count;
		}

		public static List<Member> Load(string name)
		{
			var list = new List<Member>();
			using (var r = new BinaryReader(File.OpenRead(PathFor(name))))
			{
				if (r.ReadUInt32() != Magic) throw new InvalidDataException(name + " is not a Custom Islands group");
				r.ReadInt32();
				int n = r.ReadInt32();
				for (int i = 0; i < n; i++)
				{
					var m = new Member { Name = r.ReadString(), Position = Vec(r), Euler = Vec(r), Scale = Vec(r) };
					int props = r.ReadInt32();
					for (int k = 0; k < props; k++) { string key = r.ReadString(); m.Props[key] = r.ReadString(); }
					list.Add(m);
				}
			}
			return list;
		}

		public static bool Delete(string name)
		{
			// (a typed name: no folders in it; and to the deleted folder like the groups window's Delete, not for good)
			if (FileNames.Problem(name) != null || !File.Exists(PathFor(name))) return false;
			MoveToDeleted(name);
			return true;
		}

		public static bool Exists(string name) { return File.Exists(PathFor(name)); }

		/// <summary>
		/// The groups window's Delete (ROADMAP T4): the group leaves "My groups" and its file is moved to
		/// Mods\DynamicIslands\deleted\groups (an older one of the same name gets a date), where it can be got back.
		/// </summary>
		public static void MoveToDeleted(string name)
		{
			PiecesFiles.MoveToDeleted(PathFor(name), "groups");
			PlaceableCatalog.RemoveCustom(Prefix + name);
			PlaceableCatalog.NotifyChanged();
		}

		/// <summary>Renames a group (its file and its entry in "My groups"). Placed copies on islands are separate objects: untouched.</summary>
		public static IEnumerator Rename(string from, string to)
		{
			RenameFile(from, to);
			yield return Register(to);
		}

		/// <summary>
		/// The file's part of Rename, at once: an error (the file in use) reaches the caller, not a coroutine nobody watches
		/// (review 2026-10-06: the window said "Renamed" while the file kept its name). Register(to) follows.
		/// </summary>
		public static void RenameFile(string from, string to)
		{
			PiecesFiles.Move(PathFor(from), PathFor(to));
			PlaceableCatalog.RemoveCustom(Prefix + from);
			ObjectThumbnails.Forget(Prefix + from);
		}

		/// <summary>Puts every saved group in the object catalog (loading the Raft scenes their objects come from first).</summary>
		public static IEnumerator RegisterAll()
		{
			foreach (string name in Saved().ToList()) yield return Register(name);
		}

		public static IEnumerator Register(string name)
		{
			List<Member> members;
			try { members = Load(name); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Group '" + name + "' can't be read: " + e.Message); yield break; }
			yield return PlaceableCatalog.EnsureLoaded(members.Select(m => m.Name).Distinct().ToList());
			try
			{
				GameObject old = PlaceableCatalog.Get(Prefix + name);
				var proto = new GameObject(Prefix + name);
				proto.transform.SetParent(PlaceableCatalog.Container.transform, false);
				int missing = 0;
				foreach (Member m in members)
				{
					GameObject go = PlaceableCatalog.Spawn(m.Name, proto.transform);
					if (go == null) { missing++; continue; }
					go.transform.localPosition = m.Position;
					go.transform.localRotation = Quaternion.Euler(m.Euler);
					go.transform.localScale = m.Scale;
					GroupMember gm = go.AddComponent<GroupMember>();
					gm.ObjectName = m.Name;
					gm.PropsText = GroupMember.Pack(m.Props);
					ObjectProps.ApplyTint(go, m.Props);
				}
				PlaceableCatalog.AddCustom(Prefix + name, proto, Category, name + " (" + members.Count + ")");
				if (old != null) { UnityEngine.Object.Destroy(old); ObjectThumbnails.Forget(Prefix + name); }
				PlaceableCatalog.NotifyChanged();
				if (missing > 0) Debug.LogWarning("[CUSTOM ISLANDS] Group '" + name + "': " + missing + " object(s) not found in the object list");
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Group '" + name + "': " + e.Message); }
		}

		/// <summary>
		/// A group preview was put down: replaces it with its separate objects (same places, turned and scaled like the
		/// preview, settings kept) under PlacedObjects. Returns them (the caller makes them one undo step).
		/// </summary>
		public static List<GameObject> Expand(GameObject preview, Transform placedRoot)
		{
			var created = new List<GameObject>();
			foreach (GroupMember gm in preview.GetComponentsInChildren<GroupMember>(true))
			{
				if (gm.transform.parent != preview.transform) continue; // (a member's own children)
				GameObject go = PlaceableCatalog.Spawn(gm.ObjectName, placedRoot);
				if (go == null) continue;
				go.transform.position = gm.transform.position;
				go.transform.rotation = gm.transform.rotation;
				go.transform.localScale = gm.transform.lossyScale;
				foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = true;
				EditorGameObject.Attach(go, gm.ObjectName, GroupMember.Unpack(gm.PropsText));
				created.Add(go);
			}
			UnityEngine.Object.Destroy(preview);
			return created;
		}

		static void Vec(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
		static Vector3 Vec(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
	}
}
