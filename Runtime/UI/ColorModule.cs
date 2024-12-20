using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public class ColorModule : MonoBehaviour
    {
        public static readonly Color white = Color.white;
        public static readonly Color blueButton = new Color(0.2313726f, 0.509804f, 0.9647059f, 1.0f);
        public static readonly Color greyInactive = new Color(0.4862745f, 0.4941176f, 0.5058824f, 1.0f);
        public static readonly Color redError = new Color(0.6862745f, 0.1098039f, 0.2117647f, 1.0f);
    }
}
