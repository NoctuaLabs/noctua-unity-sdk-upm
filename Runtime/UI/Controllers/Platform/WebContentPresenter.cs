using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Toggle = UnityEngine.UIElements.Toggle;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Defines the display mode for the web content presenter.
    /// </summary>
    internal enum ScreenMode
    {
        /// <summary>Web content fills the entire screen.</summary>
        FullScreen,

        /// <summary>Web content is displayed in a windowed dialog.</summary>
        Windowed
    }
    
    /// <summary>
    /// Presenter that displays web content in an embedded UniWebView, supporting fullscreen and windowed modes with a close button and "do not show again" toggle.
    /// </summary>
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

        /// <summary>
        /// Opens the web content URL in an embedded WebView (on mobile) or the system browser (in editor), and waits until the user closes it.
        /// </summary>
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
                if (url.Contains("open-native-browser=true"))
                {
                    View.visible = false;

                    uniWebView.Hide();
                    uniWebView.OnPageFinished -= PageFinished;
                    uniWebView.OnPageStarted -= PageStarted;
                    _closeButton.UnregisterCallback<PointerUpEvent>(Close);
                    _webViewAnchor.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);

                    Object.Destroy(uniWebView);
                    uniWebView = null;

                    Application.OpenURL(url);

                    // The web view is gone and its close button unregistered, so nothing else
                    // would ever complete the task — callers of OpenAsync would hang forever.
                    tcs.TrySetResult();
                }
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

            ApplyContent();
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

                _closeButton.UnregisterCallback<PointerUpEvent>(Close);
                _webViewAnchor.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);

                // Already torn down when the page handed off to the native browser
                if (uniWebView != null)
                {
                    uniWebView.Hide();
                    uniWebView.OnPageFinished -= PageFinished;
                    uniWebView.OnPageStarted -= PageStarted;

                    Object.Destroy(uniWebView);
                    uniWebView = null;
                }
            }
#else
            Application.OpenURL(Model.Url);
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        /// <summary>
        /// Sets per-open content. Kept out of <see cref="SetLayout"/>, which also runs on every
        /// geometry change — resetting the toggle there would untick it when the device rotates.
        /// </summary>
        private void ApplyContent()
        {
            _title.text = Model.Title;
            _noShowingToggle.style.display = Model.LastShown.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            _noShowingToggle.value = false;
        }

        private void SetLayout(UniWebView webView)
        {
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
            
            var pixelsPerPanelUnit = GetPixelsPerPanelUnit();

            if (pixelsPerPanelUnit <= 0f)
            {
                // Panel not laid out yet; GeometryChanged calls back once it is
                return;
            }

            ApplySafeAreaPadding(pixelsPerPanelUnit);

            // Both modes size the web view from its anchor, so it sits flush under the title bar
            webView.Frame = CalculateFrame(_webViewAnchor, pixelsPerPanelUnit);
        }

        /// <summary>
        /// Screen pixels per UI Toolkit panel unit, measured from the root's resolved width so it
        /// holds for any PanelSettings scale mode, in portrait and landscape alike.
        /// </summary>
        private float GetPixelsPerPanelUnit()
        {
            var rootWidth = View.panel?.visualTree.layout.width ?? float.NaN;

            return float.IsNaN(rootWidth) || rootWidth <= 0f ? 0f : Screen.width / rootWidth;
        }

        /// <summary>
        /// Keeps the full-screen title bar and page clear of notches, rounded corners and the
        /// navigation bar. Windowed popups are centered, so they need no inset.
        /// </summary>
        private void ApplySafeAreaPadding(float pixelsPerPanelUnit)
        {
            var isFullScreen = Model.ScreenMode == ScreenMode.FullScreen;
            var safeArea = Screen.safeArea;
            var navBar = GetNavigationBarInsets();

            // Screen.safeArea has a bottom-left origin; the panel's is top-left
            var top    = Screen.height - safeArea.yMax;
            var bottom = Mathf.Max(safeArea.yMin, navBar.bottom);
            var left   = Mathf.Max(safeArea.xMin, navBar.left);
            var right  = Mathf.Max(Screen.width - safeArea.xMax, navBar.right);

            _container.style.paddingTop    = isFullScreen ? top    / pixelsPerPanelUnit : 0f;
            _container.style.paddingBottom = isFullScreen ? bottom / pixelsPerPanelUnit : 0f;
            _container.style.paddingLeft   = isFullScreen ? left   / pixelsPerPanelUnit : 0f;
            _container.style.paddingRight  = isFullScreen ? right  / pixelsPerPanelUnit : 0f;
        }

        /// <summary>
        /// Pixel insets of the system navigation bar. Unity's <see cref="Screen.safeArea"/> only
        /// covers display cutouts, but when the game draws behind a visible navigation bar the
        /// bar overlays the web view's bottom edge (e.g. Crisp's message box).
        /// </summary>
        private RectOffset GetNavigationBarInsets()
        {
#if UNITY_ANDROID
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var window = activity.Call<AndroidJavaObject>("getWindow");
                using var decorView = window.Call<AndroidJavaObject>("getDecorView");
                using var insets = decorView.Call<AndroidJavaObject>("getRootWindowInsets");

                if (insets == null)
                {
                    return new RectOffset();
                }

                return new RectOffset(
                    insets.Call<int>("getSystemWindowInsetLeft"),
                    insets.Call<int>("getSystemWindowInsetRight"),
                    0, // status bar is hidden in-game; the notch comes from Screen.safeArea
                    insets.Call<int>("getSystemWindowInsetBottom")
                );
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to read navigation bar insets: {e.Message}");
            }
#endif
            return new RectOffset();
        }

        private static Rect CalculateFrame(VisualElement element, float pixelsPerPanelUnit)
        {
            var layout = element.worldBound;

            return new Rect(
                layout.x      * pixelsPerPanelUnit,
                layout.y      * pixelsPerPanelUnit,
                layout.width  * pixelsPerPanelUnit,
                layout.height * pixelsPerPanelUnit
            );
        }
#endif
    }
}