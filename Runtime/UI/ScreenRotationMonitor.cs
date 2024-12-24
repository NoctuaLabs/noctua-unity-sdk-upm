using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class ScreenRotationMonitor : MonoBehaviour
    {
        private bool _isLandscape;
        
        public PanelSettings PanelSettings;
        
        private void Update()
        {
            if (_isLandscape == Screen.width > Screen.height) return;

            _isLandscape = Screen.width > Screen.height;
            
            if (_isLandscape)
            {
                PanelSettings.match = 0;
                PanelSettings.referenceResolution = new Vector2Int(800, 360);
            }
            else
            {
                PanelSettings.match = 1;
                PanelSettings.referenceResolution = new Vector2Int(360, 800);
            }
        }
    }
}