using System;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Which editor tab is open (Terrain, Objects or Island). The tools check it: sculpting only happens on the
	/// Terrain tab, the transform gizmo only works on the Objects tab. The editor UI (EditorUI) shows the panels.
	/// </summary>
	public class TabSelector : MonoBehaviour
	{
		public TAB SelectedTab = TAB.TerrainEdit;

		/// <summary>Raised after the tab changes.</summary>
		public event Action<TAB> TabChanged;

		public static TabSelector instance;

		void Awake()
		{
			instance = this;
		}

		void Start()
		{
			if (TabChanged != null) TabChanged(SelectedTab);
		}

		public void UpdateTabSelection(int selectedTab)
		{
			if (!Enum.IsDefined(typeof(TAB), selectedTab)) return;
			SelectedTab = (TAB)selectedTab;
			if (TabChanged != null) TabChanged(SelectedTab);
		}
	}

	public enum TAB
	{
		TerrainEdit = 0,
		ObjectPlace = 1,
		Island = 2
	}

}
