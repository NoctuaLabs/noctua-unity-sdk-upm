using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class BindAccountDialogPresenter : MonoBehaviour
    {
        private UIDocument _uiDoc;
        
        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>("BindAccountDialog");
            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            var styleSheet = Resources.Load<StyleSheet>("Noctua");
            
            _uiDoc = gameObject.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings;
            _uiDoc.visualTreeAsset = visualTree;
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);
        }


        public void Show()
        {
            
        }
    }
}