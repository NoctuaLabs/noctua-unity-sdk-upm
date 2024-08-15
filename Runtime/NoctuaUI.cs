using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class NoctuaUI
    {
        public static TemplateContainer CreateUIFromResource(string uxmlResourceName, string ussResourceName)
        {
            var visualTreeAsset = Resources.Load<VisualTreeAsset>(uxmlResourceName);
            if (visualTreeAsset is null)
            {
                throw new Exception($"Failed to load UXML resource  {uxmlResourceName}");
            }
            
            
            var styleSheet = Resources.Load<StyleSheet>(ussResourceName);
            
            if (styleSheet is null)
            {
                throw new Exception($"Failed to load USS resource {ussResourceName}");
            }

            var rootElement = visualTreeAsset.CloneTree();
            rootElement.styleSheets.Add(styleSheet);

            return rootElement;
        }
    }
}