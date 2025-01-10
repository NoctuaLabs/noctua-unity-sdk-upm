using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class ScreenRotationMonitor : MonoBehaviour
    {
        public PanelSettings PanelSettings;
        
        private void Update()
        {
            if (PanelSettings is null) return;
            
            var isPanelSettingsLandscape = PanelSettings.referenceResolution.x > PanelSettings.referenceResolution.y;
            var isScreenLandscape = Screen.width > Screen.height;

            if (isPanelSettingsLandscape == isScreenLandscape) return;
            
            if (isScreenLandscape)
            {
                PanelSettings.match = 1;
                PanelSettings.referenceResolution = new Vector2Int(800, 360);
            }
            else
            {
                PanelSettings.match = 0;
                PanelSettings.referenceResolution = new Vector2Int(360, 800);
            }
        }
    }
}