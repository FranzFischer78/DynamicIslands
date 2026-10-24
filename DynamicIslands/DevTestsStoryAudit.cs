using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>The scripts that make a piece of Raft's story islands do something (ROADMAP LM12's audit).</summary>
		static readonly Regex StoryScript = new Regex(@"Quest|Zipline|Machete|Treasure|Note|Elevator|Lift|Door|Keypad|Lever|Crank|Hatch|Button|Cage|Camera|Radio|Battery|Engine|Fuel|Teleport|Trigger|Boss|Ladder|Generator|Valve|Wheel|Puzzle|Challenge|Interact|Pickup|Lock|Key", RegexOptions.IgnoreCase);
		static readonly Regex Plumbing = new Regex(@"^(RaycastInteractable(_Redirect)?|LODGroup|PickupItem_Networked|YieldHandler|RandomDropper|LandmarkItem.*|NetworkIDTag|SO_.*)$");

		[ConsoleCommand(name: "CIStoryAudit", docs: "Dev, anywhere (the main menu is quickest): ROADMAP LM12's audit - every piece of Raft's story islands (Radio Tower, Vasagatan, Balboa, Caravan Town, Tangaroa, Varuna Point, Temperance, Utopia) with a script that makes it do something, and whether the editor has it (and keeps it working). Writes Mods\\DynamicIslands\\story_audit.txt")]
		public static void StoryAuditCommand(string[] args) { StartTest(StoryAuditRoutine()); }

		static IEnumerator StoryAuditRoutine()
		{
			yield return PlaceableCatalog.EnsureBuilt();
			var story = new Regex(@"Landmark_(Radar|Vasagatan|BalboaIsland|CaravanIsland|Tangaroa|VarunaPoint|Temperance#|Utopia)");
			List<string> scenes = PlaceableCatalog.LandmarkSceneNames().Where(n => story.IsMatch(n)).ToList();
			Log("story audit: scenes " + string.Join(", ", scenes.ToArray()));
			var lines = new List<string> { "# scene\tpiece\tscripts\tin editor\tworking" };
			foreach (string scene in scenes)
			{
				yield return PlaceableCatalog.VisitScene(scene, s =>
				{
					var seen = new HashSet<string>();
					foreach (GameObject root in s.GetRootGameObjects())
						foreach (MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
						{
							if (m == null) continue;
							string type = m.GetType().Name;
							if (Plumbing.IsMatch(type) || !StoryScript.IsMatch(type) || m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("UnityEngine")) continue;
							// (the piece: the object, or the nearest parent the editor knows by name)
							Transform t = m.transform;
							string piece = t.name;
							for (Transform p = t; p != null; p = p.parent)
								if (PlaceableCatalog.Known(Clean(p.name))) { piece = Clean(p.name); break; }
							string key = piece + "|" + type;
							if (!seen.Add(key)) continue;
							bool known = PlaceableCatalog.Known(piece);
							bool working = known && (PlaceableCatalog.Working.IsMatch(piece) || PlaceableCatalog.IsGatherName(piece) || ReadyPieces.Of(piece) != null);
							lines.Add(PlaceableCatalog.SceneLabel(scene) + "\t" + piece + (piece != Clean(t.name) ? " / " + Clean(t.name) : "") + "\t" + type + "\t" + (known ? "yes" : "no") + "\t" + (working ? "yes" : "-"));
						}
					return Nothing();
				});
			}
			string path = Path.Combine(DynamicIslands.assetpath, "story_audit.txt");
			File.WriteAllLines(path, lines.ToArray());
			Log("story audit: " + (lines.Count - 1) + " pieces with scripts, " + lines.Count(l => l.EndsWith("\tno\t-")) + " not in the editor - " + path);
			Log("PASS: story audit");
		}

		static string Clean(string n) { return Regex.Replace(n ?? "", @"\s*\(\d+\)$|\(Clone\)", "").Trim(); }

		static IEnumerator Nothing() { yield break; }
	}
}
