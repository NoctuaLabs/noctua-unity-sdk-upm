using UnityEngine;
using UnityEngine.UI;

namespace com.noctuagames.sdk.AdPlaceholder
{
    public class PlaceholderInterstitialAd : IAdPlaceholder
    {
        private GameObject canvasObject;

        public void Load()
        {
            // Optional preload logic
        }

        public void Show()
        {
            PlaceholderAssetSource.Instance.GetAdAssetResource(
                AdPlaceholderType.Interstitial,
                OnAdTextureLoaded
            );
        }

        private void OnAdTextureLoaded(Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogError("Failed to load interstitial ad texture.");
                return;
            }

            // Canvas setup
            canvasObject = new GameObject("InterstitialAdCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            // Full-screen panel
            GameObject panelObject = new GameObject("AdPanel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = Color.black;

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Ad image container with aspect ratio control
            GameObject imageContainer = new GameObject("AdImageContainer");
            imageContainer.transform.SetParent(panelObject.transform, false);
            RectTransform containerRect = imageContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.05f, 0.05f);
            containerRect.anchorMax = new Vector2(0.95f, 0.95f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            var aspectRatioFitter = imageContainer.AddComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectRatioFitter.aspectRatio = (float)texture.width / texture.height;

            GameObject imageObject = new GameObject("AdImage");
            imageObject.transform.SetParent(imageContainer.transform, false);
            var adImage = imageObject.AddComponent<RawImage>();
            adImage.texture = texture;
            adImage.raycastTarget = false;

            RectTransform imageRect = adImage.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            // Close button (top-right)
            GameObject closeButtonObj = new GameObject("CloseButton");
            closeButtonObj.transform.SetParent(panelObject.transform, false);
            var closeImage = closeButtonObj.AddComponent<Image>();
            closeImage.color = Color.white; // Optional: set to your icon or sprite

            var closeButton = closeButtonObj.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.ColorTint;
            closeButton.onClick.AddListener(Close);

            RectTransform closeRect = closeButtonObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.sizeDelta = new Vector2(80, 80); // size of the button
            closeRect.anchoredPosition = new Vector2(-20, -20); // margin from top-right

            // Optional: Add close icon
            closeImage.sprite = Resources.Load<Sprite>("CloseIcon");
        }

        public void Close()
        {
            if (canvasObject != null)
            {
                GameObject.Destroy(canvasObject);
                canvasObject = null;
            }
        }
    }
}
