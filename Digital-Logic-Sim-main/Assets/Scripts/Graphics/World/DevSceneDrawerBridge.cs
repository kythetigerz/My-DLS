using UnityEngine;
using DLS.Graphics;

namespace DLS.Graphics.World
{
    public class DevSceneDrawerBridge : MonoBehaviour
    {

        private void OnGUI()
        {
            DevSceneDrawer.OnGUI();
        }
    }
}