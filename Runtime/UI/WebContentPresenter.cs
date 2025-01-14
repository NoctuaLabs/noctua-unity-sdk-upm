using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Toggle = UnityEngine.UIElements.Toggle;

namespace com.noctuagames.sdk.UI
{
    internal enum ScreenMode
    {
        FullScreen,
        Windowed
    }
    
    internal class WebContentPresenter : Presenter<WebContentModel>
    {
        private readonly NoctuaLogger _log = new(typeof(WebContentPresenter));
        private VisualElement _container;
        private Label _title;
        private VisualElement _closeButton;
        private VisualElement _webViewAnchor;
        private Toggle _noShowingToggle;

        private void OnEnable()
        {
            _title = View.Q<Label>("Title");
            _container = View.Q<VisualElement>("Container");
            _closeButton = View.Q<VisualElement>("CloseButton");
            _webViewAnchor = View.Q<VisualElement>("WebViewAnchor");
            _noShowingToggle = View.Q<Toggle>("NoShowingToggle");
            
            View.Q<VisualElement>("Root").style.alignItems = Align.Center;
            View.Q<VisualElement>("Root").style.justifyContent = Justify.Center;
        }

        public async UniTask OpenAsync()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var uniWebView = gameObject.AddComponent<UniWebView>();

            if (Application.platform == RuntimePlatform.Android)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.3");
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 14_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
            }

            var tcs = new UniTaskCompletionSource();

            void PageStarted(UniWebView webView, string url)
            {
                _log.Debug($"Page started: {url}");
            }

            void PageFinished(UniWebView webView, int statusCode, string url)
            {
                _log.Debug($"Page finished: {url}");
            }

            void Close(PointerUpEvent evt)
            {
                tcs.TrySetResult();
            }
            
            void GeometryChanged(GeometryChangedEvent evt)
            {
                SetLayout(uniWebView);
            }

            uniWebView.OnPageFinished += PageFinished;
            uniWebView.OnPageStarted += PageStarted;
            
            _closeButton.RegisterCallback<PointerUpEvent>(Close);
            _webViewAnchor.RegisterCallback<GeometryChangedEvent>(GeometryChanged);

            SetLayout(uniWebView);

            _log.Info($"Loading URL: {Model.Url}");
            uniWebView.SetShowSpinnerWhileLoading(true);
            uniWebView.Load(Model.Url);
            
            _log.Info("Showing WebView");
            View.visible = true;
            uniWebView.Show();

            try
            {
                await tcs.Task;
                
                if (_noShowingToggle.style.display == DisplayStyle.None)
                {
                    Model.LastShown = null;
                    
                    return;
                }
                
                if (_noShowingToggle.value)
                {
                    Model.LastShown = DateTime.Now.ToUniversalTime();
                    
                    return;
                }

                Model.LastShown = default;
            }
            finally
            {
                _log.Info("Closing WebView");
                
                View.visible = false;
                
                uniWebView.Hide();
                uniWebView.OnPageFinished -= PageFinished;
                uniWebView.OnPageStarted -= PageStarted;
                
                _closeButton.UnregisterCallback<PointerUpEvent>(Close);
                _webViewAnchor.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);

                Object.Destroy(uniWebView);
                uniWebView = null;
            }
#else
            Application.OpenURL(Model.Url);
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private void SetLayout(UniWebView webView)
        {
            _title.text = Model.Title;
            _noShowingToggle.style.display = Model.LastShown.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            _noShowingToggle.value = false;

            if (Screen.width > Screen.height)
            {
                _container.RemoveFromClassList("portrait");
                _container.AddToClassList("landscape");
            }
            else
            {
                _container.RemoveFromClassList("landscape");
                _container.AddToClassList("portrait");
            }
            
            if (Model.ScreenMode == ScreenMode.FullScreen)
            {
                _container.RemoveFromClassList("windowed");
                _container.AddToClassList("fullscreen");
            }
            else
            {
                _container.RemoveFromClassList("fullscreen");
                _container.AddToClassList("windowed");
            }
            
            if (Model.ScreenMode == ScreenMode.FullScreen)
            {
                webView.Frame = new Rect(0.05f*Screen.width, 0.1f*Screen.height, 0.9f*Screen.width, 0.8f*Screen.height);
            }
            else
            {
                webView.Frame = CalculateFrame(_webViewAnchor);
            }
        }

        private Rect CalculateFrame(VisualElement element)
        {
            var layout = element.LocalToWorld(new Rect(0, 0, element.layout.width, element.layout.height));
            var referenceResolution = gameObject.GetComponent<UIDocument>().panelSettings.referenceResolution;
            var isPortrait = Screen.width < Screen.height;
            var scale = isPortrait ? 1f*Screen.width / referenceResolution.x : 1f*Screen.height / referenceResolution.y;
            
            var adjustedLayout = new Rect(
                layout.position.x * scale,
                layout.position.y * scale,
                layout.width      * scale,
                layout.height     * scale
            );

            return adjustedLayout;
        }
#endif
    }
}