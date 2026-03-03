using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Monitors screen orientation changes and adjusts the UI Toolkit panel settings to match portrait or landscape mode.
    /// </summary>
    public class ScreenRotationMonitor : MonoBehaviour
    {
        /// <summary>The panel settings to update when orientation changes.</summary>
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