using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides shared color constants used throughout the Noctua SDK UI for consistent theming.
    /// </summary>
    public class ColorModule : MonoBehaviour
    {
        /// <summary>Standard white color.</summary>
        public static readonly Color white = Color.white;

        /// <summary>Blue color used for primary action buttons.</summary>
        public static readonly Color blueButton = new Color(0.2313726f, 0.509804f, 0.9647059f, 1.0f);

        /// <summary>Grey color used for inactive or muted UI elements.</summary>
        public static readonly Color greyInactive = new Color(0.4862745f, 0.4941176f, 0.5058824f, 1.0f);

        /// <summary>Red color used for error states and validation messages.</summary>
        public static readonly Color redError = new Color(0.6862745f, 0.1098039f, 0.2117647f, 1.0f);

        /// <summary>Green color used for success notifications.</summary>
        public static readonly Color greenSuccess = new Color(0.09019608f, 0.6392157f, 0.2901961f, 1.0f);

        /// <summary>Red color used for failure notifications.</summary>
        public static readonly Color redFailed = new Color(0.7882353f, 0.3058824f, 0.3058824f, 1.0f);
    }
}
