using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace DynamicIslands.Editor
{

    public class TabSelector : MonoBehaviour
    {
        public TAB SelectedTab = TAB.ObjectPlace;
        public GameObject ToolList;

        public static TabSelector instance;

        // Start is called before the first frame update
        void Start()
        {
            instance = this;
           UpdateTabSelection((int)TAB.ObjectPlace);
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void UpdateTabSelection(int selectedTab)
        {
			ToolList.transform.GetChild((int)SelectedTab).gameObject.SetActive(false);
			ToolList.transform.GetChild((int)selectedTab).gameObject.SetActive(true);
            
            SelectedTab = (TAB)selectedTab;
        }


	}

    public enum TAB
    {
        TerrainEdit = 0,
        ObjectPlace = 1
    }

}
